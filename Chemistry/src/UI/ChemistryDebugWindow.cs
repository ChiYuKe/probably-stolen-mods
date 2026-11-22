using System;
using TMPro;
using UnityEngine;

namespace Chemistry
{
    internal static class ChemistryDebugWindow
    {
        private const string WindowId = "chemistry.debug";
        private const string StatusId = "status";
        private const string ResetId = "reset";
        private const string ClearStorageId = "clear_lab_storage";
        private const float ButtonWidth = 178f;
        private static CustomUIWindow window;
        private static PendingAction pending;

        private enum PendingAction
        {
            None,
            ResetModData,
            ClearLabStorage,
        }

        internal static void Open()
        {
            if (window?.IsAlive == true)
            {
                window.Show();
                return;
            }
            if (CustomUIManager.Instance == null)
            {
                return;
            }

            try
            {
                Build();
            }
            catch (Exception error)
            {
                ChemistryMod.Log?.Warning("打开化学调试面板失败：" + error);
                Close();
            }
        }

        internal static void Close()
        {
            CustomUIWindow current = window;
            window = null;
            pending = PendingAction.None;
            if (current?.IsAlive == true)
            {
                current.Close();
            }
        }

        internal static void Report(string message, bool success)
        {
            if (window?.IsAlive != true || !window.IsVisible)
            {
                return;
            }
            ChemUi.SetText(window, StatusId, message);
            var label = window.Get(StatusId)?.text;
            if (label != null)
            {
                label.color = success ? ChemUi.Good : ChemUi.Bad;
            }
        }

        private static void Build()
        {
            CustomUIBuilder builder = CustomUIManager.Window(WindowId, Loc.Pick("化学调试面板", "Chemistry Debug Panel"));
            if (builder?.Handle == null)
            {
                return;
            }
            window = builder.Handle;
            builder.SetSize(632f, 566f).SetDraggable(true).SetCloseOnEscape(true).Center().OnClose(() =>
            {
                window = null;
                pending = PendingAction.None;
            });

            builder.BeginRow(10f, TextAnchor.MiddleLeft);
            Button(builder, "去地下实验室", "Go to underground lab", LabLocation.TravelHere);
            Button(builder, "回当铺", "Return to store", LabLocation.ReturnToStore);
            Button(builder, "打开化学实验台", "Open chemistry bench", () =>
            {
                Close();
                ChemistryWindow.Open();
            });
            builder.End();
            builder.AddSpace(6f);

            builder.BeginRow(9f, TextAnchor.UpperLeft);
            Section(builder, "工序测试", "Process tests");
            Button(builder, "样品检测测试包", "Sample analysis kit", DebugKit.GiveSampleKit);
            Button(builder, "制酸测试包", "Acid synthesis kit", DebugKit.GiveAcidCraftKit);
            Button(builder, "酸浸测试包", "Leaching kit", DebugKit.GiveLeachingKit);
            Button(builder, "过滤测试包", "Filtering kit", DebugKit.GiveFilteringKit);
            Button(builder, "还原测试包", "Reduction kit", DebugKit.GiveReductionKit);
            builder.End();

            Section(builder, "试剂与容器", "Reagents & containers");
            Button(builder, "满瓶酸测试包", "Full acid bottles kit", DebugKit.GiveFullAcidKit);
            Button(builder, "全容器测试包", "All containers kit", DebugKit.GiveBottleFillKit);
            Button(builder, "大容器测试包", "Bulk containers kit", DebugKit.GiveBulkContainerKit);
            Button(builder, "破损容器样品", "Damaged containers", DebugKit.GiveDamagedBottleKit);
            builder.End();

            Section(builder, "补给", "Supplies");
            Button(builder, "开局补给", "Starter kit", DebugKit.GiveStarterKit);
            Button(builder, "常用元素 ×1", "Common elements ×1", () => DebugKit.GiveCommonElements(1));
            Button(builder, "全部元素 ×1", "All elements ×1", () => DebugKit.GiveAllElements(1));
            Button(builder, "全部工具", "All tools", DebugKit.GiveAllTools);
            Button(builder, "全部化合物", "All compounds", DebugKit.GiveAllCompounds);
            builder.End();
            builder.End();
            builder.AddSpace(6f);

            builder.BeginRow(10f, TextAnchor.MiddleLeft);
            Button(builder, "前期设备测试包", "Early equipment kit", () => DebugKit.GiveEquipmentKit(1));
            Button(builder, "中期设备测试包", "Midgame equipment kit", () => DebugKit.GiveEquipmentKit(2));
            Button(builder, "后期设备测试包", "Late equipment kit", () => DebugKit.GiveEquipmentKit(3));
            builder.End();
            builder.BeginRow(10f, TextAnchor.MiddleLeft);
            Button(builder, "分层沉淀测试包", "Mixture separation kit", DebugKit.GiveMixtureKit);
            Button(builder, "蒸发冷凝测试包", "Distillation test kit", DebugKit.GiveDistillationKit);
            Button(builder, "分析与滴定测试包", "Analysis & titration kit", DebugKit.GiveAnalysisKit);
            builder.End();
            builder.BeginRow(10f, TextAnchor.MiddleLeft);
            Button(builder, "进阶实验测试包", "Advanced experiments kit", DebugKit.GiveProcessKit);
            Button(builder, "自动化厂房测试包", "Automated workshop kit", DebugKit.GiveFactoryKit);
            builder.End();
            builder.AddSpace(6f);

            builder.AddLabel(Loc.Pick("诊断与存档", "Diagnostics & save"));
            ChemUi.Style(builder.Last, 14f, 580f, 22f, true, ChemUi.Main, false, TextAlignmentOptions.Left);
            builder.BeginRow(10f, TextAnchor.MiddleLeft);
            Button(builder, "掉落表检查", "Check loot tables", LootIntegration.LogState);
            Button(builder, "仓库盘点", "Summarize store inventory", DebugKit.LogInventorySummary);
            builder.AddButton(ResetLabel(), ConfirmReset, ResetId);
            builder.Last.SetPreferredSize(ButtonWidth, 26f);
            builder.End();

            builder.BeginRow(10f, TextAnchor.MiddleLeft);
            builder.AddButton(ClearStorageLabel(), ConfirmClearStorage, ClearStorageId);
            builder.Last.SetPreferredSize(ButtonWidth, 26f);
            builder.End();

            builder.AddLabel(Loc.Pick("测试包优先送到已打开的实验室储物格，否则送往当铺库存。", "Kits go to the open lab storage or, if closed, to store inventory."), StatusId);
            ChemUi.Style(builder.Last, 13f, 580f, 42f, false, ChemUi.Dim, true, TextAlignmentOptions.Left);
            builder.Show();
        }

        private static void Section(CustomUIBuilder builder, string zh, string en)
        {
            builder.BeginPanel(194f, 184f, TextAnchor.UpperLeft, 8f, 4f);
            builder.AddLabel(Loc.Pick(zh, en));
            ChemUi.Style(builder.Last, 14f, ButtonWidth, 22f, true, ChemUi.Accent, false, TextAlignmentOptions.Left);
        }

        private static void Button(CustomUIBuilder builder, string zh, string en, Action action)
        {
            builder.AddButton(Loc.Pick(zh, en), () =>
            {
                if (pending != PendingAction.None)
                {
                    SetPending(PendingAction.None);
                    Report(Loc.Pick("已取消清空。", "Clear canceled."), true);
                }
                action();
            });
            builder.Last.SetPreferredSize(ButtonWidth, 26f);
        }

        private static string ResetLabel() => pending == PendingAction.ResetModData
            ? Loc.Pick("确认清空模组数据", "Confirm reset")
            : Loc.Pick("清空模组数据", "Reset mod data");

        private static string ClearStorageLabel() => pending == PendingAction.ClearLabStorage
            ? Loc.Pick("确认清空地下室仓库", "Confirm clear storage")
            : Loc.Pick("清空地下室仓库", "Clear lab storage");

        private static void SetPending(PendingAction action)
        {
            pending = action;
            ChemUi.SetText(window, ResetId, ResetLabel());
            ChemUi.SetText(window, ClearStorageId, ClearStorageLabel());
        }

        private static void ConfirmReset() => Confirm(PendingAction.ResetModData,
            Loc.Pick("会清除当前存档的化学模组数据。再次点击确认。",
                "This clears Chemistry data in the current save. Click again to confirm."),
            DebugKit.ClearModData);

        private static void ConfirmClearStorage()
        {
            if (LabInventoryGrid.OpenGrid == null)
            {
                SetPending(PendingAction.None);
                Report(Loc.Pick("请先进入地下实验室并打开储物格。", "Open the underground lab storage first."), false);
                return;
            }

            Confirm(PendingAction.ClearLabStorage,
                Loc.Pick("会永久移除地下室储物格里的所有物品。再次点击确认。",
                    "This permanently removes every item in lab storage. Click again to confirm."),
                DebugKit.ClearLabStorage);
        }

        private static void Confirm(PendingAction action, string warning, Action execute)
        {
            if (pending != action)
            {
                SetPending(action);
                Report(warning, false);
                return;
            }

            SetPending(PendingAction.None);
            execute();
        }
    }
}
