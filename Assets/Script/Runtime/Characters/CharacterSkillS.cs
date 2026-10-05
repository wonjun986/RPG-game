using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
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
    public class CharacterSkillS : MonoBehaviour, ISkillCooldownReset
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

        [Header("사슬 이펙트 (전방으로 뻗어나가는 사슬 스프라이트)")]
        [SerializeField] private string chainEffectPath = "Art/Effects/ChainThrow";
        [SerializeField] private float chainEffectFrameDuration = 0.03f;
        // 사거리에 맞춰 가로 스케일을 줄이면 원본 사슬이 가늘어서 잘 안 보이길래, 세로(굵기)는
        // 별도로 더 크게 키운다.
        [SerializeField] private float chainThicknessMultiplier = 3.5f;
        // 사슬이 시작되는 손 위치(발밑 기준, x는 바라보는 방향 쪽). SSkill 마지막 프레임(팔을 뻗은 자세)에서
        // 손바닥 위치를 기준 키 1.5유닛으로 환산한 값: 앞으로 약 0.66, 위로 약 1.16.
        [SerializeField] private Vector2 handOffset = new Vector2(0.62f, 1.15f);

        [Header("Phase 2-1 (Z): 당겨서 공중에 띄우기")]
        [SerializeField] private float pullInDistance = 0.6f;
        [SerializeField] private float pullInDuration = 0.12f;
        [SerializeField] private float launchHeight = 1.8f;
        [SerializeField] private float launchRiseDuration = 0.18f;
        [SerializeField] private float launchFallDuration = 0.25f;
        [SerializeField] private float zFinisherAnimDuration = 0.35f;

        [Header("Phase 2-2 (X): 관통 돌진")]
        [SerializeField] private float pierceDamageMultiplier = 2.2f;
        [SerializeField] private float pierceOvershoot = 0.8f;
        [SerializeField] private float pierceDashDuration = 0.15f;
        [SerializeField] private float pierceHitRadius = 0.6f;

        private Character character;
        private CharacterMovement2D movement;
        private Rigidbody2D body;
        private ChainSkillSpriteAnimator chainAnimator;
        private FlipbookSpriteEffect chainEffect;

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
            if (character.IsCombatLocked) return;
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
            // 갈고리가 날아가는 동안에도 대상이 움직이면 사슬 길이 계산이 어긋나 보이므로,
            // 락온에 성공한 즉시(던지는 단계부터) 고정해 둔다.
            if (hooked != null) hooked.SetImmobilized(true);

            CreateChain(direction);

            // 1) 던지기: 갈고리가 손에서 튀어나가 적(없으면 최대 사거리)까지 빠르게 날아가다 끝에서 감속한다.
            float reach = hooked != null ? Mathf.Min(DistanceToHookPoint(hooked), chainFullLength) : throwRange;
            chainAimTarget = hooked;
            StartCoroutine(TweenChainLength(0f, reach, throwDuration, easeOut: true));
            yield return chainAnimator.PlayExtend(direction, throwDuration);

            if (hooked != null)
            {
                // 2) 꽂힌 동안: 사슬 끝이 매 프레임 적을 따라간다(당겨오거나 돌진하면 사슬이 그만큼 짧아진다).
                // 대상은 락온된 순간부터 이미 고정돼 있다(위 참고).
                chainFollowTarget = true;
                chainAnimator.StartHold(direction);
                yield return StartCoroutine(WaitForFollowUp(direction, hooked));
                chainFollowTarget = false;
                if (hooked != null) hooked.SetImmobilized(false);
            }
            else
            {
                yield return new WaitForSeconds(missHoldDuration);
            }

            // 3) 회수: 지금 길이에서 손 쪽으로 점점 빨라지며 감겨 들어온다.
            StartCoroutine(TweenChainLength(chainLength, 0f, retractDuration, easeOut: false));
            yield return chainAnimator.PlayRetract(retractDuration);

            DestroyChain();
            movement.SetInputLocked(false);
            isBusy = false;
        }

        // 사슬 연출 구조: 손 위치에 놓인 ChainRoot(바라보는 방향/적 쪽으로 회전) 아래에
        //   - 원본 크기 그대로의 사슬 스프라이트(갈고리 끝이 root 기준 +chainLength 지점에 오도록 미끄러짐)
        //   - 손 앞쪽(root 기준 x >= 0)만 보여주는 SpriteMask
        // 를 둔다. 사슬을 늘였다 줄이는 대신 손에서 밀려 나왔다가 손 안으로 빨려 들어가는 것처럼 보인다.
        private GameObject chainRoot;
        private Transform chainSpriteTransform;
        private float chainSpriteScale;
        private float chainNativeMinX;
        private float chainFullLength;
        private float chainSign;
        private float chainLength;
        private Monster chainAimTarget;
        private bool chainFollowTarget;
        private static Sprite maskSprite;

        private void CreateChain(Vector2 direction)
        {
            DestroyChain();
            chainSign = direction.x >= 0f ? 1f : -1f;
            chainLength = 0f;
            chainAimTarget = null;
            chainFollowTarget = false;

            chainRoot = new GameObject("ChainRoot");
            chainRoot.transform.localScale = new Vector3(chainSign, 1f, 1f);

            chainEffect = FlipbookSpriteEffect.Create("ChainProjectile", chainEffectPath, Vector2.zero, chainEffectFrameDuration, loop: true);
            chainSpriteTransform = chainEffect.transform;
            chainSpriteTransform.SetParent(chainRoot.transform, false);
            var chainRenderer = chainEffect.GetComponent<SpriteRenderer>();
            chainRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            var sprite = chainRenderer.sprite;
            chainNativeMinX = sprite != null ? sprite.bounds.min.x : 0f;
            float nativeWidth = sprite != null && sprite.bounds.size.x > 0f ? sprite.bounds.size.x : 1f;
            chainSpriteScale = throwRange / nativeWidth;
            chainFullLength = nativeWidth * chainSpriteScale;
            chainSpriteTransform.localScale = new Vector3(chainSpriteScale, chainSpriteScale * chainThicknessMultiplier, 1f);

            var maskGO = new GameObject("ChainMask", typeof(SpriteMask));
            maskGO.transform.SetParent(chainRoot.transform, false);
            const float maskLength = 20f;
            maskGO.transform.localPosition = new Vector3(maskLength * 0.5f, 0f, 0f);
            maskGO.transform.localScale = new Vector3(maskLength, 3f, 1f);
            maskGO.GetComponent<SpriteMask>().sprite = GetMaskSprite();

            UpdateChainTransform();
        }

        private static Sprite GetMaskSprite()
        {
            if (maskSprite != null) return maskSprite;
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            maskSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return maskSprite;
        }

        private void DestroyChain()
        {
            if (chainRoot != null) Destroy(chainRoot);
            chainRoot = null;
            chainEffect = null;
            chainSpriteTransform = null;
        }

        private void OnDisable()
        {
            DestroyChain();
        }

        private IEnumerator TweenChainLength(float from, float to, float duration, bool easeOut)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (chainRoot == null || chainFollowTarget) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = easeOut ? 1f - (1f - t) * (1f - t) : t * t;
                chainLength = Mathf.Lerp(from, to, t);
                yield return null;
            }
            chainLength = to;
        }

        private Vector2 HandPosition() => HandPositionForSign(chainSign);

        private Vector2 HandPositionForSign(float sign)
        {
            return (Vector2)transform.position + new Vector2(sign * handOffset.x, handOffset.y);
        }

        // 갈고리가 꽂히는 지점: 적 발밑보다 조금 위(몸통).
        private static Vector2 HookPoint(Monster monster) => (Vector2)monster.transform.position + Vector2.up * 0.6f;

        private float DistanceToHookPoint(Monster monster) => Vector2.Distance(HandPosition(), HookPoint(monster));

        // 사슬은 손을 따라 움직이고, 겨눈 적이 있으면 그 몸통 쪽으로 기운다(띄워 올리면 위로 따라 올라간다).
        private void LateUpdate()
        {
            if (chainRoot == null) return;

            if (chainFollowTarget && chainAimTarget != null)
            {
                // X 관통 돌진으로 적을 지나쳐 적이 등 뒤로 가면 사슬이 위아래로 꺾여 보이지 않게 손 안으로 거둬들인다.
                chainLength = IsAimTargetInFront()
                    ? Mathf.Min(DistanceToHookPoint(chainAimTarget), chainFullLength)
                    : Mathf.MoveTowards(chainLength, 0f, chainFullLength / retractDuration * Time.deltaTime);
            }
            UpdateChainTransform();
        }

        private bool IsAimTargetInFront() => (HookPoint(chainAimTarget).x - HandPosition().x) * chainSign > 0.05f;

        private void UpdateChainTransform()
        {
            Vector2 hand = HandPosition();
            chainRoot.transform.position = hand;

            float angle = 0f;
            if (chainAimTarget != null && IsAimTargetInFront())
            {
                // 적 몸통 쪽으로 기울인다. root가 좌우 반전(scale.x = -1)돼 있을 때는 회전 방향도 반대로 줘야 같은 쪽으로 기운다.
                Vector2 toTarget = HookPoint(chainAimTarget) - hand;
                angle = Mathf.Atan2(toTarget.y, toTarget.x * chainSign) * Mathf.Rad2Deg * chainSign;
            }
            chainRoot.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // 갈고리 끝(스프라이트 오른쪽 끝)이 root 기준 +chainLength에 오도록 사슬을 민다. 손 뒤로 삐져나온 부분은 마스크가 가린다.
            float length = Mathf.Clamp(chainLength, 0f, chainFullLength);
            chainSpriteTransform.localPosition = new Vector3(length - chainFullLength - chainNativeMinX * chainSpriteScale, 0f, 0f);
        }

        // 바라보는 방향으로 부채꼴에 가까운 범위(60도 이내) 안에서 가장 가까운 몬스터를 찾는다.
        // 거리/각도는 몬스터의 중심(transform.position)이 아니라 콜라이더 표면까지로 잰다.
        // 허수아비처럼 덩치가 큰 대상은 중심이 사거리 밖이어도 화면상 사슬 끝이 몸통에 닿아 보이는데,
        // 중심 기준으로 재면 실제로는 닿아 보여도 락온에 실패하는 것처럼 느껴진다.
        private Monster FindHookTarget(Vector2 direction)
        {
            Vector2 hand = HandPositionForSign(direction.x >= 0f ? 1f : -1f);
            Vector2 origin = (Vector2)transform.position + direction * (throwRange * 0.5f);
            var hits = Physics2D.OverlapCircleAll(origin, throwRadius + throwRange * 0.5f);

            Monster closest = null;
            float closestDist = float.MaxValue;

            foreach (var hit in hits)
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead) continue;

                Vector2 surfacePoint = hit.ClosestPoint(hand);
                Vector2 toSurface = surfacePoint - hand;
                if (toSurface.sqrMagnitude > throwRange * throwRange) continue;
                if (toSurface.sqrMagnitude > 0.0001f && Vector2.Dot(toSurface.normalized, direction) < 0.5f) continue;

                float dist = toSurface.magnitude;
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
            StartCoroutine(chainAnimator.PlayFinisher(zFinisherAnimDuration));

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
                character.DealDamage(monster, pierceDamageMultiplier, isSkill: true);
            }
        }
    }
}
