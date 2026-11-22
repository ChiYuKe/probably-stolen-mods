using System;
using System.Collections.Generic;

namespace Chemistry
{
    internal static class GameTicks
    {
        internal const int PerMinute = 20;
        private static readonly List<(long Due, Action Work)> Scheduled = new List<(long, Action)>();
        private static double fraction;

        internal static long Now { get; private set; }
        internal static int Generation { get; private set; }

        internal static void Reset(long ticks = 0)
        {
            Now = ticks;
            fraction = 0;
            Generation++;
            Scheduled.Clear();
        }

        internal static int Advance(float seconds, int realMinutesPerDay)
        {
            fraction += seconds * (double)(GameClockState.MinutesPerDay * PerMinute) / (60d * realMinutesPerDay);
            int elapsed = (int)fraction;
            fraction -= elapsed;
            Now += elapsed;
            return elapsed;
        }

        internal static void Schedule(int ticks, Action work) => Scheduled.Add((Now + Math.Max(1, ticks), work));

        internal static void CompleteDue()
        {
            for (int index = 0; index < Scheduled.Count;)
            {
                var job = Scheduled[index];
                if (job.Due > Now)
                {
                    index++;
                    continue;
                }
                Scheduled.RemoveAt(index);
                try
                {
                    job.Work();
                }
                catch (Exception error)
                {
                    ChemistryMod.Log?.Warning("游戏刻任务结算失败：" + error);
                }
            }
        }

        internal static int Remaining(long startTick, int duration) =>
            (int)Math.Max(0, Math.Min(duration, startTick + duration - Now));

        internal static float Progress(long startTick, int duration)
            => 1f - Remaining(startTick, duration) / (float)duration;

        internal static string Label(int ticks) => Loc.Pick($"{ticks} 刻", $"{ticks} ticks");
    }
}
