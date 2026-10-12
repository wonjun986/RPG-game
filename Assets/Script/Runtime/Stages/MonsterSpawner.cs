using System.Collections.Generic;
using UnityEngine;

namespace Aethoria.Stages
{
    // 맵에 들어올 때마다 일반 몬스터가 나올 x 위치를 무작위로 고른다(GameBootstrap이 그 자리에 몬스터를 만든다).
    // - 맵 양 끝(포탈 자리)에서 edgeMargin만큼 안쪽에서만 고른다.
    // - 플레이어가 막 들어온 자리 근처(playerClearance)에는 놓지 않는다 — 들어오자마자 전투가 걸리지 않게.
    // - 몬스터끼리는 minSpacing 이상 떨어뜨린다(한 자리에 뭉쳐 연달아 전투가 걸리지 않게).
    // 조건을 만족하는 자리를 못 찾으면 그만큼 적게 돌려준다(좁은 맵에서 무한히 돌지 않게).
    public static class MonsterSpawner
    {
        private const int AttemptsPerMonster = 40;

        public static List<float> PickSpawnXs(int count, float mapXMin, float mapXMax, float playerX,
            float edgeMargin = 3f, float playerClearance = 5f, float minSpacing = 2.5f)
        {
            var picked = new List<float>(count);
            float rangeMin = mapXMin + edgeMargin;
            float rangeMax = mapXMax - edgeMargin;
            if (rangeMax <= rangeMin) return picked;

            int attempts = count * AttemptsPerMonster;
            while (picked.Count < count && attempts-- > 0)
            {
                float x = Random.Range(rangeMin, rangeMax);
                if (Mathf.Abs(x - playerX) < playerClearance) continue;

                bool tooClose = false;
                foreach (var other in picked)
                {
                    if (Mathf.Abs(x - other) < minSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (!tooClose) picked.Add(x);
            }

            picked.Sort();
            return picked;
        }
    }
}
