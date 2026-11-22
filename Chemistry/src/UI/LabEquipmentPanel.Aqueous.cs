using System;
using System.Collections.Generic;

namespace Chemistry
{
    internal sealed partial class LabEquipmentPanel
    {
        private static (Dictionary<string, decimal> Contents, AqueousReactions.Observation Effects, int Ticks) PlanSolidAddition(GameItem sample, GameItem solid)
        {
            var contents = AqueousReactions.Read(sample, sample.unitCount);
            AqueousReactions.AddSolid(contents, solid.identifier);
            var result = AqueousReactions.Resolve(contents);
            return (contents, result.Effects, result.Ticks);
        }

        private string PrepareAqueousSample(LabInventoryGrid grid, GameItem sample)
        {
            GameItem solid = Receiver;
            var result = PlanSolidAddition(sample, solid);
            if (!MachineBatch.TryCommitMeasured(grid, new List<(GameItem, int)> { (sample, sample.unitCount), (solid, 1) },
                AqueousOutput(sample, result.Contents, output))) return Failed;
            if (result.Ticks == 0) return Loc.Pick("已配成溶液，可以继续加料。", "Solution ready. You can add another reagent.");
            LiquidMotion.BeginReaction(output.childItem);
            return AqueousReactions.Describe(result.Effects);
        }

        private static MachineBatch.Product AqueousOutput(GameItem input, Dictionary<string, decimal> contents, GameInventory destination) =>
            new(input.identifier == LabMixtureData.UnknownPlastic ? LabMixtureData.UnknownPlastic : LabMixtureData.Unknown,
                destination, input.unitCount, ReagentBottle.SizeOf(input), AqueousReactions.Encode(
                    LiquidComposition.Encode(LiquidComposition.Measure(input, input.unitCount)), contents));

        private static bool CanCollectAqueous(GameItem input, string species) => input != null
            && ReagentBottle.SizeOf(input) != ReagentBottle.Bulk && LiquidComposition.HasAqueousContents(input)
            && AqueousReactions.Read(input, input.unitCount).TryGetValue(species, out decimal amount) && amount >= 1m;

        private static string CollectableGas(GameItem input)
        {
            if (CanCollectAqueous(input, AqueousReactions.Hydrogen)) return AqueousReactions.Hydrogen;
            return CanCollectAqueous(input, AqueousReactions.CarbonDioxide) ? AqueousReactions.CarbonDioxide : null;
        }

        private string OperateAqueousCollection(LabInventoryGrid grid, GameItem input)
        {
            string species = Task switch
            {
                EquipmentTask.Filter or EquipmentTask.Clarify => AqueousReactions.Precipitate,
                EquipmentTask.GasCollect => CollectableGas(input),
                _ => null
            };
            if (species == null || !CanCollectAqueous(input, species)) return null;

            bool gas = Task == EquipmentTask.GasCollect;
            var inputs = new List<(GameItem, int)> { (input, input.unitCount) };
            var contents = AqueousReactions.Read(input, input.unitCount);
            int units = (int)Math.Floor(contents[species]);
            int size = -1;
            if (gas)
            {
                inputs.Add((Receiver, 1));
                units = Math.Min(units, ReagentBottle.CapacityFor(Receiver));
                size = ReagentBottle.SizeOf(Receiver);
            }
            contents[species] -= units;
            var recovered = new MachineBatch.Product(AqueousReactions.ItemId(species), gas ? output : byproduct, units, size);
            var liquid = AqueousOutput(input, contents, gas ? byproduct : output);
            return MachineBatch.TryCommitMeasured(grid, inputs, liquid, recovered)
                ? Loc.Pick("已收集 ", "Collected ") + AqueousReactions.Name(species)
                : Failed;
        }
    }
}
