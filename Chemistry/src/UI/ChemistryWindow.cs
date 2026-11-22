using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chemistry
{
    /// <summary>
    /// 实验台窗口：周期表 / 合成台 / 图鉴三个标签页的宿主，负责窗口生命周期。
    ///
    /// 下面的公开方法由模组入口（快捷键）和 F9 调试面板调用，名字和签名必须保留：
    /// Initialize(string modId, ModLog log)、IsOpen、Toggle()、Open()、Close()、Shutdown()。
    ///
    /// 容错约定：主菜单（没有 CustomUIManager）、数据表为空、贴图缺失、重复开关窗口
    /// 都只降级或打日志，不允许抛异常。
    /// </summary>
    internal static class ChemistryWindow
    {
        private const string WindowId = "chemistry.bench";
        private const float WindowWidth = 940f;
        private const float WindowHeight = 760f;

        private static string modId = ChemistryCatalog.ModId;
        private static ModLog log;
        private static CustomUIWindow window;
        private static Vector2? savedPosition;

        public static void Initialize(string id, ModLog logger)
        {
            if (!string.IsNullOrEmpty(id))
            {
                modId = id;
            }

            log = logger;
            Loc.ModId = modId;
            ChemUi.ModId = modId;
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
                log?.Warning("当前场景没有 CustomUIManager（比如主菜单），开不了实验台窗口");
                return;
            }

            // 窗口还在（只是被藏起来）就重新显示，不重建
            if (window != null && window.IsAlive)
            {
                try
                {
                    window.Show();
                }
                catch (Exception e)
                {
                    log?.Warning("重新显示实验台窗口失败：" + e.Message);
                }

                RefreshAll();
                return;
            }

            try
            {
                Build();
            }
            catch (Exception e)
            {
                log?.Error("创建实验台窗口失败：" + e);
                // 建到一半失败时把半成品关掉，免得下次 Toggle 以为窗口还在
                SafeClose();
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

            try
            {
                if (current != null && current.IsAlive)
                {
                    current.Close();
                }
            }
            catch (Exception e)
            {
                log?.Warning("关闭实验台窗口失败：" + e.Message);
            }
        }

        /// <summary>模组被禁用/卸载时用：关窗口并丢掉库存快照。</summary>
        public static void Shutdown()
        {
            CompendiumPanel.CloseReactionBook();
            Close();
            ChemUi.InvalidateCounts();
        }

        private static void Build()
        {
            CustomUIBuilder builder = CustomUIManager.Window(
                WindowId,
                Loc.Get("window_title", "化学实验台", "Chemistry Bench"));
            if (builder == null)
            {
                log?.Warning("CustomUIManager 没能创建实验台窗口");
                return;
            }

            window = builder.Handle;
            if (window == null)
            {
                log?.Warning("实验台窗口创建后句柄为空");
                return;
            }

            builder
                .SetSize(WindowWidth, WindowHeight)
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

            builder.AddLabel(SummaryLine(), "chem_summary");
            ChemUi.Style(builder.Last, 12f, 820f, 20f, false, ChemUi.Dim, false, TextAlignmentOptions.Left);

            builder.BeginTabs(4f);
            SetupTabsLayout(builder);

            builder.BeginTab(Loc.Get("tab_periodic", "周期表", "Periodic table"));
            MakePageFlexible(builder);
            PeriodicTablePanel.BuildPage(builder, window);
            builder.End();

            builder.BeginTab(Loc.Get("tab_synthesis", "合成台", "Synthesis"));
            MakePageFlexible(builder);
            SynthesisPanel.BuildPage(builder, window);
            builder.End();

            builder.BeginTab(Loc.Get("tab_compendium", "图鉴", "Compendium"));
            MakePageFlexible(builder);
            CompendiumPanel.BuildPage(builder, window);
            builder.End();

            builder.EndTabs();

            builder.Show();

            // 列表必须在窗口显示、布局跑过一遍之后再填，滚动区域才有正确尺寸
            try
            {
                SynthesisPanel.Refresh(false);
            }
            catch (Exception e)
            {
                log?.Warning("填充合成台失败：" + e.Message);
            }

            try
            {
                CompendiumPanel.Refresh(false);
            }
            catch (Exception e)
            {
                log?.Warning("填充图鉴失败：" + e.Message);
            }
        }

        private static void RefreshAll()
        {
            try
            {
                PeriodicTablePanel.Refresh();
            }
            catch (Exception e)
            {
                log?.Warning("刷新周期表失败：" + e.Message);
            }

            try
            {
                SynthesisPanel.Refresh(false);
            }
            catch (Exception e)
            {
                log?.Warning("刷新合成台失败：" + e.Message);
            }

            try
            {
                CompendiumPanel.Refresh(false);
            }
            catch (Exception e)
            {
                log?.Warning("刷新图鉴失败：" + e.Message);
            }
        }

        private static void OnWindowClosed()
        {
            CustomUIWindow current = window;
            if (current != null && current.IsAlive && current.Rect != null)
            {
                savedPosition = current.Rect.anchoredPosition;
            }

            window = null;
        }

        /// <summary>
        /// 让标签页容器撑满窗口宽度。BeginTabs 建出来的 tab_pages 默认只按内容宽度排，
        /// 这里给它和页面根节点补 flexibleWidth，窗口拉宽时内容不会挤在左边。
        /// 节点名来自游戏的 CustomUIBuilder.BeginTabs（tab_pages），取不到就保持默认，不影响功能。
        /// </summary>
        private static void SetupTabsLayout(CustomUIBuilder builder)
        {
            if (builder == null)
            {
                return;
            }

            RectTransform tabsRoot = builder.CurrentContainer;
            if (tabsRoot == null)
            {
                return;
            }

            try
            {
                LayoutElement layout = tabsRoot.gameObject.GetComponent<LayoutElement>();
                if (layout == null)
                {
                    layout = tabsRoot.gameObject.AddComponent<LayoutElement>();
                }

                layout.flexibleWidth = 1f;

                Transform pages = tabsRoot.Find("tab_pages");
                RectTransform pagesRect = pages as RectTransform;
                if (pagesRect != null)
                {
                    LayoutElement pagesLayout = pagesRect.gameObject.GetComponent<LayoutElement>();
                    if (pagesLayout == null)
                    {
                        pagesLayout = pagesRect.gameObject.AddComponent<LayoutElement>();
                    }

                    pagesLayout.flexibleWidth = 1f;
                }
            }
            catch (Exception e)
            {
                log?.Warning("设置标签页宽度失败：" + e.Message);
            }
        }

        /// <summary>让单个标签页页根节点跟着容器一起变宽。</summary>
        private static void MakePageFlexible(CustomUIBuilder builder)
        {
            if (builder == null)
            {
                return;
            }

            RectTransform page = builder.CurrentContainer;
            if (page == null)
            {
                return;
            }

            try
            {
                LayoutElement layout = page.gameObject.GetComponent<LayoutElement>();
                if (layout == null)
                {
                    layout = page.gameObject.AddComponent<LayoutElement>();
                }

                layout.flexibleWidth = 1f;
            }
            catch (Exception e)
            {
                log?.Warning("设置标签页宽度失败：" + e.Message);
            }
        }

        /// <summary>构建失败时的兜底关闭：不回调、不抛异常。</summary>
        private static void SafeClose()
        {
            CustomUIWindow current = window;
            window = null;
            try
            {
                if (current != null && current.IsAlive)
                {
                    current.Close();
                }
            }
            catch
            {
                // 半成品窗口就算了
            }
        }

        private static string SummaryLine()
        {
            int elements = Count(ChemistryCatalog.Elements);
            int tools = Count(ChemistryCatalog.Tools);
            int compounds = Count(ChemistryCatalog.Compounds);

            return Loc.Get(
                "window_summary",
                "元素 {0} · 工具 {1} · 化合物 {2}　　按快捷键或 F9 调试面板开关本窗口，Esc 关闭，拖标题栏移动",
                "Elements {0} · Tools {1} · Compounds {2}    Toggle from the hotkey or F9 debug panel; Esc closes, drag the title bar to move",
                elements,
                tools,
                compounds);
        }

        private static int Count(Array array)
        {
            return array == null ? 0 : array.Length;
        }
    }
}
