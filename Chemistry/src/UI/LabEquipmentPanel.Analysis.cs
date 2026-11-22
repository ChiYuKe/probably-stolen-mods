using System.Collections.Generic;

namespace Chemistry
{
    internal sealed partial class LabEquipmentPanel
    {
        private void BuildAnalysisReceiver(List<MachineryPanel> sections)
        {
            string heading = Task switch
            {
                EquipmentTask.Filter => Loc.Pick("吸附剂", "Adsorbent"),
                EquipmentTask.AnalyzeAny => Loc.Pick("空白对照", "Blank"),
                EquipmentTask.Titrate => Loc.Pick("标准滴定液", "Titrant"),
                _ => null
            };
            if (heading == null) return;
            receiver = new EquipmentSlot();
            receiver.mayInventoryAddItemFunc = (item, _) => CanLoad(receiver, item) && (Task switch
            {
                EquipmentTask.Filter => item.identifier == LabAnalysisData.Carbon,
                EquipmentTask.AnalyzeAny => item.identifier == Water,
                _ => item.identifier == LabAnalysisData.Titrant && ReagentBottle.SizeOf(item) != ReagentBottle.Bulk
            });
            sections.Add(MachineHelper.Panel(heading, receiver, "bottle"));
        }

        private string AnalysisBlocker()
        {
            string id = Input?.identifier;
            if (Task == EquipmentTask.AnalyzeAny && IsOpticalLiquid(id) && Receiver?.identifier != Water)
                return Loc.Pick("空白对照槽需放一瓶水", "Load a water blank");
            if (Task == EquipmentTask.Filter && LabAnalysisData.IsDye(id))
            {
                if (Receiver?.identifier != LabAnalysisData.Carbon)
                    return Loc.Pick("放入活性炭才能脱色", "Load activated carbon to remove the color");
                if (Input.unitCount > LabMixtureData.Portion * 2 || ReagentBottle.SizeOf(Input) == ReagentBottle.Bulk)
                    return Loc.Pick("请先分装到小、中或大瓶，每批最多 100 mL", "Use a portable bottle with at most 100 mL");
            }
            if (Task != EquipmentTask.Titrate) return null;
            if (Receiver?.identifier != LabAnalysisData.Titrant || Receiver.unitCount < level)
                return Loc.Pick("标准液不足，补充试液或降低滴加量", "Not enough titrant. Refill or lower the dose.");
            return Input.unitCount + level > ReagentBottle.CapacityFor(Input) || ReagentBottle.SizeOf(Input) == ReagentBottle.Bulk
                ? Loc.Pick("样品瓶容量不足，请换较大的采样瓶", "Use a larger portable sample bottle") : null;
        }

        private string AnalysisPhase() => Task switch
        {
            EquipmentTask.Filter when LabAnalysisData.IsDye(Input?.identifier) => Loc.Pick("活性炭吸附色料中…", "Carbon is adsorbing the dye..."),
            EquipmentTask.AnalyzeAny when IsOpticalLiquid(Input?.identifier) => Loc.Pick("校零并读取透光信号…", "Zeroing and reading transmitted light..."),
            EquipmentTask.Titrate => Loc.Pick("滴加标准液并检查颜色…", "Adding titrant and checking the color..."),
            _ => null
        };

        private string OperateAnalysis(LabInventoryGrid grid, GameItem input)
        {
            string id = input?.identifier;
            switch (Task)
            {
                case EquipmentTask.AnalyzeAny when LabAnalysisData.IsOpticalSample(id):
                    int concentration = LabAnalysisData.RelativeConcentration(id);
                    return Loc.Pick($"相对浓度 {concentration}% · 原液为 100%", $"Relative concentration {concentration}% · stock = 100%");
                case EquipmentTask.AnalyzeAny or EquipmentTask.AnalyzeSolid or EquipmentTask.AnalyzeFluid
                    when LabWorkflow.CanRead(id) && SampleTable.Find(id) == null:
                    return LabWorkflow.ReadSample(input);
                case EquipmentTask.Acidity when LabTitration.PhReading(input) is string ph:
                    return Loc.Pick("教学读数 · pH ", "Training reading · pH ") + ph;
                case EquipmentTask.Filter when LabAnalysisData.IsDye(id):
                    return MachineBatch.TryCommitMeasured(grid, new List<(GameItem, int)> { (input, input.unitCount), (Receiver, 1) },
                        new MachineBatch.Product(LabAnalysisData.Treated, output, input.unitCount, ReagentBottle.SizeOf(input)),
                        new MachineBatch.Product(LabAnalysisData.Spent, byproduct))
                        ? Loc.Pick("已脱色 · 可用光谱仪复测", "Decolorized. Recheck with the spectrometer.") : Failed;
                case EquipmentTask.Titrate:
                    return Titrate(grid, input, Receiver);
                default:
                    return null;
            }
        }

        private string DiluteTracer(LabInventoryGrid grid, GameItem water, GameItem tracer)
        {
            var inputs = new List<(GameItem, int)> { (water, LabMixtureData.Portion), (tracer, LabMixtureData.Portion) };
            return MachineBatch.TryCommitMeasured(grid, inputs,
                new MachineBatch.Product(LabAnalysisData.Dilute, output, LabMixtureData.Portion * 2, LabMixtureData.SmallBottle),
                new MachineBatch.Product(ReagentBottle.ReturnedEmpty(tracer), byproduct))
                ? Loc.Pick("已配制 100 mL 半浓度示踪液", "Prepared 100 mL of half-strength tracer") : Failed;
        }

        private string Titrate(LabInventoryGrid grid, GameItem input, GameItem titrant)
        {
            var batch = LabTitration.Combine(LabTitration.Read(input, input.unitCount), LabTitration.Read(titrant, level),
                input.unitCount + level);
            var inputs = new List<(GameItem, int)> { (input, input.unitCount), (titrant, level) };
            var products = new List<MachineBatch.Product>
            {
                new MachineBatch.Product(batch.Id, output, batch.Volume, ReagentBottle.SizeOf(input),
                    LabTitration.Encode(batch.Contents, batch.Volume))
            };
            if (titrant.unitCount == level)
                products.Add(new MachineBatch.Product(ReagentBottle.ReturnedEmpty(titrant), byproduct));
            if (!MachineBatch.TryCommitMeasured(grid, inputs, products.ToArray())) return Failed;
            return LabTitration.Describe(batch.Contents);
        }
    }
}
