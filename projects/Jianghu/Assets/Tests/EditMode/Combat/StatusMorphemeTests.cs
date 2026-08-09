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
        /// **무공 경지 `stage`(1~10성)의 대전자**를 만든다. 대문파·정파·검 고정(Sandbox 와 같은 전제).
        ///
        /// ⚠⚠ 2026-07-31 — 그전에는 `sessions` 하나로 **무공 숙련과 유형 숙달을 동시에** 올렸다.
        ///   둘은 다른 축이라(무공 경지 vs 무기 숙달) 뭉치면 원인이 갈리지 않는다.
        ///   **유형 숙달은 만렙 고정**, 움직이는 것은 무공 경지뿐이다.
        /// </summary>
        private static Combatant Fighter(string name, int stage)
        {
            MartialArt art = MartialArtFactory.Create(
                "s_" + name, name, ArtKind.Attack, ArtTier.Major,
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

        /// <summary>전투 200판을 돌려 로그에 `mark` 가 몇 줄 나오는지 센다.</summary>
        private static int CountLines(string attackerArt, string mark)
        {
            Combatant attacker = Fighter(attackerArt, MasterySessions);
            Combatant defender = Fighter("참정", MasterySessions);

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
            Combatant attacker = Fighter("참정염", MasterySessions);
            Combatant defender = Fighter("참정", MasterySessions);

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
            Combatant attacker = Fighter("참정빙", MasterySessions);
            Combatant defender = Fighter("참정", MasterySessions);

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

        /// <summary>
        /// **비도(飛刀) 숙달은 상태이상을 더 세게 건다** (2026-08-05 신설 축).
        ///
        /// ⚠⚠ 이 축이 생긴 이유는 **확률 축이 포화**했기 때문이다 —
        ///   부여확률 `기본 30 + 형태소 10 + 비도숙달 50 = 90%` 에 상한이 100 이라
        ///   `DaggerMaxStatusChance` 를 올려도 남은 여지가 10%p 뿐이었다.
        ///   경위는 `DisciplineCurve.StatusPotencyPercent` 주석.
        ///
        /// ⚠ 절대 수치를 박지 않는다(위 클래스 주석) — 검사하는 것은 **부호와 방향**이다.
        ///   ⓐ 비도가 검보다 지속피해를 많이 낸다 ⓑ 상태이상이 없으면 차이가 없다.
        ///   ⓑ 가 핵심이다. 그게 없으면 *"비도가 그냥 세다"* 와 구분되지 않는다.
        /// </summary>
        [Test]
        public void 비도_숙달은_상태이상_세기를_키운다()
        {
            // 출혈(혈)을 가진 같은 무공을 검과 비도로 들린다. 다른 것은 유형뿐이다.
            int swordBleed = TotalStatusDamage("참정혈", Discipline.Sword);
            int daggerBleed = TotalStatusDamage("참정혈", Discipline.Dagger);

            Assert.Greater(daggerBleed, swordBleed,
                "비도가 검보다 지속피해를 더 내지 못한다 — 세기 보너스가 엔진에 닿지 않았다는 뜻이다.");

            // ⚠⚠ **상태이상이 없는 무공에서는 차이가 0 이어야 한다.** 이 행이 없으면
            //   위 결과가 "비도 숙달" 때문인지 "비도가 그냥 세다" 때문인지 갈리지 않는다.
            Assert.AreEqual(0, TotalStatusDamage("참정", Discipline.Dagger),
                "상태이상 형태소가 없는데 지속피해가 났다 — 측정 방식이 잘못됐다.");
        }

        /// <summary>
        /// **비도의 상태이상 세기는 무공 숙련도를 탄다** (2026-08-08 신설 · `DisciplineCurve` 주석).
        ///
        /// ⚠⚠ 2026-08-05 판은 `Scale(3, 유형 숙련도)` 라 **경지와 무관하게 항상 +3** 이었다.
        ///   그래서 비도만 후반에 시들었다 — 직접 피해는 `PowerMultiplier` 로 경지 배율을 타는데
        ///   상태이상은 그 곱셈 사슬 밖에서 절대량을 깎기 때문이다(대문파 유형 평균 3성 58.7 →
        ///   10성 47.0. 다른 넷은 −0.4 ~ +4.7). 구동자를 **무공 숙련도**로 갈아 끼운 것이 이 규칙이다.
        ///
        /// ⚠ **검을 대조군으로 함께 잰다.** 경지가 오르면 전투가 빨리 끝나 지속피해 총량 자체가
        ///   줄 수 있으므로, 절대값이 아니라 **비도−검 차이가 벌어지는가**를 본다.
        ///   검에는 이 보너스가 0 이라 차이는 곧 비도 숙달분이다.
        /// </summary>
        [Test]
        public void 비도의_상태이상_세기는_무공_숙련도를_탄다()
        {
            int early = TotalStatusDamage("참정혈", Discipline.Dagger, 3)
                        - TotalStatusDamage("참정혈", Discipline.Sword, 3);
            int late = TotalStatusDamage("참정혈", Discipline.Dagger, MartialStage.MaxStage)
                       - TotalStatusDamage("참정혈", Discipline.Sword, MartialStage.MaxStage);

            Assert.Greater(late, early,
                "10성에서 비도의 상태이상 우위가 3성보다 크지 않다 — 세기 보너스가 무공 숙련도를 "
                + "타지 않는다는 뜻이다(구동자가 유형 숙련도로 되돌아갔는지 확인할 것).");
        }

        /// <summary>
        /// 200판에서 지속피해(`피해 N` 이 아니라 상태이상 틱) 총합을 센다.
        /// ⚠ 유형만 바꾸고 나머지는 전부 고정한다.
        /// </summary>
        private static int TotalStatusDamage(string name, Discipline discipline)
        {
            return TotalStatusDamage(name, discipline, MasterySessions);
        }

        private static int TotalStatusDamage(string name, Discipline discipline, int stage)
        {
            MartialArt art = MartialArtFactory.Create(
                "s_" + name, name, ArtKind.Attack, ArtTier.Major,
                discipline, Alignment.Orthodox, "화산파");

            int sessions = AlignmentCurve.SessionsToReach(
                Alignment.Orthodox, MartialStage.ProficiencyForStage(stage));

            var attacker = new Combatant(name, SpecStats(),
                new List<LearnedArt> { new LearnedArt(art, sessions, Alignment.Orthodox) },
                new List<DisciplineMastery>
                {
                    new DisciplineMastery(discipline, DisciplineCurve.SessionsToMaster(discipline)),
                });
            Combatant defender = Fighter("참정", MasterySessions);

            int total = 0;
            for (uint seed = 1; seed <= Seeds; seed++)
            {
                CombatResult r = CombatResolver.Resolve(attacker, defender, new XorShiftRandom(seed));
                IReadOnlyList<CombatLogEntry> log = r.Log;
                for (int i = 0; i < log.Count; i++)
                {
                    // 지속피해 줄은 `<이름> : 출혈 피해 N` 꼴이다 — 타격 줄(`→`)과 구분된다.
                    string line = log[i].ToString();
                    int mark = line.IndexOf("출혈 피해 ");
                    if (mark < 0) continue;

                    int start = mark + "출혈 피해 ".Length;
                    int end = start;
                    while (end < line.Length && line[end] >= '0' && line[end] <= '9') end++;
                    if (end > start) total += int.Parse(line.Substring(start, end - start));
                }
            }
            return total;
        }

        /// <summary>같은 조건에서 앞 무공의 승률. 무승부는 0.5 로 센다.</summary>
        private static double WinRate(string nameA, string nameB)
        {
            Combatant a = Fighter(nameA, MasterySessions);
            Combatant b = Fighter(nameB, MasterySessions);

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
