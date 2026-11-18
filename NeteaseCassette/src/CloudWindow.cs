using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeteaseCassette
{
    // 网易云窗口：扫码、搜索、歌单，点歌生成磁带并直接播放
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
        private const int PageSize = 100;

        private static CustomUIWindow window;
        private static int searchPage;
        private static int trackPage;
        private static readonly Dictionary<string, Sprite> scaledSprites = new Dictionary<string, Sprite>();
        private static MachineryButton[] modeButtons;
        private static readonly List<(WeakReference<MachineryButton> Button, Sprite On, Sprite Off)> lyricButtons
            = new List<(WeakReference<MachineryButton>, Sprite, Sprite)>();
        private static Sprite[] modeIcons;
        private static Sprite[] modeDim;
        private static RectTransform searchHost;
        private static RectTransform playlistHost;

        private static int qrGeneration;
        private static string lastKeyword = string.Empty;
        private static List<NcSong> searchSongs = new List<NcSong>();
        private static List<NcSong> playlistSongs = new List<NcSong>();
        private static List<NcPlaylist> playlists = new List<NcPlaylist>();
        private static NcPlaylist currentPlaylist;

        // 窗口

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
                ShowStatus(Loc.T("deck_missing_full"));
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
            var builder = CustomUIManager.Window(WindowId, Loc.T("cloud_title"));
            if (builder == null) return;
            window = builder.Handle;

            bool loggedIn = ModEntry.Client != null && ModEntry.Client.IsLoggedIn;

            builder
                .SetSize(640f, 720f)
                .SetDraggable(true)
                .SetCloseOnEscape(true)
                .Center();

            builder.AddLabel(AccountLine(), AccountId);
            builder.AddLabel(" ", MainStatusId);
            builder.AddSpace(6f);

            builder.BeginTabs();

            // 扫码
            builder.BeginTab(Loc.T("tab_login"));
            builder.AddLabel(
                loggedIn
                    ? Loc.T("login_already")
                    : Loc.T("login_scan_hint"),
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
            builder.AddButton(Loc.T("btn_refresh_qr"), RefreshQr);
            builder.AddSpace(8f);
            builder.AddButton(Loc.T("btn_sign_out"), Logout);
            builder.AddFlexibleSpace();
            builder.End();
            builder.End();

            // 搜索
            builder.BeginTab(Loc.T("btn_search"));
            builder.BeginRow();
            builder.AddInput(Loc.T("search_placeholder"), lastKeyword, value => lastKeyword = value);
            builder.AddSpace(8f);
            builder.AddButton(Loc.T("btn_search"), () => StartSearch(lastKeyword));
            builder.End();
            builder.AddLabel(Loc.T("search_tip"), SearchStatusId);
            builder.BeginScrollFlexible(380f);
            searchHost = builder.CurrentContainer;
            builder.End();
            builder.End();

            // 歌单
            builder.BeginTab(Loc.T("tab_playlist"));
            builder.AddLabel(loggedIn ? Loc.T("playlist_tip") : Loc.T("playlist_need_login"), PlaylistStatusId);
            builder.BeginScrollFlexible(420f);
            playlistHost = builder.CurrentContainer;
            builder.End();
            builder.End();

            builder.EndTabs();

            builder.AddLabel(" ", NowPlayingId);
            builder.BeginRow();
            builder.AddFlexibleSpace();
            builder.AddButton(Loc.T("btn_stop"), StopPlayback);
            builder.AddSpace(8f);
            builder.AddButton(Loc.T("btn_close"), Close);
            builder.End();

            builder.Show();

            // 没登录才显示二维码。已登录还留着图片控件的话，会露出预制体自带的占位图
            SetVisible(QrElementId, false);

            FillSearch();
            if (loggedIn)
            {
                SetText(LoginStatusId, Loc.T("qr_unused"));
                FillPlaylists();
                LoadPlaylists();
            }
            else
            {
                FillPlaylistMessage(Loc.T("status_not_logged_in"));
                RefreshQr();
            }
        }

        private static string AccountLine()
        {
            var client = ModEntry.Client;
            if (client == null) return Loc.T("client_not_ready");
            if (!client.IsLoggedIn) return Loc.T("account_anonymous");
            string name = string.IsNullOrEmpty(client.Nickname) ? ("UID " + client.UserId) : client.Nickname;
            return Loc.T("login_as", name);
        }

        // 控件小工具

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

        // 歌名里的 emoji、变体选择符在游戏字体里是方块，显示前清掉
        internal static string Clean(string text)
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

        // 扫码流程

        private static void RefreshQr()
        {
            var client = ModEntry.Client;
            if (client == null)
            {
                ShowStatus(Loc.T("client_not_ready"));
                return;
            }
            if (client.IsLoggedIn)
            {
                SetText(LoginStatusId, AccountLine());
                return;
            }
            int generation = ++qrGeneration;
            SetText(LoginStatusId, Loc.T("qr_requesting"));
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
            SetText(LoginStatusId, Loc.T("qr_fail", error ?? Loc.T("net_down")));
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
                SetText(LoginStatusId, Loc.T("qr_render_fail"));
                yield break;
            }

            var sprite = QrSprite.Create(matrix, 7, 4);
            SetSprite(QrElementId, sprite);
            SetVisible(QrElementId, true);
            SetText(LoginStatusId, Loc.T("qr_waiting"));

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
                        ShowStatus(Loc.T("login_ok"));
                        FillPlaylists();
                        LoadPlaylists();
                        yield break;
                    case NeteaseClient.QrExpired:
                        SetText(LoginStatusId, Loc.T("qr_expired"));
                        yield break;
                    case NeteaseClient.QrScanned:
                        SetText(LoginStatusId, Loc.T("qr_scanned"));
                        break;
                    default:
                        SetText(LoginStatusId, string.IsNullOrEmpty(message) ? Loc.T("qr_waiting_short") : message);
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
            SetText(LoginStatusId, Loc.T("signed_out"));
            FillPlaylistMessage(Loc.T("status_not_logged_in"));
        }

        // 搜索

        private static void StartSearch(string keyword)
        {
            keyword = (keyword ?? string.Empty).Trim();
            if (keyword.Length == 0)
            {
                SetText(SearchStatusId, Loc.T("search_empty_kw"));
                return;
            }
            if (ModEntry.Client == null) return;
            lastKeyword = keyword;
            SetText(SearchStatusId, Loc.T("search_searching"));
            ModHelper.StartCoroutine(ModEntry.ModId, SearchRoutine(keyword));
        }

        private static IEnumerator SearchRoutine(string keyword)
        {
            List<NcSong> result = null;
            string error = null;
            yield return ModEntry.Client.Search(keyword, 30, songs => result = songs, message => error = message);
            if (!string.IsNullOrEmpty(error))
            {
                SetText(SearchStatusId, Loc.T("search_fail", error));
                yield break;
            }
            searchSongs = result ?? new List<NcSong>();
            searchPage = 0;
                SetText(SearchStatusId, Loc.T("search_found", searchSongs.Count));
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
                    builder.AddLabel(Loc.T("empty_list"));
                }
                else
                {
                    int pages = Mathf.Max(1, (searchSongs.Count + PageSize - 1) / PageSize);
                    searchPage = Mathf.Clamp(searchPage, 0, pages - 1);
                    int start = searchPage * PageSize;
                    int end = Mathf.Min(searchSongs.Count, start + PageSize);
                    for (int i = start; i < end; i++)
                    {
                        AddSongRow(builder, searchSongs[i], i + 1);
                    }
                    AddPager(builder, searchPage, pages, searchSongs.Count, page =>
                    {
                        searchPage = page;
                        FillSearch();
                    });
                }
                CustomUIBuilder.RebuildLayout(searchHost);
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("渲染搜索结果出错：" + e.Message);
            }
        }

        // 歌单

        private static void LoadPlaylists()
        {
            if (ModEntry.Client == null || !ModEntry.Client.IsLoggedIn) return;
            SetText(PlaylistStatusId, Loc.T("playlist_loading_list"));
            ModHelper.StartCoroutine(ModEntry.ModId, PlaylistRoutine());
        }

        private static IEnumerator PlaylistRoutine()
        {
            List<NcPlaylist> result = null;
            string error = null;
            yield return ModEntry.Client.GetPlaylists(lists => result = lists, message => error = message);
            if (!string.IsNullOrEmpty(error))
            {
            SetText(PlaylistStatusId, Loc.T("playlist_fail", error));
                yield break;
            }
            playlists = result ?? new List<NcPlaylist>();
            currentPlaylist = null;
            trackPage = 0;
            SetText(PlaylistStatusId, Loc.T("playlist_count", playlists.Count));
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
                    builder.AddButton(Loc.T("btn_back_playlists"), () =>
                    {
                        currentPlaylist = null;
                        trackPage = 0;
                        FillPlaylists();
                    });
                    builder.AddLabel(Loc.T("playlist_header", Fit(Clean(currentPlaylist.Name), 44), playlistSongs.Count));
                    int pages = Mathf.Max(1, (playlistSongs.Count + PageSize - 1) / PageSize);
                    trackPage = Mathf.Clamp(trackPage, 0, pages - 1);
                    int start = trackPage * PageSize;
                    int end = Mathf.Min(playlistSongs.Count, start + PageSize);
                    for (int i = start; i < end; i++)
                    {
                        AddSongRow(builder, playlistSongs[i], i + 1);
                    }
                    AddPager(builder, trackPage, pages, playlistSongs.Count, page =>
                    {
                        trackPage = page;
                        FillPlaylists();
                    });
                }
                else
                {
                    if (playlists.Count == 0)
                    {
                        builder.AddLabel(Loc.T("empty_list"));
                    }
                    foreach (var item in playlists)
                    {
                        var playlist = item;
                string label = (playlist.IsLiked ? "♥ " : "") + Clean(playlist.Name) + "   (" + Loc.T("tracks_count", playlist.TrackCount) + ")";
                        builder.AddButton(Fit(label, 62), () => OpenPlaylist(playlist));
                builder.WithTooltip(Loc.T("playlist_row", Clean(playlist.Name), playlist.TrackCount));
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
            SetText(PlaylistStatusId, Loc.T("playlist_loading", playlist.Name));
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
            SetText(PlaylistStatusId, Loc.T("read_fail", error));
                yield break;
            }
            playlistSongs = result ?? new List<NcSong>();
            trackPage = 0;
            SetText(PlaylistStatusId, Loc.T("playlist_loaded", playlist.Name, playlistSongs.Count));
            FillPlaylists();
        }

        // 点歌

        private static void PlaySong(NcSong song)
        {
            if (song == null) return;
            ModEntry.Log?.Log("点歌：" + song.Name + " (" + song.Id + ")");
            var machine = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.cassettePlayer : null;
            if (machine == null)
            {
                ModEntry.Log?.Warning("点歌失败：磁带机不在");
                ShowStatus(Loc.T("deck_missing_hint"));
                return;
            }

            var deck = MachineCassettePlayer.GetCassetteInventory();
            if (deck == null)
            {
                ModEntry.Log?.Warning("点歌失败：找不到磁带仓");
                ShowStatus(Loc.T("bay_missing_short"));
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
                    ShowStatus(Loc.T("spawn_fail"));
                    return;
                }
                // TryAcceptAll 返回的是「没塞进去的数量」，不是塞进去的数量，别拿它判断成败。
                // 这里只看物品最后挂在哪个容器里。
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
                        ShowStatus(Loc.T("deck_full_hint"));
                        RefreshDeck(machine);
                        return;
                    }
                    ModEntry.Log?.Warning("点歌失败：磁带仓和储藏格都塞不下");
                    ShowStatus(Loc.T("no_room_short"));
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
                ShowStatus(Loc.T("not_in_deck_hint"));
                RefreshDeck(machine);
                return;
            }

            StartTrack(machine, deck, trackId, index);
            SetText(NowPlayingId, "▶ " + Clean(song.Display));
            ShowStatus(Loc.T("now_playing", Clean(song.Display)));
        }

        private static GameItem CreateCassette(NcSong song, string trackId)
        {
            // 走目录生成，identifier 和 uniqueId 才有。自己 new 的话提示框、
            // 点击都会空引用，存档也认不回来。
            var cassette = ModHelper.SpawnItem(ModEntry.TapeItemId);
            if (cassette == null)
            {
                ModEntry.Log?.Warning("云磁带物品没注册上，SpawnItem 返回 null");
                return null;
            }
            cassette.name = Loc.T("tape_name", Clean(song.Display));
            CassetteHelper.InitCassetteItem(cassette, trackId);
            ModHelper.SetItemOwnerShip(cassette, true);
            ModEntry.Log?.Log("生成磁带 id=" + cassette.identifier + " uniqueId=" + cassette.uniqueId + " 曲目=" + trackId);
            return cassette;
        }

        private static GameItem FindCassette(GraphNodeStorage node, string trackId)
        {
            // 磁带也可能在放进仓里的收纳盒里，一路翻下去
            return CassetteScan.FindIn(node, trackId);
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

        // 窗口里的「停止」：云曲目和原版播放器一起收掉
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
                ShowStatus(Loc.T("stopped"));
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

        // 磁带机面板

        // 播放模式那一行：顺序 / 循环 / 随机，当前那个亮着
        private static MachineryStack BuildModeRow(int size)
        {
            string[] keys = { "mode_seq", "mode_loop", "mode_shuffle" };
            string[] labels = { Loc.T("mode_seq"), Loc.T("mode_loop"), Loc.T("mode_shuffle") };
            int[] values = { PlaybackMode.Sequential, PlaybackMode.Loop, PlaybackMode.Shuffle };

            modeButtons = new MachineryButton[3];
            modeIcons = new Sprite[3];
            modeDim = new Sprite[3];

            var row = new MachineryStack(false, 4);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                Sprite source = LoadModSprite(keys[i]);
                MachineryButton button;
                if (source != null)
                {
                    modeIcons[i] = ScaleSprite(keys[i], source, size, 1f);
                    modeDim[i] = ScaleSprite(keys[i], source, size, 0.45f);
                    button = new MachineryButton(modeIcons[i], () => { PlaybackMode.Set(values[index]); RefreshModeButtons(); }, size);
                }
                else
                {
                    button = new MachineryButton(labels[index], () => { PlaybackMode.Set(values[index]); RefreshModeButtons(); });
                }
                button.SetDescription(labels[index]);
                modeButtons[i] = button;
                row.Add(button);
            }
            return row;
        }

        private static void RefreshModeButtons()
        {
            if (modeButtons == null) return;

            int mode = PlaybackMode.Mode;
            int[] values = { PlaybackMode.Sequential, PlaybackMode.Loop, PlaybackMode.Shuffle };
            for (int i = 0; i < modeButtons.Length; i++)
            {
                var button = modeButtons[i];
                if (button == null || modeIcons == null || modeIcons[i] == null) continue;
                button.SetIcon(values[i] == mode ? modeIcons[i] : modeDim[i]);
            }
        }

        private static Sprite LoadModSprite(string key)
        {
            var mod = ModLoader.Mods.Find(m => m.Manifest.ID == ModEntry.ModId);
            Sprite sprite;
            if (mod != null && mod.Sprites.TryGetValue(key, out sprite)) return sprite;
            return null;
        }

        // 有图标就用图标，没有就退回文字，免得素材漏了按钮变空白
        private static MachineryButton MakeIconButton(string key, string fallbackText, Action callback, int size)
        {
            Sprite source = LoadModSprite(key);
            if (source == null) return new MachineryButton(fallbackText, callback);
            return new MachineryButton(ScaleSprite(key, source, size, 1f), callback, size);
        }

        // 歌词开关要能看出开没开：开着用亮图标，关着压暗
        private static MachineryButton MakeLyricButton(int size)
        {
            Sprite source = LoadModSprite("icon_lyrics");
            MachineryButton button;
            if (source == null)
            {
                button = new MachineryButton(Loc.T("btn_lyrics_word"), Lyrics.Toggle);
            }
            else
            {
                Sprite on = ScaleSprite("icon_lyrics", source, size, 1f);
                Sprite off = ScaleSprite("icon_lyrics", source, size, 0.45f);
                button = new MachineryButton(Lyrics.IsOpen ? on : off, Lyrics.Toggle, size);
                lyricButtons.RemoveAll(entry => !entry.Button.TryGetTarget(out var existing) || existing.handler == null);
                lyricButtons.Add((new WeakReference<MachineryButton>(button), on, off));
            }
            button.SetDescription(Loc.T("tip_lyrics"));
            return button;
        }

        internal static void RefreshLyricButtons()
        {
            bool isOpen = Lyrics.IsOpen;
            for (int i = lyricButtons.Count - 1; i >= 0; i--)
            {
                var entry = lyricButtons[i];
                if (!entry.Button.TryGetTarget(out var button) || button.handler == null)
                {
                    lyricButtons.RemoveAt(i);
                    continue;
                }

                Sprite icon = isOpen ? entry.On : entry.Off;
                if (button.icon != icon) button.SetIcon(icon);
            }
        }

        // 把 32px 的图标缩到跟原版按钮一样大，顺便压暗（tint < 1）
        private static Sprite ScaleSprite(string keyName, Sprite source, int size, float tint)
        {
            if (source == null || source.texture == null) return source;

            // 物品贴图没有名字（全是空串），缓存必须用传进来的 key，不然所有按钮会共用一个图
            string key = keyName + "_" + size + "_" + Mathf.RoundToInt(tint * 100f);
            Sprite cached;
            if (scaledSprites.TryGetValue(key, out cached)) return cached;

            try
            {
                Rect rect = source.rect;
                int sx = (int)rect.xMin;
                int sy = (int)rect.yMin;
                int sw = Mathf.Max(1, (int)rect.width);
                int sh = Mathf.Max(1, (int)rect.height);

                var colors = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    int y0 = sy + y * sh / size;
                    int y1 = Mathf.Max(y0 + 1, sy + (y + 1) * sh / size);
                    for (int x = 0; x < size; x++)
                    {
                        int x0 = sx + x * sw / size;
                        int x1 = Mathf.Max(x0 + 1, sx + (x + 1) * sw / size);

                        float r = 0f, g = 0f, b = 0f, a = 0f;
                        int count = 0;
                        for (int py = y0; py < y1; py++)
                        {
                            for (int px = x0; px < x1; px++)
                            {
                                Color c = source.texture.GetPixel(px, py);
                                r += c.r * c.a;
                                g += c.g * c.a;
                                b += c.b * c.a;
                                a += c.a;
                                count++;
                            }
                        }

                        colors[y * size + x] = (count == 0 || a <= 0.001f)
                            ? Color.clear
                            : new Color(r / a * tint, g / a * tint, b / a * tint, a / count);
                    }
                }

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.SetPixels(colors);
                texture.Apply(false, false);

                var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = key;
                scaledSprites[key] = sprite;
                return sprite;
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("缩放按钮图标失败：" + e.Message);
                return source;
            }
        }

        // 给原版磁带机窗口加按钮，Harmony Postfix 里调
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

            var controls = row.GetPanel("controls");
            var stack = controls != null ? controls.content as MachineryStack : null;

            // 原版的四个播放键是横排的；已经被我们换成一竖列就说明这窗口排过了
            if (stack != null && stack.vertical) return;

            // 照原版播放键的实际尺寸做，别自己定大小
            int transportSize = 16;
            if (stack != null && stack.elements.Count > 0 && stack.elements[0] != null)
            {
                transportSize = Mathf.Max(8, stack.elements[0].heightPixels);
            }
            ModEntry.Log?.Log("按钮按 " + transportSize + "px 排");

            var button = MakeIconButton("icon_netease", Loc.T("btn_cloud_word"), Open, transportSize);
            button.SetDescription(Loc.T("tip_cloud"));

            var lyricBtn = MakeLyricButton(transportSize);

            var lyricSetButton = MakeIconButton("icon_settings", Loc.T("btn_settings_word"), LyricPanel.Open, transportSize);
            lyricSetButton.SetDescription(Loc.T("tip_settings"));

            var modeRow = BuildModeRow(transportSize);
            if (stack != null)
            {
                // 上面一行放我们的按钮，中间是原版播放键，下面一行是播放模式
                var outer = new MachineryStack(true, 6);
                controls.SetContent(outer);
                stack.ResizePixels(0, 0);   // 它之前被面板撑满了整块，不清掉会把行距拉开
                outer.Add(MachineryStack.Horizontal(button, lyricBtn, lyricSetButton));
                outer.Add(stack);
                outer.Add(modeRow);
            }
            else
            {
                var content = MachineryStack.Vertical(
                    MachineryStack.Horizontal(button, lyricBtn, lyricSetButton),
                    modeRow);
                var panel = new MachineryPanel(Loc.T("panel_cloud"), content, "cloud");
                row.Add(panel);
                controls = panel;
            }
            RefreshModeButtons();
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

        // 列表行

        // 列表超过一页时的翻页条
        private static void AddPager(CustomUIBuilder builder, int page, int pages, int total, Action<int> go)
        {
            if (pages <= 1) return;

            builder.BeginRow();
            if (page > 0) builder.AddButton(Loc.T("btn_prev_page"), () => go(page - 1));
            builder.AddLabel(" " + Loc.T("pager", page + 1, pages, total) + " ");
            if (page + 1 < pages) builder.AddButton(Loc.T("btn_next_page"), () => go(page + 1));
            builder.End();
        }

        private static void AddSongRow(CustomUIBuilder builder, NcSong song, int index)
        {
            if (song == null) return;
            string name = Clean(song.Name);
            string artists = Clean(song.Artists);
            string label = index.ToString("00") + "  " + name;
            if (!string.IsNullOrEmpty(artists))
            {
                label += " — " + artists;
            }
            if (song.DurationMs > 0)
            {
                label += "   " + Duration(song.DurationMs);
            }
            builder.AddButton(Fit(label, 74), () => PlaySong(song));
            builder.WithTooltip(Clean(song.Display) + (song.DurationMs > 0 ? "  " + Duration(song.DurationMs) : ""));
        }

        private static string Duration(int milliseconds)
        {
            int totalSeconds = milliseconds / 1000;
            return (totalSeconds / 60).ToString("0") + ":" + (totalSeconds % 60).ToString("00");
        }

        // 按显示宽度截断（中文算 2 格），超长补省略号，免得按钮里被硬切
        private static string Fit(string text, int maxUnits)
        {
            if (string.IsNullOrEmpty(text)) return text;
            int units = 0;
            var builder = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                int width = c >= 0x1100 ? 2 : 1;
                if (units + width > maxUnits)
                {
                    builder.Append('…');
                    break;
                }
                builder.Append(c);
                units += width;
            }
            return builder.ToString();
        }
    }
}
