using System;

namespace Chemistry
{
    internal static class LabAnalysisData
    {
        private const string Prefix = ChemistryCatalog.ItemPrefix;
        internal const string DyeKey = "lab_dye_reference";
        internal const string DiluteKey = "lab_dye_half_strength";
        internal const string TreatedKey = "lab_decolorized_sample";
        internal const string AcidKey = "lab_titration_acid_standard";
        internal const string TitrantKey = "lab_titration_base_standard";
        internal const string HalfKey = "lab_titration_halfway";
        internal const string EndKey = "lab_titration_endpoint";
        internal const string OverKey = "lab_titration_overshoot";
        internal const string Dye = Prefix + DyeKey;
        internal const string Dilute = Prefix + DiluteKey;
        internal const string Treated = Prefix + TreatedKey;
        internal const string Acid = Prefix + AcidKey;
        internal const string Titrant = Prefix + TitrantKey;
        internal const string Half = Prefix + HalfKey;
        internal const string End = Prefix + EndKey;
        internal const string Over = Prefix + OverKey;
        internal const string Carbon = Prefix + "lab_activated_carbon";
        internal const string Spent = Prefix + "lab_spent_carbon";
        internal static readonly string[] Items = { Dye, Dilute, Treated, Acid, Titrant, Half, End, Over, Carbon, Spent };

        internal static bool IsKnown(string id) => Array.IndexOf(Items, id) >= 0;
        internal static bool IsDye(string id) => id == Dye || id == Dilute;
        internal static bool IsOpticalSample(string id) => IsDye(id) || id == Treated;
        internal static int RelativeConcentration(string id) => id == Dye ? 100 : id == Dilute ? 50 : 0;

        internal static int? Tint(string key) => key switch
        {
            DyeKey => 0x3D7CAB,
            DiluteKey => 0x84B9CC,
            TreatedKey => 0xADC5C3,
            AcidKey => 0xD79264,
            TitrantKey => 0x98BDB4,
            HalfKey => 0xE5C292,
            EndKey => 0x8DBF9A,
            OverKey => 0xBE80B5,
            _ => null
        };

        internal static string Name(string id) => id switch
        {
            Dye => Loc.Pick("蓝色示踪原液", "Blue tracer stock"),
            Dilute => Loc.Pick("半浓度示踪液", "Half-strength tracer"),
            Treated => Loc.Pick("吸附处理液", "Adsorption-treated sample"),
            Acid => Loc.Pick("酸性滴定样", "Acidic titration sample"),
            Titrant => Loc.Pick("碱性滴定标准液", "Basic titration standard"),
            Half => Loc.Pick("滴定中间液", "Partly titrated sample"),
            End => Loc.Pick("滴定终点液", "Titration endpoint sample"),
            Over => Loc.Pick("过量滴定液", "Overshot titration sample"),
            Carbon => Loc.Pick("活性炭吸附剂", "Activated carbon adsorbent"),
            _ => Loc.Pick("用过的吸附剂", "Spent adsorbent")
        };

        internal static string Description(string id)
        {
            if (id == Dye) return Loc.Pick("可在烧杯中稀释，或用光谱仪和活性炭过滤。", "Dilute in a beaker, measure by spectroscopy or filter with activated carbon.");
            if (IsOpticalSample(id)) return Loc.Pick("放入光谱仪测示踪剂相对浓度。", "Measure the relative tracer concentration with a spectrometer.");
            if (id == Carbon) return Loc.Pick("与蓝色示踪液一起放入过滤漏斗，吸附色料。", "Load with blue tracer in the filter funnel to capture dye.");
            if (id == Spent) return Loc.Pick("已吸附色料，可丢入固体废物桶。", "Contains captured tracer. Dispose of in a solid waste bin.");
            if (LabTitration.IsProduct(id)) return Loc.Pick("可用 pH 计复测，或倒入空试剂瓶分装。", "Check with the pH meter or pour into an empty reagent bottle to split the sample.");
            return Loc.Pick("游戏内滴定教学试液。可用滴定装置和 pH 计读取当前状态。", "In-game titration training solution. Use the titration set and pH meter to check the result.");
        }
    }
}
