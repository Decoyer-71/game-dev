using System;
using System.Collections.Generic;

namespace Jianghu.Core.Martial.Morphemes
{
    /// <summary>
    /// 무공명을 형태소로 분해한다. 정의서 §2 의 파서다.
    ///
    /// 순서가 전부다:
    ///   1. **접미사를 최장일치로 먼저 뗀다** — 안 그러면 `천양신공` 의 신을 迅 으로 오독한다(설계안 §1-F)
    ///   2. 남은 본체를 **한 글자씩** 사전에서 찾는다 — 미등록 글자는 삼키지 않고 실패로 보고한다(R1)
    ///   3. 수치를 합산한다 — 극한경지 종(宗)이 있으면 무공형태만 2배
    ///
    /// ⚠ **순서는 보지 않는다.** 정의서 §2-1 이 *"구성은 통제하고 순서는 자유롭게"* 로 정했다.
    ///   무공명이 서술적이라(`창천낙월` = 하늘을 찔러 달을 떨어뜨린다) 위치를 고정하면 어감이 죽는다.
    ///   순서가 의미를 갖는 곳은 상성 규칙(부정 + 무학분류 인접) 하나뿐이다.
    /// </summary>
    public static class MorphemeParser
    {
        /// <summary>
        /// 유효 형태소 1자당 기력 소모(2026-07-29 결정 B).
        ///
        /// ⚠⚠ **2026-08-01 측정 근거로 4 → 3 으로 내렸다(사용자 확정).**
        ///   **계층이 역전돼 있었다** — 계층은 곧 형태소 개수인데(강호 2 · 소문파 3 · 대문파 3~4),
        ///   회복이 턴당 10 이라 턴당 순기력이 **2자 +2 / 3자 −2 / 4자 −6** 이었다.
        ///   즉 네 번째 형태소가 주는 능력이 기력 비용을 못 갚아, 글자 수만 바꿔 재면
        ///   **3자가 4자를 이겼다**(51.2 / 50.5 / 52.8 — 무공 3성·6성·10성).
        ///   3 으로 내리면 6/9/12 가 되어 순기력 +4 / +1 / −2 가 되고, 재측정에서
        ///   **3자 vs 4자 39.2%**(4자가 60.8% 로 이긴다)로 역전이 해소됐다.
        ///
        /// ⚠ **회복(10)이나 최대기력(50)을 올리는 쪽은 택하지 않았다.** `verify` 가 잡았다 —
        ///   그쪽은 기력소실 형태소 탈(奪, 세기 14)이 기대는 *회복과의 격차*를 줄여
        ///   최대기력을 50→80 으로 올렸다가 탈이 46.5% 로 죽어 되돌린 사례를 반복한다.
        ///   소모 상수는 그 격차를 건드리지 않는다.
        ///
        /// ⚠ 여전히 **평타 전락률**(목표 10~30%)로 판정한다(설계안 §5-3).
        ///   기력 고갈 → 평타 전락이 현재 전투의 핵심 드라마이므로, 이 상수가 0에 가까우면 그 드라마가 사라진다.
        /// </summary>
        public const int QiCostPerMorpheme = 3;

        /// <summary>
        /// 무공명을 분해한다. 분해할 수 없으면 예외를 던진다.
        ///
        /// ⚠ 조합 규칙(§2-2)은 여기서 보지 않는다. <see cref="ArtCompositionRule"/> 이 따로 판정한다.
        /// </summary>
        public static ParsedArtName Parse(string name)
        {
            return Parse(name, null);
        }

        /// <summary>
        /// 무공 종류를 명시해 분해한다. **접미사 없는 문파 무공용**이다.
        /// </summary>
        public static ParsedArtName Parse(string name, ArtKind? kind)
        {
            ParsedArtName parsed;
            IReadOnlyList<string> problems;
            if (!TryParse(name, kind, out parsed, out problems))
            {
                throw new ArgumentException(
                    "무공명 '" + name + "' 을(를) 분해할 수 없다: " + string.Join(" · ", problems), nameof(name));
            }
            return parsed;
        }

        /// <summary>
        /// 무공명을 분해하되 **실패를 예외 대신 목록으로** 돌려준다.
        ///
        /// 역산 리포트(설계안 §5-2)를 위한 경로다. 기존 무공 36개를 넣고 *"사전에 무엇이 빠졌는가"* 를
        /// 한 번에 보려면, 첫 실패에서 멈추지 않고 끝까지 훑어야 한다.
        /// </summary>
        public static bool TryParse(string name, out ParsedArtName parsed, out IReadOnlyList<string> problems)
        {
            return TryParse(name, null, out parsed, out problems);
        }

        /// <summary>
        /// 무공 종류를 명시해 분해하되 실패를 목록으로 돌려준다.
        /// </summary>
        /// <param name="kind">
        /// null 이면 **접미사에서** 종류를 얻는다(강호무학).
        /// 값을 주면 **접미사를 떼지 않고 이름 전체를 본체로** 본다(문파 무공).
        ///
        /// ⚠ 문파 무공에서 접미사를 떼지 않는 것은 실수가 아니다 — 접미사가 강호무학 전용이므로,
        ///   문파 무공 이름 끝의 글자는 접미사가 아니라 **형태소여야 한다.**
        ///   접미사처럼 생긴 글자로 끝나면 (보·공·결 등은 형태소가 아니므로) 미등록 글자로 걸린다.
        /// </param>
        public static bool TryParse(
            string name, ArtKind? kind, out ParsedArtName parsed, out IReadOnlyList<string> problems)
        {
            parsed = null;
            var found = new List<string>();
            problems = found;

            if (string.IsNullOrEmpty(name))
            {
                found.Add("무공명이 비어 있다");
                return false;
            }

            ArtSuffix suffix = null;
            string bodyText = name;

            if (kind == null)
            {
                if (!ArtSuffixCatalog.TryStrip(name, out suffix, out bodyText))
                {
                    found.Add("접미사가 없다 (강호무학은 정의서 §2-4 의 관례 중 하나로 끝나야 한다). "
                              + "문파 무공이라면 무공 종류를 명시해 분해할 것");
                    return false;
                }
            }
            else
            {
                // 문파 무공도 **1자 접미사는 쓴다** — `신풍양` → `신풍양공` 처럼 내공·경공 이름이 자연스러워진다.
                // 접미사는 본체 글자 수에 들어가지 않으므로(§2-2 규칙 1) **형태소가 줄지 않는다.**
                // ⚠ 무기 접미사(`~검법` 등 2자)는 강호무학 전용이라 여기서 떼지 않는다. 그리고 그 제한이
                //   안전장치이기도 하다 — 2자 접미사에는 형태소와 겹치는 글자(신·창)가 있어 잘못 떼면
                //   형태소를 삼킨다. 1자 접미사(보·공·결·법·술)는 어느 것도 형태소가 아니다.
                // ⚠ 접미사가 가리키는 종류는 무시하고 **명시된 종류가 이긴다.** 1자 접미사는 어감용이라
                //   `법`·`술` 처럼 종류를 단정할 수 없는 글자가 섞여 있기 때문이다.
                ArtSuffixCatalog.TryStrip(name, ArtSuffixCatalog.SchoolArtSuffixMaxLength, out suffix, out bodyText);
            }

            var body = new List<Morpheme>(bodyText.Length);
            for (int i = 0; i < bodyText.Length; i++)
            {
                char c = bodyText[i];
                Morpheme m;
                if (!MorphemeDictionary.TryGet(c, out m))
                {
                    // ⚠⚠ R1 — 여기서 조용히 넘어가면 배경어 화이트리스트가 무의미해진다.
                    //   미등록 글자는 "배경어" 가 아니라 "사전에 없는 글자" 이고, 그 사실이 보고돼야
                    //   사전의 구멍이 드러난다.
                    found.Add("미등록 글자 '" + c + "'");
                    continue;
                }
                body.Add(m);
            }

            if (found.Count > 0) return false;

            parsed = Assemble(name, suffix, kind ?? suffix.Kind, body);
            return true;
        }

        private static ParsedArtName Assemble(string name, ArtSuffix suffix, ArtKind kind, List<Morpheme> body)
        {
            // 종(宗) — 무공형태의 효과를 페널티까지 함께 2배로 만든다(정의서 §3-8).
            bool doublesForm = false;
            for (int i = 0; i < body.Count; i++)
            {
                if (body[i].DoublesFormEffect) doublesForm = true;
            }

            ArtStatDelta total = ArtStatDelta.Zero;
            int backgroundCount = 0;
            var counterTargets = new List<ArtLineage>();
            ArtLineage ownLineage = ArtLineage.None;

            for (int i = 0; i < body.Count; i++)
            {
                Morpheme m = body[i];

                if (m.IsBackground) backgroundCount++;

                total += (doublesForm && m.Category == MorphemeCategory.Form) ? m.Delta * 2 : m.Delta;

                if (m.Lineage == ArtLineage.None) continue;

                // 상성(§4) — 부정 한자 **바로 앞에** 있을 때만 상성이 된다. 인접 조건이 핵심이며,
                // 이름의 의미와 기계 규칙을 일치시키기 위한 것이다. 무엇을 부정하는지가 이름에 명시돼야 한다.
                bool negatedHere = i > 0 && body[i - 1].IsNegation;
                if (negatedHere) counterTargets.Add(m.Lineage);
                else ownLineage = m.Lineage;
            }

            return new ParsedArtName(name, suffix, kind, body, total, backgroundCount, ownLineage, counterTargets);
        }
    }
}
