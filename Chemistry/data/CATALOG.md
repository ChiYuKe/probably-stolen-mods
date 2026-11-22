# 化学模组数据契约（CATALOG）

这份文件是所有分工的**唯一约定**。改数据、生成代码、画贴图都以这里为准；
名字、key、化学式、配色一旦写死，不要在别的文件里另起一套。

模组 ID：`chemistry`　程序集：`Chemistry.dll`　游戏：深空当铺 0.46D（Unity 2021.3 + Mono）

---

## 1. 文件分工（谁写哪些文件）

| 模块 | 负责文件 | 不要碰 |
| --- | --- | --- |
| 核心（lead） | `src/ChemistryMod.cs`、`src/Core/**`、`src/Game/**`、`Chemistry.csproj`、`manifest.xml`、本文件、`DESIGN.md`、`README.md` | — |
| 元素数据 | `data/elements.csv`、`src/Data/ElementTable.cs`、`tools/gen_elements.py` | 其它 |
| 工具/化合物数据 | `data/tools.csv`、`data/compounds.csv`、`src/Data/ToolTable.cs`、`src/Data/CompoundTable.cs`、`tools/author_compounds.py`、`tools/gen_compounds.py` | 其它 |
| 贴图 | `Textures/Items/*.png`、`tools/gen_textures.py` | 数据 CSV、C# 源码 |
| 界面 | `src/UI/**` | `src/Core/**`、`src/Game/**`、数据、贴图 |

规则：

- 只改自己负责的文件；**不要重命名**下面已经出现的任何类/字段/方法（别的模块靠它编译）。
- 不要改 `Directory.Build.props`，不要改根目录 `README.md`、`ProbablyStolenMods.sln`（lead 负责）。
- 注释、日志用中文；UI 文案中文优先、英文兜底（写法见 `src/Core/Loc.cs`）。
- 生成的 C# 文件头部写一行 `// 此文件由 tools/xxx.py 从 data/xxx.csv 生成，不要手改。`

### 并行编译（重要）

多个模块会同时改同一个 csproj，`dotnet build` 默认的 `obj/`、`bin/` 会互相抢锁。
各模块编译时**用自己的中间目录**，互不干扰：

```
dotnet build Chemistry\Chemistry.csproj -c Release -p:DeployToMods=false -p:BaseIntermediateOutputPath=obj\<你的名字>\ -p:BaseOutputPath=bin\<你的名字>\
```

编不过时先看错误是不是落在**别人的文件**里：是的话不要改别人的文件，把自己文件里的错误修掉后重试；
只有别人的文件还没写好导致缺类时，可以 `wait_agent` / 稍等再试。

---

## 2. id 与贴图 key 约定

| 类型 | 物品 id | 贴图文件名（= sprite key） |
| --- | --- | --- |
| 元素 | `chem_el_<符号小写>`，如 `chem_el_h`、`chem_el_fe` | `el_<符号小写>.png` → `el_h.png` |
| 工具 | `chem_tool_<key>`，如 `chem_tool_beaker` | `tool_<key>.png` |
| 化合物 | `chem_cmp_<key>`，如 `chem_cmp_water` | `cmp_<key>.png` |

拼法在 `src/Core/ChemistryCatalog.cs` 里已经实现，C# 里一律通过它取，不要手拼。

---

## 3. CSV 格式

全部 UTF-8（不带 BOM 也行），第一行是表头，逗号分隔。**字段里不要出现英文逗号**——
需要分隔的地方用 `|`。值和名字里不要带换行。

### 3.1 `data/elements.csv`（118 行数据，21 列）

核心列（前 9 列，手写/锁定）：

```csv
number,symbol,name_zh,name_en,category,mass,value,group,period
1,H,氢,Hydrogen,nonmetal,1.008,3,1,1
2,He,氦,Helium,noble_gas,4.0026,55,18,1
26,Fe,铁,Iron,transition_metal,55.845,12,8,4
```

物性列（后 12 列，由 `tools/fill_element_properties.py` 自动合并，不要手改）：

```
phase,melt_c,boil_c,density,density_unit,electronegativity,electron_config,block,discovered_by,named_by,summary_zh,summary_en
```

- `phase`：常温状态，`Gas` / `Liquid` / `Solid`，缺数据留空。
- `melt_c` / `boil_c`：熔点 / 沸点（摄氏度，保留 1 位小数），缺失留空（C# 里是 `double.NaN`，描述里整段省略）。
- `density` / `density_unit`：密度和单位（气体 `g/L`、固体/液体 `g/cm3`），缺失留空。
- `electronegativity`：鲍林电负性，缺失留空。
- `electron_config`：电子排布简式，如 `[He] 2s2 2p4`。
- `block`：元素分区 `s` / `p` / `d` / `f`。
- `discovered_by` / `named_by`：发现者 / 命名者，可能为空（古老的元素为空，描述里显示「自古已知」）。
- `summary_zh` / `summary_en`：一两句元素简介（用途/特点），来自 `data/element_lore.csv`。

数据来源与流水线：

1. `data/sources/periodic_table.json` —— 公开数据集
   [Bowserinator/Periodic-Table-JSON](https://github.com/Bowserinator/Periodic-Table-JSON)
   （其数据源自维基百科元素条目，CC BY-SA；本仓库只取 1~118 号元素的数值字段）。
2. `data/element_lore.csv` —— 118 行自写简介（`symbol,summary_zh,summary_en`），中文为主、英文一句。
3. `python tools/fill_element_properties.py` —— 把 1、2 合并进 `data/elements.csv` 的 12 个物性列。
4. `python tools/gen_elements.py` —— 读 21 列生成 `src/Data/ElementTable.cs`。

核心列规则不变：

- `number`：原子序数 1..118，唯一，升序。
- `symbol`：标准元素符号，首字母大写，如 `H`、`He`、`U`；唯一。
- `category`：只能是
  `alkali_metal` `alkaline_earth` `transition_metal` `post_transition` `metalloid`
  `nonmetal` `halogen` `noble_gas` `lanthanide` `actinide`。
- `mass`：相对原子质量，小数用 `.`。
- `value`：基础价值（信用点，整数）。参考区间：常见轻元素 3~15（H/O/C/N/S/Na/Cl/Ca/Mg/Al/Si），
  普通金属 12~40（Fe/Cu/Zn/Pb/Sn…），贵金属 120~220（Au/Ag/Pt/Pd/Rh），
  稀有/放射性 40~260（稀有气体 30~90，U/Pu 200+，Tc/Pm 稀有 180+）。
- `group`：周期表列 1..18；镧系（57~71）和锕系（89~103）填 `0`，界面会把它们单列两行。
- `period`：周期 1..7；镧系填 6，锕系填 7。

标准周期表位置：H(1,1) He(18,1)；Li(1,2) Be(2,2) B(13,2) C(14,2) N(15,2) O(16,2) F(17,2) Ne(18,2)；
Na(1,3) Mg(2,3) Al(13,3) Si(14,3) P(15,3) S(16,3) Cl(17,3) Ar(18,3)；
K(1,4) Ca(2,4) Sc(3,4)…Zn(12,4) Ga(13,4)…Kr(18,4)；
Rb(1,5) Sr(2,5) Y(3,5)…Cd(12,5) In(13,5)…Xe(18,5)；
Cs(1,6) Ba(2,6) La(3,6)…Lu 之后 Hf(4,6)…Rn(18,6)；
Fr(1,7) Ra(2,7) Ac(3,7)…Lr 之后 Rf(4,7)…Og(18,7)。

### 3.2 `data/tools.csv`

```csv
key,name_zh,name_en,category,value,desc_zh,desc_en
beaker,烧杯,Beaker,glassware,60,最基础的玻璃器皿。,The most basic glassware.
```

- `key`：小写英文+下划线，唯一，**必须用第 4 节锁定表里的 key 和顺序**。
- `category`：`glassware` `heating` `measurement` `separation` `safety` `misc`。
- `value`：基础价值，参考锁定表给的区间。
- 描述写法：一两句话，说明它在这个模组里干什么用（会被合成配方引用）。

### 3.3 `data/compounds.csv`

```csv
key,formula,name_zh,name_en,category,value,tools,desc_zh,desc_en,temperature_c
water,H2O,水,Water,other,12,beaker|bunsen_burner,两个氢一个氧，最普通的化合物。,Two hydrogens and one oxygen.,20
```

- `key`：小写英文+下划线，唯一，**必须用第 5 节锁定表里的 key 和化学式**。
- `formula`：真实化学式，支持括号和下标数字：`H2O`、`C9H8O4`、`Ca(OH)2`、`NH4NO3`。
  运行时 `FormulaParser` 会把它解析成「元素 -> 原子数」，合成时就按这个扣料，所以**化学式必须配平**。
  化学式允许同分异构体重复（乙醇和二甲醚都是 `C2H6O`），合成时按化学式扣料、产物按 key 区分。
- `category`：`acid` `base` `salt` `oxide` `organic` `gas` `other`（`water` 归 `other` 没问题）。
- `value`：参考锁定表。简单无机物 12~90，常见有机物 35~180，危险/精细品 180~400。
- `tools`：合成需要的工具 key，用 `|` 分隔；没有工具要求就留空。只能引用第 4 节里的 key。
- `temperature_c`：参考温度（摄氏度），默认 `20`；化合物物品提示会显示该值。
- 描述：一两句话 + 一点当铺味（这些东西是要摆上柜台卖的）。

---

### 3.4 未知样品（`src/Data/SampleTable.cs`，代码内静态表）

垃圾场会掉「未知样品」：一件样品带一批固定成分比例，检测后切换成 `_detected` 版本，
可以按成分提炼出对应单质。

- 物品 id：未知样品 `chem_sample_<批次>`，已检测 `chem_sample_<批次>_detected`；批次 `01`~`15`
  共 15×2=30 个 id；原版 `scrap_metal` 沿用游戏原 id，只额外登记 `chem_sample_scrap_metal_detected`。
- 每件样品 2x2 格、类型 `CHEMICAL_SUPPLIES`，走实验室储物格序列化。
- 贴图 sprite key：`sample_metal` / `sample_circuit` / `sample_slag` / `sample_liquid` /
  `sample_gas` / `sample_analyzer`（analyzer 是「检测与提炼」页图标，不是物品）。
- 掉落表：`chemSampleTable` 注册进 `dumpingGroundTG`（权重 60）和 `dumpingGroundFreshTG`（权重 40）。
- 交互（不做面板按钮）：
  - 「光谱仪」(`tool_spectrometer`) 拖到任意样品上 → 判定/回调挂在物品自己的
    `GameItem.canItemTargetItemFunc / mayItemTargetItemFunc / onItemTargetItemFunc`（`GameItem : GameItemFunc`），
    扫描 0.9 秒后弹出「成分检测报告」窗口；未知样品会被改写成同批次的 `_detected` 物品。
  - 「蒸馏器」(`tool_distiller`) 拖到**已检测**样品上 → 弹出「提炼」窗口，按成分消耗同批样品提炼单质。
  - F9 调试入口「化学：样品测试包」会发 4 件未知样品 + 光谱仪 + 蒸馏器，方便测试。
- 批次成分（元素符号:百分比，剩余为杂质）：金属 01 Fe72/Zn16、02 Al68/Fe20、03 Cu55/Zn32；
  电路板 04 Cu52/Si24/Ag12、05 Si52/Cu28/Au10；矿渣 06 Ca54/O22/Mg14、07 C58/S26、08 Ti51/Mn28/Fe12；
  液体 09 O79/H10、10 Cl53/Na35、11 K52/O22/P16、12 Mg50/Li30/Na9；气体 13 N74/O18、14 H57/C28、15 N42/O26；
  原版废金属 Fe66/Cu20。

## 4. 锁定的工具 key（32 个，顺序也照这个）

| # | key | 中文 | English | category | 参考价值 |
| --- | --- | --- | --- | --- | --- |
| 1 | beaker | 烧杯 | Beaker | glassware | 60 |
| 2 | conical_flask | 锥形瓶 | Conical flask | glassware | 70 |
| 3 | round_flask | 圆底烧瓶 | Round-bottom flask | glassware | 80 |
| 4 | volumetric_flask | 容量瓶 | Volumetric flask | glassware | 90 |
| 5 | test_tube | 试管 | Test tube | glassware | 30 |
| 6 | test_tube_rack | 试管架 | Test tube rack | glassware | 45 |
| 7 | graduated_cylinder | 量筒 | Graduated cylinder | glassware | 65 |
| 8 | pipette | 移液管 | Pipette | glassware | 55 |
| 9 | burette | 滴定管 | Burette | glassware | 120 |
| 10 | petri_dish | 培养皿 | Petri dish | glassware | 35 |
| 11 | glass_rod | 玻璃棒 | Glass rod | glassware | 20 |
| 12 | watch_glass | 表面皿 | Watch glass | glassware | 25 |
| 13 | bunsen_burner | 本生灯 | Bunsen burner | heating | 100 |
| 14 | heating_mantle | 加热套 | Heating mantle | heating | 160 |
| 15 | crucible | 坩埚 | Crucible | heating | 90 |
| 16 | mortar_pestle | 研钵 | Mortar and pestle | misc | 40 |
| 17 | balance | 分析天平 | Analytical balance | measurement | 180 |
| 18 | thermometer | 温度计 | Thermometer | measurement | 50 |
| 19 | ph_meter | pH计 | pH meter | measurement | 150 |
| 20 | spectrometer | 光谱仪 | Spectrometer | measurement | 420 |
| 21 | chromatograph | 色谱仪 | Chromatograph | measurement | 380 |
| 22 | centrifuge | 离心机 | Centrifuge | separation | 300 |
| 23 | distiller | 蒸馏器 | Distiller | separation | 260 |
| 24 | separating_funnel | 分液漏斗 | Separating funnel | separation | 85 |
| 25 | filter_funnel | 漏斗 | Filter funnel | separation | 40 |
| 26 | microscope | 显微镜 | Microscope | measurement | 320 |
| 27 | fume_hood | 通风橱 | Fume hood | safety | 280 |
| 28 | hotplate_stirrer | 加热磁力搅拌器 | Hotplate stirrer | heating | 220 |
| 29 | autoclave | 高压灭菌锅 | Autoclave | misc | 260 |
| 30 | titration_set | 滴定装置 | Titration set | glassware | 200 |
| 31 | goggles | 护目镜 | Safety goggles | safety | 30 |
| 32 | lab_coat | 实验服 | Lab coat | safety | 45 |

## 5. 锁定的化合物（550 个，key / 化学式不能改）

前 50 条是 v10 就有的老化合物（key/化学式/文案保持不变），后面 500 条是扩充包。
化学式允许同分异构体重复（例如乙醇和二甲醚都是 C2H6O），合成时按化学式扣料。

| # | key | 化学式 | 中文 | English | category | tools（合成需要的工具 key） | 参考价值 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | water | H2O | 水 | Water | other | beaker\|bunsen_burner | 12 |
| 2 | hydrogen_peroxide | H2O2 | 过氧化氢 | Hydrogen peroxide | other | beaker\|graduated_cylinder | 20 |
| 3 | carbon_dioxide | CO2 | 二氧化碳 | Carbon dioxide | gas | conical_flask | 14 |
| 4 | carbon_monoxide | CO | 一氧化碳 | Carbon monoxide | gas | conical_flask\|fume_hood | 18 |
| 5 | ammonia | NH3 | 氨 | Ammonia | gas | round_flask\|heating_mantle\|fume_hood | 25 |
| 6 | methane | CH4 | 甲烷 | Methane | gas | round_flask\|fume_hood | 22 |
| 7 | methanol | CH4O | 甲醇 | Methanol | organic | round_flask\|distiller | 30 |
| 8 | ethanol | C2H6O | 乙醇 | Ethanol | organic | round_flask\|distiller | 35 |
| 9 | acetic_acid | C2H4O2 | 乙酸 | Acetic acid | acid | conical_flask\|distiller | 40 |
| 10 | hydrochloric_acid | HCl | 盐酸 | Hydrochloric acid | acid | conical_flask\|fume_hood | 38 |
| 11 | sulfuric_acid | H2SO4 | 硫酸 | Sulfuric acid | acid | conical_flask\|distiller\|fume_hood | 70 |
| 12 | nitric_acid | HNO3 | 硝酸 | Nitric acid | acid | conical_flask\|distiller\|fume_hood | 75 |
| 13 | phosphoric_acid | H3PO4 | 磷酸 | Phosphoric acid | acid | conical_flask | 60 |
| 14 | sodium_hydroxide | NaOH | 氢氧化钠 | Sodium hydroxide | base | beaker | 45 |
| 15 | potassium_hydroxide | KOH | 氢氧化钾 | Potassium hydroxide | base | beaker | 48 |
| 16 | calcium_carbonate | CaCO3 | 碳酸钙 | Calcium carbonate | salt | mortar_pestle | 20 |
| 17 | sodium_chloride | NaCl | 氯化钠 | Sodium chloride | salt | beaker\|distiller | 15 |
| 18 | sodium_bicarbonate | NaHCO3 | 碳酸氢钠 | Sodium bicarbonate | salt | beaker | 25 |
| 19 | sodium_carbonate | Na2CO3 | 碳酸钠 | Sodium carbonate | salt | beaker\|heating_mantle | 30 |
| 20 | copper_sulfate | CuSO4 | 硫酸铜 | Copper sulfate | salt | beaker\|distiller | 55 |
| 21 | iron_oxide | Fe2O3 | 氧化铁 | Iron(III) oxide | oxide | crucible\|bunsen_burner | 35 |
| 22 | aluminum_oxide | Al2O3 | 氧化铝 | Aluminum oxide | oxide | crucible | 30 |
| 23 | silicon_dioxide | SiO2 | 二氧化硅 | Silicon dioxide | oxide | crucible | 18 |
| 24 | ozone | O3 | 臭氧 | Ozone | gas | round_flask\|fume_hood | 28 |
| 25 | glucose | C6H12O6 | 葡萄糖 | Glucose | organic | beaker\|balance | 60 |
| 26 | sucrose | C12H22O11 | 蔗糖 | Sucrose | organic | beaker\|balance | 55 |
| 27 | aspirin | C9H8O4 | 阿司匹林 | Aspirin | organic | conical_flask\|distiller\|balance | 140 |
| 28 | urea | CH4N2O | 尿素 | Urea | organic | beaker\|distiller | 50 |
| 29 | benzene | C6H6 | 苯 | Benzene | organic | round_flask\|distiller\|fume_hood | 80 |
| 30 | toluene | C7H8 | 甲苯 | Toluene | organic | round_flask\|distiller\|fume_hood | 90 |
| 31 | phenol | C6H6O | 苯酚 | Phenol | organic | round_flask\|distiller\|fume_hood | 85 |
| 32 | acetone | C3H6O | 丙酮 | Acetone | organic | round_flask\|distiller | 65 |
| 33 | formaldehyde | CH2O | 甲醛 | Formaldehyde | organic | conical_flask\|fume_hood | 45 |
| 34 | ethylene_glycol | C2H6O2 | 乙二醇 | Ethylene glycol | organic | round_flask\|distiller | 70 |
| 35 | sodium_hypochlorite | NaClO | 次氯酸钠 | Sodium hypochlorite | salt | beaker | 40 |
| 36 | potassium_permanganate | KMnO4 | 高锰酸钾 | Potassium permanganate | salt | beaker\|balance | 110 |
| 37 | silver_nitrate | AgNO3 | 硝酸银 | Silver nitrate | salt | beaker\|distiller | 160 |
| 38 | sodium_azide | NaN3 | 叠氮化钠 | Sodium azide | salt | beaker\|fume_hood | 190 |
| 39 | lithium_carbonate | Li2CO3 | 碳酸锂 | Lithium carbonate | salt | beaker | 120 |
| 40 | titanium_dioxide | TiO2 | 二氧化钛 | Titanium dioxide | oxide | crucible | 95 |
| 41 | caffeine | C8H10N4O2 | 咖啡因 | Caffeine | organic | conical_flask\|distiller | 180 |
| 42 | tnt | C7H5N3O6 | 三硝基甲苯 | TNT | organic | round_flask\|distiller\|fume_hood | 350 |
| 43 | nitroglycerin | C3H5N3O9 | 硝酸甘油 | Nitroglycerin | organic | round_flask\|distiller\|fume_hood | 400 |
| 44 | glycerol | C3H8O3 | 甘油 | Glycerol | organic | round_flask\|distiller | 75 |
| 45 | calcium_hydroxide | Ca(OH)2 | 氢氧化钙 | Calcium hydroxide | base | beaker | 35 |
| 46 | sodium_sulfate | Na2SO4 | 硫酸钠 | Sodium sulfate | salt | beaker\|distiller | 28 |
| 47 | ammonium_nitrate | NH4NO3 | 硝酸铵 | Ammonium nitrate | salt | beaker | 65 |
| 48 | potassium_nitrate | KNO3 | 硝酸钾 | Potassium nitrate | salt | beaker | 58 |
| 49 | calcium_sulfate | CaSO4 | 硫酸钙 | Calcium sulfate | salt | beaker | 22 |
| 50 | magnesium_sulfate | MgSO4 | 硫酸镁 | Magnesium sulfate | salt | beaker\|distiller | 32 |
| 51 | sulfur_dioxide | SO2 | 二氧化硫 | Sulfur dioxide | oxide | crucible\|bunsen_burner | 24 |
| 52 | sulfur_trioxide | SO3 | 三氧化硫 | Sulfur trioxide | oxide | crucible | 26 |
| 53 | dinitrogen_monoxide | N2O | 一氧化二氮 | Dinitrogen monoxide | oxide | crucible\|balance | 24 |
| 54 | nitrogen_monoxide | NO | 一氧化氮 | Nitrogen monoxide | oxide | crucible\|bunsen_burner | 22 |
| 55 | dinitrogen_trioxide | N2O3 | 三氧化二氮 | Dinitrogen trioxide | oxide | crucible | 28 |
| 56 | nitrogen_dioxide | NO2 | 二氧化氮 | Nitrogen dioxide | oxide | crucible\|balance | 24 |
| 57 | dinitrogen_tetroxide | N2O4 | 四氧化二氮 | Dinitrogen tetroxide | oxide | crucible\|bunsen_burner | 30 |
| 58 | dinitrogen_pentoxide | N2O5 | 五氧化二氮 | Dinitrogen pentoxide | oxide | crucible | 32 |
| 59 | dichlorine_monoxide | Cl2O | 一氧化二氯 | Dichlorine monoxide | oxide | crucible\|balance | 24 |
| 60 | chlorine_dioxide | ClO2 | 二氧化氯 | Chlorine dioxide | oxide | crucible\|bunsen_burner | 24 |
| 61 | dichlorine_heptoxide | Cl2O7 | 七氧化二氯 | Dichlorine heptoxide | oxide | crucible | 36 |
| 62 | diiodine_pentoxide | I2O5 | 五氧化二碘 | Diiodine pentoxide | oxide | crucible\|balance | 34 |
| 63 | phosphorus_trioxide | P2O3 | 三氧化二磷 | Phosphorus trioxide | oxide | crucible\|bunsen_burner | 28 |
| 64 | selenium_trioxide | SeO3 | 三氧化硒 | Selenium trioxide | oxide | crucible | 29 |
| 65 | xenon_trioxide | XeO3 | 三氧化氙 | Xenon trioxide | oxide | crucible\|balance | 33 |
| 66 | sodium_oxide | Na2O | 氧化钠 | Sodium oxide | oxide | crucible\|bunsen_burner | 24 |
| 67 | potassium_oxide | K2O | 氧化钾 | Potassium oxide | oxide | crucible | 25 |
| 68 | lithium_oxide | Li2O | 氧化锂 | Lithium oxide | oxide | crucible\|balance | 26 |
| 69 | rubidium_oxide | Rb2O | 氧化铷 | Rubidium oxide | oxide | crucible\|bunsen_burner | 27 |
| 70 | cesium_oxide | Cs2O | 氧化铯 | Cesium oxide | oxide | crucible | 27 |
| 71 | magnesium_oxide | MgO | 氧化镁 | Magnesium oxide | oxide | crucible\|balance | 23 |
| 72 | calcium_oxide | CaO | 氧化钙 | Calcium oxide | oxide | crucible\|bunsen_burner | 22 |
| 73 | strontium_oxide | SrO | 氧化锶 | Strontium oxide | oxide | crucible | 25 |
| 74 | barium_oxide | BaO | 氧化钡 | Barium oxide | oxide | crucible\|balance | 24 |
| 75 | beryllium_oxide | BeO | 氧化铍 | Beryllium oxide | oxide | crucible\|bunsen_burner | 25 |
| 76 | copper_i_oxide | Cu2O | 氧化亚铜 | Copper(I) oxide | oxide | crucible | 26 |
| 77 | silver_oxide | Ag2O | 氧化银 | Silver oxide | oxide | crucible\|balance | 38 |
| 78 | zinc_oxide | ZnO | 氧化锌 | Zinc oxide | oxide | crucible\|bunsen_burner | 23 |
| 79 | cadmium_oxide | CdO | 氧化镉 | Cadmium oxide | oxide | crucible | 25 |
| 80 | mercury_ii_oxide | HgO | 氧化汞 | Mercury(II) oxide | oxide | crucible\|balance | 26 |
| 81 | nickel_ii_oxide | NiO | 氧化镍 | Nickel(II) oxide | oxide | crucible\|bunsen_burner | 25 |
| 82 | cobalt_ii_oxide | CoO | 氧化钴 | Cobalt(II) oxide | oxide | crucible | 25 |
| 83 | manganese_ii_oxide | MnO | 氧化锰 | Manganese(II) oxide | oxide | crucible\|balance | 24 |
| 84 | iron_ii_oxide | FeO | 氧化亚铁 | Iron(II) oxide | oxide | crucible\|bunsen_burner | 23 |
| 85 | lead_ii_oxide | PbO | 氧化铅 | Lead(II) oxide | oxide | crucible | 24 |
| 86 | tin_ii_oxide | SnO | 氧化亚锡 | Tin(II) oxide | oxide | crucible\|balance | 24 |
| 87 | chromium_iii_trioxide | Cr2O3 | 三氧化二铬 | Chromium(III) oxide | oxide | crucible\|bunsen_burner | 30 |
| 88 | zirconium_dioxide | ZrO2 | 二氧化锆 | Zirconium dioxide | oxide | crucible | 28 |
| 89 | hafnium_dioxide | HfO2 | 二氧化铪 | Hafnium dioxide | oxide | crucible\|balance | 32 |
| 90 | vanadium_v_pentaoxide | V2O5 | 五氧化二钒 | Vanadium(V) oxide | oxide | crucible\|bunsen_burner | 35 |
| 91 | niobium_pentaoxide | Nb2O5 | 五氧化二铌 | Niobium pentaoxide | oxide | crucible | 37 |
| 92 | tantalum_pentaoxide | Ta2O5 | 五氧化二钽 | Tantalum pentaoxide | oxide | crucible\|balance | 40 |
| 93 | molybdenum_vi_trioxide | MoO3 | 三氧化钼 | Molybdenum(VI) oxide | oxide | crucible\|bunsen_burner | 30 |
| 94 | tungsten_vi_trioxide | WO3 | 三氧化钨 | Tungsten(VI) oxide | oxide | crucible | 32 |
| 95 | boron_trioxide | B2O3 | 三氧化二硼 | Boron trioxide | oxide | crucible\|balance | 29 |
| 96 | phosphorus_pentaoxide | P2O5 | 五氧化二磷 | Phosphorus pentaoxide | oxide | crucible\|bunsen_burner | 32 |
| 97 | arsenic_trioxide | As2O3 | 三氧化二砷 | Arsenic trioxide | oxide | crucible | 30 |
| 98 | arsenic_pentaoxide | As2O5 | 五氧化二砷 | Arsenic pentaoxide | oxide | crucible\|balance | 34 |
| 99 | antimony_trioxide | Sb2O3 | 三氧化二锑 | Antimony trioxide | oxide | crucible\|bunsen_burner | 31 |
| 100 | antimony_pentaoxide | Sb2O5 | 五氧化二锑 | Antimony pentaoxide | oxide | crucible | 35 |
| 101 | bismuth_trioxide | Bi2O3 | 三氧化二铋 | Bismuth trioxide | oxide | crucible\|balance | 31 |
| 102 | gallium_trioxide | Ga2O3 | 三氧化二镓 | Gallium trioxide | oxide | crucible\|bunsen_burner | 32 |
| 103 | indium_trioxide | In2O3 | 三氧化二铟 | Indium trioxide | oxide | crucible | 33 |
| 104 | germanium_dioxide | GeO2 | 二氧化锗 | Germanium dioxide | oxide | crucible\|balance | 30 |
| 105 | selenium_dioxide | SeO2 | 二氧化硒 | Selenium dioxide | oxide | crucible\|bunsen_burner | 27 |
| 106 | tellurium_dioxide | TeO2 | 二氧化碲 | Tellurium dioxide | oxide | crucible | 29 |
| 107 | lanthanum_trioxide | La2O3 | 三氧化二镧 | Lanthanum trioxide | oxide | crucible\|balance | 33 |
| 108 | cerium_dioxide | CeO2 | 二氧化铈 | Cerium dioxide | oxide | crucible\|bunsen_burner | 28 |
| 109 | neodymium_trioxide | Nd2O3 | 三氧化二钕 | Neodymium trioxide | oxide | crucible | 35 |
| 110 | samarium_trioxide | Sm2O3 | 三氧化二钐 | Samarium trioxide | oxide | crucible\|balance | 36 |
| 111 | europium_trioxide | Eu2O3 | 三氧化二铕 | Europium trioxide | oxide | crucible\|bunsen_burner | 37 |
| 112 | gadolinium_trioxide | Gd2O3 | 三氧化二钆 | Gadolinium trioxide | oxide | crucible | 36 |
| 113 | dysprosium_trioxide | Dy2O3 | 三氧化二镝 | Dysprosium trioxide | oxide | crucible\|balance | 38 |
| 114 | erbium_trioxide | Er2O3 | 三氧化二铒 | Erbium trioxide | oxide | crucible\|bunsen_burner | 37 |
| 115 | ytterbium_trioxide | Yb2O3 | 三氧化二镱 | Ytterbium trioxide | oxide | crucible | 37 |
| 116 | yttrium_trioxide | Y2O3 | 三氧化二钇 | Yttrium trioxide | oxide | crucible\|balance | 32 |
| 117 | scandium_trioxide | Sc2O3 | 三氧化二钪 | Scandium trioxide | oxide | crucible\|bunsen_burner | 31 |
| 118 | thorium_dioxide | ThO2 | 二氧化钍 | Thorium dioxide | oxide | crucible | 32 |
| 119 | uranium_iv_dioxide | UO2 | 二氧化铀 | Uranium(IV) oxide | oxide | crucible\|balance | 44 |
| 120 | uranium_vi_trioxide | UO3 | 三氧化铀 | Uranium(VI) oxide | oxide | crucible\|bunsen_burner | 46 |
| 121 | plutonium_dioxide | PuO2 | 二氧化钚 | Plutonium dioxide | oxide | crucible | 48 |
| 122 | gold_iii_trioxide | Au2O3 | 三氧化二金 | Gold(III) oxide | oxide | crucible\|balance | 50 |
| 123 | platinum_dioxide | PtO2 | 二氧化铂 | Platinum dioxide | oxide | crucible\|bunsen_burner | 45 |
| 124 | palladium_oxide | PdO | 氧化钯 | Palladium oxide | oxide | crucible | 40 |
| 125 | rhodium_trioxide | Rh2O3 | 三氧化二铑 | Rhodium trioxide | oxide | crucible\|balance | 48 |
| 126 | lithium_hydroxide | LiOH | 氢氧化锂 | Lithium hydroxide | base | beaker | 30 |
| 127 | rubidium_hydroxide | RbOH | 氢氧化铷 | Rubidium hydroxide | base | beaker\|balance | 31 |
| 128 | cesium_hydroxide | CsOH | 氢氧化铯 | Cesium hydroxide | base | beaker\|thermometer | 31 |
| 129 | magnesium_hydroxide | Mg(OH)2 | 氢氧化镁 | Magnesium hydroxide | base | beaker | 33 |
| 130 | strontium_hydroxide | Sr(OH)2 | 氢氧化锶 | Strontium hydroxide | base | beaker\|balance | 35 |
| 131 | barium_hydroxide | Ba(OH)2 | 氢氧化钡 | Barium hydroxide | base | beaker\|thermometer | 34 |
| 132 | beryllium_hydroxide | Be(OH)2 | 氢氧化铍 | Beryllium hydroxide | base | beaker | 35 |
| 133 | aluminum_hydroxide | Al(OH)3 | 氢氧化铝 | Aluminum hydroxide | base | beaker\|balance | 37 |
| 134 | iron_ii_hydroxide | Fe(OH)2 | 氢氧化亚铁 | Iron(II) hydroxide | base | beaker\|thermometer | 33 |
| 135 | iron_iii_hydroxide | Fe(OH)3 | 氢氧化铁(III) | Iron(III) hydroxide | base | beaker | 37 |
| 136 | copper_ii_hydroxide | Cu(OH)2 | 氢氧化铜(II) | Copper(II) hydroxide | base | beaker\|balance | 34 |
| 137 | zinc_hydroxide | Zn(OH)2 | 氢氧化锌 | Zinc hydroxide | base | beaker\|thermometer | 33 |
| 138 | lead_ii_hydroxide | Pb(OH)2 | 氢氧化铅(II) | Lead(II) hydroxide | base | beaker | 34 |
| 139 | lead_iv_hydroxide | Pb(OH)4 | 氢氧化铅(IV) | Lead(IV) hydroxide | base | beaker\|balance | 42 |
| 140 | nickel_ii_hydroxide | Ni(OH)2 | 氢氧化镍(II) | Nickel(II) hydroxide | base | beaker\|thermometer | 35 |
| 141 | cobalt_ii_hydroxide | Co(OH)2 | 氢氧化钴(II) | Cobalt(II) hydroxide | base | beaker | 35 |
| 142 | manganese_ii_hydroxide | Mn(OH)2 | 氢氧化锰(II) | Manganese(II) hydroxide | base | beaker\|balance | 34 |
| 143 | cadmium_hydroxide | Cd(OH)2 | 氢氧化镉 | Cadmium hydroxide | base | beaker\|thermometer | 35 |
| 144 | tin_ii_hydroxide | Sn(OH)2 | 氢氧化亚锡 | Tin(II) hydroxide | base | beaker | 34 |
| 145 | tin_iv_hydroxide | Sn(OH)4 | 氢氧化锡(IV) | Tin(IV) hydroxide | base | beaker\|balance | 42 |
| 146 | chromium_iii_hydroxide | Cr(OH)3 | 氢氧化铬(III) | Chromium(III) hydroxide | base | beaker\|thermometer | 38 |
| 147 | bismuth_hydroxide | Bi(OH)3 | 氢氧化铋 | Bismuth hydroxide | base | beaker | 39 |
| 148 | silver_hydroxide | AgOH | 氢氧化银 | Silver hydroxide | base | beaker\|balance | 42 |
| 149 | thallium_i_hydroxide | TlOH | 氢氧化亚铊 | Thallium(I) hydroxide | base | beaker\|thermometer | 32 |
| 150 | zirconium_hydroxide | Zr(OH)4 | 氢氧化锆 | Zirconium hydroxide | base | beaker | 44 |
| 151 | thorium_hydroxide | Th(OH)4 | 氢氧化钍 | Thorium hydroxide | base | beaker\|balance | 48 |
| 152 | hydrofluoric_acid | HF | 氢氟酸 | Hydrofluoric acid | acid | conical_flask\|fume_hood | 58 |
| 153 | hydrobromic_acid | HBr | 氢溴酸 | Hydrobromic acid | acid | conical_flask\|distiller\|fume_hood | 60 |
| 154 | hydroiodic_acid | HI | 氢碘酸 | Hydroiodic acid | acid | conical_flask\|balance | 66 |
| 155 | hydrosulfuric_acid | H2S | 氢硫酸 | Hydrosulfuric acid | acid | conical_flask\|fume_hood | 42 |
| 156 | hydrocyanic_acid | HCN | 氢氰酸 | Hydrocyanic acid | acid | conical_flask\|distiller\|fume_hood | 120 |
| 157 | carbonic_acid | H2CO3 | 碳酸 | Carbonic acid | acid | conical_flask\|balance | 30 |
| 158 | sulfurous_acid | H2SO3 | 亚硫酸 | Sulfurous acid | acid | conical_flask\|fume_hood | 46 |
| 159 | nitrous_acid | HNO2 | 亚硝酸 | Nitrous acid | acid | conical_flask\|distiller\|fume_hood | 52 |
| 160 | hypochlorous_acid | HClO | 次氯酸 | Hypochlorous acid | acid | conical_flask\|balance | 44 |
| 161 | chlorous_acid | HClO2 | 亚氯酸 | Chlorous acid | acid | conical_flask\|fume_hood | 48 |
| 162 | chloric_acid | HClO3 | 氯酸 | Chloric acid | acid | conical_flask\|distiller\|fume_hood | 54 |
| 163 | perchloric_acid | HClO4 | 高氯酸 | Perchloric acid | acid | conical_flask\|balance | 88 |
| 164 | bromic_acid | HBrO3 | 溴酸 | Bromic acid | acid | conical_flask\|fume_hood | 56 |
| 165 | iodic_acid | HIO3 | 碘酸 | Iodic acid | acid | conical_flask\|distiller\|fume_hood | 58 |
| 166 | periodic_acid | HIO4 | 高碘酸 | Periodic acid | acid | conical_flask\|balance | 70 |
| 167 | boric_acid | H3BO3 | 硼酸 | Boric acid | acid | conical_flask\|fume_hood | 40 |
| 168 | metasilicic_acid | H2SiO3 | 硅酸 | Silicic acid | acid | conical_flask\|distiller\|fume_hood | 34 |
| 169 | orthosilicic_acid | H4SiO4 | 原硅酸 | Orthosilicic acid | acid | conical_flask\|balance | 38 |
| 170 | chromic_acid | H2CrO4 | 铬酸 | Chromic acid | acid | conical_flask\|fume_hood | 72 |
| 171 | dichromic_acid | H2Cr2O7 | 重铬酸 | Dichromic acid | acid | conical_flask\|distiller\|fume_hood | 82 |
| 172 | permanganic_acid | HMnO4 | 高锰酸 | Permanganic acid | acid | conical_flask\|balance | 92 |
| 173 | phosphorous_acid | H3PO3 | 亚磷酸 | Phosphorous acid | acid | conical_flask\|fume_hood | 55 |
| 174 | hypophosphorous_acid | H3PO2 | 次磷酸 | Hypophosphorous acid | acid | conical_flask\|distiller\|fume_hood | 60 |
| 175 | pyrophosphoric_acid | H4P2O7 | 焦磷酸 | Pyrophosphoric acid | acid | conical_flask\|balance | 68 |
| 176 | arsenous_acid | H3AsO3 | 亚砷酸 | Arsenous acid | acid | conical_flask\|fume_hood | 78 |
| 177 | arsenic_acid | H3AsO4 | 砷酸 | Arsenic acid | acid | conical_flask\|distiller\|fume_hood | 86 |
| 178 | selenous_acid | H2SeO3 | 亚硒酸 | Selenous acid | acid | conical_flask\|balance | 74 |
| 179 | selenic_acid | H2SeO4 | 硒酸 | Selenic acid | acid | conical_flask\|fume_hood | 84 |
| 180 | tellurous_acid | H2TeO3 | 亚碲酸 | Tellurous acid | acid | conical_flask\|distiller\|fume_hood | 76 |
| 181 | fluorosilicic_acid | H2SiF6 | 氟硅酸 | Fluorosilicic acid | acid | conical_flask\|balance | 62 |
| 182 | tetrafluoroboric_acid | HBF4 | 氟硼酸 | Tetrafluoroboric acid | acid | conical_flask\|fume_hood | 80 |
| 183 | thiocyanic_acid | HSCN | 硫氰酸 | Thiocyanic acid | acid | conical_flask\|distiller\|fume_hood | 58 |
| 184 | formic_acid | CH2O2 | 甲酸 | Formic acid | acid | conical_flask\|balance | 40 |
| 185 | propionic_acid | C3H6O2 | 丙酸 | Propionic acid | acid | conical_flask\|fume_hood | 48 |
| 186 | butyric_acid | C4H8O2 | 丁酸 | Butyric acid | acid | conical_flask\|distiller\|fume_hood | 52 |
| 187 | valeric_acid | C5H10O2 | 戊酸 | Valeric acid | acid | conical_flask\|balance | 56 |
| 188 | caproic_acid | C6H12O2 | 己酸 | Caproic acid | acid | conical_flask\|fume_hood | 60 |
| 189 | oxalic_acid | H2C2O4 | 草酸 | Oxalic acid | acid | conical_flask\|distiller\|fume_hood | 46 |
| 190 | malonic_acid | C3H4O4 | 丙二酸 | Malonic acid | acid | conical_flask\|balance | 76 |
| 191 | succinic_acid | C4H6O4 | 丁二酸 | Succinic acid | acid | conical_flask\|fume_hood | 66 |
| 192 | glutaric_acid | C5H8O4 | 戊二酸 | Glutaric acid | acid | conical_flask\|distiller\|fume_hood | 72 |
| 193 | adipic_acid | C6H10O4 | 己二酸 | Adipic acid | acid | conical_flask\|balance | 78 |
| 194 | lactic_acid | C3H6O3 | 乳酸 | Lactic acid | acid | conical_flask\|fume_hood | 54 |
| 195 | malic_acid | C4H6O5 | 苹果酸 | Malic acid | acid | conical_flask\|distiller\|fume_hood | 68 |
| 196 | tartaric_acid | C4H6O6 | 酒石酸 | Tartaric acid | acid | conical_flask\|balance | 74 |
| 197 | citric_acid | C6H8O7 | 柠檬酸 | Citric acid | acid | conical_flask\|fume_hood | 70 |
| 198 | benzoic_acid | C7H6O2 | 苯甲酸 | Benzoic acid | acid | conical_flask\|distiller\|fume_hood | 64 |
| 199 | salicylic_acid | C7H6O3 | 水杨酸 | Salicylic acid | acid | conical_flask\|balance | 82 |
| 200 | gallic_acid | C7H6O5 | 没食子酸 | Gallic acid | acid | conical_flask\|fume_hood | 90 |
| 201 | uric_acid | C5H4N4O3 | 尿酸 | Uric acid | acid | conical_flask\|distiller\|fume_hood | 96 |
| 202 | potassium_chloride | KCl | 氯化钾 | Potassium chloride | salt | beaker | 25 |
| 203 | lithium_chloride | LiCl | 氯化锂 | Lithium chloride | salt | beaker\|distiller | 26 |
| 204 | calcium_chloride | CaCl2 | 氯化钙 | Calcium chloride | salt | beaker\|balance | 26 |
| 205 | magnesium_chloride | MgCl2 | 氯化镁 | Magnesium chloride | salt | beaker\|filter_funnel | 27 |
| 206 | aluminum_chloride | AlCl3 | 氯化铝 | Aluminum chloride | salt | beaker | 29 |
| 207 | iron_ii_chloride | FeCl2 | 氯化亚铁 | Iron(II) chloride | salt | beaker\|distiller | 27 |
| 208 | iron_iii_chloride | FeCl3 | 氯化铁 | Iron(III) chloride | salt | beaker\|balance | 29 |
| 209 | copper_ii_chloride | CuCl2 | 氯化铜 | Copper(II) chloride | salt | beaker\|filter_funnel | 28 |
| 210 | zinc_chloride | ZnCl2 | 氯化锌 | Zinc chloride | salt | beaker | 27 |
| 211 | silver_chloride | AgCl | 氯化银 | Silver chloride | salt | beaker\|distiller | 38 |
| 212 | barium_chloride | BaCl2 | 氯化钡 | Barium chloride | salt | beaker\|balance | 28 |
| 213 | ammonium_chloride | NH4Cl | 氯化铵 | Ammonium chloride | salt | beaker\|filter_funnel | 32 |
| 214 | lead_ii_chloride | PbCl2 | 氯化铅 | Lead(II) chloride | salt | beaker | 28 |
| 215 | nickel_ii_chloride | NiCl2 | 氯化镍 | Nickel(II) chloride | salt | beaker\|distiller | 29 |
| 216 | cobalt_ii_chloride | CoCl2 | 氯化钴 | Cobalt(II) chloride | salt | beaker\|balance | 29 |
| 217 | sodium_bromide | NaBr | 溴化钠 | Sodium bromide | salt | beaker\|filter_funnel | 29 |
| 218 | potassium_bromide | KBr | 溴化钾 | Potassium bromide | salt | beaker | 29 |
| 219 | calcium_bromide | CaBr2 | 溴化钙 | Calcium bromide | salt | beaker\|distiller | 31 |
| 220 | magnesium_bromide | MgBr2 | 溴化镁 | Magnesium bromide | salt | beaker\|balance | 31 |
| 221 | aluminum_bromide | AlBr3 | 溴化铝 | Aluminum bromide | salt | beaker\|filter_funnel | 33 |
| 222 | iron_ii_bromide | FeBr2 | 溴化亚铁 | Iron(II) bromide | salt | beaker | 31 |
| 223 | iron_iii_bromide | FeBr3 | 溴化铁 | Iron(III) bromide | salt | beaker\|distiller | 33 |
| 224 | copper_ii_bromide | CuBr2 | 溴化铜 | Copper(II) bromide | salt | beaker\|balance | 31 |
| 225 | zinc_bromide | ZnBr2 | 溴化锌 | Zinc bromide | salt | beaker\|filter_funnel | 31 |
| 226 | silver_bromide | AgBr | 溴化银 | Silver bromide | salt | beaker | 40 |
| 227 | ammonium_bromide | NH4Br | 溴化铵 | Ammonium bromide | salt | beaker\|distiller | 37 |
| 228 | sodium_iodide | NaI | 碘化钠 | Sodium iodide | salt | beaker\|balance | 30 |
| 229 | potassium_iodide | KI | 碘化钾 | Potassium iodide | salt | beaker\|filter_funnel | 30 |
| 230 | calcium_iodide | CaI2 | 碘化钙 | Calcium iodide | salt | beaker | 32 |
| 231 | magnesium_iodide | MgI2 | 碘化镁 | Magnesium iodide | salt | beaker\|distiller | 32 |
| 232 | aluminum_iodide | AlI3 | 碘化铝 | Aluminum iodide | salt | beaker\|balance | 34 |
| 233 | copper_ii_iodide | CuI2 | 碘化铜 | Copper(II) iodide | salt | beaker\|filter_funnel | 32 |
| 234 | zinc_iodide | ZnI2 | 碘化锌 | Zinc iodide | salt | beaker | 32 |
| 235 | silver_iodide | AgI | 碘化银 | Silver iodide | salt | beaker\|distiller | 42 |
| 236 | ammonium_iodide | NH4I | 碘化铵 | Ammonium iodide | salt | beaker\|balance | 38 |
| 237 | lead_ii_iodide | PbI2 | 碘化铅 | Lead(II) iodide | salt | beaker\|filter_funnel | 32 |
| 238 | sodium_fluoride | NaF | 氟化钠 | Sodium fluoride | salt | beaker | 26 |
| 239 | potassium_fluoride | KF | 氟化钾 | Potassium fluoride | salt | beaker\|distiller | 27 |
| 240 | lithium_fluoride | LiF | 氟化锂 | Lithium fluoride | salt | beaker\|balance | 28 |
| 241 | calcium_fluoride | CaF2 | 氟化钙 | Calcium fluoride | salt | beaker\|filter_funnel | 28 |
| 242 | magnesium_fluoride | MgF2 | 氟化镁 | Magnesium fluoride | salt | beaker | 29 |
| 243 | aluminum_fluoride | AlF3 | 氟化铝 | Aluminum fluoride | salt | beaker\|distiller | 31 |
| 244 | iron_iii_fluoride | FeF3 | 氟化铁 | Iron(III) fluoride | salt | beaker\|balance | 31 |
| 245 | copper_ii_fluoride | CuF2 | 氟化铜 | Copper(II) fluoride | salt | beaker\|filter_funnel | 30 |
| 246 | zinc_fluoride | ZnF2 | 氟化锌 | Zinc fluoride | salt | beaker | 29 |
| 247 | barium_fluoride | BaF2 | 氟化钡 | Barium fluoride | salt | beaker\|distiller | 30 |
| 248 | ammonium_fluoride | NH4F | 氟化铵 | Ammonium fluoride | salt | beaker\|balance | 34 |
| 249 | lead_ii_fluoride | PbF2 | 氟化铅 | Lead(II) fluoride | salt | beaker\|filter_funnel | 30 |
| 250 | sodium_sulfide | Na2S | 硫化钠 | Sodium sulfide | salt | beaker | 28 |
| 251 | potassium_sulfide | K2S | 硫化钾 | Potassium sulfide | salt | beaker\|distiller | 29 |
| 252 | calcium_sulfide | CaS | 硫化钙 | Calcium sulfide | salt | beaker\|balance | 26 |
| 253 | magnesium_sulfide | MgS | 硫化镁 | Magnesium sulfide | salt | beaker\|filter_funnel | 27 |
| 254 | zinc_sulfide | ZnS | 硫化锌 | Zinc sulfide | salt | beaker | 27 |
| 255 | iron_ii_sulfide | FeS | 硫化亚铁 | Iron(II) sulfide | salt | beaker\|distiller | 27 |
| 256 | copper_i_sulfide | Cu2S | 硫化亚铜 | Copper(I) sulfide | salt | beaker\|balance | 30 |
| 257 | copper_ii_sulfide | CuS | 硫化铜 | Copper(II) sulfide | salt | beaker\|filter_funnel | 28 |
| 258 | lead_ii_sulfide | PbS | 硫化铅 | Lead(II) sulfide | salt | beaker | 28 |
| 259 | mercury_ii_sulfide | HgS | 硫化汞 | Mercury(II) sulfide | salt | beaker\|distiller | 30 |
| 260 | silver_sulfide | Ag2S | 硫化银 | Silver sulfide | salt | beaker\|balance | 42 |
| 261 | cadmium_sulfide | CdS | 硫化镉 | Cadmium sulfide | salt | beaker\|filter_funnel | 29 |
| 262 | barium_sulfide | BaS | 硫化钡 | Barium sulfide | salt | beaker | 28 |
| 263 | aluminum_sulfide | Al2S3 | 硫化铝 | Aluminum sulfide | salt | beaker\|distiller | 33 |
| 264 | lithium_nitride | Li3N | 氮化锂 | Lithium nitride | salt | beaker\|balance | 36 |
| 265 | magnesium_nitride | Mg3N2 | 氮化镁 | Magnesium nitride | salt | beaker\|filter_funnel | 37 |
| 266 | aluminum_nitride | AlN | 氮化铝 | Aluminum nitride | salt | beaker | 31 |
| 267 | calcium_nitride | Ca3N2 | 氮化钙 | Calcium nitride | salt | beaker\|distiller | 36 |
| 268 | sodium_hydride | NaH | 氢化钠 | Sodium hydride | salt | beaker\|balance | 32 |
| 269 | potassium_hydride | KH | 氢化钾 | Potassium hydride | salt | beaker\|filter_funnel | 33 |
| 270 | lithium_hydride | LiH | 氢化锂 | Lithium hydride | salt | beaker | 34 |
| 271 | calcium_hydride | CaH2 | 氢化钙 | Calcium hydride | salt | beaker\|distiller | 34 |
| 272 | magnesium_hydride | MgH2 | 氢化镁 | Magnesium hydride | salt | beaker\|balance | 35 |
| 273 | calcium_carbide | CaC2 | 碳化钙 | Calcium carbide | salt | beaker\|filter_funnel | 36 |
| 274 | aluminum_carbide | Al2C23 | 碳化铝 | Aluminum carbide | salt | beaker | 81 |
| 275 | silicon_carbide | SiC22 | 碳化硅 | Silicon carbide | salt | beaker\|distiller | 76 |
| 276 | sodium_peroxide | Na2O2 | 过氧化钠 | Sodium peroxide | salt | beaker\|balance | 36 |
| 277 | barium_peroxide | BaO2 | 过氧化钡 | Barium peroxide | salt | beaker\|filter_funnel | 36 |
| 278 | calcium_peroxide | CaO2 | 过氧化钙 | Calcium peroxide | salt | beaker | 34 |
| 279 | sodium_nitrate | NaNO3 | 硝酸钠 | Sodium nitrate | salt | beaker\|distiller | 36 |
| 280 | lithium_nitrate | LiNO3 | 硝酸锂 | Lithium nitrate | salt | beaker\|balance | 38 |
| 281 | calcium_nitrate | Ca(NO3)2 | 硝酸钙 | Calcium nitrate | salt | beaker\|filter_funnel | 44 |
| 282 | magnesium_nitrate | Mg(NO3)2 | 硝酸镁 | Magnesium nitrate | salt | beaker | 45 |
| 283 | aluminum_nitrate | Al(NO3)3 | 硝酸铝 | Aluminum nitrate | salt | beaker\|distiller | 53 |
| 284 | iron_iii_nitrate | Fe(NO3)3 | 硝酸铁 | Iron(III) nitrate | salt | beaker\|balance | 53 |
| 285 | copper_ii_nitrate | Cu(NO3)2 | 硝酸铜 | Copper(II) nitrate | salt | beaker\|filter_funnel | 46 |
| 286 | zinc_nitrate | Zn(NO3)2 | 硝酸锌 | Zinc nitrate | salt | beaker | 45 |
| 287 | barium_nitrate | Ba(NO3)2 | 硝酸钡 | Barium nitrate | salt | beaker\|distiller | 46 |
| 288 | lead_ii_nitrate | Pb(NO3)2 | 硝酸铅 | Lead(II) nitrate | salt | beaker\|balance | 46 |
| 289 | nickel_ii_nitrate | Ni(NO3)2 | 硝酸镍 | Nickel(II) nitrate | salt | beaker\|filter_funnel | 47 |
| 290 | cobalt_ii_nitrate | Co(NO3)2 | 硝酸钴 | Cobalt(II) nitrate | salt | beaker | 47 |
| 291 | bismuth_nitrate | Bi(NO3)3 | 硝酸铋 | Bismuth nitrate | salt | beaker\|distiller | 55 |
| 292 | sodium_nitrite | NaNO2 | 亚硝酸钠 | Sodium nitrite | salt | beaker\|balance | 32 |
| 293 | potassium_nitrite | KNO2 | 亚硝酸钾 | Potassium nitrite | salt | beaker\|filter_funnel | 33 |
| 294 | calcium_nitrite | Ca(NO2)2 | 亚硝酸钙 | Calcium nitrite | salt | beaker | 38 |
| 295 | magnesium_nitrite | Mg(NO2)2 | 亚硝酸镁 | Magnesium nitrite | salt | beaker\|distiller | 39 |
| 296 | silver_nitrite | AgNO2 | 亚硝酸银 | Silver nitrite | salt | beaker\|balance | 46 |
| 297 | barium_nitrite | Ba(NO2)2 | 亚硝酸钡 | Barium nitrite | salt | beaker\|filter_funnel | 40 |
| 298 | ammonium_nitrite | NH4NO2 | 亚硝酸铵 | Ammonium nitrite | salt | beaker | 40 |
| 299 | potassium_sulfate | K2SO4 | 硫酸钾 | Potassium sulfate | salt | beaker\|distiller | 39 |
| 300 | lithium_sulfate | Li2SO4 | 硫酸锂 | Lithium sulfate | salt | beaker\|balance | 40 |
| 301 | aluminum_sulfate | Al2(SO4)3 | 硫酸铝 | Aluminum sulfate | salt | beaker\|filter_funnel | 59 |
| 302 | iron_ii_sulfate | FeSO4 | 硫酸亚铁 | Iron(II) sulfate | salt | beaker | 37 |
| 303 | iron_iii_sulfate | Fe2(SO4)3 | 硫酸铁 | Iron(III) sulfate | salt | beaker\|distiller | 59 |
| 304 | zinc_sulfate | ZnSO4 | 硫酸锌 | Zinc sulfate | salt | beaker\|balance | 37 |
| 305 | silver_sulfate | Ag2SO4 | 硫酸银 | Silver sulfate | salt | beaker\|filter_funnel | 52 |
| 306 | barium_sulfate | BaSO4 | 硫酸钡 | Barium sulfate | salt | beaker | 38 |
| 307 | lead_ii_sulfate | PbSO4 | 硫酸铅 | Lead(II) sulfate | salt | beaker\|distiller | 38 |
| 308 | nickel_ii_sulfate | NiSO4 | 硫酸镍 | Nickel(II) sulfate | salt | beaker\|balance | 39 |
| 309 | cobalt_ii_sulfate | CoSO4 | 硫酸钴 | Cobalt(II) sulfate | salt | beaker\|filter_funnel | 39 |
| 310 | ammonium_sulfate | NH42SO4 | 硫酸铵 | Ammonium sulfate | salt | beaker | 120 |
| 311 | tin_ii_sulfate | SnSO4 | 硫酸亚锡 | Tin(II) sulfate | salt | beaker\|distiller | 38 |
| 312 | manganese_ii_sulfate | MnSO4 | 硫酸锰 | Manganese(II) sulfate | salt | beaker\|balance | 38 |
| 313 | sodium_sulfite | Na2SO3 | 亚硫酸钠 | Sodium sulfite | salt | beaker\|filter_funnel | 34 |
| 314 | potassium_sulfite | K2SO3 | 亚硫酸钾 | Potassium sulfite | salt | beaker | 35 |
| 315 | calcium_sulfite | CaSO3 | 亚硫酸钙 | Calcium sulfite | salt | beaker\|distiller | 32 |
| 316 | magnesium_sulfite | MgSO3 | 亚硫酸镁 | Magnesium sulfite | salt | beaker\|balance | 33 |
| 317 | ammonium_sulfite | NH42SO3 | 亚硫酸铵 | Ammonium sulfite | salt | beaker\|filter_funnel | 116 |
| 318 | barium_sulfite | BaSO3 | 亚硫酸钡 | Barium sulfite | salt | beaker | 34 |
| 319 | potassium_carbonate | K2CO3 | 碳酸钾 | Potassium carbonate | salt | beaker\|distiller | 35 |
| 320 | magnesium_carbonate | MgCO3 | 碳酸镁 | Magnesium carbonate | salt | beaker\|balance | 33 |
| 321 | barium_carbonate | BaCO3 | 碳酸钡 | Barium carbonate | salt | beaker\|filter_funnel | 34 |
| 322 | iron_ii_carbonate | FeCO3 | 碳酸亚铁 | Iron(II) carbonate | salt | beaker | 33 |
| 323 | copper_ii_carbonate | CuCO3 | 碳酸铜 | Copper(II) carbonate | salt | beaker\|distiller | 34 |
| 324 | zinc_carbonate | ZnCO3 | 碳酸锌 | Zinc carbonate | salt | beaker\|balance | 33 |
| 325 | silver_carbonate | Ag2CO3 | 碳酸银 | Silver carbonate | salt | beaker\|filter_funnel | 48 |
| 326 | lead_ii_carbonate | PbCO3 | 碳酸铅 | Lead(II) carbonate | salt | beaker | 34 |
| 327 | manganese_ii_carbonate | MnCO3 | 碳酸锰 | Manganese(II) carbonate | salt | beaker\|distiller | 34 |
| 328 | nickel_ii_carbonate | NiCO3 | 碳酸镍 | Nickel(II) carbonate | salt | beaker\|balance | 35 |
| 329 | ammonium_carbonate | NH42CO3 | 碳酸铵 | Ammonium carbonate | salt | beaker\|filter_funnel | 116 |
| 330 | strontium_carbonate | SrCO3 | 碳酸锶 | Strontium carbonate | salt | beaker | 35 |
| 331 | potassium_bicarbonate | KHCO3 | 碳酸氢钾 | Potassium bicarbonate | salt | beaker\|distiller | 37 |
| 332 | lithium_bicarbonate | LiHCO3 | 碳酸氢锂 | Lithium bicarbonate | salt | beaker\|balance | 38 |
| 333 | ammonium_bicarbonate | NH4HCO3 | 碳酸氢铵 | Ammonium bicarbonate | salt | beaker\|filter_funnel | 44 |
| 334 | cesium_bicarbonate | CsHCO3 | 碳酸氢铯 | Cesium bicarbonate | salt | beaker | 39 |
| 335 | sodium_phosphate | Na3PO4 | 磷酸钠 | Sodium phosphate | salt | beaker\|distiller | 44 |
| 336 | potassium_phosphate | K3PO4 | 磷酸钾 | Potassium phosphate | salt | beaker\|balance | 45 |
| 337 | calcium_phosphate | Ca3(PO4)2 | 磷酸钙 | Calcium phosphate | salt | beaker\|filter_funnel | 54 |
| 338 | magnesium_phosphate | Mg3(PO4)2 | 磷酸镁 | Magnesium phosphate | salt | beaker | 55 |
| 339 | aluminum_phosphate | AlPO4 | 磷酸铝 | Aluminum phosphate | salt | beaker\|distiller | 41 |
| 340 | iron_iii_phosphate | FePO4 | 磷酸铁 | Iron(III) phosphate | salt | beaker\|balance | 41 |
| 341 | copper_ii_phosphate | Cu3(PO4)2 | 磷酸铜 | Copper(II) phosphate | salt | beaker\|filter_funnel | 56 |
| 342 | zinc_phosphate | Zn3(PO4)2 | 磷酸锌 | Zinc phosphate | salt | beaker | 55 |
| 343 | silver_phosphate | Ag3PO4 | 磷酸银 | Silver phosphate | salt | beaker\|distiller | 58 |
| 344 | barium_phosphate | Ba3(PO4)2 | 磷酸钡 | Barium phosphate | salt | beaker\|balance | 56 |
| 345 | ammonium_phosphate | NH43PO4 | 磷酸铵 | Ammonium phosphate | salt | beaker\|filter_funnel | 126 |
| 346 | sodium_permanganate | NaMnO4 | 高锰酸钠 | Sodium permanganate | salt | beaker | 74 |
| 347 | calcium_permanganate | Ca(MnO4)2 | 高锰酸钙 | Calcium permanganate | salt | beaker\|distiller | 84 |
| 348 | ammonium_permanganate | NH4MnO4 | 高锰酸铵 | Ammonium permanganate | salt | beaker\|balance | 82 |
| 349 | silver_permanganate | AgMnO4 | 高锰酸银 | Silver permanganate | salt | beaker\|filter_funnel | 86 |
| 350 | sodium_manganate | Na2MnO4 | 锰酸钠 | Sodium manganate | salt | beaker | 66 |
| 351 | potassium_manganate | K2MnO4 | 锰酸钾 | Potassium manganate | salt | beaker\|distiller | 66 |
| 352 | barium_manganate | BaMnO4 | 锰酸钡 | Barium manganate | salt | beaker\|balance | 64 |
| 353 | sodium_dichromate | Na2Cr2O7 | 重铬酸钠 | Sodium dichromate | salt | beaker\|filter_funnel | 74 |
| 354 | potassium_dichromate | K2Cr2O7 | 重铬酸钾 | Potassium dichromate | salt | beaker | 74 |
| 355 | ammonium_dichromate | NH42Cr2O7 | 重铬酸铵 | Ammonium dichromate | salt | beaker\|distiller | 156 |
| 356 | silver_dichromate | Ag2Cr2O7 | 重铬酸银 | Silver dichromate | salt | beaker\|balance | 86 |
| 357 | sodium_chromate | Na2CrO4 | 铬酸钠 | Sodium chromate | salt | beaker\|filter_funnel | 56 |
| 358 | potassium_chromate | K2CrO4 | 铬酸钾 | Potassium chromate | salt | beaker | 56 |
| 359 | barium_chromate | BaCrO4 | 铬酸钡 | Barium chromate | salt | beaker\|distiller | 54 |
| 360 | lead_ii_chromate | PbCrO4 | 铬酸铅 | Lead(II) chromate | salt | beaker\|balance | 54 |
| 361 | silver_chromate | Ag2CrO4 | 铬酸银 | Silver chromate | salt | beaker\|filter_funnel | 68 |
| 362 | calcium_chromate | CaCrO4 | 铬酸钙 | Calcium chromate | salt | beaker | 54 |
| 363 | sodium_perchlorate | NaClO4 | 高氯酸钠 | Sodium perchlorate | salt | beaker\|distiller | 56 |
| 364 | potassium_perchlorate | KClO4 | 高氯酸钾 | Potassium perchlorate | salt | beaker\|balance | 57 |
| 365 | ammonium_perchlorate | NH4ClO4 | 高氯酸铵 | Ammonium perchlorate | salt | beaker\|filter_funnel | 64 |
| 366 | barium_perchlorate | Ba(ClO4)2 | 高氯酸钡 | Barium perchlorate | salt | beaker | 68 |
| 367 | magnesium_perchlorate | Mg(ClO4)2 | 高氯酸镁 | Magnesium perchlorate | salt | beaker\|distiller | 67 |
| 368 | sodium_chlorate | NaClO3 | 氯酸钠 | Sodium chlorate | salt | beaker\|balance | 46 |
| 369 | potassium_chlorate | KClO3 | 氯酸钾 | Potassium chlorate | salt | beaker\|filter_funnel | 47 |
| 370 | barium_chlorate | Ba(ClO3)2 | 氯酸钡 | Barium chlorate | salt | beaker | 56 |
| 371 | potassium_hypochlorite | KClO | 次氯酸钾 | Potassium hypochlorite | salt | beaker\|distiller | 35 |
| 372 | calcium_hypochlorite | Ca(ClO)2 | 次氯酸钙 | Calcium hypochlorite | salt | beaker\|balance | 38 |
| 373 | barium_hypochlorite | Ba(ClO)2 | 次氯酸钡 | Barium hypochlorite | salt | beaker\|filter_funnel | 40 |
| 374 | sodium_bromate | NaBrO3 | 溴酸钠 | Sodium bromate | salt | beaker | 45 |
| 375 | potassium_bromate | KBrO3 | 溴酸钾 | Potassium bromate | salt | beaker\|distiller | 45 |
| 376 | sodium_iodate | NaIO3 | 碘酸钠 | Sodium iodate | salt | beaker\|balance | 44 |
| 377 | potassium_iodate | KIO3 | 碘酸钾 | Potassium iodate | salt | beaker\|filter_funnel | 44 |
| 378 | sodium_acetate | NaC2H3O2 | 乙酸钠 | Sodium acetate | salt | beaker | 44 |
| 379 | potassium_acetate | KC2H3O2 | 乙酸钾 | Potassium acetate | salt | beaker\|distiller | 45 |
| 380 | calcium_acetate | Ca(C2H3O2)2 | 乙酸钙 | Calcium acetate | salt | beaker\|balance | 58 |
| 381 | magnesium_acetate | Mg(C2H3O2)2 | 乙酸镁 | Magnesium acetate | salt | beaker\|filter_funnel | 59 |
| 382 | copper_ii_acetate | Cu(C2H3O2)2 | 乙酸铜 | Copper(II) acetate | salt | beaker | 60 |
| 383 | zinc_acetate | Zn(C2H3O2)2 | 乙酸锌 | Zinc acetate | salt | beaker\|distiller | 59 |
| 384 | lead_ii_acetate | Pb(C2H3O2)2 | 乙酸铅 | Lead(II) acetate | salt | beaker\|balance | 60 |
| 385 | iron_iii_acetate | Fe(C2H3O2)3 | 乙酸铁 | Iron(III) acetate | salt | beaker\|filter_funnel | 73 |
| 386 | ammonium_acetate | NH4C2H3O2 | 乙酸铵 | Ammonium acetate | salt | beaker | 52 |
| 387 | aluminum_acetate | Al(C2H3O2)3 | 乙酸铝 | Aluminum acetate | salt | beaker\|distiller | 73 |
| 388 | sodium_oxalate | Na2C2O4 | 草酸钠 | Sodium oxalate | salt | beaker\|balance | 44 |
| 389 | potassium_oxalate | K2C2O4 | 草酸钾 | Potassium oxalate | salt | beaker\|filter_funnel | 45 |
| 390 | calcium_oxalate | CaC2O4 | 草酸钙 | Calcium oxalate | salt | beaker | 42 |
| 391 | magnesium_oxalate | MgC2O4 | 草酸镁 | Magnesium oxalate | salt | beaker\|distiller | 43 |
| 392 | ammonium_oxalate | NH42C2O4 | 草酸铵 | Ammonium oxalate | salt | beaker\|balance | 126 |
| 393 | iron_ii_oxalate | FeC2O4 | 草酸亚铁 | Iron(II) oxalate | salt | beaker\|filter_funnel | 43 |
| 394 | sodium_cyanide | NaCN | 氰化钠 | Sodium cyanide | salt | beaker | 52 |
| 395 | potassium_cyanide | KCN | 氰化钾 | Potassium cyanide | salt | beaker\|distiller | 53 |
| 396 | copper_i_cyanide | CuCN | 氰化亚铜 | Copper(I) cyanide | salt | beaker\|balance | 54 |
| 397 | zinc_cyanide | Zn(CN)2 | 氰化锌 | Zinc cyanide | salt | beaker\|filter_funnel | 57 |
| 398 | silver_cyanide | AgCN | 氰化银 | Silver cyanide | salt | beaker | 66 |
| 399 | mercury_ii_cyanide | Hg(CN)2 | 氰化汞 | Mercury(II) cyanide | salt | beaker\|distiller | 60 |
| 400 | sodium_thiocyanate | NaSCN | 硫氰酸钠 | Sodium thiocyanate | salt | beaker\|balance | 40 |
| 401 | potassium_thiocyanate | KSCN | 硫氰酸钾 | Potassium thiocyanate | salt | beaker\|filter_funnel | 41 |
| 402 | pentane | C5H12 | 戊烷 | Pentane | organic | round_flask\|distiller | 76 |
| 403 | hexane | C6H14 | 己烷 | Hexane | organic | conical_flask\|fume_hood | 82 |
| 404 | heptane | C7H16 | 庚烷 | Heptane | organic | round_flask\|distiller\|fume_hood | 88 |
| 405 | octane | C8H18 | 辛烷 | Octane | organic | round_flask\|distiller | 94 |
| 406 | nonane | C9H20 | 壬烷 | Nonane | organic | conical_flask\|fume_hood | 100 |
| 407 | decane | C10H22 | 癸烷 | Decane | organic | round_flask\|distiller\|fume_hood | 106 |
| 408 | cyclohexane | C6H12 | 环己烷 | Cyclohexane | organic | round_flask\|distiller | 78 |
| 409 | cyclopentane | C5H10 | 环戊烷 | Cyclopentane | organic | conical_flask\|fume_hood | 72 |
| 410 | methylcyclohexane | C7H14 | 甲基环己烷 | Methylcyclohexane | organic | round_flask\|distiller\|fume_hood | 84 |
| 411 | isooctane | C8H18 | 异辛烷 | Isooctane | organic | round_flask\|distiller | 94 |
| 412 | cyclohexene | C6H10 | 环己烯 | Cyclohexene | organic | conical_flask\|fume_hood | 74 |
| 413 | styrene | C8H8 | 苯乙烯 | Styrene | organic | round_flask\|distiller\|fume_hood | 74 |
| 414 | isoprene | C5H8 | 异戊二烯 | Isoprene | organic | round_flask\|distiller | 68 |
| 415 | vinyl_chloride | C2H3Cl | 氯乙烯 | Vinyl chloride | organic | conical_flask\|fume_hood | 54 |
| 416 | vinyl_acetate | C4H6O2 | 乙酸乙烯酯 | Vinyl acetate | organic | round_flask\|distiller\|fume_hood | 66 |
| 417 | acrylonitrile | C3H3N | 丙烯腈 | Acrylonitrile | organic | round_flask\|distiller | 56 |
| 418 | methyl_methacrylate | C5H8O2 | 甲基丙烯酸甲酯 | Methyl methacrylate | organic | conical_flask\|fume_hood | 72 |
| 419 | xylene | C8H10 | 二甲苯 | Xylene | organic | round_flask\|distiller\|fume_hood | 78 |
| 420 | ethylbenzene | C8H10 | 乙苯 | Ethylbenzene | organic | round_flask\|distiller | 78 |
| 421 | naphthalene | C10H8 | 萘 | Naphthalene | organic | conical_flask\|fume_hood | 78 |
| 422 | anthracene | C14H10 | 蒽 | Anthracene | organic | round_flask\|distiller\|fume_hood | 90 |
| 423 | pyridine | C5H5N | 吡啶 | Pyridine | organic | round_flask\|distiller | 64 |
| 424 | aniline | C6H7N | 苯胺 | Aniline | organic | conical_flask\|fume_hood | 70 |
| 425 | nitrobenzene | C6H5NO2 | 硝基苯 | Nitrobenzene | organic | round_flask\|distiller\|fume_hood | 70 |
| 426 | chlorobenzene | C6H5Cl | 氯苯 | Chlorobenzene | organic | round_flask\|distiller | 66 |
| 427 | cresol | C7H8O | 甲酚 | Cresol | organic | conical_flask\|fume_hood | 74 |
| 428 | catechol | C6H6O2 | 邻苯二酚 | Catechol | organic | round_flask\|distiller\|fume_hood | 70 |
| 429 | hydroquinone | C6H6O2 | 对苯二酚 | Hydroquinone | organic | round_flask\|distiller | 70 |
| 430 | resorcinol | C6H6O2 | 间苯二酚 | Resorcinol | organic | conical_flask\|fume_hood | 70 |
| 431 | benzoquinone | C6H4O2 | 苯醌 | Benzoquinone | organic | round_flask\|distiller\|fume_hood | 66 |
| 432 | phthalic_anhydride | C8H4O3 | 邻苯二甲酸酐 | Phthalic anhydride | organic | round_flask\|distiller | 72 |
| 433 | naphthol | C10H8O | 萘酚 | Naphthol | organic | conical_flask\|fume_hood | 80 |
| 434 | indole | C8H7N | 吲哚 | Indole | organic | round_flask\|distiller\|fume_hood | 74 |
| 435 | quinoline | C9H7N | 喹啉 | Quinoline | organic | round_flask\|distiller | 76 |
| 436 | carbazole | C12H9N | 咔唑 | Carbazole | organic | conical_flask\|fume_hood | 86 |
| 437 | biphenyl | C12H10 | 联苯 | Biphenyl | organic | round_flask\|distiller\|fume_hood | 86 |
| 438 | diphenyl_ether | C12H10O | 二苯醚 | Diphenyl ether | organic | round_flask\|distiller | 88 |
| 439 | propanol | C3H8O | 丙醇 | Propanol | organic | conical_flask\|fume_hood | 66 |
| 440 | isopropanol | C3H8O | 异丙醇 | Isopropanol | organic | round_flask\|distiller\|fume_hood | 66 |
| 441 | butanol | C4H10O | 丁醇 | Butanol | organic | round_flask\|distiller | 72 |
| 442 | isobutanol | C4H10O | 异丁醇 | Isobutanol | organic | conical_flask\|fume_hood | 72 |
| 443 | pentanol | C5H12O | 戊醇 | Pentanol | organic | round_flask\|distiller\|fume_hood | 78 |
| 444 | hexanol | C6H14O | 己醇 | Hexanol | organic | round_flask\|distiller | 84 |
| 445 | octanol | C8H18O | 辛醇 | Octanol | organic | conical_flask\|fume_hood | 96 |
| 446 | benzyl_alcohol | C7H8O | 苯甲醇 | Benzyl alcohol | organic | round_flask\|distiller\|fume_hood | 74 |
| 447 | cyclohexanol | C6H12O | 环己醇 | Cyclohexanol | organic | round_flask\|distiller | 80 |
| 448 | menthol | C10H20O | 薄荷醇 | Menthol | organic | conical_flask\|fume_hood | 104 |
| 449 | dimethyl_ether | C2H6O | 二甲醚 | Dimethyl ether | organic | round_flask\|distiller\|fume_hood | 60 |
| 450 | diethyl_ether | C4H10O | 乙醚 | Diethyl ether | organic | round_flask\|distiller | 72 |
| 451 | anisole | C7H8O | 苯甲醚 | Anisole | organic | conical_flask\|fume_hood | 74 |
| 452 | tetrahydrofuran | C4H8O | 四氢呋喃 | Tetrahydrofuran | organic | round_flask\|distiller\|fume_hood | 68 |
| 453 | dioxane | C4H8O2 | 二氧六环 | Dioxane | organic | round_flask\|distiller | 70 |
| 454 | ethylene_oxide | C2H4O | 环氧乙烷 | Ethylene oxide | organic | conical_flask\|fume_hood | 56 |
| 455 | propylene_oxide | C3H6O | 环氧丙烷 | Propylene oxide | organic | round_flask\|distiller\|fume_hood | 62 |
| 456 | acetaldehyde | C2H4O | 乙醛 | Acetaldehyde | organic | round_flask\|distiller | 56 |
| 457 | propionaldehyde | C3H6O | 丙醛 | Propionaldehyde | organic | conical_flask\|fume_hood | 62 |
| 458 | butyraldehyde | C4H8O | 丁醛 | Butyraldehyde | organic | round_flask\|distiller\|fume_hood | 68 |
| 459 | benzaldehyde | C7H6O | 苯甲醛 | Benzaldehyde | organic | round_flask\|distiller | 70 |
| 460 | cinnamaldehyde | C9H8O | 肉桂醛 | Cinnamaldehyde | organic | conical_flask\|fume_hood | 78 |
| 461 | butanone | C4H8O | 丁酮 | Butanone | organic | round_flask\|distiller\|fume_hood | 68 |
| 462 | cyclohexanone | C6H10O | 环己酮 | Cyclohexanone | organic | round_flask\|distiller | 76 |
| 463 | acetophenone | C8H8O | 苯乙酮 | Acetophenone | organic | conical_flask\|fume_hood | 76 |
| 464 | methyl_isobutyl_ketone | C6H12O | 甲基异丁基酮 | Methyl isobutyl ketone | organic | round_flask\|distiller\|fume_hood | 80 |
| 465 | camphor | C10H16O | 樟脑 | Camphor | organic | round_flask\|distiller | 96 |
| 466 | methyl_acetate | C3H6O2 | 乙酸甲酯 | Methyl acetate | organic | conical_flask\|fume_hood | 64 |
| 467 | ethyl_acetate | C4H8O2 | 乙酸乙酯 | Ethyl acetate | organic | round_flask\|distiller\|fume_hood | 70 |
| 468 | butyl_acetate | C6H12O2 | 乙酸丁酯 | Butyl acetate | organic | round_flask\|distiller | 82 |
| 469 | methyl_formate | C2H4O2 | 甲酸甲酯 | Methyl formate | organic | conical_flask\|fume_hood | 58 |
| 470 | ethyl_formate | C3H6O2 | 甲酸乙酯 | Ethyl formate | organic | round_flask\|distiller\|fume_hood | 64 |
| 471 | dimethyl_phthalate | C10H10O4 | 邻苯二甲酸二甲酯 | Dimethyl phthalate | organic | round_flask\|distiller | 90 |
| 472 | diethyl_phthalate | C12H14O4 | 邻苯二甲酸二乙酯 | Diethyl phthalate | organic | conical_flask\|fume_hood | 102 |
| 473 | methylamine | CH5N | 甲胺 | Methylamine | organic | round_flask\|distiller\|fume_hood | 56 |
| 474 | dimethylamine | C2H7N | 二甲胺 | Dimethylamine | organic | round_flask\|distiller | 62 |
| 475 | trimethylamine | C3H9N | 三甲胺 | Trimethylamine | organic | conical_flask\|fume_hood | 68 |
| 476 | ethylamine | C2H7N | 乙胺 | Ethylamine | organic | round_flask\|distiller\|fume_hood | 62 |
| 477 | acetamide | C2H5NO | 乙酰胺 | Acetamide | organic | round_flask\|distiller | 60 |
| 478 | formamide | CH3NO | 甲酰胺 | Formamide | organic | conical_flask\|fume_hood | 54 |
| 479 | acrylamide | C3H5NO | 丙烯酰胺 | Acrylamide | organic | round_flask\|distiller\|fume_hood | 62 |
| 480 | caprolactam | C6H11NO | 己内酰胺 | Caprolactam | organic | round_flask\|distiller | 80 |
| 481 | thiourea | CH4N2S | 硫脲 | Thiourea | organic | conical_flask\|fume_hood | 58 |
| 482 | dimethylformamide | C3H7NO | 二甲基甲酰胺 | Dimethylformamide | organic | round_flask\|distiller\|fume_hood | 66 |
| 483 | dimethyl_sulfoxide | C2H6OS | 二甲基亚砜 | Dimethyl sulfoxide | organic | round_flask\|distiller | 62 |
| 484 | mercaptoethanol | C2H6OS | 巯基乙醇 | Mercaptoethanol | organic | conical_flask\|fume_hood | 62 |
| 485 | glycine | C2H5NO2 | 甘氨酸 | Glycine | organic | round_flask\|distiller\|fume_hood | 62 |
| 486 | alanine | C3H7NO2 | 丙氨酸 | Alanine | organic | round_flask\|distiller | 68 |
| 487 | valine | C5H11NO2 | 缬氨酸 | Valine | organic | conical_flask\|fume_hood | 80 |
| 488 | leucine | C6H13NO2 | 亮氨酸 | Leucine | organic | round_flask\|distiller\|fume_hood | 86 |
| 489 | isoleucine | C6H13NO2 | 异亮氨酸 | Isoleucine | organic | round_flask\|distiller | 86 |
| 490 | serine | C3H7NO3 | 丝氨酸 | Serine | organic | conical_flask\|fume_hood | 70 |
| 491 | threonine | C4H9NO3 | 苏氨酸 | Threonine | organic | round_flask\|distiller\|fume_hood | 76 |
| 492 | cysteine | C3H7NO2S | 半胱氨酸 | Cysteine | organic | round_flask\|distiller | 70 |
| 493 | methionine | C5H11NO2S | 甲硫氨酸 | Methionine | organic | conical_flask\|fume_hood | 82 |
| 494 | phenylalanine | C9H11NO2 | 苯丙氨酸 | Phenylalanine | organic | round_flask\|distiller\|fume_hood | 88 |
| 495 | tyrosine | C9H11NO3 | 酪氨酸 | Tyrosine | organic | round_flask\|distiller | 90 |
| 496 | tryptophan | C11H12N2O2 | 色氨酸 | Tryptophan | organic | conical_flask\|fume_hood | 96 |
| 497 | proline | C5H9NO2 | 脯氨酸 | Proline | organic | round_flask\|distiller\|fume_hood | 76 |
| 498 | lysine | C6H14N2O2 | 赖氨酸 | Lysine | organic | round_flask\|distiller | 90 |
| 499 | histidine | C6H9N3O2 | 组氨酸 | Histidine | organic | conical_flask\|fume_hood | 82 |
| 500 | arginine | C6H14N4O2 | 精氨酸 | Arginine | organic | round_flask\|distiller\|fume_hood | 94 |
| 501 | glutamic_acid | C5H9NO4 | 谷氨酸 | Glutamic acid | organic | round_flask\|distiller | 80 |
| 502 | aspartic_acid | C4H7NO4 | 天冬氨酸 | Aspartic acid | organic | conical_flask\|fume_hood | 74 |
| 503 | fructose | C6H12O6 | 果糖 | Fructose | organic | round_flask\|distiller\|fume_hood | 90 |
| 504 | galactose | C6H12O6 | 半乳糖 | Galactose | organic | round_flask\|distiller | 90 |
| 505 | mannose | C6H12O6 | 甘露糖 | Mannose | organic | conical_flask\|fume_hood | 90 |
| 506 | xylose | C5H10O5 | 木糖 | Xylose | organic | round_flask\|distiller\|fume_hood | 82 |
| 507 | ribose | C5H10O5 | 核糖 | Ribose | organic | round_flask\|distiller | 82 |
| 508 | lactose | C12H22O11 | 乳糖 | Lactose | organic | conical_flask\|fume_hood | 132 |
| 509 | maltose | C12H22O11 | 麦芽糖 | Maltose | organic | round_flask\|distiller\|fume_hood | 132 |
| 510 | retinol | C20H30O | 视黄醇 | Retinol | organic | round_flask\|distiller | 144 |
| 511 | tocopherol | C29H50O2 | 生育酚 | Tocopherol | organic | conical_flask\|fume_hood | 204 |
| 512 | riboflavin | C17H20N4O6 | 核黄素 | Riboflavin | organic | round_flask\|distiller\|fume_hood | 136 |
| 513 | niacin | C6H5NO2 | 烟酸 | Niacin | organic | round_flask\|distiller | 70 |
| 514 | pyridoxine | C8H11NO3 | 吡哆醇 | Pyridoxine | organic | conical_flask\|fume_hood | 88 |
| 515 | folic_acid | C19H19N7O6 | 叶酸 | Folic acid | organic | round_flask\|distiller\|fume_hood | 144 |
| 516 | paracetamol | C8H9NO2 | 对乙酰氨基酚 | Paracetamol | organic | round_flask\|distiller | 82 |
| 517 | ibuprofen | C13H18O2 | 布洛芬 | Ibuprofen | organic | conical_flask\|fume_hood | 108 |
| 518 | diclofenac | C14H11Cl2NO2 | 双氯芬酸 | Diclofenac | organic | round_flask\|distiller\|fume_hood | 102 |
| 519 | penicillin_g | C16H18N2O4S | 青霉素G | Penicillin G | organic | round_flask\|distiller | 124 |
| 520 | morphine | C17H19NO3 | 吗啡 | Morphine | organic | conical_flask\|fume_hood | 122 |
| 521 | codeine | C18H21NO3 | 可待因 | Codeine | organic | round_flask\|distiller\|fume_hood | 128 |
| 522 | ethane | C2H6 | 乙烷 | Ethane | gas | round_flask\|fume_hood | 32 |
| 523 | propane | C3H8 | 丙烷 | Propane | gas | conical_flask\|fume_hood | 38 |
| 524 | butane | C4H10 | 丁烷 | Butane | gas | round_flask\|fume_hood | 44 |
| 525 | ethene | C2H4 | 乙烯 | Ethene | gas | conical_flask\|fume_hood | 28 |
| 526 | propene | C3H6 | 丙烯 | Propene | gas | round_flask\|fume_hood | 34 |
| 527 | butene | C4H8 | 丁烯 | Butene | gas | conical_flask\|fume_hood | 40 |
| 528 | butadiene | C4H6 | 丁二烯 | Butadiene | gas | round_flask\|fume_hood | 36 |
| 529 | acetylene | C2H2 | 乙炔 | Acetylene | gas | conical_flask\|fume_hood | 24 |
| 530 | propyne | C3H4 | 丙炔 | Propyne | gas | round_flask\|fume_hood | 30 |
| 531 | phosphine | PH3 | 磷化氢 | Phosphine | gas | conical_flask\|fume_hood | 24 |
| 532 | silane | SiH4 | 硅烷 | Silane | gas | round_flask\|fume_hood | 26 |
| 533 | diborane | B2H6 | 乙硼烷 | Diborane | gas | conical_flask\|fume_hood | 33 |
| 534 | sulfur_hexafluoride | SF6 | 六氟化硫 | Sulfur hexafluoride | gas | round_flask\|fume_hood | 30 |
| 535 | nitrogen_trifluoride | NF3 | 三氟化氮 | Nitrogen trifluoride | gas | conical_flask\|fume_hood | 24 |
| 536 | gypsum | CaH4O6S | 石膏 | Gypsum | other | mortar_pestle | 26 |
| 537 | washing_soda | CH20Na2O13 | 洗涤碱 | Washing soda | other | beaker\|balance | 24 |
| 538 | epsomite | H14MgO11S | 泻盐 | Epsomite | other | beaker | 28 |
| 539 | chalcanthite | CuH10O9S | 胆矾 | Chalcanthite | other | beaker\|distiller | 46 |
| 540 | borax | B4H20Na2O17 | 硼砂 | Borax | other | beaker | 38 |
| 541 | potash_alum | AlH24KO20S2 | 明矾 | Potash alum | other | beaker\|balance | 42 |
| 542 | magnetite | Fe3O4 | 磁铁矿 | Magnetite | other | crucible | 40 |
| 543 | pyrite | FeS2 | 黄铁矿 | Pyrite | other | beaker | 36 |
| 544 | realgar | As4S4 | 雄黄 | Realgar | other | crucible | 52 |
| 545 | orpiment | As2S3 | 雌黄 | Orpiment | other | fume_hood | 60 |
| 546 | prussian_blue | C18Fe7N18 | 普鲁士蓝 | Prussian blue | other | beaker | 120 |
| 547 | basic_copper_carbonate | C1Cu2H2O5 | 铜绿 | Basic copper carbonate | other | filter_funnel | 58 |
| 548 | white_lead | C1H2O5Pb2 | 铅白 | White lead | other | beaker | 96 |
| 549 | chrome_yellow | CrO4Pb | 铬黄 | Chrome yellow | other | filter_funnel | 88 |
| 550 | red_lead | O4Pb3 | 铅丹 | Red lead | other | crucible | 74 |

---

## 6. 贴图规格

- 尺寸 **32×32** RGBA PNG，背景透明。游戏背包格 16×16 像素，所以 32×32 = 占 2×2 格。
- 文件名严格等于 sprite key（见第 2 节），全部放在 `Textures/Items/`。
- 像素风：点采样、1 像素深色描边、不要抗锯齿的渐变（PIL 用 `Image.NEAREST` 校验）。
  游戏对 `Textures/Items` 的贴图用 **Point 采样**（`ModLoader.LoadTexturesFromFolder(..., pixelArt: true)`），
  建议使用 0/255 两档 alpha，避免半透明边缘。普通物品由注册代码明确占 2×2 格，提炼反应台使用 4×3 范围内的设备轮廓。
  元素图标由材质拼图缩下来，但同样要二值化 alpha + 1px 收边，规格和程序化图标一致。
- 三套视觉语言：
  - **元素**：单质材质图标（气瓶 / 试剂瓶 / 安瓿 / 矿块 / 坩埚等按元素性质选形），
    右下角木质标签牌写元素符号（单 / 双字符必须认得出来）。由 `tools/import_element_art.py`
    从 8 列拼图切图 + 抠黑底 + 缩放到 32×32，并做 32 色量化 + 外轮廓 1px 深色描边；
    原子序数交给界面文字显示，不再画在图标里。
  - **工具**：按类别画剪影——玻璃器皿=烧杯/锥形瓶/试管，加热=灯焰/加热套，测量=天平/仪表盘，
    分离=漏斗/蒸馏头，安全=护目镜/实验服。不用写字。
  - **化合物**：锥形瓶或试剂瓶剪影 + 分类色 + 底部一行化学式小字（6~7px，必须能认出 `H2O`、`Ca(OH)2` 这种）。
- 分类配色（元素与化合物通用，工具按 category 近似取色）：

| category | 颜色 |
| --- | --- |
| alkali_metal | #E8A33D |
| alkaline_earth | #E8D24A |
| transition_metal | #E86C6C |
| post_transition | #7FB37F |
| metalloid | #4FB3A6 |
| nonmetal | #6FA8DC |
| halogen | #8E7CC3 |
| noble_gas | #C27BA0 |
| lanthanide | #4AC6C6 |
| actinide | #C46A3C |
| acid | #E07070 |
| base | #6FA8DC |
| salt | #C9B27A |
| oxide | #B0B0B8 |
| organic | #8FBF7F |
| gas | #A9D3E0 |
| other | #9AA0A6 |
| glassware | #9CC7DE |
| heating | #E8A33D |
| measurement | #C9B27A |
| separation | #7FB37F |
| safety | #C27BA0 |
| misc | #B0B0B8 |

字体只依赖 Windows 自带字体（`C:\Windows\Fonts\consolab.ttf` / `arialbd.ttf`），不要从网上找字体。

---

## 7. 生成脚本要求

- 全部用 Python 3（解释器：`C:\Users\Administrator\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe`，
  已装 Pillow）。
- 脚本从 `data/*.csv` 读数据，输出 C# 或 PNG；**可重复运行**（先清掉旧输出再写）。
- 生成后自检并在 stdout 打印：行数、生成的 id 数、贴图数、发现问题（重复/缺字段/化学式非法）。
- `gen_elements.py` 输出 `src/Data/ElementTable.cs`：
  ```csharp
  namespace Chemistry.Data
  {
      internal static class ElementTable
      {
          public static readonly ElementDef[] All =
          {
              new ElementDef { Number = 1, Symbol = "H", NameZh = "氢", NameEn = "Hydrogen", Category = "nonmetal", Mass = 1.008, Value = 3, Group = 1, Period = 1 },
              // ...
          };
      }
  }
  ```
- `author_compounds.py` 生成/校验 `data/compounds.csv`（550 条：前 50 条老配方原样保留，后面 500 条
  按氧化物/氢氧化物/酸/盐/有机物/气体/矿物分组生成），并把第 5 节锁定表整段重写；改化合物数据只改这个脚本。
- `gen_compounds.py` 输出 `src/Data/ToolTable.cs` 与 `src/Data/CompoundTable.cs`：
  ```csharp
  new CompoundDef { Key = "water", Formula = "H2O", NameZh = "水", NameEn = "Water", Category = "other", Value = 12, Tools = new[] { "beaker", "bunsen_burner" }, DescZh = "…", DescEn = "…" },
  ```
  字符串里的 `\`、`"`、换行要正确转义。
- `gen_textures.py` 输出 `Textures/Items/tool_*.png` 与 `cmp_*.png`（元素图标是它的程序化兜底版，
  一旦跑了 `import_element_art.py`，元素图标以拼图导入结果为准），并额外输出一张
  `tools/contact_sheet.png`（把所有贴图放大 4 倍拼成一张，方便人眼检查）。
- `import_element_art.py` 输出 `Textures/Items/el_*.png`（从 `tools/art_sources/element_icons_source_1/2.png`
  两张 8 列拼图切出 118 个元素），同时重建 `tools/element_contact_sheet.png` 和 `tools/contact_sheet.png`。

## 8. 验收

- `dotnet build Chemistry\Chemistry.csproj -c Release -p:DeployToMods=false` 通过（0 error）。
- 数据自检：元素 118、工具 32、化合物 550；key/元素符号唯一；化学式能被解析并配平
  （允许同分异构体共用化学式，例如乙醇和二甲醚都是 `C2H6O`）；
  每个物品 id 都能在 `Textures/Items` 找到同名 PNG，且 PNG 是 32×32。
- 贴图肉眼检查：元素标签牌上的符号能认（`tools/element_contact_sheet.png` 4 倍放大图）、
  化合物化学式可读、工具剪影能区分。
