using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace DayHud
{
    /// <summary>
    /// 左上角圆形天数表盘，底图用 mod 自带的像素表盘贴图。
    /// 按 1920x1080 参考分辨率缩放，鼠标悬停亮边 + 查看日历提示，点击打开日历。
    /// </summary>
    public class DayHudPanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private const float RetroWidth = 190f;
        private const float FallbackWidth = 160f;
        private const float MarginX = 24f;
        private const float MarginY = 18f;

        // 略高于游戏基础 HUD，低于弹窗/日历那一层；这样既能点到，开窗时又会被盖住
        private const int SortingOrder = 1;

        private const string RetroTextureKey = "retro_dial";
        private const string RetroMaskKey = "retro_dial_mask";

        private static readonly string[] WeekdayKeys =
        {
            "mech_calendar_sunday",
            "mech_calendar_monday",
            "mech_calendar_tuesday",
            "mech_calendar_wednesday",
            "mech_calendar_thursday",
            "mech_calendar_friday",
            "mech_calendar_saturday"
        };

        private static readonly string[] WeekdayFallbacks =
        {
            "Sunday",
            "Monday",
            "Tuesday",
            "Wednesday",
            "Thursday",
            "Friday",
            "Saturday"
        };

        private static Sprite fallbackSprite;

        private string modId;
        private ModLog log;

        private Canvas canvas;
        private GraphicRaycaster raycaster;
        private RectTransform dial;
        private Image shadow;
        private Image highlight;
        private Image plate;
        private TextMeshProUGUI dayText;
        private TextMeshProUGUI weekdayText;
        private TextMeshProUGUI rentText;

        private float panelWidth = RetroWidth;
        private float panelHeight = RetroWidth;
        private bool retroApplied;
        private int shownDay = int.MinValue;
        private bool userHidden;
        private bool hovering;
        private bool fontFromGame;
        private TMP_FontAsset noteFont;
        private FontStyles noteStyle = FontStyles.Normal;
        private bool noteFontTried;
        private bool rentVisible;
        private bool rentDirty = true;
        private int rentDays = -1;
        private bool rentMortgage;
        private Coroutine punchRoutine;

        public void Build(string modId, ModLog log)
        {
            this.modId = modId;
            this.log = log;

            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            canvas.enabled = false;
            CustomUIManager.AddScreenScaler(gameObject);

            raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = gameObject.AddComponent<GraphicRaycaster>();
            }

            raycaster.enabled = false;

            BuildDial();

            try
            {
                LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            }
            catch (Exception e)
            {
                log.Warning("订阅语言切换失败，就按当前语言显示：" + e.Message);
            }

            ModHook.OnModAssetsLoaded += OnModAssetsLoaded;
            shownDay = int.MinValue;
        }

        public void ToggleUserHidden()
        {
            userHidden = !userHidden;
            if (canvas != null)
            {
                canvas.enabled = !userHidden && ShouldShow();
            }

            log.Log(userHidden ? "天数圆盘：关" : "天数圆盘：开");
        }

        private void BuildDial()
        {
            Sprite sprite;
            retroApplied = TryGetRetroSprite(out sprite);
            if (retroApplied)
            {
                panelWidth = RetroWidth;
                panelHeight = RetroWidth * sprite.rect.height / sprite.rect.width;
            }
            else
            {
                sprite = GetFallbackDialSprite();
                panelWidth = FallbackWidth;
                panelHeight = FallbackWidth;
            }

            GameObject dialObject = new GameObject("Dial", typeof(RectTransform));
            dial = (RectTransform)dialObject.transform;
            dial.SetParent(transform, false);
            dial.anchorMin = new Vector2(0f, 1f);
            dial.anchorMax = new Vector2(0f, 1f);
            dial.pivot = new Vector2(0.5f, 0.5f);
            dial.sizeDelta = new Vector2(panelWidth, panelHeight);
            dial.anchoredPosition = new Vector2(MarginX + panelWidth * 0.5f, -(MarginY + panelHeight * 0.5f));

            Vector2 size = new Vector2(panelWidth, panelHeight);

            Sprite outlineSprite = sprite;
            Sprite mask;
            if (retroApplied && TryGetRetroMask(out mask))
            {
                outlineSprite = mask;
            }

            // 影子
            shadow = CreateImage(dial, "Shadow", sprite, new Vector2(3f, -4f), size);
            shadow.color = new Color(0f, 0f, 0f, 0.42f);

            // 悬停亮边：用白色剪影 + 奶油色，避免底图颜色把描边染脏
            highlight = CreateImage(dial, "Highlight", outlineSprite, Vector2.zero, size + new Vector2(6f, 6f));
            highlight.color = new Color32(225, 209, 156, 255);
            highlight.gameObject.SetActive(false);

            // 表盘底图
            plate = CreateImage(dial, "Plate", sprite, Vector2.zero, size);
            plate.color = Color.white;
            plate.raycastTarget = true;

            // 圆形命中范围：矩形四角的透明区域不会再触发悬停
            plate.gameObject.AddComponent<DayHudHitArea>();

            dayText = CreateText("Day", 32f, 12f, new Color32(238, 226, 198, 255), FontStyles.Normal);
            weekdayText = CreateText("Weekday", 14f, 8f, new Color32(176, 162, 136, 255), FontStyles.Normal);
            rentText = CreateText("Rent", 11f, 8f, new Color32(130, 198, 184, 255), FontStyles.Normal);

            rentVisible = false;
            LayoutText(false);
        }

        private void Update()
        {
            if (canvas == null || dial == null)
            {
                return;
            }

            bool visible = !userHidden && ShouldShow();
            if (canvas.enabled != visible)
            {
                canvas.enabled = visible;
            }

            if (raycaster != null && raycaster.enabled != visible)
            {
                raycaster.enabled = visible;
            }

            if (!visible)
            {
                SetHovering(false);
                noteFontTried = false;
                return;
            }

            if (!noteFontTried)
            {
                noteFontTried = true;
                AdoptNoteFont();
            }

            if (!retroApplied)
            {
                // 资源加载事件没赶上的话，这里兜一下
                ApplyRetroBackground();
            }

            KeepInsideGameArea();
            EnsureFont();
            RefreshRent();

            int day = Mathf.Max(1, StoreStation.GetDayCounter());
            if (day == shownDay)
            {
                return;
            }

            shownDay = day;
            RefreshText(day);
            Punch();
        }

        private void OnDestroy()
        {
            SetHovering(false);
            ModHook.OnModAssetsLoaded -= OnModAssetsLoaded;

            try
            {
                LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            }
            catch
            {
                // 关游戏时本地化系统可能已经先走一步了，忽略
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!CanInteract() || InteractionBlocked())
            {
                return;
            }

            SetHovering(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHovering(false);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (!CanInteract() || InteractionBlocked())
            {
                return;
            }

            SetHovering(false);

            AdvCalendarUIManager calendar = AdvCalendarUIManager.Instance;
            if (calendar == null)
            {
                log.Warning("当前场景没有日历界面，点不动");
                return;
            }

            try
            {
                calendar.ToggleUI();
            }
            catch (Exception e)
            {
                log.Error("打开日历的时候崩了：" + e);
            }
        }

        private static bool InteractionBlocked()
        {
            return PlayerStore.HasInstance && PlayerStore.Instance != null && PlayerStore.Instance.cinematicMode;
        }

        private bool CanInteract()
        {
            return canvas != null && canvas.enabled && !userHidden && ShouldShow();
        }

        private void SetHovering(bool value)
        {
            if (hovering == value)
            {
                return;
            }

            hovering = value;

            if (highlight != null)
            {
                highlight.gameObject.SetActive(value);
            }

            if (value)
            {
                ShowCalendarTooltip();
            }
            else
            {
                HideCalendarTooltip();
            }
        }

        private void ShowCalendarTooltip()
        {
            if (TooltipManager._instance == null)
            {
                return;
            }

            try
            {
                string text = LocHelper.GetLocalizedUI("inte_calendar_hover");
                if (string.IsNullOrEmpty(text) || text == "inte_calendar_hover")
                {
                    text = "查看日历";
                }

                TooltipManager._instance.SetAndShowTooltip(text, this);
            }
            catch (Exception e)
            {
                log.Warning("显示日历提示失败：" + e.Message);
            }
        }

        private void HideCalendarTooltip()
        {
            if (TooltipManager._instance != null)
            {
                TooltipManager._instance.HideTooltip(this);
            }
        }

        private void OnModAssetsLoaded(LoadedMod mod)
        {
            if (mod == null || mod.Manifest == null || mod.Manifest.ID != modId)
            {
                return;
            }

            // 贴图是后加载的：资源到位后换掉临时圆盘，再刷一遍文字
            ApplyRetroBackground();
            shownDay = int.MinValue;
            rentDirty = true;
        }

        private void OnLocaleChanged(Locale locale)
        {
            noteFont = null;
            noteFontTried = false;

            ApplyFont(ResolveFont());
            ApplyStyle(FontStyles.Normal);
            shownDay = int.MinValue;
            rentDirty = true;

            if (hovering)
            {
                ShowCalendarTooltip();
            }
        }

        private void ApplyRetroBackground()
        {
            if (retroApplied || plate == null || shadow == null || dial == null)
            {
                return;
            }

            Sprite sprite;
            if (!TryGetRetroSprite(out sprite))
            {
                return;
            }

            panelWidth = RetroWidth;
            panelHeight = RetroWidth * sprite.rect.height / sprite.rect.width;

            plate.sprite = sprite;
            shadow.sprite = sprite;

            Sprite mask;
            if (TryGetRetroMask(out mask))
            {
                highlight.sprite = mask;
            }
            else
            {
                highlight.sprite = sprite;
            }

            Vector2 size = new Vector2(panelWidth, panelHeight);
            ((RectTransform)plate.transform).sizeDelta = size;
            RectTransform shadowRect = (RectTransform)shadow.transform;
            shadowRect.sizeDelta = size;
            shadowRect.anchoredPosition = new Vector2(3f, -4f);
            ((RectTransform)highlight.transform).sizeDelta = size + new Vector2(6f, 6f);
            dial.sizeDelta = size;

            LayoutText(rentVisible);
            retroApplied = true;
            log.Log("复古表盘贴图已就位");
        }

        private void LayoutText(bool showRent)
        {
            float width = panelWidth;
            float height = panelHeight;

            float daySize = Mathf.Clamp(width * 0.168f, 14f, 40f);
            dayText.fontSizeMax = daySize;
            dayText.fontSize = daySize;
            dayText.fontSizeMin = 12f;
            RectTransform dayRect = dayText.rectTransform;
            dayRect.sizeDelta = new Vector2(width * 0.66f, height * 0.27f);
            dayRect.anchoredPosition = new Vector2(0f, height * (showRent ? 0.045f : 0.022f));

            float weekSize = Mathf.Clamp(width * 0.063f, 9f, 18f);
            weekdayText.fontSizeMax = weekSize;
            weekdayText.fontSize = weekSize;
            weekdayText.fontSizeMin = 8f;
            RectTransform weekRect = weekdayText.rectTransform;
            weekRect.sizeDelta = new Vector2(width * 0.54f, height * 0.13f);
            weekRect.anchoredPosition = new Vector2(0f, -height * (showRent ? 0.12f : 0.17f));

            float rentSize = Mathf.Clamp(width * 0.063f, 8f, 16f);
            rentText.fontSizeMax = rentSize;
            rentText.fontSize = rentSize;
            rentText.fontSizeMin = 8f;
            RectTransform rentRect = rentText.rectTransform;
            rentRect.sizeDelta = new Vector2(width * 0.42f, height * 0.1f);
            rentRect.anchoredPosition = new Vector2(0f, -height * 0.27f);
            rentText.gameObject.SetActive(showRent);
        }

        private void RefreshText(int day)
        {
            if (dayText == null || weekdayText == null)
            {
                return;
            }

            EnsureFont();

            string dayLabel = ModHelper.GetLocalized(modId, "day", day);
            if (string.IsNullOrEmpty(dayLabel) || dayLabel == "day")
            {
                dayLabel = DefaultDayText(day);
            }

            dayText.text = dayLabel;
            weekdayText.text = WeekdayName(day);
        }

        /// <summary>
        /// 把墙上便签的租金倒计时压成一行塞进表盘：今天付租 / 明天付租 / 2天后付租。
        /// </summary>
        private void RefreshRent()
        {
            if (rentText == null)
            {
                return;
            }

            if (!PlayerStore.HasInstance)
            {
                SetRentVisible(false);
                return;
            }

            PlayerStore store = PlayerStore.Instance;
            if (store == null)
            {
                SetRentVisible(false);
                return;
            }

            bool show = !store.IsPropertyPaid;
            int days = show ? store.GetRentDayCounter() : -1;
            bool mortgage = show && store.isPayingMortgage;

            if (!rentDirty && show == rentVisible && days == rentDays && mortgage == rentMortgage)
            {
                return;
            }

            rentDirty = false;
            rentDays = days;
            rentMortgage = mortgage;

            if (!show)
            {
                SetRentVisible(false);
                return;
            }

            SetRentVisible(true);
            rentText.text = FormatRent(days, mortgage);
            rentText.color = days <= 1
                ? new Color32(232, 190, 112, 255)
                : new Color32(142, 208, 192, 255);
        }

        private void SetRentVisible(bool visible)
        {
            if (rentVisible == visible)
            {
                return;
            }

            rentVisible = visible;
            LayoutText(visible);
        }

        private string FormatRent(int days, bool mortgage)
        {
            string key;
            if (days <= 0)
            {
                key = mortgage ? "pay_today" : "rent_today";
            }
            else if (days == 1)
            {
                key = mortgage ? "pay_tomorrow" : "rent_tomorrow";
            }
            else
            {
                key = mortgage ? "pay_in" : "rent_in";
            }

            string text = days <= 1
                ? ModHelper.GetLocalized(modId, key)
                : ModHelper.GetLocalized(modId, key, days);

            if (string.IsNullOrEmpty(text) || text == key)
            {
                text = DefaultRent(days, mortgage);
            }

            return text;
        }

        private static string DefaultRent(int days, bool mortgage)
        {
            bool chinese = false;
            try
            {
                chinese = LocHelper.GetCurrentLocaleCode().StartsWith("zh");
            }
            catch
            {
                // 本地化还没初始化
            }

            if (chinese)
            {
                if (days <= 0)
                {
                    return mortgage ? "今天还款" : "今天付租";
                }

                if (days == 1)
                {
                    return mortgage ? "明天还款" : "明天付租";
                }

                return days + "天后" + (mortgage ? "还款" : "付租");
            }

            if (days <= 0)
            {
                return mortgage ? "PAY TODAY" : "RENT TODAY";
            }

            if (days == 1)
            {
                return mortgage ? "PAY TMRW" : "RENT TMRW";
            }

            return (mortgage ? "PAY IN " : "RENT IN ") + days + "D";
        }

        private void ApplyFont(TMP_FontAsset font)
        {
            if (font == null)
            {
                return;
            }

            if (dayText != null)
            {
                dayText.font = font;
            }

            if (weekdayText != null)
            {
                weekdayText.font = font;
            }

            if (rentText != null)
            {
                rentText.font = font;
            }
        }

        private void ApplyStyle(FontStyles style)
        {
            if (dayText != null)
            {
                dayText.fontStyle = style;
            }

            if (weekdayText != null)
            {
                weekdayText.fontStyle = style;
            }

            if (rentText != null)
            {
                rentText.fontStyle = style;
            }
        }

        /// <summary>
        /// 三行文字统一用墙上日历便签的字体和字重，省得表盘一个字体、便签另一个字体。
        /// </summary>
        private void AdoptNoteFont()
        {
            try
            {
                foreach (StoreCalendar calendar in Resources.FindObjectsOfTypeAll<StoreCalendar>())
                {
                    if (calendar == null || calendar.gameObject == null || !calendar.gameObject.scene.IsValid())
                    {
                        continue;
                    }

                    TextMeshProUGUI source = calendar.dayTMP;
                    if (source == null || source.font == null)
                    {
                        continue;
                    }

                    noteFont = source.font;
                    noteStyle = source.fontStyle;
                    ApplyFont(noteFont);
                    ApplyStyle(noteStyle);
                    shownDay = int.MinValue;
                    log.Log("字体已跟日历便签统一：" + noteFont.name);
                    return;
                }
            }
            catch (Exception e)
            {
                log.Warning("找日历便签字体失败：" + e.Message);
            }
        }

        /// <summary>
        /// mod 在游戏还没起 UI 的时候就加载了，字体得等 FontLoader 出来再补一次。
        /// </summary>
        private void EnsureFont()
        {
            if (dayText == null)
            {
                return;
            }

            if (noteFont != null)
            {
                if (dayText.font != noteFont)
                {
                    ApplyFont(noteFont);
                    ApplyStyle(noteStyle);
                }

                return;
            }

            if (fontFromGame && dayText.font != null)
            {
                return;
            }

            TMP_FontAsset font = ResolveFont();
            if (font != null)
            {
                ApplyFont(font);
            }
        }

        private static string DefaultDayText(int day)
        {
            try
            {
                switch (LocHelper.GetCurrentLocaleCode())
                {
                    case "zh":
                    case "zh-Hans":
                        return "第" + day + "天";
                    case "ja":
                        return day + "日目";
                    case "ko":
                        return day + "일차";
                }
            }
            catch
            {
                // 本地化还没初始化，用英文兜底
            }

            return "DAY " + day;
        }

        private static string WeekdayName(int day)
        {
            int index = ((day % 7) + 7) % 7;
            string key = WeekdayKeys[index];

            try
            {
                string text = LocHelper.GetLocalizedMechanic(key);
                if (!string.IsNullOrEmpty(text) && text != key)
                {
                    return text;
                }
            }
            catch
            {
                // 落到英文
            }

            return WeekdayFallbacks[index];
        }

        private TMP_FontAsset ResolveFont()
        {
            if (noteFont != null)
            {
                return noteFont;
            }

            fontFromGame = false;

            FontLoader loader = FontLoader.Instance;
            if (loader != null)
            {
                try
                {
                    TMP_FontAsset font = loader.GetFontByRole(
                        FontLoader.FontRole.Noto_Sans,
                        LocalizationSettings.SelectedLocale);
                    if (font != null)
                    {
                        fontFromGame = true;
                        return font;
                    }
                }
                catch
                {
                    // 本地化没准备好，下面用中文字体/默认字体兜底
                }

                if (loader.simplifiedChineseFont != null)
                {
                    fontFromGame = true;
                    return loader.simplifiedChineseFont;
                }
            }

            return TMP_Settings.defaultFontAsset;
        }

        private TextMeshProUGUI CreateText(string name, float maxSize, float minSize, Color color, FontStyles style)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)textObject.transform;
            rect.SetParent(dial, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = ResolveFont();
            if (font != null)
            {
                text.font = font;
            }

            text.fontStyle = style;
            text.fontSize = maxSize;
            text.enableAutoSizing = true;
            text.fontSizeMax = maxSize;
            text.fontSizeMin = minSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private static Image CreateImage(Transform parent, string name, Sprite sprite, Vector2 offset, Vector2 size)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)imageObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;

            Image image = imageObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        private void KeepInsideGameArea()
        {
            float scale = canvas.scaleFactor;
            if (scale <= 0.0001f)
            {
                return;
            }

            float leftInset = LetterboxManager.GameAreaX;
            float topInset = Screen.height - (LetterboxManager.GameAreaY + LetterboxManager.GameAreaHeight);

            Vector2 target = new Vector2(
                (leftInset + MarginX + panelWidth * 0.5f) / scale,
                -(topInset + MarginY + panelHeight * 0.5f) / scale);

            if ((dial.anchoredPosition - target).sqrMagnitude > 0.04f)
            {
                dial.anchoredPosition = target;
            }
        }

        private static bool ShouldShow()
        {
            NewGameData newGameData = NewGameData.Instance;
            if (newGameData != null && newGameData.isInMainMenu)
            {
                return false;
            }

            // PlayerStore 只在对局里存在；用它当"这局已经起来了"的判据，
            // 免得在买/读档前就去碰 StoreStation 把单例提前建出来。
            return PlayerStore.HasInstance;
        }

        private void Punch()
        {
            if (!isActiveAndEnabled || dial == null)
            {
                return;
            }

            if (punchRoutine != null)
            {
                StopCoroutine(punchRoutine);
            }

            punchRoutine = StartCoroutine(PunchRoutine());
        }

        private IEnumerator PunchRoutine()
        {
            const float duration = 0.22f;
            float time = 0f;

            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / duration);
                float scale = 1f + 0.12f * (1f - k) * (1f - k);
                dial.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            dial.localScale = Vector3.one;
            punchRoutine = null;
        }

        private bool TryGetRetroSprite(out Sprite sprite)
        {
            sprite = null;

            Texture2D texture = FindModTexture(RetroTextureKey);
            if (texture == null)
            {
                return false;
            }

            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "DayHudRetroDial";
            return true;
        }

        private bool TryGetRetroMask(out Sprite sprite)
        {
            sprite = null;

            Texture2D texture = FindModTexture(RetroMaskKey);
            if (texture == null)
            {
                return false;
            }

            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "DayHudRetroDialMask";
            return true;
        }

        private Texture2D FindModTexture(string key)
        {
            foreach (LoadedMod mod in ModLoader.Mods)
            {
                if (mod == null || mod.Manifest == null || mod.Manifest.ID != modId)
                {
                    continue;
                }

                if (mod.Textures != null && mod.Textures.TryGetValue(key, out Texture2D texture) &&
                    texture != null)
                {
                    return texture;
                }
            }

            return null;
        }

        /// <summary>
        /// 贴图没加载出来时的兜底圆盘，保证 HUD 不会整个消失。
        /// </summary>
        private static Sprite GetFallbackDialSprite()
        {
            if (fallbackSprite != null)
            {
                return fallbackSprite;
            }

            const int size = 256;
            const float center = (size - 1) * 0.5f;
            const float outer = 124f;
            const float ringInner = 113f;
            const float seamInner = 109.5f;
            const float accentInner = 106.5f;

            Color ring = new Color(0.918f, 0.875f, 0.749f, 1f);
            Color seam = new Color(0.145f, 0.098f, 0.157f, 1f);
            Color plateEdge = new Color(0.157f, 0.098f, 0.169f, 1f);
            Color plateCenter = new Color(0.231f, 0.153f, 0.239f, 1f);
            Color accent = new Color(0.545f, 0.604f, 0.298f, 1f);

            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    if (distance >= outer + 0.5f)
                    {
                        pixels[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    Color color;
                    if (distance >= ringInner)
                    {
                        color = ring;
                    }
                    else if (distance >= seamInner)
                    {
                        color = seam;
                    }
                    else if (distance >= accentInner)
                    {
                        color = Color.Lerp(plateEdge, accent, 0.35f);
                    }
                    else
                    {
                        float t = Mathf.Clamp01(distance / accentInner);
                        color = Color.Lerp(plateCenter, plateEdge, t * t);
                    }

                    // 外沿 1px 抗锯齿
                    color.a *= Mathf.Clamp01(outer + 0.5f - distance);
                    pixels[y * size + x] = color;
                }
            }

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "DayHudFallbackDial"
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
            fallbackSprite.name = "DayHudFallbackDial";
            return fallbackSprite;
        }
    }
}
