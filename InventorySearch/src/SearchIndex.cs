using System;
using System.Collections.Generic;

namespace InventorySearch
{
    /// <summary>店里一个顶层容器（仓库、前柜、展示柜……）。</summary>
    internal sealed class ContainerRoot
    {
        public string Key;
        public string Label;
        public GameInventory Inventory;
        public PixelWindow Window;
    }

    /// <summary>一条检索结果。Item 就是游戏里真实的那件物品，不做拷贝。</summary>
    internal sealed class SearchEntry
    {
        public GameItem Item;
        public ContainerRoot Root;
        public string Path;
        public string Name;
        public string Identifier;
        public int Count;
        public long Value;
        public bool Contraband;
        public string Haystack;

        public string LocationText
        {
            get
            {
                if (string.IsNullOrEmpty(Path))
                {
                    return Root != null ? Root.Label : "";
                }

                return Root != null ? Root.Label + " › " + Path : Path;
            }
        }

        public string TitleText
        {
            get
            {
                string text = Name;
                if (Count > 1)
                {
                    text += " ×" + Count;
                }

                if (Contraband)
                {
                    text += "  <color=#E08A7A>●</color>";
                }

                return text;
            }
        }
    }

    /// <summary>
    /// 扫描店里所有物品。老办法是自己拼容器列表，但机器、背包、盒子里的东西
    /// 都在 GraphNodeStorage 的子树里，所以统一从顶层容器往下走一遍。
    /// </summary>
    internal static class SearchIndex
    {
        private const int MaxPathDepth = 24;

        public static List<ContainerRoot> BuildRoots()
        {
            var roots = new List<ContainerRoot>();
            EmporiumEntry store = EmporiumEntry.Instance;
            if (store == null)
            {
                return roots;
            }

            Add(roots, "area_warehouse", "仓库", "Warehouse", store.invElement, store.invWindow);
            Add(roots, "area_front", "前柜", "Front counter", store.frontInvinvElement, store.frontInvWindow);
            Add(roots, "area_back", "后柜", "Back counter", store.backInvinvElement, store.backInvWindow);
            Add(roots, "area_showcase", "展示柜", "Showcase", store.showcaseElement, store.showcaseWindow);
            Add(roots, "area_hidden", "暗格", "Hidden stash", store.hiddenElement, store.hiddenWindow);
            Add(roots, "area_docs", "文件柜", "Filing cabinet", store.docInvElement, store.docInvWindow);
            Add(roots, "area_afterhour", "夜间背包", "Night bag", store.afterhourInventory, store.afterhourWindow);
            Add(roots, "area_hireling", "雇员背包", "Hireling bag", store.hirelingInv, store.hirelingWindow);
            Add(roots, "area_bazaar", "夜市摊位", "Night bazaar", store.bazarLeftinvElement, store.bazarLeftInvWindow);
            Add(roots, "area_vending", "售货机", "Vending machine", store.vendingMachineElement, store.vendingMachineWindow);
            Add(roots, "area_fountain", "饮料机", "Drink fountain", store.vendingFountainElement, store.vendingFountainWindow);

            GameInventory trash = store.trashcanInvElement != null
                ? (GameInventory)store.trashcanInvElement
                : store.trashInvElement;
            Add(roots, "area_trash", "垃圾桶", "Trash", trash, store.trashInvWindow);

            return roots;
        }

        private static void Add(
            List<ContainerRoot> roots,
            string key,
            string zh,
            string en,
            GameInventory inventory,
            PixelWindow window)
        {
            if (inventory == null)
            {
                return;
            }

            roots.Add(new ContainerRoot
            {
                Key = key,
                Label = Loc.Get(key, zh, en),
                Inventory = inventory,
                Window = window
            });
        }

        public static List<SearchEntry> Scan(List<ContainerRoot> roots)
        {
            var entries = new List<SearchEntry>();
            if (roots == null)
            {
                return entries;
            }

            var seen = new HashSet<GameItem>();
            foreach (ContainerRoot root in roots)
            {
                if (root == null || root.Inventory == null)
                {
                    continue;
                }

                List<GameItem> items;
                try
                {
                    items = GraphUtils.FindAllChildrenType<GameItem>(root.Inventory, null, null);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogWarning("[InventorySearch] 扫描 " + root.Label + " 失败：" + e.Message);
                    continue;
                }

                foreach (GameItem item in items)
                {
                    if (item == null || !seen.Add(item))
                    {
                        continue;
                    }

                    SearchEntry entry = MakeEntry(item, root);
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }
            }

            return entries;
        }

        private static SearchEntry MakeEntry(GameItem item, ContainerRoot root)
        {
            try
            {
                string name = SafeName(item);
                string identifier = item.identifier ?? "";
                string path = BuildPath(item.parentInventory, root.Inventory);
                int count = Math.Max(1, item.unitCount);

                long value = 0;
                try
                {
                    value = item.GetCurrentValue(false, false, true, false, 0L);
                }
                catch
                {
                    value = 0;
                }

                bool contraband = false;
                try
                {
                    contraband = ContrabandHelper.IsContraband(item) || StolenHelper.IsStolenItem(item);
                }
                catch
                {
                    contraband = false;
                }

                var entry = new SearchEntry
                {
                    Item = item,
                    Root = root,
                    Path = path,
                    Name = name,
                    Identifier = identifier,
                    Count = count,
                    Value = value,
                    Contraband = contraband
                };

                entry.Haystack = (name + "\n" + identifier + "\n" + entry.LocationText + "\n" + path).ToLowerInvariant();
                return entry;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("[InventorySearch] 读取物品失败：" + e.Message);
                return null;
            }
        }

        private static string SafeName(GameItem item)
        {
            try
            {
                string name = item.GetDisplayName(true);
                if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
            catch
            {
                // 落到 identifier
            }

            if (!string.IsNullOrEmpty(item.identifier))
            {
                return item.identifier;
            }

            return string.IsNullOrEmpty(item.name) ? "?" : item.name;
        }

        /// <summary>从物品的直接容器一路爬到顶层容器，拼出「帆布包 › 弹药盒」这样的路径。</summary>
        private static string BuildPath(GameInventory parent, GameInventory root)
        {
            if (parent == null || root == null || parent == root)
            {
                return "";
            }

            var names = new List<string>();
            var seen = new HashSet<GraphNodeStorage>();
            GraphNodeStorage node = parent;

            for (int depth = 0; depth < MaxPathDepth && node != null && node != root; depth++)
            {
                if (!seen.Add(node))
                {
                    break;
                }

                GameItem container = node as GameItem;
                if (container != null)
                {
                    names.Add(SafeName(container));
                }

                List<GraphNodeStorage> parents = node.parents;
                node = parents != null && parents.Count > 0 ? parents[0] : null;
            }

            if (names.Count == 0)
            {
                return "";
            }

            names.Reverse();
            return string.Join(" › ", names.ToArray());
        }

        /// <summary>找物品下面最近一个带窗口的容器（背包、机器、盒子）。</summary>
        public static GameItem FindContainerWithWindow(GameInventory inventory)
        {
            if (inventory == null)
            {
                return null;
            }

            var seen = new HashSet<GraphNodeStorage>();
            GraphNodeStorage node = inventory;
            for (int depth = 0; depth < MaxPathDepth && node != null; depth++)
            {
                if (!seen.Add(node))
                {
                    break;
                }

                GameItem container = node as GameItem;
                if (container != null && (container.contentWindow != null || container.examineWindow != null))
                {
                    return container;
                }

                List<GraphNodeStorage> parents = node.parents;
                node = parents != null && parents.Count > 0 ? parents[0] : null;
            }

            return null;
        }
    }
}
