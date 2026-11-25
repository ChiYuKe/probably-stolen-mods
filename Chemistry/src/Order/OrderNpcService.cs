using System;
using System.Collections.Generic;

namespace Chemistry
{
    /// <summary>
    /// 委托的接单 / 收货 NPC。
    ///
    /// 每天最多排一次：有可结算的委托就排收货人，否则排第一个可接委托的委托人。
    /// 对白结束时接单并打开委托记录，所以就算玩家当天没理会，也能从 F6 的「委托」页重新打开。
    /// </summary>
    internal static class OrderNpcService
    {
        private const string DayKey = "order_npc_day";

        public static void QueueForDay(StoreClientManager manager)
        {
            if (manager == null || PlayerStore.Instance == null)
            {
                return;
            }

            try
            {
                OrderService.Tick();

                OrderTemplate active = OrderService.ActiveTemplate;
                OrderTemplate offer = null;
                bool collect = false;

                if (active != null)
                {
                    // 只有真的能结算了才让委托人再跑一趟，别每天来催
                    collect = OrderService.CanSettle(out _);
                }
                else
                {
                    List<OrderTemplate> available = OrderService.Available();
                    if (available.Count > 0)
                    {
                        offer = available[0];
                    }
                }

                if (!collect && offer == null)
                {
                    return;
                }

                int today = Today();
                if (ModHelper.GetModDataInt(ChemistryCatalog.ModId, DayKey, -1) == today)
                {
                    return;
                }

                StoreClient client = collect ? CreateCollector(active) : CreateOffer(offer);
                if (client == null)
                {
                    return;
                }

                ModHelper.SetClientSource(client, ChemistryCatalog.ModId);
                manager.TryAddClient(client, true);
                ModHelper.SetModDataInt(ChemistryCatalog.ModId, DayKey, today);
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("委托 NPC 排队失败：" + e.Message);
            }
        }

        /// <summary>不等到明天，立刻把可接的委托 NPC 排进队列（界面/调试用）。</summary>
        public static void OfferHere()
        {
            try
            {
                StoreClientManager manager = PlayerStore.Instance != null ? PlayerStore.Instance.storeClientManager : null;
                if (manager == null)
                {
                    return;
                }

                List<OrderTemplate> available = OrderService.Available();
                if (available.Count == 0)
                {
                    DayHudBridge.Push(Loc.Pick("现在没有可接的委托。", "No orders are available right now."), "warn", 4f);
                    return;
                }

                StoreClient client = CreateOffer(available[0]);
                if (client == null)
                {
                    return;
                }

                ModHelper.SetClientSource(client, ChemistryCatalog.ModId);
                manager.TryAddClient(client, false);
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("立刻排委托 NPC 失败：" + e.Message);
            }
        }

        public static void Shutdown()
        {
            // 立绘复用 ChemistryNpcService 的资源，这里没有自己持有的对象。
        }

        // ---------------------------------------------------------------
        // 对白
        // ---------------------------------------------------------------

        private static StoreClient CreateOffer(OrderTemplate template)
        {
            if (template == null)
            {
                return null;
            }

            string name = template.Requester;
            var client = new StoreClient
            {
                identifier = "chemistry_order_offer_" + template.Id,
                displayName = name,
                spriteName = SpriteFor(template.NpcId),
                clientIntent = StoreClient.ClientIntent.DIALOGUE,
                dismissable = false,
                nonShootable = true,
            };

            string[] lines = OfferLines(template);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    client.mainDialogue.NextDialogue();
                }

                client.mainDialogue.SetText(name, lines[i]);
            }

            string id = template.Id;
            client.mainDialogue.SetEndAction(delegate
            {
                if (OrderService.Accept(id, out string message))
                {
                    OpenPanelSoon();
                }
                else
                {
                    DayHudBridge.Push(message, "warn", 5f);
                }

                if (PlayerStore.Instance != null)
                {
                    PlayerStore.Instance.DismissCurrentClient(false, true, true);
                }
            });

            client.CompleteClientCreation(false);
            return client;
        }

        private static StoreClient CreateCollector(OrderTemplate template)
        {
            if (template == null)
            {
                return null;
            }

            string name = template.Requester;
            var client = new StoreClient
            {
                identifier = "chemistry_order_collect_" + template.Id,
                displayName = name,
                spriteName = SpriteFor(template.NpcId),
                clientIntent = StoreClient.ClientIntent.DIALOGUE,
                dismissable = false,
                nonShootable = true,
            };

            string[] lines = CollectLines(template);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    client.mainDialogue.NextDialogue();
                }

                client.mainDialogue.SetText(name, lines[i]);
            }

            client.mainDialogue.SetEndAction(delegate
            {
                OpenPanelSoon();
                if (PlayerStore.Instance != null)
                {
                    PlayerStore.Instance.DismissCurrentClient(false, true, true);
                }
            });

            client.CompleteClientCreation(false);
            return client;
        }

        private static string[] OfferLines(OrderTemplate template)
        {
            OrderState state = OrderService.Active;
            if (state != null)
            {
                return new[]
                {
                    Loc.Get("order_npc_busy_1", "你手上那单还没完，先把它做完。", "Finish the order you already took first."),
                };
            }

            string requirement = template.RequirementLine();
            string reward = Loc.Get("order_npc_reward", "报酬 {0} 信用点。", "Pays {0} credits.", template.Reward);
            string hint = OrderSupply.Describe(template);
            bool needsKit = OrderSupply.Audit(template).Count > 0;

            var lines = new List<string>
            {
                Loc.Get("order_npc_offer_1", "有笔生意，接不接？", "Got a job for you. Interested?"),
                template.Brief,
                Loc.Get("order_npc_offer_3", "我要的是：{0}。{1}", "What I need: {0}. {1}", requirement, reward),
            };

            if (needsKit)
            {
                lines.Add(Loc.Get("order_npc_offer_kit",
                    "你手上还缺：{0}。当铺的规矩，工具可以先借你，用完还回来。",
                    "You're still missing: {0}. House rules — borrow the tools, bring them back.", hint));
            }

            lines.Add(Loc.Get("order_npc_offer_4", "单子我留在你的委托记录里，想好了就动手。",
                "The order is in your journal. Start whenever you're ready."));
            return lines.ToArray();
        }

        private static string[] CollectLines(OrderTemplate template)
        {
            if (OrderService.CanSettle(out _))
            {
                return new[]
                {
                    Loc.Get("order_npc_collect_1", "货备好了？我看看。", "Got the goods? Let me see."),
                    Loc.Get("order_npc_collect_2", "按单子上的价，一分不少。", "Full price, as written on the slip."),
                };
            }

            if (!OrderService.RequirementsMet(out string missing))
            {
                return new[]
                {
                    Loc.Get("order_npc_collect_3", "东西呢？", "Where's the goods?"),
                    missing,
                    Loc.Get("order_npc_collect_4", "我不催你，但别让我等太久。", "I won't rush you — but don't keep me waiting."),
                };
            }

            return new[]
            {
                Loc.Get("order_npc_collect_5", "数量不对。单子上写的是：{0}。", "Wrong amount. The slip says: {0}.",
                    template.RequirementLine()),
                Loc.Get("order_npc_collect_6", "凑齐了再来找我。", "Come back when it's all there."),
            };
        }

        /// <summary>对白刚结束的同一帧里开窗口容易和对话收尾打架，晚一点再弹。</summary>
        private static void OpenPanelSoon()
        {
            try
            {
                ModHelper.Delay(ChemistryCatalog.ModId, 0.2f, OrderPanel.Open);
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("打开委托记录失败：" + e.Message);
            }
        }

        private static string SpriteFor(string npcId)
        {
            string key = ChemistryNpcService.EnsureNpcSprite(npcId);
            if (!string.IsNullOrEmpty(key))
            {
                return key;
            }

            try
            {
                return SpriteDict.GetRandomEngSpriteName();
            }
            catch
            {
                return null;
            }
        }

        private static int Today()
        {
            try
            {
                return Math.Max(0, StoreStation.GetDayCounter());
            }
            catch
            {
                return 0;
            }
        }
    }
}
