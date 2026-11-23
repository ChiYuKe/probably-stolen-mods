using UnityEngine;
using static Chemistry.InstrumentAnimPreview.Scene;

namespace Chemistry
{
    internal sealed class PhMeterPreview : InstrumentAnimPreview
    {
        internal PhMeterPreview() : base("Chemistry Ph Meter Preview") { }
        protected override string SpriteName => "chem_anim_ph_meter";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            int tint = Sample(look, Aqua);
            s.Housing(17, 16, 43, 30, 4, Plum);
            s.Display(22, 20, 32, 13);
            s.Knob(48, 39, 5, Rim);
            s.Shaft(59, 33, 66, 12, 2.7f, 1.7f, 0x9C8A70);
            s.Shaft(66, 12, 79, 12, 2, 1, Metal);
            s.Fastener(66, 12);
            s.Highlight(60, 26, 66, 12, 0xAAB4AC);
            float immersion = Mathf.SmoothStep(0f, 1f, progress * 4f)
                * Mathf.SmoothStep(0f, 1f, (1f - progress) * 7f);
            int probe = 17 + Scale(immersion, 13);
            s.Line(79, 11, 79, probe, Rim);
            s.Glass(67, 30, 25, 17, tint, Fill(look));
            s.Shaft(79, probe, 79, probe + 9, 2.6f, 1.3f, 0x8DA3A3);
            s.Etch(78, probe + 3, 3, Ivory);
            s.Oval(79, probe + 10, 1, 2, Cold);
            if (phase != Phase.Idle)
            {
                for (int i = 0; i < 12; i++)
                    s.Rect(24 + i * 2, 27, 2, 3, Mix(Heat, Cold, i / 11f));
                int pointer = 24 + Scale(progress, 12);
                s.Diamond(pointer, 24, 2, Ivory);
                if (phase == Phase.Run && progress < 0.5f)
                    s.Ripple(79, 47 - Scale(Fill(look), 14), step % 5, tint);
                s.Lamp(27, 39, phase == Phase.Done ? Aqua : Gold);
            }
        }
    }

    internal sealed class ConductivityMeterPreview : InstrumentAnimPreview
    {
        internal ConductivityMeterPreview() : base("Chemistry Conductivity Meter Preview") { }
        protected override string SpriteName => "chem_anim_conductivity_meter";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            int tint = Sample(look, Cold);
            s.Housing(13, 24, 42, 24, 5, 0x6E6557);
            s.Display(16, 28, 34, 14);
            s.Line(54, 29, 66, 17, Shadow);
            s.Line(66, 17, 86, 17, Rim);
            s.Glass(69, 29, 24, 18, tint, Fill(look));
            int depth = Scale(Mathf.SmoothStep(0f, 1f, progress * 4f), 5);
            s.Shaft(81, 19 + depth, 81, 25 + depth, 6, 7, 0xA49C84);
            s.Shaft(76, 25 + depth, 76, 36 + depth, 1.3f, 0.8f, Rim);
            s.Shaft(85, 25 + depth, 85, 36 + depth, 1.3f, 0.8f, Rim);
            s.Line(86, 17, 81, 19 + depth, Rim);
            if (phase != Phase.Idle)
            {
                s.Wave(19, 35, 28, phase == Phase.Run ? step * 0.68f : 0f, Gold);
                int path = 78 + step % 7;
                if (phase == Phase.Run)
                {
                    s.Diamond(path, 39, 1, Cold);
                    s.Arc(80, 41, 2 + step % 5, 2, 0.25f, 2.9f, Aqua);
                    s.Etch(20 + step % 23, 39, 2, 0x90B3A9);
                }
            }
            s.Lamp(20, 44, phase == Phase.Done ? Aqua : Metal);
        }
    }

    internal sealed class MicroscopePreview : InstrumentAnimPreview
    {
        internal MicroscopePreview() : base("Chemistry Microscope Preview") { }
        protected override string SpriteName => "chem_anim_microscope";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer, 43);
            int tint = Sample(look, Gold);
            int focus = 4 - Scale(progress * 1.4f, 4);
            s.Housing(17, 44, 49, 7, 3, Metal);
            s.Oval(31, 29, 13, 18, Metal);
            s.Oval(35, 29, 9, 13, 0x2C3845);
            s.Knob(28, 30, phase == Phase.Run ? step / 2 : 3, Gold);
            s.Line(31, 11 + focus, 44, 19 + focus, Rim);
            s.Panel(30, 8 + focus, 12, 6, Shade);
            s.Shaft(37, 12 + focus, 47, 20 + focus, 4, 5, 0x99896F);
            s.Oval(46, 21 + focus, 8, 3, 0xA38B5C);
            float swing = 1f - Mathf.SmoothStep(0f, 1f, progress * 3.5f);
            for (int lens = 0; lens < 3; lens++)
            {
                float angle = (lens - 1) * 0.72f + swing * 0.41f;
                float dx = Mathf.Sin(angle), dy = Mathf.Cos(angle);
                s.Shaft(46 + dx * 6, 21 + focus, 46 + dx * 12, 21 + focus + dy * 10,
                    2.5f, 1.5f, lens == 1 ? 0xC39E59 : Metal);
            }
            for (int i = 0; i < 3; i++) s.Etch(43, 19 + focus + i * 2, 7, Shade);
            s.Panel(34, 36, 24, 4, Shade);
            if (look.Form != Specimen.Empty) s.Diamond(46, 35, 2, tint);
            s.Oval(80, 25, 18, 17, Rim);
            s.Oval(80, 25, 16, 15, Shadow);
            s.Arc(80, 25, 18, 17, 3.2f, 5.77f, 0xE4CCAA);
            s.Arc(80, 25, 17, 16, 0.10f, 2.91f, 0x5A858C);
            if (phase != Phase.Idle)
            {
                int shake = phase == Phase.Run ? Mathf.RoundToInt((1f - progress) * Mathf.Sin(step * 0.54f) * 4f) : 0;
                s.Oval(80, 25, 15, 14, 0x21313A);
                s.Oval(79, 24, 12, 12, 0x2D4649);
                for (int i = 0; i < 12; i++)
                {
                    float angle = i * 2.40f;
                    int distance = 4 + i % 7;
                    int x = 80 + Mathf.RoundToInt(Mathf.Cos(angle) * distance) + shake;
                    int y = 25 + Mathf.RoundToInt(Mathf.Sin(angle) * distance);
                    int mineral = Mix(tint, i % 3 == 0 ? Cold : Gold, 0.32f);
                    s.Granule(x, y, 1 + Scale(progress, i % 3), mineral);
                    if (i % 3 == 0 && progress > 0.46f)
                        s.Highlight(x - 1, y, x, y + 1, Mix(mineral, Ivory, 0.75f));
                }
                s.Highlight(80, 12, 80, 38, 0x547878);
                s.Highlight(67, 25, 93, 25, 0x547878);
                if (phase == Phase.Run && progress < 0.78f)
                    s.Arc(80, 25, 5 + Scale(1f - progress, 8), 5 + Scale(1f - progress, 8), 3.65f, 5.35f, Aqua);
                s.Etch(86, 34, 5, Ivory);
            }
        }
    }

    internal sealed class ChromatographPreview : InstrumentAnimPreview
    {
        internal ChromatographPreview() : base("Chemistry Chromatograph Preview") { }
        protected override string SpriteName => "chem_anim_chromatograph";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            int tint = Sample(look, Aqua);
            s.Housing(12, 13, 81, 36, 5, 0x596E64);
            s.Panel(17, 18, 25, 24, Shade);
            for (int row = 0; row < 5; row++)
            {
                int y = 21 + row * 3;
                s.Shaft(22, y, 35, y, 1, 1, 0xB69A71);
                if (row < 4) s.Line(row % 2 == 0 ? 35 : 22, y, row % 2 == 0 ? 35 : 22, y + 3, Rim);
            }
            s.Glass(24, 37, 10, 7, tint, Fill(look));
            s.Display(46, 18, 42, 23);
            int end = Scale(progress, 36);
            if (phase != Phase.Idle)
            {
                int y = 37;
                for (int x = 0; x < end; x++)
                {
                    float first = (x - 12) / 2.4f;
                    float second = (x - 26) / 2.9f;
                    int peak = Mathf.RoundToInt(14f * Mathf.Exp(-first * first) + 9f * Mathf.Exp(-second * second));
                    s.Highlight(48 + x, y + 1, 49 + x, 38 - peak, 0x355858);
                    s.Highlight(48 + x, y, 49 + x, 37 - peak, Aqua);
                    y = 37 - peak;
                }
                if (phase == Phase.Run)
                {
                    int path = Scale(progress, 69);
                    int row = path / 14, offset = path % 14;
                    s.Etch(22 + (row % 2 == 0 ? offset : 13 - offset), 21 + row * 3, 2, Mix(tint, Ivory, 0.6f));
                }
            }
            s.Lamp(81, 44, phase == Phase.Done ? Aqua : Metal);
        }
    }

    internal sealed class TitrationSetPreview : InstrumentAnimPreview
    {
        internal TitrationSetPreview() : base("Chemistry Titration Set Preview") { }
        protected override string SpriteName => "chem_anim_titration_set";

        protected override void DrawScene(Color32[] buffer, Phase phase, float progress, int step) =>
            Draw(buffer, phase, progress, step, look, Level, ExtraTint, Effects);

        internal static void Draw(Color32[] buffer, Phase phase, float progress, int step, Look look, int level, int extraTint, int effects)
        {
            var s = new Scene(buffer);
            int tint = Sample(look, Cold);
            int endpoint = extraTint == 0 ? 0xD881A1 : extraTint;
            float dispensing = Mathf.Clamp01((progress - 0.12f) / 0.82f);
            float sourceDelta = look.Dose.Configured ? look.Dose.SourceDelta : Fill(look) * 0.75f;
            float receiverFill = look.Dose.Configured ? look.Dose.ReceiverFill : 0.48f;
            float receiverDelta = look.Dose.Configured ? look.Dose.ReceiverDelta : 0.24f;
            float fill = receiverFill + receiverDelta * dispensing;
            s.Stand(24, 7, 48, 57, 17);
            s.Glass(53, 6, 9, 21, tint, Fill(look) - sourceDelta * dispensing);
            float valve = Mathf.Sin(dispensing * Mathf.PI);
            s.Shaft(50, 28, 62, 28 - valve * 4f, 1, 2, 0xBA9563);
            s.Fastener(57, 28);
            s.Rect(57, 30, 1, 4, Aqua);
            int blend = Scale((progress - 0.42f) * 1.8f, 12);
            s.Glass(41, 36, 35, 12, Mix(look.Dose.Configured ? look.Dose.Tint : tint, endpoint, blend / 12f), fill);
            s.Housing(41, 49, 36, 4, 1, Plum);
            if (phase == Phase.Run && dispensing > 0f && dispensing < 1f)
            {
                int drops = look.Dose.Configured ? look.Dose.Drops : level;
                float contact = dispensing * drops % 1f;
                int surface = 48 - Scale(fill, 9);
                s.Pour(57, 33, surface, contact, tint);
                if (contact > 0.62f)
                    s.Arc(57, surface + 1, 2 + Scale((contact - 0.62f) / 0.38f, 7), 2, 0.1f, 3.1f, endpoint);
            }
            s.Panel(80, 20, 15, 15, Metal);
            s.Rect(83, 23, 9, 9, Mix(tint, endpoint, blend / 12f));
        }
    }
}
