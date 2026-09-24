using Jianghu.Core.Martial.Morphemes;

namespace Jianghu.Core.Martial.Display
{
    /// <summary>
    /// **enum 을 사람이 읽는 한글로 바꾼다.** 표시 전용이며 게임 규칙을 하나도 만지지 않는다.
    ///
    /// ⚠⚠ **왜 Core 에 있는가** — Phase 4 첫 화면(무공 목록)이 `Fist` · `Single` · `Front` 를
    ///   그대로 뱉는 것을 보고 만들었다. 그런데 진짜 문제는 *"영문이 샌다"* 가 아니라
    ///   **같은 표가 네 곳에 따로 있었다**는 것이다:
    ///   `Sandbox.DisciplineName` · `Sandbox.Short(Discipline)`(같은 파일 안에 중복) ·
    ///   `ArtCompositionRule.KindName` · `ParsedArtName.KindLabel`.
    ///   Unity 층에 다섯 번째를 만들면 갈라지는 것이 확정이므로 **여기 한 곳으로 모은다.**
    ///   Core 라서 `dotnet test` 가 닿는다는 것도 이유다 — Unity 층은 그 검증을 못 받는다.
    ///
    /// ⚠⚠ **문자열을 함부로 바꾸지 마라.** Sandbox 가 지표 키를 이 이름으로 짓는다
    ///   (`tier.전승무학.유형.비도.평균.10성`). 한 글자만 바꿔도 `docs/baseline-metrics.txt` 와의
    ///   대조가 통째로 끊긴다(§5-D). 바꿔야 한다면 기준선을 같은 커밋에서 다시 뜬다.
    ///
    /// ⚠ `default:` 를 `"?"` 로 두지 않고 `ToString()` 으로 두는 축이 있다 — 새 enum 값이
    ///   생겼을 때 물음표는 어느 값이 빠졌는지 숨기지만 영문 이름은 그것을 말해준다.
    /// </summary>
    public static class KoreanNames
    {
        // ─────────────────────────── 무공 유형 ───────────────────────────

        /// <summary>
        /// 무공 유형. ⚠ `Sandbox` 의 지표 키가 이 문자열을 그대로 쓴다.
        /// ⚠⚠ 내공·경공은 옛 `DisciplineName` 이 `d.ToString()` 으로 흘려보내 **`InnerArt` 가 화면에 뜨던 자리**다.
        /// </summary>
        public static string Of(Discipline discipline)
        {
            switch (discipline)
            {
                case Discipline.Sword: return "검";
                case Discipline.Blade: return "도";
                case Discipline.Spear: return "창";
                case Discipline.Fist: return "권";
                case Discipline.Dagger: return "비도";
                case Discipline.InnerArt: return "내공";
                case Discipline.Movement: return "경공";
                default: return discipline.ToString();
            }
        }

        // ─────────────────────────── 계층 ───────────────────────────

        /// <summary>무공 계층. ⚠ 지표 키가 이 문자열을 쓴다(`tier.전승무학....`).</summary>
        public static string Of(ArtTier tier)
        {
            switch (tier)
            {
                case ArtTier.Wanderer: return "강호무학";
                case ArtTier.Minor: return "소문파";
                case ArtTier.Major: return "대문파·세력";
                case ArtTier.Legacy: return "전승무학";
                case ArtTier.Absolute: return "절대경지";
                default: return tier.ToString();
            }
        }

        // ─────────────────────────── 성향 ───────────────────────────

        /// <summary>성향 전체 이름.</summary>
        public static string Of(Alignment alignment)
        {
            switch (alignment)
            {
                case Alignment.Orthodox: return "정파";
                case Alignment.Unorthodox: return "사파";
                case Alignment.Demonic: return "마도";
                default: return alignment.ToString();
            }
        }

        // ⚠ `Of(Alignment?, string none)` 오버로드가 있었으나 지웠다 (2026-08-23).
        //   성향이 없는 이유가 **셋**(강호무학 · 제천성 · 절대경지)이라 부르는 쪽이 문구 하나를
        //   넘기는 형태로는 옳게 말할 수 없다. 판단은 계층을 아는 `ArtBreakdown.AlignmentName` 에 있다.

        /// <summary>성향 1자. 콘솔 표처럼 폭이 빠듯한 곳이 쓴다.</summary>
        public static string Short(Alignment? alignment)
        {
            if (!alignment.HasValue) return "-";
            switch (alignment.Value)
            {
                case Alignment.Orthodox: return "정";
                case Alignment.Unorthodox: return "사";
                case Alignment.Demonic: return "마";
                default: return "?";
            }
        }

        // ─────────────────────────── 무공 종류 ───────────────────────────

        /// <summary>무공 종류(§2-4). 공격 / 내공 / 경공.</summary>
        public static string Of(ArtKind kind)
        {
            switch (kind)
            {
                case ArtKind.Attack: return "공격";
                case ArtKind.Internal: return "내공";
                case ArtKind.Movement: return "경공";
                default: return kind.ToString();
            }
        }

        // ─────────────────────────── 범위·진형 ───────────────────────────

        /// <summary>타격 범위(§3-12). ⚠ 전(全)·만(萬) 은 둘 다 <see cref="AttackScope.All"/> 이라 같은 이름이 된다.</summary>
        public static string Of(AttackScope scope)
        {
            switch (scope)
            {
                case AttackScope.Single: return "단일";
                case AttackScope.Two: return "2인";
                case AttackScope.Three: return "3인";
                case AttackScope.All: return "전원";
                default: return scope.ToString();
            }
        }

        /// <summary>먼저 닿는 열(진형). `docs/multi-combat-plan.md` §D3-1 의 용어를 그대로 쓴다.</summary>
        public static string Of(BattleRow row)
        {
            switch (row)
            {
                case BattleRow.Front: return "전열";
                case BattleRow.Rear: return "후열";
                default: return row.ToString();
            }
        }

        // ─────────────────────────── 형태소 ───────────────────────────

        /// <summary>
        /// 형태소 카테고리(§3). 정의서 절 제목을 그대로 쓴다.
        /// ⚠⚠ <see cref="MorphemeCategory.AbsoluteRule"/> 이 옛 `ArtCompositionRule.Describe` 에는
        ///   **빠져 있어 `?` 가 나왔다.** 규칙 형태소 4자(면·무·쌍·통)가 나중에 들어왔기 때문이다.
        /// </summary>
        public static string Of(MorphemeCategory category)
        {
            switch (category)
            {
                case MorphemeCategory.AttackMethod: return "공격방식";
                case MorphemeCategory.Defense: return "방어";
                case MorphemeCategory.Internal: return "내공";
                case MorphemeCategory.Status: return "상태이상";
                case MorphemeCategory.Form: return "무공형태";
                case MorphemeCategory.Modifier: return "수식";
                case MorphemeCategory.Element: return "자연속성";
                case MorphemeCategory.Pinnacle: return "극한경지";
                case MorphemeCategory.Negation: return "부정";
                case MorphemeCategory.Tag: return "무학분류";
                case MorphemeCategory.Background: return "배경어";
                case MorphemeCategory.Scope: return "범위";
                case MorphemeCategory.AbsoluteRule: return "절대경지규칙";
                default: return category.ToString();
            }
        }

        /// <summary>무학분류(§3-10) 태그. ⚠ <see cref="ArtLineage.None"/> 은 **빈 문자열이 아니라** 이름을 준다 — 표에서 칸이 비면 누락과 구분되지 않는다.</summary>
        public static string Of(ArtLineage lineage)
        {
            switch (lineage)
            {
                case ArtLineage.None: return "없음";
                case ArtLineage.Yang: return "양기";
                case ArtLineage.Yin: return "음기";
                case ArtLineage.Mixed: return "혼원";
                default: return lineage.ToString();
            }
        }

        /// <summary>
        /// 절대경지 규칙(§5-3).
        /// ⚠ 사전의 **개념어와 같은 말**을 쓴다(면=면역 · 무=무소모 · 쌍=이회행동 · 통=상성통괄).
        ///   다르게 지으면 같은 화면에 같은 것의 두 이름이 뜬다.
        /// </summary>
        public static string Of(AbsoluteRule rule)
        {
            switch (rule)
            {
                case AbsoluteRule.None: return "없음";
                case AbsoluteRule.StatusImmunity: return "면역";
                case AbsoluteRule.NoQiCost: return "무소모";
                case AbsoluteRule.DoubleAction: return "이회행동";
                case AbsoluteRule.CounterSupremacy: return "상성통괄";
                default: return rule.ToString();
            }
        }
    }
}
