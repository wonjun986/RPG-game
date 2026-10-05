using System.Collections;
using UnityEngine;

namespace Aethoria.Characters
{
    // W스킬 악마의 손 이펙트. 포탈이 열리며 손이 뻗어나오는 앞부분(intro)을 재생한 뒤,
    // Release()가 호출될 때까지 손이 뻗은 프레임 구간을 반복하다가, 마지막 프레임(손을 거둬들임)을
    // 재생하고 스스로 파괴된다. 일반 FlipbookSpriteEffect는 처음부터 끝까지 한 번 또는 전체 반복만
    // 되기 때문에, 지속 시간을 늘리려면 이렇게 중간 구간만 반복하는 재생이 필요하다.
    public class DemonHandEffect : MonoBehaviour
    {
        private const float MaxHoldDuration = 5f;

        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private float frameDuration;
        private int loopStart;
        private int loopEnd;
        private bool released;

        public static DemonHandEffect Create(string resourcesPath, Vector2 position, float facingSign,
            float frameDuration, int loopStart, int loopEnd, int sortingOrder)
        {
            var frames = Resources.LoadAll<Sprite>(resourcesPath);
            if (frames == null || frames.Length == 0) return null;
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));

            var go = new GameObject("DemonHand");
            go.transform.position = position;
            go.transform.localScale = new Vector3(facingSign, 1f, 1f);

            var effect = go.AddComponent<DemonHandEffect>();
            effect.spriteRenderer = go.AddComponent<SpriteRenderer>();
            effect.spriteRenderer.sortingOrder = sortingOrder;
            effect.frames = frames;
            effect.frameDuration = frameDuration;
            effect.loopStart = Mathf.Clamp(loopStart, 0, frames.Length - 1);
            effect.loopEnd = Mathf.Clamp(loopEnd, effect.loopStart, frames.Length - 1);
            effect.StartCoroutine(effect.PlayRoutine());
            return effect;
        }

        public void Release() => released = true;

        private IEnumerator PlayRoutine()
        {
            for (int i = 0; i < loopStart; i++)
            {
                spriteRenderer.sprite = frames[i];
                yield return new WaitForSeconds(frameDuration);
            }

            int loopIndex = loopStart;
            float heldTime = 0f;
            // 시전자가 도중에 죽거나 비활성화돼서 Release()가 끝내 호출되지 않아도 영원히 남지 않게 한다.
            while (!released && heldTime < MaxHoldDuration)
            {
                spriteRenderer.sprite = frames[loopIndex];
                yield return new WaitForSeconds(frameDuration);
                heldTime += frameDuration;
                loopIndex = loopIndex >= loopEnd ? loopStart : loopIndex + 1;
            }

            for (int i = loopEnd + 1; i < frames.Length; i++)
            {
                spriteRenderer.sprite = frames[i];
                yield return new WaitForSeconds(frameDuration);
            }

            Destroy(gameObject);
        }
    }
}
