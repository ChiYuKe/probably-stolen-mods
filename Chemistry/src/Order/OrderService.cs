using System;
using System.Collections.Generic;
using System.Text;

namespace Chemistry
{
    /// <summary>一张进行中委托的运行期状态。模板只存静态定义，这里存这一单的编号和进度。</summary>
    internal sealed class OrderState
    {
        public string TemplateId;
        public int Number;
        public int AcceptedDay;
        public int DeadlineDay;

        /// <summary>已经交过几批（长期合同要交满 Lots 批才结清）。</summary>
        public int DeliveredLots;

        public readonly HashSet<string> DoneSteps = new HashSet<string>(StringComparer.Ordinal);
        public readonly Dictionary<string, int> Delivered = new Dictionary<string, int>(StringComparer.Ordinal);

        public bool IsExpired(int today)
        {
            return DeadlineDay > 0 && today > DeadlineDay;
        }
    }

    /// <summary>一条结算记录，只用于「已结算」列表和排查账目。</summary>
    internal sealed class OrderSettlement
    {
        public int Number;
        public string TemplateId;
        public int Reward;
        public int Day;
    }

    /// <summary>
    /// 委托状态机：接单、步骤推进、交付结算、存档。
    ///
    /// 设计约束（见 data/ORDER_CONTRACT.md）：
    ///   · 委托和八章档案分开记账，档案记探索进度，这里记生意；
    ///   · 进度只由业务事件推进（检测、产出、回收、交付），界面只读状态；
    ///   · 结算时同时校验库存 / 委托 / 信用点，中途失败恢复到可重试状态；
    ///   · 存档数据缺失或损坏一律按空处理，绝不抛异常到游戏。
    /// </summary>
    internal static class OrderService
    {
        private const string KeyActive = "order_active";
        private const string KeyDone = "order_done";
        private const string KeyHistory = "order_history";
        private const string KeySeq = "order_seq";
        private const string KeyBorrowed = "order_borrowed";

        // 全局履历：接单之前做过的事也算数（「允许先做后看」）。
        private const string KeySeen = "order_seen";
        private const string KeyMade = "order_made";
        private const string KeyRecovered = "order_recovered";
        private const string KeyProduced = "order_produced";

        private static readonly List<OrderTemplate> templates = new List<OrderTemplate>(OrderData.CreateTemplates());
        private static readonly HashSet<string> completed = new HashSet<string>(StringComparer.Ordinal);
        private static readonly List<OrderSettlement> history = new List<OrderSettlement>();

        /// <summary>看过成分报告的样品里出现过的元素符号。</summary>
        private static readonly HashSet<string> seenSymbols = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>亲手合成/制酸过的化合物 key。</summary>
        private static readonly HashSet<string> madeKeys = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>亲手回收过的单质符号。</summary>
        private static readonly HashSet<string> recoveredSymbols = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>累计产出过的物品单位数（itemId -&gt; 单位）。长期合同按累计量判定「生产」那一步。</summary>
        private static readonly Dictionary<string, int> producedUnits = new Dictionary<string, int>(StringComparer.Ordinal);

        private static OrderState active;
        private static int sequence;
        private static int borrowedOrderNumber;
        private static bool loaded;
        private static bool initialized;
        private static string modId = ChemistryCatalog.ModId;

        /// <summary>同一帧里反复读库存时复用一次扫描结果。</summary>
        private static Dictionary<string, int> countsCache;
        private static int countsFrame = -1;

        // ---------------------------------------------------------------
        // 生命周期
        // ---------------------------------------------------------------

        public static void Init(string id, ModLog log)
        {
            if (!string.IsNullOrEmpty(id))
            {
                modId = id;
            }

            if (initialized)
            {
                return;
            }

            initialized = true;
            EnsureLoaded();
            log?.Log("委托系统已就绪：" + templates.Count + " 张委托模板");
        }

        public static void Shutdown()
        {
            initialized = false;
            loaded = false;
            active = null;
            completed.Clear();
            history.Clear();
            seenSymbols.Clear();
            madeKeys.Clear();
            recoveredSymbols.Clear();
            producedUnits.Clear();
            sequence = 0;
            borrowedOrderNumber = 0;
            countsCache = null;
            countsFrame = -1;

            for (int i = 0; i < templates.Count; i++)
            {
                ResetSteps(templates[i]);
            }
        }

        /// <summary>读档 / 新档后调用：从模组存档恢复进行中的委托、已结算记录、编号和全局履历。</summary>
        public static void EnsureLoaded()
        {
            try
            {
                completed.Clear();
                history.Clear();
                seenSymbols.Clear();
                madeKeys.Clear();
                recoveredSymbols.Clear();
                producedUnits.Clear();
                active = null;
                sequence = 0;
                borrowedOrderNumber = 0;

                ReadIdSet(ModHelper.GetModData(modId, KeyDone), completed);
                ReadIdSet(ModHelper.GetModData(modId, KeySeen), seenSymbols);
                ReadIdSet(ModHelper.GetModData(modId, KeyMade), madeKeys);
                ReadIdSet(ModHelper.GetModData(modId, KeyRecovered), recoveredSymbols);
                ReadCounts(ModHelper.GetModData(modId, KeyProduced), producedUnits);
                sequence = Math.Max(0, ModHelper.GetModDataInt(modId, KeySeq, 0));
                borrowedOrderNumber = Math.Max(0, ModHelper.GetModDataInt(modId, KeyBorrowed, 0));
                ReadHistory(ModHelper.GetModData(modId, KeyHistory));
                active = ParseActive(ModHelper.GetModData(modId, KeyActive));

                for (int i = 0; i < templates.Count; i++)
                {
                    ResetSteps(templates[i]);
                }

                if (active != null && sequence < active.Number)
                {
                    sequence = active.Number;
                }

                loaded = true;
                ApplyStepsToTemplate();
            }
            catch (Exception e)
            {
                // 存档坏了也不该影响进游戏
                active = null;
                loaded = true;
                ChemistryMod.Log?.Warning("读取委托存档失败，按空委托处理：" + e.Message);
            }
        }

        public static void Save()
        {
            try
            {
                ModHelper.SetModData(modId, KeyActive, SerializeActive(active));
                ModHelper.SetModData(modId, KeyDone, string.Join(",", new List<string>(completed).ToArray()));
                ModHelper.SetModData(modId, KeySeen, string.Join(",", new List<string>(seenSymbols).ToArray()));
                ModHelper.SetModData(modId, KeyMade, string.Join(",", new List<string>(madeKeys).ToArray()));
                ModHelper.SetModData(modId, KeyRecovered, string.Join(",", new List<string>(recoveredSymbols).ToArray()));
                ModHelper.SetModData(modId, KeyProduced, SerializeCounts(producedUnits));
                ModHelper.SetModData(modId, KeyHistory, SerializeHistory());
                ModHelper.SetModDataInt(modId, KeySeq, sequence);
                ModHelper.SetModDataInt(modId, KeyBorrowed, borrowedOrderNumber);
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("保存委托进度失败：" + e.Message);
            }
        }

        // ---------------------------------------------------------------
        // 查询
        // ---------------------------------------------------------------

        public static IList<OrderTemplate> Templates
        {
            get { return templates; }
        }

        public static OrderState Active
        {
            get
            {
                EnsureReady();
                return active;
            }
        }

        public static OrderTemplate ActiveTemplate
        {
            get
            {
                OrderState state = Active;
                return state != null ? Find(state.TemplateId) : null;
            }
        }

        public static IList<OrderSettlement> History
        {
            get
            {
                EnsureReady();
                return history;
            }
        }

        public static int BorrowedOrderNumber
        {
            get
            {
                EnsureReady();
                return borrowedOrderNumber;
            }
        }

        public static void MarkBorrowed(int orderNumber)
        {
            borrowedOrderNumber = Math.Max(0, orderNumber);
            Save();
        }

        public static OrderTemplate Find(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int i = 0; i < templates.Count; i++)
            {
                if (templates[i] != null && string.Equals(templates[i].Id, id, StringComparison.Ordinal))
                {
                    return templates[i];
                }
            }

            return null;
        }

        public static bool IsCompleted(string templateId)
        {
            EnsureReady();
            return !string.IsNullOrEmpty(templateId) && completed.Contains(templateId);
        }

        /// <summary>现在能不能接这张单：前置委托、档案章节、游戏天数、是否已在做、是否一次性已做完。</summary>
        public static bool IsAvailable(OrderTemplate template)
        {
            EnsureReady();
            if (template == null || string.IsNullOrEmpty(template.Id))
            {
                return false;
            }

            if (active != null && string.Equals(active.TemplateId, template.Id, StringComparison.Ordinal))
            {
                return false;
            }

            if (!template.Repeatable && completed.Contains(template.Id))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(template.RequiresOrder) && !completed.Contains(template.RequiresOrder))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(template.RequiresChapter) && !IsChapterUnlocked(template.RequiresChapter))
            {
                return false;
            }

            if (template.RequiresDay > 0 && Today() < template.RequiresDay)
            {
                return false;
            }

            if (template.RequiresAnalysis && (string.IsNullOrEmpty(template.TargetSymbol)
                || !seenSymbols.Contains(template.TargetSymbol)))
            {
                return false;
            }

            return true;
        }

        /// <summary>可接取列表（按模板顺序）。</summary>
        public static List<OrderTemplate> Available()
        {
            EnsureReady();
            Tick();
            var result = new List<OrderTemplate>();
            for (int i = 0; i < templates.Count; i++)
            {
                if (IsAvailable(templates[i]))
                {
                    result.Add(templates[i]);
                }
            }

            return result;
        }

        /// <summary>不能接的原因，给界面做提示。</summary>
        public static string UnavailableReason(OrderTemplate template)
        {
            if (template == null)
            {
                return Loc.Pick("委托不存在。", "No such order.");
            }

            if (active != null && string.Equals(active.TemplateId, template.Id, StringComparison.Ordinal))
            {
                return Loc.Pick("这一单正在进行中。", "This order is already in progress.");
            }

            if (!template.Repeatable && completed.Contains(template.Id))
            {
                return Loc.Pick("这一单已经结清了。", "This order has already been settled.");
            }

            if (!string.IsNullOrEmpty(template.RequiresOrder) && !completed.Contains(template.RequiresOrder))
            {
                OrderTemplate previous = Find(template.RequiresOrder);
                return Loc.Get("order_need_previous", "要先结清「{0}」。", "Settle \"{0}\" first.",
                    previous != null ? previous.Title : template.RequiresOrder);
            }

            if (!string.IsNullOrEmpty(template.RequiresChapter) && !IsChapterUnlocked(template.RequiresChapter))
            {
                return Loc.Pick("档案进度还不够，对方还在观望。", "Your archive is not far enough along; they are still watching.");
            }

            if (template.RequiresDay > 0 && Today() < template.RequiresDay)
            {
                return Loc.Get("order_need_day", "第 {0} 天之后才会派单。", "Only offered after day {0}.", template.RequiresDay);
            }

            if (template.RequiresAnalysis && (string.IsNullOrEmpty(template.TargetSymbol)
                || !seenSymbols.Contains(template.TargetSymbol)))
            {
                return Loc.Get("order_need_target_analysis",
                    "先用光谱仪检测含 {0} 的废料并打开报告，再来接单。",
                    "Scan scrap containing {0} and open its report before accepting this order.", template.TargetSymbol ?? "?");
            }

            return Loc.Pick("暂时接不了。", "Not available right now.");
        }

        // ---------------------------------------------------------------
        // 接单 / 放弃
        // ---------------------------------------------------------------

        public static bool Accept(string templateId, out string message)
        {
            EnsureReady();
            OrderTemplate template = Find(templateId);
            if (template == null)
            {
                message = Loc.Pick("没有这张委托。", "No such order.");
                return false;
            }

            if (active != null)
            {
                message = Loc.Get("order_busy", "手上还有「{0}」没做完。", "You still have \"{0}\" in progress.",
                    ActiveTemplate != null ? ActiveTemplate.Title : active.TemplateId);
                return false;
            }

            if (!IsAvailable(template))
            {
                message = UnavailableReason(template);
                return false;
            }

            sequence++;
            active = new OrderState
            {
                TemplateId = template.Id,
                Number = sequence,
                AcceptedDay = Today(),
                DeadlineDay = template.DurationDays > 0 ? Today() + template.DurationDays : 0,
            };

            ResetSteps(template);

            // 接单之前做过的事也算数：已经看过含目标元素的报告、已经制过这种试剂、
            // 已经亲手回收过这种单质，都在接单这一刻补记上（「允许先做后看」）。
            SeedFromHistory(template);

            ApplyStepsToTemplate();
            Save();

            message = Loc.Get("order_accepted", "接下第 {0} 号委托：{1}。", "Accepted order #{0}: {1}.", active.Number, template.Title);
            ChemistryMod.Log?.Log("接下委托 #" + active.Number + " " + template.Id);
            DayHudBridge.Push(message, "info", 6f);

            // 首单保底：手上完全没有含铁废料时直接给一批样品，避免首晚空手而归做不下去
            try
            {
                OrderSupply.EnsureStarterSample(template);
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("发放保底样品失败：" + e.Message);
            }

            return true;
        }

        /// <summary>按全局履历补记接单前已经完成的步骤。</summary>
        private static void SeedFromHistory(OrderTemplate template)
        {
            if (template == null || active == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(template.TargetSymbol))
            {
                if (seenSymbols.Contains(template.TargetSymbol))
                {
                    active.DoneSteps.Add("report");
                }

                if (recoveredSymbols.Contains(template.TargetSymbol))
                {
                    active.DoneSteps.Add("recover");
                }
            }

            if (!string.IsNullOrEmpty(template.ReagentKey) && madeKeys.Contains(template.ReagentKey))
            {
                active.DoneSteps.Add("reagent");
            }

            if (ProducedEnough(template))
            {
                active.DoneSteps.Add("produce");
            }

            // 只保留模板里真实存在的步骤 id
            active.DoneSteps.RemoveWhere(id => template.FindStep(id) == null);
        }

        public static bool Abandon(out string message)
        {
            EnsureReady();
            if (active == null)
            {
                message = Loc.Pick("没有进行中的委托。", "There is no order in progress.");
                return false;
            }

            OrderTemplate template = ActiveTemplate;
            message = Loc.Get("order_abandoned", "放弃了第 {0} 号委托「{1}」。", "Abandoned order #{0}: \"{1}\".",
                active.Number, template != null ? template.Title : active.TemplateId);
            active = null;
            Save();
            DayHudBridge.Push(message, "warn", 5f);
            return true;
        }

        // ---------------------------------------------------------------
        // 进度事件
        // ---------------------------------------------------------------

        /// <summary>
        /// 看过成分报告。先把样品里的元素记进全局履历（接单前扫过也算数），
        /// 再推进当前委托的「看过报告」那一步。
        /// </summary>
        public static void OnSampleAnalyzed(SampleDef sample)
        {
            if (sample?.Components == null)
            {
                return;
            }

            try
            {
                EnsureReady();
                LootIntegration.ConfirmSampleAnalyzed(sample);

                bool recorded = false;
                for (int i = 0; i < sample.Components.Length; i++)
                {
                    string symbol = sample.Components[i].Symbol;
                    if (!string.IsNullOrEmpty(symbol) && seenSymbols.Add(symbol))
                    {
                        recorded = true;
                    }
                }

                if (recorded)
                {
                    Save();
                }

                OrderTemplate template = ActiveTemplate;
                if (template == null || string.IsNullOrEmpty(template.TargetSymbol))
                {
                    return;
                }

                if (seenSymbols.Contains(template.TargetSymbol))
                {
                    MarkStep("report");
                }
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("推进委托检测进度失败：" + e.Message);
            }
        }

        /// <summary>
        /// 合成 / 制酸 / 工业线出料成功。记进全局履历和累计产量，再推进当前委托的对应步骤。
        /// units 是这一批实际入库的单位数（长期合同按累计量判定「生产」那一步）。
        /// </summary>
        public static void OnCompoundProduced(CompoundDef def)
        {
            OnCompoundProduced(def, 1);
        }

        public static void OnCompoundProduced(CompoundDef def, int units)
        {
            if (def == null)
            {
                return;
            }

            try
            {
                EnsureReady();

                bool recorded = madeKeys.Add(def.Key);
                int amount = Math.Max(1, units);
                if (!string.IsNullOrEmpty(def.ItemId))
                {
                    producedUnits.TryGetValue(def.ItemId, out int existing);
                    producedUnits[def.ItemId] = existing + amount;
                    recorded = true;
                }

                if (recorded)
                {
                    Save();
                }

                OrderTemplate template = ActiveTemplate;
                if (template == null)
                {
                    return;
                }

                if (!string.IsNullOrEmpty(template.ReagentKey) && madeKeys.Contains(template.ReagentKey))
                {
                    MarkStep("reagent");
                }

                if (ProducedEnough(template))
                {
                    MarkStep("produce");
                }
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("推进委托产出进度失败：" + e.Message);
            }
        }

        /// <summary>亲手回收出单质（酸浸-过滤-还原，或蒸馏分离）。接单前回收过的也算数。</summary>
        public static void OnElementRecovered(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                return;
            }

            try
            {
                EnsureReady();

                if (recoveredSymbols.Add(symbol))
                {
                    Save();
                }

                OrderTemplate template = ActiveTemplate;
                if (template == null || string.IsNullOrEmpty(template.TargetSymbol))
                {
                    return;
                }

                if (recoveredSymbols.Contains(template.TargetSymbol))
                {
                    MarkStep("recover");
                }
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("推进委托回收进度失败：" + e.Message);
            }
        }

        /// <summary>累计产量够不够交付要求（长期合同按每批的量算）。</summary>
        private static bool ProducedEnough(OrderTemplate template)
        {
            if (template?.Requirements == null)
            {
                return false;
            }

            for (int i = 0; i < template.Requirements.Length; i++)
            {
                OrderRequirement requirement = template.Requirements[i];
                if (requirement == null)
                {
                    continue;
                }

                if (!producedUnits.TryGetValue(requirement.ItemId, out int produced) || produced < requirement.Count)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>累计产出过多少单位（界面展示用）。</summary>
        public static int ProducedUnits(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            EnsureReady();
            return producedUnits.TryGetValue(itemId, out int value) ? value : 0;
        }

        /// <summary>玩家是否已经看过含该元素的成分报告（接单前也算）。</summary>
        public static bool HasSeenElement(string symbol)
        {
            EnsureReady();
            return !string.IsNullOrEmpty(symbol) && seenSymbols.Contains(symbol);
        }

        /// <summary>到达地下实验室：只用来刷新期限，不推进步骤。</summary>
        public static void OnLabVisited()
        {
            EnsureReady();
            Tick();
        }

        public static bool IsStepDone(string stepId)
        {
            EnsureReady();
            return active != null && !string.IsNullOrEmpty(stepId) && active.DoneSteps.Contains(stepId);
        }

        /// <summary>标记一步完成（幂等）。只有当前模板里存在的步骤才会被记下来。</summary>
        public static void MarkStep(string stepId)
        {
            EnsureReady();
            if (active == null || string.IsNullOrEmpty(stepId))
            {
                return;
            }

            OrderTemplate template = ActiveTemplate;
            if (template == null || template.FindStep(stepId) == null)
            {
                return;
            }

            if (!active.DoneSteps.Add(stepId))
            {
                return;
            }

            ApplyStepsToTemplate();
            Save();

            OrderStep step = template.FindStep(stepId);
            string message = Loc.Get("order_step_done", "委托进度：{0}", "Order progress: {0}", step != null ? step.Title : stepId);
            DayHudBridge.Push(message, "info", 4f);
            ChemistryMod.Log?.Log("委托 #" + active.Number + " 完成步骤 " + stepId);
        }

        // ---------------------------------------------------------------
        // 交付
        // ---------------------------------------------------------------

        /// <summary>店里现有可交付数量。</summary>
        public static int Deliverable(OrderRequirement requirement)
        {
            if (requirement == null || string.IsNullOrEmpty(requirement.ItemId))
            {
                return 0;
            }

            Dictionary<string, int> counts = Counts();
            return counts != null && counts.TryGetValue(requirement.ItemId, out int value) ? Math.Max(0, value) : 0;
        }

        /// <summary>店里装得满满的该液体瓶数（半瓶、空瓶、别的液体都不算）。</summary>
        public static int FullBottles(OrderRequirement requirement)
        {
            if (requirement == null || requirement.Bottles <= 0 || string.IsNullOrEmpty(requirement.ItemId))
            {
                return 0;
            }

            try
            {
                return StoreInventory.CountFullBottles(requirement.ItemId, requirement.BottleSize);
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("统计满瓶数量失败：" + e.Message);
                return 0;
            }
        }

        /// <summary>教学步骤是否都做完了。没做完时给出还差哪一步。</summary>
        public static bool RequirementsMet(out string reason)
        {
            EnsureReady();
            reason = null;
            if (active == null)
            {
                reason = Loc.Pick("没有进行中的委托。", "There is no order in progress.");
                return false;
            }

            OrderTemplate template = ActiveTemplate;
            if (template == null)
            {
                reason = Loc.Pick("委托数据丢了。", "The order data is missing.");
                return false;
            }

            if (template.TeachingSteps != null)
            {
                for (int i = 0; i < template.TeachingSteps.Length; i++)
                {
                    string id = template.TeachingSteps[i];
                    if (string.IsNullOrEmpty(id) || active.DoneSteps.Contains(id))
                    {
                        continue;
                    }

                    OrderStep step = template.FindStep(id);
                    reason = Loc.Get("order_step_missing", "还差一步：{0}", "Still missing: {0}",
                        step != null ? step.Title : id);
                    return false;
                }
            }

            return true;
        }

        /// <summary>能不能结算：教学步骤 + 店里数量。</summary>
        public static bool CanSettle(out string reason)
        {
            if (!RequirementsMet(out reason))
            {
                return false;
            }

            OrderTemplate template = ActiveTemplate;
            if (template == null || template.Requirements == null)
            {
                reason = Loc.Pick("委托数据丢了。", "The order data is missing.");
                return false;
            }

            Dictionary<string, int> counts = Counts();
            for (int i = 0; i < template.Requirements.Length; i++)
            {
                OrderRequirement requirement = template.Requirements[i];
                if (requirement == null)
                {
                    continue;
                }

                int have = counts != null && counts.TryGetValue(requirement.ItemId, out int value) ? value : 0;
                if (have < requirement.Count)
                {
                    reason = Loc.Get("order_not_enough", "店里只有 {0} 件{1}，需要 {2} 件。",
                        "The store only has {0} × {1}; {2} needed.", have, requirement.Name, requirement.Count);
                    return false;
                }

                if (requirement.Bottles > 0)
                {
                    int full = FullBottles(requirement);
                    if (full < requirement.Bottles)
                    {
                        reason = Loc.Get("order_not_enough_bottles",
                            "满瓶不够：单子上要 {0} 个装满的{1}，店里只有 {2} 个（半瓶和空瓶不算）。",
                            "Not enough full bottles: the slip wants {0} full {1}, the store has {2} (partial and empty bottles do not count).",
                            requirement.Bottles, requirement.Name, full);
                        return false;
                    }
                }
            }

            reason = null;
            return true;
        }

        /// <summary>
        /// 结算：先扣库存再发钱。扣库存失败就不发钱、不结单，状态保持在可重试。
        /// 分多批的长期合同每次只交一批，交满 Lots 批才结清并付一次钱。
        /// 结清后清空进行中的委托，所以重复点击不会双倍发钱。
        /// </summary>
        public static bool Settle(out string message, out int paid)
        {
            EnsureReady();
            paid = 0;

            if (active == null)
            {
                message = Loc.Pick("没有进行中的委托。", "There is no order in progress.");
                return false;
            }

            OrderTemplate template = ActiveTemplate;
            if (template == null)
            {
                message = Loc.Pick("委托数据丢了。", "The order data is missing.");
                return false;
            }

            if (!CanSettle(out message))
            {
                return false;
            }

            var consumed = new List<OrderRequirement>();
            for (int i = 0; i < template.Requirements.Length; i++)
            {
                OrderRequirement requirement = template.Requirements[i];
                if (requirement == null)
                {
                    continue;
                }

                bool ok;
                try
                {
                    // 要满瓶的单子先扣满瓶，避免把整瓶拆散；其余按单位扣。
                    ok = requirement.Bottles > 0
                        ? StoreInventory.TryConsumeBottled(requirement.ItemId, requirement.Count,
                            requirement.Bottles, requirement.BottleSize)
                        : StoreInventory.TryConsume(requirement.ItemId, requirement.Count);
                }
                catch (Exception e)
                {
                    ChemistryMod.Log?.Warning("交付时扣库存失败：" + e.Message);
                    ok = false;
                }

                if (!ok)
                {
                    Refund(consumed);
                    InvalidateCounts();
                    message = Loc.Get("order_deliver_failed", "交付没完成（{0} 扣不掉），状态已恢复，可以重试。",
                        "Delivery failed ({0} could not be consumed). Nothing was charged; try again.",
                        requirement.Name);
                    return false;
                }

                consumed.Add(requirement);
            }

            int lots = Math.Max(1, template.Lots);
            int number = active.Number;
            int deliveredLots = active.DeliveredLots + 1;

            // 还没交满批数：只记一批，不结账。
            if (deliveredLots < lots)
            {
                active.DeliveredLots = deliveredLots;
                InvalidateCounts();
                Save();

                message = Loc.Get("order_lot_delivered", "第 {0} 号委托已交第 {1}/{2} 批，还剩 {3} 批。",
                    "Order #{0}: lot {1}/{2} delivered, {3} to go.",
                    number, deliveredLots, lots, lots - deliveredLots);
                DayHudBridge.Push(message, "info", 7f);
                ChemistryMod.Log?.Log("委托 #" + number + " 交付第 " + deliveredLots + "/" + lots + " 批");
                return true;
            }

            int reward = Math.Max(0, template.Reward);
            try
            {
                if (reward > 0)
                {
                    ModHelper.AddCredit(reward);
                }
            }
            catch (Exception e)
            {
                // 钱发不出去就把料还回去，别让玩家白交
                Refund(consumed);
                InvalidateCounts();
                message = Loc.Pick("信用点结算失败，材料已退回，请再试一次。",
                    "Paying out failed; the goods were returned. Try again.");
                ChemistryMod.Log?.Warning("委托结算发钱失败：" + e.Message);
                return false;
            }

            paid = reward;
            history.Add(new OrderSettlement
            {
                Number = number,
                TemplateId = template.Id,
                Reward = reward,
                Day = Today(),
            });

            // completed 记的是「至少结清过一次」，可重复委托也写进去：
            // 后面的委托靠它判断前置（例如长期合同要求先做完补货单），
            // 而「能不能再接」由 Repeatable 单独控制。
            completed.Add(template.Id);

            active = null;
            ResetSteps(template);
            InvalidateCounts();
            Save();

            message = lots > 1
                ? Loc.Get("order_settled_lots", "第 {0} 号委托 {1} 批全部交完，结清到手 {2} 信用点。",
                    "Order #{0}: all {1} lots delivered. {2} credits received.", number, lots, reward)
                : Loc.Get("order_settled", "第 {0} 号委托结清，到手 {1} 信用点。",
                    "Order #{0} settled. {1} credits received.", number, reward);
            DayHudBridge.Push(message, "good", 8f);
            ChemistryMod.Log?.Log("委托 #" + number + " 结算：" + template.Id + "，报酬 " + reward);
            return true;
        }

        private static void Refund(List<OrderRequirement> consumed)
        {
            if (consumed == null)
            {
                return;
            }

            for (int i = 0; i < consumed.Count; i++)
            {
                OrderRequirement requirement = consumed[i];
                if (requirement == null)
                {
                    continue;
                }

                try
                {
                    if (requirement.Bottles > 0 && requirement.BottleSize >= 0)
                    {
                        int unitsPerBottle = ReagentBottle.CapacityFor(requirement.ItemId, requirement.BottleSize);
                        for (int bottleIndex = 0; bottleIndex < requirement.Bottles; bottleIndex++)
                        {
                            GameItem bottle = ModHelper.SpawnItem(requirement.ItemId);
                            if (bottle == null)
                            {
                                ChemistryMod.Log?.Warning("交付回滚时无法生成瓶子：" + requirement.ItemId);
                                continue;
                            }

                            ModHelper.SetItemOwnerShip(bottle, true);
                            ReagentBottle.SetSize(bottle, requirement.BottleSize);
                            bottle.SetUnitCount(unitsPerBottle);
                            if (!StoreInventory.TryPlace(bottle))
                            {
                                ChemistryMod.Log?.Warning("交付回滚时放不回店里：" + requirement.ItemId);
                            }
                        }

                        continue;
                    }

                    GameItem item = ModHelper.SpawnItem(requirement.ItemId);
                    if (item == null)
                    {
                        continue;
                    }

                    ModHelper.SetItemOwnerShip(item, true);
                    item.SetUnitCount(requirement.Count);
                    if (!StoreInventory.TryPlace(item))
                    {
                        ChemistryMod.Log?.Warning("交付回滚时放不回店里：" + requirement.ItemId);
                    }
                }
                catch (Exception e)
                {
                    ChemistryMod.Log?.Warning("交付回滚失败：" + e.Message);
                }
            }
        }

        // ---------------------------------------------------------------
        // 期限
        // ---------------------------------------------------------------

        /// <summary>检查进行中的委托有没有过期。过期只提示并放开重接，不扣钱。</summary>
        public static void Tick()
        {
            EnsureReady();
            if (active == null)
            {
                return;
            }

            OrderTemplate template = ActiveTemplate;
            if (template == null || template.DurationDays <= 0)
            {
                return;
            }

            if (!active.IsExpired(Today()))
            {
                return;
            }

            int number = active.Number;
            active = null;
            ResetSteps(template);
            Save();

            string message = Loc.Get("order_expired", "第 {0} 号委托「{1}」过期了，可以重新接。",
                "Order #{0} \"{1}\" expired. You can take it again.", number, template.Title);
            DayHudBridge.Push(message, "warn", 6f);
            ChemistryMod.Log?.Log("委托 #" + number + " 过期：" + template.Id);
        }

        // ---------------------------------------------------------------
        // 展示
        // ---------------------------------------------------------------

        /// <summary>一行摘要，给标签页标题或 HUD 用。</summary>
        public static string ProgressLine()
        {
            EnsureReady();
            OrderTemplate template = ActiveTemplate;
            if (template == null)
            {
                List<OrderTemplate> list = Available();
                return list.Count > 0
                    ? Loc.Get("order_line_idle", "没有进行中的委托 · 可接 {0} 张", "No order in progress · {0} available", list.Count)
                    : Loc.Pick("没有进行中的委托。", "No order in progress.");
            }

            int total = template.Steps != null ? template.Steps.Length : 0;
            int done = 0;
            if (template.Steps != null)
            {
                for (int i = 0; i < template.Steps.Length; i++)
                {
                    OrderStep step = template.Steps[i];
                    if (step != null && active.DoneSteps.Contains(step.Id))
                    {
                        done++;
                    }
                }
            }

            string deadline = active.DeadlineDay > 0
                ? Loc.Get("order_line_deadline", " · 剩余 {0} 天", " · {0} days left", Math.Max(0, active.DeadlineDay - Today()))
                : "";

            string lots = template.Lots > 1
                ? Loc.Get("order_line_lots", " · 已交 {0}/{1} 批", " · lots {0}/{1}", active.DeliveredLots, template.Lots)
                : "";

            return Loc.Get("order_line_active", "#{0} {1} · {2}/{3} 步{4}{5}",
                "Order #{0} {1} · {2}/{3} steps{4}{5}", active.Number, template.Title, done, total, deadline, lots);
        }

        /// <summary>当前该做什么。流程指南和 HUD 都用这一句。</summary>
        public static string NextObjective()
        {
            EnsureReady();
            OrderTemplate template = ActiveTemplate;
            if (template == null)
            {
                List<OrderTemplate> list = Available();
                if (list.Count == 0)
                {
                    return Loc.Pick("暂无可接的委托，先把档案和产量做起来。",
                        "No orders available right now; build up your archive and output first.");
                }

                return Loc.Get("order_objective_accept", "去当铺接单：{0}", "Take an order at the shop: {0}", list[0].Title);
            }

            if (template.Steps != null)
            {
                for (int i = 0; i < template.Steps.Length; i++)
                {
                    OrderStep step = template.Steps[i];
                    if (step == null || active.DoneSteps.Contains(step.Id))
                    {
                        continue;
                    }

                    if (string.Equals(step.Id, "deliver", StringComparison.Ordinal))
                    {
                        if (CanSettle(out _))
                        {
                            return template.Lots > 1
                                ? Loc.Get("order_objective_deliver_lot", "回当铺交付第 {0}/{1} 批：{2}",
                                    "Deliver lot {0}/{1} at the shop: {2}",
                                    active.DeliveredLots + 1, template.Lots, template.RequirementLine())
                                : Loc.Get("order_objective_deliver", "回当铺交付：{0}", "Deliver at the shop: {0}", template.RequirementLine());
                        }

                        if (!RequirementsMet(out string missing))
                        {
                            return missing;
                        }

                        return Loc.Get("order_objective_gather", "备货：{0}", "Stock up: {0}", template.RequirementLine());
                    }

                    return step.Hint;
                }
            }

            return CanSettle(out string reason)
                ? Loc.Get("order_objective_deliver", "回当铺交付：{0}", "Deliver at the shop: {0}", template.RequirementLine())
                : reason;
        }

        /// <summary>
        /// 成分报告里的委托提示：这批料对本单够不够、缺什么、走哪个工位。
        /// 没有进行中的委托、或和本单无关时返回空串。
        /// </summary>
        public static string SampleAdvice(SampleDef sample)
        {
            if (sample == null)
            {
                return "";
            }

            try
            {
                OrderTemplate template = ActiveTemplate;
                if (template == null || string.IsNullOrEmpty(template.TargetSymbol))
                {
                    return "";
                }

                ElementDef target = ChemistryCatalog.FindElement(template.TargetSymbol);
                string targetName = target != null ? SampleTable.ElementName(target.Symbol) : template.TargetSymbol;

                int percent = 0;
                if (sample.Components != null)
                {
                    for (int i = 0; i < sample.Components.Length; i++)
                    {
                        if (string.Equals(sample.Components[i].Symbol, template.TargetSymbol, StringComparison.Ordinal))
                        {
                            percent = sample.Components[i].Percent;
                            break;
                        }
                    }
                }

                if (percent <= 0)
                {
                    return Loc.Get("order_sample_unrelated", "本单要 {0}，这批料里没有。",
                        "This order needs {0}; this batch has none.", targetName);
                }

                int needed = LabRefining.SamplesPerProduct(percent);
                bool acid = LabRefining.IsAcidTarget(sample, template.TargetSymbol);
                CompoundDef reagent = acid ? LabRefining.RequiredAcid(sample, template.TargetSymbol) : null;
                string route = acid
                    ? Loc.Get("order_sample_route_acid", "提炼反应台酸浸（{0}）",
                        "refining station leaching ({0})", reagent != null ? Loc.Pick(reagent.NameZh, reagent.NameEn) : "acid")
                    : Loc.Get("order_sample_route_distill", "蒸馏器分离", "distiller separation");

                return Loc.Get("order_sample_match", "本单要 {0}：这批含 {1}%，{2} 份出一份，走{3}。",
                    "This order needs {0}: this batch is {1}%, {2} sample(s) per unit, via {3}.",
                    targetName, percent, needed, route);
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("生成委托样品提示失败：" + e.Message);
                return "";
            }
        }

        /// <summary>调试用：清空委托进度和存档。</summary>
        public static void ResetForDebug()
        {
            try
            {
                active = null;
                completed.Clear();
                history.Clear();
                seenSymbols.Clear();
                madeKeys.Clear();
                recoveredSymbols.Clear();
                producedUnits.Clear();
                sequence = 0;
                borrowedOrderNumber = 0;
                loaded = true;

                for (int i = 0; i < templates.Count; i++)
                {
                    ResetSteps(templates[i]);
                }

                Save();
                ChemistryMod.Log?.Log("委托进度已重置（进行中 / 已结算 / 编号 / 全局履历全部清空）");
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("重置委托进度失败：" + e.Message);
            }
        }

        // ---------------------------------------------------------------
        // 内部
        // ---------------------------------------------------------------

        private static void EnsureReady()
        {
            if (!loaded)
            {
                EnsureLoaded();
            }
        }

        private static void ResetSteps(OrderTemplate template)
        {
            if (template?.Steps == null)
            {
                return;
            }

            for (int i = 0; i < template.Steps.Length; i++)
            {
                if (template.Steps[i] != null)
                {
                    template.Steps[i].Done = false;
                }
            }
        }

        private static void ApplyStepsToTemplate()
        {
            // 注意：这里不能走 ActiveTemplate 属性——读档过程中 loaded 还没置真，
            // 属性会再调一次 EnsureLoaded，直接递归爆栈。
            OrderTemplate template = active != null ? Find(active.TemplateId) : null;
            if (template?.Steps == null)
            {
                return;
            }

            for (int i = 0; i < template.Steps.Length; i++)
            {
                OrderStep step = template.Steps[i];
                if (step != null)
                {
                    step.Done = active != null && active.DoneSteps.Contains(step.Id);
                }
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

        private static bool IsChapterUnlocked(string chapterId)
        {
            try
            {
                IList<StoryChapter> chapters = StoryService.Chapters;
                if (chapters == null)
                {
                    return false;
                }

                for (int i = 0; i < chapters.Count; i++)
                {
                    StoryChapter chapter = chapters[i];
                    if (chapter != null && string.Equals(chapter.Id, chapterId, StringComparison.Ordinal))
                    {
                        return chapter.Unlocked;
                    }
                }
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("读取档案章节失败：" + e.Message);
            }

            return false;
        }

        private static Dictionary<string, int> Counts()
        {
            int frame = -1;
            try
            {
                frame = UnityEngine.Time.frameCount;
            }
            catch
            {
                frame = -1;
            }

            if (countsCache != null && frame >= 0 && countsFrame == frame)
            {
                return countsCache;
            }

            try
            {
                countsCache = StoreInventory.CountUnits();
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("统计店里库存失败：" + e.Message);
                countsCache = new Dictionary<string, int>(StringComparer.Ordinal);
            }

            countsFrame = frame;
            return countsCache;
        }

        public static void InvalidateCounts()
        {
            countsCache = null;
            countsFrame = -1;
            ChemUi.InvalidateCounts();
        }

        // ---- 序列化 ----

        private static string SerializeActive(OrderState state)
        {
            if (state == null)
            {
                return "";
            }

            var steps = new List<string>(state.DoneSteps);
            steps.Sort(StringComparer.Ordinal);

            var delivered = new List<string>();
            foreach (KeyValuePair<string, int> pair in state.Delivered)
            {
                delivered.Add(pair.Key + ":" + pair.Value);
            }

            delivered.Sort(StringComparer.Ordinal);

            var builder = new StringBuilder();
            builder.Append(state.TemplateId ?? "").Append('|')
                .Append(state.Number).Append('|')
                .Append(state.AcceptedDay).Append('|')
                .Append(state.DeadlineDay).Append('|')
                .Append(string.Join(",", steps.ToArray())).Append('|')
                .Append(string.Join(",", delivered.ToArray())).Append('|')
                .Append(state.DeliveredLots);
            return builder.ToString();
        }

        private static OrderState ParseActive(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            string[] parts = raw.Split('|');
            if (parts.Length < 4)
            {
                return null;
            }

            string templateId = parts[0] != null ? parts[0].Trim() : "";
            if (Find(templateId) == null)
            {
                return null;
            }

            var state = new OrderState
            {
                TemplateId = templateId,
                Number = ParseInt(parts[1]),
                AcceptedDay = ParseInt(parts[2]),
                DeadlineDay = ParseInt(parts[3]),
            };

            if (state.Number <= 0)
            {
                return null;
            }

            if (parts.Length > 4)
            {
                ReadIdSet(parts[4], state.DoneSteps);
            }

            if (parts.Length > 5 && !string.IsNullOrEmpty(parts[5]))
            {
                string[] entries = parts[5].Split(',');
                for (int i = 0; i < entries.Length && i < 512; i++)
                {
                    string entry = entries[i];
                    if (string.IsNullOrEmpty(entry))
                    {
                        continue;
                    }

                    int separator = entry.LastIndexOf(':');
                    if (separator <= 0)
                    {
                        continue;
                    }

                    string id = entry.Substring(0, separator).Trim();
                    int count = ParseInt(entry.Substring(separator + 1));
                    if (id.Length > 0 && count > 0)
                    {
                        state.Delivered[id] = count;
                    }
                }
            }

            if (parts.Length > 6)
            {
                state.DeliveredLots = Math.Max(0, ParseInt(parts[6]));
            }

            return state;
        }

        private static string SerializeCounts(Dictionary<string, int> counts)
        {
            if (counts == null || counts.Count == 0)
            {
                return "";
            }

            var keys = new List<string>(counts.Keys);
            keys.Sort(StringComparer.Ordinal);

            var parts = new List<string>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                parts.Add(key + ":" + counts[key]);
            }

            return string.Join(",", parts.ToArray());
        }

        private static void ReadCounts(string raw, Dictionary<string, int> target)
        {
            if (string.IsNullOrEmpty(raw) || target == null)
            {
                return;
            }

            string[] entries = raw.Split(',');
            int limit = Math.Min(entries.Length, 4096);
            for (int i = 0; i < limit; i++)
            {
                string entry = entries[i];
                if (string.IsNullOrEmpty(entry))
                {
                    continue;
                }

                int separator = entry.LastIndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }

                string id = entry.Substring(0, separator).Trim();
                int count = ParseInt(entry.Substring(separator + 1));
                if (id.Length > 0 && count > 0)
                {
                    target[id] = count;
                }
            }
        }

        private static string SerializeHistory()
        {
            var parts = new List<string>();
            for (int i = 0; i < history.Count; i++)
            {
                OrderSettlement entry = history[i];
                if (entry == null)
                {
                    continue;
                }

                parts.Add(entry.Number + ":" + (entry.TemplateId ?? "") + ":" + entry.Reward + ":" + entry.Day);
            }

            return string.Join(";", parts.ToArray());
        }

        private static void ReadHistory(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            string[] entries = raw.Split(';');
            int limit = Math.Min(entries.Length, 512);
            for (int i = 0; i < limit; i++)
            {
                string entry = entries[i];
                if (string.IsNullOrEmpty(entry))
                {
                    continue;
                }

                string[] fields = entry.Split(':');
                if (fields.Length < 4)
                {
                    continue;
                }

                var record = new OrderSettlement
                {
                    Number = ParseInt(fields[0]),
                    TemplateId = fields[1] != null ? fields[1].Trim() : "",
                    Reward = ParseInt(fields[2]),
                    Day = ParseInt(fields[3]),
                };

                if (record.Number > 0)
                {
                    history.Add(record);
                }
            }
        }

        private static void ReadIdSet(string raw, HashSet<string> target)
        {
            if (string.IsNullOrEmpty(raw) || target == null)
            {
                return;
            }

            string[] parts = raw.Split(',');
            int limit = Math.Min(parts.Length, 4096);
            for (int i = 0; i < limit; i++)
            {
                string id = parts[i] != null ? parts[i].Trim() : "";
                if (id.Length > 0)
                {
                    target.Add(id);
                }
            }
        }

        private static int ParseInt(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            return int.TryParse(text.Trim(), out int value) ? value : 0;
        }
    }
}
