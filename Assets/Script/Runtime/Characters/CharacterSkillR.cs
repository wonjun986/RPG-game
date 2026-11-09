using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Bootstrap;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // R = 죽음의 소용돌이 (궁극기). 땅을 딛고 있을 때만 쓸 수 있다.
    // 시전하면 먼저 제자리에서 낫을 머리 위로 크게 돌리다 땅에 내리꽂는 준비 모션을 하고, 낫이 땅에 박히면
    // 네크로시아 바로 앞 땅에서 거대한 악마의 문이 솟아오른다. 문 틈으로 빛이 차오르다 문이 열리며 악룡이 걸어 나와
    // 날개를 펴고 포효한다. 입을 크게 벌린 채 포효를 이어 가는 동안, 악룡 바로 앞에서 거대한 폭발이 일어나고
    // 그 자리에서 양옆으로 충격파가 두 번(가까운 쪽 → 먼 쪽) 퍼져 나간다. 큰 폭발은 양옆 충격파가 다 끝날 때까지
    // 그대로 타오르며 범위 안 몬스터에게 계속 피해를 준다.
    // 다 터지고 나면 악룡이 다시 문 안으로 물러나고, 문이 닫히며 땅속으로 가라앉아 사라진다.
    // 그동안 네크로시아는 낫을 꽂은 자세로 무적 상태로 서 있다(문이 가라앉는 동안에는 다시 움직일 수 있다).
    // 피해는 큰 폭발과 충격파 하나하나가 제 범위에 닿은 몬스터에게 준다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    public class CharacterSkillR : MonoBehaviour, ISkillCooldownReset
    {
        [SerializeField] private float manaCost = 100f;
        [SerializeField] private float cooldown = 25f;
        // 큰 폭발(좌우 explosionHalfWidth)은 타오르는 동안 explosionTickInterval마다 기본 공격의 explosionTickMultiplier배로 친다
        // (약 1.5초 동안 7~8번 = 약 15배). 충격파(좌우 burstHalfWidth)는 터지는 동안 burstHits번, 한 번에 burstHitMultiplier배.
        // 악룡 앞 몬스터는 큰 폭발 + 첫 충격파에 닿아 기본 공격 약 24배(예전과 비슷), 멀리 있는 몬스터는 충격파 한 번(9배)만 맞는다.
        [SerializeField] private int burstHits = 3;
        [SerializeField] private float explosionTickInterval = 0.2f;
        [SerializeField] private float explosionTickMultiplier = 2f;
        [SerializeField] private float burstHitMultiplier = 3f;
        [SerializeField] private float explosionHalfWidth = 5.5f;
        [SerializeField] private float burstHalfWidth = 3f;
        [SerializeField] private float burstHeight = 6f;

        [Header("연출: 준비 모션(낫 회전 공격, 12프레임)")]
        [SerializeField] private string readyResourcesPath = "Art/Necrosia/UltReady";
        [SerializeField] private float readyFrameDuration = 0.07f;
        // 낫을 머리 위로 수평으로 돌리는 4~7프레임(0부터 세면 3~6)을 이만큼 되풀이해 돌리는 시간을 늘린다.
        [SerializeField] private int readySpinFirstFrame = 3;
        [SerializeField] private int readySpinLastFrame = 6;
        [SerializeField] private int readySpinLoops = 2;
        // 12장을 같은 캔버스(406x241)로 자르고 서 있는 1~9프레임의 부츠 위치를 맞췄다. 머리 장식~부츠(217px)가
        // 대기 모션과 같은 약 1.53유닛이 되도록 캔버스 키를 1.7로 맞춘다.
        [SerializeField] private float readyTargetHeight = 1.7f;

        [Header("연출: 악마의 문 소환(문 솟음 6 + 문 틈 빛 4 + 악룡 등장 6, 포효 8)")]
        [SerializeField] private string gateResourcesPath = "Art/Effects/UltGate";
        [SerializeField] private string dragonRoarResourcesPath = "Art/Effects/UltDragon";
        [SerializeField] private float gateRiseFrameDuration = 0.08f;   // 1~6: 땅에서 문이 솟는다
        [SerializeField] private float gateGlowFrameDuration = 0.09f;   // 7~10: 닫힌 문 틈으로 빛이 차오른다
        [SerializeField] private float gateEmergeFrameDuration = 0.1f;  // 11~16: 문이 열리며 악룡이 나와 날개를 편다
        [SerializeField] private float gateRetreatFrameDuration = 0.06f; // 사라질 때 위 과정을 거꾸로 되감는 빠르기
        [SerializeField] private float roarFrameDuration = 0.08f;
        // 0부터 셈. 입을 가장 크게 벌리고 빛이 터지는 5번째 프레임. 여기서 화면이 흔들린다.
        [SerializeField] private int roarHoldFrame = 4;
        // 충격파가 터지는 동안에는 입을 벌린 4~6프레임(0부터 3~5)만 왔다 갔다 하며(4→5→6→5) 입은 벌린 채 날개와 몸을 움직이고,
        // 몸 전체를 살짝 떨어 포효를 이어 가는 느낌을 준다.
        [SerializeField] private int roarLoopFirstFrame = 3;
        [SerializeField] private int roarLoopLastFrame = 5;
        [SerializeField] private float roarLoopFrameDuration = 0.1f;
        [SerializeField] private float roarTremble = 0.05f;
        // 네크로시아가 바라보는 쪽으로 떨어진 거리(문 가운데까지). 문틀 폭(약 5.6유닛)의 절반에 조금 더해,
        // 문이 네크로시아 바로 앞에서 솟게 한다. 날개를 편 악룡은 네크로시아 뒤까지 덮는다.
        [SerializeField] private float gateFrontDistance = 3.2f;
        [SerializeField] private float roarShakeDuration = 0.6f;
        [SerializeField] private float roarShakeMagnitude = 0.35f;
        // 기본 화면(orthographicSize 4)은 바닥 위로 5.6유닛까지만 보여서 거대한 문과 악룡이 잘린다. 소환하는 동안 화면을 넓혀
        // 바닥 위 약 7.7유닛까지 보이게 하고, 네크로시아와 문 사이를 비춘다. 문이 가라앉고 나면 원래 크기로 되돌린다.
        [SerializeField] private float summonCameraSize = 5.5f;
        [SerializeField] private float summonZoomDuration = 0.6f;
        // 네 시트를 한 캔버스(589x374)에 맞춰 놓은 프레임에서 문 가운데·땅 높이 픽셀(아래에서 잼)과,
        // 그 프레임이 게임에서 가져야 할 PPU. 위압감이 느껴지도록 문틀 키 342px가 약 6.8유닛(네크로시아 키의 4배 남짓),
        // 날개를 편 악룡은 폭 약 11유닛이 된다. 임포트 설정이 달라도 이 값으로 맞춰 그린다.
        private static readonly Vector2 GateAnchorPixel = new Vector2(290f, 19f);
        private const float GatePixelsPerUnit = 50f;
        private const int GateSortingOrder = -1; // 배경/발판(-5 이하)보다 앞, 캐릭터(0)보다 뒤

        [Header("연출: 땅을 타고 번지는 충격파 폭발 / 에너지 기둥")]
        [SerializeField] private string waveResourcesPath = "Art/Effects/UltWave";
        [SerializeField] private string pillarResourcesPath = "Art/Effects/UltPillar";
        [SerializeField] private float waveFrameDuration = 0.07f;
        [SerializeField] private float pillarFrameDuration = 0.06f;
        [SerializeField] private float pillarRiseDuration = 0.1f;   // 기둥 시트엔 솟는 장면이 없어서 땅에서 위로 늘이며 솟게 한다
        [SerializeField] private float burstFadeDuration = 0.2f;    // 마지막 프레임에서 흐려지며 사라지는 시간
        [SerializeField] private float explosionOffset = 1.5f;      // 문 가운데에서 이만큼 앞(악룡 입 바로 앞)에서 크게 터진다
        [SerializeField] private float explosionScale = 1.8f;       // 큰 폭발은 충격파 그림을 이만큼 키워 쓴다
        [SerializeField] private float explosionPillarScale = 1.3f; // 큰 폭발 가운데에서 함께 솟는 에너지 기둥 크기
        // 큰 폭발이 타오르는 동안 충격파 그림에서 가장 크게 부푼 5~7프레임(0부터 4~6)을 왔다 갔다 한다.
        [SerializeField] private int explosionHoldFirstFrame = 4;
        [SerializeField] private int explosionHoldLastFrame = 6;
        // 큰 폭발 자리에서 양옆으로 이 거리만큼 떨어진 곳에 충격파가 차례로 터진다(첫 번째, 두 번째).
        [SerializeField] private float[] ringDistances = { 6f, 10.5f };
        [SerializeField] private float ringDelay = 0.4f;            // 큰 폭발 → 첫 충격파 → 두 번째 충격파 사이 간격
        [SerializeField] private float explosionShakeDuration = 0.5f;
        [SerializeField] private float explosionShakeMagnitude = 0.4f;
        [SerializeField] private float cameraSweepSmoothTime = 0.12f;
        [SerializeField] private float cameraReturnSmoothTime = 0.35f;
        // 충격파(508x385 칸)·기둥(498x352 칸)의 땅 한가운데 픽셀(아래에서 잼)과 게임 속 크기(PPU 60: 충격파 폭 약 7유닛, 기둥 키 약 5.8유닛).
        private static readonly Vector2 WaveAnchorPixel = new Vector2(292f, 64f);
        private static readonly Vector2 PillarAnchorPixel = new Vector2(251f, 30f);
        private const float BurstPixelsPerUnit = 60f;
        private const int BurstSortingOrder = 2; // 몬스터(0) 위로 터진다

        [Header("연출: 화면 흔들림")]
        [SerializeField] private float shakeDuration = 0.25f;
        [SerializeField] private float shakeMagnitude = 0.2f;
        [SerializeField] private float burstShakeDuration = 0.15f;
        [SerializeField] private float burstShakeMagnitude = 0.12f;

        private Character character;
        private CharacterMovement2D movement;
        private SpriteRenderer visualRenderer;
        private WalkSpriteAnimator walkAnimator;
        private JumpSpriteAnimator jumpAnimator;
        private Sprite[] readyFrames;
        private Sprite[] gateFrames;
        private Sprite[] dragonRoarFrames;
        private Sprite[] waveFrames;
        private Sprite[] pillarFrames;

        private float cooldownRemaining;
        private bool isActive;

        public float CooldownRemaining => cooldownRemaining;
        public float Cooldown => cooldown;

        public void ResetCooldown() => cooldownRemaining = 0f;
        public bool IsActive => isActive;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();

            var visual = transform.Find("Visual");
            visualRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();
            walkAnimator = GetComponent<WalkSpriteAnimator>();
            jumpAnimator = GetComponent<JumpSpriteAnimator>();

            readyFrames = LoadSorted(readyResourcesPath);
            gateFrames = LoadSorted(gateResourcesPath);
            dragonRoarFrames = LoadSorted(dragonRoarResourcesPath);
            waveFrames = LoadSorted(waveResourcesPath);
            pillarFrames = LoadSorted(pillarResourcesPath);
        }

        private static Sprite[] LoadSorted(string path)
        {
            var frames = Resources.LoadAll<Sprite>(path);
            if (frames != null && frames.Length > 0)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            return frames;
        }

        private void Update()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            }

            if (movement.IsInputLocked) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                TryUseSkill();
            }
        }

        private void TryUseSkill()
        {
            if (character.IsCombatLocked) return;
            // 땅에서 문이 솟는 연출이라 땅을 딛고 있을 때만 쓸 수 있다.
            if (!movement.IsGrounded) return;
            if (isActive || cooldownRemaining > 0f) return;
            if (!character.TrySpendMana(manaCost)) return;

            cooldownRemaining = cooldown;
            StartCoroutine(UltimateRoutine());
        }

        private IEnumerator UltimateRoutine()
        {
            isActive = true;
            movement.SetInputLocked(true);
            character.SetInvincible(true);
            yield return PlayReadyMotion();

            var cameraFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cameraFollow != null) cameraFollow.Shake(shakeDuration, shakeMagnitude);

            // 낫을 꽂은 자세 그대로 앞에서 악마의 문이 솟고, 악룡이 나와 입을 크게 벌리고 포효한다.
            float facing = movement.FacingDirection.x < 0f ? -1f : 1f;
            Vector3 feet = visualRenderer != null ? visualRenderer.transform.position : transform.position;
            Vector3 gatePosition = new Vector3(feet.x + facing * gateFrontDistance, feet.y, 0f);
            var summon = CreateSummonRenderer(gatePosition, facing);
            Transform view = new GameObject("UltView").transform;
            view.position = (transform.position + gatePosition) / 2f;
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(view, cameraReturnSmoothTime);
                cameraFollow.ZoomTo(summonCameraSize, summonZoomDuration);
            }
            Coroutine roarLoop = null;
            if (summon != null)
            {
                yield return PlayGate(summon);
                yield return PlayRoarToHold(summon, cameraFollow);
                roarLoop = StartCoroutine(LoopRoar(summon, gatePosition));
            }

            // 포효를 이어 가는 동안 악룡 앞에서 크게 폭발하고, 양옆으로 충격파가 두 번 퍼져 나간다.
            yield return PlayExplosionAndRings(gatePosition, facing, view, cameraFollow);
            if (roarLoop != null) StopCoroutine(roarLoop);
            if (summon != null) summon.transform.parent.position = gatePosition;

            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(transform, cameraReturnSmoothTime);
                StartCoroutine(ResetCameraSmoothing(cameraFollow));
            }
            if (view != null) Destroy(view.gameObject);

            if (visualRenderer != null) SkillFrameNormalizer.Reset(visualRenderer.transform);
            if (walkAnimator != null) walkAnimator.enabled = true;
            if (jumpAnimator != null) jumpAnimator.enabled = true;
            character.SetInvincible(false);
            movement.SetInputLocked(false);
            isActive = false;

            // 악룡이 문 안으로 물러나고 문이 닫히며 땅속으로 가라앉는다. 다 사라지면 화면 크기를 되돌린다.
            if (summon != null) yield return PlayRetreat(summon);
            if (cameraFollow != null) cameraFollow.ResetZoom(summonZoomDuration);
        }

        // 준비 모션: 걷기/점프 애니메이터를 꺼 두고 12프레임을 한 번 재생한다. 마지막 프레임(낫을 땅에 꽂은 자세)을
        // 소환과 충격파가 끝날 때까지 그대로 들고 있으므로, 애니메이터와 스케일은 그 뒤에 되돌린다.
        private IEnumerator PlayReadyMotion()
        {
            if (readyFrames == null || readyFrames.Length == 0 || visualRenderer == null) yield break;

            if (walkAnimator != null) walkAnimator.enabled = false;
            if (jumpAnimator != null) jumpAnimator.enabled = false;
            visualRenderer.flipX = movement.FacingDirection.x < 0f;

            var sequence = new List<Sprite>();
            for (int i = 0; i < readyFrames.Length; i++)
            {
                sequence.Add(readyFrames[i]);
                // 수평으로 돌리는 구간의 마지막 프레임까지 갔으면, 그 구간을 남은 횟수만큼 한 번 더 돌린다.
                if (i == readySpinLastFrame && readySpinFirstFrame <= readySpinLastFrame && readySpinLastFrame < readyFrames.Length)
                {
                    for (int loop = 1; loop < readySpinLoops; loop++)
                    {
                        for (int j = readySpinFirstFrame; j <= readySpinLastFrame; j++) sequence.Add(readyFrames[j]);
                    }
                }
            }

            foreach (var frame in sequence)
            {
                visualRenderer.sprite = frame;
                SkillFrameNormalizer.Apply(visualRenderer.transform, frame, readyTargetHeight);
                yield return new WaitForSeconds(readyFrameDuration);
            }
        }

        // ---- 악마의 문 / 악룡 ----

        // 문이 솟을 땅(gatePosition)에 소환 그림을 놓는다. 캐릭터보다 뒤에 그려서 문과 악룡 앞에 네크로시아가 서 있는 모습이 된다.
        // 원화의 악룡은 오른쪽을 보고 있어서, 왼쪽을 보고 쓰면 좌우를 뒤집어 악룡도 네크로시아와 같은 쪽(적 쪽)을 보게 한다.
        private SpriteRenderer CreateSummonRenderer(Vector3 gatePosition, float facing)
        {
            if (gateFrames == null || gateFrames.Length == 0) return null;
            var renderer = CreateAnchoredRenderer("UltSummon", gatePosition, GateSortingOrder);
            renderer.flipX = facing < 0f;
            SetAnchoredFrame(renderer, gateFrames[0], GateAnchorPixel, GatePixelsPerUnit);
            return renderer;
        }

        private IEnumerator PlayGate(SpriteRenderer summon)
        {
            for (int i = 0; i < gateFrames.Length; i++)
            {
                if (summon == null) yield break;
                SetAnchoredFrame(summon, gateFrames[i], GateAnchorPixel, GatePixelsPerUnit);
                float frameDuration = i < 6 ? gateRiseFrameDuration : i < 10 ? gateGlowFrameDuration : gateEmergeFrameDuration;
                yield return new WaitForSeconds(frameDuration);
            }
        }

        // 포효 프레임을 입을 가장 크게 벌리는 프레임까지 재생하고, 그 순간 화면을 크게 흔든다. 그 프레임은 그대로 둔다(유지).
        private IEnumerator PlayRoarToHold(SpriteRenderer summon, CameraFollow cameraFollow)
        {
            if (dragonRoarFrames == null || dragonRoarFrames.Length == 0) yield break;
            int hold = Mathf.Clamp(roarHoldFrame, 0, dragonRoarFrames.Length - 1);
            for (int i = 0; i <= hold; i++)
            {
                if (summon == null) yield break;
                SetAnchoredFrame(summon, dragonRoarFrames[i], GateAnchorPixel, GatePixelsPerUnit);
                if (i == hold)
                {
                    if (cameraFollow != null) cameraFollow.Shake(roarShakeDuration, roarShakeMagnitude);
                    SfxPlayer.Play(SfxPlayer.SandDragonRoar);
                }
                yield return new WaitForSeconds(roarFrameDuration);
            }
        }

        // 입을 벌린 프레임들 사이를 왔다 갔다(4→5→6→5→4…) 하며 몸을 살짝 떤다. 바깥에서 멈춘다.
        private IEnumerator LoopRoar(SpriteRenderer summon, Vector3 basePosition)
        {
            if (dragonRoarFrames == null || dragonRoarFrames.Length == 0) yield break;
            int first = Mathf.Clamp(roarLoopFirstFrame, 0, dragonRoarFrames.Length - 1);
            int last = Mathf.Clamp(roarLoopLastFrame, first, dragonRoarFrames.Length - 1);
            int index = Mathf.Clamp(roarHoldFrame, first, last);
            int step = 1;
            var root = summon.transform.parent;
            while (summon != null)
            {
                SetAnchoredFrame(summon, dragonRoarFrames[index], GateAnchorPixel, GatePixelsPerUnit);
                if (last > first)
                {
                    if (index + step > last || index + step < first) step = -step;
                    index += step;
                }
                for (float t = 0f; t < roarLoopFrameDuration && summon != null; t += Time.deltaTime)
                {
                    root.position = basePosition + (Vector3)(Random.insideUnitCircle * roarTremble);
                    yield return null;
                }
            }
        }

        // 사라지기: 포효를 거꾸로 되감아 입을 다물고, 악룡 등장(문 열림)·문 틈 빛·문 솟음을 거꾸로 되감아
        // 악룡이 문 안으로 물러나고 문이 닫혀 땅속으로 가라앉게 한다.
        private IEnumerator PlayRetreat(SpriteRenderer summon)
        {
            if (dragonRoarFrames != null && dragonRoarFrames.Length > 0)
            {
                for (int i = Mathf.Clamp(roarHoldFrame, 0, dragonRoarFrames.Length - 1); i >= 0; i--)
                {
                    if (summon == null) yield break;
                    SetAnchoredFrame(summon, dragonRoarFrames[i], GateAnchorPixel, GatePixelsPerUnit);
                    yield return new WaitForSeconds(roarFrameDuration);
                }
            }
            for (int i = gateFrames.Length - 1; i >= 0; i--)
            {
                if (summon == null) yield break;
                SetAnchoredFrame(summon, gateFrames[i], GateAnchorPixel, GatePixelsPerUnit);
                yield return new WaitForSeconds(gateRetreatFrameDuration);
            }
            // 마지막 프레임(땅에 남은 연기)만 살짝 흐려지며 사라진다.
            yield return FadeAndDestroy(summon, burstFadeDuration);
        }

        // ---- 충격파 / 에너지 기둥 ----

        // 악룡 입 바로 앞에서 큰 폭발(커다란 충격파 + 에너지 기둥)이 한 번 터지고, ringDelay마다 그 자리 양옆
        // ringDistances 거리에 충격파가 한 쌍씩 터진다. 카메라는 폭발 자리를 비춘다. 마지막 충격파가 다 사라지면 끝난다.
        private IEnumerator PlayExplosionAndRings(Vector3 gatePosition, float facing, Transform view, CameraFollow cameraFollow)
        {
            Rect bounds = GameBootstrap.CurrentMapBounds;
            var center = new Vector3(gatePosition.x + facing * explosionOffset, gatePosition.y, 0f);
            if (cameraFollow != null && view != null)
            {
                cameraFollow.SetTarget(view, cameraSweepSmoothTime);
                view.position = new Vector3(center.x, view.position.y, 0f);
            }

            explosionHolding = true;
            StartCoroutine(PlayHeldPillar(center, facing));
            StartCoroutine(PlayHeldExplosion(center, facing));
            if (cameraFollow != null) cameraFollow.Shake(explosionShakeDuration, explosionShakeMagnitude);

            foreach (float distance in ringDistances)
            {
                yield return new WaitForSeconds(ringDelay);
                bool any = false;
                foreach (float side in new[] { -1f, 1f })
                {
                    float x = center.x + side * distance;
                    // 맵 밖으로 통째로 벗어나는 충격파는 터뜨리지 않는다.
                    if (x + burstHalfWidth < bounds.xMin || x - burstHalfWidth > bounds.xMax + 1f) continue;
                    // 그림의 충격파가 바깥쪽으로 퍼져 나가 보이도록 왼쪽 것은 좌우를 뒤집는다.
                    StartCoroutine(PlayBurst(false, new Vector3(x, center.y, 0f), side, 1f, burstHalfWidth, burstHits, burstHitMultiplier));
                    any = true;
                }
                if (any && cameraFollow != null) cameraFollow.Shake(burstShakeDuration, burstShakeMagnitude);
            }

            // 마지막 충격파가 다 사라지면 큰 폭발도 꺼지기 시작한다.
            int waveFrameCount = waveFrames != null ? waveFrames.Length : 0;
            yield return new WaitForSeconds(waveFrameCount * waveFrameDuration + burstFadeDuration);
            explosionHolding = false;
            yield return new WaitForSeconds(waveFrameDuration * 2f + burstFadeDuration);
        }

        private bool explosionHolding;

        // 큰 폭발: 가장 크게 부푼 프레임까지 터진 뒤 explosionHolding이 꺼질 때까지 그 구간을 왔다 갔다 하며 타오르고,
        // 그 뒤 나머지 프레임을 재생하며 사라진다. 타오르는 내내 explosionTickInterval마다 범위 안 몬스터를 친다.
        private IEnumerator PlayHeldExplosion(Vector3 position, float facing)
        {
            if (waveFrames == null || waveFrames.Length == 0) yield break;
            int last = waveFrames.Length - 1;
            int holdFirst = Mathf.Clamp(explosionHoldFirstFrame, 0, last);
            int holdLast = Mathf.Clamp(explosionHoldLastFrame, holdFirst, last);
            float pixelsPerUnit = BurstPixelsPerUnit / Mathf.Max(0.01f, explosionScale);

            var renderer = CreateAnchoredRenderer("UltExplosion", position, BurstSortingOrder);
            renderer.flipX = facing < 0f;
            float tick = explosionTickInterval; // 첫 프레임이 지나면 바로 한 번 친다
            int index = 0;
            int step = 1;
            bool rising = true;
            while (true)
            {
                if (renderer == null) yield break;
                SetAnchoredFrame(renderer, waveFrames[index], WaveAnchorPixel, pixelsPerUnit);
                for (float t = 0f; t < waveFrameDuration; t += Time.deltaTime)
                {
                    if (index > 0 && (rising || explosionHolding))
                    {
                        tick += Time.deltaTime;
                        if (tick >= explosionTickInterval)
                        {
                            tick -= explosionTickInterval;
                            DamageArea(position, explosionHalfWidth, explosionTickMultiplier);
                        }
                    }
                    yield return null;
                }

                if (rising)
                {
                    if (index < holdLast) { index++; continue; }
                    rising = false;
                }
                if (explosionHolding && holdLast > holdFirst)
                {
                    // 가장 부푼 구간을 왔다 갔다 한다.
                    if (index + step > holdLast || index + step < holdFirst) step = -step;
                    index += step;
                    continue;
                }
                if (explosionHolding) continue;
                // 꺼지기: 나머지 프레임을 재생하고 흐려진다.
                if (index < last) { index = Mathf.Max(index + 1, holdLast + 1 <= last ? holdLast + 1 : last); continue; }
                break;
            }
            yield return FadeAndDestroy(renderer, burstFadeDuration);
        }

        // 큰 폭발 가운데의 에너지 기둥: 땅에서 솟은 뒤 explosionHolding이 꺼질 때까지 프레임을 되풀이한다(피해는 큰 폭발이 준다).
        private IEnumerator PlayHeldPillar(Vector3 position, float facing)
        {
            if (pillarFrames == null || pillarFrames.Length == 0) yield break;
            float pixelsPerUnit = BurstPixelsPerUnit / Mathf.Max(0.01f, explosionPillarScale);
            var renderer = CreateAnchoredRenderer("UltPillar", position, BurstSortingOrder);
            renderer.flipX = facing < 0f;
            var root = renderer.transform.parent;
            float riseTime = 0f;
            int index = 0;
            while (renderer != null && (explosionHolding || index < pillarFrames.Length))
            {
                SetAnchoredFrame(renderer, pillarFrames[index % pillarFrames.Length], PillarAnchorPixel, pixelsPerUnit);
                for (float t = 0f; t < pillarFrameDuration; t += Time.deltaTime)
                {
                    riseTime += Time.deltaTime;
                    if (root != null) root.localScale = new Vector3(1f, Mathf.Clamp01(riseTime / pillarRiseDuration), 1f);
                    yield return null;
                }
                index++;
                // 되풀이하는 동안은 번호가 끝없이 커지지 않게 한 바퀴 안으로 되돌린다.
                if (explosionHolding && index >= pillarFrames.Length) index -= pillarFrames.Length;
            }
            if (renderer != null) yield return FadeAndDestroy(renderer, burstFadeDuration);
        }

        // scale: 그림 크기 배율. hits가 0이면 그림만 보여 주고 피해는 주지 않는다.
        private IEnumerator PlayBurst(bool pillar, Vector3 position, float facing, float scale, float halfWidth, int hits, float multiplier)
        {
            var frames = pillar ? pillarFrames : waveFrames;
            if (frames == null || frames.Length == 0) yield break;
            Vector2 anchor = pillar ? PillarAnchorPixel : WaveAnchorPixel;
            float frameDuration = pillar ? pillarFrameDuration : waveFrameDuration;
            float pixelsPerUnit = BurstPixelsPerUnit / Mathf.Max(0.01f, scale);

            var renderer = CreateAnchoredRenderer(pillar ? "UltPillar" : "UltWave", position, BurstSortingOrder);
            renderer.flipX = facing < 0f;
            var root = renderer.transform.parent;
            float riseTime = 0f;
            int hitsDone = 0;
            for (int i = 0; i < frames.Length; i++)
            {
                if (renderer == null) yield break;
                SetAnchoredFrame(renderer, frames[i], anchor, pixelsPerUnit);
                // 터지는 동안 고르게 hits번 친다(1번째 프레임 다음부터 시작해 마지막 사라지는 프레임 전에 끝낸다).
                if (hitsDone < hits && i >= 1 && i >= 1 + hitsDone * Mathf.Max(1, (frames.Length - 2) / Mathf.Max(1, hits)))
                {
                    DamageArea(position, halfWidth, multiplier);
                    hitsDone++;
                }
                for (float t = 0f; t < frameDuration; t += Time.deltaTime)
                {
                    // 기둥은 처음 잠깐 땅에서 위로 늘어나며 솟는다(부모를 세로로 늘이므로 땅 높이는 그대로).
                    if (pillar && root != null)
                    {
                        riseTime += Time.deltaTime;
                        root.localScale = new Vector3(1f, Mathf.Clamp01(riseTime / pillarRiseDuration), 1f);
                    }
                    yield return null;
                }
            }
            yield return FadeAndDestroy(renderer, burstFadeDuration);
        }

        // ---- 그리기 도우미 ----

        // position(땅 위 한 점)에 부모를 두고 그 아래에 그림을 그리는 렌더러를 만든다. 캐릭터와 같은 정렬 레이어를 쓴다.
        private SpriteRenderer CreateAnchoredRenderer(string name, Vector3 position, int sortingOrder)
        {
            var root = new GameObject(name).transform;
            root.position = position;
            var art = new GameObject("Art").transform;
            art.SetParent(root, false);
            var renderer = art.gameObject.AddComponent<SpriteRenderer>();
            if (visualRenderer != null) renderer.sortingLayerID = visualRenderer.sortingLayerID;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        // 프레임 임포트 PPU/기준점이 무엇이든 pixelsPerUnit 크기로 그리고, anchorPixel(아래 왼쪽에서 잰 픽셀)이
        // 부모 위치(땅 위 한 점)에 오게 한다. 좌우가 뒤집혀 있으면 기준 픽셀도 반대쪽으로 잰다.
        private static void SetAnchoredFrame(SpriteRenderer renderer, Sprite frame, Vector2 anchorPixel, float pixelsPerUnit)
        {
            renderer.sprite = frame;
            float scale = frame.pixelsPerUnit / pixelsPerUnit;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            Vector2 offsetPixels = frame.pivot - anchorPixel;
            if (renderer.flipX) offsetPixels.x = -offsetPixels.x;
            renderer.transform.localPosition = offsetPixels / pixelsPerUnit;
        }

        private IEnumerator FadeAndDestroy(SpriteRenderer renderer, float fadeDuration)
        {
            for (float t = 0f; t < fadeDuration && renderer != null; t += Time.deltaTime)
            {
                renderer.color = new Color(1f, 1f, 1f, 1f - t / fadeDuration);
                yield return null;
            }
            if (renderer != null) Destroy(renderer.transform.parent.gameObject);
        }

        // 카메라가 캐릭터에게 다 돌아온 뒤 원래 따라가는 속도로 되돌린다.
        private IEnumerator ResetCameraSmoothing(CameraFollow cameraFollow)
        {
            yield return new WaitForSeconds(cameraReturnSmoothTime * 2f);
            if (cameraFollow != null) cameraFollow.ResetSmoothTime();
        }

        // 폭발이 터진 땅(ground) 좌우 halfWidth, 위로 burstHeight 안에 몸이 걸친 몬스터를 한 번씩 친다.
        private void DamageArea(Vector3 ground, float halfWidth, float multiplier)
        {
            Vector2 center = new Vector2(ground.x, ground.y + burstHeight / 2f - 0.5f);
            Vector2 size = new Vector2(halfWidth * 2f, burstHeight + 1f);
            var hit = new HashSet<Monster>();
            foreach (var col in Physics2D.OverlapBoxAll(center, size, 0f))
            {
                var monster = col.GetComponentInParent<Monster>();
                if (monster == null || monster.IsDead || !hit.Add(monster)) continue;
                character.DealDamage(monster, multiplier, isSkill: true);
            }
        }
    }
}
