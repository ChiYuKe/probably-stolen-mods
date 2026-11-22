using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Chemistry
{
    /// <summary>原版网格负责拖放。实验室存档继续使用 LabStorage 的物品数量。</summary>
    internal sealed class LabInventoryGrid : MonoBehaviour
    {
        private static LabInventoryGrid active;
        private PixelWindow window;
        private StorageInventory inventory;
        private bool applying;

        internal static LabInventoryGrid OpenGrid => active;
        internal GameInventory StorageInventoryArea => inventory;

        internal bool Contains(GameItem item)
        {
            if (item?.parentInventory == null)
            {
                return false;
            }
            foreach (GameInventory area in Inventories())
            {
                if (item.parentInventory == area)
                {
                    return true;
                }
            }
            return false;
        }

        internal GameItem FindItem(string id)
        {
            foreach (GameInventory area in Inventories())
            {
                GameItem item = area.childItems.Find(candidate => candidate.identifier == id);
                if (item != null)
                {
                    return item;
                }
            }
            return null;
        }

        private IEnumerable<GameInventory> Inventories()
        {
            yield return inventory;
            foreach (GameInventory area in RefiningStation.Inventories)
            {
                yield return area;
            }
            foreach (GameInventory area in AcidStation.Inventories)
            {
                yield return area;
            }
            foreach (GameInventory area in NitricProduction.Inventories)
            {
                yield return area;
            }
            foreach (GameInventory area in IndustrialFactory.Inventories)
            {
                yield return area;
            }
            foreach (GameInventory area in LabEquipmentPanel.Inventories)
            {
                yield return area;
            }
        }

        // 一个 2×2 物品位对应四个原版小格。
        private const int SlotColumns = 18;
        private const int VisibleSlotRows = 8;
        private static int StorageSlotRows => (LabStorage.Capacity + SlotColumns - 1) / SlotColumns;

        private sealed class StorageInventory : GameGridScrollableInventory
        {
            public StorageInventory() : base(SlotColumns * 2, VisibleSlotRows * 2, StorageSlotRows * 2, scrollOverflow: 1f)
            {
                // 普通网格用 Tan，原版滚动网格默认用 Black。只替换储物格底纹。
                RenderHandler renderer = RenderHandler.current;
                foreach (Transform child in handler.rectTransform)
                {
                    var image = child.GetComponent<TreeNodeRender>()?.image;
                    if (image != null && image.sprite != null && image.sprite == renderer.blackSquare)
                    {
                        renderer.SetHighlightColor(image, RenderHandler.BackgroundColor.Tan, RenderHandler.BackgroundType.Grid);
                        break;
                    }
                }

                onSlotAddItemEndFunc += (item, _, _) =>
                {
                    if (item is GameItemElement element)
                    {
                        element.background.eventHandler.mouseScrollFunc.Add(ScrollFromItem);
                    }
                };
                onSlotRemoveItemFunc += (item, _, _) =>
                {
                    if (item is GameItemElement element)
                    {
                        element.background.eventHandler.mouseScrollFunc.Remove(ScrollFromItem);
                    }
                };
            }

            // 组合放置会一次提交多个物品，跳过逐件的实验室容量检查。
            public override bool SupportsGroupPlacement => false;

            private bool ScrollFromItem(PointerEventData eventData)
            {
                // 拿着物品时由原版拖放处理滚轮，保留调整数量或旋转的操作。
                if (ItemMouseDragHandler.current?.IsDraggingItem == true)
                {
                    return false;
                }
                Scroll(eventData.scrollDelta);
                return true;
            }
        }

        private static float lastFullNotifyTime = -10f;
        private static float lastSaveFailNotifyTime = -10f;

        public static void Open()
        {
            if (active != null && active.window?.handler != null)
            {
                return;
            }

            var window = new PixelWindow();
            var view = window.handler.gameObject.AddComponent<LabInventoryGrid>();
            active = view;
            view.window = window;
            try
            {
                view.inventory = new StorageInventory();
                view.inventory.mayInventoryAddItemFunc = (item, _) => view.CanReceive(item);
                window.Attach(view.inventory);
                window.Validate();
                var layouts = IndustrialFactoryData.SplitLayout(LabStorage.LineLayout);
                NitricProduction.Restore(view, layouts.Nitric);
                IndustrialFactory.Restore(view, layouts.Factory);
                view.ApplyStorage();
                window.Show();
                view.Position();
                RefiningStation.InstallFixture();
                AcidStation.InstallFixture();
            }
            catch (Exception e)
            {
                view.DisableGrid(e);
            }
        }

        public static void Close(bool captureChanges)
        {
            if (active == null)
            {
                return;
            }

            LabInventoryGrid view = active;
            try
            {
                if (captureChanges && view.inventory != null)
                {
                    ItemMouseDragHandler.current?.Cancel();
                    view.CaptureInventory();
                }
            }
            finally
            {
                active = null;
                NitricProduction.Close();
                IndustrialFactory.Close();
                AcidStation.Close();
                RefiningStation.Close();
                LabEquipmentPanel.CloseAll();
                view.DestroyGrid();
            }
        }

        public static void StorageSaved()
        {
            if (active == null || active.applying)
            {
                return;
            }

            try
            {
                active.ApplyStorage();
            }
            catch (Exception e)
            {
                active.DisableGrid(e);
            }
        }

        public static void CaptureAfterDrop()
        {
            if (active != null && ItemMouseDragHandler.current?.IsDraggingItem != true)
            {
                active.CaptureInventory();
            }
        }

        private void LateUpdate()
        {
            if (MapUIManager.Instance == null || MapUIManager.Instance.Current != LabLocation.Registered)
            {
                Close(false);
                return;
            }

            Position();
            RefiningStation.PositionFixture();
            AcidStation.PositionFixture();
            NitricProduction.Update();
            IndustrialFactory.Update();
            CaptureAfterDrop();
        }

        private void Position()
        {
            RectTransform rect = window.rectTransform;
            Canvas canvas = RenderHandler.current?.windowUICanvas;
            if (!(rect.parent is RectTransform parent) || canvas == null ||
                window.widthPixels <= 0 || window.heightPixels <= 0)
            {
                return;
            }

            // 只移动原版窗口。拖放时物品会在库存和全局预览层之间切换，须保持原版 UI 的比例。
            // 窗口以左上角绘制，用实际网格尺寸把窗口中心对准红框中心。
            LetterboxManager.ComputeGameArea(Screen.width, Screen.height, out int x, out int y,
                out int width, out int height);
            var screenCenter = new Vector2(x + width * (935f / 1920f), y + height * (695f / 1080f));
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenCenter, camera, out Vector2 center))
            {
                return;
            }

            int scrollbarWidth = inventory.widthPixels - inventory.width * UISettings.current.gridSize;
            rect.localPosition = new Vector3(center.x - (window.widthPixels - scrollbarWidth) * 0.5f,
                center.y + window.heightPixels * 0.5f, rect.localPosition.z);
        }

        internal bool CanReceive(GameItem item)
        {
            if (applying || Contains(item))
            {
                return true;
            }
            if (!GeneralHelper.IsItemOwned(item))
            {
                return false;
            }

            if (!LabStorage.IsAllowed(item.identifier))
            {
                NotifyNotAllowed();
                return false;
            }

            Dictionary<string, int> counts = ReadCounts();
            counts.TryGetValue(item.identifier, out int units);
            counts[item.identifier] = units + item.unitCount;
            if (LabStorage.SlotsNeeded(counts) <= LabStorage.Capacity)
            {
                return true;
            }

            NotifyFull();
            return false;
        }

        internal bool TryAddDebugItem(string id, int count, int bottleSize = -1)
        {
            GameItem item = ModHelper.SpawnItem(id);
            if (item == null)
            {
                return false;
            }

            var mayStack = item.mayStackItemFunc;
            try
            {
                if (bottleSize >= 0)
                {
                    ReagentBottle.SetSize(item, bottleSize);
                }
                if (ReagentBottle.IsGas(id))
                {
                    item.mayStackItemFunc = (_, _) => false;
                }
                ModHelper.SetItemOwnerShip(item, true);
                item.SetUnitCount(count);
                if (!CanReceive(item))
                {
                    return false;
                }

                SlotMarker slot = inventory.TryFindOneValidInventorySlot(item);
                if (slot == null || slot.inventory != inventory || slot.numTransfer < count)
                {
                    return false;
                }

                GameItem stack = slot.targetItem;
                int previousUnits = stack?.unitCount ?? 0;
                bool saved = false;
                try
                {
                    saved = slot.TryAcceptOnce(count) == count && CaptureInventory(refreshPanel: false);
                    return saved;
                }
                finally
                {
                    if (!saved)
                    {
                        if (stack != null && stack.unitCount != previousUnits)
                        {
                            stack.SetUnitCount(previousUnits);
                        }
                        if (item.parentInventory == inventory)
                        {
                            item.Destroy();
                        }
                    }
                }
            }
            finally
            {
                item.mayStackItemFunc = mayStack;
                if (item.parentInventory == null && item is GameItemElement element && !element.IsDestroyed())
                {
                    item.Destroy();
                }
            }
        }

        internal bool TryClearStorage(out int removedStacks)
        {
            removedStacks = 0;
            GameItem[] items = inventory.childItems.ToArray();
            bool saved = false;
            try
            {
                foreach (GameItem item in items)
                {
                    if (!inventory.Expel(item))
                    {
                        return false;
                    }
                }

                saved = CaptureInventory(refreshPanel: false);
                if (saved)
                {
                    removedStacks = items.Length;
                }
                return saved;
            }
            finally
            {
                foreach (GameItem item in items)
                {
                    if (saved)
                    {
                        item.Destroy();
                    }
                    else if (item.parentInventory == null && !inventory.UncheckedAccept(item))
                    {
                        ChemistryMod.Log?.Error("清空失败后未能恢复仓库物品：" + item.identifier);
                    }
                }
                if (saved)
                {
                    LabPanel.RefreshFromInventory();
                }
            }
        }

        private bool CaptureInventory(bool refreshPanel = true)
        {
            List<GameItem> items = AllItems();
            Dictionary<string, int> counts = ReadCounts(items);
            string bottleLayout = string.Join("|", items.Where(item => ReagentBottle.IsBottle(item.identifier))
                .GroupBy(item => item.identifier).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.Key + "=" + string.Join(",", group.OrderBy(item => item.unitCount)
                    .ThenBy(ReagentBottle.SizeOf).ThenBy(ReagentBottle.SavedComposition, StringComparer.Ordinal)
                    .Select(item => item.unitCount.ToString(CultureInfo.InvariantCulture) + "@"
                        + ReagentBottle.SizeOf(item).ToString(CultureInfo.InvariantCulture)
                        + (ReagentBottle.SavedComposition(item) is string composition ? "@" + composition : "")))));
            string lineLayout = IndustrialFactoryData.JoinLayout(NitricProduction.Capture(), IndustrialFactory.Capture());
            if (EqualCounts(counts, LabStorage.Counts()) && bottleLayout == LabStorage.BottleLayout && lineLayout == LabStorage.LineLayout)
            {
                return true;
            }

            applying = true;
            try
            {
                if (LabStorage.TryStoreGridCounts(counts, bottleLayout, lineLayout))
                {
                    if (refreshPanel)
                    {
                        LabPanel.RefreshFromInventory();
                    }
                    return true;
                }

                NotifySaveFailed();
                return false;
            }
            finally
            {
                applying = false;
            }
        }

        internal bool CanPourBottles(GameItem source, GameItem target, int amount, string result, int resultAmount, bool replaceTarget)
        {
            bool sourceInside = Contains(source);
            bool targetInside = Contains(target);
            if (!sourceInside && !targetInside)
            {
                return true;
            }

            Dictionary<string, int> counts = ReadCounts();
            if (sourceInside)
            {
                Adjust(source.identifier, -amount);
                if (source.unitCount == amount && ReagentBottle.ReturnedEmpty(source) is string empty)
                {
                    Adjust(empty, 1);
                }
            }
            if (targetInside)
            {
                if (replaceTarget) Adjust(target.identifier, -target.unitCount);
                Adjust(result, replaceTarget ? resultAmount : amount);
            }
            if (LabStorage.SlotsNeeded(counts) <= LabStorage.Capacity)
            {
                return true;
            }
            NotifyFull();
            return false;

            void Adjust(string id, int delta)
            {
                counts.TryGetValue(id, out int previous);
                counts[id] = previous + delta;
            }
        }

        internal bool TrySaveMachineBatch() => CaptureInventory(refreshPanel: false);

        private void ApplyStorage()
        {
            applying = true;
            try
            {
                Dictionary<string, int> wanted = LabStorage.Counts();
                List<GameItem> items = AllItems();
                Dictionary<string, int> current = ReadCounts(items);

                foreach (GameItem item in items)
                {
                    string id = item.identifier;
                    wanted.TryGetValue(id, out int target);
                    int remove = Math.Min(item.unitCount, current[id] - target);
                    if (remove <= 0)
                    {
                        continue;
                    }

                    current[id] -= remove;
                    if (remove == item.unitCount)
                    {
                        item.Destroy();
                    }
                    else
                    {
                        item.SetUnitCount(item.unitCount - remove);
                    }
                }

                foreach (KeyValuePair<string, int> entry in wanted)
                {
                    current.TryGetValue(entry.Key, out int count);
                    if (count < entry.Value && ReagentBottle.IsBottle(entry.Key))
                    {
                        var amounts = ReadBottleAmounts(entry.Key, entry.Value).ToList();
                        foreach (GameItem existing in items.Where(item => item.parentInventory != null && item.identifier == entry.Key))
                        {
                            amounts.Remove((existing.unitCount, ReagentBottle.SizeOf(existing), ReagentBottle.SavedComposition(existing)));
                        }
                        foreach (var (amount, size, composition) in amounts)
                        {
                            if (amount <= entry.Value - count)
                            {
                                AddItem(entry.Key, amount, size, composition);
                                count += amount;
                            }
                        }
                    }
                    while (count < entry.Value)
                    {
                        int amount = Math.Min(ReagentBottle.IsSingleBottle(entry.Key) || IndustrialFactoryData.IsFactoryItem(entry.Key)
                            ? 1 : LabStorage.MaxCountPerSlot,
                            entry.Value - count);
                        AddItem(entry.Key, amount);
                        count += amount;
                    }
                }
            }
            finally
            {
                applying = false;
            }
        }

        private static (int Amount, int Size, string Composition)[] ReadBottleAmounts(string itemId, int total)
        {
            foreach (string entry in LabStorage.BottleLayout.Split('|'))
            {
                if (!entry.StartsWith(itemId + "=", StringComparison.Ordinal))
                {
                    continue;
                }
                var amounts = new List<(int, int, string)>();
                foreach (string field in entry.Substring(itemId.Length + 1).Split(','))
                {
                    string[] parts = field.Split('@');
                    int size = ReagentBottle.DefaultSize(itemId);
                    if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int amount)
                        || amount <= 0 || amount > LabStorage.MaxCountPerSlot
                        || (ReagentBottle.IsSingleBottle(itemId) && amount != 1)
                        || parts.Length > 3 || (parts.Length >= 2 &&
                            (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out size) || size < 0 || size > ReagentBottle.Bulk)))
                    {
                        return Array.Empty<(int, int, string)>();
                    }
                    amounts.Add((amount, parts.Length >= 2 ? ReagentBottle.RestoreSavedSize(itemId, size, amount) : size,
                        parts.Length == 3 ? parts[2] : null));
                    total -= amount;
                    if (total < 0)
                    {
                        return Array.Empty<(int, int, string)>();
                    }
                }
                return total == 0 ? amounts.ToArray() : Array.Empty<(int, int, string)>();
            }
            // Old saves only tracked total portions; their existing stacks remain usable.
            return Array.Empty<(int, int, string)>();
        }

        private void AddItem(string id, int count, int bottleSize = -1, string composition = null)
        {
            GameItem item = ModHelper.SpawnItem(id);
            if (item == null)
            {
                throw new InvalidOperationException("无法生成库存物品 " + id);
            }

            var mayStack = item.mayStackItemFunc;
            try
            {
                if (bottleSize >= 0)
                {
                    ReagentBottle.SetSize(item, bottleSize);
                    if (ReagentBottle.IsGas(id))
                    {
                        item.mayStackItemFunc = (_, _) => false;
                    }
                }
                ModHelper.SetItemOwnerShip(item, true);
                item.SetUnitCount(count);
                if (composition != null) ReagentBottle.RestoreComposition(item, composition);
                int remaining = GraphUtils.TryAcceptAll(inventory, item, count);
                if (remaining == 0 || (remaining == count && ItemRegistration.TryUseCompactContainerFootprint(item)
                    && GraphUtils.TryAcceptAll(inventory, item, count) == 0))
                {
                    return;
                }
                throw new InvalidOperationException("原版网格放不下 " + id);
            }
            finally
            {
                item.mayStackItemFunc = mayStack;
                if (item.parentInventory == null && item is GameItemElement element && !element.IsDestroyed())
                {
                    item.Destroy();
                }
            }
        }

        private static float lastNotAllowedNotifyTime = -10f;

        /// <summary>拖入不支持物品时的提示（2 秒内只弹一次）。</summary>
        private static void NotifyNotAllowed()
        {
            if (UnityEngine.Time.unscaledTime - lastNotAllowedNotifyTime < 2f)
            {
                return;
            }

            lastNotAllowedNotifyTime = UnityEngine.Time.unscaledTime;
            DayHudBridge.Push(
                Loc.Get("lab_grid_not_allowed", "实验室储物格只收化学物品、实验设备、样品和原版废金属。",
                    "The lab storage only accepts chemistry items, lab equipment, samples and scrap metal."),
                "warn", 3f);
        }

        /// <summary>容量满时的提示（2 秒内只弹一次，避免拖拽时刷屏）。</summary>
        private static void NotifyFull()
        {
            if (UnityEngine.Time.unscaledTime - lastFullNotifyTime < 2f)
            {
                return;
            }

            lastFullNotifyTime = UnityEngine.Time.unscaledTime;
            DayHudBridge.Push(
                Loc.Get("lab_grid_full",
                    "实验室储物格满了（{0}/{1} 格）：同种物品会叠放，先把用不到的拿回店里。",
                    "Lab storage is full ({0}/{1} slots): stack identical items or move some back to the store.",
                    LabStorage.UsedSlots, LabStorage.Capacity),
                "warn", 4f);
        }

        /// <summary>超出容量导致存档写入失败时的提示，避免物品悄悄丢掉。</summary>
        private static void NotifySaveFailed()
        {
            if (UnityEngine.Time.unscaledTime - lastSaveFailNotifyTime < 5f)
            {
                return;
            }

            lastSaveFailNotifyTime = UnityEngine.Time.unscaledTime;
            ChemistryMod.Log?.Warning("实验室储物格保存失败：格子里的物品种类超过容量 " + LabStorage.Capacity);
            DayHudBridge.Push(
                Loc.Get("lab_grid_save_failed",
                    "储物格超出 {0} 格上限，多出来的物品没有保存，先拿回店里再试。",
                    "Storage exceeds the {0}-slot limit; surplus items were not saved. Move them back and retry.",
                    LabStorage.Capacity),
                "warn", 5f);
        }

        private List<GameItem> AllItems()
        {
            var items = new List<GameItem>();
            foreach (GameInventory area in Inventories())
            {
                items.AddRange(area.childItems);
            }
            return items;
        }

        private Dictionary<string, int> ReadCounts() => ReadCounts(AllItems());

        private static Dictionary<string, int> ReadCounts(List<GameItem> items)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (GameItem item in items)
            {
                counts.TryGetValue(item.identifier, out int count);
                counts[item.identifier] = count + item.unitCount;
            }
            return counts;
        }

        private static bool EqualCounts(Dictionary<string, int> left, Dictionary<string, int> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }
            foreach (KeyValuePair<string, int> entry in left)
            {
                if (!right.TryGetValue(entry.Key, out int count) || count != entry.Value)
                {
                    return false;
                }
            }
            return true;
        }

        private void DisableGrid(Exception error)
        {
            ChemistryMod.Log?.Warning("实验室原版储物槽初始化或刷新失败：" + error);
            try
            {
                Close(false);
            }
            catch (Exception cleanupError)
            {
                ChemistryMod.Log?.Warning("关闭实验室储物槽失败：" + cleanupError.Message);
            }
        }

        private void DestroyGrid()
        {
            if (inventory != null)
            {
                foreach (GameItem item in inventory.childItems)
                {
                    item.Destroy();
                }
            }
            window.Destroy();
        }
    }
}
