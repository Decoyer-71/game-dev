using System.Collections.Generic;
using Jianghu.Core.Martial.Morphemes;

namespace Jianghu.Core.Martial.Display
{
    /// <summary>
    /// **무공명의 한 토막이 무엇을 넣었는가.** 형태소 한 글자이거나, 수치를 주지 않는 접미사다.
    ///
    /// ⚠⚠ **접미사도 항목으로 낸다.** 안 그러면 `신풍양공` 의 `공` 이 목록에서 사라져
    ///   *"이름에서 수치가 풀린다"*(정의서 §0)를 보여주려는 화면이 **이름의 일부를 숨긴 채**
    ///   설명하게 된다. 대신 <see cref="IsSuffix"/> 로 갈라 두고 카테고리에 *"형태소 아님"* 을 적는다.
    /// </summary>
    public sealed class MorphemeContribution
    {
        /// <summary>이름에서 이 항목이 차지하는 글자. 형태소는 1자, 접미사는 1~2자다.</summary>
        public string Text { get; }

        /// <summary>형태소 원본. **접미사면 null 이다.**</summary>
        public Morpheme Morpheme { get; }

        /// <summary>접미사 원본. **형태소면 null 이다.**</summary>
        public ArtSuffix Suffix { get; }

        /// <summary>수치를 주지 않는 접미사인가.</summary>
        public bool IsSuffix => Suffix != null;

        /// <summary>한자. 접미사는 한자를 들고 있지 않으므로 빈 문자열이다.</summary>
        public string Hanja { get; }

        /// <summary>개념어(정의서 §3 표의 '의미' 열). 접미사는 그것이 밝히는 무공 종류를 적는다.</summary>
        public string Meaning { get; }

        /// <summary>카테고리 이름. 접미사는 `접미사(형태소 아님)`.</summary>
        public string CategoryName { get; }

        /// <summary>
        /// 이 글자가 넣은 축들. **0 인 축은 빠져 있고, 종(宗) 2배가 이미 적용된 값이다.**
        /// ⚠ 비어 있을 수 있다 — 부정·무학분류·배경어·접미사가 그렇다(설계다, 버그가 아니다).
        /// </summary>
        public IReadOnlyList<StatAxisValue> Axes { get; }

        /// <summary>종(宗)이 이 글자의 수치를 2배로 만들었는가. 화면이 그 사실을 밝힐 수 있게 낸다.</summary>
        public bool Doubled { get; }

        private MorphemeContribution(
            string text, Morpheme morpheme, ArtSuffix suffix, string hanja, string meaning,
            string categoryName, IReadOnlyList<StatAxisValue> axes, bool doubled)
        {
            Text = text;
            Morpheme = morpheme;
            Suffix = suffix;
            Hanja = hanja;
            Meaning = meaning;
            CategoryName = categoryName;
            Axes = axes;
            Doubled = doubled;
        }

        /// <summary>형태소 한 글자의 기여. <paramref name="doublesForm"/> 은 이름에 종(宗)이 있는지다.</summary>
        public static MorphemeContribution FromMorpheme(Morpheme morpheme, bool doublesForm)
        {
            if (morpheme == null) throw new System.ArgumentNullException(nameof(morpheme));

            // ⚠ 2배 규칙을 여기서 다시 적지 않는다 — 파서와 같은 함수를 부른다.
            ArtStatDelta effective = MorphemeParser.EffectiveDelta(morpheme, doublesForm);
            bool doubled = doublesForm && morpheme.Category == MorphemeCategory.Form && !morpheme.Delta.IsZero;

            return new MorphemeContribution(
                morpheme.Korean.ToString(), morpheme, null,
                morpheme.Hanja.ToString(), morpheme.Meaning,
                KoreanNames.Of(morpheme.Category),
                ArtStatDeltaDisplay.NonZeroAxes(effective),
                doubled);
        }

        /// <summary>접미사 항목. 수치가 없고 무공 종류만 밝힌다.</summary>
        public static MorphemeContribution FromSuffix(ArtSuffix suffix)
        {
            if (suffix == null) throw new System.ArgumentNullException(nameof(suffix));

            return new MorphemeContribution(
                suffix.Text, null, suffix,
                string.Empty, KoreanNames.Of(suffix.Kind) + " 무공",
                "접미사(형태소 아님)",
                new StatAxisValue[0],
                false);
        }

        /// <summary>`참(斬) 베기 · 공격방식 — 공격 +1` 형태.</summary>
        public override string ToString()
        {
            string head = IsSuffix ? Text : Text + "(" + Hanja + ")";
            return head + " " + Meaning + " · " + CategoryName + " — " + ArtStatDeltaDisplay.Join(Axes);
        }
    }
}
