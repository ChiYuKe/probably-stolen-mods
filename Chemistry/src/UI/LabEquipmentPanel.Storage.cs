using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Chemistry
{
    internal sealed partial class LabEquipmentPanel
    {
        internal enum SlotKind
        {
            Battery = 1,
            Feed,
            Receiver,
            Output,
            Byproduct
        }

        internal sealed class SavedItem
        {
            public string Equipment;
            public int MachineId;
            public SlotKind Slot;
            public string Id;
            public int ItemInstanceId;
            public int Units;
            public int? Energy;
            public int BottleSize;
            public string Composition;
        }

        internal static void CaptureContents(List<string> records)
        {
            // 只采还活着的面板：读档残留的已销毁面板会把槽内物品重复记进存档。
            foreach (LabEquipmentPanel panel in LivePanels.OrderBy(panel => panel.machineId))
            {
                foreach (var (kind, inventory) in panel.Slots())
                    CaptureSlot(records, panel.equipment.Key, panel.machineId, (int)kind, inventory);
            }
        }

        internal static void CaptureSlot(List<string> records, string equipment, int machineId, int slot, GameInventory inventory)
        {
            foreach (GameItem item in inventory.childItems)
            {
                int? energy = LabInventoryGrid.BatteryEnergy(item);
                bool bottle = ReagentBottle.IsBottle(item.identifier);
                int size = bottle ? ReagentBottle.SizeOf(item) : -1;
                string composition = bottle ? ReagentBottle.SavedComposition(item) : null;
                string encoded = string.IsNullOrEmpty(composition)
                    ? "" : Convert.ToBase64String(Encoding.UTF8.GetBytes(composition));
                int instance = LabInventoryGrid.TracksMachineIdentity(item.identifier) && item.unitCount == 1 ? item.GetUniqueID() : 0;
                records.Add(FormattableString.Invariant(
                    $"{equipment},{slot},{item.identifier},{item.unitCount},{energy},{size},{encoded},{machineId},{instance}"));
            }
        }

        internal static bool IsEquipment(string id)
        {
            const string prefix = ChemistryCatalog.ItemPrefix + "tool_";
            return id != null && id.StartsWith(prefix, StringComparison.Ordinal)
                && LabEquipmentData.Find(id.Substring(prefix.Length)) != null;
        }

        internal static IEnumerable<SavedItem> ReadContents(IEnumerable<string> records)
        {
            foreach (string record in records)
            {
                string[] fields = record.Split(',');
                // 旧版仅按型号记录槽位，无法确定原来的仪器，物品会由库存总量补回实验页。
                if (fields.Length != 9 || fields[0].Length == 0 || fields[2].Length == 0
                    || !ReadInt(fields[1], out int slot) || slot <= 0
                    || !ReadInt(fields[3], out int units) || units <= 0
                    || !ReadInt(fields[5], out int size)
                    || !ReadInt(fields[7], out int machine) || machine < 0
                    || (machine == 0 ? !LabInventoryGrid.IsFixedStation(fields[0]) : slot > (int)SlotKind.Byproduct)
                    || !ReadInt(fields[8], out int instance) || instance < 0
                    || (instance > 0 && (units != 1 || !LabInventoryGrid.TracksMachineIdentity(fields[2])))) continue;
                int? energy = null;
                if (fields[4].Length > 0)
                {
                    if (!ReadInt(fields[4], out int charge)) continue;
                    energy = charge;
                }
                string composition = null;
                if (fields[6].Length > 0)
                {
                    try { composition = Encoding.UTF8.GetString(Convert.FromBase64String(fields[6])); }
                    catch (FormatException) { continue; }
                }
                yield return new SavedItem
                {
                    Equipment = fields[0], MachineId = machine, Slot = (SlotKind)slot,
                    Id = fields[2], ItemInstanceId = instance,
                    Units = units, Energy = energy, BottleSize = size, Composition = composition
                };
            }
        }

        private static bool ReadInt(string value, out int result) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        internal static GameInventory GetSavedSlot(SavedItem saved)
        {
            LabEquipmentDef def = LabEquipmentData.Find(saved.Equipment);
            if (def == null) return null;
            ToolDef tool = ChemistryCatalog.FindTool(def.Key);
            string title = tool == null ? def.Key : Loc.Pick(tool.NameZh, tool.NameEn);
            LabEquipmentPanel panel = GetOrCreate(saved.MachineId, def, title);
            return panel == null ? null : panel.Slots().FirstOrDefault(slot => slot.Kind == saved.Slot).Inventory;
        }
    }
}
