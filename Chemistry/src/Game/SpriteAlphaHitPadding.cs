using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chemistry
{
    /// <summary>
    /// Follows the visible sprite alpha, with a one-pixel forgiving margin.
    /// The margin is clipped to the item's image rectangle, which represents its occupied footprint.
    /// </summary>
    internal sealed class SpriteAlphaHitPadding : MonoBehaviour, ICanvasRaycastFilter
    {
        private const byte VisibleAlpha = 8;
        private const int PaddingPixels = 1;

        private sealed class AlphaMask
        {
            internal int Width;
            internal int Height;
            internal byte[] Alpha;
            internal bool Readable;
        }

        private static readonly Dictionary<Sprite, AlphaMask> Masks = new Dictionary<Sprite, AlphaMask>();
        private Image image;

        private void Awake()
        {
            image = GetComponent<Image>();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (image == null || image.sprite == null || image.type != Image.Type.Simple)
            {
                return true;
            }

            RectTransform rectTransform = image.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 local))
            {
                return false;
            }

            Rect drawRect = GetDrawRect(rectTransform.rect, image.sprite, image.preserveAspect);
            if (!drawRect.Contains(local))
            {
                return false;
            }

            AlphaMask mask = GetMask(image.sprite);
            if (!mask.Readable)
            {
                // Keep the item's bounded image hitbox as a safe fallback for non-readable textures.
                return true;
            }

            float u = (local.x - drawRect.xMin) / drawRect.width;
            float v = (local.y - drawRect.yMin) / drawRect.height;
            int x = Mathf.Clamp(Mathf.FloorToInt(u * mask.Width), 0, mask.Width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(v * mask.Height), 0, mask.Height - 1);

            for (int py = Mathf.Max(0, y - PaddingPixels); py <= Mathf.Min(mask.Height - 1, y + PaddingPixels); py++)
            {
                for (int px = Mathf.Max(0, x - PaddingPixels); px <= Mathf.Min(mask.Width - 1, x + PaddingPixels); px++)
                {
                    if (mask.Alpha[py * mask.Width + px] >= VisibleAlpha)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Rect GetDrawRect(Rect bounds, Sprite sprite, bool preserveAspect)
        {
            if (!preserveAspect || sprite.rect.width <= 0f || sprite.rect.height <= 0f)
            {
                return bounds;
            }

            float scale = Mathf.Min(bounds.width / sprite.rect.width, bounds.height / sprite.rect.height);
            float width = sprite.rect.width * scale;
            float height = sprite.rect.height * scale;
            return new Rect(bounds.center.x - width * 0.5f, bounds.center.y - height * 0.5f, width, height);
        }

        private static AlphaMask GetMask(Sprite sprite)
        {
            if (Masks.TryGetValue(sprite, out AlphaMask cached))
            {
                return cached;
            }

            var result = new AlphaMask { Readable = false };
            Masks[sprite] = result;
            try
            {
                Texture2D texture = sprite.texture;
                Rect spriteRect = sprite.textureRect;
                int xMin = Mathf.RoundToInt(spriteRect.xMin);
                int yMin = Mathf.RoundToInt(spriteRect.yMin);
                int width = Mathf.RoundToInt(spriteRect.width);
                int height = Mathf.RoundToInt(spriteRect.height);
                Color32[] pixels = texture.GetPixels32();
                var alpha = new byte[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        alpha[y * width + x] = pixels[(yMin + y) * texture.width + xMin + x].a;
                    }
                }

                result.Width = width;
                result.Height = height;
                result.Alpha = alpha;
                result.Readable = width > 0 && height > 0;
            }
            catch
            {
                // Some game-provided sprites are packed or non-readable; leave their bounded hitbox intact.
            }

            return result;
        }
    }
}
