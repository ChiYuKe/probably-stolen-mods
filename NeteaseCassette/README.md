# 网易云磁带机（NeteaseCassette）

把店里那台磁带播放器接上网易云音乐：游戏内扫码登录，搜歌 / 翻歌单，点一首就生成一盘磁带，
丢进原来的播放器里直接放。播放 / 暂停 / 上一首 / 下一首还是游戏原本那套按钮，音量也还是那个音量。

对应游戏 0.46D（100-Rev13）。

## 玩之前要知道的

- 这是**非官方接口**：用的是网易云网页版那套 `music.163.com/api/*` 接口，没走官方开放平台。
  哪天网易云改接口或者风控收紧，这个 mod 就会失灵，不是游戏的问题。
- 登录是**扫码登录**，和网页版一样：手机网易云 → 右上角「扫一扫」→ 确认。cookie 存在
  `persistentDataPath/ModData/netease_cassette/session.json`，下次进游戏不用再扫。
- **不登录也能搜歌、放免费歌**；会员歌 / 版权受限的歌没登录只会拿到 128k 或者干脆没有地址，
  日志里能看到原因，播放器会自己停下来。
- 点歌会**真的生成一盘磁带**放进磁带仓，占格跟原版磁带一样，塞满了就提示。想清空直接把磁带扔了。图案从 `Textures/Items` 的 64 张卡带图里随机挑。
- 歌词：播放云曲目时会顺手拉一次歌词，点播放器上的 **「词」** 开关，**「设」** 调样式。桌面歌词式浮层，只有当前句 + 下一句，透明背景、不挡点击；当前句唱过的部分保持亮色，没唱到的压暗，一眼能看出进度。字号、当前句颜色都能调，位置可以直接拖着走再点锁定。设置和开关状态都存在 `ModData/netease_cassette/lyrics.json`，下次进游戏自动按上次的样子显示。版权方没给词（纯音乐、现场版）就显示「纯音乐，没有歌词」。
- 播放模式：「控制」面板下面有 **顺序 / 单曲循环 / 随机** 三个按钮。顺序 = 一首首放到底就停；单曲循环 = 一首放完重来这一首（手动点 ⏭ 仍然切下一首）；随机 = 随机挑一盘别的。模式存在 `ModData/netease_cassette/player.json`，默认单曲循环。
- **磁盘收纳盒**：6×2 格的新物品，内部 12×10 格，只收磁带，正好 30 盘。每个存档第一次载入时自动送一个，放在磁带机的「储藏」格里；调试面板 Modding 分类下也能手动拿一个。
- 收纳盒可以直接拖进「磁带」那一格里当仓用：播放列表会把盒子里的磁带一并算进去，播放/切歌/循环都按合并后的列表走；打开盒子时，正在播的那盘会亮绿框（暂停时是半透明绿），悬停盒子还能看到「正在播放第 N 盘」。
- 云磁带的价值是 **0**：不参与店铺估值、提示框里也不会显示价格行（游戏对 0 价值物品就是不显示），别指望拿它当货卖。
- 《用户协议》之类的事自己斟酌，账号风险自负。

## 怎么用

1. 进店，点柜台上的磁带播放器，控制区多了一个 **「云」** 按钮（旁边那个 **「词」** 是歌词开关）。
2. 点它打开「网易云音乐 · 磁带机」。
3. 「扫码登录」标签页里扫码；不登录也行，直接去「搜索」。
4. 搜到歌点一下：生成磁带 → 放进磁带仓 → 开始缓冲 → 出声。
5. 歌单标签页在登录后能看到你的歌单（包括「我喜欢的音乐」），点进去点歌同样是上面那套流程。

播放中想换歌，用播放器原来的 ▶ / ⏸ / ⏭ 就行。云曲目放完会自动下一首（原版逻辑）。

## 技术上的几个决定

- **曲目 ID 就是 `netease:<songId>`**，塞进原版磁带的 `CASSETTE_CONTENT` 标签里。
  这样存档、磁带图标、播放器状态机全都是原版逻辑，我们只拦了 `AudioManager.PlayMusic`：
  看到 `netease:` 前缀就换成"现拉现放"，其他标识原样放行。
- `AudioManager.CanPlay` 也补了一刀，不然读档时 `AudioDictionary` 里查不到这首歌，
  原版会把播放状态重置成停止。
- 音频走 `DownloadHandlerAudioClip(url, AudioType.MPEG)`，下完丢进游戏自己的 `audioSourceMusic`。
  所以音量滑块、暂停、切场景静音这些都跟原版一致，没有自己另开 AudioSource。
- 二维码是自己画的（`src/QrCode.cs`，纯 C#，不依赖外部库），矩阵验证见 `tools/QrProbe`。
- HTTP 只有一处：`src/UnityTransport.cs`，用 `UnityWebRequest` + 表单 POST。
  清单里因此声明了 `network` 能力。

## 目录

```
NeteaseCassette/
├── manifest.xml              清单（ID: netease_cassette，能力: network）
├── src/
│   ├── ModEntry.cs           入口、登录态、调试按钮
│   ├── Patches.cs            Harmony：磁带机加按钮、拦 PlayMusic / CanPlay / Stop / Pause
│   ├── CloudWindow.cs        CustomUI 窗口：扫码、搜索、歌单、点歌
│   ├── StreamPlayer.cs       要地址 → 下载 → 丢进 audioSourceMusic
│   ├── NeteaseApi.cs         网易云接口客户端（不依赖 Unity，能单独跑）
│   ├── UnityTransport.cs     INcTransport 的 UnityWebRequest 实现
│   ├── QrCode.cs             QR 编码器（纯 C#）
│   └── QrSprite.cs           QR 矩阵 → Texture2D / Sprite
└── tools/                    只用于开发验证，不参与 mod 编译、不会装进 Mods
    ├── QrProbe/              QR 矩阵打印 + 与 segno 对拍
    └── ApiProbe/             真实接口冒烟测试
```

## 编译

根目录的 `Directory.Build.props` 里配游戏路径，然后：

```
dotnet build NeteaseCassette/NeteaseCassette.csproj -c Release
```

编完自动拷到 `Mods\NeteaseCassette\`（dll + manifest）。进游戏 → 模组菜单启用 → 重启。

## 验证工具

- `dotnet run --project NeteaseCassette/tools/QrProbe -c Release -- ascii "文本"`
  直接打印 QR 矩阵；`_extract.py` 会跟 Python 的 segno 逐模块对拍。
- `dotnet run --project NeteaseCassette/tools/ApiProbe -c Release`
  真实打接口：二维码 key、轮询 801、搜索、取播放地址、公开歌单。

## 已知限制

- 只做 MP3（`encodeType=mp3`）。没做无损 / FLAC；真要无损得自己换解码。
- 没做「每日推荐」「云盘」「评论」，接口够用但 UI 没排。
- 一首歌 3~5 MB，下载 1~3 秒，缓冲期间播放器是静音的（窗口里会写"正在播放"）。
- 暂停后切歌、连续几首放不出来这些边角我们收尾成了"停下来"，不会卡住播放器。
