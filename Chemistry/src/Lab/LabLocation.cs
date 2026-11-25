using System;
using UnityEngine;

namespace Chemistry
{
    /// <summary>
    /// 地下实验室：离店可前往的 afterhour 地点（chemistry_lab）。
    /// 负责注册地点、背景舞台、到达/离开、F9 直达；面板交给 LabPanel。
    /// 契约见 data/LAB_CONTRACT.md「API 契约 / LabLocation」。
    /// </summary>
    internal static class LabLocation
    {
        public const string Id = "chemistry_lab";

        private const string BackdropName = "ChemistryLabStage";
        private const int BackdropSortingOrder = -1000;

        private static string modId = ChemistryCatalog.ModId;
        private static ModLog log;
        private static DelegateAfterhourLocation location;
        private static GameObject stage;
        private static SpriteRenderer stageRenderer;

        public static AfterhourLocation Registered
        {
            get { return location; }
        }

        internal static SpriteRenderer BackdropRenderer
        {
            get { return stageRenderer; }
        }

        public static void Init(string modId, ModLog logger)
        {
            if (!string.IsNullOrEmpty(modId))
            {
                LabLocation.modId = modId;
            }

            if (logger != null)
            {
                log = logger;
            }
            else if (log == null)
            {
                log = ChemistryMod.Log;
            }

            if (location == null)
            {
                try
                {
                    location = new DelegateAfterhourLocation
                    {
                        id = Id,
                        source = LabLocation.modId,
                        title = () => Loc.Get("lab_title", "地下实验室", "Underground Lab"),
                        panelTitle = () => Loc.Get("lab_panel_title", "地下实验室", "Underground Lab"),
                        imageKey = "chemistry:lab_card",
                        backgroundKey = "chemistry:lab_background",
                        backgroundSortingOrder = BackdropSortingOrder,
                        ambienceId = "",
                        isDiscovered = () => true,
                        isAvailable = () => true,
                        usesAfterhourBag = true,
                        showsStoreCounters = false,
                        allowsHazardousWaste = false,
                        countsDumpedWaste = false,
                        maxConcurrentEvents = 2,
                        buildPanel = LabPanel.BuildLauncher,
                        onArrive = OnArrive,
                        onLeave = OnLeave,
                    };

                    ModHelper.AddAfterhourLocation(location);
                    log?.Log("地下实验室地点已注册（" + Id + "）");
                }
                catch (Exception e)
                {
                    location = null;
                    log?.Error("注册地下实验室失败：" + e);
                }
            }

        }

        public static void Shutdown()
        {
            try
            {
                ModHelper.RemoveAfterhourLocations(modId);
            }
            catch
            {
                // 忽略
            }

            DestroyBackdrop();
            LabInventoryGrid.Close(true);
            LabPanel.Shutdown();

            location = null;
        }

        /// <summary>
        /// 懒创建背景舞台（DontDestroyOnLoad 的 GameObject + SpriteRenderer）。
        /// 贴图没加载好就先不建，下次（读档/到达实验室）再试。
        /// </summary>
        public static void EnsureBackdrop()
        {
            if (stage != null || location == null)
            {
                return;
            }

            try
            {
                CustomUIManager manager = CustomUIManager.Instance;
                if (manager == null)
                {
                    return;
                }

                Sprite sprite = manager.GetModSprite(modId, "lab_background", true);
                if (sprite == null)
                {
                    // 贴图还没进 PaperSprites（美术资源晚到/热重载），下次再试
                    return;
                }

                stage = new GameObject(BackdropName);
                UnityEngine.Object.DontDestroyOnLoad(stage);

                stageRenderer = stage.AddComponent<SpriteRenderer>();
                stageRenderer.sprite = sprite;
                stageRenderer.sortingOrder = BackdropSortingOrder;

                // Paper textures default to Bilinear in the game loader. The lab backdrop
                // is displayed close to native resolution, so nearest sampling keeps its
                // painted edges from being softened a second time by the renderer.
                if (stageRenderer.sprite.texture != null)
                {
                    stageRenderer.sprite.texture.filterMode = FilterMode.Point;
                    stageRenderer.sprite.texture.wrapMode = TextureWrapMode.Clamp;
                }

                FitStage(stageRenderer);
                stage.SetActive(false);
                location.stageObject = stage;
            }
            catch (Exception e)
            {
                log?.Warning("创建实验室背景失败：" + e.Message);
                DestroyBackdrop();
            }
        }

        /// <summary>按主相机视野和贴图尺寸把背景缩放铺满并居中（相机换了/分辨率变了可重复调用）。</summary>
        private static void FitStage(SpriteRenderer renderer)
        {
            if (renderer == null || stage == null)
            {
                return;
            }

            Sprite sprite = renderer.sprite;
            if (sprite == null)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            Vector2 spriteSize = sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                return;
            }

            float viewHeight = camera.orthographicSize * 2f;
            float viewWidth = viewHeight * camera.aspect;
            if (viewHeight <= 0f || viewWidth <= 0f)
            {
                return;
            }

            // 用 max 保证铺满视口（多出来的部分裁掉，不留黑边）
            float scale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y);
            stage.transform.localScale = new Vector3(scale, scale, 1f);

            Vector3 cameraPosition = camera.transform.position;
            stage.transform.position = new Vector3(cameraPosition.x, cameraPosition.y, 0f);
        }

        private static void OnArrive()
        {
            try
            {
                EnsureBackdrop();

                if (stage != null)
                {
                    FitStage(stageRenderer);

                    // Travel 是先 ShowStage 再 Arrive：如果背景是进本地点时才建好，
                    // 这一趟要自己把它打开，否则只有兜底背景。
                    MapUIManager manager = MapUIManager.Instance;
                    if (manager != null && manager.Current == Registered)
                    {
                        stage.SetActive(true);
                    }
                }

                LabStorage.EnsureLoaded();
                LabInventoryGrid.Open();
                StoryService.OnLabArrived();

                try
                {
                    OrderService.OnLabVisited();
                }
                catch (Exception e)
                {
                    log?.Warning("刷新委托期限失败：" + e.Message);
                }

                int visits = 0;
                try
                {
                    visits = ModHelper.GetAfterhourVisits(Id);
                }
                catch
                {
                    visits = 0;
                }

                if (visits <= 0)
                {
                    DayHudBridge.Push(
                        Loc.Get("lab_first_visit_hud", "地板下面居然藏着一间旧实验室。",
                            "There is an old laboratory hidden under the shop floor."),
                        "info", 7f);
                }

                log?.Log("进入地下实验室（累计到访 " + visits + " 次）");
            }
            catch (Exception e)
            {
                log?.Error("进入地下实验室出错：" + e);
            }
        }

        private static void OnLeave()
        {
            try
            {
                LabInventoryGrid.Close(true);
                LabPanel.Shutdown();
                SampleReportWindow.CloseAll();
                LabStorage.Save();
            }
            catch (Exception e)
            {
                log?.Warning("离开地下实验室保存失败：" + e.Message);
            }
        }

        /// <summary>F9 调试：直接前往地下实验室（地图界面没加载时静静不动）。</summary>
        public static void TravelHere()
        {
            try
            {
                MapUIManager manager = MapUIManager.Instance;
                if (manager == null)
                {
                    log?.Warning("地图界面还没加载，暂时去不了地下实验室。");
                    return;
                }

                AfterhourLocation target = Registered ?? AfterhourLocation.Get(Id);
                if (target == null)
                {
                    log?.Warning("地下实验室还没注册，去不了。");
                    return;
                }

                // 从店里直接下去时，必须先把店铺 UI 收干净：原版「离开店铺」流程会调
                // EmporiumEntry.OnLeaveStore()，关闭展示柜/地面/前后柜台等 PixelWindow，
                // 并隐藏店内可交互物和 HUD。少了这一步，切到实验室场景后店里那些面板
                // 会继续画在画面上（用户 2026-11-18 截图报告的遗留显示 BUG）。
                try
                {
                    EmporiumEntry emporium = EmporiumEntry.Instance;
                    if (emporium != null && !manager.IsOutside && !manager.IsInTransit)
                    {
                        emporium.OnLeaveStore();
                    }
                }
                catch (Exception e)
                {
                    log?.Warning("离开店铺 UI 清理失败（继续前往实验室）：" + e.Message);
                }

                manager.Travel(target);
            }
            catch (Exception e)
            {
                log?.Warning("前往地下实验室失败：" + e.Message);
            }
        }

        /// <summary>F9 调试：从 afterhour 地点回当铺。</summary>
        public static void ReturnToStore()
        {
            try
            {
                if (MapUIManager.Instance == null)
                {
                    return;
                }

                ModHelper.ReturnFromAfterhour(false);
            }
            catch (Exception e)
            {
                log?.Warning("返回当铺失败：" + e.Message);
            }
        }

        private static void DestroyBackdrop()
        {
            stageRenderer = null;

            if (stage != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(stage);
                }
                catch
                {
                    // 关游戏时可能已经销毁了
                }
            }

            stage = null;

            if (location != null)
            {
                try
                {
                    location.stageObject = null;
                }
                catch
                {
                    // 忽略
                }
            }
        }
    }
}
