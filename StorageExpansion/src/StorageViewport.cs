using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StorageExpansion
{
    internal sealed class StorageViewport : IDisposable
    {
        private const int OriginalRowLimit = 10;
        private const int TilePixels = 16;
        private const int BarWidth = 8;
        private const int BarGap = 3;
        private const int RailWidth = BarGap + BarWidth;
        private const float RailInset = 1f;

        private readonly GameGridInventory grid;
        private readonly PixelWindow window;
        private readonly StorageBaseline baseline;
        private readonly TreeNodeRender background;
        private readonly RectTransform viewport;
        private readonly TreeNodeRender track;
        private readonly TreeNodeRender thumb;
        private readonly TreeNodeRender locks;
        private readonly HashSet<int> selected = new HashSet<int>();
        private HashSet<int> unlocked = new HashSet<int>();
        private CustomUIWindow purchaseWindow;
        private int scrollRow;
        private int? scrollPointer;
        private float grabOffset;
        private Texture2D lockTexture;
        private Sprite lockSprite;
        private bool locksDirty = true;

        internal StorageViewport(GameGridInventory grid, PixelWindow window)
        {
            this.grid = grid;
            this.window = window;
            baseline = ExpansionSession.Baselines.GetValue(grid, _ => new StorageBaseline());
            if (!ReferenceEquals(baseline.CurrentShape, grid.inventoryShape))
            {
                baseline.Rows = Math.Min(grid.inventoryShape.height, OriginalRowLimit);
                baseline.CurrentShape = grid.inventoryShape;
            }

            var viewportObject = new GameObject("StorageExpansion Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport = (RectTransform)viewportObject.transform;
            viewport.SetParent(window.inner.rectTransform, false);
            SetTopLeft(viewport);
            viewportObject.GetComponent<RectMask2D>().padding = new Vector4(-1f, 0f, -1f, 0f);

            track = TreeNodeRender.Instantiate().SetParent(window.inner).InitEventHandler();
            track.gameObject.name = "StorageExpansion Scrollbar";
            SetTopLeft(track.rectTransform);
            track.image.sprite = RenderHandler.LoadFromAtlas("Window/elements_outer", "border_right_scrollbar");
            track.image.enabled = true;
            track.image.alphaHitTestMinimumThreshold = 0f;
            thumb = TreeNodeRender.Instantiate().SetParent(track).InitEventHandler();
            SetTopLeft(thumb.rectTransform);
            thumb.image.sprite = RenderHandler.LoadFromAtlas("Window/elements_outer", "border_right_scrollbar_button");
            thumb.image.enabled = true;
            thumb.image.alphaHitTestMinimumThreshold = 0f;
            BindScrollInput(track, isThumb: false);
            BindScrollInput(thumb, isThumb: true);

            locks = TreeNodeRender.Instantiate().SetParent(grid.handler).InitEventHandler();
            locks.gameObject.name = "StorageExpansion Locked Cells";
            SetTopLeft(locks.rectTransform);
            locks.image.type = Image.Type.Simple;
            locks.eventHandler.mouseClickFunc.Add(ClickLockedCell);
            locks.eventHandler.mouseScrollFunc.Add(ScrollFromPointer);
            locks.gameObject.AddComponent<CustomUITooltipTrigger>().tooltipText = ExpansionSession.Text(
                "点击解锁单格。Ctrl + 点击多选，再点已选格批量解锁。",
                "Click to unlock one cell. Ctrl-click to select several, then click a selected cell to unlock them.");

            background = WindowFramePatch.GetBackground(grid);
            background.eventHandler.mouseScrollFunc.Add(ScrollFromPointer);
            grid.onSlotAddItemEndFunc += AddItemWheel;
            grid.onSlotRemoveItemFunc += RemoveItemWheel;
            foreach (GameItem item in grid.childItems)
            {
                AddItemWheel(item, grid, null);
            }
        }

        private int VisibleHeight => baseline.Rows * UISettings.current.gridSize;

        private int MaxScroll => Math.Max(0, grid.inventoryShape.height - baseline.Rows);

        private float TrackHeight => Math.Max(0f, VisibleHeight - RailInset * 2f);

        private float ThumbHeight => TrackHeight * baseline.Rows / Math.Max(1, grid.inventoryShape.height);

        private float ThumbTravel => TrackHeight - ThumbHeight;
        internal void ApplyOriginalSize(int originalRows)
        {
            baseline.Rows = Math.Min(originalRows, OriginalRowLimit);
            selected.RemoveWhere(id => !WarehouseCells.IsVisibleCell(id, grid.inventoryShape.width));
            ApplyShape();
        }

        internal void Reload()
        {
            unlocked = WarehouseCells.Read(ModHelper.GetModData(ExpansionSession.ModId, WarehouseCells.SaveKey),
                ModHelper.GetModDataInt(ExpansionSession.ModId, "warehouse"), grid.inventoryShape.width);
            selected.Clear();
            ClosePurchase();
            scrollRow = 0;
            ApplyShape();
        }

        private void ApplyShape()
        {
            WindowFramePatch.SetInventoryShape(grid, WarehouseCells.BuildShape(grid.inventoryShape.width, baseline.Rows, unlocked));
            locksDirty = true;
            window.Validate();
        }

        internal bool LayoutWindow(PixelWindow validatedWindow, TreeNodeRender windowBackground, TreeNodeRender outsideBorder,
            out int windowHeight)
        {
            windowHeight = 0;
            if (validatedWindow != window || grid.handler.parent != window.inner || viewport == null)
            {
                return false;
            }
            baseline.CurrentShape = grid.inventoryShape;

            RectTransform content = grid.rectTransform;
            if (content.parent != viewport)
            {
                // Keep TreeNodeRender's native logical parent so saving and pointer lookup still see the original window.
                content.SetParent(viewport, false);
            }

            int width = grid.widthPixels;
            int height = VisibleHeight;
            Vector2 innerOffset = window.inner.rectTransform.anchoredPosition;
            windowHeight = height + window.yPadding - Mathf.RoundToInt(innerOffset.y);
            viewport.sizeDelta = new Vector2(width, height);

            // Keep the native left edge and padding. Extend the existing frame to enclose the rail.
            int framedWidth = window.widthPixels + RailWidth;
            Frame(windowBackground.rectTransform, framedWidth, windowHeight);
            Frame(outsideBorder.rectTransform, framedWidth, windowHeight);
            track.rectTransform.anchoredPosition = new Vector2(width + BarGap, 0);
            track.rectTransform.sizeDelta = new Vector2(BarWidth, height);
            locks.rectTransform.anchoredPosition = new Vector2(0, -height);
            locks.rectTransform.sizeDelta = new Vector2(width, WarehouseCells.AdditionalRows * UISettings.current.gridSize);
            RenderLocks();
            SetScrollRow(scrollRow);
            return true;
        }

        private static void Frame(RectTransform rect, int width, int height)
        {
            rect.sizeDelta = new Vector2(width, height + rect.anchoredPosition.y);
        }

        private void SetScrollRow(int row)
        {
            int max = MaxScroll;
            scrollRow = Mathf.Clamp(row, 0, max);
            grid.rectTransform.anchoredPosition = new Vector2(0f, scrollRow * UISettings.current.gridSize);
            float top = RailInset + (max > 0 ? scrollRow * ThumbTravel / max : 0f);
            thumb.rectTransform.anchoredPosition = new Vector2(RailInset, -top);
            thumb.rectTransform.sizeDelta = new Vector2(BarWidth - RailInset * 2f, ThumbHeight);
        }

        private void BindScrollInput(TreeNodeRender node, bool isThumb)
        {
            node.eventHandler.mouseDownFunc.Add(eventData => BeginScroll(eventData, isThumb));
            node.eventHandler.mouseDragFunc.Add(DragScroll);
            node.eventHandler.mouseUpFunc.Add(EndScroll);
            node.eventHandler.mouseScrollFunc.Add(ScrollFromPointer);
        }

        private bool BeginScroll(PointerEventData eventData, bool isThumb)
        {
            if (!TryGetTrackPosition(eventData, out float position))
            {
                return false;
            }

            scrollPointer = eventData.pointerId;
            grabOffset = isThumb ? position + thumb.rectTransform.anchoredPosition.y : ThumbHeight * 0.5f;
            if (!isThumb)
            {
                SetScrollRow(RowAtPosition(position, grabOffset, ThumbTravel, MaxScroll));
            }
            return true;
        }

        private bool DragScroll(PointerEventData eventData)
        {
            if (scrollPointer != eventData.pointerId || !TryGetTrackPosition(eventData, out float position))
            {
                return false;
            }
            SetScrollRow(RowAtPosition(position, grabOffset, ThumbTravel, MaxScroll));
            return true;
        }

        private bool EndScroll(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || scrollPointer != eventData.pointerId)
            {
                return false;
            }
            scrollPointer = null;
            return true;
        }

        private bool TryGetTrackPosition(PointerEventData eventData, out float position)
        {
            position = 0f;
            if (MaxScroll == 0 || eventData.button != PointerEventData.InputButton.Left ||
                ItemMouseDragHandler.current?.IsDraggingItem == true ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(track.rectTransform, eventData.position,
                    eventData.pressEventCamera, out Vector2 local))
            {
                return false;
            }
            position = -local.y;
            return true;
        }

        private static int RowAtPosition(float position, float offset, float travel, int maxScroll) =>
            travel > 0f ? Mathf.RoundToInt(Mathf.Clamp01((position - offset - RailInset) / travel) * maxScroll) : 0;

        private bool ScrollFromPointer(PointerEventData eventData)
        {
            if (MaxScroll == 0 || ItemMouseDragHandler.current?.IsDraggingItem == true)
            {
                return false;
            }

            SetScrollRow(scrollRow - Math.Sign(eventData.scrollDelta.y + eventData.scrollDelta.x));
            return true;
        }

        private void AddItemWheel(GameItem item, GameInventory _, SlotMarker slot)
        {
            if (item is GameItemElement element)
            {
                element.background.eventHandler.mouseScrollFunc.Add(ScrollFromPointer);
            }
        }

        private void RemoveItemWheel(GameItem item, GameInventory _, SlotMarker slot)
        {
            if (item is GameItemElement element)
            {
                element.background.eventHandler.mouseScrollFunc.Remove(ScrollFromPointer);
            }
        }

        internal void RestoreOccupiedCells()
        {
            int width = grid.inventoryShape.width;
            bool changed = false;
            foreach (GameItem item in grid.childItems)
            {
                GridShape shape = item.modifiedShape;
                for (int y = Math.Max(shape.minY, baseline.Rows); y <= Math.Min(shape.maxY, grid.inventoryShape.height - 1); y++)
                {
                    for (int x = Math.Max(shape.minX, 0); x <= Math.Min(shape.maxX, width - 1); x++)
                    {
                        if (shape.Get(x, y) == 1)
                        {
                            changed |= unlocked.Add(WarehouseCells.Id(x, y - baseline.Rows));
                        }
                    }
                }
            }
            if (changed)
            {
                ApplyShape();
                SaveUnlocked();
            }
        }

        private void RenderLocks()
        {
            int columns = grid.inventoryShape.width;
            int width = columns * TilePixels;
            int height = WarehouseCells.AdditionalRows * TilePixels;
            if (lockTexture == null || lockTexture.width != width)
            {
                DestroyLockArt();
                lockTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                lockSprite = Sprite.Create(lockTexture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect);
                locks.image.sprite = lockSprite;
                locks.image.alphaHitTestMinimumThreshold = 0.01f;
                locks.image.enabled = true;
                locksDirty = true;
            }
            if (!locksDirty)
            {
                return;
            }

            var pixels = new Color32[width * height];
            for (int row = 0; row < WarehouseCells.AdditionalRows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int id = WarehouseCells.Id(column, row);
                    if (!unlocked.Contains(id))
                    {
                        PaintLock(pixels, width, column * TilePixels, height - (row + 1) * TilePixels, selected.Contains(id));
                    }
                }
            }
            lockTexture.SetPixels32(pixels);
            lockTexture.Apply(false, false);
            locksDirty = false;
        }

        private static void PaintLock(Color32[] pixels, int stride, int left, int bottom, bool isSelected)
        {
            Color32 edge = isSelected ? new Color32(143, 207, 187, 255) : new Color32(22, 20, 31, 95);
            Color32 tile = isSelected ? new Color32(48, 78, 79, 255) : new Color32(27, 25, 38, 220);
            Color32 cross = isSelected ? new Color32(202, 248, 226, 255) : new Color32(139, 171, 170, 255);
            for (int y = 0; y < TilePixels; y++)
            {
                for (int x = 0; x < TilePixels; x++)
                {
                    bool plus = (x == 7 || x == 8) && y >= 5 && y <= 10 ||
                        (y == 7 || y == 8) && x >= 5 && x <= 10;
                    bool border = x == 0 || y == 0 || x == TilePixels - 1 || y == TilePixels - 1;
                    pixels[(bottom + y) * stride + left + x] = plus ? cross : border ? edge : tile;
                }
            }
        }

        private bool ClickLockedCell(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || ItemMouseDragHandler.current?.IsDraggingItem == true ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(locks.rectTransform, eventData.position,
                    eventData.pressEventCamera, out Vector2 local))
            {
                return false;
            }
            int gridSize = UISettings.current.gridSize;
            int column = Mathf.FloorToInt(local.x / gridSize);
            int row = Mathf.FloorToInt(-local.y / gridSize);
            if (column < 0 || column >= grid.inventoryShape.width || row < 0 || row >= WarehouseCells.AdditionalRows)
            {
                return false;
            }
            int id = WarehouseCells.Id(column, row);
            if (unlocked.Contains(id))
            {
                return false;
            }

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                if (!selected.Add(id)) selected.Remove(id);
                locksDirty = true;
                RenderLocks();
            }
            else
            {
                ShowPurchase(selected.Contains(id) ? selected.ToArray() : new[] { id });
            }
            return true;
        }

        private void ShowPurchase(int[] cells)
        {
            PlayerStore player = PlayerStore.Instance;
            if (player == null)
            {
                return;
            }
            ClosePurchase();
            var builder = CustomUIManager.Window(ExpansionSession.ModId + "_warehouse", ExpansionSession.Text("解锁仓库格子", "Unlock warehouse cells"));
            if (builder == null)
            {
                return;
            }
            purchaseWindow = builder.Handle;
            builder.SetSize(390f, 193f).SetCloseOnEscape(true).Center();
            int price = cells.Length * WarehouseCells.PricePerCell;
            builder.AddLabel(ExpansionSession.Text($"解锁 {cells.Length} 格，共 {price}¢", $"Unlock {cells.Length} cells for {price}¢"));
            builder.AddLabel(ExpansionSession.Text($"当前余额 {player.GetCash()}¢", $"Balance {player.GetCash()}¢"));
            builder.AddLabel(ExpansionSession.Text("按 Ctrl 点击多选，再点已选格确认。", "Ctrl-click to select multiple cells."));
            builder.AddButton(ExpansionSession.Text("确认解锁", "Confirm unlock"), () => Purchase(player, cells, price));
            builder.Last.SetInteractable(player.GetCash() >= price);
            builder.AddButton(ExpansionSession.Text("关闭", "Close"), ClosePurchase);
            builder.Show();
        }

        private void Purchase(PlayerStore player, int[] cells, int price)
        {
            if (player != PlayerStore.Instance || player.GetCash() < price || !window.IsVisible() ||
                cells.Any(id => !WarehouseCells.IsVisibleCell(id, grid.inventoryShape.width) || unlocked.Contains(id)) ||
                ItemMouseDragHandler.current?.IsDraggingItem == true)
            {
                ClosePurchase();
                return;
            }

            try
            {
                unlocked.UnionWith(cells);
                ApplyShape();
                SaveUnlocked();
            }
            catch (Exception error)
            {
                unlocked.ExceptWith(cells);
                try
                {
                    ApplyShape();
                    SaveUnlocked();
                }
                catch (Exception rollbackError)
                {
                    ExpansionSession.Log.Error("恢复扩容前状态失败 / Storage rollback failed: " + rollbackError);
                }
                ExpansionSession.Log.Error("购买储物格失败 / Storage purchase failed: " + error);
                OverlayHandler.current?.AppendLog(ExpansionSession.Text("扩容失败，未扣款。", "Expansion failed. No credits spent."), false);
                ClosePurchase();
                return;
            }

            player.PayMoney(price);
            selected.ExceptWith(cells);
            OverlayHandler.current?.UpdateInventoryStateUI();
            ClosePurchase();
        }

        private void SaveUnlocked()
        {
            string value = WarehouseCells.Write(unlocked);
            ModHelper.SetModData(ExpansionSession.ModId, WarehouseCells.SaveKey, value);
            if (ModHelper.GetModData(ExpansionSession.ModId, WarehouseCells.SaveKey) != value)
            {
                throw new InvalidOperationException("Could not record the unlocked cells in the current save");
            }
        }

        private void ClosePurchase()
        {
            if (purchaseWindow?.IsAlive == true)
            {
                purchaseWindow.Close();
            }
            purchaseWindow = null;
        }

        public void Dispose()
        {
            ClosePurchase();
            grid.onSlotAddItemEndFunc -= AddItemWheel;
            grid.onSlotRemoveItemFunc -= RemoveItemWheel;
            if (grid.handler != null)
            {
                background.eventHandler.mouseScrollFunc.Remove(ScrollFromPointer);
                foreach (GameItem item in grid.childItems)
                {
                    RemoveItemWheel(item, grid, null);
                }
                if (window.inner != null && grid.rectTransform.parent == viewport)
                {
                    grid.handler.SetParent(window.inner);
                    grid.rectTransform.anchoredPosition = Vector2.zero;
                    int rows = baseline.Rows;
                    foreach (GameItem item in grid.childItems)
                    {
                        rows = Math.Max(rows, item.modifiedShape.maxY + 1);
                    }
                    grid.SetShape(grid.inventoryShape.width, rows);
                    window.Validate();
                }
            }
            if (viewport != null) UnityEngine.Object.Destroy(viewport.gameObject);
            if (track != null) UnityEngine.Object.Destroy(track.gameObject);
            if (locks != null) UnityEngine.Object.Destroy(locks.gameObject);
            DestroyLockArt();
        }

        private void DestroyLockArt()
        {
            if (lockSprite != null) UnityEngine.Object.Destroy(lockSprite);
            if (lockTexture != null) UnityEngine.Object.Destroy(lockTexture);
        }

        private static void SetTopLeft(RectTransform rect)
        {
            var corner = new Vector2(0f, 1f);
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = corner;
            rect.anchoredPosition = Vector2.zero;
        }

    }
}
