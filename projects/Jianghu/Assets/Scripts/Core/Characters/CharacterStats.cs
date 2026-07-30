using System;

namespace Jianghu.Core.Characters
{
    /// <summary>
    /// 캐릭터의 기본 능력치. 무공이 아니라 몸 자체의 값이다.
    ///
    /// ⚠ 구상안의 레벨 100 · 가중치 배분은 프로토타입에서 뺐다.
    ///   검증할 가설이 "무공 조합이 전투 결과를 바꾸는가" 이므로,
    ///   레벨 성장이 섞이면 무엇 때문에 이겼는지 분리할 수 없게 된다.
    /// </summary>
    public sealed class CharacterStats
    {
        /// <summary>체력 — 0 이 되면 패배한다.</summary>
        public int MaxHealth { get; }

        /// <summary>기력 — 초식을 쓸 때 소모한다. 내공 무공이 최대치를 올린다.</summary>
        public int MaxQi { get; }

        /// <summary>공격력 — 모든 초식 피해에 더해지는 기본값.</summary>
        public int Attack { get; }

        /// <summary>방어력 — 매 타격에서 피해를 깎는다.</summary>
        public int Defense { get; }

        /// <summary>신법 — 회피와 선공의 기반. 경공 무공이 이를 보강한다.</summary>
        public int Agility { get; }

        public CharacterStats(int maxHealth, int maxQi, int attack, int defense, int agility)
        {
            if (maxHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maxHealth), "체력은 1 이상이어야 한다.");
            if (maxQi < 0) throw new ArgumentOutOfRangeException(nameof(maxQi));
            if (attack < 0) throw new ArgumentOutOfRangeException(nameof(attack));
            if (defense < 0) throw new ArgumentOutOfRangeException(nameof(defense));
            if (agility < 0) throw new ArgumentOutOfRangeException(nameof(agility));

            MaxHealth = maxHealth;
            MaxQi = maxQi;
            Attack = attack;
            Defense = defense;
            Agility = agility;
        }
    }
}
