using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Bootstrap;
using Aethoria.Monsters;

namespace Aethoria.Characters
{
    // R = 죽음의 소용돌이 (궁극기). 시전하면 캐릭터가 잠시 맵에서 사라져 무적 상태로 숨고,
    // 그 사이 커다란 칼날폭풍 무리가 등 뒤 맵 끝에서 반대편 맵 끝까지 휩쓸고 지나간다. 카메라도 폭풍을 따라가
    // 맵 전체를 쓸고 가는 모습을 보여 준 뒤 캐릭터에게 돌아온다.
    // 피해는 범위 제한 없이 현재 맵에 있는 모든 몬스터에게 매 틱마다 들어간다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    public class CharacterSkillR : MonoBehaviour, ISkillCooldownReset
    {
        [SerializeField] private float manaCost = 100f;
        [SerializeField] private float cooldown = 25f;
        [SerializeField] private float duration = 3f;
        [SerializeField] private float tickInterval = 0.15f;
        [SerializeField] private float damageMultiplierPerTick = 0.4f;
        [SerializeField] private float bossDamageMultiplier = 3.5f;

        [Header("연출: 맵을 휩쓰는 칼날폭풍")]
        [SerializeField] private string bladeStormEffectPath = "Art/Effects/DeathVortex";
        [SerializeField] private float bladeStormFrameDuration = 0.05f;
        [SerializeField] private int bladeStormCount = 3;
        [SerializeField] private float bladeStormScale = 4f;
        [SerializeField] private float bladeStormSpacing = 3f;
        [SerializeField] private float bladeStormHeight = 1.5f;
        [SerializeField] private float sweepEdgeInset = 2f;           // 폭풍 무리가 출발/도착하는 맵 끝 안쪽 거리
        [SerializeField] private float cameraSweepSmoothTime = 0.12f; // 카메라가 폭풍을 따라가는 부드러움
        [SerializeField] private float cameraReturnSmoothTime = 0.35f;

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

        public void ResetCooldown() => cooldownRemaining = 0f;
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

            var cameraFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cameraFollow != null) cameraFollow.Shake(shakeDuration, shakeMagnitude);

            // 등 뒤 맵 끝에서 출발해 바라보는 쪽 맵 끝까지 duration 동안 쓸고 간다. 카메라는 폭풍 무리 한가운데를 따라간다.
            Rect bounds = GameBootstrap.CurrentMapBounds;
            float facing = movement.FacingDirection.x < 0f ? -1f : 1f;
            float startX = facing > 0f ? bounds.xMin + sweepEdgeInset : bounds.xMax - sweepEdgeInset;
            float endX = facing > 0f ? bounds.xMax - sweepEdgeInset : bounds.xMin + sweepEdgeInset;
            float y = bounds.yMin + bladeStormHeight;
            var front = new GameObject("BladeStormFront").transform;
            front.position = new Vector3(startX, y, 0f);
            var bladeStorms = SpawnBladeStorm(front);
            if (cameraFollow != null) cameraFollow.SetTarget(front, cameraSweepSmoothTime);

            float elapsed = 0f;
            float nextTick = 0f;
            while (elapsed < duration)
            {
                front.position = new Vector3(Mathf.Lerp(startX, endX, Mathf.SmoothStep(0f, 1f, elapsed / duration)), y, 0f);
                if (elapsed >= nextTick)
                {
                    DealDamage();
                    nextTick += tickInterval;
                }
                yield return null;
                elapsed += Time.deltaTime;
            }

            foreach (var blade in bladeStorms)
            {
                if (blade != null) Destroy(blade.gameObject);
            }
            Destroy(front.gameObject);
            if (cameraFollow != null)
            {
                cameraFollow.SetTarget(transform, cameraReturnSmoothTime);
                StartCoroutine(ResetCameraSmoothing(cameraFollow));
            }

            if (visualRenderer != null) visualRenderer.enabled = true;
            character.SetInvincible(false);
            movement.SetInputLocked(false);
            isActive = false;
        }

        // 폭풍 무리 한가운데(front)에 나란히 칼날폭풍 이펙트를 붙인다. front가 움직이면 함께 쓸려 간다.
        // 맵의 세로 중앙은 하늘 쪽이라, 바닥(yMin) 기준으로 약간 띄운 높이(front의 높이)를 쓴다.
        private List<FlipbookSpriteEffect> SpawnBladeStorm(Transform front)
        {
            var effects = new List<FlipbookSpriteEffect>(bladeStormCount);
            float totalWidth = bladeStormSpacing * (bladeStormCount - 1);

            for (int i = 0; i < bladeStormCount; i++)
            {
                Vector2 position = (Vector2)front.position + new Vector2(-totalWidth / 2f + bladeStormSpacing * i, 0f);
                var blade = FlipbookSpriteEffect.Create("BladeStorm", bladeStormEffectPath, position, bladeStormFrameDuration, loop: true);
                blade.transform.SetParent(front, true);
                blade.transform.localScale = Vector3.one * bladeStormScale;
                effects.Add(blade);
            }

            return effects;
        }

        // 카메라가 캐릭터에게 다 돌아온 뒤 원래 따라가는 속도로 되돌린다.
        private IEnumerator ResetCameraSmoothing(CameraFollow cameraFollow)
        {
            yield return new WaitForSeconds(cameraReturnSmoothTime * 2f);
            if (cameraFollow != null) cameraFollow.ResetSmoothTime();
        }

        // 맵 전역 궁극기이므로 캐릭터 주변이 아니라 현재 맵에 있는 모든 몬스터를 매 틱마다 타격한다.
        // 보스(BossAI/BossSlimeAI 등 보스 전용 AI가 붙어있는 몬스터)에게는 추가 배율을 곱해 더 큰 피해를 준다.
        // (기본값 기준 풀캐스트 한 번에 보스 체력의 절반 정도가 빠지도록 맞춘 수치)
        private void DealDamage()
        {
            var monsters = Object.FindObjectsByType<Monster>(FindObjectsSortMode.None);
            foreach (var monster in monsters)
            {
                if (monster.IsDead) continue;

                float multiplier = damageMultiplierPerTick;
                if (IsBossMonster(monster)) multiplier *= bossDamageMultiplier;

                character.DealDamage(monster, multiplier, isSkill: true);
            }
        }

        // 새 보스 몬스터를 추가하면 여기에도 그 보스 전용 AI 타입을 추가해야 궁극기 배율이 적용된다.
        private static bool IsBossMonster(Monster monster)
        {
            return monster.GetComponent<BossAI>() != null || monster.GetComponent<BossSlimeAI>() != null || monster.GetComponent<SandDragonAI>() != null
                || monster.GetComponent<FinalBossAI>() != null;
        }
    }
}
