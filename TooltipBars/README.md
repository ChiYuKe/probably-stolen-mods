# 提示框实心进度条（TooltipBars）

把物品提示里的 `[========]` 文字进度条换成像素刻度条。阶梯端角、冷灰金属边和暗色凹槽沿用游戏的工业像素风，填充以原来的进度颜色为基准。水量、耐久、电量、机器进度和动物状态等使用 `RichTextBuilder.AddProgressBar` 的提示都适用。

对应游戏 0.46D。

## 干什么的

- 原版 `[====]` 字符隐藏后仍占原来的宽度，像素进度条绘制在同一个位置；
- 条的位置和宽度按 TMP 的字符坐标算（`characterInfo`），多行、多个条（角色同时有血量/饥饿/口渴/士气）都能分别对齐；
- 填充比例按 `[` 到 `]` 之间的 `=` / 空格数量计算，和原版文字条一致；
- 模组设置 UI 分类里可以关掉（默认开），关掉后立刻恢复原版的文字条。

## 目录

```
TooltipBars/
├── manifest.xml
├── src/
│   ├── TooltipBarsMod.cs        入口：配置项
│   ├── TooltipShowPatch.cs      OverlayHandler.ShowTooltip 后挂组件
│   ├── TooltipBarOverlay.cs     识别并定位 TMP 文本里的进度条
│   └── TooltipBarGraphic.cs     绘制像素边框、刻度和填充
└── Localization/                en / zh / zh-Hans / zh-Hant
```

## 编译

```
dotnet build TooltipBars/TooltipBars.csproj -c Release
```

编完自动拷到 `Mods\TooltipBars\`。进游戏 → 模组菜单启用 → 重启。
