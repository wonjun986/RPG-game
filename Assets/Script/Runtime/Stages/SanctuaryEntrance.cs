using UnityEngine;
using UnityEngine.InputSystem;

namespace Aethoria.Stages
{
    // 이리아의 성소 입구. 플레이어가 가까이 오면 머리 위에 "[Z] 입장하기"를 띄우고, Z를 누르면
    // 안으로 들어간다. 안에 이리아가 따로 있어서 여기서는 대화 없이 바로 입장만 처리한다.
    public class SanctuaryEntrance : MonoBehaviour
    {
        [SerializeField] private float interactRadius = 1.8f;
        [SerializeField] private float promptHeight = 3f;

        private Transform player;
        private System.Action onEnter;
        private TextMesh promptText;
        private bool inRange;

        public void Initialize(Transform playerTransform, System.Action enterCallback)
        {
            player = playerTransform;
            onEnter = enterCallback;
        }

        private void Awake()
        {
            CreatePrompt();
        }

        private void CreatePrompt()
        {
            var go = new GameObject("EnterPrompt");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, promptHeight, 0f);

            promptText = go.AddComponent<TextMesh>();
            promptText.text = "[Z] 입장하기";
            promptText.characterSize = 0.08f;
            promptText.fontSize = 42;
            promptText.alignment = TextAlignment.Center;
            promptText.anchor = TextAnchor.MiddleCenter;
            promptText.color = Color.white;

            go.GetComponent<MeshRenderer>().sortingOrder = 50;
            go.SetActive(false);
        }

        private void Update()
        {
            if (player == null) return;

            bool nowInRange = ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude <= interactRadius * interactRadius;
            if (nowInRange != inRange)
            {
                inRange = nowInRange;
                if (promptText != null) promptText.gameObject.SetActive(inRange);
            }
            if (!inRange) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.zKey.wasPressedThisFrame)
            {
                onEnter?.Invoke();
            }
        }
    }
}
