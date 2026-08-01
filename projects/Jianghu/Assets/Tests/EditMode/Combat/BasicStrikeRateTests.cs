using System.Collections.Generic;
using Jianghu.Core.Characters;
using Jianghu.Core.Combat;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;
using Jianghu.Core.Rng;
using NUnit.Framework;

namespace Jianghu.Tests.Combat
{
    /// <summary>
    /// **평타 전락률 계측이 실제로 도는가** (2026-08-01 신설).
    ///
    /// ⚠⚠ 이 지표가 없어서 기력 소모 상수를 4 ↔ 3 으로 놓고 **두 번 헛돌았다.**
    ///   설계안 §5-3 이 *"평타 전락률 10~30%"* 를 기력 설계의 판정 기준으로 못박고
    ///   `MorphemeParser.QiCostPerMorpheme` 주석도 그 값을 지목하는데,
    ///   **정작 재는 코드가 없어 산술 추정으로만 다퉜다.** 측정 도구의 분해능을 먼저 본다는
    ///   규율(HANDOFF §5)의 반복 사례다.
    ///
    /// ⚠ 절대 수치를 박지 않는다(HANDOFF §7). 검증하는 것은 값이 아니라 **모양**이다 —
    ///   "분모가 분자를 담는가", "안 마르는 무공은 0 인가", "비싼 무공은 더 마르는가".
    ///   밸런싱으로 상수가 바뀌어도 이 테스트들은 살아 있어야 한다.
    /// </summary>
    public class BasicStrikeRateTests
    {
        private const int Seeds = 100;

        /// <summary>⚠ 무공 경지의 최종점. 축 검증은 10성에서 한다(2026-07-31 사용자 확정).</summary>
        private const int Stage = MartialStage.MaxStage;

        /// <summary>대문파·정파·검 고정 — Sandbox 및 다른 전투 테스트와 같은 전제.</summary>
        private static Combatant Fighter(string artName)
        {
            MartialArt art = MartialArtFactory.Create(
                "s_" + artName, artName, ArtKind.Attack, ArtTier.Major,
                Discipline.Sword, Alignment.Orthodox, "화산파");

            int sessions = AlignmentCurve.SessionsToReach(
                Alignment.Orthodox, MartialStage.ProficiencyForStage(Stage));

            return new Combatant(artName, CharacterStats.MaxLevel(),
                new List<LearnedArt> { new LearnedArt(art, sessions, Alignment.Orthodox) },
                new List<DisciplineMastery>
                {
                    new DisciplineMastery(Discipline.Sword, DisciplineCurve.SessionsToMaster(Discipline.Sword)),
                });
        }

        /// <summary>같은 무공끼리 100판 붙여 평타 전락률 평균을 낸다.</summary>
        private static double MirrorRate(string artName)
        {
            Combatant a = Fighter(artName);
            Combatant b = Fighter(artName);

            double sum = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                sum += CombatResolver.Resolve(a, b, new XorShiftRandom(seed)).BasicStrikeRate;
            }
            return sum / Seeds;
        }

        [Test]
        public void 평타_수는_행동_수를_넘지_않는다()
        {
            Combatant a = Fighter("참정독명");
            Combatant b = Fighter("참정독명");

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(a, b, new XorShiftRandom(seed));

                Assert.That(r.AttackerActions, Is.GreaterThan(0), "행동이 한 번도 없는 전투는 성립하지 않는다");
                Assert.That(r.AttackerBasicStrikes, Is.InRange(0, r.AttackerActions));
                Assert.That(r.DefenderBasicStrikes, Is.InRange(0, r.DefenderActions));
                Assert.That(r.BasicStrikeRate, Is.InRange(0.0, 100.0));
            }
        }

        [Test]
        public void 기력이_마르지_않는_무공은_전락률이_0_이다()
        {
            // 2자 무공은 소모가 회복보다 작아 구조적으로 마를 수 없다.
            // ⚠ 이 테스트는 상수값에 의존하지 않는다 — 마르지 않는 무공이 무엇이든
            //   "안 마르면 전락이 없다" 는 관계 자체를 고정한다.
            Assert.That(MirrorRate("참정"), Is.EqualTo(0.0),
                "소모가 회복 이하인 무공에서 평타가 나왔다면 계측이 엉뚱한 것을 세고 있다");
        }

        [Test]
        public void 형태소가_많은_무공일수록_더_자주_평타로_내려앉는다()
        {
            // 기력 소모는 형태소 개수에 비례하므로 전락률은 단조 증가해야 한다.
            // ⚠⚠ 이 순서가 깨지면 기력 축이 계층과 반대로 돌고 있다는 뜻이다 —
            //   그게 2026-08-01 에 상수를 두고 다툰 바로 그 문제다.
            double two = MirrorRate("참정");
            double three = MirrorRate("참정독");
            double four = MirrorRate("참정독명");

            // ⚠⚠ **공허한 통과를 막는다** (2026-08-01). 전부 0 이면 `0 ≥ 0 ≥ 0` 이라 이 테스트는
            //   통과하지만 아무것도 증명하지 못한다. 그런데 2026-08-01 실측이 정확히 그 상태였다 —
            //   기력 소모 상수 3 에서는 4자 무공조차 한 번도 마르지 않아 전락률이 세 경지 전부 0.0% 였다.
            //   통과로 위장되면 다음 세션이 "기력 축은 검증됐다" 고 믿는다. Inconclusive 로 드러낸다.
            if (four <= 0.05)
            {
                Assert.Inconclusive(
                    "평타 전락이 한 번도 일어나지 않아 단조성을 판정할 수 없다 — "
                    + "기력 축이 죽어 있다는 뜻이다(설계안 §5-3: 0% 면 기력 설계가 죽은 것).");
            }

            Assert.That(three, Is.GreaterThanOrEqualTo(two));
            Assert.That(four, Is.GreaterThanOrEqualTo(three));
        }

        [Test]
        public void 권_숙달자는_같은_무공을_들어도_덜_마른다()
        {
            // 권(拳)의 유형 특성은 기력 소모 감소다(DisciplineCurve). 그 특성이 살아 있다면
            // 같은 무공을 권으로 들었을 때 전락률이 검보다 낮아야 한다.
            // ⚠ 이것이 권의 정체성("비싼 무공을 끊김 없이 쓴다")이 코드에 존재하는지의 검사다.
            //   ⚠⚠ 전락률이 양쪽 모두 0 이면 이 테스트는 통과하지만 아무것도 증명하지 못한다 —
            //   그래서 4자 무공(가장 비싼 쪽)으로 재고, 검 쪽이 실제로 말랐는지 함께 본다.
            const string Art = "참정독명";

            Combatant sword = Fighter(Art);
            Combatant fist = FistFighter(Art);

            double swordDry = 0, fistDry = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(sword, fist, new XorShiftRandom(seed));
                swordDry += r.AttackerActions == 0 ? 0 : r.AttackerBasicStrikes * 100.0 / r.AttackerActions;
                fistDry += r.DefenderActions == 0 ? 0 : r.DefenderBasicStrikes * 100.0 / r.DefenderActions;
            }

            // ⚠⚠ 검 쪽이 한 번도 마르지 않았다면 권의 우위를 잴 대상 자체가 없다.
            //   2026-08-01 실측이 그 상태였고, 그래서 **권의 유형 특성이 0 을 100% 깎는 꼴**이 되어
            //   유형 승률에서 권이 최하위(38.4%)로 떨어졌다. 통과로 위장하지 않는다.
            if (swordDry / Seeds <= 0.05)
            {
                Assert.Inconclusive(
                    "검 쪽이 한 번도 평타로 내려앉지 않아 권의 기력 우위를 판정할 수 없다 — "
                    + "기력 압력이 0 이면 권(拳)의 유형 특성은 존재하지 않는 것과 같다.");
            }

            Assert.That(fistDry / Seeds, Is.LessThanOrEqualTo(swordDry / Seeds),
                "권 숙달의 기력 소모 감소가 전락률에 나타나지 않는다");
        }

        private static Combatant FistFighter(string artName)
        {
            MartialArt art = MartialArtFactory.Create(
                "f_" + artName, artName, ArtKind.Attack, ArtTier.Major,
                Discipline.Fist, Alignment.Orthodox, "화산파");

            int sessions = AlignmentCurve.SessionsToReach(
                Alignment.Orthodox, MartialStage.ProficiencyForStage(Stage));

            return new Combatant(artName, CharacterStats.MaxLevel(),
                new List<LearnedArt> { new LearnedArt(art, sessions, Alignment.Orthodox) },
                new List<DisciplineMastery>
                {
                    new DisciplineMastery(Discipline.Fist, DisciplineCurve.SessionsToMaster(Discipline.Fist)),
                });
        }
    }
}
