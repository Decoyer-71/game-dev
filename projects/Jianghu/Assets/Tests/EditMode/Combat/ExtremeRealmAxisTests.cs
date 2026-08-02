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
    /// **극한경지 두 축이 엔진에 붙어 있는가** — 마(魔) 방어무시 · 성(聖) 상태이상저항 (2026-08-02 연결).
    ///
    /// ⚠⚠ 둘 다 **부분 손실** 상태였다. 마(魔)는 공격 +2.5 만 살고 방어무시 25 가 죽어 있었고,
    ///   성(聖)은 방어 +5 · 막기 +25 만 살고 저항 30 이 죽어 있었다. 절반만 일하니 *"글자가 아예
    ///   안 먹는다"* 는 형태로 드러나지 않아 오래 남았다 — 인계문서 §3-2 의 미연결 축 목록이 출처다.
    ///
    /// ⚠ 절대 수치를 박지 않는다(HANDOFF §7). 25·30 은 정의서에서 **재환산으로 여러 번 바뀐** 값이라
    ///   여기서 굳히면 다음 재환산 때 테스트가 먼저 깨져 조정을 막는다.
    /// </summary>
    public class ExtremeRealmAxisTests
    {
        private const int Seeds = 200;

        /// <summary>마(魔) — 방어무시를 갖는 유일한 글자. 천마신교 전승무학·검.</summary>
        private const string DefenseIgnoreArt = "마한중참";

        /// <summary>성(聖) — 상태이상저항을 갖는 유일한 글자. 소림사 전승무학·권.</summary>
        private const string ResistArt = "성뇌후격";

        /// <summary>극한경지를 갖되 위 두 축은 없는 대조군. 화산파 전승무학·검(존 尊).</summary>
        private const string ControlArt = "존풍쾌절";

        private static Combatant Fighter(
            string artName, string school, Discipline discipline, Alignment alignment,
            ArtTier tier = ArtTier.Legacy)
        {
            MartialArt art = MartialArtFactory.Create(
                "x_" + artName, artName, ArtKind.Attack, tier, discipline, alignment, school);

            int sessions = AlignmentCurve.SessionsToReach(
                alignment, MartialStage.ProficiencyForStage(MartialStage.MaxStage));

            return new Combatant(artName, CharacterStats.MaxLevel(),
                new List<LearnedArt> { new LearnedArt(art, sessions, alignment) },
                new List<DisciplineMastery>
                {
                    new DisciplineMastery(discipline, DisciplineCurve.SessionsToMaster(discipline)),
                });
        }

        private static Combatant Guarded()
        {
            // 방(防) — 방어 형태소. 방어무시가 일할 과녁이다.
            return Fighter("중참방", "점창파", Discipline.Sword, Alignment.Orthodox, ArtTier.Minor);
        }

        private static Combatant Plain()
        {
            return Fighter("중참독", "점창파", Discipline.Sword, Alignment.Orthodox, ArtTier.Minor);
        }

        /// <summary>
        /// **적중 한 방당 평균 피해.** 총합이 아니라 평균인 것이 요점이다.
        ///
        /// ⚠⚠ 처음에 총 피해로 쟀다가 틀렸다 — 총합은 **전투가 몇 턴 갔는지**에 좌우되고,
        ///   전투 길이는 성향 변동폭(마도 ±35% vs 정파 ±15%)·치명·반격에 따라 달라진다.
        ///   그래서 *"방어를 얼마나 뚫었는가"* 를 재려던 비(比)에 **전투 길이가 섞여 들어와**
        ///   부호가 뒤집혔다(마 0.9435 < 존 0.9827). 방어 경감은 **타격당 배율**이므로
        ///   타격당으로 재야 약분된다.
        /// </summary>
        private static double AverageDamagePerHit(Combatant attacker, Combatant defender)
        {
            long total = 0;
            int hits = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(attacker, defender, new XorShiftRandom(seed));
                IReadOnlyList<CombatLogEntry> log = r.Log;
                for (int i = 0; i < log.Count; i++)
                {
                    CombatLogEntry e = log[i];
                    if (e.Kind != CombatLogKind.Action) continue;
                    if (e.ActorName != attacker.Name) continue;
                    if (e.Damage <= 0) continue;                                        // 빗나감·회피는 뺀다
                    if (!string.IsNullOrEmpty(e.Note) && e.Note.IndexOf("[반격]") >= 0) continue;
                    total += e.Damage;
                    hits++;
                }
            }
            Assert.Greater(hits, 0, "한 번도 못 때렸다 — 표본이 없다.");
            return (double)total / hits;
        }

        /// <summary>`defender` 가 상태이상에 걸린 횟수.</summary>
        private static int StatusesLandedOn(Combatant attacker, Combatant defender)
        {
            int hits = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(attacker, defender, new XorShiftRandom(seed));
                IReadOnlyList<CombatLogEntry> log = r.Log;
                for (int i = 0; i < log.Count; i++)
                {
                    CombatLogEntry e = log[i];
                    if (e.Kind != CombatLogKind.Action) continue;
                    if (e.ActorName != attacker.Name) continue;
                    // ⚠ 로그 표기가 `[중독 1중첩]` 이라 닫는 대괄호까지 넣으면 영영 0 이 나온다.
                    if (!string.IsNullOrEmpty(e.Note) && e.Note.IndexOf("[중독") >= 0) hits++;
                }
            }
            return hits;
        }

        // ─────────────────────────── 데이터가 실려 있는가 ───────────────────────────

        [Test]
        public void 마는_방어무시를_성은_저항을_싣는다()
        {
            MartialArt ma = MartialArtFactory.Create(
                "x_마", DefenseIgnoreArt, ArtKind.Attack, ArtTier.Legacy,
                Discipline.Sword, Alignment.Demonic, "천마신교");
            MartialArt seong = MartialArtFactory.Create(
                "x_성", ResistArt, ArtKind.Attack, ArtTier.Legacy,
                Discipline.Fist, Alignment.Orthodox, "소림사");
            MartialArt control = MartialArtFactory.Create(
                "x_존", ControlArt, ArtKind.Attack, ArtTier.Legacy,
                Discipline.Sword, Alignment.Orthodox, "화산파");

            Assert.Greater(ma.Delta.DefenseIgnore, 0, "마(魔)에 방어무시가 없다.");
            Assert.Greater(seong.Delta.StatusResist, 0, "성(聖)에 상태이상저항이 없다.");
            Assert.AreEqual(0, control.Delta.DefenseIgnore, "대조군에 방어무시가 생겼다.");
            Assert.AreEqual(0, control.Delta.StatusResist, "대조군에 저항이 생겼다.");
        }

        // ─────────────────────────── 전투에서 일하는가 ───────────────────────────

        [Test]
        public void 마의_방어무시는_단단한_상대에게서_값을_한다()
        {
            // ⚠⚠ **비(比)로 본다.** 마(魔)는 공격 +2.5 도 같이 갖고 있어서 절대 피해를 비교하면
            //   방어무시가 일한 것인지 공격이 높은 것인지 갈리지 않는다.
            //   *"방어자에게 준 피해 ÷ 평범한 상대에게 준 피해"* 는 위력이 약분되므로
            //   **방어를 얼마나 뚫었는가만** 남는다.
            Combatant ma = Fighter(DefenseIgnoreArt, "천마신교", Discipline.Sword, Alignment.Demonic);
            Combatant control = Fighter(ControlArt, "화산파", Discipline.Sword, Alignment.Orthodox);

            double maRatio = AverageDamagePerHit(ma, Guarded()) / AverageDamagePerHit(ma, Plain());
            double controlRatio = AverageDamagePerHit(control, Guarded()) / AverageDamagePerHit(control, Plain());

            Assert.Greater(maRatio, controlRatio,
                "방어무시를 가진 마(魔)가 방어 형태소 상대에게 대조군보다 덜 막힌다는 성질이 없다.");
        }

        [Test]
        public void 성의_저항은_상태이상을_덜_걸리게_한다()
        {
            // 독(毒)을 가진 공격자가 성(聖) 보유자와 비보유자에게 각각 몇 번 중독을 걸었는가.
            Combatant poisoner = Fighter("중참독", "점창파", Discipline.Sword, Alignment.Orthodox, ArtTier.Minor);

            Combatant resistant = Fighter(ResistArt, "소림사", Discipline.Fist, Alignment.Orthodox);
            Combatant control = Fighter(ControlArt, "화산파", Discipline.Sword, Alignment.Orthodox);

            int onResistant = StatusesLandedOn(poisoner, resistant);
            int onControl = StatusesLandedOn(poisoner, control);

            Assert.Less(onResistant, onControl,
                "성(聖) 보유자가 상태이상을 그대로 맞는다 — 저항이 부여확률에 닿지 않았다.");
        }

        [Test]
        public void 방어무시는_100을_넘지_않는다()
        {
            // ⚠⚠ 이 테스트가 지키는 것은 수치가 아니라 **개념**이다.
            //   방어를 100% 무시하면 더 무시할 것이 없다. 도(刀) 숙달 100 에 마(魔) 25 를 더해
            //   125 가 되면 방어가 음수로 뒤집혀 *"피해 증폭"* 이라는 **다른 효과**가 된다.
            //   2026-07-31 에 방어관통 160% 를 제시했다가 사용자가 잡은 그 실패다(CLAUDE.md §5-C).
            //
            //   도(刀)로 마(魔) 무공을 만들 수 있는 조합이 카탈로그에 지금은 없지만,
            //   **플레이어의 독문무공 창시**(정의서 §0)가 그 조합을 만들 수 있다.
            Combatant blade = Fighter("마한중참", "천마신교", Discipline.Blade, Alignment.Demonic);

            double vsGuarded = AverageDamagePerHit(blade, Guarded());
            double vsPlain = AverageDamagePerHit(blade, Plain());

            // 방어를 완전히 무시하면 방어 형태소가 있으나 없으나 **같아야** 하고,
            // 100 을 넘어 음수 방어가 되면 방어자가 **더 아프게** 맞는다.
            Assert.LessOrEqual(vsGuarded, vsPlain,
                "방어 형태소를 가진 쪽이 오히려 더 맞았다 — 관통이 100 을 넘어 피해 증폭이 됐다.");
        }
    }
}
