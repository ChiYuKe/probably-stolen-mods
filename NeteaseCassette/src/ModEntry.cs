using System;
using UnityEngine;

namespace NeteaseCassette
{
    public class ModEntry : IMod
    {
        public const string ModId = "netease_cassette";
        internal const string SessionFile = "session.json";

        // 云磁带的物品 ID。注册进物品目录，identifier 才是目录填的；
        // 自己 new 出来的没有，游戏里到处 identifier.Equals，会空引用。
        public const string TapeItemId = "netease_tape";

        // 卡带图数量，Textures/Items 下的 tape_01 ~ tape_64
        internal const int TapeSpriteCount = 64;

        // 占几格，跟原版 cassette1 一致
        internal static int TapeShapeWidth { get; private set; } = 1;

        internal static int TapeShapeHeight { get; private set; } = 1;

        private static bool tapeSpritesReady;


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
            LyricPanel.Close();
            Lyrics.Shutdown();
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

        // 云磁带模板。每次 SpawnItem 都跑一遍，歌名和曲目由 CloudWindow 补
        private static GameItem CreateTapeTemplate()
        {
            var item = ItemDirectory.CreateEmptyItem(TapeItemId);
            item.name = "音乐磁带 - 网易云";
            item.shortDescription = "从网易云录下来的，塞进磁带机就能放。";
            // 随机贴图，不然几十盘磁带长得一模一样
            EnsureTapeSprites();
            string spriteKey = TapeKey(UnityEngine.Random.Range(1, TapeSpriteCount + 1));
            item.SetSpriteAndShapeFromMod(ModId, spriteKey);
            if (item.shape == null || item.shape.width != TapeShapeWidth || item.shape.height != TapeShapeHeight)
            {
                Log?.Warning("云磁带贴图 " + spriteKey + " 尺寸不对，退回原版贴图");
                item.SetSpriteAndShape("Items/items_amenities", "cassette1");
            }
            item.EnableManufacturer("alison");
            item.SetGameItemType("ART");
            // 云磁带不值钱，四个价值字段清掉，提示框也就不显示价格
            item.unitValue = 0L;
            item.unitBaseValue = 0L;
            item.lateUnitValue = 0L;
            item.backupUnitValue = 0L;
            AudioHelper.InitCassette(item);
            return item;
        }

        private static string TapeKey(int index)
        {
            return "tape_" + index.ToString("00");
        }

        // 把 64 张卡带图按原版 cassette1 的尺寸重画：画布照原版贴图大小（决定占几格），
        // 图案照原版图案的实际大小（决定看起来多大）。少一样就会偏小或者偏大。
        private static void EnsureTapeSprites()
        {
            if (tapeSpritesReady) return;
            tapeSpritesReady = true;

            Sprite vanilla = null;
            try
            {
                vanilla = RenderHandler.LoadFromAtlas("Items/items_amenities", "cassette1");
            }
            catch (Exception e)
            {
                Log?.Warning("拿不到原版磁带贴图：" + e.Message);
            }
            if (vanilla == null) return;

            Rect rect = vanilla.rect;
            int canvasW = Mathf.Max(1, (int)rect.width);
            int canvasH = Mathf.Max(1, (int)rect.height);

            int vx, vy, vw, vh;
            if (!OpaqueBounds(vanilla, out vx, out vy, out vw, out vh))
            {
                vx = 0;
                vy = 0;
                vw = canvasW;
                vh = canvasH;
            }
            int destX = vx - (int)rect.xMin;
            int destY = vy - (int)rect.yMin;

            int gridSize = 16;
            try
            {
                if (UISettings.current != null) gridSize = Mathf.Max(1, UISettings.current.gridSize);
            }
            catch
            {
                // 读不到就按 16 算，反正画布大小是照原版贴图来的
            }
            TapeShapeWidth = Mathf.Max(1, canvasW / gridSize);
            TapeShapeHeight = Mathf.Max(1, canvasH / gridSize);

            var mod = ModLoader.Mods.Find(m => m.Manifest.ID == ModId);
            if (mod == null)
            {
                Log?.Warning("找不到自己的模组记录，云磁带先用原图");
                return;
            }

            int done = 0;
            for (int i = 1; i <= TapeSpriteCount; i++)
            {
                string key = TapeKey(i);
                Sprite source;
                if (!mod.Sprites.TryGetValue(key, out source) || source == null) continue;
                int sx, sy, sw, sh;
                if (!OpaqueBounds(source, out sx, out sy, out sw, out sh)) continue;
                Sprite scaled = RenderInto(source, sx, sy, sw, sh, destX, destY, vw, vh, canvasW, canvasH);
                if (scaled != null)
                {
                    mod.Sprites[key] = scaled;
                    done++;
                }
            }
            Log?.Log("云磁带贴图重画了 " + done + " 张：画布 " + canvasW + "x" + canvasH + "，图案 " + vw + "x" + vh +
                     "，占 " + TapeShapeWidth + "x" + TapeShapeHeight + " 格");
        }

        // 图案在贴图里的实际范围，返回的是贴图里的绝对坐标
        private static bool OpaqueBounds(Sprite sprite, out int x, out int y, out int width, out int height)
        {
            x = 0;
            y = 0;
            width = 0;
            height = 0;
            if (sprite == null || sprite.texture == null) return false;

            Rect rect = sprite.rect;
            int left = (int)rect.xMin;
            int bottom = (int)rect.yMin;
            int w = (int)rect.width;
            int h = (int)rect.height;

            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            try
            {
                for (int py = 0; py < h; py++)
                {
                    for (int px = 0; px < w; px++)
                    {
                        if (sprite.texture.GetPixel(left + px, bottom + py).a <= 0.02f) continue;
                        if (px < minX) minX = px;
                        if (py < minY) minY = py;
                        if (px > maxX) maxX = px;
                        if (py > maxY) maxY = py;
                    }
                }
            }
            catch (Exception e)
            {
                Log?.Warning("贴图像素读不出来（不可读？）：" + e.Message);
                return false;
            }

            if (maxX < 0) return false;
            x = left + minX;
            y = bottom + minY;
            width = maxX - minX + 1;
            height = maxY - minY + 1;
            return true;
        }

        // 存档里记的贴图键现在没这张图了（改过名字的存档）：按键名散列到一张现成的卡带图，
        // 同一个键每次都落到同一张，重启也不会换个样
        internal static bool TryPickTapeSpriteFor(string keyName, out Sprite sprite)
        {
            sprite = null;
            EnsureTapeSprites();
            var mod = ModLoader.Mods.Find(m => m.Manifest.ID == ModId);
            if (mod == null) return false;

            uint hash = 0;
            if (!string.IsNullOrEmpty(keyName))
            {
                foreach (char c in keyName) hash = hash * 31 + c;
            }
            for (int n = 0; n < TapeSpriteCount; n++)
            {
                int index = (int)((hash + (uint)n) % (uint)TapeSpriteCount) + 1;
                if (mod.Sprites.TryGetValue(TapeKey(index), out sprite) && sprite != null) return true;
            }
            return false;
        }

        // 把源图的一块区域缩放到画布上的目标区域，按面积平均，缩小的时候不容易糊
        private static Sprite RenderInto(Sprite source, int sx, int sy, int sw, int sh,
                                         int destX, int destY, int destW, int destH, int canvasW, int canvasH)
        {
            try
            {
                var texture = new Texture2D(canvasW, canvasH, TextureFormat.RGBA32, false);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.SetPixels32(new Color32[canvasW * canvasH]);

                Texture2D sourceTexture = source.texture;
                for (int dy = 0; dy < destH; dy++)
                {
                    int y0 = sy + dy * sh / destH;
                    int y1 = Mathf.Max(y0 + 1, sy + (dy + 1) * sh / destH);
                    for (int dx = 0; dx < destW; dx++)
                    {
                        int x0 = sx + dx * sw / destW;
                        int x1 = Mathf.Max(x0 + 1, sx + (dx + 1) * sw / destW);

                        float r = 0f, g = 0f, b = 0f, a = 0f;
                        int count = 0;
                        for (int py = y0; py < y1; py++)
                        {
                            for (int px = x0; px < x1; px++)
                            {
                                Color c = sourceTexture.GetPixel(px, py);
                                r += c.r * c.a;
                                g += c.g * c.a;
                                b += c.b * c.a;
                                a += c.a;
                                count++;
                            }
                        }
                        if (count == 0 || a <= 0.001f) continue;
                        texture.SetPixel(destX + dx, destY + dy, new Color(r / a, g / a, b / a, a / count));
                    }
                }

                texture.Apply(false, false);
                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, canvasW, canvasH), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = source.name;
                return sprite;
            }
            catch (Exception e)
            {
                Log?.Warning("重画磁带贴图失败：" + e.Message);
                return null;
            }
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
