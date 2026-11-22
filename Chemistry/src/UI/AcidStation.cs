using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chemistry
{
    internal sealed class AcidStation : MonoBehaviour
    {
        internal const string ItemId = ChemistryCatalog.ItemPrefix + "acid_station";
        private const int CycleTicks = 64;
        private const string ReadyColor = "#B5CEB4";
        private const string PendingColor = "#D1C2A9";
        private const string BlockedColor = "#E0A08C";
        private static readonly CompoundDef[] Recipes =
        {
            ChemistryCatalog.FindCompound("hydrochloric_acid"),
            ChemistryCatalog.FindCompound("sulfuric_acid"),
            ChemistryCatalog.FindCompound("acetic_acid"),
            ChemistryCatalog.FindCompound("phosphoric_acid"),
            ChemistryCatalog.FindCompound("hydrofluoric_acid"),
        };
        private static AcidStation active;
        private static GameItem fixture;
        private static PixelWindow fixtureDock;
        private PixelWindow window;
        private GameGridInventory materials;
        private GameSlotInventory bottle;
        private GameSlotInventory output;
        private readonly GameSlotInventory[] tools = new GameSlotInventory[3];
        private readonly MachineryText[] toolLabels = new MachineryText[3];
        private GameInventory[] inventories;
        private MachineryText recipeLabel;
        private MachineryText materialLabel;
        private MachineryText bottleLabel;
        private MachineryText outputLabel;
        private MachineryText phaseLabel;
        private MachineryText status;
        private MachineryProgressBar progress;
        private MachineryButton start;
        private MachineryButton previous;
        private MachineryButton next;
        private MachineryButton back;
        private Action returnToRefining;
        private int recipeIndex;
        private long startedAt;
        private bool running;
        private string failure;

        private CompoundDef Recipe => Recipes[recipeIndex];
        internal static IReadOnlyList<CompoundDef> StandardRecipes => Recipes;
        internal static bool Running => active != null && active.running;
        internal static IReadOnlyList<GameInventory> Inventories => active?.inventories ?? Array.Empty<GameInventory>();
        internal static bool HasRecipe(CompoundDef def) =>
            def != null && (def.Key == NitricProductionData.Key || Array.IndexOf(Recipes, def) >= 0);
        internal static string RequiredMachine => Loc.Pick("在地下实验室的制酸装置中制备。", "Prepare this reagent in the underground lab's acid station.");

        internal static void Attach(GameItem item)
        {
            var launcher = new PixelWindow(true, item.name, WindowStyle.Machinery);
            launcher.openCallback = () =>
            {
                launcher.Hide();
                if (!Open() && LabInventoryGrid.OpenGrid == null)
                {
                    OverlayHandler.current?.AppendLog(RequiredMachine, false);
                }
            };
            item.SetContentWindow(launcher);
            launcher.Hide();
            item.mayItemTargetThisFunc = CanDropOnMachine;
            item.canItemTargetThisFunc = (source, target) => CanDropOnMachine(source, target) && !Running;
            item.onItemTargetThisFunc = (source, target) =>
                ModHelper.Delay(ChemistryCatalog.ModId, 0.15f, () => LoadFromMachineDrop(source, target));
        }

        internal static void InstallFixture()
        {
            if (fixtureDock != null)
            {
                return;
            }
            GameItem machine = null;
            PixelWindow dock = null;
            try
            {
                machine = ModHelper.SpawnItem(ItemId);
                if (machine?.contentWindow == null)
                {
                    throw new InvalidOperationException(ItemId);
                }

                ModHelper.SetItemOwnerShip(machine, true);
                var mount = new GameSlotInventory(machine.shape.width, machine.shape.height).SetTransparency(1f);
                dock = new PixelWindow().SetTransparency(1f);
                dock.Attach(mount);
                if (GraphUtils.TryAcceptOnce(mount, machine, 1) != 0)
                {
                    throw new InvalidOperationException("安装设备失败");
                }
                mount.mayInventoryAddItemFunc = (_, _) => false;
                mount.mayInventoryRemoveItemFunc = (_, _) => false;
                mount.maxSlotRemoveItemFunc = (_, _, _) => 0;
                dock.Show();
                fixture = machine;
                fixtureDock = dock;
                PositionFixture();
            }
            catch (Exception error)
            {
                fixture = null;
                fixtureDock = null;
                machine?.Destroy();
                dock?.Destroy();
                ChemistryMod.Log?.Warning("安装制酸装置失败：" + error);
            }
        }

        internal static void PositionFixture() => RefiningStation.PositionFixture(fixtureDock, 0.90f, 0.59f);

        private static bool CanDropOnMachine(GameItem source, GameItem target)
        {
            LabInventoryGrid grid = LabInventoryGrid.OpenGrid;
            return grid != null && target != null && (target == fixture || grid.Contains(target))
                && source != null && grid.Contains(source)
                && (ReagentBottle.IsGlassEmpty(source.identifier) || ReagentBottle.IsPlasticEmpty(source.identifier) || Recipes.Any(recipe =>
                    recipe.Tools.Any(tool => MatchesTool(source, tool))));
        }

        private static void LoadFromMachineDrop(GameItem source, GameItem target)
        {
            if (!CanDropOnMachine(source, target) || Running || !Open())
            {
                return;
            }

            GameSlotInventory slot = active.bottle;
            if (!ReagentBottle.IsEmpty(source.identifier))
            {
                int index = Array.FindIndex(active.Recipe.Tools, tool => MatchesTool(source, tool));
                if (index < 0)
                {
                    active.failure = Loc.Pick("此配方不使用该工具", "This recipe does not use that tool");
                    active.Refresh();
                    return;
                }
                slot = active.tools[index];
            }

            active.failure = slot == active.bottle && !ReagentBottle.AcceptsEmpty(active.Recipe.ItemId, source.identifier)
                ? active.BottlePrompt()
                : source.parentInventory != slot && GraphUtils.TryAcceptOnce(slot, source, 1) != 0
                    ? Loc.Pick("装槽失败，请检查槽位", "Could not load. Check the socket.")
                    : null;
            active.Refresh();
        }

        internal static bool Open(CompoundDef preferred = null, Action onBack = null)
        {
            if (preferred?.Key == NitricProductionData.Key)
            {
                return NitricProduction.Open(onBack ?? RefiningStation.Open);
            }
            if (LabInventoryGrid.OpenGrid == null || Dragging())
            {
                return false;
            }
            if (active == null)
            {
                var panel = new PixelWindow(true, Loc.Pick("制酸装置", "Acid preparation"), WindowStyle.Machinery);
                AcidStation station = panel.handler.gameObject.AddComponent<AcidStation>();
                station.window = panel;
                try
                {
                    station.Build();
                    panel.Hide();
                    active = station;
                }
                catch (Exception error)
                {
                    panel.Destroy();
                    ChemistryMod.Log?.Warning("创建制酸装置失败：" + error);
                    return false;
                }
            }

            if (preferred != null && HasRecipe(preferred) && !active.running)
            {
                active.SelectRecipe(Array.IndexOf(Recipes, preferred));
            }
            active.returnToRefining = onBack ?? RefiningStation.Open;
            NitricProduction.Hide();
            LabPanel.Shutdown();
            active.Refresh();
            if (!active.window.IsVisible())
            {
                active.window.Show();
                active.window.Center();
                active.window.rectTransform.anchoredPosition = new Vector2(active.window.rectTransform.anchoredPosition.x, -12f);
                active.window.FixBounds();
            }
            active.window.ToFront();
            return true;
        }

        internal static void Hide() => active?.window.Hide();

        internal static void Close()
        {
            fixture?.Destroy();
            fixtureDock?.Destroy();
            fixture = null;
            fixtureDock = null;
            AcidStation station = active;
            if (station == null)
            {
                return;
            }
            active = null;
            foreach (GameInventory area in station.inventories)
            {
                foreach (GameItem item in area.childItems.ToArray())
                {
                    item.Destroy();
                }
            }
            station.window.Destroy();
        }

        private void Build()
        {
            materials = new MaterialGrid();
            bottle = new GameSlotInventory(2, 2);
            output = new GameSlotInventory(2, 2);
            materials.mayInventoryAddItemFunc = (item, _) => CanLoad(item) &&
                Recipe.Inputs.Keys.Any(symbol => ChemistryCatalog.ElementItemId(symbol) == item.identifier);
            bottle.mayInventoryAddItemFunc = (item, _) => CanLoad(item) && ReagentBottle.AcceptsEmpty(Recipe.ItemId, item.identifier);
            output.mayInventoryAddItemFunc = (item, _) => MachineBatch.IsPlacing(item, output);

            var toolWidgets = new PixelElement[tools.Length];
            for (int i = 0; i < tools.Length; i++)
            {
                int position = i;
                var socket = new GameSlotInventory(2, 2);
                socket.mayInventoryAddItemFunc = (item, _) => CanLoad(item) && IsToolForSlot(item, position);
                tools[i] = socket;
                toolLabels[i] = new MachineryText(size: 7, uppercase: false);
                toolWidgets[i] = MachineryStack.Vertical(toolLabels[i], socket);
            }

            inventories = new GameInventory[] { materials, bottle, output }.Concat(tools).ToArray();
            foreach (GameInventory area in inventories)
            {
                area.mayInventoryRemoveItemFunc = (_, _) => !running;
                area.maxSlotRemoveItemFunc = (_, _, _) => running ? 0 : int.MaxValue;
            }

            recipeLabel = new MachineryText(size: 8, uppercase: false);
            materialLabel = new MachineryText(size: 7, uppercase: false);
            status = new MachineryText(size: 7, uppercase: false);
            progress = new MachineryProgressBar(width: 108);
            previous = new MachineryButton("<", () => ChangeRecipe(-1), 18, 12);
            next = new MachineryButton(">", () => ChangeRecipe(1), 18, 12);
            start = new MachineryButton(Loc.Pick("启动制备", "Start batch"), StartBatch, 66, 13);
            back = new MachineryButton(Loc.Pick("返回", "Back"), Back, 44, 13);
            var nitric = new MachineryButton(Loc.Pick("硝酸生产线", "Nitric acid line"), () => NitricProduction.Open(() => Open()), 96, 12);
            bottleLabel = new MachineryText(size: 7, uppercase: false);
            outputLabel = new MachineryText(size: 7, uppercase: false);
            phaseLabel = new MachineryText(size: 7, uppercase: false);
            window.Attach(new MachineryPanelRow(
                MachineHelper.Panel(Loc.Pick("① 投料", "1. Ingredients"), MachineryStack.Vertical(materialLabel, materials), "input"),
                MachineHelper.Panel(Loc.Pick("② 工具", "2. Tools"), MachineryStack.Horizontal(toolWidgets), "tool"),
                MachineHelper.Panel(Loc.Pick("③ 容器", "3. Container"), MachineryStack.Vertical(bottleLabel, bottle), "bottle").SetMinWidth(64),
                MachineHelper.Panel(Loc.Pick("④ 制备", "4. Run"), MachineryStack.Vertical(
                    MachineryStack.Horizontal(previous, recipeLabel, next), status, phaseLabel, progress, MachineryStack.Horizontal(start, back), nitric),
                    "control").SetMinWidth(134),
                MachineHelper.Panel(Loc.Pick("⑤ 成品", "5. Output"), MachineryStack.Vertical(outputLabel, output), "output").SetMinWidth(52)));
            ChemFontFix.Attach(window.handler.gameObject);
        }

        private bool CanLoad(GameItem item) => !running && LabInventoryGrid.OpenGrid?.CanReceive(item) == true;

        private bool IsToolForSlot(GameItem item, int position) =>
            position < Recipe.Tools.Length && MatchesTool(item, Recipe.Tools[position]);

        private static bool MatchesTool(GameItem item, string tool) => item != null &&
            (item.identifier == ChemistryCatalog.ToolItemId(tool) ||
                (tool == "conical_flask" && item.identifier == ChemistryCatalog.ToolItemId("beaker")));

        private static bool Dragging() => ItemMouseDragHandler.current?.IsDraggingItem == true;

        private string RequiredBottleId => ReagentBottle.EmptyFor(Recipe.ItemId);

        private bool HasEmptyBottle => bottle.childItem?.unitCount == 1 && ReagentBottle.AcceptsEmpty(Recipe.ItemId, bottle.childItem.identifier);

        private bool WillDamageBottle => ReagentBottle.DamagesGlass(Recipe.ItemId, bottle.childItem?.identifier);

        private string BottlePrompt() => RequiredBottleId == ReagentBottle.PlasticEmptyId
            ? Loc.Pick("推荐空塑料瓶；玻璃瓶会损坏", "Use plastic to keep the reagent; glass will be damaged")
            : Loc.Pick("装入 1 个空玻璃试剂瓶", "Insert one empty glass bottle");

        private void ChangeRecipe(int direction)
        {
            if (!running && !Dragging())
            {
                SelectRecipe((recipeIndex + direction + Recipes.Length) % Recipes.Length);
                Refresh();
            }
        }

        private void SelectRecipe(int index)
        {
            if (recipeIndex != index)
            {
                recipeIndex = index;
                failure = null;
            }
        }

        private void Back()
        {
            if (!Dragging())
            {
                window.Hide();
                returnToRefining?.Invoke();
            }
        }

        private string CheckLoad(out Dictionary<string, int> counts)
        {
            counts = new Dictionary<string, int>();
            bool wrongMaterial = false;
            foreach (GameItem item in materials.childItems)
            {
                ElementDef element = ChemistryCatalog.FindElementByItemId(item.identifier);
                if (element == null || !Recipe.Inputs.ContainsKey(element.Symbol))
                {
                    wrongMaterial = true;
                    continue;
                }
                counts.TryGetValue(element.Symbol, out int amount);
                counts[element.Symbol] = amount + item.unitCount;
            }
            if (output.childItem != null)
            {
                return Loc.Pick("请取走成品槽物品", "Clear the output slot");
            }
            if (wrongMaterial)
            {
                return Loc.Pick("请移除其他配方的原料", "Remove ingredients from the previous recipe");
            }
            foreach (var input in Recipe.Inputs)
            {
                if (!counts.TryGetValue(input.Key, out int amount) || amount < input.Value)
                {
                    return Loc.Pick("按配比装入原料", "Load the listed ingredients");
                }
            }
            if (!HasEmptyBottle)
            {
                return BottlePrompt();
            }
            for (int i = 0; i < tools.Length; i++)
            {
                if (i >= Recipe.Tools.Length)
                {
                    if (tools[i].childItem != null)
                    {
                        return Loc.Pick("请清空不使用的工具槽", "Clear unused tool sockets");
                    }
                    continue;
                }
                if (!IsToolForSlot(tools[i].childItem, i))
                {
                    return Loc.Pick("装入对应工具", "Fit the listed tools");
                }
            }
            return null;
        }

        private void StartBatch()
        {
            LabInventoryGrid grid = LabInventoryGrid.OpenGrid;
            if (grid == null || running || LabRefining.Running || Dragging() || CheckLoad(out _) != null)
            {
                return;
            }
            running = true;
            failure = null;
            startedAt = GameTicks.Now;
            CompoundDef recipe = Recipe;
            GameTicks.Schedule(CycleTicks, () => FinishBatch(grid, recipe));
            Refresh();
        }

        private void FinishBatch(LabInventoryGrid grid, CompoundDef recipe)
        {
            if (active != this || LabInventoryGrid.OpenGrid != grid)
            {
                return;
            }
            running = false;
            if (Dragging())
            {
                failure = Loc.Pick("拖拽中，制备已取消", "Batch cancelled during a drag");
            }
            else if (CheckLoad(out _) == null)
            {
                var inputs = new List<(GameItem Item, int Units)>();
                foreach (var input in recipe.Inputs)
                {
                    MachineBatch.AddInputs(inputs, materials, ChemistryCatalog.ElementItemId(input.Key), input.Value);
                }
                inputs.Add((bottle.childItem, 1));
                string result = ReagentBottle.FilledResult(recipe.ItemId, bottle.childItem.identifier);
                if (MachineBatch.TryCommitBottled(grid, inputs, bottle.childItem, result, output))
                {
                    if (result != ReagentBottle.DamagedGlassId)
                    {
                        StoryService.OnLabSynthesized(recipe);
                    }
                    LabPanel.RefreshFromInventory();
                }
                else
                {
                    failure = Loc.Pick("制备未完成，原料保留", "Batch failed. Inputs were kept.");
                }
            }
            else
            {
                failure = Loc.Pick("装料有变，原料保留", "Load changed. Inputs were kept.");
            }
            Refresh();
        }

        private void LateUpdate()
        {
            if (!Dragging())
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            string missing = CheckLoad(out var counts);
            SetText(recipeLabel, Loc.Pick(Recipe.NameZh, Recipe.NameEn) + " " + Recipe.Formula
                + "\n" + (recipeIndex + 1) + "/" + Recipes.Length);
            SetText(materialLabel, string.Join("\n", Recipe.Inputs.Select(input =>
            {
                ElementDef element = ChemistryCatalog.FindElement(input.Key);
                counts.TryGetValue(input.Key, out int amount);
                return Tint(Loc.Pick(element.NameZh, element.NameEn) + " " + input.Key + " " + amount + "/" + input.Value,
                    amount >= input.Value ? ReadyColor : PendingColor);
            })));
            for (int i = 0; i < tools.Length; i++)
            {
                ToolDef tool = i < Recipe.Tools.Length ? ChemistryCatalog.FindTool(Recipe.Tools[i]) : null;
                bool fitted = IsToolForSlot(tools[i].childItem, i);
                bool occupied = tools[i].childItem != null;
                string toolName = tool == null ? Loc.Pick("不使用", "Unused") : Loc.Pick(tool.NameZh, tool.NameEn);
                if (tool?.Key == "conical_flask")
                {
                    toolName = Loc.Pick("锥形瓶 / 烧杯", "Flask / Beaker");
                }
                string toolState = fitted ? Loc.Pick("已装", "Fitted")
                    : occupied ? Loc.Pick("请取出", "Remove")
                    : tool == null ? "—" : Loc.Pick("待装", "Needed");
                SetText(toolLabels[i], Tint(toolName + "\n" + toolState, fitted ? ReadyColor : occupied ? BlockedColor : PendingColor));
            }
            string bottleType = HasEmptyBottle ? ReagentBottle.EmptyName(bottle.childItem.identifier)
                : RequiredBottleId == ReagentBottle.PlasticEmptyId ? Loc.Pick("空塑料试剂瓶", "Empty plastic bottle")
                : Loc.Pick("空玻璃试剂瓶", "Empty glass bottle");
            SetText(bottleLabel, Tint(WillDamageBottle ? Loc.Pick("玻璃瓶会损坏", "Glass will be damaged")
                : bottleType + "\n" + (HasEmptyBottle ? "1/1" : "0/1"),
                WillDamageBottle || !HasEmptyBottle && bottle.childItem != null ? BlockedColor : HasEmptyBottle ? ReadyColor : PendingColor));
            float elapsed = running ? GameTicks.Progress(startedAt, CycleTicks) : 0f;
            bool hasProduct = output.childItem != null;
            CompoundDef product = hasProduct ? Recipes.FirstOrDefault(def => def.ItemId == output.childItem.identifier) : Recipe;
            bool damagedOutput = output.childItem?.identifier == ReagentBottle.DamagedGlassId;
            string outputName = damagedOutput || !hasProduct && WillDamageBottle ? ReagentBottle.DamagedName : product?.Formula ?? Recipe.Formula;
            SetText(outputLabel, Tint(outputName + "\n" +
                (hasProduct ? Loc.Pick("拖出取瓶", "Drag to take") : Loc.Pick("每批 1 份", "1 dose per run")),
                damagedOutput || !hasProduct && WillDamageBottle ? BlockedColor : hasProduct ? ReadyColor : PendingColor));
            SetText(status, BatchStatus(hasProduct, missing));
            int phase = hasProduct ? 3 : !running ? -1 : elapsed < 0.4f ? 0 : elapsed < 0.75f ? 1 : 2;
            SetText(phaseLabel, Phase(Loc.Pick("反应", "React"), 0, phase) + " → "
                + Phase(Loc.Pick("收集", "Collect"), 1, phase) + " → " + Phase(Loc.Pick("灌装", "Bottle"), 2, phase));
            float value = hasProduct ? 1f : elapsed;
            if (progress.value != value)
            {
                progress.SetValue(value);
            }
            SetDisabled(start, running || LabRefining.Running || missing != null);
            SetDisabled(previous, running);
            SetDisabled(next, running);
        }

        private string BatchStatus(bool hasProduct, string missing)
        {
            if (running) return Loc.Pick("制备中 · 剩余 ", "Preparing · ") + GameTicks.Label(GameTicks.Remaining(startedAt, CycleTicks));
            if (output.childItem?.identifier == ReagentBottle.DamagedGlassId) return Loc.Pick("瓶已腐蚀，试剂已损失", "Bottle corroded. Reagent lost.");
            if (hasProduct) return Loc.Pick("已灌装，拖出成品", "Filled. Drag out the bottle.");
            if (LabRefining.Running) return Loc.Pick("反应台运行中", "Refining in progress");
            return failure ?? missing ?? (WillDamageBottle
                ? Loc.Pick("启动会损失本批试剂", "Starting will waste this batch")
                : Loc.Pick("可制备 1 份", "Ready for one dose"));
        }

        private static string Tint(string text, string color) => "<color=" + color + ">" + text + "</color>";

        private static string Phase(string text, int step, int current) =>
            Tint(text, step < current ? ReadyColor : step == current ? "#F1D080" : PendingColor);

        private static void SetText(MachineryText label, string text)
        {
            if (label.GetText() != text)
            {
                label.SetText(text);
            }
        }

        private static void SetDisabled(MachineryButton button, bool disabled)
        {
            if (button.isDisabled != disabled)
            {
                button.SetDisabled(disabled);
            }
        }

        private sealed class MaterialGrid : GameGridInventory
        {
            internal MaterialGrid() : base(6, 4) { }
            public override bool SupportsGroupPlacement => false;
        }
    }
}
