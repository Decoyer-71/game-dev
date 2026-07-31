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
    /// **방어 카테고리 4축이 엔진에 붙어 있는가** — 방어 · 회피 · 막기 · 반격 (2026-07-31 연결).
    ///
    /// ⚠⚠ 이 축들은 *"죽어 있는 줄도 몰랐다"*. 민감도표에 **방어 카테고리가 통째로 빠져 있어서**
    ///   12자가 아무 일도 안 하는 것이 측정에 잡히지 않았다. 측정하지 않는 축은 고장 나도 보이지 않는다.
    ///
    /// ⚠ 절대 수치를 박지 않는다(HANDOFF §7). 보는 것은 **부등호와 존재**다 —
    ///   "방어가 피해를 줄이는가", "막기가 로그에 보이는가", "반격이 형태소 없이는 안 터지는가".
    /// </summary>
    public class DefenseAxisTests
    {
        private const int Seeds = 200;

        /// <summary>⚠ **만렙 기준으로 잰다** (2026-07-31 사용자 확정).</summary>
        private static CharacterStats SpecStats()
        {
            return CharacterStats.MaxLevel();
        }

        /// <summary>⚠ 250 = 모든 축이 상한에 닿는 수련 횟수. 검(만일검)이 0.40/회로 가장 느리다.</summary>
        private static Combatant Fighter(string name, int sessions = 250)
        {
            MartialArt art = MartialArtFactory.Create(
                "d_" + name, name, ArtKind.Attack, ArtTier.Major,
                Discipline.Sword, Alignment.Orthodox, "화산파");

            return new Combatant(name, SpecStats(),
                new List<LearnedArt> { new LearnedArt(art, sessions, Alignment.Orthodox) },
                new List<DisciplineMastery> { new DisciplineMastery(Discipline.Sword, sessions) });
        }

        /// <summary>`attacker` 가 `defender` 를 때려 실제로 넣은 총 피해. 반격분은 세지 않는다.</summary>
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

        /// <summary>로그에 `mark` 가 들어간 줄의 수.</summary>
        private static int CountMark(Combatant a, Combatant b, string mark)
        {
            int hits = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(a, b, new XorShiftRandom(seed));
                IReadOnlyList<CombatLogEntry> log = r.Log;
                for (int i = 0; i < log.Count; i++)
                {
                    if (!string.IsNullOrEmpty(log[i].Note) && log[i].Note.IndexOf(mark) >= 0) hits++;
                }
            }
            return hits;
        }

        [Test]
        public void 방어_형태소가_받는_피해를_줄인다()
        {
            // ⚠⚠ 이 경로 자체가 2026-07-31 이전에는 없었다 — 엔진이 `Stats.Defense` 만 읽어서
            //   방어 형태소(방·거·항·어·호)가 갈 곳이 없었다.
            Combatant attacker = Fighter("참정");
            long plain = DamageDealt(attacker, Fighter("참정"));
            long guarded = DamageDealt(attacker, Fighter("참정방"));

            Assert.Less(guarded, plain,
                "방(防)을 익힌 쪽이 더 적게 맞지 않는다 — 무공이 방어를 올리는 경로가 끊겨 있다.");
        }

        [Test]
        public void 방어가_아무리_높아도_피해가_0이_되지_않는다()
        {
            // ⚠⚠ 뺄셈 공식을 버리고 비율 경감으로 간 이유가 여기 있다(2026-07-31 사용자 확정).
            //   뺄셈은 방어가 공격을 넘어서는 순간 피해를 0(=하한 1)으로 만들어 전투를 끝나지 않게 한다.
            //   실제로 뺄셈일 때 방(防) 하나로 **승률 100%** 가 나왔다.
            Combatant attacker = Fighter("참정", sessions: 0);       // 가장 약한 공격
            Combatant wall = Fighter("참정방", sessions: 200);        // 가장 두꺼운 방어

            Assert.Greater(DamageDealt(attacker, wall), 0,
                "방어가 높은 상대에게 피해가 전혀 안 들어간다 — 전투가 성립하지 않는다.");
        }

        [Test]
        public void 회피_형태소가_상대의_명중을_떨어뜨린다()
        {
            Combatant attacker = Fighter("참정");
            double plain = LandedRatio(attacker, Fighter("참정"));
            double slippery = LandedRatio(attacker, Fighter("참정피"));

            Assert.Less(slippery, plain,
                "피(避)를 익힌 쪽이 덜 맞지 않는다 — 형태소 회피가 명중 판정에 닿지 않는다는 뜻이다.");
        }

        /// <summary>시도 대비 명중 비율.</summary>
        private static double LandedRatio(Combatant attacker, Combatant defender)
        {
            long attempted = 0, landed = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(attacker, defender, new XorShiftRandom(seed));
                IReadOnlyList<CombatLogEntry> log = r.Log;
                for (int i = 0; i < log.Count; i++)
                {
                    CombatLogEntry e = log[i];
                    if (e.Kind != CombatLogKind.Action || e.ActorName != attacker.Name) continue;
                    attempted += e.AttemptedHits;
                    landed += e.LandedHits;
                }
            }
            Assert.Greater(attempted, 0, "타격 표본이 없다.");
            return (double)landed / attempted;
        }

        [Test]
        public void 막기는_로그에_보이고_받는_피해를_줄인다()
        {
            // ⚠ 보이지 않는 경감은 플레이어에게 "그냥 약한 무공" 으로 읽힌다(설계 §1 반증 조건 1).
            Combatant attacker = Fighter("참정");
            Assert.Greater(CountMark(attacker, Fighter("참정방"), "[막기]"), 0,
                "막기가 한 번도 로그에 안 나온다 — 판정 자체가 안 돌고 있다.");
            Assert.AreEqual(0, CountMark(attacker, Fighter("참정"), "[막기]"),
                "막기 형태소가 없는데 막았다 — 기본 막기확률은 0 이어야 한다(사파 편차 정체성).");
        }

        [Test]
        public void 반격은_형태소가_있어야_터진다()
        {
            Assert.Greater(CountMark(Fighter("참정"), Fighter("참정반"), "[반격]"), 0,
                "반(反)을 익혔는데 반격이 한 번도 안 터진다.");
            Assert.AreEqual(0, CountMark(Fighter("참정"), Fighter("참정"), "[반격]"),
                "반격 형태소가 없는데 반격이 터졌다 — 정의서 §1-1 에 반격 기본값은 없다.");
        }

        [Test]
        public void 반격은_기력을_쓰지_않는다()
        {
            // ⚠ 반격이 기력을 먹으면 "받아친다" 가 아니라 "한 번 더 친다" 가 된다 —
            //   그러면 기력 고갈 드라마와 얽혀 반격 무공만 이중으로 손해를 본다.
            Combatant attacker = Fighter("참정");
            Combatant counterer = Fighter("참정반");

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(attacker, counterer, new XorShiftRandom(seed));
                IReadOnlyList<CombatLogEntry> log = r.Log;
                for (int i = 0; i < log.Count; i++)
                {
                    CombatLogEntry e = log[i];
                    if (string.IsNullOrEmpty(e.Note) || e.Note.IndexOf("[반격]") < 0) continue;
                    Assert.AreEqual(0, e.QiSpent, "반격이 기력을 소모했다.");
                }
            }
        }

        [Test]
        public void 반격은_맞은_직후에만_일어난다()
        {
            // ⚠⚠ 발동 조건이 **피격**이라는 것이 사용자 확정 사항이다(2026-07-31).
            //   막기 성공에 매달면 막기·반격이 같은 카테고리라 반격 형태소가 죽는다.
            Combatant attacker = Fighter("참정");
            Combatant counterer = Fighter("참정반");

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(attacker, counterer, new XorShiftRandom(seed));
                IReadOnlyList<CombatLogEntry> log = r.Log;
                for (int i = 0; i < log.Count; i++)
                {
                    CombatLogEntry e = log[i];
                    if (string.IsNullOrEmpty(e.Note) || e.Note.IndexOf("[반격]") < 0) continue;

                    Assert.Greater(i, 0, "반격이 전투 첫 줄에 나왔다 — 맞기 전에 받아친 셈이다.");
                    CombatLogEntry before = log[i - 1];
                    Assert.AreEqual(CombatLogKind.Action, before.Kind, "반격 직전 줄이 공격 행동이 아니다.");
                    Assert.AreEqual(attacker.Name, before.ActorName, "반격이 상대 공격 직후가 아니다.");
                    Assert.Greater(before.LandedHits, 0, "빗나간 공격에 반격이 터졌다.");
                }
            }
        }
    }
}
