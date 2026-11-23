using System.Collections;
using UnityEngine;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.Monsters;

namespace Aethoria.Fx
{
    // 몬스터를 때렸을 때의 타격감(Monster.TakeDamage가 부른다). 피격 모션 시트 없이 코드만으로:
    //  - 히트스톱: 아주 짧게 게임 전체를 거의 멈춘다(치명타/스킬/처치일수록 길게).
    //  - 흔들기: 맞은 몬스터의 그림을 좌우로 잘게 떨게 한다. 충돌체·AI 위치는 건드리지 않도록
    //    LateUpdate에서 더했다가 그 프레임이 화면에 그려진 뒤 바로 되돌린다.
    //  - 번쩍임: 몸 위에 같은 그림의 흰 실루엣을 잠깐 겹친다(몸 색을 바꾸지 않아서 보스의 붉은 번쩍임/슈퍼아머 색과 안 부딪힌다).
    //  - 불꽃: 맞은 자리에서 작은 불꽃이 튄다.
    // 보스는 슈퍼아머라 행동은 끊지 않고, 이 연출만으로 맞는 느낌을 준다.
    public static class HitFeedback
    {
        // 히트스톱 길이(실제 시간, 초). 게임 전체를 멈추는 연출이라 큰 한 방(치명타/처치)에만 쓴다.
        // 일반 타격·연타 스킬마다 멈추면 연타하는 내내 게임이 끊겨 보여서, 그쪽은 몸 흔들기/번쩍임/불꽃/소리만 낸다.
        private const float StopCritical = 0.04f;
        private const float StopKill = 0.06f;
        // 같은 종류의 정지가 다시 걸리기까지의 최소 간격(실제 시간). 광역기로 여러 마리를 한꺼번에 잡아도 한 번만 멈춘다.
        private const float CriticalGap = 0.5f;
        private const float KillGap = 0.3f;

        // 흔들기 세기(유닛)와 길이(실제 시간, 초)
        private const float ShakeNormal = 0.06f;
        private const float ShakeCritical = 0.1f;
        private const float ShakeDuration = 0.14f;

        private const float FlashDuration = 0.09f;

        private static readonly Color SparkA = Color.white;
        private static readonly Color SparkB = new Color(1f, 0.85f, 0.55f);
        private static readonly Color CritSparkB = new Color(1f, 0.55f, 0.15f);

        private static Character player;

        public static void OnMonsterHit(Monster monster, bool isCritical, bool isSkill, bool killed)
        {
            if (monster == null) return;

            // 타격음: Resources/Audio/Sfx/monster_hit 파일이 있으면 그걸, 없으면 코드로 합성한 소리를 낸다.
            if (!SfxPlayer.TryPlay(SfxPlayer.MonsterHit, 0.7f))
                ProceduralAudio.PlayHit(killed ? HitSound.Kill : isCritical ? HitSound.Critical : HitSound.Normal);

            if (monster.IsMinigameOpponent) return;

            var sr = monster.GetComponent<SpriteRenderer>();
            Vector3 center = sr != null ? sr.bounds.center : monster.transform.position;

            // 플레이어 반대쪽(맞아서 밀려나는 쪽). +1 = 오른쪽
            if (player == null) player = Object.FindAnyObjectByType<Character>();
            float dir = player != null ? Mathf.Sign(monster.transform.position.x - player.transform.position.x) : 1f;
            if (dir == 0f) dir = 1f;

            if (killed) HitStop.Request(StopKill, KillGap);
            else if (isCritical) HitStop.Request(StopCritical, CriticalGap);

            SpawnSparks(center, monster, isCritical || killed, dir);

            if (killed)
            {
                // 본체는 이 프레임 끝에 지워지므로 흔들기/번쩍임 대신 화면을 살짝 흔든다.
                var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
                if (follow != null) follow.Shake(0.12f, 0.07f);
                return;
            }

            if (sr == null) return;
            var fx = monster.GetComponent<MonsterHitFx>();
            if (fx == null) fx = monster.gameObject.AddComponent<MonsterHitFx>();
            fx.Play(sr, isCritical ? ShakeCritical : ShakeNormal, ShakeDuration, FlashDuration, dir);
        }

        // 불꽃은 맞은 쪽 반대 방향(밀려나는 쪽)으로 110도 부채꼴로 튄다.
        private static void SpawnSparks(Vector3 center, Monster monster, bool strong, float dir)
        {
            const float spread = 110f;
            float arcStart = (dir > 0f ? 0f : 180f) - spread * 0.5f;
            FxUtil.Burst(center, new FxUtil.BurstSpec
            {
                count = strong ? 12 : 6,
                lifeMin = 0.12f, lifeMax = 0.26f,
                speedMin = 3f, speedMax = strong ? 7f : 5f,
                sizeMin = 0.08f, sizeMax = strong ? 0.2f : 0.14f,
                sizeEnd = 0.2f, gravity = 0f, drag = 4f,
                radius = 0.15f,
                arcStart = arcStart, arc = spread,
                colorA = SparkA, colorB = strong ? CritSparkB : SparkB,
                texture = FxUtil.Sparkle,
                sortingOrder = FxUtil.OrderOf(monster, 3),
            });
        }
    }

    // 짧은 시간 정지. 다른 창(일시정지/강화창/리듬전투/레벨업 슬로우)이 timeScale을 바꿔 둔 상태면 건드리지 않고,
    // 우리가 멈춘 사이 누가 timeScale을 바꾸면 그쪽에 맡기고 되돌리지 않는다.
    public class HitStop : MonoBehaviour
    {
        private const float StopScale = 0.03f;
        // 멈춘 사이 더 맞아도 한 번의 정지가 이보다 길어지지 않는다.
        private const float MaxStop = 0.08f;

        private static HitStop instance;
        private bool active;
        private float endTime;
        private float lastStart = -10f;

        // minGap: 마지막 정지를 시작한 뒤 이만큼(실제 시간) 지나야 새로 멈춘다.
        public static void Request(float realDuration, float minGap)
        {
            if (realDuration <= 0f) return;
            if (instance == null)
            {
                var go = new GameObject("HitStop") { hideFlags = HideFlags.HideInHierarchy };
                DontDestroyOnLoad(go);
                instance = go.AddComponent<HitStop>();
            }
            instance.Begin(realDuration, minGap);
        }

        private void Begin(float duration, float minGap)
        {
            float now = Time.unscaledTime;
            if (active)
            {
                endTime = Mathf.Min(Mathf.Max(endTime, now + duration), lastStart + MaxStop);
                return;
            }
            if (!Mathf.Approximately(Time.timeScale, 1f)) return;
            if (now - lastStart < minGap) return;

            lastStart = now;
            endTime = now + duration;
            active = true;
            Time.timeScale = StopScale;
        }

        private void Update()
        {
            if (!active) return;
            if (!Mathf.Approximately(Time.timeScale, StopScale))
            {
                active = false; // 다른 쪽이 timeScale을 가져갔다
                return;
            }
            if (Time.unscaledTime >= endTime) Release();
        }

        private void Release()
        {
            if (active && Mathf.Approximately(Time.timeScale, StopScale)) Time.timeScale = 1f;
            active = false;
        }

        private void OnDisable() => Release();
    }

    // 몬스터 몸에 붙어 흔들기와 흰 번쩍임을 맡는다(처음 맞을 때 HitFeedback이 붙인다).
    public class MonsterHitFx : MonoBehaviour
    {
        private static Material silhouetteMaterial;

        private SpriteRenderer target;
        private SpriteRenderer overlay;
        private float magnitude;
        private float shakeDir = 1f;
        private float shakeStart, shakeEnd;
        private float flashEnd, flashDuration;
        private Vector3 applied;
        private Coroutine restoreLoop;

        // 그림의 알파만 쓰고 색은 꼭짓점 색으로 칠하는 셰이더(DangerFx와 같은 방식). 없으면 번쩍임만 생략한다.
        private static Material SilhouetteMaterial
        {
            get
            {
                if (silhouetteMaterial == null)
                {
                    var shader = Shader.Find("GUI/Text Shader");
                    if (shader != null) silhouetteMaterial = new Material(shader);
                }
                return silhouetteMaterial;
            }
        }

        public void Play(SpriteRenderer sr, float shakeMagnitude, float shakeDuration, float flash, float direction)
        {
            target = sr;
            shakeDir = direction;
            float now = Time.unscaledTime;
            // 흔들리는 중에 또 맞으면 더 센 쪽으로 처음부터 다시 흔든다.
            magnitude = now < shakeEnd ? Mathf.Max(magnitude, shakeMagnitude) : shakeMagnitude;
            shakeStart = now;
            shakeEnd = now + shakeDuration;
            flashDuration = flash;
            flashEnd = now + flash;

            if (overlay == null && SilhouetteMaterial != null)
            {
                var go = new GameObject("HitFlash");
                go.transform.SetParent(transform, false);
                overlay = go.AddComponent<SpriteRenderer>();
                overlay.sharedMaterial = SilhouetteMaterial;
                overlay.color = Color.clear;
            }
            if (restoreLoop == null) restoreLoop = StartCoroutine(RestoreAfterRender());
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;

            if (now < shakeEnd)
            {
                float k = 1f - (now - shakeStart) / Mathf.Max(0.01f, shakeEnd - shakeStart);
                // 처음엔 맞은 반대쪽으로 툭 밀렸다가(cos는 +1에서 시작) 줄어들며 떨린다.
                float x = shakeDir * Mathf.Cos((now - shakeStart) * 2f * Mathf.PI * 30f) * magnitude * k;
                float y = Mathf.Cos((now - shakeStart) * 2f * Mathf.PI * 23f) * magnitude * 0.3f * k;
                applied = new Vector3(x, y, 0f);
                transform.position += applied;
            }

            if (overlay != null)
            {
                if (now < flashEnd && target != null)
                {
                    overlay.sprite = target.sprite;
                    overlay.flipX = target.flipX;
                    overlay.flipY = target.flipY;
                    overlay.sortingLayerID = target.sortingLayerID;
                    overlay.sortingOrder = target.sortingOrder + 1;
                    float a = Mathf.Clamp01((flashEnd - now) / Mathf.Max(0.01f, flashDuration));
                    overlay.color = new Color(1f, 1f, 1f, 0.8f * a);
                }
                else if (overlay.color.a > 0f)
                {
                    overlay.color = Color.clear;
                }
            }
        }

        // 흔든 만큼을 화면에 그린 뒤 되돌려서, 다음 프레임의 물리/AI는 원래 위치를 본다.
        private IEnumerator RestoreAfterRender()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                if (applied != Vector3.zero)
                {
                    transform.position -= applied;
                    applied = Vector3.zero;
                }
                if (Time.unscaledTime >= shakeEnd && Time.unscaledTime >= flashEnd) break;
            }
            restoreLoop = null;
        }

        private void OnDisable()
        {
            if (applied != Vector3.zero)
            {
                transform.position -= applied;
                applied = Vector3.zero;
            }
            restoreLoop = null;
            if (overlay != null) overlay.color = Color.clear;
        }
    }
}
