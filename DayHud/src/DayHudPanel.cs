using System;
using System.Collections;
using System.Collections.Generic;
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
        private const string MoneyTextureKey = "money_bar";
        private const string MoneyMaskKey = "money_bar_mask";
        private const string LeaveTextureKey = "leave_store";
        private const string LeaveMaskKey = "leave_store_mask";
        private const float LeaveIconSize = 96f;
        private const float LeaveIconMargin = 20f;
        private const string CalendarTextureKey = "calendar_panel";
        // 游戏日历窗口原背景就叫这个名字，找不到时用它兜底定位
        private const string CalendarBackgroundSpriteName = "calendar_stripped";

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

        private sealed class HiddenObject
        {
            public GameObject GameObject;
            public bool WasActive;
        }

        public enum NoticeTone
        {
            Info,
            Warning,
            Danger,
            Muted
        }

        private sealed class NotificationItem
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public TextMeshProUGUI Text;
            public Image Background;
            public Color BaseColor;
            public Action OnClick;
            public string Message;
            public float Age;
            public float Duration;
            public bool Hovered;
            public bool IsPermanent;
            public RectTransform CloseRect;
            public TextMeshProUGUI CloseText;
        }

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
        private TextMeshProUGUI statusText;

        private RectTransform moneyRoot;
        private Image moneyShadow;
        private Image moneyHighlight;
        private Image moneyBar;
        private TextMeshProUGUI moneyText;
        private float moneyWidth = RetroWidth;
        private float moneyHeight = 56f;
        private int shownCash = int.MinValue;
        private bool moneyReady;
        private bool moneyHovering;

        private RectTransform leaveRoot;
        private Image leaveIcon;
        private Image leaveHighlight;
        private DayHudIconButton leaveButton;
        private Sprite leaveSprite;
        private Sprite leaveMaskSprite;
        private bool leaveIconReady;
        private bool leaveHovering;
        private StoreDoor leaveDoor;

        private GameObject doorInteractionOwner;
        private bool doorInteractionDone;
        private bool disableDoorInteraction = true;
        private readonly List<Collider2D> doorColliders = new List<Collider2D>();
        private readonly List<bool> doorColliderStates = new List<bool>();
        private readonly List<Behaviour> doorBehaviours = new List<Behaviour>();
        private readonly List<bool> doorBehaviourStates = new List<bool>();

        private const int VisibleNotifications = 5;
        private const int MaxStoredNotifications = 18;
        private const float NotificationRowHeight = 42f;
        private const float NotificationGap = 5f;
        private const float NotificationWidth = 230f;
        private RectTransform notificationRoot;
        private readonly List<NotificationItem> notifications = new List<NotificationItem>();
        private NotificationItem hoveredNotice;
        private NotificationItem collapseRow;
        private bool notificationsExpanded;
        private bool notificationsInitialized;
        private float nextMachinePoll;
        private bool machineScanLogged;
        private readonly Dictionary<int, Dictionary<int, int>> machineOutputs =
            new Dictionary<int, Dictionary<int, int>>();

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
        private bool hideWallCalendar = true;
        private bool calendarHidden;
        private readonly List<HiddenObject> hiddenCalendarObjects = new List<HiddenObject>();
        private bool hideCreditDisplay = true;
        private bool creditHidden;
        private readonly List<HiddenObject> hiddenCreditObjects = new List<HiddenObject>();
        private bool hideFeedbackBox = true;
        private bool feedbackHidden;
        private readonly List<HiddenObject> hiddenFeedbackObjects = new List<HiddenObject>();
        private bool hideOutsideStore = true;
        private float nextOutsideHideAttempt;
        private bool outsideHideLogged;
        private bool calendarSkin = true;
        private bool calendarTrimEdges = true;
        private bool calendarSpriteDirty;
        private Sprite calendarPanelSprite;
        private Image calendarPanelImage;
        private GameObject calendarPanelOwner;
        private Sprite calendarPanelOriginalSprite;
        private Image.Type calendarPanelOriginalType = Image.Type.Simple;
        private Color calendarPanelOriginalColor = Color.white;
        private bool calendarSkinLogged;
        private bool rentVisible;
        private bool rentDirty = true;
        private int rentDays = -1;
        private bool rentMortgage;
        private bool statusDirty = true;
        private int shownStoreState = -1;
        private int shownClockMinute = -1;
        private bool shownClockBoundary;
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
            BuildMoneyBar();
            BuildLeaveIcon();
            BuildNotifications();

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

            log.Log(userHidden ? "当铺 HUD：关" : "当铺 HUD：开");
        }

        /// <summary>
        /// 模组配置：是否把墙上的实体日历收起来。默认开。
        /// </summary>
        public void SetHideWallCalendar(bool hide)
        {
            hideWallCalendar = hide;

            if (!hide && calendarHidden && !IsOutsideStore())
            {
                calendarHidden = false;
                RestorePhysicalCalendar();
            }
            else if (hide && !calendarHidden && ShouldShow())
            {
                calendarHidden = true;
                HidePhysicalCalendar();
            }
        }

        /// <summary>
        /// 模组配置：是否把墙上的信用点屏幕收起来。默认开。
        /// </summary>
        public void SetHideCreditDisplay(bool hide)
        {
            hideCreditDisplay = hide;

            if (!hide && creditHidden && !IsOutsideStore())
            {
                creditHidden = false;
                RestoreCreditDisplay();
            }
            else if (hide && !creditHidden && ShouldShow())
            {
                creditHidden = true;
                HideCreditDisplay();
            }
        }

        /// <summary>
        /// 模组配置：是否把墙上的反馈箱收起来。默认开。
        /// 声望面板已经整合进高级日历的“反馈”标签页，墙上的反馈箱就只留外观、不做交互。
        /// </summary>
        public void SetHideFeedbackBox(bool hide)
        {
            hideFeedbackBox = hide;

            if (!hide && feedbackHidden && !IsOutsideStore())
            {
                feedbackHidden = false;
                RestoreFeedbackBox();
            }
            else if (hide && !feedbackHidden && ShouldShow())
            {
                feedbackHidden = true;
                HideFeedbackBox();
            }
        }

        /// <summary>
        /// 模组配置：离开店铺的其他场景（淘荒/地图等）是否隐藏 HUD。默认隐藏。
        /// </summary>
        public void SetHideOutsideStore(bool hide)
        {
            hideOutsideStore = hide;
        }

        /// <summary>
        /// 模组配置：打开日历时，把游戏日历窗口的底图换成模组自带的复古工业面板。默认开。
        /// </summary>
        public void SetCalendarSkin(bool enabled)
        {
            if (calendarSkin == enabled)
            {
                return;
            }

            calendarSkin = enabled;

            if (!enabled)
            {
                RestoreCalendarSkin();
            }
        }

        /// <summary>
        /// 模组配置：是否把日历底图四周的透明外边裁掉再铺满窗口。默认裁。
        /// </summary>
        public void SetCalendarTrimEdges(bool trim)
        {
            if (calendarTrimEdges == trim)
            {
                return;
            }

            calendarTrimEdges = trim;
            calendarSpriteDirty = true;
        }

        /// <summary>
        /// 每帧看一眼日历窗口在不在：先记下游戏原本的底图，再换成 mod 的贴图。
        /// 换场景后日历窗口会重建，缓存失效了自然会在下一帧重新接管。
        /// </summary>
        private void RefreshCalendarSkin()
        {
            if (!calendarSkin)
            {
                return;
            }

            AdvCalendarUIManager calendar = AdvCalendarUIManager.Instance;
            if (calendar == null || calendar.UIPanel == null)
            {
                return;
            }

            if (calendarPanelImage == null || calendarPanelOwner != calendar.UIPanel)
            {
                Image image = FindCalendarBackground(calendar.UIPanel);
                if (image == null)
                {
                    return;
                }

                calendarPanelImage = image;
                calendarPanelOwner = calendar.UIPanel;
                calendarPanelOriginalSprite = image.sprite;
                calendarPanelOriginalType = image.type;
                calendarPanelOriginalColor = image.color;
            }

            if (calendarPanelSprite == null || calendarSpriteDirty)
            {
                Sprite fresh = BuildCalendarPanelSprite();
                if (fresh == null)
                {
                    return;
                }

                Sprite stale = calendarPanelSprite;
                calendarPanelSprite = fresh;
                calendarSpriteDirty = false;

                if (stale != null)
                {
                    Destroy(stale);
                }
            }

            if (calendarPanelImage.sprite == calendarPanelSprite &&
                calendarPanelImage.type == Image.Type.Simple &&
                calendarPanelImage.color == Color.white)
            {
                return;
            }

            calendarPanelImage.sprite = calendarPanelSprite;
            calendarPanelImage.type = Image.Type.Simple;
            calendarPanelImage.preserveAspect = false;
            calendarPanelImage.color = Color.white;

            if (!calendarSkinLogged)
            {
                calendarSkinLogged = true;
                log.Log("日历窗口底图已换成复古工业面板");
            }
        }

        private void RestoreCalendarSkin()
        {
            if (calendarPanelImage != null)
            {
                calendarPanelImage.sprite = calendarPanelOriginalSprite;
                calendarPanelImage.type = calendarPanelOriginalType;
                calendarPanelImage.color = calendarPanelOriginalColor;
            }

            calendarPanelImage = null;
            calendarPanelOwner = null;
            calendarPanelOriginalSprite = null;
            calendarSkinLogged = false;
        }

        private static Image FindCalendarBackground(GameObject panel)
        {
            if (panel == null)
            {
                return null;
            }

            Image direct = panel.GetComponent<Image>();
            if (direct != null)
            {
                return direct;
            }

            // 万一以后游戏把底图挪到子节点上，就按原贴图名字找
            foreach (Image image in panel.GetComponentsInChildren<Image>(true))
            {
                if (image != null && image.sprite != null &&
                    image.sprite.name == CalendarBackgroundSpriteName)
                {
                    return image;
                }
            }

            return null;
        }

        private Sprite BuildCalendarPanelSprite()
        {
            Texture2D texture = FindModTexture(CalendarTextureKey);
            if (texture == null)
            {
                return null;
            }

            // 图2自带一圈透明外边（阴影），直接铺满窗口的话边缘会漏出游戏场景；
            // 默认按不透明区域裁一刀，再让它铺满窗口；配置里可以关掉裁切。
            Rect rect = calendarTrimEdges
                ? GetOpaqueTextureRect(texture)
                : new Rect(0f, 0f, texture.width, texture.height);
            Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
            sprite.name = "DayHudCalendarPanel";
            return sprite;
        }

        private static Rect GetOpaqueTextureRect(Texture2D texture)
        {
            if (texture == null || texture.width <= 0 || texture.height <= 0)
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            Rect full = new Rect(0f, 0f, texture.width, texture.height);

            Color32[] pixels;
            try
            {
                pixels = texture.GetPixels32();
            }
            catch
            {
                // 贴图不可读就用整张，至少不会崩
                return full;
            }

            if (pixels == null || pixels.Length < texture.width * texture.height)
            {
                return full;
            }

            const byte threshold = 8;
            int width = texture.width;
            int height = texture.height;
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x].a <= threshold)
                    {
                        continue;
                    }

                    if (x < minX)
                    {
                        minX = x;
                    }

                    if (x > maxX)
                    {
                        maxX = x;
                    }

                    if (y < minY)
                    {
                        minY = y;
                    }

                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return full;
            }

            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
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
            statusText = CreateText("Status", 10f, 7f, new Color32(126, 200, 184, 220), FontStyles.Normal);

            rentVisible = false;
            LayoutText(false);
        }

        private void BuildMoneyBar()
        {
            GameObject rootObject = new GameObject("MoneyBar", typeof(RectTransform));
            moneyRoot = (RectTransform)rootObject.transform;
            moneyRoot.SetParent(transform, false);
            moneyRoot.anchorMin = new Vector2(0f, 1f);
            moneyRoot.anchorMax = new Vector2(0f, 1f);
            moneyRoot.pivot = new Vector2(0.5f, 0.5f);
            moneyRoot.sizeDelta = new Vector2(moneyWidth, moneyHeight);

            moneyShadow = CreateImage(moneyRoot, "Shadow", null, new Vector2(2f, -3f),
                new Vector2(moneyWidth, moneyHeight));
            moneyShadow.color = new Color(0f, 0f, 0f, 0.35f);


            moneyHighlight = CreateImage(moneyRoot, "Highlight", null, Vector2.zero,
                new Vector2(moneyWidth + 6f, moneyHeight + 6f));
            moneyHighlight.color = new Color32(225, 209, 156, 255);
            moneyHighlight.gameObject.SetActive(false);

            moneyBar = CreateImage(moneyRoot, "Bar", null, Vector2.zero, new Vector2(moneyWidth, moneyHeight));
            moneyBar.color = new Color(1f, 1f, 1f, 0f);

            moneyText = CreateText(moneyRoot, "Money", 22f, 10f, new Color32(24, 44, 46, 255), FontStyles.Normal);
            LayoutMoneyText();
            ApplyMoneyBarSprite();
            moneyRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// 左下角的离店图标，功能等价于店里那扇门的“离开店铺”，
        /// 但入口固定在 HUD 上，不用再去找墙上的门。
        /// </summary>
        private void BuildLeaveIcon()
        {
            GameObject rootObject = new GameObject("LeaveStore", typeof(RectTransform));
            leaveRoot = (RectTransform)rootObject.transform;
            leaveRoot.SetParent(transform, false);
            leaveRoot.anchorMin = Vector2.zero;
            leaveRoot.anchorMax = Vector2.zero;
            leaveRoot.pivot = new Vector2(0.5f, 0.5f);
            leaveRoot.sizeDelta = new Vector2(LeaveIconSize, LeaveIconSize);

            leaveHighlight = CreateImage(leaveRoot, "Highlight", null, Vector2.zero,
                new Vector2(LeaveIconSize + 8f, LeaveIconSize + 8f));
            leaveHighlight.color = new Color32(225, 209, 156, 255);
            leaveHighlight.gameObject.SetActive(false);

            leaveIcon = CreateImage(leaveRoot, "Icon", null, Vector2.zero,
                new Vector2(LeaveIconSize, LeaveIconSize));
            leaveIcon.color = Color.white;
            leaveIcon.raycastTarget = true;
            leaveIcon.preserveAspect = true;

            leaveButton = rootObject.AddComponent<DayHudIconButton>();
            leaveButton.Highlight = leaveHighlight.gameObject;
            leaveButton.Target = leaveRoot;
            leaveButton.Clicked = OnLeaveIconClicked;
            leaveButton.Entered = () => SetLeaveHovering(true);
            leaveButton.Exited = () => SetLeaveHovering(false);

            rootObject.SetActive(false);
        }

        private void ApplyLeaveIconSprite()
        {
            if (leaveIcon == null || leaveHighlight == null)
            {
                return;
            }

            Texture2D texture = FindModTexture(LeaveTextureKey);
            if (texture == null)
            {
                return;
            }

            leaveSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            leaveSprite.name = "DayHudLeaveStore";

            leaveIcon.sprite = leaveSprite;
            leaveHighlight.sprite = leaveSprite;

            // 悬停亮边用白色剪影，避免彩色底图被奶油色乘成脏色
            Texture2D maskTexture = FindModTexture(LeaveMaskKey);
            if (maskTexture != null)
            {
                leaveMaskSprite = Sprite.Create(maskTexture,
                    new Rect(0f, 0f, maskTexture.width, maskTexture.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                leaveMaskSprite.name = "DayHudLeaveStoreMask";
                leaveHighlight.sprite = leaveMaskSprite;
            }

            leaveHighlight.color = new Color32(225, 209, 156, 255);
            leaveIconReady = true;
            leaveRoot.gameObject.SetActive(ShouldShow());

            try
            {
                // 只让不透明像素响应点击，避免方形透明边也挡住鼠标
                leaveIcon.alphaHitTestMinimumThreshold = 0.15f;
            }
            catch
            {
                // 贴图不可读时退回整块矩形命中，不影响功能
            }
        }

        private void PlaceLeaveIcon()
        {
            if (leaveRoot == null || canvas == null)
            {
                return;
            }

            float scale = canvas.scaleFactor;
            if (scale <= 0.0001f)
            {
                return;
            }

            float leftInset = LetterboxManager.GameAreaX;
            float bottomInset = Screen.height - (LetterboxManager.GameAreaY + LetterboxManager.GameAreaHeight);
            float half = LeaveIconSize * 0.5f;

            Vector2 target = new Vector2(
                (leftInset + (LeaveIconMargin + half) * scale) / scale,
                (bottomInset + (LeaveIconMargin + half) * scale) / scale);

            if ((leaveRoot.anchoredPosition - target).sqrMagnitude > 0.04f)
            {
                leaveRoot.anchoredPosition = target;
            }
        }

        private void SetLeaveHovering(bool value)
        {
            if (leaveHovering == value)
            {
                return;
            }

            leaveHovering = value;
            if (leaveHighlight != null)
            {
                leaveHighlight.gameObject.SetActive(value);
            }

            if (value)
            {
                ShowLeaveTooltip();
            }
            else
            {
                HideLeaveTooltip();
            }
        }

        private void ShowLeaveTooltip()
        {
            if (TooltipManager._instance == null)
            {
                return;
            }

            try
            {
                StoreDoor door = FindLeaveDoor();
                string text = door != null ? door.GetTooltip() : null;
                if (string.IsNullOrEmpty(text))
                {
                    bool chinese = false;
                    try
                    {
                        chinese = LocHelper.GetCurrentLocaleCode().StartsWith("zh");
                    }
                    catch
                    {
                        // 忽略
                    }

                    text = chinese ? "离开店铺" : "Leave the store";
                }

                TooltipManager._instance.SetAndShowTooltip(text, this);
            }
            catch (Exception e)
            {
                log.Warning("显示离店提示失败：" + e.Message);
            }
        }

        private void HideLeaveTooltip()
        {
            if (TooltipManager._instance != null)
            {
                TooltipManager._instance.HideTooltip(this);
            }
        }

        private void OnLeaveIconClicked()
        {
            try
            {
                StoreDoor door = FindLeaveDoor();
                if (door == null)
                {
                    log.Warning("当前场景找不到门，离店图标暂时不能用");
                    return;
                }

                door.OnClick();
            }
            catch (Exception e)
            {
                log.Warning("通过 HUD 离店失败：" + e.Message);
            }
        }

        private StoreDoor FindLeaveDoor()
        {
            if (leaveDoor != null)
            {
                return leaveDoor;
            }

            // StoreDoor 组件本身可能已经被禁用交互，但实例还在，直接 GetComponent 取
            GameObject doorObject = FindDoorObject();
            if (doorObject != null)
            {
                leaveDoor = doorObject.GetComponent<StoreDoor>();
            }

            if (leaveDoor == null)
            {
                leaveDoor = FindObjectOfType<StoreDoor>();
            }

            return leaveDoor;
        }

        /// <summary>
        /// 模组配置：是否关掉墙上旧门的交互。默认关（只保留外观）。
        /// </summary>
        public void SetDoorInteractionDisabled(bool disabled)
        {
            disableDoorInteraction = disabled;

            if (disabled)
            {
                DisableDoorInteraction();
            }
            else
            {
                RestoreDoorInteraction();
            }
        }

        /// <summary>
        /// 旧的那扇门还留着外观，但不再响应鼠标：关掉碰撞体和交互/描边组件，
        /// 离店统一走左下角的 HUD 图标。
        /// </summary>
        private void DisableDoorInteraction()
        {
            GameObject door = FindDoorObject();
            if (door == null)
            {
                return;
            }

            if (doorInteractionDone && doorInteractionOwner == door)
            {
                return;
            }

            RestoreDoorInteraction();
            doorInteractionOwner = door;

            foreach (Collider2D collider in door.GetComponentsInChildren<Collider2D>(true))
            {
                if (collider == null)
                {
                    continue;
                }

                doorColliders.Add(collider);
                doorColliderStates.Add(collider.enabled);
                collider.enabled = false;
            }

            foreach (MonoBehaviour behaviour in door.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                {
                    continue;
                }

                // 门上可能还挂着别的 tooltip/trigger 脚本，全部停掉，只留 SpriteRenderer 外观
                doorBehaviours.Add(behaviour);
                doorBehaviourStates.Add(behaviour.enabled);
                behaviour.enabled = false;
            }

            if (TooltipManager._instance != null)
            {
                // 清掉可能已经卡在门上的旧提示，避免两个“离开店铺”同时出现
                TooltipManager._instance.HideTooltip();
            }

            doorInteractionDone = true;
            log.Log("旧门交互已关闭，只保留外观");
        }

        private void RestoreDoorInteraction()
        {
            for (int i = 0; i < doorColliders.Count && i < doorColliderStates.Count; i++)
            {
                if (doorColliders[i] != null)
                {
                    doorColliders[i].enabled = doorColliderStates[i];
                }
            }

            for (int i = 0; i < doorBehaviours.Count && i < doorBehaviourStates.Count; i++)
            {
                if (doorBehaviours[i] != null)
                {
                    doorBehaviours[i].enabled = doorBehaviourStates[i];
                }
            }

            doorColliders.Clear();
            doorColliderStates.Clear();
            doorBehaviours.Clear();
            doorBehaviourStates.Clear();
            doorInteractionOwner = null;
            doorInteractionDone = false;
        }

        private GameObject FindDoorObject()
        {
            GameObject interactable = FindSceneObject("--Interactable");
            if (interactable != null)
            {
                Transform door = interactable.transform.Find("Door");
                if (door != null)
                {
                    return door.gameObject;
                }
            }

            return FindSceneObject("Door");
        }

        private void ApplyMoneyBarSprite()
        {
            if (moneyRoot == null || moneyBar == null || moneyShadow == null)
            {
                return;
            }

            Texture2D texture = FindModTexture(MoneyTextureKey);
            if (texture == null)
            {
                moneyReady = false;
                moneyRoot.gameObject.SetActive(false);
                return;
            }

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "DayHudMoneyBar";

            moneyWidth = RetroWidth;
            moneyHeight = moneyWidth * texture.height / texture.width;
            Vector2 size = new Vector2(moneyWidth, moneyHeight);

            moneyRoot.sizeDelta = size;

            moneyShadow.sprite = sprite;
            moneyShadow.color = new Color(0f, 0f, 0f, 0.35f);
            RectTransform shadowRect = (RectTransform)moneyShadow.transform;
            shadowRect.sizeDelta = size;
            shadowRect.anchoredPosition = new Vector2(2f, -3f);

            Texture2D maskTexture = FindModTexture(MoneyMaskKey);
            Sprite maskSprite = maskTexture != null
                ? Sprite.Create(maskTexture, new Rect(0f, 0f, maskTexture.width, maskTexture.height),
                    new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect)
                : null;
            if (maskSprite != null)
            {
                maskSprite.name = "DayHudMoneyBarMask";
            }

            moneyHighlight.sprite = maskSprite != null ? maskSprite : sprite;
            moneyHighlight.color = new Color32(225, 209, 156, 255);
            RectTransform highlightRect = (RectTransform)moneyHighlight.transform;
            highlightRect.sizeDelta = size + new Vector2(6f, 6f);
            moneyHighlight.gameObject.SetActive(false);


            moneyBar.sprite = sprite;
            moneyBar.color = Color.white;
            ((RectTransform)moneyBar.transform).sizeDelta = size;

            LayoutMoneyText();
            moneyReady = true;
            moneyRoot.gameObject.SetActive(true);
        }

        private void LayoutMoneyText()
        {
            if (moneyText == null)
            {
                return;
            }

            float width = moneyWidth;
            float height = moneyHeight;
            RectTransform rect = moneyText.rectTransform;
            rect.sizeDelta = new Vector2(width * 0.64f, height * 0.44f);
            rect.anchoredPosition = new Vector2(width * 0.083f, -height * 0.035f);

            float size = Mathf.Clamp(height * 0.39f, 9f, 28f);
            moneyText.fontSizeMax = size;
            moneyText.fontSize = size;
            moneyText.fontSizeMin = 9f;
        }

        private void RefreshMoney()
        {
            if (!moneyReady || moneyRoot == null || moneyText == null)
            {
                return;
            }

            if (!PlayerStore.HasInstance)
            {
                moneyRoot.gameObject.SetActive(false);
                return;
            }

            PlayerStore store = PlayerStore.Instance;
            if (store == null)
            {
                moneyRoot.gameObject.SetActive(false);
                return;
            }

            int cash = store.GetCash();
            if (cash != shownCash)
            {
                shownCash = cash;
                moneyText.text = cash + "¢";
            }

            moneyRoot.gameObject.SetActive(true);
            UpdateMoneyBarPosition();
        }

        private void UpdateMoneyBarPosition()
        {
            if (moneyRoot == null || dial == null)
            {
                return;
            }

            const float gap = -10f;
            float gameWidth = GetGameAreaWidthInCanvasUnits();
            bool sideBySide = gameWidth >= panelWidth + gap + moneyWidth + MarginX * 2f;

            if (sideBySide)
            {
                // 和表盘同一条顶边，留一点余量给描边
                float x = dial.anchoredPosition.x + panelWidth * 0.5f + gap + moneyWidth * 0.5f;
                float y = dial.anchoredPosition.y + panelHeight * 0.5f - 4f - moneyHeight * 0.5f;
                moneyRoot.anchoredPosition = new Vector2(x, y);
            }
            else
            {
                // 窄屏放不下就落到表盘下面，仍然左对齐
                float x = dial.anchoredPosition.x;
                float y = dial.anchoredPosition.y - panelHeight * 0.5f - 10f - moneyHeight * 0.5f;
                moneyRoot.anchoredPosition = new Vector2(x, y);
            }

        }

        private float GetGameAreaWidthInCanvasUnits()
        {
            float scale = canvas != null && canvas.scaleFactor > 0.0001f ? canvas.scaleFactor : 1f;
            float widthPixels = LetterboxManager.GameAreaWidth > 0 ? LetterboxManager.GameAreaWidth : Screen.width;
            return widthPixels / scale;
        }

        private void UpdateMoneyInteraction()
        {
            if (!moneyReady || moneyRoot == null || moneyBar == null || !moneyRoot.gameObject.activeSelf)
            {
                SetMoneyHovering(false);
                return;
            }

            AdvCalendarUIManager calendar = AdvCalendarUIManager.Instance;
            if (calendar != null && calendar.UIPanel != null && calendar.UIPanel.activeSelf)
            {
                SetMoneyHovering(false);
                return;
            }

            bool over = RectTransformUtility.RectangleContainsScreenPoint(
                moneyBar.rectTransform, (Vector2)Input.mousePosition, null);

            if (over && InteractionBlocked())
            {
                over = false;
            }

            if (over != moneyHovering)
            {
                SetMoneyHovering(over);
            }

            if (over && Input.GetMouseButtonDown(0))
            {
                OpenCreditStatus();
            }
        }

        private void SetMoneyHovering(bool value)
        {
            if (moneyHovering == value)
            {
                return;
            }

            moneyHovering = value;

            if (moneyHighlight != null)
            {
                moneyHighlight.gameObject.SetActive(value);
            }

            if (value)
            {
                ShowMoneyTooltip();
            }
            else
            {
                HideMoneyTooltip();
            }
        }

        private void ShowMoneyTooltip()
        {
            if (TooltipManager._instance == null)
            {
                return;
            }

            try
            {
                string text = ModHelper.GetLocalized(modId, "credit_hover");
                if (string.IsNullOrEmpty(text) || text == "credit_hover")
                {
                    bool chinese = false;
                    try
                    {
                        chinese = LocHelper.GetCurrentLocaleCode().StartsWith("zh");
                    }
                    catch
                    {
                        // 忽略
                    }

                    text = chinese ? "你当前的信用点余额" : "Your current credit count";
                }

                TooltipManager._instance.SetAndShowTooltip(text, this);
            }
            catch (Exception e)
            {
                log.Warning("显示信用点提示失败：" + e.Message);
            }
        }

        private void HideMoneyTooltip()
        {
            if (TooltipManager._instance != null)
            {
                TooltipManager._instance.HideTooltip(this);
            }
        }

        private void OpenCreditStatus()
        {
            AdvCalendarUIManager calendar = AdvCalendarUIManager.Instance;
            if (calendar == null)
            {
                log.Warning("当前场景没有日历界面，打不开信用点状态");
                return;
            }

            try
            {
                calendar.OpenUI();
                calendar.OnStatusButtonClicked();
            }
            catch (Exception e)
            {
                log.Error("打开信用点状态的时候崩了：" + e);
            }

            SetMoneyHovering(false);
        }

        private void BuildNotifications()
        {
            GameObject rootObject = new GameObject("Notifications", typeof(RectTransform));
            notificationRoot = (RectTransform)rootObject.transform;
            notificationRoot.SetParent(transform, false);
            notificationRoot.anchorMin = new Vector2(0f, 1f);
            notificationRoot.anchorMax = new Vector2(0f, 1f);
            notificationRoot.pivot = new Vector2(0f, 1f);
            notificationRoot.sizeDelta = new Vector2(NotificationWidth,
                (MaxStoredNotifications + 1) * (NotificationRowHeight + NotificationGap) - NotificationGap);
            notificationRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// 往左侧通知区推一条消息；同文会去重，最新的在最上面。
        /// </summary>
        public void Notify(string message, NoticeTone tone = NoticeTone.Info, float duration = 7f,
            Action onClick = null, bool allowDuplicate = false)
        {
            if (string.IsNullOrEmpty(message) || notificationRoot == null)
            {
                return;
            }

            if (!allowDuplicate)
            {
                foreach (NotificationItem existing in notifications)
                {
                    if (existing.Message == message)
                    {
                        return;
                    }
                }
            }

            NotificationItem item = CreateNotificationItem(message, tone, duration, onClick);
            notifications.Insert(0, item);

            while (notifications.Count > GetMaxStoredNotifications())
            {
                RemoveNotificationAt(notifications.Count - 1);
            }

            LayoutNotifications();
            notificationRoot.gameObject.SetActive(true);
        }

        /// <summary>
        /// 永久通知：不会自动过期，需要点右侧的 × 才会消失。
        /// </summary>
        public void NotifyPersistent(string message, NoticeTone tone = NoticeTone.Info,
            Action onClick = null, bool allowDuplicate = false)
        {
            Notify(message, tone, 0f, onClick, allowDuplicate);
        }

        private NotificationItem CreateNotificationItem(string message, NoticeTone tone, float duration,
            Action onClick)
        {
            bool permanent = duration <= 0f;

            GameObject rowObject = new GameObject("Notice", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform rect = (RectTransform)rowObject.transform;
            rect.SetParent(notificationRoot, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(NotificationWidth, NotificationRowHeight);

            CanvasGroup group = rowObject.GetComponent<CanvasGroup>();
            group.alpha = permanent ? 1f : 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image border = CreateStretchImage(rect, "Border", new Color32(120, 100, 80, 100));
            RectTransform borderRect = border.rectTransform;
            borderRect.offsetMin = new Vector2(-1f, -1f);
            borderRect.offsetMax = new Vector2(1f, 1f);

            Image background = CreateStretchImage(rect, "BG", new Color32(28, 21, 34, 242));

            Image accent = CreateStretchImage(rect, "Accent", ToneColor(tone));
            RectTransform accentRect = accent.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.offsetMin = new Vector2(0f, 3f);
            accentRect.offsetMax = new Vector2(4f, -3f);

            GameObject textObject = new GameObject("Text", typeof(RectTransform));
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 2f);
            textRect.offsetMax = new Vector2(permanent ? -30f : -20f, -2f);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = noteFont != null ? noteFont : ResolveFont();
            if (font != null)
            {
                text.font = font;
            }

            text.fontStyle = noteStyle;
            text.fontSize = 18f;
            text.enableAutoSizing = true;
            text.fontSizeMax = 18f;
            text.fontSizeMin = 12f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = new Color32(235, 225, 205, 255);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            text.text = message;

            if (onClick != null && !permanent)
            {
                GameObject chevronObject = new GameObject("Chevron", typeof(RectTransform));
                RectTransform chevronRect = (RectTransform)chevronObject.transform;
                chevronRect.SetParent(rect, false);
                chevronRect.anchorMin = new Vector2(1f, 0f);
                chevronRect.anchorMax = new Vector2(1f, 1f);
                chevronRect.pivot = new Vector2(1f, 0.5f);
                chevronRect.sizeDelta = new Vector2(16f, 0f);
                chevronRect.anchoredPosition = new Vector2(-4f, 0f);

                TextMeshProUGUI chevron = chevronObject.AddComponent<TextMeshProUGUI>();
                if (font != null)
                {
                    chevron.font = font;
                }

                chevron.fontStyle = FontStyles.Normal;
                chevron.fontSize = 18f;
                chevron.enableAutoSizing = false;
                chevron.alignment = TextAlignmentOptions.Center;
                chevron.color = new Color32(196, 180, 150, 255);
                chevron.raycastTarget = false;
                chevron.text = "›";
            }

            RectTransform closeRect = null;
            TextMeshProUGUI closeText = null;
            if (permanent)
            {
                GameObject closeObject = new GameObject("Close", typeof(RectTransform));
                closeRect = (RectTransform)closeObject.transform;
                closeRect.SetParent(rect, false);
                closeRect.anchorMin = new Vector2(1f, 0f);
                closeRect.anchorMax = new Vector2(1f, 1f);
                closeRect.pivot = new Vector2(1f, 0.5f);
                closeRect.sizeDelta = new Vector2(20f, 0f);
                closeRect.anchoredPosition = new Vector2(-5f, 0f);

                closeText = closeObject.AddComponent<TextMeshProUGUI>();
                if (font != null)
                {
                    closeText.font = font;
                }

                closeText.fontStyle = FontStyles.Normal;
                closeText.fontSize = 20f;
                closeText.enableAutoSizing = false;
                closeText.alignment = TextAlignmentOptions.Center;
                closeText.color = new Color32(206, 150, 130, 255);
                closeText.raycastTarget = false;
                closeText.text = "×";
            }

            return new NotificationItem
            {
                Rect = rect,
                Group = group,
                Text = text,
                Background = background,
                BaseColor = new Color32(28, 21, 34, 242),
                OnClick = onClick,
                Message = message,
                Age = 0f,
                Duration = duration,
                IsPermanent = permanent,
                CloseRect = closeRect,
                CloseText = closeText
            };
        }

        private static Image CreateStretchImage(Transform parent, string name, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)imageObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = false;
            return image;
        }

        private static Color ToneColor(NoticeTone tone)
        {
            switch (tone)
            {
                case NoticeTone.Warning:
                    return new Color32(226, 186, 110, 255);
                case NoticeTone.Danger:
                    return new Color32(214, 116, 96, 255);
                case NoticeTone.Muted:
                    return new Color32(160, 148, 126, 255);
                default:
                    return new Color32(126, 200, 184, 255);
            }
        }

        private void LayoutNotifications()
        {
            int maxStored = GetMaxStoredNotifications();
            while (notifications.Count > maxStored)
            {
                RemoveNotificationAt(notifications.Count - 1);
            }

            if (notificationRoot != null)
            {
                notificationRoot.sizeDelta = new Vector2(NotificationWidth,
                    (MaxStoredNotifications + 1) * (NotificationRowHeight + NotificationGap) - NotificationGap);
            }

            int shown = notificationsExpanded
                ? notifications.Count
                : Mathf.Min(notifications.Count, VisibleNotifications);

            for (int i = 0; i < notifications.Count; i++)
            {
                NotificationItem item = notifications[i];
                bool show = i < shown;
                item.Rect.gameObject.SetActive(show);

                if (show)
                {
                    item.Rect.sizeDelta = new Vector2(NotificationWidth, NotificationRowHeight);
                    item.Rect.anchoredPosition =
                        new Vector2(0f, -i * (NotificationRowHeight + NotificationGap));
                }
                else
                {
                    item.Hovered = false;
                    if (hoveredNotice == item)
                    {
                        HideNotificationTooltip();
                    }
                }
            }

            bool hasMore = notifications.Count > VisibleNotifications;
            if (hasMore)
            {
                EnsureCollapseRow();
                collapseRow.Rect.gameObject.SetActive(true);
                collapseRow.Rect.sizeDelta = new Vector2(NotificationWidth, NotificationRowHeight);
                collapseRow.Rect.anchoredPosition =
                    new Vector2(0f, -shown * (NotificationRowHeight + NotificationGap));

                string text = notificationsExpanded
                    ? Loc("notify_collapse", "收起通知", "Collapse")
                    : string.Format(
                        Loc("notify_folded", "已折叠通知（{0}）", "{0} more notifications"),
                        notifications.Count - shown);

                collapseRow.Message = text;
                collapseRow.Text.text = text;
                collapseRow.Group.alpha = 1f;
            }
            else if (collapseRow != null)
            {
                collapseRow.Rect.gameObject.SetActive(false);
                collapseRow.Hovered = false;
                if (hoveredNotice == collapseRow)
                {
                    HideNotificationTooltip();
                }
            }

            if (notificationRoot != null)
            {
                notificationRoot.gameObject.SetActive(notifications.Count > 0);
            }
        }

        private int GetMaxStoredNotifications()
        {
            if (canvas == null || dial == null)
            {
                return MaxStoredNotifications;
            }

            float scale = canvas.scaleFactor > 0.0001f ? canvas.scaleFactor : 1f;
            float gameHeightPixels = LetterboxManager.GameAreaHeight > 0
                ? LetterboxManager.GameAreaHeight
                : Screen.height;
            float top = -dial.anchoredPosition.y + panelHeight * 0.5f + 10f;
            float available = gameHeightPixels / scale - top - 10f;
            float step = NotificationRowHeight + NotificationGap;
            int fit = Mathf.FloorToInt((available - step) / step);
            return Mathf.Clamp(fit, VisibleNotifications, MaxStoredNotifications);
        }

        private void EnsureCollapseRow()
        {
            if (collapseRow != null)
            {
                return;
            }

            collapseRow = CreateNotificationItem(string.Empty, NoticeTone.Muted, float.MaxValue,
                ToggleNotificationsExpanded);
            collapseRow.Text.alignment = TextAlignmentOptions.Center;
            collapseRow.Text.fontSize = 15f;
            collapseRow.Text.fontSizeMax = 15f;
            collapseRow.Text.fontSizeMin = 13f;
            collapseRow.Text.color = new Color32(196, 180, 150, 255);
            collapseRow.Background.color = new Color32(24, 18, 30, 242);
            collapseRow.BaseColor = new Color32(24, 18, 30, 242);
            collapseRow.Group.alpha = 1f;
        }

        private void ToggleNotificationsExpanded()
        {
            notificationsExpanded = !notificationsExpanded;

            if (notificationsExpanded)
            {
                foreach (NotificationItem item in notifications)
                {
                    item.Age = 0f;
                    item.Group.alpha = 1f;
                }
            }

            HideNotificationTooltip();
            LayoutNotifications();
        }

        private void ShowNotificationTooltip(NotificationItem item, bool footer = false)
        {
            if (item == null || TooltipManager._instance == null)
            {
                return;
            }

            try
            {
                string text = item.Message;

                if (footer)
                {
                    string hint = notificationsExpanded
                        ? Loc("notify_collapse_hint", "点击收起通知", "Click to collapse")
                        : Loc("notify_expand_hint", "点击展开全部通知", "Click to expand all");
                    if (!string.IsNullOrEmpty(hint))
                    {
                        text = text + "\n" + hint;
                    }
                }
                else if (item.OnClick != null)
                {
                    string hint = Loc("notify_click_focus", "点击聚焦", "Click to focus");
                    if (!string.IsNullOrEmpty(hint))
                    {
                        text = text + "\n" + hint;
                    }
                }

                if (item.IsPermanent)
                {
                    string dismiss = Loc("notify_dismiss_hint", "点 × 关闭", "Click × to dismiss");
                    if (!string.IsNullOrEmpty(dismiss))
                    {
                        text = text + "\n" + dismiss;
                    }
                }

                hoveredNotice = item;
                TooltipManager._instance.SetAndShowTooltip(text, this);
            }
            catch (Exception e)
            {
                log.Warning("显示通知提示失败：" + e.Message);
            }
        }

        private void HideNotificationTooltip()
        {
            hoveredNotice = null;

            // 指针已经移到表盘或货币条上的话，让它们自己的提示保持显示
            if (hovering || moneyHovering)
            {
                return;
            }

            if (TooltipManager._instance != null)
            {
                TooltipManager._instance.HideTooltip(this);
            }
        }

        private void RemoveNotificationAt(int index)
        {
            if (index < 0 || index >= notifications.Count)
            {
                return;
            }

            NotificationItem item = notifications[index];
            notifications.RemoveAt(index);
            if (hoveredNotice == item)
            {
                HideNotificationTooltip();
            }

            if (item.Rect != null)
            {
                UnityEngine.Object.Destroy(item.Rect.gameObject);
            }
        }

        private void ClearNotifications()
        {
            HideNotificationTooltip();

            for (int i = notifications.Count - 1; i >= 0; i--)
            {
                RemoveNotificationAt(i);
            }

            if (collapseRow != null)
            {
                if (collapseRow.Rect != null)
                {
                    UnityEngine.Object.Destroy(collapseRow.Rect.gameObject);
                }

                collapseRow = null;
            }

            notificationsExpanded = false;
            notificationsInitialized = false;
            nextMachinePoll = 0f;
            machineScanLogged = false;
            machineOutputs.Clear();
        }

        private void UpdateNotifications()
        {
            if (notificationRoot == null || dial == null)
            {
                return;
            }

            float x = dial.anchoredPosition.x - panelWidth * 0.5f;
            float y = dial.anchoredPosition.y - panelHeight * 0.5f - 10f;
            notificationRoot.anchoredPosition = new Vector2(x, y);

            float dt = Time.unscaledDeltaTime;
            Vector2 mouse = (Vector2)Input.mousePosition;
            bool clicked = Input.GetMouseButtonDown(0);

            AdvCalendarUIManager calendar = AdvCalendarUIManager.Instance;
            bool modalOpen = calendar != null && calendar.UIPanel != null && calendar.UIPanel.activeSelf;

            int shown = notificationsExpanded
                ? notifications.Count
                : Mathf.Min(notifications.Count, VisibleNotifications);

            for (int i = notifications.Count - 1; i >= 0; i--)
            {
                if (i >= shown)
                {
                    continue;
                }

                NotificationItem item = notifications[i];

                if (item.IsPermanent)
                {
                    item.Group.alpha = 1f;
                }
                else
                {
                    item.Age += dt;

                    float fadeIn = Mathf.Clamp01(item.Age / 0.15f);
                    float fadeOut = Mathf.Clamp01((item.Duration - item.Age) / 0.4f);
                    item.Group.alpha = fadeIn * fadeOut;
                }

                bool over = !modalOpen &&
                            RectTransformUtility.RectangleContainsScreenPoint(item.Rect, mouse, null);

                bool closeOver = item.CloseRect != null && !modalOpen &&
                                 RectTransformUtility.RectangleContainsScreenPoint(
                                     item.CloseRect, mouse, null);

                if (item.CloseText != null)
                {
                    item.CloseText.color = closeOver
                        ? new Color32(238, 120, 100, 255)
                        : new Color32(206, 150, 130, 255);
                }

                if (closeOver && clicked)
                {
                    HideNotificationTooltip();
                    RemoveNotificationAt(i);
                    continue;
                }

                if (over != item.Hovered)
                {
                    item.Hovered = over;
                    if (item.Background != null)
                    {
                        item.Background.color = over
                            ? new Color32(52, 41, 60, 248)
                            : item.BaseColor;
                    }

                    if (over)
                    {
                        ShowNotificationTooltip(item);
                    }
                    else if (hoveredNotice == item)
                    {
                        HideNotificationTooltip();
                    }
                }

                if (over && clicked && item.OnClick != null)
                {
                    Action callback = item.OnClick;
                    HideNotificationTooltip();
                    RemoveNotificationAt(i);
                    try
                    {
                        callback.Invoke();
                    }
                    catch (Exception e)
                    {
                        log.Warning("点击通知失败：" + e.Message);
                    }

                    continue;
                }

                if (!item.IsPermanent && item.Age >= item.Duration)
                {
                    RemoveNotificationAt(i);
                }
            }

            LayoutNotifications();

            if (collapseRow != null && collapseRow.Rect.gameObject.activeSelf)
            {
                bool overFooter = !modalOpen &&
                                  RectTransformUtility.RectangleContainsScreenPoint(
                                      collapseRow.Rect, mouse, null);

                if (overFooter != collapseRow.Hovered)
                {
                    collapseRow.Hovered = overFooter;
                    if (collapseRow.Background != null)
                    {
                        collapseRow.Background.color = overFooter
                            ? new Color32(46, 36, 54, 248)
                            : collapseRow.BaseColor;
                    }

                    if (overFooter)
                    {
                        ShowNotificationTooltip(collapseRow, true);
                    }
                    else if (hoveredNotice == collapseRow)
                    {
                        HideNotificationTooltip();
                    }
                }

                if (overFooter && clicked)
                {
                    HideNotificationTooltip();
                    ToggleNotificationsExpanded();
                }
            }

            if (!notificationsInitialized)
            {
                notificationsInitialized = true;
                nextMachinePoll = Time.unscaledTime + 0.5f;
                PollMachines(false);
                return;
            }

            if (Time.unscaledTime >= nextMachinePoll)
            {
                nextMachinePoll = Time.unscaledTime + 0.5f;
                PollMachines(true);
            }
        }

        private void PollMachines(bool notify)
        {
            try
            {
                if (!PlayerStore.HasInstance)
                {
                    return;
                }

                EmporiumEntry entry = EmporiumEntry.Instance;
                if (entry == null)
                {
                    return;
                }

                List<GameItem> machines = new List<GameItem>();
                HashSet<int> seenIds = new HashSet<int>();
                List<GameItem> allItems = entry.GetAllItems();
                if (allItems == null)
                {
                    return;
                }

                foreach (GameItem item in allItems)
                {
                    if (item == null || !item.IsTag("HYDROPONIC_MACHINE_TAG"))
                    {
                        continue;
                    }

                    if (seenIds.Add(item.GetUniqueID()))
                    {
                        machines.Add(item);
                    }
                }

                HashSet<int> alive = new HashSet<int>();
                foreach (GameItem machine in machines)
                {
                    if (machine == null)
                    {
                        continue;
                    }

                    GameGridInventory output = MachineHydroponic.GetOutputSlot(machine);
                    if (output == null || output.childItems == null)
                    {
                        continue;
                    }

                    Dictionary<int, GameItem> itemsById = new Dictionary<int, GameItem>();
                    Dictionary<int, int> current = new Dictionary<int, int>();
                    foreach (GameItem item in output.childItems)
                    {
                        if (item == null)
                        {
                            continue;
                        }

                        int itemId = item.GetUniqueID();
                        itemsById[itemId] = item;
                        current[itemId] = item.unitCount;
                    }

                    int machineId = machine.GetUniqueID();
                    alive.Add(machineId);

                    if (notify && machineOutputs.TryGetValue(machineId, out Dictionary<int, int> previous))
                    {
                        foreach (KeyValuePair<int, int> pair in current)
                        {
                            int oldUnits = previous.TryGetValue(pair.Key, out int value) ? value : 0;
                            int delta = pair.Value - oldUnits;
                            if (delta > 0 && itemsById.TryGetValue(pair.Key, out GameItem produced))
                            {
                                OnMachineProduced(machine, produced, delta);
                            }
                        }
                    }

                    machineOutputs[machineId] = current;
                }

                List<int> stale = new List<int>();
                foreach (int machineId in machineOutputs.Keys)
                {
                    if (!alive.Contains(machineId))
                    {
                        stale.Add(machineId);
                    }
                }

                foreach (int machineId in stale)
                {
                    machineOutputs.Remove(machineId);
                }

                if (!machineScanLogged)
                {
                    machineScanLogged = true;
                    log.Log("通知区监听水培机 " + machines.Count + " 台");
                }
            }
            catch (Exception e)
            {
                log.Warning("扫描水培产出失败：" + e.Message);
            }
        }

        private void OnMachineProduced(GameItem machine, GameItem item, int count)
        {
            string machineName = machine != null && !string.IsNullOrEmpty(machine.name)
                ? machine.name
                : Loc("notify_machine_default", "水培系统", "Hydroponics");
            string itemName = item != null && !string.IsNullOrEmpty(item.name)
                ? item.name
                : Loc("notify_item_default", "作物", "Crop");

            string text = count > 1
                ? Loc("notify_produce", "{0} 产出 {1} ×{2}", "{0} produced {1} ×{2}",
                    machineName, itemName, count)
                : Loc("notify_produce_one", "{0} 产出 {1}", "{0} produced {1}",
                    machineName, itemName);

            GameItem target = machine;
            Notify(text, NoticeTone.Info, 10f, () => FocusGameItem(target), true);
        }

        /// <summary>打开并居中一个带 contentWindow 的物品/机器窗口。</summary>
        public bool FocusGameItem(GameItem machine)
        {
            if (machine == null)
            {
                return false;
            }

            try
            {
                if (machine.contentWindow != null)
                {
                    machine.contentWindow.Show();
                    machine.contentWindow.Center();
                    return true;
                }
            }
            catch (Exception e)
            {
                log.Warning("聚焦物品窗口失败：" + e.Message);
            }

            return false;
        }

        private string Loc(string key, string fallbackZh, string fallbackEn, params object[] args)
        {
            string text = args.Length > 0
                ? ModHelper.GetLocalized(modId, key, args)
                : ModHelper.GetLocalized(modId, key);

            if (string.IsNullOrEmpty(text) || text == key)
            {
                text = IsChineseLocale() ? fallbackZh : fallbackEn;
            }

            return args.Length > 0 ? string.Format(text, args) : text;
        }

        private static bool IsChineseLocale()
        {
            try
            {
                return LocHelper.GetCurrentLocaleCode().StartsWith("zh");
            }
            catch
            {
                return false;
            }
        }

        private void Update()
        {
            if (canvas == null || dial == null)
            {
                return;
            }

            RefreshCalendarSkin();
            if (disableDoorInteraction)
            {
                DisableDoorInteraction();
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
                SetMoneyHovering(false);
                SetLeaveHovering(false);
                if (leaveRoot != null)
                {
                    leaveRoot.gameObject.SetActive(false);
                }

                ClearNotifications();
                noteFontTried = false;

                // 离开店铺去 afterhour 场景（比如化学模组的地下实验室）时不能把墙上的日历/
                // 信用点屏幕再放回去：游戏已经收起店铺 UI，这里 SetActive 会让它们盖在别的
                // 场景背景上（用户 2026-11-19 截图：地下室里还看得到 DAY 1 / 507₡）。
                // 另外它们可能在切场景的空档被别处重新激活过，这里要主动再收一次。
                if (IsOutsideStore() && hideOutsideStore)
                {
                    HideWallDisplaysOutside();
                    return;
                }

                if (calendarHidden)
                {
                    calendarHidden = false;
                    RestorePhysicalCalendar();
                }

                if (creditHidden)
                {
                    creditHidden = false;
                    RestoreCreditDisplay();
                }

                if (feedbackHidden)
                {
                    feedbackHidden = false;
                    RestoreFeedbackBox();
                }

                return;
            }

            outsideHideLogged = false;

            if (!noteFontTried)
            {
                noteFontTried = true;
                AdoptNoteFont();
            }

            if (hideWallCalendar)
            {
                if (!calendarHidden)
                {
                    calendarHidden = true;
                    HidePhysicalCalendar();
                }
            }
            else if (calendarHidden)
            {
                calendarHidden = false;
                RestorePhysicalCalendar();
            }

            if (hideCreditDisplay)
            {
                if (!creditHidden)
                {
                    creditHidden = true;
                    HideCreditDisplay();
                }
            }
            else if (creditHidden)
            {
                creditHidden = false;
                RestoreCreditDisplay();
            }

            if (hideFeedbackBox)
            {
                if (!feedbackHidden)
                {
                    feedbackHidden = true;
                    HideFeedbackBox();
                }
            }
            else if (feedbackHidden)
            {
                feedbackHidden = false;
                RestoreFeedbackBox();
            }

            if (calendarHidden)
            {
                KeepObjectsHidden(hiddenCalendarObjects);
            }

            if (creditHidden)
            {
                KeepObjectsHidden(hiddenCreditObjects);
            }

            if (feedbackHidden)
            {
                KeepObjectsHidden(hiddenFeedbackObjects);
            }

            if (!retroApplied)
            {
                // 资源加载事件没赶上的话，这里兜一下
                ApplyRetroBackground();
            }

            if (!moneyReady)
            {
                ApplyMoneyBarSprite();
            }

            if (!leaveIconReady)
            {
                ApplyLeaveIconSprite();
            }

            if (leaveRoot != null && leaveRoot.gameObject.activeSelf != leaveIconReady)
            {
                leaveRoot.gameObject.SetActive(leaveIconReady);
            }

            KeepInsideGameArea();
            EnsureFont();
            RefreshRent();
            RefreshStatus();
            RefreshMoney();
            UpdateMoneyInteraction();

            int day = Mathf.Max(1, StoreStation.GetDayCounter());
            UpdateNotifications();
            if (day == shownDay)
            {
                return;
            }

            shownDay = day;
            RefreshText(day);
            Punch();
        }

        /// <summary>
        /// 在店铺外（淘荒/地下实验室这类 afterhour 场景）把墙上的日历、信用点屏幕、反馈箱
        /// 强制收起来。切场景的空档里这些对象可能被别处重新激活，光"保持隐藏"不够，要再找一遍。
        /// </summary>
        private void HideWallDisplaysOutside()
        {
            bool search = Time.unscaledTime >= nextOutsideHideAttempt;
            if (search)
            {
                nextOutsideHideAttempt = Time.unscaledTime + 2f;
            }

            PruneDeadObjects(hiddenCalendarObjects);
            PruneDeadObjects(hiddenCreditObjects);
            PruneDeadObjects(hiddenFeedbackObjects);

            if (search && hiddenCalendarObjects.Count == 0)
            {
                HidePhysicalCalendar();
            }

            if (search && hiddenCreditObjects.Count == 0)
            {
                HideCreditDisplay();
            }

            if (search && hiddenFeedbackObjects.Count == 0)
            {
                HideFeedbackBox();
            }

            if (hiddenCalendarObjects.Count > 0)
            {
                calendarHidden = true;
            }

            if (hiddenCreditObjects.Count > 0)
            {
                creditHidden = true;
            }

            if (hiddenFeedbackObjects.Count > 0)
            {
                feedbackHidden = true;
            }

            KeepObjectsHidden(hiddenCalendarObjects);
            KeepObjectsHidden(hiddenCreditObjects);
            KeepObjectsHidden(hiddenFeedbackObjects);

            if (!outsideHideLogged)
            {
                outsideHideLogged = true;
                log.Log("离开店铺：墙上日历/信用点屏幕/反馈箱保持隐藏（" +
                        hiddenCalendarObjects.Count + "/" + hiddenCreditObjects.Count + "/" +
                        hiddenFeedbackObjects.Count + " 个对象）");
            }
        }

        private static void PruneDeadObjects(List<HiddenObject> hidden)
        {
            for (int i = hidden.Count - 1; i >= 0; i--)
            {
                if (hidden[i].GameObject == null)
                {
                    hidden.RemoveAt(i);
                }
            }
        }

        private void OnDestroy()
        {
            DayHudApi.Detach(this);
            RestoreCalendarSkin();
            SetHovering(false);
            ClearNotifications();
            SetMoneyHovering(false);
            SetLeaveHovering(false);
            RestorePhysicalCalendar();
            RestoreCreditDisplay();
            RestoreFeedbackBox();
            RestoreDoorInteraction();

            if (leaveSprite != null)
            {
                Destroy(leaveSprite);
                leaveSprite = null;
            }

            if (leaveMaskSprite != null)
            {
                Destroy(leaveMaskSprite);
                leaveMaskSprite = null;
            }
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
            // Canvas 根节点也会收到子对象的悬停事件，这里只认表盘本体，
            // 否则鼠标移到左下角离店图标上也会把表盘点亮、弹出“查看日历”。
            if (!PointerOnDial(eventData))
            {
                return;
            }

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

            if (!PointerOnDial(eventData))
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

        private bool PointerOnDial(PointerEventData eventData)
        {
            if (dial == null || eventData == null)
            {
                return false;
            }

            return RectTransformUtility.RectangleContainsScreenPoint(dial, eventData.position,
                eventData.pressEventCamera);
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
            ApplyMoneyBarSprite();
            shownDay = int.MinValue;
            rentDirty = true;
            statusDirty = true;
        }

        private void OnLocaleChanged(Locale locale)
        {
            noteFont = null;
            // 便签被藏起来后 TMPLocalizer 不会跟着换字体，这时直接用游戏的当前语言字体
            noteFontTried = calendarHidden;

            ApplyFont(ResolveFont());
            ApplyStyle(FontStyles.Normal);
            shownDay = int.MinValue;
            rentDirty = true;
            statusDirty = true;

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

            float statusSize = Mathf.Clamp(width * 0.068f, 8f, 19f);
            statusText.fontSizeMax = statusSize;
            statusText.fontSize = statusSize;
            statusText.fontSizeMin = 8f;
            RectTransform statusRect = statusText.rectTransform;
            statusRect.sizeDelta = new Vector2(width * 0.73f, height * 0.09f);
            statusRect.anchoredPosition = new Vector2(0f, height * 0.295f);

            float daySize = Mathf.Clamp(width * 0.20f, 16f, 46f);
            dayText.fontSizeMax = daySize;
            dayText.fontSize = daySize;
            dayText.fontSizeMin = 14f;
            RectTransform dayRect = dayText.rectTransform;
            dayRect.sizeDelta = new Vector2(width * 0.68f, height * 0.28f);
            dayRect.anchoredPosition = new Vector2(0f, height * (showRent ? 0.097f : 0.05f));

            float weekSize = Mathf.Clamp(width * 0.084f, 10f, 23f);
            weekdayText.fontSizeMax = weekSize;
            weekdayText.fontSize = weekSize;
            weekdayText.fontSizeMin = 9f;
            RectTransform weekRect = weekdayText.rectTransform;
            weekRect.sizeDelta = new Vector2(width * 0.54f, height * 0.14f);
            weekRect.anchoredPosition = new Vector2(0f, -height * (showRent ? 0.086f : 0.13f));

            float rentSize = Mathf.Clamp(width * 0.0735f, 9f, 21f);
            rentText.fontSizeMax = rentSize;
            rentText.fontSize = rentSize;
            rentText.fontSizeMin = 9f;
            RectTransform rentRect = rentText.rectTransform;
            rentRect.sizeDelta = new Vector2(width * 0.68f, height * 0.1f);
            rentRect.anchoredPosition = new Vector2(0f, -height * 0.237f);
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
            // 用显示出来的天数算租金，避免开局 dayCounter=0 时显示“第1天”却提示“今天付租”
            int displayDay = Mathf.Max(1, StoreStation.GetDayCounter());
            int days = show ? (7 - displayDay % 7) % 7 : -1;
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
            int amount = mortgage ? store.mortgageWeeklyPayment : store.GetRentValue();
            rentText.text = FormatRent(days, mortgage) + " " + amount + "¢";
            rentText.color = days <= 1
                ? new Color32(232, 190, 112, 255)
                : new Color32(142, 208, 192, 255);
        }

        /// <summary>
        /// 表盘顶部补一行营业状态，顺便把空着的地方填起来。
        /// </summary>
        private void RefreshStatus()
        {
            if (statusText == null)
            {
                return;
            }

            if (!PlayerStore.HasInstance)
            {
                statusText.gameObject.SetActive(false);
                return;
            }

            PlayerStore store = PlayerStore.Instance;
            if (store == null)
            {
                statusText.gameObject.SetActive(false);
                return;
            }

            int state = (int)store.storeState;
            var clock = DayHudApi.ReadClockDisplay();
            int minute = clock.Day > 0 ? clock.MinuteOfDay : -1;
            bool boundary = clock.Day > 0 && clock.Waiting;
            if (!statusDirty && state == shownStoreState && minute == shownClockMinute && boundary == shownClockBoundary)
            {
                return;
            }

            statusDirty = false;
            shownStoreState = state;
            shownClockMinute = minute;
            shownClockBoundary = boundary;

            string key;
            Color color;
            if (store.storeState == PlayerStore.StoreState.OPEN)
            {
                key = "status_open";
                color = new Color32(126, 200, 184, 220);
            }
            else if (store.storeState == PlayerStore.StoreState.MORNING)
            {
                key = "status_morning";
                color = new Color32(226, 186, 110, 220);
            }
            else
            {
                key = "status_closed";
                color = new Color32(160, 148, 126, 220);
            }
            if (boundary)
            {
                key = store.storeState == PlayerStore.StoreState.MORNING ? "clock_wait_open"
                    : store.storeState == PlayerStore.StoreState.OPEN ? "clock_wait_close" : "clock_wait_sleep";
            }

            string text = ModHelper.GetLocalized(modId, key);
            if (string.IsNullOrEmpty(text) || text == key)
            {
                text = DefaultStatus(store.storeState);
            }

            statusText.text = minute >= 0 ? FormattableString.Invariant($"{text} · {minute / 60:00}:{minute % 60:00}") : text;
            statusText.color = color;
            statusText.gameObject.SetActive(true);
        }

        private static string DefaultStatus(PlayerStore.StoreState state)
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

            if (state == PlayerStore.StoreState.OPEN)
            {
                return chinese ? "营业中" : "OPEN";
            }

            if (state == PlayerStore.StoreState.MORNING)
            {
                return chinese ? "准备中" : "MORNING";
            }

            return chinese ? "已打烊" : "CLOSED";
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

            if (statusText != null)
            {
                statusText.font = font;
            }

            if (moneyText != null)
            {
                moneyText.font = font;
            }

            foreach (NotificationItem item in notifications)
            {
                if (item.Text != null)
                {
                    item.Text.font = font;
                }
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

            if (statusText != null)
            {
                statusText.fontStyle = style;
            }

            if (moneyText != null)
            {
                moneyText.fontStyle = style;
            }

            foreach (NotificationItem item in notifications)
            {
                if (item.Text != null)
                {
                    item.Text.fontStyle = style;
                }
            }
        }

        /// <summary>
        /// 三行文字统一用墙上日历便签的字体和字重，省得表盘一个字体、便签另一个字体。
        /// </summary>
        private void AdoptNoteFont()
        {
            try
            {
                StoreCalendar calendar = FindStoreCalendar();
                if (calendar == null || !calendar.gameObject.activeInHierarchy)
                {
                    return;
                }

                TextMeshProUGUI source = calendar.dayTMP;
                if (source == null || source.font == null)
                {
                    return;
                }

                noteFont = source.font;
                noteStyle = source.fontStyle;
                ApplyFont(noteFont);
                ApplyStyle(noteStyle);
                shownDay = int.MinValue;
                log.Log("字体已跟日历便签统一：" + noteFont.name);
            }
            catch (Exception e)
            {
                log.Warning("找日历便签字体失败：" + e.Message);
            }
        }

        /// <summary>
        /// 墙上的纸质日历和 HUD 重复了，整块收起来；mod 关掉时再放回去。
        /// </summary>
        private void HidePhysicalCalendar()
        {
            try
            {
                StoreCalendar calendar = FindStoreCalendar();
                if (calendar != null)
                {
                    HideObject(calendar.gameObject, hiddenCalendarObjects);

                    Transform parent = calendar.transform.parent;
                    if (parent != null)
                    {
                        HideChild(parent, "CalendarTop", hiddenCalendarObjects);
                        HideChild(parent, "CalendarGlow", hiddenCalendarObjects);
                    }

                    if (calendar.dayTMP != null)
                    {
                        HideObject(calendar.dayTMP.gameObject, hiddenCalendarObjects);
                    }
                }

                // 按名字兜底，防止组件引用指到别的对象
                HideAllSceneObjects("Calendar", hiddenCalendarObjects);
                HideAllSceneObjects("CalendarTop", hiddenCalendarObjects);
                HideAllSceneObjects("CalendarGlow", hiddenCalendarObjects);
                HideAllSceneObjects("rentTMP", hiddenCalendarObjects);

                if (hiddenCalendarObjects.Count > 0)
                {
                    log.Log("墙上日历已隐藏 " + hiddenCalendarObjects.Count + " 个对象：" +
                            NamesOf(hiddenCalendarObjects));
                }
            }
            catch (Exception e)
            {
                log.Warning("隐藏墙上日历失败：" + e.Message);
            }
        }

        /// <summary>
        /// 墙上的信用点屏幕也由 HUD 接管，默认收起。
        /// </summary>
        private void HideCreditDisplay()
        {
            try
            {
                GameObject interactable = FindSceneObject("--Interactable");
                if (interactable != null)
                {
                    HideChild(interactable.transform, "CreditDisplay", hiddenCreditObjects);
                    HideChild(interactable.transform, "CreditDisplayGlow", hiddenCreditObjects);
                }

                GameObject worldCanvas = FindSceneObject("CanvasWorldStoreInteract");
                if (worldCanvas != null)
                {
                    HideChild(worldCanvas.transform, "moneyTeshMeshPro", hiddenCreditObjects);
                }

                HideAllSceneObjects("CreditDisplay", hiddenCreditObjects);
                HideAllSceneObjects("CreditDisplayGlow", hiddenCreditObjects);
                HideAllSceneObjects("moneyTeshMeshPro", hiddenCreditObjects);

                if (hiddenCreditObjects.Count > 0)
                {
                    log.Log("墙上信用点屏幕已隐藏 " + hiddenCreditObjects.Count + " 个对象：" +
                            NamesOf(hiddenCreditObjects));
                }
            }
            catch (Exception e)
            {
                log.Warning("隐藏信用点屏幕失败：" + e.Message);
            }
        }

        /// <summary>
        /// 墙上的反馈箱也由日历的“反馈”标签页接管，默认收起。
        /// </summary>
        private void HideFeedbackBox()
        {
            try
            {
                HideAllSceneObjects("feedback_box", hiddenFeedbackObjects);

                if (hiddenFeedbackObjects.Count > 0)
                {
                    log.Log("墙上反馈箱已隐藏 " + hiddenFeedbackObjects.Count + " 个对象：" +
                            NamesOf(hiddenFeedbackObjects));
                }
            }
            catch (Exception e)
            {
                log.Warning("隐藏墙上反馈箱失败：" + e.Message);
            }
        }

        private void HideChild(Transform parent, string childName, List<HiddenObject> hidden)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                HideObject(child.gameObject, hidden);
            }
        }

        private void HideSceneObject(string objectName, List<HiddenObject> hidden)
        {
            GameObject target = FindSceneObject(objectName);
            if (target != null)
            {
                HideObject(target, hidden);
            }
        }

        private void HideAllSceneObjects(string objectName, List<HiddenObject> hidden)
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go != null && go.name == objectName && go.scene.IsValid())
                {
                    HideObject(go, hidden);
                }
            }
        }

        private static string NamesOf(List<HiddenObject> objects)
        {
            List<string> names = new List<string>();
            foreach (HiddenObject entry in objects)
            {
                if (entry.GameObject != null)
                {
                    names.Add(entry.GameObject.name);
                }
            }

            return string.Join(",", names.ToArray());
        }

        private void HideSceneChild(string parentName, string childName, List<HiddenObject> hidden)
        {
            GameObject parent = FindSceneObject(parentName);
            if (parent != null)
            {
                HideChild(parent.transform, childName, hidden);
            }
        }

        private void HideObject(GameObject target, List<HiddenObject> hidden)
        {
            if (target == null)
            {
                return;
            }

            foreach (HiddenObject entry in hidden)
            {
                if (entry.GameObject == target)
                {
                    if (target.activeSelf)
                    {
                        target.SetActive(false);
                    }

                    return;
                }
            }

            hidden.Add(new HiddenObject
            {
                GameObject = target,
                WasActive = target.activeSelf
            });

            if (target.activeSelf)
            {
                target.SetActive(false);
            }
        }

        private void RestorePhysicalCalendar()
        {
            RestoreObjects(hiddenCalendarObjects);
        }

        private void RestoreCreditDisplay()
        {
            RestoreObjects(hiddenCreditObjects);
        }

        private void RestoreFeedbackBox()
        {
            RestoreObjects(hiddenFeedbackObjects);
        }

        private void RestoreObjects(List<HiddenObject> hidden)
        {
            foreach (HiddenObject entry in hidden)
            {
                GameObject target = entry.GameObject;
                if (target != null && entry.WasActive && !target.activeSelf)
                {
                    target.SetActive(true);
                }
            }

            hidden.Clear();
        }

        private static void KeepObjectsHidden(List<HiddenObject> hidden)
        {
            foreach (HiddenObject entry in hidden)
            {
                GameObject target = entry.GameObject;
                if (target != null && target.activeSelf)
                {
                    target.SetActive(false);
                }
            }
        }

        private static GameObject FindSceneObject(string objectName)
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go != null && go.name == objectName && go.scene.IsValid())
                {
                    return go;
                }
            }

            return null;
        }

        private static StoreCalendar FindStoreCalendar()
        {
            foreach (StoreCalendar calendar in Resources.FindObjectsOfTypeAll<StoreCalendar>())
            {
                if (calendar == null || calendar.gameObject == null || !calendar.gameObject.scene.IsValid())
                {
                    continue;
                }

                return calendar;
            }

            return null;
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
            return CreateText(dial, name, maxSize, minSize, color, style);
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, float maxSize, float minSize, Color color,
            FontStyles style)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)textObject.transform;
            rect.SetParent(parent, false);
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

            float halfWidth = panelWidth * 0.5f * scale;
            float halfHeight = panelHeight * 0.5f * scale;
            float marginX = MarginX * scale;
            float marginY = MarginY * scale;

            // 边距和面板一样按 Canvas 单位缩放，再统一换算成像素
            Vector2 target = new Vector2(
                (leftInset + marginX + halfWidth) / scale,
                -(topInset + marginY + halfHeight) / scale);

            if ((dial.anchoredPosition - target).sqrMagnitude > 0.04f)
            {
                dial.anchoredPosition = target;
            }

            PlaceLeaveIcon();
        }

        private bool ShouldShow()
        {
            NewGameData newGameData = NewGameData.Instance;
            if (newGameData != null && newGameData.isInMainMenu)
            {
                return false;
            }

            // PlayerStore 只在对局里存在；用它当"这局已经起来了"的判据，
            // 免得在买/读档前就去碰 StoreStation 把单例提前建出来。
            if (!PlayerStore.HasInstance)
            {
                return false;
            }

            return !hideOutsideStore || !IsOutsideStore();
        }

        private static bool IsOutsideStore()
        {
            try
            {
                MapUIManager map = MapUIManager.Instance;
                if (map != null && map.IsOutside)
                {
                    return true;
                }

                // 商店 UI 都不在场景里了，说明不是店里
                if (StoreUIManager.Instance == null)
                {
                    return true;
                }
            }
            catch
            {
                // 场景切换的空档，按店里处理，下一帧会再判断
            }

            return false;
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


















