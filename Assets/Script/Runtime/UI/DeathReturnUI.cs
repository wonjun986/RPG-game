using System.Collections;
using UnityEngine;
using Aethoria.Bootstrap;
using Aethoria.Characters;

namespace Aethoria.UI
{
    // 플레이어가 쓰러지면 죽음 연출(DeathCinematic)을 보여준 뒤 고르게 한다. 어느 쪽이든 체력/마나를 채운다.
    //   Z/Enter = 재도전: 쓰러진 그 맵을 처음부터 다시 짓는다(보스 맵이면 보스전부터 다시).
    //   X = 마을로 돌아가기.
    // 레벨/경험치/퀘스트 진행은 그대로 유지된다(패널티 없음).
    public class DeathReturnUI : MonoBehaviour
    {
        private Character target;
        private CharacterMovement2D movement;
        private DeathCinematic cinematic;
        private Coroutine routine;

        public void Initialize(Character character)
        {
            target = character;
            movement = character.GetComponent<CharacterMovement2D>();
            cinematic = DeathCinematic.Create(transform, "[Z] 재도전      [X] 마을로 돌아가기");
            target.OnDied += HandlePlayerDied;
        }

        private void OnDestroy()
        {
            if (target != null) target.OnDied -= HandlePlayerDied;
        }

        private void HandlePlayerDied(Character character)
        {
            if (routine != null) return;
            routine = StartCoroutine(ReturnRoutine());
        }

        private IEnumerator ReturnRoutine()
        {
            if (movement != null) movement.SetInputLocked(true);
            var body = target.GetComponent<Rigidbody2D>();
            if (body != null) body.linearVelocity = Vector2.zero;

            cinematic.Show();

            // 리듬 전투 중에 쓰러졌다면 같은 OnDied를 받은 RhythmBattleUI가 창을 닫으면서 timeScale과
            // 입력 잠금을 되돌리므로, 한 프레임 뒤에 다시 잠그고 연출 동안 월드를 멈춘다.
            yield return null;
            if (movement != null) movement.SetInputLocked(true);
            Time.timeScale = 0f;

            yield return cinematic.Play(allowAlternate: true);

            Time.timeScale = 1f;
            if (cinematic.AlternateChosen) GameBootstrap.ReturnToVillage(); // 체력/마나 회복 + 마을 재구성 + 자동 저장
            else GameBootstrap.RetryCurrentStage(); // 체력/마나 회복 + 쓰러진 맵을 처음부터 다시

            cinematic.Hide();
            if (movement != null) movement.SetInputLocked(false);
            routine = null;
        }
    }
}
