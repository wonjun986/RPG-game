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
                    requiredMainQuests = 2, // 어둠의 숲이 열린 뒤
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
                    requiredMainQuests = 2, // 어둠의 숲이 열린 뒤
                    requiredCount = 10,
                    expReward = 110,
                    goldReward = 90,
                    offerLine = "오늘은 기사들이 떼로 몰려다닌대요. 10마리만 처치해 주시면 큰 보상을 드릴게요.",
                    completeLine = "정말 고생하셨어요! 오늘치고는 과분한 활약이었네요.",
                },
                new QuestData
                {
                    title = "오늘의 콜로서스 토벌",
                    targetMonsterName = "콜로서스",
                    requiredMainQuests = 4, // 잊혀진 폐허가 열린 뒤
                    requiredCount = 6,
                    expReward = 130,
                    goldReward = 110,
                    offerLine = "폐허의 콜로서스들이 오늘따라 쿵쿵거리며 돌아다닌대요. 6마리만 쓰러뜨려 주실래요?",
                    completeLine = "돌거인을 여섯이나! 오늘 보상은 넉넉히 챙겨 드릴게요.",
                },
                // 아래부터는 나중에 추가한 의뢰. 저장 파일이 순서 번호로 오늘의 의뢰를 기억하므로 항상 맨 뒤에 붙인다.
                new QuestData
                {
                    title = "오늘의 심해 정령 진정",
                    targetMonsterName = "심해 정령",
                    requiredMainQuests = 6, // 푸른 바다가 열린 뒤
                    requiredCount = 6,
                    expReward = 160,
                    goldReward = 130,
                    offerLine = "어부들이 오늘도 그물을 못 던졌대요. 푸른 바다의 심해 정령 6마리만 진정시켜 주실래요?",
                    completeLine = "바다가 한결 잠잠해졌대요! 어부들이 고맙다고 전해 달래요.",
                },
                new QuestData
                {
                    title = "심해 정령 대규모 정화",
                    targetMonsterName = "심해 정령",
                    requiredMainQuests = 6,
                    requiredCount = 12,
                    expReward = 240,
                    goldReward = 200,
                    offerLine = "오늘은 정령들이 떼로 올라왔대요. 12마리를 진정시켜 주시면 넉넉히 사례할게요.",
                    completeLine = "열두 마리나요?! 오늘 바다는 네크로시아 님 덕분에 살았어요.",
                },
            };
        }
    }
}
