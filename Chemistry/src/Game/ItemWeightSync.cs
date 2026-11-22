using HarmonyLib;

namespace Chemistry
{
    /// <summary>
    /// 数量变化补同步：原版合并/拆分堆叠（SlotMarker.StackItemUnchecked、GameItem.GetSplitItem）
    /// 会触发 onChangeAmountItemFunc，物品工厂里已经订阅；但 SetUnitCount 这条路径不触发事件，
    /// 模组内灌装、机械批量、读档等 20 多处都走它，这里用 postfix 兜住。
    /// SyncWeight 自带 chem_ 前缀闸门，原版物品进这里直接返回，不会被写标签。
    /// </summary>
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.SetUnitCount))]
    internal static class ItemWeightCountSync
    {
        private static void Postfix(GameItem __instance)
        {
            ItemMass.SyncWeight(__instance);
        }
    }

    /// <summary>
    /// 电子秤显示前补同步：装模组之前就生成、还没写过称重标签的旧存档物品，
    /// 没有机会再走物品工厂，靠这里在每次称重刷新时补一次，秤上读数才准。
    /// </summary>
    [HarmonyPatch(typeof(StoreScale), nameof(StoreScale.UpdateScaleDisplay))]
    internal static class ItemWeightScaleSync
    {
        private static void Prefix()
        {
            System.Collections.Generic.List<GameItem> items = EmporiumEntry.Instance?.backInvinvElement?.childItems;
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                ItemMass.SyncWeight(items[i]);
            }
        }
    }
}
