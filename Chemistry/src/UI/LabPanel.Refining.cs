using System.Linq;
using TMPro;
using UnityEngine;

namespace Chemistry
{
    internal static partial class LabPanel
    {
        private static RectTransform refiningHost;

        private static void ResetRefiningPage()
        {
            refiningHost = null;
        }

        public static void ShowRefiningStatus(string message)
        {
            if (CanRefresh())
            {
                SetLabel("lab_refining_status", message, ChemUi.Accent);
            }
        }

        private static void BuildRefiningPage(CustomUIBuilder builder)
        {
            builder.BeginRow(10f, TextAnchor.MiddleLeft);
            AddRefiningIcon(builder, "sample_analyzer", 42f);
            LabUi.Label(builder, Loc.Get("lab_samples_hint_drag",
                "双击地下实验室右侧的提炼反应台打开设备。把试剂、工具和物料拖进对应槽位，再启动反应。",
                "Double-click the refining machine on the right side of the lab. Load the reagent, tool and feed into their slots, then start the reaction."),
                null, 12.5f, 770f, 62f, true, ChemUi.Main, false, TextAlignmentOptions.TopLeft);
            builder.End();

            LabUi.Label(builder, " ", "lab_refining_status", 12f, LabUi.ContentWidth, 30f,
                true, ChemUi.Accent, false, TextAlignmentOptions.MidlineLeft);

            builder.BeginScrollFlexible(BenchScrollMinHeight);
            refiningHost = builder.CurrentContainer;
            builder.End();
        }

        private static void RefreshRefining()
        {
            CustomUIManager manager = CustomUIManager.Instance;
            if (refiningHost == null || manager == null || !CanRefresh())
            {
                return;
            }

            CustomUIBuilder.ClearContainer(refiningHost);
            var builder = new CustomUIBuilder(manager, window, refiningHost);
            if (!LabStorage.Loaded)
            {
                LabStorage.EnsureLoaded();
            }

            int rows = 0;
            foreach (LabRefining.Intermediate product in LabRefining.Intermediates)
            {
                int count = LabStorage.CountOf(product.ItemId);
                if (count > 0)
                {
                    BuildIntermediateRow(builder, product, count, ++rows);
                }
            }
            foreach (SampleDef sample in SampleTable.All)
            {
                int unknown = LabStorage.CountOf(sample.ItemId);
                if (unknown > 0)
                {
                    BuildSampleRow(builder, sample, unknown, false, ++rows);
                }

                int detected = LabStorage.CountOf(sample.DetectedId);
                if (detected > 0)
                {
                    BuildSampleRow(builder, sample, detected, true, ++rows);
                }
            }

            if (rows == 0)
            {
                LabUi.Label(builder, Loc.Get("lab_samples_empty",
                    "储物格里没有样品。翻垃圾后，在「储物格」把样品或废金属从当铺存进来。",
                    "No samples in lab storage. After scavenging, use Storage to deposit samples or scrap metal from the store."),
                    null, 13f, LabUi.RowWidth, 42f, true, ChemUi.Dim, false, TextAlignmentOptions.Left);
            }
            else
            {
                LabUi.Label(builder, Loc.Get("lab_samples_tip",
                    "液体和气样可用蒸馏器分离；已检测样品按成分配方回收时，将足量样品放入蒸馏器面板。",
                    "Use the distiller to separate liquids and gases; for analyzed samples, load the recipe amount into its panel to recover a component."),
                    null, 11.5f, LabUi.RowWidth, 22f, true, ChemUi.Dim, false, TextAlignmentOptions.Left);
            }

            CustomUIBuilder.RebuildLayout(refiningHost);
        }

        private static void BuildIntermediateRow(CustomUIBuilder builder, LabRefining.Intermediate product, int count, int index)
        {
            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            ChemUi.RowBackground(builder.CurrentContainer, index);
            ChemUi.RowLayout(builder.CurrentContainer, LabUi.RowWidth, 52f);
            AddRefiningIcon(builder, product.SpriteKey, 32f);
            LabUi.Label(builder, product.Name + " ×" + count + "\n" + product.Instructions,
                null, 12f, LabUi.RowWidth - 180f, 48f, true, ChemUi.Main, false, TextAlignmentOptions.MidlineLeft);
            builder.AddFlexibleSpace();
            LabUi.Button(builder, product.Filtered ? Loc.Pick("装入还原槽", "Load reduction") : Loc.Pick("装入过滤台", "Load filter"),
                () => RefiningStation.OpenIntermediate(product), 130f, 30f, true, product.Instructions);
            builder.End();
        }

        private static void BuildSampleRow(CustomUIBuilder builder, SampleDef sample, int count, bool detected, int index)
        {
            builder.BeginRow(8f, TextAnchor.MiddleLeft);
            ChemUi.RowBackground(builder.CurrentContainer, index);
            ChemUi.RowLayout(builder.CurrentContainer, LabUi.RowWidth, detected ? 82f : 34f);
            AddRefiningIcon(builder, sample.SpriteKey, 28f);

            string label = sample.DisplayName(detected) + " ×" + count;
            if (detected)
            {
                label += "\n" + sample.Report();
                if (System.Array.Exists(sample.Components, component => LabRefining.IsAcidTarget(sample, component.Symbol)))
                    label += "\n" + Loc.Get("lab_samples_leach_hint", "酸浸：样品和对应酸放入提炼反应台。", "Acid route: load the sample and matching acid into the refining station.");
                if (sample.Batch != "09" && sample.Components.Any(component => !LabRefining.IsAcidTarget(sample, component.Symbol)))
                    label += "\n" + Loc.Get("lab_samples_distill_hint", "直接回收：按配方数量放入蒸馏器，选目标后取产物。", "Direct recovery: load the recipe amount into the distiller, select a target and collect the product.");
            }
            else
            {
                label += "\n" + Loc.Get("lab_samples_unknown_hint", "未检测：双击光谱仪打开面板，把样品放入原料槽扫描。",
                    "Unknown: double-click the spectrometer, load the sample into its ingredient slot and scan it.");
            }

            LabUi.Label(builder, label, null, 12f, LabUi.RowWidth - 70f, detected ? 80f : 32f, true,
                ChemUi.Main, false, TextAlignmentOptions.MidlineLeft);
            builder.End();
        }

        private static void AddRefiningIcon(CustomUIBuilder builder, string key, float size)
        {
            Sprite sprite = ChemUi.ModSprite(key);
            if (sprite != null)
            {
                builder.AddImage(sprite, size, size, null);
                if (builder.Last?.image != null)
                {
                    builder.Last.image.preserveAspect = true;
                    builder.Last.image.raycastTarget = false;
                }
            }
        }
    }
}
