# 提示框实心进度条（TooltipBars）

把物品提示里的 `[========]` 文字进度条换成真正的 UI Image 进度条：底槽 + 填充两块 Image，颜色沿用原来的进度颜色。水量、耐久、电量、机器进度、动物状态这些用同一套 `RichTextBuilder.AddProgressBar` 的地方都会一起变。

对应游戏 0.46D。

## 干什么的

- 不直接改文字，原版 `[====]` 文字还在，只是被上面盖的 Image 条完全挡住；
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
│   └── TooltipBarOverlay.cs     在 TMP 文本上盖 Image 进度条
└── Localization/                en / zh / zh-Hans / zh-Hant
```

## 编译

```
dotnet build TooltipBars/TooltipBars.csproj -c Release
```

编完自动拷到 `Mods\TooltipBars\`。进游戏 → 模组菜单启用 → 重启。
