using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DebugPanelChinese
{
    /// <summary>
    /// 调试面板是运行时用代码搭出来的，文字直接写死在控件上，没有走游戏的本地化表。
    /// 这里不改游戏代码，只在窗口显示时按对照表把文字换掉，顺便把字体换成中文字体。
    /// </summary>
    internal sealed class DebugPanelLocalizer : MonoBehaviour
    {
        private static readonly string[] WindowIds =
        {
            "debug_panel",
            "debug_panel_modding",
            "debug_panel_marketing",
            "debug_panel_story",
            "debug_item_spawner"
        };

        private const float ScanInterval = 0.2f;
        private const float TooltipScanInterval = 0.5f;

        private float nextScan;
        private float nextTooltipScan;

        private TMP_FontAsset chineseFont;
        private float nextFontSearch;
        private bool fontWarningLogged;
        private bool fontLogged;

        private TextMeshProUGUI tooltipText;
        private bool tooltipPrefabChecked;
        private bool anyWindowVisible;

        private void LateUpdate()
        {
            if (CustomUIManager.Instance == null)
            {
                return;
            }

            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + ScanInterval;
                ScanWindows();
            }

            if (anyWindowVisible && Time.unscaledTime >= nextTooltipScan)
            {
                nextTooltipScan = Time.unscaledTime + TooltipScanInterval;
                FixTooltipFont();
            }
        }

        private void ScanWindows()
        {
            anyWindowVisible = false;

            for (int i = 0; i < WindowIds.Length; i++)
            {
                CustomUIWindow window = CustomUIManager.Instance.GetWindow(WindowIds[i]);
                if (window == null || window.root == null || !window.root.activeInHierarchy)
                {
                    continue;
                }

                anyWindowVisible = true;
                TranslateHierarchy(window.root.transform);
            }
        }

        private void TranslateHierarchy(Transform root)
        {
            TMP_InputField[] inputs = root.GetComponentsInChildren<TMP_InputField>(true);
            TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);

            for (int i = 0; i < texts.Length; i++)
            {
                TextMeshProUGUI text = texts[i];
                if (text == null || IsEditableInputText(text, inputs))
                {
                    continue;
                }

                string raw = text.text;
                string translated = TranslationTable.ForText(raw);
                if (translated != null && translated != raw)
                {
                    if (!ApplyFont(text))
                    {
                        // 没有能写中文的字体就先别换，免得整面板都是方块
                        continue;
                    }

                    text.text = translated;
                    continue;
                }

                // mod 自己加进来的中文按钮（比如能量棒）也要换字体，不然会是方块
                if (ContainsCjk(raw))
                {
                    ApplyFont(text);
                }
            }

            CustomUITooltipTrigger[] triggers = root.GetComponentsInChildren<CustomUITooltipTrigger>(true);
            for (int i = 0; i < triggers.Length; i++)
            {
                CustomUITooltipTrigger trigger = triggers[i];
                if (trigger == null || string.IsNullOrEmpty(trigger.tooltipText))
                {
                    continue;
                }

                string translated = TranslationTable.ForTooltip(trigger.tooltipText);
                if (translated != null && translated != trigger.tooltipText)
                {
                    trigger.tooltipText = translated;
                }
            }
        }

        private static bool IsEditableInputText(TextMeshProUGUI text, TMP_InputField[] inputs)
        {
            for (int i = 0; i < inputs.Length; i++)
            {
                TMP_InputField input = inputs[i];
                if (input != null && ReferenceEquals(input.textComponent, text))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsCjk(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if ((c >= '\u4E00' && c <= '\u9FFF') ||
                    (c >= '\u3400' && c <= '\u4DBF') ||
                    (c >= '\u3000' && c <= '\u303F') ||
                    (c >= '\uFF00' && c <= '\uFFEF'))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ApplyFont(TextMeshProUGUI text)
        {
            TMP_FontAsset font = ResolveChineseFont();
            if (font == null)
            {
                return false;
            }

            if (text.font != font)
            {
                text.font = font;
            }

            return true;
        }

        private TMP_FontAsset ResolveChineseFont()
        {
            if (chineseFont != null)
            {
                return chineseFont;
            }

            FontLoader loader = FontLoader.Instance;
            if (loader != null)
            {
                if (CanRenderChinese(loader.simplifiedChineseFont))
                {
                    return SelectFont(loader.simplifiedChineseFont);
                }

                if (CanRenderChinese(loader.simplifiedChineseHandwrittenFont))
                {
                    return SelectFont(loader.simplifiedChineseHandwrittenFont);
                }
            }

            if (Time.unscaledTime < nextFontSearch)
            {
                return null;
            }

            nextFontSearch = Time.unscaledTime + 5f;

            TMP_FontAsset[] fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();

            // 先找名字像中文字体的，找不到再退而求其次，只要能写“中”就行
            for (int i = 0; i < fonts.Length; i++)
            {
                TMP_FontAsset font = fonts[i];
                if (font != null && LooksLikeChineseFont(font.name) && CanRenderChinese(font))
                {
                    return SelectFont(font);
                }
            }

            for (int i = 0; i < fonts.Length; i++)
            {
                TMP_FontAsset font = fonts[i];
                if (font != null && CanRenderChinese(font))
                {
                    return SelectFont(font);
                }
            }

            if (!fontWarningLogged)
            {
                fontWarningLogged = true;
                Debug.LogWarning("[调试面板汉化] 没找到中文字体，界面文字可能显示成方块。");
            }

            return null;
        }

        private TMP_FontAsset SelectFont(TMP_FontAsset font)
        {
            chineseFont = font;

            if (!fontLogged)
            {
                fontLogged = true;
                Debug.Log("[调试面板汉化] 使用中文字体: " + font.name);
            }

            return font;
        }

        private static bool CanRenderChinese(TMP_FontAsset font)
        {
            if (font == null)
            {
                return false;
            }

            try
            {
                return font.HasCharacter('中', false, true) ||
                       font.HasCharacter('文', false, true);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool LooksLikeChineseFont(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string lower = name.ToLowerInvariant();
            return lower.Contains("chinese") ||
                   lower.Contains("simplified") ||
                   lower.Contains("cjk") ||
                   lower.Contains("noto sans sc") ||
                   lower.Contains("notosanssc");
        }

        private void ApplyFontToTooltipPrefab()
        {
            if (tooltipPrefabChecked)
            {
                return;
            }

            tooltipPrefabChecked = true;

            CustomUIManager manager = CustomUIManager.Instance;
            if (manager == null || manager.tooltipPrefab == null)
            {
                return;
            }

            TextMeshProUGUI prefabText =
                manager.tooltipPrefab.GetComponentInChildren<TextMeshProUGUI>(true);
            if (prefabText != null)
            {
                prefabText.font = chineseFont;
            }
        }

        private void FixTooltipFont()
        {
            if (chineseFont == null && ResolveChineseFont() == null)
            {
                return;
            }

            ApplyFontToTooltipPrefab();

            if (tooltipText == null)
            {
                GameObject tooltip = GameObject.Find("custom_ui_tooltip");
                if (tooltip == null)
                {
                    return;
                }

                tooltipText = tooltip.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tooltipText == null)
                {
                    return;
                }
            }

            if (tooltipText.font != chineseFont)
            {
                tooltipText.font = chineseFont;

                LayoutElement layout = tooltipText.GetComponent<LayoutElement>();
                if (layout != null)
                {
                    layout.preferredWidth = Mathf.Min(
                        tooltipText.GetPreferredValues(tooltipText.text).x + 1f, 360f);
                }

                Transform tooltipRoot = tooltipText.transform;
                while (tooltipRoot.parent != null && tooltipRoot.name != "custom_ui_tooltip")
                {
                    tooltipRoot = tooltipRoot.parent;
                }

                RectTransform rect = tooltipRoot as RectTransform;
                if (rect != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                }
            }
        }
    }
}
