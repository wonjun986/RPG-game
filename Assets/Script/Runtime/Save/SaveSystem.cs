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
        // 2: 메인 퀘스트에 황금 모래룡(5번)과 아쿠아(7번)가 끼워 들어갔다. 1이면 불러올 때 퀘스트 번호를 옮긴다.
        // 3: 이야기 순서가 바뀌어 천상(이리아 찾기)이 바다(심해 정령·아쿠아)보다 앞으로 왔다.
        public const int CurrentVersion = 3;
        public int version = CurrentVersion;
        public int level = 1;
        public int exp;
        public int gold;
        public List<ItemSaveEntry> items = new();
        public List<EquipSaveEntry> equipped = new();
        // 장비 강화 단계(itemId + count 자리에 강화 단계). 예전 저장 파일에는 없어서 비어 있으면 전부 0강.
        public List<ItemSaveEntry> enhanceLevels = new();
        // 예전(갈래 선택식 이전) 이리아의 축복 단계. 불러올 때 쓴 골드를 돌려주고 0으로 바꾼다 — 새로 저장할 때는 항상 0.
        public int blessingLevel;
        // 예전(특성 트리 이전) 이리아의 축복 갈래별 단계(공격력/방어력/치명타 확률/치명타 피해/스킬 피해).
        // 불러올 때 쓴 골드를 돌려주고 비운다 — 새로 저장할 때는 항상 비어 있다.
        public List<int> blessingTracks = new();
        // 이리아의 축복 특성 트리("특성이름:단계" 목록, 예: "SharpScythe:3"). 비어 있으면 아무것도 안 찍은 상태.
        public List<string> blessingTalents = new();
        public int questIndex;
        public int questStatus;
        public int questProgress;
        public int dailyQuestIndex;
        public int dailyQuestStatus;
        public int dailyQuestProgress;
        public string dailyQuestResetDate;
        // 보스 기사 등장 연출을 한 번이라도 봤는지. 본 뒤로는 등장 대사를 Esc로 건너뛸 수 있다.
        public bool bossIntroSeen;
        // 루나벨(절망의 언덕) 등장 연출을 본 적 있는지. 본 뒤로는 등장 대사를 Esc로 건너뛸 수 있다.
        public bool lunabelIntroSeen;
        // 황금 모래룡(잊혀진 폐허 보스) 등장 연출을 본 적 있는지. 본 뒤로는 Esc로 건너뛸 수 있다.
        public bool sandDragonIntroSeen;
        // 푸른 바다 추격전의 아쿠아 등장 연출을 본 적 있는지. 본 뒤로는 Esc로 건너뛸 수 있다.
        public bool seaChaseIntroSeen;
        // 루나벨 첫 응시의 대사를 본 적 있는지. 본 뒤로는 Esc로 건너뛸 수 있다.
        public bool lunabelGazeTalkSeen;
        // 첫 클리어 엔딩을 봤는지(EndingCreditsUI). 예전 저장 파일에는 없어서 false로 읽힌다.
        public bool endingSeen;
        // 노아와의 첫 만남(스토리 대사)이 아직 남았는지. 새 게임에서만 true로 시작한다 — 이 값이 없던 예전 세이브는
        // false로 읽혀서, 이미 노아를 만난 캐릭터에게 첫 만남 대사가 다시 나오지 않는다.
        public bool noaFirstMeetingPending;
        // 이시스와의 첫 만남 대사가 아직 남았는지(노아와 같은 방식).
        public bool isisFirstMeetingPending;
        // 월드맵에서 쇠사슬이 부서지는 해금 연출을 어디까지 봤는지(그때의 메인 퀘스트 번호). 이 값이 없던 예전 세이브는 -1로
        // 읽혀서, 불러올 때 지금 퀘스트 번호로 맞춘다(이미 열려 있던 지역의 연출이 한꺼번에 나오지 않게).
        public int mapUnlockSeen = -1;
        // 릴리의 몬스터 도감: 몬스터 이름(itemId 자리)별 처치 수. 이 값이 없던 예전 세이브는 빈 도감에서 시작한다.
        public List<ItemSaveEntry> bestiaryKills = new();
        // 익힌 스킬 키(A/S/D/Q/W/E/R, SkillBook). skillBookSaved가 false인 예전 세이브는 모든 스킬을 익힌 것으로 본다.
        public List<string> learnedSkills = new();
        public bool skillBookSaved;
        // 연 보물상자 id(TreasureChests). 예전 세이브에는 없어서 비어 있으면 아무것도 안 연 것.
        public List<string> openedChests = new();
        // 맵 진행 상태 깃발(WorldFlags: 열쇠, 지름길, 부순 벽, 퍼즐 등). 예전 세이브에는 없어서 비어 있으면 아무것도 안 한 것.
        public List<string> worldFlags = new();
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
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SlotPath(slot)));
                if (data != null) Upgrade(data);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"Aethoria: 슬롯 {slot} 저장 파일을 읽지 못했습니다 - {e.Message}");
                return null;
            }
        }

        // 예전 버전 세이브를 지금 형식으로 옮긴다(파일은 다음 저장 때 새 형식으로 덮어써진다).
        private static void Upgrade(SaveData data)
        {
            if (data.version < 2)
            {
                // 버전 1의 메인 퀘스트: 0 슬라임, 1 보스 슬라임, 2 기사, 3 알리아스, 4 콜로서스, 5 심해 정령, 6 이리아, 7 루나벨.
                // 5(황금 모래룡)와 7(아쿠아)이 끼워 들어가서 5번부터 뒤로 밀린다. 이미 지나온 지역이 다시 잠기지 않게,
                // 예전 5번 이상에 있던 캐릭터는 새로 생긴 퀘스트를 건너뛴 자리로 옮긴다.
                data.questIndex = ShiftV1QuestIndex(data.questIndex);
                if (data.mapUnlockSeen >= 0) data.mapUnlockSeen = ShiftV1QuestIndex(data.mapUnlockSeen);
            }
            if (data.version < 3)
            {
                // 버전 2: 6 심해 정령, 7 아쿠아, 8 이리아 찾기 → 버전 3: 6 이리아 찾기, 7 심해 정령, 8 아쿠아.
                // 이미 끝낸 싸움을 다시 시키지 않는 쪽으로 옮긴다(이리아 첫 만남 대사는 건너뛸 수 있다).
                data.questIndex = ShiftV2QuestIndex(data.questIndex);
                if (data.mapUnlockSeen >= 0) data.mapUnlockSeen = ShiftV2QuestIndex(data.mapUnlockSeen);
            }
            data.version = SaveData.CurrentVersion;
        }

        //  6(심해 정령 차례, 이리아 아직) → 6(이리아부터 만나고 바다로)
        //  7(아쿠아 차례, 정령은 끝남)   → 8(아쿠아)
        //  8(이리아 차례, 바다는 끝남)   → 9(루나벨 — 이리아가 의뢰한다)
        private static int ShiftV2QuestIndex(int index)
        {
            if (index == 7) return 8;
            if (index == 8) return 9;
            return index;
        }

        private static int ShiftV1QuestIndex(int index)
        {
            if (index <= 4) return index;
            if (index == 5) return 6;
            return index + 2;
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
