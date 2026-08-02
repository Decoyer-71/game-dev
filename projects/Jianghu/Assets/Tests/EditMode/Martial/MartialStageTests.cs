using Jianghu.Core.Martial;
using NUnit.Framework;

namespace Jianghu.Tests.Martial
{
    /// <summary>
    /// **무공 경지 1~10성** (2026-07-31 신설).
    ///
    /// ⚠⚠ 이 개념이 없던 동안 측정도 문서도 *"수련 200회"* 처럼 **횟수**로 말하고 있었다.
    ///   횟수는 축마다 뜻이 다르다 — 같은 200회가 정파에게는 10성이고 마도에게는 9성,
    ///   검 숙달로는 8성이다. 그래서 *"200회 = 만렙"* 이라는 잘못된 전제가 오래 살아 있었다.
    ///
    /// 이 파일이 지키는 것은 **경지와 수련 횟수의 대응**이다 —
    /// *"지금까지 재 온 상한 도달 횟수가 곧 10성"* (사용자 확정).
    /// </summary>
    public class MartialStageTests
    {
        [Test]
        public void 숙련_상한이_10성이다()
        {
            Assert.AreEqual(MartialStage.MaxStage, MartialStage.StageOf(AlignmentCurve.HardCap),
                "숙련 100 이 10성이 아니다 — 경지와 숙련 상한의 대응이 어긋났다.");
            Assert.AreEqual(AlignmentCurve.HardCap, MartialStage.ProficiencyForStage(MartialStage.MaxStage));
        }

        [Test]
        public void 익히기만_해도_1성이다()
        {
            // ⚠ 0성은 없다. 무공을 배웠다는 것 자체가 1성이다.
            Assert.AreEqual(1, MartialStage.StageOf(0));
            Assert.AreEqual(1, MartialStage.StageOf(1));
        }

        [Test]
        public void 경지는_숙련도를_열등분한_것이다()
        {
            // 값이 아니라 **모양**을 본다 — 경지가 오르면 숙련도도 오르고, 역함수가 서로 맞는다.
            for (int stage = 1; stage <= MartialStage.MaxStage; stage++)
            {
                int proficiency = MartialStage.ProficiencyForStage(stage);
                Assert.AreEqual(stage, MartialStage.StageOf(proficiency),
                    "{0}성에 해당하는 숙련 {1} 을 다시 경지로 바꾸면 {0}성이 나와야 한다", stage, proficiency);

                if (stage > 1)
                {
                    Assert.Greater(proficiency, MartialStage.ProficiencyForStage(stage - 1),
                        "경지가 올랐는데 필요 숙련도가 늘지 않는다.");
                }
            }
        }

        [Test]
        public void 유형마다_10성에_닿는_수련_횟수가_다르다()
        {
            // ⚠⚠ 사용자가 든 예시 그대로다 — 창은 50회, 검은 250회에 10성이다.
            //   **경지로 말하면 같은 지점**이고, 다른 것은 거기 도달하는 비용뿐이다.
            int spear = DisciplineCurve.SessionsToMaster(Discipline.Spear);
            int sword = DisciplineCurve.SessionsToMaster(Discipline.Sword);

            Assert.Less(spear, sword, "백일창이 만일검보다 오래 걸린다 — 유형 정체성이 뒤집혔다.");

            Assert.AreEqual(MartialStage.MaxStage,
                MartialStage.StageOf(DisciplineCurve.ProficiencyFor(Discipline.Spear, spear)),
                "창의 상한 도달 시점이 10성이 아니다.");
            Assert.AreEqual(MartialStage.MaxStage,
                MartialStage.StageOf(DisciplineCurve.ProficiencyFor(Discipline.Sword, sword)),
                "검의 상한 도달 시점이 10성이 아니다.");
        }

        [Test]
        public void 성향마다_10성에_닿는_수련_횟수가_다르다()
        {
            foreach (Alignment alignment in new[] { Alignment.Orthodox, Alignment.Unorthodox, Alignment.Demonic })
            {
                int sessions = AlignmentCurve.SessionsToReach(
                    alignment, MartialStage.ProficiencyForStage(MartialStage.MaxStage));

                Assert.AreEqual(MartialStage.MaxStage,
                    MartialStage.StageOf(AlignmentCurve.ProficiencyFor(alignment, sessions)),
                    "{0} 의 역산 수련 횟수가 10성을 만들지 못한다.", alignment);
            }

            // 마도가 가장 오래 걸린다 — 학습률 0.45 로 가장 낮다(정체성).
            int demonic = AlignmentCurve.SessionsToReach(Alignment.Demonic, AlignmentCurve.HardCap);
            int orthodox = AlignmentCurve.SessionsToReach(Alignment.Orthodox, AlignmentCurve.HardCap);
            Assert.Greater(demonic, orthodox, "마도가 정파보다 빨리 10성에 닿는다 — 성향 정체성이 뒤집혔다.");
        }

        [Test]
        public void 역산은_실제로_그_경지를_만든다()
        {
            // ⚠ 측정 도구가 이 역함수에 통째로 의존한다. 어긋나면 민감도표 전체가 엉뚱한 지점을 잰다.
            foreach (int stage in MartialStage.MeasurementStages)
            {
                int need = MartialStage.ProficiencyForStage(stage);
                int sessions = AlignmentCurve.SessionsToReach(Alignment.Orthodox, need);

                Assert.GreaterOrEqual(AlignmentCurve.ProficiencyFor(Alignment.Orthodox, sessions), need,
                    "{0}성에 필요하다고 계산한 수련 횟수가 실제로는 모자란다.", stage);
            }
        }
    }
}
