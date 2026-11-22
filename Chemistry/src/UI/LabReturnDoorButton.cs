using UnityEngine;

namespace Chemistry
{
    /// <summary>
    /// The steel door is painted into the lab backdrop, so detect clicks directly
    /// in its screen area instead of relying on UI canvas raycast ordering.
    /// </summary>
    internal sealed class LabReturnDoorButton : MonoBehaviour
    {
        // Corners of the steel door leaf in lab_background.png (1672x941 pixels).
        // Projecting these through the actual backdrop transform keeps the outline
        // aligned even when the game camera letterboxes or crops the scene.
        private static readonly Vector2[] DoorImageCorners =
        {
            new Vector2(65f, 326f),  // top-left
            new Vector2(218f, 363f), // top-right
            new Vector2(216f, 758f), // bottom-right
            new Vector2(65f, 812f),  // bottom-left
        };

        private LineRenderer hoverOutline;
        private Material outlineMaterial;
        private bool hovering;
        private readonly Vector2[] screenCorners = new Vector2[4];
        private readonly Vector3[] worldCorners = new Vector3[4];

        private void Awake()
        {
            CreateHoverOutline();
        }

        private void LateUpdate()
        {
            if (!IsAtLab())
            {
                SetHovering(false);
                return;
            }

            if (!UpdateDoorCorners())
            {
                SetHovering(false);
                return;
            }

            Vector2 mouse = Input.mousePosition;
            bool isHovering = IsInsideDoor(mouse, screenCorners);
            SetHovering(isHovering);
            if (isHovering)
            {
                UpdateHoverOutline();
                if (Input.GetMouseButtonDown(0))
                {
                    LabLocation.ReturnToStore();
                }
            }
        }

        private void CreateHoverOutline()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return;
            }

            outlineMaterial = new Material(shader);
            GameObject outlineObject = new GameObject("ChemistryLabReturnDoorHoverOutline");
            outlineObject.transform.SetParent(transform, false);
            hoverOutline = outlineObject.AddComponent<LineRenderer>();
            hoverOutline.useWorldSpace = true;
            hoverOutline.loop = true;
            hoverOutline.positionCount = 4;
            hoverOutline.startWidth = hoverOutline.endWidth = 0.025f;
            hoverOutline.startColor = hoverOutline.endColor = new Color32(232, 211, 151, 255);
            hoverOutline.material = outlineMaterial;
            hoverOutline.sortingOrder = 1000;
            hoverOutline.enabled = false;
        }

        private bool UpdateDoorCorners()
        {
            SpriteRenderer backdrop = LabLocation.BackdropRenderer;
            Sprite sprite = backdrop != null ? backdrop.sprite : null;
            Camera camera = Camera.main;
            if (sprite == null || camera == null || sprite.rect.width <= 0f || sprite.rect.height <= 0f)
            {
                return false;
            }

            Bounds spriteBounds = sprite.bounds;
            for (int i = 0; i < DoorImageCorners.Length; i++)
            {
                Vector2 pixel = DoorImageCorners[i];
                float localX = spriteBounds.min.x + pixel.x / sprite.rect.width * spriteBounds.size.x;
                float localY = spriteBounds.max.y - pixel.y / sprite.rect.height * spriteBounds.size.y;
                worldCorners[i] = backdrop.transform.TransformPoint(new Vector3(localX, localY, 0f));
                Vector3 screen = camera.WorldToScreenPoint(worldCorners[i]);
                screenCorners[i] = new Vector2(screen.x, screen.y);
            }

            return true;
        }

        private static bool IsInsideDoor(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                bool crosses = (a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                if (crosses)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private void UpdateHoverOutline()
        {
            if (hoverOutline == null)
            {
                return;
            }

            for (int i = 0; i < worldCorners.Length; i++)
            {
                hoverOutline.SetPosition(i, worldCorners[i]);
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            float worldPerPixel = camera.orthographic
                ? camera.orthographicSize * 2f / Mathf.Max(1, Screen.height)
                : 0.005f;
            hoverOutline.startWidth = hoverOutline.endWidth = worldPerPixel * 2.5f;
        }

        private void SetHovering(bool value)
        {
            if (hovering == value)
            {
                return;
            }

            hovering = value;
            if (hoverOutline != null)
            {
                hoverOutline.enabled = value;
            }

            TooltipManager manager = TooltipManager._instance;
            if (manager == null)
            {
                return;
            }

            if (value)
            {
                manager.SetAndShowTooltip(
                    Loc.Get("lab_return_to_store", "铁门：点击返回店铺", "Door: click to return to the shop"), this);
            }
            else
            {
                manager.HideTooltip(this);
            }
        }

        private void OnDestroy()
        {
            SetHovering(false);
            if (outlineMaterial != null)
            {
                Destroy(outlineMaterial);
            }
        }

        private static bool IsAtLab()
        {
            MapUIManager map = MapUIManager.Instance;
            return LabLocation.Registered != null && map != null &&
                map.Current == LabLocation.Registered && !map.IsInTransit &&
                !TransitUI.IsOpen;
        }
    }
}
