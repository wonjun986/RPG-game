using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Aethoria.Save
{
    // 인벤토리 한 칸(아이템 id + 개수). SaveData 안에 리스트로 들어간다.
    [Serializable]
    public class ItemSaveEntry
    {
        public string itemId;
        public int count;
    }

    // 장착 칸 하나(카테고리 + 장착 중인 아이템 id). category는 Aethoria.Data.ItemCategory를 int로 저장한다.
    [Serializable]
    public class EquipSaveEntry
    {
        public int category;
        public string itemId;
    }

    // 저장 파일 한 칸에 들어가는 내용. JsonUtility로 그대로 직렬화된다.
    // 위치/체력은 저장하지 않는다: 이어하기는 항상 마을에서 체력/마나가 가득 찬 채로 시작한다.
    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int level = 1;
        public int exp;
        public int gold;
        public List<ItemSaveEntry> items = new();
        public List<EquipSaveEntry> equipped = new();
        public int questIndex;
        public int questStatus;
        public int questProgress;
        public string savedAt;
    }

    // 저장 슬롯 5칸. persistentDataPath/save_1.json ~ save_5.json에 슬롯마다 따로 읽고 쓴다.
    // (Windows 기준 %USERPROFILE%/AppData/LocalLow/<회사>/<제품>/)
    // 슬롯 번호는 1부터 시작한다. 예전 단일 저장 파일(save.json)이 있으면 처음 접근할 때 1번 슬롯으로 옮긴다.
    public static class SaveSystem
    {
        public const int SlotCount = 5;
        private const string LegacyFileName = "save.json";

        private static bool legacyChecked;

        private static string SlotPath(int slot) => Path.Combine(Application.persistentDataPath, $"save_{slot}.json");

        public static bool HasSave => MostRecentSlot() > 0;

        public static bool SlotExists(int slot)
        {
            MigrateLegacySave();
            return slot >= 1 && slot <= SlotCount && File.Exists(SlotPath(slot));
        }

        public static bool HasEmptySlot => FirstEmptySlot() > 0;

        // 비어 있는 첫 슬롯. 모두 차 있으면 0.
        public static int FirstEmptySlot()
        {
            for (int slot = 1; slot <= SlotCount; slot++)
            {
                if (!SlotExists(slot)) return slot;
            }
            return 0;
        }

        // 가장 최근에 저장된 슬롯(타이틀의 CONTINUE가 이어하는 슬롯). 저장이 하나도 없으면 0.
        public static int MostRecentSlot() => FindSlotBySavedAt(newest: true);

        // 새 게임을 시작할 때 자동 저장이 쓸 슬롯: 빈 슬롯이 있으면 그중 첫 번째, 없으면 가장 오래된 슬롯.
        public static int SlotForNewGame()
        {
            int empty = FirstEmptySlot();
            return empty > 0 ? empty : FindSlotBySavedAt(newest: false);
        }

        private static int FindSlotBySavedAt(bool newest)
        {
            int found = 0;
            string foundAt = null;
            for (int slot = 1; slot <= SlotCount; slot++)
            {
                var data = Load(slot);
                if (data == null) continue;

                string at = data.savedAt ?? string.Empty; // "yyyy-MM-dd HH:mm:ss"라 문자열 비교로 시간순 비교가 된다
                int cmp = foundAt == null ? 0 : string.CompareOrdinal(at, foundAt);
                if (found == 0 || (newest ? cmp > 0 : cmp < 0))
                {
                    found = slot;
                    foundAt = at;
                }
            }
            return found;
        }

        public static void Save(int slot, SaveData data)
        {
            if (slot < 1 || slot > SlotCount) return;
            MigrateLegacySave();

            data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string json = JsonUtility.ToJson(data, prettyPrint: true);

            // 쓰는 도중에 게임이 꺼져도 기존 저장이 깨지지 않도록 임시 파일에 먼저 쓰고 바꿔치기한다.
            string path = SlotPath(slot);
            string tempPath = path + ".tmp";
            try
            {
                File.WriteAllText(tempPath, json);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tempPath, path);
            }
            catch (Exception e)
            {
                Debug.LogError($"Aethoria: 슬롯 {slot} 저장 실패 - {e.Message}");
            }
        }

        // 해당 슬롯의 저장 파일을 지운다. 이미 비어 있으면 아무 일도 하지 않는다.
        public static void Delete(int slot)
        {
            if (slot < 1 || slot > SlotCount) return;

            string path = SlotPath(slot);
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"Aethoria: 슬롯 {slot} 삭제 실패 - {e.Message}");
            }
        }

        // 파일이 없거나 깨졌으면 null.
        public static SaveData Load(int slot)
        {
            if (!SlotExists(slot)) return null;

            try
            {
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(SlotPath(slot)));
            }
            catch (Exception e)
            {
                Debug.LogError($"Aethoria: 슬롯 {slot} 저장 파일을 읽지 못했습니다 - {e.Message}");
                return null;
            }
        }

        private static void MigrateLegacySave()
        {
            if (legacyChecked) return;
            legacyChecked = true;

            string legacyPath = Path.Combine(Application.persistentDataPath, LegacyFileName);
            string slot1Path = SlotPath(1);
            if (!File.Exists(legacyPath) || File.Exists(slot1Path)) return;

            try
            {
                File.Move(legacyPath, slot1Path);
            }
            catch (Exception e)
            {
                Debug.LogError($"Aethoria: 예전 저장 파일을 슬롯 1로 옮기지 못했습니다 - {e.Message}");
            }
        }
    }
}
