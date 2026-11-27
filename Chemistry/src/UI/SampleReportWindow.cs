using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Chemistry
{
    /// <summary>Displays the composition report after a sample is analyzed in an instrument panel.</summary>
    internal static class SampleReportWindow
    {
        private const string DetectWindowId = "chemistry.sample_report";
        private static CustomUIWindow detectWindow;

        public static void ShowDetected(SampleDef sample, bool already)
        {
            if (sample == null) return;
            CloseDetect();

            try
            {
                CustomUIBuilder builder = CustomUIManager.Window(
                    DetectWindowId,
                    Loc.Get("sample_report_title", "成分检测报告", "Composition Report"));
                if (builder == null) return;

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
                    BuildComponentRow(builder, sample, component);

                int impurities = 100 - SumPercent(sample);
                int impurityGrams = (int)(((long)sample.Grams * impurities + 50) / 100);
                LabUi.Label(builder,
                    Loc.Get("sample_impurities_line", "杂质 {0}% · {1} g", "Impurities {0}% · {1} g",
                        impurities, ChemUi.MassText(impurityGrams)),
                    null, 12f, 520f, 22f, false, ChemUi.Dim, false, TextAlignmentOptions.Left);

                LabUi.Label(builder, RecoveryGuidance(sample), null, 11.5f, 520f, 58f,
                    true, ChemUi.Dim, false, TextAlignmentOptions.TopLeft);

                string advice = "";
                try { advice = OrderService.SampleAdvice(sample); }
                catch (Exception error) { ChemUi.Log?.Warning("读取委托提示失败：" + error.Message); }
                if (!string.IsNullOrEmpty(advice))
                    LabUi.Label(builder, advice, null, 11.5f, 520f, 32f, true, ChemUi.Accent, true,
                        TextAlignmentOptions.TopLeft);

                LabUi.Button(builder, Loc.Get("sample_close", "关闭", "Close"), CloseDetect, 110f, 30f, true, "");
                builder.Show();
                try { detectWindow.BringToFront(); }
                catch { }
            }
            catch (Exception error)
            {
                ChemUi.Log?.Warning("打开成分报告窗口失败：" + error);
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
                    ? Loc.Get("sample_report_already", "仪器复检 · 检测记录已存在", "Instrument rescan · record already filed")
                    : Loc.Get("sample_report_fresh", "仪器检测完成 · 已归档", "Instrument analysis complete · filed"),
                null, 11.5f, 400f, 20f, false, ChemUi.Dim, false, TextAlignmentOptions.Left);
            builder.End();
            builder.End();
        }

        private static void BuildComponentRow(CustomUIBuilder builder, SampleDef sample,
            (string Symbol, int Percent) component)
        {
            ElementDef element = ChemistryCatalog.FindElement(component.Symbol);
            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            if (element != null) AddIcon(builder, element.SpriteKey, 28f);
            string name = element != null ? Loc.Pick(element.NameZh, element.NameEn) : component.Symbol;
            LabUi.Label(builder, name + "（" + component.Symbol + "）", null, 13f, 300f, 26f,
                false, ChemUi.Main, true, TextAlignmentOptions.MidlineLeft);
            builder.AddFlexibleSpace();
            LabUi.Label(builder,
                component.Percent + "% · " + ChemUi.MassText(sample.ComponentGrams(component.Symbol)) + " g",
                null, 14f, 150f, 26f, false, ChemUi.Accent, true, TextAlignmentOptions.Right);
            builder.End();
        }

        public static void CloseDetect()
        {
            CustomUIWindow closing = detectWindow;
            detectWindow = null;
            if (closing != null && closing.IsAlive) closing.Close();
        }

        public static void CloseAll() => CloseDetect();

        private static void AddIcon(CustomUIBuilder builder, string spriteKey, float size)
        {
            Sprite sprite = ChemUi.ModSprite(spriteKey);
            if (sprite == null) return;
            builder.AddImage(sprite, size, size, null);
            if (builder.Last?.image != null)
            {
                builder.Last.image.preserveAspect = true;
                builder.Last.image.raycastTarget = false;
            }
        }

        private static int SumPercent(SampleDef sample) => sample.Components.Sum(component => component.Percent);

        private static string RecoveryGuidance(SampleDef sample)
        {
            string guidance = "";
            bool hasAcidTarget = sample.Components.Any(component => LabRefining.IsAcidTarget(sample, component.Symbol));
            bool hasDirectTarget = sample.Batch != "09"
                && sample.Components.Any(component => !LabRefining.IsAcidTarget(sample, component.Symbol));
            if (hasAcidTarget) guidance = LabRefining.AcidInstructions;
            if (hasDirectTarget)
            {
                if (guidance.Length > 0) guidance += "\n";
                guidance += Loc.Get("sample_report_distill_hint",
                    "直接回收：按成分配方将足量已检测样品放入蒸馏器，选择回收目标并从产物槽领取单质。",
                    "Direct recovery: load the recipe amount of analyzed sample into the distiller, select a target and collect the element from its output.");
            }
            if (sample.SpriteKey == "sample_liquid" || sample.SpriteKey == "sample_gas")
            {
                if (guidance.Length > 0) guidance += "\n";
                guidance += Loc.Pick("液体 / 气体分离：放入样品和空玻璃接收瓶，按配方选择温区。",
                    "Liquid / gas separation: load the sample and empty glass receiver, then choose a temperature band.");
            }
            return guidance;
        }
    }
}
