using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Aethoria.Characters;
using Aethoria.Combat;
using Aethoria.Data;
using Aethoria.Monsters;
using Aethoria.Bootstrap;

namespace Aethoria.UI
{
    // 채보 재생: 노트 이동, 판정, 롱노트, 판정 표시, 체력바 갱신.
    public partial class RhythmBattleUI
    {
        // 채보 하나를 처음부터 끝까지 재생한다. 각 노드는 자기 HitTime보다 telegraphDuration 먼저
        // 오른쪽 끝에서 출발해 일정한 속도로 날아오므로, 화면 위 노드 사이 간격이 곧 리듬 간격이다.
        // 여러 노드가 동시에 떠 있을 수 있고, 키(위=Z, 아래=X)를 누르면 그 레인에서 아직 판정 안 된
        // 노드 중 타이밍이 가장 가까운 것을 판정한다: ±acceptWindow 안이면 맞힘, 그보다 일찍인데
        // EarlyMissRange 안이면 그 노드를 놓친 것으로 소모한다(마구 누르기 방지). 그보다 더 멀면 무시.
        // 판정점을 acceptWindow 넘게 지나친 노드도 놓친 것이다. 판정될 때마다 onJudged(노드, 맞힘?, 오차)를 부른다.
        // onIncoming: 노드가 판정점에 닿기 incomingLead초 전에 노드마다 한 번 불린다(루나벨이 그 노드에 맞춰 피의 손을 날린다).
        private IEnumerator RunChart(List<ChartNote> notes, Color noteColor, float acceptWindow, System.Action<ChartNote, bool, float> onJudged,
            System.Action<ChartNote> onIncoming = null, float incomingLead = 0f)
        {
            resultText.text = "";
            HideJudgment();
            int nextSpawn = 0;
            int judgedCount = 0;
            float t = 0f;

            while (judgedCount < notes.Count)
            {
                t += Time.unscaledDeltaTime;

                while (nextSpawn < notes.Count && t >= notes[nextSpawn].HitTime - telegraphDuration)
                {
                    var spawned = notes[nextSpawn++];
                    spawned.Image = RentNoteImage();
                    if (spawned.Image != null)
                    {
                        spawned.Image.color = noteColor;
                        spawned.Image.rectTransform.localScale = Vector3.one;
                        SkinNote(spawned.Image, useLunabelNotes ? lunabelTapSprite : null, LunabelNoteSize);
                        spawned.Image.gameObject.SetActive(true);
                    }
                    if (spawned.IsHold)
                    {
                        spawned.BarImage = RentHoldBar();
                        if (spawned.BarImage != null)
                        {
                            spawned.BarImage.color = new Color(noteColor.r, noteColor.g, noteColor.b, HoldBarAlpha);
                            spawned.BarImage.sprite = useLunabelNotes ? lunabelBarSprite : null;
                            if (useLunabelNotes) spawned.BarImage.color = Color.white;
                            spawned.BarImage.gameObject.SetActive(true);
                        }
                        spawned.TailImage = RentNoteImage();
                        if (spawned.TailImage != null)
                        {
                            spawned.TailImage.color = noteColor;
                            spawned.TailImage.rectTransform.localScale = Vector3.one * HoldTailScale;
                            if (useLunabelNotes)
                            {
                                SkinNote(spawned.TailImage, lunabelTailSprite, LunabelTailSize);
                                spawned.TailImage.rectTransform.localScale = Vector3.one;
                            }
                            else SkinNote(spawned.TailImage, null, 0f);
                            spawned.TailImage.gameObject.SetActive(true);
                        }
                    }
                }

                if (onIncoming != null)
                {
                    for (int i = 0; i < nextSpawn; i++)
                    {
                        var note = notes[i];
                        if (note.Announced || note.Judged || t < note.HitTime - incomingLead) continue;
                        note.Announced = true;
                        onIncoming(note);
                    }
                }

                // 지나쳐 버린 노드를 먼저 놓침 처리하고 위치를 옮긴 뒤에 입력을 본다 — 그래야 이미 늦은
                // 노드가 이번 입력의 판정 대상으로 잡히지 않는다.
                for (int i = 0; i < nextSpawn; i++)
                {
                    var note = notes[i];
                    if (note.Judged) continue;

                    if (note.Holding)
                    {
                        // 누르고 있는 롱노트: 꼬리가 판정점에 닿으면 성공, 그 전에 떼면 실패.
                        bool stillHeld = LaneKeyHeld(note.Lane);
                        if (t >= note.EndTime || (!stillHeld && t >= note.EndTime - HoldReleaseGrace))
                        {
                            Judge(note, true, note.HeadDiff, onJudged);
                            judgedCount++;
                            if (BattleEnded()) yield break;
                            continue;
                        }
                        if (!stillHeld)
                        {
                            Judge(note, false, float.MaxValue, onJudged);
                            judgedCount++;
                            if (BattleEnded()) yield break;
                            continue;
                        }
                        if (t >= note.NextHoldSpark)
                        {
                            note.NextHoldSpark = t + HoldSparkInterval;
                            PlayNodeBurst(note.Lane);
                        }
                    }
                    else if (t > note.HitTime + acceptWindow)
                    {
                        Judge(note, false, float.MaxValue, onJudged);
                        judgedCount++;
                        if (BattleEnded()) yield break;
                        continue;
                    }

                    float laneY = NoteY(note, t);
                    float headX = note.Holding ? NodeCenter[note.Lane].x : NoteX(note.Lane, note.HitTime, t);
                    if (note.Image != null)
                    {
                        note.Image.rectTransform.anchoredPosition = ImageToCanvasPos(headX, laneY);
                        if (vanishNotes && !note.Holding)
                        {
                            float travel = 1f - (note.HitTime - t) / telegraphDuration;
                            Color c = note.Image.color;
                            c.a = noteColor.a * (1f - Mathf.InverseLerp(VanishFadeStart, VanishFadeEnd, travel));
                            note.Image.color = c;
                        }
                    }
                    if (note.IsHold)
                    {
                        float tailX = Mathf.Max(headX, NoteX(note.Lane, note.EndTime, t));
                        if (note.TailImage != null) note.TailImage.rectTransform.anchoredPosition = ImageToCanvasPos(tailX, laneY);
                        if (note.BarImage != null)
                        {
                            var bar = note.BarImage.rectTransform;
                            bar.anchoredPosition = ImageToCanvasPos(headX, laneY);
                            bar.sizeDelta = new Vector2((tailX - headX) * PxToCanvas, useLunabelNotes ? LunabelBarThickness : NoteSize * HoldBarThickness);
                        }
                    }
                }

                var keyboard = Keyboard.current;
                for (int lane = 0; lane < 2; lane++)
                {
                    bool pressed = keyboard != null && (lane == LaneUp
                        ? keyboard.zKey.wasPressedThisFrame
                        : keyboard.xKey.wasPressedThisFrame);
                    if (!pressed) continue;

                    var target = ClosestNote(notes, nextSpawn, lane, t);
                    if (target == null) continue;
                    float signedDiff = t - target.HitTime; // 음수면 일찍 누른 것.
                    float diff = Mathf.Abs(signedDiff);
                    if (diff <= acceptWindow && target.IsHold)
                    {
                        // 롱노트 머리: 판정은 꼬리까지 버틴 뒤에 한 번 한다. 지금은 잡기만 한다.
                        target.Holding = true;
                        target.HeadDiff = diff;
                        target.NextHoldSpark = t + HoldSparkInterval;
                        PlayNodeBurst(lane);
                        continue;
                    }
                    if (diff <= acceptWindow)
                    {
                        Judge(target, true, diff, onJudged);
                    }
                    else if (signedDiff < 0f && diff <= acceptWindow + EarlyMissRange)
                    {
                        Judge(target, false, diff, onJudged);
                    }
                    else
                    {
                        continue;
                    }
                    judgedCount++;
                    if (BattleEnded()) yield break;
                }

                // 레인 위에 아직 처리할 노드가 날아오고 있으면 그 레인 판정점 위에 "!"를 띄운다.
                for (int lane = 0; lane < 2; lane++)
                {
                    exclaim[lane].gameObject.SetActive(!vanishNotes && LaneHasIncoming(notes, nextSpawn, lane, t));
                }

                yield return null;
            }

            for (int lane = 0; lane < 2; lane++) exclaim[lane].gameObject.SetActive(false);
        }

        private void Judge(ChartNote note, bool hit, float diff, System.Action<ChartNote, bool, float> onJudged)
        {
            note.Judged = true;
            note.Holding = false;
            if (note.Image != null)
            {
                note.Image.gameObject.SetActive(false);
                note.Image = null;
            }
            if (note.TailImage != null)
            {
                note.TailImage.rectTransform.localScale = Vector3.one;
                note.TailImage.gameObject.SetActive(false);
                note.TailImage = null;
            }
            if (note.BarImage != null)
            {
                note.BarImage.gameObject.SetActive(false);
                note.BarImage = null;
            }
            if (hit) PlayNodeBurst(note.Lane);
            SfxPlayer.Play(hit ? SfxPlayer.RhythmHit : SfxPlayer.RhythmMiss);
            onJudged(note, hit, diff);
        }

        // ---- 롱노트 ----
        private const int HoldBarPoolSize = 6;
        private const float HoldBarAlpha = 0.55f;
        private const float HoldBarThickness = 0.42f; // 노트 크기 대비 막대 굵기
        private const float HoldTailScale = 0.7f;
        // 꼬리 직전 이만큼(초) 안에서 떼는 건 끝까지 버틴 것으로 쳐 준다.
        private const float HoldReleaseGrace = 0.1f;
        private const float HoldSparkInterval = 0.12f;
        private readonly Image[] holdBarPool = new Image[HoldBarPoolSize];

        private float NoteX(int lane, float hitTime, float t)
        {
            float travelT = 1f - (hitTime - t) / telegraphDuration;
            return Mathf.LerpUnclamped(NoteSpawnX, NodeCenter[lane].x, travelT);
        }

        private static bool LaneKeyHeld(int lane)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return lane == LaneUp ? keyboard.zKey.isPressed : keyboard.xKey.isPressed;
        }

        private Image RentHoldBar()
        {
            foreach (var image in holdBarPool)
            {
                if (!image.gameObject.activeSelf) return image;
            }
            return null;
        }

        // 페이크 노드는 SwitchTime부터 FeintSwitchDuration 동안 보이던 레인에서 진짜 레인으로 미끄러진다.
        private float NoteY(ChartNote note, float t)
        {
            float realY = NodeCenter[note.Lane].y;
            if (note.ShownLane == note.Lane) return realY;
            float shownY = NodeCenter[note.ShownLane].y;
            return Mathf.Lerp(shownY, realY, Mathf.Clamp01((t - note.SwitchTime) / FeintSwitchDuration));
        }

        // "!" 표시는 지금 화면에 보이는 레인 기준으로 띄운다(페이크 노드의 진짜 레인을 미리 알려 주지 않게).
        private static bool LaneHasIncoming(List<ChartNote> notes, int spawnedCount, int lane, float t)
        {
            for (int i = 0; i < spawnedCount; i++)
            {
                var note = notes[i];
                if (note.Judged) continue;
                int shown = t < note.SwitchTime ? note.ShownLane : note.Lane;
                if (shown == lane) return true;
            }
            return false;
        }

        // 이미 출발한 노드 중, 이 레인에서 아직 판정 안 됐고 지금 시각 t와 타이밍이 가장 가까운 노드.
        private static ChartNote ClosestNote(List<ChartNote> notes, int spawnedCount, int lane, float t)
        {
            ChartNote best = null;
            float bestDiff = float.MaxValue;
            for (int i = 0; i < spawnedCount; i++)
            {
                var note = notes[i];
                if (note.Judged || note.Holding || note.Lane != lane) continue;
                float diff = Mathf.Abs(t - note.HitTime);
                if (diff < bestDiff)
                {
                    best = note;
                    bestDiff = diff;
                }
            }
            return best;
        }

        // 풀에서 빌린 노드 그림을 기본 다이아몬드(customSprite == null)나 루나벨 노드로 맞춘다. 루나벨 노드는 원래 색을 살리도록
        // 흰색으로 칠하고, 그림 비율대로 size(세로 기준) 크기로 그린다.
        private void SkinNote(Image image, Sprite customSprite, float size)
        {
            var rect = image.rectTransform;
            if (customSprite == null)
            {
                image.sprite = defaultNoteSprite;
                rect.sizeDelta = new Vector2(NoteSize, NoteSize);
                return;
            }
            image.sprite = customSprite;
            image.color = new Color(1f, 1f, 1f, image.color.a);
            Vector2 px = customSprite.rect.size;
            rect.sizeDelta = new Vector2(size * px.x / px.y, size);
        }

        private Image RentNoteImage()
        {
            foreach (var image in notePool)
            {
                if (!image.gameObject.activeSelf) return image;
            }
            return null;
        }

        private void ShowResult(string text, Color color)
        {
            HideJudgment();
            resultText.text = text;
            resultText.color = color;
        }

        // 내 공격/스킬 채보의 판정은 글자 대신 판정 로고(UI/Judgment)를 띄운다. 판정 단계와 로고는 강한 순서대로
        // Perfect → PERFECT, Good → EXELLENT, OK → GOOD, Miss → MISS로 맞췄다.
        private void ShowJudgment(Sprite logo)
        {
            if (logo == null) return;
            resultText.text = "";
            if (judgmentRoutine != null) StopCoroutine(judgmentRoutine);
            judgmentRoutine = StartCoroutine(JudgmentRoutine(logo));
        }

        private IEnumerator JudgmentRoutine(Sprite logo)
        {
            judgmentImage.sprite = logo;
            judgmentImage.gameObject.SetActive(true);
            var rect = judgmentImage.rectTransform;
            float t = 0f;
            while (t < JudgmentDuration)
            {
                t += Time.unscaledDeltaTime;
                // 처음엔 크게 튀어나왔다가 제 크기로 줄어들고, 끝 무렵 흐려지며 사라진다.
                float pop = Mathf.Lerp(JudgmentPopScale, 1f, Mathf.Clamp01(t / JudgmentPopTime));
                rect.localScale = new Vector3(pop, pop, 1f);
                float k = t / JudgmentDuration;
                float alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
                judgmentImage.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }
            judgmentImage.gameObject.SetActive(false);
            judgmentRoutine = null;
        }

        private void HideJudgment()
        {
            if (judgmentRoutine != null)
            {
                StopCoroutine(judgmentRoutine);
                judgmentRoutine = null;
            }
            if (judgmentImage != null) judgmentImage.gameObject.SetActive(false);
        }

        // 노드를 제때 맞혔을 때(막기 성공/공격 적중 공통) 그 노드 자리에서 한 번 터지는 연출.
        // 턴 진행을 막지 않도록 fire-and-forget으로 띄운다.
        private void PlayNodeBurst(int lane)
        {
            if (nodeBurstFrames.Length == 0) return;
            StartCoroutine(PlayNodeBurstRoutine(lane));
        }

        private IEnumerator PlayNodeBurstRoutine(int lane)
        {
            nodeBurstImage.rectTransform.anchoredPosition = ImageToCanvasPos(NodeCenter[lane].x, NodeCenter[lane].y);
            nodeBurstImage.gameObject.SetActive(true);
            foreach (var frame in nodeBurstFrames)
            {
                nodeBurstImage.sprite = frame;
                yield return new WaitForSecondsRealtime(NodeBurstFrameInterval);
            }
            nodeBurstImage.gameObject.SetActive(false);
        }

        private void RefreshHpBars()
        {
            if (monster != null && isPractice)
            {
                monsterHpFill.fillAmount = 1f;
                monsterHpText.text = $"누적 피해 {Mathf.RoundToInt(practiceDamageTotal):N0}";
            }
            else if (monster != null)
            {
                float ratio = monster.MaxHp > 0f ? Mathf.Clamp01(monster.CurrentHp / monster.MaxHp) : 0f;
                monsterHpFill.fillAmount = ratio;
                monsterHpText.text = $"{Mathf.CeilToInt(monster.CurrentHp)} / {Mathf.CeilToInt(monster.MaxHp)}";
            }
            if (player != null)
            {
                float ratio = player.MaxHp > 0f ? Mathf.Clamp01(player.CurrentHp / player.MaxHp) : 0f;
                playerHpFill.fillAmount = ratio;
                playerHpText.text = $"{Mathf.CeilToInt(player.CurrentHp)} / {Mathf.CeilToInt(player.MaxHp)}";
            }
            skillGaugeFill.fillAmount = Mathf.Clamp01(skillGauge / MaxSkillGauge);
            skillGaugeText.text = $"{Mathf.FloorToInt(skillGauge)} / {Mathf.CeilToInt(MaxSkillGauge)}";
        }
    }
}
