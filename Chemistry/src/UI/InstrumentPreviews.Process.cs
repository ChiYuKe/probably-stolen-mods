using UnityEngine;
using static Chemistry.InstrumentAnimPreview.Scene;

namespace Chemistry
{
    internal sealed class CentrifugePreview : InstrumentAnimPreview
    {
        internal CentrifugePreview() : base("Chemistry Centrifuge Preview") { }
        protected override string SpriteName => "chem_anim_centrifuge";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer, 41);
            int tint = Sample(look, Gold);
            bool running = phase == Phase.Run;
            s.Oval(52, 50, 39, 4, Shadow);
            s.Shaft(53, 25, 53, 46, 34, 40, 0xACA58E);
            s.Housing(18, 38, 70, 11, 2, 0x544E44);
            s.Panel(27, 6, 47, 5, Metal);
            s.Oval(53, 23, 35, 17, 0x434947);
            s.Oval(53, 22, 34, 16, 0x9B917B);
            s.Oval(53, 22, 31, 14, 0x101A23);
            s.Oval(53, 22, 27, 12, running ? 0x28475A : 0x313743);
            float speed = Mathf.Clamp01(progress * 5f) * Mathf.Clamp01((1f - progress) * 5f);
            // 积分后的速度曲线，起停减速时转子仍沿同一方向转动。
            float travel = progress < 0.2f ? progress * progress * 2.5f
                : progress < 0.8f ? progress - 0.1f
                : 0.8f - (1f - progress) * (1f - progress) * 2.5f;
            float rotation = 0.6f + travel * (23f + level * 9f);
            s.Arc(53, 23, 29, 14, 3.19f, 5.91f, Ivory);
            s.Arc(53, 23, 25, 12, 0.16f, 2.89f, 0x7C9EA0);
            s.Arc(53, 22, 34, 16, 3.45f, 5.20f, 0xE5CDA5);
            s.Arc(53, 22, 32, 15, 0.22f, 2.75f, running ? 0x6CBFC9 : 0x4C717D);
            if (running && speed > 0.3f)
                for (int i = 0; i < 3; i++)
                {
                    float angle = rotation + i * 2f * Mathf.PI / 3f - 0.7f;
                    s.Arc(53, 23, 21, 9, angle - speed * 0.55f, angle, Mix(Plum, Cold, 0.36f));
                }
            for (int i = 0; i < 6; i++)
            {
                float angle = rotation + i * Mathf.PI / 3f;
                int x = 53 + Mathf.RoundToInt(Mathf.Cos(angle) * 22f);
                int y = 23 + Mathf.RoundToInt(Mathf.Sin(angle) * 9f);
                float ux = Mathf.Cos(angle), uy = Mathf.Sin(angle) * 0.46f;
                s.Shaft(53 + ux * 4, 23 + uy * 4, x, y, 2.5f, 4f, 0x76797A);
                s.Oval(x, y, 3, 2, Shadow);
                if (look.Form == Specimen.Empty) continue;
                s.Shaft(x - ux * 9, y - uy * 9, x, y, 1.6f, 2.8f, Mix(tint, Cold, 0.37f));
                s.Shaft(x - ux * 2, y - uy * 2, x + ux * 1.7f, y + uy * 1.7f, 3, 3,
                    i % 2 == 0 ? 0xAA5146 : 0xD3A668);
                if (progress > 0.65f) s.Etch(x + 1, y, 2, Mix(tint, Gold, 0.52f));
                s.Etch(x - 2, y - 2, 3, 0xECF1D9);
            }
            s.Oval(53, 23, 6, 4, Metal);
            s.Arc(53, 23, 5, 3, 3.2f, 5.72f, Rim);
            s.Diamond(53, 22, 2, running ? Aqua : Rim);
            // The smoked lid closes over the rotor and opens again after braking.
            float closed = Mathf.SmoothStep(0f, 1f, progress / 0.13f)
                * Mathf.SmoothStep(0f, 1f, (1f - progress) / 0.10f);
            int lidDepth = 2 + Scale(closed, 13);
            int lidY = 7 + lidDepth;
            s.Arc(53, lidY, 33, lidDepth, 0.04f, 3.10f, 0xABBABC);
            s.Arc(53, lidY, 31, lidDepth, 0.28f, 1.52f, 0x70888B);
            s.Highlight(33, lidY - 2, 43, lidY - 5, 0x8BA3A4);
            s.Panel(49, lidY + lidDepth - 1, 8, 3, Metal);
            s.Dial(76, 43, 6, speed);
            s.Lamp(31, 42, running || phase == Phase.Done ? Aqua : Metal);
            for (int bar = 0; bar < 6; bar++)
            {
                int y = 44 - bar / 2;
                s.Rect(39 + bar * 3, y, 2, 45 - y, speed * 6f > bar ? Aqua : 0x354550);
            }
        }
    }

    internal sealed class DistillerPreview : InstrumentAnimPreview
    {
        internal DistillerPreview() : base("Chemistry Distiller Preview") { }
        protected override string SpriteName => "chem_anim_distiller";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            int tint = Sample(look, Cold);
            s.Housing(16, 45, 37, 6, 3, Metal);
            s.Oval(36, 34, 14, 12, Rim);
            s.Oval(36, 34, 12, 10, Shade);
            s.Arc(36, 34, 12, 10, 3.32f, 4.52f, 0xABD2CA);
            s.Arc(36, 34, 13, 11, 0.18f, 2.64f, 0x557D87);
            int liquid = Scale(Fill(look) * (1f - 0.75f * progress), 7);
            if (liquid > 0) s.Oval(36, 40 - liquid / 2, 9 + liquid / 4, 2 + liquid / 2, tint);
            s.Panel(32, 13, 8, 10, Metal);
            s.Line(36, 13, 36, 9, Aqua);
            s.Line(36, 9, 51, 9, Aqua);
            s.Line(51, 9, 56, 14, Aqua);
            for (int row = 0; row < 8; row++)
                s.Line(52, 13 + row, 82, 24 + row, row is 0 or 7 ? Rim : 0x3B6370);
            s.Shaft(54, 17, 82, 27, 2, 1.6f, 0x78B0BB);
            for (int joint = 0; joint < 5; joint++)
                s.Highlight(55 + joint * 5, 15 + joint * 2, 54 + joint * 5, 20 + joint * 2, 0xC8D8C9);
            s.Highlight(55, 14, 80, 23, 0xA1C4D3);
            s.Highlight(56, 20, 79, 28, 0x26404D);
            s.Panel(52, 14, 3, 7, Metal);
            s.Panel(81, 24, 3, 8, Metal);
            s.Line(82, 27, 82, 35, Cold);
            s.Glass(72, 37, 22, 12, Cold, progress * 0.65f);
            if (phase == Phase.Run)
            {
                float cycle = Mathf.Clamp01((progress - 0.17f) / 0.75f);
                int parcel = Scale(cycle * (3 + level) % 1f, 28);
                s.Highlight(53 + parcel, 17 + parcel * 10 / 28, 55 + parcel, 17 + parcel * 10 / 28, Ivory);
                int age = step % 9;
                if (age < 6) s.Drip(82, 35 + age * age / 4, Cold);
                else s.Ripple(82, 48 - Scale(progress, 6), age - 5, Cold);
                for (int i = 0; i < 3; i++)
                    s.Arc(31 + i * 4, 38 - (step + i * 5) % 10, 1 + i % 2, 1, 3.1f, 6.25f, Mix(tint, Ivory, 0.45f));
            }
            s.Lamp(24, 47, phase == Phase.Run ? Heat : Metal);
        }
    }

    internal sealed class SeparatingFunnelPreview : InstrumentAnimPreview
    {
        internal SeparatingFunnelPreview() : base("Chemistry Separating Funnel Preview") { }
        protected override string SpriteName => "chem_anim_separating_funnel";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            int tint = Sample(look, Aqua);
            int upper = Mix(tint, Gold, 0.56f);
            int lower = Mix(tint, Cold, 0.52f);
            s.Stand(24, 7, 48, 54, 15);
            s.Panel(51, 7, 8, 5, Rim);
            // The round shoulder catches the light above a tapered lower chamber.
            int interfaceY = 24 + Scale(progress / 0.43f, 10);
            for (int y = 12; y <= 35; y++)
            {
                int half = y < 18 ? Mathf.RoundToInt(4f + 10f * Mathf.Sin((y - 12) * 0.26f))
                    : Mathf.Max(2, 14 - (y - 18) * 3 / 4);
                int color = y > interfaceY ? lower : upper;
                if (y < 16 + Scale(progress, 15) || look.Form == Specimen.Empty) color = 0x213031;
                s.Rect(55 - half, y, half * 2 + 1, 1, Mix(color, Shade, 0.28f));
                s.Dot(55 - half, y, Aqua);
                s.Dot(55 + half, y, Metal);
            }
            s.Panel(51, 34, 9, 4, Gold);
            s.Fastener(55, 36);
            s.Highlight(48, 15, 44, 18, Rim);
            s.Highlight(44, 19, 46, 23, Rim);
            if (progress < 0.43f && look.Form != Specimen.Empty)
            {
                int boundary = 24 + Scale(progress / 0.43f, 10);
                int half = Mathf.Max(1, 13 - (boundary - 18) * 3 / 4);
                s.Etch(55 - half, boundary, half * 2, Mix(lower, Ivory, 0.5f));
            }
            s.Rect(55, 38, 1, 4, Aqua);
            int slide = Scale(Mathf.SmoothStep(0f, 1f, (progress - 0.43f) * 5f), 23);
            s.Glass(47 - slide, 42, 17, 9, lower, Mathf.Min(progress * 1.5f, 0.7f));
            s.Glass(70 - slide, 42, 17, 9, upper, Mathf.Max(0f, progress - 0.5f) * 1.35f);
            if (phase == Phase.Run && (progress < 0.43f || progress > 0.63f))
            {
                int age = step % 8;
                int liquid = progress < 0.43f ? lower : upper;
                if (age < 5) s.Drip(55, 41 + age * age / 3, liquid);
                else s.Ripple(55, 47, age - 4, liquid);
            }
        }
    }

    internal sealed class HeatingMantlePreview : InstrumentAnimPreview
    {
        internal HeatingMantlePreview() : base("Chemistry Heating Mantle Preview") { }
        protected override string SpriteName => "chem_anim_heating_mantle";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer, 44);
            int tint = Sample(look, Cold);
            s.Oval(53, 51, 29, 3, Shadow);
            s.Panel(49, 7, 8, 6, Rim);
            s.Glass(49, 9, 8, 15, tint, 0f);
            s.Oval(53, 29, 15, 14, Aqua);
            s.Oval(53, 29, 13, 12, Shade);
            s.Arc(53, 29, 13, 12, 3.25f, 4.60f, 0xC9E5DF);
            if (Fill(look) > 0f) s.Oval(53, 32, 12, 6, Mix(tint, Shade, 0.30f));
            s.Shaft(53, 34, 53, 42, 22, 17, 0x9A7960);
            for (int stitch = -8; stitch <= 8; stitch++)
                s.Highlight(53 + stitch * 2, 35, 51 + stitch * 2, 39, 0x594B47);
            s.Arc(53, 34, 21, 4, 0.05f, 3.1f, Rim);
            s.Arc(53, 37, 20, 5, 3.24f, 5.95f, phase == Phase.Run ? Heat : Rim);
            s.Panel(32, 37, 43, 12, 0x6D5550);
            s.Rect(37, 39, 32, 2, Mix(Shade, Heat, phase == Phase.Run ? level * 0.27f : 0.11f));
            for (int vent = 0; vent < 8; vent++)
                s.Highlight(45 + vent * 2, 43, 45 + vent * 2, 46, 0x3F383A);
            s.Dial(64, 45, 5, Scale(progress * 2, level) / 3f);
            if (phase == Phase.Run)
                for (int i = 0; i < 2 + level; i++)
                {
                    int y = 34 - (step + i * 3) % 10;
                    s.Arc(46 + i * 4, y, 1 + (34 - y) / 5, 1, 3.2f, 6.25f, Aqua);
                }
            s.Lamp(40, 44, phase == Phase.Done ? Aqua : phase == Phase.Run ? Gold : Metal);
        }
    }

    internal sealed class FumeHoodPreview : InstrumentAnimPreview
    {
        internal FumeHoodPreview() : base("Chemistry Fume Hood Preview") { }
        protected override string SpriteName => "chem_anim_fume_hood";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            int tint = Sample(look, Aqua);
            s.Housing(16, 10, 74, 41, 4, Metal);
            s.Rect(22, 18, 61, 25, Shade);
            s.Rect(25, 19, 55, 2, Ivory);
            s.Rect(24, 42, 56, 2, Rim);
            s.Oval(73, 30, 6, 6, Shadow);
            for (int blade = 0; blade < 3; blade++)
            {
                float a = blade * 2.0944f + (phase == Phase.Run ? step * level * 0.42f : 0f);
                s.Shaft(73, 30, 73 + Mathf.Cos(a) * 5, 30 + Mathf.Sin(a) * 5, 0.8f, 2, Metal);
            }
            for (int x = 28; x < 80; x += 3)
                s.Etch(x, 22, 2, 0x7A9898);
            s.Glass(43, 33, 20, 9, tint, Fill(look));
            if (phase == Phase.Run)
                for (int i = 0; i < 3 + level; i++)
                {
                    int y = 35 - (step * level + i * 3) % 13;
                    int x = 47 + (35 - y) * 2 + i % 4;
                    s.Vapor(x, y, 3 + i % 3, step + i * 7, Mix(tint, Shade, (35 - y) / 19f));
                }
            int sash = 31 - Scale(progress * 2f, 6);
            s.Rect(23, 22, 58, sash - 22, 0x415763);
            s.Line(28, sash - 1, 45, 22, 0x65838B);
            s.Highlight(31, sash - 1, 48, 22, 0x86A9AF);
            s.Rect(21, sash, 63, 2, Rim);
            for (int i = 0; i < 6; i++)
                s.Rect(32 + i * 7, 13, 4, 2, phase == Phase.Run && i == step / 2 % 6 ? Aqua : Shadow);
            s.Lamp(74, 46, phase == Phase.Done ? Aqua : Metal);
        }
    }

    internal sealed class UltrasonicCleanerPreview : InstrumentAnimPreview
    {
        internal UltrasonicCleanerPreview() : base("Chemistry Ultrasonic Cleaner Preview") { }
        protected override string SpriteName => "chem_anim_ultrasonic_cleaner";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            int tint = Sample(look, Aqua);
            s.Oval(51, 49, 38, 4, Shadow);
            s.Housing(18, 20, 65, 28, 6, 0x668084);
            s.Panel(23, 21, 54, 17, 0x3F676E);
            s.Rect(25, 29, 50, 7, 0x496E80);
            int basketY = 34 - Scale(1f - Mathf.SmoothStep(0f, 1f, progress * 5f), 4);
            s.Shaft(27, basketY, 72, basketY, 1, 1, Metal);
            s.Highlight(28, 20, 28, basketY, Rim);
            s.Highlight(72, 20, 72, basketY, Rim);
            for (int i = 0; i < 12; i++) s.Etch(27 + i * 4, basketY - 1, 2, Rim);
            int shift = phase == Phase.Run ? step % 3 - 1 : 0;
            s.Glass(43 + shift, basketY - 11, 13, 10, tint, 0.24f);
            if (phase == Phase.Run)
            {
                s.Wave(25, 30, 50, step * (0.45f + level * 0.18f), Cold);
                for (int i = 0; i < 2 + level; i++)
                {
                    int age = (step + i * 5) % 9;
                    s.Arc(33 + i * 9, 32 - age, 1 + age / 4, 1, 3.25f, 6.25f, Aqua);
                }
            }
            s.Knob(67, 42, level * 2, Gold);
            s.Lamp(32, 41, phase == Phase.Done ? Aqua : phase == Phase.Run ? Gold : Metal);
        }
    }

    internal sealed class DryingOvenPreview : InstrumentAnimPreview
    {
        internal DryingOvenPreview() : base("Chemistry Drying Oven Preview") { }
        protected override string SpriteName => "chem_anim_drying_oven";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer, 44);
            int tint = Sample(look, Gold);
            s.Housing(20, 11, 65, 39, 5, 0x756662);
            s.Panel(25, 16, 45, 28, 0x44383A);
            s.Rect(28, 19, 39, 21, phase == Phase.Run ? 0x6A453D : Shade);
            s.Rect(29, 28, 36, 1, Rim);
            s.Rect(29, 37, 36, 1, Rim);
            for (int x = 30; x < 65; x += 4)
            {
                s.Etch(x, 28, 2, Shadow);
                s.Etch(x, 37, 2, Shadow);
            }
            if (look.Form != Specimen.Empty)
                for (int i = 0; i < 5; i++)
                    s.Granule(34 + i * 6, 34 + i % 2, i % 2 == 0 ? 2 : 1, Mix(tint, Gold, progress * 0.5f));
            if (phase == Phase.Run)
                for (int i = 0; i < 3; i++)
                {
                    int x = 35 + i * 11;
                    int y = 33 - (step + i * 4) % 13;
                    s.Vapor(x, y, 4 + i, step + i * 5, Mix(Heat, Gold, 0.35f));
                }
            s.Shaft(74, 20, 74, 33, 2, 2, 0xC4AE88);
            float heat = Mathf.Sin(Mathf.PI * progress);
            s.Dial(77, 15, 5, heat);
            if (phase == Phase.Run)
                for (int coil = 0; coil < 9; coil++)
                    s.Arc(31 + coil * 4, 39, 2, 1, 3.14f, 6.28f, Mix(Metal, Heat, heat));
            s.Lamp(74, 40, phase == Phase.Done ? Aqua : phase == Phase.Run ? Heat : Metal);
        }
    }
}
