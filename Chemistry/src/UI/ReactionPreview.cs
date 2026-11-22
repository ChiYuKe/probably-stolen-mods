using System;
using UnityEngine;

namespace Chemistry
{
    internal sealed class ReactionPreview : PixelElement, MachineryWidget
    {
        internal const int Width = 110;
        internal const int Height = 58;
        internal const int WaterColor = 0x41B1BE;
        private const float FrameInterval = 1f / 24f;
        private readonly Color32[] pixels = new Color32[Width * Height];
        private readonly TreeNodeRender artwork;
        private Sprite sprite;
        private AqueousReactions.Observation effects;
        private int tint;
        private int indicatorResult;
        private float amount;
        private float firstFrame;
        private float completedAt;
        private float nextFrame;
        private bool running;
        private bool showingResult;
        private bool solidAdded;
        private bool drawn;

        public bool canResize => false;
        public bool fillWidth { get; set; }
        public int widthPixels => Width;
        public int heightPixels => Height;
        public TreeNodeRender handler { get; }
        public RectTransform rectTransform => handler.rectTransform;
        public TreeNodeRender inner => handler;

        internal ReactionPreview()
        {
            handler = TreeNodeRender.Instantiate(this, "Chemistry Reaction Preview");
            handler.image.enabled = false;
            handler.image.raycastTarget = false;
            artwork = TreeNodeRender.Instantiate().SetParent(handler);
            artwork.image.raycastTarget = false;
            Validate();
        }

        internal void Begin(float volume, int liquidTint, AqueousReactions.Observation observed, bool solid, int resultTint)
        {
            amount = volume;
            tint = liquidTint;
            effects = observed;
            solidAdded = solid;
            indicatorResult = resultTint;
            firstFrame = Time.unscaledTime;
            running = true;
            showingResult = false;
            nextFrame = 0f;
        }

        internal void Finish(bool success)
        {
            running = false;
            showingResult = success;
            completedAt = Time.unscaledTime;
            nextFrame = 0f;
            drawn = false;
        }

        internal void Update(float idleAmount, int idleTint, float progress, bool hasResult)
        {
            float now = Time.unscaledTime;
            if (!running && (!showingResult || !hasResult))
            {
                if (!showingResult && drawn && amount == idleAmount && tint == idleTint) return;
                showingResult = false;
                amount = idleAmount;
                tint = idleTint;
                Render(amount, tint, AqueousReactions.Observation.None, 0f, 0f, false, false);
                return;
            }

            // Keep the result visible even when an accelerated game clock finishes the reaction between frames.
            float energy = running ? 1f : Mathf.Clamp01(1f - (now - completedAt) / 2.5f);
            if (now < nextFrame || (!running && energy == 0f && drawn)) return;
            nextFrame = now + FrameInterval;
            if (!running) progress = 1f;
            int color = indicatorResult == 0 ? tint : Blend(tint, indicatorResult, Mathf.Clamp01((progress - 0.35f) * 2f));
            Render(amount, color, effects, now - firstFrame, progress, solidAdded, energy > 0f, energy);
            if (!running && energy > 0f) drawn = false;
        }

        private void Render(float fill, int color, AqueousReactions.Observation observation, float time, float progress,
            bool solid, bool animate, float energy = 0f)
        {
            DrawFrame(pixels, fill, color, observation, time, progress, solid, animate ? energy : 0f);
            if (sprite == null)
            {
                sprite = MaterialItemArt.CreateSprite("chemistry_reaction_preview", pixels, Width, Height);
                MachineryStyle.SetSimple(artwork, sprite, Color.white);
            }
            else
            {
                sprite.texture.SetPixels32(pixels);
                sprite.texture.Apply(false, false);
            }
            drawn = true;
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

        internal static void DrawFrame(Color32[] buffer, float fill, int tint, AqueousReactions.Observation effects,
            float time, float progress, bool solid, float energy)
        {
            Box(buffer, 0, 0, Width, Height, 0x252A30);
            Box(buffer, 0, 0, Width, 1, 0x626860);
            Box(buffer, 0, 1, 1, Height - 1, 0x15191E);
            Box(buffer, 0, Height - 1, Width, 1, 0x15191E);
            Box(buffer, Width - 1, 1, 1, Height - 2, 0x535956);
            Box(buffer, 7, 9, 96, 1, 0x353E42);
            Box(buffer, 7, 7, 96, 2, 0x1C2429);
            Box(buffer, 28, 8, 52, 2, 0x1A2026);

            // Draw the back glass, then the moving liquid, then the front glass and its graduations.
            Box(buffer, 33, 13, 43, 34, 0x35464B);
            Box(buffer, 36, 11, 37, 2, 0x35464B);
            int surface = 12 + (int)Math.Round(34f * fill);
            bool hydrogen = (effects & AqueousReactions.Observation.HydrogenGas) != 0;
            bool bubbling = hydrogen || (effects & AqueousReactions.Observation.Gas) != 0;
            if (fill > 0f)
            {
                int surfaceTint = Blend(tint, 0xD1FFF0, 0.6f);
                int edgeTint = Blend(tint, 0x142D3A, 0.58f);
                int bodyTint = Blend(tint, 0x142D3A, 0.3f);
                for (int x = 33; x <= 75; x++)
                {
                    double wave = Math.Sin((x - 33) * 0.23 + time * 14.0) * energy * (hydrogen ? 2.2 : 1.25);
                    int y = surface + (int)Math.Round(wave);
                    Box(buffer, x, 13, 1, y - 13, x < 38 || x > 70 ? edgeTint : bodyTint);
                    Point(buffer, x, y, surfaceTint);
                    Point(buffer, x, y - 1, tint);
                }
                Box(buffer, 37, 12, 35, 1, Blend(tint, 0x142D3A, 0.55f));
                if (bubbling && energy > 0f) DrawBubbles(buffer, surface, time, energy, hydrogen);
                if ((effects & AqueousReactions.Observation.Sediment) != 0) DrawSediment(buffer, surface, time, progress);
                if (solid && progress < 0.58f)
                {
                    int y = 40 - (int)(Math.Min(1f, progress * 2.8f) * 23f);
                    int x = 53 + (int)(Math.Sin(time * 12f) * energy * 3f);
                    Box(buffer, x - 2, y, 5, 3, 0xAEBBC3);
                    Box(buffer, x - 1, y + 2, 3, 1, 0xEAEEE5);
                    Point(buffer, x + 2, y, 0x677680);
                }
            }

            Box(buffer, 31, 14, 2, 32, 0x212D35);
            Box(buffer, 76, 14, 2, 32, 0x212D35);
            Box(buffer, 32, 14, 1, 32, 0xA8C9C1);
            Box(buffer, 76, 14, 1, 32, 0x759A99);
            Box(buffer, 34, 14, 1, 30, 0x597E81);
            Box(buffer, 34, 34, 1, 9, 0xB5D4CB);
            Box(buffer, 33, 12, 2, 2, 0x85ABA8);
            Box(buffer, 74, 12, 2, 2, 0x759A99);
            Box(buffer, 35, 11, 39, 1, 0x8CAFAC);
            Box(buffer, 35, 10, 39, 1, 0x26373B);
            Box(buffer, 30, 46, 50, 2, 0x86A7A4);
            Box(buffer, 30, 48, 50, 1, 0x1C292E);
            Box(buffer, 31, 47, 12, 1, 0xD1E1D3);
            Box(buffer, 76, 47, 5, 1, 0xCFDFD4);
            Point(buffer, 80, 46, 0x546C73);
            for (int y = 20; y <= 40; y += 5) Box(buffer, y % 10 == 0 ? 67 : 69, y, y % 10 == 0 ? 6 : 4, 1, 0xA4BCB2);
        }

        private static void DrawBubbles(Color32[] buffer, int surface, float time, float energy, bool hydrogen)
        {
            int count = (int)Math.Ceiling((hydrogen ? 21f : 13f) * energy);
            for (int i = 0; i < count; i++)
            {
                float cycle = time * (1.4f + (i % 4) * 0.23f) + i * 0.381966f;
                float phase = cycle - (float)Math.Floor(cycle);
                int y = 14 + (int)(phase * (surface - 10));
                int x = 37 + (i * 19) % 34 + (int)(Math.Sin(phase * 5 + i) * 2);
                if (phase > 0.9f)
                {
                    Point(buffer, x, surface + (i % 3) + 2, 0xB5E1D9);
                    continue;
                }
                int glow = hydrogen ? 0xE6FAE7 : 0xB2E1D8;
                Point(buffer, x, y + 1, glow);
                Point(buffer, x - 1, y, glow);
                if (i % 3 == 0)
                {
                    Point(buffer, x + 1, y, 0x8FBDBE);
                    Point(buffer, x, y - 1, 0x8FBDBE);
                }
            }
        }

        private static void DrawSediment(Color32[] buffer, int surface, float time, float progress)
        {
            float settled = Mathf.Clamp01((progress - 0.2f) / 0.72f);
            for (int i = 0; i < 32; i++)
            {
                int x = 36 + (i * 17) % 37;
                int y = 13 + (i * 13) % 4;
                if (settled < 1 && i % 5 > settled * 5)
                    y += (int)Math.Max(0, (surface - 20) * (1f - settled) * (0.5 + Math.Sin(i * 2.54 - time * 3.3) * 0.4));
                Point(buffer, x, y, i % 4 == 0 ? 0xADA895 : 0xE3DED0);
                if (settled > 0.75f && i % 3 == 0) Point(buffer, x + 1, y, 0xCAC4B7);
            }
        }

        private static int Blend(int first, int second, float amount)
        {
            int r = (int)(((first >> 16) & 255) * (1 - amount) + ((second >> 16) & 255) * amount);
            int g = (int)(((first >> 8) & 255) * (1 - amount) + ((second >> 8) & 255) * amount);
            int b = (int)((first & 255) * (1 - amount) + (second & 255) * amount);
            return (r << 16) | (g << 8) | b;
        }

        private static Color32 Ink(int rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        private static void Point(Color32[] buffer, int x, int y, int rgb) => buffer[y * Width + x] = Ink(rgb);

        private static void Box(Color32[] buffer, int x, int y, int width, int height, int rgb)
        {
            Color32 color = Ink(rgb);
            for (int row = y; row < y + height; row++)
                for (int column = x; column < x + width; column++) buffer[row * Width + column] = color;
        }
    }
}
