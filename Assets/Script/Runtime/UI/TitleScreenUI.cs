using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Aethoria.UI
{
    // 게임 시작 시 표시되는 타이틀 화면(UI/Title.png). GAME START를 누르면 화면을 닫고 콜백을 실행해
    // 실제 게임을 시작한다. EXIT는 게임을 종료한다.
    // CONTINUE/SETTINGS/EXTRAS는 아직 뒷받침하는 시스템이 없어 이미지에 장식으로만 그려져 있다(클릭 불가).
    public class TitleScreenUI : MonoBehaviour
    {
        public void Initialize(UnityAction onGameStart)
        {
            EnsureEventSystem();

            var canvasGO = new GameObject("TitleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            BuildBackground(canvasGO.transform, "UI/Title");

            // 아래 앵커 좌표는 Title.png 안에 이미 그려진 "GAME START"/"EXIT" 글자 위치에 맞춘 값이다.
            BuildButton(canvasGO.transform, "GameStartButton", new Vector2(0.65f, 0.433f), new Vector2(0.95f, 0.477f), () =>
            {
                Destroy(gameObject);
                onGameStart?.Invoke();
            });
            BuildButton(canvasGO.transform, "ExitButton", new Vector2(0.65f, 0.209f), new Vector2(0.95f, 0.259f), Application.Quit);
        }

        private static void BuildBackground(Transform parent, string resourcesPath)
        {
            var go = new GameObject("Background", typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<RawImage>().texture = Resources.Load<Texture2D>(resourcesPath);
        }

        // anchorMin/anchorMax: 화면 비율(0~1) 기준의 투명 버튼 영역. 배경 이미지 위에 그대로 겹친다.
        internal static void BuildButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // 보이지 않지만 클릭/레이캐스트는 받는다
            go.GetComponent<Button>().onClick.AddListener(onClick);
        }

        internal static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
