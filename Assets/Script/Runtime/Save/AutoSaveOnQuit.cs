using System;
using UnityEngine;

namespace Aethoria.Save
{
    // 창을 닫거나 플레이 모드를 끌 때 마지막으로 한 번 저장한다. HUD 캔버스에 붙어 한 판 동안 유지된다.
    public class AutoSaveOnQuit : MonoBehaviour
    {
        private Action onQuit;

        public void Initialize(Action saveAction)
        {
            onQuit = saveAction;
        }

        private void OnApplicationQuit()
        {
            onQuit?.Invoke();
        }
    }
}
