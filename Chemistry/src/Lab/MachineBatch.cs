using System;
using System.Collections.Generic;

namespace Chemistry
{
    internal static class MachineBatch
    {
        internal readonly struct Product
        {
            internal readonly string Id;
            internal readonly GameInventory Destination;
            internal readonly int Units;
            internal readonly int BottleSize;
            internal readonly string Composition;

            internal Product(string id, GameInventory destination, int units = 1, int bottleSize = -1, string composition = null)
            {
                Id = id;
                Destination = destination;
                Units = units;
                BottleSize = bottleSize;
                Composition = composition;
            }
        }

        private static GameItem placingItem;
        private static GameInventory placingInventory;

        internal static bool IsPlacing(GameItem item, GameInventory inventory) =>
            item == placingItem && inventory == placingInventory;

        internal static bool AddInputs(List<(GameItem Item, int Units)> inputs, GameInventory source, string id, int units)
        {
            foreach (GameItem item in source.childItems)
            {
                if (item.identifier != id)
                {
                    continue;
                }
                int used = Math.Min(units, item.unitCount);
                inputs.Add((item, used));
                units -= used;
                if (units == 0)
                {
                    return true;
                }
            }
            return false;
        }

        internal static bool TryCommit(LabInventoryGrid grid, List<(GameItem Item, int Units)> inputs,
            params (string Id, GameInventory Destination)[] outputs)
        {
            var products = Array.ConvertAll(outputs, output => new Product(output.Id, output.Destination));
            return TryCommitMeasured(grid, inputs, products);
        }

        internal static bool TryCommitBottled(LabInventoryGrid grid, List<(GameItem Item, int Units)> inputs,
            GameItem bottle, string product, GameInventory destination) =>
            TryCommitMeasured(grid, inputs, new Product(product, destination, bottleSize: ReagentBottle.SizeOf(bottle)));

        internal static bool TryCommitMeasured(LabInventoryGrid grid, List<(GameItem Item, int Units)> inputs,
            params Product[] outputs)
        {
            var consumed = new List<(GameItem Item, GameInventory Source, int OriginalUnits)>();
            var produced = new List<(GameItem Item, GameItem Stack, int OriginalUnits)>();
            bool committed = false;
            try
            {
                foreach (var (item, units) in inputs)
                {
                    GameInventory source = item.parentInventory;
                    if (source == null || units <= 0 || item.unitCount < units)
                    {
                        return false;
                    }
                    consumed.Add((item, source, item.unitCount));
                    if (item.unitCount == units)
                    {
                        if (!source.Expel(item))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        item.SetUnitCount(item.unitCount - units);
                    }
                }

                foreach (Product output in outputs)
                {
                    GameInventory destination = output.Destination;
                    if (string.IsNullOrEmpty(output.Id) || destination == null || output.Units <= 0)
                    {
                        return false;
                    }
                    GameItem product = ModHelper.SpawnItem(output.Id);
                    if (product == null)
                    {
                        return false;
                    }
                    produced.Add((product, null, 0));
                    ModHelper.SetItemOwnerShip(product, true);
                    if (output.BottleSize >= 0)
                    {
                        ReagentBottle.SetSize(product, output.BottleSize);
                    }
                    product.SetUnitCount(output.Units);
                    if (output.Composition != null) ReagentBottle.RestoreComposition(product, output.Composition);
                    placingItem = product;
                    placingInventory = destination;
                    SlotMarker slot = destination.TryFindOneValidInventorySlot(product);
                    if (slot == null || slot.inventory != destination
                        || slot.numTransfer < output.Units || product.MaxNumRemove() < output.Units)
                    {
                        return false;
                    }
                    GameItem stack = slot.targetItem;
                    produced[produced.Count - 1] = (product, stack, stack?.unitCount ?? 0);
                    if (slot.TryAcceptOnce(output.Units) != output.Units)
                    {
                        return false;
                    }
                }

                committed = grid == null || grid.TrySaveMachineBatch();
                return committed;
            }
            catch (Exception error)
            {
                ChemistryMod.Log?.Warning("实验室设备结算失败：" + error);
                return false;
            }
            finally
            {
                placingItem = null;
                placingInventory = null;
                for (int i = produced.Count - 1; i >= 0; i--)
                {
                    var (item, stack, originalUnits) = produced[i];
                    if (!committed && stack != null)
                    {
                        stack.SetUnitCount(originalUnits);
                    }
                    if (!committed || item.parentInventory == null)
                    {
                        item.Destroy();
                    }
                }
                foreach (var (item, source, originalUnits) in consumed)
                {
                    if (committed && item.parentInventory == null)
                    {
                        item.Destroy();
                    }
                    else if (!committed)
                    {
                        item.SetUnitCount(originalUnits);
                        if (item.parentInventory == null && !source.UncheckedAccept(item))
                        {
                            ChemistryMod.Log?.Error("设备未能恢复原槽位：" + item.identifier);
                        }
                    }
                }
            }
        }
    }
}
