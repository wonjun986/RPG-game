using System.Collections;
using UnityEngine;
using Aethoria.Characters;
using Aethoria.UI;

namespace Aethoria.Stages
{
    // 절망의 언덕 결전장까지 걸어가는 길의 연출. 조작은 막지 않는다 — 걷는 동안
    //  - 정해진 자리를 지날 때마다 루나벨의 목소리(또는 네크로시아의 혼잣말)가 몇 초 떴다 사라지고,
    //  - 한 번은 길 앞쪽에 붉은 루나벨의 환영이 나타났다가, 다가가면 피안개처럼 흩어진다.
    public class HillApproach : MonoBehaviour
    {
        private const float LineTime = 3.4f;

        public struct Voice
        {
            public float X;
            public bool Necrosia;
            public string Line;
            public float PhantomX;   // 0이 아니면 이 대사와 함께 이 자리에 환영이 나타난다
        }

        private Transform player;
        private NpcDialogueUI dialogueUI;
        private Voice[] voices;
        private int next;
        private Coroutine lineRoutine;
        private Sprite phantomSprite;
        private float floorY;

        public void Initialize(Transform playerTransform, NpcDialogueUI dialogue, Voice[] lines, Sprite lunabelSprite, float mapFloorY)
        {
            player = playerTransform;
            dialogueUI = dialogue;
            voices = lines;
            phantomSprite = lunabelSprite;
            floorY = mapFloorY;
        }

        private void Update()
        {
            if (player == null || voices == null || next >= voices.Length) return;
            if (player.position.x < voices[next].X) return;

            var voice = voices[next++];
            if (lineRoutine != null) StopCoroutine(lineRoutine);
            lineRoutine = StartCoroutine(ShowLine(voice));
            if (voice.PhantomX != 0f && phantomSprite != null) StartCoroutine(Phantom(voice.PhantomX));
        }

        private IEnumerator ShowLine(Voice voice)
        {
            if (voice.Necrosia) dialogueUI?.ShowNecrosia(voice.Line);
            else dialogueUI?.ShowLunabel(voice.Line);
            float t = 0f;
            // 타이핑이 끝나고 나서 LineTime만큼 보여 준다.
            while (dialogueUI != null && dialogueUI.IsBusy && t < 6f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(LineTime);
            dialogueUI?.Hide();
            lineRoutine = null;
        }

        // 붉은 환영: 스르륵 나타나 숨 쉬듯 떠 있다가, 네크로시아가 가까이 오거나 몇 초 지나면 위로 흩어지며 사라진다.
        private IEnumerator Phantom(float x)
        {
            var go = new GameObject("LunabelPhantom", typeof(SpriteRenderer));
            go.transform.SetParent(transform);
            go.transform.position = new Vector3(x, floorY, 0f);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = phantomSprite;
            sr.flipX = true; // 다가오는 네크로시아(왼쪽)를 본다
            sr.sortingOrder = 4;
            var tint = new Color(1f, 0.25f, 0.3f);

            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                sr.color = new Color(tint.r, tint.g, tint.b, 0.55f * (t / 0.8f));
                yield return null;
            }

            float hold = 0f;
            while (hold < 4f && player != null && Mathf.Abs(player.position.x - x) > 3.5f)
            {
                hold += Time.deltaTime;
                float breath = 0.45f + 0.12f * Mathf.Sin(hold * 3f);
                sr.color = new Color(tint.r, tint.g, tint.b, breath);
                go.transform.position = new Vector3(x, floorY + 0.05f * Mathf.Sin(hold * 2f), 0f);
                yield return null;
            }

            Vector3 start = go.transform.position;
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                float k = t / 0.9f;
                sr.color = new Color(tint.r, tint.g, tint.b, 0.5f * (1f - k));
                go.transform.position = start + Vector3.up * (0.6f * k);
                go.transform.localScale = new Vector3(1f + 0.25f * k, 1f - 0.15f * k, 1f);
                yield return null;
            }
            Destroy(go);
        }

        private void OnDisable()
        {
            if (lineRoutine != null) dialogueUI?.Hide();
        }
    }
}
