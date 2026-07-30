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
    /// **형태소에서 유도된 무공으로 실제 전투를 돌린다.** 설계안 §4 1단계의 마지막 확인이다.
    ///
    /// ⚠⚠ 여기까지 와야 형태소 체계가 "돈다" 고 말할 수 있다. 사전·파서·조합 규칙·팩터리를
    ///   차례로 만들었지만, **엔진이 그 수치를 읽고 피해를 내는지**는 이 파일이 처음 확인한다.
    ///
    /// ⚠ 능력치는 정의서 §1-1 을 쓴다(체력 100 · 기력 50 · 공격 1 · 방어 1).
    ///   레거시 카탈로그가 쓰던 공격 20 · 위력 22~28 스케일과 **완전히 다르다** —
    ///   형태소 공격 합은 최대 5 이므로 캐릭터 기본값도 정의서대로 1 이어야 균형이 맞는다.
    /// </summary>
    public class MorphemeCombatTests
    {
        /// <summary>정의서 §1-1 의 기본 능력치. ⚠ 미검증 초기값이다.</summary>
        private static CharacterStats SpecStats()
        {
            return new CharacterStats(maxHealth: 100, maxQi: 50, attack: 1, defense: 1, agility: 1);
        }

        private static Combatant Fighter(string name, string artName, ArtTier tier,
            Discipline discipline, Alignment alignment, string school, int sessions)
        {
            MartialArt art = MartialArtFactory.Create(
                "t_" + artName, artName, ArtKind.Attack, tier, discipline, alignment, school);

            var arts = new List<LearnedArt> { new LearnedArt(art, sessions, alignment) };
            var masteries = new List<DisciplineMastery> { new DisciplineMastery(discipline, sessions) };
            return new Combatant(name, SpecStats(), arts, masteries);
        }

        [Test]
        public void 형태소_무공으로_전투가_성립한다()
        {
            // 남궁세가 `정화참탈`(공격 5 — 이 체계의 최대치) vs 점창파 `쾌자탈`(공격 2)
            Combatant strong = Fighter("남궁", "정화참탈", ArtTier.Major, Discipline.Sword, Alignment.Orthodox, "남궁세가", 200);
            Combatant weak = Fighter("점창", "쾌자탈", ArtTier.Minor, Discipline.Sword, Alignment.Orthodox, "점창파", 200);

            CombatResult result = CombatResolver.Resolve(strong, weak, new XorShiftRandom(42u));

            Assert.AreNotEqual(CombatOutcome.Draw, result.Outcome,
                "전투가 끝나지 않았다 — 피해가 방어를 못 뚫는다는 뜻이다.");
            Assert.Greater(result.Log.Count, 0, "전투 로그가 비어 있다.");
        }

        [Test]
        public void 형태소_공격합이_큰_쪽이_이긴다()
        {
            // ⚠ 같은 성향·같은 유형·같은 수련이면 **형태소 공격 합만이 차이**다.
            //   정화참탈 = 정(+2) + 화(+1) + 참(+2) = 5 · 쾌자탈 = 쾌(0) + 자(+1.5) = 1.5
            //   이 테스트가 깨지면 엔진이 형태소 수치를 안 읽는 것이다.
            int strongWins = 0;
            for (uint seed = 1; seed <= 50; seed++)
            {
                Combatant strong = Fighter("남궁", "정화참탈", ArtTier.Major, Discipline.Sword, Alignment.Orthodox, "남궁세가", 200);
                Combatant weak = Fighter("점창", "쾌자탈", ArtTier.Minor, Discipline.Sword, Alignment.Orthodox, "점창파", 200);

                CombatResult r = CombatResolver.Resolve(strong, weak, new XorShiftRandom(seed));
                if (r.Outcome == CombatOutcome.AttackerWin) strongWins++;
            }

            Assert.Greater(strongWins, 25, "공격 합이 3배 이상인데 승률이 절반 이하다 — 형태소 수치가 반영되지 않았다.");
        }

        [Test]
        public void 수련할수록_강해진다()
        {
            // 성향 배율이 형태소 합에 곱해지는지 확인한다(정의서 §1-3).
            int trainedWins = 0;
            for (uint seed = 1; seed <= 50; seed++)
            {
                Combatant trained = Fighter("숙련", "정화참탈", ArtTier.Major, Discipline.Sword, Alignment.Orthodox, "남궁세가", 200);
                Combatant novice = Fighter("초심", "정화참탈", ArtTier.Major, Discipline.Sword, Alignment.Orthodox, "남궁세가", 0);

                CombatResult r = CombatResolver.Resolve(trained, novice, new XorShiftRandom(seed));
                if (r.Outcome == CombatOutcome.AttackerWin) trainedWins++;
            }

            Assert.Greater(trainedWins, 25, "같은 무공인데 수련 차이가 승부를 가르지 못한다.");
        }

        [Test]
        public void 레거시_무공과_형태소_무공이_같은_엔진에서_돈다()
        {
            // ⚠ 과도기 확인. 카탈로그가 138종으로 교체되기 전까지 둘이 공존한다.
            //   섞어도 예외 없이 돌아가야 이행이 안전하다.
            MartialArt legacy = MartialArt.Technique(
                "old", "옛검법", Discipline.Sword, Alignment.Orthodox, basePower: 25, qiCost: 8);

            var legacyFighter = new Combatant("레거시", SpecStats(),
                new List<LearnedArt> { new LearnedArt(legacy, 200) },
                new List<DisciplineMastery> { new DisciplineMastery(Discipline.Sword, 200) });

            Combatant derived = Fighter("형태소", "정화참탈", ArtTier.Major, Discipline.Sword, Alignment.Orthodox, "남궁세가", 200);

            CombatResult r = CombatResolver.Resolve(legacyFighter, derived, new XorShiftRandom(7u));
            Assert.Greater(r.Log.Count, 0);
        }
    }
}
