using System;
using UnityEngine;

namespace EnergyBar
{
    public class EnergyBarMod : IMod
    {
        private const string ItemId = "suspicious_energy_bar";

        // 送过没有，写存档里，换档会重新送
        private const string GivenKey = "given";

        private ModManifest manifest;
        private ModLog log;

        public void Init(ModManifest manifest)
        {
            this.manifest = manifest;
            log = new ModLog(manifest);
            log.Log("装好了，游戏版本 " + Application.version);
        }

        public void OnEnable()
        {
            ModHook.OnModItemDirectoryInit += RegisterItem;

            // 读档和开新档是两条路，各挂一次
            ModHook.OnGameLoadedLate += TryGive;
            ModHook.OnNewGameStarted += OnNewGame;

            // F9 里能自己刷，测试方便
            ModHelper.AddDebugFunction(manifest.ID, "Modding", "能量棒 +1", SpawnOne);
        }

        public void OnDisable()
        {
            ModHook.OnModItemDirectoryInit -= RegisterItem;
            ModHook.OnGameLoadedLate -= TryGive;
            ModHook.OnNewGameStarted -= OnNewGame;
            ModHelper.RemoveDebugFunctions(manifest.ID);
        }

        private void RegisterItem(ModItemDirectory dir)
        {
            dir.Add(ItemId, () =>
            {
                var item = ItemDirectory.CreateEmptyItem(ItemId);
                item.name = "味道可疑的能量棒";
                item.shortDescription = "包装上的保质期已经糊了，闻起来还行。";
                item.unitBaseValue = 8;
                item.SetSpriteAndShapeFromMod(manifest.ID, "energy_bar");
                return item;
            });

            log.Log("物品目录初始化，能量棒已注册");
        }

        private void OnNewGame(string scenarioId)
        {
            TryGive();
        }

        private void TryGive()
        {
            try
            {
                if (ModHelper.GetModDataBool(manifest.ID, GivenKey))
                {
                    return;
                }

                var item = ModHelper.SpawnItem(ItemId);
                if (item == null)
                {
                    log.Warning("能量棒没生成出来，多半是物品目录那边没注册上");
                    return;
                }

                ModHelper.MoveItemToInventory(item);
                ModHelper.SetModDataBool(manifest.ID, GivenKey, true);
                log.Log("背包里塞了一根能量棒");
            }
            catch (Exception e)
            {
                log.Error("送能量棒的时候崩了：" + e);
            }
        }

        private void SpawnOne()
        {
            var item = ModHelper.SpawnItem(ItemId);
            if (item != null)
            {
                ModHelper.MoveItemToInventory(item);
            }
        }
    }
}
