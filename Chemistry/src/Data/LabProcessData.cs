using System;

namespace Chemistry
{
    internal static class LabProcessData
    {
        private const string P = ChemistryCatalog.ItemPrefix;
        internal const string CarbonDioxide = P + "cmp_carbon_dioxide";
        internal const string MineralWater = P + "process_carbonated_water";
        internal const string ExtractMixed = P + "process_extraction_mixed";
        internal const string ExtractLayers = P + "process_extraction_layers";
        internal const string WashMixed = P + "process_wash_mixed";
        internal const string WashLayers = P + "process_wash_layers";
        internal const string WaterTrace = P + "lab_extract_water";
        internal const string OilTrace = P + "lab_extract_oil";
        internal const string WashWater = P + "lab_wash_water";
        internal const string WashOil = P + "lab_wash_oil";
        internal const string PeakA = P + "lab_chromatograph_a";
        internal const string PeakB = P + "lab_chromatograph_b";
        internal const string Rinse = P + "lab_equipment_rinse";
        internal const string BrineAliquot = P + "lab_brine_aliquot";
        internal const string SealedChalk = P + "process_sealed_chalk";
        internal const string SealedSalt = P + "process_sealed_salt";
        internal const string Saturated = P + "process_saturated_salt";
        internal const string Concentrated = P + "process_concentrated_salt";
        internal const string LoadedCrystals = P + "process_crystals_with_residue";
        internal static readonly string[] Items =
        {
            MineralWater, ExtractMixed, ExtractLayers, WashMixed, WashLayers, WaterTrace, OilTrace, WashWater, WashOil, PeakA, PeakB,
            Rinse, BrineAliquot, SealedChalk, SealedSalt, Saturated, Concentrated, LoadedCrystals
        };

        internal static bool IsKnown(string id) => Array.IndexOf(Items, id) >= 0;
        internal static bool IsExtract(string id) => id == OilTrace || id == WaterTrace || id == WashOil || id == WashWater;
        internal static int TracerSignal(string id) => id switch
        {
            OilTrace => 80,
            WaterTrace => 20,
            WashOil => 64,
            _ => 16
        };
        internal static bool IsPackedSolid(string id) => id == SealedSalt || id == SealedChalk;
        internal static int? Tint(string key) => key switch
        {
            "lab_extract_water" => 0xA3C9CE,
            "lab_extract_oil" => 0x9A7449,
            "lab_wash_water" => 0xB3CACE,
            "lab_wash_oil" => 0xAD8C55,
            "lab_chromatograph_a" => 0x4B86AE,
            "lab_chromatograph_b" => 0xA8894C,
            "lab_equipment_rinse" => 0x7E8A77,
            "lab_brine_aliquot" => 0xA7BDC5,
            _ => null
        };

        internal static (int Units, string Contents) Read(string id) => id switch
        {
            LabMixtureData.Emulsion or LabMixtureData.Layers => (4, Loc.Pick("水 50 mL · 油 50 mL", "50 mL water · 50 mL oil")),
            ExtractMixed or ExtractLayers => (4, Loc.Pick("示踪水相 50 mL · 油相 50 mL", "50 mL tracer water · 50 mL oil")),
            WashMixed or WashLayers => (4, Loc.Pick("洗涤水相 50 mL · 油相 50 mL", "50 mL wash water · 50 mL oil")),
            LabDistillationData.Feed => (4, Loc.Pick("醇水混合液", "Alcohol-water mixture")),
            LabDistillationData.Remainder => (2, Loc.Pick("待继续蒸馏的余液", "Residue for further distillation")),
            LabMixtureData.Suspension or LabMixtureData.Settled => (2, Loc.Pick("水 50 mL · 固体 1 份", "50 mL water · 1 unit of solid")),
            LabMixtureData.Brine or LabMixtureData.Crystals => (2, Loc.Pick("水 50 mL · 盐 1 份", "50 mL water · 1 unit of salt")),
            Saturated or Concentrated or LoadedCrystals => (2, Loc.Pick("水 50 mL · 盐共 2 份", "50 mL water · 2 units of salt")),
            MineralWater => (2, Loc.Pick("水 50 mL · 可回收气体 25 mL", "50 mL water · 25 mL recoverable gas")),
            _ => default
        };

        internal static string Name(string id) => id switch
        {
            MineralWater => Loc.Pick("含气水样", "Carbonated water sample"),
            ExtractMixed => Loc.Pick("示踪剂萃取混合样", "Tracer extraction mixture"),
            ExtractLayers => Loc.Pick("已分层萃取样", "Layered extraction sample"),
            WashMixed => Loc.Pick("油相洗涤混合样", "Oil wash mixture"),
            WashLayers => Loc.Pick("已分层洗涤样", "Layered wash sample"),
            WaterTrace => Loc.Pick("萃取水相", "Extracted aqueous phase"),
            OilTrace => Loc.Pick("萃取油相", "Extracted oil phase"),
            WashWater => Loc.Pick("洗涤水相", "Wash water phase"),
            WashOil => Loc.Pick("洗涤后油相", "Washed oil phase"),
            PeakA => Loc.Pick("色谱 A 馏分", "Chromatography fraction A"),
            PeakB => Loc.Pick("色谱 B 馏分", "Chromatography fraction B"),
            Rinse => Loc.Pick("设备清洗废液", "Equipment rinse waste"),
            BrineAliquot => Loc.Pick("盐溶液取样", "Brine aliquot"),
            SealedChalk => Loc.Pick("密封碳酸钙样", "Sealed calcium carbonate"),
            SealedSalt => Loc.Pick("密封盐样", "Sealed salt sample"),
            Saturated => Loc.Pick("饱和盐液 · 含未溶盐", "Saturated brine with excess salt"),
            Concentrated => Loc.Pick("热浓盐溶液", "Hot concentrated brine"),
            _ => Loc.Pick("析晶样 · 含未溶盐", "Crystallized brine with excess salt")
        };

        internal static string Guide(string id) => id switch
        {
            MineralWater => Loc.Pick("到通风橱的集气面板，装入空采样气瓶。", "Fit an empty gas receiver in the fume hood's collection panel."),
            ExtractMixed or WashMixed => Loc.Pick("先用锥形瓶静置或离心机分层，再用分液漏斗收集。", "Settle or centrifuge, then collect both layers with a separatory funnel."),
            ExtractLayers or WashLayers => Loc.Pick("分液漏斗可分别收集上层油相和下层水相。", "Collect the upper oil and lower water phases using a separatory funnel."),
            WaterTrace or WashWater or WashOil => Loc.Pick("可用移液管取样，或与水对照一起放入光谱仪。", "Aliquot with the pipette or measure in the spectrometer against a water blank."),
            OilTrace => Loc.Pick("可在烧杯加水洗涤，用光谱仪检测，或用色谱仪分段收集。", "Wash with water, measure with a spectrometer or collect chromatographic fractions."),
            PeakA or PeakB => Loc.Pick("可用移液管另取样，保留余液。", "Take an aliquot with a pipette and keep the rest."),
            Rinse => Loc.Pick("可倒入废液桶。", "Transfer to a liquid waste drum."),
            BrineAliquot => Loc.Pick("可用电导率仪检测；合并至 50 mL 后可放回锥形瓶析晶。", "Measure conductivity, or pool to 50 mL and crystallize in the flask."),
            SealedChalk or SealedSalt => Loc.Pick("放回手套箱可拆封，回收固体和空瓶。", "Reopen in the glovebox to recover the solid and the empty bottle."),
            Saturated => Loc.Pick("瓶底可见未溶盐。可直接过滤，或用锥形瓶冷却，析出更多晶体。", "Excess salt is visible. Filter directly or cool in the flask to form more crystals."),
            Concentrated => Loc.Pick("两份盐均已溶解。用锥形瓶冷却析晶。", "Both salt units are dissolved. Cool in the flask to crystallize."),
            _ => Loc.Pick("可过滤或离心收集湿盐晶，再用干燥箱回收盐。", "Filter or centrifuge to collect wet salt crystals, then dry them in the oven.")
        };
    }
}
