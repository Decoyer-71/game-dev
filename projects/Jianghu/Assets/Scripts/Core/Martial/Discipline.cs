namespace Jianghu.Core.Martial
{
    /// <summary>
    /// 무공의 유형(類型). 성향과 함께 조합의 두 축을 이룬다.
    ///
    /// ⚠ 구상안에는 7종(검·도·창·권·비도·내공·경공)이 있으나 프로토타입은 5종만 쓴다.
    ///   조합이 의미를 갖는지부터 확인한 뒤 늘린다 — 축을 먼저 늘리면 일부 값이
    ///   필연적으로 약체가 되고 이해 비용만 는다(河洛群侠传 사례, docs/concepts/ 참조).
    /// </summary>
    public enum Discipline
    {
        /// <summary>검(劍) — 균형형. 명중이 높다.</summary>
        Sword = 0,

        /// <summary>도(刀) — 고위력·저명중. 한 방이 크다.</summary>
        Blade = 1,

        /// <summary>권(拳) — 다단 타격·저위력. 기력 소모가 적다.</summary>
        Fist = 2,

        /// <summary>내공(內功) — 보조. 최대 기력과 초식 위력에 관여한다.</summary>
        InnerArt = 3,

        /// <summary>경공(輕功) — 보조. 회피와 선공에 관여한다.</summary>
        Movement = 4,

        /// <summary>창(槍) — 가장 빨리 숙달된다(백일창). 선공이 강점.</summary>
        Spear = 5,

        /// <summary>비도(飛刀) — 암기. 위력은 낮지만 **상태이상을 잘 건다**.</summary>
        Dagger = 6,
    }

    /// <summary>유형이 공격 초식인지 보조 무공인지 판별한다.</summary>
    public static class DisciplineExtensions
    {
        /// <summary>내공·경공은 스스로 공격하지 않고 다른 초식을 변형시키는 보조 무공이다.</summary>
        public static bool IsSupport(this Discipline discipline)
        {
            return discipline == Discipline.InnerArt || discipline == Discipline.Movement;
        }
    }
}
