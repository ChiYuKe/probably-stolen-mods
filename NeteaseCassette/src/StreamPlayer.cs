using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace NeteaseCassette
{
    /// <summary>
    /// 网易云曲目的实际播放：先问 API 要地址，再下载成 AudioClip，最后丢进游戏自己的音乐音源。
    /// 磁带本身只是带 "netease:&lt;songId&gt;" 标签的原版磁带，所以播放/暂停/下一首还是原版逻辑。
    /// </summary>
    public static class StreamPlayer
    {
        public const string Prefix = "netease:";
        private const int MaxConsecutiveFailures = 3;

        private static NeteaseClient client;
        private static ModLog log;

        private static int generation;
        private static UnityWebRequest current;
        private static bool clipReady;
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

        /// <summary>原版 AudioManager.PlayMusic 被我们拦下来后走这里。</summary>
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
            ModHelper.StartCoroutine(ModEntry.ModId, LoadAndPlay(songId));
        }

        /// <summary>自动连播时防止一整排坏歌把播放器卡死。</summary>
        public static void ResetFailureCounter()
        {
            consecutiveFailures = 0;
        }

        /// <summary>暂停时只取消还没下载完的曲目，已经出声的交给原版 PauseMusic。</summary>
        public static void CancelPending()
        {
            if (!clipReady)
            {
                Cancel();
            }
        }

        /// <summary>停止 / 切回原版磁带时彻底放手。</summary>
        public static void Cancel()
        {
            generation++;
            clipReady = false;
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
                // 已经结束的请求 Abort 会报错，忽略
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
            // 自己再套一层：任何一步抛异常都要收尾，不能让 AudioManager 的等待协程永远挂着
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
                // 和游戏自己加载 mod 里的 mp3 用的是同一条路：UnityWebRequestMultimedia + DownloadHandlerAudioClip
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

            // 解码出来的若是空音频，Play() 不会真的开始，原版的 WaitForSound 会一直挂着
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
