using System;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ManualSave
{
    [HarmonyPatch(typeof(EscapeUIManager), nameof(EscapeUIManager.OpenEscapeMenu))]
    internal static class PauseMenuSaveButton
    {
        private const string ButtonName = "ManualSaveButton";

        private static void Postfix(EscapeUIManager __instance)
        {
            try
            {
                Install(__instance);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ManualSave] 暂停菜单按钮创建失败：" + e.Message);
            }
        }

        private static void Install(EscapeUIManager manager)
        {
            if (manager == null || manager.menuPanel == null)
            {
                return;
            }

            Transform panel = manager.menuPanel.transform;
            if (manager.menuPanel.GetComponentsInChildren<Transform>(true)
                .Any(transform => transform.name == ButtonName))
            {
                return;
            }

            Button[] buttons = manager.menuPanel.GetComponentsInChildren<Button>(true);
            Button sourceButton = buttons
                .Where(button => button != null && button.transform.parent is RectTransform)
                .OrderByDescending(button =>
                {
                    RectTransform rect = FindButtonRow(button.transform, panel) as RectTransform;
                    return rect != null ? rect.anchoredPosition.y : float.MinValue;
                })
                .FirstOrDefault();
            var sourceRect = sourceButton != null ? sourceButton.transform as RectTransform : null;
            if (sourceRect == null)
            {
                Debug.LogWarning("[ManualSave] 找不到可复制的暂停菜单按钮。");
                return;
            }

            Transform row = FindButtonRow(sourceButton.transform, panel);
            var rowRect = row as RectTransform;
            if (rowRect == null || row.parent == null)
            {
                Debug.LogWarning("[ManualSave] 找不到暂停菜单按钮所在行。");
                return;
            }

            Transform buttonList = row.parent;
            float rowStep = rowRect.rect.height + 7f;
            float topY = rowRect.anchoredPosition.y;
            GameObject copy = UnityEngine.Object.Instantiate(row.gameObject, buttonList, false);
            copy.name = ButtonName;
            copy.transform.SetSiblingIndex(row.GetSiblingIndex() + 1);

            var copyRect = copy.transform as RectTransform;
            if (copyRect == null)
            {
                UnityEngine.Object.Destroy(copy);
                return;
            }

            copyRect.anchoredPosition = new Vector2(rowRect.anchoredPosition.x, topY - rowStep);

            // 克隆按钮会连带复制原按钮的持久化点击事件；换成新的 Button，避免点击时执行原操作。
            Button clonedButton = copy.GetComponentInChildren<Button>(true);
            Graphic targetGraphic = clonedButton != null ? clonedButton.targetGraphic : null;
            Transform buttonTransform = clonedButton != null ? clonedButton.transform : copy.transform;
            if (clonedButton != null) UnityEngine.Object.DestroyImmediate(clonedButton);
            Button saveButton = buttonTransform.gameObject.AddComponent<Button>();
            saveButton.targetGraphic = targetGraphic;
            saveButton.onClick.AddListener(SaveCurrentGame);

            // The game's pause labels use LocalizeStringEvent + TMPLocalizer.
            // A cloned Continue row would otherwise refresh back to its original translation.
            foreach (MonoBehaviour component in copy.GetComponentsInChildren<MonoBehaviour>(true))
            {
                string typeName = component.GetType().Name;
                if (typeName == "TMPLocalizer" || typeName == "LocalizeStringEvent")
                {
                    UnityEngine.Object.DestroyImmediate(component);
                }
            }

            TMP_Text label = copy.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = "手动存档";
            }
            else
            {
                Text legacyLabel = copy.GetComponentInChildren<Text>(true);
                if (legacyLabel != null)
                {
                    legacyLabel.text = "手动存档";
                }
            }

            foreach (Transform sibling in buttonList)
            {
                if (sibling == copy.transform || sibling.GetComponentInChildren<Button>(true) == null)
                {
                    continue;
                }

                var rect = sibling as RectTransform;
                if (rect != null && rect.anchoredPosition.y < topY - 0.5f)
                {
                    rect.anchoredPosition -= new Vector2(0f, rowStep);
                }
            }

            RectTransform buttonListRect = buttonList as RectTransform;
            if (buttonListRect != null)
            {
                buttonListRect.sizeDelta += new Vector2(0f, rowStep);
            }
        }

        private static Transform FindButtonRow(Transform button, Transform stopAt)
        {
            Transform candidate = button;
            while (candidate != null && candidate != stopAt)
            {
                Button[] rowButtons = candidate.GetComponentsInChildren<Button>(true);
                if (rowButtons.Length == 1 &&
                    (candidate.GetComponentInChildren<TMP_Text>(true) != null ||
                     candidate.GetComponentInChildren<Text>(true) != null))
                {
                    return candidate;
                }
                candidate = candidate.parent;
            }
            return button;
        }

        private static void SaveCurrentGame()
        {
            try
            {
                if (!PlayerStore.HasInstance || PlayerStore.Instance.saveSlotId < 0 || EmporiumEntry.Instance == null)
                {
                    if (StoreUIManager.Instance != null)
                    {
                        StoreUIManager.Instance.Notify("当前没有可保存的游戏存档。", "default");
                    }
                    return;
                }

                PlayerStore.Instance.SaveGame();
                if (StoreUIManager.Instance != null)
                {
                    StoreUIManager.Instance.Notify("游戏已保存。", "default");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[ManualSave] 手动保存失败：" + e);
                if (StoreUIManager.Instance != null)
                {
                    StoreUIManager.Instance.Notify("存档失败，请查看游戏日志。", "sec");
                }
            }
        }
    }
}
