// NeteaseApi.cs
// 网易云音乐 HTTP 客户端（协程风格：无 async/Task/Thread，无 Unity 类型）。
//
// 重要：给 INcTransport 实现方的约定
//   * 每个请求都是 form-encoded POST，NcRequest.Url 已含完整 host。
//   * NcRequest.Headers 已经由 NeteaseClient 填好（User-Agent / Referer / Origin / Cookie），
//     transport 只要把这些头原样加上去，不要自己拼请求头、也不要自己维护 cookie 容器。
//     漏掉这些头会被网易云以 -462 “网络环境风险” 拒绝。
//   * Set-Cookie 响应头原样塞进 NcResponse.SetCookie（一句一个元素），客户端自己合并。
//   * NeteaseClient.CookieHeader 是当前 jar 的完整字符串，仅供调试/观测。
//
// 设计要点：
//   * 所有公开协程只 yield return null，绝不 yield 嵌套 IEnumerator，
//     这样 Unity 协程和 `while (e.MoveNext()) { }` 这种土制 pump 都能驱动。
//   * 任何异常都在内部吃掉，转成 onError / 日志，绝不逃出 IEnumerator。
//   * 相邻两次 HTTP 之间至少间隔 250ms（按时间戳算，不 Sleep，返回 null 让调用方泵帧）。

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeteaseCassette
{
    public sealed class NcSong
    {
        public long Id;
        public string Name;
        public string Artists;
        public string Album;
        public int DurationMs;

        /// <summary>“歌名 - 歌手”，歌手为空时不带尾部 “ - ”。</summary>
        public string Display
        {
            get
            {
                string name = Name ?? "";
                string artists = Artists ?? "";
                if (artists.Length == 0) return name;
                return name + " - " + artists;
            }
        }
    }

    public sealed class NcPlaylist
    {
        public long Id;
        public string Name;
        public int TrackCount;
        public bool IsLiked;

        /// <summary>“歌单名 (N)”。</summary>
        public string Display
        {
            get { return (Name ?? "") + " (" + TrackCount.ToString(CultureInfo.InvariantCulture) + ")"; }
        }
    }

    public sealed class NcRequest
    {
        public string Url;
        public Dictionary<string, string> Form;      // null -> 空 body
        public Dictionary<string, string> Headers;   // 由 NeteaseClient 填好，transport 照抄即可
    }

    public sealed class NcResponse
    {
        public long Status;
        public string Text;
        public string[] SetCookie;
        public string Error;                      // HTTP 请求本身成功时为 null

        public bool Ok { get { return Error == null && Status >= 200 && Status < 300; } }
    }

    public interface INcTransport
    {
        // Yields while the request is in flight, then invokes onDone exactly once (must not throw).
        IEnumerator Send(NcRequest req, Action<NcResponse> onDone);
    }

    public sealed class NeteaseClient
    {
        public const int QrExpired = 800;
        public const int QrWaiting = 801;
        public const int QrScanned = 802;
        public const int QrConfirmed = 803;

        internal const string BaseUrl = "https://music.163.com";
        internal const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36";

        /// <summary>两次 HTTP 之间至少间隔的毫秒数。</summary>
        internal const int MinGapMs = 250;

        const string PathAccount = "/api/nuser/account/get";
        const string PathQrUnikey = "/api/login/qrcode/unikey";
        const string PathQrLogin = "/api/login/qrcode/client/login";
        const string PathSearch = "/api/search/get/web";
        const string PathSongUrl = "/api/song/enhance/player/url/v1";
        const string PathUserPlaylist = "/api/user/playlist";
        const string PathPlaylistDetailV6 = "/api/v6/playlist/detail";
        const string PathPlaylistDetailOld = "/api/playlist/detail";
        const string PathSongDetail = "/api/song/detail";

        readonly INcTransport _transport;
        readonly Action<string> _log;

        // 有序 cookie jar：Dictionary 保证查找，List 保证输出顺序（Export/Import 往返稳定）。
        readonly Dictionary<string, string> _jar = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly List<string> _jarOrder = new List<string>();

        readonly string _nmtid;      // 32 hex，构造时生成一次，随会话持久化
        long _lastCallTicks;         // 上一次 HTTP 结束的时间戳（DateTime.UtcNow.Ticks），0 表示从未请求
        bool _loggedIn;
        long _userId;
        string _nickname = "";

        static readonly string[] CookieAttributes =
        {
            "path", "domain", "expires", "max-age", "httponly", "secure",
            "samesite", "version", "comment", "priority", "partitioned"
        };

        public NeteaseClient(INcTransport transport, Action<string> log)
        {
            _transport = transport;
            _log = log;
            _nmtid = NewNmtid();
            EnsureDefaultCookies();
        }

        // ---------------------------------------------------------------- 状态

        public bool IsLoggedIn { get { return _loggedIn; } }
        public long UserId { get { return _userId; } }
        public string Nickname { get { return _nickname ?? ""; } }

        /// <summary>当前完整 Cookie 头。这既是调试信息，也是 transport 取 cookie 的唯一通道。</summary>
        public string CookieHeader
        {
            get
            {
                EnsureDefaultCookies();
                var sb = new StringBuilder();
                for (int i = 0; i < _jarOrder.Count; i++)
                {
                    string key = _jarOrder[i];
                    string value;
                    if (!_jar.TryGetValue(key, out value)) continue;
                    if (sb.Length > 0) sb.Append("; ");
                    sb.Append(key);
                    sb.Append('=');
                    sb.Append(value);
                }
                return sb.ToString();
            }
        }

        public string ExportSession()
        {
            try
            {
                var o = new JObject();
                o["cookies"] = CookieHeader;
                o["userId"] = _userId;
                o["nickname"] = _nickname ?? "";
                return o.ToString(Formatting.None);
            }
            catch (Exception ex)
            {
                Log("ExportSession 失败: " + ex.Message);
                return "{\"cookies\":\"\",\"userId\":0,\"nickname\":\"\"}";
            }
        }

        public void ImportSession(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return;
                JObject o = ParseObject(json);
                if (o == null)
                {
                    Log("ImportSession 忽略无法解析的内容");
                    return;
                }

                JToken cookies = Prop(o, "cookies");
                if (cookies != null && cookies.Type == JTokenType.String)
                {
                    _jar.Clear();
                    _jarOrder.Clear();
                    ParseCookieText(cookies.Value<string>());
                }

                long uid = GetLong(Prop(o, "userId"), 0);
                string nick = GetString(Prop(o, "nickname"));
                _userId = uid > 0 ? uid : 0;
                _nickname = nick;
                _loggedIn = _userId > 0 || HasCookie("MUSIC_U");
                EnsureDefaultCookies();
                Log("ImportSession: userId=" + _userId + " nick=" + _nickname + " cookies=" + _jarOrder.Count);
            }
            catch (Exception ex)
            {
                Log("ImportSession 失败: " + ex.Message);
            }
        }

        public void Logout()
        {
            try
            {
                _jar.Clear();
                _jarOrder.Clear();
                _userId = 0;
                _nickname = "";
                _loggedIn = false;
                EnsureDefaultCookies();
                Log("已退出登录（NMTID 保留）");
            }
            catch (Exception ex)
            {
                Log("Logout 失败: " + ex.Message);
            }
        }

        // ---------------------------------------------------------------- 账号

        public IEnumerator CheckAccount(Action<bool> done)
        {
            var box = new NcBox<bool>();
            var pump = new NcSafePump(CheckAccountCore(box));
            while (pump.MoveNext()) yield return null;
            if (pump.Error != null && box.Error == null) box.Error = pump.Error;
            if (box.Error != null) Log("CheckAccount: " + box.Error);
            Invoke(done, box.Value);
        }

        IEnumerator CheckAccountCore(NcBox<bool> box)
        {
            var rb = new NcBox<NcResponse>();
            yield return SendPost(PathAccount, EmptyForm(), rb);
            NcResponse res = rb.Value;
            if (res == null || res.Error != null)
            {
                box.Error = res == null ? "请求失败" : res.Error;
                box.Value = false;
                yield break;
            }

            JObject o = ParseObject(NormalizeText(res.Text));
            if (o == null)
            {
                box.Error = "账号信息不是合法 JSON";
                box.Value = false;
                yield break;
            }

            JToken profile = Prop(o, "profile");
            long uid = profile != null ? GetLong(Prop(profile, "userId"), 0) : 0;
            string nick = GetString(Prop(profile, "nickname"));
            if (nick.Length == 0) nick = GetString(Prop(profile, "nickName"));

            if (uid > 0)
            {
                _userId = uid;
                _nickname = nick;
                _loggedIn = true;
                box.Value = true;
            }
            else
            {
                // 未登录是很正常的状态，不算错误
                _userId = 0;
                _nickname = "";
                _loggedIn = false;
                box.Value = false;
            }
            Log("CheckAccount -> loggedIn=" + _loggedIn + " userId=" + _userId + " nick=" + _nickname);
        }

        // ---------------------------------------------------------------- 扫码登录

        public IEnumerator QrCreate(Action<string> onKey, Action<string> onError)
        {
            var box = new NcBox<string>();
            var pump = new NcSafePump(QrCreateCore(box));
            while (pump.MoveNext()) yield return null;
            if (pump.Error != null && box.Error == null) box.Error = pump.Error;
            if (box.Error != null)
            {
                Log("QrCreate: " + box.Error);
                Invoke(onError, box.Error);
            }
            else
            {
                Invoke(onKey, box.Value);
            }
        }

        IEnumerator QrCreateCore(NcBox<string> box)
        {
            var form = new Dictionary<string, string>();
            form["type"] = "1";

            var rb = new NcBox<NcResponse>();
            yield return SendPost(PathQrUnikey, form, rb);
            NcResponse res = rb.Value;
            if (res == null || res.Error != null)
            {
                box.Error = res == null ? "请求失败" : res.Error;
                yield break;
            }

            JObject o = ParseObject(NormalizeText(res.Text));
            if (o == null)
            {
                box.Error = "二维码响应不是合法 JSON";
                yield break;
            }

            long code = GetLong(Prop(o, "code"), -1);
            string key = GetString(Prop(o, "unikey"));
            if (key.Length == 0) key = GetString(Prop(o, "key"));
            if (key.Length == 0)
            {
                box.Error = "获取二维码失败：" + ApiError(o, "没有返回 unikey");
                yield break;
            }
            Log("QrCreate -> key=" + key + " (code=" + code + ")");
            box.Value = key;
        }

        public IEnumerator QrPoll(string unikey, Action<int> onCode, Action<string> onMessage)
        {
            var box = new NcQrBox();
            var pump = new NcSafePump(QrPollCore(unikey, box));
            while (pump.MoveNext()) yield return null;
            if (pump.Error != null)
            {
                if (box.Message == null) box.Message = "二维码状态查询失败：" + pump.Error;
                box.Error = pump.Error;
                box.Code = QrWaiting;      // 网络抖动时让调用方继续轮询
            }
            if (box.Error != null) Log("QrPoll: " + box.Error);
            Invoke(onCode, box.Code);
            Invoke(onMessage, box.Message ?? "");
        }

        IEnumerator QrPollCore(string unikey, NcQrBox box)
        {
            if (string.IsNullOrEmpty(unikey))
            {
                box.Error = "二维码 key 为空";
                box.Code = QrExpired;
                box.Message = "二维码无效，请重新获取";
                yield break;
            }

            var form = new Dictionary<string, string>();
            form["key"] = unikey;
            form["type"] = "1";

            var rb = new NcBox<NcResponse>();
            yield return SendPost(PathQrLogin, form, rb);
            NcResponse res = rb.Value;
            if (res == null || res.Error != null)
            {
                box.Error = res == null ? "请求失败" : res.Error;
                box.Code = QrWaiting;
                box.Message = "网络异常，继续等待扫码";
                yield break;
            }

            JObject o = ParseObject(NormalizeText(res.Text));
            if (o == null)
            {
                box.Error = "二维码状态不是合法 JSON";
                box.Code = QrWaiting;
                box.Message = "状态解析失败，继续等待扫码";
                yield break;
            }

            long raw = GetLong(Prop(o, "code"), -1);
            int code = MapQrCode(raw);
            box.Code = code;
            box.Message = QrMessage(code, raw);
            Log("QrPoll raw=" + raw + " mapped=" + code);

            if (code == QrConfirmed)
            {
                // 803 之后必须先把账号信息填好，再回调 onCode(803)
                var ab = new NcBox<bool>();
                yield return CheckAccountCore(ab);
                if (!ab.Value && HasCookie("MUSIC_U"))
                {
                    // 有 MUSIC_U 但账号接口暂时没回话，先乐观标记已登录，下一次 CheckAccount 会纠正
                    _loggedIn = true;
                }
                if (_loggedIn)
                {
                    box.Message = _nickname.Length > 0 ? ("登录成功：" + _nickname) : "登录成功";
                }
                else
                {
                    box.Message = "登录成功（账号信息暂未取到）";
                }
                Log("QrPoll -> loggedIn=" + _loggedIn + " userId=" + _userId);
            }
        }

        int MapQrCode(long raw)
        {
            if (raw == QrExpired || raw == QrWaiting || raw == QrScanned || raw == QrConfirmed)
                return (int)raw;
            if (raw == 200)
                return HasCookie("MUSIC_U") ? QrConfirmed : QrWaiting;
            if (raw > int.MaxValue || raw < int.MinValue) return QrWaiting;
            return (int)raw;   // 未知码原样交给调用方判断
        }

        static string QrMessage(int code, long raw)
        {
            switch (code)
            {
                case QrConfirmed: return "登录成功";
                case QrScanned: return "已扫码，请在手机上确认";
                case QrWaiting: return "等待扫码";
                case QrExpired: return "二维码已过期，请重新获取";
                default: return "未知状态（code=" + raw.ToString(CultureInfo.InvariantCulture) + "）";
            }
        }

        // ---------------------------------------------------------------- 搜索

        public IEnumerator Search(string keyword, int limit, Action<List<NcSong>> onSongs, Action<string> onError)
        {
            var box = new NcBox<List<NcSong>>();
            var pump = new NcSafePump(SearchCore(keyword, limit, box));
            while (pump.MoveNext()) yield return null;
            if (pump.Error != null && box.Error == null) box.Error = pump.Error;
            if (box.Error != null)
            {
                Log("Search: " + box.Error);
                Invoke(onError, box.Error);
            }
            else
            {
                Invoke(onSongs, box.Value ?? new List<NcSong>());
            }
        }

        IEnumerator SearchCore(string keyword, int limit, NcBox<List<NcSong>> box)
        {
            string kw = keyword == null ? "" : keyword.Trim();
            if (kw.Length == 0)
            {
                box.Error = "搜索关键词为空";
                yield break;
            }

            int lim = limit <= 0 ? 30 : limit;
            if (lim > 100) lim = 100;

            var form = new Dictionary<string, string>();
            form["s"] = kw;
            form["type"] = "1";
            form["limit"] = lim.ToString(CultureInfo.InvariantCulture);
            form["offset"] = "0";

            var rb = new NcBox<NcResponse>();
            yield return SendPost(PathSearch, form, rb);
            NcResponse res = rb.Value;
            if (res == null || res.Error != null)
            {
                box.Error = res == null ? "请求失败" : res.Error;
                yield break;
            }

            JObject o = ParseObject(NormalizeText(res.Text));
            if (o == null)
            {
                box.Error = "搜索响应不是合法 JSON";
                yield break;
            }

            long code = GetLong(Prop(o, "code"), 0);
            JToken result = Prop(o, "result");
            JToken songsTok = Prop(result, "songs");
            if (songsTok == null) songsTok = Prop(o, "songs");
            if (songsTok == null)
            {
                box.Error = ApiError(o, "搜索失败");
                yield break;
            }
            if (code != 200 && code != 0 && songsTok.Type != JTokenType.Array)
            {
                box.Error = ApiError(o, "搜索失败");
                yield break;
            }

            var list = new List<NcSong>();
            JArray arr = songsTok as JArray;
            if (arr != null)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    NcSong song = ParseSong(arr[i]);
                    if (song != null && song.Id > 0) list.Add(song);
                }
            }
            Log("Search \"" + kw + "\" -> " + list.Count + " 首（songCount=" +
                GetLong(Prop(result, "songCount"), -1).ToString(CultureInfo.InvariantCulture) + "）");
            box.Value = list;
        }

        // ---------------------------------------------------------------- 播放地址

        public IEnumerator GetSongUrl(long songId, Action<string> onUrl, Action<string> onError)
        {
            var box = new NcBox<string>();
            var pump = new NcSafePump(GetSongUrlCore(songId, box));
            while (pump.MoveNext()) yield return null;
            if (pump.Error != null && box.Error == null) box.Error = pump.Error;
            if (box.Error != null)
            {
                Log("GetSongUrl(" + songId + "): " + box.Error);
                Invoke(onError, box.Error);
            }
            else
            {
                Invoke(onUrl, box.Value);
            }
        }

        IEnumerator GetSongUrlCore(long songId, NcBox<string> box)
        {
            if (songId <= 0)
            {
                box.Error = "歌曲 ID 无效";
                yield break;
            }

            bool preferHigh = _loggedIn;
            string level = preferHigh ? "exhigh" : "standard";
            string lastError = null;
            string url = null;

            var rb = new NcBox<NcResponse>();
            yield return SendPost(PathSongUrl, SongUrlForm(songId, level), rb);
            url = ExtractSongUrl(rb.Value, out lastError);

            if (string.IsNullOrEmpty(url) && preferHigh)
            {
                // exhigh 拿不到（会员/版权）就退回 standard 再试一次
                yield return SendPost(PathSongUrl, SongUrlForm(songId, "standard"), rb);
                url = ExtractSongUrl(rb.Value, out lastError);
            }

            if (string.IsNullOrEmpty(url))
            {
                box.Error = lastError ?? "这首歌需要会员或版权受限";
                yield break;
            }
            Log("GetSongUrl(" + songId + ", " + level + ") -> " + url);
            box.Value = url;
        }

        static string ExtractSongUrl(NcResponse res, out string error)
        {
            error = null;
            if (res == null) { error = "获取播放地址失败"; return null; }
            if (res.Error != null) { error = res.Error; return null; }

            JObject o = ParseObject(NormalizeText(res.Text));
            if (o == null) { error = "播放地址响应不是合法 JSON"; return null; }

            JArray arr = Prop(o, "data") as JArray;
            if (arr == null || arr.Count == 0)
            {
                error = ApiError(o, "未返回播放地址");
                return null;
            }

            JToken first = arr[0];
            string url = GetString(Prop(first, "url"));
            if (url.Length > 0) return url;

            long code = GetLong(Prop(first, "code"), 0);
            long fee = GetLong(Prop(first, "fee"), 0);
            if (fee == 1 || code == 404)
                error = "这首歌需要会员或版权受限";
            else if (code != 0 && code != 200)
                error = "未获取到播放地址（code=" + code.ToString(CultureInfo.InvariantCulture) + "）";
            else
                error = "这首歌需要会员或版权受限";
            return null;
        }

        static Dictionary<string, string> SongUrlForm(long songId, string level)
        {
            var f = new Dictionary<string, string>();
            f["ids"] = "[" + songId.ToString(CultureInfo.InvariantCulture) + "]";
            f["level"] = level;
            f["encodeType"] = "mp3";
            return f;
        }

        // ---------------------------------------------------------------- 歌单

        public IEnumerator GetPlaylists(Action<List<NcPlaylist>> onLists, Action<string> onError)
        {
            var box = new NcBox<List<NcPlaylist>>();
            var pump = new NcSafePump(GetPlaylistsCore(box));
            while (pump.MoveNext()) yield return null;
            if (pump.Error != null && box.Error == null) box.Error = pump.Error;
            if (box.Error != null)
            {
                Log("GetPlaylists: " + box.Error);
                Invoke(onError, box.Error);
            }
            else
            {
                Invoke(onLists, box.Value ?? new List<NcPlaylist>());
            }
        }

        IEnumerator GetPlaylistsCore(NcBox<List<NcPlaylist>> box)
        {
            if (_userId <= 0 || !_loggedIn)
            {
                // 连 MUSIC_U 都没有，肯定没登录，别白跑一次接口
                if (HasCookie("MUSIC_U"))
                {
                    var ab = new NcBox<bool>();
                    yield return CheckAccountCore(ab);
                }
            }
            if (_userId <= 0)
            {
                box.Error = "请先扫码登录网易云账号";
                yield break;
            }

            var form = new Dictionary<string, string>();
            form["uid"] = _userId.ToString(CultureInfo.InvariantCulture);
            form["limit"] = "100";
            form["offset"] = "0";

            var rb = new NcBox<NcResponse>();
            yield return SendPost(PathUserPlaylist, form, rb);
            NcResponse res = rb.Value;
            if (res == null || res.Error != null)
            {
                box.Error = res == null ? "请求失败" : res.Error;
                yield break;
            }

            JObject o = ParseObject(NormalizeText(res.Text));
            if (o == null)
            {
                box.Error = "歌单响应不是合法 JSON";
                yield break;
            }

            JToken listTok = Prop(o, "playlist");
            if (listTok == null) listTok = Prop(Prop(o, "result"), "playlist");
            if (listTok == null)
            {
                box.Error = ApiError(o, "获取歌单失败");
                yield break;
            }

            var list = new List<NcPlaylist>();
            JArray arr = listTok as JArray;
            if (arr != null)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    NcPlaylist p = ParsePlaylist(arr[i]);
                    if (p != null && p.Id > 0) list.Add(p);
                }
            }
            Log("GetPlaylists -> " + list.Count + " 个");
            box.Value = list;
        }

        public IEnumerator GetPlaylistTracks(long playlistId, Action<List<NcSong>> onSongs, Action<string> onError)
        {
            var box = new NcBox<List<NcSong>>();
            var pump = new NcSafePump(GetPlaylistTracksCore(playlistId, box));
            while (pump.MoveNext()) yield return null;
            if (pump.Error != null && box.Error == null) box.Error = pump.Error;
            if (box.Error != null)
            {
                Log("GetPlaylistTracks(" + playlistId + "): " + box.Error);
                Invoke(onError, box.Error);
            }
            else
            {
                Invoke(onSongs, box.Value ?? new List<NcSong>());
            }
        }

        IEnumerator GetPlaylistTracksCore(long playlistId, NcBox<List<NcSong>> box)
        {
            if (playlistId <= 0)
            {
                box.Error = "歌单 ID 无效";
                yield break;
            }

            var rb = new NcBox<NcResponse>();
            yield return SendPost(PathPlaylistDetailV6, PlaylistForm(playlistId), rb);
            string err1;
            List<NcSong> songs = ParsePlaylistTracks(rb.Value, out err1);

            if (songs == null)
            {
                // v6 挂了就退回老接口
                yield return SendPost(PathPlaylistDetailOld, PlaylistForm(playlistId), rb);
                string err2;
                songs = ParsePlaylistTracks(rb.Value, out err2);
                if (songs == null)
                {
                    box.Error = err2 ?? err1 ?? "获取歌单歌曲失败";
                    yield break;
                }
            }

            Log("GetPlaylistTracks(" + playlistId + ") -> " + songs.Count + " 首");
            box.Value = songs;
        }

        static Dictionary<string, string> PlaylistForm(long playlistId)
        {
            var f = new Dictionary<string, string>();
            f["id"] = playlistId.ToString(CultureInfo.InvariantCulture);
            f["n"] = "1000";
            f["s"] = "0";
            return f;
        }

        static List<NcSong> ParsePlaylistTracks(NcResponse res, out string error)
        {
            error = null;
            if (res == null) { error = "请求失败"; return null; }
            if (res.Error != null) { error = res.Error; return null; }

            JObject o = ParseObject(NormalizeText(res.Text));
            if (o == null) { error = "歌单响应不是合法 JSON"; return null; }

            JToken playlist = Prop(o, "playlist");
            if (playlist == null) playlist = Prop(o, "result");
            if (playlist == null)
            {
                error = ApiError(o, "歌单不存在或无法访问");
                return null;
            }

            JToken tracks = Prop(playlist, "tracks");
            if (tracks == null) tracks = Prop(playlist, "songs");

            var list = new List<NcSong>();
            JArray arr = tracks as JArray;
            if (arr != null)
            {
                for (int i = 0; i < arr.Count; i++)
                {
                    NcSong song = ParseSong(arr[i]);
                    if (song != null && song.Id > 0) list.Add(song);
                }
            }
            return list;   // 可能是空列表（空歌单 / 所有曲目都没权限）
        }

        // ---------------------------------------------------------------- HTTP

        IEnumerator SendPost(string path, Dictionary<string, string> form, NcBox<NcResponse> box)
        {
            // 节流：距离上次请求结束不足 250ms 就返回 null 让调用方泵帧
            while (true)
            {
                if (_lastCallTicks == 0) break;
                long gap = (DateTime.UtcNow.Ticks - _lastCallTicks) / TimeSpan.TicksPerMillisecond;
                if (gap >= MinGapMs || gap < 0) break;
                yield return null;
            }

            var req = new NcRequest();
            req.Url = BaseUrl + path;
            req.Form = form;
            req.Headers = BuildHeaders();

            var runner = new NcTransportRunner(_transport, req);
            while (runner.MoveNext()) yield return null;
            _lastCallTicks = DateTime.UtcNow.Ticks;

            NcResponse res = runner.Response;
            if (res == null)
            {
                res = new NcResponse();
                res.Status = 0;
                res.Error = runner.Error ?? "网络请求失败";
            }
            box.Value = res;

            MergeCookies(res);
            Log("POST " + path + " -> " + (res.Error != null ? ("ERR " + res.Error) : ("HTTP " + res.Status)) +
                (res.Text == null ? "" : (" " + res.Text.Length + "B")));
        }

        static Dictionary<string, string> EmptyForm()
        {
            return new Dictionary<string, string>();
        }

        /// <summary>每个请求固定要带的头（cookie 每次都取当前 jar，登录后会变）。</summary>
        Dictionary<string, string> BuildHeaders()
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            headers["User-Agent"] = UserAgent;
            headers["Referer"] = "https://music.163.com/";
            headers["Origin"] = "https://music.163.com";
            headers["Cookie"] = CookieHeader;
            return headers;
        }

        // ---------------------------------------------------------------- cookie

        void EnsureDefaultCookies()
        {
            if (!_jar.ContainsKey("os")) SetCookie("os", "pc");
            if (!_jar.ContainsKey("appver")) SetCookie("appver", "2.9.7");
            string v;
            if (!_jar.TryGetValue("NMTID", out v) || string.IsNullOrEmpty(v)) SetCookie("NMTID", _nmtid);
        }

        void SetCookie(string name, string value)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (!_jar.ContainsKey(name)) _jarOrder.Add(name);
            _jar[name] = value ?? "";
        }

        bool HasCookie(string name)
        {
            string v;
            return _jar.TryGetValue(name, out v) && !string.IsNullOrEmpty(v);
        }

        void MergeCookies(NcResponse res)
        {
            if (res == null) return;
            try
            {
                if (res.SetCookie != null)
                {
                    for (int i = 0; i < res.SetCookie.Length; i++) ParseCookieText(res.SetCookie[i]);
                }

                string text = NormalizeText(res.Text);
                if (text.Length > 0 && text.IndexOf("\"cookie\"", StringComparison.Ordinal) >= 0)
                {
                    JObject o = ParseObject(text);
                    if (o != null)
                    {
                        JToken c = Prop(o, "cookie");
                        if (c != null && c.Type == JTokenType.String)
                        {
                            ParseCookieText(c.Value<string>());
                        }
                        else if (c != null && c.Type == JTokenType.Object)
                        {
                            JObject co = c as JObject;
                            if (co != null)
                            {
                                foreach (KeyValuePair<string, JToken> kv in co)
                                {
                                    if (kv.Value != null && kv.Value.Type == JTokenType.String)
                                        SetCookie(kv.Key, kv.Value.Value<string>());
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("合并 cookie 失败: " + ex.Message);
            }
        }

        /// <summary>解析 Set-Cookie 头或 JSON cookie 字符串，容错地往里塞。</summary>
        void ParseCookieText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            // 一条头里可能有多块 cookie（用 ", " 连接），每块里又有 "attr=value"
            string[] pieces = text.Split(',');
            for (int p = 0; p < pieces.Length; p++)
            {
                string[] segments = pieces[p].Split(';');
                for (int i = 0; i < segments.Length; i++)
                {
                    string seg = segments[i].Trim();
                    if (seg.Length == 0) continue;
                    int eq = seg.IndexOf('=');
                    if (eq <= 0) continue;
                    string name = seg.Substring(0, eq).Trim();
                    string value = seg.Substring(eq + 1).Trim();
                    if (!IsCookieName(name)) continue;
                    if (IsCookieAttribute(name)) continue;
                    SetCookie(name, value);
                }
            }
        }

        static bool IsCookieName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')
                          || c == '_' || c == '-' || c == '.' || c == '!' || c == '#' || c == '$'
                          || c == '%' || c == '&' || c == '\'' || c == '*' || c == '+' || c == '^'
                          || c == '`' || c == '|' || c == '~';
                if (!ok) return false;
            }
            return true;
        }

        static bool IsCookieAttribute(string name)
        {
            for (int i = 0; i < CookieAttributes.Length; i++)
            {
                if (string.Equals(name, CookieAttributes[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        static string NewNmtid()
        {
            var rnd = new Random(unchecked(Environment.TickCount * 397 + DateTime.UtcNow.Millisecond + Guid.NewGuid().GetHashCode()));
            var sb = new StringBuilder(32);
            const string hex = "0123456789abcdef";
            for (int i = 0; i < 32; i++) sb.Append(hex[rnd.Next(16)]);
            return sb.ToString();
        }

        // ---------------------------------------------------------------- 解析工具

        static string NormalizeText(string s)
        {
            if (s == null) return "";
            return s.Trim().TrimStart('\uFEFF').Trim();
        }

        static JObject ParseObject(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                return JObject.Parse(text);
            }
            catch
            {
                return null;
            }
        }

        static JToken Prop(JToken token, string name)
        {
            if (token == null || name == null) return null;
            JObject o = token as JObject;
            if (o == null) return null;
            return o[name];
        }

        static string GetString(JToken token)
        {
            if (token == null) return "";
            try
            {
                switch (token.Type)
                {
                    case JTokenType.String: return token.Value<string>() ?? "";
                    case JTokenType.Integer:
                    case JTokenType.Float:
                    case JTokenType.Boolean:
                    case JTokenType.Date:
                    case JTokenType.Guid:
                    case JTokenType.Uri:
                        return token.ToString();
                    default: return "";
                }
            }
            catch
            {
                return "";
            }
        }

        static long GetLong(JToken token, long fallback)
        {
            if (token == null) return fallback;
            try
            {
                switch (token.Type)
                {
                    case JTokenType.Integer: return token.Value<long>();
                    case JTokenType.Float: return (long)token.Value<double>();
                    case JTokenType.Boolean: return token.Value<bool>() ? 1L : 0L;
                    case JTokenType.String:
                        {
                            string s = token.Value<string>();
                            if (string.IsNullOrEmpty(s)) return fallback;
                            long v;
                            if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v;
                            double d;
                            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return (long)d;
                            return fallback;
                        }
                    case JTokenType.Null:
                    case JTokenType.Undefined:
                        return fallback;
                    default:
                        return fallback;
                }
            }
            catch
            {
                return fallback;
            }
        }

        static string ApiError(JObject o, string fallback)
        {
            long code = -1;
            string msg = "";
            if (o != null)
            {
                code = GetLong(Prop(o, "code"), -1);
                msg = GetString(Prop(o, "message"));
                if (msg.Length == 0) msg = GetString(Prop(o, "msg"));
            }
            if (msg.Length == 0) return fallback + "（code=" + code.ToString(CultureInfo.InvariantCulture) + "）";
            return msg + "（code=" + code.ToString(CultureInfo.InvariantCulture) + "）";
        }

        static JToken FirstPresent(JToken a, JToken b)
        {
            return a != null ? a : b;
        }

        static NcSong ParseSong(JToken token)
        {
            if (token == null) return null;
            var song = new NcSong();
            song.Id = GetLong(Prop(token, "id"), 0);
            song.Name = GetString(Prop(token, "name"));
            song.Artists = JoinArtists(FirstPresent(Prop(token, "artists"), Prop(token, "ar")));
            song.Album = AlbumName(FirstPresent(Prop(token, "album"), Prop(token, "al")));
            long dur = GetLong(Prop(token, "dt"), 0);
            if (dur <= 0) dur = GetLong(Prop(token, "duration"), 0);
            song.DurationMs = dur > int.MaxValue ? 0 : (int)dur;
            return song;
        }

        static string JoinArtists(JToken token)
        {
            JArray arr = token as JArray;
            if (arr == null) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < arr.Count; i++)
            {
                JToken item = arr[i];
                string name = GetString(Prop(item, "name"));
                if (name.Length == 0) name = GetString(item);   // 少数接口直接给字符串
                if (name.Length == 0) continue;
                if (sb.Length > 0) sb.Append("/");
                sb.Append(name);
            }
            return sb.ToString();
        }

        static string AlbumName(JToken token)
        {
            if (token == null) return "";
            string name = GetString(Prop(token, "name"));
            if (name.Length == 0) name = GetString(token);
            return name;
        }

        static NcPlaylist ParsePlaylist(JToken token)
        {
            if (token == null) return null;
            var list = new NcPlaylist();
            list.Id = GetLong(Prop(token, "id"), 0);
            list.Name = GetString(Prop(token, "name"));
            long count = GetLong(Prop(token, "trackCount"), 0);
            if (count <= 0) count = GetLong(Prop(token, "songCount"), 0);
            list.TrackCount = count > int.MaxValue ? 0 : (int)count;
            list.IsLiked = GetLong(Prop(token, "specialType"), 0) == 5;
            return list;
        }

        // ---------------------------------------------------------------- 回调 / 日志

        void Log(string message)
        {
            if (_log == null) return;
            try
            {
                _log(message);
            }
            catch
            {
                // 日志回调自己炸了也不能影响主流程
            }
        }

        void Invoke<T>(Action<T> callback, T value)
        {
            if (callback == null) return;
            try
            {
                callback(value);
            }
            catch (Exception ex)
            {
                Log("回调异常: " + ex.Message);
            }
        }
    }

    // -------------------------------------------------------------------- 内部类型

    internal sealed class NcBox<T>
    {
        public T Value;
        public string Error;
    }

    internal sealed class NcQrBox
    {
        public int Code = NeteaseClient.QrWaiting;
        public string Message;
        public string Error;
    }

    /// <summary>把 transport 的协程包裹成人畜无害的 MoveNext（异常一律转 Error）。</summary>
    internal sealed class NcTransportRunner
    {
        readonly INcTransport _transport;
        readonly NcRequest _req;
        IEnumerator _inner;
        bool _started;
        bool _done;

        public NcResponse Response;
        public string Error;

        public NcTransportRunner(INcTransport transport, NcRequest req)
        {
            _transport = transport;
            _req = req;
        }

        public bool MoveNext()
        {
            if (Error != null || _done) return false;
            try
            {
                if (!_started)
                {
                    _started = true;
                    if (_transport == null)
                    {
                        Error = "没有可用的网络传输层";
                        return false;
                    }
                    try
                    {
                        _inner = _transport.Send(_req, HandleDone);
                    }
                    catch (Exception ex)
                    {
                        Error = "传输层异常: " + ex.Message;
                        return false;
                    }
                    if (_inner == null)
                    {
                        if (_done) return false;
                        Error = "传输层返回了空协程";
                        return false;
                    }
                }

                if (_done) return false;

                bool moved;
                try
                {
                    moved = _inner.MoveNext();
                }
                catch (Exception ex)
                {
                    Error = "传输层异常: " + ex.Message;
                    return false;
                }

                if (!moved)
                {
                    if (!_done) Error = "传输层结束但没有回调 onDone";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Error = "传输层异常: " + ex.Message;
                return false;
            }
        }

        void HandleDone(NcResponse res)
        {
            if (_done) return;
            _done = true;
            Response = res;
        }
    }

    /// <summary>
    /// 内部协程的保险丝：任何异常都变成 Error。
    /// 同时像 Unity 那样驱动嵌套 IEnumerator（yield return 子协程），
    /// 这样公开协程的调用方用 `while (e.MoveNext()) { }` 这种土制 pump 也能跑。
    /// </summary>
    internal sealed class NcSafePump
    {
        readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
        IEnumerator _inner;

        public string Error;

        public NcSafePump(IEnumerator inner)
        {
            _inner = inner;
        }

        public bool MoveNext()
        {
            while (true)
            {
                if (Error != null) return false;
                if (_inner == null)
                {
                    if (_stack.Count == 0)
                    {
                        Error = "内部协程为空";
                        return false;
                    }
                    _inner = _stack.Pop();
                    continue;
                }

                bool moved;
                try
                {
                    moved = _inner.MoveNext();
                }
                catch (Exception ex)
                {
                    Error = "内部异常: " + ex.Message;
                    return false;
                }

                if (!moved)
                {
                    _inner = _stack.Count > 0 ? _stack.Pop() : null;
                    if (_inner == null) return false;
                    continue;
                }

                object current = null;
                try
                {
                    current = _inner.Current;
                }
                catch
                {
                    current = null;
                }

                IEnumerator nested = current as IEnumerator;
                if (nested != null)
                {
                    _stack.Push(_inner);
                    _inner = nested;
                    continue;
                }
                return true;
            }
        }
    }
}
