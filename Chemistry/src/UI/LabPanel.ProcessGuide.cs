using TMPro;

namespace Chemistry
{
    internal static partial class LabPanel
    {
        private static void BuildProcessGuide(CustomUIBuilder builder)
        {
            builder.AddLabel(Loc.Pick("把设备带进地下实验室，双击打开。耗电设备需先装入电池，一次运行消耗几 kJ。产物从右侧取出，样品详情会提示下一步。",
                "Bring equipment into the underground lab and double-click it. Battery-powered devices need a charged battery; one run draws a few kJ. Take products from the right-hand slots. Sample details show the next step."));
            ChemUi.Style(builder.Last, 15f, 790f, 48f, false, ChemUi.Main, true, TextAlignmentOptions.MidlineLeft);

            GuideLine(builder, "量取与留样", "Measure and keep a sample",
                "量筒读混合样总量和配方。移液管设 25 mL，用空瓶从 50 mL 盐液中取样，余样留在另一槽。",
                "Measure batch volume and composition. Set the pipette to 25 mL and add an empty bottle to split a 50 mL brine sample into an aliquot and a remainder.");
            GuideLine(builder, "溶解、析晶与检测", "Dissolve, crystallize and test",
                "控温搅拌器可处理两份盐。低温留未溶盐，高温全溶。冷却后过滤，或用电导率仪测取出的盐样。",
                "The stirrer can process two salt units. Low heat leaves excess solid and high heat dissolves both. Cool and filter the crystals, or measure a brine aliquot.");
            GuideLine(builder, "萃取与洗涤", "Extraction and washing",
                "在烧杯加入小瓶示踪原液和实验用油。静置后分收油、水两相；取出的油相可加清水再洗一次。",
                "Combine a small tracer bottle with lab oil. Settle, collect both layers and wash the recovered oil once with fresh water.");
            GuideLine(builder, "集气", "Gas collection",
                "含气水样放进通风橱，排风设 2 档或以上，用空采样瓶或钢瓶收集。",
                "Collect gas from carbonated water in the fume hood at airflow 2 or higher. Use an empty gas vial or cylinder.");
            GuideLine(builder, "色谱、冻干与封装", "Chromatography, freeze-drying and sealing",
                "色谱仪可分收萃取油相的 A、B 馏分。冻干机回收干固体和冷阱水。手套箱可用小玻璃瓶封装或拆封固体。",
                "Collect fractions A and B from extracted oil. Freeze-dry a suspension to recover solid and cold-trap water. Use a small glass vial to seal a solid in the glovebox.");

            builder.AddButton(Loc.Pick("自动化厂房 · 第 12 天", "Automated workshop · Day 12"), IndustrialFactory.Open);
        }

        private static void GuideLine(CustomUIBuilder builder, string titleZh, string titleEn, string detailZh, string detailEn)
        {
            builder.AddLabel(Loc.Pick(titleZh, titleEn));
            ChemUi.Style(builder.Last, 16f, 790f, 26f, true, ChemUi.Main, false, TextAlignmentOptions.MidlineLeft);
            builder.AddLabel(Loc.Pick(detailZh, detailEn));
            ChemUi.Style(builder.Last, 14f, 790f, 46f, false, ChemUi.Dim, true, TextAlignmentOptions.MidlineLeft);
        }
    }
}
