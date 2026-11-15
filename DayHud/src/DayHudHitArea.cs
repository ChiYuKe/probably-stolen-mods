using UnityEngine;
using UnityEngine.UI;

namespace DayHud
{
    /// <summary>
    /// 把矩形 Image 的命中范围收成椭圆，透明四角不再触发悬停/点击。
    /// </summary>
    public class DayHudHitArea : MonoBehaviour, ICanvasRaycastFilter
    {
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            RectTransform rect = transform as RectTransform;
            if (rect == null)
            {
                return true;
            }

            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, eventCamera, out local))
            {
                return false;
            }

            Vector2 size = rect.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                return true;
            }

            float nx = local.x / (size.x * 0.5f);
            float ny = local.y / (size.y * 0.5f);
            // 稍微放宽一点，保证最外圈描边也能触发，但四个角仍然排除
            return nx * nx + ny * ny <= 1.05f;
        }
    }
}
