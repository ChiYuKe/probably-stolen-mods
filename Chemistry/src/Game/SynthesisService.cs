using System;
using System.Collections.Generic;
using System.Text;

namespace Chemistry
{
    /// <summary>一次合成检查的结果，UI 直接拿去显示。</summary>
    internal sealed class SynthesisStatus
    {
        public bool CanCraft;

        /// <summary>不能合成时给人看的一句话。</summary>
        public string Reason;

        /// <summary>元素原料：元素符号 -> 需要 / 已有。</summary>
        public readonly List<ElementNeed> Elements = new List<ElementNeed>();

        /// <summary>缺少的工具（没有的才放进来）。</summary>
        public readonly List<ToolDef> MissingTools = new List<ToolDef>();

        public bool HasMissingElements
        {
            get
            {
                foreach (ElementNeed need in Elements)
                {
                    if (need.Have < need.Need)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }

    internal sealed class ElementNeed
    {
        public ElementDef Element;
        public int Need;
        public int Have;

        public bool Enough
        {
            get { return Have >= Need; }
        }
    }

    /// <summary>
    /// 合成服务：检查原料 / 工具、扣材料、产出化合物。
    /// UI 只负责显示，真正的判定和扣料都在这里，F9 调试入口也走同一套。
    /// </summary>
    internal static class SynthesisService
    {
        /// <summary>检查一种化合物现在能不能合成（现扫一次店内库存）。</summary>
        public static SynthesisStatus Check(CompoundDef def)
        {
            return Check(def, StoreInventory.CountUnits());
        }

        /// <summary>
        /// 用调用方给的库存快照做检查。UI 刷新整张列表时先扫一次 CountUnits 再逐行传进来，
        /// 避免 50 个配方各扫一遍全店容器。
        /// </summary>
        public static SynthesisStatus Check(CompoundDef def, IDictionary<string, int> snapshot)
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

            IDictionary<string, int> counts = snapshot ?? new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (KeyValuePair<string, int> pair in def.Inputs)
            {
                ElementDef element = ChemistryCatalog.FindElement(pair.Key);
                string itemId = element != null ? element.ItemId : ChemistryCatalog.ElementItemId(pair.Key);

                int have;
                counts.TryGetValue(itemId, out have);

                status.Elements.Add(new ElementNeed
                {
                    Element = element,
                    Need = pair.Value,
                    Have = have,
                });
            }

            if (def.Tools != null)
            {
                foreach (string toolKey in def.Tools)
                {
                    ToolDef tool = ChemistryCatalog.FindTool(toolKey);
                    if (tool == null)
                    {
                        continue;
                    }

                    int have;
                    counts.TryGetValue(tool.ItemId, out have);
                    if (have <= 0)
                    {
                        status.MissingTools.Add(tool);
                    }
                }
            }

            status.CanCraft = !status.HasMissingElements && status.MissingTools.Count == 0;
            if (!status.CanCraft)
            {
                status.Reason = BuildReason(status);
            }
            if (AcidStation.HasRecipe(def))
            {
                status.CanCraft = false;
                status.Reason = AcidStation.RequiredMachine;
            }

            return status;
        }

        /// <summary>
        /// 执行合成：先整套校验，通过后生成产物放进仓库，最后才扣元素。
        /// 顺序是刻意的：先扣料再产出现在「仓库满导致产物放不进去」时材料就白扣了；
        /// 先把产物放进仓库、确认放好了再扣料，失败时把产物收回去，玩家不会亏材料。
        /// </summary>
        public static bool Craft(CompoundDef def, out string message)
        {
            SynthesisStatus status = Check(def);
            if (!status.CanCraft)
            {
                message = status.Reason;
                return false;
            }

            // 1) 先造产物并放进仓库
            GameItem output = ModHelper.SpawnItem(def.ItemId);
            if (output == null)
            {
                message = Loc.Get("craft_spawn_failed", "化合物没造出来（物品目录没注册上？）。",
                    "The compound could not be created (is it registered?).");
                return false;
            }

            try
            {
                ModHelper.SetItemOwnerShip(output, true);
            }
            catch (Exception e)
            {
                DestroyQuietly(output);
                message = Loc.Get("craft_move_failed", "设置产物归属失败，材料没扣：" + e.Message,
                    "Could not claim the product, nothing was consumed: " + e.Message);
                return false;
            }

            // 先放进仓库（满了会自动试柜台/展示柜），放不下就退出且不扣料
            if (!StoreInventory.TryPlace(output))
            {
                message = Loc.Get("craft_move_failed", "仓库和柜台都满了，产物放不进去，材料没扣。",
                    "Storage and counters are full; nothing was consumed.");
                return false;
            }

            // 2) 产物已就位，再扣元素
            var consumed = new List<KeyValuePair<ElementDef, int>>();
            foreach (ElementNeed need in status.Elements)
            {
                ElementDef element = need.Element;
                string itemId = element != null ? element.ItemId : "";
                if (string.IsNullOrEmpty(itemId))
                {
                    DestroyQuietly(output);
                    message = Loc.Get("craft_bad_element", "配方里有无法识别的元素。", "The recipe contains an unknown element.");
                    return false;
                }

                if (!StoreInventory.TryConsume(itemId, need.Need))
                {
                    // 同一帧内检查过总量却扣不动，理论上只有物品被别的逻辑动了才会发生；
                    // 把已经放进去的产物收回，避免白送。
                    DestroyQuietly(output);
                    message = Loc.Get("craft_consume_failed", "扣材料失败：{0}", "Failed to consume {0}.", DescribeElement(element));
                    return false;
                }

                consumed.Add(new KeyValuePair<ElementDef, int>(element, need.Need));
            }

            message = Loc.Get("craft_ok", "合成成功：{0}", "Crafted: {0}", DisplayName(def));
            ChemistryMod.Log?.Log(message + "（消耗 " + DescribeConsumed(consumed) + "）");
            DayHudBridge.Push(Loc.Get("hud_craft_ok", "合成成功：{0}", "Crafted: {0}", DisplayName(def)), "info", 6f);

            try
            {
                OrderService.OnCompoundProduced(def);
            }
            catch (Exception e)
            {
                // 委托进度出问题不能影响已经到手的产物
                ChemistryMod.Log?.Warning("合成后更新委托进度失败：" + e.Message);
            }

            return true;
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
                // 收不回来就算了，别把合成流程再炸一次
            }
        }

        public static string DisplayName(CompoundDef def)
        {
            if (def == null)
            {
                return "?";
            }

            return Loc.Pick(def.NameZh, def.NameEn) + " (" + def.Formula + ")";
        }

        public static string DescribeElement(ElementDef element)
        {
            if (element == null)
            {
                return "?";
            }

            return Loc.Pick(element.NameZh, element.NameEn) + " (" + element.Symbol + ")";
        }

        public static string DescribeTool(ToolDef tool)
        {
            if (tool == null)
            {
                return "?";
            }

            return Loc.Pick(tool.NameZh, tool.NameEn);
        }

        private static string BuildReason(SynthesisStatus status)
        {
            var builder = new StringBuilder();

            if (status.HasMissingElements)
            {
                builder.Append(Loc.Get("craft_missing_elements", "缺元素：", "Missing elements: "));
                bool first = true;
                foreach (ElementNeed need in status.Elements)
                {
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
                builder.Append(Loc.Get("craft_unknown_reason", "现在还不能合成。", "Cannot craft right now."));
            }

            return builder.ToString();
        }

        private static string DescribeConsumed(List<KeyValuePair<ElementDef, int>> consumed)
        {
            var builder = new StringBuilder();
            foreach (KeyValuePair<ElementDef, int> pair in consumed)
            {
                if (builder.Length > 0)
                {
                    builder.Append(" + ");
                }

                builder.Append(pair.Value).Append(' ').Append(pair.Key != null ? pair.Key.Symbol : "?");
            }

            return builder.ToString();
        }
    }
}
