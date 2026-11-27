using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.UI;

namespace Chemistry
{
    /// <summary>
    /// 把所有元素 / 工具 / 化合物注册进游戏的物品目录。
    /// 游戏会为每个 id 保存一个工厂，实际生成物品时再调用，所以这里只登记怎么造。
    /// </summary>
    internal static class ItemRegistration
    {
        private const int StandardItemSize = 2;
        private const int StationWidth = 4;
        // 64×48 贴图，每行对应 16 像素。右上角完全透明，可以留给其他物品。
        private const string StationCells = "1110" + "1111" + "1111";
        private static int registered;

        public static bool Ready
        {
            get { return registered > 0; }
        }

        public static int RegisteredCount
        {
            get { return registered; }
        }

        public static void Register(ModItemDirectory dir)
        {
            if (dir == null)
            {
                return;
            }

            MaterialItemArt.PrepareFootprintSprites();

            int count = 0;
            List<string> failed = null;
            foreach (string[] family in ReagentBottle.EmptyFamilies)
            {
                foreach (string id in family)
                {
                    if (dir.Add(id, () => CreateEmptyReagentBottle(id)))
                    {
                        count++;
                    }
                    else
                    {
                        (failed ?? (failed = new List<string>())).Add(id);
                    }
                }
            }
            foreach (string damagedId in ReagentBottle.DamagedIds)
            {
                if (dir.Add(damagedId, () => CreateDamagedBottle(damagedId)))
                {
                    count++;
                }
                else
                {
                    (failed ?? (failed = new List<string>())).Add(damagedId);
                }
            }
            foreach (string bulkId in BulkContainers.ItemIds)
            {
                if (dir.Add(bulkId, () => CreateBulkContainer(bulkId)))
                {
                    count++;
                }
                else
                {
                    (failed ?? (failed = new List<string>())).Add(bulkId);
                }
            }
            if (dir.Add(RefiningStation.ItemId, CreateRefiningStation))
            {
                count++;
            }
            else
            {
                (failed ?? (failed = new List<string>())).Add(RefiningStation.ItemId);
            }
            if (dir.Add(AcidStation.ItemId, CreateAcidStation))
            {
                count++;
            }
            else
            {
                (failed ?? (failed = new List<string>())).Add(AcidStation.ItemId);
            }

            foreach (ElementDef def in ChemistryCatalog.Elements)
            {
                ElementDef captured = def;
                if (dir.Add(captured.ItemId, () => CreateElement(captured)))
                {
                    count++;
                }
                else
                {
                    (failed ?? (failed = new List<string>())).Add(captured.ItemId);
                }
            }

            foreach (ToolDef def in ChemistryCatalog.Tools)
            {
                ToolDef captured = def;
                if (dir.Add(captured.ItemId, () => CreateTool(captured)))
                {
                    count++;
                }
                else
                {
                    (failed ?? (failed = new List<string>())).Add(captured.ItemId);
                }
            }

            foreach (CompoundDef def in ChemistryCatalog.Compounds)
            {
                CompoundDef captured = def;
                if (dir.Add(captured.ItemId, () => CreateCompound(captured)))
                {
                    count++;
                }
                else
                {
                    (failed ?? (failed = new List<string>())).Add(captured.ItemId);
                }
            }

            foreach (SampleDef sample in SampleTable.All)
            {
                if (sample.ItemId != SampleTable.VanillaScrapId)
                {
                    if (dir.Add(sample.ItemId, () => CreateSample(sample, false)))
                    {
                        count++;
                    }
                    else
                    {
                        (failed ?? (failed = new List<string>())).Add(sample.ItemId);
                    }
                }

                if (dir.Add(sample.DetectedId, () => CreateSample(sample, true)))
                {
                    count++;
                }
                else
                {
                    (failed ?? (failed = new List<string>())).Add(sample.DetectedId);
                }
            }

            foreach (LabRefining.Intermediate product in LabRefining.Intermediates)
            {
                if (dir.Add(product.ItemId, () => CreateIntermediate(product)))
                {
                    count++;
                }
                else
                {
                    (failed ?? (failed = new List<string>())).Add(product.ItemId);
                }
            }

            foreach (NitricProductionData.Item part in NitricProductionData.Items)
            {
                if (dir.Add(part.Id, () => CreateProductionItem(part)))
                {
                    count++;
                }
                else
                {
                    (failed ?? (failed = new List<string>())).Add(part.Id);
                }
            }

            foreach (string id in LabMixtureData.Items)
            {
                if (dir.Add(id, () => CreateMixtureItem(id)))
                {
                    count++;
                }
                else
                {
                    (failed ?? (failed = new List<string>())).Add(id);
                }
            }

            foreach (string id in LabAnalysisData.Items)
            {
                if (dir.Add(id, () => CreateAnalysisItem(id))) count++;
                else (failed ?? (failed = new List<string>())).Add(id);
            }
            foreach (string id in LabProcessData.Items)
            {
                if (dir.Add(id, () => CreateProcessItem(id))) count++;
                else (failed ?? (failed = new List<string>())).Add(id);
            }
            foreach (string id in new[] { IndustrialFactoryData.Frame, IndustrialFactoryData.Transport })
            {
                if (dir.Add(id, () => CreateFactoryItem(id))) count++;
                else (failed ?? (failed = new List<string>())).Add(id);
            }

            if (failed != null && failed.Count > 0)
            {
                ChemistryMod.Log?.Warning("有 " + failed.Count + " 件物品注册失败（id 冲突？）："
                    + string.Join("、", failed.ToArray()));
            }

            registered = count;
            ChemistryMod.Log?.Log("物品目录初始化：注册 " + count + " 件物品（元素 " + ChemistryCatalog.Elements.Length
                + "，工具 " + ChemistryCatalog.Tools.Length + "，化合物 " + ChemistryCatalog.Compounds.Length
                + "，含未知及已检测样品）");
        }

        private static GameItem CreateFactoryItem(string id)
        {
            GameItem item = ItemDirectory.CreateEmptyItem(id);
            bool workshop = id == IndustrialFactoryData.Frame;
            item.name = workshop ? Loc.Pick("自动化化工厂房", "Automated chemistry workshop")
                : Loc.Pick("输送管路模块", "Transport manifold");
            item.shortDescription = workshop
                ? Loc.Pick("放入地下实验室储物格并双击。安装工序设备与输送管路后可联机生产。",
                    "Place in lab storage and double-click. Fit machines and a transport manifold to run the line.")
                : Loc.Pick("装入自动化厂房的管路槽，连接预处理和回收工位。",
                    "Fit to the workshop transport bay to connect its two stages.");
            item.unitBaseValue = item.unitValue = workshop ? 350 : 90;
            item.unitCount = 1;
            item.mayStackItemFunc = (_, _) => false;
            ApplyCommon(item, workshop ? IndustrialFactoryData.FrameSprite : "machine_industrial_line_kit", "TOOL");
            if (workshop) IndustrialFactory.Attach(item);
            return item;
        }

        private static GameItem CreateProcessItem(string id)
        {
            GameItem item = ItemDirectory.CreateEmptyItem(id);
            item.name = LabProcessData.Name(id);
            item.shortDescription = LabProcessData.Guide(id);
            var sample = LabProcessData.Read(id);
            if (sample.Units > 0) item.shortDescription = sample.Contents + "\n" + item.shortDescription;
            item.unitBaseValue = item.unitValue = 5;
            item.unitCount = 1;
            item.maxSlotAmountItemFunc = (_, _, _) => ReagentBottle.IsLiquid(id) || ReagentBottle.IsGas(id)
                ? LabStorage.MaxCountPerSlot : ItemPricing.StackLimitFor(item);
            string sprite = ReagentBottle.IsLiquid(id) ? MaterialItemArt.Apply(item, id.Substring(ChemistryCatalog.ItemPrefix.Length))
                : MaterialItemArt.AttachMixture(item);
            ApplyCommon(item, sprite, "CHEMICAL_SUPPLIES");
            return item;
        }

        private static GameItem CreateAnalysisItem(string id)
        {
            GameItem item = ItemDirectory.CreateEmptyItem(id);
            item.name = LabAnalysisData.Name(id);
            item.shortDescription = LabAnalysisData.Description(id);
            item.unitBaseValue = item.unitValue = id == LabAnalysisData.Spent ? 1 : 5;
            item.unitCount = id == LabAnalysisData.Dye || id == LabAnalysisData.Acid || id == LabAnalysisData.Titrant
                ? LabMixtureData.Portion : 1;
            item.maxSlotAmountItemFunc = (_, _, _) => ReagentBottle.IsLiquid(id) || ReagentBottle.IsGas(id)
                ? LabStorage.MaxCountPerSlot : ItemPricing.StackLimitFor(item);
            string sprite = id == LabAnalysisData.Carbon ? "el_c"
                : id == LabAnalysisData.Spent ? "sample_slag"
                : MaterialItemArt.Apply(item, id.Substring(ChemistryCatalog.ItemPrefix.Length));
            ApplyCommon(item, sprite, "CHEMICAL_SUPPLIES");
            return item;
        }

        private static GameItem CreateMixtureItem(string id)
        {
            GameItem item = ItemDirectory.CreateEmptyItem(id);
            item.name = LabMixtureData.Name(id);
            item.shortDescription = LabMixtureData.Description(id);
            item.unitBaseValue = item.unitValue = id == LabMixtureData.Oil ? 4 : 12;
            item.unitCount = id == LabMixtureData.Oil ? LabMixtureData.Portion : 1;
            item.maxSlotAmountItemFunc = (_, _, _) => ReagentBottle.IsLiquid(id) || ReagentBottle.IsGas(id)
                ? LabStorage.MaxCountPerSlot : ItemPricing.StackLimitFor(item);
            if (LabMixtureData.IsDirectBatch(id))
            {
                item.mayStackItemFunc = (_, _) => false;
                item.maxSlotAmountItemFunc = (_, _, _) => 1;
            }
            string sprite = id switch
            {
                LabMixtureData.Oil or LabMixtureData.Unknown or LabMixtureData.UnknownPlastic
                    or LabDistillationData.Early or LabDistillationData.Late or LabDistillationData.Mixed =>
                    MaterialItemArt.Apply(item, id.Substring(ChemistryCatalog.ItemPrefix.Length)),
                LabMixtureData.Cake => "cmp_calcium_carbonate",
                LabMixtureData.SaltCake => "cmp_sodium_chloride",
                _ => MaterialItemArt.AttachMixture(item)
            };
            ApplyCommon(item, sprite, "CHEMICAL_SUPPLIES");
            return item;
        }

        private static GameItem CreateProductionItem(NitricProductionData.Item part)
        {
            GameItem item = ItemDirectory.CreateEmptyItem(part.Id);
            item.identifierName = part.Id;
            item.name = part.Name;
            item.unitCount = 1;
            item.unitBaseValue = item.unitValue = part.Value;
            item.shortDescription = Loc.Pick("地下实验室硝酸生产线用物品。", "Used by the nitric acid line in the underground lab.");
            item.longDescription = item.shortDescription;
            ApplyCommon(item, part.Sprite, "CHEMICAL_SUPPLIES");
            if (part.Id == NitricProductionData.LabModule || part.Id == NitricProductionData.PipeModule)
            {
                var launcher = new PixelWindow(true, item.name, WindowStyle.Machinery);
                launcher.openCallback = () =>
                {
                    launcher.Hide();
                    if (!NitricProduction.Open(control: true))
                    {
                        OverlayHandler.current?.AppendLog(AcidStation.RequiredMachine, false);
                    }
                };
                item.SetContentWindow(launcher);
                launcher.Hide();
            }
            else
            {
                EnableSupplyStacking(item);
            }
            return item;
        }

        private static GameItem CreateEmptyReagentBottle(string id)
        {
            bool plastic = ReagentBottle.IsPlasticEmpty(id);
            GameItem item = ItemDirectory.CreateEmptyItem(id);
            item.name = ReagentBottle.EmptyName(id);
            item.shortDescription = ReagentBottle.IsGasEmpty(id)
                ? Loc.Pick("未装气体的采样瓶。", "A sampling vial with no gas contents.")
                : plastic ? Loc.Pick("游戏内专装标记为腐蚀玻璃的试剂。", "In-game container for reagents marked as glass-corrosive.")
                : Loc.Pick("只能装与玻璃相容的试剂。", "For reagents compatible with glass.");
            item.unitCount = 1;
            item.mayStackItemFunc = (_, _) => false;
            ApplyCommon(item, MaterialItemArt.Apply(item, id.Substring(ChemistryCatalog.ItemPrefix.Length)), "CHEMICAL_SUPPLIES");
            return item;
        }

        private static GameItem CreateBulkContainer(string id)
        {
            GameItem item = ItemDirectory.CreateEmptyItem(id);
            item.name = BulkContainers.Name(id);
            item.shortDescription = BulkContainers.Description(id);
            item.longDescription = item.shortDescription;
            item.unitCount = 1;
            item.unitBaseValue = item.unitValue = ReagentBottle.IsEmptyTarget(id) ? 18 : 0;
            item.mayStackItemFunc = (_, _) => false;
            if (!ReagentBottle.IsEmptyTarget(id))
            {
                item.maxSlotAmountItemFunc = (self, _, _) => ReagentBottle.CapacityFor(self);
            }
            if (BulkContainers.IsSolidWaste(id))
            {
                ReagentBottle.AttachSolidWasteTarget(item);
            }
            ApplyCommon(item, BulkContainers.Sprite(id), "CHEMICAL_SUPPLIES");
            return item;
        }

        private static GameItem CreateDamagedBottle(string id)
        {
            GameItem item = ItemDirectory.CreateEmptyItem(id);
            item.name = ReagentBottle.DamagedNameFor(id);
            item.shortDescription = id switch
            {
                ReagentBottle.DamagedGasId => Loc.Pick("瓶壁开裂，已不能密封。", "The cracked vial can no longer seal."),
                ReagentBottle.DamagedPlasticId => Loc.Pick("瓶身凹陷变形，无法再次灌装。", "The crushed bottle cannot be refilled."),
                _ => Loc.Pick("瓶壁已被腐蚀穿孔，无法再次灌装。", "The glass wall is corroded through. This bottle cannot be refilled."),
            };
            item.longDescription = item.shortDescription;
            item.unitCount = 1;
            item.mayStackItemFunc = (_, _) => false;
            ApplyCommon(item, ReagentBottle.DamagedSpriteFor(id), "CHEMICAL_SUPPLIES");
            return item;
        }

        private static GameItem CreateRefiningStation()
        {
            GameItem item = ItemDirectory.CreateEmptyItem(RefiningStation.ItemId);
            item.identifierName = "refining_station";
            item.name = Loc.Pick("提炼反应台", "Refining Station");
            item.shortDescription = Loc.Pick("双击打开设备。可在地下实验室进行酸浸、过滤和还原。",
                "Double-click to open. Leaches, filters and reduces materials in the underground lab.");
            item.longDescription = item.shortDescription;
            item.unitCount = 1;
            item.unitBaseValue = item.unitValue = 300;
            item.EnableTag("CONTAINER_TAG");
            ApplyCommon(item, "machine_refining_station", "TOOL");
            RefiningStation.Attach(item);
            return item;
        }

        private static GameItem CreateAcidStation()
        {
            GameItem item = ItemDirectory.CreateEmptyItem(AcidStation.ItemId);
            item.identifierName = "acid_station";
            item.name = Loc.Pick("制酸装置", "Acid Station");
            item.shortDescription = Loc.Pick("在地下实验室双击打开。也可把兼容工具或空试剂瓶拖到设备上装槽。",
                "Double-click in the underground lab, or drop a compatible tool or empty bottle onto the station.");
            item.longDescription = item.shortDescription;
            item.unitCount = 1;
            item.unitBaseValue = item.unitValue = 300;
            item.EnableTag("CONTAINER_TAG");
            ApplyCommon(item, "machine_acid_station", "TOOL");
            AcidStation.Attach(item);
            return item;
        }

        private static GameItem CreateSample(SampleDef sample, bool detected)
        {
            GameItem item = ItemDirectory.CreateEmptyItem(detected ? sample.DetectedId : sample.ItemId);
            item.name = sample.DisplayName(detected);
            item.identifierName = "Sample";
            item.unitBaseValue = 1;
            item.unitValue = 1;
            item.unitCount = 1;
            item.longDescription = detected ? sample.Report() : Loc.Get("sample_item_unknown",
                "成分未知。带去地下实验室检测。", "Composition unknown. Analyze it in the underground lab.");
            EnableSupplyStacking(item);
            ApplyCommon(item, sample.SpriteKey, "CHEMICAL_SUPPLIES");
            return item;
        }

        private static GameItem CreateIntermediate(LabRefining.Intermediate product)
        {
            GameItem item = ItemDirectory.CreateEmptyItem(product.ItemId);
            item.identifierName = product.ItemId;
            item.name = product.Name;
            item.unitCount = 1;
            item.unitBaseValue = 1;
            item.unitValue = 1;
            item.shortDescription = product.Instructions;
            item.longDescription = product.Instructions;
            EnableSupplyStacking(item);
            ApplyCommon(item, product.SpriteKey, "CHEMICAL_SUPPLIES");
            return item;
        }

        // ---------------------------------------------------------------
        // 元素单质
        // ---------------------------------------------------------------

        private static GameItem CreateElement(ElementDef def)
        {
            GameItem item = ItemDirectory.CreateEmptyItem(def.ItemId);

            string symbol = def.Symbol;
            item.name = Loc.Pick(def.NameZh, def.NameEn) + " (" + symbol + ")";
            item.identifierName = symbol;
            item.unitBaseValue = def.Value;
            item.unitValue = ItemPricing.ElementUnitValue(def);
            item.unitCount = 1;
            int maxStack = ItemMass.MaxStack(ItemMass.ElementUnitGrams, ItemPricing.ElementUnitValue(def));
            item.maxSlotAmountItemFunc = (_, _, _) => maxStack;

            string category = ChemistryCatalog.ElementCategoryLabel(def.Category, Loc.IsChinese);
            item.longDescription = BuildElementDescription(def, category);
            item.flavorText = BuildElementFlavor(def);

            ApplyCommon(item, MaterialItemArt.Apply(item, def.SpriteKey), "CHEMICAL_SUPPLIES");

            return item;
        }

        /// <summary>
        /// 元素物品的描述：身份 / 周期表位置 / 一两句简介。
        ///
        /// 供完整物品描述使用。悬停卡片由 LabWorkflow 按行绘制。
        /// </summary>
        private static string BuildElementDescription(ElementDef def, string category)
        {
            var builder = new StringBuilder();
            bool zh = Loc.IsChinese;

            // 1) 第 N 号元素 · 名称（符号）
            builder.Append(Loc.Get(
                "el_desc_identity",
                "第 {0} 号元素 · {1}（{2}）",
                "Element {0} · {1} ({2})",
                def.Number, Loc.Pick(def.NameZh, def.NameEn), def.Symbol));

            // 2) 把分类和周期表坐标放在一行，分区与常温状态另起一行，避免提示框挤成一串。
            var position = new StringBuilder(category);
            if (def.Period > 0)
            {
                position.Append(Loc.Get("el_desc_period", " · 第 {0} 周期", " · period {0}", def.Period));
            }

            if (def.Group > 0)
            {
                position.Append(Loc.Get("el_desc_group", " · 第 {0} 族", " · group {0}", def.Group));
            }
            var properties = new StringBuilder();
            if (!string.IsNullOrEmpty(def.Block))
            {
                properties.Append(Loc.Get("el_desc_block", "{0} 区", "{0}-block", def.Block));
            }

            string phase = PhaseLabel(def.Phase);
            if (!string.IsNullOrEmpty(phase))
            {
                if (properties.Length > 0)
                {
                    properties.Append(" · ");
                }
                properties.Append(Loc.Get("el_desc_phase", "常温：{0}", "{0} at room temperature", phase));
            }

            builder.Append('\n').Append(position);
            if (properties.Length > 0)
            {
                builder.Append('\n').Append(properties);
            }

            if (!double.IsNaN(def.TemperatureC) && !double.IsInfinity(def.TemperatureC))
            {
                builder.Append('\n').Append(Loc.Get(
                    "el_desc_temperature",
                    "温度：{0:0.#} °C / {1:0.##} K",
                    "Temperature: {0:0.#} °C / {1:0.##} K",
                    def.TemperatureC, def.TemperatureC + 273.15));
            }

            if (HasTemperature(def.MeltC))
            {
                builder.Append('\n').Append(Loc.Get(
                    "el_desc_melting_point",
                    "熔点 / 凝固点：{0:0.#} °C / {1:0.##} K",
                    "Melting / freezing point: {0:0.#} °C / {1:0.##} K",
                    def.MeltC, def.MeltC + 273.15));
            }

            if (HasTemperature(def.BoilC))
            {
                builder.Append('\n').Append(Loc.Get(
                    "el_desc_boiling_point",
                    "沸点 / 液化点（常压）：{0:0.#} °C / {1:0.##} K",
                    "Boiling / liquefaction point (1 atm): {0:0.#} °C / {1:0.##} K",
                    def.BoilC, def.BoilC + 273.15));
            }

            // 3) 简介（一两句用途/特点，不含数值）
            string summary = Loc.Pick(def.SummaryZh, def.SummaryEn);
            if (!string.IsNullOrEmpty(summary))
            {
                builder.Append("\n\n").Append(summary);
            }

            return builder.ToString();
        }

        internal static bool HasTemperature(double temperature) =>
            !double.IsNaN(temperature) && !double.IsInfinity(temperature);

        internal static string PhaseLabel(string phase)
        {
            switch (phase)
            {
                case "Gas":
                    return Loc.Get("phase_gas", "气体", "Gas");
                case "Liquid":
                    return Loc.Get("phase_liquid", "液体", "Liquid");
                case "Solid":
                    return Loc.Get("phase_solid", "固体", "Solid");
                default:
                    return "";
            }
        }


        private static string BuildElementFlavor(ElementDef def)
        {
            if (def.Category == "noble_gas")
            {
                return Loc.Get("flavor_noble_gas", "装在瓶子里也一副懒得理你的样子。", "Even bottled, it looks like it can't be bothered.");
            }

            if (def.Category == "actinide")
            {
                return Loc.Get("flavor_actinide", "标签边上有一行小字：别把瓶盖拧开。", "Small print on the label: do not unscrew the cap.");
            }

            if (def.Number == 79)
            {
                return Loc.Get("flavor_gold", "物理意义上的硬通货。", "Hard currency, in the physical sense.");
            }

            return Loc.Get("flavor_element", "瓶子冰凉，晃起来没什么声音。", "The vial is cold and rattles quietly.");
        }

        // ---------------------------------------------------------------
        // 化学工具
        // ---------------------------------------------------------------

        private static GameItem CreateTool(ToolDef def)
        {
            GameItem item = ItemDirectory.CreateEmptyItem(def.ItemId);

            item.name = Loc.Pick(def.NameZh, def.NameEn);
            item.identifierName = def.Key;
            item.unitBaseValue = def.Value;
            item.unitValue = ItemPricing.ToolUnitValue(def);
            item.unitCount = 1;
            // 允许仪器物品通过自己的面板在地下实验室操作。
            item.canUseOutsideCombat = true;
            // 工具也按质量限叠：烧杯 3 件/格，天平、光谱仪这类重设备每格 1 件。
            int maxStack = ItemMass.MaxStack(ItemMass.UnitMass(def.ItemId), ItemPricing.ToolUnitValue(def));
            item.maxSlotAmountItemFunc = (_, _, _) => maxStack;

            item.shortDescription = Loc.Get(
                "item_tool_short",
                "{0}，{1}。",
                "{0}, {1}.",
                Loc.Pick(def.NameZh, def.NameEn),
                ChemistryCatalog.ToolCategoryLabel(def.Category, Loc.IsChinese));
            item.longDescription = Loc.Pick(def.DescZh, def.DescEn);
            if (def.Key == "beaker")
            {
                item.shortDescription += Loc.Pick("可代替锥形瓶：拖到制酸装置，或放进它的器皿槽。",
                    "Can replace the conical flask: drop onto the acid station or into its glassware slot.");
            }
            item.flavorText = Loc.Get(
                "flavor_tool",
                "边角有磨损，看得出前主人做过不少实验。",
                "The edges are worn; the previous owner clearly did a lot of experiments.");

            string spriteKey = def.Key switch
            {
                "thermometer" => MaterialItemArt.ThermometerSpriteKey,
                "volumetric_flask" => MaterialItemArt.VolumetricFlaskSpriteKey,
                "test_tube" => MaterialItemArt.TestTubeSpriteKey,
                _ => MaterialItemArt.ToolSpriteKey(def.Key),
            };
            ApplyCommon(item, spriteKey, "TOOL");
            LabEquipmentDef equipment = LabEquipmentData.Find(def.Key);
            if (equipment != null)
            {
                item.shortDescription += "\n"
                    + Loc.Pick("双击打开仪器面板，按槽位放入原料与接收容器，再启动配方。",
                        "Double-click to open the instrument panel, load ingredients and receivers into their slots, then run the recipe.");
                if (equipment.PowerKj > 0)
                    item.shortDescription += "\n" + Loc.Pick("耗电设备，需装入电池。", "Battery-powered. Insert a battery to run.");
                LabEquipmentPanel.Attach(item, equipment);
            }

            return item;
        }

        // ---------------------------------------------------------------
        // 化合物
        // ---------------------------------------------------------------

        private static GameItem CreateCompound(CompoundDef def)
        {
            GameItem item = ItemDirectory.CreateEmptyItem(def.ItemId);

            string formula = ChemistryCatalog.FormatFormulaForDisplay(def.Formula);
            item.name = Loc.Pick(def.NameZh, def.NameEn) + " " + formula;
            item.identifierName = def.Key;
            item.unitBaseValue = def.Value;
            item.unitValue = ItemPricing.CompoundItemValue(def);
            item.unitCount = 1;
            // 固体化合物（1 件 = 1 mol）按式量折算堆叠上限；液体/气体是容器，仍按容量合并。
            if (!ReagentBottle.IsLiquid(def.ItemId) && !ReagentBottle.IsGas(def.ItemId))
            {
                int maxStack = ItemMass.MaxStack(ItemMass.UnitMass(def.ItemId), ItemPricing.CompoundItemValue(def));
                item.maxSlotAmountItemFunc = (_, _, _) => maxStack;
                item.mayStackItemFunc = (first, second) => first.identifier == second.identifier
                    && GeneralHelper.IsItemOwned(first) == GeneralHelper.IsItemOwned(second);
            }

            string category = ChemistryCatalog.CompoundCategoryLabel(def.Category, Loc.IsChinese);
            string describedFormula = def.Key == "acetic_acid"
                ? formula + Loc.Pick("（CH<sub>3</sub>COOH）", " (CH<sub>3</sub>COOH)")
                : formula;
            item.shortDescription = Loc.Get(
                "item_compound_short",
                "化学式 {0}\n{1}，纯度足够直接使用。",
                "Formula {0}\n{1}, pure enough to use directly.",
                describedFormula, category);
            if (!double.IsNaN(def.TemperatureC) && !double.IsInfinity(def.TemperatureC))
            {
                item.shortDescription += Loc.Get(
                    "item_compound_temperature",
                    "\n温度：{0:0.#} °C / {1:0.##} K",
                    "\nTemperature: {0:0.#} °C / {1:0.##} K",
                    def.TemperatureC, def.TemperatureC + 273.15);
            }
            item.longDescription = Loc.Pick(def.DescZh, def.DescEn) + "\n" + BuildRecipeLine(def);
            item.flavorText = Loc.Get(
                "flavor_compound",
                "瓶身贴着手写的标签，字迹潦草。",
                "A handwritten label is taped to the bottle.");

            if (def.Category == "acid")
            {
                item.canUseOutsideCombat = true;
                item.longDescription += "\n" + LabRefining.AcidInstructions;
            }
            if (ReagentBottle.RequiresPlastic(def.SpriteKey))
            {
                item.shortDescription += Loc.Pick(" 会腐蚀玻璃并损失倒入的试剂；塑料瓶可以保留试剂。",
                    " Damages glass and wastes the transferred reagent in-game. Use a plastic bottle to keep it.");
            }
            ApplyCommon(item, MaterialItemArt.Apply(item, def.SpriteKey), "CHEMICAL_SUPPLIES");

            return item;
        }

        private static string BuildRecipeLine(CompoundDef def)
        {
            if (def.Key == NitricProductionData.Key)
            {
                return NitricProductionData.RouteDescription;
            }
            StringBuilder builder = new StringBuilder();
            builder.Append(Loc.Get("recipe_prefix", "合成配比：", "Synthesis: "));

            int index = 0;
            foreach (KeyValuePair<string, int> pair in def.Inputs)
            {
                if (index > 0)
                {
                    builder.Append(" + ");
                }

                ElementDef element = ChemistryCatalog.FindElement(pair.Key);
                string name = element != null ? Loc.Pick(element.NameZh, element.NameEn) : pair.Key;
                builder.Append(pair.Value).Append(' ').Append(name);
                index++;
            }

            return builder.ToString();
        }

        // ---------------------------------------------------------------
        // 公共部分
        // ---------------------------------------------------------------

        private static void EnableSupplyStacking(GameItem item)
        {
            // 同类型可叠，但每格总质量不超过 ItemMass.StackLimitGrams。
            int maxStack = ItemPricing.StackLimitFor(item);
            item.maxSlotAmountItemFunc = (_, _, _) => maxStack;
            item.mayStackItemFunc = (first, second) => first.identifier == second.identifier
                && GeneralHelper.IsItemOwned(first) == GeneralHelper.IsItemOwned(second);
        }

        private static void ApplyCommon(GameItem item, string spriteKey, string gameType)
        {
            // 用原版就有的类型名，工具提示才能翻译成人话；不要发明 CHEM_xxx，
            // 否则游戏会按 TypeHelper.GetTypeDisplayName 去查 Mechanic 表并显示 Translation Error。
            if (!string.IsNullOrEmpty(gameType))
            {
                item.SetGameItemType(gameType);
            }

            try
            {
                item.SetSpriteAndShapeFromMod(ChemistryCatalog.ModId, spriteKey);
                item.SetShape(CreateFootprint(item.identifier, spriteKey));
                if (item is GameItemElement element)
                {
                    Image image = element.background?.image;
                    if (image != null && image.GetComponent<SpriteAlphaHitPadding>() == null)
                    {
                        image.gameObject.AddComponent<SpriteAlphaHitPadding>();
                    }
                }
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("贴图设置失败：" + spriteKey + " " + e.Message);
            }

            // 价格按单位数量校准，瓶装液体再按当前容量分摊。
            ItemPricing.Attach(item);

            // 克数写进游戏称重系统（WEIGHT_TAG），店里电子秤才能称出真实重量；
            // 只对 chem_ 前缀的模组物品生效，原版物品不经这里。
            ItemMass.AttachWeight(item);
        }

        private static GridShapeBuilder CreateFootprint(string itemId, string spriteKey)
        {
            if (itemId == RefiningStation.ItemId)
            {
                return new GridShapeBuilder().SetData(StationCells, StationWidth);
            }
            if (itemId == BulkContainers.DrumId || spriteKey is BulkContainers.DrumSprite
                or BulkContainers.DrumSourceSprite or BulkContainers.CompactDrumSprite)
            {
                return new GridShapeBuilder(3, 3).SetDataFill(1);
            }
            if (itemId == IndustrialFactoryData.Frame)
            {
                return new GridShapeBuilder(4, 4).SetDataFill(1);
            }
            switch (spriteKey)
            {
                case MaterialItemArt.TitrationSetTwoByThreeSpriteKey:
                case MaterialItemArt.VolumetricFlaskTwoByThreeSpriteKey:
                case MaterialItemArt.SeparatingFunnelTwoByThreeSpriteKey:
                    return new GridShapeBuilder(2, 3).SetDataFill(1);
                case MaterialItemArt.NarrowThermometerSpriteKey:
                case MaterialItemArt.NarrowVolumetricFlaskSpriteKey:
                case MaterialItemArt.NarrowTestTubeSpriteKey:
                    return new GridShapeBuilder(1, 2).SetDataFill(1);
                case "tool_drying_oven":
                case "tool_rotary_evaporator":
                case "tool_freeze_dryer":
                    return new GridShapeBuilder(3, 3).SetDataFill(1);
                case "tool_ultrasonic_cleaner":
                    return new GridShapeBuilder(3, 2).SetDataFill(1);
                case "tool_glovebox":
                    return new GridShapeBuilder(4, 3).SetDataFill(1);
            }
            if (MaterialItemArt.IsThreeByThreeToolSprite(spriteKey))
            {
                return new GridShapeBuilder(3, 3).SetDataFill(1);
            }
            if (spriteKey == BulkContainers.LiquidWasteSourceSprite || spriteKey == BulkContainers.SolidWasteSourceSprite)
            {
                return new GridShapeBuilder(3, 3).SetDataFill(1);
            }
            int width = MaterialItemArt.IsNarrowGasVial(spriteKey) || MaterialItemArt.IsSmallGasVial(spriteKey)
                ? 1 : StandardItemSize;
            int height = MaterialItemArt.IsTallGasSprite(spriteKey) ? 4 : StandardItemSize;
            return new GridShapeBuilder(width, height).SetDataFill(1);
        }

        internal static string PreferredContainerSprite(string key)
        {
            switch (key)
            {
                case "lab_steel_cylinder_bulk": return BulkContainers.SteelSprite;
                case BulkContainers.DrumSourceSprite: return BulkContainers.DrumSprite;
                case BulkContainers.LiquidWasteSourceSprite: return BulkContainers.LiquidWasteSprite;
                case BulkContainers.SolidWasteSourceSprite: return BulkContainers.SolidWasteSprite;
                case "lab_gas_vial_small": return MaterialItemArt.SmallGasVialSpriteKey;
                case "tool_thermometer": return MaterialItemArt.ThermometerSpriteKey;
                case "tool_volumetric_flask": return MaterialItemArt.VolumetricFlaskSpriteKey;
                case "tool_test_tube": return MaterialItemArt.TestTubeSpriteKey;
                case "lab_gas_vial_large": return MaterialItemArt.TallGasVialSprite;
                case "lab_damaged_gas_vial_large": return MaterialItemArt.TallDamagedGasVialSprite;
                case BulkContainers.CompactDrumSprite: return BulkContainers.DrumSprite;
                case "lab_liquid_waste_bulk": return BulkContainers.LiquidWasteSprite;
                case "lab_solid_waste_bulk": return BulkContainers.SolidWasteSprite;
                default: return MaterialItemArt.PreferredToolSprite(key);
            }
        }

        internal static void SetContainerSprite(GameItem item, string spriteKey)
        {
            item.SetSprite(ChemistryCatalog.ModId, spriteKey);
            GridShapeBuilder footprint = CreateFootprint(item.identifier, spriteKey);
            if (!HasSameCells(item.shape, footprint))
            {
                item.SetShape(footprint.SetTransform(item.modifiedShape));
            }
            ItemPricing.Sync(item);
        }

        internal static bool TryUseCompactContainerFootprint(GameItem item)
        {
            string compact = item.spritePath switch
            {
                BulkContainers.SteelSprite => "lab_steel_cylinder_bulk",
                MaterialItemArt.TallGasVialSprite => "lab_gas_vial_large",
                MaterialItemArt.TallDamagedGasVialSprite => "lab_damaged_gas_vial_large",
                BulkContainers.LiquidWasteSprite => "lab_liquid_waste_bulk",
                BulkContainers.SolidWasteSprite => "lab_solid_waste_bulk",
                _ => null,
            };
            if (compact == null)
            {
                return false;
            }
            SetContainerSprite(item, compact);
            return true;
        }

        internal static void RestoreSavedFootprints()
        {
            int blocked = 0;
            foreach (GameItem item in StoreInventory.AllItems())
            {
                GameInventory inventory = item.parentInventory;
                if (inventory == null || !item.identifier.StartsWith(ChemistryCatalog.ItemPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                // 读档会覆盖工厂设置的形状，保留原坐标、旋转和翻转。
                GridShape saved = item.shape;
                string sprite = PreferredContainerSprite(item.spritePath);
                string material = item.identifier.Substring(ChemistryCatalog.ItemPrefix.Length);
                if (sprite == material && ReagentBottle.IsGas(item.identifier))
                {
                    sprite = MaterialItemArt.SpriteKeyFor(material);
                }
                GridShapeBuilder footprint = CreateFootprint(item.identifier, sprite).SetTransform(saved);
                bool sameCells = HasSameCells(saved, footprint);
                if (sameCells && sprite == item.spritePath)
                {
                    ItemPricing.Sync(item);
                    continue;
                }

                if (sameCells || inventory.CheckInventorySlotStillValid(item, footprint, item.modifiedState) ||
                    TryFindSavedItemPosition(item, inventory, footprint))
                {
                    if (sprite != item.spritePath)
                    {
                        item.SetSprite(ChemistryCatalog.ModId, sprite);
                    }
                    item.SetShape(footprint);
                    item.Validate();
                }
                else
                {
                    blocked++;
                }
                ItemPricing.Sync(item);
            }
            if (blocked > 0)
            {
                ChemistryMod.Log?.Warning("有 " + blocked + " 件旧物品在原库存找不到可用位置，暂时保留存档占格。");
            }
        }

        private static bool TryFindSavedItemPosition(GameItem item, GameInventory inventory, GridShapeBuilder footprint)
        {
            if (!(inventory is GameGridInventory grid))
            {
                return false;
            }
            for (int y = 0; y <= grid.inventoryShape.height - footprint.globalHeight; y++)
            {
                for (int x = 0; x <= grid.inventoryShape.width - footprint.globalWidth; x++)
                {
                    footprint.SetPosition(x, y);
                    if (grid.CheckInventorySlotStillValid(item, footprint, item.modifiedState))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool HasSameCells(GridShape saved, GridShape expected)
        {
            if (saved.width != expected.width || saved.height != expected.height)
            {
                return false;
            }
            for (int y = 0; y < expected.height; y++)
            {
                for (int x = 0; x < expected.width; x++)
                {
                    if (saved.GetLocal(x, y) != expected.GetLocal(x, y))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

    }
}
