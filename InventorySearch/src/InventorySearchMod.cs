using System;
using UnityEngine;

namespace InventorySearch
{
    /// <summary>模组设置里能选的快捷键。故意只列几个不常用的键，免得和游戏原版按键打架。</summary>
    public enum HotkeyKey
    {
        F3,
        F4,
        F6,
        F7,
        F10,
        None
    }

    public class InventorySearchMod : IMod
    {
        private ModManifest manifest;
        private ModLog log;
        private GameObject host;
        private ConfigEntry<HotkeyKey> hotkey;
        private ConfigEntry<bool> closeOnFocus;
        private HotkeyKey currentKey = HotkeyKey.F3;

        public void Init(ModManifest manifest)
        {
            this.manifest = manifest;
            log = new ModLog(manifest);
            Loc.ModId = manifest.ID;
            Loc.ResetLocale();
            SearchWindow.Initialize(manifest.ID, log);
            ClassifyWindow.Initialize(log);
            log.Log("装好了，游戏版本 " + Application.version);

            try
            {
                ModConfig config = ModHelper.GetConfig(manifest.ID);
                hotkey = config.DefineEnum(
                    "hotkey",
                    HotkeyKey.F3,
                    "按键 / Hotkey",
                    () => Loc.Get("config_hotkey", "打开仓库检索的快捷键", "Inventory search hotkey"),
                    () => Loc.Get(
                        "config_hotkey_tip",
                        "默认 F3。F3 不占用游戏原版按键；改成「None」就只用 F9 调试面板里的入口。",
                        "F3 by default. Choose \"None\" to only use the F9 debug panel entry."));

                if (hotkey != null)
                {
                    hotkey.OnChanged += OnHotkeyChanged;
                    currentKey = hotkey.Value;
                }

                closeOnFocus = config.Define(
                    "close_on_focus",
                    false,
                    "行为 / Behavior",
                    () => Loc.Get("config_close_on_focus", "定位后收起检索窗口", "Close the window after locating"),
                    () => Loc.Get(
                        "config_close_on_focus_tip",
                        "开启后点结果会立刻关掉检索窗口，免得挡住刚打开的高亮位置；想连续找东西就关掉。",
                        "When enabled the search window closes after a result is clicked, so it does not cover the highlighted item."));

                if (closeOnFocus != null)
                {
                    closeOnFocus.OnChanged += OnCloseOnFocusChanged;
                    SearchWindow.CloseOnFocus = closeOnFocus.Value;
                }
            }
            catch (Exception e)
            {
                log.Warning("注册模组设置失败：" + e.Message);
            }
        }

        public void OnEnable()
        {
            try
            {
                host = new GameObject("InventorySearchHost");
                UnityEngine.Object.DontDestroyOnLoad(host);
                HotkeyWatcher watcher = host.AddComponent<HotkeyWatcher>();
                watcher.Bind(GetHotkey, SearchWindow.Toggle);
            }
            catch (Exception e)
            {
                log.Error("创建快捷键宿主失败：" + e);
            }

            try
            {
                ModHelper.AddDebugFunction(
                    manifest.ID,
                    "Modding",
                    Loc.Get("debug_open", "仓库检索", "Inventory search"),
                    SearchWindow.Toggle);
                ModHelper.AddDebugFunction(
                    manifest.ID,
                    "Modding",
                    Loc.Get("debug_scan", "仓库检索：扫描统计", "Inventory search: scan stats"),
                    LogScan);
                ModHelper.AddDebugFunction(
                    manifest.ID,
                    "Modding",
                    Loc.Get("classify_debug", "归类整理", "Tidy by category"),
                    ClassifyWindow.Open);
            }
            catch (Exception e)
            {
                log.Warning("注册 F9 入口失败：" + e.Message);
            }

            // 读档/新档之后，上一局记住的物品引用不能用来撤销了
            try
            {
                ModHook.OnGameLoadedInit += ClassifyService.ClearUndo;
                ModHook.OnNewGameStarted += OnNewGameStarted;
            }
            catch (Exception e)
            {
                log.Warning("挂载读档清理失败：" + e.Message);
            }
        }

        public void OnDisable()
        {
            try
            {
                SearchWindow.Shutdown();
                ClassifyWindow.Close();
            }
            catch
            {
                // 关游戏时窗口可能已经没了
            }

            try
            {
                ModHook.OnGameLoadedInit -= ClassifyService.ClearUndo;
                ModHook.OnNewGameStarted -= OnNewGameStarted;
            }
            catch
            {
                // 忽略
            }

            if (hotkey != null)
            {
                hotkey.OnChanged -= OnHotkeyChanged;
            }

            if (closeOnFocus != null)
            {
                closeOnFocus.OnChanged -= OnCloseOnFocusChanged;
            }

            try
            {
                ModHelper.RemoveDebugFunctions(manifest.ID);
            }
            catch
            {
                // 调试面板可能已经销毁
            }

            if (host != null)
            {
                UnityEngine.Object.Destroy(host);
            }

            host = null;
        }

        private void OnHotkeyChanged(HotkeyKey value)
        {
            currentKey = value;
        }

        private void OnCloseOnFocusChanged(bool value)
        {
            SearchWindow.CloseOnFocus = value;
        }

        private void OnNewGameStarted(string scenarioId)
        {
            ClassifyService.ClearUndo();
        }

        /// <summary>F9 里的诊断入口：不开窗口也能看每个容器扫到多少件，排查用。</summary>
        private void LogScan()
        {
            try
            {
                var roots = SearchIndex.BuildRoots();
                var entries = SearchIndex.Scan(roots);
                log.Log("扫描完成：" + roots.Count + " 个容器，" + entries.Count + " 件物品");
                foreach (var root in roots)
                {
                    int count = entries.FindAll(e => e.Root == root).Count;
                    log.Log("  " + root.Label + "：" + count);
                }
            }
            catch (Exception e)
            {
                log.Error("扫描统计失败：" + e);
            }
        }

        private KeyCode GetHotkey()
        {
            switch (currentKey)
            {
                case HotkeyKey.F3: return KeyCode.F3;
                case HotkeyKey.F4: return KeyCode.F4;
                case HotkeyKey.F6: return KeyCode.F6;
                case HotkeyKey.F7: return KeyCode.F7;
                case HotkeyKey.F10: return KeyCode.F10;
                default: return KeyCode.None;
            }
        }
    }
}
