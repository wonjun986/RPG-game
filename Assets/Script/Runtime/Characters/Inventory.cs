using System;
using System.Collections.Generic;
using UnityEngine;
using Aethoria.Data;
using Aethoria.Save;

namespace Aethoria.Characters
{
    // 플레이어가 보유한 아이템을 id별 개수로 들고 있다(골드는 Character가 따로 관리).
    [RequireComponent(typeof(Character))]
    public class Inventory : MonoBehaviour
    {
        private readonly Dictionary<string, int> counts = new();
        // 카테고리(장식/무기 등)별로 지금 장착 중인 아이템 id 하나. 카테고리당 한 개만 장착할 수 있다.
        private readonly Dictionary<ItemCategory, string> equipped = new();
        // 아이템 id별 강화 단계(0이면 없음). 같은 아이템은 하나만 가질 수 있어서(상점도 중복 구매 불가) id 단위로 둔다.
        private readonly Dictionary<string, int> enhanceLevels = new();

        // 아이템, 이번에 새로 얻은 개수. 획득 연출(토스트 등)에서 구독한다.
        public event Action<ItemData, int> OnItemAdded;
        public event Action<ItemData> OnEquipped;
        public event Action<ItemData> OnUnequipped;
        public event Action<ItemData, int> OnEnhanced;

        public int GetCount(string itemId) => counts.TryGetValue(itemId, out var count) ? count : 0;

        public void AddItem(ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return;

            counts[item.id] = GetCount(item.id) + count;
            OnItemAdded?.Invoke(item, count);
        }

        // 강화 재료 소모 등. 보유량이 모자라면 아무것도 하지 않고 false를 반환한다.
        public bool TryRemoveItem(ItemData item, int count)
        {
            if (item == null || count <= 0) return false;
            int have = GetCount(item.id);
            if (have < count) return false;

            if (have == count) counts.Remove(item.id);
            else counts[item.id] = have - count;
            return true;
        }

        public int GetEnhanceLevel(ItemData item) =>
            item != null && enhanceLevels.TryGetValue(item.id, out var level) ? level : 0;

        public void SetEnhanceLevel(ItemData item, int level)
        {
            if (item == null) return;
            level = UnityEngine.Mathf.Clamp(level, 0, EnhanceRules.MaxLevel);
            if (level == 0) enhanceLevels.Remove(item.id);
            else enhanceLevels[item.id] = level;

            // 장착 중인 장비를 강화했으면 능력치를 바로 다시 계산한다.
            if (IsEquipped(item)) ApplyEquipmentStats();
            OnEnhanced?.Invoke(item, level);
        }

        public ItemData GetEquipped(ItemCategory category)
        {
            return equipped.TryGetValue(category, out var itemId) ? ItemDatabase.Find(itemId) : null;
        }

        public bool IsEquipped(ItemData item)
        {
            return item != null && equipped.TryGetValue(item.category, out var itemId) && itemId == item.id;
        }

        // 보유하지 않은 아이템은 장착할 수 없다. 같은 칸(카테고리)에 이미 장착된 게 있으면 갈아 끼운다.
        public void Equip(ItemData item)
        {
            if (item == null || GetCount(item.id) <= 0) return;

            equipped[item.category] = item.id;
            ApplyEquipmentStats();
            OnEquipped?.Invoke(item);
        }

        // 화면에 보여줄 전체 목록(등록된 아이템만).
        public IEnumerable<(ItemData item, int count)> All()
        {
            foreach (var entry in counts)
            {
                var data = ItemDatabase.Find(entry.Key);
                if (data != null) yield return (data, entry.Value);
            }
        }

        // 저장 파일에서 이어하기.
        public void RestoreItems(List<ItemSaveEntry> entries)
        {
            counts.Clear();
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (entry.count > 0) counts[entry.itemId] = entry.count;
            }
        }

        public List<ItemSaveEntry> ToSaveEntries()
        {
            var list = new List<ItemSaveEntry>();
            foreach (var entry in counts)
            {
                list.Add(new ItemSaveEntry { itemId = entry.Key, count = entry.Value });
            }
            return list;
        }

        public void RestoreEquipped(List<EquipSaveEntry> entries)
        {
            equipped.Clear();
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (!string.IsNullOrEmpty(entry.itemId)) equipped[(ItemCategory)entry.category] = entry.itemId;
                }
            }
            ApplyEquipmentStats();
        }

        // 해당 칸(카테고리)의 장비를 벗는다. 아이템은 가방에 그대로 남는다.
        public void Unequip(ItemCategory category)
        {
            var item = GetEquipped(category);
            if (!equipped.Remove(category)) return;

            ApplyEquipmentStats();
            OnUnequipped?.Invoke(item);
        }

        // 장착 중인 모든 장비의 능력치(기본 + 추가 옵션)를 합쳐 캐릭터에 적용한다.
        private void ApplyEquipmentStats()
        {
            var total = new ItemStats();
            foreach (var itemId in equipped.Values)
            {
                var item = ItemDatabase.Find(itemId);
                if (item != null) total += item.TotalStatsAt(GetEnhanceLevel(item));
            }
            GetComponent<Character>().SetEquipmentStats(total);
        }

        // 강화 단계는 ItemSaveEntry(itemId + count 자리에 단계)로 저장한다.
        public void RestoreEnhanceLevels(List<ItemSaveEntry> entries)
        {
            enhanceLevels.Clear();
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (!string.IsNullOrEmpty(entry.itemId) && entry.count > 0)
                        enhanceLevels[entry.itemId] = UnityEngine.Mathf.Min(entry.count, EnhanceRules.MaxLevel);
                }
            }
            ApplyEquipmentStats();
        }

        public List<ItemSaveEntry> ToEnhanceSaveEntries()
        {
            var list = new List<ItemSaveEntry>();
            foreach (var entry in enhanceLevels)
            {
                list.Add(new ItemSaveEntry { itemId = entry.Key, count = entry.Value });
            }
            return list;
        }

        public List<EquipSaveEntry> ToEquipSaveEntries()
        {
            var list = new List<EquipSaveEntry>();
            foreach (var entry in equipped)
            {
                list.Add(new EquipSaveEntry { category = (int)entry.Key, itemId = entry.Value });
            }
            return list;
        }
    }
}
