using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chemistry
{
    /// <summary>
    /// 地下实验室面板：顶部摘要与实验台、检测提炼、储物格、档案四页。
    ///
    /// MapUIManager 创建场景入口时调用 BuildLauncher（游戏随后会自动补一个「返回」按钮，
    /// LabPanelAnchor 把它摘掉：实验室里点铁门就能返店，面板上那个按钮是多余的）。
    /// 实验台操作面板从入口打开，让场景里的原版储物槽先显示。
    /// 数据全部来自 LabStorage / LabSynthesis / StoryService / ChemistryCatalog，界面只负责显示。
    ///
    /// 刷新方式：静态控件（摘要、标签页、状态行、滚动宿主）只建一次；刷新时先 ClearContainer
    /// 再重建当前页的动态内容。所有游戏侧调用都包 try/catch，窗口/控件被关掉时直接跳过。
    /// </summary>
    internal static partial class LabPanel
    {
        private const float WindowWidth = 1000f;
        private const float WindowHeight = 740f;

        private const float IconSize = 34f;
        private const float BenchTextWidth = 600f;
        private const float BenchRowHeight = 58f;
        private const float BenchButtonWidth = 66f;
        private const float BenchButtonHeight = 28f;
        private const float BenchScrollMinHeight = 390f;

        private const float StorageCellWidth = 104f;
        private const float StorageCellHeight = 50f;
        private const float StorageCellSpacing = 6f;
        private const float StorageScrollHeight = 330f;
        private const float DepositRowHeight = 28f;
        private const float DepositButtonWidth = 76f;
        private const float DepositAllButtonWidth = 90f;
        private const float DepositScrollHeight = 132f;

        private const float ArchiveScrollMinHeight = 350f;

        private static CustomUIWindow window;
        private static RectTransform benchHost;
        private static RectTransform storageGridHost;
        private static RectTransform storageListHost;
        private static RectTransform archiveHost;

        // 实验台页 550 种配方分页
        private static int benchPage;

        /// <summary>上次操作结果，显示在对应页的状态行；刷新时不会被清掉。</summary>
        private static string lastBenchMessage = "";
        private static readonly TimedReaction BenchReaction = new TimedReaction();
        private static string lastStorageMessage = "";

        // ---------------------------------------------------------------
        // 构建入口
        // ---------------------------------------------------------------

        public static void BuildLauncher(CustomUIBuilder builder)
        {
            builder.SetSize(220f, 118f);
            if (builder.Handle?.root != null)
            {
                LabPanelAnchor anchor = builder.Handle.root.AddComponent<LabPanelAnchor>();
                anchor.Size = new Vector2(220f, 118f);
                anchor.Anchor = new Vector2(0.86f, 0.44f);
                anchor.Window = builder.Handle;
                anchor.RemoveAfterhourReturnButton = true;
            }
            builder.AddButton(Loc.Get("lab_open_tools", "打开实验台", "Open lab bench"), OpenTools);
        }

        private static void OpenTools()
        {
            LabInventoryGrid.CaptureAfterDrop();
            if (CanRefresh())
            {
                window.Show();
                window.BringToFront();
                RefreshAll(false);
                return;
            }

            CustomUIBuilder builder = CustomUIManager.Window("chemistry.lab_tools",
                Loc.Get("lab_panel_title", "地下实验室", "Underground Lab"));
            if (builder == null)
            {
                return;
            }
            Build(builder);
            builder.AddButton(Loc.Get("lab_close_tools", "返回储物槽", "Back to storage"), Shutdown);
            builder.Show();
        }

        public static void RefreshFromInventory()
        {
            if (CanRefresh() && window.IsVisible)
            {
                RefreshAll(false);
            }
        }

        public static void Build(CustomUIBuilder builder)
        {
            if (builder == null)
            {
                return;
            }

            window = null;
            benchHost = null;
            storageGridHost = null;
            storageListHost = null;
            archiveHost = null;
            benchPage = 0;
            ResetRefiningPage();
            lastBenchMessage = "";
            lastStorageMessage = "";

            try
            {
                window = builder.Handle;
                builder.SetSize(WindowWidth, WindowHeight);

                // 游戏随后会用原版窄面板的位置覆盖窗口位置，挂个锚点脚本纠正到屏幕中央
                try
                {
                    if (window != null && window.root != null)
                    {
                        LabPanelAnchor anchor = window.root.AddComponent<LabPanelAnchor>();
                        anchor.Size = new Vector2(WindowWidth, WindowHeight);
                    }
                }
                catch (Exception e)
                {
                    ChemistryMod.Log?.Warning("实验室窗口定位脚本挂载失败：" + e.Message);
                }

                BuildSummaryRow(builder);

                builder.BeginTabs(4f);
                SetupTabsLayout(builder);

                builder.BeginTab(Loc.Get("lab_tab_bench", "实验台", "Bench"));
                MakePageFlexible(builder);
                BuildBenchPage(builder);
                builder.End();

                builder.BeginTab(Loc.Get("lab_tab_refining", "检测与提炼", "Analysis & Refining"));
                MakePageFlexible(builder);
                BuildRefiningPage(builder);
                builder.End();

                builder.BeginTab(Loc.Pick("实验指引", "Experiments"));
                MakePageFlexible(builder);
                BuildProcessGuide(builder);
                builder.End();

                builder.BeginTab(Loc.Get("lab_tab_orders", "委托", "Orders"));
                MakePageFlexible(builder);
                OrderPanel.BuildPage(builder, window);
                builder.End();

                builder.BeginTab(Loc.Get("lab_tab_storage", "储物格", "Storage"));
                MakePageFlexible(builder);
                BuildStoragePage(builder);
                builder.End();

                builder.BeginTab(Loc.Get("lab_tab_archive", "档案", "Archive"));
                MakePageFlexible(builder);
                BuildArchivePage(builder);
                builder.End();

                builder.EndTabs();

                // 窗口还没 Show 也能先把内容建出来，游戏 Show 之后布局自己会算
                RefreshAll(false);
            }
            catch (Exception e)
            {
                ChemUi.Log?.Error("创建地下实验室面板失败：" + e);
            }
        }

        /// <summary>关闭实验台操作面板，释放滚动宿主引用。</summary>
        public static void Shutdown()
        {
            CustomUIWindow closing = window;
            window = null;
            benchHost = null;
            storageGridHost = null;
            storageListHost = null;
            archiveHost = null;
            ResetRefiningPage();
            lastBenchMessage = "";
            lastStorageMessage = "";
            try
            {
                OrderPanel.Detach(closing);
            }
            catch
            {
                // 委托页没建起来时忽略
            }

            if (closing != null && closing.IsAlive)
            {
                closing.Close();
            }
        }

        private static void BuildSummaryRow(CustomUIBuilder builder)
        {
            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            LabUi.Label(builder, " ", "lab_summary", 13f, 720f, 22f, false, ChemUi.Accent, true, TextAlignmentOptions.Left);
            builder.AddFlexibleSpace();
            LabUi.Button(
                builder,
                Loc.Get("lab_refresh", "刷新", "Refresh"),
                OnRefreshClicked,
                84f,
                26f,
                true,
                Loc.Get("lab_refresh_tip", "重新读取储物格、店内库存和档案进度", "Reload the storage, store stock and archive progress"));
            builder.End();
        }

        private static void BuildBenchPage(CustomUIBuilder builder)
        {
            LabUi.Label(builder,
                Loc.Get("lab_bench_hint",
                    "只用实验室储物格里的材料和工具合成。缺的原料是红字；「补料」会把缺的元素和工具从店里搬进格子。",
                    "Craft only with materials and tools kept in the lab. Missing materials are red; \"Restock\" moves what is missing from the store into the lab."),
                "lab_bench_hint", 12f, LabUi.ContentWidth, 20f, true, ChemUi.Dim, false, TextAlignmentOptions.Left);

            LabUi.Label(builder, " ", "lab_craft_status", 12f, LabUi.ContentWidth, 36f, true, ChemUi.Dim, false, TextAlignmentOptions.TopLeft);

            builder.BeginScrollFlexible(BenchScrollMinHeight);
            benchHost = builder.CurrentContainer;
            builder.End();
        }

        private static void TurnBenchPage(int delta)
        {
            benchPage += delta;
            RefreshBench(true);
        }

        private static void BuildStoragePage(CustomUIBuilder builder)
        {
            LabUi.Label(builder, " ", "lab_storage_caption", 13f, LabUi.ContentWidth, 20f, false, ChemUi.Accent, true, TextAlignmentOptions.Left);

            builder.BeginScroll(StorageScrollHeight);
            builder.BeginGrid(8, StorageCellWidth, StorageCellHeight, StorageCellSpacing);
            storageGridHost = builder.CurrentContainer;
            builder.End();
            builder.End();

            LabUi.Label(builder, " ", "lab_storage_status", 12f, LabUi.ContentWidth, 32f, true, ChemUi.Dim, false, TextAlignmentOptions.TopLeft);

            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            LabUi.Label(builder,
                Loc.Get("lab_deposit_header", "从店里存入化学物品、样品和废金属", "Deposit chemicals, samples and scrap metal"),
                null, 12f, 460f, 20f, false, ChemUi.Accent, true, TextAlignmentOptions.Left);
            builder.AddFlexibleSpace();
            LabUi.Label(builder,
                Loc.Get("lab_deposit_hint", "同种物品或同批样品可叠加", "Same items or sample batches can stack"),
                null, 11f, 300f, 20f, false, ChemUi.Dim, false, TextAlignmentOptions.Right);
            builder.End();

            builder.BeginScroll(DepositScrollHeight);
            storageListHost = builder.CurrentContainer;
            builder.End();
        }

        private static void BuildArchivePage(CustomUIBuilder builder)
        {
            LabUi.Label(builder, " ", "lab_archive_progress", 13f, LabUi.ContentWidth, 44f, true, ChemUi.Accent, false, TextAlignmentOptions.TopLeft);

            builder.BeginScrollFlexible(ArchiveScrollMinHeight);
            archiveHost = builder.CurrentContainer;
            builder.End();
        }

        // ---------------------------------------------------------------
        // 刷新
        // ---------------------------------------------------------------

        private static void OnRefreshClicked()
        {
            lastBenchMessage = "";
            lastStorageMessage = "";
            RefreshAll(true);
        }

        private static bool CanRefresh()
        {
            return window != null && window.IsAlive;
        }

        private static void RefreshAll(bool resetScroll)
        {
            if (!CanRefresh())
            {
                return;
            }

            RefreshSummary();
            RefreshBench(resetScroll);
            RefreshStorage(resetScroll);
            RefreshArchive(resetScroll);
            RefreshRefining();
            RefreshObjective();
            OrderPanel.Refresh(resetScroll);
        }

        private static void RefreshSummary()
        {
            if (!CanRefresh())
            {
                return;
            }

            int used = SafeUsedSlots();
            int capacity = LabStorage.Capacity;
            int craftable = SafeCraftableCount();
            int unlocked = 0;
            int total = 0;

            try
            {
                LabProgress progress = StoryService.GetProgress();
                if (progress != null)
                {
                    unlocked = progress.UnlockedChapters;
                    total = progress.TotalChapters;
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("读取实验室档案进度失败：" + e.Message);
            }

            if (total <= 0)
            {
                total = SafeChapterCount();
            }

            if (unlocked < 0)
            {
                unlocked = 0;
            }

            if (total > 0 && unlocked > total)
            {
                unlocked = total;
            }

            SetLabel("lab_summary",
                Loc.Get("lab_summary",
                    "容量 {0}/{1} 格　·　实验室可合成 {2} 种　·　档案 {3}/{4}",
                    "Capacity {0}/{1} · Craftable here {2} · Archive {3}/{4}",
                    used, capacity, craftable, unlocked, total),
                ChemUi.Accent);
        }

        // ---------------------------------------------------------------
        // 第一页：实验台
        // ---------------------------------------------------------------

        private static void RefreshBench(bool resetScroll)
        {
            if (!CanRefresh() || benchHost == null)
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
                CustomUIBuilder.ClearContainer(benchHost);
                var builder = new CustomUIBuilder(manager, window, benchHost);

                CustomUIElement benchPagerLabel;
                CustomUIElement benchPagerPrev;
                CustomUIElement benchPagerNext;
                ChemPager.AddControls(builder,
                    delegate { TurnBenchPage(-1); },
                    delegate { TurnBenchPage(1); },
                    out benchPagerLabel, out benchPagerPrev, out benchPagerNext);

                CompoundDef[] compounds = null;
                try
                {
                    compounds = ChemistryCatalog.Compounds;
                }
                catch (Exception e)
                {
                    ChemUi.Log?.Warning("读取化合物数据失败：" + e.Message);
                }

                int shown = 0;
                int total = 0;
                if (compounds != null)
                {
                    for (int i = 0; i < compounds.Length; i++)
                    {
                        if (compounds[i] != null)
                        {
                            total++;
                        }
                    }
                }

                benchPage = ChemPager.Clamp(benchPage, total);
                int start = benchPage * ChemPager.PageSize;
                int end = Mathf.Min(total, start + ChemPager.PageSize);
                int position = 0;
                if (compounds != null && compounds.Length > 0)
                {
                    for (int i = 0; i < compounds.Length && position < end; i++)
                    {
                        CompoundDef def = compounds[i];
                        if (def == null)
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
                            BuildBenchRow(builder, def, rowIndex);
                        }
                        catch (Exception e)
                        {
                            ChemUi.Log?.Warning("渲染实验室配方行失败（" + (def.Key ?? "?") + "）：" + e.Message);
                        }
                    }
                }

                if (total == 0)
                {
                    LabUi.Label(builder,
                        Loc.Get("lab_bench_no_data",
                            "化合物数据还没加载（src/Data/CompoundTable.cs 没生成？）。",
                            "Compound data is not loaded (is src/Data/CompoundTable.cs generated?)."),
                        null, 13f, 820f, 26f, true, ChemUi.Bad, false, TextAlignmentOptions.TopLeft);
                }

                ChemPager.Update(benchPagerLabel, benchPagerPrev, benchPagerNext, benchPage, total);

                CustomUIBuilder.RebuildLayout(benchHost);
                if (resetScroll)
                {
                    ChemUi.ResetScroll(benchHost);
                }

                SetBenchStatus();
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("刷新实验室实验台失败：" + e);
                SetLabel("lab_craft_status",
                    Loc.Get("lab_refresh_failed", "刷新实验台失败，详情看日志。", "Failed to refresh the bench; see the log."),
                    ChemUi.Bad);
            }
        }

        private static void BuildBenchRow(CustomUIBuilder builder, CompoundDef def, int index)
        {
            SynthesisStatus status = null;
            try
            {
                status = LabSynthesis.Check(def);
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("检查实验室配方失败（" + (def.Key ?? "?") + "）：" + e.Message);
            }

            bool canCraft = status != null && status.CanCraft;
            bool usesAcidStation = AcidStation.HasRecipe(def);

            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            RectTransform row = builder.CurrentContainer;
            ChemUi.RowBackground(row, index);
            ChemUi.RowLayout(row, LabUi.RowWidth, BenchRowHeight);

            Sprite sprite = ChemUi.ModSprite(def.SpriteKey);
            if (sprite != null)
            {
                builder.AddImage(sprite, IconSize, IconSize, null);
                CustomUIElement icon = builder.Last;
                if (icon != null && icon.image != null)
                {
                    icon.image.preserveAspect = true;
                    icon.image.raycastTarget = false;
                }
            }

            builder.BeginColumn(2f, TextAnchor.MiddleLeft);
            LabUi.Label(builder, BuildBenchText(def, status), null, 12f, BenchTextWidth, BenchRowHeight - 6f, true,
                ChemUi.Main, false, TextAlignmentOptions.TopLeft);
            builder.End();

            builder.AddFlexibleSpace();

            string craftTooltip = status != null && !canCraft && !string.IsNullOrEmpty(status.Reason)
                ? status.Reason
                : ChemUi.CompoundTooltip(def);

            LabUi.Button(builder,
                usesAcidStation ? Loc.Pick("制酸", "Prepare") : Loc.Get("lab_craft", "合成", "Craft"),
                delegate
                {
                    if (usesAcidStation)
                    {
                        AcidStation.Open(def, OpenTools);
                    }
                    else
                    {
                        Craft(def);
                    }
                },
                BenchButtonWidth, BenchButtonHeight, canCraft || usesAcidStation,
                usesAcidStation ? AcidStation.RequiredMachine : craftTooltip);

            LabUi.Button(builder,
                Loc.Get("lab_restock", "补料", "Restock"),
                delegate { Restock(def); },
                BenchButtonWidth, BenchButtonHeight, !canCraft && def.Key != NitricProductionData.Key,
                Loc.Get("lab_restock_tip",
                    "把缺的元素数量和缺的工具从当铺仓库搬进实验室储物格",
                    "Move the missing elements and tools from the store into the lab storage"));

            builder.End();
        }

        private static string BuildBenchText(CompoundDef def, SynthesisStatus status)
        {
            var builder = new StringBuilder();

            string name = def != null ? Loc.Pick(def.NameZh, def.NameEn) : "";
            if (string.IsNullOrEmpty(name))
            {
                name = def != null && !string.IsNullOrEmpty(def.Key) ? def.Key : "?";
            }

            builder.Append("<b>").Append(name).Append("</b> (")
                .Append(def != null && !string.IsNullOrEmpty(def.Formula) ? def.Formula : "?").Append(')')
                .Append("　·　")
                .Append(ChemistryCatalog.CompoundCategoryLabel(def != null ? def.Category : "", Loc.IsChinese))
                .Append("　·　")
                .Append(Loc.Get("lab_value", "价值 {0}", "Value {0}", ItemPricing.CompoundDisplayValue(def)));

            if (def?.Key == NitricProductionData.Key)
            {
                builder.Append('\n').Append(NitricProductionData.RouteDescription);
            }
            else
            {
                builder.Append('\n').Append(NeedsLine(def, status));
                builder.Append('\n').Append(ToolsLine(def));
            }
            return builder.ToString();
        }

        /// <summary>元素需要/已有：优先用 LabSynthesis.Check 填好的 ElementNeed，缺的红字。</summary>
        private static string NeedsLine(CompoundDef def, SynthesisStatus status)
        {
            var builder = new StringBuilder();
            builder.Append(Loc.Get("lab_need_label", "需要/已有：", "Need/have: "));

            bool any = false;
            if (status != null && status.Elements != null)
            {
                for (int i = 0; i < status.Elements.Count; i++)
                {
                    ElementNeed need = status.Elements[i];
                    if (need == null)
                    {
                        continue;
                    }

                    any = true;
                    string symbol = need.Element != null && !string.IsNullOrEmpty(need.Element.Symbol)
                        ? need.Element.Symbol
                        : "?";
                    string token = Loc.Get("lab_need_token", "{0} {1}/{2}", "{0} {1}/{2}", symbol, need.Need, need.Have);
                    if (need.Enough)
                    {
                        builder.Append(token);
                    }
                    else
                    {
                        builder.Append("<color=").Append(ChemUi.BadHex).Append('>').Append(token).Append("</color>");
                    }

                    builder.Append("　 ");
                }
            }

            if (!any)
            {
                // Check 抛异常/没填时退回化学式本身，至少还能看出要什么
                try
                {
                    Dictionary<string, int> inputs = def != null ? def.Inputs : null;
                    if (inputs != null)
                    {
                        foreach (KeyValuePair<string, int> pair in inputs)
                        {
                            any = true;
                            builder.Append(pair.Key).Append(" ×").Append(pair.Value).Append("　 ");
                        }
                    }
                }
                catch (Exception e)
                {
                    ChemUi.Log?.Warning("实验室配方解析失败（" + (def != null ? def.Key : "?") + "）：" + e.Message);
                }
            }

            if (!any)
            {
                builder.Append(Loc.Get("lab_need_none", "无", "none"));
            }

            return builder.ToString().TrimEnd();
        }

        /// <summary>工具 有/缺：只看实验室储物格里的库存。</summary>
        private static string ToolsLine(CompoundDef def)
        {
            var builder = new StringBuilder();
            builder.Append(Loc.Get("lab_tool_label", "工具：", "Tools: "));

            string[] tools = def != null ? def.Tools : null;
            if (tools == null || tools.Length == 0)
            {
                builder.Append(Loc.Get("lab_tool_none", "徒手可做", "none needed"));
                return builder.ToString();
            }

            for (int i = 0; i < tools.Length; i++)
            {
                string key = tools[i];
                ToolDef tool = null;
                try
                {
                    tool = ChemistryCatalog.FindTool(key);
                }
                catch (Exception e)
                {
                    ChemUi.Log?.Warning("查工具数据失败（" + (key ?? "?") + "）：" + e.Message);
                }

                string name = tool != null
                    ? Loc.Pick(tool.NameZh, tool.NameEn)
                    : (string.IsNullOrEmpty(key) ? "?" : key);

                bool have = false;
                try
                {
                    have = tool != null && LabStorage.CountOf(tool.ItemId) > 0;
                }
                catch (Exception e)
                {
                    ChemUi.Log?.Warning("读取实验室工具库存失败：" + e.Message);
                }

                string suffix = have
                    ? Loc.Get("lab_tool_have", "（有）", " (have)")
                    : Loc.Get("lab_tool_missing", "（缺）", " (missing)");

                builder.Append("<color=").Append(have ? ChemUi.GoodHex : ChemUi.BadHex).Append('>')
                    .Append(name).Append(suffix).Append("</color>");
                if (i < tools.Length - 1)
                {
                    builder.Append("　 ");
                }
            }

            return builder.ToString();
        }

        private static void Craft(CompoundDef def)
        {
            if (def == null || !CanRefresh()) return;
            SynthesisStatus status = LabSynthesis.Check(def);
            if (!status.CanCraft)
            {
                ShowReactionStatus(status.Reason);
                return;
            }
            LabInventoryGrid grid = LabInventoryGrid.OpenGrid;
            if (grid == null) return;
            BenchReaction.TryStart(SynthesisService.DisplayName(def), TimedReaction.DurationFor(def),
                () => LabInventoryGrid.OpenGrid == grid, ShowReactionStatus, () => FinishCraft(def));
        }

        private static void ShowReactionStatus(string message)
        {
            lastBenchMessage = message ?? "";
            if (CanRefresh()) SetBenchStatus();
        }

        private static void FinishCraft(CompoundDef def)
        {
            if (def == null)
            {
                return;
            }

            string message = null;
            bool ok;
            try
            {
                ok = LabSynthesis.Craft(def, out message);
            }
            catch (Exception e)
            {
                ok = false;
                message = Loc.Get("lab_craft_error", "合成失败，详情看日志。", "Crafting failed; see the log.");
                ChemUi.Log?.Error("在实验室合成 " + (def.Key ?? "?") + " 时出错：" + e);
            }

            lastBenchMessage = message ?? (ok
                ? Loc.Get("lab_craft_ok", "合成成功。", "Crafted.")
                : Loc.Get("lab_craft_failed", "合成失败。", "Crafting failed."));

            // 实验室/店里库存都可能变了，下一次刷新全部重扫
            ChemUi.InvalidateCounts();
            if (CanRefresh()) RefreshAll(false);
        }

        private static void Restock(CompoundDef def)
        {
            if (def == null || !CanRefresh())
            {
                return;
            }

            string message = null;
            bool ok;
            try
            {
                ok = LabSynthesis.PullFromStore(def, out message);
            }
            catch (Exception e)
            {
                ok = false;
                message = Loc.Get("lab_restock_error", "补料失败，详情看日志。", "Restock failed; see the log.");
                ChemUi.Log?.Error("实验室补料出错（" + (def.Key ?? "?") + "）：" + e);
            }

            lastBenchMessage = message ?? (ok
                ? Loc.Get("lab_restock_ok", "已把缺的材料和工具补进储物格。", "Missing materials and tools moved into the lab.")
                : Loc.Get("lab_restock_failed", "没能补料（店里数量不够或储物格满了）。", "Could not restock (not enough in store, or the lab is full)."));

            ChemUi.InvalidateCounts();
            RefreshAll(false);
        }

        private static void SetBenchStatus()
        {
            bool hasMessage = !string.IsNullOrEmpty(lastBenchMessage);
            SetLabel("lab_craft_status",
                hasMessage
                    ? lastBenchMessage
                    : Loc.Get("lab_bench_idle",
                        "点「合成」开始造化合物；缺料就点「补料」从店里搬。全部配方都在上面，可滚动查看。",
                        "Press \"Craft\" to make a compound; press \"Restock\" to move missing materials in from the store. All recipes are listed above; scroll to see them."),
                hasMessage ? ChemUi.Main : ChemUi.Dim);
        }

        // ---------------------------------------------------------------
        // 第二页：储物格
        // ---------------------------------------------------------------

        private static void RefreshStorage(bool resetScroll)
        {
            if (!CanRefresh())
            {
                return;
            }

            int used = SafeUsedSlots();
            int capacity = LabStorage.Capacity;
            SetLabel("lab_storage_caption",
                Loc.Get("lab_storage_caption",
                    "实验室储物格：已用 {0}/{1} 格（每种化学物品占 1 格，同种可叠加到 999）。",
                    "Lab storage: {0}/{1} slots used (one slot per item type, stacks up to 999).",
                    used, capacity),
                ChemUi.Accent);

            RefreshStorageGrid();
            RefreshDepositList();
            SetStorageStatus();
        }

        private static void RefreshStorageGrid()
        {
            if (!CanRefresh() || storageGridHost == null)
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
                CustomUIBuilder.ClearContainer(storageGridHost);
                var builder = new CustomUIBuilder(manager, window, storageGridHost);

                LabSlot[] slots = null;
                try
                {
                    slots = LabStorage.Slots;
                }
                catch (Exception e)
                {
                    ChemUi.Log?.Warning("读取实验室储物格失败：" + e.Message);
                }

                int capacity = LabStorage.Capacity;
                for (int i = 0; i < capacity; i++)
                {
                    LabSlot slot = slots != null && i < slots.Length ? slots[i] : null;
                    try
                    {
                        BuildSlotCell(builder, slot);
                    }
                    catch (Exception e)
                    {
                        ChemUi.Log?.Warning("渲染实验室储物格失败（第 " + (i + 1) + " 格）：" + e.Message);
                    }
                }

                CustomUIBuilder.RebuildLayout(storageGridHost);
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("刷新实验室储物格失败：" + e);
            }
        }

        private static void BuildSlotCell(CustomUIBuilder builder, LabSlot slot)
        {
            string itemId = slot != null && slot.Count > 0 && !string.IsNullOrEmpty(slot.ItemId) ? slot.ItemId : null;
            int count = itemId != null ? Mathf.Max(1, slot.Count) : 0;
            LabItemInfo info = itemId != null ? LabUi.Resolve(itemId) : null;

            builder.BeginColumn(2f, TextAnchor.MiddleCenter);
            RectTransform cell = builder.CurrentContainer;

            Image plate = null;
            if (cell != null)
            {
                try
                {
                    plate = cell.gameObject.GetComponent<Image>();
                    if (plate == null)
                    {
                        plate = cell.gameObject.AddComponent<Image>();
                    }

                    plate.color = itemId != null
                        ? ChemUi.PlateColor(info != null ? info.Category : "", 0.34f)
                        : new Color(0.45f, 0.45f, 0.5f, 0.10f);
                    plate.raycastTarget = itemId != null;
                }
                catch (Exception e)
                {
                    ChemUi.Log?.Warning("创建实验室储物格底色失败：" + e.Message);
                    plate = null;
                }
            }

            if (itemId == null)
            {
                LabUi.Label(builder, Loc.Get("lab_slot_empty", "空", "Empty"), null, 11f,
                    StorageCellWidth - 8f, 16f, false, ChemUi.Dim, false, TextAlignmentOptions.Center);
                builder.End();
                return;
            }

            Sprite sprite = info != null ? ChemUi.ModSprite(info.SpriteKey) : null;
            if (sprite != null)
            {
                builder.AddImage(sprite, 22f, 22f, null);
                CustomUIElement icon = builder.Last;
                if (icon != null && icon.image != null)
                {
                    icon.image.preserveAspect = true;
                    icon.image.raycastTarget = false;
                }
            }

            LabUi.Label(builder, "×" + count, null, 11f, StorageCellWidth - 8f, 16f, false,
                ChemUi.Main, true, TextAlignmentOptions.Center);
            SampleDef sample = SampleTable.Find(itemId);
            string mass = sample != null ? sample.MassLine() + "\n" : "";
            string report = sample != null && itemId == sample.DetectedId ? sample.Report() + "\n" : "";
            builder.WithTooltip((info != null ? info.Name : itemId) + " ×" + count + "\n" + mass + report
                + Loc.Get("lab_slot_withdraw_hint", "点击取出 1 件到店里", "Click to take 1 back to the store"));

            builder.End();

            // 整格可点：给格子根节点挂 Button，射线面是底色 Image（子控件都关了 raycast）
            if (cell != null && plate != null)
            {
                try
                {
                    Button button = cell.gameObject.GetComponent<Button>();
                    if (button == null)
                    {
                        button = cell.gameObject.AddComponent<Button>();
                    }

                    button.targetGraphic = plate;
                    string captured = itemId;
                    button.onClick.AddListener(delegate { WithdrawSlot(captured); });
                }
                catch (Exception e)
                {
                    ChemUi.Log?.Warning("挂实验室储物格取出按钮失败：" + e.Message);
                }
            }
        }

        private static void WithdrawSlot(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !CanRefresh())
            {
                return;
            }

            string message = null;
            int moved = 0;
            try
            {
                moved = LabStorage.WithdrawToStore(itemId, 1, out message);
            }
            catch (Exception e)
            {
                message = Loc.Get("lab_withdraw_error", "取出失败，详情看日志。", "Failed to withdraw; see the log.");
                ChemUi.Log?.Error("从实验室取出 " + itemId + " 时出错：" + e);
            }

            if (moved > 0)
            {
                lastStorageMessage = string.IsNullOrEmpty(message)
                    ? Loc.Get("lab_withdraw_ok", "已取出 {0} 件到店里。", "Took {0} back to the store.", moved)
                    : message;
            }
            else
            {
                lastStorageMessage = message ?? Loc.Get("lab_withdraw_failed", "没能取出（店里放不下？）。", "Could not withdraw (no room in the store?).");
            }

            ChemUi.InvalidateCounts();
            RefreshAll(false);
        }

        private static void RefreshDepositList()
        {
            if (!CanRefresh() || storageListHost == null)
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
                CustomUIBuilder.ClearContainer(storageListHost);
                var builder = new CustomUIBuilder(manager, window, storageListHost);

                IDictionary<string, int> counts = null;
                try
                {
                    ChemUi.RefreshCounts();
                    counts = ChemUi.Counts;
                }
                catch (Exception e)
                {
                    ChemUi.Log?.Warning("读取店内库存失败：" + e.Message);
                }

                List<KeyValuePair<string, int>> entries = LabUi.StorableItems(counts);
                if (entries.Count == 0)
                {
                    LabUi.Label(builder,
                        Loc.Get("lab_deposit_empty",
                            "店里现在没有可存入的化学物品、样品或废金属。",
                            "The store has no chemicals, samples or scrap metal to deposit."),
                        null, 12f, 820f, 26f, true, ChemUi.Dim, false, TextAlignmentOptions.Left);
                }
                else
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        KeyValuePair<string, int> entry = entries[i];
                        try
                        {
                            BuildDepositRow(builder, entry.Key, entry.Value, i + 1);
                        }
                        catch (Exception e)
                        {
                            ChemUi.Log?.Warning("渲染实验室存入行失败（" + entry.Key + "）：" + e.Message);
                        }
                    }
                }

                CustomUIBuilder.RebuildLayout(storageListHost);
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("刷新实验室存入列表失败：" + e);
            }
        }

        private static void BuildDepositRow(CustomUIBuilder builder, string itemId, int storeCount, int index)
        {
            if (string.IsNullOrEmpty(itemId) || storeCount <= 0)
            {
                return;
            }

            LabItemInfo info = LabUi.Resolve(itemId);

            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            RectTransform row = builder.CurrentContainer;
            ChemUi.RowBackground(row, index);
            ChemUi.RowLayout(row, LabUi.RowWidth, DepositRowHeight);

            Sprite sprite = info != null ? ChemUi.ModSprite(info.SpriteKey) : null;
            if (sprite != null)
            {
                builder.AddImage(sprite, 22f, 22f, null);
                CustomUIElement icon = builder.Last;
                if (icon != null && icon.image != null)
                {
                    icon.image.preserveAspect = true;
                    icon.image.raycastTarget = false;
                }
            }

            LabUi.Label(builder,
                Loc.Get("lab_deposit_row", "{0}　·　店里 {1} 件", "{0}　·　{1} in store", info != null ? info.Name : itemId, storeCount),
                null, 12f, 560f, 24f, false, ChemUi.Main, false, TextAlignmentOptions.Left);

            builder.AddFlexibleSpace();

            string capturedId = itemId;
            LabUi.Button(builder,
                Loc.Get("lab_deposit_one", "存入 1", "Deposit 1"),
                delegate { Deposit(capturedId, 1); },
                DepositButtonWidth, 24f, storeCount > 0,
                Loc.Get("lab_deposit_one_tip", "把这件物品的 1 个存进实验室储物格", "Deposit one unit of this item into the lab storage"));

            LabUi.Button(builder,
                Loc.Get("lab_deposit_all", "全部存入", "Deposit all"),
                delegate { Deposit(capturedId, storeCount); },
                DepositAllButtonWidth, 24f, storeCount > 1,
                Loc.Get("lab_deposit_all_tip", "把店里这件物品全部存进实验室储物格", "Deposit every unit of this item into the lab storage"));

            builder.End();
        }

        private static void Deposit(string itemId, int units)
        {
            if (string.IsNullOrEmpty(itemId) || units <= 0 || !CanRefresh())
            {
                return;
            }

            string message = null;
            int moved = 0;
            try
            {
                moved = LabStorage.DepositFromStore(itemId, units, out message);
            }
            catch (Exception e)
            {
                message = Loc.Get("lab_deposit_error", "存入失败，详情看日志。", "Deposit failed; see the log.");
                ChemUi.Log?.Error("向实验室存入 " + itemId + " 时出错：" + e);
            }

            if (moved > 0)
            {
                lastStorageMessage = string.IsNullOrEmpty(message)
                    ? Loc.Get("lab_deposit_ok", "已存入 {0} 件。", "Deposited {0}.", moved)
                    : message;
            }
            else
            {
                lastStorageMessage = message ?? Loc.Get("lab_deposit_failed",
                    "没能存入（实验室容量格满了，或店里数量不够）。",
                    "Could not deposit (the lab is full or the store does not have enough).");
            }

            ChemUi.InvalidateCounts();
            RefreshAll(false);
        }

        private static void SetStorageStatus()
        {
            bool hasMessage = !string.IsNullOrEmpty(lastStorageMessage);
            SetLabel("lab_storage_status",
                hasMessage
                    ? lastStorageMessage
                    : Loc.Get("lab_storage_idle",
                        "点有货的格子取出 1 件到店里；下面的列表可以把店里的化学物品存进来。",
                        "Click a filled slot to take one item back to the store; the list below deposits store chemicals into the lab."),
                hasMessage ? ChemUi.Main : ChemUi.Dim);
        }

        // ---------------------------------------------------------------
        // 第三页：档案
        // ---------------------------------------------------------------

        private static void RefreshArchive(bool resetScroll)
        {
            if (!CanRefresh())
            {
                return;
            }

            int crafted = 0;
            int dangerous = 0;
            int visits = 0;
            int unlocked = 0;
            int chapters = 0;

            try
            {
                LabProgress progress = StoryService.GetProgress();
                if (progress != null)
                {
                    crafted = progress.CraftedKinds;
                    dangerous = progress.DangerousCrafted;
                    visits = progress.Visits;
                    unlocked = progress.UnlockedChapters;
                    chapters = progress.TotalChapters;
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("读取实验室档案进度失败：" + e.Message);
            }

            if (chapters <= 0)
            {
                chapters = SafeChapterCount();
            }

            SetLabel("lab_archive_progress",
                Loc.Get("lab_archive_progress",
                    "地下实验室档案：到访 {0} 次　·　已合成 {1}/{2} 种　·　危险品 {3} 种　·　章节 {4}/{5}",
                    "Underground lab archive: visits {0} · crafted {1}/{2} · hazardous {3} · chapters {4}/{5}",
                    visits, crafted, SafeCompoundCount(), dangerous, unlocked, chapters),
                ChemUi.Accent);

            if (archiveHost == null)
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
                CustomUIBuilder.ClearContainer(archiveHost);
                var builder = new CustomUIBuilder(manager, window, archiveHost);

                IList<StoryChapter> list = null;
                try
                {
                    list = StoryService.Chapters;
                }
                catch (Exception e)
                {
                    ChemUi.Log?.Warning("读取实验室档案章节失败：" + e.Message);
                }

                if (list == null || list.Count == 0)
                {
                    LabUi.Label(builder,
                        Loc.Get("lab_archive_empty",
                            "档案还没整理好（故事线数据没加载？）。",
                            "The archive is not ready yet (is the story data loaded?)."),
                        null, 13f, 820f, 26f, true, ChemUi.Dim, false, TextAlignmentOptions.Left);
                }
                else
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        StoryChapter chapter = list[i];
                        if (chapter == null)
                        {
                            continue;
                        }

                        try
                        {
                            BuildArchiveEntry(builder, chapter, i + 1);
                        }
                        catch (Exception e)
                        {
                            ChemUi.Log?.Warning("渲染实验室档案章节失败（" + (chapter.Id ?? "?") + "）：" + e.Message);
                        }
                    }
                }

                CustomUIBuilder.RebuildLayout(archiveHost);
                if (resetScroll)
                {
                    ChemUi.ResetScroll(archiveHost);
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("刷新实验室档案失败：" + e);
            }
        }

        private static void BuildArchiveEntry(CustomUIBuilder builder, StoryChapter chapter, int index)
        {
            builder.BeginColumn(2f, TextAnchor.UpperLeft);
            RectTransform entry = builder.CurrentContainer;
            if (entry != null)
            {
                LabUi.Paint(entry, chapter.Unlocked
                    ? new Color(0.86f, 0.78f, 0.52f, 0.06f)
                    : new Color(0f, 0f, 0f, 0.16f), false);
            }

            string title = Loc.Pick(chapter.TitleZh, chapter.TitleEn);
            if (string.IsNullOrEmpty(title))
            {
                title = string.IsNullOrEmpty(chapter.Id) ? "?" : chapter.Id;
            }

            if (chapter.Unlocked)
            {
                LabUi.Label(builder, "<b>" + index + ". " + title + "</b>", null, 14f,
                    LabUi.ContentWidth - 24f, 24f, false, ChemUi.Accent, true, TextAlignmentOptions.Left);

                string body = Loc.Pick(chapter.TextZh, chapter.TextEn);
                if (!string.IsNullOrEmpty(body))
                {
                    // 高度交给 TMP 自适应，正文多长就多高
                    LabUi.Label(builder, body, null, 12f, LabUi.ContentWidth - 24f, -1f, true,
                        ChemUi.Main, false, TextAlignmentOptions.TopLeft);
                }

                if (chapter.Reward > 0)
                {
                    LabUi.Label(builder,
                        Loc.Get("lab_chapter_reward", "一次性奖励：{0} 信用点（已领取）",
                            "One-time reward: {0} credits (claimed)", chapter.Reward),
                        null, 11f, LabUi.ContentWidth - 24f, -1f, true, ChemUi.Good, false, TextAlignmentOptions.TopLeft);
                }
            }
            else
            {
                LabUi.Label(builder, "<b>" + index + ". " + title + " ???</b>", null, 13f,
                    LabUi.ContentWidth - 24f, 24f, false, ChemUi.Dim, true, TextAlignmentOptions.Left);

                string hint = Loc.Pick(chapter.HintZh, chapter.HintEn);
                if (!string.IsNullOrEmpty(hint))
                {
                    LabUi.Label(builder,
                        Loc.Get("lab_chapter_hint", "解锁条件：{0}", "Unlock: {0}", hint),
                        null, 11f, LabUi.ContentWidth - 24f, -1f, true, ChemUi.Dim, false, TextAlignmentOptions.TopLeft);
                }

                if (chapter.Reward > 0)
                {
                    LabUi.Label(builder,
                        Loc.Get("lab_chapter_reward_locked", "解锁奖励：{0} 信用点", "Reward: {0} credits", chapter.Reward),
                        null, 11f, LabUi.ContentWidth - 24f, -1f, true, ChemUi.Dim, false, TextAlignmentOptions.TopLeft);
                }
            }

            builder.End();
            builder.AddSpace(4f);
        }

        // ---------------------------------------------------------------
        // 小工具
        // ---------------------------------------------------------------

        private static void SetLabel(string id, string text, Color color)
        {
            if (string.IsNullOrEmpty(id) || !CanRefresh())
            {
                return;
            }

            ChemUi.SetText(window, id, text ?? "");

            try
            {
                CustomUIElement element = window.Get(id);
                if (element != null)
                {
                    element.SetTextColor(color);
                }
            }
            catch
            {
                // 控件已经不在了，忽略
            }
        }

        private static int SafeUsedSlots()
        {
            try
            {
                int used = LabStorage.UsedSlots;
                if (used < 0)
                {
                    return 0;
                }

                int capacity = LabStorage.Capacity;
                return used > capacity ? capacity : used;
            }
            catch
            {
                return 0;
            }
        }

        private static int SafeCraftableCount()
        {
            try
            {
                int count = LabSynthesis.CountCraftable();
                return count > 0 ? count : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static int SafeCompoundCount()
        {
            try
            {
                CompoundDef[] compounds = ChemistryCatalog.Compounds;
                return compounds != null ? compounds.Length : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static int SafeChapterCount()
        {
            try
            {
                IList<StoryChapter> chapters = StoryService.Chapters;
                return chapters != null ? chapters.Count : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 让标签页容器撑满窗口宽度。节点名 "tab_pages" 来自游戏的 CustomUIBuilder.BeginTabs，
        /// 取不到就保持默认，不影响功能。
        /// </summary>
        private static void SetupTabsLayout(CustomUIBuilder builder)
        {
            if (builder == null)
            {
                return;
            }

            RectTransform tabsRoot = builder.CurrentContainer;
            LabUi.FlexibleWidth(tabsRoot);

            if (tabsRoot == null)
            {
                return;
            }

            try
            {
                Transform pages = tabsRoot.Find("tab_pages");
                LabUi.FlexibleWidth(pages as RectTransform);
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("设置实验室标签页宽度失败：" + e.Message);
            }
        }

        private static void MakePageFlexible(CustomUIBuilder builder)
        {
            if (builder == null)
            {
                return;
            }

            LabUi.FlexibleWidth(builder.CurrentContainer);
        }
    }
}
