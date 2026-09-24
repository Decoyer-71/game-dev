using System.Collections.Generic;
using UnityEngine;

namespace Jianghu.Unity
{
    /// <summary>
    /// **한글을 띄우는 폰트 하나를 구해 온다.** 이 프로젝트의 모든 화면이 여기서 폰트를 받는다.
    ///
    /// ⚠⚠ **왜 OS 폰트인가 — 에디터 작업을 0 으로 만들기 위해서다.**
    ///   작업공간 `CLAUDE.md` §9 가 *"Unity 에디터 작업 = 가장 비쌈 · 사용자만 가능"* 이라 못박았다
    ///   (Unity MCP 미연결이라 Claude 가 에디터를 못 만진다). 그래서 화면 설계의 기준이 **클릭 수**이고,
    ///   OS 폰트를 런타임에 불러오면 **폰트 에셋 임포트도 굽기도 필요 없다.**
    ///
    /// ⛔ **기각한 안 — TMP + OS 폰트 런타임 SDF 굽기.** `TMP_FontAsset.CreateFontAsset(Font, ...)` 는
    ///   내부에서 `FontEngine.LoadFontFace` 를 부르는데 그것이 *"Include Font Data 를 켜라"* 를 요구한다.
    ///   그건 **에셋으로 임포트된 폰트 파일의 Import Settings** 항목인데,
    ///   <see cref="Font.CreateDynamicFontFromOSFont"/> 가 만든 것은 **OS 폰트 이름만 든 런타임 객체**라
    ///   그 설정 자체가 없다 — 구조적으로 만족할 수 없는 조합이다(설계 `docs/ui-martial-list-plan.md` §1-1).
    /// ⛔ **TMP Essentials 임포트도 코드로 우회할 수 없다** — `TMP_Text` 가 초기화 때 `TMP_Settings.instance`
    ///   를 역참조하는데 그 에셋을 만드는 것이 바로 그 임포트다. 없으면 `NullReferenceException` 이다(§1-2).
    ///
    /// ⚠ **대가**: 레거시 <see cref="UnityEngine.UI.Text"/> 는 SDF 가 아니라 **확대하면 흐려진다.**
    ///   고정 크기 목록에는 무해하다. 확대·축소가 필요해지면 TMP 정식 경로로 승격한다(설계 §1-4).
    /// </summary>
    public static class KoreanFont
    {
        /// <summary>
        /// 시도할 OS 폰트 이름 — **순서대로** 시도한다.
        ///
        /// ⚠⚠ **영문 이름과 한글 이름을 둘 다 넣는다.** OS 로케일에 따라 폰트 이름 문자열이 달라질 수 있다는 것이
        ///   설계 §1-3 이 지목한 위험이고, 이 목록이 그것을 흡수한다.
        /// ⚠ 맑은 고딕이 첫째인 이유 — Windows 기본 내장이라 가장 확실하다. 나눔고딕은 설치돼 있을 수도 있어 뒤에 둔다.
        /// </summary>
        private static readonly string[] Candidates =
        {
            "Malgun Gothic", "맑은 고딕",
            "NanumGothic", "나눔고딕",
            "Gulim", "굴림",
            "Dotum", "돋움",
            "Batang", "바탕",
        };

        /// <summary>이 크기로 OS 폰트를 만든다. 동적 폰트라 실제 표시 크기는 <c>Text.fontSize</c> 가 정한다.</summary>
        private const int BaseSize = 16;

        private static Font cached;

        /// <summary>
        /// 한글 폰트를 돌려준다. **한 번 구하면 캐시한다.**
        /// 전부 실패하면 <c>null</c> 이 아니라 **Unity 기본 폰트**를 돌려주고 <b>에러 로그를 크게 남긴다</b> —
        /// ⚠ 화면이 조용히 □ 로 뒤덮이는 것이 가장 나쁜 실패다. 무엇이 잘못됐는지 콘솔에 보여야 한다.
        /// </summary>
        public static Font Get()
        {
            if (cached != null) return cached;

            Font firstLoaded = null;

            for (int i = 0; i < Candidates.Length; i++)
            {
                Font font = Font.CreateDynamicFontFromOSFont(Candidates[i], BaseSize);
                if (font == null) continue;
                if (firstLoaded == null) firstLoaded = font;

                // 2-2-2 **null 이 아니라고 한글이 나오는 것이 아니다.** `CreateDynamicFontFromOSFont` 가
                //   실패했을 때 무엇을 돌려주는지 **공식 문서에 없다**(2026-08-09 docs-lookup 대조 - 문서 공백이 실재한다).
                //   그리고 OS 의 폰트 매칭이 이름이 비슷한 **다른 폰트를 조용히 골라 줄 수 있다.**
                //   그러면 null 검사는 통과하는데 화면은 사각형이 된다 - **가장 나쁜 실패는 조용한 실패다.**
                //   그래서 글리프를 직접 물어본다.
                if (!HasHangulGlyph(font))
                {
                    Debug.LogWarning("[KoreanFont] '" + Candidates[i] + "' 은(는) 잡혔지만 한글 글리프가 없다 - 다음 후보로 넘어간다.");
                    continue;
                }

                cached = font;
                Debug.Log("[KoreanFont] '" + Candidates[i] + "' 을(를) 썼다. (후보 " + (i + 1) + "/" + Candidates.Length + ")");
                return cached;
            }

            Debug.LogError(
                "[KoreanFont] 한글이 나오는 폰트를 하나도 못 찾았다 - 화면이 사각형으로 나올 것이다."
                + "\n  시도한 이름: " + string.Join(", ", Candidates)
                + "\n  이 PC 에 설치된 폰트 중 후보:\n    " + string.Join("\n    ", FindLikelyKoreanFonts()));

            // 그래도 **뭐라도** 돌려준다. 이름은 잡혔던 폰트가 있으면 그것을, 없으면 Unity 내장 폰트를.
            //   내장 폰트 이름은 2022.3.1 에서 `Arial.ttf` -> `LegacyRuntime.ttf` 로 바뀌었다.
            //   이 문자열은 **커뮤니티 출처로만 확인**했다(공식 레퍼런스 페이지 접근 실패). null 이면 그것도 로그로 남긴다.
            cached = firstLoaded;
            if (cached == null)
            {
                cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (cached == null) Debug.LogError("[KoreanFont] 내장 폰트 'LegacyRuntime.ttf' 도 못 찾았다 - 이름이 바뀐 것 같다.");
            }
            return cached;
        }

        /// <summary>
        /// 이 폰트로 한글이 실제로 그려지는가.
        ///
        /// 동적 폰트는 **요청해야 글리프가 아틀라스에 들어온다.** 그래서 먼저
        /// <see cref="Font.RequestCharactersInTexture"/> 로 청하고 나서 물어본다.
        /// 순서를 바꾸면 있는 폰트도 없다고 나온다.
        ///
        /// 어림짐작이 하나 섞여 있다 - `advance > 0` 을 "글자가 있다" 로 읽는다.
        /// 없는 글자를 대체 문자로 그리는 폰트라면 이 검사를 통과할 수 있다.
        /// 완벽한 판정이 아니라 **조용한 실패를 줄이는 그물**이다.
        /// </summary>
        private static bool HasHangulGlyph(Font font)
        {
            const string Probe = "가한무공";   // 흔한 음절 + 이 게임이 실제로 쓰는 글자

            font.RequestCharactersInTexture(Probe, BaseSize);
            for (int i = 0; i < Probe.Length; i++)
            {
                CharacterInfo info;
                if (!font.GetCharacterInfo(Probe[i], out info, BaseSize)) return false;
                if (info.advance <= 0) return false;
            }
            return true;
        }

        /// <summary>
        /// 실패했을 때 **다음에 무엇을 넣어야 하는지**를 콘솔에 보여 주기 위한 진단.
        /// OS 에 깔린 폰트 이름 중 한글 폰트로 보이는 것을 추린다.
        /// ⚠ 추리는 기준은 어림짐작이다 — 이름에 한글이 있거나 알려진 한글 폰트 계열 낱말을 포함하는 것.
        /// </summary>
        private static List<string> FindLikelyKoreanFonts()
        {
            var hits = new List<string>();
            string[] installed = Font.GetOSInstalledFontNames();
            string[] hints = { "Gothic", "Myeongjo", "Nanum", "Malgun", "Gulim", "Dotum", "Batang", "Pretendard", "Noto Sans K" };

            for (int i = 0; i < installed.Length && hits.Count < 20; i++)
            {
                string name = installed[i];
                if (HasHangul(name)) { hits.Add(name); continue; }

                for (int h = 0; h < hints.Length; h++)
                {
                    if (name.IndexOf(hints[h], System.StringComparison.OrdinalIgnoreCase) >= 0) { hits.Add(name); break; }
                }
            }

            if (hits.Count == 0) hits.Add("(후보를 못 찾았다 — 설치된 폰트 " + installed.Length + "개)");
            return hits;
        }

        private static bool HasHangul(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] >= 0xAC00 && s[i] <= 0xD7A3) return true;   // 한글 음절
            }
            return false;
        }
    }
}
