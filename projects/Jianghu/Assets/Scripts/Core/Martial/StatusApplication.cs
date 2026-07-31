using System;

namespace Jianghu.Core.Martial
{
    /// <summary>
    /// "이 무공이 어떤 상태이상을 얼마나 걸 수 있는가" 라는 정의.
    /// 무공 데이터에 붙는 값이며, 전투 중에 실제로 걸린 상태는 <see cref="CombatResolver"/> 가 따로 관리한다.
    /// </summary>
    public sealed class StatusApplication
    {
        public StatusEffectKind Kind { get; }

        /// <summary>초식이 명중했을 때 발동할 확률(%).</summary>
        public int ChancePercent { get; }

        /// <summary>세기. 출혈·중독은 턴당 피해, 기력소실은 턴당 기력 감소, 경직은 명중 감소량이다.</summary>
        public int Potency { get; }

        /// <summary>지속 턴 수. 같은 상태이상이 다시 걸리면 이 값으로 갱신된다.</summary>
        public int DurationTurns { get; }

        public StatusApplication(StatusEffectKind kind, int chancePercent, int potency, int durationTurns)
        {
            if (chancePercent < 0 || chancePercent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(chancePercent), "확률은 0~100 이어야 한다.");
            }
            if (potency < 0) throw new ArgumentOutOfRangeException(nameof(potency));
            if (durationTurns < 1) throw new ArgumentOutOfRangeException(nameof(durationTurns), "지속 턴은 1 이상이어야 한다.");

            Kind = kind;
            ChancePercent = chancePercent;
            Potency = potency;
            DurationTurns = durationTurns;
        }

        /// <summary>사람이 읽는 이름. 전투 로그에 그대로 쓴다.</summary>
        public static string NameOf(StatusEffectKind kind)
        {
            switch (kind)
            {
                case StatusEffectKind.Bleed: return "출혈";
                case StatusEffectKind.Poison: return "중독";
                case StatusEffectKind.QiDrain: return "기력소실";
                case StatusEffectKind.Paralysis: return "마비";
                case StatusEffectKind.Stagger: return "경직";
                case StatusEffectKind.Burn: return "화상";
                case StatusEffectKind.Frostbite: return "동상";
                default: return "?";
            }
        }

        public override string ToString()
        {
            return NameOf(Kind) + " " + ChancePercent + "%";
        }
    }
}
