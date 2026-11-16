using UnityEngine;

namespace Aethoria.Stages
{
    // 마을 NPC(노아, 엘리)의 대기 모션: 숨쉬기 그림(Breath, 첫 장에서 허리 위만 조금씩 들어 올린 5장)을 1→5→1로 오가며
    // 숨을 쉬고, 숨을 다 내쉰 순간에 가끔 눈 깜빡임/윙크(Idle 시트 전체)를 한 번 넣는다. 깜빡임 시트는 숨을 내쉰 자세라
    // 한 바퀴가 끝날 때만 넣어야 몸이 튀지 않는다. 숨쉬기 그림이 없으면 첫 장으로 서 있다가 가끔 깜빡이기만 한다.
    public class NpcIdleAnimation
    {
        // 숨 한 번: 들이쉬며 1→5장, 꼭대기에서 잠깐 머물고, 내쉬며 5→1장, 바닥에서 잠깐 쉰다.
        private static readonly int[] BreathSequence = { 0, 1, 2, 3, 4, 4, 3, 2, 1, 0 };

        private readonly SpriteRenderer renderer;
        private readonly Sprite[] blinkFrames;
        private readonly Sprite[] breathFrames;

        public float BreathPeriod = 3.2f;   // 한 번 들이쉬고 내쉬는 데 걸리는 시간(초)
        public float BlinkFrameTime = 0.12f; // 깜빡임 한 장 시간
        public float BlinkInterval = 4f;     // 깜빡임 사이 평균 간격(초). 매번 0.7~1.4배로 흔든다

        private bool blinking;
        private int index;
        private float timer;
        private float blinkTimer;

        public NpcIdleAnimation(SpriteRenderer renderer, string blinkPath, string breathPath)
        {
            this.renderer = renderer;
            blinkFrames = LoadSorted(blinkPath);
            breathFrames = LoadSorted(breathPath);
            blinkTimer = BlinkInterval * Random.Range(0.4f, 1f); // 여러 NPC가 같은 순간에 깜빡이지 않게
            if (breathFrames.Length > 0) renderer.sprite = breathFrames[0];
            else if (blinkFrames.Length > 0) renderer.sprite = blinkFrames[0];
        }

        private static Sprite[] LoadSorted(string path)
        {
            if (string.IsNullOrEmpty(path)) return new Sprite[0];
            var frames = Resources.LoadAll<Sprite>(path) ?? new Sprite[0];
            System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            return frames;
        }

        public void Tick(float deltaTime)
        {
            if (blinkFrames.Length == 0 && breathFrames.Length == 0) return;
            blinkTimer -= deltaTime;
            timer += deltaTime;

            if (blinking)
            {
                if (timer < BlinkFrameTime) return;
                timer -= BlinkFrameTime;
                index++;
                if (index < blinkFrames.Length)
                {
                    renderer.sprite = blinkFrames[index];
                    return;
                }
                // 깜빡임이 끝나면 숨을 다 내쉰 자리에서 다시 숨쉬기를 이어 간다.
                blinking = false;
                index = 0;
                blinkTimer = BlinkInterval * Random.Range(0.7f, 1.4f);
                ShowBreathFrame();
                return;
            }

            if (breathFrames.Length == 0)
            {
                if (blinkTimer <= 0f) StartBlink();
                return;
            }

            float step = BreathPeriod / BreathSequence.Length;
            if (timer < step) return;
            timer -= step;
            index = (index + 1) % BreathSequence.Length;
            if (index == 0 && blinkTimer <= 0f && blinkFrames.Length > 0)
            {
                StartBlink();
                return;
            }
            ShowBreathFrame();
        }

        private void StartBlink()
        {
            blinking = true;
            timer = 0f;
            index = 0;
            renderer.sprite = blinkFrames[0];
        }

        private void ShowBreathFrame()
        {
            if (breathFrames.Length == 0)
            {
                if (blinkFrames.Length > 0) renderer.sprite = blinkFrames[0];
                return;
            }
            renderer.sprite = breathFrames[Mathf.Min(BreathSequence[index], breathFrames.Length - 1)];
        }
    }
}
