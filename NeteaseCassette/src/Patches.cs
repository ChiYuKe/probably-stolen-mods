using System.Collections.Generic;
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
    // 手动点「下一首」走这里：单曲循环下不拦，正常切歌
    [HarmonyPatch(typeof(MachineCassettePlayer), nameof(MachineCassettePlayer.OnNextButtonPressed))]
    internal static class NextButtonPatch
    {
        private static bool Prefix(GameItem machine, GameGridInventory cassetteInventory)
        {
            try
            {
                return PlaybackMode.HandleAdvance(false, machine, cassetteInventory);
            }
            catch (Exception e)
            {
                ModEntry.Log?.Error("按播放模式切歌出错：" + e);
                return true;
            }
        }
    }

    // 一首放完走这里：单曲循环要重来同一首，所以拦下原版的「下一首」
    [HarmonyPatch(typeof(AudioManager), nameof(AudioManager.OnTrackDone))]
    internal static class TrackDonePatch
    {
        private static bool Prefix()
        {
            try
            {
                ModEntry.Log?.Log("曲目放完了，按播放模式处理（模式=" + PlaybackMode.Mode + "）");
                return PlaybackMode.HandleAdvance(true, null, null);
            }
            catch (Exception e)
            {
                ModEntry.Log?.Error("自动切歌出错：" + e);
                return true;
            }
        }
    }
    // 磁带仓里的容器（收纳盒）里的磁带也算曲目，放进仓里就能直接播
    [HarmonyPatch(typeof(MachineCassettePlayer), nameof(MachineCassettePlayer.GetTrackListFromCassette))]
    internal static class TrackListPatch
    {
        private static void Postfix(GameGridInventory cassettes, ref List<string> __result)
        {
            try
            {
                CassetteScan.CollectTracks(cassettes, __result);
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("扫描容器里的磁带出错：" + e.Message);
            }
        }
    }

    // 磁带机里的容器（收纳盒）放的那盘也要绿框
    [HarmonyPatch(typeof(CassetteHelper), nameof(CassetteHelper.GetPlayingFrameColor))]
    internal static class CaseFrameColorPatch
    {
        private static bool Prefix(GameItem item, ref Color? __result)
        {
            try
            {
                Color? color = CaseFrame.ContainerColor(item);
                if (color.HasValue)
                {
                    __result = color;
                    return false;
                }
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("判断盒子播放框出错：" + e.Message);
            }
            return true;
        }
    }

    // 磁带机的储藏格和磁带仓只收带「磁带」标签的东西，给收纳盒也开个口子
    [HarmonyPatch(typeof(MachineCassettePlayer), nameof(MachineCassettePlayer.CassettePlayer))]
    internal static class CassetteFilterPatch
    {
        private static void Postfix(GameItem __result)
        {
            try
            {
                AllowCase(__result, 0);
                AllowCase(__result, 1);
            }
            catch (Exception e)
            {
                ModEntry.Log?.Error("放行收纳盒出错：" + e);
            }
        }

        private static void AllowCase(GameItem machine, int index)
        {
            if (machine == null) return;

            var grid = MachineHelper.GetBodyStorage(machine, index) as GameGridInventory;
            if (grid == null) return;

            Func<GameItem, GameInventory, bool> original = grid.mayInventoryAddItemFunc;
            grid.mayInventoryAddItemFunc = (item, inventory) =>
            {
                if (item != null && item.identifier == ModEntry.CaseItemId)
                {
                    return GeneralHelper.IsItemOwned(item);
                }
                return original == null || original(item, inventory);
            };
        }
    }
}