using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chemistry
{
    /// <summary>
    /// 委托记录：进行中 / 可接取 / 原料采购 / 已结算 四块。
    ///
    /// 既可以单独开一个窗口（`Open`），也能嵌进标签页（`BuildPage`，F6 实验台和实验室面板各有一页）。
    /// 数据全部来自 OrderService / OrderSupply / OrderPurchase，界面只负责显示和转发按钮。
    /// 所有游戏侧调用都包 try/catch：数据为空、贴图缺失、窗口被关掉都只降级。
    /// </summary>
    internal static class OrderPanel
    {
        private const string WindowId = "chemistry.orders";
        private const float WindowWidth = 900f;
        private const float WindowHeight = 660f;
        private const float ContentWidth = 800f;
        private const float RowWidth = 780f;
        private const float ScrollMinHeight = 380f;
        private const float IconSize = 26f;

        /// <summary>
        /// 一个「承载面」：独立窗口或某个标签页。F6 实验台和地下实验室面板可能同时开着，
        /// 所以每个面各记一份宿主和滚动容器，刷新时逐个重建，谁也不会把谁顶掉。
        /// </summary>
        private sealed class Surface
        {
            public CustomUIWindow Host;
            public RectTransform Content;
            public string SummaryId;
        }

        private static readonly List<Surface> surfaces = new List<Surface>();
        private static string lastMessage = "";
        private static int purchasePage;

        // ---------------------------------------------------------------
        // 独立窗口
        // ---------------------------------------------------------------

        public static void Open()
        {
            if (CustomUIManager.Instance == null)
            {
                ChemistryMod.Log?.Warning("当前场景没有 CustomUIManager，开不了委托记录");
                return;
            }

            Surface existing = FindSurface(WindowId);
            if (existing != null && existing.Host != null && existing.Host.IsAlive)
            {
                try
                {
                    existing.Host.Show();
                    existing.Host.BringToFront();
                }
                catch
                {
                    // 显示失败就重建
                }

                Refresh(true);
                return;
            }

            try
            {
                CustomUIBuilder builder = CustomUIManager.Window(WindowId,
                    Loc.Get("order_window_title", "委托记录", "Order Journal"));
                if (builder == null)
                {
                    return;
                }

                CustomUIWindow host = builder.Handle;
                builder.SetSize(WindowWidth, WindowHeight);
                builder.SetDraggable(true);
                builder.SetCloseOnEscape(true);

                LabUi.Label(builder, " ", "order_summary", 12.5f, ContentWidth, 22f, false, ChemUi.Accent, true,
                    TextAlignmentOptions.Left);

                builder.BeginScrollFlexible(ScrollMinHeight);
                RectTransform content = builder.CurrentContainer;
                builder.End();

                builder.Show();

                RemoveSurface(WindowId);
                surfaces.Add(new Surface
                {
                    Host = host,
                    Content = content,
                    SummaryId = "order_summary",
                });

                Refresh(true);
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("打开委托记录失败：" + e);
                Close();
            }
        }

        public static void Close()
        {
            Surface surface = FindSurface(WindowId);
            if (surface?.Host != null && surface.Host.IsAlive)
            {
                try
                {
                    surface.Host.Close();
                }
                catch
                {
                    // 忽略
                }
            }

            RemoveSurface(WindowId);
        }

        public static void Shutdown()
        {
            Close();
            surfaces.Clear();
            lastMessage = "";
            purchasePage = 0;
        }

        /// <summary>宿主窗口关掉时调用：只摘掉这一个面，别把别的窗口的页一起清掉。</summary>
        public static void Detach(CustomUIWindow host)
        {
            if (host == null)
            {
                surfaces.Clear();
                return;
            }

            for (int i = surfaces.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(surfaces[i].Host, host))
                {
                    surfaces.RemoveAt(i);
                }
            }
        }

        // ---------------------------------------------------------------
        // 嵌进标签页
        // ---------------------------------------------------------------

        /// <summary>在宿主标签页里建出「委托」页。宿主窗口由调用方持有。</summary>
        public static void BuildPage(CustomUIBuilder builder, CustomUIWindow host)
        {
            if (builder == null)
            {
                return;
            }

            try
            {
                LabUi.Label(builder, " ", "order_page_summary", 12.5f, ContentWidth, 22f, false, ChemUi.Accent, true,
                    TextAlignmentOptions.Left);

                builder.BeginScrollFlexible(ScrollMinHeight);
                RectTransform content = builder.CurrentContainer;
                builder.End();

                if (host != null)
                {
                    Detach(host);
                    surfaces.Add(new Surface
                    {
                        Host = host,
                        Content = content,
                        SummaryId = "order_page_summary",
                    });
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("创建委托页失败：" + e);
            }
        }

        /// <summary>刷新所有还活着的承载面。</summary>
        public static void Refresh(bool resetScroll)
        {
            CustomUIManager manager = CustomUIManager.Instance;
            if (manager == null)
            {
                return;
            }

            try
            {
                OrderService.Tick();
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("检查委托期限失败：" + e.Message);
            }

            for (int i = surfaces.Count - 1; i >= 0; i--)
            {
                Surface surface = surfaces[i];
                if (surface?.Host == null || !surface.Host.IsAlive || surface.Content == null)
                {
                    surfaces.RemoveAt(i);
                    continue;
                }

                try
                {
                    RefreshSurface(manager, surface, resetScroll);
                }
                catch (Exception e)
                {
                    ChemUi.Log?.Warning("刷新委托记录失败：" + e);
                }
            }
        }

        private static void RefreshSurface(CustomUIManager manager, Surface surface, bool resetScroll)
        {
            SetLabel(surface.Host, surface.SummaryId, SafeProgressLine(), ChemUi.Accent);

            CustomUIBuilder.ClearContainer(surface.Content);
            var builder = new CustomUIBuilder(manager, surface.Host, surface.Content);

            BuildStatusLine(builder);
            BuildActiveSection(builder);
            BuildAvailableSection(builder);
            BuildPurchaseSection(builder);
            BuildHistorySection(builder);

            CustomUIBuilder.RebuildLayout(surface.Content);
            if (resetScroll)
            {
                ChemUi.ResetScroll(surface.Content);
            }
        }

        private static string SafeProgressLine()
        {
            try
            {
                return OrderService.ProgressLine();
            }
            catch
            {
                return Loc.Pick("委托数据还没准备好。", "Order data is not ready.");
            }
        }

        private static Surface FindSurface(string windowId)
        {
            for (int i = 0; i < surfaces.Count; i++)
            {
                Surface surface = surfaces[i];
                if (surface?.Host != null && string.Equals(surface.Host.id, windowId, StringComparison.Ordinal))
                {
                    return surface;
                }
            }

            return null;
        }

        private static void RemoveSurface(string windowId)
        {
            for (int i = surfaces.Count - 1; i >= 0; i--)
            {
                Surface surface = surfaces[i];
                if (surface?.Host != null && string.Equals(surface.Host.id, windowId, StringComparison.Ordinal))
                {
                    surfaces.RemoveAt(i);
                }
            }
        }

        // ---------------------------------------------------------------
        // 状态行
        // ---------------------------------------------------------------

        private static void BuildStatusLine(CustomUIBuilder builder)
        {
            string text = string.IsNullOrEmpty(lastMessage)
                ? Loc.Pick("委托只认单子上写的品种和数量；交付时先扣库存再结账，失败不会吞料。",
                    "Orders only accept the listed item and amount. Delivery consumes stock first, then pays; a failure never eats your goods.")
                : lastMessage;

            LabUi.Label(builder, text, null, 12f, ContentWidth, 34f, true,
                string.IsNullOrEmpty(lastMessage) ? ChemUi.Dim : ChemUi.Main, false, TextAlignmentOptions.TopLeft);
            builder.AddSpace(4f);
        }

        // ---------------------------------------------------------------
        // 进行中
        // ---------------------------------------------------------------

        private static void BuildActiveSection(CustomUIBuilder builder)
        {
            OrderState state = OrderService.Active;
            OrderTemplate template = OrderService.ActiveTemplate;

            Section(builder, Loc.Pick("进行中", "In progress"));
            if (state == null || template == null)
            {
                LabUi.Label(builder,
                    Loc.Pick("现在没有进行中的委托。下面列出的单子都可以接。",
                        "No order in progress. Everything listed below is available."),
                    null, 12f, ContentWidth, 24f, true, ChemUi.Dim, false, TextAlignmentOptions.TopLeft);
                builder.AddSpace(6f);
                return;
            }

            BuildActiveHeader(builder, state, template);
            BuildStepList(builder, template);
            BuildDeliveryBlock(builder, state, template);
            builder.AddSpace(8f);
        }

        private static void BuildActiveHeader(CustomUIBuilder builder, OrderState state, OrderTemplate template)
        {
            builder.BeginColumn(2f, TextAnchor.UpperLeft);
            RectTransform block = builder.CurrentContainer;
            LabUi.Paint(block, new Color(0.86f, 0.78f, 0.52f, 0.06f), false);

            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            LabUi.Label(builder, "<b>#" + state.Number + "　" + template.Title + "</b>", null, 13.5f,
                ContentWidth - 24f, 24f, false, ChemUi.Accent, true, TextAlignmentOptions.Left);
            builder.End();

            string deadline = state.DeadlineDay > 0
                ? Loc.Get("order_deadline_line", "期限：第 {0} 天（剩余 {1} 天）", "Due: day {0} ({1} days left)",
                    state.DeadlineDay, Math.Max(0, state.DeadlineDay - Today()))
                : Loc.Pick("期限：无", "Due: none");

            LabUi.Label(builder,
                Loc.Get("order_requester_line", "委托人：{0}　·　接单：第 {1} 天　·　{2}",
                    "Requester: {0} · taken on day {1} · {2}", template.Requester, state.AcceptedDay, deadline),
                null, 11.5f, ContentWidth - 24f, 20f, false, ChemUi.Dim, false, TextAlignmentOptions.Left);

            LabUi.Label(builder, template.Brief, null, 11.5f, ContentWidth - 24f, -1f, true,
                ChemUi.Main, false, TextAlignmentOptions.TopLeft);

            builder.End();
            builder.AddSpace(4f);
        }

        private static void BuildStepList(CustomUIBuilder builder, OrderTemplate template)
        {
            if (template.Steps == null)
            {
                return;
            }

            for (int i = 0; i < template.Steps.Length; i++)
            {
                OrderStep step = template.Steps[i];
                if (step == null)
                {
                    continue;
                }

                bool done = step.Done;
                string mark = done ? "✔" : "○";
                string line = mark + " " + step.Title + (done ? "" : "\n" + step.Hint);
                LabUi.Label(builder, line, null, 11.5f, ContentWidth - 24f, done ? 20f : -1f, true,
                    done ? ChemUi.Good : ChemUi.Main, false, TextAlignmentOptions.TopLeft);
            }

            builder.AddSpace(4f);
        }

        private static void BuildDeliveryBlock(CustomUIBuilder builder, OrderState state, OrderTemplate template)
        {
            if (template.Requirements != null)
            {
                for (int i = 0; i < template.Requirements.Length; i++)
                {
                    OrderRequirement requirement = template.Requirements[i];
                    if (requirement == null)
                    {
                        continue;
                    }

                    int have = SafeDeliverable(requirement);
                    bool enough = have >= requirement.Count;
                    string note = requirement.Note;
                    string line = Loc.Get("order_delivery_row", "交付：{0} ×{1}{2}　·　店里现有 {3}",
                        "Deliver: {0} ×{1}{2} · {3} in store",
                        requirement.Name, requirement.Count, note.Length > 0 ? "（" + note + "）" : "", have);

                    LabUi.Label(builder, line, null, 12f, ContentWidth - 24f, 22f, false,
                        enough ? ChemUi.Good : ChemUi.Bad, false, TextAlignmentOptions.Left);
                }
            }

            LabUi.Label(builder,
                Loc.Get("order_reward_line", "到手：{0} 信用点", "Payout: {0} credits", template.Reward),
                null, 12f, ContentWidth - 24f, 20f, false, ChemUi.Accent, true, TextAlignmentOptions.Left);

            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            bool canSettle = OrderService.CanSettle(out string reason);
            LabUi.Button(builder, Loc.Get("order_deliver", "交付", "Deliver"),
                delegate { Deliver(); }, 96f, 28f, canSettle, canSettle ? "" : reason);

            bool canBorrow = OrderSupply.CanBorrow(template);
            LabUi.Button(builder, Loc.Get("order_borrow", "借用工位", "Loaner kit"),
                delegate { Borrow(); }, 110f, 28f, canBorrow,
                canBorrow
                    ? OrderSupply.Describe(template)
                    : Loc.Pick("这一单的借用工位已经领过了。", "The loaner kit for this order has already been claimed."));

            LabUi.Button(builder, Loc.Get("order_abandon", "放弃", "Abandon"),
                delegate { Abandon(); }, 84f, 28f, true,
                Loc.Pick("放弃这一单，进度清空，可以重新接。", "Drop this order, clear its progress and take it again."));

            builder.AddFlexibleSpace();
            builder.End();

            if (!canSettle && !string.IsNullOrEmpty(reason))
            {
                LabUi.Label(builder, reason, null, 11f, ContentWidth - 24f, 20f, false, ChemUi.Dim, false,
                    TextAlignmentOptions.Left);
            }
        }

        // ---------------------------------------------------------------
        // 可接取
        // ---------------------------------------------------------------

        private static void BuildAvailableSection(CustomUIBuilder builder)
        {
            Section(builder, Loc.Pick("可接取", "Available"));

            List<OrderTemplate> list;
            try
            {
                list = OrderService.Available();
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("读取可接委托失败：" + e.Message);
                list = new List<OrderTemplate>();
            }

            if (list.Count == 0)
            {
                LabUi.Label(builder,
                    Loc.Pick("暂时没有可接的委托。先把前面的单子结清，或者等档案和产量跟上。",
                        "No orders available right now. Settle the earlier ones, or build up your archive and output."),
                    null, 12f, ContentWidth, 24f, true, ChemUi.Dim, false, TextAlignmentOptions.TopLeft);
                builder.AddSpace(6f);
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                try
                {
                    BuildAvailableRow(builder, list[i], i);
                }
                catch (Exception e)
                {
                    ChemUi.Log?.Warning("渲染可接委托失败：" + e.Message);
                }
            }

            builder.AddSpace(6f);
        }

        private static void BuildAvailableRow(CustomUIBuilder builder, OrderTemplate template, int index)
        {
            if (template == null)
            {
                return;
            }

            builder.BeginColumn(2f, TextAnchor.UpperLeft);
            RectTransform block = builder.CurrentContainer;
            LabUi.Paint(block, new Color(0.55f, 0.72f, 1f, index % 2 == 0 ? 0.05f : 0.02f), false);

            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            LabUi.Label(builder, "<b>" + template.Title + "</b>", null, 12.5f, 520f, 22f, false,
                ChemUi.Main, true, TextAlignmentOptions.Left);
            builder.AddFlexibleSpace();
            LabUi.Label(builder,
                Loc.Get("order_row_reward", "{0} 信用点", "{0} credits", template.Reward),
                null, 12f, 130f, 22f, false, ChemUi.Accent, true, TextAlignmentOptions.Right);
            builder.End();

            LabUi.Label(builder,
                Loc.Get("order_row_requester", "委托人：{0}　·　交付：{1}", "Requester: {0} · Deliver: {1}",
                    template.Requester, template.RequirementLine()),
                null, 11.5f, ContentWidth - 24f, 20f, false, ChemUi.Dim, false, TextAlignmentOptions.Left);

            string id = template.Id;
            bool available = OrderService.IsAvailable(template);
            string reason = available ? "" : OrderService.UnavailableReason(template);

            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            LabUi.Button(builder, Loc.Get("order_accept", "接单", "Accept"),
                delegate { Accept(id); }, 84f, 26f, available, reason);
            if (!available && !string.IsNullOrEmpty(reason))
            {
                LabUi.Label(builder, reason, null, 11f, 520f, 20f, false, ChemUi.Dim, false,
                    TextAlignmentOptions.Left);
            }

            builder.AddFlexibleSpace();
            builder.End();

            builder.End();
            builder.AddSpace(4f);
        }

        // ---------------------------------------------------------------
        // 原料采购
        // ---------------------------------------------------------------

        private static void BuildPurchaseSection(CustomUIBuilder builder)
        {
            Section(builder, Loc.Pick("原料采购", "Supply purchase"));

            if (!OrderPurchase.IsUnlocked)
            {
                LabUi.Label(builder,
                    Loc.Pick("结清老锈的试单之后，才有人愿意按价给你供货。",
                        "Once Old Rust's trial order is settled, someone will supply you at a price."),
                    null, 12f, ContentWidth, 24f, true, ChemUi.Dim, false, TextAlignmentOptions.TopLeft);
                builder.AddSpace(6f);
                return;
            }

            List<OrderPurchase.Offer> offers = OrderPurchase.Offers();
            if (offers.Count == 0)
            {
                LabUi.Label(builder, Loc.Pick("供货清单是空的。", "The supply list is empty."),
                    null, 12f, ContentWidth, 22f, false, ChemUi.Dim, false, TextAlignmentOptions.TopLeft);
                builder.AddSpace(6f);
                return;
            }

            purchasePage = ChemPager.Clamp(purchasePage, offers.Count);
            int start = purchasePage * ChemPager.PageSize;
            int end = Mathf.Min(offers.Count, start + ChemPager.PageSize);

            CustomUIElement pagerLabel;
            CustomUIElement pagerPrev;
            CustomUIElement pagerNext;
            ChemPager.AddControls(builder,
                delegate { TurnPurchasePage(-1); },
                delegate { TurnPurchasePage(1); },
                out pagerLabel, out pagerPrev, out pagerNext);

            for (int i = start; i < end; i++)
            {
                try
                {
                    BuildPurchaseRow(builder, offers[i], i - start);
                }
                catch (Exception e)
                {
                    ChemUi.Log?.Warning("渲染采购行失败：" + e.Message);
                }
            }

            ChemPager.Update(pagerLabel, pagerPrev, pagerNext, purchasePage, offers.Count);
            builder.AddSpace(6f);
        }

        private static void TurnPurchasePage(int delta)
        {
            purchasePage += delta;
            Refresh(false);
        }

        private static void BuildPurchaseRow(CustomUIBuilder builder, OrderPurchase.Offer offer, int index)
        {
            if (offer == null)
            {
                return;
            }

            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            RectTransform row = builder.CurrentContainer;
            ChemUi.RowBackground(row, index);
            ChemUi.RowLayout(row, RowWidth, 30f);

            LabItemInfo info = LabUi.Resolve(offer.ItemId);
            Sprite sprite = ChemUi.ModSprite(info != null ? info.SpriteKey : null);
            if (sprite != null)
            {
                builder.AddImage(sprite, IconSize, IconSize, null);
                CustomUIElement icon = builder.Last;
                if (icon != null && icon.image != null)
                {
                    icon.image.preserveAspect = true;
                    icon.image.raycastTarget = false;
                }
            }

            LabUi.Label(builder,
                Loc.Get("order_purchase_row", "{0}　·　{1} 信用点/单位", "{0}　·　{1} credits per unit",
                    offer.Name, offer.UnitPrice),
                null, 12f, 420f, 26f, false, ChemUi.Main, false, TextAlignmentOptions.Left);

            builder.AddFlexibleSpace();

            string itemId = offer.ItemId;
            LabUi.Button(builder, Loc.Get("order_purchase_one", "采购 1", "Buy 1"),
                delegate { Buy(itemId, 1); }, 78f, 24f, true,
                Loc.Get("order_purchase_one_tip", "花 {0} 信用点买 1 单位", "Spend {0} credits for one unit", offer.UnitPrice));

            LabUi.Button(builder, Loc.Get("order_purchase_five", "采购 5", "Buy 5"),
                delegate { Buy(itemId, 5); }, 78f, 24f, true,
                Loc.Get("order_purchase_five_tip", "花 {0} 信用点买 5 单位", "Spend {0} credits for five units", offer.UnitPrice * 5));

            builder.End();
        }

        // ---------------------------------------------------------------
        // 已结算
        // ---------------------------------------------------------------

        private static void BuildHistorySection(CustomUIBuilder builder)
        {
            Section(builder, Loc.Pick("已结算", "Settled"));

            IList<OrderSettlement> history;
            try
            {
                history = OrderService.History;
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("读取结算记录失败：" + e.Message);
                history = null;
            }

            if (history == null || history.Count == 0)
            {
                LabUi.Label(builder, Loc.Pick("还没有结清过任何委托。", "No order has been settled yet."),
                    null, 12f, ContentWidth, 22f, false, ChemUi.Dim, false, TextAlignmentOptions.TopLeft);
                return;
            }

            int shown = 0;
            for (int i = history.Count - 1; i >= 0 && shown < 12; i--, shown++)
            {
                OrderSettlement entry = history[i];
                if (entry == null)
                {
                    continue;
                }

                OrderTemplate template = OrderService.Find(entry.TemplateId);
                string title = template != null ? template.Title : entry.TemplateId;
                LabUi.Label(builder,
                    Loc.Get("order_history_row", "#{0}　{1}　·　第 {2} 天　·　+{3} 信用点",
                        "#{0}　{1}　·　day {2}　·　+{3} credits", entry.Number, title, entry.Day, entry.Reward),
                    null, 11.5f, ContentWidth, 20f, false, ChemUi.Dim, false, TextAlignmentOptions.Left);
            }
        }

        // ---------------------------------------------------------------
        // 动作
        // ---------------------------------------------------------------

        private static void Accept(string templateId)
        {
            bool ok;
            string message;
            try
            {
                ok = OrderService.Accept(templateId, out message);
            }
            catch (Exception e)
            {
                ok = false;
                message = Loc.Pick("接单失败，详情看日志。", "Could not accept the order; see the log.");
                ChemUi.Log?.Error("接委托 " + templateId + " 出错：" + e);
            }

            lastMessage = message ?? "";
            OrderService.InvalidateCounts();
            Refresh(true);
        }

        private static void Deliver()
        {
            bool ok;
            string message;
            int paid;
            try
            {
                ok = OrderService.Settle(out message, out paid);
            }
            catch (Exception e)
            {
                ok = false;
                paid = 0;
                message = Loc.Pick("交付失败，详情看日志。", "Delivery failed; see the log.");
                ChemUi.Log?.Error("委托交付出错：" + e);
            }

            lastMessage = message ?? (ok ? Loc.Pick("交付完成。", "Delivered.") : Loc.Pick("交付没完成。", "Delivery did not go through."));
            OrderService.InvalidateCounts();
            Refresh(true);
        }

        private static void Borrow()
        {
            bool ok;
            string message;
            try
            {
                ok = OrderSupply.Borrow(OrderService.ActiveTemplate, out message);
            }
            catch (Exception e)
            {
                ok = false;
                message = Loc.Pick("借用失败，详情看日志。", "Could not hand out the loaner kit; see the log.");
                ChemUi.Log?.Error("领取借用工位出错：" + e);
            }

            lastMessage = message ?? "";
            OrderService.InvalidateCounts();
            Refresh(true);
        }

        private static void Abandon()
        {
            bool ok;
            string message;
            try
            {
                ok = OrderService.Abandon(out message);
            }
            catch (Exception e)
            {
                ok = false;
                message = Loc.Pick("放弃失败，详情看日志。", "Could not abandon the order; see the log.");
                ChemUi.Log?.Error("放弃委托出错：" + e);
            }

            lastMessage = message ?? "";
            OrderService.InvalidateCounts();
            Refresh(true);
        }

        private static void Buy(string itemId, int count)
        {
            bool ok;
            string message;
            try
            {
                ok = OrderPurchase.Buy(itemId, count, out message);
            }
            catch (Exception e)
            {
                ok = false;
                message = Loc.Pick("采购失败，详情看日志。", "Purchase failed; see the log.");
                ChemUi.Log?.Error("采购 " + itemId + " 出错：" + e);
            }

            lastMessage = message ?? "";
            OrderService.InvalidateCounts();
            Refresh(false);
        }

        // ---------------------------------------------------------------
        // 小工具
        // ---------------------------------------------------------------

        private static void Section(CustomUIBuilder builder, string title)
        {
            builder.AddSpace(2f);
            LabUi.Label(builder, "<b>— " + title + " —</b>", null, 12.5f, ContentWidth, 22f, false,
                ChemUi.Accent, true, TextAlignmentOptions.Left);
        }

        private static int SafeDeliverable(OrderRequirement requirement)
        {
            try
            {
                return OrderService.Deliverable(requirement);
            }
            catch
            {
                return 0;
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

        private static void SetLabel(CustomUIWindow host, string id, string text, Color color)
        {
            if (host == null || !host.IsAlive || string.IsNullOrEmpty(id))
            {
                return;
            }

            try
            {
                CustomUIElement element = host.Get(id);
                if (element == null)
                {
                    return;
                }

                element.SetText(text ?? "");
                element.SetTextColor(color);
            }
            catch
            {
                // 控件已经不在了，忽略
            }
        }
    }
}
