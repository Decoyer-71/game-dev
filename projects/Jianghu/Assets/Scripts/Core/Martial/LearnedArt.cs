using System;

namespace Jianghu.Core.Martial
{
    /// <summary>
    /// 특정 캐릭터가 익힌 무공 하나. 무공 정의(<see cref="MartialArt"/>)와 그 캐릭터의 숙련도를 묶는다.
    ///
    /// 숙련도를 직접 저장하지 않고 **수련 횟수만 저장한 뒤 매번 유도**한다.
    /// 이유는 결정론이다 — 실수를 누적하면 수련 순서에 따라 미세 오차가 갈리고,
    /// 그러면 같은 시드로 같은 전투 결과가 나온다는 보장이 깨진다(docs/jianghu-design.md §6).
    /// </summary>
    public sealed class LearnedArt
    {
        public MartialArt Art { get; }

        /// <summary>지금까지 수련한 횟수. 숙련도의 유일한 원천이다.</summary>
        public int TrainingSessions { get; private set; }

        public LearnedArt(MartialArt art, int trainingSessions = 0)
        {
            if (art == null) throw new ArgumentNullException(nameof(art));
            if (trainingSessions < 0) throw new ArgumentOutOfRangeException(nameof(trainingSessions));

            Art = art;
            TrainingSessions = trainingSessions;
        }

        /// <summary>현재 숙련도(0 ~ 성향별 상한).</summary>
        public int Proficiency => AlignmentCurve.ProficiencyFor(Art.Alignment, TrainingSessions);

        /// <summary>현재 숙련도가 만드는 위력 배율.</summary>
        public double PowerMultiplier => AlignmentCurve.PowerMultiplier(Art.Alignment, Proficiency);

        /// <summary>숙련이 이 성향의 상한에 도달했는가.</summary>
        public bool IsMastered => Proficiency >= AlignmentCurve.MaxProficiency(Art.Alignment);

        /// <summary>수련한다. 상한에 도달한 뒤에도 횟수는 계속 쌓이지만 숙련도는 오르지 않는다.</summary>
        public void Train(int sessions = 1)
        {
            if (sessions < 0) throw new ArgumentOutOfRangeException(nameof(sessions), "수련 횟수는 음수일 수 없다.");
            TrainingSessions += sessions;
        }

        public override string ToString()
        {
            return Art.Name + " (숙련 " + Proficiency + ")";
        }
    }
}
