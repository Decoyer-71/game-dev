namespace Jianghu.Core.Martial
{
    /// <summary>
    /// **무공 경지(境地) — 1성부터 10성까지.** 무협에서 무공 수련의 단위로 쓰는 그 "성(成)" 이다.
    ///
    /// ⚠⚠ 2026-07-31 신설. 그전까지 코드에는 **숙련도 0~100 밖에 없었고**, 그래서 측정도 문서도
    ///   *"수련 200회"* 처럼 **횟수**로 말하고 있었다. 횟수는 성향마다 뜻이 달라진다 —
    ///   같은 200회가 정파에게는 10성이고 마도에게는 9성이다(학습률이 0.70 대 0.45).
    ///   경지로 말하면 그 차이가 사라진다: **10성은 어느 성향에게나 10성**이고,
    ///   성향이 다른 것은 *"거기 도달하는 비용"* 뿐이다 — `AlignmentCurve` 가 원래 그렇게 설계돼 있다.
    ///
    /// ⚠ **캐릭터 레벨과 다른 축이다.** 캐릭터 능력치는 <see cref="Characters.CharacterStats"/> 의
    ///   시작/만렙으로 자라고, 무공 경지는 무공 하나하나가 따로 쌓는다. 둘을 한 변수로 뭉쳐 재면
    ///   *"무공이 세진 것인지 사람이 세진 것인지"* 를 분리할 수 없다.
    ///
    /// ⚠ 지금은 **표기·측정의 단위**이고 전투 공식은 여전히 숙련도(연속값)로 돈다.
    ///   경지 단위로 계단식 성장(9성까지 아무 변화 없다가 10성에서 도약)으로 만들지는 미결이다.
    ///
    /// **경지 → 수련 횟수** (숙련 상한 100 = 10성이므로, 지금까지 재 온 "상한 도달 횟수" 가 곧 10성이다)
    ///
    /// | 축 | 1성 | 6성 | **10성** |
    /// |---|---|---|---|
    /// | 창(백일창) | 5회 | 30회 | **50회** |
    /// | 권 | 9 | 50 | **84** |
    /// | 비도 | 10 | 60 | **100** |
    /// | 도 · 정파 | 15 | 86 | **143** |
    /// | 사파 | 7 | 38 | **138** (소프트캡 70 이후 5배 느려진다) |
    /// | 마도 | 23 | 134 | **222** |
    /// | 검(만일검) | 25 | 150 | **250** |
    /// </summary>
    public static class MartialStage
    {
        /// <summary>최고 경지. 10성이 무공 수련의 종착점이다.</summary>
        public const int MaxStage = 10;

        /// <summary>경지 하나에 해당하는 숙련도 폭. 숙련 상한 100 을 10등분한 값이다.</summary>
        public const int ProficiencyPerStage = 10;

        /// <summary>
        /// 숙련도로부터 경지를 구한다. **숙련 상한(100)에 닿으면 10성**이다.
        ///
        /// ⚠⚠ 이 대응이 요점이다 (2026-07-31 사용자 확정) — 지금까지 재 온 *"상한 도달 수련 횟수"*가
        ///   그대로 **10성 도달 시점**이 된다. 창은 50회, 검은 250회, 정파 무공은 143회에 10성이다.
        ///   축마다 횟수가 달라도 **경지로 말하면 같은 지점**을 가리킨다.
        /// ⚠ 0성은 없다 — 무공을 배웠다는 것 자체가 1성이다.
        /// </summary>
        public static int StageOf(int proficiency)
        {
            if (proficiency <= 0) return 1;

            // 올림 — 숙련 1 도 1성, 100 이어야 10성이다.
            int stage = (proficiency + ProficiencyPerStage - 1) / ProficiencyPerStage;
            return stage > MaxStage ? MaxStage : stage;
        }

        /// <summary>그 경지에 도달하는 숙련도. 1성 = 10 · 6성 = 60 · **10성 = 100(상한)**.</summary>
        public static int ProficiencyForStage(int stage)
        {
            if (stage < 1) stage = 1;
            if (stage > MaxStage) stage = MaxStage;
            return stage * ProficiencyPerStage;
        }

        /// <summary>
        /// **측정 기준점 셋** — 초반 · 중반 · 후반.
        ///
        /// ⚠⚠ 이 프로토타입이 검증하는 것은 *"무공 조합이 전투 결과를 바꾸는가"* 이므로,
        ///   측정에서 움직이는 변수는 **무공 경지 하나**여야 한다(캐릭터 레벨·유형 숙달은 고정).
        /// ⚠ 후반은 **10성 = 무공 경지의 최종점**이다. 그 너머는 없다.
        /// </summary>
        public static readonly int[] MeasurementStages = { 3, 6, MaxStage };

        /// <summary>`7성` 처럼 사람이 읽는 표기.</summary>
        public static string Describe(int stage)
        {
            return stage + "성";
        }
    }
}
