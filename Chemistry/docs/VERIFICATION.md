# 运行时验收记录（2026-11-18）

游戏：深空当铺 **0.46D**（VERSION 100-REV13，Unity 2021.3 + Mono）
环境：Windows 11，Steam 版，mod 以本地 mod 方式加载
模组：`chemistry` v0.1，程序集 `Chemistry.dll`，部署目录 `Mods\Chemistry\`

## 结论

**元素 → 工具 → 化学式合成** 三个阶段在真实游戏里跑通：
118 种元素、32 件工具、50 种化合物全部注册成功；开局补给、掉落表接入、
周期表 / 合成台 / 图鉴三个界面、合成扣料与产出入库、物品价值（unitValue）全部实测通过。

## 实测步骤与日志证据

### 1) 启用模组

新装的本地 mod 默认未启用（`modOrder` 里没有、`modTrust` 里也没有记录，游戏按 Unknown 拒绝加载）。
在游戏 **Mods 菜单**里选中「化学实验台」→「启用」→ 批准一次，然后重启游戏（游戏提示「模组更改将在游戏重启后生效」）。

重启后 Player.log：

```
[ModLoader] Loading: Chemistry.dll
[化学实验台]: 化学实验台装好了，游戏版本 0.46D
[化学实验台]: 数据规模：元素 118，工具 32，化合物 50
[ModLoader]   Initialized: 化学实验台 v0.1
[ModLoader] 化学实验台: 0 texture(s), 200 item sprite(s), 0 paper sprite(s) loaded
[ModLoader] audit: chemistry v0.1 source=Local workshop=0 owner=0 running=True trust=Match caps=none
```

### 2) 读档后的注册 / 掉落 / 开局补给

```
[化学实验台]: 物品目录初始化：注册 200 件物品（元素 118，工具 32，化合物 50）
[化学实验台]: 掉落表接入完成：元素 / 化合物 / 化学工具已进入垃圾场掉落
[化学实验台]: 开局补给已放进仓库：26 件（常用元素 + 基础化学工具）
[化学实验台]: 仓库化学物品盘点：16 种、26 单位；
    el_h×4 el_o×3 el_c×3 el_n×2 el_na×2 el_cl×2 el_ca×1 el_fe×1 el_cu×1 el_s×1
    tool_beaker×1 tool_conical_flask×1 tool_test_tube×1 tool_bunsen_burner×1 tool_balance×1 tool_thermometer×1；
    价格抽查 el_h=3 el_c=5 el_o=4 el_fe=12 tool_beaker=60
```

- 价格抽查证明 `unitValue` 写对了：游戏 `GameItem.GetValue` 用 `unitValue` 计价，
  抽查结果和 `data/elements.csv` / `data/tools.csv` 里的 value 完全一致（H=3、C=5、O=4、Fe=12、烧杯=60）。
- 仓库满时会逐件尝试 仓库/后柜/前柜/展示柜/夜间背包，仍放不下就报
  `补给放不进店里：<id> ×n` 并保留「未发放」状态，清理空间后重新读档会补发——
  实测满仓库（138 件）时没有物品凭空消失。

### 3) 周期表界面（F6）

![周期表](screenshots/periodic-table.png)

- 18 列 × 7 行 + 镧系/锕系单独两行，位置由 `ElementDef.Group/Period` 决定。
- 悬停/点击显示：序数、符号、中英名、分类、相对原子质量、价值、仓库拥有数量
  （实测 Ge：序数 32 · 类金属 · 72.63 · 价值 65 · 拥有 0）。
- 每格显示模组贴图（200 张小试剂瓶 / 工具 / 化合物贴图都在游戏里正常加载）。

> 注：本节截图摄于第一版「程序化元素试剂瓶」贴图；之后 118 个元素图标已按用户要求换成
> **单质材质拼图**（切图导入，仍为 32×32 透明背景、
> 占 2×2 格），元素符号改画在图标底部的木质标签牌上。导入规格经过两轮调整：
> 第一版保留半透明抗锯齿边，实机（游戏对 Items 贴图用 **Point 采样**）看是一圈明显毛边；
> 第二版改成覆盖率二值化 + 1px 开闭收边；第三版再加 **32 色量化**去 JPEG 抖动噪点、
> **外轮廓统一 1px 深色描边**，32px 下是干净的像素剪影。alpha 只有 0/255（外加占格标记 64），
> 和程序化贴图规格一致。新版素材验收图见
> [`../tools/element_contact_sheet.png`](../tools/element_contact_sheet.png)，
> 贴图规格/静态验收已通过（`verify.py` TEX-1~TEX-6 全 PASS）。

### 4) 合成台界面 + 实际合成

![合成台](screenshots/synthesis.png)

- 「共 50 种化合物，当前可合成 7 种」，每行显示化学式、分类、价值和
  「原料（需要/已有）」，缺的用红字；工具在店里（仓库/柜台/展示柜）即可。
- 点击水的「合成」后：

```
[化学实验台]: 合成成功：水 (H2O)（消耗 2 H + 1 O）
```

- 界面刷新后水这一行变成 `H 2/2  O 1/2`（H 从 4 扣到 2、O 从 3 扣到 2），
  当前可合成数量从 7 变成 5 —— **扣料和产出成对，产物进了仓库**。
- 合成顺序是「先造产物 → 放进店里 → 确认进包 → 再扣元素」，仓库/柜台都满时会
  直接提示「产物放不进去，材料没扣」，不会出现扣了料没东西的情况。

### 5) 图鉴界面

![图鉴](screenshots/compendium.png)

- 分类筛选 + 50 种化合物列表（名称、化学式、分类、价值），点条目显示描述、配方和工具需求。

### 6) 编译 / 部署 / 数据验收

```
dotnet build ProbablyStolenMods.sln -c Release -p:DeployToMods=false   → 0 warning / 0 error
python Chemistry/tools/verify.py                                       → PASS 68 / FAIL 0 / exit 0
```

部署产物：`Mods\Chemistry\{Chemistry.dll, manifest.xml, Textures\Items\*.png ×200}`。

## 已知限制 / 后续

- 新装模组必须在游戏 Mods 菜单启用并批准一次，之后才会自动加载（游戏本身的安全策略）。
- 存档里如果之后禁用本模组，已获得的化学物品会因为找不到物品定义而变成异常物品——
  和所有加物品的模组一样，卸载前先把化学物品处理掉。
- 104~118 号元素的官方中文名在 CJK 扩展区，个别字体可能缺字形（物品名里同时带元素符号，
  不会认不出是哪个元素）。
- 阶段四「化学公司」还没做，设计见 [`../DESIGN.md`](../DESIGN.md) 第 7 节。

## 复跑方式

1. `dotnet build Chemistry\Chemistry.csproj -c Release`（部署到游戏 Mods\Chemistry）
2. 确认游戏 Mods 菜单里 chemistry 已启用
3. `pwsh -NoProfile -File Chemistry\tools\smoke_test.ps1` 自动启动游戏并抓启动日志
4. `python Chemistry\tools\verify.py` 跑数据/贴图/编译/契约验收

---

# 地下实验室（2026-11-18 实机验证）

场景：离店 afterhour 地点 `chemistry_lab`，通过 F9 调试入口「化学：去地下实验室」直达验证
（正常玩家可以在打烊离店的交通界面里看到「地下实验室」卡片）。

## 1) 注册与事件

Player.log：

```
[化学实验台]: 地下实验室地点已注册（chemistry_lab）
[化学实验台]: 地下实验室故事线已就绪：8 章档案 + 3 个实验室离店事件
[ModLoader] Paper sprite loaded: lab_background (化学实验台)
[ModLoader] Paper sprite loaded: lab_card (化学实验台)
```

- F9「打烊后事件」面板里能看到「地下实验室 / chemistry_lab」一栏，以及
  `chem_lab_hood_knocking`（通风橱异响）、`chem_lab_notes_moved`（旧笔记被动过，9% 概率/天）、
  `chem_lab_company_visit`（公司的人来过，6%/2 天）三个离店事件，tooltip 里中英描述正常。

## 2) 场景与面板

> 注：本节的实机截图摄于第一版「程序化生成的背景」；之后背景和地点徽章已按用户要求换成
> **新的美术素材**（徽章为透明背景 PNG），构图与尺寸一致、已重新部署，
> 静态验收通过。

![地下实验室-实验台](screenshots/lab-bench.png)

- 1920×1080 场景背景铺满（当铺暗门 + 「封」条、试剂货架、烧瓶/冷凝管、地面反光）；
- 面板居中显示（原版 `panelPosition` 是给窄面板设计的，会把 1000×740 面板顶出屏幕右侧；
  本轮加了 `src/UI/LabPanelAnchor.cs` 在窗口出现后连续几帧校正到屏幕中央，复测居中正常）；
- 顶部摘要「容量 x/48 格 · 实验室可合成 N 种 · 档案 X/8」，三个标签页可正常切换。

## 3) 48 格容量格 + 从店里补料

![地下实验室-储物格](screenshots/lab-storage.png)

- 储物格页：8×6 共 48 格，已用/总格数实时显示；「从店里存入」列表列出当铺里的化学物品和数量；
- 实验台点「补料」：H 2 份、O 1 份、烧杯、本生灯从当铺搬进实验室，摘要变成「容量 4/48 格、
  可合成 1 种」，水行变成 H 2/2、O 1/1；同时店里氢 4→2、氧 3→2（扣店库存成对）。

## 4) 实验室合成 + 故事解锁

![地下实验室-合成成功](screenshots/lab-craft.png)
![地下实验室-档案](screenshots/lab-archive.png)

```
[化学实验台]: 实验室档案解锁 ch1《地板下的暗门》，奖励 80 信用点
[化学实验台]: 实验室档案解锁 ch2《旧笔记的第一页》，奖励 120 信用点
[化学实验台]: 合成成功：水 (H2O)（实验室储物格）
```

- 合成后摘要变「容量 3/48 格 · 可合成 0 种 · 档案 2/8」，水行 H 2/0、O 1/0（扣料成对）；
- 储物格里剩 3 格：烧杯、本生灯、水×1（产物优先进实验室格子）；
- 档案页显示进度「到访 1 次 · 已合成 0/50 种 · 危险品 0 种 · 章节 2/8」，ch1/ch2 正文与
  「一次性奖励（已领取）」，ch3 之后显示 ??? + 解锁条件；
- 面板「返回」正常回到当铺柜台，HUD/背包正常。

## 5) 结论

前往 → 背景/面板 → 48 格容量 → 存取补料 → 实验室合成 → 故事解锁奖励 → 返回，**整条链路实机通过**。
`verify.py` 对契约/故事/美术/构建的静态验收同样全 PASS（见 `tools/VERIFY_REPORT.md`）。
运行时唯一修过的问题是面板位置（见第 2 节）。


---

# 元素信息与物品类型修复（2026-11-18 下午）

用户实机截图发现两个问题：① 元素物品提示里出现 `[Translation Error 'type_chemical' in Mechanic]`；
② 元素描述只有一句「第 8 号元素，非金属，相对原子质量 15.999。」，太单薄。

## 1) 物品类型翻译报错

原因：`ItemRegistration.ApplyCommon` 给物品设了自定义类型 `CHEMICAL` / `CHEM_ELEMENT`（拼成 `CHEM_`+kind），
游戏的 `TypeHelper.GetTypeDisplayName` 会拿 `type_` + 小写类型名去 `Mechanic` 本地化表查，查不到就渲染成
`[Translation Error 'type_xxx' in Mechanic]`（来自 `CommonTooltipHelper.CreateItemTypeTooltip`，它遍历
`GameItem.GetGameItemType()` 逐个加 ` [类型名]`）。

修复：改成原版就有的类型名——
元素/化合物用 `CHEMICAL_SUPPLIES`（原版化学品类型），工具用 `TOOL`。
这两个 key 在游戏 `resources.assets` 里存在 `type_chemical_supplies` / `type_tool` 本地化条目
（扫描确认），所以提示里会正常显示本地化后的类型名，不再出现 Translation Error。

## 2) 元素描述详细化（参照 ptable.com 的「性质/系列」）

数据管线：

```
data/sources/periodic_table.json         公开元素数据集（Bowserinator/Periodic-Table-JSON，只取 1~118 号）
        │
data/element_lore.csv                    118 行自写简介（symbol, summary_zh, summary_en）
        │
        ▼  python tools/fill_element_properties.py
data/elements.csv                        21 列：核心 9 列 + 物性 12 列（K→℃，缺失留空）
        │
        ▼  python tools/gen_elements.py
src/Data/ElementTable.cs                 ElementDef 新字段：Phase/MeltC/BoilC/Density/
                                         DensityUnit/Electronegativity/ElectronConfig/Block/
                                         DiscoveredBy/NamedBy/SummaryZh/SummaryEn
```

物品提示框最终只显示三段（用户明确要求去掉数值行，物性数据仍保留在 CSV 和 ElementDef 里备用；简介为扩写版 77~97 字）：

```
第 26 号元素 · 铁（Fe）
过渡金属 · 第 4 周期 第 8 族 · d 区 · 常温：固体
人类用得最多的金属，地核的主要成分，血红蛋白靠它运送氧气。钢、铸铁和锈迹支撑了整个工业文明，火星的红土也是氧化铁，当铺里铁价最实在。铁锈和泥土里的破伤风菌是老搭档，踩到旧钉子要当心。
```

被去掉的渲染行：相对原子质量·电负性 / 熔点·沸点·密度 / 电子排布 / 发现者（数据仍在）。
古老元素（`Ancient Egypt` / `5000 BC` 这类）在数据里归一成空值；镧系/锕系显示「镧系 / 锕系」而不是「第 0 族」。

## 3) 结论

类型报错已消除（不再有自定义 `CHEM_*` 类型）；元素提示改成三段式（身份 / 分类位置 / 扩写简介），
原来那几行数值不再渲染（用户要求），但物性数据仍保留在 elements.csv 与 ElementDef 里。
`verify.py` 已同步扩展元素数据/描述检查（见 `tools/VERIFY_REPORT.md`）。


---

# 遗留店铺 UI 修复（2026-11-18 晚）

用户截图：从当铺直接下到地下实验室后，店里的「展示柜（商品摆放）」「地面库存」面板、
「离开店铺」按钮还画在实验室场景上。

原因：`LabEntranceButton`（店里「前往地下室」按钮）和 F9「去地下实验室」都直接调
`MapUIManager.Travel()`，绕过了原版「离开店铺」流程。原版流程会调用
`EmporiumEntry.OnLeaveStore()`：快照当前打开的店铺面板 → 关闭展示柜/地面/前后柜台/垃圾桶/
自贩机等 PixelWindow → 隐藏店内可交互物与 HUD → 触发 `OnLeavingStoreEarly/Late`。
少了这一步，店铺 UI 不会被关闭，切场景后就留在画面上。

修复：`LabLocation.TravelHere()` 在 `manager.Travel(target)` 之前，若玩家还在店里
（`!IsOutside && !IsInTransit`）先调 `EmporiumEntry.Instance.OnLeaveStore()`；
返回当铺时游戏自己的 `MapUIManager.CloseUI(true)` 会 `RestoreStoreUIAfterReturn()` 把面板原样恢复。

验证：编译 0 warning / 0 error；待用户实机确认「店内开着展示柜/地面面板 → 前往地下室 → 场景干净；
返回当铺后面板恢复」。


---

# 拖拽式成分检测 / 提炼（2026-11-18 深夜）

用户要求：检测和提炼不要面板按钮，要像游戏里的机器一样「把检测器拖到未知样品上」触发，
并弹出 UI 显示这件样品带的成分。

实现：

- **判定/回调挂在物品自己身上**：游戏 `GameItem` 继承 `GameItemFunc`，每件物品有自己的
  `canItemTargetItemFunc / mayItemTargetItemFunc / onItemTargetItemFunc`，所以给「光谱仪」
  （`tool_spectrometer`）和「蒸馏器」（`tool_distiller`）在工具工厂里挂委托即可，不动全局钩子。
- **检测**：光谱仪拖到任意样品上 → 音效 + 日志浮层 + DayHud「光谱仪正在扫描…」→ 0.9 秒后弹出
  「成分检测报告」：样品名/图标、每个成分的元素图标 + 中文名 + 百分比、杂质百分比；未知样品同时被
  改写成同批次 `_detected` 物品（名字加「（已检测）」），实验室储物格数量跟着同步。
- **提炼**：蒸馏器拖到**已检测**样品上 → 弹出「提炼」窗口，按成分列出「耗样品 N 份 · 现有 M 份」，
  点某个成分提炼：优先扣实验室储物格，不够再从当铺仓库扣（产物先放好再扣样品，失败回滚），
  产出对应元素。
- **旧面板按钮全部取消**：实验室「检测与提炼」页现在只显示储物格里的样品进度与拖拽提示。
- **测试入口**：F9 → 特殊 → 模组面板 →「化学：样品测试包（样品+光谱仪+蒸馏器）」，
  一次发 4 件未知样品 + 光谱仪 + 蒸馏器。

编译 0 warning / 0 error；静态验收见 `tools/VERIFY_REPORT.md`。拖拽的实机手感待用户确认。


---

# 实验室储物格容量与网格对齐修复（2026-11-19 凌晨）

用户截图：实验室里的大网格看起来还有很多空格，但把物品拖进去没反应。

原因（两个问题叠在一起）：
1. 场景里的原版网格按 26×12 格显示（最多能塞 78 件 2×2 物品），但逻辑容量只有 24 格（按物品种类算，
   同种叠放）；用户已经塞了 30+ 种，`CanReceive` 判定超容量后**静默拒绝**，所以看起来「有空位却放不进」。
2. 容量拒绝/存档写入失败都没有提示，玩家不知道被 24 格上限卡住；超出的物品种类也没有保存（存档写入
   同样按 24 格校验失败），返回当铺时这些物品会消失。

修复：
- **容量 24 → 48 格**（32 件化学工具 + 样品/材料能放下）；`LabStorage.Capacity = 48`。
- **场景网格与容量完全对齐**：储物格网格改成 8 列 × 6 行物品位（16×12 个格子），物理上最多就是 48 件，
  窗口标题显示「实验室储物格 · 48 格」。
- 面板「储物格」页同步改成 8 列，格子缩小以适配 48 格（8×6）。
- **拒绝有提示**：容量满时 DayHud 提示「实验室储物格满了（x/48 格）：同种物品会叠放，先把用不到的拿回店里」；
  存档写入失败（超出上限）时警告并提示「多出来的物品没有保存」。
- 文档/契约同步：`LAB_CONTRACT.md`、DESIGN、README 的 24 格改 48 格。

---

## v11 扩充（2026-11-19）：化合物 50 → 550

目标：给化学模组补 500 种化学成品，合成仍然严格按真实化学式扣料。

### 数据

- 新增 `tools/author_compounds.py`：生成/校验 `data/compounds.csv`，并整段重写 `data/CATALOG.md` 第 5 节锁定表。
- 前 50 条是 v10 老配方（key / 化学式 / 文案原样保留），后面 500 条按家族生成：
  氧化物 75、氢氧化物 26、酸 50、盐 200、有机物 120、气体 14、其它（矿物/水合物/炸药）15。
- 校验规则（脚本不通过就不写文件）：
  - 化学式能被 `FormulaParser` 同款规则解析，元素符号必须是 118 号元素表里真实存在的；
  - 盐类按阴阳离子电荷配平（例：`Ca3(PO4)2`、`Al2(SO4)3`）；
  - 有机物按不饱和度校验：`DBE = C - (H+卤素)/2 + N/2 + 1 >= 0` 且 `H+卤素-N` 为偶数；
  - key 全局唯一、中文名/英文名不重复、字段里没有英文逗号/引号/竖线/换行；
  - 化学式允许同分异构体重复（乙醇/二甲醚同为 `C2H6O`、葡萄糖/果糖同为 `C6H12O6`），生成器与验收脚本都按「同式多种」放行。
- `gen_compounds.py` 重新生成 `src/Data/CompoundTable.cs`（550 条），并逐条比对 CATALOG 第 5 节与 CSV。
- `gen_textures.py` 重新生成 `Textures/Items/cmp_*.png`（550 张 32×32），再跑 `import_element_art.py` 恢复 118 个元素图标。
- 分页：550 条一次性铺进 ScrollRect 会卡，F6 的合成台/图鉴和地下实验室的实验台都改成分页（每页 24 条，
  底部「上一页 / 第 x/y 页 / 下一页」），逻辑在 `src/UI/ChemPager.cs`。
- 故事线进度阈值同步：图鉴 50 / 150 / 400 种（原来是 10/25/50 全部）。

### 脚本自检输出（可复现）

```
python tools/author_compounds.py     -> 完成：data/compounds.csv 与 CATALOG 第 5 节已更新（550 条）
                                       分类分布：acid=55 base=29 gas=19 organic=136 other=17 oxide=79 salt=215
python tools/gen_compounds.py        -> 自检通过：工具 32 / 化合物 550，key 唯一，key 与化学式同 CATALOG 完全一致
python tools/gen_textures.py         -> 元素 118 / 工具 32 / 化合物 550 / 合计 700；全部 32x32 RGBA
python tools/import_element_art.py   -> Textures\Items 回写 118 个 32x32 RGBA 元素图标
python tools/verify.py --skip-build  -> PASS 134 / FAIL 3
```

剩余 3 个 FAIL（`LAB-15` / `LAB-16` / `SAMPLE-6`）属于同时进行的「废金属提炼反应台 / 储物格 48→144」改动，
不是本次化合物扩充引入的，等那个功能收尾后再跑一次 `verify.py` 即可。

### 游戏内实测（2026-11-19）

- 启动日志：
  `[化学实验台]: 数据规模：元素 118，工具 32，化合物 550`
  `[化学实验台]: 物品目录初始化：注册 750 件物品（元素 118，工具 32，化合物 550，含未知及已检测样品）`
  贴图加载：`707 item sprite(s)`（700 数据物品 + 6 样本/检测图标 + 1 提炼反应台）。
- F6 → 周期表：标题行正常显示「元素 118 · 工具 32 · 化合物 550」。
- F6 → 合成台：底部分页「第 1/23 页 · 共 550 种」，点「下一页」翻到第 2 页（葡萄糖/蔗糖/阿司匹林…）；
  点「合成」合成水成功，日志 `合成成功：水 (H2O)（消耗 2 H + 1 O）`，原料 H 4→2、O 3→2，
  证明扩充后的配方仍然严格按 `FormulaParser` 解析的化学式扣料。
- F6 → 图鉴：分页「第 2/23 页」，详情区显示配方「H X 2 O X 1」和工具需求。
- 地下实验室 → 实验台：分页「第 1/23 页 · 共 550 种」，翻页后第 2 页正常；
  行内「需要/已有」按化学式逐元素显示，储物格联动合成逻辑未变。

截图：`docs/screenshots/synthesis-550.png`、`compendium-550.png`、`lab-bench-550.png`、`synthesis-craft.png`。
