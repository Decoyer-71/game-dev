using System;
using System.Collections.Generic;

namespace Jianghu.Core.Martial.Morphemes
{
    /// <summary>어긴 규칙의 종류. 역산 리포트가 위반을 분류해 보고할 때 쓴다.</summary>
    public enum ArtRule
    {
        /// <summary>정의서 §2-2 규칙 1 — 본체는 4자 이내.</summary>
        BodyLength = 0,

        /// <summary>정의서 §2-2 규칙 2 — 카테고리당 최대 1자. 배경어에 적용되면 그것이 규제 R2 다.</summary>
        CategoryDuplicate = 1,

        /// <summary>정의서 §2-2 규칙 3 — 종류별 필수 구성이 빠졌다.</summary>
        RequiredMissing = 2,

        /// <summary>정의서 §2-2 규칙 3 — 선택 카테고리 개수 상한 초과.</summary>
        OptionalOverflow = 3,

        /// <summary>정의서 §5-2 제약 1 — 극한경지는 전승무학에만 허용된다.</summary>
        PinnacleRestricted = 4,

        /// <summary>정의서 §3-12 — 범위(광역)는 공격 무공이면서 대문파 이상에만 허용된다.</summary>
        ScopeRestricted = 5,

        /// <summary>
        /// 정의서 §3-10-a — **무학분류 글자는 부정 한자 바로 뒤가 아니면 쓸 수 없다** (2026-08-02 사용자 확정).
        ///
        /// 무학분류는 **문파의 속성**을 정의하는 축이다(§6-4). 이름에 들어가는 경우는 상성(§4)의
        /// 목적어일 때 하나뿐이며, 그때 그 글자는 *"내 분류가 무엇인가"* 가 아니라 **"내가 무엇에 강한가"** 다.
        ///
        /// ⚠⚠ 이 규칙 없이 출하된 위반이 **8종** 있었다. 자체 수치가 0 이고 앞에 부정이 없으면 상성도 0 이라
        ///   **성능 슬롯 하나를 통째로 버리면서 기력은 글자 수로 냈다.** 전부 자기 문파의 분류였다 —
        ///   무작위 오용이 아니라 자기 선언이었다.
        /// </summary>
        TagRequiresNegation = 6,

        /// <summary>
        /// 정의서 §5-3 — **절대경지 규칙 형태소(면·무·쌍·통)는 절대경지에만** 쓸 수 있다 (2026-08-02 신설).
        /// 극한경지의 <see cref="PinnacleRestricted"/> 와 같은 종류의 계층 제한이다.
        /// </summary>
        RuleMorphemeRestricted = 7,

        /// <summary>
        /// 정의서 §5-3 — **절대경지는 규칙 형태소를 정확히 1자 가져야 한다** (2026-08-02 신설).
        ///
        /// ⚠ 극한경지의 *"1자만"*(선택)과 달리 **필수**다. §5-3 이 절대경지를
        /// *"수치가 아니라 규칙을 바꾸는 것"* 으로 정의하므로 **규칙 없는 절대경지는 존재 의미가 없다.**
        /// </summary>
        AbsoluteRequiresRule = 8,
    }

    /// <summary>조합 규칙 위반 하나.</summary>
    public sealed class ArtRuleViolation
    {
        public ArtRule Rule { get; }
        public string Message { get; }

        public ArtRuleViolation(ArtRule rule, string message)
        {
            Rule = rule;
            Message = message;
        }

        public override string ToString()
        {
            return Message;
        }
    }

    /// <summary>
    /// 조합 규칙 검사기 — 정의서 §2-2 와 §5-2, 그리고 배경어 규제 R1~R4 의 집행 지점이다.
    ///
    /// ⚠⚠ **위반을 예외로 던지는 경로와 목록으로 모으는 경로를 둘 다 둔다.**
    ///   실제 무공 생성은 던져야 하고(잘못된 이름이 조용히 통과하면 안 된다),
    ///   역산 리포트(설계안 §5-2)는 모아야 한다(첫 실패에서 멈추면 *"사전에 무엇이 빠졌는가"* 를 못 본다).
    ///
    /// ── 배경어가 규칙과 맞물리는 방식 ────────────────────────────────────────────
    ///   **R2** 는 규칙 2 에서 자동으로 나온다 — 배경어도 카테고리이므로 "카테고리당 1자" 가 곧 "배경어 1자" 다
    ///   **R3** 은 규칙 3 에서 자동으로 나온다 — 배경어는 필수 카테고리가 아니므로 필수를 대체할 수 없다
    ///   **대가**도 자동이다 — 규칙 1 의 "4자 이내" 가 배경어를 **포함해서** 세므로 성능 슬롯 하나를 잃는다
    ///   반대로 "나머지 카테고리 중 최대 N" 에는 **넣지 않는다** — 배경어는 성능 카테고리가 아니기 때문이다
    ///
    ///   즉 규제 4조 중 코드가 따로 집행하는 것은 **R1 뿐**이고(<see cref="MorphemeDictionary"/>),
    ///   R2·R3 은 정의서 규칙에서 저절로 따라 나온다. 그래서 테스트로 고정해 둔다.
    /// ────────────────────────────────────────────────────────────────────────────
    /// </summary>
    public static class ArtCompositionRule
    {
        /// <summary>정의서 §2-2 규칙 1 — 무공명 본체의 최대 글자 수(접미사 제외).</summary>
        public const int MaxBodyLength = 4;

        /// <summary>
        /// 조합 규칙을 검사해 위반 목록을 돌려준다. 위반이 없으면 빈 목록이다.
        /// </summary>
        /// <param name="parsed">분해된 무공명.</param>
        /// <param name="tier">
        /// 이 무공의 계층. 두 규칙이 여기에 달려 있다 — **극한경지는 전승무학에만**(정의서 §5-2),
        /// **범위(광역)는 대문파 이상에만**(§3-12).
        /// ⚠ 계층은 문파명에서 유도할 수 없어 호출자가 알려줘야 한다. 전승무학은 대문파 안에
        ///   문파무학과 섞여 있고, 절대경지는 아예 문파에 속하지 않는다.
        /// </param>
        public static IReadOnlyList<ArtRuleViolation> Validate(
            ParsedArtName parsed, ArtTier tier = ArtTier.Wanderer)
        {
            if (parsed == null) throw new ArgumentNullException(nameof(parsed));

            var violations = new List<ArtRuleViolation>();

            // ── 규칙 1. 본체 4자 이내 (배경어 포함해서 센다) ──
            if (parsed.Body.Count > MaxBodyLength)
            {
                violations.Add(new ArtRuleViolation(ArtRule.BodyLength,
                    "본체가 " + parsed.Body.Count + "자다. " + MaxBodyLength + "자 이내여야 한다 (배경어도 센다)"));
            }

            // ── 규칙 2. 카테고리당 최대 1자 ──
            // 한 초식이 베면서 동시에 찌를 수 없다. 배경어에 적용되면 그것이 규제 R2 다.
            foreach (MorphemeCategory category in AllCategories)
            {
                int count = parsed.CountOf(category);
                if (count <= 1) continue;

                violations.Add(new ArtRuleViolation(ArtRule.CategoryDuplicate,
                    Describe(category) + "가 " + count + "자다. 카테고리당 1자여야 한다"
                    + (category == MorphemeCategory.Background ? " (배경어 규제 R2)" : "")));
            }

            // ── 규칙 3. 종류별 필수 구성 + 선택 상한 ──
            ValidateComposition(parsed, violations);

            // ── 정의서 §5-2 제약 1. 극한경지는 전승무학 전용 ──
            // 제약 2("무공당 1자")는 규칙 2 가 이미 잡는다.
            if (tier != ArtTier.Legacy && parsed.CountOf(MorphemeCategory.Pinnacle) > 0)
            {
                violations.Add(new ArtRuleViolation(ArtRule.PinnacleRestricted,
                    "극한경지 형태소는 전승무학에만 쓸 수 있다 (정의서 §5-2). "
                    + "극한경지 9자는 유일하게 페널티가 없으므로 계층 제한이 페널티를 대신한다"));
            }

            // ── 정의서 §3-10-a. 무학분류는 부정 바로 뒤에만 (2026-08-02 사용자 확정) ──
            //
            // ⚠⚠ 이 검사가 없던 동안 위반 8종이 출하됐고, 그 여덟이 **전부 자기 문파의 분류**를
            //   달고 있었다(소림사=혼합에 혼 · 화산파=양기에 일 …). 자기 선언은 기계적으로 아무 일도
            //   안 하면서 성능 슬롯만 먹는다 — 4자 값을 내고 3자로 싸우는 꼴이다.
            //
            // ⚠ 상성으로 살릴 수도 없었다. §6-4 가 *"상성 무공은 자기 분류를 치지 않는다"* 고
            //   못박으므로, 태그가 곧 자기 분류인 이상 앞에 부정을 붙이면 그 순간 다른 규칙을 어긴다.
            //   → 개명이 유일한 처리였고, 그래서 이 검사를 켜기 전에 8종을 먼저 고쳤다.
            {
                IReadOnlyList<Morpheme> body = parsed.Body;
                for (int i = 0; i < body.Count; i++)
                {
                    if (body[i].Category != MorphemeCategory.Tag) continue;
                    if (i > 0 && body[i - 1].IsNegation) continue;   // 상성 성립 — 정상

                    violations.Add(new ArtRuleViolation(ArtRule.TagRequiresNegation,
                        "무학분류 '" + body[i].Korean + "' 앞에 부정 한자(낙·망·멸·산·소)가 없다 (정의서 §3-10-a). "
                        + "무학분류는 문파의 속성을 정의하는 축이고, 이름에 들어가는 경우는 상성의 목적어일 때뿐이다 — "
                        + "부정 없는 분류 글자는 수치도 상성도 0 이라 성능 슬롯만 먹는다"));
                }
            }

            // ── 정의서 §5-3. 절대경지 규칙 형태소는 절대경지 전용 · 정확히 1자 필수 (2026-08-02) ──
            {
                int ruleCount = parsed.CountOf(MorphemeCategory.AbsoluteRule);

                if (tier != ArtTier.Absolute && ruleCount > 0)
                {
                    violations.Add(new ArtRuleViolation(ArtRule.RuleMorphemeRestricted,
                        "절대경지 규칙 형태소(면·무·쌍·통)는 절대경지 무학에만 쓸 수 있다 (정의서 §5-3). "
                        + "전투 규칙 자체를 바꾸는 효과라 계층 제한이 페널티를 대신한다"));
                }

                // ⚠ 극한경지는 "1자만"(선택)인데 이쪽은 **필수**다. §5-3 이 절대경지를
                //   "수치가 아니라 규칙을 바꾸는 것" 으로 정의하므로 규칙 없는 절대경지는 존재 의미가 없다.
                if (tier == ArtTier.Absolute && ruleCount != 1)
                {
                    violations.Add(new ArtRuleViolation(ArtRule.AbsoluteRequiresRule,
                        "절대경지는 규칙 형태소를 정확히 1자 가져야 하는데 " + ruleCount + "자다 (정의서 §5-3). "
                        + "절대경지의 정의가 '수치가 아니라 규칙을 바꾼다' 이므로 규칙 없는 절대경지는 성립하지 않는다"));
                }
            }

            // ── 정의서 §3-12. 범위(광역)는 공격 무공이면서 대문파 이상 ──
            if (parsed.CountOf(MorphemeCategory.Scope) > 0)
            {
                if (parsed.Kind != ArtKind.Attack)
                {
                    violations.Add(new ArtRuleViolation(ArtRule.ScopeRestricted,
                        "범위 형태소는 공격 무공에만 쓸 수 있다 — 내공·경공은 때리는 대상이 없다"));
                }
                if (tier < ArtTier.Major)
                {
                    violations.Add(new ArtRuleViolation(ArtRule.ScopeRestricted,
                        "범위 형태소는 대문파 무공부터 쓸 수 있다 (2026-07-30 확정). "
                        + "광역은 계층의 보상이므로 강호무학·소문파에는 두지 않는다"));
                }
            }

            return violations;
        }

        /// <summary>규칙 위반이 있으면 예외를 던진다. 실제 무공 생성 경로가 쓴다.</summary>
        public static void EnsureValid(ParsedArtName parsed, ArtTier tier = ArtTier.Wanderer)
        {
            IReadOnlyList<ArtRuleViolation> violations = Validate(parsed, tier);
            if (violations.Count == 0) return;

            var messages = new List<string>(violations.Count);
            for (int i = 0; i < violations.Count; i++) messages.Add(violations[i].Message);

            throw new ArgumentException(
                "무공 '" + parsed.Name + "' 이(가) 조합 규칙을 어긴다: " + string.Join(" · ", messages), nameof(parsed));
        }

        // ─────────────────────────── 종류별 필수 구성 (정의서 §2-2 규칙 3) ───────────────────────────

        private static void ValidateComposition(ParsedArtName parsed, List<ArtRuleViolation> violations)
        {
            switch (parsed.Kind)
            {
                case ArtKind.Attack:
                    Require(parsed, MorphemeCategory.AttackMethod, violations);

                    // ⚠⚠ 무공형태를 필수로 만든 것이 정의서 §2-2 의 핵심이다. 5종 전부 페널티가 있고
                    //   페널티 없는 상위호환이 다른 카테고리에 있으므로, 선택제로 두면 아무도 고르지 않는다.
                    //
                    // ⚠⚠ **단 상성 무공은 면제한다** (2026-07-29 사용자 결정).
                    //   근거 둘:
                    //   ⓐ **구조적으로 불가능하다.** 상성을 만들려면 부정+무학분류로 2슬롯을 쓰는데,
                    //      소문파는 성능 형태소가 3개라 공격방식까지 넣으면 무공형태 자리가 남지 않는다.
                    //   ⓑ **규칙의 목적은 이미 달성된다.** 무공형태 필수의 목적은 "페널티 있는 형태소를
                    //      아무도 안 고르는 것" 을 막는 것인데, 부정·무학분류 2자는 **수치가 0** 이라
                    //      상성 무공은 이미 위력을 크게 포기한 구조다. 페널티가 다른 방식으로 존재한다.
                    //
                    //   그리고 이 면제가 문장형 작명을 가능하게 한다 — `창천낙월`(하늘을 찔러 달을
                    //   떨어뜨린다)처럼 [동사][목적어] 구조가 되려면 형용사인 무공형태가 자리를 비켜야 한다.
                    if (parsed.CounterTargets.Count == 0)
                    {
                        Require(parsed, MorphemeCategory.Form, violations);
                    }

                    LimitOptional(parsed, 2, violations, MorphemeCategory.AttackMethod, MorphemeCategory.Form);
                    break;

                case ArtKind.Internal:
                    Require(parsed, MorphemeCategory.Internal, violations);
                    LimitOptional(parsed, 3, violations, MorphemeCategory.Internal);
                    break;

                case ArtKind.Movement:
                    Require(parsed, MorphemeCategory.Defense, violations);
                    LimitOptional(parsed, 3, violations, MorphemeCategory.Defense);
                    break;
            }
        }

        private static void Require(
            ParsedArtName parsed, MorphemeCategory category, List<ArtRuleViolation> violations)
        {
            if (parsed.CountOf(category) > 0) return;

            string label = Describe(category);
            violations.Add(new ArtRuleViolation(ArtRule.RequiredMissing,
                KindName(parsed.Kind) + " 무공에는 " + label + Subject(label) + " 필요하다"));
        }

        /// <summary>
        /// 필수를 뺀 "나머지 카테고리" 개수 상한을 본다.
        /// ⚠ **배경어는 세지 않는다** — 성능 카테고리가 아니기 때문이다. 대신 규칙 1 의 4자 제한이 대가를 물린다.
        /// </summary>
        private static void LimitOptional(
            ParsedArtName parsed, int limit, List<ArtRuleViolation> violations, params MorphemeCategory[] required)
        {
            int optional = 0;
            for (int i = 0; i < parsed.Body.Count; i++)
            {
                MorphemeCategory category = parsed.Body[i].Category;
                if (category == MorphemeCategory.Background) continue;

                bool isRequired = false;
                for (int r = 0; r < required.Length; r++)
                {
                    if (required[r] == category) isRequired = true;
                }
                if (!isRequired) optional++;
            }

            if (optional <= limit) return;

            violations.Add(new ArtRuleViolation(ArtRule.OptionalOverflow,
                KindName(parsed.Kind) + " 무공의 선택 카테고리가 " + optional + "자다. 최대 " + limit + "자다"));
        }

        // ─────────────────────────── 표시 ───────────────────────────

        private static readonly MorphemeCategory[] AllCategories =
        {
            MorphemeCategory.AttackMethod, MorphemeCategory.Defense, MorphemeCategory.Internal,
            MorphemeCategory.Status, MorphemeCategory.Form, MorphemeCategory.Modifier,
            MorphemeCategory.Element, MorphemeCategory.Pinnacle, MorphemeCategory.Negation,
            MorphemeCategory.Tag, MorphemeCategory.Background, MorphemeCategory.Scope,
        };

        /// <summary>
        /// 앞 낱말의 받침 유무에 따라 주격 조사를 고른다 — `공격방식이` / `무공형태가`.
        /// 위반 메시지는 역산 리포트에 그대로 찍히므로 읽히는 문장이어야 한다.
        /// </summary>
        private static string Subject(string word)
        {
            char last = word[word.Length - 1];
            if (last < 0xAC00 || last > 0xD7A3) return "가";

            bool hasFinalConsonant = (last - 0xAC00) % 28 != 0;
            return hasFinalConsonant ? "이" : "가";
        }

        private static string KindName(ArtKind kind)
        {
            switch (kind)
            {
                case ArtKind.Attack: return "공격";
                case ArtKind.Internal: return "내공";
                case ArtKind.Movement: return "경공";
                default: return "?";
            }
        }

        private static string Describe(MorphemeCategory category)
        {
            switch (category)
            {
                case MorphemeCategory.AttackMethod: return "공격방식";
                case MorphemeCategory.Defense: return "방어";
                case MorphemeCategory.Internal: return "내공";
                case MorphemeCategory.Status: return "상태이상";
                case MorphemeCategory.Form: return "무공형태";
                case MorphemeCategory.Modifier: return "수식";
                case MorphemeCategory.Element: return "자연속성";
                case MorphemeCategory.Pinnacle: return "극한경지";
                case MorphemeCategory.Negation: return "부정";
                case MorphemeCategory.Tag: return "무학분류";
                case MorphemeCategory.Background: return "배경어";
                case MorphemeCategory.Scope: return "범위";
                default: return "?";
            }
        }
    }
}
