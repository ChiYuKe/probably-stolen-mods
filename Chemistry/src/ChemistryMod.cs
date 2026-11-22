using System;
using UnityEngine;

namespace Chemistry
{
    /// <summary>模组设置里能选的快捷键。故意只列几个不常用的键，免得和游戏原版按键打架。</summary>
    public enum ChemHotkey
    {
        F5,
        F6,
        F7,
        F10,
        None
    }

    /// <summary>
    /// 化学实验台：入口。负责挂游戏事件、注册物品、注册设置和 F9 调试入口。
    /// 真正的物品数据在 src/Data，合成判定在 SynthesisService，界面在 ChemistryWindow。
    /// </summary>
    public class ChemistryMod : IMod
    {
        private ModManifest manifest;
        private ModLog log;

        private GameObject host;
        private ConfigEntry<ChemHotkey> hotkey;
        private ConfigEntry<bool> starterKitOption;
        private ConfigEntry<int> labStorageBonus;
        private ConfigEntry<int> clockSpeed;
        private ConfigEntry<bool> autoShutter;
        private GameClock gameClock;

        private ChemHotkey currentKey = ChemHotkey.F6;
        private bool giveStarterKit = true;

        public static ChemistryMod Instance { get; private set; }

        public static ModLog Log
        {
            get { return Instance != null ? Instance.log : null; }
        }

        public void Init(ModManifest manifest)
        {
            Instance = this;
            this.manifest = manifest;
            log = new ModLog(manifest);
            Loc.ModId = manifest.ID;

            ChemistryWindow.Initialize(manifest.ID, log);

            log.Log("化学实验台装好了，游戏版本 " + Application.version);
            log.Log("数据规模：元素 " + ChemistryCatalog.Elements.Length
                + "，工具 " + ChemistryCatalog.Tools.Length
                + "，化合物 " + ChemistryCatalog.Compounds.Length);

            try
            {
                ModConfig config = ModHelper.GetConfig(manifest.ID);
                clockSpeed = config.Define("real_minutes_per_day", 24, "游戏时间 / Game time",
                    () => Loc.Pick("一天对应的现实分钟", "Real minutes per game day"), min: 6, max: 120,
                    tooltip: () => Loc.Pick("默认现实 1 秒对应游戏 1 分钟。", "By default one real second advances one game minute."));
                autoShutter = config.Define("clock_auto_shutter", false, "游戏时间 / Game time",
                    () => Loc.Pick("到点自动开关店", "Schedule shop shutters"),
                    () => Loc.Pick("08:00 自动开店。20:00 后等顾客离开再自动打烊。睡觉仍由你决定。",
                        "Open at 08:00. After 20:00 close when the last customer leaves. Sleep is manual."));

                hotkey = config.DefineEnum(
                    "hotkey",
                    ChemHotkey.F6,
                    "按键 / Hotkey",
                    () => Loc.Get("config_hotkey", "打开化学实验台的快捷键", "Chemistry bench hotkey"),
                    () => Loc.Get(
                        "config_hotkey_tip",
                        "默认 F6。也可在 F9 → 特殊 → 化学调试面板里打开实验台。",
                        "F6 by default. You can also open the bench from the Chemistry debug panel via F9."));

                if (hotkey != null)
                {
                    hotkey.OnChanged += OnHotkeyChanged;
                    currentKey = hotkey.Value;
                }

                starterKitOption = config.Define(
                    "give_starter_kit",
                    true,
                    "行为 / Behavior",
                    () => Loc.Get("config_starter_kit", "开局送一套基础试剂和器皿", "Give a starter kit on new games"),
                    () => Loc.Get(
                        "config_starter_kit_tip",
                        "开启后，每次开新档第一次读档时往仓库放几瓶常用元素和基础化学工具，方便直接试合成。",
                        "When enabled, the first load of a new game puts common elements and basic glassware into storage."));

                if (starterKitOption != null)
                {
                    starterKitOption.OnChanged += OnStarterKitChanged;
                    giveStarterKit = starterKitOption.Value;
                }

                labStorageBonus = config.Define(
                    "lab_storage_bonus",
                    300,
                    "地下实验室 / Underground lab",
                    () => Loc.Get("config_lab_storage_bonus", "地下室额外储物格", "Extra underground lab storage slots"),
                    tooltip: () => Loc.Get(
                        "config_lab_storage_bonus_tip",
                        "在基础 344 格上增加的容量。默认增加 300 格，总容量 644 格。",
                        "Slots added to the base capacity of 344. Adds 300 by default, for 644 total."));

                if (labStorageBonus != null)
                {
                    labStorageBonus.OnChanged += OnLabStorageBonusChanged;
                    LabStorage.ConfigureCapacityBonus(labStorageBonus.Value);
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
                host = new GameObject("ChemistryHost");
                UnityEngine.Object.DontDestroyOnLoad(host);
                HotkeyWatcher watcher = host.AddComponent<HotkeyWatcher>();
                watcher.Bind(GetHotkey, ChemistryWindow.Toggle);
                host.AddComponent<LabEntranceButton>();
                gameClock = host.AddComponent<GameClock>();
                gameClock.Initialize(manifest.ID, log);
                gameClock.RealMinutesPerDay = clockSpeed?.Value ?? 24;
                gameClock.AutoShutter = autoShutter?.Value ?? false;
                if (clockSpeed != null) clockSpeed.OnChanged += OnClockSpeedChanged;
                if (autoShutter != null) autoShutter.OnChanged += OnAutoShutterChanged;
            }
            catch (Exception e)
            {
                log.Error("创建快捷键宿主失败：" + e);
            }

            try
            {
                ModHook.OnModItemDirectoryInit += OnModItemDirectoryInit;
                ModHook.OnLootTablesLoaded += OnLootTablesLoaded;
                ModHook.OnGameLoadedInit += OnGameLoadedInit;
                ModHook.OnGameLoadedLate += OnGameLoadedLate;
                ModHook.OnNewGameStarted += OnNewGameStarted;
                ModHook.OnCreateTooltipLate += ReagentBottle.AddAmountTooltip;
                ModHook.OnCreateTooltipLate += LabWorkflow.AddTooltip;
            }
            catch (Exception e)
            {
                log.Error("挂游戏事件失败：" + e);
            }

            // 地下实验室：注册可前往地点 + 背景故事线
            try
            {
                LabLocation.Init(manifest.ID, log);
                StoryService.Init();
            }
            catch (Exception e)
            {
                log.Error("初始化地下实验室失败：" + e);
            }

            RegisterDebugFunctions();
        }

        public void OnDisable()
        {
            if (clockSpeed != null) clockSpeed.OnChanged -= OnClockSpeedChanged;
            if (autoShutter != null) autoShutter.OnChanged -= OnAutoShutterChanged;
            if (gameClock != null) gameClock.enabled = false;
            gameClock = null;
            try
            {
                ModHook.OnModItemDirectoryInit -= OnModItemDirectoryInit;
                ModHook.OnLootTablesLoaded -= OnLootTablesLoaded;
                ModHook.OnGameLoadedInit -= OnGameLoadedInit;
                ModHook.OnGameLoadedLate -= OnGameLoadedLate;
                ModHook.OnNewGameStarted -= OnNewGameStarted;
                ModHook.OnCreateTooltipLate -= ReagentBottle.AddAmountTooltip;
                ModHook.OnCreateTooltipLate -= LabWorkflow.AddTooltip;
            }
            catch
            {
                // 忽略
            }

            try
            {
                LootIntegration.Remove();
            }
            catch
            {
                // 忽略
            }

            try
            {
                SampleReportWindow.CloseAll();
            }
            catch
            {
                // 忽略
            }

            try
            {
                ChemistryDebugWindow.Close();
                ChemistryWindow.Shutdown();
            }
            catch
            {
                // 关游戏时窗口可能已经没了
            }

            try
            {
                StoryService.Shutdown();
                LabLocation.Shutdown();
            }
            catch
            {
                // 忽略
            }

            if (hotkey != null)
            {
                hotkey.OnChanged -= OnHotkeyChanged;
            }

            if (starterKitOption != null)
            {
                starterKitOption.OnChanged -= OnStarterKitChanged;
            }

            if (labStorageBonus != null)
            {
                labStorageBonus.OnChanged -= OnLabStorageBonusChanged;
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
            Instance = null;
        }

        // ---------------------------------------------------------------
        // 游戏事件
        // ---------------------------------------------------------------

        private void OnModItemDirectoryInit(ModItemDirectory dir)
        {
            try
            {
                ItemRegistration.Register(dir);
            }
            catch (Exception e)
            {
                log.Error("注册物品失败：" + e);
            }
        }

        private void OnLootTablesLoaded()
        {
            LootIntegration.Apply();
        }

        private void OnGameLoadedInit()
        {
            gameClock?.Reset();
            ChemistryDebugWindow.Close();
            LabInventoryGrid.Close(false);
            LabEquipmentPanel.ResetTestTiers();
            LabPanel.Shutdown();
            Loc.ResetLocale();
        }

        private void OnClockSpeedChanged(int minutes)
        {
            if (gameClock != null) gameClock.RealMinutesPerDay = Mathf.Clamp(minutes, 6, 120);
        }

        private void OnAutoShutterChanged(bool enabled)
        {
            if (gameClock != null) gameClock.AutoShutter = enabled;
        }

        private void OnGameLoadedLate()
        {
            // 掉落表可能已经在上一步就绪；这里再兜一次，防止事件顺序意外
            LootIntegration.Apply();

            // 实验室储物格 / 故事线 / 背景舞台
            try
            {
                LabStorage.EnsureLoaded();
                StoryService.EnsureLoaded();
                LabLocation.EnsureBackdrop();
                SampleInteraction.AttachExistingTools();
                ItemRegistration.RestoreSavedFootprints();
            }
            catch (Exception e)
            {
                log.Warning("初始化实验室状态失败：" + e.Message);
            }

            if (giveStarterKit)
            {
                DebugKit.GiveStarterKitOnce();
            }

            // 读档后打一行盘点，排查「补给/产物有没有真的进仓库」
            DebugKit.LogInventorySummary();
        }

        private void OnNewGameStarted(string scenarioId)
        {
            Loc.ResetLocale();

            try
            {
                LabStorage.EnsureLoaded();
                StoryService.EnsureLoaded();
            }
            catch (Exception e)
            {
                log.Warning("新游戏初始化实验室状态失败：" + e.Message);
            }

            if (giveStarterKit)
            {
                DebugKit.GiveStarterKitOnce();
            }
        }

        // ---------------------------------------------------------------
        // 设置
        // ---------------------------------------------------------------

        private void OnHotkeyChanged(ChemHotkey value)
        {
            currentKey = value;
        }

        private void OnStarterKitChanged(bool value)
        {
            giveStarterKit = value;
        }

        private void OnLabStorageBonusChanged(int value)
        {
            LabStorage.ConfigureCapacityBonus(value);
        }

        private KeyCode GetHotkey()
        {
            switch (currentKey)
            {
                case ChemHotkey.F5: return KeyCode.F5;
                case ChemHotkey.F6: return KeyCode.F6;
                case ChemHotkey.F7: return KeyCode.F7;
                case ChemHotkey.F10: return KeyCode.F10;
                default: return KeyCode.None;
            }
        }

        // ---------------------------------------------------------------
        // F9 调试面板
        // ---------------------------------------------------------------

        private void RegisterDebugFunctions()
        {
            try
            {
                string label = Loc.Get("debug_window_open", "化学调试面板", "Chemistry Debug Panel");
                ModHelper.AddDebugFunction(manifest.ID, ModHelper.DEBUG_CATEGORY_SPECIAL, label,
                    ChemistryDebugWindow.Open, tall: true);
                if (host != null)
                {
                    host.AddComponent<ChemistryDebugButton>().Bind(label);
                }
            }
            catch (Exception e)
            {
                log.Warning("注册 F9 入口失败：" + e.Message);
            }
        }
    }
}
