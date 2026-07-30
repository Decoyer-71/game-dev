using System;
using Jianghu.Core.Martial;
using NUnit.Framework;

namespace Jianghu.Tests.Martial
{
    /// <summary>
    /// 성향별 성장곡선 검증.
    ///
    /// ⚠ 여기서 확인하는 것은 **절대 수치가 아니라 의도한 역전 순서**다.
    /// "초반엔 사파, 중반엔 정파, 후반엔 마도" 라는 무협 클리셰가 실제로 수치에서
    /// 일어나는지만 본다. 실제 밸런스(재미있는 수치인가)는 플레이로만 판정된다.
    /// 설계 근거: docs/jianghu-design.md §3.
    /// </summary>
    public class AlignmentCurveTests
    {
        private static double Power(Alignment alignment, int sessions)
        {
            return AlignmentCurve.PowerMultiplier(alignment, AlignmentCurve.ProficiencyFor(alignment, sessions));
        }

        [Test]
        public void 초반에는_사파가_가장_강하다()
        {
            const int sessions = 100;

            double orthodox = Power(Alignment.Orthodox, sessions);
            double unorthodox = Power(Alignment.Unorthodox, sessions);
            double demonic = Power(Alignment.Demonic, sessions);

            Assert.Greater(unorthodox, orthodox, "사파가 정파보다 약하다 (수련 {0}회)", sessions);
            Assert.Greater(orthodox, demonic, "정파가 마도보다 약하다 (수련 {0}회)", sessions);
        }

        [Test]
        public void 중반에는_정파가_사파를_넘어선다()
        {
            const int sessions = 150;

            double orthodox = Power(Alignment.Orthodox, sessions);
            double unorthodox = Power(Alignment.Unorthodox, sessions);

            Assert.Greater(orthodox, unorthodox,
                "정파가 아직 사파를 못 넘었다 (수련 {0}회: 정파 {1:F2} vs 사파 {2:F2})",
                sessions, orthodox, unorthodox);
        }

        [Test]
        public void 후반에는_마도가_가장_강하다()
        {
            const int sessions = 250;

            double orthodox = Power(Alignment.Orthodox, sessions);
            double unorthodox = Power(Alignment.Unorthodox, sessions);
            double demonic = Power(Alignment.Demonic, sessions);

            Assert.Greater(demonic, orthodox, "마도가 정파를 못 넘었다 (수련 {0}회)", sessions);
            Assert.Greater(orthodox, unorthodox, "정파가 사파보다 약하다 (수련 {0}회)", sessions);
        }

        [Test]
        public void 모든_성향이_결국_같은_숙련_상한에_도달한다()
        {
            // ⚠⚠ 2026-07-28 재설계 — 사파의 하드 상한 70 을 없앴다.
            //   상한이 닫히면 사파는 44회 수련 뒤 죽은 선택지가 된다(고를 명분이 사라진다).
            //   이제 차이는 "어디까지 가는가" 가 아니라 "거기 가는 비용" 과 "편차" 다.
            Assert.AreEqual(100, AlignmentCurve.MaxProficiency(Alignment.Orthodox));
            Assert.AreEqual(100, AlignmentCurve.MaxProficiency(Alignment.Unorthodox));
            Assert.AreEqual(100, AlignmentCurve.MaxProficiency(Alignment.Demonic));
        }

        [Test]
        public void 사파는_소프트캡_이후에도_계속_성장한다()
        {
            // 성장이 멈추지 않아야 한다. 멈추는 순간 그 선택지는 함정이 된다.
            int at70 = AlignmentCurve.ProficiencyFor(Alignment.Unorthodox, 44);
            int at100 = AlignmentCurve.ProficiencyFor(Alignment.Unorthodox, 100);
            int at200 = AlignmentCurve.ProficiencyFor(Alignment.Unorthodox, 200);

            Assert.AreEqual(70, at70, "소프트캡(70)에 도달하는 시점이 어긋났다");
            Assert.Greater(at100, at70, "소프트캡 이후 성장이 멈췄다");
            Assert.Greater(at200, at100, "소프트캡 이후 성장이 멈췄다");
            Assert.AreEqual(100, at200);
        }

        [Test]
        public void 사파는_소프트캡_이후_효율이_급락한다()
        {
            // 앞의 70 을 올리는 비용보다 뒤의 30 을 올리는 비용이 훨씬 커야 한다.
            int toSoftCap = 44;                                              // 숙련 0 → 70
            int toHardCap = AlignmentCurve.SessionsToMaster(Alignment.Unorthodox); // 숙련 0 → 100
            int lateCost = toHardCap - toSoftCap;                            // 숙련 70 → 100 에 든 수련

            Assert.Greater(lateCost, toSoftCap * 2,
                "소프트캡 이후 비용이 충분히 비싸지 않다 (앞 70: {0}회, 뒤 30: {1}회)", toSoftCap, lateCost);
        }

        [Test]
        public void 사파는_만렙에서도_고점이_가장_낮다()
        {
            // 상한은 같아도 '거기서 얼마나 강한가' 는 다르다 — 이게 고점 차이의 실체다.
            double orthodox = AlignmentCurve.PowerMultiplier(Alignment.Orthodox, 100);
            double unorthodox = AlignmentCurve.PowerMultiplier(Alignment.Unorthodox, 100);
            double demonic = AlignmentCurve.PowerMultiplier(Alignment.Demonic, 100);

            Assert.Less(unorthodox, orthodox, "사파 고점이 정파보다 높다");
            Assert.Less(orthodox, demonic, "정파 고점이 마도보다 높다");
        }

        [Test]
        public void 사파는_피해_변동폭이_가장_좁다()
        {
            // **사파의 존재 이유.** 고점이 낮은 대신 결과가 흔들리지 않는다 = 보장된 저점.
            int orthodox = AlignmentCurve.DamageVariancePercent(Alignment.Orthodox);
            int unorthodox = AlignmentCurve.DamageVariancePercent(Alignment.Unorthodox);
            int demonic = AlignmentCurve.DamageVariancePercent(Alignment.Demonic);

            Assert.Less(unorthodox, orthodox, "사파 편차가 정파보다 넓다");
            Assert.Less(orthodox, demonic, "정파 편차가 마도보다 넓다");
        }

        [Test]
        public void 사파는_같은_수련량으로_더_빨리_숙련된다()
        {
            const int sessions = 40;

            int unorthodox = AlignmentCurve.ProficiencyFor(Alignment.Unorthodox, sessions);
            int orthodox = AlignmentCurve.ProficiencyFor(Alignment.Orthodox, sessions);
            int demonic = AlignmentCurve.ProficiencyFor(Alignment.Demonic, sessions);

            Assert.Greater(unorthodox, orthodox);
            Assert.Greater(orthodox, demonic);
        }

        [Test]
        public void 마도는_계단식으로_오른다()
        {
            // ⚠ 절대 수치를 박지 않는다. 밸런싱할 때마다 깨지면 테스트가 방해물이 된다.
            //   검증할 것은 값이 아니라 **계단이라는 모양**이다.
            double At(int p) => AlignmentCurve.PowerMultiplier(Alignment.Demonic, p);

            // 경지를 넘기 전까지는 계단 안에서 전혀 오르지 않는다.
            Assert.AreEqual(At(0), At(24), 0.0001, "첫 경지 이전에 값이 올랐다");
            Assert.AreEqual(At(25), At(49), 0.0001, "같은 경지 안에서 값이 올랐다");
            Assert.AreEqual(At(50), At(74), 0.0001, "같은 경지 안에서 값이 올랐다");

            // 경지를 넘는 순간 뛴다.
            Assert.Greater(At(25), At(24), "경지를 넘었는데 값이 안 뛰었다");
            Assert.Greater(At(50), At(49), "경지를 넘었는데 값이 안 뛰었다");
            Assert.Greater(At(100), At(99), "만렙 경지에서 값이 안 뛰었다");

            // 계단 높이는 일정하다.
            Assert.AreEqual(At(25) - At(0), At(50) - At(25), 0.0001, "계단 높이가 들쭉날쭉하다");
        }

        [Test]
        public void 마도는_첫_경지_이전에는_정파보다도_약하다()
        {
            // 설계 의도다. "첫 계단까지 도달하는 비용" 이 마도가 치르는 대가다.
            double demonic = AlignmentCurve.PowerMultiplier(Alignment.Demonic, 20);
            double orthodox = AlignmentCurve.PowerMultiplier(Alignment.Orthodox, 20);

            Assert.Less(demonic, orthodox);
        }

        [Test]
        public void 정파는_선형으로_오른다()
        {
            // 구간별 증가폭이 일정해야 한다 — 예측 가능한 성장이 정파의 정체성이다.
            double d1 = AlignmentCurve.PowerMultiplier(Alignment.Orthodox, 25)
                        - AlignmentCurve.PowerMultiplier(Alignment.Orthodox, 0);
            double d2 = AlignmentCurve.PowerMultiplier(Alignment.Orthodox, 75)
                        - AlignmentCurve.PowerMultiplier(Alignment.Orthodox, 50);

            Assert.AreEqual(d1, d2, 0.0001);
        }

        [Test]
        public void 사파는_초반_증가폭이_후반보다_크다()
        {
            // 제곱근 곡선 — 초반에 급격히 오르고 갈수록 완만해진다.
            double early = AlignmentCurve.PowerMultiplier(Alignment.Unorthodox, 20)
                           - AlignmentCurve.PowerMultiplier(Alignment.Unorthodox, 0);
            double late = AlignmentCurve.PowerMultiplier(Alignment.Unorthodox, 70)
                          - AlignmentCurve.PowerMultiplier(Alignment.Unorthodox, 50);

            Assert.Greater(early, late);
        }

        [Test]
        public void 숙련도는_수련_횟수만으로_결정된다()
        {
            // 결정론 — 같은 횟수면 언제 어떻게 나눠 수련했든 결과가 같아야 한다.
            var once = new LearnedArt(TestArt(Alignment.Orthodox));
            once.Train(50);

            var split = new LearnedArt(TestArt(Alignment.Orthodox));
            for (int i = 0; i < 50; i++) split.Train();

            Assert.AreEqual(once.Proficiency, split.Proficiency);
            Assert.AreEqual(once.PowerMultiplier, split.PowerMultiplier, 0.0000001);
        }

        [Test]
        public void 상한에_도달하면_IsMastered_가_참이_된다()
        {
            var art = new LearnedArt(TestArt(Alignment.Unorthodox));
            Assert.IsFalse(art.IsMastered);

            art.Train(AlignmentCurve.SessionsToMaster(Alignment.Unorthodox));
            Assert.IsTrue(art.IsMastered);
            Assert.AreEqual(100, art.Proficiency);
        }

        [Test]
        public void 사파가_만렙에_가장_오래_걸리지는_않는다()
        {
            // 사파는 뒤가 비싸지만, 마도만큼 오래 걸려서는 안 된다 —
            // 그러면 "빠르다" 는 정체성 자체가 사라진다.
            int orthodox = AlignmentCurve.SessionsToMaster(Alignment.Orthodox);
            int unorthodox = AlignmentCurve.SessionsToMaster(Alignment.Unorthodox);
            int demonic = AlignmentCurve.SessionsToMaster(Alignment.Demonic);

            Assert.Less(unorthodox, orthodox, "사파가 정파보다 만렙이 느리다 ({0}회 vs {1}회)", unorthodox, orthodox);
            Assert.Less(orthodox, demonic, "정파가 마도보다 만렙이 느리다");
        }

        [Test]
        public void 음수_입력을_거부한다()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AlignmentCurve.ProficiencyFor(Alignment.Orthodox, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => AlignmentCurve.PowerMultiplier(Alignment.Orthodox, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LearnedArt(TestArt(Alignment.Orthodox)).Train(-1));
        }

        private static MartialArt TestArt(Alignment alignment)
        {
            return MartialArt.Technique("t", "시험초식", Discipline.Sword, alignment, basePower: 10, qiCost: 0);
        }
    }
}
