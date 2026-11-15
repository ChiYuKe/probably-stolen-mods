# 能量棒（EnergyBar）

给店里加一件能卖的东西：一根"味道可疑的能量棒"。

对应游戏 0.46D（100-Rev13）。

## 干什么的

- 物品目录里注册 `suspicious_energy_bar`：基础价值 8，贴图和物品形状都用 mod 自带的 `energy_bar.png`。
- 新开一局、或者第一次读某个存档，往背包里塞一根。送过就记在存档的 mod 数据里（`given`），同一个档不会再送第二根。
- 读档和开新档在游戏里是两条路，所以两个事件都挂了：`OnGameLoadedLate` 和 `OnNewGameStarted`。
- F9 开发面板 → Modding → 「能量棒 +1」，测试的时候直接刷一根进背包。

## 目录

```
EnergyBar/
├── manifest.xml            清单（ID: energy_bar，能力: 空）
├── src/EnergyBarMod.cs     注册物品、送物品、F9 按钮
└── Textures/Items/energy_bar.png
```

## 编译

根目录的 `Directory.Build.props` 里配游戏路径，然后：

```
dotnet build EnergyBar/EnergyBar.csproj -c Release
```

编完自动拷到 `Mods\EnergyBar\`（dll + manifest + 贴图）。进游戏 → 模组菜单启用 → 重启。

## 备注

- 物品是用 `ItemDirectory.CreateEmptyItem` 现造的，不是预制体；名字、描述、价值都写在代码里。
- 送物品走 `ModHelper.SpawnItem` + `MoveItemToInventory`，塞不进去只往日志里写一条警告，不影响读档。
- 清单的 `<Capabilities>` 是空的，不读写文件、不联网；"送过没有"走游戏自己的 mod 数据接口。
