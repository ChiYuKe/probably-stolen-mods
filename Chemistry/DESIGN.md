# 化学实验台（Chemistry）设计文档

深空当铺的化学模组。目标是把「元素周期表 → 化学工具 → 化学式合成 → 化学公司」这一条线
做成一个能玩、能卖钱、能继续往下扩的系统。

游戏：深空当铺 0.46D（Unity 2021.3 + Mono，自带 mod 加载器）
模组 id：`chemistry`　程序集：`Chemistry.dll`　标签：F6 打开实验台（可改键）

---

## 1. 分阶段目标

| 阶段 | 内容 | 状态 |
| --- | --- | --- |
| 一：元素 | 118 种元素单质做成物品，有周期表界面可查。垃圾场掉未知样品，检测后可提炼其中的元素 | 单质物品已实测，检测提炼待游戏内验收 |
| 二：工具 | 32 件化学工具做成物品，合成配方要消耗/要求对应工具 | ✅ 已实现并游戏内实测 |
| 三：化合物 | 550 种按真实化学式合成的化合物，新物品、有新价值，可卖 | ✅ 已实现并游戏内实测 |
| 三点五：地下实验室 | 独立离店场景、默认 644 格可配置专用储物、实验室合成、8 章背景故事线 | ✅ 已实现（实测见 docs） |
| 三点六：委托线 | 当铺接单 → 找料送检 → 制剂 → 回收 → 交货拿钱 → 扩产接更大的单 | ✅ 已实现（见第 5.2 节与 `docs/GAMEPLAY_ROADMAP.md`） |
| 四：化学公司 | 公司等级、许可证、产线、批量订单、员工/自动合成 | 后续迭代（本文第 7 节） |

已把一到三加地下实验室打通、能在游戏里实际产出并卖钱；公司系统先留接口和数据位，下一轮再做。
实测记录（日志、截图、复跑方法）见 [`docs/VERIFICATION.md`](docs/VERIFICATION.md)。

## 2. 玩法循环

```
翻垃圾场 ──> 捡到未知样品 / 化学工具 / 现成化合物
   │
   ├─> 样品存入地下实验室 ──> 成分检测 ──> 选择目标成分提炼单质
   │
   ├─> 元素攒够 ──> F6 打开实验台 ──> 合成台按化学式合成 ──> 新化合物进仓库
   │
   └─> 化合物/工具/元素摆上柜台 ──> 卖给顾客 ──> 信用点 ──> （阶段四：投进化学公司）
```

- 未知样品使用 `chemSampleTable`（塞进 `dumpingGroundTG` /
  `dumpingGroundFreshTG` 两个表组）、化学工具按权重加进原版 `toolTable`、
  化合物单独成表 `chemCompoundTable`。表 id 来自真实掉落导出，见第 6 节。
- 开局给一套基础补给（可在模组设置里关掉），保证第一次打开实验台就能合成水/盐，
  不至于空手看不出玩法。
- 合成是「按化学式扣元素 + 检查工具」：例如水 `H2O` 扣 2 单位 H + 1 单位 O，
  需要烧杯和本生灯；阿司匹林 `C9H8O4` 扣 9 C + 8 H + 4 O，需要锥形瓶、蒸馏器和天平。
  扣料和产出都在 `SynthesisService` 里做，界面只负责显示。

## 3. 物品与数据规模

| 类别 | 数量 | id 规则 | 贴图 |
| --- | --- | --- | --- |
| 元素单质 | 118 | `chem_el_<符号小写>` | `el_<符号小写>.png` |
| 化学工具 | 32 | `chem_tool_<key>` | `tool_<key>.png` |
| 化合物 | 550 | `chem_cmp_<key>` | `cmp_<key>.png` |

- 数据源：`data/elements.csv`、`data/tools.csv`、`data/compounds.csv`。
- 用 `tools/gen_*.py` 从 CSV 生成 C# 数据表（`src/Data/*.cs`）和贴图（`Textures/Items/*.png`），
  CSV 是唯一事实来源，改数值/改配方改 CSV 再重跑脚本。
- 元素价值按稀有度分层（常见轻元素 3~15，普通金属 12~40，贵金属 120~220，放射性更高）；
  工具 20~420；化合物 12~400。价值直接进游戏经济，太贵会破坏原版节奏，所以按这个区间控制。
- 化合物化学式由 `FormulaParser` 解析（支持括号与下标），**合成扣料和化学式永远一致**，
  不会出现「写的是 H2O、扣的是 H2O2」这种数据错位。

## 4. 代码结构

```
Chemistry/
├─ Chemistry.csproj / manifest.xml
├─ data/                  CSV 数据源 + CATALOG.md（数据契约）
├─ tools/                 生成脚本（gen_elements.py / gen_compounds.py / gen_textures.py / verify.py）
├─ Textures/Items/        32×32 物品贴图
├─ Localization/          （可选）UI 文案 CSV
└─ src/
   ├─ ChemistryMod.cs     IMod 入口：事件挂载、设置、F9 调试入口
   ├─ Core/
   │  ├─ Defs.cs           ElementDef / ToolDef / CompoundDef
   │  ├─ ChemistryCatalog.cs  mod id、物品 id 拼法、分类显示名、掉落权重
   │  ├─ FormulaParser.cs  化学式解析（H2O / Ca(OH)2 …）
   │  ├─ Loc.cs            文案查询（本地化 CSV + 中英兜底）
   │  └─ HotkeyWatcher.cs  快捷键宿主
   ├─ Data/                生成的元素/工具/化合物表
   │  └─ ElementSources.cs 逐元素的获取渠道盘点（M1）
   ├─ Game/
   │  ├─ ItemRegistration.cs  往 ModItemDirectory 注册全部物品
   │  ├─ StoreInventory.cs    扫描玩家店铺容器、统计/扣减元素单位
   │  ├─ SynthesisService.cs  合成检查与执行（扣料 + 产出）
   │  ├─ LootIntegration.cs   掉落表接入 / 撤销（不再挂单质直掉）
   │  ├─ DebugKit.cs          F9 补给与开局补给
   │  └─ DayHudBridge.cs      可选：反射推 DayHud 通知
   ├─ Order/               委托线
   │  ├─ OrderData.cs         委托模板（谁要、要什么、多少钱、前置条件）
   │  ├─ OrderService.cs      状态机 / 存档 / 结算 / 进度事件
   │  ├─ OrderSupply.cs       开局核查与借用工位
   │  ├─ OrderNpcService.cs   接单 / 收货 NPC
   │  └─ OrderPurchase.cs     原料采购（稀有元素兜底渠道）
   └─ UI/                  实验台窗口（周期表 / 合成台 / 图鉴 / 委托）+ 地下实验室面板
```

关键设计取舍：

- **不硬引用别的模组**：DayHud 通知走反射（README 里的推荐接法），没装就静默跳过。
- **不碰原版存档结构**：只用 `ModHelper.SetModData*` 存自己的标记（开局补给发没发过）。
- **扣料原子性**：先整体统计够不够，再逐个扣；中途失败不会出现扣了一半的情况。
- **界面容错**：主菜单没有 `CustomUIManager`、贴图缺失、数据为空都不能崩。

## 5. 界面

F6 打开（模组设置可改键，F9 调试面板也有入口）。四个标签页：

1. **周期表**：18 列 × 7 行，镧系/锕系单独两行；每格是元素贴图 + 符号，点击看详情
   （序数、中英名、分类、相对原子质量、价值、仓库里有多少）。
2. **合成台**：全部化合物按类别列出，显示化学式、元素需求（需要/已有）、工具需求（有/缺）；
   满足条件才亮「合成」，成功后化合物直接进仓库，失败显示原因。
3. **图鉴**：化合物分类筛选 + 详情（描述、配方、价值），方便玩家知道该去凑什么。
4. **委托**：进行中 / 可接取 / 原料采购 / 已结算四块，交付和借用工位都在这一页（见 5.2）。

## 5.1 地下实验室（离店场景）

打烊后离店时，交通界面（TransitUI）里会多出一张「地下实验室」卡片：选中前往会切到一间
有独立背景的旧实验室场景，进场显示与当铺同款的可拖放网格。技术实现走游戏自带的 afterhour 地点系统
（`AfterhourLocation` / `DelegateAfterhourLocation`），不新增 Unity 场景文件：

- **地点注册**：`ModHelper.AddAfterhourLocation(DelegateAfterhourLocation)`，id `chemistry_lab`，
  `usesAfterhourBag=true`、`showsStoreCounters=false`、`isDiscovered/isAvailable` 恒真，
  所以交通界面会自动列出。卡面用模组贴图 `chemistry:lab_card`（`GetSprite` 支持 `modId:key`）。
- **背景舞台**：注册时自带一个 `DontDestroyOnLoad` 的 `SpriteRenderer`（贴图 `Textures/Paper/lab_background.png`），
  `EnsureBackdrop` 按 `Camera.main` 的 orthographicSize/aspect 缩放铺满、对齐相机、sortingOrder=-1000，
  作为 `location.stageObject`；没有背景时退化为 `backgroundKey`。
- **面板**：游戏创建 `afterhour_location` 窗口后回调 `LabPanel.BuildLauncher`，打开操作面板后有实验台、
  检测与提炼、储物格和档案。检测和提炼都不走面板按钮：把「光谱仪」拖到未知样品上扫描，弹出成分报告；把「蒸馏器」拖到已检测样品上，弹出提炼界面按成分提炼单质。判定/回调挂在物品自己的 `GameItemFunc` 委托上（每件物品独立，不抢全局钩子）。
- **场景储物槽**：复用原版 `GameGridInventory`，夜间背包和网格之间可拖放，物品数量与 `LabStorage` 同步。
- **储物格**：`LabStorage` 是逻辑容量格——24 个格子、1 种物品占 1 格、同类叠加到 999，
  接受化学物品、样品和原版废金属；从店里存入时用 `StoreInventory.TryConsume` 扣店库存，
  取回时 `SpawnItem + StoreInventory.TryPlace` 放回店里（放置失败不扣格）；
  内容以 `"itemId:count|..."` 存进 `ModHelper.SetModData(chemistry,"lab_slots")`，随存档保存。
- **实验室合成**：`LabSynthesis` 用储物格库存做 `Check`，扣格子元素、产物优先入格（没格才放回店里），
  任一步失败用 `LabStorage.AddUnits` 回滚已扣元素；「补料」把一份配方缺的元素/工具从店里搬进格子。
- **故事线**：`StoryService` 维护 8 章（首次到访 / 首次合成 / 首次酸 / 首次有机物 / 首次危险品 /
  图鉴 50、150、400 种），解锁时一次性发信用点奖励（80~800）+ DayHud 通知，进度存
  `story_unlocked` / `lab_crafted`；另外注册 3 个实验室离店事件（通风橱异响、旧笔记被动过、公司的人来过）。
  剧情从「地板下的暗门」讲到前任化学家与「深空化学公司」，终章留下 DSC-07 磁卡，直接给阶段四做引子。

## 5.2 委托线（三点六）

把「接生意 → 找料 → 生产 → 交货」做成可追踪、可结算的主线，和八章档案分开记账。

- **模板**（`src/Order/OrderData.cs`）：老锈试单（铁 ×1，260 信用点）→ 铜回收单（铜 ×2，可重复）
  → 试剂补货单（盐酸 12 单位，可重复）→ DSC-07 硝酸合同（24 单位，第 12 天 + 档案 ch5）
  → DSC-07 工业盐合同（8 单位）。前置关系、档案门槛和天数门槛都写在模板里。
- **状态机**（`OrderService`）：每单有独立编号、接单日、期限、已完成步骤和结算记录，
  存 `order_active` / `order_done` / `order_history` / `order_seq` 四个键。
  进度只由业务事件推进：`OnSampleAnalyzed`（看过报告）、`OnCompoundProduced`（制出试剂 / 交付物）、
  `OnElementRecovered`（亲手回收）。教学步骤允许乱序完成，先做后看也认。
- **结算**：先校验教学步骤，再校验店里数量，**先扣库存再发钱**；任一步失败把已扣的材料放回店里，
  订单保持可重试。成功后才清空进行中的委托，所以重复点交付不会双倍发钱。
- **避免开局卡死**（`OrderSupply`）：接单时按路线核查光谱仪、制酸器具、空瓶、过滤漏斗、废料和制酸上游原料，
  缺什么就列出来；每个订单编号可以领一次借用工位（工具各 1 件、消耗品按一批用量、样品给批次 01）。
  关闭开局补给的新档同样可用。
- **入口**：F6 实验台的「委托」页、地下实验室面板的「委托」页、F9 独立入口三处都能打开委托记录，
  所以错过当日 NPC 来访不会丢单。接单 / 收货 NPC 挂在 `ModHook.OnGenerateCustomerLate`，每天最多排一次。
- **报告联动**：光谱仪报告里会多一行「本单要铁：这批含 72%，1 份出一份，走提炼反应台酸浸（盐酸）」，
  直接回答「这批料够不够、缺什么、去哪个工位」。地下实验室「实验指引」页顶部显示当前目标。

## 6. 掉落表接入

从游戏里导出的真实掉落表（`loot_dump.json`，由 NeteaseCassette 的导出功能生成）：

- `dumpingGroundTG`、`dumpingGroundFreshTG`：垃圾场表组，未知样品表按 60 / 40 权重进组，
  化合物表按 25 权重进 `dumpingGroundTG`。
- `toolTable`：原版工具表，每件化学工具加 4 权重（32 件共 128，约占工具掉落的 11%）。
- 表名或权重在游戏更新后失效时，F9 →「化学：掉落表检查」会打印当前接入状态。

**单质直掉已移除（M1）。** 早期版本把 118 种单质按权重 8 直接挂进普通垃圾场，等于把「检测 → 提炼」
整条玩法跳过去了。现在：

- 垃圾场只掉未知样品、化合物和化学工具，不再掉任何单质；
- 每种元素的获取渠道由 `src/Data/ElementSources.cs` 逐条枚举（样品批次 + 水相反应产物 + 原版废金属 + 原料采购），
  元素物品提示里会直接写出「来源」；F9 →「化学：元素渠道盘点」会逐元素打印并标出缺口；
- 稀有元素靠 `OrderPurchase` 的原料采购兜底（价格 = 元素价值 × 4，下限 24，结清首单后解锁），
  所以配方不会因为撤掉直掉而变成死配方。

## 7. 阶段四：化学公司（后续迭代设计）

目标：让「攒元素→合成→卖」升级成「经营一家化学公司」。预留的数据和接口在本轮已铺好
（化合物价值、合成服务、仓库扫描），下一轮按下面顺序加：

1. **营业执照 / 公司等级**：用 `ModHelper.AddNetworkUpgrade` 在网络面板加一条「化学公司」支线，
   花信用点解锁等级 1~5；等级决定能合成的最高化合物档位（普通无机 → 有机 → 危险品）。
2. **产线（自动合成）**：解锁后可以把某个配方挂到「实验台产线」上，每天夜里
   （`ModHook.OnHandlingNightlyServices`）自动扣料、产出一批化合物，产量随等级提高。
   扣料复用 `SynthesisService` 的检查逻辑，避免两套规则。
3. **批量订单**：顾客/夜市出现「要 5 瓶硫酸铜」这类订单，交付指定化合物换高额信用点，
   用 `ModHelper.AddAfterhourEvent` 或顾客生成钩子实现。
4. **员工化学家**：用原版雇员系统扩展，雇员等级影响产线速度/失败率。
5. **危险品与查抄**：TNT、硝酸甘油、叠氮化钠这类合成物默认标记成高价值风险品，
   可以接原版违禁品/查抄机制（`ContrabandHelper`），卖得贵但可能被没收。
6. **公司声望**：合成的化合物种类/数量累计成「公司声望」，解锁稀有元素采购渠道
   （解决后期缺铀/铂这类元素只能靠翻垃圾的问题）。

## 8. 拓展点

- **更多化合物**：往 `data/compounds.csv`（由 `tools/author_compounds.py` 生成） 加行 → 重跑 `gen_compounds.py` + `gen_textures.py`，
  不用改 C#。
- **反应条件**：`CompoundDef` 现在只有 `Tools`，后续可以加温度/催化剂字段，
  `SynthesisService.Check` 里加对应判定。
- **元素用途**：元素现在只是原料，后续可以给部分元素加 ItemFeature（放射性、毒性、磁性）。
- **跨模组**：`ModHelper.Expose/Lookup` 可以把 `SynthesisService` 暴露出去，
  别的模组想做化学相关玩法可以直接调。
