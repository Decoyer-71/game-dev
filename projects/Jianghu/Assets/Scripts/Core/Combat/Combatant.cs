using System;
using System.Collections.Generic;
using Jianghu.Core.Characters;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;

namespace Jianghu.Core.Combat
{
    /// <summary>
    /// 전투에 나서는 한 사람의 **정의**(이름 · 능력치 · 익힌 무공).
    ///
    /// 전투 중에 변하는 값(현재 체력·기력)은 여기 없다 — <see cref="CombatResolver"/> 가
    /// 내부에서 따로 관리한다. 그래야 같은 Combatant 를 같은 시드로 몇 번을 붙여도
    /// 항상 같은 결과가 나온다. Phase 2 의 승률표를 뽑으려면 이 성질이 반드시 필요하다.
    /// </summary>
    public sealed class Combatant
    {
        private static readonly IReadOnlyList<DisciplineMastery> NoMastery = new DisciplineMastery[0];

        public string Name { get; }
        public CharacterStats Stats { get; }
        public IReadOnlyList<LearnedArt> Arts { get; }

        /// <summary>무기 유형별 숙달도(백일창·천일도·만일검). 비워두면 전부 미숙달로 취급한다.</summary>
        public IReadOnlyList<DisciplineMastery> Masteries { get; }

        /// <summary>
        /// 이 사람의 **무학분류**(양기·음기·혼합) — 상성(정의서 §4)이 겨누는 과녁이다.
        ///
        /// 출처는 **소속 문파**다(정의서 §6-4, `SchoolCatalog` 의 `School.Lineage`).
        /// `null` 이면 무소속으로 취급하고 **상성이 성립하지 않는다** — 어느 분류도 아닌 사람은
        /// 찌를 곳이 없기 때문이다.
        ///
        /// ⚠⚠ **무소속이 생각보다 적다.** 공격 초식 71종 중 분류가 없는 것은 **11종(15.5%)** 뿐이다 —
        ///   강호무학 5 + **무림맹·사도련·제천성 6**. 대형세력 3곳이 `SchoolCatalog` 에 없어
        ///   분류가 `null` 로 떨어지기 때문이며(정의서 §6-4 의 빈칸), 천마신교만 문파를 겸해 분류를 갖는다.
        ///   → 상성·통(統)은 **매치업의 약 85% 에서 발동한다.** 조건부이되 조건이 자주 성립한다.
        ///
        /// ⚠⚠ **2026-08-02 신설.** 정의서 §6-4 가 *"상성이 작동하려면 누가 어느 분류인가가 정해져
        ///   있어야 한다"* 고 못박고 문파 16곳에 분류를 배정해 뒀는데, **`Combatant` 에 그 값을 담을
        ///   자리가 없어서** 상성이 갈 곳이 없었다. 문파 쪽 데이터는 처음부터 갖춰져 있었다.
        ///
        /// ⚠ 기본값이 `null` 인 것은 의도다 — 기존 테스트·측정 도구가 분류를 지정하지 않으므로
        ///   상성이 저절로 켜지지 않는다. **켜는 쪽이 명시하게** 두어야 원인 분리가 된다.
        /// </summary>
        public ArtLineage? Lineage { get; }

        public Combatant(
            string name, CharacterStats stats, IReadOnlyList<LearnedArt> arts,
            IReadOnlyList<DisciplineMastery> masteries = null,
            ArtLineage? lineage = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("이름은 비어 있을 수 없다.", nameof(name));
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            if (arts == null) throw new ArgumentNullException(nameof(arts));

            Name = name;
            Stats = stats;
            Arts = arts;
            Masteries = masteries ?? NoMastery;
            Lineage = lineage;
        }

        /// <summary>
        /// **익힌 무공 전부**를 통틀어 <paramref name="target"/> 분류에 갖는 상성 수(정의서 §4).
        /// 대상이 무소속(`null`)이면 0 이다.
        ///
        /// ⚠ 이것은 **방어 쪽 계산용**이다 — *"받는 피해 −5%"* 는 어느 초식을 쓰는 중인지와
        ///   무관한 상시 성질이므로, 방어군 4축(<see cref="EffectiveDefense"/> · <see cref="Evasion"/> …)이
        ///   이미 쓰는 *"익힌 무공 전부 합산"* 규칙을 그대로 따른다.
        ///   **공격 쪽**(*"주는 피해 +10%"*)은 반대로 **그 순간 쓰는 초식**의 상성만 센다 —
        ///   `Delta.Attack` 이 활성 무공에서만 오는 것과 같은 이유다. 상성은 그 초식의 성질이다.
        ///
        /// ⚠ 숙련 배율을 곱하지 않는다. 상성은 정의서 §4 에서 **개수(정수)** 로 정의된 축이고,
        ///   수련으로 "상성이 1.7개" 가 되는 것은 이름이 뜻하는 바가 아니다.
        /// </summary>
        public int CounterCountAgainst(ArtLineage? target)
        {
            if (target == null) return 0;

            int count = 0;
            for (int i = 0; i < Arts.Count; i++)
            {
                IReadOnlyList<ArtLineage> targets = Arts[i].Art.CounterTargets;
                for (int j = 0; j < targets.Count; j++)
                {
                    if (targets[j] == target.Value) count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 이 사람이 <paramref name="rule"/> 절대경지 규칙을 갖고 있는가 (정의서 §5-3).
        ///
        /// ⚠⚠ **합산이 아니라 `any` 다.** 절대경지 규칙은 불리언이라 <see cref="Martial.Morphemes.ArtStatDelta"/>
        ///   에 넣을 수 없다 — 델타는 22축이 전부 `double` 이고 무공을 여럿 배우면 더해지므로,
        ///   불리언을 숫자로 담으면 *"면역이 두 겹"* 이라는 의미 없는 값이 생긴다.
        ///
        /// ⚠ **배운 순간부터 상시 적용**된다. 그 무공을 전투에서 "쓸 때만" 이 아니다 —
        ///   절대경지 4종은 전부 **내공 무공**이고, 내공·경공이 사람에게 상시 붙는 것은
        ///   <see cref="EffectiveMaxQi"/>·<see cref="QiRegenPerTurn"/>·<see cref="SupportQiCostPercent"/> 가
        ///   이미 쓰는 규칙이다. 그 관례를 그대로 따른다.
        ///
        /// ⚠ 숙련 배율을 곱하지 않는다 — 규칙은 정도(程度)가 없다. 면역이 1.7겹일 수 없다.
        /// </summary>
        public bool HasRule(AbsoluteRule rule)
        {
            for (int i = 0; i < Arts.Count; i++)
            {
                if (Arts[i].Art.Rule == rule) return true;
            }
            return false;
        }

        /// <summary>면(免) — 모든 상태이상 면역. 무림맹 절대경지.</summary>
        public bool IsStatusImmune => HasRule(AbsoluteRule.StatusImmunity);

        /// <summary>
        /// 무(無) — 기력 무소모. 천마신교 절대경지.
        /// ⚠⚠ **엔진은 정상 작동한다**(2026-08-02 `diagnosis` 확인 — 전투 로그에서 보유자만 기력 차감이 사라진다).
        ///   ~~기력 축이 죽어 있어 현재 효과 0~~ → 축은 §1-1-c 로 살아났다.
        ///   지금 승률표에서 0.00 인 것은 **측정 블록의 대조군이 스스로 압력을 지우기 때문**이다
        ///   (대조군 `식유수` 의 식息 = 기력소모 −10%). HANDOFF §4-3-8 미결.
        /// </summary>
        public bool HasNoQiCost => HasRule(AbsoluteRule.NoQiCost);

        /// <summary>쌍(雙) — 한 턴에 2회 행동. 사도련 절대경지.</summary>
        public bool ActsTwice => HasRule(AbsoluteRule.DoubleAction);

        /// <summary>통(統) — 모든 분류에 상성 +1(과녁이 있을 때만), 상대 상성 무효. 제천성 절대경지.</summary>
        public bool HasCounterSupremacy => HasRule(AbsoluteRule.CounterSupremacy);

        /// <summary>해당 무기 유형의 숙련도. 익힌 적이 없으면 0.</summary>
        public int MasteryOf(Discipline discipline)
        {
            for (int i = 0; i < Masteries.Count; i++)
            {
                if (Masteries[i].Discipline == discipline) return Masteries[i].Proficiency;
            }
            return 0;
        }

        /// <summary>
        /// 무공이 더해진 실제 최대 기력. 보조 효과에는 숙련 배율이 곱해진다.
        ///
        /// ⚠⚠ **2026-08-02 — 유형 필터를 없앴다**(사용자 확정 · HANDOFF §4-2-V).
        ///   그전까지 여기만 `Discipline != InnerArt` 로 걸러서 **공격 무공에 넣은 최대기력 형태소가
        ///   아무 일도 안 했다** — 양(陽 +10)은 완전 무효, 합(合 +5·회복+0.5)은 회복만, 식(息 +3)도 무효.
        ///
        ///   ⚠ 이것은 설계가 아니라 **비대칭**이었다. 형태소 상시 축 일곱(최대기력·방어·회복·막기·
        ///   반격·회피·속도) 중 유형 필터를 가진 것은 여기 하나뿐이었고, 특히 **같은 카테고리(§3-3)의
        ///   형제인 음(陰 회복)은 공격 무공에서 멀쩡히 작동**했다. 회복이 되는데 최대기력이 안 될
        ///   이유가 없다.
        ///
        ///   ⚠ 출하된 카탈로그 138종에는 **내공 형태소를 담은 공격 무공이 0종**이라 이 변경으로
        ///   승률·계층·유형 지표가 하나도 움직이지 않는다(`Tools/Sandbox` 전 지표 `diff` 0줄, 실측).
        ///   고치는 이유는 밸런스가 아니라 **플레이어가 직접 무공을 지을 때**(독문무공 창시, 정의서 §0)
        ///   이름이 성능을 거짓말하기 때문이다.
        ///
        ///   ⚠ 정의서 §1-1 은 *"최대기력은 내공 무공의 축"* 이라고 적어 뒀다. 그 원칙은 **캐릭터
        ///   능력치 성장**에 대한 것이고, 형태소를 어디에 쓸지는 조합 규칙이 이미 허용해 왔다 —
        ///   정의서에 예외를 명기했다.
        ///
        /// ⚠ 레거시 `MaxQiBonus` 는 영향받지 않는다. `MartialArt.Technique()` 이 그 값을 항상 0 으로
        ///   고정하므로 0 이 아닌 레거시 무공은 애초에 내공·경공뿐이다.
        /// </summary>
        public int EffectiveMaxQi
        {
            get
            {
                double bonus = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];

                    // ⚠ 형태소 무공은 `Delta.MaxQi`(양 +10 · 합 +5 · 식 +3 · 선 +20)에서,
                    //   레거시 무공은 손으로 박은 `MaxQiBonus` 에서 읽는다. 과도기 분기다.
                    double raw = learned.Art.IsMorphemeDerived ? learned.Art.Delta.MaxQi : learned.Art.MaxQiBonus;
                    bonus += raw * learned.PowerMultiplier;
                }
                return Stats.MaxQi + (int)Math.Round(bonus);
            }
        }

        /// <summary>
        /// **보조 무공(내공·경공)이 주는 기력소모 증감(%).** 모든 초식에 적용된다.
        ///
        /// ⚠⚠ **2026-08-02 신설**(사용자 확정 · HANDOFF §4-2-V). 그전까지 소모율은
        ///   <see cref="Morphemes.ParsedArtName.QiCost"/> 안에서 **그 무공 자신의 소모에만** 곱해졌고,
        ///   그래서 식(息 −10%)이 **자기 유일한 필수 자리인 내공 무공에서 아무 일도 안 했다** —
        ///   조합 규칙이 내공 무공에 내공 형태소를 요구하는데(§2-2), 내공 무공은 시전되지 않으므로
        ///   자기 소모량이라는 것이 존재하지 않기 때문이다. 실측 민감도 **+0.25%p = 완전 무효**였다.
        ///
        /// **판단 기준은 위치다** — *"보조 무공은 시전되지 않으므로 자기 소모량이 없다. 거기 적힌
        ///   소모율은 '이 사람이 기를 아껴 쓴다' 는 뜻일 수밖에 없다."*
        ///   ⚠ 반대로 **공격 무공의 소모율은 국소로 남는다.** 범위 만(萬 +200%)은 *"이 초식이
        ///   전원을 때리니 이 초식이 비싸다"* 라서 사람에게 붙으면 뜻이 무너진다.
        ///   (만은 조합 규칙상 공격 무공 전용이라 여기 섞일 수도 없다.)
        ///
        /// ⚠ 숙련 배율을 곱한다 — <see cref="EffectiveMaxQi"/>·<see cref="QiRegenPerTurn"/> 과 같은 처리다.
        ///   만렙 정파 2.15배 기준 식 하나면 실효 약 −21.5% 다.
        /// </summary>
        public double SupportQiCostPercent
        {
            get
            {
                double sum = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];
                    if (!learned.Art.IsMorphemeDerived) continue;     // 레거시 무공엔 소모율 필드가 없다
                    if (!learned.Art.Discipline.IsSupport()) continue;
                    sum += learned.Art.Delta.QiCostPercent * learned.PowerMultiplier;
                }
                return sum;
            }
        }

        /// <summary>
        /// **무공이 더한 방어.** 캐릭터 방어 + 형태소 방어 합(× 숙련 배율).
        ///
        /// ⚠⚠ 2026-07-31 신설. 그전까지 엔진은 `Stats.Defense` 만 읽었고 —
        ///   **무공이 방어를 올리는 경로가 아예 없었다.** 설계안 §1-D 가 방어 스케일을 계산하며
        ///   *"이 표는 아직 존재하지 않는 경로를 전제한다"* 고 스스로 단서를 달았던 바로 그 구멍이다.
        ///   방어 형태소(방·거·항·어·호 +2 · 반·역·응 +1)가 갈 곳이 없어 절반이 죽어 있었다.
        ///
        /// ⚠ **숙련 배율을 곱한다.** 공격이 `Delta.Attack × PowerMultiplier` 로 자라므로
        ///   방어만 상수로 두면 수련할수록 방어의 상대가치가 무너진다(설계안 §1-D 표).
        ///   ⚠ 확률축(막기·반격·회피)은 반대로 **곱하지 않는다** — 명중·치명과 같은 처리다.
        /// </summary>
        public int EffectiveDefense
        {
            get
            {
                double bonus = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];
                    if (!learned.Art.IsMorphemeDerived) continue;   // 레거시 36종엔 방어 필드가 없다
                    bonus += learned.Art.Delta.Defense * learned.PowerMultiplier;
                }
                return Stats.Defense + (int)Math.Round(bonus);
            }
        }

        /// <summary>
        /// 정의서 §1-1 의 기본 기력회복속도. ⚠ 미검증 초기값이다.
        /// `CharacterStats` 에 회복 속도 필드가 없어 상수로 둔다 — 필드로 올릴지는 측정 후 판단한다.
        /// </summary>
        /// <remarks>
        /// ⚠⚠ **2026-07-30 측정 근거로 2 → 10 으로 올렸다.**
        ///   정의서 §1-1 은 2 였는데, 실측해 보니 4자 무공(기력 16)을 **8턴에 한 번**밖에 못 썼다.
        ///   설계안 §5-3 의 **평타 전락률 목표 10~30%** 에 견주면 실제 전락률이 **약 87%** 였고,
        ///   그 결과 체력 100 을 50턴 안에 못 깎아 **전투가 다시 무승부로 돌아갔다.**
        ///
        ///   10 으로 올리면 4자 무공을 **1.6턴에 한 번** 쓰게 되어 전락률이 ~37% 가 된다.
        ///   여기에 내공 형태소(음 +2 · 합 +0.5 · 수 +1)를 더하면 25% 안팎으로 목표 구간에 들어온다.
        ///   → **내공 무공을 배워야 초식을 끊김 없이 쓸 수 있다**는 관계가 이 값에서 생긴다.
        ///     내공의 존재 이유가 수치로 성립하는 지점이다.
        ///
        /// ⚠⚠ **위 문단의 수치는 전부 기력 소모 상수 4 시절(2026-07-30)이다.** 상수가 3 으로
        ///   내려간 뒤(커밋 9605db8) 4자 무공은 16 이 아니라 **12** 이고, 2026-08-01 에 평타
        ///   전락률을 처음 실측했더니 **세 경지 · 다섯 유형 전부 0.0%** 였다. 즉 지금은 아무도
        ///   마르지 않는다 — 위가 서술하는 관계("내공을 배워야 끊김 없이 쓴다")가 **현재는 성립하지
        ///   않는다.** 기록을 지우지 않고 남기되(§4), 현재 상태와 혼동하지 말 것.
        ///   측정은 `Tools/Sandbox` 의 `PrintQiPressure` 가 한다.
        ///
        /// ⚠ 여전히 미검증이다. 전락률을 직접 재는 지표(§5-3)를 아직 안 만들었다.
        /// </remarks>
        public const int BaseQiRegen = 10;

        /// <summary>
        /// **상태이상 저항(%p)** — 부여확률에서 그대로 빼는 값이다. 극한경지 성(聖) 하나만 갖는다(30).
        ///
        /// ⚠⚠ **2026-08-02 연결.** 그전까지 `ArtStatDelta.StatusResist` 를 **아무도 읽지 않아**
        ///   성(聖)의 세 축 중 저항만 죽어 있었다(방어 +5 · 막기 +25 는 살아 있어 부분 손실).
        ///   인계문서 §3-2 가 미연결로 적어 둔 축이고, 같은 목록의 `DefenseIgnore`(마 魔)와 함께 이었다.
        ///
        /// ⚠ 방어군 4축과 같은 규칙을 따른다 — **익힌 무공 전부 합산 · 숙련 배율 곱함.**
        ///   저항은 어느 초식을 쓰는 중인지와 무관한 상시 성질이다.
        ///
        /// ⚠ 상한을 여기서 걸지 않는다. 부여확률 쪽에서 `Clamp(0, 100)` 이 이미 걸리고,
        ///   여기서 미리 자르면 *"저항 몇까지 쌓였는가"* 가 관측에서 사라진다.
        /// </summary>
        public int StatusResistPercent
        {
            get
            {
                double bonus = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];
                    if (!learned.Art.IsMorphemeDerived) continue;

                    bonus += learned.Art.Delta.StatusResist * learned.PowerMultiplier;
                }
                return (int)Math.Round(bonus);
            }
        }

        /// <summary>
        /// 턴당 기력 회복량. 기본값에 내공 무공의 형태소 회복(음 +2 · 합 +0.5 · 수 +1 · 선 +3)이 더해진다.
        ///
        /// ⚠⚠ **이 값이 전투 성립 여부를 가른다.** 0 이면 4자 무공 기준 3턴 만에 기력이 마르고
        ///   평타(피해 1)로 전락해 전투가 끝나지 않는다(2026-07-30 실측: 전원 무승부).
        ///   설계안 §5-3 의 **평타 전락률**(목표 10~30%)이 이 값의 적정성을 판정한다.
        /// </summary>
        public int QiRegenPerTurn
        {
            get
            {
                double bonus = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];
                    if (!learned.Art.IsMorphemeDerived) continue;

                    // ⚠ 숙련 배율을 곱한다 — `EffectiveMaxQi` 와 같은 처리다(설계안 §1-C).
                    //   ⚠⚠ 단 **체력 회복은 곱하지 않기로 했다**(2026-07-30). 오래 버티는 축이
                    //   수련으로 증폭되면 후반이 교착되기 때문이다. 기력과 체력을 갈라 다룬다.
                    bonus += learned.Art.Delta.QiRegen * learned.PowerMultiplier;
                }
                return BaseQiRegen + (int)Math.Round(bonus);
            }
        }

        /// <summary>
        /// **막기 확률(%p) 중 무공이 주는 몫.** 기본값은 <c>CombatResolver.BaseBlockChance</c> 가 갖는다.
        ///
        /// ⚠ 확률축이므로 **숙련 배율을 곱하지 않는다** — 명중·치명과 같은 처리다(정의서 §1-3-a).
        /// ⚠ 방·거·항·어·호 +10%p · 계(戒) +25%p · 극한경지 성(聖) +15%p 가 여기로 들어온다.
        /// </summary>
        public int BlockChanceBonus
        {
            get
            {
                double bonus = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];
                    if (!learned.Art.IsMorphemeDerived) continue;
                    bonus += learned.Art.Delta.BlockChance;
                }
                return (int)Math.Round(bonus);
            }
        }

        /// <summary>
        /// **반격 확률(%p).** 반·역·응 +10%p.
        ///
        /// ⚠⚠ 기본값이 없다. 막기·회피와 달리 **형태소 없이는 아예 일어나지 않는 일**로 둔다 —
        ///   정의서 §1-1 이 막기·회피는 캐릭터 기본값으로 적었지만 반격은 적지 않았다.
        /// </summary>
        public int CounterRate
        {
            get
            {
                double bonus = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];
                    if (!learned.Art.IsMorphemeDerived) continue;
                    bonus += learned.Art.Delta.CounterRate;
                }
                return (int)Math.Round(bonus);
            }
        }

        /// <summary>내공 무공이 주는 초식 위력 보너스(%). 모든 초식에 곱연산으로 적용된다.</summary>
        public int PowerBonusPercent
        {
            get
            {
                double bonus = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];
                    if (learned.Art.Discipline == Discipline.InnerArt)
                    {
                        bonus += learned.Art.PowerBonusPercent * learned.PowerMultiplier;
                    }
                }
                return (int)Math.Round(bonus);
            }
        }

        /// <summary>
        /// **형태소 회피 1점을 회피율 몇 %p 로 볼 것인가** (2026-07-31 신설).
        ///
        /// ⚠⚠ 정의서 §3-2 는 피·둔·섬을 `회피 +15%` 로 적었는데, 그대로 %p 로 넣으면
        ///   민감도 **77~80% = 지배적**이었다. 회피는 **모든 피격에** 걸리므로 방어(66%)보다도 크다 —
        ///   방어 공식에서 배운 것과 같은 이야기다: *"받는 피해 −X% 는 주는 피해 +X% 보다 값이 크다."*
        ///   0.4 로 환산하면 6%p 가 되어 60~61% 로 들어온다.
        ///
        /// ⚠ 사전 값을 고치지 않고 **엔진에서 환산**하는 쪽을 택했다. 명중이 이미 그렇게 돼 있고
        ///   (`AccuracyPointToPercent`), 사전은 정의서를 옮기는 자리라 손대면 두 문서가 갈라진다.
        /// </summary>
        public const double EvasionPointToPercent = 0.3;

        /// <summary>
        /// **캐릭터 기본 회피(%p)** — 정의서 §1-1 이 *"시작 5 / 만렙 5"* 로 적어 둔 값이다.
        ///
        /// ⚠⚠ **2026-08-02 연결.** 정의서가 이 행에 *"⚠ 아직 엔진 미연결"* 을 스스로 달아 두고 있었다.
        ///   그전까지 회피의 기반은 `Stats.Agility / 2` 뿐이라 **시작 캐릭터의 회피가 0** 이었다.
        ///
        /// ⚠ 신법(<see cref="CharacterStats.Agility"/>)과 **다른 것**이다. 신법은 1 → 4 로 자라고
        ///   이 값은 시작·만렙이 똑같이 5 다 — 정의서가 성장하지 않는 상수로 적었다.
        ///   *"누구나 맞기 전에 몸을 튼다"* 는 바닥값이고, 신법은 그 위에 쌓이는 성장분이다.
        /// </summary>
        public const int BaseEvasion = 5;

        /// <summary>
        /// 회피 수치. 신법 절반이 기반이고 경공 무공이 얹힌다.
        ///
        /// ⚠⚠ 2026-07-31 — **형태소 회피(피·둔·섬)를 잇었다.** 그전에는 경공 무공의
        ///   레거시 `EvasionBonus` 만 읽어서, 방어 카테고리 12자 중 회피 3자가 통째로 죽어 있었다.
        ///   ⚠ 민감도표에 방어 카테고리가 아예 없어서 **죽은 줄도 몰랐다** — 측정하지 않는 축은
        ///   고장 나도 보이지 않는다는 사례로 남긴다.
        ///
        /// ⚠ 형태소분에는 **숙련 배율을 곱하지 않는다**(확률축 공통 규칙). 레거시분은 기존대로 곱한다 —
        ///   과도기 분기이며 카탈로그가 138종으로 넘어가면 사라진다.
        /// </summary>
        public int Evasion
        {
            get
            {
                double bonus = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];
                    if (learned.Art.IsMorphemeDerived)
                    {
                        bonus += learned.Art.Delta.Evasion * EvasionPointToPercent;
                        continue;
                    }
                    if (learned.Art.Discipline == Discipline.Movement)
                    {
                        bonus += learned.Art.EvasionBonus * learned.PowerMultiplier;
                    }
                }
                return BaseEvasion + Stats.Agility / 2 + (int)Math.Round(bonus);
            }
        }

        /// <summary>
        /// **속도(速度)** — 신법 + 형태소 속도. 정의서 §1-1 의 독립 스탯이다.
        ///
        /// ⚠⚠ 2026-07-31 — **선공(<see cref="Initiative"/>)에서 갈라냈다.** 선공에는 창 숙달(+25)이
        ///   섞여 있어서, 속도를 선공으로 대신 쓰면 **창을 든 사람이 추가 행동을 독식한다.**
        ///   설계안 §2 가 요구한 *"속도/회피 분리"* 의 절반이 여기서 이뤄진다.
        ///
        /// ⚠ 숙련 배율을 곱하지 않는다 — 확률축·비교축 공통 규칙이다.
        /// </summary>
        public int Speed
        {
            get
            {
                double bonus = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];
                    if (learned.Art.IsMorphemeDerived) bonus += learned.Art.Delta.Speed;
                }

                // ⚠ 창 숙달(백일창)이 여기 얹힌다 — *"먼저 찌른다"* 에 *"자주 찌른다"* 를 더한 것이다.
                //   선공에도 속도가 들어가므로 창은 두 축을 겸한다(2026-07-31 유형 재조정).
                // ⚠ 프로토타입 단순화는 `Initiative` 와 같다 — 창 숙달자는 다른 무기를 들어도 받는다.
                int spearSpeed = DisciplineCurve.SpeedBonus(Discipline.Spear, MasteryOf(Discipline.Spear));
                return Stats.Agility + (int)Math.Round(bonus) + spearSpeed;
            }
        }

        /// <summary>
        /// 선공 판정 수치. 높은 쪽이 먼저 친다.
        /// **속도** + 경공 무공 + **창 숙달**(백일창 — 먼저 찌른다).
        ///
        /// ⚠⚠ 2026-07-31 — **형태소 속도를 잇는 유일한 자리다.** 정의서 §1-1 이 속도를
        ///   *"행동 순서"* 로 규정했으므로 여기 말고 갈 곳이 없다.
        ///   그전까지 속도는 사전에 값만 있고 엔진이 안 읽어, 속도를 주는 글자가 전부 죽어 있었다 —
        ///   수식 속·신·급 **52%**(무의미) · 무공형태 쾌(속도+2/명중−2) **28.5%** ·
        ///   공격방식 투·척·포·사(속도+1.5) **29%**. 반대로 속도를 **파는** 글자(중·후 −2, 유·변 −2)는
        ///   페널티가 없는 셈이라 중·후가 **86.7% 로 지배적**이었다.
        ///
        /// ⚠ **숙련 배율을 곱하지 않는다.** 선공은 크기가 아니라 **비교**라서, 배율을 곱하면
        ///   수련한 쪽이 항상 먼저 치게 되어 형태소 선택이 묻힌다.
        ///
        /// ⚠ 프로토타입 단순화: 창 숙달이 있으면 다른 무기를 쓸 때도 선공 보너스가 붙는다.
        ///   선공은 전투 시작 전에 정해지는데 그 시점엔 어느 초식을 쓸지 아직 모르기 때문이다.
        ///   실제로는 한 사람이 무기 하나를 주력으로 쓰므로 당장은 문제가 안 된다.
        /// </summary>
        public int Initiative
        {
            get
            {
                double bonus = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];
                    if (learned.Art.Discipline == Discipline.Movement && !learned.Art.IsMorphemeDerived)
                    {
                        bonus += learned.Art.InitiativeBonus * learned.PowerMultiplier;
                    }
                }

                int spearBonus = DisciplineCurve.InitiativeBonus(Discipline.Spear, MasteryOf(Discipline.Spear));
                return Speed + (int)Math.Round(bonus) + spearBonus;
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
