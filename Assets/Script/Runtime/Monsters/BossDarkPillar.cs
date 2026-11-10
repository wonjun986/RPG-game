using UnityEngine;
using Aethoria.Characters;
using Aethoria.Combat;

namespace Aethoria.Monsters
{
    // 보스 기사 알리아스가 점프해 검을 땅에 내리꽂으면 양옆 바닥에서 솟구치는 어둠의 마력 기둥.
    // 시트(Art/Effects/AliasDarkPillar, 6장, 피벗 아래 가운데)는 1~3번째 장이 솟아오르고 4~6번째 장이 다 솟은 기둥이 일렁인다.
    // 솟아오른 뒤 4~6번째 장을 한 번 더 돌리고 옅어지며 사라진다. 2번째 장부터 옅어지기 전까지 기둥 몸통에 닿으면 한 번 피해를 준다.
    public class BossDarkPillar : MonoBehaviour
    {
        public const string FramesPath = "Art/Effects/AliasDarkPillar";
        private static readonly int[] Sequence = { 0, 1, 2, 3, 4, 5, 3, 4, 5 };
        private const float FrameDuration = 0.07f;
        private const int HitFirstStep = 1;
        private const float FadeTime = 0.2f;
        private const float BodyHeight = 1.5f;      // 네크로시아 그림 키(충돌 판정은 발밑뿐이라 몸통을 직접 잰다)
        private const float CoreWidthRatio = 0.45f; // 그림 폭 중 가운데 기둥 줄기만 맞는 범위로 친다(바깥 연기는 뺀다)
        private const float CoreHeightRatio = 0.9f;

        private Sprite[] frames;
        private SpriteRenderer spriteRenderer;
        private Character target;
        private float damageAttack;
        private Vector2 hitSize;
        private float timer;
        private bool hit;

        private static Sprite[] cachedFrames;

        public static void Erupt(Vector2 bottomCenter, float attackPower, float scale)
        {
            if (cachedFrames == null || cachedFrames.Length == 0)
            {
                cachedFrames = Resources.LoadAll<Sprite>(FramesPath);
                if (cachedFrames != null) System.Array.Sort(cachedFrames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            if (cachedFrames == null || cachedFrames.Length < 6) return;

            var go = new GameObject("BossDarkPillar");
            go.transform.position = bottomCenter;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var pillar = go.AddComponent<BossDarkPillar>();
            pillar.frames = cachedFrames;
            pillar.spriteRenderer = go.AddComponent<SpriteRenderer>();
            pillar.spriteRenderer.sprite = cachedFrames[0];
            pillar.spriteRenderer.sortingOrder = 4;
            pillar.damageAttack = attackPower;
            // 판정 크기는 다 솟은 기둥(4번째 장) 기준.
            Vector2 size = cachedFrames[3].bounds.size * scale;
            pillar.hitSize = new Vector2(size.x * CoreWidthRatio, size.y * CoreHeightRatio);
            pillar.target = Object.FindAnyObjectByType<Character>();
        }

        private void Update()
        {
            timer += Time.deltaTime;
            int step = Mathf.FloorToInt(timer / FrameDuration);
            float playTime = Sequence.Length * FrameDuration;
            if (timer >= playTime + FadeTime)
            {
                Destroy(gameObject);
                return;
            }
            spriteRenderer.sprite = frames[Sequence[Mathf.Min(step, Sequence.Length - 1)]];
            bool fading = timer >= playTime;
            if (fading) spriteRenderer.color = new Color(1f, 1f, 1f, 1f - (timer - playTime) / FadeTime);

            if (hit || fading || step < HitFirstStep || target == null || target.IsDead) return;
            Vector2 feet = target.transform.position;
            Vector2 bottom = transform.position;
            // 솟아오르는 동안은 지금 장의 키만큼만 맞는다.
            float height = Mathf.Min(hitSize.y, spriteRenderer.sprite.bounds.size.y * transform.localScale.y * CoreHeightRatio);
            bool overlapX = Mathf.Abs(feet.x - bottom.x) <= hitSize.x * 0.5f + 0.3f;
            bool overlapY = feet.y <= bottom.y + height && feet.y + BodyHeight >= bottom.y;
            if (!overlapX || !overlapY) return;

            hit = true;
            target.TakeDamage(CombatMath.PhysicalDamage(damageAttack, target.Stats.defense), bottom);
        }
    }
}
