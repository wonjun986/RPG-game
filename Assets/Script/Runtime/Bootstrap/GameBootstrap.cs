using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Data;
using Aethoria.Monsters;
using Aethoria.Quests;
using Aethoria.Save;
using Aethoria.Stages;
using Aethoria.UI;

namespace Aethoria.Bootstrap
{
    // Play 모드로 들어가면 씬이 비어있어도 맵/플레이어/몬스터를 코드에서 바로 구성한다.
    // 에디터에서 미리 데이터 에셋이나 프리팹을 만들어 둘 필요가 없다.
    public static partial class GameBootstrap
    {
        private const int MapWidth = 20;
        // 어둠의 숲 1·2 맵 폭(보스 맵은 MapWidth). 넓어진 만큼 기사도 3 → 5마리.
        private const int ForestWidth = 40;
        private const int ForestKnightsPerStage = 5;
        private const int MapHeight = 14;
        private const float WallThickness = 0.5f;
        private const int VillageStage = 0; // 게임 시작 시 진입하는 안전 지역. 몬스터 없이 포탈로 1스테이지에 들어간다.
        // 던전 맵(20유닛)과 폭이 같으면 화면 폭에 거의 꽉 차서 카메라가 거의 못 움직인다(가장자리 근처에서만 살짝).
        // 마을은 좌우로 길게 걷는 구간이라 일부러 훨씬 넓게 잡아서 카메라가 계속 따라오는 느낌이 나게 한다.
        private const int VillageWidth = 32;
        private const int TotalStages = 3;
        // 광활한 평야: 어둠의 숲(1~3)과 별개로 월드맵에서 바로 들어가는 3연속 필드. 스테이지 번호와 겹치지
        // 않게 음수로 두고, 숫자가 작을수록(더 음수일수록) 더 깊이 들어간 맵이다. 마지막(3번째) 맵 끝에 필드 보스가 나온다.
        private const int PlainsStage1 = -1;
        private const int PlainsStage2 = -2;
        private const int PlainsStage3 = -3;
        private const int TotalPlainsStages = 3;
        private const int PlainsWidth = 40;
        private const int BossStage = 3;
        // 알리아스 보스전 배경 음악(Sound/Game Bgm/3_보스/알리아스 (어둠의 숲)/The Unresolved Chord, 3분 28초).
        private const string AliasBossBgmPath = "Audio/Bgm/alias_boss";
        private const float AliasBossBgmLoopStart = 15f;
        // 마을 배경 음악(Sound/Game Bgm/2_맵/마을/마을 브금.mp4에서 소리만 뽑은 것, 3분 6초).
        private const string VillageBgmPath = "Audio/Bgm/village";
        // 파일 맨 앞 약 3.95초가 완전한 무음이라, 마을에 들어오자마자 들리도록 그만큼 건너뛰고 튼다(다시 돌 때도 같은 자리부터).
        private const float VillageBgmSilence = 3.9f;
        // 잊혀진 폐허 배경 음악(Sound/Game Bgm/2_맵/잊혀진 폐허의 녹화본에서 소리만 뽑아 앞 무음을 자른 것, 3분 27초).
        private const string RuinsBgmPath = "Audio/Bgm/ruins";
        // 아직 곡 파일이 없는 지역들. 아래 경로(Assets/Resources/Audio/Bgm/파일이름.ogg 등)에 파일을 넣으면 바로 나온다.
        // 파일이 없는 동안은 BgmPlayer가 fallback 곡을 틀거나(지정한 경우) 음악을 끈다.
        private const string TutorialForestBgmPath = "Audio/Bgm/tutorial_forest"; // 튜토리얼 숲
        private const string DarkForestBgmPath = "Audio/Bgm/dark_forest";         // 어둠의 숲 1~2(보스 맵은 알리아스 곡)
        private const string PlainsBgmPath = "Audio/Bgm/plains";                  // 광활한 평야 1~3
        private const string TrainingHallBgmPath = "Audio/Bgm/training_hall";     // 이시스의 연습장(없으면 마을 곡)
        private const string EnsroomBgmPath = "Audio/Bgm/ensroom";                // 엔스룸 마을(없으면 마을 곡)
        private const string BlueSeaBgmPath = "Audio/Bgm/blue_sea";               // 푸른 바다 1~3
        private const string AquaBossBgmPath = "Audio/Bgm/aqua_boss";             // 추격전 + 아쿠아 2차전(없으면 모래룡 곡)
        private const string HeavenBgmPath = "Audio/Bgm/heaven";                  // 천상의 신전/이리아의 성소
        private const string LunabelBossBgmPath = "Audio/Bgm/lunabel_boss";       // 절망의 언덕 루나벨전
        // 연습장: 마을의 이시스에게 말을 걸어 들어가는 허수아비 맵. 몬스터도 경험치도 없고 마나/체력이 계속 가득 찬다.
        private const int PracticeStage = -4;
        private const int PracticeWidth = 20; // 배경 그림(배율 1에서 폭 20.48) 한 장으로 덮이게 — 좌우 반전 복사본을 붙이지 않는다
        // 잊혀진 폐허: 평야처럼 월드맵에서 바로 들어가는 3연속 필드(-5 → -6 → -7). 첫 맵만 폐허 입구 배경이고,
        // 그 뒤로는 평평한 폐허 마당 배경이 이어진다. 떠 있는 발판 배치는 세 맵이 모두 다르다.
        private const int RuinsStage1 = -5;
        private const int RuinsStage3 = -7;
        private const int TotalRuinsStages = 3;
        private const int RuinsWidth = 40;
        // 엔스룸 마을: 월드맵에서 들어가는 두 번째 안전 지역. 몬스터 없이 분수 광장을 걸어 다닐 수 있다.
        private const int EnsroomStage = -8;
        private const int EnsroomWidth = 32;
        // 이리아의 성소: 천상의 신전의 성소 건물(겉모습)로 들어가면 나오는 작은 실내 맵. 막다른
        // 방이라 돌아가는 포탈 하나만 있다.
        private const int SanctuaryStage = -9;
        private const int SanctuaryWidth = 20;
        // 천상의 신전: 월드맵에서 들어가는 세 번째 안전 지역. 구름 위 대리석 광장 가운데의 이리아의 성소 건물로 들어간다.
        // 예전에는 천상으로 향하는 계단(-10)을 지나 따로 된 신전(-11)에 들어갔지만 한 맵으로 합쳤다(-11은 비워 둔다).
        private const int TempleStage = -10;
        private const int TempleWidth = 40;
        // 절망의 언덕: 월드맵에서 들어가는 최종보스 맵. 일반 몬스터 없이 언덕 끝에 루나벨만 서 있다.
        private const int HillStage = -12;
        private const int HillWidth = 72; // 걸어가는 길(x -36~9) + 결전장(x 9~35)
        // 튜토리얼 숲: 새 게임을 시작하면 도입 스토리 뒤에 눈을 뜨는 어둠의 숲 맵. 이동/전투를 배우고(ForestTutorial)
        // 오른쪽 끝 포탈로 마을에 들어간다. 이어하기로는 다시 오지 않는다.
        private const int TutorialStage = -13;
        // 푸른 바다: 폐허처럼 월드맵에서 바로 들어가는 3연속 필드(-14 → -15 → -16). 세 맵 모두 같은 바닷가 돌길
        // 배경을 쓰고, 해저 유적 발판 배치는 맵마다 다르다. 심해 정령이 들어올 때마다 무작위 위치에 나온다.
        private const int BlueSeaStage1 = -14;
        private const int BlueSeaStage3 = -16;
        private const int TotalBlueSeaStages = 3;
        private const int BlueSeaWidth = 40;
        // 푸른 바다 마지막 맵 오른쪽 돌길이 무너지면 떨어지는 물속 추격전(SeaCollapseCutscene → SeaChaseController).
        // 카메라가 고정이라 맵 폭 없이 화면 한 칸만 쓴다. 쓰러지면 추격전 처음부터 다시 시작한다.
        private const int SeaChaseStage = -17;
        private const int SeaChaseFloorY = -2;
        // 추격전에서 이기면 곧바로 이어지는 아쿠아 2차전: 배경이 흐르지 않는 바닷속 고정 투기장(AquaAI).
        // 화면 한 칸 폭이라 카메라가 움직이지 않는다. 쓰러지면 추격전이 아니라 2차전부터 다시 시작한다.
        private const int SeaArenaStage = -18;
        private const int SeaArenaWidth = 22; // 예전 14는 한 화면이라 피할 자리가 없어서 넓혔다(카메라가 좌우로 따라간다)

        // 마을에 들어올 때 서는 자리. Edge는 들어온 방향에 따라 왼쪽/오른쪽 끝, Center는 마을 한가운데(게임을 이어서 시작할 때).
        private enum VillageSpawn { Edge, Noa, Isis, Center }

        // 플레이어/카메라/HUD는 스테이지가 바뀌어도 유지되고, 맵/몬스터/포탈만 stageRoot 아래에
        // 묶어서 통째로 지웠다가 다시 짓는다.
        private static Character player;
        private static GameObject stageRoot;
        private static GameObject hudRoot;
        private static int currentStage;
        private static int mapXMin, mapXMax, mapYMin, mapYMax;
        private static StageClearUI stageClearUI;
        private static BossHealthUI bossHealthUI;
        private static NpcDialogueUI npcDialogueUI;
        private static WorldMapUI worldMapUI;
        private static PortalConfirmUI portalConfirmUI;
        private static InventoryUI inventoryUI;
        private static StatusUI statusUI;
        private static ShopUI shopUI;
        private static EnhanceUI enhanceUI;
        private static BlessingUI blessingUI;
        private static SkillLearnUI skillLearnUI;
        private static QuestTrackerUI questTrackerUI;
        private static BestiaryUI bestiaryUI;
        private static QuestManager questManager;
        private static DailyQuestManager dailyQuestManager;
        private static SaveToastUI saveToast;
        private static SaveScreenUI saveScreen;
        private static TutorialUI tutorialUI;
        private static StoryIntroUI storyIntroUI;
        private static EndingCreditsUI endingCreditsUI;
        // 보스 기사 등장 연출을 이미 봤는지(세이브에 저장). 두 번째 만남부터는 등장 대사를 Esc로 건너뛸 수 있다.
        private static bool bossIntroSeen;
        private static bool lunabelIntroSeen;
        // 황금 모래룡 등장 연출을 본 적 있는지(세이브에 저장). 본 뒤로는 Esc로 건너뛸 수 있다.
        private static bool sandDragonIntroSeen;
        private static bool seaChaseIntroSeen;
        // 아쿠아 2차전 등장 대사를 이번 실행에서 본 적 있는지. 쓰러져 다시 도전할 때 Esc로 건너뛸 수 있게 한다.
        private static bool aquaArenaIntroSeen;
        private static bool lunabelGazeTalkSeen;
        // 루나벨을 처음 쓰러뜨린 뒤 엔딩(EndingCreditsUI)을 이미 봤는지. 다시 쓰러뜨릴 때는 엔딩 없이 바로 마을로 돌아간다.
        private static bool endingSeen;
        // 노아와의 첫 만남 대사가 아직 남았는지(새 게임에서만 true, 한 번 보면 false로 저장된다).
        private static bool noaFirstMeetingPending;
        private static bool isisFirstMeetingPending;
        // 자동 저장이 기록되는 슬롯. 이어하기면 불러온 슬롯, 새 게임이면 빈 슬롯(없으면 가장 오래된 슬롯),
        // 노아의 저장 화면에서 다른 슬롯에 저장하면 그 슬롯으로 바뀐다.
        private static int activeSlot = 1;

        public static int ActiveSlot => activeSlot;

        // 궁극기의 칼날폭풍처럼 맵 전역에 이펙트를 뿌리는 연출이 현재 맵의 실제 크기를 알아야 할 때 쓴다.
        public static Rect CurrentMapBounds => new Rect(mapXMin, mapYMin, mapXMax - mapXMin + 1, mapYMax - mapYMin + 1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Run()
        {
            if (Object.FindFirstObjectByType<StageManager>() != null) return;

            ShowTitleScreen();
        }

        private static void ShowTitleScreen()
        {
            var titleGO = new GameObject("TitleScreen");
            titleGO.AddComponent<TitleScreenUI>().Initialize(StartNewGame, ContinueGame);
        }

        private static void StartNewGame()
        {
            activeSlot = SaveSystem.SlotForNewGame();
            StartRun(null);
        }

        // 타이틀의 CONTINUE → 불러오기 창에서 고른 슬롯을 이어한다. 이후 자동 저장도 이 슬롯에 기록된다.
        private static void ContinueGame(int slot)
        {
            activeSlot = slot;
            StartRun(SaveSystem.Load(slot));
        }

        // save가 있으면 레벨/경험치/골드/퀘스트 진행을 되돌린 뒤, 항상 마을에서 시작한다.
        private static void StartRun(SaveData save)
        {
            player = SpawnPlayer();
            Monster.AnyDied -= HandleMonsterDiedForDrops; // 다시 시작할 때 중복 구독되지 않게
            Monster.AnyDied += HandleMonsterDiedForDrops;
            // 릴리의 몬스터 도감: 어떤 몬스터든 쓰러지면 이름별 처치 수를 센다(새 게임이면 빈 도감에서 시작).
            Bestiary.Restore(save?.bestiaryKills);
            TreasureChests.Restore(save?.openedChests);
            WorldFlags.Restore(save?.worldFlags);
            Monster.AnyDied -= RecordBestiaryKill;
            Monster.AnyDied += RecordBestiaryKill;
            var inventory = player.GetComponent<Inventory>();
            bossIntroSeen = save != null && save.bossIntroSeen;
            lunabelIntroSeen = save != null && save.lunabelIntroSeen;
            sandDragonIntroSeen = save != null && save.sandDragonIntroSeen;
            seaChaseIntroSeen = save != null && save.seaChaseIntroSeen;
            lunabelGazeTalkSeen = save != null && save.lunabelGazeTalkSeen;
            endingSeen = save != null && save.endingSeen;
            noaFirstMeetingPending = save == null || save.noaFirstMeetingPending;
            isisFirstMeetingPending = save == null || save.isisFirstMeetingPending;
            if (save != null)
            {
                player.RestoreProgress(save.level, save.exp, save.gold);
                inventory.RestoreItems(save.items);
                inventory.RestoreEquipped(save.equipped);
                inventory.RestoreEnhanceLevels(save.enhanceLevels);
                player.RestoreTalents(save.blessingTalents);
                // 예전 축복(골드로 기도하던 두 방식 — 총 단계 하나 / 갈래별 단계) 세이브: 예전 단계는 없애고 거기에 쓴
                // 골드를 전부 돌려준다(특성 트리는 레벨업 점수로 찍는다). 축복 화면을 처음 열 때 한 번 안내한다.
                int oldPrayers = save.blessingLevel;
                if (save.blessingTracks != null) foreach (int lv in save.blessingTracks) oldPrayers += Mathf.Max(0, lv);
                if (oldPrayers > 0)
                {
                    int refund = BlessingRules.TotalGoldSpent(oldPrayers);
                    player.AddGold(refund);
                    player.BlessingRefundNotice = refund;
                }
                // 스킬 배우기가 생기기 전의 세이브는 이미 모든 스킬을 쓰던 캐릭터라 전부 익힌 채로 시작한다.
                var skillBook = player.GetComponent<SkillBook>();
                if (save.skillBookSaved) skillBook.Restore(save.learnedSkills);
                else skillBook.LearnAll();
            }

            BuildHud(player);
            if (save != null)
            {
                questManager.RestoreState(save.questIndex, (QuestStatus)save.questStatus, save.questProgress);
                dailyQuestManager.RestoreState(save.dailyQuestResetDate, save.dailyQuestIndex, (DailyQuestStatus)save.dailyQuestStatus, save.dailyQuestProgress);
            }
            worldMapUI.SetUnlockSeen(save == null ? 0 : save.mapUnlockSeen >= 0 ? save.mapUnlockSeen : questManager.QuestIndex);

            // 기본 무기: 처음 시작하는 캐릭터든 이어하기든 항상 최소 1개는 가방에 갖고 있다(장착은 직접).
            EnsureDefaultWeapon(inventory);
            // 새로 시작하는 캐릭터는 회복 포션 몇 개를 들고 시작한다(이어하기는 저장된 개수 그대로).
            if (save == null) inventory.AddItem(ItemDatabase.Potion, StarterPotionCount);

            // 자동 저장: 퀘스트 상태가 바뀔 때, 레벨업할 때, 마을에 들어올 때(BuildVillage), 게임을 끌 때(AutoSaveOnQuit).
            questManager.OnChanged += SaveGame;
            dailyQuestManager.OnChanged += SaveGame;
            player.OnLevelUp += _ => SaveGame();
            // 레벨업마다 축복 점수가 1점 생긴다 — 이리아에게 가면 찍을 수 있다고 알려 준다.
            player.OnLevelUp += p => ChoicePopup.Toast($"축복 점수 +1  (남은 점수 {p.TalentPointsAvailable}) — 이리아에게 가 보자", true, 2f);

            // 새 게임은 도입 스토리를 본 뒤 튜토리얼 숲에서 눈을 뜬다(숲은 스토리 뒤에 미리 지어 둔다). 이어하기는 늘 마을 한가운데에서 시작한다.
            currentStage = save == null ? TutorialStage : VillageStage;
            BuildStage(enteringForward: true, VillageSpawn.Center);
            if (save == null) storyIntroUI.Play();
        }

        private static void RecordBestiaryKill(Monster monster)
        {
            if (monster != null && monster.Data != null) Bestiary.Record(monster.Data.monsterName);
        }

        // 기본 무기는 가방에 넣어주기만 하고 장착은 하지 않는다. 장비 칸은 플레이어가 직접 장착하기 전까지 비워 둔다.
        private static void EnsureDefaultWeapon(Inventory inventory)
        {
            if (inventory.GetCount(ItemDatabase.ReaperScythe.id) <= 0)
            {
                inventory.AddItem(ItemDatabase.ReaperScythe);
            }
        }

        // 게임오버 화면의 RETRY: 현재 판을 정리하고 지금 쓰던 슬롯의 마지막 저장 지점(없으면 처음)부터 다시 시작한다.
        public static void RestartGame()
        {
            TeardownRun();
            StartRun(SaveSystem.Load(activeSlot));
        }

        // 현재 레벨/경험치/퀘스트 진행을 지금 쓰는 슬롯에 저장한다(자동 저장).
        public static void SaveGame()
        {
            SaveGameToSlot(activeSlot);
        }

        // 노아의 저장 화면에서 고른 슬롯에 저장한다. 이후 자동 저장도 이 슬롯에 기록된다.
        // 죽은 상태로는 저장하지 않는다(게임오버 직후 덮어쓰기 방지). 저장했으면 true.
        public static bool SaveGameToSlot(int slot)
        {
            if (player == null || player.IsDead || questManager == null || dailyQuestManager == null) return false;

            activeSlot = slot;
            SaveSystem.Save(slot, new SaveData
            {
                level = player.Level,
                exp = player.CurrentExp,
                gold = player.Gold,
                items = player.GetComponent<Inventory>().ToSaveEntries(),
                equipped = player.GetComponent<Inventory>().ToEquipSaveEntries(),
                enhanceLevels = player.GetComponent<Inventory>().ToEnhanceSaveEntries(),
                blessingLevel = 0,
                blessingTalents = player.TalentsToSave(),
                questIndex = questManager.QuestIndex,
                questStatus = (int)questManager.Status,
                questProgress = questManager.Progress,
                dailyQuestIndex = dailyQuestManager.QuestIndex,
                dailyQuestStatus = (int)dailyQuestManager.Status,
                dailyQuestProgress = dailyQuestManager.Progress,
                dailyQuestResetDate = dailyQuestManager.ResetDate,
                bossIntroSeen = bossIntroSeen,
                lunabelIntroSeen = lunabelIntroSeen,
                sandDragonIntroSeen = sandDragonIntroSeen,
                seaChaseIntroSeen = seaChaseIntroSeen,
                lunabelGazeTalkSeen = lunabelGazeTalkSeen,
                endingSeen = endingSeen,
                noaFirstMeetingPending = noaFirstMeetingPending,
                isisFirstMeetingPending = isisFirstMeetingPending,
                mapUnlockSeen = worldMapUI != null ? worldMapUI.UnlockSeen : 0,
                bestiaryKills = Bestiary.ToSaveEntries(),
                learnedSkills = player.GetComponent<SkillBook>().ToSaveEntries(),
                skillBookSaved = true,
                openedChests = TreasureChests.ToSave(),
                worldFlags = WorldFlags.ToSave(),
            });
            saveToast?.Show();
            return true;
        }

        // 게임오버 화면의 TITLE: 현재 판을 통째로 정리하고 타이틀 화면으로 돌아간다.
        public static void ReturnToTitle()
        {
            TeardownRun();
            ShowTitleScreen();
        }

        // 플레이어/맵/HUD를 전부 지우고 시간 배속도 원래대로 되돌린다.
        private static void TeardownRun()
        {
            BgmPlayer.Stop();
            Aethoria.Fx.AmbientFx.Apply(Aethoria.Fx.AmbientPreset.None); // 맵 입자·환경음도 끈다
            if (stageRoot != null) Object.Destroy(stageRoot);
            if (player != null) Object.Destroy(player.gameObject);
            if (hudRoot != null) Object.Destroy(hudRoot);
            stageRoot = null;
            player = null;
            hudRoot = null;
            stageClearUI = null;
            bossHealthUI = null;
            npcDialogueUI = null;
            worldMapUI = null;
            portalConfirmUI = null;
            inventoryUI = null;
            statusUI = null;
            shopUI = null;
            enhanceUI = null;
            blessingUI = null;
            skillLearnUI = null;
            questTrackerUI = null;
            bestiaryUI = null;
            questManager = null;
            dailyQuestManager = null;
            saveToast = null;
            saveScreen = null;
            tutorialUI = null;
            storyIntroUI = null;
            endingCreditsUI = null;
            Time.timeScale = 1f;
        }

        // 포탈을 타고 다음 스테이지로 넘어갈 때 호출된다. 기존 맵/몬스터/포탈을 지우고 새로 짓는다.
        public static void EnterNextStage()
        {
            if (currentStage >= TotalStages) return;

            currentStage++;
            BuildStage(enteringForward: true);
        }

        // 쓰러졌을 때(DeathReturnUI), 그리고 던전 보스를 잡고 열린 출구 포탈(OpenBossExitPortal)에 들어갔을 때 호출된다.
        // 체력/마나를 가득 채워준다. 기본은 던전에서 걸어 돌아온 것처럼 마을 오른쪽 끝(출구 포탈 쪽)에 세우고,
        // spawnAtNoa면 노아 바로 앞에 세운다.
        public static void ReturnToVillage(bool spawnAtNoa = false)
        {
            player.ClearMaxHpPenalty();
            player.Heal(player.MaxHp);
            player.RestoreMana(player.MaxMana);
            currentStage = VillageStage;
            BuildStage(enteringForward: false, spawnAtNoa ? VillageSpawn.Noa : VillageSpawn.Edge);
        }

        // 쓰러진 뒤 "재도전"을 골랐을 때(DeathReturnUI) 호출된다. 체력/마나를 채우고 쓰러진 그 맵을 처음 들어올 때처럼
        // 다시 짓는다 — 보스 맵이면 보스와 등장 연출부터(이미 본 연출은 Esc로 건너뛸 수 있다), 튜토리얼 숲이면 숲에서 다시 눈을 뜬다.
        public static void RetryCurrentStage()
        {
            player.ClearMaxHpPenalty();
            player.Heal(player.MaxHp);
            player.RestoreMana(player.MaxMana);
            // 절망의 언덕에서 쓰러져 다시 도전하면 걷는 길을 건너뛰고 결전장 입구에서 시작한다.
            if (currentStage == HillStage) hillStartAtArena = true;
            BuildStage(enteringForward: true);
        }

        // 마을의 이시스에게서 "연습장 입장"을 골랐을 때 호출된다.
        public static void EnterPracticeRoom()
        {
            currentStage = PracticeStage;
            BuildStage(enteringForward: true);
        }

        // 연습장 왼쪽 끝 포탈. 연습장 입구(이시스 앞)로 돌아간다.
        private static void LeavePracticeRoom()
        {
            currentStage = VillageStage;
            BuildStage(enteringForward: false, VillageSpawn.Isis);
        }

        // 월드맵에서 "광활한 평야"를 골랐을 때 호출된다. 항상 평야 1번째 맵부터 시작한다.
        public static void EnterPlains()
        {
            currentStage = PlainsStage1;
            BuildStage(enteringForward: true);
        }

        // 평야/폐허 1번째 맵 왼쪽 끝 포탈. 둘 다 마을과 바로 이어져 있어서 마을 출구 쪽(오른쪽 끝)으로 돌아간다.
        private static void LeaveFieldToVillage()
        {
            currentStage = VillageStage;
            BuildStage(enteringForward: false);
        }

        // 던전(어둠의 숲/평야/폐허/바다) 첫 맵의 왼쪽 포탈: "마을로 이동하시겠습니까?"에 예를 골라야 마을로 나간다.
        // 아니오면 그 자리에 남고, 포탈에서 한 번 벗어났다 다시 밟아야 또 묻는다.
        private static void ConfirmReturnToVillage(Portal portal)
        {
            if (portalConfirmUI == null)
            {
                LeaveFieldToVillage();
                return;
            }
            portalConfirmUI.Show(portal, "마을로 이동하시겠습니까?", LeaveFieldToVillage);
        }

        // 월드맵에서 "잊혀진 폐허"를 골랐을 때 호출된다. 항상 폐허 입구(1번째 맵)부터 시작한다.
        public static void EnterRuins()
        {
            currentStage = RuinsStage1;
            BuildStage(enteringForward: true);
        }

        // 월드맵에서 "푸른 바다"를 골랐을 때 호출된다. 항상 바다 1번째 맵부터 시작한다.
        public static void EnterBlueSea()
        {
            currentStage = BlueSeaStage1;
            BuildStage(enteringForward: true);
        }

        // 월드맵에서 "엔스룸 마을"을 골랐을 때 호출된다.
        public static void EnterEnsroom()
        {
            currentStage = EnsroomStage;
            BuildStage(enteringForward: true);
        }

        // 월드맵에서 "천상의 신전"을 골랐을 때 호출된다. 항상 왼쪽 끝 포탈 앞에서 시작한다.
        public static void EnterTemple()
        {
            currentStage = TempleStage;
            BuildStage(enteringForward: true);
        }

        // 월드맵에서 "절망의 언덕"을 골랐을 때 호출된다.
        public static void EnterHill()
        {
            currentStage = HillStage;
            BuildStage(enteringForward: true);
        }

        // 신전 광장의 성소 입구(SanctuaryEntrance)에서 [Z]를 누르면 호출된다.
        private static void EnterSanctuary()
        {
            currentStage = SanctuaryStage;
            BuildStage(enteringForward: true);
        }

        // 성소 안의 돌아가는 포탈. 천상의 신전의 성소 입구 앞으로 돌아간다. 신전에 그냥 들어오면
        // BuildTemple이 늘 왼쪽 끝에 스폰시키는데, 성소에서 나올 때 그러면 방금 나온 입구에서 멀리 떨어져 어색하다.
        // 플래그를 하나 세워서 이번 한 번만 성소 입구 자리에 세우게 한다.
        private static bool templeSpawnAtSanctuary;

        private static void LeaveSanctuary()
        {
            currentStage = TempleStage;
            templeSpawnAtSanctuary = true;
            BuildStage(enteringForward: false);
        }

        // 관리자 모드(AdminUI)의 순간이동 목록. 월드맵/포탈을 거치지 않고 그 맵을 처음 들어올 때처럼 바로 짓는다.
        private static readonly (string Name, int Stage)[] AdminWarpTargets =
        {
            ("마을", VillageStage),
            ("어둠의 숲 (입구)", 1),
            ("어둠의 숲 보스 (기사)", BossStage),
            ("광활한 평야 1", PlainsStage1),
            ("광활한 평야 보스 (슬라임)", PlainsStage3),
            ("잊혀진 폐허 1", RuinsStage1),
            ("잊혀진 폐허 보스 (황금 모래룡)", RuinsStage3),
            ("푸른 바다 1", BlueSeaStage1),
            ("푸른 바다 3 (붕괴)", BlueSeaStage3),
            ("푸른 바다 추격전", SeaChaseStage),
            ("푸른 바다 아쿠아 2차전", SeaArenaStage),
            ("엔스룸 마을", EnsroomStage),
            ("천상의 신전", TempleStage),
            ("이리아의 성소", SanctuaryStage),
            ("절망의 언덕 (루나벨)", HillStage),
        };

        public static int AdminWarpCount => AdminWarpTargets.Length;
        public static string AdminWarpName(int index) => AdminWarpTargets[index].Name;

        // 전투/대화/메뉴처럼 시간이 멈춘 창이 떠 있으면 그 창이 꼬이지 않게 이동하지 않는다. 이동했으면 true.
        public static bool AdminWarp(int index)
        {
            if (player == null || player.IsDead || stageRoot == null) return false;
            if (index < 0 || index >= AdminWarpTargets.Length || Time.timeScale == 0f) return false;

            tutorialUI?.HideGuide();
            player.ClearMaxHpPenalty();
            player.Heal(player.MaxHp);
            player.RestoreMana(player.MaxMana);
            currentStage = AdminWarpTargets[index].Stage;
            BuildStage(enteringForward: true);
            return true;
        }

        // 맵 장치 상태를 처음으로 되돌린다: 주운 열쇠, 연 덮개·봉인 문, 부순 벽·가지·바위, 푼 퍼즐, 잡은 정예, 연 전멸방,
        // 연 보물상자. 스토리·퀘스트·레벨·소지품은 그대로 둔다. 지금 있는 맵은 새로 지어서(방 구조 지역이면 입구부터) 바로 보이게 한다.
        public static bool AdminResetMap()
        {
            if (player == null || player.IsDead || stageRoot == null || Time.timeScale == 0f) return false;
            WorldFlags.Restore(null);
            TreasureChests.Restore(null);
            tutorialUI?.HideGuide();
            BuildStage(enteringForward: true);
            return true;
        }

        // AdminResetMap의 반대: 모든 지역의 맵 장치를 다 푼 상태로 만든다(열쇠 3개·덮개·가지·봉인 문·퍼즐·정예·부서지는 벽·전멸방).
        // 보물상자는 열지 않고 그대로 둔다(직접 열어 보일 수 있게). 지금 맵은 새로 지어서 열린 길이 바로 보이게 한다.
        private static readonly string[] AllMapFlags =
        {
            FlagKey1, FlagKey2, FlagKey3, FlagBranch, FlagHatch, FlagElite, KeyGate.OpenFlag, CrystalPuzzle.SolvedFlag,
            FlagPlainsVine, FlagPlainsElite, FlagPlainsBells, FlagPlainsArena,
            FlagRuinsWall, FlagRuinsElite, FlagRuinsRunes, FlagRuinsArena,
            FlagSeaCoral, FlagSeaElite, FlagSeaTide, FlagSeaArena,
        };

        public static bool AdminSolveMap()
        {
            if (player == null || player.IsDead || stageRoot == null || Time.timeScale == 0f) return false;
            foreach (var flag in AllMapFlags) WorldFlags.Set(flag);
            tutorialUI?.HideGuide();
            BuildStage(enteringForward: true);
            return true;
        }

        // 평야/폐허 맵 사이를 오갈 때 쓴다(일반 스테이지의 EnterNextStage/EnterPreviousStage와 달리 음수
        // 번호라 그쪽 함수의 경계 체크와 맞지 않아 따로 둔다). 숫자가 작을수록(더 음수) 더 깊은 맵이다.
        private static void EnterNextFieldStage()
        {
            currentStage--;
            BuildStage(enteringForward: true);
        }

        private static void EnterPreviousFieldStage()
        {
            currentStage++;
            BuildStage(enteringForward: false);
        }

        // 맵 왼쪽 끝 포탈을 타고 이전 스테이지(마을 포함)로 돌아갈 때 호출된다.
        // 몬스터를 잡은 기록 등은 남지 않고, 그 스테이지를 처음 들어갈 때처럼 새로 짓는다.
        public static void EnterPreviousStage()
        {
            if (currentStage <= VillageStage) return;

            currentStage--;
            BuildStage(enteringForward: false);
        }

        // enteringForward: 오른쪽 끝(다음 스테이지 포탈)으로 들어왔으면 true, 왼쪽 끝(이전 스테이지
        // 포탈)을 타고 되돌아왔으면 false. 되돌아왔을 때 항상 맵 왼쪽 끝에 스폰시키면, 방금 걸어
        // 나온 왼쪽 포탈과 정반대 쪽인 오른쪽 끝(원래 그 스테이지로 들어갔던 포탈)까지 다시 가로질러
        // 걸어야 해서 어색하다. 대신 들어온 방향의 반대쪽 끝에 스폰시켜서 방향 감각이 이어지게 한다.
        private static Aethoria.Fx.AmbientPreset AmbientPresetForStage()
        {
            // 비밀의 장소는 들어온 지역의 분위기를 그대로 쓴다.
            if (currentStage == SecretStage)
            {
                int saved = currentStage;
                currentStage = secretOriginStage;
                var preset = AmbientPresetForStage();
                currentStage = saved;
                return preset;
            }
            if (currentStage == VillageStage) return Aethoria.Fx.AmbientPreset.Village;
            if (currentStage == TutorialStage || currentStage == 1 || currentStage == 2 || currentStage == BossStage) return Aethoria.Fx.AmbientPreset.Forest;
            if (currentStage <= PlainsStage1 && currentStage >= PlainsStage3) return Aethoria.Fx.AmbientPreset.Plains;
            if (currentStage <= RuinsStage1 && currentStage >= RuinsStage3) return Aethoria.Fx.AmbientPreset.Desert;
            if ((currentStage <= BlueSeaStage1 && currentStage >= BlueSeaStage3) || currentStage == SeaChaseStage || currentStage == SeaArenaStage)
                return Aethoria.Fx.AmbientPreset.Sea;
            if (currentStage == TempleStage || currentStage == SanctuaryStage) return Aethoria.Fx.AmbientPreset.Heaven;
            if (currentStage == HillStage) return Aethoria.Fx.AmbientPreset.Crimson;
            if (currentStage == PracticeStage || currentStage == EnsroomStage) return Aethoria.Fx.AmbientPreset.Indoor;
            return Aethoria.Fx.AmbientPreset.None;
        }

        // 맵 조명 색을 네크로시아에게 살짝 입힌다. 캐릭터는 중립적인 흰 조명으로 그려져 있어서 노을 진 폐허·밤 숲 같은
        // 배경 위에서 오려 붙인 것처럼 보였다. 게임 코드에서 플레이어 그림 색을 따로 바꾸는 곳은 없어서(피격 번쩍임은 덧그림)
        // 그림 색에 곱한다. 몬스터는 AI가 피격·광폭 색을 직접 바꾸므로 건드리지 않는다.
        private static Color SceneTintFor(Aethoria.Fx.AmbientPreset preset)
        {
            switch (preset)
            {
                case Aethoria.Fx.AmbientPreset.Village: return new Color(0.9f, 0.88f, 1f);    // 보랏빛 밤 마을
                case Aethoria.Fx.AmbientPreset.Forest: return new Color(0.84f, 0.87f, 0.97f); // 차가운 밤 숲
                case Aethoria.Fx.AmbientPreset.Plains: return new Color(1f, 0.99f, 0.95f);    // 맑은 낮
                case Aethoria.Fx.AmbientPreset.Desert: return new Color(1f, 0.88f, 0.74f);    // 황금빛 노을
                case Aethoria.Fx.AmbientPreset.Sea: return new Color(0.9f, 0.96f, 1f);        // 푸른 바다빛
                case Aethoria.Fx.AmbientPreset.Heaven: return new Color(1f, 0.98f, 0.93f);    // 따뜻한 천상의 빛
                case Aethoria.Fx.AmbientPreset.Crimson: return new Color(1f, 0.8f, 0.8f);     // 붉은 달
                case Aethoria.Fx.AmbientPreset.Indoor:
                    return currentStage == EnsroomStage ? new Color(1f, 0.98f, 0.94f) : new Color(1f, 0.95f, 0.88f);
                default: return Color.white;
            }
        }

        private static void ApplyPlayerSceneTint(Aethoria.Fx.AmbientPreset preset)
        {
            if (player == null) return;
            var visual = player.transform.Find("Visual");
            var sr = visual != null ? visual.GetComponent<SpriteRenderer>() : player.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = SceneTintFor(preset);
        }

        // 스테이지를 짓고 나서 던전 지도(MinimapUI)를 지금 방에 맞춘다.
        private static void BuildStage(bool enteringForward, VillageSpawn villageSpawn = VillageSpawn.Edge)
        {
            BuildStageCore(enteringForward, villageSpawn);
            RefreshMinimap();
        }

        private static void BuildStageCore(bool enteringForward, VillageSpawn villageSpawn)
        {
            if (stageRoot != null) Object.Destroy(stageRoot);
            stageRoot = new GameObject("StageRoot");

            // 보스를 잡지 않고 포탈로 스테이지를 떠나면 보스 몬스터는 stageRoot와 함께 사라지지만
            // OnDied가 발생하지 않아 체력바는 그대로 남아있었다. 스테이지를 새로 지을 때마다 일단
            // 끊어두고, 보스 스테이지면 SpawnBoss에서 다시 Bind해서 보여준다.
            bossHealthUI.Hide();
            stageClearUI.Cancel();
            // 튜토리얼 숲에서는 오른쪽 위 퀘스트를 숨기고, 숲을 나와 마을에 들어오면 띄운다.
            questTrackerUI?.SetSuppressed(currentStage == TutorialStage);
            // 알리아스 보스 맵에 들어오면 보스전 곡을 처음부터 튼다. 3분 넘게 싸워 곡이 끝나면 도입부를 건너뛰고 15초부터 되풀이한다.
            // 마을은 마을 곡을 끝까지 듣고 처음부터 되풀이한다(마을 안에서 맵을 다시 지어도 끊지 않고 이어서 튼다).
            if (currentStage == BossStage) BgmPlayer.Play(AliasBossBgmPath, AliasBossBgmLoopStart);
            else if (currentStage == VillageStage) BgmPlayer.Play(VillageBgmPath, VillageBgmSilence, restartIfPlaying: false, startSeconds: VillageBgmSilence);
            // 폐허는 세 맵 내내 곡을 끊지 않고 이어서 튼다. 마지막 맵에서는 모래룡이 솟아오르는 순간 등장 연출
            // (SandDragonIntroCutscene)이 보스 곡으로 바꾼다.
            else if (currentStage <= RuinsStage1 && currentStage >= RuinsStage3) BgmPlayer.Play(RuinsBgmPath, 0f, restartIfPlaying: false);
            // 추격전/2차전은 아쿠아 전용 곡(없으면 모래룡 보스 곡을 임시로) — 2차전은 추격전에서 곧바로 이어지므로 끊지 않는다.
            else if (currentStage == SeaChaseStage) BgmPlayer.Play(AquaBossBgmPath, 0f, fallbackPath: SandDragonIntroCutscene.BossBgmPath);
            else if (currentStage == SeaArenaStage) BgmPlayer.Play(AquaBossBgmPath, 0f, restartIfPlaying: false, fallbackPath: SandDragonIntroCutscene.BossBgmPath);
            // 여러 맵으로 이어진 지역은 맵을 넘어가도 곡을 끊지 않는다(restartIfPlaying: false).
            else if (currentStage == TutorialStage) BgmPlayer.Play(TutorialForestBgmPath, 0f, restartIfPlaying: false, fallbackPath: DarkForestBgmPath);
            else if (currentStage == 1 || currentStage == 2) BgmPlayer.Play(DarkForestBgmPath, 0f, restartIfPlaying: false);
            else if (currentStage <= PlainsStage1 && currentStage >= PlainsStage3) BgmPlayer.Play(PlainsBgmPath, 0f, restartIfPlaying: false);
            else if (currentStage <= BlueSeaStage1 && currentStage >= BlueSeaStage3) BgmPlayer.Play(BlueSeaBgmPath, 0f, restartIfPlaying: false);
            else if (currentStage == TempleStage || currentStage == SanctuaryStage)
                BgmPlayer.Play(HeavenBgmPath, 0f, restartIfPlaying: false);
            else if (currentStage == HillStage) BgmPlayer.Play(LunabelBossBgmPath, 0f);
            else if (currentStage == PracticeStage)
                BgmPlayer.Play(TrainingHallBgmPath, 0f, restartIfPlaying: false, startSeconds: 0f, fallbackPath: VillageBgmPath);
            else if (currentStage == EnsroomStage)
                BgmPlayer.Play(EnsroomBgmPath, 0f, restartIfPlaying: false, startSeconds: 0f, fallbackPath: VillageBgmPath);
            // 비밀의 장소는 들어온 지역 곡을 끊지 않고 그대로 이어 튼다.
            else if (currentStage == SecretStage) { }
            else BgmPlayer.Stop();
            // 레벨/경험치 틀은 마을(노아의 마을, 엔스룸 마을)에서만 보인다.
            PlayerHUD.SetLevelVisible(currentStage == VillageStage || currentStage == EnsroomStage);
            // 맵 분위기: 떠다니는 입자·배경 앰비언스·발소리 재질.
            Aethoria.Fx.AmbientFx.Apply(AmbientPresetForStage());
            ApplyPlayerSceneTint(AmbientPresetForStage());
            // 던전 첫 맵에 앞으로 걸어 들어왔으면(마을/월드맵에서 출발) 스테이지 클리어 창에 보여줄 획득 기록을 새로 시작한다.
            // 방 사이를 넘어갈 때는(어둠의 숲·평야·폐허·바다 방) 기록을 새로 시작하지 않는다.
            if (enteringForward && !forestRoomTransition && !regionRoomTransition && (currentStage == 1 || currentStage == PlainsStage1 || currentStage == RuinsStage1 || currentStage == BlueSeaStage1 || currentStage == HillStage)) stageClearUI.BeginRun();

            if (currentStage == VillageStage)
            {
                BuildVillage(enteringForward, villageSpawn);
                return;
            }

            if (currentStage == TutorialStage)
            {
                BuildTutorialForest();
                return;
            }

            if (currentStage == PracticeStage)
            {
                BuildPracticeRoom();
                return;
            }

            if (currentStage == SecretStage)
            {
                BuildSecretRoom();
                return;
            }

            // 평야·폐허·바다 방(GameBootstrap.RegionRooms). 예전 1·2번 맵 대신 방 7개가 이어진다. 보스 맵(3번)은 아래 그대로.
            if (RegionForStage(currentStage) != null)
            {
                BuildRegionRoom();
                return;
            }

            if (currentStage <= PlainsStage1 && currentStage >= PlainsStage3)
            {
                BuildPlains(PlainsStage1 - currentStage + 1, enteringForward);
                return;
            }

            if (currentStage == EnsroomStage)
            {
                BuildEnsroom();
                return;
            }

            if (currentStage == TempleStage)
            {
                BuildTemple(enteringForward);
                return;
            }

            if (currentStage == SanctuaryStage)
            {
                BuildSanctuary();
                return;
            }

            if (currentStage == HillStage)
            {
                BuildHill();
                return;
            }

            if (currentStage <= RuinsStage1 && currentStage >= RuinsStage3)
            {
                BuildRuins(RuinsStage1 - currentStage + 1, enteringForward);
                return;
            }

            if (currentStage <= BlueSeaStage1 && currentStage >= BlueSeaStage3)
            {
                BuildBlueSea(BlueSeaStage1 - currentStage + 1, enteringForward);
                return;
            }

            if (currentStage == SeaChaseStage)
            {
                BuildSeaChase();
                return;
            }

            if (currentStage == SeaArenaStage)
            {
                BuildSeaArena();
                return;
            }

            // 어둠의 숲 방(비선형 맵 시범, GameBootstrap.ForestRooms). 예전 1·2번 맵 대신 방 9개가 이어진다. 보스방(3)은 아래 그대로.
            if (currentStage == 1 || currentStage == 2)
            {
                BuildForestRoom();
                return;
            }

            // 어둠의 숲 1·2는 다른 사냥터처럼 40칸으로 넓혀 카메라가 따라가며 탐험하게 하고, 보스 맵만 한 화면(20칸)이다.
            // 배경은 한 장짜리 그림 대신 여러 층의 시차 배경(하늘·먼 숲·가까운 숲·흙길)을 깐다.
            BuildMap(stageRoot.transform, currentStage == BossStage ? MapWidth : ForestWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                null, BackgroundGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;
            SpawnParallax(ForestParallaxLayers(), xMin, xMax, yMin);
            SpawnForestBackdrop(stageRoot.transform, yMin, currentStage);

            player.SetCombatLocked(false);
            float playerSpawnX = enteringForward ? xMin + 1.5f : xMax - 1.5f;
            PlacePlayerAtSpawn(playerSpawnX, yMin);
            FitCamera(xMin, xMax, yMin);

            SpawnForestPlatforms(stageRoot.transform, yMin, currentStage);
            SpawnForestSecrets(stageRoot.transform, yMin, currentStage);

            // 보스 맵에는 보스만 나오고 일반 몬스터는 나오지 않는다.
            if (currentStage == BossStage)
            {
                SpawnBoss(stageRoot.transform, yMin, xMax);
            }
            else
            {
                // 기본 간격(2.5)은 맵이 좁아지니 기사 세 마리가 한데 뭉쳐 나와 피할 틈이 없었다.
                // 서로 최소 4.5 떨어뜨려서 최소 한쪽은 지나갈 길이 남게 한다.
                SpawnKnights(stageRoot.transform, yMin,
                    MonsterSpawner.PickSpawnXs(ForestKnightsPerStage, xMin, xMax, playerSpawnX, minSpacing: 4.5f));
            }

            // 왼쪽 끝은 이전 스테이지(마을 포함)로 돌아가는 포탈, 오른쪽 끝은 마지막 스테이지가
            // 아닐 때만 다음 스테이지로 가는 포탈. 보스방은 들어오면 보스와 싸울 수밖에 없어서 포탈을 두지 않는다
            // (이기면 맵 끝에 마을로 돌아가는 포탈이 열리고, 지면 마을로 돌아간다).
            // 첫 맵의 왼쪽 포탈은 마을로 나가는 길이라 바로 보내지 않고 한 번 묻는다.
            if (currentStage != BossStage)
            {
                var backPortal = SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true, framed: ForestPortalStyle);
                if (currentStage == 1) backPortal.SetCustomTrigger(() => ConfirmReturnToVillage(backPortal));
            }
            if (currentStage < TotalStages)
            {
                SpawnPortal(stageRoot.transform, xMax - 0.5f, yMin, backward: false, framed: ForestPortalStyle);
            }

            var stageManagerGO = new GameObject("StageManager");
            stageManagerGO.transform.SetParent(stageRoot.transform);
            stageManagerGO.AddComponent<StageManager>().Initialize(player);

            Debug.Log($"Aethoria: 맵 {currentStage}/{TotalStages} 진입");
        }
    }
}
