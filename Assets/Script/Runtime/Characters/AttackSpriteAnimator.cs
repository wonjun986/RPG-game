using System.Collections;
using UnityEngine;

namespace Aethoria.Characters
{
    // Resources/Art/Necrosia/Attack 아래의 낫 공격 프레임을 불러와, 요청 시 한 번만 재생한다.
    // 재생 중에는 WalkSpriteAnimator(걷기/대기 스프라이트)를 잠시 꺼서 서로 스프라이트를
    // 덮어쓰지 않게 하고, 끝나면 다시 켜서 제어권을 돌려준다.
    public class AttackSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private string resourcesPath = "Art/Necrosia/Attack";
        // 이 시간 동안 제자리에 묶이므로(후딜) 짧게 둔다.
        [SerializeField] private float totalDuration = 0.35f;
        // 공격 시트(보랏빛 낫의 연속 베기 자세, 8프레임)는 다리를 넓게 벌린 자세라 서 있는 키를 잴 수 없어서,
        // 캔버스 키(364px)를 통째로 맞춘다. 8장 모두 뒷발 부츠와 부츠 바닥을 같은 자리에 맞춰 두었고,
        // 기준점은 1번째 프레임 두 발 한가운데(CharacterArtPostprocessor의 Attack 기준점)다.
        [SerializeField] private float bodyPixelHeight = 364f;
        // 대기 모션과 머리·몸통 크기가 같아 보이는 캔버스 키.
        [SerializeField] private float bodyTargetHeight = 1.5f;

        private SpriteRenderer spriteRenderer;
        private WalkSpriteAnimator walkAnimator;
        private JumpSpriteAnimator jumpAnimator;
        private Sprite[] frames;
        private Coroutine playRoutine;

        public bool HasFrames => frames != null && frames.Length > 0;

        private CharacterMovement2D movement;

        private void Awake()
        {
            movement = GetComponent<CharacterMovement2D>();
            walkAnimator = GetComponent<WalkSpriteAnimator>();
            jumpAnimator = GetComponent<JumpSpriteAnimator>();

            var visual = transform.Find("Visual");
            spriteRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();

            frames = Resources.LoadAll<Sprite>(resourcesPath);
            if (frames != null && frames.Length > 0)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
        }

        public void Play(Vector2 facingDirection)
        {
            if (!HasFrames) return;
            // 공격 모션이 걸음째 미끄러져 보이지 않게 재생하는 동안 제자리에 세운다.
            GetComponent<CharacterMovement2D>()?.RootFor(totalDuration);
            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayRoutine(facingDirection));
        }

        // ---- 이름 붙은 공격 클립(기본 공격 연속기용) ----
        // path 폴더의 프레임을 duration 동안 한 번 재생한다. bodyPixelHeight는 그 시트에서 선 자세의 정수리~부츠 밑(px),
        // bodyTargetHeight는 그 키가 화면에서 보일 높이(유닛)라 시트마다 그림 크기가 달라도 같은 키로 보인다.
        private readonly System.Collections.Generic.Dictionary<string, Sprite[]> clipCache = new();

        public bool HasClip(string path) => LoadClip(path).Length > 0;

        private Sprite[] LoadClip(string path)
        {
            if (clipCache.TryGetValue(path, out var cached)) return cached;
            var loaded = Resources.LoadAll<Sprite>(path) ?? new Sprite[0];
            System.Array.Sort(loaded, (a, b) => string.CompareOrdinal(a.name, b.name));
            clipCache[path] = loaded;
            return loaded;
        }

        // rootSeconds: 제자리에 묶어 둘 시간. duration보다 짧으면 남은 끝자락은 좌우 이동으로 끊을 수 있다(걸음 그림으로 바로 넘어간다).
        public void PlayClip(string path, Vector2 facingDirection, float duration, float clipBodyPixelHeight, float clipBodyTargetHeight,
            float rootSeconds = -1f)
        {
            var clip = LoadClip(path);
            if (clip.Length == 0) return;
            GetComponent<CharacterMovement2D>()?.RootFor(rootSeconds >= 0f ? rootSeconds : duration);
            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayFrames(clip, facingDirection, duration, clipBodyPixelHeight, clipBodyTargetHeight));
        }

        // 공중 공격용: 제자리에 묶지 않고(공중 좌우 이동 유지), 이동 입력으로 끊지 않으며, 땅에 닿으면 그 자리에서 끝낸다.
        public void PlayAerialClip(string path, Vector2 facingDirection, float duration, float clipBodyPixelHeight, float clipBodyTargetHeight)
        {
            var clip = LoadClip(path);
            if (clip.Length == 0) return;
            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayFrames(clip, facingDirection, duration, clipBodyPixelHeight, clipBodyTargetHeight, aerial: true));
        }

        // 공격 잔상은 AttackAfterimage가 스프라이트가 바뀔 때마다 알아서 남긴다(모든 공격·스킬 공통).

        private IEnumerator PlayRoutine(Vector2 facingDirection)
        {
            return PlayFrames(frames, facingDirection, totalDuration, bodyPixelHeight, bodyTargetHeight);
        }

        private IEnumerator PlayFrames(Sprite[] frames, Vector2 facingDirection, float totalDuration, float bodyPixelHeight, float bodyTargetHeight,
            bool aerial = false)
        {
            if (walkAnimator != null) walkAnimator.enabled = false;
            if (jumpAnimator != null) jumpAnimator.enabled = false; // 공중에서도 점프 스프라이트가 덮어쓰지 않게
            spriteRenderer.flipX = facingDirection.x < 0f;

            float frameDuration = totalDuration / frames.Length;
            for (int i = 0; i < frames.Length; i++)
            {
                spriteRenderer.sprite = frames[i];
                // 이동 중 걷기 스프라이트가 남겨둔 스케일을 그대로 물려받으면 이동 중에 공격할 때 스프라이트가
                // 커 보였다. 프레임마다 직접 스케일을 정하되, 시트 전체에 같은 배율을 건다(프레임 높이로 맞추면
                // 짧게 잘린 프레임에서 캐릭터가 커진다).
                SkillFrameNormalizer.ApplyBody(spriteRenderer.transform, frames[i], bodyPixelHeight, bodyTargetHeight);
                bool walkedOut = false;
                for (float t = 0f; t < frameDuration; t += Time.deltaTime)
                {
                    yield return null;
                    if (aerial)
                    {
                        // 공중 공격은 착지하는 순간 끝낸다(남은 회전 프레임을 땅 위에서 재생하지 않는다).
                        if (movement != null && movement.IsGrounded) { walkedOut = true; break; }
                        continue;
                    }
                    // 제자리 묶임이 풀린 끝자락에 좌우로 움직이면 남은 프레임을 건너뛰고 걸음으로 넘어간다(후딜 캔슬).
                    if (movement != null && !movement.IsRooted && movement.HasMoveInput) { walkedOut = true; break; }
                }
                // 그새 다른 동작(스킬/피격 등)이 스프라이트를 가져갔으면 조용히 물러난다. 걷기/점프
                // 애니메이터 복구와 스케일 초기화는 마지막에 가져간 쪽이 끝날 때 한다(여기서 하면
                // 그쪽 재생 도중에 걷기 스프라이트가 덮어써서 이상하게 캔슬된 것처럼 보였다).
                if (spriteRenderer.sprite != frames[i])
                {
                    playRoutine = null;
                    yield break;
                }
                if (walkedOut) break;
            }

            SkillFrameNormalizer.Reset(spriteRenderer.transform);
            if (walkAnimator != null) walkAnimator.enabled = true;
            if (jumpAnimator != null) jumpAnimator.enabled = true;
            playRoutine = null;
        }
    }
}
