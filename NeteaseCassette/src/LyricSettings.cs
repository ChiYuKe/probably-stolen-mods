using System;

namespace NeteaseCassette
{
    // 歌词浮层的显示设置，存 ModData/netease_cassette/lyrics.json
    [Serializable]
    public class LyricSettings
    {
        public int LayoutVersion = 1;     // 1 = 位置改成相对右上角算
        public float FontSize = 34f;      // 当前句字号，下一句按 0.72 倍
        public float PosX = -40f;         // 相对右上角的位置，负数是往左/往下
        public float PosY = -40f;
        public bool Locked = false;       // 锁住之后不能再拖
        public bool Shown = false;        // 上次退出时歌词是开着的，下次进来自动打开
        public int ColorIndex = 0;        // 当前句颜色，对照 Lyrics.ColorNames
    }
}
