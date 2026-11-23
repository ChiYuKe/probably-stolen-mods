using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Chemistry
{
    internal sealed partial class LabInventoryGrid
    {
        private const string MainStorageArea = "lab";
        private Dictionary<(string Area, string Item), Queue<int>> restoringMachineIds;

        internal static bool TracksMachineIdentity(string id) =>
            id == IndustrialFactoryData.Frame || id == RefiningStation.ItemId || LabEquipmentPanel.IsEquipment(id);

        internal static bool IsFixedStation(string id) => id == RefiningStation.ItemId || id == AcidStation.ItemId;

        private static GameInventory ResolveSavedSlot(LabEquipmentPanel.SavedItem item)
        {
            if (item.MachineId != 0) return LabEquipmentPanel.GetSavedSlot(item);
            int index = (int)item.Slot - 1;
            if (item.Equipment == AcidStation.ItemId) return AcidStation.RestoreSlot(index);
            var slots = RefiningStation.Inventories;
            return index >= 0 && index < slots.Count ? slots[index] : null;
        }

        private static string WarehouseArea(int index) => "warehouse" + index.ToString(CultureInfo.InvariantCulture);

        private static IEnumerable<(string Area, GameInventory Inventory)> ProductionAreas()
        {
            var nitric = NitricProduction.Inventories;
            for (int i = 0; i < nitric.Count; i++)
                yield return ("nitric" + i.ToString(CultureInfo.InvariantCulture), nitric[i]);

            foreach (var (owner, index, grid) in IndustrialFactory.OwnedInventories)
                yield return (owner == 0 ? FormattableString.Invariant($"factory{index}")
                    : FormattableString.Invariant($"factory{owner}:{index}"), grid);
        }

        private string CaptureEquipmentLayout()
        {
            var records = new List<string>();
            LabEquipmentPanel.CaptureContents(records);
            CaptureMachineIdentities(records, MainStorageArea, inventory);
            CaptureStation(records, RefiningStation.ItemId, 0, RefiningStation.Inventories);
            CaptureStation(records, AcidStation.ItemId, 0, AcidStation.Inventories);
            foreach (GameItem station in PortableRefiners())
                CaptureStation(records, RefiningStation.ItemId, station.GetUniqueID(), RefiningStation.PortableSlots(station));
            foreach (var (area, grid) in ProductionAreas())
                CaptureMachineIdentities(records, area, grid);
            for (int i = 0; i < warehouseGrids.Count; i++)
                CaptureMachineIdentities(records, WarehouseArea(i), warehouseGrids[i]);
            return string.Join("|", records);
        }

        private static void CaptureStation(List<string> records, string id, int instance, IReadOnlyList<GameInventory> slots)
        {
            for (int i = 0; i < slots.Count; i++)
                LabEquipmentPanel.CaptureSlot(records, id, instance, i + 1, slots[i]);
        }

        private IEnumerable<GameItem> PortableRefiners() =>
            PageGrids().Cast<GameInventory>().Concat(LabEquipmentPanel.Inventories)
                .SelectMany(grid => grid.childItems).Where(item => item.identifier == RefiningStation.ItemId);

        private static bool IsPortableRefinerSlot(LabEquipmentPanel.SavedItem item) =>
            item.MachineId > 0 && item.Equipment == RefiningStation.ItemId;

        private void RestorePortableRefiners(string[] records)
        {
            var slots = LabEquipmentPanel.ReadContents(records).Where(IsPortableRefinerSlot).ToList();
            if (slots.Count == 0) return;
            var machines = PortableRefiners().ToDictionary(item => item.uniqueId, RefiningStation.PortableSlots);
            foreach (var item in slots)
            {
                try
                {
                    int index = (int)item.Slot - 1;
                    // 便携反应台的内置窗口由物品拥有，里面的物品没有计入实验页总数。
                    if (machines.TryGetValue(item.MachineId, out var inventories) && index < inventories.Count
                        && RestoreEquipmentItem(inventories[index], item)) continue;
                    if (!RestoreEquipmentItem(inventory, item))
                        ChemistryMod.Log?.Warning("无法将便携提炼反应台内容物退回实验页：" + item.Id);
                }
                catch (Exception error)
                {
                    ChemistryMod.Log?.Warning("恢复便携提炼反应台槽失败：" + item.Id + " " + error.Message);
                }
            }
        }

        private static void CaptureMachineIdentities(List<string> records, string area, GameInventory grid)
        {
            foreach (GameItem item in grid.childItems)
            {
                if (TracksMachineIdentity(item.identifier))
                    records.Add(FormattableString.Invariant($"@{area},{item.identifier},{item.GetUniqueID()}"));
            }
        }

        private HashSet<(int Id, string Item)> RestoreMachineIdentities(string[] records)
        {
            restoringMachineIds = new Dictionary<(string, string), Queue<int>>();
            var owners = new HashSet<(int, string)>();
            foreach (GameItem item in StoreInventory.AllItems())
            {
                if (item.uniqueId > 0 && TracksMachineIdentity(item.identifier))
                    owners.Add((item.uniqueId, item.identifier));
            }
            foreach (string record in records)
            {
                if (!record.StartsWith("@", StringComparison.Ordinal)) continue;
                string[] fields = record.Split(',');
                if (fields.Length != 3 || !TracksMachineIdentity(fields[1])
                    || !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int id)
                    || id <= 0 || !owners.Add((id, fields[1]))) continue;
                var key = (fields[0].Substring(1), fields[1]);
                if (!restoringMachineIds.TryGetValue(key, out Queue<int> ids))
                    restoringMachineIds.Add(key, ids = new Queue<int>());
                ids.Enqueue(id);
            }
            return owners;
        }

        private void RestoreProductionMachineIds()
        {
            foreach (var (area, grid) in ProductionAreas())
            {
                foreach (GameItem item in grid.childItems)
                {
                    int id = TakeMachineId(area, item.identifier);
                    if (id > 0) item.uniqueId = id;
                }
            }
        }

        private int TakeMachineId(string area, string identifier) =>
            restoringMachineIds != null && restoringMachineIds.TryGetValue((area, identifier), out Queue<int> ids)
                && ids.Count > 0 ? ids.Dequeue() : 0;
    }
}
