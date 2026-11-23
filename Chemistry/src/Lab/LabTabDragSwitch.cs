using HarmonyLib;

namespace Chemistry
{
    /// <summary>
    /// 拖拽物品悬停到实验室储物窗口的仓库页签上时，自动切到那一页，
    /// 手上的东西就能直接丢进可见网格。原版 ItemMouseDragHandler 只在拖拽中
    /// 才会调 MachineryTabs.UpdateDragHover（高亮页签），所以这个 Postfix
    /// 天然只在拖拽时触发；非拖拽的普通悬停走 mouseEnterFunc，不经过这里。
    /// 页签元素没显式 id、不进 CustomUIWindow 注册表，拖到哪一页只能从
    /// MachineryTabs 的私有状态（dragHoverTabs / hovered）里拿。
    /// </summary>
    [HarmonyPatch(typeof(MachineryTabs), nameof(MachineryTabs.UpdateDragHover))]
    internal static class LabTabDragSwitch
    {
        private static void Postfix()
        {
            LabInventoryGrid grid = LabInventoryGrid.OpenGrid;
            if (grid == null)
            {
                return;
            }

            MachineryTabs tabs = Traverse.Create(typeof(MachineryTabs)).Field("dragHoverTabs").GetValue<MachineryTabs>();
            if (tabs == null || !ReferenceEquals(tabs, grid.PageTabs))
            {
                return;
            }

            int hovered = Traverse.Create(tabs).Field("hovered").GetValue<int>();
            grid.SwitchPageFromDrag(hovered);
        }
    }
}
