using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jianghu.Core.Characters;
using Jianghu.Core.Combat;
using Jianghu.Core.Martial;
using Jianghu.Core.Rng;
using NUnit.Framework;

namespace Jianghu.Tests.Combat
{
    /// <summary>
    /// 전투 해결기 검증.
    ///
    /// 가장 중요한 것은 **결정론**이다. 같은 조합 × 같은 시드가 항상 같은 결과를 내야
    /// Phase 2 의 승률표를 뽑을 수 있고, 그래야 "지배 전략이 있는가"(반증 조건 2)를
    /// 사람 눈이 아니라 숫자로 판정할 수 있다.
    /// </summary>
    public class CombatTests
    {
        // ── 시험용 무공. 수치는 미검증 초기값이며 밸런스가 아니라 '성격 차이' 확인이 목적이다 ──
        private static MartialArt Sword(Alignment a = Alignment.Orthodox)
            => MartialArt.Technique("sword", "기본검법", Discipline.Sword, a, basePower: 20, qiCost: 6, hitCount: 1, accuracyBonus: 10);

        private static MartialArt Blade(Alignment a = Alignment.Orthodox)
            => MartialArt.Technique("blade", "패도", Discipline.Blade, a, basePower: 32, qiCost: 8, hitCount: 1, accuracyBonus: -10);

        private static MartialArt Fist(Alignment a = Alignment.Orthodox)
            => MartialArt.Technique("fist", "연환권", Discipline.Fist, a, basePower: 20, qiCost: 4, hitCount: 3, accuracyBonus: 5);

        private static MartialArt Inner(Alignment a = Alignment.Orthodox)
            => MartialArt.Support("inner", "심법", Discipline.InnerArt, a, maxQiBonus: 20, powerBonusPercent: 15);

        private static MartialArt Steps(Alignment a = Alignment.Orthodox)
            => MartialArt.Support("steps", "보법", Discipline.Movement, a, evasionBonus: 8, initiativeBonus: 6);

        private static CharacterStats BaseStats(
            int health = 120, int qi = 40, int attack = 20, int defense = 10, int agility = 12)
            => new CharacterStats(health, qi, attack, defense, agility);

        private static Combatant Fighter(string name, CharacterStats stats, params LearnedArt[] arts)
            => new Combatant(name, stats, arts.ToList());

        private static LearnedArt Learned(MartialArt art, int sessions = 0)
            => new LearnedArt(art, sessions);

        private static string Serialize(CombatResult r)
        {
            var sb = new StringBuilder();
            sb.Append(r.Outcome).Append('|').Append(r.Turns).Append('|').Append(r.WinnerName).Append('\n');
            foreach (CombatLogEntry e in r.Log) sb.Append(e).Append('\n');
            return sb.ToString();
        }

        // ────────────────────────────── 결정론 ──────────────────────────────

        [Test]
        public void 같은_시드는_같은_전투_결과를_낸다()
        {
            Combatant a = Fighter("검객", BaseStats(), Learned(Sword(), 100));
            Combatant b = Fighter("도객", BaseStats(), Learned(Blade(), 100));

            CombatResult first = CombatResolver.Resolve(a, b, new XorShiftRandom(9001u));
            CombatResult second = CombatResolver.Resolve(a, b, new XorShiftRandom(9001u));

            Assert.AreEqual(Serialize(first), Serialize(second));
        }

        [Test]
        public void 전투는_입력_Combatant_를_변형하지_않는다()
        {
            // 같은 객체를 재사용해도 결과가 같아야 승률표를 뽑을 수 있다.
            Combatant a = Fighter("검객", BaseStats(), Learned(Sword(), 100), Learned(Inner()));
            Combatant b = Fighter("권객", BaseStats(), Learned(Fist(), 100));

            int qiBefore = a.EffectiveMaxQi;
            CombatResult r1 = CombatResolver.Resolve(a, b, new XorShiftRandom(7u));
            CombatResult r2 = CombatResolver.Resolve(a, b, new XorShiftRandom(7u));

            Assert.AreEqual(qiBefore, a.EffectiveMaxQi, "전투가 Combatant 의 상태를 바꿨다");
            Assert.AreEqual(Serialize(r1), Serialize(r2));
        }

        [Test]
        public void 시드가_다르면_전개가_달라질_수_있다()
        {
            Combatant a = Fighter("검객", BaseStats(), Learned(Sword(), 60));
            Combatant b = Fighter("도객", BaseStats(), Learned(Blade(), 60));

            var seen = new HashSet<string>();
            for (uint seed = 1; seed <= 40; seed++)
            {
                seen.Add(Serialize(CombatResolver.Resolve(a, b, new XorShiftRandom(seed))));
            }

            Assert.Greater(seen.Count, 1, "40개 시드가 전부 같은 전개를 냈다 — 난수가 전투에 반영되지 않는다");
        }

        // ────────────────────────────── 결말 ──────────────────────────────

        [Test]
        public void 압도적으로_강한_쪽이_이긴다()
        {
            Combatant strong = Fighter("고수", BaseStats(health: 400, attack: 80, defense: 30), Learned(Sword(), 200));
            Combatant weak = Fighter("낭인", BaseStats(health: 50, attack: 3, defense: 0));

            for (uint seed = 1; seed <= 20; seed++)
            {
                CombatResult r = CombatResolver.Resolve(strong, weak, new XorShiftRandom(seed));
                Assert.AreEqual(CombatOutcome.AttackerWin, r.Outcome, "시드 {0} 에서 고수가 못 이겼다", seed);
                Assert.AreEqual("고수", r.WinnerName);
            }
        }

        [Test]
        public void 서로_피해를_거의_못_주면_무승부로_끝난다()
        {
            // 방어가 극단적으로 높으면 최소 피해 1 만 들어간다 → 최대 턴에서 잘린다.
            Combatant a = Fighter("갑", BaseStats(health: 300, attack: 5, defense: 200));
            Combatant b = Fighter("을", BaseStats(health: 300, attack: 5, defense: 200));

            CombatResult r = CombatResolver.Resolve(a, b, new XorShiftRandom(1u), maxTurns: 50);

            Assert.AreEqual(CombatOutcome.Draw, r.Outcome);
            Assert.AreEqual(50, r.Turns);
            Assert.IsNull(r.WinnerName);
        }

        [Test]
        public void 최대_턴을_넘기지_않는다()
        {
            Combatant a = Fighter("갑", BaseStats(health: 9999, attack: 1, defense: 500));
            Combatant b = Fighter("을", BaseStats(health: 9999, attack: 1, defense: 500));

            CombatResult r = CombatResolver.Resolve(a, b, new XorShiftRandom(1u), maxTurns: 7);

            Assert.AreEqual(7, r.Turns);
        }

        // ────────────────────────────── 기력 ──────────────────────────────

        [Test]
        public void 기력이_마르면_평타로_전환된다()
        {
            // 기력 6 = 검법 1회분. 두 번째 턴부터는 평타여야 한다.
            Combatant a = Fighter("검객", BaseStats(health: 200, qi: 6), Learned(Sword()));
            Combatant b = Fighter("허수아비", BaseStats(health: 400, attack: 0, defense: 0));

            CombatResult r = CombatResolver.Resolve(a, b, new XorShiftRandom(5u));

            List<CombatLogEntry> mine = r.Log.Where(e => e.ActorName == "검객").ToList();
            Assert.AreEqual("기본검법", mine[0].ArtName, "첫 턴에 검법을 못 썼다");
            Assert.IsTrue(mine.Skip(1).All(e => e.ArtName == "평타"),
                "기력이 마른 뒤에도 초식을 쓰고 있다");
        }

        [Test]
        public void 보조무공만_익히면_평타로_싸운다()
        {
            Combatant a = Fighter("내공수련자", BaseStats(), Learned(Inner()), Learned(Steps()));
            Combatant b = Fighter("허수아비", BaseStats(health: 300, attack: 0, defense: 0));

            CombatResult r = CombatResolver.Resolve(a, b, new XorShiftRandom(3u));

            Assert.IsTrue(r.Log.Where(e => e.ActorName == "내공수련자").All(e => e.ArtName == "평타"));
        }

        // ────────────────────────────── 보조 무공 효과 ──────────────────────────────

        [Test]
        public void 내공은_최대_기력과_초식_위력을_올린다()
        {
            Combatant plain = Fighter("맨몸", BaseStats(), Learned(Sword()));
            Combatant withInner = Fighter("심법", BaseStats(), Learned(Sword()), Learned(Inner()));

            Assert.AreEqual(40, plain.EffectiveMaxQi);
            Assert.AreEqual(60, withInner.EffectiveMaxQi, "내공이 최대 기력을 안 올렸다");

            Assert.AreEqual(0, plain.PowerBonusPercent);
            Assert.AreEqual(15, withInner.PowerBonusPercent, "내공이 위력 보너스를 안 줬다");
        }

        [Test]
        public void 경공은_회피와_선공을_올린다()
        {
            Combatant plain = Fighter("맨몸", BaseStats(), Learned(Sword()));
            Combatant withSteps = Fighter("보법", BaseStats(), Learned(Sword()), Learned(Steps()));

            Assert.AreEqual(6, plain.Evasion);            // 신법 12 의 절반
            Assert.AreEqual(14, withSteps.Evasion);       // + 경공 8

            Assert.AreEqual(12, plain.Initiative);
            Assert.AreEqual(18, withSteps.Initiative);    // + 경공 6
        }

        [Test]
        public void 선공은_경공이_높은_쪽이_가져간다()
        {
            Combatant fast = Fighter("쾌속", BaseStats(), Learned(Sword()), Learned(Steps()));
            Combatant slow = Fighter("둔중", BaseStats(), Learned(Sword()));

            // 느린 쪽을 첫 인자로 넣어도, 로그의 첫 행동자는 빠른 쪽이어야 한다.
            CombatResult r = CombatResolver.Resolve(slow, fast, new XorShiftRandom(11u));

            Assert.AreEqual("쾌속", r.Log[0].ActorName);
        }

        // ────────────────────── 가설 검증 — 조합이 전투를 바꾸는가 ──────────────────────

        [Test]
        public void 다단_초식과_단타_초식은_전투_기록이_다르다()
        {
            // 반증 조건 1 대응 — "무공 조합을 바꿔도 차이를 체감할 수 없다" 를 막는다.
            Combatant fistUser = Fighter("권객", BaseStats(), Learned(Fist(), 100));
            Combatant bladeUser = Fighter("도객", BaseStats(), Learned(Blade(), 100));
            Combatant dummy = Fighter("허수아비", BaseStats(health: 500, attack: 0, defense: 0));

            CombatResult fistFight = CombatResolver.Resolve(fistUser, dummy, new XorShiftRandom(21u));
            CombatResult bladeFight = CombatResolver.Resolve(bladeUser, dummy, new XorShiftRandom(21u));

            // ⚠ 로그 전체가 아니라 '초식을 쓴 항목'만 본다.
            //   기력이 마르면 평타(단타)로 전환되는 것이 의도된 동작이기 때문이다.
            List<CombatLogEntry> fistMoves = fistFight.Log.Where(e => e.ArtName == "연환권").ToList();
            List<CombatLogEntry> bladeMoves = bladeFight.Log.Where(e => e.ArtName == "패도").ToList();

            Assert.IsNotEmpty(fistMoves, "권법을 한 번도 쓰지 않았다");
            Assert.IsNotEmpty(bladeMoves, "도법을 한 번도 쓰지 않았다");
            Assert.IsTrue(fistMoves.All(e => e.AttemptedHits == 3), "권법이 다단 타격으로 기록되지 않았다");
            Assert.IsTrue(bladeMoves.All(e => e.AttemptedHits == 1), "도법이 단타로 기록되지 않았다");
            Assert.AreNotEqual(Serialize(fistFight), Serialize(bladeFight),
                "서로 다른 무공인데 전투 기록이 동일하다");
        }

        [Test]
        public void 다단_초식은_부분_명중이_일어난다()
        {
            // 단타는 '전부 아니면 전무'지만 다단은 그 사이가 있다 — 이게 분산 차이의 실체다.
            Combatant fistUser = Fighter("권객", BaseStats(), Learned(Fist(), 100));
            Combatant evasive = Fighter("경공고수", BaseStats(health: 900, attack: 0, agility: 60), Learned(Steps()));

            bool sawPartial = false;
            for (uint seed = 1; seed <= 30 && !sawPartial; seed++)
            {
                CombatResult r = CombatResolver.Resolve(fistUser, evasive, new XorShiftRandom(seed));
                sawPartial = r.Log.Any(e => e.ActorName == "권객" && e.LandedHits > 0 && e.LandedHits < e.AttemptedHits);
            }

            Assert.IsTrue(sawPartial, "30개 시드에서 부분 명중이 한 번도 없었다");
        }

        [Test]
        public void 숙련도가_높으면_더_빨리_이긴다()
        {
            Combatant novice = Fighter("초심자", BaseStats(), Learned(Sword(), 0));
            Combatant master = Fighter("숙련자", BaseStats(), Learned(Sword(), 200));
            Combatant dummy = Fighter("허수아비", BaseStats(health: 400, attack: 0, defense: 0));

            CombatResult noviceFight = CombatResolver.Resolve(novice, dummy, new XorShiftRandom(33u));
            CombatResult masterFight = CombatResolver.Resolve(master, dummy, new XorShiftRandom(33u));

            Assert.Less(masterFight.Turns, noviceFight.Turns,
                "숙련 200회가 0회보다 빠르지 않다 (숙련 {0}턴 vs 초심 {1}턴)",
                masterFight.Turns, noviceFight.Turns);
        }

        // ────────────────── 성향이 전투까지 전달되는가 ──────────────────
        //
        // 곡선 단위 테스트(AlignmentCurveTests)와 별개로, 그 곡선이 실제 전투 수치까지
        // 도달하는지를 본다. 턴 수는 정수라 미세한 차이를 못 잡으므로 **총 피해 평균**으로 측정한다.

        /// <summary>허수아비를 고정 턴 동안 때려 나온 초식 피해 표본을 모은다.</summary>
        private static List<int> SampleTechniqueDamage(Alignment alignment, int sessions, int seeds)
        {
            Combatant fighter = Fighter("무인", BaseStats(), Learned(Sword(alignment), sessions));
            Combatant dummy = Fighter("허수아비", BaseStats(health: 100000, attack: 0, defense: 0, agility: 0));

            var samples = new List<int>();
            for (uint seed = 1; seed <= seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(fighter, dummy, new XorShiftRandom(seed), maxTurns: 12);
                // ⚠ 평타는 성향이 정파로 고정이라 표본을 오염시킨다. 초식만 센다.
                samples.AddRange(r.Log
                    .Where(e => e.ActorName == "무인" && e.ArtName == "기본검법" && e.LandedHits > 0)
                    .Select(e => e.Damage));
            }
            return samples;
        }

        [Test]
        public void 초반에는_사파가_전투에서도_더_강하다()
        {
            const int sessions = 100;

            double orthodox = SampleTechniqueDamage(Alignment.Orthodox, sessions, 40).Average();
            double unorthodox = SampleTechniqueDamage(Alignment.Unorthodox, sessions, 40).Average();

            Assert.Greater(unorthodox, orthodox,
                "수련 {0}회 시점에서 사파 평균 피해가 정파보다 낮다 (사파 {1:F1} vs 정파 {2:F1})",
                sessions, unorthodox, orthodox);
        }

        [Test]
        public void 후반에는_마도가_전투에서도_압도한다()
        {
            const int sessions = 250;

            double orthodox = SampleTechniqueDamage(Alignment.Orthodox, sessions, 40).Average();
            double demonic = SampleTechniqueDamage(Alignment.Demonic, sessions, 40).Average();

            Assert.Greater(demonic, orthodox,
                "수련 {0}회 시점에서 마도 평균 피해가 정파보다 낮다 (마도 {1:F1} vs 정파 {2:F1})",
                sessions, demonic, orthodox);
        }

        // ────────────── 사파의 존재 이유 — 보장된 저점 (2026-07-28 재설계) ──────────────

        [Test]
        public void 사파는_피해_편차가_가장_좁고_마도가_가장_넓다()
        {
            // 숙련 0 에서는 세 성향의 위력 배율이 모두 1.0 이라 기본 피해가 같다.
            // 따라서 여기서 벌어지는 차이는 **오직 변동폭 때문**이다 — 변수를 분리한 측정이다.
            List<int> orthodox = SampleTechniqueDamage(Alignment.Orthodox, 0, 40);
            List<int> unorthodox = SampleTechniqueDamage(Alignment.Unorthodox, 0, 40);
            List<int> demonic = SampleTechniqueDamage(Alignment.Demonic, 0, 40);

            int SpreadOf(List<int> xs) => xs.Max() - xs.Min();

            Assert.Less(SpreadOf(unorthodox), SpreadOf(orthodox),
                "사파 편차가 정파보다 넓다 (사파 {0} vs 정파 {1})", SpreadOf(unorthodox), SpreadOf(orthodox));
            Assert.Less(SpreadOf(orthodox), SpreadOf(demonic),
                "정파 편차가 마도보다 넓다 (정파 {0} vs 마도 {1})", SpreadOf(orthodox), SpreadOf(demonic));
        }

        [Test]
        public void 사파는_최저_피해가_가장_높다()
        {
            // **이것이 "보장된 저점"의 실체다.** 사파를 고를 명분이 여기서 나온다.
            int orthodoxWorst = SampleTechniqueDamage(Alignment.Orthodox, 0, 40).Min();
            int unorthodoxWorst = SampleTechniqueDamage(Alignment.Unorthodox, 0, 40).Min();
            int demonicWorst = SampleTechniqueDamage(Alignment.Demonic, 0, 40).Min();

            Assert.Greater(unorthodoxWorst, orthodoxWorst,
                "사파 최저 피해가 정파보다 낮다 (사파 {0} vs 정파 {1})", unorthodoxWorst, orthodoxWorst);
            Assert.Greater(orthodoxWorst, demonicWorst,
                "정파 최저 피해가 마도보다 낮다 (정파 {0} vs 마도 {1})", orthodoxWorst, demonicWorst);
        }

        [Test]
        public void 마도는_최고_피해가_가장_높다()
        {
            // 저점을 내준 대가로 마도는 터질 때 가장 크게 터진다.
            int orthodoxBest = SampleTechniqueDamage(Alignment.Orthodox, 0, 40).Max();
            int unorthodoxBest = SampleTechniqueDamage(Alignment.Unorthodox, 0, 40).Max();
            int demonicBest = SampleTechniqueDamage(Alignment.Demonic, 0, 40).Max();

            Assert.Greater(demonicBest, orthodoxBest);
            Assert.Greater(orthodoxBest, unorthodoxBest);
        }

        [Test]
        public void 사파는_전투_결과가_가장_일정하다()
        {
            // 편차가 좁다는 것은 결국 "몇 턴에 끝나는지가 예측된다" 는 뜻이어야 한다.
            // 이게 성립해야 나중에 비무대회에서 "확실한 성적" 이라는 선택지가 생긴다.
            int SpreadOfTurns(Alignment alignment)
            {
                Combatant f = Fighter("무인", BaseStats(), Learned(Sword(alignment), 0));
                Combatant target = Fighter("표적", BaseStats(health: 600, attack: 0, defense: 0, agility: 0));

                var turns = new List<int>();
                for (uint seed = 1; seed <= 60; seed++)
                {
                    turns.Add(CombatResolver.Resolve(f, target, new XorShiftRandom(seed), maxTurns: 200).Turns);
                }
                return turns.Max() - turns.Min();
            }

            int unorthodox = SpreadOfTurns(Alignment.Unorthodox);
            int demonic = SpreadOfTurns(Alignment.Demonic);

            Assert.Less(unorthodox, demonic,
                "사파 턴 수 편차가 마도보다 크다 (사파 {0} vs 마도 {1})", unorthodox, demonic);
        }
    }
}
