namespace Aethoria.Quests
{
    // 노아가 순서대로 맡기는 퀘스트 목록. 하나를 보고해야 다음 퀘스트가 열린다.
    // 새 퀘스트는 여기에 항목만 추가하면 된다. targetMonsterName은 GameBootstrap에서 정한 몬스터 이름과 같아야 한다.
    public static class QuestDatabase
    {
        public static QuestData[] CreateMainLine()
        {
            return new[]
            {
                new QuestData
                {
                    title = "말썽꾸러기 슬라임 퇴치",
                    targetMonsterName = "슬라임",
                    requiredCount = 5,
                    expReward = 80,
                    offerLine = "광활한 평야에 슬라임들이 불어나서 길이 온통 끈적해졌어요. 5마리만 정리해 주실래요?",
                    completeLine = "고마워요! 이제 평야 길이 한결 깨끗해졌겠네요.",
                },
                new QuestData
                {
                    title = "평야의 왕 보스 슬라임",
                    targetMonsterName = "보스 슬라임",
                    requiredCount = 1,
                    expReward = 250,
                    offerLine = "평야 끝에 왕관을 쓴 커다란 슬라임이 나타났대요. 물대포를 쏜다니 조심하세요!",
                    completeLine = "그 큰 슬라임을요? 정말 대단해요! 평야가 다시 조용해졌어요.",
                },
                new QuestData
                {
                    title = "어둠의 숲 기사 토벌",
                    targetMonsterName = "기사",
                    requiredCount = 5,
                    expReward = 150,
                    offerLine = "숲에 기사들이 자꾸 나타나서 상인들이 무서워해요. 5마리만 쫓아내 주실래요?",
                    completeLine = "정말 해내셨군요! 이제 상인들도 안심하겠어요.",
                },
                new QuestData
                {
                    title = "보스 기사 처치",
                    targetMonsterName = "보스 기사",
                    requiredCount = 1,
                    expReward = 500,
                    offerLine = "기사들을 이끄는 우두머리가 숲 깊은 곳에 있대요. 부디 조심해서 쓰러뜨려 주세요.",
                    completeLine = "우두머리까지... 당신 덕분에 마을이 평화로워졌어요!",
                },
            };
        }
    }
}
