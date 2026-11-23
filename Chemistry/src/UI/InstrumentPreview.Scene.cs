using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chemistry
{
    internal abstract partial class InstrumentAnimPreview
    {
        internal static void Present(Color32[] pixels, Phase phase, float progress, float age, float inspection, bool ready, bool success)
        {
            const int unlit = 0x32423F;
            int signal = phase == Phase.Done && !success ? Scene.Heat : Scene.Aqua;
            int edge = inspection > 0.5f ? 0x88948A : 0x53585B;
            Box(pixels, 2, 55, 106, 1, edge);
            // Short recessed rail with a three-position ready / run / result indicator.
            Box(pixels, 5, 3, 100, 3, 0x101619);
            Box(pixels, 7, 4, 78, 1, 0x2D3939);
            int length = phase == Phase.Run ? Scene.Scale(progress, 78) : phase == Phase.Done ? 78 : 0;
            if (length > 0) Box(pixels, 7, 4, length, 1, 0x506664);
            Box(pixels, 89, 4, 3, 1, phase == Phase.Idle && ready ? Scene.Gold : unlit);
            Box(pixels, 94, 4, 3, 1, phase == Phase.Run ? Scene.Aqua : unlit);
            Box(pixels, 99, 4, 3, 1, phase == Phase.Done ? (age < 0.28f ? Scene.Ivory : signal) : unlit);
        }

        // 绘图坐标从左上角起算，写入时转换成 Unity 纹理的行序。
        internal readonly struct Scene
        {
            // Metal scanner and chemical scanner sprites share these enamel and edge colours.
            internal const int Shadow = 0x292234;
            internal const int Shade = 0x373541;
            internal const int Metal = 0x53585B;
            internal const int Rim = 0x88948A;
            internal const int Ivory = 0xC4C4B0;
            internal const int Aqua = 0x79BCAC;
            internal const int Gold = 0xBA9060;
            internal const int Heat = 0xB26B4E;
            internal const int Cold = 0x80929B;
            internal const int Plum = 0x6B5269;
            private static readonly Dictionary<int, Color32[]> Stages = new();
            private readonly Color32[] pixels;

            internal Scene(Color32[] pixels, int horizon = 46)
            {
                this.pixels = pixels;
                if (!Stages.TryGetValue(horizon, out Color32[] stage))
                {
                    PaintStage(horizon);
                    Stages.Add(horizon, (Color32[])pixels.Clone());
                    return;
                }
                Array.Copy(stage, pixels, stage.Length);
            }

            private void PaintStage(int horizon)
            {
                // Same native pixel pitch and stamped bezel as the spectrometer.
                Rect(0, 0, Width, Height, 0x111417);
                Rect(1, 1, 108, 56, 0x353A3C);
                Rect(2, 1, 106, 1, 0x60635F);
                Rect(2, 55, 106, 2, 0x24292C);
                Rect(4, 4, 102, 48, 0x101619);
                Rect(5, 5, 100, horizon - 5, 0x171C20);
                Rect(5, horizon, 100, 51 - horizon, 0x252D31);
                Rect(5, horizon, 100, 1, 0x34403F);
                Rect(4, 51, 102, 1, 0x444E4B);
                Dot(3, 3, 0x8A8272);
                Dot(106, 3, 0x8A8272);
                Dot(3, 54, 0x686458);
                Dot(106, 54, 0x686458);
            }

            internal static int Mix(int a, int b, float amount) => Blend(a, b, Mathf.Clamp01(amount));
            internal static int Scale(float progress, int size) => Mathf.RoundToInt(Mathf.Clamp01(progress) * size);
            internal static int Sample(Look sample, int fallback) => sample.Form == Specimen.Empty ? fallback : sample.Tint;
            internal static float Fill(Look sample) => sample.Form == Specimen.Liquid ? sample.Fill : 0f;

            internal void Dot(int x, int y, int color)
            {
                if ((uint)x < Width && (uint)y < Height)
                    Point(pixels, x, Height - 1 - y, color);
            }

            internal void Rect(int x, int y, int w, int h, int color)
            {
                int left = Math.Max(0, x), right = Math.Min(Width, x + w);
                int top = Math.Max(0, y), bottom = Math.Min(Height, y + h);
                if (left < right && top < bottom)
                    Box(pixels, left, Height - bottom, right - left, bottom - top, color);
            }

            internal void Line(int x, int y, int endX, int endY, int color)
            {
                FineLine(x * 2, y * 2, endX * 2, endY * 2, color);
            }

            private void FineDot(int x, int y, Color32 color)
            {
                x >>= 1;
                y >>= 1;
                if ((uint)x < Width && (uint)y < Height)
                    pixels[(Height - 1 - y) * Width + x] = color;
            }

            private void FineLine(int x, int y, int endX, int endY, int color)
            {
                Color32 ink = Ink(color);
                int dx = Math.Abs(endX - x), dy = -Math.Abs(endY - y);
                int sx = x < endX ? 1 : -1, sy = y < endY ? 1 : -1;
                int error = dx + dy;
                while (true)
                {
                    FineDot(x, y, ink);
                    if (x == endX && y == endY) return;
                    int twice = error * 2;
                    if (twice >= dy) { error += dy; x += sx; }
                    if (twice <= dx) { error += dx; y += sy; }
                }
            }

            internal void Oval(int x, int y, int rx, int ry, int color)
            {
                int halfHeight = ry * 2;
                int halfWidth = rx * 2;
                for (int row = -halfHeight; row <= halfHeight; row++)
                {
                    int half = Mathf.RoundToInt(halfWidth * Mathf.Sqrt(1f - (float)(row * row) / (halfHeight * halfHeight)));
                    FineRow(x * 2 - half, y * 2 + row, half * 2 + 1, color);
                }
            }

            private void FineRow(int x, int y, int length, int color)
            {
                int left = Math.Max(0, x >> 1), right = Math.Min(Width, (x + length + 1) >> 1);
                y >>= 1;
                if (length > 0 && (uint)y < Height && left < right)
                    Array.Fill(pixels, Ink(color), (Height - 1 - y) * Width + left, right - left);
            }

            internal void Panel(int x, int y, int w, int h, int color)
            {
                Rect(x, y, w, h, Shadow);
                Rect(x + 1, y + 1, w - 2, h - 2, color);
                Rect(x + 1, y + 1, w - 2, 1, Mix(color, Rim, 0.62f));
                Rect(x + 1, y + h - 2, w - 2, 1, Mix(color, Shade, 0.7f));
            }

            internal void Housing(int x, int y, int w, int h, int depth, int color)
            {
                color = Mix(color, 0x3D4649, 0.43f);
                int side = Mix(color, Shade, 0.72f);
                for (int offset = depth; offset > 0; offset--)
                    Rect(x + offset, y - offset, w, h, side);
                for (int row = 1; row <= depth; row++)
                    Rect(x + row, y - row, w - 1, 1, Mix(color, Rim, 0.55f));
                Rect(x + depth, y - depth, w - 1, 1, Mix(color, Ivory, 0.47f));
                Panel(x, y, w, h, color);
                Rect(x + 2, y + h - 3, w - 4, 1, Mix(color, Shade, 0.67f));
                if (w >= 30 && h >= 12)
                {
                    Fastener(x + 3, y + 3);
                    Fastener(x + w - 4, y + 3);
                    Fastener(x + 3, y + h - 4);
                    Fastener(x + w - 4, y + h - 4);
                }
                if (h >= 22)
                {
                    Rect(x + w - 14, y + h - 8, 10, 5, 0x252D31);
                    for (int slot = 0; slot < 3; slot++)
                        Rect(x + w - 13 + slot * 3, y + h - 7, 1, 3, Metal);
                }
            }

            internal void Fastener(int x, int y)
            {
                Dot(x, y, Shade);
                Dot(x, y - 1, Rim);
            }

            internal void Etch(int x, int y, int length, int color) =>
                FineRow(x * 2, y * 2 + 1, length * 2, color);

            internal void Highlight(int x, int y, int endX, int endY, int color) =>
                FineLine(x * 2, y * 2 + 1, endX * 2, endY * 2 + 1, color);

            // A tapered shaft is shaded across its axis. Drawing it directly between two
            // joints keeps the lighting attached to the part while the mechanism moves.
            internal void Shaft(float x, float y, float endX, float endY, float radius, float tip, int enamel)
            {
                float dx = endX - x, dy = endY - y;
                float length = Mathf.Sqrt(dx * dx + dy * dy);
                if (length < 0.01f) return;
                float nx = -dy / length, ny = dx / length;
                int bands = Mathf.CeilToInt(Mathf.Max(radius, tip) * 5f);
                for (int i = 0; i <= bands; i++)
                {
                    float across = i * 2f / bands - 1f;
                    float normal = Mathf.Sqrt(Mathf.Max(0f, 1f - across * across));
                    float light = Mathf.Clamp01(normal * 0.70f - across * (nx + ny) * 0.40f);
                    int ink = light < 0.15f ? Shade : light < 0.43f ? Mix(enamel, Shade, 0.55f)
                        : light < 0.8f ? enamel : Mix(enamel, Ivory, 0.48f);
                    FineLine(Mathf.RoundToInt((x + nx * across * radius) * 2),
                        Mathf.RoundToInt((y + ny * across * radius) * 2),
                        Mathf.RoundToInt((endX + nx * across * tip) * 2),
                        Mathf.RoundToInt((endY + ny * across * tip) * 2), ink);
                }
            }

            internal void Dial(int x, int y, int radius, float reading)
            {
                Oval(x, y + 1, radius + 1, radius, Shadow);
                Oval(x, y, radius, radius, 0x79746E);
                Oval(x, y, radius - 1, radius - 1, 0xC4C4B0);
                for (int tick = 0; tick < 7; tick++)
                {
                    float a = 2.6f + tick * 0.70f;
                    int tx = x + Mathf.RoundToInt(Mathf.Cos(a) * (radius - 2));
                    int ty = y + Mathf.RoundToInt(Mathf.Sin(a) * (radius - 2));
                    Dot(tx, ty, Shade);
                }
                float angle = 2.6f + Mathf.Clamp01(reading) * 4.2f;
                Shaft(x, y, x + Mathf.Cos(angle) * (radius - 2), y + Mathf.Sin(angle) * (radius - 2),
                    0.9f, 0.35f, 0xA14034);
                Dot(x, y, Shadow);
            }

            internal void Arc(int x, int y, int rx, int ry, float start, float end, int color)
            {
                int segments = Math.Max(3, Mathf.CeilToInt(Mathf.Abs(end - start) * rx));
                int previousX = Mathf.RoundToInt((x + rx * Mathf.Cos(start)) * 2);
                int previousY = Mathf.RoundToInt((y + ry * Mathf.Sin(start)) * 2);
                for (int i = 1; i <= segments; i++)
                {
                    float angle = Mathf.Lerp(start, end, i / (float)segments);
                    int nextX = Mathf.RoundToInt((x + rx * Mathf.Cos(angle)) * 2);
                    int nextY = Mathf.RoundToInt((y + ry * Mathf.Sin(angle)) * 2);
                    FineLine(previousX, previousY, nextX, nextY, color);
                    previousX = nextX;
                    previousY = nextY;
                }
            }

            internal void Granule(int x, int y, int radius, int color)
            {
                Oval(x + 1, y + 1, radius, radius, Mix(color, Shadow, 0.62f));
                Diamond(x, y, radius, color);
                FineRow(x * 2, y * 2 - 1, radius + 1, Mix(color, Ivory, 0.52f));
            }

            internal void Vapor(int x, int y, int rise, int time, int color)
            {
                for (int row = 0; row < rise * 2; row++)
                {
                    int offset = Mathf.RoundToInt(Mathf.Sin(row * 0.52f + time * 0.33f) * row / 9f);
                    FineRow(x * 2 + offset, y * 2 - row, 2, Mix(color, Shade, row / (rise * 2.4f)));
                }
            }

            internal void Ripple(int x, int y, int age, int color)
            {
                int radius = 2 + age;
                Arc(x, y, radius, 1 + age / 4, 0.20f, 2.94f, Mix(color, Shade, age / 12f));
                Arc(x, y, radius, 1 + age / 4, 3.45f, 5.10f, Mix(color, Ivory, 0.31f));
            }

            internal void Lamp(int x, int y, int color)
            {
                Rect(x - 1, y - 1, 5, 4, Shadow);
                Rect(x, y, 3, 2, Mix(color, Shadow, 0.35f));
                Etch(x, y, 3, color);
                FineRow(x * 2 + 1, y * 2, 4, Mix(color, Ivory, 0.45f));
            }

            internal void Knob(int x, int y, int phase, int color)
            {
                Oval(x, y, 4, 4, Shadow);
                Oval(x, y - 1, 3, 3, Rim);
                Oval(x, y, 3, 3, Metal);
                double angle = phase * Math.PI / 8;
                FineLine(x * 2, y * 2, x * 2 + (int)Math.Round(Math.Sin(angle) * 5), y * 2 - (int)Math.Round(Math.Cos(angle) * 5), color);
                FineRow(x * 2 - 1, y * 2 + 4, 3, Mix(Metal, Shadow, 0.45f));
            }

            internal void Stand(int x, int top, int bottom, int reach, int clamp)
            {
                Oval(x + 3, bottom + 2, 15, 3, Shadow);
                Rect(x - 9, bottom - 1, 22, 3, Metal);
                Rect(x, top, 3, bottom - top, Shade);
                Rect(x, top, 1, bottom - top, Rim);
                Panel(Math.Min(x, reach), clamp, Math.Abs(x - reach) + 2, 4, Metal);
            }

            internal void Glass(int x, int y, int w, int h, int tint, float fill)
            {
                Rect(x + 1, y + 1, w - 2, h - 1, 0x213031);
                int liquid = Scale(fill, (h - 3) * 2);
                if (liquid > 0)
                {
                    int surface = (y + h) * 2 - liquid;
                    for (int row = 0; row < liquid; row++)
                        FineRow((x + 1) * 2, surface + row, (w - 2) * 2, Mix(tint, Shade, row >= liquid * 3 / 4 ? 0.47f : 0.22f));
                    FineRow((x + 1) * 2 + 1, surface, (w - 2) * 2 - 2, Mix(tint, Ivory, 0.51f));
                    FineDot((x + 1) * 2, surface - 1, Ink(Aqua));
                    FineDot((x + w - 2) * 2, surface - 1, Ink(Mix(Aqua, Metal, 0.42f)));
                }
                Rect(x, y + 1, 1, h - 1, 0x506664);
                Rect(x + w - 1, y + 1, 1, h - 1, Metal);
                Rect(x + 1, y + h, w - 2, 1, Rim);
                FineLine(x * 2 + 4, y * 2 + 5, x * 2 + 4, (y + h - 2) * 2, Mix(Aqua, Shade, 0.4f));
                FineLine(x * 2 + 5, y * 2 + 5, x * 2 + 5, y * 2 + 10, 0xA8AEA5);
                FineLine((x + w) * 2 - 5, y * 2 + 5, (x + w) * 2 - 5, (y + h) * 2 - 3, 0x3C5050);
                Rect(x + 2, y + 2, 1, Math.Min(4, h - 2), 0xA8AEA5);
                FineRow((x + 1) * 2, (y + h) * 2 - 1, (w - 2) * 2, Mix(Aqua, Shade, 0.5f));
                Rect(x - 1, y, w + 2, 1, Rim);
                FineRow((x + 1) * 2, y * 2 + 1, (w - 2) * 2, 0x38545A);
            }

            internal void Diamond(int x, int y, int radius, int color)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int half = radius - Math.Abs(dy);
                    Rect(x - half, y + dy, half * 2 + 1, 1, color);
                }
                Dot(x, y - radius, Mix(color, Ivory, 0.6f));
            }

            internal void Drip(int x, int y, int color)
            {
                FineDot(x * 2, y * 2 - 3, Ink(color));
                FineRow(x * 2 - 1, y * 2 - 1, 3, color);
                FineRow(x * 2 - 2, y * 2, 5, Mix(color, Shade, 0.20f));
                FineRow(x * 2 - 1, y * 2 + 1, 3, color);
                FineDot(x * 2 - 1, y * 2, Ink(Mix(color, Ivory, 0.62f)));
            }

            internal void Pour(int x, int nozzle, int surface, float progress, int color)
            {
                float falling = Mathf.Clamp01(progress / 0.62f);
                if (progress < 0.62f)
                {
                    int y = nozzle + Scale(falling * falling, Math.Max(0, surface - nozzle));
                    Drip(x, y, color);
                    if (falling > 0.35f) Highlight(x, y - 4, x, y - 2, Mix(color, Aqua, 0.5f));
                    return;
                }
                float settling = (progress - 0.62f) / 0.38f;
                Ripple(x, surface, Scale(settling, 7), Mix(color, Shade, settling * 0.62f));
                if (settling < 0.45f)
                {
                    Etch(x - 4, surface - 2, 1, Ivory);
                    Etch(x + 4, surface - 2, 1, Ivory);
                }
            }

            internal void Wave(int x, int y, int length, float time, int color)
            {
                int previous = y * 2 + Mathf.RoundToInt(4f * Mathf.Sin(-time));
                for (int i = 1; i < length * 2; i++)
                {
                    int next = y * 2 + Mathf.RoundToInt(4f * Mathf.Sin(i * 0.24f - time));
                    FineLine(x * 2 + i - 1, previous, x * 2 + i, next, color);
                    previous = next;
                }
            }

            internal void Display(int x, int y, int w, int h)
            {
                Rect(x, y, w, h, 0x101619);
                Rect(x + 1, y + 1, w - 2, h - 2, 0x121F20);
                Rect(x + 1, y + 1, w - 2, 1, 0x444E4B);
                for (int row = y + 4; row < y + h - 2; row += 4)
                    Rect(x + 2, row, w - 4, 1, 0x203031);
                for (int col = x + 5; col < x + w - 2; col += 5)
                    Rect(col, y + 2, 1, h - 4, 0x203031);
            }
        }
    }
}
