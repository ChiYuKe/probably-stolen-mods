using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Chemistry
{
    internal static class LiquidComposition
    {
        private const string CompositionTag = "chemistry_liquid_composition";
        internal const string Unidentified = "?";

        internal static bool IsMixture(string id) => id is LabMixtureData.Unknown or LabMixtureData.UnknownPlastic;

        // The stored amounts describe a reference portion. Scaling them preserves the recipe when a bottle is partly poured out.
        internal static Dictionary<string, decimal> Measure(string id, string stored, int units)
        {
            var amounts = new Dictionary<string, decimal>(StringComparer.Ordinal);
            if (units <= 0) return amounts;
            if (!IsMixture(id))
            {
                amounts.Add(id, units);
                return amounts;
            }

            amounts = Decode(stored);
            if (amounts.Count == 0)
            {
                amounts.Add(Unidentified, units);
                return amounts;
            }

            decimal referenceUnits = amounts.Values.Sum();
            string largest = amounts.OrderByDescending(entry => entry.Value).First().Key;
            decimal remaining = units;
            foreach (string component in amounts.Keys.ToArray())
            {
                if (component == largest) continue;
                decimal share = amounts[component] * units / referenceUnits;
                amounts[component] = share;
                remaining -= share;
            }
            amounts[largest] = remaining;
            return amounts;
        }

        internal static Dictionary<string, decimal> Measure(GameItem item, int units) =>
            Measure(item.identifier, Saved(item), units);

        internal static string Combine(GameItem target, int targetUnits, GameItem source, int sourceUnits)
        {
            var contents = Measure(target, targetUnits);
            foreach (var (component, amount) in Measure(source, sourceUnits))
            {
                contents.TryGetValue(component, out decimal current);
                contents[component] = current + amount;
            }
            return Encode(contents);
        }

        internal static string Encode(Dictionary<string, decimal> contents) => string.Join("/", contents
            .Where(entry => entry.Value > 0m).OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => FormattableString.Invariant($"{entry.Key}:{entry.Value}")));

        private static Dictionary<string, decimal> Decode(string text)
        {
            var contents = new Dictionary<string, decimal>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return contents;
            int separator = text.IndexOf('!');
            if (separator >= 0) text = text.Substring(0, separator);
            foreach (string entry in text.Split('/'))
            {
                string[] parts = entry.Split(':');
                if (parts.Length != 2 || (parts[0] != Unidentified && (IsMixture(parts[0]) || !ReagentBottle.IsLiquid(parts[0])))
                    || !decimal.TryParse(parts[1], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal units)
                    || units <= 0m || units > int.MaxValue || !contents.TryAdd(parts[0], units))
                {
                    contents.Clear();
                    break;
                }
            }
            return contents;
        }

        internal static string Saved(GameItem item) => IsMixture(item.identifier)
            && item.state.dict.TryGetValue(CompositionTag, out TagState tag) ? tag.GetString() : null;

        internal static decimal ReferenceUnits(string carrier) => Decode(carrier).Values.Sum();

        internal static bool HasAqueousContents(GameItem item) => Saved(item)?.Contains('!') == true;

        internal static void Restore(GameItem item, string value)
        {
            if (IsMixture(item.identifier) && Decode(value).Count > 0)
                item.ModifyTag(CompositionTag, tag => tag.SetString(value), skipValidate: true);
        }

        internal static void AddTooltip(RichTextBuilder builder, GameItem item)
        {
            int total = item.unitCount;
            if (total <= 0) return;
            builder.AddLine(Loc.Pick("投料组成（按体积）", "Ingredients by volume"), false);
            foreach (string line in ComponentLines(item)) builder.AddLine(line, false);
            if (!HasAqueousContents(item)) return;
            var contents = AqueousReactions.Read(item, total);
            foreach (string line in DescribeAqueousContents(contents)) builder.AddLine(line, false);
            if (AqueousReactions.CanRecoverWater(item))
            {
                string route;
                if (ReagentBottle.SizeOf(item) == ReagentBottle.Bulk)
                    route = Loc.Pick("先分装到试剂瓶，再用蒸馏器回收水。", "Bottling a portion makes it ready for distillation.");
                else if (AqueousReactions.NeedsPhaseCollection(contents))
                    route = Loc.Pick("先收集气体或过滤沉淀，再蒸馏回收水。", "Collect gas or filter sediment, then distill the water.");
                else
                {
                    var recovery = AqueousReactions.PlanWaterRecovery(contents, total, total);
                    route = recovery.Solid != null ? Loc.Pick($"可蒸馏回收水和{AqueousReactions.Name(recovery.Solid)}。",
                        $"Distill to recover water and {AqueousReactions.Name(recovery.Solid)}.")
                        : recovery.Water > 0 ? Loc.Pick("可蒸馏回收水，留下浓缩液。", "Distill to recover water and leave a concentrate.")
                        : Loc.Pick("已浓缩，暂时无法再分出水。", "Concentrated. There is not enough water for another portion.");
                }
                builder.AddLine(route, false);
            }
        }

        private static string Format(decimal value) => value is > 0m and < 0.01m ? "<0.01" : value.ToString("0.##");

        private static IEnumerable<string> ComponentLines(GameItem item)
        {
            int total = item.unitCount;
            foreach (var (component, units) in Measure(item, total).OrderByDescending(entry => entry.Value))
            {
                string volume = Format(units * ReagentBottle.MillilitersPerUnit);
                string percent = Format(100m * units / total);
                yield return $"{Name(component)} · {volume} mL · {percent}%";
            }
        }

        internal static string Report(GameItem item)
        {
            IEnumerable<string> reportLines = HasAqueousContents(item)
                ? DescribeAqueousContents(AqueousReactions.Read(item, item.unitCount)).Append(
                    Loc.Pick("样品 ", "Sample ") + ((long)item.unitCount * ReagentBottle.MillilitersPerUnit) + " mL")
                : ComponentLines(item);
            var lines = reportLines.Take(4).ToArray();
            string report = string.Join("\n", lines.Take(3));
            if (lines.Length > 3) report += "\n" + Loc.Pick("其余成分见物品详情。", "See the item details for the remaining ingredients.");
            return report;
        }

        private static IEnumerable<string> DescribeAqueousContents(Dictionary<string, decimal> contents)
        {
            var dissolved = contents.Keys.Where(species => !AqueousReactions.IsGas(species)
                && species != AqueousReactions.Precipitate && species != "water")
                .OrderBy(species => species, StringComparer.Ordinal).Select(AqueousReactions.Name).ToArray();
            if (dissolved.Length > 0)
                yield return Loc.Pick("溶液里有 ", "Dissolved: ") + string.Join(Loc.Pick("、", ", "), dissolved);
            foreach (var (species, gas) in contents.Where(entry => AqueousReactions.IsGas(entry.Key)))
            {
                string name = AqueousReactions.Name(species);
                yield return gas >= 1m ? Loc.Pick($"含{name}，可用排风柜收集。", $"Contains {name}. Collect it at the fume hood.")
                    : Loc.Pick($"含少量{name}。", $"Contains a little {name}.");
            }
            if (contents.TryGetValue(AqueousReactions.Precipitate, out decimal sediment) && sediment > 0m)
                yield return sediment >= 1m ? Loc.Pick("有白色沉淀，可过滤回收。", "White sediment. Recover it by filtering.")
                    : Loc.Pick("有少量白色沉淀。", "Contains a little white sediment.");
        }

        private static string Name(string id)
        {
            if (id == Unidentified) return Loc.Pick("成分不详", "Unknown ingredients");
            string compoundPrefix = ChemistryCatalog.ItemPrefix + "cmp_";
            if (id.StartsWith(compoundPrefix, StringComparison.Ordinal)
                && ChemistryCatalog.FindCompound(id.Substring(compoundPrefix.Length)) is CompoundDef compound)
                return Loc.Pick(compound.NameZh, compound.NameEn);
            if (ChemistryCatalog.FindElementByItemId(id) is ElementDef element) return Loc.Pick(element.NameZh, element.NameEn);
            if (LabAnalysisData.IsKnown(id)) return LabAnalysisData.Name(id);
            if (LabMixtureData.IsKnown(id)) return LabMixtureData.Name(id);
            if (LabProcessData.IsKnown(id)) return LabProcessData.Name(id);
            return id;
        }
    }
}
