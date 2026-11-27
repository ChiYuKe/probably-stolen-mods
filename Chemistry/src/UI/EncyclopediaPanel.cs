using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chemistry
{
    /// <summary>Searchable index of registered Chemistry items, with recipes and practical workflows.</summary>
    internal static class EncyclopediaPanel
    {
        private const float SidebarWidth = 218f;
        private const float GridCellWidth = 202f;
        private const float GridCellHeight = 142f;
        private const float FlowNodeWidth = 56f;
        private const float FlowNodeHeight = 76f;
        private static readonly Color Mauve = new Color(0.46f, 0.23f, 0.36f, 1f);
        private static readonly Color MauveDark = new Color(0.32f, 0.16f, 0.26f, 1f);
        private static readonly Color SidebarBackground = new Color(0.18f, 0.17f, 0.21f, 1f);
        private static readonly Color Paper = new Color(0.94f, 0.94f, 0.95f, 1f);
        private static readonly Color Tile = new Color(0.23f, 0.25f, 0.32f, 1f);
        private static readonly Color DetailText = new Color(0.14f, 0.14f, 0.16f, 1f);

        private static CustomUIWindow window;
        private static CustomUIWindow reactionBook;
        private static CustomUIWindow flowGuide;
        private static RectTransform listHost;
        private static RectTransform categoryHost;
        private static readonly List<string> categoryKeys = new List<string>();
        private static readonly List<string> categoryLabels = new List<string>();
        private static readonly HashSet<string> expandedCategories = new HashSet<string>();
        private static readonly List<ViewState> history = new List<ViewState>();
        private static EncyclopediaEntry[] entries;
        private static string search = "";
        private static string categoryFilter = "";
        private static string categoryLabelFilter = "";
        private static EncyclopediaEntry selectedEntry;
        private static int historyIndex = -1;
        private static int page;

        internal static void BuildPage(CustomUIBuilder builder, CustomUIWindow host)
        {
            window = host;
            listHost = null;
            categoryHost = null;
            selectedEntry = null;
            page = 0;
            entries = EncyclopediaCatalog.BuildEntries();
            BuildCategories();
            history.Clear();
            historyIndex = -1;

            if (builder == null) return;

            builder.BeginRow(0f, TextAnchor.MiddleLeft);
            RectTransform titleBar = builder.CurrentContainer;
            SetBackground(titleBar, Mauve, false);
            SetLayout(titleBar, 0f, 34f, 1f, 0f);
            builder.AddLabel(Loc.Pick("数据库", "Database"), "cmp_db_title");
            ChemUi.Style(builder.Last, 16f, 160f, 26f, false, Color.white, true, TextAlignmentOptions.Left);
            builder.AddFlexibleSpace();
            AddHeaderButton(builder, Loc.Pick("任务流程", "Task flow"), ShowFlowGuide, 88f);
            AddHeaderButton(builder, Loc.Pick("反应方程式", "Equations"), ShowReactionBook, 104f);
            AddHeaderButton(builder, Loc.Get("cmp_refresh", "刷新", "Refresh"), OnRefreshClicked, 66f);
            builder.End();

            builder.AddSpace(4f);
            builder.BeginRow(8f, TextAnchor.UpperLeft);
            builder.BeginColumn(4f, TextAnchor.UpperLeft);
            RectTransform contentColumn = builder.CurrentContainer;
            SetLayout(contentColumn, 0f, 0f, 1f, 1f);
            SetBackground(contentColumn, Paper, false);

            builder.BeginRow(4f, TextAnchor.MiddleLeft);
            RectTransform navigationBar = builder.CurrentContainer;
            SetBackground(navigationBar, new Color(0.88f, 0.87f, 0.89f, 1f), false);
            SetLayout(navigationBar, 0f, 34f, 1f, 0f);
            builder.AddButton("←", GoBack, "cmp_back");
            StyleNavButton(builder.Last);
            builder.AddButton("→", GoForward, "cmp_forward");
            StyleNavButton(builder.Last);
            builder.AddLabel(" ", "cmp_breadcrumb");
            ChemUi.Style(builder.Last, 12f, 460f, 24f, false, Mauve, true, TextAlignmentOptions.Left);
            builder.AddFlexibleSpace();
            builder.AddLabel(" ", "cmp_count");
            ChemUi.Style(builder.Last, 11f, 130f, 22f, false, DetailText, false, TextAlignmentOptions.Right);
            builder.End();

            builder.BeginScrollFlexible(340f);
            listHost = builder.CurrentContainer;
            SetBackground(listHost, Paper, false);
            builder.End();
            builder.End();

            builder.BeginColumn(5f, TextAnchor.UpperLeft);
            RectTransform sidebar = builder.CurrentContainer;
            SetLayout(sidebar, SidebarWidth, 0f, 0f, 1f);
            SetBackground(sidebar, SidebarBackground, false);
            builder.AddLabel(Loc.Pick("搜索数据库", "Search database"));
            ChemUi.Style(builder.Last, 12f, SidebarWidth - 16f, 22f, false, Color.white, false, TextAlignmentOptions.Left);
            builder.BeginRow(3f, TextAnchor.MiddleLeft);
            builder.AddInput(Loc.Pick("名称、公式或用途", "Name, formula or use"), search, null, "cmp_search", OnSearchChanged);
            builder.Last.SetPreferredSize(SidebarWidth - 48f, 30f);
            builder.Last.SetFlexible(1f, 0f);
            builder.AddButton("×", ClearSearch);
            builder.Last.SetFixedWidth(26f);
            builder.Last.SetPreferredSize(26f, 30f);
            builder.Last.SetImageColor(Mauve);
            ChemUi.StyleButtonText(builder.Last, 16f, true, Color.white);
            if (builder.Last?.text != null) builder.Last.text.alignment = TextAlignmentOptions.Center;
            builder.End();
            builder.AddLabel(Loc.Pick("分类", "Categories"));
            ChemUi.Style(builder.Last, 13f, SidebarWidth - 16f, 24f, false, Color.white, true, TextAlignmentOptions.Left);
            builder.BeginScrollFlexible(300f);
            categoryHost = builder.CurrentContainer;
            builder.End();
            builder.End();
            builder.End();

            history.Add(CaptureState());
            historyIndex = 0;
            BuildSidebar();
            UpdateNavigation();
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
            LabUi.Label(builder, Loc.Pick("↑ 用通风橱收集气体。↓ 用过滤漏斗或离心机回收沉淀。",
                "↑ Collect gas at the fume hood. ↓ Recover precipitate with a filter funnel or centrifuge."),
                null, 12f, 560f, 26f, true, ChemUi.Dim, false, TextAlignmentOptions.TopLeft);
            builder.Show();
        }

        private static void ShowFlowGuide()
        {
            CloseFlowGuide();
            CustomUIBuilder builder = CustomUIManager.Window("chemistry.task_flow", Loc.Pick("任务与实验流程", "Tasks and lab workflow"));
            if (builder == null) return;
            flowGuide = builder.Handle;
            builder.SetSize(760f, 650f).SetDraggable(true).SetCloseOnEscape(true).Center();
            builder.OnClose(() => flowGuide = null);

            builder.BeginScrollFlexible(565f);
            AddGuideHeading(builder, Loc.Pick("基础实验流程", "Basic lab workflow"));
            AddGuideText(builder, Loc.Pick(
                "1. 在当铺或垃圾场收集元素、试剂和未知样品。\n"
                + "2. 首次处理废料时，先用光谱仪检测并打开成分报告，选定可回收目标。\n"
                + "3. 按样品详情准备酸或其他试剂；普通化合物在合成台按元素配比合成，工具留在地下实验室储物格中备用。\n"
                + "4. 在提炼反应台依次完成酸浸、过滤、还原；液体样品则按详情选择混合、静置、过滤、蒸馏、分液或检测。\n"
                + "5. 从产物槽取回材料，按需要分装、干燥或交付；废液和不可回收残渣放入对应废物桶。",
                "1. Gather elements, reagents and unknown samples from the pawnshop or dumping ground.\n"
                + "2. For scrap recovery, scan the sample with the spectrometer and open its composition report before choosing a target.\n"
                + "3. Prepare the acid or reagent listed for the sample. Synthesize ordinary compounds from their elemental ratios; keep tools in underground lab storage for reuse.\n"
                + "4. At the refining station, leach, filter and reduce in order. For liquids, follow the item entry and choose mixing, settling, filtering, distillation, separation or analysis.\n"
                + "5. Collect output, then transfer, dry or deliver it as needed. Put waste liquid and unrecoverable solids in the matching waste bin."), 12f, 700f, 112f, true, ChemUi.Main);

            AddGuideHeading(builder, Loc.Pick("委托顺序", "Order sequence"));
            OrderTemplate[] orders = OrderData.CreateTemplates();
            for (int i = 0; i < orders.Length; i++)
            {
                OrderTemplate order = orders[i];
                string prerequisite = PrerequisiteLine(order, orders);
                AddGuideHeading(builder, (i + 1) + ". " + order.Title);
                string reward = Loc.Get("encyclopedia_order_reward", "奖励 {0} 信用点", "Reward: {0} credits", order.Reward);
                if (order.Repeatable) reward += Loc.Pick(" · 可重复接取", " · repeatable");
                AddGuideText(builder, order.Brief + "\n" + order.RequirementLine() + " · " + reward + "\n" + prerequisite,
                    11f, 700f, 82f, true, ChemUi.Dim);
                if (order.Steps != null)
                {
                    for (int stepIndex = 0; stepIndex < order.Steps.Length; stepIndex++)
                    {
                        OrderStep step = order.Steps[stepIndex];
                        AddGuideText(builder, "• " + step.Title + " — " + step.Hint,
                            11f, 680f, 48f, true, ChemUi.Main);
                    }
                }
                builder.AddSpace(5f);
            }

            AddGuideHeading(builder, Loc.Pick("档案章节", "Story archive"));
            IList<StoryChapter> chapters = StoryData.CreateChapters();
            for (int i = 0; i < chapters.Count; i++)
            {
                StoryChapter chapter = chapters[i];
                AddGuideText(builder, (i + 1) + ". " + Loc.Pick(chapter.TitleZh, chapter.TitleEn)
                    + Loc.Get("encyclopedia_reward", " · 奖励 {0} 信用点", " · reward {0} credits", chapter.Reward)
                    + "\n" + Loc.Pick("解锁条件：", "Unlock: ") + Loc.Pick(chapter.HintZh, chapter.HintEn),
                    11f, 700f, 42f, true, ChemUi.Dim);
            }
            builder.End();
            builder.Show();
        }

        private static string PrerequisiteLine(OrderTemplate order, OrderTemplate[] orders)
        {
            var builder = new StringBuilder(Loc.Pick("开放条件：", "Availability: "));
            bool any = false;
            if (!string.IsNullOrEmpty(order.RequiresOrder))
            {
                OrderTemplate previous = Array.Find(orders, candidate => candidate.Id == order.RequiresOrder);
                builder.Append(Loc.Pick("完成「", "Complete '") + (previous?.Title ?? order.RequiresOrder) + Loc.Pick("」", "'"));
                any = true;
            }
            if (!string.IsNullOrEmpty(order.RequiresChapter))
            {
                StoryChapter chapter = Array.Find(StoryData.CreateChapters(), item => item.Id == order.RequiresChapter);
                if (any) builder.Append(Loc.Pick("；", "; "));
                builder.Append(Loc.Pick("解锁档案「", "Unlock archive '")
                    + (chapter == null ? order.RequiresChapter : Loc.Pick(chapter.TitleZh, chapter.TitleEn)) + Loc.Pick("」", "'"));
                any = true;
            }
            if (order.RequiresDay > 0)
            {
                if (any) builder.Append(Loc.Pick("；", "; "));
                builder.Append(Loc.Get("encyclopedia_order_day", "到达第 {0} 天", "Reach day {0}", order.RequiresDay));
                any = true;
            }
            if (order.RequiresAnalysis)
            {
                if (any) builder.Append(Loc.Pick("；", "; "));
                builder.Append(Loc.Pick("完成一次样品检测", "Analyze a sample once"));
                any = true;
            }
            if (!any) builder.Append(Loc.Pick("无前置条件", "No prerequisites"));
            if (order.DurationDays > 0) builder.Append(Loc.Get("encyclopedia_order_deadline", " · 期限 {0} 天", " · {0}-day limit", order.DurationDays));
            if (order.Lots > 1) builder.Append(Loc.Get("encyclopedia_order_lots", " · 分 {0} 批交付", " · deliver in {0} lots", order.Lots));
            return builder.ToString();
        }

        private static void AddGuideHeading(CustomUIBuilder builder, string text)
        {
            builder.AddLabel(text);
            ChemUi.Style(builder.Last, 14f, 700f, 24f, true, ChemUi.Accent, true, TextAlignmentOptions.Left);
        }

        private static void AddGuideText(CustomUIBuilder builder, string text, float fontSize, float width,
            float height, bool wrap, Color color)
        {
            builder.AddLabel(text);
            ChemUi.Style(builder.Last, fontSize, width, height, wrap, color, false, TextAlignmentOptions.TopLeft);
        }

        internal static void CloseReactionBook()
        {
            CustomUIWindow current = reactionBook;
            reactionBook = null;
            if (current?.IsAlive == true) current.Close();
        }

        internal static void CloseFlowGuide()
        {
            CustomUIWindow current = flowGuide;
            flowGuide = null;
            if (current?.IsAlive == true) current.Close();
        }

        internal static void Refresh(bool resetScroll)
        {
            if (window == null || !window.IsAlive || listHost == null) return;
            CustomUIManager manager = CustomUIManager.Instance;
            if (manager == null) return;

            try
            {
                CustomUIBuilder.ClearContainer(listHost);
                var builder = new CustomUIBuilder(manager, window, listHost);
                if (selectedEntry != null)
                {
                    BuildDetail(builder, selectedEntry);
                    ChemUi.SetText(window, "cmp_count", Loc.Pick("物品详情", "Item details"));
                }
                else
                {
                    List<EncyclopediaEntry> filtered = FilteredEntries();
                    int total = filtered.Count;
                    page = ChemPager.Clamp(page, total);
                    int start = page * ChemPager.PageSize;
                    int end = Mathf.Min(total, start + ChemPager.PageSize);
                    ChemPager.AddControls(builder, delegate { TurnPage(-1); }, delegate { TurnPage(1); },
                        out CustomUIElement pagerLabel, out CustomUIElement pagerPrev, out CustomUIElement pagerNext);
                    ChemPager.Update(pagerLabel, pagerPrev, pagerNext, page, total);
                    if (total == 0)
                    {
                        builder.AddLabel(Loc.Pick("没有符合条件的物品。", "No matching items."));
                        ChemUi.Style(builder.Last, 13f, 600f, 32f, false, DetailText, false, TextAlignmentOptions.Left);
                    }
                    else
                    {
                        builder.BeginGrid(3, GridCellWidth, GridCellHeight, 8f);
                        for (int i = start; i < end; i++) BuildCard(builder, filtered[i], i - start);
                        builder.End();
                    }

                    ChemUi.SetText(window, "cmp_count", Loc.Get("encyclopedia_count", "共 {0} 件", "{0} items", total));
                }

                CustomUIBuilder.RebuildLayout(listHost);
                if (resetScroll) ChemUi.ResetScroll(listHost);
                UpdateNavigation();
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("刷新百科列表失败：" + e);
            }
        }

        private static List<EncyclopediaEntry> FilteredEntries()
        {
            var result = new List<EncyclopediaEntry>();
            if (entries == null) return result;
            string query = (search ?? "").Trim();
            for (int i = 0; i < entries.Length; i++)
            {
                EncyclopediaEntry entry = entries[i];
                if (!string.IsNullOrEmpty(categoryFilter) && entry.CategoryKey != categoryFilter) continue;
                if (!string.IsNullOrEmpty(categoryLabelFilter) && entry.CategoryLabel != categoryLabelFilter) continue;
                if (!string.IsNullOrEmpty(query) && entry.SearchText.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                result.Add(entry);
            }
            return result;
        }

        private static void TurnPage(int delta)
        {
            page += delta;
            UpdateCurrentHistory();
            Refresh(true);
        }

        private static void OnRefreshClicked()
        {
            entries = EncyclopediaCatalog.BuildEntries();
            ChemUi.RefreshCounts();
            selectedEntry = null;
            page = 0;
            history.Clear();
            historyIndex = -1;
            BuildCategories();
            history.Add(CaptureState());
            historyIndex = 0;
            BuildSidebar();
            Refresh(true);
        }

        private static void BuildCard(CustomUIBuilder builder, EncyclopediaEntry entry, int index)
        {
            builder.BeginColumn(3f, TextAnchor.MiddleCenter);
            RectTransform card = builder.CurrentContainer;
            SetBackground(card, index % 2 == 0 ? Tile : new Color(0.27f, 0.29f, 0.36f, 1f), false);
            builder.AddImage(ChemUi.ModSprite(entry.SpriteKey), 76f, 76f);
            builder.Last.SetPreferredSize(76f, 76f);
            if (builder.Last?.image != null)
            {
                builder.Last.image.preserveAspect = true;
                builder.Last.image.raycastTarget = false;
            }
            string label = entry.Name + (string.IsNullOrEmpty(entry.Formula) ? "" : "\n" + entry.Formula);
            builder.AddButton(label, delegate { NavigateToDetail(entry); });
            CustomUIElement button = builder.Last;
            if (button != null)
            {
                button.SetPreferredSize(GridCellWidth - 12f, 48f);
                button.SetImageColor(Mauve);
                ChemUi.StyleButtonText(button, 12f, true, Color.white);
                if (button.text != null)
                {
                    button.text.alignment = TextAlignmentOptions.Center;
                    button.text.enableWordWrapping = true;
                }
                button.SetTooltip(entry.Name + "\n" + entry.Description
                    + (entry.Operation.Length == 0 ? "" : "\n" + entry.Operation));
            }
            builder.End();
        }

        private static void BuildDetail(CustomUIBuilder builder, EncyclopediaEntry entry)
        {
            builder.BeginRow(12f, TextAnchor.MiddleLeft);
            builder.AddImage(ChemUi.ModSprite(entry.SpriteKey), 92f, 92f);
            builder.Last.SetPreferredSize(92f, 92f);
            if (builder.Last?.image != null)
            {
                builder.Last.image.preserveAspect = true;
                builder.Last.image.raycastTarget = false;
            }
            builder.BeginColumn(3f, TextAnchor.MiddleLeft);
            string title = entry.Name + (string.IsNullOrEmpty(entry.Formula) ? "" : "  ·  " + entry.Formula);
            builder.AddLabel(title);
            ChemUi.Style(builder.Last, 20f, 520f, 32f, true, DetailText, false, TextAlignmentOptions.Left);
            if (!LabStorage.Loaded) LabStorage.EnsureLoaded();
            string value = entry.Value > 0 ? entry.Value.ToString() : Loc.Pick("不可出售", "—");
            builder.AddLabel(Loc.Get("encyclopedia_meta", "{0}　·　价值 {1}　·　当铺 {2}　·　实验室 {3}",
                "{0}　·　value {1}　·　shop {2}　·　lab {3}",
                entry.CategoryLabel, value, ChemUi.CountOf(entry.ItemId), LabStorage.CountOf(entry.ItemId)));
            ChemUi.Style(builder.Last, 12f, 520f, 24f, true, Mauve, false, TextAlignmentOptions.Left);
            builder.End();
            builder.End();

            builder.AddSeparator(null, 5);
            AddSectionHeading(builder, Loc.Pick("物品说明", "Item description"));
            AddWrappedText(builder, entry.Description, 13f, 620f, DetailText);
            builder.AddSpace(8f);
            AddSectionHeading(builder, Loc.Pick("操作方式与仪器", "How to use and equipment"));
            AddWrappedText(builder,
                string.IsNullOrEmpty(entry.Operation)
                    ? Loc.Pick("此物品没有额外操作步骤。", "No additional handling steps are listed.")
                    : entry.Operation,
                13f, 620f, DetailText);
            builder.AddSpace(8f);
            AddSectionHeading(builder, Loc.Pick("配方与处理流程", "Recipe and process"));
            BuildFlowDiagram(builder, entry);
        }

        private static void BuildFlowDiagram(CustomUIBuilder builder, EncyclopediaEntry entry)
        {
            EncyclopediaFlowStage[] stages = entry?.FlowStages;
            if (stages == null || stages.Length == 0)
            {
                AddWrappedText(builder, Loc.Pick("暂无流程图。", "No process diagram is available."),
                    12f, 620f, DetailText);
                return;
            }

            for (int i = 0; i < stages.Length; i++)
            {
                EncyclopediaFlowStage stage = stages[i];
                builder.AddLabel((i + 1) + ". " + stage.Title);
                ChemUi.Style(builder.Last, 12f, 620f, 22f, false, Mauve, true, TextAlignmentOptions.Left);
                builder.BeginRow(4f, TextAnchor.MiddleCenter);
                if (stage.Inputs != null)
                {
                    for (int inputIndex = 0; inputIndex < stage.Inputs.Length; inputIndex++)
                    {
                        if (inputIndex > 0) AddFlowPlus(builder);
                        AddFlowNode(builder, stage.Inputs[inputIndex], false);
                    }
                }

                if (stage.Inputs != null && stage.Inputs.Length > 0) AddFlowArrow(builder);
                AddFlowNode(builder, stage.Machine, true);
                if (stage.Outputs != null && stage.Outputs.Length > 0)
                {
                    AddFlowArrow(builder);
                    for (int outputIndex = 0; outputIndex < stage.Outputs.Length; outputIndex++)
                    {
                        if (outputIndex > 0) AddFlowPlus(builder);
                        AddFlowNode(builder, stage.Outputs[outputIndex], false);
                    }
                }
                builder.End();

                if (!string.IsNullOrEmpty(stage.Note))
                {
                    AddWrappedText(builder, stage.Note, 11f, 620f, DetailText);
                }
                builder.AddSpace(7f);
            }
        }

        private static void AddFlowNode(CustomUIBuilder builder, EncyclopediaFlowNode node, bool machine)
        {
            if (node == null) return;
            builder.BeginColumn(1f, TextAnchor.MiddleCenter);
            RectTransform root = builder.CurrentContainer;
            SetBackground(root, machine ? new Color(0.80f, 0.70f, 0.76f, 1f) : new Color(0.88f, 0.88f, 0.91f, 1f), false);
            float width = machine ? FlowNodeWidth + 14f : FlowNodeWidth;
            SetLayout(root, width, FlowNodeHeight, 0f, 0f);
            float iconSize = machine ? 44f : 38f;
            builder.AddImage(ChemUi.ModSprite(node.SpriteKey), iconSize, iconSize);
            builder.Last.SetPreferredSize(iconSize, iconSize);
            if (builder.Last?.image != null)
            {
                builder.Last.image.preserveAspect = true;
                builder.Last.image.raycastTarget = false;
            }
            builder.AddLabel(node.Label);
            ChemUi.Style(builder.Last, 10f, width - 4f, 28f, true, DetailText, machine, TextAlignmentOptions.Center);
            builder.End();
        }

        private static void AddFlowArrow(CustomUIBuilder builder)
        {
            builder.AddLabel("→");
            ChemUi.Style(builder.Last, 18f, 14f, 42f, false, Mauve, true, TextAlignmentOptions.Center);
        }

        private static void AddFlowPlus(CustomUIBuilder builder)
        {
            builder.AddLabel("+");
            ChemUi.Style(builder.Last, 15f, 14f, 42f, false, ChemUi.Dim, true, TextAlignmentOptions.Center);
        }

        private static void AddSectionHeading(CustomUIBuilder builder, string text)
        {
            builder.AddLabel(text);
            ChemUi.Style(builder.Last, 15f, 620f, 26f, false, Mauve, true, TextAlignmentOptions.Left);
        }

        private static void AddWrappedText(CustomUIBuilder builder, string text, float fontSize, float width, Color color)
        {
            string value = text ?? "";
            builder.AddLabel(value);
            CustomUIElement element = builder.Last;
            ChemUi.Style(element, fontSize, width, 0f, true, color, false, TextAlignmentOptions.TopLeft);
            if (element?.text != null)
            {
                float height = element.text.GetPreferredValues(value, width, 0f).y;
                element.SetPreferredSize(width, Mathf.Max(fontSize + 10f, height + 8f));
            }
        }

        private static void AddHeaderButton(CustomUIBuilder builder, string text, Action callback, float width)
        {
            builder.AddButton(text, callback);
            CustomUIElement button = builder.Last;
            button?.SetFixedWidth(width);
            button?.SetPreferredSize(width, 26f);
            if (button != null)
            {
                button.SetImageColor(MauveDark);
                ChemUi.StyleButtonText(button, 11f, false, Color.white);
                if (button.text != null) button.text.alignment = TextAlignmentOptions.Center;
            }
        }

        private static void StyleNavButton(CustomUIElement button)
        {
            button?.SetFixedWidth(30f);
            button?.SetPreferredSize(30f, 28f);
            if (button == null) return;
            button.SetImageColor(Paper);
            ChemUi.StyleButtonText(button, 20f, true, DetailText);
            if (button.text != null) button.text.alignment = TextAlignmentOptions.Center;
        }

        private static void SetBackground(RectTransform rect, Color color, bool raycast)
        {
            if (rect == null) return;
            try
            {
                Image image = rect.gameObject.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
                image.color = color;
                image.raycastTarget = raycast;
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("设置百科面板底色失败：" + e.Message);
            }
        }

        private static void SetLayout(RectTransform rect, float width, float height, float flexibleWidth, float flexibleHeight)
        {
            if (rect == null) return;
            LayoutElement layout = rect.gameObject.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
            if (width > 0f)
            {
                layout.minWidth = width;
                layout.preferredWidth = width;
            }
            if (height > 0f)
            {
                layout.minHeight = height;
                layout.preferredHeight = height;
            }
            if (flexibleWidth >= 0f) layout.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0f) layout.flexibleHeight = flexibleHeight;
        }

        private static void OnSearchChanged(string value)
        {
            search = value ?? "";
            page = 0;
            selectedEntry = null;
            UpdateCurrentHistory();
            Refresh(true);
        }

        private static void ClearSearch()
        {
            search = "";
            try
            {
                window?.Get("cmp_search")?.SetString("");
            }
            catch { }
            OnSearchChanged("");
        }

        private static void SelectCategory(string key, string label)
        {
            categoryFilter = key ?? "";
            categoryLabelFilter = label ?? "";
            selectedEntry = null;
            page = 0;
            PushHistory();
            BuildSidebar();
            Refresh(true);
        }

        private static void ToggleCategory(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!expandedCategories.Add(key)) expandedCategories.Remove(key);
            BuildSidebar();
        }

        private static void BuildSidebar()
        {
            if (categoryHost == null || window == null || !window.IsAlive) return;
            CustomUIManager manager = CustomUIManager.Instance;
            if (manager == null) return;
            try
            {
                CustomUIBuilder.ClearContainer(categoryHost);
                var builder = new CustomUIBuilder(manager, window, categoryHost);
                for (int i = 0; i < categoryKeys.Count; i++)
                {
                    string key = categoryKeys[i];
                    string label = categoryLabels[i];
                    bool expanded = !string.IsNullOrEmpty(key) && expandedCategories.Contains(key);
                    builder.BeginRow(3f, TextAnchor.MiddleCenter);
                    builder.AddButton(string.IsNullOrEmpty(key) ? "·" : expanded ? "▼" : "▶",
                        string.IsNullOrEmpty(key) ? null : (Action)(() => ToggleCategory(key)));
                    CustomUIElement arrow = builder.Last;
                    arrow?.SetFixedWidth(24f);
                    arrow?.SetPreferredSize(24f, 27f);
                    if (arrow != null)
                    {
                        arrow.SetImageColor(MauveDark);
                        ChemUi.StyleButtonText(arrow, 10f, false, Color.white);
                        if (arrow.text != null) arrow.text.alignment = TextAlignmentOptions.Center;
                        if (string.IsNullOrEmpty(key)) arrow.SetInteractable(false);
                    }

                    builder.AddButton(label, () => SelectCategory(key, ""));
                    StyleCategoryButton(builder.Last, categoryFilter == key && categoryLabelFilter.Length == 0, 27f);
                    builder.End();

                    if (!expanded) continue;
                    List<string> children = CategoryChildren(key);
                    for (int childIndex = 0; childIndex < children.Count; childIndex++)
                    {
                        string child = children[childIndex];
                        builder.AddButton("　› " + child, () => SelectCategory(key, child));
                        StyleCategoryButton(builder.Last, categoryFilter == key && categoryLabelFilter == child, 25f);
                    }
                }
                CustomUIBuilder.RebuildLayout(categoryHost);
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("刷新百科分类栏失败：" + e);
            }
        }

        private static void StyleCategoryButton(CustomUIElement button, bool selected, float height)
        {
            if (button == null) return;
            button.SetFlexible(1f, 0f);
            button.SetPreferredSize(SidebarWidth - 52f, height);
            button.SetImageColor(selected ? new Color(0.62f, 0.31f, 0.48f, 1f) : Mauve);
            ChemUi.StyleButtonText(button, 11f, selected, Color.white);
        }

        private static List<string> CategoryChildren(string key)
        {
            var children = new List<string>();
            if (entries == null) return children;
            for (int i = 0; i < entries.Length; i++)
            {
                EncyclopediaEntry entry = entries[i];
                if (entry.CategoryKey != key || string.IsNullOrEmpty(entry.CategoryLabel)
                    || children.Contains(entry.CategoryLabel)) continue;
                children.Add(entry.CategoryLabel);
            }
            children.Sort(StringComparer.Ordinal);
            return children;
        }

        private static void NavigateToDetail(EncyclopediaEntry entry)
        {
            if (entry == null) return;
            selectedEntry = entry;
            PushHistory();
            Refresh(true);
        }

        private static void GoBack()
        {
            if (historyIndex <= 0) return;
            historyIndex--;
            ApplyHistory();
        }

        private static void GoForward()
        {
            if (historyIndex < 0 || historyIndex >= history.Count - 1) return;
            historyIndex++;
            ApplyHistory();
        }

        private static void ApplyHistory()
        {
            if (historyIndex < 0 || historyIndex >= history.Count) return;
            ViewState state = history[historyIndex];
            categoryFilter = state.CategoryKey;
            categoryLabelFilter = state.CategoryLabel;
            search = state.Search;
            page = state.Page;
            selectedEntry = FindEntry(state.ItemId);
            try
            {
                window?.Get("cmp_search")?.SetString(search);
            }
            catch { }
            BuildSidebar();
            Refresh(true);
        }

        private static EncyclopediaEntry FindEntry(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].ItemId == itemId) return entries[i];
            }
            return null;
        }

        private static void PushHistory()
        {
            if (historyIndex < 0)
            {
                history.Clear();
                historyIndex = -1;
            }
            else if (historyIndex < history.Count - 1)
            {
                history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
            }
            history.Add(CaptureState());
            historyIndex = history.Count - 1;
        }

        private static void UpdateCurrentHistory()
        {
            if (historyIndex < 0 || historyIndex >= history.Count) return;
            if (historyIndex < history.Count - 1)
            {
                history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
            }
            history[historyIndex] = CaptureState();
        }

        private static ViewState CaptureState()
        {
            return new ViewState
            {
                CategoryKey = categoryFilter,
                CategoryLabel = categoryLabelFilter,
                Search = search,
                Page = page,
                ItemId = selectedEntry?.ItemId
            };
        }

        private static void UpdateNavigation()
        {
            if (window == null || !window.IsAlive) return;
            string crumb;
            if (selectedEntry != null)
            {
                crumb = Loc.Pick("索引", "Index") + "  >  " + selectedEntry.CategoryLabel + "  >  " + selectedEntry.Name;
            }
            else
            {
                string category = string.IsNullOrEmpty(categoryLabelFilter)
                    ? CategoryName(categoryFilter) : categoryLabelFilter;
                crumb = Loc.Pick("索引", "Index") + "  >  " + category;
            }
            ChemUi.SetText(window, "cmp_breadcrumb", crumb);
            ChemUi.SetInteractable(window, "cmp_back", historyIndex > 0);
            ChemUi.SetInteractable(window, "cmp_forward", historyIndex >= 0 && historyIndex < history.Count - 1);
        }

        private static string CategoryName(string key)
        {
            if (string.IsNullOrEmpty(key)) return Loc.Pick("全部物品", "All items");
            int index = categoryKeys.IndexOf(key);
            return index >= 0 && index < categoryLabels.Count
                ? categoryLabels[index] : key;
        }

        private static void BuildCategories()
        {
            categoryKeys.Clear();
            categoryLabels.Clear();
            AddCategory("", Loc.Pick("全部物品", "All items"));
            AddCategory("elements", Loc.Pick("元素", "Elements"));
            AddCategory("compounds", Loc.Pick("化合物", "Compounds"));
            AddCategory("tools", Loc.Pick("器材", "Tools"));
            AddCategory("equipment", Loc.Pick("仪器与设备", "Instruments"));
            AddCategory("samples", Loc.Pick("样品", "Samples"));
            AddCategory("materials", Loc.Pick("混合物与产物", "Mixtures and products"));
            AddCategory("containers", Loc.Pick("容器与废物桶", "Containers and waste bins"));
            AddCategory("components", Loc.Pick("部件与耗材", "Parts and supplies"));
        }

        private static void AddCategory(string key, string label)
        {
            categoryKeys.Add(key);
            categoryLabels.Add(label);
        }

        private sealed class ViewState
        {
            internal string CategoryKey;
            internal string CategoryLabel;
            internal string Search;
            internal int Page;
            internal string ItemId;
        }
    }
}
