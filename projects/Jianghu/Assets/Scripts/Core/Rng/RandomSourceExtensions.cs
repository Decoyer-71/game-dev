using System;

namespace Jianghu.Core.Rng
{
    /// <summary>
    /// <see cref="IRandomSource"/> 를 게임 판정에 쓰기 편하게 감싼 헬퍼들.
    /// 구현체가 뭐든 이 헬퍼를 거치면 동작이 동일하다.
    /// </summary>
    public static class RandomSourceExtensions
    {
        /// <summary>
        /// [minInclusive, maxExclusive) 범위의 정수를 뽑는다.
        /// ⚠ 나머지 연산이라 아주 미세한 modulo bias 가 있다. 무협 전투 판정 수준에서는 무시 가능하다고 본다
        ///   (범위가 2^32 에 비해 압도적으로 작다). 통계적 엄밀함이 필요해지면 그때 재작성한다.
        /// </summary>
        public static int Range(this IRandomSource rng, int minInclusive, int maxExclusive)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxExclusive),
                    "maxExclusive 는 minInclusive 보다 커야 한다. (min=" + minInclusive + ", max=" + maxExclusive + ")");
            }

            uint span = unchecked((uint)((long)maxExclusive - minInclusive));
            return unchecked((int)(minInclusive + (long)(rng.NextUInt() % span)));
        }

        /// <summary>percent 퍼센트 확률로 true. 0 이하면 항상 false, 100 이상이면 항상 true.</summary>
        public static bool Chance(this IRandomSource rng, int percent)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (percent <= 0) return false;
            if (percent >= 100) return true;
            return rng.Range(0, 100) < percent;
        }
    }
}
