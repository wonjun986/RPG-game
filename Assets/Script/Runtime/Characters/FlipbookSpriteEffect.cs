using System.Collections;
using UnityEngine;

namespace Aethoria.Characters
{
    // Resources 아래의 프레임들을 순서대로 재생하는 범용 1회성/반복 이펙트 스프라이트.
    // 날아가는 무기, 타격 시 터지는 이펙트 등 위치만 외부에서 옮겨주면 되는
    // 단순한 비주얼을 매번 새로 만들지 않고 재사용하기 위한 것.
    public class FlipbookSpriteEffect : MonoBehaviour
    {
        // loop가 false면 프레임을 한 번 다 재생한 뒤 스스로 파괴된다.
        // loop가 true면 계속 반복 재생하며, 호출한 쪽에서 직접 Destroy 해줘야 한다.
        public static FlipbookSpriteEffect Create(string name, string resourcesPath, Vector2 position, float frameDuration, bool loop, int sortingOrder = 2)
        {
            var go = new GameObject(name);
            go.transform.position = position;

            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = sortingOrder;

            var effect = go.AddComponent<FlipbookSpriteEffect>();
            effect.Setup(spriteRenderer, resourcesPath, frameDuration, loop);
            return effect;
        }

        private void Setup(SpriteRenderer spriteRenderer, string resourcesPath, float frameDuration, bool loop)
        {
            var frames = Resources.LoadAll<Sprite>(resourcesPath);
            if (frames != null && frames.Length > 0)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                StartCoroutine(PlayRoutine(spriteRenderer, frames, frameDuration, loop));
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private IEnumerator PlayRoutine(SpriteRenderer spriteRenderer, Sprite[] frames, float frameDuration, bool loop)
        {
            int i = 0;
            while (true)
            {
                spriteRenderer.sprite = frames[i];
                yield return new WaitForSeconds(frameDuration);

                i++;
                if (i >= frames.Length)
                {
                    if (!loop)
                    {
                        Destroy(gameObject);
                        yield break;
                    }
                    i = 0;
                }
            }
        }
    }
}
