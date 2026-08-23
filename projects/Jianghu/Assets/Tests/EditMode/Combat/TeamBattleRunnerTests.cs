using System.Collections.Generic;
using Jianghu.Core.Combat;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;
using NUnit.Framework;

namespace Jianghu.Tests.EditMode.Combat
{
    /// <summary>
    /// **반복 전투 요약 회귀 테스트.**
    ///
    /// ⚠⚠ 이 도구의 존재 이유는 *"1전투를 보고 판단하지 못하게"* 다. 4대4 한 판은 분산이 크고,
    ///   자동 측정도 40전씩 돌린다(1대1은 100전). 설계 `docs/ui-combat-view-plan.md` §6.
    /// </summary>
    public class TeamBattleRunnerTests
    {
        private const int Stage = 10;

        private static MartialArt Find(string name)
        {
            foreach (MartialArt a in MartialArtCatalog.All) if (a.Name == name) return a;
            Assert.Fail("무공 '" + name + "' 이 카탈로그에 없다 — 이 테스트의 전제가 깨졌다");
            return null;
        }

        /// <summary>전2 + 후2 동질 팀. 측정 관례와 같은 배치다.</summary>
        private static List<BattlePlacement> Team(string prefix, string artName)
        {
            MartialArt art = Find(artName);
            var team = new List<BattlePlacement>(4);
            for (int i = 0; i < 4; i++)
            {
                Combatant c = CombatantBuilder.Build(
                    prefix + (i + 1) + " " + art.Name, new[] { art }, Stage);
                team.Add(new BattlePlacement(c, i < 2 ? BattleRow.Front : BattleRow.Rear));
            }
            return team;
        }

        // ─────────────────────────── 결정론 ───────────────────────────

        /// <summary>같은 시드는 같은 전투다(프로젝트 §1-2).</summary>
        [Test]
        public void 같은_시드는_같은_전투다()
        {
            List<BattlePlacement> a = Team("A", "성뇌후격");
            List<BattlePlacement> b = Team("B", "만우쾌사");

            TeamCombatResult x = TeamBattleRunner.RunOnce(a, b, 7);
            TeamCombatResult y = TeamBattleRunner.RunOnce(a, b, 7);

            Assert.That(y.Outcome, Is.EqualTo(x.Outcome));
            Assert.That(y.Rounds, Is.EqualTo(x.Rounds));
            Assert.That(y.Log.Count, Is.EqualTo(x.Log.Count));
        }

        /// <summary>같은 인자로 부르면 요약도 같다 — 요약 계산 자체에 상태가 없다.</summary>
        [Test]
        public void 같은_인자면_요약도_같다()
        {
            List<BattlePlacement> a = Team("A", "성뇌후격");
            List<BattlePlacement> b = Team("B", "만우쾌사");

            TeamBattleSummary x = TeamBattleRunner.Run(a, b, 20);
            TeamBattleSummary y = TeamBattleRunner.Run(a, b, 20);

            Assert.That(y.TeamAWinRate, Is.EqualTo(x.TeamAWinRate));
            Assert.That(y.AverageRounds, Is.EqualTo(x.AverageRounds));
        }

        /// <summary>
        /// **시드를 안 바꾸면 같은 전투를 N번 보는 것이다.** 합계가 커져 통계처럼 보이지만 표본은 1이다.
        /// ⚠ 이 테스트가 그 함정을 못박는다 — 시드가 늘지 않게 고치면 여기서 깨진다.
        /// </summary>
        [Test]
        public void 시드가_늘어야_표본이_는다()
        {
            List<BattlePlacement> a = Team("A", "성뇌후격");
            List<BattlePlacement> b = Team("B", "만우쾌사");

            // ⚠⚠ **승패로 보면 안 된다.** 이 대진은 100:0 이라 20판이 전부 A승인 것이 정상이고,
            //   처음에 승패로 검사했다가 그 정상 결과에 걸려 테스트가 깨졌다. **대진이 기울면
            //   승패는 시드에 둔감하다.** 시드가 실제로 도는지는 **경합 수가 흔들리는가**로 본다.
            var rounds = new List<int>();
            TeamBattleRunner.Run(a, b, 20, onEach: r => rounds.Add(r.Rounds));

            Assert.That(rounds.Count, Is.EqualTo(20));
            Assert.That(rounds, Is.Not.All.EqualTo(rounds[0]),
                "20판의 경합 수가 전부 같다 — 시드가 안 늘고 있다");
        }

        /// <summary>첫 시드는 **1** 이다 — Sandbox 의 모든 측정 루프와 같아야 표가 나란히 읽힌다.</summary>
        [Test]
        public void 첫_시드는_1_이고_Sandbox_와_같다()
        {
            Assert.That(TeamBattleRunner.FirstSeed, Is.EqualTo(1u));

            List<BattlePlacement> a = Team("A", "성뇌후격");
            List<BattlePlacement> b = Team("B", "만우쾌사");

            // 1판만 돌린 요약은 시드 1 한 판과 같아야 한다.
            TeamBattleSummary one = TeamBattleRunner.Run(a, b, 1);
            TeamCombatResult seed1 = TeamBattleRunner.RunOnce(a, b, 1);

            Assert.That(one.AverageRounds, Is.EqualTo(seed1.Rounds));
            Assert.That(one.TeamAWins, Is.EqualTo(seed1.Outcome == TeamOutcome.TeamAWin ? 1 : 0));
        }

        // ─────────────────────────── 집계 ───────────────────────────

        /// <summary>승·패·무를 더하면 전체 판수다 — 어느 하나도 새지 않는다.</summary>
        [Test]
        public void 승패무를_더하면_전체_판수다()
        {
            TeamBattleSummary s = TeamBattleRunner.Run(Team("A", "성뇌후격"), Team("B", "만우쾌사"), 25);
            Assert.That(s.TeamAWins + s.TeamBWins + s.Draws, Is.EqualTo(s.Fights));
            Assert.That(s.Fights, Is.EqualTo(25));
        }

        /// <summary>
        /// **무승부는 0.5점이다** — 1대1 측정과 같은 관례. 다르게 세면 두 표를 나란히 못 읽는다.
        /// ⚠ 무승부를 0점이나 1점으로 바꾸면 여기서 깨진다.
        /// </summary>
        [Test]
        public void 무승부는_반_점으로_센다()
        {
            // ⚠⚠ **무승부를 실제로 만들어서 잰다.** 무승부가 0건인 대진으로 공식만 대조하면
            //   *"무승부를 0점으로 세도 통과하는"* 빈 테스트가 된다 — 이 저장소가 여러 번 밟은
            //   *"그 조건이 표본에 있는가"* 를 안 묻는 실수다.
            //   경합 1회로 자르면 아무도 안 죽어 **전 판이 무승부**가 된다.
            TeamBattleSummary all = TeamBattleRunner.Run(
                Team("A", "성뇌후격"), Team("B", "만우쾌사"), 10, maxRounds: 1);

            Assert.That(all.Draws, Is.EqualTo(10), "전제가 깨졌다 — 1경합에 승부가 났다");
            Assert.That(all.TeamAWinRate, Is.EqualTo(0.5).Within(1e-12));

            // 무승부가 없는 대진에서는 승수 그대로다.
            TeamBattleSummary none = TeamBattleRunner.Run(Team("A", "성뇌후격"), Team("B", "만우쾌사"), 25);
            Assert.That(none.Draws, Is.EqualTo(0));
            Assert.That(none.TeamAWinRate, Is.EqualTo((double)none.TeamAWins / none.Fights).Within(1e-12));
        }

        /// <summary>
        /// **표준오차가 실제로 계산된다.** 이 값이 없으면 잡음을 신호로 읽는다 —
        /// 거울 대진 100판이 43% 로 나왔던 것이 그 실례다(2026-08-23 실측).
        /// ⚠ 판수가 늘면 줄어야 한다. 안 줄면 계산이 판수를 안 보고 있는 것이다.
        /// </summary>
        [Test]
        public void 표준오차는_판수가_늘면_준다()
        {
            List<BattlePlacement> a = Team("A", "제화정참");
            List<BattlePlacement> b = Team("B", "제화정참");   // 거울 대진 — 점수가 흩어지는 조건

            TeamBattleSummary few = TeamBattleRunner.Run(a, b, 25);
            TeamBattleSummary many = TeamBattleRunner.Run(a, b, 400);

            Assert.That(few.StandardError, Is.GreaterThan(0), "흩어지는 대진인데 표준오차가 0이다");
            Assert.That(many.StandardError, Is.LessThan(few.StandardError));
        }

        /// <summary>
        /// **한쪽이 전승하면 표준오차는 0이다** — 흩어짐이 없으니 맞다.
        /// ⚠ 이항근사 `√p(1−p)/n` 도 이 경우 0을 주지만, 무승부가 0.5라 그 근사는 일반적으로 틀린다.
        ///   그래서 판별력이 있는 곳은 위 테스트다.
        /// </summary>
        [Test]
        public void 전승이면_표준오차가_0_이다()
        {
            TeamBattleSummary s = TeamBattleRunner.Run(Team("A", "성뇌후격"), Team("B", "만우쾌사"), 20);
            Assert.That(s.TeamAWins, Is.EqualTo(20), "전제가 깨졌다 — 이 대진이 더는 전승이 아니다");
            Assert.That(s.StandardError, Is.EqualTo(0).Within(1e-12));
        }

        /// <summary>전 판 무승부여도 흩어짐이 없다.</summary>
        [Test]
        public void 전_판_무승부면_표준오차가_0_이다()
        {
            TeamBattleSummary s = TeamBattleRunner.Run(
                Team("A", "성뇌후격"), Team("B", "만우쾌사"), 10, maxRounds: 1);

            Assert.That(s.Draws, Is.EqualTo(10));
            Assert.That(s.StandardError, Is.EqualTo(0).Within(1e-12));
        }

        /// <summary>평균 생존자는 0~팀 인원 사이다.</summary>
        [Test]
        public void 평균_생존자는_인원_범위_안이다()
        {
            TeamBattleSummary s = TeamBattleRunner.Run(Team("A", "성뇌후격"), Team("B", "만우쾌사"), 20);

            Assert.That(s.AverageSurvivorsA, Is.InRange(0.0, 4.0));
            Assert.That(s.AverageSurvivorsB, Is.InRange(0.0, 4.0));
            Assert.That(s.AverageRounds, Is.GreaterThan(0));
        }

        /// <summary>갈고리가 판마다 한 번씩 불린다 — Sandbox 의 반격 집계가 이것에 얹혀 있다.</summary>
        [Test]
        public void 갈고리는_판마다_한_번_불린다()
        {
            int called = 0;
            TeamBattleRunner.Run(Team("A", "성뇌후격"), Team("B", "만우쾌사"), 12, onEach: r => called++);
            Assert.That(called, Is.EqualTo(12));
        }

        /// <summary>0판은 만들 수 없다 — 0으로 나누기를 조용히 넘기지 않는다.</summary>
        [Test]
        public void 판수가_0_이하면_예외다()
        {
            List<BattlePlacement> a = Team("A", "성뇌후격");
            List<BattlePlacement> b = Team("B", "만우쾌사");

            Assert.Throws<System.ArgumentOutOfRangeException>(() => TeamBattleRunner.Run(a, b, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => TeamBattleRunner.Run(a, b, -1));
        }

        // ─────────────────────────── 편성 ───────────────────────────

        /// <summary>
        /// **인원이 다른 편성도 받는다** — 설계 §3 이 *"3대4 도 봐야 ①의 답이 나온다"* 고 적었고,
        /// `ResolveTeams` 는 각 팀 1명 이상만 요구한다.
        /// </summary>
        [Test]
        public void 인원이_달라도_싸울_수_있다()
        {
            List<BattlePlacement> a = Team("A", "성뇌후격");
            List<BattlePlacement> b = Team("B", "만우쾌사");
            b.RemoveAt(3);

            TeamBattleSummary s = TeamBattleRunner.Run(a, b, 10);
            Assert.That(s.Fights, Is.EqualTo(10));
            Assert.That(s.AverageSurvivorsB, Is.InRange(0.0, 3.0));
        }
    }
}
