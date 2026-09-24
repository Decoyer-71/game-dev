using System.Globalization;

namespace Jianghu.Core.Martial.Display
{
    /// <summary>
    /// **수치 축 하나의 읽을거리** — 이름 · 값 · 단위. 표시 전용이다.
    ///
    /// ⚠ 값을 `double` 그대로 들고 있는다. 문자열로만 넘기면 화면 쪽이 부호로 색을 칠하거나
    ///   크기순으로 정렬할 수 없다 — 그건 표시 계층이 자주 하는 일이다.
    /// </summary>
    public readonly struct StatAxisValue
    {
        /// <summary>축 이름. 정의서 용어를 그대로 쓴다(치명률·치명배율은 §1-2 에서 확정된 구분이다).</summary>
        public string Name { get; }

        /// <summary>값. 부호가 살아 있다.</summary>
        public double Value { get; }

        /// <summary>
        /// 단위. 빈 문자열이면 단위 없는 스칼라다.
        ///
        /// ⚠⚠ **`%p` 와 `%` 를 구분한다.** 정의서가 구분하기 때문이다 — 회피율 `+5%p` 는
        ///   확률에 더하는 것이고 기력소모 `−30%` 는 값에 곱하는 것이다. 화면에서 둘을 같은
        ///   `%` 로 찍으면 *"이름이 성능을 거짓말한다"*(정의서 §0)의 작은 판본이 된다.
        /// </summary>
        public string Unit { get; }

        public StatAxisValue(string name, double value, string unit)
        {
            Name = name;
            Value = value;
            Unit = unit ?? string.Empty;
        }

        /// <summary>양수인가. 화면이 색을 고를 때 쓴다.</summary>
        public bool IsGain => Value > 0;

        /// <summary>
        /// `+1.5` / `-30%` / `+0.3배` 형태의 값 문자열. **이름은 붙이지 않는다** — 이름과 값을
        /// 다른 칸에 놓는 표가 많아서다.
        ///
        /// ⚠ <see cref="CultureInfo.InvariantCulture"/> 로 찍는다. 지역 설정에 따라 소수점이
        ///   쉼표가 되면 같은 코드가 PC 마다 다른 글자를 낸다.
        /// </summary>
        public string Text
        {
            get
            {
                string sign = Value > 0 ? "+" : string.Empty;
                return sign + Value.ToString("0.###", CultureInfo.InvariantCulture) + Unit;
            }
        }

        /// <summary>`공격 +1.5` 형태. 한 칸에 다 넣는 곳이 쓴다.</summary>
        public override string ToString()
        {
            return Name + " " + Text;
        }
    }
}
