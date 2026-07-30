using System;
using System.Collections.Generic;
using Jianghu.Core.Characters;
using Jianghu.Core.Martial;

namespace Jianghu.Core.Combat
{
    /// <summary>
    /// 전투에 나서는 한 사람의 **정의**(이름 · 능력치 · 익힌 무공).
    ///
    /// 전투 중에 변하는 값(현재 체력·기력)은 여기 없다 — <see cref="CombatResolver"/> 가
    /// 내부에서 따로 관리한다. 그래야 같은 Combatant 를 같은 시드로 몇 번을 붙여도
    /// 항상 같은 결과가 나온다. Phase 2 의 승률표를 뽑으려면 이 성질이 반드시 필요하다.
    /// </summary>
    public sealed class Combatant
    {
        private static readonly IReadOnlyList<DisciplineMastery> NoMastery = new DisciplineMastery[0];

        public string Name { get; }
        public CharacterStats Stats { get; }
        public IReadOnlyList<LearnedArt> Arts { get; }

        /// <summary>무기 유형별 숙달도(백일창·천일도·만일검). 비워두면 전부 미숙달로 취급한다.</summary>
        public IReadOnlyList<DisciplineMastery> Masteries { get; }

        public Combatant(
            string name, CharacterStats stats, IReadOnlyList<LearnedArt> arts,
            IReadOnlyList<DisciplineMastery> masteries = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("이름은 비어 있을 수 없다.", nameof(name));
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            if (arts == null) throw new ArgumentNullException(nameof(arts));

            Name = name;
            Stats = stats;
            Arts = arts;
            Masteries = masteries ?? NoMastery;
        }

        /// <summary>해당 무기 유형의 숙련도. 익힌 적이 없으면 0.</summary>
        public int MasteryOf(Discipline discipline)
        {
            for (int i = 0; i < Masteries.Count; i++)
            {
                if (Masteries[i].Discipline == discipline) return Masteries[i].Proficiency;
            }
            return 0;
        }

        /// <summary>내공 무공이 더해진 실제 최대 기력. 보조 효과에는 숙련 배율이 곱해진다.</summary>
        public int EffectiveMaxQi
        {
            get
            {
                double bonus = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];
                    if (learned.Art.Discipline == Discipline.InnerArt)
                    {
                        bonus += learned.Art.MaxQiBonus * learned.PowerMultiplier;
                    }
                }
                return Stats.MaxQi + (int)Math.Round(bonus);
            }
        }

        /// <summary>내공 무공이 주는 초식 위력 보너스(%). 모든 초식에 곱연산으로 적용된다.</summary>
        public int PowerBonusPercent
        {
            get
            {
                double bonus = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];
                    if (learned.Art.Discipline == Discipline.InnerArt)
                    {
                        bonus += learned.Art.PowerBonusPercent * learned.PowerMultiplier;
                    }
                }
                return (int)Math.Round(bonus);
            }
        }

        /// <summary>회피 수치. 신법 절반이 기반이고 경공 무공이 얹힌다.</summary>
        public int Evasion
        {
            get
            {
                double bonus = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];
                    if (learned.Art.Discipline == Discipline.Movement)
                    {
                        bonus += learned.Art.EvasionBonus * learned.PowerMultiplier;
                    }
                }
                return Stats.Agility / 2 + (int)Math.Round(bonus);
            }
        }

        /// <summary>
        /// 선공 판정 수치. 높은 쪽이 먼저 친다.
        /// 경공 무공 + **창 숙달**(백일창 — 먼저 찌른다)이 얹힌다.
        ///
        /// ⚠ 프로토타입 단순화: 창 숙달이 있으면 다른 무기를 쓸 때도 선공 보너스가 붙는다.
        ///   선공은 전투 시작 전에 정해지는데 그 시점엔 어느 초식을 쓸지 아직 모르기 때문이다.
        ///   실제로는 한 사람이 무기 하나를 주력으로 쓰므로 당장은 문제가 안 된다.
        /// </summary>
        public int Initiative
        {
            get
            {
                double bonus = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];
                    if (learned.Art.Discipline == Discipline.Movement)
                    {
                        bonus += learned.Art.InitiativeBonus * learned.PowerMultiplier;
                    }
                }

                int spearBonus = DisciplineCurve.InitiativeBonus(Discipline.Spear, MasteryOf(Discipline.Spear));
                return Stats.Agility + (int)Math.Round(bonus) + spearBonus;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
