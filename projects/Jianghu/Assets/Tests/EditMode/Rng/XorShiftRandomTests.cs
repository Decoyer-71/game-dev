using System;
using System.Collections.Generic;
using Jianghu.Core.Rng;
using NUnit.Framework;

namespace Jianghu.Tests.Rng
{
    /// <summary>
    /// 결정론 보장 검증. 이게 깨지면 전투 테스트 전체가 의미를 잃는다.
    /// 이 파일은 `dotnet test` 와 Unity Test Runner 양쪽에서 그대로 컴파일된다
    /// (그래서 UnityEngine 을 참조하지 않는다).
    /// </summary>
    public class XorShiftRandomTests
    {
        [Test]
        public void 같은_시드는_같은_시퀀스를_낸다()
        {
            var a = new XorShiftRandom(12345u);
            var b = new XorShiftRandom(12345u);

            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(a.NextUInt(), b.NextUInt(), "{0}번째에서 갈라졌다", i);
            }
        }

        [Test]
        public void 다른_시드는_다른_시퀀스를_낸다()
        {
            var a = new XorShiftRandom(1u);
            var b = new XorShiftRandom(2u);

            bool anyDifference = false;
            for (int i = 0; i < 100; i++)
            {
                if (a.NextUInt() != b.NextUInt())
                {
                    anyDifference = true;
                    break;
                }
            }

            Assert.IsTrue(anyDifference, "시드 1 과 2 가 100회 내내 같은 값을 냈다 — 시드 혼합이 고장났다");
        }

        [Test]
        public void 시드_0_도_고장나지_않는다()
        {
            // xorshift 는 상태가 전부 0 이면 영원히 0 만 낸다. 시드 0 이 그 함정에 빠지는지 본다.
            var rng = new XorShiftRandom(0u);

            var seen = new HashSet<uint>();
            for (int i = 0; i < 100; i++)
            {
                seen.Add(rng.NextUInt());
            }

            Assert.Greater(seen.Count, 1, "시드 0 에서 같은 값만 반복되고 있다");
        }

        [Test]
        public void Range_는_지정한_범위를_벗어나지_않는다()
        {
            var rng = new XorShiftRandom(777u);

            for (int i = 0; i < 10000; i++)
            {
                int v = rng.Range(3, 8); // 3,4,5,6,7 만 나와야 한다
                Assert.GreaterOrEqual(v, 3);
                Assert.Less(v, 8);
            }
        }

        [Test]
        public void Range_는_범위_안_모든_값을_낸다()
        {
            var rng = new XorShiftRandom(31337u);
            var seen = new HashSet<int>();

            for (int i = 0; i < 10000; i++)
            {
                seen.Add(rng.Range(0, 6)); // 주사위
            }

            Assert.AreEqual(6, seen.Count, "0~5 중 안 나온 값이 있다: 실제로 나온 값 수 = {0}", seen.Count);
        }

        [Test]
        public void Range_는_잘못된_범위를_거부한다()
        {
            var rng = new XorShiftRandom(1u);

            Assert.Throws<ArgumentOutOfRangeException>(() => rng.Range(5, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.Range(5, 1));
        }

        [Test]
        public void Range_는_음수_범위도_처리한다()
        {
            var rng = new XorShiftRandom(99u);

            for (int i = 0; i < 1000; i++)
            {
                int v = rng.Range(-10, 10);
                Assert.GreaterOrEqual(v, -10);
                Assert.Less(v, 10);
            }
        }

        [Test]
        public void Chance_의_경계값은_확정적이다()
        {
            var rng = new XorShiftRandom(42u);

            for (int i = 0; i < 100; i++)
            {
                Assert.IsFalse(rng.Chance(0), "0% 가 true 를 냈다");
                Assert.IsFalse(rng.Chance(-5), "음수 확률이 true 를 냈다");
                Assert.IsTrue(rng.Chance(100), "100% 가 false 를 냈다");
                Assert.IsTrue(rng.Chance(150), "100 초과 확률이 false 를 냈다");
            }
        }

        [Test]
        public void Chance_의_실제_빈도가_지정_확률에_근접한다()
        {
            // 통계 검사라 시드에 따라 흔들린다. 넉넉한 허용 범위로 '완전히 틀어졌는지'만 본다.
            var rng = new XorShiftRandom(2026u);

            int hit = 0;
            const int trials = 100000;
            for (int i = 0; i < trials; i++)
            {
                if (rng.Chance(30)) hit++;
            }

            double ratio = (double)hit / trials;
            Assert.That(ratio, Is.EqualTo(0.30).Within(0.02), "30% 판정의 실제 적중률이 {0:P2} 다", ratio);
        }
    }
}
