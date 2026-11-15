using System;
using HarmonyLib;

namespace NeteaseCassette
{
    /// <summary>磁带机窗口多一块「网易云」面板，点按钮打开扫码 / 点歌窗口。</summary>
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

    /// <summary>"netease:123" 这种曲目不走 AudioDirectory，交给 StreamPlayer 现拉现放。</summary>
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

    /// <summary>AudioDirectory 里当然没有云曲目，这里直接回答“能放”，不然读档会把播放状态重置掉。</summary>
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

    /// <summary>
    /// 旧版本（0.1 早期）生成的云磁带 identifier 是 null，直接调 MusicCassette 造出来的。
    /// identifier 为 null 的物品在游戏里会到处空引用，存档也没法还原，
    /// 所以读档前先把这些节点改成注册过的云磁带 ID。
    /// </summary>
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.DecodeNodes))]
    internal static class SaveMigratePatch
    {
        private static void Prefix(System.Collections.Generic.List<SaveItemNode> savedItems)
        {
            if (savedItems == null) return;
            int fixedCount = 0;
            foreach (var node in savedItems)
            {
                if (node == null) continue;
                if (!string.IsNullOrEmpty(node.identifier)) continue;
                if (node.itemState == null || !node.itemState.IsTag(CassetteHelper.CASSETTE_TAG)) continue;
                node.identifier = ModEntry.TapeItemId;
                fixedCount++;
            }
            if (fixedCount > 0)
            {
                ModEntry.Log?.Log("读档前修正了 " + fixedCount + " 盘没登记过 ID 的旧云磁带");
            }
        }
    }
}
