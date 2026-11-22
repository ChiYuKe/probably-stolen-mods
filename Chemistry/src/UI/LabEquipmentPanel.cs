using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chemistry
{
    internal sealed partial class LabEquipmentPanel : MonoBehaviour
    {
        private const string Water = LabMixtureData.Water;
        private static readonly Dictionary<string, LabEquipmentPanel> Panels = new Dictionary<string, LabEquipmentPanel>();
        private static int testTier;
        private PixelWindow window;
        private LabEquipmentDef equipment;
        private GameItem machine;
        private EquipmentSlot feed;
        private EquipmentSlot receiver;
        private GameSlotInventory output;
        private MachineryText load;
        private MachineryText setting;
        private MachineryText status;
        private MachineryProgressBar progress;
        private ReactionPreview reactionPreview;
        private MachineryButton start;
        private MachineryButton previous;
        private MachineryButton next;
        private long startedAt;
        private int runTicks;
        private int level = 1;
        private bool running;
        private string result;
        private (GameItem Input, int Units, GameItem Receiver, int ReceiverUnits) resultLoad;

        internal static IEnumerable<GameInventory> Inventories => Panels.Values.SelectMany(panel => panel.Areas());
        internal static bool Running => Panels.Values.Any(panel => panel.running);
        internal static void EnableTestTier(int tier) => testTier = Math.Max(testTier, tier);
        internal static void ResetTestTiers() => testTier = 0;
        internal static bool IsTierUnlocked(int tier, int day) => testTier >= tier || Math.Max(1, StoreStation.GetDayCounter()) >= day;

        private EquipmentTask Task => equipment.Task;
        private GameItem Input => feed?.childItems.FirstOrDefault();
        private GameItem Receiver => receiver?.childItems.FirstOrDefault();
        private int SelectedUnits => equipment.Key == "pipette" ? level : level * 2;
        private (GameItem, int, GameItem, int) CurrentLoad =>
            (Input, Input?.unitCount ?? 0, Receiver, Receiver?.unitCount ?? 0);
        private static bool Dragging => ItemMouseDragHandler.current?.IsDraggingItem == true;
        private bool Unlocked => IsTierUnlocked(equipment.Tier, equipment.UnlockDay);

        internal static void Attach(GameItem item, LabEquipmentDef def)
        {
            var launcher = new PixelWindow(true, item.name, WindowStyle.Machinery);
            launcher.openCallback = () =>
            {
                launcher.Hide();
                if (IndustrialFactory.UsesMachine(item))
                {
                    OverlayHandler.current?.AppendLog(Loc.Pick("这台设备已接入厂房，卸下后可手动操作。",
                        "This machine is installed in the workshop. Disconnect it before manual use."), false);
                    return;
                }
                if (LabInventoryGrid.OpenGrid == null)
                {
                    OverlayHandler.current?.AppendLog(Loc.Pick("请在地下实验室操作这台设备。",
                        "Use this equipment in the underground lab."), false);
                    return;
                }
                Open(item, def);
            };
            item.SetContentWindow(launcher);
            launcher.Hide();
        }

        private static void Open(GameItem item, LabEquipmentDef def)
        {
            if (!Panels.TryGetValue(def.Key, out LabEquipmentPanel panel))
            {
                var window = new PixelWindow(true, item.name, WindowStyle.Machinery);
                try
                {
                    panel = window.handler.gameObject.AddComponent<LabEquipmentPanel>();
                    panel.window = window;
                    panel.equipment = def;
                    panel.Build();
                    Panels.Add(def.Key, panel);
                }
                catch (Exception error)
                {
                    window.Destroy();
                    ChemistryMod.Log?.Warning("打开实验设备失败：" + def.Key + " " + error);
                    return;
                }
            }
            if (!panel.running)
            {
                panel.machine = item;
            }
            LabPanel.Shutdown();
            foreach (LabEquipmentPanel other in Panels.Values)
            {
                if (other != panel) other.window.Hide();
            }
            panel.window.Show();
            panel.window.Center();
            panel.window.rectTransform.anchoredPosition = new Vector2(panel.window.rectTransform.anchoredPosition.x, -12f);
            panel.window.FixBounds();
            panel.Refresh();
        }

        internal static void CloseAll()
        {
            LabEquipmentPanel[] closing = Panels.Values.ToArray();
            Panels.Clear();
            foreach (LabEquipmentPanel panel in closing)
            {
                panel.running = false;
                foreach (GameInventory area in panel.Areas())
                {
                    foreach (GameItem item in area.childItems.ToArray()) item.Destroy();
                }
                panel.window.Destroy();
            }
        }

        private IEnumerable<GameInventory> Areas()
        {
            if (feed != null) yield return feed;
            if (receiver != null) yield return receiver;
            if (output != null) yield return output;
            if (byproduct != null) yield return byproduct;
        }

        private void Build()
        {
            var sections = new List<MachineryPanel>();
            load = new MachineryText(size: 7, uppercase: false);
            if (Task != EquipmentTask.Settings)
            {
                feed = new EquipmentSlot();
                feed.mayInventoryAddItemFunc = (item, _) => CanLoad(feed, item) && AcceptsInput(item);
                sections.Add(MachineHelper.Panel(Loc.Pick("样品", "Sample"), MachineryStack.Vertical(load, feed), "input"));
            }
            if (Task == EquipmentTask.Dispense || Task == EquipmentTask.Distill)
            {
                receiver = new EquipmentSlot();
                receiver.mayInventoryAddItemFunc = (item, _) => (Task == EquipmentTask.Distill && MachineBatch.IsPlacing(item, receiver))
                    || (CanLoad(receiver, item) && (Task == EquipmentTask.Distill
                        ? IsPortableGlass(item) : ReagentBottle.IsLiquidEmpty(item.identifier) || ReagentBottle.IsLiquid(item.identifier)));
                sections.Add(MachineHelper.Panel(Loc.Pick("接收容器", "Receiver"), receiver, "bottle"));
            }
            BuildMixtureReceiver(sections);
            BuildAnalysisReceiver(sections);
            BuildProcessReceiver(sections);

            var controls = new List<PixelElement>
            {
                new MachineryText(equipment.TierLabel + " · " + (equipment.UnlockDay == 1
                    ? Loc.Pick("第 1 天起", "Day 1 onward")
                    : Loc.Pick($"第 {equipment.UnlockDay} 天解锁", $"Unlocks day {equipment.UnlockDay}")), size: 7, uppercase: false)
            };
            if (equipment.Adjustable)
            {
                setting = new MachineryText(size: 7, uppercase: false);
                previous = new MachineryButton("<", () => ChangeLevel(-1), 18, 12);
                next = new MachineryButton(">", () => ChangeLevel(1), 18, 12);
                controls.Add(MachineryStack.Horizontal(previous, setting, next));
            }
            if (Task is EquipmentTask.Mix or EquipmentTask.Titrate)
            {
                reactionPreview = new ReactionPreview();
                controls.Add(reactionPreview);
            }
            status = new MachineryText(size: 7, uppercase: false);
            progress = new MachineryProgressBar(width: 114);
            start = new MachineryButton(equipment.ActionLabel, StartRun, 92, 13);
            controls.AddRange(new PixelElement[] { status, progress, start });
            BuildPeakReadout(controls);
            sections.Add(MachineHelper.Panel(Loc.Pick("操作", "Controls"), MachineryStack.Vertical(controls.ToArray()), "control").SetMinWidth(134));

            if (equipment.HasOutput || SupportsBrineSampling)
            {
                output = new GameSlotInventory(2, Task == EquipmentTask.GasCollect ? 4 : 2);
                output.mayInventoryAddItemFunc = (item, _) => MachineBatch.IsPlacing(item, output);
                string outputName = Task switch
                {
                    EquipmentTask.SeparateLayers => Loc.Pick("下层 · 水", "Lower layer · Water"),
                    EquipmentTask.Distill => Loc.Pick("冷凝液", "Condensate"),
                    EquipmentTask.GasCollect => Loc.Pick("气体接收", "Gas output"),
                    _ => Loc.Pick("产物", "Output")
                };
                sections.Add(MachineHelper.Panel(outputName, output, "output"));
            }
            BuildMixtureOutput(sections);
            foreach (GameInventory area in Areas())
            {
                area.mayInventoryRemoveItemFunc = (_, _) => !running;
                area.maxSlotRemoveItemFunc = (_, _, _) => running ? 0 : int.MaxValue;
            }
            window.Attach(new MachineryPanelRow(sections.ToArray()));
            ChemFontFix.Attach(window.handler.gameObject);
        }

        private bool CanLoad(GameInventory area, GameItem item) => !running && Unlocked && item != machine
            && (area.childItems.Count == 0 || item.parentInventory == area)
            && LabInventoryGrid.OpenGrid?.CanReceive(item) == true;

        private bool AcceptsInput(GameItem item)
        {
            string id = item.identifier;
            SampleDef sample = SampleTable.Find(id);
            bool solidSample = sample != null && sample.SpriteKey != "sample_liquid" && sample.SpriteKey != "sample_gas";
            return Task switch
            {
                EquipmentTask.Weigh => true,
                EquipmentTask.Volume => ReagentBottle.IsLiquid(id) || ReagentBottle.IsLiquidEmpty(id)
                    || LabMixtureData.IsDirectBatch(id) || LabProcessData.Read(id).Units > 0,
                EquipmentTask.Dispense => ReagentBottle.IsLiquid(id) || (SupportsBrineSampling && id == LabMixtureData.Brine),
                EquipmentTask.Filter => LabRefining.FindIntermediate(id)?.Filtered == false || LabMixtureData.HasSolids(id)
                    || LabAnalysisData.IsDye(id) || HasProcessSediment(id) || CanCollectAqueous(item, AqueousReactions.Precipitate),
                EquipmentTask.Distill => AqueousReactions.CanRecoverWater(item) || LabDistillationData.Accepts(id)
                    || (sample?.Batch == "09" && id == sample.DetectedId),
                EquipmentTask.AnalyzeSolid => solidSample || LabWorkflow.CanReadSolid(id),
                EquipmentTask.AnalyzeFluid => (sample != null && !solidSample) || LabWorkflow.CanReadFluid(id),
                EquipmentTask.AnalyzeAny => sample != null || LabWorkflow.CanRead(id),
                EquipmentTask.Acidity => FluidClass(id) != null || LabTitration.PhReading(item) != null || AqueousReactions.Acidity(item) != null,
                EquipmentTask.Conductivity => FluidClass(id) != null || id == LabProcessData.BrineAliquot,
                EquipmentTask.Titrate => LabTitration.IsSample(id),
                EquipmentTask.GlasswareCycle => IsCleanableGlass(item),
                EquipmentTask.WaterCycle => id == Water,
                EquipmentTask.SolidCycle => solidSample,
                _ => AcceptsProcessInput(item) || AcceptsMixtureInput(item)
            };
        }

        private static bool IsPortableGlass(GameItem item) =>
            item != null && ReagentBottle.IsGlassEmpty(item.identifier);

        private static string FluidClass(string id)
        {
            if (!ReagentBottle.IsLiquid(id)) return null;
            if (id == Water) return "water";
            string category = ChemistryCatalog.FindCompound(id.Substring((ChemistryCatalog.ItemPrefix + "cmp_").Length))?.Category;
            return category == "acid" || category == "base" ? category : null;
        }

        private string InputPrompt() => Task switch
        {
            EquipmentTask.Weigh => Loc.Pick("放入物品", "Place an item"),
            EquipmentTask.Volume or EquipmentTask.Dispense => Loc.Pick("放入液体容器", "Load a liquid container"),
            EquipmentTask.Filter => Loc.Pick("放入悬浊液、浸出液或示踪液", "Load suspension, leachate or tracer"),
            EquipmentTask.Distill => Loc.Pick("装入可分离的水溶液、醇水或 09 号水样", "Load a supported aqueous or alcohol-water mixture, or sample 09"),
            EquipmentTask.AnalyzeFluid => Loc.Pick("放入液体、气体或流体样品", "Load a liquid, gas or fluid sample"),
            EquipmentTask.AnalyzeAny => Loc.Pick("放入元素、试剂或待检样品", "Load an element, reagent or sample"),
            EquipmentTask.Titrate => Loc.Pick("放入滴定教学样品", "Load a titration training sample"),
            EquipmentTask.AnalyzeSolid or EquipmentTask.SolidCycle => Loc.Pick("放入固体样品", "Load a solid sample"),
            EquipmentTask.Acidity or EquipmentTask.Conductivity => Loc.Pick("放入水或酸碱试剂", "Load water, acid or base"),
            EquipmentTask.GlasswareCycle => Loc.Pick("放入空玻璃器皿", "Load empty glassware"),
            EquipmentTask.WaterCycle => Loc.Pick("放入一瓶水做测试", "Load water for a test run"),
            _ => ProcessInputPrompt() ?? MixtureInputPrompt() ?? Loc.Pick("放入待检测样品", "Load a sample")
        };

        private int AmountToDispense(GameItem source, GameItem target)
        {
            return source != null && target != null && ReagentBottle.CanReceiveLiquid(source, target)
                ? ReagentBottle.TransferUnits(source, target, SelectedUnits) : 0;
        }

        private string Blocker()
        {
            if (!Unlocked) return Loc.Pick($"第 {equipment.UnlockDay} 天可操作", $"Available on day {equipment.UnlockDay}");
            if (machine?.parentInventory == null || LabInventoryGrid.OpenGrid == null || !GeneralHelper.IsItemEffectivelyOwned(machine))
                return Loc.Pick("请双击实验室内的设备", "Open a device in the lab");
            if (IndustrialFactory.UsesMachine(machine))
                return Loc.Pick("请先从厂房卸下设备", "Disconnect the machine from the workshop first");
            if (output?.childItem != null || byproduct?.childItem != null) return Loc.Pick("先取出产物", "Take the output first");
            if (feed != null && !(Task == EquipmentTask.GasCollect && Input == null)
                && (Input == null || !AcceptsInput(Input))) return InputPrompt();
            if (Task == EquipmentTask.Distill) return DistillationBlocker();
            if (Task == EquipmentTask.Dispense && Input?.identifier != LabMixtureData.Brine && AmountToDispense(Input, Receiver) <= 0)
                return Loc.Pick("请装入有余量的兼容接收瓶", "Use a compatible receiver with free capacity");
            return ProcessBlocker() ?? AnalysisBlocker() ?? MixtureBlocker();
        }

        private void ChangeLevel(int delta)
        {
            if (running || Dragging) return;
            level = Mathf.Clamp(level + delta, 1, 3);
            result = null;
            Refresh();
        }

        private void StartRun()
        {
            if (Running || AcidStation.Running || LabRefining.Running || Dragging || Blocker() != null) return;
            LabInventoryGrid grid = LabInventoryGrid.OpenGrid;
            result = null;
            running = true;
            startedAt = GameTicks.Now;
            bool addingSolid = Task == EquipmentTask.Mix && AqueousReactions.IsSolidReagent(Receiver?.identifier);
            var solidPlan = addingSolid ? PlanSolidAddition(Input, Receiver) : default;
            runTicks = Task switch
            {
                EquipmentTask.Settle => 120,
                EquipmentTask.Distill => 72 + level * 8,
                EquipmentTask.Titrate => LabTitration.ReactionTicks,
                EquipmentTask.Mix when solidPlan.Ticks > 0 => solidPlan.Ticks,
                _ => equipment.CycleTicks + (equipment.Adjustable ? (level - 1) * 8 : 0)
            };
            if (reactionPreview != null)
            {
                int indicator = 0;
                if (Task == EquipmentTask.Titrate)
                {
                    var dose = LabTitration.Combine(LabTitration.Read(Input, Input.unitCount), LabTitration.Read(Receiver, level),
                        Input.unitCount + level);
                    indicator = PreviewTint(dose.Id);
                }
                reactionPreview.Begin(PreviewFill(Input), PreviewTint(Input?.identifier), solidPlan.Effects, addingSolid, indicator);
            }
            GameTicks.Schedule(runTicks, () => Finish(grid));
            Refresh();
        }

        private void Finish(LabInventoryGrid grid)
        {
            if (!running || LabInventoryGrid.OpenGrid != grid) return;
            running = false;
            bool success = false;
            try
            {
                string blocker = Dragging ? Loc.Pick("拖动中，本次操作已取消", "Run cancelled during an item drag") : Blocker();
                result = blocker ?? Operate(grid, Input);
                success = blocker == null && result != Failed;
            }
            catch (Exception error)
            {
                ChemistryMod.Log?.Warning("实验设备操作失败：" + equipment.Key + " " + error);
                result = Failed;
            }
            reactionPreview?.Finish(success);
            resultLoad = CurrentLoad;
            if (window.IsVisible()) Refresh();
        }

        private string Operate(LabInventoryGrid grid, GameItem input)
        {
            string aqueousResult = OperateAqueousCollection(grid, input);
            if (aqueousResult != null) return aqueousResult;
            string processResult = OperateProcess(grid, input);
            if (processResult != null) return processResult;
            string analysisResult = OperateAnalysis(grid, input);
            if (analysisResult != null) return analysisResult;
            string mixtureResult = OperateMixture(grid, input);
            if (mixtureResult != null) return mixtureResult;
            switch (Task)
            {
                case EquipmentTask.Weigh:
                    return Loc.Pick($"库存读数：{input.unitCount} 单位", $"Inventory reading: {input.unitCount} units");
                case EquipmentTask.Volume:
                    var sampleVolume = LabProcessData.Read(input.identifier);
                    if (sampleVolume.Units > 0)
                        return Volume(sampleVolume.Units) + "\n" + sampleVolume.Contents;
                    int amount = ReagentBottle.Contents(input);
                    return Loc.Pick("量筒读数：", "Volume: ") + Volume(amount) + " / " + Volume(ReagentBottle.CapacityFor(input));
                case EquipmentTask.Dispense:
                    int transfer = AmountToDispense(input, Receiver);
                    bool damaged = ReagentBottle.DamagesGlass(input, Receiver);
                    return ReagentBottle.Pour(input, Receiver, transfer)
                        ? damaged ? Loc.Pick("接收瓶已腐蚀", "Receiver damaged")
                            : Loc.Pick("已转移 ", "Transferred ") + Volume(transfer) : Failed;
                case EquipmentTask.Filter:
                case EquipmentTask.Clarify:
                    string filtrate = LabRefining.IntermediateId(LabRefining.FindIntermediate(input.identifier).Symbol, true);
                    return Commit(grid, input, filtrate);
                case EquipmentTask.Distill:
                    return Distill(grid, input);
                case EquipmentTask.Acidity:
                    return AqueousReactions.Acidity(input) ?? (FluidClass(input.identifier) switch
                    {
                        "acid" => Loc.Pick("酸性试剂", "Acidic reagent"),
                        "base" => Loc.Pick("碱性试剂", "Basic reagent"),
                        _ => Loc.Pick("水样 · 中性参照", "Water · neutral reference")
                    });
                case EquipmentTask.Conductivity:
                    return input.identifier == Water ? Loc.Pick("水样 · 低导电参照", "Water · low-conductivity reference")
                        : Loc.Pick("检测到导电试剂", "Conductive reagent detected");
                case EquipmentTask.AnalyzeSolid:
                case EquipmentTask.AnalyzeFluid:
                case EquipmentTask.AnalyzeAny:
                    SampleDef sample = SampleTable.Find(input.identifier);
                    return input.identifier == sample.DetectedId
                        ? WrapReport(sample) : Commit(grid, input, sample.DetectedId, WrapReport(sample));
                default:
                    return Loc.Pick($"模拟循环完成 · {equipment.SettingLabel} {level} 档",
                        $"Simulation complete · {equipment.SettingLabel} level {level}");
            }
        }

        private string Commit(LabInventoryGrid grid, GameItem item, string id, string success = null) =>
            MachineBatch.TryCommit(grid, new List<(GameItem, int)> { (item, 1) }, (id, output))
                ? success ?? Loc.Pick("完成，请取出产物", "Complete. Take the output.") : Failed;

        private static string WrapReport(SampleDef sample) => string.Join("\n",
            sample.Components.Select(component => component.Symbol + " " + component.Percent + "%")
                .Append(Loc.Pick("杂质 ", "Other ") + (100 - sample.Components.Sum(component => component.Percent)) + "%"));

        private static string Failed => Loc.Pick("操作未完成，材料已保留", "Run failed. Inputs were kept.");
        private static string Volume(int units) => ((long)units * ReagentBottle.MillilitersPerUnit) + " mL";

        private void LateUpdate()
        {
            if (window.IsVisible() && !Dragging) Refresh();
        }

        private void OnDestroy() => reactionPreview?.Release();

        private static float PreviewFill(GameItem item) => item == null ? 0f : Mathf.Clamp(ReagentBottle.Contents(item) / 6f, 0.27f, 0.82f);

        private static int PreviewTint(string id) => id != null && id.StartsWith(ChemistryCatalog.ItemPrefix, StringComparison.Ordinal)
            && MaterialItemArt.TryGetFluid(id.Substring(ChemistryCatalog.ItemPrefix.Length), out int? color)
            && id != Water ? color ?? ReactionPreview.WaterColor : ReactionPreview.WaterColor;

        private void Refresh()
        {
            if (result != null && resultLoad != CurrentLoad) result = null;
            GameItem input = Input;
            if (feed != null) SetText(load, input == null ? InputPrompt() : input.name);
            if (setting != null)
            {
                SetText(setting, Task switch
                {
                    EquipmentTask.Dispense => Volume(SelectedUnits),
                    EquipmentTask.Titrate => Loc.Pick("本次滴加 ", "Add ") + Volume(level),
                    EquipmentTask.Distill => TemperatureText(),
                    EquipmentTask.Mix when equipment.Key == "hotplate_stirrer" => HeatingBand(),
                    _ => Loc.Pick($"{equipment.SettingLabel} {level} 档", $"{equipment.SettingLabel} level {level}")
                });
                SetDisabled(previous, running || level == 1);
                SetDisabled(next, running || level == 3);
            }
            var blocker = Blocker();
            float elapsed = running ? GameTicks.Progress(startedAt, runTicks) : 0f;
            reactionPreview?.Update(PreviewFill(input), PreviewTint(input?.identifier), elapsed, result != null && output?.childItem != null);
            string phase = Task == EquipmentTask.Distill ? DistillationPhase(elapsed)
                : elapsed < 0.28f ? Loc.Pick("准备中…", "Preparing...")
                : elapsed < 0.82f ? ProcessPhase() ?? AnalysisPhase() ?? MixturePhase() ?? (equipment.ActionLabel + "…")
                    : Loc.Pick("收集结果…", "Collecting result...");
            string ready = Task == EquipmentTask.Dispense && blocker == null && Input?.identifier != LabMixtureData.Brine
                ? Loc.Pick("本次转移 ", "Transfer ") + Volume(AmountToDispense(input, Receiver))
                : Task == EquipmentTask.Distill && blocker == null ? DistillationPreview() : Loc.Pick("就绪", "Ready");
            SetText(status, running ? phase + " " + GameTicks.Label(GameTicks.Remaining(startedAt, runTicks)) : result ?? blocker ?? ready);
            SetDisabled(start, Running || AcidStation.Running || LabRefining.Running || blocker != null);
            float value = running ? elapsed : result != null && result != Failed ? 1f : 0f;
            if (progress.value != value) progress.SetValue(value);
            RefreshPeakReadout(elapsed);
        }

        private static void SetText(MachineryText text, string value)
        {
            if (text.GetText() != value) text.SetText(value);
        }

        private static void SetDisabled(MachineryButton button, bool disabled)
        {
            if (button.isDisabled != disabled) button.SetDisabled(disabled);
        }

        private sealed class EquipmentSlot : GameGridInventory
        {
            internal EquipmentSlot() : base(4, 4) { }
            public override bool SupportsGroupPlacement => false;
        }
    }
}
