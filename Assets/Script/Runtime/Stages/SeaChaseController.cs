using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 푸른 바다 물속 추격전. 카메라는 고정이고 배경(먼 층/중간 층)과 장애물이 왼쪽으로 흘러서 앞으로 달아나는 것처럼 보인다.
    // 물속에 떨어지면 먼저 등장 연출이 나온다: 네크로시아가 가라앉음 → 혼잣말 → 물의 여왕 아쿠아가 나타나 대화 → 추격 시작.
    // 이 연출을 이미 본 세이브는 Esc로 건너뛸 수 있다.
    // 네크로시아는 방향키로 화면 안을 자유롭게 헤엄치고(대시 프레임을 반복 재생), 왼쪽에서 아쿠아가 쫓아온다.
    // 장애물에 부딪히면 아쿠아가 한 칸 가까워지고, 아쿠아는 주기적으로 경고선을 띄운 뒤 그 줄을 따라 돌진한다.
    // 돌진은 창 찌르기와 물의 드릴이 번갈아 나온다. 드릴은 회오리로 변신해 일직선으로 화면 밖까지 날아간 뒤,
    // 화면 양쪽에서 번갈아 네크로시아를 겨냥해 대각선으로 여러 번 다시 날아온다.
    // 아쿠아와의 1차 전투(함정 유도형): 네크로시아는 직접 공격하지 못하고, 아쿠아의 돌진을 흘러오는 바위에 부딪히게 해서 피해를 준다.
    // 창 돌진은 바위에 박히면 그 자리에서 멈춰 피해를 입고 잠깐 정신을 잃는다(바위 뒤에 숨으면 안전하다).
    // 드릴은 지나가는 길의 바위를 부술 때마다 피해를 입는다. 체력을 다 깎으면 아쿠아가 물러나고 마을로 돌아간다.
    // 쓰러지면 DeathReturnUI가 이 맵을 처음부터 다시 짓는다.
    public class SeaChaseController : MonoBehaviour
    {
        public static readonly Color DeepWaterColor = new Color(0.02f, 0.08f, 0.22f);

        private const string FarLayerPath = "Backgrounds/sea_chase_far";
        private const string MidLayerPath = "Backgrounds/sea_chase_mid";
        private const float FarLayerSpeed = 0.6f;
        private const float MidLayerSpeed = 3.2f;
        // 중간 층의 기둥과 산호가 밝아서 그 앞을 지나는 캐릭터가 묻히지 않게 살짝 어둡게 깐다.
        private static readonly Color MidLayerTint = new Color(0.8f, 0.8f, 0.85f);
        // 화면 흔들림은 위쪽으로만 움직이므로 그만큼 배경을 위로 남겨 둔다.
        private const float BackgroundOverscan = 1.1f;

        // 아쿠아 체력: 창이 바위에 박히거나 드릴이 바위를 부술 때마다 1씩 깎인다.
        private const int BossMaxHp = 8;
        private const float BossStunTime = 1.3f;
        private const float BossStunShake = 0.12f;
        private const float BossHurtFlashTime = 0.25f;
        private static readonly Color BossHurtColor = new Color(1f, 0.5f, 0.5f);
        private const float FragmentLife = 0.7f;
        private const int FragmentCount = 6;
        private const float SwimSpeed = 6.5f;
        // 앞뒤로 헤엄치지 않으면 물살에 밀려 아쿠아 쪽으로 점점 빠르게 떠내려간다. 다시 헤엄치면 곧바로 힘을 잃는다.
        private const float DriftMaxSpeed = 2f;
        private const float DriftRampTime = 1.5f;
        private const float DriftRecoverTime = 0.3f;
        private const float SwimFrameRate = 12f;
        private const string SwimFramesPath = "Art/Necrosia/Swim";
        // 대시 프레임(340px 칸 → 1.05유닛)을 임시로 쓰던 때와 같은 픽셀 배율이 되도록 370px 칸을 환산한 값.
        private const float SwimTargetHeight = 1.14f;
        private const float BottomMargin = 1.2f;
        private const float TopMargin = 1.2f;
        private const float LeftMargin = 1f;    // 왼쪽으로 너무 물러나면 아쿠아의 몸에 그대로 부딪힌다
        private const float RightMargin = 1f;
        // Shift = 물속 대시: 누르고 있는 방향(없으면 앞쪽)으로 짧게 치고 나간다. 도는 동안은 장애물/아쿠아에 맞지 않는다.
        private const float DashDistance = 3f;
        private const float DashDuration = 0.2f;
        private const float DashCooldown = 1.5f;
        private const float DashFrameRateScale = 2.5f;  // 대시하는 동안 헤엄 동작을 빠르게 돌린다
        private const float DashGhostInterval = 0.04f;
        private const float DashGhostLife = 0.25f;
        private static readonly Color DashGhostColor = new Color(0.75f, 0.55f, 1f, 0.55f);

        // 물의 여왕 아쿠아. 대기(Idle 8장)와 창 찌르기(Thrust 8장)를 소용돌이 가운데가 캔버스 가운데에 오도록 같은 크기(916x564)로 잘랐다.
        private const string BossFramesPath = "Art/Monsters/Aqua/Idle";
        private const string BossThrustFramesPath = "Art/Monsters/Aqua/Thrust";
        // 물의 드릴: 변신(8장)은 대기와 같은 키에서 시작하고, 회전(8장)은 드릴 끝이 변신 마지막 프레임과 같은 자리에 오게 잘랐다.
        private const string BossDrillTransformFramesPath = "Art/Monsters/Aqua/DrillTransform";
        private const string BossDrillFramesPath = "Art/Monsters/Aqua/Drill";
        // 회전 시트의 앞 세 장은 작게 시작해 커지는 장면이라, 변신 끝 크기와 맞는 뒤쪽만 반복한다.
        private const int DrillLoopFirstFrame = 3;
        private const float BossHeight = 4f;
        private const float BossFrameRate = 8f;
        private const float BossRestOffset = 1.3f;      // 화면 왼쪽 끝 기준 평소 위치(몸 가운데)
        private const float BossStepPerHit = 1.2f;
        private const float BossRetreatPerSecond = 0.1f;
        private const float BossApproachSpeed = 3f;
        private const float BossFollowYSpeed = 1.5f;
        // 캔버스가 뻗은 창까지 담아 넓어서, 판정은 그림 속 몸통 크기(소용돌이 폭 약 3.3, 키 약 3.8)보다 조금 작게 따로 잡는다.
        // 찌르는 동안은 몸 앞으로 뻗은 창(가슴 높이, 끝이 몸 가운데에서 약 3.2)에도 얇은 판정을 둔다.
        private static readonly Vector2 BossBodySize = new Vector2(2.6f, 3.4f);
        private const float BossSpearTip = 3.2f;
        private const float BossSpearHeightOffset = 0.75f;
        private const float BossSpearThickness = 0.6f;
        private const float LungeWarnTime = 0.9f;
        private const float LungeDuration = 0.6f;
        private const float LungeLaneHeight = 1.6f;
        private const float DrillChargeTime = 1.1f;     // 변신하는 동안 경고선이 떠 있다
        private const float DrillDuration = 0.7f;
        private const float DrillLoopCycle = 0.3f;
        private const float DrillLaneHeight = 2f;
        private const float DrillTravelMargin = 6f;     // 드릴 전체가 화면 밖으로 나갈 때까지 달린다
        private const float DrillTipLift = 0.14f;       // 드릴 끝은 캔버스 가운데보다 이만큼 위에 있다
        private const int DrillDiagonalPasses = 3;
        private const float DrillDiagonalWarnTime = 0.6f;
        private const float DrillDiagonalDuration = 0.55f;
        private const float DrillDiagonalStartMargin = 4f;  // 화면 밖 이만큼에서 출발한다(드릴 끝이 보이지 않을 거리)
        private const float DrillDiagonalStartHeight = 3f;  // 화면 가운데에서 위/아래로 이만큼 떨어진 곳에서 출발한다
        // 드릴이 기울어져도 맞도록, 드릴 축을 따라가는 캡슐로 판정한다(뒤쪽 물줄기 끝과 뾰족한 끝은 조금 뺀다).
        private const float DrillCoreBack = 1.8f;
        private const float DrillCoreFront = 2.4f;
        private const float DrillCoreRadius = 0.9f;
        private const float PlayerHitRadius = 0.4f;
        private const float FirstLungeTime = 4f;
        private const float LungeIntervalStart = 5f;
        private const float LungeIntervalEnd = 3.5f;
        private static readonly Color LungeWarnColor = new Color(1f, 0.25f, 0.2f);

        private const string ObstacleFolder = "Art/Props/Sea/";
        private static readonly string[] ObstacleSprites =
        {
            "sea_rock_small", "sea_rock_spike", "sea_rock_tiny", "sea_rock_wedge", "sea_column_1", "sea_column_2",
        };
        private static readonly Color ObstacleTint = new Color(0.55f, 0.65f, 0.8f);
        private const float ObstacleHeight = 1.6f;
        private const float ObstacleSpeed = 6f;
        private const float ObstacleHitboxShrink = 0.7f;
        private const float FirstObstacleTime = 2f;
        private const float ObstacleIntervalStart = 1.3f;
        private const float ObstacleIntervalEnd = 0.75f;

        private const float ObstacleDamageRatio = 0.1f;
        private const float BossDamageRatio = 0.2f;
        private const float DrillDamageRatio = 0.25f;
        private const float HitInvincibleTime = 1.1f;
        private const float HitFlickerInterval = 0.06f;
        private const float HitShove = 1f;
        private const float BossHitShove = 2f;  // 물리면 몸 밖으로 튕겨 나가 연달아 물리지 않게 한다

        private const float FadeInTime = 0.6f;
        private const float FadeOutTime = 0.8f;
        private const float EscapeRetreatTime = 1.2f;

        private const string IntroIdleFramesPath = "Art/Necrosia/Idle";
        private const float IntroIdleFrameRate = 6f;
        private const float IntroIdleTargetHeight = 1.8f;
        // 등장 대화 동안 네크로시아가 서 있는 돌섬. 대화창이 화면 아래를 가려서 해저가 아니라 화면 가운데쯤에 띄운다.
        private const string StartPlatformSprite = "sea_rock_small";
        private const int StartPlatformSurfaceFromTopPx = 38;
        private const float StartPlatformWidth = 3f;
        private const float StartPlatformDrop = 0.5f;   // 화면 가운데보다 이만큼 아래가 밟는 면
        private const float SinkDuration = 1.4f;
        private const float SinkTopMargin = 0.5f;
        private const float BossEntranceDuration = 1f;
        private const float BossEntranceOffset = 3f;    // 대화하는 동안 몸 전체가 보이는 자리(화면 왼쪽 끝 기준)
        private const float ScrollRampTime = 1f;

        private const string NextHint = "   <color=#B98CE8>[Z]</color>";
        private const string SkipHint = "   <color=#B98CE8>[Esc] 스킵</color>";

        private static readonly string[] BeforeAquaLines =
        {
            "...바닥이 통째로 꺼질 줄이야. 여긴 바다 밑인가.",
            "숨이 막히지 않아. 이 물... 보통 물이 아니군.",
        };

        private static readonly (bool aqua, string line)[] AquaLines =
        {
            (true, "내 아이들을 쓰러뜨린 자가 너로구나, 작은 사신."),
            (false, "...네가 이 바다의 주인인가. 정령들은 붉은 달에 물들어 있었다. 나는 풀어 줬을 뿐이야."),
            (true, "변명은 파도에 실어 보내렴. 나는 물의 여왕 아쿠아."),
            (true, "내 바다에 떨어진 이상, 네가 돌아갈 뭍은 없단다."),
            (false, "...정면으로는 승산이 없어. 하지만 저 돌진, 앞만 보고 들이받는군."),
            (false, "떠내려오는 바위 뒤에 숨어서 끌어들이면... 저 녀석이 알아서 처박히겠지."),
        };

        private static readonly (bool aqua, string line)[] VictoryLines =
        {
            (true, "...내 바다의 바위를 창 삼아 휘두르다니. 영리한 사신이구나."),
            (true, "좋아, 오늘은 물러나 주마. 하지만 기억하렴. 바다는 한 번 삼킨 것을 쉽게 놓아주지 않는단다."),
            (false, "...그 말, 다음에 만나면 그대로 돌려주지."),
        };

        private class ScrollLayer
        {
            public SpriteRenderer[] Tiles;
            public float TileWidth;
            public float Speed;
            public float Offset;
        }

        private class Obstacle
        {
            public SpriteRenderer Renderer;
        }

        private Character player;
        private CharacterMovement2D movement;
        private Rigidbody2D body;
        private SpriteRenderer playerRenderer;
        private WalkSpriteAnimator walkAnimator;
        private JumpSpriteAnimator jumpAnimator;
        private HitReactSpriteAnimator hitAnimator;
        private bool walkWasEnabled;
        private bool jumpWasEnabled;
        private bool hitWasEnabled;
        private float savedGravity;
        private bool swimming;

        private NpcDialogueUI dialogueUI;
        private bool skippable;
        private bool skipped;
        private bool inIntro = true;
        private bool talking;
        private bool facingLeft;
        private float scrollFactor;
        private System.Action onIntroShown;
        private System.Action onEscaped;
        private Vector2 viewCenter;
        private float viewHalfWidth;
        private float viewHalfHeight;
        private readonly List<ScrollLayer> layers = new List<ScrollLayer>();
        private readonly List<Obstacle> obstacles = new List<Obstacle>();

        private Transform startPlatform;
        private Sprite[] swimFrames;
        private Sprite[] introIdleFrames;
        private Sprite[] bossFrames;
        private Sprite[] bossThrustFrames;
        private Sprite[] bossDrillTransformFrames;
        private Sprite[] bossDrillLoopFrames;
        private enum BossAttack { None, Spear, Drill }
        private BossAttack bossAttack;
        private bool nextAttackIsDrill;
        // 재생 중인 공격 모션. 없으면 대기 모션을 돌린다.
        private Sprite[] bossPoseFrames;
        private float bossPoseStartTime;
        private float bossPoseDuration;
        private bool bossPoseLoops;
        private SpriteRenderer bossRenderer;
        private Vector3 bossCenterOffset;
        private SpriteRenderer lungeWarning;
        private Coroutine lungeRoutine;
        private bool lunging;
        private float bossCloseness;

        private Image fade;
        private GameObject chaseHud;
        private Image progressFill;     // 아쿠아 체력바
        private int bossHp = BossMaxHp;
        private bool bossStunned;
        private float bossHurtUntil;
        private bool victoryStarted;

        private class Fragment
        {
            public SpriteRenderer Renderer;
            public Vector2 Velocity;
            public float Spin;
            public float Age;
        }
        private readonly List<Fragment> fragments = new List<Fragment>();

        private float BattleProgress => 1f - (float)bossHp / BossMaxHp;
        private Text hintText;

        private bool running;
        private float elapsed;
        private float nextObstacleTime = FirstObstacleTime;
        private float nextLungeTime = FirstLungeTime;
        private float invincibleUntil;
        private float animationTime;
        private float swimAnimationTime;
        private Vector2 moveInput;
        private float driftSpeed;
        private bool dashRequested;
        private Vector2 dashVelocity;
        private float dashEndTime = -1f;
        private float dashReadyTime;
        private float nextGhostTime;
        private bool IsDashing => Time.time < dashEndTime;

        private float ViewLeft => viewCenter.x - viewHalfWidth;
        private float ViewRight => viewCenter.x + viewHalfWidth;
        private float ViewBottom => viewCenter.y - viewHalfHeight;
        private float ViewTop => viewCenter.y + viewHalfHeight;

        public void Initialize(Character targetPlayer, NpcDialogueUI dialogue, bool introSkippable, System.Action introShown, System.Action escaped)
        {
            player = targetPlayer;
            dialogueUI = dialogue;
            skippable = introSkippable;
            onIntroShown = introShown;
            onEscaped = escaped;
            movement = player.GetComponent<CharacterMovement2D>();
            body = player.GetComponent<Rigidbody2D>();
            walkAnimator = player.GetComponent<WalkSpriteAnimator>();
            jumpAnimator = player.GetComponent<JumpSpriteAnimator>();
            hitAnimator = player.GetComponent<HitReactSpriteAnimator>();
            var visual = player.transform.Find("Visual");
            playerRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : player.GetComponent<SpriteRenderer>();

            var cam = Camera.main;
            viewCenter = cam.transform.position;
            viewHalfHeight = cam.orthographicSize;
            viewHalfWidth = viewHalfHeight * cam.aspect;

            swimFrames = LoadSortedFrames(SwimFramesPath);
            introIdleFrames = LoadSortedFrames(IntroIdleFramesPath);
            bossFrames = LoadSortedFrames(BossFramesPath);
            bossThrustFrames = LoadSortedFrames(BossThrustFramesPath);
            bossDrillTransformFrames = LoadSortedFrames(BossDrillTransformFramesPath);
            var drillFrames = LoadSortedFrames(BossDrillFramesPath);
            bossDrillLoopFrames = new Sprite[Mathf.Max(0, drillFrames.Length - DrillLoopFirstFrame)];
            if (bossDrillLoopFrames.Length > 0) System.Array.Copy(drillFrames, DrillLoopFirstFrame, bossDrillLoopFrames, 0, bossDrillLoopFrames.Length);

            fade = CreateFade(transform, DeepWaterColor);
            SetFadeAlpha(fade, 1f);
            BuildLayer(FarLayerPath, FarLayerSpeed, -12, Color.white);
            BuildLayer(MidLayerPath, MidLayerSpeed, -11, MidLayerTint);
            BuildStartPlatform();
            BuildBoss();
            BuildLungeWarning();
            BuildChaseHud();
        }

        // 화면 전체를 덮는 단색 막. 무너지는 연출과 추격전이 같은 색으로 이어지도록 함께 쓴다.
        public static Image CreateFade(Transform parent, Color color)
        {
            var canvasGO = new GameObject("SeaChaseFade", typeof(Canvas));
            canvasGO.transform.SetParent(parent, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var imageGO = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)imageGO.transform;
            rect.SetParent(canvasGO.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = imageGO.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(color.r, color.g, color.b, 0f);
            return image;
        }

        public static IEnumerator FadeTo(Image image, float to, float duration)
        {
            float from = image.color.a;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                SetFadeAlpha(image, Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetFadeAlpha(image, to);
        }

        private static void SetFadeAlpha(Image image, float alpha)
        {
            var color = image.color;
            color.a = alpha;
            image.color = color;
        }

        private static Sprite[] LoadSortedFrames(string path)
        {
            var frames = Resources.LoadAll<Sprite>(path);
            if (frames != null && frames.Length > 0)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            return frames;
        }

        private void BuildLayer(string resourcePath, float speed, int sortingOrder, Color tint)
        {
            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite == null) return;

            float scale = viewHalfHeight * 2f * BackgroundOverscan / sprite.bounds.size.y;
            float tileWidth = sprite.bounds.size.x * scale;
            // 화면을 덮는 장 수 + 흘러나가는 동안 비지 않게 한 장 더.
            int count = Mathf.CeilToInt(viewHalfWidth * 2f / tileWidth) + 1;
            var tiles = new SpriteRenderer[count];
            // 그림 아래끝을 화면 아래끝에 맞춘다(넘치는 쪽은 위).
            float centerY = ViewBottom + sprite.bounds.size.y * scale / 2f;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("ChaseLayer", typeof(SpriteRenderer));
                go.transform.SetParent(transform);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                go.transform.position = new Vector3(0f, centerY, 0f);
                tiles[i] = go.GetComponent<SpriteRenderer>();
                tiles[i].sprite = sprite;
                tiles[i].sortingOrder = sortingOrder;
                tiles[i].color = tint;
            }
            // 이어 붙인 자리에 틈이 비치지 않게 아주 살짝 겹친다.
            layers.Add(new ScrollLayer { Tiles = tiles, TileWidth = tileWidth - 0.01f, Speed = speed });
        }

        private void BuildStartPlatform()
        {
            var sprite = Resources.Load<Sprite>(ObstacleFolder + StartPlatformSprite);
            if (sprite == null) return;

            var go = new GameObject("ChaseStartPlatform", typeof(SpriteRenderer));
            go.transform.SetParent(transform);
            float scale = StartPlatformWidth / sprite.bounds.size.x;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            // 스프라이트 피벗은 하단 중앙이라, 밟는 면이 피벗보다 얼마나 위에 있는지 구해서 맞춘다.
            float surfaceAbovePivot = (sprite.rect.height - StartPlatformSurfaceFromTopPx) / sprite.pixelsPerUnit * scale;
            go.transform.position = new Vector3(player.transform.position.x, viewCenter.y - StartPlatformDrop - surfaceAbovePivot, 0f);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -2;
            startPlatform = go.transform;
        }

        private void BuildBoss()
        {
            var go = new GameObject("SeaChaseBoss", typeof(SpriteRenderer));
            go.transform.SetParent(transform);
            bossRenderer = go.GetComponent<SpriteRenderer>();
            bossRenderer.sortingOrder = 5;
            if (bossFrames == null || bossFrames.Length == 0) return;

            bossRenderer.sprite = bossFrames[0];
            float scale = BossHeight / bossFrames[0].bounds.size.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            bossCenterOffset = bossRenderer.bounds.center - go.transform.position;
            SetBossCenter(new Vector2(ViewLeft - BossHeight, viewCenter.y));
        }

        private void BuildLungeWarning()
        {
            var go = new GameObject("SeaChaseLungeWarning", typeof(SpriteRenderer));
            go.transform.SetParent(transform);
            lungeWarning = go.GetComponent<SpriteRenderer>();
            lungeWarning.sprite = UISprites.WhitePixel;
            lungeWarning.sortingOrder = 3;
            Vector2 pixel = UISprites.WhitePixel.bounds.size;
            go.transform.localScale = new Vector3(viewHalfWidth * 2f / pixel.x, LungeLaneHeight / pixel.y, 1f);
            go.SetActive(false);
        }

        private void BuildChaseHud()
        {
            var canvasGO = new GameObject("SeaChaseHud", typeof(Canvas), typeof(CanvasScaler));
            chaseHud = canvasGO;
            canvasGO.transform.SetParent(transform, false);
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            var background = CreateTopCenterImage(canvasGO.transform, "Progress_BG", new Vector2(0f, -24f), new Vector2(360f, 14f));
            background.color = new Color(0f, 0f, 0f, 0.5f);

            progressFill = CreateTopCenterImage(canvasGO.transform, "Progress_Fill", new Vector2(0f, -26f), new Vector2(356f, 10f));
            progressFill.UseAsFilled();
            progressFill.color = new Color(0.45f, 0.85f, 1f);
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFill.fillAmount = 1f;

            var nameGO = new GameObject("BossName", typeof(RectTransform), typeof(Text));
            var nameRect = (RectTransform)nameGO.transform;
            nameRect.SetParent(canvasGO.transform, false);
            nameRect.anchorMin = new Vector2(0.5f, 1f);
            nameRect.anchorMax = new Vector2(0.5f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -4f);
            nameRect.sizeDelta = new Vector2(360f, 20f);
            var nameText = nameGO.GetComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 15;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = new Color(0.75f, 0.92f, 1f);
            nameText.text = "물의 여왕 아쿠아";
            nameGO.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            var textGO = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(canvasGO.transform, false);
            textRect.anchorMin = new Vector2(0.5f, 1f);
            textRect.anchorMax = new Vector2(0.5f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = new Vector2(0f, -46f);
            textRect.sizeDelta = new Vector2(600f, 30f);
            hintText = textGO.GetComponent<Text>();
            hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hintText.fontSize = 18;
            hintText.fontStyle = FontStyle.Bold;
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = Color.white;
            hintText.text = string.Empty;   // 공략은 따로 알려 주지 않고 등장 대화의 네크로시아 대사로 짐작하게 한다
            var outline = textGO.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
            canvasGO.SetActive(false); // 등장 연출이 끝나면 켠다
        }

        private static Image CreateTopCenterImage(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        // 쓰러져서 이 맵을 다시 지을 때는 이전 컨트롤러의 OnDestroy(조작 복구)가 프레임 끝에 돌아서,
        // 그 뒤에 수영 상태로 바꾸도록 한 프레임 기다린다.
        private IEnumerator Start()
        {
            yield return null;
            BeginSwim();
            onIntroShown?.Invoke();

            StartCoroutine(FadeTo(fade, 0f, FadeInTime));
            yield return SinkPlayer();

            yield return Talk(BeforeAquaLines);
            if (!skipped)
            {
                facingLeft = true;
                yield return BossEntrance();
                yield return Talk(AquaLines);
            }
            dialogueUI?.Hide();
            talking = false;

            facingLeft = false;
            if (playerRenderer != null) playerRenderer.flipX = false;
            inIntro = false;
            chaseHud.SetActive(true);
            running = true;
        }

        // 위에서 천천히 가라앉아 돌섬 위에 내려앉는다.
        private IEnumerator SinkPlayer()
        {
            float x = player.transform.position.x;
            float fromY = ViewTop - SinkTopMargin;
            float toY = viewCenter.y - StartPlatformDrop;
            for (float t = 0f; t < SinkDuration && !SkipPressed(); t += Time.deltaTime)
            {
                body.position = new Vector2(x, Mathf.Lerp(fromY, toY, Mathf.SmoothStep(0f, 1f, t / SinkDuration)));
                yield return null;
            }
            body.position = new Vector2(x, toY);
        }

        private IEnumerator BossEntrance()
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.Shake(BossEntranceDuration, 0.2f);

            float y = PlayerHitbox.center.y;
            float fromX = BossCenter.x;
            float toX = ViewLeft + BossEntranceOffset;
            for (float t = 0f; t < BossEntranceDuration; t += Time.deltaTime)
            {
                SetBossCenter(new Vector2(Mathf.Lerp(fromX, toX, Mathf.SmoothStep(0f, 1f, t / BossEntranceDuration)), y));
                yield return null;
            }
        }

        private IEnumerator Talk(string[] lines)
        {
            foreach (var line in lines)
            {
                yield return Talk(false, line);
                if (skipped) yield break;
            }
        }

        private IEnumerator Talk((bool aqua, string line)[] lines)
        {
            foreach (var (aqua, line) in lines)
            {
                yield return Talk(aqua, line);
                if (skipped) yield break;
            }
        }

        private IEnumerator Talk(bool aqua, string line)
        {
            talking = true;
            string text = line + (skippable ? SkipHint : "") + NextHint;
            if (aqua) dialogueUI?.ShowAqua(text);
            else dialogueUI?.ShowNecrosia(text);

            yield return null; // 대사가 바뀐 그 프레임의 입력으로 바로 넘어가지 않게
            while (!AdvancePressed())
            {
                if (SkipPressed())
                {
                    skipped = true;
                    yield break;
                }
                yield return null;
            }
        }

        private bool SkipPressed()
        {
            var keyboard = Keyboard.current;
            return skippable && keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }

        private static bool AdvancePressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame;
        }

        private void BeginSwim()
        {
            movement.SetCutsceneLocked(true);
            // 스킬이 끝나며 풀지 못하도록 물리 이동도 함께 잠근다(이 잠금이 걸려 있어야 걷기 속도가 덮어쓰지 않는다).
            movement.SetInputLocked(true);
            movement.Face(Vector2.right);
            player.SetInvincible(false);

            walkWasEnabled = walkAnimator != null && walkAnimator.enabled;
            jumpWasEnabled = jumpAnimator != null && jumpAnimator.enabled;
            hitWasEnabled = hitAnimator != null && hitAnimator.enabled;
            if (walkAnimator != null) walkAnimator.enabled = false;
            if (jumpAnimator != null) jumpAnimator.enabled = false;
            // 피격 포즈가 끝나며 걷기/점프 애니메이터를 다시 켜므로, 추격 중에는 깜빡임만 직접 처리한다.
            if (hitAnimator != null) hitAnimator.enabled = false;

            savedGravity = body.gravityScale;
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            if (playerRenderer != null) playerRenderer.flipX = false;
            swimming = true;
        }

        private void OnDestroy()
        {
            // 대화 도중 맵이 사라지면(관리자 순간이동 등) 대화창이 남지 않게 닫는다.
            if (talking) dialogueUI?.Hide();
            if (!swimming || player == null) return;

            body.gravityScale = savedGravity;
            movement.SetCutsceneLocked(false);
            if (walkAnimator != null) walkAnimator.enabled = walkWasEnabled;
            if (jumpAnimator != null) jumpAnimator.enabled = jumpWasEnabled;
            if (hitAnimator != null) hitAnimator.enabled = hitWasEnabled;
            if (playerRenderer != null)
            {
                playerRenderer.enabled = true;
                SkillFrameNormalizer.Reset(playerRenderer.transform);
            }
        }

        private void Update()
        {
            if (player == null) return;

            // 등장 연출 동안은 멈춰 있다가 추격이 시작되면 서서히 흐르기 시작한다.
            if (!inIntro) scrollFactor = Mathf.MoveTowards(scrollFactor, 1f, Time.deltaTime / ScrollRampTime);
            ScrollLayers();
            UpdateStartPlatform();
            UpdateObstacles();
            animationTime += Time.deltaTime;
            swimAnimationTime += Time.deltaTime * (IsDashing ? DashFrameRateScale : 1f);
            AnimateBoss();
            UpdateDashGhosts();
            UpdateFragments();
            if (bossRenderer != null) bossRenderer.color = Time.time < bossHurtUntil ? BossHurtColor : Color.white;
            if (swimming && !player.IsDead)
            {
                if (inIntro) AnimateIntroIdle();
                else AnimateSwim();
            }

            if (!running || player.IsDead) return;

            ReadMoveInput();
            elapsed += Time.deltaTime;
            float progress = BattleProgress;

            if (elapsed >= nextObstacleTime)
            {
                SpawnObstacle();
                nextObstacleTime = elapsed + Mathf.Lerp(ObstacleIntervalStart, ObstacleIntervalEnd, progress);
            }
            if (elapsed >= nextLungeTime && !lunging)
            {
                lungeRoutine = StartCoroutine(nextAttackIsDrill ? DrillRoutine() : LungeRoutine());
                nextAttackIsDrill = !nextAttackIsDrill;
            }

            UpdateBoss();
            CheckHits();
            UpdateHitFlicker();

            if (bossHp <= 0 && !victoryStarted) StartCoroutine(VictoryRoutine());
        }

        private void FixedUpdate()
        {
            if (!running || player == null || player.IsDead) return;

            bool swimmingAcross = Mathf.Abs(moveInput.x) > 0.01f;
            float driftRate = DriftMaxSpeed / (swimmingAcross ? DriftRecoverTime : DriftRampTime);
            driftSpeed = Mathf.MoveTowards(driftSpeed, swimmingAcross ? 0f : DriftMaxSpeed, driftRate * Time.fixedDeltaTime);

            if (dashRequested)
            {
                dashRequested = false;
                Vector2 direction = moveInput.sqrMagnitude > 0.01f ? moveInput : Vector2.right;
                dashVelocity = direction * (DashDistance / DashDuration);
                dashEndTime = Time.time + DashDuration;
                dashReadyTime = Time.time + DashCooldown;
                driftSpeed = 0f;
            }

            Vector2 velocity = IsDashing ? dashVelocity : moveInput * SwimSpeed + Vector2.left * driftSpeed;
            Vector2 next = body.position + velocity * Time.fixedDeltaTime;
            next.x = Mathf.Clamp(next.x, ViewLeft + LeftMargin, ViewRight - RightMargin);
            next.y = Mathf.Clamp(next.y, ViewBottom + BottomMargin, ViewTop - TopMargin);
            body.MovePosition(next);
        }

        private void ReadMoveInput()
        {
            moveInput = Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.leftArrowKey.isPressed) moveInput.x -= 1f;
            if (keyboard.rightArrowKey.isPressed) moveInput.x += 1f;
            if (keyboard.downArrowKey.isPressed) moveInput.y -= 1f;
            if (keyboard.upArrowKey.isPressed) moveInput.y += 1f;
            moveInput = moveInput.normalized;

            if ((keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame)
                && !IsDashing && Time.time >= dashReadyTime)
            {
                dashRequested = true;
            }
        }

        // 대시하는 동안 지나간 자리에 보랏빛 잔상을 남긴다.
        private readonly List<(SpriteRenderer renderer, float bornAt)> dashGhosts = new List<(SpriteRenderer, float)>();

        private void SpawnDashGhost()
        {
            var go = new GameObject("SeaChaseDashGhost", typeof(SpriteRenderer));
            go.transform.SetParent(transform);
            go.transform.SetPositionAndRotation(playerRenderer.transform.position, playerRenderer.transform.rotation);
            go.transform.localScale = playerRenderer.transform.lossyScale;
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = playerRenderer.sprite;
            renderer.flipX = playerRenderer.flipX;
            renderer.color = DashGhostColor;
            renderer.sortingLayerID = playerRenderer.sortingLayerID;
            renderer.sortingOrder = playerRenderer.sortingOrder - 1;
            dashGhosts.Add((renderer, Time.time));
        }

        private void UpdateDashGhosts()
        {
            for (int i = dashGhosts.Count - 1; i >= 0; i--)
            {
                var (renderer, bornAt) = dashGhosts[i];
                float age = Time.time - bornAt;
                if (renderer == null || age >= DashGhostLife)
                {
                    if (renderer != null) Destroy(renderer.gameObject);
                    dashGhosts.RemoveAt(i);
                    continue;
                }
                // 장애물처럼 뒤로 흘러가며 옅어진다.
                renderer.transform.position += Vector3.left * (ObstacleSpeed * scrollFactor * Time.deltaTime);
                var color = DashGhostColor;
                color.a *= 1f - age / DashGhostLife;
                renderer.color = color;
            }
        }

        private void ScrollLayers()
        {
            foreach (var layer in layers)
            {
                layer.Offset = (layer.Offset + layer.Speed * scrollFactor * Time.deltaTime) % layer.TileWidth;
                float left = ViewLeft - layer.Offset;
                for (int i = 0; i < layer.Tiles.Length; i++)
                {
                    var tile = layer.Tiles[i].transform;
                    tile.position = new Vector3(left + layer.TileWidth * (i + 0.5f), tile.position.y, 0f);
                }
            }
        }

        // 추격이 시작되면 장애물과 같은 속도로 뒤로 흘러가 사라진다.
        private void UpdateStartPlatform()
        {
            if (startPlatform == null) return;

            startPlatform.position += Vector3.left * (ObstacleSpeed * scrollFactor * Time.deltaTime);
            if (startPlatform.position.x + StartPlatformWidth < ViewLeft) Destroy(startPlatform.gameObject);
        }

        private void AnimateSwim()
        {
            if (playerRenderer == null || swimFrames == null || swimFrames.Length == 0) return;

            var frame = swimFrames[Mathf.FloorToInt(swimAnimationTime * SwimFrameRate) % swimFrames.Length];
            playerRenderer.sprite = frame;
            SkillFrameNormalizer.Apply(playerRenderer.transform, frame, SwimTargetHeight);
            if (IsDashing && Time.time >= nextGhostTime)
            {
                nextGhostTime = Time.time + DashGhostInterval;
                SpawnDashGhost();
            }
        }

        private void AnimateIntroIdle()
        {
            if (playerRenderer == null || introIdleFrames == null || introIdleFrames.Length == 0) return;

            var frame = introIdleFrames[Mathf.FloorToInt(animationTime * IntroIdleFrameRate) % introIdleFrames.Length];
            playerRenderer.sprite = frame;
            playerRenderer.flipX = facingLeft;
            SkillFrameNormalizer.Apply(playerRenderer.transform, frame, IntroIdleTargetHeight);
        }

        private void AnimateBoss()
        {
            if (bossPoseFrames != null && bossPoseFrames.Length > 0)
            {
                int count = bossPoseFrames.Length;
                int index = Mathf.FloorToInt((animationTime - bossPoseStartTime) / bossPoseDuration * count);
                bossRenderer.sprite = bossPoseFrames[bossPoseLoops ? index % count : Mathf.Clamp(index, 0, count - 1)];
                return;
            }

            if (bossFrames == null || bossFrames.Length == 0) return;
            bossRenderer.sprite = bossFrames[Mathf.FloorToInt(animationTime * BossFrameRate) % bossFrames.Length];
        }

        // duration: 한 번 재생(반복이면 한 바퀴)에 걸리는 시간.
        private void PlayBossPose(Sprite[] frames, float duration, bool loops)
        {
            bossPoseFrames = frames;
            bossPoseStartTime = animationTime;
            bossPoseDuration = duration;
            bossPoseLoops = loops;
        }

        private void EndBossAttack()
        {
            bossPoseFrames = null;
            bossAttack = BossAttack.None;
            SetBossAngle(0f);
        }

        // 그림의 기준점이 몸 가운데가 아니어서, 돌려도 몸 가운데가 제자리에 있도록 다시 놓는다.
        private void SetBossAngle(float degrees)
        {
            Vector2 center = BossCenter;
            bossRenderer.transform.rotation = Quaternion.Euler(0f, 0f, degrees);
            SetBossCenter(center);
        }

        private Vector2 BossCenter => bossRenderer.transform.position + bossRenderer.transform.rotation * bossCenterOffset;

        private void SetBossCenter(Vector2 center)
        {
            bossRenderer.transform.position = (Vector3)center - bossRenderer.transform.rotation * bossCenterOffset;
        }

        private void UpdateBoss()
        {
            if (lunging) return;

            bossCloseness = Mathf.Max(0f, bossCloseness - BossRetreatPerSecond * Time.deltaTime);
            float targetX = ViewLeft + BossRestOffset + bossCloseness * BossStepPerHit;
            Vector2 center = BossCenter;
            center.x = Mathf.MoveTowards(center.x, targetX, BossApproachSpeed * Time.deltaTime);
            center.y = Mathf.MoveTowards(center.y, PlayerHitbox.center.y, BossFollowYSpeed * Time.deltaTime);
            SetBossCenter(center);
        }

        // 경고선을 띄워 줄을 알려 준 뒤 그 줄을 따라 창을 찌르며 화면 끝까지 돌진하고, 왼쪽 밖에서 다시 따라붙는다.
        private IEnumerator LungeRoutine()
        {
            lunging = true;
            float laneY = PlayerHitbox.center.y;
            yield return WarnLane(laneY, LungeLaneHeight, LungeWarnTime, laneY);

            bossAttack = BossAttack.Spear;
            PlayBossPose(bossThrustFrames, LungeDuration, loops: false);
            float fromX = BossCenter.x;
            float toX = ViewRight + BossHeight;
            for (float t = 0f; t < LungeDuration; t += Time.deltaTime)
            {
                SetBossCenter(new Vector2(Mathf.Lerp(fromX, toX, t / LungeDuration), laneY));
                // 창끝이나 몸이 바위에 닿으면 그 자리에 박힌다.
                var rock = FindRockTouching(BossSpearHitbox) ?? FindRockTouching(BossBodyHitbox);
                if (rock != null)
                {
                    yield return CrashIntoRock(rock);
                    break;
                }
                yield return null;
            }
            FinishCharge(laneY);
        }

        // 화면 안에 들어와 있는 바위 중 area에 닿은 것.
        private Obstacle FindRockTouching(Rect area)
        {
            foreach (var obstacle in obstacles)
            {
                var bounds = obstacle.Renderer.bounds;
                if (bounds.max.x > ViewRight) continue;
                if (area.Overlaps(Shrunk(bounds, ObstacleHitboxShrink))) return obstacle;
            }
            return null;
        }

        // 창이 바위에 박힘: 바위가 부서지고 아쿠아가 피해를 입은 채 잠깐 그 자리에서 비틀거린다.
        private IEnumerator CrashIntoRock(Obstacle rock)
        {
            ShatterRock(rock);
            DamageBoss();
            bossAttack = BossAttack.None;
            bossStunned = true;
            bossPoseFrames = null;
            Vector2 center = BossCenter;
            for (float t = 0f; t < BossStunTime; t += Time.deltaTime)
            {
                float fade = 1f - t / BossStunTime;
                SetBossCenter(center + new Vector2(Mathf.Sin(t * 40f) * BossStunShake * fade, 0f));
                yield return null;
            }
            SetBossCenter(center);
            bossStunned = false;
        }

        // 드릴이 지나가며 닿은 바위를 모두 부수고, 부순 만큼 피해를 입는다(멈추지 않고 그대로 뚫고 간다).
        private void SmashRocksWithDrill()
        {
            for (int i = obstacles.Count - 1; i >= 0; i--)
            {
                var obstacle = obstacles[i];
                var bounds = obstacle.Renderer.bounds;
                if (bounds.max.x > ViewRight || bounds.min.x < ViewLeft) continue;
                var rect = Shrunk(bounds, ObstacleHitboxShrink);
                if (!DrillTouches(rect.center, Mathf.Min(rect.width, rect.height) / 2f)) continue;

                ShatterRock(obstacle);
                DamageBoss();
            }
        }

        private void DamageBoss()
        {
            if (bossHp <= 0) return;
            bossHp--;
            progressFill.fillAmount = (float)bossHp / BossMaxHp;
            bossHurtUntil = Time.time + BossHurtFlashTime;
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.Shake(0.3f, 0.25f);
        }

        // 바위를 없애고 그 그림을 작게 잘게 흩뿌린다.
        private void ShatterRock(Obstacle rock)
        {
            obstacles.Remove(rock);
            var source = rock.Renderer;
            Vector3 center = source.bounds.center;
            for (int i = 0; i < FragmentCount; i++)
            {
                var go = new GameObject("RockFragment", typeof(SpriteRenderer));
                go.transform.SetParent(transform);
                go.transform.position = center + (Vector3)(Random.insideUnitCircle * 0.4f);
                go.transform.localScale = source.transform.localScale * Random.Range(0.2f, 0.35f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                var renderer = go.GetComponent<SpriteRenderer>();
                renderer.sprite = source.sprite;
                renderer.color = source.color;
                renderer.sortingOrder = source.sortingOrder + 1;
                float angle = Random.Range(0f, Mathf.PI * 2f);
                fragments.Add(new Fragment
                {
                    Renderer = renderer,
                    Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(2f, 5f),
                    Spin = Random.Range(-360f, 360f),
                });
            }
            Destroy(source.gameObject);
        }

        private void UpdateFragments()
        {
            for (int i = fragments.Count - 1; i >= 0; i--)
            {
                var fragment = fragments[i];
                fragment.Age += Time.deltaTime;
                if (fragment.Renderer == null || fragment.Age >= FragmentLife)
                {
                    if (fragment.Renderer != null) Destroy(fragment.Renderer.gameObject);
                    fragments.RemoveAt(i);
                    continue;
                }
                // 물속이라 금방 느려지며 천천히 가라앉는다.
                fragment.Velocity = fragment.Velocity * (1f - 2.5f * Time.deltaTime) + Vector2.down * (1.5f * Time.deltaTime);
                var piece = fragment.Renderer.transform;
                piece.position += (Vector3)(fragment.Velocity * Time.deltaTime);
                piece.Rotate(0f, 0f, fragment.Spin * Time.deltaTime);
                var color = fragment.Renderer.color;
                color.a = 1f - fragment.Age / FragmentLife;
                fragment.Renderer.color = color;
            }
        }

        // 물의 드릴: 경고선이 떠 있는 동안 회오리로 변신하고, 더 넓은 줄을 빠르게 뚫고 지나간다.
        private IEnumerator DrillRoutine()
        {
            lunging = true;
            float laneY = PlayerHitbox.center.y;
            float centerY = laneY - DrillTipLift;
            PlayBossPose(bossDrillTransformFrames, DrillChargeTime, loops: false);
            yield return WarnLane(laneY, DrillLaneHeight, DrillChargeTime, centerY);

            bossAttack = BossAttack.Drill;
            PlayBossPose(bossDrillLoopFrames, DrillLoopCycle, loops: true);
            yield return Charge(centerY, ViewRight + DrillTravelMargin, DrillDuration, smashRocks: true);

            // 방금 오른쪽으로 나갔으니 오른쪽에서 먼저 돌아온다.
            for (int pass = 0; pass < DrillDiagonalPasses; pass++)
            {
                yield return DiagonalPass(fromRight: pass % 2 == 0);
            }
            FinishCharge(viewCenter.y);
        }

        // 화면 밖 위나 아래(네크로시아와 반대쪽)에서 지금 네크로시아가 있는 자리를 겨냥해 반대쪽 밖까지 가로지른다.
        private IEnumerator DiagonalPass(bool fromRight)
        {
            Vector2 aim = PlayerHitbox.center;
            float side = fromRight ? 1f : -1f;
            float startHeight = aim.y > viewCenter.y ? -DrillDiagonalStartHeight : DrillDiagonalStartHeight;
            var start = new Vector2(viewCenter.x + side * (viewHalfWidth + DrillDiagonalStartMargin), viewCenter.y + startHeight);
            Vector2 direction = (aim - start).normalized;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float length = (viewHalfWidth + DrillDiagonalStartMargin) * 2f / Mathf.Abs(direction.x);

            SetBossCenter(start);
            SetBossAngle(angle);

            ShowLane(aim, DrillLaneHeight, angle, length);
            for (float t = 0f; t < DrillDiagonalWarnTime; t += Time.deltaTime)
            {
                PulseLane(t);
                yield return null;
            }
            lungeWarning.gameObject.SetActive(false);

            Vector2 end = start + direction * length;
            for (float t = 0f; t < DrillDiagonalDuration; t += Time.deltaTime)
            {
                SetBossCenter(Vector2.Lerp(start, end, t / DrillDiagonalDuration));
                SmashRocksWithDrill();
                yield return null;
            }
        }

        private void ShowLane(Vector2 center, float height, float angle, float length)
        {
            Vector2 pixel = UISprites.WhitePixel.bounds.size;
            var lane = lungeWarning.transform;
            lane.localScale = new Vector3(length / pixel.x, height / pixel.y, 1f);
            lane.SetPositionAndRotation(center, Quaternion.Euler(0f, 0f, angle));
            lungeWarning.gameObject.SetActive(true);
        }

        private void PulseLane(float time)
        {
            var color = LungeWarnColor;
            color.a = 0.15f + 0.25f * Mathf.PingPong(time * 6f, 1f);
            lungeWarning.color = color;
        }

        private IEnumerator WarnLane(float laneY, float laneHeight, float duration, float bossY)
        {
            ShowLane(new Vector2(viewCenter.x, laneY), laneHeight, 0f, viewHalfWidth * 2f);

            Vector2 start = BossCenter;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                PulseLane(t);
                SetBossCenter(new Vector2(start.x, Mathf.Lerp(start.y, bossY, t / duration)));
                yield return null;
            }
            lungeWarning.gameObject.SetActive(false);
        }

        private IEnumerator Charge(float y, float toX, float duration, bool smashRocks)
        {
            float fromX = BossCenter.x;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                SetBossCenter(new Vector2(Mathf.Lerp(fromX, toX, t / duration), y));
                if (smashRocks) SmashRocksWithDrill();
                yield return null;
            }
        }

        private void FinishCharge(float y)
        {
            EndBossAttack();
            SetBossCenter(new Vector2(ViewLeft - BossHeight, y));
            lunging = false;
            lungeRoutine = null;
            // 공격 길이가 패턴마다 달라서, 다음 공격까지의 간격은 끝난 시점부터 잰다.
            nextLungeTime = elapsed + Mathf.Lerp(LungeIntervalStart, LungeIntervalEnd, BattleProgress);
        }

        private void SpawnObstacle()
        {
            var sprite = Resources.Load<Sprite>(ObstacleFolder + ObstacleSprites[Random.Range(0, ObstacleSprites.Length)]);
            if (sprite == null) return;

            var go = new GameObject("ChaseObstacle", typeof(SpriteRenderer));
            go.transform.SetParent(transform);
            float scale = ObstacleHeight / sprite.bounds.size.y;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = ObstacleTint;
            renderer.sortingOrder = 1;

            float y = Random.Range(ViewBottom + BottomMargin, ViewTop - TopMargin - ObstacleHeight);
            float width = sprite.bounds.size.x * scale;
            go.transform.position = new Vector3(ViewRight + width, y, 0f);
            // 피벗과 상관없이 아래끝이 y에 오도록 맞춘다.
            go.transform.position += new Vector3(0f, y - renderer.bounds.min.y, 0f);
            obstacles.Add(new Obstacle { Renderer = renderer });
        }

        private void UpdateObstacles()
        {
            for (int i = obstacles.Count - 1; i >= 0; i--)
            {
                var obstacle = obstacles[i];
                obstacle.Renderer.transform.position += Vector3.left * (ObstacleSpeed * Time.deltaTime);
                if (obstacle.Renderer.bounds.max.x >= ViewLeft) continue;

                Destroy(obstacle.Renderer.gameObject);
                obstacles.RemoveAt(i);
            }
        }

        private Rect PlayerHitbox
        {
            get
            {
                Vector2 position = player.transform.position;
                return new Rect(position.x - 0.4f, position.y + 0.1f, 0.8f, 1f);
            }
        }

        private Rect BossBodyHitbox => new Rect(BossCenter - BossBodySize / 2f, BossBodySize);

        private Rect BossSpearHitbox
        {
            get
            {
                Vector2 center = BossCenter;
                float left = center.x + BossBodySize.x / 2f;
                return new Rect(left, center.y + BossSpearHeightOffset - BossSpearThickness / 2f, center.x + BossSpearTip - left, BossSpearThickness);
            }
        }

        private bool DrillTouchesPlayer() => DrillTouches(PlayerHitbox.center, PlayerHitRadius);

        private bool DrillTouches(Vector2 point, float radius)
        {
            Vector2 center = BossCenter;
            Vector2 axis = bossRenderer.transform.rotation * Vector3.right;
            Vector2 lift = bossRenderer.transform.rotation * (Vector3.up * DrillTipLift);
            Vector2 back = center + lift - axis * DrillCoreBack;
            float along = Mathf.Clamp(Vector2.Dot(point - back, axis), 0f, DrillCoreBack + DrillCoreFront);
            float reach = DrillCoreRadius + radius;
            return (point - (back + axis * along)).sqrMagnitude <= reach * reach;
        }

        private static Rect Shrunk(Bounds bounds, float ratio)
        {
            Vector2 size = bounds.size * ratio;
            return new Rect((Vector2)bounds.center - size / 2f, size);
        }

        private void CheckHits()
        {
            if (Time.time < invincibleUntil || IsDashing) return;

            var hitbox = PlayerHitbox;
            if (bossAttack == BossAttack.Drill)
            {
                if (DrillTouchesPlayer())
                {
                    HitPlayer(DrillDamageRatio, BossHitShove);
                    return;
                }
            }
            else if (!bossStunned && bossRenderer.sprite != null && (hitbox.Overlaps(BossBodyHitbox) || (bossAttack == BossAttack.Spear && hitbox.Overlaps(BossSpearHitbox))))
            {
                HitPlayer(BossDamageRatio, BossHitShove);
                bossCloseness = Mathf.Max(0f, bossCloseness - 0.5f);
                return;
            }

            foreach (var obstacle in obstacles)
            {
                if (!hitbox.Overlaps(Shrunk(obstacle.Renderer.bounds, ObstacleHitboxShrink))) continue;

                HitPlayer(ObstacleDamageRatio, -HitShove);
                bossCloseness += 1f;
                return;
            }
        }

        private void HitPlayer(float damageRatio, float shoveX)
        {
            invincibleUntil = Time.time + HitInvincibleTime;
            player.TakeDamage(player.MaxHp * damageRatio, hitReaction: false);
            if (player.IsDead) return;

            Vector2 next = body.position + Vector2.right * shoveX;
            next.x = Mathf.Clamp(next.x, ViewLeft + LeftMargin, ViewRight - RightMargin);
            body.position = next;
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.Shake(0.25f, 0.15f);
        }

        private void UpdateHitFlicker()
        {
            if (playerRenderer == null) return;

            bool flickering = Time.time < invincibleUntil;
            playerRenderer.enabled = !flickering || Mathf.FloorToInt(Time.time / HitFlickerInterval) % 2 == 0;
        }

        // 체력을 다 깎음: 아쿠아가 비틀거리며 물러날 말을 남기고 사라진다.
        private IEnumerator VictoryRoutine()
        {
            victoryStarted = true;
            running = false;
            moveInput = Vector2.zero;
            if (playerRenderer != null) playerRenderer.enabled = true;
            if (lungeRoutine != null) StopCoroutine(lungeRoutine);
            lungeRoutine = null;
            lungeWarning.gameObject.SetActive(false);
            lunging = true;     // 평소 따라붙기(UpdateBoss)를 멈춘다
            EndBossAttack();
            bossStunned = true;
            hintText.text = "아쿠아를 물리쳤다!";

            // 화면 밖에서 쓰러졌을 수도 있으니 대화하는 동안 보이는 자리로 데려온다.
            Vector2 from = BossCenter;
            var to = new Vector2(ViewLeft + BossEntranceOffset, viewCenter.y);
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.8f);
                SetBossCenter(Vector2.Lerp(from, to, k) + new Vector2(Mathf.Sin(t * 40f) * BossStunShake * (1f - k), 0f));
                yield return null;
            }
            SetBossCenter(to);

            facingLeft = true;
            if (playerRenderer != null) playerRenderer.flipX = true;
            skipped = false;
            yield return Talk(VictoryLines);
            dialogueUI?.Hide();
            talking = false;

            Vector2 start = BossCenter;
            for (float t = 0f; t < EscapeRetreatTime; t += Time.deltaTime)
            {
                SetBossCenter(Vector2.Lerp(start, new Vector2(ViewLeft - BossHeight, start.y), t / EscapeRetreatTime));
                yield return null;
            }

            yield return FadeTo(fade, 1f, FadeOutTime);
            onEscaped?.Invoke();
        }
    }
}
