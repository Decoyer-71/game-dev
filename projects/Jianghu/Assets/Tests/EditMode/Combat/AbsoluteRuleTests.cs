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
    /// **절대경지 규칙 4종이 엔진에 붙어 있는가** — 정의서 §5-3 (2026-08-02 연결).
    ///
    /// 규칙은 **수치가 아니라 전투 규칙 자체**를 바꾼다. 그래서 <see cref="ArtStatDelta"/> 에 들어가지
    /// 않고 <see cref="AbsoluteRule"/> 로 나르며, <see cref="Combatant"/> 가 익힌 무공 전체에서
    /// **`any` 로 접어** 사람에게 상시 적용한다(보조 무공의 기존 처리와 같다).
    ///
    /// ⚠⚠ **합산이 아닌 이유**: 델타는 22축이 전부 `double` 이라 무공을 여럿 배우면 더해진다.
    ///   불리언을 숫자로 담으면 *"면역이 두 겹"* 이라는 의미 없는 값이 생긴다.
    ///
    /// ⚠ 절대 수치를 박지 않는다(HANDOFF §7). 보는 것은 **부등호와 존재**다.
    /// </summary>
    public class AbsoluteRuleTests
    {
        private const int Seeds = 200;

        /// <summary>절대경지 무공 넷. 이름 둘째 자리가 규칙 글자다.</summary>
        private const string Immunity = "정면합광";       // 면(免) — 무림맹
        private const string NoQiCost = "식무유수";       // 무(無) — 천마신교
        private const string DoubleAct = "음쌍쾌신";      // 쌍(雙) — 사도련
        private const string Supremacy = "합통현유";      // 통(統) — 제천성

        private static MartialArt Absolute(string name)
        {
            return MartialArtFactory.Create(
                "abs_" + name, name, ArtKind.Internal, ArtTier.Absolute,
                Discipline.InnerArt, null, null);
        }

        /// <summary>공격 무공 하나 + (선택) 절대경지 내공 하나를 익힌 사람.</summary>
        private static Combatant Fighter(
            string attackArtName, string absoluteName = null, ArtLineage? lineage = null,
            string school = "점창파", ArtTier tier = ArtTier.Minor)
        {
            MartialArt attack = MartialArtFactory.Create(
                "a_" + attackArtName, attackArtName, ArtKind.Attack, tier,
                Discipline.Sword, Alignment.Orthodox, school);

            int sessions = AlignmentCurve.SessionsToReach(
                Alignment.Orthodox, MartialStage.ProficiencyForStage(MartialStage.MaxStage));

            var arts = new List<LearnedArt> { new LearnedArt(attack, sessions, Alignment.Orthodox) };
            if (absoluteName != null)
            {
                arts.Add(new LearnedArt(Absolute(absoluteName), sessions, Alignment.Orthodox));
            }

            return new Combatant(
                attackArtName + (absoluteName ?? ""), CharacterStats.MaxLevel(), arts,
                new List<DisciplineMastery>
                {
                    new DisciplineMastery(Discipline.Sword, DisciplineCurve.SessionsToMaster(Discipline.Sword)),
                },
                lineage);
        }

        private static int CountLog(Combatant a, Combatant d, string actorName, string mark)
        {
            int n = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(a, d, new XorShiftRandom(seed));
                IReadOnlyList<CombatLogEntry> log = r.Log;
                for (int i = 0; i < log.Count; i++)
                {
                    CombatLogEntry e = log[i];
                    if (e.Kind != CombatLogKind.Action || e.ActorName != actorName) continue;
                    if (mark == null) { n++; continue; }
                    if (!string.IsNullOrEmpty(e.Note) && e.Note.IndexOf(mark) >= 0) n++;
                }
            }
            return n;
        }

        private static double AverageDamagePerHit(Combatant a, Combatant d)
        {
            long total = 0; int hits = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(a, d, new XorShiftRandom(seed));
                IReadOnlyList<CombatLogEntry> log = r.Log;
                for (int i = 0; i < log.Count; i++)
                {
                    CombatLogEntry e = log[i];
                    if (e.Kind != CombatLogKind.Action || e.ActorName != a.Name) continue;
                    if (e.Damage <= 0) continue;
                    if (!string.IsNullOrEmpty(e.Note) && e.Note.IndexOf("[반격]") >= 0) continue;
                    total += e.Damage; hits++;
                }
            }
            Assert.Greater(hits, 0, "표본이 없다.");
            return (double)total / hits;
        }

        /// <summary>
        /// **턴당 행동 수.** 총합이 아니다 — 2회 행동은 전투를 짧게 만들어 총합을 줄인다.
        /// </summary>
        private static double ActionsPerTurn(Combatant a, Combatant d)
        {
            int acts = 0, turns = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(a, d, new XorShiftRandom(seed));
                turns += r.Turns;
                for (int i = 0; i < r.Log.Count; i++)
                {
                    if (r.Log[i].Kind == CombatLogKind.Action && r.Log[i].ActorName == a.Name) acts++;
                }
            }
            Assert.Greater(turns, 0, "표본이 없다.");
            return (double)acts / turns;
        }

        /// <summary>**초식 시전으로** 쓴 기력. `Action` 만 센다 — 기력소실 틱과 필드를 공유하기 때문이다.</summary>
        private static int CastQiSpent(Combatant a, Combatant d)
        {
            int spent = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(a, d, new XorShiftRandom(seed));
                for (int i = 0; i < r.Log.Count; i++)
                {
                    if (r.Log[i].Kind == CombatLogKind.Action && r.Log[i].ActorName == a.Name)
                    {
                        spent += r.Log[i].QiSpent;
                    }
                }
            }
            return spent;
        }

        // ─────────────────────────── 데이터가 실려 있는가 ───────────────────────────

        [Test]
        public void 절대경지_넷이_규칙을_하나씩_싣는다()
        {
            Assert.AreEqual(AbsoluteRule.StatusImmunity, Absolute(Immunity).Rule);
            Assert.AreEqual(AbsoluteRule.NoQiCost, Absolute(NoQiCost).Rule);
            Assert.AreEqual(AbsoluteRule.DoubleAction, Absolute(DoubleAct).Rule);
            Assert.AreEqual(AbsoluteRule.CounterSupremacy, Absolute(Supremacy).Rule);
        }

        [Test]
        public void 규칙_형태소는_수치를_주지_않는다()
        {
            // §5-3 — "수치로 강하게 만들지 않는다. 대신 규칙을 바꾼다."
            // 수치를 주면 전승무학을 압도해 문파 성장 경로가 무의미해진다.
            foreach (char c in new[] { '면', '무', '쌍', '통' })
            {
                Morpheme m = MorphemeDictionary.Get(c);
                Assert.AreEqual(MorphemeCategory.AbsoluteRule, m.Category, "{0} 의 카테고리", c);
                Assert.IsTrue(m.Delta.IsZero, "{0} 가 수치를 준다 — 규칙 형태소는 Zero 여야 한다", c);
            }
        }

        // ─────────────────────────── 조합 규칙 ───────────────────────────

        [Test]
        public void 규칙_형태소는_절대경지에만_쓸_수_있다()
        {
            // 극한경지가 전승무학 전용인 것과 같은 계층 제한이다.
            Assert.Throws<System.ArgumentException>(() => MartialArtFactory.Create(
                "bad", "정면합광", ArtKind.Internal, ArtTier.Major,
                Discipline.InnerArt, Alignment.Orthodox, "점창파"));
        }

        [Test]
        public void 절대경지는_규칙_형태소가_없으면_성립하지_않는다()
        {
            // ⚠ 극한경지의 "1자만"(선택)과 달리 **필수**다 — §5-3 이 절대경지를
            //   "수치가 아니라 규칙을 바꾸는 것" 으로 정의하기 때문이다.
            Assert.Throws<System.ArgumentException>(() => MartialArtFactory.Create(
                "bad2", "정합광휘", ArtKind.Internal, ArtTier.Absolute,
                Discipline.InnerArt, null, null));
        }

        [Test]
        public void 무학분류는_부정_없이_쓸_수_없다()
        {
            // §3-10-a (2026-08-02 사용자 확정). 이 검사가 없던 동안 위반 8종이 출하됐다.
            Assert.Throws<System.ArgumentException>(() => MartialArtFactory.Create(
                "bad3", "유명참일", ArtKind.Attack, ArtTier.Major,
                Discipline.Sword, Alignment.Orthodox, "화산파"));

            // 부정이 앞에 있으면 정상이다 — 낙월 = 음기무학 상성.
            Assert.DoesNotThrow(() => MartialArtFactory.Create(
                "ok", "창천낙월", ArtKind.Attack, ArtTier.Minor,
                Discipline.Sword, Alignment.Orthodox, "점창파"));
        }

        // ─────────────────────────── 전투에서 일하는가 ───────────────────────────

        [Test]
        public void 면은_상태이상을_막는다()
        {
            Combatant poisoner = Fighter("중참독");

            int onPlain = CountLog(poisoner, Fighter("쾌자탈"), poisoner.Name, "[중독");
            int onImmune = CountLog(poisoner, Fighter("쾌자탈", Immunity), poisoner.Name, "[중독");

            Assert.Greater(onPlain, 0, "대조군에 중독이 한 번도 안 걸렸다 — 표본이 없다.");
            Assert.AreEqual(0, onImmune, "면(免) 보유자에게 상태이상이 걸렸다 — 면역이 부여 단계를 막지 못했다.");
        }

        [Test]
        public void 쌍은_행동_수를_늘린다()
        {
            // ⚠ 속공과 겹치지 않게 만들었으므로(확정 1회를 쓴 턴에는 속공을 건너뛴다)
            //   같은 무공끼리 붙여 속공 조건을 대칭으로 둔다.
            // ⚠⚠ **턴당 행동 수로 잰다.** 총 행동 수로 쟀다가 틀렸다 — 2회 행동은 상대를 더 빨리
            //   눕히므로 **전투가 짧아져 총합이 오히려 줄어든다**(실측 7080 → 4328).
            //   재려는 것은 "얼마나 자주 치는가" 이지 "몇 번 쳤는가" 가 아니다.
            // ⚠⚠ **대조군의 이름을 다르게 둔다.** 처음에 양쪽 다 `쾌자탈` 로 뒀다가 틀렸다 —
            //   `Combatant.Name` 이 같으면 로그 필터가 **두 사람의 행동을 합산**해 대조군이
            //   저절로 2.0 에 가까워진다(실측 1.971 vs 1.957 로 차이가 사라졌다).
            double plainRate = ActionsPerTurn(Fighter("쾌자탈"), Fighter("중참방"));
            double twiceRate = ActionsPerTurn(Fighter("쾌자탈", DoubleAct), Fighter("중참방"));

            Assert.Greater(twiceRate, plainRate,
                "쌍(雙) 보유자의 턴당 행동 수가 늘지 않았다 — 2회 행동이 안 붙었다.");
        }

        [Test]
        public void 통은_분류를_가진_상대에게만_상성_우위를_얻는다()
        {
            // ⚠⚠ **2×2 로 잰다.** 같은 공격자가 **분류만 다른 두 상대**를 때린다.
            //   상대가 바뀌는 것은 `Lineage` 하나뿐이므로 통(統)의 과녁 조건만 남는다.
            //
            // ⚠ 처음엔 *"무소속 상대에게도 붙는다"* 로 만들고 무소속 상대 하나로만 쟀는데,
            //   그 테스트는 **잘못된 이유로 통과**했다 — 절대경지 무공이 실어 오는 현(玄 회피+10)·
            //   유(柔變 명중+2)가 명중률을 바꿔 *"적중한 타격의 평균"* 자체를 움직였기 때문이다.
            //   보조 무공을 붙이고 빼는 대조는 규칙 외의 것도 같이 움직인다.
            Combatant supreme = Fighter("쾌자탈", Supremacy);
            Combatant plain = Fighter("쾌자탈");

            double supremeVsTagged = AverageDamagePerHit(supreme, Fighter("쾌자탈", null, ArtLineage.Yin));
            double supremeVsNone = AverageDamagePerHit(supreme, Fighter("쾌자탈", null, null));

            double plainVsTagged = AverageDamagePerHit(plain, Fighter("쾌자탈", null, ArtLineage.Yin));
            double plainVsNone = AverageDamagePerHit(plain, Fighter("쾌자탈", null, null));

            Assert.Greater(supremeVsTagged, supremeVsNone,
                "통(統) 보유자가 분류를 가진 상대를 더 아프게 때리지 않는다 — 상성 +2 가 안 붙었다.");
            Assert.AreEqual(plainVsNone, plainVsTagged,
                "통(統) 이 없는데도 상대 분류가 피해를 바꿨다 — 분류가 다른 축을 오염시키고 있다.");
        }

        [Test]
        public void 통은_피격당할_때_상대_상성을_무효로_만든다()
        {
            // ⚠⚠ 이 테스트가 §5-C 대원칙을 지킨다 — *"상대 상성 무효"* 라는 이름의 절반이
            //   실현되는지 본다. 공격 측만 걸면 이름이 거짓말이 된다.
            //   창천낙월(낙월)은 **음기무학**에 상성 +1 이므로 방어자를 음기로 둔다.
            Combatant counterArt = Fighter("창천낙월", null, ArtLineage.Yang);

            double onPlain = AverageDamagePerHit(counterArt, Fighter("쾌자탈", null, ArtLineage.Yin));
            double onSupreme = AverageDamagePerHit(counterArt, Fighter("쾌자탈", Supremacy, ArtLineage.Yin));

            Assert.Less(onSupreme, onPlain,
                "통(統) 보유자가 상성 공격을 그대로 맞는다 — 상대 상성 무효가 방어 측에 안 붙었다.");
        }

        [Test]
        public void 무는_기력을_소모하지_않는다()
        {
            // ⚠⚠ **승률로 재지 않는다.** 평타 전락률이 전 무공·전 경지 0.0% 라(HANDOFF §4-2-d)
            //   아무도 기력이 마르지 않으므로 이 규칙은 **승률에 아무 영향이 없다.**
            //   그래서 기구(機構)가 붙었는지만 직접 본다 — 승률로 재면 공허한 통과가 된다.
            // ⚠⚠ **로그 종류를 반드시 거른다.** 처음에 안 걸렀다가 틀렸다 — `QiSpent` 필드는
            //   초식 소모(Action)와 **기력소실 상태이상 틱(StatusTick)이 공유**한다. 대조 무공에
            //   탈(奪)이 들어 있으면 상대가 깎은 기력이 "내가 쓴 기력" 으로 집계된다(실측 80).
            //   ⚠ 무(無)는 **시전 소모**를 없앨 뿐 기력소실 면역이 아니다 — 다른 축이다.
            Combatant plain = Fighter("중참독");
            Combatant free = Fighter("중참독", NoQiCost);

            Assert.IsTrue(free.HasNoQiCost, "진단: 무(無) 규칙이 사람에게 안 붙었다.");

            int plainQiSpent = CastQiSpent(plain, Fighter("중참방"));
            int freeQiSpent = CastQiSpent(free, Fighter("중참방"));

            Assert.Greater(plainQiSpent, 0, "대조군이 기력을 안 썼다 — 표본이 없다.");
            Assert.AreEqual(0, freeQiSpent, "무(無) 보유자가 기력을 썼다 — 무소모가 안 붙었다.");
        }
    }
}
