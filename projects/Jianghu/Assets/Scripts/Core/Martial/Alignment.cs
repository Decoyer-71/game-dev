namespace Jianghu.Core.Martial
{
    /// <summary>
    /// 무공의 성향(性向). 이 프로토타입이 검증하려는 재미의 핵심 축이다.
    ///
    /// 무협 클리셰 — "사파는 빨리 강해지나 한계가 있고, 정파는 느리나 고점이 높으며,
    /// 마도는 가장 오래 걸리지만 한 단계 오를 때마다 폭발한다" — 를 수치로 번역한 것이다.
    /// 실제 곡선은 <see cref="AlignmentCurve"/> 에 있다.
    /// 설계 근거: docs/jianghu-design.md §3.
    /// </summary>
    public enum Alignment
    {
        /// <summary>정파(正派) — 느리지만 꾸준하고 고점이 높다.</summary>
        Orthodox = 0,

        /// <summary>사파(邪派) — 빨리 강해지나 숙련 상한이 낮다.</summary>
        Unorthodox = 1,

        /// <summary>마도(魔道) — 가장 느리지만 경지마다 계단식으로 폭증한다.</summary>
        Demonic = 2,
    }
}
