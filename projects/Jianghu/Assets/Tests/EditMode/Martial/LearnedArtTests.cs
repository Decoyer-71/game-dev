using System;
using Jianghu.Core.Martial;
using NUnit.Framework;

namespace Jianghu.Tests.Martial
{
    /// <summary>
    /// 익힌 무공의 **유효 성향** 검증 (2026-07-30 결정).
    ///
    /// ⚠⚠ 성향 배타 규칙(정파 무공을 배우면 사파·마도를 못 배운다)이 들어오면서 생긴 문제를 푼 것이다.
    ///   강호무학에 성향을 박아 두면 **시작 무공 하나로 성향이 확정**되어,
    ///   정의서 §5-1 이 강호무학을 *"무소속 낭인의 무학. 시작점이자 최후의 보루"* 라고 한 것과 어긋난다.
    ///
    /// 그래서 강호무학은 성향을 갖지 않고 **익힌 사람의 성향을 따라 자란다.**
    /// </summary>
    public class LearnedArtTests
    {
        /// <summary>강호무학 표본 — 성향이 없다(`alignment: null`).</summary>
        private static MartialArt WandererArt()
        {
            return MartialArt.Technique("w_test", "절정검법", Discipline.Sword, null, basePower: 10, qiCost: 5);
        }

        /// <summary>문파 무공 표본 — 성향이 박혀 있다.</summary>
        private static MartialArt SchoolArt()
        {
            return MartialArt.Technique(
                "s_test", "정화참탈", Discipline.Sword, Alignment.Orthodox, basePower: 10, qiCost: 5,
                school: "남궁세가");
        }

        [Test]
        public void 강호무학은_익힌_사람의_성향을_따른다()
        {
            MartialArt art = WandererArt();

            var byOrthodox = new LearnedArt(art, 100, Alignment.Orthodox);
            var byDemonic = new LearnedArt(art, 100, Alignment.Demonic);

            Assert.AreEqual(Alignment.Orthodox, byOrthodox.EffectiveAlignment);
            Assert.AreEqual(Alignment.Demonic, byDemonic.EffectiveAlignment);

            // 같은 무공·같은 수련 횟수인데 성향 곡선이 달라 배율이 갈린다.
            // 수련 100회 시점에는 정파가 마도보다 앞선다(마도는 가장 늦게 가장 세진다).
            Assert.Greater(byOrthodox.PowerMultiplier, byDemonic.PowerMultiplier,
                "성향이 갈렸는데 배율이 같다 — 유효 성향이 반영되지 않았다.");
        }

        [Test]
        public void 성향이_박힌_무공은_익힌_사람을_무시한다()
        {
            MartialArt art = SchoolArt();

            // 마도 무인이 정파 무공을 들고 있어도 그 무공은 정파 곡선으로 자란다.
            // ⚠ "마도 무인이 정파 무공을 배울 수 있는가" 는 별개 문제이며 성향 배타 규칙이 다룬다.
            var learned = new LearnedArt(art, 100, Alignment.Demonic);

            Assert.AreEqual(Alignment.Orthodox, learned.EffectiveAlignment,
                "무공에 박힌 성향이 익힌 사람에게 덮였다.");
        }

        [Test]
        public void 강호무학에_사람_성향을_주지_않으면_예외다()
        {
            // 조용히 기본값으로 넘어가면 "왜 이 무공이 정파로 자라지" 를 나중에 추적하게 된다.
            // 성향이 없는 무공은 반드시 익힌 사람을 알아야 한다.
            Assert.Throws<ArgumentException>(() => new LearnedArt(WandererArt(), 100));
        }

        [Test]
        public void 수련_횟수는_무공마다_따로_쌓인다()
        {
            // ⚠⚠ 경지(1성~10성) 설계의 근거다. 무공별 수련 횟수가 이미 따로 관리되므로
            //   **새 배율을 곱하지 않고도** "가장 숙달된 무공이 가장 강하다" 가 성립한다.
            //   여기에 경지 배율을 별도로 곱하면 곱셈이 셋이 되어, 유형 숙달을 위력에서 뺐던
            //   2026-07-28 의 실패(성향과 곱해져 후반을 독식)를 반복하게 된다.
            MartialArt art = SchoolArt();

            var deep = new LearnedArt(art, 200);
            var shallow = new LearnedArt(art, 20);

            Assert.Greater(deep.Proficiency, shallow.Proficiency);
            Assert.Greater(deep.PowerMultiplier, shallow.PowerMultiplier,
                "같은 무공이라도 더 수련한 쪽이 강해야 한다.");
        }
    }
}
