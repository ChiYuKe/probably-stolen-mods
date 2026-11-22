using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chemistry
{
    /// <summary>
    /// 图鉴标签页：化合物列表 + 分类筛选 + 详情（描述、配方、价值）。
    /// 分类下拉的选项是从化合物数据里现场收集的，不写死分类清单。
    /// </summary>
    internal static class CompendiumPanel
    {
        private const float RowWidth = 820f;
        private const float RowHeight = 30f;

        private static CustomUIWindow window;
        private static CustomUIWindow reactionBook;
        private static RectTransform listHost;
        private static readonly List<string> categoryKeys = new List<string>();
        private static readonly List<string> categoryLabels = new List<string>();
        private static int filterIndex;
        private static CompoundDef selected;
        private static int page;

        public static void BuildPage(CustomUIBuilder builder, CustomUIWindow host)
        {
            window = host;
            listHost = null;
            selected = null;
            page = 0;
            BuildCategories();

            if (builder == null)
            {
                return;
            }

            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            builder.AddDropdown(categoryLabels, Mathf.Clamp(filterIndex, 0, Mathf.Max(0, categoryLabels.Count - 1)), OnFilterChanged, "cmp_filter");
            builder.Last.SetFixedWidth(200f);
            builder.AddSpace(8f);
            builder.AddButton(Loc.Get("cmp_refresh", "刷新", "Refresh"), OnRefreshClicked, "cmp_refresh");
            builder.Last.SetFixedWidth(84f);
            builder.AddSpace(8f);
            builder.AddButton(Loc.Pick("反应方程式", "Equations"), ShowReactionBook, "cmp_reactions");
            builder.Last.SetFixedWidth(100f);
            builder.AddSpace(8f);
            builder.AddLabel(" ", "cmp_count");
            ChemUi.Style(builder.Last, 12f, 340f, 20f, false, ChemUi.Dim, false, TextAlignmentOptions.Left);
            builder.AddFlexibleSpace();
            builder.End();

            builder.BeginScrollFlexible(270f);
            listHost = builder.CurrentContainer;
            builder.End();

            builder.AddSpace(4f);

            // 详情区：标题 / 分类与价值 / 描述和配方
            builder.BeginColumn(2f, TextAnchor.UpperLeft);
            RectTransform detail = builder.CurrentContainer;
            if (detail != null)
            {
                try
                {
                    Image background = detail.gameObject.GetComponent<Image>();
                    if (background == null)
                    {
                        background = detail.gameObject.AddComponent<Image>();
                    }

                    background.color = new Color(0f, 0f, 0f, 0.22f);
                    background.raycastTarget = false;
                }
                catch (Exception e)
                {
                    ChemUi.Log?.Warning("图鉴详情区铺底失败：" + e.Message);
                }
            }

            builder.AddLabel(" ", "cmp_title");
            ChemUi.Style(builder.Last, 16f, RowWidth, 24f, false, ChemUi.Main, true, TextAlignmentOptions.Left);
            builder.AddLabel(" ", "cmp_meta");
            ChemUi.Style(builder.Last, 12f, RowWidth, 18f, false, ChemUi.Dim, false, TextAlignmentOptions.Left);
            builder.AddLabel(" ", "cmp_body");
            ChemUi.Style(builder.Last, 12f, RowWidth, 68f, true, ChemUi.Dim, false, TextAlignmentOptions.TopLeft);
            builder.AddLabel(" ", "cmp_route");
            ChemUi.Style(builder.Last, 12f, RowWidth, 30f, true, ChemUi.Main, false, TextAlignmentOptions.TopLeft);
            builder.End();

            ShowDetail(null);
        }

        private static void ShowReactionBook()
        {
            CloseReactionBook();
            CustomUIBuilder builder = CustomUIManager.Window("chemistry.reaction_book", Loc.Pick("反应方程式", "Reaction equations"));
            if (builder == null) return;
            reactionBook = builder.Handle;
            builder.SetSize(600f, 460f).SetDraggable(true).SetCloseOnEscape(true).Center();
            LabUi.Label(builder, Loc.Pick("先用烧杯或搅拌器配制水溶液。可继续加料（任意固体或液体试剂，容量装得下即可），也可把两份溶液倒在一起。",
                "Prepare solutions with the beaker or stirrer. Add any solid or liquid reagent while the beaker has room, or pour two solutions together."),
                null, 12f, 560f, 34f, true, ChemUi.Main, false, TextAlignmentOptions.TopLeft);
            builder.BeginScrollFlexible(260f);
            foreach (AqueousReactions.Equation equation in AqueousReactions.Equations)
            {
                LabUi.Label(builder, equation.Display + "    ·    " + GameTicks.Label(equation.Ticks),
                    null, 13f, 535f, 22f, false, ChemUi.Main, false, TextAlignmentOptions.Left);
            }
            LabUi.Label(builder, LabTitration.Equation + "    ·    " + GameTicks.Label(LabTitration.ReactionTicks)
                + Loc.Pick(" · 教学滴定", " · Training titration"),
                null, 13f, 535f, 22f, false, ChemUi.Main, false, TextAlignmentOptions.Left);
            builder.End();
            LabUi.Label(builder, Loc.Pick("↑ 用排风柜收集气体。↓ 用过滤漏斗或离心机回收沉淀。",
                "↑ Collect gas at the fume hood. ↓ Recover precipitate with a filter funnel or centrifuge."),
                null, 12f, 560f, 26f, true, ChemUi.Dim, false, TextAlignmentOptions.TopLeft);
            builder.Show();
        }

        internal static void CloseReactionBook()
        {
            CustomUIWindow current = reactionBook;
            reactionBook = null;
            if (current?.IsAlive == true) current.Close();
        }

        /// <summary>重扫库存并按当前分类筛选重建列表。</summary>
        public static void Refresh(bool resetScroll)
        {
            if (window == null || !window.IsAlive || listHost == null)
            {
                return;
            }

            CustomUIManager manager = CustomUIManager.Instance;
            if (manager == null)
            {
                return;
            }

            try
            {
                CustomUIBuilder.ClearContainer(listHost);
                var builder = new CustomUIBuilder(manager, window, listHost);

                CustomUIElement pagerLabel;
                CustomUIElement pagerPrev;
                CustomUIElement pagerNext;
                ChemPager.AddControls(builder,
                    delegate { TurnPage(-1); },
                    delegate { TurnPage(1); },
                    out pagerLabel, out pagerPrev, out pagerNext);

                string filter = CurrentFilter();
                CompoundDef[] compounds = ChemistryCatalog.Compounds;
                int total = 0;
                if (compounds != null)
                {
                    for (int i = 0; i < compounds.Length; i++)
                    {
                        CompoundDef def = compounds[i];
                        if (def == null)
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(filter) && !string.Equals(def.Category, filter, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        total++;
                    }
                }

                page = ChemPager.Clamp(page, total);
                int start = page * ChemPager.PageSize;
                int end = Mathf.Min(total, start + ChemPager.PageSize);
                int position = 0;
                int shown = 0;
                if (compounds != null)
                {
                    for (int i = 0; i < compounds.Length && position < end; i++)
                    {
                        CompoundDef def = compounds[i];
                        if (def == null)
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(filter) && !string.Equals(def.Category, filter, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        int rowIndex = position;
                        position++;
                        if (rowIndex < start)
                        {
                            continue;
                        }

                        shown++;
                        try
                        {
                            BuildRow(builder, def);
                        }
                        catch (Exception e)
                        {
                            ChemUi.Log?.Warning("渲染图鉴行失败（" + def.Key + "）：" + e.Message);
                        }
                    }
                }

                if (total == 0)
                {
                    builder.AddLabel(Loc.Get("cmp_empty", "这个分类下没有化合物。", "No compounds in this category."));
                    ChemUi.Style(builder.Last, 13f, 800f, 26f, false, ChemUi.Dim, false, TextAlignmentOptions.Left);
                }

                ChemPager.Update(pagerLabel, pagerPrev, pagerNext, page, total);

                CustomUIBuilder.RebuildLayout(listHost);
                if (resetScroll)
                {
                    ChemUi.ResetScroll(listHost);
                }

                ChemUi.SetText(window, "cmp_count", Loc.Get(
                    "cmp_count",
                    "共 {0} 种（本页 {1} 种）",
                    "{0} in total ({1} on this page)",
                    total,
                    shown));
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("刷新图鉴失败：" + e);
            }
        }

        private static void TurnPage(int delta)
        {
            page += delta;
            Refresh(true);
        }

        private static void OnRefreshClicked()
        {
            Refresh(true);
        }

        private static void BuildRow(CustomUIBuilder builder, CompoundDef def)
        {
            string name = Loc.Pick(def.NameZh, def.NameEn);
            if (string.IsNullOrEmpty(name))
            {
                name = string.IsNullOrEmpty(def.Key) ? "?" : def.Key;
            }

            string label = Loc.Get(
                "cmp_row",
                "{0}　{1}　·　{2}　·　价值 {3}",
                "{0}　{1}　·　{2}　·　value {3}",
                ChemUi.Fit(name, 26),
                string.IsNullOrEmpty(def.Formula) ? "?" : def.Formula,
                ChemistryCatalog.CompoundCategoryLabel(def.Category, Loc.IsChinese),
                def.Value);

            builder.AddButton(label, delegate { ShowDetail(def); }, null);
            CustomUIElement button = builder.Last;
            if (button == null)
            {
                return;
            }

            button.SetFlexible(0f, 0f);
            button.SetPreferredSize(RowWidth, RowHeight);
            ChemUi.StyleButtonText(button, 13f, false, ChemUi.Main);
            button.SetTooltip(ChemUi.CompoundTooltip(def));
        }

        private static void OnFilterChanged(int index)
        {
            filterIndex = index;
            selected = null;
            page = 0;
            ShowDetail(null);
            Refresh(true);
        }

        private static string CurrentFilter()
        {
            if (filterIndex <= 0 || filterIndex >= categoryKeys.Count)
            {
                return "";
            }

            return categoryKeys[filterIndex];
        }

        private static void BuildCategories()
        {
            categoryKeys.Clear();
            categoryLabels.Clear();

            categoryKeys.Add("");
            categoryLabels.Add(Loc.Get("cmp_all", "全部", "All"));

            CompoundDef[] compounds = ChemistryCatalog.Compounds;
            if (compounds == null)
            {
                return;
            }

            for (int i = 0; i < compounds.Length; i++)
            {
                CompoundDef def = compounds[i];
                if (def == null || string.IsNullOrEmpty(def.Category))
                {
                    continue;
                }

                bool exists = false;
                for (int j = 0; j < categoryKeys.Count; j++)
                {
                    if (string.Equals(categoryKeys[j], def.Category, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    categoryKeys.Add(def.Category);
                    categoryLabels.Add(ChemistryCatalog.CompoundCategoryLabel(def.Category, Loc.IsChinese));
                }
            }
        }

        private static void ShowDetail(CompoundDef def)
        {
            selected = def;

            if (window == null || !window.IsAlive)
            {
                return;
            }

            if (def == null)
            {
                ChemUi.SetText(window, "cmp_title", Loc.Get("cmp_detail_title", "化合物图鉴", "Compound compendium"));
                ChemUi.SetText(window, "cmp_meta", Loc.Get("cmp_detail_hint", "从上面的列表里点一种化合物，这里显示描述、配方和价值。",
                    "Click a compound above to see its description, recipe and value."));
                ChemUi.SetText(window, "cmp_body", "");
                ChemUi.SetText(window, "cmp_route", "");
                return;
            }

            string name = Loc.Pick(def.NameZh, def.NameEn);
            if (string.IsNullOrEmpty(name))
            {
                name = string.IsNullOrEmpty(def.Key) ? "?" : def.Key;
            }

            ChemUi.SetText(window, "cmp_title", name + "  (" + (string.IsNullOrEmpty(def.Formula) ? "?" : def.Formula) + ")");
            ChemUi.SetText(window, "cmp_meta", Loc.Get(
                "cmp_detail_meta",
                "{0}　·　价值 {1}　·　拥有 {2}",
                "{0}　·　value {1}　·　owned {2}",
                ChemistryCatalog.CompoundCategoryLabel(def.Category, Loc.IsChinese),
                def.Value,
                ChemUi.CountOf(def.ItemId)));

            var builder = new StringBuilder();
            string desc = Loc.Pick(def.DescZh, def.DescEn);
            if (!string.IsNullOrEmpty(desc))
            {
                builder.Append(desc);
            }

            string recipe = RecipeLine(def);
            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(recipe);
            ChemUi.SetText(window, "cmp_body", builder.ToString());
            ChemUi.SetText(window, "cmp_route", LabWorkflow.Describe(def.ItemId));
        }

        private static string RecipeLine(CompoundDef def)
        {
            if (def.Key == NitricProductionData.Key)
            {
                return NitricProductionData.RouteDescription;
            }
            var builder = new StringBuilder();
            builder.Append(Loc.Get("cmp_recipe_label", "配方：", "Recipe: "));

            bool any = false;
            try
            {
                foreach (KeyValuePair<string, int> pair in def.Inputs)
                {
                    any = true;
                    ElementDef element = ChemistryCatalog.FindElement(pair.Key);
                    string symbol = element != null && !string.IsNullOrEmpty(element.Symbol) ? element.Symbol : pair.Key;
                    builder.Append(symbol).Append(" ×").Append(pair.Value).Append("　");
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("解析图鉴配方失败（" + (def.Key ?? "?") + "）：" + e.Message);
            }

            if (!any)
            {
                builder.Append(Loc.Get("cmp_recipe_unknown", "化学式无法解析", "unparseable formula"));
            }

            builder.Append("　·　").Append(Loc.Get("cmp_tools_label", "工具：", "Tools: "));
            if (def.Tools == null || def.Tools.Length == 0)
            {
                builder.Append(Loc.Get("cmp_tools_none", "徒手可做", "none needed"));
            }
            else
            {
                for (int i = 0; i < def.Tools.Length; i++)
                {
                    ToolDef tool = ChemistryCatalog.FindTool(def.Tools[i]);
                    string toolName = tool != null
                        ? Loc.Pick(tool.NameZh, tool.NameEn)
                        : (string.IsNullOrEmpty(def.Tools[i]) ? "?" : def.Tools[i]);
                    if (i > 0)
                    {
                        builder.Append(Loc.Get("list_separator", "、", ", "));
                    }

                    builder.Append(toolName);
                }
            }

            return builder.ToString().TrimEnd();
        }
    }
}
