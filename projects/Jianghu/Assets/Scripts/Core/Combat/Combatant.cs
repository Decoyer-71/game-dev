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
                    if (learned.Art.Discipline != Discipline.InnerArt) continue;

                    // ⚠ 형태소 무공은 `Delta.MaxQi`(양 +10 · 합 +5 · 식 +3 · 선 +20)에서,
                    //   레거시 무공은 손으로 박은 `MaxQiBonus` 에서 읽는다. 과도기 분기다.
                    double raw = learned.Art.IsMorphemeDerived ? learned.Art.Delta.MaxQi : learned.Art.MaxQiBonus;
                    bonus += raw * learned.PowerMultiplier;
                }
                return Stats.MaxQi + (int)Math.Round(bonus);
            }
        }

        /// <summary>
        /// 정의서 §1-1 의 기본 기력회복속도. ⚠ 미검증 초기값이다.
        /// `CharacterStats` 에 회복 속도 필드가 없어 상수로 둔다 — 필드로 올릴지는 측정 후 판단한다.
        /// </summary>
        public const int BaseQiRegen = 2;

        /// <summary>
        /// 턴당 기력 회복량. 기본값에 내공 무공의 형태소 회복(음 +2 · 합 +0.5 · 수 +1 · 선 +3)이 더해진다.
        ///
        /// ⚠⚠ **이 값이 전투 성립 여부를 가른다.** 0 이면 4자 무공 기준 3턴 만에 기력이 마르고
        ///   평타(피해 1)로 전락해 전투가 끝나지 않는다(2026-07-30 실측: 전원 무승부).
        ///   설계안 §5-3 의 **평타 전락률**(목표 10~30%)이 이 값의 적정성을 판정한다.
        /// </summary>
        public int QiRegenPerTurn
        {
            get
            {
                double bonus = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];
                    if (!learned.Art.IsMorphemeDerived) continue;

                    // ⚠ 숙련 배율을 곱한다 — `EffectiveMaxQi` 와 같은 처리다(설계안 §1-C).
                    //   ⚠⚠ 단 **체력 회복은 곱하지 않기로 했다**(2026-07-30). 오래 버티는 축이
                    //   수련으로 증폭되면 후반이 교착되기 때문이다. 기력과 체력을 갈라 다룬다.
                    bonus += learned.Art.Delta.QiRegen * learned.PowerMultiplier;
                }
                return BaseQiRegen + (int)Math.Round(bonus);
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
