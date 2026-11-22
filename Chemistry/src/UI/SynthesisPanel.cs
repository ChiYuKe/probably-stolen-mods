using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chemistry
{
    /// <summary>
    /// 合成台标签页：列出全部化合物，显示化学式、原料（需要/已有，缺的红字）、所需工具（有/缺）。
    /// 能合成的行给「合成」按钮，点下去走 SynthesisService.Craft，把结果 message 显示在状态行并刷新列表。
    /// 判定和扣料全部交给 SynthesisService，界面只负责显示。
    /// </summary>
    internal static class SynthesisPanel
    {
        private const float RowHeight = 56f;
        private const float TextWidth = 640f;
        private const float ButtonWidth = 80f;

        private static CustomUIWindow window;
        private static RectTransform listHost;
        private static string lastMessage = "";
        private static readonly TimedReaction Reaction = new TimedReaction();

        // 550 种化合物分页显示，免得一次性铺 550 行 ScrollRect 卡顿
        private static int page;

        public static void BuildPage(CustomUIBuilder builder, CustomUIWindow host)
        {
            window = host;
            listHost = null;
            lastMessage = "";
            page = 0;

            if (builder == null)
            {
                return;
            }

            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            builder.AddLabel(Loc.Get(
                "syn_hint",
                "原料按化学式扣料，工具要放在店里（仓库/柜台/展示柜都算）。缺的原料是红字。",
                "Materials are taken by the formula; tools must be in the store (storage, counters, showcase). Missing materials are red."));
            ChemUi.Style(builder.Last, 12f, 700f, 20f, false, ChemUi.Dim, false, TextAlignmentOptions.Left);
            builder.AddFlexibleSpace();
            builder.AddButton(Loc.Get("syn_refresh", "刷新", "Refresh"), OnRefreshClicked, "syn_refresh");
            builder.Last.SetFixedWidth(84f);
            builder.End();

            builder.AddLabel(" ", "syn_status");
            ChemUi.Style(builder.Last, 13f, ChemUi.ListWidth, 34f, true, ChemUi.Main, false, TextAlignmentOptions.TopLeft);

            builder.BeginScrollFlexible(400f);
            listHost = builder.CurrentContainer;
            builder.End();
        }

        private static void TurnPage(int delta)
        {
            page += delta;
            Refresh(true);
        }

        /// <summary>重扫库存并重建整个化合物列表。resetScroll=true 时把滚动条拉回顶部。</summary>
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
                ChemUi.RefreshCounts();
                CustomUIBuilder.ClearContainer(listHost);
                var builder = new CustomUIBuilder(manager, window, listHost);

                // 分页条放在滚动区第一行：和列表行同一套控件，点击稳定
                CustomUIElement pagerLabel;
                CustomUIElement pagerPrev;
                CustomUIElement pagerNext;
                ChemPager.AddControls(builder,
                    delegate { TurnPage(-1); },
                    delegate { TurnPage(1); },
                    out pagerLabel, out pagerPrev, out pagerNext);

                CompoundDef[] compounds = ChemistryCatalog.Compounds;
                int craftable = 0;
                int total = CountCompounds(compounds);
                page = ChemPager.Clamp(page, total);
                int start = page * ChemPager.PageSize;
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

                        try
                        {
                            if (BuildRow(builder, def, rowIndex))
                            {
                                craftable++;
                            }
                        }
                        catch (Exception e)
                        {
                            ChemUi.Log?.Warning("渲染化合物行失败（" + def.Key + "）：" + e.Message);
                        }
                    }
                }
                else
                {
                    builder.AddLabel(Loc.Get(
                        "syn_no_data",
                        "化合物数据还没加载（src/Data/CompoundTable.cs 没生成？）。",
                        "Compound data is not loaded (is src/Data/CompoundTable.cs generated?)."));
                    ChemUi.Style(builder.Last, 13f, 800f, 26f, true, ChemUi.Bad, false, TextAlignmentOptions.TopLeft);
                }

                if (total > 0 && end <= start)
                {
                    // 理论上不会发生；保险起见把页码拉回第一页重新排
                    page = 0;
                }

                ChemPager.Update(pagerLabel, pagerPrev, pagerNext, page, total);

                CustomUIBuilder.RebuildLayout(listHost);
                if (resetScroll)
                {
                    ChemUi.ResetScroll(listHost);
                }

                UpdateStatus(craftable, total, page);
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("刷新合成台失败：" + e);
                ChemUi.SetText(window, "syn_status", Loc.Get(
                    "syn_refresh_failed",
                    "刷新合成台失败，详情看日志。",
                    "Failed to refresh the synthesis list; see the log."));
            }
        }

        private static void OnRefreshClicked()
        {
            lastMessage = "";
            Refresh(true);
        }

        private static bool BuildRow(CustomUIBuilder builder, CompoundDef def, int index)
        {
            SynthesisStatus status = null;
            try
            {
                status = SynthesisService.Check(def, ChemUi.Counts);
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("检查配方失败（" + def.Key + "）：" + e.Message);
            }

            bool canCraft = status != null && status.CanCraft;

            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            RectTransform row = builder.CurrentContainer;
            ChemUi.RowBackground(row, index);
            ChemUi.RowLayout(row, ChemUi.ListWidth, RowHeight);

            Sprite sprite = ChemUi.ModSprite(def.SpriteKey);
            if (sprite != null)
            {
                builder.AddImage(sprite, 34f, 34f, null);
                CustomUIElement icon = builder.Last;
                if (icon != null && icon.image != null)
                {
                    icon.image.preserveAspect = true;
                    icon.image.raycastTarget = false;
                }
            }

            builder.BeginColumn(2f, TextAnchor.MiddleLeft);
            builder.AddLabel(BuildRowText(def, status), null);
            ChemUi.Style(builder.Last, 12f, TextWidth, RowHeight - 6f, true, ChemUi.Main, false, TextAlignmentOptions.TopLeft);
            builder.End();

            builder.AddFlexibleSpace();
            builder.AddButton(
                Loc.Get("syn_craft", "合成", "Craft"),
                delegate { Craft(def); },
                null);
            CustomUIElement button = builder.Last;
            if (button != null)
            {
                button.SetFlexible(0f, 0f);
                button.SetFixedWidth(ButtonWidth);
                button.SetPreferredSize(ButtonWidth, 30f);
                button.SetInteractable(canCraft);
                ChemUi.StyleButtonText(button, 13f, true, canCraft ? ChemUi.Main : ChemUi.Dim);

                string tooltip = status != null && !canCraft && !string.IsNullOrEmpty(status.Reason)
                    ? status.Reason
                    : ChemUi.CompoundTooltip(def);
                button.SetTooltip(tooltip);
            }

            builder.End();
            return canCraft;
        }

        private static string BuildRowText(CompoundDef def, SynthesisStatus status)
        {
            var builder = new StringBuilder();
            string name = Loc.Pick(def.NameZh, def.NameEn);
            if (string.IsNullOrEmpty(name))
            {
                name = string.IsNullOrEmpty(def.Key) ? "?" : def.Key;
            }

            builder.Append("<b>").Append(name).Append("</b> (")
                .Append(string.IsNullOrEmpty(def.Formula) ? "?" : def.Formula).Append(")")
                .Append("　·　")
                .Append(ChemistryCatalog.CompoundCategoryLabel(def.Category, Loc.IsChinese))
                .Append("　·　")
                .Append(Loc.Get("syn_value", "价值 {0}", "Value {0}", def.Value));

            if (def.Key == NitricProductionData.Key)
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

        private static string NeedsLine(CompoundDef def, SynthesisStatus status)
        {
            var builder = new StringBuilder();
            builder.Append(Loc.Get("syn_need_label", "原料（需要/已有）：", "Needs (need/have): "));

            bool any = false;
            if (status != null)
            {
                // SynthesisService.Check 的 ElementNeed 已经算好需要/已有，直接用
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
                    string token = Loc.Get("syn_need_token", "{0} {1}/{2}", "{0} {1}/{2}", symbol, need.Need, need.Have);
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
            else
            {
                // Check 抛异常时退回化学式本身，至少还能看出要什么
                try
                {
                    foreach (KeyValuePair<string, int> pair in def.Inputs)
                    {
                        any = true;
                        builder.Append(pair.Key).Append(' ').Append(pair.Value).Append("　 ");
                    }
                }
                catch
                {
                    // 化学式也解析不了就显示「?」
                }
            }

            if (!any)
            {
                builder.Append(Loc.Get("syn_none", "无", "none"));
            }

            // 末尾多出来的全角空格不影响阅读，但会让省略号提前，这里去掉
            return builder.ToString().TrimEnd();
        }

        private static string ToolsLine(CompoundDef def)
        {
            var builder = new StringBuilder();
            builder.Append(Loc.Get("syn_tool_label", "工具：", "Tools: "));

            if (def.Tools == null || def.Tools.Length == 0)
            {
                builder.Append(Loc.Get("syn_tool_none", "徒手可做", "none needed"));
                return builder.ToString();
            }

            for (int i = 0; i < def.Tools.Length; i++)
            {
                string key = def.Tools[i];
                ToolDef tool = ChemistryCatalog.FindTool(key);
                string name = tool != null ? Loc.Pick(tool.NameZh, tool.NameEn) : (string.IsNullOrEmpty(key) ? "?" : key);
                bool have = tool != null && ChemUi.CountOf(tool.ItemId) > 0;
                string suffix = have
                    ? Loc.Get("syn_tool_have", "（有）", " (have)")
                    : Loc.Get("syn_tool_missing", "（缺）", " (missing)");

                builder.Append(have ? "<color=" + ChemUi.GoodHex + ">" : "<color=" + ChemUi.BadHex + ">")
                    .Append(name)
                    .Append(suffix)
                    .Append("</color>");

                if (i < def.Tools.Length - 1)
                {
                    builder.Append("　 ");
                }
            }

            return builder.ToString();
        }

        private static void Craft(CompoundDef def)
        {
            if (def == null) return;
            SynthesisStatus status = SynthesisService.Check(def);
            if (!status.CanCraft)
            {
                ShowReactionStatus(status.Reason);
                return;
            }
            PlayerStore store = PlayerStore.Instance;
            Reaction.TryStart(SynthesisService.DisplayName(def), TimedReaction.DurationFor(def),
                () => PlayerStore.HasInstance && PlayerStore.Instance == store,
                ShowReactionStatus, () => FinishCraft(def));
        }

        private static void ShowReactionStatus(string message)
        {
            lastMessage = message ?? "";
            ChemUi.SetText(window, "syn_status", lastMessage);
        }

        private static void FinishCraft(CompoundDef def)
        {
            if (def == null)
            {
                return;
            }

            string message = null;
            bool ok;
            bool threw = false;
            try
            {
                ok = SynthesisService.Craft(def, out message);
            }
            catch (Exception e)
            {
                ok = false;
                threw = true;
                message = Loc.Get("syn_craft_error", "合成失败，详情看日志。", "Crafting failed; see the log.");
                ChemUi.Log?.Error("合成 " + def.Key + " 时出错：" + e);
            }

            lastMessage = message ?? "";

            if (ok || threw)
            {
                // 成功（或半途抛异常）后重扫库存，让列表里的「已有」数量跟实际状态一致
                ChemUi.InvalidateCounts();
                Refresh(false);
            }
            else if (!string.IsNullOrEmpty(lastMessage))
            {
                ChemUi.SetText(window, "syn_status", lastMessage);
            }
        }

        private static int CountCompounds(CompoundDef[] compounds)
        {
            if (compounds == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < compounds.Length; i++)
            {
                if (compounds[i] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static void UpdateStatus(int craftable, int total, int currentPage)
        {
            string text = Loc.Get(
                "syn_summary",
                "共 {0} 种化合物，本页可合成 {1} 种（第 {2}/{3} 页）。",
                "{0} compounds, {1} craftable on this page (page {2}/{3}).",
                total,
                craftable,
                currentPage + 1,
                ChemPager.PageCount(total));

            if (!string.IsNullOrEmpty(lastMessage))
            {
                text = text + "　　" + lastMessage;
            }

            ChemUi.SetText(window, "syn_status", text);
        }
    }
}
