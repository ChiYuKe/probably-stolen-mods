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
        private const string ElementTableId = "chemElementTable";

        /// <summary>样品表塞进哪个表组：普通垃圾场 + 新货垃圾场。</summary>
        private const string DumpingGroundGroup = "dumpingGroundTG";
        private const string DumpingGroundFreshGroup = "dumpingGroundFreshTG";

        /// <summary>原版工具表，我们的化学工具直接按权重加进去。</summary>
        private const string VanillaToolTable = "toolTable";

        private static bool applied;

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
                RegisterElementTable();
                AddToolsToVanillaTable();
                applied = true;
                ChemistryMod.Log?.Log("掉落表接入完成：未知样品 / 化合物 / 化学工具已进入垃圾场掉落");
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

        private static void RegisterElementTable()
        {
            var entries = new List<(string itemId, float weight)>();
            foreach (ElementDef element in ChemistryCatalog.Elements)
                entries.Add((element.ItemId, element.Number <= 20 ? 4f : element.Number > 92 ? 0.15f : 1.5f));
            ModHelper.RegisterLootTable(ChemistryCatalog.ModId, ElementTableId, entries.ToArray());
            ModHelper.AddLootGroupEntry(ChemistryCatalog.ModId, DumpingGroundGroup, ElementTableId, 8f);
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
                ModHelper.RemoveLootGroupEntry(ChemistryCatalog.ModId, DumpingGroundGroup, ElementTableId);
                ModHelper.ClearLootTable(ChemistryCatalog.ModId, SampleTableId);
                ModHelper.ClearLootTable(ChemistryCatalog.ModId, CompoundTableId);
                ModHelper.ClearLootTable(ChemistryCatalog.ModId, ElementTableId);
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

            log.Log("掉落表状态：已接入 = " + applied);
            ChemistryDebugWindow.Report(applied
                ? Loc.Pick("化学掉落表已接入。条目明细见游戏日志。", "Chemistry loot tables are active. See the game log for entries.")
                : Loc.Pick("化学掉落表尚未接入。详情见游戏日志。", "Chemistry loot tables are inactive. See the game log for details."), applied);
            LogTable(log, SampleTableId);
            LogTable(log, CompoundTableId);
            LogTable(log, ElementTableId);

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
