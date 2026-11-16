using UnityEngine;
using UnityEngine.EventSystems;

namespace ObjectGrabber
{
    /// <summary>
    /// 浮球上的拖拽/右键处理。左键拖动，右键关闭。
    /// </summary>
    public sealed class ObjectGrabberBall : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private ObjectGrabberPanel panel;
        private Vector3 baseScale = Vector3.one;

        public void Init(ObjectGrabberPanel owner)
        {
            panel = owner;
            baseScale = transform.localScale;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || panel == null)
            {
                return;
            }

            panel.BeginDrag();
            transform.localScale = baseScale * 1.12f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || panel == null)
            {
                return;
            }

            panel.DragTo(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            transform.localScale = baseScale;
            if (panel != null)
            {
                panel.EndDrag();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right && panel != null)
            {
                panel.Close();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (panel != null && !panel.IsDragging)
            {
                transform.localScale = baseScale * 1.08f;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (panel != null && !panel.IsDragging)
            {
                transform.localScale = baseScale;
            }
        }

        private void OnDisable()
        {
            transform.localScale = baseScale;
        }
    }
}
