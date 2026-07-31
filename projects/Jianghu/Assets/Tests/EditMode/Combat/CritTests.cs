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
    /// **치명 축이 실제로 엔진에 붙어 있는가** (2026-07-31 연결).
    ///
    /// ⚠⚠ 이 파일이 생기기 전까지 치명은 `ArtStatDelta` 에 값만 있고 엔진이 읽지 않았다.
    ///   그래서 치명 형태소(명·광·휘·야·암·한·뇌)가 민감도표에서 전부 **49% = 무영향**으로 나왔다.
    ///   기력은 글자 수로 매겨지므로, 축이 안 붙은 글자는 **넣으면 순수 손해**였다.
    ///
    /// ⚠ 절대 수치를 박지 않는다(HANDOFF §7). 검증하는 것은 값이 아니라 **모양**이다 —
    ///   "치명이 로그에 나타나는가", "밝다가 어둡다보다 자주 터지는가" 같은 순서·존재만 본다.
    ///   밸런싱으로 상수가 바뀌어도 이 테스트들은 살아 있어야 한다.
    /// </summary>
    public class CritTests
    {
        /// <summary>측정 표본 수. 치명은 확률축이라 한 판으로는 아무것도 말할 수 없다.</summary>
        private const int Seeds = 200;

        /// <summary>⚠ 무공 경지의 최종점. 축 검증은 **10성**에서 한다(2026-07-31 사용자 확정).</summary>
        private const int MasterySessions = MartialStage.MaxStage;

        /// <summary>
        /// ⚠ **만렙 기준으로 잰다** (2026-07-31 사용자 확정). 캐릭터 능력치가 나중에 성장 요소가
        ///   되므로 지금 맞추는 수치가 성장의 끝이어야 한다.
        /// </summary>
        private static CharacterStats SpecStats()
        {
            return CharacterStats.MaxLevel();
        }

        /// <summary>
        /// 무공명 하나로 대전자를 만든다. 대문파·정파·검으로 고정한다 —
        /// 비교 대상이 형태소 하나뿐이어야 하므로 나머지는 전부 같게 둔다(Sandbox 와 같은 전제).
        /// </summary>
        /// ⚠ 인자는 **무공 경지(1~10성)** 다. 유형 숙달은 만렙 고정 — 두 축을 뭉치지 않는다(2026-07-31).
        private static Combatant Fighter(string name, int stage)
        {
            MartialArt art = MartialArtFactory.Create(
                "t_" + name, name, ArtKind.Attack, ArtTier.Major,
                Discipline.Sword, Alignment.Orthodox, "화산파");

            int sessions = AlignmentCurve.SessionsToReach(
                Alignment.Orthodox, MartialStage.ProficiencyForStage(stage));

            return new Combatant(name, SpecStats(),
                new List<LearnedArt> { new LearnedArt(art, sessions, Alignment.Orthodox) },
                new List<DisciplineMastery>
                {
                    new DisciplineMastery(Discipline.Sword, DisciplineCurve.SessionsToMaster(Discipline.Sword)),
                });
        }

        /// <summary>
        /// `attacker` 가 터뜨린 치명 횟수를 센다.
        ///
        /// ⚠ 로그의 `[치명]` 표기를 읽는다. **표기가 곧 관찰 창구**라서 그렇다 —
        ///   치명이 로그에 안 보이면 플레이어도 못 보고, 그건 설계 §1 의 반증 조건 1 에 걸린다.
        ///   그러니 이 세는 방식 자체가 "보이는가" 를 함께 검사한다.
        /// </summary>
        private static int CountCrits(Combatant attacker, Combatant defender)
        {
            int crits = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(attacker, defender, new XorShiftRandom(seed));
                IReadOnlyList<CombatLogEntry> log = r.Log;
                for (int i = 0; i < log.Count; i++)
                {
                    CombatLogEntry e = log[i];
                    if (e.Kind != CombatLogKind.Action) continue;
                    if (e.ActorName != attacker.Name) continue;
                    if (string.IsNullOrEmpty(e.Note) || e.Note.IndexOf("[치명") < 0) continue;

                    // "[치명 ×3]" 처럼 한 행동에서 여러 번 터질 수 있다 — 타격당 판정이기 때문이다.
                    crits += ParseCritCount(e.Note);
                }
            }
            return crits;
        }

        /// <summary>`[치명]` = 1 · `[치명 ×3]` = 3.</summary>
        private static int ParseCritCount(string note)
        {
            int mark = note.IndexOf("[치명");
            int times = note.IndexOf('×', mark);
            if (times < 0) return 1;

            int end = note.IndexOf(']', times);
            int count;
            return int.TryParse(note.Substring(times + 1, end - times - 1), out count) ? count : 1;
        }

        [Test]
        public void 치명이_실제로_터지고_로그에_남는다()
        {
            // 치명 형태소가 **없는** 무공이다. 그래도 터져야 한다 —
            // 정의서 §1-1 은 치명률 10% 를 무공 속성이 아니라 **캐릭터 기본 능력치**로 적었다.
            int crits = CountCrits(Fighter("참정", MasterySessions), Fighter("참정", MasterySessions));

            Assert.Greater(crits, 0,
                "치명이 한 번도 안 터졌다 — 엔진이 치명 축을 아예 굴리지 않는다는 뜻이다.");
        }

        [Test]
        public void 밝다_형태소가_치명을_더_자주_터뜨린다()
        {
            // ⚠ 둘 다 3글자라 **기력 소모가 같다.** 글자 수가 다르면 평타 전락 빈도가 갈려
            //   행동 횟수 자체가 달라지고, 그러면 치명 횟수 비교가 오염된다.
            //   명(明) = 치명률 +10%p · 암(暗) = 치명배율 +0.3 — 확률축을 만지는 건 명뿐이다.
            int bright = CountCrits(Fighter("참정명", MasterySessions), Fighter("참정", MasterySessions));
            int dark = CountCrits(Fighter("참정암", MasterySessions), Fighter("참정", MasterySessions));

            Assert.Greater(bright, dark,
                "밝다(치명률 +10%p)가 어둡다(치명배율 +0.3)보다 자주 터지지 않는다 — " +
                "정의서 §1-2 의 '밝다=자주 터진다 / 어둡다=크게 터진다' 대비가 무너졌다.");
        }

        [Test]
        public void 어둡다_형태소는_치명_횟수가_아니라_한_방을_키운다()
        {
            // 같은 치명 횟수로 더 큰 피해를 낸다는 것이 치명배율의 정의다.
            // ⚠ 총 피해가 아니라 **치명 한 번당 피해**를 봐야 한다 — 총량은 명중·행동 횟수에 오염된다.
            Combatant plain = Fighter("참정", MasterySessions);
            Combatant dark = Fighter("참정암", MasterySessions);
            Combatant dummy = Fighter("참정", MasterySessions);

            double plainPerCrit = CritDamageRatio(plain, dummy);
            double darkPerCrit = CritDamageRatio(dark, dummy);

            Assert.Greater(darkPerCrit, plainPerCrit,
                "어둡다(치명배율 +0.3)를 넣었는데 치명 한 방이 커지지 않았다.");
        }

        /// <summary>
        /// **치명이 하나 터진 단타 행동**만 골라 평균 피해를 낸다.
        ///
        /// ⚠ 조건을 이렇게 좁히는 이유 — 치명 없는 타격이 섞이면 배율 효과가 희석되고,
        ///   다단 타격이 섞이면 몇 타가 치명이었는지 로그 한 줄로는 나눌 수 없다.
        /// </summary>
        private static double CritDamageRatio(Combatant attacker, Combatant defender)
        {
            long total = 0;
            int n = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(attacker, defender, new XorShiftRandom(seed));
                IReadOnlyList<CombatLogEntry> log = r.Log;
                for (int i = 0; i < log.Count; i++)
                {
                    CombatLogEntry e = log[i];
                    if (e.Kind != CombatLogKind.Action) continue;
                    if (e.ActorName != attacker.Name) continue;
                    if (e.LandedHits != 1) continue;
                    if (string.IsNullOrEmpty(e.Note) || e.Note.IndexOf("[치명") < 0) continue;
                    if (ParseCritCount(e.Note) != 1) continue;

                    total += e.Damage;
                    n++;
                }
            }

            Assert.Greater(n, 0, "치명 표본이 하나도 안 잡혔다 — 측정 자체가 성립하지 않는다.");
            return (double)total / n;
        }

        [Test]
        public void 치명배율은_수련으로_커지지_않는다()
        {
            // ⚠⚠ **이 프로젝트에서 가장 비싸게 배운 규칙의 회귀 방지선이다** (HANDOFF §5).
            //   유형 숙달을 위력에 곱했더니 후반을 독식해서 뺐다. 치명배율도 곱셈이므로
            //   숙련 배율이 얹히면 같은 함정이 그대로 재현된다.
            //
            //   수련 0 과 200 은 위력·명중이 다르니 총 피해로는 분리할 수 없다.
            //   그래서 **치명 한 방 / 평타 한 방의 비율**을 본다 — 배율이 순수 상수라면
            //   수련이 아무리 쌓여도 이 비율은 그대로여야 한다.
            double novice = CritToNormalRatio(1);                    // 1성 — 갓 익힌 무공
            double master = CritToNormalRatio(MartialStage.MaxStage); // 10성 — 경지의 최종점

            Assert.AreEqual(novice, master, 0.15,
                "수련에 따라 치명 배율이 달라진다 — 어딘가에서 숙련 배율이 치명에 곱해지고 있다. " +
                "곱셈 누적은 후반을 독식한다(HANDOFF §5).");
        }

        /// <summary>치명 한 방 피해 ÷ 치명 아닌 한 방 피해. 배율이 상수면 수련과 무관해야 한다.</summary>
        private static double CritToNormalRatio(int stage)
        {
            Combatant attacker = Fighter("참정암", stage);
            Combatant defender = Fighter("참정", stage);

            long critSum = 0, normalSum = 0;
            int critN = 0, normalN = 0;

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(attacker, defender, new XorShiftRandom(seed));
                IReadOnlyList<CombatLogEntry> log = r.Log;
                for (int i = 0; i < log.Count; i++)
                {
                    CombatLogEntry e = log[i];
                    if (e.Kind != CombatLogKind.Action) continue;
                    if (e.ActorName != attacker.Name) continue;
                    if (e.LandedHits != 1) continue;

                    bool isCrit = !string.IsNullOrEmpty(e.Note) && e.Note.IndexOf("[치명") >= 0;
                    if (isCrit && ParseCritCount(e.Note) == 1) { critSum += e.Damage; critN++; }
                    else if (!isCrit) { normalSum += e.Damage; normalN++; }
                }
            }

            Assert.Greater(critN, 0, "치명 표본이 없다.");
            Assert.Greater(normalN, 0, "평범한 타격 표본이 없다.");
            return ((double)critSum / critN) / ((double)normalSum / normalN);
        }
    }
}
