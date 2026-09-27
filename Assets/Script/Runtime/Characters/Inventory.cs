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

        // 아이템, 이번에 새로 얻은 개수. 획득 연출(토스트 등)에서 구독한다.
        public event Action<ItemData, int> OnItemAdded;
        public event Action<ItemData> OnEquipped;

        public int GetCount(string itemId) => counts.TryGetValue(itemId, out var count) ? count : 0;

        public void AddItem(ItemData item, int count = 1)
        {
            if (item == null || count <= 0) return;

            counts[item.id] = GetCount(item.id) + count;
            OnItemAdded?.Invoke(item, count);
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
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(entry.itemId)) equipped[(ItemCategory)entry.category] = entry.itemId;
            }
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
