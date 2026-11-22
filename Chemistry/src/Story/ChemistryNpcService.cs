using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Chemistry
{
    /// <summary>Queues the Chemistry story visitors in the normal shop customer flow.</summary>
    internal static class ChemistryNpcService
    {
        private const string StageKey = "npc_story_stage";
        private const string SpritePrefix = "chem_npc_";
        private static readonly List<UnityEngine.Object> ownedAssets = new List<UnityEngine.Object>();
        private static bool spritesRegistered;

        private sealed class NpcStage
        {
            public string Id;
            public string NameZh;
            public string NameEn;
            public string SpriteFile;
            public string Chapter;
            public string[] LinesZh;
            public string[] LinesEn;
        }

        private static readonly NpcStage[] Stages =
        {
            new NpcStage
            {
                Id = "mentor", NameZh = "沈老师", NameEn = "Dr. Shen", SpriteFile = "npc_chemist_mentor.png", Chapter = "ch0",
                LinesZh = new[] { "地下室的灯亮了？看来你真的找到那张旧工作台了。", "先别急着炼东西。把废料放进分析仪，弄清成分，再决定怎么处理。", "记住：每一份记录都留着。有人曾经靠这些配方活下来。" },
                LinesEn = new[] { "The basement lights are on? So you really found the old bench.", "Don't rush into refining. Analyze scrap first, learn what's in it, then choose a process.", "Keep every record. Someone once survived by keeping these recipes." }
            },
            new NpcStage
            {
                Id = "scrap_collector", NameZh = "老锈", NameEn = "Old Rust", SpriteFile = "npc_scrap_collector.png", Chapter = "ch1",
                LinesZh = new[] { "你已经做出第一批东西了？那我这儿的破烂也许就不是破烂了。", "拆船场、旧电池堆和烧毁的设备里，常能翻到混着金属的废料。", "你把成分报告带来，我就知道该替你留哪一堆。" },
                LinesEn = new[] { "You've made your first batch? Then maybe my junk isn't junk to you.", "Shipbreakers, dead battery piles and burned-out machines are good sources of metal-bearing scrap.", "Bring me an assay report and I'll know which pile to save for you." }
            },
            new NpcStage
            {
                Id = "lab_mechanic", NameZh = "阿栓", NameEn = "Latch", SpriteFile = "npc_lab_mechanic.png", Chapter = "ch2",
                LinesZh = new[] { "酸做出来了？先把排风和耐腐蚀的容器检查一遍。", "我把蒸馏器的密封圈换成了旧潜水服的复合胶，至少不会一加热就漏。", "设备要是卡住，先看温度、压力和容器余量，别拿扳手敲玻璃。" },
                LinesEn = new[] { "You made acid? Check the exhaust and corrosion-proof vessels first.", "I replaced the still's seals with composite rubber from an old dive suit. It should hold under heat.", "If a machine stalls, check heat, pressure and vessel capacity before reaching for a wrench." }
            },
            new NpcStage
            {
                Id = "reagent_broker", NameZh = "白鸦", NameEn = "White Crow", SpriteFile = "npc_reagent_broker.png", Chapter = "ch4",
                LinesZh = new[] { "你已经碰过危险配方了。以后来路不明的试剂，别直接倒进反应釜。", "DSC-07 最近在收购旧实验室的存货，连废料桶都不放过。", "我可以替你打听原料，但每笔交易都会留下痕迹。想清楚再开口。" },
                LinesEn = new[] { "You've handled a hazardous recipe. Never pour an unknown reagent straight into a reactor.", "DSC-07 has been buying up old lab stock lately, even the waste drums.", "I can find reagents for you, but every deal leaves a trail. Think before you ask." }
            }
        };

        public static void QueueForDay(StoreClientManager manager)
        {
            if (manager == null || PlayerStore.Instance == null) return;

            try
            {
                EnsureSpritesRegistered();
                int stage = ModHelper.GetModDataInt(ChemistryCatalog.ModId, StageKey, 0);
                if (stage < 0 || stage >= Stages.Length) return;

                NpcStage npc = Stages[stage];
                if (!IsChapterUnlocked(npc.Chapter)) return;

                StoreClient client = CreateClient(npc, stage);
                ModHelper.SetClientSource(client, ChemistryCatalog.ModId);
                manager.TryAddClient(client, true);
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("Chemistry NPC could not be queued: " + e.Message);
            }
        }

        public static void Shutdown()
        {
            for (int i = ownedAssets.Count - 1; i >= 0; i--)
            {
                if (ownedAssets[i] != null) UnityEngine.Object.Destroy(ownedAssets[i]);
            }
            ownedAssets.Clear();
            spritesRegistered = false;
        }

        private static bool IsChapterUnlocked(string id)
        {
            IList<StoryChapter> chapters = StoryService.Chapters;
            for (int i = 0; i < chapters.Count; i++)
                if (chapters[i].Id == id) return chapters[i].Unlocked;
            return false;
        }

        private static StoreClient CreateClient(NpcStage npc, int stage)
        {
            var client = new StoreClient
            {
                identifier = "chemistry_story_" + npc.Id,
                displayName = Loc.Pick(npc.NameZh, npc.NameEn),
                spriteName = SpritePrefix + npc.Id,
                clientIntent = StoreClient.ClientIntent.DIALOGUE,
                dismissable = false,
                nonShootable = true
            };

            for (int i = 0; i < npc.LinesZh.Length; i++)
            {
                if (i > 0) client.mainDialogue.NextDialogue();
                client.mainDialogue.SetText(client.displayName, Loc.Pick(npc.LinesZh[i], npc.LinesEn[i]));
            }
            client.mainDialogue.SetEndAction(() =>
            {
                ModHelper.SetModData(ChemistryCatalog.ModId, StageKey, (stage + 1).ToString());
                if (PlayerStore.Instance != null) PlayerStore.Instance.DismissCurrentClient(false, true, true);
            });
            client.CompleteClientCreation(false);
            return client;
        }

        private static void EnsureSpritesRegistered()
        {
            if (spritesRegistered || SpriteDict.Instance == null) return;

            string folder = ModHelper.GetModFolder(ChemistryCatalog.ModId);
            if (string.IsNullOrEmpty(folder)) return;
            string characters = Path.Combine(folder, "Textures", "Characters");
            var entries = new List<SpriteDict.SpriteEntry>();

            for (int i = 0; i < Stages.Length; i++)
            {
                NpcStage npc = Stages[i];
                string path = Path.Combine(characters, npc.SpriteFile);
                if (!File.Exists(path))
                {
                    ChemistryMod.Log?.Warning("NPC portrait missing: " + path);
                    continue;
                }

                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.name = SpritePrefix + npc.Id + "_texture";
                texture.filterMode = FilterMode.Point;
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path), true))
                {
                    UnityEngine.Object.Destroy(texture);
                    continue;
                }

                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = SpritePrefix + npc.Id;
                entries.Add(new SpriteDict.SpriteEntry { key = SpritePrefix + npc.Id, sprite = sprite });
                ownedAssets.Add(texture);
                ownedAssets.Add(sprite);
            }

            if (entries.Count > 0) SpriteDict.Instance.ProcessEntriesToDict(entries);
            spritesRegistered = entries.Count == Stages.Length;
        }
    }
}
