using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Combat;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // S = 사슬 갈고리. 전방 멀리 사슬을 던져 적을 락온한 뒤, 이어지는 입력에 따라 갈라진다.
    // Z: 락온한 적을 바로 앞까지 당겨와 공중에 띄운다 (피해 없음, 순수 컨트롤).
    // X: 락온한 적을 향해 돌진하며 관통 피해를 준다.
    // 락온에 실패했으면 Z/X 모두 아무 효과가 없다 (둘 다 락온된 적을 필요로 함).
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    [RequireComponent(typeof(ChainSkillSpriteAnimator))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterSkillS : MonoBehaviour
    {
        [Header("Phase 1: 사슬 던지기")]
        [SerializeField] private float manaCost = 5f;
        [SerializeField] private float cooldown = 4f;
        [SerializeField] private float throwRange = 3.5f; // 실제 스프라이트에 그려진 사슬 길이에 맞춘 사거리
        [SerializeField] private float throwRadius = 0.5f;
        [SerializeField] private float throwDuration = 0.3f;
        [SerializeField] private float missHoldDuration = 0.15f;
        [SerializeField] private float retractDuration = 0.25f;
        [SerializeField] private float followUpWindow = 0.8f;

        [Header("Phase 2-1 (Z): 당겨서 공중에 띄우기")]
        [SerializeField] private float pullInDistance = 0.6f;
        [SerializeField] private float pullInDuration = 0.12f;
        [SerializeField] private float launchHeight = 1.8f;
        [SerializeField] private float launchRiseDuration = 0.18f;
        [SerializeField] private float launchFallDuration = 0.25f;

        [Header("Phase 2-2 (X): 관통 돌진")]
        [SerializeField] private float pierceDamageMultiplier = 2.2f;
        [SerializeField] private float pierceOvershoot = 0.8f;
        [SerializeField] private float pierceDashDuration = 0.15f;
        [SerializeField] private float pierceHitRadius = 0.6f;

        private Character character;
        private CharacterMovement2D movement;
        private Rigidbody2D body;
        private ChainSkillSpriteAnimator chainAnimator;

        private float cooldownRemaining;
        private bool isBusy;

        public float CooldownRemaining => cooldownRemaining;
        public float Cooldown => cooldown;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
            body = GetComponent<Rigidbody2D>();
            chainAnimator = GetComponent<ChainSkillSpriteAnimator>();
        }

        private void Update()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.sKey.wasPressedThisFrame)
            {
                TryUseSkill();
            }
        }

        private void TryUseSkill()
        {
            if (isBusy || cooldownRemaining > 0f) return;
            if (!character.TrySpendMana(manaCost)) return;

            cooldownRemaining = cooldown;
            StartCoroutine(HookRoutine());
        }

        private IEnumerator HookRoutine()
        {
            isBusy = true;
            movement.SetInputLocked(true);

            Vector2 direction = movement.FacingDirection;
            Monster hooked = FindHookTarget(direction);

            yield return chainAnimator.PlayExtend(direction, throwDuration);

            if (hooked != null)
            {
                chainAnimator.StartHold(direction);
                yield return StartCoroutine(WaitForFollowUp(direction, hooked));
            }
            else
            {
                yield return new WaitForSeconds(missHoldDuration);
            }

            yield return chainAnimator.PlayRetract(retractDuration);

            movement.SetInputLocked(false);
            isBusy = false;
        }

        // 바라보는 방향으로 부채꼴에 가까운 범위(60도 이내) 안에서 가장 가까운 몬스터를 찾는다.
        private Monster FindHookTarget(Vector2 direction)
        {
            Vector2 origin = (Vector2)transform.position + direction * (throwRange * 0.5f);
            var hits = Physics2D.OverlapCircleAll(origin, throwRadius + throwRange * 0.5f);

            Monster closest = null;
            float closestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead) continue;

                Vector2 toMonster = (Vector2)monster.transform.position - (Vector2)transform.position;
                if (toMonster.sqrMagnitude > throwRange * throwRange) continue;
                if (Vector2.Dot(toMonster.normalized, direction) < 0.5f) continue;

                float dist = toMonster.magnitude;
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = monster;
                }
            }

            return closest;
        }

        // 사슬이 적에게 꽂힌 동안 Z(당겨서 띄우기)/X(관통 돌진) 입력을 기다린다.
        // 둘 다 락온된 대상이 있을 때만 유효하다 (락온에 실패했으면 아무 입력도 반응하지 않는다).
        private IEnumerator WaitForFollowUp(Vector2 direction, Monster hooked)
        {
            var keyboard = Keyboard.current;
            float elapsed = 0f;

            while (elapsed < followUpWindow)
            {
                if (keyboard != null && hooked != null)
                {
                    if (keyboard.zKey.wasPressedThisFrame)
                    {
                        yield return StartCoroutine(PullAndLaunch(direction, hooked));
                        yield break;
                    }
                    if (keyboard.xKey.wasPressedThisFrame)
                    {
                        yield return StartCoroutine(PierceDash(direction, hooked));
                        yield break;
                    }
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        // Z: 락온한 적을 바로 앞까지 당겨온 뒤, 위로 띄웠다가 다시 내려오게 한다. (피해 없음, 컨트롤 전용)
        // 몬스터에 물리/애니메이션 상태가 없어서 위치를 직접 보간해 "띄워진" 느낌만 표현한다.
        private IEnumerator PullAndLaunch(Vector2 direction, Monster hooked)
        {
            Transform hookedTransform = hooked.transform;

            Vector2 frontPoint = (Vector2)transform.position + direction * pullInDistance;
            Vector2 pullStart = hookedTransform.position;

            float elapsed = 0f;
            while (elapsed < pullInDuration)
            {
                if (hookedTransform == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / pullInDuration);
                hookedTransform.position = Vector2.Lerp(pullStart, frontPoint, t);
                yield return null;
            }

            Vector2 launchStart = hookedTransform != null ? (Vector2)hookedTransform.position : frontPoint;
            Vector2 launchPeak = launchStart + Vector2.up * launchHeight;

            elapsed = 0f;
            while (elapsed < launchRiseDuration)
            {
                if (hookedTransform == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / launchRiseDuration);
                hookedTransform.position = Vector2.Lerp(launchStart, launchPeak, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < launchFallDuration)
            {
                if (hookedTransform == null) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / launchFallDuration);
                hookedTransform.position = Vector2.Lerp(launchPeak, launchStart, t);
                yield return null;
            }
        }

        // X: 락온한 적의 위치를 지나쳐 그 너머까지 돌진하며, 경로에 걸리는 모든 몬스터에게 관통 피해를 준다.
        private IEnumerator PierceDash(Vector2 direction, Monster hooked)
        {
            Vector2 start = body.position;
            Vector2 targetPoint = hooked.transform.position;
            Vector2 end = targetPoint + direction * pierceOvershoot;

            float elapsed = 0f;
            while (elapsed < pierceDashDuration)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
                float t = Mathf.Clamp01(elapsed / pierceDashDuration);
                body.MovePosition(Vector2.Lerp(start, end, t));
            }

            DealPierceDamage(start, end);
        }

        // 시작~끝 지점을 여러 번 샘플링해서 겹치는 몬스터를 모두 모은 뒤, 한 번씩만 데미지를 준다.
        // (적을 뚫고 지나가는 관통 판정을 별도 스윕 캐스트 없이 흉내낸다.)
        private void DealPierceDamage(Vector2 start, Vector2 end)
        {
            var hitMonsters = new HashSet<Monster>();
            const int sampleCount = 4;

            for (int i = 0; i <= sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                Vector2 point = Vector2.Lerp(start, end, t);
                var hits = Physics2D.OverlapCircleAll(point, pierceHitRadius);
                foreach (var hit in hits)
                {
                    var monster = hit.GetComponent<Monster>();
                    if (monster != null && !monster.IsDead) hitMonsters.Add(monster);
                }
            }

            foreach (var monster in hitMonsters)
            {
                float damage = CombatMath.PhysicalDamage(character.Stats.attack * pierceDamageMultiplier, monster.Defense);
                monster.TakeDamage(damage);
            }
        }
    }
}
