using UnityEngine;
using UnityEngine.EventSystems;

namespace Chemistry
{
    /// <summary>
    /// 仪器动画占 110×58 的界面区域，使用 220×116 的点采样贴图。
    /// 待机 / 运行 / 完成三态驱动，每种设备只实现自己的场景绘制（与 ReactionPreview、
    /// SpectrometerPreview 同一套像素画风）。烧杯和光谱仪保留原有专属动画，
    /// 其余设备由 <see cref="Create"/> 按键位各配一份独立动画。
    /// </summary>
    internal abstract partial class InstrumentAnimPreview : PixelElement, MachineryWidget
    {
        internal enum Specimen { Empty, Solid, Liquid, Gas }
        internal enum Phase { Idle, Run, Done }

        internal readonly struct Transfer
        {
            internal readonly bool Configured;
            internal readonly int Tint;
            internal readonly float ReceiverFill;
            internal readonly float SourceDelta;
            internal readonly float ReceiverDelta;
            internal readonly int Drops;

            internal Transfer(int tint, float receiverFill, float sourceDelta, float receiverDelta, int drops)
            {
                Configured = true;
                Tint = tint;
                ReceiverFill = receiverFill;
                SourceDelta = sourceDelta;
                ReceiverDelta = receiverDelta;
                Drops = drops;
            }

            internal static bool Same(Transfer left, Transfer right) => left.Configured == right.Configured && left.Tint == right.Tint
                && left.ReceiverFill == right.ReceiverFill && left.SourceDelta == right.SourceDelta
                && left.ReceiverDelta == right.ReceiverDelta && left.Drops == right.Drops;
        }

        /// <summary>样品外观：形态、配色与液体量，待机和动画里据此呈现装载的样品。</summary>
        internal readonly struct Look
        {
            internal static readonly Look None = new(Specimen.Empty, 0x908377, 0f);
            internal readonly Specimen Form;
            internal readonly int Tint;
            internal readonly float Fill;
            internal readonly Transfer Dose;
            internal Look(Specimen form, int tint, float fill) : this(form, tint, fill, default) { }
            internal Look(Specimen form, int tint, float fill, Transfer dose)
            {
                Form = form;
                Tint = tint;
                Fill = fill;
                Dose = dose;
            }
            internal static bool Same(Look left, Look right) =>
                left.Form == right.Form && left.Tint == right.Tint && left.Fill == right.Fill
                    && Transfer.Same(left.Dose, right.Dose);
        }

        internal const int Width = 110;
        internal const int Height = 58;
        internal const int TextureWidth = Width;
        internal const int TextureHeight = Height;
        private const float FrameInterval = 1f / 24f;

        private readonly Color32[] pixels = new Color32[TextureWidth * TextureHeight];
        private readonly TreeNodeRender artwork;
        private Sprite sprite;
        private Phase phase = Phase.Idle;
        protected Look look = Look.None;
        private float startedAt;
        private float nextFrame;
        private float feedbackAt = float.NegativeInfinity;
        private float inspectLight;
        private float lastProgress;
        private int lastStep;
        private int renderedLevel;
        private bool inspecting;
        private bool ready;
        private bool renderedReady;
        private bool success;
        private bool dirty = true;

        public bool canResize => false;
        public bool fillWidth { get; set; }
        public int widthPixels => Width;
        public int heightPixels => Height;
        public TreeNodeRender handler { get; }
        public RectTransform rectTransform => handler.rectTransform;
        public TreeNodeRender inner => handler;

        /// <summary>档位（1..3），面板随设置同步；火焰、转速、温度等用它区分强度。</summary>
        internal int Level { get; set; } = 1;
        /// <summary>附加颜色（滴定终点等），Begin 前由面板写入。</summary>
        internal int ExtraTint { get; set; }
        /// <summary>反应观察位标志（AqueousReactions.Observation），混合类动画据此画气泡、沉淀。</summary>
        internal int Effects { get; set; }

        protected InstrumentAnimPreview(string name)
        {
            handler = TreeNodeRender.Instantiate(this, name);
            handler.image.enabled = false;
            handler.image.raycastTarget = false;
            artwork = TreeNodeRender.Instantiate().SetParent(handler);
            artwork.gameObject.AddComponent<InstrumentInspection>().Preview = this;
            Validate();
        }

        /// <summary>按设备键挑专属动画；beaker / spectrometer 返回 null，继续用原有专属动画。</summary>
        internal static InstrumentAnimPreview Create(string key) => key switch
        {
            "conical_flask" => new ConicalFlaskPreview(),
            "graduated_cylinder" => new GraduatedCylinderPreview(),
            "pipette" => new PipettePreview(),
            "mortar_pestle" => new MortarPestlePreview(),
            "bunsen_burner" => new BunsenBurnerPreview(),
            "balance" => new BalancePreview(),
            "filter_funnel" => new FilterFunnelPreview(),
            "burette" => new BurettePreview(),
            "ph_meter" => new PhMeterPreview(),
            "conductivity_meter" => new ConductivityMeterPreview(),
            "centrifuge" => new CentrifugePreview(),
            "distiller" => new DistillerPreview(),
            "separating_funnel" => new SeparatingFunnelPreview(),
            "hotplate_stirrer" => new HotplateStirrerPreview(),
            "heating_mantle" => new HeatingMantlePreview(),
            "fume_hood" => new FumeHoodPreview(),
            "ultrasonic_cleaner" => new UltrasonicCleanerPreview(),
            "drying_oven" => new DryingOvenPreview(),
            "microscope" => new MicroscopePreview(),
            "titration_set" => new TitrationSetPreview(),
            "chromatograph" => new ChromatographPreview(),
            "rotary_evaporator" => new RotaryEvaporatorPreview(),
            "freeze_dryer" => new FreezeDryerPreview(),
            "autoclave" => new AutoclavePreview(),
            "glovebox" => new GloveboxPreview(),
            _ => null
        };

        internal void Begin(Look sample)
        {
            look = sample;
            startedAt = Time.unscaledTime;
            phase = Phase.Run;
            lastProgress = 0f;
            lastStep = 0;
            success = false;
            dirty = true;
        }

        internal void Finish(bool ok)
        {
            phase = Phase.Done;
            success = ok;
            feedbackAt = Time.unscaledTime;
            dirty = true;
        }

        internal void Inspect(bool value) => inspecting = value;
        internal System.Action StartRequested { private get; set; }
        internal void TryStart()
        {
            if (ready && phase != Phase.Run) StartRequested?.Invoke();
        }

        internal void Update(Look sample, float progress, bool showResult, bool canStart)
        {
            float now = Time.unscaledTime;
            float targetLight = inspecting ? 1f : 0f;
            bool movingLight = inspectLight != targetLight;
            ready = canStart;
            if (phase == Phase.Run)
            {
                lastProgress = Mathf.Clamp01(progress);
                lastStep = (int)((now - startedAt) * 18f);
                if (!dirty && now < nextFrame) return;
                RenderFrame(Phase.Run, lastProgress, lastStep, now, targetLight);
                return;
            }
            if (phase == Phase.Done && showResult)
            {
                if (dirty || ((movingLight || now < feedbackAt + 0.65f + FrameInterval) && now >= nextFrame))
                    RenderFrame(success ? Phase.Done : Phase.Run, success ? 1f : lastProgress, lastStep, now, targetLight);
                return;
            }
            // 完成后换装了新样品，或一直在待机：按当前样品重绘一帧静态待机画面。
            dirty |= phase != Phase.Idle || renderedLevel != Level;
            phase = Phase.Idle;
            if (!Look.Same(look, sample) || renderedReady != ready)
            {
                feedbackAt = now;
                look = sample;
                dirty = true;
            }
            if (dirty || ((movingLight || now < feedbackAt + 0.65f + FrameInterval) && now >= nextFrame))
                RenderFrame(Phase.Idle, 0f, 0, now, targetLight);
        }

        private void RenderFrame(Phase current, float progress, int step, float now, float targetLight)
        {
            nextFrame = now + FrameInterval;
            inspectLight = Mathf.MoveTowards(inspectLight, targetLight, 0.19f);
            DrawScene(pixels, current, progress, step);
            Present(pixels, phase, progress, Mathf.Clamp01((now - feedbackAt) / 0.65f), inspectLight, ready, success);
            if (phase == Phase.Done) ResultMark(pixels, success);
            if (sprite == null)
            {
                sprite = MaterialItemArt.CreateSprite(SpriteName, pixels, TextureWidth, TextureHeight);
                MachineryStyle.SetSimple(artwork, sprite, Color.white);
                artwork.image.raycastTarget = true;
                Validate();
            }
            else
            {
                sprite.texture.SetPixels32(pixels);
                sprite.texture.Apply(false, false);
            }
            renderedLevel = Level;
            renderedReady = ready;
            dirty = false;
        }

        protected abstract string SpriteName { get; }
        protected abstract void DrawScene(Color32[] buffer, Phase phase, float progress, int step);

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

        // ---- 像素小工具 ----

        protected static Color32 Ink(int rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        private static void Point(Color32[] buffer, int x, int y, int rgb) => Box(buffer, x, y, 1, 1, rgb);

        private static void Box(Color32[] buffer, int x, int y, int width, int height, int rgb)
        {
            Color32 color = Ink(rgb);
            for (int row = y; row < y + height; row++)
                System.Array.Fill(buffer, color, row * TextureWidth + x, width);
        }

        /// <summary>空心矩形描边。</summary>
        protected static void Outline(Color32[] buffer, int x, int y, int width, int height, int rgb)
        {
            Box(buffer, x, y, width, 1, rgb);
            Box(buffer, x, y + height - 1, width, 1, rgb);
            Box(buffer, x, y + 1, 1, height - 2, rgb);
            Box(buffer, x + width - 1, y + 1, 1, height - 2, rgb);
        }

        protected static int Blend(int first, int second, float amount)
        {
            int r = (int)(((first >> 16) & 255) * (1f - amount) + ((second >> 16) & 255) * amount);
            int g = (int)(((first >> 8) & 255) * (1f - amount) + ((second >> 8) & 255) * amount);
            int b = (int)((first & 255) * (1f - amount) + (second & 255) * amount);
            return (r << 16) | (g << 8) | b;
        }

        /// <summary>完成角标：通过打勾、失败打叉，画在场景之上。</summary>
        internal static void ResultMark(Color32[] buffer, bool ok)
        {
            Box(buffer, 95, 4, 11, 8, 0x0E1418);
            Outline(buffer, 95, 4, 11, 8, ok ? 0x2E4A42 : 0x4A2E2A);
            if (ok)
            {
                Point(buffer, 97, 8, Scene.Aqua);
                Point(buffer, 98, 7, Scene.Aqua);
                Point(buffer, 99, 6, Scene.Aqua);
                Point(buffer, 100, 7, Scene.Aqua);
                Point(buffer, 101, 8, Scene.Aqua);
                Point(buffer, 102, 9, Scene.Aqua);
                Point(buffer, 103, 10, Scene.Aqua);
            }
            else
            {
                Point(buffer, 98, 6, 0xC96A5A);
                Point(buffer, 99, 7, 0xC96A5A);
                Point(buffer, 100, 8, 0xC96A5A);
                Point(buffer, 101, 9, 0xC96A5A);
                Point(buffer, 102, 10, 0xC96A5A);
                Point(buffer, 102, 6, 0xC96A5A);
                Point(buffer, 101, 7, 0xC96A5A);
                Point(buffer, 99, 9, 0xC96A5A);
                Point(buffer, 98, 10, 0xC96A5A);
            }
        }
    }

    internal sealed class InstrumentInspection : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        internal InstrumentAnimPreview Preview { private get; set; }
        public void OnPointerEnter(PointerEventData eventData) => Preview?.Inspect(true);
        public void OnPointerExit(PointerEventData eventData) => Preview?.Inspect(false);
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && !eventData.dragging)
                Preview?.TryStart();
        }
        private void OnDisable() => Preview?.Inspect(false);
    }
}
