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

        /// <summary>
        /// 이 무공이 실제로 자랄 때 쓰는 성향.
        ///
        /// 무공에 성향이 있으면 그것을, **없으면(강호무학) 익힌 사람의 성향**을 쓴다
        /// (<see cref="MartialArt.IsAlignmentFree"/>). 그래서 같은 `절정검법` 이라도
        /// 정파 무인이 익히면 정파 곡선으로, 마도 무인이 익히면 마도 곡선으로 자란다.
        /// </summary>
        public Alignment EffectiveAlignment { get; }

        /// <param name="ownerAlignment">
        /// 익힌 사람의 성향. **성향 없는 무공(강호무학)에는 필수**이고, 성향이 박힌 무공에는 무시된다.
        /// </param>
        public LearnedArt(MartialArt art, int trainingSessions = 0, Alignment? ownerAlignment = null)
        {
            if (art == null) throw new ArgumentNullException(nameof(art));
            if (trainingSessions < 0) throw new ArgumentOutOfRangeException(nameof(trainingSessions));
            if (art.IsAlignmentFree && ownerAlignment == null)
            {
                throw new ArgumentException(
                    art.Name + " 은(는) 성향이 없는 무공(강호무학)이라 익힌 사람의 성향이 필요하다.",
                    nameof(ownerAlignment));
            }

            Art = art;
            TrainingSessions = trainingSessions;
            EffectiveAlignment = art.Alignment ?? ownerAlignment.Value;
        }

        /// <summary>현재 숙련도(0 ~ 성향별 상한).</summary>
        public int Proficiency => AlignmentCurve.ProficiencyFor(EffectiveAlignment, TrainingSessions);

        /// <summary>
        /// **현재 무공 경지(1~10성).** 숙련도를 사람이 쓰는 단위로 옮긴 것이다.
        /// ⚠ 캐릭터 레벨과 다른 축이다 — 무공 하나하나가 따로 쌓는다(<see cref="MartialStage"/>).
        /// </summary>
        public int Stage => MartialStage.StageOf(Proficiency);

        /// <summary>현재 숙련도가 만드는 위력 배율.</summary>
        public double PowerMultiplier => AlignmentCurve.PowerMultiplier(EffectiveAlignment, Proficiency);

        /// <summary>숙련이 이 성향의 상한에 도달했는가.</summary>
        public bool IsMastered => Proficiency >= AlignmentCurve.MaxProficiency(EffectiveAlignment);

        /// <summary>수련한다. 상한에 도달한 뒤에도 횟수는 계속 쌓이지만 숙련도는 오르지 않는다.</summary>
        public void Train(int sessions = 1)
        {
            if (sessions < 0) throw new ArgumentOutOfRangeException(nameof(sessions), "수련 횟수는 음수일 수 없다.");
            TrainingSessions += sessions;
        }

        public override string ToString()
        {
            return Art.Name + " (" + MartialStage.Describe(Stage) + " · 숙련 " + Proficiency + ")";
        }
    }
}
