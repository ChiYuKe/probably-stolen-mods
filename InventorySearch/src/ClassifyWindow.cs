using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace InventorySearch
{
    /// <summary>归类整理的预览/执行窗口：先看会挪多少件，再决定执行还是撤销。</summary>
    internal static class ClassifyWindow
    {
        private const string WindowId = "inventory_search.classify";

        private static ModLog log;
        private static CustomUIWindow window;
        private static int scopeIndex;
        private static List<ClassifyCandidate> candidates = new List<ClassifyCandidate>();
        private static int skipped;

        private static readonly ClassifyGroup[] Order =
        {
            ClassifyGroup.Contraband,
            ClassifyGroup.Weapon,
            ClassifyGroup.Luxury,
            ClassifyGroup.Consumable,
            ClassifyGroup.Household,
            ClassifyGroup.Supply,
            ClassifyGroup.Document,
            ClassifyGroup.Other
        };

        public static void Initialize(ModLog logger)
        {
            log = logger;
        }

        public static void Open()
        {
            if (CustomUIManager.Instance == null)
            {
                log?.Warning("当前场景没有 CustomUIManager，开不了归类窗口");
                return;
            }

            if (window != null && window.IsAlive)
            {
                window.Show();
                RefreshPreview();
                return;
            }

            try
            {
                Build();
            }
            catch (Exception e)
            {
                log?.Error("创建归类窗口失败：" + e);
                window = null;
            }
        }

        public static void Close()
        {
            CustomUIWindow current = window;
            window = null;
            try
            {
                if (current != null && current.IsAlive)
                {
                    current.Close();
                }
            }
            catch
            {
                // 场景卸载时窗口可能已经没了
            }
        }

        private static void Build()
        {
            CustomUIBuilder builder = CustomUIManager.Window(
                WindowId,
                Loc.Get("classify_title", "归类整理", "Tidy by category"));
            if (builder == null)
            {
                return;
            }

            window = builder.Handle;
            builder
                .SetSize(640f, 650f)
                .SetDraggable(true)
                .SetCloseOnEscape(true)
                .OnClose(OnClosed)
                .Center();

            var scopes = new List<string>
            {
                Loc.Get("classify_scope_warehouse", "只整理仓库（新货入柜）", "Warehouse only"),
                Loc.Get("classify_scope_all", "整理全部容器（含柜台、展示柜、暗格）", "All containers")
            };

            builder.AddDropdown(scopes, Mathf.Clamp(scopeIndex, 0, 1), OnScopeChanged, "scope");
            builder.Last.SetFixedWidth(380f);
            builder.AddSpace(6f);

            builder.AddLabel(RulesText(), "rules");
            ConfigureLabel(builder.Last, 13f, 220f);

            builder.AddLabel(" ", "counts");
            ConfigureLabel(builder.Last, 13f, 190f);

            builder.AddLabel(" ", "status");
            ConfigureLabel(builder.Last, 12f, 38f);

            builder.AddSpace(4f);
            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            builder.AddButton(Loc.Get("classify_execute", "执行", "Apply"), Execute, "execute");
            builder.Last.SetFixedWidth(110f);
            builder.AddButton(Loc.Get("classify_undo", "撤销上一次", "Undo last"), Undo, "undo");
            builder.Last.SetFixedWidth(150f);
            builder.AddButton(Loc.Get("classify_refresh", "刷新预览", "Refresh"), RefreshPreview, "refresh");
            builder.Last.SetFixedWidth(110f);
            builder.AddFlexibleSpace();
            builder.AddButton(Loc.Get("classify_close", "关闭", "Close"), Close, "close");
            builder.Last.SetFixedWidth(92f);
            builder.End();

            builder.Show();
            RefreshPreview();
        }

        private static void OnClosed()
        {
            window = null;
        }

        private static string RulesText()
        {
            var lines = new List<string>
            {
                Loc.Get("classify_rule_contraband", "违禁 / 赃物 → 暗格", "Contraband / stolen → hidden stash"),
                Loc.Get("classify_rule_weapon", "武器 / 弹药 / 护甲 → 展示柜", "Weapons / ammo / armor → showcase"),
                Loc.Get("classify_rule_luxury", "奢侈品 → 展示柜", "Luxury → showcase"),
                Loc.Get("classify_rule_consumable", "食物 / 饮料 / 药品 → 前柜", "Food / drink / medicine → front counter"),
                Loc.Get("classify_rule_household", "家居 / 彩票 → 前柜", "Household / lottery → front counter"),
                Loc.Get("classify_rule_supply", "材料 / 工具 / 零件 / 种子 → 后柜", "Materials / tools / parts / seeds → back counter"),
                Loc.Get("classify_rule_document", "文件 / 其他 → 仓库", "Documents / other → warehouse"),
                Loc.Get("classify_note", "只动本店所有、没固定、能拿起来的顶层物品；机器、背包、盒子和放不下的都会留在原地。",
                    "Only owned, unpinned top-level items are moved; machines, bags, boxes and items that do not fit stay where they are.")
            };

            return string.Join("\n", lines.ToArray());
        }

        private static void ConfigureLabel(CustomUIElement element, float size, float height)
        {
            if (element == null)
            {
                return;
            }

            element.SetPreferredSize(600f, height);
            if (element.text != null)
            {
                element.text.enableAutoSizing = false;
                element.text.fontSize = size;
                element.text.enableWordWrapping = true;
                element.text.overflowMode = TextOverflowModes.Overflow;
                element.text.alignment = TextAlignmentOptions.TopLeft;
            }
        }

        private static void OnScopeChanged(int index)
        {
            scopeIndex = index;
            RefreshPreview();
        }

        private static void RefreshPreview()
        {
            if (window == null || !window.IsAlive)
            {
                return;
            }

            try
            {
                candidates = ClassifyService.Collect(scopeIndex == 1, out skipped);
            }
            catch (Exception e)
            {
                log?.Warning("预览归类失败：" + e);
                candidates = new List<ClassifyCandidate>();
                skipped = 0;
            }

            var counts = new Dictionary<ClassifyGroup, int>();
            foreach (ClassifyCandidate candidate in candidates)
            {
                int count;
                counts.TryGetValue(candidate.Group, out count);
                counts[candidate.Group] = count + 1;
            }

            var text = new List<string>();
            foreach (ClassifyGroup group in Order)
            {
                int count;
                if (counts.TryGetValue(group, out count) && count > 0)
                {
                    text.Add(Loc.Get(
                        "classify_count_line",
                        "{0} → {1}：{2}",
                        "{0} → {1}: {2}",
                        GroupName(group),
                        TargetName(group),
                        count));
                }
            }

            if (text.Count == 0)
            {
                text.Add(Loc.Get("classify_none", "没有需要移动的物品。", "Nothing to move."));
            }
            else
            {
                text.Add("");
                text.Add(Loc.Get(
                    "classify_skipped",
                    "预计移动 {0} 件，{1} 件跳过。",
                    "{0} to move, {1} skipped.",
                    candidates.Count,
                    skipped));
            }

            SetText("counts", string.Join("\n", text.ToArray()));
            SetInteractable("execute", candidates.Count > 0);
            SetInteractable("undo", ClassifyService.HasUndo);
            SetStatus(Loc.Get(
                "classify_planned",
                "先点「执行」，全部移动会立刻生效；不满意可以撤销。",
                "Click Apply to move them; you can undo afterwards."));
        }

        private static void Execute()
        {
            if (EmporiumEntry.Instance == null || ModHelper.IsOutsideStore())
            {
                SetStatus(Loc.Get("classify_no_store", "回当铺里再整理。", "Go back to the store first."));
                return;
            }

            try
            {
                if (UIDragHandler.isDraggingAnyUI)
                {
                    SetStatus(Loc.Get("classify_dragging", "先松开鼠标再整理。", "Let go of the mouse first."));
                    return;
                }

                ClassifyResult result = ClassifyService.Execute(candidates);
                RefreshPreview();
                SetStatus(Loc.Get(
                    "classify_moved",
                    "已移动 {0} 件，{1} 件没放下。",
                    "Moved {0}, {1} did not fit.",
                    result.Moved,
                    result.Failed));
            }
            catch (Exception e)
            {
                log?.Error("执行归类失败：" + e);
                SetStatus(Loc.Get("classify_error", "整理时出错了，看日志。", "Something went wrong; check the log."));
            }
        }

        private static void Undo()
        {
            if (!ClassifyService.HasUndo)
            {
                SetStatus(Loc.Get("classify_no_undo", "没有可撤销的整理。", "Nothing to undo."));
                return;
            }

            if (EmporiumEntry.Instance == null || ModHelper.IsOutsideStore())
            {
                SetStatus(Loc.Get("classify_no_store", "回当铺里再整理。", "Go back to the store first."));
                return;
            }

            try
            {
                int restored;
                int failed;
                ClassifyService.Undo(out restored, out failed);
                RefreshPreview();
                SetStatus(Loc.Get(
                    "classify_undo_done",
                    "已撤回 {0} 件，{1} 件没放下。",
                    "Restored {0}, {1} did not fit.",
                    restored,
                    failed));
            }
            catch (Exception e)
            {
                log?.Error("撤销归类失败：" + e);
                SetStatus(Loc.Get("classify_error", "整理时出错了，看日志。", "Something went wrong; check the log."));
            }
        }

        private static string GroupName(ClassifyGroup group)
        {
            switch (group)
            {
                case ClassifyGroup.Contraband:
                    return Loc.Get("classify_group_contraband", "违禁 / 赃物", "Contraband / stolen");
                case ClassifyGroup.Weapon:
                    return Loc.Get("classify_group_weapon", "武器 / 弹药 / 护甲", "Weapons / ammo / armor");
                case ClassifyGroup.Luxury:
                    return Loc.Get("classify_group_luxury", "奢侈品", "Luxury");
                case ClassifyGroup.Consumable:
                    return Loc.Get("classify_group_consumable", "食物 / 饮料 / 药品", "Food / drink / medicine");
                case ClassifyGroup.Household:
                    return Loc.Get("classify_group_household", "家居 / 彩票", "Household / lottery");
                case ClassifyGroup.Supply:
                    return Loc.Get("classify_group_supply", "材料 / 工具 / 零件", "Materials / tools / parts");
                case ClassifyGroup.Document:
                    return Loc.Get("classify_group_document", "文件", "Documents");
                default:
                    return Loc.Get("classify_group_other", "其他", "Other");
            }
        }

        private static string TargetName(ClassifyGroup group)
        {
            switch (group)
            {
                case ClassifyGroup.Contraband:
                    return Loc.Get("classify_target_hidden", "暗格", "hidden stash");
                case ClassifyGroup.Weapon:
                case ClassifyGroup.Luxury:
                    return Loc.Get("classify_target_showcase", "展示柜", "showcase");
                case ClassifyGroup.Consumable:
                case ClassifyGroup.Household:
                    return Loc.Get("classify_target_front", "前柜", "front counter");
                case ClassifyGroup.Supply:
                    return Loc.Get("classify_target_back", "后柜", "back counter");
                default:
                    return Loc.Get("classify_target_warehouse", "仓库", "warehouse");
            }
        }

        private static void SetText(string id, string text)
        {
            if (window == null || !window.IsAlive)
            {
                return;
            }

            try
            {
                CustomUIElement element = window.Get(id);
                if (element != null)
                {
                    element.SetText(text ?? "");
                }
            }
            catch
            {
                // 控件没了不影响执行
            }
        }

        private static void SetInteractable(string id, bool value)
        {
            if (window == null || !window.IsAlive)
            {
                return;
            }

            try
            {
                CustomUIElement element = window.Get(id);
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

        private static void SetStatus(string text)
        {
            SetText("status", text);
        }
    }
}
