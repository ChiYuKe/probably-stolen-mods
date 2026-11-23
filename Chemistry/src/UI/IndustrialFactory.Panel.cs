using System;
using UnityEngine;
using static Chemistry.IndustrialFactoryData;

namespace Chemistry
{
    internal sealed partial class IndustrialFactory
    {
        private PixelWindow window;
        private GameGridInventory stock;
        private GameGridInventory utilities;
        private GameGridInventory buffer;
        private GameGridInventory products;
        private GameGridInventory liquids;
        private GameGridInventory returns;
        private GameSlotInventory transport;
        private GameSlotInventory preparation;
        private GameSlotInventory dryer;
        private MachineryText status;
        private MachineryText prepStatus;
        private MachineryText recoveryStatus;
        private MachineryButton start;
        private MachineryButton route;
        private MachineryButton limit;
        private MachineryButton assemble;
        private MachineryButton assembleTransport;
        private MachineryProgressBar prepProgress;
        private MachineryProgressBar recoveryProgress;

        private void Build()
        {
            window = new PixelWindow(true, Loc.Pick("自动化化工厂房", "Automated chemistry workshop"), WindowStyle.Machinery);
            transport = Socket(id => id == Transport);
            preparation = Socket(id => Array.Exists(Routes, entry => id == entry.PrepTool));
            dryer = Socket(id => id == Dryer);
            stock = Bank(6, 4, id => Array.Exists(Routes, entry => id == entry.Feed));
            utilities = Bank(6, 4, id => id == NitricProductionData.Power || ReagentBottle.IsGlassEmpty(id));
            buffer = Bank(4, 4, _ => false);
            products = Bank(4, 4, _ => false);
            liquids = Bank(8, 4, _ => false);
            returns = Bank(8, 4, _ => false);

            status = Label();
            prepStatus = Label();
            recoveryStatus = Label();
            prepProgress = new MachineryProgressBar(width: 82);
            recoveryProgress = new MachineryProgressBar(width: 82);
            start = new MachineryButton(Loc.Pick("启动联机", "Start line"), () => ChangeSettings(routeIndex, stockLimit, !automatic), 74, 13);
            route = new MachineryButton(Recipe.Name, () => ChangeSettings((routeIndex + 1) % Routes.Length, stockLimit, false), 122, 13);
            limit = new MachineryButton("", () => ChangeSettings(routeIndex, stockLimit >= MaxLimit ? 2 : stockLimit + 2, automatic), 84, 13);
            assemble = new MachineryButton(Loc.Pick("组装厂房 · 6 机件", "Build workshop · 6 parts"), () => Construct(Frame, FrameParts), 108, 13);
            assembleTransport = new MachineryButton(Loc.Pick("组装管路 · 3 机件", "Build manifold · 3 parts"),
                () => Construct(Transport, TransportParts), 108, 13);

            var console = MachineryStack.Vertical(
                MachineryStack.Horizontal(route, start, limit),
                status,
                MachineryStack.Horizontal(assemble, assembleTransport));
            var hardware = MachineryStack.Horizontal(
                Panel("预处理设备", "Preparation", MachineryStack.Vertical(preparation, prepStatus, prepProgress), "tool"),
                Panel("输送管路", "Transport", transport, "tool"),
                Panel("干燥箱", "Oven", MachineryStack.Vertical(dryer, recoveryStatus, recoveryProgress), "tool"));
            var feed = MachineryStack.Horizontal(
                Panel("投料仓", "Feed stock", stock, "input"),
                Panel("动力匣 / 空瓶", "Power / bottles", utilities, "bottle"),
                Panel("中转缓冲", "Transfer buffer", buffer, "input"));
            var output = MachineryStack.Horizontal(
                Panel("固体成品", "Recovered solids", products, "output"),
                Panel("回收水", "Recovered water", liquids, "output"),
                Panel("退回空瓶", "Returned bottles", returns, "output"));
            window.Attach(new MachineryPanelRow(Panel("联机控制", "Line controls",
                MachineryStack.Vertical(console, hardware, feed, output), "control")));
            ChemFontFix.Attach(window.handler.gameObject);
            window.Hide();
        }

        private bool CanLoad(GameInventory inventory, GameItem item, Func<string, bool> accepts) =>
            restoring || MachineBatch.IsPlacing(item, inventory)
            || (item != null && Unlocked && Installed && accepts(item.identifier) && grid.CanReceive(item));

        private GameGridInventory Bank(int width, int height, Func<string, bool> accepts)
        {
            var area = new FactoryGrid(width, height);
            inventories.Add(area);
            area.mayInventoryAddItemFunc = (item, _) => CanLoad(area, item, accepts);
            return area;
        }

        private GameSlotInventory Socket(Func<string, bool> accepts)
        {
            var area = new GameSlotInventory(4, 4);
            inventories.Add(area);
            area.mayInventoryAddItemFunc = (item, _) => CanLoad(area, item, accepts);
            return area;
        }

        private void Refresh()
        {
            string missing = HardwareBlocker();
            string header = notice ?? missing ?? (automatic ? Loc.Pick("联机中 · 关面板后继续运行", "Running with the panel open or closed")
                : Loc.Pick("待机", "Standby"));
            string toolName = ChemistryCatalog.FindTool(routeIndex == 0 ? "centrifuge" : "hotplate_stirrer")
                is ToolDef tool ? Loc.Pick(tool.NameZh, tool.NameEn) : "";
            Set(status, header + "\n" + toolName + " → " + Loc.Pick("缓冲 → 干燥回收", "buffer → drying and recovery"));
            bool activeLine = automatic && missing == null;
            Set(prepStatus, StageText(prepare, PrepareTicks, activeLine, Loc.Pick("等待投料", "Waiting for feed")));
            Set(recoveryStatus, StageText(recover, RecoverTicks, activeLine, Loc.Pick("等待中转", "Waiting for transfer")));
            SetValue(prepProgress, prepare.Sample == null ? 0f : GameTicks.Progress(prepare.Started, PrepareTicks));
            SetValue(recoveryProgress, recover.Sample == null ? 0f : GameTicks.Progress(recover.Started, RecoverTicks));
            SetButton(start, automatic ? Loc.Pick("暂停联机", "Pause line") : Loc.Pick("启动联机", "Start line"), !automatic && missing != null);
            SetButton(route, Recipe.Name, automatic || Units(buffer) > 0);
            SetButton(limit, Loc.Pick($"库存上限 {stockLimit}", $"Stock limit {stockLimit}"), false);
            SetButton(assemble, Loc.Pick("组装厂房 · 6 机件", "Build workshop · 6 parts"), !CanConstructFrame || !Unlocked);
            SetButton(assembleTransport, Loc.Pick("组装管路 · 3 机件", "Build manifold · 3 parts"), !Unlocked);
        }

        private static string StageText(Stage stage, int ticks, bool activeLine, string idle)
        {
            if (!activeLine) return Loc.Pick("待机", "Standby");
            if (stage.Sample == null) return stage.State ?? idle;
            return Loc.Pick("剩余 ", "Remaining ") + GameTicks.Label(GameTicks.Remaining(stage.Started, ticks));
        }

        private static MachineryText Label() => new MachineryText(size: 7, uppercase: false);
        private static MachineryPanel Panel(string zh, string en, PixelElement content, string kind) =>
            MachineHelper.Panel(Loc.Pick(zh, en), content, kind);

        private static void Set(MachineryText label, string text)
        {
            if (label.GetText() != text) label.SetText(text);
        }
        private static void SetButton(MachineryButton button, string text, bool disabled)
        {
            if (button.text != text) button.SetText(text);
            if (button.isDisabled != disabled) button.SetDisabled(disabled);
        }
        private static void SetValue(MachineryProgressBar bar, float value)
        {
            if (bar.value != value) bar.SetValue(value);
        }

        private sealed class FactoryGrid : GameGridInventory
        {
            internal FactoryGrid(int width, int height) : base(width, height) { }
            public override bool SupportsGroupPlacement => false;
        }
    }
}
