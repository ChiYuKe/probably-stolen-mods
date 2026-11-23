using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chemistry
{
    internal sealed class RefiningStation : MonoBehaviour
    {
        internal const string ItemId = ChemistryCatalog.ItemPrefix + "refining_station";
        private static readonly HashSet<RefiningStation> openStations = new HashSet<RefiningStation>();
        private static RefiningStation active;
        private static PixelWindow fixtureDock;
        private GameItem machine;
        private PixelWindow window;
        private GameGridInventory feed;
        private GameSlotInventory reagent;
        private GameSlotInventory tool;
        private GameGridInventory output;
        private GameInventory[] inventories;
        private MachineryText state;
        private MachineryText requirement;
        private MachineryButton targetButton;
        private MachineryButton runButton;
        private MachineryButton prepareButton;
        private MachineryProgressBar progress;
        private string target;
        private string notice;
        private bool completed;
        private bool noticeOk;
        private (string feed, int count, string reagent, string tool, bool running, bool available) lastLoad;

        private static string FunnelId => ChemistryCatalog.ToolItemId(LabRefining.FilterToolKey);
        internal static IReadOnlyList<GameInventory> Inventories => active?.inventories ?? Array.Empty<GameInventory>();

        internal static IReadOnlyList<GameInventory> PortableSlots(GameItem item)
        {
            if (item.identifier != ItemId) return Array.Empty<GameInventory>();
            var station = item.contentWindow?.handler?.GetComponent<RefiningStation>();
            return station != null && !station.IsFixture ? station.inventories : Array.Empty<GameInventory>();
        }

        private bool IsFixture => active == this;
        private bool IsOperational => LabInventoryGrid.OpenGrid != null && machine.parentInventory != null;

        internal static void Attach(GameItem item)
        {
            item.mayStackItemFunc = (_, _) => false;
            var panel = new PixelWindow(true, item.name, WindowStyle.Machinery);
            var station = panel.handler.gameObject.AddComponent<RefiningStation>();
            station.window = panel;
            station.machine = item;
            try
            {
                station.Build();
                item.SetContentWindow(panel);
                item.mayInventoryRemoveItemFunc = (_, _) => !LabRefining.IsRunning(station);
                item.maxSlotRemoveItemFunc = (_, _, _) => LabRefining.IsRunning(station) ? 0 : int.MaxValue;
                panel.openCallback = station.OnOpen;
                panel.Hide();
            }
            catch
            {
                panel.Destroy();
                throw;
            }
        }

        internal static void InstallFixture()
        {
            if (fixtureDock != null)
            {
                return;
            }

            GameItem item = null;
            PixelWindow dock = null;
            try
            {
                item = ModHelper.SpawnItem(ItemId);
                RefiningStation station = item?.contentWindow?.handler.GetComponent<RefiningStation>();
                if (station == null)
                {
                    throw new InvalidOperationException(ItemId);
                }

                ModHelper.SetItemOwnerShip(item, true);
                var mount = new GameSlotInventory(item.shape.width, item.shape.height).SetTransparency(1f);
                dock = new PixelWindow().SetTransparency(1f);
                dock.Attach(mount);
                if (GraphUtils.TryAcceptOnce(mount, item, 1) != 0)
                {
                    throw new InvalidOperationException("安装设备失败");
                }

                mount.mayInventoryAddItemFunc = (_, _) => false;
                mount.mayInventoryRemoveItemFunc = (_, _) => false;
                mount.maxSlotRemoveItemFunc = (_, _, _) => 0;
                dock.Show();
                active = station;
                fixtureDock = dock;
                PositionFixture();
            }
            catch (Exception error)
            {
                active = null;
                fixtureDock = null;
                item?.Destroy();
                dock?.Destroy();
                ChemUi.Log?.Warning("安装提炼反应台失败：" + error);
            }
        }

        internal static void PositionFixture() => PositionFixture(fixtureDock, 0.85f, 0.59f);

        internal static void PositionFixture(PixelWindow dock, float horizontal, float vertical)
        {
            RectTransform rect = dock?.rectTransform;
            Canvas canvas = RenderHandler.current?.windowUICanvas;
            if (rect == null || !(rect.parent is RectTransform parent) || canvas == null)
            {
                return;
            }
            LetterboxManager.ComputeGameArea(Screen.width, Screen.height, out int x, out int y, out int width, out int height);
            var screenCenter = new Vector2(x + width * horizontal, y + height * vertical);
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenCenter, camera, out Vector2 center))
            {
                rect.localPosition = new Vector3(center.x - dock.widthPixels * 0.5f,
                    center.y + dock.heightPixels * 0.5f, rect.localPosition.z);
            }
        }

        internal static void Open()
        {
            if (active == null)
            {
                return;
            }
            if (active.window.IsVisible())
            {
                active.window.ToFront();
            }
            else
            {
                active.window.Show();
            }
        }

        private void OnOpen()
        {
            AcidStation.Hide();
            NitricProduction.Hide();
            openStations.Add(this);
            if (IsFixture)
            {
                LabPanel.Shutdown();
                window.Center();
                window.rectTransform.anchoredPosition = new Vector2(window.rectTransform.anchoredPosition.x, -12f);
                window.FixBounds();
            }
            UpdateControls(true);
        }

        private void Build()
        {
            feed = new StationGrid(6, 4);
            reagent = new GameSlotInventory(2, 2);
            tool = new GameSlotInventory(2, 2);
            output = new StationGrid(8, 4);
            inventories = new GameInventory[] { output, feed, reagent, tool };
            feed.mayInventoryAddItemFunc = (item, _) => IsFeed(item.identifier) && CanLoad(item)
                && feed.childItems.All(loaded => loaded.identifier == item.identifier);
            reagent.mayInventoryAddItemFunc = (item, _) => MachineBatch.IsPlacing(item, reagent)
                || (LabRefining.IsAcid(item.identifier) && CanLoad(item));
            tool.mayInventoryAddItemFunc = (item, _) => item.identifier == FunnelId && CanLoad(item);
            output.mayInventoryAddItemFunc = (item, _) => MachineBatch.IsPlacing(item, output);
            foreach (GameInventory inventory in new GameInventory[] { feed, reagent, tool })
            {
                inventory.mayInventoryRemoveItemFunc = (_, _) => !LabRefining.IsRunning(this);
                inventory.maxSlotRemoveItemFunc = (_, _, _) => LabRefining.IsRunning(this) ? 0 : int.MaxValue;
            }

            state = new MachineryText(size: 8, uppercase: false);
            requirement = new MachineryText(size: 7, uppercase: false);
            targetButton = new MachineryButton(Loc.Pick("切换目标", "Change target"), CycleTarget, 64, 12);
            runButton = new MachineryButton(Loc.Pick("启动设备", "Start"), Run, 70, 14);
            prepareButton = new MachineryButton(Loc.Pick("打开制酸装置", "Acid station"), PrepareAcid, 70, 12);
            progress = new MachineryProgressBar(width: 128);
            var controls = MachineryStack.Vertical(state, requirement, progress,
                MachineryStack.Horizontal(targetButton, runButton), prepareButton);
            window.Attach(new MachineryPanelRow(
                MachineHelper.Panel(Loc.Pick("工具", "Tool"), tool, "tool").SetMinWidth(46),
                MachineHelper.Panel(Loc.Pick("试剂", "Reagent"), reagent, "reagent").SetMinWidth(48),
                MachineHelper.Panel(Loc.Pick("投料", "Input"), feed, "input"),
                MachineHelper.Panel(Loc.Pick("反应控制", "Controls"), controls, "control").SetMinWidth(158),
                MachineHelper.Panel(Loc.Pick("产物输出", "Output"), output, "output")));
            ChemFontFix.Attach(window.handler.gameObject);
        }

        private bool CanLoad(GameItem item)
        {
            return IsOperational && !LabRefining.IsRunning(this) &&
                (IsFixture ? LabInventoryGrid.OpenGrid.CanReceive(item) : GeneralHelper.IsItemOwned(item));
        }

        private static SampleDef AnalyzedSample(string id)
        {
            SampleDef sample = SampleTable.Find(id);
            return sample?.DetectedId == id ? sample : null;
        }

        private static bool IsFeed(string id)
        {
            SampleDef sample = AnalyzedSample(id);
            return LabRefining.FindIntermediate(id) != null ||
                (sample != null && Array.Exists(sample.Components, part => LabRefining.IsAcidTarget(sample, part.Symbol)));
        }

        public static void OpenLeaching(GameItem acid, GameItem scrap)
        {
            if (!AreInLab(acid, scrap, LabRefining.AcidInstructions))
            {
                return;
            }
            if (AnalyzedSample(scrap.identifier) == null)
            {
                Hint(Loc.Pick("先用光谱仪检测废料。", "Scan the scrap with the Spectrometer first."));
                return;
            }
            Open();
            active?.Load(scrap, active.feed, acid, active.reagent);
        }

        public static void OpenFiltering(GameItem funnel, GameItem liquid)
        {
            if (!AreInLab(funnel, liquid, Loc.Pick("先把漏斗和浸出液放进实验室。", "Bring the funnel and leachate into the lab first.")))
            {
                return;
            }
            Open();
            active?.Load(liquid, active.feed, funnel, active.tool);
        }

        public static void OpenIntermediate(LabRefining.Intermediate material)
        {
            GameItem item = LabInventoryGrid.OpenGrid?.FindItem(material.ItemId);
            Open();
            if (item != null && active != null && item.parentInventory != active.feed)
            {
                active.Load(item, active.feed);
            }
        }

        private static bool AreInLab(GameItem first, GameItem second, string hint)
        {
            LabInventoryGrid grid = LabInventoryGrid.OpenGrid;
            if (grid != null && grid.Contains(first) && grid.Contains(second))
            {
                return true;
            }
            Hint(hint);
            return false;
        }

        private static void Hint(string hint)
        {
            DayHudBridge.Push(hint, "info", 4f);
            OverlayHandler.current?.AppendLog(hint, false);
        }

        private void Load(GameItem material, GameInventory input, GameItem equipment = null, GameInventory rack = null)
        {
            if (LabRefining.Running || ItemMouseDragHandler.current?.IsDraggingItem == true)
            {
                return;
            }
            if (material.parentInventory != input && GraphUtils.TryAcceptAll(input, material) != 0)
            {
                Hint(Loc.Pick("投料格有其他物料或空间不足。", "Clear the input grid or make room for the batch."));
            }
            if (equipment != null && equipment.parentInventory != rack && GraphUtils.TryAcceptOnce(rack, equipment, 1) != 0)
            {
                Hint(Loc.Pick("试剂或工具槽已占用，请手动换料。", "Free the reagent or tool socket before loading."));
            }
            UpdateControls(false);
        }

        public static void Close()
        {
            // 读档/切场景可能已把窗口整个销毁（假 null），IsVisible 会在这里炸。
            foreach (RefiningStation station in openStations)
            {
                if (station != null && station.window != null && station.window.handler != null && station.window.IsVisible())
                {
                    station.window.Hide();
                }
            }
            openStations.Clear();
            if (active == null)
            {
                return;
            }
            RefiningStation closing = active;
            active = null;
            if (closing.machine != null && (closing.machine.contentWindow == null || closing.machine.contentWindow.handler != null))
            {
                closing.machine.Destroy();
            }
            fixtureDock?.Destroy();
            fixtureDock = null;
        }

        private void OnDestroy()
        {
            openStations.Remove(this);
        }

        internal void Report(string message, bool ok, string outputId)
        {
            notice = message;
            noticeOk = ok;
            completed = outputId != null;
            UpdateControls(true);
        }

        private void LateUpdate()
        {
            if (ItemMouseDragHandler.current?.IsDraggingItem != true)
            {
                UpdateControls(false);
            }
            bool running = LabRefining.IsRunning(this);
            if (running)
            {
                string text = Loc.Pick("设备运行中 · 剩余 ", "Processing · ") + GameTicks.Label(LabRefining.RemainingTicks);
                if (state.GetText() != text) state.SetText(text);
            }
            float value = running ? LabRefining.Progress : completed ? 1f : 0f;
            if (progress.value != value)
            {
                progress.SetValue(value);
            }
        }

        private void UpdateControls(bool force)
        {
            var items = feed.childItems;
            var load = (feed: items.FirstOrDefault()?.identifier, count: items.Sum(item => item.unitCount),
                reagent: reagent.childItem?.identifier, tool: tool.childItem?.identifier, running: LabRefining.Running || AcidStation.Running,
                available: IsOperational);
            if (!force && load == lastLoad)
            {
                return;
            }
            bool feedChanged = load.feed != lastLoad.feed;
            bool reagentChanged = load.reagent != lastLoad.reagent;
            if (!force && !load.running)
            {
                notice = null;
                completed = false;
            }
            lastLoad = load;

            SampleDef sample = AnalyzedSample(load.feed);
            LabRefining.Intermediate intermediate = LabRefining.FindIntermediate(load.feed);
            string[] targets = Targets(sample);
            if (feedChanged || (reagentChanged && LabRefining.IsAcid(load.reagent)) || !targets.Contains(target))
            {
                target = targets.FirstOrDefault(symbol => load.reagent != null &&
                    LabRefining.RequiredAcid(sample, symbol)?.ItemId == load.reagent) ?? targets.FirstOrDefault();
            }
            CompoundDef acid = LabRefining.RequiredAcid(sample, target);
            int units = acid == null ? 0 : LabRefining.SamplesPerProduct(Array.Find(sample.Components, part => part.Symbol == target).Percent);
            bool ready = acid != null ? load.reagent == acid.ItemId && load.count >= units
                : intermediate != null && (intermediate.Filtered || load.tool == FunnelId);
            string operation = acid != null ? Loc.Pick("启动酸浸", "Leach")
                : intermediate?.Filtered == true ? Loc.Pick("启动还原", "Reduce")
                : intermediate != null ? Loc.Pick("启动过滤", "Filter") : Loc.Pick("启动设备", "Start");
            runButton.SetText(operation);
            runButton.SetDisabled(!ready || load.running || !load.available);
            runButton.SetDescription(notice ?? "");
            targetButton.SetText(target == null ? Loc.Pick("切换目标", "Change target")
                : Loc.Pick("目标 ", "Target ") + target);
            targetButton.SetDisabled(load.running || targets.Length < 2);
            prepareButton.SetDisabled(!load.available || LabRefining.Running);
            prepareButton.SetDescription(Loc.Pick("打开制酸装置，拖入原料、工具和空试剂瓶。",
                "Open the acid station and load materials, tools and an empty reagent bottle."));
            string stateText = load.feed == null
                ? Loc.Pick("拖入废料、浸出液或滤液", "Drop scrap, leachate or filtrate")
                : Loc.Pick("已装料", "Loaded") + " ×" + load.count;
            if (acid != null && load.reagent != null && load.reagent != acid.ItemId)
            {
                stateText = Loc.Pick("试剂与目标不匹配", "Reagent does not match target");
            }
            if (!load.available)
            {
                stateText = Loc.Pick("设备限在地下实验室使用", "Operates in the underground lab");
            }
            else if (LabRefining.IsRunning(this))
            {
                stateText = Loc.Pick("设备运行中", "Processing");
            }
            else if (AcidStation.Running)
            {
                stateText = Loc.Pick("制酸装置运行中", "Acid station running");
            }
            else if (completed)
            {
                stateText = Loc.Pick("产物已就绪", "Output ready");
            }
            else if (notice != null)
            {
                stateText = noticeOk ? Loc.Pick("试剂已制备", "Acid prepared") : Loc.Pick("操作未完成", "Operation incomplete");
            }
            state.SetText(stateText);
            state.SetColor(notice == null || noticeOk ? new Color32(190, 205, 186, 255) : new Color32(222, 160, 132, 255));
            requirement.SetText(acid != null
                ? ReagentBottle.IsEmpty(load.reagent)
                    ? Loc.Pick("取出空试剂瓶后可装入新试剂", "Remove the empty bottle to reload")
                    : Loc.Pick(acid.NameZh, acid.NameEn) + " ×1  /  " + Loc.Pick("废料 ", "Scrap ") + load.count + "/" + units
                : intermediate?.Filtered == false ? Loc.Pick("安装过滤漏斗", "Insert filter funnel")
                : intermediate != null ? Loc.Pick("内置还原槽", "Built-in reduction cell") : "");
        }

        private static string[] Targets(SampleDef sample)
        {
            return sample == null ? Array.Empty<string>() : sample.Components
                .Where(part => LabRefining.IsAcidTarget(sample, part.Symbol)).Select(part => part.Symbol).ToArray();
        }

        private void CycleTarget()
        {
            string[] choices = Targets(AnalyzedSample(feed.childItems.FirstOrDefault()?.identifier));
            if (choices.Length > 1 && !LabRefining.Running)
            {
                target = choices[(Array.IndexOf(choices, target) + 1) % choices.Length];
                notice = null;
                UpdateControls(true);
            }
        }

        private void Run()
        {
            if (!IsOperational || LabRefining.Running || AcidStation.Running || ItemMouseDragHandler.current?.IsDraggingItem == true)
            {
                return;
            }
            LabInventoryGrid.CaptureAfterDrop();
            UpdateControls(false);
            if (runButton.isDisabled)
            {
                return;
            }
            SampleDef sample = AnalyzedSample(lastLoad.feed);
            LabRefining.Intermediate material = LabRefining.FindIntermediate(lastLoad.feed);
            if (sample != null)
            {
                LabRefining.StartLeaching(this, lastLoad.reagent, sample, target);
            }
            else if (material?.Filtered == false)
            {
                LabRefining.StartFiltering(this, material);
            }
            else if (material != null)
            {
                LabRefining.StartReduction(this, material);
            }
        }

        private void PrepareAcid()
        {
            CompoundDef acid = LabRefining.RequiredAcid(AnalyzedSample(lastLoad.feed), target);
            if (!IsOperational || LabRefining.Running)
            {
                return;
            }
            if (AcidStation.Open(acid, () =>
                {
                    if (this != null && IsOperational)
                    {
                        window.Show();
                        window.ToFront();
                    }
                    else
                    {
                        Open();
                    }
                }))
            {
                window.Hide();
            }
        }

        internal bool TryFinish(string inputId, int inputUnits, string reagentId, string requiredToolId, string outputId)
        {
            if (!IsOperational || (requiredToolId != null && tool.childItem?.identifier != requiredToolId))
            {
                return false;
            }
            var inputs = new List<(GameItem Item, int Units)>();
            if (!MachineBatch.AddInputs(inputs, feed, inputId, inputUnits))
            {
                return false;
            }
            LabInventoryGrid grid = IsFixture ? LabInventoryGrid.OpenGrid : null;
            if (reagentId == null)
            {
                return MachineBatch.TryCommit(grid, inputs, (outputId, output));
            }
            GameItem acid = reagent.childItem;
            if (acid?.identifier != reagentId)
            {
                return false;
            }
            inputs.Add((acid, 1));
            return acid.unitCount == 1 && ReagentBottle.IsLiquid(reagentId)
                ? MachineBatch.TryCommit(grid, inputs, (outputId, output),
                    (ReagentBottle.EmptyFor(reagentId, ReagentBottle.SizeOf(acid)), reagent))
                : MachineBatch.TryCommit(grid, inputs, (outputId, output));
        }

        private sealed class StationGrid : GameGridInventory
        {
            public StationGrid(int width, int height) : base(width, height) { }
            public override bool SupportsGroupPlacement => false;
        }
    }
}
