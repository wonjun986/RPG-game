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

        private void Awake()
        {
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

        private IEnumerator PlayRoutine(Vector2 facingDirection)
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
                yield return new WaitForSeconds(frameDuration);
                // 그새 다른 동작(스킬/피격 등)이 스프라이트를 가져갔으면 조용히 물러난다. 걷기/점프
                // 애니메이터 복구와 스케일 초기화는 마지막에 가져간 쪽이 끝날 때 한다(여기서 하면
                // 그쪽 재생 도중에 걷기 스프라이트가 덮어써서 이상하게 캔슬된 것처럼 보였다).
                if (spriteRenderer.sprite != frames[i])
                {
                    playRoutine = null;
                    yield break;
                }
            }

            SkillFrameNormalizer.Reset(spriteRenderer.transform);
            if (walkAnimator != null) walkAnimator.enabled = true;
            if (jumpAnimator != null) jumpAnimator.enabled = true;
            playRoutine = null;
        }
    }
}
