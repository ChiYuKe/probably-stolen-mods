using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chemistry
{
    /// <summary>
    /// 周期表格子的鼠标响应。格子的底色 Image 是整格的射线接收面（子节点都关掉了 raycast），
    /// 所以悬停/点击格子任意位置都能触发详情更新，并顺便把底色提亮一点做悬停反馈。
    /// </summary>
    public sealed class ElementCellTrigger : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private ElementDef def;
        private Image plate;
        private Color resting;
        private Color hover;

        internal void Init(ElementDef element, Image plateImage)
        {
            def = element;
            plate = plateImage;
            if (plate != null)
            {
                resting = plate.color;
                hover = new Color(
                    Mathf.Clamp01(resting.r * 1.45f + 0.06f),
                    Mathf.Clamp01(resting.g * 1.45f + 0.06f),
                    Mathf.Clamp01(resting.b * 1.45f + 0.06f),
                    Mathf.Clamp01(resting.a + 0.30f));
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            PeriodicTablePanel.ShowDetail(def);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (plate != null)
            {
                plate.color = hover;
            }

            PeriodicTablePanel.ShowDetail(def);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (plate != null)
            {
                plate.color = resting;
            }
        }
    }
}
