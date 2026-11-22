namespace Chemistry
{
    internal static class BulkContainers
    {
        internal const string SteelSprite = "lab_tall_steel_cylinder_bulk";
        internal const string CompactDrumSprite = "lab_liquid_drum_bulk";
        internal const string DrumSourceSprite = "lab_wide_liquid_drum_bulk";
        internal const string LiquidWasteSourceSprite = "lab_wide_liquid_waste_bulk";
        internal const string SolidWasteSourceSprite = "lab_wide_solid_waste_bulk";
        internal const string DrumSprite = "lab_wide_liquid_drum_bulk_3x3";
        internal const string LiquidWasteSprite = "lab_wide_liquid_waste_bulk_3x3";
        internal const string SolidWasteSprite = "lab_wide_solid_waste_bulk_3x3";
        internal const string SteelId = ChemistryCatalog.ItemPrefix + "empty_steel_cylinder";
        internal const string DrumId = ChemistryCatalog.ItemPrefix + "empty_liquid_drum";
        internal const string EmptyLiquidWasteId = ChemistryCatalog.ItemPrefix + "empty_liquid_waste_drum";
        internal const string LiquidWasteId = ChemistryCatalog.ItemPrefix + "liquid_waste_drum";
        internal const string EmptySolidWasteId = ChemistryCatalog.ItemPrefix + "empty_solid_waste_bin";
        internal const string SolidWasteId = ChemistryCatalog.ItemPrefix + "solid_waste_bin";
        internal const int SteelCapacity = 200;
        internal const int DrumCapacity = 160;
        internal const int LiquidWasteCapacity = 80;
        internal const int SolidWasteCapacity = 40;
        internal static readonly string[] ItemIds =
        {
            SteelId, DrumId, EmptyLiquidWasteId, LiquidWasteId, EmptySolidWasteId, SolidWasteId,
        };

        internal static bool IsEmptyWaste(string id) => id == EmptyLiquidWasteId || id == EmptySolidWasteId;

        internal static bool IsWaste(string id) => IsLiquidWaste(id) || IsSolidWaste(id);

        internal static bool IsLiquidWaste(string id) => id == EmptyLiquidWasteId || id == LiquidWasteId;

        internal static bool IsSolidWaste(string id) => id == EmptySolidWasteId || id == SolidWasteId;

        internal static bool AcceptsSolid(string id) =>
            id == LabAnalysisData.Spent || ReagentBottle.IsDamaged(id)
                || SampleTable.Find(id)?.SpriteKey is "sample_metal" or "sample_circuit" or "sample_slag";

        internal static string Sprite(string id) => id switch
        {
            SteelId => SteelSprite,
            DrumId => DrumSprite,
            EmptyLiquidWasteId or LiquidWasteId => LiquidWasteSprite,
            EmptySolidWasteId or SolidWasteId => SolidWasteSprite,
            _ => null,
        };

        internal static string Name(string id) => id switch
        {
            SteelId => Loc.Pick("空气体钢瓶", "Empty steel gas cylinder"),
            DrumId => Loc.Pick("空大液桶", "Empty bulk liquid drum"),
            EmptyLiquidWasteId => Loc.Pick("空废液桶", "Empty liquid waste drum"),
            LiquidWasteId => Loc.Pick("废液桶", "Liquid waste drum"),
            EmptySolidWasteId => Loc.Pick("空废物桶", "Empty solid waste bin"),
            _ => Loc.Pick("废物桶", "Solid waste bin"),
        };

        internal static string Description(string id) => id switch
        {
            SteelId => Loc.Pick("拖入气体采样瓶装气。每只钢瓶只装一种气体。",
                "Drop a gas vial onto it to transfer gas. Holds one gas type at a time."),
            DrumId => Loc.Pick("拖入液体试剂瓶装液。每只大桶只装一种试剂。",
                "Drop a reagent bottle onto it to fill. Holds one liquid at a time."),
            EmptyLiquidWasteId or LiquidWasteId => Loc.Pick("拖入液体试剂瓶可收集废液。混合后不可回收。",
                "Drop a liquid reagent bottle onto it to collect waste. Mixed waste cannot be recovered."),
            _ => Loc.Pick("接收破损容器、废金属、废电路板和矿渣。投入后不可取回。",
                "Accepts damaged containers, scrap metal, circuit boards, and slag. Deposited items cannot be recovered."),
        };
    }
}
