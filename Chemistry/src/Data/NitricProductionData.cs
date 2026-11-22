using System;
using System.Linq;

namespace Chemistry
{
    internal static class NitricProductionData
    {
        internal const string Key = "nitric_acid";
        internal const string CleanFeed = "chem_line_nitrogen_feed";
        internal const string Crude = "chem_line_crude";
        internal const string Refined = "chem_line_refined";
        internal const string Parts = "chem_line_salvage_parts";
        internal const string Power = "chem_line_power_pack";
        internal const string LabModule = "chem_line_lab_module";
        internal const string PipeModule = "chem_line_pipe_module";

        internal sealed class Item
        {
            internal readonly string Id;
            internal readonly string Zh;
            internal readonly string En;
            internal readonly string Sprite;
            internal readonly int Value;
            internal string Name => Loc.Pick(Zh, En);

            internal Item(string id, string zh, string en, string sprite, int value)
            {
                Id = id;
                Zh = zh;
                En = en;
                Sprite = sprite;
                Value = value;
            }
        }

        internal static readonly Item[] Items =
        {
            new Item(CleanFeed, "净化氮基料", "Prepared nitrogen feed", "sample_gas", 4),
            new Item(Crude, "未过滤工艺液", "Unfiltered process fluid", "sample_liquid", 8),
            new Item(Refined, "待灌装工艺液", "Ready-to-fill process fluid", "tool_test_tube", 12),
            new Item(Parts, "回收机件", "Salvaged machine parts", "sample_circuit", 6),
            new Item(Power, "工业动力匣", "Industrial power pack", "tool_crucible", 6),
            new Item(LabModule, "实验室设备组", "Laboratory equipment kit", "machine_lab_equipment_kit", 120),
            new Item(PipeModule, "工业联机设备组", "Industrial line kit", "machine_industrial_line_kit", 240),
        };

        internal static Item Find(string id) => Array.Find(Items, item => item.Id == id);
        internal static CompoundDef Product => ChemistryCatalog.FindCompound(Key);
        internal static string RouteDescription => Loc.Pick("废弃采样气瓶 → 检测、预处理、转化、过滤、灌装",
            "Discarded gas samplers → scan, prepare, convert, filter, fill");

        internal static int FeedUnits(string id, bool allowUnscanned)
        {
            SampleDef sample = SampleTable.Find(id);
            if (sample == null || (!allowUnscanned && id != sample.DetectedId))
            {
                return 0;
            }

            int grade = sample.Components.FirstOrDefault(part => part.Symbol == "N").Percent;
            return grade >= 60 ? 1 : grade > 0 ? 2 : 0;
        }
    }
}
