using System;
using System.Linq;
using UnityEngine;
using static Chemistry.NitricProductionData;

namespace Chemistry
{
    internal sealed partial class NitricProduction
    {
        private PixelWindow controlWindow;
        private readonly System.Collections.Generic.List<PixelWindow> windows = new System.Collections.Generic.List<PixelWindow>();
        private MachineryText dashboard;
        private MachineryText requirements;
        private MachineryButton factoryButton;
        private MachineryButton targetButton;
        private MachineryButton buildLab;
        private MachineryButton buildFactory;
        private MachineryButton stockRecipe;
        private MachineryButton salvageButton;
        private MachineryButton buyButton;
        private Action back = RefiningStation.Open;
        private long nextRefresh;

        private sealed class StepView
        {
            internal PixelWindow Window;
            internal MachineryText State;
            internal MachineryText Mode;
            internal MachineryButton Quantity;
            internal MachineryButton Run;
            internal MachineryButton Recipe;
            internal MachineryProgressBar Progress;
        }

        private StepView[] views;

        private sealed class MachineGrid : GameGridInventory
        {
            internal MachineGrid(int width, int height) : base(width, height) { }
            public override bool SupportsGroupPlacement => false;
        }

        private GameGridInventory NewGrid(int width, int height, Func<string, bool> accepts)
        {
            var inventory = new MachineGrid(width, height);
            inventories.Add(inventory);
            inventory.mayInventoryAddItemFunc = (item, _) => restoring || MachineBatch.IsPlacing(item, inventory)
                || (item != null && accepts(item.identifier) && grid.CanReceive(item));
            return inventory;
        }

        private void Build()
        {
            works = NewGrid(6, 4, id => id == Parts || id == Power || id == NitricProductionData.Product.ItemId
                || id == ChemistryCatalog.ToolItemId("distiller") || id == ChemistryCatalog.ToolItemId("autoclave") || CanSalvage(id));
            stock = NewGrid(6, 4, id => NitricProductionData.FeedUnits(id, true) > 0);
            utilities = NewGrid(6, 4, id => id == Power || ReagentBottle.IsGlassEmpty(id));
            warehouse = NewGrid(8, 4, _ => false);
            controlWindow = NewWindow(Loc.Pick("硝酸生产 · 控制台", "Nitric acid · Control"));
            dashboard = Label();
            requirements = Label();
            buildLab = Button("建造实验室设备", "Build lab equipment", () => BuildUpgrade(false), 88);
            buildFactory = Button("建造工业联机设备", "Build industrial line", () => BuildUpgrade(true), 94);
            factoryButton = Button("启动联机", "Start line", ToggleFactory, 64);
            targetButton = Button("库存上限 8", "Stock limit 8", () => { limit = limit == BufferLimit ? 2 : limit + 2; }, 76);
            stockRecipe = Button("进料模式", "Feed recipe", ChangeFeedGrade, 91);
            salvageButton = Button("拆解废旧电路板", "Salvage circuit board", Salvage, 100);
            buyButton = Button("购买零件 · 18", "Buy part · 18", BuyPart, 74);
            controlWindow.Attach(new MachineryPanelRow(MachineHelper.Panel(Loc.Pick("生产控制", "Production controls"),
                MachineryStack.Vertical(Navigation(), dashboard,
                    MachineryStack.Horizontal(factoryButton, targetButton, stockRecipe),
                    requirements, MachineryStack.Horizontal(buildLab, buildFactory),
                    MachineryStack.Horizontal(salvageButton, buyButton),
                    MachineryStack.Horizontal(
                        Bay("施工", "Construction", works, "tool"),
                        Bay("原料仓", "Feed stock", stock, "input"),
                        Bay("空瓶 / 动力", "Bottles / power", utilities, "bottle"),
                        Bay("成品仓", "Finished stock", warehouse, "output"))), "control")));

            views = new StepView[steps.Length];
            for (int index = 0; index < steps.Length; index++)
            {
                views[index] = BuildStep(steps[index]);
            }
        }

        private StepView BuildStep(Step step)
        {
            var view = new StepView { Window = NewWindow(Loc.Pick("硝酸生产 · ", "Nitric acid · ") + step.Name) };
            step.Input = NewGrid(4, 4, id => step.Feed == null ? NitricProductionData.FeedUnits(id, false) > 0 : id == step.Feed);
            step.Output = NewGrid(4, 4, _ => false);
            var banks = new System.Collections.Generic.List<PixelElement>
            {
                Bay("投料", "Feed", step.Input, "input"),
                Bay("产物", "Output", step.Output, "output"),
            };
            if (step.Tool != null)
            {
                var tool = new GameSlotInventory(2, 2);
                step.ToolSlot = tool;
                inventories.Add(tool);
                tool.mayInventoryAddItemFunc = (item, _) => item?.identifier == ChemistryCatalog.ToolItemId(step.Tool) &&
                    (restoring || grid.CanReceive(item));
                ToolDef def = ChemistryCatalog.FindTool(step.Tool);
                banks.Insert(1, MachineHelper.Panel(Loc.Pick(def.NameZh, def.NameEn), tool, "tool"));
            }
            else
            {
                step.Bottles = NewGrid(4, 4, ReagentBottle.IsGlassEmpty);
                banks.Insert(1, Bay("空试剂瓶", "Empty bottles", step.Bottles, "bottle"));
            }

            view.Mode = Label();
            view.State = Label();
            view.Progress = new MachineryProgressBar(width: 146);
            view.Quantity = Button("批量 1", "Batch size 1", () => { step.Requested = step.Requested % BatchLimit + 1; }, 82);
            view.Run = Button("手动加工", "Process manually", () => Request(step), 92);
            MachineryStack actions = MachineryStack.Horizontal(view.Quantity, view.Run);
            if (step.Feed == null)
            {
                view.Recipe = Button("进料模式", "Feed recipe", ChangeFeedGrade, 91);
                actions.Add(view.Recipe);
            }
            view.Window.Attach(new MachineryPanelRow(MachineHelper.Panel(step.Name,
                MachineryStack.Vertical(Navigation(), view.Mode, StepPrompt(step), view.State, view.Progress,
                    actions, MachineryStack.Horizontal(banks.ToArray())), "control")));
            return view;
        }

        private PixelWindow NewWindow(string name)
        {
            var window = new PixelWindow(true, name, WindowStyle.Machinery);
            windows.Add(window);
            ChemFontFix.Attach(window.handler.gameObject);
            window.Hide();
            return window;
        }

        private MachineryStack Navigation()
        {
            var buttons = steps.Select(step => new MachineryButton(step.Name, () => Show(step), 72, 12))
                .Cast<PixelElement>().ToList();
            buttons.Add(Button("控制台", "Control", () => Show(null), 56));
            buttons.Add(Button("返回", "Back", () =>
            {
                if (Dragging) return;
                HideWindows();
                back.Invoke();
            }, 44));
            return MachineryStack.Horizontal(buttons.ToArray());
        }

        private void Show(Step step)
        {
            if (Dragging) return;
            HideWindows();
            visibleStep = step;
            PixelWindow window = step == null ? controlWindow : views[Array.IndexOf(steps, step)].Window;
            window.Show();
            Refresh(true);
            window.Center();
            window.rectTransform.anchoredPosition = new Vector2(window.rectTransform.anchoredPosition.x, -12f);
            window.FixBounds();
            window.ToFront();
        }

        private void HideWindows()
        {
            foreach (PixelWindow window in windows)
            {
                window.Hide();
            }
        }

        private void DestroyWindows()
        {
            foreach (GameInventory inventory in inventories)
            {
                foreach (GameItem item in inventory.childItems.ToArray())
                {
                    item.Destroy();
                }
            }
            foreach (PixelWindow window in windows)
            {
                window.Destroy();
            }
        }

        private void Refresh(bool force = false)
        {
            long now = GameTicks.Now;
            if (!force && now < nextRefresh) return;
            nextRefresh = now + 2;
            if (controlWindow.IsVisible())
            {
                RefreshControl(now);
                return;
            }
            if (visibleStep == null) return;

            int index = Array.IndexOf(steps, visibleStep);
            Step step = visibleStep;
            StepView view = views[index];
            if (!view.Window.IsVisible()) return;
            bool ready = TryInputs(index, out _, out string reason);
            SetText(view.Mode, automatic ? Loc.Pick("联机运行 · 物料由控制台调度", "Linked · controlled from the terminal")
                : BatchUnlocked ? Loc.Pick("实验室 · 各工位独立排队，手动转料", "Laboratory · queue jobs and move goods by hand")
                : Loc.Pick("手工作坊 · 每次处理一份", "Workshop · one dose per operation"));
            SetText(view.State, Describe(index, ready, reason));
            view.Progress.SetValue(Progress(step, now));
            UpdateButton(view.Quantity, Loc.Pick("批量 ", "Batch size ") + step.Requested, automatic || !BatchUnlocked || step.Remaining > 0);
            UpdateButton(view.Run, step.Remaining > 0 ? Loc.Pick("取消队列", "Cancel queue")
                : BatchUnlocked ? Loc.Pick("加入队列", "Queue work") : Loc.Pick("手动加工", "Process manually"),
                automatic || (!BatchUnlocked && step.Remaining == 0 && !ready));
            if (view.Recipe != null)
            {
                UpdateButton(view.Recipe, RecipeLabel(), automatic || step.Remaining > 0);
            }
        }

        private void RefreshControl(long now)
        {
            string mode = automatic ? Loc.Pick("联机已开启", "Line enabled")
                : FactoryUnlocked ? Loc.Pick("工业设备就绪", "Industrial line ready")
                : BatchUnlocked ? Loc.Pick("实验室批处理已解锁", "Laboratory batches unlocked")
                : Loc.Pick("手工作坊", "Workshop");
            string flow = string.Join(" → ", steps.Select(step => step.Name));
            if (FactoryUnlocked)
            {
                flow = Loc.Pick("原料仓 → 分拣 → ", "Feed stock → Sort → ") + flow + Loc.Pick(" → 成品仓", " → Finished stock");
            }
            string stages = string.Join("\n", steps.Select((step, index) =>
            {
                bool ready = TryInputs(index, out _, out string reason);
                int inStock = Units(automatic && index == 0 ? stock : step.Input);
                int outStock = Units(Destination(index));
                return step.Name + "  " + Loc.Pick("入/出 ", "In/out ") + inStock + "/" + outStock
                    + "  ·  " + Describe(index, ready, reason)
                    + (step.Finish > 0 ? "  " + Mathf.RoundToInt(100 * Progress(step, now)) + "%" : "");
            }));
            int rate = operatingTicks > 0 ? Mathf.RoundToInt(produced * (60f * GameTicks.PerMinute) / operatingTicks) : 0;
            string inventory = Loc.Pick("原料 ", "Feed ") + Units(stock) + Loc.Pick(" · 空瓶 ", " · Bottles ")
                + utilities.childItems.Where(item => ReagentBottle.IsGlassEmpty(item.identifier)).Sum(item => item.unitCount)
                + Loc.Pick(" · 动力 ", " · Power ") + Units(utilities, Power)
                + Loc.Pick(" · 成品 ", " · Finished ") + Units(warehouse) + "/" + limit;
            SetText(dashboard, Loc.Pick("硝酸 HNO₃ · ", "Nitric acid HNO₃ · ") + mode + "\n" + flow + "\n" + stages
                + "\n" + inventory + "\n" + Loc.Pick("本次运行 ", "This run ") + produced
                + Loc.Pick(" 份 · ", " dose(s) · ") + rate + Loc.Pick(" 份/游戏小时", " doses/game hour"));
            SetText(requirements, (notice == null ? "" : notice + "\n") + ConstructionText());
            UpdateButton(factoryButton, automatic ? Loc.Pick("停止联机", "Stop line") : Loc.Pick("启动联机", "Start line"), !FactoryUnlocked);
            UpdateButton(targetButton, Loc.Pick("库存上限 ", "Stock limit ") + limit, !FactoryUnlocked);
            UpdateButton(stockRecipe, RecipeLabel(), !FactoryUnlocked || automatic);
            SetDisabled(buildLab, automatic || BatchUnlocked || !HasBuildInputs(false));
            SetDisabled(buildFactory, automatic || !BatchUnlocked || FactoryUnlocked || !HasBuildInputs(true));
            SetDisabled(salvageButton, !works.childItems.Any(item => CanSalvage(item.identifier)));
            SetDisabled(buyButton, PlayerStore.Instance?.CanPay(PartPrice) != true);
        }

        private bool HasBuildInputs(bool industrial) => BuildRequirements(industrial).All(part => Units(works, part.Id) >= part.Units);

        private string ConstructionText()
        {
            if (FactoryUnlocked)
            {
                return Loc.Pick("离开实验室时暂停，返回自动继续。", "The line pauses while you are away and resumes when you return.");
            }
            return BatchUnlocked
                ? Loc.Pick("联机施工需要 8 个回收机件、2 个动力匣和 1 台高压灭菌锅。", "Build line with 8 salvaged parts, 2 power packs and 1 autoclave.")
                : Loc.Pick("实验室施工：4 回收机件、1 份硝酸、1 蒸馏器。", "Build lab: 4 salvaged parts, 1 nitric acid dose, 1 distiller.");
        }

        private string RecipeLabel() => feedGrade == 0 ? Loc.Pick("优先高品位", "Prefer high grade")
            : feedGrade == 1 ? Loc.Pick("仅高品位", "High grade only") : Loc.Pick("仅低品位", "Low grade only");

        private void ChangeFeedGrade()
        {
            if (!Dragging && !automatic && steps[0].Remaining == 0) feedGrade = (feedGrade + 1) % 3;
        }

        private string Describe(int index, bool ready, string reason)
        {
            Step step = steps[index];
            string text = step.Finish > 0 ? Loc.Pick("加工中", "Processing")
                : step.Failure ?? (ready ? Loc.Pick("就绪", "Ready") : reason);
            return step.Remaining == 0 ? text : text + Loc.Pick(" · 队列剩余 ", " · Queued ") + step.Remaining;
        }

        private static MachineryText StepPrompt(Step step)
        {
            var text = Label();
            string prompt = step.Feed == null
                ? Loc.Pick("先用光谱仪检测采样气瓶。高品位用 1 件，低品位用 2 件。", "Scan gas samplers first. Use 1 high-grade or 2 low-grade samples.")
                : NitricProductionData.Find(step.Feed).Name + " → " +
                    (NitricProductionData.Find(step.Product)?.Name ?? Loc.Pick("硝酸（试剂瓶）", "Nitric acid (bottled)"));
            text.SetText(prompt);
            return text;
        }

        private static float Progress(Step step, long now) => step.Finish == 0 ? 0f
            : Mathf.Clamp01((now - step.Started) / (float)(step.Finish - step.Started));

        private static MachineryText Label() => new MachineryText(size: 7, uppercase: false);
        private static MachineryButton Button(string zh, string en, Action action, int width) => new MachineryButton(Loc.Pick(zh, en), action, width, 12);
        private static MachineryPanel Bay(string zh, string en, PixelElement inventory, string type) => MachineHelper.Panel(Loc.Pick(zh, en), inventory, type);

        private static void SetText(MachineryText label, string text)
        {
            if (label.GetText() != text) label.SetText(text);
        }

        private static void UpdateButton(MachineryButton button, string text, bool disabled)
        {
            if (button.text != text) button.SetText(text);
            SetDisabled(button, disabled);
        }

        private static void SetDisabled(MachineryButton button, bool disabled)
        {
            if (button.isDisabled != disabled) button.SetDisabled(disabled);
        }
    }
}
