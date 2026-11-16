using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace NeteaseCassette
{
    // 云曲目的播放：问接口要地址、下载、丢给游戏自己的音乐音源。
    // 磁带还是原版磁带，标签里写 netease:<id>，播放/暂停/下一首都走原版。
    public static class StreamPlayer
    {
        public const string Prefix = "netease:";
        private const int MaxConsecutiveFailures = 3;

        // 正在放的云曲目，0 就是没有
        public static long CurrentSongId { get; private set; }

        private static NeteaseClient client;
        private static ModLog log;

        private static int generation;
        private static UnityWebRequest current;
        private static bool clipReady;
        private static long lastSongId;
        private static AudioClip lastClip;
        private static int consecutiveFailures;

        public static void Init(NeteaseClient neteaseClient, ModLog modLog)
        {
            client = neteaseClient;
            log = modLog;
        }

        public static void Shutdown()
        {
            Cancel();
            client = null;
            log = null;
        }

        public static bool IsCloudTrack(string identifier)
        {
            return !string.IsNullOrEmpty(identifier) && identifier.StartsWith(Prefix, StringComparison.Ordinal);
        }

        public static bool TryGetSongId(string trackId, out long songId)
        {
            songId = 0L;
            return IsCloudTrack(trackId) && long.TryParse(trackId.Substring(Prefix.Length), out songId);
        }

        public static string TrackId(long songId)
        {
            return Prefix + songId.ToString();
        }

        // 拦住 AudioManager.PlayMusic 之后走这里
        public static void PlayCloud(string trackId)
        {
            long songId;
            if (!TryGetSongId(trackId, out songId))
            {
                return;
            }
            if (client == null)
            {
                Fail("网易云客户端还没准备好");
                return;
            }
            Cancel();
            CurrentSongId = songId;
            Lyrics.OnTrackStarted(songId, null);
            ModHelper.StartCoroutine(ModEntry.ModId, LoadAndPlay(songId));
        }

        // 连播时别让一串坏歌把播放器卡死
        public static void ResetFailureCounter()
        {
            consecutiveFailures = 0;
        }

        // 暂停只撤还没下载完的，已经出声的交给原版 PauseMusic
        public static void CancelPending()
        {
            if (!clipReady)
            {
                Cancel();
            }
        }

        // 停止或者换回原版磁带，彻底放手
        public static void Cancel()
        {
            generation++;
            clipReady = false;
            CurrentSongId = 0;
            Lyrics.OnStopped();
            try
            {
                if (current != null && AudioManager.Exists)
                {
                    var source = AudioManager.Instance.audioSourceMusic;
                    if (source != null)
                    {
                        source.Stop();
                        source.clip = null;
                    }
                }
            }
            catch (Exception e)
            {
                log?.Warning("停掉上一首时出错：" + e.Message);
            }
            DisposeCurrent();
        }

        private static void DisposeCurrent()
        {
            if (current == null) return;
            try
            {
                current.Abort();
            }
            catch
            {
                // 已经结束的请求 Abort 会抛，忽略
            }
            try
            {
                current.Dispose();
            }
            catch
            {
                // 同上
            }
            current = null;
        }

        private static IEnumerator LoadAndPlay(long songId)
        {
            // 外面再包一层，哪一步抛异常都要收尾，不然 AudioManager 的等待协程会一直挂着
            var inner = LoadAndPlayCore(songId);
            while (true)
            {
                bool moved;
                object current;
                try
                {
                    moved = inner.MoveNext();
                    current = moved ? inner.Current : null;
                }
                catch (Exception e)
                {
                    DisposeCurrent();
                    Fail("播放流程出错：" + e.Message);
                    yield break;
                }
                if (!moved) yield break;
                yield return current;
            }
        }

        private static IEnumerator LoadAndPlayCore(long songId)
        {
            int gen = generation;

            // 刚放完的同一首（循环、重播）直接用上次解好的，省掉重新下载那一两秒
            if (songId == lastSongId && lastClip != null)
            {
                log?.Log("复用已解码的音频 " + songId);
                yield return PlayExisting(lastClip, songId, gen);
                yield break;
            }

            string url = null;
            string urlError = null;
            yield return client.GetSongUrl(songId, u => url = u, e => urlError = e);
            if (gen != generation) yield break;
            if (string.IsNullOrEmpty(url))
            {
                Fail(string.IsNullOrEmpty(urlError) ? "拿不到播放地址（可能要会员或版权受限）" : urlError);
                yield break;
            }

            DownloadHandlerAudioClip handler = null;
            UnityWebRequest req = null;
            UnityWebRequestAsyncOperation op = null;
            try
            {
                // 跟游戏加载 mod 里 mp3 是同一条路：UnityWebRequestMultimedia + DownloadHandlerAudioClip
                req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG);
                handler = req.downloadHandler as DownloadHandlerAudioClip;
                req.timeout = 30;
                req.SetRequestHeader("User-Agent", UnityTransport.UserAgent);
                req.SetRequestHeader("Referer", "https://music.163.com/");
                op = req.SendWebRequest();
            }
            catch (Exception e)
            {
                if (req != null) req.Dispose();
                Fail("下载音频失败：" + e.Message);
                yield break;
            }

            current = req;
            while (op != null && !op.isDone)
            {
                yield return null;
            }
            if (gen != generation) yield break;

            AudioClip clip = null;
            string error = null;
            try
            {
                if (req.result == UnityWebRequest.Result.Success)
                {
                    clip = handler != null ? handler.audioClip : DownloadHandlerAudioClip.GetContent(req);
                }
                else
                {
                    error = req.error;
                }
            }
            catch (Exception e)
            {
                error = e.Message;
            }

            if (clip == null)
            {
                DisposeCurrent();
                Fail("音频解码失败：" + (string.IsNullOrEmpty(error) ? "格式不支持或下载中断" : error));
                yield break;
            }

            lastSongId = songId;
            lastClip = clip;

            yield return PlayExisting(clip, songId, gen);
        }

        // 把解码好的音频挂到游戏的音乐音源上，顺便确认它真的出声了
        private static IEnumerator PlayExisting(AudioClip clip, long songId, int gen)
        {
            AudioSource source = null;
            try
            {
                if (AudioManager.Exists)
                {
                    source = AudioManager.Instance.audioSourceMusic;
                }
            }
            catch (Exception e)
            {
                log?.Warning("拿音乐音源失败：" + e.Message);
            }

            if (source == null)
            {
                DisposeCurrent();
                Fail("找不到游戏的音乐音源");
                yield break;
            }

            source.clip = clip;
            source.Play();
            clipReady = true;
            consecutiveFailures = 0;
            log?.Log("开始播放网易云曲目 " + songId);

            // 空音频 Play() 不会出声，原版 WaitForSound 会一直等
            yield return null;
            if (gen != generation) yield break;
            if (!source.isPlaying)
            {
                Cancel();
                Fail("音频没有出声（可能是解码失败或格式不支持）");
            }
        }

        private static void Fail(string message)
        {
            consecutiveFailures++;
            CurrentSongId = 0;
            Lyrics.OnStopped();
            log?.Warning("网易云播放失败：" + message);
            try
            {
                AudioManager.StopAudioCoroutine();
                var machine = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.cassettePlayer : null;
                if (machine != null)
                {
                    machine.ModifyTag(MachineCassettePlayer.CASETTE_PLAYER_STATE_INT, (TagState tag) =>
                    {
                        tag.Enable().SetInt(0);
                    });
                    RefreshFrames(machine);
                }
            }
            catch (Exception e)
            {
                log?.Warning("失败收尾时出错：" + e.Message);
            }
            CloudWindow.ShowStatus(message);

            if (consecutiveFailures >= MaxConsecutiveFailures)
            {
                consecutiveFailures = 0;
                log?.Warning("连续几首都放不出来，先停一会儿");
            }
        }

        // 磁带上写的歌名（「音乐磁带 - XXX」里的 XXX），歌词窗口拿来当标题
        public static string CurrentCassetteTitle()
        {
            if (CurrentSongId <= 0) return null;
            string trackId = TrackId(CurrentSongId);
            try
            {
                string title = FindCassetteTitle(MachineCassettePlayer.GetCassetteInventory(), trackId);
                if (title != null) return title;
                var machine = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.cassettePlayer : null;
                if (machine == null) return null;
                return FindCassetteTitle(MachineHelper.GetBodyStorage(machine, 0) as GameInventory, trackId);
            }
            catch (Exception e)
            {
                log?.Warning("找磁带标题失败：" + e.Message);
                return null;
            }
        }

        private static string FindCassetteTitle(GameInventory inventory, string trackId)
        {
            // 磁带可能在收纳盒里，所以走递归查找
            GameItem item = CassetteScan.Find(inventory, trackId);
            if (item == null) return null;
            string name = item.name ?? "";
            int dash = name.IndexOf(" - ", StringComparison.Ordinal);
            return dash >= 0 ? name.Substring(dash + 3) : name;
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
