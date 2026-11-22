using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chemistry
{
    /// <summary>
    /// 实验室面板的显示信息：把 chem_ 物品 id 反查成数据表里的名字 / 贴图 key / 分类。
    /// 查不到时退回原 id（只显示文字），绝不因为数据缺失抛异常。
    /// </summary>
    internal sealed class LabItemInfo
    {
        public string ItemId;
        public string Name;
        public string SpriteKey;
        public string Category;
        public bool Known;
    }

    /// <summary>
    /// 实验室界面的小工具：物品反查、店内可存物品枚举、通用控件样式。
    /// 和其它界面一样，所有游戏侧调用都包 try/catch——主菜单、空数据、贴图缺失最多降级。
    /// </summary>
    internal static class LabUi
    {
        /// <summary>面板内容的目标宽度（窗口 1000 减去 meta 边框后的近似可用宽度）。</summary>
        public const float ContentWidth = 880f;

        /// <summary>列表行宽度，比内容宽度略窄，避免滚动条压住右侧按钮。</summary>
        public const float RowWidth = 860f;

        /// <summary>把物品 id 反查成显示信息；元素/工具/化合物全部按数据表来，不写死清单。</summary>
        public static LabItemInfo Resolve(string itemId)
        {
            var info = new LabItemInfo
            {
                ItemId = itemId ?? "",
                Name = string.IsNullOrEmpty(itemId) ? "?" : itemId,
                SpriteKey = null,
                Category = "",
                Known = false,
            };

            if (string.IsNullOrEmpty(itemId))
            {
                return info;
            }

            try
            {
                string bulkSprite = BulkContainers.Sprite(itemId);
                if (bulkSprite != null)
                {
                    info.Name = BulkContainers.Name(itemId);
                    info.SpriteKey = bulkSprite;
                    info.Category = "other";
                    info.Known = true;
                    return info;
                }
                if (ReagentBottle.IsEmpty(itemId))
                {
                    info.Name = ReagentBottle.EmptyName(itemId);
                    info.SpriteKey = itemId.Substring(ChemistryCatalog.ItemPrefix.Length);
                    info.Category = "other";
                    info.Known = true;
                    return info;
                }
                if (ReagentBottle.IsDamaged(itemId))
                {
                    info.Name = ReagentBottle.DamagedNameFor(itemId);
                    info.SpriteKey = ReagentBottle.DamagedSpriteFor(itemId);
                    info.Category = "other";
                    info.Known = true;
                    return info;
                }
                LabRefining.Intermediate intermediate = LabRefining.FindIntermediate(itemId);
                if (intermediate != null)
                {
                    info.Name = intermediate.Name;
                    info.SpriteKey = intermediate.SpriteKey;
                    info.Category = "other";
                    info.Known = true;
                    return info;
                }

                SampleDef sample = SampleTable.Find(itemId);
                if (sample != null)
                {
                    info.Name = sample.DisplayName(itemId == sample.DetectedId);
                    info.SpriteKey = sample.SpriteKey;
                    info.Category = "transition_metal";
                    info.Known = true;
                    return info;
                }

                ElementDef element = ChemistryCatalog.FindElementByItemId(itemId);
                if (element != null)
                {
                    string symbol = string.IsNullOrEmpty(element.Symbol) ? "?" : element.Symbol;
                    string name = Loc.Pick(element.NameZh, element.NameEn);
                    info.Name = string.IsNullOrEmpty(name) ? symbol : name + " (" + symbol + ")";
                    info.SpriteKey = element.SpriteKey;
                    info.Category = element.Category;
                    info.Known = true;
                    return info;
                }

                ToolDef[] tools = ChemistryCatalog.Tools;
                if (tools != null)
                {
                    for (int i = 0; i < tools.Length; i++)
                    {
                        ToolDef tool = tools[i];
                        if (tool == null || !string.Equals(tool.ItemId, itemId, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        string name = Loc.Pick(tool.NameZh, tool.NameEn);
                        info.Name = string.IsNullOrEmpty(name)
                            ? (string.IsNullOrEmpty(tool.Key) ? itemId : tool.Key)
                            : name;
                        info.SpriteKey = tool.SpriteKey;
                        info.Category = tool.Category;
                        info.Known = true;
                        return info;
                    }
                }

                CompoundDef[] compounds = ChemistryCatalog.Compounds;
                if (compounds != null)
                {
                    for (int i = 0; i < compounds.Length; i++)
                    {
                        CompoundDef compound = compounds[i];
                        if (compound == null || !string.Equals(compound.ItemId, itemId, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        string name = Loc.Pick(compound.NameZh, compound.NameEn);
                        if (string.IsNullOrEmpty(name))
                        {
                            name = string.IsNullOrEmpty(compound.Key) ? itemId : compound.Key;
                        }

                        string formula = string.IsNullOrEmpty(compound.Formula) ? "?" : compound.Formula;
                        info.Name = name + " (" + formula + ")";
                        info.SpriteKey = compound.SpriteKey;
                        info.Category = compound.Category;
                        info.Known = true;
                        return info;
                    }
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("反查化学物品显示信息失败（" + itemId + "）：" + e.Message);
            }

            return info;
        }

        /// <summary>
        /// 从店内库存快照里收集「实验室收得下」的物品，顺序固定：元素 → 工具 → 化合物 → 兜底。
        /// 全程按数据表枚举，不写死任何物品清单；数量为 0 或不在表里的 id 会被跳过。
        /// </summary>
        public static List<KeyValuePair<string, int>> StorableItems(IDictionary<string, int> counts)
        {
            var result = new List<KeyValuePair<string, int>>();
            if (counts == null)
            {
                return result;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                ElementDef[] elements = ChemistryCatalog.Elements;
                if (elements != null)
                {
                    for (int i = 0; i < elements.Length; i++)
                    {
                        AddStorable(result, seen, counts, elements[i] != null ? elements[i].ItemId : null);
                    }
                }

                ToolDef[] tools = ChemistryCatalog.Tools;
                if (tools != null)
                {
                    for (int i = 0; i < tools.Length; i++)
                    {
                        AddStorable(result, seen, counts, tools[i] != null ? tools[i].ItemId : null);
                    }
                }

                CompoundDef[] compounds = ChemistryCatalog.Compounds;
                if (compounds != null)
                {
                    for (int i = 0; i < compounds.Length; i++)
                    {
                        AddStorable(result, seen, counts, compounds[i] != null ? compounds[i].ItemId : null);
                    }
                }

                // 兜底：前缀合法但不在数据表里的 id（正常不会出现，别把玩家的东西藏掉）
                foreach (KeyValuePair<string, int> pair in counts)
                {
                    AddStorable(result, seen, counts, pair.Key);
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("枚举店里可存入实验室的化学物品失败：" + e.Message);
            }

            return result;
        }

        private static void AddStorable(List<KeyValuePair<string, int>> result, HashSet<string> seen, IDictionary<string, int> counts, string itemId)
        {
            if (result == null || seen == null || counts == null || string.IsNullOrEmpty(itemId) || seen.Contains(itemId))
            {
                return;
            }

            int units;
            if (!counts.TryGetValue(itemId, out units) || units <= 0)
            {
                return;
            }

            try
            {
                if (!LabStorage.IsAllowed(itemId))
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            seen.Add(itemId);
            result.Add(new KeyValuePair<string, int>(itemId, units));
        }

        /// <summary>加一个统一风格的文案控件。height 传 -1 表示高度交给 TMP 自适应（档案正文用）。</summary>
        public static void Label(CustomUIBuilder builder, string text, string id, float size, float width, float height, bool wrap, Color color, bool bold, TextAlignmentOptions alignment)
        {
            if (builder == null)
            {
                return;
            }

            try
            {
                builder.AddLabel(text ?? "", id);
                CustomUIElement label = builder.Last;
                ChemUi.Style(label, size, width, height, wrap, color, bold, alignment);

                if (label != null && height <= 0f)
                {
                    // 自适应高度：清掉预制体上可能带的高度限制，并允许溢出，
                    // 免得长正文被压低成一行（meta 标签预制体的高度字段是未知的）
                    if (label.text != null && wrap)
                    {
                        label.text.overflowMode = TextOverflowModes.Overflow;
                    }

                    if (label.root != null)
                    {
                        LayoutElement layout = label.root.GetComponent<LayoutElement>();
                        if (layout == null)
                        {
                            layout = label.root.AddComponent<LayoutElement>();
                        }

                        layout.preferredHeight = -1f;
                        layout.minHeight = -1f;
                    }
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("添加实验室文案失败：" + e.Message);
            }
        }

        /// <summary>加一个固定尺寸、可控制是否可点的按钮；宽度/高度都给 0 时只做基本样式。</summary>
        public static CustomUIElement Button(CustomUIBuilder builder, string text, Action onClick, float width, float height, bool interactable, string tooltip)
        {
            if (builder == null)
            {
                return null;
            }

            try
            {
                builder.AddButton(text ?? "", onClick, null);
                CustomUIElement button = builder.Last;
                if (button == null)
                {
                    return null;
                }

                button.SetFlexible(0f, 0f);
                if (width > 0f)
                {
                    button.SetFixedWidth(width);
                }

                if (width > 0f && height > 0f)
                {
                    button.SetPreferredSize(width, height);
                }

                button.SetInteractable(interactable);
                ChemUi.StyleButtonText(button, height > 0f && height <= 26f ? 11f : 12f, true, interactable ? ChemUi.Main : ChemUi.Dim);
                if (button.text != null)
                {
                    // 这些按钮都是固定宽度的小按钮，居中比左对齐好看
                    button.text.alignment = TextAlignmentOptions.Center;
                }

                if (!string.IsNullOrEmpty(tooltip))
                {
                    button.SetTooltip(tooltip);
                }

                return button;
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("添加实验室按钮失败：" + e.Message);
                return null;
            }
        }

        /// <summary>给容器铺一层纯色底（行/格子/详情区共用）。</summary>
        public static void Paint(RectTransform rect, Color color, bool raycast)
        {
            if (rect == null)
            {
                return;
            }

            try
            {
                Image image = rect.gameObject.GetComponent<Image>();
                if (image == null)
                {
                    image = rect.gameObject.AddComponent<Image>();
                }

                image.color = color;
                image.raycastTarget = raycast;
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("给实验室控件铺底失败：" + e.Message);
            }
        }

        /// <summary>让容器在横向布局里撑满（标签页根节点、页面根节点用）。</summary>
        public static void FlexibleWidth(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            try
            {
                LayoutElement layout = rect.gameObject.GetComponent<LayoutElement>();
                if (layout == null)
                {
                    layout = rect.gameObject.AddComponent<LayoutElement>();
                }

                layout.flexibleWidth = 1f;
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("设置实验室容器宽度失败：" + e.Message);
            }
        }

        /// <summary>让容器在纵向布局里吃掉剩余高度（本面板暂时没用到，留作扩展）。</summary>
        public static void FlexibleHeight(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            try
            {
                LayoutElement layout = rect.gameObject.GetComponent<LayoutElement>();
                if (layout == null)
                {
                    layout = rect.gameObject.AddComponent<LayoutElement>();
                }

                layout.flexibleHeight = 1f;
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("设置实验室容器高度失败：" + e.Message);
            }
        }
    }
}
