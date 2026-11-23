using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aethoria.Monsters
{
    // 루나벨 2페이즈에 결전장 곳곳에 떠오르는 눈. 천천히 떠진 뒤 눈동자가 네크로시아를 따라 움직이고, 가끔 깜빡인다.
    // 눈 그림(eye_big_1~6)은 눈동자가 박혀 있어서, 다 뜬 뒤에는 눈동자를 지운 바탕(eye_open_base) 위에 따로 움직이는
    // 눈동자(gaze_iris)를 눈 모양 가림판(eye_open_mask, SpriteMask) 안에서만 보이게 얹는다.
    public class WatchingEye : MonoBehaviour
    {
        private const string Folder = "Backgrounds/LunabelGaze/";
        // 눈 그림(350x208, 가운데 피벗) 속 눈동자 자리와 크기(픽셀).
        private static readonly Vector2 IrisCenterPx = new Vector2(176f, 111f);
        private const float IrisDiameterPx = 168f;
        private const float ImageW = 350f, ImageH = 208f;
        private static readonly Vector2 LookRangePx = new Vector2(32f, 13f); // 눈동자가 움직이는 폭

        private static Sprite[] frames;
        private static Sprite baseSprite, maskSprite, irisSprite;

        private Transform target;
        private SpriteRenderer frameA, frameB, baseRenderer, irisRenderer;
        private Vector3 irisHome;
        private Vector2 look;
        private bool tracking;
        private float blinkTimer;
        private float seed;
        private Coroutine routine;

        public static bool AssetsReady
        {
            get
            {
                if (frames == null || frames.Length == 0 || frames[0] == null)
                {
                    frames = new Sprite[6];
                    for (int i = 0; i < 6; i++) frames[i] = Resources.Load<Sprite>($"{Folder}eye_big_{i + 1}");
                    baseSprite = Resources.Load<Sprite>(Folder + "eye_open_base");
                    maskSprite = Resources.Load<Sprite>(Folder + "eye_open_mask");
                    irisSprite = Resources.Load<Sprite>(Folder + "gaze_iris");
                }
                return frames[0] != null && frames[5] != null && baseSprite != null && maskSprite != null && irisSprite != null;
            }
        }

        // width: 눈 폭(유닛). delay: 떠지기 시작할 때까지 기다리는 시간.
        public static WatchingEye Spawn(Transform parent, Vector3 position, float width, int sortingOrder, Transform player, float delay)
        {
            if (!AssetsReady) return null;
            var go = new GameObject("WatchingEye", typeof(SortingGroup));
            go.transform.SetParent(parent);
            go.transform.position = position;
            float scale = width / (ImageW / baseSprite.pixelsPerUnit);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            go.GetComponent<SortingGroup>().sortingOrder = sortingOrder;
            var eye = go.AddComponent<WatchingEye>();
            eye.target = player;
            eye.Build();
            eye.routine = eye.StartCoroutine(eye.Open(delay));
            return eye;
        }

        private SpriteRenderer Child(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = new Color(1f, 1f, 1f, 0f);
            return sr;
        }

        private void Build()
        {
            seed = Random.Range(0f, 100f);
            blinkTimer = Random.Range(3f, 7f);
            baseRenderer = Child("Base", baseSprite, 0);

            var maskGO = new GameObject("Mask", typeof(SpriteMask));
            maskGO.transform.SetParent(transform, false);
            var mask = maskGO.GetComponent<SpriteMask>();
            mask.sprite = maskSprite;

            irisRenderer = Child("Iris", irisSprite, 1);
            irisRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            float ppu = baseSprite.pixelsPerUnit;
            irisHome = new Vector3((IrisCenterPx.x - ImageW / 2f) / ppu, (ImageH / 2f - IrisCenterPx.y) / ppu, 0f);
            irisRenderer.transform.localPosition = irisHome;
            float irisScale = IrisDiameterPx / irisSprite.rect.width * (irisSprite.pixelsPerUnit / ppu);
            irisRenderer.transform.localScale = new Vector3(irisScale, irisScale, 1f);

            frameA = Child("FrameA", frames[0], 2);
            frameB = Child("FrameB", frames[0], 3);
        }

        // 프레임 진행 open(0~5, 사이 값은 이웃한 두 장을 겹쳐 그린다)과 투명도.
        private void ShowFrames(float open, float alpha)
        {
            open = Mathf.Clamp(open, 0f, 5f);
            int i0 = Mathf.FloorToInt(open), i1 = Mathf.Min(5, i0 + 1);
            frameA.sprite = frames[i0];
            frameB.sprite = frames[i1];
            frameA.color = new Color(1f, 1f, 1f, alpha);
            frameB.color = new Color(1f, 1f, 1f, alpha * (open - i0));
        }

        private void ShowTracking(float alpha)
        {
            baseRenderer.color = new Color(1f, 1f, 1f, alpha);
            irisRenderer.color = new Color(1f, 1f, 1f, alpha);
        }

        private IEnumerator Open(float delay)
        {
            // 감긴 채 어둠 속에서 스며 나온다.
            for (float t = 0f; t < 0.5f; t += Time.deltaTime) { ShowFrames(0f, t / 0.5f * 0.8f); yield return null; }
            ShowFrames(0f, 0.8f);
            yield return new WaitForSeconds(delay);
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                float k = t * t * (3f - 2f * t);
                ShowFrames(k * 5f, Mathf.Lerp(0.8f, 1f, k));
                yield return null;
            }
            // 박힌 눈동자 그림 → 따로 움직이는 눈동자로 살짝 겹쳐 바꾼다.
            look = Vector2.zero;
            for (float t = 0f; t < 0.2f; t += Time.deltaTime)
            {
                ShowTracking(t / 0.2f);
                yield return null;
            }
            ShowTracking(1f);
            ShowFrames(5f, 0f);
            tracking = true;
            routine = null;
        }

        private IEnumerator Blink()
        {
            tracking = false;
            const float half = 0.13f;
            for (float t = 0f; t < half; t += Time.deltaTime) { float k = t / half; ShowFrames(5f - 4f * k, 1f); ShowTracking(1f - k); yield return null; }
            for (float t = 0f; t < half; t += Time.deltaTime) { float k = t / half; ShowFrames(1f + 4f * k, 1f); ShowTracking(k); yield return null; }
            ShowFrames(5f, 0f);
            ShowTracking(1f);
            tracking = true;
            routine = null;
        }

        public void Close()
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(CloseRoutine());
        }

        private IEnumerator CloseRoutine()
        {
            tracking = false;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float k = t / 0.6f;
                ShowTracking(1f - Mathf.Clamp01(k * 3f));
                ShowFrames(5f * (1f - k), 1f - k);
                yield return null;
            }
            Destroy(gameObject);
        }

        private void Update()
        {
            if (!tracking) return;
            if (target != null)
            {
                Vector3 eyeWorld = irisRenderer.transform.parent.TransformPoint(irisHome);
                Vector2 dir = ((Vector2)(target.position + Vector3.up * 0.8f) - (Vector2)eyeWorld);
                Vector2 want = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.zero;
                look = Vector2.Lerp(look, want, 1f - Mathf.Exp(-6f * Time.deltaTime));
                float ppu = baseSprite.pixelsPerUnit;
                irisRenderer.transform.localPosition = irisHome + new Vector3(look.x * LookRangePx.x / ppu, look.y * LookRangePx.y / ppu, 0f);
            }

            blinkTimer -= Time.deltaTime;
            if (blinkTimer <= 0f && routine == null)
            {
                blinkTimer = Random.Range(4f, 9f);
                routine = StartCoroutine(Blink());
            }
        }
    }
}
