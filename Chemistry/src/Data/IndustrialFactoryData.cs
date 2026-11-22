namespace Chemistry
{
    internal static class IndustrialFactoryData
    {
        internal const string Frame = ChemistryCatalog.ItemPrefix + "factory_workshop";
        internal const string Transport = ChemistryCatalog.ItemPrefix + "factory_transport";
        internal const string FrameSprite = "machine_factory_workshop";
        internal const int UnlockDay = 12;
        internal const int DefaultLimit = 4;
        internal const int MaxLimit = 8;
        internal const int FrameParts = 6;
        internal const int TransportParts = 3;

        internal sealed class Route
        {
            internal readonly string Feed;
            internal readonly string Intermediate;
            internal readonly string Solid;
            internal readonly string PrepTool;
            internal readonly string Zh;
            internal readonly string En;

            internal Route(string feed, string intermediate, string solid, string prepTool, string zh, string en)
            {
                Feed = feed;
                Intermediate = intermediate;
                Solid = solid;
                PrepTool = ChemistryCatalog.ToolItemId(prepTool);
                Zh = zh;
                En = en;
            }

            internal string Name => Loc.Pick(Zh, En);
        }

        internal static readonly Route[] Routes =
        {
            new Route(LabMixtureData.Suspension, LabMixtureData.Settled, LabMixtureData.Chalk,
                "centrifuge", "悬浊液回收线", "Suspension recovery"),
            new Route(LabMixtureData.Brine, LabMixtureData.Crystals, LabMixtureData.Salt,
                "hotplate_stirrer", "盐液结晶线", "Brine crystallization"),
        };

        internal static string Dryer => ChemistryCatalog.ToolItemId("drying_oven");
        internal static bool IsFactoryItem(string id) => id == Frame || id == Transport;

        internal static (string Nitric, string Factory) SplitLayout(string layout)
        {
            int separator = layout?.IndexOf('!') ?? -1;
            return separator < 0 ? (layout ?? "", "") : (layout.Substring(0, separator), layout.Substring(separator + 1));
        }

        internal static string JoinLayout(string nitric, string factory) =>
            string.IsNullOrEmpty(factory) ? nitric : nitric + "!" + factory;
    }
}
