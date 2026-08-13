namespace Jianghu.Core.Martial.Morphemes
{
    /// <summary>
    /// 무공이 만지는 수치의 묶음. **형태소 하나가 주는 것도, 무공 전체의 합계도 같은 타입이다.**
    ///
    /// 이게 이 시스템의 핵심 장치다 — 정의서 §0 의 *"무공명 = 형태소 조합"* 은 코드에서
    /// `사전에서 꺼낸 델타들을 그냥 더한다` 로 환원된다. 그래서 <see cref="op_Addition"/> 만
    /// 정확하면 122개 무공의 수치가 자동으로 따라온다.
    ///
    /// ⚠ **`double` 인 이유**: 정의서에 `+1.5`(찌르기 공격) · `+0.5`(찌르기 속도) · `+0.3`(어둡다 치명배율) 이
    ///   있어 정수로는 담기지 않는다. 기존 코드가 `PowerMultiplier` 를 double 로 쓰고 있어 관례에도 맞다.
    ///   프로젝트 §1-2 의 `System.Random` 금지는 **난수**에 대한 규칙이지 부동소수에 대한 것이 아니다.
    ///
    /// ⚠ **`record` 를 쓰지 않는다** (프로젝트 §1-3, Unity 6 = C# 9). `readonly struct` 로 값 의미를 낸다.
    ///
    /// ⚠⚠ **여기 있는 축 중 상당수는 아직 전투 엔진이 읽지 않는다.** 막기·반격·치명·기력회복·
    ///   방어무시·상태이상 저항이 그렇다(설계안 §2 엔진 격차표). 정의서를 **먼저 온전히 옮겨 두고**
    ///   엔진을 한 축씩 붙이는 순서다(설계안 §4 4단계). 그래야 승률이 흔들렸을 때 원인이 분리된다.
    /// </summary>
    public readonly struct ArtStatDelta
    {
        // ── 공격·방어 본체 ──

        /// <summary>공격(攻擊). 정의서 §1-3 에서 성향 배율이 곱해지는 축이다.</summary>
        public double Attack { get; }

        /// <summary>방어(防禦).</summary>
        public double Defense { get; }

        /// <summary>명중(命中).</summary>
        public double Accuracy { get; }

        /// <summary>속도(速度) — 행동 순서.</summary>
        public double Speed { get; }

        // ── 확률축 (단위: %p) ──

        /// <summary>회피율(回避率) 증감(%p).</summary>
        public double Evasion { get; }

        /// <summary>막기확률(防禦率) 증감(%p).</summary>
        public double BlockChance { get; }

        /// <summary>반격률(反擊率) 증감(%p).</summary>
        public double CounterRate { get; }

        /// <summary>치명률(致命率) 증감(%p) — 치명타가 **터질 확률**(정의서 §1-2).</summary>
        public double CritChance { get; }

        /// <summary>치명배율(致命倍率) 증감(배수) — 치명타 시 **피해 배수**(정의서 §1-2).</summary>
        public double CritMultiplier { get; }

        /// <summary>방어무시(防禦無視) 비율(%). 극한경지 마(魔).</summary>
        public double DefenseIgnore { get; }

        /// <summary>
        /// **총 위력 증감(%)** — 범위 형태소(다·군·전)의 대가가 여기 들어간다 (2026-08-09 신설).
        ///
        /// ⚠⚠ <see cref="Attack"/>(합산 스칼라)과 **다른 축이다.** 저쪽은 형태소 공격 합에 더해지고,
        ///   이쪽은 <c>(캐릭터 공격 + 형태소 위력)</c> **전체에 곱해진다.** 갈라 둔 이유가 측정으로 나왔다:
        ///
        ///   범위의 옛 대가는 공격 합 감산(다 −1 · 군 −2 · 전 −3)이었는데 **대가 구실을 못 했다.**
        ///   ⓐ 만렙 캐릭터 공격 8 이 형태소 밖에 있어 감산이 닿지 못하고
        ///   ⓑ <c>CombatResolver.ArtPower</c> 가 음수를 0 으로 잘라 **일정 지점 아래에서는 감산이 무료**이며
        ///   ⓒ 무엇보다 **이득은 대상 수 곱셈(×2·×3·×4)인데 대가는 뺄셈**이라 애초에 급이 다르다.
        ///   실측: 4대4에서 범위 5종 평균 **85.6%** vs 대조 35.2%(10성). 공격 합 **−2.75** 로 카탈로그
        ///   최저인 `궤격비전` 이 **99.5%** 였다(HANDOFF §4-12).
        ///
        /// ⚠ 곱셈 축이므로 <see cref="QiCostPercent"/> 와 같은 꼴이다 — 100 을 더해 나눈다.
        /// ⚠ **성향·경지 배율보다 바깥에서 곱한다.** 안쪽에 넣으면 수련이 대가를 갉아먹는다.
        /// </summary>
        public double AttackPercent { get; }

        // ── 기력(氣力) ──

        /// <summary>최대기력 증감.</summary>
        public double MaxQi { get; }

        /// <summary>기력회복속도 증감(턴당).</summary>
        public double QiRegen { get; }

        /// <summary>
        /// 기력소모 증감(%). 극한경지 선(仙)의 `−30%` 가 여기 들어간다.
        ///
        /// ⚠ 곱할 **기준 소모량**은 정의서에 없었다(설계안 §1-B). 2026-07-29 결정 B 로
        ///   `기력 소모 = (본체 글자 수 − 배경어 수) × 상수` 로 정했고, 상수 초기값은 4 다.
        /// </summary>
        public double QiCostPercent { get; }

        // ── 상태이상(狀態異常) ──

        /// <summary>모든 상태이상의 부여확률 증감(%p). 극한경지 왕(王).</summary>
        public double StatusApplyBonus { get; }

        /// <summary>상태이상 저항(%p). 극한경지 성(聖).</summary>
        public double StatusResist { get; }

        /// <summary>중독(中毒) 부여확률(%p). 독(毒).</summary>
        public double PoisonChance { get; }

        /// <summary>출혈(出血) 부여확률(%p). 혈(血).</summary>
        public double BleedChance { get; }

        /// <summary>화상(火傷) 부여확률(%p). 염(炎).</summary>
        public double BurnChance { get; }

        /// <summary>동상(凍傷) 부여확률(%p). 빙(氷).</summary>
        public double FrostbiteChance { get; }

        /// <summary>
        /// 기력소실(氣力消失) 부여확률(%p). 탈(奪).
        ///
        /// ⚠⚠ 2026-07-29 추가. 정의서 §3-4 의 원래 5자에는 없었다 — **엔진에는 기력소실이 있는데
        ///   그것을 가리킬 글자가 없어서 정파 7문파가 상태이상을 이름으로 표현할 수 없었다.**
        ///   무공명을 실제로 지어보고서야 드러난 구멍이다(`martial-art-naming.md` §4).
        /// </summary>
        public double QiDrainChance { get; }

        /// <summary>
        /// 경직(硬直) 부여확률(%p). 경(硬).
        ///
        /// ⚠ 위와 같은 이유로 추가했다. 마도는 마비(비)만 가리킬 수 있어 절반만 표현됐다.
        ///   경직은 그 자체로도 명중을 깎지만, **3스택이 쌓이면 마비로 확정 전이**되는 것이 핵심이다.
        /// </summary>
        public double StaggerChance { get; }

        /// <summary>
        /// 마비(痲痺) 스택. 비(痺).
        ///
        /// ⚠ 다른 상태이상과 달리 **확률이 아니라 스택**이다. 현재 엔진에서 마비는
        ///   확률이 아니라 경직 3스택에서 확정 발동하는 게이팅이라(2026-07-28 설계),
        ///   정의서 §3-4 의 *"마비 스택 +1"* 도 그 구조를 따른다.
        /// </summary>
        public double ParalysisStack { get; }

        /// <summary>
        /// **공격 축 중 음수 성분만 따로 합한 값** (0 이하). 2026-08-05 신설.
        ///
        /// ⚠⚠ 왜 따로 드는가 — <see cref="Attack"/> 은 합산 스칼라라 *"이 안에 −0.75 짜리 페널티가
        ///   있었는지"* 를 잃어버린다. 그런데 `CombatResolver` 가 **공격에만 숙련 배율을 곱하므로**
        ///   페널티가 경지에 비례해 커지는 비대칭이 생겼다 — 기만 형태소가 3성 60.5 → 10성 51.7 로
        ///   혼자 시들었다(HANDOFF §4-6-6). 대가는 고정이어야 하므로 음수분을 배율 밖으로 빼야 하고,
        ///   그러려면 **얼마가 음수였는지**를 합산 뒤에도 알아야 한다.
        ///
        /// ⚠ 형태소 하나에서는 `min(0, attack)` 이고, 합칠 때 함께 더해지며, 종(宗)의 2배에서도
        ///   함께 2배가 된다 — *"페널티까지 함께 2배"* 라는 정의서 §3-8 규정을 그대로 따른다.
        /// ⚠ **이 값은 이미 <see cref="Attack"/> 안에 포함돼 있다.** 빼서 쓰는 쪽이 책임진다.
        /// </summary>
        public double AttackPenalty { get; }

        private ArtStatDelta(
            double attack, double defense, double accuracy, double speed,
            double evasion, double blockChance, double counterRate,
            double critChance, double critMultiplier, double defenseIgnore,
            double maxQi, double qiRegen, double qiCostPercent,
            double statusApplyBonus, double statusResist,
            double poisonChance, double bleedChance, double burnChance, double frostbiteChance,
            double qiDrainChance, double staggerChance, double paralysisStack,
            double attackPenalty, double attackPercent)
        {
            Attack = attack;
            AttackPenalty = attackPenalty;
            AttackPercent = attackPercent;
            Defense = defense;
            Accuracy = accuracy;
            Speed = speed;
            Evasion = evasion;
            BlockChance = blockChance;
            CounterRate = counterRate;
            CritChance = critChance;
            CritMultiplier = critMultiplier;
            DefenseIgnore = defenseIgnore;
            MaxQi = maxQi;
            QiRegen = qiRegen;
            QiCostPercent = qiCostPercent;
            StatusApplyBonus = statusApplyBonus;
            StatusResist = statusResist;
            PoisonChance = poisonChance;
            BleedChance = bleedChance;
            BurnChance = burnChance;
            FrostbiteChance = frostbiteChance;
            QiDrainChance = qiDrainChance;
            StaggerChance = staggerChance;
            ParalysisStack = paralysisStack;
        }

        /// <summary>
        /// 아무 수치도 만지지 않는 델타. 부정(§3-9)·무학분류(§3-10)·배경어가 이걸 갖는다.
        ///
        /// ⚠ 이 셋이 0 인 것은 **버그가 아니라 설계다.** 부정·무학분류는 상성 규칙(§4)으로
        ///   일하고, 배경어는 어감으로만 일한다.
        /// </summary>
        public static ArtStatDelta Zero => default;

        /// <summary>
        /// 이름 붙인 인자로 델타를 만든다. 사전 기술이 정의서 표와 나란히 읽히도록 하는 것이 목적이다.
        /// 예: `ArtStatDelta.Of(attack: 1.5, speed: 0.5)` ← 정의서 §3-1 찌르기 행 그대로.
        /// </summary>
        public static ArtStatDelta Of(
            double attack = 0, double defense = 0, double accuracy = 0, double speed = 0,
            double evasion = 0, double blockChance = 0, double counterRate = 0,
            double critChance = 0, double critMultiplier = 0, double defenseIgnore = 0,
            double maxQi = 0, double qiRegen = 0, double qiCostPercent = 0,
            double statusApplyBonus = 0, double statusResist = 0,
            double poisonChance = 0, double bleedChance = 0, double burnChance = 0, double frostbiteChance = 0,
            double qiDrainChance = 0, double staggerChance = 0, double paralysisStack = 0,
            double attackPercent = 0)
        {
            return new ArtStatDelta(
                attack, defense, accuracy, speed,
                evasion, blockChance, counterRate,
                critChance, critMultiplier, defenseIgnore,
                maxQi, qiRegen, qiCostPercent,
                statusApplyBonus, statusResist,
                poisonChance, bleedChance, burnChance, frostbiteChance,
                qiDrainChance, staggerChance, paralysisStack,
                // ⚠ 형태소 하나의 페널티는 그 글자의 음수 공격분이다. 합·곱은 아래 연산자가 잇는다.
                attack < 0 ? attack : 0,
                attackPercent);
        }

        /// <summary>
        /// 축별 단순 합산. **무공 전체 수치는 이 연산 하나로 나온다.**
        ///
        /// ⚠ 상한·하한을 여기서 걸지 않는다. 공격 합이 음수가 될 수 있고(`사(+0.5)` + `환(−2)` = −1.5,
        ///   설계안 §1-E) 그게 문제인지 여부는 §5-4 민감도 측정으로 판정할 사안이다.
        ///   **측정 전에 값을 잘라내면 측정 대상 자체가 사라진다.**
        /// </summary>
        public static ArtStatDelta operator +(ArtStatDelta a, ArtStatDelta b)
        {
            return new ArtStatDelta(
                a.Attack + b.Attack,
                a.Defense + b.Defense,
                a.Accuracy + b.Accuracy,
                a.Speed + b.Speed,
                a.Evasion + b.Evasion,
                a.BlockChance + b.BlockChance,
                a.CounterRate + b.CounterRate,
                a.CritChance + b.CritChance,
                a.CritMultiplier + b.CritMultiplier,
                a.DefenseIgnore + b.DefenseIgnore,
                a.MaxQi + b.MaxQi,
                a.QiRegen + b.QiRegen,
                a.QiCostPercent + b.QiCostPercent,
                a.StatusApplyBonus + b.StatusApplyBonus,
                a.StatusResist + b.StatusResist,
                a.PoisonChance + b.PoisonChance,
                a.BleedChance + b.BleedChance,
                a.BurnChance + b.BurnChance,
                a.FrostbiteChance + b.FrostbiteChance,
                a.QiDrainChance + b.QiDrainChance,
                a.StaggerChance + b.StaggerChance,
                a.ParalysisStack + b.ParalysisStack,
                a.AttackPenalty + b.AttackPenalty,
                a.AttackPercent + b.AttackPercent);
        }

        /// <summary>
        /// 배수를 곱한다. 극한경지 종(宗)의 *"무공형태 효과 2배(페널티 포함)"* 가 이걸 쓴다.
        ///
        /// ⚠ 성향 배율(<c>AlignmentCurve</c>)은 여기서 곱하지 않는다. 정의서 §1-3 은 성향 배율을
        ///   **공격축에만** 명시하고, 보조 무공 적용 여부는 아직 미결이다(설계안 §1-C).
        ///   그 결정이 서기 전에 여기서 곱해버리면 미결 사항이 코드에 굳는다.
        /// </summary>
        public static ArtStatDelta operator *(ArtStatDelta a, double factor)
        {
            return new ArtStatDelta(
                a.Attack * factor,
                a.Defense * factor,
                a.Accuracy * factor,
                a.Speed * factor,
                a.Evasion * factor,
                a.BlockChance * factor,
                a.CounterRate * factor,
                a.CritChance * factor,
                a.CritMultiplier * factor,
                a.DefenseIgnore * factor,
                a.MaxQi * factor,
                a.QiRegen * factor,
                a.QiCostPercent * factor,
                a.StatusApplyBonus * factor,
                a.StatusResist * factor,
                a.PoisonChance * factor,
                a.BleedChance * factor,
                a.BurnChance * factor,
                a.FrostbiteChance * factor,
                a.QiDrainChance * factor,
                a.StaggerChance * factor,
                a.ParalysisStack * factor,
                a.AttackPenalty * factor,
                // ⚠ 종(宗)은 **무공형태**만 2배로 만들고 범위 형태소에는 닿지 않으므로 이 줄은 지금 도달하지 않는다.
                //   그래도 다른 축과 같이 곱해 둔다 — *"페널티까지 함께 2배"*(정의서 §3-8)의 일관성이다.
                a.AttackPercent * factor);
        }

        /// <summary>아무 축도 만지지 않는가. 부정·무학분류·배경어 판별과 사전 무결성 테스트에 쓴다.</summary>
        public bool IsZero
        {
            get
            {
                return Attack == 0 && Defense == 0 && Accuracy == 0 && Speed == 0
                       && Evasion == 0 && BlockChance == 0 && CounterRate == 0
                       && CritChance == 0 && CritMultiplier == 0 && DefenseIgnore == 0
                       && MaxQi == 0 && QiRegen == 0 && QiCostPercent == 0
                       && StatusApplyBonus == 0 && StatusResist == 0
                       && PoisonChance == 0 && BleedChance == 0 && BurnChance == 0 && FrostbiteChance == 0
                       && QiDrainChance == 0 && StaggerChance == 0 && ParalysisStack == 0;
            }
        }
    }
}
