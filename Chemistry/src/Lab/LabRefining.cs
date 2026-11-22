using System;
using System.Collections.Generic;
using System.Linq;

namespace Chemistry
{
    internal static class LabRefining
    {
        // 每份样品按 100 个含量单位计算。50 个目标成分单位可封装为 1 份单质。
        private const int UnitsPerProduct = 50;
        public const string FilterToolKey = "filter_funnel";
        private const int StageTicks = 48;
        private static LabInventoryGrid processingGrid;
        private static RefiningStation processingStation;
        private static long startedAt;
        public static bool Running => processingGrid != null && processingGrid == LabInventoryGrid.OpenGrid;
        internal static bool IsRunning(RefiningStation station) => Running && processingStation == station;
        public static float Progress => Running ? GameTicks.Progress(startedAt, StageTicks) : 0f;
        internal static int RemainingTicks => Running ? GameTicks.Remaining(startedAt, StageTicks) : 0;
        private static readonly Dictionary<string, Intermediate> Products = BuildIntermediates();
        public static IEnumerable<Intermediate> Intermediates => Products.Values;

        internal sealed class Intermediate
        {
            public string Symbol { get; }
            public bool Filtered { get; }
            public string ItemId { get; }
            public string SpriteKey => Filtered ? "tool_test_tube" : "tool_beaker";
            public string Name => Filtered
                ? Loc.Get("refining_filtrate", "{0}滤液", "{0} filtrate", SampleTable.ElementName(Symbol))
                : Loc.Get("refining_leachate", "{0}浸出液", "{0} leachate", SampleTable.ElementName(Symbol));
            public string Instructions => Filtered
                ? Loc.Get("refining_station_filtrate_hint", "在提炼反应台把滤液送入还原槽。", "Load the filtrate into the reduction cell at the refining station.")
                : Loc.Get("refining_leachate_hint", "把浸出液拖入投料格，再装入过滤漏斗。", "Load the leachate into the refining station and insert a funnel.");

            public Intermediate(string symbol, bool filtered)
            {
                Symbol = symbol;
                Filtered = filtered;
                ItemId = IntermediateId(symbol, filtered);
            }
        }

        public static string AcidInstructions => Loc.Get("refining_acid_hint",
            "在提炼反应台把酸拖入试剂槽，把已检测废料拖入投料格。",
            "Drag acid into the reagent socket and analyzed scrap into the refining station input grid.");

        public static Intermediate FindIntermediate(string id)
        {
            return id != null && Products.TryGetValue(id, out Intermediate product) ? product : null;
        }

        public static bool IsAcidTarget(SampleDef sample, string symbol)
        {
            return RequiredAcid(sample, symbol) != null;
        }

        public static CompoundDef RequiredAcid(SampleDef sample, string symbol)
        {
            // 按废料基质和目标金属配置游戏内试剂。金线路的硝酸是密闭装置的前处理耗材。
            string key = (sample?.Batch, symbol) switch
            {
                ("01", "Fe" or "Zn") or ("02", "Fe") or (SampleTable.VanillaScrapId, "Fe") => "hydrochloric_acid",
                ("02", "Al") or ("03", "Zn") or ("08", "Mn" or "Fe") => "sulfuric_acid",
                ("03", "Cu") or ("04", "Cu" or "Ag") or ("05", "Cu" or "Au")
                    or (SampleTable.VanillaScrapId, "Cu") => "nitric_acid",
                ("06", "Ca" or "Mg") => "acetic_acid",
                ("08", "Ti") => "phosphoric_acid",
                _ => null,
            };
            return ChemistryCatalog.FindCompound(key);
        }

        public static string IntermediateId(string symbol, bool filtered)
        {
            return ChemistryCatalog.ItemPrefix + (filtered ? "filtrate_" : "leachate_") + symbol.ToLowerInvariant();
        }

        private static Dictionary<string, Intermediate> BuildIntermediates()
        {
            var result = new Dictionary<string, Intermediate>(StringComparer.Ordinal);
            var symbols = SampleTable.All.SelectMany(sample => sample.Components
                .Where(component => IsAcidTarget(sample, component.Symbol)).Select(component => component.Symbol)).Distinct();
            foreach (string symbol in symbols)
            {
                var leachate = new Intermediate(symbol, false);
                var filtrate = new Intermediate(symbol, true);
                result.Add(leachate.ItemId, leachate);
                result.Add(filtrate.ItemId, filtrate);
            }
            return result;
        }

        public static bool IsAcid(string itemId)
        {
            return ChemistryCatalog.Compounds.Any(def => def.Category == "acid" && def.ItemId == itemId);
        }

        public static void StartLeaching(RefiningStation station, string reagentId, SampleDef sample, string symbol)
        {
            CompoundDef required = RequiredAcid(sample, symbol);
            if (required == null || required.ItemId != reagentId)
            {
                ReportMissingInputs(station);
                return;
            }
            int percent = Array.Find(sample.Components, component => component.Symbol == symbol).Percent;
            RunStage(station, sample.DetectedId, SamplesPerProduct(percent), IntermediateId(symbol, false),
                Loc.Get("refining_leaching", "正在浸出：废料反应，目标金属进入液相…", "Leaching: the target metal is moving into solution..."),
                reagentId: reagentId);
        }

        public static void StartFiltering(RefiningStation station, Intermediate product)
        {
            string funnelId = ChemistryCatalog.ToolItemId(FilterToolKey);
            if (product == null || product.Filtered)
            {
                ReportMissingInputs(station);
                return;
            }
            RunStage(station, product.ItemId, 1, IntermediateId(product.Symbol, true),
                Loc.Get("refining_filtering", "正在过滤：残渣留在滤芯，滤液流入收集瓶…", "Filtering: residue stays behind as filtrate collects..."),
                requiredToolId: funnelId);
        }

        public static void StartReduction(RefiningStation station, Intermediate product)
        {
            if (product == null || !product.Filtered)
            {
                ReportMissingInputs(station);
                return;
            }
            RunStage(station, product.ItemId, 1, ChemistryCatalog.ElementItemId(product.Symbol),
                Loc.Get("refining_reducing", "还原槽运行中：收集目标金属单质…", "Reduction cell active: collecting the elemental metal..."));
        }

        private static void ReportMissingInputs(RefiningStation station)
        {
            Notify(station, Loc.Pick("请检查设备里的物料。", "Check the loaded items."), false);
        }

        private static void RunStage(RefiningStation station, string inputId, int inputUnits, string outputId, string progress,
            string reagentId = null, string requiredToolId = null)
        {
            LabInventoryGrid grid = LabInventoryGrid.OpenGrid;
            if (grid == null)
            {
                return;
            }
            if (processingGrid == grid || AcidStation.Running)
            {
                Notify(station, Loc.Get("refining_busy", "上一批反应还在进行。", "A batch is still processing."), false);
                return;
            }

            processingGrid = grid;
            processingStation = station;
            startedAt = GameTicks.Now;
            Notify(station, progress, true);
            GameTicks.Schedule(StageTicks, () =>
            {
                if (processingGrid != grid || processingStation != station)
                {
                    return;
                }
                processingGrid = null;
                processingStation = null;
                if (grid == null || LabInventoryGrid.OpenGrid != grid || station == null)
                {
                    return;
                }
                if (ItemMouseDragHandler.current?.IsDraggingItem == true)
                {
                    Notify(station, Loc.Get("refining_input_moved", "操作已取消，原料保留在设备中。", "Run cancelled. No reagents or samples were consumed."), false);
                    return;
                }

                LabInventoryGrid.CaptureAfterDrop();
                if (!station.TryFinish(inputId, inputUnits, reagentId, requiredToolId, outputId))
                {
                    Notify(station, Loc.Get("refining_exchange_failed", "操作未完成，请检查装料和产物格空间。",
                        "Check the loaded items and space in the output grid."), false);
                    return;
                }

                LabPanel.RefreshFromInventory();
                Intermediate output = FindIntermediate(outputId);
                string name = output?.Name ?? SampleTable.ElementName(ChemistryCatalog.FindElementByItemId(outputId).Symbol);
                string message = Loc.Get("refining_stage_done", "产物格已生成 {0} ×1", "Output ready: {0} x1", name);
                Notify(station, message, true, outputId);
            });
        }

        private static void Notify(RefiningStation station, string message, bool success, string outputId = null)
        {
            station.Report(message, success, outputId);
            LabPanel.ShowRefiningStatus(message);
            OverlayHandler.current?.AppendLog(message, false);
            DayHudBridge.Push(message, success ? "info" : "warn", 4f);
        }

        public static int SamplesPerProduct(int percent)
        {
            return (UnitsPerProduct + percent - 1) / percent;
        }

        public static string Analyze(SampleDef sample)
        {
            if (!LabStorage.Loaded)
            {
                LabStorage.EnsureLoaded();
            }
            int count = LabStorage.CountOf(sample.ItemId);
            if (count == 0)
            {
                return Loc.Get("lab_samples_missing", "实验室储物格里的样品不足。", "There are not enough samples in lab storage.");
            }

            if (!LabStorage.TryExchange(sample.ItemId, count, sample.DetectedId, count))
            {
                return StorageError();
            }

            return Loc.Get("lab_samples_analyzed", "已检测 {0} ×{1}", "Analyzed {0} x{1}", sample.Name, count);
        }

        public static string Refine(SampleDef sample, string symbol)
        {
            if (IsAcidTarget(sample, symbol))
            {
                return AcidInstructions;
            }
            if (!LabStorage.Loaded)
            {
                LabStorage.EnsureLoaded();
            }
            int percent = Array.Find(sample.Components, c => c.Symbol == symbol).Percent;
            ElementDef element = ChemistryCatalog.FindElement(symbol);
            if (percent <= 0 || element == null)
            {
                return Loc.Get("lab_samples_no_target", "此样品没有可提炼的目标成分。", "This sample has no such recoverable component.");
            }

            int needed = SamplesPerProduct(percent);
            if (LabStorage.CountOf(sample.DetectedId) < needed)
            {
                return Loc.Get("lab_samples_missing", "实验室储物格里的样品不足。", "There are not enough samples in lab storage.");
            }

            if (!LabStorage.TryExchange(sample.DetectedId, needed, element.ItemId, 1))
            {
                return StorageError();
            }

            return Loc.Get("lab_samples_refined", "提炼完成，{0} ×1 已存入储物格。", "Refined {0} x1 and placed it in lab storage.",
                SampleTable.ElementName(symbol));
        }

        /// <summary>
        /// 拖蒸馏器提炼时用：优先扣实验室储物格里的已检测样品；不够再用店里的，产物也放回店里。
        /// 这样在当铺里检测完不用先搬进实验室就能提炼。
        /// </summary>
        public static string RefineAnywhere(SampleDef sample, string symbol)
        {
            if (IsAcidTarget(sample, symbol))
            {
                return AcidInstructions;
            }
            if (!LabStorage.Loaded)
            {
                LabStorage.EnsureLoaded();
            }

            int percent = Array.Find(sample.Components, c => c.Symbol == symbol).Percent;
            ElementDef element = ChemistryCatalog.FindElement(symbol);
            if (percent <= 0 || element == null)
            {
                return Loc.Get("lab_samples_no_target", "此样品没有可提炼的目标成分。", "This sample has no such recoverable component.");
            }

            int needed = SamplesPerProduct(percent);
            int inLab = LabStorage.CountOf(sample.DetectedId);
            if (inLab >= needed)
            {
                if (!LabStorage.TryExchange(sample.DetectedId, needed, element.ItemId, 1))
                {
                    return StorageError();
                }

                return Loc.Get("lab_samples_refined", "提炼完成，{0} ×1 已存入储物格。",
                    "Refined {0} x1 and placed it in lab storage.", SampleTable.ElementName(symbol));
            }

            // 店里的样品：先造产物并放好，再扣样品；放不下就不扣。
            int inStore = 0;
            try
            {
                inStore = StoreInventory.CountUnits().TryGetValue(sample.DetectedId, out int count) ? count : 0;
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("统计店里样品数量失败：" + e.Message);
                inStore = 0;
            }

            if (inStore < needed)
            {
                int have = inLab + inStore;
                return Loc.Get("lab_samples_missing_anywhere",
                    "样品不够：提炼 {0} 需要 {1} 份已检测样品（实验室 {2} + 店里 {3}）。",
                    "Not enough samples: refining {0} needs {1} analyzed samples (lab {2} + store {3}).",
                    SampleTable.ElementName(symbol), needed, inLab, inStore);
            }

            GameItem product = null;
            try
            {
                product = ModHelper.SpawnItem(element.ItemId);
                if (product == null)
                {
                    return Loc.Get("lab_samples_spawn_failed", "产物生成失败。", "Could not create the product.");
                }

                ModHelper.SetItemOwnerShip(product, true);
                if (!StoreInventory.TryPlace(product))
                {
                    product.Destroy();
                    return Loc.Get("lab_samples_store_full", "店里的仓库放不下产物，先清出空间再提炼。",
                        "No room in the store for the product; free some space first.");
                }

                if (!StoreInventory.TryConsume(sample.DetectedId, needed))
                {
                    product.Destroy();
                    return Loc.Get("lab_samples_consume_failed", "扣除样品失败，提炼取消。", "Could not consume the samples; refining cancelled.");
                }
            }
            catch (Exception e)
            {
                if (product != null)
                {
                    try
                    {
                        product.Destroy();
                    }
                    catch
                    {
                        // 忽略
                    }
                }

                ChemUi.Log?.Warning("提炼出错：" + e.Message);
                return Loc.Get("lab_samples_error", "提炼出错，详情看日志。", "Refining failed; see the log.");
            }

            ChemUi.InvalidateCounts();
            return Loc.Get("lab_samples_refined_store", "提炼完成，{0} ×1 已放进店里仓库。",
                "Refined {0} x1 and placed it in the store storage.", SampleTable.ElementName(symbol));
        }

        private static string StorageError()
        {
            return Loc.Get("lab_samples_storage_error", "未能存入产物。请检查储物格空间后重试。",
                "Could not store the result. Check the space in lab storage and retry.");
        }
    }
}
