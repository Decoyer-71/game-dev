using System.Collections.Generic;
using Jianghu.Core.Characters;
using Jianghu.Core.Combat;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;
using NUnit.Framework;

namespace Jianghu.Tests.Combat
{
    /// <summary>
    /// **내공 형태소 4자(양·음·합·식)가 이름대로 작동하는가.**
    ///
    /// ⚠⚠ 이 파일이 존재하는 이유 — 2026-08-01 까지 이 넷은 **한 번도 측정된 적이 없었고**,
    ///   재 보니 넷 다 정확히 무효였다(HANDOFF §4-2-P). 원인은 밸런스가 아니라 **엔진 결함 2건**이었다:
    ///
    ///   ⓐ <see cref="Combatant.EffectiveMaxQi"/> 만 유형 필터를 갖고 있어, 공격 무공에 넣은
    ///      최대기력 형태소가 아무 일도 안 했다(양 완전 무효 · 합 절반 · 식의 최대기력분 무효)
    ///   ⓑ 기력소모율이 **그 무공 자신의 소모에만** 곱해져서, 식(息)이 **자기 유일한 필수 자리인
    ///      내공 무공에서** 무효였다(내공 무공은 시전되지 않는다)
    ///
    ///   둘 다 2026-08-02 에 고쳤다(§4-2-V). **이 테스트가 없으면 같은 결함이 조용히 돌아온다** —
    ///   저장소 교훈 *"측정하지 않는 축은 고장 나도 보이지 않는다"*(`Combatant.Evasion` 주석).
    ///
    /// ⚠ 여기서는 **승률이 아니라 스탯 경로**를 본다. 승률은 기력 압력이 없으면 0 이 나오므로
    ///   (평타 전락률 0.0%) 결함 유무를 판정할 수 없다 — 그 구분이 §4-2-P 의 핵심이었다.
    /// </summary>
    public class InnerArtMorphemeTests
    {
        private const int Stage = 10;

        private static MartialArt Build(string name, ArtKind kind, Discipline discipline)
        {
            return MartialArtFactory.Create(
                "t_" + name, name, kind, ArtTier.Major, discipline, Alignment.Orthodox, "화산파");
        }

        /// <summary>공격 무공 하나 + (선택) 내공 무공 하나를 든 만렙 검객.</summary>
        private static Combatant Fighter(string attackName, string innerName)
        {
            int sessions = AlignmentCurve.SessionsToReach(
                Alignment.Orthodox, MartialStage.ProficiencyForStage(Stage));

            var arts = new List<LearnedArt>
            {
                new LearnedArt(Build(attackName, ArtKind.Attack, Discipline.Sword), sessions, Alignment.Orthodox),
            };
            if (innerName != null)
            {
                arts.Add(new LearnedArt(
                    Build(innerName, ArtKind.Internal, Discipline.InnerArt), sessions, Alignment.Orthodox));
            }

            var masteries = new List<DisciplineMastery>
            {
                new DisciplineMastery(Discipline.Sword, DisciplineCurve.SessionsToMaster(Discipline.Sword)),
            };
            return new Combatant("t", CharacterStats.MaxLevel(), arts, masteries);
        }

        // ─────────────────────── 결함 ⓐ — 최대기력은 어느 무공에 있든 붙는다 ───────────────────────

        [Test]
        public void 최대기력_형태소는_공격_무공에_넣어도_작동한다()
        {
            int plain = Fighter("참정", null).EffectiveMaxQi;
            int withYang = Fighter("참양정", null).EffectiveMaxQi;

            Assert.Greater(withYang, plain,
                "양(陽 최대기력+10)을 공격 무공에 넣었는데 최대기력이 그대로다. "
                + "EffectiveMaxQi 에 유형 필터가 되살아났는지 확인할 것(§4-2-V).");
        }

        [Test]
        public void 최대기력_형태소는_공격_무공이든_내공_무공이든_같은_값을_준다()
        {
            int inAttack = Fighter("참양정", null).EffectiveMaxQi;
            int inInner = Fighter("참정", "양공").EffectiveMaxQi;

            Assert.AreEqual(inInner, inAttack,
                "같은 글자인데 어디 쓰였느냐로 최대기력이 달라진다. "
                + "이름이 성능을 거짓말하는 상태다(§5-C).");
        }

        [Test]
        public void 최대기력과_회복은_같은_규칙을_따른다()
        {
            // 음(陰 회복+2)은 결함 시절에도 공격 무공에서 작동했다. 양(陽)이 그와 어긋나 있던 것이
            // 결함의 정체이므로, 둘이 같은 규칙을 따르는지가 회귀의 핵심이다.
            Combatant yangAttack = Fighter("참양정", null);
            Combatant yangInner = Fighter("참정", "양공");
            Combatant eumAttack = Fighter("참음정", null);
            Combatant eumInner = Fighter("참정", "음공");

            Assert.AreEqual(yangInner.EffectiveMaxQi, yangAttack.EffectiveMaxQi, "양(최대기력)이 위치를 탄다.");
            Assert.AreEqual(eumInner.QiRegenPerTurn, eumAttack.QiRegenPerTurn, "음(회복)이 위치를 탄다.");
        }

        // ─────────────────────── 결함 ⓑ — 식(息)은 자기 필수 자리에서 일해야 한다 ───────────────────────

        [Test]
        public void 식은_내공_무공에_있을_때_모든_초식의_소모를_깎는다()
        {
            Combatant plain = Fighter("참정", null);
            Combatant withSik = Fighter("참정", "식공");

            Assert.AreEqual(0, plain.SupportQiCostPercent,
                "내공 무공이 없는데 보조 소모율이 0 이 아니다.");
            Assert.Less(withSik.SupportQiCostPercent, 0,
                "식(息 −10%)을 내공 무공으로 배웠는데 소모율이 안 깎인다. "
                + "식의 유일한 필수 자리가 내공 무공이므로, 여기서 무효면 식은 어디서도 일하지 않는다.");
        }

        [Test]
        public void 공격_무공의_소모율은_사람에게_붙지_않는다()
        {
            // 범위 만(萬 +200%)은 *이 초식이* 전원을 때리는 대가다. 사람에게 붙으면 뜻이 무너진다.
            // ⚠ 조합 규칙상 범위는 공격 무공 전용이라 애초에 보조 무공에 들어갈 수도 없다.
            Combatant withMan = Fighter("참정만", null);

            Assert.AreEqual(0, withMan.SupportQiCostPercent,
                "공격 무공의 소모율이 사람에게 붙었다. 만(萬)은 그 초식의 값이지 사람의 값이 아니다.");
        }

        [Test]
        public void 보조_소모율은_숙련_배율을_탄다()
        {
            // 최대기력·회복과 같은 처리다(설계안 §1-C). 상수로 두면 수련할수록 상대가치가 무너진다.
            int novice = AlignmentCurve.SessionsToReach(
                Alignment.Orthodox, MartialStage.ProficiencyForStage(1));
            int master = AlignmentCurve.SessionsToReach(
                Alignment.Orthodox, MartialStage.ProficiencyForStage(Stage));

            var masteries = new List<DisciplineMastery>
            {
                new DisciplineMastery(Discipline.Sword, DisciplineCurve.SessionsToMaster(Discipline.Sword)),
            };

            Combatant low = new Combatant("t", CharacterStats.MaxLevel(),
                new List<LearnedArt>
                {
                    new LearnedArt(Build("참정", ArtKind.Attack, Discipline.Sword), novice, Alignment.Orthodox),
                    new LearnedArt(Build("식공", ArtKind.Internal, Discipline.InnerArt), novice, Alignment.Orthodox),
                }, masteries);

            Combatant high = new Combatant("t", CharacterStats.MaxLevel(),
                new List<LearnedArt>
                {
                    new LearnedArt(Build("참정", ArtKind.Attack, Discipline.Sword), master, Alignment.Orthodox),
                    new LearnedArt(Build("식공", ArtKind.Internal, Discipline.InnerArt), master, Alignment.Orthodox),
                }, masteries);

            Assert.Less(high.SupportQiCostPercent, low.SupportQiCostPercent,
                "무공을 더 익혔는데 기력 효율이 나아지지 않는다.");
        }
    }
}
