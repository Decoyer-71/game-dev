using System;
using Jianghu.Core.Martial.Morphemes;

namespace Jianghu.Core.Combat
{
    /// <summary>
    /// **한 사람과 그 사람이 선 열.** 다대다 전투에 들어가는 단위다(설계 <c>docs/multi-combat-plan.md</c> §3-0).
    ///
    /// ⚠⚠ **왜 <see cref="Combatant"/> 에 열 필드를 넣지 않았는가** — 배치는 *그 전투에서의 배치*이지
    ///   사람의 속성이 아니다. 같은 제자가 이번 비무에서는 전열, 다음 문파전에서는 후열에 설 수 있다.
    ///   그리고 배치는 **플레이어의 결정**이므로(Phase 3/4) 캐릭터 데이터가 아니라 전투 입력에 실린다.
    ///
    /// ⚠ 측정 하네스는 4인 편성에서 **전열 2 + 후열 2 를 고정 상수**로 쓴다. 엔진은 비대칭·가변을 받는다.
    /// </summary>
    public readonly struct BattlePlacement
    {
        public Combatant Fighter { get; }
        public BattleRow Row { get; }

        public BattlePlacement(Combatant fighter, BattleRow row)
        {
            if (fighter == null) throw new ArgumentNullException(nameof(fighter));
            Fighter = fighter;
            Row = row;
        }

        /// <summary>전열에 세운다.</summary>
        public static BattlePlacement Front(Combatant fighter)
        {
            return new BattlePlacement(fighter, BattleRow.Front);
        }

        /// <summary>후열에 세운다.</summary>
        public static BattlePlacement Rear(Combatant fighter)
        {
            return new BattlePlacement(fighter, BattleRow.Rear);
        }

        public override string ToString()
        {
            return (Fighter == null ? "(빈자리)" : Fighter.Name) + (Row == BattleRow.Front ? "(전열)" : "(후열)");
        }
    }
}
