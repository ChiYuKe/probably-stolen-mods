using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace InventorySearch
{
    /// <summary>
    /// 检索窗口本体：搜索框、范围/排序/违禁筛选、结果列表、点击定位。
    /// 界面走游戏自带的 CustomUI，外观和模组设置、调试面板保持一致。
    /// </summary>
    internal static class SearchWindow
    {
        private const string WindowId = "inventory_search.window";
        private const int MaxRows = 150;
        private const float RowTextWidth = 528f;

        private static string modId = "inventory_search";
        private static ModLog log;

        /// <summary>定位成功后自动收起窗口，由模组设置里的开关控制。</summary>
        public static bool CloseOnFocus;

        private static CustomUIWindow window;
        private static RectTransform resultsHost;
        private static ScrollRect scroll;
        private static Vector2? savedPosition;

        private static List<ContainerRoot> roots = new List<ContainerRoot>();
        private static List<SearchEntry> entries = new List<SearchEntry>();
        private static List<SearchEntry> shown = new List<SearchEntry>();
        private static List<string> areaLabels = new List<string>();

        private static string query = "";
        private static int areaIndex;
        private static int sortIndex;
        private static bool onlyContraband;
        private static int totalCount;
        private static int matchCount;
        private static bool truncated;

        private static Coroutine debounce;
        private static Coroutine clearHighlight;
        private static GameInventory highlightedInventory;

        private static readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();

        public static void Initialize(string id, ModLog logger)
        {
            modId = id;
            log = logger;
        }

        public static bool IsOpen
        {
            get { return window != null && window.IsAlive && window.IsVisible; }
        }

        public static void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public static void Open()
        {
            if (CustomUIManager.Instance == null)
            {
                log?.Warning("当前场景没有 CustomUIManager，开不了检索窗口");
                return;
            }

            if (window != null && window.IsAlive)
            {
                window.Show();
                Refresh(true);
                return;
            }

            try
            {
                Build();
            }
            catch (Exception e)
            {
                log?.Error("创建检索窗口失败：" + e);
                window = null;
                resultsHost = null;
            }
        }

        public static void Close()
        {
            CustomUIWindow current = window;
            if (current != null && current.IsAlive && current.Rect != null)
            {
                savedPosition = current.Rect.anchoredPosition;
            }

            window = null;
            resultsHost = null;
            scroll = null;
            StopDebounce();

            try
            {
                if (current != null && current.IsAlive)
                {
                    current.Close();
                }
            }
            catch
            {
                // 窗口可能已经被场景卸载带走了
            }
        }

        /// <summary>模组被禁用/卸载时用：关窗口并把高亮也收掉。</summary>
        public static void Shutdown()
        {
            Close();
            ClearHighlightNow();
        }

        private static void Build()
        {
            CustomUIBuilder builder = CustomUIManager.Window(
                WindowId,
                Loc.Get("window_title", "仓库检索", "Inventory Search"));
            if (builder == null)
            {
                log?.Warning("CreateWindow 返回空，窗口没建起来");
                return;
            }

            window = builder.Handle;
            builder
                .SetSize(720f, 640f)
                .SetDraggable(true)
                .SetCloseOnEscape(true)
                .OnClose(OnWindowClosed);

            if (savedPosition.HasValue)
            {
                builder.SetPosition(savedPosition.Value);
            }
            else
            {
                builder.Center();
            }

            roots = SearchIndex.BuildRoots();
            areaLabels = BuildAreaLabels();

            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            builder.AddInput(
                Loc.Get("search_placeholder", "输入名称或标识，空格分隔多个关键词", "Name or identifier, space-separated"),
                query,
                OnSubmit,
                "query",
                OnTyping);
            builder.Last.SetFixedWidth(400f);
            builder.AddButton(Loc.Get("refresh", "刷新", "Refresh"), delegate { Refresh(true); }, "refresh");
            builder.Last.SetFixedWidth(84f);
            builder.AddButton(Loc.Get("clear", "清空", "Clear"), ClearSearch, "clear");
            builder.Last.SetFixedWidth(84f);
            builder.AddButton(Loc.Get("classify_button", "归类", "Tidy"), ClassifyWindow.Open, "classify");
            builder.Last.SetFixedWidth(84f);
            builder.End();

            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            builder.AddDropdown(
                areaLabels,
                Mathf.Clamp(areaIndex, 0, Mathf.Max(0, areaLabels.Count - 1)),
                OnAreaChanged,
                "area");
            builder.Last.SetFixedWidth(170f);
            builder.AddDropdown(
                SortLabels(),
                Mathf.Clamp(sortIndex, 0, 3),
                OnSortChanged,
                "sort");
            builder.Last.SetFixedWidth(170f);
            builder.AddToggle(
                Loc.Get("only_contraband", "只看违禁 / 赃物", "Contraband only"),
                onlyContraband,
                OnContrabandChanged,
                "contraband");
            builder.AddFlexibleSpace();
            builder.End();

            builder.AddLabel(" ", "status");
            ConfigureStatusLabel(builder.Last);

            builder.BeginScrollFlexible(400f);
            resultsHost = builder.CurrentContainer;
            builder.End();

            builder.AddLabel(
                Loc.Get("hint", "点一条结果：打开对应容器并高亮那件物品；「归类」按类别把货挪到对应柜子。", "Click a result to open its container and highlight it; Tidy moves goods to their matching containers."),
                "hint");
            ConfigureHintLabel(builder.Last);

            builder.Show();
            Refresh(true);
        }

        private static void ConfigureStatusLabel(CustomUIElement element)
        {
            if (element == null)
            {
                return;
            }

            element.SetPreferredSize(660f, 22f);
            if (element.text != null)
            {
                element.text.enableAutoSizing = false;
                element.text.fontSize = 14f;
                element.text.enableWordWrapping = false;
                element.text.overflowMode = TextOverflowModes.Ellipsis;
                element.text.alignment = TextAlignmentOptions.Left;
            }
        }

        private static void ConfigureHintLabel(CustomUIElement element)
        {
            if (element == null)
            {
                return;
            }

            element.SetPreferredSize(660f, 40f);
            if (element.text != null)
            {
                element.text.enableAutoSizing = false;
                element.text.fontSize = 12f;
                element.text.color = new Color(0.62f, 0.62f, 0.6f);
                element.text.enableWordWrapping = true;
                element.text.overflowMode = TextOverflowModes.Truncate;
                element.text.alignment = TextAlignmentOptions.Left;
            }
        }

        private static void OnWindowClosed()
        {
            if (window != null && window.IsAlive && window.Rect != null)
            {
                savedPosition = window.Rect.anchoredPosition;
            }

            window = null;
            resultsHost = null;
            scroll = null;
            StopDebounce();
        }

        private static List<string> BuildAreaLabels()
        {
            var labels = new List<string>
            {
                Loc.Get("area_all", "全部", "All")
            };

            foreach (ContainerRoot root in roots)
            {
                if (root != null && !labels.Contains(root.Label))
                {
                    labels.Add(root.Label);
                }
            }

            return labels;
        }

        private static List<string> SortLabels()
        {
            return new List<string>
            {
                Loc.Get("sort_name", "名称", "Name"),
                Loc.Get("sort_value", "价值", "Value"),
                Loc.Get("sort_count", "数量", "Count"),
                Loc.Get("sort_location", "位置", "Location")
            };
        }

        private static void OnSubmit(string value)
        {
            query = value ?? "";
            Refresh(true);
        }

        private static void OnTyping(string value)
        {
            query = value ?? "";
            ScheduleRefresh();
        }

        private static void OnAreaChanged(int index)
        {
            areaIndex = index;
            Refresh(false);
        }

        private static void OnSortChanged(int index)
        {
            sortIndex = index;
            Refresh(false);
        }

        private static void OnContrabandChanged(bool value)
        {
            onlyContraband = value;
            Refresh(false);
        }

        private static void ClearSearch()
        {
            query = "";
            try
            {
                if (window != null && window.IsAlive)
                {
                    CustomUIElement input = window.Get("query");
                    if (input != null)
                    {
                        input.SetString("", false);
                    }
                }
            }
            catch
            {
                // 输入框没了不影响检索
            }

            Refresh(true);
        }

        private static void ScheduleRefresh()
        {
            if (!IsOpen)
            {
                return;
            }

            StopDebounce();
            try
            {
                debounce = ModHelper.StartCoroutine(modId, DebouncedRefresh());
            }
            catch (Exception e)
            {
                log?.Warning("启动搜索延迟失败：" + e.Message);
                Refresh(false);
            }
        }

        private static IEnumerator DebouncedRefresh()
        {
            yield return new WaitForSecondsRealtime(0.22f);
            debounce = null;
            Refresh(false);
        }

        private static void StopDebounce()
        {
            if (debounce == null)
            {
                return;
            }

            try
            {
                ModHelper.StopCoroutine(modId, debounce);
            }
            catch
            {
                // 协程宿主可能已经没了
            }

            debounce = null;
        }

        private static void Refresh(bool rescan)
        {
            if (!IsOpen || resultsHost == null)
            {
                return;
            }

            try
            {
                if (rescan)
                {
                    roots = SearchIndex.BuildRoots();
                    entries = SearchIndex.Scan(roots);
                    totalCount = entries.Count;
                }

                Filter();
                Render();

                if (EmporiumEntry.Instance == null)
                {
                    SetStatus(Loc.Get(
                        "no_store",
                        "当前不在当铺里：先载入一个存档，再回来检索。",
                        "Not in the store: load a save first."));
                }
            }
            catch (Exception e)
            {
                log?.Warning("刷新检索结果失败：" + e);
            }
        }

        private static void Filter()
        {
            string area = areaIndex > 0 && areaIndex < areaLabels.Count ? areaLabels[areaIndex] : null;
            string[] tokens = (query ?? "").ToLowerInvariant().Split(new[] { ' ', '\u3000' }, StringSplitOptions.RemoveEmptyEntries);

            var list = new List<SearchEntry>();
            foreach (SearchEntry entry in entries)
            {
                if (entry == null || entry.Item == null)
                {
                    continue;
                }

                if (area != null && (entry.Root == null || entry.Root.Label != area))
                {
                    continue;
                }

                if (onlyContraband && !entry.Contraband)
                {
                    continue;
                }

                bool matches = true;
                foreach (string token in tokens)
                {
                    if (entry.Haystack == null || entry.Haystack.IndexOf(token, StringComparison.Ordinal) < 0)
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    list.Add(entry);
                }
            }

            Sort(list);
            matchCount = list.Count;
            truncated = list.Count > MaxRows;
            if (truncated)
            {
                list.RemoveRange(MaxRows, list.Count - MaxRows);
            }

            shown = list;
        }

        private static void Sort(List<SearchEntry> list)
        {
            switch (sortIndex)
            {
                case 1:
                    list.Sort(delegate (SearchEntry a, SearchEntry b)
                    {
                        int result = b.Value.CompareTo(a.Value);
                        return result != 0 ? result : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    });
                    break;
                case 2:
                    list.Sort(delegate (SearchEntry a, SearchEntry b)
                    {
                        int result = b.Count.CompareTo(a.Count);
                        return result != 0 ? result : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    });
                    break;
                case 3:
                    list.Sort(delegate (SearchEntry a, SearchEntry b)
                    {
                        int result = string.Compare(a.LocationText, b.LocationText, StringComparison.OrdinalIgnoreCase);
                        return result != 0 ? result : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    });
                    break;
                default:
                    list.Sort(delegate (SearchEntry a, SearchEntry b)
                    {
                        int result = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                        return result != 0 ? result : string.Compare(a.LocationText, b.LocationText, StringComparison.OrdinalIgnoreCase);
                    });
                    break;
            }
        }

        private static void Render()
        {
            if (resultsHost == null || window == null || !window.IsAlive || CustomUIManager.Instance == null)
            {
                return;
            }

            CustomUIBuilder.ClearContainer(resultsHost);
            var builder = new CustomUIBuilder(CustomUIManager.Instance, window, resultsHost);

            if (shown.Count == 0)
            {
                builder.AddLabel(Loc.Get("empty", "没有匹配的物品。", "Nothing matches."));
            }
            else
            {
                foreach (SearchEntry entry in shown)
                {
                    BuildRow(builder, entry);
                }
            }

            CustomUIBuilder.RebuildLayout(resultsHost);

            scroll = resultsHost.GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                scroll.verticalNormalizedPosition = 1f;
            }

            UpdateStatus();
        }

        private static void BuildRow(CustomUIBuilder builder, SearchEntry entry)
        {
            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            RectTransform row = builder.CurrentContainer;

            LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 42f;
            layout.preferredHeight = 42f;
            layout.preferredWidth = 660f;

            // 透明底图只负责接鼠标，点行里哪儿都算点中这一行
            Image background = row.gameObject.AddComponent<Image>();
            background.color = new Color(0.55f, 0.72f, 1f, 0f);
            background.raycastTarget = true;

            Sprite icon = GetIcon(entry.Item);
            builder.AddImage(icon, 34f, 34f, null);
            if (builder.Last != null && builder.Last.image != null)
            {
                builder.Last.image.preserveAspect = true;
                builder.Last.image.raycastTarget = false;
                if (icon == null)
                {
                    // 没有图标就把 Graphic 关掉，别让预制体的占位图露出来
                    builder.Last.image.enabled = false;
                }
            }

            builder.BeginColumn(0f, TextAnchor.MiddleLeft);
            AddText(builder, entry.TitleText, 16f, true, new Color(0.93f, 0.92f, 0.86f), RowTextWidth);
            AddText(builder, Subtitle(entry), 12f, false, new Color(0.68f, 0.68f, 0.66f), RowTextWidth);
            builder.End();

            builder.End();

            RowClick click = row.gameObject.AddComponent<RowClick>();
            click.Init(background, delegate { Focus(entry); });

            try
            {
                CustomUITooltipTrigger tooltip = row.gameObject.AddComponent<CustomUITooltipTrigger>();
                tooltip.tooltipText = BuildTooltip(entry);
            }
            catch
            {
                // 提示框没有也能用
            }
        }

        private static void AddText(CustomUIBuilder builder, string text, float size, bool bold, Color color, float width)
        {
            builder.AddLabel(text, null);
            CustomUIElement element = builder.Last;
            if (element == null)
            {
                return;
            }

            TextMeshProUGUI label = element.text;
            if (label != null)
            {
                label.enableAutoSizing = false;
                label.fontSize = size;
                label.color = color;
                label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
                label.enableWordWrapping = false;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.alignment = TextAlignmentOptions.Left;
                label.raycastTarget = false;
            }

            element.SetPreferredSize(width, size + 6f);
        }

        private static string Subtitle(SearchEntry entry)
        {
            string text = entry.LocationText;
            if (entry.Value > 0)
            {
                text += "    " + Loc.Get("value_short", "估值 {0}", "value {0}", entry.Value.ToString("N0"));
            }

            return text;
        }

        private static string BuildTooltip(SearchEntry entry)
        {
            var text = new StringBuilder();
            text.Append(entry.Name);
            if (entry.Count > 1)
            {
                text.Append(" ×").Append(entry.Count);
            }

            text.Append('\n').Append(entry.LocationText);

            if (!string.IsNullOrEmpty(entry.Identifier))
            {
                text.Append('\n').Append(entry.Identifier);
            }

            try
            {
                if (!string.IsNullOrEmpty(entry.Item.shortDescription))
                {
                    text.Append('\n').Append(entry.Item.shortDescription);
                }
            }
            catch
            {
                // 说明文字取不到就算了
            }

            text.Append('\n').Append(Loc.Get("tooltip_click", "点击定位", "Click to locate"));
            return text.ToString();
        }

        private static Sprite GetIcon(GameItem item)
        {
            if (item == null)
            {
                return null;
            }

            string key;
            try
            {
                key = (item.spriteAtlasPath ?? "") + "|" + (item.spritePath ?? "");
            }
            catch
            {
                key = "?";
            }

            Sprite sprite;
            if (iconCache.TryGetValue(key, out sprite))
            {
                return sprite;
            }

            try
            {
                GameItemElement element = item as GameItemElement;
                sprite = element != null
                    ? element.ResolveSpriteByName(item.spritePath)
                    : RenderHandler.LoadFromAtlas(item.spriteAtlasPath, item.spritePath);
            }
            catch
            {
                sprite = null;
            }

            iconCache[key] = sprite;
            return sprite;
        }

        private static void UpdateStatus()
        {
            string status = Loc.Get(
                "status_count",
                "共 {0} 件 · 匹配 {1} 条",
                "{0} items · {1} matches",
                totalCount,
                matchCount);

            if (truncated)
            {
                status += "  " + Loc.Get(
                    "status_truncated",
                    "（只显示前 {0} 条，再输点关键词缩小范围）",
                    "(showing first {0}, narrow the search)",
                    MaxRows);
            }

            SetStatus(status);
        }

        private static void SetStatus(string text)
        {
            if (window == null || !window.IsAlive)
            {
                return;
            }

            try
            {
                CustomUIElement element = window.Get("status");
                if (element != null)
                {
                    element.SetText(text ?? "");
                }
            }
            catch
            {
                // 状态栏没了不影响主要功能
            }
        }

        /// <summary>点结果：确定物品所在窗口，必要时打开它，然后把格子闪一下。</summary>
        private static void Focus(SearchEntry entry)
        {
            if (entry == null || entry.Item == null)
            {
                return;
            }

            try
            {
                FocusCore(entry);
            }
            catch (Exception e)
            {
                log?.Warning("定位物品失败：" + e);
            }
        }

        private static void FocusCore(SearchEntry entry)
        {
            GameItem item = entry.Item;
            GameInventory inventory = item.parentInventory;
            if (inventory == null)
            {
                SetStatus(Loc.Get("item_gone", "那件物品已经不在原位了，刷新一下试试。", "That item moved. Try refreshing."));
                // 别在按钮回调里直接重建列表，等下一帧再刷
                try
                {
                    ModHelper.Delay(modId, 0f, delegate
                    {
                        if (IsOpen)
                        {
                            Refresh(true);
                        }
                    });
                }
                catch
                {
                    // 延迟排不上就等玩家自己点刷新
                }

                return;
            }

            PixelWindow target = entry.Root != null ? entry.Root.Window : null;
            if (entry.Root == null || inventory != entry.Root.Inventory)
            {
                GameItem container = SearchIndex.FindContainerWithWindow(inventory);
                if (container != null)
                {
                    PixelWindow containerWindow = container.contentWindow ?? container.examineWindow;
                    if (containerWindow != null)
                    {
                        target = containerWindow;
                    }
                }
            }

            if (target == null)
            {
                SetStatus(Loc.Get(
                    "no_window",
                    "东西在「{0}」里，但这个容器没有可打开的窗口。",
                    "It is inside \"{0}\", which has no window to open.",
                    entry.LocationText));
                return;
            }

            if (!EnsureVisible(target))
            {
                SetStatus(Loc.Get(
                    "outside",
                    "在店外没法定位店里的容器，回店里再试。",
                    "Cannot open store containers while you are away from the store."));
                return;
            }

            Highlight(inventory, item);

            SetStatus(Loc.Get(
                "located",
                "已定位：{0}（{1}）",
                "Located: {0} ({1})",
                entry.Name,
                entry.LocationText));

            if (CloseOnFocus)
            {
                Close();
            }
        }

        private static bool EnsureVisible(PixelWindow target)
        {
            if (target == null || target.handler == null)
            {
                return false;
            }

            if (target.IsVisible())
            {
                if (target.canMove)
                {
                    target.ToFront(false);
                }

                return true;
            }

            bool outside = false;
            try
            {
                outside = ModHelper.IsOutsideStore();
            }
            catch
            {
                outside = false;
            }

            // 离开店铺后柜台窗口全被 SetActive(false) 了，这时候再激活会把店里的面板带到别的场景
            if (outside)
            {
                return false;
            }

            if (target.canMove)
            {
                target.Show();
                target.Center();
            }
            else
            {
                // 柜台的窗口不跟着鼠标走，重新激活就行，别打乱原来的层级
                target.handler.gameObject.SetActive(true);
                target.Validate();
                WindowsHandler.TryAddWindow(target);
            }

            return true;
        }

        private static void Highlight(GameInventory inventory, GameItem item)
        {
            ClearHighlightNow();

            try
            {
                SlotMarker slot = inventory.GetIncompleteItemInventorySlot(item);
                if (slot == null)
                {
                    return;
                }

                inventory.HighlightSlot(slot, RenderHandler.BackgroundColor.Blue, true);
                highlightedInventory = inventory;
                clearHighlight = ModHelper.StartCoroutine(modId, ClearHighlightLater(inventory));
            }
            catch (Exception e)
            {
                log?.Warning("高亮物品失败：" + e.Message);
            }
        }

        private static IEnumerator ClearHighlightLater(GameInventory inventory)
        {
            yield return new WaitForSecondsRealtime(3.5f);
            clearHighlight = null;

            if (inventory == null)
            {
                yield break;
            }

            try
            {
                // 玩家正在拖东西的话，高亮是游戏在用，别去清
                if (UIDragHandler.isDraggingAnyUI)
                {
                    yield break;
                }

                inventory.ClearHighlight();
            }
            catch
            {
                // 容器可能已经不在了
            }

            if (highlightedInventory == inventory)
            {
                highlightedInventory = null;
            }
        }

        private static void ClearHighlightNow()
        {
            if (clearHighlight != null)
            {
                try
                {
                    ModHelper.StopCoroutine(modId, clearHighlight);
                }
                catch
                {
                    // 忽略
                }

                clearHighlight = null;
            }

            if (highlightedInventory != null)
            {
                try
                {
                    highlightedInventory.ClearHighlight();
                }
                catch
                {
                    // 忽略
                }

                highlightedInventory = null;
            }
        }
    }
}
