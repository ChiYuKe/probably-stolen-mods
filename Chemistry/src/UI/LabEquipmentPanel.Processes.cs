using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chemistry
{
    internal sealed partial class LabEquipmentPanel
    {
        private bool SupportsBrineSampling => equipment.Key == "pipette";
        private MachineryProgressBar peakA;
        private MachineryProgressBar peakB;

        private void BuildPeakReadout(List<PixelElement> controls)
        {
            if (Task != EquipmentTask.AnalyzeFluid) return;
            peakA = new MachineryProgressBar(width: 68).SetColor(new Color32(113, 189, 216, 255));
            peakB = new MachineryProgressBar(width: 68).SetColor(new Color32(213, 172, 107, 255));
            controls.Add(MachineryStack.Horizontal(new MachineryText("A", size: 7), peakA));
            controls.Add(MachineryStack.Horizontal(new MachineryText("B", size: 7), peakB));
        }

        private void RefreshPeakReadout(float elapsed)
        {
            if (peakA == null) return;
            bool scanning = running && Input?.identifier == LabProcessData.OilTrace;
            float a = scanning ? Mathf.Clamp01((elapsed - 0.2f) / 0.24f) * 0.8f : output.childItem?.identifier == LabProcessData.PeakA ? 0.8f : 0f;
            float b = scanning ? Mathf.Clamp01((elapsed - 0.55f) / 0.28f) * 0.45f : byproduct.childItem?.identifier == LabProcessData.PeakB ? 0.45f : 0f;
            if (peakA.value != a) peakA.SetValue(a);
            if (peakB.value != b) peakB.SetValue(b);
        }

        private void BuildProcessReceiver(List<MachineryPanel> sections)
        {
            string label = Task switch
            {
                EquipmentTask.GasCollect => Loc.Pick("空采样气瓶 / 钢瓶", "Empty gas bottle / cylinder"),
                EquipmentTask.FreezeDry or EquipmentTask.Seal or EquipmentTask.AnalyzeFluid =>
                    Loc.Pick("空玻璃瓶", "Empty glass bottle"),
                _ => null
            };
            if (label == null) return;
            receiver = new EquipmentSlot();
            receiver.mayInventoryAddItemFunc = (item, _) => CanLoad(receiver, item) && (Task switch
            {
                EquipmentTask.GasCollect => ReagentBottle.IsGasEmpty(item.identifier),
                _ => IsPortableGlass(item)
            });
            sections.Add(MachineHelper.Panel(label, receiver, "bottle"));
        }

        private static bool HasProcessSediment(string id) =>
            id == LabProcessData.Saturated || id == LabProcessData.LoadedCrystals;

        private static bool IsOpticalLiquid(string id) => LabAnalysisData.IsOpticalSample(id) || LabProcessData.IsExtract(id);

        private bool AcceptsProcessInput(GameItem item)
        {
            string id = item.identifier;
            return Task switch
            {
                EquipmentTask.Mix => id == LabAnalysisData.Dye && ReagentBottle.SizeOf(item) == LabMixtureData.SmallBottle,
                EquipmentTask.Settle => id == LabProcessData.ExtractMixed || id == LabProcessData.WashMixed
                    || id == LabProcessData.Saturated || id == LabProcessData.Concentrated || id == LabProcessData.BrineAliquot,
                EquipmentTask.SeparateLayers => id == LabProcessData.ExtractLayers || id == LabProcessData.WashLayers,
                EquipmentTask.Clarify => id == LabProcessData.ExtractMixed || id == LabProcessData.WashMixed || HasProcessSediment(id)
                    || CanCollectAqueous(item, AqueousReactions.Precipitate),
                EquipmentTask.GasCollect => id == LabProcessData.MineralWater || CollectableGas(item) != null,
                EquipmentTask.FreezeDry => LabMixtureData.HasSolids(id) || id == LabProcessData.LoadedCrystals,
                EquipmentTask.Seal => id == LabMixtureData.Chalk || id == LabMixtureData.Salt || LabProcessData.IsPackedSolid(id),
                _ => false
            };
        }

        private string ProcessInputPrompt() => Task switch
        {
            EquipmentTask.GasCollect => Loc.Pick("放入含气样品，空载可检查排风", "Load a sample containing gas, or run empty to check ventilation"),
            EquipmentTask.FreezeDry => Loc.Pick("放入悬浊液、沉降液或析晶样", "Load a suspension or crystallized sample"),
            EquipmentTask.Seal => Loc.Pick("放入盐、碳酸钙或已封装固体", "Load salt, calcium carbonate or a sealed solid"),
            _ => null
        };

        private string ProcessBlocker()
        {
            string id = Input?.identifier;
            GameItem bottle = Receiver;
            if (Task == EquipmentTask.Dispense && id == LabMixtureData.Brine &&
                (level != 1 || !IsPortableGlass(bottle) || bottle.unitCount != 1))
                return Loc.Pick("取盐液样需设为 25 mL，并装入空玻璃瓶", "Set 25 mL and load an empty glass bottle");
            if (Task == EquipmentTask.Settle && id == LabProcessData.BrineAliquot &&
                (Input.unitCount != 2 || ReagentBottle.SizeOf(Input) != LabMixtureData.SmallBottle))
                return Loc.Pick("合并为 50 mL 后装入小号试剂瓶", "Pool 50 mL in a small reagent bottle");
            if (Task == EquipmentTask.Mix && id == LabAnalysisData.Dye)
            {
                if (Input.unitCount != LabMixtureData.Portion) return Loc.Pick("需要小号瓶的 50 mL 示踪原液", "Use 50 mL of tracer stock in a small bottle");
                if (bottle?.identifier != LabMixtureData.Oil || bottle.unitCount != LabMixtureData.Portion)
                    return Loc.Pick("再装入 50 mL 实验用油", "Add 50 mL of lab sample oil");
            }
            if (Task == EquipmentTask.GasCollect && Input != null)
            {
                if (level == 1) return Loc.Pick("集气前将排风调至 2 档或以上", "Set airflow to level 2 or higher");
                if (bottle == null || !ReagentBottle.IsGasEmpty(bottle.identifier) || bottle.unitCount != 1)
                    return Loc.Pick("装入一个空采样气瓶或钢瓶", "Load one empty gas vial or cylinder");
            }
            bool needsGlass = Task == EquipmentTask.FreezeDry || (Task == EquipmentTask.Seal && !LabProcessData.IsPackedSolid(id))
                || (Task == EquipmentTask.AnalyzeFluid && id == LabProcessData.OilTrace);
            if (needsGlass && (!IsPortableGlass(bottle) || bottle.unitCount != 1))
                return Loc.Pick("装入一个空玻璃试剂瓶", "Load one empty glass reagent bottle");
            if (Task == EquipmentTask.Seal && !LabProcessData.IsPackedSolid(id) && ReagentBottle.SizeOf(bottle) != LabMixtureData.SmallBottle)
                return Loc.Pick("固体封装使用小号玻璃试剂瓶", "Use a small glass bottle for solid samples");
            if (Task == EquipmentTask.Seal && LabProcessData.IsPackedSolid(id) && bottle != null)
                return Loc.Pick("拆封前请清空接收瓶槽", "Clear the bottle slot before unsealing");
            if (Task == EquipmentTask.AnalyzeFluid && id == LabProcessData.OilTrace && Input.unitCount != LabMixtureData.Portion)
                return Loc.Pick("色谱分收需要 50 mL 油相", "Fraction collection requires 50 mL of extracted oil");
            return null;
        }

        private string HeatingBand() => level == 1 ? Loc.Pick("低温 · 一份溶解", "Low · 1 unit dissolves")
            : level == 2 ? Loc.Pick("中温 · 一份溶解", "Medium · 1 unit dissolves")
            : Loc.Pick("高温 · 两份可溶", "High · up to 2 units dissolve");

        private string ProcessPhase() => Task switch
        {
            EquipmentTask.GasCollect when Input != null => Loc.Pick("气泡释放，导入采样瓶…", "Gas released into receiver..."),
            EquipmentTask.FreezeDry => Loc.Pick("冻干升华，冷阱捕集水分…", "Sublimating and trapping moisture..."),
            EquipmentTask.AnalyzeFluid when Input?.identifier == LabProcessData.OilTrace => Loc.Pick("扫描色谱峰并分段收集…", "Scanning peaks and collecting fractions..."),
            EquipmentTask.Seal => Loc.Pick("隔离舱操作中…", "Working in the isolation chamber..."),
            _ => null
        };

        private string OperateProcess(LabInventoryGrid grid, GameItem input)
        {
            string id = input?.identifier;
            switch (Task)
            {
                case EquipmentTask.Dispense when id == LabMixtureData.Brine:
                    return SampleBrine(grid, input);
                case EquipmentTask.Conductivity when id == LabProcessData.BrineAliquot:
                    return Loc.Pick("电导率：高于纯水对照 · 含溶解盐", "Conductivity above water blank · dissolved salt detected");
                case EquipmentTask.Settle when id == LabProcessData.BrineAliquot:
                    return MachineBatch.TryCommit(grid, new List<(GameItem, int)> { (input, 2) }, (LabMixtureData.Crystals, output))
                        ? Loc.Pick("合并样已析晶", "Pooled sample crystallized") : Failed;
                case EquipmentTask.Mix when id == LabAnalysisData.Dye:
                    return MixExtraction(grid, input);
                case EquipmentTask.Settle or EquipmentTask.Clarify when id == LabProcessData.ExtractMixed:
                    return Commit(grid, input, LabProcessData.ExtractLayers);
                case EquipmentTask.Settle or EquipmentTask.Clarify when id == LabProcessData.WashMixed:
                    return Commit(grid, input, LabProcessData.WashLayers);
                case EquipmentTask.Settle when id == LabProcessData.Saturated || id == LabProcessData.Concentrated:
                    return Commit(grid, input, LabProcessData.LoadedCrystals);
                case EquipmentTask.Filter or EquipmentTask.Clarify when HasProcessSediment(id):
                    return FilterSalt(grid, input);
                case EquipmentTask.SeparateLayers when id == LabProcessData.ExtractLayers || id == LabProcessData.WashLayers:
                    return CollectExtraction(grid, input);
                case EquipmentTask.AnalyzeAny when LabProcessData.IsExtract(id):
                    int signal = LabProcessData.TracerSignal(id);
                    return Loc.Pick($"示踪信号 · 原液的 {signal}%", $"Tracer signal · {signal}% of stock");
                case EquipmentTask.AnalyzeFluid when id == LabProcessData.OilTrace:
                    return CollectChromatogram(grid, input);
                case EquipmentTask.GasCollect:
                    return input == null ? Loc.Pick($"排风正常 · 风量 {level} 档", $"Ventilation normal · level {level}") : CollectGas(grid, input);
                case EquipmentTask.FreezeDry:
                    return FreezeDry(grid, input);
                case EquipmentTask.Seal:
                    return SealSample(grid, input);
                default:
                    return null;
            }
        }

        private string SampleBrine(LabInventoryGrid grid, GameItem sample)
        {
            var inputs = new List<(GameItem, int)> { (sample, 1), (Receiver, 1) };
            return MachineBatch.TryCommitMeasured(grid, inputs,
                new MachineBatch.Product(LabProcessData.BrineAliquot, output, 1, ReagentBottle.SizeOf(Receiver)),
                new MachineBatch.Product(LabProcessData.BrineAliquot, byproduct, 1, LabMixtureData.SmallBottle))
                ? Loc.Pick("取样 25 mL · 余样 25 mL", "25 mL aliquot · 25 mL retained") : Failed;
        }

        private string MixExtraction(LabInventoryGrid grid, GameItem tracer)
        {
            var inputs = new List<(GameItem, int)> { (tracer, LabMixtureData.Portion), (Receiver, LabMixtureData.Portion) };
            return MachineBatch.TryCommitMeasured(grid, inputs, new MachineBatch.Product(LabProcessData.ExtractMixed, output),
                new MachineBatch.Product(ReagentBottle.ReturnedEmpty(Receiver), byproduct))
                ? Loc.Pick("已混合 · 用锥形瓶静置再分液", "Mixed. Settle in the flask before separating.") : Failed;
        }

        private string CollectExtraction(LabInventoryGrid grid, GameItem sample)
        {
            bool washed = sample.identifier == LabProcessData.WashLayers;
            var inputs = new List<(GameItem, int)> { (sample, 1), (Receiver, 1) };
            return MachineBatch.TryCommitMeasured(grid, inputs,
                new MachineBatch.Product(washed ? LabProcessData.WashWater : LabProcessData.WaterTrace, output, 2, LabMixtureData.SmallBottle),
                new MachineBatch.Product(washed ? LabProcessData.WashOil : LabProcessData.OilTrace, byproduct, 2, ReagentBottle.SizeOf(Receiver)))
                ? Loc.Pick("两相各 50 mL · 可用光谱仪对比", "50 mL of each phase. Compare with the spectrometer.") : Failed;
        }

        private string CollectChromatogram(LabInventoryGrid grid, GameItem sample)
        {
            var inputs = new List<(GameItem, int)> { (sample, 2), (Receiver, 1) };
            return MachineBatch.TryCommitMeasured(grid, inputs,
                new MachineBatch.Product(LabProcessData.PeakA, output, 1, ReagentBottle.SizeOf(sample)),
                new MachineBatch.Product(LabProcessData.PeakB, byproduct, 1, ReagentBottle.SizeOf(Receiver)))
                ? Loc.Pick("峰 A 先出 · 峰 B 后出\n各收集 25 mL", "Peak A first · Peak B second\n25 mL collected from each") : Failed;
        }

        private string CollectGas(LabInventoryGrid grid, GameItem sample)
        {
            var inputs = new List<(GameItem, int)> { (sample, 1), (Receiver, 1) };
            return MachineBatch.TryCommitMeasured(grid, inputs,
                new MachineBatch.Product(LabProcessData.CarbonDioxide, output, 1, ReagentBottle.SizeOf(Receiver)),
                new MachineBatch.Product(Water, byproduct, 2, LabMixtureData.SmallBottle))
                ? Loc.Pick("回收二氧化碳 25 mL · 脱气水 50 mL", "Recovered 25 mL CO₂ and 50 mL degassed water") : Failed;
        }

        private string DissolveSalt(LabInventoryGrid grid, GameItem water, GameItem salt)
        {
            int units = Math.Min(2, salt.unitCount);
            string product = units == 1 ? LabMixtureData.Brine
                : level == 3 ? LabProcessData.Concentrated : LabProcessData.Saturated;
            var inputs = new List<(GameItem, int)> { (water, 2), (salt, units) };
            return MachineBatch.TryCommit(grid, inputs, (product, output))
                ? units == 2 && level < 3 ? Loc.Pick("盐已达饱和 · 瓶底有未溶盐", "Saturated · undissolved salt remains")
                    : Loc.Pick("盐已溶解 · 转入锥形瓶冷却", "Salt dissolved. Cool in the flask.") : Failed;
        }

        private string FilterSalt(LabInventoryGrid grid, GameItem sample)
        {
            bool excessOnly = sample.identifier == LabProcessData.Saturated;
            var liquid = excessOnly ? new MachineBatch.Product(LabMixtureData.Brine, output)
                : new MachineBatch.Product(Water, output, 2, LabMixtureData.SmallBottle);
            return MachineBatch.TryCommitMeasured(grid, new List<(GameItem, int)> { (sample, 1) },
                liquid, new MachineBatch.Product(LabMixtureData.SaltCake, byproduct, excessOnly ? 1 : 2))
                ? Loc.Pick("已收集母液及湿盐晶", "Collected mother liquor and wet crystals") : Failed;
        }

        private string FreezeDry(LabInventoryGrid grid, GameItem sample)
        {
            bool salt = sample.identifier == LabMixtureData.Crystals || sample.identifier == LabProcessData.LoadedCrystals;
            int units = sample.identifier == LabProcessData.LoadedCrystals ? 2 : 1;
            var inputs = new List<(GameItem, int)> { (sample, 1), (Receiver, 1) };
            return MachineBatch.TryCommitMeasured(grid, inputs,
                new MachineBatch.Product(salt ? LabMixtureData.Salt : LabMixtureData.Chalk, output, units),
                new MachineBatch.Product(Water, byproduct, 2, ReagentBottle.SizeOf(Receiver)),
                new MachineBatch.Product(ReagentBottle.GlassEmpties[LabMixtureData.SmallBottle], receiver))
                ? Loc.Pick("已回收干固体及 50 mL 冷阱水\n空原瓶已退回接收槽", "Recovered dry solid and 50 mL cold-trap water\nSource bottle returned")
                : Failed;
        }

        private string SealSample(LabInventoryGrid grid, GameItem sample)
        {
            if (!LabProcessData.IsPackedSolid(sample.identifier))
            {
                string sealedId = sample.identifier == LabMixtureData.Salt ? LabProcessData.SealedSalt : LabProcessData.SealedChalk;
                return MachineBatch.TryCommit(grid, new List<(GameItem, int)> { (sample, 1), (Receiver, 1) }, (sealedId, output))
                    ? Loc.Pick("样品已隔离封装", "Sample sealed under isolation") : Failed;
            }
            string solid = sample.identifier == LabProcessData.SealedSalt ? LabMixtureData.Salt : LabMixtureData.Chalk;
            return MachineBatch.TryCommit(grid, new List<(GameItem, int)> { (sample, 1) },
                (solid, output), (ReagentBottle.GlassEmpties[LabMixtureData.SmallBottle], byproduct))
                ? Loc.Pick("已拆封，可取出固体和空瓶", "Unsealed. Take the solid and empty bottle.") : Failed;
        }

    }
}
