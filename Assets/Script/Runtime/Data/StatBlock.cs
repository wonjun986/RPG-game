using System;

namespace Aethoria.Data
{
    [Serializable]
    public struct StatBlock
    {
        public float attack;
        public float magic;
        public float hp;
        public float agility;
        public float defense;
        public float mana;

        public static StatBlock operator +(StatBlock a, StatBlock b)
        {
            return new StatBlock
            {
                attack = a.attack + b.attack,
                magic = a.magic + b.magic,
                hp = a.hp + b.hp,
                agility = a.agility + b.agility,
                defense = a.defense + b.defense,
                mana = a.mana + b.mana,
            };
        }

        public static StatBlock operator *(StatBlock stat, float multiplier)
        {
            return new StatBlock
            {
                attack = stat.attack * multiplier,
                magic = stat.magic * multiplier,
                hp = stat.hp * multiplier,
                agility = stat.agility * multiplier,
                defense = stat.defense * multiplier,
                mana = stat.mana * multiplier,
            };
        }
    }
}
