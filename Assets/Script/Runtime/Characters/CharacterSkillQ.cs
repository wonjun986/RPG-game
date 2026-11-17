using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Bootstrap;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // Q = 차원 참격. 기본 공격 모션으로 낫을 휘두르면 바로 앞 허공이 찢어지며 차원 균열(VoidRift)이 열리고,
    // 알리아스 종막(AliasSwordRitual)의 무차별 참격처럼 균열 앞쪽 구역에 보랏빛 참격(VoidSlash)이 제멋대로 각도로
    // 연달아 쏟아진 뒤, 마지막에 커다란 참격 하나가 구역을 가른다. 참격 하나하나가 닿은 적을 벤다.
    // 연타는 균열 자리에서 일어나므로, 낫을 휘두른 뒤에는 바로 움직일 수 있다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    [RequireComponent(typeof(AttackSpriteAnimator))]
    public class CharacterSkillQ : MonoBehaviour, ISkillCooldownReset
    {
        [SerializeField] private float manaCost = 40f;
        [SerializeField] private float cooldown = 10f;

        [Header("차원 균열")]
        [SerializeField] private string riftPath = "Art/Effects/VoidRift";
        // 균열 가운데 자리: 발 기준점에서 바라보는 쪽으로 riftForward, 위로 riftHeight(몸 가운데 높이).
        [SerializeField] private float riftForward = 1.1f;
        [SerializeField] private float riftHeight = 0.8f;
        [SerializeField] private float riftFrameDuration = 0.06f;
        // 낫을 휘두르기 시작해서 칼날이 앞을 지나는 순간에 허공이 찢어진다(기본 공격 0.35초 중).
        [SerializeField] private float riftDelay = 0.1f;

        [Header("보랏빛 참격")]
        [SerializeField] private string slashPath = "Art/Effects/VoidSlash";
        // 균열이 다 벌어질 즈음(6프레임 중 3번째) 참격이 쏟아지기 시작한다.
        [SerializeField] private float slashDelay = 0.15f;
        // 참격이 쏟아지는 구역: 균열 가운데에서 앞으로 zoneForward 떨어진 곳을 가운데로, 가로 ±zoneHalfSize.x, 세로 ±zoneHalfSize.y.
        [SerializeField] private float zoneForward = 1.6f;
        [SerializeField] private Vector2 zoneHalfSize = new Vector2(1.4f, 0.6f);
        [SerializeField] private int barrageSlashes = 8;
        [SerializeField] private float barrageInterval = 0.07f;
        [SerializeField] private Vector2 barrageScaleRange = new Vector2(0.8f, 1.2f);
        [SerializeField] private float barrageFrameDuration = 0.035f;
        [SerializeField] private float barrageDamageMultiplier = 0.3f;
        // 참격 그림(키 약 1.6유닛) 배율 1일 때 닿는 반지름.
        [SerializeField] private float slashHitRadius = 0.75f;

        [Header("마지막 일격")]
        [SerializeField] private float finisherDelay = 0.12f;
        [SerializeField] private float finisherScale = 1.9f;
        [SerializeField] private float finisherAngle = -35f;
        [SerializeField] private float finisherFrameDuration = 0.05f;
        [SerializeField] private float finisherDamageMultiplier = 1.0f;

        private Character character;
        private CharacterMovement2D movement;
        private AttackSpriteAnimator attackAnimator;
        private Sprite[] slashFrames;

        private float cooldownRemaining;

        public float CooldownRemaining => cooldownRemaining;
        public float Cooldown => cooldown;
        public float ManaCost => manaCost;

        public void ResetCooldown() => cooldownRemaining = 0f;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
            attackAnimator = GetComponent<AttackSpriteAnimator>();

            slashFrames = Resources.LoadAll<Sprite>(slashPath);
            System.Array.Sort(slashFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
        }

        private void Update()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            }

            if (movement.IsInputLocked) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            {
                TryUseSkill();
            }
        }

        private void TryUseSkill()
        {
            if (character.IsCombatLocked) return;
            if (!SkillBook.Allows(this, "Q")) return; // 아직 익히지 않은 스킬(SkillBook)
            if (!movement.IsGrounded) return; // 공중에서는 공격과 스킬을 쓸 수 없다
            if (cooldownRemaining > 0f) return;
            if (!character.TrySpendMana(manaCost)) return;

            cooldownRemaining = cooldown;
            StartCoroutine(SkillRoutine());
        }

        private IEnumerator SkillRoutine()
        {
            float facing = movement.FacingDirection.x < 0f ? -1f : 1f;
            attackAnimator.Play(new Vector2(facing, 0f));
            SfxPlayer.Play(SfxPlayer.NecrosiaZAttack);

            yield return new WaitForSeconds(riftDelay);

            Vector2 riftCenter = (Vector2)transform.position + new Vector2(facing * riftForward, riftHeight);
            var rift = FlipbookSpriteEffect.Create("VoidRift", riftPath, riftCenter, riftFrameDuration, loop: false, sortingOrder: 2);
            if (rift != null) rift.GetComponent<SpriteRenderer>().flipX = facing < 0f;

            yield return new WaitForSeconds(slashDelay);
            StartCoroutine(BarrageRoutine(riftCenter, facing));
        }

        // 균열 앞 구역에 참격을 무차별로 쏟아붓고, 마지막에 구역 가운데를 커다란 참격으로 가른다.
        private IEnumerator BarrageRoutine(Vector2 riftCenter, float facing)
        {
            if (slashFrames == null || slashFrames.Length == 0) yield break;

            Vector2 zoneCenter = riftCenter + Vector2.right * (facing * zoneForward);
            for (int i = 0; i < barrageSlashes; i++)
            {
                var at = zoneCenter + new Vector2(Random.Range(-zoneHalfSize.x, zoneHalfSize.x), Random.Range(-zoneHalfSize.y, zoneHalfSize.y));
                float scale = Random.Range(barrageScaleRange.x, barrageScaleRange.y);
                StartCoroutine(PlaySlash(at, Random.Range(0f, 360f), scale, Random.value < 0.5f, barrageFrameDuration, sortingOrder: 3));
                HitInCircle(at, slashHitRadius * scale, barrageDamageMultiplier);
                if (i % 3 == 0) ShakeCamera(0.1f, 0.08f);
                yield return new WaitForSeconds(barrageInterval * Random.Range(0.7f, 1.3f));
            }

            yield return new WaitForSeconds(finisherDelay);
            StartCoroutine(PlaySlash(zoneCenter, finisherAngle * facing, finisherScale, facing < 0f, finisherFrameDuration, sortingOrder: 4));
            HitInCircle(zoneCenter, slashHitRadius * finisherScale, finisherDamageMultiplier);
            ShakeCamera(0.25f, 0.2f);
        }

        private IEnumerator PlaySlash(Vector2 at, float angle, float scale, bool flip, float frameDuration, int sortingOrder)
        {
            var go = new GameObject("VoidSlash");
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, 0f, angle));
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.flipX = flip;
            foreach (var frame in slashFrames)
            {
                if (go == null) yield break;
                renderer.sprite = frame;
                yield return new WaitForSeconds(frameDuration);
            }
            if (go != null) Destroy(go);
        }

        private void HitInCircle(Vector2 center, float radius, float multiplier)
        {
            var hitThisSlash = new HashSet<Monster>();
            foreach (var hit in Physics2D.OverlapCircleAll(center, radius))
            {
                var monster = hit.GetComponent<Monster>();
                if (monster == null || monster.IsDead || !hitThisSlash.Add(monster)) continue;
                character.DealDamage(monster, multiplier, isSkill: true);
            }
        }

        private static void ShakeCamera(float duration, float magnitude)
        {
            var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (follow != null) follow.Shake(duration, magnitude);
        }
    }
}
