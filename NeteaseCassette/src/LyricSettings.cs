using System;

namespace NeteaseCassette
{
    // 歌词浮层的显示设置，存 ModData/netease_cassette/lyrics.json
    [Serializable]
    public class LyricSettings
    {
        public float FontSize = 34f;      // 当前句字号，下一句按 0.72 倍
        public float PosX = 0f;           // 相对屏幕底边中点的位置，1080p 参考单位
        public float PosY = 200f;
        public bool Locked = false;       // 锁住之后不能再拖
        public int ColorIndex = 0;        // 当前句颜色，对照 Lyrics.ColorNames
    }
}
