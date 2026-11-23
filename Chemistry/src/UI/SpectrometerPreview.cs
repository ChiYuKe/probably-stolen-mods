using System;
using UnityEngine;

namespace Chemistry
{
    /// <summary>光谱仪操作区的像素光路与读数屏。</summary>
    internal sealed class SpectrometerPreview : PixelElement, MachineryWidget
    {
        internal enum Specimen { Empty, Solid, Liquid, Gas }

        private enum Phase { Idle, Warming, Sweeping, Settling, Recorded }

        internal const int Width = 110;
        internal const int Height = 58;
        private const float FrameInterval = 1f / 24f;
        private const float WarmupEnd = 0.18f;
        private const float SweepEnd = 0.88f;
        private const int PlotX = 10;
        private const int PlotWidth = 84;
        private const int Baseline = 8;
        private const int Bay = 0x171C20;
        private const int Screen = 0x121F20;
        private const int Copper = 0xBA9060;
        private const int Teal = 0x79BCAC;
        private const int Pale = 0xC7DED0;
        private static readonly int[] PeakInks = { 0xA8CFB1, 0xD5B17D, 0x87B5C5, 0xB5A4C3 };

        private readonly Color32[] pixels = new Color32[Width * Height];
        private readonly int[] trace = new int[PlotWidth];
        private readonly int[] peaks = new int[PlotWidth];
        private readonly TreeNodeRender artwork;
        private Sprite sprite;
        private (Specimen Form, int Tint) specimen;
        private float startedAt;
        private float nextFrame;
        private bool running;
        private bool recorded;
        private bool dirty = true;

        public bool canResize => false;
        public bool fillWidth { get; set; }
        public int widthPixels => Width;
        public int heightPixels => Height;
        public TreeNodeRender handler { get; }
        public RectTransform rectTransform => handler.rectTransform;
        public TreeNodeRender inner => handler;

        internal SpectrometerPreview()
        {
            handler = TreeNodeRender.Instantiate(this, "Chemistry Spectrometer Preview");
            handler.image.enabled = false;
            handler.image.raycastTarget = false;
            artwork = TreeNodeRender.Instantiate().SetParent(handler);
            artwork.image.raycastTarget = false;
            Validate();
        }

        internal void Begin(string identifier, (Specimen Form, int Tint) sample)
        {
            specimen = sample;
            BuildTrace(identifier);
            startedAt = Time.unscaledTime;
            running = true;
            recorded = false;
            dirty = true;
        }

        internal void Finish(bool success)
        {
            running = false;
            recorded = success;
            dirty = true;
        }

        internal void Update((Specimen Form, int Tint) idleSample, float progress, bool hasResult)
        {
            if (!running)
            {
                if (recorded && !hasResult)
                {
                    recorded = false;
                    dirty = true;
                }
                if (!recorded && specimen != idleSample)
                {
                    specimen = idleSample;
                    dirty = true;
                }
                if (dirty) Render(recorded ? Phase.Recorded : Phase.Idle, 1f, 0);
                return;
            }

            float now = Time.unscaledTime;
            if (!dirty && now < nextFrame) return;
            nextFrame = now + FrameInterval;
            int step = (int)((now - startedAt) * 18f);
            if (progress < WarmupEnd)
                Render(Phase.Warming, progress / WarmupEnd, step);
            else if (progress < SweepEnd)
                Render(Phase.Sweeping, (progress - WarmupEnd) / (SweepEnd - WarmupEnd), step);
            else
                Render(Phase.Settling, 1f, step);
        }

        private void Render(Phase phase, float progress, int step)
        {
            Box(0, 0, Width, Height, 0x111417);
            Box(1, 1, 108, 56, 0x353A3C);
            Box(2, 55, 106, 2, 0x60635F);
            Box(1, 1, 108, 2, 0x24292C);
            Box(4, 28, 102, 25, Bay);
            Box(4, 53, 102, 1, 0x252A2B);
            Box(4, 27, 102, 1, 0x545652);
            Point(3, 54, 0x8A8272);
            Point(106, 54, 0x8A8272);
            Point(3, 3, 0x686458);
            Point(106, 3, 0x686458);

            DrawOptics(phase, progress, step);
            DrawReadout(phase, progress);

            if (sprite == null)
            {
                sprite = MaterialItemArt.CreateSprite("chemistry_spectrometer_preview", pixels, Width, Height);
                MachineryStyle.SetSimple(artwork, sprite, Color.white);
            }
            else
            {
                sprite.texture.SetPixels32(pixels);
                sprite.texture.Apply(false, false);
            }
            dirty = false;
        }

        private void DrawOptics(Phase phase, float progress, int step)
        {
            bool beamOn = phase is Phase.Sweeping or Phase.Settling;
            bool warming = phase == Phase.Warming;
            int lift = phase == Phase.Idle ? 3 : warming ? 3 - (int)(progress * 4f) : 0;
            int beam = phase == Phase.Sweeping ? SweepInk(progress) : Teal;

            // 镜筒、探测器与样品托架共用一段光学导轨。
            Box(9, 30, 92, 2, 0x3D4848);
            Box(14, 29, 82, 1, 0x20292A);
            Box(29, 39, 52, 1, 0x2D3939);
            DrawOpticalHead(10, false, warming || beamOn ? Copper : 0x5D533F);
            int detectorLamp = beamOn && progress >= 0.85f ? beam : phase == Phase.Recorded ? Teal : 0x3A5550;
            DrawOpticalHead(81, true, detectorLamp);

            Box(45, 32, 20, 2, 0x414D4D);
            Box(47, 34, 16, 15, 0x101619);
            DrawSpecimen(35 + lift);

            if (beamOn)
            {
                Box(34, 38, 14, 3, 0x304948);
                Box(62, 38, 14, 3, 0x263A38);
                Box(34, 39, 14, 1, beam);
                Box(48, 39, 14, 1, Blend(beam, Pale, 0.32f));
                Box(62, 39, 14, 1, Blend(beam, Bay, 0.28f));
                int pulse = 35 + (int)(progress * 37f);
                int pulseLength = Math.Min(5, 76 - pulse);
                Box(pulse, 39, pulseLength, 1, pulse < 62 ? 0xDFE6D2 : Pale);
                if (step % 6 < 2) Point(54, 40, Pale);
            }

            if (phase is Phase.Settling or Phase.Recorded)
            {
                Box(46, 48, 18, 3, 0x495557);
                Box(48, 50, 14, 1, 0x78817A);
                Point(55, 49, phase == Phase.Recorded ? Teal : Copper);
            }
        }

        private void DrawOpticalHead(int x, bool detector, int lamp)
        {
            Box(x, 33, 19, 17, 0x101518);
            Box(x + 1, 34, 17, 15, 0x3D4649);
            Box(x + 2, 46, 15, 2, 0x626A66);
            Box(x + 2, 35, 15, 1, 0x2B3337);
            Box(x + 4, 38, 11, 6, 0x252D31);
            for (int slot = 0; slot < 3; slot++) Box(x + 5 + slot * 3, 39, 1, 4, 0x4C5556);
            Box(x + (detector ? 13 : 3), 46, 3, 1, lamp);
            int lens = detector ? x - 5 : x + 19;
            Box(lens, 36, 5, 7, 0x293237);
            Box(lens, 37, 5, 5, 0x505E5D);
            Box(lens + (detector ? 0 : 4), 38, 1, 3, lamp);
        }

        private void DrawSpecimen(int bottom)
        {
            const int Glass = 0x506664;
            Box(48, bottom, 14, 11, 0x213031);
            Box(48, bottom, 14, 1, Glass);
            Box(48, bottom + 1, 1, 10, Glass);
            Box(61, bottom + 1, 1, 10, 0x3C5050);
            Box(48, bottom + 10, 14, 2, 0x4D5C5B);
            Box(50, bottom + 11, 10, 1, 0x778783);

            int color = specimen.Tint;
            int shade = Blend(color, Bay, 0.55f);
            int light = Blend(color, Pale, 0.36f);
            switch (specimen.Form)
            {
                case Specimen.Solid:
                    Box(50, bottom + 1, 10, 2, shade);
                    Box(51, bottom + 3, 7, 2, color);
                    Box(53, bottom + 5, 3, 1, light);
                    Point(52, bottom + 4, light);
                    Point(58, bottom + 2, color);
                    break;
                case Specimen.Liquid:
                    Box(49, bottom + 1, 12, 5, shade);
                    Box(49, bottom + 6, 12, 1, light);
                    Box(51, bottom + 2, 1, 3, color);
                    Point(57, bottom + 5, color);
                    break;
                case Specimen.Gas:
                    Box(50, bottom + 1, 10, 8, shade);
                    Box(52, bottom + 3, 3, 1, color);
                    Box(56, bottom + 6, 2, 1, color);
                    Point(52, bottom + 7, light);
                    break;
            }
        }

        private void DrawReadout(Phase phase, float progress)
        {
            Box(5, 4, 100, 21, 0x101619);
            Box(6, 23, 98, 1, 0x444E4B);
            Box(8, 6, 88, 16, Screen);
            for (int x = PlotX; x <= 90; x += 10) Box(x, 7, 1, 14, 0x203031);
            Box(PlotX, 13, PlotWidth, 1, 0x203031);
            Box(PlotX, 18, PlotWidth, 1, 0x203031);
            Box(PlotX, Baseline, PlotWidth, 1, 0x344643);

            // 右侧指示灯依次对应待机、采集与读数锁定。
            Box(98, 18, 4, 2, phase is Phase.Idle or Phase.Warming ? Copper : 0x504334);
            Box(98, 13, 4, 2, phase is Phase.Sweeping or Phase.Settling ? Teal : 0x32423F);
            Box(98, 8, 4, 2, phase == Phase.Recorded ? 0xA2C69C : 0x364237);

            if (phase == Phase.Idle) return;
            if (phase == Phase.Warming)
            {
                int width = (int)(PlotWidth * progress);
                Box(PlotX, Baseline, width, 1, Copper);
                if (width > 0) Point(PlotX + width - 1, Baseline + 1, 0xDCBD8A);
                return;
            }

            int last = (int)((PlotWidth - 1) * progress);
            if (phase == Phase.Sweeping)
            {
                int trail = Math.Max(0, last - 3);
                Box(PlotX + trail, Baseline + 1, last - trail + 1, 12, 0x283C39);
            }
            for (int i = 0; i <= last; i++)
            {
                int height = trace[i];
                int previous = i == 0 ? height : trace[i - 1];
                int bottom = Mathf.Min(height, previous);
                Box(PlotX + i, Baseline + 1, 1, height, 0x284E47);
                Box(PlotX + i, Baseline + bottom, 1, Math.Abs(height - previous) + 1, Teal);
                if (peaks[i] > 0)
                    Point(PlotX + i, Baseline + height + 1, PeakInks[peaks[i] - 1]);
            }
            if (phase == Phase.Sweeping)
            {
                Box(PlotX + last, Baseline + 1, 1, 12, 0x476761);
                Point(PlotX + last, Baseline + trace[last], Pale);
                Point(PlotX + last, 21, Pale);
            }
        }

        private void BuildTrace(string identifier)
        {
            Array.Fill(trace, 1);
            Array.Clear(peaks, 0, peaks.Length);
            SampleDef sample = SampleTable.Find(identifier);
            if (sample != null)
            {
                int remaining = 100;
                int spacing = Math.Min(18, 48 / Math.Max(1, sample.Components.Length - 1));
                for (int i = 0; i < sample.Components.Length; i++)
                {
                    var component = sample.Components[i];
                    remaining -= component.Percent;
                    AddPeak(14 + spacing * i + (int)(Fingerprint(component.Symbol) % 7), 4,
                        1 + (component.Percent * 10 + 50) / 100, i % 3);
                }
                if (remaining > 0) AddPeak(74, 6, 1 + (remaining * 10 + 50) / 100, 3);
                return;
            }

            if (LabAnalysisData.IsOpticalSample(identifier))
            {
                int concentration = LabAnalysisData.RelativeConcentration(identifier);
                if (concentration > 0) AddPeak(32, 10, 1 + concentration / 10, 2);
                return;
            }

            // 其他可分析物品使用稳定的示意波形，每次扫描保留同一组峰。
            uint seed = Fingerprint(identifier);
            AddPeak(15 + (int)(seed % 9), 5, 8, 0);
            AddPeak(39 + (int)((seed >> 8) % 9), 7, 5 + (int)((seed >> 16) % 3), 1);
            AddPeak(65 + (int)((seed >> 24) % 9), 4, 4, 2);
        }

        private void AddPeak(int center, int radius, int height, int ink)
        {
            for (int x = center - radius + 1; x < center + radius; x++)
            {
                int distance = x - center;
                float fraction = (float)distance / radius;
                int value = 1 + (int)((height - 1) * (1f - fraction * fraction) + 0.5f);
                trace[x] = Mathf.Max(trace[x], value);
            }
            peaks[center] = ink + 1;
        }

        private static uint Fingerprint(string id)
        {
            uint value = 2166136261u;
            foreach (char character in id) value = unchecked((value ^ character) * 16777619u);
            return value;
        }

        private static int SweepInk(float progress) =>
            progress < 0.25f ? 0x8BAAC5 : progress < 0.5f ? Teal : progress < 0.75f ? 0xB2C28A : Copper;

        private static int Blend(int first, int second, float amount)
        {
            int r = (int)(((first >> 16) & 255) * (1f - amount) + ((second >> 16) & 255) * amount);
            int g = (int)(((first >> 8) & 255) * (1f - amount) + ((second >> 8) & 255) * amount);
            int b = (int)((first & 255) * (1f - amount) + (second & 255) * amount);
            return (r << 16) | (g << 8) | b;
        }

        private static Color32 Ink(int rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        private void Point(int x, int y, int rgb) => pixels[y * Width + x] = Ink(rgb);

        private void Box(int x, int y, int width, int height, int rgb)
        {
            Color32 color = Ink(rgb);
            for (int row = y; row < y + height; row++)
                Array.Fill(pixels, color, row * Width + x, width);
        }

        internal void Release()
        {
            if (sprite == null) return;
            UnityEngine.Object.Destroy(sprite.texture);
            UnityEngine.Object.Destroy(sprite);
            sprite = null;
        }

        public void Validate() => MachineryStyle.Place(artwork, 0, 0, Width, Height);
        public void ResizePixels(int width, int height) { }
        public int WidthPixelsClamp(int width) => Width;
        public int HeightPixelsClamp(int height) => Height;
        public int XClamp(int x) => 0;
        public int YClamp(int y) => 0;
        public bool Attach(PixelElement element) => false;
        public bool Detach(PixelElement element) => false;
    }
}
