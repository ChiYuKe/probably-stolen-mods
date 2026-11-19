using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

namespace StorageExpansion
{
    public sealed class StorageExpansionMod : IMod
    {
        private Harmony harmony;
        private GameObject host;

        public void Init(ModManifest manifest)
        {
            ExpansionSession.ModId = manifest.ID;
            ExpansionSession.Log = new ModLog(manifest);
        }

        public void OnEnable()
        {
            try
            {
                if (!WindowFramePatch.IsSupported)
                {
                    ExpansionSession.Log.Warning("找不到原版网格布局接口，储物扩容未启用。");
                    return;
                }
                harmony = new Harmony("com.liukeyu.probablystolen.storageexpansion");
                harmony.PatchAll(typeof(StorageExpansionMod).Assembly);
                ModHook.OnGameLoadedNormal += ExpansionSession.Reload;
                ModHook.OnGameLoadedLate += ExpansionSession.RestoreOccupiedCells;
                ModHook.OnNewGameStarted += OnNewGameStarted;

                host = new GameObject("StorageExpansion");
                UnityEngine.Object.DontDestroyOnLoad(host);
                host.AddComponent<ExpansionWatcher>();
                ExpansionSession.Log.Log("储物扩容已启用 / Storage Expansion enabled");
            }
            catch (Exception error)
            {
                OnDisable();
                ExpansionSession.Log.Error("初始化储物扩容失败 / Storage Expansion failed to initialize: " + error);
            }
        }

        private static void OnNewGameStarted(string _) => ExpansionSession.Reload();

        public void OnDisable()
        {
            ModHook.OnGameLoadedNormal -= ExpansionSession.Reload;
            ModHook.OnGameLoadedLate -= ExpansionSession.RestoreOccupiedCells;
            ModHook.OnNewGameStarted -= OnNewGameStarted;
            if (host != null)
            {
                host.SetActive(false);
                UnityEngine.Object.Destroy(host);
                host = null;
            }
            harmony?.UnpatchAll(harmony.Id);
            harmony = null;
            ExpansionSession.Clear();
        }
    }

    internal sealed class ExpansionWatcher : MonoBehaviour
    {
        private void LateUpdate() => ExpansionSession.BindCurrentStore();
    }

    internal static class ExpansionSession
    {
        internal static string ModId;
        internal static ModLog Log;
        internal static readonly Dictionary<GameGridInventory, StorageViewport> Views =
            new Dictionary<GameGridInventory, StorageViewport>();
        internal static readonly ConditionalWeakTable<GameGridInventory, StorageBaseline> Baselines =
            new ConditionalWeakTable<GameGridInventory, StorageBaseline>();

        private static EmporiumEntry currentStore;
        private static GameGridInventory warehouse;

        internal static string Text(string zh, string en) =>
            (LocHelper.GetCurrentLocaleCode() ?? "").StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? zh : en;

        internal static void BindCurrentStore()
        {
            EmporiumEntry store = EmporiumEntry.Instance;
            if (store == currentStore && (store == null || warehouse == store.invElement))
            {
                return;
            }

            Clear();
            if (store == null || store.invElement == null || PlayerStore.Instance == null)
            {
                return;
            }

            currentStore = store;
            warehouse = store.invElement;
            Bind(store.invElement, store.invWindow);
            RestoreLegacyContainers(store);
        }

        private static void Bind(GameGridInventory grid, PixelWindow window)
        {
            if (grid == null || window?.handler == null)
            {
                return;
            }

            StorageViewport view = null;
            try
            {
                view = new StorageViewport(grid, window);
                Views.Add(grid, view);
                view.Reload();
                view.RestoreOccupiedCells();
            }
            catch (Exception error)
            {
                Views.Remove(grid);
                view?.Dispose();
                Log.Warning("无法连接仓库 / Could not attach to the warehouse: " + error);
            }
        }

        internal static void Reload()
        {
            BindCurrentStore();
            foreach (StorageViewport view in Views.Values)
            {
                view.Reload();
            }
            if (currentStore != null)
            {
                RestoreLegacyContainers(currentStore);
            }
        }

        private static void RestoreLegacyContainers(EmporiumEntry store)
        {
            // Preserve rows already paid for in version 0.2 without adding controls to these containers.
            RestoreLegacyRows(store.backInvinvElement, store.backInvWindow, "back_counter", 4);
            RestoreLegacyRows(store.showcaseElement, store.showcaseWindow, "showcase", 7);
            RestoreLegacyRows(store.hiddenElement, store.hiddenWindow, "hidden", 2);
        }

        private static void RestoreLegacyRows(GameGridInventory grid, PixelWindow window, string key, int maxOriginalRows)
        {
            int rows = Mathf.Clamp(ModHelper.GetModDataInt(ModId, key), 0, WarehouseCells.AdditionalRows);
            if (rows == 0 || grid == null || window == null)
            {
                return;
            }
            StorageBaseline baseline = Baselines.GetValue(grid, _ => new StorageBaseline());
            if (!ReferenceEquals(baseline.CurrentShape, grid.inventoryShape))
            {
                baseline.Rows = Math.Min(grid.inventoryShape.height, maxOriginalRows);
            }
            int expectedRows = baseline.Rows + rows;
            if (grid.inventoryShape.height == expectedRows)
            {
                return;
            }
            grid.SetShape(grid.inventoryShape.width, expectedRows);
            baseline.CurrentShape = grid.inventoryShape;
            window.Validate();
        }

        internal static void RestoreOccupiedCells()
        {
            foreach (StorageViewport view in Views.Values)
            {
                view.RestoreOccupiedCells();
            }
        }

        internal static void Clear()
        {
            var previous = new List<StorageViewport>(Views.Values);
            Views.Clear();
            currentStore = null;
            warehouse = null;
            foreach (StorageViewport view in previous)
            {
                view.Dispose();
            }
        }
    }

    internal sealed class StorageBaseline
    {
        internal int Rows;
        internal GridShape CurrentShape;
    }

    [HarmonyPatch(typeof(PixelWindow), nameof(PixelWindow.Validate))]
    internal static class WindowFramePatch
    {
        private static readonly MethodInfo Setter = AccessTools.PropertySetter(typeof(PixelWindow), nameof(PixelWindow.heightPixels));
        private static readonly MethodInfo ShapeSetter = AccessTools.PropertySetter(typeof(GameGridInventory), nameof(GameGridInventory.inventoryShape));
        private static readonly FieldInfo Background = AccessTools.Field(typeof(GameGridInventory), "background");

        internal static bool IsSupported => Setter != null && ShapeSetter != null && Background != null;

        internal static TreeNodeRender GetBackground(GameGridInventory grid) => (TreeNodeRender)Background.GetValue(grid);
        internal static void SetInventoryShape(GameGridInventory grid, GridShape shape) =>
            ShapeSetter.Invoke(grid, new object[] { shape });

        private static void Postfix(PixelWindow __instance, TreeNodeRender ___background, TreeNodeRender ___outsideBorder)
        {
            if (__instance.childElement is GameGridInventory grid &&
                ExpansionSession.Views.TryGetValue(grid, out StorageViewport view) &&
                view.LayoutWindow(__instance, ___background, ___outsideBorder, out int height))
            {
                // Report the viewport height to the shop layout while the inventory retains its full logical size.
                Setter.Invoke(__instance, new object[] { height });
            }
        }
    }

    [HarmonyPatch(typeof(GameGridInventory), nameof(GameGridInventory.SetShape), typeof(int), typeof(int))]
    internal static class GridResizePatch
    {
        private static void Postfix(GameGridInventory __instance, int __1)
        {
            if (ExpansionSession.Views.TryGetValue(__instance, out StorageViewport view))
            {
                view.ApplyOriginalSize(__1);
            }
        }
    }
}
