using System;
using System.Collections.Generic;
using Jianghu.Core.Martial.Morphemes;

namespace Jianghu.Core.Martial
{
    /// <summary>
    /// 무공 한 종류의 정의(원본 데이터). 개별 캐릭터의 숙련도는 <see cref="LearnedArt"/> 가 갖는다.
    ///
    /// 공격 초식(검·도·권)과 보조 무공(내공·경공)이 한 클래스를 공유한다.
    /// 쓰지 않는 쪽 필드는 0 이며, 생성은 <see cref="Technique"/> / <see cref="Support"/> 팩터리로만 한다.
    ///
    /// ⚠ 구상안의 등급축(일반/상승/진파/절세)과 학습경로축(강호/세가/전승)은 여기 없다.
    ///   등급은 "선택"이 아니라 "진행 단계"이므로 다른 축과 같은 무게로 넣으면 선택을 잡아먹는다
    ///   (鬼谷八荒 실패 사례 — docs/concepts/wuxia-grandmaster-rpg.md §2-2).
    /// </summary>
    public sealed class MartialArt
    {
        public string Id { get; }
        public string Name { get; }

        /// <summary>
        /// 출처 문파(門派). 예: "점창파". 비어 있으면 **강호무학** — 문파에 속하지 않은 낭인의 무학이다.
        ///
        /// 구상안의 학습경로축(강호무학 / 문파무학 / 전승무학)이 여기서 자란다.
        /// 프로토타입은 출처를 기록만 하고 전투에는 쓰지 않지만, 나중에
        /// "이 문파 제자만 배울 수 있다", "우리 문파가 가르칠 수 있는 무공" 같은 규칙이 여기 붙는다.
        /// </summary>
        public string School { get; }

        public Discipline Discipline { get; }

        /// <summary>
        /// 이 무공의 성향(正邪魔). **null 이면 성향이 없다 — 강호무학이 그렇다.**
        ///
        /// ⚠⚠ 2026-07-30 결정. 성향 배타 규칙(정파 무공을 배우면 사파·마도를 못 배운다)이 들어오면서
        ///   강호무학에 성향을 박아 두면 **`절정검법` 하나 배우는 순간 성향이 확정**된다.
        ///   정의서 §5-1 이 강호무학을 *"무소속 낭인의 무학. 시작점이자 최후의 보루"* 라고 한 것과 어긋난다.
        ///
        /// 그래서 강호무학은 성향을 갖지 않고 **익힌 사람의 성향을 따라 자란다**
        /// (<see cref="LearnedArt.EffectiveAlignment"/>). 시작점에서 성향이 강제되지 않고,
        /// 어느 성향이 되든 계속 쓸 수 있어 "최후의 보루" 가 말 그대로 성립한다.
        /// </summary>
        public Alignment? Alignment { get; }

        /// <summary>성향이 없는 무공인가(강호무학). 익힌 사람의 성향을 따른다.</summary>
        public bool IsAlignmentFree => Alignment == null;

        // ── 공격 초식용 ──
        /// <summary>기본 위력. 숙련 배율이 여기에 곱해진다.</summary>
        public int BasePower { get; }
        /// <summary>1회 사용에 드는 기력.</summary>
        public int QiCost { get; }
        /// <summary>타격 횟수. 권법처럼 여러 번 때리는 초식은 2 이상이며, 타격마다 명중을 판정한다.</summary>
        public int HitCount { get; }
        /// <summary>명중률 보정(%p). 도법은 음수, 검법은 양수.</summary>
        public int AccuracyBonus { get; }

        // ── 보조 무공용 (내공·경공). 전부 숙련 배율이 곱해져 적용된다 ──
        /// <summary>내공 — 최대 기력 증가분.</summary>
        public int MaxQiBonus { get; }
        /// <summary>내공 — 모든 초식 위력에 더해지는 곱연산 보너스(%).</summary>
        public int PowerBonusPercent { get; }
        /// <summary>경공 — 회피 증가분.</summary>
        public int EvasionBonus { get; }
        /// <summary>경공 — 선공 판정 증가분.</summary>
        public int InitiativeBonus { get; }

        /// <summary>
        /// 이 무공이 명중 시 걸 수 있는 상태이상. 보통 0~1개다.
        ///
        /// 성향별 배정(사파=출혈·중독 / 정파=기력소실 / 마도=경직)은 **데이터 층의 관례**이며
        /// 코드로 강제하지 않는다 — 사천당가처럼 정파이면서 중독을 거는 예외가 있다(§5-5).
        /// </summary>
        public IReadOnlyList<StatusApplication> Effects { get; }

        /// <summary>
        /// **형태소에서 유도된 수치 묶음.** 무공명을 분해해 얻는다(정의서 §0).
        ///
        /// ⚠⚠ 2026-07-30 신설. 이전에는 <see cref="BasePower"/> 같은 int 필드에 수치를
        ///   **손으로 박아** 넣었는데, 형태소 체계로 넘어오면서 그 출처가 이름이 됐다.
        ///   기존 필드는 레거시 카탈로그(`MartialArtCatalog` 36종)가 아직 쓰고 있어 남겨 뒀다 —
        ///   두 경로가 공존하는 과도기이며, 카탈로그가 138종으로 교체되면 정리한다.
        ///
        /// ⚠ int 가 아니라 <see cref="ArtStatDelta"/>(double) 인 이유 — 정의서에 `+1.5`(찌르기)·
        ///   `+0.5`(던지기)·`+0.3`(치명배율) 이 있어 정수로 자르면 정보가 사라진다.
        /// </summary>
        public ArtStatDelta Delta { get; }

        /// <summary>형태소에서 유도된 무공인가. false 면 레거시(손으로 수치를 박은) 무공이다.</summary>
        public bool IsMorphemeDerived { get; }

        /// <summary>
        /// 이 무공의 접근성 계층.
        ///
        /// ⚠⚠ 2026-07-31 — **문파명에서 유도하던 것을 저장값으로 바꿨다.** 유도 방식은
        ///   `SchoolCatalog` 에 없는 소속을 전부 강호무학으로 떨어뜨렸고, 그래서
        ///   **대형세력 16종(무림맹·사도련·제천성)이 강호무학으로 분류돼 있었다.**
        ///   승률표의 강호무학 그룹 11종 중 6종이 실제로는 대문파급 4자 무공이었고,
        ///   *"계층 간 우위가 없다"* 는 측정 결과가 상당 부분 여기서 나왔다.
        ///   ⚠ 카탈로그는 처음부터 정확한 계층을 알고 있었다 — 그걸 버리고 있었던 것이 문제다.
        ///
        /// ⚠ 전승무학(<see cref="ArtTier.Legacy"/>)·절대경지도 이제 구분된다.
        ///   그전에는 소속 문파명을 따라 전부 대문파로 뭉뚱그려졌다.
        /// </summary>
        public ArtTier Tier { get; }

        private static readonly StatusApplication[] NoEffects = new StatusApplication[0];
        private static readonly ArtLineage[] NoCounters = new ArtLineage[0];

        /// <summary>
        /// 이 무공이 **상성 우위를 갖는 무학분류**(정의서 §4). 같은 분류가 두 번 들어 있으면 상성 +2 다
        /// (부정+분류 짝이 한 이름에 둘 들어간 경우 — 지금 카탈로그에는 없다).
        ///
        /// 이름에서 유도된다 — 부정 한자(낙·망·멸·산·소) **바로 뒤에** 무학분류(일·월·혼)가 올 때만
        /// 생긴다. `낙월`(달을 떨어뜨린다) = 음기무학에 상성 +1. 138종 중 **4종**만 갖는다
        /// (창천낙월 · 참천멸월 · 절해망혼 · 절지낙월 — 전부 검법).
        ///
        /// ⚠⚠ **2026-08-02 신설.** 그전까지 `MorphemeParser` 가 만든 `ParsedArtName.CounterTargets` 를
        ///   <see cref="Morphemes.MartialArtFactory"/> 가 **그냥 버려서** 전투 엔진에 닿지 않았다.
        ///   `AttackScope`(범위)와 같은 형태의 누락이다 — 인계문서 §3-2 가 미연결 축을 *"정확히 셋"* 이라
        ///   적었는데 상성을 빠뜨려 실제로는 넷이었다.
        ///   ⚠ 이 누락에는 대가가 있었다: 정의서 §2-2 예외가 *"상성 무공은 부정·무학분류 2자가 수치 0이라
        ///   위력을 크게 포기한 구조"* 라며 무공형태 필수를 면제해 줬는데, **포기한 대가로 받기로 한
        ///   상성이 구현되지 않아 순손실이었다.** 실제로 `창천낙월` 은 소문파 최하위권(43.8%)이었다.
        /// </summary>
        public IReadOnlyList<ArtLineage> CounterTargets { get; }

        /// <summary>
        /// **절대경지 규칙**(§5-3). 규칙 형태소(면·무·쌍·통)에서 유도되며, 없으면 <see cref="Morphemes.AbsoluteRule.None"/>.
        /// ⚠ 효과는 <see cref="Combat.Combatant"/> 가 익힌 무공 전체에서 <c>any</c> 로 접어 **사람에게 상시** 적용한다 —
        ///   보조 무공(내공·경공)의 기존 처리와 같다. 합산이 아닌 이유는 <see cref="Morphemes.AbsoluteRule"/> 참조.
        /// </summary>
        public AbsoluteRule Rule { get; }

        /// <summary>
        /// **한 번에 때리는 대상 수**(정의서 §3-12). 범위 형태소(다多·군群·전全·만萬)에서 유도되며
        /// 없으면 <see cref="Morphemes.AttackScope.Single"/>.
        ///
        /// ⚠⚠ **2026-08-09 신설.** 그전까지 `MorphemeParser` 가 만든 `ParsedArtName.Scope` 를
        ///   <see cref="Morphemes.MartialArtFactory"/> 가 **그냥 버려서** 전투 엔진에 닿지 않았다 —
        ///   `CounterTargets`(2026-08-02)와 **똑같은 형태의 누락**이고, 그 자리 주석이
        ///   *"`AttackScope`(범위)는 아직 같은 상태로 남아 있다"* 고 스스로 적어 두고 있었다.
        ///   그래서 범위 무공 6종은 **대가만 내고 이점이 0** 이었다(공격 −1/−2/−3 · 만萬 기력 +200%).
        ///
        /// ⚠ **이 값을 실어 보내는 것만으로는 아무것도 안 바뀐다.** 1대1(<see cref="Combat.CombatResolver.Resolve"/>)은
        ///   상대가 하나뿐이라 읽을 곳이 없다. 실제로 쓰는 것은 다대다(`ResolveTeams`)이며,
        ///   설계는 `docs/multi-combat-plan.md` 에 있다.
        /// </summary>
        public AttackScope Scope { get; }

        /// <summary>
        /// **무공 종류**(공격 · 내공 · 경공). 무공명을 다시 분해할 때 **반드시 필요하다**.
        ///
        /// ⚠⚠ **2026-08-09 신설 — 그전까지 이 값이 여기서 버려졌다.**
        ///   <see cref="Morphemes.MartialArtFactory"/> 가 만들 때 쓰고 보관하지 않았는데,
        ///   `MorphemeParser.Parse(name)` 의 1인자 오버로드는 **접미사에서 종류를 읽는 강호무학 9종 전용**이라
        ///   나머지 **129종은 종류를 명시하지 않으면 *"접미사가 없다"* 로 예외**가 난다.
        ///   즉 이 값 없이는 **카탈로그의 93%를 다시 분해할 수 없었다.**
        ///   `CounterTargets`(2026-08-02) · `Scope`(2026-08-09)와 **똑같은 형태의 누락**이다.
        ///
        /// ⚠ 다시 분해할 때는 직접 파서를 부르지 말고 <see cref="Morphemes.MartialArtFactory.Decompose"/> 를 쓴다 —
        ///   강호무학이냐 아니냐로 갈리는 규칙이 **한 곳에만** 있어야 갈라지지 않는다.
        /// </summary>
        public ArtKind Kind { get; }

        /// <summary>
        /// **먼저 닿는 열**(진형). 공격방식(던지기만 후열) ∪ 수식 `어둡다` 중 **이름에서 앞선 글자**가 정한다.
        /// 규칙과 그 선택의 근거는 <see cref="BattleRowRule"/>.
        ///
        /// ⚠ <see cref="Scope"/> 와 같은 통로로 실려 온다 — 파서가 <see cref="ParsedArtName.PreferredRow"/> 로
        ///   정하고 <see cref="Morphemes.MartialArtFactory"/> 가 여기 옮긴다.
        /// ⚠ 손수 만든 무공(<see cref="Technique"/>)과 평타는 열 성향이 없으므로 **전열**이다.
        /// ⚠ 1대1에서는 읽히지 않는다 — 열은 다대다에만 있다(설계 §D3-8).
        /// </summary>
        public BattleRow PreferredRow { get; }

        /// <summary>
        /// **형태소에서 유도해 만든다.** 무공명을 분해한 결과를 그대로 받는다.
        ///
        /// ⚠ 수치를 인자로 받지 않는 것이 요점이다 — 수치의 출처는 오직 이름이다(정의서 §0).
        ///   조합 규칙 검사는 호출자(`MartialArtFactory`)가 이미 통과시킨 뒤에 부른다.
        /// </summary>
        public static MartialArt FromMorphemes(
            string id, string name, string school, Discipline discipline, Alignment? alignment,
            ArtStatDelta delta, int qiCost, ArtTier tier, int hitCount = 1,
            IReadOnlyList<ArtLineage> counterTargets = null, AbsoluteRule rule = AbsoluteRule.None,
            AttackScope scope = AttackScope.Single, BattleRow preferredRow = BattleRow.Front,
            ArtKind kind = ArtKind.Attack,
            params StatusApplication[] effects)
        {
            if (hitCount < 1) throw new ArgumentOutOfRangeException(nameof(hitCount), "타격 횟수는 1 이상이어야 한다.");

            return new MartialArt(
                id, name, school, discipline, alignment,
                basePower: 0, qiCost: qiCost, hitCount: hitCount, accuracyBonus: 0,
                maxQiBonus: 0, powerBonusPercent: 0, evasionBonus: 0, initiativeBonus: 0,
                effects: effects, delta: delta, morphemeDerived: true, tier: tier,
                counterTargets: counterTargets, rule: rule, scope: scope, preferredRow: preferredRow, kind: kind);
        }

        private MartialArt(
            string id, string name, string school, Discipline discipline, Alignment? alignment,
            int basePower, int qiCost, int hitCount, int accuracyBonus,
            int maxQiBonus, int powerBonusPercent, int evasionBonus, int initiativeBonus,
            StatusApplication[] effects,
            ArtStatDelta delta = default, bool morphemeDerived = false, ArtTier tier = ArtTier.Wanderer,
            IReadOnlyList<ArtLineage> counterTargets = null, AbsoluteRule rule = AbsoluteRule.None,
            AttackScope scope = AttackScope.Single, BattleRow preferredRow = BattleRow.Front,
            ArtKind kind = ArtKind.Attack)
        {
            Kind = kind;
            Delta = delta;
            IsMorphemeDerived = morphemeDerived;
            Tier = tier;
            CounterTargets = counterTargets ?? NoCounters;
            Rule = rule;
            Scope = scope;
            PreferredRow = preferredRow;
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("무공 Id 는 비어 있을 수 없다.", nameof(id));
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("무공 이름은 비어 있을 수 없다.", nameof(name));

            Id = id;
            Name = name;
            School = school ?? string.Empty;
            Discipline = discipline;
            Alignment = alignment;
            BasePower = basePower;
            QiCost = qiCost;
            HitCount = hitCount;
            AccuracyBonus = accuracyBonus;
            MaxQiBonus = maxQiBonus;
            PowerBonusPercent = powerBonusPercent;
            EvasionBonus = evasionBonus;
            InitiativeBonus = initiativeBonus;
            Effects = effects ?? NoEffects;
        }

        /// <summary>
        /// 공격 초식을 만든다. 유형은 검·도·권 중 하나여야 한다.
        ///
        /// ⚠ <paramref name="scope"/> 와 <paramref name="preferredRow"/> 는 2026-08-09 에 붙였다.
        ///   형태소 무공은 이름이 둘 다 정하지만(<see cref="FromMorphemes"/>), 이 손수 만드는 통로에는
        ///   그 값을 넣을 자리가 아예 없어 **범위·열을 가진 초식을 테스트에서 만들 수 없었다.**
        ///   기본값이 단일·전열이라 기존 호출부는 무영향이다.
        /// </summary>
        public static MartialArt Technique(
            string id, string name, Discipline discipline, Alignment? alignment,
            int basePower, int qiCost, int hitCount = 1, int accuracyBonus = 0, string school = null,
            AttackScope scope = AttackScope.Single, BattleRow preferredRow = BattleRow.Front,
            params StatusApplication[] effects)
        {
            if (discipline.IsSupport())
            {
                throw new ArgumentException("내공·경공은 공격 초식이 될 수 없다. Support 로 만들 것.", nameof(discipline));
            }
            if (basePower < 0) throw new ArgumentOutOfRangeException(nameof(basePower));
            if (qiCost < 0) throw new ArgumentOutOfRangeException(nameof(qiCost));
            if (hitCount < 1) throw new ArgumentOutOfRangeException(nameof(hitCount), "타격 횟수는 1 이상이어야 한다.");

            return new MartialArt(id, name, school, discipline, alignment, basePower, qiCost, hitCount, accuracyBonus, 0, 0, 0, 0, effects,
                scope: scope, preferredRow: preferredRow, kind: ArtKind.Attack);
        }

        /// <summary>보조 무공을 만든다. 유형은 내공·경공 중 하나여야 한다.</summary>
        public static MartialArt Support(
            string id, string name, Discipline discipline, Alignment? alignment,
            int maxQiBonus = 0, int powerBonusPercent = 0, int evasionBonus = 0, int initiativeBonus = 0,
            string school = null)
        {
            if (!discipline.IsSupport())
            {
                throw new ArgumentException("검·도·권은 보조 무공이 될 수 없다. Technique 으로 만들 것.", nameof(discipline));
            }

            // ⚠ 보조 무공의 종류는 유형에서 곧바로 나온다 — 내공이면 내공 무공, 경공이면 경공 무공이다.
            return new MartialArt(id, name, school, discipline, alignment, 0, 0, 0, 0,
                maxQiBonus, powerBonusPercent, evasionBonus, initiativeBonus, null,
                kind: discipline == Discipline.InnerArt ? ArtKind.Internal : ArtKind.Movement);
        }

        /// <summary>문파에 속하지 않은 강호무학인가.</summary>
        public bool IsWandererArt => string.IsNullOrEmpty(School);

        public override string ToString()
        {
            return Name;
        }
    }
}
