using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chemistry
{
    internal sealed partial class LabEquipmentPanel : MonoBehaviour
    {
        private const string Water = LabMixtureData.Water;
        private static readonly Dictionary<int, LabEquipmentPanel> Panels = new Dictionary<int, LabEquipmentPanel>();
        private PixelWindow window;
        private LabEquipmentDef equipment;
        private int machineId;
        private GameItem machine;
        private GameSlotInventory battery;
        private EquipmentSlot feed;
        private EquipmentSlot receiver;
        private GameSlotInventory output;
        private MachineryText load;
        private MachineryText setting;
        private MachineryText status;
        private MachineryProgressBar progress;
        private ReactionPreview reactionPreview;
        private SpectrometerPreview scanPreview;
        private InstrumentAnimPreview preview;
        private MachineryButton start;
        private MachineryButton previous;
        private MachineryButton next;
        private long startedAt;
        private int runTicks;
        private int level = 1;
        private bool running;
        private string result;
        private (GameItem Input, int Units, GameItem Receiver, int ReceiverUnits) resultLoad;

        /// <summary>
        /// 读档/切场景会把窗口 GameObject 整个销毁，却不会经过 <see cref="CloseAll"/>
        /// （那时 LabInventoryGrid.active 已是假 null，Close 提前返回），字典里就会留下
        /// 「窗口已销毁」的残留面板。后面任何双击在 IsVisible() 上都会抛空引用，
        /// 设备面板从此打不开——所以枚举前一律先过滤，并在 Open 时清掉。
        /// </summary>
        private static bool IsPanelAlive(LabEquipmentPanel panel) =>
            panel != null && panel.window != null && panel.window.handler != null;

        private static IEnumerable<LabEquipmentPanel> LivePanels => Panels.Values.Where(IsPanelAlive);

        internal static IEnumerable<GameInventory> Inventories => LivePanels.SelectMany(panel => panel.Areas());
        internal static bool Running => LivePanels.Any(panel => panel.running);

        /// <summary>把窗口已销毁的残留面板逐出字典（场景销毁绕过了 CloseAll 时兜底）。</summary>
        private static void PruneStalePanels()
        {
            List<int> staleKeys = null;
            foreach (KeyValuePair<int, LabEquipmentPanel> entry in Panels)
            {
                if (!IsPanelAlive(entry.Value))
                {
                    (staleKeys ??= new List<int>()).Add(entry.Key);
                }
            }
            if (staleKeys == null) return;
            foreach (int key in staleKeys)
            {
                Panels.Remove(key);
            }
        }

        private EquipmentTask Task => equipment.Task;
        private GameItem Input => feed?.childItems.FirstOrDefault();
        private GameItem Receiver => receiver?.childItems.FirstOrDefault();
        private int SelectedUnits => equipment.Key == "pipette" ? level : level * 2;
        private (GameItem, int, GameItem, int) CurrentLoad =>
            (Input, Input?.unitCount ?? 0, Receiver, Receiver?.unitCount ?? 0);
        private static bool Dragging => ItemMouseDragHandler.current?.IsDraggingItem == true;

        internal static void Attach(GameItem item, LabEquipmentDef def)
        {
            item.mayStackItemFunc = (_, _) => false;
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
            PruneStalePanels();
            LabEquipmentPanel panel = GetOrCreate(item.GetUniqueID(), def, item.name);
            if (panel == null) return;
            if (!panel.running)
            {
                panel.machine = item;
            }
            LabPanel.Shutdown();
            int visiblePeers = Panels.Values.Count(other => other != panel && other.window != null && other.window.IsVisible());
            panel.window.SetTitle(item.name);
            panel.window.Show();
            panel.window.Center();
            float offset = Math.Min(visiblePeers * 36f, 180f);
            panel.window.rectTransform.anchoredPosition = new Vector2(offset, -12f - offset * 0.5f);
            panel.window.FixBounds();
            panel.window.ToFront();
            panel.Refresh();
        }

        private static LabEquipmentPanel GetOrCreate(int id, LabEquipmentDef def, string title)
        {
            if (Panels.TryGetValue(id, out LabEquipmentPanel panel) && IsPanelAlive(panel))
            {
                return panel.equipment == def ? panel : null;
            }
            Panels.Remove(id);
            var window = new PixelWindow(true, title, WindowStyle.Machinery);
            try
            {
                panel = window.handler.gameObject.AddComponent<LabEquipmentPanel>();
                panel.window = window;
                panel.equipment = def;
                panel.machineId = id;
                panel.Build();
                Panels.Add(id, panel);
                return panel;
            }
            catch (Exception error)
            {
                window.Destroy();
                ChemistryMod.Log?.Warning("打开实验设备失败：" + def.Key + " " + error);
                return null;
            }
        }

        internal static void CloseAll()
        {
            LabEquipmentPanel[] closing = Panels.Values.ToArray();
            Panels.Clear();
            foreach (LabEquipmentPanel panel in closing)
            {
                // 读档兜底时面板窗口可能已被场景销毁：只清还活着的物品和窗口。
                if (!IsPanelAlive(panel)) continue;
                panel.running = false;
                foreach (GameInventory area in panel.Areas())
                {
                    foreach (GameItem item in area.childItems.ToArray())
                    {
                        // 场景销毁残留的物品是假 null；物品还在但 contentWindow 已被
                        // 场景卸载时，Destroy 里的 Hide 会先炸，一并跳过。
                        if (item == null || (item.contentWindow != null && item.contentWindow.handler == null)) continue;
                        item.Destroy();
                    }
                }
                panel.window.Destroy();
            }
        }

        private IEnumerable<(SlotKind Kind, GameInventory Inventory)> Slots()
        {
            if (battery != null) yield return (SlotKind.Battery, battery);
            if (feed != null) yield return (SlotKind.Feed, feed);
            if (receiver != null) yield return (SlotKind.Receiver, receiver);
            if (output != null) yield return (SlotKind.Output, output);
            if (byproduct != null) yield return (SlotKind.Byproduct, byproduct);
        }

        private IEnumerable<GameInventory> Areas() => Slots().Select(slot => slot.Inventory);

        private void Build()
        {
            var sections = new List<MachineryPanel>();
            if (equipment.PowerKj > 0)
            {
                battery = new GameSlotInventory();
                battery.mayInventoryAddItemFunc = (item, _) => GeneralHelper.IsItemOwned(item) && item.IsTag("power_source_item");
                battery.SetBackgroundFadeSprite("Items/items_tool", "battery100", 0.5f, 0.5f);
                battery.onSlotAddItemFunc += (_, _, _) =>
                {
                    if (machine != null) AudioManager.Instance.Play3("battery_insert");
                };
                battery.onSlotRemoveItemFunc += (_, _, _) => AudioManager.Instance.Play3("battery_removed");
                sections.Add(MachineHelper.PowerPanel(battery));
            }
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

            var controls = new List<PixelElement>();
            if (equipment.Adjustable)
            {
                setting = new MachineryText(size: 7, uppercase: false);
                previous = new MachineryButton("<", () => ChangeLevel(-1), 18, 12);
                next = new MachineryButton(">", () => ChangeLevel(1), 18, 12);
                controls.Add(MachineryStack.Horizontal(previous, setting, next));
            }
            // 每台设备优先配专属动画；烧杯与光谱仪保留原有的反应/扫描画面。
            preview = InstrumentAnimPreview.Create(equipment.Key);
            if (preview != null)
            {
                preview.StartRequested = StartRun;
                controls.Add(preview);
            }
            else if (Task is EquipmentTask.Mix or EquipmentTask.Titrate)
            {
                reactionPreview = new ReactionPreview();
                controls.Add(reactionPreview);
            }
            else if (Task == EquipmentTask.AnalyzeAny)
            {
                scanPreview = new SpectrometerPreview();
                controls.Add(scanPreview);
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

        private bool CanLoad(GameInventory area, GameItem item) => !running && item != machine
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
            if (machine?.parentInventory == null || LabInventoryGrid.OpenGrid == null || !GeneralHelper.IsItemEffectivelyOwned(machine))
                return Loc.Pick("请双击实验室内的设备", "Open a device in the lab");
            if (battery != null)
            {
                if (battery.childItem == null) return Loc.Pick("装入电池才能运行", "Insert a battery to run");
                if (!PowerHelper.CanDrawPowerSource(battery.childItem, equipment.PowerKj))
                    return Loc.Pick("电池电量不足", "Battery is out of power");
            }
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
            bool addingSolid = Task == EquipmentTask.Mix && IsDissolvableSolid(Receiver);
            bool addingLiquid = Task == EquipmentTask.Mix && !addingSolid && IsMergeableLiquid(Receiver);
            var solidPlan = addingSolid ? PlanSolidAddition(Input, Receiver) : default;
            var liquidPlan = addingLiquid
                ? AqueousReactions.Plan(Input, Input.unitCount, Receiver, PourableUnits(Input, Receiver)) : default;
            runTicks = Task switch
            {
                EquipmentTask.Settle => 120,
                EquipmentTask.Distill => 72 + level * 8,
                EquipmentTask.Titrate => LabTitration.ReactionTicks,
                EquipmentTask.Mix when solidPlan.Ticks > 0 => solidPlan.Ticks,
                EquipmentTask.Mix when liquidPlan.Ticks > 0 => liquidPlan.Ticks,
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
                reactionPreview.Begin(PreviewFill(Input), PreviewTint(Input?.identifier),
                    addingSolid ? solidPlan.Effects : liquidPlan.Effects, addingSolid, indicator);
            }
            if (scanPreview != null)
            {
                GameItem sample = Input;
                scanPreview.Begin(sample.identifier, ScanSpecimen(sample));
            }
            if (preview != null)
            {
                if (Task == EquipmentTask.Titrate)
                {
                    var dose = LabTitration.Combine(LabTitration.Read(Input, Input.unitCount), LabTitration.Read(Receiver, level),
                        Input.unitCount + level);
                    preview.ExtraTint = PreviewTint(dose.Id);
                }
                if (Task == EquipmentTask.Mix)
                    preview.Effects = (int)(addingSolid ? solidPlan.Effects : liquidPlan.Effects)
                        | (addingSolid ? HotplateStirrerPreview.SolidAdded : 0);
                preview.Level = level;
                preview.Begin(InstrumentLoad(Input));
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
                // 电量在开跑时校验、完成时结算：中途被抽走的电不算在本次头上
                if (success && battery?.childItem != null) PowerHelper.DrawPowerSource(battery.childItem, equipment.PowerKj);
            }
            catch (Exception error)
            {
                ChemistryMod.Log?.Warning("实验设备操作失败：" + equipment.Key + " " + error);
                result = Failed;
            }
            reactionPreview?.Finish(success);
            scanPreview?.Finish(success);
            preview?.Finish(success);
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
                    return WeighReport(input);
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

        private void OnDestroy()
        {
            reactionPreview?.Release();
            scanPreview?.Release();
            preview?.Release();
        }

        private static float PreviewFill(GameItem item) => item == null ? 0f : Mathf.Clamp(ReagentBottle.Contents(item) / 6f, 0.27f, 0.82f);

        private static int PreviewTint(string id) => id != null && id.StartsWith(ChemistryCatalog.ItemPrefix, StringComparison.Ordinal)
            && MaterialItemArt.TryGetFluid(id.Substring(ChemistryCatalog.ItemPrefix.Length), out int? color)
            && id != Water ? color ?? ReactionPreview.WaterColor : ReactionPreview.WaterColor;

        /// <summary>专属动画用的样品外观：形态/配色沿用扫描判定，液体量沿用瓶内容量。</summary>
        private static InstrumentAnimPreview.Look PreviewLook(GameItem item)
        {
            var (form, tint) = ScanSpecimen(item);
            return new InstrumentAnimPreview.Look((InstrumentAnimPreview.Specimen)form, tint, PreviewFill(item));
        }

        private InstrumentAnimPreview.Look InstrumentLoad(GameItem input)
        {
            var sample = PreviewLook(input);
            GameItem target = Receiver;
            if (Task is not (EquipmentTask.Dispense or EquipmentTask.Titrate) || input?.identifier == LabMixtureData.Brine)
                return sample;

            bool titration = Task == EquipmentTask.Titrate;
            GameItem source = titration ? target : input;
            GameItem destination = titration ? input : target;
            int count = titration ? Mathf.Min(level, source?.unitCount ?? 0) : AmountToDispense(source, destination);
            int sourceCapacity = source == null ? 1 : Math.Max(1, ReagentBottle.CapacityFor(source));
            int receiverCapacity = destination == null ? 1 : Math.Max(1, ReagentBottle.CapacityFor(destination));
            float receiverFill = destination == null ? 0f : ReagentBottle.Contents(destination) / (float)receiverCapacity;
            float sourceFill = source == null ? 0f : ReagentBottle.Contents(source) / (float)sourceCapacity;
            var dose = new InstrumentAnimPreview.Transfer(PreviewTint(destination?.identifier),
                receiverFill,
                count / (float)sourceCapacity, count / (float)receiverCapacity, count);
            return new InstrumentAnimPreview.Look(source == null ? InstrumentAnimPreview.Specimen.Empty : InstrumentAnimPreview.Specimen.Liquid,
                PreviewTint(source?.identifier), sourceFill, dose);
        }

        private static (SpectrometerPreview.Specimen Form, int Tint) ScanSpecimen(GameItem item)
        {
            if (item == null) return default;
            string id = item.identifier;
            if (SampleTable.Find(id) is SampleDef sample)
            {
                return sample.SpriteKey switch
                {
                    "sample_liquid" => (SpectrometerPreview.Specimen.Liquid, 0x7B8C65),
                    "sample_gas" => (SpectrometerPreview.Specimen.Gas, 0x7D8C88),
                    "sample_circuit" => (SpectrometerPreview.Specimen.Solid, 0x9B7854),
                    "sample_slag" => (SpectrometerPreview.Specimen.Solid, 0x8D8270),
                    _ => (SpectrometerPreview.Specimen.Solid, 0x908377)
                };
            }

            string key = id.StartsWith(ChemistryCatalog.ItemPrefix, StringComparison.Ordinal)
                ? id.Substring(ChemistryCatalog.ItemPrefix.Length) : id;
            if (id == Water) return (SpectrometerPreview.Specimen.Liquid, ReactionPreview.WaterColor);
            if (MaterialItemArt.TryGetFluid(key, out int? fluidTint))
            {
                if (fluidTint is int liquidTint) return (SpectrometerPreview.Specimen.Liquid, liquidTint);
                return (SpectrometerPreview.Specimen.Gas,
                    MaterialAppearance.GasColors.TryGetValue(key, out int gasTint) ? gasTint : 0x7D8C88);
            }

            var element = key.StartsWith("el_", StringComparison.Ordinal) ? ChemistryCatalog.FindElementByItemId(id) : null;
            var compound = key.StartsWith("cmp_", StringComparison.Ordinal) ? ChemistryCatalog.FindCompound(key.Substring(4)) : null;
            string category = element?.Category ?? compound?.Category;
            return (SpectrometerPreview.Specimen.Solid, category == null ? 0x908377 : Tint(ChemUi.CategoryColor(category)));
        }

        private static int Tint(Color color) =>
            ((int)(color.r * 255f) << 16) | ((int)(color.g * 255f) << 8) | (int)(color.b * 255f);

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
            bool canStart = blocker == null && !Running && !AcidStation.Running && !LabRefining.Running;
            float elapsed = running ? GameTicks.Progress(startedAt, runTicks) : 0f;
            reactionPreview?.Update(PreviewFill(input), PreviewTint(input?.identifier), elapsed, result != null && output?.childItem != null);
            scanPreview?.Update(ScanSpecimen(input), elapsed, result != null && result != Failed);
            if (preview != null)
            {
                preview.Level = level;
                preview.Update(InstrumentLoad(input), elapsed, result != null, canStart);
            }
            string phase = Task == EquipmentTask.Distill ? DistillationPhase(elapsed)
                : elapsed < 0.28f ? Loc.Pick("准备中…", "Preparing...")
                : elapsed < 0.82f ? ProcessPhase() ?? AnalysisPhase() ?? MixturePhase() ?? (equipment.ActionLabel + "…")
                    : Loc.Pick("收集结果…", "Collecting result...");
            string ready = Task == EquipmentTask.Dispense && blocker == null && Input?.identifier != LabMixtureData.Brine
                ? Loc.Pick("本次转移 ", "Transfer ") + Volume(AmountToDispense(input, Receiver))
                : Task == EquipmentTask.Distill && blocker == null ? DistillationPreview() : Loc.Pick("就绪", "Ready");
            SetText(status, running ? phase + " " + GameTicks.Label(GameTicks.Remaining(startedAt, runTicks)) : result ?? blocker ?? ready);
            SetDisabled(start, !canStart);
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
