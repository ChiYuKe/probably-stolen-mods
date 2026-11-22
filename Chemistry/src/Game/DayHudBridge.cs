using System;

namespace Chemistry
{
    internal static class DayHudBridge
    {
        public static void Push(string message, string tone = "info", float duration = 7f)
        {
            if (string.IsNullOrEmpty(message)) return;
            var push = ModHelper.Lookup<Func<string, string, float, bool>>("day_hud", "notice_v1");
            if (push == null) return;
            try
            {
                push(message, tone, duration);
            }
            catch (Exception error)
            {
                ChemistryMod.Log?.Warning("发送 DayHud 通知失败：" + error.Message);
            }
        }
    }
}
