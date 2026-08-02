using System.Collections.Generic;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;
using NUnit.Framework;

namespace Jianghu.Tests.Martial
{
    /// <summary>
    /// 무공명 파서와 조합 규칙 검사 (설계안 §5-1 중 2·3단계 해당분).
    ///
    /// 여기서 지키는 것은 두 가지다:
    ///   1. **접미사를 먼저, 최장일치로 뗀다** — 안 그러면 `천양신공` 의 신을 형태소 迅 으로 오독한다
    ///   2. **미등록 글자를 삼키지 않는다**(R1) — 삼키면 사전의 구멍이 영원히 안 보이게 된다
    /// </summary>
    public class MorphemeParserTests
    {
        // ─────────────────────────── 접미사 분리 (설계안 §1-F) ───────────────────────────

        [Test]
        public void 접미사를_최장일치로_뗀다()
        {
            // ⚠⚠ 이 프로젝트에서 가장 잡기 어려운 오독을 막는 테스트다.
            //   `공`(1자)만 떼면 본체가 `천양신` 이 되고, 신이 수식 신(迅, 속도+2)으로 읽힌다.
            //   `신공`(2자)을 떼야 본체가 `천양` 이 된다.
            ParsedArtName parsed = MorphemeParser.Parse("천양신공");

            Assert.AreEqual("신공", parsed.Suffix.Text, "접미사를 최장일치로 떼지 못했다.");
            Assert.AreEqual(2, parsed.Body.Count, "본체가 '천양' 두 자여야 한다. 실제: {0}", parsed);
            Assert.AreEqual('천', parsed.Body[0].Korean);
            Assert.AreEqual('양', parsed.Body[1].Korean);
        }

        [Test]
        public void 창법의_창을_찌르기로_읽지_않는다()
        {
            // 설계안 §1-F 의 두 번째 충돌 — 접미사 `~창법` 과 형태소 창(槍, 찌르기).
            ParsedArtName parsed = MorphemeParser.Parse("참정창법");

            Assert.AreEqual("창법", parsed.Suffix.Text);
            Assert.AreEqual(2, parsed.Body.Count, "본체가 '참정' 두 자여야 한다. 실제: {0}", parsed);
        }

        [Test]
        public void 접미사가_없으면_분해하지_못한다()
        {
            ParsedArtName parsed;
            IReadOnlyList<string> problems;

            Assert.IsFalse(MorphemeParser.TryParse("참정신화", out parsed, out problems),
                "접미사 없는 이름이 통과했다.");
            Assert.AreEqual(1, problems.Count);
        }

        [Test]
        public void 접미사가_유형을_정한다()
        {
            Assert.AreEqual(Discipline.Sword, MorphemeParser.Parse("참정검법").Suffix.Discipline);
            Assert.AreEqual(Discipline.Dagger, MorphemeParser.Parse("사환표법").Suffix.Discipline,
                "비도 접미사 `~표법`(설계안 §1-G 보강)이 비도 유형으로 가지 않는다.");
            Assert.AreEqual(ArtKind.Internal, MorphemeParser.Parse("천양신공").Kind);
            Assert.AreEqual(ArtKind.Movement, MorphemeParser.Parse("신환보").Kind);
        }

        [Test]
        public void 봉법과_편법은_유형을_내놓지_않는다()
        {
            // ⚠ 정의서 §2-4 에는 있으나 Discipline 7종에 대응이 없다. 임의로 매핑하지 않고
            //   무공명 122개 작명 단계로 미뤘다(설계안 §1-G). 조용히 틀린 유형을 주는 것보다 낫다.
            Assert.IsFalse(MorphemeParser.Parse("참정봉법").Suffix.HasDiscipline);
            Assert.IsFalse(MorphemeParser.Parse("참정편법").Suffix.HasDiscipline);
        }

        // ─────────────────────────── R1 — 미등록 글자 ───────────────────────────

        [Test]
        public void R1_미등록_글자는_배경어로_삼켜지지_않는다()
        {
            // ⚠⚠ 배경어 규제의 핵심이다. 복(伏)은 기존 무공 `복호권법` 에 실제로 쓰이는 글자이고
            //   사전에는 없다. 이런 글자가 조용히 넘어가면 역산 리포트(설계안 §5-2)가 무의미해진다.
            ParsedArtName parsed;
            IReadOnlyList<string> problems;

            Assert.IsFalse(MorphemeParser.TryParse("복호권법", out parsed, out problems),
                "미등록 글자가 든 이름이 통과했다.");
            Assert.IsNull(parsed);

            bool mentionsBok = false;
            for (int i = 0; i < problems.Count; i++)
            {
                if (problems[i].Contains("복")) mentionsBok = true;
            }
            Assert.IsTrue(mentionsBok, "미등록 글자 '복' 이 보고되지 않았다. 보고된 것: {0}", string.Join(" · ", problems));
        }

        [Test]
        public void 미등록_글자를_한_번에_모아_보고한다()
        {
            // 역산 리포트(설계안 §5-2)를 위한 경로다. 첫 실패에서 멈추면 "사전에 무엇이 빠졌는가" 를 못 본다.
            //
            // ⚠ `복호권법` 을 쓰지 않는다 — 호(護)는 방어 '막기' 로 **사전에 있어서** 미등록이 하나뿐이다
            //   (설계안 §5-2 의 예시가 `복호권법 → 미등록: 복(伏)` 하나만 적은 것이 맞다).
            //   여러 개를 모으는지 보려면 둘 다 미등록인 이름이 필요하다.
            ParsedArtName parsed;
            IReadOnlyList<string> problems;

            MorphemeParser.TryParse("복묘권법", out parsed, out problems);

            Assert.AreEqual(2, problems.Count,
                "미등록 글자 두 개(복·묘)가 각각 보고돼야 한다. 실제: {0}", string.Join(" · ", problems));
        }

        // ─────────────────────────── 수치 합산 ───────────────────────────

        [Test]
        public void 신환보가_정의서_수치를_낸다()
        {
            // 정의서 §0 의 대표 예시. 신(속도+1) + 환(명중+2/공격−0.75).
            // ⚠⚠ 2026-07-31 밸런싱으로 값이 바뀌었다(속도 2→1 · 공격 −2→−0.75). 축별 환율을
            //   측정으로 맞춘 결과이며, 근거는 정의서 §3-5-a 에 있다. **예시가 바뀐 것이지
            //   시스템이 바뀐 것이 아니다** — 이름에서 수치가 나온다는 성질은 그대로다.
            ParsedArtName parsed = MorphemeParser.Parse("신환보");

            Assert.AreEqual(1, parsed.Delta.Speed, 1e-9);
            Assert.AreEqual(2, parsed.Delta.Accuracy, 1e-9);
            Assert.AreEqual(-0.75, parsed.Delta.Attack, 1e-9);
        }

        [Test]
        public void 종주는_무공형태만_두배로_만든다()
        {
            // 정의서 §3-8 — 종(宗)은 무공형태 효과를 **페널티까지 함께** 2배로 키운다.
            // ⚠ 상위호환이 아니라 "극단으로 미는" 선택이라는 것이 이 테스트의 요지다 — 명중도 함께 두 배로 나빠진다.
            // ⚠ 절대 수치를 박지 않는다 — 무공형태 값은 밸런싱으로 바뀐다(2026-07-31 실제로 바뀌었다).
            //   검증하는 것은 **정(正)의 공격이 한 번 더 얹혔는가** 라는 관계다.
            ParsedArtName plain = MorphemeParser.Parse("참정검법");
            ParsedArtName doubled = MorphemeParser.Parse("참정종검법");
            double formAttack = MorphemeDictionary.Get('정').Delta.Attack;
            double pinnacleAttack = MorphemeDictionary.Get('종').Delta.Attack;

            Assert.AreEqual(plain.Delta.Attack + formAttack + pinnacleAttack, doubled.Delta.Attack, 1e-9,
                "무공형태의 공격이 2배로 들어가지 않았다.");
            Assert.AreEqual(plain.Delta.Accuracy * 2, doubled.Delta.Accuracy, 1e-9,
                "무공형태의 페널티(명중)가 함께 2배가 되지 않았다.");
            Assert.Less(doubled.Delta.Accuracy, plain.Delta.Accuracy, "페널티가 커지지 않았다.");
        }

        // ─────────────────────────── 상성 (정의서 §4) ───────────────────────────

        [Test]
        public void 부정_바로_뒤의_무학분류에만_상성이_붙는다()
        {
            // `낙월` = 달을 떨어뜨린다 → 음기무학에 상성 +1.
            ParsedArtName counter = MorphemeParser.Parse("참정낙월검법");

            Assert.AreEqual(1, counter.AdvantageAgainst(ArtLineage.Yin), "낙월이 음기무학 상성을 만들지 않았다.");
            Assert.AreEqual(0, counter.AdvantageAgainst(ArtLineage.Yang));
        }

        [Test]
        public void 부정이_앞에_없으면_상성이_아니라_분류다()
        {
            // ⚠ 인접 조건이 핵심이다(정의서 §4) — 무엇을 부정하는지가 이름에 명시돼야 상성이 생긴다.
            //   `참정월` 의 월은 부정 대상이 아니라 이 무공 자신의 분류다.
            ParsedArtName tagged = MorphemeParser.Parse("참정월검법");

            Assert.AreEqual(0, tagged.AdvantageAgainst(ArtLineage.Yin), "부정 없이 상성이 붙었다.");
            Assert.AreEqual(ArtLineage.Yin, tagged.Lineage, "무학분류 태그가 붙지 않았다.");
        }

        // ─────────────────────────── 기력 소모 (결정 B) ───────────────────────────

        [Test]
        public void 기력_소모는_유효_형태소_수에_비례한다()
        {
            Assert.AreEqual(2 * MorphemeParser.QiCostPerMorpheme, MorphemeParser.Parse("사환표법").QiCost,
                "2자 무공의 기력 소모가 어긋난다.");
            Assert.AreEqual(4 * MorphemeParser.QiCostPerMorpheme, MorphemeParser.Parse("참정신화검법").QiCost,
                "4자 무공의 기력 소모가 어긋난다.");
        }

        [Test]
        public void 배경어는_기력_소모에서_빠진다()
        {
            // ⚠⚠ 2026-07-29 결정 B 의 핵심. 배경어가 비용을 늘리면 아무도 배경어를 쓰지 않게 되고,
            //   그러면 배경어를 허용한 목적 자체가 사라진다. 배경어는 성능도 비용도 0 이어야 일관된다.
            ParsedArtName parsed = MorphemeParser.Parse("창천낙월검법");

            Assert.AreEqual(4, parsed.Body.Count, "본체는 4자다.");
            Assert.AreEqual(1, parsed.BackgroundCount, "배경어 천(天)이 배경어로 세어지지 않았다.");
            Assert.AreEqual(3, parsed.EffectiveMorphemeCount);
            Assert.AreEqual(3 * MorphemeParser.QiCostPerMorpheme, parsed.QiCost,
                "배경어가 기력 소모에 포함됐다. 4자가 아니라 3자분이어야 한다.");
        }

        [Test]
        public void 배경어는_수치에_기여하지_않는다()
        {
            ParsedArtName withBackground = MorphemeParser.Parse("천양신공");
            ParsedArtName without = MorphemeParser.Parse("양신공");

            Assert.AreEqual(without.Delta.MaxQi, withBackground.Delta.MaxQi, 1e-9,
                "배경어가 수치를 바꿨다.");
        }

        // ─────────────────────────── 조합 규칙 (정의서 §2-2) ───────────────────────────

        [Test]
        public void 적법한_무공은_위반이_없다()
        {
            Assert.AreEqual(0, Violations("참정검법").Count, "공격 무공 최소 구성이 걸렸다.");
            Assert.AreEqual(0, Violations("참정낙월검법").Count, "4자 공격 무공이 걸렸다.");
            Assert.AreEqual(0, Violations("천양신공").Count, "배경어가 든 내공 무공이 걸렸다.");
            Assert.AreEqual(0, Violations("방속보").Count, "경공 무공이 걸렸다.");
        }

        [Test]
        public void 카테고리가_겹치면_걸린다()
        {
            // 정의서 §2-2 규칙 2 — 한 초식이 베면서 동시에 찌를 수 없다.
            AssertHas(Violations("참자검법"), ArtRule.CategoryDuplicate, "공격방식 2자");
            AssertHas(Violations("정중검법"), ArtRule.CategoryDuplicate, "무공형태 2자");
        }

        [Test]
        public void 필수_구성이_빠지면_걸린다()
        {
            // ⚠⚠ 무공형태 필수가 정의서 §2-2 의 핵심 장치다.
            AssertHas(Violations("참신검법"), ArtRule.RequiredMissing, "무공형태 없는 공격 무공");
            AssertHas(Violations("정신검법"), ArtRule.RequiredMissing, "공격방식 없는 공격 무공");
            AssertHas(Violations("신환보"), ArtRule.RequiredMissing, "방어 없는 경공 무공");
        }

        [Test]
        public void 본체가_다섯자면_걸린다()
        {
            IReadOnlyList<ArtRuleViolation> violations = Violations("참정신화독검법");

            AssertHas(violations, ArtRule.BodyLength, "본체 5자");
            AssertHas(violations, ArtRule.OptionalOverflow, "선택 카테고리 3자");
        }

        [Test]
        public void 정의서_예시_중_창천낙월만_살아남았다()
        {
            // ⚠⚠ **판정이 2026-07-29 에 바뀐 항목이다. 지우지 않고 남긴다.**
            //   처음에는 정의서 §0·§2-1 의 예시 세 개가 **전부** 조합 규칙을 어겼다(설계안 §1-A).
            //   그 뒤 두 번의 사용자 결정이 상황을 바꿨다:
            //     ① 배경어 허용 → 천(天)이 미등록 글자에서 벗어났다
            //     ② **상성 무공은 무공형태 면제** → 창천낙월의 마지막 위반이 사라졌다
            //   결과: **창천낙월은 적법한 무공이 됐고, 나머지 둘은 여전히 어긴다.**

            Assert.AreEqual(0, ArtCompositionRule.Validate(MorphemeParser.Parse("창천낙월", ArtKind.Attack)).Count,
                "창천낙월이 아직 규칙에 걸린다 — 상성 면제가 적용되지 않았다.");

            // 신환보는 경공인데 방어 형태소가 없다. 예시가 어감 설명용이었을 뿐 무공이 아니다.
            AssertHas(Violations("신환보"), ArtRule.RequiredMissing, "신환보 — 방어 없음");

            // 암중독환은 중을 重(무공형태)으로 읽으므로 환(幻)과 함께 무공형태 2자가 된다.
            // 초안이 "중은 사전에 없는 글자" 라고 본 것은 한자 中 에 대해서만 맞다 — 한글 '중' 은 重 이 선점했다.
            IReadOnlyList<ArtRuleViolation> dark = Violations("암중독환표법");
            AssertHas(dark, ArtRule.CategoryDuplicate, "암중독환 — 무공형태 2자(중重·환幻)");
            AssertHas(dark, ArtRule.RequiredMissing, "암중독환 — 공격방식 없음");
        }

        // ─────────────────────────── 배경어 규제 R2 · R3 ───────────────────────────

        [Test]
        public void R2_배경어는_무공당_한_자다()
        {
            // 규칙 2(카테고리당 1자)에서 자동으로 나온다. 코드가 따로 집행하지 않으므로 테스트로 고정한다.
            AssertHas(Violations("천천참정검법"), ArtRule.CategoryDuplicate, "배경어 2자");
        }

        [Test]
        public void R3_배경어만으로는_필수를_채울_수_없다()
        {
            // 규칙 3 에서 자동으로 나온다. 배경어는 필수 카테고리가 아니다.
            AssertHas(Violations("천검법"), ArtRule.RequiredMissing, "배경어뿐인 공격 무공");
        }

        [Test]
        public void 배경어는_선택_카테고리_상한에_들어가지_않는다()
        {
            // 배경어는 성능 카테고리가 아니므로 "나머지 중 최대 2" 에 세지 않는다.
            // 대신 규칙 1 의 4자 제한이 대가를 물린다 — 배경어 1자 = 성능 슬롯 1개 포기.
            Assert.AreEqual(0, Violations("천참정신검법").Count,
                "배경어가 선택 카테고리로 세어졌다. 참(공격방식)+정(무공형태)+신(수식1) 이면 적법하다.");
        }

        // ─────────────────────────── 극한경지 (정의서 §5-2) ───────────────────────────

        [Test]
        public void 극한경지는_전승무학에만_쓸_수_있다()
        {
            ParsedArtName parsed = MorphemeParser.Parse("참정종검법");

            AssertHas(ArtCompositionRule.Validate(parsed), ArtRule.PinnacleRestricted, "일반 무공의 극한경지");
            Assert.AreEqual(0, ArtCompositionRule.Validate(parsed, ArtTier.Legacy).Count,
                "전승무학인데도 극한경지가 걸렸다.");
        }

        [Test]
        public void 극한경지는_무공당_한_자다()
        {
            // 정의서 §5-2 제약 2 — `제패`(황제+패도) 같은 중첩 금지. 규칙 2 가 잡는다.
            ParsedArtName parsed = MorphemeParser.Parse("참정제패검법");

            AssertHas(ArtCompositionRule.Validate(parsed, ArtTier.Legacy),
                ArtRule.CategoryDuplicate, "극한경지 2자");
        }

        // ─────────────────────────── 범위 (정의서 §3-12) ───────────────────────────

        [Test]
        public void 광역은_대문파_무공부터_쓸_수_있다()
        {
            // 2026-07-30 확정 — 광역은 계층의 보상이므로 강호무학·소문파에는 두지 않는다.
            ParsedArtName parsed = MorphemeParser.Parse("참정군", ArtKind.Attack);

            AssertHas(ArtCompositionRule.Validate(parsed, ArtTier.Minor), ArtRule.ScopeRestricted, "소문파의 광역");
            Assert.AreEqual(0, ArtCompositionRule.Validate(parsed, ArtTier.Major).Count,
                "대문파인데도 광역이 걸렸다.");
        }

        [Test]
        public void 광역은_공격_무공에만_쓸_수_있다()
        {
            // 내공·경공은 때리는 대상이 없다.
            ParsedArtName parsed = MorphemeParser.Parse("양전공", ArtKind.Internal);

            AssertHas(ArtCompositionRule.Validate(parsed, ArtTier.Major), ArtRule.ScopeRestricted, "내공 무공의 광역");
        }

        [Test]
        public void 만은_기력_소모를_세배로_만든다()
        {
            // ⚠ 여기서 정의서 §1-1-a 의 기준 소모량이 비로소 곱할 대상을 갖는다(설계안 §1-B 가 지적한 빈칸).
            //   4자 무공 기준 16 → 48 이라 기력 50 으로 사실상 한 번 쓰고 고갈된다. 필살기 성격이다.
            ParsedArtName plain = MorphemeParser.Parse("참정화월", ArtKind.Attack);
            ParsedArtName wide = MorphemeParser.Parse("참정화만", ArtKind.Attack);

            Assert.AreEqual(4 * MorphemeParser.QiCostPerMorpheme, plain.QiCost);
            Assert.AreEqual(plain.QiCost * 3, wide.QiCost, "만(萬)이 기력 소모를 3배로 만들지 않았다.");
            Assert.AreEqual(AttackScope.All, wide.Scope);
        }

        [Test]
        public void 범위_형태소가_없으면_단일_대상이다()
        {
            Assert.AreEqual(AttackScope.Single, MorphemeParser.Parse("참정검법").Scope);
        }

        // ─────────────────────────── 도우미 ───────────────────────────

        private static IReadOnlyList<ArtRuleViolation> Violations(string name)
        {
            return ArtCompositionRule.Validate(MorphemeParser.Parse(name));
        }

        private static void AssertHas(IReadOnlyList<ArtRuleViolation> violations, ArtRule expected, string what)
        {
            for (int i = 0; i < violations.Count; i++)
            {
                if (violations[i].Rule == expected) return;
            }

            var messages = new List<string>();
            for (int i = 0; i < violations.Count; i++) messages.Add(violations[i].Message);
            Assert.Fail("{0} 이(가) {1} 로 걸리지 않았다. 실제 위반: {2}",
                what, expected, messages.Count == 0 ? "없음" : string.Join(" · ", messages));
        }
    }
}
