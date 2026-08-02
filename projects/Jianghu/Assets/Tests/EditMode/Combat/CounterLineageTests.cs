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
    /// **상성(相性)이 엔진에 붙어 있는가** — 정의서 §4 (2026-08-02 연결).
    ///
    /// ⚠⚠ 이 축은 **파서까지만 살아 있었다.** `MorphemeParser` 가 `CounterTargets` 를 정확히
    ///   만들어 놓았는데 `MartialArtFactory` 가 그것을 버려서 `MartialArt` 에 실리지도 않았다 —
    ///   `AttackScope`(범위)와 똑같은 형태의 누락이다. 인계문서 §3-2 는 미연결 축을
    ///   *"2026-08-01 전수 확인 결과 정확히 셋"* 이라고 적었는데 **상성을 빠뜨려 실제로는 넷**이었다.
    ///
    /// ⚠⚠ 이 누락에는 대가가 있었다. 정의서 §2-2 예외가 *"상성 무공은 부정·무학분류 2자가 수치 0이라
    ///   **위력을 크게 포기한 구조**"* 라며 무공형태 필수를 면제해 줬는데, **포기의 대가로 받기로 한
    ///   상성이 없어서 순손실**이었다. 실제로 `창천낙월` 은 소문파 최하위권(43.8%)이었다.
    ///
    /// ⚠ 절대 수치를 박지 않는다(HANDOFF §7). 보는 것은 **부등호와 존재**다.
    ///   상성 1당 +10%/−5% 라는 값 자체는 정의서에 **미검증 초기값**이라 적혀 있고, 여기서 굳히면
    ///   나중에 조정할 때 테스트가 먼저 깨져 조정을 막는다.
    ///
    /// ⚠⚠ **대조 방식이 요점이다.** 무공을 바꿔 비교하면 상성 외의 수치까지 같이 움직인다
    ///   (부정·무학분류 2자를 빼면 글자 수가 달라져 계층·기력이 통째로 바뀐다).
    ///   그래서 **같은 무공·같은 능력치를 두고 방어자의 무학분류만 바꾼다.** 상성만 움직인다.
    /// </summary>
    public class CounterLineageTests
    {
        private const int Seeds = 200;

        /// <summary>낙월(落月) = 달을 떨어뜨린다 → **음기무학**에 상성 +1 (정의서 §4).</summary>
        private const string CounterArt = "창천낙월";

        /// <summary>상성이 없는 같은 계층·같은 유형의 무공. `창천낙월` 과 성능 형태소 수(3)가 같다.</summary>
        private const string PlainArt = "쾌자탈";

        private static Combatant Fighter(string artName, ArtLineage? lineage, string school = "점창파")
        {
            MartialArt art = MartialArtFactory.Create(
                "c_" + artName, artName, ArtKind.Attack, ArtTier.Minor,
                Discipline.Sword, Alignment.Orthodox, school);

            int sessions = AlignmentCurve.SessionsToReach(
                Alignment.Orthodox, MartialStage.ProficiencyForStage(MartialStage.MaxStage));

            return new Combatant(artName, CharacterStats.MaxLevel(),
                new List<LearnedArt> { new LearnedArt(art, sessions, Alignment.Orthodox) },
                new List<DisciplineMastery>
                {
                    new DisciplineMastery(Discipline.Sword, DisciplineCurve.SessionsToMaster(Discipline.Sword)),
                },
                lineage);
        }

        /// <summary>`attacker` 가 `defender` 에게 넣은 총 피해. 반격분은 세지 않는다.</summary>
        private static long DamageDealt(Combatant attacker, Combatant defender)
        {
            long total = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(attacker, defender, new XorShiftRandom(seed));
                IReadOnlyList<CombatLogEntry> log = r.Log;
                for (int i = 0; i < log.Count; i++)
                {
                    CombatLogEntry e = log[i];
                    if (e.Kind != CombatLogKind.Action) continue;
                    if (e.ActorName != attacker.Name) continue;
                    if (!string.IsNullOrEmpty(e.Note) && e.Note.IndexOf("[반격]") >= 0) continue;
                    total += e.Damage;
                }
            }
            return total;
        }

        // ─────────────────────────── 데이터가 실제로 실리는가 ───────────────────────────

        [Test]
        public void 상성_무공은_대상_분류를_싣고_온다()
        {
            MartialArt art = MartialArtFactory.Create(
                "c_낙월", CounterArt, ArtKind.Attack, ArtTier.Minor,
                Discipline.Sword, Alignment.Orthodox, "점창파");

            // 낙(落) 바로 뒤에 월(月) → 음기무학에 상성 +1
            CollectionAssert.Contains(art.CounterTargets, ArtLineage.Yin,
                "낙월인데 음기무학 상성이 실리지 않았다 — 팩토리가 또 버리고 있다.");
            Assert.AreEqual(1, art.CounterTargets.Count, "상성이 하나만 있어야 한다.");
        }

        [Test]
        public void 상성이_없는_무공은_대상_분류가_비어_있다()
        {
            MartialArt art = MartialArtFactory.Create(
                "c_평범", PlainArt, ArtKind.Attack, ArtTier.Minor,
                Discipline.Sword, Alignment.Orthodox, "점창파");

            Assert.AreEqual(0, art.CounterTargets.Count,
                "부정+무학분류가 없는 무공에 상성이 생겼다.");
        }

        [Test]
        public void 카탈로그의_상성_무공은_넷이다()
        {
            // ⚠ 이 숫자를 박아 두는 이유 — 무공명이 곧 데이터라 **이름 한 글자가 바뀌면 상성이
            //   조용히 생기거나 사라진다.** 승률 테스트는 그것을 정상값으로 통과시킨다
            //   (HANDOFF §4-2-X ④ 오탈자 감사와 같은 취지).
            var withCounter = new List<string>();
            IReadOnlyList<MartialArt> all = MartialArtCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].CounterTargets.Count > 0) withCounter.Add(all[i].Name);
            }

            CollectionAssert.AreEquivalent(
                new[] { "창천낙월", "참천멸월", "절해망혼", "절지낙월" }, withCounter,
                "상성 무공의 구성이 바뀌었다. 이름이 바뀌었거나 사전이 바뀌었다.");
        }

        // ─────────────────────────── 전투에서 실제로 일하는가 ───────────────────────────

        [Test]
        public void 상성_대상을_때리면_피해가_는다()
        {
            Combatant attacker = Fighter(CounterArt, ArtLineage.Yang);

            long vsTargeted = DamageDealt(attacker, Fighter(PlainArt, ArtLineage.Yin));    // 낙월이 겨누는 분류
            long vsOther = DamageDealt(attacker, Fighter(PlainArt, ArtLineage.Mixed));     // 안 겨누는 분류

            Assert.Greater(vsTargeted, vsOther,
                "음기무학을 겨누는 낙월이 음기 상대에게 더 아프지 않다 — 상성이 피해에 닿지 않았다.");
        }

        [Test]
        public void 무소속에게는_상성이_성립하지_않는다()
        {
            // 정의서 §6-4 — 분류가 정해지지 않은 상대는 찌를 곳이 없다.
            Combatant attacker = Fighter(CounterArt, ArtLineage.Yang);

            long vsUnaffiliated = DamageDealt(attacker, Fighter(PlainArt, null));
            long vsOther = DamageDealt(attacker, Fighter(PlainArt, ArtLineage.Mixed));

            Assert.AreEqual(vsOther, vsUnaffiliated,
                "무소속 상대에게 상성이 걸렸다 — 과녁이 없는데 명중한 셈이다.");
        }

        [Test]
        public void 상성이_없는_무공은_상대_분류가_달라도_피해가_같다()
        {
            // ⚠ 이게 없으면 위 두 테스트가 "분류를 바꾸면 뭐든 달라진다" 로도 통과한다.
            Combatant attacker = Fighter(PlainArt, ArtLineage.Yang);

            long vsYin = DamageDealt(attacker, Fighter(PlainArt, ArtLineage.Yin));
            long vsMixed = DamageDealt(attacker, Fighter(PlainArt, ArtLineage.Mixed));

            Assert.AreEqual(vsMixed, vsYin,
                "상성이 없는 무공인데 상대 분류가 피해를 바꿨다 — 분류가 다른 축을 오염시키고 있다.");
        }

        [Test]
        public void 방어_상성은_받는_피해를_줄인다()
        {
            // 방어자가 `창천낙월`(음기무학 상성)을 익히고 있으면, **음기무학 공격자**에게 덜 맞는다.
            // ⚠ 방어 쪽은 익힌 무공 전부를 합산한다(`Combatant.CounterCountAgainst`) —
            //   받는 피해 감소는 어느 초식을 쓰는 중인지와 무관한 상시 성질이기 때문이다.
            Combatant defender = Fighter(CounterArt, ArtLineage.Yang);

            long fromCountered = DamageDealt(Fighter(PlainArt, ArtLineage.Yin), defender);
            long fromOther = DamageDealt(Fighter(PlainArt, ArtLineage.Mixed), defender);

            Assert.Less(fromCountered, fromOther,
                "음기무학 상성을 가진 방어자가 음기 공격자에게 그대로 맞는다 — 받는 피해 감소가 안 붙었다.");
        }
    }
}
