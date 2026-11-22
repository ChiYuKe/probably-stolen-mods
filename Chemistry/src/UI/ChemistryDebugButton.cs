using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chemistry
{
    internal sealed class ChemistryDebugButton : MonoBehaviour
    {
        private string label;
        private CustomUIWindow arrangedWindow;

        internal void Bind(string buttonLabel)
        {
            label = buttonLabel;
        }

        private void LateUpdate()
        {
            CustomUIWindow window = CustomUIManager.Instance?.GetWindow("debug_panel");
            if (window?.IsVisible != true || window == arrangedWindow)
            {
                return;
            }
            arrangedWindow = window;

            Transform chemistryLabel = null;
            Transform moddingLabel = null;
            foreach (TextMeshProUGUI text in window.root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.text == label)
                {
                    chemistryLabel = text.transform;
                }
                else if (text.text == "Modding Panel" || text.text == "模组面板" || text.text == "模組面板")
                {
                    moddingLabel = text.transform;
                }
            }

            if (chemistryLabel == null || moddingLabel == null)
            {
                return;
            }

            Transform list = moddingLabel.GetComponentInParent<ScrollRect>(true)?.content;
            if (list == null || !chemistryLabel.IsChildOf(list) || !moddingLabel.IsChildOf(list))
            {
                return;
            }

            Transform moddingButton = RowUnder(moddingLabel, list);
            Transform chemistryButton = RowUnder(chemistryLabel, list);
            int moddingIndex = moddingButton.GetSiblingIndex();
            int index = chemistryButton.GetSiblingIndex() < moddingIndex ? moddingIndex : moddingIndex + 1;
            chemistryButton.SetSiblingIndex(index);
        }

        private static Transform RowUnder(Transform label, Transform list)
        {
            while (label.parent != list)
            {
                label = label.parent;
            }
            return label;
        }
    }
}
