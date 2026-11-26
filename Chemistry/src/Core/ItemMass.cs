using System;
using System.Collections.Generic;
using System.Globalization;

namespace Chemistry
{
    /// <summary>
    /// Integer gram accounting for Chemistry items, stack limits, and the game's weight tag.
    /// Element inventory units use one common mass. Atomic weights remain scientific reference
    /// data for the periodic table and compound formulas.
    /// </summary>
    internal static class ItemMass
    {
        public const int ElementUnitGrams = 1;
        public const int StackLimitGrams = 200;

        /// <summary>Mass of one inventory unit in whole grams; zero means unregistered.</summary>
        public static int UnitMass(string itemId)
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

            if (ChemistryCatalog.FindElementByItemId(itemId) != null)
            {
                return ElementUnitGrams;
            }

            // Fluid item counts are integer container units, so calculate the mass of one such unit.
            if (ReagentBottle.IsLiquid(itemId) || ReagentBottle.IsGas(itemId))
            {
                return FluidUnitGrams(itemId);
            }

            CompoundDef compound = FindCompoundByItemId(itemId);
            if (compound != null)
            {
                return WholeGrams(MolarMass(compound));
            }

            return FixedGrams(itemId);
        }

        /// <summary>200 gram slot limit converted to an integer item count.</summary>
        public static int MaxStack(int unitGrams)
        {
            if (unitGrams <= 0)
            {
                return LabStorage.MaxCountPerSlot;
            }

            int byMass = StackLimitGrams / unitGrams;
            return Math.Min(LabStorage.MaxCountPerSlot, Math.Max(1, byMass));
        }

        /// <summary>Limits a stack by both the mass budget and the sale value budget.</summary>
        public static int MaxStack(int unitGrams, int unitValue)
        {
            int byMass = MaxStack(unitGrams);
            if (unitValue <= 0)
            {
                return byMass;
            }

            int byValue = Math.Max(1, ItemPricing.MaxSingleSaleValue / unitValue);
            return Math.Min(byMass, byValue);
        }

        /// <summary>Mass tooltip for the current integer count.</summary>
        public static string TooltipLine(GameItem item)
        {
            string id = item?.identifier;
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            int unit = UnitMass(id);
            if (unit <= 0)
            {
                return null;
            }

            long total = (long)unit * Math.Max(1, item.unitCount);
            return Line(total);
        }

        // ---------------------------------------------------------------
        // Game weight tag synchronization
        // ---------------------------------------------------------------

        /// <summary>Writes the current integer weight and keeps it synced with stack changes.</summary>
        public static void AttachWeight(GameItem item)
        {
            if (item == null)
            {
                return;
            }

            SyncWeight(item);
            item.onChangeAmountItemFunc += SyncWeight;
        }

        /// <summary>Write whole grams to WEIGHT_TAG for Chemistry items only.</summary>
        public static void SyncWeight(GameItem item)
        {
            string id = item?.identifier;
            if (string.IsNullOrEmpty(id) || !id.StartsWith(ChemistryCatalog.ItemPrefix, StringComparison.Ordinal))
            {
                return;
            }

            int unit = UnitMass(id);
            if (unit <= 0)
            {
                return;
            }

            long total = (long)unit * Math.Max(1, item.unitCount);
            GeneralHelper.SetItemWeight(item, (int)Math.Min(int.MaxValue, total));
        }

        /// <summary>Whole gram mass of fluid contents; empty or unregistered contents weigh zero.</summary>
        public static long ContentGrams(string itemId, int units)
        {
            if (units <= 0)
            {
                return 0;
            }

            if (ChemistryCatalog.FindElementByItemId(itemId) != null)
            {
                return (long)ElementUnitGrams * units;
            }

            if (!ReagentBottle.IsLiquid(itemId) && !ReagentBottle.IsGas(itemId))
            {
                return 0;
            }

            return (long)FluidUnitGrams(itemId) * units;
        }

        public static string Line(long grams)
        {
            return Loc.Get("item_mass_line", "质量  {0}", "Mass  {0}", Format(grams));
        }

        /// <summary>Format an integer gram value without fractional mass units.</summary>
        public static string Format(long grams)
        {
            if (grams <= 0)
            {
                return "?";
            }

            if (grams < 1000)
            {
                return grams.ToString(CultureInfo.InvariantCulture) + " g";
            }

            long whole = grams / 1000;
            int remainder = (int)(grams % 1000);
            if (remainder == 0)
            {
                return whole.ToString(CultureInfo.InvariantCulture) + " kg";
            }

            string fraction = remainder.ToString("D3", CultureInfo.InvariantCulture).TrimEnd('0');
            return whole.ToString(CultureInfo.InvariantCulture) + "." + fraction + " kg";
        }

        // ---------------------------------------------------------------
        // Formula mass used for non-fluid compounds and gas calculations
        // ---------------------------------------------------------------

        private static readonly Dictionary<string, double> MolarCache = new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>Compound formula mass in g/mol; zero when its formula cannot be parsed.</summary>
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
        // Fluid mass conversion
        // ---------------------------------------------------------------

        private static int FluidUnitGrams(string itemId)
        {
            double grams;
            if (ReagentBottle.IsGas(itemId))
            {
                double molar = MolarMassOf(itemId);
                grams = molar > 0
                    ? molar / 22414d * ReagentBottle.MillilitersPerUnit
                    : 0;
            }
            else
            {
                grams = ReagentBottle.MillilitersPerUnit * LiquidDensity(itemId);
            }

            // The integer gram scale cannot represent sub-gram gas portions. Keep every measurable
            // fluid unit at least one gram so bottle counts and the weight tag stay in integer units.
            return grams > 0 ? Math.Max(1, WholeGrams(grams)) : 0;
        }

        private static int WholeGrams(double grams)
        {
            if (grams <= 0 || double.IsNaN(grams))
            {
                return 0;
            }

            if (grams >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)Math.Round(grams, MidpointRounding.AwayFromZero);
        }

        // Common liquid density estimates (g/mL); unspecified liquids default to water density.
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
        // Fixed integer mass for tools, containers, and equipment
        // ---------------------------------------------------------------

        private static int FixedGrams(string itemId)
        {
            if (itemId.StartsWith(ChemistryCatalog.ItemPrefix + "leachate_", StringComparison.Ordinal))
            {
                return 130;
            }
            if (itemId.StartsWith(ChemistryCatalog.ItemPrefix + "filtrate_", StringComparison.Ordinal))
            {
                return 85;
            }
            return Fixed.TryGetValue(itemId, out int grams) ? grams : 0;
        }

        private static readonly Dictionary<string, int> Fixed = BuildFixed();

        private static Dictionary<string, int> BuildFixed()
        {
            var table = new Dictionary<string, int>(StringComparer.Ordinal);

            var tools = new Dictionary<string, int>
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
                if (tools.TryGetValue(tool.Key, out int grams))
                {
                    table[ChemistryCatalog.ToolItemId(tool.Key)] = grams;
                }
            }

            AddBottleFamily(table, ReagentBottle.GlassEmpties, 25, 45, 70);
            AddBottleFamily(table, ReagentBottle.PlasticEmpties, 18, 32, 50);
            AddBottleFamily(table, ReagentBottle.GasEmpties, 55, 80, 110);
            table[ReagentBottle.DamagedGlassId] = 40;
            table[ReagentBottle.DamagedPlasticId] = 26;
            table[ReagentBottle.DamagedGasId] = 72;

            table[BulkContainers.DrumId] = 1400;
            table[BulkContainers.SteelId] = 2600;
            table[BulkContainers.EmptyLiquidWasteId] = 1500;
            table[BulkContainers.LiquidWasteId] = 1500;
            table[BulkContainers.EmptySolidWasteId] = 2200;
            table[BulkContainers.SolidWasteId] = 2200;

            table[LabMixtureData.Cake] = 180;
            table[LabMixtureData.SaltCake] = 230;
            table[LabMixtureData.Crystals] = 160;
            table[LabAnalysisData.Carbon] = 65;
            table[LabAnalysisData.Spent] = 240;
            table[LabProcessData.SealedChalk] = 215;
            table[LabProcessData.SealedSalt] = 190;
            table[LabProcessData.LoadedCrystals] = 175;

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

        private static void AddBottleFamily(Dictionary<string, int> table, string[] ids,
            int small, int medium, int large)
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
