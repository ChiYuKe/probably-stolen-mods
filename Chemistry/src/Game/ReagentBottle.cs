using System;

namespace Chemistry
{
    internal static class ReagentBottle
    {
        internal const string EmptyId = ChemistryCatalog.ItemPrefix + "empty_reagent_bottle";
        internal const string EmptySpriteKey = "empty_reagent_bottle";
        internal const string PlasticSpriteKey = "empty_plastic_reagent_bottle";
        internal const string PlasticEmptyId = ChemistryCatalog.ItemPrefix + PlasticSpriteKey;
        internal const string DamagedGlassId = ChemistryCatalog.ItemPrefix + "damaged_glass_reagent_bottle";
        internal const string DamagedGasId = ChemistryCatalog.ItemPrefix + "damaged_gas_vial";
        internal const string DamagedPlasticId = ChemistryCatalog.ItemPrefix + "damaged_plastic_reagent_bottle";
        internal const int MillilitersPerUnit = 25;
        internal const int Medium = 1;
        internal const int Bulk = 3;
        internal static readonly string[] GlassEmpties = { EmptyId + "_small", EmptyId, EmptyId + "_large" };
        internal static readonly string[] PlasticEmpties = { PlasticEmptyId + "_small", PlasticEmptyId, PlasticEmptyId + "_large" };
        private const string GasEmptyId = ChemistryCatalog.ItemPrefix + "empty_gas_vial";
        internal static readonly string[] GasEmpties = { GasEmptyId + "_small", GasEmptyId, GasEmptyId + "_large" };
        internal static readonly string[][] EmptyFamilies = { GlassEmpties, PlasticEmpties, GasEmpties };
        internal static readonly string[] DamagedIds = { DamagedGlassId, DamagedPlasticId, DamagedGasId };
        internal static readonly string[] SizeSuffixes = { "small", "medium", "large" };
        private const string GlassCorrosiveKey = "cmp_hydrofluoric_acid";
        private static readonly TimedReaction DirectReaction = new TimedReaction();

        internal static bool IsLiquid(string id)
        {
            return TryGetFluid(id, out int? color) && color.HasValue;
        }

        internal static bool IsGas(string id) => TryGetFluid(id, out int? color) && !color.HasValue;

        private static bool TryGetFluid(string id, out int? color)
        {
            color = null;
            return id != null && id.StartsWith(ChemistryCatalog.ItemPrefix, StringComparison.Ordinal)
                && MaterialItemArt.TryGetFluid(id.Substring(ChemistryCatalog.ItemPrefix.Length), out color);
        }

        internal static int SizeOf(string spriteKey)
        {
            if (spriteKey?.EndsWith("_small", StringComparison.Ordinal) == true) return 0;
            if (spriteKey?.EndsWith("_large", StringComparison.Ordinal) == true) return 2;
            if (spriteKey?.EndsWith("_bulk", StringComparison.Ordinal) == true
                || spriteKey?.EndsWith("_bulk_3x3", StringComparison.Ordinal) == true) return Bulk;
            return Medium;
        }

        internal static int UnitsForSize(int size) => 4 << size;

        internal static int RestoreSavedSize(string id, int size, int units)
        {
            // Older versions saved the enlarged drum sprite as a medium bottle.
            return size == Medium && units > UnitsForSize(2) && units <= BulkContainers.DrumCapacity && IsLiquid(id)
                ? Bulk : size;
        }

        internal static int DefaultSize(string id) =>
            LabMixtureData.IsDirectBatch(id) ? LabMixtureData.SmallBottle
            : id?.StartsWith(ChemistryCatalog.ItemPrefix, StringComparison.Ordinal) == true
                ? SizeOf(MaterialItemArt.SpriteKeyFor(id.Substring(ChemistryCatalog.ItemPrefix.Length))) : Medium;

        internal static int SizeOf(GameItem item) =>
            item.spritePath?.StartsWith("lab_", StringComparison.Ordinal) == true
                ? SizeOf(item.spritePath) : DefaultSize(item.identifier);

        internal static int CapacityFor(string id) => CapacityFor(id, DefaultSize(id));

        internal static int CapacityFor(GameItem item) => CapacityFor(item.identifier, SizeOf(item));

        internal static int CapacityFor(string id, int size)
        {
            if (BulkContainers.IsSolidWaste(id)) return BulkContainers.SolidWasteCapacity;
            if (BulkContainers.IsLiquidWaste(id)) return BulkContainers.LiquidWasteCapacity;
            if (size != Bulk) return UnitsForSize(size);
            return IsGas(id) || IsGasEmpty(id) ? BulkContainers.SteelCapacity : BulkContainers.DrumCapacity;
        }

        /// <summary>Whole content units the target can still receive.</summary>
        internal static int RemainingCapacity(GameItem target)
        {
            if (target == null || IsDamaged(target.identifier) || !IsBottle(target.identifier)) return 0;
            return Math.Max(0, CapacityFor(target) - Contents(target));
        }

        internal static int GasCapacity(GameItem vial) => Math.Max(CapacityFor(vial), vial.unitCount);

        internal static bool IsEmpty(string id) => IsLiquidEmpty(id) || IsGasEmpty(id);

        internal static bool IsLiquidEmpty(string id) => IsGlassEmpty(id) || IsPlasticEmpty(id) || id == BulkContainers.DrumId;

        internal static bool IsGasEmpty(string id) => id == BulkContainers.SteelId || Array.IndexOf(GasEmpties, id) >= 0;

        internal static bool IsGlassEmpty(string id) => Array.IndexOf(GlassEmpties, id) >= 0;

        internal static bool IsPlasticEmpty(string id) => Array.IndexOf(PlasticEmpties, id) >= 0;

        internal static bool IsDamaged(string id) => id == DamagedGlassId || id == DamagedGasId || id == DamagedPlasticId;

        internal static bool IsEmptyTarget(string id) => IsEmpty(id) || BulkContainers.IsEmptyWaste(id);

        internal static bool IsSingleBottle(string id) => IsEmptyTarget(id) || IsDamaged(id) || LabMixtureData.IsDirectBatch(id);

        internal static bool IsBottle(string id) => IsSingleBottle(id) || IsLiquid(id) || IsGas(id) || BulkContainers.IsWaste(id);

        internal static bool RequiresPlastic(string spriteKey) => spriteKey == GlassCorrosiveKey || spriteKey == LabMixtureData.UnknownPlasticKey;

        private static bool UsesPlastic(string liquidId) => liquidId == LabMixtureData.UnknownPlastic
            || liquidId == ChemistryCatalog.ItemPrefix + GlassCorrosiveKey;

        internal static string EmptyFor(string liquidId, int size = Medium)
        {
            if (IsGas(liquidId))
            {
                return size == Bulk ? BulkContainers.SteelId : GasEmpties[size];
            }
            if (size == Bulk) return BulkContainers.DrumId;
            return (UsesPlastic(liquidId) ? PlasticEmpties : GlassEmpties)[size];
        }

        internal static string ReturnedEmpty(GameItem source) =>
            IsLiquid(source.identifier) || IsGas(source.identifier) ? EmptyFor(source.identifier, SizeOf(source)) : null;

        internal static bool DamagesGlass(string liquidId, string bottleId) =>
            liquidId == ChemistryCatalog.ItemPrefix + GlassCorrosiveKey && IsGlassEmpty(bottleId);

        private static bool ContainsGlassCorrosiveLiquid(GameItem source)
        {
            string corrosive = ChemistryCatalog.ItemPrefix + GlassCorrosiveKey;
            return source.identifier == corrosive || (LiquidComposition.IsMixture(source.identifier)
                && LiquidComposition.Measure(source, source.unitCount).ContainsKey(corrosive));
        }

        internal static bool DamagesGlass(GameItem source, GameItem target) =>
            (IsGlassEmpty(target.identifier) || (IsLiquid(target.identifier) && SizeOf(target) != Bulk && !UsesPlastic(target.identifier)))
            && ContainsGlassCorrosiveLiquid(source);

        internal static bool AcceptsEmpty(string liquidId, string bottleId) =>
            (IsGlassEmpty(bottleId) && liquidId != LabMixtureData.UnknownPlastic) || bottleId == BulkContainers.DrumId
            || (UsesPlastic(liquidId) && IsPlasticEmpty(bottleId));

        internal static bool CanReceiveLiquid(GameItem source, GameItem target)
        {
            string id = target.identifier;
            if (BulkContainers.IsLiquidWaste(id) || id == BulkContainers.DrumId) return true;
            if (!LiquidComposition.IsMixture(source.identifier))
                return IsLiquidEmpty(id) ? AcceptsEmpty(source.identifier, id) : IsLiquid(id);
            if (IsPlasticEmpty(id) || (IsLiquid(id) && (UsesPlastic(id) || SizeOf(target) == Bulk))) return true;
            if (!IsGlassEmpty(id) && !IsLiquid(id)) return false;
            return source.identifier != LabMixtureData.UnknownPlastic || ContainsGlassCorrosiveLiquid(source)
                || !LiquidComposition.Measure(source, source.unitCount).ContainsKey(LiquidComposition.Unidentified);
        }

        internal static string FilledResult(string liquidId, string bottleId) => bottleId switch
        {
            BulkContainers.EmptyLiquidWasteId or BulkContainers.LiquidWasteId => BulkContainers.LiquidWasteId,
            BulkContainers.EmptySolidWasteId or BulkContainers.SolidWasteId => BulkContainers.SolidWasteId,
            _ => DamagesGlass(liquidId, bottleId) ? DamagedGlassId : liquidId,
        };

        internal static string DamagedName => Loc.Pick("破损玻璃试剂瓶", "Damaged glass reagent bottle");

        internal static string DamagedNameFor(string id) => id switch
        {
            DamagedGasId => Loc.Pick("破损气体采样瓶", "Damaged gas sampling vial"),
            DamagedPlasticId => Loc.Pick("破损塑料试剂瓶", "Damaged plastic reagent bottle"),
            _ => DamagedName,
        };

        internal static string DamagedSpriteFor(string id, int size = Medium)
        {
            if (id == DamagedGasId && size == 2)
            {
                return MaterialItemArt.TallDamagedGasVialSprite;
            }
            string family = id switch
            {
                DamagedGasId => "lab_damaged_gas_vial_",
                DamagedPlasticId => "lab_damaged_plastic_bottle_",
                _ => "lab_damaged_glass_bottle_",
            };
            return family + SizeSuffixes[size];
        }

        internal static string EmptyName(string id)
        {
            if (id == BulkContainers.SteelId || id == BulkContainers.DrumId) return BulkContainers.Name(id);
            int size = SizeOf(id);
            string name = IsGasEmpty(id) ? Loc.Pick("空采样气瓶", "Empty gas sampling vial")
                : IsPlasticEmpty(id) ? Loc.Pick("空塑料试剂瓶", "Empty plastic reagent bottle")
                : Loc.Pick("空玻璃试剂瓶", "Empty glass reagent bottle");
            string suffix = size == 0 ? Loc.Pick("小号", "Small")
                : size == 2 ? Loc.Pick("大号", "Large") : Loc.Pick("中号", "Medium");
            return name + " · " + suffix;
        }

        internal static void SetSize(GameItem item, int size)
        {
            string sprite = MaterialItemArt.SizedSprite(item.identifier, size);
            if (sprite != null)
            {
                ItemRegistration.SetContainerSprite(item, sprite);
            }
        }

        internal static void AddAmountTooltip(RichTextBuilder builder, GameItem item)
        {
            string id = item.identifier;
            bool gas = IsGas(id) || IsGasEmpty(id);
            bool damaged = IsDamaged(id);
            bool solidWaste = BulkContainers.IsSolidWaste(id);
            if (!IsBottle(id))
            {
                return;
            }

            int current = Contents(item);
            int max = damaged ? 0 : gas ? GasCapacity(item) : CapacityFor(item);
            long volume = (long)current * MillilitersPerUnit;
            long maxVolume = (long)max * MillilitersPerUnit;
            string text = damaged
                ? Loc.Pick("有效容量：0 mL（破损，不可灌装）", "Usable capacity: 0 mL (damaged; cannot be refilled)")
                : solidWaste
                    ? Loc.Get("solid_waste_amount_tooltip", "固体废物：{0} / {1} 件", "Solid waste: {0} / {1} items", current, max)
                : gas
                    ? Loc.Get("gas_volume_tooltip", "气体含量：{0} / {1} mL", "Gas contents: {0} / {1} mL", volume, maxVolume)
                    : Loc.Get("bottle_volume_tooltip", "液体含量：{0} / {1} mL", "Liquid contents: {0} / {1} mL", volume, maxVolume);
            float ratio = max > 0 ? Math.Max(0f, Math.Min(1f, (float)current / max)) : 0f;
            builder.AddProgressBar(ratio, 10,
                RenderHandler.ColorPalette.LightCyan, RenderHandler.ColorPalette.VeryDarkCyan, null, " ", true, false);
            builder.AddLine(text, true, damaged ? RenderHandler.ColorPalette.Gray : RenderHandler.ColorPalette.LightCyan,
                false, false, false, false,
                RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None, RenderHandler.ColorPalette.None);
            if (LabTitration.IsProduct(id))
            {
                builder.AddLine(LabTitration.Describe(LabTitration.Read(item, current)), false);
            }
            else if (LiquidComposition.IsMixture(id))
            {
                LiquidComposition.AddTooltip(builder, item);
            }
        }

        internal static string SavedComposition(GameItem item) => LiquidComposition.IsMixture(item.identifier)
            ? LiquidComposition.Saved(item) : LabTitration.SavedComposition(item);

        internal static void RestoreComposition(GameItem item, string composition)
        {
            if (composition == null) return;
            if (LiquidComposition.IsMixture(item.identifier)) LiquidComposition.Restore(item, composition);
            else LabTitration.RestoreComposition(item, composition);
        }

        internal static int Contents(GameItem item) => IsDamaged(item.identifier) || IsEmptyTarget(item.identifier) ? 0
            : LabMixtureData.IsDirectBatch(item.identifier) ? LabMixtureData.Portion * 2 : item.unitCount;

        internal static void Attach(GameItem bottle)
        {
            bottle.mayStackItemFunc = (_, _) => false;
            bottle.maxSlotAmountItemFunc = (item, _, _) => Math.Max(CapacityFor(item), item.unitCount);
            if (LabTitration.IsProduct(bottle.identifier) || LiquidComposition.IsMixture(bottle.identifier))
            {
                // Native partial placement spawns a default item and would lose the measured composition.
                bottle.slotPriorityFunc = (item, _, slot) =>
                {
                    if (slot.numTransfer < item.unitCount) slot.numTransfer = 0;
                    return 0;
                };
            }
            AttachTransfer(bottle);
        }

        internal static void AttachTransfer(GameItem bottle)
        {
            bottle.canUseOutsideCombat = true;
            bottle.mayThisTargetItemFunc += IsPourTarget;
            bottle.canThisTargetItemFunc += CanPour;
            bottle.onThisTargetItemFunc += (source, target) =>
                ModHelper.Delay(ChemistryCatalog.ModId, 0.05f, () => PourByHand(source, target));
        }

        internal static void AttachSolidWasteTarget(GameItem bin)
        {
            bin.mayItemTargetThisFunc += IsSolidWasteTransfer;
            bin.canItemTargetThisFunc += (source, target) => IsSolidWasteTransfer(source, target) && CanPour(source, target);
            bin.onItemTargetThisFunc += (source, target) =>
            {
                if (IsSolidWasteTransfer(source, target))
                {
                    ModHelper.Delay(ChemistryCatalog.ModId, 0.05f, () => Pour(source, target));
                }
            };
        }

        private static bool IsSolidWasteTransfer(GameItem source, GameItem target) =>
            BulkContainers.AcceptsSolid(source?.identifier) && BulkContainers.IsSolidWaste(target?.identifier);

        private static bool IsPourTarget(GameItem source, GameItem target)
        {
            if (source == null || target == null || source == target || source.unitCount <= 0) return false;
            if (IsLiquid(source.identifier))
            {
                return CanReceiveLiquid(source, target);
            }
            if (IsGas(source.identifier))
            {
                return IsGasEmpty(target.identifier) || (target.identifier == source.identifier &&
                    (SizeOf(source) == Bulk || SizeOf(target) == Bulk));
            }
            return IsSolidWasteTransfer(source, target);
        }

        private static bool CanPour(GameItem source, GameItem target)
        {
            return IsPourTarget(source, target)
                && IsPourGrid(source.parentInventory) && IsPourGrid(target.parentInventory)
                && source.MayRemove() && target.MayRemove()
                && (IsEmptyTarget(target.identifier)
                    ? target.unitCount == 1
                    : RemainingCapacity(target) > 0)
                && GeneralHelper.IsItemEffectivelyOwned(source) && GeneralHelper.IsItemEffectivelyOwned(target);
        }

        private static bool IsPourGrid(GameInventory inventory) =>
            inventory is GameGridInventory || (inventory != null && inventory == LabInventoryGrid.OpenGrid?.StorageInventoryArea);

        internal static int TransferUnits(GameItem source, GameItem target, int limit)
        {
            if (source == null || target == null || limit <= 0 || source.unitCount <= 0)
            {
                return 0;
            }

            return Math.Min(limit, Math.Min(source.unitCount, RemainingCapacity(target)));
        }

        private static void PourByHand(GameItem source, GameItem target)
        {
            if (!CanPour(source, target)) return;
            int dose = TransferUnits(source, target, int.MaxValue);
            string reactionName = Loc.Pick("液体混合", "Mixing liquids");
            int ticks = 0;
            if (LabTitration.CanCombine(source.identifier, target.identifier)
                && LabTitration.Combine(LabTitration.Read(target, target.unitCount), LabTitration.Read(source, dose),
                    target.unitCount + dose).Reacted > 0m)
            {
                reactionName = Loc.Pick("酸碱中和", "Neutralization");
                ticks = LabTitration.ReactionTicks;
            }
            else if (IsLiquid(target.identifier) && HasReactiveSolution(source, target) && !DamagesGlass(source, target))
            {
                var reaction = AqueousReactions.Plan(target, target.unitCount, source, dose);
                ticks = reaction.Ticks;
            }
            if (ticks == 0)
            {
                Pour(source, target);
                return;
            }

            var sourceGrid = source.parentInventory;
            var targetGrid = target.parentInventory;
            int sourceAmount = source.unitCount;
            int targetAmount = target.unitCount;
            DirectReaction.TryStart(reactionName, ticks,
                () => source.parentInventory == sourceGrid && target.parentInventory == targetGrid
                    && source.unitCount == sourceAmount && target.unitCount == targetAmount && CanPour(source, target),
                message => DayHudBridge.Push(message, "info", 1f),
                () => Pour(source, target));
        }

        private static bool HasReactiveSolution(GameItem source, GameItem target) =>
            LiquidComposition.HasAqueousContents(source) || LiquidComposition.HasAqueousContents(target);

        internal static bool Pour(GameItem source, GameItem target, int maxUnits = int.MaxValue)
        {
            if (maxUnits <= 0 || !CanPour(source, target) || ItemMouseDragHandler.current?.IsDraggingItem == true)
            {
                return false;
            }

            var lab = LabInventoryGrid.OpenGrid;
            bool targetIsEmpty = IsEmptyTarget(target.identifier);
            int sourceCapacity = CapacityFor(source);
            bool liquid = TryGetFluid(source.identifier, out int? tint) && tint.HasValue;
            int sourceAmount = source.unitCount;
            int targetAmount = targetIsEmpty ? 0 : target.unitCount;
            int targetCapacity = CapacityFor(target);
            int transferred = TransferUnits(source, target, maxUnits);
            string result = FilledResult(source.identifier, target.identifier);
            int resultAmount = targetAmount + transferred;
            bool mixed = liquid && IsLiquid(target.identifier) && source.identifier != target.identifier;
            bool measuredMix = LabTitration.CanCombine(source.identifier, target.identifier);
            bool plastic = SizeOf(target) != Bulk && (IsPlasticEmpty(target.identifier) || UsesPlastic(target.identifier));
            string composition = null;
            string observation = null;
            if (liquid && DamagesGlass(source, target))
            {
                result = DamagedGlassId;
            }
            else if (measuredMix)
            {
                var reaction = LabTitration.Combine(LabTitration.Read(target, targetAmount), LabTitration.Read(source, transferred), resultAmount);
                result = reaction.Id;
                composition = LabTitration.Encode(reaction.Contents, reaction.Volume);
            }
            else if (liquid && HasReactiveSolution(source, target) && (IsLiquid(target.identifier) || IsLiquidEmpty(target.identifier)))
            {
                var reaction = AqueousReactions.Plan(target, targetAmount, source, transferred);
                result = plastic ? LabMixtureData.UnknownPlastic : LabMixtureData.Unknown;
                if (reaction.Ticks > 0) observation = AqueousReactions.Describe(reaction.Effects);
                composition = AqueousReactions.Encode(LiquidComposition.Combine(target, targetAmount, source, transferred), reaction.Amounts);
            }
            else if (mixed)
            {
                bool glass = SizeOf(target) != Bulk && !plastic;
                (result, resultAmount) = LabMixtureData.PouredProduct(target.identifier, targetAmount,
                    source.identifier, transferred, glass, plastic);
            }
            else if (targetIsEmpty && LiquidComposition.IsMixture(result))
            {
                result = plastic ? LabMixtureData.UnknownPlastic : LabMixtureData.Unknown;
            }
            else if (targetIsEmpty && LabTitration.IsProduct(result))
            {
                composition = LabTitration.Encode(LabTitration.Read(source, sourceAmount), sourceAmount);
            }
            if (composition == null && LiquidComposition.IsMixture(result))
                composition = LiquidComposition.Combine(target, targetAmount, source, transferred);
            bool damaged = result == DamagedGlassId;
            if (damaged) resultAmount = 1;
            bool replaceTarget = targetIsEmpty || result != target.identifier || composition != null;
            bool sourceEmpties = transferred == sourceAmount;
            if (lab != null && !lab.CanPourBottles(source, target, transferred, result, resultAmount, replaceTarget))
            {
                return false;
            }

            GameItem empty = null;
            GameItem filled = null;
            GameInventory sourceGrid = source.parentInventory;
            GameInventory targetGrid = target.parentInventory;
            bool committed = false;
            try
            {
                if (sourceEmpties)
                {
                    string emptyId = ReturnedEmpty(source);
                    if (emptyId != null)
                    {
                        empty = CreateReplacement(source, emptyId, 1);
                    }
                }
                if (replaceTarget)
                {
                    filled = CreateReplacement(target, result, resultAmount);
                    RestoreComposition(filled, composition);
                }

                if ((sourceEmpties && !(empty == null ? sourceGrid.Expel(source) : Replace(source, empty, sourceGrid)))
                    || (filled != null && !Replace(target, filled, targetGrid)))
                {
                    return false;
                }

                if (!sourceEmpties) source.SetUnitCount(sourceAmount - transferred);
                if (filled == null) target.SetUnitCount(targetAmount + transferred);
                committed = lab == null || lab.TrySaveMachineBatch();
                if (!committed)
                {
                    return false;
                }
                if (sourceEmpties) source.Destroy();
                if (filled != null) target.Destroy();
                if (liquid)
                {
                    LiquidMotion.Begin(empty ?? source, sourceAmount / (float)sourceCapacity, tint.Value);
                    if (IsLiquid(result))
                    {
                        TryGetFluid(result, out int? resultTint);
                        LiquidMotion.Begin(filled ?? target, targetAmount / (float)targetCapacity, resultTint ?? tint.Value);
                    }
                    AudioHelper.PourWater();
                }
                LabPanel.RefreshFromInventory();
                if (damaged)
                {
                    OverlayHandler.current?.AppendLog(Loc.Pick("玻璃瓶被腐蚀，本次倒入的试剂已损失。",
                        "The glass bottle was damaged and the transferred reagent was lost."), false);
                }
                else if (measuredMix)
                {
                    OverlayHandler.current?.AppendLog(LabTitration.Describe(LabTitration.Read(filled, resultAmount)), false);
                }
                else if (observation != null)
                {
                    LiquidMotion.BeginReaction(filled ?? target);
                    OverlayHandler.current?.AppendLog(observation, false);
                }
                else if (mixed)
                {
                    OverlayHandler.current?.AppendLog((filled ?? target).name, false);
                }
                return true;
            }
            finally
            {
                if (!committed)
                {
                    RestoreOnFailure(source, empty, sourceGrid);
                    if (sourceEmpties && empty == null && source.parentInventory == null)
                    {
                        sourceGrid.UncheckedAccept(source);
                    }
                    RestoreOnFailure(target, filled, targetGrid);
                    if (source.unitCount != sourceAmount) source.SetUnitCount(sourceAmount);
                    if (!targetIsEmpty && target.unitCount != targetAmount) target.SetUnitCount(targetAmount);
                }
            }
        }

        private static GameItem CreateReplacement(GameItem old, string id, int amount)
        {
            GameItem item = ModHelper.SpawnItem(id);
            if (item == null)
            {
                throw new InvalidOperationException("Cannot create " + id);
            }
            ModHelper.SetItemOwnerShip(item, true);
            if (IsLiquid(id) || IsGas(id) || id == DamagedGlassId || LabMixtureData.IsDirectBatch(id))
            {
                SetSize(item, SizeOf(old));
            }
            if (ItemRegistration.PreferredContainerSprite(old.spritePath) != old.spritePath)
            {
                item.SetSprite(ChemistryCatalog.ModId, old.spritePath);
            }
            item.SetShape(old.shape);
            item.modifiedShape = old.modifiedShape.Clone();
            item.SetUnitCount(amount);
            ItemPricing.Sync(item);
            return item;
        }

        private static bool Replace(GameItem old, GameItem replacement, GameInventory grid)
        {
            return grid.Expel(old) && grid.UncheckedAccept(replacement);
        }

        private static void RestoreOnFailure(GameItem old, GameItem replacement, GameInventory grid)
        {
            if (replacement == null)
            {
                return;
            }
            replacement.Destroy();
            if (old.parentInventory == null)
            {
                grid.UncheckedAccept(old);
            }
        }
    }
}
