using UnityEngine;

namespace DebugPanelChinese
{
    public class DebugPanelChineseMod : IMod
    {
        private ModManifest manifest;
        private ModLog log;
        private GameObject host;

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
                if (host == null)
                {
                    host = new GameObject("DebugPanelChinese");
                    Object.DontDestroyOnLoad(host);
                    host.AddComponent<DebugPanelLocalizer>();
                }

                log.Log("F9 调试面板汉化已启用");
            }
            catch (System.Exception e)
            {
                log.Error("启用调试面板汉化的时候崩了：" + e);
            }
        }

        public void OnDisable()
        {
            if (host != null)
            {
                Object.Destroy(host);
                host = null;
            }
        }
    }
}
