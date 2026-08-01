using System;
using System.Collections.Generic;
using Jianghu.Core.Characters;
using Jianghu.Core.Martial;

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

        public Combatant(
            string name, CharacterStats stats, IReadOnlyList<LearnedArt> arts,
            IReadOnlyList<DisciplineMastery> masteries = null)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("이름은 비어 있을 수 없다.", nameof(name));
            if (stats == null) throw new ArgumentNullException(nameof(stats));
            if (arts == null) throw new ArgumentNullException(nameof(arts));

            Name = name;
            Stats = stats;
            Arts = arts;
            Masteries = masteries ?? NoMastery;
        }

        /// <summary>해당 무기 유형의 숙련도. 익힌 적이 없으면 0.</summary>
        public int MasteryOf(Discipline discipline)
        {
            for (int i = 0; i < Masteries.Count; i++)
            {
                if (Masteries[i].Discipline == discipline) return Masteries[i].Proficiency;
            }
            return 0;
        }

        /// <summary>내공 무공이 더해진 실제 최대 기력. 보조 효과에는 숙련 배율이 곱해진다.</summary>
        public int EffectiveMaxQi
        {
            get
            {
                double bonus = 0;
                for (int i = 0; i < Arts.Count; i++)
                {
                    LearnedArt learned = Arts[i];
                    if (learned.Art.Discipline != Discipline.InnerArt) continue;

                    // ⚠ 형태소 무공은 `Delta.MaxQi`(양 +10 · 합 +5 · 식 +3 · 선 +20)에서,
                    //   레거시 무공은 손으로 박은 `MaxQiBonus` 에서 읽는다. 과도기 분기다.
                    double raw = learned.Art.IsMorphemeDerived ? learned.Art.Delta.MaxQi : learned.Art.MaxQiBonus;
                    bonus += raw * learned.PowerMultiplier;
                }
                return Stats.MaxQi + (int)Math.Round(bonus);
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
                return Stats.Agility / 2 + (int)Math.Round(bonus);
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
