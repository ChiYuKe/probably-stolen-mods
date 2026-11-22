using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Chemistry.NitricProductionData;

namespace Chemistry
{
    internal sealed partial class NitricProduction
    {
        internal const int BatchLimit = 4;
        private const int BufferLimit = 8;
        private const int PartPrice = 18;
        private static NitricProduction active;

        private readonly LabInventoryGrid grid;
        private readonly List<GameInventory> inventories = new List<GameInventory>();
        private readonly Step[] steps;
        private Step visibleStep;
        private GameGridInventory works;
        private GameGridInventory stock;
        private GameGridInventory utilities;
        private GameGridInventory warehouse;
        private bool automatic;
        private bool restoring;
        private int limit = BufferLimit;
        private int feedGrade;
        private int produced;
        private long operatingTicks;
        private long lastTick;
        private string notice;

        private sealed class Step
        {
            internal string Zh;
            internal string En;
            internal string Tool;
            internal string Feed;
            internal string Product;
            internal GameGridInventory Input;
            internal GameGridInventory Output;
            internal GameSlotInventory ToolSlot;
            internal GameGridInventory Bottles;
            internal int Requested = 1;
            internal int Remaining;
            internal long Started;
            internal long Finish;
            internal string Failure;
            internal string Name => Loc.Pick(Zh, En);
        }

        private NitricProduction(LabInventoryGrid grid)
        {
            this.grid = grid;
            steps = new[]
            {
                new Step { Zh = "预处理", En = "Prepare feed", Tool = "beaker", Product = CleanFeed },
                new Step { Zh = "转化", En = "Convert", Tool = "hotplate_stirrer", Feed = CleanFeed, Product = Crude },
                new Step { Zh = "过滤", En = "Filter", Tool = "filter_funnel", Feed = Crude, Product = Refined },
                new Step { Zh = "灌装", En = "Fill", Feed = Refined, Product = NitricProductionData.Product.ItemId },
            };
            visibleStep = steps[0];
            lastTick = GameTicks.Now;
        }

        internal static IReadOnlyList<GameInventory> Inventories => active?.inventories ?? (IReadOnlyList<GameInventory>)Array.Empty<GameInventory>();
        private bool Has(string id) => grid.FindItem(id) != null;
        private bool BatchUnlocked => Has(LabModule);
        private bool FactoryUnlocked => BatchUnlocked && Has(PipeModule);
        private static bool Dragging => ItemMouseDragHandler.current?.IsDraggingItem == true;

        internal static bool Open(Action back = null, bool control = false)
        {
            LabInventoryGrid grid = LabInventoryGrid.OpenGrid;
            if (grid == null || Dragging)
            {
                return false;
            }
            if (!EnsureCreated(grid))
            {
                return false;
            }
            if (back != null)
            {
                active.back = back;
            }
            AcidStation.Hide();
            LabPanel.Shutdown();
            active.Show(control ? null : active.visibleStep);
            return true;
        }

        private static bool EnsureCreated(LabInventoryGrid grid)
        {
            if (active != null) return true;
            var line = new NitricProduction(grid);
            try
            {
                line.Build();
                active = line;
                return true;
            }
            catch (Exception error)
            {
                line.DestroyWindows();
                ChemistryMod.Log?.Warning("创建硝酸生产线失败：" + error);
                return false;
            }
        }

        internal static string Capture()
        {
            if (active == null) return "";
            var placements = active.inventories.SelectMany((inventory, index) => inventory.childItems
                .OrderBy(item => item.identifier, StringComparer.Ordinal).ThenBy(item => item.unitCount)
                .ThenBy(ReagentBottle.SizeOf)
                .Select(item => index + "," + item.identifier + "," + item.unitCount
                    + (ReagentBottle.IsBottle(item.identifier) ? "," + ReagentBottle.SizeOf(item) : "")
                    + (ReagentBottle.SavedComposition(item) is string composition ? "," + composition : ""))).ToArray();
            if (placements.Length == 0 && !active.automatic && active.limit == BufferLimit && active.feedGrade == 0 &&
                active.steps.All(step => step.Remaining == 0))
            {
                return "";
            }
            string header = (active.automatic ? "1" : "0") + "," + active.limit + "," + active.feedGrade
                + ",0," + string.Join(",", active.steps.Select(step => step.Remaining));
            return header + ";" + string.Join(";", placements);
        }

        internal static void Restore(LabInventoryGrid grid, string layout)
        {
            if (string.IsNullOrEmpty(layout) || !EnsureCreated(grid)) return;
            string[] records = layout.Split(';');
            string[] header = records[0].Split(',');
            if (header.Length != 4 + active.steps.Length) return;
            active.automatic = header[0] == "1";
            if (int.TryParse(header[1], out int target)) active.limit = Mathf.Clamp(target, 2, BufferLimit);
            if (int.TryParse(header[2], out int grade)) active.feedGrade = Mathf.Clamp(grade, 0, 2);
            for (int i = 0; i < active.steps.Length; i++)
            {
                if (int.TryParse(header[4 + i], out int remaining))
                {
                    active.steps[i].Remaining = Mathf.Clamp(remaining, 0, BatchLimit);
                }
            }

            var available = LabStorage.Counts();
            active.restoring = true;
            try
            {
                foreach (string record in records.Skip(1))
                {
                    string[] fields = record.Split(',');
                    int size = -1;
                    if (fields.Length < 3 || fields.Length > 5 || !int.TryParse(fields[0], out int area) ||
                        area < 0 || area >= active.inventories.Count || !int.TryParse(fields[2], out int units) ||
                        (fields.Length >= 4 && (!int.TryParse(fields[3], out size) || size < 0 || size > ReagentBottle.Bulk)) ||
                        units <= 0 || !available.TryGetValue(fields[1], out int stock) || stock < units)
                    {
                        continue;
                    }
                    GameItem item = ModHelper.SpawnItem(fields[1]);
                    if (item == null) continue;
                    try
                    {
                        ModHelper.SetItemOwnerShip(item, true);
                        if (size >= 0)
                        {
                            ReagentBottle.SetSize(item, ReagentBottle.RestoreSavedSize(item.identifier, size, units));
                        }
                        item.SetUnitCount(units);
                        if (fields.Length == 5) ReagentBottle.RestoreComposition(item, fields[4]);
                        GameInventory destination = active.inventories[area];
                        SlotMarker slot = destination.TryFindOneValidInventorySlot(item);
                        if (slot?.inventory == destination && slot.numTransfer >= units && slot.TryAcceptOnce(units) == units)
                        {
                            available[fields[1]] -= units;
                        }
                    }
                    finally
                    {
                        if (item.parentInventory == null) item.Destroy();
                    }
                }
            }
            catch (Exception error)
            {
                ChemistryMod.Log?.Warning("还原硝酸生产线物料失败：" + error);
            }
            finally
            {
                active.restoring = false;
            }
        }

        internal static void Hide() => active?.HideWindows();

        internal static void Close()
        {
            NitricProduction line = active;
            active = null;
            line?.DestroyWindows();
        }

        internal static void Update()
        {
            if (active != null && !Dragging)
            {
                long now = GameTicks.Now;
                if (now - active.lastTick >= 2) active.Tick(now);
                active.Refresh();
            }
        }

        private void Tick(long now)
        {
            if (automatic && !FactoryUnlocked)
            {
                StopFactory();
                notice = Loc.Pick("联机设备已移出实验室。", "Line equipment was removed from the lab.");
            }

            if (automatic)
            {
                operatingTicks += now - lastTick;
            }
            lastTick = now;

            // Drain downstream buffers first so upstream stations can make use of freed capacity.
            for (int index = steps.Length - 1; index >= 0; index--)
            {
                Step step = steps[index];
                if ((!automatic && step.Remaining == 0) || step.Finish > now)
                {
                    continue;
                }
                if (!TryInputs(index, out var inputs, out _))
                {
                    step.Finish = 0;
                    continue;
                }
                if (step.Finish == 0)
                {
                    step.Started = now;
                    step.Finish = now + (automatic ? 28 : BatchUnlocked ? 48 : 96);
                    continue;
                }

                step.Finish = 0;
                GameInventory destination = Destination(index);
                if (!automatic) step.Remaining--;
                bool committed = index == steps.Length - 1
                    ? MachineBatch.TryCommitBottled(grid, inputs,
                        inputs.Find(input => ReagentBottle.IsGlassEmpty(input.Item.identifier)).Item, step.Product, destination)
                    : MachineBatch.TryCommit(grid, inputs, (step.Product, destination));
                if (!committed)
                {
                    step.Failure = Loc.Pick("结算失败。检查槽位和实验室容量。", "Could not finish. Check slots and lab capacity.");
                    step.Remaining = 0;
                    if (automatic)
                    {
                        notice = step.Name + " · " + step.Failure;
                        StopFactory();
                    }
                    continue;
                }

                step.Failure = null;
                if (index == steps.Length - 1)
                {
                    if (automatic) produced++;
                    StoryService.OnLabSynthesized(NitricProductionData.Product);
                }
                LabPanel.RefreshFromInventory();
            }
        }

        private GameInventory Destination(int index) => !automatic ? steps[index].Output
            : index == steps.Length - 1 ? warehouse : steps[index + 1].Input;

        private bool TryInputs(int index, out List<(GameItem Item, int Units)> inputs, out string reason)
        {
            Step step = steps[index];
            inputs = null;
            reason = null;
            GameInventory destination = Destination(index);
            if (automatic && Units(warehouse) >= limit)
            {
                reason = Loc.Pick("已达成品库存上限", "Finished stock target reached");
            }
            else if (Units(destination) >= (automatic ? BufferLimit : BatchUnlocked ? BatchLimit : 1))
            {
                reason = Loc.Pick("下游缓冲区已满", "Output buffer full");
            }
            else if (!automatic && !BatchUnlocked && step.Tool != null &&
                step.ToolSlot.childItem?.identifier != ChemistryCatalog.ToolItemId(step.Tool))
            {
                reason = Loc.Pick("装入所需工具", "Fit the required tool");
            }
            if (reason != null)
            {
                return false;
            }

            GameInventory source = automatic && index == 0 ? stock : step.Input;
            string feed = step.Feed ?? source.childItems.Select(item => item.identifier).Distinct()
                .Where(id =>
                {
                    int units = NitricProductionData.FeedUnits(id, automatic);
                    return units > 0 && (feedGrade == 0 || feedGrade == units);
                })
                .OrderBy(id => NitricProductionData.FeedUnits(id, automatic))
                .FirstOrDefault(id => Units(source, id) >= NitricProductionData.FeedUnits(id, automatic));
            if (feed == null || Units(source, feed) == 0)
            {
                reason = index == 0 ? Loc.Pick("缺少足量同批含氮样品", "Need more nitrogen samples from the same lot")
                    : Loc.Pick("等待上游物料", "Waiting for upstream material");
                return false;
            }
            GameItem emptyBottle = index == steps.Length - 1
                ? (automatic ? utilities : step.Bottles).childItems.FirstOrDefault(item => ReagentBottle.IsGlassEmpty(item.identifier))
                : null;
            if (index == steps.Length - 1 && emptyBottle == null)
            {
                reason = Loc.Pick("缺少空试剂瓶", "No empty reagent bottles");
                return false;
            }
            if (automatic && index == 1 && Units(utilities, Power) == 0)
            {
                reason = Loc.Pick("缺少工业动力匣", "No industrial power packs");
                return false;
            }

            inputs = new List<(GameItem Item, int Units)>();
            MachineBatch.AddInputs(inputs, source, feed, index == 0 ? NitricProductionData.FeedUnits(feed, automatic) : 1);
            if (emptyBottle != null)
            {
                inputs.Add((emptyBottle, 1));
            }
            if (automatic && index == 1)
            {
                MachineBatch.AddInputs(inputs, utilities, Power, 1);
            }
            return true;
        }

        private void Request(Step step)
        {
            if (automatic || Dragging)
            {
                return;
            }
            step.Failure = null;
            if (step.Remaining > 0)
            {
                step.Finish = 0;
                step.Remaining = 0;
            }
            else if (BatchUnlocked || TryInputs(Array.IndexOf(steps, step), out _, out _))
            {
                step.Remaining = BatchUnlocked ? step.Requested : 1;
            }
        }

        private void ToggleFactory()
        {
            if (Dragging) return;
            if (!automatic && steps.Any(step => step.Remaining > 0))
            {
                notice = Loc.Pick("请先完成或取消各工位队列。", "Finish or cancel the station queues first.");
                return;
            }
            bool wasRunning = automatic;
            notice = null;
            StopFactory();
            if (!wasRunning && FactoryUnlocked)
            {
                automatic = true;
                produced = 0;
                operatingTicks = 0;
            }
        }

        private void StopFactory()
        {
            automatic = false;
            foreach (Step step in steps)
            {
                step.Remaining = 0;
                step.Finish = 0;
            }
        }

        internal static (string Id, int Units)[] BuildRequirements(bool industrial) => industrial
            ? new[] { (Parts, 8), (Power, 2), (ChemistryCatalog.ToolItemId("autoclave"), 1) }
            : new[] { (Parts, 4), (NitricProductionData.Product.ItemId, 1), (ChemistryCatalog.ToolItemId("distiller"), 1) };

        private void BuildUpgrade(bool industrial)
        {
            if (automatic || Dragging || (industrial ? !BatchUnlocked || FactoryUnlocked : BatchUnlocked)) return;
            var inputs = new List<(GameItem Item, int Units)>();
            foreach (var (id, units) in BuildRequirements(industrial))
            {
                if (!MachineBatch.AddInputs(inputs, works, id, units)) return;
            }
            string output = industrial ? PipeModule : LabModule;
            notice = MachineBatch.TryCommit(grid, inputs, (output, grid.StorageInventoryArea))
                ? NitricProductionData.Find(output).Name + Loc.Pick("已建成，存入实验室。", " built and placed in lab storage.")
                : Loc.Pick("建造失败。检查实验室空位。", "Could not build. Check lab storage space.");
        }

        private static bool CanSalvage(string id)
        {
            SampleDef sample = SampleTable.Find(id);
            return sample != null && (sample.Batch == "04" || sample.Batch == "05");
        }

        private void Salvage()
        {
            if (Dragging) return;
            GameItem scrap = works.childItems.FirstOrDefault(item => CanSalvage(item.identifier));
            if (scrap == null) return;
            var inputs = new List<(GameItem Item, int Units)> { (scrap, 1) };
            notice = MachineBatch.TryCommit(grid, inputs, (Parts, works), (Parts, works))
                ? Loc.Pick("拆出 2 个回收机件。", "Recovered 2 salvaged parts.")
                : Loc.Pick("拆解失败。检查施工槽容量。", "Could not salvage. Check the construction bay.");
        }

        private void BuyPart()
        {
            PlayerStore store = PlayerStore.Instance;
            if (Dragging || store == null || !store.CanPay(PartPrice)) return;
            store.PayMoney(PartPrice);
            if (MachineBatch.TryCommit(grid, new List<(GameItem Item, int Units)>(), (Parts, works)))
            {
                notice = Loc.Pick("回收机件已送到施工槽。", "Salvaged part delivered to the construction bay.");
            }
            else
            {
                store.PayMoney(-PartPrice);
                notice = Loc.Pick("购买失败。检查施工槽容量。", "Purchase failed. Check the construction bay.");
            }
        }

        private static int Units(GameInventory inventory, string id = null) => inventory.childItems
            .Where(item => id == null || item.identifier == id).Sum(item => item.unitCount);
    }
}
