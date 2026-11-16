using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeteaseCassette
{
    // 播放模式：顺序 / 单曲循环 / 随机
    public static class PlaybackMode
    {
        public const int Sequential = 0;
        public const int Loop = 1;      // 单曲循环：同一首重来
        public const int Shuffle = 2;

        private const string FileName = "player.json";

        private static bool loaded;
        private static int mode = Loop;

        public static int Mode
        {
            get
            {
                EnsureLoaded();
                return mode;
            }
        }

        public static void Set(int value)
        {
            mode = Mathf.Clamp(value, 0, 2);
            loaded = true;
            try
            {
                ModHelper.WriteModDataFileText(ModEntry.ModId, FileName, mode.ToString());
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("存播放模式失败：" + e.Message);
            }
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;

            try
            {
                string text = ModHelper.ReadModDataFileText(ModEntry.ModId, FileName);
                int parsed;
                if (!string.IsNullOrEmpty(text) && int.TryParse(text.Trim(), out parsed))
                {
                    mode = Mathf.Clamp(parsed, 0, 2);
                }
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("读播放模式失败：" + e.Message);
            }
        }

        // auto = 一首自然放完；false = 用户点了下一首。
        // 返回 true 就交回原版，false 表示我们已经自己处理了。
        public static bool HandleAdvance(bool auto, GameItem machine, GameGridInventory deck)
        {
            if (machine == null && EmporiumEntry.Instance != null) machine = EmporiumEntry.Instance.cassettePlayer;
            if (deck == null) deck = MachineCassettePlayer.GetCassetteInventory();
            if (machine == null || deck == null) return true;

            if (machine.GetTagReadonly(MachineCassettePlayer.CASETTE_PLAYER_STATE_INT).GetInt() == 0) return true;

            List<string> tracks = MachineCassettePlayer.GetTrackListFromCassette(deck);
            if (tracks.Count == 0) return true;

            int current = machine.GetTagReadonly(MachineCassettePlayer.CURRENT_PLAYING_CASSETTE_INDEX_TAG).GetInt();
            if (current < 0 || current >= tracks.Count) current = Mathf.Clamp(current, 0, tracks.Count - 1);

            if (Mode == Loop)
            {
                // 手动点下一首就正常切歌，只有自然放完才重来这一首
                if (!auto) return true;
                Log(machine, tracks, current, "单曲循环");
                Play(machine, tracks, current);
                return false;
            }

            if (Mode == Shuffle)
            {
                if (tracks.Count <= 1) return true;
                int next;
                do
                {
                    next = UnityEngine.Random.Range(0, tracks.Count);
                }
                while (next == current);

                Log(machine, tracks, next, "随机");
                Play(machine, tracks, next);
                return false;
            }

            // 顺序：放到最后一首就停
            int following = current + 1;
            if (following >= tracks.Count)
            {
                StopAtEnd(machine);
                return false;
            }

            Log(machine, tracks, following, "顺序");
            Play(machine, tracks, following);
            return false;
        }

        private static void Log(GameItem machine, List<string> tracks, int index, string what)
        {
            ModEntry.Log?.Log("切歌：" + what + " -> 第 " + (index + 1) + "/" + tracks.Count + " 盘");
        }

        // 跟原版 PlayTrackAtIndex 做一样的事（那个是私有的，只能自己来）
        private static void Play(GameItem machine, List<string> tracks, int index)
        {
            machine.ModifyTag(MachineCassettePlayer.CURRENT_PLAYING_CASSETTE_INDEX_TAG, tag => tag.Enable().SetInt(index));
            machine.ModifyTag(MachineCassettePlayer.CURRENT_PLAYING_TRACK_ID_TAG, tag => tag.Enable().SetString(tracks[index]));
            machine.ModifyTag(MachineCassettePlayer.CASETTE_PLAYER_STATE_INT, tag => tag.Enable().SetInt(1));
            AudioManager.Instance.PlayMusic(tracks[index]);
            AudioManager.Instance.PlayAndCallback(AudioManager.OnTrackDone);
            RefreshFrames(machine);
        }

        private static void StopAtEnd(GameItem machine)
        {
            machine.ModifyTag(MachineCassettePlayer.CASETTE_PLAYER_STATE_INT, tag => tag.Enable().SetInt(0));
            AudioManager.StopAudioCoroutine();
            AudioManager.Instance.StopMusic();
            RefreshFrames(machine);
            ModEntry.Log?.Log("顺序播放：最后一首放完了");
        }

        private static void RefreshFrames(GameItem machine)
        {
            var deck = MachineHelper.GetBodyStorage(machine, 1) as GameInventory;
            if (deck == null) return;
            foreach (var item in deck.childItems)
            {
                var element = item as GameItemElement;
                if (element != null) element.RefreshPlayingFrame();
            }
        }
    }
}
