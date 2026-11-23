using System.Collections.Generic;
using UnityEngine;

namespace Aethoria.Characters
{
    // 네크로시아의 모든 공격(기본 연속기·공중 공격·각 스킬·궁극기 시전)에 보랏빛 잔상을 남긴다.
    // 어느 스크립트가 그림을 바꾸든 Visual의 스프라이트만 지켜본다: 공격 시트에 속한 프레임이 다음 프레임으로
    // 넘어가는 순간, 직전 자세를 그 자리(그때의 위치·크기·좌우)에 남겨서 낫을 휘두른 궤적이 이어져 보이게 한다.
    // 공격하며 몸이 빠르게 움직일 때(돌진 베기 등)는 프레임이 그대로여도 일정 거리마다 하나씩 더 남긴다.
    // 다른 애니메이터가 코루틴/LateUpdate에서 크기를 정한 뒤에 읽도록 늦게 돈다.
    [DefaultExecutionOrder(1000)]
    public class AttackAfterimage : MonoBehaviour
    {
        // 공격 자세가 들어 있는 시트들. 걷기·대기·점프·피격·가드·수영·대시(대시는 자체 잔상)는 뺀다.
        private static readonly string[] AttackClipPaths =
        {
            "Art/Necrosia/Attack", "Art/Necrosia/AttackDown", "Art/Necrosia/AttackX", "Art/Necrosia/JumpAttack",
            "Art/Necrosia/ASkill", "Art/Necrosia/SSkill", "Art/Necrosia/DSkill", "Art/Necrosia/WSkill",
            "Art/Necrosia/ZSkill", "Art/Necrosia/ChainSkill", "Art/Necrosia/ESkill",
            "Art/Necrosia/Ult", "Art/Necrosia/UltReady",
        };

        private const float GhostLife = 0.24f;
        private const float GhostGrow = 0.04f;           // 사라지면서 살짝 부풀어 번지는 정도
        private const float MoveGhostDistance = 0.32f;   // 공격 중 이만큼 움직이면 프레임이 그대로여도 하나 더
        private const float MoveGhostInterval = 0.035f;
        private const int PoolSize = 20;
        // 처음엔 밝은 연보라, 사라질수록 짙은 보라로 가라앉는다(대시 잔상과 같은 계열).
        private static readonly Color StartColor = new Color(0.82f, 0.62f, 1f, 0.5f);
        private static readonly Color EndColor = new Color(0.42f, 0.18f, 0.85f, 0f);

        // 플레이 모드 진입 시 도메인 리로드가 꺼져 있어 static에 두면 이전 세션 것이 남으므로 인스턴스마다 모은다.
        private HashSet<Sprite> attackSprites;

        private SpriteRenderer source;
        private CharacterSkillDash dash;

        // 직전 LateUpdate에 보였던 모습
        private Sprite lastSprite;
        private Vector3 lastPosition, lastScale;
        private Quaternion lastRotation;
        private bool lastFlipX, lastFlipY, lastVisible;

        private Vector3 lastGhostPosition;
        private float lastGhostTime;

        private readonly List<Ghost> pool = new();
        private Transform poolRoot;

        private void Awake()
        {
            var visual = transform.Find("Visual");
            source = visual != null ? visual.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();
            dash = GetComponent<CharacterSkillDash>();

            if (attackSprites == null)
            {
                attackSprites = new HashSet<Sprite>();
                foreach (var path in AttackClipPaths)
                {
                    var sprites = Resources.LoadAll<Sprite>(path);
                    if (sprites == null) continue;
                    foreach (var s in sprites) attackSprites.Add(s);
                }
            }
        }

        private void OnDestroy()
        {
            if (poolRoot != null) Destroy(poolRoot.gameObject);
        }

        private void LateUpdate()
        {
            if (source == null) return;
            var t = source.transform;
            var sprite = source.sprite;
            bool visible = source.enabled && source.gameObject.activeInHierarchy && sprite != null;
            bool isAttack = visible && attackSprites.Contains(sprite);
            bool dashing = dash != null && dash.IsDashing;

            // 공격 프레임이 바뀌었으면 바뀌기 직전 자세를 남긴다(공격이 끝나 다른 그림으로 넘어갈 때의 마지막 자세 포함).
            if (lastVisible && lastSprite != null && sprite != lastSprite && attackSprites.Contains(lastSprite) && !dashing)
            {
                Spawn(lastSprite, lastPosition, lastRotation, lastScale, lastFlipX, lastFlipY);
            }
            // 공격하며 빠르게 움직이는 중이면 지나간 자리에도 남긴다.
            else if (isAttack && !dashing && Time.time - lastGhostTime >= MoveGhostInterval
                     && (t.position - lastGhostPosition).sqrMagnitude >= MoveGhostDistance * MoveGhostDistance)
            {
                Spawn(sprite, t.position, t.rotation, t.lossyScale, source.flipX, source.flipY);
            }

            lastSprite = sprite;
            lastVisible = visible;
            lastPosition = t.position;
            lastRotation = t.rotation;
            lastScale = t.lossyScale;
            lastFlipX = source.flipX;
            lastFlipY = source.flipY;
            if (!isAttack) lastGhostPosition = t.position; // 공격을 시작한 자리부터 거리를 잰다
        }

        private void Spawn(Sprite sprite, Vector3 position, Quaternion rotation, Vector3 scale, bool flipX, bool flipY)
        {
            var ghost = GetGhost();
            if (ghost == null) return;
            ghost.transform.SetPositionAndRotation(position, rotation);
            var sr = ghost.Renderer;
            sr.sprite = sprite;
            sr.flipX = flipX;
            sr.flipY = flipY;
            sr.sharedMaterial = source.sharedMaterial;
            sr.sortingLayerID = source.sortingLayerID;
            sr.sortingOrder = source.sortingOrder - 1;
            ghost.Begin(scale);
            lastGhostPosition = position;
            lastGhostTime = Time.time;
        }

        private Ghost GetGhost()
        {
            Ghost oldest = null;
            foreach (var g in pool)
            {
                if (g == null) continue;
                if (!g.gameObject.activeSelf) return g;
                if (oldest == null || g.Age > oldest.Age) oldest = g;
            }
            if (pool.Count < PoolSize)
            {
                if (poolRoot == null) poolRoot = new GameObject("NecrosiaAttackAfterimages").transform;
                var go = new GameObject("AttackGhost", typeof(SpriteRenderer));
                go.transform.SetParent(poolRoot, false);
                var ghost = go.AddComponent<Ghost>();
                pool.Add(ghost);
                return ghost;
            }
            return oldest; // 가득 찼으면 가장 오래된 잔상을 다시 쓴다
        }

        private class Ghost : MonoBehaviour
        {
            public SpriteRenderer Renderer { get; private set; }
            public float Age { get; private set; }
            private Vector3 baseScale;

            private void Awake() => Renderer = GetComponent<SpriteRenderer>();

            public void Begin(Vector3 scale)
            {
                baseScale = scale;
                Age = 0f;
                transform.localScale = scale;
                Renderer.color = StartColor;
                gameObject.SetActive(true);
            }

            private void Update()
            {
                Age += Time.deltaTime;
                float k = Age / GhostLife;
                if (k >= 1f) { gameObject.SetActive(false); return; }
                // 처음에 빨리 옅어지고 끝자락은 천천히 사라지게(ease-out).
                float e = 1f - (1f - k) * (1f - k);
                Renderer.color = Color.Lerp(StartColor, EndColor, e);
                transform.localScale = baseScale * (1f + GhostGrow * e);
            }
        }
    }
}
