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
    // HUD와 각종 UI 생성.
    public static partial class GameBootstrap
    {
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
            // HUD 프레임 오른쪽 회복 포션 칸([F] · 개수 · 쿨타임).
            canvasGO.AddComponent<PotionSlotUI>().Initialize(player);
            // 쓰러지면 게임오버 화면 대신 잠깐의 연출 후 마을로 돌아간다.
            canvasGO.AddComponent<DeathReturnUI>().Initialize(player);

            stageClearUI = canvasGO.AddComponent<StageClearUI>();
            stageClearUI.Initialize(player);

            bossHealthUI = canvasGO.AddComponent<BossHealthUI>();
            bossHealthUI.Initialize();

            // 던전(방 구조 지역) 지도: 오른쪽 아래 작은 지도, M으로 크게.
            var minimap = canvasGO.AddComponent<MinimapUI>();
            minimap.Initialize();
            InitMinimap(minimap);

            npcDialogueUI = canvasGO.AddComponent<NpcDialogueUI>();
            npcDialogueUI.Initialize();

            worldMapUI = canvasGO.AddComponent<WorldMapUI>();
            worldMapUI.Initialize(player.GetComponent<CharacterMovement2D>());

            portalConfirmUI = canvasGO.AddComponent<PortalConfirmUI>();
            portalConfirmUI.Initialize(player.GetComponent<CharacterMovement2D>());

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

            // 이시스에게 기술을 배우는 화면. 하나 배울 때마다 저장한다.
            skillLearnUI = canvasGO.AddComponent<SkillLearnUI>();
            skillLearnUI.Initialize(player, player.GetComponent<CharacterMovement2D>(), SaveGame);

            bestiaryUI = canvasGO.AddComponent<BestiaryUI>();
            bestiaryUI.Initialize(player.GetComponent<CharacterMovement2D>());

            // 리듬 전투 화면. 필드 전투는 실시간이고, 이 화면은 엔스룸 릴리의 리듬 대결(미니게임)에서만 열린다
            // (LilyNpc가 FindFirstObjectByType으로 찾아 쓴다).
            canvasGO.AddComponent<RhythmBattleUI>().Initialize();

            questManager = canvasGO.AddComponent<QuestManager>();
            questManager.Initialize(player, QuestDatabase.CreateMainLine());
            // 월드맵 지역은 메인 퀘스트(스토리) 진행에 맞춰 열린다.
            worldMapUI.SetQuestManager(questManager);
            questTrackerUI = canvasGO.AddComponent<QuestTrackerUI>();
            questTrackerUI.Initialize(questManager);

            dailyQuestManager = canvasGO.AddComponent<DailyQuestManager>();
            var mainQuests = questManager;
            dailyQuestManager.Initialize(player, DailyQuestDatabase.CreatePool(), () => mainQuests != null ? mainQuests.QuestIndex : 0);
            canvasGO.AddComponent<DailyQuestTrackerUI>().Initialize(dailyQuestManager);

            saveToast = canvasGO.AddComponent<SaveToastUI>();
            saveToast.Initialize();

            // F1로 여닫는 관리자 모드(골드/레벨/무적/즉사/순간이동).
            canvasGO.AddComponent<AdminUI>().Initialize(player);

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
            // 루나벨을 처음 쓰러뜨린 뒤 한 번 나오는 엔딩(에필로그 + 크레딧).
            endingCreditsUI = canvasGO.AddComponent<EndingCreditsUI>();
            endingCreditsUI.Initialize();
        }
    }
}
