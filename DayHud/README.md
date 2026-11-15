# 天数圆盘（DayHud）

左上角加一个复古圆形天数面板：第几天、星期几、还有几天该付钱，都挤在这块表盘里。鼠标移上去描边会亮，提示「查看日历」，点一下打开游戏的日历。

对应游戏 0.46D（100-Rev13）。

## 干什么的

- 按 1920x1080 参考分辨率缩放，挂在一个常驻 Canvas 上，mod 自己管创建和销毁，切场景不会丢。
- 层级（`SortingOrder` 1）比游戏基础 HUD 高一点、比弹窗和日历低：平时点得到，开窗的时候会被盖住。
- 天数读 `StoreStation.GetDayCounter()`，只在数字变了的时候刷文字，顺带弹一下。
- 倒计时分「今天付 / 明天付 / N 天后付」三档；抵押状态显示的是还款日，不是租金。
- 悬停时描边高亮、鼠标变手型；提示文字优先用游戏本地化表里的 `inte_calendar_hover`，取不到就用「查看日历」兜底。
- 点击打开日历。当前场景里没有日历界面的话只写一条警告，不会崩。
- 命中范围是椭圆：矩形贴图的透明四角不响应悬停和点击（`DayHudHitArea` 实现 `ICanvasRaycastFilter`），最外圈描边稍微放宽，保证能触发。
- F9 开发面板 → Modding → 「天数圆盘 开/关」，截图或者嫌挡视野的时候用。

## 目录

```
DayHud/
├── manifest.xml          清单（ID: day_hud，能力: 空）
├── src/
│   ├── DayHudMod.cs      入口：建常驻 Canvas、F9 开关
│   ├── DayHudPanel.cs    表盘本体：贴图、三段文字、悬停、点击
│   └── DayHudHitArea.cs  椭圆命中判定
├── Textures/             retro_dial.png（表盘）、retro_dial_mask.png（描边遮罩）
└── Localization/         en / zh / zh-Hans / zh-Hant 四份 csv
```

## 编译

根目录的 `Directory.Build.props` 里配游戏路径，然后：

```
dotnet build DayHud/DayHud.csproj -c Release
```

编完自动拷到 `Mods\DayHud\`（dll + manifest + 贴图 + 语言表）。进游戏 → 模组菜单启用 → 重启。

## 备注

- 文字用 TextMeshPro，字体跟着游戏走。天数、租金、还款这些文案在 `Localization/*.csv` 里，加一种语言就多放一份 csv（键名照现成的抄）。
- 贴图是从 `Textures\` 按文件名加载的，换层级或者改文件名都会加载不到。
- 清单的 `<Capabilities>` 是空的，不读写文件、不联网。
