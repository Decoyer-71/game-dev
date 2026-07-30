using System.Collections.Generic;
using System.Text;

namespace Jianghu.Core.Martial.Morphemes
{
    /// <summary>
    /// 무공명을 형태소로 분해한 결과. **무공의 수치가 전부 여기서 나온다.**
    ///
    /// 정의서 §0 의 *"무공명 = 형태소 조합"* 이 실제로 성립하는 지점이다 —
    /// `천양신공` 을 넣으면 접미사 `신공`(내공 무공), 본체 `천`(배경어)·`양`(최대기력+10) 으로 갈리고
    /// <see cref="Delta"/> 가 그 합이 된다.
    ///
    /// ⚠ 이 객체는 **조합 규칙을 검사하지 않는다.** 분해만 한다. 규칙 판정은 <see cref="ArtCompositionRule"/> 이
    ///   따로 한다 — 분해와 판정을 분리해야 역산 리포트(설계안 §5-2)가 *"분해는 됐지만 규칙에 걸린다"* 를
    ///   구분해 보고할 수 있다.
    /// </summary>
    public sealed class ParsedArtName
    {
        /// <summary>원래 무공명 전체. 예: "천양신공".</summary>
        public string Name { get; }

        /// <summary>
        /// 떼어낸 접미사. 예: `~신공`. **문파 무공은 null 이다.**
        ///
        /// ⚠⚠ 2026-07-29 결정 — 접미사는 **강호무학 전용**이다. 접미사가 4자 중 2자를 먹으므로
        ///   접미사를 붙이면 형태소가 2개로 줄고, 안 붙이면 4자 전부가 형태소가 된다.
        ///   즉 **접미사를 쓰느냐가 곧 세기**이며, 그것을 계층에 묶은 것이다
        ///   (`docs/martial-art-naming.md` §1).
        /// </summary>
        public ArtSuffix Suffix { get; }

        /// <summary>
        /// 무공 종류.
        ///
        /// 접미사가 있으면 접미사가 정하고, **없으면 호출자가 알려줘야 한다.**
        /// 접미사를 뗀 문파 무공은 이름만으로 공격·내공·경공을 구분할 수 없기 때문이다 —
        /// 그 정보는 문파의 무공 슬롯 데이터에 있다.
        /// </summary>
        public ArtKind Kind { get; }

        /// <summary>접미사를 쓴 이름인가. 계층 검사가 쓴다 — 강호무학만 true 여야 한다.</summary>
        public bool HasSuffix => Suffix != null;

        /// <summary>접미사를 뗀 본체의 형태소들. 이름에 쓰인 순서 그대로다.</summary>
        public IReadOnlyList<Morpheme> Body { get; }

        /// <summary>
        /// 본체 형태소들의 수치 합.
        ///
        /// ⚠ 극한경지 종(宗)이 있으면 **무공형태 형태소의 수치가 페널티까지 함께 2배**로 들어간다
        ///   (정의서 §3-8). 성향 배율은 여기 곱해지지 않는다 — 그건 전투 시점의 일이다.
        /// </summary>
        public ArtStatDelta Delta { get; }

        /// <summary>본체에 쓰인 배경어 수. 규제 R2 상 0 또는 1 이다.</summary>
        public int BackgroundCount { get; }

        /// <summary>
        /// 배경어를 뺀 유효 형태소 수. **기력 소모의 기준**이다(2026-07-29 결정 B).
        /// </summary>
        public int EffectiveMorphemeCount => Body.Count - BackgroundCount;

        /// <summary>
        /// 이 무공의 기력 소모.
        ///
        /// `(본체 글자 수 − 배경어 수) × <see cref="MorphemeParser.QiCostPerMorpheme"/>` 에
        /// <see cref="ArtStatDelta.QiCostPercent"/> 를 곱한다.
        ///
        /// ⚠ 배경어를 빼는 것이 핵심이다. 빼지 않으면 어감을 위해 배경어를 넣었다는 이유로 기력을 더 내게 되고,
        ///   그러면 아무도 배경어를 쓰지 않아 **허용한 목적 자체가 사라진다.**
        ///   배경어는 성능도 비용도 0 인 순수 중립이어야 일관된다.
        ///
        /// ⚠ 증감률을 곱하는 지점이 여기다. 극한경지 선(仙)의 `−30%` 와 범위 만(萬)의 `+200%` 가
        ///   비로소 곱할 대상을 갖는다 — 정의서에 기준 소모량이 없어 둘 다 무력했던 문제(설계안 §1-B)가 닫힌다.
        /// </summary>
        public int QiCost
        {
            get
            {
                double basis = EffectiveMorphemeCount * MorphemeParser.QiCostPerMorpheme;
                double scaled = basis * (100.0 + Delta.QiCostPercent) / 100.0;
                return scaled < 0 ? 0 : (int)System.Math.Round(scaled, System.MidpointRounding.AwayFromZero);
            }
        }

        /// <summary>
        /// 이 무공이 한 번에 때리는 대상 수(§3-12). 범위 형태소가 없으면 <see cref="AttackScope.Single"/>.
        /// ⚠ 카테고리당 1자 규칙 덕분에 범위 형태소는 최대 하나다.
        /// </summary>
        public AttackScope Scope { get; }

        /// <summary>
        /// 이 무공 자신의 무학분류(§3-10). 부정 뒤에 붙지 않은 일·월·혼이 여기 온다.
        ///
        /// ⚠ **추론이다.** 정의서 §3-10 은 일·월·혼을 "태그" 라고만 하고 §4 는 부정 뒤에 올 때만 다룬다.
        ///   부정이 앞에 없는 무학분류 글자가 무엇을 하는지는 명시돼 있지 않아, **그 무공의 분류를 정한다**고
        ///   읽었다. 정의서에 확인이 필요한 항목이다.
        /// </summary>
        public ArtLineage Lineage { get; }

        /// <summary>
        /// 상성 우위 대상(§4). 부정 한자 **바로 뒤에** 무학분류 한자가 올 때마다 하나씩 쌓인다.
        /// `낙월` = 달을 떨어뜨린다 → 음기무학 하나.
        /// </summary>
        public IReadOnlyList<ArtLineage> CounterTargets { get; }

        public ParsedArtName(
            string name, ArtSuffix suffix, ArtKind kind, IReadOnlyList<Morpheme> body, ArtStatDelta delta,
            int backgroundCount, ArtLineage lineage, IReadOnlyList<ArtLineage> counterTargets)
        {
            Name = name;
            Suffix = suffix;
            Kind = kind;

            AttackScope scope = AttackScope.Single;
            for (int i = 0; i < body.Count; i++)
            {
                if (body[i].Scope != AttackScope.Single) scope = body[i].Scope;
            }
            Scope = scope;
            Body = body;
            Delta = delta;
            BackgroundCount = backgroundCount;
            Lineage = lineage;
            CounterTargets = counterTargets;
        }

        /// <summary>지정한 분류에 대한 상성 수치. 정의서 §4 기준 상성 1당 주는 피해 +10% · 받는 피해 −5%.</summary>
        public int AdvantageAgainst(ArtLineage lineage)
        {
            if (lineage == ArtLineage.None) return 0;

            int count = 0;
            for (int i = 0; i < CounterTargets.Count; i++)
            {
                if (CounterTargets[i] == lineage) count++;
            }
            return count;
        }

        private string KindLabel()
        {
            switch (Kind)
            {
                case ArtKind.Attack: return "공격";
                case ArtKind.Internal: return "내공";
                case ArtKind.Movement: return "경공";
                default: return "?";
            }
        }

        /// <summary>같은 카테고리 형태소가 본체에 몇 자 있는가. 조합 규칙 판정이 쓴다.</summary>
        public int CountOf(MorphemeCategory category)
        {
            int count = 0;
            for (int i = 0; i < Body.Count; i++)
            {
                if (Body[i].Category == category) count++;
            }
            return count;
        }

        /// <summary>`천양신공 = 천(天) + 양(陽) [~신공]` 형태. 역산 리포트(설계안 §5-2)의 출력 단위다.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Name).Append(" = ");
            for (int i = 0; i < Body.Count; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(Body[i].ToString());
            }
            if (Body.Count == 0) sb.Append("(본체 없음)");
            sb.Append(" [").Append(HasSuffix ? Suffix.ToString() : KindLabel()).Append(']');
            return sb.ToString();
        }
    }
}
