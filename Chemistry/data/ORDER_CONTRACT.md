# 委托（Order）实现契约

这是 `docs/GAMEPLAY_ROADMAP.md` 落地时的接口契约。命名空间统一 `Chemistry`，
公开成员签名按本文实现，不要改名、不要改参数列表。

## 目标

把「接到一笔生意 → 从废料里找出成分 → 亲手生产 → 交货拿钱 → 扩产接更大的单」做成一条
可追踪、可交付、有报酬的委托线。委托记录独立于八章档案：档案记探索进度，委托记生意。

## 文件分工

| 模块 | 负责文件 |
| --- | --- |
| 委托数据 | `src/Order/OrderData.cs` |
| 委托状态机 / 存档 / 结算 | `src/Order/OrderService.cs` |
| 开局核查与借用工位 | `src/Order/OrderSupply.cs` |
| 接单 / 收货 NPC | `src/Order/OrderNpcService.cs` |
| 委托界面 | `src/UI/OrderPanel.cs` |
| 原料渠道盘点（M1） | `src/Data/ElementSources.cs` |

## 数据模型

```csharp
internal enum OrderKind { Recover, SupplyReagent, SupplySolid, Contract }

internal sealed class OrderRequirement
{
    public readonly string ItemId;
    public readonly int Count;
    public readonly string NoteZh;
    public readonly string NoteEn;
    public OrderRequirement(string itemId, int count, string noteZh = null, string noteEn = null);
    public string Name { get; }        // LabUi.Resolve(ItemId).Name
    public string Note { get; }        // Loc.Pick(NoteZh, NoteEn)
}

internal sealed class OrderStep
{
    public readonly string Id;
    public readonly string TitleZh, TitleEn;
    public readonly string HintZh, HintEn;
    public bool Done;
    public string Title { get; }
    public string Hint { get; }
}

internal sealed class OrderTemplate
{
    public string Id;
    public OrderKind Kind;
    public string TitleZh, TitleEn;
    public string RequesterZh, RequesterEn;
    public string BriefZh, BriefEn;
    public string NpcId;            // OrderNpcService 的立绘 id
    public int Reward;              // 信用点
    public int DurationDays;        // 0 = 无期限
    public bool Repeatable;
    public string RequiresOrder;    // 前置委托 id，null = 无
    public string RequiresChapter;  // 需要解锁的档案章节 id，null = 无
    public int RequiresDay;         // 最少游戏天数，0 = 无
    public OrderRequirement[] Requirements;
    public OrderStep[] Steps;
    public string[] TeachingSteps;  // 结算前必须完成的步骤 id（不含 deliver）
}
```

## `src/Order/OrderService.cs`

```csharp
internal sealed class OrderState
{
    public string TemplateId;
    public int Number;                     // 独立订单编号，从 1 递增
    public int AcceptedDay;
    public int DeadlineDay;                // 0 = 无期限
    public HashSet<string> DoneSteps;
    public Dictionary<string, int> Delivered;
}

internal sealed class OrderSettlement
{
    public int Number;
    public string TemplateId;
    public int Reward;
    public int Day;
}

internal static class OrderService
{
    public static void Init(string modId, ModLog log);
    public static void Shutdown();
    public static void EnsureLoaded();
    public static void Save();

    public static IList<OrderTemplate> Templates { get; }
    public static OrderTemplate Find(string id);
    public static OrderState Active { get; }              // null = 没有进行中的委托
    public static OrderTemplate ActiveTemplate { get; }
    public static IList<OrderSettlement> History { get; }

    public static bool IsCompleted(string templateId);     // 一次性委托是否已完成
    public static bool IsAvailable(OrderTemplate template); // 现在能不能接
    public static List<OrderTemplate> Available();          // 可接取列表（按模板顺序）

    public static bool Accept(string templateId, out string message);
    public static bool Abandon(out string message);

    // 进度事件：全部幂等，没有进行中的委托时静默返回
    public static void OnSampleAnalyzed(SampleDef sample);
    public static void OnCompoundProduced(CompoundDef def);
    public static void OnElementRecovered(string symbol);
    public static void OnLabVisited();

    public static bool IsStepDone(string stepId);
    public static void MarkStep(string stepId);            // 幂等，内部 Save

    public static int Deliverable(OrderRequirement requirement);  // 店里现有可交付数量
    public static bool RequirementsMet(out string reason);
    public static bool CanSettle(out string reason);
    public static bool Settle(out string message, out int paid);

    public static string ProgressLine();      // 一行摘要
    public static string NextObjective();     // 当前目标（流程指南 / HUD）
    public static void ResetForDebug();
}
```

语义要求：

- **存档键**（`ModHelper.SetModData(modId, key, value)`）：`order_active`、`order_done`、
  `order_history`、`order_seq`、`order_borrowed`。数据缺失/损坏一律按空处理，绝不抛异常。
- `Accept`：已有进行中的委托 → false + 说明；`IsAvailable` 为假 → false + 原因。
  成功后 `Number = ++order_seq`、`AcceptedDay = StoreStation.GetDayCounter()`、
  `DeadlineDay = DurationDays > 0 ? AcceptedDay + DurationDays : 0`，清空步骤与交付量，`Save`。
- `Settle`：先 `CanSettle`（教学步骤全完成 + 店里数量够），再**先扣库存再发钱**；
  扣库存失败 → 不发钱、不结单；发钱用 `ModHelper.AddCredit`。
  成功后写 `order_history`、一次性委托写进 `order_done`、清空 `order_active`、`Save`。
  重复点击不会双倍发钱（`Active` 已清空）。
- `Deliverable`：`StoreInventory.CountUnits()` 里该 id 的数量；异常按 0。
- `NextObjective`：没有进行中的委托时给「去当铺接单」类提示；有委托时给第一个未完成步骤的 Hint。
- 所有游戏侧调用 try/catch；`StoreStation` / `PlayerStore` 为空时按 0 天处理。

## 委托模板（第一版到后期）

| id | 类型 | 前置 | 交付 | 报酬 |
| --- | --- | --- | --- | --- |
| `rust_trial_iron` | Recover | — | 铁 ×1 | 260 |
| `rust_copper_recovery` | Recover | `rust_trial_iron` | 铜 ×2 | 420（可重复） |
| `rust_reagent_restock` | SupplyReagent | `rust_trial_iron` | 盐酸 ×12 | 360（可重复） |
| `dsc_nitric_contract` | Contract | `rust_reagent_restock` + 档案 ch5 + 第 12 天 | 硝酸 ×24 | 900（可重复） |
| `dsc_solid_contract` | Contract | `dsc_nitric_contract` | 盐 ×8 | 700（可重复） |

步骤 id 约定：

- `report`：看过成分报告（`OnSampleAnalyzed` 且样品含该委托的目标元素）
- `reagent`：产出该委托路线需要的试剂（`OnCompoundProduced`）
- `recover`：亲手回收出目标单质（`OnElementRecovered`）
- `produce`：产出交付物（`OnCompoundProduced`）
- `deliver`：交付（`Settle` 内部标记）

## `src/Order/OrderSupply.cs`

```csharp
internal static class OrderSupply
{
    internal sealed class Missing
    {
        public string ItemId;
        public int Count;
        public string ReasonZh, ReasonEn;
    }

    public static List<Missing> Audit(OrderTemplate template);   // 首单缺什么
    public static bool Borrow(OrderTemplate template, out string message);
    public static bool CanBorrow(OrderTemplate template);
    public static string Describe(OrderTemplate template);       // 一句话清单
}
```

- `Audit`：核查光谱仪、制酸器具、空瓶、过滤漏斗、废料数量，以及制酸配方的全部上游元素。
  只列「店里和实验室都没有」的项。
- `Borrow`：给缺的工具各 1 件、缺的消耗品按一批用量、缺的样品给批次 01。
  每个订单编号只能领一次（`order_borrowed` 记录 `number`），避免重复接单套取物资。
- 关闭开局补给（`give_starter_kit=false`）时同样可用。

## `src/Order/OrderNpcService.cs`

```csharp
internal static class OrderNpcService
{
    public static void QueueForDay(StoreClientManager manager);
    public static void Shutdown();
    public static void OfferHere();      // 立刻把可接的委托 NPC 排进今天的顾客队列（调试/UI 用）
}
```

- 挂在 `ModHook.OnGenerateCustomerLate`（和 `ChemistryNpcService` 同一时机）。
- 有可接委托 → 排一个 `StoreClient`（`ClientIntent.DIALOGUE`），对白介绍委托，
  结束时 `OrderService.Accept`，并 `OrderPanel.Open()` 打开委托记录。
- 有进行中的委托且可结算 → 排收货 NPC，对白提示回店交付，结束时打开委托记录。
- 立绘复用 `Textures/Characters/npc_scrap_collector.png` 等已有资源，缺失时用随机原版立绘。

## `src/UI/OrderPanel.cs`

```csharp
internal static class OrderPanel
{
    public static void Open();                                  // 独立窗口
    public static void Close();
    public static void BuildPage(CustomUIBuilder builder, CustomUIWindow host);  // 嵌进标签页
    public static void Refresh(bool resetScroll);
    public static void Shutdown();
}
```

- 独立窗口 id `chemistry.orders`，宽高 `860 × 620`；标签页版本由宿主传入 `host`。
- 内容分三块：**进行中**（编号、委托人、剩余天数、步骤勾选、交付槽数量、交付按钮）、
  **可接取**（标题、委托人、交付要求、报酬、「接单」按钮）、**已结算**（编号、报酬、日期）。
- 交付区显示：要求数量、店里现有、将交出数量、到手信用点；数量不足时按钮灰掉并给出原因。
- 所有游戏侧调用 try/catch；数据为空/贴图 null/窗口被关掉都不能抛异常。

## `src/Data/ElementSources.cs`（M1）

```csharp
internal static class ElementSources
{
    internal sealed class Channel
    {
        public string Kind;        // sample / compound / loot / purchase
        public string Ref;
        public string DetailZh, DetailEn;
    }

    public static List<Channel> For(string symbol);
    public static bool HasAnyChannel(string symbol);
    public static List<string> ElementsWithoutChannel();
    public static string Describe(string symbol);
    public static void LogReport();     // F9：逐元素打印渠道，标出缺口
}
```

- 渠道来源按数据表枚举：含该元素的样品批次（含酸浸/蒸馏路线）、能分解出该元素的化合物、
  原版掉落表里已有的原版物品（如 `scrap_metal`）、以及委托采购。
- `ElementsWithoutChannel` 只用于诊断，不阻塞游戏。

## 验收

- `dotnet build Chemistry\Chemistry.csproj -c Release -p:DeployToMods=false` 0 警告 0 错误。
- 新档关闭开局补给：接首单 → 借用工位补齐 → 检测批次 01 或普通废金属 → 制盐酸 → 酸浸/过滤/还原
  得到铁 → 回店交付 → 信用点到账、订单进历史、可以接下一单。
- 重复点交付、仓满、余额不足、错误物品都不吞料、不双倍发钱。
- 普通与新鲜垃圾场的掉落路径里没有单质直掉。
