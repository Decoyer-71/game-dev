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

        public IReadOnlyList<CombatLogEntry> Log { get; }

        public CombatResult(
            CombatOutcome outcome, string winnerName, int turns,
            int attackerHealthLeft, int defenderHealthLeft, IReadOnlyList<CombatLogEntry> log)
        {
            Outcome = outcome;
            WinnerName = winnerName;
            Turns = turns;
            AttackerHealthLeft = attackerHealthLeft;
            DefenderHealthLeft = defenderHealthLeft;
            Log = log ?? throw new ArgumentNullException(nameof(log));
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
