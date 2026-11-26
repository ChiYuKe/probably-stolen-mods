using System;
using System.Collections.Generic;

namespace Chemistry
{
    /// <summary>
    /// 原料采购：把垃圾场里曾经直接掉落的单质换成「花钱买」。
    ///
    /// 垃圾场不再直掉单质之后，稀有元素（铀、铂、稀土这类）如果只剩翻垃圾一条路，
    /// 后期配方就会卡死。这里给每种元素一条明码标价的采购渠道：
    /// 价钱按元素价值加价，常见轻元素便宜、贵金属和放射性元素很贵，
    /// 所以翻垃圾仍然是更划算的路子，采购只是兜底。
    ///
    /// 解锁条件：结清首单（老锈的试单）之后才开，避免新手直接刷钱跳过玩法。
    /// </summary>
    internal static class OrderPurchase
    {
        /// <summary>加价倍数：采购价按一个库存质量单位的售价计算。</summary>
        private const int Markup = 4;

        /// <summary>单次采购的最少花费，别让常见元素白送。</summary>
        private const int MinPrice = 24;

        /// <summary>一次最多买多少单位，防止一口气刷爆库存。</summary>
        public const int MaxBatch = 20;

        internal sealed class Offer
        {
            public string ItemId;
            public string Symbol;
            public string Name;
            public int UnitPrice;
            public int Count;
            public int Price;
        }

        /// <summary>采购是否已解锁。</summary>
        public static bool IsUnlocked
        {
            get { return OrderService.IsCompleted("rust_trial_iron"); }
        }

        /// <summary>可采购的元素清单（按原子序数）。未解锁时返回空表。</summary>
        public static List<Offer> Offers()
        {
            var result = new List<Offer>();
            if (!IsUnlocked)
            {
                return result;
            }

            try
            {
                ElementDef[] elements = ChemistryCatalog.Elements;
                if (elements == null)
                {
                    return result;
                }

                for (int i = 0; i < elements.Length; i++)
                {
                    ElementDef element = elements[i];
                    if (element == null || string.IsNullOrEmpty(element.ItemId))
                    {
                        continue;
                    }

                    result.Add(new Offer
                    {
                        ItemId = element.ItemId,
                        Symbol = element.Symbol,
                        Name = LabUi.Resolve(element.ItemId).Name,
                        UnitPrice = UnitPrice(element),
                        Count = 1,
                        Price = UnitPrice(element),
                    });
                }
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("枚举采购清单失败：" + e.Message);
            }

            return result;
        }

        public static int UnitPrice(ElementDef element)
        {
            if (element == null)
            {
                return MinPrice;
            }

            return Math.Max(MinPrice, ItemPricing.ElementUnitValue(element) * Markup);
        }

        public static int PriceOf(string itemId, int count)
        {
            ElementDef element = null;
            try
            {
                element = ChemistryCatalog.FindElementByItemId(itemId);
            }
            catch
            {
                element = null;
            }

            if (element == null)
            {
                return 0;
            }

            return UnitPrice(element) * Math.Max(1, count);
        }

        /// <summary>买东西：先查钱够不够，再扣钱、再发货；发货失败把钱退回去。</summary>
        public static bool Buy(string itemId, int count, out string message)
        {
            count = Math.Max(1, Math.Min(MaxBatch, count));
            message = null;

            if (!IsUnlocked)
            {
                message = Loc.Pick("结清老锈的试单之后才有人愿意给你供货。",
                    "Nobody will supply you until Old Rust's trial order is settled.");
                return false;
            }

            ElementDef element;
            try
            {
                element = ChemistryCatalog.FindElementByItemId(itemId);
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("查询采购元素失败：" + e.Message);
                element = null;
            }

            if (element == null)
            {
                message = Loc.Pick("这件东西不在供货清单上。", "That item is not on the supply list.");
                return false;
            }

            int price = UnitPrice(element) * count;
            int cash = 0;
            try
            {
                cash = PlayerStore.Instance != null ? PlayerStore.Instance.GetCash() : 0;
            }
            catch
            {
                cash = 0;
            }

            if (cash < price)
            {
                message = Loc.Get("order_purchase_no_cash", "信用点不够：要 {0}，现有 {1}。",
                    "Not enough credits: {0} needed, {1} on hand.", price, cash);
                return false;
            }

            try
            {
                // 关掉收银音效：那一步会碰 NegociationUIManager，在非交易场景里可能拿不到实例，
                // 一旦抛异常就会出现「钱已经扣了却报失败」。
                PlayerStore.Instance.ModCash(-price, false);
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("扣采购款失败：" + e.Message);
                message = Loc.Pick("付款失败，没扣钱也没发货。", "Payment failed; nothing was charged or shipped.");
                return false;
            }

            int delivered = 0;
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

                    delivered++;
                }
                catch (Exception e)
                {
                    ChemistryMod.Log?.Warning("采购发货失败：" + e.Message);
                    break;
                }
            }

            if (delivered < count)
            {
                int refund = UnitPrice(element) * (count - delivered);
                try
                {
                    if (refund > 0)
                    {
                        PlayerStore.Instance.ModCash(refund, false);
                    }
                }
                catch
                {
                    // 退不回去也只能记日志
                }

                message = Loc.Get("order_purchase_partial", "仓库放不下，只到货 {0}/{1} 件，多付的 {2} 信用点已退回。",
                    "Only {0}/{1} fit in storage; {2} credits were refunded.", delivered, count, refund);
                OrderService.InvalidateCounts();
                return delivered > 0;
            }

            message = Loc.Get("order_purchase_ok", "采购到货：{0} ×{1}，花掉 {2} 信用点。",
                "Purchased {0} ×{1} for {2} credits.", LabUi.Resolve(itemId).Name, count, price);
            OrderService.InvalidateCounts();
            return true;
        }
    }
}
