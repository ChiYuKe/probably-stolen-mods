using System;

namespace InventorySearch
{
    /// <summary>
    /// 模组自己的文案查询：优先走 Localization/*.csv，缺词条时按当前语言回落到写死在代码里的中/英。
    /// </summary>
    internal static class Loc
    {
        public static string ModId = "inventory_search";

        private static bool? chinese;

        public static bool IsChinese
        {
            get
            {
                if (!chinese.HasValue)
                {
                    try
                    {
                        string code = LocHelper.GetCurrentLocaleCode();
                        chinese = string.IsNullOrEmpty(code) || code.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        chinese = true;
                    }
                }

                return chinese.Value;
            }
        }

        public static string Get(string key, string zh, string en, params object[] args)
        {
            try
            {
                string localized = ModHelper.GetLocalized(ModId, key, args);
                if (!string.IsNullOrEmpty(localized) && localized != key)
                {
                    return localized;
                }
            }
            catch
            {
                // 本地化表还没加载好，直接走兜底
            }

            string text = IsChinese ? zh : en;
            if (args != null && args.Length > 0)
            {
                try
                {
                    text = string.Format(text, args);
                }
                catch (FormatException)
                {
                    // 文案里的花括号不合法就原样返回
                }
            }

            return text;
        }

        /// <summary>切场景/读档后语言可能变，让下一次查询重新判断。</summary>
        public static void ResetLocale()
        {
            chinese = null;
        }
    }
}
