using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chemistry
{
    /// <summary>
    /// 掉落表接入：让未知样品、化合物和工具能在垃圾场里被翻出来。
    /// 表 id 是从游戏里导出来的真实表（NeteaseCassette 的掉落导出），
    /// 版本更新如果表名变了，F9 里的「掉落表检查」会打日志，改这里的常量即可。
    /// </summary>
    internal static class LootIntegration
    {
        public const string SampleTableId = "chemSampleTable";
        public const string CompoundTableId = "chemCompoundTable";
        /// <summary>
        /// 早期版本把 118 种单质直接挂进普通垃圾场（权重 8）。M1 起不再直掉：
        /// 元素只能从样品提炼或原料采购拿到。这里保留常量只用于清理旧存档/旧版本残留的挂载。
        /// </summary>
        private const string LegacyElementTableId = "chemElementTable";

        /// <summary>样品表塞进哪个表组：普通垃圾场 + 新货垃圾场。</summary>
        private const string DumpingGroundGroup = "dumpingGroundTG";
        private const string DumpingGroundFreshGroup = "dumpingGroundFreshTG";

        /// <summary>原版工具表，我们的化学工具直接按权重加进去。</summary>
        private const string VanillaToolTable = "toolTable";

        private static bool applied;

        /// <summary>首翻保底样品是否已经发过（存进模组存档，新档重置）。</summary>
        private const string FirstSampleKey = "loot_first_sample";

        private static bool hooked;
        private static bool pendingFirstSample;

        public static bool Applied
        {
            get { return applied; }
        }

        public static void Apply()
        {
            if (applied)
            {
                return;
            }

            try
            {
                RegisterSampleTable();
                RegisterCompoundTable();
                RemoveLegacyElementTable();
                AddToolsToVanillaTable();
                applied = true;
                ChemistryMod.Log?.Log("掉落表接入完成：未知样品 / 化合物 / 化学工具已进入垃圾场掉落"
                    + "（单质直掉已移除，元素改走样品提炼和原料采购）");
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("接入掉落表失败（不影响合成功能）：" + e);
            }
        }

        private static void RegisterSampleTable()
        {
            var entries = new List<(string itemId, float weight)>();
            foreach (SampleDef sample in SampleTable.All)
            {
                if (sample.Weight > 0)
                {
                    entries.Add((sample.ItemId, sample.Weight));
                }
            }

            ModHelper.RegisterLootTable(ChemistryCatalog.ModId, SampleTableId, entries.ToArray());
            ModHelper.AddLootGroupEntry(ChemistryCatalog.ModId, DumpingGroundGroup, SampleTableId, 60f);
            ModHelper.AddLootGroupEntry(ChemistryCatalog.ModId, DumpingGroundFreshGroup, SampleTableId, 40f);
        }

        private static void RegisterCompoundTable()
        {
            var entries = new List<(string itemId, float weight)>();
            foreach (CompoundDef def in ChemistryCatalog.Compounds)
            {
                if (def.Category != "acid")
                {
                    entries.Add((def.ItemId, ChemistryCatalog.CompoundLootWeight(def)));
                }
            }

            ModHelper.RegisterLootTable(ChemistryCatalog.ModId, CompoundTableId, entries.ToArray());
            ModHelper.AddLootGroupEntry(ChemistryCatalog.ModId, DumpingGroundGroup, CompoundTableId, 25f);
        }

        /// <summary>把旧版本留下的单质直掉挂载撤掉（新版本根本不再注册它）。</summary>
        private static void RemoveLegacyElementTable()
        {
            try
            {
                ModHelper.RemoveLootGroupEntry(ChemistryCatalog.ModId, DumpingGroundGroup, LegacyElementTableId);
                ModHelper.ClearLootTable(ChemistryCatalog.ModId, LegacyElementTableId);
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("清理旧版单质掉落表失败（不影响游戏）：" + e.Message);
            }
        }

        private static void AddToolsToVanillaTable()
        {
            ModHelper.AddLootEntry(ChemistryCatalog.ModId, VanillaToolTable, ReagentBottle.EmptyId, 12f);
            ModHelper.AddLootEntry(ChemistryCatalog.ModId, VanillaToolTable, ReagentBottle.PlasticEmptyId, 8f);
            foreach (string id in BulkContainers.ItemIds)
            {
                if (ReagentBottle.IsEmptyTarget(id))
                {
                    ModHelper.AddLootEntry(ChemistryCatalog.ModId, VanillaToolTable, id, 3f);
                }
            }
            foreach (int size in new[] { 0, 2 })
            {
                ModHelper.AddLootEntry(ChemistryCatalog.ModId, VanillaToolTable, ReagentBottle.GlassEmpties[size], 4f);
                ModHelper.AddLootEntry(ChemistryCatalog.ModId, VanillaToolTable, ReagentBottle.PlasticEmpties[size], 3f);
            }
            foreach (string id in ReagentBottle.GasEmpties)
                ModHelper.AddLootEntry(ChemistryCatalog.ModId, VanillaToolTable, id, 4f);
            ModHelper.AddLootEntry(ChemistryCatalog.ModId, VanillaToolTable, NitricProductionData.Parts, 20f);
            ModHelper.AddLootEntry(ChemistryCatalog.ModId, VanillaToolTable, NitricProductionData.Power, 12f);
            ModHelper.AddLootEntry(ChemistryCatalog.ModId, VanillaToolTable, LabMixtureData.Oil, 5f);
            foreach (string id in new[] { LabAnalysisData.Carbon, LabAnalysisData.Dye, LabAnalysisData.Acid, LabAnalysisData.Titrant })
            {
                ModHelper.AddLootEntry(ChemistryCatalog.ModId, VanillaToolTable, id, 5f);
            }
            foreach (ToolDef def in ChemistryCatalog.Tools)
            {
                int tier = LabEquipmentData.Find(def.Key)?.Tier ?? 1;
                ModHelper.AddLootEntry(ChemistryCatalog.ModId, VanillaToolTable, def.ItemId, tier == 1 ? 4f : tier == 2 ? 2.5f : 1.25f);
            }
        }

        /// <summary>
        /// 首翻保底：第一次在垃圾场翻出东西时，如果抽到的不是可送检样品，就换成批次 01。
        /// 只保底一次（存档记 `loot_first_sample`），其余掉落仍按现有掉落表随机。
        /// </summary>
        public static void Hook()
        {
            if (hooked)
            {
                return;
            }

            try
            {
                ModHook.OnLootRolled += OnLootRolled;
                hooked = true;
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("挂掉落保底失败（不影响玩耍）：" + e.Message);
            }
        }

        public static void Unhook()
        {
            if (!hooked)
            {
                return;
            }

            try
            {
                ModHook.OnLootRolled -= OnLootRolled;
            }
            catch
            {
                // 关游戏时事件表可能已经清空，忽略
            }

            hooked = false;
        }

        private static void OnLootRolled(LootRollContext context)
        {
            try
            {
                if (context == null || string.IsNullOrEmpty(context.itemId))
                {
                    return;
                }

                string group = context.groupId ?? "";
                if (!group.StartsWith(DumpingGroundGroup, StringComparison.Ordinal)
                    && !group.StartsWith(DumpingGroundFreshGroup, StringComparison.Ordinal))
                {
                    return;
                }

                if (HasFirstSample() || pendingFirstSample)
                {
                    return;
                }

                // 非铁样品不能消耗铁路线保底；改成批次 01，直到含铁样品确实进入玩家库存。
                SampleDef rolled = SampleTable.Find(context.itemId);
                if (!ContainsIron(rolled))
                {
                    SampleDef fallback = SampleTable.Find(ChemistryCatalog.ItemPrefix + "sample_01");
                    if (fallback == null)
                    {
                        return;
                    }

                    context.itemId = fallback.ItemId;
                    ChemistryMod.Log?.Log("首翻保底：把掉落换成含铁的批次 01 未知金属碎片");
                }

                pendingFirstSample = true;
                ScheduleConfirmFirstSample();
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("掉落保底处理失败：" + e.Message);
            }
        }

        private static void ScheduleConfirmFirstSample()
        {
            try
            {
                ModHelper.Delay(ChemistryCatalog.ModId, 0.5f, ConfirmFirstSampleCollected);
            }
            catch (Exception e)
            {
                pendingFirstSample = false;
                ChemistryMod.Log?.Warning("安排保底确认失败：" + e.Message);
            }
        }

        private static bool ContainsIron(SampleDef sample)
        {
            return sample?.Components != null
                && Array.Exists(sample.Components, component => string.Equals(component.Symbol, "Fe", StringComparison.Ordinal));
        }

        /// <summary>只有已入库的含铁样品才永久消耗首翻保底。</summary>
        private static void ConfirmFirstSampleCollected()
        {
            if (HasFirstSample())
            {
                pendingFirstSample = false;
                return;
            }

            try
            {
                Dictionary<string, int> counts = StoreInventory.CountUnits();
                foreach (SampleDef sample in SampleTable.All)
                {
                    if (!ContainsIron(sample))
                    {
                        continue;
                    }

                    if ((counts.TryGetValue(sample.ItemId, out int raw) && raw > 0)
                        || (counts.TryGetValue(sample.DetectedId, out int detected) && detected > 0))
                    {
                        MarkFirstSample();
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("确认首翻样品失败：" + e.Message);
            }
            finally
            {
                pendingFirstSample = false;
            }
        }

        /// <summary>查看含铁样品的成分报告时，这是实际持有该样品的确定证据。</summary>
        public static void ConfirmSampleAnalyzed(SampleDef sample)
        {
            if (ContainsIron(sample))
            {
                MarkFirstSample();
            }
        }

        private static bool HasFirstSample()
        {
            try
            {
                return ModHelper.GetModDataBool(ChemistryCatalog.ModId, FirstSampleKey, false);
            }
            catch
            {
                return true;
            }
        }

        private static void MarkFirstSample()
        {
            try
            {
                ModHelper.SetModDataBool(ChemistryCatalog.ModId, FirstSampleKey, true);
                pendingFirstSample = false;
            }
            catch (Exception e)
            {
                ChemistryMod.Log?.Warning("记录首翻保底失败：" + e.Message);
            }
        }

        /// <summary>新档时清掉保底标记，让下一档也能保底一次。</summary>
        public static void ResetFirstSample()
        {
            pendingFirstSample = false;
            try
            {
                ModHelper.SetModDataBool(ChemistryCatalog.ModId, FirstSampleKey, false);
            }
            catch
            {
                // 忽略
            }
        }

        /// <summary>模组被禁用时尽量把加进去的条目撤掉，避免重载后权重翻倍。</summary>
        public static void Remove()
        {
            if (!applied)
            {
                return;
            }

            try
            {
                foreach (string[] family in ReagentBottle.EmptyFamilies)
                {
                    foreach (string id in family)
                    {
                        ModHelper.RemoveLootEntry(ChemistryCatalog.ModId, VanillaToolTable, id);
                    }
                }
                foreach (string id in BulkContainers.ItemIds)
                {
                    if (ReagentBottle.IsEmptyTarget(id))
                    {
                        ModHelper.RemoveLootEntry(ChemistryCatalog.ModId, VanillaToolTable, id);
                    }
                }
                ModHelper.RemoveLootEntry(ChemistryCatalog.ModId, VanillaToolTable, NitricProductionData.Parts);
                ModHelper.RemoveLootEntry(ChemistryCatalog.ModId, VanillaToolTable, NitricProductionData.Power);
                ModHelper.RemoveLootEntry(ChemistryCatalog.ModId, VanillaToolTable, LabMixtureData.Oil);
                foreach (string id in new[] { LabAnalysisData.Carbon, LabAnalysisData.Dye, LabAnalysisData.Acid, LabAnalysisData.Titrant })
                {
                    ModHelper.RemoveLootEntry(ChemistryCatalog.ModId, VanillaToolTable, id);
                }
                foreach (ToolDef def in ChemistryCatalog.Tools)
                {
                    ModHelper.RemoveLootEntry(ChemistryCatalog.ModId, VanillaToolTable, def.ItemId);
                }

                ModHelper.RemoveLootGroupEntry(ChemistryCatalog.ModId, DumpingGroundGroup, SampleTableId);
                ModHelper.RemoveLootGroupEntry(ChemistryCatalog.ModId, DumpingGroundFreshGroup, SampleTableId);
                ModHelper.RemoveLootGroupEntry(ChemistryCatalog.ModId, DumpingGroundGroup, CompoundTableId);
                ModHelper.RemoveLootGroupEntry(ChemistryCatalog.ModId, DumpingGroundGroup, LegacyElementTableId);
                ModHelper.ClearLootTable(ChemistryCatalog.ModId, SampleTableId);
                ModHelper.ClearLootTable(ChemistryCatalog.ModId, CompoundTableId);
                ModHelper.ClearLootTable(ChemistryCatalog.ModId, LegacyElementTableId);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Chemistry] 撤销掉落表条目失败：" + e.Message);
            }

            applied = false;
        }

        /// <summary>F9 里的诊断入口：看看我们的表加进去了没有、权重多少。</summary>
        public static void LogState()
        {
            ModLog log = ChemistryMod.Log;
            if (log == null)
            {
                return;
            }

            log.Log("掉落表状态：已接入 = " + applied + "（单质直掉已移除）");
            ChemistryDebugWindow.Report(applied
                ? Loc.Pick("化学掉落表已接入，垃圾场不再直掉单质。条目明细见游戏日志。",
                    "Chemistry loot tables are active; the dumping ground no longer drops raw elements. See the game log for entries.")
                : Loc.Pick("化学掉落表尚未接入。详情见游戏日志。", "Chemistry loot tables are inactive. See the game log for details."), applied);
            LogTable(log, SampleTableId);
            LogTable(log, CompoundTableId);
            LogTable(log, LegacyElementTableId);

            try
            {
                List<LootEntry> tools = ModHelper.GetLootEntries(VanillaToolTable);
                log.Log("原版工具表条目数：" + (tools != null ? tools.Count : 0));
            }
            catch (Exception e)
            {
                log.Warning("读取工具表失败：" + e.Message);
            }

        }

        private static void LogTable(ModLog log, string tableId)
        {
            try
            {
                List<LootEntry> entries = ModHelper.GetLootEntries(tableId);
                log.Log(tableId + "：" + (entries != null ? entries.Count : 0) + " 条");
            }
            catch (Exception e)
            {
                log.Warning("读取表 " + tableId + " 失败：" + e.Message);
            }
        }
    }
}
