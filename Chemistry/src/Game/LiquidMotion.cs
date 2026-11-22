using System;
using UnityEngine;

namespace Chemistry
{
    internal sealed class LiquidMotion : MonoBehaviour
    {
        private const float PourSeconds = 0.38f;
        private const float ReactionSeconds = 2.5f;
        private const float FrameInterval = 1f / 30f;
        private const float RestThreshold = 0.003f;
        private const float MaximumSlosh = 0.32f;
        private GameItemElement item;
        private (string Sprite, RectInt Chamber) vessel;
        private string art;
        private int color;
        private int transferColor;
        private AqueousReactions.Observation effects;
        private float reactionStarted = float.NegativeInfinity;
        private bool empty;
        private bool configured;
        private bool usingLive;
        private bool pouring;
        private float pourStart;
        private float from;
        private float target;
        private float slosh;
        private float sloshVelocity;
        private float lastMouse;
        private float lastVelocity;
        private bool wasDragging;
        private float nextFrame;
        private float renderedFill = -1f;
        private Vector2 renderedUp;
        private int renderedColor;
        private string renderedArt;
        private Color32[] pixels;
        private Color32[] expanded;
        private Sprite live;

        internal static Sprite Select(GameItemElement item, (string Sprite, RectInt Chamber) vessel, string art,
            int color, bool empty, AqueousReactions.Observation effects, float target)
        {
            LiquidMotion motion = Get(item, !empty);
            if (motion == null)
            {
                return null;
            }
            motion.vessel = vessel;
            motion.art = art;
            motion.color = color;
            motion.empty = empty;
            motion.effects = effects;
            motion.target = target;
            motion.configured = true;

            float now = Time.unscaledTime;
            bool dragging = motion.IsDragging;
            Vector2 up = motion.LiquidUp(dragging);
            float amount = motion.Sample(now);
            motion.usingLive = motion.NeedsLive(dragging, up, amount, now);
            return motion.usingLive ? motion.Draw(now, up, amount, true) : null;
        }

        internal static void Begin(GameItem bottle, float previousFill, int color)
        {
            if (!(bottle is GameItemElement item))
            {
                return;
            }
            LiquidMotion motion = Get(item, true);
            if (motion == null)
            {
                return;
            }
            float now = Time.unscaledTime;
            motion.from = motion.pouring ? motion.Sample(now) : Mathf.Clamp01(previousFill);
            motion.transferColor = color;
            motion.pourStart = now;
            motion.pouring = true;
            item.HandleInnateModSprite();
        }

        internal static void BeginReaction(GameItem bottle)
        {
            if (bottle is not GameItemElement item || !AqueousReactions.HasGas(AqueousReactions.VisualContents(item))) return;
            LiquidMotion motion = Get(item, true);
            if (motion == null) return;
            motion.reactionStarted = Time.unscaledTime;
            item.HandleInnateModSprite();
        }

        private static LiquidMotion Get(GameItemElement item, bool create)
        {
            if (item.handler == null)
            {
                return null;
            }
            GameObject root = item.handler.gameObject;
            LiquidMotion motion = root.GetComponent<LiquidMotion>();
            if (motion == null && create)
            {
                motion = root.AddComponent<LiquidMotion>();
                motion.item = item;
            }
            return motion;
        }

        private bool IsDragging
        {
            get
            {
                ItemMouseDragHandler drag = ItemMouseDragHandler.current;
                return drag?.IsDraggingItem == true && drag.GetCurrentItem() == item;
            }
        }

        private float Sample(float now)
        {
            if (!pouring)
            {
                return target;
            }
            float progress = (now - pourStart) / PourSeconds;
            if (progress >= 1f)
            {
                pouring = false;
                return target;
            }
            return Mathf.SmoothStep(from, target, Mathf.Clamp01(progress));
        }

        private void LateUpdate()
        {
            if (!configured || item.background == null)
            {
                return;
            }
            bool dragging = IsDragging;
            if (!dragging && !item.background.image.enabled)
            {
                return;
            }

            UpdateSlosh(dragging);
            float now = Time.unscaledTime;
            Vector2 up = LiquidUp(dragging);
            float amount = Sample(now);
            bool nextLive = NeedsLive(dragging, up, amount, now);
            if (nextLive != usingLive)
            {
                item.HandleInnateModSprite();
            }
            else if (nextLive)
            {
                Draw(now, up, amount, false);
            }
        }

        private bool ReactionActive(float now) => AqueousReactions.HasGas(effects) && now - reactionStarted < ReactionSeconds;

        private bool NeedsLive(bool dragging, Vector2 up, float amount, float now) =>
            pouring || ReactionActive(now) || (amount > 0f && amount < 1f &&
                (dragging || Mathf.Abs(up.x) > RestThreshold || up.y < 0.9999f));

        private void UpdateSlosh(bool dragging)
        {
            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.035f);
            float velocity = 0f;
            if (dragging)
            {
                float mouse = Input.mousePosition.x;
                velocity = wasDragging ? Mathf.Clamp((mouse - lastMouse) / dt, -2200f, 2200f) : 0f;
                lastMouse = mouse;
            }
            sloshVelocity -= Mathf.Clamp((velocity - lastVelocity) * 0.0024f, -2.8f, 2.8f);
            lastVelocity = velocity;
            wasDragging = dragging;

            float dragLean = -velocity * 0.000065f;
            sloshVelocity += ((dragLean - slosh) * 95f - 7f * sloshVelocity) * dt;
            slosh = Mathf.Clamp(slosh + sloshVelocity * dt, -MaximumSlosh, MaximumSlosh);
            if (!dragging && Mathf.Abs(slosh) < RestThreshold && Mathf.Abs(sloshVelocity) < 0.03f)
            {
                slosh = 0f;
                sloshVelocity = 0f;
            }
        }

        private Vector2 LiquidUp(bool dragging)
        {
            RectTransform transform = (dragging ? item.fakeRotator : item.rotator)?.rectTransform;
            if (transform == null)
            {
                return Vector2.up;
            }
            Vector3 horizontal = transform.TransformVector(Vector3.right);
            Vector3 vertical = transform.TransformVector(Vector3.up);
            var normal = new Vector2(horizontal.y - slosh * horizontal.x, vertical.y - slosh * vertical.x);
            return normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector2.up;
        }

        private Sprite Draw(float now, Vector2 up, float amount, bool force)
        {
            int paintColor = empty ? transferColor : color;
            bool reacting = ReactionActive(now);
            if (!force && (now < nextFrame || (!reacting && Mathf.Abs(renderedFill - amount) < RestThreshold
                && (renderedUp - up).sqrMagnitude < RestThreshold * RestThreshold
                && renderedColor == paintColor && renderedArt == art)))
            {
                return live;
            }

            int size = MaterialItemArt.Size;
            bool wide = art == BulkContainers.DrumSprite;
            int width = wide ? size * 3 / 2 : size;
            if (live != null && live.texture.width != width)
            {
                Release();
            }
            pixels ??= new Color32[size * size];
            Array.Clear(pixels, 0, pixels.Length);
            MaterialItemArt.DrawLiquid(pixels, vessel, paintColor, amount, up);
            MaterialItemArt.DrawReaction(pixels, effects, up, reacting ? now - reactionStarted : 0f);
            if (wide)
            {
                expanded ??= new Color32[width * width];
                MaterialItemArt.ExpandPixels(pixels, expanded, width);
            }
            Color32[] frame = wide ? expanded : pixels;
            if (live == null)
            {
                live = MaterialItemArt.CreateSprite("chemistry_liquid_motion", frame, width, width);
            }
            else
            {
                live.texture.SetPixels32(frame);
                live.texture.Apply(false, false);
            }

            nextFrame = now + FrameInterval;
            renderedFill = amount;
            renderedUp = up;
            renderedColor = paintColor;
            renderedArt = art;
            return live;
        }

        private void OnDestroy() => Release();

        private void Release()
        {
            if (live == null)
            {
                return;
            }
            Destroy(live.texture);
            Destroy(live);
            live = null;
        }
    }
}
