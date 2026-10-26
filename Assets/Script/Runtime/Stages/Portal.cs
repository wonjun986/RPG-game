using UnityEngine;
using Aethoria.Bootstrap;
using Aethoria.Characters;

namespace Aethoria.Stages
{
    // 맵 가장자리의 포탈. 보라색 소용돌이 포탈 스프라이트가 반복 재생되고, 플레이어가 일정 거리 안에
    // 들어오면 자동으로 다음(또는 이전) 스테이지(맵)로 넘어간다.
    // 되돌아가는 포탈은 입장 지점 바로 근처에 놓이는데, 플레이어가 스폰되자마자 트리거 범위 안에
    // 있으면 그대로 즉시 발동해버린다. 스폰 시점에 이미 범위 안이었다면, 한 번 벗어났다가 다시
    // 들어와야만 실제로 작동하게 해서 이 문제를 막는다.
    [RequireComponent(typeof(CircleCollider2D))]
    public class Portal : MonoBehaviour
    {
        private bool triggered;
        private bool backward;
        private bool needsExitFirst;
        private System.Action customTrigger;

        private const string FramesPath = "Art/Props/Portal";
        private const float FrameDuration = 0.12f;

        private SpriteRenderer visualRenderer;
        private Transform framedRoot;
        private SpriteRenderer frameRenderer;
        private SpriteRenderer floorGlow;
        private Color floorGlowColor;
        private static Sprite softEllipse;
        private Sprite[] frames;
        private int frameIndex;
        private float frameTimer;

        // 포탈 그림을 자식 오브젝트로 붙인다. 포탈은 맵 끝에서 0.5만큼 안쪽에 놓이므로 그림 중심을
        // 그대로 두면 절반 가까이 화면 밖으로 잘린다. inwardOffset만큼 맵 안쪽으로 밀어서 그린다.
        // floorOffset은 포탈 오브젝트 위치에서 바닥까지의 높이 차(스프라이트 피벗이 발판 높이에 있음).
        // framesPath: 지역마다 다른 포탈 그림을 쓰고 싶으면 넘긴다(예: 엔스룸 전용 금빛 포탈). 안 주면 기본 포탈.
        public void CreateVisual(float inwardOffset, float floorOffset, string framesPath = FramesPath)
        {
            frames = Resources.LoadAll<Sprite>(framesPath);
            if (frames == null || frames.Length == 0) return;
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));

            var go = new GameObject("PortalVisual");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(backward ? inwardOffset : -inwardOffset, -floorOffset, 0f);

            visualRenderer = go.AddComponent<SpriteRenderer>();
            visualRenderer.sprite = frames[0];
            visualRenderer.sortingOrder = -5; // 배경보다 앞, 캐릭터/몬스터보다 뒤
        }

        // 테두리(조각/덩굴)는 고정이고 안쪽 거울만 소용돌이치는 포탈용. 장식 전체를 매번 통째로
        // 갈아 끼우면 프레임마다 테두리 그림이 미세하게 달라서 전체가 흔들려 보였다(엔스룸 포탈에서 발견).
        // basePath의 그림 한 장은 고정으로 깔고, overlayFramesPath의 그림들(테두리와 같은 캔버스 크기,
        // 거울 부분만 그려져 있고 나머지는 투명)만 애니메이션으로 그 위에 겹친다. 두 그림이 같은 크기/
        // 피벗이면 좌표 계산 없이 같은 자리에 겹치기만 해도 딱 맞는다.
        // 거울 프레임마다 임포트 트리밍/피벗이 미세하게(혹은 어떨 땐 크게) 어긋나는 문제를 몇 번
        // 다르게 고쳐 봤는데도 계속 밀리거나 아예 위치가 틀어졌다. 이 프로젝트에서 임포트 설정이
        // 믿을 수 없게 바뀌는 걸 반복해서 겪어서, 더 이상 여러 프레임을 갈아 끼우지 않고 한 장만
        // 고정해서 쓴다 — 애니메이션은 포기하지만 적어도 움직이거나 어긋나 보이지 않는다.
        public void CreateLayeredVisual(float inwardOffset, float floorOffset, string basePath, string overlayFramesPath)
        {
            var baseSprite = Resources.Load<Sprite>(basePath);
            if (baseSprite == null) return;

            var go = new GameObject("PortalVisual");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(backward ? inwardOffset : -inwardOffset, -floorOffset, 0f);

            var overlayFrames = Resources.LoadAll<Sprite>(overlayFramesPath);
            if (overlayFrames != null && overlayFrames.Length > 0)
            {
                System.Array.Sort(overlayFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

                var overlayGO = new GameObject("Swirl");
                overlayGO.transform.SetParent(go.transform, false);
                var overlayRenderer = overlayGO.AddComponent<SpriteRenderer>();
                overlayRenderer.sprite = overlayFrames[overlayFrames.Length / 2];
                overlayRenderer.sortingOrder = -5;
            }

            // 테두리는 거울보다 앞에 그린다(테두리 안쪽 타원은 이미 투명하게 뚫려 있어서 거울이
            // 정상적으로 비쳐 보인다).
            var baseGO = new GameObject("Base");
            baseGO.transform.SetParent(go.transform, false);
            var baseRenderer = baseGO.AddComponent<SpriteRenderer>();
            baseRenderer.sprite = baseSprite;
            baseRenderer.sortingOrder = -4;
        }

        // 테두리(문틀) 한 장 + 그 구멍 안에서 도는 소용돌이 프레임들(천상의 계단 포탈). 엔스룸처럼 프레임을 테두리와 같은
        // 캔버스로 만들면 너무 커서, 소용돌이는 구멍 크기만 한 작은 캔버스(모든 프레임이 같은 크기)로 따로 만들어 두고
        // 테두리 그림 픽셀 좌표로 자리를 계산해 겹친다. 프레임 크기가 전부 같으니 갈아 끼워도 위치가 흔들리지 않는다.
        // swirlBottomCenterPx: 소용돌이 캔버스 아래 가운데가 테두리 그림(왼쪽 위 원점)의 어디에 오는지.
        // swirlPixelScale: 소용돌이 프레임을 몇 배로 늘려 그리는지(용량을 줄이려고 절반 해상도로 저장했다면 2).
        public void CreateFramedVisual(float inwardOffset, float floorOffset, string basePath, string swirlFolder,
            Vector2 swirlBottomCenterPx, float swirlPixelScale, float swirlFrameDuration)
        {
            var baseSprite = Resources.Load<Sprite>(basePath);
            if (baseSprite == null) return;

            var go = new GameObject("PortalVisual");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(backward ? inwardOffset : -inwardOffset, -floorOffset, 0f);

            var baseGO = new GameObject("Frame");
            baseGO.transform.SetParent(go.transform, false);
            var baseRenderer = baseGO.AddComponent<SpriteRenderer>();
            baseRenderer.sprite = baseSprite;
            baseRenderer.sortingOrder = -4;
            framedRoot = go.transform;
            frameRenderer = baseRenderer;

            var swirlFrames = Resources.LoadAll<Sprite>(swirlFolder);
            if (swirlFrames == null || swirlFrames.Length == 0) return;
            System.Array.Sort(swirlFrames, (a, b) => string.CompareOrdinal(a.name, b.name));

            // 두 그림 모두 피벗이 아래 가운데라는 전제로, 테두리 픽셀 좌표를 테두리의 실제 PPU로 나눠 자리를 잡는다.
            float basePpu = baseSprite.pixelsPerUnit;
            Rect baseRect = baseSprite.rect;
            var swirlGO = new GameObject("Swirl");
            swirlGO.transform.SetParent(go.transform, false);
            swirlGO.transform.localPosition = new Vector3(
                (swirlBottomCenterPx.x - baseRect.width / 2f) / basePpu,
                (baseRect.height - swirlBottomCenterPx.y) / basePpu, 0f);
            float swirlScale = swirlPixelScale * swirlFrames[0].pixelsPerUnit / basePpu;
            swirlGO.transform.localScale = new Vector3(swirlScale, swirlScale, 1f);

            visualRenderer = swirlGO.AddComponent<SpriteRenderer>();
            visualRenderer.sprite = swirlFrames[0];
            visualRenderer.sortingOrder = -5; // 테두리 뒤
            frames = swirlFrames;
            frameDuration = swirlFrameDuration;
        }

        // 문틀 포탈을 배경에 녹인다. 밝은 문틀을 배경 조명 색으로 살짝 눌러 주고, 발밑에 그림자를, 앞쪽 바닥과 뒤쪽 벽에
        // 소용돌이 빛이 번진 듯한 옅은 빛을 깐다(그림을 그냥 얹으면 배경과 따로 노는 스티커처럼 보였다).
        // groundLocalY: 포탈 오브젝트 기준 바닥 높이. CreateFramedVisual 다음에 부른다.
        public void BlendIntoScene(Color frameTint, Color glowColor, float groundLocalY)
        {
            if (framedRoot == null) return;
            frameRenderer.color = frameTint;
            float width = frameRenderer.sprite.bounds.size.x;
            float height = frameRenderer.sprite.bounds.size.y;
            float x = framedRoot.localPosition.x;

            CreateSoftEllipse("Shadow", new Vector2(x, groundLocalY + 0.02f), new Vector2(width * 1.15f, 0.5f),
                new Color(0.03f, 0f, 0.06f, 0.6f), -6);
            CreateSoftEllipse("WallGlow", new Vector2(x, groundLocalY + height * 0.5f), new Vector2(width * 1.6f, height * 1.1f),
                new Color(glowColor.r, glowColor.g, glowColor.b, glowColor.a * 0.5f), -7);
            floorGlowColor = glowColor;
            floorGlow = CreateSoftEllipse("FloorGlow", new Vector2(x, groundLocalY - 0.1f), new Vector2(width * 1.7f, 0.9f), glowColor, -3);
        }

        private SpriteRenderer CreateSoftEllipse(string name, Vector2 localPosition, Vector2 size, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = SoftEllipseSprite();
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        // 가운데가 진하고 가장자리로 갈수록 부드럽게 사라지는 원(지름 1유닛). 늘려서 타원으로 쓴다.
        private static Sprite SoftEllipseSprite()
        {
            if (softEllipse != null) return softEllipse;
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float center = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
                    float a = Mathf.Clamp01(1f - d * d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            softEllipse = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return softEllipse;
        }

        private float frameDuration = FrameDuration;

        private void Update()
        {
            if (floorGlow != null)
            {
                // 소용돌이 빛이 바닥에서 천천히 일렁인다.
                float pulse = 0.8f + 0.2f * Mathf.Sin(Time.time * 2.2f);
                floorGlow.color = new Color(floorGlowColor.r, floorGlowColor.g, floorGlowColor.b, floorGlowColor.a * pulse);
            }
            if (visualRenderer == null || frames == null || frames.Length < 2) return;

            frameTimer += Time.deltaTime;
            if (frameTimer < frameDuration) return;
            frameTimer -= frameDuration;

            frameIndex = (frameIndex + 1) % frames.Length;
            visualRenderer.sprite = frames[frameIndex];
        }

        public void Configure(bool isBackward, Transform playerTransform)
        {
            backward = isBackward;

            // Physics2D.OverlapCircleAll 등 물리 엔진 질의는, 플레이어가 스폰 스크립트로 방금
            // 옮겨진 위치가 그 시점까지 물리 엔진에 동기화되었는지(SyncTransforms 호출 여부,
            // 그 프레임이 FixedUpdate/Update 중 어디쯤인지 등)에 따라 결과가 달라질 수 있어서
            // 신뢰할 수 없었다. 대신 이미 알고 있는 플레이어 트랜스폼과의 순수 거리 계산으로
            // 판정하면 타이밍에 상관없이 항상 정확하다.
            if (playerTransform != null)
            {
                var col = GetComponent<CircleCollider2D>();
                float distance = Vector2.Distance(transform.position, playerTransform.position);
                if (distance <= col.radius)
                {
                    needsExitFirst = true;
                }
            }
        }

        // 월드맵처럼 밟는다고 바로 스테이지를 옮기지 않고 선택 UI만 띄우는 포탈에 쓴다. 설정해두면
        // OnTriggerEnter2D가 기본 이전/다음 스테이지 이동 대신 이 콜백을 대신 호출한다.
        public void SetCustomTrigger(System.Action onEnter)
        {
            customTrigger = onEnter;
        }

        // UI에서 취소하고 그대로 있기로 했을 때 호출한다. 다시 밟으면 발동하도록 풀어주되,
        // 아직 범위 안에 서 있는 채로 즉시 재발동하지 않도록 한 번 벗어났다 들어와야 하게 한다.
        public void ResetTrigger()
        {
            triggered = false;
            needsExitFirst = true;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (needsExitFirst && other.GetComponent<Character>() != null)
            {
                needsExitFirst = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (triggered || needsExitFirst) return;

            var character = other.GetComponent<Character>();
            if (character == null || character.IsDead) return;

            triggered = true;

            if (customTrigger != null) customTrigger();
            else if (backward) GameBootstrap.EnterPreviousStage();
            else GameBootstrap.EnterNextStage();
        }
    }
}
