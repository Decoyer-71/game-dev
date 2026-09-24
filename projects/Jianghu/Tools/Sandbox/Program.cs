using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Jianghu.Core.Characters;
using Jianghu.Core.Combat;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Display;
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
        ///
        /// ⚠⚠ **2026-08-02 400 → 1600 으로 다시 올렸다.** 같은 병이 한 단계 아래에서 재발했다 —
        ///   내공 형태소 블록은 효과가 **1~3%p 급**이라 400전으로는 분해되지 않는다.
        ///   실제로 400전에서 양(陽)이 **정확히 +0.00%p**(⚠무효) 로 나와 *"현행에서도 이미 죽어 있다"* 는
        ///   결론을 쓰고 커밋까지 했는데, 1600전에서 **+2.44%p** 가 나왔다. 400전 표본 안에는
        ///   양(陽)이 승패를 가른 시드가 한 번도 없었을 뿐이다.
        ///   → **0.00 은 "효과 없음" 이 아니라 "이 표본으로는 못 잡음" 일 수 있다.**
        ///   비용은 12s → 15.7s 로 거의 늘지 않는다(민감도 블록은 대상이 수십 개뿐이라).
        /// </summary>
        private const int SensitivityFights = 1600;

        private const double DominantThreshold = 0.65;
        private const double DeadThreshold = 0.35;

        /// <summary>
        /// 순위표를 뽑는 계층들.
        ///
        /// ⚠⚠ 2026-07-31 — **전승무학·절대경지가 빠져 있었다.** 그리고 계층을 문파명에서
        ///   유도하던 탓에 `SchoolCatalog` 에 없는 **대형세력 16종이 강호무학으로 분류**됐다.
        ///   그래서 *"계층 간 우위가 없다"* 는 측정이 나왔던 것이다 — 강호무학 그룹 11종 중
        ///   6종이 실제로는 대문파급 4자 무공이었다.
        /// </summary>
        private static readonly ArtTier[] RankedTiers =
        {
            ArtTier.Wanderer, ArtTier.Minor, ArtTier.Major, ArtTier.Legacy, ArtTier.Absolute,
        };

        /// <summary>
        /// **판정 지표 수집기 — 2026-08-05 신설.**
        ///
        /// ⚠⚠ **왜 만들었나.** 이 도구의 출력에는 필요한 지표가 **전부 들어 있었는데도**,
        ///   안을 실험할 때마다 `grep` 으로 **보고 싶은 부분만 잘라 보고** 전체 결론을 내는 실수가
        ///   2026-08-04~05 이틀에 **네 번** 반복됐다. 마지막 사례가 결정적이다 — 기만 형태소의
        ///   대가를 옮기고 *"소문파가 좋아졌으니 작동한다"* 고 보고했는데, 같은 출력의 **전승무학
        ///   블록에는 종환화격이 79.7%(지배)로 올라 있었다.** 안 본 것이지 없던 것이 아니다.
        ///
        /// → **정보 부족이 아니라 선택적 관찰이다.** 그러니 대책도 *"더 잘 보자"* 가 아니라
        ///   **"안 본 지표의 변화를 도구가 들이밀게"** 여야 한다. 이 프로젝트가 §1 에서
        ///   *"실수를 사람 기억에 맡기지 않고 컴파일이 깨지게 만들어 뒀다"* 고 한 것과 같은 선이다.
        ///
        /// 쓰는 법 — `--metrics <파일>` 로 기준선을 뜨고, 고친 뒤 `--compare <파일>` 로 **전 지표 diff**.
        /// </summary>
        private static class Metrics
        {
            private static readonly SortedDictionary<string, double> Values =
                new SortedDictionary<string, double>(StringComparer.Ordinal);

            public static void Add(string key, double value)
            {
                Values[key] = value;   // 같은 키가 두 번 오면 마지막이 이긴다(경지 루프 안에서 키에 경지를 넣는다)
            }

            public static void Dump(string path)
            {
                var sb = new StringBuilder();
                sb.Append("# Jianghu 밸런스 기준선 — Sandbox --metrics 로 생성. 손으로 고치지 말 것.\n");
                sb.Append("# 지표 ").Append(Values.Count).Append("개\n");
                foreach (KeyValuePair<string, double> kv in Values)
                {
                    sb.Append(kv.Key).Append(" = ").Append(kv.Value.ToString("F2")).Append('\n');
                }
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                Console.WriteLine();
                Console.WriteLine("기준선 " + Values.Count + "개 지표를 " + path + " 에 썼다.");
            }

            /// <summary>
            /// 기준선과 대조해 **바뀐 지표 전부**를 낸다.
            /// ⚠ 임계(<paramref name="epsilon"/>) 미만은 잡음으로 보고 접는다 — 단 접은 개수는 반드시 찍는다.
            /// </summary>
            public static void Compare(string path, double epsilon = 0.05)
            {
                if (!File.Exists(path))
                {
                    Console.WriteLine("⛔ 기준선 파일이 없다: " + path + "  (먼저 --metrics 로 뜨라)");
                    return;
                }

                var baseline = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    double v;
                    if (double.TryParse(line.Substring(eq + 1).Trim(),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out v))
                    {
                        baseline[line.Substring(0, eq).Trim()] = v;
                    }
                }

                var changed = new List<KeyValuePair<string, double[]>>();   // key -> {before, after}
                var added = new List<string>();
                int same = 0;

                foreach (KeyValuePair<string, double> kv in Values)
                {
                    double before;
                    if (!baseline.TryGetValue(kv.Key, out before)) { added.Add(kv.Key); continue; }
                    if (Math.Abs(kv.Value - before) < epsilon) { same++; continue; }
                    changed.Add(new KeyValuePair<string, double[]>(kv.Key, new[] { before, kv.Value }));
                }

                var removed = new List<string>();
                foreach (KeyValuePair<string, double> kv in baseline)
                {
                    if (!Values.ContainsKey(kv.Key)) removed.Add(kv.Key);
                }

                // 변화가 큰 순으로. **부호가 아니라 크기**로 정렬한다 — 어느 쪽이 나쁜지는 지표마다 다르다.
                changed.Sort((x, y) =>
                    Math.Abs(y.Value[1] - y.Value[0]).CompareTo(Math.Abs(x.Value[1] - x.Value[0])));

                Console.WriteLine();
                Console.WriteLine("══════ 기준선 대조 (" + path + ") ══════");
                Console.WriteLine("  바뀜 " + changed.Count + "건 · 같음 " + same + "건 · 신규 "
                                  + added.Count + "건 · 사라짐 " + removed.Count + "건");
                if (changed.Count == 0 && added.Count == 0 && removed.Count == 0)
                {
                    Console.WriteLine("  ✅ 모든 지표가 기준선과 같다.");
                    return;
                }

                Console.WriteLine();
                for (int i = 0; i < changed.Count; i++)
                {
                    double b = changed[i].Value[0], a = changed[i].Value[1];
                    Console.WriteLine("   " + Pad(changed[i].Key, 46)
                                      + b.ToString("F1").PadLeft(8) + " →" + a.ToString("F1").PadLeft(8)
                                      + "   (" + (a - b >= 0 ? "+" : "") + (a - b).ToString("F1") + ")");
                }
                for (int i = 0; i < added.Count; i++) Console.WriteLine("   🆕 신규   " + added[i]);
                for (int i = 0; i < removed.Count; i++) Console.WriteLine("   ⛔ 사라짐 " + removed[i]);

                Console.WriteLine();
                Console.WriteLine("  ⚠⚠ **이 목록 전부를 보고에 넣는다.** 일부만 인용하면 이 도구를 만든 이유가 사라진다.");
                Console.WriteLine("  ⚠ 좋아짐/나빠짐은 도구가 판정하지 않는다 — 지표마다 방향이 다르다. 사람이 읽어야 한다.");
            }
        }

        private static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            string metricsOut = null, compareTo = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--metrics") metricsOut = args[i + 1];
                else if (args[i] == "--compare") compareTo = args[i + 1];
            }

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
                foreach (ArtTier tier in RankedTiers)
                {
                    PrintRanking(techniques, tier, stage);
                }
                PrintCrossTier(techniques, stage);

                // ⚠⚠ 다대다는 **별도 표 · 별도 지표**다. 위 1대1 순위표와 섞지 않는다(설계 §D8).
                Console.WriteLine();
                Console.WriteLine("████ 4대4 동질 팀 — 범위 형태소가 값을 갖는가 (" + MultiFights + "전/쌍) ████");
                foreach (ArtTier tier in RankedTiers)
                {
                    PrintMultiCombat(techniques, tier, stage);
                    PrintCounterProbe(techniques, tier, stage);
                }

                PrintSensitivity(stage);
            }

            PrintDaggerMasteryCondition(techniques);
            PrintStartingViability();
            PrintSampleBattle(MartialStage.MaxStage);

            // ⚠⚠ 기준선 대조는 **맨 마지막**이다. 앞의 모든 표가 지표를 채운 뒤여야 한다.
            if (metricsOut != null) Metrics.Dump(metricsOut);
            if (compareTo != null) Metrics.Compare(compareTo);
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

            PrintInnerArtSensitivity(stage);
            PrintPinnacleSensitivity(stage);
            PrintAbsoluteRuleSensitivity(stage);
            PrintDisciplineSensitivity(stage);
            PrintMorphemeCountSensitivity(stage);
            PrintQiPressure(stage);
        }

        /// <summary>
        /// **내공 형태소 4자(양·음·합·식) — 2026-08-02 신설.**
        ///
        /// ⚠⚠ 이 넷은 **2026-08-01 까지 한 번도 측정된 적이 없었다.** 위 `CompareOptional` 들이
        ///   방어·수식·자연속성·상태이상만 재고 내공 카테고리를 통째로 빠뜨렸기 때문이다.
        ///   재 보니 넷 다 정확히 무효였고, 원인이 밸런스가 아니라 **엔진 결함 2건**이었다(§4-2-P·V).
        ///   → 저장소 교훈 *"측정하지 않는 축은 고장 나도 보이지 않는다"* 의 두 번째 사례다.
        ///
        /// ⚠ **`CompareOptional` 로 잴 수 없다.** 그건 글자를 공격 무공(`참정X`)에 넣어 재는데,
        ///   내공 형태소의 자리는 거기가 아니다. *"내공 무공을 하나 더 배운 쪽이 이기는가"* 로 잰다.
        ///
        /// ⚠⚠ **두 조건에서 재는 것이 핵심이다.**
        ///   기력 압력이 없으면(평타 전락률 0.0%) 최대기력·회복은 **무엇을 사도 값이 0** 이라
        ///   평범한 대전에서는 넷 다 항상 무효로 나온다 — 그건 형태소 탓이 아니라 압력 탓이다.
        ///   기력을 실제로 0 으로 미는 것은 현재 **기력소실 탈(奪)** 뿐이므로, 탈 대전에서 함께 잰다.
        ///
        /// ⚠⚠ **2026-08-02 2차 — 기준선 빼기를 폐기하고 <see cref="SymmetricAdvantage"/> 로 바꿨다.**
        ///   그전에는 `WinRate(배운 쪽, 안 배운 쪽)` 에서 **자기대전 값(대조군)을 빼서** 값을 만들었다.
        ///   절대경지 블록에서 그 방식이 실제로 결론을 뒤집은 전례가 있다(HANDOFF §4-2-AA) —
        ///   **편향은 쌍마다 다르므로 한 대조군 값을 전 행에 빼면 틀린다.**
        ///   자리를 바꿔 두 번 재 평균내면 편향이 구조적으로 상쇄되어 **0 이 진짜 0** 이 된다.
        ///   → 기력 작업의 주 계기판이 이 블록이므로(HANDOFF §4-3-7) 먼저 바꾼다.
        ///
        /// ⚠ 표 첫 행의 **자기대전 sanity 행**은 지우지 말 것. HANDOFF §4-3-6 일반화 1 —
        ///   *"측정 방식이 0 을 0 으로 내는지부터 확인한다."* 이 행이 `+0.00%p` 가 아니면 표 전체를 못 쓴다.
        /// </summary>
        private static void PrintInnerArtSensitivity(int stage)
        {
            Console.WriteLine();
            Console.WriteLine("  ── 내공 형태소 (그 내공 무공을 배운 쪽 vs 안 배운 쪽 · 양방향 평균) ──");
            Console.WriteLine("     ⚠ 압력이 없으면 전부 무효로 나온다. 그건 형태소가 아니라 기력 축의 문제다");

            // ⚠⚠ **2026-08-02 — 왼쪽 열의 공격 무공을 `참정`(2자) → `참정독명`(4자) 로 바꿨다.**
            //   시작 기력 25% 를 채택해 압력이 생긴 직후 이 표를 다시 뽑았는데 왼쪽 열이 **여전히
            //   전부 +0.00 무효**였다. 원인은 형태소도 엔진도 아니라 **이 줄이었다** — 참정은
            //   2자라 기력 6 이고 회복이 10 이라, **어떤 시작 기력에서도 마를 수가 없다**
            //   (같은 실행의 전락률 표가 참정 0.0% · 참정독 0.0% · 참정독명 11.8% 로 그대로 말해 준다).
            //   → 즉 이 열은 *"압력 없음"* 을 잰 것이 아니라 **압력이 없는 무공을 골라 놓고 있었다.**
            //   ⚠ 하마터면 *"채택했는데 내공 4자가 안 살아났다"* 로 잘못 읽을 뻔했다.
            //     HANDOFF §4-3-6 일반화 1(*"측정 방식이 0 을 0 으로 내는지부터 확인한다"*)의 **뒷면**이다 —
            //     sanity 행이 0 을 내는지만 봤지, **효과가 나와야 할 행이 나올 수 있는 조건인지**는 안 봤다.
            Console.WriteLine("     {0,-6}{1,10}{2,10}", "", "4자 압력", "탈 대전");
            Console.WriteLine("     {0,-6}{1,9}{2,10}  ← 0 이어야 한다 (sanity)",
                "대조군", Signed(InnerDuel("참정독명", null, stage)), Signed(InnerDuel("참정탈", null, stage)));

            string[] inners = { "양공", "음공", "합공", "식공" };
            for (int i = 0; i < inners.Length; i++)
            {
                Console.WriteLine("     {0,-6}{1,9}{2,10}",
                    inners[i],
                    Signed(InnerDuel("참정독명", inners[i], stage)),
                    Signed(InnerDuel("참정탈", inners[i], stage)));
            }
        }

        /// <summary>
        /// **극한경지 9자(존·제·마·패·성·선·왕·황·종) — 2026-08-05 신설.**
        ///
        /// ⚠⚠ **이 축은 지금까지 한 번도 상시 측정된 적이 없다.** 무공형태·수식·방어·자연속성·
        ///   상태이상·내공·절대경지 규칙에는 전부 고정 대조군 블록이 있는데 극한경지만 없었고,
        ///   그래서 이 9자는 **전승무학 8종의 계층 승률로 간접 추정**하는 수밖에 없었다.
        ///   그 승률에는 나머지 3글자가 섞여 있다 — 실제로 `존풍쾌절`(존 공격 1.5)이
        ///   `제화정참`(제 공격 1.0)보다 낮게 나오는데, 그게 존 탓인지 같이 붙은 쾌(快 명중−2) 탓인지
        ///   **가릴 방법이 없었다.**
        ///   → 저장소 교훈 *"측정하지 않는 축은 고장 나도 보이지 않는다"* 의 **네 번째** 사례다
        ///     (앞선 셋: 방어 카테고리 · 내공 형태소 · 절대경지 규칙).
        ///   → `balance-audit` 스킬의 한계 항목 *"Sandbox 가 안 재는 축은 diff 에도 안 나온다"* 가
        ///     정확히 여기였다. 이 블록이 없으면 마(魔)를 깎아도 `--compare` 가 결과를 못 보여준다.
        ///
        /// ── 대조군 설계 (이 블록의 본체다) ────────────────────────────────────────
        ///
        /// ⚠⚠ **`X풍쾌참` vs `풍쾌참` 로 재지 않는다.** 2026-08-02 §4-2-X 가 그 방식을 썼는데
        ///   **4자 vs 3자**라 두 겹으로 오염돼 있다: ⓐ 형태소 하나만큼의 성능 차 ⓑ **기력비용 12 vs 9**.
        ///   ⓑ 가 특히 나쁘다 — 회복이 10 이라 3자는 **어떤 시작 기력에서도 마르지 않는다**.
        ///   즉 대조군이 기력 축에서 일방적으로 유리했고, 그 상태로 잰 선(仙) 값은 신뢰할 수 없다.
        ///   같은 병을 2026-08-02~04 에 세 번 밟았다(식息 · 수水 · ㉯실험 3자 대조).
        ///
        /// ✅ **대조군은 `명풍쾌참` — 극한경지 자리에 평범한 수식 글자 명(明)을 넣은 같은 길이 4자다.**
        ///   ⓐ 성능 형태소 4자로 같다 ⓑ **기력비용이 12 로 같다** ⓒ 전승무학은 극한경지를 **요구하지
        ///   않으므로**(`ArtCompositionRule` 은 *"극한경지는 전승무학 전용"* 만 강제한다. 실제로
        ///   사천당가 `만우쾌사`에 극한경지가 없다) 대조군이 같은 계층에 설 수 있다.
        ///   → 그래서 각 행은 **"이 극한경지 글자는 평범한 수식 한 글자보다 얼마나 나은가"** 를 뜻한다.
        ///   ⚠ 명(明)은 중립이 아니라 `critChance +10` 이다. **눈금의 영점이 0 이 아니라 명(明)이다** —
        ///     그래서 황(皇 critChance+15)처럼 성격이 겹치는 글자는 낮게 나오는 것이 정상이다.
        ///
        /// ⚠⚠ **조건은 제3자가 아니라 양쪽을 함께 바꿔서 만든다.** 상대를 따로 세우고 두 승률을
        ///   빼면 **한 대조군 값을 전 행에서 빼는** 금지된 방식이 된다(HANDOFF §4-2-AA — 그 방식이
        ///   실제로 결론을 뒤집은 전례가 있다). 대신 **패딩 한 글자를 양쪽 다 갈아** 조건을 만든다:
        ///   - **평범** = 풍(風) — 아무 조건 없음
        ///   - **방어전** = 방(防 방어+1.2·막기+8) — 서로 방어가 있어야 **마(魔)의 방어무시 25%** 가 일한다
        ///   - **기력전** = 탈(奪 기력소실) — 서로 기력을 깎아야 **선(仙)** 이 살아난다(§4-2-X: 선은
        ///     죽은 것이 아니라 **조건부**다. 탈 대전에서만 46.5 → 64.0 으로 올랐다)
        ///   세 열 모두 **양쪽이 같은 패딩**이라 `SymmetricAdvantage` 의 정의상 **0 이 진짜 0** 이다.
        ///
        /// ⚠ 첫 행의 자기대전 sanity 행은 지우지 말 것 (HANDOFF §4-3-6 일반화 1 —
        ///   *"측정 방식이 0 을 0 으로 내는지부터 확인한다"*). `+0.00%p` 가 아니면 표 전체를 못 쓴다.
        /// </summary>
        private static void PrintPinnacleSensitivity(int stage)
        {
            Console.WriteLine();
            Console.WriteLine("  ── 극한경지 9자 (같은 4자 전승무학 · 극한경지 자리에 명(明)을 넣은 대조군) ──");
            Console.WriteLine("     ⚠ 영점이 0 이 아니라 명(明 치명률+10)이다. 성격이 겹치는 글자는 낮게 나온다");

            // 패딩 3자 = <조건글자> + 쾌(무공형태 필수) + 참(공격방식 필수).
            // ⚠ 조건 글자만 다르고 나머지는 전 열에서 같다 — 열끼리 비교할 수 있는 이유다.
            string[][] columns =
            {
                new[] { "풍쾌참", "평범" },
                new[] { "방쾌참", "방어전" },
                new[] { "탈쾌참", "기력전" },
            };

            Console.WriteLine("     {0,-10}{1,11}{2,11}{3,11}", "", "평범", "방어전", "기력전");
            Console.WriteLine("     {0,-10}{1}{2}{3}  ← 0 이어야 한다 (sanity)",
                "대조군",
                Signed(PinnacleDuel('명', columns[0][0], stage)),
                Signed(PinnacleDuel('명', columns[1][0], stage)),
                Signed(PinnacleDuel('명', columns[2][0], stage)));

            // 정의서 §3-8 게재 순서. 표시용 한자는 사전에서 읽어 이름과 성능이 어긋날 수 없게 한다.
            char[] pinnacles = { '존', '제', '마', '패', '성', '선', '왕', '황', '종' };

            double best = double.MinValue;
            double worst = double.MaxValue;

            for (int i = 0; i < pinnacles.Length; i++)
            {
                double plain = PinnacleDuel(pinnacles[i], columns[0][0], stage);
                double guard = PinnacleDuel(pinnacles[i], columns[1][0], stage);
                double drain = PinnacleDuel(pinnacles[i], columns[2][0], stage);

                Morpheme m = MorphemeDictionary.Get(pinnacles[i]);
                Console.WriteLine("     {0,-10}{1}{2}{3}",
                    pinnacles[i] + " " + m.Hanja, Signed(plain), Signed(guard), Signed(drain));

                Metrics.Add("sens.극한경지.평범." + pinnacles[i] + "." + stage + "성", plain * 100);
                Metrics.Add("sens.극한경지.방어전." + pinnacles[i] + "." + stage + "성", guard * 100);
                Metrics.Add("sens.극한경지.기력전." + pinnacles[i] + "." + stage + "성", drain * 100);

                if (plain > best) best = plain;
                if (plain < worst) worst = plain;
            }

            // ⚠ 격차는 **평범 열만** 낸다. 조건 열의 격차는 "그 조건이 얼마나 갈리는가" 라
            //   축의 평탄함과 뜻이 다르다 — 한 숫자로 합치면 둘 다 못 읽는다.
            Console.WriteLine("     → 평범 열 스프레드 {0:F1}%p", (best - worst) * 100);
            Metrics.Add("sens.극한경지.격차평범." + stage + "성", (best - worst) * 100);

            PrintDoublesFormByForm(stage);
        }

        /// <summary>
        /// **종(宗)만 따로 — 무공형태를 갈아 가며 잰다** (2026-08-05 신설).
        ///
        /// ⚠⚠ **종은 위 표에서 읽으면 안 된다.** 9자 중 홀로 수치가 아니라 **규칙**을 만지기 때문이다
        ///   (`doublesFormEffect` — 무공형태 효과를 페널티까지 2배로 키운다). 위 표의 패딩 무공형태는
        ///   쾌(快 속도+2·명중−2)라서 종이 붙으면 **속도+4·명중−4** 가 되고, 명중 −4 는 명중률
        ///   −20%p 다(1점 = 5%p). 즉 위 표의 종 행은 *"종이 약하다"* 가 아니라
        ///   **"종을 가장 나쁜 무공형태와 묶었다"** 를 재고 있다.
        ///
        /// ⚠ 이 성질은 사전 주석이 이미 못박아 둔 것이다 — *"종의 값은 어떤 무공형태와 묶느냐가
        ///   정한다. 정체성은 수치가 아니라 2배 메커니즘이다."* 그렇다면 **하나의 숫자로 낼 수 없고,
        ///   무공형태별로 내야 한다.** 2026-08-05 에 공격 3 → 1 을 판정한 근거가 바로 이 축의
        ///   격차(공격 1 에서 28.0%p · 공격 3 에서 25.5%p)였는데, **그 측정이 상시 블록에 없었다.**
        /// </summary>
        private static void PrintDoublesFormByForm(int stage)
        {
            Console.WriteLine("     ── 종(宗)만 따로 — 무공형태에 기생하므로 형태별로 잰다 ──");

            // 무공형태 9자의 대표 5행(정직 · 중후 · 쾌 · 환궤 · 유변) 중 성격이 갈리는 넷.
            // ⚠ 패딩의 나머지 둘은 풍(자연)·참(공격방식)으로 고정한다 — 무공형태만 움직인다.
            char[] forms = { '정', '중', '쾌', '환', '유' };

            double best = double.MinValue;
            double worst = double.MaxValue;
            for (int i = 0; i < forms.Length; i++)
            {
                double v = PinnacleDuel('종', "풍" + forms[i] + "참", stage);
                Console.WriteLine("        무공형태 {0}   {1}", forms[i], Signed(v));
                Metrics.Add("sens.극한경지.종.무공형태" + forms[i] + "." + stage + "성", v * 100);
                if (v > best) best = v;
                if (v < worst) worst = v;
            }
            Console.WriteLine("        → 형태별 스프레드 {0:F1}%p  ⚠ 이 크기가 종의 정체성이다", (best - worst) * 100);
            Metrics.Add("sens.극한경지.종.형태별격차." + stage + "성", (best - worst) * 100);
        }

        /// <summary>
        /// 극한경지 글자 하나만 다른 **같은 길이 전승무학 4자** 한 쌍을 붙인다.
        ///
        /// ⚠ 대조군은 언제나 명(明) 자리다. <c>pinnacle == '명'</c> 이면 양쪽이 완전히 같은 표본이므로
        ///   <see cref="SymmetricAdvantage"/> 의 정의상 **정확히 0** 이 나온다 — 그것이 sanity 행이다.
        /// </summary>
        private static double PinnacleDuel(char pinnacle, string padding, int stage)
        {
            MartialArt art = TryMakeAttack(pinnacle + padding, ArtTier.Legacy);
            MartialArt control = TryMakeAttack("명" + padding, ArtTier.Legacy);
            if (art == null || control == null) return 0;

            return SymmetricAdvantage(
                ToCombatant(art, stage), ToCombatant(control, stage), SensitivityFights);
        }

        /// <summary>
        /// **절대경지 규칙 4종 — 2026-08-02 신설.**
        ///
        /// ⚠⚠ 넷 다 **내공 무공**이라 계층 승률표에 아예 등장하지 않는다. 내공 형태소가
        ///   그랬던 것과 같은 **측정 공백**이며, 저장소 교훈 *"측정하지 않는 축은 고장 나도 보이지
        ///   않는다"* 의 세 번째 사례가 되지 않도록 규칙을 붙이면서 함께 만든다.
        ///
        /// ⚠⚠ **무(無 기력 무소모)는 판정불가로 낸다.** 평타 전락률이 전 무공·전 경지 0.0% 라
        ///   아무도 기력이 마르지 않으므로 소모를 0 으로 만들어도 승률이 안 움직인다(HANDOFF §4-2-d).
        ///   0.00%p 를 "무효" 로 찍으면 **규칙이 고장 난 것처럼 읽힌다** — 고장이 아니라 **잴 수 없는 것**이다.
        ///   기력 축 제로섬(§4-2-O)이 풀리면 자동으로 진짜 수치가 나온다.
        ///
        /// ⚠ 통(統 상성 절대우위)은 상대가 **문파 소속(무학분류 보유)** 이어야 방어 측 효과가 드러난다.
        ///   공격 측 +2 는 무소속 상대에게도 붙으므로(*"모든 분류에"*) 대조군은 무소속으로 둔다.
        /// </summary>
        private static void PrintAbsoluteRuleSensitivity(int stage)
        {
            Console.WriteLine();
            Console.WriteLine("  ── 절대경지 규칙 (규칙 글자만 뺀 같은 무공 대비) ──");

            // ⚠⚠ **대조군은 "안 배운 쪽" 이 아니라 "규칙 글자만 뺀 같은 무공" 이다.**
            //   처음에 안 배운 쪽과 비교했다가 틀렸다 — 절대경지 무공은 규칙 글자 외에 **성능 형태소
            //   3자**를 더 갖는다. `음쌍쾌신` 은 쾌(속도+2)+신(속도+1)로 속도가 +3 이고,
            //   `식무유수`·`합통현유` 는 유(柔變 속도−2)를 물고 있다. 보조 무공의 속도는 사람에게
            //   합산되므로(§3-2) 속공 추가 행동이 통째로 흔들린다 — 실측 무(無)가 **−16.00%p** 로
            //   나온 것이 그 때문이었지 규칙이 손해라서가 아니다.
            //   → 3자 대조군(`음쾌신` 등)을 대문파 내공으로 세워 **규칙만 남긴다.**
            string[][] rules =
            {
                new[] { "정면합광", "정합광", "면 면역" },
                new[] { "음쌍쾌신", "음쾌신", "쌍 2회행동" },
                new[] { "합통현유", "합현유", "통 상성우위" },
                // ⚠⚠ **무(無) 행만 패딩이 카탈로그 이름과 다르다** (2026-08-02).
                //   원래 `식무유수`(천마신교 카탈로그) vs `식유수` 였는데 **대조군이 스스로 압력을
                //   지우고 있었다** — 식(息)이 `qiCostPercent −10` 이라 양쪽 다 전락률 0.0% 가 되고,
                //   **둘 다 안 마르면 무소모는 잴 것이 없다**(`diagnosis` 규명 · HANDOFF §4-3-8).
                //   → 내공 형태소 넷 중 **양(陽)만 `maxQi` 만 건드려 턴당 순기력을 안 바꾼다**
                //     (음 회복+2 · 합 회복+0.5 · 식 소모−10% 는 전부 수지를 바꾼다).
                //     양은 **버퍼만 키우므로 전락이 늦춰질 뿐 사라지지 않는다.**
                //   ⚠ 그래서 이 행이 재는 것은 **카탈로그 무공 `식무유수` 가 아니라 규칙 무(無) 자체**다.
                //     카탈로그 무공은 자기 식(息) 때문에 이 이점을 스스로 깎는다 — 별건(§4-5).
                //
                // ⚠⚠ **그리고 식(息)만 바꾼 1차 수정은 절반만 맞았다** — 남은 패딩 **수(水)** 가
                //   `qiRegen +1` 인데 **숙련 배율(만렙 정파 ≈2.15배)이 곱해져 정확히 +2** 가 된다.
                //   회복 10+2 = **12** 가 공격 무공 `참정독명`(4자) 소모 **12** 와 정확히 같아져
                //   대조군이 영원히 안 마른다(실측 A·B 둘 다 전락률 0.0%). 같은 병의 재발이다.
                //   → **수(水) → 화(火).** 화는 `attack +0.6` 뿐인데 **보조 무공의 공격은 엔진이
                //     아예 안 읽으므로**(HANDOFF §3-2 실측) 양쪽에게 문자 그대로 아무것도 안 준다.
                //   ⚠ 사전 전수 확인: 기력 축을 건드리는 글자는 **내공 4자 + 수(水) + 선(仙) + 만(萬)**
                //     이 전부다(`qiRegen|qiCostPercent|maxQi` grep). 나머지는 패딩으로 안전하다.
                new[] { "양무유화", "양유화", "무 무소모" },
            };

            // ⚠⚠ **값은 `SymmetricAdvantage` 로 낸다** — 0 이 진짜 0 이다. 기준선을 빼지 마라.
            // ⚠⚠ **상대를 세 종류로 나눠 잰다** (2026-08-02 2차). 한 종류로만 재면 틀린다 —
            //   면(免)·통(統)은 **조건부 규칙**이라 상대에 따라 가치가 0 에서 절대적까지 오간다.
            //   극한경지 선(仙)이 탈(奪) 대전에서만 살아난 것과 같은 구조다(§4-2-X).
            //   평균 하나로 재면 "쌍이 세다" 로 보이지만, 실은 **셋이 조건부이고 쌍만 무조건**일 수 있다.
            Console.WriteLine("     {0,-12}{1,12}{2,12}{3,12}", "", "무해", "상태이상", "상성");
            for (int i = 0; i < rules.Length; i++)
            {
                // ⚠⚠ **2026-08-02 — 무해 열의 공격 무공을 `참정`(2자) → `참정독명`(4자) 로 바꿨다.**
                //   무(無 기력 무소모)를 재려면 **재는 쪽이 기력을 쓰긴 써야** 한다. 참정은 2자라
                //   기력 6 < 회복 10 이라 시작 기력을 어떻게 잡아도 안 마르고, 그러면 소모를 0 으로
                //   만들어도 승률이 안 움직인다 — 규칙이 고장 난 게 아니라 **잴 수 없는 자리에
                //   세워 뒀던 것**이다. 자세한 경위는 `PrintInnerArtSensitivity` 의 같은 날 주석.
                double plain = AbsoluteDuel(rules[i][0], rules[i][1], stage, "참정독명", null);
                double status = AbsoluteDuel(rules[i][0], rules[i][1], stage, "참정독", null);
                double counter = AbsoluteDuel(rules[i][0], rules[i][1], stage, "창천낙월", ArtLineage.Yin);

                // ⚠⚠ **무(無)가 0.00 인 것은 "효과 없음" 도 "고장" 도 아니라 이 표의 대조군 결함이다**
                //   (2026-08-02 `diagnosis` 규명). 엔진은 정상이다 — 전투 로그에서 무(無) 보유 쪽은
                //   `(기력 -N)` 표기가 실제로 사라지고 대조군은 매 턴 찍힌다.
                //   원인은 **대조군 `식유수` 의 식(息)** 이다: `qiCostPercent −10` · `maxQi +3` 이라
                //   **대조군 자신이 이미 기력 할인을 받아** 압력이 0 이 된다(실측: 참정독명 단독 11.8% →
                //   식유수를 붙이면 A·B 둘 다 0.0%). **양쪽 다 안 마르면 무소모는 잴 것이 없다.**
                //   ⛔ 고치려면 식(息) 아닌 내공 형태소로 패딩을 갈아야 하는데, 조합 규칙상 내공 무공은
                //     Internal 1자가 필수이고 **양(maxQi+10)·음(qiRegen+2)·합(둘 다) 도 전부 압력을 줄인다.**
                //     즉 **완전히 중립인 Internal 형태소가 없을 수 있다** → 설계 판단이 필요하므로
                //     `verify` 없이 손대지 않는다(§5-C). HANDOFF §4-3-8 미결 목록.
                string tail = rules[i][0] == "양무유수"
                    ? "  ⚠ 패딩을 양(陽)으로 바꿔 잰다 (식息은 압력을 지운다 · §4-3-8)"
                    : "";
                Console.WriteLine("     {0,-12}{1}{2}{3}{4}",
                    rules[i][2], Signed(plain), Signed(status), Signed(counter), tail);
            }
            Console.WriteLine("     ⚠ 무해=상태이상 없는 상대 · 상태이상=독 보유 · 상성=낙월(음기 상성) 보유");

            PrintDoubleActionByQiCost(stage);
            PrintNoQiCostAsAttackArt(stage);
        }

        /// <summary>
        /// **㉯ 실험 — 무(無)를 공격 무공으로 만들면 어떤가** (2026-08-04 사용자 지시로 측정).
        ///
        /// ⚠⚠ **이것은 측정이지 채택이 아니다. 카탈로그는 건드리지 않았다.**
        ///
        /// 문제 — 천마신교 절대경지 `식무유수`는 **4자 중 2자가 죽어 있다**(HANDOFF §4-5-4).
        ///   무(無)를 가진 사람은 `EffectiveQiCost`가 0을 반환하므로 기력 경제 글자가 전부 무의미해지는데,
        ///   조합 규칙이 **내공 무공에 내공 형태소 1자를 필수**로 요구하고 그 넷이 전부 기력 경제다.
        ///   → **개명으로는 못 푼다.**
        ///
        /// ㉯ 안 — **공격 무공으로 만들면** 필수가 공격방식·무공형태로 바뀌어 **죽는 글자가 0**이 된다.
        ///   ✅ 규칙상 막혀 있지 않다: 조합 규칙이 강제하는 것은 *"규칙 형태소는 절대경지 **계층**에만"*
        ///     이지 종류가 아니고, 정의서 §5-3-a 도 *"규칙은 이름의 **둘째 자리**"* 라는 작명 규칙뿐이다.
        ///     넷이 전부 내공인 것은 **카탈로그의 선택**이다.
        ///   ⚠ 대가 — 4종 중 하나만 공격이 되어 대칭이 깨지고, **기력 0짜리 4자 공격 무공**이
        ///     계층 승률표를 지배할 수 있다. **그 지배 여부를 재는 것이 이 블록의 목적이다.**
        ///
        /// 덤 — 지금 절대경지 4종은 **전부 내공이라 계층 승률표에 아예 안 나온다**(§3-2 측정 공백).
        ///   공격 무공이면 표에 들어와 직접 측정된다.
        /// </summary>
        private static void PrintNoQiCostAsAttackArt(int stage)
        {
            Console.WriteLine();
            Console.WriteLine("  ── ㉯ 실험: 무(無)를 공격 무공으로 (카탈로그 미반영) ──");

            // 후보 이름 — 둘째 자리가 규칙 글자여야 한다(§5-3-a). 공격 무공은 공격방식·무공형태 필수.
            //
            // ⚠⚠ **대조군을 3자로 두면 정확히 0 이 나온다. 그건 무(無)가 무용해서가 아니다.**
            //   3자 무공은 기력 9 < 회복 10 이라 **애초에 마르지 않는다** — 절약할 것이 없다.
            //   같은 병을 오늘만 세 번째 밟았다(식息 · 수水 · 여기). HANDOFF §4-3-6 일반화 6.
            //   → **결정에 필요한 비교는 4자 대문파와의 대결**이다. 절대경지가 실제로 밀어낼 상대이고,
            //     4자는 기력 12 > 회복 10 이라 **전락률 11.8% 로 압력을 받는다.**
            //   ⚠ 규칙 글자가 슬롯을 하나 먹으므로 무(無) 공격 무공의 **성능 형태소는 3자뿐**이다.
            //     즉 *"성능 3 + 무소모"* 대 *"성능 4 + 전락 11.8%"* 의 교환이다.
            string[][] pairs =
            {
                new[] { "정무참광", "정참광",   "3자 대조(압력 없음 — 0 이 정상)" },
                new[] { "정무참광", "참정독명", "**4자 대문파 — 지배 판정은 이쪽**" },
                new[] { "중무격명", "참정독명", "**4자 대문파 — 지배 판정은 이쪽**" },
            };

            for (int i = 0; i < pairs.Length; i++)
            {
                MartialArt withRule = TryMakeAttack(pairs[i][0], ArtTier.Absolute);
                MartialArt control = TryMakeAttack(pairs[i][1], ArtTier.Major);
                if (withRule == null || control == null) continue;

                double adv = SymmetricAdvantage(
                    ToCombatant(withRule, stage), ToCombatant(control, stage), SensitivityFights);
                Console.WriteLine("     {0,-10} vs {1,-10} {2}   {3}",
                    pairs[i][0], pairs[i][1], Signed(adv), pairs[i][2]);
            }
        }

        /// <summary>측정용 공격 무공을 만든다. 실패하면 **조용히 넘기지 않고 사유를 찍는다.**</summary>
        private static MartialArt TryMakeAttack(string name, ArtTier tier)
        {
            IReadOnlyList<string> problems;
            MartialArt art;
            MartialArtFactory.TryCreate("x_" + name, name, ArtKind.Attack, tier,
                Discipline.Sword, Alignment.Demonic, "천마신교", 1, null, out art, out problems);
            if (art == null)
            {
                Console.WriteLine("     ⛔ 생성 실패 " + name + " — " + string.Join(" · ", problems));
            }
            return art;
        }

        /// <summary>
        /// **쌍(雙) 의 값이 공격 무공의 기력비용에 얼마나 의존하는가 — 2026-08-02 신설.**
        ///
        /// ⚠⚠ **왜 이 블록이 필요한가.** 같은 날 §5-3-b ④ 에서 쌍은 세 상대에게
        ///   `+11.62 / +11.00 / +11.38` 로 **무조건·균일**이었다. 그런데 시작 기력을 25% 로
        ///   낮추자(<see cref="CombatResolver.StartingQiDivisor"/>) 설정이 그대로인 두 열이
        ///   `+6.50 / +3.06` 으로 내려앉았고, 4자 압력에서는 **−7.66%p** 로 부호가 뒤집혔다.
        ///
        /// ⚠ **위 표로는 원인을 분리할 수 없다** — 세 열이 *상대 종류*와 *공격 무공*을 **동시에**
        ///   바꾸기 때문이다(무해=참정독명 4자 · 상태이상=참정독 3자 · 상성=창천낙월).
        ///   HANDOFF §4-3-6 실수 #4(*"하나만 다르다를 믿기 전에 정말 하나만 다른지 센다"*) 그대로다.
        ///   → **상대를 무해로 고정하고 공격 무공의 글자 수만 늘린다.** 다른 것은 기력비용뿐이다.
        ///
        /// 가설 — 쌍은 **행동마다 기력을 다시 쓴다.** 소모가 회복을 넘는 순간(4자: 12×2=24 vs 회복 10)
        ///   2회 행동이 곧 2배 고갈이 되어 **스스로 평타로 내려앉는다.** 그러면 쌍은 *"강한 규칙"* 이
        ///   아니라 *"싼 무공에서만 강한 규칙"* 이고, 절대경지 사용자가 쓸 무공은 대개 4자다.
        /// </summary>
        private static void PrintDoubleActionByQiCost(int stage)
        {
            Console.WriteLine();
            Console.WriteLine("  ── 쌍(雙) × 공격 무공 기력비용 (상대는 무해로 고정) ──");
            Console.WriteLine("     ⚠ 상대를 고정하고 **글자 수만** 늘린다. 바뀌는 것은 기력비용뿐이다");

            string[][] attacks =
            {
                new[] { "참정", "2자 · 기력 6" },
                new[] { "참정독", "3자 · 기력 9" },
                new[] { "참정독명", "4자 · 기력 12" },
            };

            for (int i = 0; i < attacks.Length; i++)
            {
                double v = AbsoluteDuel("음쌍쾌신", "음쾌신", stage, attacks[i][0], null);
                Console.WriteLine("     {0,-16}{1}", attacks[i][1], Signed(v));
            }
        }

        /// <summary>
        /// 공격 무공과 **성능 형태소 3자를 같게 두고 규칙 글자만** 다르게 해 승률 차를 낸다.
        /// 대조군은 같은 3자를 대문파 내공 무공으로 세운 것이다 — 규칙 형태소는 절대경지 전용이라
        /// 대조군에는 넣을 수 없고, 그래서 **규칙 하나만 남는다.**
        /// </summary>
        private static double AbsoluteDuel(
            string absoluteName, string controlName, int stage, string attackName, ArtLineage? selfLineage)
        {
            MartialArt attack = TryBuild(attackName);
            if (attack == null) return 0;

            IReadOnlyList<string> problems;
            MartialArt absolute, control;
            MartialArtFactory.TryCreate("s_" + absoluteName, absoluteName, ArtKind.Internal,
                ArtTier.Absolute, Discipline.InnerArt, null, null, 1, null, out absolute, out problems);
            MartialArtFactory.TryCreate("c_" + controlName, controlName, ArtKind.Internal,
                ArtTier.Major, Discipline.InnerArt, Alignment.Orthodox, "화산파", 1, null, out control, out problems);
            // ⚠⚠ **조용히 0 을 반환하지 않는다** (2026-08-02). 생성 실패와 *"효과 없음"* 이 **같은
            //   0.00 으로 나오면 구분할 수 없다.** `diagnosis` 가 무(無) 진단에서 가장 먼저 의심한
            //   함정이 이것이다(HANDOFF §4-3-8). 실패는 실패라고 말해야 한다.
            if (absolute == null || control == null)
            {
                Console.WriteLine("     ⛔ 무공 생성 실패: " + absoluteName + " / " + controlName
                                  + " — " + string.Join(" · ", problems));
                return 0;
            }

            // ⚠ 양쪽이 같은 공격 무공·같은 분류다. 다른 것은 **규칙 글자 하나**뿐이다.
            //   상성 대조에서는 양쪽 다 음기(Yin)로 두어 낙월(음기 상성)이 서로에게 걸리게 한다 —
            //   그래야 통(統)의 *"상대 상성 무효"* 가 실제로 일할 자리가 생긴다.
            return SymmetricAdvantage(
                WithInner(attack, absolute, stage, selfLineage),
                WithInner(attack, control, stage, selfLineage),
                SensitivityFights);
        }

        private static string Signed(double delta)
        {
            double p = delta * 100;
            string body = (p >= 0 ? "+" : "") + p.ToString("F2") + "%p";
            if (p > -0.5 && p < 0.5) body += " ⚠무효";
            return body.PadLeft(9);
        }

        /// <summary>
        /// 공격 무공은 같게 두고 **내공 무공 유무만** 다르게 해 앞쪽의 **순수 우위**(%p, 0 이 대등)를 낸다.
        ///
        /// ⚠ 반환값이 승률이 아니라 우위다(2026-08-02 변경). <c>innerName == null</c> 이면 양쪽이
        ///   완전히 같은 표본이므로 <see cref="SymmetricAdvantage"/> 의 정의상 **정확히 0** 이 나온다 —
        ///   그것이 표의 sanity 행이다.
        /// </summary>
        private static double InnerDuel(string attackName, string innerName, int stage)
        {
            MartialArt attack = TryBuild(attackName);
            if (attack == null) return 0;

            MartialArt inner = null;
            if (innerName != null)
            {
                IReadOnlyList<string> problems;
                MartialArtFactory.TryCreate("s_" + innerName, innerName, ArtKind.Internal, ArtTier.Major,
                    Discipline.InnerArt, Alignment.Orthodox, "화산파", 1, null, out inner, out problems);
                if (inner == null) return 0;
            }

            return SymmetricAdvantage(
                WithInner(attack, inner, stage), WithInner(attack, null, stage), SensitivityFights);
        }

        private static Combatant WithInner(
            MartialArt attack, MartialArt inner, int stage, ArtLineage? lineage = null)
        {
            int sessions = AlignmentCurve.SessionsToReach(
                Alignment.Orthodox, MartialStage.ProficiencyForStage(stage));

            var arts = new List<LearnedArt> { new LearnedArt(attack, sessions, Alignment.Orthodox) };
            if (inner != null) arts.Add(new LearnedArt(inner, sessions, Alignment.Orthodox));

            var masteries = new List<DisciplineMastery>
            {
                new DisciplineMastery(attack.Discipline, DisciplineCurve.SessionsToMaster(attack.Discipline)),
            };
            return new Combatant("내공표본", CharacterStats.MaxLevel(), new Loadout(arts.ToArray()), masteries, lineage);
        }

        /// <summary>
        /// **평타 전락률 — 기력 설계의 판정 기준** (2026-08-01 신설).
        ///
        /// ⚠⚠ 설계안 §5-3 이 *"기력 부족으로 초식을 못 쓴 비율, 목표 10~30%"* 를 못박고
        ///   <see cref="MorphemeParser.QiCostPerMorpheme"/> 주석도 *"이 상수는 전락률로 판정한다"* 고
        ///   적어 뒀는데 **재는 코드가 없었다.** 그래서 2026-08-01 에 상수를 4 ↔ 3 으로 놓고
        ///   두 번 논쟁하는 동안 근거가 전부 **산술 추정**이었다.
        ///   → HANDOFF §5 의 *"밸런싱이 막혔을 때 원인이 값이 아니라 표현력일 수 있다"* 와 같은 자리다.
        ///   여기서는 표현력이 아니라 **관측 자체가 없었다.**
        ///
        /// ⚠ 0% 면 기력 축이 죽은 것이다 — 내공 형태소(양·음·합·식)와 기력소실 탈(奪)이
        ///   **동시에 존재 이유를 잃는다.** 30% 를 넘으면 반대로 평타 싸움이 된다.
        /// ⚠ 유형별로도 잰다. 권(拳)의 특성이 기력 소모 감소이므로, 권만 낮게 나오는 것이 정상이다.
        /// </summary>
        private static void PrintQiPressure(int stage)
        {
            Console.WriteLine();
            Console.WriteLine("  ── 평타 전락률 (목표 10~30% · 설계안 §5-3) — 같은 무공끼리 "
                              + FightsPerMatchup + "전 ──");

            foreach (string name in new[] { "참정", "참정독", "참정독명" })
            {
                MartialArt art = TryBuild(name);
                if (art == null) continue;

                double rate = MirrorBasicStrikeRate(art, stage);
                Metrics.Add("qi.전락률." + name.Length + "자." + stage + "성", rate);
                string flag = rate <= 0.05 ? "  ⚠⚠ 기력 축이 죽어 있다"
                    : rate < 10 ? "  ⚠ 목표 미만"
                    : rate > 30 ? "  ⚠ 목표 초과" : "  ✅";
                Console.WriteLine("     " + Pad(name + "(" + name.Length + "자 · 기력 " + art.QiCost + ")", 22)
                                  + rate.ToString("F1").PadLeft(5) + "%" + flag);
            }

            Console.WriteLine("     ── 4자 무공을 유형별로 ──");
            foreach (var kind in new[]
            {
                new KeyValuePair<string, Discipline>("검", Discipline.Sword),
                new KeyValuePair<string, Discipline>("도", Discipline.Blade),
                new KeyValuePair<string, Discipline>("창", Discipline.Spear),
                new KeyValuePair<string, Discipline>("권", Discipline.Fist),
                new KeyValuePair<string, Discipline>("비도", Discipline.Dagger),
            })
            {
                MartialArt art = TryBuild("참정독명", kind.Value);
                if (art == null) continue;
                Metrics.Add("qi.전락률.4자." + kind.Key + "." + stage + "성", MirrorBasicStrikeRate(art, stage));
                Console.WriteLine("     " + Pad(kind.Key, 22)
                                  + MirrorBasicStrikeRate(art, stage).ToString("F1").PadLeft(5) + "%");
            }
        }

        /// <summary>같은 무공끼리 붙여 양쪽 합산 평타 전락률의 평균을 낸다.</summary>
        private static double MirrorBasicStrikeRate(MartialArt art, int stage)
        {
            Combatant a = ToCombatant(art, stage);
            Combatant b = ToCombatant(art, stage);

            double sum = 0;
            for (uint seed = 1; seed <= FightsPerMatchup; seed++)
            {
                sum += CombatResolver.Resolve(a, b, new XorShiftRandom(seed)).BasicStrikeRate;
            }
            return sum / FightsPerMatchup;
        }

        /// <summary>
        /// **형태소 개수 민감도 — 계층의 대리 실험** (2026-07-31 신설).
        ///
        /// ⚠⚠ 계층은 곧 형태소 개수다(강호 2 · 소문파 3 · 대문파 3~4). 그런데 계층 승률표는
        ///   무공마다 글자 구성이 달라 **개수만의 효과를 분리하지 못한다.**
        ///   여기서는 **앞 글자를 그대로 두고 뒤에만 덧붙여** 개수 하나만 바꾼다.
        ///
        /// 기력 소모 = 글자 수 × <see cref="MorphemeParser.QiCostPerMorpheme"/>(현재 **3**) 이고
        /// 회복이 턴당 <c>CombatResolver.BaseQiRegen</c>(10) 이므로:
        ///   2자 = 6(턴당 +4) · 3자 = 9(+1) · 4자 = 12(−2)
        /// → **글자가 늘수록 능력은 하나 늘고 기력은 그보다 빨리 마른다.** 그 순손익을 잰다.
        ///
        /// ⚠⚠ 위 숫자는 상수 **3** 기준이다(2026-08-01 정정). 그전에는 ×4 기준(8/12/16)이 적혀
        ///   있었는데 커밋 9605db8 이 상수를 내리면서 이 주석을 안 고쳤다. 실제 압력이 얼마인지는
        ///   <see cref="PrintQiPressure"/> 가 재는 **평타 전락률**로 본다 — 산술로 추정하지 않는다.
        /// </summary>
        private static void PrintMorphemeCountSensitivity(int stage)
        {
            Console.WriteLine();
            Console.WriteLine("  ── 형태소 개수 (계층 대리) — 앞 글자 고정, 뒤에만 덧붙임 ──");

            var names = new[] { "참정", "참정독", "참정독명" };
            for (int i = 0; i < names.Length; i++)
            {
                for (int j = i + 1; j < names.Length; j++)
                {
                    double rate = Duel(names[i], names[j], stage) * 100;
                    Console.WriteLine("     " + Pad(names[i] + "(" + names[i].Length + "자) vs "
                                                  + names[j] + "(" + names[j].Length + "자)", 26)
                                      + rate.ToString("F1").PadLeft(6) + "%"
                                      + (rate > 50 ? "  ⚠ 짧은 쪽이 이긴다" : ""));
                }
            }
        }

        /// <summary>
        /// **유형(무기) 민감도** — 같은 무공명을 다섯 무기로 들려 붙인다 (2026-07-31 신설).
        ///
        /// ⚠⚠ 이 측정이 없어서 **계층 내 격차의 주범을 형태소라고 오해하고 있었다.**
        ///   대문파 44종 순위표에서 상위 6종이 전부 검(劍)이었는데, 형태소 민감도표는
        ///   검으로 고정해 재고 있었으므로 그 사실이 보이지 않았다.
        ///
        /// ⚠ 유형 숙달은 전부 **10성 고정**이다. 재는 것은 *"숙달했을 때 무엇을 얻는가"* 이지
        ///   *"얼마나 빨리 숙달하는가"* 가 아니다 — 후자는 학습률(백일창·천일도·만일검)이고
        ///   그건 시간 비용이라 승률로 환산되지 않는다.
        /// ⚠ 상태이상 유무로 두 번 잰다. 비도의 특성(상태이상 확률 +30%p)은 상태이상 형태소를
        ///   넣은 무공에서만 값을 하기 때문이다.
        /// </summary>
        private static void PrintDisciplineSensitivity(int stage)
        {
            Console.WriteLine();
            Console.WriteLine("  ── 유형(무기) — 같은 무공을 다섯 무기로 " + SensitivityFights + "전 ──");

            var kinds = new[]
            {
                new KeyValuePair<string, Discipline>("검", Discipline.Sword),
                new KeyValuePair<string, Discipline>("도", Discipline.Blade),
                new KeyValuePair<string, Discipline>("창", Discipline.Spear),
                new KeyValuePair<string, Discipline>("권", Discipline.Fist),
                new KeyValuePair<string, Discipline>("비도", Discipline.Dagger),
            };

            // ⚠⚠ 2자(참정)는 기력 8 인데 회복이 10 이라 **기력이 절대 마르지 않는다** — 권의 특성이
            //   측정에서 통째로 0 이 된다. 4자(기력 16)를 반드시 함께 재야 한다.
            foreach (string name in new[] { "참정", "참정독", "참정독명" })
            {
                Console.WriteLine("     [" + name + "]");
                var rows = new List<KeyValuePair<string, double>>();
                for (int i = 0; i < kinds.Length; i++)
                {
                    double sum = 0;
                    int n = 0;
                    for (int j = 0; j < kinds.Length; j++)
                    {
                        if (i == j) continue;
                        MartialArt a = TryBuild(name, kinds[i].Value);
                        MartialArt b = TryBuild(name, kinds[j].Value);
                        if (a == null || b == null) continue;
                        sum += WinRate(ToCombatant(a, stage), ToCombatant(b, stage), SensitivityFights);
                        n++;
                    }
                    if (n > 0) rows.Add(new KeyValuePair<string, double>(kinds[i].Key, sum / n));
                }
                Report(rows, "유형." + name, stage);
            }
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
            Report(rows, label, stage);
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
            Report(rows, label, stage);
        }

        private static void Report(List<KeyValuePair<string, double>> rows)
        {
            Report(rows, null, 0);
        }

        /// <summary>
        /// ⚠⚠ `label`/`stage` 를 받는 판은 **지표를 함께 수집한다**(2026-08-05). 표를 눈으로만 읽던
        ///   것이 선택적 관찰의 원인이었으므로, 찍는 자리에서 바로 <see cref="Metrics"/> 에 넣는다.
        ///
        /// ⚠⚠ **2026-08-05 2차 — 이 판을 만들어 놓고 아무도 호출하지 않고 있었다.**
        ///   민감도표 7종(공격방식·무공형태·방어·수식·자연속성·상태이상·유형)이 전부 인자 없는
        ///   <see cref="Report(List{KeyValuePair{string,double}})"/> 를 불러서, **기준선 124개 지표에
        ///   `sens.*` 키가 하나도 없었다.** 즉 `--compare` 가 형태소 축을 통째로 못 봤다.
        ///   → §5-D 가 *"안 본 지표를 도구가 들이밀게"* 만든 장치인데 **그 도구에 같은 구멍이 있었다.**
        ///   실제 피해: §4-6-8(공격 페널티 분리) 이후 무공형태 격차가 **9.4 → 16.3%p** 로 벌어졌는데
        ///   그 변화가 어느 `--compare` 보고에도 뜨지 않았다.
        ///   ⚠ 그래서 이 오버로드는 **호출되는지까지가 기능**이다. 새 민감도표를 만들면 반드시 라벨을 넘긴다.
        /// </summary>
        private static void Report(List<KeyValuePair<string, double>> rows, string label, int stage)
        {
            if (label != null)
            {
                rows.Sort((x, y) => y.Value.CompareTo(x.Value));
                for (int i = 0; i < rows.Count; i++)
                {
                    Metrics.Add("sens." + label + "." + rows[i].Key + "." + stage + "성", rows[i].Value * 100);
                }
                if (rows.Count >= 2)
                {
                    Metrics.Add("sens." + label + ".격차." + stage + "성",
                        (rows[0].Value - rows[rows.Count - 1].Value) * 100);
                }
            }

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

        private static MartialArt TryBuild(string name)        {            return TryBuild(name, Discipline.Sword);        }                private static MartialArt TryBuild(string name, Discipline discipline)        {
            MartialArt art;
            IReadOnlyList<string> problems;
            // 대문파급·정파·검으로 고정한다 — 비교 대상이 형태소 하나뿐이어야 하므로 나머지는 전부 같게 둔다.
            MartialArtFactory.TryCreate(
                "s_" + name, name, ArtKind.Attack, ArtTier.Major,
                discipline, Alignment.Orthodox, "화산파", 1, null, out art, out problems);
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
            // ⚠⚠ 규칙 본체는 **Core 의 `CombatantBuilder`** 로 옮겼다 (2026-08-23). 전투 화면이
            //   같은 대전자를 만들어야 하는데, 여기 private 로 두면 그쪽이 규칙을 다시 쓰게 되고
            //   **언젠가 갈라진다** — 이 저장소가 다섯 번 겪은 형태다(그쪽 주석에 목록이 있다).
            //   옮기면서 동작은 한 톨도 바꾸지 않았고, 기준선 `--compare` 로 확인했다.
            return CombatantBuilder.Build(art, stage, stats);
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

        /// <summary>
        /// **A 가 B 에 대해 갖는 순수 우위(%p, 0 이 대등).** 자리를 바꿔 두 번 재고 평균낸다.
        ///
        /// ⚠⚠ **2026-08-02 신설 — 기준선을 빼는 방식을 폐기하고 이걸로 대체했다.**
        ///   그전에는 `WinRate(A, B) − 0.5` 를 쓰고, 남는 편향을 *"효과가 0 인 규칙 행"* 으로
        ///   추정해 빼려 했다. **그 가정이 틀렸다** — 편향은 **쌍마다 다르다.**
        ///   실측: 규칙 효과가 0 인 상황인데 무(無) 쌍은 −3.75%p, 통(統) 쌍은 +0.25%p 였다.
        ///   두 쌍의 무공이 통째로 다르기 때문이다(`식유수` vs `합현유`).
        ///
        /// **왜 이 식이 편향을 지우는가** — 선공은 `Initiative` 동률일 때 난수가 가르는데,
        /// 자리를 바꾸면 **A 가 이득 본 만큼 B 가 이득을 본다.** 그래서 상쇄된다.
        /// ✅ **A 와 B 가 완전히 같으면 결과가 정의상 정확히 0 이다**(`x + (1−x) = 1`).
        ///   즉 **뺄셈이 사라진다** — 0 이 진짜 0 이라 보정할 것이 없다.
        ///
        /// ⚠ 대가는 측정 2배다. 대상이 적은 블록에만 쓴다(계층 승률표는 138×138 이라 못 쓴다).
        /// ⚠ 이 블록 밖의 민감도표들은 아직 옛 방식이다 — 같은 편향을 갖는다. 별건.
        /// </summary>
        private static double SymmetricAdvantage(Combatant a, Combatant b, int fights)
        {
            double forward = WinRate(a, b, fights);
            double backward = WinRate(b, a, fights);
            return (forward + (1.0 - backward)) / 2.0 - 0.5;
        }

        /// <summary>한 계층 안에서 전수 대전을 돌려 순위를 낸다.</summary>
        private static void PrintRanking(List<MartialArt> all, ArtTier tier, int stage)
        {
            // ⚠⚠ **범위 무공을 토너먼트에서 뺀다** (2026-08-08 사용자 확정 · `verify` 통과).
            //   그전에는 범위 무공을 **참가시킨 뒤 격차·σ 계산에서만** 뺐다(아래 `live`).
            //   그것으로는 **1차 오염(범위 무공 자신의 승률)만** 지워지고
            //   **2차 오염 — 다른 무공이 그 샌드백을 이겨서 얻은 승률 — 은 그대로 남는다.**
            //
            //   실측(10성, 이 변경 전후):
            //     · 대문파  평균±2σ 39.5~65.7 ⚠ → **36.7~63.0 ✅** (평균 52.7 → 49.9)
            //     · 전승    38.3~74.9 ⚠ → 29.0~71.0 ⚠ (σ 9.1 → 10.5)
            //     · `마한중참` 67.1 → **62.0** — 지배 의심이 사라진다
            //     · `황야환투` 37.4 → **28.0 ⚠ 죽은 선택지** — 더 심한 샌드백(`만우쾌사` 3.7)에 가려져 있었다
            //   → **오염이 양방향이다**: 상단을 부풀리고(모두가 샌드백을 이긴다),
            //     동시에 약한 무공을 가린다(더 심한 것이 밑에 깔려 있으면 덜 나빠 보인다).
            //
            //   ⚠ 강호무학·소문파는 영향이 없다. 우연이 아니라 `ArtCompositionRule` 이
            //     **범위 형태소를 대문파 이상으로 제한**하기 때문이다.
            //   ⚠⚠ **이것은 "미래의 참값" 이 아니다.** 다대다가 붙으면 범위 무공은 사라지는 것이
            //     아니라 **경쟁력 있는 상태로 토너먼트에 돌아온다.** 지금 재는 것은
            //     *"샌드백 왜곡의 크기"* 이지 *"범위 무공이 제 몫을 했을 때의 값"* 이 아니다.
            //   ⚠ 범위 무공을 **버리지 않는다** — 아래에서 깨끗한 집단을 상대로 따로 재서 낸다.
            //     승률에는 손대지 않는다. Phase 3 가 붙으면 판정 보류를 풀고 다시 잰다.
            var group = new List<MartialArt>();
            var scoped = new List<MartialArt>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Tier != tier) continue;
                if (HasScopeMorpheme(all[i])) scoped.Add(all[i]); else group.Add(all[i]);
            }
            if (group.Count < 2) return;

            Console.WriteLine();
            Console.WriteLine("── " + TierName(tier) + " (" + group.Count + "종"
                              + (scoped.Count > 0 ? " · 범위 " + scoped.Count + "종은 따로" : "") + ") "
                              + new string('─', 36));

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

            // ⚠ `live` 는 이제 `rows` 와 같다 — 범위 무공이 애초에 `group` 에 안 들어오기 때문이다.
            //   그래도 이름을 남겨 둔다: 아래 σ 블록이 *"판정에 쓰는 값들"* 이라는 뜻으로 읽는다.
            var live = new List<double>();

            for (int i = 0; i < rows.Count; i++)
            {
                MartialArt a = rows[i].Key;
                live.Add(rows[i].Value);

                // ⚠⚠ **공격 합을 함께 찍는다** (2026-08-05). 순위표에 승률만 있어서 *"왜 이 무공이
                //   바닥인가"* 를 물을 때마다 이름을 손으로 분해하고 있었다. 형태소 합은 파서가
                //   이미 갖고 있으므로 **눈으로 세지 말고 도구가 내게** 한다(§5-D 와 같은 선).
                //   ⚠ 이 값은 **형태소 합이지 실제 피해가 아니다** — 성향 숙련 배율·유형 숙달·
                //     캐릭터 공격(만렙 8)이 그 위에 얹힌다. 무공끼리 비교하는 용도로만 읽을 것.
                string school = a.IsWandererArt ? "(강호무학)" : a.School;
                Console.Write("  " + (i + 1).ToString().PadLeft(2) + "  "
                              + Pad(a.Name, 26) + Pad(school, 12)
                              + Pad(Short(a.Discipline) + "·" + Short(a.Alignment), 8)
                              + (rows[i].Value * 100).ToString("F1").PadLeft(6) + "%"
                              + "  공" + a.Delta.Attack.ToString("F2").PadLeft(5));
                if (rows[i].Value >= DominantThreshold) Console.Write("  ⚠ 지배 의심");
                else if (rows[i].Value <= DeadThreshold) Console.Write("  ⚠ 죽은 선택지 의심");
                Console.WriteLine();
            }

            double gap = (rows[0].Value - rows[rows.Count - 1].Value) * 100;
            Metrics.Add("tier." + TierName(tier) + ".격차전체." + stage + "성", gap);
            Metrics.Add("tier." + TierName(tier) + ".최고." + stage + "성", rows[0].Value * 100);
            Metrics.Add("tier." + TierName(tier) + ".최저." + stage + "성", rows[rows.Count - 1].Value * 100);
            Console.WriteLine("  → 계층 내 격차(전체) " + gap.ToString("F1") + "%p"
                              + "  (참고 — 판정은 아래 평균±2σ 가 한다)");

            // ⚠⚠ **`격차범위제외` 지표는 2026-08-08 에 없앴다.** 범위 무공이 애초에 토너먼트에
            //   안 들어오므로 `격차전체` 와 값이 같아졌다 — 같은 값을 두 이름으로 내면
            //   나중에 *"어느 쪽이 판정 기준이었지"* 를 다시 묻게 된다.
            //   ⚠ `--compare` 에 **사라짐 12건**으로 뜬다(4계층 × 3경지 중 범위가 있던 조합). 정상이다.
            //
            //   옛 경위 — 2026-08-04 에 이 지표를 만든 이유는 *"죽은 무공 하나가 계층 격차 전체를
            //   정의해 버린다"* 였다(대문파 `정천창군` 10.4% · 전승 `만우쾌사` 3.6%). 그 진단은
            //   맞았지만 처방이 절반이었다 — **범위 무공을 계산에서만 빼고 토너먼트에는 남겨 둬서**
            //   다른 무공이 그 샌드백을 이겨 얻은 승률은 그대로 남았다(위 `group` 주석의 2차 오염).
            {
                // ⚠⚠ **2026-08-05 — 격차에서 ✅/⚠ 판정을 뗐다.** 판정은 아래 `평균±2σ` 가 한다.
                //   격차(최고−최저)는 **종수에 체계적으로 끌려간다.** 정규분포 N표본의 기대 range 는
                //   `k(N)·σ` 이고 k 는 N 과 함께 커진다 — 5종짜리 강호무학과 39종짜리 대문파를
                //   **같은 30%p 잣대로 재는 것은 애초에 성립하지 않았다.**
                //
                // ⚠ **10성 실측 대조 — 네 계층 전부 적는다**(잘 맞은 둘만 인용하지 않는다):
                //     소문파  k(14)=3.408 · 예측 15.3 vs 실측 15.4  (오차 0.1%p)
                //     대문파  k(39)=4.302 · 예측 29.3 vs 실측 30.8  (오차 1.6%p)
                //     강호무학 k(5)=2.326 · 예측 19.8 vs 실측 24.0  (오차 4.2%p · 상대 21%)
                //     전승무학 k(7)=2.704 · 예측 24.6 vs 실측 29.7  (오차 5.1%p · 상대 17%)
                //   → **표본이 큰 쪽에서 잘 맞고 작은 쪽에서 크게 어긋난다.** 즉 *"격차는 σ×종수일 뿐"*
                //     은 **경향이지 항등식이 아니다.** 그래도 판정 기준으로 쓸 수 없다는 결론은 선다 —
                //     같은 임계값이 계층마다 다른 것을 뜻하기 때문이다.
                //   ⚠ 처음에 k(39) 를 **4.2 로 기억해서 적었는데 틀렸다**(정확값 ≈4.302, `verify` 가
                //     몬테카를로 200만 회로 확인). 기억으로 적은 상수는 근거가 아니다.
                //
                //   ⚠ 격차는 지표로 계속 낸다 — 사람이 순위표를 읽을 때 눈금이 되고, 과거 기록과도 잇는다.
            }

            // ⚠⚠ **범위 무공을 따로 잰다** (2026-08-08 신설).
            //   토너먼트에서 뺐다고 **버리는 것이 아니다.** 깨끗한 집단(위 `fighters`)을 상대로
            //   각각 재서 여기 낸다 — 서로를 상대하지 않으므로 범위 무공끼리의 오염도 없고,
            //   비범위 무공의 승률에도 영향을 주지 않는다.
            //   ⛔ **이 값으로 밸런스를 판정하지 마라.** `AttackScope` 미연결이라 대가만 내고
            //     이점이 0 인 상태의 숫자다. Phase 3 다대다가 붙으면 그때 판정 보류를 푼다.
            if (scoped.Count > 0)
            {
                Console.WriteLine("  ── 범위 무공 " + scoped.Count + "종 (위 " + group.Count
                                  + "종을 상대로 · ⛔ 판정 보류 — 1대1이라 이점이 없다) ──");
                for (int i = 0; i < scoped.Count; i++)
                {
                    Combatant c = ToCombatant(scoped[i], stage);
                    double sum = 0;
                    for (int j = 0; j < fighters.Count; j++) sum += WinRate(c, fighters[j]);
                    double rate = sum / fighters.Count;

                    MartialArt a = scoped[i];
                    Console.WriteLine("      " + Pad(a.Name, 26)
                                      + Pad(a.IsWandererArt ? "(강호무학)" : a.School, 12)
                                      + Pad(Short(a.Discipline) + "·" + Short(a.Alignment), 8)
                                      + (rate * 100).ToString("F1").PadLeft(6) + "%"
                                      + "  공" + a.Delta.Attack.ToString("F2").PadLeft(5));
                    Metrics.Add("scope." + a.Name + "." + stage + "성", rate * 100);
                }
            }

            // ⚠⚠ **계층 전체 표준편차 — 2026-08-05 신설.**
            //   `격차`(최고−최저)는 **표본 수에 체계적으로 끌려간다.** 종수가 많을수록 양 끝이
            //   멀어지는 것은 밸런스가 아니라 통계다 — 강호무학 5종과 대문파 39종(범위 제외)을
            //   **같은 30%p 잣대로 재는 것 자체가 틀렸을 수 있다.**
            //   인계 §4-6-5 가 *"목표치를 표준편차 기준으로 다시 정할지가 미결"* 로 남겨 둔 것이 이것이고,
            //   판정하려면 **먼저 그 값이 있어야** 한다(성향별 표준편차는 있는데 계층 전체가 없었다).
            // ⚠ 범위 무공은 뺀다 — 격차와 같은 기준이어야 두 지표를 나란히 읽을 수 있다.
            if (live.Count >= 2)
            {
                double avg = 0;
                for (int i = 0; i < live.Count; i++) avg += live[i];
                avg /= live.Count;

                // ⚠ **N 으로 나눈다(모집단 분산).** 표본이 아니라 **그 계층의 무공 전부**이므로
                //   불편추정량(N−1)이 아니라 모집단 σ 가 맞는 정의다. 기존 성향별 σ 와도 같은 방식이다.
                //   ⚠ `verify` 가 소표본에서 N−1 을 권했는데, 이번 값으로는 어느 쪽이든 ✅/⚠ 가
                //     안 뒤집힘을 확인했다. **정의 일관성**을 택했다.
                double varSum = 0;
                for (int i = 0; i < live.Count; i++) varSum += (live[i] - avg) * (live[i] - avg);
                double sd = Math.Sqrt(varSum / live.Count) * 100;

                Metrics.Add("tier." + TierName(tier) + ".표준편차." + stage + "성", sd);
                Metrics.Add("tier." + TierName(tier) + ".종수." + stage + "성", live.Count);

                // ⚠⚠ **주 판정 기준 — 평균 ± 2σ 가 [죽은, 지배] 임계 안에 드는가** (2026-08-05 신설).
                //   임의로 지은 값이 아니라 **이미 쓰던 두 임계값에서 유도**한 것이다:
                //   `DeadThreshold 35` · `DominantThreshold 65`. 정규 근사에서 ±2σ 는 약 95% 를 덮으므로
                //   *"이 계층 무공의 대부분이 죽지도 지배하지도 않는가"* 를 직접 묻는 셈이다.
                //
                //   ⚠⚠ **평균을 함께 쓰는 것이 핵심이다.** 상위 계층은 평균이 50 이 아니라 **의도적으로
                //     높다**(전승 56.6 — 계층 이점). σ 하나만 보면 그 편향이 안 보이고,
                //     "±2σ 가 65 를 넘는가" 로 물으면 **계층 이점과 흩어짐이 함께** 판정된다.
                //
                // ⚠⚠ **한계 — 승률 분포가 정규인지 확인하지 않았다.** "±2σ ≈ 95%" 는 정규 가정이고,
                //   이 저장소는 그것을 검정한 적이 없다. 그래서 이 값은 **판정이 아니라 신호**다
                //   (§5-D: *"도구는 좋아짐/나빠짐을 판정하지 않는다 — 읽는 것은 사람 몫"*).
                // ⚠ **옛 기준의 결함을 1건 실증했을 뿐, 새 기준이 일반적으로 낫다는 증명은 아니다**
                //   (`verify` 지적). 위양성(정상 계층을 ⚠ 로 찍는 경우)은 아직 검사되지 않았다.
                //   실증한 것 하나 — 전승무학이 옛 기준으로 **격차 29.7 ✅** 였는데 그 계층에는
                //   `마한중참` 67.1(지배 의심)이 있었고 새 기준의 상단 2σ 는 **74.9** 다.
                double upper = (avg * 100) + 2 * sd;
                double lower = (avg * 100) - 2 * sd;
                bool ok = upper <= DominantThreshold * 100 && lower >= DeadThreshold * 100;

                Metrics.Add("tier." + TierName(tier) + ".상단2시그마." + stage + "성", upper);
                Metrics.Add("tier." + TierName(tier) + ".하단2시그마." + stage + "성", lower);

                Console.WriteLine("  → 계층 표준편차 " + sd.ToString("F1") + "%p"
                                  + " (범위 제외 " + live.Count + "종 · 평균 " + (avg * 100).ToString("F1") + "%)"
                                  + "  ·  평균±2σ " + lower.ToString("F1") + "~" + upper.ToString("F1")
                                  + (ok ? "  ✅ [35,65] 이내" : "  ⚠ [35,65] 벗어남")
                                  + (live.Count < 8 ? "  ⚠ 표본 " + live.Count + "종 — σ 추정이 불안정하다" : ""));
            }

            // ⚠⚠ **성향별로도 가른다** (2026-08-04 사용자 가설). 성향 곡선은 **의도적으로 다르다** —
            //   정파 선형(만렙 2.15) · 사파 제곱근(2.00, 초반 급상승) · 마도 계단(2.24, 24숙련까지 1.0).
            //   §3-3 이 *"초반 사파 → 중반 정파 → 후반 마도"* 라는 역전 순서를 **의도**로 못박았다.
            //   → 그렇다면 계층 격차에는 **고쳐야 할 불균형**과 **의도된 성향 차이**가 섞여 있다.
            //     성향 안에서의 격차가 작고 성향 사이가 크다면, 그 격차는 **설계대로 작동하는 것**이다.
            //   ⚠ 범위 무공은 여기서도 뺀다. 강호무학은 성향이 없어 이 표에 안 나온다.
            var byAlign = new Dictionary<Alignment, List<double>>();
            for (int i = 0; i < rows.Count; i++)
            {
                MartialArt a = rows[i].Key;
                if (a.IsWandererArt || HasScopeMorpheme(a)) continue;
                if (a.Alignment == null) continue;         // ⚠ 성향 없는 무공은 이 표에 못 넣는다
                Alignment al = a.Alignment.Value;
                if (!byAlign.ContainsKey(al)) byAlign[al] = new List<double>();
                byAlign[al].Add(rows[i].Value);            // rows 가 이미 내림차순이라 순서 유지된다
            }
            foreach (KeyValuePair<Alignment, List<double>> kv in byAlign)
            {
                if (kv.Value.Count < 2) continue;
                double g = (kv.Value[0] - kv.Value[kv.Value.Count - 1]) * 100;
                double avg = 0;
                for (int i = 0; i < kv.Value.Count; i++) avg += kv.Value[i];
                avg /= kv.Value.Count;
                double var = 0;
                for (int i = 0; i < kv.Value.Count; i++) var += (kv.Value[i] - avg) * (kv.Value[i] - avg);
                double sd = Math.Sqrt(var / kv.Value.Count) * 100;

                Metrics.Add("tier." + TierName(tier) + "." + Short(kv.Key) + "파.표준편차." + stage + "성", sd);
                Metrics.Add("tier." + TierName(tier) + "." + Short(kv.Key) + "파.평균." + stage + "성", avg * 100);
                Console.WriteLine("     └ " + Short(kv.Key) + "파 " + kv.Value.Count.ToString().PadLeft(2) + "종 · "
                                  + "격차 " + g.ToString("F1").PadLeft(5) + "%p"
                                  + (g <= 30 ? " ✅" : " ⚠")
                                  + " · 표준편차 " + sd.ToString("F1").PadLeft(4) + "%p"
                                  + " · 평균 " + (avg * 100).ToString("F1") + "%");
            }

            PrintTierByDiscipline(tier, stage, rows);
            PrintTierByAttackSum(tier, stage, rows);
        }

        /// <summary>
        /// **공격 합이 승률을 설명하는가 — 2026-08-05 신설.**
        ///
        /// ⚠⚠ **순위표에 공격 합을 찍어 놓고 눈으로 상관을 읽으려다 그만뒀다.** 오늘만 두 번
        ///   눈으로 세다 틀렸고(§4-8-2), 39종을 손으로 옮겨 적는 것 자체가 그 실수의 자리다.
        ///   → **상관계수와 구간별 평균을 도구가 낸다.** 그래야 기준선에 남고 `--compare` 가 본다.
        ///
        /// ⚠ 상관은 인과가 아니다. 이 표가 답하는 것은 딱 하나 — *"공격 형태소를 조정하면
        ///   계층 격차가 움직이는가"* 다. r² 이 작으면 **그 손잡이로는 격차를 못 고친다.**
        /// ⚠ 범위 무공은 뺀다(대가만 내고 이점이 0 이라 관계를 통째로 왜곡한다).
        /// </summary>
        private static void PrintTierByAttackSum(
            ArtTier tier, int stage, List<KeyValuePair<MartialArt, double>> rows)
        {
            var w = new List<double>();
            var a = new List<double>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (HasScopeMorpheme(rows[i].Key)) continue;
                w.Add(rows[i].Value * 100);
                a.Add(rows[i].Key.Delta.Attack);
            }
            if (w.Count < 5) return;

            double mw = 0, ma = 0;
            for (int i = 0; i < w.Count; i++) { mw += w[i]; ma += a[i]; }
            mw /= w.Count; ma /= a.Count;

            double cov = 0, sw = 0, sa = 0;
            for (int i = 0; i < w.Count; i++)
            {
                cov += (w[i] - mw) * (a[i] - ma);
                sw += (w[i] - mw) * (w[i] - mw);
                sa += (a[i] - ma) * (a[i] - ma);
            }
            if (sw <= 0 || sa <= 0) return;

            double r = cov / (Math.Sqrt(sw) * Math.Sqrt(sa));
            Console.WriteLine("     → 공격 합 vs 승률  r " + r.ToString("F3")
                              + " · r² " + (r * r).ToString("F3")
                              + ((r * r) < 0.25 ? "  ⚠ 공격 축으로는 격차를 못 고친다" : ""));
            Metrics.Add("tier." + TierName(tier) + ".공격상관r." + stage + "성", r);

            // 구간별 평균 — 상관계수 하나로는 **비선형(하한선)** 을 못 본다.
            //   실제로 대문파는 "0.5 이하만 나쁘고 그 위로는 평평" 이라 r 이 작게 나온다.
            double[] cuts = { 0.5, 1.5, 2.5 };
            string[] names = { "≤0.5", "0.5~1.5", "1.5~2.5", ">2.5" };
            for (int b = 0; b < 4; b++)
            {
                double sum = 0;
                int n = 0;
                for (int i = 0; i < w.Count; i++)
                {
                    int bucket = a[i] <= cuts[0] ? 0 : (a[i] <= cuts[1] ? 1 : (a[i] <= cuts[2] ? 2 : 3));
                    if (bucket != b) continue;
                    sum += w[i]; n++;
                }
                if (n == 0) continue;
                Console.WriteLine("        공격 합 " + Pad(names[b], 9) + n.ToString().PadLeft(2) + "종 · 평균 "
                                  + (sum / n).ToString("F1").PadLeft(5) + "%");
                Metrics.Add("tier." + TierName(tier) + ".공격구간" + names[b] + ".평균." + stage + "성", sum / n);
            }
        }

        /// <summary>
        /// **계층을 유형(무기)으로도 가른다 — 2026-08-05 신설.**
        ///
        /// ⚠⚠ **성향 분해는 2026-08-04 에 만들었는데 유형 분해는 없었다.** 그래서 계층 격차를
        ///   *"성향 때문인가 아닌가"* 로만 물을 수 있었고, 실제로 그렇게 물었다가 한 번 틀렸다
        ///   (§4-8-2 — 지배 4종이 마도라고 세었는데 실은 **문파 편중**이었다).
        ///
        /// ⚠⚠ **유형은 성향과 달리 "의도된 차이" 라는 방패가 없다.** §3-3 이 성향 역전 순서를
        ///   설계 의도로 못박은 것과 달리, 다섯 무기는 **서로 대등해야 한다** — 무기 선택이 곧
        ///   문파 선택이므로 특정 무기가 구조적으로 밀리면 그 문파들이 통째로 밀린다.
        ///
        /// ⚠ **유형 민감도표(<see cref="PrintDisciplineSensitivity"/>)로는 이걸 못 본다.**
        ///   그 표는 *같은 무공명*을 다섯 무기로 들려 재므로 공격방식이 고정된다. 그런데 실제
        ///   카탈로그에서는 **공격방식과 유형이 1:1 로 묶여 있다**(사전 §3-1 주석: 69/71).
        ///   비도 무공은 반드시 던지기(투척포사 · 공격 0.5 로 최약)를 쓰고, 검·도는 베기(공격 2)를 쓴다.
        ///   → **두 페널티가 겹치는데 그 겹침이 어느 표에도 안 나온다.** 이 분해가 그 자리다.
        /// </summary>
        private static void PrintTierByDiscipline(
            ArtTier tier, int stage, List<KeyValuePair<MartialArt, double>> rows)
        {
            // ⚠ 범위 무공은 성향 분해와 같은 이유로 뺀다(`AttackScope` 미연결이라 이점이 0).
            var byKind = new Dictionary<Discipline, List<double>>();
            for (int i = 0; i < rows.Count; i++)
            {
                MartialArt a = rows[i].Key;
                if (HasScopeMorpheme(a)) continue;
                if (!byKind.ContainsKey(a.Discipline)) byKind[a.Discipline] = new List<double>();
                byKind[a.Discipline].Add(rows[i].Value);   // rows 가 이미 내림차순이라 순서 유지된다
            }
            if (byKind.Count < 2) return;

            double bestAvg = double.MinValue;
            double worstAvg = double.MaxValue;

            foreach (KeyValuePair<Discipline, List<double>> kv in byKind)
            {
                double avg = 0;
                for (int i = 0; i < kv.Value.Count; i++) avg += kv.Value[i];
                avg /= kv.Value.Count;

                Metrics.Add("tier." + TierName(tier) + ".유형." + DisciplineName(kv.Key) + ".평균." + stage + "성",
                    avg * 100);
                Console.WriteLine("     └ " + Pad(DisciplineName(kv.Key), 5)
                                  + kv.Value.Count.ToString().PadLeft(2) + "종 · 평균 "
                                  + (avg * 100).ToString("F1").PadLeft(5) + "%"
                                  + " · 최고 " + (kv.Value[0] * 100).ToString("F1").PadLeft(5)
                                  + " · 최저 " + (kv.Value[kv.Value.Count - 1] * 100).ToString("F1").PadLeft(5));

                // ⚠ 1종짜리 유형은 평균이 곧 그 무공이라 격차 판정에 넣으면 표본 착시가 된다.
                if (kv.Value.Count < 2) continue;
                if (avg > bestAvg) bestAvg = avg;
                if (avg < worstAvg) worstAvg = avg;
            }

            if (bestAvg == double.MinValue || worstAvg == double.MaxValue) return;

            double spread = (bestAvg - worstAvg) * 100;
            Console.WriteLine("     → 유형 평균 격차 " + spread.ToString("F1") + "%p"
                              + (spread <= 10 ? " ✅" : " ⚠") + " (2종 이상인 유형만)");
            Metrics.Add("tier." + TierName(tier) + ".유형평균격차." + stage + "성", spread);
        }

        /// <summary>
        /// **유형 숙달이 실제로 발동하는 무공의 비율 — 2026-08-05 신설 (사용자 지적으로).**
        ///
        /// ⚠⚠ **비도(飛刀)의 숙달만 조건부다.** `DisciplineCurve.cs:47` 이 이미 적어 뒀다 —
        ///   *"비도(상태) — 상태이상 형태소를 넣은 무공에서만. 실측 기여 +4.9%p"*.
        ///   검(명중)·창(선공·속도)·도(방어관통)는 무공 구성과 무관하게 항상 걸리고,
        ///   권(기력 −100%)은 구성이 아니라 **런타임 조건**(기력이 말라야 한다)이다.
        ///
        /// ⚠⚠ **그래서 "비도 숙달을 손보자" 는 안에는 숨은 전제가 있다 — 비도 무공이 전부
        ///   상태이상을 갖는가.** 세어 보지 않고 그 안을 올렸다가 사용자가 잡았다.
        ///   → 전제를 사람 기억이 아니라 **도구가 매번 세게** 한다.
        ///
        /// ⚠ 판정은 **이름 문자열이 아니라 파싱된 델타**로 한다(§4-8 과 같은 이유 —
        ///   한글 한 글자가 키라 문자 대조는 틀린다).
        /// </summary>
        private static void PrintDaggerMasteryCondition(List<MartialArt> all)
        {
            Console.WriteLine();
            Console.WriteLine("██ 비도(飛刀) 숙달 — **상태이상 축**의 발동 조건 ██");
            Console.WriteLine("     ⚠⚠ 2026-08-09 — 이 표는 이제 **비도 숙달의 절반**만 말한다.");
            Console.WriteLine("        치명률 축이 신설돼 **상태이상이 없어도 숙달이 값을 한다.**");
            Console.WriteLine("        그전에는 ❌ 가 곧 *\"숙달이 통째로 죽는다\"* 였고, 그것이 신설의 계기였다.");

            int met = 0, total = 0;
            for (int i = 0; i < all.Count; i++)
            {
                MartialArt a = all[i];
                // ⚠ 넘겨받는 `all` 은 `MartialArtCatalog.Techniques()`(공격 초식)라 종류 필터가 필요 없다.
                if (a.Discipline != Discipline.Dagger) continue;

                total++;
                bool has = HasStatusMorpheme(a);
                if (has) met++;

                Console.WriteLine("     " + (has ? "✅" : "❌") + "  " + Pad(a.Name, 12)
                                  + Pad(TierName(a.Tier), 12)
                                  + (a.IsWandererArt ? "(강호무학)" : a.School)
                                  + (has ? "" : "   ⚠ 상태이상 축은 안 걸린다(치명 축은 걸린다)"));
            }

            if (total == 0) return;
            double rate = met * 100.0 / total;
            Console.WriteLine("     → 상태이상 축 충족 " + met + "/" + total + " (" + rate.ToString("F1") + "%)"
                              + "  ·  치명 축은 " + total + "/" + total + " 전부 발동");
            Metrics.Add("discipline.비도.숙달조건충족률", rate);
        }

        /// <summary>상태이상 형태소를 하나라도 가졌는가. 델타로 판정한다(문자열 대조 금지).</summary>
        private static bool HasStatusMorpheme(MartialArt art)
        {
            ArtStatDelta d = art.Delta;
            return d.PoisonChance > 0 || d.BleedChance > 0 || d.BurnChance > 0
                   || d.FrostbiteChance > 0 || d.QiDrainChance > 0 || d.StaggerChance > 0
                   || d.ParalysisStack > 0;
        }

        // ⚠⚠ 이름표는 Core(`KoreanNames`)에 있다. 여기 있던 표는 **같은 파일 안에서만 두 번**
        //   (`DisciplineName` · `Short(Discipline)`) 중복이었고 `ArtCompositionRule` · `ParsedArtName`
        //   까지 넷이었다. 지표 키가 이 문자열로 만들어지므로(`tier.전승무학.유형.비도....`)
        //   갈라지면 `docs/baseline-metrics.txt` 와의 대조가 끊긴다.
        private static string DisciplineName(Discipline d) => KoreanNames.Of(d);

        /// <summary>
        /// 이름에 **범위 형태소**(다多·군群·전全·만萬)가 들어 있는가 — 2026-08-04 신설.
        ///
        /// ⚠⚠ **문자열 대조가 아니라 파서로 판정한다.** 한글 한 글자가 키라서 `전`·`만` 같은
        ///   글자는 다른 뜻으로도 쓰일 수 있고, 실제로 `bash` 문자 클래스로 세다가 한 번 틀렸다.
        ///   `MorphemeParser` 가 실제로 무엇으로 해석하는지가 유일한 진실이다.
        /// ⚠ 파싱 실패는 **false** 로 둔다 — 못 읽은 것을 범위로 단정하지 않는다.
        ///
        /// ⚠⚠ **`kind` 를 반드시 넘겨야 한다.** null 로 부르면 파서가 *"접미사에서 종류를 얻는"*
        ///   강호무학 경로를 타서 **문파 무공이 전부 파싱 실패**한다(처음에 그렇게 만들어 표시가
        ///   하나도 안 붙었다). 반대로 강호무학에 `kind` 를 넘기면 접미사(검법·도법…)를 안 떼고
        ///   본체로 읽어 실패한다. **두 경로가 배타적**이므로 갈라서 부른다.
        /// </summary>
        private static bool HasScopeMorpheme(MartialArt art)
        {
            ParsedArtName parsed;
            IReadOnlyList<string> problems;
            // ⚠ `MartialArt` 는 `Kind` 를 들고 있지 않다. 유형(Discipline)에서 되돌린다.
            ArtKind kind = art.Discipline == Discipline.InnerArt ? ArtKind.Internal
                         : art.Discipline == Discipline.Movement ? ArtKind.Movement
                         : ArtKind.Attack;

            bool ok = art.IsWandererArt
                ? MorphemeParser.TryParse(art.Name, out parsed, out problems)       // 접미사에서 종류를 읽는다
                : MorphemeParser.TryParse(art.Name, kind, out parsed, out problems); // 이름 전체가 본체다
            if (!ok) return false;
            return parsed.CountOf(MorphemeCategory.Scope) > 0;
        }

        /// <summary>계층 간 격차 — 여기는 벌어지는 것이 **정상**이다. 다만 압도적이면 안 된다.</summary>
        private static void PrintCrossTier(List<MartialArt> all, int stage)
        {
            var byTier = new Dictionary<ArtTier, List<MartialArt>>();
            foreach (ArtTier t in RankedTiers)
            {
                byTier[t] = new List<MartialArt>();
            }
            for (int i = 0; i < all.Count; i++) byTier[all[i].Tier].Add(all[i]);

            Console.WriteLine();
            Console.WriteLine("── 계층 간 (상위가 이기는 것이 정상. 다만 90% 이상이면 하위 계층이 무의미해진다) ──");

            Report(byTier, ArtTier.Minor, ArtTier.Wanderer, stage);
            // ⚠ 2026-08-02 — 이 줄이 **두 번 적혀 있었다.** 같은 값이 두 줄로 찍혀
            //   표를 읽는 사람이 서로 다른 쌍이라고 오해할 수 있었고, 138 규모 측정을 한 번 더 돌리고 있었다.
            Report(byTier, ArtTier.Major, ArtTier.Minor, stage);
            Report(byTier, ArtTier.Legacy, ArtTier.Major, stage);
            Report(byTier, ArtTier.Major, ArtTier.Wanderer, stage);
        }

        private static void Report(
            Dictionary<ArtTier, List<MartialArt>> byTier, ArtTier high, ArtTier low, int stage)
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

        // ─────────────────── 다대다 4대4 (2026-08-09 신설 · 설계 §D8) ───────────────────

        /// <summary>4대4. 측정에서만 쓰는 편성이며 엔진은 비대칭·가변을 받는다(설계 §D1).</summary>
        private const int MultiTeamSize = 4;

        /// <summary>
        /// 4대4 한 쌍당 전투 수.
        ///
        /// ⚠ 1대1(<see cref="FightsPerMatchup"/> 100전)보다 낮게 잡았다 — **한 전투가 훨씬 길다**
        ///   (참가자 8명이 매 경합 행동한다). 실행 시간을 재고 정한 값이며, 표본이 작은 만큼
        ///   **소수점 한 자리를 신뢰하지 말 것**. 이 블록은 *"범위가 쓸모 있어졌는가"* 라는
        ///   **부호와 크기**를 보는 자리이지 미세 조정용이 아니다.
        /// </summary>
        private const int MultiFights = 40;

        /// <summary>
        /// 대조군 표본 수. ⚠ 전수로 돌리면 대문파 4자군만 30종이 넘어 30×29 매치업이 된다.
        /// 뽑는 방식과 버린 개수는 <see cref="SampleControls"/> 가 **반드시 찍는다**(§5-D — 조용한 절단 금지).
        /// </summary>
        private const int MultiControlSample = 12;

        private sealed class MultiTally
        {
            public long Counters;
            public long Rounds;
            public int Draws;
            public int Fights;
        }

        /// <summary>
        /// **동질 4인 팀끼리의 라운드로빈** — 범위 형태소가 실제로 값을 갖는가를 처음으로 잰다.
        ///
        /// ⚠⚠ **1대1 표와 절대 섞지 않는다.** 다른 토너먼트를 한 표에 섞는 것은 이 저장소가 이미 밟은
        ///   실수다(HANDOFF §4-3-6 실수 #2). 지표 이름도 <c>multi.*</c> 로 갈라 둔다.
        ///
        /// ⚠⚠ **이것은 범위 형태소 가치의 상한이다**(설계 §D8). 동질 팀에서는 **넷 전부가 범위 무공**을
        ///   쓰는데, 실제 편성이라면 한둘만 들 것이다. *"실전 값"* 이 아니다.
        ///
        /// ⛔⛔ **이 데이터로 엔진의 다중 후보 동작을 판단하지 마라.** 동질 팀은 각 전투원이 공격 초식
        ///   후보를 **1개만** 갖는 조건이고, 그것은 §4-9-6 이 *"Sandbox·테스트 31곳 전부가 후보 1개라
        ///   `SelectArt` 의 순서 의존 결함이 안 보였다"* 고 지적한 **바로 그 조건**이다.
        ///
        /// **대조군 설계** — 범위 무공만 재면 *"세다/약하다"* 를 말할 기준이 없다. 같은 계층 안에서
        /// **글자 수가 같고 반격 형태소 보유 여부가 같은** 비범위 무공을 짝지어 **같은 표에** 넣는다.
        /// ⚠ 반격을 통제하는 이유(설계 §D6) — 범위 공격은 맞은 사람 **각자**가 반격하므로 그것이
        ///   내장 대가인데, `Counter()` 는 반격 형태소가 있을 때만 발동한다. 통제하지 않으면 제동의
        ///   유무가 **대조군 뽑기에 좌우되고**, 우리가 재는 것이 대조군 선택의 산물이 된다.
        /// </summary>
        private static void PrintMultiCombat(List<MartialArt> all, ArtTier tier, int stage)
        {
            // 같은 계층 안에서 (글자 수 · 반격 보유) 로 묶는다. 범위 무공이 없는 묶음은 잴 이유가 없다.
            var groups = new SortedDictionary<string, List<MartialArt>>(StringComparer.Ordinal);
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Tier != tier) continue;
                string key = all[i].Name.Length + "자" + (HasCounterMorpheme(all[i]) ? "·반격" : "");
                if (!groups.ContainsKey(key)) groups[key] = new List<MartialArt>();
                groups[key].Add(all[i]);
            }

            foreach (KeyValuePair<string, List<MartialArt>> kv in groups)
            {
                var scoped = new List<MartialArt>();
                var controls = new List<MartialArt>();
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    if (HasScopeMorpheme(kv.Value[i])) scoped.Add(kv.Value[i]); else controls.Add(kv.Value[i]);
                }
                if (scoped.Count == 0 || controls.Count == 0) continue;

                List<MartialArt> sampled = SampleControls(controls, kv.Key);

                var field = new List<MartialArt>(scoped);
                field.AddRange(sampled);

                var teams = new List<List<BattlePlacement>>();
                for (int i = 0; i < field.Count; i++) teams.Add(HomogeneousTeam(field[i], stage));

                var tally = new MultiTally();
                var rows = new List<KeyValuePair<MartialArt, double>>();
                for (int i = 0; i < field.Count; i++)
                {
                    double sum = 0;
                    for (int j = 0; j < field.Count; j++)
                    {
                        if (i == j) continue;
                        sum += TeamWinRate(teams[i], teams[j], tally);
                    }
                    rows.Add(new KeyValuePair<MartialArt, double>(field[i], sum / (field.Count - 1)));
                }
                rows.Sort((x, y) => y.Value.CompareTo(x.Value));

                Console.WriteLine();
                Console.WriteLine("  ── " + TierName(tier) + " " + kv.Key + " · 4대4 동질 팀 (범위 "
                                  + scoped.Count + " · 대조 " + sampled.Count + ") "
                                  + "⚠ 범위 가치의 **상한**이다 — 넷 전부가 범위를 쓴다 ──");

                double scopeSum = 0, controlSum = 0;
                var byDiscipline = new SortedDictionary<string, List<double>>(StringComparer.Ordinal);
                for (int i = 0; i < rows.Count; i++)
                {
                    MartialArt a = rows[i].Key;
                    bool isScope = HasScopeMorpheme(a);
                    if (isScope) scopeSum += rows[i].Value; else controlSum += rows[i].Value;

                    string d = DisciplineName(a.Discipline);
                    if (!byDiscipline.ContainsKey(d)) byDiscipline[d] = new List<double>();
                    byDiscipline[d].Add(rows[i].Value);

                    Console.Write("    " + (i + 1).ToString().PadLeft(2) + "  "
                                  + Pad(a.Name, 26) + Pad(isScope ? "범위" : "대조", 6)
                                  + Pad(Short(a.Discipline) + "·" + Short(a.Alignment), 8)
                                  + (rows[i].Value * 100).ToString("F1").PadLeft(6) + "%"
                                  + "  공" + a.Delta.Attack.ToString("F2").PadLeft(5));
                    if (rows[i].Value >= DominantThreshold) Console.Write("  ⚠ 지배 의심");
                    else if (rows[i].Value <= DeadThreshold) Console.Write("  ⚠ 죽은 선택지 의심");
                    Console.WriteLine();

                    Metrics.Add("multi." + a.Name + "." + stage + "성", rows[i].Value * 100);
                }

                string tag = TierName(tier) + "." + kv.Key;
                double scopeAvg = scopeSum / scoped.Count * 100;
                double controlAvg = controlSum / sampled.Count * 100;
                Metrics.Add("multi." + tag + ".범위평균." + stage + "성", scopeAvg);
                Metrics.Add("multi." + tag + ".대조평균." + stage + "성", controlAvg);
                Metrics.Add("multi." + tag + ".범위이득." + stage + "성", scopeAvg - controlAvg);

                Console.WriteLine("    → 범위 평균 " + scopeAvg.ToString("F1") + "%  ·  대조 평균 "
                                  + controlAvg.ToString("F1") + "%  ·  차 "
                                  + (scopeAvg - controlAvg >= 0 ? "+" : "") + (scopeAvg - controlAvg).ToString("F1") + "%p");

                // ⚠ 유형별 — §D3-4-a 의 *"비도 필수 픽"* 위험을 보는 자리다. 후열 접근권이 비도에
                //   몰려 있으므로 비도가 혼자 올라가면 반대 방향의 지배가 된다.
                foreach (KeyValuePair<string, List<double>> dk in byDiscipline)
                {
                    double s = 0;
                    for (int i = 0; i < dk.Value.Count; i++) s += dk.Value[i];
                    Metrics.Add("multi." + tag + ".유형." + dk.Key + "." + stage + "성", s / dk.Value.Count * 100);
                }

                // ⚠⚠ 미결 M3 — 반격 계수는 *"라운드당 상대 1명"* 전제로 맞춰진 값이다(설계 §6).
                //   범위 공격은 맞은 사람 각자가 반격하므로 한 경합에 최대 팀 크기만큼 나올 수 있다.
                //   **얼마나 커지는지 먼저 재고** 나서 값을 본다 — 측정 전에 고치지 않는다.
                double perRound = tally.Rounds == 0 ? 0 : (double)tally.Counters / tally.Rounds;
                double drawRate = tally.Fights == 0 ? 0 : tally.Draws * 100.0 / tally.Fights;
                double avgRounds = tally.Fights == 0 ? 0 : (double)tally.Rounds / tally.Fights;
                Metrics.Add("multi." + tag + ".반격_경합당." + stage + "성", perRound);
                Metrics.Add("multi." + tag + ".무승부율." + stage + "성", drawRate);
                Metrics.Add("multi." + tag + ".경합수." + stage + "성", avgRounds);

                Console.WriteLine("    → 평균 " + avgRounds.ToString("F1") + "경합 · 무승부 "
                                  + drawRate.ToString("F1") + "% · 반격 " + perRound.ToString("F2") + "회/경합"
                                  + (drawRate > 0 ? "   ⚠ 무승부가 0 이 아니다 — 그 자체가 신호다" : ""));
            }
        }

        /// <summary>
        /// **미결 M3 전용 탐침 — 반격이 몇 배로 늘어나는가** (설계 §6 · 2026-08-09 신설).
        ///
        /// ⚠⚠ **위 <see cref="PrintMultiCombat"/> 의 `반격_경합당` 은 M3 을 재지 못한다.**
        ///   그 표는 대조군을 **반격 보유 여부로 통제**하는데(§D6), 범위 무공이 들어 있는 묶음에는
        ///   반격 형태소를 가진 무공이 하나도 없어 값이 **구조적으로 0** 이 된다.
        ///   → **0.00 은 *"폭증이 없다"* 가 아니라 *"이 표본에 반격자가 없다"* 이다.**
        ///   그래서 통제를 일부러 깨는 별도 탐침을 둔다. 안 그러면 0 을 보고 *"M3 은 문제없다"* 로 읽는다.
        ///
        /// 재는 것 — **같은 반격 보유 팀**을 상대로 ⓐ 범위 무공 팀과 ⓑ 단일 대상 무공 팀이
        /// 각각 경합당 몇 회의 반격을 받는가. 설계는 *"최대 팀 크기만큼"* 을 우려했다.
        /// ⛔ **재기만 한다. 값을 고치지 않는다**(설계 §6 — 측정 전에 반격 계수를 손대지 않는다).
        ///
        /// ⚠⚠ **반격을 가진 상대를 만들려면 경공 무공을 함께 들려야 한다.** 사전 §3-2 가 방어 11자를
        ///   **경공 무공 필수**로 묶어 두어 반·역·응은 보법에만 붙는다. 그런데 이 도구의
        ///   <see cref="ToCombatant"/> 는 **공격 초식 하나만** 들려준다 — 그래서 다른 모든 블록에서
        ///   <c>CounterRate</c> 가 **구조적으로 0** 이다. 이 탐침만 예외로 보법을 하나 얹는다.
        /// </summary>
        private static void PrintCounterProbe(List<MartialArt> all, ArtTier tier, int stage)
        {
            var scoped = new List<MartialArt>();
            var plain = new List<MartialArt>();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Tier != tier) continue;
                if (HasScopeMorpheme(all[i])) scoped.Add(all[i]); else plain.Add(all[i]);
            }
            if (scoped.Count == 0 || plain.Count == 0) return;

            MartialArt counterStep = FindCounterStep();
            if (counterStep == null)
            {
                Console.WriteLine("    ⛔ 반격 형태소를 가진 경공 무공이 카탈로그에 없다 — M3 을 잴 수 없다.");
                return;
            }

            // 상대 팀 — 단일 대상 무공 + **반격 보법**. 표본은 대조군 뽑기와 같은 방식으로 줄인다.
            List<MartialArt> foes = SampleEvenly(plain, MultiControlSample, TierName(tier) + " M3 상대");

            // ⚠⚠ **비교군도 같은 방식으로 뽑는다** (2026-08-09 `verify` 가 잡았다).
            //   처음에는 `plain` 앞에서부터 `scoped.Count` 개를 그냥 잘라 썼는데, 카탈로그 선언 순서라
            //   **앞쪽 두 문파(소림·무당)에 쏠린 표본**이었다. 균등 표집으로 바꾸니 대문파 배수가
            //   2.8 → 2.4 로 움직였다 — *"조용한 절단"* 을 `SampleControls` 주석에 적어 두고
            //   같은 함수의 반대편에서 스스로 어긴 사례다.
            List<MartialArt> singles = SampleEvenly(plain, scoped.Count, TierName(tier) + " M3 단일군");

            var scopeTally = new MultiTally();
            var plainTally = new MultiTally();
            for (int f = 0; f < foes.Count; f++)
            {
                List<BattlePlacement> foe = HomogeneousTeam(foes[f], stage, counterStep);
                for (int i = 0; i < scoped.Count; i++)
                {
                    TeamWinRate(HomogeneousTeam(scoped[i], stage), foe, scopeTally);
                }
                for (int i = 0; i < singles.Count; i++)
                {
                    TeamWinRate(HomogeneousTeam(singles[i], stage), foe, plainTally);
                }
            }

            double scopeRate = scopeTally.Rounds == 0 ? 0 : (double)scopeTally.Counters / scopeTally.Rounds;
            double plainRate = plainTally.Rounds == 0 ? 0 : (double)plainTally.Counters / plainTally.Rounds;
            Metrics.Add("multi.M3." + TierName(tier) + ".반격_경합당.범위." + stage + "성", scopeRate);
            Metrics.Add("multi.M3." + TierName(tier) + ".반격_경합당.단일." + stage + "성", plainRate);
            if (plainRate > 0)
            {
                Metrics.Add("multi.M3." + TierName(tier) + ".반격배수." + stage + "성", scopeRate / plainRate);
            }

            Console.WriteLine("    M3 탐침 — 상대 " + foes.Count + "종에 반격 보법(" + counterStep.Name
                              + ") 을 얹었다 · 반격 회수/경합 : 범위 " + scopeRate.ToString("F2")
                              + " vs 단일 " + plainRate.ToString("F2")
                              + (plainRate > 0 ? "  (배수 " + (scopeRate / plainRate).ToString("F2") + ")" : ""));
        }

        /// <summary>반격 형태소(반·역·응)를 가진 경공 무공 하나. 없으면 null.</summary>
        private static MartialArt FindCounterStep()
        {
            IReadOnlyList<MartialArt> all = MartialArtCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Discipline == Discipline.Movement && HasCounterMorpheme(all[i])) return all[i];
            }
            return null;
        }

        /// <summary>
        /// 대조군을 <see cref="MultiControlSample"/> 종으로 줄인다.
        ///
        /// ⚠⚠ **공격 합 순으로 정렬해 균등 간격으로 뽑는다.** 앞에서부터 잘라내면 대조군이 한쪽으로
        ///   치우쳐, 범위 무공이 세 보이는지 약해 보이는지가 **자르는 방식**에 좌우된다.
        /// ⚠⚠ **버린 개수를 반드시 찍는다.** 조용한 절단은 *"전부 쟀다"* 로 읽힌다(§5-D).
        /// </summary>
        private static List<MartialArt> SampleControls(List<MartialArt> controls, string groupKey)
        {
            return SampleEvenly(controls, MultiControlSample, "대조군 " + groupKey);
        }

        /// <summary>
        /// <paramref name="want"/> 종을 **공격 합 균등 간격**으로 뽑는다. 뽑을 것이 그보다 적으면 전부.
        /// ⚠ 표본을 줄이는 자리는 **전부 이 함수를 지난다** — 한쪽만 다른 방식으로 자르면
        ///   그 차이가 결론에 스며들고, 실제로 M3 탐침에서 한 번 그렇게 됐다.
        /// </summary>
        private static List<MartialArt> SampleEvenly(List<MartialArt> pool, int want, string label)
        {
            if (want < 1 || pool.Count <= want) return pool;

            var sorted = new List<MartialArt>(pool);
            sorted.Sort((x, y) => x.Delta.Attack.CompareTo(y.Delta.Attack));

            var picked = new List<MartialArt>(want);
            for (int i = 0; i < want; i++)
            {
                int index = want == 1 ? sorted.Count / 2
                          : (int)Math.Round(i * (sorted.Count - 1.0) / (want - 1));
                picked.Add(sorted[index]);
            }

            Console.WriteLine("    ⚠ " + label + " " + pool.Count + "종 중 " + want
                              + "종만 썼다(공격 합 균등 간격). 뺀 것 " + (pool.Count - want)
                              + "종 — 실행 시간 때문이다.");
            return picked;
        }

        /// <summary>
        /// 같은 무공을 넷이 든 팀. **전열 2 · 후열 2 고정**(측정 상수 — 설계 §D1).
        /// ⚠ 넷이 같은 무공이므로 **누구를 앞에 세우든 같다** — 배치가 변수로 새지 않는다.
        /// </summary>
        /// <param name="support">함께 익힐 보조 무공(내공·경공). M3 탐침만 쓴다. 없으면 null.</param>
        private static List<BattlePlacement> HomogeneousTeam(MartialArt art, int stage, MartialArt support = null)
        {
            var team = new List<BattlePlacement>(MultiTeamSize);
            for (int i = 0; i < MultiTeamSize; i++)
            {
                team.Add(new BattlePlacement(
                    support == null ? ToCombatant(art, stage) : ToCombatantWith(art, support, stage),
                    i < MultiTeamSize / 2 ? BattleRow.Front : BattleRow.Rear));
            }
            return team;
        }

        /// <summary>공격 초식 + 보조 무공 하나를 함께 익힌 대전자. <see cref="ToCombatant"/> 와 같은 규칙이다.</summary>
        private static Combatant ToCombatantWith(MartialArt art, MartialArt support, int stage)
        {
            // ⚠ 첫 번째가 주(主) 무공이다 — 성향·유형 숙달·무학분류를 그것에서 읽는다.
            return CombatantBuilder.Build(art.Name, new[] { art, support }, stage, Stats());
        }

        /// <summary>
        /// ⚠⚠ 승률 공식(무승부 0.5점 · 시드 1부터)은 **Core 의 `TeamBattleRunner`** 에 있다
        ///   (2026-08-23 이관). 전투 화면도 같은 것을 쓴다 — 두 곳에 두면 갈라지고, 그러면
        ///   **화면과 측정표가 서로 다른 승률을 말하면서 둘 다 "승률" 이라고 부르게** 된다.
        /// ⚠ 여기 남은 것은 요약에 없는 집계(반격 횟수)뿐이고, `onEach` 갈고리로 얹는다.
        /// </summary>
        private static double TeamWinRate(
            List<BattlePlacement> a, List<BattlePlacement> b, MultiTally tally)
        {
            TeamBattleSummary summary = TeamBattleRunner.Run(a, b, MultiFights, onEach: r =>
            {
                if (r.Outcome == TeamOutcome.Draw) tally.Draws++;

                tally.Fights++;
                tally.Rounds += r.Rounds;
                for (int i = 0; i < r.Log.Count; i++)
                {
                    CombatLogEntry e = r.Log[i];
                    if (e.Kind == CombatLogKind.Action
                        && !string.IsNullOrEmpty(e.Note) && e.Note.IndexOf("[반격]") >= 0)
                    {
                        tally.Counters++;
                    }
                }
            });
            return summary.TeamAWinRate;
        }

        private static bool HasCounterMorpheme(MartialArt art)
        {
            return art.Delta.CounterRate > 0;
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
                Metrics.Add("combat.전투길이." + row.Key.Trim(), avg);
                Metrics.Add("combat.무승부." + row.Key.Trim(), draws);
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

        private static string TierName(ArtTier t) => KoreanNames.Of(t);

        private static string Short(Discipline d) => KoreanNames.Of(d);

        /// <summary>⚠ 성향이 null 이면 강호무학이다 — 익힌 사람의 성향을 따르므로 무공 자체에는 성향이 없다.</summary>
        private static string Short(Alignment? a) => KoreanNames.Short(a);

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
