using System;

namespace Aethoria.Quests
{
    public enum QuestType
    {
        Kill, // targetMonsterName 몬스터를 requiredCount마리 처치
        Talk, // targetNpc에게 찾아가 말을 걸면 완료
    }

    // 퀘스트 의뢰/보고 때 나오는 스토리 대사 한 줄. isNecrosia가 false면 그 장면의 NPC(의뢰/보고 받는 사람)가 말한다.
    [Serializable]
    public struct QuestStoryLine
    {
        public bool isNecrosia;
        public string text;

        public QuestStoryLine(bool isNecrosia, string text)
        {
            this.isNecrosia = isNecrosia;
            this.text = text;
        }
    }

    // 퀘스트 하나의 정의. 메인 퀘스트는 스토리 장(chapter) 순서대로 이어지고, 의뢰/보고 때 스토리 대사(offerStory/completeStory)가
    // 먼저 나온 뒤 의뢰 문구(offerLine)/보고 문구(completeLine)가 나온다. 일일 의뢰는 처치 퀘스트만 쓰고 스토리 대사는 비워 둔다.
    // targetMonsterName은 MonsterData.monsterName과 정확히 같아야 처치 수가 올라간다.
    [Serializable]
    public class QuestData
    {
        public const string Noa = "노아";
        public const string Iria = "이리아";

        public string chapter;      // 추적 UI에 붙는 장 이름(예: "1장 깨어난 사신"). 일일 의뢰는 비워 둔다.
        public string title;
        public QuestType type = QuestType.Kill;
        public string targetMonsterName;
        public int requiredCount = 1;
        public string targetNpc;    // Talk 퀘스트에서 찾아가야 하는 NPC
        public string location;     // 추적 UI에 보여 줄 장소 안내(예: "광활한 평야")
        public string giverNpc = Noa;  // 이 퀘스트를 의뢰하는 NPC
        public string turnInNpc = Noa; // 완료 보고를 받는 NPC
        public int expReward;
        public int goldReward; // 일일 의뢰에서 사용. 메인 퀘스트는 0(경험치만 지급).

        public QuestStoryLine[] offerStory;    // 의뢰 전에 나오는 스토리 대사
        public string offerLine;                // 의뢰 문구(보상과 [Z] 수락 / [X] 거절이 붙는다)
        public QuestStoryLine[] completeStory; // 보고할 때 나오는 스토리 대사
        public string completeLine;             // 보고를 받았을 때 하는 말(보상이 붙는다)
    }
}
