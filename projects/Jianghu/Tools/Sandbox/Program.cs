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
        private const double DominantThreshold = 0.65;
        private const double DeadThreshold = 0.35;

        private static void Main()
        {
            Console.OutputEncoding = Encoding.UTF8;

            PrintMorphemeReport();

            List<MartialArt> techniques = MartialArtCatalog.Techniques();
            Console.WriteLine("무공 " + MartialArtCatalog.All.Count + "종(공격 초식 " + techniques.Count + ")"
                              + " · 문파 " + SchoolCatalog.All.Count + "곳 · 매치업당 " + FightsPerMatchup + "전");

            foreach (int sessions in new[] { 100, 200 })
            {
                Console.WriteLine();
                Console.WriteLine("████ 수련 " + sessions + "회 시점 ████");
                foreach (SchoolTier tier in new[] { SchoolTier.Wanderer, SchoolTier.Minor, SchoolTier.Major })
                {
                    PrintRanking(techniques, tier, sessions);
                }
                PrintCrossTier(techniques, sessions);
            }

            PrintSampleBattle(200);
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

        // ─────────────────────────── 캐릭터 구성 ───────────────────────────

        private static CharacterStats Stats()
        {
            return new CharacterStats(maxHealth: 200, maxQi: 40, attack: 20, defense: 25, agility: 12);
        }

        private static Combatant ToCombatant(MartialArt art, int sessions)
        {
            var arts = new List<LearnedArt> { new LearnedArt(art, sessions) };
            // 한 무공을 수련하면 성향 숙련과 유형 숙달이 함께 오른다는 전제.
            var masteries = new List<DisciplineMastery> { new DisciplineMastery(art.Discipline, sessions) };
            return new Combatant(art.Name, Stats(), arts, masteries);
        }

        // ─────────────────────────── 측정 ───────────────────────────

        private static double WinRate(Combatant a, Combatant b)
        {
            double score = 0;
            for (uint seed = 1; seed <= FightsPerMatchup; seed++)
            {
                CombatResult r = CombatResolver.Resolve(a, b, new XorShiftRandom(seed));
                if (r.Outcome == CombatOutcome.AttackerWin) score += 1.0;
                else if (r.Outcome == CombatOutcome.Draw) score += 0.5;
            }
            return score / FightsPerMatchup;
        }

        /// <summary>한 계층 안에서 전수 대전을 돌려 순위를 낸다.</summary>
        private static void PrintRanking(List<MartialArt> all, SchoolTier tier, int sessions)
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
            for (int i = 0; i < group.Count; i++) fighters.Add(ToCombatant(group[i], sessions));

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
        private static void PrintCrossTier(List<MartialArt> all, int sessions)
        {
            var byTier = new Dictionary<SchoolTier, List<MartialArt>>();
            foreach (SchoolTier t in new[] { SchoolTier.Wanderer, SchoolTier.Minor, SchoolTier.Major })
            {
                byTier[t] = new List<MartialArt>();
            }
            for (int i = 0; i < all.Count; i++) byTier[all[i].Tier].Add(all[i]);

            Console.WriteLine();
            Console.WriteLine("── 계층 간 (상위가 이기는 것이 정상. 다만 90% 이상이면 하위 계층이 무의미해진다) ──");

            Report(byTier, SchoolTier.Minor, SchoolTier.Wanderer, sessions);
            Report(byTier, SchoolTier.Major, SchoolTier.Minor, sessions);
            Report(byTier, SchoolTier.Major, SchoolTier.Wanderer, sessions);
        }

        private static void Report(
            Dictionary<SchoolTier, List<MartialArt>> byTier, SchoolTier high, SchoolTier low, int sessions)
        {
            List<MartialArt> hi = byTier[high];
            List<MartialArt> lo = byTier[low];
            if (hi.Count == 0 || lo.Count == 0) return;

            double sum = 0;
            int n = 0;
            for (int i = 0; i < hi.Count; i++)
            {
                Combatant h = ToCombatant(hi[i], sessions);
                for (int j = 0; j < lo.Count; j++)
                {
                    sum += WinRate(h, ToCombatant(lo[j], sessions));
                    n++;
                }
            }
            double rate = sum / n * 100;
            string flag = rate >= 90 ? "  ⚠ 하위 계층이 무의미해짐" : rate <= 50 ? "  ⚠ 상위 계층 이점이 없음" : "";
            Console.WriteLine("  " + Pad(TierName(high) + " vs " + TierName(low), 28)
                              + rate.ToString("F1").PadLeft(6) + "%" + flag);
        }

        // ─────────────────────────── 표본 로그 ───────────────────────────

        private static void PrintSampleBattle(int sessions)
        {
            Console.WriteLine();
            Console.WriteLine("══════ 전투 로그 표본 (수련 " + sessions + "회, 시드 42) ══════");

            // 성격이 가장 대비되는 둘: 마도 경직·마비(천마검결) vs 사파 중독(절명비도)
            MartialArt aArt = MartialArtCatalog.ById("cm_geomgyeol");
            MartialArt bArt = MartialArtCatalog.ById("sm_jeolmyeong");
            if (aArt == null || bArt == null)
            {
                Console.WriteLine("  ⚠ 표본 무공 ID 를 찾지 못했다. 카탈로그가 바뀌었는지 확인할 것.");
                return;
            }

            CombatResult r = CombatResolver.Resolve(
                ToCombatant(aArt, sessions), ToCombatant(bArt, sessions), new XorShiftRandom(42u));
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
