using System;
using System.Collections.Generic;

namespace Jianghu.Core.Martial.Morphemes
{
    /// <summary>
    /// 형태소 사전 — **정의서 §3 을 코드로 옮기는 유일한 지점이다.**
    ///
    /// 다른 어디에도 형태소 수치를 적지 않는다. 무공 122개의 수치가 전부 이 파일에서 나오므로,
    /// 여기가 정의서와 어긋나면 게임 전체가 어긋난다. 수정할 때는 반드시 정의서를 먼저 고친다
    /// (정의서 머리말: *"이 문서가 무공 리소스의 유일한 진실의 원천이다"*).
    ///
    /// ── 배경어 규제 4조 (2026-07-29 사용자 요구) ────────────────────────────────
    /// 배경어는 수치도 비용도 0 인 중립 글자라, 규제가 없으면 "공짜로 이름을 길게 만드는 장치" 가 된다.
    ///
    ///   **R1  배경어는 화이트리스트다.** 등록된 글자만 배경어이고 미등록 글자는 예외다.
    ///   **R2  무공당 배경어 최대 1자** (파서 층에서 강제 · 설계안 §4 3단계)
    ///   **R3  배경어만으로 필수 카테고리를 대체할 수 없다** (필수 규칙이 이미 강제하나 테스트로 고정)
    ///   **R4  배경어 추가는 형태소 추가보다 엄격하게.** 추가할 때마다 *"이 글자는 정말 수치가
    ///         없어야 하는가"* 를 먼저 답하고 근거를 설계안에 남긴다
    ///
    /// ⚠⚠ **R1 하나가 나머지 셋을 합친 것보다 중요하다.** 배경어를 "사전에 없는 글자" 로 정의하면
    ///   파서가 모르는 글자를 전부 조용히 삼킨다. 그러면 ⓐ 오타가 검출되지 않고
    ///   ⓑ **사전에 추가했어야 할 형태소가 배경어로 묻히며** ⓒ 역산 리포트(설계안 §5-2)가
    ///   통째로 무의미해진다. 정의서가 75자를 공들여 고른 이유가 거기서 증발한다.
    ///   그래서 <see cref="TryGet"/> 은 미등록 글자에 대해 **false 를 돌려주고**, 파서가 예외를 던진다.
    /// ────────────────────────────────────────────────────────────────────────────
    /// </summary>
    public static class MorphemeDictionary
    {
        // ─────────────────────────── 형태소 75자 (정의서 §3) ───────────────────────────

        /// <summary>
        /// **범위 무공이 단일 대상 무공 대비 내는 직격 피해 배수** — 범위 대가의 **유일한 손잡이**다
        /// (2026-08-09 신설 · 정의서 §3-12).
        ///
        /// 각 글자의 위력 배수는 <c>이 값 ÷ 대상 수</c> 로 유도된다. 그래서 손잡이가 하나이고,
        /// *"넷을 때리되 직격 피해 합은 단일의 T배"* 라는 **한 문장으로 읽힌다.**
        ///
        /// ⚠⚠ **"총 피해" 가 아니라 "직격 피해" 다.** 지속 피해(출혈·중독·화상)는
        ///   <c>CombatResolver.TickStatuses</c> 에서 상수로 나가 이 배수를 **받지 않는다.**
        ///   → **상태이상을 문 범위 무공이 그만큼 유리하다.** 실측(T=1.30 · 4대4 10성):
        ///   출혈을 문 `환벌혈군` 53.1% vs 상태이상이 없는 `정천창군` 27.3%.
        ///   이름이 성능을 말해야 하므로(정의서 §0) **이 서술을 "총 피해" 로 쓰면 거짓말이 된다.**
        ///
        /// ⚠⚠ **1.00 이면 직격 피해 합이 정확히 같다.** 그런데 실측에서 그때 범위가 **−26%p 로 진다**
        ///   (4대4 10성 31.4 vs 57.6). 피해를 여러 명에게 나눠 넣으면 적의 행동 수가 늦게 줄기 때문이다 —
        ///   행동경제 손실이 과잉 피해 절감보다 크다. **파단점은 T ≈ 1.22 근처**다.
        /// ⚠ 이 값은 **아직 확정되지 않았다**(2026-08-09 시점). 스윕 결과와 남은 결정은 HANDOFF §4-12 참조.
        /// </summary>
        public const double ScopeDamageBudget = 1.15;

        /// <summary>
        /// <c>전全</c>·<c>만萬</c> 의 대상 수를 **몇으로 놓고 대가를 매길 것인가.**
        ///
        /// ⚠⚠ 전원 타격은 대상 수가 **그 전투의 인원에 따라 변한다.** 대가는 사전에 고정돼야 하므로
        ///   기준 편성을 하나 정해야 하고, 그 기준이 이 값이다 — **측정 편성(4인)** 을 쓴다(설계 §D1).
        /// ⚠ 그래서 **인원이 많은 전투일수록 전원기가 강해진다.** 이름 그대로의 성질이라 결함이 아니지만,
        ///   편성 규모가 4에서 달라지면 **여기서부터 다시 봐야 한다.**
        /// </summary>
        public const int ScopeAllTargets = 4;

        /// <summary>
        /// **만(萬) 전용 직격 피해 배수** — 전(全)과 성격을 가르는 자리다 (2026-08-09 사용자 지시로 스윕 중).
        ///
        /// ⚠⚠ **만이 위력 대가를 아예 안 내면 전(全)과 성능만 다르고 성격이 같아진다** — 실제로
        ///   그렇게 두었더니 `환창혈만` 이 4대4에서 **91.6~96.1%** 로 홀로 지배했다(T 와 무관.
        ///   기력 축이라 <see cref="ScopeDamageBudget"/> 이 닿지 않는다). HANDOFF §4-12.
        /// ⚠ 반대로 전(全)과 **같은 값**을 주면 위력 대가가 같은데 기력까지 더 내므로 만이 **완전 하위호환**이 된다.
        ///   → 그래서 이 값은 반드시 <see cref="ScopeDamageBudget"/> **보다 커야** 한다.
        ///   *"만은 위력을 덜 팔고 대신 기력을 판다"* — 이것이 두 글자를 가르는 한 문장이다.
        /// ⚠ <c>4.0</c> 이면 위력 대가가 **0** 이고 그게 스윕 이전의 옛 상태다(기력만 낸다).
        /// ⚠⚠ **미확정 스윕값이다.** 실전 테스트로 정한다(2026-08-09 사용자 확정).
        /// </summary>
        public const double ScopeDamageBudgetLegion = 2.5;

        /// <summary>
        /// 대상 <paramref name="targets"/> 명을 때리는 범위 형태소의 대가.
        /// 총 피해가 <see cref="ScopeDamageBudget"/> 배가 되도록 **위력을 곱셈으로** 깎는다.
        /// </summary>
        private static ArtStatDelta ScopeCost(int targets)
        {
            return ScopeCost(targets, ScopeDamageBudget, false);
        }

        /// <summary>
        /// <paramref name="budget"/> 배수로 대가를 유도한다.
        /// <paramref name="paysElsewhere"/> 는 **다른 축으로도 대가를 내는가** — 만(萬)의 기력 소모가 그것이다.
        /// </summary>
        private static ArtStatDelta ScopeCost(int targets, double budget, bool paysElsewhere)
        {
            double percent = (budget / targets - 1.0) * 100.0;

            // ⚠⚠ **대가가 양수면 범위가 순이득이 된다.** 배수가 대상 수를 넘으면(예: T 2.0 에 다多 2인)
            //   이 카테고리의 존재 이유가 사라진다 — 사전 §3-12 가 *"대가가 이 카테고리의 본체다"* 라고
            //   못박은 그 자리다. 조용히 넘기지 않고 여기서 깬다.
            if (percent > 0)
            {
                throw new InvalidOperationException(
                    "범위 대가가 순이득이다(대상 " + targets + "인 · +" + percent.ToString("F1")
                    + "%). 배수(" + budget + ")가 대상 수보다 크면 안 된다.");
            }

            // ⚠ 위력 대가가 0 이어도 **다른 축으로 내고 있으면** 정상이다(만萬의 기력 +200%).
            //   둘 다 0 이면 대가가 아예 없는 범위 글자가 되므로 그건 막는다.
            if (percent == 0 && !paysElsewhere)
            {
                throw new InvalidOperationException(
                    "범위 대가가 0 인데 다른 축으로도 내지 않는다(대상 " + targets + "인).");
            }

            return ArtStatDelta.Of(attackPercent: percent);
        }

        private static readonly Morpheme[] MorphemeList = BuildMorphemes();

        private static Morpheme[] BuildMorphemes()
        {
            var list = new List<Morpheme>();

            // 한 행에 여러 글자가 같은 수치를 갖는 정의서 표 구조를 그대로 옮긴다.
            // 표의 행 하나 = 아래 호출 하나가 되도록 맞춘 것이며, 옮겨 적는 실수를 줄이는 것이 목적이다.
            void Row(
                string korean, string hanja, string meaning, MorphemeCategory category, ArtStatDelta delta,
                ArtLineage lineage = ArtLineage.None, bool isNegation = false, bool doublesFormEffect = false,
                AttackScope scope = AttackScope.Single, AbsoluteRule rule = AbsoluteRule.None)
            {
                if (korean.Length != hanja.Length)
                {
                    throw new InvalidOperationException(
                        "형태소 행 '" + meaning + "' 의 한글(" + korean.Length + "자)과 한자(" + hanja.Length + "자) 개수가 다르다.");
                }

                for (int i = 0; i < korean.Length; i++)
                {
                    list.Add(new Morpheme(
                        korean[i], hanja[i], meaning, category, delta, lineage, isNegation, doublesFormEffect, scope, rule));
                }
            }

            // ── §3-1 공격 방식 (14자) · 택 1 · **공격 무공 필수** ──
            Row("벌참절단", "伐斬截斷", "베기", MorphemeCategory.AttackMethod, ArtStatDelta.Of(attack: 2));
            // ⚠⚠ 2026-08-02 — 속도를 0.5/1.0/1.5 → **1.0/2.0/3.0** 으로 올렸다(사용자 확정 · HANDOFF §4-2-W).
            //   사다리가 *공격 −0.5 ↔ 속도 +0.5* 로 **1:1 교환**하고 있었는데, 두 축의 실제 가치가
            //   그렇지 않아 한 칸 내려갈 때마다 순손실이 났다. 규칙을 **"공격 0.5 를 팔면 속도 1.0 을 산다"**
            //   로 바꿨다 — 실측에서 이 값이 공격방식 축을 가장 평평하게 만든다(스프레드 19.2 → 8.3%p).
            //   ⚠ 더 올리면(2.0/4.0/6.0) 축이 반대로 21.7%p 벌어지고 던지기가 베기를 넘어 이름이 거짓말한다.
            //   ⚠ 이 교정이 계층 내 격차까지 줄이는 것은 **공격방식과 유형이 69/71 로 1:1 결합**돼 있어서다.
            Row("자창", "刺槍", "찌르기", MorphemeCategory.AttackMethod, ArtStatDelta.Of(attack: 1.5, speed: 1));
            Row("구타격박", "毆打擊拍", "때리기", MorphemeCategory.AttackMethod, ArtStatDelta.Of(attack: 1, speed: 2));
            Row("투척포사", "投擲拋射", "던지기", MorphemeCategory.AttackMethod, ArtStatDelta.Of(attack: 0.5, speed: 3));

            // ── §3-2 방어 (11자) · 택 1 · **경공 무공 필수** ──
            Row("방거항어호", "防拒抗禦護", "막기", MorphemeCategory.Defense, ArtStatDelta.Of(defense: 1.2, blockChance: 8));
            Row("피둔섬", "避遁閃", "회피", MorphemeCategory.Defense, ArtStatDelta.Of(evasion: 15));
            Row("반역응", "反逆應", "반격", MorphemeCategory.Defense, ArtStatDelta.Of(defense: 1, counterRate: 10));
            // ⚠ 종교 형태소 (2026-07-30). 소림사(불교)를 다른 문파와 구별하는 글자다.
            //   **기존 축의 복제가 아니라 새 자리여야 한다** — 방어군 5자는 전부 페널티가 없는데
            //   계(戒)만 페널티를 갖는다. 막기에 극단적으로 몰되 속도를 판다.
            //   근거: 지계(持戒)는 육바라밀의 하나로 "지켜서 막는" 개념이다. ⚠ 무협 사용례는 미검증.
            Row("계", "戒", "지계", MorphemeCategory.Defense, ArtStatDelta.Of(blockChance: 25, speed: -1));

            // ── §3-3 내공 (3자) · 택 1 · **내공 무공 필수** ──
            Row("양", "陽", "양기", MorphemeCategory.Internal, ArtStatDelta.Of(maxQi: 10));
            Row("음", "陰", "음기", MorphemeCategory.Internal, ArtStatDelta.Of(qiRegen: 2));
            Row("합", "合", "합일", MorphemeCategory.Internal, ArtStatDelta.Of(maxQi: 5, qiRegen: 0.5));
            // ⚠ 종교 형태소 (2026-07-30). 도교의 조식(調息)이자 불교의 호흡 수련이라 소림·무당 공통이다.
            //   **내공 3자는 전부 기력의 양(量)을 다루는데 식(息)만 소모율을 다룬다** — 새 자리다.
            //   극한경지 선(仙, −30%)의 일반형 하위 단계이기도 하다.
            Row("식", "息", "조식", MorphemeCategory.Internal, ArtStatDelta.Of(qiCostPercent: -10, maxQi: 3));

            // ── §3-4 상태이상 (7자) ──
            Row("독", "毒", "중독", MorphemeCategory.Status, ArtStatDelta.Of(poisonChance: 10));
            Row("혈", "血", "출혈", MorphemeCategory.Status, ArtStatDelta.Of(bleedChance: 10));
            Row("비", "痺", "마비", MorphemeCategory.Status, ArtStatDelta.Of(paralysisStack: 1));
            Row("염", "炎", "화상", MorphemeCategory.Status, ArtStatDelta.Of(burnChance: 10));
            Row("빙", "氷", "동상", MorphemeCategory.Status, ArtStatDelta.Of(frostbiteChance: 10));
            // ⚠⚠ 아래 2자는 2026-07-29 추가분이다(정의서 원안 5자 → 7자).
            //   추가 근거: 엔진 `StatusEffectKind` 는 5종인데 형태소는 그중 3종만 가리킬 수 있었다.
            //   그래서 **정파에 배정된 기력소실과 마도의 경직을 무공명으로 표현할 수 없었고**,
            //   정파 7문파가 상태이상 형태소를 하나도 쓰지 못하는 상태였다.
            //   무공명을 실제로 지어보고서야 드러난 구멍이다(`docs/martial-art-naming.md` §4).
            //   ✅ 한글 '탈'·'경' 은 기존 75자에 없어 충돌이 없다.
            Row("탈", "奪", "기력소실", MorphemeCategory.Status, ArtStatDelta.Of(qiDrainChance: 10));
            Row("경", "硬", "경직", MorphemeCategory.Status, ArtStatDelta.Of(staggerChance: 10));

            // ── §3-5 무공형태 (9자) · 택 1 · **공격 무공 필수** ──
            // ⚠ 5종 전부 페널티가 있고, 그게 의도다(정의서 §2-2). 페널티 없는 상위호환이 다른
            //   카테고리에 있으므로 선택제로 두면 아무도 고르지 않는다.
            Row("정직", "正直", "정직", MorphemeCategory.Form, ArtStatDelta.Of(attack: 0.75, accuracy: -2));
            Row("중후", "重厚", "무거움", MorphemeCategory.Form, ArtStatDelta.Of(attack: 1, speed: -2));
            Row("쾌", "快", "빠름", MorphemeCategory.Form, ArtStatDelta.Of(speed: 2, accuracy: -2));
            Row("환궤", "幻詭", "기만", MorphemeCategory.Form, ArtStatDelta.Of(accuracy: 2, attack: -0.75));
            Row("유변", "柔變", "변화", MorphemeCategory.Form, ArtStatDelta.Of(accuracy: 2, speed: -2));

            // ── §3-6 수식 (11자) · 택 1 ──
            // 밝다/어둡다의 대비가 정의서 §1-2 용어 정리의 산물이다 — 밝다 = 자주 터진다(치명률),
            // 어둡다 = 크게 터진다(치명배율).
            Row("속신급", "速迅急", "빠르다", MorphemeCategory.Modifier, ArtStatDelta.Of(speed: 1));
            Row("적확", "的確", "맞히다", MorphemeCategory.Modifier, ArtStatDelta.Of(accuracy: 1.4));
            Row("명광휘", "明光輝", "밝다", MorphemeCategory.Modifier, ArtStatDelta.Of(critChance: 10));
            Row("야암한", "夜暗寒", "어둡다", MorphemeCategory.Modifier, ArtStatDelta.Of(critMultiplier: 0.3));
            // ⚠ 종교 형태소 (2026-07-30). 무당파(도교)를 구별하는 글자다. 노자 「玄之又玄」.
            //   **수식 11자는 전부 페널티가 없는데 현(玄)만 페널티를 갖는다.** 그리고 수식 카테고리에
            //   회피 축이 들어오는 것도 처음이다 — 두 겹으로 새 자리다.
            //   "종잡을 수 없으나 나 또한 상대를 놓친다."  ⚠ 무협 사용례는 미검증.
            Row("현", "玄", "현묘", MorphemeCategory.Modifier, ArtStatDelta.Of(evasion: 10, accuracy: -1));

            // ── §3-7 자연속성 (5자) · 택 1 ──
            // ⚠ 원안은 "꾸밈말이라 의미 없음" 이었으나 +1 수준의 수치를 준다. 이 시스템의 핵심 가치가
            //   "이름에서 성능을 읽는다" 인데 의미 없는 글자가 섞이면 그 원칙이 깨지기 때문이다.
            //   화(火)는 약한 꾸밈, 염(炎)은 실제 화상 — 한자 의미로도 짝이 맞는다.
            Row("풍", "風", "바람", MorphemeCategory.Element, ArtStatDelta.Of(speed: 1));
            Row("뇌", "雷", "벼락", MorphemeCategory.Element, ArtStatDelta.Of(critChance: 5));
            Row("수", "水", "물", MorphemeCategory.Element, ArtStatDelta.Of(qiRegen: 1));
            Row("화", "火", "불", MorphemeCategory.Element, ArtStatDelta.Of(attack: 0.6));
            Row("냉", "冷", "차가움", MorphemeCategory.Element, ArtStatDelta.Of(accuracy: 1));

            // ── §3-8 극한경지 (9자) · **전승무학 전용 · 무공당 1자** (정의서 §5-2) ──
            // ⚠⚠ 이 9자만 페널티가 없다. 제약이 없으면 무조건 이득이 되므로 계층·개수 제한이
            //   페널티를 대신한다. 제약은 파서 층에서 강제한다(설계안 §4 3단계).
            // ⚠⚠ 2026-08-02 재환산 (사용자 확정 · HANDOFF §4-2-X). **성격은 그대로 두고 총가치만 맞췄다.**
            //   그전에는 고정 대조군(`X풍쾌참` vs `풍쾌참`) 실측이 **존 98.0 ~ 선 46.5, 스프레드 51.5%p**
            //   였다 — 게임에서 가장 큰 단일 축 격차였고, §4-2-a(무공형태 80%p)·공격방식(19.2%p)에 이은
            //   **같은 병의 세 번째 재발**이다: 축의 단위가 다른데 숫자만 비슷하게 맞춰 둔 것.
            //   재환산 후 **8자가 81.0~88.0 (7.0%p)** 안에 든다.
            //   ⚠ 황(皇)은 건드리지 않았다 — 낮췄더니 전승 유일 비도인 `황야환투` 가 더 나빠졌다.
            //     그 무공은 공격 합이 −0.25(하한 적용 시 0)라 **황의 명중·치명이 위력의 전부**다.
            Row("존", "尊", "지존", MorphemeCategory.Pinnacle, ArtStatDelta.Of(speed: 1, attack: 1.5, accuracy: 1));
            Row("제", "帝", "황제", MorphemeCategory.Pinnacle, ArtStatDelta.Of(attack: 1, defense: 1, accuracy: 1, speed: 1));
            Row("마", "魔", "천마", MorphemeCategory.Pinnacle, ArtStatDelta.Of(defenseIgnore: 25, attack: 2.5));
            Row("패", "霸", "패도", MorphemeCategory.Pinnacle, ArtStatDelta.Of(attack: 2.3, critMultiplier: 0.3, critChance: 5));
            Row("성", "聖", "검성", MorphemeCategory.Pinnacle, ArtStatDelta.Of(defense: 5, blockChance: 25, statusResist: 30));

            // ⚠⚠ 선(仙)은 **손대지 않는다.** 값이 전부 기력 축인데 평타 전락률이 0.0% 라
            //   평상시에는 무엇을 넣어도 효과가 0 이다 — 실측으로 최대기력 30·회복 5·소모 −45 까지
            //   키워도 **+1.0%p 뿐**이었다. 죽은 것이 아니라 **조건부**다: 탈(奪) 보유 상대에게는
            //   46.5 → 64.0(+17.5)으로 오르고, 재환산 뒤 그 대전의 스프레드는 35.0 → **15.5%p** 로 좁아진다.
            //   → **기력 축이 살아나기 전에는 이 글자를 고칠 수 없다**(§4-2-O 제로섬).
            Row("선", "仙", "검선", MorphemeCategory.Pinnacle, ArtStatDelta.Of(maxQi: 20, qiRegen: 3, qiCostPercent: -30));

            Row("왕", "王", "검왕", MorphemeCategory.Pinnacle, ArtStatDelta.Of(attack: 1.75, defense: 2, statusApplyBonus: 15));
            Row("황", "皇", "검황", MorphemeCategory.Pinnacle, ArtStatDelta.Of(accuracy: 3, critChance: 15));
            // 종(宗)만 수치가 아니라 규칙을 만진다 — 무공형태 효과를 페널티까지 함께 2배로 키운다.
            // ⚠ 종(宗)의 공격을 1 → 3 으로 올렸다(2026-08-02). `verify` 가 *"극단이라는 성격이 흐려지지
            //   않는가"* 를 물었고 실측으로 닫았다 — 무공형태를 바꿔 가며 재면 종의 승률 격차가
            //   **공격 1 에서 28.0%p · 공격 3 에서 25.5%p** 로 거의 그대로다. 종의 값은 여전히
            //   *어떤 무공형태와 묶느냐*가 정한다.
            // ⚠⚠ 2026-08-05 — 공격 **3 → 1** (사용자 확정 · 정의서 §3-8-a).
            //   `종환화격`(종이 쓰인 **유일한** 무공)이 10성 **83.4%** 로 지배했다.
            //   극한경지 9자를 나란히 놓으면 종만 이상치였다 — **공격값이 최고(3.0)인데
            //   부가 효과(무공형태 2배)까지** 갖는다. 이제 제(帝)와 같은 자리(공격 1.0)다.
            //   ✅ "극단" 정체성은 안 흔들린다 — 무공형태별 격차가 **공격 1 에서 28.0%p ·
            //     공격 3 에서 25.5%p** 로 거의 같다(2026-08-02 실측). 정체성은 수치가 아니라 2배 메커니즘이다.
            //   실측: 종환화격 83.4 → **56.9** · 전승 격차(범위제외) 49.7 → **29.7**(목표 이내).
            //   ⛔ 알고 받아들인 부작용 — 마한중참 63.1 → 67.1 · 패혈중창 60.9 → 66.0 이
            //     새로 지배 임계(65)를 넘었다. 깎인 승률이 상위권으로 재분배된 것이다.
            //
            // ⏸⚠⚠ 2026-08-05 2차 — **종은 나머지 8자와 단위가 다르다. 고치지 않기로 했다**
            //   (사용자 확정 · 정의서 §3-8-b 에 전문). ⛔ **여기 값을 만지려는 사람은 그 절을 먼저 읽어라.**
            //   ⓐ 극한경지 측정 블록을 처음 만들어 재 보니 **7자가 +23.8~27.9%p 로 이미 평평**하고,
            //     벗어난 것은 조건부인 선(仙)과 이 종(宗)뿐이다.
            //   ⓑ 그래서 **극한경지 축은 균일 배율로 조정할 수 없다** — 7자만 내리면 종이 그 자리를
            //     채운다. 실측 기각 2건: ×0.8 → 전승 최고 65.1(여전히 지배) · 격차 31.0(목표 초과),
            //     ×0.6 → 전승 최고 **68.7 = 종환화격**(어제 고친 것이 되살아남) · 격차 34.4 ·
            //     `dotnet test` **198/1 실패**(마의 방어무시가 유명무실해짐).
            //     **현행이 격차 기준으로 이미 최적이다.**
            //   ⓒ ⚠⚠ 아래 `doublesFormEffect` 는 **환·궤에 걸린 레버리지**다. `MorphemeParser.cs:169`
            //     가 무공형태 델타를 통째로 2배 하고 `ArtStatDelta` 의 `AttackPenalty` 축까지 함께
            //     곱하므로, §1-3(공격 페널티를 배율 밖으로)의 이득을 **종환화격만 2배로 받았다.**
            //     → **환·궤를 손대면 종을 반드시 다시 잰다.** 지표에는 안 뜬다.
            Row("종", "宗", "종주", MorphemeCategory.Pinnacle, ArtStatDelta.Of(attack: 1), doublesFormEffect: true);

            // ── §3-9 부정 (5자) · 자체 수치 없음 ──
            Row("낙망멸산소", "落亡滅散消", "부정", MorphemeCategory.Negation, ArtStatDelta.Zero, isNegation: true);

            // ── §3-12 범위 (4자) · 택 1 · **공격 무공 전용 · 대문파 이상 전용** (2026-07-30 신설) ──
            // ⚠⚠ 대가가 이 카테고리의 본체다. 대가가 없으면 다대다 전투가 생기는 순간
            //   모든 무공이 광역이 되어 "광역이냐 단일이냐" 라는 선택이 사라진다.
            //   대가를 **두 축으로 갈랐다** — 전(全)은 위력을 팔고, 만(萬)은 기력을 판다.
            //   그래서 전원 타격 두 글자가 성능이 아니라 **성격**으로 구분된다.
            // ⚠ 최대 공격 합은 공격방식2 + 무공형태2 + 자연1 = 5 다. 전(全)의 −3 이면 2 가 남는다.
            //   즉 전원을 때리되 위력은 40% 수준이 된다.  ⚠ 전부 미검증 초기값이다.
            // ⚠⚠ **2026-08-09 — 감산에서 배수로 바꿨다.** 옛 값(다 −1 · 군 −2 · 전 −3 공격 감산)은
            //   첫 다대다 측정에서 **대가 구실을 못 했다**(HANDOFF §4-12): 4대4 범위 5종 평균 85.6% ·
            //   대조 35.2%. 공격 합 −2.75 로 카탈로그 최저인 `궤격비전` 이 99.5% 였다.
            //   이유는 셋이고 전부 구조다 — ⓐ 캐릭터 공격 8 이 형태소 밖이라 감산이 못 건드리고
            //   ⓑ `ArtPower` 가 음수를 0 으로 잘라 일정 지점 아래에서는 감산이 무료이며
            //   ⓒ **이득은 대상 수 곱셈인데 대가는 뺄셈**이라 급이 다르다.
            //   → 대가를 **곱셈 축**(`AttackPercent`)으로 옮긴다. 손잡이는 아래 하나뿐이다.
            Row("다", "多", "여럿", MorphemeCategory.Scope, ScopeCost(2), scope: AttackScope.Two);
            Row("군", "群", "무리", MorphemeCategory.Scope, ScopeCost(3), scope: AttackScope.Three);
            Row("전", "全", "전부", MorphemeCategory.Scope, ScopeCost(ScopeAllTargets), scope: AttackScope.All);
            // 만(萬) — **위력을 덜 팔고 기력을 판다**(만인적萬人敵). 기력 소모가 3배다.
            //   ⚠⚠ 2026-08-01 정정 — 상수 4 시절 "16 → 48" 로 적혀 있었으나 상수가 3 이 되어
            //   4자 무공 기준 **12 → 36** 이다(실측: 환창혈만 36 · 만우쾌사 27).
            //   기력 50 이라 여전히 한 번 쓰고 고갈되는 필살기 성격은 유지된다.
            //   ⚠⚠ **2026-08-09 — 위력 대가를 여기에도 붙였다**(사용자 지시로 스윕 중).
            //     그전에는 위력을 **하나도** 안 팔아서, 전(全)이 배수형 대가를 지게 된 뒤
            //     `환창혈만` 이 4대4에서 **91.6~96.1%** 로 홀로 지배했다(HANDOFF §4-12).
            //     ⚠ 배수는 <see cref="ScopeDamageBudgetLegion"/> 이고 **전(全)보다 커야** 한다 —
            //       같으면 만이 완전 하위호환(위력 같은데 기력만 더)이 되어 죽은 글자가 된다.
            Row("만", "萬", "만인", MorphemeCategory.Scope,
                ScopeCost(ScopeAllTargets, ScopeDamageBudgetLegion, true) + ArtStatDelta.Of(qiCostPercent: 200),
                scope: AttackScope.All);

            // ── §3-10 무학분류 (3자) · 태그일 뿐 자체 수치 없음 ──
            Row("일", "日", "양기무학", MorphemeCategory.Tag, ArtStatDelta.Zero, ArtLineage.Yang);
            Row("월", "月", "음기무학", MorphemeCategory.Tag, ArtStatDelta.Zero, ArtLineage.Yin);
            Row("혼", "混", "혼합무학", MorphemeCategory.Tag, ArtStatDelta.Zero, ArtLineage.Mixed);

            // ── §5-3 절대경지 규칙 (4자) · **절대경지 전용 · 필수 1자** (2026-08-02 신설) ──
            //
            // ⚠ 수치가 없다(`Zero`). 정의서 §5-3 이 *"수치로 강하게 만들지 않는다 — 대신 규칙을
            //   바꾼다"* 고 못박았기 때문이다. 전승무학을 수치로 압도하면 문파 성장 경로가 통째로
            //   무의미해진다는 것이 그 근거다.
            //
            // ⚠⚠ **한글 키 충돌을 사전 84자 + 배경어 6자 전수로 확인했다** (2026-08-02).
            //   면·쌍·무·통은 어디에도 없다. 중/重·광/光·성/聖·공/功·산/散 이 한글을 선점해
            //   한자를 못 썼던 전례가 있으므로 새 글자는 반드시 이 확인을 거친다(HANDOFF §0).
            //
            // 대형세력 배정은 정의서 §5-5-c 표에 있다.
            Row("면", "免", "면역", MorphemeCategory.AbsoluteRule, ArtStatDelta.Zero,
                rule: AbsoluteRule.StatusImmunity);
            Row("무", "無", "무소모", MorphemeCategory.AbsoluteRule, ArtStatDelta.Zero,
                rule: AbsoluteRule.NoQiCost);
            Row("쌍", "雙", "이회행동", MorphemeCategory.AbsoluteRule, ArtStatDelta.Zero,
                rule: AbsoluteRule.DoubleAction);
            Row("통", "統", "상성통괄", MorphemeCategory.AbsoluteRule, ArtStatDelta.Zero,
                rule: AbsoluteRule.CounterSupremacy);

            return list.ToArray();
        }

        // ─────────────────────────── 배경어 화이트리스트 (R1) ───────────────────────────

        /// <summary>
        /// 배경어 사전. **1자로 시작한다 — 최소로 시작하는 것이 규제의 본체다** (R4).
        ///
        /// ⚠⚠ **정의서 §3-7 의 선언("의미 없는 글자가 섞이면 이름에서 성능을 읽는 원칙이 깨진다")과
        ///   충돌한다.** 2026-07-29 결정 A 로 **배경어에 한해 그 문장을 무효화**했다. 정의서에 역반영이 필요하다.
        ///
        /// ⚠⚠ **중(中)은 배경어가 될 수 없다 (2026-07-29 구현 중 확인).**
        ///   설계안은 초기 배경어를 천·중 2자로 잡았으나, 한글 '중' 은 이미 무공형태 **중(重, 무거움)** 이
        ///   차지하고 있다(§3-5). 사전의 키는 한자가 아니라 **한글**이므로(<see cref="Morpheme.Korean"/>),
        ///   중을 배경어로 등록하면 중(重)을 가리거나 사전이 중복 키로 터진다.
        ///   정의서 §3-6 이 *"중 | 맞히다 中 | 무공형태 重 | 맞히다에서 제외"* 로 이미 정리한 충돌이,
        ///   배경어로 되살리려는 순간 그대로 되돌아온 것이다.
        ///   → **R4 의 질문("이 글자는 정말 수치가 없어야 하는가")이 스스로 답한다** — 중은 수치가 있다.
        ///   → 결과: `암중독환` 은 파서에서 중을 重(무공형태)으로 읽으므로 환(幻)과 함께 **무공형태 2자**가
        ///     되어 §2-2 규칙 2 위반이다. 이 예시는 어감 설명용이며 게임에 들어갈 무공이 아니다(설계안 §1-A).
        /// </summary>
        private static readonly Morpheme[] BackgroundList =
        {
            // 천(天) — 정의서가 직접 인정한 유일한 배경어다. §3-1 이 찌르기 후보 穿 을 제외하며
            //          "사용례 창천낙월의 천(天)과 한글이 겹친다" 고 적었다. 즉 정의서는 天 이
            //          형태소가 아닌 글자임을 알면서 무공명에 들어간다고 전제했다.
            new Morpheme('천', '天', "하늘", MorphemeCategory.Background, ArtStatDelta.Zero),

            // ── 2026-07-30 추가 4자 (`game-research` → `verify` 통과) ──
            // 추가 사유: 배경어가 천(天) 하나뿐이라 **문장형 상성 무공이 전부 `○천○○` 이 됐다.**
            //   목적어가 될 글자가 하나뿐이었기 때문이다(`docs/martial-art-naming.md` §4-B).
            // ⚠ 조사가 낸 후보 12자 중 4자만 넣는다 — 12자를 한 번에 넣으면 R4("최소로 시작하는 것이
            //   규제의 본체다")를 형해화한다. 122개를 지으며 부족하면 R4 절차로 추가한다.
            new Morpheme('지', '地', "땅", MorphemeCategory.Background, ArtStatDelta.Zero),
            new Morpheme('해', '海', "바다", MorphemeCategory.Background, ArtStatDelta.Zero),
            new Morpheme('운', '雲', "구름", MorphemeCategory.Background, ArtStatDelta.Zero),
            new Morpheme('몽', '夢', "꿈", MorphemeCategory.Background, ArtStatDelta.Zero),

            // 우(雨) — 2026-07-30 추가. R4 절차("이 글자는 정말 수치가 없어야 하는가")를 밟았다:
            //   비는 순수 자연물 명사이고 붙일 성능축이 없다. 물(水)은 이미 기력회복으로 있고,
            //   "쏟아짐" 은 타격 횟수(문파 가중치)나 범위(§3-12)가 이미 담당한다.
            //   ⚠ 추가 사유는 사천당가 전승무학 `만우쾌사` 하나다 — 만천화우(滿天花雨)의 그림을
            //     예외 없이 재현하려면 '비' 가 필요했다. 배경어 5자 → 6자.
            new Morpheme('우', '雨', "비", MorphemeCategory.Background, ArtStatDelta.Zero),

            // ⚠⚠ 채택하지 않은 것과 그 이유 (다시 묻지 않기 위해 남긴다):
            //   봉(峰)·무(霧)·령(靈) 보류 · 강(江)·곡(谷)·세(世)·계(界)·진(塵) 탈락 · 파(波)·극(極)·옥(獄) 탈락
            //   성(星)·공(空)·산(山)은 **애초에 불가능** — 성(聖)·공(功)·산(散)이 한글을 선점했다
            //   상세 판정은 `docs/martial-art-naming.md` §4-B
        };

        // ─────────────────────────── 조회 ───────────────────────────

        private static readonly Dictionary<char, Morpheme> Lookup = BuildLookup();

        private static Dictionary<char, Morpheme> BuildLookup()
        {
            var map = new Dictionary<char, Morpheme>();

            // ⚠ Add 는 키가 겹치면 예외를 던진다. **그게 여기서 원하는 동작이다** —
            //   한글 표기가 겹치는 두 글자가 사전에 들어오면 조용히 덮이는 대신 즉시 터진다.
            //   정의서 §3-6 이 신·중·명·일 네 건의 충돌을 정리한 이유가 이것이고,
            //   배경어 중(中)이 걸러진 것도 이 규칙 덕분이다.
            for (int i = 0; i < MorphemeList.Length; i++) map.Add(MorphemeList[i].Korean, MorphemeList[i]);
            for (int i = 0; i < BackgroundList.Length; i++) map.Add(BackgroundList[i].Korean, BackgroundList[i]);

            return map;
        }

        /// <summary>
        /// 형태소 개수. **81** 이어야 한다 — 정의서 §3 원안 75자
        /// + 상태이상 보강 2자(탈奪·경硬, 2026-07-29) + 범위 4자(다多·군群·전全·만萬, 2026-07-30).
        /// 배경어는 포함하지 않는다.
        /// </summary>
        public static int Count => MorphemeList.Length;

        /// <summary>배경어 개수. R4 에 따라 최소로 유지한다.</summary>
        public static int BackgroundCount => BackgroundList.Length;

        /// <summary>형태소 75자 전체. 배경어는 들어 있지 않다.</summary>
        public static IReadOnlyList<Morpheme> All => MorphemeList;

        /// <summary>배경어 전체.</summary>
        public static IReadOnlyList<Morpheme> BackgroundWords => BackgroundList;

        /// <summary>
        /// 한 글자를 형태소로 바꾼다. 배경어도 여기서 찾힌다.
        ///
        /// ⚠⚠ **미등록 글자는 false 다 (R1).** 배경어로 삼키지 않는다. 이 한 줄이 규제의 전부다.
        /// </summary>
        public static bool TryGet(char korean, out Morpheme morpheme)
        {
            return Lookup.TryGetValue(korean, out morpheme);
        }

        /// <summary>한 글자를 형태소로 바꾼다. 미등록이면 예외.</summary>
        public static Morpheme Get(char korean)
        {
            Morpheme found;
            if (!Lookup.TryGetValue(korean, out found))
            {
                throw new ArgumentException(
                    "'" + korean + "' 은(는) 형태소 사전에도 배경어 사전에도 없다. "
                    + "형태소로 추가할 글자인지 먼저 검토할 것 (배경어 추가는 언제나 차선책이다 · R4).",
                    nameof(korean));
            }
            return found;
        }

        /// <summary>사전에 있는 글자인가. 형태소와 배경어를 함께 본다.</summary>
        public static bool Contains(char korean)
        {
            return Lookup.ContainsKey(korean);
        }

        /// <summary>카테고리에 속한 형태소들. 조합 규칙 검사와 민감도 측정(설계안 §5-4)이 쓴다.</summary>
        public static IReadOnlyList<Morpheme> ByCategory(MorphemeCategory category)
        {
            var found = new List<Morpheme>();
            for (int i = 0; i < MorphemeList.Length; i++)
            {
                if (MorphemeList[i].Category == category) found.Add(MorphemeList[i]);
            }
            for (int i = 0; i < BackgroundList.Length; i++)
            {
                if (BackgroundList[i].Category == category) found.Add(BackgroundList[i]);
            }
            return found;
        }
    }
}
