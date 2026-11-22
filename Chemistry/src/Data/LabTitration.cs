using System;
using System.Globalization;

namespace Chemistry
{
    internal static class LabTitration
    {
        internal const string Equation = "H⁺ + OH⁻ → H₂O";
        internal const int ReactionTicks = 20;
        private const string CompositionTag = "chemistry_titration_composition";
        private const decimal StockMicromolesPerMilliliter = 1m;
        private const decimal StockMicromolesPerVolumeUnit = ReagentBottle.MillilitersPerUnit * StockMicromolesPerMilliliter;

        // Balance is positive for excess H⁺ and negative for excess OH⁻. Amounts are in µmol.
        internal readonly struct Composition
        {
            internal readonly decimal Balance;
            internal readonly decimal FormedWater;

            internal Composition(decimal balance, decimal formedWater)
            {
                Balance = balance;
                FormedWater = formedWater;
            }

            internal Composition Scale(int numerator, int denominator) =>
                new(Balance * numerator / denominator, FormedWater * numerator / denominator);
        }

        internal static bool IsProduct(string id) => id is LabAnalysisData.Half or LabAnalysisData.End or LabAnalysisData.Over;

        internal static bool IsSample(string id) => id == LabAnalysisData.Acid || IsProduct(id);

        internal static bool CanCombine(string first, string second) =>
            (IsSample(first) || first == LabAnalysisData.Titrant)
            && (IsSample(second) || second == LabAnalysisData.Titrant)
            && (first != second || IsProduct(first));

        internal static (string Id, int Volume, Composition Contents, decimal Reacted) Combine(Composition first, Composition second, int volume)
        {
            decimal acid = Math.Max(0m, first.Balance) + Math.Max(0m, second.Balance);
            decimal hydroxide = Math.Max(0m, -first.Balance) + Math.Max(0m, -second.Balance);
            // The equation consumes one unit of each reactant per unit of water formed.
            decimal extent = Math.Min(acid, hydroxide);
            decimal balance = acid - hydroxide;
            if (Math.Abs(balance) < 1e-20m) balance = 0m;
            string id = balance > 0m ? LabAnalysisData.Half : balance < 0m ? LabAnalysisData.Over : LabAnalysisData.End;
            return (id, volume, new Composition(balance, first.FormedWater + second.FormedWater + extent), extent);
        }

        internal static Composition Read(GameItem item, int volume)
        {
            if (IsProduct(item.identifier) && TryDecode(SavedComposition(item), out var contents, out int referenceVolume))
                return contents.Scale(volume, referenceVolume);
            return LegacyComposition(item.identifier, item.unitCount).Scale(volume, Math.Max(1, item.unitCount));
        }

        internal static Composition LegacyComposition(string id, int volume)
        {
            const decimal dose = StockMicromolesPerVolumeUnit;
            return id switch
            {
                LabAnalysisData.Acid => new Composition(volume * dose, 0),
                LabAnalysisData.Titrant => new Composition(-volume * dose, 0),
                LabAnalysisData.Half => new Composition(dose, dose).Scale(volume, 3),
                LabAnalysisData.End => new Composition(0, 2 * dose).Scale(volume, 4),
                LabAnalysisData.Over => volume >= 5 ? new Composition(-(volume - 4) * dose, 2 * dose)
                    : new Composition(-dose, 2 * dose).Scale(volume, 5),
                _ => default
            };
        }

        internal static string Encode(Composition contents, int volume) =>
            FormattableString.Invariant($"{contents.Balance}:{contents.FormedWater}:{volume}");

        private static bool TryDecode(string text, out Composition contents, out int volume)
        {
            contents = default;
            volume = 0;
            if (string.IsNullOrEmpty(text)) return false;
            string[] parts = text.Split(':');
            if (parts.Length != 3
                || !decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal balance)
                || !decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out decimal water)
                || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out volume)
                || volume <= 0 || water < 0m || water > volume * StockMicromolesPerVolumeUnit
                || Math.Abs(balance) > volume * StockMicromolesPerVolumeUnit)
                return false;
            contents = new Composition(balance, water);
            return true;
        }

        internal static string SavedComposition(GameItem item) => IsProduct(item.identifier)
            && item.state.dict.TryGetValue(CompositionTag, out TagState tag) ? tag.GetString() : null;

        internal static void RestoreComposition(GameItem item, string value)
        {
            if (IsProduct(item.identifier) && TryDecode(value, out _, out _))
                item.ModifyTag(CompositionTag, tag => tag.SetString(value), skipValidate: true);
        }

        internal static string Describe(Composition contents)
        {
            if (contents.Balance > 0m) return Loc.Pick("还有酸没反应完。", "Some acid remains.");
            if (contents.Balance < 0m) return Loc.Pick("碱加多了，溶液偏碱。", "Excess base made the solution alkaline.");
            return Loc.Pick("刚好中和。", "Neutralized.");
        }

        internal static string PhReading(GameItem item)
        {
            if (item.identifier == LabMixtureData.Water) return "7.00";
            if (!IsSample(item.identifier) && item.identifier != LabAnalysisData.Titrant) return null;
            decimal balance = Read(item, item.unitCount).Balance;
            double concentration = (double)Math.Abs(balance) / (1000d * Math.Max(1, item.unitCount) * ReagentBottle.MillilitersPerUnit);
            double dominantIon = (concentration + Math.Sqrt(concentration * concentration + 4e-14)) * 0.5d;
            double ph = -Math.Log10(dominantIon);
            return (balance < 0m ? 14d - ph : ph).ToString("0.00");
        }
    }
}
