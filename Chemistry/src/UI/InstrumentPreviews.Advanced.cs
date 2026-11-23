using UnityEngine;
using static Chemistry.InstrumentAnimPreview.Scene;

namespace Chemistry
{
    internal sealed class RotaryEvaporatorPreview : InstrumentAnimPreview
    {
        internal RotaryEvaporatorPreview() : base("Chemistry Rotary Evaporator Preview") { }
        protected override string SpriteName => "chem_anim_rotary_evaporator";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            int tint = Sample(look, Gold);
            bool running = phase == Phase.Run;
            s.Housing(17, 45, 75, 7, 3, Metal);
            s.Shaft(56, 43, 56, 7, 5, 3, 0x958465);
            s.Shaft(63, 13, 43, 23, 5, 4, 0xA38C7E);
            s.Shaft(44, 22, 36, 28, 3, 2, 0x70979B);
            s.Rect(51, 15, 9, 3, phase == Phase.Done ? Aqua : Rim);
            s.Highlight(44, 21, 36, 27, Aqua);
            s.Shaft(63, 15, 74, 20, 1.3f, 1.3f, Rim);
            s.Oval(30, 32, 12, 10, Rim);
            s.Oval(30, 32, 10, 8, 0x324650);
            if (Fill(look) > 0f)
            {
                int depth = 2 + Scale(Fill(look) * (1f - progress), 2);
                s.Oval(30, 35, 8, depth, Mix(tint, Shade, 0.22f));
                s.Arc(30, 35, 8, depth, 3.18f, 6.25f, Mix(tint, Ivory, 0.58f));
            }
            s.Arc(30, 32, 11, 9, 3.28f, 4.98f, 0xB5D3CE);
            if (running)
            {
                float angle = step * (0.30f + level * 0.16f);
                s.Arc(30, 32, 9, 6, angle - 0.75f, angle, Gold);
                s.Arc(30, 32, 8, 5, angle + 2.1f, angle + 2.8f, 0x91C6C5);
            }
            s.Oval(30, 40, 15, 5, running ? Heat : 0x8B6662);
            s.Arc(30, 40, 15, 4, 3.57f, 5.82f, running ? Gold : Metal);
            s.Panel(16, 40, 29, 7, 0x79615B);
            s.Glass(74, 8, 13, 26, Cold, 0.21f);
            s.Highlight(84, 10, 84, 30, 0xB4DFE2);
            for (int i = 0; i < 5; i++)
            {
                s.Highlight(77, 13 + i * 4, 83, 11 + i * 4, Cold);
                s.Highlight(83, 12 + i * 4, 77, 16 + i * 4, 0x5F8699);
            }
            s.Shaft(81, 7, 81, 11, 9, 7, 0xA78966);
            s.Shaft(86, 12, 95, 9, 1.8f, 1.4f, 0x885451);
            s.Shaft(95, 9, 97, 19, 1.4f, 1.4f, 0x885451);
            s.Panel(72, 31, 17, 4, 0x76857A);
            s.Glass(73, 36, 15, 10, Cold, progress * 0.72f);
            if (running)
            {
                int age = step % 8;
                if (age < 5) s.Drip(80, 33 + age * age / 2, Cold);
                else s.Ripple(80, 46 - Scale(progress * 0.72f, 7), age - 4, Cold);
                s.Lamp(55, 17, Cold);
            }
        }
    }

    internal sealed class FreezeDryerPreview : InstrumentAnimPreview
    {
        internal FreezeDryerPreview() : base("Chemistry Freeze Dryer Preview") { }
        protected override string SpriteName => "chem_anim_freeze_dryer";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            int tint = Sample(look, Cold);
            s.Housing(14, 13, 79, 36, 4, 0x5F7486);
            s.Panel(20, 18, 43, 23, Shade);
            s.Rect(23, 22, 37, 16, 0x333D54);
            s.Line(23, 33, 59, 33, Rim);
            s.Etch(24, 22, 35, 0x7B97AB);
            for (int x = 25; x < 59; x += 4) s.Etch(x, 33, 2, 0x3A4C64);
            if (look.Form != Specimen.Empty)
                for (int i = 0; i < 6; i++)
                {
                    int x = 26 + i * 6;
                    float dry = Mathf.Clamp01((progress - i * 0.045f) * 1.4f);
                    s.Granule(x, 31, 3 - Scale(dry, 2), Mix(Cold, tint, dry));
                    if (dry < 0.8f) s.Highlight(x - 2, 31, x + 1, 28, Cold);
                }
            s.Glass(73, 20, 12, 22, Cold, progress * 0.59f);
            s.Shaft(63, 28, 73, 28, 2, 1.4f, 0xB2926B);
            s.Dial(67, 17, 5, Mathf.Sin(progress * Mathf.PI));
            s.Line(67, 29, 67, 34, Rim);
            for (int y = 24; y < 40; y += 3) s.Etch(81, y, 4, Rim);
            if (phase == Phase.Run)
            {
                for (int i = 0; i < 4; i++)
                    s.Vapor(28 + (i * 17 + step / 2) % 28, 31 - (step + i * 5) % 6, 3, step + i * 5, Cold);
                s.Drip(78, 26 + step % 12, Aqua);
            }
            s.Lamp(25, 44, phase == Phase.Done ? Aqua : phase == Phase.Run ? Cold : Metal);
        }
    }

    internal sealed class AutoclavePreview : InstrumentAnimPreview
    {
        internal AutoclavePreview() : base("Chemistry Autoclave Preview") { }
        protected override string SpriteName => "chem_anim_autoclave";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            bool running = phase == Phase.Run;
            s.Housing(19, 19, 66, 30, 5, 0x686D63);
            s.Oval(48, 29, 23, 19, Rim);
            s.Oval(48, 29, 21, 17, Shade);
            s.Oval(48, 29, 17, 13, 0x35464D);
            s.Arc(48, 29, 22, 18, 3.18f, 5.79f, Ivory);
            s.Arc(48, 29, 19, 15, 0.2f, 2.94f, Rim);
            s.Fastener(48, 13);
            s.Fastener(48, 44);
            s.Line(34, 33, 61, 33, Metal);
            s.Glass(39, 23, 7, 10, Aqua, 0f);
            s.Glass(49, 23, 7, 10, Aqua, 0f);
            if (running)
            {
                int density = 3 + level;
                for (int i = 0; i < density; i++)
                {
                    int x = 36 + (i * 13 + step / 3) % 23;
                    int y = 34 - (step + i * 3) % 11;
                    s.Vapor(x, y, 3 + i % 2, step + i * 6, Mix(Rim, Ivory, (34 - y) / 13f));
                }
            }
            float seal = Mathf.SmoothStep(0f, 1f, progress * 7f)
                * Mathf.SmoothStep(0f, 1f, (1f - progress) * 6f);
            s.Shaft(30, 30, 35 + seal * 7, 23 + seal * 7, 2.8f, 1.8f, 0xAC936E);
            s.Fastener(30, 30);
            s.Dial(78, 26, 6, Mathf.Sin(Mathf.PI * progress) * (0.55f + level * 0.14f));
            s.Lamp(74, 39, phase == Phase.Done ? Aqua : running ? Heat : Metal);
        }
    }

    internal sealed class GloveboxPreview : InstrumentAnimPreview
    {
        internal GloveboxPreview() : base("Chemistry Glovebox Preview") { }
        protected override string SpriteName => "chem_anim_glovebox";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            int tint = Sample(look, Gold);
            s.Housing(14, 13, 79, 37, 4, 0x626A76);
            s.Panel(19, 16, 67, 26, Metal);
            s.Rect(22, 19, 61, 20, 0x293643);
            s.Rect(23, 20, 59, 2, 0x658184);
            s.Highlight(26, 29, 35, 21, 0x567581);
            s.Highlight(27, 33, 41, 21, 0x415D6B);
            float reach = Mathf.SmoothStep(0f, 1f, progress * 2.4f)
                * (1f - Mathf.SmoothStep(0f, 1f, (progress - 0.71f) * 4f));
            int distance = 10 - Scale(reach, 7);
            s.Panel(49, 31, 10, 7, 0x6E5760);
            if (look.Form != Specimen.Empty) s.Granule(54, 30, 3, tint);
            for (int side = -1; side <= 1; side += 2)
            {
                int port = 54 + side * 18;
                int hand = 54 + side * distance;
                int wristY = 28 + Scale(reach, 3);
                s.Shaft(port, 38, hand, wristY, 3, 2, 0x9D826B);
                s.Oval(hand, wristY, 3, 2, 0xB49370);
                s.Shaft(hand, wristY, hand - side * 3, wristY - 3 + reach * 2, 1.5f, 0.8f, Gold);
                s.Shaft(hand, wristY + 1, hand - side * 4, wristY + 2 - reach, 1.3f, 0.7f, Gold);
                s.Oval(port, 39, 6, 5, Rim);
                s.Oval(port, 39, 4, 3, Shade);
                s.Arc(port, 39, 5, 4, 3.1f, 5.8f, Ivory);
            }
            if (phase == Phase.Run && progress > 0.54f) s.Rect(50, 31, 8, 1, step % 4 < 2 ? Ivory : Gold);
            if (phase == Phase.Done) s.Rect(50, 31, 8, 1, tint);
            s.Lamp(77, 45, phase == Phase.Done ? Aqua : Metal);
        }
    }

    internal sealed class HotplateStirrerPreview : InstrumentAnimPreview
    {
        internal const int SolidAdded = 0x10;

        internal HotplateStirrerPreview() : base("Chemistry Hotplate Stirrer Preview") { }
        protected override string SpriteName => "chem_anim_hotplate_stirrer";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer, 44);
            int tint = Sample(look, Aqua);
            bool running = phase == Phase.Run;
            float fill = Fill(look);
            s.Housing(24, 39, 56, 12, 6, 0x745C63);
            s.Oval(52, 38, 24, 5, Shade);
            s.Oval(52, 37, 21, 4, running ? Mix(Metal, Heat, level * 0.28f) : Metal);
            s.Arc(52, 37, 21, 4, 0.14f, 2.99f, running ? Heat : Rim);
            s.Glass(36, 10, 33, 25, tint, fill);
            if (fill > 0f)
            {
                int surface = 35 - Scale(fill, 22);
                int stir = running ? step * (level + 1) : 0;
                if (running)
                {
                    float drive = Mathf.Sin(Mathf.PI * progress) * (level + 1);
                    for (int col = -13; col <= 13; col++)
                    {
                        float bowl = Mathf.Max(0f, 1f - Mathf.Abs(col) / 10f);
                        int dip = Mathf.Min(32 - surface, Mathf.RoundToInt(drive * bowl * bowl));
                        if (dip > 0) s.Rect(52 + col, surface, 1, dip, 0x2E4953);
                        s.Dot(52 + col, surface + Mathf.Max(0, dip), Mix(tint, Ivory, 0.48f));
                    }
                    s.Arc(52, surface + 2, 8, 2, stir * 0.18f, stir * 0.18f + 2.4f, Mix(tint, Ivory, 0.34f));
                }
                int bar = running ? 2 + Mathf.RoundToInt(Mathf.Abs(Mathf.Cos(stir * 0.28f)) * 7) : 8;
                s.Rect(52 - bar / 2, 33, bar, 1, Ivory);
                if (running && (effects & SolidAdded) != 0 && progress < 0.24f)
                    s.Granule(54, 8 + Scale(progress / 0.24f, 24), 2, Gold);
                if (running && (effects & 0x9) != 0)
                    for (int i = 0; i < 4; i++) s.Dot(41 + i * 7, 33 - (stir + i * 6) % Mathf.Max(1, 33 - surface), Aqua);
                if (progress > 0.46f && (effects & 0x2) != 0)
                    for (int i = 0; i < 7; i++) s.Dot(40 + i * 4, 34 - i % 2, Mix(tint, Ivory, 0.65f));
            }
            s.Knob(66, 45, level * 2, Gold);
            s.Lamp(37, 44, phase == Phase.Done ? Aqua : running ? Heat : Metal);
        }
    }
}
