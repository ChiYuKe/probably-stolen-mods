using System;
using UnityEngine;

namespace DayHud
{
    /// <summary>
    /// 左上角圆形天数面板。挂在一个常驻的 Canvas 上，自己管创建和销毁。
    /// </summary>
    public class DayHudMod : IMod
    {
        private ModManifest manifest;
        private ModLog log;
        private GameObject hudObject;
        private DayHudPanel panel;

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
                EnsureHud();
            }
            catch (Exception e)
            {
                log.Error("建天数面板的时候崩了：" + e);
            }

            // F9 里能开关，截图或者嫌挡视野的时候用
            try
            {
                ModHelper.AddDebugFunction(manifest.ID, "Modding", "天数圆盘 开/关", ToggleHud);
            }
            catch (Exception e)
            {
                log.Warning("注册 F9 开关失败：" + e.Message);
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
                // 关游戏时调试面板可能已经销毁了，忽略
            }

            if (hudObject != null)
            {
                UnityEngine.Object.Destroy(hudObject);
            }

            hudObject = null;
            panel = null;
        }

        private void EnsureHud()
        {
            if (hudObject != null)
            {
                if (panel != null)
                {
                    return;
                }

                UnityEngine.Object.Destroy(hudObject);
                hudObject = null;
            }

            // 常驻对象：换场景不丢，进主菜单时面板自己隐藏
            hudObject = new GameObject("DayHudCanvas", typeof(RectTransform), typeof(Canvas));
            UnityEngine.Object.DontDestroyOnLoad(hudObject);

            panel = hudObject.AddComponent<DayHudPanel>();
            panel.Build(manifest.ID, log);
        }

        private void ToggleHud()
        {
            try
            {
                EnsureHud();
                if (panel != null)
                {
                    panel.ToggleUserHidden();
                }
            }
            catch (Exception e)
            {
                log.Error("开关天数面板的时候崩了：" + e);
            }
        }
    }
}
