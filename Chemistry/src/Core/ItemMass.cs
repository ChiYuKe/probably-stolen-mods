using System;
using System.Collections.Generic;

namespace Chemistry
{
    /// <summary>
    /// 统一的单件质量（克）解析、「同类型最多叠 200 克」的堆叠上限，
    /// 以及把克数写进游戏自带称重系统（WEIGHT_TAG）的同步逻辑。
    ///
    /// 质量来源（按顺序）：
    ///   · 样品：SampleTable 里的实测克数；
    ///   · 元素：摩尔质量（1 件 = 1 mol）；
    ///   · 化合物：按化学式折算的式量；
    ///   · 中间产物、器皿、工具、设备：杂项表固定值；
    ///   · 液体/气体试剂：瓶内实装质量按 25 mL/单位 × 密度折算，见 <see cref="ContentGrams"/>。
    ///
    /// 返回 0 表示该物品没有登记质量：提示框不显示质量行，堆叠也不套 200 克上限，也不写称重标签。
    /// 液体瓶罐是容器（按容量合并），不套质量上限，只显示实装质量。
    /// </summary>
    internal static class ItemMass
    {
        public const double StackLimitGrams = 200;

        /// <summary>单件质量（克）；0 = 未登记。</summary>
        public static double UnitMass(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            SampleDef sample = SampleTable.Find(itemId);
            if (sample != null)
            {
                return sample.Grams;
            }

            ElementDef element = ChemistryCatalog.FindElementByItemId(itemId);
            if (element != null)
            {
                return element.Mass;
            }

            CompoundDef compound = FindCompoundByItemId(itemId);
            if (compound != null)
            {
                return MolarMass(compound);
            }

            double fixedGrams = FixedGrams(itemId);
            if (fixedGrams > 0)
            {
                return fixedGrams;
            }

            // 液体/气体试剂按每单位 25 mL 折算成实装质量。
            if (ReagentBottle.IsLiquid(itemId))
            {
                return ReagentBottle.MillilitersPerUnit * LiquidDensity(itemId);
            }
            if (ReagentBottle.IsGas(itemId))
            {
                return MolarMassOf(itemId) / 22414d * ReagentBottle.MillilitersPerUnit;
            }

            return 0;
        }

        /// <summary>200 克上限换算成每格件数；未登记的沿用原来的大堆叠。</summary>
        public static int MaxStack(double unitGrams)
        {
            if (unitGrams <= 0)
            {
                return LabStorage.MaxCountPerSlot;
            }

            int byMass = (int)(StackLimitGrams / unitGrams);
            return Math.Min(LabStorage.MaxCountPerSlot, Math.Max(1, byMass));
        }

        /// <summary>提示框质量行：只显示当前物品的真实总质量（单件质量 × 件数），不按件拆分。
        /// 瓶装液体/气体的单件质量本就是瓶内实装质量，元素统一按「1 件 = 1 mol」折算。</summary>
        public static string TooltipLine(GameItem item)
        {
            string id = item?.identifier;
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            double unit = UnitMass(id);
            if (unit <= 0)
            {
                return null;
            }

            return Line(unit * Math.Max(1, item.unitCount));
        }

        // ---------------------------------------------------------------
        // 游戏称重系统（店里电子秤读的就是 WEIGHT_TAG）
        // ---------------------------------------------------------------

        /// <summary>物品生成时挂上：先按当前件数写一次称重克数，再订阅数量变化，
        /// 之后合并/拆分堆叠都会自动重写。原版物品不走物品工厂，天然不受影响。</summary>
        public static void AttachWeight(GameItem item)
        {
            if (item == null)
            {
                return;
            }

            SyncWeight(item);
            item.onChangeAmountItemFunc += SyncWeight;
        }

        /// <summary>
        /// 把当前总质量写进游戏自带的称重标签（WEIGHT_TAG，整克）。
        /// 只处理 chem_ 前缀的模组物品——原版物品（包括复用原版 ID 的 scrap_metal）一律不碰，
        /// 定价也不会动：模组物品不在原版按公斤计价的 basePriceDictionary 里。
        /// </summary>
        public static void SyncWeight(GameItem item)
        {
            string id = item?.identifier;
            if (string.IsNullOrEmpty(id) || !id.StartsWith(ChemistryCatalog.ItemPrefix, StringComparison.Ordinal))
            {
                return;
            }

            double unit = UnitMass(id);
            if (unit <= 0)
            {
                return;
            }

            int grams = (int)Math.Round(unit * Math.Max(1, item.unitCount));
            GeneralHelper.SetItemWeight(item, grams);
        }

        /// <summary>瓶内实装质量（克）；空瓶返回 0。</summary>
        public static double ContentGrams(string itemId, int units)
        {
            if (units <= 0)
            {
                return 0;
            }

            double milliliters = units * (double)ReagentBottle.MillilitersPerUnit;
            if (ReagentBottle.IsGas(itemId))
            {
                double molar = MolarMassOf(itemId);
                return molar > 0 ? milliliters / 22414d * molar : 0;
            }

            return milliliters * LiquidDensity(itemId);
        }

        public static string Line(double grams)
        {
            return Loc.Get("item_mass_line", "质量  {0}", "Mass  {0}", Format(grams));
        }

        /// <summary>公斤/克自动切换，小数点与语言无关。</summary>
        public static string Format(double grams)
        {
            return grams >= 1000d ? ChemUi.MassText(grams / 1000d) + " kg" : ChemUi.MassText(grams) + " g";
        }

        // ---------------------------------------------------------------
        // 化学式量
        // ---------------------------------------------------------------

        private static readonly Dictionary<string, double> MolarCache = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>化合物的摩尔质量（式量，g/mol）；解析失败返回 0。</summary>
        public static double MolarMass(CompoundDef compound)
        {
            if (compound == null || string.IsNullOrEmpty(compound.Key))
            {
                return 0;
            }

            if (MolarCache.TryGetValue(compound.Key, out double cached))
            {
                return cached;
            }

            double total = 0;
            if (FormulaParser.TryParse(compound.Formula, out Dictionary<string, int> counts, out _))
            {
                foreach (KeyValuePair<string, int> pair in counts)
                {
                    ElementDef element = ChemistryCatalog.FindElement(pair.Key);
                    if (element != null)
                    {
                        total += element.Mass * pair.Value;
                    }
                }
            }
            else
            {
                ChemUi.Log?.Warning("计算式量失败：" + compound.Key + " (" + compound.Formula + ")");
            }

            MolarCache[compound.Key] = total;
            return total;
        }

        private static double MolarMassOf(string itemId)
        {
            ElementDef element = ChemistryCatalog.FindElementByItemId(itemId);
            if (element != null)
            {
                return element.Mass;
            }

            return MolarMass(FindCompoundByItemId(itemId));
        }

        private static CompoundDef FindCompoundByItemId(string itemId)
        {
            foreach (CompoundDef compound in ChemistryCatalog.Compounds)
            {
                if (compound.ItemId == itemId)
                {
                    return compound;
                }
            }

            return null;
        }

        // ---------------------------------------------------------------
        // 常见液体密度（g/mL）；其余默认按 1.0。
        // ---------------------------------------------------------------

        private static readonly Dictionary<string, double> LiquidDensities = new Dictionary<string, double>
        {
            { "hydrogen_peroxide", 1.11 },
            { "methanol", 0.79 },
            { "ethanol", 0.79 },
            { "acetic_acid", 1.05 },
            { "hydrochloric_acid", 1.19 },
            { "sulfuric_acid", 1.83 },
            { "nitric_acid", 1.51 },
            { "phosphoric_acid", 1.69 },
            { "benzene", 0.88 },
            { "toluene", 0.87 },
        };

        private static double LiquidDensity(string itemId)
        {
            const string marker = "cmp_";
            string key = itemId;
            if (key != null && key.StartsWith(ChemistryCatalog.ItemPrefix, StringComparison.Ordinal))
            {
                key = key.Substring(ChemistryCatalog.ItemPrefix.Length);
                if (key.StartsWith(marker, StringComparison.Ordinal))
                {
                    key = key.Substring(marker.Length);
                    return LiquidDensities.TryGetValue(key, out double density) ? density : 1.0;
                }
            }

            return 1.0;
        }

        // ---------------------------------------------------------------
        // 非化学计量物品的固定质量（克）：器皿、工具、样品瓶、设备。
        // ---------------------------------------------------------------

        private static double FixedGrams(string itemId)
        {
            if (itemId.StartsWith(ChemistryCatalog.ItemPrefix + "leachate_", StringComparison.Ordinal))
            {
                return 130;
            }
            if (itemId.StartsWith(ChemistryCatalog.ItemPrefix + "filtrate_", StringComparison.Ordinal))
            {
                return 85;
            }
            return Fixed.TryGetValue(itemId, out double grams) ? grams : 0;
        }

        private static readonly Dictionary<string, double> Fixed = BuildFixed();

        private static Dictionary<string, double> BuildFixed()
        {
            var table = new Dictionary<string, double>(StringComparer.Ordinal);

            // 实验工具（ToolTable 的 32 件；光谱仪等大型设备以公斤计）。
            var tools = new Dictionary<string, double>
            {
                { "beaker", 65 },
                { "conical_flask", 85 },
                { "round_flask", 110 },
                { "volumetric_flask", 95 },
                { "test_tube", 18 },
                { "test_tube_rack", 220 },
                { "graduated_cylinder", 75 },
                { "pipette", 25 },
                { "burette", 140 },
                { "petri_dish", 30 },
                { "glass_rod", 35 },
                { "watch_glass", 28 },
                { "bunsen_burner", 480 },
                { "heating_mantle", 900 },
                { "crucible", 50 },
                { "mortar_pestle", 350 },
                { "balance", 1600 },
                { "thermometer", 40 },
                { "ph_meter", 260 },
                { "spectrometer", 3200 },
                { "chromatograph", 2800 },
                { "centrifuge", 4500 },
                { "distiller", 1900 },
                { "separating_funnel", 95 },
                { "filter_funnel", 45 },
                { "microscope", 1300 },
                { "fume_hood", 24000 },
                { "hotplate_stirrer", 1100 },
                { "autoclave", 5200 },
                { "titration_set", 800 },
                { "goggles", 32 },
                { "lab_coat", 420 },
            };
            foreach (ToolDef tool in ChemistryCatalog.Tools)
            {
                if (tools.TryGetValue(tool.Key, out double grams))
                {
                    table[ChemistryCatalog.ToolItemId(tool.Key)] = grams;
                }
            }

            // 空瓶与破损瓶（small / medium / large 三档）。
            AddBottleFamily(table, ReagentBottle.GlassEmpties, 25, 45, 70);
            AddBottleFamily(table, ReagentBottle.PlasticEmpties, 18, 32, 50);
            AddBottleFamily(table, ReagentBottle.GasEmpties, 55, 80, 110);
            table[ReagentBottle.DamagedGlassId] = 40;
            table[ReagentBottle.DamagedPlasticId] = 26;
            table[ReagentBottle.DamagedGasId] = 72;

            // 桶装容器与废物桶。
            table[BulkContainers.DrumId] = 1400;
            table[BulkContainers.SteelId] = 2600;
            table[BulkContainers.EmptyLiquidWasteId] = 1500;
            table[BulkContainers.LiquidWasteId] = 1500;
            table[BulkContainers.EmptySolidWasteId] = 2200;
            table[BulkContainers.SolidWasteId] = 2200;

            // 混合物/分析/流程里的固体形态。
            table[LabMixtureData.Cake] = 180;
            table[LabMixtureData.SaltCake] = 230;
            table[LabMixtureData.Crystals] = 160;
            table[LabAnalysisData.Carbon] = 65;
            table[LabAnalysisData.Spent] = 240;
            table[LabProcessData.SealedChalk] = 215;
            table[LabProcessData.SealedSalt] = 190;
            table[LabProcessData.LoadedCrystals] = 175;

            // 硝酸生产线与厂房设备。
            table[NitricProductionData.CleanFeed] = 120;
            table[NitricProductionData.Crude] = 260;
            table[NitricProductionData.Refined] = 95;
            table[NitricProductionData.Parts] = 340;
            table[NitricProductionData.Power] = 480;
            table[NitricProductionData.LabModule] = 8000;
            table[NitricProductionData.PipeModule] = 9500;
            table[IndustrialFactoryData.Frame] = 32000;
            table[IndustrialFactoryData.Transport] = 9200;
            table[RefiningStation.ItemId] = 26000;
            table[AcidStation.ItemId] = 24000;

            return table;
        }

        private static void AddBottleFamily(Dictionary<string, double> table, string[] ids,
            double small, double medium, double large)
        {
            if (ids == null || ids.Length < 3)
            {
                return;
            }

            table[ids[0]] = small;
            table[ids[1]] = medium;
            table[ids[2]] = large;
        }
    }
}
