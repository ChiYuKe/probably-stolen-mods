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

        internal LabEquipmentDef(string key, int tier, EquipmentTask task, string actionZh,
            string actionEn, string settingZh = null, string settingEn = null)
        {
            Key = key;
            Tier = tier;
            Task = task;
            ActionZh = actionZh;
            ActionEn = actionEn;
            SettingZh = settingZh;
            SettingEn = settingEn;
        }

        internal int UnlockDay => Tier == 1 ? 1 : Tier == 2 ? 5 : 12;
        internal string TierLabel => Tier == 1 ? Loc.Pick("前期", "Early")
            : Tier == 2 ? Loc.Pick("中期", "Midgame") : Loc.Pick("后期", "Late");
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
            new ToolDef { Key = "rotary_evaporator", NameZh = "旋转蒸发仪", NameEn = "Rotary evaporator", Category = "separation", Value = 450, DescZh = "旋转烧瓶旁装有冷凝回收组件。", DescEn = "A rotating flask with a condenser and collection assembly." },
            new ToolDef { Key = "freeze_dryer", NameZh = "冷冻干燥机", NameEn = "Freeze dryer", Category = "separation", Value = 515, DescZh = "真空干燥舱配有独立冷阱。", DescEn = "A vacuum drying chamber with a separate cold trap." },
            new ToolDef { Key = "glovebox", NameZh = "密闭手套箱", NameEn = "Glovebox", Category = "safety", Value = 540, DescZh = "密封观察窗下有一对手套口。", DescEn = "A sealed viewing chamber with two glove ports." },
        };

        internal static readonly LabEquipmentDef[] All =
        {
            new LabEquipmentDef("beaker", 1, EquipmentTask.Mix, "配制混合样", "Mix sample"),
            new LabEquipmentDef("conical_flask", 1, EquipmentTask.Settle, "静置 / 析晶", "Settle / crystallize"),
            new LabEquipmentDef("graduated_cylinder", 1, EquipmentTask.Volume, "读取刻度", "Read scale"),
            new LabEquipmentDef("pipette", 1, EquipmentTask.Dispense, "定量移液", "Dispense"),
            new LabEquipmentDef("mortar_pestle", 1, EquipmentTask.SolidCycle, "研磨", "Grind", "研磨", "Grinding"),
            new LabEquipmentDef("bunsen_burner", 1, EquipmentTask.Settings, "测试火力", "Test burner", "火力", "Flame"),
            new LabEquipmentDef("balance", 1, EquipmentTask.Weigh, "称量", "Weigh"),
            new LabEquipmentDef("filter_funnel", 1, EquipmentTask.Filter, "过滤", "Filter"),

            new LabEquipmentDef("burette", 2, EquipmentTask.Dispense, "定量滴加", "Dispense"),
            new LabEquipmentDef("ph_meter", 2, EquipmentTask.Acidity, "检查酸碱性", "Check acidity"),
            new LabEquipmentDef("conductivity_meter", 2, EquipmentTask.Conductivity, "检查导电性", "Check conductivity"),
            new LabEquipmentDef("centrifuge", 2, EquipmentTask.Clarify, "离心分离", "Centrifuge"),
            new LabEquipmentDef("distiller", 2, EquipmentTask.Distill, "蒸发并冷凝", "Evaporate and condense", "温区", "Temperature band"),
            new LabEquipmentDef("separating_funnel", 2, EquipmentTask.SeparateLayers, "分收上下层", "Collect both layers"),
            new LabEquipmentDef("hotplate_stirrer", 2, EquipmentTask.Mix, "控温搅拌", "Temperature-controlled stirring", "温区", "Temperature band"),
            new LabEquipmentDef("heating_mantle", 2, EquipmentTask.WaterCycle, "加热测试", "Test heating", "火力", "Heat"),
            new LabEquipmentDef("fume_hood", 2, EquipmentTask.GasCollect, "排风 / 收集气体", "Ventilate / collect gas", "风量", "Airflow"),
            new LabEquipmentDef("ultrasonic_cleaner", 2, EquipmentTask.GlasswareCycle, "清洗器皿", "Clean glassware", "清洗强度", "Cleaning intensity"),
            new LabEquipmentDef("drying_oven", 2, EquipmentTask.Dry, "干燥回收", "Dry and recover"),
            new LabEquipmentDef("microscope", 2, EquipmentTask.AnalyzeSolid, "观察固体", "Inspect solid"),

            new LabEquipmentDef("titration_set", 3, EquipmentTask.Titrate, "滴加并判定终点", "Titrate and check endpoint"),
            new LabEquipmentDef("spectrometer", 3, EquipmentTask.AnalyzeAny, "扫描成分", "Scan sample"),
            new LabEquipmentDef("chromatograph", 3, EquipmentTask.AnalyzeFluid, "分析流体", "Analyze fluid"),
            new LabEquipmentDef("rotary_evaporator", 3, EquipmentTask.Distill, "减压冷凝回收", "Recover under vacuum", "温区", "Temperature band"),
            new LabEquipmentDef("freeze_dryer", 3, EquipmentTask.FreezeDry, "冻干并收集冷阱水", "Freeze-dry and recover water"),
            new LabEquipmentDef("autoclave", 3, EquipmentTask.GlasswareCycle, "器皿灭菌", "Sterilize glassware", "循环强度", "Intensity"),
            new LabEquipmentDef("glovebox", 3, EquipmentTask.Seal, "隔离封装 / 拆封", "Seal / reopen"),
        };

        private static readonly Dictionary<string, LabEquipmentDef> ByKey = All.ToDictionary(def => def.Key);

        internal static LabEquipmentDef Find(string key) => ByKey.TryGetValue(key, out var def) ? def : null;
    }
}
