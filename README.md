# probably-stolen-mods

《深空当铺》（Probably Stolen）的 mod 源码，自己写着玩，能用就留着。

游戏是 Unity 2021.3 + Mono，自带 mod 加载器，不需要 BepInEx / MelonLoader 那一套。
编出来的东西丢进游戏根目录的 `Mods\` 就能用。

## 目录

- `EnergyBar/` —— 加一根"味道可疑的能量棒"。新开一局或读档时往背包里塞一根，F9 开发面板里也能刷。
- `DayHud/` —— 左上角的复古圆形天数面板，显示营业状态、天数、星期、租金倒计时和金额，右侧是一条信用点货币条（悬停亮边并提示余额，点击打开状态页）；表盘下方是通知区，按条显示水培产出等事件，点击通知可以直接聚焦对应的机器窗口（F9 调试菜单可推送测试通知）。表盘悬停提示“查看日历”，点击打开日历。模组设置里可以分别恢复墙上的日历和信用点屏幕（默认都隐藏），也可以设置离开店铺的其他场景时自动隐藏 HUD（默认开启）。
- `DebugPanelChinese/` —— F9 调试面板、模组面板和物品生成器的界面汉化。
- `ObjectGrabber/` —— F9 特殊栏里的“对象获取器”：拖一个浮球到游戏对象上，实时显示名字、ID、数值和组件信息。
- `NeteaseCassette/` —— 网易云磁带机，游戏内扫码登录、点歌生成磁带，用原来的播放器放。

后面写的 mod 也按这个结构放，一个 mod 一个文件夹，里面自带 manifest 和贴图，互相不干扰。
根目录的 `ProbablyStolenMods.sln` 把它们收在一起，一个解决方案编全部。

## DayHud 通知接口

游戏自带的 mod 加载器没有统一的通知总线，也没有跨 mod 发消息的接口；`ModHook` 只有游戏加载、开关门、离店/回店、入夜这类生命周期事件，`ModHelper` 则是注册物品/选项/地点之类的 API。所以其他 mod 想往 DayHud 的通知区推消息，需要走 DayHud 暴露的静态接口。

**不要硬引用 `DayHud.dll`**，不然别人没装 DayHud 时他的 mod 会在加载阶段直接报错。用反射，并且**延迟到真正要推的时候再解析类型**（mod 加载顺序不保证）：

```csharp
using System;
using System.Reflection;

private static MethodInfo push;

private static bool TryPush(string message, string tone = "info", float duration = 7f)
{
    if (push == null)
    {
        Type api = Type.GetType("DayHud.DayHudApi, DayHud");
        push = api?.GetMethod("Push", new[] { typeof(string), typeof(string), typeof(float) });
    }

    if (push == null)
    {
        return false; // 没装 DayHud，跳过
    }

    push.Invoke(null, new object[] { message, tone, duration });
    return true;
}
```

可点击版本：

```csharp
MethodInfo pushFocus = api?.GetMethod("PushFocus",
    new[] { typeof(string), typeof(string), typeof(float), typeof(GameItem) });

pushFocus?.Invoke(null, new object[] { "机器产出 蘑菇 ×3", "info", 10f, machineItem });
```

- `Push` / `PushFocus` 的参数只有 `string` / `float` / 游戏自带的 `GameItem`，不依赖 DayHud 里的枚举或委托，反射签名稳定。
- `tone` 支持 `info` / `warning` / `danger` / `muted`，也接受 `warn` / `error` / `silent`。
- 没装 DayHud 时 `Type.GetType` 返回 `null`，调用直接跳过，不会影响别的 mod。
- DayHud 装了但 HUD 还没建好时，`Push` 会先排队（最多 20 条），HUD 建好后补发。
- 也可以用反射读 `DayHudApi.IsAvailable` 判断当前是否能立刻显示；不判断直接 `Push` 也会进队列。
- 水培产出这类 DayHud 自己监听的事件不需要其他 mod 调接口；只有其他 mod 自己的事件才需要主动推。

强类型 API（`DayHudApi.Notify`、`DayHudApi.Focus`）也保留着，愿意硬引用的人可以用，但不是推荐接法。

完整的复制即用 Bridge、`PushFocus` / `PushAction` 用法和注意事项见 [`DayHud/API.md`](DayHud/API.md)。

## 编译

需要 .NET SDK（6 以上）或者 Visual Studio 2022。游戏目录只写在根目录的 `Directory.Build.props` 里，改这一处所有 mod 都跟着走：

```xml
<GameDir>E:\SteamLibrary\steamapps\common\Probably Stolen</GameDir>
```

不想改文件也行，命令行临时指定：`dotnet build ProbablyStolenMods.sln -c Release -p:GameDir="D:\...\Probably Stolen"`。

### Visual Studio

双击 `ProbablyStolenMods.sln` 打开。`Mods` 文件夹里是各个 mod 项目；下面的 `Game API (decompiled, read-only reference)` 只是反编译出来的游戏 API 源码，方便写的时候对照，不参与编译。

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
