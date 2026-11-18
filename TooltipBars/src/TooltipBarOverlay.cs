using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace TooltipBars
{
    /// <summary>
    /// 隐藏工具提示里的 [====] 字符，按原字符坐标绘制像素进度条。
    /// </summary>
    public sealed class TooltipBarOverlay : MonoBehaviour
    {
        internal static bool Enabled = true;

        private static readonly FieldInfo TextNodeField = typeof(RichTextElement).GetField(
            "textNode", BindingFlags.Instance | BindingFlags.NonPublic);

        private struct BarData
        {
            public int First;
            public int Last;
            public float Ratio;
            public Color FillColor;
        }


        private readonly List<TooltipBarGraphic> visuals = new List<TooltipBarGraphic>();
        private readonly List<BarData> bars = new List<BarData>();

        private TMP_Text text;
        private string lastText;
        private Vector2 lastSize = new Vector2(-1f, -1f);
        private bool failed;
        private bool charactersHidden;

        internal static void Attach(RichTextElement element)
        {
            if (element == null)
            {
                return;
            }

            TMP_Text target = GetForegroundText(element);
            if (target == null)
            {
                return;
            }

            TooltipBarOverlay overlay = target.GetComponent<TooltipBarOverlay>();
            if (overlay == null)
            {
                overlay = target.gameObject.AddComponent<TooltipBarOverlay>();
            }

            overlay.text = target;
        }

        private static TMP_Text GetForegroundText(RichTextElement element)
        {
            if (element == null || TextNodeField == null)
            {
                return null;
            }

            TreeNodeRender node = TextNodeField.GetValue(element) as TreeNodeRender;
            return node != null ? node.text : null;
        }

        private void LateUpdate()
        {
            try
            {
                Refresh();
            }
            catch (Exception e)
            {
                if (!failed)
                {
                    failed = true;
                    TooltipBarsMod.Log?.Warning("画 UI 进度条失败，保留原版文字条：" + e.Message);
                }

                HideAll();

                try
                {
                    if (text != null && text.isActiveAndEnabled)
                    {
                        text.ForceMeshUpdate();
                    }

                    charactersHidden = false;
                }
                catch
                {
                    // 恢复不了就算了，至少别让异常继续往外冒
                }
            }
        }

        private void Refresh()
        {
            if (!Enabled)
            {
                HideAll();
                RestoreCharacters();
                lastText = null;
                return;
            }

            if (failed)
            {
                return;
            }

            if (text == null)
            {
                text = GetComponent<TMP_Text>();
            }

            if (text == null || !text.isActiveAndEnabled)
            {
                return;
            }

            if (string.IsNullOrEmpty(text.text))
            {
                HideAll();
                lastText = null;
                return;
            }

            Vector2 size = text.rectTransform.rect.size;
            if (text.text == lastText && size == lastSize && !text.havePropertiesChanged)
            {
                return;
            }

            lastText = text.text;
            lastSize = size;

            // 布局/文本变了，重算一次字符位置，颜色也会恢复，需要重新隐藏
            text.ForceMeshUpdate();
            charactersHidden = false;

            CollectBars();
            if (bars.Count == 0)
            {
                HideAll();
                return;
            }

            HideCharacters();

            for (int i = 0; i < bars.Count; i++)
            {
                Apply(i, bars[i]);
            }

            for (int i = bars.Count; i < visuals.Count; i++)
            {
                visuals[i].gameObject.SetActive(false);
            }
        }

        private void CollectBars()
        {
            bars.Clear();

            TMP_TextInfo info = text.textInfo;
            if (info == null)
            {
                return;
            }

            int count = info.characterCount;
            for (int i = 0; i < count; i++)
            {
                TMP_CharacterInfo open = info.characterInfo[i];
                if (open.character != '[' || !open.isVisible)
                {
                    continue;
                }

                int j = i + 1;
                int filled = 0;
                int empty = 0;
                bool valid = true;

                while (j < count && info.characterInfo[j].character != ']')
                {
                    char c = info.characterInfo[j].character;
                    if (c == '=')
                    {
                        filled++;
                    }
                    else if (c == ' ')
                    {
                        empty++;
                    }
                    else
                    {
                        valid = false;
                        break;
                    }

                    j++;
                }

                if (!valid || j >= count || !info.characterInfo[j].isVisible ||
                    info.characterInfo[j].lineNumber != open.lineNumber)
                {
                    continue;
                }

                int total = filled + empty;
                if (total < 3)
                {
                    continue;
                }

                BarData bar = new BarData
                {
                    First = i,
                    Last = j,
                    Ratio = (float)filled / total,
                    FillColor = ReadFillColor(open)
                };

                bars.Add(bar);
                i = j;
            }
        }

        private Color ReadFillColor(TMP_CharacterInfo characterInfo)
        {
            try
            {
                TMP_TextInfo info = text.textInfo;
                int material = characterInfo.materialReferenceIndex;
                int vertex = characterInfo.vertexIndex;

                if (info != null && material >= 0 && material < info.meshInfo.Length)
                {
                    Color32[] colors = info.meshInfo[material].colors32;
                    if (colors != null && vertex >= 0 && vertex + 3 < colors.Length)
                    {
                        return colors[vertex];
                    }
                }
            }
            catch
            {
                // 取不到颜色就用默认青色
            }

            return new Color(0.27f, 0.67f, 0.91f, 1f);
        }

        /// <summary>
        /// 把原来的 [====] 字符顶点全部设成透明，字还在、宽度还在，只是看不见。
        /// </summary>
        private void HideCharacters()
        {
            TMP_TextInfo info = text.textInfo;
            if (info == null)
            {
                return;
            }

            bool changed = false;

            for (int b = 0; b < bars.Count; b++)
            {
                for (int i = bars[b].First; i <= bars[b].Last; i++)
                {
                    TMP_CharacterInfo character = info.characterInfo[i];
                    int material = character.materialReferenceIndex;
                    int vertex = character.vertexIndex;

                    if (!character.isVisible || material < 0 || material >= info.meshInfo.Length || vertex < 0)
                    {
                        continue;
                    }

                    Color32[] colors = info.meshInfo[material].colors32;
                    if (colors == null || vertex + 3 >= colors.Length)
                    {
                        continue;
                    }

                    Color32 clear = new Color32(0, 0, 0, 0);
                    colors[vertex] = clear;
                    colors[vertex + 1] = clear;
                    colors[vertex + 2] = clear;
                    colors[vertex + 3] = clear;
                    changed = true;
                }
            }

            if (changed)
            {
                charactersHidden = true;
                text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            }
        }

        private void RestoreCharacters()
        {
            if (!charactersHidden)
            {
                return;
            }

            charactersHidden = false;

            if (text != null && text.isActiveAndEnabled)
            {
                // 重新生成一次网格，颜色就回来了
                text.ForceMeshUpdate();
            }
        }

        private void Apply(int index, BarData bar)
        {
            TMP_TextInfo info = text.textInfo;
            TMP_CharacterInfo open = info.characterInfo[bar.First];
            TMP_CharacterInfo close = info.characterInfo[bar.Last];

            float x0 = open.bottomLeft.x;
            float x1 = close.topRight.x;
            float width = Mathf.Max(4f, x1 - x0);

            // 细条：占字高的 70%，上下居中；比之前略粗一点，但不撑满整行
            float glyphBottom = open.bottomLeft.y;
            float glyphTop = open.topRight.y;
            float glyphHeight = Mathf.Max(4f, glyphTop - glyphBottom);
            float height = Mathf.Clamp(glyphHeight * 0.70f, 8f, 14f);
            float y = glyphBottom + (glyphHeight - height) * 0.5f;

            TooltipBarGraphic visual = GetVisual(index);
            RectTransform root = visual.rectTransform;
            root.localPosition = new Vector3(x0, y, 0f);
            root.sizeDelta = new Vector2(width, height);

            visual.SetProgress(bar.Ratio, bar.FillColor);
            root.gameObject.SetActive(true);
        }

        private TooltipBarGraphic GetVisual(int index)
        {
            while (visuals.Count <= index)
            {
                visuals.Add(CreateVisual());
            }

            return visuals[index];
        }

        private TooltipBarGraphic CreateVisual()
        {
            GameObject rootObject = new GameObject("TooltipProgressBar", typeof(RectTransform),
                typeof(TooltipBarGraphic));
            RectTransform root = (RectTransform)rootObject.transform;
            root.SetParent(transform, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.zero;
            root.pivot = Vector2.zero;

            TooltipBarGraphic graphic = rootObject.GetComponent<TooltipBarGraphic>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private void HideAll()
        {
            for (int i = 0; i < visuals.Count; i++)
            {
                if (visuals[i] != null)
                {
                    visuals[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
