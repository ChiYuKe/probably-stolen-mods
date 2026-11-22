# 地下实验室（Lab）实现契约

这是本轮「地下实验室」功能的唯一接口契约。所有分工文件必须按下表的**类名/命名空间/公开成员签名**实现，
不要改名、不要改参数列表。命名空间统一 `Chemistry`。

## 目标

在离店交通界面（TransitUI）新增一个可前往地点 **地下实验室**（location id `chemistry_lab`），进门是一个
自定义场景：背景图 + 实验室面板。面板里四页：

1. **实验台**：只用实验室储物格里的材料合成，可一键把缺的料从当铺仓库补进格子；
2. **检测与提炼**：光谱仪揭晓成分。固体废料回收目标金属需要先合成并消耗酸，得到持久化的浸出液；
   漏斗把浸出液变为滤液，还原槽把滤液变为单质。液体、气体及可直接分离的成分使用蒸馏器。
3. **储物格**：基础容量 **344 格**，默认在模组设置中增加 300 格（默认总容量 644 格）；仓库保持 18 列 × 8 行的可视区，可滚动查看后续 2×2 物品位，接受化学物品、实验设备、样品和原版废金属，可从店里存入/取回；
4. **档案**：8 章背景故事线，按进度（首次到访、首次合成、合成酸/有机物/危险品、图鉴 50/150/400）解锁，每章一次性信用点奖励。

## 文件分工

| 模块 | 负责文件 | 不要碰 |
| --- | --- | --- |
| 核心/集成（lead） | `src/ChemistryMod.cs`、`src/Core/**`、`src/Game/**`、`data/LAB_CONTRACT.md`、文档 | — |
| 实验室逻辑 | `src/Lab/LabStorage.cs`、`src/Lab/LabSynthesis.cs`、`src/Lab/LabLocation.cs` | 其它 |
| 故事线 | `src/Story/StoryService.cs`（可新增 `src/Story/StoryData.cs`） | 其它 |
| 实验室界面 | `src/UI/LabPanel.cs`（可新增 `src/UI/LabUi.cs`） | 其它 |
| 美术 | `tools/gen_lab_art.py`、`Textures/Paper/lab_background.png`、`Textures/Paper/lab_card.png`、`tools/lab_contact_sheet.png` | 其它 |

规则和上一轮一样：只改自己的文件；不要改 csproj / Directory.Build.props / 别人的文件；
并行编译用自己的中间目录（见 CATALOG.md 里的命令，把 `obj\xxx` 换成自己的名字）。

## 已确认的游戏 API（不要自己猜）

- 注册地点：`new DelegateAfterhourLocation { ... }`（字段全部 public）→ `ModHelper.AddAfterhourLocation(location)`；
  移除：`ModHelper.RemoveAfterhourLocations(modId)`。
- 到达/离开后游戏会调 `onArrive`/`onLeave`；面板：游戏创建窗口 `afterhour_location`（style meta），
  调 `location.BuildPanel(builder)`，随后自动加「返回当铺」按钮 —— **我们的 BuildPanel 不要自己 Show 窗口**。
- 背景：`MapUIManager.ShowStage` 优先激活 `location.StageObject`（自定义 GameObject），
  没有时才用 `BackgroundKey` 走 `CustomUIManager.GetSprite(key)`。
- 贴图 key 支持 `"chemistry:lab_background"` 形式（`GetSprite` 内部会按 `:` 拆成 modId/spriteKey，
  查 `LoadedMod.Sprites` 和 `LoadedMod.PaperSprites`）；`Textures/Paper/*.png` 会进 PaperSprites。
- 交通界面 `TransitUI` 自动列出所有 `AfterhourLocation.GetDiscovered()` 的地点，卡片显示
  `ImageKey`（`"chemistry:lab_card"`）和 `GetTitle()`。
- 调试直达：`MapUIManager.Instance.Travel(AfterhourLocation.Get("chemistry_lab"))`（`Travel` 是 public）；
  返回：`ModHelper.ReturnFromAfterhour(false)`。
- 到访次数：`ModHelper.GetAfterhourVisits("chemistry_lab")`；地点计数：
  `ModHelper.GetAfterhourCounter/SetAfterhourCounter`。
- 模组存档数据：`ModHelper.SetModData/GetModData(modId, key, ...)`（存 `PlayerStore`，随存档保存）。

## API 契约（必须逐字实现这些公开签名）

### `src/Lab/LabStorage.cs`

```csharp
internal sealed class LabSlot
{
    public string ItemId;   // null/空 = 空格
    public int Count;       // >0 才有意义
}

internal static class LabStorage
{
    public static int Capacity { get; }   // 基础 344 + 模组设置的额外格数
    public const int MaxCountPerSlot = 999;

    public static bool Loaded { get; }
    public static LabSlot[] Slots { get; }      // 长度 Capacity
    public static int UsedSlots { get; }        // Count>0 的格子数

    public static void EnsureLoaded();          // 从 ModHelper.GetModData(modId,"lab_slots") 读；首次/读档后调用
    public static void Save();                  // 写回 "itemId:count|itemId:count"
    public static void ResetAll();              // 清空格子并 Save（调试）

    public static bool IsAllowed(string itemId);                    // 接受已注册的化学物品、样品和原版废金属
    public static int CountOf(string itemId);
    public static System.Collections.Generic.Dictionary<string, int> Counts();

    public static int DepositFromStore(string itemId, int units, out string message);
    public static int WithdrawToStore(string itemId, int units, out string message);
    public static bool TryConsume(string itemId, int units);        // 原子扣减（合成用）
    public static bool TryExchange(string inputId, int inputUnits, string outputId, int outputUnits);
    public static bool Has(string itemId, int units = 1);
    public static void AddUnits(string itemId, int units);          // 合成失败回滚用（不 Save，调用方 Save）
}
```

语义要求：

- **可存物品**：`ChemistryCatalog` 的元素、工具、化合物，`SampleTable` 的未检测或已检测样品（含原版废金属），以及已登记的浸出液和滤液。
- `DepositFromStore`：从当铺仓库扣库存（`StoreInventory.TryConsume`），放进同 id 的已有格子或空格；
  没有空格 → 返回 0 + `message="实验室容量格满了"`；扣不到 → 0 + 原因；成功返回实际存入数并 `Save`。
- `WithdrawToStore`：从格子取出 → `ModHelper.SpawnItem` + `SetItemOwnerShip(true)` +
  `StoreInventory.TryPlace`；放置失败不扣格子；成功扣格子并 `Save`。
- `TryConsume`：先确认数量够再扣，失败不改变任何格子；成功 `Save`。
- `TryExchange`：检查原料和扣料后的产物空间，交换完成后一次写回。空间不足或写入失败时保留原料。
- 持久化容错：数据缺失/损坏/含非法 id 时按空/跳过处理，绝不抛异常到游戏；`Count` 夹在 `1..MaxCountPerSlot`。
- 容量语义：每格只装同一物品 ID，叠加到 `MaxCountPerSlot` 后使用空格；空格用 `ItemId=null`。

### `src/Lab/LabSynthesis.cs`

```csharp
internal static class LabSynthesis
{
    public static SynthesisStatus Check(CompoundDef def);       // 用 LabStorage 的库存判断
    public static int CountCraftable();                         // 现在能用实验室材料做几种
    public static bool Craft(CompoundDef def, out string message);
    public static bool PullFromStore(CompoundDef def, out string message);
    public static string Describe(SynthesisStatus status);      // 缺料/缺工具的说明（UI 用）
}
```

- `Check`：填 `SynthesisStatus.Elements`（ElementNeed：Element/Need/Have/Enough，Have 来自 `LabStorage.CountOf(def.ItemId)`）、
  `MissingTools`（工具物品不在实验室格子里）、`CanCraft`、`Reason`。不要复用 `SynthesisService.Check`
  （那个用的是全店库存），但可以复用 `SynthesisStatus`/`ElementNeed` 类型。
- `Craft`：`Check` 通过后逐个 `LabStorage.TryConsume` 扣元素；产物优先放进实验室空格（`LabStorage.AddUnits`），
  没空格则 `SpawnItem + StoreInventory.TryPlace` 放回店里；任何一步失败要把已扣的元素用 `AddUnits` 还回格子、`Save`，
  并保证「失败不扣料」。成功后调 `StoryService.OnLabSynthesized(def)` 并推 DayHud 通知。
- `PullFromStore`：把 `def` 缺的元素数量、缺的工具各 1 件，用 `LabStorage.DepositFromStore` 从店里搬进格子；
  全部满足返回 true，否则 false + 说明（例如容量格满）。
- `Describe`：`缺元素：氢 H ×2、氧 O ×1；缺工具：蒸馏器` 这种一句话（中英按 `Loc.IsChinese`）。

### `src/Story/StoryService.cs`

```csharp
internal sealed class StoryChapter
{
    public string Id;
    public string TitleZh;
    public string TitleEn;
    public string TextZh;      // 可多段，用 \n
    public string TextEn;
    public string HintZh;      // 未解锁时显示的条件提示
    public string HintEn;
    public int Reward;         // 一次性信用点奖励
    public bool Unlocked;
}

internal sealed class LabProgress
{
    public int Visits;
    public int CraftedKinds;       // 已合成过的不同化合物种数
    public int DangerousCrafted;   // 危险品已合成种数（tnt/nitroglycerin/sodium_azide）
    public int UnlockedChapters;
    public int TotalChapters;
}

internal static class StoryService
{
    public static void Init();                 // 幂等：注册实验室 afterhour 事件 + 订阅 ModHook 事件
    public static void Shutdown();             // 取消订阅 + ModHelper.RemoveAfterhourEvents(modId)
    public static void EnsureLoaded();         // 读存档数据（解锁章节、已合成化合物集合），读档后调用
    public static System.Collections.Generic.IList<StoryChapter> Chapters { get; }
    public static LabProgress GetProgress();
    public static void OnLabArrived();
    public static void OnLabSynthesized(CompoundDef def);
    public static void OnLabDeposit();
    public static void ResetForDebug();
}
```

- 8 章固定顺序（标题/正文全部中英双语，写得到位些，这是「背景故事线」的主体），
  触发条件：首次到访、首次合成、首次合成酸、首次合成有机物、首次合成危险品、
  已合成 50 种、已合成 150 种、已合成 400 种。
- 奖励建议 80/120/150/200/250/300/500/800 信用点（`ModHelper.AddCredit(reward)`），每章只发一次。
- 解锁时 `DayHudBridge.Push(...)` 通知玩家，日志 `ChemistryMod.Log?.Log(...)`。
- 故事内容方向：当铺地板下的暗门 → 前任房主/化学家的旧实验室 → 旧笔记里的配方 → 酸碱、有机、危险品越做越大 →
  笔记反复提到「深空化学公司」→ 终章把线索交给玩家，为后续「化学公司」阶段留钩子。要贴合《深空当铺》的赛博当铺味。
- 事件：往 `chemistry_lab` 注册 2~3 个 `ModHelper.AddAfterhourEvent`（例如「通风橱异响」「旧笔记被动过」
  「公司的人来过」），带中英描述、`AfterhourEventTone`、`dailyChancePercent`；`Shutdown` 时清掉。
- 持久化 `ModHelper.SetModData(modId, "story_unlocked", ...)`、`"lab_crafted"`（化合物 key 逗号分隔）；
  每次变化 `Save`。数据损坏时按空处理，不抛异常。

### `src/Lab/LabLocation.cs`

```csharp
internal static class LabLocation
{
    public const string Id = "chemistry_lab";

    public static AfterhourLocation Registered { get; }
    public static void Init(string modId, ModLog log);   // 构造并注册 DelegateAfterhourLocation；注册 F9 调试入口
    public static void Shutdown();                        // RemoveAfterhourLocations + 销毁背景对象
    public static void EnsureBackdrop();                  // 懒创建背景 GameObject（DontDestroyOnLoad + SpriteRenderer）
    public static void TravelHere();                      // F9：MapUIManager.Instance.Travel(Registered)
    public static void ReturnToStore();                   // F9：ModHelper.ReturnFromAfterhour(false)
}
```

`DelegateAfterhourLocation` 关键字段取值：

```csharp
id = "chemistry_lab", source = modId,
title = () => Loc.Get("lab_title", "地下实验室", "Underground Lab"),
panelTitle = () => Loc.Get("lab_panel_title", "地下实验室", "Underground Lab"),
imageKey = "chemistry:lab_card",
backgroundKey = "chemistry:lab_background",     // 兜底（自建 stage 建不出来时）
backgroundSortingOrder = -1000,
ambienceId = "",
isDiscovered = () => true,
isAvailable = () => true,
usesAfterhourBag = true,           // 夜间背包与场景储物槽之间可拖放
showsStoreCounters = false,
allowsHazardousWaste = false,
countsDumpedWaste = false,
maxConcurrentEvents = 2,
buildPanel = LabPanel.BuildLauncher,
onArrive = OnArrive, onLeave = OnLeave
```

- `EnsureBackdrop`：`new GameObject("ChemistryLabStage")` + `DontDestroyOnLoad` + `SpriteRenderer`，
  sprite 取 `CustomUIManager.Instance?.GetModSprite(modId, "lab_background", true)`；取不到就下次再试。
  把 GameObject 赋给 `Registered.stageObject`。缩放：`Camera.main.orthographicSize*2` 得到视野高，
  按 `sprite.bounds.size` 取 `max(视野宽/ sprite 宽, 视野高/ sprite 高)`；位置对齐相机中心。sortingOrder=-1000。
- `OnArrive`：适配背景 → 载入 `LabStorage` → 打开原版 `GameGridInventory` → 更新故事线。
  `OnLeave`：保存已完成的拖放，关闭场景网格和操作面板。
- F9 调试入口（注册在 `Init` 里，名字带 ASCII 方便搜索）：
  「化学：去地下实验室 · go lab」→ `TravelHere`；「化学：回当铺 · back store」→ `ReturnToStore`。

### `src/UI/LabPanel.cs`

```csharp
internal static class LabPanel
{
    public static void BuildLauncher(CustomUIBuilder builder);   // MapUIManager 的场景入口
    public static void Build(CustomUIBuilder builder);           // 打开实验台时构建操作面板
    public static void Shutdown();
}
```

- 窗口宽高建议 `builder.SetSize(1000f, 740f)`；标题/摘要 + `BeginTabs` 四页：实验台 / 检测与提炼 / 储物格 / 档案。
- **实验台**：550 种配方分页显示（每页 24 行，图标 + 名称/化学式/价值 + 元素需要/已有 + 工具有/缺），
  行内有「合成」「补料」按钮，只有 `Check().CanCraft` 时可点合成；状态行显示上次结果/错误。
- **储物格**：`容量 已用/24 格`；6 列 × 4 行格子（有货显示图标+数量，点击取出 1；空格显示「空」）；
  下面「从店里存入」列表（枚举当铺库存里的化学物品，含名称/店里数量/「存入 1」「全部存入」）。
- **档案**：进度（到访次数、已合成 X/550、危险品 X、章节 X/8）+ 章节列表（已解锁显示正文，未解锁显示标题???和条件提示）。
- 所有游戏侧调用 try/catch；数据为空/贴图 null/窗口被关掉都不能抛异常；按钮点击后刷新本页。
- 文案 `Loc.Get(key, zh, en)` / `Loc.Pick`；物品名从数据表取；不要硬编码元素/化合物清单。
- 复用 `src/UI/ChemUi.cs` 的样式帮助（`ChemUi.Style/StyleButtonText/ModSprite/RowLayout/RowBackground/SetText` 等）。

## 贴图约定（美术）

- `Textures/Paper/lab_background.png`：16:9 场景背景（建议 1920×1080），
  风格：当铺地下的旧化学实验室，暗紫/青绿工业色、管线、货架上的试剂瓶、旧灯、暗门；像素/扁平混合，别太糊。
- `Textures/Paper/lab_card.png`：交通卡片图，显示区域 296×168（建议 592×336），同一场景的插画/徽章。
- `tools/lab_contact_sheet.png`：两张图放大拼图，供人眼验收。
- 脚本 `tools/gen_lab_art.py` 可重复生成，stdout 打印尺寸/文件。

## 验收

- `dotnet build Chemistry\Chemistry.csproj -c Release -p:DeployToMods=false` 0 error。
- 进游戏：离店交通界面能看到「地下实验室」卡片（有图）；选中前往后出现背景 + 面板；
  四页能开；从店里存 24 格上限生效；检测和提炼结果入格；实验室合成扣格子材料并产出入格；
  首次到访/首次合成触发档案章节和信用点奖励；回当铺正常。
- 数据/贴图由 lead 的验收脚本复核。
