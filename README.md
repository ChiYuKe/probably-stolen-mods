# probably-stolen-mods

《深空当铺》（Probably Stolen）的 mod 源码，自己写着玩，能用就留着。

游戏是 Unity 2021.3 + Mono，自带 mod 加载器，不需要 BepInEx / MelonLoader 那一套。
编出来的东西丢进游戏根目录的 `Mods\` 就能用。

## 目录

- `EnergyBar/` —— 加一根"味道可疑的能量棒"。新开一局会放一根在后柜台，F9 开发面板里也能刷。

## 编译

需要 .NET SDK，6 以上就行。先把 `EnergyBar/EnergyBar.csproj` 里的 `<GameDir>` 改成自己的游戏目录：

```xml
<GameDir>E:\SteamLibrary\steamapps\common\Probably Stolen</GameDir>
```

然后：

```
dotnet build EnergyBar/EnergyBar.csproj -c Release
```

编完会自动把 dll、manifest 和贴图拷到 `Mods\EnergyBar\`。
进游戏，模组菜单里启用，再重启一次才生效。

## 备注

- 现在对应游戏 0.46D（100-Rev13）。游戏更新之后 API 可能会动，挂了再修。
- manifest 里 `<Capabilities>` 是空的，这个 mod 不读写文件也不联网。
- 调试的时候在游戏根目录放个空的 `dev.txt`，F8 看日志，F9 开开发面板。
