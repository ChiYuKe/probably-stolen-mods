using TMPro;

namespace Chemistry
{
    internal static partial class LabPanel
    {
        private static void BuildProcessGuide(CustomUIBuilder builder)
        {
            builder.AddLabel(" ", "lab_objective");
            ChemUi.Style(builder.Last, 15f, 790f, 46f, true, ChemUi.Accent, true, TextAlignmentOptions.MidlineLeft);

            builder.AddLabel(Loc.Pick("主线是把垃圾场废料拆成可用元素，再用配方和设备加工。设备带进地下实验室后双击打开；电动设备需装电池，状态行会提示缺料，产物从右侧输出格取出。",
                "The main loop turns dumping-ground scrap into usable elements, then processes them with recipes and equipment. Bring equipment into the underground lab and double-click it; powered devices need a battery. Status text shows missing inputs, and products appear in the right-hand output slots."));
            ChemUi.Style(builder.Last, 15f, 790f, 62f, false, ChemUi.Main, true, TextAlignmentOptions.MidlineLeft);

            GuideSection(builder, "一、主线回收：废料 → 单质", "1. Core recovery: scrap to elements");
            GuideLine(builder, "入库与扫描", "Deposit and scan",
                "在「储物格」把垃圾场的未知样品存入实验室；把光谱仪拖到样品上扫描，报告会列出成分比例与杂质。",
                "Move unknown samples from the shop into lab storage. Drag the Spectrometer onto a sample to read its composition and impurities.", 58f);
            GuideLine(builder, "选择分离路线", "Choose a separation route",
                "按报告选择目标：可直接分离的液体或气体用蒸馏器；反应台会显示酸浸目标对应的酸，按「打开制酸装置」制备，再把样品和酸装入反应台。",
                "Choose a target from the report. Use the Distiller for direct liquid or gas separation. The refining station shows the acid for each leaching target; use its Acid Station button to prepare it, then load the sample and acid.", 64f);
            GuideLine(builder, "过滤与还原", "Filter and reduce",
                "浸出液 → 过滤漏斗 → 滤液 → 还原槽。设备输出格会保留中间物；实验室「检测与提炼」页的接续按钮可把它送进下一台。回收的元素可用于合成或经营。",
                "Leachate → filter funnel → filtrate → reduction cell. Machine outputs keep intermediates; the follow-up buttons on the lab's Analysis & Refining page load them into the next station. Recovered elements can be used in recipes or sold.", 64f);

            GuideSection(builder, "二、分阶段扩产", "2. Scale production in stages");
            GuideLine(builder, "前期：手动制备", "Early game: manual batches",
                "逐台准备工具、试剂和样品。先完成一次检测与元素回收，再按机器状态提示补料；制酸装置可手动制备盐酸、硫酸、醋酸等试剂。",
                "Load tools, reagents and samples at each station. Complete one scan and recovery first, then follow each machine's status. The acid station prepares reagents such as hydrochloric, sulfuric and acetic acid.", 58f);
            GuideLine(builder, "中期：硝酸批量线", "Mid game: nitric acid batches",
                "硝酸线依次执行预处理、转化、过滤和灌装。施工槽可拆解 04/05 批焦化电路板，每件得到 2 个机件；也可花 18 信用点购买。4 个机件、1 瓶硝酸和 1 台蒸馏器可组装实验室模块，解锁每站最多 4 批并加快运行。",
                "The nitric line runs preparation, conversion, filtering and filling. Salvage batch 04/05 scorched circuit boards for 2 parts each, or buy parts for 18 credits. Build the lab module with 4 parts, 1 nitric acid and 1 distiller to queue up to 4 batches per station and speed up the line.", 76f);
            GuideLine(builder, "后期：硝酸线自动化", "Late game: automate the nitric line",
                "再用 8 个机件、2 个动力匣和 1 台高压釜组装管路模块。装入实验室模块与管路模块后，可切换自动运行；补充含氮样品、动力匣和空玻璃瓶，并设置成品库存上限。",
                "Build the manifold module with 8 parts, 2 power packs and 1 autoclave. With both modules installed, switch the nitric line to automatic and supply nitrogen samples, power packs and empty glass bottles; set a finished-stock limit.", 64f);
            GuideLine(builder, "第 12 天：自动化厂房", "Day 12: automated workshop",
                "第 12 天起可用 6 个机件组装厂房、用 3 个机件组装输送管路。安装离心机或控温搅拌器、干燥箱并供给动力匣，可自动回收悬浊液中的白垩，或从盐液中回收盐和水。",
                "From day 12, build the workshop with 6 parts and the transport manifold with 3. Install a centrifuge or hotplate stirrer, a drying oven and power packs to recover chalk from suspension or salt and water from brine.", 64f);

            GuideSection(builder, "三、其他设备练习", "3. Other equipment workflows");

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

            builder.AddButton(Loc.Pick("打开制酸装置", "Open acid station"), () => AcidStation.Open());
            builder.AddButton(Loc.Pick("打开硝酸生产线", "Open nitric production line"), () => NitricProduction.Open());
            builder.AddButton(Loc.Pick("自动化厂房 · 第 12 天", "Automated workshop · Day 12"), IndustrialFactory.Open);
        }

        /// <summary>流程指南顶部那一行「当前目标」，跟着委托进度变。</summary>
        private static void RefreshObjective()
        {
            if (!CanRefresh())
            {
                return;
            }

            string objective;
            try
            {
                objective = OrderService.NextObjective();
            }
            catch
            {
                objective = Loc.Pick("委托数据还没准备好。", "Order data is not ready.");
            }

            SetLabel("lab_objective",
                Loc.Get("lab_objective", "当前目标：{0}", "Current objective: {0}", objective),
                ChemUi.Accent);
        }

        private static void GuideSection(CustomUIBuilder builder, string titleZh, string titleEn)
        {
            builder.AddLabel(Loc.Pick(titleZh, titleEn));
            ChemUi.Style(builder.Last, 18f, 790f, 30f, true, ChemUi.Accent, false, TextAlignmentOptions.MidlineLeft);
        }

        private static void GuideLine(CustomUIBuilder builder, string titleZh, string titleEn, string detailZh, string detailEn,
            float detailHeight = 46f)
        {
            builder.AddLabel(Loc.Pick(titleZh, titleEn));
            ChemUi.Style(builder.Last, 16f, 790f, 26f, true, ChemUi.Main, false, TextAlignmentOptions.MidlineLeft);
            builder.AddLabel(Loc.Pick(detailZh, detailEn));
            ChemUi.Style(builder.Last, 14f, 790f, detailHeight, false, ChemUi.Dim, true, TextAlignmentOptions.MidlineLeft);
        }
    }
}
