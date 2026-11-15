# probably-stolen-mods

《深空当铺》（Probably Stolen）的 mod 源码，自己写着玩，能用就留着。

游戏是 Unity 2021.3 + Mono，自带 mod 加载器，不需要 BepInEx / MelonLoader 那一套。
编出来的东西丢进游戏根目录的 `Mods\` 就能用。

## 目录

- `EnergyBar/` —— 加一根"味道可疑的能量棒"。新开一局或读档时往背包里塞一根，F9 开发面板里也能刷。
- `DayHud/` —— 左上角的复古圆形天数面板，显示天数、星期和租金倒计时；悬停亮边并提示“查看日历”，点击打开日历。
- `DebugPanelChinese/` —— F9 调试面板、模组面板和物品生成器的界面汉化。
- `NeteaseCassette/` —— 网易云磁带机，游戏内扫码登录、点歌生成磁带，用原来的播放器放。

后面写的 mod 也按这个结构放，一个 mod 一个文件夹，里面自带 manifest 和贴图，互相不干扰。
根目录的 `ProbablyStolenMods.sln` 把它们收在一起，一个解决方案编全部。

## 编译

需要 .NET SDK（6 以上）或者 Visual Studio 2022。游戏目录只写在根目录的 `Directory.Build.props` 里，改这一处所有 mod 都跟着走：

```xml
<GameDir>E:\SteamLibrary\steamapps\common\Probably Stolen</GameDir>
```

不想改文件也行，命令行临时指定：`dotnet build ProbablyStolenMods.sln -c Release -p:GameDir="D:\...\Probably Stolen"`。

### Visual Studio

双击 `ProbablyStolenMods.sln` 打开。`Mods` 文件夹里是各个 mod 项目，下面几个是解决方案的公共文件（`.gitignore`、`Directory.Build.props`、`README`）。

切到 `Release`，然后 `生成 → 生成解决方案`（Ctrl+Shift+B）就全编一遍。只想编一个就右键那个项目 → 生成。

### 命令行

```
dotnet build ProbablyStolenMods.sln -c Release
```

编完会自动把 dll、manifest 和贴图拷到游戏目录的 `Mods\<mod名>\`。只想检查能不能编译过、不碰游戏目录，加 `-p:DeployToMods=false`。

进游戏，模组菜单里启用，再重启一次才生效。

## 备注

- 新加一个 mod 之后，记得把它收进解决方案：`dotnet sln ProbablyStolenMods.sln add 新mod\新mod.csproj`。
- 现在对应游戏 0.46D（100-Rev13）。游戏更新之后 API 可能会动，挂了再修。
- manifest 的 `<Capabilities>` 除了 NeteaseCassette（要联网，声明了 `network`）都是空的，不读写文件也不联网。
- 调试的时候在游戏根目录放个空的 `dev.txt`，F8 看日志，F9 开开发面板。
- `DebugPanelChinese` 只是把界面文字换掉，F9 面板的搜索框仍然按英文按钮名匹配，中文界面下搜索请输入英文关键词。
