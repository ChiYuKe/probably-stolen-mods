# probably-stolen-mods

《深空当铺》（Probably Stolen）的 mod 源码，自己写着玩，能用就留着。

游戏是 Unity 2021.3 + Mono，自带 mod 加载器，不需要 BepInEx / MelonLoader 那一套。
编出来的东西丢进游戏根目录的 `Mods\` 就能用。

## 目录

- `EnergyBar/` —— 加一根"味道可疑的能量棒"。新开一局或读档时往背包里塞一根，F9 开发面板里也能刷。
- `DayHud/` —— 当铺 HUD：左上角的复古圆形天数面板，显示营业状态、天数、星期、租金倒计时和金额，右侧是一条信用点货币条（悬停亮边并提示余额，点击打开状态页）；表盘下方是通知区，支持限时通知和永久通知（永久通知点 × 才消失），默认显示 5 条，多出的折叠成“已折叠通知（N）”，点击可展开/收起，通知可点击聚焦对应机器。表盘悬停提示“查看日历”，点击打开日历；高级日历里新增“反馈”标签页，直接显示反馈箱的阵营声望面板；左下角新增离店图标，悬停显示门的提示，点击等同“离开店铺”。模组设置里可以恢复墙上的日历、信用点屏幕和反馈箱，也可以恢复旧门的交互，默认都关闭；离开店铺的其他场景时 HUD 会自动隐藏（默认开启）。
- `DebugPanelChinese/` —— F9 调试面板、模组面板和物品生成器的界面汉化。
- `ObjectGrabber/` —— F9 特殊栏里的“对象获取器”：拖一个浮球到游戏对象上，实时显示名字、ID、数值和组件信息。
- `TooltipBars/` —— 把物品提示里的 `[====]` 文字进度条换成真正的 UI Image 进度条（水量、耐久、电量、机器进度等），可在模组设置里关掉。
- `InventorySearch/` —— 仓库检索：F3 搜索全店存货（含机器和盒子内部），按容器/价值/数量筛选排序，点结果打开对应容器并高亮那件物品；窗口里的「归类」按类别把货物挪到暗格/展示柜/前柜/后柜/仓库。
- `ManualSave/` —— 暂停菜单手动存档：点击后调用游戏原生存档并保留游戏已有的历史存档。
- `NeteaseCassette/` —— 网易云磁带机，游戏内扫码登录、点歌生成磁带，用原来的播放器放。
- `Chemistry/` —— 化学实验台：118 种元素单质、32 件化学工具、550 种按真实化学式合成的化合物，F6 打开周期表/合成台/图鉴，原料进垃圾场掉落；另有可前往的「地下实验室」离店场景（独立背景、24 格专用储物、实验室合成、8 章背景故事线）。后续接「化学公司」经营线。详见 [`Chemistry/README.md`](Chemistry/README.md)。

后面写的 mod 也按这个结构放，一个 mod 一个文件夹，里面自带 manifest 和贴图，互相不干扰。
根目录的 `ProbablyStolenMods.sln` 把它们收在一起，一个解决方案编全部。

## DayHud 通知接口

通过游戏公开的 `ModHelper.Lookup` 接入 DayHud。每次发送时查询，服务可用时才调用。

```csharp
using System;

var push = ModHelper.Lookup<Func<string, string, float, bool>>("day_hud", "notice_v1");
push?.Invoke("机器开始运行", "info", 7f);
```

普通通知、永久通知、点击聚焦和点击回调的用法见 [DayHud 接口说明](DayHud/API.md)。

## 编译

需要 .NET SDK（6 以上）或者 Visual Studio 2022。游戏目录只写在根目录的 `Directory.Build.props` 里，改这一处所有 mod 都跟着走：

```xml
<GameDir>E:\SteamLibrary\steamapps\common\Probably Stolen</GameDir>
```

不想改文件也行，命令行临时指定：`dotnet build ProbablyStolenMods.sln -c Release -p:GameDir="D:\...\Probably Stolen"`。

### Visual Studio

双击 `ProbablyStolenMods.sln` 打开。`Mods` 文件夹里是各个 mod 项目；下面的 `Game API (decompiled, read-only reference)` 只是反编译出来的游戏 API 源码，方便写的时候对照，不参与编译。

切到 `Release`，然后 `生成 → 生成解决方案`（Ctrl+Shift+B）就全编一遍。只想编一个就右键那个项目 → 生成。
DLL 直接生成到游戏的 `Mods\<项目名>\`，`manifest.xml`、贴图和本地化文件也会同步到该目录。
比如 Chemistry 生成到 `Mods\Chemistry\`，TooltipBars 生成到 `Mods\TooltipBars\`。
如果某个项目的安装目录名称不同，在该项目的 `.csproj` 中用 `<ModFolderName>目录名</ModFolderName>` 指定。

### 命令行

```
dotnet build ProbablyStolenMods.sln -c Release
```

命令行使用同一套生成规则。要把产物留在项目的 `bin` 目录，可以加 `-p:DeployToMods=false`。

进游戏，模组菜单里启用，再重启一次才生效。

## 备注

- 新加一个 mod 之后，记得把它收进解决方案：`dotnet sln ProbablyStolenMods.sln add 新mod\新mod.csproj`。
- 现在对应游戏 0.46D（100-Rev13）。游戏更新之后 API 可能会动，挂了再修。
- manifest 的 `<Capabilities>` 除了 NeteaseCassette（要联网，声明了 `network`）都是空的，不读写文件也不联网。
- 调试的时候在游戏根目录放个空的 `dev.txt`，F8 看日志，F9 开开发面板。
- `DebugPanelChinese` 只是把界面文字换掉，F9 面板的搜索框仍然按英文按钮名匹配，中文界面下搜索请输入英文关键词。



