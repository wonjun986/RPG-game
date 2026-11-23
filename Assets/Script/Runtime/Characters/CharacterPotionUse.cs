using UnityEngine;
using UnityEngine.InputSystem;
using Aethoria.Combat;
using Aethoria.Data;
using Aethoria.Bootstrap;

namespace Aethoria.Characters
{
    // F = 회복 포션 마시기. 가방의 회복 포션(ItemDatabase.Potion)을 하나 마셔 체력을 회복하고,
    // 머리 위에 회복량을 초록 숫자로 띄운다. 연달아 들이켜지 않게 짧은 쿨타임을 둔다.
    // 대화/메뉴 등으로 조작이 잠겨 있거나 체력이 가득 차 있으면 마시지 않는다.
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(CharacterMovement2D))]
    [RequireComponent(typeof(Inventory))]
    public class CharacterPotionUse : MonoBehaviour
    {
        [SerializeField] private float cooldown = 1f;

        private static readonly Color HealPopupColor = new Color(0.35f, 0.95f, 0.45f);

        private Character character;
        private CharacterMovement2D movement;
        private Inventory inventory;
        private float cooldownRemaining;

        // HUD 포션 칸(PotionSlotUI)이 쿨타임을 그린다.
        public float Cooldown => cooldown;
        public float CooldownRemaining => cooldownRemaining;

        private void Awake()
        {
            character = GetComponent<Character>();
            movement = GetComponent<CharacterMovement2D>();
            inventory = GetComponent<Inventory>();
        }

        private void Update()
        {
            if (cooldownRemaining > 0f)
            {
                cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
            }

            if (movement.IsInputLocked) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                TryDrink();
            }
        }

        private void TryDrink()
        {
            if (character.IsDead || cooldownRemaining > 0f) return;
            if (character.ItemsLocked)
            {
                // 루나벨의 주시: 아이템을 쓸 수 없다고 머리 위에 알려 준다.
                var go = new GameObject("ItemLockedText");
                go.transform.position = transform.position + Vector3.up * 2.1f;
                var text = go.AddComponent<TextMesh>();
                text.text = "주시당하고 있다 — 아이템을 쓸 수 없다";
                text.characterSize = 0.06f;
                text.fontSize = 40;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = new Color(1f, 0.35f, 0.4f);
                go.GetComponent<MeshRenderer>().sortingOrder = 60;
                go.AddComponent<Aethoria.Stages.FloatingTextFade>();
                cooldownRemaining = 0.6f; // 연타로 글자가 겹쳐 쌓이지 않게
                return;
            }
            if (character.CurrentHp >= character.MaxHp) return;

            var potion = ItemDatabase.Potion;
            if (!inventory.TryRemoveItem(potion, 1)) return;

            cooldownRemaining = cooldown;
            float before = character.CurrentHp;
            character.Heal(potion.healAmount * character.TalentPotionMultiplier); // 축복 특성 성수
            SfxPlayer.Play(SfxPlayer.PotionDrink);
            DamagePopup.Create(transform.position + Vector3.up * 1.2f, character.CurrentHp - before, HealPopupColor);
        }
    }
}
