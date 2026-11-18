# 网易云磁带机（NeteaseCassette）

把店里那台磁带播放器接上网易云音乐：游戏内扫码登录，搜歌 / 翻歌单，点一首就生成一盘磁带，
丢进原来的播放器里直接放。播放 / 暂停 / 上一首 / 下一首还是游戏原本那套按钮，音量也还是那个音量。

对应游戏 0.46D（商店标签 100-Rev13）。

## 玩之前要知道的

- 这是**非官方接口**：用的是网易云网页版那套 `music.163.com/api/*` 接口，没走官方开放平台。
  哪天网易云改接口或者风控收紧，这个 mod 就会失灵，不是游戏的问题。
- 登录是**扫码登录**，和网页版一样：手机网易云 → 右上角「扫一扫」→ 确认。cookie 存在
  `persistentDataPath/ModData/netease_cassette/session.json`，下次进游戏不用再扫。
- **不登录也能搜歌、放免费歌**；会员歌 / 版权受限的歌没登录只会拿到 128k 或者干脆没有地址，
  日志里能看到原因，播放器会自己停下来。
- 点歌会**真的生成一盘磁带**放进磁带仓，占格跟原版磁带一样，塞满了就提示。想清空直接把磁带扔了。
  图案从 `Textures/Items` 的 64 张卡带图里随机挑。
- 歌词：播放云曲目时会顺手拉一次歌词。播放器上 **「词」** 是开关（图标亮着＝开着，压暗＝关着），
  **「设」** 调样式。桌面歌词式浮层，只有当前句 + 下一句，透明背景、不挡点击；唱过的部分保持亮色，
  没唱到的压暗。字号、当前句颜色可调，位置能拖着走再锁定。设置和开关状态存在
  `ModData/netease_cassette/lyrics.json`，下次进游戏按上次的样子来。版权方没给词就显示「纯音乐，没有歌词」。
- 播放模式：「控制」面板下面有 **顺序 / 单曲循环 / 随机**。顺序 = 一首首放到底就停；
  单曲循环 = 一首放完重来这一首（手动点 ⏭ 仍然切下一首）；随机 = 随机挑一盘别的。
  存在 `ModData/netease_cassette/player.json`，默认单曲循环。
- **磁盘收纳盒**：6×2 格的新物品，内部 12×10 格，只收磁带，正好 30 盘。价值 40。
  每个存档第一次载入时送一个，放在磁带机的「储藏」格里；之后能在杰克逊博士的货里买到，
  也可能混在家居用品的随机货里（调试面板 Modding 分类下也能手动拿）。
- 收纳盒可以直接拖进「磁带」那一格当仓用：播放列表会把盒子里的磁带一并算进去，
  播放 / 切歌 / 循环都按合并后的列表走。打开盒子时正在播的那盘会亮绿框（暂停是半透明绿），
  悬停盒子还能看到「正在播放第 N 盘」。
- 云磁带的价值是 **0**：不参与店铺估值、提示框不显示价格行，别指望拿它当货卖。
- 《用户协议》之类的事自己斟酌，账号风险自负。

## 怎么用

1. 进店，点柜台上的磁带播放器，控制区多了一个 **「云」** 按钮。
2. 点它打开「网易云音乐 · 磁带机」。
3. 「扫码登录」页扫码；不登录也行，直接去「搜索」。
4. 搜到歌点一下：生成磁带 → 放进磁带仓 → 缓冲 → 出声。
5. 歌单页在登录后能看到你的歌单（含「我喜欢的音乐」），列表每页 100 首，用底部的「上一页 / 下一页」翻。

播放中想换歌，用播放器原来的 ▶ / ⏸ / ⏭。自动接下一首的行为由播放模式决定。

## 技术上的几个决定

- **曲目 ID 就是 `netease:<songId>`**，塞进原版磁带的 `CASSETTE_CONTENT` 标签里。
  存档、磁带图标、播放器状态机全是原版逻辑，我们只在 `AudioManager.PlayMusic` 上拦一下：
  看到 `netease:` 前缀就换成「现拉现放」，其他标识原样放行。
- `AudioManager.CanPlay` 也补了一刀，不然读档时 `AudioDictionary` 里查不到这首歌，
  原版会把播放状态重置成停止。
- 音频走 `DownloadHandlerAudioClip(url, AudioType.MPEG)`，下完丢进游戏自己的 `audioSourceMusic`。
  音量滑块、暂停、切场景静音都跟原版一致，没有另开 AudioSource。同一首歌重复播放会复用上一次解码好的
  `AudioClip`，循环和重播不用重新下载。
- 播放模式、自动切歌接在原版的 `AudioManager.OnTrackDone` 上；手动切歌接在 `OnNextButtonPressed` 上，
  两者分开，单曲循环时手动点 ⏭ 仍然会正常换歌。
- 二维码是自己画的（`src/QrCode.cs`，纯 C#，不依赖外部库），矩阵验证见 `tools/QrProbe`。
- HTTP 只有一处：`src/UnityTransport.cs`，用 `UnityWebRequest` + 表单 POST，因此清单声明了 `network` 能力。

## 目录

```
NeteaseCassette/
├── manifest.xml              清单（ID: netease_cassette，能力: network）
├── src/
│   ├── ModEntry.cs           入口、登录态、两个物品模板、调试按钮
│   ├── Patches.cs            Harmony：面板按钮、PlayMusic/CanPlay/Stop/Pause、播放模式、
│   │                         磁带仓容器扫描、播放框、格子过滤器
│   ├── CloudWindow.cs        CustomUI 窗口：扫码、搜索、歌单、点歌
│   ├── StreamPlayer.cs       要地址 → 下载/复用 → 丢进 audioSourceMusic
│   ├── NeteaseApi.cs         网易云接口客户端（不依赖 Unity，能单独跑）
│   ├── UnityTransport.cs     INcTransport 的 UnityWebRequest 实现
│   ├── Lyrics.cs             歌词浮层：拉取、排版、卡拉 OK 高亮、拖动
│   ├── LyricSettings.cs      lyrics.json 的结构
│   ├── LyricPanel.cs         歌词样式窗口
│   ├── PlaybackMode.cs       顺序 / 单曲循环 / 随机
│   ├── CaseFrame.cs          收纳盒里那盘的播放框
│   ├── CassetteScan.cs       往容器里递归找磁带
│   ├── LootDump.cs           开发用：导出掉落表（已停用，接线在 ModEntry 里注释着）
│   ├── Loc.cs                文案查表（Loc.T，读 Localization/*.csv）
│   ├── QrCode.cs             QR 编码器（纯 C#）
│   └── QrSprite.cs           QR 矩阵 → Texture2D / Sprite
├── Localization/             en / zh / zh-Hans / zh-Hant 四份文案 CSV
├── Textures/Items/           64 张卡带图 + 3 个功能图标 + 3 个模式图标 + 收纳盒贴图
└── tools/                    只用于开发验证，不参与编译、不会装进 Mods
    ├── QrProbe/              QR 矩阵打印 + 与 segno 对拍
    └── ApiProbe/             真实接口冒烟测试
```

## 本地化

界面文案和物品名都不再硬编码，全部走 `Localization/` 下的 CSV（键,文本，UTF-8，无表头）：

```
Localization/
├── en.csv        英文
├── zh.csv        中文（部分环境读这个码）
├── zh-Hans.csv   简体中文
└── zh-Hant.csv   繁体中文
```

- 游戏按当前语言找对应文件，找不到回退到 `en`，再找不到就显示键名（日志里会有一次警告）；
- 加一门语言只需放一个 `<语言代码>.csv`，代码不用动；支持的语言代码见游戏文档（`en`、`fr`、`de`、`ru`、`zh`、`ja`、`es`、`ptBR`、`ko`、`it`、`pl`）；
- 文本里用 `{0}`、`{1}` 占位符，代码里是 `Loc.T("key", 参数…)`；
- 物品名在物品生成时解析，游戏中途切语言不会更新已经在背包里的物品。

## 编译


根目录的 `Directory.Build.props` 里配游戏路径，然后：

```
dotnet build NeteaseCassette/NeteaseCassette.csproj -c Release
```

编完自动拷到 `Mods\NeteaseCassette\`（dll + manifest + Textures）。进游戏 → 模组菜单启用 → 重启。

## 发布到创意工坊

1. 确认 `Mods\NeteaseCassette\` 里是 `manifest.xml`、`NeteaseCassette.dll`、`Textures\Items\*.png`；
2. 准备一张 `preview.png` 放进那个文件夹（创意工坊封面）；
3. 写一个 `upload.vdf`：

```
"workshopitem"
{
    "appid"            "4348910"
    "publishedfileid"  "0"
    "contentfolder"    "E:\\SteamLibrary\\steamapps\\common\\Probably Stolen\\Mods\\NeteaseCassette"
    "previewfile"      "E:\\SteamLibrary\\steamapps\\common\\Probably Stolen\\Mods\\NeteaseCassette\\preview.png"
    "visibility"       "2"
    "title"            "网易云磁带机"
    "description"      "..."
    "changenote"       "首次发布"
}
```

4. `steamcmd +login <账号> +workshop_build_item upload.vdf +quit`。第一次会创建条目并把 ID 写回 vdf，
   之后再跑同一条命令就是更新。首次上传要先在 Steam 网站上接受创意工坊协议。
5. 更新版本时同步改 `manifest.xml` 里的 `ModVersion` 和 `GameVersions`。

## 验证工具

- `dotnet run --project NeteaseCassette/tools/QrProbe -c Release -- ascii "文本"` 打印 QR 矩阵；
  `verify.py` 跟 Python 的 segno 逐模块对拍。
- `dotnet run --project NeteaseCassette/tools/ApiProbe -c Release` 真实打接口：
  二维码 key、轮询 801、搜索、取播放地址、公开歌单。

## 已知限制

- 只做 MP3（`encodeType=mp3`），没做无损 / FLAC。
- 没做「每日推荐」「云盘」「评论」。
- 歌单单次最多取 1000 首（接口上限），超出部分看不到。
- 界面文本支持简中 / 繁中 / 英文，其余语言回退到英文；物品名和界面文案都走 `Localization/*.csv`。
- 一首歌 3~5 MB，首次播放要下载 1~3 秒，缓冲期间是静音的（窗口里显示「正在播放」）。
- 网易云接口是网页版那套，随时可能失效；接口层返回的错误原文（少数状态行里的 `{0}`）仍是中文。
