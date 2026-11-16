using System;
using HarmonyLib;
using UnityEngine;

namespace NeteaseCassette
{
    // 磁带机窗口多一个「云」按钮，点了开扫码/点歌窗口
    [HarmonyPatch(typeof(MachineCassettePlayer), nameof(MachineCassettePlayer.CassettePlayer))]
    internal static class CassettePlayerPatch
    {
        private static void Postfix(ref GameItem __result)
        {
            try
            {
                CloudWindow.AddCloudPanel(__result);
            }
            catch (Exception e)
            {
                ModEntry.Log?.Error("给磁带机加网易云面板失败：" + e);
            }
        }
    }

    // netease:xxx 不走音频表，交给 StreamPlayer 现拉
    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.PlayMusic))]
    internal static class PlayMusicPatch
    {
        private static bool Prefix(string identifier)
        {
            try
            {
                if (StreamPlayer.IsCloudTrack(identifier))
                {
                    StreamPlayer.PlayCloud(identifier);
                    return false;
                }
                StreamPlayer.Cancel();
            }
            catch (Exception e)
            {
                ModEntry.Log?.Error("拦截 PlayMusic 时出错：" + e);
            }
            return true;
        }
    }

    // 音频表里没云曲目，这里直接说能放，不然读档会把播放状态清掉
    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.CanPlay))]
    internal static class CanPlayPatch
    {
        private static bool Prefix(string identifier, ref bool __result)
        {
            if (!StreamPlayer.IsCloudTrack(identifier))
            {
                return true;
            }
            __result = true;
            return false;
        }
    }

    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.StopMusic))]
    internal static class StopMusicPatch
    {
        private static void Prefix()
        {
            try
            {
                StreamPlayer.Cancel();
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("停止云曲目时出错：" + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.PauseMusic))]
    internal static class PauseMusicPatch
    {
        private static void Prefix()
        {
            try
            {
                StreamPlayer.CancelPending();
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("暂停云曲目时出错：" + e.Message);
            }
        }
    }

    // 存档里的贴图键找不到时兜个底：把那个键直接指到一张现成的卡带图上，
    // 不然改过贴图命名的存档会满屏问号。
    [HarmonyPatch(typeof(GameItemElement), nameof(GameItemElement.ResolveSpriteByName))]
    internal static class ModSpriteFallbackPatch
    {
        private static bool Prefix(GameItemElement __instance, string name, ref Sprite __result)
        {
            if (!__instance.useSpriteFromMod) return true;
            if (__instance.spriteAtlasPath != ModEntry.ModId) return true;

            var mod = ModLoader.Mods.Find(m => m.Manifest.ID == ModEntry.ModId);
            if (mod == null || mod.Sprites.ContainsKey(name)) return true;

            Sprite replacement;
            if (!ModEntry.TryPickTapeSpriteFor(name, out replacement)) return true;
            mod.Sprites[name] = replacement;
            __result = replacement;
            return false;
        }
    }
}
