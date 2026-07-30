using System;
using System.Collections.Generic;

namespace Jianghu.Core.Martial.Morphemes
{
    /// <summary>
    /// **무공명 하나에서 <see cref="MartialArt"/> 를 만든다.** 형태소 체계의 마지막 다리다.
    ///
    /// 이 클래스가 하는 일은 셋뿐이고, 전부 이미 만들어 둔 것을 잇는 것이다:
    ///   1. <see cref="MorphemeParser"/> 로 이름을 분해한다
    ///   2. <see cref="ArtCompositionRule"/> 로 조합 규칙을 검사한다 — **어기면 예외**
    ///   3. 분해 결과를 <see cref="MartialArt"/> 에 담는다
    ///
    /// ⚠⚠ **수치를 인자로 받지 않는다.** 정의서 §0 의 *"무공명 = 형태소 조합으로 수치를 자동 유도"* 가
    ///   여기서 실물이 된다. 이 지점 이후로 무공 수치를 손으로 적는 곳은 없다.
    ///
    /// ⚠ 규칙 위반을 조용히 넘기지 않는 이유 — 데이터 오류를 놓치면 나중에 밸런스가 왜 이상한지
    ///   추적할 수 없다. 역산 리포트(설계안 §5-2)처럼 **모아서 보고**해야 할 때는
    ///   <see cref="TryCreate"/> 를 쓴다.
    /// </summary>
    public static class MartialArtFactory
    {
        /// <summary>무공명에서 무공을 만든다. 분해나 규칙 검사에 실패하면 예외를 던진다.</summary>
        /// <param name="tier">
        /// 계층. 규칙 두 가지가 여기 달려 있다 — **극한경지는 전승무학에만**, **범위(광역)는 대문파 이상에만**.
        /// ⚠ 강호무학(<see cref="ArtTier.Wanderer"/>)은 **접미사에서** 무공 종류를 얻으므로 `kind` 를 무시한다.
        /// </param>
        public static MartialArt Create(
            string id, string name, ArtKind kind, ArtTier tier,
            Discipline discipline, Alignment? alignment, string school = null,
            int hitCount = 1, params StatusApplication[] effects)
        {
            MartialArt art;
            IReadOnlyList<string> problems;
            if (!TryCreate(id, name, kind, tier, discipline, alignment, school, hitCount, effects,
                    out art, out problems))
            {
                throw new ArgumentException(
                    "무공 '" + name + "' 을(를) 만들 수 없다: " + string.Join(" · ", problems), nameof(name));
            }
            return art;
        }

        /// <summary>
        /// 무공을 만들되 실패를 예외 대신 목록으로 돌려준다.
        /// 138종을 한 번에 검증하거나 역산 리포트를 만들 때 쓴다.
        /// </summary>
        public static bool TryCreate(
            string id, string name, ArtKind kind, ArtTier tier,
            Discipline discipline, Alignment? alignment, string school, int hitCount,
            StatusApplication[] effects,
            out MartialArt art, out IReadOnlyList<string> problems)
        {
            art = null;
            var found = new List<string>();
            problems = found;

            // 강호무학만 무기 접미사로 종류를 밝힌다. 나머지 계층은 종류를 명시받는다(정의서 §2-4).
            ArtKind? parseKind = tier == ArtTier.Wanderer ? (ArtKind?)null : kind;

            ParsedArtName parsed;
            IReadOnlyList<string> parseProblems;
            if (!MorphemeParser.TryParse(name, parseKind, out parsed, out parseProblems))
            {
                found.AddRange(parseProblems);
                return false;
            }

            IReadOnlyList<ArtRuleViolation> violations = ArtCompositionRule.Validate(parsed, tier);
            if (violations.Count > 0)
            {
                for (int i = 0; i < violations.Count; i++) found.Add(violations[i].Message);
                return false;
            }

            art = MartialArt.FromMorphemes(
                id, name, school, discipline, alignment, parsed.Delta, parsed.QiCost, hitCount, effects);
            return true;
        }
    }
}
