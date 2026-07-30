using System;

namespace Jianghu.Core.Martial
{
    /// <summary>
    /// 무기 유형별 숙달 곡선 — **백일창(百日槍) 천일도(千日刀) 만일검(萬日劍)**.
    ///
    /// 무협 관용구 그대로다. 창은 백일이면 쓸 만해지고, 도는 천일, 검은 만일이 걸린다.
    /// 실제 비율 1:10:100 은 게임에 그대로 쓰기엔 너무 극단적이라 압축했다.
    ///
    /// ── ⚠⚠ 성향 곡선과 반드시 다른 것을 만져야 한다 ─────────────────────────
    /// **유형 숙련은 위력에 관여하지 않는다.** 이건 취향이 아니라 측정 결과다.
    ///
    /// 2026-07-28 승률표(Tools/Sandbox)에서, 성향 축 하나만으로도 수련 250회 시점에
    /// 상위 3개가 전부 마도가 되어 **유형 선택이 무의미해졌다**(검·마 68%, 권·마 65%, 도·마 64%).
    /// 여기에 유형 숙련까지 위력에 곱하면 "가장 늦게 숙달되는 무기 × 가장 늦게 강해지는 성향"이
    /// 후반을 독식한다 — 반증 조건 2(지배 전략)에 정면으로 걸린다.
    ///
    /// 그래서 역할을 갈랐다:
    ///   · <see cref="AlignmentCurve"/> (성향) = **얼마나 강한가** — 위력 배율
    ///   · 이 클래스 (유형)               = **어떻게 싸우는가** — 명중 · 선공 · 기력 · 방어 관통
    /// 두 축이 서로 다른 차원을 만지므로 곱연산 폭주가 없다.
    /// ────────────────────────────────────────────────────────────────────
    /// </summary>
    public static class DisciplineCurve
    {
        public const int MaxProficiency = 100;

        // 숙달 속도. 창 > 권 > 비도 > 도 > 검 순으로 빠르다.
        private const double SpearLearningRate = 2.00;   // 백일창 — 압도적으로 빠르다
        private const double FistLearningRate = 1.20;
        private const double DaggerLearningRate = 1.00;  // 비도 — 중간
        private const double BladeLearningRate = 0.70;   // 천일도
        private const double SwordLearningRate = 0.40;   // 만일검 — 가장 오래 걸린다

        // 만숙(숙련 100) 시 발현되는 특성. 유형마다 **딱 하나씩**이다 — 겹치면 정체성이 흐려진다.
        private const int SpearMaxInitiative = 25;        // 창 — 먼저 찌른다
        private const int FistMaxQiReductionPercent = 50; // 권 — 지치지 않는다
        private const int DaggerMaxStatusChance = 30;     // 비도 — 암기에 독을 바른다
        private const int BladeMaxPenetrationPercent = 70; // 도 — 단단한 상대를 가른다
        private const int SwordMaxAccuracy = 25;          // 검 — 빈틈이 없다

        /// <summary>수련 1회당 오르는 유형 숙련도.</summary>
        public static double LearningRate(Discipline discipline)
        {
            switch (discipline)
            {
                case Discipline.Spear: return SpearLearningRate;
                case Discipline.Fist: return FistLearningRate;
                case Discipline.Dagger: return DaggerLearningRate;
                case Discipline.Blade: return BladeLearningRate;
                case Discipline.Sword: return SwordLearningRate;
                case Discipline.InnerArt:
                case Discipline.Movement:
                    // 보조 무공은 무기 숙달 개념이 없다. 자체 숙련(LearnedArt)으로만 성장한다.
                    return 0.0;
                default: throw new ArgumentOutOfRangeException(nameof(discipline));
            }
        }

        /// <summary>수련 횟수로부터 유형 숙련도를 구한다(0 ~ 100).</summary>
        public static int ProficiencyFor(Discipline discipline, int trainingSessions)
        {
            if (trainingSessions < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(trainingSessions), "수련 횟수는 음수일 수 없다.");
            }

            int raw = (int)(trainingSessions * LearningRate(discipline));
            return raw > MaxProficiency ? MaxProficiency : raw;
        }

        /// <summary>만숙에 필요한 수련 횟수. 백일창 : 천일도 : 만일검의 압축판이다.</summary>
        public static int SessionsToMaster(Discipline discipline)
        {
            double rate = LearningRate(discipline);
            if (rate <= 0) return 0;
            return (int)Math.Ceiling(MaxProficiency / rate);
        }

        /// <summary>창 — 선공 보너스. 숙련에 비례해 발현된다.</summary>
        public static int InitiativeBonus(Discipline discipline, int proficiency)
        {
            return discipline == Discipline.Spear ? Scale(SpearMaxInitiative, proficiency) : 0;
        }

        /// <summary>검 — 명중 보너스(%p).</summary>
        public static int AccuracyBonus(Discipline discipline, int proficiency)
        {
            return discipline == Discipline.Sword ? Scale(SwordMaxAccuracy, proficiency) : 0;
        }

        /// <summary>도 — 상대 방어력을 무시하는 비율(%).</summary>
        public static int DefensePenetrationPercent(Discipline discipline, int proficiency)
        {
            return discipline == Discipline.Blade ? Scale(BladeMaxPenetrationPercent, proficiency) : 0;
        }

        /// <summary>권 — 기력 소모 감소율(%).</summary>
        public static int QiCostReductionPercent(Discipline discipline, int proficiency)
        {
            return discipline == Discipline.Fist ? Scale(FistMaxQiReductionPercent, proficiency) : 0;
        }

        /// <summary>
        /// 비도 — 상태이상 부여 확률 보너스(%p).
        ///
        /// 비도를 쓰는 문파가 사천당가(독)·살문(암살)·장강수로채(수적)로 **전부 독·암살 계열**이라
        /// 이 특성을 배정했다. 암기에 독을 바른다는 것이 무협 관례이기도 하다.
        /// ⚠ 상태이상 시스템과 직결되므로 강해지기 쉽다. 비도 무공의 위력을 낮게 잡아 상쇄한다.
        /// </summary>
        public static int StatusChanceBonus(Discipline discipline, int proficiency)
        {
            return discipline == Discipline.Dagger ? Scale(DaggerMaxStatusChance, proficiency) : 0;
        }

        private static int Scale(int maxValue, int proficiency)
        {
            if (proficiency < 0) throw new ArgumentOutOfRangeException(nameof(proficiency));
            int p = proficiency > MaxProficiency ? MaxProficiency : proficiency;
            return (int)Math.Round(maxValue * p / 100.0, MidpointRounding.AwayFromZero);
        }
    }
}
