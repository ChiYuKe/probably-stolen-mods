using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Chemistry
{
    /// <summary>
    /// 实验室专用储物格，收纳化学物品、样品和实验设备，随存档持久化。
    /// 契约见 data/LAB_CONTRACT.md「API 契约 / LabStorage」。
    /// </summary>
    internal sealed class LabSlot
    {
        public string ItemId;
        public int Count;
    }

    internal static class LabStorage
    {
        public const int BaseCapacity = 344;
        private const int MaxCapacityBonus = 10000;
        private static int capacityBonus = 300;
        public static int Capacity => Math.Max(BaseCapacity + capacityBonus, slots?.Length ?? 0);
        public const int MaxCountPerSlot = 999;

        /// <summary>ModHelper 存档数据 key：内容形如 "chem_el_h:12|chem_tool_beaker:1"。</summary>
        internal const string DataKey = "lab_slots";

        private static LabSlot[] slots = CreateSlots();
        private static bool loaded;
        internal static string BottleLayout { get; private set; } = "";
        internal static string LineLayout { get; private set; } = "";

        /// <summary>电池等电源物品的逐件明细（id@电量:数量），随储物格一起存档。
        /// 电源不在化学白名单里，单独走这一段，避免挡掉整次保存。</summary>
        internal static string PowerLayout { get; private set; } = "";
        internal static string EquipmentLayout { get; private set; } = "";

        internal static void ConfigureCapacityBonus(int bonus)
        {
            capacityBonus = Math.Max(0, Math.Min(MaxCapacityBonus, bonus));
            int target = BaseCapacity + capacityBonus;
            if (target == slots.Length)
            {
                return;
            }

            // Never discard stored items when the setting is reduced mid-session.
            if (target < slots.Length)
            {
                for (int i = target; i < slots.Length; i++)
                {
                    if (!IsEmpty(slots[i]))
                    {
                        return;
                    }
                }
            }

            int previousLength = slots.Length;
            Array.Resize(ref slots, target);
            for (int i = previousLength; i < slots.Length; i++)
            {
                slots[i] = new LabSlot();
            }
        }

        public static bool Loaded
        {
            get { return loaded; }
        }

        public static LabSlot[] Slots
        {
            get { return slots; }
        }

        public static int UsedSlots
        {
            get
            {
                int used = 0;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!IsEmpty(slots[i]))
                    {
                        used++;
                    }
                }

                return used;
            }
        }

        private static LabSlot[] CreateSlots()
        {
            var result = new LabSlot[BaseCapacity + capacityBonus];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new LabSlot();
            }

            return result;
        }

        private static bool IsEmpty(LabSlot slot)
        {
            return slot == null || string.IsNullOrEmpty(slot.ItemId) || slot.Count <= 0;
        }

        private static void ClearSlots()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].ItemId = null;
                slots[i].Count = 0;
            }
        }

        // ---------------------------------------------------------------
        // 载入 / 保存
        // ---------------------------------------------------------------

        /// <summary>
        /// 从存档读回储物格。读档、进新游戏、进入实验室时都会调；
        /// 任何时候读到损坏/非法数据都按空处理，绝不把异常抛给游戏。
        /// </summary>
        public static void EnsureLoaded()
        {
            loaded = true;
            ClearSlots();
            BottleLayout = "";
            LineLayout = "";
            PowerLayout = "";
            EquipmentLayout = "";

            try
            {
                string raw = ModHelper.GetModData(ChemistryCatalog.ModId, DataKey);
                int boundary = raw?.IndexOf('~') ?? -1;
                Parse(boundary < 0 ? raw : raw.Substring(0, boundary));
                if (boundary >= 0)
                {
                    int lineBoundary = raw.IndexOf('~', boundary + 1);
                    BottleLayout = lineBoundary < 0 ? raw.Substring(boundary + 1)
                        : raw.Substring(boundary + 1, lineBoundary - boundary - 1);
                    if (lineBoundary >= 0)
                    {
                        int powerBoundary = raw.IndexOf('~', lineBoundary + 1);
                        LineLayout = powerBoundary < 0 ? raw.Substring(lineBoundary + 1)
                            : raw.Substring(lineBoundary + 1, powerBoundary - lineBoundary - 1);
                        if (powerBoundary >= 0)
                        {
                            int equipmentBoundary = raw.IndexOf('~', powerBoundary + 1);
                            PowerLayout = equipmentBoundary < 0 ? raw.Substring(powerBoundary + 1)
                                : raw.Substring(powerBoundary + 1, equipmentBoundary - powerBoundary - 1);
                            if (equipmentBoundary >= 0) EquipmentLayout = raw.Substring(equipmentBoundary + 1);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ClearSlots();
                BottleLayout = "";
                LineLayout = "";
                PowerLayout = "";
                EquipmentLayout = "";
                Debug.LogWarning("[Chemistry] 读取实验室储物格失败，按空处理：" + e.Message);
            }
        }

        private static void Parse(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            // 先按 id 合并（防同一 id 出现在多段里），再顺序填格子。
            var order = new List<string>();
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);

            string[] entries = raw.Split('|');
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                entry = entry.Trim();
                if (entry.Length == 0)
                {
                    continue;
                }

                int colon = entry.LastIndexOf(':');
                if (colon <= 0 || colon >= entry.Length - 1)
                {
                    continue;
                }

                string itemId = entry.Substring(0, colon).Trim();
                if (!IsAllowed(itemId))
                {
                    continue;
                }

                int count;
                if (!int.TryParse(entry.Substring(colon + 1).Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out count))
                {
                    continue;
                }

                if (count <= 0)
                {
                    continue;
                }

                if (count > MaxCountPerSlot)
                {
                    count = MaxCountPerSlot;
                }

                int existing;
                if (totals.TryGetValue(itemId, out existing))
                {
                    totals[itemId] = Math.Min(Capacity * MaxCountPerSlot, existing + count);
                }
                else
                {
                    order.Add(itemId);
                    totals[itemId] = count;
                }
            }

            foreach (string itemId in order)
            {
                if (AddToSlots(itemId, totals[itemId]) == 0)
                {
                    break;
                }
            }
        }

        /// <summary>把储物格写回存档。没载入过就不写，免得把存档洗成空。</summary>
        public static void Save()
        {
            TrySave();
        }

        private static bool TrySave()
        {
            if (!loaded)
            {
                return false;
            }

            try
            {
                var builder = new StringBuilder();
                for (int i = 0; i < slots.Length; i++)
                {
                    LabSlot slot = slots[i];
                    if (IsEmpty(slot) || !IsAllowed(slot.ItemId))
                    {
                        continue;
                    }

                    int count = slot.Count;
                    if (count > MaxCountPerSlot)
                    {
                        count = MaxCountPerSlot;
                    }

                    if (builder.Length > 0)
                    {
                        builder.Append('|');
                    }

                    builder.Append(slot.ItemId).Append(':').Append(count.ToString(CultureInfo.InvariantCulture));
                }

                ModHelper.SetModData(ChemistryCatalog.ModId, DataKey,
                    WithLayouts(builder.ToString(), BottleLayout, LineLayout, PowerLayout));
                LabInventoryGrid.StorageSaved();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Chemistry] 保存实验室储物格失败：" + e.Message);
                return false;
            }
        }

        private static string WithLayouts(string counts, string bottles, string lines, string power) =>
            EquipmentLayout.Length > 0 ? $"{counts}~{bottles}~{lines}~{power}~{EquipmentLayout}" :
            power.Length > 0 ? $"{counts}~{bottles}~{lines}~{power}" :
            lines.Length > 0 ? $"{counts}~{bottles}~{lines}" :
            bottles.Length > 0 ? $"{counts}~{bottles}" : counts;

        /// <summary>调试用：清空全部格子并写回存档。</summary>
        public static void ResetAll()
        {
            loaded = true;
            ClearSlots();
            BottleLayout = "";
            LineLayout = "";
            PowerLayout = "";
            EquipmentLayout = "";
            Save();
        }

        // ---------------------------------------------------------------
        // 查询
        // ---------------------------------------------------------------

        /// <summary>接受已登记的化学物品及可检测的样品，包括原版废金属。</summary>
        public static bool IsAllowed(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            if (itemId == RefiningStation.ItemId || itemId == AcidStation.ItemId ||
                ReagentBottle.IsSingleBottle(itemId) || BulkContainers.IsWaste(itemId) || SampleTable.Find(itemId) != null ||
                LabRefining.FindIntermediate(itemId) != null || NitricProductionData.Find(itemId) != null
                || LabMixtureData.IsKnown(itemId) || LabAnalysisData.IsKnown(itemId) || LabProcessData.IsKnown(itemId)
                || IndustrialFactoryData.IsFactoryItem(itemId))
            {
                return true;
            }

            if (!itemId.StartsWith(ChemistryCatalog.ItemPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            if (ChemistryCatalog.FindElementByItemId(itemId) != null)
            {
                return true;
            }

            ToolDef[] tools = ChemistryCatalog.Tools;
            for (int i = 0; i < tools.Length; i++)
            {
                if (string.Equals(tools[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            CompoundDef[] compounds = ChemistryCatalog.Compounds;
            for (int i = 0; i < compounds.Length; i++)
            {
                if (string.Equals(compounds[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static int CountOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                LabSlot slot = slots[i];
                if (!IsEmpty(slot) && string.Equals(slot.ItemId, itemId, StringComparison.Ordinal))
                {
                    total += slot.Count;
                }
            }

            return total;
        }

        public static Dictionary<string, int> Counts()
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < slots.Length; i++)
            {
                LabSlot slot = slots[i];
                if (IsEmpty(slot))
                {
                    continue;
                }

                int existing;
                counts.TryGetValue(slot.ItemId, out existing);
                counts[slot.ItemId] = existing + slot.Count;
            }

            return counts;
        }

        public static int SlotsNeeded(Dictionary<string, int> counts)
        {
            int needed = 0;
            foreach (int units in counts.Values)
            {
                needed += (units + MaxCountPerSlot - 1) / MaxCountPerSlot;
            }
            return needed;
        }

        public static bool TryStoreGridCounts(Dictionary<string, int> counts, string bottleLayout, string lineLayout,
            string powerLayout, string equipmentLayout)
        {
            if (!loaded || SlotsNeeded(counts) > Capacity)
            {
                return false;
            }
            foreach (KeyValuePair<string, int> entry in counts)
            {
                if (!IsAllowed(entry.Key) || entry.Value <= 0)
                {
                    return false;
                }
            }

            var original = SnapshotSlots();
            string previousLayout = BottleLayout;
            string previousLine = LineLayout;
            string previousPower = PowerLayout;
            string previousEquipment = EquipmentLayout;
            BottleLayout = bottleLayout;
            LineLayout = lineLayout;
            PowerLayout = powerLayout ?? "";
            EquipmentLayout = equipmentLayout;
            ClearSlots();
            foreach (KeyValuePair<string, int> entry in counts)
            {
                AddToSlots(entry.Key, entry.Value);
            }
            if (TrySave())
            {
                return true;
            }

            RestoreSlots(original);
            BottleLayout = previousLayout;
            LineLayout = previousLine;
            PowerLayout = previousPower;
            EquipmentLayout = previousEquipment;
            return false;
        }

        public static bool Has(string itemId, int units = 1)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            if (units <= 0)
            {
                return true;
            }

            return CountOf(itemId) >= units;
        }

        // ---------------------------------------------------------------
        // 存入 / 取出 / 扣减
        // ---------------------------------------------------------------

        /// <summary>
        /// 从当铺库存扣掉 units 个单位塞进实验室格子（先进同 id 的格子，再占空格）。
        /// 返回实际存入数；没有空格时返回 0 且 message="实验室容量格满了"。
        /// </summary>
        public static int DepositFromStore(string itemId, int units, out string message)
        {
            message = "";

            if (!IsAllowed(itemId))
            {
                message = Loc.Get("lab_storage_only_chem", "实验室只收化学物品。", "The lab only stores chemistry items.");
                return 0;
            }

            if (units <= 0)
            {
                message = Loc.Get("lab_storage_bad_count", "数量不对。", "Invalid amount.");
                return 0;
            }

            if (!loaded)
            {
                EnsureLoaded();
            }

            int available = CountStoreUnits(itemId);
            if (available <= 0)
            {
                message = Loc.Get("lab_storage_store_empty", "当铺里没有{0}。",
                    "There is no {0} in the store.", DisplayName(itemId));
                return 0;
            }

            int room = RoomFor(itemId);
            if (room <= 0)
            {
                message = Loc.Get("lab_storage_full", "实验室容量格满了", "The lab storage is full");
                return 0;
            }

            int toStore = Math.Min(Math.Min(units, available), room);
            bool consumed;
            try
            {
                consumed = StoreInventory.TryConsume(itemId, toStore);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Chemistry] 从当铺扣实验室材料失败：" + e.Message);
                consumed = false;
            }

            if (!consumed)
            {
                message = Loc.Get("lab_storage_consume_failed", "从当铺扣材料失败：{0}。",
                    "Could not take {0} from the store.", DisplayName(itemId));
                return 0;
            }

            int added = AddToSlots(itemId, toStore);
            if (added <= 0)
            {
                message = Loc.Get("lab_storage_full", "实验室容量格满了", "The lab storage is full");
                return 0;
            }

            if (added < toStore)
            {
                // 理论上不会发生（容量是刚算过的），真出现也只是少存几件，记一笔日志够排查了。
                Debug.LogWarning("[Chemistry] 实验室储物格实际存入数少于扣减数：" + itemId + " 扣 " + toStore + " 存 " + added);
            }

            Save();
            message = Loc.Get("lab_storage_deposit_ok", "存入实验室：{0} ×{1}",
                "Stored in the lab: {0} x{1}", DisplayName(itemId), added);
            return added;
        }

        /// <summary>
        /// 从实验室格子取出物品放回当铺（仓库/后柜/前柜/展示柜/夜间背包）。
        /// 放置失败不扣格子；成功才扣并 Save。返回实际取回数。
        /// </summary>
        public static int WithdrawToStore(string itemId, int units, out string message)
        {
            message = "";

            if (BulkContainers.IsWaste(itemId) && !BulkContainers.IsEmptyWaste(itemId))
            {
                message = Loc.Pick("请在储物格中拖走整桶废物。", "Move the whole waste container from the storage grid.");
                return 0;
            }

            if (!IsAllowed(itemId))
            {
                message = Loc.Get("lab_storage_only_chem", "实验室只收化学物品。", "The lab only stores chemistry items.");
                return 0;
            }

            if (units <= 0)
            {
                message = Loc.Get("lab_storage_bad_count", "数量不对。", "Invalid amount.");
                return 0;
            }

            if (!loaded)
            {
                EnsureLoaded();
            }

            int stored = CountOf(itemId);
            if (stored <= 0)
            {
                message = Loc.Get("lab_storage_lab_empty", "实验室里没有{0}。",
                    "There is no {0} in the lab.", DisplayName(itemId));
                return 0;
            }

            int toTake = Math.Min(ReagentBottle.IsSingleBottle(itemId) ? 1 : units, stored);
            int before = toTake > 1 ? CountStoreUnits(itemId) : 0;

            GameItem item = null;
            try
            {
                item = ModHelper.SpawnItem(itemId);
                if (item == null)
                {
                    message = Loc.Get("lab_storage_spawn_failed", "物品没造出来（物品目录没注册上？）。",
                        "The item could not be created (is it registered?).");
                    return 0;
                }

                ModHelper.SetItemOwnerShip(item, true);
                item.SetUnitCount(toTake);
            }
            catch (Exception e)
            {
                DestroyQuietly(item);
                Debug.LogWarning("[Chemistry] 生成实验室取回物品失败：" + e.Message);
                message = Loc.Get("lab_storage_claim_failed", "设置物品归属失败：{0}。",
                    "Could not claim the item: {0}.", e.Message);
                return 0;
            }

            bool placed;
            try
            {
                placed = StoreInventory.TryPlace(item);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Chemistry] 实验室取回物品放置失败：" + e.Message);
                placed = false;
            }

            // 多件物品可能被游戏拆成几摞分别进不同容器（甚至只放下一部分），
            // 用库存差值确认真正放进去多少；放置失败时也可能已经放进去一部分。
            int gained;
            if (toTake > 1)
            {
                int after = CountStoreUnits(itemId);
                if (before >= 0 && after >= 0)
                {
                    gained = Math.Max(0, Math.Min(toTake, after - before));
                }
                else
                {
                    gained = placed ? toTake : 0;
                }
            }
            else
            {
                gained = placed ? 1 : 0;
            }

            if (gained <= 0)
            {
                message = Loc.Get("lab_storage_place_failed", "当铺仓库和柜台都满了，东西先留在实验室格子里。",
                    "Store storage and counters are full; the items stay in the lab.");
                return 0;
            }

            int removed = RemoveFromSlots(itemId, Math.Min(gained, stored));
            if (removed <= 0)
            {
                message = Loc.Get("lab_storage_place_failed", "当铺仓库和柜台都满了，东西先留在实验室格子里。",
                    "Store storage and counters are full; the items stay in the lab.");
                return 0;
            }

            Save();
            message = Loc.Get("lab_storage_withdraw_ok", "取回当铺：{0} ×{1}",
                "Moved back to the store: {0} x{1}", DisplayName(itemId), removed);
            return removed;
        }

        /// <summary>原子扣减：数量不够时一个格子都不动；成功才扣并 Save。</summary>
        public static bool TryConsume(string itemId, int units)
        {
            if (string.IsNullOrEmpty(itemId) || units <= 0)
            {
                return false;
            }

            if (CountOf(itemId) < units)
            {
                return false;
            }

            int removed = RemoveFromSlots(itemId, units);
            if (removed < units)
            {
                // 理论上前面已经确认够数；真出现变化就把扣掉的原样补回去（不如不扣）。
                if (removed > 0)
                {
                    AddToSlots(itemId, removed);
                }

                return false;
            }

            Save();
            return true;
        }

        /// <summary>一次完成样品转换。在放不下产物时保留原料，成功后统一存档。</summary>
        public static bool TryExchange(string inputId, int inputUnits, string outputId, int outputUnits,
            string reagentId = null)
        {
            if (inputId == outputId || inputUnits <= 0 || outputUnits <= 0
                || !IsAllowed(outputId) || CountOf(inputId) < inputUnits
                || (reagentId != null && (reagentId == inputId || reagentId == outputId || !Has(reagentId))))
            {
                return false;
            }

            var original = SnapshotSlots();
            RemoveFromSlots(inputId, inputUnits);
            if (reagentId != null)
            {
                RemoveFromSlots(reagentId, 1);
            }
            if (AddToSlots(outputId, outputUnits) == outputUnits && TrySave())
            {
                return true;
            }

            RestoreSlots(original);
            return false;
        }

        private static (string ItemId, int Count)[] SnapshotSlots()
        {
            var snapshot = new (string ItemId, int Count)[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                snapshot[i] = (slots[i].ItemId, slots[i].Count);
            }
            return snapshot;
        }

        private static void RestoreSlots((string ItemId, int Count)[] snapshot)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].ItemId = snapshot[i].ItemId;
                slots[i].Count = snapshot[i].Count;
            }
        }

        /// <summary>
        /// 往格子里加物品（合成失败回滚用）。只改内存，不 Save，由调用方统一 Save。
        /// </summary>
        public static void AddUnits(string itemId, int units)
        {
            if (string.IsNullOrEmpty(itemId) || units <= 0)
            {
                return;
            }

            if (!IsAllowed(itemId))
            {
                Debug.LogWarning("[Chemistry] 试图把非化学物品放进实验室格子：" + itemId);
                return;
            }

            int added = AddToSlots(itemId, units);
            if (added < units)
            {
                Debug.LogWarning("[Chemistry] 实验室格子放不下回滚材料：" + itemId + " 缺 " + (units - added));
            }
        }

        // ---------------------------------------------------------------
        // 内部辅助
        // ---------------------------------------------------------------

        /// <summary>还能再塞多少单位（同 id 格子余量 + 空格容量）。</summary>
        private static int RoomFor(string itemId)
        {
            int room = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                LabSlot slot = slots[i];
                if (IsEmpty(slot))
                {
                    room += MaxCountPerSlot;
                }
                else if (string.Equals(slot.ItemId, itemId, StringComparison.Ordinal))
                {
                    room += MaxCountPerSlot - slot.Count;
                }
            }

            return room < 0 ? 0 : room;
        }

        /// <summary>按「先同 id 的格子、再空格」的顺序放进 units 个单位，返回实际放进去的数量。</summary>
        private static int AddToSlots(string itemId, int units)
        {
            int remaining = units;

            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                LabSlot slot = slots[i];
                if (IsEmpty(slot) || !string.Equals(slot.ItemId, itemId, StringComparison.Ordinal))
                {
                    continue;
                }

                int space = MaxCountPerSlot - slot.Count;
                if (space <= 0)
                {
                    continue;
                }

                int take = Math.Min(space, remaining);
                slot.Count += take;
                remaining -= take;
            }

            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                LabSlot slot = slots[i];
                if (!IsEmpty(slot))
                {
                    continue;
                }

                int take = Math.Min(MaxCountPerSlot, remaining);
                slot.ItemId = itemId;
                slot.Count = take;
                remaining -= take;
            }

            return units - remaining;
        }

        /// <summary>从格子里扣掉 units 个单位（同 id 可能分散在多格），返回实际扣掉的数量。</summary>
        private static int RemoveFromSlots(string itemId, int units)
        {
            int remaining = units;

            for (int i = 0; i < slots.Length && remaining > 0; i++)
            {
                LabSlot slot = slots[i];
                if (IsEmpty(slot) || !string.Equals(slot.ItemId, itemId, StringComparison.Ordinal))
                {
                    continue;
                }

                int take = Math.Min(slot.Count, remaining);
                slot.Count -= take;
                remaining -= take;

                if (slot.Count <= 0)
                {
                    slot.ItemId = null;
                    slot.Count = 0;
                }
            }

            return units - remaining;
        }

        /// <summary>安全地数店里某种物品有多少单位；扫描失败返回 -1。</summary>
        private static int CountStoreUnits(string itemId)
        {
            try
            {
                Dictionary<string, int> counts = StoreInventory.CountUnits();
                int value;
                return counts != null && counts.TryGetValue(itemId, out value) ? value : 0;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Chemistry] 扫描当铺库存失败：" + e.Message);
                return -1;
            }
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

        /// <summary>物品 id 对应的中/英文显示名（查不到就退回 id）。</summary>
        private static string DisplayName(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return "?";
            }

            NitricProductionData.Item part = NitricProductionData.Find(itemId);
            if (part != null)
            {
                return part.Name;
            }

            SampleDef sample = SampleTable.Find(itemId);
            if (sample != null)
            {
                return sample.DisplayName(itemId == sample.DetectedId);
            }

            ElementDef element = ChemistryCatalog.FindElementByItemId(itemId);
            if (element != null)
            {
                return Loc.Pick(element.NameZh, element.NameEn) + " (" + element.Symbol + ")";
            }

            ToolDef[] tools = ChemistryCatalog.Tools;
            for (int i = 0; i < tools.Length; i++)
            {
                if (string.Equals(tools[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    return Loc.Pick(tools[i].NameZh, tools[i].NameEn);
                }
            }

            CompoundDef[] compounds = ChemistryCatalog.Compounds;
            for (int i = 0; i < compounds.Length; i++)
            {
                if (string.Equals(compounds[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    return Loc.Pick(compounds[i].NameZh, compounds[i].NameEn) + " " + compounds[i].Formula;
                }
            }

            return itemId;
        }
    }
}
