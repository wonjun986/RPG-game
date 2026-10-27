namespace Aethoria.Quests
{
    // 스토리를 따라 순서대로 이어지는 메인 퀘스트. 하나를 보고해야 다음 퀘스트가 열리고, 월드맵 지역도 이 진행도에 맞춰 열린다
    // (WorldMapUI의 RequiredQuests). 루나벨에게 패배하고 숲에서 깨어난 네크로시아가 힘을 되찾아 가며 붉은 달의 정체를 쫓고,
    // 천상의 무녀 이리아를 만나 절망의 언덕의 루나벨과 결판을 낸다.
    // 앞의 다섯 퀘스트는 예전 목록과 순서가 같아서, 예전 저장 파일의 퀘스트 번호를 그대로 이어 쓸 수 있다.
    // targetMonsterName은 GameBootstrap에서 정한 몬스터 이름과 같아야 한다.
    public static class QuestDatabase
    {
        private static QuestStoryLine Me(string text) => new QuestStoryLine(true, text);
        private static QuestStoryLine Npc(string text) => new QuestStoryLine(false, text);

        public static QuestData[] CreateMainLine()
        {
            return new[]
            {
                // ---- 1장 깨어난 사신: 숲에서 깨어난 몸으로 낫을 쥐는 감각부터 되찾는다 ----
                new QuestData
                {
                    chapter = "1장 깨어난 사신",
                    title = "말썽꾸러기 슬라임 퇴치",
                    targetMonsterName = "슬라임",
                    requiredCount = 5,
                    location = "광활한 평야",
                    expReward = 80,
                    offerStory = new[]
                    {
                        Npc("힘을 되찾으시려면 우선 가벼운 상대부터 시작하는 게 좋겠어요."),
                        Me("...낫을 쥐는 감각이 아직 둔하다."),
                        Npc("마침 광활한 평야에 슬라임들이 불어나서 길이 온통 끈적해졌대요. 몸 풀기 삼아 딱 좋을 거예요."),
                    },
                    offerLine = "평야의 슬라임 5마리만 정리해 주실래요?",
                    completeStory = new[]
                    {
                        Me("...조금씩, 낫이 손에 붙는다."),
                        Npc("벌써 표정이 달라지셨어요! 처음 오셨을 때랑은 완전 딴사람 같아요."),
                    },
                    completeLine = "고마워요! 이제 평야 길이 한결 깨끗해졌겠네요.",
                },
                new QuestData
                {
                    chapter = "1장 깨어난 사신",
                    title = "평야의 왕 보스 슬라임",
                    targetMonsterName = "보스 슬라임",
                    requiredCount = 1,
                    location = "광활한 평야 끝",
                    expReward = 250,
                    offerStory = new[]
                    {
                        Npc("그런데 평야 끝에서 왕관을 쓴 커다란 슬라임을 봤다는 소문이 있어요."),
                        Me("왕관이라... 슬라임 주제에."),
                        Npc("붉은 달이 뜬 뒤로 저런 녀석들이 자꾸 커지고 있대요. 물대포를 쏜다니 조심하세요!"),
                    },
                    offerLine = "평야 끝의 보스 슬라임을 쓰러뜨려 주세요.",
                    completeStory = new[]
                    {
                        Me("붉은 달의 기운이 저 녀석까지 부풀렸던 건가."),
                        Npc("그 큰 슬라임을요? 이 정도면... 네크로시아 님이 깨어나신 어둠의 숲에도 다시 들어가 보실 수 있겠어요."),
                    },
                    completeLine = "평야가 다시 조용해졌어요! <color=#9FD4FF>[어둠의 숲]으로 가는 길이 열렸어요.</color>",
                },

                // ---- 2장 숲의 기사들: 붉은 기운에 붙들린 기사들, 그리고 우두머리의 유언 ----
                new QuestData
                {
                    chapter = "2장 숲의 기사들",
                    title = "어둠의 숲 기사 토벌",
                    targetMonsterName = "기사",
                    requiredCount = 5,
                    location = "어둠의 숲",
                    expReward = 150,
                    offerStory = new[]
                    {
                        Npc("네크로시아 님이 깨어나신 그 숲... 요즘은 갑옷 입은 기사들이 길을 막고 있대요."),
                        Me("기사? 그 숲에 그런 것들은 없었다."),
                        Npc("다들 붉은 달이 뜬 날부터 나타났다고 해요. 꼭 무언가를 지키고 있는 것처럼요."),
                    },
                    offerLine = "숲의 기사 5마리만 쫓아내 주실래요?",
                    completeStory = new[]
                    {
                        Me("놈들의 검에서 붉은 기운이 느껴졌다. 누군가에게 붙들린 자들이다."),
                        Npc("그럼 그 기사들을 부리는 누군가가 있다는 거네요..."),
                    },
                    completeLine = "정말 해내셨군요! 이제 상인들도 안심하겠어요.",
                },
                new QuestData
                {
                    chapter = "2장 숲의 기사들",
                    title = "알리아스 처치",
                    targetMonsterName = "알리아스",
                    requiredCount = 1,
                    location = "어둠의 숲 가장 깊은 곳",
                    expReward = 500,
                    offerStory = new[]
                    {
                        Npc("숲 가장 깊은 곳에 기사들을 이끄는 우두머리, 알리아스가 있대요."),
                        Me("그 녀석이라면 붉은 달에 대해 무언가 알고 있겠지."),
                    },
                    offerLine = "부디 조심해서 알리아스를 쓰러뜨려 주세요.",
                    completeStory = new[]
                    {
                        Me("...녀석은 쓰러지면서 '그분을 막아다오'라고 했다."),
                        Npc("그분... 설마 루나벨을 말하는 걸까요? 기사들도 원해서 싸운 게 아니었군요."),
                        Npc("숲 너머 잊혀진 폐허에 아주 오래된 기록이 남아 있대요. 거기라면 무언가 알 수 있을지도 몰라요."),
                    },
                    completeLine = "당신 덕분에 숲이 평화로워졌어요! <color=#9FD4FF>[잊혀진 폐허]로 가는 길이 열렸어요.</color>",
                },

                // ---- 3장 잊혀진 폐허: 폐허는 옛날 사신을 섬기던 성소였다 ----
                new QuestData
                {
                    chapter = "3장 잊혀진 폐허",
                    title = "잊혀진 폐허의 돌거인",
                    targetMonsterName = "콜로서스",
                    requiredCount = 5,
                    location = "잊혀진 폐허",
                    expReward = 300,
                    offerStory = new[]
                    {
                        Npc("폐허에서 이끼 낀 돌거인, 콜로서스들이 깨어났대요."),
                        Me("폐허... 어딘가 낯이 익다."),
                        Npc("옛날 이야기로는 그 폐허가 '밤의 사신'을 모시던 신전이었대요."),
                        Me("......"),
                    },
                    offerLine = "돌거인 5마리만 잠재워 주세요. 몸집이 커서 한 대 한 대가 묵직하니 조심하세요!",
                    completeStory = new[]
                    {
                        Me("기억났다. 그곳은 한때 나를 섬기던 자들의 성소였다. 루나벨이 모든 걸 짓밟기 전까지."),
                        Npc("그럼 네크로시아 님이... 그 전설 속 사신?"),
                        Me("지금은 이름뿐이다. 되찾기 전까지는."),
                        Npc("...폐허에서 바다 쪽으로 길이 이어져 있대요. 요즘 그 바다가 이상하게 푸르게 빛난다던데..."),
                    },
                    completeLine = "폐허 길이 다시 열렸어요. 정말 든든해요! <color=#9FD4FF>[푸른 바다]로 가는 길이 열렸어요.</color>",
                },

                // ---- 4장 푸른 바다: 붉은 달에 물든 바다의 정령들 ----
                new QuestData
                {
                    chapter = "4장 푸른 바다",
                    title = "심해 정령 진정시키기",
                    targetMonsterName = "심해 정령",
                    requiredCount = 5,
                    location = "푸른 바다",
                    expReward = 400,
                    offerStory = new[]
                    {
                        Npc("어부들이 그러는데, 푸른 바다에서 눈이 하나 달린 정령들이 올라온대요."),
                        Me("바다까지 붉은 달에 물든 건가."),
                        Npc("정령들은 원래 바다를 지키는 존재였대요. 붉은 기운에서 풀어 주면 다시 잠잠해질 거예요."),
                    },
                    offerLine = "심해 정령 5마리를 쓰러뜨려 바다를 진정시켜 주세요.",
                    completeStory = new[]
                    {
                        Me("정령들이 쓰러지면서 하나같이 하늘을 가리켰다. 구름 위... 천상의 신전 쪽이다."),
                        Npc("천상의 신전이라면... 붉은 달에 맞설 힘을 가진 무녀, 이리아 님이 계신다고 들었어요."),
                    },
                    completeLine = "바다가 다시 잔잔해졌대요! <color=#9FD4FF>[천상으로 향하는 계단]으로 가는 길이 열렸어요.</color>",
                },

                // ---- 5장 천상의 무녀: 이리아를 찾아간다 ----
                new QuestData
                {
                    chapter = "5장 천상의 무녀",
                    title = "천상의 무녀를 찾아서",
                    type = QuestType.Talk,
                    targetNpc = QuestData.Iria,
                    turnInNpc = QuestData.Iria,
                    location = "천상의 신전 · 이리아의 성소",
                    expReward = 300,
                    offerStory = new[]
                    {
                        Npc("이리아 님을 만나 보세요. 천상으로 향하는 계단 끝, 천상의 신전 성소 안에 계신대요."),
                        Me("무녀라... 사신에게 손을 내밀어 줄지는 모르겠군."),
                        Npc("괜찮을 거예요. 네크로시아 님은 지금 모두를 지키고 계시잖아요."),
                    },
                    offerLine = "천상의 신전 성소에 계신 이리아 님을 찾아가 주세요.",
                    completeStory = new[]
                    {
                        Npc("...기다리고 있었어, 사신 네크로시아."),
                        Me("나를 알고 있나."),
                        Npc("붉은 달이 뜬 그 밤, 이 신전에서도 보였거든. 네 낫이 꺾이는 순간이."),
                        Npc("루나벨의 힘은 상대의 피를 빨아들여 제 것으로 삼는 데 있어. 하지만 여기까지 온 지금의 너라면 버틸 수 있을 거야."),
                    },
                    completeLine = "내 축복이 필요하면 언제든 말해 줘.",
                },

                // ---- 6장 피의 여왕: 절망의 언덕의 루나벨과 결판을 내고 마을로 돌아온다 ----
                new QuestData
                {
                    chapter = "6장 피의 여왕",
                    title = "피의 여왕 루나벨",
                    targetMonsterName = "루나벨",
                    requiredCount = 1,
                    location = "절망의 언덕",
                    giverNpc = QuestData.Iria,
                    turnInNpc = QuestData.Noa,
                    expReward = 1000,
                    offerStory = new[]
                    {
                        Npc("...루, 루나벨. 그 이름을 입에 올리는 것만으로도 손이 떨려. 미안, 잠깐만..."),
                        Npc("붉은 달이 뜬 그 밤, 이 제단에서 전부 봤어. 그 애가 웃으면서... 네 피를 한 방울씩 빨아들이는 걸."),
                        Npc("나는 아무것도 못 했어. 그냥 숨죽이고... 그 웃음소리가 그치기만 기다렸어."),
                        Me("...이리아."),
                        Npc("지금도 눈을 감으면 들려. 그 애가 부르는 소리가. 심장의 피가 거기로 끌려가는 것 같아서... 무서워. 솔직히, 너무 무서워."),
                        Me("그래서 내가 간다. 그날 빼앗긴 것들을, 전부 되찾아 오겠다."),
                        Npc("...응. 절망의 언덕으로 가는 길을 열어 둘게. 그 위에서 루나벨이... 너를 기다리고 있어."),
                        Npc("가기 전에 꼭 축복을 받아 둬. 그리고 약속해. 반드시 돌아오겠다고. 또 아무것도 못 하고 지켜보기만 하는 건... 싫어."),
                    },
                    offerLine = "절망의 언덕의 루나벨을 쓰러뜨려 줘. <color=#1F5FA8>[절망의 언덕]으로 가는 길이 열려.</color>",
                    completeStory = new[]
                    {
                        Npc("네크로시아 님! 정말... 정말 돌아오셨군요!"),
                        Me("약속했지. 살아서 돌아오겠다고."),
                        Npc("하늘의 붉은 달이 사라졌어요. 마을 사람들 모두 밖으로 나와서 하늘만 올려다보고 있어요."),
                        Me("힘도, 이름도, 영광도 되찾았다. ...하지만 이 마을에서 받은 것도 잊지 않겠다."),
                    },
                    completeLine = "고마워요, 네크로시아 님. 언제든 들러 주세요!",
                },
            };
        }
    }
}
