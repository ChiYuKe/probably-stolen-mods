using System;
using System.Collections.Generic;
using System.Text;

namespace Chemistry
{
    /// <summary>
    /// 首单的「避免开局卡死」机制。
    ///
    /// 接单时按路线核查光谱仪、制酸器具、空瓶、过滤漏斗、废料数量，以及制酸配方的全部上游原料。
    /// 缺什么就列出来；玩家可以领一次借用工位（工具各 1 件、消耗品按一批用量、样品给批次 01）。
    /// 每个订单编号只能领一次，避免重复接单套取物资；关闭开局补给的新档同样可用。
    /// </summary>
    internal static class OrderSupply
    {
        /// <summary>借用清单里的一项。</summary>
        internal sealed class Missing
        {
            public string ItemId;
            public int Count;
            public string ReasonZh;
            public string ReasonEn;

            public string Reason
            {
                get { return Loc.Pick(ReasonZh, ReasonEn); }
            }

            public string Name
            {
                get
                {
                    try
                    {
                        return LabUi.Resolve(ItemId).Name;
                    }
                    catch
                    {
                        return ItemId;
                    }
                }
            }
        }

        private const int SmallBottleUnits = 4;

        /// <summary>核查这张委托还缺什么。只列「店里和实验室都没有」的项。</summary>
        public static List<Missing> Audit(OrderTemplate template)
        {
            var result = new List<Missing>();
            if (template == null)
            {
                return result;
            }

            try
            {
                switch (template.Kind)
                {
                    case OrderKind.Recover:
                        AuditRecover(template, result);
                        break;
                    case OrderKind.SupplyReagent:
                        AuditReagent(template, result);
                        break;
                    case OrderKind.SupplySolid:
                        AuditSolid(template, result);
                        break;
                    case OrderKind.Contract:
                        AuditContract(template, result);
                        break;
                }
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("核查委托补给失败：" + e.Message);
            }

            return result;
        }

        private static void AuditRecover(OrderTemplate template, List<Missing> result)
        {
            // 检测和分离的基础工具
            RequireTool(result, SampleInteraction.DetectorToolKey,
                "没有光谱仪就看不到成分报告。", "Without a spectrometer you cannot read a composition report.");
            RequireTool(result, SampleInteraction.RefinerToolKey,
                "没有蒸馏器就分不出样品里的元素。", "Without a distiller you cannot separate the sample's elements.");
            RequireTool(result, LabRefining.FilterToolKey,
                "没有过滤漏斗就过滤不了浸出液。", "Without a filter funnel you cannot filter the leachate.");

            // 制酸路线：试剂本身的器皿 + 空瓶 + 上游元素
            CompoundDef reagent = FindReagent(template);
            if (reagent != null)
            {
                AuditReagentTools(reagent, result);
                RequireEmptyBottle(result, reagent, 1);
                AuditReagentInputs(reagent, result);
            }

            // 废料：一份就够（首单两种路线都是 1 份出一份成品）
            RequireSample(result, template);
        }

        private static void AuditReagent(OrderTemplate template, List<Missing> result)
        {
            CompoundDef reagent = FindReagent(template);
            if (reagent == null)
            {
                return;
            }

            AuditReagentTools(reagent, result);
            RequireEmptyBottle(result, reagent, RequiredBottles(template));
            AuditReagentInputs(reagent, result);
        }

        private static void AuditSolid(OrderTemplate template, List<Missing> result)
        {
            RequireTool(result, "drying_oven",
                "固体成品要在干燥箱里出。", "The solid product is finished in a drying oven.");
            RequireTool(result, "hotplate_stirrer",
                "盐液结晶线要用加热搅拌台。", "The brine route needs a hotplate stirrer.");
        }

        private static void AuditContract(OrderTemplate template, List<Missing> result)
        {
            // 长期合同要的是产能，不是开局工具。这里只提醒上游原料和空瓶。
            CompoundDef reagent = FindReagent(template);
            if (reagent != null)
            {
                RequireEmptyBottle(result, reagent, RequiredBottles(template));
                AuditReagentInputs(reagent, result);
            }
        }

        private static void AuditReagentTools(CompoundDef reagent, List<Missing> result)
        {
            if (reagent?.Tools == null)
            {
                return;
            }

            for (int i = 0; i < reagent.Tools.Length; i++)
            {
                string key = reagent.Tools[i];
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                RequireTool(result, key,
                    "制备" + Loc.Pick(reagent.NameZh, reagent.NameEn) + "要用到它。",
                    "Preparing " + Loc.Pick(reagent.NameZh, reagent.NameEn) + " needs it.");
            }
        }

        private static void AuditReagentInputs(CompoundDef reagent, List<Missing> result)
        {
            if (reagent?.Inputs == null)
            {
                return;
            }

            foreach (KeyValuePair<string, int> pair in reagent.Inputs)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value <= 0)
                {
                    continue;
                }

                string itemId = ChemistryCatalog.ElementItemId(pair.Key);
                if (Has(itemId))
                {
                    continue;
                }

                result.Add(new Missing
                {
                    ItemId = itemId,
                    Count = Math.Max(1, pair.Value),
                    ReasonZh = "制" + Loc.Pick(reagent.NameZh, reagent.NameEn) + "缺上游原料。",
                    ReasonEn = "Missing an upstream material for " + Loc.Pick(reagent.NameZh, reagent.NameEn) + ".",
                });
            }
        }

        private static void RequireTool(List<Missing> result, string toolKey, string reasonZh, string reasonEn)
        {
            if (string.IsNullOrEmpty(toolKey))
            {
                return;
            }

            string itemId = ChemistryCatalog.ToolItemId(toolKey);
            if (Has(itemId))
            {
                return;
            }

            result.Add(new Missing
            {
                ItemId = itemId,
                Count = 1,
                ReasonZh = reasonZh,
                ReasonEn = reasonEn,
            });
        }

        private static void RequireEmptyBottle(List<Missing> result, CompoundDef reagent, int bottles)
        {
            if (reagent == null || bottles <= 0)
            {
                return;
            }

            string emptyId;
            try
            {
                emptyId = ReagentBottle.EmptyFor(reagent.ItemId, 0);
            }
            catch
            {
                emptyId = ReagentBottle.EmptyId;
            }

            if (string.IsNullOrEmpty(emptyId) || Has(emptyId))
            {
                return;
            }

            result.Add(new Missing
            {
                ItemId = emptyId,
                Count = bottles,
                ReasonZh = "成品要装瓶，缺空瓶。",
                ReasonEn = "The product needs a bottle; you have no empties.",
            });
        }

        private static void RequireSample(List<Missing> result, OrderTemplate template)
        {
            // 普通废金属（原版）或批次 01 的未知金属碎片，任有其一就不算缺。
            if (Has(SampleTable.VanillaScrapId) || Has(ChemistryCatalog.ItemPrefix + "sample_01"))
            {
                return;
            }

            SampleDef fallback = SampleTable.Find(ChemistryCatalog.ItemPrefix + "sample_01");
            if (fallback == null)
            {
                return;
            }

            result.Add(new Missing
            {
                ItemId = fallback.ItemId,
                Count = 1,
                ReasonZh = "手上一件含铁废料都没有，先给一批保底样品。",
                ReasonEn = "No iron-bearing scrap on hand; here is one guaranteed batch.",
            });
        }

        private static int RequiredBottles(OrderTemplate template)
        {
            int units = template != null ? template.TotalUnits() : 0;
            return Math.Max(1, (units + SmallBottleUnits - 1) / SmallBottleUnits);
        }

        private static CompoundDef FindReagent(OrderTemplate template)
        {
            if (template == null || string.IsNullOrEmpty(template.ReagentKey))
            {
                return null;
            }

            try
            {
                return ChemistryCatalog.FindCompound(template.ReagentKey);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>店里或实验室储物格里有没有这件东西。</summary>
        private static bool Has(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            try
            {
                if (ChemUi.CountOf(itemId) > 0)
                {
                    return true;
                }
            }
            catch
            {
                // 库存扫描失败时按「没有」处理，宁可多给一点也别卡住玩家
            }

            try
            {
                return LabStorage.CountOf(itemId) > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>这一单还能不能领借用工位：每个订单编号只能领一次。</summary>
        public static bool CanBorrow(OrderTemplate template)
        {
            OrderState state = OrderService.Active;
            if (state == null)
            {
                return false;
            }

            if (template == null)
            {
                template = OrderService.ActiveTemplate;
            }

            if (template == null)
            {
                return false;
            }

            return OrderService.BorrowedOrderNumber != state.Number;
        }

        /// <summary>领取借用工位：缺的工具各 1 件、缺的消耗品按一批用量、缺的样品给批次 01。</summary>
        public static bool Borrow(OrderTemplate template, out string message)
        {
            if (template == null)
            {
                template = OrderService.ActiveTemplate;
            }

            OrderState state = OrderService.Active;
            if (state == null || template == null)
            {
                message = Loc.Pick("没有进行中的委托。", "There is no order in progress.");
                return false;
            }

            if (!CanBorrow(template))
            {
                message = Loc.Get("order_borrow_once", "第 {0} 号委托的借用工位已经领过了。",
                    "The loaner kit for order #{0} has already been claimed.", state.Number);
                return false;
            }

            List<Missing> missing = Audit(template);
            if (missing.Count == 0)
            {
                message = Loc.Pick("工具和原料都齐了，不用借。", "You already have everything; no loaner kit needed.");
                return false;
            }

            int given = 0;
            var failed = new List<string>();
            for (int i = 0; i < missing.Count; i++)
            {
                Missing entry = missing[i];
                if (entry == null || string.IsNullOrEmpty(entry.ItemId) || entry.Count <= 0)
                {
                    continue;
                }

                if (Give(entry.ItemId, entry.Count))
                {
                    given += entry.Count;
                }
                else
                {
                    failed.Add(entry.Name);
                }
            }

            if (given <= 0)
            {
                message = Loc.Pick("借用工位没领成（仓库放不下？先清点空间）。",
                    "Could not hand out the loaner kit (no room in the store?).");
                return false;
            }

            OrderService.MarkBorrowed(state.Number);
            OrderService.InvalidateCounts();

            if (failed.Count > 0)
            {
                message = Loc.Get("order_borrow_partial", "借到 {0} 件，还有 {1} 件没放进去（仓库满了）。",
                    "Received {0} items; {1} could not be placed (store is full).", given, string.Join("、", failed.ToArray()));
            }
            else
            {
                message = Loc.Get("order_borrow_ok", "借用工位已就位：{0} 件。用完记得还回去——这是当铺的规矩。",
                    "Loaner kit handed out: {0} items. Return it when you're done — house rules.", given);
            }

            DayHudBridge.Push(message, "info", 7f);
            ChemistryMod.Log?.Log("委托 #" + state.Number + " 领取借用工位 " + given + " 件");
            return true;
        }

        private static bool Give(string itemId, int count)
        {
            bool any = false;
            for (int i = 0; i < count; i++)
            {
                try
                {
                    GameItem item = ModHelper.SpawnItem(itemId);
                    if (item == null)
                    {
                        break;
                    }

                    ModHelper.SetItemOwnerShip(item, true);
                    if (!StoreInventory.TryPlace(item))
                    {
                        break;
                    }

                    any = true;
                }
                catch (Exception e)
                {
                    ChemistryMod.Log?.Warning("发放借用物品失败（" + itemId + "）：" + e.Message);
                    break;
                }
            }

            return any;
        }

        /// <summary>一句话清单，给界面显示。</summary>
        public static string Describe(OrderTemplate template)
        {
            List<Missing> missing = Audit(template);
            if (missing.Count == 0)
            {
                return Loc.Pick("工具和原料都齐了。", "You have all the tools and materials.");
            }

            var builder = new StringBuilder();
            for (int i = 0; i < missing.Count; i++)
            {
                Missing entry = missing[i];
                if (entry == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(Loc.Pick("、", ", "));
                }

                builder.Append(entry.Name);
                if (entry.Count > 1)
                {
                    builder.Append(" ×").Append(entry.Count);
                }
            }

            return builder.ToString();
        }
    }
}
