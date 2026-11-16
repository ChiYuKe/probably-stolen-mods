# DayHud 跨 mod 通知接口（反射接法）

其他 mod **不需要引用 `DayHud.dll`**。没有装 DayHud 时，反射解析不到类型，调用直接跳过，不会让别人的 mod 报错。

## 反射签名

| 程序集 | 类型 |
| --- | --- |
| `DayHud` | `DayHud.DayHudApi` |

| 方法 | 签名 | 说明 |
| --- | --- | --- |
| `Push` | `static bool Push(string message, string tone, float duration)` | 普通通知；`duration <= 0` 为永久通知 |
| `PushPersistent` | `static bool PushPersistent(string message, string tone)` | 永久通知，点 × 才消失 |
| `PushFocus` | `static bool PushFocus(string message, string tone, float duration, GameItem focusItem)` | 点击后打开并居中物品/机器窗口；`duration <= 0` 为永久 |
| `PushAction` | `static bool PushAction(string message, string tone, float duration, Action onClick)` | 点击后执行自定义回调；`duration <= 0` 为永久 |
| `IsAvailable` | `static bool IsAvailable { get; }` | HUD 是否已建好；不判断也可以，`Push` 会排队 |
| `ApiVersion` | `static int ApiVersion { get; }` | 当前为 `2`，后续只增不改 |

`tone` 支持：`info` / `warning` / `danger` / `muted`，也接受 `warn` / `error` / `silent`。

## 复制即用的 Bridge

放在自己 mod 的任意 `.cs` 里即可，不需要 DayHud 引用：

```csharp
using System;
using System.Reflection;
using UnityEngine;

internal static class DayHudBridge
{
    private static Type apiType;
    private static MethodInfo push;
    private static MethodInfo pushFocus;
    private static MethodInfo pushAction;
    private static PropertyInfo isAvailable;
    private static bool loggedError;

    private static bool Resolve()
    {
        if (push != null) return true;

        Type type = Type.GetType("DayHud.DayHudApi, DayHud");
        if (type == null)
        {
            // 加载顺序不保证，按程序集名再兜一次
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (assemblies[i].GetName().Name == "DayHud")
                {
                    type = assemblies[i].GetType("DayHud.DayHudApi");
                    break;
                }
            }
        }

        if (type == null) return false;

        apiType = type;
        push = type.GetMethod("Push", new[] { typeof(string), typeof(string), typeof(float) });
        pushFocus = type.GetMethod("PushFocus",
            new[] { typeof(string), typeof(string), typeof(float), typeof(GameItem) });
        pushAction = type.GetMethod("PushAction",
            new[] { typeof(string), typeof(string), typeof(float), typeof(Action) });
        isAvailable = type.GetProperty("IsAvailable");

        return push != null;
    }

    public static bool IsAvailable
    {
        get
        {
            if (!Resolve() || isAvailable == null) return false;
            return (bool)isAvailable.GetValue(null);
        }
    }

    public static bool Push(string message, string tone = "info", float duration = 7f)
    {
        if (!Resolve() || push == null) return false;
        try
        {
            push.Invoke(null, new object[] { message, tone, duration });
            return true;
        }
        catch (Exception e)
        {
            LogOnce(e);
            return false;
        }
    }

    public static bool PushFocus(string message, GameItem item,
        string tone = "info", float duration = 10f)
    {
        if (!Resolve() || pushFocus == null)
        {
            // 兼容：没装 DayHud，或对方装的版本还没有 PushFocus
            // 退回普通通知，至少消息能显示，不会因为缺少方法而崩
            return Push(message, tone, duration);
        }

        try
        {
            pushFocus.Invoke(null, new object[] { message, tone, duration, item });
            return true;
        }
        catch (Exception e)
        {
            LogOnce(e);
            return false;
        }
    }

    public static bool PushAction(string message, Action action,
        string tone = "info", float duration = 10f)
    {
        if (!Resolve() || pushAction == null)
        {
            return Push(message, tone, duration);
        }

        try
        {
            pushAction.Invoke(null, new object[] { message, tone, duration, action });
            return true;
        }
        catch (Exception e)
        {
            LogOnce(e);
            return false;
        }
    }

    private static void LogOnce(Exception e)
    {
        if (loggedError) return;
        loggedError = true;
        Debug.LogWarning("[DayHudBridge] " + e.Message);
    }
}
```

## 用法

普通通知：

```csharp
DayHudBridge.Push("我的机器开始运行", "info", 6f);
DayHudBridge.Push("水位偏低", "warning", 8f);
```

永久通知（点 × 才消失）：

```csharp
DayHudBridge.Push("水培系统缺水，已暂停", "danger", 0f);

// 或者反射 PushPersistent(string,string)
Type api = Type.GetType("DayHud.DayHudApi, DayHud");
api?.GetMethod("PushPersistent", new[] { typeof(string), typeof(string) })
   ?.Invoke(null, new object[] { "需要手动关闭的提醒", "danger" });
```

点击聚焦物品/机器窗口：

```csharp
// machine 是你自己的事件里拿到的 GameItem；duration 传 0 就是永久通知
DayHudBridge.PushFocus($"产出 {output.name} ×{count}", machine, "info", 10f);
```

点击执行自己的逻辑（不一定是 GameItem）：

```csharp
DayHudBridge.PushAction("客户到店", () => OpenMyCustomerWindow(), "info", 8f);
```

## 注意

- **延迟解析**：不要在 `Init` 里解析一次就把 `null` 缓存死。Bridge 每次 `push == null` 都会重试，等 DayHud 程序集加载后自然能接上。
- **不要手动 `Assembly.LoadFrom` DayHud.dll**，让游戏的 mod 加载器自己加载；手动加载可能造成重复程序集。
- **主线程调用**：Unity 的 UI 操作必须在主线程；mod 的事件回调一般都在主线程。
- **没装 DayHud**：`Type.GetType` 返回 `null`，`Push` 返回 `false`，不会抛异常。
- **永久通知**：`duration <= 0` 或 `PushPersistent` 创建，不会自动过期，行尾显示 `×`，点 `×` 才移除；永久通知折叠在“已折叠通知（N）”里时也不会过期。
- **限时通知**：`duration > 0`，到时间自动淡出；折叠期间暂停计时，展开后才继续。
- **折叠**：默认显示 5 条，多出的收进“已折叠通知（N）”，点击展开全部，底部变成“收起通知”可再折回；最多保留 18 条。
- **装了旧版 DayHud**：旧版本可能只有 `Push`，没有 `PushFocus` / `PushAction`。Bridge 里检测到方法不存在时退回普通通知，消息照常显示，只是不能点击；如果你只打算支持最新版，也可以把 fallback 改成 `return false`。
- **装了但 HUD 还没建好**：`Push` 会先排队（最多 20 条），HUD 建好后补发；`Push` 返回 `true` 表示已接收。
- **旧版 DayHud**：没有 `PushFocus` / `PushAction` 时，Bridge 自动退回 `Push`，不会崩。
- **异常隔离**：`DayHudApi` 的入口内部都做了 try/catch，就算 HUD 内部出错也不会把异常抛回你的 mod，最多返回 `false` 并写一条 `[DayHudApi]` 警告。
- **点击回调异常**：你的 `onClick` 如果抛异常，DayHud 会捕获并写日志，不会卡住通知区或影响其他通知。
- `GameItem` 的窗口：只有带 `contentWindow` 的物品/机器才能聚焦；没有窗口时点击不会发生任何事。
