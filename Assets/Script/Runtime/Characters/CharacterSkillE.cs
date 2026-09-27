using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Combat;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // E = 사슬 폭풍. 그 자리에 앉아(ESkill 5프레임) 땅에 사슬을 풀어 넣으면, 바로 앞에 있는 적의 발밑에서
    // 사슬 회오리가 솟아오른다(ChainStormStart 앞부분 → ChainStormLoop 반복 → ChainStormStart 끝부분으로 사그라듦).
    // 회오리가 도는 동안(1.5초) 범위 안의 모든 적에게 짧은 간격으로 연타 피해를 주며, 맞은 적은 그동안
    // 속박되어 움직이지 못한다(연타가 끝나면 풀린다). 앞에 적이 없으면 바라보는 방향 앞쪽 바닥에 솟는다.
    // 스킬을 쓰는 동안(앉기~일어나기)은 무적이고 이동할 수 없다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterSkillE : MonoBehaviour, ISkillCooldownReset
    {
        [SerializeField] private float manaCost = 65f;
        [SerializeField] private float cooldown = 15f;

        [Header("앉는 동작 (ESkill)")]
        [SerializeField] private string seatingFramesPath = "Art/Necrosia/ESkill";
        [SerializeField] private float seatingFrameDuration = 0.07f;
        [SerializeField] private float standUpFrameDuration = 0.05f;

        [Header("사슬 폭풍 위치")]
        [SerializeField] private float targetSearchRange = 5f;   // 이 거리 안의 앞쪽 적 중 가장 가까운 적 발밑에 솟는다
        [SerializeField] private float fallbackDistance = 2f;    // 앞에 적이 없을 때 솟는 거리

        [Header("사슬 폭풍 연출")]
        [SerializeField] private string stormStartPath = "Art/Effects/ChainStormStart";
        [SerializeField] private string stormLoopPath = "Art/Effects/ChainStormLoop";
        [SerializeField] private int stormRiseFrameCount = 6;     // ChainStormStart 앞 6프레임은 솟아오르기, 나머지는 사그라들기
        [SerializeField] private float stormRiseFrameDuration = 0.05f;
        [SerializeField] private float stormLoopFrameDuration = 0.06f;
        [SerializeField] private float stormFadeFrameDuration = 0.07f;
        [SerializeField] private int stormSortingOrder = 3;       // 몬스터 앞에 그린다

        [Header("연타")]
        [SerializeField] private float hurricaneDuration = 1.5f;
        [SerializeField] private float hitInterval = 0.1f;        // 1.5초 동안 15타
        [SerializeField] private float damageMultiplierPerHit = 0.45f;
        [SerializeField] private Vector2 hitBoxSize = new Vector2(1.6f, 2.6f); // 회오리 발밑 기준 폭 x 높이

        private Character character;
        private CharacterMovement2D movement;
        private Rigidbody2D body;
        private SpriteRenderer visualRenderer;
        private WalkSpriteAnimator walkAnimator;
        private JumpSpriteAnimator jumpAnimator;
        private Sprite[] seatingFrames;
        private Sprite[] stormStartFrames;
        private Sprite[] stormLoopFrames;
        // 회오리에 맞은 적을 잠시 묶어 도망 못 가고 계속 얻어맞게 한다. 연타가 끝나면 전부 풀어준다.
        private readonly HashSet<Monster> boundTargets = new();

        private float cooldownRemaining;
        private bool isBusy;

        public float CooldownRemaining => cooldownRemaining;
        public float Cooldown => cooldown;

        public void ResetCooldown() => cooldownRemaining = 0f;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
            body = GetComponent<Rigidbody2D>();
            walkAnimator = GetComponent<WalkSpriteAnimator>();
            jumpAnimator = GetComponent<JumpSpriteAnimator>();

            var visual = transform.Find("Visual");
            visualRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();

            seatingFrames = LoadSortedFrames(seatingFramesPath);
            stormStartFrames = LoadSortedFrames(stormStartPath);
            stormLoopFrames = LoadSortedFrames(stormLoopPath);
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

        private void Update()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            }

            if (movement.IsInputLocked) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                TryUseSkill();
            }
        }

        private void TryUseSkill()
        {
            if (character.IsCombatLocked) return;
            if (isBusy || cooldownRemaining > 0f) return;
            if (!character.TrySpendMana(manaCost)) return;

            cooldownRemaining = cooldown;
            StartCoroutine(ChainStormRoutine());
        }

        private IEnumerator ChainStormRoutine()
        {
            isBusy = true;
            movement.SetInputLocked(true);
            character.SetInvincible(true);
            body.linearVelocity = new Vector2(0f, body.linearVelocity.y);

            Vector2 direction = movement.FacingDirection;
            BeginPose(direction);

            // 1) 앉으며 땅에 사슬을 풀어 넣는다.
            yield return PlayPose(0, seatingFrames.Length - 1, seatingFrameDuration);

            // 2) 앞에 있는 적 발밑에서 사슬 폭풍이 솟아오르고, 도는 동안 연타한다(앉은 자세 유지).
            Vector2 stormPosition = FindStormPosition(direction);
            yield return StormRoutine(stormPosition);

            // 3) 일어선다(앉는 동작 역재생).
            yield return PlayPose(seatingFrames.Length - 1, 0, standUpFrameDuration);
            EndPose();

            character.SetInvincible(false);
            movement.SetInputLocked(false);
            isBusy = false;
        }

        private IEnumerator StormRoutine(Vector2 position)
        {
            var stormGO = new GameObject("ChainStorm", typeof(SpriteRenderer));
            stormGO.transform.position = position;
            var stormRenderer = stormGO.GetComponent<SpriteRenderer>();
            stormRenderer.sortingOrder = stormSortingOrder;

            int riseCount = Mathf.Clamp(stormRiseFrameCount, 0, stormStartFrames.Length);

            // 솟아오르기
            for (int i = 0; i < riseCount; i++)
            {
                stormRenderer.sprite = stormStartFrames[i];
                yield return new WaitForSeconds(stormRiseFrameDuration);
            }

            // 회오리 + 연타 (hurricaneDuration 동안)
            float elapsed = 0f;
            float frameTimer = 0f;
            float hitTimer = 0f;
            int loopIndex = 0;
            if (stormLoopFrames.Length > 0) stormRenderer.sprite = stormLoopFrames[0];
            DealHit(position);

            while (elapsed < hurricaneDuration)
            {
                yield return null;
                float dt = Time.deltaTime;
                elapsed += dt;
                frameTimer += dt;
                hitTimer += dt;

                if (stormLoopFrames.Length > 0 && frameTimer >= stormLoopFrameDuration)
                {
                    frameTimer -= stormLoopFrameDuration;
                    loopIndex = (loopIndex + 1) % stormLoopFrames.Length;
                    stormRenderer.sprite = stormLoopFrames[loopIndex];
                }

                while (hitTimer >= hitInterval && elapsed < hurricaneDuration + hitInterval * 0.5f)
                {
                    hitTimer -= hitInterval;
                    DealHit(position);
                }
            }

            ReleaseBoundTargets();

            // 사그라들기
            for (int i = riseCount; i < stormStartFrames.Length; i++)
            {
                stormRenderer.sprite = stormStartFrames[i];
                yield return new WaitForSeconds(stormFadeFrameDuration);
            }

            Destroy(stormGO);
        }

        // 바라보는 방향 앞쪽(targetSearchRange 이내)에서 가장 가까운 살아있는 적의 발밑. 없으면 앞쪽 바닥.
        private Vector2 FindStormPosition(Vector2 direction)
        {
            Vector2 self = transform.position;
            Monster closest = null;
            float closestDist = float.MaxValue;

            foreach (var hit in Physics2D.OverlapCircleAll(self, targetSearchRange))
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead) continue;

                float forward = (monster.transform.position.x - self.x) * Mathf.Sign(direction.x);
                if (forward < 0f) continue;

                float dist = Vector2.Distance(self, monster.transform.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = monster;
                }
            }

            if (closest != null) return closest.transform.position;
            return self + new Vector2(Mathf.Sign(direction.x) * fallbackDistance, 0f);
        }

        // 회오리 기둥(발밑에서 위로 hitBoxSize.y) 안의 모든 적에게 한 타씩. 맞은 적은 그 자리에 묶어서
        // 도망가지 못하고 계속 범위 안에서 얻어맞게 한다.
        private void DealHit(Vector2 stormBase)
        {
            Vector2 center = stormBase + Vector2.up * (hitBoxSize.y * 0.5f);
            foreach (var hit in Physics2D.OverlapBoxAll(center, hitBoxSize, 0f))
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead) continue;

                monster.SetImmobilized(true);
                boundTargets.Add(monster);

                float damage = CombatMath.PhysicalDamage(character.Stats.attack * damageMultiplierPerHit, monster.Defense);
                monster.TakeDamage(damage);
            }
        }

        private void ReleaseBoundTargets()
        {
            foreach (var monster in boundTargets)
            {
                if (monster != null) monster.SetImmobilized(false);
            }
            boundTargets.Clear();
        }

        // 걷기/점프 애니메이터가 스프라이트를 덮어쓰지 않도록 잠시 끄고, ESkill 프레임은 폴더 PPU로
        // 크기를 맞춰 두었으므로 Visual 스케일 보정도 원래대로 둔다.
        private void BeginPose(Vector2 direction)
        {
            if (walkAnimator != null) walkAnimator.enabled = false;
            if (jumpAnimator != null) jumpAnimator.enabled = false;
            if (visualRenderer == null) return;
            SkillFrameNormalizer.Reset(visualRenderer.transform);
            visualRenderer.flipX = direction.x < 0f;
        }

        private void EndPose()
        {
            if (walkAnimator != null) walkAnimator.enabled = true;
            if (jumpAnimator != null) jumpAnimator.enabled = true;
        }

        private IEnumerator PlayPose(int from, int to, float frameDuration)
        {
            if (visualRenderer == null || seatingFrames == null || seatingFrames.Length == 0) yield break;

            int step = to >= from ? 1 : -1;
            for (int i = from; ; i += step)
            {
                visualRenderer.sprite = seatingFrames[i];
                yield return new WaitForSeconds(frameDuration);
                if (i == to) break;
            }
        }
    }
}
