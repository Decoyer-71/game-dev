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
        // ⚠⚠ 2026-08-08 — 이 축의 **구동자가 유형 숙련도에서 무공 숙련도로 바뀌었다.**
        //   값도 3 → 6 으로 올랐는데, 늘린 것이 아니라 **기울기로 갈아 끼운 것**이다:
        //   그전에는 3성이든 10성이든 +3 이었고, 지금은 3성 +2 · 6성 +4 · 10성 +6 이다.
        //   근거는 아래 <see cref="StatusPotencyBonus"/> 의 "왜 구동자를 바꿨나" 절.
        private const int DaggerMaxStatusPotency = 6;      // 비도 — 그 무공 만숙 시 상태이상 세기 +6

        /// <summary>
        /// 비도 — **치명률(%p).** 2026-08-09 신설. ⚠⚠ **이 축만 무조건 발동한다.**
        ///
        /// ⚠⚠ **왜 필요했나 — 다섯 유형 중 비도만 숙달이 조건부였다.**
        ///   비도의 보상 둘(<see cref="DaggerMaxStatusChance"/> · <see cref="DaggerMaxStatusPotency"/>)이
        ///   **전부 상태이상 축**이라, 상태이상 형태소가 없는 비도 무공은 **숙달 보상이 정확히 0** 이었다.
        ///   실측 3/10 종이 그렇다 — `투유표법`(강호무학) · `황야환투` · `만우쾌사`(둘 다 전승무학).
        ///   나머지 넷(검 명중 · 도 관통 · 창 선공·속도 · 권 기력)은 **무공 구성과 무관하게 항상** 걸린다.
        ///   → *"만일검·백일창"* 은 **사람이 무기를 다룬 세월**이지 무공의 조건부 보상이 아니다.
        ///     조건부로 두면 이름이 성능을 거짓말한다(정의서 §0).
        ///
        /// **왜 하필 치명인가** — 근거 셋:
        ///   1. **어느 유형 숙달도 안 쓰는 빈 축**이다. 명중을 주면 검(만일검), 속도를 주면 창과 겹친다
        ///   2. *"급소를 노려 던진다"* — 암기의 관용적 심상이고 이름이 성능을 말한다
        ///   3. ⭐ **죽어 있는 치명 축을 살린다.** 기본 치명률이 10% 뿐이라 치명배율 형태소
        ///      `어둡다`(야·암·한 +0.3)가 90% 의 타격에 안 닿아 민감도표에서 *"무의미"* 였다.
        ///      비도 무공 중 `궤암척혈`·`환한투독`·`황야환투` 가 바로 그 글자를 물고 있다
        ///
        /// ⚠⚠ **구동자는 무공 숙련도다**(`LearnedArt.Proficiency`). <see cref="StatusPotencyBonus"/> 와 같은 선택이며,
        ///   ⛔ **정의서 §3-4-b 가 세운 *"확률은 유형 숙련도 · 세기는 무공 숙련도"* 라는 구분을 이 축이 깬다.**
        ///   확률축인데 무공 숙련도를 탄다 — 알고 택한 것이지 실수가 아니다. 근거는 실측이다:
        ///   유형 숙련도로 굴리면 측정에서 **유형 숙달이 만렙 고정**이라(§4-10-2) 3성에도 보너스가 통째로
        ///   들어가 **초반이 과해진다**. 무공 숙련도로 굴리면 3성 +5 · 6성 +9 · 10성 +15 로 기울기가 생긴다.
        ///   ⚠ 이 선택은 §3-4-b 를 갱신해야 성립한다 — 두 구동자 표를 세 줄로 다시 쓴다.
        /// ⚠⚠ **미확정 스윕값이다** (2026-08-09). 구동자 선택도 함께 미확정이다.
        /// </summary>
        private const int DaggerMaxCritChance = 15;
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
        /// 비도 — **치명률 보너스(%p).** 비도 숙달 중 **유일하게 무조건 발동**하는 축이다.
        /// 왜 이 축이 필요했고 왜 하필 치명인지는 <see cref="DaggerMaxCritChance"/> 주석에 있다.
        /// </summary>
        public static int CritChanceBonus(Discipline discipline, int proficiency)
        {
            return discipline == Discipline.Dagger ? Scale(DaggerMaxCritChance, proficiency) : 0;
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
        ///
        /// ══════ 왜 구동자를 바꿨나 — **수준이 아니라 기울기가 문제였다** (2026-08-08 사용자 확정) ══════
        ///
        /// ⚠⚠ **위 2026-08-05 판(유형 숙달분 단독)은 절반짜리였다.** 고정 가산이라 수준만 올리고
        ///   **기울기는 그대로 뒀다.** 실측 — 대문파 유형 평균이 무공 3성 → 10성에서
        ///   검 52.2→55.9(+3.7) · 도 42.5→47.2(+4.7) · 권 50.4→51.1(+0.7) · 창 58.1→57.7(−0.4) 인데
        ///   **비도만 58.7→47.0(−11.7)** 이었다. 비도는 약한 유형이 아니라 **3성에 유형 최상위이고
        ///   10성에 최하위인 유형**이었다. §4-9 가 이것을 "약하다" 로 읽고 수준을 올린 것이다.
        ///
        /// **원인은 구조다** — 무공 위력은 `CombatResolver.ArtPower` 에서
        ///   `(basePower − penalty) × PowerMultiplier + penalty` 로 경지 배율(1.1 → 2.0~2.24)을 타는데,
        ///   상태이상은 `TickStatuses` 가 `Health -= Potency` 로 **그 곱셈 사슬 밖에서** 절대량을 깎는다.
        ///   정체성 전체가 상태이상에 걸린 비도만 경지가 오를수록 상대적으로 시든다.
        ///   ⚠ 도(刀)가 유일하게 반대 방향(+4.7)인 것도 같은 구조다 — 관통 **비율**은 고정이지만
        ///     그것이 깎는 방어가 `DamagePerHit` 의 분모라 **사슬 안**이고, 절대 이득이 함께 자란다.
        ///
        /// ⛔ **경지 배율을 세기에 곱하는 안(근본안)은 버렸다.** HANDOFF §5 가
        ///   *"경지·버프도 같은 이유로 곱셈으로 넣지 않는다"* 를 못박았고, 위에 적힌 대로
        ///   `세기 × 1.4` 로 **이미 한 번 기각된 것과 같은 구조**다. 범위만 넓힌 재시도였다.
        ///
        /// ✅ 그래서 **구동자를 갈아 끼웠다.** 항을 하나 더 더하지 않는다 —
        ///   ⛔ 가산항을 얹는 판을 먼저 만들어 실측했는데(유형분 3 유지 + 무공분 6),
        ///     **3성도 30%만큼 같이 올라** 대문파 비도가 3성 58.7 → 62.1 로 뛰었다.
        ///     비도는 3성에서 이미 유형 최상위였으므로 그건 격차를 키우는 방향이다
        ///     (대문파 격차 3성 30.8 → 31.5 · 6성 27.3 → 28.7 로 실제로 나빠졌다).
        ///   → 그래서 **더하는 것이 아니라 바꾸는 것**이다. 그전 `+3 고정`은 3성에도 10성에도 같았고,
        ///     지금은 **3성 +2 · 6성 +4 · 10성 +6** 이다. 만렙 총량은 늘지만 초반은 오히려 줄어든다.
        ///   → 무협적으로도 읽힌다: *"암기에 독을 바르는 솜씨"* 는 유형 숙달이 정하고(확률 —
        ///     <see cref="StatusChanceBonus"/> 는 그대로 유형 숙련도를 탄다), **그 초식을 얼마나
        ///     익혔는가**가 독이 얼마나 깊이 드는지를 정한다. 두 함수가 각각 하나의 구동자를 갖는다.
        ///
        /// ⚠⚠ **`MartialStage`(경지 1~10)가 아니라 무공 숙련도(0~100 연속값)를 쓴다.**
        ///   `MartialStage` 주석이 *"지금은 표기·측정의 단위이고 전투 공식은 여전히 숙련도로 돈다 —
        ///   경지 단위 계단식 성장으로 만들지는 미결"* 이라고 적어 뒀다. 여기서 계단을 처음
        ///   도입하는 것은 **별건**이고, 연속값을 쓰면 나머지 전투 공식과 자가 같다.
        ///
        /// ⚠ **대가 — 다섯 유형 중 비도만 유형 숙련도 밖의 축을 탄다.** 유형 숙련도와 무공 숙련도는
        ///   <see cref="DisciplineMastery"/> 주석이 명시적으로 갈라 둔 별개 축이고, 검·창·도·권의
        ///   특성은 전부 전자만 탄다. 위 *"확률과 세기는 한 축의 두 표현"* 이라는 자체 정당화는
        ///   **이제 그대로 성립하지 않는다** — 두 표현이 서로 다른 구동자를 갖게 됐다.
        ///   ⓐ 원인(상태이상 축 전체가 경지를 안 탄다)은 **안 고쳐졌다.** 상태이상 형태소를 쓰는
        ///     공격 무공은 **45/71 종**이고(검 9/19 · 도 8/12 · 창 8/13 · 권 13/17 · 비도 7/10),
        ///     나머지 38종은 여전히 같은 기울기 결함을 안고 있다. **비도만 예외 처리한 것**이다
        ///   ⓑ 전면 수정(상태이상 세기 전체를 경지 연동)은 2026-08-08 에 **의식적으로 보류**했다 —
        ///     45종 재측정이 따르고, 곱을 못 쓰므로 가산으로는 기울기를 완전히 맞출 수 없다
        ///   ⓒ **강호무학 `투유표법`과 전승무학 `황야환투`에는 이 수정이 닿지 않는다.**
        ///     둘 다 상태이상 형태소가 없어 이 축 자체가 발동하지 않는다(Sandbox `비도 숙달 발동 조건`
        ///     블록이 ❌ 로 표시한다). 실측에서도 그 두 계층의 σ 판정이 **한 자리도 안 움직였다.**
        ///     `투유표법`은 강호무학이 형태소 2자 계층이라 **개명으로도 못 고친다**(공격방식+무공형태
        ///     둘 다 필수라 자리가 없다). 남은 과제다
        ///
        /// ⚠⚠ **값 6 은 8 과 겨뤄 골랐다** (실측). 반올림 때문에 6·7·8 은 3성이 전부 +2 로 같고
        ///   10성만 갈린다. 8 은 기울기를 −2.2 까지 펴지만 **모든 계층 지표가 6 보다 나빴다**
        ///   (소문파 10성 42.8~57.5 → 41.7~58.6 · 대문파 6성 38.8~65.7 → 38.6~66.0) —
        ///   10성 비도가 54.9 로 계층 평균 52.7 을 확실히 웃돌아 **수준이 과해졌기 때문**이다.
        ///   6 에서는 52.0 으로 계층 평균에 붙는다. **기울기를 완전히 펴는 것이 목표가 아니다.**
        /// </summary>
        public static int StatusPotencyBonus(Discipline discipline, int artProficiency)
        {
            // ⚠ 인자가 **유형 숙련도가 아니라 무공 숙련도**다. 위 "왜 구동자를 바꿨나" 절.
            return discipline == Discipline.Dagger ? Scale(DaggerMaxStatusPotency, artProficiency) : 0;
        }

        private static int Scale(int maxValue, int proficiency)
        {
            if (proficiency < 0) throw new ArgumentOutOfRangeException(nameof(proficiency));
            int p = proficiency > MaxProficiency ? MaxProficiency : proficiency;
            return (int)Math.Round(maxValue * p / 100.0, MidpointRounding.AwayFromZero);
        }
    }
}
