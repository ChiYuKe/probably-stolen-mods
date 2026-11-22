namespace Chemistry
{
    internal static class LabDistillationData
    {
        internal const string Ethanol = ChemistryCatalog.ItemPrefix + "cmp_ethanol";
        internal const string Feed = ChemistryCatalog.ItemPrefix + "mixture_alcohol_feed";
        internal const string Remainder = ChemistryCatalog.ItemPrefix + "mixture_alcohol_residue";
        internal const string EarlyKey = "lab_early_distillate";
        internal const string LateKey = "lab_late_distillate";
        internal const string MixedKey = "lab_mixed_distillate";
        internal const string Early = ChemistryCatalog.ItemPrefix + EarlyKey;
        internal const string Late = ChemistryCatalog.ItemPrefix + LateKey;
        internal const string Mixed = ChemistryCatalog.ItemPrefix + MixedKey;

        internal static bool Accepts(string id) => id == Feed || id == Remainder || id == LabMixtureData.Brine;

        internal static int Temperature(int level, bool vacuum) => (vacuum ? 40 : 70) + (level - 1) * 15;

        internal static string Blocker(string id, int level)
        {
            if (level == 1) return Loc.Pick("温区过低，暂无可收集的冷凝液", "Temperature too low for collection");
            if (level == 2 && id != Feed)
                return Loc.Pick("该样品需要高温区", "This sample requires the high temperature band");
            return null;
        }

        internal static (string Distillate, int Units, string Residue) Products(string id, int level)
        {
            string empty = ReagentBottle.GlassEmpties[LabMixtureData.SmallBottle];
            return id switch
            {
                Feed when level == 2 => (Early, LabMixtureData.Portion, Remainder),
                Feed => (Mixed, LabMixtureData.Portion * 2, empty),
                Remainder => (Late, LabMixtureData.Portion, empty),
                LabMixtureData.Brine => (LabMixtureData.Water, LabMixtureData.Portion, LabMixtureData.SaltCake),
                _ => (LabMixtureData.Water, 1, null)
            };
        }
    }
}
