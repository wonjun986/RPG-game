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
        private static QuestManager questManager;
        private static DailyQuestManager dailyQuestManager;
        private static SaveToastUI saveToast;
        private static SaveScreenUI saveScreen;
        private static TutorialUI tutorialUI;
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
            if (save != null)
            {
                player.RestoreProgress(save.level, save.exp, save.gold);
                inventory.RestoreItems(save.items);
                inventory.RestoreEquipped(save.equipped);
                inventory.RestoreEnhanceLevels(save.enhanceLevels);
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

            currentStage = VillageStage;
            BuildStage(enteringForward: true);

            // 조작법 안내창은 새 게임을 처음 시작했을 때만 뜬다(이어하기는 건너뜀).
            if (save == null) tutorialUI.Show();
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
                questIndex = questManager.QuestIndex,
                questStatus = (int)questManager.Status,
                questProgress = questManager.Progress,
                dailyQuestIndex = dailyQuestManager.QuestIndex,
                dailyQuestStatus = (int)dailyQuestManager.Status,
                dailyQuestProgress = dailyQuestManager.Progress,
                dailyQuestResetDate = dailyQuestManager.ResetDate,
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
            questManager = null;
            dailyQuestManager = null;
            saveToast = null;
            saveScreen = null;
            tutorialUI = null;
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
            if (enteringForward && (currentStage == 1 || currentStage == PlainsStage1 || currentStage == RuinsStage1)) stageClearUI.BeginRun();

            if (currentStage == VillageStage)
            {
                BuildVillage(enteringForward, villageSpawn);
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

            // 보스 맵에는 보스만 나오고 일반 몬스터는 나오지 않는다.
            if (currentStage == BossStage)
            {
                SpawnBoss(stageRoot.transform, yMin);
            }
            else
            {
                SpawnKnights(stageRoot.transform, yMin, MonsterSpawner.PickSpawnXs(KnightsPerStage, xMin, xMax, playerSpawnX));
            }

            // 왼쪽 끝은 항상 이전 스테이지(마을 포함)로 돌아가는 포탈, 오른쪽 끝은 마지막 스테이지가
            // 아닐 때만 다음 스테이지로 가는 포탈.
            SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true);
            if (currentStage < TotalStages)
            {
                SpawnPortal(stageRoot.transform, xMax - 0.5f, yMin, backward: false);
            }

            var stageManagerGO = new GameObject("StageManager");
            stageManagerGO.transform.SetParent(stageRoot.transform);
            stageManagerGO.AddComponent<StageManager>().Initialize(player);

            Debug.Log($"Aethoria: 맵 {currentStage}/{TotalStages} 진입");
        }

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

        private static void BuildEnsroom()
        {
            BuildMap(stageRoot.transform, EnsroomWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                "Backgrounds/ensroom", EnsroomGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            player.SetCombatLocked(true);
            // 왼쪽 끝 귀환 포탈 트리거(반경 2)에 걸리지 않을 만큼 떨어진 곳에 세운다(연습장과 같은 이유).
            PlacePlayerAtSpawn(xMin + 4f, yMin);
            FitCamera(xMin, xMax, yMin);

            var backPortal = SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true);
            backPortal.SetCustomTrigger(LeaveFieldToVillage);

            Debug.Log("Aethoria: 엔스룸 마을 진입");
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
            noaGO.AddComponent<NoaNpc>().Initialize(player.transform, npcDialogueUI, questManager, dailyQuestManager, saveScreen, inventoryUI, shopUI);
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
            isisGO.AddComponent<IsisNpc>().Initialize(player.transform, npcDialogueUI, inventoryUI);
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
            string backgroundResourcePath, float groundFraction, float fixedScale = 0f)
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
                renderer.sortingOrder = -10;
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
        }

        private static Character SpawnPlayer()
        {
            var data = ScriptableObject.CreateInstance<CharacterData>();
            data.characterName = "소울이터";
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

        private static void SpawnBoss(Transform parent, int floorY)
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
            go.transform.position = new Vector3(5f, floorY, 0f);
            go.transform.SetParent(parent);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<CharacterPlaceholderVisual>().Configure(new Color(0.5f, 0.1f, 0.5f), 48, 72, PlaceholderShape.Humanoid);

            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.3f, 1.8f);
            collider.offset = new Vector2(0f, 0.9f);

            go.AddComponent<Rigidbody2D>();
            go.AddComponent<MonsterWalkSpriteAnimator>().Configure("Art/Monsters/BossKnight/Walk");
            var bossAttackAnimator = go.AddComponent<MonsterAttackSpriteAnimator>();
            bossAttackAnimator.AddClip(BossAI.JumpAttackClip, "Art/Monsters/BossKnight/JumpAttack");
            var monster = go.AddComponent<Monster>();
            monster.AssignData(data);
            go.AddComponent<BossAI>();
            go.AddComponent<MonsterHealthBar>().Configure(newWidth: 1.4f, newVerticalOffset: 0.15f);

            bossHealthUI.Bind(monster, data.monsterName);
            monster.OnDied += _ => stageClearUI.Show(() => RetryDungeon(1)); // 보스를 잡으면 스테이지 클리어 창
            monster.OnDied += _ => TryDropDevilNecklace();
        }

        // 강화석 드랍: 일반 몬스터는 25% 확률로 1개, 보스(기사/슬라임)는 항상 3개.
        private const float EnhanceStoneDropChance = 0.25f;
        private const int BossEnhanceStoneDrop = 3;
        private const int StarterPotionCount = 3;

        private static void HandleMonsterDiedForDrops(Monster monster)
        {
            if (player == null || player.IsDead || monster == null) return;

            bool isBoss = monster.GetComponent<BossAI>() != null || monster.GetComponent<BossSlimeAI>() != null;
            if (isBoss)
            {
                player.GetComponent<Inventory>().AddItem(ItemDatabase.EnhanceStone, BossEnhanceStoneDrop);
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
        private static Portal SpawnPortal(Transform parent, float x, float floorY, bool backward)
        {
            var go = new GameObject(backward ? "Portal_Back" : "Portal_Forward");
            go.transform.position = new Vector3(x, floorY + PortalHeightAboveFloor, 0f);
            go.transform.SetParent(parent);

            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = PortalTriggerRadius;

            var portal = go.AddComponent<Portal>();
            portal.Configure(backward, player.transform);
            portal.CreateVisual(PortalVisualInwardOffset, PortalHeightAboveFloor);
            return portal;
        }
    }
}
