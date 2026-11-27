using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Chemistry
{
    internal sealed class EncyclopediaEntry
    {
        internal string ItemId;
        internal string Name;
        internal string Formula;
        internal string CategoryKey;
        internal string CategoryLabel;
        internal string SpriteKey;
        internal string Description;
        internal string Operation;
        internal string Recipe;
        internal string ProcessFlow;
        internal EncyclopediaFlowStage[] FlowStages;
        internal string SearchText;
        internal int Value;
    }

    /// <summary>Builds the in-game reference index from the same tables used to register Chemistry items.</summary>
    internal static class EncyclopediaCatalog
    {
        internal static EncyclopediaEntry[] BuildEntries()
        {
            var entries = new List<EncyclopediaEntry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (ElementDef def in ChemistryCatalog.Elements)
            {
                string name = Loc.Pick(def.NameZh, def.NameEn) + " (" + def.Symbol + ")";
                string summary = Loc.Pick(def.SummaryZh, def.SummaryEn);
                var description = new StringBuilder();
                description.Append(Loc.Get("encyclopedia_element_number", "原子序数 {0} · 相对原子质量 {1}", "Atomic number {0} · relative atomic mass {1}",
                    def.Number, ChemUi.MassText(def.Mass)));
                string phase = ItemRegistration.PhaseLabel(def.Phase);
                if (!string.IsNullOrEmpty(phase)) description.Append("\n").Append(Loc.Get("encyclopedia_element_phase", "常温状态：{0}", "Phase at room temperature: {0}", phase));
                if (ItemRegistration.HasTemperature(def.MeltC)) description.Append("\n").Append(Loc.Get("encyclopedia_element_melt", "熔点 {0:0.#} °C", "Melting point {0:0.#} °C", def.MeltC));
                if (ItemRegistration.HasTemperature(def.BoilC)) description.Append("\n").Append(Loc.Get("encyclopedia_element_boil", "沸点 {0:0.#} °C", "Boiling point {0:0.#} °C", def.BoilC));
                if (!string.IsNullOrEmpty(summary)) description.Append("\n").Append(summary);
                Add(entries, ids, def.ItemId, name, def.Symbol, "elements",
                    Loc.Pick("元素 · ", "Element · ") + ChemistryCatalog.ElementCategoryLabel(def.Category, Loc.IsChinese),
                    def.SpriteKey, description.ToString(), LabWorkflow.Describe(def.ItemId), null,
                    ItemPricing.ElementUnitValue(def), def.Symbol);
            }

            foreach (CompoundDef def in ChemistryCatalog.Compounds)
            {
                Add(entries, ids, def.ItemId, Loc.Pick(def.NameZh, def.NameEn), def.Formula, "compounds",
                    Loc.Pick("化合物 · ", "Compound · ") + ChemistryCatalog.CompoundCategoryLabel(def.Category, Loc.IsChinese),
                    def.SpriteKey, Loc.Pick(def.DescZh, def.DescEn), LabWorkflow.Describe(def.ItemId), RecipeLine(def),
                    ItemPricing.CompoundDisplayValue(def), def.Formula);
            }

            foreach (ToolDef def in ChemistryCatalog.Tools)
            {
                LabEquipmentDef equipment = LabEquipmentData.Find(def.Key);
                string category = equipment == null ? "tools" : "equipment";
                string categoryLabel = equipment == null
                    ? Loc.Pick("器材 · ", "Tool · ") + ChemistryCatalog.ToolCategoryLabel(def.Category, Loc.IsChinese)
                    : Loc.Pick("仪器 · ", "Instrument · ") + ChemistryCatalog.ToolCategoryLabel(def.Category, Loc.IsChinese);
                Add(entries, ids, def.ItemId, Loc.Pick(def.NameZh, def.NameEn), null, category,
                    categoryLabel, MaterialItemArt.ToolSpriteKey(def.Key), Loc.Pick(def.DescZh, def.DescEn),
                    LabWorkflow.Describe(def.ItemId), null, ItemPricing.ToolUnitValue(def), def.Key);
            }

            foreach (SampleDef sample in SampleTable.All)
            {
                if (sample.ItemId != SampleTable.VanillaScrapId)
                {
                    AddSample(entries, ids, sample, false);
                }
                // 原版废金属本体不是模组物品，但分析后生成的记录物品属于模组。
                AddSample(entries, ids, sample, true);
            }

            foreach (LabRefining.Intermediate item in LabRefining.Intermediates)
            {
                Add(entries, ids, item.ItemId, item.Name, null, "materials", Loc.Pick("回收中间产物", "Recovery intermediate"),
                    item.SpriteKey, item.Instructions, LabWorkflow.Describe(item.ItemId), null, 1, item.Symbol);
            }

            foreach (NitricProductionData.Item item in NitricProductionData.Items)
            {
                Add(entries, ids, item.Id, item.Name, null, "components", Loc.Pick("生产线物料", "Production line supply"),
                    item.Sprite, Loc.Pick("用于硝酸生产线的进料、工艺液、维护耗材或设备组件。", "A feed, process fluid, maintenance supply or machine part for the nitric acid line."),
                    NitricProductionData.Workflow(item.Id), null, item.Value, item.Id);
            }

            foreach (string id in LabMixtureData.Items)
            {
                string sprite = ReagentBottle.IsLiquid(id) || ReagentBottle.IsGas(id)
                    ? MaterialItemArt.SpriteKeyFor(id.Substring(ChemistryCatalog.ItemPrefix.Length))
                    : id == LabMixtureData.Cake ? "cmp_calcium_carbonate"
                    : id == LabMixtureData.SaltCake ? "cmp_sodium_chloride" : "sample_liquid";
                Add(entries, ids, id, LabMixtureData.Name(id), null, "materials", Loc.Pick("混合物与实验产物", "Mixtures and lab products"),
                    sprite, LabMixtureData.Description(id), LabWorkflow.Describe(id), null, id == LabMixtureData.Oil ? 4 : 12, id);
            }

            foreach (string id in LabAnalysisData.Items)
            {
                string sprite = id == LabAnalysisData.Carbon ? "el_c"
                    : id == LabAnalysisData.Spent ? "sample_slag"
                    : ReagentBottle.IsLiquid(id) || ReagentBottle.IsGas(id)
                        ? MaterialItemArt.SpriteKeyFor(id.Substring(ChemistryCatalog.ItemPrefix.Length)) : "sample_liquid";
                Add(entries, ids, id, LabAnalysisData.Name(id), null, "materials", Loc.Pick("分析与教学样品", "Analysis and training sample"),
                    sprite, LabAnalysisData.Description(id), LabWorkflow.Describe(id), null, id == LabAnalysisData.Spent ? 1 : 5, id);
            }

            foreach (string id in LabProcessData.Items)
            {
                string sprite = ReagentBottle.IsLiquid(id) || ReagentBottle.IsGas(id)
                    ? MaterialItemArt.SpriteKeyFor(id.Substring(ChemistryCatalog.ItemPrefix.Length)) : "tool_test_tube";
                Add(entries, ids, id, LabProcessData.Name(id), null, "materials", Loc.Pick("实验流程样品", "Workflow sample"),
                    sprite, LabProcessData.Guide(id), LabWorkflow.Describe(id), null, 5, id);
            }

            foreach (string id in ReagentBottle.EmptyFamilies[0])
            {
                AddEmptyBottle(entries, ids, id);
            }
            foreach (string id in ReagentBottle.EmptyFamilies[1])
            {
                AddEmptyBottle(entries, ids, id);
            }
            foreach (string id in ReagentBottle.EmptyFamilies[2])
            {
                AddEmptyBottle(entries, ids, id);
            }
            foreach (string id in ReagentBottle.DamagedIds)
            {
                string desc = id == ReagentBottle.DamagedGasId
                    ? Loc.Pick("瓶壁开裂，无法密封或再次灌装。", "Cracked and unable to seal or refill.")
                    : id == ReagentBottle.DamagedPlasticId
                        ? Loc.Pick("瓶身变形，无法再次灌装。", "Deformed and unable to refill.")
                        : Loc.Pick("玻璃已被腐蚀穿孔，无法再次灌装。", "Corroded through and unable to refill.");
                Add(entries, ids, id, ReagentBottle.DamagedNameFor(id), null, "containers", Loc.Pick("破损容器", "Damaged container"),
                    ReagentBottle.DamagedSpriteFor(id), desc, LabWorkflow.Describe(id), null, 0, id);
            }

            foreach (string id in BulkContainers.ItemIds)
            {
                Add(entries, ids, id, BulkContainers.Name(id), null, "containers", Loc.Pick("大容量容器与废物桶", "Bulk container or waste bin"),
                    BulkContainers.Sprite(id), BulkContainers.Description(id), LabWorkflow.Describe(id), null,
                    ReagentBottle.IsEmptyTarget(id) ? 18 : 0, id);
            }

            Add(entries, ids, RefiningStation.ItemId, Loc.Pick("提炼反应台", "Refining Station"), null, "equipment",
                Loc.Pick("仪器 · 回收设备", "Instrument · recovery"), "machine_refining_station",
                Loc.Pick("处理已检测样品：酸浸后过滤，再将滤液还原为目标单质。", "Processes analyzed samples: leach with acid, filter, then reduce the filtrate to recover the target element."),
                LabWorkflow.Describe(RefiningStation.ItemId), null, 300, "refining");
            Add(entries, ids, AcidStation.ItemId, Loc.Pick("制酸装置", "Acid Station"), null, "equipment",
                Loc.Pick("仪器 · 配方制备", "Instrument · recipe preparation"), "machine_acid_station",
                Loc.Pick("按当前配方装入元素原料、工具和空瓶，启动后从产物槽取出试剂。", "Load the recipe's elemental inputs, tools and an empty bottle, start the batch, then collect the reagent from the output slot."),
                LabWorkflow.Describe(AcidStation.ItemId), null, 300, "acid station");

            Add(entries, ids, IndustrialFactoryData.Frame, Loc.Pick("自动化化工厂房", "Automated chemistry workshop"), null, "equipment",
                Loc.Pick("仪器 · 自动生产", "Instrument · automation"), IndustrialFactoryData.FrameSprite,
                Loc.Pick("放进地下实验室储物格并双击；安装工序设备与输送管路后，选择生产路线并装入原料。", "Place in lab storage and double-click. Fit the process equipment and transport manifold, choose a route, and load its feed."),
                LabWorkflow.Describe(IndustrialFactoryData.Frame), null, 350, "workshop");
            Add(entries, ids, IndustrialFactoryData.Transport, Loc.Pick("输送管路模块", "Transport manifold"), null, "components",
                Loc.Pick("厂房部件", "Workshop component"), "machine_industrial_line_kit",
                Loc.Pick("装入自动化厂房的管路槽，连接预处理与回收工位。", "Fit it to the workshop's transport bay to connect the preparation and recovery stations."),
                LabWorkflow.Describe(IndustrialFactoryData.Transport), null, 90, "transport");

            BuildProcessFlows(entries);
            return entries.ToArray();
        }

        private static void BuildProcessFlows(List<EncyclopediaEntry> entries)
        {
            EncyclopediaEntry[] all = entries.ToArray();
            for (int i = 0; i < entries.Count; i++)
            {
                EncyclopediaEntry entry = entries[i];
                entry.ProcessFlow = ProcessFlow(entry);
                entry.FlowStages = EncyclopediaFlow.Build(entry, all);
                if (!string.IsNullOrEmpty(entry.ProcessFlow))
                    entry.SearchText += " " + entry.ProcessFlow;
            }
        }

        private static string ProcessFlow(EncyclopediaEntry entry)
        {
            if (entry.CategoryKey == "equipment")
            {
                if (entry.ItemId == RefiningStation.ItemId) return RefiningStationFlow();
                if (entry.ItemId == AcidStation.ItemId) return AcidStationFlow();
                if (entry.ItemId == IndustrialFactoryData.Frame) return FactoryFlow();
                if (entry.ItemId == IndustrialFactoryData.Transport)
                    return Loc.Pick("1. 将输送管路模块装入自动化化工厂房的管路槽。\n2. 确认它连接预处理与回收工位，再在厂房面板选择生产路线。\n3. 向生产线装入对应原料并启动；从指定输出端收取产品。",
                        "1. Fit the transport manifold in the workshop bay.\n2. Confirm it connects the preparation and recovery stations, then select a route.\n3. Load that route's feed, start production and collect the output.");

                ToolDef tool = ChemistryCatalog.Tools.FirstOrDefault(item => item.ItemId == entry.ItemId);
                LabEquipmentDef equipment = tool == null ? null : LabEquipmentData.Find(tool.Key);
                return equipment == null
                    ? Loc.Pick("1. 在地下实验室储物格中双击设备打开面板。\n2. 按操作说明准备样品、试剂和接收容器，装入电池后启动。\n3. 从产物槽或副产物槽分别取回结果。",
                        "1. Double-click the device in underground lab storage.\n2. Prepare the sample, reagent and receiver listed in its instructions; insert a battery if required.\n3. Collect the results from the output and byproduct slots.")
                    : LabEquipmentData.ProcessFlow(equipment);
            }

            if (entry.CategoryKey == "samples") return SampleFlow(entry);
            if (entry.CategoryKey == "compounds")
            {
                CompoundDef compound = ChemistryCatalog.Compounds.FirstOrDefault(item => item.ItemId == entry.ItemId);
                if (compound != null) return CompoundFlow(compound);
            }
            if (entry.CategoryKey == "elements")
                return Loc.Pick("1. 获取单质，或从已检测样品的回收流程中提取。\n2. 按合成配方放入所需元素和器材；也可按元素状态进行称量、转移或分装。\n3. 处理废料时先用光谱仪查看成分报告，再选择可回收目标。",
                    "1. Obtain the element or recover it from an analyzed sample.\n2. Use it in a synthesis recipe, or weigh and transfer it according to its state.\n3. For scrap recovery, scan the sample and choose a target from its report.");
            if (entry.CategoryKey == "tools")
                return Loc.Pick("1. 将器材放入地下实验室储物格备用。\n2. 在合成台选择配方并查看所需器材；按提示准备器材和原料。\n3. 启动合成后收取产物；配方要求的器材不会被消耗。",
                    "1. Keep the tool in underground lab storage.\n2. Select a recipe on the synthesis page and check its required tools and ingredients.\n3. Start synthesis and collect the product; required tools are not consumed.");
            if (entry.CategoryKey == "materials") return MaterialFlow(entry);
            if (entry.CategoryKey == "containers")
                return ContainerFlow(entry);
            if (!string.IsNullOrEmpty(entry.Operation))
                return Loc.Pick("1. 按上方操作说明准备物品并完成当前步骤。\n2. 收取产物后，依据物品状态继续分离、检测、干燥或回收。\n", 
                    "1. Follow the handling instructions above to prepare the item and complete this step.\n2. Collect the result, then continue with separation, analysis, drying or recovery as indicated.");
            return Loc.Pick("1. 查看物品说明和操作方式，准备对应物料。\n2. 完成设备或配方步骤后收取结果，并按用途保存或交付。",
                "1. Check the description and handling instructions, then prepare the required materials.\n2. Complete the equipment or recipe step, collect the result and store or deliver it.");
        }

        private static string SampleFlow(EncyclopediaEntry entry)
        {
            SampleDef sample = SampleTable.Find(entry.ItemId);
            if (sample == null) return Loc.Pick("1. 双击对应仪器打开面板，把样品放入原料槽并启动检测。\n2. 查看检测结果或成分报告，再按目标选择回收或蒸馏配方。",
                "1. Double-click the matching instrument, load the sample into its ingredient slot and run the analysis.\n2. Review the result or composition report, then choose a recovery or distillation recipe.");

            bool detected = entry.ItemId == sample.DetectedId;
            bool hasAcidTarget = sample.Components.Any(component => LabRefining.IsAcidTarget(sample, component.Symbol));
            string inspect;
            if (detected)
            {
                inspect = Loc.Pick("1. 打开已检测样品的成分报告并查看各成分比例。\n",
                    "1. Open the analyzed sample's composition report and review its component percentages.\n");
            }
            else
            {
                inspect = Loc.Pick("1. 用光谱仪检测样品", "1. Scan the sample with the spectrometer");
                inspect += Loc.Pick("；检测液体样品时在空白对照槽放入水", "; place water in the blank slot when scanning an optical liquid");
                inspect += Loc.Pick("，再打开生成的成分报告并选择目标。\n", ", then open the resulting composition report and choose a target.\n");
            }

            var next = new StringBuilder();
            int step = 2;
            if (hasAcidTarget)
            {
                next.Append(Loc.Get("encyclopedia_sample_refining_flow",
                    "{0}. 报告中的可酸浸目标送入提炼反应台：酸浸 → 装入漏斗过滤 → 还原滤液，收取目标单质。\n",
                    "{0}. Send an acid-leachable target to the refining station: leach → filter with a funnel → reduce the filtrate and collect the element.\n",
                    step++));
            }
            bool hasDirectTarget = detected && sample.Batch != "09"
                && sample.Components.Any(component => !LabRefining.IsAcidTarget(sample, component.Symbol));
            if (hasDirectTarget)
                next.Append(Loc.Get("encyclopedia_sample_direct_recovery_flow",
                    "{0}. 直接回收：按目标含量把足量已检测样品放入蒸馏器，选择回收目标并从产物槽取单质。\n",
                    "{0}. Direct recovery: load the required amount of analyzed sample into the distiller, choose a recovery target and collect the element from its output.\n",
                    step++));
            if (sample.SpriteKey == "sample_liquid" || sample.SpriteKey == "sample_gas")
                next.Append(Loc.Get("encyclopedia_sample_distill_flow",
                    "{0}. 液体或气体样品按报告放入蒸馏器，配空玻璃接收瓶并选择温区；收取馏分后继续处理余液。\n",
                    "{0}. Follow the report in the distiller with an empty glass receiver and selected temperature band; collect the fraction and continue with the residue.\n",
                    step++));
            else if (!hasAcidTarget && !hasDirectTarget)
                next.Append(Loc.Get("encyclopedia_sample_other_flow",
                    "{0}. 报告中没有可回收目标；按成分选择蒸馏、分离或保存。",
                    "{0}. If the report has no recoverable target, choose distillation, separation or storage based on its contents.",
                    step));
            return inspect + next.ToString();
        }

        private static string CompoundFlow(CompoundDef compound)
        {
            if (compound.Key == NitricProductionData.Key)
                return Loc.Pick("1. 在自动化化工厂房选择硝酸生产路线。\n2. 按路线装好反应设备、输送管路、原料和维护耗材。\n3. 启动生产线，按面板指示从输出端收集硝酸。",
                    "1. Select the nitric acid route in the automated workshop.\n2. Fit the required equipment and transport manifold, then load feed and maintenance supplies.\n3. Start production and collect nitric acid from the indicated output.");
            if (AcidStation.HasRecipe(compound))
                return Loc.Pick("1. 在合成台选中配方并点击“制酸”，打开制酸装置。\n2. 按配方装入元素原料、所需器材和空瓶。\n3. 启动批次，从产物槽取出试剂；可分装或供后续设备使用。",
                    "1. Select the recipe on the synthesis page and choose Prepare to open the acid station.\n2. Load the elemental feed, required tools and an empty bottle.\n3. Start the batch and collect the reagent; aliquot it or use it in another device.");
            return Loc.Pick("1. 在合成台选择该化合物，核对上方配方中的元素比例与器材。\n2. 按提示将原料和工具放入地下实验室储物格。\n3. 启动合成并收取产物；液体或气体试剂按需分装、检测或用于反应。",
                "1. Select this compound on the synthesis page and check its elemental ratios and required tools.\n2. Place the ingredients and tools in underground lab storage as prompted.\n3. Start synthesis and collect the product; aliquot, analyze or react liquid and gas reagents as needed.");
        }

        private static string RefiningStationFlow()
        {
            return Loc.Pick("1. 先检测废料，在成分报告中选择可回收目标，并准备报告指定的酸。\n2. 将已检测样品放入投料格、酸放入试剂槽；启动酸浸，取得目标浸出液。\n3. 把浸出液装入提炼反应台并放入漏斗，启动过滤，取得滤液。\n4. 将滤液装入还原槽并启动，收取目标单质。",
                "1. Analyze the scrap, choose a recoverable target in its report and prepare the specified acid.\n2. Load the analyzed sample and acid into their slots; leach to obtain the target leachate.\n3. Load the leachate and filter funnel, then run filtration to obtain filtrate.\n4. Load the filtrate into the reduction cell, run it and collect the elemental product.");
        }

        private static string AcidStationFlow()
        {
            return Loc.Pick("1. 在合成台选择需要制备的酸类配方并打开制酸装置。\n2. 对照配方放入元素原料、所需器材和相容的空瓶。\n3. 启动制备，待批次完成后从产物槽取出试剂。\n4. 将试剂分装保存，或按样品报告送入提炼反应台。",
                "1. Select the acid recipe on the synthesis page and open the acid station.\n2. Load its elemental feed, required tools and a compatible empty bottle.\n3. Start the batch and collect the reagent when it finishes.\n4. Store or aliquot the reagent, or use it in the refining station as directed by a sample report.");
        }

        private static string FactoryFlow()
        {
            return Loc.Pick("1. 把自动化化工厂房放入地下实验室储物格并双击打开面板。\n2. 安装路线需要的工序设备和输送管路模块。\n3. 选择生产路线，按槽位提示装入原料与维护耗材。\n4. 启动生产并从对应输出槽收取产品；缺少设备或物料时先按面板提示补齐。",
                "1. Place the automated workshop in lab storage and double-click to open it.\n2. Fit the process equipment and transport manifold required by the route.\n3. Select a production route and load its feed and maintenance supplies.\n4. Start production and collect the output; install any missing equipment or supplies shown by the panel.");
        }

        private static string MaterialFlow(EncyclopediaEntry entry)
        {
            string id = entry.ItemId;
            if (LabRefining.FindIntermediate(id) is LabRefining.Intermediate intermediate)
                return intermediate.Filtered
                    ? Loc.Pick("1. 将滤液装入提炼反应台的还原槽。\n2. 启动还原并从产物槽收取目标单质。",
                        "1. Load the filtrate into the refining station's reduction cell.\n2. Run reduction and collect the target element from the output.")
                    : Loc.Pick("1. 将浸出液装入提炼反应台投料格并放入漏斗。\n2. 启动过滤，得到对应滤液；随后将滤液送入还原槽。",
                        "1. Load the leachate into the refining station and insert a filter funnel.\n2. Run filtration to obtain the matching filtrate, then load it into the reduction cell.");
            if (id == LabProcessData.OilTrace)
                return Loc.Pick("1. 将示踪液与等量水放入烧杯混合。\n2. 把混合样放入锥形瓶静置分层，或用离心机澄清。\n3. 用分液漏斗收集萃取油相；需要分段时，将两份萃取油相送入色谱仪，收取 A、B 馏分。",
                    "1. Mix equal portions of tracer and water in a beaker.\n2. Settle the mixture in a conical flask or clarify it in a centrifuge.\n3. Collect the extracted oil phase with a separatory funnel; for fractionation, load two portions into the chromatograph and collect A and B.");
            if (id == LabProcessData.PeakA || id == LabProcessData.PeakB)
                return Loc.Pick("1. 该馏分由色谱仪分段产生，余量保留在瓶中。\n2. 用移液管取样，可用光谱仪检测；剩余馏分按样品报告继续处理。",
                    "1. This fraction comes from chromatographic separation; the remaining volume stays in its bottle.\n2. Take an aliquot with a pipette for spectrometer analysis, then follow the sample report for further handling.");
            if (LabProcessData.IsKnown(id))
                return LabProcessData.Guide(id);
            if (LabAnalysisData.IsKnown(id))
                return LabWorkflow.Describe(id);
            return string.IsNullOrEmpty(entry.Operation)
                ? Loc.Pick("按物品说明选择后续设备；若已完成处理，可保存或交付。", "Choose the next device from the item description; store or deliver it when processing is complete.")
                : Loc.Pick("1. 按当前样品状态准备物料。\n2. " + entry.Operation, "1. Prepare the material in its current state.\n2. " + entry.Operation);
        }

        private static string ContainerFlow(EncyclopediaEntry entry)
        {
            if (ReagentBottle.IsDamaged(entry.ItemId))
                return Loc.Pick("1. 将破损容器拖入固体废物桶清理库存。\n2. 转装剩余试剂时先检查相容性，并换用可密封的空瓶。",
                    "1. Drag the damaged container into a solid waste bin to clear it.\n2. Check compatibility and use a sealable empty bottle before transferring any remaining reagent.");
            if (BulkContainers.IsSolidWaste(entry.ItemId) || BulkContainers.IsLiquidWaste(entry.ItemId))
                return Loc.Pick("1. 将对应的固体废料、破损容器或待弃液体拖入废物桶。\n2. 确认库存已清理；可回收样品先检测并处理，不要直接丢弃。",
                    "1. Drag matching solid waste, damaged containers or unwanted liquid into the bin.\n2. Confirm the inventory is cleared; analyze and process recoverable samples before disposal.");
            return Loc.Pick("1. 检查容器材质与剩余容量。\n2. 将兼容的装液瓶拖到空瓶上分装；腐蚀玻璃的液体使用塑料瓶。\n3. 气体用空采样瓶或钢瓶收集；收集完成后按物品说明继续检测或反应。",
                "1. Check the container material and remaining capacity.\n2. Drag a compatible filled bottle onto an empty bottle to transfer; use plastic for liquids that corrode glass.\n3. Collect gas in a vial or cylinder, then follow the item instructions for analysis or reaction.");
        }

        internal static string RecipeLine(CompoundDef def)
        {
            if (def.Key == NitricProductionData.Key) return NitricProductionData.RouteDescription;
            var builder = new StringBuilder();
            builder.Append(Loc.Get("cmp_recipe_label", "配方：", "Recipe: "));
            bool any = false;
            try
            {
                foreach (KeyValuePair<string, int> pair in def.Inputs)
                {
                    any = true;
                    ElementDef element = ChemistryCatalog.FindElement(pair.Key);
                    builder.Append(element?.Symbol ?? pair.Key).Append(" ×").Append(pair.Value).Append("　");
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("解析百科配方失败（" + (def.Key ?? "?") + "）: " + e.Message);
            }
            if (!any) builder.Append(Loc.Get("cmp_recipe_unknown", "化学式无法解析", "unparseable formula"));
            builder.Append("　·　").Append(Loc.Get("cmp_tools_label", "工具：", "Tools: "));
            if (def.Tools == null || def.Tools.Length == 0)
            {
                builder.Append(Loc.Get("cmp_tools_none", "无需工具", "none needed"));
            }
            else
            {
                for (int i = 0; i < def.Tools.Length; i++)
                {
                    ToolDef tool = ChemistryCatalog.FindTool(def.Tools[i]);
                    if (i > 0) builder.Append(Loc.Get("list_separator", "、", ", "));
                    builder.Append(tool == null ? def.Tools[i] : Loc.Pick(tool.NameZh, tool.NameEn));
                }
            }
            return builder.ToString().TrimEnd();
        }

        private static void AddSample(List<EncyclopediaEntry> entries, HashSet<string> ids, SampleDef sample, bool detected)
        {
            string id = detected ? sample.DetectedId : sample.ItemId;
            string description = detected ? sample.Report()
                : Loc.Pick("成分尚未鉴定。先用光谱仪检测，再打开成分报告查看可回收组分。", "Composition is unknown. Analyze with the spectrometer, then open the report to see recoverable components.");
            string name = sample.DisplayName(detected) + Loc.Get("encyclopedia_sample_batch", " · 批次 {0}", " · batch {0}", sample.Batch);
            Add(entries, ids, id, name, null, "samples",
                detected ? Loc.Pick("样品 · 已检测", "Sample · analyzed") : Loc.Pick("样品 · 未鉴定", "Sample · unknown"),
                sample.SpriteKey, description, LabWorkflow.Describe(id), null, 1, sample.Batch);
        }

        private static void AddEmptyBottle(List<EncyclopediaEntry> entries, HashSet<string> ids, string id)
        {
            int capacity = ReagentBottle.CapacityFor(id) * ReagentBottle.MillilitersPerUnit;
            string desc = ReagentBottle.IsGasEmpty(id)
                ? Loc.Get("encyclopedia_gas_vial", "空采样气瓶，可装入通风橱集气槽；容量 {0} mL。", "Empty gas vial for the fume hood receiver; capacity {0} mL.", capacity)
                : ReagentBottle.IsPlasticEmpty(id)
                    ? Loc.Get("encyclopedia_plastic_bottle", "塑料瓶用于盛放会腐蚀玻璃的试剂；容量 {0} mL。", "Plastic bottle for reagents that corrode glass; capacity {0} mL.", capacity)
                    : Loc.Get("encyclopedia_glass_bottle", "仅用于与玻璃相容的液体；拖动试剂瓶到空瓶上可分装，容量 {0} mL。", "For liquids compatible with glass. Drag a filled bottle onto it to transfer; capacity {0} mL.", capacity);
            Add(entries, ids, id, ReagentBottle.EmptyName(id), null, "containers", Loc.Pick("空容器", "Empty container"),
                MaterialItemArt.SpriteKeyFor(id.Substring(ChemistryCatalog.ItemPrefix.Length)), desc, LabWorkflow.Describe(id), null, 0, id);
        }

        private static void Add(List<EncyclopediaEntry> entries, HashSet<string> ids, string itemId, string name,
            string formula, string categoryKey, string categoryLabel, string spriteKey, string description,
            string operation, string recipe, int value, string searchTerms)
        {
            if (string.IsNullOrEmpty(itemId) || !ids.Add(itemId)) return;
            entries.Add(new EncyclopediaEntry
            {
                ItemId = itemId,
                Name = string.IsNullOrEmpty(name) ? itemId : name,
                Formula = formula,
                CategoryKey = categoryKey,
                CategoryLabel = categoryLabel,
                SpriteKey = spriteKey,
                Description = description ?? "",
                Operation = operation ?? "",
                Recipe = recipe ?? "",
                Value = value,
                SearchText = string.Join(" ", itemId, name, formula, categoryLabel, description, operation, recipe, searchTerms)
            });
        }
    }
}
