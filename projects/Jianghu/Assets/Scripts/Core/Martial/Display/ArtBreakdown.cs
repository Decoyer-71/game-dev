using System.Collections.Generic;
using Jianghu.Core.Martial.Morphemes;

namespace Jianghu.Core.Martial.Display
{
    /// <summary>
    /// **무공 하나를 화면에 그릴 수 있는 형태로 편 것.** 순수 함수라 `dotnet test` 가 닿는다.
    ///
    /// ⚠⚠ **Unity 층에 이 계산을 두지 않는 이유**가 이 클래스의 요점이다. Phase 4 첫 화면을
    ///   띄워보니 ⓐ 축 하나만 찍어 성(聖)이 무효과로 보이고 ⓑ enum 이 영문으로 새고
    ///   ⓒ 접미사 글자가 목록에서 사라졌는데, 셋 다 *"화면이 안 예쁘다"* 가 아니라
    ///   **표시 로직이 테스트가 닿지 않는 곳에 있다**는 한 가지 문제였다. 그래서 Core 로 내렸다.
    ///
    /// ⚠ 여기서 게임 규칙을 새로 만들지 않는다. 전부 <see cref="MartialArtFactory.Decompose"/> 가
    ///   낸 것을 읽어 이름을 붙일 뿐이다.
    /// </summary>
    public sealed class ArtBreakdown
    {
        /// <summary>원본 무공.</summary>
        public MartialArt Art { get; }

        /// <summary>다시 분해한 결과. 화면이 더 파고들 때 쓴다.</summary>
        public ParsedArtName Parsed { get; }

        /// <summary>무공명. 예: `성뇌후격`.</summary>
        public string Name => Art.Name;

        /// <summary>문파. 강호무학은 null 이다.</summary>
        public string School => Art.School;

        public string TierName => KoreanNames.Of(Art.Tier);

        public string DisciplineName => KoreanNames.Of(Art.Discipline);

        /// <summary>성향. ⚠ 강호무학은 무공에 성향이 없다 — 익힌 사람을 따른다.</summary>
        public string AlignmentName => KoreanNames.Of(Art.Alignment, "익힌 사람을 따름");

        public string KindName => KoreanNames.Of(Art.Kind);

        public string ScopeName => KoreanNames.Of(Art.Scope);

        public string RowName => KoreanNames.Of(Art.PreferredRow);

        /// <summary>무학분류(§3-10). 없으면 `없음`.</summary>
        public string LineageName => KoreanNames.Of(Parsed.Lineage);

        /// <summary>절대경지 규칙(§5-3). 없으면 `없음`.</summary>
        public string RuleName => KoreanNames.Of(Art.Rule);

        /// <summary>상성 우위 대상(§4) 이름들. 없으면 빈 목록이다.</summary>
        public IReadOnlyList<string> CounterNames { get; }

        /// <summary>
        /// **이름의 토막들** — 본체 형태소가 이름에 쓰인 순서 그대로, 접미사가 있으면 맨 뒤에.
        /// ⚠⚠ 이 목록의 <see cref="MorphemeContribution.Text"/> 를 이어붙이면 <see cref="Name"/> 이 된다.
        ///   회귀 테스트가 138종 전부에 대해 그것을 검사한다 — 화면이 이름의 일부를 삼키지 못하게.
        /// </summary>
        public IReadOnlyList<MorphemeContribution> Characters { get; }

        /// <summary>합계 수치 중 0 이 아닌 축들.</summary>
        public IReadOnlyList<StatAxisValue> Total { get; }

        /// <summary>기력 소모. 접미사는 글자 수에 안 들어가고 배경어는 빠진다(<see cref="ParsedArtName.QiCost"/>).</summary>
        public int QiCost => Art.QiCost;

        /// <summary>타격 횟수.</summary>
        public int HitCount => Art.HitCount;

        /// <summary>종(宗)이 무공형태 수치를 2배로 만들었는가(§3-8).</summary>
        public bool FormEffectDoubled { get; }

        private ArtBreakdown(
            MartialArt art, ParsedArtName parsed, IReadOnlyList<MorphemeContribution> characters,
            IReadOnlyList<StatAxisValue> total, IReadOnlyList<string> counterNames, bool formDoubled)
        {
            Art = art;
            Parsed = parsed;
            Characters = characters;
            Total = total;
            CounterNames = counterNames;
            FormEffectDoubled = formDoubled;
        }

        /// <summary>
        /// 무공을 펴낸다.
        /// ⚠ 분해는 <see cref="MartialArtFactory.Decompose"/> 를 쓴다 — 강호무학이냐로 갈리는
        ///   규칙이 거기 한 곳에만 있다. 파서를 직접 부르면 129종이 예외를 낸다.
        /// </summary>
        public static ArtBreakdown Of(MartialArt art)
        {
            if (art == null) throw new System.ArgumentNullException(nameof(art));

            ParsedArtName parsed = MartialArtFactory.Decompose(art);
            bool doublesForm = MorphemeParser.HasFormDoubler(parsed.Body);

            var characters = new List<MorphemeContribution>(parsed.Body.Count + 1);
            for (int i = 0; i < parsed.Body.Count; i++)
            {
                characters.Add(MorphemeContribution.FromMorpheme(parsed.Body[i], doublesForm));
            }
            if (parsed.HasSuffix) characters.Add(MorphemeContribution.FromSuffix(parsed.Suffix));

            var counters = new List<string>(parsed.CounterTargets.Count);
            for (int i = 0; i < parsed.CounterTargets.Count; i++)
            {
                counters.Add(KoreanNames.Of(parsed.CounterTargets[i]));
            }

            return new ArtBreakdown(
                art, parsed, characters,
                ArtStatDeltaDisplay.NonZeroAxes(parsed.Delta),
                counters, doublesForm);
        }

        /// <summary>`전승무학 · 소림사 · 권 · 정파 · 공격` 형태의 한 줄 머리말.</summary>
        public string HeadLine()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(TierName);
            if (!string.IsNullOrEmpty(School)) sb.Append(" · ").Append(School);
            sb.Append(" · ").Append(DisciplineName)
              .Append(" · ").Append(AlignmentName)
              .Append(" · ").Append(KindName);
            return sb.ToString();
        }

        public override string ToString()
        {
            return Name + " [" + HeadLine() + "]";
        }
    }
}
