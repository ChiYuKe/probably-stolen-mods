using System;
using UnityEngine;

namespace NeteaseCassette
{
    public class ModEntry : IMod
    {
        public const string ModId = "netease_cassette";
        internal const string SessionFile = "session.json";

        /// <summary>
        /// 云磁带的物品 ID。必须走物品目录注册：物品的 identifier / identifierName / uniqueId
        /// 都是 DirectoryMaster 填的，直接 new 出来的物品 identifier 是 null，
        /// 游戏里一堆 identifier.Equals(...) 的提示框/交互代码会当场空引用（磁带就没提示框、点不动），
        /// 存档也还原不回来。
        /// </summary>
        public const string TapeItemId = "netease_tape";

        internal static ModLog Log { get; private set; }
        internal static NeteaseClient Client { get; private set; }

        private ModManifest manifest;

        public void Init(ModManifest manifest)
        {
            this.manifest = manifest;
            Log = new ModLog(manifest);
            Log.Log("装好了，游戏版本 " + Application.version);
        }

        public void OnEnable()
        {
            Client = new NeteaseClient(new UnityTransport(), message => Log.Log("[api] " + message));
            Client.ImportSession(ReadSession());
            StreamPlayer.Init(Client, Log);

            ModHook.OnGameLoadedLate += OnGameLoaded;
            ModHook.OnModItemDirectoryInit += RegisterItems;
            ModHelper.AddDebugFunction(ModId, "Modding", "网易云磁带机", CloudWindow.Open);

            Log.Log("网易云客户端就绪，登录状态：" + (Client.IsLoggedIn ? (Client.Nickname ?? "已登录") : "未登录"));
        }

        public void OnDisable()
        {
            ModHook.OnGameLoadedLate -= OnGameLoaded;
            ModHook.OnModItemDirectoryInit -= RegisterItems;
            ModHelper.RemoveDebugFunctions(ModId);

            SaveSession();
            StreamPlayer.Shutdown();
            CloudWindow.Close();
            Client = null;
            Log = null;
        }

        private void OnGameLoaded()
        {
            try
            {
                bool hasDeck = EmporiumEntry.Instance != null && EmporiumEntry.Instance.cassettePlayer != null;
                Log.Log("存档载入完成，磁带机：" + (hasDeck ? "在" : "不在") + "，云登录：" + (Client != null && Client.IsLoggedIn ? "是" : "否"));
            }
            catch (Exception e)
            {
                Log?.Warning("读档回调出错：" + e.Message);
            }
        }

        private static void RegisterItems(ModItemDirectory dir)
        {
            dir.Add(TapeItemId, CreateTapeTemplate);
            Log?.Log("云磁带物品已注册：" + TapeItemId);
        }

        /// <summary>云磁带模板。每次 SpawnItem 都会跑一遍，具体歌名 / 曲目 ID 由 CloudWindow 覆盖。</summary>
        private static GameItem CreateTapeTemplate()
        {
            var item = ItemDirectory.CreateEmptyItem(TapeItemId);
            item.name = "音乐磁带 - 网易云";
            item.shortDescription = "从网易云录下来的，塞进磁带机就能放。";
            item.SetSpriteAndShape("Items/items_amenities", "cassette1");
            item.EnableManufacturer("alison");
            item.SetGameItemType("ART");
            item.unitValue = 150L;
            AudioHelper.InitCassette(item);
            return item;
        }

        internal static void SaveSession()
        {
            try
            {
                if (Client == null) return;
                ModHelper.WriteModDataFileText(ModId, SessionFile, Client.ExportSession() ?? "{}");
            }
            catch (Exception e)
            {
                Log?.Warning("保存登录状态失败：" + e.Message);
            }
        }

        private static string ReadSession()
        {
            try
            {
                if (!ModHelper.ModDataFileExists(ModId, SessionFile)) return null;
                return ModHelper.ReadModDataFileText(ModId, SessionFile);
            }
            catch (Exception e)
            {
                Log?.Warning("读取登录状态失败：" + e.Message);
                return null;
            }
        }

        internal static void ClearSession()
        {
            try
            {
                if (ModHelper.ModDataFileExists(ModId, SessionFile))
                {
                    ModHelper.DeleteModDataFile(ModId, SessionFile);
                }
            }
            catch (Exception e)
            {
                Log?.Warning("删除登录状态失败：" + e.Message);
            }
        }
    }
}
