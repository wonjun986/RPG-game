using System.Collections;
using UnityEngine;

namespace Aethoria.Monsters
{
    // 연습장 허수아비. Monster로 만들어 두어 모든 스킬의 타격 판정/데미지 숫자가 그대로 적용되지만,
    // 맞을 때마다 체력을 가득 되돌려서 절대 쓰러지지 않는다(처치 경험치/퀘스트 카운트도 없음).
    // - 맞으면 흔들리는 프레임(2~4)을 한 번 재생하고 기본 자세(1)로 돌아온다.
    // - 머리 위에 연속으로 넣은 피해 합계를 보여주고, 잠시 안 맞으면 초기화한다.
    // - 사슬로 끌려가는 등 자리에서 밀려나면 잠시 뒤 제자리로 천천히 돌아온다.
    [RequireComponent(typeof(Monster))]
    public class TrainingDummy : MonoBehaviour
    {
        [SerializeField] private string framesPath = "Art/Props/Dummy";
        [SerializeField] private float wobbleFrameDuration = 0.06f;
        [SerializeField] private float comboResetDelay = 2f;
        [SerializeField] private float returnDelay = 0.8f;
        [SerializeField] private float returnSpeed = 4f;
        [SerializeField] private float labelHeight = 2.7f;

        private Monster monster;
        private SpriteRenderer spriteRenderer;
        private Sprite[] frames;
        private Coroutine wobbleRoutine;
        private TextMesh totalLabel;

        private Vector3 homePosition;
        private float totalDamage;
        private float lastHitTime = -999f;

        private void Awake()
        {
            monster = GetComponent<Monster>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            frames = Resources.LoadAll<Sprite>(framesPath);
            if (frames != null && frames.Length > 0)
            {
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
                spriteRenderer.sprite = frames[0];
            }

            CreateLabel();
        }

        private void Start()
        {
            homePosition = transform.position;
            monster.OnDamaged += HandleDamaged;
        }

        private void OnDestroy()
        {
            if (monster != null) monster.OnDamaged -= HandleDamaged;
        }

        private void CreateLabel()
        {
            var go = new GameObject("DamageTotal");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, labelHeight, 0f);

            totalLabel = go.AddComponent<TextMesh>();
            totalLabel.characterSize = 0.07f;
            totalLabel.fontSize = 42;
            totalLabel.fontStyle = FontStyle.Bold;
            totalLabel.alignment = TextAlignment.Center;
            totalLabel.anchor = TextAnchor.MiddleCenter;
            totalLabel.color = new Color(1f, 0.85f, 0.3f);
            go.GetComponent<MeshRenderer>().sortingOrder = 50;
            go.SetActive(false);
        }

        private void HandleDamaged(Monster _, float damage)
        {
            monster.RestoreFullHp();

            if (Time.time - lastHitTime > comboResetDelay) totalDamage = 0f;
            totalDamage += damage;
            lastHitTime = Time.time;
            totalLabel.text = $"누적 피해 {Mathf.RoundToInt(totalDamage):N0}";
            totalLabel.gameObject.SetActive(true);

            if (frames != null && frames.Length > 1)
            {
                if (wobbleRoutine != null) StopCoroutine(wobbleRoutine);
                wobbleRoutine = StartCoroutine(Wobble());
            }
        }

        private IEnumerator Wobble()
        {
            for (int i = 1; i < frames.Length; i++)
            {
                spriteRenderer.sprite = frames[i];
                yield return new WaitForSeconds(wobbleFrameDuration);
            }
            spriteRenderer.sprite = frames[0];
            wobbleRoutine = null;
        }

        private void Update()
        {
            if (totalLabel.gameObject.activeSelf && Time.time - lastHitTime > comboResetDelay)
            {
                totalLabel.gameObject.SetActive(false);
                totalDamage = 0f;
            }

            if (Time.time - lastHitTime > returnDelay && transform.position != homePosition)
            {
                transform.position = Vector3.MoveTowards(transform.position, homePosition, returnSpeed * Time.deltaTime);
            }
        }
    }
}
