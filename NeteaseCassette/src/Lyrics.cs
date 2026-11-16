using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeteaseCassette
{
    // 桌面歌词：屏幕上一块字，当前句亮、下一句暗，没有窗口框也不挡点击
    // 横排就是两句横着叠起来，竖排是字一个一个竖着排（两列，当前句在右）
    public static class Lyrics
    {
        private const int CacheLimit = 30;
        private const int SortingOrder = 30000;
        private const string SettingsFile = "lyrics.json";

        public static readonly string[] ColorNames = { "白", "黄", "青", "粉", "绿" };

        private static readonly Color[] Colors =
        {
            new Color(1f, 1f, 1f, 1f),
            new Color(1f, 0.89f, 0.48f, 1f),
            new Color(0.50f, 0.91f, 0.88f, 1f),
            new Color(1f, 0.62f, 0.77f, 1f),
            new Color(0.62f, 0.94f, 0.54f, 1f)
        };

        private const string DimHex = "#8A8D99C0";
        private static readonly Color NextColor = new Color(0.73f, 0.74f, 0.79f, 0.78f);

        // 一行歌词：时间和内容（秒）
        private struct Row
        {
            public float Time;
            public string Text;
        }

        private static GameObject canvasObject;
        private static RectTransform canvasRect;
        private static RectTransform root;
        private static TextMeshProUGUI textMain;
        private static TextMeshProUGUI textNext;
        private static TMP_FontAsset font;
        private static LyricSettings settings;

        private static bool running;
        private static long songId;
        private static string songName = "";
        private static List<Row> rows = new List<Row>();
        private static bool instrumental;
        private static string loadError;
        private static int currentRow = -1;

        private static Color shownColor;
        private static float playbackTime = -1f;
        private static int shownSplit = int.MinValue;
        private static int shownRow = int.MinValue;
        private static long shownSongId = -1;
        private static bool needFit = true;

        private static bool dragging;
        private static Vector2 dragStartMouse;
        private static Vector2 dragStartPos;

        private static readonly Dictionary<long, List<Row>> cache = new Dictionary<long, List<Row>>();

        public static LyricSettings Settings
        {
            get
            {
                EnsureSettings();
                return settings;
            }
        }

        private static bool Visible
        {
            get { return canvasObject != null && canvasObject.activeSelf && textMain != null; }
        }

        public static bool IsOpen
        {
            get { return Visible; }
        }

        public static void Toggle()
        {
            if (Visible) Close();
            else Open();
        }

        public static void Open()
        {
            EnsureSettings();
            if (!EnsureCanvas()) return;

            currentRow = -1;
            needFit = true;
            Render(true);
            StartRunner();

            if (songId > 0 && rows.Count == 0 && !instrumental && loadError == null)
            {
                ModHelper.StartCoroutine(ModEntry.ModId, Load(songId));
            }
        }

        public static void Close()
        {
            if (canvasObject != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(canvasObject);
                }
                catch
                {
                    // 场景切换时已经跟着没了
                }
            }
            canvasObject = null;
            canvasRect = null;
            root = null;
            textMain = null;
            textNext = null;
            dragging = false;
            currentRow = -1;
        }

        public static void Shutdown()
        {
            Close();
            cache.Clear();
            settings = null;
            songId = 0;
            songName = "";
            rows = new List<Row>();
        }

        // 换歌。name 为 null 的时候自己去磁带上找名字
        public static void OnTrackStarted(long id, string name)
        {
            songId = id;
            songName = name ?? "";
            currentRow = -1;
            loadError = null;

            List<Row> cached;
            if (id > 0 && cache.TryGetValue(id, out cached) && cached != null)
            {
                rows = cached;
                instrumental = rows.Count == 0;
                Render(true);
                return;
            }

            rows = new List<Row>();
            instrumental = false;
            Render(true);

            if (id > 0) ModHelper.StartCoroutine(ModEntry.ModId, Load(id));
        }

        public static void OnStopped()
        {
            currentRow = -1;
            Render(true);
        }

        // 设置


        public static void ChangeFontSize(float delta)
        {
            Settings.FontSize = Mathf.Clamp(Settings.FontSize + delta, 16f, 64f);
            SaveSettings();
            needFit = true;
            Render(true);
        }

        public static void SetColor(int index)
        {
            Settings.ColorIndex = Mathf.Clamp(index, 0, Colors.Length - 1);
            SaveSettings();
            Render(true);
        }

        public static void ToggleLock()
        {
            Settings.Locked = !Settings.Locked;
            SaveSettings();
        }

        public static void ResetPosition()
        {
            Settings.PosX = 0f;
            Settings.PosY = 200f;
            SaveSettings();
            needFit = true;
            ApplyPosition();
        }

        public static string StatusLine()
        {
            var s = Settings;
            string color = ColorNames[Mathf.Clamp(s.ColorIndex, 0, ColorNames.Length - 1)];
            string pos = s.Locked ? "位置已锁定" : "点歌词本身可以拖";
            return Mathf.RoundToInt(s.FontSize) + " 号 · " + color + " · " + pos;
        }

        private static void EnsureSettings()
        {
            if (settings != null) return;
            settings = new LyricSettings();

            try
            {
                string json = ModHelper.ReadModDataFileText(ModEntry.ModId, SettingsFile);
                if (!string.IsNullOrEmpty(json))
                {
                    var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<LyricSettings>(json);
                    if (loaded != null) settings = loaded;
                }
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("读歌词设置失败：" + e.Message);
            }

            settings.FontSize = Mathf.Clamp(settings.FontSize, 16f, 64f);
            settings.ColorIndex = Mathf.Clamp(settings.ColorIndex, 0, Colors.Length - 1);
        }

        private static void SaveSettings()
        {
            try
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(settings, Newtonsoft.Json.Formatting.Indented);
                ModHelper.WriteModDataFileText(ModEntry.ModId, SettingsFile, json);
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("存歌词设置失败：" + e.Message);
            }
        }

        // 画布

        private static bool EnsureCanvas()
        {
            if (canvasObject != null)
            {
                canvasObject.SetActive(true);
                return true;
            }

            font = FindFont();
            if (font == null)
            {
                ModEntry.Log?.Warning("找不到中文字体，歌词显示不了");
                return false;
            }

            canvasObject = new GameObject("netease_cassette_lyrics", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            canvasRect = (RectTransform)canvasObject.transform;

            var rootGo = new GameObject("lyric_root", typeof(RectTransform));
            rootGo.transform.SetParent(canvasObject.transform, false);
            root = (RectTransform)rootGo.transform;
            root.anchorMin = new Vector2(0.5f, 0f);
            root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0.5f);

            textMain = CreateText("lyric_main");
            textNext = CreateText("lyric_next");

            ModEntry.Log?.Log("歌词浮层建好了，字体 " + font.name);

            // 画布第一帧的尺寸还是 0，等一帧再摆位置，不然会被钳到奇怪的地方
            ModHelper.StartCoroutine(ModEntry.ModId, FirstLayout());
            return true;
        }

        private static TextMeshProUGUI CreateText(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(root, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.alignment = TextAlignmentOptions.Top;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.richText = true;
            text.outlineWidth = 0.22f;
            text.outlineColor = new Color32(0, 0, 0, 240);

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            return text;
        }

        private static IEnumerator FirstLayout()
        {
            yield return null;
            yield return null;
            if (!Visible) yield break;

            needFit = true;
            Render(true);

            ModEntry.Log?.Log("歌词布局：画布 " + (canvasRect != null ? canvasRect.rect.width + "x" + canvasRect.rect.height : "?") +
                              "，字号 " + textMain.fontSize + "，块 " + root.sizeDelta.x + "x" + root.sizeDelta.y +
                              "，位置 " + root.anchoredPosition.x + "," + root.anchoredPosition.y);
        }

        // 游戏 UI 里随便找个带中文的 TMP 字体，找不到就用默认的
        private static TMP_FontAsset FindFont()
        {
            TMP_FontAsset first = null;
            try
            {
                var texts = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    var candidate = texts[i] != null ? texts[i].font : null;
                    if (candidate == null) continue;
                    if (first == null) first = candidate;
                    try
                    {
                        if (candidate.HasCharacter('的', true)) return candidate;
                    }
                    catch
                    {
                        // 有些字体资源不让查，跳过
                    }
                }
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("扫场景里的字体失败：" + e.Message);
            }

            if (first != null) return first;
            try
            {
                return TMP_Settings.defaultFontAsset;
            }
            catch
            {
                return null;
            }
        }

        // 歌词数据

        private static IEnumerator Load(long id)
        {
            if (ModEntry.Client == null)
            {
                loadError = "客户端没准备好";
                Render(true);
                yield break;
            }

            NcLyric lyric = null;
            string error = null;
            yield return ModEntry.Client.GetLyric(id, l => lyric = l, e => error = e);

            if (id != songId) yield break;   // 已经换歌了，这份扔掉

            if (error != null || lyric == null)
            {
                loadError = error ?? "拿不到歌词";
                rows = new List<Row>();
                Render(true);
                yield break;
            }

            rows = ParseLrc(lyric.Raw);
            instrumental = rows.Count == 0 || (lyric.Instrumental && string.IsNullOrEmpty(lyric.Raw));
            if (rows.Count == 0) loadError = null;

            if (cache.Count >= CacheLimit) cache.Clear();
            cache[id] = rows;

            currentRow = -1;
            Render(true);
        }

        private static void StartRunner()
        {
            if (running) return;
            running = true;
            ModHelper.StartCoroutine(ModEntry.ModId, Run());
        }

        private static IEnumerator Run()
        {
            try
            {
                while (Visible)
                {
                    try
                    {
                        Tick();
                    }
                    catch (Exception e)
                    {
                        // 一帧出错不能让整个刷新循环停掉
                        ModEntry.Log?.Warning("歌词刷新出错：" + e.Message);
                    }
                    yield return null;
                }
            }
            finally
            {
                running = false;
            }
        }

        private static void Tick()
        {
            float time = -1f;
            try
            {
                if (StreamPlayer.CurrentSongId > 0 && AudioManager.Exists)
                {
                    var source = AudioManager.Instance.audioSourceMusic;
                    if (source != null && source.clip != null) time = source.time;
                }
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("读播放进度失败：" + e.Message);
            }

            playbackTime = time;

            if (time >= 0f && rows.Count > 0)
            {
                int index = FindRow(time);
                if (index != currentRow) currentRow = index;
            }
            else if (currentRow != -1)
            {
                currentRow = -1;
            }

            // 每帧都算一次：同一句里进度往前挪也要重新上色，Render 自己会比对
            Render();
            HandleDrag();
        }

        private static void HandleDrag()
        {
            var s = settings;
            if (s == null || root == null) return;

            if (s.Locked)
            {
                dragging = false;
                return;
            }

            Vector3 mouse = Input.mousePosition;
            if (!dragging)
            {
                if (Input.GetMouseButtonDown(0) && RectTransformUtility.RectangleContainsScreenPoint(root, mouse, null))
                {
                    dragging = true;
                    dragStartMouse = mouse;
                    dragStartPos = root.anchoredPosition;
                }
                return;
            }

            if (Input.GetMouseButton(0))
            {
                var canvas = canvasObject.GetComponent<Canvas>();
                float scale = canvas != null ? Mathf.Max(0.01f, canvas.scaleFactor) : 1f;
                Vector2 pos = dragStartPos + ((Vector2)mouse - dragStartMouse) / scale;
                root.anchoredPosition = ClampToCanvas(pos, root.sizeDelta);
            }
            else
            {
                dragging = false;
                Vector2 final = root.anchoredPosition;
                if (final != new Vector2(s.PosX, s.PosY))
                {
                    s.PosX = final.x;
                    s.PosY = final.y;
                    SaveSettings();
                }
            }
        }

        // 二分找当前行：最后一个时间 <= time 的
        private static int FindRow(float time)
        {
            int lo = 0;
            int hi = rows.Count - 1;
            int found = 0;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (rows[mid].Time <= time)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return found;
        }

        private static void Render(bool force = false)
        {
            if (!Visible) return;

            int split = ComputeSplit();
            bool rowChanged = currentRow != shownRow || songId != shownSongId;
            Color color = Colors[Mathf.Clamp(Settings.ColorIndex, 0, Colors.Length - 1)];

            // 大多数帧什么都没变，比一下几个整数就退出了
            if (!force && !rowChanged && !needFit && split == shownSplit && color == shownColor) return;

            shownSplit = split;
            shownRow = currentRow;
            shownSongId = songId;
            shownColor = color;

            string main;
            string next;
            Compose(out main, out next, split);

            textMain.color = color;
            textNext.color = NextColor;
            textMain.text = main;
            textNext.text = next;

            // 只有换行/改设置才重新量尺寸，逐字上色不动布局
            if (force || rowChanged || needFit)
            {
                Relayout();
                needFit = false;
            }
        }

        // 当前句该亮到第几个字；-1 表示整句都亮
        private static int ComputeSplit()
        {
            if (rows.Count == 0 || currentRow < 0) return -1;

            float progress = Progress();
            if (progress <= 0.001f) return 0;
            if (progress >= 0.999f) return -1;
            return Mathf.Clamp(Mathf.RoundToInt(rows[currentRow].Text.Length * progress), 1, rows[currentRow].Text.Length - 1);
        }


        private static void Compose(out string current, out string next, int split)
        {
            current = "";
            next = "";

            if (StreamPlayer.CurrentSongId <= 0)
            {
                current = "没有在放网易云";
                return;
            }
            if (!string.IsNullOrEmpty(loadError))
            {
                current = "歌词没拿到：" + loadError;
                return;
            }
            if (instrumental)
            {
                current = string.IsNullOrEmpty(songName) ? "纯音乐，没有歌词" : songName + "（纯音乐）";
                return;
            }
            if (rows.Count == 0)
            {
                current = "正在找歌词…";
                return;
            }

            int index = currentRow < 0 ? 0 : currentRow;
            current = Highlight(rows[index].Text, split);
            next = index + 1 < rows.Count ? rows[index + 1].Text : "";
        }

        // 唱过的亮着，没唱到的压暗
        private static string Highlight(string body, int split)
        {
            if (string.IsNullOrEmpty(body) || split < 0) return body;
            if (split <= 0) return "<color=" + DimHex + ">" + body + "</color>";
            if (split >= body.Length) return body;
            return body.Substring(0, split) + "<color=" + DimHex + ">" + body.Substring(split) + "</color>";
        }

        // 当前句唱到哪了（0~1），下一句的时间当结尾，没有就按 4 秒算
        private static float Progress()
        {
            int index = currentRow;
            if (index < 0 || index >= rows.Count || playbackTime < 0f) return 1f;

            float start = rows[index].Time;
            float end = index + 1 < rows.Count ? rows[index + 1].Time : start + 4f;
            float span = Mathf.Max(0.6f, end - start);
            return Mathf.Clamp01((playbackTime - start) / span);
        }

        // 量尺寸、摆两段文字；太长就缩字号，实在不行横排自动换行
        private static void Relayout()
        {
            if (root == null) return;

            var s = settings;
            float maxWidth = canvasRect != null && canvasRect.rect.width > 100f ? canvasRect.rect.width - 80f : 1600f;
            float maxHeight = canvasRect != null && canvasRect.rect.height > 100f ? canvasRect.rect.height - 120f : 900f;

            float baseSize = Mathf.Clamp(s.FontSize, 16f, 64f);
            baseSize = Mathf.Min(baseSize, Mathf.Max(18f, maxWidth * 0.03f));

            const float gap = 8f;
            float mainW = 0f, mainH = 0f, nextW = 0f, nextH = 0f;
            bool hasNext = !string.IsNullOrEmpty(textNext.text);

            for (int attempt = 0; attempt < 8; attempt++)
            {
                textMain.fontSize = baseSize;
                textNext.fontSize = Mathf.Max(12f, baseSize * 0.72f);

                var main = textMain.GetPreferredValues(textMain.text, maxWidth, 5000f);
                var next = hasNext ? textNext.GetPreferredValues(textNext.text, maxWidth, 5000f) : Vector2.zero;

                mainW = main.x;
                mainH = main.y;
                nextW = next.x;
                nextH = next.y;

                bool tooWide = Mathf.Max(mainW, nextW) > maxWidth;
                bool tooTall = mainH + (hasNext ? gap + nextH : 0f) > maxHeight;
                if (!tooWide && !tooTall) break;
                if (baseSize <= 12f) break;
                baseSize *= 0.9f;
            }

            // 多留几像素，免得 TMP 因为取整提前换行
            const float pad = 10f;
            float mainWidth = mainW + pad;
            float nextWidth = nextW + pad;
            float width = Mathf.Max(mainWidth, nextWidth);
            float height = mainH + (hasNext ? gap + nextH : 0f);

            root.sizeDelta = new Vector2(Mathf.Max(32f, width), Mathf.Max(24f, height));

            // 子节点锚在 root 左上角，y 向下为负
            textMain.rectTransform.sizeDelta = new Vector2(Mathf.Max(8f, mainWidth), Mathf.Max(8f, mainH));
            textNext.rectTransform.sizeDelta = new Vector2(Mathf.Max(8f, nextWidth), Mathf.Max(8f, nextH));
            textMain.rectTransform.anchoredPosition = new Vector2((width - mainWidth) * 0.5f, 0f);
            textNext.rectTransform.anchoredPosition = new Vector2((width - nextWidth) * 0.5f, -mainH - gap);

            ApplyPosition();
        }
        private static void ApplyPosition()
        {
            if (root == null || settings == null) return;
            if (canvasRect == null || canvasRect.rect.width <= 100f) return;

            Vector2 pos = ClampToCanvas(new Vector2(settings.PosX, settings.PosY), root.sizeDelta);
            root.anchoredPosition = pos;
        }

        private static Vector2 ClampToCanvas(Vector2 pos, Vector2 size)
        {
            if (canvasRect == null) return pos;
            float halfW = canvasRect.rect.width * 0.5f;
            float height = canvasRect.rect.height;
            float margin = 12f;

            pos.x = Mathf.Clamp(pos.x, -halfW + size.x * 0.5f, halfW - size.x * 0.5f);
            pos.y = Mathf.Clamp(pos.y, size.y * 0.5f + margin, height - size.y * 0.5f - margin);
            return pos;
        }

        // LRC：一行可能带好几个时间戳，也可能什么都没有
        private static List<Row> ParseLrc(string raw)
        {
            var list = new List<Row>();
            if (string.IsNullOrEmpty(raw)) return list;

            string[] lines = raw.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] != '[') continue;

                var stamps = new List<float>();
                string rest = line;
                while (rest.Length > 0 && rest[0] == '[')
                {
                    int close = rest.IndexOf(']');
                    if (close <= 0) break;
                    float seconds;
                    if (TryParseStamp(rest.Substring(1, close - 1), out seconds)) stamps.Add(seconds);
                    rest = rest.Substring(close + 1);
                }

                string body = rest.Trim();
                if (body.Length == 0 || stamps.Count == 0) continue;
                if (IsCredit(body)) continue;

                // 时间全是 0 又带冒号的，基本是「Cello：xxx」这类演奏名单
                bool allZero = true;
                for (int s = 0; s < stamps.Count; s++)
                {
                    if (stamps[s] > 0.01f)
                    {
                        allZero = false;
                        break;
                    }
                }
                if (allZero && HasColonNear(body)) continue;

                body = CloudWindow.Clean(body);
                for (int s = 0; s < stamps.Count; s++)
                {
                    list.Add(new Row { Time = stamps[s], Text = body });
                }
            }

            list.Sort((a, b) => a.Time.CompareTo(b.Time));
            return list;
        }

        private static bool TryParseStamp(string tag, out float seconds)
        {
            seconds = 0f;
            int colon = tag.IndexOf(':');
            if (colon <= 0 || colon >= tag.Length - 1) return false;

            int minutes;
            if (!int.TryParse(tag.Substring(0, colon), NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes))
            {
                return false;
            }

            float part;
            if (!float.TryParse(tag.Substring(colon + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out part))
            {
                return false;
            }

            seconds = minutes * 60f + part;
            return true;
        }

        // 网易云把作词作曲这些也写进歌词里，时间都是 0，不滤掉开头会挤一堆
        private static readonly string[] CreditRoles =
        {
            "作词", "作曲", "编曲", "制作", "监制", "出品", "混音", "母带", "录音",
            "和声", "吉他", "贝斯", "鼓", "键盘", "弦乐", "统筹", "企划", "发行", "OP", "SP"
        };

        private static bool IsCredit(string body)
        {
            int colon = body.IndexOfAny(new[] { ':', '：' });
            if (colon < 0 || colon > 20) return false;

            string head = body.Substring(0, colon).Trim();
            for (int i = 0; i < CreditRoles.Length; i++)
            {
                // 「作词 : X」和「作词 Lyricist：X」都要挡掉
                if (head.StartsWith(CreditRoles[i], StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool HasColonNear(string body)
        {
            int colon = body.IndexOfAny(new[] { ':', '：' });
            return colon >= 0 && colon <= 30;
        }
    }
}
