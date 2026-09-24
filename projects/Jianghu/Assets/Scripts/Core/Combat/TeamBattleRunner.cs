using System;
using System.Collections.Generic;
using Jianghu.Core.Rng;

namespace Jianghu.Core.Combat
{
    /// <summary>
    /// **같은 편성으로 여러 번 싸운 결과의 요약.**
    ///
    /// ⚠⚠ 원수(原數)를 함께 든다. <see cref="TeamAWinRate"/> 는 무승부를 반 점으로 세는 값이라
    ///   그것만 보면 *"무승부가 몇 번이었는지"* 가 사라진다. 이 저장소는 요약 하나만 보고
    ///   전체를 판단하다 여러 번 틀렸다(§5-D 선택적 관찰).
    /// </summary>
    public sealed class TeamBattleSummary
    {
        public int Fights { get; }
        public int TeamAWins { get; }
        public int TeamBWins { get; }
        public int Draws { get; }

        /// <summary>
        /// A팀 승률(0~1). **⚠ 무승부는 0.5점으로 센다** — 1대1 측정(`Sandbox.WinRate`)과 같은 관례다.
        /// 다르게 세면 두 표를 나란히 읽을 수 없다.
        /// </summary>
        public double TeamAWinRate { get; }

        /// <summary>
        /// **승률의 표준오차(1σ, 0~1).** 판마다의 점수(승 1 · 무 0.5 · 패 0)에서 낸 표본 표준편차를
        /// `√판수` 로 나눈 값이다.
        ///
        /// ⚠⚠ **이 값이 없으면 잡음을 신호로 읽는다.** 거울 대진(같은 무공 4대4)을 100판 돌렸더니
        ///   **43%** 가 나왔다 — 50% 여야 하는 대진이다. 500판으로 늘리니 49.2% 로 들어왔고,
        ///   43% 는 표집 잡음(−1.4σ)이었다. 100판의 1σ 가 **약 ±5%p** 라서 그렇다(2026-08-23 실측).
        /// ⚠ 그러므로 **`41% vs 43%` 같은 2%p 차이는 100판으로 판별할 수 없다.** 화면이 승률만
        ///   내놓으면 그 차이를 근거로 T 를 정하게 된다.
        /// </summary>
        public double StandardError { get; }

        /// <summary>평균 경합 수. 전투가 늘어지는지 보는 축이다.</summary>
        public double AverageRounds { get; }

        /// <summary>평균 생존자 수. **승패만으로는 안 보이는 "얼마나 이겼나"** 가 여기 있다.</summary>
        public double AverageSurvivorsA { get; }

        public double AverageSurvivorsB { get; }

        public TeamBattleSummary(
            int fights, int teamAWins, int teamBWins, int draws,
            double teamAWinRate, double standardError, double averageRounds,
            double averageSurvivorsA, double averageSurvivorsB)
        {
            StandardError = standardError;
            Fights = fights;
            TeamAWins = teamAWins;
            TeamBWins = teamBWins;
            Draws = draws;
            TeamAWinRate = teamAWinRate;
            AverageRounds = averageRounds;
            AverageSurvivorsA = averageSurvivorsA;
            AverageSurvivorsB = averageSurvivorsB;
        }

        /// <summary>`52.5% ±5.0%p (A 20승 · B 18승 · 무 2) · 평균 9.3경합 · 생존 1.4대1.1` 형태.</summary>
        public override string ToString()
        {
            return (TeamAWinRate * 100).ToString("F1") + "%"
                   + " ±" + (StandardError * 100).ToString("F1") + "%p "
                   + "(A " + TeamAWins + "승 · B " + TeamBWins + "승 · 무 " + Draws + ")"
                   + " · 평균 " + AverageRounds.ToString("F1") + "경합"
                   + " · 생존 " + AverageSurvivorsA.ToString("F1") + "대" + AverageSurvivorsB.ToString("F1");
        }
    }

    /// <summary>
    /// **같은 편성을 시드만 바꿔 여러 번 돌린다.**
    ///
    /// ⚠⚠ **왜 필요한가** — 4대4 한 전투는 분산이 크다. 자동 측정도 40전씩 돌리고(1대1은 100전),
    ///   그래서 전투 화면이 *"1전투를 보고 T 를 정하는"* 일을 못 하게 하려면 이 도구가 있어야 한다
    ///   (설계 `docs/ui-combat-view-plan.md` §6).
    ///
    /// ⚠⚠ **승률 공식을 여기 한 곳에만 둔다.** Sandbox 의 `TeamWinRate` 도 이것을 쓴다 —
    ///   무승부를 반 점으로 세는 관례가 두 곳에 있으면 언젠가 갈라지고, 그러면 화면과 측정표가
    ///   **다른 승률을 말하는데 둘 다 "승률" 이라고 부르게** 된다.
    /// </summary>
    public static class TeamBattleRunner
    {
        /// <summary>
        /// 첫 시드. ⚠ **1 이다** — Sandbox 의 모든 측정 루프가 `seed = 1` 부터 돈다.
        /// 화면이 0 부터 돌면 같은 편성인데 다른 숫자가 나와 *"엔진이 바뀌었나"* 를 의심하게 된다.
        /// </summary>
        public const uint FirstSeed = 1;

        /// <summary>
        /// 한 판만 싸운다. 로그 전문이 필요할 때(화면의 `1회` 버튼) 쓴다.
        /// ⚠ 시드가 같으면 **결과도 같다**(프로젝트 §1-2 결정론 난수).
        /// </summary>
        public static TeamCombatResult RunOnce(
            IReadOnlyList<BattlePlacement> teamA, IReadOnlyList<BattlePlacement> teamB,
            uint seed = FirstSeed, int maxRounds = CombatResolver.DefaultMaxTurns)
        {
            return CombatResolver.ResolveTeams(teamA, teamB, new XorShiftRandom(seed), maxRounds);
        }

        /// <summary>
        /// <paramref name="fights"/> 번 싸우고 요약한다. 시드는 <paramref name="firstSeed"/> 부터 1씩 는다.
        ///
        /// ⚠⚠ **시드를 바꾸지 않으면 같은 전투를 N번 보는 것**이다. 결정론 난수라 그렇다 —
        ///   합계가 커져서 통계처럼 보이지만 표본은 여전히 1이다.
        /// </summary>
        /// <param name="onEach">
        /// 판마다 부르는 갈고리. 요약에 없는 것을 세는 쪽(Sandbox 의 반격 집계)이 쓴다.
        /// ⚠ 이것이 있어서 **승률 공식을 복제하지 않고도** 남의 집계를 얹을 수 있다.
        /// </param>
        /// <param name="maxRounds">
        /// 이 경합 수를 넘기면 무승부다. ⚠ 기본값을 바꾸면 기존 측정이 전부 움직인다 —
        /// 짧게 주는 것은 **무승부 처리를 시험할 때** 쓴다.
        /// </param>
        public static TeamBattleSummary Run(
            IReadOnlyList<BattlePlacement> teamA, IReadOnlyList<BattlePlacement> teamB,
            int fights, uint firstSeed = FirstSeed, Action<TeamCombatResult> onEach = null,
            int maxRounds = CombatResolver.DefaultMaxTurns)
        {
            if (fights <= 0) throw new ArgumentOutOfRangeException(nameof(fights), "한 판 이상 싸워야 한다.");

            int aWins = 0, bWins = 0, draws = 0;
            long rounds = 0;
            long survivorsA = 0, survivorsB = 0;

            for (int i = 0; i < fights; i++)
            {
                TeamCombatResult r = RunOnce(teamA, teamB, unchecked(firstSeed + (uint)i), maxRounds);

                if (r.Outcome == TeamOutcome.TeamAWin) aWins++;
                else if (r.Outcome == TeamOutcome.TeamBWin) bWins++;
                else draws++;

                rounds += r.Rounds;
                survivorsA += r.SurvivorsA;
                survivorsB += r.SurvivorsB;

                if (onEach != null) onEach(r);
            }

            // ⚠ 무승부 0.5점 — 1대1 측정과 같은 관례다(위 `TeamAWinRate` 주석).
            double score = aWins + draws * 0.5;
            double rate = score / fights;

            // 판마다의 점수는 1 / 0.5 / 0 셋뿐이라 표본 분산을 개수만으로 낼 수 있다.
            // ⚠ 이항근사(√p(1−p)/n)를 쓰지 않는다 — 무승부가 0.5 라 그 근사는 틀린다.
            double variance =
                (aWins * Square(1.0 - rate)
                 + draws * Square(0.5 - rate)
                 + bWins * Square(0.0 - rate)) / fights;
            double standardError = fights > 1 ? System.Math.Sqrt(variance / fights) : 0;

            return new TeamBattleSummary(
                fights, aWins, bWins, draws,
                rate, standardError,
                (double)rounds / fights,
                (double)survivorsA / fights,
                (double)survivorsB / fights);
        }

        private static double Square(double x)
        {
            return x * x;
        }
    }
}
