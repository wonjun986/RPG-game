using System;

namespace Aethoria.Quests
{
    // 퀘스트 하나의 정의. 지금은 "특정 이름의 몬스터를 N마리 처치" 한 종류만 있다.
    // targetMonsterName은 MonsterData.monsterName과 정확히 같아야 처치 수가 올라간다.
    [Serializable]
    public class QuestData
    {
        public string title;
        public string targetMonsterName;
        public int requiredCount;
        public int expReward;
        public int goldReward; // 일일 의뢰에서 사용. 메인 퀘스트는 0(경험치만 지급).

        public string offerLine;    // 노아가 의뢰할 때 하는 말
        public string completeLine; // 완료 보고를 받았을 때 하는 말
    }
}
