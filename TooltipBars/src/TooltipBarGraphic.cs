using UnityEngine;
using UnityEngine.UI;

namespace TooltipBars
{
    internal sealed class TooltipBarGraphic : MaskableGraphic
    {
        private const float GridHeight = 9f;
        private static readonly Color32 Outline = new Color32(25, 23, 32, 255);
        private static readonly Color32 Frame = new Color32(63, 61, 71, 255);
        private static readonly Color32 Rim = new Color32(105, 105, 114, 255);
        private static readonly Color32 Groove = new Color32(36, 36, 46, 255);
        private static readonly Color32 Tick = new Color32(80, 80, 91, 255);
        private static readonly Color32 Steel = new Color32(104, 110, 115, 255);
        private static readonly Color32 Light = new Color32(215, 211, 198, 255);

        private float ratio;
        private Color accent;

        internal void SetProgress(float progress, Color sourceColor)
        {
            ratio = Mathf.Clamp01(progress);
            accent = Color.Lerp(sourceColor, Steel, 0.32f);
            accent.a = sourceColor.a;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            // Keep the end caps square at any bar length. Compact bars use narrower caps.
            float columns = Mathf.Max(12f, rect.width / rect.height * GridHeight);
            float unitX = rect.width / columns;
            float unitY = rect.height / GridHeight;

            void Band(float left, float bottom, float right, float top, Color32 shade)
            {
                AddQuad(mesh, rect.xMin + left * unitX, rect.yMin + bottom * unitY,
                    rect.xMin + right * unitX, rect.yMin + top * unitY, shade);
            }

            Band(2f, 0f, columns - 2f, 9f, Outline);
            Band(1f, 1f, columns - 1f, 8f, Outline);
            Band(0f, 2f, columns, 7f, Outline);
            Band(1f, 2f, columns - 1f, 7f, Frame);
            Band(2f, 1f, columns - 2f, 8f, Frame);
            Band(2f, 7f, columns - 2f, 8f, Rim);
            Band(2f, 2f, columns - 2f, 7f, Groove);

            const float fillStart = 3f;
            float fillWidth = columns - 2f * fillStart;
            if (fillWidth >= 16f)
            {
                for (int quarter = 1; quarter <= 3; quarter++)
                {
                    float x = fillStart + fillWidth * quarter / 4f;
                    Band(x - 0.5f, 2f, x + 0.5f, 3f, Tick);
                }
            }

            if (ratio <= 0f)
            {
                return;
            }

            float fillEnd = fillStart + fillWidth * ratio;
            Color shadeBelow = Color.Lerp(accent, Outline, 0.42f);
            Color highlight = Color.Lerp(accent, Light, 0.35f);
            Color front = Color.Lerp(accent, Light, 0.55f);
            shadeBelow.a = highlight.a = front.a = accent.a;

            Band(fillStart, 3f, fillEnd, 4f, shadeBelow);
            float frontStart = Mathf.Max(fillStart, fillEnd - 1f);
            if (frontStart > fillStart)
            {
                Band(fillStart, 4f, frontStart, 6f, accent);
            }
            Band(frontStart, 4f, fillEnd, 6f, front);
            Band(fillStart, 6f, fillEnd, 7f, highlight);
        }

        private static void AddQuad(VertexHelper mesh, float left, float bottom, float right, float top, Color32 shade)
        {
            int vertex = mesh.currentVertCount;
            mesh.AddVert(new Vector3(left, bottom), shade, Vector2.zero);
            mesh.AddVert(new Vector3(left, top), shade, Vector2.zero);
            mesh.AddVert(new Vector3(right, top), shade, Vector2.zero);
            mesh.AddVert(new Vector3(right, bottom), shade, Vector2.zero);
            mesh.AddTriangle(vertex, vertex + 1, vertex + 2);
            mesh.AddTriangle(vertex + 2, vertex + 3, vertex);
        }
    }
}
