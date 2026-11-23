using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Chemistry
{
    /// <summary>
    /// 玩家自定义仓库：实验室窗口左侧竖条切换的多页储物格，放什么不做化学限制。
    /// 每页存一份 id→数量 计数加瓶剂明细（编码与 LabStorage 一致），走每局的 ModData 持久化；
    /// 开窗时按计数重建真实物品，拖放后/关窗时把格子里的物品捕获回计数。
    /// </summary>
    internal static class LabWarehouses
    {
        private const string NamesKey = "lab_warehouse_names";
        private const string DataKeyPrefix = "lab_warehouse_";
        private const int StaleKeyCleanup = 16;

        /// <summary>仓库数量上限：页签条要在一屏内排得下。</summary>
        internal const int MaxCount = 12;

        /// <summary>首个仓库的售价；第 N 个 = BasePrice × N，越买越贵。
        /// 量级参照开局现金 600、房租按天累计的经济节奏。</summary>
        internal const int BasePrice = 300;

        /// <summary>已拥有 ownedCount 个仓库时，开下一个的价格。</summary>
        internal static int PriceFor(int ownedCount)
        {
            return BasePrice * (ownedCount + 1);
        }

        internal sealed class Snapshot
        {
            internal string Name = "";
            internal Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.Ordinal);
            internal string BottleLayout = "";

            /// <summary>电池明细 "id@电量:数量|…"：电量是标签状态，计数存档丢不起。</summary>
            internal string BatteryLayout = "";
        }

        private static readonly List<Snapshot> snapshots = new List<Snapshot>();
        private static bool loaded;

        internal static bool Loaded => loaded;
        internal static int Count => loaded ? snapshots.Count : 0;

        internal static Snapshot Get(int index)
        {
            return loaded && index >= 0 && index < snapshots.Count ? snapshots[index] : null;
        }

        /// <summary>读档/新开局时重建列表。数据缺失或损坏一律按空仓库处理；
        /// 超过上限的历史仓库保留不删，只挡新建。</summary>
        internal static void EnsureLoaded()
        {
            loaded = true;
            snapshots.Clear();
            try
            {
                string names = ModHelper.GetModData(ChemistryCatalog.ModId, NamesKey, null);
                if (string.IsNullOrEmpty(names))
                {
                    return;
                }
                string[] parts = names.Split('|');
                for (int i = 0; i < parts.Length; i++)
                {
                    string name = (parts[i] ?? "").Trim();
                    if (name.Length == 0)
                    {
                        continue;
                    }
                    snapshots.Add(Decode(name, ModHelper.GetModData(ChemistryCatalog.ModId, DataKeyPrefix + i, null)));
                }
            }
            catch (Exception e)
            {
                snapshots.Clear();
                Debug.LogWarning("[Chemistry] 读取自定义仓库失败，按空处理：" + e.Message);
            }
        }

        internal static int Create()
        {
            if (!loaded)
            {
                EnsureLoaded();
            }
            if (snapshots.Count >= MaxCount)
            {
                return -1;
            }
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (Snapshot snapshot in snapshots)
            {
                used.Add(snapshot.Name);
            }
            int number = 1;
            while (used.Contains("仓库" + number))
            {
                number++;
            }
            snapshots.Add(new Snapshot { Name = "仓库" + number });
            SaveAll();
            return snapshots.Count - 1;
        }

        /// <summary>删除仓库；只许删空仓库，非空返回拒绝原因，成功返回 null。</summary>
        internal static string Delete(int index)
        {
            if (!loaded || index < 0 || index >= snapshots.Count)
            {
                return Loc.Pick("仓库不存在。", "Warehouse does not exist.");
            }
            if (snapshots[index].Counts.Count > 0 || snapshots[index].BatteryLayout.Length > 0)
            {
                return Loc.Pick("这个仓库不是空的，先搬空再删。", "This warehouse is not empty; move its items out first.");
            }
            snapshots.RemoveAt(index);
            SaveAll();
            return null;
        }

        /// <summary>重命名仓库；非法名字返回拒绝原因，成功返回 null。
        /// 名字本身不限长，页签显示多长由 LabInventoryGrid 截断。</summary>
        internal static string Rename(int index, string name)
        {
            Snapshot snapshot = Get(index);
            if (snapshot == null)
            {
                return Loc.Pick("仓库不存在。", "Warehouse does not exist.");
            }
            name = (name ?? "").Trim().Replace("|", " ");
            if (name.Length == 0)
            {
                return Loc.Pick("名字不能是空的。", "The name cannot be empty.");
            }
            for (int i = 0; i < snapshots.Count; i++)
            {
                if (i != index && string.Equals(snapshots[i].Name, name, StringComparison.Ordinal))
                {
                    return Loc.Pick("已经有同名的仓库了。", "A warehouse with that name already exists.");
                }
            }
            snapshot.Name = name;
            SaveAll();
            return null;
        }

        /// <summary>把一页仓库的实时内容写进存档；内容没变就不写，避免每帧落盘。</summary>
        internal static void Store(int index, Dictionary<string, int> counts, string bottleLayout, string batteryLayout)
        {
            Snapshot snapshot = Get(index);
            if (snapshot == null || counts == null)
            {
                return;
            }
            if (LabInventoryGrid.EqualCounts(counts, snapshot.Counts)
                && string.Equals(bottleLayout ?? "", snapshot.BottleLayout, StringComparison.Ordinal)
                && string.Equals(batteryLayout ?? "", snapshot.BatteryLayout, StringComparison.Ordinal))
            {
                return;
            }
            snapshot.Counts = counts;
            snapshot.BottleLayout = bottleLayout ?? "";
            snapshot.BatteryLayout = batteryLayout ?? "";
            Save(index);
        }

        internal static void SaveAll()
        {
            if (!loaded)
            {
                return;
            }
            try
            {
                var names = new StringBuilder();
                for (int i = 0; i < snapshots.Count; i++)
                {
                    if (names.Length > 0)
                    {
                        names.Append('|');
                    }
                    names.Append(snapshots[i].Name.Replace("|", " ").Trim());
                    Save(i);
                }
                ModHelper.SetModData(ChemistryCatalog.ModId, NamesKey, names.ToString());
                // 删剩的旧键清空，免得下一局读回幽灵仓库。
                for (int i = snapshots.Count; i < snapshots.Count + StaleKeyCleanup; i++)
                {
                    ModHelper.SetModData(ChemistryCatalog.ModId, DataKeyPrefix + i, "");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Chemistry] 保存自定义仓库列表失败：" + e.Message);
            }
        }

        private static void Save(int index)
        {
            Snapshot snapshot = snapshots[index];
            var builder = new StringBuilder();
            foreach (KeyValuePair<string, int> entry in snapshot.Counts)
            {
                if (entry.Value <= 0)
                {
                    continue;
                }
                if (builder.Length > 0)
                {
                    builder.Append('|');
                }
                builder.Append(entry.Key).Append(':').Append(entry.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (snapshot.BottleLayout.Length != 0 || snapshot.BatteryLayout.Length != 0)
            {
                if (builder.Length > 0)
                {
                    builder.Append('|');
                }
                builder.Append('~').Append(snapshot.BottleLayout);
            }
            if (snapshot.BatteryLayout.Length != 0)
            {
                builder.Append('~').Append(snapshot.BatteryLayout);
            }
            ModHelper.SetModData(ChemistryCatalog.ModId, DataKeyPrefix + index, builder.ToString());
        }

        private static Snapshot Decode(string name, string raw)
        {
            var snapshot = new Snapshot { Name = name };
            if (string.IsNullOrEmpty(raw))
            {
                return snapshot;
            }
            try
            {
                // 三段式：计数 ~ 瓶剂明细 ~ 电池明细（电量随存档保留）。
                string[] sections = raw.Split('~');
                string countsRaw = sections[0];
                if (sections.Length > 1)
                {
                    snapshot.BottleLayout = sections[1];
                }
                if (sections.Length > 2)
                {
                    snapshot.BatteryLayout = sections[2];
                }
                else if (snapshot.BottleLayout.Length > 0 && !snapshot.BottleLayout.Contains("="))
                {
                    // 旧版省略空瓶剂段时，电池明细被写到了第二段。
                    snapshot.BatteryLayout = snapshot.BottleLayout;
                    snapshot.BottleLayout = "";
                }
                foreach (string entry in countsRaw.Split('|'))
                {
                    int colon = (entry ?? "").LastIndexOf(':');
                    if (colon <= 0 || colon >= entry.Length - 1)
                    {
                        continue;
                    }
                    if (int.TryParse(entry.Substring(colon + 1).Trim(), NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out int count) && count > 0)
                    {
                        snapshot.Counts[entry.Substring(0, colon).Trim()] = count;
                    }
                }
            }
            catch (Exception e)
            {
                snapshot.Counts.Clear();
                snapshot.BottleLayout = "";
                snapshot.BatteryLayout = "";
                Debug.LogWarning("[Chemistry] 解析自定义仓库失败：" + e.Message);
            }
            return snapshot;
        }
    }
}
