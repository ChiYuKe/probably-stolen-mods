using System.Collections.Generic;
using System.Linq;

namespace Chemistry
{
    internal enum EquipmentTask
    {
        Weigh, Volume, Dispense, Filter, Distill, AnalyzeSolid, AnalyzeFluid, AnalyzeAny,
        Acidity, Conductivity, Settings, GlasswareCycle, WaterCycle, SolidCycle,
        Mix, Settle, SeparateLayers, Clarify, Dry, Titrate, GasCollect, FreezeDry, Seal
    }

    internal sealed class LabEquipmentDef
    {
        internal readonly string Key;
        internal readonly int Tier;
        internal readonly EquipmentTask Task;
        internal readonly string ActionZh;
        internal readonly string ActionEn;
        internal readonly string SettingZh;
        internal readonly string SettingEn;
        internal readonly int PowerKj;

        internal LabEquipmentDef(string key, int tier, EquipmentTask task, string actionZh,
            string actionEn, string settingZh = null, string settingEn = null, int powerKj = 0)
        {
            Key = key;
            Tier = tier;
            Task = task;
            ActionZh = actionZh;
            ActionEn = actionEn;
            SettingZh = settingZh;
            SettingEn = settingEn;
            PowerKj = powerKj;
        }

        internal string ActionLabel => Loc.Pick(ActionZh, ActionEn);
        internal string SettingLabel => Loc.Pick(SettingZh, SettingEn);
        internal bool Adjustable => SettingZh != null || Task == EquipmentTask.Dispense || Task == EquipmentTask.Titrate;
        internal bool HasOutput => Task == EquipmentTask.Filter || Task == EquipmentTask.Distill || IsAnalyzer
            || Task == EquipmentTask.Mix || Task == EquipmentTask.Settle || Task == EquipmentTask.SeparateLayers
            || Task == EquipmentTask.Clarify || Task == EquipmentTask.Dry || Task == EquipmentTask.Titrate
            || Task == EquipmentTask.GasCollect || Task == EquipmentTask.FreezeDry || Task == EquipmentTask.Seal;
        internal bool IsAnalyzer => Task == EquipmentTask.AnalyzeSolid || Task == EquipmentTask.AnalyzeFluid || Task == EquipmentTask.AnalyzeAny;
        internal int CycleTicks => Adjustable ? 32 : Tier == 1 ? 22 : 48;
    }

    internal static class LabEquipmentData
    {
        internal static readonly ToolDef[] NewTools =
        {
            new ToolDef { Key = "conductivity_meter", NameZh = "电导率仪", NameEn = "Conductivity meter", Category = "measurement", Value = 210, DescZh = "探头接入台式读数仪。", DescEn = "A benchtop meter with a wired probe." },
            new ToolDef { Key = "ultrasonic_cleaner", NameZh = "超声波清洗机", NameEn = "Ultrasonic cleaner", Category = "misc", Value = 240, DescZh = "不锈钢清洗槽，适合小型器皿。", DescEn = "A stainless bath for small glassware." },
            new ToolDef { Key = "drying_oven", NameZh = "实验室干燥箱", NameEn = "Drying oven", Category = "heating", Value = 275, DescZh = "有观察窗和多层托盘的干燥箱。", DescEn = "A drying chamber with a viewing window and stacked trays." },
            new ToolDef { Key = "rotary_evaporator", NameZh = "旋转蒸发仪", NameEn = "Rotary evaporator", Category = "separation", Value = 300, DescZh = "旋转烧瓶旁装有冷凝回收组件。", DescEn = "A rotating flask with a condenser and collection assembly." },
            new ToolDef { Key = "freeze_dryer", NameZh = "冷冻干燥机", NameEn = "Freeze dryer", Category = "separation", Value = 330, DescZh = "真空干燥舱配有独立冷阱。", DescEn = "A vacuum drying chamber with a separate cold trap." },
            new ToolDef { Key = "glovebox", NameZh = "密闭手套箱", NameEn = "Glovebox", Category = "safety", Value = 350, DescZh = "密封观察窗下有一对手套口。", DescEn = "A sealed viewing chamber with two glove ports." },
        };

        internal static readonly LabEquipmentDef[] All =
        {
            new LabEquipmentDef("beaker", 1, EquipmentTask.Mix, "配制混合样", "Mix sample"),
            new LabEquipmentDef("conical_flask", 1, EquipmentTask.Settle, "静置 / 析晶", "Settle / crystallize"),
            new LabEquipmentDef("graduated_cylinder", 1, EquipmentTask.Volume, "读取刻度", "Read scale"),
            new LabEquipmentDef("pipette", 1, EquipmentTask.Dispense, "定量移液", "Dispense"),
            new LabEquipmentDef("mortar_pestle", 1, EquipmentTask.SolidCycle, "研磨", "Grind", "研磨", "Grinding"),
            new LabEquipmentDef("bunsen_burner", 1, EquipmentTask.Settings, "测试火力", "Test burner", "火力", "Flame"),
            new LabEquipmentDef("balance", 1, EquipmentTask.Weigh, "称量", "Weigh", powerKj: 1),
            new LabEquipmentDef("filter_funnel", 1, EquipmentTask.Filter, "过滤", "Filter"),

            new LabEquipmentDef("burette", 2, EquipmentTask.Dispense, "定量滴加", "Dispense"),
            new LabEquipmentDef("ph_meter", 2, EquipmentTask.Acidity, "检查酸碱性", "Check acidity", powerKj: 2),
            new LabEquipmentDef("conductivity_meter", 2, EquipmentTask.Conductivity, "检查导电性", "Check conductivity", powerKj: 2),
            new LabEquipmentDef("centrifuge", 2, EquipmentTask.Clarify, "离心分离", "Centrifuge", powerKj: 5),
            new LabEquipmentDef("distiller", 2, EquipmentTask.Distill, "蒸发并冷凝", "Evaporate and condense", "温区", "Temperature band", powerKj: 6),
            new LabEquipmentDef("separating_funnel", 2, EquipmentTask.SeparateLayers, "分收上下层", "Collect both layers"),
            new LabEquipmentDef("hotplate_stirrer", 2, EquipmentTask.Mix, "控温搅拌", "Temperature-controlled stirring", "温区", "Temperature band", powerKj: 4),
            new LabEquipmentDef("heating_mantle", 2, EquipmentTask.WaterCycle, "加热测试", "Test heating", "火力", "Heat", powerKj: 4),
            new LabEquipmentDef("fume_hood", 2, EquipmentTask.GasCollect, "排风 / 收集气体", "Ventilate / collect gas", "风量", "Airflow", powerKj: 3),
            new LabEquipmentDef("ultrasonic_cleaner", 2, EquipmentTask.GlasswareCycle, "清洗器皿", "Clean glassware", "清洗强度", "Cleaning intensity", powerKj: 3),
            new LabEquipmentDef("drying_oven", 2, EquipmentTask.Dry, "干燥回收", "Dry and recover", powerKj: 8),
            new LabEquipmentDef("microscope", 2, EquipmentTask.AnalyzeSolid, "观察固体", "Inspect solid", powerKj: 2),

            new LabEquipmentDef("titration_set", 3, EquipmentTask.Titrate, "滴加并判定终点", "Titrate and check endpoint"),
            new LabEquipmentDef("spectrometer", 3, EquipmentTask.AnalyzeAny, "扫描成分", "Scan sample", powerKj: 6),
            new LabEquipmentDef("chromatograph", 3, EquipmentTask.AnalyzeFluid, "分析流体", "Analyze fluid", powerKj: 6),
            new LabEquipmentDef("rotary_evaporator", 3, EquipmentTask.Distill, "减压冷凝回收", "Recover under vacuum", "温区", "Temperature band", powerKj: 6),
            new LabEquipmentDef("freeze_dryer", 3, EquipmentTask.FreezeDry, "冻干并收集冷阱水", "Freeze-dry and recover water", powerKj: 10),
            new LabEquipmentDef("autoclave", 3, EquipmentTask.GlasswareCycle, "器皿灭菌", "Sterilize glassware", "循环强度", "Intensity", powerKj: 8),
            new LabEquipmentDef("glovebox", 3, EquipmentTask.Seal, "隔离封装 / 拆封", "Seal / reopen", powerKj: 3),
        };

        private static readonly Dictionary<string, LabEquipmentDef> ByKey = All.ToDictionary(def => def.Key);

        internal static LabEquipmentDef Find(string key) => ByKey.TryGetValue(key, out var def) ? def : null;

        internal static string OperationGuide(LabEquipmentDef equipment)
        {
            if (equipment == null) return Loc.Pick("没有单独的操作面板。合成配方需要这件器材时，按合成台提示放入地下实验室储物格；工具不会被合成消耗。", "It has no separate operation panel. If a recipe requires it, follow the synthesis panel and keep it in lab storage; crafting does not consume the tool.");

            string open = Loc.Pick("在地下实验室储物格中双击仪器打开面板。", "In the underground lab, double-click the instrument in storage to open its panel.");
            string operation = equipment.Key switch
            {
                "beaker" => Loc.Pick("将可处理的溶液放入原料槽；需要加料时，把固体或液体放入加料槽，再启动配方。", "Load a supported solution in the ingredient slot. Add a solid or liquid in the second slot when needed, then run the recipe."),
                "conical_flask" => Loc.Pick("放入悬浊液、分层样或盐液并启动静置/析晶；完成后按面板提示收取沉降物或晶体。", "Load a suspension, layered sample or brine and start settling or crystallization. Collect the sediment or crystals when prompted."),
                "graduated_cylinder" => Loc.Pick("把液体容器放入原料槽并启动，面板会显示当前体积。", "Load a liquid container in the ingredient slot and start to read its volume."),
                "pipette" => Loc.Pick("把液体样品放入原料槽、空瓶或相容液体瓶放入接收槽；用左右箭头选取体积后启动。盐液可取样并留下余样。", "Load a liquid sample and an empty or compatible liquid bottle in the receiver slot. Set the volume with the arrows and start. Brine can be sampled while retaining the remainder."),
                "mortar_pestle" => Loc.Pick("把固体样品放入原料槽，选择研磨强度并启动。", "Load a solid sample, choose the grinding level and start."),
                "bunsen_burner" => Loc.Pick("无需投料；选择火力档后启动测试。", "No sample is needed. Choose a flame level and start the test."),
                "balance" => Loc.Pick("把待称物放入原料槽并启动，读数显示在面板中。", "Load an item in the ingredient slot and start; its reading appears in the panel."),
                "filter_funnel" => Loc.Pick("装入待过滤样品；若处理示踪液，还要把活性炭装入吸附剂槽。启动后从产物槽取滤液，副产物槽取滤渣或废炭。", "Load the sample. For tracer liquid, also place activated carbon in the adsorbent slot. Start, then collect filtrate and retained solids or spent carbon from the output slots."),
                "burette" => Loc.Pick("把液体放入原料槽、空瓶或相容液体瓶放入接收槽；选择滴加量后启动，液体会按设定量转移。", "Load a liquid in the ingredient slot and an empty or compatible liquid bottle in the receiver. Set the dose and start to transfer that amount."),
                "ph_meter" => Loc.Pick("把酸、碱或滴定样品放入原料槽并启动；读数会显示在面板中。", "Load an acid, base or titration sample and start; the reading appears in the panel."),
                "conductivity_meter" => Loc.Pick("把溶液或盐液取样放入原料槽并启动，查看电导读数。", "Load a solution or brine aliquot and start to read its conductivity."),
                "centrifuge" => Loc.Pick("放入悬浊液、沉降液或可离心样品并启动；从产物槽取澄清液，从副产物槽取沉淀。", "Load a suspension, settled sample or supported mixture and start. Collect clear liquid from the output and sediment from the byproduct slot."),
                "distiller" => Loc.Pick("分离液体时放入样品和空玻璃接收瓶，选择温区后启动，并从产物槽收集馏分。回收已检测样品时，选择非酸浸目标，按所需份数放入样品；启动后从产物槽领取单质。需要酸浸的目标放入提炼反应台。", "For liquid separation, load the sample and an empty glass receiver, choose a temperature band and collect the fraction from the output. To recover an element from analyzed scrap, select a non-acid target, load the required sample count and collect the element from the output. Use the refining station for acid-leachable targets."),
                "separating_funnel" => Loc.Pick("放入已分层的样品，在上层接收瓶槽放空玻璃瓶后启动；油相装入接收瓶，下层水相进入另一产物槽。", "Load a layered sample, place an empty glass bottle in the upper-layer receiver slot and start. The oil enters the bottle and the lower water phase goes to the other output slot."),
                "hotplate_stirrer" => Loc.Pick("把溶液放入原料槽、固体或液体放入加料槽，选择温区后启动混合；按提示从产物槽取回溶液。", "Load a solution and any solid or liquid ingredient in their labeled slots, choose a temperature band and start mixing. Collect the solution from the output as prompted."),
                "heating_mantle" => Loc.Pick("以水样作为加热测试物，选择火力后启动；面板会显示加热结果。", "Use water as the heating test sample, choose a heat level and start; the result appears in the panel."),
                "fume_hood" => Loc.Pick("放入含气样品；要收集气体时，在接收槽放空采样瓶或钢瓶，把风量调至 2 档以上后启动。", "Load a gas-bearing sample. To collect gas, place an empty vial or cylinder in the receiver, set airflow to level 2 or higher and start."),
                "ultrasonic_cleaner" => Loc.Pick("将可清洗的玻璃器皿放入原料槽，选择清洗强度并启动。", "Load cleanable glassware in the ingredient slot, choose cleaning intensity and start."),
                "drying_oven" => Loc.Pick("放入湿滤渣或湿盐晶并启动；从产物槽取干燥后的固体。", "Load wet filter cake or salt crystals and start. Collect the dried solid from the output."),
                "microscope" => Loc.Pick("把固体样品放入原料槽并启动，查看观察结果。", "Load a solid sample and start to view the observation result."),
                "titration_set" => Loc.Pick("把滴定样品放入原料槽、标准液放入标准液槽；用箭头设置滴加量并启动。结果保存在输出瓶中，可用 pH 计复测。", "Load the titration sample and standard solution in their labeled slots. Set the dose with the arrows and start. The result stays in the output bottle and can be checked with a pH meter."),
                "spectrometer" => Loc.Pick("把待测样品放入原料槽并启动；测液体样品时，在空白对照槽放入水。检测废料后打开成分报告选择回收目标。", "Load the sample and start. For optical liquid samples, place water in the blank slot. After scanning scrap, open its report to choose a recovery target."),
                "chromatograph" => Loc.Pick("把液体样品放入原料槽、空玻璃瓶放入接收槽并启动；从输出槽和副产物槽分别收取 A、B 馏分。", "Load a liquid sample and an empty glass receiver, then start. Collect fractions A and B from the output and byproduct slots."),
                "rotary_evaporator" => Loc.Pick("放入待回收液体和空玻璃接收瓶，选择温区并启动；设备在减压下蒸发，馏分进入接收瓶。", "Load the liquid and an empty glass receiver, choose the temperature band and start. The unit evaporates under vacuum and sends condensate to the receiver."),
                "freeze_dryer" => Loc.Pick("放入悬浊液或析晶样，并在接收槽放空玻璃瓶；启动后从产物槽取干燥固体，从接收瓶取冷阱水。", "Load a suspension or crystallized sample and place an empty glass bottle in the receiver. Collect the dried solid and recovered cold-trap water."),
                "autoclave" => Loc.Pick("把可灭菌的玻璃器皿放入原料槽，选择循环强度并启动。", "Load sterilizable glassware, choose cycle intensity and start."),
                "glovebox" => Loc.Pick("封装时把盐或碳酸钙放入原料槽、空玻璃瓶放入接收槽；拆封时装入已封装样品。启动后从产物槽取回封装品或固体。", "To seal, load salt or calcium carbonate in the ingredient slot and an empty glass bottle in the receiver. To reopen, load a sealed sample. Start and collect the sealed item or solid from the output."),
                _ => Loc.Pick("放入面板支持的样品，查看原料槽和接收槽提示，选择参数后启动并取走产物。", "Load a supported sample, follow the sample and receiver slot prompts, choose settings, start and collect the output.")
            };
            string power = equipment.PowerKj > 0
                ? Loc.Get("encyclopedia_equipment_power", "耗电 {0} kJ；运行前把电池装入供能槽。", "Uses {0} kJ; insert a battery in the power slot before running.", equipment.PowerKj)
                : "";
            return open + operation + (power.Length == 0 ? "" : "\n" + power);
        }

        internal static string ProcessFlow(LabEquipmentDef equipment)
        {
            if (equipment == null)
                return Loc.Pick("1. 按对应配方准备器材和原料。\n2. 在地下实验室储物格使用所需工具；配方完成后收取产物。",
                    "1. Prepare the tool and ingredients required by the recipe.\n2. Use the tool from underground lab storage, then collect the product when crafting finishes.");

            string flow;
            switch (equipment.Key)
            {
                case "chromatograph":
                    flow = Loc.Pick(
                        "1. 制备待分段样：将示踪液与等量水放入烧杯混合，再用锥形瓶静置或离心机分层；用分液漏斗收集萃取油相。\n"
                        + "2. 将两份萃取油相（共 50 mL）放入原料槽，空玻璃瓶放入接收槽。普通液体或气体也可放入检测，但只有萃取油相会生成 A、B 馏分。\n"
                        + "3. 装入电池并启动色谱仪；峰 A 先出、峰 B 后出，各收集 25 mL。\n"
                        + "4. 从产物槽取 A 馏分，从副产物槽取 B 馏分；可用移液管取样，再用光谱仪检测或按样品说明继续处理。",
                        "1. Prepare a sample for fractionation: mix tracer and an equal portion of water in a beaker, settle it in a conical flask or centrifuge, then collect the oil phase with a separatory funnel.\n"
                        + "2. Load two portions of extracted oil (50 mL total) in the ingredient slot and an empty glass bottle in the receiver. Other liquids and gases can be read, but only extracted oil produces A and B fractions.\n"
                        + "3. Insert a battery and start the chromatograph. Peak A comes first, then peak B; each fraction is 25 mL.\n"
                        + "4. Take A from the output and B from the byproduct slot. Use a pipette for aliquots, then analyze with the spectrometer or follow the sample entry.");
                    break;
                case "spectrometer":
                    flow = Loc.Pick(
                        "1. 将待测物放入原料槽；检测光学液体或萃取样时，在空白对照槽放水。\n"
                        + "2. 装入电池并启动扫描，读取面板结果。\n"
                        + "3. 未知废料扫描后打开生成的成分报告并选择目标；有可酸浸成分时走“酸浸 → 过滤 → 还原”，液体或气体按报告用蒸馏器分离。",
                        "1. Load the sample. For optical liquids or extracts, place water in the blank slot.\n"
                        + "2. Insert a battery, run the scan and read the panel result.\n"
                        + "3. After scanning unknown scrap, open its composition report and choose a target. Acid-leachable components go through leaching, filtration and reduction; separate liquids or gases with the distiller as directed.");
                    break;
                case "distiller":
                    flow = Loc.Pick(
                        "1. 液体分离：将可蒸馏样品放入原料槽，将空玻璃瓶放入接收槽并选择温区。\n"
                        + "2. 启动后从产物槽收集冷凝馏分；余液留在副产物槽，按报告继续处理。\n"
                        + "3. 回收已检测样品：放入配方要求数量的样品，用“回收目标”按钮选择可直接回收的成分，启动后从产物槽取出单质。酸浸目标按报告放入提炼反应台。",
                        "1. For liquid separation, load a supported sample and an empty glass receiver, then choose a temperature band.\n"
                        + "2. Start the recipe and collect the condensate from the output; the residue stays in the byproduct slot for further processing.\n"
                        + "3. To recover an element from analyzed scrap, load the required sample count, choose a direct recovery target and collect the element from the output. Use the refining station for acid-leachable targets.");
                    break;
                default:
                    flow = equipment.Task switch
                    {
                        EquipmentTask.Weigh => Loc.Pick("1. 将待称物放入原料槽。\n2. 启动后读取质量；称量只返回读数，不消耗样品。", "1. Load the item in the ingredient slot.\n2. Start and read its mass; weighing returns a reading without consuming the sample."),
                        EquipmentTask.Volume => Loc.Pick("1. 将液体瓶或流程样品放入原料槽。\n2. 启动后读取体积；需要转移时再用移液管或滴定管分装。", "1. Load a liquid bottle or process sample in the ingredient slot.\n2. Start and read its volume; use a pipette or burette to transfer it."),
                        EquipmentTask.Dispense => Loc.Pick("1. 将来源液体放入原料槽，将空瓶或相容接收瓶放入接收槽。\n2. 设置转移量并启动；目标体积进入接收瓶，剩余液体留在来源瓶。", "1. Load the source liquid and an empty or compatible receiver.\n2. Set the transfer amount and start; the measured dose enters the receiver and the remainder stays in the source."),
                        EquipmentTask.Filter or EquipmentTask.Clarify => Loc.Pick("1. 将悬浊液、浸出液或可处理混合样放入原料槽；脱色时另放活性炭。\n2. 启动过滤或澄清；从产物槽收滤液，从副产物槽收滤渣或废炭。\n3. 湿固体可送干燥箱；浸出滤液可继续还原。", "1. Load a suspension, leachate or supported mixture; add activated carbon when decolorizing.\n2. Start filtration or clarification; collect filtrate from the output and solids or spent carbon from the byproduct slot.\n3. Dry wet solids or send leachate to reduction."),
                        EquipmentTask.Distill => Loc.Pick("1. 将可蒸馏样品放入原料槽，空玻璃瓶放入接收槽并选择温区。\n2. 启动后收集冷凝馏分；剩余样品按报告继续蒸馏或转入下一步。", "1. Load a supported sample, an empty glass receiver and choose a temperature band.\n2. Start and collect the condensate; continue distilling the residue or follow its report for the next step."),
                        EquipmentTask.AnalyzeSolid => Loc.Pick("1. 将固体样品放入原料槽并启动观察。\n2. 查看面板结果；需进一步鉴定时改用光谱仪扫描并查看成分报告。", "1. Load a solid sample and start the inspection.\n2. Read the panel result; use the spectrometer and its composition report for further identification."),
                        EquipmentTask.AnalyzeFluid => Loc.Pick("1. 将液体、气体或流程样品放入原料槽，按面板要求放入空玻璃接收瓶。\n2. 装入电池并启动检测；读取结果，或收取设备生成的馏分。\n3. 对未知废料打开成分报告并选择后续回收目标。", "1. Load a liquid, gas or process sample and place an empty glass receiver if the panel requests one.\n2. Insert a battery and run the analysis; read the result or collect generated fractions.\n3. Open the composition report for unknown scrap and choose a recovery target."),
                        EquipmentTask.AnalyzeAny => Loc.Pick("1. 将元素、试剂或样品放入原料槽；检测光学液体时在空白对照槽放水。\n2. 装入电池并启动，查看读数或成分报告。\n3. 对废料选择目标，再按报告转入提炼反应台或蒸馏器。", "1. Load an element, reagent or sample; add a water blank for optical liquids.\n2. Insert a battery, start and read the result or composition report.\n3. Choose a scrap target, then continue at the refining station or distiller as directed."),
                        EquipmentTask.Acidity => Loc.Pick("1. 将水样、酸碱试剂或滴定样品放入原料槽。\n2. 启动并读取酸碱性；需要定量中和时转入滴定装置。", "1. Load water, an acid or base, or a titration sample.\n2. Start and read the acidity; use the titration set for a measured neutralization."),
                        EquipmentTask.Conductivity => Loc.Pick("1. 将溶液或盐液取样放入原料槽，水可作为低导电参照。\n2. 启动并读取电导；读数高于水参照表示样品含可导电溶质。", "1. Load a solution or brine aliquot; water serves as a low-conductivity reference.\n2. Start and read conductivity; a higher reading indicates dissolved conductive material."),
                        EquipmentTask.Settings => Loc.Pick("1. 选择火力或设备档位。\n2. 启动测试并读取面板结果；此测试无需投料。", "1. Choose the flame or device setting.\n2. Start the test and read the panel result; no sample is used."),
                        EquipmentTask.GlasswareCycle => Loc.Pick("1. 将可处理的空玻璃器皿放入原料槽并选择循环强度。\n2. 启动清洗或灭菌，完成后取回器皿。", "1. Load cleanable empty glassware and choose the cycle intensity.\n2. Start cleaning or sterilization and collect the glassware when complete."),
                        EquipmentTask.WaterCycle => Loc.Pick("1. 将水样放入原料槽并选择火力。\n2. 启动加热测试，读取结果；该步骤不制备试剂。", "1. Load water and choose a heat level.\n2. Start the heating test and read its result; it does not produce a reagent."),
                        EquipmentTask.SolidCycle => Loc.Pick("1. 将固体放入原料槽并选择研磨强度。\n2. 启动研磨，完成后取回粉碎样品。", "1. Load a solid and choose the grinding level.\n2. Start grinding and collect the processed sample."),
                        EquipmentTask.Mix => Loc.Pick("1. 将溶液放入原料槽，需要加料时把固体或液体放入加料槽。\n2. 选择温区或搅拌设置后启动，取回混合液。\n3. 有沉淀或分层时继续静置、过滤或分液。", "1. Load a solution and any solid or liquid ingredient.\n2. Choose the temperature or mixing setting, start and collect the mixture.\n3. Settle, filter or separate it if solids or layers form."),
                        EquipmentTask.Settle => Loc.Pick("1. 将悬浊液、分层样或盐液放入原料槽。\n2. 启动静置或析晶；完成后按结果收取沉降物、晶体或分层样。", "1. Load a suspension, layered sample or brine.\n2. Start settling or crystallization, then collect the sediment, crystals or layered sample."),
                        EquipmentTask.SeparateLayers => Loc.Pick("1. 将已分层样品放入原料槽，空玻璃瓶放入上层接收槽。\n2. 启动分液；分别收集上层油相和产物槽中的下层水相。", "1. Load a layered sample and an empty glass bottle in the upper-layer receiver.\n2. Start separation and collect the upper oil and lower water phases separately."),
                        EquipmentTask.Dry => Loc.Pick("1. 将湿滤渣或湿盐晶放入原料槽。\n2. 启动干燥并从产物槽收取干燥固体。", "1. Load wet filter cake or salt crystals.\n2. Start drying and collect the dry solid from the output."),
                        EquipmentTask.Titrate => Loc.Pick("1. 将滴定样品放入原料槽，标准液放入标准液槽。\n2. 设置滴加量并启动；从输出瓶取回处理后的样品，再用 pH 计复测。", "1. Load the titration sample and standard solution in their labeled slots.\n2. Set the dose and start; collect the treated sample from the output bottle and recheck it with the pH meter."),
                        EquipmentTask.GasCollect => Loc.Pick("1. 将含气样品放入原料槽，空采样瓶或钢瓶放入接收槽，风量调至 2 档以上。\n2. 启动收集；从气体输出和副产物槽分别取气体与脱气液。", "1. Load a gas-bearing sample, an empty vial or cylinder, and set airflow to level 2 or higher.\n2. Start collection; collect gas and degassed liquid from their separate output slots."),
                        EquipmentTask.FreezeDry => Loc.Pick("1. 将悬浊液或析晶样放入原料槽，空玻璃瓶放入接收槽。\n2. 启动冻干；从产物槽取干燥固体，从接收瓶取冷阱水。", "1. Load a suspension or crystallized sample and an empty glass receiver.\n2. Start freeze-drying; collect the dry solid and cold-trap water separately."),
                        EquipmentTask.Seal => Loc.Pick("1. 封装时放入盐或碳酸钙及空玻璃瓶；拆封时放入已封装样品。\n2. 启动隔离舱，按操作类型从产物槽收取封装品或回收固体。", "1. To seal, load salt or calcium carbonate and an empty glass bottle; to reopen, load the sealed sample.\n2. Start the glovebox cycle and collect the sealed item or recovered solid from the output."),
                        _ => Loc.Pick("1. 按上方操作说明准备样品、试剂和接收容器。\n2. 选择设备参数并启动；从产物槽或副产物槽取回结果，再按物品说明继续处理。",
                            "1. Prepare the sample, reagent and receiver as described above.\n2. Choose the device setting and start; collect the output and byproduct, then follow the item entry for further handling.")
                    };
                    break;
            }

            if (equipment.PowerKj > 0)
                flow += "\n" + Loc.Get("encyclopedia_equipment_power", "本次运行耗电 {0} kJ；开始前检查供能槽电池。", "This run uses {0} kJ; check the power slot before starting.", equipment.PowerKj);
            return flow;
        }
    }
}
