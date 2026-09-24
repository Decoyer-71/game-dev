using System.Collections.Generic;
using Jianghu.Core.Combat;
using Jianghu.Core.Martial.Morphemes;

namespace Jianghu.Core.Martial.Display
{
    /// <summary>
    /// <see cref="ArtStatDelta"/> 를 **사람이 읽을 축 목록**으로 편다.
    ///
    /// ⚠⚠ **0 인 축을 빼는 것이 이 클래스의 존재 이유다.** 축이 23개라 전부 찍으면
    ///   글자 하나가 건드린 두세 축이 20개의 `0` 사이에 묻힌다. 반대로 화면이 축 하나만
    ///   골라 찍으면(스모크 화면이 그랬다) 성(聖)처럼 **공격이 0 이고 방어·막기·상태저항을
    ///   주는 글자가 "아무것도 안 하는 글자" 로 보인다.** 그래서 *"0 아닌 것 전부"* 다.
    ///
    /// ⚠⚠ **<see cref="ArtStatDelta.AttackPenalty"/> 는 내지 않는다.** 그 값은 이미
    ///   <see cref="ArtStatDelta.Attack"/> 안에 들어 있는 파생값이라(그쪽 주석), 같이 찍으면
    ///   `환(−2)` 이 *"공격 −2 · 공격페널티 −2"* 로 보여 **−4 로 읽힌다.** 필요한 쪽은
    ///   구조체에서 직접 읽는다.
    ///
    /// ⚠⚠ **회피율·명중은 사전 값이 곧 %p 가 아니다 — 엔진이 상수를 곱한다.** 23축을 전수로
    ///   대조해 이 둘만 그렇다는 것을 확인했다(2026-08-23):
    ///
    ///   | 축 | 사전 값 | 엔진 | 실제 |
    ///   |---|---|---|---|
    ///   | 회피율 | `피·둔·섬 15` | <see cref="Combatant.EvasionPointToPercent"/> = 0.3 | **+4.5%p** |
    ///   | 명중 | `쾌 −2` | <see cref="CombatResolver.AccuracyPointToPercent"/> = 5 | **−10%p** |
    ///
    ///   처음에는 사전 값을 그대로 내고 `회피율 +15%p` 라 적었는데, 그건 **3.33배 과장**이고
    ///   *"존재하지 않는 값에 그럴듯한 단위를 붙인 것"* 이다 — 프로젝트 §1-0 이 등재한
    ///   **방어관통 160%** 와 같은 형태의 거짓말이라 `verify` 가 잡았다.
    ///   ⚠ **상수를 여기 베끼지 않고 엔진 것을 참조한다.** 저쪽이 미검증 초기값이라 움직일 수 있고
    ///     (`CombatResolver.AccuracyPointToPercent` 주석), 베끼면 그때 화면만 옛값에 남는다.
    ///   ⚠ 곱셈은 **선형**이라 *"글자별 합 = 합계"* 가 그대로 성립한다.
    /// </summary>
    public static class ArtStatDeltaDisplay
    {
        /// <summary>표시 대상 축의 수. 구조체 필드 24개에서 파생값 <c>AttackPenalty</c> 하나를 뺀 값이다.</summary>
        public const int AxisCount = 23;

        /// <summary>
        /// 0 이 아닌 축만 뽑는다. 순서는 <see cref="ArtStatDelta"/> 의 선언 순서 —
        /// 공격·방어 본체 → 확률축 → 기력 → 상태이상 — 를 따른다. 정의서 §1 의 순서이기도 하다.
        /// </summary>
        public static IReadOnlyList<StatAxisValue> NonZeroAxes(ArtStatDelta delta)
        {
            var axes = new List<StatAxisValue>();

            // ── 공격·방어 본체 (단위 없는 스칼라) ──
            Add(axes, "공격", delta.Attack, "");
            Add(axes, "방어", delta.Defense, "");
            Add(axes, "명중", delta.Accuracy * CombatResolver.AccuracyPointToPercent, "%p");
            Add(axes, "속도", delta.Speed, "");

            // ── 확률축 ──
            Add(axes, "회피율", delta.Evasion * Combatant.EvasionPointToPercent, "%p");
            Add(axes, "막기확률", delta.BlockChance, "%p");
            Add(axes, "반격률", delta.CounterRate, "%p");
            Add(axes, "치명률", delta.CritChance, "%p");
            Add(axes, "치명배율", delta.CritMultiplier, "배");
            Add(axes, "방어무시", delta.DefenseIgnore, "%");
            Add(axes, "총위력", delta.AttackPercent, "%");

            // ── 기력 ──
            Add(axes, "최대기력", delta.MaxQi, "");
            Add(axes, "기력회복", delta.QiRegen, "/턴");
            Add(axes, "기력소모", delta.QiCostPercent, "%");

            // ── 상태이상 ──
            Add(axes, "상태이상 부여", delta.StatusApplyBonus, "%p");
            Add(axes, "상태이상 저항", delta.StatusResist, "%p");
            Add(axes, "중독", delta.PoisonChance, "%p");
            Add(axes, "출혈", delta.BleedChance, "%p");
            Add(axes, "화상", delta.BurnChance, "%p");
            Add(axes, "동상", delta.FrostbiteChance, "%p");
            Add(axes, "기력소실", delta.QiDrainChance, "%p");
            Add(axes, "경직", delta.StaggerChance, "%p");
            Add(axes, "마비", delta.ParalysisStack, "스택");

            return axes;
        }

        /// <summary>
        /// 축 목록을 `공격 +1.5 · 속도 +0.5` 로 잇는다. 비면 <paramref name="empty"/> 를 낸다.
        /// ⚠ 빈 목록은 **버그가 아니다** — 부정·무학분류·배경어는 수치를 0 주는 것이 설계다
        ///   (<see cref="ArtStatDelta.Zero"/> 주석).
        /// </summary>
        public static string Join(IReadOnlyList<StatAxisValue> axes, string empty = "수치 없음")
        {
            if (axes == null || axes.Count == 0) return empty;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < axes.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(axes[i].ToString());
            }
            return sb.ToString();
        }

        private static void Add(List<StatAxisValue> axes, string name, double value, string unit)
        {
            if (value == 0) return;
            axes.Add(new StatAxisValue(name, value, unit));
        }
    }
}
