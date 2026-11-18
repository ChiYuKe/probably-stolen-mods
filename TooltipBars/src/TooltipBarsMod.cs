using System;
using HarmonyLib;
using UnityEngine;

namespace TooltipBars
{
    /// <summary>
    /// 把工具提示里的 [====] 文字进度条绘制成像素刻度条。
    /// </summary>
    public class TooltipBarsMod : IMod
    {
        internal static ModLog Log;

        private ConfigEntry<bool> solidBarEntry;

        public void Init(ModManifest manifest)
        {
            Log = new ModLog(manifest);
            Log.Log("装好了，游戏版本 " + Application.version);

            try
            {
                ModConfig config = ModHelper.GetConfig(manifest.ID);
                solidBarEntry = config.Define(
                    "solid_progress_bars",
                    true,
                    "UI",
                    () => ConfigText(manifest.ID, "config_solid_bars", "实心进度条"),
                    () => ConfigText(manifest.ID, "config_solid_bars_tip",
                        "把物品提示里的 [====] 文字进度条换成真正的 UI 进度条。"));
            }
            catch (Exception e)
            {
                Log.Warning("注册配置项失败：" + e.Message);
            }
        }

        public void OnEnable()
        {
            if (solidBarEntry != null)
            {
                solidBarEntry.OnChanged += OnSolidBarChanged;
                TooltipBarOverlay.Enabled = solidBarEntry.Value;
            }
            else
            {
                TooltipBarOverlay.Enabled = true;
            }
        }

        public void OnDisable()
        {
            if (solidBarEntry != null)
            {
                solidBarEntry.OnChanged -= OnSolidBarChanged;
            }

            TooltipBarOverlay.Enabled = false;
        }

        private static void OnSolidBarChanged(bool value)
        {
            TooltipBarOverlay.Enabled = value;
        }

        private static string ConfigText(string modId, string key, string fallback)
        {
            string text = ModHelper.GetLocalized(modId, key);
            return string.IsNullOrEmpty(text) || text == key ? fallback : text;
        }
    }
}
