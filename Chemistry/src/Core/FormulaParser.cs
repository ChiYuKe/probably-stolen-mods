using System;
using System.Collections.Generic;
using System.Globalization;

namespace Chemistry
{
    /// <summary>
    /// 把化学式解析成「元素符号 -> 原子数」，支持括号和下标数字。
    /// 例：H2O -> {H:2,O:1}；Ca(OH)2 -> {Ca:1,O:2,H:2}；C9H8O4 -> {C:9,H:8,O:4}。
    /// 只接受真实化学式写法（元素符号首字母大写、可选小写，数字表示个数），
    /// 写错会抛 FormatException——数据校验脚本会拿它检查整张表。
    /// </summary>
    internal static class FormulaParser
    {
        public static Dictionary<string, int> Parse(string formula)
        {
            if (string.IsNullOrWhiteSpace(formula))
            {
                throw new FormatException("化学式为空");
            }

            int index = 0;
            Dictionary<string, int> result = ParseGroup(formula, ref index, '\0');
            SkipSpaces(formula, ref index);
            if (index != formula.Length)
            {
                throw new FormatException("化学式 '" + formula + "' 第 " + (index + 1) + " 个字符无法解析");
            }

            return result;
        }

        public static bool TryParse(string formula, out Dictionary<string, int> counts, out string error)
        {
            counts = null;
            error = null;
            try
            {
                counts = Parse(formula);
                if (counts.Count == 0)
                {
                    error = "化学式 '" + formula + "' 里没有任何元素";
                    return false;
                }

                foreach (KeyValuePair<string, int> pair in counts)
                {
                    if (pair.Value <= 0)
                    {
                        error = "元素 " + pair.Key + " 的个数不是正数";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>
        /// 解析一串元素/括号，直到遇到 closing（外层括号）或字符串结尾。
        /// 嵌套括号时把内层结果乘上外层数字再合并。
        /// </summary>
        private static Dictionary<string, int> ParseGroup(string formula, ref int index, char closing)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();

            while (index < formula.Length)
            {
                char c = formula[index];

                if (char.IsWhiteSpace(c))
                {
                    index++;
                    continue;
                }

                if (closing != '\0' && c == closing)
                {
                    index++;
                    return counts;
                }

                if (c == '(')
                {
                    index++;
                    Dictionary<string, int> inner = ParseGroup(formula, ref index, ')');
                    int multiplier = ReadNumber(formula, ref index);
                    Merge(counts, inner, multiplier);
                    continue;
                }

                if (c == ')')
                {
                    throw new FormatException("化学式 '" + formula + "' 里有多余的右括号");
                }

                if (!char.IsUpper(c))
                {
                    throw new FormatException("化学式 '" + formula + "' 第 " + (index + 1) + " 个字符 '" + c + "' 不是元素符号开头");
                }

                int start = index;
                index++;
                while (index < formula.Length && char.IsLower(formula[index]))
                {
                    index++;
                }

                string symbol = formula.Substring(start, index - start);
                if (symbol.Length > 3)
                {
                    throw new FormatException("元素符号 '" + symbol + "' 太长");
                }

                int count = ReadNumber(formula, ref index);
                Add(counts, symbol, count);
            }

            if (closing != '\0')
            {
                throw new FormatException("化学式 '" + formula + "' 缺少 '" + closing + "'");
            }

            return counts;
        }

        private static int ReadNumber(string formula, ref int index)
        {
            int start = index;
            while (index < formula.Length && char.IsDigit(formula[index]))
            {
                index++;
            }

            if (start == index)
            {
                return 1;
            }

            string text = formula.Substring(start, index - start);
            int value;
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value <= 0)
            {
                throw new FormatException("化学式 '" + formula + "' 里的数字 '" + text + "' 不合法");
            }

            return value;
        }

        private static void Add(Dictionary<string, int> counts, string symbol, int count)
        {
            int existing;
            counts.TryGetValue(symbol, out existing);
            counts[symbol] = existing + count;
        }

        private static void Merge(Dictionary<string, int> target, Dictionary<string, int> source, int multiplier)
        {
            foreach (KeyValuePair<string, int> pair in source)
            {
                Add(target, pair.Key, pair.Value * multiplier);
            }
        }

        private static void SkipSpaces(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }
        }
    }
}
