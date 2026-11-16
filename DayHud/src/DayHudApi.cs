using System;
using System.Collections.Generic;
using UnityEngine;

namespace DayHud
{
    /// <summary>
    /// 给其他 mod 用的轻量接口。游戏本身没有通知总线，只有 ModHook 的生命周期事件，
    /// 所以跨 mod 推通知需要走这里。
    ///
    /// 其他 mod 不建议硬引用 DayHud.dll，用反射即可：
    ///
    /// var api = Type.GetType("DayHud.DayHudApi, DayHud");
    /// var push = api?.GetMethod("Push", new[] { typeof(string), typeof(string), typeof(float) });
    /// push?.Invoke(null, new object[] { "消息", "info", 7f });
    ///
    /// 没装 DayHud 时 Type.GetType 返回 null，反射调用自然不会发生，不会让别的 mod 报错。
    /// 如果 DayHud 已装但 HUD 还没建好，Push 会先排进队列，等 HUD 建好后补发。
    /// </summary>
    public static class DayHudApi
    {
        public enum Tone
        {
            Info,
            Warning,
            Danger,
            Muted
        }

        private sealed class PendingNotice
        {
            public string Message;
            public Tone Tone;
            public float Duration;
            public Action OnClick;
            public bool AllowDuplicate;
        }

        private const int MaxPending = 20;

        private static readonly List<PendingNotice> pending = new List<PendingNotice>();
        private static DayHudPanel panel;

        /// <summary>接口版本，后续只增不改；其他 mod 可选检查。</summary>
        public static int ApiVersion => 2;

        /// <summary>HUD 当前是否已经建好，可以立刻显示通知。</summary>
        public static bool IsAvailable => panel != null;

        internal static void Attach(DayHudPanel target)
        {
            panel = target;
            try
            {
                FlushPending();
            }
            catch (Exception e)
            {
                LogWarning("补发排队通知失败：" + e.Message);
            }
        }

        internal static void Detach(DayHudPanel target)
        {
            if (panel == target)
            {
                panel = null;
            }
        }

        /// <summary>
        /// 反射友好的入口：参数只有 string / float，不依赖 DayHud 里的任何类型。
        /// tone 支持 info / warning / danger / muted，也接受 warn / error / silent。
        /// HUD 还没建好时会排队，返回 true 表示已接收。
        /// </summary>
        public static bool Push(string message, string tone = "info", float duration = 7f)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            Tone parsed = ParseTone(tone);
            if (panel == null)
            {
                Enqueue(new PendingNotice
                {
                    Message = message,
                    Tone = parsed,
                    Duration = duration
                });
                return true;
            }

            try
            {
                panel.Notify(message, ToPanelTone(parsed), duration);
                return true;
            }
            catch (Exception e)
            {
                LogWarning("推送通知失败：" + e.Message);
                return false;
            }
        }

        /// <summary>
        /// 反射友好的可点击入口：点击通知后打开并居中 focusItem 的 contentWindow。
        /// </summary>
        public static bool PushFocus(string message, string tone, float duration, GameItem focusItem)
        {
            return PushAction(message, tone, duration,
                focusItem != null ? (Action)(() => Focus(focusItem)) : null);
        }

        /// <summary>
        /// 反射友好的自定义回调入口：点击通知后执行 onClick。
        /// onClick 是 System.Action，不依赖 DayHud 里的任何类型。
        /// </summary>
        public static bool PushAction(string message, string tone, float duration, Action onClick)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            Tone parsed = ParseTone(tone);
            if (panel == null)
            {
                Enqueue(new PendingNotice
                {
                    Message = message,
                    Tone = parsed,
                    Duration = duration,
                    OnClick = onClick
                });
                return true;
            }

            try
            {
                panel.Notify(message, ToPanelTone(parsed), duration, onClick);
                return true;
            }
            catch (Exception e)
            {
                LogWarning("推送可点击通知失败：" + e.Message);
                return false;
            }
        }

        /// <summary>推一条普通通知，默认 7 秒后消失。</summary>
        public static void Notify(string message, float duration = 7f)
        {
            Notify(message, Tone.Info, duration);
        }

        /// <summary>
        /// 强类型版本，给方便直接引用的代码用。onClick 不为空时这一行可点击；
        /// allowDuplicate 为 true 时允许相同文本重复出现。
        /// </summary>
        public static void Notify(string message, Tone tone, float duration = 7f,
            Action onClick = null, bool allowDuplicate = false)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (panel == null)
            {
                Enqueue(new PendingNotice
                {
                    Message = message,
                    Tone = tone,
                    Duration = duration,
                    OnClick = onClick,
                    AllowDuplicate = allowDuplicate
                });
                return;
            }

            try
            {
                panel.Notify(message, ToPanelTone(tone), duration, onClick, allowDuplicate);
            }
            catch (Exception e)
            {
                LogWarning("推送通知失败：" + e.Message);
            }
        }

        /// <summary>打开并居中一个带 contentWindow 的物品/机器窗口，方便做点击聚焦。</summary>
        public static bool Focus(GameItem item)
        {
            if (panel == null || item == null)
            {
                return false;
            }

            try
            {
                return panel.FocusGameItem(item);
            }
            catch (Exception e)
            {
                LogWarning("聚焦物品窗口失败：" + e.Message);
                return false;
            }
        }

        private static void Enqueue(PendingNotice notice)
        {
            while (pending.Count >= MaxPending)
            {
                pending.RemoveAt(0);
            }

            pending.Add(notice);
        }

        private static void FlushPending()
        {
            if (panel == null || pending.Count == 0)
            {
                return;
            }

            PendingNotice[] queued = pending.ToArray();
            pending.Clear();

            foreach (PendingNotice notice in queued)
            {
                try
                {
                    panel.Notify(notice.Message, ToPanelTone(notice.Tone), notice.Duration,
                        notice.OnClick, notice.AllowDuplicate);
                }
                catch (Exception e)
                {
                    LogWarning("补发通知失败：" + e.Message);
                }
            }
        }

        private static void LogWarning(string message)
        {
            try
            {
                Debug.LogWarning("[DayHudApi] " + message);
            }
            catch
            {
                // 日志系统还没起来时忽略
            }
        }

        private static Tone ParseTone(string tone)
        {
            if (string.IsNullOrEmpty(tone))
            {
                return Tone.Info;
            }

            if (Enum.TryParse(tone, true, out Tone parsed))
            {
                return parsed;
            }

            switch (tone.Trim().ToLowerInvariant())
            {
                case "warn":
                    return Tone.Warning;
                case "error":
                    return Tone.Danger;
                case "silent":
                case "quiet":
                    return Tone.Muted;
                default:
                    return Tone.Info;
            }
        }

        private static DayHudPanel.NoticeTone ToPanelTone(Tone tone)
        {
            switch (tone)
            {
                case Tone.Warning:
                    return DayHudPanel.NoticeTone.Warning;
                case Tone.Danger:
                    return DayHudPanel.NoticeTone.Danger;
                case Tone.Muted:
                    return DayHudPanel.NoticeTone.Muted;
                default:
                    return DayHudPanel.NoticeTone.Info;
            }
        }
    }
}
