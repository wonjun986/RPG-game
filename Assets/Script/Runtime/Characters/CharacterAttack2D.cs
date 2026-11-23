using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Bootstrap;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // Z = 기본 공격. 낫이 휘둘러지는 호의 안쪽(몸 앞부터 칼날 끝까지, 발밑부터 머리 위까지) 상자 안에 있는 몬스터를 때린다.
    //
    // 연속기(UseCombo): 1타 위에서 아래로 내려치기(AttackDown 6프레임) → Z를 이어 누르면 2·3타 X자 연속 베기(AttackX 8프레임,
    // 대각선 두 번 + X자 잔상). 1타가 거의 끝나 갈 때나 끝난 직후 잠깐(ComboWindow) 안에 누르면 이어지고, 늦으면 다시 1타부터.
    // 피해는 칼날이 닿는 프레임에 들어간다(누르는 순간이 아니라). UseCombo를 끄면 예전 한 동작 공격(Attack 8프레임)으로 돌아간다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    [RequireComponent(typeof(AttackSpriteAnimator))]
    public class CharacterAttack2D : MonoBehaviour
    {
        // 공격 시트(캔버스 키 364px → 1.5유닛, 발 기준점 x 264)에서 칼날 호가 몸 앞 약 1.1유닛, 발밑 0.1~1.3유닛 높이를 감싼다.
        // 손맛을 위해 칼날 끝보다 0.5유닛쯤 더 멀리까지 닿게 한다. 값은 발 기준점(바라보는 쪽이 +x)에서 잰 거리.
        // 연속기로 바꾸며 범위를 넓혔다: 낫을 머리 위에서 내려치고 X자로 크게 베는 그림이라 앞쪽 2.3유닛, 머리 위 1.9유닛까지,
        // 몸 바로 뒤(-0.2)에 붙은 적도 맞는다.
        [SerializeField] private float attackNear = -0.2f;
        [SerializeField] private float attackFar = 2.3f;
        [SerializeField] private float attackBottom = -0.1f;
        [SerializeField] private float attackTop = 1.9f;
        // 공격 모션(AttackSpriteAnimator, 0.35초) 길이와 맞춰 둔다. 모션이 끝나자마자 다음 공격이 나간다.
        [SerializeField] private float cooldown = 0.35f;
        [SerializeField] private float manaRestoreOnAttack = 5f;

        // ---- 연속기 ----
        public static bool UseCombo = true;

        private const string DownClip = "Art/Necrosia/AttackDown";
        private const string XClip = "Art/Necrosia/AttackX";
        private const string XSlashFx = "Art/Effects/XSlash";
        // 선 자세의 정수리~부츠 밑(px). 대기 모션의 같은 키(약 1.53유닛)로 보이게 맞춘다.
        private const float DownStandPixels = 385f;
        // 285일 땐 대기·1타보다 약 12% 크게 보여서(특징점 매칭으로 잰 얼굴·몸 배율) 그만큼 키웠다.
        private const float XStandPixels = 319f;
        private const float StandTargetHeight = 1.53f;

        private const float DownDuration = 0.34f;    // 6프레임
        private const float DownHitTime = 0.22f;     // 5번째 프레임(낫이 땅을 찍는 순간)
        private const float DownDamage = 1f;
        private const float XDuration = 0.46f;       // 8프레임
        private const float XFirstHitTime = 0.07f;   // 2번째 프레임(첫 대각선)
        private const float XSecondHitTime = 0.25f;  // 5번째 프레임(두 번째 대각선, X가 완성된다)
        // 후딜: 동작의 이 비율이 지나면 제자리 묶임이 풀리고 다음 공격·이동으로 끊을 수 있다(남은 프레임은 건너뛴다).
        private const float RecoveryCancelAt = 0.8f;
        private const float XDamage = 0.75f;          // 두 번 맞으니 한 번은 조금 약하게
        private const float ComboBufferFrom = 0.55f;  // 1타 재생이 이만큼(비율) 지나면 다음 입력을 미리 받아 둔다
        private const float ComboWindow = 0.35f;      // 1타가 끝난 뒤 이 시간 안에 누르면 2·3타로 이어진다
        private const float XSlashFrameTime = 0.05f;
        private const float XSlashHeight = 1.9f;      // 잔상 높이(유닛)
        private static readonly Vector2 XSlashOffset = new Vector2(0.95f, 0.75f); // 발 기준점에서 앞·위로

        // ---- 공중 공격: 공중제비를 돌며 낫으로 몸 둘레를 원형으로 베는 회전 베기(JumpAttack 8프레임) ----
        // 1번 준비 → 2~7번 한 바퀴 회전(보랏빛 원형 베기 고리) → 8번 마무리. 점프 한 번에 한 번만 쓸 수 있고,
        // 쓰는 순간 살짝 떠올라 체공감을 준다. 땅에 닿으면 그 자리에서 끝난다.
        private const string AirClip = "Art/Necrosia/JumpAttack";
        // 캔버스(346px, 회전 중심이 캔버스 한가운데)를 이 키로 맞추면 대기 모션과 얼굴·몸 크기가 같다(특징점 매칭으로 잼).
        private const float AirCanvasPixels = 346f;
        private const float AirCanvasHeight = 1.76f;
        private const float AirDuration = 0.48f;          // 8프레임 × 0.06초
        private static readonly float[] AirHitTimes = { 0.12f, 0.24f, 0.36f }; // 고리가 보이는 3·5·7번째 프레임
        private const float AirDamage = 0.6f;
        private const float AirLift = 3f;                 // 쓰는 순간 최소 상승 속도
        private const float AirRadius = 1.15f;            // 회전 고리(반지름 약 0.8유닛)보다 조금 넉넉하게
        private const float AirCenterHeight = 0.75f;      // 발 기준점에서 회전 중심까지(CharacterArtPostprocessor의 JumpAttack 기준점과 맞춤)

        private bool airAttackUsed;

        private Character character;
        private CharacterMovement2D movement;
        private AttackSpriteAnimator attackAnimator;

        private float cooldownRemaining;
        private int comboStep;            // 0 = 다음은 1타, 1 = 다음은 2·3타
        private float comboTimer;         // 1타가 끝난 뒤 남은 연결 시간
        private float stepElapsed;
        private float stepDuration;
        private bool bufferedNext;
        private Coroutine hitRoutine;

        public float CooldownRemaining => cooldownRemaining;
        public float Cooldown => cooldown;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
            attackAnimator = GetComponent<AttackSpriteAnimator>();
        }

        private bool ComboReady => UseCombo && attackAnimator.HasClip(DownClip) && attackAnimator.HasClip(XClip);

        private void Update()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            }

            if (ComboReady) UpdateComboTimers();

            if (movement.IsInputLocked || character.IsCombatLocked) return;

            if (movement.IsGrounded && airAttackUsed)
            {
                // 착지하면 공중 공격의 남은 쿨타임을 풀어서 바로 지상 공격으로 이어 갈 수 있게 한다.
                airAttackUsed = false;
                if (cooldownRemaining > 0f && stepDuration <= 0f) cooldownRemaining = 0f;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.zKey.wasPressedThisFrame)
            {
                if (!movement.IsGrounded) TryAirAttack();
                else if (ComboReady) OnComboPressed();
                else TryPerformBasicAttack();
            }

            // 1타 끝에 미리 눌러 둔 입력은 1타가 끝나는 순간 바로 2·3타로 잇는다.
            if (ComboReady && bufferedNext && cooldownRemaining <= 0f)
            {
                bufferedNext = false;
                StartComboStep();
            }
        }

        // ---- 공중 공격 ----
        private void TryAirAttack()
        {
            if (airAttackUsed || cooldownRemaining > 0f || !attackAnimator.HasClip(AirClip)) return;
            airAttackUsed = true;
            bufferedNext = false;
            Vector2 facing = movement.FacingDirection;
            if (hitRoutine != null) StopCoroutine(hitRoutine);

            movement.LiftAtLeast(AirLift);
            attackAnimator.PlayAerialClip(AirClip, facing, AirDuration, AirCanvasPixels, AirCanvasHeight);
            SfxPlayer.Play(SfxPlayer.NecrosiaZAttack);
            character.RestoreMana(manaRestoreOnAttack);
            cooldownRemaining = AirDuration;
            stepDuration = 0f;
            hitRoutine = StartCoroutine(AirHits());
        }

        private IEnumerator AirHits()
        {
            float elapsed = 0f;
            foreach (float t in AirHitTimes)
            {
                yield return new WaitForSeconds(t - elapsed);
                elapsed = t;
                if (movement.IsGrounded) break; // 착지하면 남은 타격도 끝(모션과 같이)
                if (t > AirHitTimes[0]) SfxPlayer.Play(SfxPlayer.NecrosiaZAttack);
                HitCircle((Vector2)transform.position + Vector2.up * AirCenterHeight, AirRadius, AirDamage);
            }
            hitRoutine = null;
        }

        private void HitCircle(Vector2 center, float radius, float multiplier)
        {
            foreach (var hit in Physics2D.OverlapCircleAll(center, radius))
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead) continue;
                character.DealDamage(monster, multiplier, isSkill: false);
            }
        }

        // ---- 예전 한 동작 공격 ----
        private void TryPerformBasicAttack()
        {
            if (cooldownRemaining > 0f) return;
            if (!movement.IsGrounded) return; // 공중에서는 공격과 스킬을 쓸 수 없다

            cooldownRemaining = cooldown;
            PerformBasicAttack();
        }

        private void PerformBasicAttack()
        {
            attackAnimator.Play(movement.FacingDirection);
            SfxPlayer.Play(SfxPlayer.NecrosiaZAttack);
            character.RestoreMana(manaRestoreOnAttack);
            HitBox(movement.FacingDirection, 1f);
        }

        // ---- 연속기 ----
        private void UpdateComboTimers()
        {
            if (stepDuration > 0f) stepElapsed += Time.deltaTime;
            if (cooldownRemaining <= 0f && comboStep == 1 && !bufferedNext)
            {
                comboTimer -= Time.deltaTime;
                if (comboTimer <= 0f) comboStep = 0; // 늦었다 — 다음은 다시 1타부터
            }
        }

        private void OnComboPressed()
        {
            if (cooldownRemaining > 0f)
            {
                // 1타 후반에 누르면 이어지게 받아 둔다(2·3타 도중의 입력은 버린다).
                if (comboStep == 1 && stepDuration > 0f && stepElapsed >= stepDuration * ComboBufferFrom) bufferedNext = true;
                return;
            }
            StartComboStep();
        }

        private void StartComboStep()
        {
            if (!movement.IsGrounded) return; // 공중에서는 공격과 스킬을 쓸 수 없다
            Vector2 facing = movement.FacingDirection;
            SfxPlayer.Play(SfxPlayer.NecrosiaZAttack);
            character.RestoreMana(manaRestoreOnAttack);
            if (hitRoutine != null) StopCoroutine(hitRoutine);

            if (comboStep == 0)
            {
                attackAnimator.PlayClip(DownClip, facing, DownDuration, DownStandPixels, StandTargetHeight, DownDuration * RecoveryCancelAt);
                stepDuration = DownDuration;
                cooldownRemaining = DownDuration * RecoveryCancelAt;
                hitRoutine = StartCoroutine(DownHits(facing));
                comboStep = 1;
                comboTimer = ComboWindow;
            }
            else
            {
                attackAnimator.PlayClip(XClip, facing, XDuration, XStandPixels, StandTargetHeight, XDuration * RecoveryCancelAt);
                stepDuration = XDuration;
                cooldownRemaining = XDuration * RecoveryCancelAt;
                hitRoutine = StartCoroutine(XHits(facing));
                comboStep = 0;
            }
            stepElapsed = 0f;
        }

        // 대시 등으로 공격을 끊을 때: 아직 안 들어간 타격과 미리 받아 둔 다음 입력을 버리고, 바로 다시 공격할 수 있게 한다.
        // 연속기 단계는 그대로 둬서 대시 직후 Z를 누르면 이어지던 타로 나간다(연결 시간 안이라면).
        public void CancelAttack()
        {
            if (hitRoutine != null) { StopCoroutine(hitRoutine); hitRoutine = null; }
            bufferedNext = false;
            cooldownRemaining = 0f;
            stepDuration = 0f;
        }

        private IEnumerator DownHits(Vector2 facing)
        {
            yield return new WaitForSeconds(DownHitTime);
            HitBox(facing, DownDamage);
            hitRoutine = null;
        }

        private IEnumerator XHits(Vector2 facing)
        {
            yield return new WaitForSeconds(XFirstHitTime);
            HitBox(facing, XDamage);
            yield return new WaitForSeconds(XSecondHitTime - XFirstHitTime);
            SfxPlayer.Play(SfxPlayer.NecrosiaZAttack);
            SpawnXSlash(facing);
            HitBox(facing, XDamage);
            hitRoutine = null;
        }

        // X자 잔상: 몸 앞에 크게 겹쳐 6프레임 재생한다. 캐릭터를 따라다니지 않고 벤 자리에 남는다.
        private void SpawnXSlash(Vector2 facing)
        {
            float side = facing.x < 0f ? -1f : 1f;
            Vector2 at = (Vector2)transform.position + new Vector2(side * XSlashOffset.x, XSlashOffset.y);
            var fx = FlipbookSpriteEffect.Create("XSlash", XSlashFx, at, XSlashFrameTime, loop: false, sortingOrder: 30);
            if (fx == null) return;
            var first = Resources.Load<Sprite>(XSlashFx + "/x_slash_1");
            float scale = first != null ? XSlashHeight / first.bounds.size.y : 1f;
            fx.transform.localScale = new Vector3(side * scale, scale, 1f);
        }

        private void HitBox(Vector2 facing, float multiplier)
        {
            var hits = Physics2D.OverlapBoxAll(AttackBoxCenter(facing), AttackBoxSize, 0f);
            foreach (var hit in hits)
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead) continue;

                character.DealDamage(monster, multiplier, isSkill: false);
            }
        }

        private void OnDrawGizmosSelected()
        {
            var facingSource = movement != null ? movement : GetComponent<CharacterMovement2D>();
            if (facingSource == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(AttackBoxCenter(facingSource.FacingDirection), AttackBoxSize);
        }

        private Vector2 AttackBoxSize => new Vector2(attackFar - attackNear, attackTop - attackBottom);

        private Vector2 AttackBoxCenter(Vector2 facing)
        {
            float side = facing.x < 0f ? -1f : 1f;
            return (Vector2)transform.position + new Vector2(side * (attackNear + attackFar) / 2f, (attackBottom + attackTop) / 2f);
        }
    }
}
