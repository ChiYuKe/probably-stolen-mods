using System;

namespace Chemistry
{
    /// <summary>
    /// 样品相关的拖放交互（挂在物品自己身上，不动全局钩子）：
    ///   · 「光谱仪」拖到未知样品上 → 扫描（音效 + 日志 + DayHud）→ 弹出成分报告；
    ///   · 酸浸出固体废料中的目标金属，漏斗过滤浸出液；蒸馏器处理其他适用成分。
    ///
    /// 游戏 <see cref="GameItem"/> 继承 <see cref="GameItemFunc"/>，所以每件物品都有自己的
    /// canItemTargetItemFunc / mayItemTargetItemFunc / onItemTargetItemFunc；在工具工厂里给
    /// 在物品工厂挂接这些委托。
    /// </summary>
    internal static class SampleInteraction
    {
        public const string DetectorToolKey = "spectrometer";
        public const string RefinerToolKey = "distiller";
        public const string AcidInteractionKey = "acid_reagent";

        // 同一帧同一对物品只处理一次：游戏 Target() 会从两侧各调一次回调。
        private static int lastHandledFrame = -1;
        private static GameItem lastHandledSource;
        private static GameItem lastHandledTarget;

        /// <summary>工具创建时调用：给光谱仪/蒸馏器挂上拖到样品上的判定与回调。</summary>
        public static void Attach(GameItem item, string toolKey)
        {
            if (item == null || string.IsNullOrEmpty(toolKey))
            {
                return;
            }

            if (toolKey == DetectorToolKey)
            {
                item.canItemTargetItemFunc = CanDetect;
                item.mayItemTargetItemFunc = CanDetect;
                item.onItemTargetItemFunc = OnDetect;
            }
            else if (toolKey == RefinerToolKey)
            {
                item.canItemTargetItemFunc = CanRefine;
                item.mayItemTargetItemFunc = CanRefine;
                item.onItemTargetItemFunc = OnRefine;
            }
            else if (toolKey == AcidInteractionKey)
            {
                item.canThisTargetItemFunc -= CanLeach;
                item.mayThisTargetItemFunc -= CanLeach;
                item.onThisTargetItemFunc -= OnLeach;
                item.canThisTargetItemFunc += CanLeach;
                item.mayThisTargetItemFunc += CanLeach;
                item.onThisTargetItemFunc += OnLeach;
            }
            else if (toolKey == LabRefining.FilterToolKey)
            {
                item.canItemTargetItemFunc = CanFilter;
                item.mayItemTargetItemFunc = CanFilter;
                item.onItemTargetItemFunc = OnFilter;
            }
        }

        /// <summary>
        /// 兜底：读档后扫描店里已有的化学工具，把拖放委托补挂上（防止某些读档路径没走物品工厂）。
        /// </summary>
        public static void AttachExistingTools()
        {
            try
            {
                var items = StoreInventory.AllItems();
                if (items == null)
                {
                    return;
                }

                int attached = 0;
                foreach (GameItem item in items)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    string id = item.identifier;
                    string name = item.identifierName;
                    if (IsToolItem(id, name, DetectorToolKey))
                    {
                        item.canItemTargetItemFunc = CanDetect;
                        item.mayItemTargetItemFunc = CanDetect;
                        item.onItemTargetItemFunc = OnDetect;
                        attached++;
                    }
                    else if (IsToolItem(id, name, RefinerToolKey))
                    {
                        item.canItemTargetItemFunc = CanRefine;
                        item.mayItemTargetItemFunc = CanRefine;
                        item.onItemTargetItemFunc = OnRefine;
                        attached++;
                    }
                    else if (IsToolItem(id, name, LabRefining.FilterToolKey))
                    {
                        Attach(item, LabRefining.FilterToolKey);
                        attached++;
                    }
                    else if (LabRefining.IsAcid(id))
                    {
                        item.canUseOutsideCombat = true;
                        Attach(item, AcidInteractionKey);
                        attached++;
                    }
                }

                if (attached > 0)
                {
                    ChemUi.Log?.Log("样品拖放：读档后补挂 " + attached + " 件检测/提炼工具的委托");
                }
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("补挂检测/提炼工具失败：" + e.Message);
            }
        }

        private static bool IsToolItem(string itemId, string identifierName, string toolKey)
        {
            if (!string.IsNullOrEmpty(itemId) && itemId == ChemistryCatalog.ToolItemId(toolKey))
            {
                return true;
            }

            return !string.IsNullOrEmpty(identifierName) && identifierName == toolKey;
        }

        // ---------------------------------------------------------------
        // 判定
        // ---------------------------------------------------------------

        private static bool CanDetect(GameItem source, GameItem target)
        {
            // 未知样品可检测；已检测的再拖一次也能重新调出报告。
            return FindSample(target) != null;
        }

        private static bool CanRefine(GameItem source, GameItem target)
        {
            SampleDef sample = FindSample(target);
            return sample != null && target != null && target.identifier == sample.DetectedId;
        }

        private static bool CanLeach(GameItem source, GameItem target)
        {
            SampleDef sample = FindSample(target);
            return sample != null && Array.Exists(sample.Components, component => LabRefining.IsAcidTarget(sample, component.Symbol));
        }

        private static bool CanFilter(GameItem source, GameItem target)
        {
            return LabRefining.FindIntermediate(target?.identifier)?.Filtered == false;
        }

        private static void OnLeach(GameItem source, GameItem target)
        {
            if (CanLeach(source, target) && ShouldHandle(source, target))
            {
                ModHelper.Delay(ChemistryCatalog.ModId, 0.15f, () => RefiningStation.OpenLeaching(source, target));
            }
        }

        private static void OnFilter(GameItem source, GameItem target)
        {
            if (ShouldHandle(source, target))
            {
                ModHelper.Delay(ChemistryCatalog.ModId, 0.15f, () => RefiningStation.OpenFiltering(source, target));
            }
        }

        private static SampleDef FindSample(GameItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.identifier))
            {
                return null;
            }

            return SampleTable.Find(item.identifier);
        }

        // ---------------------------------------------------------------
        // 检测
        // ---------------------------------------------------------------

        private static void OnDetect(GameItem source, GameItem target)
        {
            if (!ShouldHandle(source, target))
            {
                return;
            }

            try
            {
                SampleDef sample = FindSample(target);
                if (sample == null)
                {
                    return;
                }

                bool already = target.identifier == sample.DetectedId;

                PlayScanEffect(sample, already);

                GameTicks.Schedule(18, () =>
                {
                    if (target.parentInventory == null || FindSample(target) != sample) return;
                    if (!already)
                    {
                        if (target.identifier == sample.DetectedId) return;
                        MarkDetected(sample, target);
                    }
                    SampleReportWindow.ShowDetected(sample, already);
                });
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("光谱仪检测出错：" + e);
            }
        }

        private static void MarkDetected(SampleDef sample, GameItem item)
        {
            try
            {
                item.identifier = sample.DetectedId;
                item.SetName(sample.DisplayName(true));
                item.shortDescription = sample.Report();
                item.longDescription = sample.Report();
                item.Validate();
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("标记样品已检测失败：" + e.Message);
            }

            // 如果这件样品在实验室储物格里，电池格数量同步（未知批次 -> 已检测批次）
            try
            {
                LabInventoryGrid.CaptureAfterDrop();
            }
            catch
            {
                // 不在实验室/储物格不可用时忽略
            }
        }

        private static void PlayScanEffect(SampleDef sample, bool already)
        {
            string name = sample.DisplayName(false);
            try
            {
                AudioManager audio = AudioManager.Instance;
                if (audio != null)
                {
                    audio.Play2("interface_click");
                }
            }
            catch
            {
                // 音效不可用就算了
            }

            try
            {
                OverlayHandler handler = OverlayHandler.current;
                if (handler != null)
                {
                    handler.AppendLog(
                        Loc.Get("sample_log_scanning", "光谱仪扫描：{0}…", "Spectrometer scanning: {0}...", name),
                        false);
                }
            }
            catch
            {
                // 日志浮层不可用时忽略
            }

            DayHudBridge.Push(
                Loc.Get("sample_hud_scanning", "光谱仪正在扫描 {0}…", "Spectrometer is scanning {0}...", name),
                "info", 2f);

            if (already)
            {
                DayHudBridge.Push(
                    Loc.Get("sample_hud_already", "{0} 已经检测过了，直接调出报告。",
                        "{0} was already analyzed; showing the report.", name),
                    "info", 3f);
            }
        }

        // ---------------------------------------------------------------
        // 提炼
        // ---------------------------------------------------------------

        private static void OnRefine(GameItem source, GameItem target)
        {
            if (!ShouldHandle(source, target))
            {
                return;
            }

            try
            {
                SampleDef sample = FindSample(target);
                if (sample == null)
                {
                    return;
                }

                if (target.identifier != sample.DetectedId)
                {
                    DayHudBridge.Push(
                        Loc.Get("sample_hud_not_detected", "先拿光谱仪检测这件样品，才知道能提炼什么。",
                            "Scan the sample with the spectrometer first."),
                        "warn", 3f);
                    return;
                }

                if (Array.TrueForAll(sample.Components, component => LabRefining.IsAcidTarget(sample, component.Symbol)))
                {
                    DayHudBridge.Push(LabRefining.AcidInstructions, "info", 4f);
                    return;
                }
                PlayRefineEffect(sample);

                // 拖拽结束的同一帧里开窗口容易和游戏的拖放收尾打架，晚一帧再弹。
                ModHelper.Delay(ChemistryCatalog.ModId, 0.15f,
                    () => SampleReportWindow.ShowRefine(sample, ""));
            }
            catch (Exception e)
            {
                ChemUi.Log?.Warning("蒸馏器提炼出错：" + e);
            }
        }

        private static void PlayRefineEffect(SampleDef sample)
        {
            try
            {
                AudioManager audio = AudioManager.Instance;
                if (audio != null)
                {
                    audio.Play2("interface_click");
                }
            }
            catch
            {
            }

            try
            {
                OverlayHandler handler = OverlayHandler.current;
                if (handler != null)
                {
                    handler.AppendLog(
                        Loc.Get("sample_log_refine", "蒸馏器接入：{0}", "Distiller attached: {0}", sample.DisplayName(true)),
                        false);
                }
            }
            catch
            {
            }
        }

        // ---------------------------------------------------------------
        // 去重
        // ---------------------------------------------------------------

        private static bool ShouldHandle(GameItem source, GameItem target)
        {
            if (source == null || target == null)
            {
                return false;
            }

            if (lastHandledFrame == UnityEngine.Time.frameCount &&
                ReferenceEquals(lastHandledSource, source) &&
                ReferenceEquals(lastHandledTarget, target))
            {
                return false;
            }

            lastHandledFrame = UnityEngine.Time.frameCount;
            lastHandledSource = source;
            lastHandledTarget = target;
            return true;
        }
    }
}
