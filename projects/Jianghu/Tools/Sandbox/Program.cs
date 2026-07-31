using System;
using System.Collections.Generic;
using System.Text;
using Jianghu.Core.Characters;
using Jianghu.Core.Combat;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;
using Jianghu.Core.Rng;

namespace Jianghu.Sandbox
{
    /// <summary>
    /// 무공 전수 대전을 돌려 승률 순위를 찍는다.
    ///
    /// 반증 조건 2 판정 도구다 — "특정 조합 하나가 명백히 우월해 나머지가 죽는가".
    /// 대상은 <see cref="MartialArtCatalog"/> 의 공격 초식 전부이며, 무공이 93개로 늘어도 그대로 돈다.
    ///
    /// ⚠⚠ **계층 안에서만 비교한다.** 대문파 무공이 강호무학보다 강한 것은 정상이다 —
    ///   접근성이 그 대가다(SchoolTier 주석 참조). 계층을 섞어 평평하게 맞추면 성장의 의미가 사라진다.
    /// </summary>
    internal static class Program
    {
        private const int FightsPerMatchup = 100;

        /// <summary>
        /// **민감도표 전용 표본 수** (2026-07-31 신설).
        ///
        /// ⚠⚠ 100전이면 승률 60% 근처에서 표준편차가 **약 4.9%p** 다. 목표 구간이 53~65% 인데
        ///   측정 오차가 그 폭의 절반이라, 상수를 고쳐 얻은 변화인지 흔들림인지 구분되지 않는다 —
        ///   실제로 상태이상 조정 중에 *"고쳤는데 값이 안 움직이는"* 상황이 나왔다.
        ///   400전이면 편차가 절반(약 2.4%p)으로 줄고, 대상 매치업이 수십 개뿐이라 비용도 작다.
        ///
        /// ⚠ 계층 승률표(<see cref="FightsPerMatchup"/>)는 그대로 둔다 — 그쪽은 138×138 이라
        ///   같은 배수를 곱하면 실행 시간이 통째로 늘어난다.
        /// </summary>
        private const int SensitivityFights = 400;

        /// <summary>
        /// ⚠⚠ **측정에서 움직이는 변수는 무공 경지 하나뿐이다** (2026-07-31 사용자 교정).
        ///
        /// 그전에는 `sessions` 하나로 **무공 숙련과 유형 숙달을 동시에** 올리고 있었다.
        /// 둘은 다른 축이다 — 무공 경지는 무공마다 따로 쌓고(1~10성), 유형 숙달(백일창·천일도·
        /// 만일검)은 **사람이 그 무기를 얼마나 다뤘는가**로 캐릭터 쪽에 가깝다.
        /// 뭉쳐서 재면 *"무공이 세진 것인지 사람이 세진 것인지"* 를 분리할 수 없다.
        ///
        /// → **유형 숙달은 만렙(숙련 100)으로 고정**한다. 캐릭터 능력치를 만렙으로 고정한 것과 같은 이유다.
        /// </summary>
        private static int MasteredSessions(Discipline discipline)
        {
            return DisciplineCurve.SessionsToMaster(discipline);
        }

        private const double DominantThreshold = 0.65;
        private const double DeadThreshold = 0.35;

        private static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;

            PrintMorphemeReport();

            List<MartialArt> techniques = MartialArtCatalog.Techniques();
            Console.WriteLine("무공 " + MartialArtCatalog.All.Count + "종(공격 초식 " + techniques.Count + ")"
                              + " · 문파 " + SchoolCatalog.All.Count + "곳 · 매치업당 " + FightsPerMatchup + "전");

            // ⚠⚠ 2026-07-31 사용자 교정 — 측정 축이 **수련 횟수에서 무공 경지(1~10성)로** 바뀌었다.
            //   횟수는 성향마다 뜻이 달라진다(같은 200회가 정파 10성 · 마도 9성). 경지로 말하면
            //   **10성은 어느 성향에게나 10성**이고, 다른 것은 거기 도달하는 비용뿐이다.
            //   초반(3성) · 중반(6성) · 후반(10성= 무공 경지의 최종점) 세 지점에서 잰다.
            foreach (int stage in MartialStage.MeasurementStages)
            {
                Console.WriteLine();
                Console.WriteLine("████ 무공 " + MartialStage.Describe(stage) + " 시점 ████");
                foreach (SchoolTier tier in new[] { SchoolTier.Wanderer, SchoolTier.Minor, SchoolTier.Major })
                {
                    PrintRanking(techniques, tier, stage);
                }
                PrintCrossTier(techniques, stage);

                PrintSensitivity(stage);
            }

            PrintStartingViability();
            PrintSampleBattle(MartialStage.MaxStage);
        }

        // ─────────────────────────── 형태소 역산 리포트 (설계안 §5-2) ───────────────────────────

        /// <summary>
        /// 기존 무공 이름을 형태소 파서에 넣어 **실패를 실패로 처리하지 않고 리포트로 모은다.**
        ///
        /// ⚠⚠ 이 리포트가 곧 *"사전에 무엇이 빠졌는가"* 의 답이다. 몇 개가 통과하는지가
        ///   형태소 사전의 실전 적합도를 그대로 보여주고, 조합 규칙이 현실과 맞는지도 함께 드러난다
        ///   (기존 이름 대부분이 규칙을 위반한다면 규칙이 현실과 안 맞는다는 뜻이다).
        ///
        /// ⚠ 기존 36개는 형태소 체계를 **모르고 지은 이름**이다. 통과율이 낮은 것 자체는 실패가 아니다.
        ///   보아야 할 것은 **어떤 글자가 몇 번 걸리는가** — 반복해서 나오는 글자가 사전에 빠진 형태소다.
        /// </summary>
        private static void PrintMorphemeReport()
        {
            Console.WriteLine("══════ 형태소 역산 리포트 (설계안 §5-2) ══════");
            Console.WriteLine("형태소 " + MorphemeDictionary.Count + "자 · 배경어 " + MorphemeDictionary.BackgroundCount
                              + "자 · 접미사 " + ArtSuffixCatalog.All.Count + "종");
            Console.WriteLine();

            var missing = new Dictionary<char, int>();
            int noSuffix = 0;
            int parsedOk = 0;
            int ruleOk = 0;

            foreach (MartialArt art in MartialArtCatalog.All)
            {
                // ⚠⚠ 기존 무공명은 `청강검법(靑剛劍法)` 처럼 **한자를 괄호로 달아 한 문자열에 넣어 뒀다.**
                //   파서는 한글 표기만 다루므로 괄호 앞까지만 쓴다.
                //   → 형태소 체계로 넘어가면 한자는 이름에 박아 넣을 것이 아니라 형태소가 갖는 정보다
                //     (Morpheme.Hanja). 이름 필드를 한글 전용으로 정리할 필요가 있다.
                string name = KoreanNameOf(art.Name);

                ParsedArtName parsed;
                IReadOnlyList<string> problems;

                if (MorphemeParser.TryParse(name, out parsed, out problems))
                {
                    parsedOk++;
                    IReadOnlyList<ArtRuleViolation> violations = ArtCompositionRule.Validate(parsed);
                    if (violations.Count == 0)
                    {
                        ruleOk++;
                        Console.WriteLine("  ✅ " + Pad(name, 12) + parsed);
                    }
                    else
                    {
                        var messages = new List<string>();
                        for (int i = 0; i < violations.Count; i++) messages.Add(violations[i].Message);
                        Console.WriteLine("  ⚠  " + Pad(name, 12) + string.Join(" · ", messages));
                    }
                    continue;
                }

                ArtSuffix suffix;
                string body;
                if (!ArtSuffixCatalog.TryStrip(name, out suffix, out body))
                {
                    noSuffix++;
                    Console.WriteLine("  ❌ " + Pad(name, 12) + "접미사 없음");
                    continue;
                }

                var unknown = new List<char>();
                foreach (char c in body)
                {
                    if (MorphemeDictionary.Contains(c)) continue;
                    unknown.Add(c);
                    missing[c] = missing.ContainsKey(c) ? missing[c] + 1 : 1;
                }
                Console.WriteLine("  ❌ " + Pad(name, 12) + "미등록: " + string.Join(" · ", unknown));
            }

            int total = MartialArtCatalog.All.Count;
            Console.WriteLine();
            Console.WriteLine("  → 분해 성공 " + parsedOk + "/" + total
                              + " · 그중 규칙까지 통과 " + ruleOk
                              + " · 접미사 없음 " + noSuffix);

            if (missing.Count > 0)
            {
                var rows = new List<KeyValuePair<char, int>>(missing);
                rows.Sort((x, y) => y.Value.CompareTo(x.Value));

                Console.WriteLine();
                Console.WriteLine("  ── 미등록 글자 (많이 나오는 순) " + new string('─', 30));
                Console.WriteLine("  ⚠ 여러 번 나오는 글자는 사전에 빠진 형태소일 가능성이 높다.");
                for (int i = 0; i < rows.Count; i++)
                {
                    Console.WriteLine("     " + rows[i].Key + "  " + rows[i].Value + "회");
                }
            }
            Console.WriteLine();
        }

        /// <summary>`청강검법(靑剛劍法)` 에서 한글 표기 `청강검법` 만 떼어낸다.</summary>
        private static string KoreanNameOf(string name)
        {
            int paren = name.IndexOf('(');
            return paren < 0 ? name : name.Substring(0, paren);
        }

        // ─────────────────── 형태소 민감도표 (설계안 §5-4) ───────────────────

        /// <summary>
        /// **형태소 하나만 다른 무공 쌍**을 붙여 승률을 잰다. 어느 글자가 지배적인지 지목하는 유일한 도구다.
        ///
        /// ⚠⚠ **형태소 체계이기 때문에 비로소 가능해진 측정이다.** 무공을 개별 조정하던 시절에는
        ///   "이 무공이 센 이유" 를 글자 단위로 분리할 수 없었다.
        ///
        /// 판정: **47~53% = 무의미한 형태소** · 53~65% = 정상 · **65% 이상 = 지배적 형태소**
        /// ⚠ 성향 곡선 때문에 수련 시점마다 순위가 뒤집힐 수 있어 두 시점에서 잰다.
        /// </summary>
        private static void PrintSensitivity(int stage)
        {
            Console.WriteLine();
            Console.WriteLine("██ 형태소 민감도 (무공 " + MartialStage.Describe(stage) + ") — 한 글자만 바꿔 "
                              + SensitivityFights + "전 ██");

            // 같은 카테고리 안은 서로 교체해 비교한다(카테고리당 1자 규칙 때문).
            Compare("공격방식", "정", true, stage, "벌", "참", "절", "단", "자", "창", "구", "타", "격", "박", "투", "척", "포", "사");
            Compare("무공형태", "참", false, stage, "정", "직", "중", "후", "쾌", "환", "궤", "유", "변");

            // 선택 카테고리는 **넣음 vs 뺌** 으로 비교한다.
            // ⚠⚠ 2026-07-31 신설. 방어 카테고리가 민감도표에 **통째로 빠져 있었다** —
            //   측정 대상이 아니었으니 방어 형태소 12자가 죽어 있는지조차 알 수 없었다.
            //   ⚠ 대표 1자씩만 잰다(방=거=항=어=호, 피=둔=섬, 반=역=응). 같은 행은 수치가 동일하다.
            CompareOptional("방어", stage, "방", "피", "반", "계");

            CompareOptional("수식", stage, "속", "신", "급", "적", "확", "명", "광", "휘", "야", "암", "한", "현");
            CompareOptional("자연속성", stage, "풍", "뇌", "수", "화", "냉");
            CompareOptional("상태이상", stage, "독", "혈", "비", "염", "빙", "탈", "경");
        }

        /// <summary>같은 카테고리 형태소들을 서로 붙인다. 기준 글자 하나를 고정하고 나머지 한 자리를 바꾼다.</summary>
        private static void Compare(string label, string fixedChar, bool varyFirst, int stage, params string[] chars)
        {
            Console.WriteLine();
            Console.WriteLine("  ── " + label + " ──");

            var rows = new List<KeyValuePair<string, double>>();
            for (int i = 0; i < chars.Length; i++)
            {
                string name = varyFirst ? chars[i] + fixedChar : fixedChar + chars[i];
                double sum = 0;
                int n = 0;
                for (int j = 0; j < chars.Length; j++)
                {
                    if (i == j) continue;
                    string other = varyFirst ? chars[j] + fixedChar : fixedChar + chars[j];
                    sum += Duel(name, other, stage);
                    n++;
                }
                rows.Add(new KeyValuePair<string, double>(chars[i], sum / n));
            }
            Report(rows);
        }

        /// <summary>선택 카테고리 — 그 글자를 넣은 무공 vs 안 넣은 무공.</summary>
        private static void CompareOptional(string label, int stage, params string[] chars)
        {
            Console.WriteLine();
            Console.WriteLine("  ── " + label + " (넣음 vs 뺌) ──");

            var rows = new List<KeyValuePair<string, double>>();
            for (int i = 0; i < chars.Length; i++)
            {
                rows.Add(new KeyValuePair<string, double>(chars[i], Duel("참정" + chars[i], "참정", stage)));
            }
            Report(rows);
        }

        private static void Report(List<KeyValuePair<string, double>> rows)
        {
            rows.Sort((x, y) => y.Value.CompareTo(x.Value));
            for (int i = 0; i < rows.Count; i++)
            {
                double rate = rows[i].Value * 100;
                string flag = rate >= 65 ? "  ⚠ 지배적"
                    : (rate >= 47 && rate <= 53) ? "  ⚠ 무의미"
                    : "";
                Console.WriteLine("     " + Pad(rows[i].Key, 4) + rate.ToString("F1").PadLeft(6) + "%" + flag);
            }
        }

        /// <summary>두 무공명을 붙여 앞쪽의 승률을 낸다. 조합 규칙을 어기는 이름은 0.5(무효)로 돌린다.</summary>
        private static double Duel(string nameA, string nameB, int stage)
        {
            MartialArt a = TryBuild(nameA);
            MartialArt b = TryBuild(nameB);
            if (a == null || b == null) return 0.5;

            return WinRate(ToCombatant(a, stage), ToCombatant(b, stage), SensitivityFights);
        }

        private static MartialArt TryBuild(string name)
        {
            MartialArt art;
            IReadOnlyList<string> problems;
            // 대문파급·정파·검으로 고정한다 — 비교 대상이 형태소 하나뿐이어야 하므로 나머지는 전부 같게 둔다.
            MartialArtFactory.TryCreate(
                "s_" + name, name, ArtKind.Attack, ArtTier.Major,
                Discipline.Sword, Alignment.Orthodox, "화산파", 1, null, out art, out problems);
            return art;
        }

        // ─────────────────────────── 캐릭터 구성 ───────────────────────────

        /// <summary>
        /// ⚠⚠ 2026-07-31 — **만렙 캐릭터 기준으로 잰다**(사용자 확정). 캐릭터 능력치가 나중에
        ///   성장 요소가 되므로, 지금 맞추는 수치가 **성장의 끝**이어야 다시 맞출 일이 없다.
        ///
        /// ⚠ 2026-07-30 에는 정의서 §1-1 의 값(체력 100 · 공격 1)을 그대로 썼다. 그건 문맥상
        ///   **시작값**이며, 시작 기준 측정은 <see cref="PrintStartingViability"/> 가 따로 한다.
        /// </summary>
        private static CharacterStats Stats()
        {
            return CharacterStats.MaxLevel();
        }

        private static Combatant ToCombatant(MartialArt art, int stage)
        {
            return ToCombatant(art, stage, Stats());
        }

        /// <summary>
        /// **무공 경지 `stage`(1~10성) 의 대전자**를 만든다.
        ///
        /// ⚠⚠ 경지 → 수련 횟수 환산은 **성향마다 다르다**(정파 0.70/회 · 사파 1.60 → 소프트캡 후 1/5 ·
        ///   마도 0.45). 그래서 횟수가 아니라 경지로 지정한다 — 그래야 세 성향의 **같은 지점**을 비교한다.
        ///   같은 200회가 정파에게는 10성이고 마도에게는 9성이다.
        /// </summary>
        private static Combatant ToCombatant(MartialArt art, int stage, CharacterStats stats)
        {
            // ⚠ 강호무학은 성향이 없어 익힌 사람의 성향이 필요하다. 측정에서는 정파로 고정한다 —
            //   성향별 비교는 문파 무공으로 하고, 강호무학은 계층 비교용 표본일 뿐이다.
            Alignment owner = art.Alignment ?? Alignment.Orthodox;

            int sessions = AlignmentCurve.SessionsToReach(owner, MartialStage.ProficiencyForStage(stage));
            var arts = new List<LearnedArt> { new LearnedArt(art, sessions, owner) };

            // ⚠ 유형 숙달은 **만렙 고정**이다(위 `MasteredSessions` 주석). 무공 경지와 같이 움직이면
            //   두 축이 섞여, 형태소 민감도가 무공 때문인지 무기 숙달 때문인지 갈리지 않는다.
            var masteries = new List<DisciplineMastery>
            {
                new DisciplineMastery(art.Discipline, MasteredSessions(art.Discipline)),
            };
            return new Combatant(art.Name, stats, arts, masteries);
        }

        // ─────────────────────────── 측정 ───────────────────────────

        private static double WinRate(Combatant a, Combatant b, int fights = FightsPerMatchup)
        {
            double score = 0;
            for (uint seed = 1; seed <= fights; seed++)
            {
                CombatResult r = CombatResolver.Resolve(a, b, new XorShiftRandom(seed));
                if (r.Outcome == CombatOutcome.AttackerWin) score += 1.0;
                else if (r.Outcome == CombatOutcome.Draw) score += 0.5;
            }
            return score / fights;
        }

        /// <summary>한 계층 안에서 전수 대전을 돌려 순위를 낸다.</summary>
        private static void PrintRanking(List<MartialArt> all, SchoolTier tier, int stage)
        {
            var group = new List<MartialArt>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Tier == tier) group.Add(all[i]);
            }
            if (group.Count < 2) return;

            Console.WriteLine();
            Console.WriteLine("── " + TierName(tier) + " (" + group.Count + "종) " + new string('─', 40));

            var fighters = new List<Combatant>();
            for (int i = 0; i < group.Count; i++) fighters.Add(ToCombatant(group[i], stage));

            var rows = new List<KeyValuePair<MartialArt, double>>();
            for (int i = 0; i < group.Count; i++)
            {
                double sum = 0;
                for (int j = 0; j < group.Count; j++)
                {
                    if (i == j) continue;
                    sum += WinRate(fighters[i], fighters[j]);
                }
                rows.Add(new KeyValuePair<MartialArt, double>(group[i], sum / (group.Count - 1)));
            }
            rows.Sort((x, y) => y.Value.CompareTo(x.Value));

            for (int i = 0; i < rows.Count; i++)
            {
                MartialArt a = rows[i].Key;
                string school = a.IsWandererArt ? "(강호무학)" : a.School;
                Console.Write("  " + (i + 1).ToString().PadLeft(2) + "  "
                              + Pad(a.Name, 26) + Pad(school, 12)
                              + Pad(Short(a.Discipline) + "·" + Short(a.Alignment), 8)
                              + (rows[i].Value * 100).ToString("F1").PadLeft(6) + "%");
                if (rows[i].Value >= DominantThreshold) Console.Write("  ⚠ 지배 의심");
                else if (rows[i].Value <= DeadThreshold) Console.Write("  ⚠ 죽은 선택지 의심");
                Console.WriteLine();
            }

            double gap = (rows[0].Value - rows[rows.Count - 1].Value) * 100;
            Console.WriteLine("  → 계층 내 격차 " + gap.ToString("F1") + "%p"
                              + (gap <= 30 ? "  ✅ 목표(30%p) 이내" : "  ⚠ 목표(30%p) 초과"));
        }

        /// <summary>계층 간 격차 — 여기는 벌어지는 것이 **정상**이다. 다만 압도적이면 안 된다.</summary>
        private static void PrintCrossTier(List<MartialArt> all, int stage)
        {
            var byTier = new Dictionary<SchoolTier, List<MartialArt>>();
            foreach (SchoolTier t in new[] { SchoolTier.Wanderer, SchoolTier.Minor, SchoolTier.Major })
            {
                byTier[t] = new List<MartialArt>();
            }
            for (int i = 0; i < all.Count; i++) byTier[all[i].Tier].Add(all[i]);

            Console.WriteLine();
            Console.WriteLine("── 계층 간 (상위가 이기는 것이 정상. 다만 90% 이상이면 하위 계층이 무의미해진다) ──");

            Report(byTier, SchoolTier.Minor, SchoolTier.Wanderer, stage);
            Report(byTier, SchoolTier.Major, SchoolTier.Minor, stage);
            Report(byTier, SchoolTier.Major, SchoolTier.Wanderer, stage);
        }

        private static void Report(
            Dictionary<SchoolTier, List<MartialArt>> byTier, SchoolTier high, SchoolTier low, int stage)
        {
            List<MartialArt> hi = byTier[high];
            List<MartialArt> lo = byTier[low];
            if (hi.Count == 0 || lo.Count == 0) return;

            double sum = 0;
            int n = 0;
            for (int i = 0; i < hi.Count; i++)
            {
                Combatant h = ToCombatant(hi[i], stage);
                for (int j = 0; j < lo.Count; j++)
                {
                    sum += WinRate(h, ToCombatant(lo[j], stage));
                    n++;
                }
            }
            double rate = sum / n * 100;
            string flag = rate >= 90 ? "  ⚠ 하위 계층이 무의미해짐" : rate <= 50 ? "  ⚠ 상위 계층 이점이 없음" : "";
            Console.WriteLine("  " + Pad(TierName(high) + " vs " + TierName(low), 28)
                              + rate.ToString("F1").PadLeft(6) + "%" + flag);
        }

        // ─────────────────── 시작 캐릭터 전투 성립 검사 (2026-07-31 신설) ───────────────────

        /// <summary>
        /// **시작 캐릭터로도 전투가 성립하는가.**
        ///
        /// ⚠⚠ 밸런싱은 만렙 기준으로 한다(사용자 확정). 그러면 **시작 시점은 아무도 안 보게 되는데**,
        ///   거기서 전투가 성립하지 않으면 게임이 시작되지도 않는다. 그래서 시작 스탯에서는
        ///   승률이 아니라 **성립 조건**만 본다 — 목표 8~15턴(HANDOFF §5) · 무승부 없음.
        ///
        /// ⚠ 8~15턴은 편의가 아니라 **성향 설계의 성립 조건**이다. 전투가 길어지면 큰 수의 법칙으로
        ///   사파(±5%)와 마도(±35%)의 편차 차이가 평균에 묻혀 정체성이 사라진다.
        /// </summary>
        private static void PrintStartingViability()
        {
            Console.WriteLine();
            Console.WriteLine("██ 시작 캐릭터 전투 성립 검사 — 목표 8~15턴 · 무승부 0 ██");

            // ⚠ 두 축을 **양 끝으로만** 본다 — 시작 캐릭터 × 무공 1성 / 만렙 캐릭터 × 무공 10성.
            //   중간은 민감도표가 3성·6성·10성으로 이미 훑는다.
            foreach (var row in new[]
            {
                new KeyValuePair<string, CharacterStats>("시작 캐릭터 · 무공 1성", CharacterStats.Starting()),
                new KeyValuePair<string, CharacterStats>("만렙 캐릭터 · 무공 10성", CharacterStats.MaxLevel()),
            })
            {
                int stage = row.Key.StartsWith("시작") ? 1 : MartialStage.MaxStage;
                MartialArt a = TryBuild("참정");
                MartialArt b = TryBuild("참정");
                if (a == null || b == null) return;

                int draws = 0;
                long turnSum = 0;
                int min = int.MaxValue, max = 0;
                for (uint seed = 1; seed <= FightsPerMatchup; seed++)
                {
                    CombatResult r = CombatResolver.Resolve(
                        ToCombatant(a, stage, row.Value), ToCombatant(b, stage, row.Value),
                        new XorShiftRandom(seed));
                    if (r.Outcome == CombatOutcome.Draw) draws++;
                    turnSum += r.Turns;
                    if (r.Turns < min) min = r.Turns;
                    if (r.Turns > max) max = r.Turns;
                }

                double avg = (double)turnSum / FightsPerMatchup;
                string flag = draws > 0 ? "  ⚠ 무승부 발생"
                    : (avg < 8 || avg > 15) ? "  ⚠ 목표 8~15턴 밖" : "  ✅";
                Console.WriteLine("  " + Pad(row.Key, 18)
                                  + "평균 " + avg.ToString("F1") + "턴 (" + min + "~" + max + ")"
                                  + " · 무승부 " + draws + flag);
            }
        }

        // ─────────────────────────── 표본 로그 ───────────────────────────

        private static void PrintSampleBattle(int stage)
        {
            Console.WriteLine();
            Console.WriteLine("══════ 전투 로그 표본 (무공 " + MartialStage.Describe(stage) + ", 시드 42) ══════");

            // 성격이 가장 대비되는 둘: 마도 전승무학(마한중참) vs 사파 대문파(궤암척혈)
            MartialArt aArt = MartialArtCatalog.ByName("마한중참");
            MartialArt bArt = MartialArtCatalog.ByName("궤암척혈");
            if (aArt == null || bArt == null)
            {
                Console.WriteLine("  ⚠ 표본 무공 ID 를 찾지 못했다. 카탈로그가 바뀌었는지 확인할 것.");
                return;
            }

            CombatResult r = CombatResolver.Resolve(
                ToCombatant(aArt, stage), ToCombatant(bArt, stage), new XorShiftRandom(42u));
            foreach (CombatLogEntry e in r.Log) Console.WriteLine("  " + e);
            Console.WriteLine("  → " + r);
        }

        // ─────────────────────────── 표시 도우미 ───────────────────────────

        private static string TierName(SchoolTier t)
        {
            switch (t)
            {
                case SchoolTier.Wanderer: return "강호무학";
                case SchoolTier.Minor: return "소문파";
                case SchoolTier.Major: return "대문파";
                default: return "?";
            }
        }

        private static string Short(Discipline d)
        {
            switch (d)
            {
                case Discipline.Sword: return "검";
                case Discipline.Blade: return "도";
                case Discipline.Fist: return "권";
                case Discipline.Spear: return "창";
                case Discipline.Dagger: return "비도";
                default: return "?";
            }
        }

        /// <summary>⚠ 성향이 null 이면 강호무학이다 — 익힌 사람의 성향을 따르므로 무공 자체에는 성향이 없다.</summary>
        private static string Short(Alignment? a)
        {
            switch (a)
            {
                case Alignment.Orthodox: return "정";
                case Alignment.Unorthodox: return "사";
                case Alignment.Demonic: return "마";
                case null: return "-";
                default: return "?";
            }
        }

        /// <summary>한글이 콘솔에서 2칸을 차지하는 것을 감안해 폭을 맞춘다.</summary>
        private static string Pad(string s, int width)
        {
            int visual = 0;
            foreach (char c in s) visual += c > 0x7F ? 2 : 1;
            int padding = width - visual;
            return padding > 0 ? s + new string(' ', padding) : s + " ";
        }
    }
}
