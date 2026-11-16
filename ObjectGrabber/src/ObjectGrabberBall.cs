using System;
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
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        private ObjectGrabberPanel panel;
        private Vector3 baseScale = Vector3.one;
        private bool blockingGameDrag;
        private bool itemDragDisabled;

        public void Init(ObjectGrabberPanel owner)
        {
            panel = owner;
            baseScale = transform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            // 游戏的物品拖拽是直接轮询 Input.mousePosition 的，不经过 EventSystem。
            // 把这个静态标记支起来，ItemMouseDragHandler 就不会把浮球下面的物品一起拖走。
            blockingGameDrag = true;
            UIDragHandler.isDraggingAnyUI = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                ReleaseGameDragLatch();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || panel == null)
            {
                return;
            }

            UIDragHandler.isDraggingAnyUI = true;
            panel.BeginDrag();
            transform.localScale = baseScale * 1.12f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || panel == null)
            {
                return;
            }

            UIDragHandler.isDraggingAnyUI = true;
            panel.DragTo(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            transform.localScale = baseScale;
            ReleaseGameDragLatch();
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

        private void Update()
        {
            // 鼠标在窗口外松开时 EventSystem 不一定会发 PointerUp，这里兜底放掉标记
            if (blockingGameDrag && !Input.GetMouseButton(0))
            {
                ReleaseGameDragLatch();
                return;
            }

            if (blockingGameDrag)
            {
                // 游戏里其它逻辑可能会把标记清掉，按住浮球期间每帧重新支起来
                UIDragHandler.isDraggingAnyUI = true;
            }

            // 万一物品拖拽在浮球按下前已经启动，直接把它掐掉，别让下层物品跟着走
            if (blockingGameDrag && !itemDragDisabled &&
                ItemMouseDragHandler.current != null &&
                ItemMouseDragHandler.current.IsDraggingItem)
            {
                try
                {
                    itemDragDisabled = ItemMouseDragHandler.current.Disable();
                }
                catch (Exception)
                {
                    itemDragDisabled = false;
                }
            }
        }

        private void ReleaseGameDragLatch()
        {
            if (itemDragDisabled)
            {
                itemDragDisabled = false;
                try
                {
                    if (ItemMouseDragHandler.current != null)
                    {
                        ItemMouseDragHandler.current.Enable();
                    }
                }
                catch (Exception)
                {
                    // 场景切换时 handler 可能已经销毁
                }
            }

            if (!blockingGameDrag)
            {
                return;
            }

            blockingGameDrag = false;
            UIDragHandler.isDraggingAnyUI = false;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ReleaseGameDragLatch();
            }
        }

        private void OnDisable()
        {
            transform.localScale = baseScale;
            ReleaseGameDragLatch();
        }
    }
}
