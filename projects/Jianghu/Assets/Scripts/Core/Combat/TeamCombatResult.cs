using System;
using System.Collections.Generic;

namespace Jianghu.Core.Combat
{
    /// <summary>팀 전투의 결말.</summary>
    public enum TeamOutcome
    {
        /// <summary>첫 인자로 넘긴 팀이 이겼다.</summary>
        TeamAWin = 0,

        /// <summary>둘째 인자로 넘긴 팀이 이겼다.</summary>
        TeamBWin = 1,

        /// <summary>최대 경합에 도달했거나 양쪽이 동시에 전멸했다.</summary>
        Draw = 2,
    }

    /// <summary>
    /// 팀 전투 하나의 결과. 같은 입력 + 같은 시드면 항상 동일하다.
    ///
    /// ⚠⚠ <see cref="CombatResult"/> 를 고치지 않고 **병행 타입**으로 만들었다.
    ///   그쪽은 <c>AttackerHealthLeft</c>/<c>DefenderHealthLeft</c> 처럼 **참가자가 정확히 둘**이라는
    ///   전제로 필드가 박혀 있어, 고치면 Combat 테스트 79건이 통째로 노출된다.
    ///   근거는 <c>docs/multi-combat-plan.md</c> §3-3.
    ///
    /// ⚠ 진행 단위를 <b>턴(turn)</b> 이 아니라 <b>경합(round)</b> 이라 부른다. 1대1의 "턴" 은
    ///   *"양쪽이 한 번씩"* 이었지만 여기서는 *"살아 있는 전원이 한 번씩"* 이기 때문이다.
    /// </summary>
    public sealed class TeamCombatResult
    {
        public TeamOutcome Outcome { get; }

        /// <summary>진행된 경합 수.</summary>
        public int Rounds { get; }

        /// <summary>
        /// 각 팀 구성원의 남은 체력 — <b>입력 순서 그대로</b>다. 0 이면 쓰러진 것이고,
        /// 살아남은 인원수·잔여 체력 합이 여기서 나온다(압승인지 신승인지 구분된다).
        /// </summary>
        public IReadOnlyList<int> TeamAHealthLeft { get; }
        public IReadOnlyList<int> TeamBHealthLeft { get; }

        /// <summary>
        /// **평타 전락 계측** — 팀 단위 합산. 뜻과 분모 규칙은 <see cref="CombatResult.AttackerActions"/> 와 같다
        /// (분모는 턴이 아니라 **행동** · **반격은 세지 않는다**).
        ///
        /// ⚠ 범위 무공이라도 **행동은 1회로 센다.** 한 행동으로 넷을 때려도 기력은 한 번 내기 때문이다
        ///   (<c>CombatResolver.StrikeTarget</c> 주석의 행동 단위 / 대상 단위 구분).
        /// </summary>
        public int TeamAActions { get; }
        public int TeamABasicStrikes { get; }
        public int TeamBActions { get; }
        public int TeamBBasicStrikes { get; }

        /// <summary>양 팀 합산 평타 전락률(%). 행동이 0 이면 0.</summary>
        public double BasicStrikeRate
        {
            get
            {
                int actions = TeamAActions + TeamBActions;
                if (actions <= 0) return 0;
                return (TeamABasicStrikes + TeamBBasicStrikes) * 100.0 / actions;
            }
        }

        public IReadOnlyList<CombatLogEntry> Log { get; }

        public TeamCombatResult(
            TeamOutcome outcome, int rounds,
            IReadOnlyList<int> teamAHealthLeft, IReadOnlyList<int> teamBHealthLeft,
            IReadOnlyList<CombatLogEntry> log,
            int teamAActions = 0, int teamABasicStrikes = 0,
            int teamBActions = 0, int teamBBasicStrikes = 0)
        {
            Outcome = outcome;
            Rounds = rounds;
            TeamAHealthLeft = teamAHealthLeft ?? throw new ArgumentNullException(nameof(teamAHealthLeft));
            TeamBHealthLeft = teamBHealthLeft ?? throw new ArgumentNullException(nameof(teamBHealthLeft));
            Log = log ?? throw new ArgumentNullException(nameof(log));
            TeamAActions = teamAActions;
            TeamABasicStrikes = teamABasicStrikes;
            TeamBActions = teamBActions;
            TeamBBasicStrikes = teamBBasicStrikes;
        }

        /// <summary>살아남은 인원 수.</summary>
        public int SurvivorsA { get { return CountAlive(TeamAHealthLeft); } }
        public int SurvivorsB { get { return CountAlive(TeamBHealthLeft); } }

        private static int CountAlive(IReadOnlyList<int> health)
        {
            int n = 0;
            for (int i = 0; i < health.Count; i++)
            {
                if (health[i] > 0) n++;
            }
            return n;
        }

        public override string ToString()
        {
            switch (Outcome)
            {
                case TeamOutcome.Draw:
                    return Rounds + "경합 무승부 (" + SurvivorsA + "대" + SurvivorsB + " 생존)";
                case TeamOutcome.TeamAWin:
                    return Rounds + "경합, A 승 (" + SurvivorsA + "명 생존)";
                default:
                    return Rounds + "경합, B 승 (" + SurvivorsB + "명 생존)";
            }
        }
    }
}
