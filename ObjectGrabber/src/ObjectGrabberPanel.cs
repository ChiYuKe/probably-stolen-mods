using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ObjectGrabber
{
    /// <summary>
    /// 浮球和右侧信息面板。整个东西挂在一个常驻 Canvas 上，换场景不会丢。
    /// </summary>
    public sealed class ObjectGrabberPanel : MonoBehaviour
    {
        private static readonly Color HeaderColor = new Color(0.96f, 0.76f, 0.32f, 1f);
        private static readonly Color BodyColor = new Color(0.91f, 0.91f, 0.94f, 1f);
        private static readonly Color FooterColor = new Color(0.50f, 0.50f, 0.56f, 1f);
        private static readonly Color PanelColor = new Color(0.055f, 0.06f, 0.075f, 0.94f);
        private static readonly Color AccentColor = new Color(0.91f, 0.64f, 0.24f, 0.92f);

        private const float PanelWidth = 400f;
        private const float BallSize = 48f;

        private RectTransform canvasRect;        private RectTransform ballRect;
        private RectTransform panelRect;
        private Image ballImage;
        private TextMeshProUGUI headerText;
        private TextMeshProUGUI bodyText;
        private TextMeshProUGUI footerText;

        private Sprite ballSprite;
        private Sprite panelSprite;

        private TMP_FontAsset font;
        private float nextFontCheck;
        private float nextFontSearch;
        private float nextInspect;

        private string lastHeader;
        private string lastBody;
        private bool built;
        private bool positioned;
        private bool dragging;

        public bool IsDragging => dragging;

        private void Awake()
        {
            TryBuild();
        }

        private void Start()
        {
            EnsureFont();
            if (!positioned && canvasRect != null && canvasRect.rect.width > 1f)
            {
                SetBallScreenPosition(new Vector2(Screen.width * 0.72f, Screen.height * 0.62f));
                positioned = true;
            }

            InspectNow();
        }

        private void OnEnable()
        {
            if (!built)
            {
                return;
            }

            nextFontCheck = 0f;
            nextInspect = 0f;
            EnsureFont();
            PositionPanel();
        }

        private void Update()
        {
            if (!built)
            {
                return;
            }

            EnsureFont();

            if (!positioned && canvasRect != null && canvasRect.rect.width > 1f)
            {
                SetBallScreenPosition(new Vector2(Screen.width * 0.72f, Screen.height * 0.62f));
                positioned = true;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (!dragging)
            {
                Vector2 clamped = ClampBall(ballRect.anchoredPosition);
                if (clamped != ballRect.anchoredPosition)
                {
                    ballRect.anchoredPosition = clamped;
                }
            }

            float interval = dragging ? 0.05f : 0.15f;
            if (Time.unscaledTime >= nextInspect)
            {
                nextInspect = Time.unscaledTime + interval;
                InspectNow();
            }

            PositionPanel();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        public void BeginDrag()
        {
            dragging = true;
            nextInspect = 0f;
        }

        public void EndDrag()
        {
            dragging = false;
            nextInspect = 0f;
        }

        public void DragTo(Vector2 screenPosition)
        {
            SetBallScreenPosition(screenPosition);
            InspectNow();
            PositionPanel();
        }

        private void TryBuild()
        {
            try
            {
                Build();
                built = true;
            }
            catch (Exception e)
            {
                Debug.LogError("[对象获取器] 建界面失败: " + e);
            }
        }

        private void Build()
        {
            canvasRect = transform as RectTransform;

            font = ResolveFont();
            if (font == null)
            {
                font = TMP_Settings.defaultFontAsset;
            }

            panelSprite = CreateRoundedSprite(32, 8);
            ballSprite = CreateCircleSprite(64);

            BuildPanel();
            BuildBall();

            headerText.text = "对象获取器";
            bodyText.text = "拖到对象上\n\n把浮球拖到物品、机器、角色或界面控件上，\n浮球会自动读取鼠标下面的元素。";
            footerText.text = "拖动浮球拾取 · 右键关闭 · Esc 退出";
            lastHeader = headerText.text;
            lastBody = bodyText.text;
        }

        private void BuildPanel()
        {
            GameObject panel = new GameObject(
                "InfoPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            panel.transform.SetParent(transform, false);

            panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.zero;
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.sizeDelta = new Vector2(PanelWidth, 120f);

            Image background = panel.GetComponent<Image>();
            background.sprite = panelSprite;
            background.type = Image.Type.Sliced;
            background.color = PanelColor;
            background.raycastTarget = false;

            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 14, 12);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(panel.transform, false);
            RectTransform accentRect = (RectTransform)accent.transform;
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.sizeDelta = new Vector2(-16f, 3f);
            accentRect.anchoredPosition = new Vector2(0f, -7f);

            Image accentImage = accent.GetComponent<Image>();
            accentImage.color = AccentColor;
            accentImage.raycastTarget = false;

            LayoutElement accentLayout = accent.AddComponent<LayoutElement>();
            accentLayout.ignoreLayout = true;

            headerText = CreateText(panel.transform, "Header", 17f, HeaderColor, FontStyles.Bold);
            headerText.alignment = TextAlignmentOptions.MidlineLeft;
            headerText.enableWordWrapping = false;
            headerText.overflowMode = TextOverflowModes.Ellipsis;

            bodyText = CreateText(panel.transform, "Body", 13f, BodyColor, FontStyles.Normal);
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.lineSpacing = 3f;

            footerText = CreateText(panel.transform, "Footer", 11f, FooterColor, FontStyles.Normal);
            footerText.alignment = TextAlignmentOptions.MidlineLeft;

            panel.SetActive(true);
        }

        private void BuildBall()
        {
            GameObject ball = new GameObject(
                "Ball",
                typeof(RectTransform),
                typeof(Image),
                typeof(ObjectGrabberBall));
            ball.transform.SetParent(transform, false);

            ballRect = (RectTransform)ball.transform;
            ballRect.anchorMin = Vector2.zero;
            ballRect.anchorMax = Vector2.zero;
            ballRect.pivot = new Vector2(0.5f, 0.5f);
            ballRect.sizeDelta = new Vector2(BallSize, BallSize);

            ballImage = ball.GetComponent<Image>();
            ballImage.sprite = ballSprite;
            ballImage.preserveAspect = true;
            ballImage.raycastTarget = true;

            ObjectGrabberBall handler = ball.GetComponent<ObjectGrabberBall>();
            handler.Init(this);

            ballRect.SetAsLastSibling();
        }

        private TextMeshProUGUI CreateText(
            Transform parent, string name, float size, Color color, FontStyles style)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }

            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.richText = true;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            text.margin = Vector4.zero;
            return text;
        }

        private void EnsureFont()
        {
            if (Time.unscaledTime < nextFontCheck)
            {
                return;
            }

            nextFontCheck = Time.unscaledTime + 1f;

            TMP_FontAsset resolved = ResolveFont();
            if (resolved == null || resolved == font)
            {
                return;
            }

            font = resolved;
            headerText.font = font;
            bodyText.font = font;
            footerText.font = font;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            PositionPanel();
        }

        private TMP_FontAsset ResolveFont()
        {
            FontLoader loader = FontLoader.Instance;
            if (loader != null)
            {
                if (CanRenderChinese(loader.simplifiedChineseFont))
                {
                    return loader.simplifiedChineseFont;
                }

                if (CanRenderChinese(loader.simplifiedChineseHandwrittenFont))
                {
                    return loader.simplifiedChineseHandwrittenFont;
                }
            }

            if (Time.unscaledTime < nextFontSearch)
            {
                return null;
            }

            nextFontSearch = Time.unscaledTime + 5f;

            TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
            for (int i = 0; i < fonts.Length; i++)
            {
                TMP_FontAsset candidate = fonts[i];
                if (candidate != null && LooksLikeChineseFont(candidate.name) && CanRenderChinese(candidate))
                {
                    return candidate;
                }
            }

            for (int i = 0; i < fonts.Length; i++)
            {
                TMP_FontAsset candidate = fonts[i];
                if (candidate != null && CanRenderChinese(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool CanRenderChinese(TMP_FontAsset candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            try
            {
                return candidate.HasCharacter('中', false, true) ||
                       candidate.HasCharacter('文', false, true);
            }
            catch (Exception)
            {
                return false;
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
                   lower.Contains("notosanssc");
        }

        private void InspectNow()
        {
            if (!built || canvasRect == null)
            {
                return;
            }

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, ballRect.position);
            InspectResult result = ObjectInspector.Inspect(screen, transform);
            if (result == null)
            {
                return;
            }

            if (string.Equals(result.Header, lastHeader) && string.Equals(result.Body, lastBody))
            {
                return;
            }

            lastHeader = result.Header;
            lastBody = result.Body;

            headerText.text = result.Header;
            bodyText.text = result.Body;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            PositionPanel();
        }

        private void SetBallScreenPosition(Vector2 screen)
        {
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
            {
                ballRect.anchoredPosition = ClampBall(local);
            }
        }

        private Vector2 ClampBall(Vector2 local)
        {
            if (canvasRect == null)
            {
                return local;
            }

            float halfWidth = canvasRect.rect.width * 0.5f;
            float halfHeight = canvasRect.rect.height * 0.5f;
            float radius = BallSize * 0.5f + 4f;

            local.x = Mathf.Clamp(local.x, -halfWidth + radius, halfWidth - radius);
            local.y = Mathf.Clamp(local.y, -halfHeight + radius, halfHeight - radius);
            return local;
        }

        private void PositionPanel()
        {
            if (canvasRect == null || panelRect == null || ballRect == null)
            {
                return;
            }

            float halfWidth = canvasRect.rect.width * 0.5f;
            float halfHeight = canvasRect.rect.height * 0.5f;
            float panelWidth = panelRect.rect.width;
            float panelHeight = panelRect.rect.height;

            Vector2 ball = ballRect.anchoredPosition;
            float x = ball.x + 34f;
            float y = ball.y + 26f;

            if (x + panelWidth > halfWidth - 8f)
            {
                x = ball.x - 34f - panelWidth;
            }

            float minX = -halfWidth + 8f;
            float maxX = halfWidth - 8f - panelWidth;
            x = Mathf.Clamp(x, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX));

            if (y - panelHeight < -halfHeight + 8f)
            {
                y = -halfHeight + 8f + panelHeight;
            }

            if (y > halfHeight - 8f)
            {
                y = halfHeight - 8f;
            }

            panelRect.anchoredPosition = new Vector2(x, y);
        }

        private Sprite CreateCircleSprite(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ObjectGrabberBall",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float outer = size * 0.5f - 1.5f;
            float ringWidth = Mathf.Max(2.5f, size * 0.07f);
            float dotRadius = size * 0.09f;

            Color32 ring = new Color32(232, 163, 61, 255);
            Color32 fill = new Color32(18, 18, 24, 235);
            Color32 dot = new Color32(255, 204, 102, 255);
            Color32 clear = new Color32(0, 0, 0, 0);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(outer - distance + 0.75f);
                    if (alpha <= 0f)
                    {
                        pixels[y * size + x] = clear;
                        continue;
                    }

                    Color32 color;
                    if (distance <= dotRadius)
                    {
                        color = dot;
                    }
                    else if (distance >= outer - ringWidth)
                    {
                        color = ring;
                    }
                    else
                    {
                        color = fill;
                    }

                    color.a = (byte)Mathf.RoundToInt(color.a * alpha);
                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = "ObjectGrabberBallSprite";
            return sprite;
        }

        private Sprite CreateRoundedSprite(int size, int radius)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ObjectGrabberPanel",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float qx = Mathf.Abs(x - center) - (half - radius);
                    float qy = Mathf.Abs(y - center) - (half - radius);
                    float dx = Mathf.Max(qx, 0f);
                    float dy = Mathf.Max(qy, 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.name = "ObjectGrabberPanelSprite";
            return sprite;
        }
    }
}
