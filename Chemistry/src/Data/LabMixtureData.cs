using System;

namespace Chemistry
{
    internal static class LabMixtureData
    {
        internal const int Portion = 2;
        internal const int SmallBottle = 0;
        internal const int OilTint = 0xD5A848;
        internal const string OilKey = "lab_sample_oil";
        internal const string Oil = ChemistryCatalog.ItemPrefix + OilKey;
        internal const string Water = ChemistryCatalog.ItemPrefix + "cmp_water";
        internal const string Chalk = ChemistryCatalog.ItemPrefix + "cmp_calcium_carbonate";
        internal const string Salt = ChemistryCatalog.ItemPrefix + "cmp_sodium_chloride";
        internal const string Emulsion = ChemistryCatalog.ItemPrefix + "mixture_emulsion";
        internal const string Layers = ChemistryCatalog.ItemPrefix + "mixture_layers";
        internal const string Suspension = ChemistryCatalog.ItemPrefix + "mixture_suspension";
        internal const string Settled = ChemistryCatalog.ItemPrefix + "mixture_settled";
        internal const string Cake = ChemistryCatalog.ItemPrefix + "mixture_filter_cake";
        internal const string Brine = ChemistryCatalog.ItemPrefix + "mixture_brine";
        internal const string Crystals = ChemistryCatalog.ItemPrefix + "mixture_crystallized";
        internal const string SaltCake = ChemistryCatalog.ItemPrefix + "mixture_wet_crystals";
        internal const string UnknownKey = "mixture_unknown";
        internal const string UnknownPlasticKey = "mixture_unknown_plastic";
        internal const string Unknown = ChemistryCatalog.ItemPrefix + UnknownKey;
        internal const string UnknownPlastic = ChemistryCatalog.ItemPrefix + UnknownPlasticKey;
        internal static readonly string[] Items =
        {
            Oil, Emulsion, Layers, Suspension, Settled, Cake, Brine, Crystals, SaltCake, Unknown, UnknownPlastic,
            LabDistillationData.Feed, LabDistillationData.Remainder, LabDistillationData.Early,
            LabDistillationData.Late, LabDistillationData.Mixed
        };

        internal static bool IsBatch(string id) => id == Emulsion || id == Layers || HasSolids(id) || id == Brine;
        internal static bool HasSolids(string id) => id == Suspension || id == Settled || id == Crystals;
        internal static bool IsKnown(string id) => Array.IndexOf(Items, id) >= 0;

        internal static bool IsDirectBatch(string id) => id == Layers || id == LabDistillationData.Feed;

        internal static (string Id, int Amount) PouredProduct(string target, int targetUnits, string source, int poured, bool glass, bool plastic)
        {
            int total = targetUnits + poured;
            if (glass && targetUnits == Portion && poured == Portion)
            {
                string ingredient = target == Water ? source : source == Water ? target : null;
                if (ingredient == Oil) return (Layers, 1);
                if (ingredient == LabDistillationData.Ethanol) return (LabDistillationData.Feed, 1);
                if (ingredient == LabAnalysisData.Dye) return (LabAnalysisData.Dilute, total);
            }

            return (plastic ? UnknownPlastic : Unknown, total);
        }

        internal static string Name(string id) => id switch
        {
            Oil => Loc.Pick("实验用油", "Lab sample oil"),
            Emulsion => Loc.Pick("油水混合样", "Oil-water emulsion"),
            Layers => Loc.Pick("双层油水样", "Layered oil-water sample"),
            Suspension => Loc.Pick("浑浊悬浊液", "Cloudy suspension"),
            Settled => Loc.Pick("已沉降样品", "Settled suspension"),
            Brine => Loc.Pick("待析晶盐溶液", "Crystallization brine"),
            Crystals => Loc.Pick("已析晶盐溶液", "Crystallized brine"),
            SaltCake => Loc.Pick("湿盐晶", "Wet salt crystals"),
            Unknown or UnknownPlastic => Loc.Pick("未鉴定混合液", "Unidentified mixture"),
            LabDistillationData.Feed => Loc.Pick("醇水混合液", "Alcohol-water mixture"),
            LabDistillationData.Remainder => Loc.Pick("醇水蒸馏余液", "Alcohol-water still residue"),
            LabDistillationData.Early => Loc.Pick("前馏分 · 富醇", "Early cut · Alcohol-rich"),
            LabDistillationData.Late => Loc.Pick("后馏分 · 富水", "Late cut · Water-rich"),
            LabDistillationData.Mixed => Loc.Pick("混收馏分", "Mixed distillate"),
            _ => Loc.Pick("湿滤渣", "Wet filter cake")
        };

        internal static string Description(string id) => id switch
        {
            Oil => Loc.Pick("分层实验的油相，可用玻璃试剂瓶分装。", "Oil phase for separation. Transfers into glass reagent bottles."),
            Emulsion => Loc.Pick("油 50 mL、水 50 mL。用锥形瓶静置，或用离心机加速分层。", "50 mL oil and 50 mL water. Settle in a flask or accelerate in the centrifuge."),
            Layers => Loc.Pick("上层油 50 mL，下层水 50 mL。可用分液漏斗分别收集。", "Top: 50 mL oil. Bottom: 50 mL water. Collect separately with the separatory funnel."),
            Suspension => Loc.Pick("水 50 mL，含 1 份悬浮固体。可静置、离心或过滤。", "50 mL water with 1 unit of suspended solid. Settle, centrifuge or filter it."),
            Settled => Loc.Pick("上层澄清水 50 mL，底部有沉淀。可过滤或离心收集。", "50 mL clear water over sediment. Collect by filtering or centrifuging."),
            Brine => Loc.Pick("水 50 mL，含 1 份盐。移液管可取样，锥形瓶可冷却析晶。", "50 mL water and 1 unit of salt. Take a pipette sample or crystallize in the flask."),
            Crystals => Loc.Pick("底部已析出盐晶。可过滤或离心，再干燥湿盐晶。", "Salt crystals have formed. Filter or centrifuge, then dry the crystals."),
            SaltCake => Loc.Pick("将湿盐晶放进干燥箱，可回收 1 份氯化钠。", "Dry in the oven to recover 1 unit of sodium chloride."),
            Unknown or UnknownPlastic => Loc.Pick("可分装或倒入废液桶。处理方式见样品详情。", "Transfer it or discard it in a liquid waste drum. See the sample details for available processes."),
            LabDistillationData.Feed => Loc.Pick("100 mL 混合液。中温先收前馏分，高温可混收整批。", "100 mL mixture. Collect the early cut at medium heat, or the combined batch at high heat."),
            LabDistillationData.Remainder => Loc.Pick("余液 50 mL。用高温区继续冷凝，收集富水馏分。", "50 mL residue. Use high heat to collect the water-rich cut."),
            LabDistillationData.Early => Loc.Pick("先冷凝收集的富醇馏分。", "Alcohol-rich condensate collected first."),
            LabDistillationData.Late => Loc.Pick("继续升温后收集的富水馏分。", "Water-rich condensate collected after raising the heat."),
            LabDistillationData.Mixed => Loc.Pick("高温一次收齐的前后馏分。", "Early and late cuts collected together at high heat."),
            _ => Loc.Pick("将湿滤渣放进干燥箱，可回收 1 份碳酸钙。", "Dry in the oven to recover 1 unit of calcium carbonate.")
        };
    }
}
