using UnityEngine;
using UnityEngine.UI;

namespace Aethoria.UI
{
    // NPC 근처에 가면 뜨는 짧은 대사창. 대화 프레임 이미지(예: UI/Noa_Talk) 위에 대사 텍스트를 얹어서 보여준다.
    public class NpcDialogueUI : MonoBehaviour
    {
        private const float BoxWidth = 1000f;
        private const float BoxHeight = BoxWidth * 724f / 2172f; // 대화 프레임 원본 비율(2172x724)에 맞춤

        private GameObject box;
        private RawImage frameImage;
        private Text dialogueText;
        private string currentResourcePath;

        public void Initialize()
        {
            box = new GameObject("NpcDialogueBox", typeof(RectTransform));
            var rect = (RectTransform)box.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(BoxWidth, BoxHeight);
            rect.anchoredPosition = new Vector2(0f, 30f);

            var frameGO = new GameObject("Frame", typeof(RectTransform), typeof(RawImage));
            var frameRect = (RectTransform)frameGO.transform;
            frameRect.SetParent(rect, false);
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;
            frameImage = frameGO.GetComponent<RawImage>();

            // 대화 프레임 그림 속 비어있는 대사 영역(초상화 오른쪽의 어두운 직사각형)에 맞춘 좌표.
            var textGO = new GameObject("DialogueText", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = new Vector2(0.32f, 0.22f);
            textRect.anchorMax = new Vector2(0.92f, 0.53f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            dialogueText = textGO.GetComponent<Text>();
            dialogueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dialogueText.fontSize = 22;
            dialogueText.color = Color.white;
            dialogueText.alignment = TextAnchor.MiddleLeft;
            dialogueText.horizontalOverflow = HorizontalWrapMode.Wrap;
            dialogueText.verticalOverflow = VerticalWrapMode.Overflow;

            box.SetActive(false);
        }

        public void Show(string resourcePath, string line)
        {
            if (currentResourcePath != resourcePath)
            {
                frameImage.texture = Resources.Load<Texture2D>(resourcePath);
                currentResourcePath = resourcePath;
            }
            dialogueText.text = line;
            box.SetActive(true);
        }

        public void Hide()
        {
            box.SetActive(false);
            currentResourcePath = null;
        }
    }
}
