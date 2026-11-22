using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Chemistry
{
    /// <summary>
    /// 游戏自带的 CustomUI 预制体上的 TMP 字体只带一部分字形，模组自己写进去的中文会画成方块
    /// （按钮文字、tooltip 都这样）。这里在窗口显示期间定时扫一遍，把画不出这段文字的 TMP 换成
    /// 游戏里能画的中文字体；tooltip 单独常驻处理。
    ///
    /// 用法：窗口建好后 ChemFontFix.Attach(window) 就行；模组启用时也会起一份常驻扫描，
    /// 按窗口 id 兜底（窗口重开/重建不会漏）。
    /// </summary>
    internal static class ChemFontFix
    {
        private const float CandidateSearchInterval = 5f;

        private static readonly List<TMP_FontAsset> candidates = new List<TMP_FontAsset>();
        private static readonly Dictionary<string, TMP_FontAsset> fontByText =
            new Dictionary<string, TMP_FontAsset>(StringComparer.Ordinal);
        private static readonly Dictionary<int, string> verifiedTexts = new Dictionary<int, string>();
        private static readonly HashSet<string> warnedTexts = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<int> diagnosedTexts = new HashSet<int>();

        private static TMP_FontAsset preferredFont;
        private static float nextCandidateSearch = -1f;
        private static bool loggedApplied;
        private static bool globalWatcherReady;

        /// <summary>化学模组自己的窗口 id，常驻修正器会挨个扫。</summary>
        private static readonly string[] WatchedWindowIds =
        {
            "afterhour_location",
            "chemistry.bench",
            "chemistry.lab_tools",
            "chemistry.sample_report",
            "chemistry.sample_refine"
        };

        // ---------------------------------------------------------------
        // 挂载
        // ---------------------------------------------------------------

        /// <summary>给一个 CustomUI 窗口挂上字体修正（窗口里的按钮、标签、tooltip 都会管）。</summary>
        public static void Attach(CustomUIWindow window)
        {
            if (window != null)
            {
                Attach(window.root);
            }
        }

        /// <summary>给任意 GameObject 根节点挂上字体修正。</summary>
        public static void Attach(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            EnsureGlobalWatcher();

            try
            {
                if (root.GetComponent<ChemFontFixBehaviour>() == null)
                {
                    root.AddComponent<ChemFontFixBehaviour>();
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("挂字体修正失败：" + e.Message);
            }
        }

        /// <summary>常驻修正器：扫模组自己的窗口和游戏共用的 tooltip。模组启用时挂一次。</summary>
        public static void EnsureGlobalWatcher()
        {
            if (globalWatcherReady)
            {
                return;
            }

            try
            {
                GameObject host = new GameObject("ChemistryFontFix");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<ChemFontFixWatcher>();
                globalWatcherReady = true;
                ChemUi.Log?.Log("字体修正常驻扫描已启动");
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("创建字体修正常驻扫描失败：" + e.Message);
            }
        }

        internal static void ScanOpenWindows()
        {
            CustomUIManager manager = CustomUIManager.Instance;
            if (manager == null)
            {
                return;
            }

            for (int i = 0; i < WatchedWindowIds.Length; i++)
            {
                try
                {
                    CustomUIWindow window = manager.GetWindow(WatchedWindowIds[i]);
                    if (window == null || window.root == null || !window.root.activeInHierarchy)
                    {
                        continue;
                    }

                    Scan(window.root.transform);
                }
                catch
                {
                    // 窗口关闭/销毁的空档，跳过
                }
            }
        }

        // ---------------------------------------------------------------
        // 窗口内文字
        // ---------------------------------------------------------------

        internal static void Scan(Transform root)
        {
            if (root == null || !root.gameObject.activeInHierarchy)
            {
                return;
            }

            try
            {
                TMP_InputField[] inputs = root.GetComponentsInChildren<TMP_InputField>(true);
                TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    TextMeshProUGUI text = texts[i];
                    if (text == null)
                    {
                        continue;
                    }

                    string content = text.text;
                    if (string.IsNullOrEmpty(content) || !HasCjk(content))
                    {
                        continue;
                    }

                    if (IsInputText(text, inputs))
                    {
                        continue;
                    }

                    int id = text.GetInstanceID();
                    string previous;
                    if (verifiedTexts.TryGetValue(id, out previous) && previous == content)
                    {
                        continue;
                    }

                    TMP_FontAsset current = text.font;
                    bool canRender = CanRender(current, content);
                    if (diagnosedTexts.Count < 24 && diagnosedTexts.Add(id))
                    {
                        Material mat = text.fontSharedMaterial;
                        ChemUi.Log?.Log("字体检查：\"" + Shorten(content) + "\" 字体=" +
                                        (current != null ? current.name : "null") +
                                        " 模式=" + (current != null ? current.atlasPopulationMode.ToString() : "?") +
                                        " 图集=" + (current != null && current.atlasTexture != null
                                            ? current.atlasTexture.name
                                            : "null") +
                                        " 材质=" + (mat != null
                                            ? (mat.name + " tex=" + (mat.mainTexture != null ? mat.mainTexture.name : "null"))
                                            : "null") +
                                        " 组件=" + text.name +
                                        " 路径=" + ParentPath(text.transform, 3) +
                                        " 可显示=" + canRender);
                    }

                    bool materialMismatch = IsMaterialMismatch(text, current);
                    if (canRender && !materialMismatch)
                    {
                        verifiedTexts[id] = content;
                        continue;
                    }

                    if (canRender && materialMismatch)
                    {
                        // 字形有、只是材质指向了别的图集：把材质掰回字体自己的，字就画得出来了
                        ApplyFontMaterial(text, current);
                        verifiedTexts[id] = content;
                        continue;
                    }

                    TMP_FontAsset target = PickFont(content);
                    if (target == null)
                    {
                        if (warnedTexts.Add(content))
                        {
                            ChemUi.Log?.Warning("找不到能显示这段中文的字体：" + Shorten(content));
                        }

                        continue;
                    }

                    ApplyFont(text, target);
                    verifiedTexts[id] = content;
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("字体修正扫描失败：" + e.Message);
            }
        }

        private static void ApplyFontMaterial(TextMeshProUGUI text, TMP_FontAsset font)
        {
            try
            {
                if (font.material != null && text.fontSharedMaterial != font.material)
                {
                    text.fontSharedMaterial = font.material;
                }

                text.ForceMeshUpdate();

                if (!loggedApplied)
                {
                    loggedApplied = true;
                    ChemUi.Log?.Log("中文字体材质修正：" + font.name);
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("修正文字材质失败：" + e.Message);
            }
        }

        private static void ApplyFont(TextMeshProUGUI text, TMP_FontAsset font)
        {
            try
            {
                if (text.font != font)
                {
                    text.font = font;
                }

                text.ForceMeshUpdate();

                if (!loggedApplied)
                {
                    loggedApplied = true;
                    ChemUi.Log?.Log("中文字体修正：使用 " + font.name);
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("替换中文字体失败：" + e.Message);
            }
        }

        private static bool IsInputText(TextMeshProUGUI text, TMP_InputField[] inputs)
        {
            for (int i = 0; i < inputs.Length; i++)
            {
                TMP_InputField input = inputs[i];
                if (input != null && ReferenceEquals(input.textComponent, text))
                {
                    return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------
        // tooltip（和窗口无关，常驻一份）
        // ---------------------------------------------------------------

        internal static void FixTooltipPrefab()
        {
            CustomUIManager manager = CustomUIManager.Instance;
            if (manager == null || manager.tooltipPrefab == null)
            {
                return;
            }

            try
            {
                TextMeshProUGUI prefabText = manager.tooltipPrefab.GetComponentInChildren<TextMeshProUGUI>(true);
                if (prefabText != null && !CanRender(prefabText.font, "中文"))
                {
                    TMP_FontAsset font = PickFont("中文");
                    if (font != null)
                    {
                        prefabText.font = font;
                    }
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("修正 tooltip 预制体字体失败：" + e.Message);
            }
        }

        internal static void FixTooltip()
        {
            try
            {
                GameObject tooltip = GameObject.Find("custom_ui_tooltip");
                if (tooltip == null)
                {
                    return;
                }

                TextMeshProUGUI text = tooltip.GetComponentInChildren<TextMeshProUGUI>(true);
                if (text == null)
                {
                    return;
                }

                string content = text.text;
                if (string.IsNullOrEmpty(content) || !HasCjk(content))
                {
                    return;
                }

                if (CanRender(text.font, content))
                {
                    return;
                }

                TMP_FontAsset font = PickFont(content);
                if (font != null)
                {
                    text.font = font;
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("修正 tooltip 字体失败：" + e.Message);
            }
        }

        // ---------------------------------------------------------------
        // 字体挑选
        // ---------------------------------------------------------------

        private static TMP_FontAsset PickFont(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            TMP_FontAsset cached;
            if (fontByText.TryGetValue(text, out cached))
            {
                return cached;
            }

            if (preferredFont != null && CanRender(preferredFont, text))
            {
                fontByText[text] = preferredFont;
                return preferredFont;
            }

            RefreshCandidates();

            for (int i = 0; i < candidates.Count; i++)
            {
                TMP_FontAsset font = candidates[i];
                if (font != null && CanRender(font, text))
                {
                    fontByText[text] = font;
                    return font;
                }
            }

            return null;
        }

        private static void RefreshCandidates()
        {
            if (candidates.Count > 0 && Time.unscaledTime < nextCandidateSearch)
            {
                return;
            }

            nextCandidateSearch = Time.unscaledTime + CandidateSearchInterval;

            var seen = new HashSet<TMP_FontAsset>();
            var chineseNamed = new List<TMP_FontAsset>();
            var others = new List<TMP_FontAsset>();

            FontLoader loader = FontLoader.Instance;
            if (loader != null)
            {
                AddCandidate(seen, chineseNamed, others, loader.simplifiedChineseFont);
                AddCandidate(seen, chineseNamed, others, loader.simplifiedChineseHandwrittenFont);
                AddCandidate(seen, chineseNamed, others, loader.notoSans);
                AddCandidate(seen, chineseNamed, others, loader.notoSans_Normal);
                AddCandidate(seen, chineseNamed, others, loader.notoSerif);
            }

            try
            {
                AddCandidate(seen, chineseNamed, others, TMP_Settings.defaultFontAsset);
            }
            catch
            {
                // 设置资源还没加载，无所谓
            }

            // 场景里已经在用的字体：能显示中文的正文/标题字体会排到前面
            try
            {
                TextMeshProUGUI[] sceneTexts = UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>(true);
                for (int i = 0; i < sceneTexts.Length; i++)
                {
                    if (sceneTexts[i] != null)
                    {
                        AddCandidate(seen, chineseNamed, others, sceneTexts[i].font);
                    }
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("扫描场景字体失败：" + e.Message);
            }

            try
            {
                TMP_FontAsset[] all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                for (int i = 0; i < all.Length; i++)
                {
                    AddCandidate(seen, chineseNamed, others, all[i]);
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("扫描全部字体失败：" + e.Message);
            }

            candidates.Clear();
            candidates.AddRange(chineseNamed);
            candidates.AddRange(others);

            if (preferredFont == null && candidates.Count > 0)
            {
                preferredFont = candidates[0];
            }
        }

        private static void AddCandidate(HashSet<TMP_FontAsset> seen, List<TMP_FontAsset> chineseNamed,
            List<TMP_FontAsset> others, TMP_FontAsset font)
        {
            if (font == null || !seen.Add(font))
            {
                return;
            }

            // 名字像中文正文字体的排前面；其余照旧，反正后面都要逐个测字形
            if (LooksLikeChineseFont(font.name))
            {
                chineseNamed.Add(font);
            }
            else
            {
                others.Add(font);
            }
        }

        private static bool LooksLikeChineseFont(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string lower = name.ToLowerInvariant();
            return lower.Contains("chinese") ||
                   lower.Contains("simplified") ||
                   lower.Contains("cjk") ||
                   lower.Contains("noto sans sc") ||
                   lower.Contains("notosanssc") ||
                   lower.Contains("sourcehan") ||
                   lower.Contains("source han") ||
                   lower.Contains("思源") ||
                   lower.Contains("heiti") ||
                   lower.Contains("msyh");
        }

        private static bool CanRender(TMP_FontAsset font, string text)
        {
            if (font == null || string.IsNullOrEmpty(text))
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!NeedsGlyph(c))
                {
                    continue;
                }

                try
                {
                    // 只认这个字体自己图集里已经有的字形：按钮预制体的字体带了 fallback 表，
                    // HasCharacter(searchFallbacks:true) 会说能画，实际渲染出来还是方块。
                    // tryAddCharacter：动态字体缺字时顺手把字模加进图集（静态字体会返回 false）。
                    if (!font.HasCharacter(c, false, true))
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasCjk(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            for (int i = 0; i < text.Length; i++)
            {
                if (NeedsGlyph(text[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>需要中文字形覆盖的字符（CJK、CJK 标点、全角符号）。</summary>
        private static bool NeedsGlyph(char c)
        {
            return (c >= '\u2E80' && c <= '\u9FFF') ||
                   (c >= '\uF900' && c <= '\uFAFF') ||
                   (c >= '\uFF00' && c <= '\uFFEF') ||
                   (c >= '\u3000' && c <= '\u303F');
        }

        private static string Shorten(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            return text.Length <= 24 ? text : text.Substring(0, 24) + "…";
        }

        /// <summary>TMP 的文字材质要指向字体自己的图集，指到别的图集上就会画出一堆方块。</summary>
        private static bool IsMaterialMismatch(TextMeshProUGUI text, TMP_FontAsset font)
        {
            if (text == null || font == null)
            {
                return false;
            }

            try
            {
                Material mat = text.fontSharedMaterial;
                if (mat == null || mat.mainTexture == null || font.atlasTexture == null)
                {
                    return mat == null;
                }

                return !ReferenceEquals(mat.mainTexture, font.atlasTexture);
            }
            catch
            {
                return false;
            }
        }

        private static string ParentPath(Transform transform, int depth)
        {
            if (transform == null)
            {
                return "?";
            }

            var parts = new List<string>();
            Transform current = transform;
            for (int i = 0; i < depth && current != null; i++)
            {
                parts.Add(current.name);
                current = current.parent;
            }

            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }
    }

    /// <summary>挂在 CustomUI 窗口根节点上：窗口显示期间每 0.25 秒修一次字体。</summary>
    internal sealed class ChemFontFixBehaviour : MonoBehaviour
    {
        private const float ScanInterval = 0.25f;

        private float nextScan;

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextScan)
            {
                return;
            }

            nextScan = Time.unscaledTime + ScanInterval;
            ChemFontFix.Scan(transform);
        }
    }

    /// <summary>常驻扫描模组窗口和游戏共用的 tooltip。</summary>
    internal sealed class ChemFontFixWatcher : MonoBehaviour
    {
        private const float ScanInterval = 0.3f;

        private float nextScan;

        private void LateUpdate()
        {
            if (CustomUIManager.Instance == null)
            {
                return;
            }

            if (Time.unscaledTime < nextScan)
            {
                return;
            }

            nextScan = Time.unscaledTime + ScanInterval;
            ChemFontFix.ScanOpenWindows();
            ChemFontFix.FixTooltipPrefab();
            ChemFontFix.FixTooltip();
        }
    }
}
