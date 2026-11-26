using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chemistry
{
    /// <summary>
    /// 只看玩家自己店里的容器：仓库、前柜、后柜、展示柜、暗格、文件柜、夜间背包、雇员背包、夜市摊位。
    /// 垃圾桶/售货机这种里面有别人的东西，不算可用的原料。
    /// 扫描方式沿用 InventorySearch：从顶层容器顺着 GraphNodeStorage 子树往下走。
    /// </summary>
    internal static class StoreInventory
    {
        private const int MaxDepth = 24;

        public static List<GameInventory> Roots()
        {
            var roots = new List<GameInventory>();
            EmporiumEntry store = EmporiumEntry.Instance;
            if (store == null)
            {
                return roots;
            }

            Add(roots, store.invElement);
            Add(roots, store.frontInvinvElement);
            Add(roots, store.backInvinvElement);
            Add(roots, store.showcaseElement);
            Add(roots, store.hiddenElement);
            Add(roots, store.docInvElement);
            Add(roots, store.afterhourInventory);
            Add(roots, store.hirelingInv);
            Add(roots, store.bazarLeftinvElement);

            return roots;
        }

        private static void Add(List<GameInventory> list, GameInventory inventory)
        {
            if (inventory != null && !list.Contains(inventory))
            {
                list.Add(inventory);
            }
        }

        /// <summary>扫描所有容器里的物品，去重，跳过已经销毁的。</summary>
        public static List<GameItem> AllItems()
        {
            var items = new List<GameItem>();
            var seen = new HashSet<GameItem>();

            foreach (GameInventory root in Roots())
            {
                List<GameItem> found;
                try
                {
                    found = GraphUtils.FindAllChildrenType<GameItem>(root, null, null);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Chemistry] 扫描容器失败：" + e.Message);
                    continue;
                }

                if (found == null)
                {
                    continue;
                }

                foreach (GameItem item in found)
                {
                    if (item == null || !seen.Add(item))
                    {
                        continue;
                    }

                    GameItemElement element = item as GameItemElement;
                    if (element != null && element.IsDestroyed())
                    {
                        continue;
                    }

                    items.Add(item);
                }
            }

            return items;
        }

        /// <summary>物品 id -> 玩家拥有的总单位数。</summary>
        public static Dictionary<string, int> CountUnits()
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (GameItem item in AllItems())
            {
                string id = item.identifier;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                int existing;
                counts.TryGetValue(id, out existing);
                counts[id] = existing + Math.Max(1, item.unitCount);
            }

            return counts;
        }

        /// <summary>
        /// 从店里扣掉指定 id 的若干个单位。先整体确认数量够，再逐个扣，避免扣到一半失败。
        /// 不足时返回 false，且不扣任何东西。
        /// </summary>
        public static bool TryConsume(string itemId, int units)
        {
            if (string.IsNullOrEmpty(itemId) || units <= 0)
            {
                return false;
            }

            List<GameItem> candidates = FindConsumable(itemId);
            int available = 0;
            foreach (GameItem item in candidates)
            {
                available += Math.Min(item.unitCount, SafeMaxRemove(item));
            }

            if (available < units)
            {
                return false;
            }

            int remaining = units;
            foreach (GameItem item in candidates)
            {
                if (remaining <= 0)
                {
                    break;
                }

                int take = Math.Min(remaining, Math.Min(item.unitCount, SafeMaxRemove(item)));
                if (take <= 0)
                {
                    continue;
                }

                int left = item.unitCount - take;
                if (left <= 0)
                {
                    item.Destroy();
                }
                else
                {
                    item.SetUnitCount(left);
                }

                remaining -= take;
            }

            return remaining <= 0;
        }

        /// <summary>列出某 id 可以安全移除的物品实例。</summary>
        public static List<GameItem> FindConsumable(string itemId)
        {
            var result = new List<GameItem>();

            foreach (GameItem item in AllItems())
            {
                if (!string.Equals(item.identifier, itemId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (SafeMaxRemove(item) <= 0)
                {
                    continue;
                }

                result.Add(item);
            }

            return result;
        }

        /// <summary>店里装满的指定尺寸液体瓶数。</summary>
        public static int CountFullBottles(string liquidId, int bottleSize)
        {
            if (string.IsNullOrEmpty(liquidId))
            {
                return 0;
            }

            int count = 0;
            foreach (GameItem item in AllItems())
            {
                if (IsFullBottle(item, liquidId, bottleSize))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsFullBottle(GameItem item, string liquidId, int bottleSize)
        {
            if (item == null || !string.Equals(item.identifier, liquidId, StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                if (SafeMaxRemove(item) <= 0 || (bottleSize >= 0 && ReagentBottle.SizeOf(item) != bottleSize))
                {
                    return false;
                }

                int capacity = ReagentBottle.CapacityFor(item);
                return capacity > 0 && ReagentBottle.Contents(item) >= capacity;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 校验并扣除指定数量、指定尺寸的满瓶。只从匹配的满瓶扣除，不拆用中大瓶或半瓶。
        /// 结算前整体校验，确认后才修改库存。
        /// </summary>
        public static bool TryConsumeBottled(string liquidId, int units, int fullBottles, int bottleSize)
        {
            if (string.IsNullOrEmpty(liquidId) || units <= 0 || fullBottles <= 0 || bottleSize < 0 || bottleSize > 2)
            {
                return false;
            }

            var full = new List<GameItem>();
            int available = 0;
            foreach (GameItem item in AllItems())
            {
                if (!IsFullBottle(item, liquidId, bottleSize))
                {
                    continue;
                }

                full.Add(item);
                available += Math.Min(item.unitCount, SafeMaxRemove(item));
            }

            int capacity = ReagentBottle.CapacityFor(liquidId, bottleSize);
            if (capacity <= 0 || units != capacity * fullBottles || full.Count < fullBottles || available < units)
            {
                return false;
            }

            // 每个匹配瓶都必须完整交付，不从瓶内剩余液量或其他尺寸补差额。
            int remaining = units;
            for (int i = 0; i < full.Count && remaining > 0; i++)
            {
                GameItem item = full[i];
                int take = Math.Min(remaining, Math.Min(item.unitCount, SafeMaxRemove(item)));
                if (take <= 0)
                {
                    continue;
                }

                int left = item.unitCount - take;
                if (left <= 0)
                {
                    item.Destroy();
                }
                else
                {
                    item.SetUnitCount(left);
                }

                remaining -= take;
            }

            return remaining == 0;
        }

        private static int ConsumeFrom(List<GameItem> items, int units)
        {
            int remaining = units;
            foreach (GameItem item in items)
            {
                if (remaining <= 0)
                {
                    break;
                }

                int take = Math.Min(remaining, Math.Min(item.unitCount, SafeMaxRemove(item)));
                if (take <= 0)
                {
                    continue;
                }

                int left = item.unitCount - take;
                if (left <= 0)
                {
                    item.Destroy();
                }
                else
                {
                    item.SetUnitCount(left);
                }

                remaining -= take;
            }

            return remaining;
        }

        private static int SafeMaxRemove(GameItem item)
        {
            try
            {
                if (!item.MayRemove())
                {
                    return 0;
                }

                return item.MaxNumRemove();
            }
            catch
            {
                return 0;
            }
        }

        internal static int PlaceTestSupply(GameItem item, bool keepWhole)
        {
            EmporiumEntry store = EmporiumEntry.Instance;
            if (store == null)
            {
                return 0;
            }

            int requested = item.unitCount;
            if (keepWhole && item.MaxNumRemove() < requested)
            {
                return 0;
            }

            int remaining = requested;
            GameInventory[] destinations =
            {
                store.invElement, store.backInvinvElement, store.frontInvinvElement,
                store.showcaseElement, store.afterhourInventory,
            };
            foreach (GameInventory inventory in destinations)
            {
                if (inventory == null)
                {
                    continue;
                }

                if (keepWhole)
                {
                    SlotMarker slot = inventory.TryFindOneValidInventorySlot(item);
                    if (slot != null && slot.targetItem == null && slot.numTransfer >= requested
                        && slot.TryAcceptOnce(requested) == requested)
                    {
                        return requested;
                    }
                }
                else
                {
                    remaining = GraphUtils.TryAcceptAll(inventory, item, remaining);
                    if (remaining == 0)
                    {
                        break;
                    }
                }
            }

            return requested - remaining;
        }

        /// <summary>
        /// 把一件物品放进玩家店里：先仓库，满了依次试后柜/前柜/展示柜/夜间背包。
        /// 用 parentInventory 判断是否真的放进去了（ModHelper 的几个 Move 方法不返回成败）。
        /// 全放不下时销毁该物品并返回 false，避免留下悬空/幽灵物品。
        /// </summary>
        public static bool TryPlace(GameItem item)
        {
            if (item == null)
            {
                return false;
            }

            try
            {
                ModHelper.MoveItemToInventory(item);
                if (item.parentInventory != null)
                {
                    return true;
                }

                ModHelper.MoveItemToBackCounter(item);
                if (item.parentInventory != null)
                {
                    return true;
                }

                ModHelper.MoveItemToFrontCounter(item);
                if (item.parentInventory != null)
                {
                    return true;
                }

                ModHelper.MoveItemToDisplayCase(item);
                if (item.parentInventory != null)
                {
                    return true;
                }

                ModHelper.MoveItemToAfterhours(item);
                if (item.parentInventory != null)
                {
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Chemistry] 放置物品出错：" + e.Message);
            }

            try
            {
                item.Destroy();
            }
            catch
            {
                // 收不回来也没别的办法
            }

            return false;
        }
    }
}
