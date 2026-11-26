using System;

namespace Chemistry
{
    /// <summary>
    /// Keeps Chemistry item values within the base game's ordinary material price range.
    /// </summary>
    internal static class ItemPricing
    {
        internal const int MaxSingleSaleValue = 350;
        private static readonly int MediumFluidUnits = ReagentBottle.UnitsForSize(ReagentBottle.Medium);

        internal static int ElementUnitValue(ElementDef element)
        {
            if (element == null || element.Mass <= 0 || double.IsNaN(element.Mass) || double.IsInfinity(element.Mass))
            {
                return 1;
            }

            if (element.Value <= 0)
            {
                return 0;
            }

            double perGram = element.Value / element.Mass;
            int rounded = (int)Math.Round(perGram, MidpointRounding.AwayFromZero);
            return Math.Max(1, Math.Min(MaxSingleSaleValue, rounded));
        }

        internal static int ToolUnitValue(ToolDef tool) => ClampPositive(tool?.Value ?? 0);

        internal static int CompoundItemValue(CompoundDef compound) => ClampPositive(compound?.Value ?? 0);

        internal static int CompoundDisplayValue(CompoundDef compound)
        {
            int baseValue = CompoundItemValue(compound);
            if (compound == null || (!ReagentBottle.IsLiquid(compound.ItemId) && !ReagentBottle.IsGas(compound.ItemId)))
            {
                return baseValue;
            }

            return FluidUnitValue(baseValue, MediumFluidUnits) * MediumFluidUnits;
        }

        internal static int BaseUnitValue(string itemId, long fallback)
        {
            ElementDef element = ChemistryCatalog.FindElementByItemId(itemId);
            if (element != null)
            {
                return ElementUnitValue(element);
            }

            ToolDef tool = FindToolByItemId(itemId);
            if (tool != null)
            {
                return ToolUnitValue(tool);
            }

            CompoundDef compound = ChemistryCatalog.FindCompoundByItemId(itemId);
            if (compound != null)
            {
                return CompoundItemValue(compound);
            }

            return ClampPositive(fallback);
        }

        internal static void Attach(GameItem item)
        {
            if (item == null)
            {
                return;
            }

            Sync(item);
            item.onChangeAmountItemFunc += Sync;
        }

        internal static void Sync(GameItem item)
        {
            if (item == null)
            {
                return;
            }

            int baseValue = BaseUnitValue(item.identifier, item.unitBaseValue);
            if (ReagentBottle.IsLiquid(item.identifier) || ReagentBottle.IsGas(item.identifier))
            {
                int capacity = Math.Max(1, ReagentBottle.CapacityFor(item));
                int unitValue = FluidUnitValue(baseValue, capacity);
                if (item.unitCount > capacity)
                {
                    unitValue = Math.Min(unitValue, MaxSingleSaleValue / item.unitCount);
                }
                item.unitValue = unitValue;
                return;
            }

            int count = Math.Max(1, item.unitCount);
            int saleUnitLimit = MaxSingleSaleValue / count;
            item.unitValue = Math.Min(baseValue, saleUnitLimit);
        }

        internal static int StackLimitFor(GameItem item)
        {
            if (item == null)
            {
                return LabStorage.MaxCountPerSlot;
            }

            int unitValue = BaseUnitValue(item.identifier, item.unitBaseValue);
            return ItemMass.MaxStack(ItemMass.UnitMass(item.identifier), unitValue);
        }

        private static int FluidUnitValue(int baseValue, int capacity)
        {
            if (baseValue <= 0 || capacity <= 0)
            {
                return 0;
            }

            // Catalog values describe a medium bottle; larger vessels keep its unit rate until
            // the full vessel reaches the same cap as any other Chemistry item.
            int referenceUnitValue = Math.Max(1, baseValue / MediumFluidUnits);
            int vesselUnitLimit = MaxSingleSaleValue / capacity;
            return Math.Min(referenceUnitValue, vesselUnitLimit);
        }

        private static int ClampPositive(long value)
        {
            if (value <= 0)
            {
                return 0;
            }

            return (int)Math.Min(MaxSingleSaleValue, value);
        }

        private static ToolDef FindToolByItemId(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            ToolDef[] tools = ChemistryCatalog.Tools;
            for (int i = 0; i < tools.Length; i++)
            {
                if (string.Equals(tools[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    return tools[i];
                }
            }

            return null;
        }
    }
}
