# 调试面板汉化（DebugPanelChinese）

把 F9 调试面板、模组面板和物品生成器的界面文字换成中文，游戏代码一行不改。

对应游戏 0.46D（100-Rev13）。

## 干什么的

- 调试面板是运行时用代码搭出来的，文字硬编码在控件上，没走游戏的本地化表。这个 mod 在窗口显示的时候按对照表把文字换掉，顺便把字体换成中文字体。
- 覆盖五个窗口：`debug_panel`、`debug_panel_modding`、`debug_panel_marketing`、`debug_panel_story`、`debug_item_spawner`。
- 每 0.2 秒扫一遍可见窗口、0.5 秒扫一遍提示框，所以面板开得晚一点、翻页之后新冒出来的控件也能翻到。
- 只匹配完整字符串（分类标题先剥掉一层 `<b>`），不做全局替换，免得把物品 ID、命令这类文本也改掉。
- 入口只做一件事：挂一个常驻的 `DebugPanelLocalizer`。不用 Harmony，不 patch 游戏方法。

## 目录

```
DebugPanelChinese/
├── manifest.xml                   清单（ID: debug_panel_chinese，能力: 空）
└── src/
    ├── DebugPanelChineseMod.cs    入口：挂常驻 localizer
    ├── DebugPanelLocalizer.cs     扫窗口、换文字、找中文字体、处理 tooltip
    └── TranslationTable.cs        英文 → 中文对照表（按栏目分组）
```

## 编译

根目录的 `Directory.Build.props` 里配游戏路径，然后：

```
dotnet build DebugPanelChinese/DebugPanelChinese.csproj -c Release
```

编完自动拷到 `Mods\DebugPanelChinese\`（dll + manifest）。进游戏 → 模组菜单启用 → 重启。

## 已知限制

- 对照表是手工维护的。游戏更新后调试面板加了新按钮，得照着补一行，没补的会保持英文。
- 只翻文字，不改布局：中文比英文短的地方会留点空，长的地方可能挤。
- **搜索框还是按英文按钮名匹配**，中文界面下搜索要输英文关键词。
