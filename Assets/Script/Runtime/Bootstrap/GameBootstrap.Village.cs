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
    // 마을, 튜토리얼 숲, 어둠의 숲 발판, 마을 건물(노아 상점/연습장/엘리 대장간), 연습장 맵.
    public static partial class GameBootstrap
    {
        // 어둠의 숲 발판: 원화(DrakForest_scattfolding) 속 이끼 낀 바위섬/구름다리를 조각내 전용
        // 스프라이트(Art/Props/Forest)로 만들고 실제로 밟을 수 있게 올려준다.
        // 1·2번 맵은 폭 40칸(x -20~19)이고 생김새를 확 다르게 했다. 점프 규칙: 1단 약 1.6, 2단 약 3.25 높이,
        // 이동 4칸/초라 2단 점프 한 번에 옆으로 약 2.8칸 — 이어 오르는 발판끼리는 높이 차 2.3 이하, 가장자리 간격 2 이하.
        //   1 "숲길": 옆으로 넓게 퍼진 두 층 길. 출발 쪽 징검돌 → 긴 나무 위 길(wide) → 구름다리 → 내리막,
        //     오른쪽 끝에서 위로 꺾여 올라가는 숨은 길 꼭대기(바닥 위 8)에 보물상자.
        //   2 "거목 오르기": 맵 가운데에 지그재그로 쌓인 바위섬 탑을 타고 바닥 위 11까지 오르는 수직 맵.
        //     꼭대기에 보물상자, 탑 중턱 왼쪽 곁가지 끝에 숨겨진 포탈(GameBootstrap.Exploration).
        //   보스 맵: 알리아스와 땅에서 싸우도록 낮은 발판 네 개만(높이 올라가도 대각선 검기가 날아온다)
        private static void SpawnForestPlatforms(Transform parent, int floorY, int stageIndex)
        {
            switch (stageIndex)
            {
                case 1:
                    // 출발: 낮은 징검돌 두 개
                    SpawnPlatform(parent, "forest_platform_4", -15.5f, floorY + 2.2f, 70, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_6", -12.6f, floorY + 2.6f, 44, ForestPropFolder);
                    // 나무 위 긴 길(폭 약 6) → 구름다리 → 내리막 섬 두 개
                    SpawnPlatform(parent, "forest_platform_wide", -7.0f, floorY + 4.4f, 80, ForestPropFolder);
                    SpawnPlatform(parent, "forest_bridge", -1.0f, floorY + 4.8f, 98, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_3", 2.6f, floorY + 3.0f, 48, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_2", 5.5f, floorY + 2.2f, 43, ForestPropFolder);
                    // 숨은 길: 오른쪽에서 위로 꺾여 꼭대기 섬(보물상자)까지
                    SpawnPlatform(parent, "forest_platform_6", 8.4f, floorY + 3.6f, 44, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_7", 10.6f, floorY + 5.6f, 34, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_9", 12.4f, floorY + 7.4f, 36, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_1", 15.4f, floorY + 8.0f, 77, ForestPropFolder);
                    // 출구 앞 낮은 섬
                    SpawnPlatform(parent, "forest_platform_4", 16.2f, floorY + 2.4f, 70, ForestPropFolder);
                    break;
                case 2:
                    // 왼쪽 들머리: 낮은 섬들
                    SpawnPlatform(parent, "forest_platform_2", -15.0f, floorY + 2.2f, 43, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_7", -11.8f, floorY + 3.8f, 34, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_6", -9.2f, floorY + 2.4f, 44, ForestPropFolder);
                    // 가운데 탑: 좌우로 번갈아 오르는 지그재그
                    SpawnPlatform(parent, "forest_platform_4", -4.5f, floorY + 2.3f, 70, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_6", -1.5f, floorY + 4.4f, 44, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_3", -4.6f, floorY + 6.5f, 48, ForestPropFolder);
                    SpawnPlatform(parent, "forest_bridge", -0.8f, floorY + 8.4f, 98, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_6", 3.0f, floorY + 10.2f, 44, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_1", 6.8f, floorY + 11.0f, 77, ForestPropFolder);
                    // 탑 중턱 왼쪽 곁가지(숨겨진 포탈)
                    SpawnPlatform(parent, "forest_platform_5", -3.7f, floorY + 10.0f, 29, ForestPropFolder);
                    // 오른쪽 내리막: 꼭대기에서 뛰어내려 바닥으로
                    SpawnPlatform(parent, "forest_platform_2", 10.6f, floorY + 6.4f, 43, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_4", 12.8f, floorY + 2.5f, 70, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_6", 15.6f, floorY + 3.4f, 44, ForestPropFolder);
                    break;
                default:
                    SpawnPlatform(parent, "forest_platform_7", -7.6f, floorY + 2.2f, 40, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_5", -3.8f, floorY + 2.5f, 53, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_6", 0.3f, floorY + 2.3f, 47, ForestPropFolder);
                    SpawnPlatform(parent, "forest_platform_3", 4.4f, floorY + 2.4f, 50, ForestPropFolder);
                    break;
            }
        }

        private const string ForestPropFolder = "Art/Props/Forest/";

        // 마을: 몬스터도 StageManager도 없는 안전 지역. 포탈까지 걸어가면 1스테이지로 들어간다.
        // 캐릭터가 떠 보인다는 피드백에 따라 바닥 기준점을 그림 더 아래쪽(가장 앞쪽 보도블록)으로 내림.
        private const float VillageGroundFraction = 0.87f;

        private static void BuildVillage(bool enteringForward, VillageSpawn spawn)
        {
            // 배경은 한 장짜리 그림(Backgrounds/village) 대신 시차 배경(하늘·먼 마을·고딕 거리·돌바닥)을 깐다.
            BuildMap(stageRoot.transform, VillageWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                null, VillageGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;
            SpawnParallax(VillageParallaxLayers(), xMin, xMax, yMin);
            SpawnVillageFountain(stageRoot.transform, yMin);

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
                case VillageSpawn.Center:
                    PlacePlayerAtSpawn((xMin + xMax) * 0.5f, yMin);
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
            var exitPortal = SpawnPortal(stageRoot.transform, xMax - 0.5f, yMin, backward: false, framed: VillagePortalStyle);
            exitPortal.SetCustomTrigger(() => worldMapUI.Show(exitPortal));
            // 은빛 문틀이 어두운 밤 마을 배경에서 혼자 떠 보여서, 배경 조명 색으로 눌러 주고 발밑 그림자와 보랏빛 번짐을 깐다.
            exitPortal.BlendIntoScene(new Color(0.72f, 0.66f, 0.85f), new Color(0.62f, 0.32f, 1f, 0.45f), -PortalHeightAboveFloor);

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
                    var exit = SpawnPortal(root, xMax - 0.5f, yMin, backward: false, framed: VillagePortalStyle);
                    exit.SetCustomTrigger(LeaveTutorialForest);
                });

            Debug.Log("Aethoria: 튜토리얼 숲 진입");
        }

        // 튜토리얼 몬스터(Tutorial_Monster_Move, 6프레임 걷기). 첫 전투라 평야 슬라임보다 약하게:
        // 일반 공격 한 턴이면 거의 잡히고, 놓쳐도 아프지 않다.
        private const string ForestSpiritWalkPath = "Art/Monsters/ForestSpirit/Walk";

        private static Monster SpawnForestSpirit(Transform parent, int floorY, float x)
        {
            var data = ForestSpiritData();

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
            isis.Initialize(player.transform, npcDialogueUI, inventoryUI, skillLearnUI);
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
        // 배경(TrainingHall — Sprite/Map Sprite/01_Village/Original/고딕 성당의 보랏빛 훈련장, 1774x887): 허수아비가 선 뒤쪽보다 조금 앞,
        // 반들반들한 돌바닥 가운데(그림 위에서 약 755px)를 바닥으로 삼는다.
        private const float PracticeGroundFraction = 755f / 887f;
        // 1.2배: 그림 아래끝이 화면 아래끝(카메라 높이 바닥+2.4, 크기 4 → 바닥 아래 1.6)까지 덮이는 가장 작은 크기이고,
        // 난간이 캐릭터 허리께에 와서 비율도 자연스럽다. 그림 폭(약 21유닛)이 맵 폭(20)을 한 장으로 덮어 좌우 반전 복사본은
        // 붙지 않고, 그림 위끝도 화면 위끝보다 한참 높아 위로 늘린 하늘(어색하게 늘어져 보였다)도 붙이지 않는다.
        private const float PracticeBackgroundScale = 1.2f;
        // 배경 그림 속 허수아비(약 -0.9, 1.5, 3.9, 6.3)와 겹쳐 둘로 보이지 않게 그 사이에 세운다.
        private static readonly float[] DummyXs = { 0.3f, 5.1f };

        private static void BuildPracticeRoom()
        {
            BuildMap(stageRoot.transform, PracticeWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                "Backgrounds/TrainingHall", PracticeGroundFraction, PracticeBackgroundScale, skyExtension: false);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            player.SetCombatLocked(false);
            // 왼쪽 끝 귀환 포탈(반경 2)의 트리거에 닿지 않을 만큼 떨어진 곳에 세운다. xMin + 2.5처럼 반경 경계 바로 밖이면
            // 스폰 시 "이미 안에 있음" 판정(중심 거리 기준)은 피하면서도 플레이어 콜라이더는 원에 걸쳐서, 들어오자마자 마을로 튕겨 나갔다.
            PlacePlayerAtSpawn(xMin + 4f, yMin);
            FitCamera(xMin, xMax, yMin);

            SpawnTrainingDummies(stageRoot.transform, yMin);

            var backPortal = SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true, framed: PracticePortalStyle);
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
                // 플레이어가 머리 위에 올라서지 못하게 트리거로 둔다(공격/스킬 판정은 트리거도 잡으므로 그대로 맞는다).
                collider.isTrigger = true;

                // 중력/넉백으로 밀려나지 않게 고정한다(사슬로 끌어오는 것처럼 위치를 직접 옮기는 스킬은 그대로 먹힌다).
                var body = go.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;

                go.AddComponent<Monster>().AssignData(data);
                go.AddComponent<TrainingDummy>();
            }
        }
    }
}
