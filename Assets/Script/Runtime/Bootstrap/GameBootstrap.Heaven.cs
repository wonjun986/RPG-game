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
    // 엔스룸 마을, 절망의 언덕, 천상의 신전/이리아의 성소.
    public static partial class GameBootstrap
    {
        // 엔스룸 마을: 몬스터도 StageManager도 없는 안전 지역. 왼쪽 끝 포탈로 처음 마을(출구 쪽)로 돌아간다.
        // 배경은 한 장짜리 그림(Backgrounds/ensroom) 대신 시차 배경(낮하늘·성채 도시·대리석 거리·광장 바닥, EnsroomParallaxLayers)을 깐다.
        // 광장 가운데에는 물줄기가 솟았다 잦아드는 대리석 분수(8프레임)가 선다.
        private const float EnsroomGroundFraction = 0.87f; // 시차 배경이라 쓰이지 않지만 BuildMap 인자로 넘긴다
        private const string EnsroomFountainFolder = "Backgrounds/Parallax/Ensroom/Props/Fountain";
        private const float EnsroomFountainX = 1.2f;       // 릴리(LilyX) 바로 오른쪽
        private const float EnsroomFountainHeight = 3.4f;  // 물줄기 꼭대기까지(캐릭터 키의 약 1.5배)
        private const float EnsroomFountainFrameTime = 0.11f;
        // 엔스룸 포탈: 등나무꽃과 금장식이 감긴 하얀 대리석 고딕 아치(보랏빛 꽃장식의 고딕 마블 아치문, 1086x1448) 한 장을 고정으로
        // 세우고, 아치 안쪽에 금빛·하늘빛 꽃잎 소용돌이(마법의 소용돌이 애니메이션 프레임, 4장)를 번갈아 쓰며 22.5도씩 돌려
        // 아치 구멍 모양으로 자른 16프레임을 돌린다. 구멍은 문틀 그림 (250,254)~(835,1269)이고, 소용돌이 프레임은 그 칸을
        // 절반 해상도(292x507)로 저장했다(구멍 가장자리를 4px 넓혀 문틀 뒤로 파고들게 했다). 그림 맨 아래가 거의 꽉 차 있어 살짝만 묻는다.
        private static readonly FramedPortalStyle EnsroomPortalStyle = new FramedPortalStyle(
            "Art/Props/EnsroomPortal/ensroom_portal_base", "Art/Props/EnsroomPortal/Swirl", new Vector2(542.5f, 1269f), sink: 0.03f);
        // 예전 엔스룸 포탈(테두리 + 거울 6프레임, 1칸 = 240px)이 쓰던 묻는 깊이. 겹친 그림 포탈(CreateLayeredVisual)용으로 남겨 둔다.
        private const float EnsroomPortalSink = 0.15f;

        private static void BuildEnsroom()
        {
            BuildMap(stageRoot.transform, EnsroomWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                null, EnsroomGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;
            SpawnParallax(EnsroomParallaxLayers(), xMin, xMax, yMin);
            SpawnEnsroomFountain(stageRoot.transform, yMin);

            player.SetCombatLocked(true);
            // 왼쪽 끝 귀환 포탈 트리거(반경 2)에 걸리지 않을 만큼 떨어진 곳에 세운다(연습장과 같은 이유).
            PlacePlayerAtSpawn(xMin + 4f, yMin);
            FitCamera(xMin, xMax, yMin);

            var backPortal = SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true, framed: EnsroomPortalStyle);
            backPortal.SetCustomTrigger(LeaveFieldToVillage);

            SpawnLily(stageRoot.transform, yMin);

            Debug.Log("Aethoria: 엔스룸 마을 진입");
        }

        // 광장 분수: 프레임(252x281, 1칸 = 135px, 아래 가운데 기준)마다 수반까지 함께 그려져 있어 통째로 넘긴다.
        // 광장 바닥(-9)·거리(-12)보다 앞, 캐릭터보다 뒤. 밑동을 바닥 타일에 살짝 묻는다.
        private static void SpawnEnsroomFountain(Transform parent, int floorY)
        {
            var fountain = FlipbookSpriteEffect.Create("EnsroomFountain", EnsroomFountainFolder,
                new Vector2(EnsroomFountainX, floorY - 0.1f), EnsroomFountainFrameTime, loop: true, sortingOrder: -7);
            if (fountain == null) return;
            fountain.transform.SetParent(parent, true);
            float frameHeight = 281f / 135f;
            float scale = EnsroomFountainHeight / frameHeight;
            fountain.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // 릴리: 광장 분수(x≈1.2) 바로 왼쪽에 세운다. 분수 앞을 가리지 않고,
        // 입장 지점(x≈-12)에서 걸어오면 자연스럽게 눈에 들어오는 자리.
        private const float LilyX = -1.3f;

        private static void SpawnLily(Transform parent, int floorY)
        {
            var lilyGO = new GameObject("Lily", typeof(SpriteRenderer));
            lilyGO.transform.SetParent(parent);
            lilyGO.transform.position = new Vector3(LilyX, floorY, 0f);
            lilyGO.AddComponent<LilyNpc>().Initialize(player.transform, npcDialogueUI, bestiaryUI);
        }

        // 절망의 언덕: 붉은 달 아래 무너진 언덕을 걸어 올라 끝의 결전장에서 최종보스 루나벨과 싸운다(72칸).
        //  x -36 ~ 9  걸어가는 길: 말라 죽은 고목, 무너진 아치문, 용암 균열 돌섬·사슬 다리. 루나벨의 목소리와 붉은 환영(HillApproach).
        //  x 9 ~ 35   결전장: 평평한 바닥(보스 패턴이 평지 기준이라 발판을 두지 않는다). 입구(HillArenaX)를 넘으면 등장 연출이 시작되고
        //             입구가 막혀 전투 구역이 결전장으로 좁혀진다. 쓰러져 다시 도전하면 걷는 길을 건너뛰고 결전장 입구에서 시작한다.
        private const float HillArenaX = 9f;
        private const float HillArenaTriggerX = 12f;
        private static bool hillStartAtArena;
        private const string HillPropFolder = "Art/Props/Hill/";
        private const string HillBackdropFolder = "Backgrounds/Parallax/Hill/Props/";
        private static readonly Color HillBackdropTint = new Color(0.62f, 0.5f, 0.55f);

        private static void BuildHill()
        {
            bool atArena = hillStartAtArena;
            hillStartAtArena = false;

            BuildMap(stageRoot.transform, HillWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                null, 0.85f);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;
            SpawnParallax(HillParallaxLayers(), xMin, xMax, yMin);

            player.SetCombatLocked(false);
            PlacePlayerAtSpawn(atArena ? HillArenaX + 1.5f : xMin + 3f, yMin);
            FitCamera(xMin, xMax, yMin);

            SpawnHillBackdrop(stageRoot.transform, yMin);
            SpawnHillPlatforms(stageRoot.transform, yMin);

            // 왼쪽 끝 포탈: 마을로 돌아갈지 묻는다(결전장에 들어가기 전까지는 돌아갈 수 있다).
            var backPortal = SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true);
            backPortal.SetCustomTrigger(() => ConfirmReturnToVillage(backPortal));

            var intro = SpawnLunabel(stageRoot.transform, yMin, xMax);
            int floor = yMin, right = xMax;
            if (atArena)
            {
                LockHillArena(floor, right);
            }
            else
            {
                intro.TriggerX = HillArenaTriggerX;
                intro.OnTriggered = () => LockHillArena(floor, right);
                SpawnHillApproach(yMin);
            }

            var stageManagerGO = new GameObject("StageManager");
            stageManagerGO.transform.SetParent(stageRoot.transform);
            stageManagerGO.AddComponent<StageManager>().Initialize(player);

            Debug.Log("Aethoria: 절망의 언덕 진입");
        }

        // 결전장 입구에 보이지 않는 벽을 세우고, 맵 왼쪽 끝(보스 패턴·카메라가 쓰는 범위)을 입구로 옮긴다.
        private static void LockHillArena(int floorY, int xMax)
        {
            if (stageRoot == null) return;
            var wall = new GameObject("HillArenaWall", typeof(BoxCollider2D));
            wall.transform.SetParent(stageRoot.transform);
            wall.transform.position = new Vector3(HillArenaX - WallThickness / 2f, floorY + MapHeight / 2f, 0f);
            wall.GetComponent<BoxCollider2D>().size = new Vector2(WallThickness, MapHeight + 4f);
            mapXMin = Mathf.RoundToInt(HillArenaX);
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.SetHorizontalBounds(mapXMin, xMax);
        }

        // 배경 소품: 고목 둘, 무너진 아치문 둘(가운데 구간 입구와 결전장 입구).
        private static void SpawnHillBackdrop(Transform parent, int floorY)
        {
            SpawnBackdropProp(parent, HillBackdropFolder + "hill_prop_deadtree", -27f, floorY, 0.45f, HillBackdropTint);
            SpawnBackdropProp(parent, HillBackdropFolder + "hill_prop_gate", -13.5f, floorY, 0.5f, HillBackdropTint);
            SpawnBackdropProp(parent, HillBackdropFolder + "hill_prop_deadtree", -2.5f, floorY, 0.55f, HillBackdropTint);
            SpawnBackdropProp(parent, HillBackdropFolder + "hill_prop_gate", HillArenaX - 1.2f, floorY, 0.68f, HillBackdropTint);
            SpawnBackdropProp(parent, HillBackdropFolder + "hill_prop_deadtree", 22f, floorY, 0.5f, HillBackdropTint * 0.85f);
        }

        // 걷는 길의 발판(용암 균열 타일셋). 2단 점프(약 3.25)로 이어지는 윗길과, 바닥에 선 사슬 다리·계단·기둥.
        // surfaceFromTopPx는 조각 그림 맨 위에서 밟는 면까지의 픽셀(PPU 130).
        private static void SpawnHillPlatforms(Transform parent, int floorY)
        {
            SpawnHillGround(parent, "hill_pillar_stump", -31.5f, floorY, -1);
            SpawnHillGround(parent, "hill_stairs", -22f, floorY, 44);
            SpawnPlatform(parent, "hill_rock_a", -25.5f, floorY + 2.6f, 32, HillPropFolder);
            SpawnPlatform(parent, "hill_ledge_a", -18.5f, floorY + 3.4f, 22, HillPropFolder);
            SpawnPlatform(parent, "hill_rock_c", -14.5f, floorY + 5.2f, 24, HillPropFolder);
            SpawnHillGround(parent, "hill_pillar_tall", -9.5f, floorY, 4);
            SpawnPlatform(parent, "hill_rock_b", -7f, floorY + 3.9f, 40, HillPropFolder);
            SpawnHillGround(parent, "hill_bridge", -0.5f, floorY, 20);
            SpawnPlatform(parent, "hill_ledge_b", 1.5f, floorY + 4.2f, 31, HillPropFolder);
            SpawnHillGround(parent, "hill_pillar_mid", 6.2f, floorY, 20);
        }

        private static void SpawnHillGround(Transform parent, string name, float x, int floorY, int surfaceFromTopPx)
        {
            var go = CreatePropSprite(parent, name, new Vector3(x, floorY - GroundPropSink, 0f), out var sprite, HillPropFolder);
            if (go == null || surfaceFromTopPx < 0) return;
            AddOneWaySurface(go, sprite, (sprite.rect.height - surfaceFromTopPx) / sprite.pixelsPerUnit);
        }

        // 걸어가는 동안 들리는 루나벨의 목소리와 붉은 환영.
        private static void SpawnHillApproach(int floorY)
        {
            var frames = Resources.LoadAll<Sprite>(FinalBossAI.IdlePath);
            Sprite phantom = null;
            if (frames != null && frames.Length > 0)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                phantom = frames[0];
            }
            var go = new GameObject("HillApproach");
            go.transform.SetParent(stageRoot.transform);
            go.AddComponent<HillApproach>().Initialize(player.transform, npcDialogueUI, new[]
            {
                new HillApproach.Voice { X = -30f, Line = "...또 왔구나, 사신. 그 다리로 여기까지 기어 올라오다니." },
                new HillApproach.Voice { X = -21f, Necrosia = true, Line = "...목소리가 언덕 전체에서 울린다." },
                new HillApproach.Voice { X = -12f, Line = "그날 네 피 맛이 아직도 혀끝에 남아 있어. 달콤하고, 따뜻했지." },
                new HillApproach.Voice { X = -4f, Line = "도망치지 않은 건 칭찬해 줄게. 이번엔... 얼마나 버틸까?", PhantomX = 3.5f },
                new HillApproach.Voice { X = 5f, Necrosia = true, Line = "이번엔 다르다. 빼앗긴 건 전부 되찾는다." },
            }, phantom, floorY);
        }

        // 천상의 신전, 이리아의 성소 포탈: 보랏빛 성전 문틀(신비로운 보랏빛 성전 포털, 1086x1448) 한 장을 고정으로 세우고,
        // 그 구멍 안에 푸른 소용돌이(마법의 푸른 소용돌이 포털)를 22.5도씩 돌려 구멍 모양으로 잘라 둔 16프레임을 돌린다.
        // 소용돌이 프레임은 문틀 그림 (300,374)~(786,1254) 칸을 절반 해상도(243x440)로 저장한 것이다.
        private static readonly FramedPortalStyle HeavenPortalStyle = new FramedPortalStyle(
            "Art/Props/HeavenPortal/heaven_portal_base", "Art/Props/HeavenPortal/Swirl", new Vector2(543f, 1254f));

        // 처음 마을 출구 포탈: 고딕 수정문 문틀(고딕 판타지 수정문 포털 프레임, 1086x1448) 한 장을 고정으로 세우고, 그 구멍 안에
        // 보랏빛 소용돌이(보랏빛 마법 포털 4프레임 스프라이트) 4장을 번갈아 쓰며 22.5도씩 돌려 구멍 모양으로 잘라 둔 16프레임을 돌린다.
        // 소용돌이 프레임은 문틀 그림 (296,418)~(790,1246) 칸을 절반 해상도(247x414)로 저장한 것이다.
        // 문틀 폭(약 2.86유닛)이 넓어서 기본 안쪽 오프셋(0.9)을 쓰면 왼쪽 끝이 엘리 대장간 처마(오른쪽 끝 약 12.3)에 걸리고
        // 맵 오른쪽 끝엔 빈 칸이 남았다. 트리거 자리(맵 끝 0.5 안쪽)에 그대로 세워 배경 아치 안에 들어가게 하고,
        // 돌계단 아랫단이 바닥선 위에 떠 보이지 않게 살짝(0.1) 묻는다.
        private static readonly FramedPortalStyle VillagePortalStyle = new FramedPortalStyle(
            "Art/Props/VillagePortal/village_portal_base", "Art/Props/VillagePortal/Swirl", new Vector2(543f, 1246f),
            inwardOffset: 0f, sink: 0.1f);

        // 잊혀진 폐허 포탈: 담쟁이 덮인 고딕 유적 아치(고딕 유적 포털 아치 프레임, 1086x1448) 한 장을 고정으로 세우고, 아치 안쪽에
        // 갈색/금빛 소용돌이(황금빛 마법 소용돌이 포털 스프라이트 시트, 2x2) 4장을 번갈아 쓰며 22.5도씩 돌린 16프레임을 돌린다.
        // 아치 아래가 뚫려 있어 구멍을 픽셀로 따라가면 담쟁이 틈으로 새서, 돌 아치 안쪽(기둥 사이 x 300~790, 꼭짓점 (545,445)인
        // 뾰족 아치)을 도형으로 그려 잘랐다. 소용돌이 프레임은 문틀 그림 (290,430)~(800,1440) 칸을 절반 해상도(255x505)로 저장한 것이다.
        private static readonly FramedPortalStyle RuinsPortalStyle = new FramedPortalStyle(
            "Art/Props/RuinsPortal/ruins_portal_base", "Art/Props/RuinsPortal/Swirl", new Vector2(545f, 1440f));

        // 광활한 평야 포탈: 이끼와 담쟁이·들꽃이 자란 낡은 돌 아치(마젠타 배경의 고대 수정 관문, 1159x1356 — 풀·잎이 많아 자홍 배경으로
        // 뽑았다) 한 장을 고정으로 세우고, 아치 안쪽에 에메랄드·금빛 잎사귀 소용돌이(에메랄드 황금 소용돌이 포털 스프라이트 시트, 4장)를
        // 번갈아 쓰며 22.5도씩 돌려 아치 구멍 모양으로 자른 16프레임을 돌린다. 구멍은 문틀 그림 (295,220)~(851,1190)이고, 소용돌이 프레임은
        // 그 칸을 절반 해상도(278x485)로 저장했다. 그림 맨 아래 약 26px이 비어 있어 그만큼(약 0.07) 묻는다.
        private static readonly FramedPortalStyle PlainsPortalStyle = new FramedPortalStyle(
            "Art/Props/PlainsPortal/plains_portal_base", "Art/Props/PlainsPortal/Swirl", new Vector2(573f, 1190f), sink: 0.07f);

        // 어둠의 숲 포탈: 가시덩굴·사슬이 감기고 붉은 수정이 박힌 비틀린 고목 두 그루가 맞닿은 아치(고딕 판타지 수정 포털 아치,
        // 1159x1358, 자홍 배경) 한 장을 고정으로 세우고, 붉은·보랏빛 저주 소용돌이(네 개의 마법 소용돌이 포털, 4장)를 번갈아 쓰며
        // 22.5도씩 돌려 아치 구멍 모양으로 자른 16프레임을 돌린다. 구멍은 (280,196)~(852,1181), 소용돌이 프레임은 절반 해상도(286x492).
        private static readonly FramedPortalStyle ForestPortalStyle = new FramedPortalStyle(
            "Art/Props/ForestPortal/forest_portal_base", "Art/Props/ForestPortal/Swirl", new Vector2(566f, 1181f), sink: 0.08f);

        // 이시스의 연습장 포탈: 쇠띠를 두른 나무 아치 위에 엇갈린 연습용 검과 보라 수정 문장, 양옆에 깃발·촛대·방패·허수아비가 선
        // 실내 문(고딕 판타지 검의 문, 1166x1349, 자홍 배경). 소용돌이는 처음 마을 출구와 같은 보랏빛 소용돌이 원본(4장)을 이 문의
        // 구멍 모양으로 다시 잘랐다. 구멍은 (291,272)~(875,1230), 소용돌이 프레임은 절반 해상도(292x479).
        private static readonly FramedPortalStyle PracticePortalStyle = new FramedPortalStyle(
            "Art/Props/PracticePortal/practice_portal_base", "Art/Props/PracticePortal/Swirl", new Vector2(583f, 1230f), sink: 0.05f);

        // 푸른 바다 포탈: 청록 수정이 박힌 덩굴 석조 아치(청록 수정과 덩굴이 어우러진 고대 석조 포탈, 1086x1448) 한 장을 고정으로 세우고,
        // 아치 안쪽에 물 소용돌이(마법의 물 소용돌이 포털 4종) 4장을 번갈아 쓰며 22.5도씩 돌린 16프레임을 돌린다. 폐허 포탈처럼 아래가
        // 뚫린 아치라 돌 아치 안쪽(기둥 사이 x 298~788, 꼭짓점 (543,360)인 뾰족 아치)을 도형으로 그려 잘랐다.
        // 소용돌이 프레임은 문틀 그림 (290,350)~(796,1426) 칸을 절반 해상도(253x538)로 저장한 것이다.
        // 그림 맨 아래 22px(바위 받침 아래)가 비어 있어 그만큼(약 0.06) 묻어야 바위가 바닥에 닿는다.
        private static readonly FramedPortalStyle SeaPortalStyle = new FramedPortalStyle(
            "Art/Props/SeaPortal/sea_portal_base", "Art/Props/SeaPortal/Swirl", new Vector2(543f, 1426f), sink: 0.06f);

        // 천상의 신전: 몬스터도 StageManager도 없는 안전 지역. 구름 위에 떠 있는 대리석 다리 광장 한가운데에 이리아의 성소 건물이
        // 서 있고, 입구에서 [Z]를 누르면 성소 안으로 들어간다. 왼쪽 끝 포탈은 월드맵을 띄운다.
        // 예전에는 천상으로 향하는 계단(한 장짜리 배경 통로 → 발판 등반 맵)을 지나 신전에 들어갔지만, 계단을 없애고 신전 한 맵으로 합쳤다.
        // 배경은 시차 배경(HeavenTempleParallaxLayers).
        private static void BuildTemple(bool enteringForward)
        {
            BuildMap(stageRoot.transform, TempleWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                null, 0.85f);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;
            SpawnParallax(HeavenTempleParallaxLayers(), xMin, xMax, yMin);

            player.SetCombatLocked(true);
            if (templeSpawnAtSanctuary)
            {
                // 성소에서 막 나왔으면 입구 바로 앞에 세운다(입구는 포탈이 아니라 트리거라 거리 제약이 없다).
                templeSpawnAtSanctuary = false;
                PlacePlayerAtSpawn(SanctuaryX, yMin);
            }
            else
            {
                // 왼쪽 끝 포탈 트리거(반경 2)에 걸리지 않을 만큼 떨어진 곳에 세운다.
                PlacePlayerAtSpawn(xMin + 4f, yMin);
            }
            FitCamera(xMin, xMax, yMin);

            // 왼쪽 끝 포탈은 마을 출구처럼 월드맵을 띄운다. 취소하면 포탈이 다시 발동하도록 풀어준다.
            var backPortal = SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true, framed: HeavenPortalStyle);
            backPortal.SetCustomTrigger(() => worldMapUI.Show(backPortal));

            SpawnSanctuaryBuilding(stageRoot.transform, yMin);
            SpawnTempleFloatingIslands(stageRoot.transform, yMin);
            SpawnTempleDecor(stageRoot.transform, yMin);

            Debug.Log("Aethoria: 천상의 신전 진입");
        }

        // 신전 뒤 하늘에 떠 있는 폭포 섬(heaven_island, 407x353 · PPU 130). 꽃 회랑(-12) 뒤, 먼 성도(-14) 앞(-13)에
        // 옅게 깔아 거리감을 준다. 세상에 붙어 있어 회랑보다 빨리 지나가지만, 작고 옅어서 멀리 있는 것처럼 보인다.
        private const string HeavenPropFolder = "Art/Props/Heaven/";
        private static readonly Color TempleIslandTint = new Color(0.85f, 0.86f, 0.95f, 0.9f);

        private static void SpawnTempleFloatingIslands(Transform parent, int floorY)
        {
            SpawnFloatingIsland(parent, -11f, floorY + 3.6f, 0.75f);
            SpawnFloatingIsland(parent, 14.5f, floorY + 4.2f, 0.6f);
        }

        // bottomY = 섬 그림 밑끝(폭포 끝)의 높이.
        private static void SpawnFloatingIsland(Transform parent, float x, float bottomY, float scale)
        {
            var sprite = Resources.Load<Sprite>(HeavenPropFolder + "heaven_island");
            if (sprite == null) return;
            var go = new GameObject("TempleFloatingIsland", typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(x, bottomY, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -13;
            sr.color = TempleIslandTint;
        }

        // 광장 소품(Backgrounds/Parallax/Heaven/Props, 1칸 = 135px, 아래 가운데 기준). 맵 x -20 ~ 19:
        //  -19.5 월드맵 포탈 | -16 입장 지점 | -13.5 떠 있는 수정 제단 | -9.5 가로등 | -3.5 성화 화로 | -0.6 천사상
        //  | 0.5~7.5 성소 건물 | 8.6 천사상 | 11.5 성화 화로 | 16 가로등(등불이 안쪽을 보게 좌우 반전).
        // 모두 바닥 띠(-9) 앞, 캐릭터 뒤(-8). 밑동은 대리석 윗면에 살짝(0.1) 묻는다. 성소 위로는 빛줄기가 천천히 일렁인다.
        private const string TemplePropFolder = HeavenParallaxFolder + "Props/";
        private const float TemplePropSink = 0.1f;

        private static void SpawnTempleDecor(Transform parent, int floorY)
        {
            // 천사상(648x1498 → 키 약 3.4칸, 캐릭터의 약 2배) 한 쌍이 성소 양옆을 지킨다.
            SpawnTempleProp(parent, "heaven_angel", -0.6f, floorY, 3.4f, false);
            SpawnTempleProp(parent, "heaven_angel", 8.6f, floorY, 3.4f, true);
            // 가로등(542x1702 → 키 약 3.6칸). 등불이 오른쪽에 달린 그림이라 오른쪽 가로등은 뒤집어 광장 안쪽을 비추게 한다.
            SpawnTempleProp(parent, "heaven_lamp", -9.5f, floorY, 3.6f, false);
            SpawnTempleProp(parent, "heaven_lamp", 16f, floorY, 3.6f, true);
            // 성화 화로: 불꽃만 흔들리는 6프레임(282x684 → 키 약 2.3칸).
            SpawnTempleBrazier(parent, -3.5f, floorY);
            SpawnTempleBrazier(parent, 11.5f, floorY);
            SpawnTempleObelisk(parent, -13.5f, floorY);
            SpawnTempleLightRays(parent, SanctuaryX, floorY);
        }

        private static SpriteRenderer SpawnTempleProp(Transform parent, string name, float x, float bottomY, float height, bool flip,
            int sortingOrder = -8)
        {
            var sprite = Resources.Load<Sprite>(TemplePropFolder + name);
            if (sprite == null) return null;
            var go = new GameObject("Temple_" + name, typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(x, bottomY - TemplePropSink, 0f);
            float scale = height / sprite.bounds.size.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = flip;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        private const float TempleBrazierHeight = 2.3f;

        private static void SpawnTempleBrazier(Transform parent, float x, int floorY)
        {
            var brazier = FlipbookSpriteEffect.Create("TempleBrazier", TemplePropFolder + "Brazier",
                new Vector2(x, floorY - TemplePropSink), 0.1f, loop: true, sortingOrder: -8);
            if (brazier == null) return;
            brazier.transform.SetParent(parent, true);
            float scale = TempleBrazierHeight / (684f / 135f);
            brazier.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // 떠 있는 수정 제단: 제단(661x437)은 바닥에 서 있고, 그 위 수정(661x971, 밑끝이 제단 윗면)만 천천히 떠오르내린다.
        // 둘을 합친 키가 약 3.6칸이 되게 한다.
        private static void SpawnTempleObelisk(Transform parent, float x, int floorY)
        {
            const float scale = 3.6f / ((437f + 971f) / 135f);
            float baseHeight = 437f / 135f * scale;
            SpawnTempleProp(parent, "heaven_obelisk_base", x, floorY, baseHeight, false);
            var crystal = SpawnTempleProp(parent, "heaven_obelisk_crystal", x, floorY + baseHeight + 0.05f, 971f / 135f * scale, false, -7);
            if (crystal != null) crystal.gameObject.AddComponent<FloatingBob>();
        }

        // 빛줄기(1774x887, 검정 배경을 지워 밝기만큼 비치게 함): 성소 위 화면 꼭대기에서 비스듬히 내려온다.
        // 성소·천사상보다 앞(-7), 캐릭터보다 뒤.
        private static void SpawnTempleLightRays(Transform parent, float x, int floorY)
        {
            var sr = SpawnTempleProp(parent, "heaven_lightrays", x, floorY + 1.6f + TemplePropSink, 5f, false, -7);
            if (sr == null) return;
            sr.color = new Color(1f, 0.97f, 0.92f, 0.5f);
            sr.gameObject.AddComponent<AlphaPulse>();
            // 카메라가 떠 있는 섬 쪽으로 올라가도 빛 윗변이 보이지 않게 늘 화면 위끝에 매단다.
            sr.gameObject.AddComponent<ScreenTopAnchor>();
        }

        // 이리아의 성소 건물: 광장 가운데에서 조금 오른쪽(입장 지점에서 걸어오면 화면 가운데로 들어오는 자리)에 세운다.
        // 바닥 위에 바로 붙이면 캐릭터와 같은 줄에 붙어 보여서, 바닥 너머 하늘에 떠 있는 섬처럼 살짝(0.4) 띄워 뒤로 물린다.
        // 꽃 회랑(-12)보다 앞, 낮은 구름(-10)·바닥 띠(-9)보다 뒤(-11)라 건물 밑 얼음 조각은 바닥 띠 뒤로 숨는다.
        private const float SanctuaryX = 4f;
        private const float SanctuaryWidthOnMap = 7f;
        private const float SanctuaryRaise = 0.4f;
        // 원화(1536x1024) 속에서 떠 있는 발판의 평평한 바닥면(계단이 시작되는 줄)이 위에서부터 약 85% 지점.
        // 그 아래로 늘어진 얼음 조각들은 바닥 띠 뒤로 숨는다.
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
                renderer.sortingOrder = -11;

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
            iriaGO.AddComponent<IriaNpc>().Initialize(player.transform, npcDialogueUI, blessingUI, questManager);
        }

        // 배경(Interior of Iria's Sanctuary, 1672x941) 속 대리석 바닥이 위에서부터 약 84% 지점이라 그 줄을 바닥으로 삼는다.
        private const float SanctuaryInteriorGroundFraction = 0.80f;

        private static void BuildSanctuary()
        {
            BuildMap(stageRoot.transform, SanctuaryWidth, MapHeight, out int xMin, out int xMax, out int yMin, out int yMax,
                "Backgrounds/IriaSanctuaryInterior", SanctuaryInteriorGroundFraction);
            mapXMin = xMin; mapXMax = xMax; mapYMin = yMin; mapYMax = yMax;

            player.SetCombatLocked(true);
            // 왼쪽 끝 귀환 포탈 트리거(반경 2)에 걸리지 않을 만큼 떨어진 곳에 세운다(엔스룸/연습장과 같은 이유).
            PlacePlayerAtSpawn(xMin + 4f, yMin);
            FitCamera(xMin, xMax, yMin);

            var backPortal = SpawnPortal(stageRoot.transform, xMin + 0.5f, yMin, backward: true, framed: HeavenPortalStyle);
            backPortal.SetCustomTrigger(LeaveSanctuary);

            SpawnIria(stageRoot.transform, yMin, SanctuaryIriaX);
            SpawnSanctuaryDecor(stageRoot.transform, yMin);

            Debug.Log("Aethoria: 이리아의 성소 진입");
        }

        // 성소 내부 장식(Art/Props/Sanctuary, 아래 가운데 기준, 그림이 없으면 그 장식만 건너뛴다). 맵 x -10 ~ 10:
        // 귀환 포탈 -9.5, 입장 지점 -6, 이리아 0. 이리아 양옆에 촛대, 그 바깥에 수정 구슬 받침, 더 바깥에 꽃 화분,
        // 위쪽에 깃발(바닥에서 띄워 매단다), 화면 양 끝 맨 앞에 백합 덤불(캐릭터보다 앞이라 끝에만 둔다).
        private const string SanctuaryPropFolder = "Art/Props/Sanctuary/";

        private static void SpawnSanctuaryDecor(Transform parent, int floorY)
        {
            SpawnSanctuaryProp(parent, "sanctuary_banner", -7.8f, floorY + 3.1f, 3.4f, false, -9);
            SpawnSanctuaryProp(parent, "sanctuary_banner", 7.8f, floorY + 3.1f, 3.4f, true, -9);

            SpawnSanctuaryProp(parent, "sanctuary_flower_urn", -6.7f, floorY, 1.7f, false, -8);
            SpawnSanctuaryProp(parent, "sanctuary_flower_urn", 6.7f, floorY, 1.7f, true, -8);

            SpawnSanctuaryProp(parent, "sanctuary_orb_pedestal", -4.8f, floorY, 1.5f, false, -8);
            SpawnSanctuaryProp(parent, "sanctuary_orb_pedestal", 4.8f, floorY, 1.5f, true, -8);
            // 받침 위 수정 구슬은 따로 떠서 위아래로 흔들린다.
            foreach (float x in new[] { -4.8f, 4.8f })
            {
                var orb = SpawnSanctuaryProp(parent, "sanctuary_orb", x, floorY + 1.6f, 0.75f, false, -7);
                if (orb != null) orb.gameObject.AddComponent<FloatingBob>();
            }

            SpawnSanctuaryProp(parent, "sanctuary_candelabra", -2.6f, floorY, 2.5f, false, -8);
            SpawnSanctuaryProp(parent, "sanctuary_candelabra", 2.6f, floorY, 2.5f, true, -8);

            SpawnSanctuaryProp(parent, "sanctuary_lily_fg", -10.2f, floorY - 1.55f, 1.8f, false, 40);
            SpawnSanctuaryProp(parent, "sanctuary_lily_fg", 10.2f, floorY - 1.55f, 1.8f, true, 40);
        }

        private static SpriteRenderer SpawnSanctuaryProp(Transform parent, string name, float x, float bottomY, float height, bool flip, int sortingOrder)
        {
            var sprite = Resources.Load<Sprite>(SanctuaryPropFolder + name);
            if (sprite == null) return null;
            var go = new GameObject("Sanctuary_" + name, typeof(SpriteRenderer));
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(x, bottomY - TemplePropSink, 0f);
            float scale = height / sprite.bounds.size.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.flipX = flip;
            sr.sortingOrder = sortingOrder;
            return sr;
        }
    }
}
