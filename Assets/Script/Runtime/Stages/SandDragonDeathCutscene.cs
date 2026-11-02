using System.Collections;
using UnityEngine;
using Aethoria.Characters;
using Aethoria.Monsters;

namespace Aethoria.Stages
{
    // 황금 모래룡 사망 연출. 체력이 0이 되면 하던 패턴을 모두 끊고, 마지막으로 포효한 뒤(SandDragonAI.DeathRoar)
    // 모래 속에 쓰러지는 모션(Art/Monsters/SandDragon/Die 8프레임, 모래 드래곤의 최후의 붕괴)을 한 번 재생한다.
    // 쓰러진 모습으로 잠깐 멈췄다가 서서히 사라지고, 그다음 onFinished(출구 포탈)를 부른 뒤 모래룡을 지운다.
    // 보상(경험치/골드/드랍/퀘스트)은 쓰러진 순간 Monster.OnDied/AnyDied로 이미 처리된다.
    [RequireComponent(typeof(Monster))]
    public class SandDragonDeathCutscene : MonoBehaviour
    {
        public const string DiePath = "Art/Monsters/SandDragon/Die";
        private const float DelayAfterBattle = 0.3f;
        private const float DieFrameInterval = 0.15f;
        private const float HoldLastFrame = 1.2f;
        private const float FadeOutDuration = 0.8f;

        private Monster monster;
        private SpriteRenderer spriteRenderer;
        private Sprite[] dieFrames;
        private System.Action onFinished;
        private bool running;

        public void Initialize(System.Action finishedCallback)
        {
            onFinished = finishedCallback;
            monster = GetComponent<Monster>();
            monster.DestroyOnDeath = false;
            monster.OnDied += HandleDied;
        }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            dieFrames = Resources.LoadAll<Sprite>(DiePath);
            System.Array.Sort(dieFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
        }

        private void HandleDied(Monster _)
        {
            if (running) return;
            running = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            // 쓰러진 모래룡이 더 움직이거나 부딪히지 않게 멈춰 세운다.
            var dragonAI = GetComponent<SandDragonAI>();
            if (dragonAI != null) dragonAI.HaltForDeath();
            var attackAnimator = GetComponent<MonsterAttackSpriteAnimator>();
            if (attackAnimator != null) attackAnimator.Stop();
            var body = GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;
            foreach (var col in GetComponents<Collider2D>()) col.enabled = false;
            if (body != null) body.bodyType = RigidbodyType2D.Kinematic;
            spriteRenderer.color = Color.white;
            HideHealthBar();

            yield return new WaitForSeconds(DelayAfterBattle);

            // 마지막 포효: 전투 포효처럼 입을 크게 벌리며 화면이 흔들린 뒤 쓰러진다.
            if (dragonAI != null) yield return dragonAI.DeathRoar();
            if (attackAnimator != null) attackAnimator.Stop();

            // 쓰러지는 모션: 꿈틀거리던 대기 애니메이터를 끄고 사망 프레임을 한 번 재생한 뒤 마지막 프레임에 멈춘다.
            var walkAnimator = GetComponent<MonsterWalkSpriteAnimator>();
            if (walkAnimator != null) walkAnimator.enabled = false;
            var cameraFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            if (cameraFollow != null) cameraFollow.Shake(dieFrames.Length * DieFrameInterval, 0.12f);
            foreach (var frame in dieFrames)
            {
                spriteRenderer.sprite = frame;
                yield return new WaitForSeconds(DieFrameInterval);
            }
            yield return new WaitForSeconds(HoldLastFrame);

            for (float t = 0f; t < FadeOutDuration; t += Time.deltaTime)
            {
                spriteRenderer.color = new Color(1f, 1f, 1f, 1f - t / FadeOutDuration);
                yield return null;
            }

            onFinished?.Invoke();
            Destroy(gameObject);
        }

        private void HideHealthBar()
        {
            var bar = GetComponent<MonsterHealthBar>();
            if (bar != null) bar.enabled = false;
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("HealthBar")) child.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (monster != null) monster.OnDied -= HandleDied;
        }
    }
}
