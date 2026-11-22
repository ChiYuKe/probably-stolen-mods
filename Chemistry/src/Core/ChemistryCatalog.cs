using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Chemistry.Data;

namespace Chemistry
{
    /// <summary>
    /// 模组级的固定约定：mod id、物品 id / 贴图 key 的拼法、分类显示名。
    /// 所有 id 都带 chem_ 前缀，避免和原版物品或别的模组撞车。
    /// </summary>
    internal static class ChemistryCatalog
    {
        /// <summary>和 manifest.xml 里的 ID 一致，ModHelper 的所有接口都用它。</summary>
        public const string ModId = "chemistry";

        /// <summary>物品 id 前缀。</summary>
        public const string ItemPrefix = "chem_";

        public static ElementDef[] Elements
        {
            get { return ElementTable.All; }
        }

        public static ToolDef[] Tools { get; } = ToolTable.All.Concat(LabEquipmentData.NewTools).ToArray();

        public static CompoundDef[] Compounds
        {
            get { return CompoundTable.All; }
        }

        // ---- id / 贴图 key 约定（贴图文件名必须和 SpriteKey 一模一样） ----

        public static string ElementItemId(string symbol)
        {
            return ItemPrefix + "el_" + (symbol ?? "").ToLowerInvariant();
        }

        public static string ElementSpriteKey(string symbol)
        {
            return "el_" + (symbol ?? "").ToLowerInvariant();
        }

        public static string ToolItemId(string key)
        {
            return ItemPrefix + "tool_" + key;
        }

        public static string ToolSpriteKey(string key)
        {
            return "tool_" + key;
        }

        public static string CompoundItemId(string key)
        {
            return ItemPrefix + "cmp_" + key;
        }

        public static string CompoundSpriteKey(string key)
        {
            return "cmp_" + key;
        }

        internal static string FormatFormulaForDisplay(string formula)
        {
            return Regex.Replace(formula, "[0-9]+", "<sub>$0</sub>");
        }

        // ---- 查表 ----

        public static ElementDef FindElement(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                return null;
            }

            ElementDef[] all = Elements;
            for (int i = 0; i < all.Length; i++)
            {
                if (string.Equals(all[i].Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                {
                    return all[i];
                }
            }

            return null;
        }

        public static ToolDef FindTool(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            ToolDef[] all = Tools;
            for (int i = 0; i < all.Length; i++)
            {
                if (string.Equals(all[i].Key, key, StringComparison.Ordinal))
                {
                    return all[i];
                }
            }

            return null;
        }

        public static CompoundDef FindCompound(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            CompoundDef[] all = Compounds;
            for (int i = 0; i < all.Length; i++)
            {
                if (string.Equals(all[i].Key, key, StringComparison.Ordinal))
                {
                    return all[i];
                }
            }

            return null;
        }

        public static ElementDef FindElementByItemId(string itemId)
        {
            ElementDef[] all = Elements;
            for (int i = 0; i < all.Length; i++)
            {
                if (string.Equals(all[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    return all[i];
                }
            }

            return null;
        }

        // ---- 分类显示名（UI、物品描述都用这一份，别到处抄） ----

        public static string ElementCategoryLabel(string key, bool zh)
        {
            switch (key)
            {
                case "alkali_metal": return zh ? "碱金属" : "Alkali metal";
                case "alkaline_earth": return zh ? "碱土金属" : "Alkaline earth metal";
                case "transition_metal": return zh ? "过渡金属" : "Transition metal";
                case "post_transition": return zh ? "后过渡金属" : "Post-transition metal";
                case "metalloid": return zh ? "类金属" : "Metalloid";
                case "nonmetal": return zh ? "非金属" : "Nonmetal";
                case "halogen": return zh ? "卤素" : "Halogen";
                case "noble_gas": return zh ? "稀有气体" : "Noble gas";
                case "lanthanide": return zh ? "镧系" : "Lanthanide";
                case "actinide": return zh ? "锕系" : "Actinide";
                default: return zh ? "未知" : "Unknown";
            }
        }

        public static string ToolCategoryLabel(string key, bool zh)
        {
            switch (key)
            {
                case "glassware": return zh ? "玻璃器皿" : "Glassware";
                case "heating": return zh ? "加热设备" : "Heating";
                case "measurement": return zh ? "测量仪器" : "Measurement";
                case "separation": return zh ? "分离提纯" : "Separation";
                case "safety": return zh ? "安全防护" : "Safety";
                default: return zh ? "其他" : "Misc";
            }
        }

        public static string CompoundCategoryLabel(string key, bool zh)
        {
            switch (key)
            {
                case "acid": return zh ? "酸" : "Acid";
                case "base": return zh ? "碱" : "Base";
                case "salt": return zh ? "盐" : "Salt";
                case "oxide": return zh ? "氧化物" : "Oxide";
                case "organic": return zh ? "有机物" : "Organic";
                case "gas": return zh ? "气体" : "Gas";
                default: return zh ? "其他" : "Other";
            }
        }

        /// <summary>化合物掉落权重：贵重的合成物更少见。</summary>
        public static float CompoundLootWeight(CompoundDef def)
        {
            if (def == null)
            {
                return 0f;
            }

            float weight = 30f / (1f + Math.Max(0, def.Value) / 60f);
            return Math.Max(1f, weight);
        }
    }
}
