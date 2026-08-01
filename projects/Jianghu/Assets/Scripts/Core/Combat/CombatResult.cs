using System;
using System.Collections.Generic;

namespace Jianghu.Core.Combat
{
    /// <summary>전투의 결말.</summary>
    public enum CombatOutcome
    {
        /// <summary>선공 여부와 무관하게, 전투를 신청한 쪽(첫 인자)이 이겼다.</summary>
        AttackerWin = 0,

        /// <summary>상대(둘째 인자)가 이겼다.</summary>
        DefenderWin = 1,

        /// <summary>최대 턴에 도달했다. 무승부.</summary>
        Draw = 2,
    }

    /// <summary>
    /// 전투 하나의 결과. 같은 입력 + 같은 시드면 항상 동일하다.
    /// </summary>
    public sealed class CombatResult
    {
        public CombatOutcome Outcome { get; }

        /// <summary>이긴 쪽 이름. 무승부면 null.</summary>
        public string WinnerName { get; }

        /// <summary>진행된 턴 수.</summary>
        public int Turns { get; }

        /// <summary>남은 체력 — 진단·밸런싱용. 아슬아슬한 승리인지 압승인지 구분된다.</summary>
        public int AttackerHealthLeft { get; }
        public int DefenderHealthLeft { get; }

        /// <summary>
        /// **평타 전락 계측** (2026-08-01 신설) — 양쪽이 낸 행동 수와, 그중 기력이 모자라
        /// 평타로 내려앉은 횟수.
        ///
        /// ⚠⚠ 설계안 §5-3 이 *"평타 전락률 목표 10~30%"* 를 기력 설계의 판정 기준으로 못박고
        ///   <see cref="Martial.Morphemes.MorphemeParser.QiCostPerMorpheme"/> 주석도 그것을 지목하는데,
        ///   **재는 코드가 없었다.** 그래서 기력 상수를 고를 때 근거가 산술 추정뿐이었다.
        ///
        /// ⚠ 분모는 **행동**이다(턴 아님) · **반격은 제외**한다. 이유는 `CombatResolver.PerformAction` 주석.
        /// </summary>
        public int AttackerActions { get; }
        public int AttackerBasicStrikes { get; }
        public int DefenderActions { get; }
        public int DefenderBasicStrikes { get; }

        /// <summary>
        /// 양쪽 합산 평타 전락률(%). 행동이 0 이면 0.
        ///
        /// ⚠ **합산이다.** 비대칭 대결(예: 권 vs 검)에서 한쪽만 마르는 경우를 보려면
        ///   위의 네 값을 직접 쓴다 — 이 값 하나로는 어느 쪽이 말랐는지 알 수 없다.
        /// </summary>
        public double BasicStrikeRate
        {
            get
            {
                int actions = AttackerActions + DefenderActions;
                if (actions <= 0) return 0;
                return (AttackerBasicStrikes + DefenderBasicStrikes) * 100.0 / actions;
            }
        }

        public IReadOnlyList<CombatLogEntry> Log { get; }

        public CombatResult(
            CombatOutcome outcome, string winnerName, int turns,
            int attackerHealthLeft, int defenderHealthLeft, IReadOnlyList<CombatLogEntry> log,
            int attackerActions = 0, int attackerBasicStrikes = 0,
            int defenderActions = 0, int defenderBasicStrikes = 0)
        {
            Outcome = outcome;
            WinnerName = winnerName;
            Turns = turns;
            AttackerHealthLeft = attackerHealthLeft;
            DefenderHealthLeft = defenderHealthLeft;
            Log = log ?? throw new ArgumentNullException(nameof(log));
            AttackerActions = attackerActions;
            AttackerBasicStrikes = attackerBasicStrikes;
            DefenderActions = defenderActions;
            DefenderBasicStrikes = defenderBasicStrikes;
        }

        public override string ToString()
        {
            switch (Outcome)
            {
                case CombatOutcome.Draw:
                    return Turns + "턴 무승부";
                default:
                    return Turns + "턴, " + WinnerName + " 승";
            }
        }
    }
}
