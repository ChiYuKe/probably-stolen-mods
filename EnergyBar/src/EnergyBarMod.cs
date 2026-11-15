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
            ModHook.OnGameLoadedLate += OnGameLoaded;
        }

        public void OnDisable()
        {
            ModHook.OnModItemDirectoryInit -= RegisterItem;
            ModHook.OnGameLoadedLate -= OnGameLoaded;
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
        }

        private void OnGameLoaded()
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
                    log.Warning("能量棒没生成出来，看看贴图文件名对不对");
                    return;
                }

                ModHelper.MoveItemToBackCounter(item);
                ModHelper.SetModDataBool(manifest.ID, GivenKey, true);
                log.Log("放了一根能量棒在后柜台");
            }
            catch (Exception e)
            {
                log.Error("送能量棒的时候崩了：" + e);
            }
        }
    }
}
