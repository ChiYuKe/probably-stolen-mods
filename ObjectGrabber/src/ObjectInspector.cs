using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ObjectGrabber
{
    internal sealed class InspectResult
    {
        public string Header;
        public string Body;
        public GameObject Target;

        public static InspectResult Make(string header, IEnumerable<string> lines, GameObject target = null)
        {
            StringBuilder builder = new StringBuilder();
            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(line);
            }

            return new InspectResult
            {
                Header = string.IsNullOrEmpty(header) ? "对象获取器" : header,
                Body = builder.ToString(),
                Target = target
            };
        }
    }

    /// <summary>
    /// 把浮球下面的东西翻译成一段给人看的文字。
    /// 优先走游戏自己的像素元素拾取（物品、机器、角色、窗口都在这套系统里），
    /// 没有像素元素时再看普通 UI，最后看 2D 场景碰撞体。
    /// </summary>
    internal static class ObjectInspector
    {
        public static InspectResult Inspect(Vector2 screenPosition, Transform selfRoot)
        {
            if (EventSystem.current == null)
            {
                return Hint("这个场景没有 EventSystem，暂时没法拾取对象。");
            }

            try
            {
                PixelElement element = null;
                try
                {
                    element = RenderHandler.RaycastElement<PixelElement>(screenPosition);
                }
                catch (Exception)
                {
                    // 拾取失败就继续走 UI / 场景兜底
                }

                RaycastResult? topUi = TopUi(screenPosition, selfRoot);

                if (topUi.HasValue && topUi.Value.gameObject != null)
                {
                    GameObject hitObject = topUi.Value.gameObject;
                    TreeNodeRender render = hitObject.GetComponentInParent<TreeNodeRender>();
                    if (render != null)
                    {
                        if (element == null)
                        {
                            element = SafeFindElement(render);
                        }
                    }
                    else if (IsUsefulUi(hitObject))
                    {
                        return FromUi(hitObject);
                    }
                }

                if (element != null)
                {
                    return FromElement(element);
                }

                if (topUi.HasValue && topUi.Value.gameObject != null)
                {
                    return FromUi(topUi.Value.gameObject);
                }

                Collider2D collider = FindWorldCollider(screenPosition);
                if (collider != null)
                {
                    return FromWorld(collider);
                }

                return Hint("浮球下面没有可识别的对象。\n把浮球拖到物品、机器、角色或界面控件上。");
            }
            catch (Exception e)
            {
                return Hint("读取对象时出错：\n" + Clean(e.Message, 160));
            }
        }

        private static RaycastResult? TopUi(Vector2 screenPosition, Transform selfRoot)
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            for (int i = 0; i < results.Count; i++)
            {
                GameObject hitObject = results[i].gameObject;
                if (hitObject == null)
                {
                    continue;
                }

                Transform hitTransform = hitObject.transform;
                if (selfRoot != null &&
                    (hitTransform == selfRoot || hitTransform.IsChildOf(selfRoot)))
                {
                    continue;
                }

                return results[i];
            }

            return null;
        }

        private static bool IsUsefulUi(GameObject hitObject)
        {
            return hitObject.GetComponent<Selectable>() != null ||
                   hitObject.GetComponent<TMP_Text>() != null ||
                   hitObject.GetComponent<TMP_InputField>() != null ||
                   hitObject.GetComponent<ScrollRect>() != null ||
                   hitObject.GetComponent<CustomUITooltipTrigger>() != null;
        }

        private static PixelElement SafeFindElement(TreeNodeRender render)
        {
            try
            {
                return render.FindParentElement();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static InspectResult FromElement(PixelElement element)
        {
            List<string> lines = new List<string>();
            GameObject renderObject = null;
            TreeNodeRender render = null;

            try
            {
                render = element.handler;
                if (render != null)
                {
                    renderObject = render.gameObject;
                }
            }
            catch (Exception)
            {
                // 有些元素还没初始化完，忽略
            }

            string header = "游戏元素";
            string type = "游戏元素";

            GameItem item = element as GameItem;
            PixelWindow window = element as PixelWindow;

            if (item != null)
            {
                type = item is GameCharacterItem ? "角色" : "物品";
                header = Clean(item.GetDisplayName(), 24);
                if (string.IsNullOrEmpty(header))
                {
                    header = "物品";
                }

                Line(lines, "类型", type);
                Line(lines, "物品 ID", Clean(item.identifier, 90));
                Line(lines, "唯一 ID", item.uniqueId.ToString());
                Line(lines, "内部名", Clean(item.identifierName, 90));
                Line(lines, "数量", item.unitCount.ToString());
                Line(lines, "单价", item.unitValue.ToString("N0"));
                Line(lines, "物品类型", Clean(Join(item.itemTypes), 140));
                Line(lines, "特性数量", item.itemFeatures == null ? "" : item.itemFeatures.Count + " 个");
                Line(lines, "描述", Clean(item.shortDescription, 180));
                Line(lines, "精灵", Clean(item.spritePath, 120));

                GameCharacterItem character = item as GameCharacterItem;
                if (character != null)
                {
                    try
                    {
                        Line(lines, "生命", character.health + " / " + character.maxHealth);
                        Line(lines, "士气", character.currentMorale + " / " + character.maxMorale);
                        Line(lines, "反应", character.reaction.ToString());
                        Line(lines, "状态", character.isDead ? "已死亡" : "存活");
                    }
                    catch (Exception)
                    {
                        // 角色数据没初始化完就跳过
                    }
                }
            }
            else if (window != null)
            {
                type = "窗口";
                header = Clean(window.titleString, 24);
                if (string.IsNullOrEmpty(header))
                {
                    header = "窗口";
                }

                Line(lines, "类型", type);
                Line(lines, "窗口标题", Clean(window.titleString, 90));
                Line(lines, "窗口尺寸", window.widthPixels + " x " + window.heightPixels);
            }
            else
            {
                Line(lines, "类型", type);
                TooltipListener tooltip = element as TooltipListener;
                if (tooltip != null)
                {
                    try
                    {
                        RichText richText = tooltip.GetTooltip();
                        if (richText != null)
                        {
                            Line(lines, "提示", Clean(richText.nakedText, 260));
                        }
                    }
                    catch (Exception)
                    {
                        // 提示没准备好就算了
                    }
                }
            }

            if (renderObject != null)
            {
                Line(lines, "渲染对象", Clean(renderObject.name, 90));
                Line(lines, "层级", Clean(Path(renderObject.transform), 130));
                Line(lines, "实例 ID", renderObject.GetInstanceID().ToString());
                Line(lines, "激活", renderObject.activeInHierarchy ? "是" : "否");
                Line(lines, "场景", SceneName(renderObject));

                try
                {
                    if (render != null && render.window != null)
                    {
                        Line(lines, "所在窗口", Clean(render.window.titleString, 70));
                    }
                }
                catch (Exception)
                {
                    // 窗口引用可能已经销毁
                }

                Line(lines, "组件", ComponentSummary(renderObject));
            }

            try
            {
                if (element.rectTransform != null)
                {
                    Line(lines, "UI 位置", Vec(element.rectTransform.anchoredPosition));
                }
            }
            catch (Exception)
            {
                // 元素没有 RectTransform
            }

            GameObject targetObject = renderObject;
            if (targetObject == null)
            {
                try
                {
                    RectTransform rectTransform = element.rectTransform;
                    if (rectTransform != null)
                    {
                        targetObject = rectTransform.gameObject;
                    }
                }
                catch (Exception)
                {
                    // 元素没有可用的渲染节点
                }
            }

            return InspectResult.Make(header, lines, targetObject);
        }

        private static InspectResult FromUi(GameObject hitObject)
        {
            List<string> lines = new List<string>();
            TMP_Text text = hitObject.GetComponent<TMP_Text>();
            if (text == null)
            {
                text = hitObject.GetComponentInChildren<TMP_Text>(true);
            }

            string header = Clean(hitObject.name, 28);
            if (text != null && !string.IsNullOrEmpty(text.text) &&
                header.IndexOf("Text", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                header = Clean(text.text, 42);
            }

            if (string.IsNullOrEmpty(header))
            {
                header = "界面控件";
            }

            Line(lines, "类型", "界面控件");
            Line(lines, "对象名", Clean(hitObject.name, 90));
            Line(lines, "层级", Clean(Path(hitObject.transform), 130));
            Line(lines, "实例 ID", hitObject.GetInstanceID().ToString());
            Line(lines, "激活", hitObject.activeInHierarchy ? "是" : "否");
            Line(lines, "图层", LayerName(hitObject));
            Line(lines, "标签", TagName(hitObject));
            Line(lines, "场景", SceneName(hitObject));

            RectTransform rect = hitObject.transform as RectTransform;
            if (rect != null)
            {
                Line(lines, "UI 位置", Vec(rect.anchoredPosition));
                Line(lines, "UI 尺寸", Vec(rect.rect.size));
            }
            else
            {
                Line(lines, "位置", Vec(hitObject.transform.position));
            }

            if (text != null)
            {
                Line(lines, "文本", Clean(text.text, 240));
            }

            Image image = hitObject.GetComponent<Image>();
            if (image != null && image.sprite != null)
            {
                Line(lines, "图片", Clean(image.sprite.name, 90));
            }

            Button button = hitObject.GetComponent<Button>();
            if (button != null)
            {
                Line(lines, "按钮", button.interactable ? "可点击" : "已禁用");
            }

            Canvas canvas = hitObject.GetComponent<Canvas>();
            if (canvas != null)
            {
                Line(lines, "Canvas 排序", canvas.sortingOrder.ToString());
            }

            Line(lines, "组件", ComponentSummary(hitObject));
            return InspectResult.Make(header, lines, hitObject);
        }

        private static InspectResult FromWorld(Collider2D collider)
        {
            GameObject hitObject = collider.gameObject;
            List<string> lines = new List<string>();

            string header = Clean(hitObject.name, 28);
            if (string.IsNullOrEmpty(header))
            {
                header = "场景对象";
            }

            Line(lines, "类型", "场景对象");
            Line(lines, "对象名", Clean(hitObject.name, 90));
            Line(lines, "层级", Clean(Path(hitObject.transform), 130));
            Line(lines, "实例 ID", hitObject.GetInstanceID().ToString());
            Line(lines, "激活", hitObject.activeInHierarchy ? "是" : "否");
            Line(lines, "图层", LayerName(hitObject));
            Line(lines, "标签", TagName(hitObject));
            Line(lines, "场景", SceneName(hitObject));
            Line(lines, "位置", Vec(hitObject.transform.position));
            Line(lines, "碰撞体", ColliderName(collider));

            SpriteRenderer spriteRenderer = hitObject.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                if (spriteRenderer.sprite != null)
                {
                    Line(lines, "精灵", Clean(spriteRenderer.sprite.name, 90));
                }

                Line(lines, "排序层", spriteRenderer.sortingLayerName + " / " + spriteRenderer.sortingOrder);
            }

            Line(lines, "组件", ComponentSummary(hitObject));
            return InspectResult.Make(header, lines, hitObject);
        }

        private static Collider2D FindWorldCollider(Vector2 screenPosition)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return null;
            }

            try
            {
                Vector3 world = camera.ScreenToWorldPoint(
                    new Vector3(screenPosition.x, screenPosition.y, Mathf.Abs(camera.transform.position.z)));
                Collider2D[] hits = Physics2D.OverlapPointAll(new Vector2(world.x, world.y));

                Collider2D best = null;
                int bestOrder = int.MinValue;

                for (int i = 0; i < hits.Length; i++)
                {
                    Collider2D hit = hits[i];
                    if (hit == null)
                    {
                        continue;
                    }

                    SpriteRenderer renderer = hit.GetComponent<SpriteRenderer>();
                    int order = renderer != null ? renderer.sortingOrder : 0;
                    if (best == null || order > bestOrder)
                    {
                        best = hit;
                        bestOrder = order;
                    }
                }

                return best;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static InspectResult Hint(string message)
        {
            return InspectResult.Make("对象获取器", new[]
            {
                "<color=#9A9AA6>拖到对象上</color>",
                "",
                message
            });
        }

        private static void Line(List<string> lines, string label, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            lines.Add("<color=#8F8F9B>" + label + "</color>  <color=#E8E8EE>" + value + "</color>");
        }

        private static string ComponentSummary(GameObject hitObject)
        {
            List<string> parts = new List<string>();

            AddComponent<RectTransform>(parts, hitObject, "RectTransform");
            if (hitObject.GetComponent<RectTransform>() == null)
            {
                AddComponent<Transform>(parts, hitObject, "Transform");
            }

            AddComponent<CanvasRenderer>(parts, hitObject, "CanvasRenderer");
            AddComponent<Image>(parts, hitObject, "Image");
            AddComponent<RawImage>(parts, hitObject, "RawImage");
            AddComponent<TMP_Text>(parts, hitObject, "TMP_Text");
            AddComponent<TMP_InputField>(parts, hitObject, "TMP_InputField");
            AddComponent<Button>(parts, hitObject, "Button");
            AddComponent<Toggle>(parts, hitObject, "Toggle");
            AddComponent<Slider>(parts, hitObject, "Slider");
            AddComponent<ScrollRect>(parts, hitObject, "ScrollRect");
            AddComponent<Canvas>(parts, hitObject, "Canvas");
            AddComponent<CanvasGroup>(parts, hitObject, "CanvasGroup");
            AddComponent<RectMask2D>(parts, hitObject, "RectMask2D");
            AddComponent<Mask>(parts, hitObject, "Mask");
            AddComponent<TreeNodeRender>(parts, hitObject, "TreeNodeRender");
            AddComponent<CustomUITooltipTrigger>(parts, hitObject, "CustomUITooltipTrigger");
            AddComponent<UIDragHandler>(parts, hitObject, "UIDragHandler");
            AddComponent<SpriteRenderer>(parts, hitObject, "SpriteRenderer");
            AddComponent<Collider2D>(parts, hitObject, "Collider2D");
            AddComponent<Rigidbody2D>(parts, hitObject, "Rigidbody2D");

            if (parts.Count == 0)
            {
                return "无常用组件";
            }

            if (parts.Count > 10)
            {
                parts.RemoveRange(10, parts.Count - 10);
                parts.Add("…");
            }

            return string.Join(", ", parts);
        }

        private static void AddComponent<T>(List<string> parts, GameObject hitObject, string label)
            where T : Component
        {
            if (hitObject.GetComponent<T>() != null)
            {
                parts.Add(label);
            }
        }

        private static string ColliderName(Collider2D collider)
        {
            if (collider is BoxCollider2D)
            {
                return "BoxCollider2D";
            }

            if (collider is CircleCollider2D)
            {
                return "CircleCollider2D";
            }

            if (collider is CapsuleCollider2D)
            {
                return "CapsuleCollider2D";
            }

            if (collider is PolygonCollider2D)
            {
                return "PolygonCollider2D";
            }

            if (collider is EdgeCollider2D)
            {
                return "EdgeCollider2D";
            }

            return "Collider2D";
        }

        private static string LayerName(GameObject hitObject)
        {
            string name = LayerMask.LayerToName(hitObject.layer);
            return string.IsNullOrEmpty(name) ? hitObject.layer.ToString() : name;
        }

        private static string TagName(GameObject hitObject)
        {
            try
            {
                return Clean(hitObject.tag, 40);
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static string SceneName(GameObject hitObject)
        {
            try
            {
                return Clean(hitObject.scene.name, 80);
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static string Path(Transform transform)
        {
            if (transform == null)
            {
                return "";
            }

            List<string> parts = new List<string>();
            int depth = 0;
            Transform current = transform;
            while (current != null && depth < 8)
            {
                parts.Add(current.name);
                current = current.parent;
                depth++;
            }

            parts.Reverse();
            string path = string.Join("/", parts);
            if (current != null)
            {
                path = "…/" + path;
            }

            return path;
        }

        private static string Join(List<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return "";
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(values[i]);
            }

            return builder.ToString();
        }

        private static string Vec(Vector2 value)
        {
            return "(" + value.x.ToString("0.#") + ", " + value.y.ToString("0.#") + ")";
        }

        private static string Vec(Vector3 value)
        {
            return "(" + value.x.ToString("0.#") + ", " + value.y.ToString("0.#") + ", " + value.z.ToString("0.#") + ")";
        }

        private static string Clean(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            value = value.Replace("\r", " ").Replace("\n", " ").Replace("<", "＜").Replace(">", "＞").Trim();
            if (maxLength > 0 && value.Length > maxLength)
            {
                value = value.Substring(0, maxLength) + "…";
            }

            return value;
        }
    }
}

