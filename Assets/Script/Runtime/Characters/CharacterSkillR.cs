using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Bootstrap;
using Aethoria.Combat;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // R = 죽음의 소용돌이 (궁극기). 시전하면 캐릭터가 잠시 맵에서 사라져 무적 상태로 숨고,
    // 그 사이 맵 전체를 휘감을 만큼 커다란 칼날폭풍 이펙트 몇 개가 맵 곳곳에 떠오르며,
    // 피해는 범위 제한 없이 현재 맵에 있는 모든 몬스터에게 매 틱마다 들어간다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    public class CharacterSkillR : MonoBehaviour
    {
        [SerializeField] private float manaCost = 100f;
        [SerializeField] private float cooldown = 25f;
        [SerializeField] private float duration = 3f;
        [SerializeField] private float tickInterval = 0.15f;
        [SerializeField] private float damageMultiplierPerTick = 0.4f;
        [SerializeField] private float bossDamageMultiplier = 3.5f;

        [Header("연출: 맵 중앙 칼날폭풍")]
        [SerializeField] private string bladeStormEffectPath = "Art/Effects/DeathVortex";
        [SerializeField] private float bladeStormFrameDuration = 0.05f;
        [SerializeField] private int bladeStormCount = 3;
        [SerializeField] private float bladeStormScale = 4f;
        [SerializeField] private float bladeStormSpacing = 3f;
        [SerializeField] private float bladeStormHeight = 1.5f;

        [Header("연출: 화면 흔들림")]
        [SerializeField] private float shakeDuration = 0.25f;
        [SerializeField] private float shakeMagnitude = 0.2f;

        private Character character;
        private CharacterMovement2D movement;
        private SpriteRenderer visualRenderer;

        private float cooldownRemaining;
        private bool isActive;

        public float CooldownRemaining => cooldownRemaining;
        public float Cooldown => cooldown;
        public bool IsActive => isActive;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();

            var visual = transform.Find("Visual");
            visualRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();
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
            if (isActive || cooldownRemaining > 0f) return;
            if (!character.TrySpendMana(manaCost)) return;

            cooldownRemaining = cooldown;
            StartCoroutine(WhirlwindRoutine());
        }

        private IEnumerator WhirlwindRoutine()
        {
            isActive = true;
            movement.SetInputLocked(true);
            character.SetInvincible(true);
            if (visualRenderer != null) visualRenderer.enabled = false;

            StartCoroutine(ShakeCameraRoutine());
            var bladeStorms = SpawnBladeStorm();

            float elapsed = 0f;
            while (elapsed < duration)
            {
                DealDamage();

                yield return new WaitForSeconds(tickInterval);
                elapsed += tickInterval;
            }

            foreach (var blade in bladeStorms)
            {
                if (blade != null) Destroy(blade.gameObject);
            }

            if (visualRenderer != null) visualRenderer.enabled = true;
            character.SetInvincible(false);
            movement.SetInputLocked(false);
            isActive = false;
        }

        // 캐릭터가 숨어있는 동안, 맵 중앙을 기준으로 나란히 칼날폭풍 이펙트를 띄운다.
        // 카메라가 주인공을 따라가며 화면을 제한하므로, 맵 전체에 무작위로 뿌리면 화면 밖으로
        // 잘려서 잘 안 보인다 — 대신 맵 중앙 한 군데에 몰아서 확실히 보이게 한다.
        // 맵의 세로 중앙(bounds.center.y)은 타일맵 전체의 중간일 뿐, 실제로는 하늘 쪽이라
        // 바닥(yMin) 기준으로 약간 띄운 높이를 써야 캐릭터/몬스터가 있는 화면 안에 보인다.
        private List<FlipbookSpriteEffect> SpawnBladeStorm()
        {
            var effects = new List<FlipbookSpriteEffect>(bladeStormCount);
            Rect bounds = GameBootstrap.CurrentMapBounds;
            float centerX = bounds.center.x;
            float y = bounds.yMin + bladeStormHeight;

            float totalWidth = bladeStormSpacing * (bladeStormCount - 1);
            float startX = centerX - totalWidth / 2f;

            for (int i = 0; i < bladeStormCount; i++)
            {
                Vector2 position = new Vector2(startX + bladeStormSpacing * i, y);
                var blade = FlipbookSpriteEffect.Create("BladeStorm", bladeStormEffectPath, position, bladeStormFrameDuration, loop: true);
                blade.transform.localScale = Vector3.one * bladeStormScale;
                effects.Add(blade);
            }

            return effects;
        }

        // 카메라를 짧게 흔들어 궁극기 발동 임팩트를 준다. 끝나면 원래 위치로 정확히 되돌린다.
        private IEnumerator ShakeCameraRoutine()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null) yield break;

            Vector3 basePosition = mainCamera.transform.position;
            float elapsed = 0f;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float damper = 1f - Mathf.Clamp01(elapsed / shakeDuration);
                float offsetX = Random.Range(-1f, 1f) * shakeMagnitude * damper;
                float offsetY = Random.Range(-1f, 1f) * shakeMagnitude * damper;
                mainCamera.transform.position = basePosition + new Vector3(offsetX, offsetY, 0f);
                yield return null;
            }

            mainCamera.transform.position = basePosition;
        }

        // 맵 전역 궁극기이므로 캐릭터 주변이 아니라 현재 맵에 있는 모든 몬스터를 매 틱마다 타격한다.
        // 보스(BossAI가 붙어있는 몬스터)에게는 추가 배율을 곱해 더 큰 피해를 준다.
        // (기본값 기준 풀캐스트 한 번에 보스 체력의 절반 정도가 빠지도록 맞춘 수치)
        private void DealDamage()
        {
            var monsters = Object.FindObjectsByType<Monster>(FindObjectsSortMode.None);
            foreach (var monster in monsters)
            {
                if (monster.IsDead) continue;

                float multiplier = damageMultiplierPerTick;
                if (monster.GetComponent<BossAI>() != null) multiplier *= bossDamageMultiplier;

                float damage = CombatMath.PhysicalDamage(character.Stats.attack * multiplier, monster.Defense);
                monster.TakeDamage(damage);
            }
        }
    }
}
