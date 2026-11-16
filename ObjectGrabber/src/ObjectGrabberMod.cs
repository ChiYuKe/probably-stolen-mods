using System;
using UnityEngine;
using UnityEngine.UI;

namespace ObjectGrabber
{
    public class ObjectGrabberMod : IMod
    {
        private ModManifest manifest;
        private ModLog log;
        private GameObject host;
        private ObjectGrabberPanel panel;

        public void Init(ModManifest manifest)
        {
            this.manifest = manifest;
            log = new ModLog(manifest);
            log.Log("装好了，游戏版本 " + Application.version);
        }

        public void OnEnable()
        {
            try
            {
                ModHelper.AddDebugFunction(manifest.ID, "Special", "对象获取器", Toggle);
                log.Log("对象获取器已注册，F9 → 特殊 → 对象获取器");
            }
            catch (Exception e)
            {
                log.Error("注册 F9 按钮失败：" + e);
            }
        }

        public void OnDisable()
        {
            try
            {
                ModHelper.RemoveDebugFunctions(manifest.ID);
            }
            catch
            {
                // 关游戏时调试面板可能已经销毁
            }

            if (host != null)
            {
                UnityEngine.Object.Destroy(host);
            }

            host = null;
            panel = null;
        }

        private void Toggle()
        {
            try
            {
                if (host == null)
                {
                    host = CreateHost();
                    panel = host.GetComponent<ObjectGrabberPanel>();
                    host.SetActive(true);
                    log.Log("对象获取器已打开");
                    return;
                }

                if (panel == null)
                {
                    UnityEngine.Object.Destroy(host);
                    host = CreateHost();
                    panel = host.GetComponent<ObjectGrabberPanel>();
                }

                bool show = !host.activeSelf;
                host.SetActive(show);
                log.Log(show ? "对象获取器已打开" : "对象获取器已关闭");
            }
            catch (Exception e)
            {
                log.Error("打开对象获取器失败：" + e);
            }
        }

        private GameObject CreateHost()
        {
            GameObject canvasObject = new GameObject(
                "ObjectGrabberCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            scaler.referencePixelsPerUnit = 100f;

            UnityEngine.Object.DontDestroyOnLoad(canvasObject);
            canvasObject.AddComponent<ObjectGrabberPanel>();
            return canvasObject;
        }
    }
}
