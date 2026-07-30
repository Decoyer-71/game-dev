namespace Jianghu.Core.Rng
{
    /// <summary>
    /// 게임 결과에 영향을 주는 모든 난수의 유일한 출처.
    ///
    /// 전투 판정·성장·이벤트 등에서 <c>UnityEngine.Random</c> 이나 <c>System.Random</c> 을
    /// 직접 쓰지 않고 반드시 이 인터페이스를 주입받아 쓴다.
    /// 근거: docs/jianghu-design.md §3-2 (결정론적 난수).
    /// 같은 시드 → 같은 결과가 보장되어야 전투를 테스트할 수 있고 밸런싱이 가능하다.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>0 이상 2^32 미만의 난수를 하나 뽑고 내부 상태를 전진시킨다.</summary>
        uint NextUInt();
    }
}
