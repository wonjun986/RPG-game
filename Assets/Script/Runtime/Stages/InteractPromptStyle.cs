using UnityEngine;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // NPC·분수·성소 입구 머리 위 "[Z] 대화하기" 같은 상호작용 안내를 한 모양으로 맞춘다.
    //  - 글자 뒤에 둥근 반투명 말풍선(그림이 있으면 UI/Common/PromptBubble, 9-slice)을 글자 크기에 맞춰 깐다.
    //  - [Z] 같은 키 표시는 금빛으로 칠한다.
    //  - 나타날 때 톡 튀어나오고, 떠 있는 동안 위아래로 살짝 떠다닌다.
    // 각 NPC는 지금처럼 TextMesh를 만들고 켜고 끄기만 하면 된다 — 만든 뒤 InteractPromptStyle.Apply(text)만 부르면 된다.
    [RequireComponent(typeof(TextMesh))]
    public class InteractPromptStyle : MonoBehaviour
    {
        private const string KeyColor = "#FFD36B";
        private static readonly Vector2 Padding = new Vector2(0.5f, 0.2f);   // 양 끝이 둥글어서 가로 여백을 넉넉히

        private TextMesh text;
        private MeshRenderer textRenderer;
        private SpriteRenderer bubble;
        private Vector3 basePosition;
        private string lastStyled;
        private float shownAt;

        public static void Apply(TextMesh textMesh)
        {
            if (textMesh != null && textMesh.GetComponent<InteractPromptStyle>() == null)
                textMesh.gameObject.AddComponent<InteractPromptStyle>();
        }

        private void Awake()
        {
            text = GetComponent<TextMesh>();
            textRenderer = GetComponent<MeshRenderer>();
            basePosition = transform.localPosition;

            var go = new GameObject("PromptBubble");
            go.transform.SetParent(transform, false);
            bubble = go.AddComponent<SpriteRenderer>();
            bubble.sprite = UiArt.LoadOr("UI/Common/PromptBubble",
                () => UiArt.RoundedPanel(new Color(0.06f, 0.03f, 0.09f, 0.78f), new Color(0.7f, 0.55f, 0.95f, 0.9f), 8, 2));
            bubble.drawMode = SpriteDrawMode.Sliced;
            bubble.sortingLayerID = textRenderer.sortingLayerID;
            bubble.sortingOrder = textRenderer.sortingOrder - 1;
        }

        private void OnEnable()
        {
            shownAt = Time.unscaledTime;
        }

        private void LateUpdate()
        {
            // 글자가 바뀌었으면(예: 이시스의 "새 기술!") 키 표시를 다시 칠한다.
            if (text.text != lastStyled)
            {
                lastStyled = StyleKeys(text.text);
                text.text = lastStyled;
            }

            // 톡 튀어나오기 + 둥실 떠다니기.
            float t = Time.unscaledTime - shownAt;
            float k = Mathf.Clamp01(t / 0.2f);
            float m = k - 1f;
            float pop = 1f + 2.7f * m * m * m + 1.7f * m * m;   // 살짝 넘쳤다가 제자리
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.5f, 1f, pop);
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(t * 3f) * 0.04f);

            // 말풍선을 글자 크기에 맞춘다. TextMesh에는 MeshFilter가 없어서, 글자가 실제로 그려진 범위(렌더러의 월드 경계)를
            // 이 오브젝트의 로컬 크기로 되돌려 쓴다(톡 튀는 크기·NPC 쪽 크기 보정까지 나눠서 뺀다).
            Bounds b = textRenderer.bounds;
            if (b.size.x <= 0f) return;
            Vector3 scale = transform.lossyScale;
            float sx = Mathf.Max(0.0001f, Mathf.Abs(scale.x)), sy = Mathf.Max(0.0001f, Mathf.Abs(scale.y));
            Vector3 center = transform.InverseTransformPoint(b.center);
            bubble.transform.localPosition = new Vector3(center.x, center.y, 0.01f);
            bubble.size = new Vector2(b.size.x / sx + Padding.x, b.size.y / sy + Padding.y);
            bubble.sortingOrder = textRenderer.sortingOrder - 1;
        }

        // "[Z]" "[1]" 같은 대괄호 키 표시를 금빛으로. 이미 색을 입힌 부분은 건드리지 않는다.
        private static string StyleKeys(string raw)
        {
            if (string.IsNullOrEmpty(raw) || raw.Contains("<color=" + KeyColor)) return raw;
            var sb = new System.Text.StringBuilder(raw.Length + 32);
            int i = 0;
            while (i < raw.Length)
            {
                int open = raw.IndexOf('[', i);
                int close = open >= 0 ? raw.IndexOf(']', open) : -1;
                if (open < 0 || close < 0 || close - open > 6)
                {
                    sb.Append(raw, i, raw.Length - i);
                    break;
                }
                sb.Append(raw, i, open - i);
                sb.Append("<color=").Append(KeyColor).Append('>').Append(raw, open, close - open + 1).Append("</color>");
                i = close + 1;
            }
            return sb.ToString();
        }
    }
}
