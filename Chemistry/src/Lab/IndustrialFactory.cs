using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Chemistry.IndustrialFactoryData;

namespace Chemistry
{
    internal sealed partial class IndustrialFactory
    {
        private static readonly Dictionary<int, IndustrialFactory> Factories = new Dictionary<int, IndustrialFactory>();
        private readonly LabInventoryGrid grid;
        private int frameId;
        private readonly List<GameInventory> inventories = new List<GameInventory>();
        private int routeIndex;
        private int stockLimit = DefaultLimit;
        private bool automatic;
        private bool restoring;
        private const int PrepareTicks = 48;
        private const int RecoverTicks = 64;
        private long nextTick;
        private readonly Stage prepare = new Stage();
        private readonly Stage recover = new Stage();
        private string notice;

        private sealed class Stage
        {
            internal long Started;
            internal GameItem Sample;
            internal string State;
            internal void Reset() { Started = 0; Sample = null; }
        }

        private IndustrialFactory(LabInventoryGrid grid, int frameId)
        {
            this.grid = grid;
            this.frameId = frameId;
        }
        private Route Recipe => Routes[routeIndex];
        private bool Installed => frameId > 0 && grid.StorageInventoryArea.childItems.Any(item => item.uniqueId == frameId && item.identifier == Frame);
        private bool CanConstructFrame => !grid.StorageInventoryArea.childItems.Any(item => item.identifier == Frame);
        private bool Unlocked => Math.Max(1, StoreStation.GetDayCounter()) >= UnlockDay;
        private static bool Dragging => ItemMouseDragHandler.current?.IsDraggingItem == true;
        internal static IEnumerable<GameInventory> Inventories => Factories.Values.SelectMany(factory => factory.inventories);
        internal static IEnumerable<(int Owner, int Index, GameInventory Inventory)> OwnedInventories =>
            Factories.Values.OrderBy(factory => factory.frameId)
                .SelectMany(factory => factory.inventories.Select((inventory, index) => (factory.frameId, index, inventory)));
        internal static bool UsesMachine(GameItem item) => Factories.Values.Any(factory =>
            factory.preparation.childItem == item || factory.dryer.childItem == item);

        internal static void Attach(GameItem frame)
        {
            var launcher = new PixelWindow(true, frame.name, WindowStyle.Machinery);
            launcher.openCallback = () =>
            {
                launcher.Hide();
                LabInventoryGrid current = LabInventoryGrid.OpenGrid;
                if (current != null && frame.parentInventory == current.StorageInventoryArea) Open(frame);
                else OverlayHandler.current?.AppendLog(Loc.Pick("请先把厂房放入地下实验室储物格。",
                    "Place the workshop in underground lab storage first."), false);
            };
            frame.SetContentWindow(launcher);
            launcher.Hide();
        }

        internal static void Open()
        {
            Open(LabInventoryGrid.OpenGrid?.StorageInventoryArea.childItems.FirstOrDefault(item => item.identifier == Frame));
        }

        private static void Open(GameItem frame)
        {
            LabInventoryGrid grid = LabInventoryGrid.OpenGrid;
            if (grid == null || Dragging) return;
            IndustrialFactory factory = GetOrCreate(grid, frame?.GetUniqueID() ?? 0);
            if (factory == null) return;
            LabPanel.Shutdown();
            AcidStation.Hide();
            NitricProduction.Hide();
            factory.window.Show();
            factory.window.Center();
            factory.window.FixBounds();
            factory.Refresh();
        }

        private static IndustrialFactory GetOrCreate(LabInventoryGrid grid, int id, bool claimUnbound = true)
        {
            if (Factories.TryGetValue(id, out IndustrialFactory factory)) return factory;
            if (claimUnbound && id > 0 && Factories.TryGetValue(0, out factory))
            {
                Factories.Remove(0);
                factory.frameId = id;
                Factories.Add(id, factory);
                return factory;
            }
            factory = new IndustrialFactory(grid, id);
            try
            {
                factory.Build();
                Factories.Add(id, factory);
                return factory;
            }
            catch (Exception error)
            {
                factory.window?.Destroy();
                ChemistryMod.Log?.Warning("无法创建自动化厂房：" + error);
                return null;
            }
        }

        internal static void Close()
        {
            var closing = Factories.Values.ToArray();
            Factories.Clear();
            foreach (IndustrialFactory factory in closing)
            {
                foreach (GameInventory inventory in factory.inventories)
                {
                    foreach (GameItem item in inventory.childItems.ToArray())
                    {
                        if (item == null || (item.contentWindow != null && item.contentWindow.handler == null)) continue;
                        item.Destroy();
                    }
                }
                if (factory.window?.handler != null) factory.window.Destroy();
            }
        }

        internal static void Update()
        {
            if (Factories.Count == 0 || Dragging) return;
            long now = GameTicks.Now;
            foreach (IndustrialFactory factory in Factories.Values)
            {
                if (now < factory.nextTick) continue;
                factory.nextTick = now + 2;
                if (factory.automatic) factory.Tick(now);
                if (factory.window.IsVisible()) factory.Refresh();
            }
        }

        private string HardwareBlocker()
        {
            if (!Unlocked) return Loc.Pick("第 12 天解锁厂房", "Workshop unlocks on day 12");
            if (!Installed) return Loc.Pick("先将厂房放进实验室储物格", "Install a workshop in lab storage first");
            if (transport.childItem?.identifier != Transport) return Loc.Pick("缺少输送管路模块", "Fit a transport manifold");
            return null;
        }

        private void Tick(long now)
        {
            if (HardwareBlocker() != null)
            {
                prepare.Reset();
                recover.Reset();
                return;
            }
            GameItem feed = First(stock, Recipe.Feed);
            GameItem passed = First(buffer, Recipe.Intermediate);
            GameItem bottle = utilities.childItems.FirstOrDefault(item => ReagentBottle.IsGlassEmpty(item.identifier));
            bool hasPower = First(utilities, NitricProductionData.Power) != null;
            bool bufferFull = Units(buffer) >= MaxLimit;
            bool finishedFull = Units(products) >= stockLimit || liquids.childItems.Count >= stockLimit || returns.childItems.Count >= stockLimit;

            string prepareBlocker = preparation.childItem?.identifier != Recipe.PrepTool
                ? Loc.Pick("缺少预处理设备", "Preparation machine missing")
                : feed == null ? Loc.Pick("等待原料", "Waiting for feed")
                : bufferFull ? Loc.Pick("中转缓冲已满", "Buffer full")
                : !hasPower ? Loc.Pick("等待动力匣", "Waiting for a power pack") : null;
            string recoveryBlocker = dryer.childItem?.identifier != Dryer
                ? Loc.Pick("缺少干燥箱", "Drying oven missing")
                : passed == null ? Loc.Pick("等待管路供料", "Waiting for transferred feed")
                : finishedFull ? Loc.Pick("成品仓达到上限", "Output stock limit reached")
                : bottle == null ? Loc.Pick("等待空玻璃瓶", "Waiting for empty glass")
                : null;

            Advance(recover, passed, recoveryBlocker, now, RecoverTicks, () => Recover(passed, bottle));
            Advance(prepare, feed, prepareBlocker, now, PrepareTicks, () =>
            {
                GameItem power = First(utilities, NitricProductionData.Power);
                return power != null && MachineBatch.TryCommit(grid, new List<(GameItem, int)> { (feed, 1), (power, 1) },
                    (Recipe.Intermediate, buffer));
            });
        }

        private bool Recover(GameItem sample, GameItem bottle)
        {
            var inputs = new List<(GameItem, int)> { (sample, 1), (bottle, 1) };
            return MachineBatch.TryCommitMeasured(grid, inputs,
                new MachineBatch.Product(Recipe.Solid, products),
                new MachineBatch.Product(LabMixtureData.Water, liquids, LabMixtureData.Portion, ReagentBottle.SizeOf(bottle)),
                new MachineBatch.Product(ReagentBottle.GlassEmpties[LabMixtureData.SmallBottle], returns));
        }

        private static void Advance(Stage stage, GameItem sample, string blocker, long now, int ticks, Func<bool> commit)
        {
            if (blocker != null)
            {
                stage.Reset();
                stage.State = blocker;
                return;
            }
            if (stage.Sample != sample)
            {
                stage.Sample = sample;
                stage.Started = now;
            }
            if (now - stage.Started < ticks)
            {
                stage.State = Loc.Pick("运行中", "Running");
                return;
            }
            bool completed = commit();
            stage.State = completed ? Loc.Pick("已完成一批", "Batch transferred")
                : Loc.Pick("产物无处可放或保存失败", "Output blocked or save failed");
            stage.Reset();
        }

        private static GameItem First(GameInventory inventory, string id) =>
            inventory.childItems.Find(item => item.identifier == id && item.unitCount > 0);
        private static int Units(GameInventory inventory) => inventory.childItems.Sum(item => item.unitCount);

        private void ChangeSettings(int recipe, int limit, bool run)
        {
            if (Dragging) return;
            var before = (routeIndex, stockLimit, automatic);
            (routeIndex, stockLimit, automatic) = (recipe, limit, run);
            if (!grid.TrySaveMachineBatch())
            {
                (routeIndex, stockLimit, automatic) = before;
                notice = Loc.Pick("设置未能保存", "Could not save settings");
                return;
            }
            notice = null;
            prepare.Reset();
            recover.Reset();
        }

        private void Construct(string id, int parts)
        {
            if (!Unlocked || Dragging || (id == Frame && !CanConstructFrame)) return;
            var inputs = new List<(GameItem, int)>();
            if (!MachineBatch.AddInputs(inputs, grid.StorageInventoryArea, NitricProductionData.Parts, parts))
            {
                notice = Loc.Pick($"储物格需有 {parts} 个回收机件", $"Lab storage needs {parts} salvaged parts");
                return;
            }
            notice = MachineBatch.TryCommit(grid, inputs, (id, grid.StorageInventoryArea))
                ? Loc.Pick("已组装，物品在实验室储物格", "Assembled and placed in lab storage")
                : Loc.Pick("组装失败，请检查储物空间", "Assembly failed. Check storage space.");
        }

        internal static string Capture() => string.Join("^", Factories.Values.OrderBy(factory => factory.frameId).Select(factory => factory.CaptureFactory()));

        private string CaptureFactory()
        {
            var slots = inventories.SelectMany((inventory, index) => inventory.childItems
                .OrderBy(item => item.identifier, StringComparer.Ordinal).ThenBy(item => item.unitCount).ThenBy(ReagentBottle.SizeOf)
                .Select(item => $"{index},{item.identifier},{item.unitCount},{(ReagentBottle.IsBottle(item.identifier) ? ReagentBottle.SizeOf(item) : -1)}"
                    + (ReagentBottle.SavedComposition(item) is string composition ? "," + composition : "")));
            return $"2,{routeIndex},{stockLimit},{(automatic ? 1 : 0)},{frameId};" + string.Join(";", slots);
        }

        internal static void Restore(LabInventoryGrid grid, string layout, HashSet<(int Id, string Item)> owners)
        {
            if (string.IsNullOrEmpty(layout)) return;
            var counts = LabStorage.Counts();
            foreach (GameInventory inventory in NitricProduction.Inventories)
            {
                foreach (GameItem item in inventory.childItems)
                {
                    if (counts.TryGetValue(item.identifier, out int available)) counts[item.identifier] = available - item.unitCount;
                }
            }
            foreach (string entry in layout.Split('^'))
                RestoreFactory(grid, entry, counts, owners);
        }

        private static void RestoreFactory(LabInventoryGrid grid, string layout, Dictionary<string, int> counts,
            HashSet<(int Id, string Item)> owners)
        {
            string[] records = layout.Split(';');
            string[] header = records[0].Split(',');
            int id = 0;
            bool legacy = header.Length == 4 && header[0] == "1";
            if (!legacy && (header.Length != 5 || header[0] != "2" || !int.TryParse(header[4], out id) || id < 0)) return;
            if (id > 0 && !owners.Contains((id, Frame))) return;
            if (!int.TryParse(header[1], out int recipe) || recipe < 0 || recipe >= Routes.Length
                || !int.TryParse(header[2], out int limit)) return;
            // 读档保留未绑定的旧厂房，等玩家打开一座实体厂房再认领。
            IndustrialFactory factory = GetOrCreate(grid, id, claimUnbound: false);
            if (factory == null) return;
            factory.routeIndex = recipe;
            factory.stockLimit = Mathf.Clamp(limit, 2, MaxLimit);
            factory.automatic = header[3] == "1";
            factory.restoring = true;
            try
            {
                foreach (string record in records.Skip(1)) factory.RestoreSlot(record, counts);
            }
            catch (Exception error)
            {
                ChemistryMod.Log?.Warning("还原自动化厂房库存失败：" + error);
            }
            finally
            {
                factory.restoring = false;
            }
        }

        internal static void BindLegacyFrame()
        {
            if (!Factories.TryGetValue(0, out var unbound)) return;
            var frames = unbound.grid.StorageInventoryArea.childItems.Where(item => item.identifier == Frame).Take(2).ToArray();
            if (frames.Length == 1) GetOrCreate(unbound.grid, frames[0].GetUniqueID());
        }

        private void RestoreSlot(string record, Dictionary<string, int> counts)
        {
            string[] fields = record.Split(',');
            if (fields.Length is < 4 or > 5 || !int.TryParse(fields[0], out int index) || index < 0 || index >= inventories.Count
                || !int.TryParse(fields[2], out int quantity) || quantity <= 0
                || !int.TryParse(fields[3], out int size) || size < -1 || size > ReagentBottle.Bulk
                || !counts.TryGetValue(fields[1], out int available) || available < quantity) return;
            GameItem item = ModHelper.SpawnItem(fields[1]);
            if (item == null) return;
            try
            {
                ModHelper.SetItemOwnerShip(item, true);
                if (size >= 0) ReagentBottle.SetSize(item, ReagentBottle.RestoreSavedSize(item.identifier, size, quantity));
                item.SetUnitCount(quantity);
                if (fields.Length == 5) ReagentBottle.RestoreComposition(item, fields[4]);
                GameInventory destination = inventories[index];
                SlotMarker slot = destination.TryFindOneValidInventorySlot(item);
                if (slot?.inventory == destination && slot.numTransfer >= quantity && slot.TryAcceptOnce(quantity) == quantity)
                    counts[fields[1]] -= quantity;
            }
            finally
            {
                if (item.parentInventory == null) item.Destroy();
            }
        }
    }
}
