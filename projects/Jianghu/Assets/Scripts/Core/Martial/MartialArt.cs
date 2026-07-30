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

        /// <summary>이 무공의 접근성 계층. 문파명으로부터 유도된다. 무소속이면 강호무학.</summary>
        public SchoolTier Tier => SchoolCatalog.TierOf(School);

        private static readonly StatusApplication[] NoEffects = new StatusApplication[0];

        /// <summary>
        /// **형태소에서 유도해 만든다.** 무공명을 분해한 결과를 그대로 받는다.
        ///
        /// ⚠ 수치를 인자로 받지 않는 것이 요점이다 — 수치의 출처는 오직 이름이다(정의서 §0).
        ///   조합 규칙 검사는 호출자(`MartialArtFactory`)가 이미 통과시킨 뒤에 부른다.
        /// </summary>
        public static MartialArt FromMorphemes(
            string id, string name, string school, Discipline discipline, Alignment? alignment,
            ArtStatDelta delta, int qiCost, int hitCount = 1, params StatusApplication[] effects)
        {
            if (hitCount < 1) throw new ArgumentOutOfRangeException(nameof(hitCount), "타격 횟수는 1 이상이어야 한다.");

            return new MartialArt(
                id, name, school, discipline, alignment,
                basePower: 0, qiCost: qiCost, hitCount: hitCount, accuracyBonus: 0,
                maxQiBonus: 0, powerBonusPercent: 0, evasionBonus: 0, initiativeBonus: 0,
                effects: effects, delta: delta, morphemeDerived: true);
        }

        private MartialArt(
            string id, string name, string school, Discipline discipline, Alignment? alignment,
            int basePower, int qiCost, int hitCount, int accuracyBonus,
            int maxQiBonus, int powerBonusPercent, int evasionBonus, int initiativeBonus,
            StatusApplication[] effects,
            ArtStatDelta delta = default, bool morphemeDerived = false)
        {
            Delta = delta;
            IsMorphemeDerived = morphemeDerived;
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

        /// <summary>공격 초식을 만든다. 유형은 검·도·권 중 하나여야 한다.</summary>
        public static MartialArt Technique(
            string id, string name, Discipline discipline, Alignment? alignment,
            int basePower, int qiCost, int hitCount = 1, int accuracyBonus = 0, string school = null,
            params StatusApplication[] effects)
        {
            if (discipline.IsSupport())
            {
                throw new ArgumentException("내공·경공은 공격 초식이 될 수 없다. Support 로 만들 것.", nameof(discipline));
            }
            if (basePower < 0) throw new ArgumentOutOfRangeException(nameof(basePower));
            if (qiCost < 0) throw new ArgumentOutOfRangeException(nameof(qiCost));
            if (hitCount < 1) throw new ArgumentOutOfRangeException(nameof(hitCount), "타격 횟수는 1 이상이어야 한다.");

            return new MartialArt(id, name, school, discipline, alignment, basePower, qiCost, hitCount, accuracyBonus, 0, 0, 0, 0, effects);
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

            return new MartialArt(id, name, school, discipline, alignment, 0, 0, 0, 0,
                maxQiBonus, powerBonusPercent, evasionBonus, initiativeBonus, null);
        }

        /// <summary>문파에 속하지 않은 강호무학인가.</summary>
        public bool IsWandererArt => string.IsNullOrEmpty(School);

        public override string ToString()
        {
            return Name;
        }
    }
}
