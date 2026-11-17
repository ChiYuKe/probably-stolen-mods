using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DayHud
{
    /// <summary>
    /// HUD 左下角小图标的悬停/点击处理。逻辑放回 DayHudPanel，
    /// 这里只负责把 Pointer 事件转成回调，并做放大和亮边。
    /// </summary>
    public class DayHudIconButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler
    {
        public Action Clicked;
        public Action Entered;
        public Action Exited;
        public GameObject Highlight;
        public RectTransform Target;

        private bool hovering;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (hovering)
            {
                return;
            }

            hovering = true;
            if (Highlight != null)
            {
                Highlight.SetActive(true);
            }

            if (Target != null)
            {
                Target.localScale = Vector3.one * 1.06f;
            }

            if (Entered != null)
            {
                Entered();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!hovering)
            {
                return;
            }

            hovering = false;
            if (Highlight != null)
            {
                Highlight.SetActive(false);
            }

            if (Target != null)
            {
                Target.localScale = Vector3.one;
            }

            if (Exited != null)
            {
                Exited();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            if (Clicked != null)
            {
                Clicked();
            }
        }

        private void OnDisable()
        {
            hovering = false;
            if (Highlight != null)
            {
                Highlight.SetActive(false);
            }

            if (Target != null)
            {
                Target.localScale = Vector3.one;
            }
        }
    }
}
