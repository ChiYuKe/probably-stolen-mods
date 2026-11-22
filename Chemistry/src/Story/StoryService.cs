using System;
using System.Collections.Generic;

namespace Chemistry
{
    internal sealed class StoryChapter
    {
        public string Id;
        public string TitleZh;
        public string TitleEn;
        public string TextZh;
        public string TextEn;
        public string HintZh;
        public string HintEn;
        public int Reward;
        public bool Unlocked;
    }

    internal sealed class LabProgress
    {
        public int Visits;
        public int CraftedKinds;
        public int DangerousCrafted;
        public int UnlockedChapters;
        public int TotalChapters;
    }

    /// <summary>
    /// 地下实验室背景故事线（接口见 data/LAB_CONTRACT.md）。
    /// 8 章文案在 StoryData.cs；这里负责按进度解锁、一次性信用点奖励、
    /// lab_crafted / story_unlocked 两个存档字段，以及 chemistry_lab 的离店事件。
    ///
    /// 注意：地图里的 location.Arrive() 是在 visits++ 之前调的，
    /// 所以「首次到访」由 OnLabArrived 按「正在到达就算一次」来判，
    /// 并同时订阅 ModHook.OnAfterhourArriveLate（visits++ 之后）兜底；两次调用不会重复发奖励。
    /// </summary>
    internal static class StoryService
    {
        /// <summary>实验室地点的 id，和 LabLocation.Id 保持一致（这里用字面量，避免跨模块改签名时互相牵连）。</summary>
        private const string LabLocationId = "chemistry_lab";

        private const string KeyStoryUnlocked = "story_unlocked";
        private const string KeyLabCrafted = "lab_crafted";

        /// <summary>危险品：这三样合成了才算接触过「烫手货」。</summary>
        private static readonly string[] DangerousKeys =
        {
            "tnt", "nitroglycerin", "sodium_azide",
            // v11 扩充包：500 种新化合物里的危险品
            "picric_acid", "rdx", "petn", "hmx", "tatp", "mercury_fulminate",
            "dinitrotoluene", "phosgene", "hydrocyanic_acid",
            "sodium_cyanide", "potassium_cyanide",
        };

        private static readonly List<StoryChapter> chapters = new List<StoryChapter>(StoryData.CreateChapters());

        /// <summary>已解锁章节 id（= 已发过奖励的章节，保证每章只发一次）。</summary>
        private static readonly HashSet<string> unlockedIds = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>已合成过的不同化合物 key。</summary>
        private static readonly HashSet<string> craftedKeys = new HashSet<string>(StringComparer.Ordinal);

        private static bool initialized;
        private static bool hooked;
        private static bool loaded;

        // ---------------------------------------------------------------
        // 生命周期
        // ---------------------------------------------------------------

        /// <summary>幂等：订阅实验室到达事件 + 注册 chemistry_lab 的离店事件。</summary>
        public static void Init()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            try
            {
                if (!hooked)
                {
                    ModHook.OnAfterhourArriveLate += OnAfterhourArriveLate;
                    hooked = true;
                }
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("故事线订阅实验室到达事件失败：" + e.Message);
            }

            try
            {
                RegisterLabEvents();
                ChemistryMod.Log?.Log("地下实验室故事线已就绪：8 章档案 + 3 个实验室离店事件");
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("注册实验室离店事件失败：" + e.Message);
            }
        }

        /// <summary>取消订阅 + 清掉本模组注册的所有离店事件。</summary>
        public static void Shutdown()
        {
            try
            {
                if (hooked)
                {
                    ModHook.OnAfterhourArriveLate -= OnAfterhourArriveLate;
                    hooked = false;
                }
            }
            catch
            {
                // 关游戏时事件表可能已经清空，忽略
            }

            try
            {
                ModHelper.RemoveAfterhourEvents(ChemistryCatalog.ModId);
            }
            catch
            {
                // 忽略
            }

            initialized = false;
        }

        /// <summary>
        /// 读档/新档后调用：从 ModHelper 的模组存档里恢复已解锁章节和已合成化合物，
        /// 重算 Chapters[i].Unlocked，并把「条件已满足但存档里漏了」的章节补记上（不重复发奖励）。
        /// 数据缺失/损坏一律按空处理，绝不抛异常。
        /// </summary>
        public static void EnsureLoaded()
        {
            try
            {
                unlockedIds.Clear();
                craftedKeys.Clear();

                ReadIdSet(ModHelper.GetModData(ChemistryCatalog.ModId, KeyStoryUnlocked), unlockedIds, IsKnownChapterId);
                ReadIdSet(ModHelper.GetModData(ChemistryCatalog.ModId, KeyLabCrafted), craftedKeys, IsKnownCompoundKey);

                for (int i = 0; i < chapters.Count; i++)
                {
                    chapters[i].Unlocked = unlockedIds.Contains(chapters[i].Id);
                }

                loaded = true;

                // 存档数据和实际进度对不上时静默补齐，避免玩家解锁了却看不到正文
                if (EvaluateUnlocks(false, GetVisits()))
                {
                    ChemistryMod.Log?.Log("故事线按当前进度补齐了未记录的章节解锁");
                }
            }
            catch (Exception e)
            {
                // 存档坏了也不该影响进游戏，按空处理
                loaded = true;
                ChemistryMod.Log?.Warning("读取实验室故事线存档失败，按空档案处理：" + e.Message);
            }
        }

        public static IList<StoryChapter> Chapters
        {
            get { return chapters; }
        }

        public static LabProgress GetProgress()
        {
            if (!loaded)
            {
                EnsureLoaded();
            }

            int unlocked = 0;
            for (int i = 0; i < chapters.Count; i++)
            {
                if (chapters[i].Unlocked)
                {
                    unlocked++;
                }
            }

            return new LabProgress
            {
                Visits = GetVisits(),
                CraftedKinds = craftedKeys.Count,
                DangerousCrafted = CountDangerousCrafted(),
                UnlockedChapters = unlocked,
                TotalChapters = chapters.Count,
            };
        }

        // ---------------------------------------------------------------
        // 进度推进
        // ---------------------------------------------------------------

        /// <summary>
        /// 到达地下实验室。游戏里 location.Arrive() 在 visits++ 之前先调一次，
        /// MapUIManager 又把 visits++ 之后的 OnAfterhourArriveLate 留给我们再调一次。
        /// 「正在到达」本身就算一次到访，所以这里按 max(visits, 1) 判条件：
        /// 首次到访章节能在面板构建之前就解锁，后面那次重入不会重复发奖励。
        /// </summary>
        public static void OnLabArrived()
        {
            try
            {
                if (!loaded)
                {
                    EnsureLoaded();
                }

                EvaluateUnlocks(true, Math.Max(1, GetVisits()));
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("推进实验室到访进度失败：" + e.Message);
            }
        }

        /// <summary>在实验室合成成功一种化合物：记进 lab_crafted，再检查解锁。</summary>
        public static void OnLabSynthesized(CompoundDef def)
        {
            if (def == null)
            {
                return;
            }

            try
            {
                if (!loaded)
                {
                    EnsureLoaded();
                }

                if (!string.IsNullOrEmpty(def.Key)
                    && ChemistryCatalog.FindCompound(def.Key) != null
                    && craftedKeys.Add(def.Key))
                {
                    SaveState();
                }

                EvaluateUnlocks(true, GetVisits());
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("推进实验室合成进度失败：" + e.Message);
            }
        }

        /// <summary>往实验室储物格存入东西：刷新一次进度判定（到访/合成章节可能刚好满足）。</summary>
        public static void OnLabDeposit()
        {
            try
            {
                if (!loaded)
                {
                    EnsureLoaded();
                }

                EvaluateUnlocks(true, GetVisits());
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("检查实验室存入进度失败：" + e.Message);
            }
        }

        /// <summary>调试用：清空章节解锁和已合成记录，存档也一起清。</summary>
        public static void ResetForDebug()
        {
            try
            {
                unlockedIds.Clear();
                craftedKeys.Clear();

                for (int i = 0; i < chapters.Count; i++)
                {
                    chapters[i].Unlocked = false;
                }

                loaded = true;
                SaveState();
                ChemistryMod.Log?.Log("实验室故事线已重置（章节解锁与 lab_crafted 全部清空）");
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("重置实验室故事线失败：" + e.Message);
            }
        }

        // ---------------------------------------------------------------
        // 事件
        // ---------------------------------------------------------------

        private static void OnAfterhourArriveLate(string locationId)
        {
            if (string.Equals(locationId, LabLocationId, StringComparison.Ordinal))
            {
                OnLabArrived();
            }
        }

        /// <summary>注册 3 个 chemistry_lab 的离店事件：通风橱异响 / 旧笔记被动过 / 公司的人来过。</summary>
        private static void RegisterLabEvents()
        {
            // 1) 刚发现地下室就能撞上的小动静
            List<AfterhourEffect> hoodEffects = new List<AfterhourEffect>
            {
                new AfterhourEffect(
                    () => Loc.Pick(
                        "实验室的电不太稳，今晚做合成多留意火候。",
                        "The lab's power is unsteady tonight. Mind your burners."),
                    null,
                    0f,
                    AfterhourEffectPolarity.Bad),
            };

            ModHelper.AddAfterhourEvent(
                ChemistryCatalog.ModId,
                "chem_lab_hood_knocking",
                LabLocationId,
                () => Loc.Pick("通风橱异响", "The Fume Hood Won't Settle Down"),
                1,
                () => Loc.Pick(
                    "凌晨三点，通风橱的排风扇自己启动了三次。监控只拍到一团雾，和一排在玻璃上慢慢淡掉的指印。",
                    "At three in the morning the fume hood's fan started itself three times. The camera caught fog, and a row of fingerprints fading on the glass."),
                hoodEffects,
                12f,
                AfterhourEventTone.Mixed,
                () => IsChapterUnlocked("ch1"));

            // 2) 合成过东西之后，笔记开始「自己翻页」
            List<AfterhourEffect> notesEffects = new List<AfterhourEffect>
            {
                new AfterhourEffect(
                    () => Loc.Pick(
                        "笔记里多出一条没写进档案的配方，先记下来。",
                        "A recipe that never made it into the archive. Write it down."),
                    null,
                    0f,
                    AfterhourEffectPolarity.Good),
            };

            ModHelper.AddAfterhourEvent(
                ChemistryCatalog.ModId,
                "chem_lab_notes_moved",
                LabLocationId,
                () => Loc.Pick("旧笔记被动过", "The Old Notes Have Been Moved"),
                1,
                () => Loc.Pick(
                    "你离开时笔记本是合上的，回来时摊在新的一页。那页的配方你从没见过，字迹却和你的很像。",
                    "The notebook was shut when you left. It lay open at a page you've never seen, in handwriting that looks uncomfortably like yours."),
                notesEffects,
                9f,
                AfterhourEventTone.Mixed,
                () => IsChapterUnlocked("ch2"));

            // 3) 碰过危险品之后，公司那边就开始注意这间地下室
            List<AfterhourEffect> companyEffects = new List<AfterhourEffect>
            {
                new AfterhourEffect(
                    () => Loc.Pick(
                        "有人在清点地下室的货架，危险品先锁好。",
                        "Someone is counting the basement shelves. Lock the dangerous stock away."),
                    null,
                    0f,
                    AfterhourEffectPolarity.Bad),
            };

            ModHelper.AddAfterhourEvent(
                ChemistryCatalog.ModId,
                "chem_lab_company_visit",
                LabLocationId,
                () => Loc.Pick("公司的人来过", "The Company Paid a Visit"),
                2,
                () => Loc.Pick(
                    "门缝底下塞着一张没有抬头的收货单，纸是新的，压着 DSC 的钢印。你数了数货架，少了一瓶没来得及登记的酸。",
                    "A delivery order with no letterhead was shoved under the door, new paper stamped with DSC. You count the shelves: one unregistered bottle of acid is gone."),
                companyEffects,
                6f,
                AfterhourEventTone.Bad,
                () => IsChapterUnlocked("ch5"));
        }

        // ---------------------------------------------------------------
        // 解锁判定 & 奖励
        // ---------------------------------------------------------------

        /// <summary>按当前进度检查 8 章；grantRewards=true 时给新解锁的章节发信用点 + 推通知。</summary>
        private static bool EvaluateUnlocks(bool grantRewards, int visits)
        {
            bool changed = false;

            for (int i = 0; i < chapters.Count; i++)
            {
                StoryChapter chapter = chapters[i];
                if (chapter.Unlocked)
                {
                    unlockedIds.Add(chapter.Id);
                    continue;
                }

                if (!MeetsCondition(i, visits))
                {
                    continue;
                }

                chapter.Unlocked = true;
                unlockedIds.Add(chapter.Id);
                changed = true;

                if (grantRewards)
                {
                    GrantReward(chapter, i);
                }
            }

            if (changed)
            {
                SaveState();
            }

            return changed;
        }

        /// <summary>章节顺序固定：到访 / 首次合成 / 酸 / 有机物 / 危险品 / 50 种 / 150 种 / 400 种。</summary>
        private static bool MeetsCondition(int index, int visits)
        {
            switch (index)
            {
                case 0:
                    return visits >= 1;
                case 1:
                    return craftedKeys.Count >= 1;
                case 2:
                    return HasCraftedCategory("acid");
                case 3:
                    return HasCraftedCategory("organic");
                case 4:
                    return HasCraftedDangerous();
                case 5:
                    return craftedKeys.Count >= 50;
                case 6:
                    return craftedKeys.Count >= 150;
                case 7:
                    return craftedKeys.Count >= 400;
                default:
                    return false;
            }
        }

        private static bool HasCraftedCategory(string category)
        {
            foreach (string key in craftedKeys)
            {
                CompoundDef def = ChemistryCatalog.FindCompound(key);
                if (def != null && string.Equals(def.Category, category, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCraftedDangerous()
        {
            for (int i = 0; i < DangerousKeys.Length; i++)
            {
                if (craftedKeys.Contains(DangerousKeys[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountDangerousCrafted()
        {
            int count = 0;
            for (int i = 0; i < DangerousKeys.Length; i++)
            {
                if (craftedKeys.Contains(DangerousKeys[i]))
                {
                    count++;
                }
            }

            return count;
        }

        private static void GrantReward(StoryChapter chapter, int index)
        {
            try
            {
                if (chapter.Reward > 0)
                {
                    ModHelper.AddCredit(chapter.Reward);
                }

                string zh = "档案解锁 · 第" + (index + 1) + "章《" + chapter.TitleZh
                    + "》 奖励 " + chapter.Reward + " 信用点";
                string en = "Archive unlocked · Chapter " + (index + 1) + ": " + chapter.TitleEn
                    + " (+" + chapter.Reward + " credits)";

                DayHudBridge.Push(Loc.Pick(zh, en), "info", 9f);

                ChemistryMod.Log?.Log("实验室档案解锁 " + chapter.Id
                    + "《" + chapter.TitleZh + "》，奖励 " + chapter.Reward + " 信用点");
            }
            catch (Exception e)
            {
                // 奖励发不出去也不能打断解锁流程，下次进档按存档补记
                ChemistryMod.Log?.Warning("发放实验室章节奖励失败：" + e.Message);
            }
        }

        // ---------------------------------------------------------------
        // 存档读写
        // ---------------------------------------------------------------

        /// <summary>把 story_unlocked / lab_crafted 写回模组存档（逗号分隔）。</summary>
        private static void SaveState()
        {
            try
            {
                List<string> unlocked = new List<string>();
                for (int i = 0; i < chapters.Count; i++)
                {
                    if (unlockedIds.Contains(chapters[i].Id))
                    {
                        unlocked.Add(chapters[i].Id);
                    }
                }

                List<string> crafted = new List<string>(craftedKeys);
                crafted.Sort(StringComparer.Ordinal);

                ModHelper.SetModData(ChemistryCatalog.ModId, KeyStoryUnlocked, string.Join(",", unlocked));
                ModHelper.SetModData(ChemistryCatalog.ModId, KeyLabCrafted, string.Join(",", crafted));
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("保存实验室故事线进度失败：" + e.Message);
            }
        }

        /// <summary>解析逗号分隔的 id 列表：空/损坏/未知 id 一律丢弃，不抛异常。</summary>
        private static void ReadIdSet(string raw, HashSet<string> target, Func<string, bool> isValid)
        {
            if (string.IsNullOrEmpty(raw) || target == null || isValid == null)
            {
                return;
            }

            string[] parts = raw.Split(',');
            int limit = Math.Min(parts.Length, 4096);
            for (int i = 0; i < limit; i++)
            {
                string id = parts[i] != null ? parts[i].Trim() : null;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                try
                {
                    if (isValid(id))
                    {
                        target.Add(id);
                    }
                }
                catch
                {
                    // 单条坏数据跳过就行
                }
            }
        }

        private static bool IsKnownChapterId(string id)
        {
            for (int i = 0; i < chapters.Count; i++)
            {
                if (string.Equals(chapters[i].Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsKnownCompoundKey(string key)
        {
            return ChemistryCatalog.FindCompound(key) != null;
        }

        private static bool IsChapterUnlocked(string id)
        {
            for (int i = 0; i < chapters.Count; i++)
            {
                if (string.Equals(chapters[i].Id, id, StringComparison.Ordinal))
                {
                    return chapters[i].Unlocked;
                }
            }

            return false;
        }

        private static int GetVisits()
        {
            try
            {
                return Math.Max(0, ModHelper.GetAfterhourVisits(LabLocationId));
            }
            catch
            {
                return 0;
            }
        }
    }
}
