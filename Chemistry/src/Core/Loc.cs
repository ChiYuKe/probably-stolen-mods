using System;

namespace Chemistry
{
    /// <summary>
    /// 模组自己的文案查询：优先走 Localization/*.csv，缺词条时按当前语言回落到写死在代码里的中/英。
    /// 物品名/描述不走这里，直接存在数据表里（中英文各一份），生成物品时挑一份。
    /// </summary>
    internal static class Loc
    {
        public static string ModId = ChemistryCatalog.ModId;

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

        /// <summary>物品数据表里中英各存一份，这里按当前语言挑一份。</summary>
        public static string Pick(string zh, string en)
        {
            if (IsChinese)
            {
                return string.IsNullOrEmpty(zh) ? en : zh;
            }

            return string.IsNullOrEmpty(en) ? zh : en;
        }
    }
}
