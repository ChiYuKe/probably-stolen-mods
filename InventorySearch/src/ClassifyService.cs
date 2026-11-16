using System;
using System.Collections.Generic;

namespace InventorySearch
{
    /// <summary>归类结果的类别；Skip 表示机器、容器、固定物品这类不该动的东西。</summary>
    internal enum ClassifyGroup
    {
        Skip = 0,
        Contraband,
        Weapon,
        Luxury,
        Consumable,
        Household,
        Supply,
        Document,
        Other
    }

    internal sealed class ClassifyCandidate
    {
        public GameItem Item;
        public ClassifyGroup Group;
        public GameGridInventory Source;
        public GameGridInventory Target;
    }

    internal sealed class ClassifyResult
    {
        public int Moved;
        public int Skipped;
        public int Failed;
        public readonly Dictionary<ClassifyGroup, int> MovedByGroup = new Dictionary<ClassifyGroup, int>();
    }

    /// <summary>
    /// 跨容器归类的核心逻辑：把本店所有、未固定、能拿起来的顶层物品，
    /// 按物品类型挪到对应的柜子。放不下就留在原地，不丢东西。
    /// </summary>
    internal static class ClassifyService
    {
        private static readonly string[] WeaponTypes =
        {
            "WEAPON", "FIREARM", "MELEE_WEAPON", "AMMUNITION", "MUNITION", "ARMOR", "GUN_MOD"
        };

        private static readonly string[] LuxuryTypes =
        {
            "LUXURY_ITEM"
        };

        private static readonly string[] ConsumableTypes =
        {
            "FOOD", "PROCESSED_FOOD", "TREAT", "BREVAGE", "WATER", "WATER_RATION",
            "MEDICAL", "NARCOTIC", "SUBSTANCE", "ALCOHOL", "POISON"
        };

        private static readonly string[] HouseholdTypes =
        {
            "HOUSEHOLD_GOOD", "LOTTERY"
        };

        private static readonly string[] SupplyTypes =
        {
            "MATERIAL", "BASIC_MATERIAL", "JUNK", "TOOL", "FILTER", "CHEMICAL_SUPPLIES",
            "SEED", "FARMING_SUPPLIES", "ENERGY_CELL", "WATER_PURIFICATION_SUPPLY", "ACCESS_CARD"
        };

        private static readonly List<KeyValuePair<GameItem, GameInventory>> lastUndo =
            new List<KeyValuePair<GameItem, GameInventory>>();

        public static bool HasUndo
        {
            get { return lastUndo.Count > 0; }
        }

        public static void ClearUndo()
        {
            lastUndo.Clear();
        }

        public static List<ClassifyCandidate> Collect(bool allSources, out int skipped)
        {
            skipped = 0;
            var result = new List<ClassifyCandidate>();
            EmporiumEntry store = EmporiumEntry.Instance;
            if (store == null)
            {
                return result;
            }

            List<GameGridInventory> sources = Sources(store, allSources);
            var seen = new HashSet<GameItem>();

            foreach (GameGridInventory source in sources)
            {
                if (source == null)
                {
                    continue;
                }

                List<GameItem> children;
                try
                {
                    children = new List<GameItem>(source.childItems);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning("[InventorySearch] 归类时读取容器失败：" + e.Message);
                    continue;
                }

                foreach (GameItem item in children)
                {
                    if (item == null || !seen.Add(item))
                    {
                        continue;
                    }

                    if (item.parentInventory != source || !Eligible(item))
                    {
                        skipped++;
                        continue;
                    }

                    ClassifyGroup group;
                    try
                    {
                        group = GroupOf(item);
                    }
                    catch
                    {
                        group = ClassifyGroup.Skip;
                    }

                    if (group == ClassifyGroup.Skip)
                    {
                        skipped++;
                        continue;
                    }

                    GameGridInventory target = TargetOf(group, store);
                    if (target == null || target == source)
                    {
                        continue;
                    }

                    result.Add(new ClassifyCandidate
                    {
                        Item = item,
                        Group = group,
                        Source = source,
                        Target = target
                    });
                }
            }

            return result;
        }

        public static ClassifyResult Execute(List<ClassifyCandidate> candidates)
        {
            var result = new ClassifyResult();
            if (candidates == null || EmporiumEntry.Instance == null)
            {
                return result;
            }

            EmporiumEntry store = EmporiumEntry.Instance;
            var undo = new List<KeyValuePair<GameItem, GameInventory>>();

            // 这一轮开始，上一轮的撤销记录就作废
            lastUndo.Clear();

            // 小柜子先腾空：暗格、展示柜里不合规的东西先挪出来，再往里放新的，
            // 不然容易被自己的占位卡死。
            Pass(candidates, store, result, undo, true);
            Pass(candidates, store, result, undo, false);

            if (undo.Count > 0)
            {
                lastUndo.AddRange(undo);
            }

            return result;
        }

        private static void Pass(
            List<ClassifyCandidate> candidates,
            EmporiumEntry store,
            ClassifyResult result,
            List<KeyValuePair<GameItem, GameInventory>> undo,
            bool evacuateSmall)
        {
            GameGridInventory hidden = store.hiddenElement;
            GameGridInventory showcase = store.showcaseElement;

            foreach (ClassifyCandidate candidate in candidates)
            {
                if (candidate == null || candidate.Item == null || candidate.Source == null || candidate.Target == null)
                {
                    result.Skipped++;
                    continue;
                }

                bool small = candidate.Source == hidden || candidate.Source == showcase;
                if (small != evacuateSmall)
                {
                    continue;
                }

                GameItem item = candidate.Item;
                try
                {
                    if (item.parentInventory != candidate.Source)
                    {
                        result.Skipped++;
                        continue;
                    }

                    if (!TryMove(item, candidate.Target, candidate.Source))
                    {
                        result.Failed++;
                        continue;
                    }
                }
                catch
                {
                    result.Failed++;
                    continue;
                }

                result.Moved++;
                int count;
                result.MovedByGroup.TryGetValue(candidate.Group, out count);
                result.MovedByGroup[candidate.Group] = count + 1;
                undo.Add(new KeyValuePair<GameItem, GameInventory>(item, candidate.Source));
            }
        }

        public static void Undo(out int restored, out int failed)
        {
            restored = 0;
            failed = 0;

            for (int i = lastUndo.Count - 1; i >= 0; i--)
            {
                GameItem item = lastUndo[i].Key;
                GameInventory source = lastUndo[i].Value;
                if (item == null || source == null)
                {
                    continue;
                }

                try
                {
                    if (item.parentInventory == null)
                    {
                        failed++;
                        continue;
                    }

                    if (item.parentInventory == source)
                    {
                        continue;
                    }

                    if (TryMove(item, source, item.parentInventory))
                    {
                        restored++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch
                {
                    failed++;
                }
            }

            lastUndo.Clear();
        }

        private static bool TryMove(GameItem item, GameInventory target, GameInventory source)
        {
            if (item == null || target == null)
            {
                return false;
            }

            if (item.parentInventory == target)
            {
                return true;
            }

            try
            {
                if (!GeneralHelper.MayPlayerInsertInto(target))
                {
                    return false;
                }

                SlotMarker slot = target.TryFindOneValidInventorySlot(item);
                if (slot == null || slot.inventory != target || slot.targetItem != null)
                {
                    return false;
                }

                if (slot.numTransfer < item.unitCount)
                {
                    return false;
                }

                int amount = item.unitCount;
                int moved = slot.TryAcceptOnce(amount);
                if (moved == amount && item.parentInventory == target)
                {
                    return true;
                }

                // 理论上不会走到这里；万一物品被挤出容器，尽量塞回原来的地方
                if (item.parentInventory == null && source != null)
                {
                    GraphUtils.TryAcceptAll(source, item);
                }

                return false;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[InventorySearch] 移动物品失败：" + e.Message);
                return false;
            }
        }

        private static List<GameGridInventory> Sources(EmporiumEntry store, bool allSources)
        {
            var sources = new List<GameGridInventory> { store.invElement };
            if (!allSources)
            {
                return sources;
            }

            Add(sources, store.frontInvinvElement);
            Add(sources, store.backInvinvElement);
            Add(sources, store.showcaseElement);
            Add(sources, store.hiddenElement);
            return sources;
        }

        private static void Add(List<GameGridInventory> list, GameGridInventory inventory)
        {
            if (inventory != null && !list.Contains(inventory))
            {
                list.Add(inventory);
            }
        }

        private static bool Eligible(GameItem item)
        {
            try
            {
                if (item == null || item.unitCount <= 0)
                {
                    return false;
                }

                GameItemElement element = item as GameItemElement;
                if (element != null && element.IsDestroyed())
                {
                    return false;
                }

                if (item.parentInventory == null || !GeneralHelper.IsItemOwned(item))
                {
                    return false;
                }

                if (ItemPinHelper.IsPinned(item) || DebrisHelper.IsDebris(item))
                {
                    return false;
                }

                if (item.IsTag("IMPORTANT_TAG") || item.IsTag("SHOWCASE_SPECIAL_ITEM_TAG"))
                {
                    return false;
                }

                if (!item.MayRemove() || item.MaxNumRemove() < item.unitCount)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static ClassifyGroup GroupOf(GameItem item)
        {
            if (item.IsGameItemType("MACHINE") || item.IsGameItemType("MODULE") || item.IsGameItemType("NODE"))
            {
                return ClassifyGroup.Skip;
            }

            if (item.IsTag("CONTAINER_TAG") || item.IsTag("BACKPACK_TAG") ||
                item.IsGameItemType("STORAGE") || item.IsGameItemType("SUPPLY_CRATE"))
            {
                return ClassifyGroup.Skip;
            }

            if (ContrabandHelper.IsContraband(item) || StolenHelper.IsStolenItem(item) || item.IsGameItemType("CONTRABAND"))
            {
                return ClassifyGroup.Contraband;
            }

            if (item.IsGameItemType("DOCUMENT") || item.IsTag("DOCUMENT"))
            {
                return ClassifyGroup.Document;
            }

            if (HasAnyType(item, WeaponTypes) || item.IsTag("WEAPON") || item.IsTag("FIREARM"))
            {
                return ClassifyGroup.Weapon;
            }

            if (HasAnyType(item, LuxuryTypes))
            {
                return ClassifyGroup.Luxury;
            }

            if (HasAnyType(item, ConsumableTypes) || item.IsTag("FOOD") || item.IsTag("BREVAGE"))
            {
                return ClassifyGroup.Consumable;
            }

            if (HasAnyType(item, HouseholdTypes) || item.IsTag("HOUSEHOLD_GOOD"))
            {
                return ClassifyGroup.Household;
            }

            if (HasAnyType(item, SupplyTypes))
            {
                return ClassifyGroup.Supply;
            }

            return ClassifyGroup.Other;
        }

        private static bool HasAnyType(GameItem item, string[] types)
        {
            foreach (string type in types)
            {
                if (item.IsGameItemType(type))
                {
                    return true;
                }
            }

            return false;
        }

        private static GameGridInventory TargetOf(ClassifyGroup group, EmporiumEntry store)
        {
            switch (group)
            {
                case ClassifyGroup.Contraband:
                    return store.hiddenElement;
                case ClassifyGroup.Weapon:
                case ClassifyGroup.Luxury:
                    return store.showcaseElement;
                case ClassifyGroup.Consumable:
                case ClassifyGroup.Household:
                    return store.frontInvinvElement;
                case ClassifyGroup.Supply:
                    return store.backInvinvElement;
                case ClassifyGroup.Document:
                case ClassifyGroup.Other:
                    return store.invElement;
                default:
                    return null;
            }
        }
    }
}
