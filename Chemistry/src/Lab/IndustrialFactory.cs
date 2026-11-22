using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Chemistry.IndustrialFactoryData;

namespace Chemistry
{
    internal sealed partial class IndustrialFactory
    {
        private static IndustrialFactory active;
        private readonly LabInventoryGrid grid;
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

        private IndustrialFactory(LabInventoryGrid grid) => this.grid = grid;
        private Route Recipe => Routes[routeIndex];
        private bool Installed => grid.StorageInventoryArea.childItems.Any(item => item.identifier == Frame);
        private bool Unlocked => LabEquipmentPanel.IsTierUnlocked(3, UnlockDay);
        private static bool Dragging => ItemMouseDragHandler.current?.IsDraggingItem == true;
        internal static IReadOnlyList<GameInventory> Inventories => active?.inventories ?? (IReadOnlyList<GameInventory>)Array.Empty<GameInventory>();
        internal static bool UsesMachine(GameItem item) => active != null
            && (active.preparation.childItem == item || active.dryer.childItem == item);

        internal static void Attach(GameItem frame)
        {
            var launcher = new PixelWindow(true, frame.name, WindowStyle.Machinery);
            launcher.openCallback = () =>
            {
                launcher.Hide();
                LabInventoryGrid current = LabInventoryGrid.OpenGrid;
                if (current != null && frame.parentInventory == current.StorageInventoryArea) Open();
                else OverlayHandler.current?.AppendLog(Loc.Pick("请先把厂房放入地下实验室储物格。",
                    "Place the workshop in underground lab storage first."), false);
            };
            frame.SetContentWindow(launcher);
            launcher.Hide();
        }

        internal static void Open()
        {
            LabInventoryGrid grid = LabInventoryGrid.OpenGrid;
            if (grid == null || Dragging || !EnsureCreated(grid)) return;
            LabPanel.Shutdown();
            AcidStation.Hide();
            NitricProduction.Hide();
            active.window.Show();
            active.window.Center();
            active.window.FixBounds();
            active.Refresh();
        }

        private static bool EnsureCreated(LabInventoryGrid grid)
        {
            if (active != null) return true;
            var factory = new IndustrialFactory(grid);
            try
            {
                factory.Build();
                active = factory;
                return true;
            }
            catch (Exception error)
            {
                factory.window?.Destroy();
                ChemistryMod.Log?.Warning("无法创建自动化厂房：" + error);
                return false;
            }
        }

        internal static void Close()
        {
            IndustrialFactory closing = active;
            active = null;
            if (closing == null) return;
            foreach (GameInventory inventory in closing.inventories)
            {
                foreach (GameItem item in inventory.childItems.ToArray()) item.Destroy();
            }
            closing.window.Destroy();
        }

        internal static void Update()
        {
            if (active == null || Dragging) return;
            long now = GameTicks.Now;
            if (now < active.nextTick) return;
            active.nextTick = now + 2;
            if (active.automatic) active.Tick(now);
            if (active.window.IsVisible()) active.Refresh();
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
            if (!Unlocked || Dragging || (id == Frame && Installed)) return;
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

        internal static string Capture()
        {
            if (active == null) return "";
            var factory = active;
            var slots = factory.inventories.SelectMany((inventory, index) => inventory.childItems
                .OrderBy(item => item.identifier, StringComparer.Ordinal).ThenBy(item => item.unitCount).ThenBy(ReagentBottle.SizeOf)
                .Select(item => $"{index},{item.identifier},{item.unitCount},{(ReagentBottle.IsBottle(item.identifier) ? ReagentBottle.SizeOf(item) : -1)}"
                    + (ReagentBottle.SavedComposition(item) is string composition ? "," + composition : "")));
            return $"1,{factory.routeIndex},{factory.stockLimit},{(factory.automatic ? 1 : 0)};" + string.Join(";", slots);
        }

        internal static void Restore(LabInventoryGrid grid, string layout)
        {
            if (string.IsNullOrEmpty(layout)) return;
            string[] records = layout.Split(';');
            string[] header = records[0].Split(',');
            if (header.Length != 4 || header[0] != "1" || !int.TryParse(header[1], out int recipe)
                || recipe < 0 || recipe >= Routes.Length || !int.TryParse(header[2], out int limit)
                || !EnsureCreated(grid)) return;
            var factory = active;
            factory.routeIndex = recipe;
            factory.stockLimit = Mathf.Clamp(limit, 2, MaxLimit);
            factory.automatic = header[3] == "1";
            var counts = LabStorage.Counts();
            foreach (GameInventory inventory in NitricProduction.Inventories)
            {
                foreach (GameItem item in inventory.childItems)
                {
                    if (counts.TryGetValue(item.identifier, out int available)) counts[item.identifier] = available - item.unitCount;
                }
            }
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
