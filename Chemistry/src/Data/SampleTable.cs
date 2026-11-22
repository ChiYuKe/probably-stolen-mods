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
        public readonly (string Symbol, int Percent)[] Components;

        public string Batch { get; }
        public string ItemId { get; }
        public string DetectedId { get; }
        public string Name => Loc.Pick(NameZh, NameEn);

        public SampleDef(string batch, string spriteKey, string nameZh, string nameEn, int weight,
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
            int impurities = 100 - Components.Sum(c => c.Percent);
            return contents + " · " + Loc.Get("sample_impurities", "杂质 {0}%", "Impurities {0}%", impurities);
        }
    }

    internal static class SampleTable
    {
        public const string VanillaScrapId = "scrap_metal";

        // 抽中批次时组成已经确定。检测只把同一批次切换成已检测物品，
        // 两个 ID 均走游戏原有物品存档和实验室储物格序列化。
        public static readonly SampleDef[] All =
        {
            new SampleDef("01", "metal", "未知金属碎片", "Unknown metal fragments", 24,
                ("Fe", 72), ("Zn", 16)),
            new SampleDef("02", "metal", "未知金属碎片", "Unknown metal fragments", 22,
                ("Al", 68), ("Fe", 20)),
            new SampleDef("03", "metal", "未知金属碎片", "Unknown metal fragments", 18,
                ("Cu", 55), ("Zn", 32)),
            new SampleDef("04", "circuit", "烧蚀电路板", "Scorched circuit board", 13,
                ("Cu", 52), ("Si", 24), ("Ag", 12)),
            new SampleDef("05", "circuit", "烧蚀电路板", "Scorched circuit board", 6,
                ("Si", 52), ("Cu", 28), ("Au", 10)),
            new SampleDef("06", "slag", "混合矿渣", "Mixed slag", 19,
                ("Ca", 54), ("O", 22), ("Mg", 14)),
            new SampleDef("07", "slag", "混合矿渣", "Mixed slag", 17,
                ("C", 58), ("S", 26)),
            new SampleDef("08", "slag", "混合矿渣", "Mixed slag", 12,
                ("Ti", 51), ("Mn", 28), ("Fe", 12)),
            new SampleDef("09", "liquid", "浑浊密封样品", "Cloudy sealed sample", 22,
                ("O", 79), ("H", 10)),
            new SampleDef("10", "liquid", "浑浊密封样品", "Cloudy sealed sample", 19,
                ("Cl", 53), ("Na", 35)),
            new SampleDef("11", "liquid", "浑浊密封样品", "Cloudy sealed sample", 14,
                ("K", 52), ("O", 22), ("P", 16)),
            new SampleDef("12", "liquid", "浑浊密封样品", "Cloudy sealed sample", 9,
                ("Mg", 50), ("Li", 30), ("Na", 9)),
            new SampleDef("13", "gas", "废弃采样气瓶", "Discarded gas sampler", 22,
                ("N", 74), ("O", 18)),
            new SampleDef("14", "gas", "废弃采样气瓶", "Discarded gas sampler", 25,
                ("H", 57), ("C", 28)),
            new SampleDef("15", "gas", "废弃采样气瓶", "Discarded gas sampler", 26,
                ("N", 42), ("O", 26)),
            // 原版焊机追加掉落，沿用原版物品 ID；这里只登记它的检测结果。
            new SampleDef(VanillaScrapId, "metal", "废金属", "Scrap metal", 0,
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
