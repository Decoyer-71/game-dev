using System.Collections.Generic;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;
using NUnit.Framework;

namespace Jianghu.Tests.Martial
{
    /// <summary>
    /// 실제 무공명이 조합 규칙을 지키는지 검증한다 (정의서 §7-E 의 122개 작명분).
    ///
    /// ⚠⚠ **계층 = 형태소 개수** (2026-07-29 사용자 결정):
    ///   강호무학은 `~검법` 처럼 **접미사를 쓰고 본체가 2자**뿐이라 형태소가 둘이다.
    ///   문파 무공은 접미사 없이 **4자를 전부 형태소로** 쓴다. 즉 이름의 길이가 곧 세기다.
    ///   접미사는 **강호무학 전용**이며, 그래서 강호무학이 구조적으로 가장 약하다.
    ///
    /// 이 파일은 지어낸 이름이 규칙을 지키는지만 본다. **재미있는 이름인가는 사람이 판정한다.**
    /// </summary>
    public class ArtNamingTests
    {
        /// <summary>
        /// 강호무학 9종 — 공격 5(유형별) + 내공 2 + 경공 2. 정의서 §5-1.
        ///
        /// 공격 5종에 무공형태 5종을 **하나씩 배분**했다. 그래야 시작 무공 다섯이
        /// 전부 다른 성격이 되고, 플레이어가 첫 선택에서 형태 차이를 배운다.
        /// </summary>
        private static readonly string[] WandererArts =
        {
            // 공격 5 — 유형별로 하나씩. 본체 2자 = 공격방식 + 무공형태(둘 다 필수).
            "절정검법",  // 截正 — 베기 + 정직
            "벌중도법",  // 伐重 — 베기 + 무거움
            "자쾌창법",  // 刺快 — 찌르기 + 빠름
            "타환권법",  // 打幻 — 때리기 + 기만
            "투유표법",  // 投柔 — 던지기 + 변화

            // 내공 2 — 양(陽)과 음(陰)을 갈라 성격을 대비시킨다.
            "양화신공",  // 陽火 — 최대기력 + 공격
            "음유심법",  // 陰柔 — 기력회복 + 명중

            // 경공 2 — 회피형과 반격형.
            "피신보",    // 避迅 — 회피 + 속도
            "반유신법",  // 反柔 — 반격 + 명중
        };

        [Test]
        public void 강호무학_아홉종이_조합_규칙을_지킨다()
        {
            foreach (string name in WandererArts)
            {
                ParsedArtName parsed;
                IReadOnlyList<string> problems;

                Assert.IsTrue(MorphemeParser.TryParse(name, out parsed, out problems),
                    "{0} 을(를) 분해하지 못했다: {1}", name, string.Join(" · ", problems));

                IReadOnlyList<ArtRuleViolation> violations = ArtCompositionRule.Validate(parsed);

                var messages = new List<string>();
                for (int i = 0; i < violations.Count; i++) messages.Add(violations[i].Message);
                Assert.AreEqual(0, violations.Count,
                    "{0} 이(가) 규칙을 어긴다: {1}", name, string.Join(" · ", messages));
            }
        }

        [Test]
        public void 강호무학은_형태소가_둘뿐이라_가장_약하다()
        {
            // ⚠⚠ 계층 = 형태소 개수라는 설계의 핵심이다. 강호무학이 2형태소인 것은
            //   빈약해서가 아니라 **의도된 바닥**이다. 문파에 들어가면 3자·4자로 올라간다.
            foreach (string name in WandererArts)
            {
                ParsedArtName parsed = MorphemeParser.Parse(name);
                Assert.AreEqual(2, parsed.Body.Count, "{0} 의 본체가 2자가 아니다.", name);
            }
        }

        [Test]
        public void 강호무학_공격_다섯은_무공형태가_모두_다르다()
        {
            // 시작 무공 다섯이 같은 형태를 쓰면 "유형만 다른 같은 무공" 이 된다.
            var forms = new HashSet<char>();
            for (int i = 0; i < 5; i++)
            {
                ParsedArtName parsed = MorphemeParser.Parse(WandererArts[i]);
                foreach (Morpheme m in parsed.Body)
                {
                    if (m.Category != MorphemeCategory.Form) continue;
                    Assert.IsTrue(forms.Add(m.Korean),
                        "{0} 의 무공형태 {1} 이(가) 앞선 무공과 겹친다.", WandererArts[i], m);
                }
            }
            Assert.AreEqual(5, forms.Count, "무공형태 5종이 하나씩 배분되지 않았다.");
        }

        // ─────────────────────────── 소문파 28종 (7문파 × 4) ───────────────────────────

        private sealed class Draft
        {
            public string School { get; }
            public string Name { get; }
            public ArtKind Kind { get; }

            public Draft(string school, string name, ArtKind kind)
            {
                School = school;
                Name = name;
                Kind = kind;
            }
        }

        /// <summary>
        /// 소문파 7곳 × 4종(공격 2 · 내공 1 · 경공 1) = 28종. 정의서 §5-1.
        ///
        /// **본체 3자 · 접미사 없음.** 강호무학(2자)보다 형태소가 하나 많고, 대문파(3~4자)보다 하나 적다.
        ///
        /// 문파의 특화 병기(`SchoolCatalog`)가 공격방식을 정하고, 성향이 상태이상을 정한다:
        ///   정파 → 탈(奪 기력소실) · 사파 → 혈(血)·독(毒) · 마도 → 비(痺 마비)·경(硬 경직)
        ///
        /// ⚠ **어감은 사람이 판정한다.** 이 테스트가 보는 것은 규칙 준수뿐이다.
        ///   형태소 체계의 이점이 여기서 나온다 — 어감이 마음에 안 들면 **글자만 바꾸면
        ///   수치가 알아서 따라온다.** 수치를 다시 조정할 필요가 없다.
        /// </summary>
        private static readonly Draft[] MinorSchoolArts =
        {
            // ── 종남파 (정파 · 창) ──
            // 특징: '매우' '정직한' '찌르기' 창술이 특징인 문파. 종남산 도가. 색은 바람(風).
            new Draft("종남파", "정풍창", ArtKind.Attack),      // 正風槍 — 정직 + 바람 + 찌르기
            new Draft("종남파", "정탈창", ArtKind.Attack),      // 正奪槍 — 정직하게 찔러 기력을 뺏는다
            new Draft("종남파", "신풍양공", ArtKind.Internal),  // 迅風陽功 — 빠르다 + 바람 + 양기
            new Draft("종남파", "쾌풍섬보", ArtKind.Movement),  // 快風閃步 — 빠름 + 바람 + 회피

            // ── 점창파 (정파 · 검) ──
            // 특징: '매우' '빠른' '찌르기' 검술이 특징인 문파. ✅ 정의서 §6-1 이 이미 정해 둔 문장이다.
            // ⚠⚠ `창천낙월` 은 사용자 지정 무공이다 — 정의서 §2-1 의 사용례를 그대로 살렸다.
            //   "하늘을 찔러(창천) 달을 떨어뜨린다(낙월)" → 낙+월 인접으로 **음기무학 상성 +1**.
            //   무공형태가 없지만 **상성 무공이라 면제**된다.
            new Draft("점창파", "창천낙월", ArtKind.Attack),    // 槍天落月 — 찌르기 + 배경어 + 부정 + 음기무학
            new Draft("점창파", "쾌자탈", ArtKind.Attack),      // 快刺奪 — 빠르게 찔러 기력을 뺏는다
            new Draft("점창파", "양명정공", ArtKind.Internal),  // 陽明正功 — 양기 + 밝다 + 정직
            new Draft("점창파", "쾌섬신술", ArtKind.Movement),  // 快閃迅術 — 빠름 + 회피 + 빠르다

            // ── 하북팽가 (정파 · 도) ──
            // 특징: '극히' '무거운' '베기' 도법이 특징인 세가. 색은 불(火).
            new Draft("하북팽가", "중화참", ArtKind.Attack),    // 重火斬 — 무겁게 불처럼 벤다
            new Draft("하북팽가", "후탈벌", ArtKind.Attack),    // 厚奪伐 — 두텁게 베어 기력을 뺏는다
            new Draft("하북팽가", "화중양공", ArtKind.Internal),// 火重陽功 — 불 + 무거움 + 양기
            new Draft("하북팽가", "항중화보", ArtKind.Movement),// 抗重火步 — 막기 + 무거움 + 불

            // ── 녹림 (사파 · 도) ──
            // 특징: '매우' '기만적인' '베기' 도법과 '출혈'이 특징인 무리. 산적의 매복·기습.
            new Draft("녹림", "환혈참", ArtKind.Attack),        // 幻血斬 — 기만하여 베고 피를 낸다
            new Draft("녹림", "궤독벌", ArtKind.Attack),        // 詭毒伐 — 속여서 베고 중독시킨다
            new Draft("녹림", "암음혈공", ArtKind.Internal),    // 暗陰血功 — 어둡다 + 음기 + 출혈
            new Draft("녹림", "둔암유보", ArtKind.Movement),    // 遁暗柔步 — 회피 + 어둡다 + 변화

            // ── 장강수로채 (사파 · 비도) ──
            // 특징: '매우' '변화하는' '던지기' 암기술과 '중독'이 특징인 수적. 색은 물(水).
            new Draft("장강수로채", "유독척", ArtKind.Attack),     // 柔毒擲 — 부드럽게 던져 중독시킨다
            new Draft("장강수로채", "변혈투", ArtKind.Attack),     // 變血投 — 변화하며 던져 피를 낸다
            new Draft("장강수로채", "음수유공", ArtKind.Internal), // 陰水柔功 — 음기 + 물 + 변화
            new Draft("장강수로채", "유수피술", ArtKind.Movement), // 柔水避術 — 변화 + 물 + 회피

            // ── 시마궁 (마도 · 권) ──
            // 특징: '매우' '기만적인' '때리기' 권법과 '마비'가 특징인 문파. 색은 어둡다(夜·暗).
            new Draft("시마궁", "환비격", ArtKind.Attack),      // 幻痺擊 — 환영처럼 때려 마비시킨다
            new Draft("시마궁", "야궤타", ArtKind.Attack),      // 夜詭打 — 어둠 속에서 속여 때린다
            new Draft("시마궁", "암음궤공", ArtKind.Internal),  // 暗陰詭功 — 어둡다 + 음기 + 기만
            new Draft("시마궁", "반환야보", ArtKind.Movement),  // 反幻夜步 — 반격 + 기만 + 어둡다

            // ── 흑문 (마도 · 창) ──
            // 특징: '극히' '무거운' '찌르기' 창술과 '경직'이 특징인 문파. 색은 차갑다(寒·暗).
            new Draft("흑문", "중경창", ArtKind.Attack),        // 重硬槍 — 무겁게 찔러 굳게 만든다
            new Draft("흑문", "암중자", ArtKind.Attack),        // 暗重刺 — 어둠 속에서 무겁게 찌른다
            new Draft("흑문", "한음중공", ArtKind.Internal),    // 寒陰重功 — 차갑다 + 음기 + 무거움
            new Draft("흑문", "항암중보", ArtKind.Movement),    // 抗暗重步 — 막기 + 어둡다 + 무거움
        };

        [Test]
        public void 소문파_스물여덟종이_조합_규칙을_지킨다()
        {
            foreach (Draft draft in MinorSchoolArts)
            {
                ParsedArtName parsed;
                IReadOnlyList<string> problems;

                Assert.IsTrue(MorphemeParser.TryParse(draft.Name, draft.Kind, out parsed, out problems),
                    "{0} 의 {1} 을(를) 분해하지 못했다: {2}",
                    draft.School, draft.Name, string.Join(" · ", problems));

                // ⚠ 계층을 명시해야 계층별 제약이 실제로 걸린다 — 소문파는 **광역(범위) 형태소를 쓸 수 없다**
                //   (2026-07-30 확정: 광역은 대문파 무공부터). 계층을 빼면 이 제약이 검사되지 않는다.
                IReadOnlyList<ArtRuleViolation> violations = ArtCompositionRule.Validate(parsed, ArtTier.Minor);

                var messages = new List<string>();
                for (int i = 0; i < violations.Count; i++) messages.Add(violations[i].Message);
                Assert.AreEqual(0, violations.Count,
                    "{0} 의 {1} 이(가) 규칙을 어긴다: {2}",
                    draft.School, draft.Name, string.Join(" · ", messages));
            }
        }

        [Test]
        public void 소문파는_문파당_공격둘_내공하나_경공하나다()
        {
            // 정의서 §5-1 의 소문파 구성. 슬롯이 어긋나면 문파의 무공 풀이 기울어진다.
            var counts = new Dictionary<string, Dictionary<ArtKind, int>>();
            foreach (Draft draft in MinorSchoolArts)
            {
                if (!counts.ContainsKey(draft.School)) counts[draft.School] = new Dictionary<ArtKind, int>();
                Dictionary<ArtKind, int> byKind = counts[draft.School];
                byKind[draft.Kind] = byKind.ContainsKey(draft.Kind) ? byKind[draft.Kind] + 1 : 1;
            }

            Assert.AreEqual(7, counts.Count, "소문파는 7곳이다.");
            foreach (KeyValuePair<string, Dictionary<ArtKind, int>> school in counts)
            {
                Assert.AreEqual(2, Count(school.Value, ArtKind.Attack), "{0} 의 공격 무공", school.Key);
                Assert.AreEqual(1, Count(school.Value, ArtKind.Internal), "{0} 의 내공 무공", school.Key);
                Assert.AreEqual(1, Count(school.Value, ArtKind.Movement), "{0} 의 경공 무공", school.Key);
            }
        }

        [Test]
        public void 소문파는_성능_형태소가_셋이다()
        {
            // ⚠⚠ 계층 = **형태소 개수**이지 글자 수가 아니다. 둘은 두 가지 이유로 어긋난다:
            //   ⓐ **접미사는 본체 글자 수에 안 들어간다**(§2-2 규칙 1 은 "접미사 제외").
            //      `신풍양공` 은 본체 `신풍양` 3자 + 접미사 `공` 이며 `신풍양` 과 성능이 같다.
            //      즉 접미사는 성능을 깎지 않고 어감만 얻는 **공짜 장치**다.
            //   ⓑ **배경어는 성능 형태소가 아니다.** `창천낙월` 은 본체 4자지만 천(天)이 배경어라 성능은 3이다.
            //
            //   그래서 세는 것은 `EffectiveMorphemeCount`(본체 − 배경어)이고, 기력 소모도 여기서 나온다.
            foreach (Draft draft in MinorSchoolArts)
            {
                ParsedArtName parsed = MorphemeParser.Parse(draft.Name, draft.Kind);

                Assert.AreEqual(3, parsed.EffectiveMorphemeCount,
                    "{0} 의 성능 형태소가 3개가 아니다 ({1}).", draft.Name, parsed);
                Assert.LessOrEqual(parsed.Body.Count, ArtCompositionRule.MaxBodyLength,
                    "{0} 의 본체가 4자를 넘는다.", draft.Name);
                Assert.AreEqual(3 * MorphemeParser.QiCostPerMorpheme, parsed.QiCost, "{0} 의 기력 소모", draft.Name);
            }
        }

        [Test]
        public void 문파_무공은_무기_접미사를_쓰지_않는다()
        {
            // 무기 접미사(`~검법`·`~도법`)는 **강호무학 전용**이다(2026-07-29 사용자 결정).
            // 문파 무공은 1자 접미사(보·공·결·법·술)만 어감용으로 쓴다.
            foreach (Draft draft in MinorSchoolArts)
            {
                ParsedArtName parsed = MorphemeParser.Parse(draft.Name, draft.Kind);
                if (!parsed.HasSuffix) continue;

                Assert.AreEqual(1, parsed.Suffix.Text.Length,
                    "{0} 이(가) 무기 접미사 '{1}' 을(를) 썼다 — 강호무학 전용이다.", draft.Name, parsed.Suffix);
            }
        }

        [Test]
        public void 창천낙월은_상성_무공이라_무공형태가_면제된다()
        {
            // ⚠⚠ 사용자 지정 무공(정의서 §2-1 사용례). "하늘을 찔러 달을 떨어뜨린다."
            //   이 이름이 성립하려면 무공형태(형용사)가 자리를 비켜야 한다 — 문장이 [동사][목적어] 구조이기 때문이다.
            //   면제 근거는 **상성 무공은 부정·무학분류 2자가 수치 0이라 이미 위력을 크게 포기했다**는 것이다.
            ParsedArtName parsed = MorphemeParser.Parse("창천낙월", ArtKind.Attack);

            Assert.AreEqual(0, parsed.CountOf(MorphemeCategory.Form), "무공형태가 들어 있다.");
            Assert.AreEqual(1, parsed.AdvantageAgainst(ArtLineage.Yin), "낙월이 음기무학 상성을 만들지 않았다.");
            Assert.AreEqual(0, ArtCompositionRule.Validate(parsed).Count, "면제가 적용되지 않았다.");

            // ⚠ 면제는 상성 무공에만이다. 상성이 없으면 여전히 무공형태가 필요하다.
            ParsedArtName noCounter = MorphemeParser.Parse("창천월", ArtKind.Attack);
            AssertHasRule(ArtCompositionRule.Validate(noCounter), ArtRule.RequiredMissing);
        }

        private static void AssertHasRule(IReadOnlyList<ArtRuleViolation> violations, ArtRule expected)
        {
            for (int i = 0; i < violations.Count; i++)
            {
                if (violations[i].Rule == expected) return;
            }
            Assert.Fail("{0} 위반이 검출되지 않았다.", expected);
        }

        // ─────────────────────────── 대문파 72종 (9문파 × 8) ───────────────────────────

        /// <summary>
        /// 대문파 9곳 × 8종(공격 4 = 무기당 2 · 내공 2 · 경공 2) = 72종. 정의서 §5-1.
        ///
        /// **성능 형태소 3~4 · 기력 소모 12~16.** 소문파(3)보다 한 칸 넓고, 그 폭이 곧 대문파의 이점이다.
        /// 4자 중 1자를 배경어로 쓰면 성능은 3이 되고 기력도 12로 내려간다 — **어감을 사면 성능과 비용이 함께 내려간다.**
        ///
        /// 대문파에만 있는 것 둘:
        ///   · **광역(범위) 형태소** — 문파당 1종씩 뒀다. 계층의 보상이다(정의서 §3-12)
        ///   · **네 칸짜리 이름** — 상태이상과 자연속성을 함께 담을 수 있다
        ///
        /// 그리고 **문파마다 상성 무공 1종**을 뒀다. 부정+무학분류가 붙는 문장형 이름이라
        /// 무공명이 그대로 문장으로 읽힌다 — `타천멸일`(하늘을 쳐서 해를 멸한다).
        /// </summary>
        private static readonly Draft[] MajorSchoolArts =
        {
            // ── 소림사 (정파 · 혼합무학 · 창·권) ── 【광역】
            // 특징: '무거운' '때리기' 권법과 '찌르기' 창술. 나한진 — 여럿을 동시에 상대한다. 색은 벼락(雷).
            // ⚠⚠ **정파인데 마비를 건다** — 점혈(點穴) 수법. 사천당가에 이어 두 번째 예외다.
            new Draft("소림사", "중뇌창", ArtKind.Attack),      // 重雷槍 — 무겁게 벼락처럼 찌른다
            new Draft("소림사", "정천창군", ArtKind.Attack),    // 正天槍群 — 하늘을 정직하게 찔러 무리를 친다 [광역 3인]
            new Draft("소림사", "후격비혼", ArtKind.Attack),    // 厚擊痺混 — 두텁게 쳐서 혈을 짚는다 [혼합무학]
            new Draft("소림사", "직뇌타비", ArtKind.Attack),    // 直雷打痺 — 벼락같이 곧게 쳐서 마비시킨다
            new Draft("소림사", "중뇌양공", ArtKind.Internal),  // 重雷陽功
            new Draft("소림사", "명식후결", ArtKind.Internal),  // 明息厚訣 — 조식(調息) 【종교】
            new Draft("소림사", "계중명보", ArtKind.Movement),  // 戒重明步 — 지계(持戒)로 막는다 【종교】
            new Draft("소림사", "응후급보", ArtKind.Movement),  // 應厚急步

            // ── 무당파 (정파 · 혼합무학 · 검·권) ── 【상성】
            // 특징: '변화하는' '베기' 검술과 '때리기' 권법. 태극 — 상극을 읽어 친다. 색은 물(水)·구름(雲).
            new Draft("무당파", "유수참탈", ArtKind.Attack),    // 柔水斬奪 — 물처럼 부드럽게 베어 기력을 뺏는다
            new Draft("무당파", "참천멸월", ArtKind.Attack),    // 斬天滅月 — 하늘을 베어 달을 멸한다 [상성 음기]
            new Draft("무당파", "유운박탈", ArtKind.Attack),    // 柔雲拍奪 — 구름처럼 부드럽게 쳐서 기력을 뺏는다
            new Draft("무당파", "변현격탈", ArtKind.Attack),    // 變玄擊奪 — 현묘하게 변화하며 쳐서 기력을 뺏는다 【종교】
            new Draft("무당파", "유식수공", ArtKind.Internal),  // 柔息水功 — 조식(調息) 【종교】
            new Draft("무당파", "변명양결", ArtKind.Internal),  // 變明陽訣
            new Draft("무당파", "피현유보", ArtKind.Movement),  // 避玄柔步 — 현묘하여 잡히지 않는다 【종교】
            new Draft("무당파", "역변명보", ArtKind.Movement),  // 逆變明步

            // ── 화산파 (정파 · 양기무학 · 검·도) ── 【가중치】
            // 특징: '매우' '빠른' '베기' 검술과 도법. 매화 — 한 번에 여러 번 벤다(타격 횟수 +1). 색은 바람(風).
            new Draft("화산파", "쾌풍절탈", ArtKind.Attack),    // 快風截奪 — 바람처럼 빠르게 베어 기력을 뺏는다
            new Draft("화산파", "유명참일", ArtKind.Attack),    // 柔明斬日 — 밝고 부드럽게 벤다 [양기무학]
            new Draft("화산파", "급쾌벌탈", ArtKind.Attack),    // 急快伐奪 — 급하고 빠르게 베어 기력을 뺏는다
            new Draft("화산파", "풍정단", ArtKind.Attack),      // 風正斷 — 바람처럼 곧게 끊는다
            new Draft("화산파", "쾌풍양공", ArtKind.Internal),  // 快風陽功
            new Draft("화산파", "신음유결", ArtKind.Internal),  // 迅陰柔訣
            new Draft("화산파", "섬풍쾌보", ArtKind.Movement),  // 閃風快步
            new Draft("화산파", "반신풍보", ArtKind.Movement),  // 反迅風步

            // ── 남궁세가 (정파 · 양기무학 · 검·도) ── 【없음 — 순수 위력】
            // 특징: '정직한' '베기' 검술과 도법. 제왕검형 — 잔재주 없이 정면으로 가장 강하다. 색은 불(火).
            // ⚠⚠ 광역도 상성도 가중치도 없다. **그것이 이 문파의 정체성이다** — 네 슬롯을 전부
            //   위력축에 쓰므로 순수 공격력이 가장 높다. 광역·상성 무공은 수치 0인 형태소에 슬롯을 쓴다.
            new Draft("남궁세가", "정화참탈", ArtKind.Attack),  // 正火斬奪 — 불처럼 정직하게 베어 기력을 뺏는다
            new Draft("남궁세가", "직천참일", ArtKind.Attack),  // 直天斬日 — 하늘을 곧게 벤다 [양기무학]
            new Draft("남궁세가", "직명벌탈", ArtKind.Attack),  // 直明伐奪 — 밝고 곧게 베어 기력을 뺏는다
            new Draft("남궁세가", "정화단광", ArtKind.Attack),  // 正火斷光 — 불처럼 정직하게 끊는다 [공격 최대치]
            new Draft("남궁세가", "정화양공", ArtKind.Internal),// 正火陽功
            new Draft("남궁세가", "직명합결", ArtKind.Internal),// 直明合訣
            new Draft("남궁세가", "방정화보", ArtKind.Movement),// 防正火步
            new Draft("남궁세가", "반직명보", ArtKind.Movement),// 反直明步

            // ── 사천당가 (정파 · 혼합무학 · 권·비도) ── 【가중치】
            // 특징: '극히' '빠른' '던지기' 암기술과 '독공'. ✅ 정의서 §6-1 이 이미 정해 둔 문장이다.
            // ⚠⚠ 정파이면서 중독을 거는 예외 문파다(§5-5). 그래서 탈(奪)이 아니라 독(毒)을 쓴다.
            new Draft("사천당가", "쾌독척", ArtKind.Attack),    // 快毒擲 — 빠르게 던져 중독시킨다
            new Draft("사천당가", "궤암포독", ArtKind.Attack),  // 詭暗拋毒 — 어둠 속에서 속여 던져 중독시킨다
            new Draft("사천당가", "환독타", ArtKind.Attack),    // 幻毒打 — 환영처럼 때려 중독시킨다
            new Draft("사천당가", "유암격독", ArtKind.Attack),  // 柔暗擊毒 — 어둠 속에서 부드럽게 쳐서 중독시킨다
            new Draft("사천당가", "암음독공", ArtKind.Internal),// 暗陰毒功
            new Draft("사천당가", "쾌양독결", ArtKind.Internal),// 快陽毒訣
            new Draft("사천당가", "둔암쾌보", ArtKind.Movement),// 遁暗快步
            new Draft("사천당가", "피급독술", ArtKind.Movement),// 避急毒術

            // ── 서량군문 (사파 · 양기무학 · 창·도) ── 【광역】
            // 특징: '무거운' '찌르기' 창술과 '베기' 도법 + 출혈. 군진 — 무리를 함께 벤다. 색은 차가움(冷).
            new Draft("서량군문", "중혈창", ArtKind.Attack),    // 重血槍 — 무겁게 찔러 피를 낸다
            new Draft("서량군문", "후냉자혈", ArtKind.Attack),  // 厚冷刺血 — 두텁고 차갑게 찔러 피를 낸다
            new Draft("서량군문", "중명벌혈", ArtKind.Attack),  // 重明伐血 — 무겁고 밝게 베어 피를 낸다
            new Draft("서량군문", "후참혈군", ArtKind.Attack),  // 厚斬血群 — 두텁게 베어 무리에게 피를 낸다 [광역 3인]
            new Draft("서량군문", "중냉양공", ArtKind.Internal),// 重冷陽功
            new Draft("서량군문", "야음혈결", ArtKind.Internal),// 夜陰血訣
            new Draft("서량군문", "항중냉보", ArtKind.Movement),// 抗重冷步
            new Draft("서량군문", "거후혈보", ArtKind.Movement),// 拒厚血步

            // ── 살문 (사파 · 음기무학 · 검·비도) ── 【상성】
            // 특징: '어두운' '던지기' 암기술과 '베기' 검술 + 출혈. 약점을 읽어 친다. 색은 밤(夜)·어둠(暗).
            new Draft("살문", "환야절혈", ArtKind.Attack),      // 幻夜截血 — 어둠 속에서 현혹해 베고 피를 낸다
            new Draft("살문", "절해망혼", ArtKind.Attack),      // 截海亡混 — 바다를 베어 혼을 없앤다 [상성 혼합]
            new Draft("살문", "궤암척혈", ArtKind.Attack),      // 詭暗擲血 — 어둠 속에서 속여 던져 피를 낸다
            new Draft("살문", "환한투독", ArtKind.Attack),      // 幻寒投毒 — 차갑게 현혹하며 던져 중독시킨다
            new Draft("살문", "야음환공", ArtKind.Internal),    // 夜陰幻功
            new Draft("살문", "한양궤결", ArtKind.Internal),    // 寒陽詭訣
            new Draft("살문", "둔야환보", ArtKind.Movement),    // 遁夜幻步
            new Draft("살문", "섬암궤술", ArtKind.Movement),    // 閃暗詭術

            // ── 천마신교 (마도 · 음기무학 · 검·권) ── 【없음 — 순수 위력】
            // 특징: '무거운' '베기' 검술과 '때리기' 권법 + 경직. 정면으로 압도한다. 색은 차갑다(寒).
            // ⚠ 남궁세가와 같은 자리다 — 특수 능력 없이 위력으로 이긴다. 정파와 마도 양쪽에 하나씩 뒀다.
            new Draft("천마신교", "중한참경", ArtKind.Attack),  // 重寒斬硬 — 무겁고 차갑게 베어 굳게 만든다
            new Draft("천마신교", "후냉절월", ArtKind.Attack),  // 厚冷截月 — 두텁고 차갑게 벤다 [음기무학]
            new Draft("천마신교", "중야격경", ArtKind.Attack),  // 重夜擊硬 — 어둠 속에서 무겁게 쳐서 굳게 만든다
            new Draft("천마신교", "후암타비", ArtKind.Attack),  // 厚暗打痺 — 어둠 속에서 두텁게 쳐서 마비시킨다
            new Draft("천마신교", "중한양공", ArtKind.Internal),// 重寒陽功
            new Draft("천마신교", "후음비결", ArtKind.Internal),// 厚陰痺訣
            new Draft("천마신교", "호중한보", ArtKind.Movement),// 護重寒步
            new Draft("천마신교", "역후비보", ArtKind.Movement),// 逆厚痺步

            // ── 혈교 (마도 · 혼합무학 · 권·창) ── 【광역 만(萬)】
            // 특징: '기만적인' '때리기' 권법과 '찌르기' 창술 + 출혈. 만인을 상대하는 광란. 색은 불(火).
            // ⚠⚠ **마도인데 출혈을 건다** — 혈교(血敎)가 피를 쓰지 않는 편이 부자연스럽다. 세 번째 예외다.
            new Draft("혈교", "환화격혈", ArtKind.Attack),      // 幻火擊血 — 불처럼 현혹하며 쳐서 피를 낸다
            new Draft("혈교", "궤화박경", ArtKind.Attack),      // 詭火拍硬 — 불처럼 속이며 쳐서 굳게 만든다
            new Draft("혈교", "환몽자혈", ArtKind.Attack),      // 幻夢刺血 — 꿈처럼 현혹해 찔러 피를 낸다
            new Draft("혈교", "환창혈만", ArtKind.Attack),      // 幻槍血萬 — 만인을 현혹해 찔러 피를 낸다 [광역 전원·기력 3배]
            new Draft("혈교", "환화양공", ArtKind.Internal),    // 幻火陽功
            new Draft("혈교", "휘음궤결", ArtKind.Internal),    // 輝陰詭訣
            new Draft("혈교", "어환화보", ArtKind.Movement),    // 禦幻火步
            new Draft("혈교", "반궤경보", ArtKind.Movement),    // 反詭硬步
        };

        // ─────────────────────────── 전승무학 9종 (대문파당 1) ───────────────────────────

        /// <summary>
        /// 전승무학 9종 — 대문파 장문제자 전용. 정의서 §5-1·§5-2.
        ///
        /// **성능 형태소 4 · 극한경지 1자 포함 · 기력 16.** 극한경지를 쓸 수 있는 유일한 계층이다.
        ///
        /// ⚠⚠ **극한경지 9자와 대문파 9곳이 하나씩 맞아떨어진다.** 우연이 아니라 정의서 §3-8 이
        ///   9자를 고를 때부터 문파 성격을 염두에 뒀기 때문이다 — 남궁세가(제왕검형)에 제(帝),
        ///   천마신교에 마(魔), 소림사(금강불괴)에 성(聖), 무당파(도교)에 선(仙) 처럼
        ///   **이름이 곧 그 문파인** 자리가 여럿 있다.
        ///
        /// ⚠ 배경어를 쓰지 않는다 — 4자 중 1자가 극한경지라 배경어까지 넣으면 성능이 3으로 줄어
        ///   전승무학이 대문파 무공보다 약해진다.
        /// </summary>
        private static readonly Draft[] LegacyArts =
        {
            new Draft("소림사", "성뇌후격", ArtKind.Attack),      // 聖雷厚擊 — 성(聖 수호) · 금강불괴
            new Draft("무당파", "선음유수", ArtKind.Internal),    // 仙陰柔水 — 선(仙 지속) · 도가의 신선
            new Draft("화산파", "존풍쾌절", ArtKind.Attack),      // 尊風快截 — 존(尊 속공) · 빠른 매화검
            new Draft("남궁세가", "제화정참", ArtKind.Attack),    // 帝火正斬 — 제(帝 균형) · 제왕검형
            // ⚠⚠ **유일하게 극한경지를 쓰지 않는 전승무학**이다(2026-07-30 결정). 만천화우(滿天花雨)를
            //   예외 없이 재현하려다 나온 결과다 — 본체 4자 중 공격방식·무공형태 2자가 필수로 고정이라
            //   남는 2자를 두고 **만(萬)·우(雨)·극한경지가 경합**했고, 셋 다 넣을 수는 없었다.
            //   당가는 위력이 아니라 **물량·상태이상·타격 횟수**로 이기는 문파이므로 극한경지를 포기했다.
            //   ⚠ 정의서 §5-2 는 *"극한경지는 전승무학에만 허용"* 이라고 했지 *"전승무학은 반드시 쓴다"* 고
            //     하지 않았다. 규칙 위반이 아니라 **쓰지 않기로 한 선택**이다. 그 대가로 왕(王)이 미사용으로 남는다.
            //   ⚠ `만우환사`(幻)가 아니라 `만우쾌사`(快)인 이유 — 환은 공격 −2 라 사(0.5)와 합치면
            //     **공격이 −1.5 로 음수**가 된다(설계안 §1-E). 쾌(快)는 당가 특징 문장의
            //     *'극히 빠른 던지기'* 와도 정확히 맞는다.
            new Draft("사천당가", "만우쾌사", ArtKind.Attack),    // 萬雨快射 — 만 개의 비를 빠르게 쏜다 [광역 전원]
            new Draft("서량군문", "패혈중창", ArtKind.Attack),    // 霸血重槍 — 패(霸 파괴) · 군부의 정복
            new Draft("살문", "황야환투", ArtKind.Attack),        // 皇夜幻投 — 황(皇 정밀) · 암살은 정밀이다
            new Draft("천마신교", "마한중참", ArtKind.Attack),    // 魔寒重斬 — 마(魔 관통) · 이름 그대로
            new Draft("혈교", "종환화격", ArtKind.Attack),        // 宗火幻擊 — 종(宗 극단) · 광란을 끝까지 민다
        };

        [Test]
        public void 전승무학_아홉종이_조합_규칙을_지킨다()
        {
            foreach (Draft draft in LegacyArts)
            {
                ParsedArtName parsed;
                IReadOnlyList<string> problems;

                Assert.IsTrue(MorphemeParser.TryParse(draft.Name, draft.Kind, out parsed, out problems),
                    "{0} 의 {1} 을(를) 분해하지 못했다: {2}",
                    draft.School, draft.Name, string.Join(" · ", problems));

                IReadOnlyList<ArtRuleViolation> violations = ArtCompositionRule.Validate(parsed, ArtTier.Legacy);

                var messages = new List<string>();
                for (int i = 0; i < violations.Count; i++) messages.Add(violations[i].Message);
                Assert.AreEqual(0, violations.Count,
                    "{0} 의 {1} 이(가) 규칙을 어긴다: {2}",
                    draft.School, draft.Name, string.Join(" · ", messages));
            }
        }

        [Test]
        public void 전승무학은_극한경지를_정확히_한_자씩_쓴다()
        {
            // 정의서 §5-2 제약 2 — 한 무공에 극한경지 1자. 그리고 **9자가 9문파에 하나씩** 배분된다.
            var used = new HashSet<char>();

            foreach (Draft draft in LegacyArts)
            {
                ParsedArtName parsed = MorphemeParser.Parse(draft.Name, draft.Kind);

                // ⚠ 사천당가만 예외다 — 극한경지 대신 만(萬 전원 타격)과 배경어 우(雨)를 택했다.
                //   위력이 아니라 물량으로 이기는 문파라 그 교환이 정체성에 맞는다.
                bool usesPinnacle = parsed.CountOf(MorphemeCategory.Pinnacle) == 1;
                if (draft.School == "사천당가")
                {
                    Assert.AreEqual(0, parsed.CountOf(MorphemeCategory.Pinnacle),
                        "사천당가 전승무학은 극한경지를 쓰지 않기로 했다.");
                    Assert.AreNotEqual(AttackScope.Single, parsed.Scope,
                        "극한경지를 포기한 대가로 광역을 얻어야 한다.");
                }
                else
                {
                    Assert.IsTrue(usesPinnacle, "{0} 의 극한경지 형태소는 1자여야 한다.", draft.Name);
                    Assert.AreEqual(4, parsed.EffectiveMorphemeCount,
                        "{0} 의 성능 형태소가 4가 아니다 — 전승무학은 최상위 계층이다.", draft.Name);
                    Assert.AreEqual(0, parsed.BackgroundCount,
                        "{0} 에 배경어가 들어갔다 — 성능이 3으로 줄어 대문파보다 약해진다.", draft.Name);
                }

                foreach (Morpheme m in parsed.Body)
                {
                    if (m.Category != MorphemeCategory.Pinnacle) continue;
                    Assert.IsTrue(used.Add(m.Korean), "극한경지 {0} 이(가) 두 문파에 배분됐다.", m);
                }
            }

            // ⚠ 8자다. 사천당가가 극한경지를 포기하면서 **왕(王)이 미사용으로 남았다.**
            //   9↔9 대응이 깨진 것은 아쉽지만, 만천화우의 그림을 예외 없이 재현한 값이다.
            //   왕(王)은 사전에 그대로 있으므로 나중에 절대경지나 다른 자리에서 쓸 수 있다.
            Assert.AreEqual(8, used.Count, "극한경지 배분 수 — 왕(王)만 미사용이어야 한다.");
            Assert.IsFalse(used.Contains('왕'), "미사용으로 남기기로 한 것은 왕(王)이다.");
        }

        [Test]
        public void 전승무학은_전승_계층에서만_적법하다()
        {
            // 극한경지는 전승무학 전용이다(정의서 §5-2 제약 1). 대문파로 검사하면 걸려야 한다.
            ParsedArtName parsed = MorphemeParser.Parse("마한중참", ArtKind.Attack);

            AssertHasRule(ArtCompositionRule.Validate(parsed, ArtTier.Major), ArtRule.PinnacleRestricted);
            Assert.AreEqual(0, ArtCompositionRule.Validate(parsed, ArtTier.Legacy).Count);
        }

        [Test]
        public void 대문파_일흔두종이_조합_규칙을_지킨다()
        {
            foreach (Draft draft in MajorSchoolArts)
            {
                ParsedArtName parsed;
                IReadOnlyList<string> problems;

                Assert.IsTrue(MorphemeParser.TryParse(draft.Name, draft.Kind, out parsed, out problems),
                    "{0} 의 {1} 을(를) 분해하지 못했다: {2}",
                    draft.School, draft.Name, string.Join(" · ", problems));

                IReadOnlyList<ArtRuleViolation> violations = ArtCompositionRule.Validate(parsed, ArtTier.Major);

                var messages = new List<string>();
                for (int i = 0; i < violations.Count; i++) messages.Add(violations[i].Message);
                Assert.AreEqual(0, violations.Count,
                    "{0} 의 {1} 이(가) 규칙을 어긴다: {2}",
                    draft.School, draft.Name, string.Join(" · ", messages));
            }
        }

        [Test]
        public void 대문파는_문파당_공격넷_내공둘_경공둘이다()
        {
            // 정의서 §5-1 — 대문파 문파무학 8종(공격 4 = 무기당 2). 전승무학 1종은 별도다.
            var counts = new Dictionary<string, Dictionary<ArtKind, int>>();
            foreach (Draft draft in MajorSchoolArts)
            {
                if (!counts.ContainsKey(draft.School)) counts[draft.School] = new Dictionary<ArtKind, int>();
                Dictionary<ArtKind, int> byKind = counts[draft.School];
                byKind[draft.Kind] = byKind.ContainsKey(draft.Kind) ? byKind[draft.Kind] + 1 : 1;
            }

            Assert.AreEqual(9, counts.Count, "대문파는 9곳이다.");
            foreach (KeyValuePair<string, Dictionary<ArtKind, int>> school in counts)
            {
                Assert.AreEqual(4, Count(school.Value, ArtKind.Attack), "{0} 의 공격 무공", school.Key);
                Assert.AreEqual(2, Count(school.Value, ArtKind.Internal), "{0} 의 내공 무공", school.Key);
                Assert.AreEqual(2, Count(school.Value, ArtKind.Movement), "{0} 의 경공 무공", school.Key);
            }
        }

        [Test]
        public void 대문파는_성능_형태소가_셋이나_넷이다()
        {
            // 계층 = 형태소 개수. 소문파 3 → 대문파 3~4.
            // ⚠ 대문파만 폭이 있는 이유는 **배경어 자리** 때문이다 — 4자 중 1자를 배경어로 쓰면 성능은 3이 된다.
            foreach (Draft draft in MajorSchoolArts)
            {
                ParsedArtName parsed = MorphemeParser.Parse(draft.Name, draft.Kind);

                Assert.GreaterOrEqual(parsed.EffectiveMorphemeCount, 3, "{0} 의 성능 형태소가 3 미만이다.", draft.Name);
                Assert.LessOrEqual(parsed.EffectiveMorphemeCount, 4, "{0} 의 성능 형태소가 4를 넘는다.", draft.Name);
                Assert.LessOrEqual(parsed.Body.Count, ArtCompositionRule.MaxBodyLength,
                    "{0} 의 본체가 4자를 넘는다.", draft.Name);
            }
        }

        /// <summary>어떤 문파가 어떤 특징 축을 갖는지. **갖지 않는 문파가 있는 것이 요점이다.**</summary>
        // ⚠ 사천당가는 여기 없다 — 당가의 광역은 **전승무학**(`만우쾌사`)에 있고,
        //   이 표는 **문파무학 8종**의 축 배분을 본다. 층이 다르다.
        private static readonly string[] WideAttackSchools = { "소림사", "서량군문", "혈교" };
        private static readonly string[] CounterSchools = { "무당파", "살문", "종남파", "점창파" };

        [Test]
        public void 특징_축은_문파마다_다르다()
        {
            // ⚠⚠ **이 테스트가 이 설계의 핵심이다** (2026-07-30 사용자 지적으로 방향을 바꿨다).
            //   처음에는 대문파 9곳 전부에 광역 1 + 상성 1 을 넣었는데, 그건 개성이 아니라 **템플릿**이었다.
            //   *"모든 대문파 무공에 광역기·상성무공이 들어가는 것도 반대다. 이러면 진짜 각 문파의
            //     특징을 살리기 어렵다"* 는 지적이 정확했다.
            //
            //   지금은 축을 나눠 가진다 — 광역 3곳 · 상성 2곳(대문파) · 가중치 2곳 ·
            //   **아무것도 없는 곳 2곳**(남궁세가·천마신교). 없는 것도 정체성이다:
            //   네 슬롯을 전부 위력축에 쓰므로 **순수 공격력이 가장 높다.**
            var wide = new List<string>();
            var counter = new List<string>();

            foreach (Draft draft in MajorSchoolArts)
            {
                ParsedArtName parsed = MorphemeParser.Parse(draft.Name, draft.Kind);
                if (parsed.Scope != AttackScope.Single && !wide.Contains(draft.School)) wide.Add(draft.School);
                if (parsed.CounterTargets.Count > 0 && !counter.Contains(draft.School)) counter.Add(draft.School);
            }

            // ⚠ 2026-07-30 사천당가가 넷째로 합류했다(`만천환사`). 가중치를 이미 가진 문파인데
            //   광역까지 갖는 것이 예외처럼 보이지만, **두 축은 층이 다르다** —
            //   가중치는 **문파 특징 층**(정의서 §6-3, 문파 데이터에 직접 기입)에서 오고
            //   광역은 **무공 형태소 층**(§3-12, 이름에서 유도)에서 온다.
            //   그리고 암기를 뿌리는 문파에 광역이 없는 편이 오히려 부자연스럽다.
            Assert.AreEqual(3, wide.Count, "광역을 가진 대문파 수 — 전부가 갖거나 아무도 안 갖으면 축이 아니다.");
            Assert.AreEqual(2, counter.Count, "상성을 가진 대문파 수");
            Assert.Less(wide.Count + counter.Count, 9, "9곳이 모두 축을 가지면 다시 템플릿이 된다.");

            foreach (string school in WideAttackSchools) Assert.Contains(school, wide, "{0} 이 광역을 잃었다.", school);
            foreach (string school in new[] { "무당파", "살문" }) Assert.Contains(school, counter, "{0} 이 상성을 잃었다.", school);
        }

        [Test]
        public void 광역은_대문파에만_있다()
        {
            // 계층 제약은 그대로다 — 광역은 대문파부터 쓸 수 있다(정의서 §3-12).
            // 다만 **대문파라고 다 갖는 것은 아니다**(위 테스트).
            foreach (string name in WandererArts)
            {
                Assert.AreEqual(AttackScope.Single, MorphemeParser.Parse(name).Scope,
                    "강호무학 {0} 에 광역이 들어 있다.", name);
            }
            foreach (Draft draft in MinorSchoolArts)
            {
                Assert.AreEqual(AttackScope.Single, MorphemeParser.Parse(draft.Name, draft.Kind).Scope,
                    "소문파 {0} 에 광역이 들어 있다.", draft.Name);
            }
        }

        [Test]
        public void 상성_무공은_자기_분류를_치지_않는다()
        {
            // ⚠ 문파마다 무학분류(양기·음기·혼합)가 있고(`SchoolCatalog`), 상성은 **다른 분류**를 쳐야 의미가 있다.
            //   자기 분류를 치면 "우리 편에게 강한 무공" 이 되어 이름의 의미와 어긋난다.
            foreach (Draft draft in MajorSchoolArts)
            {
                ParsedArtName parsed = MorphemeParser.Parse(draft.Name, draft.Kind);
                if (parsed.CounterTargets.Count == 0) continue;

                School school = SchoolCatalog.ByName(draft.School);
                Assert.AreEqual(0, parsed.AdvantageAgainst(school.Lineage),
                    "{0}({1}) 의 {2} 가 자기 분류를 친다.", draft.School, school.Lineage, draft.Name);
            }
        }

        // ─────────────────────────── 대형세력 16종 (4세력 × 4) ───────────────────────────

        /// <summary>
        /// 대형세력 4곳 × 4종(공격 2 · 내공 1 · 경공 1) = 16종. 정의서 §5-5.
        ///
        /// **급은 대문파급**(성능 형태소 3~4). 문파+세력 이중 조건이라 난이도가 대문파에 준한다.
        /// ⚠ 계층을 늘리지 않는다 — 계층은 수직축(얼마나 얻기 어려운가)이고 세력은 수평축(어느 경로로 얻는가)이다.
        ///
        /// 세력도 문파처럼 **무학분류**를 갖는다(상성이 갈 곳). 그리고 축을 하나씩 나눠 가진다 —
        /// 무림맹 상성 · 사도련 광역 · 제천성 가중치 · 천마신교 광역.
        ///
        /// ⚠⚠ **천마신교는 문파 무공과 세력 무공의 성격이 다르다.** 문파로서는 무거움·경직·순수 위력이고,
        ///   세력으로서는 기만·마비·광역이다. 두 층이 같은 조직인데도 다른 얼굴을 갖는다.
        /// </summary>
        private static readonly Draft[] FactionArts =
        {
            // ── 무림맹 (정파 연맹체 · 양기무학 · 창·검) ── 【상성】
            // 특징: '정직한' '찌르기' 창술과 '베기' 검술이 특징인 연맹. 색은 밝음(明·光).
            new Draft("무림맹", "명정자탈", ArtKind.Attack),    // 明正刺奪 — 밝고 정직하게 찔러 기력을 뺏는다
            new Draft("무림맹", "절지낙월", ArtKind.Attack),    // 截地落月 — 땅을 베어 달을 떨어뜨린다 [상성 음기]
            new Draft("무림맹", "광양직공", ArtKind.Internal),  // 光陽直功
            new Draft("무림맹", "방직명보", ArtKind.Movement),  // 防直明步

            // ── 사도련 (사파 연맹체 · 음기무학 · 비도·도) ── 【광역】
            // 특징: '기만적인' '던지기' 암기술과 '베기' 도법 + 출혈이 특징인 연맹. 색은 밤(夜)·차가움(寒).
            new Draft("사도련", "궤야척혈", ArtKind.Attack),    // 詭夜擲血 — 밤에 속여 던져 피를 낸다
            new Draft("사도련", "환벌혈군", ArtKind.Attack),    // 幻伐血群 — 현혹하며 베어 무리에게 피를 낸다 [광역 3인]
            new Draft("사도련", "한음궤공", ArtKind.Internal),  // 寒陰詭功
            new Draft("사도련", "둔궤야보", ArtKind.Movement),  // 遁詭夜步

            // ── 제천성 (황실 산하 기관 · 혼합무학 · 검·권) ── 【가중치】
            // 특징: '무거운' '베기' 검술과 '때리기' 권법 + 경직이 특징인 관부 기관. 색은 차가움(冷).
            // ⚠⚠ 정·사·마 출신을 다 받는 유일한 세력이라 분류도 **혼합**이다.
            //   경직(硬)을 쓰는 것은 포박·제압이 관의 일이기 때문이다.
            new Draft("제천성", "중냉참경", ArtKind.Attack),    // 重冷斬硬 — 무겁고 차갑게 베어 굳게 만든다
            new Draft("제천성", "후명격경", ArtKind.Attack),    // 厚明擊硬 — 두텁고 밝게 쳐서 굳게 만든다
            new Draft("제천성", "냉음중공", ArtKind.Internal),  // 冷陰重功
            new Draft("제천성", "거후명보", ArtKind.Movement),  // 拒厚明步

            // ── 천마신교 (문파 겸 마도 연맹체 · 음기무학 · 검·권) ── 【광역】
            // 특징: '기만적인' '베기' 검술과 '때리기' 권법 + 마비가 특징인 마교. 색은 밤(夜)·어둠(暗).
            // ⚠ 문파 무공(무거움·경직·순수 위력)과 **일부러 다르게** 잡았다. 같은 조직의 다른 얼굴이다.
            new Draft("천마신교", "환야참비", ArtKind.Attack),  // 幻夜斬痺 — 어둠 속에서 현혹해 베고 마비시킨다
            new Draft("천마신교", "궤격비전", ArtKind.Attack),  // 詭擊痺全 — 속여 쳐서 전원을 마비시킨다 [광역 전원]
            new Draft("천마신교", "암음환결", ArtKind.Internal),// 暗陰幻訣
            new Draft("천마신교", "섬환야술", ArtKind.Movement),// 閃幻夜術
        };

        [Test]
        public void 대형세력_열여섯종이_조합_규칙을_지킨다()
        {
            foreach (Draft draft in FactionArts)
            {
                ParsedArtName parsed;
                IReadOnlyList<string> problems;

                Assert.IsTrue(MorphemeParser.TryParse(draft.Name, draft.Kind, out parsed, out problems),
                    "{0} 의 {1} 을(를) 분해하지 못했다: {2}",
                    draft.School, draft.Name, string.Join(" · ", problems));

                // 세력 무공은 대문파급이다 — 광역은 허용되고 극한경지는 허용되지 않는다.
                IReadOnlyList<ArtRuleViolation> violations = ArtCompositionRule.Validate(parsed, ArtTier.Major);

                var messages = new List<string>();
                for (int i = 0; i < violations.Count; i++) messages.Add(violations[i].Message);
                Assert.AreEqual(0, violations.Count,
                    "{0} 의 {1} 이(가) 규칙을 어긴다: {2}",
                    draft.School, draft.Name, string.Join(" · ", messages));
            }
        }

        [Test]
        public void 대형세력은_네_곳이_각각_공격둘_내공하나_경공하나다()
        {
            var counts = new Dictionary<string, Dictionary<ArtKind, int>>();
            foreach (Draft draft in FactionArts)
            {
                if (!counts.ContainsKey(draft.School)) counts[draft.School] = new Dictionary<ArtKind, int>();
                Dictionary<ArtKind, int> byKind = counts[draft.School];
                byKind[draft.Kind] = byKind.ContainsKey(draft.Kind) ? byKind[draft.Kind] + 1 : 1;
            }

            Assert.AreEqual(4, counts.Count, "대형세력은 4곳이다.");
            foreach (KeyValuePair<string, Dictionary<ArtKind, int>> faction in counts)
            {
                Assert.AreEqual(2, Count(faction.Value, ArtKind.Attack), "{0} 의 공격 무공", faction.Key);
                Assert.AreEqual(1, Count(faction.Value, ArtKind.Internal), "{0} 의 내공 무공", faction.Key);
                Assert.AreEqual(1, Count(faction.Value, ArtKind.Movement), "{0} 의 경공 무공", faction.Key);
            }
        }

        [Test]
        public void 대형세력_무공은_대문파급이고_극한경지가_없다()
        {
            // 정의서 §5-5 — 급은 대문파급(성능 3~4). 전승무학이 없는 대신 **절대경지의 첫 통로**가 보상이다.
            foreach (Draft draft in FactionArts)
            {
                ParsedArtName parsed = MorphemeParser.Parse(draft.Name, draft.Kind);

                Assert.GreaterOrEqual(parsed.EffectiveMorphemeCount, 3, "{0} 의 성능 형태소", draft.Name);
                Assert.LessOrEqual(parsed.EffectiveMorphemeCount, 4, "{0} 의 성능 형태소", draft.Name);
                Assert.AreEqual(0, parsed.CountOf(MorphemeCategory.Pinnacle),
                    "{0} 에 극한경지가 들어갔다 — 전승무학 전용이다.", draft.Name);
            }
        }

        // ─────────────────────────── 절대경지 4종 ───────────────────────────

        /// <summary>
        /// 절대경지 무학 4종 — 기연(奇緣)으로만 얻는다. 정의서 §5-3.
        ///
        /// **⚠⚠ 전부 내공 무공이다** (2026-07-30 결정). 정의서 §5-3 이 *"수치로 강하게 만들지 않는다.
        ///   대신 규칙을 바꾼다"* 고 못박았는데, **공격 무공은 위력 수치가 본체**라 서로 맞지 않는다.
        ///   공격 무공으로 만들면 전승무학보다 수치가 낮은 "절세무공" 이 나와 이름값을 못 한다.
        ///
        /// **그리고 내공으로 두는 쪽이 오히려 더 화려하다** — "한 턴 2회 행동" 이 붙으면
        ///   두 번 나가는 것은 절대경지가 아니라 **플레이어가 10성까지 올린 자기 무공**이다.
        ///   공격 무공으로 만들면 그것만 쓰게 되어 134개를 지은 의미가 사라지고,
        ///   §5-3 이 경계한 *"전승무학을 압도하면 문파 성장 경로가 무의미해진다"* 가 그대로 일어난다.
        ///
        /// ⚠ 규칙 변경 4종은 형태소가 아니라 **별도 플래그**로 붙는다(설계안 §3-4).
        ///   그래서 이름은 다른 무공과 **똑같은 방식**으로 짓는다 — 작명 규칙을 새로 만들지 않았다.
        /// ⚠ 극한경지는 쓸 수 없다. 정의서 §5-2 가 전승무학 전용으로 못박았고 절대경지는 별개 계층이다.
        /// </summary>
        private static readonly Draft[] AbsoluteArts =
        {
            // 규칙 변경: 모든 상태이상 면역 — 사파·마도를 무력화한다.
            // 양기의 정광(正光)이 사기(邪氣)를 물리친다는 그림.
            new Draft("절대경지", "정합광일", ArtKind.Internal),  // 正合光日 [양기무학]

            // 규칙 변경: 기력을 소모하지 않음 — 평타 전락이 영원히 사라진다.
            // 숨(息)이 물처럼 부드럽게 순환한다는 그림.
            new Draft("절대경지", "식유수혼", ArtKind.Internal),  // 息柔水混 [혼합무학]

            // 규칙 변경: 한 턴에 2회 행동 — 행동 경제를 깬다.
            // 그림자처럼 빠르다. 쾌(속도+2)와 신(속도+2)이 겹쳐 속도 4 가 되는 것도 그림과 맞는다.
            new Draft("절대경지", "음쾌신월", ArtKind.Internal),  // 陰快迅月 [음기무학]

            // 규칙 변경: 모든 분류에 상성 +2, 상대 상성 무효 — 상성 절대우위.
            // 합(合)과 혼(混)이 "모든 것을 아우른다" 를 그대로 말한다.
            new Draft("절대경지", "합현혼유", ArtKind.Internal),  // 合玄混柔 [혼합무학]
        };

        [Test]
        public void 절대경지_네종이_조합_규칙을_지킨다()
        {
            foreach (Draft draft in AbsoluteArts)
            {
                ParsedArtName parsed;
                IReadOnlyList<string> problems;

                Assert.IsTrue(MorphemeParser.TryParse(draft.Name, draft.Kind, out parsed, out problems),
                    "{0} 을(를) 분해하지 못했다: {1}", draft.Name, string.Join(" · ", problems));

                IReadOnlyList<ArtRuleViolation> violations = ArtCompositionRule.Validate(parsed, ArtTier.Absolute);

                var messages = new List<string>();
                for (int i = 0; i < violations.Count; i++) messages.Add(violations[i].Message);
                Assert.AreEqual(0, violations.Count,
                    "{0} 이(가) 규칙을 어긴다: {1}", draft.Name, string.Join(" · ", messages));
            }
        }

        [Test]
        public void 절대경지는_전부_내공_무공이다()
        {
            // ⚠⚠ 이 테스트가 2026-07-30 설계 결정을 고정한다.
            //   공격 무공으로 만들면 ⓐ 위력이 전승무학보다 낮아 초라해지고
            //   ⓑ 그것만 쓰게 되어 나머지 134종이 죽는다.
            foreach (Draft draft in AbsoluteArts)
            {
                Assert.AreEqual(ArtKind.Internal, draft.Kind, "{0} 이(가) 내공 무공이 아니다.", draft.Name);

                ParsedArtName parsed = MorphemeParser.Parse(draft.Name, draft.Kind);
                Assert.AreEqual(4, parsed.EffectiveMorphemeCount,
                    "{0} 의 성능 형태소가 4가 아니다 — 절대경지는 최상위다.", draft.Name);
                Assert.AreEqual(0, parsed.CountOf(MorphemeCategory.Pinnacle),
                    "{0} 에 극한경지가 들어갔다 — 전승무학 전용이다(정의서 §5-2).", draft.Name);
                Assert.AreEqual(0, parsed.BackgroundCount,
                    "{0} 에 배경어가 들어갔다 — 성능이 3으로 줄어 대문파급이 된다.", draft.Name);
            }
        }

        [Test]
        public void 문파_무공은_이름이_겹치지_않는다()
        {
            var seen = new HashSet<string>();
            foreach (string name in WandererArts) Assert.IsTrue(seen.Add(name), "{0} 이(가) 중복이다.", name);
            foreach (Draft draft in MinorSchoolArts) Assert.IsTrue(seen.Add(draft.Name), "{0} 이(가) 중복이다.", draft.Name);
            foreach (Draft draft in MajorSchoolArts) Assert.IsTrue(seen.Add(draft.Name), "{0} 이(가) 중복이다.", draft.Name);
            foreach (Draft draft in LegacyArts) Assert.IsTrue(seen.Add(draft.Name), "{0} 이(가) 중복이다.", draft.Name);
            foreach (Draft draft in FactionArts) Assert.IsTrue(seen.Add(draft.Name), "{0} 이(가) 중복이다.", draft.Name);
            foreach (Draft draft in AbsoluteArts) Assert.IsTrue(seen.Add(draft.Name), "{0} 이(가) 중복이다.", draft.Name);

            // ✅ 강호 9 + 소문파 28 + 대문파 72 + 전승 9 + 대형세력 16 + 절대경지 4 = **138. 전부 지었다.**
            Assert.AreEqual(138, seen.Count, "무공명 총수 — 정의서 §5-4 의 138 과 맞아야 한다");
        }

        private static int Count(Dictionary<ArtKind, int> byKind, ArtKind kind)
        {
            return byKind.ContainsKey(kind) ? byKind[kind] : 0;
        }

        [Test]
        public void 강호무학_기력_소모는_전부_같다()
        {
            // 본체 2자 · 배경어 0 이므로 전부 8 이다. 계층이 오르면 12 · 16 으로 는다.
            foreach (string name in WandererArts)
            {
                Assert.AreEqual(2 * MorphemeParser.QiCostPerMorpheme, MorphemeParser.Parse(name).QiCost,
                    "{0} 의 기력 소모가 다르다.", name);
            }
        }
    }
}
