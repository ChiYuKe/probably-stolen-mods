using System.Collections.Generic;

namespace Chemistry
{
    /// <summary>
    /// 分析天平（<see cref="EquipmentTask.Weigh"/>）的称量报告，统一走 <see cref="ItemMass"/> 的质量体系：
    /// 固体按「单件质量 × 件数」读数，并给出 200 克堆叠上限；瓶装液体/气体按实装质量加空瓶皮重；
    /// 已检测样品按成分百分比估算含量；元素和化合物另报参考货值，对应当铺估价的用途。
    /// </summary>
    internal sealed partial class LabEquipmentPanel
    {
        private static string WeighReport(GameItem item)
        {
            string id = item?.identifier;
            if (string.IsNullOrEmpty(id)) return Failed;
            var lines = new List<string>();
            if (ReagentBottle.IsLiquid(id) || ReagentBottle.IsGas(id))
            {
                AddBottleReading(item, id, lines);
            }
            else
            {
                AddMassReading(item, id, lines);
            }

            if (LabWorkflow.IsReferenceMaterial(id) && item.unitValue > 0)
            {
                lines.Add(Loc.Pick($"参考货值 {(long)item.unitValue * item.unitCount} 信用点",
                    $"Appraised at {(long)item.unitValue * item.unitCount} credits"));
            }
            return string.Join("\n", lines);
        }

        /// <summary>Solid items, elements, and samples use integer mass units and counts.</summary>
        private static void AddMassReading(GameItem item, string id, List<string> lines)
        {
            int unit = ItemMass.UnitMass(id);
            if (unit <= 0)
            {
                lines.Add(Loc.Pick($"库存读数：{item.unitCount} 单位 · 未登记质量",
                    $"Inventory reading: {item.unitCount} units · mass unknown"));
                return;
            }

            int count = item.unitCount;
            long total = (long)unit * count;
            ElementDef element = ChemistryCatalog.FindElementByItemId(id);
            lines.Add(element != null
                ? Loc.Pick($"{count} 单位 · 每单位 {ItemMass.Format(unit)} · 共 {ItemMass.Format(total)}",
                    $"{count} units · {ItemMass.Format(unit)} each · {ItemMass.Format(total)} total")
                : count > 1
                    ? Loc.Pick($"{count} 件 · 每件 {ItemMass.Format(unit)} · 共 {ItemMass.Format(total)}",
                        $"{count} pcs · {ItemMass.Format(unit)} each · {ItemMass.Format(total)} total")
                    : Loc.Pick($"读数 {ItemMass.Format(unit)}", $"Reading {ItemMass.Format(unit)}"));
            int maxStack = ItemMass.MaxStack(unit);
            if (maxStack < LabStorage.MaxCountPerSlot)
            {
                lines.Add(Loc.Pick($"每格最多 {maxStack} 件（200 克上限）", $"At most {maxStack} per slot (200 g cap)"));
            }

            if (SampleTable.Find(id) is SampleDef sample)
            {
                lines.Add(id == sample.DetectedId ? SampleContentLine(sample, count) : UndetectedHint);
            }
        }

        /// <summary>已检测样品按「样品克数 × 质量百分比」估算各成分的合计质量。</summary>
        private static string SampleContentLine(SampleDef sample, int count)
        {
            var parts = new List<string>();
            foreach (var component in sample.Components)
            {
                parts.Add(component.Symbol + " " + Loc.Pick("约 ", "~ ") + ItemMass.Format((long)sample.ComponentGrams(component.Symbol) * count));
            }
            return Loc.Pick("含 ", "Contains ") + string.Join(Loc.Pick("、", ", "), parts);
        }

        private static string UndetectedHint => Loc.Pick("先用光谱仪鉴定，才能估算成分质量",
            "Scan it with the spectrometer to estimate component masses");

        /// <summary>瓶装试剂：实装质量加空瓶皮重；空瓶只报皮重。</summary>
        private static void AddBottleReading(GameItem item, string id, List<string> lines)
        {
            int units = ReagentBottle.Contents(item);
            long contents = ItemMass.ContentGrams(id, units);
            string emptyId = ReagentBottle.ReturnedEmpty(item);
            int tare = string.IsNullOrEmpty(emptyId) ? 0 : ItemMass.UnitMass(emptyId);
            if (contents > 0)
            {
                long milliliters = (long)units * ReagentBottle.MillilitersPerUnit;
                string amount = Loc.Pick($"{units} 单位 / {milliliters} mL", $"{units} units / {milliliters} mL");
                string line = Loc.Pick($"实装 {amount} · {ItemMass.Format(contents)}", $"Contents {amount} · {ItemMass.Format(contents)}");
                if (tare > 0)
                {
                    line += Loc.Pick($" · 皮重 {ItemMass.Format(tare)}", $" · tare {ItemMass.Format(tare)}");
                }
                lines.Add(line);
            }
            else
            {
                lines.Add(tare > 0
                    ? Loc.Pick($"空瓶 · 皮重 {ItemMass.Format(tare)}", $"Empty · tare {ItemMass.Format(tare)}")
                    : Loc.Pick("空瓶", "Empty bottle"));
            }
        }
    }
}
