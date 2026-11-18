using System;

namespace NeteaseCassette
{
    // 界面文案统一走这里。CSV 在 Localization/ 下，
    // 游戏会按当前语言查，缺了回退到 en，再缺就回退成键名本身。
    public static class Loc
    {
        public static string T(string key, params object[] args)
        {
            string text = null;
            try
            {
                text = ModHelper.GetLocalized(ModEntry.ModId, key, args);
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("取文案失败（" + key + "）：" + e.Message);
            }

            return string.IsNullOrEmpty(text) ? key : text;
        }
    }
}
