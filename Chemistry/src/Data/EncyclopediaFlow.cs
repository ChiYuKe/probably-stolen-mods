using System;
using System.Collections.Generic;
using System.Linq;

namespace Chemistry
{
    internal sealed class EncyclopediaFlowNode
    {
        internal readonly string Label;
        internal readonly string SpriteKey;

        internal EncyclopediaFlowNode(string label, string spriteKey)
        {
            Label = label ?? "";
            SpriteKey = spriteKey;
        }
    }

    internal sealed class EncyclopediaFlowStage
    {
        internal readonly string Title;
        internal readonly EncyclopediaFlowNode[] Inputs;
        internal readonly EncyclopediaFlowNode Machine;
        internal readonly EncyclopediaFlowNode[] Outputs;
        internal readonly string Note;

        internal EncyclopediaFlowStage(string title, EncyclopediaFlowNode[] inputs,
            EncyclopediaFlowNode machine, EncyclopediaFlowNode[] outputs, string note = null)
        {
            Title = title ?? "";
            Inputs = inputs ?? Array.Empty<EncyclopediaFlowNode>();
            Machine = machine;
            Outputs = outputs ?? Array.Empty<EncyclopediaFlowNode>();
            Note = note;
        }
    }

    internal static class EncyclopediaFlow
    {
        internal static EncyclopediaFlowStage[] Build(EncyclopediaEntry entry, EncyclopediaEntry[] entries)
        {
            if (entry == null) return Array.Empty<EncyclopediaFlowStage>();
            if (entry.CategoryKey == "equipment") return EquipmentFlow(entry, entries);
            if (entry.CategoryKey == "compounds") return CompoundFlow(entry, entries);
            if (entry.CategoryKey == "samples") return SampleFlow(entry, entries);
            if (entry.CategoryKey == "materials") return MaterialFlow(entry, entries);
            if (entry.CategoryKey == "elements") return ElementFlow(entry, entries);
            if (entry.CategoryKey == "containers") return ContainerFlow(entry, entries);
            if (entry.CategoryKey == "tools") return ToolFlow(entry, entries);
            return GenericFlow(entry, entries);
        }

        private static EncyclopediaFlowStage[] EquipmentFlow(EncyclopediaEntry entry, EncyclopediaEntry[] entries)
        {
            if (entry.ItemId == RefiningStation.ItemId) return RefiningStationFlow(entries);
            if (entry.ItemId == AcidStation.ItemId) return AcidStationFlow(entries);
            if (entry.ItemId == IndustrialFactoryData.Frame) return FactoryFlow(entry, entries);
            if (entry.ItemId == IndustrialFactoryData.Transport)
                return One("安装管路", new[] { Item(entries, entry.ItemId) },
                    Node(Loc.Pick("自动化厂房", "Workshop"), "machine_factory_workshop"),
                    new[] { Node(Loc.Pick("已连接工位", "Connected stations"), "machine_industrial_line_kit") });

            ToolDef tool = ChemistryCatalog.Tools.FirstOrDefault(item => item.ItemId == entry.ItemId);
            LabEquipmentDef equipment = tool == null ? null : LabEquipmentData.Find(tool.Key);
            if (equipment == null) return GenericFlow(entry, entries);
            if (equipment.Key == "chromatograph") return ChromatographFlow(entries);
            if (equipment.Key == "spectrometer") return SpectrometerFlow(entry, entries);
            if (equipment.Key == "distiller") return DistillerFlow(entry, entries);
            return InstrumentFlow(entry, equipment, entries);
        }

        private static EncyclopediaFlowStage[] DistillerFlow(EncyclopediaEntry entry, EncyclopediaEntry[] entries)
        {
            var machine = Item(entries, entry.ItemId);
            return new[]
            {
                Stage(Loc.Pick("液体配方：蒸馏分离", "Liquid recipe: distillation"),
                    new[]
                    {
                        Node(Loc.Pick("可蒸馏样品", "Supported sample"), "sample_liquid"),
                        Item(entries, ReagentBottle.GlassEmpties[0])
                    },
                    machine,
                    new[]
                    {
                        Node(Loc.Pick("冷凝馏分", "Condensate"), "sample_liquid"),
                        Node(Loc.Pick("余液", "Residue"), "sample_liquid")
                    }),
                Stage(Loc.Pick("回收配方：样品制单质", "Recovery recipe: sample to element"),
                    new[] { Node(Loc.Pick("已检测样品 ×配方数量", "Analyzed sample × recipe amount"), "sample_slag") },
                    machine,
                    new[] { Node(Loc.Pick("目标单质 ×1", "Target element ×1"), "el_fe") },
                    Loc.Pick("用“回收目标”切换成分；需要酸浸的目标使用提炼反应台。",
                        "Choose a component with Recovery target; use the refining station for acid-leachable targets."))
            };
        }

        private static EncyclopediaFlowStage[] ChromatographFlow(EncyclopediaEntry[] entries)
        {
            return new[]
            {
                Stage(Loc.Pick("混合示踪样", "Mix tracer sample"),
                    new[]
                    {
                        Item(entries, LabAnalysisData.Dye),
                        Item(entries, LabMixtureData.Water)
                    },
                    Tool("beaker", Loc.Pick("烧杯", "Beaker")),
                    new[] { Item(entries, LabProcessData.ExtractMixed) }),
                Stage(Loc.Pick("静置分层", "Settle into layers"),
                    new[] { Item(entries, LabProcessData.ExtractMixed) },
                    Tool("conical_flask", Loc.Pick("锥形瓶", "Conical flask")),
                    new[] { Item(entries, LabProcessData.ExtractLayers) }),
                Stage(Loc.Pick("收集萃取相", "Collect the extract"),
                    new[]
                    {
                        Item(entries, LabProcessData.ExtractLayers),
                        Item(entries, ReagentBottle.GlassEmpties[0])
                    },
                    Tool("separating_funnel", Loc.Pick("分液漏斗", "Separatory funnel")),
                    new[]
                    {
                        Item(entries, LabProcessData.WaterTrace),
                        Item(entries, LabProcessData.OilTrace)
                    }),
                Stage(Loc.Pick("色谱分段", "Separate fractions"),
                    new[]
                    {
                        Item(entries, LabProcessData.OilTrace, Loc.Pick("萃取油相 ×2", "Extracted oil ×2")),
                        Item(entries, ReagentBottle.GlassEmpties[0])
                    },
                    Item(entries, ChemistryCatalog.ToolItemId("chromatograph")),
                    new[]
                    {
                        Item(entries, LabProcessData.PeakA),
                        Item(entries, LabProcessData.PeakB)
                    },
                    Loc.Pick("A 先出，B 后出；各 25 mL。需电池 6 kJ。", "A comes first, then B; 25 mL each. Requires 6 kJ.")),
            };
        }

        private static EncyclopediaFlowStage[] SpectrometerFlow(EncyclopediaEntry entry, EncyclopediaEntry[] entries)
        {
            return new[]
            {
                Stage(Loc.Pick("扫描未知样品", "Scan unknown sample"),
                    new[] { Node(Loc.Pick("未知样品", "Unknown sample"), "sample_slag") },
                    Item(entries, entry.ItemId),
                    new[] { Node(Loc.Pick("成分报告", "Composition report"), "sample_slag") },
                    Loc.Pick("液体样品需用水作空白；报告用于选择回收目标。", "Use water as the blank for optical liquids; use the report to choose a recovery target."))
            };
        }

        private static EncyclopediaFlowStage[] RefiningStationFlow(EncyclopediaEntry[] entries)
        {
            EncyclopediaFlowNode leachate = Node(Loc.Pick("目标浸出液", "Target leachate"), "tool_beaker");
            EncyclopediaFlowNode filtrate = Node(Loc.Pick("目标滤液", "Target filtrate"), "tool_test_tube");
            EncyclopediaFlowNode element = Node(Loc.Pick("目标单质", "Target element"), "tool_test_tube");
            return new[]
            {
                Stage(Loc.Pick("酸浸", "Leach"),
                    new[]
                    {
                        Node(Loc.Pick("已检测样品", "Analyzed sample"), "sample_slag"),
                        Node(Loc.Pick("配套酸", "Required acid"), "tool_test_tube")
                    },
                    Item(entries, RefiningStation.ItemId),
                    new[] { leachate }),
                Stage(Loc.Pick("过滤", "Filter"),
                    new[]
                    {
                        leachate,
                        Item(entries, ChemistryCatalog.ToolItemId(LabRefining.FilterToolKey))
                    },
                    Item(entries, RefiningStation.ItemId),
                    new[] { filtrate }),
                Stage(Loc.Pick("还原", "Reduce"),
                    new[] { filtrate },
                    Item(entries, RefiningStation.ItemId),
                    new[] { element })
            };
        }

        private static EncyclopediaFlowStage[] AcidStationFlow(EncyclopediaEntry[] entries)
        {
            return One(Loc.Pick("按配方制备", "Prepare from recipe"),
                new[]
                {
                    Node(Loc.Pick("元素原料", "Element feed"), "sample_slag"),
                    Item(entries, ReagentBottle.GlassEmpties[0])
                },
                Item(entries, AcidStation.ItemId),
                new[] { Node(Loc.Pick("成品试剂", "Reagent"), "tool_test_tube") },
                Loc.Pick("先在合成台选择酸类配方，再装料启动。", "Select an acid recipe on the synthesis page, then load and start the batch."));
        }

        private static EncyclopediaFlowStage[] FactoryFlow(EncyclopediaEntry entry, EncyclopediaEntry[] entries)
        {
            return One(Loc.Pick("自动化生产", "Automated production"),
                new[] { Node(Loc.Pick("路线原料", "Route feed"), "tool_test_tube") },
                Item(entries, entry.ItemId),
                new[] { Node(Loc.Pick("路线产物", "Route output"), "tool_test_tube") },
                Loc.Pick("先安装工序设备和管路，再选择路线并装入原料。", "Install process equipment and the manifold, then choose a route and load its feed."));
        }

        private static EncyclopediaFlowStage[] InstrumentFlow(EncyclopediaEntry entry, LabEquipmentDef equipment,
            EncyclopediaEntry[] entries)
        {
            EncyclopediaFlowNode machine = Item(entries, entry.ItemId);
            EncyclopediaFlowNode solid = Node(Loc.Pick("固体样品", "Solid sample"), "sample_slag");
            EncyclopediaFlowNode liquid = Node(Loc.Pick("液体样品", "Liquid sample"), "sample_liquid");
            EncyclopediaFlowNode receiver = Item(entries, ReagentBottle.GlassEmpties[0]);
            EncyclopediaFlowNode emptyGas = Item(entries, ReagentBottle.GasEmpties[0]);
            EncyclopediaFlowNode liquidResult = Node(Loc.Pick("处理液", "Processed liquid"), "sample_liquid");
            EncyclopediaFlowNode solidResult = Node(Loc.Pick("处理固体", "Processed solid"), "sample_slag");

            switch (equipment.Task)
            {
                case EquipmentTask.Weigh:
                    return One(Loc.Pick("称量", "Weigh"),
                        new[] { solid }, machine,
                        new[] { Node(Loc.Pick("质量读数", "Mass reading"), entry.SpriteKey) });
                case EquipmentTask.Volume:
                    return One(Loc.Pick("读取体积", "Read volume"),
                        new[] { liquid }, machine,
                        new[] { Node(Loc.Pick("体积读数", "Volume reading"), entry.SpriteKey) });
                case EquipmentTask.Dispense:
                    return One(Loc.Pick("定量转移", "Transfer a measured amount"),
                        new[] { liquid, receiver }, machine,
                        new[] { Node(Loc.Pick("分装液体", "Aliquoted liquid"), "sample_liquid") });
                case EquipmentTask.Filter:
                case EquipmentTask.Clarify:
                    return One(Loc.Pick("过滤 / 澄清", "Filter / clarify"),
                        new[] { Node(Loc.Pick("待处理样品", "Sample"), "sample_liquid") }, machine,
                        new[] { liquidResult, solidResult });
                case EquipmentTask.Distill:
                    return One(Loc.Pick("蒸馏分离", "Distill and separate"),
                        new[] { liquid, receiver }, machine,
                        new[]
                        {
                            Node(Loc.Pick("冷凝馏分", "Condensate"), "sample_liquid"),
                            Node(Loc.Pick("余液", "Residue"), "sample_liquid")
                        });
                case EquipmentTask.AnalyzeSolid:
                    return One(Loc.Pick("固体观察", "Inspect solid"),
                        new[] { solid }, machine,
                        new[] { Node(Loc.Pick("观察结果", "Observation"), entry.SpriteKey) });
                case EquipmentTask.AnalyzeFluid:
                    return One(Loc.Pick("流体检测", "Analyze fluid"),
                        new[] { liquid, receiver }, machine,
                        new[] { Node(Loc.Pick("检测结果", "Analysis result"), entry.SpriteKey) });
                case EquipmentTask.AnalyzeAny:
                    return One(Loc.Pick("样品扫描", "Scan sample"),
                        new[]
                        {
                            Node(Loc.Pick("待测样品", "Sample"), "sample_slag"),
                            Node(Loc.Pick("水空白", "Water blank"), "cmp_water")
                        },
                        machine,
                        new[] { Node(Loc.Pick("成分报告", "Composition report"), "sample_slag") });
                case EquipmentTask.Acidity:
                case EquipmentTask.Conductivity:
                    return One(Loc.Pick("溶液检测", "Measure solution"),
                        new[] { liquid }, machine,
                        new[] { Node(Loc.Pick("读数", "Reading"), entry.SpriteKey) });
                case EquipmentTask.Settings:
                    return One(Loc.Pick("参数测试", "Test setting"),
                        Array.Empty<EncyclopediaFlowNode>(), machine,
                        new[] { Node(Loc.Pick("面板读数", "Panel reading"), entry.SpriteKey) });
                case EquipmentTask.GlasswareCycle:
                    return One(Loc.Pick("器皿处理", "Process glassware"),
                        new[] { receiver }, machine,
                        new[] { Node(Loc.Pick("处理完成", "Cycle complete"), "tool_test_tube") });
                case EquipmentTask.WaterCycle:
                    return One(Loc.Pick("加热测试", "Heating test"),
                        new[] { Node(Loc.Pick("水样", "Water"), "cmp_water") }, machine,
                        new[] { Node(Loc.Pick("测试读数", "Test reading"), entry.SpriteKey) });
                case EquipmentTask.SolidCycle:
                    return One(Loc.Pick("固体研磨", "Grind solid"),
                        new[] { solid }, machine,
                        new[] { Node(Loc.Pick("研磨完成", "Grinding complete"), "sample_slag") },
                        Loc.Pick("样品留在原料槽，面板显示完成状态。", "The sample stays in the ingredient slot; the panel shows completion."));
                case EquipmentTask.Mix:
                    return One(Loc.Pick("混合样品", "Mix sample"),
                        new[] { liquid, Node(Loc.Pick("加料", "Ingredient"), "cmp_sodium_chloride") },
                        machine, new[] { Node(Loc.Pick("混合样", "Mixed sample"), "sample_liquid") });
                case EquipmentTask.Settle:
                    return One(Loc.Pick("静置 / 析晶", "Settle / crystallize"),
                        new[] { Node(Loc.Pick("混合样", "Mixture"), "sample_liquid") },
                        machine, new[] { Node(Loc.Pick("分层 / 沉降 / 晶体", "Layers / sediment / crystals"), "sample_liquid") },
                        Loc.Pick("结果取决于投入样品；按面板提示分别收取。", "The output depends on the loaded sample; collect it from the indicated slot."));
                case EquipmentTask.SeparateLayers:
                    return One(Loc.Pick("分收上下层", "Collect both layers"),
                        new[] { Node(Loc.Pick("分层样品", "Layered sample"), "sample_liquid"), receiver },
                        machine,
                        new[]
                        {
                            Node(Loc.Pick("油相", "Oil phase"), "lab_sample_oil"),
                            Node(Loc.Pick("水相", "Water phase"), "cmp_water")
                        });
                case EquipmentTask.Dry:
                    return One(Loc.Pick("干燥固体", "Dry solid"),
                        new[] { Node(Loc.Pick("湿滤渣 / 湿盐晶", "Wet cake / crystals"), "sample_slag") },
                        machine,
                        new[] { Node(Loc.Pick("干燥固体", "Dry solid"), "sample_slag") });
                case EquipmentTask.Titrate:
                    return One(Loc.Pick("滴定", "Titrate"),
                        new[]
                        {
                            Node(Loc.Pick("滴定样品", "Sample"), "sample_liquid"),
                            Item(entries, LabAnalysisData.Titrant, Loc.Pick("标准液", "Titrant"))
                        },
                        machine,
                        new[] { Node(Loc.Pick("处理后样品", "Treated sample"), "sample_liquid") });
                case EquipmentTask.GasCollect:
                    return One(Loc.Pick("收集气体", "Collect gas"),
                        new[]
                        {
                            Node(Loc.Pick("含气样品", "Gas-bearing sample"), "sample_liquid"),
                            emptyGas
                        },
                        machine,
                        new[]
                        {
                            Node(Loc.Pick("收集气体", "Collected gas"), "sample_gas"),
                            Node(Loc.Pick("脱气液", "Degassed liquid"), "sample_liquid")
                        });
                case EquipmentTask.FreezeDry:
                    return One(Loc.Pick("冷冻干燥", "Freeze dry"),
                        new[] { solid, receiver }, machine,
                        new[]
                        {
                            solidResult,
                            Node(Loc.Pick("冷阱水", "Cold-trap water"), "cmp_water")
                        });
                case EquipmentTask.Seal:
                    return One(Loc.Pick("封装 / 拆封", "Seal / reopen"),
                        new[]
                        {
                            Node(Loc.Pick("盐或碳酸钙", "Salt or chalk"), "cmp_sodium_chloride"),
                            receiver
                        },
                        machine,
                        new[] { Node(Loc.Pick("密封样品", "Sealed sample"), "tool_test_tube") });
                default:
                    return One(Loc.Pick("仪器处理", "Instrument process"),
                        new[] { Node(Loc.Pick("待处理样品", "Sample"), "sample_slag") },
                        machine,
                        new[] { Node(Loc.Pick("处理结果", "Result"), "sample_liquid") });
            }
        }

        private static EncyclopediaFlowStage[] CompoundFlow(EncyclopediaEntry entry, EncyclopediaEntry[] entries)
        {
            CompoundDef compound = ChemistryCatalog.Compounds.FirstOrDefault(item => item.ItemId == entry.ItemId);
            if (compound == null) return GenericFlow(entry, entries);
            var inputs = new List<EncyclopediaFlowNode>();
            if (compound.Inputs != null)
            {
                foreach (KeyValuePair<string, int> pair in compound.Inputs)
                {
                    ElementDef element = ChemistryCatalog.FindElement(pair.Key);
                    if (element == null) continue;
                    inputs.Add(Item(entries, element.ItemId,
                        Loc.Get("encyclopedia_flow_quantity", "{0} ×{1}", "{0} ×{1}",
                            Loc.Pick(element.NameZh, element.NameEn), pair.Value)));
                }
            }
            if (inputs.Count == 0) inputs.Add(Node(Loc.Pick("配方原料", "Recipe feed"), "tool_test_tube"));

            string stationLabel;
            EncyclopediaFlowNode station;
            if (compound.Key == NitricProductionData.Key)
            {
                stationLabel = Loc.Pick("自动化厂房", "Workshop");
                station = Node(stationLabel, IndustrialFactoryData.FrameSprite);
            }
            else if (AcidStation.HasRecipe(compound))
            {
                stationLabel = Loc.Pick("制酸装置", "Acid station");
                station = Item(entries, AcidStation.ItemId, stationLabel);
            }
            else
            {
                stationLabel = Loc.Pick("合成台", "Synthesis");
                station = Node(stationLabel, "tool_beaker");
            }

            string toolNote = compound.Tools == null || compound.Tools.Length == 0
                ? Loc.Pick("无需额外器材。", "No additional tools.")
                : Loc.Pick("器材：", "Tools: ") + string.Join(Loc.Pick("、", ", "),
                    compound.Tools.Select(key =>
                    {
                        ToolDef tool = ChemistryCatalog.FindTool(key);
                        return tool == null ? key : Loc.Pick(tool.NameZh, tool.NameEn);
                    }));
            return One(Loc.Pick("配方合成", "Synthesize recipe"),
                inputs.ToArray(), station, new[] { Item(entries, entry.ItemId) }, toolNote);
        }

        private static EncyclopediaFlowStage[] SampleFlow(EncyclopediaEntry entry, EncyclopediaEntry[] entries)
        {
            SampleDef sample = SampleTable.Find(entry.ItemId);
            if (sample == null) return GenericFlow(entry, entries);
            bool detected = entry.ItemId == sample.DetectedId;
            string analyzedId = sample.DetectedId;
            var stages = new List<EncyclopediaFlowStage>();
            if (!detected)
            {
                stages.Add(Stage(Loc.Pick("检测成分", "Analyze composition"),
                    new[] { Item(entries, entry.ItemId) },
                    Node(Loc.Pick("光谱仪", "Spectrometer"), MaterialItemArt.ToolSpriteKey("spectrometer")),
                    new[] { Item(entries, analyzedId, Loc.Pick("成分报告", "Composition report"), entry.SpriteKey) },
                    sample.SpriteKey == "sample_liquid"
                        ? Loc.Pick("液体样品检测时需在空白槽放水。", "Place water in the blank slot when scanning a liquid.")
                        : null));
            }

            EncyclopediaFlowNode analyzed = Item(entries, analyzedId,
                Loc.Pick("已检测样品", "Analyzed sample"), entry.SpriteKey);
            (string Symbol, int Percent) acidTarget = sample.Components.FirstOrDefault(component =>
                LabRefining.IsAcidTarget(sample, component.Symbol));
            bool hasDirectTarget = sample.Batch != "09"
                && sample.Components.Any(component => !LabRefining.IsAcidTarget(sample, component.Symbol));
            if (!string.IsNullOrEmpty(acidTarget.Symbol))
            {
                CompoundDef acid = LabRefining.RequiredAcid(sample, acidTarget.Symbol);
                EncyclopediaFlowNode acidNode = acid == null
                    ? Node(Loc.Pick("配套酸", "Required acid"), "cmp_hydrochloric_acid")
                    : Item(entries, acid.ItemId);
                EncyclopediaFlowNode leachate = Node(Loc.Pick("目标浸出液", "Target leachate"), "tool_beaker");
                EncyclopediaFlowNode filtrate = Node(Loc.Pick("目标滤液", "Target filtrate"), "tool_test_tube");
                ElementDef element = ChemistryCatalog.FindElement(acidTarget.Symbol);
                EncyclopediaFlowNode elementalProduct = element == null
                    ? Node(acidTarget.Symbol, "el_fe") : Item(entries, element.ItemId);
                stages.Add(Stage(Loc.Pick("酸浸", "Leach"),
                    new[] { analyzed, acidNode },
                    Node(Loc.Pick("提炼反应台", "Refining station"), "machine_refining_station"),
                    new[] { leachate }));
                stages.Add(Stage(Loc.Pick("过滤", "Filter"),
                    new[]
                    {
                        leachate,
                        Node(Loc.Pick("过滤漏斗", "Filter funnel"), MaterialItemArt.ToolSpriteKey("filter_funnel"))
                    },
                    Node(Loc.Pick("提炼反应台", "Refining station"), "machine_refining_station"),
                    new[] { filtrate }));
                stages.Add(Stage(Loc.Pick("还原", "Reduce"),
                    new[] { filtrate },
                    Node(Loc.Pick("提炼反应台", "Refining station"), "machine_refining_station"),
                    new[] { elementalProduct }));
            }
            if (detected && hasDirectTarget)
            {
                stages.Add(Stage(Loc.Pick("按配方回收成分", "Recover component by recipe"),
                    new[] { Node(Loc.Pick("已检测样品 ×配方数量", "Analyzed sample × recipe amount"), "sample_slag") },
                    Node(Loc.Pick("蒸馏器", "Distiller"), MaterialItemArt.ToolSpriteKey("distiller")),
                    new[] { Node(Loc.Pick("目标单质 ×1", "Target element ×1"), "el_fe") },
                    Loc.Pick("切换“回收目标”选择成分；需要酸浸的目标使用提炼反应台。",
                        "Choose a component with Recovery target; use the refining station for acid-leachable targets.")));
            }
            if (sample.SpriteKey == "sample_liquid" || sample.SpriteKey == "sample_gas")
            {
                stages.Add(Stage(Loc.Pick("蒸馏分离", "Distill and separate"),
                    new[]
                    {
                        analyzed,
                        Item(entries, ReagentBottle.GlassEmpties[0])
                    },
                    Node(Loc.Pick("蒸馏器", "Distiller"), MaterialItemArt.ToolSpriteKey("distiller")),
                    new[]
                    {
                        Node(Loc.Pick("馏分", "Fraction"), "sample_liquid"),
                        Node(Loc.Pick("余液", "Residue"), "sample_liquid")
                    }));
            }
            else if (string.IsNullOrEmpty(acidTarget.Symbol) && !hasDirectTarget)
            {
                stages.Add(Stage(Loc.Pick("选择回收目标", "Choose target"),
                    new[] { analyzed },
                    Node(Loc.Pick("成分报告", "Composition report"), "sample_slag"),
                    new[] { Node(Loc.Pick("可回收组分", "Recoverable component"), "sample_slag") },
                    Loc.Pick("可回收目标以报告中的样品成分为准。", "Use the composition report to identify recoverable components.")));
            }
            return stages.ToArray();
        }

        private static EncyclopediaFlowStage[] MaterialFlow(EncyclopediaEntry entry, EncyclopediaEntry[] entries)
        {
            if (LabRefining.FindIntermediate(entry.ItemId) is LabRefining.Intermediate intermediate)
            {
                if (intermediate.Filtered)
                {
                    ElementDef element = ChemistryCatalog.FindElement(intermediate.Symbol);
                    return One(Loc.Pick("还原滤液", "Reduce filtrate"),
                        new[] { Item(entries, entry.ItemId) },
                        Node(Loc.Pick("提炼反应台", "Refining station"), "machine_refining_station"),
                        new[] { element == null ? Node(intermediate.Symbol, "el_fe") : Item(entries, element.ItemId) });
                }
                return One(Loc.Pick("过滤浸出液", "Filter leachate"),
                    new[]
                    {
                        Item(entries, entry.ItemId),
                        Node(Loc.Pick("过滤漏斗", "Filter funnel"), MaterialItemArt.ToolSpriteKey("filter_funnel"))
                    },
                    Node(Loc.Pick("提炼反应台", "Refining station"), "machine_refining_station"),
                    new[] { Node(Loc.Pick("目标滤液", "Target filtrate"), "tool_test_tube") });
            }

            string id = entry.ItemId;
            if (id == LabProcessData.OilTrace)
                return One(Loc.Pick("色谱分段", "Separate chromatographic fractions"),
                    new[]
                    {
                        Item(entries, id, Loc.Pick("萃取油相 ×2", "Extracted oil ×2")),
                        Item(entries, ReagentBottle.GlassEmpties[0])
                    },
                    Node(Loc.Pick("色谱仪", "Chromatograph"), MaterialItemArt.ToolSpriteKey("chromatograph")),
                    new[]
                    {
                        Item(entries, LabProcessData.PeakA),
                        Item(entries, LabProcessData.PeakB)
                    });
            if (id == LabProcessData.PeakA || id == LabProcessData.PeakB)
                return One(Loc.Pick("取样检测", "Aliquot and analyze"),
                    new[] { Item(entries, id) },
                    Node(Loc.Pick("移液管", "Pipette"), MaterialItemArt.ToolSpriteKey("pipette")),
                    new[] { Node(Loc.Pick("检测样品", "Test aliquot"), entry.SpriteKey) });
            if (id == LabProcessData.ExtractMixed || id == LabProcessData.WashMixed)
                return One(Loc.Pick("静置分层", "Settle into layers"),
                    new[] { Item(entries, id) },
                    Node(Loc.Pick("锥形瓶", "Conical flask"), MaterialItemArt.ToolSpriteKey("conical_flask")),
                    new[] { Item(entries, id == LabProcessData.ExtractMixed ? LabProcessData.ExtractLayers : LabProcessData.WashLayers) });
            if (id == LabProcessData.ExtractLayers || id == LabProcessData.WashLayers)
                return One(Loc.Pick("分收两相", "Collect both phases"),
                    new[]
                    {
                        Item(entries, id),
                        Item(entries, ReagentBottle.GlassEmpties[0])
                    },
                    Node(Loc.Pick("分液漏斗", "Separatory funnel"), MaterialItemArt.ToolSpriteKey("separating_funnel")),
                    new[]
                    {
                        Item(entries, id == LabProcessData.ExtractLayers ? LabProcessData.WaterTrace : LabProcessData.WashWater),
                        Item(entries, id == LabProcessData.ExtractLayers ? LabProcessData.OilTrace : LabProcessData.WashOil)
                    });
            if (id == LabMixtureData.Emulsion)
                return One(Loc.Pick("油水分层", "Separate oil and water"),
                    new[] { Item(entries, id) },
                    Node(Loc.Pick("锥形瓶", "Conical flask"), MaterialItemArt.ToolSpriteKey("conical_flask")),
                    new[] { Item(entries, LabMixtureData.Layers) });
            if (id == LabMixtureData.Layers)
                return One(Loc.Pick("收集上下层", "Collect upper and lower layers"),
                    new[] { Item(entries, id), Item(entries, ReagentBottle.GlassEmpties[0]) },
                    Node(Loc.Pick("分液漏斗", "Separatory funnel"), MaterialItemArt.ToolSpriteKey("separating_funnel")),
                    new[]
                    {
                        Item(entries, LabMixtureData.Water),
                        Item(entries, LabMixtureData.Oil)
                    });
            if (id == LabMixtureData.Suspension)
                return One(Loc.Pick("沉降", "Settle suspension"),
                    new[] { Item(entries, id) },
                    Node(Loc.Pick("锥形瓶", "Conical flask"), MaterialItemArt.ToolSpriteKey("conical_flask")),
                    new[] { Item(entries, LabMixtureData.Settled) });
            if (id == LabMixtureData.Settled || id == LabMixtureData.Crystals)
            {
                bool crystals = id == LabMixtureData.Crystals;
                return One(Loc.Pick("过滤回收", "Filter and recover"),
                    new[] { Item(entries, id) },
                    Node(Loc.Pick("过滤漏斗", "Filter funnel"), MaterialItemArt.ToolSpriteKey("filter_funnel")),
                    new[]
                    {
                        Item(entries, LabMixtureData.Water),
                        Item(entries, crystals ? LabMixtureData.SaltCake : LabMixtureData.Cake)
                    });
            }
            if (id == LabMixtureData.Cake || id == LabMixtureData.SaltCake)
                return One(Loc.Pick("干燥回收", "Dry and recover"),
                    new[] { Item(entries, id) },
                    Node(Loc.Pick("干燥箱", "Drying oven"), MaterialItemArt.ToolSpriteKey("drying_oven")),
                    new[] { Item(entries, id == LabMixtureData.Cake ? LabMixtureData.Chalk : LabMixtureData.Salt) });
            if (id == LabMixtureData.Brine)
                return One(Loc.Pick("冷却析晶", "Cool and crystallize"),
                    new[] { Item(entries, id) },
                    Node(Loc.Pick("锥形瓶", "Conical flask"), MaterialItemArt.ToolSpriteKey("conical_flask")),
                    new[] { Item(entries, LabMixtureData.Crystals) });

            return GenericFlow(entry, entries);
        }

        private static EncyclopediaFlowStage[] ElementFlow(EncyclopediaEntry entry, EncyclopediaEntry[] entries)
        {
            EncyclopediaFlowNode target = Item(entries, entry.ItemId);
            SampleDef sourceSample = SampleTable.All.FirstOrDefault(sample =>
                sample.Components.Any(component => component.Symbol == entry.Formula
                    && LabRefining.IsAcidTarget(sample, component.Symbol)));
            if (sourceSample == null)
            {
                return One(Loc.Pick("作为配方原料", "Use as recipe feed"),
                    new[] { target },
                    Node(Loc.Pick("合成台", "Synthesis"), "tool_beaker"),
                    new[] { Node(Loc.Pick("配方产物", "Recipe product"), "tool_test_tube") },
                    Loc.Pick("具体去向由所选配方决定。", "The selected recipe determines the product."));
            }

            CompoundDef requiredAcid = LabRefining.RequiredAcid(sourceSample, entry.Formula);
            EncyclopediaFlowNode analyzed = Item(entries, sourceSample.DetectedId,
                Loc.Pick("已检测样品", "Analyzed sample"), sourceSample.SpriteKey);
            EncyclopediaFlowNode acid = requiredAcid == null
                ? Node(Loc.Pick("配套酸", "Required acid"), "tool_test_tube")
                : Item(entries, requiredAcid.ItemId);
            EncyclopediaFlowNode leachate = Node(Loc.Pick("目标浸出液", "Target leachate"), "tool_beaker");
            EncyclopediaFlowNode filtrate = Node(Loc.Pick("目标滤液", "Target filtrate"), "tool_test_tube");
            return new[]
            {
                Stage(Loc.Pick("酸浸", "Leach"), new[] { analyzed, acid },
                    Item(entries, RefiningStation.ItemId), new[] { leachate },
                    Loc.Pick("先检测样品，并在成分报告中选择对应元素。", "Analyze the sample and choose this element in its composition report.")),
                Stage(Loc.Pick("过滤", "Filter"), new[]
                    {
                        leachate,
                        Item(entries, ChemistryCatalog.ToolItemId(LabRefining.FilterToolKey))
                    }, Item(entries, RefiningStation.ItemId), new[] { filtrate }),
                Stage(Loc.Pick("还原", "Reduce"), new[] { filtrate },
                    Item(entries, RefiningStation.ItemId), new[] { target })
            };
        }

        private static EncyclopediaFlowStage[] ContainerFlow(EncyclopediaEntry entry, EncyclopediaEntry[] entries)
        {
            if (BulkContainers.IsSolidWaste(entry.ItemId) || BulkContainers.IsLiquidWaste(entry.ItemId))
                return One(Loc.Pick("废物处理", "Dispose of waste"),
                    new[] { Node(Loc.Pick("对应废料", "Matching waste"), "sample_slag") },
                    Item(entries, entry.ItemId),
                    new[] { Node(Loc.Pick("库存清理", "Inventory cleared"), "tool_test_tube") });
            if (ReagentBottle.IsDamaged(entry.ItemId))
                return One(Loc.Pick("处理破损容器", "Dispose of damaged container"),
                    new[] { Item(entries, entry.ItemId) },
                    Node(Loc.Pick("固体废物桶", "Solid waste bin"), BulkContainers.Sprite(BulkContainers.SolidWasteId)),
                    new[] { Node(Loc.Pick("已清理", "Cleared"), "tool_test_tube") });
            return One(Loc.Pick("分装液体", "Transfer liquid"),
                new[]
                {
                    Node(Loc.Pick("装液瓶", "Filled bottle"), "cmp_water"),
                    Item(entries, entry.ItemId)
                },
                Node(Loc.Pick("移液管", "Pipette"), MaterialItemArt.ToolSpriteKey("pipette")),
                new[] { Node(Loc.Pick("接收瓶", "Receiver bottle"), entry.SpriteKey) },
                Loc.Pick("腐蚀玻璃的试剂需换用塑料瓶。", "Use plastic for reagents that corrode glass."));
        }

        private static EncyclopediaFlowStage[] ToolFlow(EncyclopediaEntry entry, EncyclopediaEntry[] entries)
        {
            return One(Loc.Pick("作为配方器材", "Use as recipe tool"),
                new[] { Item(entries, entry.ItemId) },
                Node(Loc.Pick("合成台", "Synthesis"), "tool_beaker"),
                new[] { Node(Loc.Pick("配方产物", "Recipe product"), "tool_test_tube") },
                Loc.Pick("具体产物由选中的配方决定；器材本身不会被消耗。", "The selected recipe determines the product; the tool is not consumed."));
        }

        private static EncyclopediaFlowStage[] GenericFlow(EncyclopediaEntry entry, EncyclopediaEntry[] entries)
        {
            string operation = entry.Operation ?? "";
            string key = operation.IndexOf("蒸馏器", StringComparison.OrdinalIgnoreCase) >= 0
                || operation.IndexOf("distiller", StringComparison.OrdinalIgnoreCase) >= 0 ? "distiller"
                : operation.IndexOf("色谱仪", StringComparison.OrdinalIgnoreCase) >= 0
                    || operation.IndexOf("chromatograph", StringComparison.OrdinalIgnoreCase) >= 0 ? "chromatograph"
                : operation.IndexOf("光谱仪", StringComparison.OrdinalIgnoreCase) >= 0
                    || operation.IndexOf("spectrometer", StringComparison.OrdinalIgnoreCase) >= 0 ? "spectrometer"
                : operation.IndexOf("分液漏斗", StringComparison.OrdinalIgnoreCase) >= 0
                    || operation.IndexOf("separatory funnel", StringComparison.OrdinalIgnoreCase) >= 0 ? "separating_funnel"
                : operation.IndexOf("过滤", StringComparison.OrdinalIgnoreCase) >= 0
                    || operation.IndexOf("filter", StringComparison.OrdinalIgnoreCase) >= 0 ? "filter_funnel"
                : operation.IndexOf("滴定", StringComparison.OrdinalIgnoreCase) >= 0
                    || operation.IndexOf("titration", StringComparison.OrdinalIgnoreCase) >= 0 ? "titration_set"
                : "beaker";
            return One(Loc.Pick("后续处理", "Next process"),
                new[] { Item(entries, entry.ItemId) },
                Node(Loc.Pick("对应设备", "Matching device"), MaterialItemArt.ToolSpriteKey(key)),
                new[] { Node(Loc.Pick("处理结果", "Result"), entry.SpriteKey) });
        }

        private static EncyclopediaFlowStage[] One(string title, EncyclopediaFlowNode[] inputs,
            EncyclopediaFlowNode machine, EncyclopediaFlowNode[] outputs, string note = null)
        {
            return new[] { Stage(title, inputs, machine, outputs, note) };
        }

        private static EncyclopediaFlowStage Stage(string title, EncyclopediaFlowNode[] inputs,
            EncyclopediaFlowNode machine, EncyclopediaFlowNode[] outputs, string note = null)
        {
            return new EncyclopediaFlowStage(title, inputs, machine, outputs, note);
        }

        private static EncyclopediaFlowNode Item(EncyclopediaEntry[] entries, string id, string label = null,
            string spriteKey = null)
        {
            EncyclopediaEntry item = entries == null ? null : Array.Find(entries, entry => entry.ItemId == id);
            return new EncyclopediaFlowNode(label ?? item?.Name ?? id, spriteKey ?? item?.SpriteKey ?? "tool_test_tube");
        }

        private static EncyclopediaFlowNode Node(string label, string spriteKey)
        {
            return new EncyclopediaFlowNode(label, spriteKey);
        }

        private static EncyclopediaFlowNode Tool(string key, string label)
        {
            return Node(label, MaterialItemArt.ToolSpriteKey(key));
        }
    }
}
