using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Bootstrap;
using Aethoria.Characters;
using Aethoria.Stages;

namespace Aethoria.UI
{
    // 마을 출구 포탈을 밟으면 뜨는 월드맵(UI/All_Area_Map). 지금 실제로 갈 수 있는 지역은
    // 어둠의 숲(기존 1~3스테이지+보스)과 광활한 평야뿐이라, 그 칸들만 갈 수 있고 나머지는 어둡게 덮어서
    // 아직 갈 수 없다는 것만 보여준다. SD 캐릭터(UI/SD)가 커서 역할을 하며 방향키로 지역
    // 사이를 옮겨 다니고, Enter/Z로 선택한다. "마을"을 선택하거나 Esc를 누르면 "마을로
    // 이동하시겠습니까?" 확인 창이 뜨고, 예를 골라야 실제로 창을 닫는다.
    public class WorldMapUI : MonoBehaviour
    {
        private class MapNode
        {
            public string Name;
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public bool Locked;
            public System.Action OnSelect;

            public Vector2 Center => (AnchorMin + AnchorMax) * 0.5f;
        }

        private const float AlertDuration = 1.6f;

        private GameObject panel;
        private RectTransform cursor;
        private Text alertText;
        private Coroutine alertRoutine;
        private Portal sourcePortal;
        private CharacterMovement2D playerMovement;

        private MapNode[] nodes;
        private MapNode villageNode;
        private int currentIndex;

        private GameObject confirmPanel;
        private bool confirmingExit;

        public void Initialize(CharacterMovement2D movement)
        {
            playerMovement = movement;
            BuildPanel();
        }

        private void BuildPanel()
        {
            TitleScreenUI.EnsureEventSystem();

            panel = new GameObject("WorldMapPanel", typeof(RectTransform), typeof(RawImage));
            var rect = (RectTransform)panel.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<RawImage>().texture = Resources.Load<Texture2D>("UI/All_Area_Map");

            // 아래 좌표들은 All_Area_Map.png 안에 이미 그려진 각 지역 라벨 위치에 맞춘 값
            // (0~1, 왼쪽 아래 기준). 마을 = 취소하고 그대로 있기, 어둠의 숲 = 유일하게 구현된 던전,
            // 나머지는 아직 콘텐츠가 없어 잠금 처리.
            nodes = new[]
            {
                new MapNode { Name = "마을", AnchorMin = new Vector2(0.11f, 0.69f), AnchorMax = new Vector2(0.25f, 0.81f), Locked = false },
                new MapNode { Name = "어둠의 숲", AnchorMin = new Vector2(0.34f, 0.67f), AnchorMax = new Vector2(0.48f, 0.78f), Locked = false, OnSelect = HandleEnterDarkForest },
                new MapNode { Name = "잊혀진 폐허", AnchorMin = new Vector2(0.59f, 0.68f), AnchorMax = new Vector2(0.73f, 0.79f), Locked = true },
                new MapNode { Name = "절망의 언덕", AnchorMin = new Vector2(0.78f, 0.74f), AnchorMax = new Vector2(0.92f, 0.84f), Locked = true },
                new MapNode { Name = "광활한 평야", AnchorMin = new Vector2(0.17f, 0.42f), AnchorMax = new Vector2(0.33f, 0.52f), Locked = false, OnSelect = HandleEnterPlains },
                new MapNode { Name = "엔스룸 마을", AnchorMin = new Vector2(0.39f, 0.27f), AnchorMax = new Vector2(0.54f, 0.36f), Locked = true },
                new MapNode { Name = "푸른 바다", AnchorMin = new Vector2(0.73f, 0.35f), AnchorMax = new Vector2(0.88f, 0.44f), Locked = true },
            };

            villageNode = nodes[0];

            foreach (var node in nodes)
            {
                var localNode = node;
                BuildNodeButton(panel.transform, localNode);
            }

            BuildCursor(panel.transform);
            BuildAlertText(panel.transform);
            BuildConfirmDialog(panel.transform);

            panel.SetActive(false);
        }

        // 클릭으로도 고를 수 있게, 잠긴 지역이면 안내문을 띄우고 아니면 선택한다(키보드 확인과 동일한 경로).
        private void BuildNodeButton(Transform parent, MapNode node)
        {
            TitleScreenUI.BuildButton(parent, node.Name, node.AnchorMin, node.AnchorMax, () =>
            {
                int index = System.Array.IndexOf(nodes, node);
                if (index >= 0) currentIndex = index;
                MoveCursorTo(node);
                TrySelect(node);
            });

            if (node.Locked)
            {
                var go = new GameObject(node.Name + "_Locked", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = node.AnchorMin;
                rect.anchorMax = node.AnchorMax;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.SetAsFirstSibling(); // 버튼보다 뒤에 그려서 클릭은 버튼이 그대로 받게 한다

                var image = go.GetComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0.6f);
                image.raycastTarget = false;
            }
        }

        private void BuildCursor(Transform parent)
        {
            var go = new GameObject("Cursor", typeof(RectTransform), typeof(RawImage));
            cursor = (RectTransform)go.transform;
            cursor.SetParent(parent, false);
            cursor.sizeDelta = new Vector2(72f, 76.6f);
            cursor.anchorMin = cursor.anchorMax = Vector2.zero;
            cursor.pivot = new Vector2(0.5f, 0.15f); // 발밑이 지역 라벨 위치에 오도록

            var image = go.GetComponent<RawImage>();
            image.texture = Resources.Load<Texture2D>("UI/SD");
            image.raycastTarget = false;
        }

        private void BuildAlertText(Transform parent)
        {
            var go = new GameObject("AlertText", typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.06f);
            rect.anchorMax = new Vector2(0.5f, 0.06f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(700f, 50f);

            alertText = go.GetComponent<Text>();
            alertText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            alertText.fontSize = 26;
            alertText.fontStyle = FontStyle.Bold;
            alertText.alignment = TextAnchor.MiddleCenter;
            alertText.color = new Color(1f, 0.4f, 0.4f);
            alertText.text = string.Empty;
        }

        // 마을 칸을 고르거나 Esc를 누르면 뜨는 확인 창. 실수로 지도를 닫는 걸 막는다.
        // Z/Enter = 예(그대로 진행), X/Esc = 아니오(지도로 돌아가기).
        private void BuildConfirmDialog(Transform parent)
        {
            confirmPanel = new GameObject("ExitConfirmPanel", typeof(RectTransform), typeof(Image));
            var dimRect = (RectTransform)confirmPanel.transform;
            dimRect.SetParent(parent, false);
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            var dimImage = confirmPanel.GetComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.65f);
            dimImage.raycastTarget = true; // 뒤쪽 지도 클릭을 막는다

            var boxGO = new GameObject("Box", typeof(RectTransform), typeof(Image));
            var boxRect = (RectTransform)boxGO.transform;
            boxRect.SetParent(confirmPanel.transform, false);
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(560f, 230f);
            boxGO.GetComponent<Image>().color = new Color(0.06f, 0.04f, 0.09f, 0.97f);

            var messageGO = new GameObject("Message", typeof(RectTransform), typeof(Text));
            var messageRect = (RectTransform)messageGO.transform;
            messageRect.SetParent(boxRect, false);
            messageRect.anchorMin = new Vector2(0f, 0.5f);
            messageRect.anchorMax = new Vector2(1f, 1f);
            messageRect.offsetMin = Vector2.zero;
            messageRect.offsetMax = Vector2.zero;
            var messageText = messageGO.GetComponent<Text>();
            messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            messageText.fontSize = 28;
            messageText.fontStyle = FontStyle.Bold;
            messageText.alignment = TextAnchor.MiddleCenter;
            messageText.color = Color.white;
            messageText.text = "마을로 이동하시겠습니까?";

            BuildDialogButton(boxRect, "YesButton", "예 (Z)", new Vector2(0.08f, 0.12f), new Vector2(0.47f, 0.42f), ConfirmExitYes);
            BuildDialogButton(boxRect, "NoButton", "아니오 (X)", new Vector2(0.53f, 0.12f), new Vector2(0.92f, 0.42f), ConfirmExitNo);

            confirmPanel.SetActive(false);
        }

        internal static void BuildDialogButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var textGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
            var textRect = (RectTransform)textGO.transform;
            textRect.SetParent(rect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
        }

        public void Show(Portal portal)
        {
            sourcePortal = portal;
            currentIndex = 0;
            confirmingExit = false;
            confirmPanel.SetActive(false);
            panel.SetActive(true);
            MoveCursorTo(nodes[currentIndex]);
            HideAlert();
            if (playerMovement != null) playerMovement.SetInputLocked(true);
            Time.timeScale = 0f;
        }

        private void Hide()
        {
            panel.SetActive(false);
            Time.timeScale = 1f;
            if (playerMovement != null) playerMovement.SetInputLocked(false);
        }

        private void Update()
        {
            if (panel == null || !panel.activeSelf) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (confirmingExit)
            {
                if (keyboard.zKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    ConfirmExitYes();
                }
                else if (keyboard.xKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
                {
                    ConfirmExitNo();
                }
                return;
            }

            // 굳이 마을 칸까지 이동하지 않아도 Esc 한 번으로 나갈지 물어볼 수 있게 한다.
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                ShowExitConfirm();
                return;
            }

            if (keyboard.leftArrowKey.wasPressedThisFrame) MoveSelection(Vector2.left);
            else if (keyboard.rightArrowKey.wasPressedThisFrame) MoveSelection(Vector2.right);
            else if (keyboard.upArrowKey.wasPressedThisFrame) MoveSelection(Vector2.up);
            else if (keyboard.downArrowKey.wasPressedThisFrame) MoveSelection(Vector2.down);

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.zKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                TrySelect(nodes[currentIndex]);
            }
        }

        // 현재 노드에서 direction 쪽으로 가장 "정면"에 가까우면서 가까운 노드를 찾아 커서를 옮긴다.
        // (같은 방향으로의 치우침이 클수록, 거리가 가까울수록 우선한다 — 표준 방향키 메뉴 내비게이션 방식)
        private void MoveSelection(Vector2 direction)
        {
            Vector2 currentPos = nodes[currentIndex].Center;
            int bestIndex = -1;
            float bestScore = float.MaxValue;

            for (int i = 0; i < nodes.Length; i++)
            {
                if (i == currentIndex) continue;

                Vector2 delta = nodes[i].Center - currentPos;
                float along = Vector2.Dot(delta, direction);
                if (along <= 0.01f) continue;

                float perpendicular = (delta - direction * along).magnitude;
                float score = along + perpendicular * 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0) return;

            currentIndex = bestIndex;
            MoveCursorTo(nodes[currentIndex]);
            HideAlert();
        }

        private void MoveCursorTo(MapNode node)
        {
            if (cursor == null) return;
            cursor.anchorMin = cursor.anchorMax = node.Center;
            cursor.anchoredPosition = Vector2.zero;
        }

        private void TrySelect(MapNode node)
        {
            if (node.Locked)
            {
                ShowAlert($"{node.Name}은(는) 아직 갈 수 없습니다.");
                return;
            }

            if (node == villageNode)
            {
                ShowExitConfirm();
                return;
            }

            node.OnSelect?.Invoke();
        }

        private void ShowExitConfirm()
        {
            confirmingExit = true;
            confirmPanel.SetActive(true);
        }

        private void ConfirmExitYes()
        {
            confirmingExit = false;
            confirmPanel.SetActive(false);
            HandleCancel();
        }

        private void ConfirmExitNo()
        {
            confirmingExit = false;
            confirmPanel.SetActive(false);
        }

        private void ShowAlert(string message)
        {
            alertText.text = message;
            if (alertRoutine != null) StopCoroutine(alertRoutine);
            alertRoutine = StartCoroutine(HideAlertAfterDelay());
        }

        private void HideAlert()
        {
            if (alertRoutine != null) StopCoroutine(alertRoutine);
            alertText.text = string.Empty;
        }

        // 월드맵이 떠 있는 동안 Time.timeScale = 0이라 WaitForSeconds는 흐르지 않는다.
        // 실시간으로 흐르는 WaitForSecondsRealtime을 써야 실제로 잠깐 뒤에 사라진다.
        private IEnumerator HideAlertAfterDelay()
        {
            yield return new WaitForSecondsRealtime(AlertDuration);
            alertText.text = string.Empty;
            alertRoutine = null;
        }

        private void HandleEnterDarkForest()
        {
            Hide();
            GameBootstrap.EnterNextStage();
        }

        private void HandleEnterPlains()
        {
            Hide();
            GameBootstrap.EnterPlains();
        }

        private void HandleCancel()
        {
            Hide();
            sourcePortal?.ResetTrigger();
        }
    }
}
