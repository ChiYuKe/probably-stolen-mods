using System;
using EPOOutline;
using UnityEngine;

namespace Chemistry
{
    /// <summary>
    /// 店内桌腿通风柜入口：柜台右侧那条带排气扇的桌腿原本焊死在 desk.png 里，
    /// 没有独立 GameObject（原版场景是编辑器摆的全幅贴图层）。这里按 desk 精灵的
    /// bounds 把裁出来的 lab_entrance 精灵逐像素盖回原位，配 BoxCollider2D +
    /// Interactable（照抄原版 DrJacksonLocation.EnsureSheetTablet 的运行时拼装套路），
    /// 点击前往地下实验室。悬停描边走原版 EPOOutline（与店内其他可交互物一致：
    /// 可用金色、不可用红色），不用 doNotOutline 也不自己调 tint。
    /// </summary>
    internal sealed class LabEntranceVent : MonoBehaviour
    {
        /// <summary>桌腿柜体在 desk.png 960x540 画布上的像素矩形（x、y 从左上角起，670,336 起 113x158）。</summary>
        private static readonly RectInt LegRect = new RectInt(670, 336, 113, 158);

        // Match the visible front face in lab_entrance.png. The source crop also
        // contains neighboring desk pixels, so keep the clickable/outlined shape
        // inside the hatch and pedestal rather than using the full crop rectangle.
        private static readonly Vector2[] HotspotShapePixels =
        {
            new Vector2(18f, 8f),
            new Vector2(100f, 8f),
            new Vector2(112f, 20f),
            new Vector2(112f, 138f),
            new Vector2(103f, 151f),
            new Vector2(24f, 151f),
            new Vector2(18f, 143f),
            new Vector2(18f, 22f),
        };

        private const string DeskSpriteName = "desk";
        private const string SpriteKey = "lab_entrance";
        private const string HotspotName = "ChemistryLabEntranceVent";

        private Sprite sprite;
        private SpriteRenderer deskRenderer;
        private GameObject hotspot;
        private SpriteRenderer hotspotRenderer;
        private Interactable hotspotInteractable;
        private LabEntranceVentClickable clickable;

        private void LateUpdate()
        {
            bool show = IsAtStore();
            if (!show)
            {
                if (hotspot != null && hotspot.activeSelf)
                {
                    hotspot.SetActive(false);
                }

                return;
            }

            if (sprite == null)
            {
                CustomUIManager manager = CustomUIManager.Instance;
                sprite = manager != null ? manager.GetModSprite(ChemistryCatalog.ModId, SpriteKey, true) : null;
                if (sprite == null)
                {
                    // 贴图还没进 PaperSprites（美术资源晚到/热重载），下次再试
                    return;
                }

                // 精灵要逐像素盖在 Point 过滤的原版桌面上，Paper 默认的双线性会发糊
                sprite.texture.filterMode = FilterMode.Point;
            }

            if (!EnsureDeskRenderer())
            {
                return;
            }

            if (hotspot == null)
            {
                Create();
            }

            if (!hotspot.activeSelf)
            {
                hotspot.SetActive(true);
            }

            Position();

            bool canEnter = CanEnter();
            if (hotspotInteractable != null)
            {
                hotspotInteractable.isAvailable = canEnter;
            }

            if (clickable != null)
            {
                clickable.SetAvailable(canEnter);
            }
        }

        /// <summary>当前是否停在店里（原 LabEntranceButton 的判断，按钮下掉后搬到这里）。</summary>
        private static bool IsAtStore()
        {
            MapUIManager map = MapUIManager.Instance;
            StoreUIManager store = StoreUIManager.Instance;
            return LabLocation.Registered != null && PlayerStore.HasInstance && map != null &&
                store != null && store.isActiveAndEnabled && !map.IsOutside && !map.IsInTransit &&
                !TransitUI.IsOpen && (map.confirmationPanel == null || !map.confirmationPanel.activeInHierarchy) &&
                (store.blackscreen == null || !store.blackscreen.gameObject.activeInHierarchy);
        }

        /// <summary>现在能不能进实验室（重伤 / 广场恐惧休息 / crackdown 期间不行）。</summary>
        private static bool CanEnter()
        {
            PlayerStore player = PlayerStore.Instance;
            return player != null && (player.devMode ||
                (!HealthData.IsSeriouslyWounded() && !StartingPerkContent.IsAgoraphobiaResting() &&
                StoreStation.Instance.storeEventManager?.IsEventActive("crackdown") == false));
        }

        /// <summary>找场景里画 desk 的那个渲染器（全幅 960x540，定位基准）。切场景后引用失效会重找。</summary>
        private bool EnsureDeskRenderer()
        {
            if (deskRenderer != null && deskRenderer.sprite != null && deskRenderer.sprite.name == DeskSpriteName)
            {
                return true;
            }

            deskRenderer = null;
            SpriteRenderer[] renderers = FindObjectsOfType<SpriteRenderer>();
            foreach (SpriteRenderer candidate in renderers)
            {
                Sprite candidateSprite = candidate != null ? candidate.sprite : null;
                if (candidateSprite != null && candidateSprite.name == DeskSpriteName)
                {
                    deskRenderer = candidate;
                    break;
                }
            }

            return deskRenderer != null;
        }

        private void Create()
        {
            hotspot = new GameObject(HotspotName);
            hotspot.transform.SetParent(transform, false);

            hotspotRenderer = hotspot.AddComponent<SpriteRenderer>();
            hotspotRenderer.sprite = sprite;

            PolygonCollider2D polygon = hotspot.AddComponent<PolygonCollider2D>();
            var colliderPoints = new Vector2[HotspotShapePixels.Length];
            for (int i = 0; i < HotspotShapePixels.Length; i++)
            {
                Vector2 pixel = HotspotShapePixels[i];
                colliderPoints[i] = new Vector2(
                    sprite.bounds.min.x + pixel.x / sprite.rect.width * sprite.bounds.size.x,
                    sprite.bounds.max.y - pixel.y / sprite.rect.height * sprite.bounds.size.y);
            }
            polygon.SetPath(0, colliderPoints);

            // RequireComponent 会在运行时自动补 Outlinable（游戏程序集里有），
            // Awake 里把 BlurShift 清零、DilateShift 设 1（细像素描边，原版就是这套）
            hotspotInteractable = hotspot.AddComponent<Interactable>();
            hotspotInteractable.isControlable = false;

            // 描边和店内其他可交互物（如睡袋）保持一致：Interactable 悬停时会开关
            // Outlinable 并给颜色（可用金/不可用红）。运行时 AddComponent 的
            // Outlinable 是全默认值，枚举 0 不是合法绘制模式，得显式指明，并把
            // 自己身上的 SpriteRenderer 注册成描边目标，否则描边画不出来。
            Outlinable outlinable = hotspot.GetComponent<Outlinable>();
            if (outlinable != null)
            {
                outlinable.RenderStyle = RenderStyle.Single;
                outlinable.DrawingMode = OutlinableDrawingMode.Normal;
                outlinable.AddAllChildRenderersToRenderingList(RenderersAddingMode.SpriteRenderer);
                MatchVanillaOutlineLayer(outlinable);
            }

            clickable = hotspot.AddComponent<LabEntranceVentClickable>();
            clickable.Init(() =>
            {
                if (IsAtStore() && CanEnter())
                {
                    LabLocation.TravelHere();
                }
            });

            // 顺序照 DrJackson：先 Interactable/TooltipInterface 再 TooltipContainer，
            // 它的 Awake 会 GetComponent 这两个
            hotspot.AddComponent<TooltipContainer>();
        }

        /// <summary>
        /// 运行时新建的 Outlinable 默认 outlineLayer=0，但场景 Outliner 的
        /// outlineLayerMask 是编辑器序列化的，未必放行 0 号层（现象：悬停无描边，
        /// 而原版道具正常）。照抄原版可交互物的描边层（优先睡袋，截图证实它
        /// 描边正常），保证掩码放行。
        /// </summary>
        private void MatchVanillaOutlineLayer(Outlinable ours)
        {
            Outlinable reference = null;
            Outlinable[] candidates = FindObjectsOfType<Outlinable>();
            foreach (Outlinable candidate in candidates)
            {
                if (candidate == null || candidate == ours)
                {
                    continue;
                }

                if (candidate.GetComponent<StoreBedroll>() != null)
                {
                    reference = candidate;
                    break;
                }

                if (reference == null && candidate.GetComponent<Interactable>() != null)
                {
                    reference = candidate;
                }
            }

            if (reference == null)
            {
                ChemistryMod.Log?.Warning("桌腿入口：没找到原版可交互物的描边层可参考，保持 0 号层。");
                return;
            }

            ours.OutlineLayer = reference.OutlineLayer;
            ChemistryMod.Log?.Log("桌腿入口描边：参考 " + reference.gameObject.name +
                "（outlineLayer=" + reference.OutlineLayer + "，targets=" + ours.OutlineTargets.Count + "）");
        }

        /// <summary>按 desk 精灵 bounds 把画布像素矩形换算成世界坐标，盖回原位。</summary>
        private void Position()
        {
            Sprite desk = deskRenderer.sprite;
            Bounds deskBounds = deskRenderer.bounds;
            if (desk == null || deskBounds.size.x <= 0f || deskBounds.size.y <= 0f)
            {
                return;
            }

            float worldPerPixelX = deskBounds.size.x / desk.rect.width;
            float worldPerPixelY = deskBounds.size.y / desk.rect.height;

            // 画布坐标 y 向下；基准用 bounds.center（渲染四边形的真实中心），
            // 不依赖原版精灵的 pivot 摆在哪。LegRect 是从贴图左上角数的，
            // 原版 desk 精灵的 rect 就是整张 960x540 画布（提取纹理实测）。
            float legCenterX = LegRect.x + LegRect.width * 0.5f;
            float legCenterYFromBottom = desk.rect.height - (LegRect.y + LegRect.height * 0.5f);
            Vector3 center = deskBounds.center +
                new Vector3(
                    (legCenterX - desk.rect.center.x) * worldPerPixelX,
                    (legCenterYFromBottom - desk.rect.center.y) * worldPerPixelY,
                    0f);

            // 我方精灵的 PPU 不一定和原版一致，按宽度反推缩放（同源像素，纵横比一致）
            float scale = LegRect.width * worldPerPixelX / sprite.bounds.size.x;
            hotspot.transform.localScale = new Vector3(scale, scale, 1f);
            hotspot.transform.position = center - sprite.bounds.center * scale;

            hotspotRenderer.sortingLayerID = deskRenderer.sortingLayerID;
            hotspotRenderer.sortingOrder = deskRenderer.sortingOrder + 1;
        }

        private void OnDestroy()
        {
            if (hotspot != null)
            {
                Destroy(hotspot);
            }

            hotspot = null;
            hotspotRenderer = null;
            hotspotInteractable = null;
            clickable = null;
        }
    }

    /// <summary>桌腿通风柜的点击行为 + 悬停提示：点击去地下实验室，提示「前往地下室」。
    /// 悬停描边由 Interactable 统一管，这里不再自己调 tint。</summary>
    internal sealed class LabEntranceVentClickable : TooltipInterface, IClickable
    {
        private Action onClick;
        private bool available = true;

        public void Init(Action onClickHandler)
        {
            onClick = onClickHandler;
        }

        public void SetAvailable(bool value)
        {
            available = value;
        }

        public void OnClick()
        {
            if (onClick != null)
            {
                onClick();
            }
        }

        public override string GetTooltip()
        {
            return Loc.Get("lab_enter_from_store", "前往地下室", "Go to basement");
        }

        public override bool IsTooltipEnabled()
        {
            return available;
        }
    }
}
