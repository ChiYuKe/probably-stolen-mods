using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Chemistry
{
    internal static class InventoryFormulaDisplay
    {
        private static readonly Dictionary<string, string> Formulas =
            ChemistryCatalog.Compounds.Select(compound => (compound.ItemId,
                    Text: ChemistryCatalog.FormatFormulaForDisplay(compound.Formula)))
                .Concat(ChemistryCatalog.Elements.Select(element => (element.ItemId, Text: element.Symbol)))
                .ToDictionary(entry => entry.ItemId, entry => entry.Text);

        private static readonly ConditionalWeakTable<GameItemElement, TextMeshProUGUI> Labels =
            new ConditionalWeakTable<GameItemElement, TextMeshProUGUI>();
        private static readonly Dictionary<Material, Material> OutlineMaterials = new Dictionary<Material, Material>();

        internal static void Refresh(GameItemElement item, int visibleUnits)
        {
            if (item.parentInventory == null || item.isDebugMenu || visibleUnits <= 0 || item.identifier == null
                || !Formulas.TryGetValue(item.identifier, out string formula) || string.IsNullOrEmpty(formula))
            {
                Hide(item);
                return;
            }

            TextMeshProUGUI label = Labels.GetValue(item, CreateLabel);
            int gridSize = UISettings.current.gridSize;
            GridShape shape = item.modifiedShape;
            label.rectTransform.sizeDelta = new Vector2((Mathf.Min(2f, shape.globalWidth) - 0.12f) * gridSize, gridSize * 0.68f);
            label.rectTransform.anchoredPosition = new Vector2(shape.globalWidth * gridSize * 0.5f,
                -(shape.globalHeight - 0.36f) * gridSize);
            label.fontSizeMax = gridSize * 0.56f;
            label.text = formula;
            label.enabled = true;
        }

        private static void Hide(GameItem item)
        {
            if (item is GameItemElement element && Labels.TryGetValue(element, out TextMeshProUGUI label)
                && label != null)
            {
                label.enabled = false;
            }
        }

        [HarmonyPatch(typeof(GameItem), nameof(GameItem.RemoveParent))]
        private static class RemoveItemFromInventory
        {
            private static void Postfix(GameItem __instance, bool __result)
            {
                if (__result)
                {
                    Hide(__instance);
                }
            }
        }

        private static TextMeshProUGUI CreateLabel(GameItemElement item)
        {
            // The middle node follows the slot without rotating with the item sprite.
            TreeNodeRender node = TreeNodeRender.InstantiateText().SetParent(item.middle);
            Vector2 topLeft = new Vector2(0f, 1f);
            node.rectTransform.anchorMin = node.rectTransform.anchorMax = topLeft;
            node.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            TextMeshProUGUI label = node.text;
            label.margin = Vector4.zero;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color32(255, 227, 149, 255);
            ApplyOutline(label);
            label.richText = true;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Truncate;
            label.enableAutoSizing = true;
            label.fontSizeMin = 1f;
            return label;
        }

        private static void ApplyOutline(TextMeshProUGUI label)
        {
            Material source = label.fontSharedMaterial;
            if (source == null || !source.HasProperty("_OutlineWidth") || !source.HasProperty("_OutlineColor"))
            {
                return;
            }
            if (!OutlineMaterials.TryGetValue(source, out Material outline))
            {
                outline = new Material(source);
                outline.SetFloat("_OutlineWidth", 0.16f);
                outline.SetColor("_OutlineColor", new Color32(25, 21, 29, 255));
                outline.EnableKeyword("OUTLINE_ON");
                OutlineMaterials.Add(source, outline);
            }
            label.fontSharedMaterial = outline;
        }
    }
}
