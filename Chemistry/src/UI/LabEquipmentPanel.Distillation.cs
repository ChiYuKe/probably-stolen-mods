using System.Collections.Generic;

namespace Chemistry
{
    internal sealed partial class LabEquipmentPanel
    {
        private string TemperatureText()
        {
            bool vacuum = equipment.Key == "rotary_evaporator";
            int degrees = LabDistillationData.Temperature(level, vacuum);
            return (vacuum ? Loc.Pick("减压", "Vacuum") : Loc.Pick("常压", "Atmospheric")) + $" · {degrees} °C";
        }

        private string DistillationBlocker()
        {
            if (AqueousReactions.CanRecoverWater(Input)) return WaterRecoveryBlocker();
            string reason = LabDistillationData.Blocker(Input.identifier, level);
            if (reason != null) return reason;
            GameItem bottle = Receiver;
            if (!IsPortableGlass(bottle) || bottle.unitCount != 1)
                return Loc.Pick("装入 1 个空玻璃试剂瓶", "Load one empty glass reagent bottle");
            var batch = LabDistillationData.Products(Input.identifier, level);
            return ReagentBottle.CapacityFor(bottle) < batch.Units
                ? Loc.Pick("接收瓶至少需要 ", "Receiver needs at least ") + Volume(batch.Units) : null;
        }

        private string DistillationPreview()
        {
            if (AqueousReactions.CanRecoverWater(Input))
            {
                var recovery = PlanWaterRecovery(Input);
                return WaterRecoverySummary(recovery.Water, recovery.Solid);
            }
            var batch = LabDistillationData.Products(Input.identifier, level);
            string name = batch.Distillate == Water ? Loc.Pick("水", "Water") : LabMixtureData.Name(batch.Distillate);
            return name + " · " + Volume(batch.Units);
        }

        private static string DistillationPhase(float elapsed)
        {
            if (elapsed < 0.26f) return Loc.Pick("加热升温…", "Heating...");
            if (elapsed < 0.58f) return Loc.Pick("液体蒸发…", "Evaporating...");
            if (elapsed < 0.85f) return Loc.Pick("蒸气冷凝…", "Condensing...");
            return Loc.Pick("收集冷凝液…", "Collecting condensate...");
        }

        private string Distill(LabInventoryGrid grid, GameItem input)
        {
            if (AqueousReactions.CanRecoverWater(input)) return RecoverWater(grid, input);
            GameItem bottle = Receiver;
            var batch = LabDistillationData.Products(input.identifier, level);
            string summary = DistillationPreview();
            var products = new List<MachineBatch.Product>
            {
                new MachineBatch.Product(batch.Distillate, output, batch.Units, ReagentBottle.SizeOf(bottle))
            };
            if (batch.Residue != null)
            {
                products.Add(new MachineBatch.Product(batch.Residue, byproduct));
            }
            if (input.identifier == LabMixtureData.Brine)
            {
                products.Add(new MachineBatch.Product(ReagentBottle.GlassEmpties[LabMixtureData.SmallBottle], receiver));
                summary += "\n" + Loc.Pick("空原瓶退回接收槽", "Source bottle returned to receiver slot");
            }
            var inputs = new List<(GameItem, int)> { (input, 1), (bottle, 1) };
            return MachineBatch.TryCommitMeasured(grid, inputs, products.ToArray()) ? summary : Failed;
        }

        private string WaterRecoveryBlocker()
        {
            if (ReagentBottle.SizeOf(Input) == ReagentBottle.Bulk)
                return Loc.Pick("先从大液桶分装到试剂瓶", "Transfer some solution from the bulk drum to a reagent bottle first");
            if (AqueousReactions.NeedsPhaseCollection(AqueousReactions.Read(Input, Input.unitCount)))
                return Loc.Pick("请先收集气体或过滤沉淀", "Collect the gas or filter the sediment first");
            if (!IsPortableGlass(Receiver) || Receiver.unitCount != 1)
                return Loc.Pick("装入空玻璃瓶接回冷凝水", "Load an empty glass bottle for the condensate");
            return PlanWaterRecovery(Input).Water > 0 ? null
                : Loc.Pick("只剩少量浓缩液，无法再分离一格水", "Only a little concentrate remains. There is not enough water for another portion.");
        }

        private (int Water, int Remaining, string Solid, int SolidUnits) PlanWaterRecovery(GameItem sample) =>
            AqueousReactions.PlanWaterRecovery(AqueousReactions.Read(sample, sample.unitCount), sample.unitCount,
                ReagentBottle.CapacityFor(Receiver));

        private static string WaterRecoverySummary(int units, string solid) =>
            Loc.Pick("冷凝水 ", "Condensed water ") + Volume(units) + " · "
            + (solid == null ? Loc.Pick("留下浓缩液", "concentrate remains") : AqueousReactions.Name(solid));

        private string RecoverWater(LabInventoryGrid grid, GameItem input)
        {
            var recovery = PlanWaterRecovery(input);
            if (recovery.Water <= 0) return Failed;
            var inputs = new List<(GameItem, int)> { (input, input.unitCount), (Receiver, 1) };
            var products = new List<MachineBatch.Product>
            {
                new(Water, output, recovery.Water, ReagentBottle.SizeOf(Receiver))
            };
            if (recovery.Solid != null)
            {
                products.Add(new MachineBatch.Product(AqueousReactions.ItemId(recovery.Solid), byproduct, recovery.SolidUnits));
                products.Add(new MachineBatch.Product(ReagentBottle.ReturnedEmpty(input), receiver));
            }
            else
            {
                string composition = AqueousReactions.ConcentratedSample(AqueousReactions.Read(input, input.unitCount),
                    input.unitCount, recovery.Remaining);
                products.Add(new MachineBatch.Product(input.identifier, byproduct, recovery.Remaining, ReagentBottle.SizeOf(input), composition));
            }
            return MachineBatch.TryCommitMeasured(grid, inputs, products.ToArray())
                ? WaterRecoverySummary(recovery.Water, recovery.Solid) : Failed;
        }
    }
}
