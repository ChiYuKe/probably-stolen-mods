using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chemistry
{
    /// <summary>
    /// 周期表标签页：18 列 × 7 行的主表 + 镧系/锕系两行（group=0 的元素按 CATALOG 约定单列），
    /// 每格显示模组贴图和元素符号，悬停/点击在下方详情栏显示序数、中英名、分类、相对原子质量、价值、拥有数量。
    /// 全部位置来自 ElementDef.Group / Period，没有硬编码的元素清单。
    /// </summary>
    internal static class PeriodicTablePanel
    {
        private const float CellWidth = 44f;
        private const float CellHeight = 48f;
        private const float CellSpacing = 2f;
        private const float DetailWidth = 820f;
        private const float DetailHeight = 64f;

        private static CustomUIWindow window;
        private static ElementDef selected;

        /// <summary>在窗口构建阶段调用：铺好静态的表格和详情栏（只建一次）。</summary>
        public static void BuildPage(CustomUIBuilder builder, CustomUIWindow host)
        {
            window = host;
            selected = null;

            if (builder == null)
            {
                return;
            }

            ElementDef[] elements = ChemistryCatalog.Elements;
            if (elements == null || elements.Length == 0)
            {
                builder.AddLabel(Loc.Get(
                    "pt_no_data",
                    "元素数据还没加载（src/Data/ElementTable.cs 没生成？）。",
                    "Element data is not loaded (is src/Data/ElementTable.cs generated?)."));
                ChemUi.Style(builder.Last, 13f, 820f, 26f, true, ChemUi.Bad, false, TextAlignmentOptions.TopLeft);
                return;
            }

            ChemUi.RefreshCounts();

            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            builder.AddLabel(Loc.Get(
                "pt_hint",
                "移到或点一下元素格子看详情；镧系、锕系在下面单独两行。",
                "Hover or click a cell for details; lanthanides and actinides are in their own rows below."));
            ChemUi.Style(builder.Last, 12f, 720f, 20f, false, ChemUi.Dim, false, TextAlignmentOptions.Left);
            builder.AddFlexibleSpace();
            builder.AddButton(Loc.Get("pt_refresh", "刷新", "Refresh"), Refresh, "pt_refresh");
            builder.Last.SetFixedWidth(84f);
            builder.End();

            // 主表：7 个周期 × 18 个族，空格子也画上暗格，表格才对齐
            builder.BeginGrid(18, CellWidth, CellHeight, CellSpacing);
            for (int period = 1; period <= 7; period++)
            {
                for (int group = 1; group <= 18; group++)
                {
                    AddSlot(builder, FindAt(elements, group, period));
                }
            }

            builder.End();

            builder.AddSpace(6f);

            // group=0 的元素按分类拆成镧系/锕系两行；分类缺失时退回按周期判断，
            // 剩下实在分不出来的放第三行，保证数据表里每个元素都能显示。
            var lanthanides = new List<ElementDef>();
            var actinides = new List<ElementDef>();
            var leftovers = new List<ElementDef>();
            for (int i = 0; i < elements.Length; i++)
            {
                ElementDef def = elements[i];
                if (def == null)
                {
                    continue;
                }

                // 主表只收 group 1..18、period 1..7 的元素
                if (def.Group >= 1 && def.Group <= 18 && def.Period >= 1 && def.Period <= 7)
                {
                    continue;
                }

                if (def.Group != 0)
                {
                    leftovers.Add(def);
                }
                else if (MatchesCategory(def, "lanthanide", 6))
                {
                    lanthanides.Add(def);
                }
                else if (MatchesCategory(def, "actinide", 7))
                {
                    actinides.Add(def);
                }
                else
                {
                    leftovers.Add(def);
                }
            }

            SortByNumber(lanthanides);
            SortByNumber(actinides);
            SortByNumber(leftovers);

            AddFBlockRow(builder, lanthanides, Loc.Get("pt_lanthanide", "镧系", "Lanthanides"));
            AddFBlockRow(builder, actinides, Loc.Get("pt_actinide", "锕系", "Actinides"));
            if (leftovers.Count > 0)
            {
                AddFBlockRow(builder, leftovers, Loc.Get("pt_f_other", "其他", "Other"));
            }

            builder.AddSpace(4f);
            builder.AddLabel(" ", "pt_detail");
            ChemUi.Style(builder.Last, 13f, DetailWidth, DetailHeight, true, ChemUi.Main, false, TextAlignmentOptions.TopLeft);

            ShowHint();
        }

        /// <summary>窗口重新打开/点刷新时调用：重扫库存并把当前选中元素的拥有数量更新掉。</summary>
        public static void Refresh()
        {
            ChemUi.RefreshCounts();
            if (selected != null)
            {
                ShowDetail(selected);
            }
            else
            {
                ShowHint();
            }
        }

        /// <summary>把某个元素的信息写到详情栏。def 为空时清成提示文字。</summary>
        public static void ShowDetail(ElementDef def)
        {
            if (def == null)
            {
                return;
            }

            selected = def;

            string nameZh = string.IsNullOrEmpty(def.NameZh) ? "?" : def.NameZh;
            string nameEn = string.IsNullOrEmpty(def.NameEn) ? "?" : def.NameEn;
            string category = ChemistryCatalog.ElementCategoryLabel(def.Category, Loc.IsChinese);
            string symbol = string.IsNullOrEmpty(def.Symbol) ? "?" : def.Symbol;

            string text = "<b>" + symbol + "　" + nameZh + " / " + nameEn + "</b>\n"
                + Loc.Get(
                    "pt_detail_line",
                    "序数 {0}　·　分类：{1}　·　相对原子质量 {2}　·　价值 {3}　·　拥有 {4}",
                    "Number {0}　·　Category: {1}　·　Mass {2}　·　Value {3}　·　Owned {4}",
                    def.Number,
                    category,
                    ChemUi.MassText(def.Mass),
                    def.Value,
                    ChemUi.CountOf(def.ItemId));

            SetDetail(text);
        }

        private static void ShowHint()
        {
            SetDetail(Loc.Get(
                "pt_detail_hint",
                "把鼠标移到元素上或点一下格子，这里会显示序数、中英名、分类、相对原子质量、价值和店里现有的数量。",
                "Hover or click a cell to see its number, names, category, atomic mass, value and how many you own."));
        }

        private static void SetDetail(string text)
        {
            ChemUi.SetText(window, "pt_detail", text);
        }

        private static ElementDef FindAt(ElementDef[] elements, int group, int period)
        {
            for (int i = 0; i < elements.Length; i++)
            {
                ElementDef def = elements[i];
                if (def != null && def.Group == group && def.Period == period)
                {
                    return def;
                }
            }

            return null;
        }

        private static bool MatchesCategory(ElementDef def, string category, int fallbackPeriod)
        {
            if (def == null || def.Group != 0)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(def.Category))
            {
                return string.Equals(def.Category, category, StringComparison.OrdinalIgnoreCase);
            }

            // 分类字段缺失时按 CATALOG 约定的周期兜底：镧系 6、锕系 7
            return def.Period == fallbackPeriod;
        }

        private static void SortByNumber(List<ElementDef> list)
        {
            if (list == null)
            {
                return;
            }

            list.Sort(delegate (ElementDef a, ElementDef b)
            {
                int left = a != null ? a.Number : int.MaxValue;
                int right = b != null ? b.Number : int.MaxValue;
                return left.CompareTo(right);
            });
        }

        private static void AddSlot(CustomUIBuilder builder, ElementDef def)
        {
            builder.BeginColumn(0f, TextAnchor.MiddleCenter);
            RectTransform cell = builder.CurrentContainer;

            Image plate = null;
            if (cell != null)
            {
                try
                {
                    plate = cell.gameObject.AddComponent<Image>();
                    plate.color = def == null
                        ? new Color(0.35f, 0.35f, 0.38f, 0.10f)
                        : ChemUi.PlateColor(def.Category, 0.34f);
                    plate.raycastTarget = def != null;
                }
                catch (Exception e)
                {
                    ChemUi.Log?.Warning("创建周期表格子底色失败：" + e.Message);
                    plate = null;
                }
            }

            if (def == null)
            {
                builder.End();
                return;
            }

            // 贴图取不到就只显示符号，不拿预制体占位图充数
            Sprite sprite = ChemUi.ModSprite(def.SpriteKey);
            if (sprite != null)
            {
                builder.AddImage(sprite, 26f, 26f, null);
                CustomUIElement icon = builder.Last;
                if (icon != null && icon.image != null)
                {
                    icon.image.preserveAspect = true;
                    icon.image.raycastTarget = false;
                }
            }

            builder.AddLabel(string.IsNullOrEmpty(def.Symbol) ? "?" : def.Symbol, null);
            ChemUi.Style(builder.Last, 12f, CellWidth - 2f, 14f, false, ChemUi.Main, true, TextAlignmentOptions.Center);
            builder.End();

            if (cell != null)
            {
                try
                {
                    ElementCellTrigger trigger = cell.gameObject.AddComponent<ElementCellTrigger>();
                    trigger.Init(def, plate);
                }
                catch (Exception e)
                {
                    ChemUi.Log?.Warning("挂周期表格子点击响应失败：" + e.Message);
                }
            }
        }

        private static void AddFBlockRow(CustomUIBuilder builder, List<ElementDef> items, string label)
        {
            // 16 列 = 1 格行标 + 15 个元素，和主表同样的格子尺寸
            builder.BeginGrid(16, CellWidth, CellHeight, CellSpacing);

            builder.BeginColumn(0f, TextAnchor.MiddleCenter);
            builder.AddLabel(label, null);
            ChemUi.Style(builder.Last, 11f, CellWidth, 36f, true, ChemUi.Accent, true, TextAlignmentOptions.Center);
            builder.End();

            int count = 0;
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    AddSlot(builder, items[i]);
                    count++;
                }
            }

            // 数据不全时补透明格，避免这一行比主表短一截看起来像坏了
            while (count < 15)
            {
                AddFillerCell(builder);
                count++;
            }

            builder.End();
        }

        private static void AddFillerCell(CustomUIBuilder builder)
        {
            builder.BeginColumn(0f, TextAnchor.MiddleCenter);
            RectTransform cell = builder.CurrentContainer;
            if (cell != null)
            {
                try
                {
                    Image plate = cell.gameObject.AddComponent<Image>();
                    plate.color = new Color(0f, 0f, 0f, 0f);
                    plate.raycastTarget = false;
                }
                catch
                {
                    // 补位格子只要占位，加不上底色也无所谓
                }
            }

            builder.End();
        }
    }
}
