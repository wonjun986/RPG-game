using UnityEngine;

namespace Aethoria.Stages
{
    // NPC 머리 위 퀘스트 표시("!" 받을 의뢰 / "?" 보고할 의뢰)를 그림(UI/Common/QuestMarkAvailable · QuestMarkTurnIn)으로 바꿔 그린다.
    // NPC 쪽은 지금처럼 TextMesh 글자만 "!"/"?"로 바꾸고 켜고 끄면 된다 — 이 컴포넌트가 글자를 보고 그림을 고르고, 글자는 숨긴다.
    // 그림이 없으면 아무것도 하지 않는다(예전처럼 노란 글자). 위아래로 둥실 떠다니고 은은하게 맥동한다.
    [RequireComponent(typeof(TextMesh))]
    public class QuestMarkerArt : MonoBehaviour
    {
        private const float Size = 0.55f;   // 월드 단위(그림 한 변)

        private static Sprite available;
        private static Sprite turnIn;
        private static bool loaded;

        private TextMesh text;
        private MeshRenderer textRenderer;
        private SpriteRenderer sprite;

        public static void Apply(TextMesh marker)
        {
            if (marker == null || marker.GetComponent<QuestMarkerArt>() != null) return;
            if (!loaded)
            {
                available = Resources.Load<Sprite>("UI/Common/QuestMarkAvailable");
                turnIn = Resources.Load<Sprite>("UI/Common/QuestMarkTurnIn");
                loaded = true;
            }
            if (available == null || turnIn == null) return;
            marker.gameObject.AddComponent<QuestMarkerArt>();
        }

        private void Awake()
        {
            text = GetComponent<TextMesh>();
            textRenderer = GetComponent<MeshRenderer>();
            var go = new GameObject("QuestMarkerSprite");
            go.transform.SetParent(transform, false);
            sprite = go.AddComponent<SpriteRenderer>();
            sprite.sortingLayerID = textRenderer.sortingLayerID;
            sprite.sortingOrder = textRenderer.sortingOrder;
        }

        private void LateUpdate()
        {
            textRenderer.enabled = false;
            var s = text.text == "?" ? turnIn : available;
            if (sprite.sprite != s)
            {
                sprite.sprite = s;
                float px = Mathf.Max(s.rect.width, s.rect.height) / s.pixelsPerUnit;
                sprite.transform.localScale = Vector3.one * (Size / px);
            }
            float t = Time.time;
            sprite.transform.localPosition = new Vector3(0f, Mathf.Sin(t * 2.6f) * 0.06f, 0f);
            float pulse = 0.88f + 0.12f * Mathf.Sin(t * 4f);
            sprite.color = new Color(pulse, pulse, pulse, 1f);
        }
    }
}
