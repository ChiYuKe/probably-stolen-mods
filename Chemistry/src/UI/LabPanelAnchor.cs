using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chemistry
{
    /// <summary>
    /// 把实验室入口和操作面板放到各自的屏幕锚点，覆盖地点系统的初始位置。
    ///
    /// 入口窗口（BuildLauncher）还负责摘掉 MapUIManager.OpenLocationPanel 在
    /// buildPanel 之后无条件补的那个「返回」按钮（afterhour_return_action，回调
    /// MapUIManager.Return）：实验室里已有铁门可点击返店（LabReturnDoorButton），
    /// 面板上这个按钮是多余的。
    ///
    /// 实现说明：CustomUIBuilder.Spawn 只把「显式带 id」的元素注册进
    /// CustomUIWindow.elements，而游戏补返回按钮用的是 AddButton(text, onClick, null)，
    /// 压根不在注册表里——所以只能直接扫窗口层级里的 Button 组件：优先按文本
    /// 匹配（按钮文本就是 afterhour_return_action 的本地化值），兜底取添加顺序
    /// 最后一个非关闭按钮。游戏是在我们 buildPanel 返回之后才补按钮，所以等
    /// 窗口显示后（LateUpdate）再动手。
    /// </summary>
    internal sealed class LabPanelAnchor : MonoBehaviour
    {
        public Vector2 Size = new Vector2(1000f, 740f);
        public Vector2 Anchor = new Vector2(0.5f, 0.5f);
        public int Frames = 6;

        /// <summary>要处理的窗口（入口窗口用）；为空时只做锚定。</summary>
        public CustomUIWindow Window;

        /// <summary>是否摘掉游戏自动补的「返回」按钮（仅入口窗口用）。</summary>
        public bool RemoveAfterhourReturnButton;

        private bool returnButtonRemoved;

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

                if (RemoveAfterhourReturnButton && !returnButtonRemoved && Window != null && Window.IsAlive)
                {
                    RemoveAfterhourReturn();
                }
            }
            catch
            {
                // 窗口已经被关掉就算了
            }

            Frames--;
            if (Frames <= 0)
            {
                if (RemoveAfterhourReturnButton && !returnButtonRemoved)
                {
                    ChemistryMod.Log?.Warning("实验室入口：未能移除地点面板的返回按钮（候选元素没找到），按钮保留。");
                }

                Destroy(this);
            }
        }

        private void RemoveAfterhourReturn()
        {
            // 返回按钮的文本来自 afterhour_return_action，与按钮上的 TMP 文本按值比较
            //（截图里的乱码只是按钮字体缺字形，字符串本身没问题）
            string expected = null;
            try
            {
                expected = LocHelper.GetLocalizedUI("afterhour_return_action");
            }
            catch
            {
                expected = null;
            }

            CustomUIWindowFrame frame = Window.frame;
            Transform scope = frame != null && frame.contentRoot != null
                ? frame.contentRoot
                : Window.root.transform;
            Button closeButton = frame != null ? frame.closeButton : null;

            Button match = null;
            Button last = null;
            Button[] buttons = scope.GetComponentsInChildren<Button>(true);
            foreach (Button candidate in buttons)
            {
                if (candidate == null || candidate == closeButton)
                {
                    continue;
                }

                if (match == null && expected != null)
                {
                    TMP_Text label = candidate.GetComponentInChildren<TMP_Text>(true);
                    if (label != null && label.text == expected)
                    {
                        match = candidate;
                    }
                }

                // GetComponentsInChildren 是深度优先序 = 添加顺序，游戏最后补的在最后
                last = candidate;
            }

            // 文本匹配不上（本地化实现变化？）就按添加顺序兜底
            Button target = match != null ? match : last;
            if (target == null)
            {
                return; // 没找到就下一帧再试，Frames 耗尽后打警告
            }

            RectTransform removed = target.transform as RectTransform;
            float freedHeight = removed != null ? removed.rect.height : 0f;
            target.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(target.gameObject);
            returnButtonRemoved = true;

            if (freedHeight > 0f)
            {
                // 游戏 AddSpace(8f) + 按钮一起算进腾出的高度，窗口别留一截空白
                Size = new Vector2(Size.x, Mathf.Max(40f, Size.y - freedHeight - 8f));
            }

            ChemistryMod.Log?.Log("实验室入口：已移除地点面板自动补的返回按钮（" +
                (match != null ? "按文本匹配" : "按添加顺序兜底") + "）。");
        }
    }
}
