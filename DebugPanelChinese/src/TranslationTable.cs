using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DebugPanelChinese
{
    /// <summary>
    /// F9 调试面板里的硬编码英文，按原样对照成中文。
    /// 只匹配完整字符串（分类标题额外剥一层 &lt;b&gt;），不做全局替换，
    /// 免得把物品 ID 之类的文本也改掉。
    /// </summary>
    internal static class TranslationTable
    {
        private const string CellTipSuffix =
            "\nClick: spawn 1, Shift+click: spawn 10\nRight-click: toggle favorite\nF2: copy id";

        private const string CellTipChineseSuffix =
            "\n点击：生成 1 个，Shift+点击：生成 10 个\n右键：切换收藏\nF2：复制标识";

        private static readonly Regex MatchCount = new Regex(
            @"^(\d+) match(es)?$", RegexOptions.CultureInvariant);

        private static readonly Regex NoIdentifierMatch = new Regex(
            @"^No identifier matches '(.*)'\.$", RegexOptions.CultureInvariant | RegexOptions.Singleline);

        private static readonly Dictionary<string, string> Exact =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // 窗口标题
                { "Debug Panel", "调试面板" },
                { "Modding Debug Panel", "模组调试面板" },
                { "Marketing Debug Panel", "营销调试面板" },
                { "Story Debug Panel", "剧情调试面板" },
                { "Item Spawner", "物品生成器" },

                // 分类 / 列标题
                { "Store", "商店" },
                { "Command", "命令" },
                { "Spawn", "生成" },
                { "Special", "特殊" },
                { "Modding", "模组" },
                { "Scene", "场景" },
                { "Kits", "套件" },
                { "Items", "物品" },
                { "Customers", "顾客" },
                { "Butcher", "屠夫" },
                { "Lou", "露" },
                { "Eleanor", "埃莉诺" },
                { "Serpico", "塞尔皮科" },
                { "Story Days", "剧情日程" },

                // 搜索栏
                { "search buttons...", "搜索按钮…" },
                { "(no functions yet)", "（暂无功能）" },

                // 商店
                { "Set Credit to 0", "信用点归零" },
                { "Add 500 Credit", "增加 500 信用点" },
                { "Add 10000 Credit", "增加 10000 信用点" },
                { "Set All Rep to Max", "所有声望拉到最高" },
                { "Set All Rep to Min", "所有声望降到最低" },
                { "Set Security Rep to Min", "治安声望降到最低" },
                { "Buy Store + Deed (Keep Playing)", "买下店铺和地契（继续游戏）" },
                { "Ending: Instant Buyout (Win)", "结局：立即买断（胜利）" },
                { "Ending: Arrested (Loss)", "结局：被捕（失败）" },

                // 命令
                { "Toggle Dev Mode", "切换开发者模式" },
                { "Toggle Verbose Log", "切换详细日志" },
                { "Skip Night (Machines Only)", "跳过夜晚（仅机器）" },
                { "Unlock Shotgun", "解锁霰弹枪" },
                { "Force Clear Inv", "强制清空背包" },
                { "Force Clear Trashcan", "强制清空垃圾桶" },
                { "Open Wild UI", "打开 Wild 界面" },
                { "Queue Basic Gun Order (LL)", "排队基础枪械订单（LL）" },
                { "Queue Mid Gun Order (Merc)", "排队中级枪械订单（佣兵）" },
                { "Queue High Gun Order (UL)", "排队高级枪械订单（UL）" },

                // 生成
                { "Open Debug Spawner", "打开调试生成器" },
                { "Spawn Aquaculture Kit", "生成水产套件" },
                { "Spawn Hydroponic Kit", "生成水培套件" },
                { "Spawn Plant DNA Kit", "生成植物 DNA 套件" },
                { "Spawn Gun Kit", "生成枪械套件" },
                { "Spawn Rat Ranching Kit", "生成老鼠养殖套件" },

                // 特殊
                { "Open Sprite Viewer", "打开精灵查看器" },
                { "Open Customer Viewer", "打开顾客查看器" },
                { "Afterhour Events", "打烊后事件" },
                { "Modding Panel", "模组面板" },
                { "Legacy Panel", "旧版面板" },
                { "Marketing Panel", "营销面板" },
                { "Story Panel", "剧情面板" },

                // 模组面板
                { "Back to Debug Panel", "返回调试面板" },
                { "Paper Editor", "纸张编辑器" },
                { "Reload Mod Books", "重新加载模组书页" },
                { "Close Paper Books", "关闭模组书页" },
                { "Open Mods Folder", "打开模组文件夹" },
                { "Sprite Viewer", "精灵查看器" },
                { "Paper Sprite Picker", "纸张精灵选择器" },

                // 按钮上的 ? 提示
                { "For clean screenshots and footage: hides the tooltips and hover outlines that would otherwise pop up over the shot.",
                  "用于干净的截图和录像：隐藏会出现在画面上的提示框和悬停描边。" },
                { "For still shots: makes the customers stand still instead of swaying, so a screenshot catches them in a steady pose.",
                  "用于静态截图：让顾客保持不动而不是摇晃，这样截图时能拍到稳定的姿势。" },
                { "For showing off the cast: the customer at the counter keeps changing into other characters from the game, so one clip shows many faces.",
                  "用于展示角色阵容：柜台前的顾客会不断变成游戏中的其他角色，一段视频就能看到许多面孔。" },
                { "Shows the game's Debug.Log output (console, Player.log and GameLog files). Off every session by default; warnings and errors always show, and so do mods' own log lines.",
                  "显示游戏的 Debug.Log 输出（控制台、Player.log 和 GameLog 文件）。每次启动默认关闭；警告和错误始终显示，模组自己的日志也会显示。" },
                { "Buys the store with cash at today's price (credits are topped up first if short), puts the deed on the counter and queues what follows the deed (Joe Wild's visit, in the editor), without the ending screen. Ends a running mortgage. Skips the buyout achievements.",
                  "按今天的价格用现金买下店铺（现金不足时会先补足），把地契放到柜台上，并排队处理地契之后的事件（在编辑器里是乔·怀尔德的来访），不显示结局界面。会终止正在进行的抵押贷款。会跳过买断相关成就。" },
                { "Buys the store with cash at today's price (credits are topped up first if short) and ends the run with the buyout ending, to test the game over screen and the run summary. Skips the buyout achievements.",
                  "按今天的价格用现金买下店铺（现金不足时会先补足），并以买断结局结束本局，用于测试游戏结束界面和本局总结。会跳过买断相关成就。" },
                { "Ends the run with the arrest ending, to test the game over screen and the run summary.",
                  "以被捕结局结束本局，用于测试游戏结束界面和本局总结。" },

                // 物品生成器
                { "filter by identifier...", "按标识筛选…" },
                { "Stack x10", "堆叠 ×10" },
                { "All", "全部" },
                { "Recent", "最近" },
                { "Favorites", "收藏" },
                { "No item directories are registered in this scene.", "此场景没有注册任何物品目录。" },
                { "Click: spawn into the store. Shift+click: x10. Right-click: toggle favorite. Filter updates as you type (2+ characters).",
                  "点击：生成到商店。Shift+点击：×10。右键：切换收藏。输入时自动筛选（至少 2 个字符）。" },
                { "Spawn 10 units per click. Holding Shift while clicking does the same.",
                  "每次点击生成 10 个。按住 Shift 点击效果相同。" },
                { "Nothing spawned yet. Spawned items show up here, newest first.",
                  "还没有生成过物品。生成过的物品会显示在这里，最新的在前。" },
                { "No favorites yet. Right-click an item to add it.",
                  "还没有收藏。右键点击物品即可收藏。" },
                { "This group is empty.", "这个分组是空的。" }
            };

        public static string ForText(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            if (Exact.TryGetValue(raw, out string translated))
            {
                return translated;
            }

            // 分类标题是 "<b>Store</b>"，去掉标签翻译完再包回去
            if (raw.Length > 7 &&
                raw.StartsWith("<b>", StringComparison.Ordinal) &&
                raw.EndsWith("</b>", StringComparison.Ordinal))
            {
                string inner = raw.Substring(3, raw.Length - 7);
                if (Exact.TryGetValue(inner, out string innerTranslated))
                {
                    return "<b>" + innerTranslated + "</b>";
                }
            }

            Match match = MatchCount.Match(raw);
            if (match.Success)
            {
                return match.Groups[1].Value + " 个匹配";
            }

            match = NoIdentifierMatch.Match(raw);
            if (match.Success)
            {
                return "没有匹配“" + match.Groups[1].Value + "”的标识。";
            }

            return null;
        }

        public static string ForTooltip(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            if (Exact.TryGetValue(raw, out string translated))
            {
                return translated;
            }

            // 物品格子的提示：第一行是物品 ID，后面几行是固定说明
            if (raw.EndsWith(CellTipSuffix, StringComparison.Ordinal))
            {
                return raw.Substring(0, raw.Length - CellTipSuffix.Length) + CellTipChineseSuffix;
            }

            // 说明文字 + 空行 + "Added by mod 'xxx'"
            const string addedMarker = "\n\nAdded by mod '";
            int index = raw.IndexOf(addedMarker, StringComparison.Ordinal);
            if (index >= 0)
            {
                string head = raw.Substring(0, index);
                string tail = raw.Substring(index + addedMarker.Length);
                if (tail.EndsWith("'", StringComparison.Ordinal))
                {
                    tail = tail.Substring(0, tail.Length - 1);
                }
                string headTranslated = ForText(head) ?? head;
                return headTranslated + "\n\n由模组“" + tail + "”添加";
            }

            const string addedPrefix = "Added by mod '";
            if (raw.StartsWith(addedPrefix, StringComparison.Ordinal) &&
                raw.EndsWith("'", StringComparison.Ordinal))
            {
                return "由模组“" + raw.Substring(addedPrefix.Length, raw.Length - addedPrefix.Length - 1) + "”添加";
            }

            return ForText(raw);
        }
    }
}
