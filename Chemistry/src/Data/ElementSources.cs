using System;
using System.Collections.Generic;
using System.Text;

namespace Chemistry
{
    /// <summary>
    /// 原料渠道盘点（M1）。
    ///
    /// 垃圾场不再直掉单质之后，每种元素都得有一条能走通的路。这里按数据表把渠道枚举出来：
    ///   · sample   —— 未知样品里的成分（酸浸回收或蒸馏分离）
    ///   · reaction —— 水相反应等流程的产物（目前只有氢）
    ///   · vanilla  —— 原版物品自带（废金属里的铁和铜）
    ///   · purchase —— 委托记录里的原料采购
    /// 只做诊断和界面提示，不阻塞游戏；F9 调试面板会逐元素打印一遍并标出缺口。
    /// </summary>
    internal static class ElementSources
    {
        internal sealed class Channel
        {
            public string Kind;
            public string Ref;
            public string DetailZh;
            public string DetailEn;

            public string Detail
            {
                get { return Loc.Pick(DetailZh, DetailEn); }
            }
        }

        public const string KindSample = "sample";
        public const string KindReaction = "reaction";
        public const string KindVanilla = "vanilla";
        public const string KindPurchase = "purchase";

        /// <summary>某元素的所有可获得渠道。</summary>
        public static List<Channel> For(string symbol)
        {
            var result = new List<Channel>();
            if (string.IsNullOrEmpty(symbol))
            {
                return result;
            }

            try
            {
                CollectSamples(symbol, result);
                CollectReactions(symbol, result);
                CollectVanilla(symbol, result);
                CollectPurchase(symbol, result);
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("盘点元素渠道失败（" + symbol + "）：" + e.Message);
            }

            return result;
        }

        public static bool HasAnyChannel(string symbol)
        {
            return For(symbol).Count > 0;
        }

        /// <summary>一条渠道都没有的元素。正常应该是空表。</summary>
        public static List<string> ElementsWithoutChannel()
        {
            var missing = new List<string>();
            try
            {
                ElementDef[] elements = ChemistryCatalog.Elements;
                if (elements == null)
                {
                    return missing;
                }

                for (int i = 0; i < elements.Length; i++)
                {
                    ElementDef element = elements[i];
                    if (element == null || string.IsNullOrEmpty(element.Symbol))
                    {
                        continue;
                    }

                    if (!HasAnyChannel(element.Symbol))
                    {
                        missing.Add(element.Symbol);
                    }
                }
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("检查元素渠道缺口失败：" + e.Message);
            }

            return missing;
        }

        /// <summary>一句话说明某元素从哪来。</summary>
        public static string Describe(string symbol)
        {
            List<Channel> channels = For(symbol);
            if (channels.Count == 0)
            {
                return Loc.Pick("暂时没有稳定的获取渠道。", "No reliable source yet.");
            }

            var builder = new StringBuilder();
            for (int i = 0; i < channels.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(Loc.Pick("；", "; "));
                }

                builder.Append(channels[i].Detail);
            }

            return builder.ToString();
        }

        /// <summary>F9 诊断：逐元素打印渠道，最后汇总缺口。</summary>
        public static void LogReport()
        {
            ModLog log = ChemistryMod.Log;
            if (log == null)
            {
                return;
            }

            try
            {
                ElementDef[] elements = ChemistryCatalog.Elements;
                if (elements == null)
                {
                    log.Warning("元素数据没加载，无法盘点渠道");
                    return;
                }

                int covered = 0;
                for (int i = 0; i < elements.Length; i++)
                {
                    ElementDef element = elements[i];
                    if (element == null || string.IsNullOrEmpty(element.Symbol))
                    {
                        continue;
                    }

                    List<Channel> channels = For(element.Symbol);
                    if (channels.Count > 0)
                    {
                        covered++;
                    }

                    log.Log("元素渠道 " + element.Symbol + "（" + element.Number + "）："
                        + (channels.Count == 0 ? "无" : Describe(element.Symbol)));
                }

                List<string> gaps = ElementsWithoutChannel();
                log.Log("元素渠道盘点：覆盖 " + covered + "/" + elements.Length
                    + "，缺口 " + gaps.Count + (gaps.Count > 0 ? "（" + string.Join(",", gaps.ToArray()) + "）" : ""));

                ChemistryDebugWindow.Report(gaps.Count == 0
                    ? Loc.Pick("全部元素都有可获得渠道。", "Every element has at least one source.")
                    : Loc.Get("element_source_gaps", "有 {0} 种元素没有渠道：{1}",
                        "{0} elements have no source: {1}", gaps.Count, string.Join(",", gaps.ToArray())),
                    gaps.Count == 0);
            }
            catch (Exception e)
            {
                log.Warning("元素渠道盘点失败：" + e.Message);
            }
        }

        // ---------------------------------------------------------------
        // 各类渠道
        // ---------------------------------------------------------------

        private static void CollectSamples(string symbol, List<Channel> result)
        {
            SampleDef[] samples = SampleTable.All;
            if (samples == null)
            {
                return;
            }

            for (int i = 0; i < samples.Length; i++)
            {
                SampleDef sample = samples[i];
                if (sample?.Components == null)
                {
                    continue;
                }

                for (int c = 0; c < sample.Components.Length; c++)
                {
                    if (!string.Equals(sample.Components[c].Symbol, symbol, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int percent = sample.Components[c].Percent;
                    bool acid = LabRefining.IsAcidTarget(sample, symbol);
                    CompoundDef reagent = acid ? LabRefining.RequiredAcid(sample, symbol) : null;
                    string routeZh = acid
                        ? "提炼反应台酸浸（" + (reagent != null ? reagent.NameZh : "酸") + "）"
                        : "蒸馏器分离";
                    string routeEn = acid
                        ? "refining station leaching (" + (reagent != null ? reagent.NameEn : "acid") + ")"
                        : "distiller separation";

                    result.Add(new Channel
                    {
                        Kind = KindSample,
                        Ref = sample.ItemId,
                        DetailZh = "样品批次 " + sample.Batch + "（" + percent + "%）走" + routeZh,
                        DetailEn = "sample batch " + sample.Batch + " (" + percent + "%) via " + routeEn,
                    });
                    break;
                }
            }
        }

        private static void CollectReactions(string symbol, List<Channel> result)
        {
            string itemId = ChemistryCatalog.ElementItemId(symbol);
            AqueousReactions.Equation[] equations = AqueousReactions.Equations;
            if (equations == null)
            {
                return;
            }

            for (int i = 0; i < equations.Length; i++)
            {
                AqueousReactions.Equation equation = equations[i];
                if (equation?.Products == null)
                {
                    continue;
                }

                for (int p = 0; p < equation.Products.Length; p++)
                {
                    if (!string.Equals(AqueousReactions.ItemId(equation.Products[p].Key), itemId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    result.Add(new Channel
                    {
                        Kind = KindReaction,
                        Ref = equation.Display,
                        DetailZh = "水相反应产物：" + equation.Display,
                        DetailEn = "aqueous reaction product: " + equation.Display,
                    });
                    break;
                }
            }
        }

        private static void CollectVanilla(string symbol, List<Channel> result)
        {
            // 原版废金属（scrap_metal）本身就在垃圾场掉落，检测后能提炼出铁和铜。
            if (string.Equals(symbol, "Fe", StringComparison.Ordinal) || string.Equals(symbol, "Cu", StringComparison.Ordinal))
            {
                result.Add(new Channel
                {
                    Kind = KindVanilla,
                    Ref = SampleTable.VanillaScrapId,
                    DetailZh = "原版废金属检测后提炼",
                    DetailEn = "analyzed vanilla scrap metal",
                });
            }
        }

        private static void CollectPurchase(string symbol, List<Channel> result)
        {
            ElementDef element = ChemistryCatalog.FindElement(symbol);
            if (element == null)
            {
                return;
            }

            int price = OrderPurchase.UnitPrice(element);
            result.Add(new Channel
            {
                Kind = KindPurchase,
                Ref = element.ItemId,
                DetailZh = "委托记录里的原料采购（" + price + " 信用点/单位）",
                DetailEn = "supply purchase in the order journal (" + price + " credits per unit)",
            });
        }
    }
}
