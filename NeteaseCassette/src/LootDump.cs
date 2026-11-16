using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace NeteaseCassette
{
    // 把当前（原版 + 模组改动后的）掉落表导成一份 json，方便离线抄成文档
    public static class LootDump
    {
        private const string FileName = "loot_dump.json";
        private static bool done;

        public static void Run()
        {
            if (done) return;

            try
            {
                if (!LootRegistry.TableIds.Any()) return;   // 表还没载入，下次再试
                done = true;
                var tables = new Dictionary<string, object>();
                foreach (string id in LootRegistry.TableIds.OrderBy(x => x, StringComparer.Ordinal))
                {
                    List<LootEntry> entries = LootRegistry.GetTableEntries(id) ?? new List<LootEntry>();
                    tables[id] = new
                    {
                        total = entries.Sum(e => e.weight),
                        entries = entries.Select(e => new { id = e.id, weight = e.weight }).ToList()
                    };
                }

                var groups = new Dictionary<string, object>();
                foreach (string id in LootRegistry.GroupIds.OrderBy(x => x, StringComparer.Ordinal))
                {
                    List<LootEntry> entries = LootRegistry.GetGroupEntries(id) ?? new List<LootEntry>();
                    groups[id] = new
                    {
                        total = entries.Sum(e => e.weight),
                        entries = entries.Select(e => new { id = e.id, weight = e.weight }).ToList()
                    };
                }

                var root = new
                {
                    generatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    tables,
                    groups
                };

                ModHelper.WriteModDataFileText(ModEntry.ModId, FileName, JsonConvert.SerializeObject(root, Formatting.Indented));
                ModEntry.Log?.Log("掉落表已导出：" + tables.Count + " 张表、" + groups.Count + " 个表组");
            }
            catch (Exception e)
            {
                ModEntry.Log?.Warning("导出掉落表失败：" + e.Message);
            }
        }
    }
}
