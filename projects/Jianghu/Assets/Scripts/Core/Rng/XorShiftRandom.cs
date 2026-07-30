namespace Jianghu.Core.Rng
{
    /// <summary>
    /// xorshift128 기반 결정론적 난수원.
    ///
    /// ⚠⚠ <c>System.Random</c> 을 쓰지 않는 이유 — 이게 이 클래스가 존재하는 유일한 이유다.
    /// <c>System.Random</c> 의 내부 알고리즘은 **런타임 구현마다 다르다.**
    /// .NET (Core) 계열과 Unity 가 쓰는 Mono 는 같은 시드에 서로 다른 시퀀스를 낸다.
    /// 우리는 `dotnet test` 로 검증한 전투 결과가 Unity 플레이에서도 똑같이 나와야 하므로,
    /// 런타임에 의존하지 않도록 알고리즘 자체를 여기에 고정한다.
    /// </summary>
    public sealed class XorShiftRandom : IRandomSource
    {
        private uint _x;
        private uint _y;
        private uint _z;
        private uint _w;

        /// <summary>시드 하나로 초기화한다. 같은 시드는 항상 같은 시퀀스를 낸다.</summary>
        public XorShiftRandom(uint seed)
        {
            // 시드 1개를 상태 4개로 흩뿌린다(SplitMix 계열 혼합).
            // xorshift 는 상태가 전부 0 이면 영원히 0 만 내놓고 고장나므로, Mix 가 0 을 내지 않도록 보장한다.
            _x = Mix(ref seed);
            _y = Mix(ref seed);
            _z = Mix(ref seed);
            _w = Mix(ref seed);
        }

        private static uint Mix(ref uint state)
        {
            unchecked
            {
                state += 0x9E3779B9u;
                uint z = state;
                z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                z = z ^ (z >> 16);
                return z | 1u; // 최하위 비트를 세워 0 을 원천 차단. 상태 공간을 약간 잃지만 안전을 택한다
            }
        }

        /// <inheritdoc />
        public uint NextUInt()
        {
            unchecked
            {
                uint t = _x ^ (_x << 11);
                _x = _y;
                _y = _z;
                _z = _w;
                _w = _w ^ (_w >> 19) ^ t ^ (t >> 8);
                return _w;
            }
        }
    }
}
