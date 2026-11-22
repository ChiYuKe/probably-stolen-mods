using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Chemistry
{
    /// <summary>
    /// F9 调试面板用的入口和开局补给。实验室储物格打开时，手动补给优先放入实验室；
    /// 否则放进当铺容器，不偷偷扣钱，方便测试配方。
    /// </summary>
    internal static class DebugKit
    {
        private const string StarterKey = "starter_kit_given";
        private const int TestRunsPerCase = 2;

        /// <summary>开局补给：几瓶常用单质 + 一套基础玻璃器皿，够做水和盐。</summary>
        private static readonly string[] StarterElements = { "H", "O", "C", "N", "Na", "Cl", "Ca", "Fe", "Cu", "S" };
        private static readonly int[] StarterUnits = { 4, 3, 3, 2, 2, 2, 1, 1, 1, 1 };
        private static readonly string[] StarterTools =
        {
            "beaker", "conical_flask", "test_tube", "bunsen_burner", "balance", "thermometer",
        };
        private static readonly (string SampleId, string Symbol)[] RefiningTestCases =
        {
            ("chem_sample_01", "Fe"),
            ("chem_sample_05", "Cu"),
        };

        /// <summary>
        /// 开局补给。每件物品单独 1 单位分别放（大堆叠遇到半满仓库会被拆得乱七八糟），
        /// 并且逐个尝试仓库/前柜/后柜/展示柜，放不下就报错而不是悄悄消失；
        /// 只有整套都放好才记「已发放」，否则下次读档再补。
        /// </summary>
        public static void GiveStarterKitOnce()
        {
            try
            {
                if (ModHelper.GetModDataBool(ChemistryCatalog.ModId, StarterKey))
                {
                    return;
                }

                if (!ItemRegistration.Ready)
                {
                    // 物品目录还没初始化好，等下次读档再给
                    return;
                }

                int placed = 0;
                int failed = 0;

                for (int i = 0; i < StarterElements.Length; i++)
                {
                    ElementDef def = ChemistryCatalog.FindElement(StarterElements[i]);
                    if (def == null)
                    {
                        continue;
                    }

                    int units = StarterUnits[i];
                    for (int u = 0; u < units; u++)
                    {
                        if (Spawn(def.ItemId, 1))
                        {
                            placed++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                }

                foreach (string key in StarterTools)
                {
                    ToolDef tool = ChemistryCatalog.FindTool(key);
                    if (tool == null)
                    {
                        continue;
                    }

                    if (Spawn(tool.ItemId, 1))
                    {
                        placed++;
                    }
                    else
                    {
                        failed++;
                    }
                }

                if (failed > 0)
                {
                    ChemistryMod.Log?.Warning("开局补给只放进 " + placed + " 件，还有 " + failed
                        + " 件放不下（仓库/柜台都满了？）。清点空间后重新读档会再补一次。");
                    DayHudBridge.Push(
                        Loc.Get("hud_starter_kit_full", "化学实验台：仓库放不下开局补给，清理空间后重新读档可补发",
                            "Chemistry lab: storage is full, the starter kit will be re-sent on the next load."),
                        "warning",
                        10f);
                    return;
                }

                ModHelper.SetModDataBool(ChemistryCatalog.ModId, StarterKey, true);
                ChemistryMod.Log?.Log("开局补给已放进仓库：" + placed + " 件（常用元素 + 基础化学工具）");

                DayHudBridge.Push(
                    Loc.Get("hud_starter_kit", "化学实验台：开局补给已放进仓库，按 F6 打开", "Chemistry lab: starter kit delivered to storage. Press F6."),
                    "info",
                    10f);
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Error("发放开局补给失败：" + e);
            }
        }

        /// <summary>F9 手动补一套开局补给（不受「已发放」标记限制）。</summary>
        public static void GiveStarterKit()
        {
            int placed = 0;
            for (int i = 0; i < StarterElements.Length; i++)
            {
                ElementDef def = ChemistryCatalog.FindElement(StarterElements[i]);
                if (def == null)
                {
                    continue;
                }

                for (int u = 0; u < StarterUnits[i]; u++)
                {
                    if (Spawn(def.ItemId, 1, preferLab: true))
                    {
                        placed++;
                    }
                }
            }

            foreach (string key in StarterTools)
            {
                ToolDef tool = ChemistryCatalog.FindTool(key);
                if (tool != null && Spawn(tool.ItemId, 1, preferLab: true))
                {
                    placed++;
                }
            }
            ReportStoreSupply(placed, StarterUnits.Sum() + StarterTools.Length);
        }

        /// <summary>每种元素补 1 瓶（unitCount 可指定，默认 1）。</summary>
        public static void GiveAllElements(int units)
        {
            int count = 0;
            foreach (ElementDef def in ChemistryCatalog.Elements)
            {
                if (Spawn(def.ItemId, units, preferLab: true))
                {
                    count++;
                }
            }

            ChemistryMod.Log?.Log("元素补给：" + count + " 种，每种 " + units + " 单位");
            ReportStoreSupply(count, ChemistryCatalog.Elements.Length);
        }

        /// <summary>只补常用元素，测试配方时少塞点垃圾。</summary>
        public static void GiveCommonElements(int units)
        {
            string[] common = { "H", "C", "N", "O", "Na", "Mg", "Al", "Si", "P", "S", "Cl", "K", "Ca", "Fe", "Cu", "Zn" };
            int count = 0;
            foreach (string symbol in common)
            {
                ElementDef def = ChemistryCatalog.FindElement(symbol);
                if (def != null && Spawn(def.ItemId, units, preferLab: true))
                {
                    count++;
                }
            }

            ChemistryMod.Log?.Log("常用元素补给：" + count + " 种，每种 " + units + " 单位");
            ReportStoreSupply(count, common.Length);
        }

        public static void GiveAllTools()
        {
            int count = 0;
            foreach (ToolDef def in ChemistryCatalog.Tools)
            {
                if (Spawn(def.ItemId, 1, preferLab: true))
                {
                    count++;
                }
            }

            ChemistryMod.Log?.Log("工具补给：" + count + " 件");
            ReportStoreSupply(count, ChemistryCatalog.Tools.Length);
        }

        public static void GiveAllCompounds()
        {
            int count = 0;
            foreach (CompoundDef def in ChemistryCatalog.Compounds)
            {
                if (Spawn(def.ItemId, 1, preferLab: true))
                {
                    count++;
                }
            }

            ChemistryMod.Log?.Log("化合物补给：" + count + " 种");
            ReportStoreSupply(count, ChemistryCatalog.Compounds.Length);
        }

        private static void ReportStoreSupply(int placed, int requested)
        {
            string destination = LabInventoryGrid.OpenGrid != null
                ? Loc.Pick("地下室储物格", "lab storage")
                : Loc.Pick("当铺库存", "store inventory");
            string message = Loc.Get("debug_store_supply", "补给已发放到{0}：{1}/{2} 项。",
                "Delivered {1}/{2} supplies to {0}.", destination, placed, requested);
            ChemistryDebugWindow.Report(message, placed == requested);
        }

        /// <summary>每种未检测样品各一件，附检测和分离所需工具。</summary>
        public static void GiveSampleKit()
        {
            var supplies = new Dictionary<string, int>();
            foreach (SampleDef sample in SampleTable.All)
            {
                supplies.Add(sample.ItemId, 1);
            }

            foreach (string key in new[] { SampleInteraction.DetectorToolKey, SampleInteraction.RefinerToolKey, LabRefining.FilterToolKey })
            {
                supplies.Add(ChemistryCatalog.ToolItemId(key), 1);
            }
            GiveTestSupplies(supplies);
        }

        public static void GiveAcidCraftKit()
        {
            const int factoryRuns = 9;
            SampleDef highGrade = SampleTable.Find("chem_sample_13");
            SampleDef lowGrade = SampleTable.Find("chem_sample_15");
            var supplies = new Dictionary<string, int>
            {
                { ChemistryCatalog.ToolItemId("beaker"), 1 },
                { ChemistryCatalog.ToolItemId("hotplate_stirrer"), 1 },
                { ChemistryCatalog.ToolItemId(LabRefining.FilterToolKey), 1 },
                { ChemistryCatalog.ToolItemId(SampleInteraction.DetectorToolKey), 1 },
                { ChemistryCatalog.ToolItemId(SampleInteraction.RefinerToolKey), 1 },
                { highGrade.DetectedId, 1 },
                { lowGrade.DetectedId, NitricProduction.BatchLimit * NitricProductionData.FeedUnits(lowGrade.DetectedId, false) },
                { highGrade.ItemId, factoryRuns },
                { lowGrade.ItemId, NitricProductionData.FeedUnits(lowGrade.ItemId, true) },
                { SampleTable.Find("chem_sample_04").ItemId, 1 },
                { NitricProductionData.Parts, 1 },
                { NitricProductionData.Power, factoryRuns },
                { NitricProductionData.Product.ItemId, 1 },
            };

            int glassBottles = 1 + NitricProduction.BatchLimit + factoryRuns;
            int plasticBottles = 0;
            foreach (CompoundDef acid in AcidStation.StandardRecipes)
            {
                AddUnits(supplies, acid.ItemId, 1);
                int runs = TestRunsPerCase;
                if (ReagentBottle.EmptyFor(acid.ItemId) == ReagentBottle.PlasticEmptyId)
                {
                    plasticBottles += runs;
                    // 额外跑一次玻璃瓶损坏分支。
                    glassBottles++;
                    runs++;
                }
                else
                {
                    glassBottles += runs;
                }
                foreach (KeyValuePair<string, int> input in acid.Inputs)
                {
                    AddUnits(supplies, ChemistryCatalog.ElementItemId(input.Key), input.Value * runs);
                }
                foreach (string key in acid.Tools)
                {
                    supplies[ChemistryCatalog.ToolItemId(key)] = 1;
                }
            }
            foreach (bool industrial in new[] { false, true })
            {
                foreach (var (id, units) in NitricProduction.BuildRequirements(industrial))
                {
                    AddUnits(supplies, id, units);
                }
            }

            foreach (var (id, symbol) in RefiningTestCases)
            {
                SampleDef sample = SampleTable.Find(id);
                int percent = Array.Find(sample.Components, component => component.Symbol == symbol).Percent;
                supplies.Add(sample.DetectedId, LabRefining.SamplesPerProduct(percent));
            }
            supplies.Add(ReagentBottle.EmptyId, glassBottles);
            supplies.Add(ReagentBottle.PlasticEmptyId, plasticBottles);
            GiveTestSupplies(supplies);
        }

        public static void GiveFullAcidKit()
        {
            var supplies = new List<(string Id, int Units, int Size)>();
            foreach (CompoundDef acid in ChemistryCatalog.Compounds)
            {
                if (acid.Category == "acid" && ReagentBottle.IsLiquid(acid.ItemId))
                {
                    AddFullBottleSamples(supplies, acid.ItemId);
                    supplies.Add((acid.ItemId, ReagentBottle.CapacityFor(acid.ItemId, ReagentBottle.Bulk), ReagentBottle.Bulk));
                }
            }
            GiveTestSupplies(supplies);
        }

        public static void GiveBottleFillKit()
        {
            var supplies = new List<(string Id, int Units, int Size)>();
            foreach (string[] family in ReagentBottle.EmptyFamilies)
            {
                foreach (string id in family)
                {
                    supplies.Add((id, id == ReagentBottle.EmptyId ? 2 : 1, -1));
                }
            }

            foreach (string key in new[] { "water", "hydrofluoric_acid", "carbon_dioxide" })
            {
                AddFullBottleSamples(supplies, ChemistryCatalog.CompoundItemId(key));
            }

            string acetic = ChemistryCatalog.CompoundItemId("acetic_acid");
            string argon = ChemistryCatalog.ElementItemId("Ar");
            supplies.Add((acetic, ReagentBottle.CapacityFor(acetic) / 2, -1));
            supplies.Add((argon, ReagentBottle.CapacityFor(argon) / 2, -1));
            AddDamagedBottleSamples(supplies);
            GiveTestSupplies(supplies);
        }

        public static void GiveBulkContainerKit()
        {
            GiveTestSupplies(new List<(string Id, int Units, int Size)>
            {
                (BulkContainers.SteelId, 1, -1),
                (BulkContainers.DrumId, 1, -1),
                (BulkContainers.EmptyLiquidWasteId, 1, -1),
                (BulkContainers.EmptySolidWasteId, 1, -1),
                (ReagentBottle.GasEmpties[2], 1, -1),
                (ReagentBottle.GlassEmpties[2], 1, -1),
                (ChemistryCatalog.CompoundItemId("carbon_dioxide"), BulkContainers.SteelCapacity / 2, ReagentBottle.Bulk),
                (ChemistryCatalog.CompoundItemId("water"), BulkContainers.DrumCapacity / 2, ReagentBottle.Bulk),
                ("chem_sample_06", 4, -1),
            });
        }

        private static void AddFullBottleSamples(List<(string Id, int Units, int Size)> supplies, string id)
        {
            for (int size = 0; size < ReagentBottle.SizeSuffixes.Length; size++)
            {
                supplies.Add((id, ReagentBottle.CapacityFor(id, size), size));
            }
        }

        public static void GiveDamagedBottleKit()
        {
            var supplies = new List<(string Id, int Units, int Size)>();
            AddDamagedBottleSamples(supplies);
            string water = ChemistryCatalog.CompoundItemId("water");
            supplies.Add((ReagentBottle.EmptyId, 1, -1));
            supplies.Add((water, ReagentBottle.CapacityFor(water), -1));
            GiveTestSupplies(supplies);
        }

        private static void AddDamagedBottleSamples(List<(string Id, int Units, int Size)> supplies)
        {
            foreach (string id in ReagentBottle.DamagedIds)
            {
                for (int size = 0; size < ReagentBottle.SizeSuffixes.Length; size++)
                {
                    supplies.Add((id, 1, size));
                }
            }
        }

        public static void GiveLeachingKit()
        {
            var supplies = new Dictionary<string, int>();
            foreach (SampleDef sample in SampleTable.All)
            {
                foreach (var (symbol, percent) in sample.Components)
                {
                    CompoundDef acid = LabRefining.RequiredAcid(sample, symbol);
                    if (acid != null)
                    {
                        AddUnits(supplies, sample.DetectedId, TestRunsPerCase * LabRefining.SamplesPerProduct(percent));
                        AddUnits(supplies, acid.ItemId, TestRunsPerCase);
                    }
                }
            }
            GiveTestSupplies(supplies);
        }

        public static void GiveFilteringKit()
        {
            GiveIntermediateKit(filtered: false);
        }

        internal static void GiveEquipmentKit(int tier)
        {
            var supplies = LabEquipmentData.All.Where(def => def.Tier == tier)
                .ToDictionary(def => ChemistryCatalog.ToolItemId(def.Key), _ => 1);
            supplies.Add("energy_credit", 1);
            string water = ChemistryCatalog.CompoundItemId("water");
            supplies.Add(water, ReagentBottle.CapacityFor(water));
            supplies.Add(ReagentBottle.EmptyId, 3);
            supplies.Add(SampleTable.All[0].ItemId, 2);
            if (tier <= 2)
            {
                supplies.Add(LabRefining.IntermediateId("Fe", false), 2);
            }
            if (tier >= 2)
            {
                supplies.Add(SampleTable.All.First(sample => sample.Batch == "09").DetectedId, 2);
            }
            if (tier == 3)
            {
                supplies.Add(SampleTable.All.First(sample => sample.Batch == "13").ItemId, 2);
            }
            GiveTestSupplies(supplies);
        }

        internal static void GiveMixtureKit()
        {
            var supplies = new List<(string Id, int Units, int Size)>();
            foreach (string key in new[] { "beaker", "conical_flask", "separating_funnel", "filter_funnel", "centrifuge", "drying_oven" })
            {
                supplies.Add((ChemistryCatalog.ToolItemId(key), 1, -1));
            }
            for (int i = 0; i < 3; i++)
            {
                supplies.Add((LabMixtureData.Water, LabMixtureData.Portion, LabMixtureData.SmallBottle));
            }
            supplies.Add((LabMixtureData.Oil, LabMixtureData.Portion, LabMixtureData.SmallBottle));
            supplies.Add((AqueousReactions.ItemId(AqueousReactions.Acetic), LabMixtureData.Portion, LabMixtureData.SmallBottle));
            supplies.Add((AqueousReactions.ItemId("sodium_carbonate"), 1, -1));
            supplies.Add((LabMixtureData.Chalk, 1, -1));
            supplies.Add((LabMixtureData.Salt, 1, -1));
            supplies.Add((ReagentBottle.GlassEmpties[LabMixtureData.SmallBottle], 2, -1));
            GiveTestSupplies(supplies);
        }

        /// <summary>
        /// 水溶液反应测试包：钠、乙酸和全套粉剂，覆盖烧杯加料的全部反应方程
        /// （钠遇水放氢、酸碱中和、酸+碳酸盐放二氧化碳、钙盐沉淀），配套集气和过滤工具。
        /// </summary>
        internal static void GiveAqueousKit()
        {
            var supplies = new List<(string Id, int Units, int Size)>();
            foreach (string key in new[] { "beaker", "conical_flask", "filter_funnel", "fume_hood", "ph_meter" })
            {
                supplies.Add((ChemistryCatalog.ToolItemId(key), 1, -1));
            }
            for (int i = 0; i < 3; i++)
            {
                supplies.Add((LabMixtureData.Water, LabMixtureData.Portion, LabMixtureData.SmallBottle));
            }
            string acetic = AqueousReactions.ItemId(AqueousReactions.Acetic);
            supplies.Add((acetic, ReagentBottle.CapacityFor(acetic), -1));
            supplies.Add((ChemistryCatalog.ElementItemId("Na"), 2, -1));
            foreach (string key in new[] { "sodium_bicarbonate", "potassium_bicarbonate", "sodium_carbonate", "potassium_carbonate",
                "calcium_chloride", "sodium_hydroxide", "potassium_hydroxide", "calcium_hydroxide" })
            {
                supplies.Add((ChemistryCatalog.CompoundItemId(key), 1, -1));
            }
            supplies.Add((LabMixtureData.Chalk, 1, -1));
            supplies.Add((ReagentBottle.GasEmpties[LabMixtureData.SmallBottle], 2, -1));
            supplies.Add((ReagentBottle.GlassEmpties[LabMixtureData.SmallBottle], 2, -1));
            GiveTestSupplies(supplies);
        }

        /// <summary>气体处理测试包：全套采样气瓶 + 钢瓶，二氧化碳/氢气满瓶，含气水样测通风橱集气。</summary>
        internal static void GiveGasKit()
        {
            var supplies = new List<(string Id, int Units, int Size)>();
            foreach (string id in ReagentBottle.GasEmpties)
            {
                supplies.Add((id, 1, -1));
            }
            supplies.Add((BulkContainers.SteelId, 1, -1));
            string co2 = ChemistryCatalog.CompoundItemId(AqueousReactions.CarbonDioxide);
            AddFullBottleSamples(supplies, co2);
            supplies.Add((co2, ReagentBottle.CapacityFor(co2, ReagentBottle.Bulk), ReagentBottle.Bulk));
            string hydrogen = ChemistryCatalog.ElementItemId("H");
            supplies.Add((hydrogen, ReagentBottle.CapacityFor(hydrogen), ReagentBottle.Medium));
            supplies.Add((LabProcessData.MineralWater, 2, -1));
            foreach (string key in new[] { "fume_hood", "chromatograph" })
            {
                supplies.Add((ChemistryCatalog.ToolItemId(key), 1, -1));
            }
            GiveTestSupplies(supplies);
        }

        /// <summary>废物处理测试包：废液桶 + 废物桶 + 可投弃的废料（破损容器、废炭、废样品、清洗废液）。</summary>
        internal static void GiveWasteKit()
        {
            var supplies = new List<(string Id, int Units, int Size)>
            {
                (BulkContainers.EmptyLiquidWasteId, 1, -1),
                (BulkContainers.EmptySolidWasteId, 1, -1),
                (ReagentBottle.DamagedIds[0], 1, -1),
                (ReagentBottle.DamagedIds[1], 1, -1),
                (ReagentBottle.DamagedIds[2], 1, -1),
                (LabProcessData.Rinse, 2, -1),
                (LabAnalysisData.Spent, 1, -1),
                (SampleTable.VanillaScrapId, 1, -1),
                (SampleTable.All[0].ItemId, 1, -1),
                (SampleTable.All[3].ItemId, 1, -1),
                (SampleTable.All[5].ItemId, 1, -1),
            };
            string water = ChemistryCatalog.CompoundItemId("water");
            supplies.Add((water, ReagentBottle.CapacityFor(water), -1));
            supplies.Add((LabMixtureData.Water, LabMixtureData.Portion, LabMixtureData.SmallBottle));
            GiveTestSupplies(supplies);
        }

        /// <summary>
        /// 合成材料测试包：按剧情链路的用量发常用元素（够做水、酸、有机物和 TNT 等危险品），
        /// 配上危险品配方要的工具，测档案 ch2~ch5 的解锁节奏。
        /// </summary>
        internal static void GiveSynthesisKit()
        {
            var symbols = new[] { "H", "C", "N", "O", "Na", "S", "Cl", "K", "Ca", "Fe", "Cu", "Zn", "Mg", "Al", "Si", "P" };
            var units = new[] { 32, 32, 32, 32, 16, 16, 8, 8, 8, 8, 8, 8, 4, 4, 4, 4 };
            var supplies = new Dictionary<string, int>();
            for (int i = 0; i < symbols.Length; i++)
            {
                supplies.Add(ChemistryCatalog.ElementItemId(symbols[i]), units[i]);
            }
            foreach (string key in new[] { "beaker", "conical_flask", "round_flask", "distiller", "fume_hood" })
            {
                supplies.Add(ChemistryCatalog.ToolItemId(key), 1);
            }
            GiveTestSupplies(supplies);
        }

        internal static void GiveDistillationKit()
        {
            var supplies = new List<(string Id, int Units, int Size)>();
            foreach (string key in new[] { "beaker", "distiller", "rotary_evaporator", "drying_oven" })
            {
                supplies.Add((ChemistryCatalog.ToolItemId(key), 1, -1));
            }
            supplies.Add((LabDistillationData.Feed, 2, -1));
            supplies.Add((LabMixtureData.Brine, 1, -1));
            supplies.Add((LabMixtureData.Water, LabMixtureData.Portion, LabMixtureData.SmallBottle));
            supplies.Add((LabDistillationData.Ethanol, LabMixtureData.Portion, LabMixtureData.SmallBottle));
            supplies.Add((ReagentBottle.GlassEmpties[LabMixtureData.SmallBottle], 5, -1));
            GiveTestSupplies(supplies);
        }

        internal static void GiveAnalysisKit()
        {
            var supplies = new List<(string Id, int Units, int Size)>();
            foreach (string key in new[] { "beaker", "filter_funnel", "spectrometer", "titration_set", "ph_meter" })
            {
                supplies.Add((ChemistryCatalog.ToolItemId(key), 1, -1));
            }
            for (int i = 0; i < 2; i++)
            {
                supplies.Add((LabAnalysisData.Acid, 2, ReagentBottle.Medium));
                supplies.Add((LabMixtureData.Water, 2, LabMixtureData.SmallBottle));
            }
            supplies.Add((LabAnalysisData.Dye, 2, LabMixtureData.SmallBottle));
            supplies.Add((LabAnalysisData.Titrant, 6, ReagentBottle.Medium));
            supplies.Add((LabAnalysisData.Carbon, 2, -1));
            GiveTestSupplies(supplies);
        }

        internal static void GiveFactoryKit()
        {
            var supplies = new Dictionary<string, int>
            {
                { IndustrialFactoryData.Frame, 1 },
                { IndustrialFactoryData.Transport, 1 },
                { ChemistryCatalog.ToolItemId("centrifuge"), 1 },
                { ChemistryCatalog.ToolItemId("hotplate_stirrer"), 1 },
                { IndustrialFactoryData.Dryer, 1 },
                { LabMixtureData.Suspension, 6 },
                { LabMixtureData.Brine, 4 },
                { NitricProductionData.Power, 10 },
                { NitricProductionData.Parts, 3 },
                { ReagentBottle.GlassEmpties[LabMixtureData.SmallBottle], 10 }
            };
            GiveTestSupplies(supplies);
        }

        internal static void GiveProcessKit()
        {
            var supplies = new List<(string Id, int Units, int Size)>();
            foreach (string key in new[] { "beaker", "hotplate_stirrer", "conical_flask", "graduated_cylinder",
                "pipette", "conductivity_meter", "separating_funnel", "chromatograph", "spectrometer", "fume_hood",
                "filter_funnel", "centrifuge", "drying_oven", "freeze_dryer", "glovebox", "ultrasonic_cleaner" })
            {
                supplies.Add((ChemistryCatalog.ToolItemId(key), 1, -1));
            }
            for (int i = 0; i < 3; i++)
            {
                supplies.Add((LabMixtureData.Water, 2, LabMixtureData.SmallBottle));
            }
            supplies.Add((LabMixtureData.Water, 1, LabMixtureData.SmallBottle));
            supplies.Add((LabAnalysisData.Dye, 2, LabMixtureData.SmallBottle));
            supplies.Add((LabMixtureData.Oil, 2, LabMixtureData.SmallBottle));
            supplies.Add((LabProcessData.MineralWater, 1, -1));
            supplies.Add((LabMixtureData.Brine, 1, -1));
            supplies.Add((LabMixtureData.Suspension, 3, -1));
            supplies.Add((LabMixtureData.Salt, 2, -1));
            supplies.Add((LabMixtureData.Chalk, 1, -1));
            supplies.Add((ReagentBottle.GlassEmpties[LabMixtureData.SmallBottle], 7, -1));
            supplies.Add((ReagentBottle.GasEmpties[LabMixtureData.SmallBottle], 2, -1));
            GiveTestSupplies(supplies);
        }

        public static void GiveReductionKit()
        {
            GiveIntermediateKit(filtered: true);
        }

        private static void GiveIntermediateKit(bool filtered)
        {
            var supplies = new Dictionary<string, int>();
            foreach (LabRefining.Intermediate product in LabRefining.Intermediates)
            {
                if (product.Filtered == filtered)
                {
                    supplies.Add(product.ItemId, TestRunsPerCase);
                }
            }
            if (!filtered)
            {
                supplies.Add(ChemistryCatalog.ToolItemId(LabRefining.FilterToolKey), 1);
            }
            GiveTestSupplies(supplies);
        }

        private static void AddUnits(Dictionary<string, int> supplies, string id, int units)
        {
            supplies.TryGetValue(id, out int previous);
            supplies[id] = previous + units;
        }

        private static bool GiveTestSupplies(Dictionary<string, int> supplies) =>
            GiveTestSupplies(supplies.Select(supply => (supply.Key, supply.Value, -1)).ToArray());

        private static bool GiveTestSupplies(IReadOnlyCollection<(string Id, int Units, int Size)> supplies)
        {
            LabInventoryGrid grid = LabInventoryGrid.OpenGrid;
            if (!ItemRegistration.Ready || (grid == null && EmporiumEntry.Instance == null))
            {
                ReportTestSupply(Loc.Pick("请先载入存档再领取测试包。", "Load a save before claiming the test kit."), false);
                return false;
            }
            if (grid != null && (LabRefining.Running || AcidStation.Running || LabEquipmentPanel.Running || ItemMouseDragHandler.current?.IsDraggingItem == true))
            {
                ReportTestSupply(Loc.Pick("请等当前反应或拖拽结束后再领取。", "Finish the current reaction or drag before claiming supplies."), false);
                return false;
            }

            var failed = new List<string>();
            bool inventoryChanged = false;
            foreach (var (id, requestedUnits, bottleSize) in supplies)
            {
                int placedUnits = 0;
                try
                {
                    bool singleItem = ReagentBottle.IsSingleBottle(id)
                        || Array.Exists(ChemistryCatalog.Tools, tool => tool.ItemId == id);
                    int batchLimit = singleItem ? 1
                        : bottleSize >= 0 ? ReagentBottle.CapacityFor(id, bottleSize)
                        : ReagentBottle.IsBottle(id) ? ReagentBottle.CapacityFor(id) : requestedUnits;
                    while (placedUnits < requestedUnits)
                    {
                        int units = Math.Min(batchLimit, requestedUnits - placedUnits);
                        if (grid != null)
                        {
                            // 指定尺寸的样品必须整瓶发放。普通补给可拆开以填满已有堆叠。
                            while (units > 0 && !grid.TryAddDebugItem(id, units, bottleSize))
                            {
                                units = bottleSize >= 0 ? 0 : units / 2;
                            }
                        }
                        else
                        {
                            units = PlaceStoreTestSupply(id, units, bottleSize);
                        }
                        if (units == 0)
                        {
                            break;
                        }
                        placedUnits += units;
                    }
                }
                catch (Exception e)
                {
                    ChemistryMod.Log?.Warning("发放测试物品失败：" + id + "，" + e.Message);
                }
                inventoryChanged |= placedUnits > 0;
                if (placedUnits < requestedUnits)
                {
                    string variant = bottleSize < 0 ? id
                        : id + " (" + (bottleSize == ReagentBottle.Bulk ? "bulk" : ReagentBottle.SizeSuffixes[bottleSize]) + ")";
                    failed.Add(variant + " " + placedUnits + "/" + requestedUnits);
                }
            }

            int placed = supplies.Count - failed.Count;
            if (grid != null && inventoryChanged)
            {
                LabPanel.RefreshFromInventory();
            }
            string destination = grid != null ? Loc.Pick("实验室储物格", "lab storage") : Loc.Pick("当铺库存", "store inventory");
            if (failed.Count > 0)
            {
                ChemistryMod.Log?.Warning("未发放的测试物品：" + string.Join("、", failed));
            }
            string message = failed.Count == 0
                ? Loc.Get("debug_kit_delivered", "测试包已送到{0}，共 {1} 种物品。", "Test kit delivered to {0} ({1} item types).", destination, placed)
                : Loc.Get("debug_kit_partial", "测试包送到{0} {1}/{2} 种。请检查空位和日志。", "Delivered {1}/{2} test item types to {0}. Check space and the log.", destination, placed, supplies.Count);
            ReportTestSupply(message, failed.Count == 0);
            return inventoryChanged;
        }

        private static int PlaceStoreTestSupply(string id, int units, int bottleSize)
        {
            GameItem item = ModHelper.SpawnItem(id);
            if (item == null)
            {
                return 0;
            }

            var mayStack = item.mayStackItemFunc;
            try
            {
                if (bottleSize >= 0)
                {
                    ReagentBottle.SetSize(item, bottleSize);
                }
                if (bottleSize >= 0 || ReagentBottle.IsGas(id))
                {
                    item.mayStackItemFunc = (_, _) => false;
                }

                ModHelper.SetItemOwnerShip(item, true);
                item.SetUnitCount(units);
                return StoreInventory.PlaceTestSupply(item, keepWhole: bottleSize >= 0);
            }
            finally
            {
                item.mayStackItemFunc = mayStack;
                if (item.parentInventory == null && item is GameItemElement element && !element.IsDestroyed())
                {
                    item.Destroy();
                }
            }
        }

        private static void ReportTestSupply(string message, bool success)
        {
            ChemistryDebugWindow.Report(message, success);
            OverlayHandler.current?.AppendLog(message, false);
            DayHudBridge.Push(message, success ? "info" : "warn", 5f);
            ChemistryMod.Log?.Log(message);
        }

        internal static void ClearLabStorage()
        {
            LabInventoryGrid grid = LabInventoryGrid.OpenGrid;
            if (grid == null)
            {
                ChemistryDebugWindow.Report(Loc.Pick("请先进入地下实验室并打开储物格。", "Open the underground lab storage first."), false);
                return;
            }
            if (LabRefining.Running || AcidStation.Running || ItemMouseDragHandler.current?.IsDraggingItem == true)
            {
                ChemistryDebugWindow.Report(Loc.Pick("请等当前反应或拖拽结束后再清空。", "Finish the current reaction or drag before clearing storage."), false);
                return;
            }

            try
            {
                if (grid.TryClearStorage(out int removedStacks))
                {
                    ChemistryDebugWindow.Report(removedStacks == 0
                        ? Loc.Pick("当前仓库页已经是空的。", "The current page is already empty.")
                        : string.Format(Loc.Pick("当前仓库页已清空，移除 {0} 组物品。", "Current page cleared. Removed {0} item stacks."), removedStacks), true);
                    return;
                }
            }
            catch (Exception error)
            {
                ChemistryMod.Log?.Warning("清空当前仓库页失败：" + error);
            }

            ChemistryDebugWindow.Report(Loc.Pick("无法清空当前仓库页，请查看日志。", "Could not clear the current page. Check the log."), false);
        }

        /// <summary>清掉本模组写在存档里的数据（开局补给标记等），方便重测。</summary>
        public static void ClearModData()
        {
            try
            {
                ModHelper.ClearModData(ChemistryCatalog.ModId);
                ChemistryMod.Log?.Log("已清空本模组存档数据，下次读档会重新发开局补给");
                ChemistryDebugWindow.Report(Loc.Pick("已清空当前存档的化学模组数据。", "Chemistry data cleared for this save."), true);
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("清空模组数据失败：" + e.Message);
                ChemistryDebugWindow.Report(Loc.Pick("清空失败，详情见游戏日志。", "Reset failed. See the game log."), false);
            }
        }

        private static void Give(string symbol, int units)
        {
            ElementDef def = ChemistryCatalog.FindElement(symbol);
            if (def != null)
            {
                Spawn(def.ItemId, units);
            }
        }

        /// <summary>生成一件物品；调试按钮在地下实验室打开时优先放进实验室储物格。</summary>
        private static bool Spawn(string itemId, int units, bool preferLab = false)
        {
            try
            {
                LabInventoryGrid grid = LabInventoryGrid.OpenGrid;
                if (preferLab && grid != null)
                {
                    return grid.TryAddDebugItem(itemId, units);
                }

                GameItem item = ModHelper.SpawnItem(itemId);
                if (item == null)
                {
                    ChemistryMod.Log?.Warning("生成物品失败：" + itemId);
                    return false;
                }

                if (units > 1)
                {
                    item.SetUnitCount(units);
                }

                ModHelper.SetItemOwnerShip(item, true);

                if (!TryPlace(item))
                {
                    ChemistryMod.Log?.Warning("补给放不进店里：" + itemId + " ×" + units);
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("生成物品出错 " + itemId + "：" + e.Message);
                return false;
            }
        }

        /// <summary>依次尝试几个玩家容器；放进去（parentInventory 非空）就算成功。</summary>
        private static bool TryPlace(GameItem item)
        {
            return StoreInventory.TryPlace(item);
        }

        /// <summary>
        /// 盘点仓库里的化学物品，排查「补给/合成产物到底进没进仓库」用。
        /// F9 里可以手动调用，开局补给之后也会自动打一行。
        /// </summary>
        public static void LogInventorySummary()
        {
            try
            {
                Dictionary<string, int> counts = StoreInventory.CountUnits();
                int types = 0;
                int units = 0;
                var parts = new System.Text.StringBuilder();
                foreach (KeyValuePair<string, int> pair in counts)
                {
                    if (pair.Key == null || !pair.Key.StartsWith(ChemistryCatalog.ItemPrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    types++;
                    units += pair.Value;
                    if (parts.Length < 400)
                    {
                        if (parts.Length > 0)
                        {
                            parts.Append(' ');
                        }

                        parts.Append(pair.Key.Replace(ChemistryCatalog.ItemPrefix, "")).Append('×').Append(pair.Value);
                    }
                }

                // 抽查价格：游戏 GetValue 用的是 unitValue，如果没写对这里会是 0
                string priceSample = BuildPriceSample(counts);
                ChemistryMod.Log?.Log("仓库化学物品盘点：" + types + " 种、" + units + " 单位；" + parts
                    + (string.IsNullOrEmpty(priceSample) ? "" : "；价格抽查 " + priceSample));
                string message = Loc.Pick("当铺库存有 {0} 种化学物品，共 {1} 单位。明细见游戏日志。", "Store inventory has {0} chemistry item types and {1} units. See the game log for details.");
                ChemistryDebugWindow.Report(string.Format(message, types, units), true);
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("盘点仓库失败：" + e.Message);
                ChemistryDebugWindow.Report(Loc.Pick("盘点失败，详情见游戏日志。", "Inventory summary failed. See the game log."), false);
            }
        }

        /// <summary>挑几件化学物品报一下游戏内价值，验证 unitValue 生效。</summary>
        private static string BuildPriceSample(Dictionary<string, int> counts)
        {
            var builder = new System.Text.StringBuilder();
            var sampleIds = new[] { "chem_el_h", "chem_el_c", "chem_el_o", "chem_el_fe", "chem_cmp_water", "chem_tool_beaker" };

            foreach (string id in sampleIds)
            {
                if (!counts.ContainsKey(id))
                {
                    continue;
                }

                GameItem item = null;
                foreach (GameItem candidate in StoreInventory.FindConsumable(id))
                {
                    item = candidate;
                    break;
                }

                if (item == null)
                {
                    continue;
                }

                long value = 0;
                try
                {
                    value = item.GetValue(null, null, true);
                }
                catch
                {
                    value = -1;
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(id.Replace(ChemistryCatalog.ItemPrefix, "")).Append('=').Append(value);
            }

            return builder.ToString();
        }
    }
}
