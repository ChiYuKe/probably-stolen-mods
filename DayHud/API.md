# DayHud 模组接入

在游戏主线程调用 `ModHelper.Lookup`。DayHud 启用后会注册以下服务。服务尚未注册时，查询返回 `null`。

| 服务键 | 委托签名 | 用途 |
| --- | --- | --- |
| `notice_v1` | `Func<string, string, float, bool>` | 普通通知 |
| `notice_focus_v1` | `Func<string, string, float, GameItem, bool>` | 点击后聚焦物品或打开其设备窗口 |
| `notice_action_v1` | `Func<string, string, float, Action, bool>` | 点击后执行调用方提供的回调 |

三个委托的前三个参数依次为消息、语气和显示秒数。语气可用 `info`、`warning`、`danger`、`muted`。传入 `0f` 可创建永久通知，由玩家手动关闭。返回 `true` 表示 DayHud 已接收消息。

## 示例

```csharp
using System;

var notice = ModHelper.Lookup<Func<string, string, float, bool>>("day_hud", "notice_v1");
notice?.Invoke("水位偏低", "warning", 8f);

var focus = ModHelper.Lookup<Func<string, string, float, GameItem, bool>>("day_hud", "notice_focus_v1");
focus?.Invoke("机器产出蘑菇 ×3", "info", 10f, machineItem);

var action = ModHelper.Lookup<Func<string, string, float, Action, bool>>("day_hud", "notice_action_v1");
action?.Invoke("客户到店", "info", 8f, OpenMyCustomerWindow);
```

在发送通知时查询服务，适应模组载入顺序和启停。调用方使用上述接口即可避免为通知接入引入反射扫描警告。

## 时钟

DayHud 从 `ModHelper.Lookup<Func<(int Day, int MinuteOfDay, bool Waiting)>>("chemistry", "clock_display_v1")` 取得 Chemistry 提供的显示时间。全天分钟范围为 `0–1439`，`Day` 小于 1 时隐藏时分。

## 既有静态接口

`DayHud.DayHudApi` 保留以下成员供已有调用方使用。接口版本为 `4`。

| 成员 | 签名 |
| --- | --- |
| `Push` | `static bool Push(string message, string tone, float duration)` |
| `PushPersistent` | `static bool PushPersistent(string message, string tone)` |
| `PushFocus` | `static bool PushFocus(string message, string tone, float duration, GameItem focusItem)` |
| `PushAction` | `static bool PushAction(string message, string tone, float duration, Action onClick)` |
| `IsAvailable` | `static bool IsAvailable { get; }` |
| `Notify`、`NotifyPersistent`、`Focus` | 保留现有强类型重载 |
| `SetClockSource`、`ClearClockSource` | 接收 `Func<(int Day, int MinuteOfDay, bool Waiting)>` |
| `TryGetGameTime` | `static bool TryGetGameTime(out int day, out int hour, out int minute)` |

游戏的权限扫描按调用方 DLL 检查。旧模组如果通过动态反射查找上述成员，可能需要改用模组服务并重新编译。DayHud 的升级无法修改其他模组已编译的调用代码。
