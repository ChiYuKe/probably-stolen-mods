using System;
using HarmonyLib;
using UnityEngine;

namespace ManualSave
{
    public sealed class ManualSaveMod : IMod
    {
        private Harmony harmony;
        private ModLog log;

        public void Init(ModManifest manifest)
        {
            log = new ModLog(manifest);
            log.Log("手动存档已加载，游戏版本 " + Application.version);
        }

        public void OnEnable()
        {
            try
            {
                harmony = new Harmony("com.liukeyu.probablystolen.manualsave");
                harmony.PatchAll(typeof(ManualSaveMod).Assembly);
            }
            catch (Exception e)
            {
                log?.Error("安装暂停菜单按钮失败：" + e);
            }
        }

        public void OnDisable()
        {
            harmony?.UnpatchAll("com.liukeyu.probablystolen.manualsave");
            harmony = null;
        }
    }
}
