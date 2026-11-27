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
                : Loc.Get("refining_leachate_hint", "把浸出液放入提炼反应台投料格，再将过滤漏斗放入工具槽。", "Place the leachate in the refining station input and the filter funnel in its tool slot.");

            public Intermediate(string symbol, bool filtered)
            {
                Symbol = symbol;
                Filtered = filtered;
                ItemId = IntermediateId(symbol, filtered);
            }
        }

        public static string AcidInstructions => Loc.Get("refining_acid_hint",
            "在提炼反应台把酸放入试剂槽，把已检测废料放入投料格，再按配方启动酸浸。",
            "Place acid in the reagent socket and analyzed scrap in the refining station input, then run the leaching recipe.");

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

                // 还原槽出单质：这是委托里「亲手回收」那一步
                ElementDef produced = ChemistryCatalog.FindElementByItemId(outputId);
                if (produced != null)
                {
                    try
                    {
                        OrderService.OnElementRecovered(produced.Symbol);
                    }
                    catch (Exception e)
                    {
                        ChemUi.Log?.Warning("更新委托回收进度失败：" + e.Message);
                    }
                }
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

        /// <summary>提炼出单质后推进委托进度（「亲手回收」那一步）。</summary>
        internal static void NotifyRecovered(ElementDef element)
        {
            if (element == null)
            {
                return;
            }

            try
            {
                OrderService.OnElementRecovered(element.Symbol);
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("更新委托回收进度失败：" + e.Message);
            }
        }

    }
}
