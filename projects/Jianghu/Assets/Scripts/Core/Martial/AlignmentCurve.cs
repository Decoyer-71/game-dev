using System;

namespace Jianghu.Core.Martial
{
    /// <summary>
    /// 성향별 성장곡선. **이 프로토타입의 심장이다.**
    ///
    /// 여기서 재미가 안 나오면 나머지는 의미가 없다 — 검증할 가설이
    /// "익힌 무공의 조합(유형 × 성향)이 전투 결과를 눈에 보이게 바꾸는가" 이기 때문이다.
    /// 설계 근거: docs/jianghu-design.md §3.
    ///
    /// 숙련도를 실수로 누적하지 않고 **수련 횟수에서 매번 유도**한다.
    /// 실수 누적은 순서에 따라 오차가 달라져 결정론이 깨지고, 그러면 전투 테스트가 성립하지 않는다.
    ///
    /// ── ⚠⚠ 2026-07-28 재설계 ──────────────────────────────────────────────
    /// 초판은 사파에 **하드 상한 70**을 걸었다. 그런데 사파는 44회 수련이면 상한에 닿아서,
    /// 그 뒤로는 성장이 완전히 멈춘 죽은 선택지가 됐다. "고점이 낮다"가 아니라
    /// "고르는 순간 함정"이었다 — 河洛群侠传의 검법 계열이 실제로 이렇게 죽었다
    /// (docs/concepts/wuxia-grandmaster-rpg.md §2-2).
    ///
    /// 두 가지를 바꿨다:
    ///  ① 하드 상한을 **소프트 상한**으로. 사파도 100까지 가되 70 이후 학습 효율이 1/5 로 급락한다
    ///  ② 사파의 정체성을 최대치가 아니라 **편차**로 옮겼다 (<see cref="DamageVariancePercent"/>).
    ///     사파는 피해 변동폭이 가장 좁다 = **보장된 저점**. 기대값은 낮아도 계산이 선다.
    ///     이러면 "확실한 승리가 필요한가, 크게 이겨야 하는가" 라는 진짜 결정이 생긴다.
    /// ─────────────────────────────────────────────────────────────────────
    ///
    /// ⚠ 아래 상수는 전부 미검증 초기값이다. 테스트로 확인하는 것은 "절대 수치"가 아니라
    ///   **의도한 역전 순서와 편차 순서**뿐이다. 실제 밸런스는 플레이로만 잡힌다.
    /// </summary>
    public static class AlignmentCurve
    {
        /// <summary>모든 성향이 도달할 수 있는 절대 상한. 성향 차이는 여기 오는 '비용'에서 난다.</summary>
        public const int HardCap = 100;

        // 수련 1회당 오르는 숙련도(소프트 상한 이전). 정파는 느리고, 사파는 빠르고, 마도가 가장 느리다.
        private const double OrthodoxLearningRate = 0.70;
        private const double UnorthodoxLearningRate = 1.60;
        private const double DemonicLearningRate = 0.45;

        // 소프트 상한 — 이 지점을 넘으면 학습 효율이 LateLearningPenalty 배만큼 나빠진다.
        private const int OrthodoxSoftCap = 100;   // 사실상 없음
        private const int UnorthodoxSoftCap = 70;  // ⚠ 사파만 실질적으로 걸린다
        private const int DemonicSoftCap = 100;    // 사실상 없음

        // 소프트 상한 이후 학습 효율 저하 배수. 5.0 이면 같은 숙련 1 을 올리는 데 5배가 든다.
        private const double UnorthodoxLatePenalty = 5.0;

        // 위력 배율 계수.
        // ⚠⚠ 2026-07-28 2차 조정 — 만렙 위력을 서로 가깝게 좁혔다(2.00 / 2.15 / 2.24).
        //   1차 설계는 마도 3.00 vs 사파 1.95 로 **위력 54% 차이**였는데, 승률표를 뽑아 보니
        //   그 격차가 유형 숙달 특성(명중 +25 ≈ 피해 +16%, 관통 70% ≈ 피해 +25%)을 압도해서
        //   후반 상위 4개가 전부 마도가 됐다(격차 65%p). 성향이 유형 선택을 통째로 잡아먹은 것이다.
        //   → 성향은 **위력 축에서 물러난다.** 남는 차이는 ① 도달 시점 ② 편차 두 가지뿐이다.
        private const double OrthodoxPowerSpan = 1.15;   // 선형: 1.0 → 2.15
        private const double UnorthodoxPowerSpan = 1.00; // 제곱근: 초반 급상승, 만렙 2.00
        private const double DemonicStepGain = 0.31;     // 계단: 25 숙련마다 +0.31 → 만렙 2.24
        private const int DemonicStepSize = 25;

        // 피해 변동폭(%). 사파의 진짜 강점이 여기 있다.
        private const int OrthodoxVariance = 15;    // ±15% — 표준
        private const int UnorthodoxVariance = 5;   // ±5%  — 보장된 저점. 계산이 서는 무공
        private const int DemonicVariance = 35;     // ±35% — 도박

        /// <summary>도달 가능한 숙련도 상한. 이제 모든 성향이 같다 — 차이는 '거기 오는 비용'이다.</summary>
        public static int MaxProficiency(Alignment alignment)
        {
            EnsureKnown(alignment);
            return HardCap;
        }

        /// <summary>이 지점을 넘으면 학습 효율이 나빠진다.</summary>
        public static int SoftCap(Alignment alignment)
        {
            switch (alignment)
            {
                case Alignment.Orthodox: return OrthodoxSoftCap;
                case Alignment.Unorthodox: return UnorthodoxSoftCap;
                case Alignment.Demonic: return DemonicSoftCap;
                default: throw new ArgumentOutOfRangeException(nameof(alignment));
            }
        }

        /// <summary>소프트 상한 이후 학습에 드는 배수. 1.0 이면 저하 없음.</summary>
        public static double LateLearningPenalty(Alignment alignment)
        {
            switch (alignment)
            {
                case Alignment.Unorthodox: return UnorthodoxLatePenalty;
                case Alignment.Orthodox:
                case Alignment.Demonic: return 1.0;
                default: throw new ArgumentOutOfRangeException(nameof(alignment));
            }
        }

        /// <summary>수련 1회당 오르는 숙련도(소프트 상한 이전).</summary>
        public static double LearningRate(Alignment alignment)
        {
            switch (alignment)
            {
                case Alignment.Orthodox: return OrthodoxLearningRate;
                case Alignment.Unorthodox: return UnorthodoxLearningRate;
                case Alignment.Demonic: return DemonicLearningRate;
                default: throw new ArgumentOutOfRangeException(nameof(alignment));
            }
        }

        /// <summary>
        /// 피해 변동폭(%). 실제 피해는 (100 - v) ~ (100 + v) % 사이에서 정해진다.
        ///
        /// **사파의 존재 이유가 이 값이다.** 사파는 편차가 가장 좁아 최저 피해가 보장된다.
        /// 반대로 마도는 편차가 가장 넓어, 같은 기대값이어도 결과가 크게 흔들린다.
        /// </summary>
        public static int DamageVariancePercent(Alignment alignment)
        {
            switch (alignment)
            {
                case Alignment.Orthodox: return OrthodoxVariance;
                case Alignment.Unorthodox: return UnorthodoxVariance;
                case Alignment.Demonic: return DemonicVariance;
                default: throw new ArgumentOutOfRangeException(nameof(alignment));
            }
        }

        /// <summary>
        /// 수련 횟수로부터 현재 숙련도를 구한다.
        /// 소프트 상한까지는 정상 속도, 그 뒤로는 <see cref="LateLearningPenalty"/> 배만큼 느려진다.
        /// </summary>
        public static int ProficiencyFor(Alignment alignment, int trainingSessions)
        {
            if (trainingSessions < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(trainingSessions), "수련 횟수는 음수일 수 없다.");
            }

            double raw = trainingSessions * LearningRate(alignment);
            int soft = SoftCap(alignment);

            double effective;
            if (raw <= soft)
            {
                effective = raw;
            }
            else
            {
                // 소프트 상한 초과분은 페널티 배수만큼 나눠서 반영된다 — 성장이 멈추지는 않는다.
                effective = soft + (raw - soft) / LateLearningPenalty(alignment);
            }

            int result = (int)effective;
            return result > HardCap ? HardCap : result;
        }

        /// <summary>
        /// 숙련도가 초식 위력에 곱해지는 배율. 성향마다 곡선의 모양 자체가 다르다.
        /// 이 함수가 세 성향의 정체성을 만든다.
        /// </summary>
        public static double PowerMultiplier(Alignment alignment, int proficiency)
        {
            if (proficiency < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(proficiency), "숙련도는 음수일 수 없다.");
            }

            int p = proficiency > HardCap ? HardCap : proficiency;

            switch (alignment)
            {
                case Alignment.Orthodox:
                    // 선형 — 꾸준하고 예측 가능하다.
                    return 1.0 + (p / 100.0) * OrthodoxPowerSpan;

                case Alignment.Unorthodox:
                    // 제곱근 — 초반에 급격히 오르고 갈수록 완만해진다. 만렙에서도 고점이 가장 낮다.
                    return 1.0 + Math.Sqrt(p / 100.0) * UnorthodoxPowerSpan;

                case Alignment.Demonic:
                    // 계단 — 경지(25 숙련)를 넘을 때만 뛴다.
                    // ⚠ 숙련 24 까지는 배율이 1.0 이라 정파보다도 약하다. 버그가 아니라 설계다.
                    //   "첫 계단까지 도달하는 비용" 이 마도가 치르는 대가다.
                    return 1.0 + (p / DemonicStepSize) * DemonicStepGain;

                default:
                    throw new ArgumentOutOfRangeException(nameof(alignment));
            }
        }

        /// <summary>
        /// **그 숙련도에 도달하는 데 필요한 최소 수련 횟수** — <see cref="ProficiencyFor"/> 의 역함수.
        ///
        /// ⚠⚠ 2026-07-31 신설. 측정을 **수련 횟수가 아니라 무공 경지**(<see cref="MartialStage"/>)로
        ///   지정하기 위해 필요하다. 같은 200회가 정파에게는 10성이고 마도에게는 9성이라,
        ///   횟수로 기준을 잡으면 **성향마다 다른 지점을 비교하게 된다.**
        /// </summary>
        public static int SessionsToReach(Alignment alignment, int proficiency)
        {
            EnsureKnown(alignment);
            if (proficiency <= 0) return 0;

            int p = proficiency > HardCap ? HardCap : proficiency;
            int soft = SoftCap(alignment);

            // 소프트 상한 이전은 그대로, 이후는 페널티 배수만큼 더 든다(ProficiencyFor 의 역).
            double rawNeeded = p <= soft ? p : soft + (p - soft) * LateLearningPenalty(alignment);
            return (int)Math.Ceiling(rawNeeded / LearningRate(alignment));
        }

        /// <summary>절대 상한(100)에 도달하는 데 필요한 최소 수련 횟수.</summary>
        public static int SessionsToMaster(Alignment alignment)
        {
            int soft = SoftCap(alignment);
            double rawNeeded = soft + (HardCap - soft) * LateLearningPenalty(alignment);
            return (int)Math.Ceiling(rawNeeded / LearningRate(alignment));
        }

        private static void EnsureKnown(Alignment alignment)
        {
            if (alignment != Alignment.Orthodox && alignment != Alignment.Unorthodox && alignment != Alignment.Demonic)
            {
                throw new ArgumentOutOfRangeException(nameof(alignment));
            }
        }
    }
}
