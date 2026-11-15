using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeteaseCassette
{
    /// <summary>游戏里的网易云窗口：扫码登录、搜索、我的歌单，点歌生成磁带并直接播放。</summary>
    public static class CloudWindow
    {
        private const string WindowId = "netease_cassette.cloud";
        private const string QrElementId = "qr";
        private const string AccountId = "account";
        private const string MainStatusId = "main_status";
        private const string LoginStatusId = "login_status";
        private const string SearchStatusId = "search_status";
        private const string PlaylistStatusId = "playlist_status";
        private const string NowPlayingId = "now_playing";
        private const int MaxListRows = 150;

        private static CustomUIWindow window;
        private static RectTransform searchHost;
        private static RectTransform playlistHost;

        private static int qrGeneration;
        private static string lastKeyword = string.Empty;
        private static List<NcSong> searchSongs = new List<NcSong>();
        private static List<NcSong> playlistSongs = new List<NcSong>();
        private static List<NcPlaylist> playlists = new List<NcPlaylist>();
        private static NcPlaylist currentPlaylist;

        // ---------------------------------------------------------------- 窗口

        public static void Open()
        {
            if (CustomUIManager.Instance == null)
            {
                ModEntry.Log?.Warning("场景里没有 CustomUIManager，开不了窗口");
                return;
            }
            Build();
            if (EmporiumEntry.Instance == null || EmporiumEntry.Instance.cassettePlayer == null)
            {
                ShowStatus("没找到磁带机：要先载入店铺存档（现在只能搜歌，不能生成磁带）");
            }
        }

        public static void Close()
        {
            qrGeneration++;
            try
            {
                CustomUIManager.Instance?.CloseWindow(WindowId);
            }
            catch
            {
                // 窗口本来就不在，忽略
            }
            window = null;
            searchHost = null;
            playlistHost = null;
        }

        private static void Build()
        {
            var builder = CustomUIManager.Window(WindowId, "网易云音乐 · 磁带机");
            if (builder == null) return;
            window = builder.Handle;

            bool loggedIn = ModEntry.Client != null && ModEntry.Client.IsLoggedIn;

            builder
                .SetSize(640f, 720f)
                .SetDraggable(true)
                .SetCloseOnEscape(true)
                .Center();

            builder.AddHeader("网易云音乐");
            builder.AddLabel(AccountLine(), AccountId);
            builder.AddLabel(" ", MainStatusId);
            builder.AddSpace(6f);

            builder.BeginTabs();

            // ---------------- 扫码
            builder.BeginTab("扫码登录");
            builder.AddLabel(
                loggedIn
                    ? "已经登录。想换账号：先点「退出登录」，再点「刷新二维码」。"
                    : "手机网易云音乐 → 右上角「扫一扫」→ 扫下面的二维码。",
                "login_hint");
            builder.AddSpace(2f);
            builder.BeginRow(4f, TextAnchor.MiddleCenter);
            builder.AddFlexibleSpace();
            builder.AddImage((Sprite)null, 220f, 220f, QrElementId);
            builder.AddFlexibleSpace();
            builder.End();
            builder.AddLabel(" ", LoginStatusId);
            builder.AddSpace(2f);
            builder.BeginRow();
            builder.AddButton("刷新二维码", RefreshQr);
            builder.AddSpace(8f);
            builder.AddButton("退出登录", Logout);
            builder.AddFlexibleSpace();
            builder.End();
            builder.End();

            // ---------------- 搜索
            builder.BeginTab("搜索");
            builder.BeginRow();
            builder.AddInput("输入歌名 / 歌手，回车搜索", lastKeyword, value => lastKeyword = value);
            builder.AddSpace(8f);
            builder.AddButton("搜索", () => StartSearch(lastKeyword));
            builder.End();
            builder.AddLabel("点一首歌：生成磁带 → 丢进播放器 → 直接开播。", SearchStatusId);
            builder.BeginScrollFlexible(380f);
            searchHost = builder.CurrentContainer;
            builder.End();
            builder.End();

            // ---------------- 歌单
            builder.BeginTab("歌单");
            builder.AddLabel(loggedIn ? "点开歌单，再点曲目。" : "扫码登录后才能读你的歌单。", PlaylistStatusId);
            builder.BeginScrollFlexible(420f);
            playlistHost = builder.CurrentContainer;
            builder.End();
            builder.End();

            builder.EndTabs();

            builder.AddLabel(" ", NowPlayingId);
            builder.BeginRow();
            builder.AddFlexibleSpace();
            builder.AddButton("停止", StopPlayback);
            builder.AddSpace(8f);
            builder.AddButton("关闭", Close);
            builder.End();

            builder.Show();

            // 没登录才需要二维码；已登录就把图片控件收起来，不然会露出预制体自带的占位图
            SetVisible(QrElementId, false);

            FillSearch();
            if (loggedIn)
            {
                SetText(LoginStatusId, "已登录，二维码用不上了");
                FillPlaylists();
                LoadPlaylists();
            }
            else
            {
                FillPlaylistMessage("未登录。");
                RefreshQr();
            }
        }

        private static string AccountLine()
        {
            var client = ModEntry.Client;
            if (client == null) return "客户端未就绪";
            if (!client.IsLoggedIn) return "未登录（不登录也能搜歌、放免费歌）";
            string name = string.IsNullOrEmpty(client.Nickname) ? ("UID " + client.UserId) : client.Nickname;
            return "已登录：" + name;
        }

        // ---------------------------------------------------------------- 控件小工具

        private static void SetText(string elementId, string text)
        {
            var w = window;
            if (w == null || !w.IsAlive) return;
            try
            {
                var element = w.Get(elementId);
                element?.SetText(text);
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("更新控件 " + elementId + " 失败：" + e.Message);
            }
        }

        private static void SetSprite(string elementId, Sprite sprite)
        {
            var w = window;
            if (w == null || !w.IsAlive) return;
            try
            {
                var element = w.Get(elementId);
                element?.SetSprite(sprite);
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("更新图片 " + elementId + " 失败：" + e.Message);
            }
        }

        private static void SetVisible(string elementId, bool visible)
        {
            var w = window;
            if (w == null || !w.IsAlive) return;
            try
            {
                var element = w.Get(elementId);
                element?.SetVisible(visible);
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("显示/隐藏控件 " + elementId + " 失败：" + e.Message);
            }
        }

        public static void ShowStatus(string message)
        {
            SetText(MainStatusId, message ?? string.Empty);
        }

        /// <summary>歌名里常见的 emoji / 变体选择符在游戏字体里是方块，显示前清掉。</summary>
        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var builder = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (char.IsSurrogate(c))
                {
                    i++;                     // 跳过代理对（emoji）
                    continue;
                }
                if (c >= '\uFE00' && c <= '\uFE0F') continue;
                if (c >= '\u1400' && c <= '\u167F') continue;   // 游戏字体里没有的加拿大音节文字，会变方块
                if (c >= '\uE000' && c <= '\uF8FF') continue;   // 私有区
                if (c == '\u200D' || c == '\u20E3') continue;
                builder.Append(c);
            }
            return builder.ToString();
        }

        // ---------------------------------------------------------------- 扫码

        private static void RefreshQr()
        {
            var client = ModEntry.Client;
            if (client == null)
            {
                ShowStatus("客户端未就绪");
                return;
            }
            if (client.IsLoggedIn)
            {
                SetText(LoginStatusId, AccountLine());
                return;
            }
            int generation = ++qrGeneration;
            SetText(LoginStatusId, "正在向网易云要二维码…");
            ModHelper.StartCoroutine(ModEntry.ModId, QrRoutine(generation));
        }

        private static IEnumerator QrRoutine(int generation)
        {
            string unikey = null;
            string error = null;
            yield return ModEntry.Client.QrCreate(key => unikey = key, message => error = message);
            if (generation != qrGeneration) yield break;

            if (string.IsNullOrEmpty(unikey))
            {
                SetText(LoginStatusId, "要二维码失败：" + (error ?? "网络不通"));
                yield break;
            }

            bool[,] matrix = null;
            try
            {
                matrix = QrCode.Encode("https://music.163.com/login?codekey=" + unikey, QrCode.EccMedium);
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("生成二维码出错：" + e.Message);
            }
            if (matrix == null)
            {
                SetText(LoginStatusId, "二维码生成失败");
                yield break;
            }

            var sprite = QrSprite.Create(matrix, 7, 4);
            SetSprite(QrElementId, sprite);
            SetVisible(QrElementId, true);
            SetText(LoginStatusId, "等待扫码…（手机网易云 → 扫一扫）");

            while (generation == qrGeneration)
            {
                int code = -1;
                string message = null;
                yield return ModEntry.Client.QrPoll(unikey, value => code = value, value => message = value);
                if (generation != qrGeneration) yield break;

                switch (code)
                {
                    case NeteaseClient.QrConfirmed:
                        ModEntry.SaveSession();
                        ModEntry.Log?.Log("扫码登录成功：" + ModEntry.Client.Nickname);
                        SetText(LoginStatusId, AccountLine());
                        SetText(AccountId, AccountLine());
                        ShowStatus("登录成功，可以搜歌了");
                        FillPlaylists();
                        LoadPlaylists();
                        yield break;
                    case NeteaseClient.QrExpired:
                        SetText(LoginStatusId, "二维码过期了，点「刷新二维码」再来一次");
                        yield break;
                    case NeteaseClient.QrScanned:
                        SetText(LoginStatusId, "已扫码，在手机上点「确认登录」");
                        break;
                    default:
                        SetText(LoginStatusId, string.IsNullOrEmpty(message) ? "等待扫码…" : message);
                        break;
                }

                yield return new WaitForSecondsRealtime(2f);
            }
        }

        private static void Logout()
        {
            qrGeneration++;
            try
            {
                ModEntry.Client?.Logout();
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("退出登录出错：" + e.Message);
            }
            ModEntry.ClearSession();
            playlists.Clear();
            playlistSongs.Clear();
            currentPlaylist = null;
            SetText(AccountId, AccountLine());
            SetText(LoginStatusId, "已退出登录");
            FillPlaylistMessage("未登录。");
        }

        // ---------------------------------------------------------------- 搜索

        private static void StartSearch(string keyword)
        {
            keyword = (keyword ?? string.Empty).Trim();
            if (keyword.Length == 0)
            {
                SetText(SearchStatusId, "先输点关键词");
                return;
            }
            if (ModEntry.Client == null) return;
            lastKeyword = keyword;
            SetText(SearchStatusId, "搜索中…");
            ModHelper.StartCoroutine(ModEntry.ModId, SearchRoutine(keyword));
        }

        private static IEnumerator SearchRoutine(string keyword)
        {
            List<NcSong> result = null;
            string error = null;
            yield return ModEntry.Client.Search(keyword, 30, songs => result = songs, message => error = message);
            if (!string.IsNullOrEmpty(error))
            {
                SetText(SearchStatusId, "搜索失败：" + error);
                yield break;
            }
            searchSongs = result ?? new List<NcSong>();
            SetText(SearchStatusId, "找到 " + searchSongs.Count + " 首，点一首就开始放。");
            FillSearch();
        }

        private static void FillSearch()
        {
            if (searchHost == null || window == null || !window.IsAlive) return;
            try
            {
                CustomUIBuilder.ClearContainer(searchHost);
                var builder = new CustomUIBuilder(CustomUIManager.Instance, window, searchHost);
                if (searchSongs.Count == 0)
                {
                    builder.AddLabel("（空）");
                }
                else
                {
                    int shown = 0;
                    foreach (var item in searchSongs)
                    {
                        if (shown >= MaxListRows) break;
                        shown++;
                        var song = item;
                        string label = Clean(song.Display);
                        if (song.DurationMs > 0)
                        {
                            label += "  [" + (song.DurationMs / 60000).ToString("0") + ":" +
                                     ((song.DurationMs / 1000) % 60).ToString("00") + "]";
                        }
                        builder.AddButton(label, () => PlaySong(song));
                    }
                    if (searchSongs.Count > shown)
                    {
                        builder.AddLabel("……还有 " + (searchSongs.Count - shown) + " 首没列出来");
                    }
                }
                CustomUIBuilder.RebuildLayout(searchHost);
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("渲染搜索结果出错：" + e.Message);
            }
        }

        // ---------------------------------------------------------------- 歌单

        private static void LoadPlaylists()
        {
            if (ModEntry.Client == null || !ModEntry.Client.IsLoggedIn) return;
            SetText(PlaylistStatusId, "读取歌单中…");
            ModHelper.StartCoroutine(ModEntry.ModId, PlaylistRoutine());
        }

        private static IEnumerator PlaylistRoutine()
        {
            List<NcPlaylist> result = null;
            string error = null;
            yield return ModEntry.Client.GetPlaylists(lists => result = lists, message => error = message);
            if (!string.IsNullOrEmpty(error))
            {
                SetText(PlaylistStatusId, "读歌单失败：" + error);
                yield break;
            }
            playlists = result ?? new List<NcPlaylist>();
            currentPlaylist = null;
            SetText(PlaylistStatusId, "共 " + playlists.Count + " 个歌单，点进去看曲目。");
            FillPlaylists();
        }

        private static void FillPlaylists()
        {
            if (playlistHost == null || window == null || !window.IsAlive) return;
            try
            {
                CustomUIBuilder.ClearContainer(playlistHost);
                var builder = new CustomUIBuilder(CustomUIManager.Instance, window, playlistHost);
                if (currentPlaylist != null)
                {
                    builder.AddButton("← 返回歌单列表", () =>
                    {
                        currentPlaylist = null;
                        FillPlaylists();
                    });
                    builder.AddHeader(Clean(currentPlaylist.Display));
                    int shown = 0;
                    foreach (var item in playlistSongs)
                    {
                        if (shown >= MaxListRows) break;
                        shown++;
                        var song = item;
                        builder.AddButton(Clean(song.Display), () => PlaySong(song));
                    }
                    if (playlistSongs.Count > shown)
                    {
                        builder.AddButton("……还有 " + (playlistSongs.Count - shown) + " 首没列出来", () => { });
                    }
                }
                else
                {
                    if (playlists.Count == 0)
                    {
                        builder.AddLabel("（空）");
                    }
                    foreach (var item in playlists)
                    {
                        var playlist = item;
                        builder.AddButton(Clean(playlist.Display), () => OpenPlaylist(playlist));
                    }
                }
                CustomUIBuilder.RebuildLayout(playlistHost);
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("渲染歌单出错：" + e.Message);
            }
        }

        private static void FillPlaylistMessage(string message)
        {
            if (playlistHost == null || window == null || !window.IsAlive) return;
            try
            {
                CustomUIBuilder.ClearContainer(playlistHost);
                var builder = new CustomUIBuilder(CustomUIManager.Instance, window, playlistHost);
                builder.AddLabel(message);
                CustomUIBuilder.RebuildLayout(playlistHost);
            }
            catch
            {
                // 窗口没了就算了
            }
        }

        private static void OpenPlaylist(NcPlaylist playlist)
        {
            if (ModEntry.Client == null) return;
            currentPlaylist = playlist;
            SetText(PlaylistStatusId, "读取《" + playlist.Name + "》…");
            ModHelper.StartCoroutine(ModEntry.ModId, TracksRoutine(playlist));
        }

        private static IEnumerator TracksRoutine(NcPlaylist playlist)
        {
            List<NcSong> result = null;
            string error = null;
            yield return ModEntry.Client.GetPlaylistTracks(playlist.Id, songs => result = songs, message => error = message);
            if (currentPlaylist == null || currentPlaylist.Id != playlist.Id) yield break;
            if (!string.IsNullOrEmpty(error))
            {
                SetText(PlaylistStatusId, "读取失败：" + error);
                yield break;
            }
            playlistSongs = result ?? new List<NcSong>();
            SetText(PlaylistStatusId, "《" + playlist.Name + "》共 " + playlistSongs.Count + " 首。");
            FillPlaylists();
        }

        // ---------------------------------------------------------------- 点歌

        private static void PlaySong(NcSong song)
        {
            if (song == null) return;
            ModEntry.Log?.Log("点歌：" + song.Name + " (" + song.Id + ")");
            var machine = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.cassettePlayer : null;
            if (machine == null)
            {
                ModEntry.Log?.Warning("点歌失败：磁带机不在");
                ShowStatus("要先载入店铺存档，磁带机才在");
                return;
            }

            var deck = MachineCassettePlayer.GetCassetteInventory();
            if (deck == null)
            {
                ModEntry.Log?.Warning("点歌失败：找不到磁带仓");
                ShowStatus("找不到磁带仓");
                return;
            }

            string trackId = StreamPlayer.TrackId(song.Id);
            GameItem cassette = FindCassette(deck, trackId);
            if (cassette == null)
            {
                var storage = MachineHelper.GetBodyStorage(machine, 0);
                cassette = FindCassette(storage, trackId);
            }

            if (cassette == null)
            {
                cassette = CreateCassette(song, trackId);
                if (cassette == null)
                {
                    ShowStatus("生成磁带失败，看日志");
                    return;
                }
                // 注意：GraphUtils.TryAcceptAll 返回的是「没塞进去的数量」，不是塞进去的数量，
                // 所以这里不看返回值，只看物品最后挂在哪个容器里。
                GraphUtils.TryAcceptAll(deck, cassette);
                if (cassette.parentInventory != deck)
                {
                    var storage = MachineHelper.GetBodyStorage(machine, 0);
                    if (storage != null)
                    {
                        GraphUtils.TryAcceptAll(storage, cassette);
                    }
                    if (cassette.parentInventory != null)
                    {
                        ModEntry.Log?.Log("磁带仓满了，磁带放进储藏格");
                        ShowStatus("磁带仓满了，这盘先放进储藏格了，拖到「磁带」格再点播放");
                        RefreshDeck(machine);
                        return;
                    }
                    ModEntry.Log?.Warning("点歌失败：磁带仓和储藏格都塞不下");
                    ShowStatus("磁带仓和储藏格都塞不下，先清几盘");
                    try
                    {
                        cassette.Destroy();
                    }
                    catch
                    {
                        // 已经不在任何容器里，Unity 那边会回收
                    }
                    return;
                }
            }

            var tracks = MachineCassettePlayer.GetTrackListFromCassette(deck);
            int index = tracks.IndexOf(trackId);
            if (index < 0)
            {
                ModEntry.Log?.Warning("点歌失败：磁带不在播放仓里 trackId=" + trackId);
                ShowStatus("这盘磁带不在播放仓里（可能在储藏格），先拖进来");
                RefreshDeck(machine);
                return;
            }

            StartTrack(machine, deck, trackId, index);
            ShowStatus("正在播放：" + Clean(song.Display));
        }

        private static GameItem CreateCassette(NcSong song, string trackId)
        {
            // 必须走目录生成：identifier / identifierName / uniqueId 都要有，
            // 否则提示框和点击会空引用，存档也还原不了这盘磁带。
            var cassette = ModHelper.SpawnItem(ModEntry.TapeItemId);
            if (cassette == null)
            {
                ModEntry.Log?.Warning("云磁带物品没注册上，SpawnItem 返回 null");
                return null;
            }
            cassette.name = "音乐磁带 - " + Clean(song.Display);
            CassetteHelper.InitCassetteItem(cassette, trackId);
            ModHelper.SetItemOwnerShip(cassette, true);
            ModEntry.Log?.Log("生成磁带 id=" + cassette.identifier + " uniqueId=" + cassette.uniqueId + " 曲目=" + trackId);
            return cassette;
        }

        private static GameItem FindCassette(GraphNodeStorage node, string trackId)
        {
            var inventory = node as GameInventory;
            if (inventory == null || string.IsNullOrEmpty(trackId)) return null;
            foreach (var item in inventory.childItems)
            {
                if (item == null) continue;
                if (!item.IsTag(CassetteHelper.CASSETTE_TAG)) continue;
                if (!item.IsTag(CassetteHelper.CASSETTE_CONTENT)) continue;
                if (item.GetTagReadonly(CassetteHelper.CASSETTE_CONTENT).GetString() == trackId) return item;
            }
            return null;
        }

        private static void StartTrack(GameItem machine, GameInventory deck, string trackId, int index)
        {
            ModEntry.Log?.Log("开始播放 " + trackId + "（第 " + index + " 盘）");
            AudioManager.StopAudioCoroutine();
            machine.ModifyTag(MachineCassettePlayer.CURRENT_PLAYING_CASSETTE_INDEX_TAG, tag => tag.Enable().SetInt(index));
            machine.ModifyTag(MachineCassettePlayer.CURRENT_PLAYING_TRACK_ID_TAG, tag => tag.Enable().SetString(trackId));
            machine.ModifyTag(MachineCassettePlayer.CASETTE_PLAYER_STATE_INT, tag => tag.Enable().SetInt(1));
            AudioManager.Instance.PlayMusic(trackId);
            AudioManager.Instance.PlayAndCallback(AudioManager.OnTrackDone);
            foreach (var item in deck.childItems)
            {
                var element = item as GameItemElement;
                if (element != null) element.RefreshPlayingFrame();
            }
        }

        /// <summary>窗口里那个「停止」：云曲目和原版播放器一起收掉，状态回到停止。</summary>
        private static void StopPlayback()
        {
            try
            {
                StreamPlayer.Cancel();
                AudioManager.StopAudioCoroutine();
                var machine = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.cassettePlayer : null;
                if (machine != null)
                {
                    machine.ModifyTag(MachineCassettePlayer.CASETTE_PLAYER_STATE_INT, tag =>
                    {
                        tag.Enable().SetInt(0);
                    });
                    RefreshDeck(machine);
                }
                SetText(NowPlayingId, " ");
                ShowStatus("已停止");
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("停止播放出错：" + e.Message);
            }
        }

        private static void RefreshDeck(GameItem machine)
        {
            var deck = MachineHelper.GetBodyStorage(machine, 1) as GameInventory;
            if (deck == null) return;
            foreach (var item in deck.childItems)
            {
                var element = item as GameItemElement;
                if (element != null) element.RefreshPlayingFrame();
            }
        }

        // ---------------------------------------------------------------- 磁带机面板

        /// <summary>在原版磁带机窗口右侧加一块「网易云」面板（Harmony Postfix 调用）。</summary>
        public static void AddCloudPanel(GameItem machine)
        {
            if (machine == null || machine.contentWindow == null)
            {
                return;
            }
            var row = machine.contentWindow.childElement as MachineryPanelRow;
            if (row == null)
            {
                ModEntry.Log?.Warning("磁带机窗口结构不是 MachineryPanelRow，跳过加面板");
                return;
            }
            if (row.GetPanel("cloud") != null)
            {
                return;
            }

            var button = new MachineryButton("云", Open);
            button.SetDescription("扫码登录网易云，点歌变成磁带，用这台机器播放");

            var controls = row.GetPanel("controls");
            var stack = controls != null ? controls.content as MachineryStack : null;
            if (stack != null)
            {
                stack.Add(button);
            }
            else
            {
                var panel = new MachineryPanel("网易云", MachineryStack.Horizontal(button), "cloud");
                row.Add(panel);
                controls = panel;
            }
            if (controls != null && controls.role == null)
            {
                controls.SetRole("cloud");
            }
            try
            {
                machine.contentWindow.Validate();
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("磁带机窗口重新排版失败：" + e.Message);
            }
            ModEntry.Log?.Log("磁带机加上了网易云按钮");
        }
    }
}
