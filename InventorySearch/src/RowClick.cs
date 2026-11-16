using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InventorySearch
{
    /// <summary>
    /// 结果行的点击/悬停响应。行根节点自己挂一张透明 Image 当整行的射线接收面，
    /// 所以点图标、点文字、点空白都能触发。
    /// </summary>
    public sealed class RowClick : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private Action onClick;
        private Image background;
        private Color restingColor;

        public void Init(Image image, Action callback)
        {
            background = image;
            onClick = callback;
            restingColor = image != null ? image.color : Color.clear;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Left && onClick != null)
            {
                onClick();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (background != null)
            {
                background.color = new Color(0.55f, 0.72f, 1f, 0.16f);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (background != null)
            {
                background.color = restingColor;
            }
        }
    }
}
