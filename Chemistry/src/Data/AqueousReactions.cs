using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Chemistry
{
    internal static class AqueousReactions
    {
        internal const string Acetic = "acetic_acid";
        internal const string CarbonDioxide = "carbon_dioxide";
        internal const string Hydrogen = "el_h";
        internal const string Precipitate = "calcium_carbonate";
        private const string Sodium = "el_na";
        private const string Water = "water";
        private const string WaterIncluded = "v2!";
        // Inventory volume and reaction portions are separate game units.
        private const decimal WaterPerVolumeUnit = 10m;
        private const string CompoundPrefix = ChemistryCatalog.ItemPrefix + "cmp_";
        private const decimal Trace = 1e-20m;

        [Flags]
        internal enum Observation
        {
            None = 0,
            Gas = 1,
            Sediment = 2,
            Neutralization = 4,
            HydrogenGas = 8
        }

        internal readonly struct Term
        {
            internal readonly string Key;
            internal readonly int Coefficient;

            internal Term(string key, int coefficient = 1)
            {
                Key = key;
                Coefficient = coefficient;
            }
        }

        internal sealed class Equation
        {
            internal readonly Term[] Reactants;
            internal readonly Term[] Products;
            internal readonly int Ticks;

            internal Equation(int ticks, Term[] reactants, params Term[] products)
            {
                Reactants = reactants;
                Products = products;
                Ticks = ticks;
                var reactantAtoms = Atoms(reactants);
                var productAtoms = Atoms(products);
                if (reactantAtoms.Count != productAtoms.Count || reactantAtoms.Any(entry =>
                    !productAtoms.TryGetValue(entry.Key, out int count) || entry.Value != count))
                    throw new InvalidOperationException("Unbalanced lab equation: " + Display);
            }

            private static Dictionary<string, int> Atoms(Term[] terms)
            {
                var totals = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (Term term in terms)
                {
                    foreach (var atom in FormulaParser.Parse(Formula(term.Key)))
                    {
                        totals.TryGetValue(atom.Key, out int count);
                        totals[atom.Key] = count + atom.Value * term.Coefficient;
                    }
                }
                return totals;
            }

            internal string Display => DisplaySide(Reactants) + " → " + DisplaySide(Products);

            private static string DisplaySide(Term[] side) => string.Join(" + ", side.Select(term =>
                (term.Coefficient == 1 ? "" : term.Coefficient + " ") + ChemistryCatalog.FormatFormulaForDisplay(Formula(term.Key))
                + (IsGas(term.Key) ? "↑" : term.Key == Precipitate ? "↓" : "")));
        }

        private static readonly string[] Powders =
        {
            "sodium_bicarbonate", "potassium_bicarbonate", "sodium_carbonate", "potassium_carbonate", "calcium_chloride",
            "sodium_hydroxide", "potassium_hydroxide", "calcium_hydroxide",
            "potassium_chloride", "sodium_sulfate", "potassium_sulfate", "magnesium_sulfate", "magnesium_chloride",
            "sodium_acetate", "potassium_acetate", "calcium_acetate", "glucose", "sucrose"
        };

        internal static readonly Equation[] Equations =
        {
            new Equation(20, new[] { new Term(Sodium, 2), new Term(Water, 2) },
                new Term("sodium_hydroxide", 2), new Term(Hydrogen)),
            AcidHydroxide("sodium_hydroxide", "sodium_acetate"),
            AcidHydroxide("potassium_hydroxide", "potassium_acetate"),
            new Equation(20, new[] { new Term(Acetic, 2), new Term("calcium_hydroxide") },
                new Term("calcium_acetate"), new Term("water", 2)),
            BaseBicarbonate("sodium_hydroxide", "sodium_bicarbonate", "sodium_carbonate"),
            BaseBicarbonate("potassium_hydroxide", "potassium_bicarbonate", "potassium_carbonate"),
            AcidCarbonate("sodium_bicarbonate", "sodium_acetate", 1),
            AcidCarbonate("potassium_bicarbonate", "potassium_acetate", 1),
            AcidCarbonate("sodium_carbonate", "sodium_acetate", 2),
            AcidCarbonate("potassium_carbonate", "potassium_acetate", 2),
            CalciumCarbonate("sodium_carbonate", "sodium_chloride"),
            CalciumCarbonate("potassium_carbonate", "potassium_chloride"),
            new Equation(30, new[] { new Term(Acetic, 2), new Term(Precipitate) },
                new Term("calcium_acetate"), new Term("water"), new Term(CarbonDioxide))
        };

        private static Equation AcidHydroxide(string baseKey, string acetate) => new(20,
            new[] { new Term(Acetic), new Term(baseKey) }, new Term(acetate), new Term("water"));

        private static Equation BaseBicarbonate(string baseKey, string bicarbonate, string carbonate) => new(20,
            new[] { new Term(baseKey), new Term(bicarbonate) }, new Term(carbonate), new Term("water"));

        private static Equation AcidCarbonate(string salt, string acetate, int acidCoefficient) => new(30,
            new[] { new Term(Acetic, acidCoefficient), new Term(salt) }, new Term(acetate, acidCoefficient),
            new Term("water"), new Term(CarbonDioxide));

        private static Equation CalciumCarbonate(string salt, string chloride) => new(25,
            new[] { new Term("calcium_chloride"), new Term(salt) }, new Term(Precipitate), new Term(chloride, 2));

        private static CompoundDef Compound(string key) => ChemistryCatalog.FindCompound(key)
            ?? throw new InvalidOperationException("Missing lab compound: " + key);

        private static string Formula(string key) => key == Sodium ? "Na" : key == Hydrogen ? "H2" : Compound(key).Formula;

        internal static bool IsSolidReagent(string id) => IsSodium(id) || (id != null && id.StartsWith(CompoundPrefix, StringComparison.Ordinal)
            && Array.IndexOf(Powders, id.Substring(CompoundPrefix.Length)) >= 0);

        /// <summary>是否为注册化合物物品（cmp_ 前缀且能在化合物表中找到）。</summary>
        internal static bool IsCompound(string id) => id != null && id.StartsWith(CompoundPrefix, StringComparison.Ordinal)
            && ChemistryCatalog.FindCompound(id.Substring(CompoundPrefix.Length)) != null;

        internal static bool IsSodium(string id) => id == ChemistryCatalog.ItemPrefix + Sodium;

        internal static bool IsGas(string key) => key is CarbonDioxide or Hydrogen;

        internal static bool HasGas(Observation effects) => (effects & (Observation.Gas | Observation.HydrogenGas)) != 0;

        internal static Observation VisualContents(GameItem item)
        {
            if (!LiquidComposition.HasAqueousContents(item)) return Observation.None;
            var amounts = Read(item, item.unitCount);
            Observation effects = Observation.None;
            if (amounts.ContainsKey(CarbonDioxide)) effects |= Observation.Gas;
            if (amounts.ContainsKey(Hydrogen)) effects |= Observation.HydrogenGas;
            if (amounts.ContainsKey(Precipitate)) effects |= Observation.Sediment;
            return effects;
        }

        internal static string ItemId(string key) => key == Hydrogen ? ChemistryCatalog.ElementItemId("H") : ChemistryCatalog.CompoundItemId(key);

        internal static (Observation Effects, int Ticks) Resolve(Dictionary<string, decimal> amounts)
        {
            Observation effects = Observation.None;
            int ticks = 0;
            foreach (Equation equation in Equations)
            {
                decimal extent = equation.Reactants.Min(term => amounts.TryGetValue(term.Key, out decimal amount)
                    ? amount / term.Coefficient : 0m);
                if (extent < Trace) continue;
                foreach (Term term in equation.Reactants)
                {
                    decimal remaining = amounts[term.Key] - extent * term.Coefficient;
                    if (remaining < Trace) amounts.Remove(term.Key);
                    else amounts[term.Key] = remaining;
                }
                foreach (Term term in equation.Products)
                {
                    Add(amounts, term.Key, extent * term.Coefficient);
                    if (term.Key == CarbonDioxide) effects |= Observation.Gas;
                    if (term.Key == Hydrogen) effects |= Observation.HydrogenGas;
                    if (term.Key == Precipitate) effects |= Observation.Sediment;
                }
                if (equation.Reactants[0].Key == Acetic && !equation.Products.Any(term => term.Key == CarbonDioxide))
                    effects |= Observation.Neutralization;
                ticks += equation.Ticks;
            }
            if (!amounts.ContainsKey(Precipitate)) effects &= ~Observation.Sediment;
            return (effects, ticks);
        }

        internal static string Describe(Observation effects)
        {
            bool gas = (effects & Observation.Gas) != 0;
            bool hydrogen = (effects & Observation.HydrogenGas) != 0;
            bool sediment = (effects & Observation.Sediment) != 0;
            if ((gas || hydrogen) && sediment) return Loc.Pick("冒出了气泡，瓶底出现白色沉淀。", "Bubbles formed and white sediment appeared.");
            if (gas && hydrogen) return Loc.Pick("冒出了氢气和二氧化碳。", "Hydrogen and carbon dioxide bubbles formed.");
            if (hydrogen) return Loc.Pick("液体起泡，放出了氢气。", "The liquid bubbled and released hydrogen.");
            if (gas) return Loc.Pick("冒出了二氧化碳气泡。", "Carbon dioxide bubbles formed.");
            if (sediment) return Loc.Pick("瓶底出现了白色沉淀。", "White sediment appeared.");
            return (effects & Observation.Neutralization) != 0 ? Loc.Pick("发生了酸碱中和。", "The acid and base reacted.")
                : Loc.Pick("反应完成。", "Reaction complete.");
        }

        private static void Add(Dictionary<string, decimal> amounts, string key, decimal addition)
        {
            amounts.TryGetValue(key, out decimal amount);
            amounts[key] = amount + addition;
        }

        internal static string Name(string key)
        {
            if (key == Hydrogen) return Loc.Pick("氢气", "Hydrogen");
            if (key == Sodium) return Loc.Pick("钠", "Sodium");
            CompoundDef compound = Compound(key);
            return Loc.Pick(compound.NameZh, compound.NameEn);
        }

        internal static bool CanPrepare(GameItem item)
        {
            if (item == null || item.unitCount <= 0) return false;
            string id = item.identifier;
            if (id == LabMixtureData.Water || id == CompoundPrefix + Acetic || LiquidComposition.HasAqueousContents(item)
                || (ReagentBottle.IsLiquid(id) && IsCompound(id))) return true;
            return LiquidComposition.IsMixture(item.identifier) && item.unitCount > 0;
        }

        internal static bool HasWater(GameItem sample) => Read(sample, sample.unitCount).TryGetValue(Water, out decimal amount) && amount > 0m;

        internal static bool CanRecoverWater(GameItem sample)
        {
            if (sample == null || sample.unitCount <= 0 || !LiquidComposition.HasAqueousContents(sample)) return false;
            var carrier = LiquidComposition.Measure(sample, sample.unitCount);
            if (carrier.Count != 1 || !carrier.ContainsKey(LabMixtureData.Water)) return false;
            var amounts = Read(sample, sample.unitCount);
            return amounts.ContainsKey(Water) && amounts.Keys.Any(IsRecoverableSolute)
                && amounts.Keys.All(key => key == Water || key == Precipitate || IsGas(key) || IsRecoverableSolute(key));
        }

        private static bool IsRecoverableSolute(string key) => Array.IndexOf(Powders, key) >= 0
            || key is "sodium_acetate" or "potassium_acetate" or "calcium_acetate" or "sodium_chloride" or "potassium_chloride";

        internal static bool NeedsPhaseCollection(Dictionary<string, decimal> amounts) => amounts.Any(entry =>
            (IsGas(entry.Key) || entry.Key == Precipitate) && entry.Value >= 1m);

        internal static (int Water, int Remaining, string Solid, int SolidUnits) PlanWaterRecovery(
            Dictionary<string, decimal> amounts, int inputUnits, int receiverCapacity)
        {
            if (inputUnits <= 0 || receiverCapacity <= 0 || NeedsPhaseCollection(amounts)) return default;
            var residues = amounts.Where(entry => entry.Key != Water && entry.Value > 0m).ToArray();
            if (receiverCapacity >= inputUnits && residues.Length == 1 && IsRecoverableSolute(residues[0].Key)
                && residues[0].Value >= 1m && residues[0].Value <= int.MaxValue && decimal.Truncate(residues[0].Value) == residues[0].Value)
                return (inputUnits, 0, residues[0].Key, (int)residues[0].Value);

            int water = Math.Min(receiverCapacity, inputUnits - 1);
            return (water, inputUnits - water, null, 0);
        }

        internal static string ConcentratedSample(Dictionary<string, decimal> amounts, int originalVolume, int remainingVolume)
        {
            amounts[Water] *= (decimal)remainingVolume / originalVolume;
            return Encode(FormattableString.Invariant($"{LabMixtureData.Water}:{remainingVolume}"), amounts);
        }

        internal static string Acidity(GameItem sample)
        {
            if (!LiquidComposition.HasAqueousContents(sample)) return null;
            var amounts = Read(sample, sample.unitCount);
            if (amounts.ContainsKey(Acetic)) return Loc.Pick("溶液偏酸。", "The solution is acidic.");
            return amounts.ContainsKey("sodium_hydroxide") || amounts.ContainsKey("potassium_hydroxide")
                || amounts.ContainsKey("calcium_hydroxide") ? Loc.Pick("溶液偏碱。", "The solution is alkaline.") : null;
        }

        internal static void AddSolid(Dictionary<string, decimal> amounts, string id)
        {
            if (IsSodium(id)) Add(amounts, Sodium, 2m);
            else Add(amounts, id.Substring(CompoundPrefix.Length), 1m);
        }

        internal static Dictionary<string, decimal> Read(GameItem item, int units)
        {
            var volumes = LiquidComposition.Measure(item, units);
            string encoded = LiquidComposition.Saved(item);
            int separator = encoded?.IndexOf('!') ?? -1;
            if (separator >= 0)
            {
                decimal referenceVolume = LiquidComposition.ReferenceUnits(encoded.Substring(0, separator));
                string payload = encoded.Substring(separator + 1);
                bool hasWaterBalance = payload.StartsWith(WaterIncluded, StringComparison.Ordinal);
                var amounts = Decode(hasWaterBalance ? payload.Substring(WaterIncluded.Length) : payload);
                if (referenceVolume <= 0m || units <= 0) return new Dictionary<string, decimal>();
                foreach (string key in amounts.Keys.ToArray()) amounts[key] = amounts[key] * units / referenceVolume;
                if (!hasWaterBalance && volumes.TryGetValue(LabMixtureData.Water, out decimal water)) Add(amounts, Water, water * WaterPerVolumeUnit);
                return amounts;
            }
            var stock = new Dictionary<string, decimal>();
            foreach (KeyValuePair<string, decimal> entry in volumes)
            {
                if (!entry.Key.StartsWith(CompoundPrefix, StringComparison.Ordinal)) continue;
                string key = entry.Key.Substring(CompoundPrefix.Length);
                if (entry.Key == LabMixtureData.Water) Add(stock, Water, entry.Value * WaterPerVolumeUnit);
                else if (ChemistryCatalog.FindCompound(key) != null && ReagentBottle.IsLiquid(entry.Key)) Add(stock, key, entry.Value);
            }
            return stock;
        }

        internal static (Dictionary<string, decimal> Amounts, Observation Effects, int Ticks) Plan(GameItem target,
            int targetUnits, GameItem source, int sourceUnits)
        {
            var amounts = Read(target, targetUnits);
            foreach (var entry in Read(source, sourceUnits)) Add(amounts, entry.Key, entry.Value);
            var applied = Resolve(amounts);
            return (amounts, applied.Effects, applied.Ticks);
        }

        internal static string Encode(string carrier, Dictionary<string, decimal> amounts) => carrier + "!" + WaterIncluded + string.Join("/", amounts
            .Where(entry => entry.Value >= Trace).OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => FormattableString.Invariant($"{entry.Key}:{entry.Value}")));

        private static Dictionary<string, decimal> Decode(string encoded)
        {
            var amounts = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (string entry in encoded.Split('/'))
            {
                int separator = entry.IndexOf(':');
                if (separator <= 0 || !decimal.TryParse(entry.Substring(separator + 1), NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out decimal amount) || amount <= 0m || amount > int.MaxValue) continue;
                string key = entry.Substring(0, separator);
                if (key is Sodium or Hydrogen || ChemistryCatalog.FindCompound(key) != null) amounts[key] = amount;
            }
            return amounts;
        }
    }
}
