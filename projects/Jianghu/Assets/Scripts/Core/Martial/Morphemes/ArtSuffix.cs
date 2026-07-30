using System.Collections.Generic;

namespace Jianghu.Core.Martial.Morphemes
{
    /// <summary>
    /// 무공의 종류. 정의서 §2-2 규칙 3(종류별 필수 구성)의 세 행에 대응한다.
    /// 무공명의 **접미사에서** 결정된다 — `~검법` 이면 공격, `~신공` 이면 내공이다.
    /// </summary>
    public enum ArtKind
    {
        /// <summary>공격 무공 — 공격방식 1 + **무공형태 1** 필수.</summary>
        Attack = 0,

        /// <summary>내공 무공 — 내공(양·음·합) 1 필수.</summary>
        Internal = 1,

        /// <summary>경공 무공 — 방어(막기·회피·반격) 1 필수.</summary>
        Movement = 2,
    }

    /// <summary>
    /// 무공명 접미사(정의서 §2-4). `천양신공` 의 `신공` 이 여기 해당한다.
    ///
    /// ⚠⚠ **접미사를 본체보다 먼저, 최장일치로 떼어내야 한다.** 그러지 않으면 오독한다:
    ///   `천양<b>신</b>공` 의 신을 형태소 신(迅, 속도+2)으로 읽어버린다.
    ///   접미사와 본체 글자가 겹치는 것은 신(~신법·~신공 ↔ 迅)과 창(~창법 ↔ 槍) 두 건이며,
    ///   §2-4 접미사 17종을 형태소 75자와 전수 대조해 확인했다(설계안 §1-F).
    /// </summary>
    public sealed class ArtSuffix
    {
        /// <summary>접미사 문자열. 예: "권법" · "보".</summary>
        public string Text { get; }

        /// <summary>이 접미사가 정하는 무공 종류.</summary>
        public ArtKind Kind { get; }

        /// <summary>
        /// 대응하는 유형(類型). <see cref="HasDiscipline"/> 이 false 면 의미가 없다.
        /// </summary>
        public Discipline Discipline { get; }

        /// <summary>
        /// 유형이 정해져 있는가.
        ///
        /// ⚠ `~봉법`·`~편법` 만 false 다. 정의서 §2-4 에는 있으나 현재 <see cref="Discipline"/> 7종에
        ///   대응이 없다. **임의로 매핑하지 않는다** — 봉(棒)은 타격 무기라 도(刀)로 보내면 성격이 어긋나고,
        ///   그렇다고 권(拳)에 넣는 것도 근거가 약하다. 애초에 봉·편을 쓰는 무공을 만들지 않는 선택도 있다.
        ///   **무공명 122개를 짓는 단계에서 결정한다**(설계안 §1-G).
        ///   그때까지 이 접미사는 인식은 되지만 유형을 내놓지 못한다 — 조용히 틀린 유형을 주는 것보다 낫다.
        /// </summary>
        public bool HasDiscipline { get; }

        public ArtSuffix(string text, ArtKind kind, Discipline discipline, bool hasDiscipline = true)
        {
            Text = text;
            Kind = kind;
            Discipline = discipline;
            HasDiscipline = hasDiscipline;
        }

        public override string ToString()
        {
            return "~" + Text;
        }
    }

    /// <summary>
    /// 접미사 사전(정의서 §2-4 + 설계안 §1-G 의 `~표법` 보강).
    ///
    /// ⚠ **`~비도` 를 쓰지 않는다.** 비도 계열 접미사가 정의서에 빠져 있어 보강이 필요했지만,
    ///   `~비도` 는 형태소 비(痺, 마비)와 세 번째 접미사 충돌을 만든다. 같은 일을 하면서
    ///   충돌이 없는 `~표법`(飛鏢) 을 쓴다.
    /// </summary>
    public static class ArtSuffixCatalog
    {
        private static readonly ArtSuffix[] Suffixes =
        {
            // ── 격투 (§2-4) ──
            new ArtSuffix("권법", ArtKind.Attack, Discipline.Fist),
            new ArtSuffix("장법", ArtKind.Attack, Discipline.Fist),
            new ArtSuffix("지법", ArtKind.Attack, Discipline.Fist),
            new ArtSuffix("조법", ArtKind.Attack, Discipline.Fist),
            new ArtSuffix("각법", ArtKind.Attack, Discipline.Fist),

            // ── 무기 (§2-4) ──
            new ArtSuffix("검법", ArtKind.Attack, Discipline.Sword),
            new ArtSuffix("도법", ArtKind.Attack, Discipline.Blade),
            new ArtSuffix("창법", ArtKind.Attack, Discipline.Spear),
            new ArtSuffix("봉법", ArtKind.Attack, Discipline.Fist, hasDiscipline: false),
            new ArtSuffix("편법", ArtKind.Attack, Discipline.Fist, hasDiscipline: false),

            // ── 비도 (설계안 §1-G 보강) ──
            new ArtSuffix("표법", ArtKind.Attack, Discipline.Dagger),

            // ── 이동 (§2-4) ──
            new ArtSuffix("경공", ArtKind.Movement, Discipline.Movement),
            new ArtSuffix("신법", ArtKind.Movement, Discipline.Movement),
            new ArtSuffix("보", ArtKind.Movement, Discipline.Movement),

            // ── 내공 (§2-4) ──
            new ArtSuffix("신공", ArtKind.Internal, Discipline.InnerArt),
            new ArtSuffix("심법", ArtKind.Internal, Discipline.InnerArt),
            new ArtSuffix("공", ArtKind.Internal, Discipline.InnerArt),
            new ArtSuffix("결", ArtKind.Internal, Discipline.InnerArt),
            new ArtSuffix("법", ArtKind.Internal, Discipline.InnerArt),

            // ── 1자 보강 (2026-07-29) ──
            // 문파 무공의 내공·경공에 접미사를 붙이면 이름이 자연스러워진다(`신풍양` → `신풍양공`).
            // ⚠ 접미사는 본체 글자 수에 들어가지 않으므로(§2-2 규칙 1 은 "접미사 제외") **형태소가 줄지 않는다.**
            //   즉 접미사는 성능을 깎지 않고 어감만 얻는 공짜 장치다.
            new ArtSuffix("술", ArtKind.Movement, Discipline.Movement),
        };

        /// <summary>
        /// 문파 무공이 쓸 수 있는 접미사의 최대 길이(글자).
        ///
        /// ⚠⚠ **무기 접미사(`~검법`·`~도법`)는 강호무학 전용**이다(2026-07-29 사용자 결정).
        ///   문파 무공은 `보`·`공`·`결`·`법`·`술` 같은 **1자 접미사만** 쓴다.
        ///   길이를 제한하는 것이 안전장치이기도 하다 — 2자 접미사에는 형태소와 겹치는 글자(신·창)가 들어 있어,
        ///   문파 무공에서 잘못 떼면 형태소를 통째로 삼킨다. 1자 접미사(보·공·결·법·술)는
        ///   **어느 것도 형태소가 아니므로** 그 사고가 구조적으로 불가능하다.
        /// </summary>
        public const int SchoolArtSuffixMaxLength = 1;

        /// <summary>등록된 접미사 전체.</summary>
        public static IReadOnlyList<ArtSuffix> All => Suffixes;

        /// <summary>
        /// 무공명 끝에서 접미사를 **최장일치로** 떼어낸다.
        ///
        /// ⚠⚠ 최장일치가 핵심이다. `천양신공` 에서 `공`(1자)이 아니라 `신공`(2자)을 떼야
        ///   남은 본체가 `천양` 이 되고, `신` 이 형태소 迅 으로 오독되지 않는다.
        /// </summary>
        /// <returns>접미사를 찾았으면 true. 그때 <paramref name="body"/> 는 접미사를 뗀 나머지다.</returns>
        public static bool TryStrip(string name, out ArtSuffix suffix, out string body)
        {
            return TryStrip(name, int.MaxValue, out suffix, out body);
        }

        /// <summary>
        /// 길이 상한을 두고 접미사를 뗀다. 문파 무공은 <see cref="SchoolArtSuffixMaxLength"/> 를 넘겨
        /// **1자 접미사만** 떼게 한다.
        /// </summary>
        public static bool TryStrip(string name, int maxLength, out ArtSuffix suffix, out string body)
        {
            suffix = null;
            body = name;
            if (string.IsNullOrEmpty(name)) return false;

            for (int i = 0; i < Suffixes.Length; i++)
            {
                ArtSuffix candidate = Suffixes[i];
                if (candidate.Text.Length > maxLength) continue;

                // 접미사가 이름 전체인 경우는 본체가 비므로 무공명이 될 수 없다. 걸러낸다.
                if (name.Length <= candidate.Text.Length) continue;
                if (!name.EndsWith(candidate.Text, System.StringComparison.Ordinal)) continue;

                if (suffix == null || candidate.Text.Length > suffix.Text.Length)
                {
                    suffix = candidate;
                }
            }

            if (suffix == null) return false;

            body = name.Substring(0, name.Length - suffix.Text.Length);
            return true;
        }
    }
}
