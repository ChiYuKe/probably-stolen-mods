using UnityEngine;
using static Chemistry.InstrumentAnimPreview.Scene;

namespace Chemistry
{
    internal sealed class ConicalFlaskPreview : InstrumentAnimPreview
    {
        internal ConicalFlaskPreview() : base("Chemistry Conical Flask Preview") { }
        protected override string SpriteName => "chem_anim_conical_flask";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer, 44);
            int tint = Sample(look, Cold);
            int surface = 47 - Scale(Fill(look), 24);
            bool running = phase == Phase.Run;
            int displaced = running && progress < 0.38f ? Mathf.RoundToInt(Mathf.Sin(step * 0.45f) * (1f - progress)) : 0;
            s.Oval(55, 50, 26, 4, Shadow);
            s.Oval(53, 47, 24, 4, Plum);
            s.Shaft(54, 11, 54, 24, 6, 6, 0x456873);
            s.Oval(54, 10, 8, 2, Aqua);
            s.Oval(54, 10, 6, 1, Shadow);
            s.Arc(54, 10, 7, 2, 3.2f, 5.55f, Ivory);
            for (int y = 13; y < 49; y++)
            {
                int half = y < 24 ? 6 : 6 + (y - 24) * 14 / 24;
                if (y > 45) half -= y - 45;
                bool submerged = y >= surface + displaced && surface < 47;
                int body = submerged ? Mix(tint, Shade, (y - surface) * 0.021f) : 0x354751;
                s.Rect(54 - half, y, half * 2 + 1, 1, body);
                s.Etch(55 - half, y, 2 + half / 5, Mix(body, Aqua, 0.29f));
                s.Dot(54 - half, y, Aqua);
                s.Dot(54 + half, y, Metal);
            }
            s.Line(44, 28, 38, 40, 0x688C93);
            s.Highlight(48, 15, 48, 23, 0xBCD6D1);
            s.Highlight(45, 27, 39, 39, 0xABC9C5);
            s.Highlight(61, 27, 68, 41, 0x617B85);
            s.Rect(38, 48, 33, 1, Rim);
            if (surface < 47)
            {
                int radius = 6 + (surface - 24) * 13 / 24;
                s.Oval(54, surface + displaced, radius, 2, Mix(tint, Shade, 0.15f));
                s.Arc(54, surface + displaced, radius, 2, 0.1f, 2.85f, Mix(tint, Ivory, 0.62f));
            }
            for (int i = 0; i < Scale((progress - 0.20f) / 0.80f, 9); i++)
                s.Granule(41 + i * 19 % 27, 44 + i % 3, i % 3 == 0 ? 2 : 1, Mix(tint, Ivory, 0.68f));
        }
    }

    internal sealed class GraduatedCylinderPreview : InstrumentAnimPreview
    {
        internal GraduatedCylinderPreview() : base("Chemistry Graduated Cylinder Preview") { }
        protected override string SpriteName => "chem_anim_graduated_cylinder";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            int tint = Sample(look, Aqua);
            float fill = Fill(look);
            int readAt = 45 - Scale(fill, 34);
            s.Oval(44, 50, 17, 3, Shadow);
            s.Shaft(44, 43, 44, 49, 5, 15, 0x74949B);
            s.Oval(44, 49, 15, 2, Metal);
            s.Glass(36, 7, 16, 39, tint, fill);
            for (int i = 0; i < 9; i++)
            {
                int y = 10 + i * 4;
                s.Etch(48 - i % 2 * 3, y, 8 + i % 2 * 3, Rim);
                s.Etch(50, y + 2, 4, 0x667D78);
            }
            if (phase != Phase.Idle)
            {
                int cursor = 10 + Scale(progress * 1.35f, readAt - 10);
                s.Line(38, cursor, 71, cursor, Gold);
                s.Etch(38, cursor + 1, 16, Mix(Gold, Shade, 0.51f));
                s.Diamond(72, cursor, 2, Gold);
                s.Panel(76, 20, 20, 17, Metal);
                s.Rect(79, 24, 14, 8, Shade);
                int magnified = 24 + Scale((cursor - 10) / (float)Mathf.Max(1, readAt - 10), 5);
                s.Etch(81, magnified, 10, Aqua);
                s.Highlight(79, 28, 81, magnified, Gold);
            }
        }
    }

    internal sealed class PipettePreview : InstrumentAnimPreview
    {
        internal PipettePreview() : base("Chemistry Pipette Preview") { }
        protected override string SpriteName => "chem_anim_pipette";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            int tint = Sample(look, Cold);
            bool running = phase == Phase.Run;
            float traverse = Mathf.Clamp01((progress - 0.24f) / 0.34f);
            int x = 36 + Scale(Mathf.SmoothStep(0f, 1f, traverse), 37);
            int lift = Mathf.RoundToInt(4f * Mathf.Sin(traverse * Mathf.PI));
            float dispensing = Mathf.Clamp01((progress - 0.60f) / 0.35f);
            int drops = look.Dose.Configured ? look.Dose.Drops : level;
            float dropPhase = dispensing * drops % 1f;
            int press = running && (progress < 0.24f || progress > 0.65f) ? 2 : 0;
            float sourceDelta = look.Dose.Configured ? look.Dose.SourceDelta : Fill(look) * 0.53f;
            float receiverDelta = look.Dose.Configured ? look.Dose.ReceiverDelta : 0.54f;
            float fill = look.Dose.ReceiverFill + receiverDelta * dispensing;
            s.Oval(54, 50, 37, 3, Shadow);
            s.Glass(23, 32, 26, 16, tint, Fill(look) - sourceDelta * Mathf.Clamp01(progress / 0.24f));
            s.Glass(60, 32, 27, 16, tint, fill);
            int barrel = 14 - lift;
            s.Shaft(x, barrel, x, barrel + 11, 4, 3, 0xAAABA1);
            s.Shaft(x, barrel + 11, x, barrel + 17, 3, 1, 0x8B7358);
            int plunger = barrel - 4 + press;
            s.Rect(x - 1, plunger, 2, barrel - plunger, Rim);
            s.Oval(x, plunger, 3, 1, Heat);
            s.Panel(x + 2, barrel + 2, 5, 4, Plum);
            for (int grip = 0; grip < 4; grip++)
                s.Etch(x - 3, barrel + 2 + grip * 2, 6, Mix(Plum, Rim, 0.36f));
            s.Shaft(x, barrel + 17, x, 34 - lift, 1.6f, 0.35f, phase == Phase.Idle ? Rim : Aqua);
            if (running && dispensing > 0f && dispensing < 1f)
                s.Pour(x, 35, 48 - Scale(fill, 13), dropPhase, tint);
            if (running && progress < 0.24f) s.Drip(x, 42 - step % 7, Aqua);
        }
    }

    internal sealed class MortarPestlePreview : InstrumentAnimPreview
    {
        internal MortarPestlePreview() : base("Chemistry Mortar Pestle Preview") { }
        protected override string SpriteName => "chem_anim_mortar_pestle";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            int tint = Sample(look, Gold);
            bool running = phase == Phase.Run;
            float swing = running ? Mathf.Sin(step * (0.31f + level * 0.12f)) : 0f;
            int x = 52 + Mathf.RoundToInt(swing * 12);
            int y = 36 + Mathf.RoundToInt(Mathf.Abs(swing) * 2);
            s.Oval(55, 50, 30, 4, Shadow);
            s.Oval(54, 43, 24, 8, Metal);
            s.Oval(54, 37, 27, 8, Rim);
            s.Oval(54, 37, 23, 6, Shade);
            s.Arc(54, 37, 25, 7, 0.12f, 3.05f, Ivory);
            s.Arc(54, 43, 21, 6, 0.30f, 2.83f, 0x434E52);
            if (look.Form != Specimen.Empty)
                for (int i = 0; i < 9; i++)
                    s.Granule(37 + i * 7 % 34, 37 + i % 4, progress < 0.43f && i % 3 == 0 ? 2 : 1, tint);
            int handle = 78 + Mathf.RoundToInt(swing * 3);
            s.Shaft(x, y, handle, 12, 5, 2.7f, 0xAE9E85);
            s.Oval(x, y, 5, 3, Metal);
            s.Oval(handle, 12, 3, 2, Ivory);
            s.Highlight(x - 2, y - 3, handle - 2, 12, 0xE9DFC9);
            if (running)
                for (int i = 0; i < level + 1; i++)
                    s.Dot(x + (i * 7 + step) % 15 - 7, y - 3 - (step + i * 3) % 8, Gold);
        }
    }

    internal sealed class BunsenBurnerPreview : InstrumentAnimPreview
    {
        internal BunsenBurnerPreview() : base("Chemistry Bunsen Burner Preview") { }
        protected override string SpriteName => "chem_anim_bunsen_burner";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            s.Oval(48, 50, 24, 4, Shadow);
            s.Housing(30, 44, 35, 7, 3, Metal);
            s.Shaft(48, 44, 48, 24, 6, 3, 0xAE865A);
            s.Rect(42, 23, 12, 3, Rim);
            s.Shaft(48, 35, 48, 39, 6, 6, 0x907D62);
            for (int vent = 0; vent < 3; vent++)
                s.Etch(44 + vent * 3, 37, 2, 0xA38D75);
            s.Shaft(54, 40, 70, 40, 2, 2, 0x9A754B);
            s.Shaft(70, 40, 91, 48, 2, 1, Plum);
            s.Knob(60, 46, level * 2, Gold);
            if (phase == Phase.Run)
            {
                float envelope = Mathf.Clamp01(progress * 16f) * Mathf.Clamp01((1f - progress) * 16f);
                int plume = 3 + Scale(envelope, 4 + level * 3 + step % 4);
                for (int i = 0; i < plume; i++)
                {
                    int center = 48 + Mathf.RoundToInt(Mathf.Sin(step * 0.55f + i * 0.31f) * i / 10f);
                    int half = (plume - i) * 5 / plume;
                    int shell = Mix(0x6072C5, 0x73ABED, i / (float)plume);
                    s.Rect(center - half, 22 - i, half * 2 + 1, 1, shell);
                    if (half > 1) s.Etch(center - half / 2, 22 - i, half + 1, Mix(Cold, Ivory, 0.63f));
                }
                s.Oval(48, 23, 6, 1, 0x88C7EB);
            }
            if (phase == Phase.Done) s.Rect(44, 23, 8, 1, Heat);
        }
    }

    internal sealed class BalancePreview : InstrumentAnimPreview
    {
        internal BalancePreview() : base("Chemistry Balance Preview") { }
        protected override string SpriteName => "chem_anim_balance";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer, 45);
            bool running = phase == Phase.Run;
            int shift = running ? Mathf.RoundToInt(3f * (1f - progress) * Mathf.Sin(step * 0.61f)) : 0;
            s.Oval(54, 51, 37, 4, Shadow);
            s.Housing(21, 38, 64, 13, 5, Metal);
            s.Panel(25, 9, 44, 29, 0x43636D);
            s.Rect(28, 12, 38, 22, 0x233944);
            s.Line(29, 25, 40, 14, 0x3C5964);
            s.Line(30, 29, 44, 15, 0x3C5964);
            s.Rect(70, 10, 5, 28, Rim);
            s.Shaft(30, 36, 47, 32 + shift, 2, 1, 0xA58A57);
            s.Fastener(30, 36);
            s.Shaft(47, 38, 47, 30 + shift, 3, 2, Rim);
            s.Oval(47, 30 + shift, 16, 3, Rim);
            s.Oval(47, 29 + shift, 13, 2, 0x647675);
            s.Arc(47, 29 + shift, 15, 3, 0.22f, 2.86f, Ivory);
            if (look.Form != Specimen.Empty)
            {
                s.Granule(46, 25 + shift, 4, look.Tint);
                s.Granule(53, 27 + shift, 2, look.Tint);
            }
            s.Highlight(30, 18, 36, 13, 0x719296);
            s.Highlight(58, 31, 64, 25, 0x536C73);
            s.Dial(65, 44, 5, 0.5f + shift * 0.12f);
            s.Display(30, 40, 29, 9);
            if (phase != Phase.Idle)
                for (int i = 0; i < 5; i++)
                    s.Rect(34 + i * 4, 46 - (step + i * 3) % 3, 2, 2 + (step + i * 3) % 3, phase == Phase.Done ? Aqua : Gold);
            s.Lamp(73, 44, phase == Phase.Done ? Aqua : Metal);
        }
    }

    internal sealed class FilterFunnelPreview : InstrumentAnimPreview
    {
        internal FilterFunnelPreview() : base("Chemistry Filter Funnel Preview") { }
        protected override string SpriteName => "chem_anim_filter_funnel";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            int tint = Sample(look, Aqua);
            int filter = Mix(tint, Ivory, 0.53f);
            s.Stand(75, 7, 48, 49, 22);
            s.Glass(36, 36, 25, 13, tint, progress * Fill(look));
            s.Shaft(49, 24, 49, 34, 2, 1, 0x91ABA8);
            for (int row = 0; row < 13; row++)
            {
                int half = 15 - row;
                s.Rect(49 - half, 12 + row, half * 2 + 1, 1, row < 7 ? Ivory : Rim);
                s.Dot(49 - half, 12 + row, Aqua);
                s.Dot(49 + half, 12 + row, Metal);
            }
            for (int fold = -3; fold <= 3; fold++)
                s.Highlight(49 + fold * 4, 14, 49 + fold, 23, Mix(Rim, tint, progress * 0.55f));
            s.Oval(49, 12, 16, 3, Shade);
            s.Oval(49, 12, 13, 2, filter);
            s.Arc(49, 12, 15, 3, 0.1f, 3f, Ivory);
            s.Highlight(36, 14, 47, 24, 0xDDE2CC);
            for (int i = 0; i < Scale(progress, 9); i++)
                s.Dot(39 + i * 7 % 20, 11 + i % 3, Mix(tint, Shadow, 0.34f));
            if (phase == Phase.Run)
            {
                int age = step % 8;
                if (age < 5) s.Drip(49, 34 + age * age / 2, filter);
                else s.Ripple(49, 48 - Scale(progress * Fill(look), 10), age - 4, filter);
            }
        }
    }

    internal sealed class BurettePreview : InstrumentAnimPreview
    {
        internal BurettePreview() : base("Chemistry Burette Preview") { }
        protected override string SpriteName => "chem_anim_burette";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            int tint = Sample(look, Cold);
            s.Stand(68, 6, 48, 46, 15);
            float dispensing = Mathf.Clamp01((progress - 0.16f) / 0.77f);
            float sourceDelta = look.Dose.Configured ? look.Dose.SourceDelta : Fill(look) * 0.76f;
            float receiverDelta = look.Dose.Configured ? look.Dose.ReceiverDelta : 0.58f;
            float fill = look.Dose.ReceiverFill + receiverDelta * dispensing;
            s.Glass(42, 6, 9, 25, tint, Fill(look) - sourceDelta * dispensing);
            for (int i = 0; i < 6; i++) s.Etch(47 - i % 2, 10 + i * 3, 5 + i % 2, Rim);
            s.Rect(46, 29, 2, 10, Aqua);
            bool open = phase == Phase.Run && dispensing > 0f && dispensing < 1f;
            float opening = Mathf.SmoothStep(0f, 1f, (progress - 0.10f) / 0.09f)
                * Mathf.SmoothStep(0f, 1f, (0.97f - progress) / 0.08f);
            float angle = opening * 1.20f;
            float dx = Mathf.Cos(angle) * 8f, dy = Mathf.Sin(angle) * 4f;
            s.Shaft(47 - dx, 33 - dy, 47 + dx, 33 + dy, 1.6f, 2, 0xAC8458);
            s.Fastener(47, 33);
            s.Glass(35, 40, 23, 9, tint, fill);
            if (open)
            {
                int drops = look.Dose.Configured ? look.Dose.Drops : level * 2;
                s.Pour(47, 39, 49 - Scale(fill, 6), dispensing * drops % 1f, tint);
            }
        }
    }
}
