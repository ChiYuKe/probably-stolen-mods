using System;
using System.Collections.Generic;
using System.Linq;

namespace Chemistry
{
    internal sealed class SampleDef
    {
        public readonly string SpriteKey;
        public readonly string NameZh;
        public readonly string NameEn;
        public readonly int Weight;
        public readonly int Grams;
        public readonly (string Symbol, int Percent)[] Components;

        public string Batch { get; }
        public string ItemId { get; }
        public string DetectedId { get; }
        public string Name => Loc.Pick(NameZh, NameEn);
        public int ImpurityPercent => 100 - Components.Sum(component => component.Percent);

        public SampleDef(string batch, string spriteKey, string nameZh, string nameEn, int weight, int grams,
            params (string Symbol, int Percent)[] components)
        {
            Batch = batch;
            string customId = ChemistryCatalog.ItemPrefix + "sample_" + batch;
            ItemId = batch == SampleTable.VanillaScrapId ? batch : customId;
            DetectedId = customId + "_detected";
            SpriteKey = "sample_" + spriteKey;
            NameZh = nameZh;
            NameEn = nameEn;
            Weight = weight;
            Grams = grams;
            Components = components;
        }

        public string DisplayName(bool detected)
        {
            return detected ? Loc.Get("sample_detected_name", "{0}（已检测）", "{0} (analyzed)", Name) : Name;
        }

        public string Report()
        {
            string contents = string.Join(" · ", Components.Select(c =>
                SampleTable.ElementName(c.Symbol) + " " + c.Percent + "%"));
            return contents + " · " + Loc.Get("sample_impurities", "杂质 {0}%", "Impurities {0}%", ImpurityPercent);
        }

        /// <summary>按质量分数折算某成分的单件质量（克）；占比只用于展示，不参与提炼结算。</summary>
        public double ComponentGrams(string symbol)
        {
            int percent = Array.Find(Components, c => c.Symbol == symbol).Percent;
            return Grams * percent / 100.0;
        }

        public string MassLine()
        {
            return ItemMass.Line(Grams);
        }
    }

    internal static class SampleTable
    {
        public const string VanillaScrapId = "scrap_metal";

        // 抽中批次时组成已经确定。检测只把同一批次切换成已检测物品，
        // 两个 ID 均走游戏原有物品存档和实验室储物格序列化。
        // grams 是单件实物质量（克），只在提示框和报告里展示。
        public static readonly SampleDef[] All =
        {
            new SampleDef("01", "metal", "未知金属碎片", "Unknown metal fragments", 24, 148,
                ("Fe", 72), ("Zn", 16)),
            new SampleDef("02", "metal", "未知金属碎片", "Unknown metal fragments", 22, 112,
                ("Al", 68), ("Fe", 20)),
            new SampleDef("03", "metal", "未知金属碎片", "Unknown metal fragments", 18, 166,
                ("Cu", 55), ("Zn", 32)),
            new SampleDef("04", "circuit", "烧蚀电路板", "Scorched circuit board", 13, 96,
                ("Cu", 52), ("Si", 24), ("Ag", 12)),
            new SampleDef("05", "circuit", "烧蚀电路板", "Scorched circuit board", 6, 74,
                ("Si", 52), ("Cu", 28), ("Au", 10)),
            new SampleDef("06", "slag", "混合矿渣", "Mixed slag", 19, 244,
                ("Ca", 54), ("O", 22), ("Mg", 14)),
            new SampleDef("07", "slag", "混合矿渣", "Mixed slag", 17, 188,
                ("C", 58), ("S", 26)),
            new SampleDef("08", "slag", "混合矿渣", "Mixed slag", 12, 216,
                ("Ti", 51), ("Mn", 28), ("Fe", 12)),
            new SampleDef("09", "liquid", "浑浊密封样品", "Cloudy sealed sample", 22, 46,
                ("O", 79), ("H", 10)),
            new SampleDef("10", "liquid", "浑浊密封样品", "Cloudy sealed sample", 19, 58,
                ("Cl", 53), ("Na", 35)),
            new SampleDef("11", "liquid", "浑浊密封样品", "Cloudy sealed sample", 14, 51,
                ("K", 52), ("O", 22), ("P", 16)),
            new SampleDef("12", "liquid", "浑浊密封样品", "Cloudy sealed sample", 9, 42,
                ("Mg", 50), ("Li", 30), ("Na", 9)),
            new SampleDef("13", "gas", "废弃采样气瓶", "Discarded gas sampler", 22, 312,
                ("N", 74), ("O", 18)),
            new SampleDef("14", "gas", "废弃采样气瓶", "Discarded gas sampler", 25, 286,
                ("H", 57), ("C", 28)),
            new SampleDef("15", "gas", "废弃采样气瓶", "Discarded gas sampler", 26, 334,
                ("N", 42), ("O", 26)),
            // 原版焊机追加掉落，沿用原版物品 ID；这里只登记它的检测结果。
            new SampleDef(VanillaScrapId, "metal", "废金属", "Scrap metal", 0, 138,
                ("Fe", 66), ("Cu", 20)),
        };

        private static readonly Dictionary<string, SampleDef> ById = BuildIndex();

        public static SampleDef Find(string itemId)
        {
            return itemId != null && ById.TryGetValue(itemId, out SampleDef sample) ? sample : null;
        }

        public static string ElementName(string symbol)
        {
            ElementDef element = ChemistryCatalog.FindElement(symbol);
            return element == null ? symbol : Loc.Pick(element.NameZh, element.NameEn) + " (" + symbol + ")";
        }

        private static Dictionary<string, SampleDef> BuildIndex()
        {
            var index = new Dictionary<string, SampleDef>(StringComparer.Ordinal);
            foreach (SampleDef sample in All)
            {
                index.Add(sample.ItemId, sample);
                index.Add(sample.DetectedId, sample);
            }
            return index;
        }
    }
}
