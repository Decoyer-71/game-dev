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

        [Test]
        public void 문파_무공은_이름이_겹치지_않는다()
        {
            var seen = new HashSet<string>();
            foreach (string name in WandererArts) Assert.IsTrue(seen.Add(name), "{0} 이(가) 중복이다.", name);
            foreach (Draft draft in MinorSchoolArts) Assert.IsTrue(seen.Add(draft.Name), "{0} 이(가) 중복이다.", draft.Name);
            foreach (Draft draft in MajorSchoolArts) Assert.IsTrue(seen.Add(draft.Name), "{0} 이(가) 중복이다.", draft.Name);

            // 강호 9 + 소문파 28 + 대문파 72 = 109. 남은 것은 전승무학 9 + 절대경지 4 = 13 이다.
            Assert.AreEqual(109, seen.Count, "지금까지 지은 무공명 수");
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
