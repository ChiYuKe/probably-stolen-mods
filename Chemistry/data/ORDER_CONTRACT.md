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

    /// <summary>液体单子要求满瓶数量；0 = 不校验瓶数。</summary>
    public readonly int Bottles;

    /// <summary>指定瓶型尺寸：0 小 / 1 中 / 2 大；-1 = 不限定。</summary>
    public readonly int BottleSize;

    public OrderRequirement(string itemId, int count, string noteZh = null, string noteEn = null,
        int bottles = 0, int bottleSize = -1);
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
    public int Lots = 1;            // 要分几批交货才结清；长期合同是 2
    public int DurationDays;        // 0 = 无期限
    public bool Repeatable;
    public string RequiresOrder;    // 前置委托 id，null = 无
    public string RequiresChapter;  // 需要解锁的档案章节 id，null = 无
    public int RequiresDay;         // 最少游戏天数，0 = 无
    public bool RequiresAnalysis;   // 要先做过一次成分检测才派单（首单用）
    public OrderRequirement[] Requirements;   // Lots > 1 时是「每一批」的量
    public OrderStep[] Steps;
    public string[] TeachingSteps;  // 结算前必须完成的步骤 id（不含 deliver）
    public string ReagentKey;       // 这条路线要用的试剂 key
    public string TargetSymbol;     // 这条路线要回收的目标元素符号
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
    public int DeliveredLots;              // 已交几批（长期合同要交满 Lots 批）
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

    public static bool IsCompleted(string templateId);     // 是否至少结清过一次
    public static bool IsAvailable(OrderTemplate template); // 现在能不能接
    public static List<OrderTemplate> Available();          // 可接取列表（按模板顺序）
    public static string UnavailableReason(OrderTemplate template);

    public static bool Accept(string templateId, out string message);
    public static bool Abandon(out string message);

    // 进度事件：全部幂等。接单之前发生过的事也会记进全局履历，接单时补记（允许先做后看）
    public static void OnSampleAnalyzed(SampleDef sample);
    public static void OnCompoundProduced(CompoundDef def);
    public static void OnCompoundProduced(CompoundDef def, int units);
    public static void OnElementRecovered(string symbol);
    public static void OnLabVisited();

    public static bool IsStepDone(string stepId);
    public static void MarkStep(string stepId);            // 幂等，内部 Save
    public static bool HasSeenElement(string symbol);      // 是否看过含该元素的报告
    public static int ProducedUnits(string itemId);        // 累计产出单位数

    public static int Deliverable(OrderRequirement requirement);  // 店里现有可交付数量
    public static int FullBottles(OrderRequirement requirement);  // 店里符合指定尺寸且装满的该液体瓶数
    public static bool RequirementsMet(out string reason);
    public static bool CanSettle(out string reason);
    public static bool Settle(out string message, out int paid);

    public static string ProgressLine();      // 一行摘要
    public static string NextObjective();     // 当前目标（流程指南 / HUD）
    public static string SampleAdvice(SampleDef sample);  // 成分报告里的委托提示
    public static void InvalidateCounts();
    public static void ResetForDebug();
}
```

语义要求：

- **存档键**（`ModHelper.SetModData(modId, key, value)`）：`order_active`、`order_done`、
  `order_history`、`order_seq`、`order_borrowed`，以及全局履历 `order_seen` / `order_made` /
  `order_recovered` / `order_produced`。数据缺失/损坏一律按空处理，绝不抛异常。
- **全局履历**：`order_seen` 记看过成分报告的元素符号，`order_made` 记合成/制酸过的化合物 key，
  `order_recovered` 记亲手回收过的单质符号，`order_produced` 记累计产出单位数。
  接单时按这些履历补记步骤，所以「先检测后接单」不会被判成没做过。
- `Accept`：已有进行中的委托 → false + 说明；`IsAvailable` 为假 → false + 原因。
  成功后 `Number = ++order_seq`、`AcceptedDay = StoreStation.GetDayCounter()`、
  `DeadlineDay = DurationDays > 0 ? AcceptedDay + DurationDays : 0`，清空步骤与交付量，
  再按全局履历补记步骤、发首单保底样品，最后 `Save`。
- `Settle`：先 `CanSettle`（教学步骤 + 店里数量 + 满瓶数），再**先扣库存再发钱**。
  `Lots > 1` 时每次只扣一批并 `DeliveredLots++`，交满 `Lots` 批才发钱、写 `order_history`、
  写 `order_done` 并清空 `order_active`；未交满时订单保持进行中。
  扣库存失败 → 不发钱、不结单；发钱失败 → 把料退回店里。
  重复点击不会双倍发钱（`Active` 已清空）。
- `CanSettle` 里 `Bottles > 0` 的要求会校验指定尺寸的满瓶数；盐酸补货单固定为 3 个满小瓶。
  半瓶、空瓶、破损瓶、中大瓶和别的液体都不算。瓶容量由 `BottleSize` 对应，小瓶 4 单位；
  必须满足 `Count = Bottles × 单瓶容量`。`TryConsumeBottled` 只扣指定尺寸的满瓶，
  每个交付瓶整瓶移除，不会从半瓶补差或只扣瓶内液量而留下空瓶。
- 机器订单的产量按 `MachineBatch` 实际提交的 `Product.Units` 累加；容器容量只是上限，
  不会因为选用更大的空瓶就提前完成 `produce` 步骤。
- `Deliverable`：`StoreInventory.CountUnits()` 里该 id 的数量；异常按 0。
- `NextObjective`：没有进行中的委托时给「去当铺接单」类提示；有委托时给第一个未完成步骤的 Hint。
- 所有游戏侧调用 try/catch；`StoreStation` / `PlayerStore` 为空时按 0 天处理。

## 委托模板（第一版到后期）

| id | 类型 | 前置 | 交付 | 批数 | 报酬 |
| --- | --- | --- | --- | --- | --- |
| `rust_trial_iron` | Recover | — | 铁 ×1 | 1 | 260 |
| `rust_copper_recovery` | Recover | `rust_trial_iron` | 铜 ×2 | 1 | 420（可重复） |
| `rust_reagent_restock` | SupplyReagent | `rust_trial_iron` | 盐酸 ×12（3 个满小瓶） | 1 | 360（可重复） |
| `dsc_nitric_contract` | Contract | `rust_reagent_restock` + 档案 ch5 + 第 12 天 | 硝酸 ×24/批 | 2 | 900（可重复） |
| `dsc_solid_contract` | Contract | `dsc_nitric_contract` | 盐 ×8/批 | 2 | 700（可重复） |

步骤 id 约定：

- `report`：看过成分报告（`OnSampleAnalyzed` 记下样品里出现的元素符号）
- `reagent`：产出该委托路线需要的试剂（`OnCompoundProduced` 记下化合物 key）
- `recover`：亲手回收出目标单质（`OnElementRecovered`）
- `produce`：累计产出量达到交付要求（`order_produced` 累计单位数）
- `deliver`：交付（`Settle` 内部标记）

**先做后看**：`report` / `reagent` / `recover` / `produce` 四个步骤都读全局履历，
接单前做过的事在 `Accept` 时补记，玩家不会因为「先扫了再点接单」被卡住。

**开局顺序**：首单 `RequiresAnalysis = true`，玩家没做过一次成分检测之前老锈不会派单。
配合 `LootIntegration` 的首翻保底（第一次在垃圾场翻出东西时换成批次 01），
「翻垃圾 → 检测 → 老锈出现」的顺序是固定的。

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
    public static bool EnsureStarterSample(OrderTemplate template);  // 首单保底样品
    public static string Describe(OrderTemplate template);       // 一句话清单
}
```

- `Audit`：核查光谱仪、制酸器具、空瓶、过滤漏斗、废料数量，以及制酸配方的全部上游元素。
  只列「店里和实验室都没有」的项。
- `Borrow`：给缺的工具各 1 件、缺的消耗品按一批用量、缺的样品给批次 01。
  **只有整批都放进仓库才消耗那一次机会**（`order_borrowed` 记录 `number`）；仓库满时领到一部分，
  清出空间还能再领剩下的 —— 没领到的部分下次 `Audit` 仍会列出来，不会重复发放。
- `EnsureStarterSample`：接单时手上完全没有含目标元素的废料，就直接给一批 01 号样品，
  免得第一次翻垃圾空手而归。不占用借用工位的机会。
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
- 先扫废料再点接单，「看过报告」那一步要能直接抵掉。
- 重复点交付、仓满、余额不足、错误物品都不吞料、不双倍发钱。
- 补货单：半瓶、空瓶、别的酸都不能顶「3 个满瓶」。
- 长期合同：两批分别交付，第一批不结账，第二批交完才付一次钱。
- 普通与新鲜垃圾场的掉落路径里没有单质直掉。
