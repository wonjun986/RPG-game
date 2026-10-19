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
    public static class GameBootstrap
    {
        private const int MapWidth = 20;
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
        // 연습장: 마을의 이시스에게 말을 걸어 들어가는 허수아비 맵. 몬스터도 경험치도 없고 마나/체력이 계속 가득 찬다.
        private const int PracticeStage = -4;
        private const int PracticeWidth = 28;
        // 잊혀진 폐허: 평야처럼 월드맵에서 바로 들어가는 3연속 필드(-5 → -6 → -7). 첫 맵만 폐허 입구 배경이고,
        // 그 뒤로는 평평한 폐허 마당 배경이 이어진다. 떠 있는 발판 배치는 세 맵이 모두 다르다.
        private const int RuinsStage1 = -5;
        private const int RuinsStage3 = -7;
        private const int TotalRuinsStages = 3;
        private const int RuinsWidth = 40;
        // 엔스룸 마을: 월드맵에서 들어가는 두 번째 안전 지역. 몬스터 없이 분수 광장을 걸어 다닐 수 있다.
        private const int EnsroomStage = -8;
        private const int EnsroomWidth = 32;
        // 이리아의 성소: 천상으로 향하는 계단의 성소 건물(겉모습)로 들어가면 나오는 작은 실내 맵. 막다른
        // 방이라 돌아가는 포탈 하나만 있다.
        private const int SanctuaryStage = -9;
        private const int SanctuaryWidth = 20;
        // 천상으로 향하는 계단: 월드맵에서 들어가는 세 번째 안전 지역. 길게 이어진 대리석 회랑(계단)을
        // 오른쪽 끝까지 걸어가면 포탈로 천상의 신전(-11)에 들어간다. 원래 엔스룸 분수 앞에 있던 성소
        // 건물은 신전으로 옮겨 왔고, 엔스룸은 다시 건물 없는 빈 광장으로 되돌아갔다.
        private const int StairsStage = -10;
        private const int StairsWidth = 40;
        private const int TempleStage = -11;
        private const int TempleWidth = 32;
        // 절망의 언덕: 월드맵에서 들어가는 최종보스 맵. 일반 몬스터 없이 언덕 끝에 루나벨만 서 있다.
        private const int HillStage = -12;
        private const int HillWidth = 32;
        // 튜토리얼 숲: 새 게임을 시작하면 도입 스토리 뒤에 눈을 뜨는 어둠의 숲 맵. 이동/전투를 배우고(ForestTutorial)
        // 오른쪽 끝 포탈로 마을에 들어간다. 이어하기로는 다시 오지 않는다.
        private const int TutorialStage = -13;

        // 마을에 들어올 때 서는 자리. Edge는 들어온 방향에 따라 왼쪽/오른쪽 끝.
        private enum VillageSpawn { Edge, Noa, Isis }

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
        private static InventoryUI inventoryUI;
        private static StatusUI statusUI;
        private static ShopUI shopUI;
        private static EnhanceUI enhanceUI;
        private static BlessingUI blessingUI;
        private static QuestManager questManager;
        private static DailyQuestManager dailyQuestManager;
        private static SaveToastUI saveToast;
        private static SaveScreenUI saveScreen;
        private static TutorialUI tutorialUI;
        private static StoryIntroUI storyIntroUI;
        // 보스 기사 등장 연출을 이미 봤는지(세이브에 저장). 두 번째 만남부터는 등장 대사를 Esc로 건너뛸 수 있다.
        private static bool bossIntroSeen;
        private static bool lunabelIntroSeen;
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
            var inventory = player.GetComponent<Inventory>();
            bossIntroSeen = save != null && save.bossIntroSeen;
            lunabelIntroSeen = save != null && save.lunabelIntroSeen;
            noaFirstMeetingPending = save == null || save.noaFirstMeetingPending;
            isisFirstMeetingPending = save == null || save.isisFirstMeetingPending;
            if (save != null)
            {
                player.RestoreProgress(save.level, save.exp, save.gold);
                inventory.RestoreItems(save.items);
                inventory.RestoreEquipped(save.equipped);
                inventory.RestoreEnhanceLevels(save.enhanceLevels);
                player.RestoreBlessingLevel(save.blessingLevel);
            }

            BuildHud(player);
            if (save != null)
            {
                questManager.RestoreState(save.questIndex, (QuestStatus)save.questStatus, save.questProgress);
                dailyQuestManager.RestoreState(save.dailyQuestResetDate, save.dailyQuestIndex, (DailyQuestStatus)save.dailyQuestStatus, save.dailyQuestProgress);
            }

            // 기본 무기: 처음 시작하는 캐릭터든 이어하기든 항상 최소 1개는 가방에 갖고 있다(장착은 직접).
            EnsureDefaultWeapon(inventory);
            // 새로 시작하는 캐릭터는 회복 포션 몇 개를 들고 시작한다(이어하기는 저장된 개수 그대로).
            if (save == null) inventory.AddItem(ItemDatabase.Potion, StarterPotionCount);

            // 자동 저장: 퀘스트 상태가 바뀔 때, 레벨업할 때, 마을에 들어올 때(BuildVillage), 게임을 끌 때(AutoSaveOnQuit).
            questManager.OnChanged += SaveGame;
            dailyQuestManager.OnChanged += SaveGame;
            player.OnLevelUp += _ => SaveGame();

            // 새 게임은 도입 스토리를 본 뒤 튜토리얼 숲에서 눈을 뜬다(숲은 스토리 뒤에 미리 지어 둔다). 이어하기는 늘 마을에서 시작한다.
            currentStage = save == null ? TutorialStage : VillageStage;
            BuildStage(enteringForward: true);
            if (save == null) storyIntroUI.Play();
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
                blessingLevel = player.BlessingLevel,
                questIndex = questManager.QuestIndex,
                questStatus = (int)questManager.Status,
                questProgress = questManager.Progress,
                dailyQuestIndex = dailyQuestManager.QuestIndex,
                dailyQuestStatus = (int)dailyQuestManager.Status,
                dailyQuestProgress = dailyQuestManager.Progress,
                dailyQuestResetDate = dailyQuestManager.ResetDate,
                bossIntroSeen = bossIntroSeen,
                lunabelIntroSeen = lunabelIntroSeen,
                noaFirstMeetingPending = noaFirstMeetingPending,
                isisFirstMeetingPending = isisFirstMeetingPending,
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
            inventoryUI = null;
            statusUI = null;
            shopUI = null;
            enhanceUI = null;
            blessingUI = null;
            questManager = null;
            dailyQuestManager = null;
            saveToast = null;
            saveScreen = null;
            tutorialUI = null;
            storyIntroUI = null;
            Time.timeScale = 1f;
        }

        // 포탈을 타고 다음 스테이지로 넘어갈 때 호출된다. 기존 맵/몬스터/포탈을 지우고 새로 짓는다.
        public static void EnterNextStage()
        {
            if (currentStage >= TotalStages) return;

            currentStage++;
            BuildStage(enteringForward: true);
        }

        // 스테이지 클리어 창의 "다시하기": 체력/마나를 채우고 방금 깬 던전의 첫 맵부터 다시 들어간다.
        private static void RetryDungeon(int firstStage)
        {
            player.Heal(player.MaxHp);
            player.RestoreMana(player.MaxMana);
            currentStage = firstStage;
            BuildStage(enteringForward: true);
        }

        // 쓰러졌을 때(DeathReturnUI), 그리고 던전 보스를 잡고 스테이지 클리어 창에서 "마을로 돌아가기"를 골랐을 때(StageClearUI) 호출된다.
        // 체력/마나를 가득 채워준다. 기본은 던전에서 걸어 돌아온 것처럼 마을 오른쪽 끝(출구 포탈 쪽)에 세우고,
        // spawnAtNoa면 노아 바로 앞에 세운다.
        public static void ReturnToVillage(bool spawnAtNoa = false)
        {
            player.Heal(player.MaxHp);
            player.RestoreMana(player.MaxMana);
            currentStage = VillageStage;
            BuildStage(enteringForward: false, spawnAtNoa ? VillageSpawn.Noa : VillageSpawn.Edge);
        }

        // 쓰러진 뒤 "재도전"을 골랐을 때(DeathReturnUI) 호출된다. 체력/마나를 채우고 쓰러진 그 맵을 처음 들어올 때처럼
        // 다시 짓는다 — 보스 맵이면 보스와 등장 연출부터(이미 본 연출은 Esc로 건너뛸 수 있다), 튜토리얼 숲이면 숲에서 다시 눈을 뜬다.
        public static void RetryCurrentStage()
        {
            player.Heal(player.MaxHp);
            player.RestoreMana(player.MaxMana);
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

        // 월드맵에서 "잊혀진 폐허"를 골랐을 때 호출된다. 항상 폐허 입구(1번째 맵)부터 시작한다.
        public static void EnterRuins()
        {
            currentStage = RuinsStage1;
            BuildStage(enteringForward: true);
        }

        // 월드맵에서 "엔스룸 마을"을 골랐을 때 호출된다.
        public static void EnterEnsroom()
        {
            currentStage = EnsroomStage;
            BuildStage(enteringForward: true);
        }

        // 월드맵에서 "천상으로 향하는 계단"을 골랐을 때 호출된다. 항상 계단 입구(왼쪽 끝)부터 시작한다.
        public static void EnterTemple()
        {
            currentStage = StairsStage;
            BuildStage(enteringForward: true);
        }

        // 월드맵에서 "절망의 언덕"을 골랐을 때 호출된다.
        public static void EnterHill()
        {
            currentStage = HillStage;
            BuildStage(enteringForward: true);
        }

        // 천상의 신전의 성소 입구(SanctuaryEntrance)에서 [Z]를 누르면 호출된다.
        private static void EnterSanctuary()
        {
            currentStage = SanctuaryStage;
            BuildStage(enteringForward: true);
        }

        // 성소 안의 돌아가는 포탈. 천상의 신전의 성소 입구 앞으로 돌아간다. 신전에 그냥 들어오면
        // BuildTemple이 늘 왼쪽 끝(계단으로 돌아가는 포탈 쪽)에 스폰시키는데, 성소에서 나올 때는 그러면 방금 나온
        // 입구에서 저 멀리 가로질러 걸어야 해서 어색하다. 플래그를 하나 세워서 이번 한 번만 성소
        // 입구 자리에 세우게 한다.
        private static bool templeSpawnAtSanctuary;

        private static void LeaveSanctuary()
        {
            currentStage = TempleStage;
            templeSpawnAtSanctuary = true;
            BuildStage(enteringForward: false);
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
        private static void BuildStage(bool enteringForward, VillageSpawn villageSpawn = VillageSpawn.Edge)
        {
            if (stageRoot != null) Object.Destroy(stageRoot);
            stageRoot = new GameObject("StageRoot");

            // 보스를 잡지 않고 포탈로 스테이지를 떠나면 보스 몬스터는 stageRoot와 함께 사라지지만
            // OnDied가 발생하지 않아 체력바는 그대로 남아있었다. 스테이지를 새로 지을 때마다 일단
            // 끊어두고, 보스 스테이지면 SpawnBoss에서 다시 Bind해서 보여준다.
            bossHealthUI.Hide();
            stageClearUI.Cancel();
            // 던전 첫 맵에 앞으로 걸어 들어왔으면(마을/월드맵에서 출발) 스테이지 클리어 창에 보여줄 획득 기록을 새로 시작한다.
            if (enteringForward && (currentStage == 1 || currentStage == PlainsStage1 || currentStage == RuinsStage1 || currentStage == HillStage)) stageClearUI.BeginRun();

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

            if (currentStage == StairsStage)
            {
                BuildStairs(enteringForward);
                return;
            }

            if (currentStage == TempleStage)
            {
                BuildTemple();
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

            BuildMap(stageRoot.transform, MapWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                "Backgrounds/map", BackgroundGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            player.SetCombatLocked(false);
            float playerSpawnX = enteringForward ? xMin + 1.5f : xMax - 1.5f;
            PlacePlayerAtSpawn(playerSpawnX, yMin);
            FitCamera(xMin, xMax, yMin);

            SpawnForestPlatforms(stageRoot.transform, yMin, currentStage);

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
                    MonsterSpawner.PickSpawnXs(KnightsPerStage, xMin, xMax, playerSpawnX, minSpacing: 4.5f));
            }

            // 왼쪽 끝은 이전 스테이지(마을 포함)로 돌아가는 포탈, 오른쪽 끝은 마지막 스테이지가
            // 아닐 때만 다음 스테이지로 가는 포탈. 보스방은 들어오면 보스와 싸울 수밖에 없어서 포탈을 두지 않는다
            // (이기면 스테이지 클리어 창, 지면 마을로 돌아간다).
            if (currentStage != BossStage) SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true);
            if (currentStage < TotalStages)
            {
                SpawnPortal(stageRoot.transform, xMax - 0.5f, yMin, backward: false);
            }

            var stageManagerGO = new GameObject("StageManager");
            stageManagerGO.transform.SetParent(stageRoot.transform);
            stageManagerGO.AddComponent<StageManager>().Initialize(player);

            Debug.Log($"Aethoria: 맵 {currentStage}/{TotalStages} 진입");
        }

        // 어둠의 숲 발판: 원화(DrakForest_scattfolding) 속 이끼 낀 바위섬/구름다리를 조각내 전용
        // 스프라이트(Art/Props/Forest)로 만들고 실제로 밟을 수 있게 올려준다.
        // 평야/폐허와 달리 바닥이 끊김 없이 쭉 이어진 평지라, 플레이어가 아무 발판 바로 밑까지
        // 걸어가서 제자리에서 2단 점프로 올라탈 수 있어야 한다. 2단 점프 최고 높이는
        // jumpForce(11.3)/gravityScale(4)로 계산하면 약 3.25유닛이라 바닥 기준 2.7을 넘기지 않는다.
        // 또한 MonsterAI.jumpOverHeight(2)보다 높이 있어야 발판 위에 서 있는 동안 바로 아래를
        // 지나가는 기사와 부딪히지 않고 피할 수 있다 — 그래서 전부 바닥 기준 2.1~2.7 사이로 맞췄다
        // (기사 세 마리가 한데 뭉쳐 나와도 발판으로 피해 지나갈 길을 주기 위함, SpawnKnights 쪽
        // minSpacing도 같이 넓혔다). 가로 폭 20유닛 맵에 8개(보스 맵은 7개)를 촘촘히 깔아 거의 끊김
        // 없이 발판을 타고 넘어갈 수 있게 했다. 보스 맵은 보스가 서는 x=5 바닥 자리 위로는 큰 발판만
        // 높이 띄워 지나가고, 나머지는 양옆에 둔다.
        private static void SpawnForestPlatforms(Transform parent, int floorY, int stageIndex)
        {
            switch (stageIndex)
            {
                case 1:
                    SpawnPlatform(parent, "forest_platform_9", -7.5f, floorY + 2.1f, 58, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_7", -5.7f, floorY + 2.4f, 40, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_8", -3.9f, floorY + 2.6f, 29, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_5", -2.0f, floorY + 2.2f, 53, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_6", 0.1f, floorY + 2.7f, 47, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_4", 2.1f, floorY + 2.3f, 72, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_3", 4.3f, floorY + 2.5f, 50, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_2", 6.6f, floorY + 2.1f, 44, ForestPropFolder);
                    break;
                case 2:
                    SpawnPlatform(parent, "forest_platform_7", -7.6f, floorY + 2.2f, 40, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_9", -5.8f, floorY + 2.6f, 58, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_5", -4.0f, floorY + 2.1f, 53, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_8", -2.1f, floorY + 2.5f, 29, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_4", 0.0f, floorY + 2.3f, 72, ForestPropFolder);
                    SpawnPlatform(parent, "forest_bridge", 2.3f, floorY + 2.7f, 99, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_1", 4.9f, floorY + 2.2f, 79, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_6", 7.2f, floorY + 2.4f, 47, ForestPropFolder);
                    break;
                default:
                    SpawnPlatform(parent, "forest_platform_7", -7.6f, floorY + 2.2f, 40, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_9", -5.8f, floorY + 2.5f, 58, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_8", -3.9f, floorY + 2.1f, 29, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_5", -1.8f, floorY + 2.6f, 53, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_6", 0.3f, floorY + 2.3f, 47, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_3", 2.6f, floorY + 2.4f, 50, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_wide", 7.0f, floorY + 2.6f, 82, ForestPropFolder);
                    break;
            }
        }

        private const string ForestPropFolder = "Art/Props/Forest/";

        // 마을: 몬스터도 StageManager도 없는 안전 지역. 포탈까지 걸어가면 1스테이지로 들어간다.
        // 캐릭터가 떠 보인다는 피드백에 따라 바닥 기준점을 그림 더 아래쪽(가장 앞쪽 보도블록)으로 내림.
        private const float VillageGroundFraction = 0.87f;

        private static void BuildVillage(bool enteringForward, VillageSpawn spawn)
        {
            BuildMap(stageRoot.transform, VillageWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                "Backgrounds/village", VillageGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            player.SetCombatLocked(true);
            switch (spawn)
            {
                case VillageSpawn.Noa:
                    // 노아 오른쪽에 서서 노아를 바라보게 한다.
                    PlacePlayerAtSpawn(NoaX + NoaFrontOffset, yMin);
                    player.GetComponent<CharacterMovement2D>()?.Face(Vector2.left);
                    break;
                case VillageSpawn.Isis:
                    // 연습장에서 나오면 이시스 오른쪽에 서서 이시스를 바라본다.
                    PlacePlayerAtSpawn(IsisX + NoaFrontOffset, yMin);
                    player.GetComponent<CharacterMovement2D>()?.Face(Vector2.left);
                    break;
                default:
                    PlacePlayerAtSpawn(enteringForward ? xMin + 1.5f : xMax - 1.5f, yMin);
                    break;
            }
            FitCamera(xMin, xMax, yMin);
            SpawnNoaShop(stageRoot.transform, yMin);
            SpawnPracticeHall(stageRoot.transform, yMin);
            SpawnEllyForge(stageRoot.transform, yMin);

            // 마을 밖으로 나가는 포탈은 바로 1스테이지로 보내지 않고, 어느 지역으로 갈지 고르는
            // 월드맵 화면을 띄운다. 취소하면 포탈이 다시 발동하도록 풀어준다(ResetTrigger).
            var exitPortal = SpawnPortal(stageRoot.transform, xMax - 0.5f, yMin, backward: false);
            exitPortal.SetCustomTrigger(() => worldMapUI.Show(exitPortal));

            SaveGame();
            Debug.Log("Aethoria: 마을 진입");
        }

        // 튜토리얼 숲: 전용 배경(Tutorial_Map)의 평평한 숲길을 걷는 맵. 처음엔 몬스터가 없다. 진행은 ForestTutorial이 맡고,
        // 숲의 정령(튜토리얼 몬스터)은 이동 연습이 끝난 뒤에 한 마리만 나오며, 그걸 잡아야 출구 포탈이 열린다.
        // 배경(2172x724) 속 흙길 윗면이 위에서부터 약 605px 지점이라 그 줄을 바닥으로 삼는다.
        private const float TutorialGroundFraction = 605f / 724f;
        private const int TutorialWidth = 32;
        // 세로를 다 덮게 자동으로 맞추면 배율이 약 1.49라 나무/성이 거대해서 네크로시아가 너무 작아 보였다. 1.1로 고정하고
        // 모자란 폭은 좌우 반전한 복사본으로 채운다. 화면 맨 아래가 조금 비지만 그림 아래쪽이 카메라 배경색과 거의 같은 검보라라 티가 안 난다.
        private const float TutorialBackgroundScale = 1.1f;

        private static void BuildTutorialForest()
        {
            BuildMap(stageRoot.transform, TutorialWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                "Backgrounds/TutorialForest", TutorialGroundFraction, TutorialBackgroundScale);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            player.SetCombatLocked(false);
            PlacePlayerAtSpawn(xMin + 2f, yMin);
            FitCamera(xMin, xMax, yMin);

            var root = stageRoot.transform;
            var tutorial = stageRoot.AddComponent<ForestTutorial>();
            tutorial.Initialize(player, npcDialogueUI, tutorialUI, storyIntroUI, xMin, xMax,
                x => SpawnForestSpirit(root, yMin, x),
                () =>
                {
                    var exit = SpawnPortal(root, xMax - 0.5f, yMin, backward: false);
                    exit.SetCustomTrigger(LeaveTutorialForest);
                });

            Debug.Log("Aethoria: 튜토리얼 숲 진입");
        }

        // 튜토리얼 몬스터(Tutorial_Monster_Move, 6프레임 걷기). 첫 전투라 평야 슬라임보다 약하게:
        // 일반 공격 한 턴이면 거의 잡히고, 놓쳐도 아프지 않다.
        private const string ForestSpiritWalkPath = "Art/Monsters/ForestSpirit/Walk";

        private static Monster SpawnForestSpirit(Transform parent, int floorY, float x)
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "숲의 정령";
            data.level = 1;
            data.maxHp = 100f;
            data.attack = 1f;
            data.defense = 0f;
            data.expReward = 10;
            data.goldReward = 8;

            var go = new GameObject("ForestSpirit");
            go.transform.position = new Vector3(x, floorY, 0f);
            go.transform.SetParent(parent);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.55f, 0.3f, 0.75f), 24, 32);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.8f, 1.5f);
            collider.offset = new Vector2(0f, 0.75f);

            go.AddComponent<Rigidbody2D>();
            go.AddComponent<MonsterWalkSpriteAnimator>().Configure(ForestSpiritWalkPath);
            var monster = go.AddComponent<Monster>();
            monster.AssignData(data);
            // 일반 공격으로는 체력 1에서 버티고, 스킬로만 쓰러진다(스킬 사용법을 직접 해 보게).
            go.AddComponent<TutorialSkillFinish>();
            go.AddComponent<MonsterAI>();
            go.AddComponent<MonsterHealthBar>();
            return monster;
        }

        // 튜토리얼 숲의 출구 포탈. 마을 왼쪽 끝으로 들어가고, 마을에서 알아 둘 것들을 안내창으로 보여준다.
        private static void LeaveTutorialForest()
        {
            currentStage = VillageStage;
            BuildStage(enteringForward: true);
            tutorialUI.Show(TutorialUI.VillagePages);
        }

        // 마을 중간쯤에 노아의 상점과 노아를 놓는다. 상점은 배경보다 앞, 캐릭터보다는 뒤에 그려진다.
        // 광활한 평야: 넓은 들판(40유닛)에 슬라임이 흩어져 있고, 기둥/아치/떠 있는 발판을 밟고 올라갈 수 있다.
        // 배경(YardMap) 속 흙길이 그림 위에서 약 82% 높이라 그 줄을 바닥으로 삼는다.
        private const float PlainsGroundFraction = 0.82f;

        // plainsIndex: 1~3. 3번째 맵 끝에서만 필드 보스(슬라임)가 나온다(어둠의 숲이 BossStage에서만
        // 보스를 추가하는 것과 같은 방식). 세 맵 모두 같은 지형을 재사용하고, 슬라임은 1~2번째 맵에만
        // 들어올 때마다 무작위 위치(MonsterSpawner)에 나온다.
        private static void BuildPlains(int plainsIndex, bool enteringForward)
        {
            BuildMap(stageRoot.transform, PlainsWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                "Backgrounds/plains", PlainsGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            player.SetCombatLocked(false);
            float playerSpawnX = enteringForward ? xMin + 1.5f : xMax - 1.5f;
            PlacePlayerAtSpawn(playerSpawnX, yMin);
            FitCamera(xMin, xMax, yMin);

            SpawnPlainsPlatforms(stageRoot.transform, yMin);

            bool isFirstPlains = plainsIndex <= 1;
            bool isLastPlains = plainsIndex >= TotalPlainsStages;

            // 보스 맵(마지막 평야)에는 보스 슬라임만 나오고 일반 슬라임은 나오지 않는다.
            if (isLastPlains)
            {
                SpawnBossSlime(stageRoot.transform, yMin);
            }
            else
            {
                SpawnSlimes(stageRoot.transform, yMin, MonsterSpawner.PickSpawnXs(SlimesPerStage, xMin, xMax, playerSpawnX));
            }

            var backPortal = SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true);
            backPortal.SetCustomTrigger(isFirstPlains ? LeaveFieldToVillage : EnterPreviousFieldStage);

            if (!isLastPlains)
            {
                var forwardPortal = SpawnPortal(stageRoot.transform, xMax - 0.5f, yMin, backward: false);
                forwardPortal.SetCustomTrigger(EnterNextFieldStage);
            }

            var stageManagerGO = new GameObject("StageManager");
            stageManagerGO.transform.SetParent(stageRoot.transform);
            stageManagerGO.AddComponent<StageManager>().Initialize(player);

            Debug.Log($"Aethoria: 광활한 평야 {plainsIndex}/{TotalPlainsStages} 진입");
        }

        // 평야 지형: Yard_scattfolding에서 잘라낸 조각들을 왼쪽(출발)에서 오른쪽으로 이어지는 코스로 배치한다.
        // 한 번 점프가 약 1.6, 2단 점프가 약 3.2유닛이라, 인접한 발판끼리는 높이 차가 이 안에 들도록 했다.
        // 겹쳐 선 발판끼리는 위 발판 아랫면과 아래 발판 윗면 사이에 캐릭터 키(1.5) 이상 공간을 남긴다.
        // surfaceFromTopPx: 그림 맨 위에서 밟는 면(풀밭/돌길 윗줄)까지의 픽셀. -1이면 밟을 수 없는 장식.
        private static void SpawnPlainsPlatforms(Transform parent, int floorY)
        {
            // 1구역 (출발): 작은 기둥 두 개와 낮은 단, 작은 떠 있는 발판으로 몸풀기
            SpawnGroundProp(parent, "pillar_5", -18.6f, floorY, 12);
            SpawnGroundProp(parent, "pillar_6", -16.0f, floorY, 18);
            SpawnGroundProp(parent, "ground_3", -12.4f, floorY, 60);
            SpawnPlatform(parent, "platform_10", -14.2f, floorY + 2.5f, 30);
            SpawnPlatform(parent, "platform_8", -10.4f, floorY + 2.6f, 30);

            // 2구역: 높은 공중 돌길(ground_2), 그 아래 낮은 발판과 이중 아치(장식)
            SpawnPlatform(parent, "ground_2", -4.3f, floorY + 4.3f, 56);
            SpawnPlatform(parent, "platform_3", -7.5f, floorY + 1.4f, 31);
            SpawnGroundProp(parent, "arch_1", -3.2f, floorY, -1);

            // 3구역: 큰 기둥 → 덩굴 발판 → 윗면이 평평한 아치
            SpawnPlatform(parent, "platform_9", 1.8f, floorY + 2.9f, 27);
            SpawnGroundProp(parent, "pillar_1", 3.6f, floorY, 8);
            SpawnPlatform(parent, "platform_12", 6.2f, floorY + 4.0f, 35);
            SpawnGroundProp(parent, "arch_2", 9.0f, floorY, 25);

            // 4구역: 가장 긴 공중 다리(platform_7), 아래에는 부서진 기둥들
            SpawnGroundProp(parent, "pillar_2", 11.2f, floorY, 28);
            SpawnPlatform(parent, "platform_7", 15.4f, floorY + 4.6f, 49);
            SpawnGroundProp(parent, "pillar_3", 13.5f, floorY, -1);
            SpawnGroundProp(parent, "pillar_4", 17.8f, floorY, 20);
        }

        // 엔스룸 마을: 몬스터도 StageManager도 없는 안전 지역. 왼쪽 끝 포탈로 처음 마을(출구 쪽)로 돌아간다.
        // 배경(ensroom.png)은 원화(Ensroom Village, 2172x724) 아래쪽 돌바닥을 세로로 60px 늘여 덧붙인 것이다
        // (맵 폭에 맞춰 깔면 바닥 아래가 1유닛 남짓밖에 안 남아 화면 맨 아래가 비었다). 벤치 바로 앞 돌바닥
        // (원화 y 650)이 지금 그림 높이 784px 중 650px 지점이라 그 줄을 바닥으로 삼는다.
        private const float EnsroomGroundFraction = 650f / 784f;
        // 테두리(조각/덩굴)는 ensroom_portal_base 한 장으로 고정하고, 안쪽 거울만 Swirl 폴더의
        // 6프레임으로 애니메이션한다(통째로 갈아 끼우면 테두리까지 떨려 보였다).
        private const string EnsroomPortalBase = "Art/Props/EnsroomPortal/ensroom_portal_base";
        private const string EnsroomPortalSwirlFolder = "Art/Props/EnsroomPortal/Swirl";

        private static void BuildEnsroom()
        {
            BuildMap(stageRoot.transform, EnsroomWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                "Backgrounds/ensroom", EnsroomGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            player.SetCombatLocked(true);
            // 왼쪽 끝 귀환 포탈 트리거(반경 2)에 걸리지 않을 만큼 떨어진 곳에 세운다(연습장과 같은 이유).
            PlacePlayerAtSpawn(xMin + 4f, yMin);
            FitCamera(xMin, xMax, yMin);

            var backPortal = SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true, EnsroomPortalBase, EnsroomPortalSwirlFolder);
            backPortal.SetCustomTrigger(LeaveFieldToVillage);

            Debug.Log("Aethoria: 엔스룸 마을 진입");
        }

        // 절망의 언덕: 붉은 달 아래 용암이 흐르는 절벽 끝. 일반 몬스터 없이 오른쪽 끝에 최종보스 루나벨이 서 있고,
        // 왼쪽 끝 포탈로 처음 마을(출구 쪽)로 돌아간다. 보스방이라 오른쪽 포탈은 없다.
        // 배경(Hill of Despair, 2172x724) 맨 앞 갈라진 돌바닥 윗면이 위에서부터 약 628px 지점이라 그 줄을 바닥으로 삼는다.
        private const float HillGroundFraction = 628f / 724f;

        private static void BuildHill()
        {
            BuildMap(stageRoot.transform, HillWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                "Backgrounds/HillOfDespair", HillGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            player.SetCombatLocked(false);
            PlacePlayerAtSpawn(xMin + 4f, yMin);
            FitCamera(xMin, xMax, yMin);

            // 들어오면 곧바로 루나벨과의 결전이라 돌아가는 포탈은 두지 않는다(이기면 스테이지 클리어 창,
            // 지면 마을로 돌아간다). 그래서 루나벨전은 도주도 막혀 있다(RhythmBattleUI).
            SpawnLunabel(stageRoot.transform, yMin, xMax);

            var stageManagerGO = new GameObject("StageManager");
            stageManagerGO.transform.SetParent(stageRoot.transform);
            stageManagerGO.AddComponent<StageManager>().Initialize(player);

            Debug.Log("Aethoria: 절망의 언덕 진입");
        }

        // 천상으로 향하는 계단: 몬스터도 StageManager도 없는 안전 지역. 왼쪽 끝 포탈은 처음 마을(출구 쪽)로,
        // 오른쪽 끝 포탈은 천상의 신전으로 이어진다.
        // 배경(Stairs to the Temple of the Angel, 2172x724) 대리석 회랑 바닥 앞쪽 가장자리가 위에서부터 약 595px
        // 지점이라 그 줄을 바닥으로 삼는다.
        private const float StairsGroundFraction = 595f / 724f;
        // 맵 폭(40)에 맞춰 늘리면 배율이 약 1.84라 기둥/아치가 캐릭터에 비해 거인국처럼 커 보였다. 1.3으로 줄이고
        // 모자란 폭은 좌우 반전한 복사본으로 채운다. 이보다 더 줄이면 카메라(orthoSize 4) 아래쪽 바닥 밑이 비기 시작한다.
        private const float StairsBackgroundScale = 1.3f;

        private static void BuildStairs(bool enteringForward)
        {
            BuildMap(stageRoot.transform, StairsWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                "Backgrounds/HeavenStairs", StairsGroundFraction, StairsBackgroundScale);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            player.SetCombatLocked(true);
            // 양끝 포탈 트리거(반경 2)에 걸리지 않을 만큼 떨어진 곳에 세운다. 신전에서 되돌아왔으면 오른쪽 끝.
            PlacePlayerAtSpawn(enteringForward ? xMin + 4f : xMax - 4f, yMin);
            FitCamera(xMin, xMax, yMin);

            var backPortal = SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true);
            backPortal.SetCustomTrigger(LeaveFieldToVillage);

            var forwardPortal = SpawnPortal(stageRoot.transform, xMax - 0.5f, yMin, backward: false);
            forwardPortal.SetCustomTrigger(EnterNextFieldStage);

            Debug.Log("Aethoria: 천상으로 향하는 계단 진입");
        }

        // 천상의 신전: 계단 끝에서 들어오는 안전 지역. 왼쪽 끝 포탈로 계단(오른쪽 끝)으로 돌아가고,
        // 다리 위 성소 건물 입구로 이리아의 성소에 들어간다.
        // 배경(Temple of the Heavens, 1916x821) 맨 앞 대리석 다리 윗면 앞쪽 가장자리가 위에서부터 약 683px 지점이라
        // 그 줄을 바닥으로 삼는다.
        private const float TempleGroundFraction = 683f / 821f;
        // 배경에서 다리 난간 윗면(원화 y 643) 아래만 남긴 그림. 성소 건물 앞에 한 겹 더 그려서 성소가 다리
        // 너머 하늘에 떠 있고 아래쪽이 난간 뒤로 가려지게 한다. 순서는 배경(-10) < 성소(-7) < 덮개(-6) < 포탈(-5).
        private const string TempleFrontOverlay = "Backgrounds/TempleOfHeavens_Front";

        private static void BuildTemple()
        {
            BuildMap(stageRoot.transform, TempleWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                "Backgrounds/TempleOfHeavens", TempleGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;
            BuildMapBackground(stageRoot.transform, xMin, xMax, yMin, TempleFrontOverlay, TempleGroundFraction, sortingOrder: -6);

            player.SetCombatLocked(true);
            if (templeSpawnAtSanctuary)
            {
                // 성소에서 막 나왔으면 입구 바로 앞에 세운다(입구 자체는 포탈이 아니라 트리거라 거리 제약이 없다).
                templeSpawnAtSanctuary = false;
                PlacePlayerAtSpawn(SanctuaryX, yMin);
            }
            else
            {
                // 왼쪽 끝 귀환 포탈 트리거(반경 2)에 걸리지 않을 만큼 떨어진 곳에 세운다(연습장과 같은 이유).
                PlacePlayerAtSpawn(xMin + 4f, yMin);
            }
            FitCamera(xMin, xMax, yMin);

            var backPortal = SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true);
            backPortal.SetCustomTrigger(EnterPreviousFieldStage);

            SpawnSanctuaryBuilding(stageRoot.transform, yMin);

            Debug.Log("Aethoria: 천상의 신전 진입");
        }

        // 배경 한가운데 여신상(그림 폭의 약 52%, x≈0.7)을 가리지 않게 다리 오른쪽에 성소 건물을 놓는다.
        private const float SanctuaryX = 5.5f;
        // 다리 위에 바로 세우면 캐릭터와 같은 줄에 붙어 보여서, 다리 너머 하늘에 떠 있는 섬처럼 조금 뒤로 물렸다.
        // 멀리 있는 만큼 작게(4.5) 그리고, 발판 바닥면을 난간 윗면(바닥 위 약 0.67)보다 살짝 높게 띄운다.
        // 그 아래 얼음 조각들은 TempleFrontOverlay(난간/다리)에 가려진다.
        private const float SanctuaryWidthOnMap = 4.5f;
        private const float SanctuaryRaise = 0.8f;
        // 원화(1536x1024) 속에서 떠 있는 발판의 평평한 바닥면(계단이 시작되는 줄)이 위에서부터 약 85% 지점.
        // 그 아래로 늘어진 얼음 조각들은 다리 난간 뒤로 숨는다.
        private const float SanctuaryBaseFraction = 0.85f;

        private static void SpawnSanctuaryBuilding(Transform parent, int floorY)
        {
            var sprite = Resources.Load<Sprite>("Backgrounds/IriaSanctuary");
            if (sprite != null)
            {
                var go = new GameObject("IriaSanctuary", typeof(SpriteRenderer));
                go.transform.SetParent(parent);

                var renderer = go.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = -7;

                float scale = SanctuaryWidthOnMap / sprite.bounds.size.x;
                go.transform.localScale = new Vector3(scale, scale, 1f);

                var bounds = sprite.bounds;
                float baseAbovePivot = (bounds.max.y - bounds.size.y * SanctuaryBaseFraction) * scale;
                go.transform.position = new Vector3(SanctuaryX - bounds.center.x * scale, floorY + SanctuaryRaise - baseAbovePivot, 0f);
            }

            var entranceGO = new GameObject("SanctuaryEntrance");
            entranceGO.transform.SetParent(parent);
            entranceGO.transform.position = new Vector3(SanctuaryX, floorY, 0f);
            entranceGO.AddComponent<SanctuaryEntrance>().Initialize(player.transform, EnterSanctuary);
        }

        // 성소 안쪽, 제단 바로 앞에 선 이리아.
        private const float SanctuaryIriaX = 0f;

        private static void SpawnIria(Transform parent, int floorY, float x)
        {
            var iriaGO = new GameObject("Iria", typeof(SpriteRenderer));
            iriaGO.transform.SetParent(parent);
            iriaGO.transform.position = new Vector3(x, floorY, 0f);
            iriaGO.AddComponent<IriaNpc>().Initialize(player.transform, npcDialogueUI, blessingUI);
        }

        // 배경(Interior of Iria's Sanctuary, 1672x941) 속 대리석 바닥이 위에서부터 약 84% 지점이라 그 줄을 바닥으로 삼는다.
        private const float SanctuaryInteriorGroundFraction = 0.84f;

        private static void BuildSanctuary()
        {
            BuildMap(stageRoot.transform, SanctuaryWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                "Backgrounds/IriaSanctuaryInterior", SanctuaryInteriorGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            player.SetCombatLocked(true);
            // 왼쪽 끝 귀환 포탈 트리거(반경 2)에 걸리지 않을 만큼 떨어진 곳에 세운다(엔스룸/연습장과 같은 이유).
            PlacePlayerAtSpawn(xMin + 4f, yMin);
            FitCamera(xMin, xMax, yMin);

            var backPortal = SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true);
            backPortal.SetCustomTrigger(LeaveSanctuary);

            SpawnIria(stageRoot.transform, yMin, SanctuaryIriaX);

            Debug.Log("Aethoria: 이리아의 성소 진입");
        }

        // 잊혀진 폐허: 1번째 맵은 폐허 입구(Forgotten Ruins_Entrance), 2~3번째 맵은 평평한 돌길이 이어지는
        // 폐허 마당(Forgotten Ruins_yard). 마당 그림은 돌길 윗면이 약 81% 높이라 그 줄을 바닥으로 삼는다.
        // 입구 그림은 왼쪽 높은 돌단 → 계단 → 가운데 돌다리 → 계단 → 오른쪽 아래 돌길로 내려가는 구조라,
        // 가장 낮은 오른쪽 돌길(그림의 약 83.4% 높이)을 바닥으로 삼고 그 위로 길을 따라 경사 지형을 깐다.
        // 콜로서스가 들어올 때마다 무작위 위치에 나오고, 보스는 아직 없다.
        private const float RuinsEntranceGroundFraction = 785f / 941f;
        private const float RuinsYardGroundFraction = 0.81f;

        // 입구 지형: (월드 x, 바닥 위 높이). 배경(1672x941)이 맵 폭 40에 맞춰 깔려 1유닛 = 41.8px이므로
        // x = 그림 x / 41.8 - 20, 높이 = (785 - 그림 y) / 41.8. 계단은 걸어서 오르내릴 수 있게 완만한 경사로 이었다.
        private static readonly Vector2[] RuinsEntranceTerrain =
        {
            new Vector2(-20f, 4.43f),   // 왼쪽 높은 돌단(그림 y 600)
            new Vector2(-9.71f, 4.43f),
            new Vector2(-4.69f, 1.44f), // 계단을 내려와 가운데 돌다리(그림 y 725)
            new Vector2(2.73f, 1.44f),
            new Vector2(5.12f, 0f),     // 다시 계단을 내려와 오른쪽 돌길(바닥)
        };

        // 입구 맵에서는 높은 돌단 위에서도 화면이 잘리지 않게 카메라가 이만큼까지 따라 올라간다.
        private const float RuinsEntranceCameraRise = 3f;

        // x 위치의 지형 높이(바닥 기준). 지형이 없거나 지형 점 범위 밖(오른쪽 평지)이면 0.
        private static float TerrainHeightAt(Vector2[] terrain, float x)
        {
            if (terrain == null || terrain.Length == 0) return 0f;
            if (x <= terrain[0].x) return terrain[0].y;
            for (int i = 1; i < terrain.Length; i++)
            {
                if (x <= terrain[i].x)
                {
                    float t = Mathf.InverseLerp(terrain[i - 1].x, terrain[i].x, x);
                    return Mathf.Lerp(terrain[i - 1].y, terrain[i].y, t);
                }
            }
            return 0f;
        }

        // 지형 윗면을 따라 이어진 선 콜라이더. 맨 왼쪽은 왼쪽 벽에, 맨 오른쪽은 바닥에 닿아 있어 아래로 빠질 틈이 없다.
        private static void BuildTerrain(Transform parent, int floorY, Vector2[] terrain)
        {
            var go = new GameObject("Terrain", typeof(EdgeCollider2D));
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(0f, floorY, 0f);

            var edge = go.GetComponent<EdgeCollider2D>();
            edge.points = terrain;
            edge.edgeRadius = 0.05f;
        }

        private static void BuildRuins(int ruinsIndex, bool enteringForward)
        {
            bool isFirstRuins = ruinsIndex <= 1;
            bool isLastRuins = ruinsIndex >= TotalRuinsStages;

            BuildMap(stageRoot.transform, RuinsWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                isFirstRuins ? "Backgrounds/ruins_entrance" : "Backgrounds/ruins_yard",
                isFirstRuins ? RuinsEntranceGroundFraction : RuinsYardGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            var terrain = isFirstRuins ? RuinsEntranceTerrain : null;
            if (terrain != null) BuildTerrain(stageRoot.transform, yMin, terrain);

            player.SetCombatLocked(false);
            float playerSpawnX = enteringForward ? xMin + 1.5f : xMax - 1.5f;
            PlacePlayerAtSpawn(playerSpawnX, yMin + TerrainHeightAt(terrain, playerSpawnX));
            FitCamera(xMin, xMax, yMin, terrain != null ? RuinsEntranceCameraRise : 0f);

            SpawnRuinsPlatforms(stageRoot.transform, yMin, ruinsIndex);
            SpawnColossi(stageRoot.transform, x => yMin + TerrainHeightAt(terrain, x),
                MonsterSpawner.PickSpawnXs(ColossiPerStage, xMin, xMax, playerSpawnX, minSpacing: 3.5f));

            float backPortalX = xMin + 0.5f;
            var backPortal = SpawnPortal(stageRoot.transform, backPortalX, yMin + TerrainHeightAt(terrain, backPortalX), backward: true);
            backPortal.SetCustomTrigger(isFirstRuins ? LeaveFieldToVillage : EnterPreviousFieldStage);

            if (!isLastRuins)
            {
                var forwardPortal = SpawnPortal(stageRoot.transform, xMax - 0.5f, yMin, backward: false);
                forwardPortal.SetCustomTrigger(EnterNextFieldStage);
            }

            var stageManagerGO = new GameObject("StageManager");
            stageManagerGO.transform.SetParent(stageRoot.transform);
            stageManagerGO.AddComponent<StageManager>().Initialize(player);

            Debug.Log($"Aethoria: 잊혀진 폐허 {ruinsIndex}/{TotalRuinsStages} 진입");
        }

        // 폐허 발판: Forgotten Ruins_Platform에서 잘라낸 떠 있는 돌섬들. 맵마다 다른 코스로 배치한다.
        // 평야와 같은 규칙 — 인접 발판 높이 차는 2단 점프(약 3.2) 안, 바닥에서 바로 오르는 첫 발판은 3 이하.
        // surfaceFromTopPx는 그림 맨 위에서 돌판 윗면(이끼 낀 밟는 면)까지의 픽셀.
        private static void SpawnRuinsPlatforms(Transform parent, int floorY, int ruinsIndex)
        {
            switch (ruinsIndex)
            {
                case 1:
                    // 입구: 길은 높은 돌단에서 계단 두 번을 거쳐 내려간다(RuinsEntranceTerrain). 발판은 그 길 위로
                    // 돌단 위 돌섬 → 계단 위 돌섬들 → 긴 공중 다리로 이어지는 윗길을 만들고, 오른쪽 평지로 내려온다.
                    SpawnPlatform(parent, "ruin_rock_3", -15.5f, floorY + 6.4f, 41, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_6", -6.8f, floorY + 5.4f, 34, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_5", -2.5f, floorY + 4.2f, 41, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_bridge_long", 5.6f, floorY + 5.6f, 97, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_7", 13.2f, floorY + 3.0f, 20, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_spire", 16.3f, floorY + 1.8f, 74, RuinsPropFolder);
                    break;
                case 2:
                    // 마당 1: 작은 돌섬 계단으로 높은 다리까지 올라갔다가, 낮은 기둥 섬을 거쳐 다시 오르내린다.
                    SpawnPlatform(parent, "ruin_rock_4", -15.5f, floorY + 2.4f, 34, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_slab_4", -12.2f, floorY + 3.8f, 49, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_bridge", -6.6f, floorY + 5.0f, 74, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_1", -1.2f, floorY + 3.6f, 55, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_pillar_2", 3.4f, floorY + 1.4f, 137, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_slab_3", 8.3f, floorY + 3.0f, 32, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_8", 12.5f, floorY + 4.4f, 46, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_mound_3", 15.6f, floorY + 2.6f, 53, RuinsPropFolder);
                    break;
                default:
                    // 마당 2: 탑이 솟은 성문 섬과 밧줄 다리 섬이 번갈아 나오는 높낮이 큰 코스.
                    SpawnPlatform(parent, "ruin_pillar_1", -15.2f, floorY + 1.3f, 126, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_mound_1", -10.6f, floorY + 2.8f, 86, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rock_2", -6.4f, floorY + 4.3f, 51, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_gate_high", -1.6f, floorY + 2.0f, 178, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_rope", 4.2f, floorY + 3.4f, 95, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_mound_2", 8.6f, floorY + 4.8f, 61, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_slab_1", 12.6f, floorY + 3.2f, 56, RuinsPropFolder);
                    SpawnPlatform(parent, "ruin_pillar_4", 16.4f, floorY + 1.2f, 188, RuinsPropFolder);
                    break;
            }
        }

        // 바닥에 세우는 조각(기둥/아치/낮은 단). 아랫단 돌무더기가 흙길에 살짝 묻히도록 조금 내려 박는다.
        private const float GroundPropSink = 0.15f;

        private static void SpawnGroundProp(Transform parent, string spriteName, float x, int floorY, int surfaceFromTopPx)
        {
            var go = CreatePropSprite(parent, spriteName, new Vector3(x, floorY - GroundPropSink, 0f), out var sprite);
            if (go == null || surfaceFromTopPx < 0) return;

            AddOneWaySurface(go, sprite, (sprite.rect.height - surfaceFromTopPx) / sprite.pixelsPerUnit);
        }

        // 공중에 떠 있는 발판. surfaceY = 밟는 면의 월드 높이. folder = 조각 그림이 있는 Resources 폴더.
        private static void SpawnPlatform(Transform parent, string spriteName, float x, float surfaceY, int surfaceFromTopPx,
            string folder = PlainsPropFolder)
        {
            var sprite = Resources.Load<Sprite>(folder + spriteName);
            if (sprite == null) return;

            // 스프라이트 피벗은 하단 중앙이라, 밟는 면이 피벗보다 얼마나 위에 있는지 구해서 위치를 맞춘다.
            float surfaceAbovePivot = (sprite.rect.height - surfaceFromTopPx) / sprite.pixelsPerUnit;
            var go = CreatePropSprite(parent, spriteName, new Vector3(x, surfaceY - surfaceAbovePivot, 0f), out _, folder);
            AddOneWaySurface(go, sprite, surfaceAbovePivot);
        }

        private const string PlainsPropFolder = "Art/Props/Plains/";
        private const string RuinsPropFolder = "Art/Props/Ruins/";

        private static GameObject CreatePropSprite(Transform parent, string spriteName, Vector3 position, out Sprite sprite,
            string folder = PlainsPropFolder)
        {
            sprite = Resources.Load<Sprite>(folder + spriteName);
            if (sprite == null) return null;

            var go = new GameObject("Prop_" + spriteName, typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.position = position;

            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -5; // 배경보다 앞, 캐릭터/몬스터보다 뒤
            return go;
        }

        // 아래에서는 뚫고 올라갈 수 있고 위에서는 밟고 설 수 있는 단방향 윗면.
        private static void AddOneWaySurface(GameObject go, Sprite sprite, float surfaceAbovePivot)
        {
            const float thickness = 0.2f;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(sprite.bounds.size.x * 0.85f, thickness); // 가장자리 덩굴까지는 밟히지 않게 조금 좁게
            collider.offset = new Vector2(0f, surfaceAbovePivot - thickness / 2f);
            collider.usedByEffector = true;

            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
        }

        // 마을 한가운데 여신상 분수 바로 앞 광장에 둔다(상점 뒤로 여신상이 솟아 보이는 자리).
        // 배경은 맵 폭 32유닛에 맞춰 깔리므로 월드 x = (그림 x / 2172 - 0.5) * 32.
        private const float NoaShopX = 0.6f;
        private const float NoaShopWidth = 6f;
        private const float NoaX = NoaShopX + 1.8f;
        private const float NoaFrontOffset = 1.2f; // 보스 처치 후 귀환할 때 플레이어가 서는 자리(노아 기준 오른쪽)

        private static void SpawnNoaShop(Transform parent, int floorY)
        {
            var shopSprite = Resources.Load<Sprite>("Backgrounds/NoaShop");
            if (shopSprite != null)
            {
                var shopGO = new GameObject("NoaShop", typeof(SpriteRenderer));
                shopGO.transform.SetParent(parent);
                shopGO.transform.position = new Vector3(NoaShopX, floorY, 0f);

                var shopRenderer = shopGO.GetComponent<SpriteRenderer>();
                shopRenderer.sprite = shopSprite;
                shopRenderer.sortingOrder = -5;

                float scale = NoaShopWidth / shopSprite.bounds.size.x;
                shopGO.transform.localScale = new Vector3(scale, scale, 1f);
            }

            var noaGO = new GameObject("Noa", typeof(SpriteRenderer));
            noaGO.transform.SetParent(parent);
            noaGO.transform.position = new Vector3(NoaX, floorY, 0f);
            var noa = noaGO.AddComponent<NoaNpc>();
            noa.Initialize(player.transform, npcDialogueUI, questManager, dailyQuestManager, saveScreen, inventoryUI, shopUI);
            // 새 게임에서 처음 말을 걸면 상점 대화 전에 스토리 대사가 먼저 나온다. 다 보면 기록하고 저장한다.
            if (noaFirstMeetingPending)
            {
                noa.SetFirstMeeting(() =>
                {
                    noaFirstMeetingPending = false;
                    SaveGame();
                });
            }
        }

        // 마을 왼쪽 광장(여신상 분수와 왼쪽 노점 사이)에 이시스의 연습장 건물과 이시스를 놓는다.
        // 건물 그림(IsisPractice, 1536x1024)의 돌 바닥 아랫단은 그림 위에서 약 905px 지점이라 그 아래는 땅에 묻는다.
        private const float PracticeHallX = -9.5f;
        private const float PracticeHallWidth = 7f;
        private const float PracticeHallBaseFraction = 905f / 1024f;
        private const float PracticeHallLift = 0.4f; // 바닥선 기준으로 놓으면 건물이 땅에 살짝 묻혀 보여서 조금 띄운다
        private const float IsisX = PracticeHallX + 2.3f;
        private const float IsisSink = 0.3f; // 건물 앞 바닥에 발이 붙어 보이도록 조금 내린다

        private static void SpawnPracticeHall(Transform parent, int floorY)
        {
            var hallSprite = Resources.Load<Sprite>("Backgrounds/IsisPractice");
            if (hallSprite != null)
            {
                var hallGO = new GameObject("PracticeHall", typeof(SpriteRenderer));
                hallGO.transform.SetParent(parent);

                var hallRenderer = hallGO.GetComponent<SpriteRenderer>();
                hallRenderer.sprite = hallSprite;
                hallRenderer.sortingOrder = -5;

                float scale = PracticeHallWidth / hallSprite.bounds.size.x;
                hallGO.transform.localScale = new Vector3(scale, scale, 1f);

                // 임포트 피벗이 어디든 그림 속 바닥 아랫단이 floorY에, 그림 가로 중앙이 PracticeHallX에 오도록 맞춘다.
                var bounds = hallSprite.bounds;
                float baseAbovePivot = (bounds.max.y - bounds.size.y * PracticeHallBaseFraction) * scale;
                hallGO.transform.position = new Vector3(PracticeHallX - bounds.center.x * scale, floorY - baseAbovePivot + PracticeHallLift, 0f);
            }

            var isisGO = new GameObject("Isis", typeof(SpriteRenderer));
            isisGO.transform.SetParent(parent);
            isisGO.transform.position = new Vector3(IsisX, floorY - IsisSink, 0f);
            var isis = isisGO.AddComponent<IsisNpc>();
            isis.Initialize(player.transform, npcDialogueUI, inventoryUI);
            // 새 게임에서 처음 말을 걸면 연습장 입장 질문 전에 스토리 대사가 먼저 나온다. 다 보면 기록하고 저장한다.
            if (isisFirstMeetingPending)
            {
                isis.SetFirstMeeting(() =>
                {
                    isisFirstMeetingPending = false;
                    SaveGame();
                });
            }
        }

        // 마을 오른쪽 광장(노아의 상점과 마을 출구 포탈 사이)에 대장장이 엘리의 대장간과 엘리를 놓는다.
        // 건물 그림(EllyForge, 검은 배경을 지우고 잘라낸 것)의 돌계단 아랫단이 그림 위에서 약 848px(전체 854px) 지점.
        // 출구 포탈 그림(x 약 13.2~16)과 겹치지 않게 오른쪽 끝을 12.3쯤에 맞췄다.
        private const float EllyForgeX = 8.8f;
        private const float EllyForgeWidth = 7f;
        private const float EllyForgeBaseFraction = 848f / 854f;
        private const float EllyX = EllyForgeX - 2f; // 숫돌이 있는 왼쪽 처마 앞
        private const float EllySink = 0.15f;

        private static void SpawnEllyForge(Transform parent, int floorY)
        {
            var forgeSprite = Resources.Load<Sprite>("Backgrounds/EllyForge");
            if (forgeSprite != null)
            {
                var forgeGO = new GameObject("EllyForge", typeof(SpriteRenderer));
                forgeGO.transform.SetParent(parent);

                var forgeRenderer = forgeGO.GetComponent<SpriteRenderer>();
                forgeRenderer.sprite = forgeSprite;
                forgeRenderer.sortingOrder = -5;

                float scale = EllyForgeWidth / forgeSprite.bounds.size.x;
                forgeGO.transform.localScale = new Vector3(scale, scale, 1f);

                // 연습장과 같은 방식: 임포트 피벗이 어디든 계단 아랫단이 floorY에, 그림 가로 중앙이 EllyForgeX에 오게 한다.
                var bounds = forgeSprite.bounds;
                float baseAbovePivot = (bounds.max.y - bounds.size.y * EllyForgeBaseFraction) * scale;
                forgeGO.transform.position = new Vector3(EllyForgeX - bounds.center.x * scale, floorY - baseAbovePivot, 0f);
            }

            var ellyGO = new GameObject("Elly", typeof(SpriteRenderer));
            ellyGO.transform.SetParent(parent);
            ellyGO.transform.position = new Vector3(EllyX, floorY - EllySink, 0f);
            ellyGO.AddComponent<EllyNpc>().Initialize(player.transform, npcDialogueUI, inventoryUI, enhanceUI);
        }

        // 연습장: 왼쪽 끝 포탈로 마을(이시스 앞)로 돌아간다. 허수아비는 절대 쓰러지지 않고, 머무는 동안 마나/체력이 계속 가득 찬다.
        // 배경(Practice_room) 속 반들반들한 바닥 한가운데가 그림 위에서 약 74% 높이라 그 줄을 바닥으로 삼는다.
        private const float PracticeGroundFraction = 0.74f;
        private static readonly float[] DummyXs = { -3f, 1f, 5f, 9f };

        private static void BuildPracticeRoom()
        {
            BuildMap(stageRoot.transform, PracticeWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                "Backgrounds/practice_room", PracticeGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            player.SetCombatLocked(false);
            // 왼쪽 끝 귀환 포탈(반경 2)의 트리거에 닿지 않을 만큼 떨어진 곳에 세운다. xMin + 2.5처럼 반경 경계 바로 밖이면
            // 스폰 시 "이미 안에 있음" 판정(중심 거리 기준)은 피하면서도 플레이어 콜라이더는 원에 걸쳐서, 들어오자마자 마을로 튕겨 나갔다.
            PlacePlayerAtSpawn(xMin + 4f, yMin);
            FitCamera(xMin, xMax, yMin);

            SpawnTrainingDummies(stageRoot.transform, yMin);

            var backPortal = SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true);
            backPortal.SetCustomTrigger(LeavePracticeRoom);

            stageRoot.AddComponent<PracticeRoomRules>().Initialize(player);

            Debug.Log("Aethoria: 연습장 진입");
        }

        private static void SpawnTrainingDummies(Transform parent, int floorY)
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "허수아비";
            data.level = 1;
            data.maxHp = 1000000f; // 맞을 때마다 다시 가득 차므로 사실상 무한
            data.attack = 0f; // 놓치면 최소 기본 데미지(5)만 들어오고, 연습장에서는 체력이 바로 다시 찬다
            data.defense = 0f;
            data.expReward = 0;
            data.goldReward = 0;

            foreach (var x in DummyXs)
            {
                var go = new GameObject("TrainingDummy", typeof(SpriteRenderer));
                go.transform.SetParent(parent);
                go.transform.position = new Vector3(x, floorY, 0f);

                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(1.2f, 2.2f);
                collider.offset = new Vector2(0f, 1.1f);

                // 중력/넉백으로 밀려나지 않게 고정한다(사슬로 끌어오는 것처럼 위치를 직접 옮기는 스킬은 그대로 먹힌다).
                var body = go.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;

                go.AddComponent<Monster>().AssignData(data);
                go.AddComponent<TrainingDummy>();
            }
        }

        private static void PlacePlayerAtSpawn(float spawnX, float floorY)
        {
            player.transform.position = new Vector3(spawnX, floorY, 0f);
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private static void BuildMap(Transform parent, int width, int height, out int xMin, out int xMax, out int yMin, out int yMax,
            string backgroundResourcePath, float groundFraction, float backgroundScale = 0f)
        {
            xMin = -width / 2;
            xMax = xMin + width - 1;
            yMin = -height / 2;
            yMax = yMin + height - 1;

            BuildMapBackground(parent, xMin, xMax, yMin, backgroundResourcePath, groundFraction, backgroundScale);
            BuildBoundaryWalls(parent, xMin, xMax, yMin, yMax);
        }

        // map.png 원화 속에서 실제로 밟고 서 있는 돌바닥은 이미지 맨 아래가 아니라 위에서부터
        // 약 80% 지점에 있고, 그 아래는 반사되는 물웅덩이(파란 배경)라 예전 방식(이미지를 맵 높이에
        // 맞춰 그냥 늘리기)으로는 캐릭터 발밑에 그 물웅덩이가 걸려 붕 떠 보였다.
        private const float BackgroundGroundFraction = 0.8f;

        // 카메라(orthoSize 5)가 바닥 위로 넉넉히 8~9유닛까지 올려다봐도 배경이 다 채워지도록 요구하는
        // 최소 높이. village.png처럼 가로로 아주 긴(파노라마) 배경은 맵 폭에 맞춰 늘리면 세로가
        // 모자라 화면 위쪽에 빈 공간이 생기므로, 폭 기준 배율과 이 높이 기준 배율 중 더 큰 쪽을 쓴다.
        private const float MinBackgroundHeightAboveGround = 9f;

        // 배경 원화를 맵 폭(xMin~xMax+1)에 맞춰 가로세로 비율 그대로 깔되, 세로 커버리지가 모자라면
        // 대신 세로 기준으로 확대한다. groundFraction 지점이 정확히 바닥 높이(yMin)에 오도록 맞춘다.
        // fixedScale > 0이면 그 배율을 그대로 쓴다(그림 속 건물이 캐릭터에 비해 너무 커 보일 때). 그러면 맵 폭을
        // 다 못 덮을 수 있어서, 좌우 반전한 복사본을 양옆에 이어 붙여 이음매 없이 채운다.
        private static void BuildMapBackground(Transform parent, int xMin, int xMax, int yMin,
            string backgroundResourcePath, float groundFraction, float fixedScale = 0f, int sortingOrder = -10)
        {
            var sprite = Resources.Load<Sprite>(backgroundResourcePath);
            if (sprite == null) return;

            float width = xMax - xMin + 1;
            float centerX = (xMin + xMax + 1) / 2f;

            Vector2 nativeSize = sprite.bounds.size;
            float scale = fixedScale;
            if (scale <= 0f)
            {
                float scaleByWidth = width / nativeSize.x;
                float scaleByHeight = MinBackgroundHeightAboveGround / (nativeSize.y * groundFraction);
                scale = Mathf.Max(scaleByWidth, scaleByHeight);
            }

            float scaledWidth = nativeSize.x * scale;
            float scaledHeight = nativeSize.y * scale;
            float centerY = yMin + scaledHeight * (groundFraction - 0.5f);

            // 가운데 한 장 + 맵 끝까지 덮는 데 필요한 만큼 양옆으로(홀수 번째는 좌우 반전).
            int sideTiles = Mathf.Max(0, Mathf.CeilToInt((width / scaledWidth - 1f) / 2f));
            for (int i = -sideTiles; i <= sideTiles; i++)
            {
                var go = new GameObject("MapBackground", typeof(SpriteRenderer));
                go.transform.SetParent(parent);

                var renderer = go.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = sortingOrder;
                renderer.flipX = (i & 1) != 0;

                go.transform.localScale = new Vector3(scale, scale, 1f);
                go.transform.position = new Vector3(centerX + i * scaledWidth, centerY, 0f);
            }
        }

        private static void BuildBoundaryWalls(Transform parent, int xMin, int xMax, int yMin, int yMax)
        {
            var bounds = new GameObject("MapBounds");
            bounds.transform.SetParent(parent);

            float worldMinX = xMin;
            float worldMaxX = xMax + 1;
            float worldMinY = yMin;
            float worldMaxY = yMax + 1;
            float width = worldMaxX - worldMinX;
            float height = worldMaxY - worldMinY;
            float centerX = (worldMinX + worldMaxX) / 2f;
            float centerY = (worldMinY + worldMaxY) / 2f;

            CreateWall(bounds.transform, "Wall_Bottom", new Vector2(centerX, worldMinY - WallThickness / 2f), new Vector2(width + WallThickness * 2f, WallThickness));
            CreateWall(bounds.transform, "Wall_Top", new Vector2(centerX, worldMaxY + WallThickness / 2f), new Vector2(width + WallThickness * 2f, WallThickness));
            CreateWall(bounds.transform, "Wall_Left", new Vector2(worldMinX - WallThickness / 2f, centerY), new Vector2(WallThickness, height));
            CreateWall(bounds.transform, "Wall_Right", new Vector2(worldMaxX + WallThickness / 2f, centerY), new Vector2(WallThickness, height));
        }

        private static void CreateWall(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var wall = new GameObject(name, typeof(BoxCollider2D));
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;
            wall.GetComponent<BoxCollider2D>().size = size;
        }

        // maxRise > 0이면 플레이어가 바닥보다 높은 곳(언덕/계단 위)에 올라갔을 때 카메라도 그만큼까지 따라 올라간다.
        private static void FitCamera(int xMin, int xMax, int floorY, float maxRise = 0f)
        {
            var mainCamera = Camera.main;
            if (mainCamera == null) return;

            // 캐릭터/몬스터/이펙트 등 모든 스프라이트가 각자 다른 PPU로 그려지는데, 그걸 하나하나
            // 손보는 대신 카메라를 줌인해서 화면에 보이는 모든 것을 한 번에 비례대로 키운다.
            const float orthoSize = 4f;
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = orthoSize;

            // 배경 스프라이트가 화면을 다 못 덮는 가장자리(특히 바닥 아래쪽)에서 Unity 기본 파란 배경이
            // 그대로 비쳐 보이는 것을 막기 위해, 배경 그림과 어울리는 어두운 색으로 클리어 컬러를 맞춘다.
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color(0.03f, 0.02f, 0.05f);

            float cameraY = floorY + orthoSize * 0.6f;
            var follow = mainCamera.GetComponent<CameraFollow>();
            if (follow == null) follow = mainCamera.gameObject.AddComponent<CameraFollow>();
            follow.Follow(player.transform, xMin, xMax, cameraY, floorY, maxRise);
        }

        private static void BuildHud(Character player)
        {
            var canvasGO = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            hudRoot = canvasGO;
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            canvasGO.AddComponent<PlayerHUD>().Initialize(player);
            // 쓰러지면 게임오버 화면 대신 잠깐의 연출 후 마을로 돌아간다.
            canvasGO.AddComponent<DeathReturnUI>().Initialize(player);

            stageClearUI = canvasGO.AddComponent<StageClearUI>();
            stageClearUI.Initialize(player);

            bossHealthUI = canvasGO.AddComponent<BossHealthUI>();
            bossHealthUI.Initialize();

            npcDialogueUI = canvasGO.AddComponent<NpcDialogueUI>();
            npcDialogueUI.Initialize();

            worldMapUI = canvasGO.AddComponent<WorldMapUI>();
            worldMapUI.Initialize(player.GetComponent<CharacterMovement2D>());

            inventoryUI = canvasGO.AddComponent<InventoryUI>();
            inventoryUI.Initialize(player, player.GetComponent<CharacterMovement2D>());

            statusUI = canvasGO.AddComponent<StatusUI>();
            statusUI.Initialize(player, player.GetComponent<CharacterMovement2D>());

            shopUI = canvasGO.AddComponent<ShopUI>();
            shopUI.Initialize(player, player.GetComponent<CharacterMovement2D>());

            enhanceUI = canvasGO.AddComponent<EnhanceUI>();
            enhanceUI.Initialize(player, player.GetComponent<CharacterMovement2D>());

            blessingUI = canvasGO.AddComponent<BlessingUI>();
            blessingUI.Initialize(player, player.GetComponent<CharacterMovement2D>());

            // 몬스터가 접촉하면 실시간 전투 대신 이 화면으로 전환된다(MonsterAI/BossAI/BossSlimeAI가
            // Awake에서 FindFirstObjectByType으로 알아서 찾아 쓰므로 별도로 넘겨줄 필요 없다).
            canvasGO.AddComponent<RhythmBattleUI>().Initialize();

            questManager = canvasGO.AddComponent<QuestManager>();
            questManager.Initialize(player, QuestDatabase.CreateMainLine());
            canvasGO.AddComponent<QuestTrackerUI>().Initialize(questManager);

            dailyQuestManager = canvasGO.AddComponent<DailyQuestManager>();
            dailyQuestManager.Initialize(player, DailyQuestDatabase.CreatePool());
            canvasGO.AddComponent<DailyQuestTrackerUI>().Initialize(dailyQuestManager);

            saveToast = canvasGO.AddComponent<SaveToastUI>();
            saveToast.Initialize();

            canvasGO.AddComponent<ItemPickupToastUI>().Initialize(player.GetComponent<Inventory>());

            saveScreen = canvasGO.AddComponent<SaveScreenUI>();
            saveScreen.Initialize(player.GetComponent<CharacterMovement2D>());
            canvasGO.AddComponent<AutoSaveOnQuit>().Initialize(SaveGame);

            // 필드에서 Esc → 타이틀로 돌아가기 확인 창.
            canvasGO.AddComponent<TitleReturnUI>().Initialize(player, player.GetComponent<CharacterMovement2D>());

            // 다른 HUD 요소 위에 그려지도록 가장 마지막에 추가한다(같은 캔버스에서 나중 자식이 위에 그려짐).
            tutorialUI = canvasGO.AddComponent<TutorialUI>();
            tutorialUI.Initialize(player.GetComponent<CharacterMovement2D>());

            // 도입 스토리는 튜토리얼 안내보다도 위에 그린다.
            storyIntroUI = canvasGO.AddComponent<StoryIntroUI>();
            storyIntroUI.Initialize();
        }

        private static Character SpawnPlayer()
        {
            var data = ScriptableObject.CreateInstance<CharacterData>();
            data.characterName = "네크로시아";
            data.minLevel = 1;
            data.maxLevel = 50;
            // 데미지 계산식(DamageCalculation.md) 규모: 1레벨 공격력 15 / 방어력 1. 레벨당 +0.5씩 오른다.
            data.baseStats = new StatBlock { attack = 15, magic = 18, hp = 150, agility = 30, defense = 1, mana = 120 };
            data.growthPerLevel = new StatBlock { attack = 0.5f, magic = 0.5f, hp = 5, agility = 0, defense = 0.5f, mana = 2 };

            var go = new GameObject("PlayerCharacter");
            go.AddComponent<Rigidbody2D>();

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.6f, 0.5f);
            collider.offset = new Vector2(0f, 0.25f);

            go.AddComponent<CharacterMovement2D>();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.AddComponent<SpriteRenderer>();
            visual.AddComponent<CharacterPlaceholderVisual>();

            go.AddComponent<WalkBobVisual>();
            go.AddComponent<WalkSpriteAnimator>();
            go.AddComponent<JumpSpriteAnimator>();
            var character = go.AddComponent<Character>();
            character.AssignData(data);
            go.AddComponent<HitReactSpriteAnimator>();
            go.AddComponent<Inventory>();
            // 전투는 몬스터와 닿으면 시작되는 리듬 전투(RhythmBattleUI)로만 진행하므로,
            // 필드에서 쓰던 기본 공격/스킬(CharacterAttack2D, CharacterSkill*)은 붙이지 않는다.
            // 대시만 몹을 피해 지나가는 회피기로 남긴다(피해 없음).
            go.AddComponent<CharacterSkillDash>();

            return character;
        }

        // 일반 몬스터 수. 위치는 맵에 들어올 때마다 MonsterSpawner가 무작위로 고른다.
        // 어둠의 숲(1~2스테이지)에는 기사만, 광활한 평야(1~2번째 맵)에는 슬라임만 나온다. 보스 맵에는 일반 몬스터가 없다.
        private const int KnightsPerStage = 3;
        private const int SlimesPerStage = 4;
        private const int ColossiPerStage = 3;

        private static void SpawnSlimes(Transform parent, int floorY, IEnumerable<float> xOffsets)
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "슬라임";
            data.level = 1;
            // 플레이어(공격력 15) 노드 한 타 약 15, 일반 채보(약 9노드) 전부 Perfect면 약 160 — 한두 턴에 잡힌다.
            data.maxHp = 150f;
            data.attack = 3f; // 놓친 노드마다 (3-1) x 0.7 + 5 = 약 6
            data.defense = 1f;
            data.expReward = 10;
            data.goldReward = 8;

            foreach (var x in xOffsets)
            {
                var go = new GameObject("Slime");
                go.transform.position = new Vector3(x, floorY, 0f);
                go.transform.SetParent(parent);
                go.AddComponent<SpriteRenderer>();
                go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.3f, 0.55f, 0.95f), 24, 16);

                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.9f, 0.6f);
                collider.offset = new Vector2(0f, 0.3f);

                go.AddComponent<Rigidbody2D>();
                go.AddComponent<MonsterWalkSpriteAnimator>().Configure("Art/Monsters/Slime/Walk");
                go.AddComponent<Monster>().AssignData(data);
                go.AddComponent<MonsterAI>();
                go.AddComponent<MonsterHealthBar>();
            }
        }

        // 평야 오른쪽 끝의 필드 보스. 평야에 들어올 때마다 다시 나타난다.
        private static void SpawnBossSlime(Transform parent, int floorY)
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "보스 슬라임";
            data.level = 6;
            // 보스 채보(약 17노드) 한 턴 전부 Perfect면 약 265 — 서너 턴은 버틴다.
            data.maxHp = 800f;
            data.attack = 10f; // 놓친 노드마다 (10-1) x 0.7 + 5 = 약 11
            data.defense = 4f;
            data.expReward = 300;
            data.goldReward = 200;

            var go = new GameObject("Boss_Slime");
            go.transform.position = new Vector3(16f, floorY, 0f);
            go.transform.SetParent(parent);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.2f, 0.45f, 0.95f), 48, 32);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(2.6f, 1.8f);
            collider.offset = new Vector2(0f, 0.9f);

            go.AddComponent<Rigidbody2D>();
            go.AddComponent<MonsterWalkSpriteAnimator>().Configure("Art/Monsters/BossSlime/Walk");
            go.AddComponent<MonsterAttackSpriteAnimator>().Configure("Art/Monsters/BossSlime/Attack", 1.4f);
            var monster = go.AddComponent<Monster>();
            monster.AssignData(data);
            go.AddComponent<BossSlimeAI>();
            go.AddComponent<MonsterHealthBar>().Configure(newWidth: 2.4f, newVerticalOffset: 0.15f);

            bossHealthUI.Bind(monster, data.monsterName);
            monster.OnDied += _ => stageClearUI.Show(() => RetryDungeon(PlainsStage1));
        }

        // 잊혀진 폐허의 기본 몬스터. 이끼 낀 돌 거인이라 기사보다 크고(약 2.2유닛) 단단하다.
        // groundYAt: x 위치의 지면 높이(입구처럼 경사 지형이 있는 맵에서 땅 속에 박히지 않게).
        private static void SpawnColossi(Transform parent, System.Func<float, float> groundYAt, IEnumerable<float> xOffsets)
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "콜로서스";
            data.level = 4;
            // 플레이어 노드 한 타 약 12(방어력 5), 일반 채보 전부 Perfect면 약 130 — 두세 턴은 버틴다.
            data.maxHp = 300f;
            data.attack = 8f; // 놓친 노드마다 (8-1) x 0.7 + 5 = 약 10
            data.defense = 5f;
            data.expReward = 35;
            data.goldReward = 26;

            foreach (var x in xOffsets)
            {
                var go = new GameObject("Colossus");
                go.transform.position = new Vector3(x, groundYAt(x), 0f);
                go.transform.SetParent(parent);
                go.AddComponent<SpriteRenderer>();
                go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.6f, 0.6f, 0.55f), 40, 44);

                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(1.3f, 2f);
                collider.offset = new Vector2(0f, 1f);

                go.AddComponent<Rigidbody2D>();
                go.AddComponent<MonsterWalkSpriteAnimator>().Configure("Art/Monsters/Colossus/Walk");
                go.AddComponent<MonsterAttackSpriteAnimator>().Configure("Art/Monsters/Colossus/Attack", 0.9f);
                go.AddComponent<Monster>().AssignData(data);
                go.AddComponent<MonsterAI>();
                go.AddComponent<MonsterHealthBar>().Configure(newWidth: 1.4f, newVerticalOffset: 0.15f);
            }
        }

        private static void SpawnKnights(Transform parent, int floorY, IEnumerable<float> xOffsets)
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "기사";
            data.level = 1;
            // 플레이어 노드 한 타 약 13(방어력 3), 일반 채보 전부 Perfect면 약 145 — 슬라임보다 조금 질기다.
            data.maxHp = 170f;
            data.attack = 5f; // 놓친 노드마다 (5-1) x 0.7 + 5 = 약 8
            data.defense = 3f;
            data.expReward = 20; // 보스 전 일반 스테이지가 줄어든 만큼(4→2) 마리당 경험치를 2배로 올려 보정
            data.goldReward = 15;

            foreach (var x in xOffsets)
            {
                var go = new GameObject("Knight");
                go.transform.position = new Vector3(x, floorY, 0f);
                go.transform.SetParent(parent);
                go.AddComponent<SpriteRenderer>();
                go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.85f, 0.3f, 0.3f), 24, 24);

                var collider = go.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.6f, 1.3f);
                collider.offset = new Vector2(0f, 0.65f);

                go.AddComponent<Rigidbody2D>();
                go.AddComponent<MonsterWalkSpriteAnimator>();
                go.AddComponent<MonsterAttackSpriteAnimator>().Configure("Art/Monsters/Knight/Attack", 0.6f);
                go.AddComponent<Monster>().AssignData(data);
                go.AddComponent<MonsterAI>();
                go.AddComponent<MonsterHealthBar>();
            }
        }

        // 보스 기사는 맵 오른쪽 끝에서 플레이어 쪽(왼쪽)을 보고 대기 모션으로 서 있다가, 등장 연출(BossIntroCutscene)의
        // 대사가 끝나면 점프 내려찍기로 뛰어든다.
        private static void SpawnBoss(Transform parent, int floorY, int xMax)
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "보스 기사";
            data.level = 10;
            // 최종 보스. 보스 채보 한 턴 전부 Perfect면 약 245(방어력 5) — 1레벨 기준 일곱 턴 남짓.
            data.maxHp = 1800f;
            data.attack = 15f; // 놓친 노드마다 (15-1) x 0.7 + 5 = 약 15
            data.defense = 5f;
            data.expReward = 600;
            data.goldReward = 400;

            var go = new GameObject("Boss_Knight");
            go.transform.position = new Vector3(xMax - 1.5f, floorY, 0f);
            go.transform.SetParent(parent);
            go.AddComponent<SpriteRenderer>().flipX = true;
            go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.5f, 0.1f, 0.5f), 48, 72, PlaceholderShape.Humanoid);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.3f, 1.8f);
            collider.offset = new Vector2(0f, 0.9f);

            go.AddComponent<Rigidbody2D>();
            var bossWalkAnimator = go.AddComponent<MonsterWalkSpriteAnimator>();
            bossWalkAnimator.Configure("Art/Monsters/BossKnight/Walk");
            bossWalkAnimator.ConfigureIdle("Art/Monsters/BossKnight/Idle", 5f);
            var bossAttackAnimator = go.AddComponent<MonsterAttackSpriteAnimator>();
            bossAttackAnimator.AddClip(BossAI.JumpAttackClip, "Art/Monsters/BossKnight/JumpAttack");
            var monster = go.AddComponent<Monster>();
            monster.AssignData(data);
            var bossAI = go.AddComponent<BossAI>();
            go.AddComponent<MonsterHealthBar>().Configure(newWidth: 1.4f, newVerticalOffset: 0.15f);

            bossHealthUI.Bind(monster, data.monsterName);
            // 보스를 잡으면 월드로 돌아와 유언 → 쓰러지는 모션(BossDeathCutscene)이 끝난 뒤 스테이지 클리어 창을 띄운다.
            go.AddComponent<BossDeathCutscene>().Initialize(player, npcDialogueUI, () => stageClearUI.Show(() => RetryDungeon(1)));

            go.AddComponent<BossIntroCutscene>().Initialize(bossAI, player, npcDialogueUI, skippable: bossIntroSeen,
                onShown: () => bossIntroSeen = true);
            monster.OnDied += _ => TryDropDevilNecklace();
        }

        // 절망의 언덕의 최종보스 루나벨. 걷지 않고 언덕 오른쪽 끝에 서서 플레이어를 바라보며, 중거리에서는 손을 뻗어
        // 피의 손을 날리고, 몸에 닿으면 리듬전투(보스 패턴)로 싸운다(FinalBossAI). 보스 기사보다 단단하고 아프다.
        // 언덕에 들어오면 의자에 앉은 루나벨과 대사를 주고받고(LunabelIntroCutscene), 피의 기둥이 플레이어 발밑까지
        // 솟구친 뒤 루나벨이 일어서며 곧바로 리듬전투가 열린다.
        private static void SpawnLunabel(Transform parent, int floorY, int xMax)
        {
            var data = ScriptableObject.CreateInstance<MonsterData>();
            data.monsterName = "루나벨";
            data.level = 15;
            data.maxHp = 2600f;
            data.attack = 20f; // 놓친 노드마다 (20-1) x 0.7 + 5 = 약 18
            data.defense = 7f;
            data.expReward = 1000;
            data.goldReward = 700;

            var go = new GameObject("Boss_Lunabel");
            go.transform.position = new Vector3(xMax - 3f, floorY, 0f);
            go.transform.SetParent(parent);
            go.AddComponent<SpriteRenderer>().flipX = true;
            go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.75f, 0.1f, 0.2f), 48, 72, PlaceholderShape.Humanoid);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.3f, 2.2f);
            collider.offset = new Vector2(0f, 1.1f);

            go.AddComponent<Rigidbody2D>();
            // 걷기 모션이 없어서 대기 프레임을 걷기 자리에도 넣는다(걷기 프레임이 비면 애니메이터가 꺼진다). 제자리에 서 있으므로 늘 대기 모션이 돈다.
            var walkAnimator = go.AddComponent<MonsterWalkSpriteAnimator>();
            walkAnimator.Configure(LunabelIdlePath);
            walkAnimator.ConfigureIdle(LunabelIdlePath, 6f);
            go.AddComponent<MonsterAttackSpriteAnimator>().Configure(LunabelAttackPath, 1.0f);
            var monster = go.AddComponent<Monster>();
            monster.AssignData(data);
            var bossAI = go.AddComponent<FinalBossAI>();
            go.AddComponent<MonsterHealthBar>().Configure(newWidth: 1.6f, newVerticalOffset: 0.15f);

            bossHealthUI.Bind(monster, data.monsterName);
            // 루나벨을 쓰러뜨리면 필드로 돌아와 패배 모션 → 네크로시아와의 마지막 대화 → 쓰러져 사라진 뒤 마을의 노아 앞으로 돌아간다.
            go.AddComponent<LunabelDeathCutscene>().Initialize(player, npcDialogueUI, () => ReturnToVillage(spawnAtNoa: true));

            go.AddComponent<LunabelIntroCutscene>().Initialize(bossAI, player, npcDialogueUI, skippable: lunabelIntroSeen,
                onShown: () => lunabelIntroSeen = true);
        }

        // 원본 시트(고딕 붉은 여왕 6프레임, 고딕 붉은 마법 공격 시퀀스)는 왼쪽을 보고 있어서, 다른 몬스터처럼
        // 오른쪽을 보도록 좌우 반전해 잘라 두었다. 공격 시퀀스는 검은 배경을 지우고 8프레임으로 나눴다.
        private const string LunabelIdlePath = FinalBossAI.IdlePath;
        private const string LunabelAttackPath = "Art/Monsters/FinalBoss/Attack";

        // 강화석 드랍: 일반 몬스터는 25% 확률로 1개, 보스(기사/슬라임)는 항상 3개.
        private const float EnhanceStoneDropChance = 0.25f;
        private const int BossEnhanceStoneDrop = 3;
        // 수정구: 보스(기사/슬라임)를 잡을 때마다 항상 3개.
        private const int BossCrystalOrbDrop = 3;
        private const int StarterPotionCount = 3;

        private static void HandleMonsterDiedForDrops(Monster monster)
        {
            if (player == null || player.IsDead || monster == null) return;

            bool isBoss = monster.GetComponent<BossAI>() != null || monster.GetComponent<BossSlimeAI>() != null
                || monster.GetComponent<FinalBossAI>() != null;
            if (isBoss)
            {
                player.GetComponent<Inventory>().AddItem(ItemDatabase.EnhanceStone, BossEnhanceStoneDrop);
                player.GetComponent<Inventory>().AddItem(ItemDatabase.CrystalOrb, BossCrystalOrbDrop);
            }
            else if (Random.value < EnhanceStoneDropChance)
            {
                player.GetComponent<Inventory>().AddItem(ItemDatabase.EnhanceStone);
            }
        }

        // 보스 기사를 잡으면 30% 확률로 악마의 목걸이(장식)를 준다.
        private const float DevilNecklaceDropChance = 0.3f;

        private static void TryDropDevilNecklace()
        {
            if (Random.value >= DevilNecklaceDropChance) return;
            if (player == null) return;

            player.GetComponent<Inventory>().AddItem(ItemDatabase.DevilNecklace);
        }

        private const float PortalTriggerRadius = 2f;
        private const float PortalHeightAboveFloor = 0.7f;
        private const float PortalVisualInwardOffset = 0.9f;

        // 지정한 x 위치에 포탈을 놓는다. 포탈 그림이 반복 재생되고, 플레이어가 일정 거리 안에 들어오면
        // 자동으로 다음(또는 backward=true면 이전) 스테이지로 넘어간다. 반환값은 호출부에서
        // SetCustomTrigger로 기본 동작을 다른 걸로 바꿔치기하고 싶을 때(월드맵 등) 쓴다.
        // framesPath: 지역 전용 포탈 그림이 있으면 넘긴다(예: 엔스룸은 금빛 포탈). 안 주면 기본 포탈.
        // overlayFramesPath까지 주면 framesPath는 고정 테두리 한 장, overlayFramesPath는 그 위에서
        // 애니메이션되는 안쪽 거울로 따로 그린다(엔스룸 포탈처럼 테두리가 커서 통째로 갈아 끼우면
        // 떨려 보이는 경우에 쓴다).
        private static Portal SpawnPortal(Transform parent, float x, float floorY, bool backward,
            string framesPath = null, string overlayFramesPath = null)
        {
            var go = new GameObject(backward ? "Portal_Back" : "Portal_Forward");
            go.transform.position = new Vector3(x, floorY + PortalHeightAboveFloor, 0f);
            go.transform.SetParent(parent);

            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = PortalTriggerRadius;

            var portal = go.AddComponent<Portal>();
            portal.Configure(backward, player.transform);
            if (overlayFramesPath != null) portal.CreateLayeredVisual(PortalVisualInwardOffset, PortalHeightAboveFloor, framesPath, overlayFramesPath);
            else if (framesPath != null) portal.CreateVisual(PortalVisualInwardOffset, PortalHeightAboveFloor, framesPath);
            else portal.CreateVisual(PortalVisualInwardOffset, PortalHeightAboveFloor);
            return portal;
        }
    }
}
