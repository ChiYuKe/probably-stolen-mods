using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DayHud
{
    /// <summary>
    /// 把反馈箱的声望内容接进高级日历，作为一个独立标签页。
    /// 只借用它的 Layout（阵营列）和 NotePanel（perk 说明），
    /// 反馈箱自己的纸面背景、标题栏、关闭按钮都隐藏掉，页面背景用日历自己的。
    /// </summary>
    public class CalendarFeedbackTab : MonoBehaviour
    {
        private string modId;
        private ModLog log;

        private AdvCalendarUIManager calendar;
        private ReputationUIManager reputation;
        private bool featureEnabled = true;
        private Button tabButton;
        private TextMeshProUGUI tabLabel;
        private string tabText;
        private TextMeshProUGUI headerLabel;
        private string headerText;

        private GameObject host;
        private bool attached;

        private RectTransform layoutRect;
        private RectTransform noteRect;
        private Transform layoutParent;
        private Transform noteParent;
        private int layoutSibling;
        private int noteSibling;
        private Vector2 layoutAnchorMin;
        private Vector2 layoutAnchorMax;
        private Vector2 layoutPivot;
        private Vector2 layoutPosition;
        private Vector2 layoutSize;
        private Vector3 layoutScale;
        private Quaternion layoutRotation;
        private Vector2 noteAnchorMin;
        private Vector2 noteAnchorMax;
        private Vector2 notePivot;
        private Vector2 notePosition;
        private Vector2 noteSize;
        private Vector3 noteScale;
        private Quaternion noteRotation;
        private bool noteWasActive;
        private CanvasGroup noteGroup;
        private float noteGroupAlpha;
        private Graphic noteGraphic;
        private bool noteGraphicWasEnabled;
        private TextMeshProUGUI[] noteTexts;
        private float[] noteFontSizes;
        private bool[] noteAutoSizing;
        private RectTransform[] noteAdjustedRects;
        private Vector2[] noteAdjustedPositions;
        private Vector2[] noteAdjustedSizes;

        private LayoutGroup layoutGroup;
        private TextAnchor layoutAlignment;
        private bool layoutAlignmentStored;
        private HorizontalOrVerticalLayoutGroup layoutSpacingGroup;
        private float layoutSpacing;
        private bool layoutSpacingStored;

        private GameObject panelChrome;
        private bool panelChromeWasActive;
        private Graphic[] panelGraphics;
        private bool[] panelGraphicsEnabled;

        public void Init(string modId, ModLog log)
        {
            this.modId = modId;
            this.log = log;
            tabText = ModHelper.GetLocalized(modId, "calendar_tab_feedback");
            if (string.IsNullOrEmpty(tabText) || tabText == "calendar_tab_feedback")
            {
                tabText = "反馈";
            }

            headerText = ModHelper.GetLocalized(modId, "calendar_feedback_title");
            if (string.IsNullOrEmpty(headerText) || headerText == "calendar_feedback_title")
            {
                headerText = "阵营声望";
            }
        }

        /// <summary>
        /// 模组配置：是否在日历里显示“反馈”标签页。关掉时移除标签并还原反馈面板。
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            featureEnabled = enabled;

            if (!enabled)
            {
                AdvCalendarUIManager current = calendar;
                ResetReferences();

                // 如果关开关时正好停在“反馈”页，切回日历页，别留一个空页面
                if (current != null && current.UIPanel != null && current.UIPanel.activeSelf)
                {
                    current.OnCalendarButtonClicked();
                }
            }
        }

        private void Update()
        {
            if (!featureEnabled)
            {
                return;
            }

            if (calendar == null)
            {
                if (attached || host != null || tabButton == null)
                {
                    attached = false;
                    host = null;
                    tabButton = null;
                    tabLabel = null;
                    reputation = null;
                }
            }

            if (calendar != null && calendar.UIPanel == null)
            {
                ResetReferences();
            }

            EnsureTab();

            string localized = ModHelper.GetLocalized(modId, "calendar_tab_feedback");
            if (!string.IsNullOrEmpty(localized) && localized != "calendar_tab_feedback")
            {
                tabText = localized;
            }

            if (tabLabel != null && tabLabel.text != tabText)
            {
                tabLabel.text = tabText;
            }

            if (attached && calendar != null && calendar.UIPanel != null && !calendar.UIPanel.activeSelf)
            {
                DetachPanel();
                SetTabNormal();
            }

            if (attached && calendar != null &&
                (calendar.calendarSection.activeSelf || calendar.nightlyReportSection.activeSelf ||
                 calendar.statusSection.activeSelf))
            {
                DetachPanel();
                SetTabNormal();
            }

            if (attached && reputation != null && reputation.uiPanel != null &&
                !reputation.uiPanel.activeSelf)
            {
                DetachPanel();
                SetTabNormal();
                if (calendar != null)
                {
                    calendar.OnCalendarButtonClicked();
                }
            }
        }

        private void LateUpdate()
        {
            if (headerLabel != null && headerLabel.text != headerText)
            {
                headerLabel.text = headerText;
            }
        }

        private void EnsureTab()
        {
            if (tabButton != null)
            {
                return;
            }

            if (calendar == null)
            {
                calendar = AdvCalendarUIManager.Instance;
            }

            if (calendar == null || calendar.statusButton == null)
            {
                return;
            }

            CreateTab();
        }

        private void CreateTab()
        {
            try
            {
                GameObject clone = Instantiate(calendar.statusButton.gameObject,
                    calendar.statusButton.transform.parent);
                clone.name = "DayHudFeedbackTab";
                clone.transform.SetSiblingIndex(calendar.statusButton.transform.GetSiblingIndex() + 1);

                tabLabel = clone.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tabLabel != null)
                {
                    tabLabel.text = tabText;
                }

                tabButton = clone.GetComponent<Button>();
                if (tabButton != null)
                {
                    tabButton.onClick = new Button.ButtonClickedEvent();
                    tabButton.onClick.AddListener(OnFeedbackTabClicked);
                    tabButton.image.color = calendar.normalColor;
                }

                calendar.calendarButton.onClick.AddListener(OnOtherTabClicked);
                calendar.nightlyReportButton.onClick.AddListener(OnOtherTabClicked);
                calendar.statusButton.onClick.AddListener(OnOtherTabClicked);

                log.Log("日历反馈标签已创建");
            }
            catch (Exception e)
            {
                log.Warning("创建日历反馈标签失败：" + e.Message);
            }
        }

        private void OnFeedbackTabClicked()
        {
            try
            {
                if (calendar == null)
                {
                    return;
                }

                if (reputation == null)
                {
                    reputation = ReputationUIManager.Instance;
                }

                if (reputation == null || reputation.uiPanel == null)
                {
                    log.Warning("当前场景没有反馈箱面板，无法显示声望标签页");
                    return;
                }

                calendar.OnAnySectionButtonClicked();
                AttachPanel();
                reputation.OpenUI();

                if (tabButton != null)
                {
                    tabButton.image.color = calendar.selectedColor;
                }
            }
            catch (Exception e)
            {
                log.Warning("打开日历反馈标签失败：" + e.Message);
            }
        }

        private void OnOtherTabClicked()
        {
            DetachPanel();
            SetTabNormal();
        }

        private void SetTabNormal()
        {
            if (tabButton != null && calendar != null)
            {
                tabButton.image.color = calendar.normalColor;
            }
        }

        private void AttachPanel()
        {
            if (attached || calendar == null || reputation == null || reputation.uiPanel == null)
            {
                return;
            }

            GameObject section = calendar.statusSection;
            if (section == null)
            {
                return;
            }

            RectTransform panelRect = reputation.uiPanel.GetComponent<RectTransform>();
            layoutRect = reputation.layout != null
                ? reputation.layout.transform as RectTransform
                : null;
            noteRect = panelRect != null ? panelRect.Find("NotePanel") as RectTransform : null;

            if (layoutRect == null || noteRect == null)
            {
                log.Warning("反馈箱缺少 Layout/NotePanel，无法嵌入日历");
                return;
            }

            StoreRect(layoutRect, out layoutParent, out layoutSibling,
                out layoutAnchorMin, out layoutAnchorMax, out layoutPivot,
                out layoutPosition, out layoutSize, out layoutScale, out layoutRotation);
            StoreRect(noteRect, out noteParent, out noteSibling,
                out noteAnchorMin, out noteAnchorMax, out notePivot,
                out notePosition, out noteSize, out noteScale, out noteRotation);

            layoutGroup = layoutRect.GetComponent<LayoutGroup>();
            if (layoutGroup != null)
            {
                layoutAlignment = layoutGroup.childAlignment;
                layoutAlignmentStored = true;
                layoutGroup.childAlignment = TextAnchor.MiddleCenter;
            }

            layoutSpacingGroup = layoutRect.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (layoutSpacingGroup != null)
            {
                layoutSpacing = layoutSpacingGroup.spacing;
                layoutSpacingStored = true;
                layoutSpacingGroup.spacing = 50f;
            }

            host = new GameObject("DayHudFeedbackPage", typeof(RectTransform));
            RectTransform hostRect = (RectTransform)host.transform;
            RectTransform pageRect = calendar.UIPanel.GetComponent<RectTransform>();
            if (pageRect == null)
            {
                return;
            }

            hostRect.SetParent(pageRect, false);
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.offsetMin = Vector2.zero;
            hostRect.offsetMax = Vector2.zero;
            hostRect.localScale = Vector3.one;
            hostRect.SetAsLastSibling();

            layoutRect.SetParent(hostRect, false);
            layoutRect.anchorMin = Vector2.zero;
            layoutRect.anchorMax = Vector2.one;
            layoutRect.pivot = new Vector2(0.5f, 0.5f);
            layoutRect.offsetMin = new Vector2(40f, 220f);
            layoutRect.offsetMax = new Vector2(-40f, -235f);
            layoutRect.localScale = Vector3.one * 1.7f;
            layoutRect.localRotation = Quaternion.identity;

            noteRect.SetParent(hostRect, false);
            noteRect.anchorMin = new Vector2(0f, 0f);
            noteRect.anchorMax = new Vector2(1f, 0f);
            noteRect.pivot = new Vector2(0.5f, 0f);
            noteRect.offsetMin = new Vector2(40f, 80f);
            noteRect.offsetMax = new Vector2(-40f, 185f);
            noteRect.localScale = Vector3.one;
            noteRect.localRotation = Quaternion.identity;
            noteWasActive = noteRect.gameObject.activeSelf;
            noteRect.gameObject.SetActive(true);

            noteGroup = noteRect.GetComponent<CanvasGroup>();
            if (noteGroup != null)
            {
                noteGroupAlpha = noteGroup.alpha;
                noteGroup.alpha = 1f;
            }

            noteGraphic = noteRect.GetComponent<Graphic>();
            if (noteGraphic != null)
            {
                noteGraphicWasEnabled = noteGraphic.enabled;
                noteGraphic.enabled = false;
            }

            ApplyNoteFonts();

            Transform chrome = reputation.uiPanel.transform.Find("Panel");
            if (chrome != null)
            {
                panelChrome = chrome.gameObject;
                panelChromeWasActive = panelChrome.activeSelf;
                panelChrome.SetActive(false);
            }

            panelGraphics = reputation.uiPanel.GetComponents<Graphic>();
            panelGraphicsEnabled = new bool[panelGraphics.Length];
            for (int i = 0; i < panelGraphics.Length; i++)
            {
                panelGraphicsEnabled[i] = panelGraphics[i].enabled;
                panelGraphics[i].enabled = false;
            }

            attached = true;
            Canvas.ForceUpdateCanvases();
            log.Log("反馈面板内容已嵌入日历");
        }

        private void ApplyNoteFonts()
        {
            if (reputation == null)
            {
                return;
            }

            noteTexts = new[]
            {
                reputation.perkTitle,
                reputation.perkStatus,
                reputation.perkProgress,
                reputation.perkdescription,
                reputation.perkFlavorText
            };
            float[] sizes = { 22f, 16f, 16f, 18f, 16f };
            noteFontSizes = new float[noteTexts.Length];
            noteAutoSizing = new bool[noteTexts.Length];

            for (int i = 0; i < noteTexts.Length; i++)
            {
                TextMeshProUGUI text = noteTexts[i];
                if (text == null)
                {
                    noteFontSizes[i] = 0f;
                    continue;
                }

                noteFontSizes[i] = text.fontSize;
                noteAutoSizing[i] = text.enableAutoSizing;
                text.enableAutoSizing = false;
                text.fontSize = sizes[i];
            }

            noteAdjustedRects = new[]
            {
                reputation.perkTitle != null ? reputation.perkTitle.rectTransform : null,
                reputation.perkdescription != null ? reputation.perkdescription.rectTransform : null,
                reputation.perkFlavorText != null ? reputation.perkFlavorText.rectTransform : null
            };
            Vector2[] positions =
            {
                new Vector2(90f, -6f),
                new Vector2(90f, -38f),
                new Vector2(90f, -80f)
            };
            Vector2[] rectSizes =
            {
                new Vector2(1200f, 30f),
                new Vector2(1200f, 40f),
                new Vector2(1200f, 24f)
            };

            noteAdjustedPositions = new Vector2[noteAdjustedRects.Length];
            noteAdjustedSizes = new Vector2[noteAdjustedRects.Length];
            for (int i = 0; i < noteAdjustedRects.Length; i++)
            {
                RectTransform rect = noteAdjustedRects[i];
                if (rect == null)
                {
                    continue;
                }

                noteAdjustedPositions[i] = rect.anchoredPosition;
                noteAdjustedSizes[i] = rect.sizeDelta;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = positions[i];
                rect.sizeDelta = rectSizes[i];
            }
        }

        private void RestoreNoteFonts()
        {
            if (noteTexts == null)
            {
                return;
            }

            for (int i = 0; i < noteTexts.Length; i++)
            {
                TextMeshProUGUI text = noteTexts[i];
                if (text == null || noteFontSizes == null)
                {
                    continue;
                }

                text.fontSize = noteFontSizes[i];
                text.enableAutoSizing = noteAutoSizing != null && noteAutoSizing[i];
            }

            if (noteAdjustedRects != null)
            {
                for (int i = 0; i < noteAdjustedRects.Length; i++)
                {
                    RectTransform rect = noteAdjustedRects[i];
                    if (rect == null)
                    {
                        continue;
                    }

                    rect.anchoredPosition = noteAdjustedPositions[i];
                    rect.sizeDelta = noteAdjustedSizes[i];
                }
            }

            noteTexts = null;
            noteFontSizes = null;
            noteAutoSizing = null;
            noteAdjustedRects = null;
            noteAdjustedPositions = null;
            noteAdjustedSizes = null;
        }

        private void CreateHeader(RectTransform hostRect)
        {
            TextMeshProUGUI text = null;
            Transform template = calendar.statusSection != null
                ? calendar.statusSection.transform.Find("ActivePerksTMP")
                : null;
            if (template != null)
            {
                GameObject clone = Instantiate(template.gameObject, hostRect);
                clone.name = "FeedbackHeader";
                clone.SetActive(true);
                RectTransform cloneRect = (RectTransform)clone.transform;
                cloneRect.anchorMin = new Vector2(0f, 1f);
                cloneRect.anchorMax = new Vector2(0f, 1f);
                cloneRect.pivot = new Vector2(0f, 1f);
                cloneRect.anchoredPosition = new Vector2(40f, -185f);
                cloneRect.sizeDelta = new Vector2(520f, 40f);
                cloneRect.localScale = Vector3.one;
                text = clone.GetComponent<TextMeshProUGUI>();
            }

            if (text == null)
            {
                GameObject headerObject = new GameObject("FeedbackHeader", typeof(RectTransform));
                RectTransform rect = (RectTransform)headerObject.transform;
                rect.SetParent(hostRect, false);
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(40f, -185f);
                rect.sizeDelta = new Vector2(520f, 40f);
                text = headerObject.AddComponent<TextMeshProUGUI>();

                TMP_FontAsset font = null;
                if (calendar != null && calendar.statusButton != null)
                {
                    TextMeshProUGUI label = calendar.statusButton.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (label != null)
                    {
                        font = label.font;
                    }
                }

                if (font == null)
                {
                    font = TMP_Settings.defaultFontAsset;
                }

                if (font != null)
                {
                    text.font = font;
                }

                text.fontSize = 18f;
                text.enableAutoSizing = true;
                text.fontSizeMax = 18f;
                text.fontSizeMin = 14f;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.color = new Color32(150, 70, 45, 255);
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Ellipsis;
            }

            headerLabel = text;
            if (headerLabel != null)
            {
                headerLabel.text = headerText;
                headerLabel.raycastTarget = false;
            }
        }

        private void DetachPanel()
        {
            if (!attached)
            {
                return;
            }

            try
            {
                if (reputation != null && reputation.uiPanel != null && reputation.uiPanel.activeSelf)
                {
                    reputation.CloseUI();
                }

                if (layoutRect != null && layoutParent != null)
                {
                    layoutRect.SetParent(layoutParent, false);
                    RestoreRect(layoutRect, layoutSibling,
                        layoutAnchorMin, layoutAnchorMax, layoutPivot,
                        layoutPosition, layoutSize, layoutScale, layoutRotation);
                }

                if (noteRect != null && noteParent != null)
                {
                    noteRect.SetParent(noteParent, false);
                    RestoreRect(noteRect, noteSibling,
                        noteAnchorMin, noteAnchorMax, notePivot,
                        notePosition, noteSize, noteScale, noteRotation);
                    noteRect.gameObject.SetActive(noteWasActive);

                    if (noteGroup != null)
                    {
                        noteGroup.alpha = noteGroupAlpha;
                    }

                    if (noteGraphic != null)
                    {
                        noteGraphic.enabled = noteGraphicWasEnabled;

                    }
                }

                RestoreNoteFonts();

                if (layoutGroup != null && layoutAlignmentStored)
                {
                    layoutGroup.childAlignment = layoutAlignment;
                }

                if (layoutSpacingGroup != null && layoutSpacingStored)
                {
                    layoutSpacingGroup.spacing = layoutSpacing;
                }

                if (panelChrome != null && panelChromeWasActive)
                {
                    panelChrome.SetActive(true);
                }

                if (panelGraphics != null && panelGraphicsEnabled != null)
                {
                    for (int i = 0; i < panelGraphics.Length && i < panelGraphicsEnabled.Length; i++)
                    {
                        if (panelGraphics[i] != null)
                        {
                            panelGraphics[i].enabled = panelGraphicsEnabled[i];
                        }
                    }
                }
            }
            catch (Exception e)
            {
                log.Warning("还原反馈箱面板失败：" + e.Message);
            }

            if (host != null)
            {
                Destroy(host);
                host = null;
            }

            layoutRect = null;
            noteRect = null;
            layoutParent = null;
            noteParent = null;
            layoutGroup = null;
            layoutAlignmentStored = false;
            layoutSpacingGroup = null;
            layoutSpacingStored = false;
            noteGroup = null;
            noteGraphic = null;
            headerLabel = null;
            panelChrome = null;
            panelGraphics = null;
            panelGraphicsEnabled = null;
            attached = false;
        }

        private static void StoreRect(RectTransform rect, out Transform parent, out int sibling,
            out Vector2 anchorMin, out Vector2 anchorMax, out Vector2 pivot,
            out Vector2 position, out Vector2 size, out Vector3 scale, out Quaternion rotation)
        {
            parent = rect.parent;
            sibling = rect.GetSiblingIndex();
            anchorMin = rect.anchorMin;
            anchorMax = rect.anchorMax;
            pivot = rect.pivot;
            position = rect.anchoredPosition;
            size = rect.sizeDelta;
            scale = rect.localScale;
            rotation = rect.localRotation;
        }

        private static void RestoreRect(RectTransform rect, int sibling,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 position, Vector2 size, Vector3 scale, Quaternion rotation)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = scale;
            rect.localRotation = rotation;
            rect.SetSiblingIndex(sibling);
        }

        private void ResetReferences()
        {
            DetachPanel();

            try
            {
                if (calendar != null)
                {
                    calendar.calendarButton.onClick.RemoveListener(OnOtherTabClicked);
                    calendar.nightlyReportButton.onClick.RemoveListener(OnOtherTabClicked);
                    calendar.statusButton.onClick.RemoveListener(OnOtherTabClicked);
                }

                if (tabButton != null)
                {
                    tabButton.onClick.RemoveListener(OnFeedbackTabClicked);
                    Destroy(tabButton.gameObject);
                }
            }
            catch (Exception e)
            {
                log.Warning("清理日历反馈标签失败：" + e.Message);
            }

            tabButton = null;
            tabLabel = null;
            calendar = null;
            reputation = null;
        }

        private void OnDestroy()
        {
            ResetReferences();
        }
    }
}














