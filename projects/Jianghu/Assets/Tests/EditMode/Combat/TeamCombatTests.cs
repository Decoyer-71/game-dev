using System.Collections.Generic;
using System.Text;
using Jianghu.Core.Characters;
using Jianghu.Core.Combat;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;
using Jianghu.Core.Rng;
using NUnit.Framework;

namespace Jianghu.Tests.Combat
{
    /// <summary>
    /// **다대다 전투** — <see cref="CombatResolver.ResolveTeams"/> 검증 (2026-08-09 신설).
    ///
    /// 설계는 <c>docs/multi-combat-plan.md</c> 이고 이 파일은 그 §4 의 **2·3단계**를 건다:
    /// 경합 루프 · 이니셔티브 큐 · 범위(<see cref="AttackScope"/>) 대상 수 · 승패 판정,
    /// 그리고 **진형**(<see cref="BattleRow"/> — 열 성향 판정과 우선 열 타겟팅).
    ///
    /// ⚠ 진형이 변수가 아닌 시험은 <c>Team(...)</c> 로 **전원을 전열**에 세운다.
    ///   열을 재는 시험만 <c>Line(BattlePlacement.Front/Rear(...))</c> 로 배치를 명시한다.
    ///
    /// ⚠ 절대 수치를 박지 않는다(HANDOFF §7) — 보는 것은 **개수·순서·존재**다.
    ///   피해량·승률은 여기서 판정하지 않는다. 그건 Sandbox 측정의 일이다.
    /// </summary>
    public class TeamCombatTests
    {
        // ───────────────────────────── 시험용 부품 ─────────────────────────────
        //
        // ⚠ 수치는 밸런스가 아니라 **관측 조건**을 만들기 위한 값이다.
        //   양 팀의 민첩을 같게 두어 속공 추가 행동을 끄고, 체력을 크게 잡아
        //   "누가 몇 명을 때렸는가" 가 죽음으로 흐려지지 않게 한다.

        private static CharacterStats Stats(
            int health = 5000, int qi = 40, int attack = 20, int defense = 10, int agility = 12)
            => new CharacterStats(health, qi, attack, defense, agility);

        private static Combatant Man(string name, CharacterStats stats, params LearnedArt[] arts)
            => new Combatant(name, stats, new List<LearnedArt>(arts));

        /// <summary>무공을 하나도 안 익힌 허수아비. 평타만 낸다.</summary>
        private static Combatant Dummy(string name, int health = 5000)
            => new Combatant(name, Stats(health: health), new List<LearnedArt>());

        private static LearnedArt Learned(MartialArt art, int sessions = 100)
            => new LearnedArt(art, sessions);

        /// <summary>범위·열을 지정한 평범한 검법. 기본은 단일 대상 · 전열 우선(근접)이다.</summary>
        private static MartialArt Sword(
            AttackScope scope = AttackScope.Single, int qiCost = 4, int basePower = 20,
            BattleRow row = BattleRow.Front)
            => MartialArt.Technique(
                "sword_" + scope + "_" + row, "범위검법", Discipline.Sword, Alignment.Orthodox,
                basePower: basePower, qiCost: qiCost, hitCount: 1, accuracyBonus: 10, school: null,
                scope: scope, preferredRow: row);

        /// <summary>후열을 먼저 치는 무공(던지기 자리). 수치는 검법과 같게 두어 **열만** 변수로 남긴다.</summary>
        private static MartialArt Thrown(AttackScope scope = AttackScope.Single, int qiCost = 4, int basePower = 20)
            => Sword(scope, qiCost, basePower, BattleRow.Rear);

        /// <summary>
        /// 형태소 유도 무공 — <see cref="ArtStatDelta"/> 와 <see cref="AbsoluteRule"/> 을 직접 지정한다.
        /// ⚠ 반격 확률과 절대경지 규칙은 **형태소 무공에만** 붙는다(<c>Combatant.CounterRate</c> 주석).
        ///   손수 만드는 <see cref="MartialArt.Technique"/> 로는 만들 수 없어 이 통로가 필요하다.
        /// </summary>
        private static MartialArt Derived(
            string name, ArtStatDelta delta, AbsoluteRule rule = AbsoluteRule.None,
            AttackScope scope = AttackScope.Single, int qiCost = 4)
            => MartialArt.FromMorphemes(
                "d_" + name, name, "점창파", Discipline.Sword, Alignment.Orthodox,
                delta, qiCost, ArtTier.Minor, hitCount: 1,
                counterTargets: null, rule: rule, scope: scope);

        /// <summary>전원을 **전열**에 세운다. 진형이 변수가 아닌 시험은 이걸 쓴다.</summary>
        private static List<BattlePlacement> Team(params Combatant[] members)
        {
            var placed = new List<BattlePlacement>(members.Length);
            for (int i = 0; i < members.Length; i++) placed.Add(BattlePlacement.Front(members[i]));
            return placed;
        }

        /// <summary>열을 직접 지정해 세운다.</summary>
        private static List<BattlePlacement> Line(params BattlePlacement[] placed)
            => new List<BattlePlacement>(placed);

        private static string Serialize(TeamCombatResult r)
        {
            var sb = new StringBuilder();
            sb.Append(r.Outcome).Append('|').Append(r.Rounds).Append('\n');
            for (int i = 0; i < r.TeamAHealthLeft.Count; i++) sb.Append(r.TeamAHealthLeft[i]).Append(',');
            for (int i = 0; i < r.TeamBHealthLeft.Count; i++) sb.Append(r.TeamBHealthLeft[i]).Append(',');
            sb.Append('\n');
            foreach (CombatLogEntry e in r.Log) sb.Append(e).Append('\n');
            return sb.ToString();
        }

        /// <summary>한 경합에서 <paramref name="actor"/> 가 실제로 때린 **서로 다른 대상**의 수.</summary>
        private static int TargetsStruck(TeamCombatResult r, string actor, int round)
        {
            var names = new HashSet<string>();
            foreach (CombatLogEntry e in r.Log)
            {
                if (e.Kind != CombatLogKind.Action || e.Turn != round || e.ActorName != actor) continue;
                if (IsCounter(e)) continue;
                names.Add(e.TargetName);
            }
            return names.Count;
        }

        private static bool IsCounter(CombatLogEntry e)
            => !string.IsNullOrEmpty(e.Note) && e.Note.IndexOf("[반격]") >= 0;

        // ───────────────────────────── 범위 — 대상 수 ─────────────────────────────

        [Test]
        public void 단일_무공은_한_명만_때린다()
        {
            var a = Team(Man("A1", Stats(), Learned(Sword(AttackScope.Single))));
            var b = Team(Dummy("B1"), Dummy("B2"), Dummy("B3"), Dummy("B4"));

            TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(11u), maxRounds: 1);

            Assert.AreEqual(1, TargetsStruck(r, "A1", 1));
        }

        [Test]
        public void 다多_무공은_두_명을_때린다()
        {
            var a = Team(Man("A1", Stats(), Learned(Sword(AttackScope.Two))));
            var b = Team(Dummy("B1"), Dummy("B2"), Dummy("B3"), Dummy("B4"));

            TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(11u), maxRounds: 1);

            Assert.AreEqual(2, TargetsStruck(r, "A1", 1));
        }

        [Test]
        public void 군群_무공은_세_명을_때린다()
        {
            var a = Team(Man("A1", Stats(), Learned(Sword(AttackScope.Three))));
            var b = Team(Dummy("B1"), Dummy("B2"), Dummy("B3"), Dummy("B4"));

            TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(11u), maxRounds: 1);

            Assert.AreEqual(3, TargetsStruck(r, "A1", 1));
        }

        [Test]
        public void 전全_무공은_살아_있는_전원을_때린다()
        {
            var a = Team(Man("A1", Stats(), Learned(Sword(AttackScope.All))));
            var b = Team(Dummy("B1"), Dummy("B2"), Dummy("B3"), Dummy("B4"));

            TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(11u), maxRounds: 1);

            Assert.AreEqual(4, TargetsStruck(r, "A1", 1));
        }

        [Test]
        public void 적이_범위보다_적으면_있는_만큼만_때린다()
        {
            // 군(3인)인데 적이 둘뿐이다. 설계 §D4 — "살아 있는 적이 N보다 적으면 있는 만큼만".
            var a = Team(Man("A1", Stats(), Learned(Sword(AttackScope.Three))));
            var b = Team(Dummy("B1"), Dummy("B2"));

            TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(11u), maxRounds: 1);

            Assert.AreEqual(2, TargetsStruck(r, "A1", 1));
        }

        [Test]
        public void 범위_무공도_기력은_한_번만_낸다()
        {
            // ⚠⚠ 행동 단위 / 대상 단위 구분의 핵심(2026-08-09 갈라낸 선).
            //   넷을 때려도 기력은 한 번이므로, 기력이 찍힌 로그 줄은 그 행동에 **하나뿐**이어야 한다.
            var a = Team(Man("A1", Stats(), Learned(Sword(AttackScope.All))));
            var b = Team(Dummy("B1"), Dummy("B2"), Dummy("B3"), Dummy("B4"));

            TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(11u), maxRounds: 1);

            int paid = 0;
            foreach (CombatLogEntry e in r.Log)
            {
                if (e.Kind == CombatLogKind.Action && e.ActorName == "A1" && e.QiSpent > 0) paid++;
            }

            Assert.AreEqual(4, TargetsStruck(r, "A1", 1), "넷을 때리지 않았다면 이 시험은 성립하지 않는다");
            Assert.AreEqual(1, paid, "범위 무공이 대상 수만큼 기력을 냈다");
        }

        // ───────────────────────────── 큐 (설계 §D2) ─────────────────────────────

        [Test]
        public void 큐는_속도가_아니라_선공_순으로_선다()
        {
            // ⚠⚠ 이 저장소가 한 번 틀린 지점이다 — 정렬 키는 `Speed` 가 아니라 `Initiative` 다.
            //   경공(legacy Movement)은 **선공만** 올리고 속도는 건드리지 않으므로, 둘을 가른다.
            MartialArt steps = MartialArt.Support("steps", "보법", Discipline.Movement, Alignment.Orthodox,
                initiativeBonus: 20);

            Combatant slow = Man("느린쪽", Stats(), Learned(Sword()));
            Combatant fast = Man("빠른쪽", Stats(), Learned(Sword()), Learned(steps));

            Assert.AreEqual(slow.Speed, fast.Speed, "속도가 갈리면 이 시험은 선공을 재는 것이 아니다");
            Assert.Greater(fast.Initiative, slow.Initiative);

            // 큐가 팀을 가로질러 서는지도 함께 본다 — 뒤 팀(B)의 사람이 먼저 나와야 한다.
            TeamCombatResult r = CombatResolver.ResolveTeams(
                Team(slow), Team(fast), new XorShiftRandom(3u), maxRounds: 1);

            Assert.AreEqual("빠른쪽", r.Log[0].ActorName);
        }

        [Test]
        public void 경합_도중_쓰러진_사람은_자기_차례를_건너뛴다()
        {
            // 아주 센 하나가 적 둘 중 하나를 그 경합에 눕힌다.
            // 쓰러진 사람은 큐에 자리가 있어도 행동 로그를 남기면 안 된다.
            MartialArt steps = MartialArt.Support("steps", "보법", Discipline.Movement, Alignment.Orthodox,
                initiativeBonus: 50);
            Combatant killer = Man("살수", Stats(attack: 400), Learned(Sword(qiCost: 0, basePower: 400)), Learned(steps));

            int verified = 0;
            for (uint seed = 1; seed <= 60; seed++)
            {
                TeamCombatResult r = CombatResolver.ResolveTeams(
                    Team(killer), Team(Dummy("B1", health: 10), Dummy("B2", health: 10)),
                    new XorShiftRandom(seed), maxRounds: 1);

                for (int i = 0; i < r.TeamBHealthLeft.Count; i++)
                {
                    if (r.TeamBHealthLeft[i] > 0) continue;

                    string dead = i == 0 ? "B1" : "B2";
                    foreach (CombatLogEntry e in r.Log)
                    {
                        if (e.Kind == CombatLogKind.Action && e.ActorName == dead && !IsCounter(e))
                        {
                            Assert.Fail("쓰러진 " + dead + " 이 같은 경합에 행동했다 (seed " + seed + ")");
                        }
                    }
                    verified++;
                }
            }

            Assert.Greater(verified, 0, "아무도 쓰러지지 않아 건너뜀 자체가 관측되지 않았다 — 시험이 공허하다");
        }

        [Test]
        public void 기력_회복은_경합마다_한_번만_돈다()
        {
            // ⚠⚠ 회복 타이밍은 기력 압력 축의 캘리브레이션이 걸린 자리다(설계 §D2 · HANDOFF §4-3).
            //   큐 항목마다 전원을 회복시키면 인원수만큼 빨리 차오른다 — 그것을 **개수로** 잡는다.
            //
            //   최대기력 40 → 시작 10(1/4) · 회복 10/경합 · 초식 25.
            //     1: 20 평타 / 2: 30 초식→5 / 3: 15 평타 / 4: 25 초식→0 / 5: 10 평타
            //     6: 20 평타 / 7: 30 초식→5 / 8: 15 평타 / 9: 25 초식→0 / 10: 10 평타
            //   → 10행동 중 초식 4 · 평타 6. 회복이 두 배로 돌면 초식이 더 나온다.
            var a = Team(Man("A1", Stats(qi: 40), Learned(Sword(qiCost: 25))));
            var b = Team(Dummy("B1"));

            TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(5u), maxRounds: 10);

            Assert.AreEqual(10, r.Rounds);
            Assert.AreEqual(10, r.TeamAActions, "한 경합에 한 번씩 행동해야 한다");
            Assert.AreEqual(6, r.TeamABasicStrikes, "평타 전락 횟수가 기력 회복 주기와 맞지 않는다");
        }

        [Test]
        public void 아무도_같은_편을_때리지_않는다()
        {
            var a = Team(Man("A1", Stats(), Learned(Sword(AttackScope.All))), Man("A2", Stats(), Learned(Sword())));
            var b = Team(Man("B1", Stats(), Learned(Sword(AttackScope.All))), Man("B2", Stats(), Learned(Sword())));

            TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(77u), maxRounds: 12);

            foreach (CombatLogEntry e in r.Log)
            {
                if (e.Kind != CombatLogKind.Action) continue;
                bool actorIsA = e.ActorName[0] == 'A';
                bool targetIsA = e.TargetName[0] == 'A';
                Assert.AreNotEqual(actorIsA, targetIsA, "같은 편을 때렸다: " + e);
            }
        }

        // ───────────────────────────── 반격 (설계 §D6) ─────────────────────────────

        [Test]
        public void 범위에_맞은_사람들이_각자_반격한다()
        {
            MartialArt counterArt = Derived("반격검", ArtStatDelta.Of(attack: 2, counterRate: 100));

            var a = Team(Man("A1", Stats(), Learned(Sword(AttackScope.All, basePower: 40))));
            var b = Team(
                Man("B1", Stats(), Learned(counterArt)),
                Man("B2", Stats(), Learned(counterArt)),
                Man("B3", Stats(), Learned(counterArt)));

            TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(21u), maxRounds: 1);

            var counterers = new HashSet<string>();
            foreach (CombatLogEntry e in r.Log)
            {
                if (e.Kind == CombatLogKind.Action && IsCounter(e)) counterers.Add(e.ActorName);
            }

            Assert.AreEqual(3, TargetsStruck(r, "A1", 1), "셋을 때리지 않았다면 이 시험은 성립하지 않는다");
            Assert.AreEqual(3, counterers.Count, "맞은 사람이 각자 반격하지 않았다");
        }

        [Test]
        public void 반격이_반격을_부르지_않는다()
        {
            // 양쪽 다 반격 100%. 연쇄가 있다면 반격 줄이 연달아 붙는다.
            MartialArt counterArt = Derived("반격검", ArtStatDelta.Of(attack: 2, counterRate: 100));

            var a = Team(Man("A1", Stats(), Learned(counterArt)));
            var b = Team(Man("B1", Stats(), Learned(counterArt)));

            TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(33u), maxRounds: 20);

            bool sawCounter = false;
            bool previousWasCounter = false;
            foreach (CombatLogEntry e in r.Log)
            {
                if (e.Kind != CombatLogKind.Action) continue;
                bool isCounter = IsCounter(e);
                if (isCounter)
                {
                    sawCounter = true;
                    Assert.IsFalse(previousWasCounter, "반격이 반격을 불렀다: " + e);
                }
                previousWasCounter = isCounter;
            }

            Assert.IsTrue(sawCounter, "반격이 한 번도 안 나왔다 — 시험이 공허하다");
        }

        // ───────────────────────── 절대경지 쌍(雙) (설계 §D5) ─────────────────────────

        [Test]
        public void 쌍雙의_두_번째_행동은_대상을_다시_고른다()
        {
            // ⚠ "다시 고른다" 는 **매번 다른 대상** 이라는 뜻이 아니다(무작위라 같은 사람이 또 뽑힐 수 있다).
            //   두 행동의 대상이 **갈라지는 시드가 존재한다**는 것이 재선택의 관측 가능한 형태다.
            MartialArt twice = Derived("쌍검", ArtStatDelta.Of(attack: 2), rule: AbsoluteRule.DoubleAction);
            Combatant actor = Man("A1", Stats(), Learned(twice));

            int split = 0;
            int acted = 0;
            for (uint seed = 1; seed <= 60; seed++)
            {
                TeamCombatResult r = CombatResolver.ResolveTeams(
                    Team(actor), Team(Dummy("B1"), Dummy("B2"), Dummy("B3"), Dummy("B4")),
                    new XorShiftRandom(seed), maxRounds: 1);

                var names = new List<string>();
                foreach (CombatLogEntry e in r.Log)
                {
                    if (e.Kind == CombatLogKind.Action && e.ActorName == "A1" && !IsCounter(e)) names.Add(e.TargetName);
                }

                Assert.AreEqual(2, names.Count, "쌍(雙)이 한 경합에 두 번 행동하지 않았다 (seed " + seed + ")");
                acted++;
                if (names[0] != names[1]) split++;
            }

            Assert.AreEqual(60, acted);
            Assert.Greater(split, 0, "두 행동이 언제나 같은 대상을 쳤다 — 대상을 다시 고르지 않는다");
        }

        // ───────────────────────────── 승패 (설계 §D7) ─────────────────────────────

        [Test]
        public void 한_팀이_전멸하면_상대가_이긴다()
        {
            var a = Team(
                Man("A1", Stats(attack: 400), Learned(Sword(qiCost: 0, basePower: 400))),
                Man("A2", Stats(attack: 400), Learned(Sword(qiCost: 0, basePower: 400))));
            var b = Team(Dummy("B1", health: 30), Dummy("B2", health: 30));

            TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(4u), maxRounds: 20);

            Assert.AreEqual(TeamOutcome.TeamAWin, r.Outcome);
            Assert.AreEqual(0, r.SurvivorsB);
            Assert.Greater(r.SurvivorsA, 0);
        }

        [Test]
        public void 상한에_걸리면_무승부다()
        {
            // 서로 거의 못 깎는 둘. 최대 경합에서 잘린다.
            var a = Team(Dummy("A1"), Dummy("A2"));
            var b = Team(Dummy("B1"), Dummy("B2"));

            TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(4u), maxRounds: 3);

            Assert.AreEqual(TeamOutcome.Draw, r.Outcome);
            Assert.AreEqual(3, r.Rounds);
            Assert.AreEqual(2, r.SurvivorsA);
            Assert.AreEqual(2, r.SurvivorsB);
        }

        // ───────────────────────────── 결정론 ─────────────────────────────

        [Test]
        public void 같은_시드는_같은_팀전투_결과를_낸다()
        {
            // ⚠ 열을 섞어 둔다 — 진형이 들어온 뒤로는 배치도 재현 대상이다.
            List<BattlePlacement> A() => Line(
                BattlePlacement.Front(Man("A1", Stats(health: 300), Learned(Sword(AttackScope.Two)))),
                BattlePlacement.Rear(Man("A2", Stats(health: 300), Learned(Thrown()))));
            List<BattlePlacement> B() => Line(
                BattlePlacement.Front(Man("B1", Stats(health: 300), Learned(Sword(AttackScope.Three)))),
                BattlePlacement.Rear(Man("B2", Stats(health: 300), Learned(Sword()))));

            TeamCombatResult first = CombatResolver.ResolveTeams(A(), B(), new XorShiftRandom(9001u));
            TeamCombatResult second = CombatResolver.ResolveTeams(A(), B(), new XorShiftRandom(9001u));

            Assert.AreEqual(Serialize(first), Serialize(second));
        }

        [Test]
        public void 팀전투는_입력_Combatant_를_변형하지_않는다()
        {
            Combatant a1 = Man("A1", Stats(health: 300), Learned(Sword(AttackScope.All)));
            Combatant b1 = Man("B1", Stats(health: 300), Learned(Sword()));
            Combatant b2 = Man("B2", Stats(health: 300), Learned(Sword()));

            int qiBefore = a1.EffectiveMaxQi;
            TeamCombatResult r1 = CombatResolver.ResolveTeams(Team(a1), Team(b1, b2), new XorShiftRandom(7u));
            TeamCombatResult r2 = CombatResolver.ResolveTeams(Team(a1), Team(b1, b2), new XorShiftRandom(7u));

            Assert.AreEqual(qiBefore, a1.EffectiveMaxQi, "전투가 Combatant 의 상태를 바꿨다");
            Assert.AreEqual(Serialize(r1), Serialize(r2));
        }

        // ───────────────────────────── 1대1 불변 ─────────────────────────────

        [Test]
        public void 빈_팀은_거부한다()
        {
            Assert.Throws<System.ArgumentException>(() =>
                CombatResolver.ResolveTeams(new List<BattlePlacement>(), Team(Dummy("B1")), new XorShiftRandom(1u)));
        }

        // ───────────────────────── 진형 — 열 성향 (설계 §D3-3) ─────────────────────────

        [Test]
        public void 공격방식_넷_중_던지기만_후열이다()
        {
            // ⚠ 이 시험이 없으면 사전의 뜻 문자열이 바뀔 때 규칙이 **조용히 죽는다**(전부 전열이 된다).
            AssertRow("벌참절단", BattleRow.Front);
            AssertRow("자창", BattleRow.Front);
            AssertRow("구타격박", BattleRow.Front);
            AssertRow("투척포사", BattleRow.Rear);
        }

        [Test]
        public void 수식_어둡다_세_자는_후열이다()
        {
            AssertRow("야암한", BattleRow.Rear);
        }

        [Test]
        public void 기만에는_열_성향이_없다()
        {
            // ⛔ B안(기만에 후열을 주는 안) 기각을 코드에 고정한다 — 설계 §D3-3-b.
            //   기만은 명중 +2 인 이미 강한 형태소라 후열까지 얹으면 중복 강화다.
            foreach (char c in "환궤")
            {
                Morpheme m = MorphemeDictionary.Get(c);
                BattleRow row;
                Assert.IsFalse(BattleRowRule.TryRowOf(m, out row), c + " 에 열 성향이 생겼다");
            }

            // 실제 무공으로도 본다 — `환혈참` 은 기만이 맨 앞이지만 열을 정하는 것은 베기(참)다.
            Assert.AreEqual(BattleRow.Front, MorphemeParser.Parse("환혈참", ArtKind.Attack).PreferredRow);
        }

        [Test]
        public void 열_성향이_둘이면_이름에서_앞선_글자가_이긴다()
        {
            // ⭐ 글자 순서에 처음으로 의미가 생기는 자리다 (설계 §D3-3-a).
            Assert.AreEqual(BattleRow.Rear, MorphemeParser.Parse("야참", ArtKind.Attack).PreferredRow, "어둡다가 앞이면 후열");
            Assert.AreEqual(BattleRow.Front, MorphemeParser.Parse("참야", ArtKind.Attack).PreferredRow, "베기가 앞이면 전열");

            // ⚠ 실제 138종은 대체로 `[수식][무공형태][공격방식]` 순이라 **수식이 이긴다.**
            //   설계 §D3-3-a 가 예고한 대가이며, 그것이 실제로 그러함을 여기서 못박는다.
            Assert.AreEqual(BattleRow.Rear, MorphemeParser.Parse("야궤타", ArtKind.Attack).PreferredRow, "근접(때리기)인데 야가 앞이다");
        }

        [Test]
        public void 열은_파서에서_무공까지_실려_온다()
        {
            // 팩토리가 `parsed.PreferredRow` 를 버리면 여기서 걸린다 — 범위(Scope)가 겪었던 바로 그 누락이다.
            MartialArt thrown = Create("유독척", Discipline.Dagger, "장강수로채");
            MartialArt melee = Create("환혈참", Discipline.Blade, "녹림");

            Assert.AreEqual(BattleRow.Rear, thrown.PreferredRow, "던지기 무공이 전열기로 실려 왔다");
            Assert.AreEqual(BattleRow.Front, melee.PreferredRow);
        }

        [Test]
        public void 파서는_이름의_글자_순서를_보존한다()
        {
            // ⚠ 이 규칙의 **전제**다. Body 가 순서를 잃으면 열이 무작위로 정해진다.
            ParsedArtName parsed = MorphemeParser.Parse("궤암척혈", ArtKind.Attack);

            var body = new StringBuilder();
            for (int i = 0; i < parsed.Body.Count; i++) body.Append(parsed.Body[i].Korean);

            Assert.AreEqual("궤암척혈", body.ToString());
        }

        private static void AssertRow(string groupChars, BattleRow expected)
        {
            foreach (char c in groupChars)
            {
                Morpheme m = MorphemeDictionary.Get(c);
                BattleRow row;
                Assert.IsTrue(BattleRowRule.TryRowOf(m, out row), c + " 에 열 성향이 없다");
                Assert.AreEqual(expected, row, c + " 의 열이 다르다");
            }
        }

        private static MartialArt Create(string name, Discipline discipline, string school)
        {
            return MartialArtFactory.Create(
                "t_" + name, name, ArtKind.Attack, ArtTier.Minor, discipline, Alignment.Unorthodox, school);
        }

        // ───────────────────────── 진형 — 타겟팅 (설계 §D3-2 · §D4) ─────────────────────────

        [Test]
        public void 전열이_살아_있으면_근접은_후열을_못_친다()
        {
            var a = Team(Man("A1", Stats(), Learned(Sword())));
            var b = Line(
                BattlePlacement.Front(Dummy("전1")), BattlePlacement.Front(Dummy("전2")),
                BattlePlacement.Rear(Dummy("후1")), BattlePlacement.Rear(Dummy("후2")));

            for (uint seed = 1; seed <= 40; seed++)
            {
                TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(seed), maxRounds: 1);
                foreach (string hit in StruckNames(r, "A1"))
                {
                    Assert.IsTrue(hit[0] == '전', "근접 무공이 후열 " + hit + " 을 쳤다 (seed " + seed + ")");
                }
            }
        }

        [Test]
        public void 던지기는_후열을_먼저_친다()
        {
            var a = Team(Man("A1", Stats(), Learned(Thrown())));
            var b = Line(
                BattlePlacement.Front(Dummy("전1")), BattlePlacement.Front(Dummy("전2")),
                BattlePlacement.Rear(Dummy("후1")), BattlePlacement.Rear(Dummy("후2")));

            for (uint seed = 1; seed <= 40; seed++)
            {
                TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(seed), maxRounds: 1);
                foreach (string hit in StruckNames(r, "A1"))
                {
                    Assert.IsTrue(hit[0] == '후', "던지기가 전열 " + hit + " 을 쳤다 (seed " + seed + ")");
                }
            }
        }

        [Test]
        public void 전열이_전멸하면_근접이_후열을_친다()
        {
            // 전열을 아예 비운 배치 = "모두 노출". 설계 §D3-1 — 전열이 없으면 후열이 곧 전열이다.
            var a = Team(Man("A1", Stats(), Learned(Sword())));
            var b = Line(BattlePlacement.Rear(Dummy("후1")), BattlePlacement.Rear(Dummy("후2")));

            TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(12u), maxRounds: 1);

            List<string> hits = StruckNames(r, "A1");
            Assert.AreEqual(1, hits.Count, "때릴 상대를 못 찾았다");
            Assert.AreEqual('후', hits[0][0], "후열이 아닌 것을 쳤다: " + hits[0]);
        }

        [Test]
        public void 후열이_전멸하면_던지기가_전열을_친다()
        {
            // ⚠ "후열 우선" 은 "후열만" 이 아니다 — 전열 우선의 정확한 대칭이다(설계 §D3-3).
            var a = Team(Man("A1", Stats(), Learned(Thrown())));
            var b = Line(BattlePlacement.Front(Dummy("전1")), BattlePlacement.Front(Dummy("전2")));

            TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(12u), maxRounds: 1);

            List<string> hits = StruckNames(r, "A1");
            Assert.AreEqual(1, hits.Count, "때릴 상대를 못 찾았다");
            Assert.AreEqual('전', hits[0][0], "전열이 아닌 것을 쳤다: " + hits[0]);
        }

        [Test]
        public void 근접_군群은_전열_둘과_후열_하나를_친다()
        {
            // 4인 편성(전2/후2)에서 근접 3인 범위기는 **우선 열을 채우고 넘친 만큼만** 반대 열로 간다(설계 §D4).
            var a = Team(Man("A1", Stats(), Learned(Sword(AttackScope.Three))));
            var b = Line(
                BattlePlacement.Front(Dummy("전1")), BattlePlacement.Front(Dummy("전2")),
                BattlePlacement.Rear(Dummy("후1")), BattlePlacement.Rear(Dummy("후2")));

            for (uint seed = 1; seed <= 40; seed++)
            {
                TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(seed), maxRounds: 1);
                List<string> hits = StruckNames(r, "A1");

                Assert.AreEqual(3, hits.Count, "seed " + seed);
                int front = 0;
                for (int i = 0; i < hits.Count; i++)
                {
                    if (hits[i][0] == '전') front++;
                }
                Assert.AreEqual(2, front, "전열 둘을 채우지 않았다 (seed " + seed + ")");
            }
        }

        [Test]
        public void 전全은_열과_무관하게_전원을_친다()
        {
            // ⭐ 특례 코드 없이 성립해야 한다 — 대상 수가 생존자 수를 넘으므로 열을 볼 일이 없다.
            var a = Team(Man("A1", Stats(), Learned(Sword(AttackScope.All))));
            var b = Line(
                BattlePlacement.Front(Dummy("전1")), BattlePlacement.Front(Dummy("전2")),
                BattlePlacement.Rear(Dummy("후1")), BattlePlacement.Rear(Dummy("후2")));

            TeamCombatResult r = CombatResolver.ResolveTeams(a, b, new XorShiftRandom(12u), maxRounds: 1);

            Assert.AreEqual(4, StruckNames(r, "A1").Count);
        }

        [Test]
        public void 후열_던지기도_1대1에서는_상대를_친다()
        {
            // 열 개념이 1대1로 새지 않았는지 본다(설계 §D3-8). 1대1은 상대가 하나뿐이라 진형이 없다.
            Combatant a = Man("검객", Stats(health: 300), Learned(Thrown()));
            Combatant d = Man("도객", Stats(health: 300), Learned(Sword()));

            CombatResult r = CombatResolver.Resolve(a, d, new XorShiftRandom(8u));

            int struck = 0;
            foreach (CombatLogEntry e in r.Log)
            {
                if (e.Kind == CombatLogKind.Action && e.ActorName == "검객" && e.TargetName == "도객") struck++;
            }
            Assert.Greater(struck, 0, "후열 성향 무공이 1대1에서 상대를 못 쳤다");
        }

        /// <summary><paramref name="actor"/> 가 그 전투에서 실제로 때린 대상 이름들(반격 제외).</summary>
        private static List<string> StruckNames(TeamCombatResult r, string actor)
        {
            var names = new List<string>();
            foreach (CombatLogEntry e in r.Log)
            {
                if (e.Kind != CombatLogKind.Action || e.ActorName != actor || IsCounter(e)) continue;
                if (!names.Contains(e.TargetName)) names.Add(e.TargetName);
            }
            return names;
        }
    }
}
