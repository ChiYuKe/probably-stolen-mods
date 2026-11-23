using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Chemistry
{
    /// <summary>原版网格负责拖放。实验室存档继续使用 LabStorage 的物品数量。</summary>
    internal sealed partial class LabInventoryGrid : MonoBehaviour
    {
        private static LabInventoryGrid active;
        private PixelWindow window;
        private StorageInventory inventory;
        private MachineryStack filterBar;
        private MachineryTabs pageTabs;
        private MachineryStack plusPage;
        private MachineryStack minusPage;
        private CustomUIWindow renameWindow;
        private CustomUIElement renameInput;
        private int renameIndex = -1;
        private readonly List<StorageInventory> warehouseGrids = new List<StorageInventory>();
        private int activePage;
        private readonly Dictionary<StorageFilter, MachineryButton> filterButtons = new Dictionary<StorageFilter, MachineryButton>();
        private readonly Dictionary<GameItemElement, Color> originalItemColors = new Dictionary<GameItemElement, Color>();
        private StorageFilter currentFilter;
        private bool applying;

        // 自定义仓库给足竖向空间，靠滚动条翻页；约等于实验室容量的两倍纵深。
        private const int WarehouseSubRows = 160;

        /// <summary>页签最多显示的字数：存档存全名，页签只放前几字加省略号，
        /// 防止超长名字把整条页签撑出网格。</summary>
        private const int TabCaptionLimit = 6;

        private static string TabCaption(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return Loc.Pick("仓库", "Warehouse");
            }
            return name.Length <= TabCaptionLimit ? name : name.Substring(0, TabCaptionLimit) + "…";
        }

        private enum StorageFilter
        {
            All,
            Bottles,
            Elements,
            Compounds,
            Equipment,
            Samples,
            Other,
        }

        internal static LabInventoryGrid OpenGrid => active;
        internal GameInventory StorageInventoryArea => inventory;

        internal bool Contains(GameItem item)
        {
            if (item?.parentInventory == null)
            {
                return false;
            }
            // 故意不含自定义仓库页：从自定义页拖进实验室页要重新过容量/白名单校验。
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
            public StorageInventory(int totalSubRows = -1) : base(SlotColumns * 2, VisibleSlotRows * 2,
                totalSubRows < 0 ? StorageSlotRows * 2 : totalSubRows, scrollOverflow: 1f)
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

            public override SlotMarker TryInventorySlot(GameItem item, int requestedNum,
                Vector2 itemGraphicsCenterPosition, GridShape tempShape, TagSystem tempState)
            {
                SlotMarker slot = base.TryInventorySlot(item, requestedNum, itemGraphicsCenterPosition, tempShape, tempState);
                // 原版搜索空格时会用非有限坐标回调本方法。只有鼠标落点校验失败才自动补位。
                if (!float.IsFinite(itemGraphicsCenterPosition.x) || item == null || requestedNum <= 0
                    || (slot != null && slot.numTransfer > 0))
                {
                    return slot;
                }

                // 鼠标落点可能正好压在占格冲突或越界的位置；储物格有滚动空间，
                // 所以在整张网格里找一个能放下的位置，避免空着容量却拒绝拖入。
                SlotMarker autoSlot = base.TryFindOneValidInventorySlot(item, keepOrientation: true);
                if (autoSlot != null && autoSlot.numTransfer > 0)
                {
                    autoSlot.numTransfer = Math.Min(autoSlot.numTransfer, Math.Max(1, requestedNum));
                    return autoSlot;
                }
                return slot;
            }

            // 组合放置（多选拖动）重新放开：同库存重排数量不变，跨库存由
            // GroupPlacementGate 补总量校验，见 MultiSelectFilter.cs。
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
                var content = new MachineryStack(vertical: true, gap: 2)
                    .SetAlign(MachineryStack.Align.Start)
                    .SetMainAlign(MachineryStack.Align.Start);
                content.Add(view.BuildFilterBar());
                view.BuildPageRow();
                content.Add(view.pageTabs);
                view.inventory.onSlotAddItemEndFunc += (_, _, _) => view.RefreshFilter();
                window.Attach(content);
                window.Validate();
                RefiningStation.InstallFixture();
                AcidStation.InstallFixture();
                var layouts = IndustrialFactoryData.SplitLayout(LabStorage.LineLayout);
                string[] equipmentRecords = LabStorage.EquipmentLayout.Split('|');
                var owners = view.RestoreMachineIdentities(equipmentRecords);
                NitricProduction.Restore(view, layouts.Nitric);
                IndustrialFactory.Restore(view, layouts.Factory, owners);
                view.RestoreProductionMachineIds();
                view.RestoreEquipmentContents(equipmentRecords, owners);
                view.ApplyStorage(matchPowerCharge: LabStorage.EquipmentLayout.Length > 0);
                view.BuildWarehousePages();
                view.RestorePortableRefiners(equipmentRecords);
                IndustrialFactory.BindLegacyFrame();
                view.restoringMachineIds = null;
                view.RefreshFilter();
                window.Show();
                view.Position();
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
                    view.CaptureWarehouses();
                    LabWarehouses.SaveAll();
                }
            }
            finally
            {
                active = null;
                view.CloseRenameWindow();
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
                active.CaptureWarehouses();
            }
        }

        [HarmonyPatch(typeof(PlayerStore), nameof(PlayerStore.SaveGame))]
        private static class SaveLabInventories
        {
            private static void Prefix()
            {
                if (active == null) return;
                ItemMouseDragHandler.current?.Cancel();
                active.CaptureInventory(refreshPanel: false);
                active.CaptureWarehouses();
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
            ApplyFilterColors();
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

        private MachineryStack BuildFilterBar()
        {
            filterBar = new MachineryStack(vertical: false, gap: 4)
                .SetAlign(MachineryStack.Align.Start)
                .SetMainAlign(MachineryStack.Align.Start);
            AddFilterButton(StorageFilter.All, "全部", 60);
            AddFilterButton(StorageFilter.Bottles, "瓶剂", 60);
            AddFilterButton(StorageFilter.Elements, "元素", 60);
            AddFilterButton(StorageFilter.Compounds, "化合物", 76);
            AddFilterButton(StorageFilter.Equipment, "设备", 60);
            AddFilterButton(StorageFilter.Samples, "样品", 60);
            AddFilterButton(StorageFilter.Other, "其他", 60);
            return filterBar;
        }

        private void AddFilterButton(StorageFilter filter, string label, int width)
        {
            var button = new MachineryButton(Loc.Pick(label, filter switch
            {
                StorageFilter.All => "All",
                StorageFilter.Bottles => "Bottles",
                StorageFilter.Elements => "Elements",
                StorageFilter.Compounds => "Compounds",
                StorageFilter.Equipment => "Tools",
                StorageFilter.Samples => "Samples",
                _ => "Other",
            }), () => SetFilter(filter), width, 13);
            filterButtons.Add(filter, button);
            filterBar.Add(button);
        }

        private void SetFilter(StorageFilter filter)
        {
            currentFilter = filter;
            RefreshFilter();
        }

        private void RefreshFilter()
        {
            if (inventory == null)
            {
                return;
            }

            foreach (KeyValuePair<StorageFilter, MachineryButton> entry in filterButtons)
            {
                entry.Value.SetDisabled(entry.Key == currentFilter);
            }

            ApplyFilterColors();

            var present = new HashSet<GameItemElement>();
            foreach (StorageInventory page in PageGrids())
            {
                foreach (GameItem item in page.childItems)
                {
                    if (item is GameItemElement element)
                    {
                        present.Add(element);
                    }
                }
            }
            foreach (GameItemElement element in originalItemColors.Keys.Where(item => !present.Contains(item)).ToArray())
            {
                originalItemColors.Remove(element);
            }
        }

        /// <summary>原版在拿起、放下、合堆、重排时都会用 ToggleBackground 把物品背景
        /// 重置回全亮（GameItemElement.ValidateSelectLocked / UpdateFakeReal），
        /// 一次性的染色撑不过拖拽，所以每帧补刷；只在颜色被洗掉时才写入。</summary>
        private void ApplyFilterColors()
        {
            if (inventory == null)
            {
                return;
            }

            foreach (StorageInventory page in PageGrids())
            {
                foreach (GameItem item in page.childItems)
                {
                    if (!(item is GameItemElement element) || element.background?.image == null)
                    {
                        continue;
                    }
                    if (!originalItemColors.TryGetValue(element, out Color original))
                    {
                        original = element.background.image.color;
                        originalItemColors[element] = original;
                    }

                    Color expected = original;
                    if (currentFilter != StorageFilter.All && !MatchesFilter(element.identifier, currentFilter))
                    {
                        expected.a *= 0.2f;
                    }
                    Color actual = element.background.image.color;
                    if (!Mathf.Approximately(actual.a, expected.a) || !Mathf.Approximately(actual.r, expected.r)
                        || !Mathf.Approximately(actual.g, expected.g) || !Mathf.Approximately(actual.b, expected.b))
                    {
                        element.background.image.color = expected;
                    }
                }
            }
        }

        /// <summary>筛选模式下被变暗的物品：不参与原版框选（见 MultiSelectFilter）。</summary>
        internal bool IsFilteredOut(GameItemElement element)
        {
            return currentFilter != StorageFilter.All && !MatchesFilter(element.identifier, currentFilter);
        }

        private static bool MatchesFilter(string id, StorageFilter filter)
        {
            if (filter == StorageFilter.All)
            {
                return true;
            }

            bool chemistry = id?.StartsWith(ChemistryCatalog.ItemPrefix, StringComparison.Ordinal) == true;
            bool bottle = ReagentBottle.IsBottle(id) || ReagentBottle.IsEmpty(id)
                || BulkContainers.ItemIds.Contains(id);
            bool element = id?.StartsWith(ChemistryCatalog.ItemPrefix + "el_", StringComparison.Ordinal) == true;
            bool compound = id?.StartsWith(ChemistryCatalog.ItemPrefix + "cmp_", StringComparison.Ordinal) == true;
            bool equipment = id?.StartsWith(ChemistryCatalog.ItemPrefix + "tool_", StringComparison.Ordinal) == true
                || id == RefiningStation.ItemId || id == AcidStation.ItemId;
            bool sample = id?.StartsWith(ChemistryCatalog.ItemPrefix + "sample_", StringComparison.Ordinal) == true
                || id?.StartsWith(ChemistryCatalog.ItemPrefix + "intermediate_", StringComparison.Ordinal) == true;

            return filter switch
            {
                StorageFilter.Bottles => bottle,
                StorageFilter.Elements => element,
                StorageFilter.Compounds => compound,
                StorageFilter.Equipment => equipment,
                StorageFilter.Samples => sample,
                StorageFilter.Other => !chemistry || !(bottle || element || compound || equipment || sample),
                _ => true,
            };
        }

        // ---------------------------------------------------------------
        // 自定义仓库页：左侧竖条切换，页 0 固定是实验室储物格。
        // ---------------------------------------------------------------

        internal bool OwnsPage(GameInventory area)
        {
            if (area == null)
            {
                return false;
            }
            if (area == inventory)
            {
                return true;
            }
            for (int i = 0; i < warehouseGrids.Count; i++)
            {
                if (warehouseGrids[i] == area)
                {
                    return true;
                }
            }
            return false;
        }

        private IEnumerable<StorageInventory> PageGrids()
        {
            if (inventory != null)
            {
                yield return inventory;
            }
            for (int i = 0; i < warehouseGrids.Count; i++)
            {
                if (warehouseGrids[i] != null)
                {
                    yield return warehouseGrids[i];
                }
            }
        }

        private void BuildPageRow()
        {
            // 原版 MachineryTabs：单击切换、选中页签双击触发改名回调、自动显隐页面。
            pageTabs = new MachineryTabs();
            pageTabs.onChanged = OnTabChanged;
            pageTabs.onRenameRequest = OnTabRenameRequest;
            pageTabs.AddTab(Loc.Pick("实验", "Lab"), inventory);
        }

        /// <summary>页签重排：[实验][仓库…][＋][－]。＋/－ 是挂在条尾的空页动作
        /// 页签，随仓库增删整体重排，保证它们永远收尾；desired 为重排后选中的页。</summary>
        private void RebuildTabs(int desired)
        {
            pageTabs.Detach(inventory);
            for (int i = 0; i < warehouseGrids.Count; i++)
            {
                if (warehouseGrids[i] != null)
                {
                    pageTabs.Detach(warehouseGrids[i]);
                }
            }
            if (plusPage != null)
            {
                pageTabs.Detach(plusPage);
                UnityEngine.Object.Destroy(plusPage.handler.gameObject);
                plusPage = null;
            }
            if (minusPage != null)
            {
                pageTabs.Detach(minusPage);
                UnityEngine.Object.Destroy(minusPage.handler.gameObject);
                minusPage = null;
            }

            pageTabs.AddTab(Loc.Pick("实验", "Lab"), inventory);
            for (int i = 0; i < warehouseGrids.Count; i++)
            {
                pageTabs.AddTab(TabCaption(LabWarehouses.Get(i)?.Name), warehouseGrids[i]);
            }
            // 空 MachineryStack 当占位页：被“选中”时无内容可显示，回调里会马上切回真实页。
            // ＋ 页签直接标出下一个仓库的售价。
            plusPage = new MachineryStack(vertical: true, gap: 0);
            minusPage = new MachineryStack(vertical: true, gap: 0);
            pageTabs.AddTab("＋$" + LabWarehouses.PriceFor(warehouseGrids.Count), plusPage);
            pageTabs.AddTab(Loc.Pick("－", "-"), minusPage);

            int selected = Mathf.Clamp(desired, 0, warehouseGrids.Count);
            pageTabs.Select(selected);
            activePage = selected;
            window.Validate();
        }

        private void BuildWarehousePages()
        {
            if (!LabWarehouses.Loaded)
            {
                LabWarehouses.EnsureLoaded();
            }
            for (int i = 0; i < LabWarehouses.Count; i++)
            {
                AddWarehousePage(i);
            }
            RebuildTabs(0);
        }

        private void AddWarehousePage(int index)
        {
            LabWarehouses.Snapshot snapshot = LabWarehouses.Get(index);
            if (snapshot == null)
            {
                return;
            }

            var grid = new StorageInventory(WarehouseSubRows);
            grid.mayInventoryAddItemFunc = (item, _) => CanReceiveFree(item);
            grid.onSlotAddItemEndFunc += (_, _, _) => RefreshFilter();
            warehouseGrids.Add(grid);
            RestoreWarehouse(index, grid);
        }

        private bool IsActionTab(int index)
        {
            PixelElement page = index >= 0 ? pageTabs.GetPage(index) : null;
            return page == plusPage || page == minusPage;
        }

        private void OnTabChanged(int index)
        {
            if (IsActionTab(index))
            {
                if (pageTabs.GetPage(index) == plusPage)
                {
                    CreateWarehouse();
                }
                else
                {
                    DeleteActiveWarehouse();
                }
                return;
            }
            activePage = index;
            RefreshFilter();
        }

        private void OnTabRenameRequest(int index)
        {
            if (index > 0 && !IsActionTab(index))
            {
                RenameWarehouse(index - 1);
            }
        }

        private void SwitchPage(int page)
        {
            // 动作页签（＋/－）在真实页之后，越界判断一并把它们挡掉。
            if (page == activePage || page < 0 || page > warehouseGrids.Count)
            {
                return;
            }
            pageTabs.Select(page);
            activePage = page;
            RefreshFilter();
        }

        /// <summary>页签控件（LabTabDragSwitch 拖拽悬停切页用）。</summary>
        internal MachineryTabs PageTabs => pageTabs;

        /// <summary>拖拽悬停到仓库页签时切页：序号越界或重复切由 SwitchPage 自行挡掉。</summary>
        internal void SwitchPageFromDrag(int page)
        {
            SwitchPage(page);
        }

        private void RenameWarehouse(int index)
        {
            LabWarehouses.Snapshot snapshot = LabWarehouses.Get(index);
            if (snapshot == null)
            {
                return;
            }
            CloseRenameWindow();
            CustomUIBuilder builder = CustomUIManager.Window("chem_warehouse_rename",
                Loc.Get("lab_warehouse_rename_title", "重命名仓库", "Rename warehouse"));
            if (builder == null)
            {
                ChemistryMod.Log?.Warning("创建重命名窗口失败");
                return;
            }
            renameIndex = index;
            builder.SetSize(340f, 110f)
                .SetDraggable(true)
                .SetCloseOnEscape(true)
                .OnClose(CloseRenameWindow)
                .Center();
            builder.AddInput(Loc.Get("lab_warehouse_rename_hint", "输入新名字", "New name"), snapshot.Name, null, "name");
            renameInput = builder.Last;
            builder.BeginRow(6f, TextAnchor.MiddleLeft);
            builder.AddButton(Loc.Get("lab_warehouse_rename_ok", "确定", "OK"), ApplyRename, "ok");
            builder.Last.SetFixedWidth(80f);
            builder.AddButton(Loc.Get("lab_warehouse_rename_cancel", "取消", "Cancel"), CloseRenameWindow, "cancel");
            builder.Last.SetFixedWidth(80f);
            builder.End();
            renameWindow = builder.Show();
        }

        private void ApplyRename()
        {
            int index = renameIndex;
            if (index < 0)
            {
                CloseRenameWindow();
                return;
            }
            string name = renameInput?.input?.text;
            string error = LabWarehouses.Rename(index, name);
            if (error != null)
            {
                // 窗口留着，提示后直接改字重试。
                DayHudBridge.Push(error, "warn", 3f);
                return;
            }
            CloseRenameWindow();
            pageTabs.SetCaption(index + 1, TabCaption(LabWarehouses.Get(index)?.Name));
        }

        private void CloseRenameWindow()
        {
            renameIndex = -1;
            renameInput = null;
            CustomUIWindow current = renameWindow;
            renameWindow = null;
            try
            {
                if (current != null && current.IsAlive)
                {
                    current.Close();
                }
            }
            catch
            {
                // 窗口可能已被场景卸载
            }
        }

        private GameGridScrollableInventory PageAt(int page)
        {
            if (page == 0)
            {
                return inventory;
            }
            return page - 1 < warehouseGrids.Count ? warehouseGrids[page - 1] : null;
        }

        private void CreateWarehouse()
        {
            try
            {
                if (LabWarehouses.Count >= LabWarehouses.MaxCount)
                {
                    DayHudBridge.Push(Loc.Get("lab_warehouse_limit",
                        "自定义仓库最多 {0} 个，先删掉一些再建。",
                        "Custom warehouses are limited to {0}; delete some first.", LabWarehouses.MaxCount),
                        "warn", 3f);
                    return;
                }
                int price = LabWarehouses.PriceFor(LabWarehouses.Count);
                PlayerStore store = PlayerStore.Instance;
                if (store == null)
                {
                    return;
                }
                if (!store.CanPay(price))
                {
                    DayHudBridge.Push(Loc.Get("lab_warehouse_poor",
                        "开新仓库需要 ${0}，钱不够。", "A new warehouse costs ${0}; you can't afford it.", price),
                        "warn", 3f);
                    return;
                }
                // 先建仓再扣款：Create 因上限兜底返回 -1 时不能白扣钱。
                int index = LabWarehouses.Create();
                if (index < 0)
                {
                    return;
                }
                AddWarehousePage(index);
                RebuildTabs(index + 1);
                RefreshFilter();
                store.PayMoney(price, playRegisterSound: true);
                DayHudBridge.Push(Loc.Get("lab_warehouse_bought",
                    "花 ${0} 开了新仓库。", "Paid ${0} for the new warehouse.", price), "info", 2.5f);
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("新建自定义仓库失败：" + e.Message);
            }
        }

        private void DeleteActiveWarehouse()
        {
            if (activePage == 0)
            {
                DayHudBridge.Push(Loc.Get("lab_warehouse_delete_hint",
                    "先切到要删除的仓库页，再点删除。",
                    "Switch to the warehouse page you want to delete first."), "info", 3f);
                return;
            }
            int index = activePage - 1;
            string error = LabWarehouses.Delete(index);
            if (error != null)
            {
                DayHudBridge.Push(error, "warn", 3f);
                return;
            }
            // 删除只允许空仓，网格里没有物品；把网格移出列表并销毁，再由 RebuildTabs 重排页签。
            StorageInventory removed = warehouseGrids[index];
            warehouseGrids.RemoveAt(index);
            pageTabs.Detach(removed);
            UnityEngine.Object.Destroy(removed.handler.gameObject);
            RebuildTabs(0);
            RefreshFilter();
        }

        /// <summary>自定义仓库只挡三类：未认领的、带内部物品的容器和机器——计数式存档救不了它们。</summary>
        private bool CanReceiveFree(GameItem item)
        {
            if (applying || Contains(item))
            {
                return true;
            }
            if (!GeneralHelper.IsItemOwned(item))
            {
                return false;
            }
            if (item.IsGameItemType("MACHINE") || GraphUtils.FindChildrenType<GameItem>(item, null, null).Count > 0)
            {
                NotifyWarehouseReject();
                return false;
            }
            return true;
        }

        private static float lastWarehouseRejectNotifyTime = -10f;

        private static void NotifyWarehouseReject()
        {
            if (UnityEngine.Time.unscaledTime - lastWarehouseRejectNotifyTime < 2f)
            {
                return;
            }
            lastWarehouseRejectNotifyTime = UnityEngine.Time.unscaledTime;
            DayHudBridge.Push(Loc.Get("lab_warehouse_reject",
                "带内容物的容器和机器放不进自定义仓库（存档只记数量），先把里面清空。",
                "Containers with contents and machines cannot go into custom warehouses (only item counts are saved)."),
                "warn", 4f);
        }

        private void RestoreWarehouse(int index, StorageInventory grid)
        {
            LabWarehouses.Snapshot snapshot = LabWarehouses.Get(index);
            if (snapshot == null)
            {
                return;
            }
            applying = true;
            try
            {
                foreach (KeyValuePair<string, int> entry in snapshot.Counts)
                {
                    int count = entry.Value;
                    if (ReagentBottle.IsBottle(entry.Key))
                    {
                        foreach (var (amount, size, composition) in ReadBottleAmounts(snapshot.BottleLayout, entry.Key, count))
                        {
                            if (amount <= count)
                            {
                                AddItem(entry.Key, amount, size, composition, grid);
                                count -= amount;
                            }
                        }
                    }
                    while (count > 0)
                    {
                        bool equipment = TracksMachineIdentity(entry.Key);
                        int take = Math.Min(equipment || ReagentBottle.IsSingleBottle(entry.Key) ? 1 : LabStorage.MaxCountPerSlot, count);
                        AddItem(entry.Key, take, target: grid, instanceId: TakeMachineId(WarehouseArea(index), entry.Key));
                        count -= take;
                    }
                }

                foreach (var battery in ReadBatteryLayout(snapshot.BatteryLayout))
                {
                    for (int i = 0; i < battery.Units; i++)
                    {
                        AddPowerItem(grid, battery.Id, battery.Energy);
                    }
                }
            }
            finally
            {
                applying = false;
            }
        }

        /// <summary>生成一件带电量的电源类物品（电池等）；电量写回原版能量标签。</summary>
        private void AddPowerItem(GameInventory target, string id, int? energy)
        {
            GameItem item = ModHelper.SpawnItem(id);
            if (item == null)
            {
                throw new InvalidOperationException("无法生成电源物品 " + id);
            }
            var mayStack = item.mayStackItemFunc;
            try
            {
                // 电量各不相同，逐件生成不叠放。
                item.mayStackItemFunc = (_, _) => false;
                ModHelper.SetItemOwnerShip(item, true);
                item.SetUnitCount(1);
                RestoreBatteryEnergy(item, energy);
                if (GraphUtils.TryAcceptAll(target, item, 1) != 0)
                {
                    throw new InvalidOperationException("电源物品放不下 " + id);
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

        private bool CaptureWarehouses()
        {
            for (int i = 0; i < warehouseGrids.Count; i++)
            {
                StorageInventory grid = warehouseGrids[i];
                if (grid == null)
                {
                    continue;
                }
                var items = new List<GameItem>(grid.childItems);
                // 电源类物品（电池等）带电量标签，单独走明细段，不进计数。
                LabWarehouses.Store(i, ReadCounts(items), BuildBottleLayout(items), BuildBatteryLayout(items));
            }
            return true;
        }

        private static bool IsPowerItem(GameItem item)
        {
            return item != null && (item.IsTag("power_source_item") || item.identifier is
                "energy_credit" or "energy_credit_ext" or "energy_credit_breeder" or "energy_credit_dis" or "energy_credit_hyp");
        }

        internal static int? BatteryEnergy(GameItem item)
        {
            TagState energy = item.GetTagReadonly("power_source_item_energy");
            return energy?.IsEnabled() == true ? energy.GetInt() : (int?)null;
        }

        private static void RestoreBatteryEnergy(GameItem item, int? energy)
        {
            if (energy is int remainingEnergy)
            {
                item.ModifyTag("power_source_item_energy", state => state.Enable().SetInt(remainingEnergy), false);
                PowerHelper.UpdateEnergyCreditSprite(item);
            }
        }

        /// <summary>电源逐件保存。带电量的条目为 id@电量:数量，旧条目为 id:数量。</summary>
        private static string BuildBatteryLayout(List<GameItem> items)
        {
            var builder = new StringBuilder();
            foreach (GameItem item in items)
            {
                if (!IsPowerItem(item))
                {
                    continue;
                }
                if (builder.Length > 0)
                {
                    builder.Append('|');
                }
                builder.Append(item.identifier);
                if (BatteryEnergy(item) is int energy)
                {
                    builder.Append('@').Append(energy.ToString(CultureInfo.InvariantCulture));
                }
                builder.Append(':').Append(item.unitCount.ToString(CultureInfo.InvariantCulture));
            }
            return builder.ToString();
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

            // 电源使用原版网格检查占格，并走独立存档明细。
            if (IsPowerItem(item))
            {
                return true;
            }
            if (!LabStorage.IsAllowed(item.identifier))
            {
                NotifyNotAllowed(item);
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

        /// <summary>多选整组放入实验室储物格前的总量校验：单件检查都会过、
        /// 加在一起超容量的边界由这里兜住（见 GroupPlacementGate）。</summary>
        internal bool MayAcceptGroup(List<GameItem> items)
        {
            Dictionary<string, int> counts = ReadCounts();
            foreach (GameItem item in items)
            {
                if (item == null || !GeneralHelper.IsItemOwned(item))
                {
                    return false;
                }
                if (IsPowerItem(item))
                {
                    continue;
                }
                if (!LabStorage.IsAllowed(item.identifier))
                {
                    NotifyNotAllowed(item);
                    return false;
                }
                counts.TryGetValue(item.identifier, out int units);
                counts[item.identifier] = units + item.unitCount;
            }
            if (LabStorage.SlotsNeeded(counts) <= LabStorage.Capacity)
            {
                return true;
            }
            NotifyFull();
            return false;
        }

        internal bool TryAddDebugItem(string id, int count, int bottleSize = -1)
        {
            // 发到当前页：在自定义仓库页领取就进自定义仓库，规则按页走。
            GameGridScrollableInventory target = PageAt(activePage) ?? inventory;
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
                if (target == inventory ? !CanReceive(item) : !CanReceiveFree(item))
                {
                    return false;
                }

                SlotMarker slot = target.TryFindOneValidInventorySlot(item);
                if (slot == null || slot.inventory != target || slot.numTransfer < count)
                {
                    return false;
                }

                GameItem stack = slot.targetItem;
                int previousUnits = stack?.unitCount ?? 0;
                bool saved = false;
                try
                {
                    saved = slot.TryAcceptOnce(count) == count
                        && (target == inventory ? CaptureInventory(refreshPanel: false) : CaptureWarehouses());
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
                        if (item.parentInventory == target)
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

        /// <summary>清空“当前页”：实验页走实验室计数，自定义页只清格子并立即落盘。</summary>
        internal bool TryClearStorage(out int removedStacks)
        {
            removedStacks = 0;
            GameGridScrollableInventory target = PageAt(activePage) ?? inventory;
            bool labPage = target == inventory;
            GameItem[] items = target.childItems.ToArray();
            bool saved = false;
            try
            {
                foreach (GameItem item in items)
                {
                    if (!target.Expel(item))
                    {
                        return false;
                    }
                }

                saved = labPage ? CaptureInventory(refreshPanel: false) : CaptureWarehouses();
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
                    else if (item.parentInventory == null && !target.UncheckedAccept(item))
                    {
                        ChemistryMod.Log?.Error("清空失败后未能恢复仓库物品：" + item.identifier);
                    }
                }
                if (saved && labPage)
                {
                    LabPanel.RefreshFromInventory();
                }
            }
        }

        private bool CaptureInventory(bool refreshPanel = true)
        {
            List<GameItem> items = AllItems();
            // 电池等电源物品不进计数段：它们由 BatteryLayout 带电量逐件还原，
            // 混进计数会被 ApplyStorage 按默认电量重新生成，电量就丢了。
            Dictionary<string, int> counts = ReadCounts(items);
            string bottleLayout = BuildBottleLayout(items);
            string powerLayout = BuildBatteryLayout(items);
            string lineLayout = IndustrialFactoryData.JoinLayout(NitricProduction.Capture(), IndustrialFactory.Capture());
            string equipmentLayout = CaptureEquipmentLayout();
            if (EqualCounts(counts, LabStorage.Counts()) && bottleLayout == LabStorage.BottleLayout
                && lineLayout == LabStorage.LineLayout && powerLayout == LabStorage.PowerLayout
                && equipmentLayout == LabStorage.EquipmentLayout)
            {
                return true;
            }

            applying = true;
            try
            {
                if (LabStorage.TryStoreGridCounts(counts, bottleLayout, lineLayout, powerLayout, equipmentLayout))
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

        /// <summary>瓶剂逐瓶明细（数量@容量@成分），还原时按瓶重建，避免半瓶被并成整瓶。</summary>
        private static string BuildBottleLayout(List<GameItem> items)
        {
            return string.Join("|", items.Where(item => ReagentBottle.IsBottle(item.identifier))
                .GroupBy(item => item.identifier).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => group.Key + "=" + string.Join(",", group.OrderBy(item => item.unitCount)
                    .ThenBy(ReagentBottle.SizeOf).ThenBy(ReagentBottle.SavedComposition, StringComparer.Ordinal)
                    .Select(item => item.unitCount.ToString(CultureInfo.InvariantCulture) + "@"
                        + ReagentBottle.SizeOf(item).ToString(CultureInfo.InvariantCulture)
                        + (ReagentBottle.SavedComposition(item) is string composition ? "@" + composition : "")))));
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

        private void RestoreEquipmentContents(string[] records, HashSet<(int Id, string Item)> owners)
        {
            var slots = LabEquipmentPanel.ReadContents(records).ToList();
            if (slots.Count == 0) return;
            var equipmentInSlots = slots.Where(slot => slot.ItemInstanceId > 0).ToArray();
            bool foundOwner;
            do
            {
                foundOwner = false;
                foreach (var slot in equipmentInSlots)
                {
                    if (HasSavedSlotOwner(slot, owners))
                        foundOwner |= owners.Add((slot.ItemInstanceId, slot.Id));
                }
            }
            while (foundOwner);

            Dictionary<string, int> counts = LabStorage.Counts();
            var power = ReadBatteryLayout(LabStorage.PowerLayout).GroupBy(entry => (entry.Id, entry.Energy))
                .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Units));
            foreach (GameItem item in AllItems())
            {
                if (IsPowerItem(item))
                {
                    var key = (item.identifier, BatteryEnergy(item));
                    if (power.ContainsKey(key)) power[key] -= item.unitCount;
                }
                else if (counts.ContainsKey(item.identifier))
                {
                    counts[item.identifier] -= item.unitCount;
                }
            }

            // 槽位记录使用库存总量作为上限。放不回设备的物品由 ApplyStorage 补到实验页。
            foreach (LabEquipmentPanel.SavedItem item in slots)
            {
                if (IsPortableRefinerSlot(item) || !HasSavedSlotOwner(item, owners)) continue;
                var powerKey = (item.Id, item.Energy);
                bool isPower = power.TryGetValue(powerKey, out int available);
                if (!isPower) counts.TryGetValue(item.Id, out available);
                if (available < item.Units) continue;

                try
                {
                    GameInventory target = ResolveSavedSlot(item);
                    if (target == null || !RestoreEquipmentItem(target, item)) continue;
                    if (isPower) power[powerKey] -= item.Units;
                    else counts[item.Id] -= item.Units;
                }
                catch (Exception error)
                {
                    ChemistryMod.Log?.Warning("恢复实验设备槽失败：" + item.Equipment + "/" + item.Slot + " " + error.Message);
                }
            }
        }

        private static bool HasSavedSlotOwner(LabEquipmentPanel.SavedItem item, HashSet<(int Id, string Item)> owners) =>
            item.MachineId == 0 || owners.Contains((item.MachineId, IsPortableRefinerSlot(item)
                ? item.Equipment : ChemistryCatalog.ToolItemId(item.Equipment)));

        private static bool RestoreEquipmentItem(GameInventory target, LabEquipmentPanel.SavedItem saved)
        {
            GameItem item = ModHelper.SpawnItem(saved.Id);
            if (item == null) return false;
            var allowsItem = target.mayInventoryAddItemFunc;
            try
            {
                ModHelper.SetItemOwnerShip(item, true);
                if (saved.ItemInstanceId > 0) item.uniqueId = saved.ItemInstanceId;
                if (ReagentBottle.IsBottle(saved.Id) && saved.BottleSize >= 0 && saved.BottleSize <= ReagentBottle.Bulk)
                {
                    ReagentBottle.SetSize(item, ReagentBottle.RestoreSavedSize(saved.Id, saved.BottleSize, saved.Units));
                }
                item.SetUnitCount(saved.Units);
                if (!string.IsNullOrEmpty(saved.Composition)) ReagentBottle.RestoreComposition(item, saved.Composition);
                RestoreBatteryEnergy(item, saved.Energy);

                target.mayInventoryAddItemFunc = (candidate, _) => candidate == item;
                SlotMarker slot = target.TryFindOneValidInventorySlot(item);
                return slot?.inventory == target && slot.numTransfer >= saved.Units
                    && slot.TryAcceptOnce(saved.Units) == saved.Units;
            }
            finally
            {
                target.mayInventoryAddItemFunc = allowsItem;
                if (item.parentInventory == null && item is GameItemElement element && !element.IsDestroyed()) item.Destroy();
            }
        }

        private void ApplyStorage(bool matchPowerCharge = false)
        {
            applying = true;
            try
            {
                Dictionary<string, int> wanted = LabStorage.Counts();
                List<GameItem> items = AllItems();
                Dictionary<string, int> current = ReadCounts(items);

                foreach (GameItem item in items)
                {
                    if (IsPowerItem(item))
                    {
                        continue;
                    }
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
                        var amounts = ReadBottleAmounts(LabStorage.BottleLayout, entry.Key, entry.Value).ToList();
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
                        bool single = ReagentBottle.IsSingleBottle(entry.Key) || IndustrialFactoryData.IsFactoryItem(entry.Key)
                            || TracksMachineIdentity(entry.Key);
                        int amount = Math.Min(single ? 1 : LabStorage.MaxCountPerSlot, entry.Value - count);
                        AddItem(entry.Key, amount, instanceId: TakeMachineId(MainStorageArea, entry.Key));
                        count += amount;
                    }
                }

                RestorePowerItems(matchPowerCharge);
            }
            finally
            {
                applying = false;
            }
        }

        /// <summary>按 PowerLayout 补齐电池：只补缺、不清多余，机器槽和退回格里的电池都算数。</summary>
        private void RestorePowerItems(bool matchCharge)
        {
            var current = AllItems().Where(IsPowerItem)
                .GroupBy(item => (item.identifier, matchCharge ? BatteryEnergy(item) : null))
                .ToDictionary(group => group.Key, group => group.Sum(item => item.unitCount));
            foreach (var entry in ReadBatteryLayout(LabStorage.PowerLayout))
            {
                var key = (entry.Id, matchCharge ? entry.Energy : null);
                current.TryGetValue(key, out int available);
                int existing = Math.Min(available, entry.Units);
                current[key] = available - existing;
                try
                {
                    for (int i = existing; i < entry.Units; i++)
                    {
                        AddPowerItem(inventory, entry.Id, entry.Energy);
                    }
                }
                catch (Exception e)
                {
                    ChemistryMod.Log?.Warning("恢复实验室电池失败：" + entry.Id + " " + e.Message);
                }
            }
        }

        private static IEnumerable<(string Id, int? Energy, int Units)> ReadBatteryLayout(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                yield break;
            }

            foreach (string entry in raw.Split('|'))
            {
                int colon = entry.LastIndexOf(':');
                string details = colon < 0 ? entry : entry.Substring(0, colon);
                int at = details.IndexOf('@');
                if (colon < 0 && at < 0)
                {
                    continue;
                }
                int units = 1;
                if (colon >= 0 && (!int.TryParse(entry.Substring(colon + 1), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out units) || units <= 0))
                {
                    continue;
                }
                int? energy = null;
                string id = details.Trim();
                if (at >= 0)
                {
                    id = details.Substring(0, at).Trim();
                    if (!int.TryParse(details.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int charge))
                    {
                        continue;
                    }
                    energy = charge;
                }
                if (id.Length > 0)
                {
                    yield return (id, energy, Math.Min(units, LabStorage.MaxCountPerSlot));
                }
            }
        }

        private static (int Amount, int Size, string Composition)[] ReadBottleAmounts(string layout, string itemId, int total)
        {
            foreach (string entry in layout.Split('|'))
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

        private void AddItem(string id, int count, int bottleSize = -1, string composition = null, GameInventory target = null,
            int instanceId = 0)
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
                if (instanceId > 0) item.uniqueId = instanceId;
                item.SetUnitCount(count);
                if (composition != null) ReagentBottle.RestoreComposition(item, composition);
                int remaining = GraphUtils.TryAcceptAll(target ?? inventory, item, count);
                if (remaining == 0 || (remaining == count && ItemRegistration.TryUseCompactContainerFootprint(item)
                    && GraphUtils.TryAcceptAll(target ?? inventory, item, count) == 0))
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
        private static void NotifyNotAllowed(GameItem item)
        {
            if (UnityEngine.Time.unscaledTime - lastNotAllowedNotifyTime < 2f)
            {
                return;
            }

            lastNotAllowedNotifyTime = UnityEngine.Time.unscaledTime;
            ChemistryMod.Log?.Warning("实验室储物格拒绝物品：" + item.identifier);
            DayHudBridge.Push(
                Loc.Get("lab_grid_unsupported_item",
                    "实验页接收化学物品、实验设备、样品、废金属和电池。其他物品请放进仓库页。",
                    "The lab page accepts chemistry items, equipment, samples, scrap metal and batteries. Store other items in a warehouse."),
                "warn", 4f);
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
                // 电源走单独明细，化学物品计数只包含白名单物品。
                if (IsPowerItem(item))
                {
                    continue;
                }
                counts.TryGetValue(item.identifier, out int count);
                counts[item.identifier] = count + item.unitCount;
            }
            return counts;
        }

        internal static bool EqualCounts(Dictionary<string, int> left, Dictionary<string, int> right)
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
            foreach (StorageInventory grid in PageGrids())
            {
                foreach (GameItem item in grid.childItems.ToArray())
                {
                    item.Destroy();
                }
            }
            window.Destroy();
        }
    }
}
