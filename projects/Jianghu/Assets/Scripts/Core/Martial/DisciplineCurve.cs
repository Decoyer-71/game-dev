using System;

namespace Jianghu.Core.Martial
{
    /// <summary>
    /// 무기 유형별 숙달 곡선 — **백일창(百日槍) 천일도(千日刀) 만일검(萬日劍)**.
    ///
    /// 무협 관용구 그대로다. 창은 백일이면 쓸 만해지고, 도는 천일, 검은 만일이 걸린다.
    /// 실제 비율 1:10:100 은 게임에 그대로 쓰기엔 너무 극단적이라 압축했다.
    ///
    /// ── ⚠⚠ 성향 곡선과 반드시 다른 것을 만져야 한다 ─────────────────────────
    /// **유형 숙련은 위력에 관여하지 않는다.** 이건 취향이 아니라 측정 결과다.
    ///
    /// 2026-07-28 승률표(Tools/Sandbox)에서, 성향 축 하나만으로도 수련 250회 시점에
    /// 상위 3개가 전부 마도가 되어 **유형 선택이 무의미해졌다**(검·마 68%, 권·마 65%, 도·마 64%).
    /// 여기에 유형 숙련까지 위력에 곱하면 "가장 늦게 숙달되는 무기 × 가장 늦게 강해지는 성향"이
    /// 후반을 독식한다 — 반증 조건 2(지배 전략)에 정면으로 걸린다.
    ///
    /// 그래서 역할을 갈랐다:
    ///   · <see cref="AlignmentCurve"/> (성향) = **얼마나 강한가** — 위력 배율
    ///   · 이 클래스 (유형)               = **어떻게 싸우는가** — 명중 · 선공 · 기력 · 방어 관통
    /// 두 축이 서로 다른 차원을 만지므로 곱연산 폭주가 없다.
    /// ────────────────────────────────────────────────────────────────────
    /// </summary>
    public static class DisciplineCurve
    {
        public const int MaxProficiency = 100;

        // 숙달 속도. 창 > 권 > 비도 > 도 > 검 순으로 빠르다.
        private const double SpearLearningRate = 2.00;   // 백일창 — 압도적으로 빠르다
        private const double FistLearningRate = 1.20;
        private const double DaggerLearningRate = 1.00;  // 비도 — 중간
        private const double BladeLearningRate = 0.70;   // 천일도
        private const double SwordLearningRate = 0.40;   // 만일검 — 가장 오래 걸린다

        // 만숙(숙련 100) 시 발현되는 특성. 유형마다 **딱 하나씩**이다 — 겹치면 정체성이 흐려진다.
        //
        // ⚠⚠ **2026-07-31 전면 재조정.** 유형 민감도(같은 무공을 다섯 무기로 들고 400전)를 처음
        //   재 봤더니 **검 89.1% 대 나머지 37~42%** 였다. 같은 무공인데 무기만 바꿔 이만큼 갈리면
        //   *"무공 조합이 전투를 바꾸는가"* 라는 이 프로토타입의 가설 자체를 무기가 덮어버린다.
        //
        //   원인은 **다섯 특성의 실효값이 전혀 달랐던 것**이다:
        //     · 검(명중)   — **항상 · 모든 타격에** 걸린다. 명중 형태소 5점어치였다
        //     · 창(선공)   — 전투당 1회 우위뿐. 추가 행동은 속도 기준이라 창 숙달이 안 들어갔다
        //     · 도(관통)   — 상대 방어가 3~7 뿐이라 깎을 것이 없다(비율 경감 전환 후 더 작아졌다)
        //     · 권(기력)   — 회복 10 이 3자 무공(12)을 거의 감당해 **잉여**였다
        //     · 비도(상태) — 상태이상 형태소를 넣은 무공에서만. 실측 기여 +4.9%p
        //
        //   → 사용자 확정 방향은 **"종류는 그대로, 크기만 맞춘다"** 이므로 정체성 문구는 건드리지 않는다.
        private const int SpearMaxInitiative = 25;         // 창 — 먼저 찌른다
        private const int SpearMaxSpeed = 2;               // 창 — ⚠ 신설. 선공만으로는 전투당 1회라 크기가 안 나온다
        private const int FistMaxQiReductionPercent = 100; // 권 — 지치지 않는다 (50 → 100: 50%로는 회복에 묻혔다)
        private const int DaggerMaxStatusChance = 50;      // 비도 — 암기에 독을 바른다 (30 → 50)
        // ⚠⚠ 2026-08-05 신설. 확률 축이 90%(상한 100)로 포화해 크기를 못 준다 —
        //   근거와 경위는 아래 <see cref="StatusPotencyBonus"/> 주석.
        private const int DaggerMaxStatusPotency = 3;      // 비도 — 만숙 시 상태이상 세기 +3
        /// <summary>
        /// 도 — 상대 방어를 무시하는 비율. **100 이 물리적 상한이다.**
        ///
        /// ⚠⚠ 2026-07-31 — 승률을 맞추려고 잠시 **160** 을 넣었다가 되돌렸다(사용자 지적).
        ///   방어를 100% 무시하면 더 무시할 것이 없다. 그 이상은 방어를 **음수**로 만들어
        ///   피해를 증폭하는 것이므로 *"관통"* 이 아니라 다른 효과이고, 이름이 성능을 거짓말한다 —
        ///   이 프로젝트의 대원칙(정의서 §0)에 정면으로 어긋난다.
        ///
        /// ⚠ 70 → 100 으로 올린 것은 유효하다. 다만 **100 에서도 도는 46~48% 다** —
        ///   상대 방어가 3~7 뿐이라 다 무시해도 피해가 6~10% 늘 뿐이기 때문이다.
        ///   즉 **도는 크기로 맞출 수 있는 유형이 아니다.** 처리 방향은 HANDOFF §4-2-d 참조.
        /// </summary>
        private const int BladeMaxPenetrationPercent = 100;
        private const int SwordMaxAccuracy = 7;           // 검 — 빈틈이 없다 (⚠⚠ 25 → 10. 이 하나가 89% 를 만들었다)

        /// <summary>수련 1회당 오르는 유형 숙련도.</summary>
        public static double LearningRate(Discipline discipline)
        {
            switch (discipline)
            {
                case Discipline.Spear: return SpearLearningRate;
                case Discipline.Fist: return FistLearningRate;
                case Discipline.Dagger: return DaggerLearningRate;
                case Discipline.Blade: return BladeLearningRate;
                case Discipline.Sword: return SwordLearningRate;
                case Discipline.InnerArt:
                case Discipline.Movement:
                    // 보조 무공은 무기 숙달 개념이 없다. 자체 숙련(LearnedArt)으로만 성장한다.
                    return 0.0;
                default: throw new ArgumentOutOfRangeException(nameof(discipline));
            }
        }

        /// <summary>수련 횟수로부터 유형 숙련도를 구한다(0 ~ 100).</summary>
        public static int ProficiencyFor(Discipline discipline, int trainingSessions)
        {
            if (trainingSessions < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(trainingSessions), "수련 횟수는 음수일 수 없다.");
            }

            int raw = (int)(trainingSessions * LearningRate(discipline));
            return raw > MaxProficiency ? MaxProficiency : raw;
        }

        /// <summary>만숙에 필요한 수련 횟수. 백일창 : 천일도 : 만일검의 압축판이다.</summary>
        public static int SessionsToMaster(Discipline discipline)
        {
            double rate = LearningRate(discipline);
            if (rate <= 0) return 0;
            return (int)Math.Ceiling(MaxProficiency / rate);
        }

        /// <summary>창 — 선공 보너스. 숙련에 비례해 발현된다.</summary>
        public static int InitiativeBonus(Discipline discipline, int proficiency)
        {
            return discipline == Discipline.Spear ? Scale(SpearMaxInitiative, proficiency) : 0;
        }

        /// <summary>
        /// 창 — **속도 보너스** (2026-07-31 신설).
        ///
        /// ⚠⚠ 선공(<see cref="InitiativeBonus"/>)만으로는 크기가 나오지 않는다 — 선공은 전투당
        ///   한 번뿐인 우위이고, 추가 행동(정의서 §1-3-d)은 **속도** 기준이라 창 숙달이 아예
        ///   들어가지 않았다. *"먼저 찌른다"* 는 정체성을 유지한 채 **자주 찌른다**를 더한 것이다.
        /// ⚠ 선공에도 속도가 얹히므로 창은 두 축을 겸한다 — 그래서 값이 3 으로 작다.
        /// </summary>
        public static int SpeedBonus(Discipline discipline, int proficiency)
        {
            return discipline == Discipline.Spear ? Scale(SpearMaxSpeed, proficiency) : 0;
        }

        /// <summary>검 — 명중 보너스(%p).</summary>
        public static int AccuracyBonus(Discipline discipline, int proficiency)
        {
            return discipline == Discipline.Sword ? Scale(SwordMaxAccuracy, proficiency) : 0;
        }

        /// <summary>
        /// 도 — 상대 방어력을 무시하는 비율(%). **0~100 을 넘지 않는다.**
        /// ⚠ 상한을 여기서 못박는다 — 100 을 넘는 값은 방어를 음수로 만들어 *"관통"* 이라는
        ///   이름과 다른 일을 하게 된다(2026-07-31 실제로 그런 값을 넣었다가 되돌렸다).
        /// </summary>
        public static int DefensePenetrationPercent(Discipline discipline, int proficiency)
        {
            if (discipline != Discipline.Blade) return 0;

            int penetration = Scale(BladeMaxPenetrationPercent, proficiency);
            return penetration > 100 ? 100 : penetration;
        }

        /// <summary>권 — 기력 소모 감소율(%).</summary>
        public static int QiCostReductionPercent(Discipline discipline, int proficiency)
        {
            return discipline == Discipline.Fist ? Scale(FistMaxQiReductionPercent, proficiency) : 0;
        }

        /// <summary>
        /// 비도 — 상태이상 부여 확률 보너스(%p).
        ///
        /// 비도를 쓰는 문파가 사천당가(독)·살문(암살)·장강수로채(수적)로 **전부 독·암살 계열**이라
        /// 이 특성을 배정했다. 암기에 독을 바른다는 것이 무협 관례이기도 하다.
        /// ⚠ 상태이상 시스템과 직결되므로 강해지기 쉽다. 비도 무공의 위력을 낮게 잡아 상쇄한다.
        /// </summary>
        public static int StatusChanceBonus(Discipline discipline, int proficiency)
        {
            return discipline == Discipline.Dagger ? Scale(DaggerMaxStatusChance, proficiency) : 0;
        }

        /// <summary>
        /// 비도 — 상태이상 **세기** 보너스(고정 가산). 2026-08-05 신설.
        ///
        /// ⚠⚠ **왜 확률이 아니라 세기인가 — 확률 축이 포화했기 때문이다.**
        ///   부여확률은 `기본 30 + 형태소 10 + 비도숙달 50 = 90%` 이고 상한이 100 이다.
        ///   즉 <see cref="DaggerMaxStatusChance"/> 를 아무리 올려도 **남은 여지가 10%p 뿐**이라
        ///   크기를 줄 수 없다. 2026-08-05 에 *"비도 숙달을 키우자"* 는 안을 올렸다가
        ///   이 상한을 확인하고 **단순판이 무효임을 알았다.**
        ///   → HANDOFF §5 의 *"밸런싱이 막혔을 때 원인이 값이 아니라 표현력일 수 있다"* 와 같은 자리다.
        ///     여기서는 표현력이 아니라 **축이 이미 천장에 닿아 있었다.**
        ///
        /// ⚠⚠ **왜 배수가 아니라 가산인가 (2026-08-05 사용자 확정 · `verify` 지적으로).**
        ///   처음에 `세기 × 1.4` 로 만들었다가 되돌렸다. HANDOFF §5 가 *"숙련·성향 배율을 곱하지
        ///   않는다 · 경지·버프도 곱셈으로 넣지 않는다"* 를 못박았는데, 배수판은 **비도 숙련도에
        ///   정비례해 스케일하는 곱**이라 그 문언의 정면 대상이었다.
        ///   ⚠ 그때 근거로 *"치명배율도 같은 자리에 있다"* 고 적었는데 **틀렸다** — 치명배율은
        ///     숙련과 무관한 고정 상수다(`CombatResolver.BaseCritMultiplier`. 그 근처
        ///     `PerformAction` 이 *"확률축에는 숙련 배율을 곱하지 않는다"* 고 적어 뒀다).
        ///     비유가 성립하지 않았다.
        ///   → 가산으로 바꾸면 이 축이 **직접가산형 넷과 같은 모양**이 된다 —
        ///     검 명중 +7 · 창 선공 +25 · 창 속도 +2 · 비도 확률 +50.
        ///
        /// ⚠⚠ **단 "유형 숙달에 곱이 없다" 는 말은 사실이 아니다** (`verify` 2회차 지적).
        ///   권(<see cref="QiCostReductionPercent"/>)과 도(<see cref="DefensePenetrationPercent"/>)는
        ///   **이미 숙련도에 비례한 퍼센트를 다른 스탯에 곱한다** — 반려된 배수판과 같은 구조다.
        ///   즉 이번 선택은 *"전례 없는 예외를 피했다"* 가 아니라 **"둘 중 더 단순한 쪽을 골랐다"** 이다.
        ///   ⚠ 권·도의 곱셈 구조가 §5 위반인지는 **별건이며 판정된 적이 없다.** 여기서 단정하지 않는다.
        ///
        /// ⚠ **정체성은 안 바뀐다.** *"암기에 독을 바른다"* 를 확률이 아니라 **농도**로 읽는 것이며,
        ///   `유형마다 특성 딱 하나` 규칙(위 §특성 주석)도 지켜진다 — 비도의 특성은 여전히
        ///   **상태이상 하나**이고, 확률과 세기는 그 한 축의 두 표현이다.
        ///   (창槍이 선공 + 속도 두 수치를 갖는 것과 같은 선례다 — <see cref="SpeedBonus"/>)
        ///
        /// ⚠⚠ **왜 지속(turns)이 아니라 세기(potency)인가** — 지속은 갱신 규칙과 엉킨다.
        ///   화상은 갱신되지 않고(`CombatResolver.BurnTurns` 주석) 중독은 스택제라 지속 개념이 없다.
        ///   턴을 건드리면 일곱 글자가 **서로 다른 방향으로** 움직인다. 세기는 전부 같은 뜻이다.
        ///
        /// ⚠⚠ **가산의 대가 — 상태이상마다 상대적 크기가 다르다.** 출혈 5 에 +3 은 +60% 지만
        ///   중독 10 에 +3 은 +30% 다(`CombatResolver.BleedPotency`·`PoisonPotencyPerStack`).
        ///   배수판에는 없던 성질이며, **약한 상태이상을 더 키우는 방향**이다.
        ///   ⚠ 지금 비도 카탈로그 7종은 전부 독·혈이라 이 둘만 문제가 되고, 실측이 그 상태에서 나왔다.
        /// ⚠ 동상(Frostbite)은 세기가 0 이라(스스로는 피해를 주지 않는다) 이 보너스가 닿지 않는다.
        ///   **빙(氷)을 쓰는 비도 무공이 생기면 그 무공만 숙달이 죽는다.** 그때 다시 볼 것.
        /// </summary>
        public static int StatusPotencyBonus(Discipline discipline, int proficiency)
        {
            return discipline == Discipline.Dagger ? Scale(DaggerMaxStatusPotency, proficiency) : 0;
        }

        private static int Scale(int maxValue, int proficiency)
        {
            if (proficiency < 0) throw new ArgumentOutOfRangeException(nameof(proficiency));
            int p = proficiency > MaxProficiency ? MaxProficiency : proficiency;
            return (int)Math.Round(maxValue * p / 100.0, MidpointRounding.AwayFromZero);
        }
    }
}
