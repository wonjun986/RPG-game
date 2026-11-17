using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aethoria.Characters
{
    // 네크로시아가 익힌 스킬(A/S/D/Q/W/E/R). 새 게임은 스킬 없이 시작한다. A는 튜토리얼 숲에서 정령을 마무리하는 순간
    // 몸이 기억해 내며 깨어나고(ForestTutorial), 나머지는 레벨이 차면 마을의 이시스에게 배운다(IsisNpc).
    // 기본 공격(Z)과 대시(Shift)는 처음부터 쓸 수 있다. 각 스킬은 시전 전에 Allows로 익혔는지 확인한다.
    public class SkillBook : MonoBehaviour
    {
        public readonly struct SkillInfo
        {
            public readonly string Key;
            public readonly string Name;
            public readonly int RequiredLevel;
            public SkillInfo(string key, string name, int requiredLevel) { Key = key; Name = name; RequiredLevel = requiredLevel; }
        }

        // 배우는 순서(이시스는 이 순서대로 가르친다). A는 튜토리얼에서 깨어나지만, 혹시 못 익힌 채면 이시스가 대신 가르친다.
        public static readonly SkillInfo[] Skills =
        {
            new SkillInfo("A", "낫 회전 베기", 1),
            new SkillInfo("S", "어둠의 주먹", 3),
            new SkillInfo("D", "낫 부메랑", 5),
            new SkillInfo("Q", "차원 참격", 7),
            new SkillInfo("W", "악마의 손", 10),
            new SkillInfo("E", "사슬 폭풍", 13),
            new SkillInfo("R", "죽음의 소용돌이", 16),
        };

        private readonly HashSet<string> learned = new();

        public event Action<string> OnLearned;

        public bool IsLearned(string key) => learned.Contains(key);

        // 스킬 컴포넌트가 시전 전에 부른다. 스킬 책이 없는 캐릭터(연출용 등)는 막지 않는다.
        public static bool Allows(Component user, string key)
        {
            var book = user.GetComponent<SkillBook>();
            return book == null || book.IsLearned(key);
        }

        public bool Learn(string key)
        {
            if (!learned.Add(key)) return false;
            OnLearned?.Invoke(key);
            return true;
        }

        public void LearnAll()
        {
            foreach (var skill in Skills) learned.Add(skill.Key);
        }

        public void Restore(IEnumerable<string> keys)
        {
            learned.Clear();
            if (keys == null) return;
            foreach (var key in keys) learned.Add(key);
        }

        public List<string> ToSaveEntries() => new List<string>(learned);

        // 지금 레벨로 배울 수 있는데 아직 안 배운 스킬들(배우는 순서대로).
        public List<SkillInfo> Learnable(int level)
        {
            var result = new List<SkillInfo>();
            foreach (var skill in Skills)
            {
                if (!IsLearned(skill.Key) && level >= skill.RequiredLevel) result.Add(skill);
            }
            return result;
        }

        // 아직 레벨이 모자라 못 배우는 다음 스킬. 다 배울 수 있으면 false.
        public bool TryGetNextLocked(int level, out SkillInfo next)
        {
            foreach (var skill in Skills)
            {
                if (!IsLearned(skill.Key) && level < skill.RequiredLevel)
                {
                    next = skill;
                    return true;
                }
            }
            next = default;
            return false;
        }

        public static int RequiredLevel(string key)
        {
            foreach (var skill in Skills)
            {
                if (skill.Key == key) return skill.RequiredLevel;
            }
            return 1;
        }
    }
}
