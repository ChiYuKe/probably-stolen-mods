using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chemistry
{
    internal static class MaterialItemArt
    {
        internal const int Size = 32;
        private const int GaugeLevels = 4;
        internal const string SmallGasVialSpriteKey = "lab_gas_vial_narrow_small";
        internal const string NarrowThermometerSpriteKey = "tool_thermometer_narrow";
        internal const string NarrowVolumetricFlaskSpriteKey = "tool_volumetric_flask_narrow";
        internal const string NarrowTestTubeSpriteKey = "tool_test_tube_narrow";
        internal const string TitrationSetTwoByThreeSpriteKey = "tool_titration_set_2x3";
        internal const string VolumetricFlaskTwoByThreeSpriteKey = "tool_volumetric_flask_2x3";
        internal const string SeparatingFunnelTwoByThreeSpriteKey = "tool_separating_funnel_2x3";
        internal const string TallGasVialSprite = "lab_tall_gas_vial_large";
        internal const string TallDamagedGasVialSprite = "lab_tall_damaged_gas_vial_large";
        private static bool thermometerSpriteReady;
        private static bool volumetricFlaskSpriteReady;
        private static bool testTubeSpriteReady;
        private static readonly Color32 Gauge = new Color32(159, 189, 175, 255);
        private sealed class LiquidMask
        {
            internal readonly Color32[] Pixels;
            internal readonly int[] Samples;
            internal readonly float Area;

            internal LiquidMask(Color32[] pixels)
            {
                Pixels = pixels;
                var samples = new List<int>();
                for (int index = 0; index < pixels.Length; index++)
                {
                    if (pixels[index].a > 0)
                    {
                        samples.Add(index);
                        Area += pixels[index].a;
                    }
                }
                Samples = samples.ToArray();
            }
        }

        private static readonly Dictionary<string, LiquidMask> LiquidMasks = new Dictionary<string, LiquidMask>();
        private static readonly Dictionary<(string Id, string Art), Sprite> MixtureSprites = new();

        internal static bool TryGetFluid(string key, out int? color)
        {
            color = key switch
            {
                LabMixtureData.OilKey => LabMixtureData.OilTint,
                LabMixtureData.UnknownKey or LabMixtureData.UnknownPlasticKey => 0x998DA8,
                LabDistillationData.EarlyKey => 0x9CB9AA,
                LabDistillationData.LateKey => 0x6D9CAA,
                LabDistillationData.MixedKey => 0x7CA4A5,
                _ => LabAnalysisData.Tint(key) ?? LabProcessData.Tint(key)
            };
            return color.HasValue || MaterialAppearance.Fluids.TryGetValue(key, out color);
        }

        internal static string AttachMixture(GameItem item)
        {
            string id = item.identifier;
            item.getItemSpriteModifierForItemFunc = self =>
            {
                bool measured = LabMixtureData.IsDirectBatch(id);
                var vessel = Bottles[measured ? ReagentBottle.SizeOf(self) : 0];
                var key = (id, vessel.Sprite);
                if (!MixtureSprites.TryGetValue(key, out Sprite sprite))
                {
                    float fill = measured ? 2f * LabMixtureData.Portion / ReagentBottle.CapacityFor(self) : 1f;
                    var pixels = new Color32[Size * Size];
                    switch (id)
                    {
                        case LabMixtureData.Layers:
                        case LabProcessData.ExtractLayers:
                        case LabProcessData.WashLayers:
                            DrawLiquid(pixels, vessel, LabMixtureData.OilTint, fill, Vector2.up);
                            DrawLiquid(pixels, vessel, 0x6D9CAA, fill * 0.5f, Vector2.up);
                            break;
                        case LabMixtureData.Settled:
                        case LabMixtureData.Crystals:
                        case LabProcessData.Saturated:
                        case LabProcessData.LoadedCrystals:
                            DrawLiquid(pixels, vessel, 0x6D9CAA, 0.5f, Vector2.up);
                            DrawLiquid(pixels, vessel, id == LabMixtureData.Settled ? 0x998467 : 0xE4DDC7, 0.16f, Vector2.up);
                            break;
                        case LabProcessData.MineralWater:
                            DrawLiquid(pixels, vessel, 0x9BB8BE, 0.5f, Vector2.up);
                            break;
                        case LabProcessData.SealedChalk:
                        case LabProcessData.SealedSalt:
                            DrawLiquid(pixels, vessel, id == LabProcessData.SealedSalt ? 0xDED8BD : 0xBEAA87, 0.22f, Vector2.up);
                            break;
                        case LabMixtureData.Brine:
                        case LabProcessData.Concentrated:
                            DrawLiquid(pixels, vessel, 0xA7BDC5, 0.5f, Vector2.up);
                            break;
                        case LabDistillationData.Feed:
                        case LabDistillationData.Remainder:
                            DrawLiquid(pixels, vessel, 0x7CA4A5, id == LabDistillationData.Feed ? fill : 0.5f, Vector2.up);
                            break;
                        default:
                            bool emulsion = id == LabMixtureData.Emulsion || id == LabProcessData.ExtractMixed
                                || id == LabProcessData.WashMixed;
                            DrawLiquid(pixels, vessel, emulsion ? 0xB9A05D : 0xA3947D, emulsion ? 1f : 0.5f, Vector2.up);
                            break;
                    }
                    sprite = CreateSprite(id + "_fill", pixels, Size, Size);
                    MixtureSprites.Add(key, sprite);
                }
                return new List<ItemSpriteModifier> { new ItemSpriteModifier(sprite, Vector2.zero) };
            };
            return Bottles[0].Sprite;
        }
        private static readonly (string Sprite, RectInt Chamber) SteelCylinder = (BulkContainers.SteelSprite, new RectInt(11, 16, 2, 12));
        private static readonly (string Sprite, RectInt Chamber) LiquidDrum = (BulkContainers.DrumSprite, new RectInt(24, 12, 2, 14));
        private static readonly Dictionary<string, Color32[]> VesselPixels = new Dictionary<string, Color32[]>();
        private static readonly RectInt SmallGasVialChamber = new RectInt(13, 14, 6, 9);
        private static readonly HashSet<string> ThreeByThreeToolKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "autoclave", "hotplate_stirrer", "fume_hood", "microscope",
            "distiller", "centrifuge", "chromatograph", "spectrometer",
            "ph_meter", "balance", "heating_mantle", "test_tube_rack", "conductivity_meter",
        };
        private static readonly (string Sprite, RectInt Chamber)[] Bottles =
        {
            ("lab_reagent_bottle_small", new RectInt(8, 15, 15, 11)),
            ("lab_reagent_bottle_medium", new RectInt(8, 17, 15, 11)),
            ("lab_reagent_bottle_large", new RectInt(7, 16, 18, 12)),
        };
        private static readonly (string Sprite, RectInt Chamber)[] Vials =
        {
            ("lab_gas_vial_small", new RectInt(13, 14, 6, 9)),
            ("lab_gas_vial_medium", new RectInt(12, 19, 8, 7)),
            (TallGasVialSprite, new RectInt(13, 13, 7, 13)),
        };
        private static readonly (string Sprite, RectInt Chamber)[] PlasticBottles =
        {
            ("lab_plastic_reagent_bottle_small", new RectInt(15, 17, 2, 8)),
            ("lab_plastic_reagent_bottle_medium", new RectInt(14, 17, 3, 9)),
            ("lab_plastic_reagent_bottle_large", new RectInt(14, 15, 3, 11)),
        };
        private static readonly Dictionary<(string Key, string Sprite, int Level, AqueousReactions.Observation Effects), Sprite> FillSprites = new();

        internal static void PrepareFootprintSprites()
        {
            LoadedMod mod = ModLoader.Mods.Find(entry => entry.Manifest.ID == ChemistryCatalog.ModId);
            if (mod == null)
            {
                return;
            }

            if (PrepareNarrowSprite(mod, "lab_gas_vial_small", SmallGasVialSpriteKey))
            {
                Vials[0] = (SmallGasVialSpriteKey, SmallGasVialChamber);
            }

            thermometerSpriteReady = PrepareNarrowSprite(mod, "tool_thermometer", NarrowThermometerSpriteKey);
            volumetricFlaskSpriteReady = PrepareTwoByThreeToolSprite(mod, "tool_volumetric_flask", VolumetricFlaskTwoByThreeSpriteKey);
            testTubeSpriteReady = PrepareNarrowSprite(mod, "tool_test_tube", NarrowTestTubeSpriteKey);
            foreach (string key in ThreeByThreeToolKeys)
            {
                PrepareLargeToolSprite(mod, "tool_" + key, "tool_" + key + "_3x3");
            }
            PrepareTwoByThreeToolSprite(mod, "tool_titration_set", TitrationSetTwoByThreeSpriteKey);
            PrepareTwoByThreeToolSprite(mod, "tool_separating_funnel", SeparatingFunnelTwoByThreeSpriteKey);
            PrepareThreeByThreeContainerSprite(mod, BulkContainers.DrumSourceSprite, BulkContainers.DrumSprite);
            PrepareThreeByThreeContainerSprite(mod, BulkContainers.LiquidWasteSourceSprite, BulkContainers.LiquidWasteSprite);
            PrepareThreeByThreeContainerSprite(mod, BulkContainers.SolidWasteSourceSprite, BulkContainers.SolidWasteSprite);
        }

        private static bool PrepareThreeByThreeContainerSprite(LoadedMod mod, string sourceKey, string targetKey)
        {
            if (!(mod.Sprites is IDictionary<string, Sprite> sprites))
            {
                return false;
            }
            if (sprites.TryGetValue(targetKey, out Sprite existing))
            {
                return existing != null && existing.texture.width == Size * 3 / 2
                    && existing.texture.height == Size * 3 / 2;
            }
            if (!sprites.TryGetValue(sourceKey, out Sprite source) || source == null || !source.texture.isReadable
                || source.texture.width != Size * 2 || source.texture.height != Size * 2)
            {
                return false;
            }

            const int width = Size * 3 / 2;
            Color32[] sourcePixels = source.texture.GetPixels32();
            var scaled = new Color32[width * width];
            for (int y = 0; y < width; y++)
            {
                int sourceY = y * source.texture.height / width;
                for (int x = 0; x < width; x++)
                {
                    scaled[y * width + x] = sourcePixels[sourceY * source.texture.width + x * source.texture.width / width];
                }
            }

            sprites[targetKey] = CreateSprite(targetKey, scaled, width, width);
            return true;
        }

        private static bool PrepareLargeToolSprite(LoadedMod mod, string sourceKey, string targetKey)
        {
            if (!(mod.Sprites is IDictionary<string, Sprite> sprites))
            {
                return false;
            }
            if (sprites.TryGetValue(targetKey, out Sprite existing))
            {
                return existing != null && existing.texture.width == Size * 3 / 2 && existing.texture.height == Size * 3 / 2;
            }
            if (!sprites.TryGetValue(sourceKey, out Sprite source) || source == null || !source.texture.isReadable
                || source.texture.width != Size || source.texture.height != Size)
            {
                return false;
            }

            const int width = Size * 3 / 2;
            Color32[] sourcePixels = source.texture.GetPixels32();
            var enlarged = new Color32[width * width];
            for (int y = 0; y < width; y++)
            {
                int sourceY = y * Size / width;
                for (int x = 0; x < width; x++)
                {
                    enlarged[y * width + x] = sourcePixels[sourceY * Size + x * Size / width];
                }
            }

            sprites[targetKey] = CreateSprite(targetKey, enlarged, width, width);
            return true;
        }

        private static bool PrepareTwoByThreeToolSprite(LoadedMod mod, string sourceKey, string targetKey)
        {
            if (!(mod.Sprites is IDictionary<string, Sprite> sprites))
            {
                return false;
            }
            if (sprites.TryGetValue(targetKey, out Sprite existing))
            {
                return existing != null && existing.texture.width == Size && existing.texture.height == Size * 3 / 2;
            }
            if (!sprites.TryGetValue(sourceKey, out Sprite source) || source == null || !source.texture.isReadable
                || source.texture.width != Size || source.texture.height != Size)
            {
                return false;
            }

            const int width = Size;
            const int height = Size * 3 / 2;
            Color32[] sourcePixels = source.texture.GetPixels32();
            var scaled = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int sourceY = y * Size / height;
                Array.Copy(sourcePixels, sourceY * Size, scaled, y * width, width);
            }

            sprites[targetKey] = CreateSprite(targetKey, scaled, width, height);
            return true;
        }

        internal static string ToolSpriteKey(string key)
        {
            string source = "tool_" + key;
            if (key == "titration_set")
            {
                LoadedMod titrationMod = ModLoader.Mods.Find(entry => entry.Manifest.ID == ChemistryCatalog.ModId);
                return titrationMod != null && titrationMod.Sprites.ContainsKey(TitrationSetTwoByThreeSpriteKey)
                    ? TitrationSetTwoByThreeSpriteKey : source;
            }
            if (key == "separating_funnel")
            {
                LoadedMod funnelMod = ModLoader.Mods.Find(entry => entry.Manifest.ID == ChemistryCatalog.ModId);
                return funnelMod != null && funnelMod.Sprites.ContainsKey(SeparatingFunnelTwoByThreeSpriteKey)
                    ? SeparatingFunnelTwoByThreeSpriteKey : source;
            }
            if (!ThreeByThreeToolKeys.Contains(key))
            {
                return source;
            }
            string enlarged = source + "_3x3";
            LoadedMod mod = ModLoader.Mods.Find(entry => entry.Manifest.ID == ChemistryCatalog.ModId);
            return mod != null && mod.Sprites.ContainsKey(enlarged) ? enlarged : source;
        }

        internal static bool IsThreeByThreeToolSprite(string key) =>
            key != null && key.EndsWith("_3x3", StringComparison.Ordinal);

        internal static string PreferredToolSprite(string key)
        {
            if (string.IsNullOrEmpty(key) || !key.StartsWith("tool_", StringComparison.Ordinal))
            {
                return key;
            }
            if (key == "tool_titration_set_3x3")
            {
                return ToolSpriteKey("titration_set");
            }
            if (key == "tool_separating_funnel_3x3")
            {
                return ToolSpriteKey("separating_funnel");
            }
            if (key == NarrowVolumetricFlaskSpriteKey)
            {
                return VolumetricFlaskSpriteKey;
            }
            string toolKey = key.Substring("tool_".Length);
            return ThreeByThreeToolKeys.Contains(toolKey) ? ToolSpriteKey(toolKey) : key;
        }

        private static bool PrepareNarrowSprite(LoadedMod mod, string sourceKey, string targetKey)
        {
            if (!(mod.Sprites is IDictionary<string, Sprite> sprites))
            {
                return false;
            }
            if (sprites.TryGetValue(targetKey, out Sprite existing))
            {
                return existing != null && existing.texture.width == Size / 2 && existing.texture.height == Size;
            }
            if (!sprites.TryGetValue(sourceKey, out Sprite source) || source == null || !source.texture.isReadable
                || source.texture.width != Size || source.texture.height != Size)
            {
                return false;
            }

            Color32[] sourcePixels = source.texture.GetPixels32();
            const int width = Size / 2;
            int sourceOffset = Size / 4;
            var cropped = new Color32[width * Size];
            for (int y = 0; y < Size; y++)
            {
                Array.Copy(sourcePixels, y * Size + sourceOffset, cropped, y * width, width);
            }

            sprites[targetKey] = CreateSprite(targetKey, cropped, width, Size);
            return true;
        }

        internal static string SpriteKeyFor(string key)
        {
            string bulk = BulkContainers.Sprite(ChemistryCatalog.ItemPrefix + key);
            if (bulk != null)
            {
                return bulk;
            }
            if (ReagentBottle.IsEmpty(ChemistryCatalog.ItemPrefix + key))
            {
                return Vessel(key, true).Sprite;
            }
            return TryGetFluid(key, out int? color)
                ? Vessel(key, color.HasValue).Sprite : key;
        }

        internal static string SizedSprite(string id, int size)
        {
            if (LabMixtureData.IsDirectBatch(id)) return Bottles[Math.Min(size, 2)].Sprite;
            if (ReagentBottle.IsDamaged(id))
            {
                return ReagentBottle.DamagedSpriteFor(id, Math.Min(size, 2));
            }
            return ReagentBottle.IsLiquid(id) || ReagentBottle.IsGas(id)
                ? Vessel(id.Substring(ChemistryCatalog.ItemPrefix.Length), ReagentBottle.IsLiquid(id), size).Sprite : null;
        }

        private static (string Sprite, RectInt Chamber) Vessel(string key, bool liquid, int size = -1)
        {
            string id = ChemistryCatalog.ItemPrefix + key;
            if (id == BulkContainers.SteelId || (size == ReagentBottle.Bulk && !liquid))
            {
                return SteelCylinder;
            }
            if (id == BulkContainers.DrumId || size == ReagentBottle.Bulk)
            {
                return LiquidDrum;
            }
            if (ReagentBottle.IsGasEmpty(id))
            {
                return Vials[ReagentBottle.SizeOf(key)];
            }
            if (ReagentBottle.IsPlasticEmpty(id) || ReagentBottle.RequiresPlastic(key))
            {
                return PlasticBottles[size < 0 ? ReagentBottle.SizeOf(key) : size];
            }
            if (ReagentBottle.IsGlassEmpty(id))
            {
                return Bottles[ReagentBottle.SizeOf(key)];
            }
            bool element = key.StartsWith("el_", StringComparison.Ordinal);
            if (liquid)
            {
                return Bottles[size >= 0 ? size : element ? 0 : key == "cmp_water" || key == "cmp_ethanol" || key == "cmp_acetone" ? 2 : 1];
            }
            return Vials[size >= 0 ? size : element ? 0 : key == "cmp_carbon_dioxide" || key == "cmp_ammonia" || key == "cmp_methane" ? 2 : 1];
        }

        internal static string Apply(GameItem item, string spriteKey)
        {
            bool contained = TryGetFluid(spriteKey, out int? liquidColor);
            bool emptyBottle = ReagentBottle.IsEmpty(item.identifier);
            if (emptyBottle)
            {
                contained = true;
                liquidColor = 0x869694;
            }
            else if (contained && liquidColor.HasValue)
            {
                ReagentBottle.Attach(item);
            }
            else if (contained)
            {
                ReagentBottle.AttachTransfer(item);
                item.maxSlotAmountItemFunc = (self, _, _) => ReagentBottle.GasCapacity(self);
                // Split and combine with the native inventory without mixing substances
                // or attaching independent volume state to a stack.
                item.mayStackItemFunc = (first, second) => first.identifier == second.identifier
                    && ReagentBottle.SizeOf(first) != ReagentBottle.Bulk && ReagentBottle.SizeOf(second) != ReagentBottle.Bulk
                    && GeneralHelper.IsItemOwned(first) == GeneralHelper.IsItemOwned(second);
            }
            int gasColor = contained && !liquidColor.HasValue ? MaterialAppearance.GasColors[spriteKey] : 0;
            var initialVessel = contained ? Vessel(spriteKey, liquidColor.HasValue) : (Sprite: spriteKey, Chamber: default(RectInt));
            item.getItemSpriteModifierForItemFunc = self =>
            {
                int capacity = contained ? ReagentBottle.CapacityFor(self) : 0;
                int steps = capacity > 16 ? 32 : capacity;
                int level = emptyBottle || !contained || self.unitCount <= 0 ? 0
                    : self.unitCount >= capacity ? steps
                    : Mathf.Clamp(Mathf.RoundToInt((float)self.unitCount / capacity * steps), 1, steps - 1);
                var vessel = contained && self.spritePath?.StartsWith("lab_", StringComparison.Ordinal) == true
                    ? Vessel(spriteKey, liquidColor.HasValue, ReagentBottle.SizeOf(self.spritePath)) : initialVessel;
                string art = self.spritePath?.StartsWith("lab_", StringComparison.Ordinal) == true ? self.spritePath : vessel.Sprite;
                var effects = liquidColor.HasValue && !emptyBottle ? AqueousReactions.VisualContents(self) : AqueousReactions.Observation.None;
                var layers = new List<ItemSpriteModifier>(1);
                if (liquidColor is int rgb && !ReagentBottle.IsGasEmpty(self.identifier) && self is GameItemElement element)
                {
                    Sprite live = LiquidMotion.Select(element, vessel, art, rgb, emptyBottle, effects, emptyBottle ? 0f :
                        Mathf.Clamp01((float)self.unitCount / capacity));
                    if (live != null)
                    {
                        layers.Add(new ItemSpriteModifier(live, Vector2.zero));
                        return layers;
                    }
                }
                if (level > 0)
                {
                    var key = (spriteKey, art, level, effects);
                    if (!FillSprites.TryGetValue(key, out Sprite fill))
                    {
                        fill = CreateFillSprite(spriteKey, vessel, liquidColor, gasColor, level, steps, art, effects);
                        FillSprites.Add(key, fill);
                    }
                    layers.Add(new ItemSpriteModifier(fill, Vector2.zero));
                }
                return layers;
            };
            return initialVessel.Sprite;
        }

        internal static bool IsNarrowGasVial(string key) => key == TallGasVialSprite || key == TallDamagedGasVialSprite;

        internal static bool IsSmallGasVial(string key) => key == SmallGasVialSpriteKey;

        internal static string ThermometerSpriteKey => thermometerSpriteReady ? NarrowThermometerSpriteKey : "tool_thermometer";

        internal static string VolumetricFlaskSpriteKey => volumetricFlaskSpriteReady
            ? VolumetricFlaskTwoByThreeSpriteKey : "tool_volumetric_flask";

        internal static string TestTubeSpriteKey => testTubeSpriteReady ? NarrowTestTubeSpriteKey : "tool_test_tube";

        internal static bool IsTallGasSprite(string key) => key == BulkContainers.SteelSprite || IsNarrowGasVial(key);

        private static Sprite CreateFillSprite(string key, (string Sprite, RectInt Chamber) vessel,
            int? liquidColor, int gasColor, int level, int capacity, string art, AqueousReactions.Observation effects)
        {
            var pixels = new Color32[Size * Size];
            if (liquidColor is int rgb)
            {
                DrawLiquid(pixels, vessel, rgb, level / (float)capacity, Vector2.up);
                DrawReaction(pixels, effects, Vector2.up, 0f);
            }
            else
            {
                DrawGas(pixels, vessel.Chamber, gasColor, level / (float)capacity);
            }
            bool wide = art == BulkContainers.DrumSprite;
            if (IsSmallGasVial(art))
            {
                const int narrowWidth = Size / 2;
                int sourceOffset = Size / 4;
                var cropped = new Color32[narrowWidth * Size];
                for (int row = 0; row < Size; row++)
                {
                    Array.Copy(pixels, row * Size + sourceOffset, cropped, row * narrowWidth, narrowWidth);
                }
                return CreateSprite(key + "_material_fill_small_" + level, cropped, narrowWidth, Size);
            }
            if (!wide && !IsTallGasSprite(art))
            {
                return CreateSprite(key + "_material_fill_" + level, pixels, Size, Size);
            }

            bool narrow = IsNarrowGasVial(art);
            int width = wide ? Size * 3 / 2 : narrow ? Size / 2 : Size;
            int height = wide ? width : Size * 2;
            var expanded = new Color32[width * height];
            ExpandPixels(pixels, expanded, width, narrow);
            return CreateSprite(key + "_material_fill_large_" + level, expanded, width, height);
        }

        internal static void ExpandPixels(Color32[] pixels, Color32[] expanded, int width, bool narrow = false)
        {
            int height = expanded.Length / width;
            int sourceOffset = narrow ? Size / 4 : 0;
            for (int row = 0; row < height; row++)
            {
                int sourceY = row * Size / height;
                for (int column = 0; column < width; column++)
                {
                    int sourceX = sourceOffset + (narrow ? column : column * Size / width);
                    expanded[row * width + column] = pixels[sourceY * Size + sourceX];
                }
            }
        }

        internal static Sprite CreateSprite(string name, Color32[] pixels, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            // The native item renderer enables alpha-hit testing on the modifier image.
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static Color32[] GetVesselPixels(string key)
        {
            if (VesselPixels.TryGetValue(key, out Color32[] pixels))
            {
                return pixels;
            }
            string sourceKey = key == SmallGasVialSpriteKey ? "lab_gas_vial_small"
                : key == SmallGasVialSpriteKey + "_fill_mask" ? "lab_gas_vial_small_fill_mask" : key;
            LoadedMod mod = ModLoader.Mods.Find(entry => entry.Manifest.ID == ChemistryCatalog.ModId);
            if (mod != null && mod.Sprites.TryGetValue(sourceKey, out Sprite sprite) && sprite.texture.isReadable
                && sprite.texture.width == Size && sprite.texture.height == Size)
            {
                pixels = sprite.texture.GetPixels32();
                VesselPixels[key] = pixels;
            }
            return pixels;
        }

        private static LiquidMask GetLiquidMask(string key, RectInt chamber)
        {
            if (LiquidMasks.TryGetValue(key, out LiquidMask mask))
            {
                return mask;
            }
            Color32[] pixels = GetVesselPixels(key + "_fill_mask");
            if (pixels == null)
            {
                pixels = new Color32[Size * Size];
                Paint(pixels, chamber.xMin, chamber.yMin, chamber.width, chamber.height, new Color32(255, 255, 255, 255));
            }
            mask = new LiquidMask(pixels);
            LiquidMasks[key] = mask;
            return mask;
        }

        internal static void DrawLiquid(Color32[] pixels, (string Sprite, RectInt Chamber) vessel, int color, float fill, Vector2 up)
        {
            if (fill <= 0f)
            {
                return;
            }
            string key = vessel.Sprite == BulkContainers.DrumSprite ? BulkContainers.CompactDrumSprite : vessel.Sprite;
            Color32[] artwork = GetVesselPixels(key);
            LiquidMask mask = GetLiquidMask(key, vessel.Chamber);
            if (mask.Area == 0f)
            {
                return;
            }
            float span = Math.Abs(up.x) + Math.Abs(up.y);
            float surface = fill >= 1f ? float.PositiveInfinity : FindLiquidSurface(mask, up, span, fill);
            Color32 tint = Shade(VisibleLiquidColor(color), 1f);
            foreach (int index in mask.Samples)
            {
                float coverage = PixelCoverage(index, up, surface, span);
                if (coverage <= 0f)
                {
                    continue;
                }
                float strength = 0.92f * coverage * (mask.Pixels[index].a / 255f);
                if (artwork == null)
                {
                    Color32 fallback = tint;
                    fallback.a = (byte)Mathf.RoundToInt(255f * strength);
                    pixels[index] = fallback;
                }
                else if (artwork[index].a != 0)
                {
                    Color32 original = artwork[index];
                    var multiply = new Color32((byte)(original.r * tint.r / 255), (byte)(original.g * tint.g / 255),
                        (byte)(original.b * tint.b / 255), original.a);
                    pixels[index] = Color32.Lerp(original, multiply, strength);
                }
            }
        }

        internal static void DrawReaction(Color32[] pixels, AqueousReactions.Observation effects, Vector2 up, float time)
        {
            if (effects == AqueousReactions.Observation.None) return;
            int left = Size;
            int right = -1;
            int bottom = Size;
            int top = -1;
            float lowest = float.PositiveInfinity;
            for (int index = 0; index < pixels.Length; index++)
            {
                if (pixels[index].a == 0) continue;
                int x = index % Size;
                int y = index / Size;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                bottom = Math.Min(bottom, y);
                top = Math.Max(top, y);
                lowest = Math.Min(lowest, PixelHeight(index, up));
            }
            if (right < left) return;

            if ((effects & AqueousReactions.Observation.Sediment) != 0)
            {
                for (int index = 0; index < pixels.Length; index++)
                {
                    if (pixels[index].a == 0) continue;
                    float height = PixelHeight(index, up) - lowest;
                    if (height < 1.2f || (height < 2.2f && (index % Size) % 3 == 0))
                        pixels[index] = new Color32(224, 220, 191, pixels[index].a);
                }
            }
            if (!AqueousReactions.HasGas(effects)) return;

            int width = right - left + 1;
            int heightInPixels = top - bottom + 1;
            int count = Math.Min(7, Math.Max(1, width * heightInPixels / 20));
            for (int i = 0; i < count; i++)
            {
                float cycle = i * 0.381966f + 0.19f + time * (0.95f + 0.13f * (i % 3));
                float phase = cycle - (float)Math.Floor(cycle);
                float across = i * 0.618034f + 0.24f;
                int x = left + (int)((across - (float)Math.Floor(across)) * width);
                int y = bottom + (int)(phase * heightInPixels);
                int index = y * Size + x;
                if (pixels[index].a != 0)
                {
                    pixels[index] = new Color32(190, 241, 227, pixels[index].a);
                    if (i % 3 == 0 && y > bottom && pixels[index - Size].a != 0)
                        pixels[index - Size] = new Color32(116, 186, 197, pixels[index - Size].a);
                }
            }
        }

        private static float FindLiquidSurface(LiquidMask mask, Vector2 up, float span, float fill)
        {
            float low = float.PositiveInfinity;
            float high = float.NegativeInfinity;
            foreach (int index in mask.Samples)
            {
                float projected = PixelHeight(index, up);
                low = Math.Min(low, projected - span * 0.5f);
                high = Math.Max(high, projected + span * 0.5f);
            }
            float needed = mask.Area * fill;
            for (int step = 0; step < 10; step++)
            {
                float surface = (low + high) * 0.5f;
                float area = 0f;
                foreach (int index in mask.Samples)
                {
                    area += mask.Pixels[index].a * PixelCoverage(index, up, surface, span);
                }
                if (area < needed) low = surface;
                else high = surface;
            }
            return (low + high) * 0.5f;
        }

        private static float PixelHeight(int index, Vector2 up) => up.x * (index % Size + 0.5f) + up.y * (index / Size + 0.5f);

        private static float PixelCoverage(int index, Vector2 up, float surface, float span) =>
            Mathf.Clamp01(0.5f + (surface - PixelHeight(index, up)) / span);

        private static int VisibleLiquidColor(int color)
        {
            if (color == 0x869694)
            {
                return 0x189EB0;
            }

            Color.RGBToHSV(Shade(color, 1f), out float hue, out float saturation, out float brightness);
            Color32 vivid = Color.HSVToRGB(hue, Mathf.Min(0.96f, Mathf.Max(0.58f, saturation * 1.6f + 0.17f)),
                Mathf.Clamp01(brightness * 1.10f + 0.03f));
            return (vivid.r << 16) | (vivid.g << 8) | vivid.b;
        }

        private static void DrawGas(Color32[] pixels, RectInt chamber, int color, float density)
        {
            if (color == 0xa0aaa6)
            {
                color = 0x5F9DAB;
            }
            for (int row = 0; row < chamber.height; row++)
            {
                float glow = 1f - Math.Abs(2f * row / (chamber.height - 1) - 1f) * 0.5f;
                Color32 mist = Shade(color, 1.12f);
                mist.a = (byte)Mathf.RoundToInt(density * glow * 160f);
                Paint(pixels, chamber.xMin, chamber.yMin + row, chamber.width, 1, mist);
            }
            int bars = Math.Min(chamber.width, GaugeLevels);
            for (int bar = 0; bar < bars; bar++)
            {
                float fill = Mathf.Clamp01(density * bars - bar);
                Color32 tick = Gauge;
                tick.a = (byte)Mathf.RoundToInt(fill * 220f);
                Paint(pixels, chamber.xMin + bar, chamber.yMax, 1, 1, tick);
            }
        }

        private static Color32 Shade(int rgb, float factor)
        {
            return new Color32(Channel(16), Channel(8), Channel(0), 255);
            byte Channel(int shift) => (byte)Math.Min(255, Mathf.RoundToInt(((rgb >> shift) & 255) * factor));
        }

        private static void Paint(Color32[] pixels, int x, int y, int width, int height, Color32 color)
        {
            for (int row = y; row < y + height; row++)
            {
                int start = (Size - row - 1) * Size;
                for (int col = x; col < x + width; col++)
                {
                    pixels[start + col] = color;
                }
            }
        }
    }
}
