using System.Collections.Generic;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;
using NUnit.Framework;

namespace Jianghu.Tests.Martial
{
    /// <summary>
    /// **사전과 카탈로그가 성한가.** 밸런스가 아니라 **무결성**을 본다.
    ///
    /// ⚠⚠ 이 파일이 존재하는 이유(2026-08-02, 사용자 지적) —
    ///   *"오탈자 신경써서 봐줘. 사전에 잘못된 글자가 등록돼 있으면 아무리 테스트해도 안 고쳐진다."*
    ///
    ///   맞는 지적이다. 이 프로젝트는 **무공명이 곧 데이터**라(정의서 §0) 이름에 오탈자가 하나 있으면
    ///   그 무공의 수치 전체가 조용히 달라지는데, 승률 테스트는 그것을 **정상값으로 통과시킨다.**
    ///   밸런스 테스트가 아무리 많아도 이 층은 못 잡는다.
    ///
    /// ⚠ 그리고 **파서의 키는 한자가 아니라 한글 한 글자**다(HANDOFF §0). 한글이 깨지면
    ///   `MorphemeDictionary` 의 조회가 통째로 어긋난다.
    /// </summary>
    public class CatalogIntegrityTests
    {
        /// <summary>
        /// 완성형 한글 음절 영역 `가`(U+AC00) ~ `힣`(U+D7A3). 자모(ㄱ·ㅏ)나 옛한글은 여기 들어오지 않는다.
        ///
        /// ⚠⚠ **문자 리터럴이 아니라 코드포인트로 적는다** (2026-08-02). 원래 `'가'`·`'힣'` 로 적었는데
        ///   `dotnet test` 는 통과하고 **Unity Test Runner 에서만 실패**했다. 이 파일이 읽히는 인코딩이나
        ///   비교 구현이 두 환경에서 갈릴 수 있다는 뜻이고, **한글 리터럴 자체가 검사 대상인 테스트에서
        ///   한글 리터럴을 기준으로 쓰면 기준과 대상이 같이 깨진다.**
        /// </summary>
        private const int HangulFirst = 0xAC00;
        private const int HangulLast = 0xD7A3;

        [Test]
        public void 사전의_모든_키가_완성형_한글_한_글자다()
        {
            // ⚠ 하나씩 Assert 하지 않고 **전부 모아서** 보고한다. 첫 실패에서 멈추면
            //   *"몇 글자가 깨졌는지"* 를 알 수 없고, 오탈자는 보통 한 번에 여러 개 들어온다.
            var failures = new List<string>();
            foreach (Morpheme m in MorphemeDictionary.All)
            {
                int code = m.Korean;
                if (code >= HangulFirst && code <= HangulLast) continue;

                failures.Add("U+" + code.ToString("X4") + " (" + m.Hanja + " " + m.Meaning
                             + ", " + m.Category + ")");
            }

            Assert.IsEmpty(failures,
                "완성형 한글이 아닌 사전 키가 있다 — 자모가 섞였거나 오타다. "
                + "무공명이 곧 데이터라 이런 글자 하나가 그 무공의 수치를 통째로 바꾼다:\n  "
                + string.Join("\n  ", failures.ToArray()));
        }

        [Test]
        public void 사전에_중복된_키가_없다()
        {
            var seen = new Dictionary<char, string>();
            foreach (Morpheme m in MorphemeDictionary.All)
            {
                Assert.IsFalse(seen.ContainsKey(m.Korean),
                    "사전 키 '" + m.Korean + "' 이 둘 이상이다 — "
                    + (seen.ContainsKey(m.Korean) ? seen[m.Korean] : "") + " vs " + m.Meaning
                    + ". 나중 것이 앞의 것을 덮어써 한쪽이 조용히 죽는다.");
                seen[m.Korean] = m.Meaning;
            }
        }

        [Test]
        public void 카탈로그의_모든_무공이_자기_종류로_분해된다()
        {
            var failures = new List<string>();
            foreach (MartialArt art in MartialArtCatalog.All)
            {
                ParsedArtName parsed;
                IReadOnlyList<string> problems;
                if (MorphemeParser.TryParse(art.Name, KindOf(art), out parsed, out problems)) continue;
                failures.Add(art.Name + " — " + string.Join(" · ", problems));
            }

            Assert.IsEmpty(failures,
                "분해되지 않는 무공이 있다. 이름의 오탈자이거나 사전에 없는 글자다:\n  "
                + string.Join("\n  ", failures.ToArray()));
        }

        [Test]
        public void 카탈로그의_모든_무공이_조합_규칙을_지킨다()
        {
            var failures = new List<string>();
            foreach (MartialArt art in MartialArtCatalog.All)
            {
                ParsedArtName parsed;
                IReadOnlyList<string> problems;
                if (!MorphemeParser.TryParse(art.Name, KindOf(art), out parsed, out problems)) continue;

                IReadOnlyList<ArtRuleViolation> violations = ArtCompositionRule.Validate(parsed, art.Tier);
                for (int i = 0; i < violations.Count; i++)
                {
                    failures.Add(art.Name + " — " + violations[i].Message);
                }
            }

            Assert.IsEmpty(failures,
                "조합 규칙을 어기는 무공이 있다:\n  " + string.Join("\n  ", failures.ToArray()));
        }

        [Test]
        public void 무공을_수련하면_약해지지_않는다()
        {
            // ⚠⚠ 2026-08-02 — 이 단언이 처음 만들어졌을 때 **7종이 깨졌다.**
            //   `totalPower = (Stats.Attack + Delta.Attack × 숙련배율)` 인데 공격 합이 음수인 무공이
            //   있어서, 수련할수록(배율 ↑) 총 위력이 **줄었다.** 실측으로 궤격비전이 3성 상대에게
            //   **1성 78.0% → 10성 1.2%** 였다 — 설계안 §1-E 의 *"수련해도 세지지 않는 무공"* 보다 나쁘다.
            //
            //   해결은 **엔진의 공격 기여 하한 0**(`CombatResolver.DamagePerHit`)이다. 사전 값을 고치는
            //   길도 있었지만 범위 무공(−2.75)은 그것으로 못 닫힌다(사용자 확정).
            //
            // ⚠ 그래서 이 테스트는 **사전 값이 아니라 실제 동작**을 본다 — 음수 공격 합 자체는
            //   여전히 존재하고, 그것이 해롭지 않다는 것이 우리가 보장하는 성질이다.
            var failures = new List<string>();
            foreach (MartialArt art in MartialArtCatalog.Techniques())
            {
                if (art.Delta.Attack >= 0) continue;      // 음수인 것만 보면 된다

                double atNovice = art.Delta.Attack * new LearnedArt(art, 0, Alignment.Orthodox).PowerMultiplier;
                double atMaster = art.Delta.Attack
                    * new LearnedArt(art, AlignmentCurve.SessionsToReach(
                        Alignment.Orthodox, MartialStage.ProficiencyForStage(MartialStage.MaxStage)),
                        Alignment.Orthodox).PowerMultiplier;

                // 하한이 걸리면 둘 다 0 이 되어 "수련해도 안 약해진다" 가 성립한다.
                double flooredNovice = atNovice < 0 ? 0 : atNovice;
                double flooredMaster = atMaster < 0 ? 0 : atMaster;
                if (flooredMaster >= flooredNovice) continue;

                failures.Add(art.Name + " 공격합 " + art.Delta.Attack.ToString("F2")
                             + " → 초심 " + flooredNovice.ToString("F2") + " · 만렙 " + flooredMaster.ToString("F2"));
            }

            Assert.IsEmpty(failures,
                "수련할수록 약해지는 무공이 있다(설계안 §1-E). "
                + "`CombatResolver.DamagePerHit` 의 공격 기여 하한이 사라졌는지 확인할 것:\n  "
                + string.Join("\n  ", failures.ToArray()));
        }

        [Test]
        public void 공격_합이_음수인_무공은_범위_아니면_기만을_쓴_것이다()
        {
            // ⚠ 음수 자체는 막지 않는다(위 테스트가 그것이 해롭지 않음을 보장한다). 다만 **어디서 오는지**는
            //   고정해 둔다 — 새 형태소가 공격을 파는 축을 몰래 늘리면 여기서 걸린다.
            //   현재 공격을 파는 글자는 기만(幻·詭 −0.75)과 범위(다 −1 · 군 −2 · 전 −3) 둘뿐이다.
            var failures = new List<string>();
            foreach (MartialArt art in MartialArtCatalog.Techniques())
            {
                if (art.Delta.Attack >= 0) continue;

                ParsedArtName parsed;
                IReadOnlyList<string> problems;
                if (!MorphemeParser.TryParse(art.Name, KindOf(art), out parsed, out problems)) continue;

                bool explained = parsed.Scope != AttackScope.Single;
                for (int i = 0; i < parsed.Body.Count; i++)
                {
                    if (parsed.Body[i].Delta.Attack < 0) explained = true;
                }
                if (explained) continue;

                failures.Add(art.Name + " 공격합 " + art.Delta.Attack.ToString("F2"));
            }

            Assert.IsEmpty(failures,
                "공격을 파는 글자(기만·범위) 없이 공격 합이 음수인 무공이 있다 — "
                + "새 형태소가 공격 페널티를 들여왔는지 확인할 것:\n  "
                + string.Join("\n  ", failures.ToArray()));
        }

        /// <summary>강호무학은 접미사에서 종류를 얻고, 문파 무공은 유형에서 얻는다.</summary>
        private static ArtKind? KindOf(MartialArt art)
        {
            if (art.Tier == ArtTier.Wanderer) return null;
            if (art.Discipline == Discipline.InnerArt) return ArtKind.Internal;
            if (art.Discipline == Discipline.Movement) return ArtKind.Movement;
            return ArtKind.Attack;
        }
    }
}
