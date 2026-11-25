using System;
using TMPro;
using UnityEngine;

namespace Chemistry
{
    /// <summary>
    /// 拖放检测/提炼后弹出的两个小窗口：
    ///   · 成分报告（光谱仪拖到样品上）；
    ///   · 酸浸或蒸馏，按报告选择要处理的成分。
    /// 只负责显示和按钮回调，检测/提炼逻辑在 SampleInteraction / LabRefining。
    /// </summary>
    internal static class SampleReportWindow
    {
        private const string DetectWindowId = "chemistry.sample_report";
        private const string RefineWindowId = "chemistry.sample_refine";

        private static CustomUIWindow detectWindow;
        private static CustomUIWindow refineWindow;
        private static readonly TimedReaction RefineReaction = new TimedReaction();
        // ---------------------------------------------------------------
        // 成分报告
        // ---------------------------------------------------------------

        public static void ShowDetected(SampleDef sample, bool already)
        {
            if (sample == null)
            {
                return;
            }

            CloseDetect();

            try
            {
                CustomUIBuilder builder = CustomUIManager.Window(
                    DetectWindowId,
                    Loc.Get("sample_report_title", "成分检测报告", "Composition Report"));
                if (builder == null)
                {
                    return;
                }

                detectWindow = builder.Handle;
                builder.SetSize(560f, 476f);
                builder.SetDraggable(true);
                builder.SetCloseOnEscape(true);

                BuildHeader(builder, sample, already);

                builder.BeginRow(6f, TextAnchor.MiddleLeft);
                LabUi.Label(builder,
                    Loc.Get("sample_components_header", "检出成分", "Detected components"),
                    null, 12f, 320f, 22f, false, ChemUi.Accent, true, TextAlignmentOptions.Left);
                builder.AddFlexibleSpace();
                LabUi.Label(builder,
                    Loc.Get("sample_impurities_header", "剩余为杂质", "The rest is impurities"),
                    null, 11f, 180f, 22f, false, ChemUi.Dim, false, TextAlignmentOptions.Right);
                builder.End();

                foreach (var component in sample.Components)
                {
                    BuildComponentRow(builder, sample, component, -1);
                }

                int impurities = 100 - SumPercent(sample);
                LabUi.Label(builder,
                    Loc.Get("sample_impurities_line", "杂质 {0}% · {1} g", "Impurities {0}% · {1} g",
                        impurities, ChemUi.MassText(sample.Grams * impurities / 100.0)),
                    null, 12f, 520f, 22f, false, ChemUi.Dim, false, TextAlignmentOptions.Left);

                LabUi.Label(builder,
                    Array.Exists(sample.Components, component => LabRefining.IsAcidTarget(sample, component.Symbol))
                        ? LabRefining.AcidInstructions
                        : Loc.Get("sample_report_distill_hint", "把蒸馏器拖到样品上，选择要分离的成分。",
                            "Drag the Distiller onto the sample and select a component to separate."),
                    null, 11.5f, 520f, 36f, true, ChemUi.Dim, false, TextAlignmentOptions.TopLeft);

                // 委托相关：这批料对本单够不够、缺什么、去哪个工位
                string advice = "";
                try
                {
                    advice = OrderService.SampleAdvice(sample);
                }
                catch (Exception e)
                {
                    ChemUi.Log?.Warning("读取委托提示失败：" + e.Message);
                }

                if (!string.IsNullOrEmpty(advice))
                {
                    LabUi.Label(builder, advice, null, 11.5f, 520f, 32f, true, ChemUi.Accent, true,
                        TextAlignmentOptions.TopLeft);
                }

                LabUi.Button(builder, Loc.Get("sample_close", "关闭", "Close"), CloseDetect, 110f, 30f, true, "");
                builder.Show();

                try
                {
                    detectWindow.BringToFront();
                }
                catch
                {
                    // 忽略
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("打开成分报告窗口失败：" + e);
            }
        }

        private static void BuildHeader(CustomUIBuilder builder, SampleDef sample, bool already)
        {
            builder.BeginRow(10f, TextAnchor.MiddleLeft);
            AddIcon(builder, sample.SpriteKey, 46f);
            builder.BeginColumn(2f, TextAnchor.MiddleLeft);
            LabUi.Label(builder, sample.DisplayName(true), null, 15f, 400f, 24f, false, ChemUi.Main, true,
                TextAlignmentOptions.Left);
            LabUi.Label(builder,
                already
                    ? Loc.Get("sample_report_already", "光谱仪复扫 · 检测记录已存在", "Rescanned · record already filed")
                    : Loc.Get("sample_report_fresh", "光谱仪扫描完成 · 已归档", "Spectrometer scan complete · filed"),
                null, 11.5f, 400f, 20f, false, ChemUi.Dim, false, TextAlignmentOptions.Left);
            builder.End();
            builder.End();
        }

        private static void BuildComponentRow(CustomUIBuilder builder, SampleDef sample,
            (string Symbol, int Percent) component, int rowIndex)
        {
            ElementDef element = ChemistryCatalog.FindElement(component.Symbol);
            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            if (rowIndex >= 0)
            {
                ChemUi.RowBackground(builder.CurrentContainer, rowIndex);
            }

            if (element != null)
            {
                AddIcon(builder, element.SpriteKey, 28f);
            }

            string name = element != null ? Loc.Pick(element.NameZh, element.NameEn) : component.Symbol;
            LabUi.Label(builder, name + "（" + component.Symbol + "）", null, 13f, 300f, 26f,
                false, ChemUi.Main, true, TextAlignmentOptions.MidlineLeft);
            builder.AddFlexibleSpace();
            LabUi.Label(builder,
                component.Percent + "% · " + ChemUi.MassText(sample.ComponentGrams(component.Symbol)) + " g",
                null, 14f, 150f, 26f,
                false, ChemUi.Accent, true, TextAlignmentOptions.Right);
            builder.End();
        }

        // ---------------------------------------------------------------
        // 提炼
        // ---------------------------------------------------------------

        public static void ShowRefine(SampleDef sample, string statusMessage)
        {
            if (sample == null)
            {
                return;
            }

            CloseRefine();

            try
            {
                CustomUIBuilder builder = CustomUIManager.Window(
                    RefineWindowId,
                    Loc.Get("sample_refine_title", "提炼", "Refining"));
                if (builder == null)
                {
                    return;
                }

                refineWindow = builder.Handle;
                builder.SetSize(640f, 486f);
                builder.SetDraggable(true);
                builder.SetCloseOnEscape(true);

                BuildRefineHeader(builder, sample);

                LabUi.Label(builder,
                    string.IsNullOrEmpty(statusMessage) ? " " : statusMessage,
                    "sample_refine_status", 12f, 580f, 30f, true, ChemUi.Accent, false,
                    TextAlignmentOptions.TopLeft);

                if (!LabStorage.Loaded)
                {
                    LabStorage.EnsureLoaded();
                }

                int available = AvailableCount(sample);
                int index = 0;
                foreach (var component in sample.Components)
                {
                    if (!LabRefining.IsAcidTarget(sample, component.Symbol))
                    {
                        BuildRefineRow(builder, sample, component, available, index++);
                    }
                }

                LabUi.Label(builder,
                    Loc.Get("sample_refine_hint",
                        "每次分离按成分含量扣除所需份数，收集 1 份产物。",
                        "Each separation consumes the required number of samples and collects one product."),
                    null, 11.5f, 580f, 36f, true, ChemUi.Dim, false, TextAlignmentOptions.TopLeft);

                builder.BeginRow(8f, TextAnchor.MiddleLeft);
                LabUi.Button(builder, Loc.Get("sample_refine_close", "关掉蒸馏器", "Close distiller"),
                    CloseRefine, 150f, 30f, true, "");
                builder.End();

                builder.Show();
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("打开提炼窗口失败：" + e);
            }
        }

        private static void BuildRefineHeader(CustomUIBuilder builder, SampleDef sample)
        {
            builder.BeginRow(10f, TextAnchor.MiddleLeft);
            AddIcon(builder, sample.SpriteKey, 44f);
            builder.BeginColumn(2f, TextAnchor.MiddleLeft);
            LabUi.Label(builder, sample.DisplayName(true), null, 15f, 440f, 24f, false, ChemUi.Main, true,
                TextAlignmentOptions.Left);
            int inLab = LabStorage.CountOf(sample.DetectedId);
            int inStore = StoreCount(sample.DetectedId);
            LabUi.Label(builder,
                Loc.Get("sample_refine_stock", "可提炼样品：实验室 {0} 份 · 店里 {1} 份",
                    "Analyzable samples: lab {0} · store {1}", inLab, inStore),
                null, 11.5f, 440f, 20f, false, ChemUi.Dim, false, TextAlignmentOptions.Left);
            builder.End();
            builder.End();
        }

        private static void BuildRefineRow(CustomUIBuilder builder, SampleDef sample,
            (string Symbol, int Percent) component, int available, int index)
        {
            ElementDef element = ChemistryCatalog.FindElement(component.Symbol);
            int need = LabRefining.SamplesPerProduct(component.Percent);
            bool enough = available >= need;

            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            ChemUi.RowBackground(builder.CurrentContainer, index);
            ChemUi.RowLayout(builder.CurrentContainer, 580f, 44f);
            if (element != null)
            {
                AddIcon(builder, element.SpriteKey, 28f);
            }

            string name = element != null ? Loc.Pick(element.NameZh, element.NameEn) : component.Symbol;
            LabUi.Label(builder,
                name + "（" + component.Symbol + "） " + component.Percent + "% · " +
                ChemUi.MassText(sample.ComponentGrams(component.Symbol)) + " g\n" +
                Loc.Get("sample_refine_cost", "耗样品 {0} 份 · 现有 {1} 份", "Cost {0} · have {1}", need, available),
                null, 12f, 380f, 42f, true, enough ? ChemUi.Main : ChemUi.Dim, false, TextAlignmentOptions.MidlineLeft);
            builder.AddFlexibleSpace();

            string message = Loc.Get("sample_refine_button", "提炼 ×1", "Refine x1");
            string tooltip = Loc.Get("sample_refine_tip",
                "消耗 {0} 份已检测样品，产出 {1} ×1（存入实验室储物格）",
                "Consume {0} analyzed samples to recover {1} x1 into lab storage",
                need, SampleTable.ElementName(component.Symbol));
            LabUi.Button(builder, message, () => DoRefine(sample, component.Symbol), 104f, 30f, enough, tooltip);
            builder.End();
        }

        private static void DoRefine(SampleDef sample, string symbol)
        {
            LabInventoryGrid grid = LabInventoryGrid.OpenGrid;
            CustomUIWindow origin = refineWindow;
            RefineReaction.TryStart(SampleTable.ElementName(symbol), 48,
                () => LabInventoryGrid.OpenGrid == grid,
                message =>
                {
                    if (refineWindow == origin) ChemUi.SetText(refineWindow, "sample_refine_status", message);
                },
                () => FinishRefine(sample, symbol, origin));
        }

        private static void FinishRefine(SampleDef sample, string symbol, CustomUIWindow origin)
        {
            string message = LabRefining.RefineAnywhere(sample, symbol);
            try
            {
                LabInventoryGrid.StorageSaved();
            }
            catch
            {
                // 储物格不可用时忽略
            }

            if (refineWindow == origin) ShowRefine(sample, message);
        }

        // ---------------------------------------------------------------
        // 关闭
        // ---------------------------------------------------------------

        public static void CloseDetect()
        {
            CustomUIWindow closing = detectWindow;
            detectWindow = null;
            if (closing != null && closing.IsAlive)
            {
                closing.Close();
            }
        }

        public static void CloseRefine()
        {
            CustomUIWindow closing = refineWindow;
            refineWindow = null;
            if (closing != null && closing.IsAlive)
            {
                closing.Close();
            }
        }

        public static void CloseAll()
        {
            CloseDetect();
            CloseRefine();
        }

        // ---------------------------------------------------------------
        // 小工具
        // ---------------------------------------------------------------

        private static int AvailableCount(SampleDef sample)
        {
            return LabStorage.CountOf(sample.DetectedId) + StoreCount(sample.DetectedId);
        }

        private static int StoreCount(string itemId)
        {
            try
            {
                var counts = StoreInventory.CountUnits();
                return counts != null && counts.TryGetValue(itemId, out int count) ? count : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static void AddIcon(CustomUIBuilder builder, string spriteKey, float size)
        {
            Sprite sprite = ChemUi.ModSprite(spriteKey);
            if (sprite == null)
            {
                return;
            }

            builder.AddImage(sprite, size, size, null);
            if (builder.Last?.image != null)
            {
                builder.Last.image.preserveAspect = true;
                builder.Last.image.raycastTarget = false;
            }
        }

        private static int SumPercent(SampleDef sample)
        {
            int total = 0;
            foreach (var component in sample.Components)
            {
                total += component.Percent;
            }

            return total;
        }
    }
}
