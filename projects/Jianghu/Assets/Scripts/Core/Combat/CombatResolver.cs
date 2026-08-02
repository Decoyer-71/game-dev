using System;
using System.Collections.Generic;
using System.Text;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;
using Jianghu.Core.Rng;

namespace Jianghu.Core.Combat
{
    /// <summary>
    /// 턴제 1:1 전투를 끝까지 해결한다. **완전 자동**이다.
    ///
    /// 플레이어의 결정은 전투가 시작되기 *전에* 전부 들어간다 — 어떤 무공을 어떤 성향으로
    /// 얼마나 익혔는가. 전투는 그 답안을 채점하는 장치다.
    ///
    /// 입력 <see cref="Combatant"/> 를 변형하지 않는다. 그래서 같은 조합 × 같은 시드를
    /// 몇 번 돌려도 결과가 같고, 승률표를 뽑을 수 있다.
    /// </summary>
    public static class CombatResolver
    {
        /// <summary>이 턴을 넘기면 무승부. 무한 교착(둘 다 피해 1)을 끊는 장치다.</summary>
        public const int DefaultMaxTurns = 50;

        /// <summary>
        /// **형태소 명중 1점을 명중률 몇 %p 로 볼 것인가** (2026-07-30 신설).
        ///
        /// ⚠⚠ 정의서에 없는 환산이다. §1-1 은 명중을 스탯(기본 1)으로, 회피를 확률(5%)로 적어
        /// **둘을 잇는 규칙을 정하지 않았다.** 그래서 여기서 정한다.
        ///
        /// 5 를 고른 근거 — 수식 '맞히다'(적·확 +2)가 **+10%p** 가 되어 검 숙달(+25%p)보다는 작지만
        /// 체감되는 크기이고, 무공형태 '정직'의 명중 −2 가 **−10%p** 라 페널티가 실제로 아프다.
        /// 정의서 §2-2 가 무공형태를 필수로 만든 이유("페널티가 열등함이 아니라 성격이 되게")가
        /// 이 환산에서 비로소 성립한다.
        ///
        /// ⚠ **미검증 초기값이다.** 민감도표(§5-4)에서 수식 12자가 47~53% 로 죽어 있으면 올리고,
        ///   65% 를 넘으면 내린다.
        /// </summary>
        public const int AccuracyPointToPercent = 5;

        /// <summary>
        /// 기본 명중률(%).
        ///
        /// ⚠⚠ **2026-07-30 측정 근거로 85 → 65 로 내렸다.**
        ///   85 이면 검 숙달(+25)만으로 110 이 되어 상한 99 에 박히고, 그 순간
        ///   **명중 축 전체가 무의미해진다** — 형태소 민감도표에서 수식 12자가 전부 승률 0% 로 나왔다.
        ///   글자를 넣어도 명중은 99 그대로인데 기력만 4 더 쓰니 당연한 결과였다.
        ///
        ///   그리고 정의서 §1-1 은 **회피 기본 5%** 를 명시하는데, 명중 85 는 그 5% 도 무의미하게 만든다.
        ///   65 로 내려야 명중과 회피가 **둘 다** 의미를 갖는다.
        ///
        /// ⚠ 여전히 미검증 초기값이다. 민감도표에서 수식 12자가 살아나는지로 판정한다.
        /// </summary>
        private const int BaseHitChance = 65;
        private const int MinHitChance = 25;
        private const int MaxHitChance = 99;

        /// <summary>
        /// 기본 치명률(%). 정의서 §1-1 의 캐릭터 기본값 전사 (2026-07-31 축 연결).
        ///
        /// **누구나 10% 는 터진다.** 치명 형태소를 하나도 안 넣은 무공도 이 값으로 굴린다 —
        /// 정의서가 이것을 무공 속성이 아니라 **캐릭터 기본 능력치**로 적었기 때문이다.
        /// 형태소(명·광·휘 +10%p · 뇌/패 +5%p · 황 +15%p)는 여기에 더해진다.
        /// </summary>
        public const int BaseCritChance = 10;

        /// <summary>
        /// 기본 치명배율(배). 정의서 §1-1 전사.
        ///
        /// ⚠⚠ **여기에 숙련 배율(`PowerMultiplier`)도 성향 배율도 곱하지 않는다** (2026-07-31 확정).
        ///   HANDOFF §5 의 *"곱셈 누적은 후반을 독식한다"* — 유형 숙달을 위력에서 뺀 것과 같은 이유다.
        ///   치명배율을 **형태소 상수**로 묶어두면 최대치가 `2.0 + 0.6 = 2.6배` 에서 멈추므로
        ///   수련이 쌓여도 이 축이 후반을 삼킬 수 없다. 성장으로 커지는 배율을 새로 만들지 않는다.
        /// </summary>
        public const double BaseCritMultiplier = 2.0;

        /// <summary>치명배율 하한. 음수 델타가 들어와도 피해가 줄거나 회복되지 않게 막는다.</summary>
        private const double MinCritMultiplier = 1.0;

        /// <summary>
        /// **속도 1점이 주는 추가 행동 확률(%p)** — 상대와의 속도 차이에 곱한다 (2026-07-31 사용자 확정).
        ///
        /// ⚠⚠ 이 상수가 **속도를 통화로 만든다.** 정의서 §1-1 대로 속도를 '행동 순서' 로만 이었더니
        ///   ±2 든 ±5 든 결과가 소수점까지 같은 **이진값**이었다 — 선공은 크기를 반영하지 못한다.
        ///
        /// ⚠ 값의 근거는 측정이다. 무공형태 쾌(속도+2/명중−2)가 25.7% 로 죽어 있었고,
        ///   명중 −2 의 실측 비용이 **−22%p** 라 속도 +2 가 그만큼을 갚아야 한다.
        /// </summary>
        public const int ExtraActionPercentPerSpeed = 6;

        /// <summary>추가 행동 확률 상한. 속도 격차가 벌어져도 매 턴 두 번 치는 상대는 만들지 않는다.</summary>
        public const int MaxExtraActionChance = 50;

        /// <summary>
        /// **피해 눈금 배수** (2026-07-31 사용자 확정). 세지려는 값이 아니라 **잘게 쪼개려는 값**이다.
        ///
        /// ⚠⚠ 이걸 넣기 전에는 타격 한 번이 7~8 이라 `Math.Round` 가 **12% 미만의 차이를 통째로 지웠다.**
        ///   방어 계수를 3 → 2 · 2.5 로 내려도 승률이 49.8%(무의미)에서 66%(지배적)로 **건너뛰기만 했고**
        ///   그 사이 값을 만들 방법이 없었다. 밸런싱의 최소 눈금을 반올림이 정해 버린 것이다.
        ///
        /// 5 를 고른 근거 — 타격이 35~80 이 되어 눈금이 **약 1.5%** 가 된다. 민감도 측정 오차
        /// (400전 기준 ±2.4%p)보다 작으므로 충분하고, 더 키우면 로그 가독성만 잃는다.
        ///
        /// ⚠ **체력·최소피해·지속피해를 같은 배수로 함께 옮겼다.** 그래서 이 변경은 재밸런싱이 아니라
        ///   눈금 세분화이며, **이전 측정값이 보존된다.** 비율축(치명배율·막기 감소율·방어 계수·
        ///   회피 환산·명중 환산·상태이상 확률·성향 편차)은 단위가 없으므로 건드리지 않는다.
        /// ⚠ **기력계는 배수에서 제외한다** — 기력은 체력과 단위가 다르고(정의서 §1-1-a 의
        ///   `글자 수 × <see cref="Martial.Morphemes.MorphemeParser.QiCostPerMorpheme"/>`),
        ///   평타 전락률이라는 별도 지표로 판정한다.
        ///   ⚠⚠ 2026-08-01 정정 — 원문은 상수를 `4` 로 못박아 뒀는데 커밋 9605db8 이 3 으로 내렸다.
        ///   그리고 *"이미 맞춰져 있다"* 도 사실이 아니었다: 전락률을 처음 실측한 결과 **0.0%** 다.
        /// </summary>
        public const int DamageScale = 5;

        /// <summary>
        /// 한 타격의 최소 피해. 교착 방지선이며 **눈금 배수와 함께 움직인다**(= 옛 스케일의 1).
        /// ⚠ 이 값이 곧 *"공격합이 음수인 무공"* 의 실제 위력이다(설계안 §1-E).
        /// </summary>
        public const int MinDamagePerHit = DamageScale;

        /// <summary>
        /// **방어 1점이 피해를 얼마나 깎는가** — 비율 경감 공식의 계수 (2026-07-31 신설).
        ///
        /// `피해 = 공격 × 100 / (100 + 방어 × DefenseScale)`
        ///
        /// ⚠⚠ 정의서에 방어 공식이 없다. §1-3 은 **공격만** 정의하고 방어를 어떻게 쓰는지 적지 않았다 —
        ///   명중 환산·상태이상 기본확률과 같은 종류의 구멍이며, 셋 중 가장 크게 터졌다.
        ///
        /// 값의 출처는 측정이다. 방(防) 형태소 하나를 넣음 vs 뺌으로 재면:
        ///   뺄셈(기존 공식) **100%** · 뺄셈에 배율 미적용 90% · 비율 K=10 **95%** · **K=3 → 65.5/66.8%**.
        /// ⚠ 목표 구간 53~65% 는 다른 선택 카테고리(상태이상·수식)와 같은 잣대다.
        ///
        /// ⚠⚠ **한때 3 이 이 축에서 고를 수 있는 가장 작은 값이었다.** 2 와 2.5 는 수련 200회 시점에
        ///   **49.8% = 완전 무의미**로 떨어졌는데, 값이 작아서가 아니라 **정수 반올림에 삼켜져서**였다 —
        ///   타격이 7~8 이던 시절 `Math.Round` 가 1 미만의 차이를 지웠다. 그래서 이 축은
        ///   *"무의미(49.8%)" 아니면 "지배적(66%)"* 두 값만 가질 수 있었다.
        ///   → <see cref="DamageScale"/> 로 눈금을 5배 잘게 만든 뒤에야 **2 를 고를 수 있게 됐다.**
        ///     밸런싱이 막혔을 때 원인이 값이 아니라 **표현력**일 수 있다는 사례로 남긴다.
        /// </summary>
        public const int DefenseScale = 2;

        /// <summary>
        /// 기본 막기확률(%) — **0 이다. 막기는 무공이 주는 것이지 누구나 하는 것이 아니다.**
        ///
        /// ⚠⚠ 처음엔 회피(5%)와 나란히 5 로 잡았다가 **테스트가 반증했다** (2026-07-31).
        ///   `CombatTests.사파는_전투_결과가_가장_일정하다` 가 깨졌다 — 사파 턴수 편차 12 > 마도 8.
        ///   원인: 막기는 **성향과 무관하게 5% 확률로 피해를 절반**으로 만든다. 그 흔들림이
        ///   사파의 ±5% 피해 편차보다 크므로, 기본 막기를 두는 순간 *"사파는 결과가 일정하다"* 가
        ///   수치에서 사라진다. **성향 3종의 정체성이 이 프로토타입의 검증 대상 그 자체**이므로
        ///   (설계 §1 가설) 막기를 위해 그것을 내줄 수 없다.
        ///
        /// ⚠ 정의서 §1-1 은 회피만 `5%` 로 명시하고 막기는 **스탯 1** 로만 적었다 —
        ///   확률 환산 규칙이 없으므로 0 을 기본으로 두는 것이 정의서와 충돌하지 않는다.
        /// ⚠ 이 테스트가 설계를 지켜낸 두 번째 사례다(HANDOFF §7 — *"테스트부터 의심하지 말 것"*).
        /// </summary>
        public const int BaseBlockChance = 0;

        /// <summary>막기확률 상한. 100% 막기가 나오면 전투가 끝나지 않는다.</summary>
        public const int MaxBlockChance = 75;

        /// <summary>
        /// **막으면 그 타격 피해가 얼마나 줄어드는가(%)** — 2026-07-31 사용자 확정.
        ///
        /// ⚠⚠ **회피와 갈라 놓는 것이 이 값의 존재 이유다.** 회피는 이미 *"빗나감 = 피해 0"* 이므로
        ///   막기를 무효화로 만들면 두 축이 같은 것이 되고, 그러면 섬(閃, 회피)과 방(防, 막기)을
        ///   나눈 이유가 사라진다. 절반이면 **회피는 도박, 막기는 완충**이라는 대비가 선다.
        /// </summary>
        public const int BlockDamageReductionPercent = 50;

        /// <summary>
        /// **반격 피해 = 자기 초식 한 방의 몇 %인가.**
        ///
        /// ⚠ 100%면 반격 무공이 사실상 매 턴 두 번 때리게 된다 — 기력도 안 쓰고 턴도 안 잡아먹으므로
        ///   그건 다른 어떤 형태소보다 크다. 절반이 *"받아친다"* 의 크기다. ⚠ 미검증 초기값.
        /// </summary>
        public const int CounterDamagePercent = 50;

        /// <summary>
        /// **상성 1당 주는 피해 증가(%)** — 정의서 §4. ⚠ 미검증 초기값이다.
        ///
        /// ⚠ 이름이 <see cref="CounterDamagePercent"/>(반격 위력)와 비슷하지만 **전혀 다른 축**이다.
        ///   저쪽은 반격(反擊), 이쪽은 상성(相性)이다.
        /// </summary>
        public const int CounterDamageBonusPercent = 10;

        /// <summary>
        /// **상성 1당 받는 피해 감소(%)** — 정의서 §4. ⚠ 미검증 초기값이다.
        ///
        /// ⚠ 주는 쪽(10)의 절반인 것은 정의서가 그렇게 정한 값이다. 1:1 전투에서
        ///   *"받는 피해 −X%"* 가 *"주는 피해 +X%"* 보다 값이 크기 때문이다(내 수명은 늘고
        ///   상대 수명은 그대로다 — `DamagePerHit` 의 비율 경감 주석과 같은 근거).
        /// </summary>
        public const int CounterDamageReductionPercent = 5;

        /// <summary>
        /// **절대경지 통(統) 이 모든 분류에 갖는 상성 수** — 정의서 §5-3 의 *"모든 분류에 상성 +2"*.
        /// ⚠ 일반 상성이 조합당 +1 인 것의 두 배다. 전승무학 상성 무공(+1)을 확실히 넘도록 정해진 값이다.
        /// </summary>
        public const int CounterSupremacyAdvantage = 2;

        /// <summary>⚠ 실험 중 — 쌍(雙) 보유 시 타격 위력(%).</summary>
        public const int DoubleActionPowerPercent = 55;

        // ── 상태이상 규칙 상수. 근거: docs/martial-system-proposal.md §5 ──
        /// <summary>중독 최대 중첩.</summary>
        public const int MaxPoisonStacks = 5;

        /// <summary>
        /// **상태이상 형태소가 있을 때의 기본 부여확률(%)** (2026-07-31 축 연결에서 신설).
        ///
        /// ⚠⚠ 정의서에 없는 값이다. §3-4 는 일곱 글자를 전부 *"부여 +10%"* 로만 적어
        /// **무엇에 더하는지를 정하지 않았다** — 명중 환산(<see cref="AccuracyPointToPercent"/>)과 같은 종류의 구멍이다.
        ///
        /// 30 을 고른 근거 — 제안서 §5-2/§5-4 가 설계 구간을 **출혈 35~45% · 중독 30~40% ·
        /// 기력소실 40~50% · 경직 40~50%** 로 적어 뒀다. `30 + 형태소 10 = 40%` 는 그 넷 모두의 구간 안이다.
        ///
        /// ⚠ **0 을 기본으로 삼을 수는 없다.** 조합 규칙이 상태이상을 **카테고리당 1자**로 묶어
        /// 일곱 글자가 전부 `+10%p` 로 같으므로, 기본이 0 이면 모든 상태이상 무공이 10% 로 균일해지고
        /// *"확률 × 효과" 의 확률 쪽이 통째로 죽는다* — 치명배율이 죽은 것과 같은 구조다(HANDOFF §5).
        /// 글자 간 차이는 확률이 아니라 **효과의 성격**에서 나오게 하는 것이 이 설계의 선택이다.
        /// </summary>
        public const int BaseStatusChance = 30;

        // ── 세기·지속. ⚠⚠ 정의서에 **한 줄도 없어** 여기서 정한다 ──
        //
        // 제안서 §5 의 원래 수치(출혈 세기 6~10 · 기력소실 6~10)는 **위력 22~28 시절**의 값이다.
        // 지금은 형태소 스케일이라 타격 한 번이 7~8 이고, 그 값을 그대로 쓰면
        // 상태이상 하나가 무공 본체보다 세진다. **비율을 유지한 채 스케일만 낮춰 옮겼다.**
        // ⚠ 전부 미검증 초기값이며 민감도표(§5-4)로 판정한다.

        /// <summary>
        /// 출혈 — 매 턴 고정 피해. 방어 무시.
        /// ⚠⚠ 2026-07-31 측정으로 2 → 1 로 내렸다. 2 이면 민감도 **70~76% = 지배적**이었다 —
        ///   부여확률 40% 로 매 턴 갱신되니 사실상 상시 유지되어 턴당 피해가 25%씩 늘어난 셈이다.
        /// </summary>
        /// ⚠ 2026-07-31 눈금 배수 도입으로 1 → 5 (`DamageScale` 과 함께 옮긴 값. 실질 변화 없음).
        public const int BleedPotency = 1 * DamageScale;
        public const int BleedTurns = 3;

        /// <summary>
        /// 중독 — **중첩 1겹당** 매 턴 피해. 5중첩이면 턴당 10.
        /// ⚠⚠ 2026-07-31 측정으로 1 → 2 로 올렸다. 1 이면 **52~53% = 무의미**였다.
        ///   매 턴 한 겹씩 빠지는 구조라 실제 중첩이 1~2 에 머물러 턴당 1 밖에 안 됐다.
        /// </summary>
        /// ⚠ 2026-07-31 눈금 배수와 함께 2 → 10 (실질 변화 없음).
        public const int PoisonPotencyPerStack = 2 * DamageScale;

        /// <summary>
        /// 기력소실 — 매 턴 깎이는 기력.
        ///
        /// ⚠ 제안서 §5-4 가 *"초식 1회분 이상"* 을 조건으로 달았다. 3자 무공이 12(당시 상수 4), 회복이
        ///   10 이므로 8 이면 **초식을 한 턴 걸러 쓰게 만든다** — 봉인까지는 아니되 체감되는 크기다.
        ///
        /// ⚠⚠ 2026-07-31 측정으로 8 → 10. 8 이면 **47~53% = 무의미**였다 —
        ///   기력 회복이 턴당 10 이라 8 은 회복에 먹혀 초식 사용 리듬을 바꾸지 못했다.
        ///   회복과 같은 10 이어야 그 턴의 회복이 통째로 상쇄되어 **실제로 한 턴을 평타로 만든다.**
        ///   ⚠ 12 도 재봤으나 65~66% 로 지배적이었다.
        ///
        /// ⚠⚠ **2026-08-01 정정 — 위 문단은 10 이라고 적혀 있는데 실제 값은 14 였다.**
        ///   같은 날 10 → 14 로 올리며 이 주석을 안 고쳤다(§4 · commit-audit 대상이었다).
        ///
        /// ⚠⚠ **2026-08-02 측정으로 14 → 20 (사용자 확정).** 14 는 **51~52.5% = 무의미**였다 —
        ///   회복이 턴당 10 이라 격차가 −4 뿐이고, 그 정도로는 상대의 기력을 무공 소모(2자 6 ·
        ///   3자 9 · 4자 12) 아래로 밀어내지 못해 **초식 리듬이 바뀌지 않았다.** 20 이면 격차가
        ///   −10 이 되어 실제로 초식을 못 사게 만든다 → **탈 55.75 / 55.75 / 56.50**(3·6·10성 400전).
        ///   ⚠ 25 도 재봤으나 6성에서 61.00 으로 다른 여섯 글자의 최댓값(58.00)을 이탈했다.
        ///   ✅ `verify` 전수 대조 — `FALL`·`TIER3V4`·`DISC_2/3/4자`·`LEN` 까지 **탈 하나만 변하고
        ///   나머지는 소수점까지 동일**하다. 이 상수는 다른 축과 결합돼 있지 않다.
        ///
        /// ⚠⚠ **이 값은 틱당이다.** <see cref="QiDrainTurns"/>=2 와 곱해 실효 총 소실 **40**,
        ///   회복 상쇄 후 순 **−20**. "20" 을 총량으로 읽지 말 것.
        ///
        /// ⚠ **탈이 살아난 것이 기력 축이 살아난 것은 아니다.** 평타 전락률은 여전히 **0.0%** 이고
        ///   권(拳)의 소모 감소 특성과 내공 형태소는 그대로 죽어 있다(HANDOFF §4-2-U).
        ///   탈은 상대의 기력을 직접 공격하므로 압력이 없어도 혼자 성립하는 예외였을 뿐이다.
        /// </summary>
        public const int QiDrainPotency = 20;
        public const int QiDrainTurns = 2;

        /// <summary>
        /// 경직 — 중첩당 명중 감소(%p). 제안서 §5-3 의 −8 을 그대로 쓴다.
        /// ⚠⚠ 지속은 2026-07-31 측정으로 2 → 3턴. 2턴이면 중첩이 쌓이기 전에 만료돼
        ///   **3중첩 마비에 사실상 도달하지 못했고**(민감도 47~50% = 무의미), 그러면 경직은
        ///   명중을 조금 깎는 것 말고 하는 일이 없다 — 게이팅 설계 자체가 죽는다.
        /// </summary>
        public const int StaggerPotency = 8;
        public const int StaggerTurns = 3;

        /// <summary>
        /// 화상 — **체증형.** 걸린 뒤 1 → 2 → 3 으로 커지고 3턴이면 꺼진다.
        ///
        /// ⚠⚠ **다시 걸어도 연장되지 않는다** (2026-07-31 측정 후 확정). *"이미 타고 있으면 더 타지 않는다"*.
        ///   갱신을 허용했더니 부여확률 40% 로 상시 유지되어 턴당 피해가 상한에 고정됐고,
        ///   민감도 **70~79% = 지배적**이 나왔다. 갱신을 끊으면 **꺼졌다 다시 붙는 리듬**이 생겨
        ///   출혈(상시 유지·일정)과 성격이 갈린다 — 세기를 깎아 해결하면 이 대비가 사라진다.
        /// </summary>
        /// ⚠⚠ 지속은 2026-07-31 측정으로 3 → 2턴. 3턴이면 갱신을 끊고 체증을 2단계로 묶어도
        ///   **66~67% 로 여전히 지배적**이었다. 한 번 붙었을 때의 총량(1+2=3)이 출혈 한 주기와
        ///   같아지는 지점이 여기다.
        /// ⚠ 2026-07-31 눈금 배수와 함께 1 → 5 (실질 변화 없음).
        public const int BurnPotency = 1 * DamageScale;
        public const int BurnTurns = 2;

        /// <summary>화상 체증 상한(단계). ⚠ 2026-07-31 측정으로 3 → 2 — 3단계면 68~74% 로 지배적이었다.</summary>
        public const int MaxBurnEscalation = 2;

        /// <summary>
        /// 동상 — 걸린 동안 **받는 피해 증가(%)**. 스스로는 피해를 주지 않는다.
        /// ⚠ 2026-07-31 측정으로 20 → 25. 20 이면 수련 100회 시점에 52.2% 로 무의미 구간이었다 —
        ///   피해를 주지 않는 축이라 다른 여섯 글자보다 체감이 늦게 온다.
        /// </summary>
        public const int FrostbiteVulnerabilityPercent = 25;
        public const int FrostbiteTurns = 2;

        /// <summary>
        /// 비(痺) 형태소가 한 번에 쌓는 경직 중첩. 경(硬)은 1 이다.
        ///
        /// ⚠⚠ 2026-07-31 사용자 확정. 정의서 §3-4 의 *"마비 스택 +1"* 을 **경직 중첩 +1**로 읽는다 —
        ///   엔진의 마비는 확률이 아니라 **경직 3중첩 게이팅**이고(제안서 §5-3), 그 구조를 우회하면
        ///   *"확률형 행동불가"* 라는 조사에서 가장 일관되게 실패한 형태로 되돌아간다.
        ///   그래서 비는 마비를 직접 걸지 않고 **두 겹씩 쌓아** 2회 성공에 마비에 닿는다.
        /// </summary>
        public const int ParalysisMorphemeStaggerGain = 2;

        /// <summary>⚠⚠ 경직이 이만큼 쌓이면 **마비가 확정 발동**한다. 확률이 개입하지 않는다.</summary>
        public const int StaggerStacksForParalysis = 3;

        /// <summary>마비 지속(턴). 연장 불가.</summary>
        public const int ParalysisTurns = 1;

        /// <summary>
        /// 마비 발동 후 이 턴 수만큼 경직을 새로 걸 수 없다 — 연속 마비 차단.
        ///
        /// ⚠⚠ 2026-07-31 측정으로 2 → 4. 비(痺)가 두 겹씩 쌓아 **2회 성공에 마비**에 닿다 보니
        ///   락이 짧으면 마비가 반복돼 민감도 **65~69% = 지배적**이 나왔다.
        ///   락을 늘리는 것이 이 축에서 유일하게 **비만 골라 누르는 손잡이**다 —
        ///   경(硬)은 애초에 3회를 모아야 해서 마비에 거의 닿지 않으므로 영향을 덜 받는다.
        ///   지속·세기를 건드리면 둘이 같이 움직여 벌어진 간격이 그대로 남는다.
        /// </summary>
        public const int StaggerLockAfterParalysis = 4;

        /// <summary>쓸 수 있는 초식이 없을 때의 맨손 공격. 기력을 쓰지 않는다.</summary>
        private static readonly LearnedArt BasicStrike = new LearnedArt(
            MartialArt.Technique("basic_strike", "평타", Discipline.Fist, Alignment.Orthodox, basePower: 0, qiCost: 0));

        /// <summary>전투 중에만 존재하는 상태이상 하나.</summary>
        private sealed class ActiveStatus
        {
            public StatusEffectKind Kind;
            public int Potency;
            public int RemainingTurns;  // 출혈 · 기력소실 · 경직 · 동상 · 화상
            public int Stacks;          // 중독 · 경직(명중 페널티 중첩) · 화상(경과 턴수)

            /// <summary>
            /// 경직 전용 — **마비 게이지.** 이게 <see cref="StaggerStacksForParalysis"/> 에 닿으면 마비가 확정 발동한다.
            ///
            /// ⚠⚠ 명중 페널티 중첩(<see cref="Stacks"/>)과 **갈라 둔 이유** (2026-07-31 측정): 비(痺)가
            ///   한 번에 두 겹을 쌓게 했더니 마비만 빨라지는 게 아니라 **명중 페널티도 즉시 −16** 이 되어
            ///   민감도 65~69% 로 지배적이 됐다. 정의서 §3-4 는 비를 *"마비 스택 +1"* 이라 적었지
            ///   *"경직 세기 2배"* 라고 하지 않았다 — 둘을 나누면 글자 뜻 그대로가 된다.
            ///   **비는 마비에 빨리 닿고, 경직의 아픔 자체는 경(硬)과 같다.**
            /// </summary>
            public int ParalysisGauge;
        }

        /// <summary>전투 중에만 존재하는 가변 상태. Combatant 를 오염시키지 않기 위해 분리했다.</summary>
        private sealed class Fighter
        {
            public Combatant Def;
            public int Health;
            public int Qi;
            public readonly List<ActiveStatus> Statuses = new List<ActiveStatus>();
            public int ParalyzeTurns;
            public int StaggerLockTurns;
            public bool IsDown => Health <= 0;

            /// <summary>낸 행동 수 — 평타 전락률의 분모.</summary>
            public int Actions;

            /// <summary>그중 기력이 모자라 평타로 내려앉은 횟수.</summary>
            public int BasicStrikes;

            public ActiveStatus Find(StatusEffectKind kind)
            {
                for (int i = 0; i < Statuses.Count; i++)
                {
                    if (Statuses[i].Kind == kind) return Statuses[i];
                }
                return null;
            }
        }

        public static CombatResult Resolve(
            Combatant attacker, Combatant defender, IRandomSource rng, int maxTurns = DefaultMaxTurns)
        {
            if (attacker == null) throw new ArgumentNullException(nameof(attacker));
            if (defender == null) throw new ArgumentNullException(nameof(defender));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (maxTurns < 1) throw new ArgumentOutOfRangeException(nameof(maxTurns), "최대 턴은 1 이상이어야 한다.");

            Fighter a = NewFighter(attacker);
            Fighter d = NewFighter(defender);
            var log = new List<CombatLogEntry>();

            // 선공 판정. 동률이면 난수로 가른다 — 주입받은 rng 를 쓰므로 결정론이 유지된다.
            bool attackerFirst = a.Def.Initiative > d.Def.Initiative
                                 || (a.Def.Initiative == d.Def.Initiative && rng.Chance(50));

            int turn = 0;
            while (turn < maxTurns)
            {
                turn++;

                // ⚠⚠ 턴 시작 회복 (2026-07-30 신설). 설계안 §2 격차표의 1순위 미구현 항목이었다.
                //   이게 없으면 **기력이 영영 돌아오지 않아** 3턴 만에 평타(피해 1)로 전락하고,
                //   체력 100 을 50턴 안에 못 깎아 무승부가 난다 — 형태소 무공으로 처음 싸운
                //   2026-07-30 측정에서 실제로 승률 0 · 전원 무승부가 나왔다.
                //
                // ⚠ 회복은 **양쪽 모두** 턴 시작에 받는다. 선공 순서와 무관해야 공평하다.
                Regenerate(a);
                Regenerate(d);

                Fighter first = attackerFirst ? a : d;
                Fighter second = attackerFirst ? d : a;

                Act(turn, first, second, rng, log);
                if (first.IsDown || second.IsDown) break;   // 출혈로 자기가 죽을 수도 있다

                Act(turn, second, first, rng, log);
                if (first.IsDown || second.IsDown) break;
            }

            CombatOutcome outcome;
            string winner;
            if (d.IsDown && !a.IsDown)
            {
                outcome = CombatOutcome.AttackerWin;
                winner = a.Def.Name;
            }
            else if (a.IsDown && !d.IsDown)
            {
                outcome = CombatOutcome.DefenderWin;
                winner = d.Def.Name;
            }
            else
            {
                // 둘 다 쓰러졌거나(동시에 출혈사) 최대 턴 도달
                outcome = CombatOutcome.Draw;
                winner = null;
            }

            return new CombatResult(
                outcome, winner, turn, a.Health, d.Health, log,
                a.Actions, a.BasicStrikes, d.Actions, d.BasicStrikes);
        }

        private static Fighter NewFighter(Combatant c)
        {
            return new Fighter
            {
                Def = c,
                Health = c.Stats.MaxHealth,
                Qi = c.EffectiveMaxQi,
            };
        }

        // ─────────────────────────── 한 사람의 행동 ───────────────────────────

        private static void Act(int turn, Fighter actor, Fighter target, IRandomSource rng, List<CombatLogEntry> log)
        {
            // 1) 자기에게 걸린 상태이상이 먼저 발동한다. 여기서 죽을 수도 있다.
            TickStatuses(turn, actor, log);
            if (actor.IsDown) return;

            if (actor.StaggerLockTurns > 0) actor.StaggerLockTurns--;

            // 2) 마비면 그 턴을 통째로 잃는다.
            if (actor.ParalyzeTurns > 0)
            {
                actor.ParalyzeTurns--;
                log.Add(CombatLogEntry.Incapacitated(turn, actor.Def.Name, "마비"));
                return;
            }

            PerformAction(turn, actor, target, rng, log);
            if (actor.IsDown || target.IsDown) return;

            // 3) **속도 우위면 한 번 더 친다** (2026-07-31 사용자 확정).
            //
            // ⚠⚠ 이게 없으면 속도는 **이진값**이다. 선공 판정만으로는 ±2 든 ±5 든
            //   "먼저 치느냐"만 바뀌어 승률이 소수점까지 같았다 — 실제로 재봤다.
            //   그러면 속도를 사는 형태소(쾌 25.7%)는 사전 수치를 어떻게 만져도 살아나지 않는다.
            //   *"빠른 무공은 몇 턴에 한 번 더 친다"* 는 무협적으로도 자연스럽고,
            //   절대경지의 **2회 행동**(확정)과 개념이 이어진다 — 이쪽은 확률형 하위 단계다.
            //
            // ⚠ **상대와의 차이**로 굴린다. 절대 속도로 굴리면 양쪽이 같이 빨라져 전투만 짧아진다.
            // ⚠ 상태이상 진행(1)과 마비(2)를 다시 거치지 않는다 — 추가 행동은 '행동'만이다.
            // ⚠⚠ **절대경지 쌍(雙) — 한 턴에 2회 행동** (2026-08-02 신설). 확정 1회다.
            //   **속공과 겹치지 않는다** — 확정 추가 행동을 쓴 턴에는 속공 판정을 건너뛴다.
            //   겹치면 한 턴에 3회가 되어 *"2회 행동"* 이라는 이름이 거짓말이 된다(§5-C 대원칙).
            // ⚠⚠ **기력 소모도 2배가 된다** — `PerformAction` 이 호출마다 `EffectiveQiCost` 를 다시
            //   차감하기 때문이다. 기력 축이 죽은 지금(전락률 0.0%)은 무해하지만, 축이 살아나면
            //   **보유자가 스스로 말라붙는다.** 그때 재조정 대상이다(HANDOFF §4-2-O).
            if (actor.Def.ActsTwice)
            {
                PerformAction(turn, actor, target, rng, log, extra: true);
                return;
            }

            int advantage = actor.Def.Speed - target.Def.Speed;
            if (advantage <= 0) return;

            int extraChance = Clamp(advantage * ExtraActionPercentPerSpeed, 0, MaxExtraActionChance);
            if (!rng.Chance(extraChance)) return;

            PerformAction(turn, actor, target, rng, log, extra: true);
        }

        /// <summary>초식 하나를 실제로 쓴다. 상태이상 진행·마비 판정은 포함하지 않는다.</summary>
        private static void PerformAction(
            int turn, Fighter actor, Fighter target, IRandomSource rng, List<CombatLogEntry> log,
            bool extra = false)
        {
            LearnedArt chosen = SelectArt(actor);

            // ⚠⚠ **평타 전락률 계측** (2026-08-01 신설). 설계안 §5-3 이 목표 10~30% 로 못박고
            //   `MorphemeParser.QiCostPerMorpheme` 주석이 *"이 상수는 전락률로 판정한다"* 고 적었는데
            //   **정작 그 값을 재는 코드가 없었다.** 기력 상수를 4↔3 으로 놓고 두 번 논쟁하는 동안
            //   판정 기준이 산술 추정뿐이었다 — 측정 도구의 분해능을 먼저 본다는 규율(§5)의 반복 사례다.
            //
            // ⚠ **분모는 '턴' 이 아니라 '행동' 이다.** 설계안 §5-3 의 문구는 "초식을 못 쓴 **턴** 비율" 인데,
            //   그 뒤 속도 우위의 **추가 행동**이 생겨(2026-07-31) 한 턴에 두 번 칠 수 있게 됐다.
            //   그러면 "초식 한 번 + 평타 한 번" 인 턴을 어느 쪽으로 셀지가 모호해진다.
            //   행동 기준은 그 모호함이 없고 *"기력이 모자라 초식을 못 낸 비율"* 이라는 원래 물음에 직답한다.
            // ⚠ **반격은 세지 않는다.** 반격도 기력이 마르면 평타로 내려앉지만(`ResolveCounter`),
            //   그건 피격이 방아쇠인 반응이지 행동 선택이 아니다. 섞으면 분모의 뜻이 흐려진다.
            actor.Actions++;
            if (ReferenceEquals(chosen, BasicStrike)) actor.BasicStrikes++;

            int mastery = actor.Def.MasteryOf(chosen.Art.Discipline);

            int qiCost = EffectiveQiCost(chosen.Art, mastery, actor.Def);   // 내공(식息) + 권 숙달 → 소모 감소
            actor.Qi -= qiCost;

            int attempts = chosen.Art.HitCount < 1 ? 1 : chosen.Art.HitCount;
            int basePerHit = DamagePerHit(actor.Def, target.Def, chosen, attempts, mastery);

            // ⚠⚠ 2026-07-30 — 명중도 형태소에서 읽는다(수식 '맞히다' 적·확 +2, 무공형태 '정직' −2 등).
            //   이전에는 형태소 무공의 명중이 통째로 0 이라, **수식 12자가 민감도표에서 전부 승률 0%** 였다.
            //   글자를 넣으면 기력만 4 더 쓰고 효과는 없었으니 당연한 결과였다.
            int artAccuracy = chosen.Art.IsMorphemeDerived
                ? (int)System.Math.Round(chosen.Art.Delta.Accuracy * AccuracyPointToPercent)
                : chosen.Art.AccuracyBonus;

            int accuracy = artAccuracy
                           + DisciplineCurve.AccuracyBonus(chosen.Art.Discipline, mastery)  // 검 숙달
                           - StaggerPenalty(actor);                                          // 자기가 경직이면 빗나간다
            int hitChance = Clamp(BaseHitChance + accuracy - target.Def.Evasion, MinHitChance, MaxHitChance);

            // ⚠ 무공 자신의 성향이 아니라 **유효 성향**을 쓴다 — 강호무학은 성향이 없고
            //   익힌 사람의 성향을 따르기 때문이다(2026-07-30 결정).
            int variance = AlignmentCurve.DamageVariancePercent(chosen.EffectiveAlignment);

            // ⚠⚠ 2026-07-31 — 치명 축 연결. 그전까지 사전에는 값이 있는데 엔진이 안 읽어
            //   치명 형태소(명·광·휘·야·암·한·뇌)가 민감도표에서 전부 **49% = 무영향**이었다.
            //   글자를 넣으면 기력만 4 더 쓰고 얻는 게 없었으니 넣을 이유가 없는 글자였다.
            //
            // ⚠ **확률축에는 숙련 배율을 곱하지 않는다.** 바로 위 명중이 이미 그렇게 돼 있고,
            //   곱하면 수련이 확률을 밀어올려 위 `BaseCritMultiplier` 주석의 함정이 확률 쪽으로 되살아난다.
            // ⚠ 레거시 36종에는 치명 필드 자체가 없다 — 기본값 10% / 2.0배로만 굴린다.
            //   위력·명중과 같은 과도기 분기이며, 카탈로그가 138종으로 온전히 넘어가면 함께 사라진다.
            int critChance = BaseCritChance;
            double critMultiplier = BaseCritMultiplier;
            if (chosen.Art.IsMorphemeDerived)
            {
                critChance += (int)Math.Round(chosen.Art.Delta.CritChance, MidpointRounding.AwayFromZero);
                critMultiplier += chosen.Art.Delta.CritMultiplier;
            }
            critChance = Clamp(critChance, 0, 100);
            if (critMultiplier < MinCritMultiplier) critMultiplier = MinCritMultiplier;

            // ⚠⚠ 2026-07-31 — 막기 축 연결. 방어자의 무공 수치를 읽는 첫 경로다.
            //   회피(빗나감 = 피해 0)와 갈리는 지점이 여기다 — **막기는 피해를 절반으로 줄인다.**
            int blockChance = Clamp(BaseBlockChance + target.Def.BlockChanceBonus, 0, MaxBlockChance);

            int landed = 0;
            int crits = 0;
            int blocks = 0;
            int damage = 0;
            for (int i = 0; i < attempts; i++)
            {
                if (!rng.Chance(hitChance)) continue;

                landed++;
                int perHit = RollDamage(basePerHit, variance, rng);

                // ⚠⚠ **타격당 판정**이다 (2026-07-31 확정). 행동당 한 번이 아니다.
                //   3타 권법은 치명 기회가 3번이지만 한 번 터져도 그 턴 피해의 1/3 만 부푼다 —
                //   `DamagePerHit` 주석의 *"다단은 분산이 낮다"* 는 성격이 치명 축에서도 유지된다.
                //   행동당으로 굴리면 단타와 다단의 치명 가치가 같아져 그 정체성이 지워진다.
                if (rng.Chance(critChance))
                {
                    crits++;
                    perHit = (int)Math.Round(perHit * critMultiplier, MidpointRounding.AwayFromZero);
                }

                // ⚠ 막기도 **타격당 판정**이다. 치명과 같은 자리에서 굴려 대칭을 맞춘다 —
                //   치명이 공격의 폭발이면 막기는 방어의 폭발이고, 다단 초식은 둘 다 기회가 많되
                //   한 번의 결과가 그 턴 피해의 1/n 만 흔든다.
                // ⚠ 치명 **뒤에** 적용한다. 그래야 "크게 터진 한 방을 막았다" 가 성립한다.
                if (rng.Chance(blockChance))
                {
                    blocks++;
                    perHit = (int)Math.Round(perHit * (100 - BlockDamageReductionPercent) / 100.0,
                        MidpointRounding.AwayFromZero);
                    if (perHit < MinDamagePerHit) perHit = MinDamagePerHit;   // 막아도 최소치는 들어간다
                }

                damage += perHit;
            }

            // ⚠⚠ 동상(취약) — 걸린 상대는 더 아프게 맞는다 (2026-07-31 신설).
            //   ⚠ 타격마다가 아니라 **행동의 총 피해에** 곱한다. 타격당 곱하면 반올림 손실이
            //     타격 수만큼 누적되어, 다단 초식일수록 취약이 옅어지는 엉뚱한 성질이 생긴다.
            //   ⚠ 지속 피해(출혈·중독·화상)에는 곱하지 않는다 — 그쪽은 이미 방어를 무시하므로
            //     둘을 겹치면 "무시 × 증폭" 이 되어 한쪽 조합만 과하게 커진다.
            int vulnerability = VulnerabilityPercent(target);
            if (vulnerability > 0 && damage > 0)
            {
                damage = (int)Math.Round(damage * (100 + vulnerability) / 100.0, MidpointRounding.AwayFromZero);
            }

            target.Health -= damage;
            if (target.Health < 0) target.Health = 0;

            // 4) 명중했으면 상태이상 부여를 판정한다.
            string note = landed > 0 ? ApplyEffects(turn, actor, target, chosen.Art, mastery, rng, log) : null;

            // ⚠ 치명은 로그에 **반드시 보여야 한다.** 안 보이면 "왜 갑자기 크게 맞았지" 가 남고,
            //   그건 설계 §1 의 반증 조건 1("차이를 체감할 수 없다")에 그대로 걸린다.
            if (crits > 0)
            {
                string mark = "[치명" + (crits > 1 ? " ×" + crits : "") + "]";
                note = string.IsNullOrEmpty(note) ? mark : mark + " " + note;
            }

            // ⚠ 막기도 같은 이유로 보여야 한다. 피해가 왜 작았는지 설명되지 않으면
            //   플레이어에게는 그냥 "약한 무공" 으로 보인다.
            if (blocks > 0)
            {
                string mark = "[막기" + (blocks > 1 ? " ×" + blocks : "") + "]";
                note = string.IsNullOrEmpty(note) ? mark : mark + " " + note;
            }

            // ⚠ 추가 행동도 보여야 한다. 안 보이면 "왜 두 번 맞았지" 가 남는다.
            if (extra) note = string.IsNullOrEmpty(note) ? "[속공]" : "[속공] " + note;

            log.Add(CombatLogEntry.Action(
                turn, actor.Def.Name, target.Def.Name, chosen.Art.Name,
                attempts, landed, damage, qiCost, target.Health, note));

            // 5) 맞은 쪽이 받아친다.
            if (landed > 0 && !target.IsDown) Counter(turn, target, actor, rng, log);
        }

        /// <summary>
        /// **반격** — 맞은 쪽이 확률로 되받아친다 (2026-07-31 사용자 확정).
        ///
        /// ⚠⚠ **막기 성공이 아니라 피격이 방아쇠다.** 막기·회피·반격 형태소는 전부 같은 `방어`
        ///   카테고리라 조합 규칙상 **한 무공에 둘 이상 넣을 수 없다.** 반격을 막기에 매달면
        ///   반·역·응을 넣은 무공은 막기가 기본치뿐이라 **반격이 거의 안 터진다** — 글자가 죽는다.
        ///
        /// ⚠ **행동당 1회**만 굴린다. 타격당으로 굴리면 3타 권법을 상대할 때 반격이 3배로 터져
        ///   반격 무공이 다단 상대에게만 극단적으로 강해진다 — 상성이 아니라 왜곡이다.
        ///
        /// ⚠ 기력을 쓰지 않고 턴도 잡아먹지 않는다. 대신 **위력이 절반**이고 치명·상태이상이 없다.
        ///   ⚠ 기력이 마르면 반격도 평타로 내려앉는다(`SelectArt` 가 쓸 수 있는 초식만 고른다) —
        ///     "기력 고갈 → 평타 전락" 이라는 전투의 드라마를 반격만 예외로 두지 않는다.
        /// </summary>
        private static void Counter(
            int turn, Fighter counterer, Fighter victim, IRandomSource rng, List<CombatLogEntry> log)
        {
            int rate = counterer.Def.CounterRate;
            if (rate <= 0 || !rng.Chance(Clamp(rate, 0, 100))) return;

            LearnedArt art = SelectArt(counterer);
            int mastery = counterer.Def.MasteryOf(art.Art.Discipline);
            int perHit = DamagePerHit(counterer.Def, victim.Def, art, 1, mastery);

            int damage = (int)Math.Round(perHit * CounterDamagePercent / 100.0, MidpointRounding.AwayFromZero);
            if (damage < MinDamagePerHit) damage = MinDamagePerHit;

            victim.Health -= damage;
            if (victim.Health < 0) victim.Health = 0;

            log.Add(CombatLogEntry.Action(
                turn, counterer.Def.Name, victim.Def.Name, art.Art.Name,
                1, 1, damage, 0, victim.Health, "[반격]"));
        }

        // ─────────────────────────── 상태이상 ───────────────────────────

        /// <summary>턴 시작에 걸려 있는 상태이상을 발동시킨다.</summary>
        private static void TickStatuses(int turn, Fighter f, List<CombatLogEntry> log)
        {
            for (int i = f.Statuses.Count - 1; i >= 0; i--)
            {
                ActiveStatus s = f.Statuses[i];
                bool expired = false;

                switch (s.Kind)
                {
                    case StatusEffectKind.Bleed:
                        // 지속제. 매 턴 고정 피해, 방어 무시.
                        f.Health -= s.Potency;
                        if (f.Health < 0) f.Health = 0;
                        log.Add(CombatLogEntry.StatusTick(turn, f.Def.Name, "출혈", s.Potency, 0, f.Health));
                        expired = --s.RemainingTurns <= 0;
                        break;

                    case StatusEffectKind.Poison:
                    {
                        // 스택제. 쌓일수록 아프고, 매 턴 한 겹씩 빠진다.
                        int dmg = s.Potency * s.Stacks;
                        f.Health -= dmg;
                        if (f.Health < 0) f.Health = 0;
                        log.Add(CombatLogEntry.StatusTick(turn, f.Def.Name, "중독 " + s.Stacks + "중첩", dmg, 0, f.Health));
                        expired = --s.Stacks <= 0;
                        break;
                    }

                    case StatusEffectKind.QiDrain:
                    {
                        // ⚠⚠ 이게 체감되려면 '초식을 못 쓰게 만드는' 수준이어야 한다(§5-4).
                        int before = f.Qi;
                        f.Qi -= s.Potency;
                        if (f.Qi < 0) f.Qi = 0;
                        log.Add(CombatLogEntry.StatusTick(turn, f.Def.Name, "기력소실", 0, before - f.Qi, f.Health));
                        expired = --s.RemainingTurns <= 0;
                        break;
                    }

                    case StatusEffectKind.Burn:
                    {
                        // ⚠⚠ 체증형. `Stacks` 를 **경과 턴수**로 쓴다 — 중독의 `Stacks`(중첩 수)와
                        //   이름은 같지만 의미가 다르다. 다시 걸려도 이 값은 리셋되지 않는다.
                        if (s.Stacks < MaxBurnEscalation) s.Stacks++;
                        int burn = s.Potency * s.Stacks;
                        f.Health -= burn;
                        if (f.Health < 0) f.Health = 0;
                        log.Add(CombatLogEntry.StatusTick(turn, f.Def.Name, "화상 " + s.Stacks + "단계", burn, 0, f.Health));
                        expired = --s.RemainingTurns <= 0;
                        break;
                    }

                    case StatusEffectKind.Frostbite:
                        // 발동 효과가 없다. 피해 계산에서 증폭으로 작용한다(`VulnerabilityPercent`).
                        expired = --s.RemainingTurns <= 0;
                        break;

                    case StatusEffectKind.Stagger:
                        // 발동 효과가 없다. 명중 판정에서 깎인다.
                        expired = --s.RemainingTurns <= 0;
                        break;

                    case StatusEffectKind.Paralysis:
                        // 마비는 Fighter.ParalyzeTurns 로 따로 관리한다.
                        expired = true;
                        break;
                }

                if (expired) f.Statuses.RemoveAt(i);
                if (f.IsDown) return;
            }
        }

        /// <summary>경직으로 인한 명중 감소량.</summary>
        private static int StaggerPenalty(Fighter f)
        {
            ActiveStatus s = f.Find(StatusEffectKind.Stagger);
            return s == null ? 0 : s.Potency * s.Stacks;
        }

        /// <summary>동상(취약)으로 이 사람이 **더 받는** 피해 비율(%). 안 걸렸으면 0.</summary>
        private static int VulnerabilityPercent(Fighter f)
        {
            return f.Find(StatusEffectKind.Frostbite) == null ? 0 : FrostbiteVulnerabilityPercent;
        }

        /// <summary>명중한 초식의 상태이상 부여를 판정한다. 로그에 붙일 설명을 돌려준다.</summary>
        private static string ApplyEffects(
            int turn, Fighter actor, Fighter target, MartialArt art, int mastery,
            IRandomSource rng, List<CombatLogEntry> log)
        {
            // ⚠⚠ **절대경지 면(免) — 모든 상태이상 면역** (2026-08-02 신설).
            //   여기가 **두 경로가 갈라지기 전**이라는 것이 요점이다. 아래에서 형태소 유도 경로
            //   (`ApplyMorphemeStatus`)와 레거시 경로(`art.Effects`)로 나뉘는데, 한쪽만 막으면
            //   **레거시 36종이 거는 상태이상에는 면역이 뚫린다.** `verify` 가 잡은 구멍이다.
            // ⚠ **부여 단계에서 막는다.** 이미 걸린 것을 지우는 것이 아니라 안 걸리게 하는 것이
            //   "면역" 이다 — 지속피해 틱(`TickStatuses`)을 건드리지 않는 이유가 이것이다.
            if (target.Def.IsStatusImmune) return null;

            // 비도 숙달 → 상태이상이 더 잘 걸린다.
            int chanceBonus = DisciplineCurve.StatusChanceBonus(art.Discipline, mastery);

            // ⚠⚠ 2026-07-31 — 상태이상 축 연결. 그전까지 형태소 7자(독·혈·비·염·빙·탈·경)는
            //   사전에 값이 있는데 엔진에 **도달할 경로 자체가 없었다** — 카탈로그 138종은
            //   `StatusApplication` 을 하나도 넘기지 않으므로 `art.Effects` 가 항상 비어 있었다.
            //   측정에서 7자 전부 **45~46%**, 즉 무의미(47~53%)보다도 **낮게** 나온 이유가 이것이다.
            //   기력은 글자 수로 매겨지는데 얻는 것이 0 이었으니 **넣으면 손해인 글자**였다.
            if (art.IsMorphemeDerived)
            {
                // 극한경지 왕(王) — 모든 상태이상 부여확률 +15%p. 여기가 그 축이 붙는 유일한 자리다.
                chanceBonus += (int)Math.Round(art.Delta.StatusApplyBonus, MidpointRounding.AwayFromZero);
                return ApplyMorphemeStatus(turn, target, art, chanceBonus, rng, log);
            }

            if (art.Effects.Count == 0) return null;

            StringBuilder note = null;
            for (int i = 0; i < art.Effects.Count; i++)
            {
                StatusApplication app = art.Effects[i];
                int chance = Clamp(app.ChancePercent + chanceBonus, 0, 100);
                if (!rng.Chance(chance)) continue;

                string applied = Apply(turn, target, app.Kind, app.Potency, app.DurationTurns, 1, log);
                if (applied == null) continue;

                if (note == null) note = new StringBuilder();
                else note.Append(' ');
                note.Append('[').Append(applied).Append(']');
            }

            return note?.ToString();
        }

        /// <summary>
        /// **형태소에서 상태이상을 유도해 부여한다.** 형태소 체계의 마지막 미연결 축이었다(2026-07-31).
        ///
        /// ⚠⚠ 무공 하나가 거는 상태이상은 **최대 1종**이다. 손으로 정한 제약이 아니라
        ///   조합 규칙 §2-2 의 *"카테고리당 1자"* 에서 자동으로 나오는 성질이다 —
        ///   그래서 여기서 델타를 위에서부터 훑어 **처음 걸리는 하나**로 끝낸다.
        ///
        /// ⚠ 세기·지속은 델타에 없다. 정의서 §3-4 가 확률만 적었기 때문이며,
        ///   그래서 상수(<see cref="BleedPotency"/> 등)로 둔다 — **무공별로 다르지 않다.**
        ///   달라지는 것은 "무엇이 걸리는가" 뿐이고, 그게 일곱 글자를 가르는 축이다.
        /// </summary>
        private static string ApplyMorphemeStatus(
            int turn, Fighter target, MartialArt art, int chanceBonus, IRandomSource rng, List<CombatLogEntry> log)
        {
            ArtStatDelta d = art.Delta;

            StatusEffectKind kind;
            double points;
            int potency;
            int duration;
            int stackGain = 1;

            if (d.PoisonChance > 0)
            {
                kind = StatusEffectKind.Poison; points = d.PoisonChance;
                potency = PoisonPotencyPerStack; duration = 1;   // 스택제 — 지속 개념이 없다
            }
            else if (d.BleedChance > 0)
            {
                kind = StatusEffectKind.Bleed; points = d.BleedChance;
                potency = BleedPotency; duration = BleedTurns;
            }
            else if (d.BurnChance > 0)
            {
                kind = StatusEffectKind.Burn; points = d.BurnChance;
                potency = BurnPotency; duration = BurnTurns;
            }
            else if (d.FrostbiteChance > 0)
            {
                kind = StatusEffectKind.Frostbite; points = d.FrostbiteChance;
                potency = 0; duration = FrostbiteTurns;          // 스스로는 피해를 주지 않는다
            }
            else if (d.QiDrainChance > 0)
            {
                kind = StatusEffectKind.QiDrain; points = d.QiDrainChance;
                potency = QiDrainPotency; duration = QiDrainTurns;
            }
            else if (d.StaggerChance > 0)
            {
                kind = StatusEffectKind.Stagger; points = d.StaggerChance;
                potency = StaggerPotency; duration = StaggerTurns;
            }
            else if (d.ParalysisStack > 0)
            {
                // ⚠⚠ 비(痺). 유일하게 확률이 아니라 스택으로 적힌 글자다 —
                //   확률은 경(硬)과 같게 두고(그래서 `points` 를 형태소 1자분으로 환산),
                //   **한 번에 두 겹을 쌓아** 2회 성공에 마비에 닿는다. 경은 3회다.
                kind = StatusEffectKind.Stagger;
                points = d.ParalysisStack * StatusPointsPerMorpheme;
                potency = StaggerPotency; duration = StaggerTurns;
                stackGain = ParalysisMorphemeStaggerGain;
            }
            else
            {
                return null;   // 상태이상 형태소가 없는 무공. 대다수가 여기로 빠진다
            }

            // ⚠⚠ **상태이상 저항**(극한경지 성 聖, −30%p)을 여기서 뺀다 — 2026-08-02 연결.
            //   그전까지 `Delta.StatusResist` 를 아무도 읽지 않아 성(聖)의 세 축 중 저항만 죽어 있었다.
            // ⚠ 곱셈(*"저항 30% 만큼 확률을 줄인다"*)이 아니라 **뺄셈**이다. 정의서 §1-1 이 이 축의
            //   단위를 `%p` 로 적었고, 부여확률 자체가 `기본 30 + 형태소 10 + …` 인 덧셈 축이라
            //   같은 단위로 맞춰야 이름이 뜻하는 대로 읽힌다(곱셈 누적 금지 — HANDOFF §5).
            int chance = Clamp(
                BaseStatusChance + (int)Math.Round(points, MidpointRounding.AwayFromZero) + chanceBonus
                - target.Def.StatusResistPercent, 0, 100);
            if (!rng.Chance(chance)) return null;

            string applied = Apply(turn, target, kind, potency, duration, stackGain, log);
            return applied == null ? null : "[" + applied + "]";
        }

        /// <summary>
        /// 상태이상 형태소 **1자가 주는 부여확률(%p)**. 정의서 §3-4 가 일곱 글자에 똑같이 적어 둔 값이다.
        /// ⚠ 비(痺)만 확률이 아니라 스택으로 적혀 있어, 그 스택을 이 값으로 환산해 확률을 맞춘다.
        /// </summary>
        private const int StatusPointsPerMorpheme = 10;

        /// <summary>상태이상 하나를 실제로 건다. 걸리지 않았으면 null.</summary>
        private static string Apply(
            int turn, Fighter target, StatusEffectKind kind, int potency, int durationTurns, int stackGain,
            List<CombatLogEntry> log)
        {
            switch (kind)
            {
                case StatusEffectKind.Bleed:
                case StatusEffectKind.QiDrain:
                case StatusEffectKind.Frostbite:
                {
                    // 지속제 — 중첩하지 않고 지속만 갱신한다.
                    // ⚠ 동상도 여기다. 세기가 0 이고 지속만 의미를 갖는다(효과는 피해 계산 쪽에 있다).
                    ActiveStatus s = target.Find(kind);
                    if (s == null)
                    {
                        target.Statuses.Add(new ActiveStatus
                        {
                            Kind = kind,
                            Potency = potency,
                            RemainingTurns = durationTurns,
                        });
                    }
                    else
                    {
                        s.Potency = potency;
                        s.RemainingTurns = durationTurns;
                    }
                    return StatusApplication.NameOf(kind);
                }

                case StatusEffectKind.Burn:
                {
                    // ⚠⚠ 체증형이고 **갱신되지 않는다.** 이미 타고 있으면 이번 부여는 버린다 —
                    //   갱신을 허용하면 상시 유지되어 지배적 형태소가 된다(상수 주석 참조).
                    if (target.Find(StatusEffectKind.Burn) != null) return null;

                    target.Statuses.Add(new ActiveStatus
                    {
                        Kind = StatusEffectKind.Burn,
                        Potency = potency,
                        RemainingTurns = durationTurns,
                        Stacks = 0,   // 경과 턴수. 매 턴 오르며 그게 곧 피해 배수다
                    });
                    return "화상";
                }

                case StatusEffectKind.Poison:
                {
                    // 스택제 — 지속 개념이 없고 중첩만 쌓인다.
                    ActiveStatus s = target.Find(StatusEffectKind.Poison);
                    if (s == null)
                    {
                        target.Statuses.Add(new ActiveStatus
                        {
                            Kind = StatusEffectKind.Poison,
                            Potency = potency,
                            Stacks = 1,
                        });
                        return "중독 1중첩";
                    }
                    if (s.Stacks >= MaxPoisonStacks) return null;   // 이미 최대
                    s.Stacks++;
                    s.Potency = potency;
                    return "중독 " + s.Stacks + "중첩";
                }

                case StatusEffectKind.Stagger:
                {
                    // ⚠⚠ 게이팅의 핵심. 확률은 '경직이 걸리는가'에만 개입하고
                    //     '마비가 터지는가'에는 개입하지 않는다.
                    if (target.StaggerLockTurns > 0) return null;   // 마비 직후엔 다시 못 쌓는다

                    ActiveStatus s = target.Find(StatusEffectKind.Stagger);
                    if (s == null)
                    {
                        s = new ActiveStatus
                        {
                            Kind = StatusEffectKind.Stagger,
                            Potency = potency,
                            RemainingTurns = durationTurns,
                            Stacks = 0,
                        };
                        target.Statuses.Add(s);
                    }
                    s.Potency = potency;
                    s.RemainingTurns = durationTurns;

                    // ⚠ 명중 페널티는 언제나 한 겹씩. 비(痺)가 더 쌓는 것은 **마비 게이지뿐**이다.
                    s.Stacks++;
                    s.ParalysisGauge += stackGain;

                    if (s.ParalysisGauge >= StaggerStacksForParalysis)
                    {
                        // 확정 발동. 경직은 전부 소멸하고, 한동안 다시 쌓을 수 없다.
                        target.Statuses.Remove(s);
                        target.ParalyzeTurns = ParalysisTurns;
                        target.StaggerLockTurns = StaggerLockAfterParalysis + ParalysisTurns;
                        log.Add(CombatLogEntry.StatusTick(turn, target.Def.Name,
                            "마비 게이지 " + StaggerStacksForParalysis + " → 마비!", 0, 0, target.Health));
                        return "마비 유발";
                    }
                    return "경직 " + s.Stacks + "중첩";
                }

                case StatusEffectKind.Paralysis:
                    // ⚠ 무공이 마비를 직접 거는 것은 설계상 쓰지 않는다(확률형 행동불가는 조사에서
                    //   가장 일관되게 실패한 항목이다). 그래도 데이터가 들어오면 동작은 하게 둔다.
                    target.ParalyzeTurns = ParalysisTurns;
                    return "마비";

                default:
                    return null;
            }
        }

        // ─────────────────────────── 행동 선택·피해 ───────────────────────────

        /// <summary>
        /// 지금 쓸 수 있는 초식 중 기대 피해가 가장 큰 것을 고른다.
        /// 난수를 쓰지 않는다 — 선택까지 흔들리면 무엇 때문에 이겼는지 분리할 수 없다.
        /// 동점이면 목록 순서상 앞선 것.
        /// </summary>
        private static LearnedArt SelectArt(Fighter actor)
        {
            LearnedArt best = null;
            double bestScore = -1;

            IReadOnlyList<LearnedArt> arts = actor.Def.Arts;
            for (int i = 0; i < arts.Count; i++)
            {
                LearnedArt learned = arts[i];
                if (learned.Art.Discipline.IsSupport()) continue;      // 내공·경공은 스스로 공격하지 않는다

                // ⚠ 숙달로 깎인 실제 소모량으로 판단해야 한다. 권 숙달자는 남들이 못 쓰는 상황에서도 초식을 낸다.
                int mastery = actor.Def.MasteryOf(learned.Art.Discipline);
                if (EffectiveQiCost(learned.Art, mastery, actor.Def) > actor.Qi) continue;

                double score = learned.Art.BasePower * learned.PowerMultiplier * learned.Art.HitCount;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = learned;
                }
            }

            // 쓸 수 있는 초식이 없으면 맨손. 기력이 마르면 전투 양상이 바뀌는 것이 의도다.
            return best ?? BasicStrike;
        }

        /// <summary>
        /// 턴 시작 회복 — 기력을 되돌린다.
        ///
        /// ⚠⚠ **이것이 없으면 전투가 성립하지 않는다** (2026-07-30 실측). 기력이 영영 안 돌아오면
        ///   4자 무공(당시 기력 16 · 상수 4) 기준 3턴 만에 고갈되고, 그 뒤로는 평타(피해 1)만 나가
        ///   체력 100 을 50턴 안에 못 깎는다. 실제로 **전원 무승부 · 승률 0** 이 나왔다.
        ///   ⚠ 현재 상수는 3 이라 4자 무공은 **12** 다. 위 수치를 현재 값으로 읽지 말 것.
        ///
        /// **기력 고갈 → 평타 전락은 살려 두되 영구적이지 않게 하는 것**이 이 단계의 목적이다.
        /// 그 드라마가 현재 전투의 핵심이고(HANDOFF §3-2), 회복 속도가 그 빈도를 정한다.
        /// 설계안 §5-3 의 **평타 전락률**(목표 10~30%)이 이 값의 적정성을 판정한다.
        ///
        /// ⚠ 회복량은 정의서 §1-1 의 **기력회복속도 2** 가 기준이며, 내공 형태소(음 +2 · 합 +0.5 ·
        ///   수 +1 · 선 +3)가 더한다. ⚠ 전부 미검증 초기값이다.
        /// ⚠ 체력 회복은 아직 없다 — 회복 형태소를 넣을 때 같은 자리에 붙인다(2026-07-30 설계).
        /// </summary>
        private static void Regenerate(Fighter f)
        {
            if (f.IsDown) return;

            int max = f.Def.EffectiveMaxQi;
            if (f.Qi >= max) return;

            f.Qi += f.Def.QiRegenPerTurn;
            if (f.Qi > max) f.Qi = max;
        }

        /// <summary>
        /// 한 번의 타격 피해.
        ///
        /// 총 위력을 먼저 구한 뒤 타격 횟수로 나눈다. 그래서 다단 초식(권법)은
        /// **방어력에 상대적으로 약하고 분산이 낮다** — 잽을 여러 번 넣는 감각이다.
        /// 반대로 단타 초식(도법)은 방어를 한 번만 통과하는 대신 빗나가면 그 턴이 통째로 날아간다.
        /// </summary>
        private static int DamagePerHit(Combatant actor, Combatant target, LearnedArt art, int attempts, int mastery)
        {
            // ⚠⚠ 2026-07-30 — 위력의 출처가 바뀌었다. 손으로 박은 `BasePower` 가 아니라
            //   **무공명을 분해해 얻은 형태소 공격 합**(`Delta.Attack`)을 쓴다.
            //   공식의 모양은 그대로다 — 정의서 §1-3 의 `캐릭터 공격 + (형태소 공격 합 × 성향 배율)` 과
            //   이미 같은 꼴이었고, 곱할 대상만 교체됐다.
            //
            // ⚠ 레거시 36종(`MartialArtCatalog`)은 아직 손으로 박은 수치를 쓰므로 갈라서 읽는다.
            //   카탈로그가 138종으로 교체되면 이 분기는 사라진다.
            //
            // ⚠⚠ **스케일이 완전히 다르다.** 레거시는 `BasePower` 22~28 인데 형태소 공격 합은 최대 5 다.
            //   그래서 캐릭터 기본 능력치도 정의서 §1-1(공격 1)로 맞춰야 하며,
            //   **2026-07-30 이전의 승률표 측정값은 전부 무의미하다**(HANDOFF §5-2).
            double basePower = art.Art.IsMorphemeDerived ? art.Art.Delta.Attack : art.Art.BasePower;
            double artPower = basePower * art.PowerMultiplier;

            // ⚠⚠ **무공은 아무리 대가가 커도 맨손보다 약해질 수 없다** (2026-08-02 신설 · 사용자 확정).
            //   그전까지 공격 합이 음수인 무공이 **7종** 있었고(궤암포독·궤암척혈·환한투독·궤야척혈·
            //   황야환투 −0.25 · 환벌혈군 −0.75 · 궤격비전 −2.75), 이 식이 `Stats.Attack + artPower` 라
            //   **수련할수록(PowerMultiplier ↑) 총 위력이 줄었다.**
            //   ⛔ 실측: 궤격비전을 3성 상대에게 붙이면 **1성 78.0% → 10성 1.2%** 다. 설계안 §1-E 의
            //     *"수련해도 세지지 않는 무공"* 보다 나쁘다 — **수련이 전투력을 파괴한다.**
            //
            //   원인은 기만(幻·詭 공격 −0.75)과 범위(다 −1 · 군 −2 · 전 −3)다.
            //   ⚠⚠ HANDOFF §4-2-a 는 *"공격을 파는 유일한 글자가 기만인데 공격방식 최소가 던지기(+0.5)라
            //     0 아래로 안 내려간다"* 며 이 문제를 닫았다고 적어 뒀는데, **−0.75 + 0.5 = −0.25** 이고
            //     **범위 형태소를 아예 빠뜨렸다.** 산수 실수가 아니라 원인 분석이 불완전했던 것이다.
            //
            //   ⚠ 대가 — 하한에 걸리는 조합에서는 기만의 *"공격 −0.75"* 가 실제로 실현되지 않는다.
            //     그래도 사전 값 수정으로는 범위 무공(−2.75)을 못 닫으므로 이쪽을 택했다(정의서 §1-3-e).
            if (artPower < 0) artPower = 0;
            double totalPower = (actor.Stats.Attack + artPower) * (100 + actor.PowerBonusPercent) / 100.0;

            // ⚠ 실험(2026-08-02): 쌍(雙) 2회 행동에 위력 −50% — 되돌리거나 확정할 것
            if (actor.ActsTwice) totalPower = totalPower * DoubleActionPowerPercent / 100.0;

            // 도 숙달 → 방어 관통. 위력을 올리는 게 아니라 상대 방어를 무시한다 —
            // 그래서 단단한 상대에게만 강하고, 물렁한 상대에겐 이점이 거의 없다.
            // ⚠ 2026-07-31 — `Stats.Defense` 가 아니라 **무공이 더한 방어**를 읽는다.
            //   방어 형태소가 엔진에 닿는 유일한 경로다(`Combatant.EffectiveDefense`).
            // ⚠⚠ **방어무시 형태소**(극한경지 마 魔, 25%)를 여기 합류시킨다 — 2026-08-02 연결.
            //   그전까지 `Delta.DefenseIgnore` 를 아무도 읽지 않아 마(魔)의 두 축 중 방어무시만
            //   죽어 있었다(공격 +2.5 는 살아 있어 부분 손실). 인계문서 §3-2 의 미연결 축이다.
            //
            // ⚠ 도(刀) 숙달의 관통과 **같은 자리에서 더한다.** 둘 다 *"상대 방어를 무시한다"* 는
            //   같은 뜻이고, 따로 곱하면 곱셈 누적이 새로 생긴다(HANDOFF §5 금지).
            // ⚠⚠ 상한 100 — 방어를 100% 무시하면 **더 무시할 것이 없다.** 도 숙달 100 에 마 25 를
            //   더해 125 가 되면 방어가 음수로 뒤집혀 *"피해 증폭"* 이라는 다른 효과가 된다.
            //   이것이 2026-07-31 에 실제로 밟은 실패다(방어관통 160% — CLAUDE.md §5-C).
            int penetration = Clamp(
                DisciplineCurve.DefensePenetrationPercent(art.Art.Discipline, mastery)
                + (int)Math.Round(art.Art.Delta.DefenseIgnore, MidpointRounding.AwayFromZero), 0, 100);
            double effectiveDefense = target.EffectiveDefense * (100 - penetration) / 100.0;

            // ⚠⚠ 2026-07-31 — **뺄셈에서 비율 경감으로 바꿨다** (사용자 확정).
            //   뺄셈은 이 스케일에서 어떤 형태로도 지배적이었다 — 무공 방어를 처음 이었을 때
            //   방(防) 형태소 하나로 **승률 100%** 가 나왔다. 타격이 7~8 인데 형태소 방어 +2 에
            //   숙련 배율까지 곱하면 −4.3, 상대 공격의 절반이 통째로 지워졌기 때문이다.
            //   ⚠ 1:1 전투에서 *"받는 피해 −X%"* 는 *"주는 피해 +X%"* 보다 값이 크다 —
            //     내 수명은 늘리고 상대 수명은 그대로다. 두 축을 같은 크기로 넣으면 안 된다.
            double afterDefense = totalPower * 100.0 / (100.0 + effectiveDefense * DefenseScale);

            // ⚠⚠ **상성**(정의서 §4) — 2026-08-02 신설. 그전까지 파서가 만든 상성이 팩토리에서
            //   버려져 **엔진에 한 번도 닿은 적이 없었다**(`MartialArt.CounterTargets` 주석 참조).
            //
            //   공격 쪽은 **지금 쓰는 초식**의 상성만 센다 — 상성은 그 초식의 성질이고,
            //   `Delta.Attack` 이 활성 무공에서만 오는 것과 같은 취급이다.
            //   방어 쪽은 **익힌 무공 전부**를 합산한다(`Combatant.CounterCountAgainst`) —
            //   *"받는 피해 −5%"* 는 어느 초식을 쓰는 중인지와 무관한 상시 성질이기 때문이다.
            //
            // ⚠⚠ **덧셈으로 합친다.** `(1 + 0.10a) × (1 − 0.05d)` 로 곱하지 않는다 —
            //   HANDOFF §5 와 정의서가 *"곱셈 누적을 새로 만들지 말 것"* 을 반복해서 못박았고
            //   (유형 숙달 × 성향이 후반을 독식한 실패), 상성은 그 규칙의 예외가 될 이유가 없다.
            //
            // ⚠ 하한 0 — 상성 배수가 음수가 되면 *"때릴수록 상대가 회복한다"* 는 뜻이 되어
            //   개념적으로 존재할 수 없다. 지금 사전으로는 방어 상성이 20 을 넘을 수 없어
            //   실제로는 도달하지 않지만, 절대경지 4번(*"모든 분류에 상성 +2"*)이 붙으면
            //   경로가 생기므로 미리 막는다.
            // ⚠⚠ **절대경지 통(統) — 모든 분류에 상성 +2, 상대 상성 무효** (2026-08-02 신설).
            //   **양방향이다.** 공격할 때 분류 무관 +2 를 얻고, **피격당할 때 상대의 상성을 0** 으로
            //   만든다. 한쪽만 걸면 *"상대 상성 무효"* 라는 이름의 절반이 실현되지 않는다 —
            //   `verify` 가 잡은 지점이다(§5-C 대원칙: 이름과 성능이 일치해야 한다).
            // ⚠ 양쪽이 다 보유하면 서로 무효화되어 **대칭**이 된다.
            //   순서가 중요하다 — **"상대 상성 무효" 를 먼저 본다.** 그래야 양쪽이 다 보유했을 때
            //   둘 다 0 이 되어 대칭이 된다. 반대로 짜면 서로 +2 를 얻어 **둘 다 강해지는** 꼴이 된다.
            // ⚠⚠ **통(統)의 +2 는 상대가 무학분류를 가질 때만 붙는다** (2026-08-02 2차 · 사용자 확정).
            //   처음엔 *"'모든 분류에' 이므로 과녁을 가리지 않는다"* 며 무소속 상대에게도 붙였는데,
            //   실측 **+31.8%p 무조건**이 나왔다. 과녁이 없어도 붙으면 그건 상성이 아니라
            //   **그냥 주는 피해 +20%** 다 — 상성은 정의상 *"무엇에 강한가"* 이기 때문이다(§3-10-a).
            //   ⚠ 이것은 **정의서 §5-3-a 를 다시 연 변경**이다. 그 문서의 배선표가 *"공격 시 분류 무관 +2"*
            //     라고 적고 사용자 승인을 받았었다 — 버그 수정이 아니라 승인된 규칙의 재결정이다.
            int counterFor;
            if (target.HasCounterSupremacy) counterFor = 0;                       // 방어자가 절대 → 내 상성 무효
            else if (actor.HasCounterSupremacy) counterFor = SupremacyAgainst(target.Lineage);
            else counterFor = CountCounters(art.Art.CounterTargets, target.Lineage);

            int counterAgainst;
            if (actor.HasCounterSupremacy) counterAgainst = 0;                    // 공격자가 절대 → 상대 상성 무효
            else if (target.HasCounterSupremacy) counterAgainst = SupremacyAgainst(actor.Lineage);
            else counterAgainst = target.CounterCountAgainst(actor.Lineage);
            if (counterFor > 0 || counterAgainst > 0)
            {
                double counterPercent = 100
                                        + counterFor * CounterDamageBonusPercent
                                        - counterAgainst * CounterDamageReductionPercent;
                if (counterPercent < 0) counterPercent = 0;
                afterDefense = afterDefense * counterPercent / 100.0;
            }

            // ⚠ 눈금 배수는 **마지막에** 곱한다. 방어(비율)·성향 배율은 단위가 없으므로
            //   어디서 곱하든 결과가 같고, 여기서 곱해야 위 수치들이 정의서와 같은 단위로 읽힌다.
            int perHit = (int)Math.Round(afterDefense * DamageScale / attempts, MidpointRounding.AwayFromZero);
            return perHit < MinDamagePerHit ? MinDamagePerHit : perHit;   // 교착 방지
        }

        /// <summary>
        /// <paramref name="targets"/> 안에 <paramref name="lineage"/> 가 몇 번 들어 있는가.
        /// 같은 분류가 두 번 있으면 상성 +2 다(정의서 §5-3 절대경지 4번이 그 경로다).
        /// </summary>
        /// <summary>
        /// 절대경지 통(統)이 <paramref name="lineage"/> 를 가진 상대에게 갖는 상성 수.
        /// **무소속(`null`)이면 0** — 과녁이 없으면 상성이 성립하지 않는다(§4 · §3-10-a).
        /// </summary>
        private static int SupremacyAgainst(ArtLineage? lineage)
        {
            return lineage == null ? 0 : CounterSupremacyAdvantage;
        }

        private static int CountCounters(IReadOnlyList<ArtLineage> targets, ArtLineage? lineage)
        {
            if (lineage == null || targets == null) return 0;

            int count = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] == lineage.Value) count++;
            }
            return count;
        }

        /// <summary>
        /// 기본 피해에 성향별 변동폭을 적용한다.
        ///
        /// **여기가 사파의 존재 이유다.** 사파는 변동폭이 ±5% 라 최저 피해가 사실상 보장되고,
        /// 마도는 ±35% 라 같은 기대값이어도 결과가 크게 흔들린다.
        /// </summary>
        private static int RollDamage(int basePerHit, int variancePercent, IRandomSource rng)
        {
            if (variancePercent <= 0) return basePerHit;

            int roll = 100 + rng.Range(-variancePercent, variancePercent + 1);
            int result = (int)Math.Round(basePerHit * roll / 100.0, MidpointRounding.AwayFromZero);
            return result < 1 ? 1 : result;   // 최소 1 은 보장(교착 방지)
        }

        /// <summary>
        /// 이 사람이 실제로 내는 기력 소모량.
        ///
        /// 두 감면이 순서대로 걸린다:
        ///   1. **보조 무공의 소모율**(<see cref="Combatant.SupportQiCostPercent"/>) — 식(息 −10%)
        ///   2. **유형 숙달**(권 −100%)
        ///
        /// ⚠ 순서가 이렇게인 이유 — 숙달은 *"이 사람이 이 무기를 얼마나 잘 다루는가"* 라서 **마지막**에
        ///   와야 한다. 반대로 넣으면 내공을 익힐수록 권 숙달의 절대 감면폭이 줄어드는 모양이 된다.
        /// </summary>
        private static int EffectiveQiCost(MartialArt art, int mastery, Combatant owner)
        {
            // ⚠⚠ **절대경지 무(無) — 기력 무소모** (2026-08-02 신설).
            //   기존 감면(보조 무공 소모율 → 유형 숙달) **뒤가 아니라 앞**에서 즉시 끝낸다.
            //   0 에 무엇을 곱하고 무엇을 빼도 0 이므로 순서 논쟁 자체가 성립하지 않는다.
            // ⚠⚠ **이 규칙은 지금 효과가 0 이다.** 평타 전락률이 전 무공·전 경지 0.0% 라
            //   아무도 기력이 마르지 않는다(HANDOFF §4-2-d). 극한경지 선(仙)과 권(拳)의
            //   기력소모 −100% 가 같은 이유로 죽어 있다. **기력 축 제로섬(§4-2-O)이 풀려야 산다.**
            //   측정 블록은 그때까지 판정불가로 낸다 — 공허한 통과를 만들지 않는다.
            if (owner != null && owner.HasNoQiCost) return 0;

            double cost = art.QiCost;

            if (owner != null)
            {
                double percent = owner.SupportQiCostPercent;
                if (percent != 0) cost = cost * (100.0 + percent) / 100.0;
            }

            int reduction = DisciplineCurve.QiCostReductionPercent(art.Discipline, mastery);
            if (reduction > 0) cost = cost * (100 - reduction) / 100.0;

            int result = (int)Math.Round(cost, MidpointRounding.AwayFromZero);
            return result < 0 ? 0 : result;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
