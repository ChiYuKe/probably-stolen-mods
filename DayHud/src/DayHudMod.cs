using System;
using UnityEngine;

namespace DayHud
{
    /// <summary>
    /// 左上角圆形天数面板。挂在一个常驻的 Canvas 上，自己管创建和销毁。
    /// </summary>
    public class DayHudMod : IMod
    {
        private ModManifest manifest;
        private ModLog log;
        private ConfigEntry<bool> hideCalendarEntry;
        private ConfigEntry<bool> hideCreditEntry;
        private ConfigEntry<bool> hideOutsideEntry;
        private ConfigEntry<bool> calendarSkinEntry;
        private ConfigEntry<bool> calendarTrimEntry;
        private GameObject hudObject;
        private DayHudPanel panel;

        public void Init(ModManifest manifest)
        {
            this.manifest = manifest;
            log = new ModLog(manifest);
            log.Log("装好了，游戏版本 " + Application.version);

            try
            {
                ModConfig config = ModHelper.GetConfig(manifest.ID);
                hideCalendarEntry = config.Define(
                    "hide_wall_calendar",
                    true,
                    "HUD",
                    () => ConfigText("config_hide_calendar", "隐藏墙上的日历便签"),
                    () => ConfigText("config_hide_calendar_tip",
                        "开启后墙上的纸质日历会收起来，天数和租金只在左上角表盘显示。"));
                hideCreditEntry = config.Define(
                    "hide_credit_display",
                    true,
                    "HUD",
                    () => ConfigText("config_hide_credit", "隐藏墙上的信用点屏幕"),
                    () => ConfigText("config_hide_credit_tip",
                        "开启后墙上的信用点屏幕会收起来，余额显示在表盘下方的货币条上。"));
                hideOutsideEntry = config.Define(
                    "hide_outside_store",
                    true,
                    "HUD",
                    () => ConfigText("config_hide_outside", "在其他场景隐藏 HUD"),
                    () => ConfigText("config_hide_outside_tip",
                        "开启后离开当铺去淘荒、地图等场景时自动收起 HUD，回到店里再显示。"));
                calendarSkinEntry = config.Define(
                    "calendar_retro_panel",
                    true,
                    "HUD",
                    () => ConfigText("config_calendar_skin", "日历窗口换复古面板"),
                    () => ConfigText("config_calendar_skin_tip",
                        "开启后打开日历时，日历窗口的背景会换成模组自带的复古工业面板。"));
                calendarTrimEntry = config.Define(
                    "calendar_trim_edges",
                    true,
                    "HUD",
                    () => ConfigText("config_calendar_trim", "裁掉日历底图的透明边"),
                    () => ConfigText("config_calendar_trim_tip",
                        "开启后会把背景图四周的透明外边裁掉再铺满窗口；关掉则整张图直接拉伸铺满。"));
            }
            catch (Exception e)
            {
                log.Warning("注册配置项失败：" + e.Message);
            }
        }

        public void OnEnable()
        {
            try
            {
                EnsureHud();
            }
            catch (Exception e)
            {
                log.Error("建天数面板的时候崩了：" + e);
            }

            if (hideCalendarEntry != null)
            {
                hideCalendarEntry.OnChanged += OnHideCalendarChanged;

                if (panel != null)
                {
                    panel.SetHideWallCalendar(hideCalendarEntry.Value);
                }
            }

            if (hideCreditEntry != null)
            {
                hideCreditEntry.OnChanged += OnHideCreditChanged;

                if (panel != null)
                {
                    panel.SetHideCreditDisplay(hideCreditEntry.Value);
                }
            }

            if (hideOutsideEntry != null)
            {
                hideOutsideEntry.OnChanged += OnHideOutsideChanged;

                if (panel != null)
                {
                    panel.SetHideOutsideStore(hideOutsideEntry.Value);
                }
            }

            if (calendarSkinEntry != null)
            {
                calendarSkinEntry.OnChanged += OnCalendarSkinChanged;

                if (panel != null)
                {
                    panel.SetCalendarSkin(calendarSkinEntry.Value);
                }
            }

            if (calendarTrimEntry != null)
            {
                calendarTrimEntry.OnChanged += OnCalendarTrimChanged;

                if (panel != null)
                {
                    panel.SetCalendarTrimEdges(calendarTrimEntry.Value);
                }
            }

            // F9 里能开关，截图或者嫌挡视野的时候用
            try
            {
                ModHelper.AddDebugFunction(manifest.ID, "Modding", "当铺 HUD 开/关", ToggleHud);
                ModHelper.AddDebugFunction(manifest.ID, "Modding", "当铺 HUD 推送测试通知", PushTestNotification);
                ModHelper.AddDebugFunction(manifest.ID, "Modding", "当铺 HUD 推送永久通知", PushPersistentTestNotification);
            }
            catch (Exception e)
            {
                log.Warning("注册 F9 开关失败：" + e.Message);
            }
        }

        public void OnDisable()
        {
            DayHudApi.Detach(panel);

            if (hideCalendarEntry != null)
            {
                hideCalendarEntry.OnChanged -= OnHideCalendarChanged;
            }

            if (hideCreditEntry != null)
            {
                hideCreditEntry.OnChanged -= OnHideCreditChanged;
            }

            if (hideOutsideEntry != null)
            {
                hideOutsideEntry.OnChanged -= OnHideOutsideChanged;
            }

            if (calendarSkinEntry != null)
            {
                calendarSkinEntry.OnChanged -= OnCalendarSkinChanged;
            }

            if (calendarTrimEntry != null)
            {
                calendarTrimEntry.OnChanged -= OnCalendarTrimChanged;
            }

            try
            {
                ModHelper.RemoveDebugFunctions(manifest.ID);
            }
            catch
            {
                // 关游戏时调试面板可能已经销毁了，忽略
            }

            if (hudObject != null)
            {
                UnityEngine.Object.Destroy(hudObject);
            }

            hudObject = null;
            panel = null;
        }

        private void EnsureHud()
        {
            if (hudObject != null)
            {
                if (panel != null)
                {
                    return;
                }

                UnityEngine.Object.Destroy(hudObject);
                hudObject = null;
            }

            // 常驻对象：换场景不丢，进主菜单时面板自己隐藏
            hudObject = new GameObject("DayHudCanvas", typeof(RectTransform), typeof(Canvas));
            UnityEngine.Object.DontDestroyOnLoad(hudObject);

            panel = hudObject.AddComponent<DayHudPanel>();
            panel.Build(manifest.ID, log);
            DayHudApi.Attach(panel);

            if (hideCalendarEntry != null)
            {
                panel.SetHideWallCalendar(hideCalendarEntry.Value);
            }

            if (hideCreditEntry != null)
            {
                panel.SetHideCreditDisplay(hideCreditEntry.Value);
            }

            if (hideOutsideEntry != null)
            {
                panel.SetHideOutsideStore(hideOutsideEntry.Value);
            }

            if (calendarSkinEntry != null)
            {
                panel.SetCalendarSkin(calendarSkinEntry.Value);
            }

            if (calendarTrimEntry != null)
            {
                panel.SetCalendarTrimEdges(calendarTrimEntry.Value);
            }
        }

        private void OnHideCalendarChanged(bool value)
        {
            if (panel != null)
            {
                panel.SetHideWallCalendar(value);
            }
        }

        private void OnHideCreditChanged(bool value)
        {
            if (panel != null)
            {
                panel.SetHideCreditDisplay(value);
            }
        }

        private void OnHideOutsideChanged(bool value)
        {
            if (panel != null)
            {
                panel.SetHideOutsideStore(value);
            }
        }

        private void OnCalendarSkinChanged(bool value)
        {
            if (panel != null)
            {
                panel.SetCalendarSkin(value);
            }
        }

        private void OnCalendarTrimChanged(bool value)
        {
            if (panel != null)
            {
                panel.SetCalendarTrimEdges(value);
            }
        }

        private void PushTestNotification()
        {
            try
            {
                if (panel == null)
                {
                    EnsureHud();
                }

                if (panel != null)
                {
                    panel.Notify(ConfigText("notify_test", "通知测试"),
                        DayHudPanel.NoticeTone.Info, 5f);
                }
            }
            catch (Exception e)
            {
                log.Warning("推送测试通知失败：" + e.Message);
            }
        }

        private void PushPersistentTestNotification()
        {
            try
            {
                if (panel == null)
                {
                    EnsureHud();
                }

                if (panel != null)
                {
                    panel.NotifyPersistent(ConfigText("notify_test_persistent", "永久通知测试"),
                        DayHudPanel.NoticeTone.Warning, null, true);
                }
            }
            catch (Exception e)
            {
                log.Warning("推送永久通知失败：" + e.Message);
            }
        }

        private string ConfigText(string key, string fallback)
        {
            string text = ModHelper.GetLocalized(manifest.ID, key);
            return string.IsNullOrEmpty(text) || text == key ? fallback : text;
        }

        private void ToggleHud()
        {
            try
            {
                EnsureHud();
                if (panel != null)
                {
                    panel.ToggleUserHidden();
                }
            }
            catch (Exception e)
            {
                log.Error("开关天数面板的时候崩了：" + e);
            }
        }
    }
}


