using System.Collections.Generic;
using Jianghu.Core.Characters;
using Jianghu.Core.Combat;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;
using Jianghu.Core.Rng;
using NUnit.Framework;

namespace Jianghu.Tests.Combat
{
    /// <summary>
    /// **여러 공격 무공을 배웠을 때 무엇을 고르는가** (2026-08-05 신설).
    ///
    /// ⚠⚠ **이 파일이 생기기 전까지 `SelectArt` 는 통째로 고장 나 있었다.**
    ///   점수식이 `BasePower × PowerMultiplier × HitCount` 인데 형태소 무공은
    ///   `BasePower` 가 **0** 이라(`MartialArt.FromMorphemes` 하드코딩) 카탈로그 138종의
    ///   점수가 전부 0 이었고, 비교가 엄격 부등호라 **맨 처음 것만 통과**했다.
    ///   → 기력이 충분한 한 **맨 처음 배운 공격 무공만 평생** 썼다.
    ///
    /// ⚠⚠ **왜 여태 안 보였나 — 표본이 가렸다.** Sandbox 와 다른 모든 테스트가 공격 무공을
    ///   **1개씩만** 준다. 후보가 하나면 순서 의존이 드러날 수 없다.
    ///   → 🆕 **일반화: "축을 측정한다" 와 "그 축이 일할 조건을 만든다" 는 다른 일이다.**
    ///     후보가 하나뿐인 표본으로는 선택 로직을 영원히 검증하지 못한다.
    ///
    /// ⚠ 절대 수치를 박지 않는다. 검사하는 것은 **모양**이다 — "순서와 무관한가",
    ///   "더 센 것을 고르는가". 밸런싱으로 상수가 바뀌어도 살아 있어야 한다.
    /// </summary>
    public class SelectArtTests
    {
        /// <summary>⚠ 무공 경지의 최종점. 축 검증은 10성에서 한다.</summary>
        private const int Stage = MartialStage.MaxStage;

        private static LearnedArt Morpheme(string name)
        {
            MartialArt art = MartialArtFactory.Create(
                "s_" + name, name, ArtKind.Attack, ArtTier.Major,
                Discipline.Sword, Alignment.Orthodox, "화산파");
            return Learn(art);
        }

        private static LearnedArt Learn(MartialArt art)
        {
            int sessions = AlignmentCurve.SessionsToReach(
                Alignment.Orthodox, MartialStage.ProficiencyForStage(Stage));
            return new LearnedArt(art, sessions, Alignment.Orthodox);
        }

        private static Combatant Fighter(string name, params LearnedArt[] arts)
        {
            var masteries = new List<DisciplineMastery>();
            for (int i = 0; i < arts.Length; i++)
            {
                masteries.Add(new DisciplineMastery(
                    arts[i].Art.Discipline, DisciplineCurve.SessionsToMaster(arts[i].Art.Discipline)));
            }
            return new Combatant(name, CharacterStats.MaxLevel(), new List<LearnedArt>(arts), masteries);
        }

        /// <summary>전투 로그에서 `표본` 이 **처음 낸 초식의 이름**을 읽는다.</summary>
        private static string FirstArtUsed(Combatant sample, Combatant opponent)
        {
            CombatResult r = CombatResolver.Resolve(sample, opponent, new XorShiftRandom(7));
            IReadOnlyList<CombatLogEntry> log = r.Log;
            for (int i = 0; i < log.Count; i++)
            {
                string s = log[i].ToString();
                int who = s.IndexOf("표본의 ");
                if (who < 0) continue;
                int arrow = s.IndexOf('→', who);
                if (arrow < 0) continue;

                int start = who + "표본의 ".Length;
                return s.Substring(start, arrow - start).Trim();
            }
            return "(없음)";
        }

        /// <summary>
        /// **배운 순서를 바꿔도 같은 무공이 나가야 한다.**
        /// ⚠ 이것이 원래 결함의 직접 재현이다 — 고치기 전에는 두 값이 서로 달랐다.
        /// </summary>
        [Test]
        public void 공격무공_선택은_배운_순서에_좌우되지_않는다()
        {
            // ⚠ 공격 합이 실제로 다른 둘을 고른다. `참정`(2.75)과 `참정독명`(2.75)은 **같아서** 못 쓴다 —
            //   독·명은 공격을 주지 않기 때문이다. 화(火)가 공격 +0.6 을 주므로 그것으로 가른다.
            LearnedArt weak = Morpheme("참정");          // 공격 2.75
            LearnedArt strong = Morpheme("참정화");      // 공격 3.35
            Assert.Greater(strong.Art.Delta.Attack, weak.Art.Delta.Attack, "표본 설계가 틀렸다.");

            Combatant dummy = Fighter("상대", Morpheme("참정"));

            string weakFirst = FirstArtUsed(Fighter("표본", weak, strong), dummy);
            string strongFirst = FirstArtUsed(Fighter("표본", strong, weak), dummy);

            Assert.AreEqual(strongFirst, weakFirst,
                "배운 순서에 따라 다른 무공이 나간다 — SelectArt 가 순서에 의존한다.");
            Assert.AreEqual("참정화", weakFirst, "더 센 무공을 고르지 않았다.");
        }

        /// <summary>
        /// **레거시 무공이 섞여도 실제로 센 쪽이 나가야 한다.**
        ///
        /// ⚠⚠ 결함의 **두 번째 얼굴**이다. 레거시는 `BasePower > 0` 이라 형태소(0)와 섞이면
        ///   순서와 무관하게 **레거시가 항상** 이겼다. 지금 카탈로그엔 레거시가 없어 발현되지
        ///   않았을 뿐이고, `MartialArt.Technique()` 는 여전히 공개 API 다.
        /// ⚠ 레거시 `BasePower` 는 형태소 공격 합과 **같은 자리에 들어가지만 스케일이 다르다**
        ///   (22~28 대 최대 5). 그래서 레거시가 이기는 것 **자체는 정상**이다 — 검사하는 것은
        ///   *"순서가 아니라 세기로 갈리는가"* 다.
        /// </summary>
        [Test]
        public void 레거시가_섞여도_순서가_아니라_세기로_갈린다()
        {
            LearnedArt morpheme = Morpheme("참정화");
            LearnedArt legacy = Learn(MartialArt.Technique(
                "legacy_weak", "약한레거시", Discipline.Sword,
                basePower: 1, qiCost: 3, school: "화산파", alignment: Alignment.Orthodox));

            Assert.Less(legacy.Art.BasePower, morpheme.Art.Delta.Attack,
                "표본 설계가 틀렸다 — 레거시가 형태소보다 약해야 이 검사가 뜻을 갖는다.");

            Combatant dummy = Fighter("상대", Morpheme("참정"));

            string legacyFirst = FirstArtUsed(Fighter("표본", legacy, morpheme), dummy);
            string morphemeFirst = FirstArtUsed(Fighter("표본", morpheme, legacy), dummy);

            Assert.AreEqual(morphemeFirst, legacyFirst, "레거시/형태소 혼재에서 순서에 의존한다.");
            Assert.AreEqual("참정화", legacyFirst,
                "약한 레거시가 더 센 형태소 무공을 밀어냈다 — BasePower 가 그대로 이기고 있다.");
        }
    }
}
