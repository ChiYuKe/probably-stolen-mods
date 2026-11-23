using System.Collections.Generic;
using HarmonyLib;

namespace Chemistry
{
    /// <summary>原版框选（Ctrl 多选）按可拿性收集物品，没有配色板口子；
    /// 筛选模式下变暗的物品不该被框进去，这里把它们从框选结果里剔除。</summary>
    [HarmonyPatch(typeof(ItemMultiSelectHandler), "ComputeBandItems")]
    internal static class MultiSelectFilter
    {
        private static void Postfix(List<GameItemElement> __result)
        {
            LabInventoryGrid grid = LabInventoryGrid.OpenGrid;
            if (grid == null || __result == null || __result.Count == 0)
            {
                return;
            }
            __result.RemoveAll(item => item != null && grid.OwnsPage(item.parentInventory) && grid.IsFilteredOut(item));
        }
    }

    /// <summary>整组精确放置用 Expel + UncheckedAccept 提交，绕过 mayInventoryAddItemFunc；
    /// 同库存重排数量不变没关系，跨库存放进实验室储物格时在这里补总量与白名单校验。</summary>
    [HarmonyPatch(typeof(ItemMultiSelectHandler), "CommitPrecisePlacement")]
    internal static class GroupPlacementGate
    {
        private static bool Prefix(GameInventory inventory, List<GameItemElement> ___selectedItems)
        {
            LabInventoryGrid grid = LabInventoryGrid.OpenGrid;
            if (grid == null || inventory != grid.StorageInventoryArea || ___selectedItems == null
                || ___selectedItems.Count == 0)
            {
                return true;
            }

            GameInventory home = null;
            foreach (GameItemElement item in ___selectedItems)
            {
                if (item == null || item.parentInventory == null)
                {
                    return true;
                }
                if (home == null)
                {
                    home = item.parentInventory;
                }
                else if (home != item.parentInventory)
                {
                    return true;
                }
            }
            if (home == inventory)
            {
                return true;
            }

            return grid.MayAcceptGroup(new List<GameItem>(___selectedItems));
        }
    }
}
