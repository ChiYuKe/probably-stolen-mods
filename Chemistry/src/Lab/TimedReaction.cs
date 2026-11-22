using System;
using System.Linq;

namespace Chemistry
{
    internal sealed class TimedReaction
    {
        private sealed class Job
        {
            internal string Name;
            internal int Duration;
            internal long Start;
            internal int Generation;
        }

        private Job current;
        internal bool Running => current != null && current.Generation == GameTicks.Generation;

        internal static int DurationFor(CompoundDef def)
            => 48 + Math.Min(112, def.Inputs.Values.Sum() * 4 + (def.Tools?.Length ?? 0) * 8);

        internal bool TryStart(string name, int ticks, Func<bool> available, Action<string> show, Action complete)
        {
            if (Running)
            {
                show(Loc.Pick("已有一批反应在进行。", "A reaction is already in progress."));
                return false;
            }
            var job = new Job { Name = name, Duration = ticks, Start = GameTicks.Now, Generation = GameTicks.Generation };
            current = job;
            Advance(job, available, show, complete);
            return true;
        }

        private void Advance(Job job, Func<bool> available, Action<string> show, Action complete)
        {
            if (current != job || !Running) return;
            if (!available())
            {
                current = null;
                show(Loc.Pick("反应已取消，原料保留。", "Reaction cancelled. Materials kept."));
                return;
            }
            int remaining = GameTicks.Remaining(job.Start, job.Duration);
            if (remaining == 0 && ItemMouseDragHandler.current?.IsDraggingItem != true)
            {
                current = null;
                complete();
                return;
            }
            GameTicks.Schedule(Math.Max(1, Math.Min(remaining, 5)), () => Advance(job, available, show, complete));
            show(remaining == 0 ? Loc.Pick("等待拖放结束后结算", "Waiting for item drag to finish")
                : Loc.Pick("反应中 · ", "Reacting · ") + job.Name + " · " + GameTicks.Label(remaining));
        }
    }
}
