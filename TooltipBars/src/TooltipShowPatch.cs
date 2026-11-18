using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace TooltipBars
{
    // 工具提示用的是 RichTextElement，显示时走 OverlayHandler.ShowTooltip；
    // 挂上组件后由它在 LateUpdate 里绘制进度条。
    [HarmonyPatch(typeof(OverlayHandler))]
    [HarmonyPatch("ShowTooltip", new Type[] { typeof(Vector2), typeof(Vector2), typeof(RichText), typeof(int) })]
    internal static class TooltipShowPatch
    {
        private static readonly FieldInfo TooltipElementField = typeof(OverlayHandler).GetField(
            "tooltipElement", BindingFlags.Instance | BindingFlags.NonPublic);

        private static void Postfix(OverlayHandler __instance)
        {
            if (__instance == null || TooltipElementField == null)
            {
                return;
            }

            try
            {
                RichTextElement element = TooltipElementField.GetValue(__instance) as RichTextElement;
                TooltipBarOverlay.Attach(element);
            }
            catch (Exception e)
            {
                TooltipBarsMod.Log?.Warning("给工具提示挂进度条失败：" + e.Message);
            }
        }
    }
}
