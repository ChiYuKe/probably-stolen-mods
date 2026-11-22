using System.Collections.Generic;
using HarmonyLib;

namespace Chemistry
{
    [HarmonyPatch(typeof(GameItemElement), "UpdateInnateModSprite")]
    internal static class LiquidLayerVisibility
    {
        private static void Postfix(GameItemElement __instance, int __0,
            List<TreeNodeRender> ___innateSpriteMods, List<TreeNodeRender> ___innateFakeSpriteMods)
        {
            string id = __instance.identifier;
            if (!ReagentBottle.IsLiquid(id) && !ReagentBottle.IsLiquidEmpty(id))
            {
                return;
            }

            FollowBackground(___innateSpriteMods[__0], __instance.background);
            FollowBackground(___innateFakeSpriteMods[__0], __instance.fakeBackground);
        }

        private static void FollowBackground(TreeNodeRender layer, TreeNodeRender background)
        {
            layer.image.enabled = background.image.enabled;
            var color = layer.image.color;
            color.a *= background.image.color.a;
            layer.image.color = color;
        }
    }
}
