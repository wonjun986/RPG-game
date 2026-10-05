namespace Aethoria.Quests
{
    // 노아가 매일 하나씩 무작위로 내주는 일일 의뢰 후보들. 자정(기기 날짜 기준)이 지나면
    // DailyQuestManager가 이 중 하나를 새로 뽑는다. 메인 퀘스트 목록(QuestDatabase)과 달리
    // 순서 없이 매일 반복해서 받을 수 있다. 새 의뢰는 여기에 항목만 추가하면 된다.
    public static class DailyQuestDatabase
    {
        public static QuestData[] CreatePool()
        {
            return new[]
            {
                new QuestData
                {
                    title = "오늘의 슬라임 사냥",
                    targetMonsterName = "슬라임",
                    requiredCount = 8,
                    expReward = 50,
                    goldReward = 40,
                    offerLine = "오늘은 슬라임이 유독 많이 보이네요. 8마리만 정리해 주실래요?",
                    completeLine = "역시 빠르시네요! 오늘 몫은 다 끝났어요.",
                },
                new QuestData
                {
                    title = "오늘의 기사 토벌",
                    targetMonsterName = "기사",
                    requiredCount = 6,
                    expReward = 70,
                    goldReward = 50,
                    offerLine = "숲 기사들이 오늘따라 유난히 설쳐요. 6마리만 처치해 주실래요?",
                    completeLine = "덕분에 오늘 하루도 무사히 넘어가네요. 고마워요!",
                },
                new QuestData
                {
                    title = "슬라임 대량 토벌",
                    targetMonsterName = "슬라임",
                    requiredCount = 15,
                    expReward = 90,
                    goldReward = 70,
                    offerLine = "오늘은 욕심 좀 내볼까요? 슬라임 15마리만 정리해 주시면 넉넉히 챙겨 드릴게요.",
                    completeLine = "15마리나요?! 정말 대단하세요. 약속한 보상 받아가세요.",
                },
                new QuestData
                {
                    title = "기사단 소탕 임무",
                    targetMonsterName = "기사",
                    requiredCount = 10,
                    expReward = 110,
                    goldReward = 90,
                    offerLine = "오늘은 기사들이 떼로 몰려다닌대요. 10마리만 처치해 주시면 큰 보상을 드릴게요.",
                    completeLine = "정말 고생하셨어요! 오늘치고는 과분한 활약이었네요.",
                },
            };
        }
    }
}
