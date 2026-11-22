using System;
using UnityEngine;

namespace Chemistry
{
    /// <summary>
    /// 常驻对象上的小监听器：每帧看快捷键。模组本体不继承 MonoBehaviour，
    /// 所以单独挂一个宿主，卸载模组时整个对象销毁。
    /// （和 InventorySearch 的写法一致，两个模组各用各的宿主，互不干扰。）
    /// </summary>
    internal sealed class HotkeyWatcher : MonoBehaviour
    {
        private Func<KeyCode> key;
        private Action onPressed;

        public void Bind(Func<KeyCode> keyProvider, Action callback)
        {
            key = keyProvider;
            onPressed = callback;
        }

        private void Update()
        {
            if (key == null || onPressed == null)
            {
                return;
            }

            KeyCode code = key();
            if (code == KeyCode.None || !Input.GetKeyDown(code))
            {
                return;
            }

            // 正在输入框里打字、或者正在改键，都不该被快捷键抢走
            try
            {
                if (StoreUIManager.IsTextFieldFocused() || KeyBindHelper.IsCapturing)
                {
                    return;
                }
            }
            catch
            {
                // 主菜单里没有 StoreUIManager，照常响应
            }

            try
            {
                onPressed();
            }
            catch (Exception e)
            {
                Debug.LogError("[Chemistry] 快捷键回调出错：" + e);
            }
        }
    }
}
