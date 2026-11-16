using UnityEngine;

namespace NeteaseCassette
{
    // QR 矩阵画成 Texture2D/Sprite，给 CustomUI 的图片控件用
    public static class QrSprite
    {
        private static Texture2D lastTexture;

        public static Sprite Create(bool[,] matrix, int scale = 8, int quietZone = 4)
        {
            if (matrix == null) return null;
            int modules = matrix.GetLength(0);
            if (modules <= 0) return null;
            if (scale < 1) scale = 1;
            if (quietZone < 0) quietZone = 0;

            int size = (modules + quietZone * 2) * scale;
            var pixels = new Color32[size * size];
            Color32 dark = new Color32(18, 18, 18, 255);
            Color32 light = new Color32(248, 248, 248, 255);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = light;

            for (int my = 0; my < modules; my++)
            {
                for (int mx = 0; mx < modules; mx++)
                {
                    if (!matrix[my, mx]) continue;
                    int x0 = (mx + quietZone) * scale;
                    // 贴图原点在左下角，矩阵在左上角，翻一下，不然二维码是镜像的扫不出来
                    int y0 = (modules - 1 - my + quietZone) * scale;
                    for (int dy = 0; dy < scale; dy++)
                    {
                        int row = (y0 + dy) * size;
                        for (int dx = 0; dx < scale; dx++)
                        {
                            pixels[row + x0 + dx] = dark;
                        }
                    }
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "netease_qr",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            if (lastTexture != null)
            {
                Object.Destroy(lastTexture);
            }
            lastTexture = texture;

            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
