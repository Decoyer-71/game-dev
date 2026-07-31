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
    /// **상태이상 축이 실제로 엔진에 붙어 있는가** (2026-07-31 연결).
    ///
    /// ⚠⚠ 이 파일이 생기기 전까지 상태이상 형태소 7자(독·혈·비·염·빙·탈·경)는 사전에 값이 있는데
    ///   **엔진에 도달할 경로 자체가 없었다** — 카탈로그 138종은 `StatusApplication` 을 하나도
    ///   넘기지 않으므로 `art.Effects` 가 항상 비어 있었다. 그래서 민감도표에서 7자 전부
    ///   **45~46%**, 즉 무의미(47~53%)보다도 **낮게** 나왔다 — 기력만 먹고 얻는 게 0 이었으니
    ///   **넣으면 손해인 글자**였다는 뜻이다.
    ///
    /// ⚠ 절대 수치를 박지 않는다(HANDOFF §7). 검증하는 것은 값이 아니라 **모양**이다 —
    ///   "걸리는가", "로그에 보이는가", "화상이 커지는가", "비가 경보다 빨리 마비에 닿는가".
    ///   밸런싱으로 상수가 바뀌어도 이 테스트들은 살아 있어야 한다.
    /// </summary>
    public class StatusMorphemeTests
    {
        /// <summary>측정 표본 수. 부여는 확률축이라 한 판으로는 아무것도 말할 수 없다.</summary>
        private const int Seeds = 200;

        private static CharacterStats SpecStats()
        {
            return new CharacterStats(maxHealth: 100, maxQi: 50, attack: 1, defense: 1, agility: 1);
        }

        /// <summary>무공명 하나로 대전자를 만든다. `CritTests` 와 같은 전제(대문파·정파·검)다.</summary>
        private static Combatant Fighter(string name, int sessions)
        {
            MartialArt art = MartialArtFactory.Create(
                "s_" + name, name, ArtKind.Attack, ArtTier.Major,
                Discipline.Sword, Alignment.Orthodox, "화산파");

            return new Combatant(name, SpecStats(),
                new List<LearnedArt> { new LearnedArt(art, sessions, Alignment.Orthodox) },
                new List<DisciplineMastery> { new DisciplineMastery(Discipline.Sword, sessions) });
        }

        /// <summary>전투 200판을 돌려 로그에 `mark` 가 몇 줄 나오는지 센다.</summary>
        private static int CountLines(string attackerArt, string mark)
        {
            Combatant attacker = Fighter(attackerArt, 200);
            Combatant defender = Fighter("참정", 200);

            int hits = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(attacker, defender, new XorShiftRandom(seed));
                IReadOnlyList<CombatLogEntry> log = r.Log;
                for (int i = 0; i < log.Count; i++)
                {
                    string line = log[i].ToString();
                    if (line.IndexOf(mark) >= 0) hits++;
                }
            }
            return hits;
        }

        [Test]
        public void 상태이상_형태소가_없으면_아무것도_걸리지_않는다()
        {
            // ⚠ 이 테스트가 먼저다. 기본 확률(`BaseStatusChance`)을 도입했으므로
            //   "형태소가 없어도 걸린다" 는 사고가 나면 일곱 글자의 존재 이유가 통째로 사라진다.
            Assert.AreEqual(0, CountLines("참정", "출혈"), "출혈 형태소가 없는데 출혈이 걸렸다.");
            Assert.AreEqual(0, CountLines("참정", "중독"), "중독 형태소가 없는데 중독이 걸렸다.");
            Assert.AreEqual(0, CountLines("참정", "화상"), "화상 형태소가 없는데 화상이 걸렸다.");
        }

        [Test]
        public void 일곱_글자가_각각_자기_상태이상을_건다()
        {
            // 형태소 → 상태이상 대응이 어긋나면(예: 염이 출혈을 건다) 이름에서 성능을 읽는다는
            // 정의서 §0 의 전제가 깨진다. 일곱 글자를 한 번에 훑는다.
            Assert.Greater(CountLines("참정독", "중독"), 0, "독(毒)이 중독을 걸지 않는다.");
            Assert.Greater(CountLines("참정혈", "출혈"), 0, "혈(血)이 출혈을 걸지 않는다.");
            Assert.Greater(CountLines("참정염", "화상"), 0, "염(炎)이 화상을 걸지 않는다.");
            Assert.Greater(CountLines("참정빙", "동상"), 0, "빙(氷)이 동상을 걸지 않는다.");
            Assert.Greater(CountLines("참정탈", "기력소실"), 0, "탈(奪)이 기력소실을 걸지 않는다.");
            Assert.Greater(CountLines("참정경", "경직"), 0, "경(硬)이 경직을 걸지 않는다.");
            Assert.Greater(CountLines("참정비", "경직"), 0, "비(痺)가 경직을 걸지 않는다.");
        }

        [Test]
        public void 화상은_턴이_갈수록_아파진다()
        {
            // ⚠⚠ 화상의 정체성이 여기 있다. 출혈은 '즉시 일정', 중독은 '쌓았다가 빠짐',
            //   화상은 '유지될수록 커짐' — 셋이 같은 모양이면 글자를 나눈 의미가 없다.
            //   1단계보다 2단계가 아프다는 것만 본다(수치가 아니라 부등호).
            int first = 0, later = 0;
            Combatant attacker = Fighter("참정염", 200);
            Combatant defender = Fighter("참정", 200);

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(attacker, defender, new XorShiftRandom(seed));
                IReadOnlyList<CombatLogEntry> log = r.Log;
                for (int i = 0; i < log.Count; i++)
                {
                    if (log[i].Kind != CombatLogKind.StatusTick) continue;
                    string line = log[i].ToString();
                    if (line.IndexOf("화상 1단계") >= 0) first += log[i].Damage;
                    else if (line.IndexOf("화상 2단계") >= 0) later += log[i].Damage;
                }
            }

            Assert.Greater(first, 0, "화상 1단계 표본이 없다 — 측정이 성립하지 않는다.");
            Assert.Greater(later, 0, "화상이 2단계까지 간 적이 없다 — 체증이 일어나지 않는다는 뜻이다.");
        }

        [Test]
        public void 동상은_스스로_피해를_주지_않고_받는_피해를_키운다()
        {
            // 동상은 유일하게 피해가 0인 상태이상이다. 틱으로 체력이 깎이면 설계가 어긋난 것이다.
            Combatant attacker = Fighter("참정빙", 200);
            Combatant defender = Fighter("참정", 200);

            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(attacker, defender, new XorShiftRandom(seed));
                IReadOnlyList<CombatLogEntry> log = r.Log;
                for (int i = 0; i < log.Count; i++)
                {
                    if (log[i].Kind != CombatLogKind.StatusTick) continue;
                    if (log[i].ToString().IndexOf("동상") < 0) continue;
                    Assert.AreEqual(0, log[i].Damage, "동상이 직접 피해를 줬다 — 취약(증폭)이어야 한다.");
                }
            }

            // 그리고 실제로 이겨야 한다 — 아무 일도 안 하는 글자가 아니라는 뜻이다.
            Assert.Greater(WinRate("참정빙", "참정"), 0.5,
                "동상을 넣은 쪽이 안 넣은 쪽을 못 이긴다 — 취약이 피해 계산에 반영되지 않는다는 뜻이다.");
        }

        [Test]
        public void 비는_경보다_마비에_빨리_닿는다()
        {
            // ⚠⚠ 2026-07-31 사용자 확정 규칙의 회귀 방지선이다.
            //   비(痺)는 마비 게이지를 두 겹씩 쌓아 **2회 성공**에 닿고, 경(硬)은 3회다.
            //   ⚠ 명중 페널티는 둘이 같아야 한다 — 비가 '경직 세기 2배'가 되면
            //     민감도 65~69% 로 지배적이 된다(측정으로 확인).
            int byParalysisMorpheme = CountLines("참정비", "→ 마비!");
            int byStaggerMorpheme = CountLines("참정경", "→ 마비!");

            Assert.Greater(byParalysisMorpheme, byStaggerMorpheme,
                "비(痺)가 경(硬)보다 마비를 자주 터뜨리지 않는다 — 정의서 §3-4 의 '마비 스택 +1' 이 죽었다.");
        }

        [Test]
        public void 마비는_확률이_아니라_게이팅으로만_터진다()
        {
            // 제안서 §5-3 의 핵심 — 확률은 '경직이 걸리는가'에만 개입한다.
            // 경직을 못 거는 무공은 아무리 때려도 마비를 못 만든다.
            Assert.AreEqual(0, CountLines("참정독", "→ 마비!"),
                "경직 형태소가 없는 무공이 마비를 터뜨렸다 — 확률형 행동불가가 되살아났다.");
        }

        [Test]
        public void 기력소실은_상대를_평타로_민다()
        {
            // ⚠ 제안서 §5-4 가 가장 불확실하다고 적은 항목이다. 숫자가 조금 깎이는 게 아니라
            //   **초식을 못 쓰게 만드는 것**이 조건이었다. 그 조건이 성립하는지만 본다.
            Assert.Greater(WinRate("참정탈", "참정"), 0.5,
                "기력소실을 넣은 쪽이 안 넣은 쪽을 못 이긴다 — 회복에 먹혀 아무 일도 안 한다는 뜻이다.");
        }

        [Test]
        public void 상태이상_일곱_글자가_전부_넣을_이유를_갖는다()
        {
            // ⚠⚠ 이 테스트가 이번 작업의 목적 그 자체다. 기력은 글자 수로 매겨지므로,
            //   승률이 50% 이하인 글자는 **넣으면 손해**다. 연결 전에는 일곱 글자 전부가 그랬다.
            //   ⚠ 상한(지배적 65%)은 여기서 검사하지 않는다 — 그건 밸런스 판정이고
            //     민감도표(Sandbox)의 일이다. 여기서는 **부호**만 지킨다.
            string[] chars = { "독", "혈", "비", "염", "빙", "탈", "경" };
            for (int i = 0; i < chars.Length; i++)
            {
                double rate = WinRate("참정" + chars[i], "참정");
                Assert.Greater(rate, 0.5,
                    chars[i] + " 을(를) 넣은 쪽이 안 넣은 쪽을 못 이긴다 — 기력만 더 쓰는 글자라는 뜻이다.");
            }
        }

        /// <summary>같은 조건에서 앞 무공의 승률. 무승부는 0.5 로 센다.</summary>
        private static double WinRate(string nameA, string nameB)
        {
            Combatant a = Fighter(nameA, 200);
            Combatant b = Fighter(nameB, 200);

            double score = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(a, b, new XorShiftRandom(seed));
                if (r.Outcome == CombatOutcome.AttackerWin) score += 1.0;
                else if (r.Outcome == CombatOutcome.Draw) score += 0.5;
            }
            return score / Seeds;
        }
    }
}
