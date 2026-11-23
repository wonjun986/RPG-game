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
    // 맵 공통 부품: 소품/발판, 맵 틀/배경/벽, 카메라 맞추기, 포탈.
    public static partial class GameBootstrap
    {
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

        private static void PlacePlayerAtSpawn(float spawnX, float floorY)
        {
            // 비밀의 장소에서 돌아올 때는 맵 끝 대신 숨겨진 포탈이 있던 자리에 선다(한 번만).
            if (pendingSpawnPoint.HasValue)
            {
                spawnX = pendingSpawnPoint.Value.x;
                floorY = pendingSpawnPoint.Value.y;
                pendingSpawnPoint = null;
            }
            player.transform.position = new Vector3(spawnX, floorY, 0f);
            var body = player.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;
        }

        private static void BuildMap(Transform parent, int width, int height, out int xMin, out int xMax, out int yMin, out int yMax,
            string backgroundResourcePath, float groundFraction, float backgroundScale = 0f, bool skyExtension = true)
        {
            xMin = -width / 2;
            xMax = xMin + width - 1;
            yMin = -height / 2;
            yMax = yMin + height - 1;

            BuildMapBackground(parent, xMin, xMax, yMin, backgroundResourcePath, groundFraction, backgroundScale, skyExtension: skyExtension);
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
            string backgroundResourcePath, float groundFraction, float fixedScale = 0f, int sortingOrder = -10, bool skyExtension = true)
        {
            bool isMainBackground = sortingOrder == -10;
            if (isMainBackground) backgroundTopAboveFloor = 0f;
            if (string.IsNullOrEmpty(backgroundResourcePath)) return; // 시차 배경(SpawnParallax)을 쓰는 맵
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
            // 메인 배경은 그림 위끝 몇 줄을 위로 길게 늘린 하늘을 덧대서, 이단 점프로 카메라가 올라가도 맵이 잘려 보이지 않게 한다.
            Sprite skySprite = isMainBackground && skyExtension ? CreateSkyExtensionSprite(sprite) : null;
            if (isMainBackground)
                backgroundTopAboveFloor = scaledHeight * groundFraction + (skySprite != null ? SkyExtensionHeight : 0f);

            // 가운데 한 장 + 맵 끝까지 덮는 데 필요한 만큼 양옆으로(홀수 번째는 좌우 반전).
            int sideTiles = Mathf.Max(0, Mathf.CeilToInt((width / scaledWidth - 1f) / 2f));
            for (int i = -sideTiles; i <= sideTiles; i++)
            {
                if (skySprite != null)
                {
                    var sky = new GameObject("MapBackgroundSky", typeof(SpriteRenderer));
                    sky.transform.SetParent(parent);
                    var skyRenderer = sky.GetComponent<SpriteRenderer>();
                    skyRenderer.sprite = skySprite;
                    skyRenderer.sortingOrder = sortingOrder;
                    skyRenderer.flipX = (i & 1) != 0;
                    // 이음매가 비치지 않게 그림 위끝에 살짝 겹쳐 세운다.
                    sky.transform.localScale = new Vector3(scale, SkyExtensionHeight / skySprite.bounds.size.y, 1f);
                    sky.transform.position = new Vector3(centerX + i * scaledWidth, centerY + scaledHeight / 2f - 0.02f, 0f);
                }

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

        // 배경 위로 덧대는 하늘 높이(유닛). 이단 점프 꼭대기에서도 화면 위가 빈 배경색으로 드러나지 않을 만큼.
        private const float SkyExtensionHeight = 8f;

        // 배경 그림 맨 위 줄(가장자리 1줄은 빼고 그 아래 2줄)만 잘라 아래 가운데 피벗 스프라이트로 만든다. 세로로 늘리면 그 줄의 색이
        // 그대로 위로 이어져 하늘이 길어진 것처럼 보인다. 맨 끝 줄을 피하는 건 필터링이 반대편(바닥) 줄을 끌어오지 않게 하려는 것.
        private static Sprite CreateSkyExtensionSprite(Sprite source)
        {
            Rect rect = source.rect;
            if (source.texture == null || rect.height < 8f) return null;
            var strip = new Rect(rect.x, rect.yMax - 3f, rect.width, 2f);
            return Sprite.Create(source.texture, strip, new Vector2(0.5f, 0f), source.pixelsPerUnit);
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
        // 지금 맵의 배경 그림이 바닥 위로 얼마나 높이까지 덮는지(BuildMapBackground가 기록). 카메라가 올라갈 수 있는 한계.
        private static float backgroundTopAboveFloor;
        private const float CameraRiseMargin = 0.2f;

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
            // 이단 점프나 높은 발판 위에서도 플레이어가 화면 밖으로 나가지 않게, 모든 맵에서 카메라가 위로 따라 올라간다.
            // 배경 그림 위끝을 넘어 빈 하늘(배경색)이 보이지 않을 만큼까지만 오른다(화면 위끝 = 바닥 + 크기 x 1.4).
            float viewTopAboveFloor = orthoSize * 1.4f;
            maxRise = Mathf.Max(maxRise, backgroundTopAboveFloor - viewTopAboveFloor - CameraRiseMargin);
            SceneLighting.Ensure(mainCamera);
            var follow = mainCamera.GetComponent<CameraFollow>();
            if (follow == null) follow = mainCamera.gameObject.AddComponent<CameraFollow>();
            follow.Follow(player.transform, xMin, xMax, cameraY, floorY, maxRise);
        }

        private const float PortalTriggerRadius = 2f;

        // 문틀 한 장 + 그 구멍 안에서 도는 소용돌이 프레임으로 그리는 포탈의 그림 설정(Portal.CreateFramedVisual 참고).
        // 소용돌이 프레임은 모두 절반 해상도로 저장했고(PixelScale 2), 0.08초마다 넘긴다.
        private sealed class FramedPortalStyle
        {
            public readonly string BasePath;
            public readonly string SwirlFolder;
            public readonly Vector2 SwirlBottomCenterPx;
            public readonly float SwirlPixelScale;
            public readonly float SwirlFrameDuration;
            public readonly float InwardOffset;
            public readonly float Sink;

            public FramedPortalStyle(string basePath, string swirlFolder, Vector2 swirlBottomCenterPx,
                float inwardOffset = PortalVisualInwardOffset, float sink = 0f, float swirlPixelScale = 2f, float swirlFrameDuration = 0.08f)
            {
                BasePath = basePath;
                SwirlFolder = swirlFolder;
                SwirlBottomCenterPx = swirlBottomCenterPx;
                InwardOffset = inwardOffset;
                Sink = sink;
                SwirlPixelScale = swirlPixelScale;
                SwirlFrameDuration = swirlFrameDuration;
            }
        }
        private const float PortalHeightAboveFloor = 0.7f;
        private const float PortalVisualInwardOffset = 0.9f;

        // 던전 보스(알리아스, 보스 슬라임, 황금 모래룡)를 잡으면 스테이지 클리어 창 대신 맵 오른쪽 끝에 포탈이 열린다.
        // 그 포탈로 들어가면 체력/마나를 채우고 마을(노아 앞)로 돌아간다. 루나벨은 처치 연출이 끝나면 바로 마을로 돌아간다.
        private static void OpenBossExitPortal(FramedPortalStyle framed = null)
        {
            if (stageRoot == null) return;
            var portal = SpawnPortal(stageRoot.transform, mapXMax - 0.5f, mapYMin, backward: false, framed: framed);
            portal.SetCustomTrigger(() => ReturnToVillage(spawnAtNoa: true));
        }

        // 지정한 x 위치에 포탈을 놓는다. 포탈 그림이 반복 재생되고, 플레이어가 일정 거리 안에 들어오면
        // 자동으로 다음(또는 backward=true면 이전) 스테이지로 넘어간다. 반환값은 호출부에서
        // SetCustomTrigger로 기본 동작을 다른 걸로 바꿔치기하고 싶을 때(월드맵 등) 쓴다.
        // framesPath: 지역 전용 포탈 그림이 있으면 넘긴다(예: 엔스룸은 금빛 포탈). 안 주면 기본 포탈.
        // overlayFramesPath까지 주면 framesPath는 고정 테두리 한 장, overlayFramesPath는 그 위에서
        // 애니메이션되는 안쪽 거울로 따로 그린다(엔스룸 포탈처럼 테두리가 커서 통째로 갈아 끼우면
        // 떨려 보이는 경우에 쓴다).
        private static Portal SpawnPortal(Transform parent, float x, float floorY, bool backward,
            string framesPath = null, string overlayFramesPath = null, FramedPortalStyle framed = null)
        {
            var go = new GameObject(backward ? "Portal_Back" : "Portal_Forward");
            go.transform.position = new Vector3(x, floorY + PortalHeightAboveFloor, 0f);
            go.transform.SetParent(parent);

            var collider = go.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = PortalTriggerRadius;

            var portal = go.AddComponent<Portal>();
            portal.Configure(backward, player.transform);
            if (framed != null) portal.CreateFramedVisual(framed.InwardOffset, PortalHeightAboveFloor + framed.Sink, framed.BasePath, framed.SwirlFolder,
                framed.SwirlBottomCenterPx, framed.SwirlPixelScale, framed.SwirlFrameDuration);
            else if (overlayFramesPath != null) portal.CreateLayeredVisual(PortalVisualInwardOffset, PortalHeightAboveFloor + EnsroomPortalSink, framesPath, overlayFramesPath);
            else if (framesPath != null) portal.CreateVisual(PortalVisualInwardOffset, PortalHeightAboveFloor, framesPath);
            else portal.CreateVisual(PortalVisualInwardOffset, PortalHeightAboveFloor);
            // 소용돌이 빛이 주변 바닥과 벽을 은은하게 비춘다(URP 2D 포인트 라이트).
            SceneLighting.AddPointLight(go.transform, Vector3.zero, new Color(0.7f, 0.5f, 1f), 3.2f, 0.55f);
            return portal;
        }
    }
}
