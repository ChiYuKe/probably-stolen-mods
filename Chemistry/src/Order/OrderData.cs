using System;
using System.Collections.Generic;

namespace Chemistry
{
    /// <summary>委托的类型。决定界面分组、以及哪一类进度事件能推进它的步骤。</summary>
    internal enum OrderKind
    {
        /// <summary>从废料里回收单质（首单和铜回收单）。</summary>
        Recover,

        /// <summary>补货：交付若干瓶试剂。</summary>
        SupplyReagent,

        /// <summary>补货：交付固体产物。</summary>
        SupplySolid,

        /// <summary>长期合同：同一张单反复供货。</summary>
        Contract,
    }

    /// <summary>一条交付要求。数量按游戏里的「单位」算（液体 1 单位 = 25 mL）。</summary>
    internal sealed class OrderRequirement
    {
        public readonly string ItemId;
        public readonly int Count;
        public readonly string NoteZh;
        public readonly string NoteEn;

        public OrderRequirement(string itemId, int count, string noteZh = null, string noteEn = null)
        {
            ItemId = itemId ?? "";
            Count = Math.Max(1, count);
            NoteZh = noteZh;
            NoteEn = noteEn;
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

        public string Note
        {
            get { return string.IsNullOrEmpty(NoteZh) && string.IsNullOrEmpty(NoteEn) ? "" : Loc.Pick(NoteZh, NoteEn); }
        }
    }

    /// <summary>委托里的一步。Done 是运行期状态，模板本身只存文案。</summary>
    internal sealed class OrderStep
    {
        public readonly string Id;
        public readonly string TitleZh;
        public readonly string TitleEn;
        public readonly string HintZh;
        public readonly string HintEn;
        public bool Done;

        public OrderStep(string id, string titleZh, string titleEn, string hintZh, string hintEn)
        {
            Id = id ?? "";
            TitleZh = titleZh;
            TitleEn = titleEn;
            HintZh = hintZh;
            HintEn = hintEn;
        }

        public string Title
        {
            get { return Loc.Pick(TitleZh, TitleEn); }
        }

        public string Hint
        {
            get { return Loc.Pick(HintZh, HintEn); }
        }
    }

    /// <summary>
    /// 一张委托的静态定义：谁要、要什么、给多少钱、要先做过什么。
    /// 运行期状态（编号、已完成的步骤、已交付量）在 OrderService 的 OrderState 里。
    /// </summary>
    internal sealed class OrderTemplate
    {
        public string Id;
        public OrderKind Kind;
        public string TitleZh;
        public string TitleEn;
        public string RequesterZh;
        public string RequesterEn;
        public string BriefZh;
        public string BriefEn;

        /// <summary>接单/收货 NPC 的立绘 id（见 OrderNpcService）。</summary>
        public string NpcId;

        /// <summary>结算时一次性付的信用点。</summary>
        public int Reward;

        /// <summary>期限（游戏天）。0 = 无期限，首单永远是 0。</summary>
        public int DurationDays;

        /// <summary>可重复接取（补货单和长期合同）。</summary>
        public bool Repeatable;

        /// <summary>前置委托 id：必须先结算过它。</summary>
        public string RequiresOrder;

        /// <summary>需要先解锁的档案章节 id。</summary>
        public string RequiresChapter;

        /// <summary>最少游戏天数（工业线要等设备可建）。0 = 不限。</summary>
        public int RequiresDay;

        public OrderRequirement[] Requirements;
        public OrderStep[] Steps;

        /// <summary>结算前必须完成的步骤 id（不含 deliver）。</summary>
        public string[] TeachingSteps;

        /// <summary>这条委托路线要用的试剂 key，用于「制出试剂」这一步的判定。</summary>
        public string ReagentKey;

        /// <summary>这条委托要回收的目标元素符号，用于「看过报告 / 亲手回收」判定。</summary>
        public string TargetSymbol;

        public string Title
        {
            get { return Loc.Pick(TitleZh, TitleEn); }
        }

        public string Requester
        {
            get { return Loc.Pick(RequesterZh, RequesterEn); }
        }

        public string Brief
        {
            get { return Loc.Pick(BriefZh, BriefEn); }
        }

        public OrderStep FindStep(string id)
        {
            if (Steps == null || string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (int i = 0; i < Steps.Length; i++)
            {
                if (Steps[i] != null && string.Equals(Steps[i].Id, id, StringComparison.Ordinal))
                {
                    return Steps[i];
                }
            }

            return null;
        }

        public bool IsTeachingStep(string id)
        {
            if (TeachingSteps == null || string.IsNullOrEmpty(id))
            {
                return false;
            }

            for (int i = 0; i < TeachingSteps.Length; i++)
            {
                if (string.Equals(TeachingSteps[i], id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>交付总量（多条要求相加）。</summary>
        public int TotalUnits()
        {
            int total = 0;
            if (Requirements != null)
            {
                for (int i = 0; i < Requirements.Length; i++)
                {
                    if (Requirements[i] != null)
                    {
                        total += Requirements[i].Count;
                    }
                }
            }

            return total;
        }

        /// <summary>一句话的交付要求，例如「铁（Fe）×1」。</summary>
        public string RequirementLine()
        {
            if (Requirements == null || Requirements.Length == 0)
            {
                return Loc.Pick("无", "none");
            }

            var parts = new List<string>();
            for (int i = 0; i < Requirements.Length; i++)
            {
                OrderRequirement requirement = Requirements[i];
                if (requirement == null)
                {
                    continue;
                }

                string note = requirement.Note;
                parts.Add(note.Length > 0
                    ? requirement.Name + " ×" + requirement.Count + "（" + note + "）"
                    : requirement.Name + " ×" + requirement.Count);
            }

            return string.Join("　·　", parts.ToArray());
        }
    }

    /// <summary>
    /// 委托模板数据。全部中英双语，文案跟着《深空当铺》的赛博当铺味走。
    /// 顺序就是界面里的展示顺序；前置关系通过 RequiresOrder 串起来。
    /// </summary>
    internal static class OrderData
    {
        // 常用物品 id，避免在模板里到处拼字符串
        private static string Iron
        {
            get { return ChemistryCatalog.ElementItemId("Fe"); }
        }

        private static string Copper
        {
            get { return ChemistryCatalog.ElementItemId("Cu"); }
        }

        private static string HydrochloricAcid
        {
            get { return ChemistryCatalog.CompoundItemId("hydrochloric_acid"); }
        }

        private static string NitricAcid
        {
            get { return ChemistryCatalog.CompoundItemId("nitric_acid"); }
        }

        private static string Salt
        {
            get { return ChemistryCatalog.CompoundItemId("sodium_chloride"); }
        }

        public static OrderTemplate[] CreateTemplates()
        {
            return new[]
            {
                // ---------------------------------------------------------------
                // 第一版：老锈要一份回收铁
                // ---------------------------------------------------------------
                new OrderTemplate
                {
                    Id = "rust_trial_iron",
                    Kind = OrderKind.Recover,
                    TitleZh = "老锈的试单：一份回收铁",
                    TitleEn = "Old Rust's Trial Order: One Recovered Iron",
                    RequesterZh = "老锈",
                    RequesterEn = "Old Rust",
                    NpcId = "scrap_collector",
                    BriefZh = "老锈想看看你这间当铺到底能不能把废料里的东西提出来。"
                        + "他愿意收一份从垃圾场废料里亲手提出来的铁，价钱按成品算。"
                        + "沈老师的旧笔记指向地下室那台旧设备——先去检测，再决定用哪批料。",
                    BriefEn = "Old Rust wants to see whether this pawnshop can actually pull something out of scrap. "
                        + "He'll buy one unit of iron you recovered yourself from dumping-ground scrap, priced as a finished good. "
                        + "Dr. Shen's old notes point at the equipment in the basement — analyze first, then pick your batch.",
                    Reward = 260,
                    DurationDays = 0,
                    Repeatable = false,
                    RequiresDay = 0,
                    TargetSymbol = "Fe",
                    ReagentKey = "hydrochloric_acid",
                    Requirements = new[] { new OrderRequirement(Iron, 1, "亲手回收", "recovered by hand") },
                    Steps = new[]
                    {
                        new OrderStep("report", "看过成分报告", "Read a composition report",
                            "用光谱仪扫一件含铁的废料（普通废金属或批次 01 的未知金属碎片），把成分报告打开看一遍。",
                            "Scan an iron-bearing sample with the spectrometer (plain scrap metal or batch 01 fragments) and open its report."),
                        new OrderStep("reagent", "制备盐酸", "Prepare hydrochloric acid",
                            "在反应台打开制酸装置，按配方拖入原料、器皿和空瓶，启动后取走成品。",
                            "Open the acid station at the reaction bench, load the recipe's materials, glassware and an empty bottle, then collect the product."),
                        new OrderStep("recover", "亲手回收出铁", "Recover the iron yourself",
                            "在提炼反应台酸浸废料，用过滤漏斗过滤浸出液，再把滤液送进还原槽取出铁。",
                            "Leach the scrap at the refining station, filter the leachate with a funnel, then reduce the filtrate to collect the iron."),
                        new OrderStep("deliver", "回店交货", "Deliver to the shop",
                            "带铁回当铺，在委托记录里确认交付。",
                            "Bring the iron back to the pawnshop and confirm the delivery in the order journal."),
                    },
                    TeachingSteps = new[] { "report", "reagent", "recover" },
                },

                // ---------------------------------------------------------------
                // 前期：铜回收单（可重复）
                // ---------------------------------------------------------------
                new OrderTemplate
                {
                    Id = "rust_copper_recovery",
                    Kind = OrderKind.Recover,
                    TitleZh = "老锈的铜回收单",
                    TitleEn = "Old Rust's Copper Recovery",
                    RequesterZh = "老锈",
                    RequesterEn = "Old Rust",
                    NpcId = "scrap_collector",
                    BriefZh = "老锈手上压着两批烧蚀电路板，含铜高低不一样。"
                        + "他要两份铜，也提醒你：选哪批料、留多少给更值钱的目标，自己算。"
                        + "交付价和当铺柜台价都列在单子上，留货还是交货由你决定。",
                    BriefEn = "Old Rust has two lots of scorched circuit boards with very different copper grades. "
                        + "He wants two units of copper, and he's leaving the math to you: which lot to burn, and how much to keep for a better buyer. "
                        + "The order lists both the delivery price and the counter price — keeping the goods is your call.",
                    Reward = 420,
                    DurationDays = 0,
                    Repeatable = true,
                    RequiresOrder = "rust_trial_iron",
                    TargetSymbol = "Cu",
                    ReagentKey = "nitric_acid",
                    Requirements = new[] { new OrderRequirement(Copper, 2, "两份成品", "two units") },
                    Steps = new[]
                    {
                        new OrderStep("report", "比较两份电路板报告", "Compare both circuit-board reports",
                            "分别扫一批烧蚀电路板，比较含铜比例，再决定烧哪一批。",
                            "Scan both circuit-board lots, compare their copper grades, then decide which one to burn."),
                        new OrderStep("reagent", "制备硝酸", "Prepare nitric acid",
                            "铜线路要硝酸。可以在制酸装置里制备，也可以走生产线。",
                            "The copper route needs nitric acid — make it at the acid station or run the production line."),
                        new OrderStep("recover", "回收出铜", "Recover the copper",
                            "酸浸、过滤、还原，取出两份铜。",
                            "Leach, filter and reduce to collect two units of copper."),
                        new OrderStep("deliver", "回店交货", "Deliver to the shop",
                            "带铜回当铺，在委托记录里确认交付。",
                            "Bring the copper back and confirm the delivery in the order journal."),
                    },
                    TeachingSteps = new[] { "report", "reagent", "recover" },
                },

                // ---------------------------------------------------------------
                // 中期：试剂补货单（多瓶交付，可重复）
                // ---------------------------------------------------------------
                new OrderTemplate
                {
                    Id = "rust_reagent_restock",
                    Kind = OrderKind.SupplyReagent,
                    TitleZh = "老锈的试剂补货单",
                    TitleEn = "Old Rust's Reagent Restock",
                    RequesterZh = "老锈",
                    RequesterEn = "Old Rust",
                    NpcId = "reagent_broker",
                    BriefZh = "老锈的拆船队要用盐酸洗锈，一次要三小瓶（12 单位）。"
                        + "手工制酸装置能出，投资实验室设备组也能出——排几份、什么时候转运、留多少空瓶，你自己定。"
                        + "单子只认盐酸，别拿别的酸或者空瓶来凑。",
                    BriefEn = "Old Rust's shipbreaking crew needs hydrochloric acid to strip rust — three small bottles (12 units). "
                        + "The manual acid station can supply it, and so can the lab equipment kit; how many batches you queue, when you move them and how many empties you keep is up to you. "
                        + "The order only accepts hydrochloric acid — no other acid, no empty bottles.",
                    Reward = 360,
                    DurationDays = 0,
                    Repeatable = true,
                    RequiresOrder = "rust_trial_iron",
                    ReagentKey = "hydrochloric_acid",
                    Requirements = new[] { new OrderRequirement(HydrochloricAcid, 12, "三小瓶", "three small bottles") },
                    Steps = new[]
                    {
                        new OrderStep("produce", "制备盐酸", "Prepare the hydrochloric acid",
                            "在制酸装置或生产线做出至少 12 单位的盐酸，装进瓶子。",
                            "Make at least 12 units of hydrochloric acid at the acid station or the production line, and bottle it."),
                        new OrderStep("deliver", "回店交货", "Deliver to the shop",
                            "带盐酸回当铺，在委托记录里确认交付。",
                            "Bring the acid back and confirm the delivery in the order journal."),
                    },
                    TeachingSteps = new[] { "produce" },
                },

                // ---------------------------------------------------------------
                // 后期：DSC-07 连续采购
                // ---------------------------------------------------------------
                new OrderTemplate
                {
                    Id = "dsc_nitric_contract",
                    Kind = OrderKind.Contract,
                    TitleZh = "DSC-07 长期合同：硝酸",
                    TitleEn = "DSC-07 Standing Contract: Nitric Acid",
                    RequesterZh = "DSC-07 采购员",
                    RequesterEn = "DSC-07 Buyer",
                    NpcId = "reagent_broker",
                    BriefZh = "DSC-07 每个月都要二十桶硝酸，先给你第一批：24 单位。"
                        + "走硝酸联机线，设好库存上限、补上动力、记得清理出料仓。"
                        + "断料或者满仓时，合同会停在那儿等你处理，不会自己扣钱。",
                    BriefEn = "DSC-07 takes twenty drums of nitric acid a month. Your first lot is 24 units. "
                        + "Run the nitric line: set the stock cap, keep the power topped up and clear the output bin. "
                        + "If the line starves or fills up, the contract waits for you — it never bills you behind your back.",
                    Reward = 900,
                    DurationDays = 0,
                    Repeatable = true,
                    RequiresOrder = "rust_reagent_restock",
                    RequiresChapter = "ch5",
                    RequiresDay = IndustrialFactoryData.UnlockDay,
                    ReagentKey = "nitric_acid",
                    Requirements = new[] { new OrderRequirement(NitricAcid, 24, "一批", "one lot") },
                    Steps = new[]
                    {
                        new OrderStep("produce", "生产硝酸", "Produce the nitric acid",
                            "用硝酸联机线产出 24 单位硝酸，出料仓满了先清仓。",
                            "Run the nitric line for 24 units; clear the output bin if it fills up."),
                        new OrderStep("deliver", "交付第一批", "Deliver the first lot",
                            "带硝酸回当铺，在委托记录里确认交付。合同结清后可以接下一批。",
                            "Bring the acid back and confirm the delivery. The contract reopens for the next lot once settled."),
                    },
                    TeachingSteps = new[] { "produce" },
                },

                new OrderTemplate
                {
                    Id = "dsc_solid_contract",
                    Kind = OrderKind.SupplySolid,
                    TitleZh = "DSC-07 长期合同：工业盐",
                    TitleEn = "DSC-07 Standing Contract: Industrial Salt",
                    RequesterZh = "DSC-07 采购员",
                    RequesterEn = "DSC-07 Buyer",
                    NpcId = "reagent_broker",
                    BriefZh = "固体这一单走独立工业车间：盐液结晶线出湿晶，干燥箱出成品。"
                        + "DSC-07 要 8 单位工业盐。哪条线先交、留多少库存，你自己排。",
                    BriefEn = "The solid order runs through the separate industrial workshop: the brine line makes wet crystals, the dryer finishes them. "
                        + "DSC-07 wants 8 units of industrial salt. Which line ships first, and how much stock you keep, is your call.",
                    Reward = 700,
                    DurationDays = 0,
                    Repeatable = true,
                    RequiresOrder = "dsc_nitric_contract",
                    RequiresDay = IndustrialFactoryData.UnlockDay,
                    Requirements = new[] { new OrderRequirement(Salt, 8, "工业级", "industrial grade") },
                    Steps = new[]
                    {
                        new OrderStep("produce", "产出工业盐", "Produce the industrial salt",
                            "在工业车间跑盐液结晶线，再用干燥箱出成品。",
                            "Run the brine crystallization route in the workshop, then finish the product in the drying oven."),
                        new OrderStep("deliver", "交付一批", "Deliver a lot",
                            "带盐回当铺，在委托记录里确认交付。",
                            "Bring the salt back and confirm the delivery in the order journal."),
                    },
                    TeachingSteps = new[] { "produce" },
                },
            };
        }
    }
}
