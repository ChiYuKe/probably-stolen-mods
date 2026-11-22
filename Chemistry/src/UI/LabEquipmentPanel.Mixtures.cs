using System.Collections.Generic;

namespace Chemistry
{
    internal sealed partial class LabEquipmentPanel
    {
        private GameSlotInventory byproduct;

        private void BuildMixtureReceiver(List<MachineryPanel> sections)
        {
            if (Task != EquipmentTask.Mix && Task != EquipmentTask.SeparateLayers) return;
            receiver = new EquipmentSlot();
            receiver.mayInventoryAddItemFunc = (item, _) => CanLoad(receiver, item)
                && (Task == EquipmentTask.Mix ? IsMixIngredient(item) : IsPortableGlass(item));
            sections.Add(MachineHelper.Panel(Task == EquipmentTask.Mix ? Loc.Pick("加料 · 固体或液体", "Add solids or liquids")
                : Loc.Pick("上层接收瓶", "Upper-layer bottle"), receiver, "bottle"));
        }

        private void BuildMixtureOutput(List<MachineryPanel> sections)
        {
            string title = Task switch
            {
                EquipmentTask.Mix => Loc.Pick("退回空瓶", "Returned bottle"),
                EquipmentTask.SeparateLayers => Loc.Pick("上层 · 油", "Upper layer · Oil"),
                EquipmentTask.Distill => Loc.Pick("余液 / 残留物", "Residue"),
                EquipmentTask.GasCollect => Loc.Pick("剩余溶液", "Remaining liquid"),
                EquipmentTask.FreezeDry => Loc.Pick("冷阱回收水", "Cold-trap water"),
                EquipmentTask.Seal => Loc.Pick("返还空瓶", "Returned bottle"),
                EquipmentTask.AnalyzeFluid => Loc.Pick("色谱 B 馏分", "Chromatography fraction B"),
                EquipmentTask.Dispense when SupportsBrineSampling => Loc.Pick("剩余样品", "Remaining sample"),
                EquipmentTask.Titrate => Loc.Pick("退回空滴定瓶", "Returned titrant bottle"),
                EquipmentTask.Filter or EquipmentTask.Clarify => Loc.Pick("固体残留", "Solid residue"),
                _ => null
            };
            if (title == null) return;
            byproduct = new GameSlotInventory(2, 2);
            byproduct.mayInventoryAddItemFunc = (item, _) => MachineBatch.IsPlacing(item, byproduct);
            sections.Add(MachineHelper.Panel(title, byproduct, "output"));
        }

        private static bool IsCleanableGlass(GameItem item)
        {
            string id = item.identifier;
            return IsPortableGlass(item) || id == ChemistryCatalog.ToolItemId("test_tube")
                || id == ChemistryCatalog.ToolItemId("watch_glass") || id == ChemistryCatalog.ToolItemId("beaker")
                || id == ChemistryCatalog.ToolItemId("conical_flask");
        }

        private static bool IsMixIngredient(GameItem item) =>
            item.identifier == LabMixtureData.Chalk || item.identifier == LabMixtureData.Salt
            || AqueousReactions.IsSolidReagent(item.identifier)
            || (item.identifier == LabProcessData.CarbonDioxide && MaterialItemArt.IsSmallGasVial(item.spritePath))
            || (IsMixLiquid(item.identifier) && ReagentBottle.SizeOf(item) != ReagentBottle.Bulk);

        private static bool IsMixLiquid(string id) => id == LabMixtureData.Oil || id == LabDistillationData.Ethanol
            || id == LabAnalysisData.Dye || id == LabProcessData.OilTrace;

        private bool AcceptsMixtureInput(GameItem item)
        {
            string id = item.identifier;
            return Task switch
            {
                EquipmentTask.Mix => (AqueousReactions.CanPrepare(item) && ReagentBottle.SizeOf(item) != ReagentBottle.Bulk)
                    || id == LabMixtureData.Layers || id == LabMixtureData.Settled,
                EquipmentTask.Settle => id == LabMixtureData.Emulsion || id == LabMixtureData.Suspension || id == LabMixtureData.Brine,
                EquipmentTask.SeparateLayers => id == LabMixtureData.Layers,
                EquipmentTask.Clarify => LabMixtureData.HasSolids(id) || id == LabMixtureData.Emulsion
                    || LabRefining.FindIntermediate(id)?.Filtered == false,
                EquipmentTask.Dry => id == LabMixtureData.Cake || id == LabMixtureData.SaltCake || IsCleanableGlass(item),
                _ => false
            };
        }

        private string MixtureInputPrompt() => Task switch
        {
            EquipmentTask.Mix => Loc.Pick("装入水、乙酸、水溶液或已分层样品", "Load water, acetic acid, an aqueous solution or a layered sample"),
            EquipmentTask.Settle => Loc.Pick("装入混合样、悬浊液或盐溶液", "Load emulsion, suspension or brine"),
            EquipmentTask.SeparateLayers => Loc.Pick("装入已静置的双层油水样", "Load a settled oil-water sample"),
            EquipmentTask.Clarify => Loc.Pick("装入混合样、悬浊液或浸出液", "Load emulsion, suspension or leachate"),
            EquipmentTask.Dry => Loc.Pick("装入湿滤渣、湿盐晶或空器皿", "Load wet residue, crystals or glassware"),
            _ => null
        };

        private string MixtureBlocker()
        {
            if (Task == EquipmentTask.Mix)
            {
                if (AqueousReactions.IsSolidReagent(Receiver?.identifier))
                {
                    if (!AqueousReactions.CanPrepare(Input))
                        return Loc.Pick("先装入水、乙酸或水溶液", "Load water, acetic acid or an aqueous solution first");
                    if (AqueousReactions.IsSodium(Receiver.identifier) && !AqueousReactions.HasWater(Input))
                        return Loc.Pick("这个样品里需要有水", "This sample must contain water");
                    return null;
                }
                if (Input.identifier == LabAnalysisData.Dye) return null;
                if (AqueousReactions.CanPrepare(Input) && Input.identifier != Water)
                    return Loc.Pick("装入支持的固体试剂", "Load a supported solid reagent");
                if (Input.identifier != Water)
                    return Receiver == null ? null : Loc.Pick("摇匀样品前请清空加料槽", "Clear the ingredient slot before remixing");
                if (Input.unitCount != LabMixtureData.Portion || ReagentBottle.SizeOf(Input) != LabMixtureData.SmallBottle)
                    return Loc.Pick("小号水瓶需装 50 mL", "The small water bottle must hold 50 mL");
                if (Receiver == null || !IsMixIngredient(Receiver))
                    return Loc.Pick("装入第二原料：油、乙醇、示踪液或固体试剂", "Load a second ingredient such as oil, ethanol, tracer or a solid reagent");
                if (IsMixLiquid(Receiver.identifier) && Receiver.unitCount != LabMixtureData.Portion)
                    return Loc.Pick("第二瓶液体需装 50 mL", "The second bottle must hold 50 mL");
            }
            if (Task == EquipmentTask.SeparateLayers && !IsPortableGlass(Receiver))
            {
                return Loc.Pick("装入空玻璃试剂瓶收集上层", "Load an empty glass bottle for the upper layer");
            }
            return null;
        }

        private string MixturePhase() => Task switch
        {
            EquipmentTask.Settle when Input?.identifier == LabMixtureData.Emulsion => Loc.Pick("油水分层中…", "Layers forming..."),
            EquipmentTask.Settle when Input?.identifier == LabMixtureData.Brine => Loc.Pick("冷却析晶中…", "Crystals forming..."),
            EquipmentTask.Settle => Loc.Pick("颗粒沉降中…", "Particles settling..."),
            EquipmentTask.SeparateLayers => Loc.Pick("沿分界面收集上下层…", "Collecting the two layers..."),
            EquipmentTask.Clarify when Input?.identifier == LabMixtureData.Emulsion => Loc.Pick("离心加速分层…", "Accelerating layer separation..."),
            _ => null
        };

        private string OperateMixture(LabInventoryGrid grid, GameItem input)
        {
            string id = input?.identifier;
            switch (Task)
            {
                case EquipmentTask.Mix:
                    if (AqueousReactions.IsSolidReagent(Receiver?.identifier)) return PrepareAqueousSample(grid, input);
                    if (id == LabMixtureData.Layers) return Commit(grid, input, LabMixtureData.Emulsion);
                    if (id == LabMixtureData.Settled) return Commit(grid, input, LabMixtureData.Suspension);
                    return Mix(grid, input, Receiver);
                case EquipmentTask.Settle:
                    return Commit(grid, input, id == LabMixtureData.Emulsion ? LabMixtureData.Layers
                        : id == LabMixtureData.Brine ? LabMixtureData.Crystals : LabMixtureData.Settled);
                case EquipmentTask.SeparateLayers:
                    return MachineBatch.TryCommitMeasured(grid, new List<(GameItem, int)> { (input, 1), (Receiver, 1) },
                        new MachineBatch.Product(Water, output, LabMixtureData.Portion, LabMixtureData.SmallBottle),
                        new MachineBatch.Product(LabMixtureData.Oil, byproduct, LabMixtureData.Portion, ReagentBottle.SizeOf(Receiver)))
                        ? Loc.Pick("上层油 50 mL · 下层水 50 mL", "50 mL upper oil · 50 mL lower water") : Failed;
                case EquipmentTask.Clarify when id == LabMixtureData.Emulsion:
                    return Commit(grid, input, LabMixtureData.Layers, Loc.Pick("已分层，可转入分液漏斗", "Layered. Transfer to the separatory funnel."));
                case EquipmentTask.Filter or EquipmentTask.Clarify when LabMixtureData.HasSolids(id):
                    string residue = id == LabMixtureData.Crystals ? LabMixtureData.SaltCake : LabMixtureData.Cake;
                    return MachineBatch.TryCommitMeasured(grid, new List<(GameItem, int)> { (input, 1) },
                        new MachineBatch.Product(Water, output, LabMixtureData.Portion, LabMixtureData.SmallBottle),
                        new MachineBatch.Product(residue, byproduct))
                        ? Loc.Pick("已收集 50 mL 澄清液和湿固体", "Collected 50 mL of clear liquid and wet solid") : Failed;
                case EquipmentTask.Dry:
                    if (id == LabMixtureData.Cake) return Commit(grid, input, LabMixtureData.Chalk);
                    if (id == LabMixtureData.SaltCake) return Commit(grid, input, LabMixtureData.Salt);
                    return Loc.Pick("空器皿干燥循环完成", "Empty glassware cycle complete");
                default:
                    return null;
            }
        }

        private string Mix(LabInventoryGrid grid, GameItem water, GameItem ingredient)
        {
            if (ingredient.identifier == LabAnalysisData.Dye) return DiluteTracer(grid, water, ingredient);
            if (ingredient.identifier == LabMixtureData.Salt && equipment.Key == "hotplate_stirrer")
                return DissolveSalt(grid, water, ingredient);
            bool liquid = IsMixLiquid(ingredient.identifier);
            string batch = ingredient.identifier switch
            {
                LabMixtureData.Oil => LabMixtureData.Emulsion,
                LabDistillationData.Ethanol => LabDistillationData.Feed,
                LabProcessData.CarbonDioxide => LabProcessData.MineralWater,
                LabProcessData.OilTrace => LabProcessData.WashMixed,
                LabMixtureData.Salt => LabMixtureData.Brine,
                _ => LabMixtureData.Suspension
            };
            var inputs = new List<(GameItem, int)>
            {
                (water, LabMixtureData.Portion), (ingredient, liquid ? LabMixtureData.Portion : 1)
            };
            var product = new MachineBatch.Product(batch, output);
            bool returnBottle = liquid || (ingredient.identifier == LabProcessData.CarbonDioxide && ingredient.unitCount == 1);
            bool complete = returnBottle
                ? MachineBatch.TryCommitMeasured(grid, inputs, product,
                    new MachineBatch.Product(ReagentBottle.ReturnedEmpty(ingredient), byproduct))
                : MachineBatch.TryCommitMeasured(grid, inputs, product);
            return complete ? Loc.Pick("已装好混合样，拖出继续处理", "Sample ready. Drag it out to continue.") : Failed;
        }
    }
}
