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
        private static MachineryButton lyricButton;
        private static Sprite lyricOn;
        private static Sprite lyricOff;
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

            builder.AddLabel(AccountLine(), AccountId);
            builder.AddLabel(" ", MainStatusId);
            builder.AddSpace(6f);

            builder.BeginTabs();

            // 扫码
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

            // 搜索
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

            // 歌单
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

            // 没登录才显示二维码。已登录还留着图片控件的话，会露出预制体自带的占位图
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

        // 搜索

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
            searchPage = 0;
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
            trackPage = 0;
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
                    builder.AddButton("返回歌单列表", () =>
                    {
                        currentPlaylist = null;
                        trackPage = 0;
                        FillPlaylists();
                    });
                    builder.AddLabel("《" + Fit(Clean(currentPlaylist.Name), 44) + "》  " + playlistSongs.Count + " 首");
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
                        builder.AddLabel("（空）");
                    }
                    foreach (var item in playlists)
                    {
                        var playlist = item;
                        string label = (playlist.IsLiked ? "♥ " : "") + Clean(playlist.Name) + "   (" + playlist.TrackCount + " 首)";
                        builder.AddButton(Fit(label, 62), () => OpenPlaylist(playlist));
                        builder.WithTooltip(Clean(playlist.Name) + "（" + playlist.TrackCount + " 首）");
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
            trackPage = 0;
            SetText(PlaylistStatusId, "《" + playlist.Name + "》共 " + playlistSongs.Count + " 首。");
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
            SetText(NowPlayingId, "▶ " + Clean(song.Display));
            ShowStatus("正在播放：" + Clean(song.Display));
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
            cassette.name = "音乐磁带 - " + Clean(song.Display);
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

        // 磁带机面板

        // 播放模式那一行：顺序 / 循环 / 随机，当前那个亮着
        private static MachineryStack BuildModeRow(int size)
        {
            string[] keys = { "mode_seq", "mode_loop", "mode_shuffle" };
            string[] labels = { "顺序播放", "单曲循环", "随机播放" };
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
                button = new MachineryButton("词", ToggleLyrics);
            }
            else
            {
                lyricOn = ScaleSprite("icon_lyrics", source, size, 1f);
                lyricOff = ScaleSprite("icon_lyrics", source, size, 0.45f);
                button = new MachineryButton(lyricOn, ToggleLyrics, size);
            }
            button.SetDescription("开关桌面歌词");
            lyricButton = button;
            return button;
        }

        private static void ToggleLyrics()
        {
            Lyrics.Toggle();
            RefreshLyricButton();
        }

        private static void RefreshLyricButton()
        {
            if (lyricButton == null || lyricOn == null || lyricOff == null) return;
            lyricButton.SetIcon(Lyrics.IsOpen ? lyricOn : lyricOff);
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

            var button = MakeIconButton("icon_netease", "云", Open, transportSize);
            button.SetDescription("扫码登录网易云，点歌变成磁带，用这台机器播放");

            var lyricBtn = MakeLyricButton(transportSize);

            var lyricSetButton = MakeIconButton("icon_settings", "设", LyricPanel.Open, transportSize);
            lyricSetButton.SetDescription("歌词的字号、颜色和位置");

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
                var panel = new MachineryPanel("网易云", content, "cloud");
                row.Add(panel);
                controls = panel;
            }
            RefreshModeButtons();
            RefreshLyricButton();
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
            if (page > 0) builder.AddButton("上一页", () => go(page - 1));
            builder.AddLabel(" 第 " + (page + 1) + "/" + pages + " 页，共 " + total + " 首 ");
            if (page + 1 < pages) builder.AddButton("下一页", () => go(page + 1));
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
