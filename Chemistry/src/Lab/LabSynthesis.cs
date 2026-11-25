using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Chemistry
{
    /// <summary>
    /// 实验室合成：只用实验室储物格里的材料和工具，不扫当铺库存。
    /// 契约见 data/LAB_CONTRACT.md「API 契约 / LabSynthesis」。
    /// </summary>
    internal static class LabSynthesis
    {
        /// <summary>用实验室储物格的库存判断能不能合成（不要复用 SynthesisService.Check，那个看全店库存）。</summary>
        public static SynthesisStatus Check(CompoundDef def)
        {
            var status = new SynthesisStatus();

            if (def == null)
            {
                status.Reason = Loc.Get("craft_no_def", "没有这种化合物。", "Unknown compound.");
                return status;
            }

            if (def.Key == NitricProductionData.Key)
            {
                status.Reason = AcidStation.RequiredMachine;
                return status;
            }

            if (!LabStorage.Loaded)
            {
                LabStorage.EnsureLoaded();
            }

            foreach (KeyValuePair<string, int> pair in def.Inputs)
            {
                ElementDef element = ChemistryCatalog.FindElement(pair.Key);
                string itemId = element != null ? element.ItemId : ChemistryCatalog.ElementItemId(pair.Key);

                status.Elements.Add(new ElementNeed
                {
                    Element = element,
                    Need = pair.Value,
                    Have = LabStorage.CountOf(itemId),
                });
            }

            if (def.Tools != null)
            {
                for (int i = 0; i < def.Tools.Length; i++)
                {
                    ToolDef tool = ChemistryCatalog.FindTool(def.Tools[i]);
                    if (tool == null)
                    {
                        continue;
                    }

                    if (LabStorage.CountOf(tool.ItemId) <= 0)
                    {
                        status.MissingTools.Add(tool);
                    }
                }
            }

            status.CanCraft = !status.HasMissingElements && status.MissingTools.Count == 0;
            if (!status.CanCraft)
            {
                status.Reason = Describe(status);
            }

            return status;
        }

        /// <summary>现在实验室材料够做几种化合物（档案页/面板统计用）。</summary>
        public static int CountCraftable()
        {
            int count = 0;
            CompoundDef[] all = ChemistryCatalog.Compounds;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && Check(all[i]).CanCraft)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 执行实验室合成：先把实验室格子里的元素扣掉，产物优先进实验室格子，没空格就放回当铺。
        /// 任何一步失败都会把已经扣掉的元素用 AddUnits 还回格子并 Save，保证「失败不扣料」。
        /// </summary>
        public static bool Craft(CompoundDef def, out string message)
        {
            if (AcidStation.HasRecipe(def))
            {
                message = AcidStation.RequiredMachine;
                return false;
            }
            if (AcidStation.Running || LabRefining.Running)
            {
                message = Loc.Pick("等设备运行结束后再合成。", "Wait for the machines to finish before crafting.");
                return false;
            }
            SynthesisStatus status = Check(def);
            if (!status.CanCraft)
            {
                message = status.Reason;
                return false;
            }

            // 1) 扣元素（逐个 TryConsume，失败就把前面扣掉的还回去）
            var consumed = new List<KeyValuePair<string, int>>();
            for (int i = 0; i < status.Elements.Count; i++)
            {
                ElementNeed need = status.Elements[i];
                ElementDef element = need.Element;
                if (element == null || string.IsNullOrEmpty(element.ItemId))
                {
                    Rollback(consumed);
                    message = Loc.Get("craft_bad_element", "配方里有无法识别的元素。",
                        "The recipe contains an unknown element.");
                    return false;
                }

                if (!LabStorage.TryConsume(element.ItemId, need.Need))
                {
                    Rollback(consumed);
                    message = Loc.Get("lab_craft_consume_failed", "实验室里的{0}不够（要 {1}）。",
                        "Not enough {0} in the lab (need {1}).",
                        SynthesisService.DescribeElement(element), need.Need);
                    return false;
                }

                consumed.Add(new KeyValuePair<string, int>(element.ItemId, need.Need));
            }

            // 2) 产物：优先进实验室格子，没空格再生成物品放回当铺
            GameItem output = null;
            string failReason = null;
            try
            {
                if (CanAddToLab(def.ItemId))
                {
                    int before = LabStorage.CountOf(def.ItemId);
                    LabStorage.AddUnits(def.ItemId, 1);
                    if (LabStorage.CountOf(def.ItemId) <= before)
                    {
                        failReason = PlaceInStore(def, out output);
                    }
                }
                else
                {
                    failReason = PlaceInStore(def, out output);
                }
            }
            catch (Exception e)
            {
                DestroyQuietly(output);
                output = null;
                failReason = Loc.Get("lab_craft_exception", "合成产物时出错：{0}",
                    "Something went wrong while creating the product: {0}", e.Message);
            }

            if (!string.IsNullOrEmpty(failReason))
            {
                Rollback(consumed);
                message = failReason;
                return false;
            }

            // 3) 成功收尾
            LabStorage.Save();

            try
            {
                StoryService.OnLabSynthesized(def);
            }
            catch (Exception e)
            {
                // 故事线出问题也不能把已经合成好的东西吞掉
                ChemistryMod.Log?.Warning("实验室合成后更新故事进度失败：" + e.Message);
            }

            try
            {
                OrderService.OnCompoundProduced(def);
            }
            catch (Exception e)
            {
                // 委托进度同理，不能影响已经到手的产物
                ChemistryMod.Log?.Warning("实验室合成后更新委托进度失败：" + e.Message);
            }

            message = Loc.Get("craft_ok", "合成成功：{0}", "Crafted: {0}", SynthesisService.DisplayName(def));
            ChemistryMod.Log?.Log(message + "（实验室储物格）");
            DayHudBridge.Push(Loc.Get("hud_craft_ok", "合成成功：{0}", "Crafted: {0}",
                SynthesisService.DisplayName(def)), "info", 6f);
            return true;
        }

        /// <summary>
        /// 把缺的元素数量、缺的工具各 1 件从当铺库存搬进实验室格子。
        /// 全部满足返回 true，否则 false + 说明（容量满/店里不够/材料还是缺）。
        /// </summary>
        public static bool PullFromStore(CompoundDef def, out string message)
        {
            if (def == null)
            {
                message = Loc.Get("craft_no_def", "没有这种化合物。", "Unknown compound.");
                return false;
            }

            SynthesisStatus status = Check(def);
            if (status.CanCraft)
            {
                message = Loc.Get("lab_pull_already", "实验室里的材料已经齐了。",
                    "The lab already has everything needed.");
                return true;
            }

            var problems = new List<string>();

            for (int i = 0; i < status.Elements.Count; i++)
            {
                ElementNeed need = status.Elements[i];
                int missing = need.Need - need.Have;
                if (missing <= 0)
                {
                    continue;
                }

                if (need.Element == null)
                {
                    problems.Add(DescribeElement(need.Element));
                    continue;
                }

                string depositMessage;
                int got = LabStorage.DepositFromStore(need.Element.ItemId, missing, out depositMessage);
                if (got < missing && !string.IsNullOrEmpty(depositMessage))
                {
                    problems.Add(depositMessage);
                }
            }

            for (int i = 0; i < status.MissingTools.Count; i++)
            {
                ToolDef tool = status.MissingTools[i];
                if (tool == null)
                {
                    continue;
                }

                string depositMessage;
                int got = LabStorage.DepositFromStore(tool.ItemId, 1, out depositMessage);
                if (got <= 0 && !string.IsNullOrEmpty(depositMessage))
                {
                    problems.Add(depositMessage);
                }
            }

            SynthesisStatus after = Check(def);
            if (after.CanCraft)
            {
                message = Loc.Get("lab_pull_ok", "缺的材料已经从当铺补进实验室格子了。",
                    "The missing materials were moved from the store into the lab.");
                return true;
            }

            var builder = new StringBuilder();
            if (problems.Count > 0)
            {
                builder.Append(string.Join(Loc.Get("list_separator", "、", ", "), problems.ToArray()));
            }

            string missingText = Describe(after);
            if (!string.IsNullOrEmpty(missingText))
            {
                if (builder.Length > 0)
                {
                    builder.Append(Loc.Get("sentence_separator", "；", "; "));
                }

                builder.Append(missingText);
            }

            message = builder.Length > 0
                ? builder.ToString()
                : Loc.Get("lab_pull_failed", "没能把材料补齐。", "Could not move the materials.");
            return false;
        }

        /// <summary>缺料/缺工具的一句话说明（UI 用）。</summary>
        public static string Describe(SynthesisStatus status)
        {
            if (status == null)
            {
                return "";
            }

            var builder = new StringBuilder();

            if (status.HasMissingElements)
            {
                builder.Append(Loc.Get("craft_missing_elements", "缺元素：", "Missing elements: "));
                bool first = true;
                for (int i = 0; i < status.Elements.Count; i++)
                {
                    ElementNeed need = status.Elements[i];
                    if (need.Enough)
                    {
                        continue;
                    }

                    if (!first)
                    {
                        builder.Append(Loc.Get("list_separator", "、", ", "));
                    }

                    builder.Append(DescribeElement(need.Element)).Append(" ×").Append(need.Need - need.Have);
                    first = false;
                }
            }

            if (status.MissingTools.Count > 0)
            {
                if (builder.Length > 0)
                {
                    builder.Append(Loc.Get("sentence_separator", "；", "; "));
                }

                builder.Append(Loc.Get("craft_missing_tools", "缺工具：", "Missing tools: "));
                for (int i = 0; i < status.MissingTools.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(Loc.Get("list_separator", "、", ", "));
                    }

                    builder.Append(DescribeTool(status.MissingTools[i]));
                }
            }

            if (builder.Length == 0)
            {
                return Loc.Get("lab_desc_ready", "材料齐全，可以合成。", "Materials ready.");
            }

            return builder.ToString();
        }

        // ---------------------------------------------------------------
        // 内部辅助
        // ---------------------------------------------------------------

        /// <summary>产物能不能进实验室格子：同 id 格子有余量或还有空格。</summary>
        private static bool CanAddToLab(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !LabStorage.IsAllowed(itemId))
            {
                return false;
            }

            LabSlot[] slots = LabStorage.Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                LabSlot slot = slots[i];
                if (string.IsNullOrEmpty(slot.ItemId) || slot.Count <= 0)
                {
                    return true;
                }

                if (string.Equals(slot.ItemId, itemId, StringComparison.Ordinal)
                    && slot.Count < LabStorage.MaxCountPerSlot)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>生成产物放进当铺；成功返回 null，失败返回给人看的原因。</summary>
        private static string PlaceInStore(CompoundDef def, out GameItem output)
        {
            output = null;

            GameItem item;
            try
            {
                item = ModHelper.SpawnItem(def.ItemId);
            }
            catch (Exception e)
            {
                return Loc.Get("lab_craft_spawn_failed", "化合物没造出来（物品目录没注册上？）：{0}",
                    "The compound could not be created (is it registered?): {0}", e.Message);
            }

            if (item == null)
            {
                return Loc.Get("lab_craft_spawn_failed", "化合物没造出来（物品目录没注册上？）。",
                    "The compound could not be created (is it registered?).");
            }

            output = item;

            try
            {
                ModHelper.SetItemOwnerShip(item, true);
            }
            catch (Exception e)
            {
                DestroyQuietly(item);
                output = null;
                return Loc.Get("lab_craft_claim_failed", "设置产物归属失败：{0}",
                    "Could not claim the product: {0}", e.Message);
            }

            bool placed;
            try
            {
                placed = StoreInventory.TryPlace(item);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Chemistry] 实验室产物放入当铺失败：" + e.Message);
                placed = false;
            }

            if (!placed)
            {
                output = null;
                return Loc.Get("lab_craft_place_failed", "仓库和柜台都满了，产物放不进去，材料没扣。",
                    "Storage and counters are full; nothing was consumed.");
            }

            output = null;
            return null;
        }

        /// <summary>把已经扣掉的元素还回实验室格子并保存。</summary>
        private static void Rollback(List<KeyValuePair<string, int>> consumed)
        {
            if (consumed == null || consumed.Count == 0)
            {
                return;
            }

            for (int i = 0; i < consumed.Count; i++)
            {
                LabStorage.AddUnits(consumed[i].Key, consumed[i].Value);
            }

            LabStorage.Save();
        }

        private static void DestroyQuietly(GameItem item)
        {
            try
            {
                if (item != null)
                {
                    item.Destroy();
                }
            }
            catch
            {
                // 收不回来就算了
            }
        }

        private static string DescribeElement(ElementDef element)
        {
            if (element == null)
            {
                return Loc.Get("lab_unknown_element", "未知元素", "unknown element");
            }

            return SynthesisService.DescribeElement(element);
        }

        private static string DescribeTool(ToolDef tool)
        {
            if (tool == null)
            {
                return Loc.Get("lab_unknown_tool", "未知工具", "unknown tool");
            }

            return SynthesisService.DescribeTool(tool);
        }
    }
}
