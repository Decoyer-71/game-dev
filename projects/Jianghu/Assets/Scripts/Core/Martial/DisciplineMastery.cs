using System;

namespace Jianghu.Core.Martial
{
    /// <summary>
    /// 한 사람이 특정 무기 유형을 얼마나 다룰 줄 아는가.
    ///
    /// ⚠ <see cref="LearnedArt"/>(무공 하나의 숙련)와 **다른 개념이다.**
    ///   · LearnedArt   = "이 초식을 얼마나 익혔는가" → 위력 (성향 곡선)
    ///   · DisciplineMastery = "이 무기를 얼마나 다루는가" → 명중·선공·기력·방어관통 (유형 곡선)
    ///
    /// 무기 숙달은 무공에 딸린 게 아니라 **사람에게 딸린다.** 검을 오래 다룬 사람은
    /// 새 검법을 배워도 손이 안 흔들린다 — 그게 만일검(萬日劍)의 의미다.
    /// 그래서 이 값은 Combatant 가 갖고, 그 사람의 모든 해당 유형 초식에 적용된다.
    /// </summary>
    public sealed class DisciplineMastery
    {
        public Discipline Discipline { get; }

        /// <summary>이 무기를 다룬 수련 횟수.</summary>
        public int TrainingSessions { get; private set; }

        public DisciplineMastery(Discipline discipline, int trainingSessions = 0)
        {
            if (discipline.IsSupport())
            {
                throw new ArgumentException("내공·경공은 무기가 아니므로 유형 숙달 대상이 아니다.", nameof(discipline));
            }
            if (trainingSessions < 0) throw new ArgumentOutOfRangeException(nameof(trainingSessions));

            Discipline = discipline;
            TrainingSessions = trainingSessions;
        }

        /// <summary>현재 유형 숙련도(0 ~ 100).</summary>
        public int Proficiency => DisciplineCurve.ProficiencyFor(Discipline, TrainingSessions);

        public bool IsMastered => Proficiency >= DisciplineCurve.MaxProficiency;

        public void Train(int sessions = 1)
        {
            if (sessions < 0) throw new ArgumentOutOfRangeException(nameof(sessions), "수련 횟수는 음수일 수 없다.");
            TrainingSessions += sessions;
        }

        public override string ToString()
        {
            return Discipline + " 숙달 " + Proficiency;
        }
    }
}
