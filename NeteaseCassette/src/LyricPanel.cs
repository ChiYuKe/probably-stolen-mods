using System;
using UnityEngine;

namespace NeteaseCassette
{
    // 歌词设置窗口：字号、颜色、位置锁
    public static class LyricPanel
    {
        private const string WindowId = "netease_cassette.lyrics_ui";
        private const string StatusId = "ly_set_status";
        private const string LockId = "ly_set_lock";
        private const string ColorIdPrefix = "ly_set_color_";

        private static CustomUIWindow window;

        public static void Open()
        {
            if (CustomUIManager.Instance == null)
            {
                ModEntry.Log?.Warning("场景里没有 CustomUIManager，开不了歌词设置");
                return;
            }

            var builder = CustomUIManager.Window(WindowId, "歌词设置");
            if (builder == null) return;
            window = builder.Handle;

            builder
                .SetSize(560f, 340f)
                .SetDraggable(true)
                .SetCloseOnEscape(true)
                .Center();

            builder.AddLabel("字号");
            builder.BeginRow();
            builder.AddButton("小一点", () => { Lyrics.ChangeFontSize(-2f); Refresh(); });
            builder.AddSpace(6f);
            builder.AddButton("大一点", () => { Lyrics.ChangeFontSize(2f); Refresh(); });
            builder.End();

            builder.AddSpace(4f);
            builder.AddLabel("当前句颜色");
            builder.BeginRow(4f);
            for (int i = 0; i < Lyrics.ColorNames.Length; i++)
            {
                int index = i;
                builder.AddButton(Lyrics.ColorNames[i], () => { Lyrics.SetColor(index); Refresh(); }, ColorIdPrefix + index);
                if (i < Lyrics.ColorNames.Length - 1) builder.AddSpace(4f);
            }
            builder.End();

            builder.AddSpace(4f);
            builder.AddLabel("位置");
            builder.BeginRow();
            builder.AddButton("锁定位置", () => { Lyrics.ToggleLock(); Refresh(); }, LockId);
            builder.AddSpace(6f);
            builder.AddButton("回到默认位置", () => { Lyrics.ResetPosition(); Refresh(); });
            builder.End();

            builder.AddSpace(6f);
            builder.AddLabel(" ", StatusId);
            builder.BeginRow();
            builder.AddFlexibleSpace();
            builder.AddButton("关闭", () => CustomUIManager.Instance?.CloseWindow(WindowId));
            builder.End();

            builder.Show();
            Refresh();
        }

        public static void Close()
        {
            try
            {
                CustomUIManager.Instance?.CloseWindow(WindowId);
            }
            catch
            {
                // 没开过就算了
            }
            window = null;
        }

        // 点完之后把按钮文字和状态行一起刷新，别让按钮一直写着老状态
        private static void Refresh()
        {
            var s = Lyrics.Settings;
            SetText(StatusId, Lyrics.StatusLine());
            SetText(LockId, s.Locked ? "解锁拖动" : "锁定位置");

            for (int i = 0; i < Lyrics.ColorNames.Length; i++)
            {
                string name = Lyrics.ColorNames[i];
                SetText(ColorIdPrefix + i, i == s.ColorIndex ? "【" + name + "】" : name);
            }
        }

        private static void SetText(string id, string value)
        {
            var w = window;
            if (w == null || !w.IsAlive) return;
            try
            {
                var element = w.Get(id);
                if (element != null) element.SetText(value);
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("刷新歌词设置失败：" + e.Message);
            }
        }
    }
}
