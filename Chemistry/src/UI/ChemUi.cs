using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chemistry
{
    /// <summary>
    /// 界面模块共用的小工具：库存快照、贴图安全获取、TMP 文案样式、分类配色、滚动条定位。
    /// 所有游戏侧调用都包了 try/catch——主菜单、空数据、贴图缺失时界面最多降级，不能炸。
    /// </summary>
    internal static class ChemUi
    {
        /// <summary>模组 ID，ChemistryWindow.Initialize 时会更新成 manifest 里的真实 ID。</summary>
        public static string ModId = ChemistryCatalog.ModId;

        // 和游戏原生面板接近的文字色（暖白 / 灰 / 绿 / 红 / 金）
        public static readonly Color Main = new Color(0.93f, 0.92f, 0.86f);
        public static readonly Color Dim = new Color(0.68f, 0.68f, 0.66f);
        public static readonly Color Good = new Color(0.55f, 0.82f, 0.55f);
        public static readonly Color Bad = new Color(0.88f, 0.42f, 0.42f);
        public static readonly Color Accent = new Color(0.86f, 0.78f, 0.52f);

        /// <summary>RichText 用的颜色：缺料红、满足绿。</summary>
        public const string BadHex = "#E86C6C";

        public const string GoodHex = "#8CCF8C";

        /// <summary>窗口内容宽度（窗口 940 减去边框后的近似可用宽度）。</summary>
        public const float ListWidth = 820f;

        private static Dictionary<string, int> counts;
        private static bool countsReady;

        public static ModLog Log
        {
            get { return ChemistryMod.Log; }
        }

        /// <summary>玩家店里的 itemId -&gt; 单位数快照。</summary>
        public static Dictionary<string, int> Counts
        {
            get
            {
                if (!countsReady)
                {
                    RefreshCounts();
                }

                return counts;
            }
        }

        /// <summary>重新扫描一次店内库存。扫描失败时退化成空表，界面显示「拥有 0」。</summary>
        public static void RefreshCounts()
        {
            countsReady = true;
            try
            {
                counts = StoreInventory.CountUnits();
            }
            catch (Exception e)
            {
                counts = new Dictionary<string, int>(StringComparer.Ordinal);
                Log?.Warning("统计店内库存失败：" + e.Message);
            }

            if (counts == null)
            {
                counts = new Dictionary<string, int>(StringComparer.Ordinal);
            }
        }

        public static void InvalidateCounts()
        {
            countsReady = false;
        }

        public static int CountOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            Dictionary<string, int> map;
            try
            {
                map = Counts;
            }
            catch
            {
                return 0;
            }

            int value;
            return map != null && map.TryGetValue(itemId, out value) ? value : 0;
        }

        /// <summary>取模组自己的贴图；取不到返回 null（调用方只显示文字）。</summary>
        public static Sprite ModSprite(string spriteKey)
        {
            if (string.IsNullOrEmpty(spriteKey))
            {
                return null;
            }

            try
            {
                CustomUIManager manager = CustomUIManager.Instance;
                if (manager == null)
                {
                    return null;
                }

                return manager.GetModSprite(ModId, MaterialItemArt.SpriteKeyFor(spriteKey), true);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>统一设置一个 TMP 文案的字体大小/颜色/换行/对齐，并按需给 LayoutElement。</summary>
        public static void Style(CustomUIElement element, float size, float width, float height, bool wrap, Color color, bool bold, TextAlignmentOptions alignment)
        {
            if (element == null)
            {
                return;
            }

            if (width > 0f || height > 0f)
            {
                element.SetPreferredSize(width, height);
            }

            TextMeshProUGUI label = element.text;
            if (label == null)
            {
                return;
            }

            label.enableAutoSizing = false;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            // 缺料的红字靠 <color=...> 富文本，明确打开，免得预制体上被关掉
            label.richText = true;
            label.enableWordWrapping = wrap;
            label.overflowMode = wrap ? TextOverflowModes.Truncate : TextOverflowModes.Ellipsis;
            label.alignment = alignment;
            label.raycastTarget = false;
        }

        /// <summary>按钮里的文字样式（左对齐、单行省略号）。</summary>
        public static void StyleButtonText(CustomUIElement element, float size, bool bold, Color color)
        {
            if (element == null)
            {
                return;
            }

            TextMeshProUGUI label = element.text;
            if (label == null)
            {
                return;
            }

            label.enableAutoSizing = false;
            label.fontSize = size;
            label.color = color;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.alignment = TextAlignmentOptions.Left;
        }

        /// <summary>给行容器铺一层交替的半透明底色，看起来像原生列表。</summary>
        public static void RowBackground(RectTransform row, int index)
        {
            if (row == null)
            {
                return;
            }

            try
            {
                Image background = row.gameObject.GetComponent<Image>();
                if (background == null)
                {
                    background = row.gameObject.AddComponent<Image>();
                }

                background.color = new Color(0.55f, 0.72f, 1f, index % 2 == 0 ? 0.05f : 0.02f);
                // 接鼠标：在行上按住拖也能滚动列表（ScrollRect 会从祖先上找到拖拽处理）
                background.raycastTarget = true;
            }
            catch (Exception e)
            {
                Log?.Warning("给列表行加底色失败：" + e.Message);
            }
        }

        /// <summary>给行容器定宽定高，避免滚动列表里每行高矮不一。</summary>
        public static void RowLayout(RectTransform row, float width, float height)
        {
            if (row == null)
            {
                return;
            }

            try
            {
                LayoutElement layout = row.gameObject.GetComponent<LayoutElement>();
                if (layout == null)
                {
                    layout = row.gameObject.AddComponent<LayoutElement>();
                }

                if (width > 0f)
                {
                    layout.preferredWidth = width;
                }

                if (height > 0f)
                {
                    layout.minHeight = height;
                    layout.preferredHeight = height;
                }
            }
            catch (Exception e)
            {
                Log?.Warning("设置列表行尺寸失败：" + e.Message);
            }
        }

        public static void SetText(CustomUIWindow window, string elementId, string text)
        {
            if (window == null || !window.IsAlive)
            {
                return;
            }

            try
            {
                CustomUIElement element = window.Get(elementId);
                if (element != null)
                {
                    element.SetText(text ?? "");
                }
            }
            catch
            {
                // 控件已经不在了，不影响主要功能
            }
        }

        public static void SetInteractable(CustomUIWindow window, string elementId, bool value)
        {
            if (window == null || !window.IsAlive)
            {
                return;
            }

            try
            {
                CustomUIElement element = window.Get(elementId);
                if (element != null)
                {
                    element.SetInteractable(value);
                }
            }
            catch
            {
                // 忽略
            }
        }

        /// <summary>刷新列表后把滚动条拉回顶部。</summary>
        public static void ResetScroll(RectTransform host)
        {
            if (host == null)
            {
                return;
            }

            try
            {
                ScrollRect scroll = host.GetComponentInParent<ScrollRect>();
                if (scroll != null)
                {
                    scroll.verticalNormalizedPosition = 1f;
                }
            }
            catch
            {
                // 滚动条取不到就算了
            }
        }

        /// <summary>CATALOG.md 第 6 节的分类配色，元素/化合物/工具共用。</summary>
        public static Color CategoryColor(string category)
        {
            switch (category)
            {
                case "alkali_metal": return Hex(0xE8, 0xA3, 0x3D);
                case "alkaline_earth": return Hex(0xE8, 0xD2, 0x4A);
                case "transition_metal": return Hex(0xE8, 0x6C, 0x6C);
                case "post_transition": return Hex(0x7F, 0xB3, 0x7F);
                case "metalloid": return Hex(0x4F, 0xB3, 0xA6);
                case "nonmetal": return Hex(0x6F, 0xA8, 0xDC);
                case "halogen": return Hex(0x8E, 0x7C, 0xC3);
                case "noble_gas": return Hex(0xC2, 0x7B, 0xA0);
                case "lanthanide": return Hex(0x4A, 0xC6, 0xC6);
                case "actinide": return Hex(0xC4, 0x6A, 0x3C);
                case "acid": return Hex(0xE0, 0x70, 0x70);
                case "base": return Hex(0x6F, 0xA8, 0xDC);
                case "salt": return Hex(0xC9, 0xB2, 0x7A);
                case "oxide": return Hex(0xB0, 0xB0, 0xB8);
                case "organic": return Hex(0x8F, 0xBF, 0x7F);
                case "gas": return Hex(0xA9, 0xD3, 0xE0);
                case "glassware": return Hex(0x9C, 0xC7, 0xDE);
                case "heating": return Hex(0xE8, 0xA3, 0x3D);
                case "measurement": return Hex(0xC9, 0xB2, 0x7A);
                case "separation": return Hex(0x7F, 0xB3, 0x7F);
                case "safety": return Hex(0xC2, 0x7B, 0xA0);
                case "misc": return Hex(0xB0, 0xB0, 0xB8);
                default: return Hex(0x9A, 0xA0, 0xA6);
            }
        }

        /// <summary>分类色 + 指定透明度，用作周期表格子的底色。</summary>
        public static Color PlateColor(string category, float alpha)
        {
            Color color = CategoryColor(category);
            color.a = alpha;
            return color;
        }

        private static Color Hex(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        /// <summary>把数字格式化成与语言无关的文本（游戏语言切到英文也不该出现逗号小数点混用）。</summary>
        public static string MassText(double mass)
        {
            if (mass <= 0d)
            {
                return "?";
            }

            try
            {
                return mass.ToString("0.###", CultureInfo.InvariantCulture);
            }
            catch
            {
                return mass.ToString();
            }
        }

        /// <summary>按显示宽度截断（中文算 2 格），超长补省略号。</summary>
        public static string Fit(string text, int maxUnits)
        {
            if (string.IsNullOrEmpty(text) || maxUnits <= 0)
            {
                return text;
            }

            int units = 0;
            var builder = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                int width = c >= 0x1100 ? 2 : 1;
                if (units + width > maxUnits)
                {
                    builder.Append('…');
                    break;
                }

                builder.Append(c);
                units += width;
            }

            return builder.ToString();
        }

        /// <summary>化合物悬停提示：名字、化学式、分类、价值、描述。</summary>
        public static string CompoundTooltip(CompoundDef def)
        {
            if (def == null)
            {
                return "";
            }

            var builder = new StringBuilder();
            builder.Append(Loc.Pick(def.NameZh, def.NameEn));
            builder.Append(" (").Append(string.IsNullOrEmpty(def.Formula) ? "?" : def.Formula).Append(")\n");
            builder.Append(ChemistryCatalog.CompoundCategoryLabel(def.Category, Loc.IsChinese));
            builder.Append("  ·  ");
            builder.Append(Loc.Get("tooltip_value", "价值 {0}", "Value {0}", def.Value));
            string desc = Loc.Pick(def.DescZh, def.DescEn);
            if (!string.IsNullOrEmpty(desc))
            {
                builder.Append('\n').Append(desc);
            }

            return builder.ToString();
        }
    }
}
