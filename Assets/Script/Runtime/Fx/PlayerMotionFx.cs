using System.Collections.Generic;
using UnityEngine;
using Aethoria.Characters;

namespace Aethoria.Fx
{
    // 네크로시아의 움직임 연출(추가 그림 없음):
    //  - 방향 전환: 돌아서는 순간 가로로 살짝 찌그러졌다가 튕기듯 펴진다(스쿼시&스트레치).
    //  - 점프/착지/공중 점프: 발밑(또는 공중)에 먼지가 퍼진다.
    //  - 잔상: 대시 말고도 빠르게 움직이는 동안(넉백·스킬 이동 등) 반투명 잔상을 남긴다. 대시는 원래 잔상을 그대로 쓴다.
    //  - 발소리: 걷는 동안 맵 바닥 재질(Surface)에 맞는 발소리를 낸다.
    // 크기 연출은 Visual 트랜스폼에 곱해서 얹는다. 애니메이터들이 매 프레임 Visual 크기를 정하므로(SkillFrameNormalizer),
    // 프레임 맨 처음에 원래 크기로 되돌리고 맨 마지막(LateUpdate)에 다시 곱한다.
    [DefaultExecutionOrder(-1000)]
    public class PlayerMotionFx : MonoBehaviour
    {
        public static Surface CurrentSurface = Surface.Grass;

        private const float SquashDuration = 0.16f;
        private const float StepInterval = 0.33f;     // 걷기 8프레임 한 바퀴(0.67초)에 두 걸음
        private const float GhostSpeed = 7f;          // 이보다 빠르면 잔상(평소 걷기 4)
        private const float GhostInterval = 0.05f;
        private const float GhostLife = 0.28f;
        private static readonly Color GhostColor = new Color(0.75f, 0.55f, 1f, 0.5f);

        private CharacterMovement2D movement;
        private CharacterSkillDash dash;
        private Transform visual;
        private SpriteRenderer visualRenderer;
        private Collider2D bodyCollider;

        private float lastFacing;
        private float squashTimer = -1f;
        private Vector3 restoreScale;
        private bool scaleApplied;

        private bool wasGrounded = true;
        private float airTime;
        private float minVerticalVelocity;
        private float stepTimer;

        private Vector3 lastPosition;
        private float nextGhostTime;
        private readonly List<(SpriteRenderer sr, float born)> ghosts = new List<(SpriteRenderer, float)>();

        private void Awake()
        {
            movement = GetComponent<CharacterMovement2D>();
            dash = GetComponent<CharacterSkillDash>();
            bodyCollider = GetComponent<Collider2D>();
            visual = transform.Find("Visual");
            if (visual != null) visualRenderer = visual.GetComponent<SpriteRenderer>();
            lastFacing = movement != null ? movement.FacingDirection.x : 1f;
            lastPosition = transform.position;
            LootFx.Target = transform;
        }

        private void OnEnable()
        {
            if (movement != null) movement.OnDoubleJump += HandleDoubleJump;
        }

        private void OnDisable()
        {
            if (movement != null) movement.OnDoubleJump -= HandleDoubleJump;
            RestoreScale();
            foreach (var g in ghosts) if (g.sr != null) Destroy(g.sr.gameObject);
            ghosts.Clear();
        }

        private Vector2 Feet => bodyCollider != null
            ? new Vector2(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y)
            : (Vector2)transform.position;

        private int Order => visualRenderer != null ? visualRenderer.sortingOrder : 0;

        // 프레임 맨 처음: 지난 프레임에 곱해 둔 크기 연출을 걷어 내서 다른 스크립트가 원래 크기를 보게 한다.
        private void Update()
        {
            RestoreScale();
            if (movement == null) return;

            // 방향 전환
            float facing = movement.FacingDirection.x;
            if (Mathf.Sign(facing) != Mathf.Sign(lastFacing) && Mathf.Abs(facing) > 0.01f)
            {
                squashTimer = 0f;
                if (movement.IsGrounded) Dust(Feet, 4, 0.6f, 0.25f, 180f);
            }
            lastFacing = facing;

            // 점프/착지 먼지
            bool grounded = movement.IsGrounded;
            if (!grounded)
            {
                airTime += Time.deltaTime;
                minVerticalVelocity = Mathf.Min(minVerticalVelocity, movement.VerticalVelocity);
            }
            if (wasGrounded && !grounded && movement.VerticalVelocity > 1f)
            {
                Dust(Feet, 7, 1.4f, 0.35f, 180f);
            }
            else if (!wasGrounded && grounded && airTime > 0.18f)
            {
                float impact = Mathf.Clamp01(-minVerticalVelocity / 18f);
                Dust(Feet, 8 + Mathf.RoundToInt(impact * 8f), 1.6f + impact * 2f, 0.35f + impact * 0.25f, 180f);
                squashTimer = impact > 0.35f ? 0f : squashTimer;
            }
            if (grounded) { airTime = 0f; minVerticalVelocity = 0f; }
            wasGrounded = grounded;

            // 발소리
            if (grounded && movement.IsMoving && !movement.IsInputLocked)
            {
                stepTimer -= Time.deltaTime;
                if (stepTimer <= 0f)
                {
                    stepTimer = StepInterval;
                    ProceduralAudio.PlayFootstep(CurrentSurface);
                }
            }
            else
            {
                stepTimer = 0.08f; // 걷기 시작하자마자 첫 발소리
            }

            UpdateGhosts();
        }

        private void LateUpdate()
        {
            // 빠른 이동 잔상(대시는 자체 잔상이 있어서 뺀다)
            Vector3 position = transform.position;
            float speed = Time.deltaTime > 0f ? (position - lastPosition).magnitude / Time.deltaTime : 0f;
            lastPosition = position;
            bool dashing = dash != null && dash.IsDashing;
            if (!dashing && speed > GhostSpeed && speed < 60f && Time.time >= nextGhostTime && visualRenderer != null && visualRenderer.enabled)
            {
                nextGhostTime = Time.time + GhostInterval;
                SpawnGhost();
            }

            // 크기 연출 얹기
            if (visual == null || squashTimer < 0f) return;
            squashTimer += Time.deltaTime;
            float t = squashTimer / SquashDuration;
            if (t >= 1f) { squashTimer = -1f; return; }
            // 처음엔 납작(가로 0.72, 세로 1.1)했다가 살짝 넘치게 펴진 뒤(가로 1.06) 원래대로.
            float sx = t < 0.45f ? Mathf.Lerp(1f, 0.72f, Mathf.Sin(t / 0.45f * Mathf.PI * 0.5f))
                                 : Mathf.LerpUnclamped(0.72f, 1f, EaseOutBack((t - 0.45f) / 0.55f));
            float sy = 1f + (1f - sx) * 0.4f;
            restoreScale = visual.localScale;
            visual.localScale = new Vector3(restoreScale.x * sx, restoreScale.y * sy, restoreScale.z);
            scaleApplied = true;
        }

        private void RestoreScale()
        {
            if (!scaleApplied || visual == null) return;
            visual.localScale = restoreScale;
            scaleApplied = false;
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        private void HandleDoubleJump()
        {
            FxUtil.Burst(Feet, new FxUtil.BurstSpec
            {
                count = 10, lifeMin = 0.25f, lifeMax = 0.4f, speedMin = 1.5f, speedMax = 2.5f,
                sizeMin = 0.12f, sizeMax = 0.2f, sizeEnd = 0.3f, drag = 3f,
                colorA = new Color(0.8f, 0.6f, 1f, 0.9f), colorB = new Color(1f, 1f, 1f, 0.8f),
                texture = FxUtil.Sparkle, sortingOrder = Order + 1,
            });
            var ring = FxUtil.MakeSprite("JumpRing", FxUtil.RingSprite, Feet, new Color(0.8f, 0.65f, 1f, 0.8f), Order - 1);
            ring.gameObject.AddComponent<FadeScale>().Init(0.3f, new Vector3(0.4f, 0.15f, 1f), new Vector3(1.6f, 0.5f, 1f));
        }

        private void Dust(Vector2 at, int count, float speed, float size, float arc)
        {
            FxUtil.Burst(at + new Vector2(0f, 0.05f), new FxUtil.BurstSpec
            {
                count = count, lifeMin = 0.35f, lifeMax = 0.6f, speedMin = speed * 0.5f, speedMax = speed,
                sizeMin = size * 0.6f, sizeMax = size, sizeEnd = 1.8f, gravity = -0.05f, drag = 4f,
                arcStart = 0f, arc = arc, radius = 0.15f,
                colorA = DustColor(0.55f), colorB = DustColor(0.35f),
                texture = FxUtil.SoftCircle, sortingOrder = Order + 1,
            });
        }

        private static Color DustColor(float alpha)
        {
            Color c = CurrentSurface switch
            {
                Surface.Sand => new Color(0.86f, 0.74f, 0.52f),
                Surface.Wet => new Color(0.75f, 0.88f, 1f),
                Surface.Stone => new Color(0.72f, 0.7f, 0.74f),
                Surface.Wood => new Color(0.7f, 0.6f, 0.5f),
                _ => new Color(0.78f, 0.76f, 0.66f),
            };
            c.a = alpha;
            return c;
        }

        private void SpawnGhost()
        {
            var go = new GameObject("MotionGhost");
            go.transform.SetPositionAndRotation(visual.position, visual.rotation);
            go.transform.localScale = visual.lossyScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = visualRenderer.sprite;
            sr.flipX = visualRenderer.flipX;
            sr.sharedMaterial = FxUtil.SpriteMaterial;
            sr.sortingLayerID = visualRenderer.sortingLayerID;
            sr.sortingOrder = visualRenderer.sortingOrder - 1;
            sr.color = GhostColor;
            ghosts.Add((sr, Time.time));
        }

        private void UpdateGhosts()
        {
            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                var (sr, born) = ghosts[i];
                float age = Time.time - born;
                if (sr == null || age >= GhostLife)
                {
                    if (sr != null) Destroy(sr.gameObject);
                    ghosts.RemoveAt(i);
                    continue;
                }
                var c = GhostColor;
                c.a *= 1f - age / GhostLife;
                sr.color = c;
            }
        }
    }

    // 정해진 시간 동안 커지며(또는 작아지며) 사라지는 한 장짜리 이펙트.
    public class FadeScale : MonoBehaviour
    {
        private float life, age;
        private Vector3 from, to;
        private SpriteRenderer sr;
        private Color baseColor;
        private bool unscaled;

        public void Init(float duration, Vector3 startScale, Vector3 endScale, bool useUnscaledTime = false)
        {
            life = duration;
            from = startScale;
            to = endScale;
            unscaled = useUnscaledTime;
            sr = GetComponent<SpriteRenderer>();
            baseColor = sr != null ? sr.color : Color.white;
            transform.localScale = from;
        }

        private void Update()
        {
            age += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(age / Mathf.Max(0.01f, life));
            transform.localScale = Vector3.Lerp(from, to, 1f - (1f - t) * (1f - t));
            if (sr != null) { var c = baseColor; c.a *= 1f - t; sr.color = c; }
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
