using System;
using UnityEngine;

namespace Chemistry
{
    internal sealed class GameClockState
    {
        internal const int MinutesPerDay = 24 * 60;
        internal const int Morning = 6 * 60;
        internal const int Opening = 8 * 60;
        internal const int Closing = 20 * 60;
        private int phaseTicks;

        internal int Day { get; private set; }
        internal PlayerStore.StoreState Phase { get; private set; }
        internal int Minute => phaseTicks / GameTicks.PerMinute % MinutesPerDay;
        internal bool ReachedBoundary => phaseTicks >= End(Phase) * GameTicks.PerMinute;
        internal int SavedTicks => phaseTicks;

        internal void SetPhase(int day, PlayerStore.StoreState phase, int savedTicks = -1)
        {
            Day = day;
            Phase = phase;
            int start = Start(phase) * GameTicks.PerMinute;
            phaseTicks = savedTicks < start || savedTicks > End(phase) * GameTicks.PerMinute ? start : savedTicks;
        }

        internal void Tick(int ticks) => phaseTicks = Math.Min(End(Phase) * GameTicks.PerMinute, phaseTicks + ticks);

        private static int Start(PlayerStore.StoreState phase) => phase == PlayerStore.StoreState.MORNING ? Morning
            : phase == PlayerStore.StoreState.OPEN ? Opening : Closing;

        private static int End(PlayerStore.StoreState phase) => phase == PlayerStore.StoreState.MORNING ? Opening
            : phase == PlayerStore.StoreState.OPEN ? Closing : MinutesPerDay + Morning;
    }

    internal sealed class GameClock : MonoBehaviour
    {
        private const string DisplayService = "clock_display_v1";
        private const string SaveKey = "game_clock_v2";
        private const string LegacySaveKey = "game_clock_v1";
        private string modId;
        private ModLog log;
        private readonly GameClockState state = new GameClockState();
        private PlayerStore owner;
        private long savedWorldTick = -1;
        private float nextShutterCheck;
        private bool saveWarning;

        internal void Initialize(string modId, ModLog log)
        {
            this.modId = modId;
            this.log = log;
            ModHelper.Expose(modId, DisplayService, (Func<(int Day, int MinuteOfDay, bool Waiting)>)ReadDisplay);
        }

        internal int RealMinutesPerDay { get; set; } = 24;
        internal bool AutoShutter { get; set; }
        internal (int Day, int MinuteOfDay, bool Waiting) ReadDisplay() => owner == null
            ? default : (state.Day, state.Minute, state.ReachedBoundary);

        internal void Reset()
        {
            owner = null;
            savedWorldTick = -1;
            nextShutterCheck = 0f;
            saveWarning = false;
            GameTicks.Reset();
        }

        private void Update()
        {
            if (modId == null) return;
            if (NewGameData.Instance?.isInMainMenu == true || !PlayerStore.HasInstance)
            {
                if (owner != null) Reset();
                return;
            }
            PlayerStore store = PlayerStore.Instance;
            int day = Math.Max(1, StoreStation.GetDayCounter());
            var phase = store.storeState;
            if (owner != store)
            {
                owner = store;
                var restored = RestoreTicks(day, phase);
                GameTicks.Reset(restored.World);
                state.SetPhase(day, phase, restored.Phase);
                savedWorldTick = -1;
            }
            else if (state.Day != day || state.Phase != phase)
            {
                state.SetPhase(day, phase);
                savedWorldTick = -1;
            }

            var ui = StoreUIManager.Instance;
            bool paused = Time.timeScale <= 0f || ui?.escapeUIManager?.menuPanel?.activeInHierarchy == true
                || ui?.gameOptionUIManager?.IsOpen == true;
            int advanced = !paused && store.lockShutterButtonFlag <= 0
                ? GameTicks.Advance(Math.Min(Time.unscaledDeltaTime, 0.5f), RealMinutesPerDay) : 0;
            state.Tick(advanced);
            SaveTicks();
            if (advanced > 0) GameTicks.CompleteDue();
            if (!paused && AutoShutter && state.ReachedBoundary && phase != PlayerStore.StoreState.CLOSED)
                TrySwitchShutter(store, ui, phase);
        }

        private (int Phase, long World) RestoreTicks(int day, PlayerStore.StoreState phase)
        {
            string[] fields = ModHelper.GetModData(modId, SaveKey)?.Split(',');
            if (fields?.Length == 4 && long.TryParse(fields[3], out long world) && world >= 0 && world <= long.MaxValue / 2)
            {
                int ticks = MatchesPhase(fields, day, phase) && int.TryParse(fields[2], out int saved) ? saved : -1;
                return (ticks, world);
            }
            fields = (ModHelper.GetModData(modId, LegacySaveKey) ?? ModHelper.GetModData("day_hud", LegacySaveKey))?.Split(',');
            return fields?.Length == 3 && MatchesPhase(fields, day, phase) && int.TryParse(fields[2], out int minute)
                && minute >= 0 && minute <= GameClockState.MinutesPerDay + GameClockState.Morning
                ? (minute * GameTicks.PerMinute, 0) : (-1, 0);
        }

        private static bool MatchesPhase(string[] fields, int day, PlayerStore.StoreState phase)
            => int.TryParse(fields[0], out int savedDay) && savedDay == day
                && int.TryParse(fields[1], out int savedPhase) && savedPhase == (int)phase;

        private void OnDisable()
        {
            Reset();
            if (modId != null) ModHelper.Expose(modId, DisplayService, null);
        }

        private void SaveTicks()
        {
            if (savedWorldTick == GameTicks.Now) return;
            try
            {
                ModHelper.SetModData(modId, SaveKey, FormattableString.Invariant($"{state.Day},{(int)state.Phase},{state.SavedTicks},{GameTicks.Now}"));
                savedWorldTick = GameTicks.Now;
            }
            catch (Exception error)
            {
                if (saveWarning) return;
                saveWarning = true;
                log?.Warning("保存游戏时钟失败：" + error.Message);
            }
        }

        private void TrySwitchShutter(PlayerStore store, StoreUIManager ui, PlayerStore.StoreState phase)
        {
            if (Time.unscaledTime < nextShutterCheck) return;
            nextShutterCheck = Time.unscaledTime + 1f;
            StoreShutterButton shutter = StoreShutterButton.Instance;
            if (ui == null || shutter == null || !shutter.isActiveAndEnabled || EmporiumEntry.Instance == null
                || store.playerLocation != PlayerStore.PlayerLocation.IN_STORE || MapUIManager.Instance?.IsOutside == true
                || store.lockShutterButtonFlag > 0 || store.isClientBeingArrested || store.isHandlingShootout
                || ItemMouseDragHandler.current?.IsDraggingItem == true || ui.closeShutterConfirmationPanel?.activeInHierarchy == true
                || AdvCalendarUIManager.Instance?.UIPanel?.activeInHierarchy == true)
                return;

            try
            {
                if (phase == PlayerStore.StoreState.MORNING && store.CanStartDay())
                    shutter.OpenShutter();
                else if (phase == PlayerStore.StoreState.OPEN && store.currentClientInstance == null
                    && store.storeClientManager?.clientStack?.Count == 0 && store.CanEndDay())
                    shutter.CloseShutter();
            }
            catch (Exception error)
            {
                nextShutterCheck = Time.unscaledTime + 10f;
                log?.Warning("自动切换营业状态失败：" + error.Message);
            }
        }
    }
}
