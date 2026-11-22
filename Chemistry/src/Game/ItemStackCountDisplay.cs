using System;
using HarmonyLib;

namespace Chemistry
{
    internal static class ItemStackCountDisplay
    {
        private static bool IsChemistryItem(GameItem item)
        {
            return item.identifier?.StartsWith(ChemistryCatalog.ItemPrefix, StringComparison.Ordinal) == true;
        }

        [HarmonyPatch(typeof(GameItemElement), "ValidateSelectLocked")]
        private static class InventoryItem
        {
            private static void Postfix(GameItemElement __instance,
                TreeNodeRender ___stackCountText, TreeNodeRender ___stackCountTextBackground)
            {
                InventoryFormulaDisplay.Refresh(__instance, __instance.unitCount);
                if (IsChemistryItem(__instance) && string.IsNullOrEmpty(__instance.customText))
                {
                    ___stackCountText.text.enabled = false;
                    ___stackCountTextBackground.text.enabled = false;
                }
            }
        }

        [HarmonyPatch(typeof(GameItemElement), nameof(GameItemElement.UpdateFakeReal))]
        private static class DragPreview
        {
            private static void Postfix(GameItemElement __instance,
                TreeNodeRender ___stackCountText, TreeNodeRender ___stackCountTextBackground,
                TreeNodeRender ___fakeStackCountText, TreeNodeRender ___fakeStackCountTextBackground, int numPickedUp)
            {
                InventoryFormulaDisplay.Refresh(__instance, __instance.unitCount - numPickedUp);
                if (IsChemistryItem(__instance))
                {
                    ___stackCountText.text.enabled = false;
                    ___stackCountTextBackground.text.enabled = false;
                    ___fakeStackCountText.text.enabled = false;
                    ___fakeStackCountTextBackground.text.enabled = false;
                }
            }
        }
    }
}
