using System;

namespace Jianghu.Core.Martial.Morphemes
{
    /// <summary>
    /// 형태소(形態素) 한 글자. 정의서 §3 표의 한 칸이 이 객체 하나다.
    ///
    /// 무공명은 이 글자들의 나열이고, 무공의 수치는 <see cref="Delta"/> 의 단순 합이다.
    /// 정의서 §0 의 채택 근거 1(*"콘텐츠 비용이 선형 → 상수"*)이 여기서 성립한다 —
    /// 무공 122개를 각각 조정하는 대신 이 객체 75개만 조정하면 된다.
    ///
    /// ⚠ **한글 표기(<see cref="Korean"/>)가 키다.** 한자가 아니다. 플레이어가 읽는 것도,
    ///   파서가 쪼개는 것도 한글이기 때문이다. 그래서 한글이 겹치는 두 한자는 공존할 수 없고,
    ///   정의서 §3-6 이 신(迅/神)·중(中/重)·명(命/明)·일(日/日) 네 건을 그 이유로 정리했다.
    /// </summary>
    public sealed class Morpheme
    {
        /// <summary>한글 표기 한 글자. **사전의 키이자 파서의 단위**다.</summary>
        public char Korean { get; }

        /// <summary>한자 한 글자. 표시와 근거 추적용이며 수치 유도에는 쓰지 않는다.</summary>
        public char Hanja { get; }

        /// <summary>
        /// 한글 개념어(예: "베기" · "막기" · "밝다"). 정의서 §3 표의 '의미' 열이다.
        ///
        /// 두 곳에서 쓴다. ⓐ 역산 리포트(설계안 §5-2)가 사람이 읽을 수 있게 하고,
        /// ⓑ 정의서 §6-2 의 **이중 인덱스**(한자 표기 ↔ 한글 개념어) 중 개념어 쪽이 여기다.
        /// 문파 특징 문장의 *"'빠른' '찌르기' 검술"* 이 이 문자열과 맞물린다.
        /// </summary>
        public string Meaning { get; }

        /// <summary>카테고리. 조합 규칙(정의서 §2-2)이 이 값 단위로 판정한다.</summary>
        public MorphemeCategory Category { get; }

        /// <summary>이 글자가 더하는 수치. 부정·무학분류·배경어는 <see cref="ArtStatDelta.Zero"/> 다.</summary>
        public ArtStatDelta Delta { get; }

        /// <summary>무학분류 태그(§3-10). 일·월·혼만 <see cref="ArtLineage.None"/> 이 아니다.</summary>
        public ArtLineage Lineage { get; }

        /// <summary>
        /// 부정 한자인가(§3-9 낙·망·멸·산·소).
        /// 바로 뒤에 무학분류 글자가 오면 그 분류에 상성 +1 을 만든다(§4).
        /// </summary>
        public bool IsNegation { get; }

        /// <summary>
        /// 무공형태(§3-5)의 효과를 **페널티까지 함께** 2배로 만드는가. 극한경지 종(宗) 하나뿐이다.
        ///
        /// ⚠ 이 글자만 수치가 아니라 **규칙**을 만진다. 그래서 <see cref="ArtStatDelta"/> 로 표현되지 않고
        ///   별도 플래그로 둔다. 이득과 페널티를 함께 키우므로 상위호환이 아니라
        ///   "극단으로 미는" 선택이 된다(정의서 §3-8 주석).
        /// </summary>
        public bool DoublesFormEffect { get; }

        /// <summary>
        /// 이 글자가 정하는 타격 범위(§3-12). 범위 형태소가 아니면 <see cref="AttackScope.Single"/> 이다.
        /// ⚠ 수치가 아니라 **대상 수**라서 <see cref="ArtStatDelta"/> 로 표현하지 않는다 —
        ///   "전원" 을 숫자로 담으려면 마법의 상수가 필요해지고, 그러면 합산 연산이 거짓말을 한다.
        /// </summary>
        public AttackScope Scope { get; }

        /// <summary>
        /// 이 글자가 주는 **절대경지 규칙**(§5-3). 규칙 형태소가 아니면 <see cref="Morphemes.AbsoluteRule.None"/> 이다.
        /// ⚠ <see cref="Scope"/>·<see cref="DoublesFormEffect"/> 와 같은 이유로 <see cref="ArtStatDelta"/> 밖에 둔다 —
        ///   **수치가 아니라 규칙**이라서 합산 연산이 의미를 갖지 않는다.
        /// </summary>
        public AbsoluteRule Rule { get; }

        /// <summary>배경어인가(설계안 결정 A). 기력 소모 글자 수와 §2-2 슬롯 계산에서 빠진다.</summary>
        public bool IsBackground => Category == MorphemeCategory.Background;

        public Morpheme(
            char korean, char hanja, string meaning, MorphemeCategory category, ArtStatDelta delta,
            ArtLineage lineage = ArtLineage.None, bool isNegation = false, bool doublesFormEffect = false,
            AttackScope scope = AttackScope.Single, AbsoluteRule rule = AbsoluteRule.None)
        {
            if (string.IsNullOrEmpty(meaning))
            {
                throw new ArgumentException("형태소의 개념어는 비어 있을 수 없다.", nameof(meaning));
            }

            Korean = korean;
            Hanja = hanja;
            Meaning = meaning;
            Category = category;
            Delta = delta;
            Lineage = lineage;
            IsNegation = isNegation;
            DoublesFormEffect = doublesFormEffect;
            Scope = scope;
            Rule = rule;
        }

        /// <summary>`참(斬)` 형태로 찍는다. 역산 리포트(설계안 §5-2)의 출력 단위다.</summary>
        public override string ToString()
        {
            return Korean + "(" + Hanja + ")";
        }
    }
}
