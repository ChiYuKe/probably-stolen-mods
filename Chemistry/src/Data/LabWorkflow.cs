using System;
using System.Collections.Generic;
using System.Linq;

namespace Chemistry
{
    internal static class LabWorkflow
    {
        private static readonly Dictionary<string, CompoundDef> Compounds = ChemistryCatalog.Compounds.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        private static readonly Dictionary<string, ElementDef> Elements = ChemistryCatalog.Elements.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        private static readonly Dictionary<string, ToolDef> Tools = ChemistryCatalog.Tools.ToDictionary(item => item.ItemId, StringComparer.Ordinal);

        internal static bool IsReferenceMaterial(string id) => id != null && (Elements.ContainsKey(id) || Compounds.ContainsKey(id));

        internal static bool CanRead(string id) => IsReferenceMaterial(id) || LabMixtureData.IsKnown(id) || LabAnalysisData.IsKnown(id)
            || LabProcessData.IsKnown(id) || LabRefining.FindIntermediate(id) != null;

        internal static bool CanReadSolid(string id) => (IsReferenceMaterial(id) || id is LabMixtureData.Cake or LabMixtureData.SaltCake
            or LabAnalysisData.Carbon or LabAnalysisData.Spent) && !ReagentBottle.IsLiquid(id) && !ReagentBottle.IsGas(id);

        internal static bool CanReadFluid(string id) => CanRead(id)
            && (ReagentBottle.IsLiquid(id) || ReagentBottle.IsGas(id) || LabProcessData.Read(id).Units > 0);

        internal static string ReadSample(GameItem item)
        {
            string id = item.identifier;
            if (LiquidComposition.IsMixture(id)) return LiquidComposition.Report(item);
            if (LabTitration.IsProduct(id)) return LabTitration.Describe(LabTitration.Read(item, item.unitCount));
            if (LabRefining.FindIntermediate(id) is LabRefining.Intermediate intermediate) return intermediate.Name + "\n" + intermediate.Instructions;
            var batch = LabProcessData.Read(id);
            if (batch.Units > 0) return batch.Contents;

            string formula = Elements.TryGetValue(id, out ElementDef element) ? element.Symbol
                : Compounds.TryGetValue(id, out CompoundDef compound) ? ChemistryCatalog.FormatFormulaForDisplay(compound.Formula) : null;
            string identity = Loc.Pick("检出 ", "Detected ") + (formula == null ? item.name : formula);
            if (ReagentBottle.IsLiquid(id) || ReagentBottle.IsGas(id))
                return identity + " · " + ((long)item.unitCount * ReagentBottle.MillilitersPerUnit) + " mL";
            return identity + " · " + Loc.Pick("固体样品", "Solid sample");
        }

        internal static void AddTooltip(RichTextBuilder builder, GameItem item)
        {
            if (Describe(item.identifier) is string route)
                builder.AddLine(Loc.Pick("处理流程  ", "Workflow  ") + route, false);
        }

        internal static string Describe(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Compounds.TryGetValue(id, out CompoundDef compound)) return CompoundRoute(compound);
            if (Elements.ContainsKey(id))
                return Loc.Pick("可在垃圾场找到元素样品。", "Find element samples at the dumping ground. ")
                    + MaterialHandling(id) + Loc.Pick("，也可用于配方合成。", ", or use it in synthesis.");
            if (Tools.TryGetValue(id, out ToolDef tool))
            {
                var equipment = LabEquipmentData.Find(tool.Key);
                return equipment == null ? Loc.Pick("配方工具或取样器材。", "Use for recipes or sampling.")
                    : Loc.Pick("在地下实验室双击，执行「", "Double-click in the underground lab to run ") + equipment.ActionLabel
                        + Loc.Pick($"」，第 {equipment.UnlockDay} 天解锁。", $" (day {equipment.UnlockDay}).");
            }
            if (SampleTable.Find(id) is SampleDef sample)
            {
                if (id != sample.DetectedId) return Loc.Pick("用光谱仪鉴定，随后查看成分报告。", "Identify it with the spectrometer, then open its report.");
                return sample.Components.Any(component => LabRefining.IsAcidTarget(sample, component.Symbol))
                    ? Loc.Pick("打开成分报告选择目标，再送入提炼反应台。", "Select a target in the report and load the refining station.")
                    : Loc.Pick("打开成分报告查看可回收组分。", "Open the report to see recoverable components.");
            }
            if (LabRefining.FindIntermediate(id) is LabRefining.Intermediate intermediate) return intermediate.Instructions;
            if (LabProcessData.IsKnown(id)) return LabProcessData.Guide(id);
            if (LabAnalysisData.IsKnown(id)) return AnalysisRoute(id);
            if (LabMixtureData.IsKnown(id)) return LiquidComposition.IsMixture(id)
                ? Loc.Pick("先看瓶内成分，再选择分装、检测或分离。", "Check the contents to choose transfer, analysis or separation.")
                : LabMixtureData.Description(id);
            if (ReagentBottle.IsDamaged(id)) return Loc.Pick("拖到废物桶，回收占用的库存空间。", "Drop it onto a solid waste bin to clear inventory space.");
            if (BulkContainers.IsSolidWaste(id)) return Loc.Pick("接收废料和破损容器。", "Accepts scrap and damaged containers.");
            if (BulkContainers.IsLiquidWaste(id)) return Loc.Pick("接收待丢弃的液体。", "Accepts liquid for disposal.");
            if (ReagentBottle.IsGasEmpty(id)) return Loc.Pick("接收气体，或装入排风柜的接收槽。", "Receive transferred gas or load it in the fume hood.");
            if (ReagentBottle.IsLiquidEmpty(id)) return Loc.Pick("接收相容液体，可用于分装和设备接收槽。", "Receive compatible liquids during transfers and equipment runs.");
            if (id == AcidStation.ItemId || id == RefiningStation.ItemId)
                return Loc.Pick("在地下实验室双击，打开设备操作面板。", "Double-click in the underground lab to open the machine.");
            if (NitricProductionData.Find(id) != null) return Loc.Pick("在生产线面板按槽位装配或补料。", "Install or supply it using the production line panel.");
            if (id == IndustrialFactoryData.Frame) return Loc.Pick("双击厂房，安装设备并选择生产线。", "Open the workshop, install the equipment and choose a route.");
            if (id == IndustrialFactoryData.Transport) return Loc.Pick("装入自动化厂房的运输槽。", "Install in the workshop's transport slot.");
            return null;
        }

        private static string CompoundRoute(CompoundDef compound)
        {
            if (compound.Key == NitricProductionData.Key) return Loc.Pick("在地下实验室生产线制备，按设备面板收集。", "Use the underground production line and collect its output.");
            if (AcidStation.HasRecipe(compound)) return Loc.Pick("用制酸装置制备，成品可分装或供设备使用。", "Prepare at the acid station. Transfer the product or feed it into equipment.");
            if (compound.ItemId == LabMixtureData.Salt)
                return Loc.Pick("配方合成 → 烧杯配盐液 → 蒸馏回水或冷却析晶。", "Synthesize → dissolve in the beaker → distill or crystallize.");
            if (AqueousReactions.IsSolidReagent(compound.ItemId))
                return Loc.Pick("配方合成 → 烧杯加料 → 按样品详情分离产物。", "Synthesize → add in the beaker → separate as indicated on the sample.");
            string source = compound.Category == "acid" ? Loc.Pick("先用配方合成。", "Synthesize it from a recipe. ")
                : Loc.Pick("可从掉落获取或用配方合成。", "Find in loot or synthesize it. ");
            return source + MaterialHandling(compound.ItemId);
        }

        private static string MaterialHandling(string id)
        {
            if (AqueousReactions.IsSodium(id)) return Loc.Pick("可作为固体放入烧杯加料槽", "Load as a solid in the beaker");
            if (ReagentBottle.IsGas(id)) return Loc.Pick("可分装到采样气瓶或钢瓶，也可用色谱仪检测", "Transfer to a vial or cylinder, or analyze by chromatography");
            if (ReagentBottle.IsLiquid(id)) return Loc.Pick("可用移液管分装、用色谱仪检测", "Aliquot with the pipette or analyze by chromatography");
            return Loc.Pick("可用显微镜观察或称量", "Inspect under the microscope or weigh it");
        }

        private static string AnalysisRoute(string id) => id switch
        {
            LabAnalysisData.Carbon => Loc.Pick("装入过滤漏斗，对示踪液做吸附处理。", "Load in the filter funnel to remove dye."),
            LabAnalysisData.Spent => Loc.Pick("投入固体废物桶。", "Dispose of in a solid waste bin."),
            LabAnalysisData.Dye or LabAnalysisData.Dilute or LabAnalysisData.Treated =>
                Loc.Pick("送光谱仪检测，使用水作对照。", "Run in the spectrometer with a water blank."),
            LabAnalysisData.Titrant => Loc.Pick("加入滴定装置，处理教学酸样。", "Load in the titration set to treat the training acid."),
            _ => Loc.Pick("用滴定装置继续处理，或在 pH 计读数。", "Continue in the titration set or check it with the pH meter.")
        };
    }
}
