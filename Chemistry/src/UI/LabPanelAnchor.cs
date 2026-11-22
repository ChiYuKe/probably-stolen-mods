using UnityEngine;

namespace Chemistry
{
    /// <summary>
    /// 把实验室入口和操作面板放到各自的屏幕锚点，覆盖地点系统的初始位置。
    /// </summary>
    internal sealed class LabPanelAnchor : MonoBehaviour
    {
        public Vector2 Size = new Vector2(1000f, 740f);
        public Vector2 Anchor = new Vector2(0.5f, 0.5f);
        public int Frames = 6;

        private void LateUpdate()
        {
            try
            {
                RectTransform rect = transform as RectTransform;
                if (rect != null)
                {
                    rect.anchorMin = Anchor;
                    rect.anchorMax = Anchor;
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = Size;
                }
            }
            catch
            {
                // 窗口已经被关掉就算了
            }

            Frames--;
            if (Frames <= 0)
            {
                Destroy(this);
            }
        }
    }
}
