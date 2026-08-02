using System;

namespace Jianghu.Core.Characters
{
    /// <summary>
    /// 캐릭터의 기본 능력치. 무공이 아니라 몸 자체의 값이다.
    ///
    /// ⚠ 구상안의 레벨 100 · 가중치 배분은 프로토타입에서 뺐다.
    ///   검증할 가설이 "무공 조합이 전투 결과를 바꾸는가" 이므로,
    ///   레벨 성장이 섞이면 무엇 때문에 이겼는지 분리할 수 없게 된다.
    /// </summary>
    public sealed class CharacterStats
    {
        /// <summary>체력 — 0 이 되면 패배한다.</summary>
        public int MaxHealth { get; }

        /// <summary>기력 — 초식을 쓸 때 소모한다. 내공 무공이 최대치를 올린다.</summary>
        public int MaxQi { get; }

        /// <summary>공격력 — 모든 초식 피해에 더해지는 기본값.</summary>
        public int Attack { get; }

        /// <summary>방어력 — 매 타격에서 피해를 깎는다.</summary>
        public int Defense { get; }

        /// <summary>신법 — 회피와 선공의 기반. 경공 무공이 이를 보강한다.</summary>
        public int Agility { get; }

        public CharacterStats(int maxHealth, int maxQi, int attack, int defense, int agility)
        {
            if (maxHealth <= 0) throw new ArgumentOutOfRangeException(nameof(maxHealth), "체력은 1 이상이어야 한다.");
            if (maxQi < 0) throw new ArgumentOutOfRangeException(nameof(maxQi));
            if (attack < 0) throw new ArgumentOutOfRangeException(nameof(attack));
            if (defense < 0) throw new ArgumentOutOfRangeException(nameof(defense));
            if (agility < 0) throw new ArgumentOutOfRangeException(nameof(agility));

            MaxHealth = maxHealth;
            MaxQi = maxQi;
            Attack = attack;
            Defense = defense;
            Agility = agility;
        }

        /// <summary>
        /// **만렙 캐릭터** — 정의서 §1-1 의 성장 종착점 (2026-07-31 사용자 확정).
        ///
        /// ⚠⚠ **밸런싱은 이 값을 기준으로 한다.** 나중에 캐릭터 능력치가 성장 요소가 되므로,
        ///   지금 맞춰 두는 수치가 **성장의 끝**이어야 나중에 다시 맞출 일이 없다.
        ///
        /// ⚠ 체력이 100 → 500 인 것은 세지려는 게 아니라 **눈금을 잘게 만들려는 것**이다
        ///   (<see cref="Combat.CombatResolver.DamageScale"/>). 타격이 7~8 이던 시절에는
        ///   `Math.Round` 가 12% 미만의 차이를 통째로 지워서, 방어 계수를 낮춰도 승률이
        ///   49.8% 에서 66% 로 건너뛰기만 했다.
        ///
        /// ⚠⚠ **공격·방어 3 의 근거 — 무공 기여의 약 25%.** 만렙 무공의 공격 기여는
        ///   `형태소합 5 × 성향배율 2.24 ≈ 11.2` 다. 정의서 §1-1 이 *"무공이 캐릭터 능력을
        ///   압도한다 — 이건 의도다"* 라고 못박았으므로 캐릭터 몫은 그보다 확실히 작아야 한다.
        ///   ⚠ 반대로 0 에 가까우면 캐릭터 성장이 아무 의미가 없고, **형태소 하나하나의 진폭이
        ///   그대로 승률이 되어** 지배적 형태소가 잘 안 잡힌다 — 캐릭터 스탯은 양쪽 모두가
        ///   갖는 공통 바닥이라 형태소의 상대 비중을 낮추는 역할도 한다.
        /// </summary>
        /// <remarks>
        /// ⚠⚠ **기력만 성장하지 않는다 (시작도 만렙도 50).** 처음엔 80 으로 잡았다가 되돌렸다 —
        ///   회복이 턴당 10 이고 3자 무공 소모가 12 였던 시절(상수 4), 최대 기력이 80 이면
        ///   **11턴 전투 동안 기력이 마르지 않는다.** 그러면 두 가지가 한꺼번에 죽는다:
        ///   ⓐ 기력 고갈 → 평타 전락이라는 전투의 드라마(HANDOFF §5, 목표 전락률 10~30%)
        ///   ⓑ 기력소실 형태소 탈(奪) — 실제로 승률 46.5% 로 **넣으면 손해**가 됐다
        ///   그리고 애초에 **최대기력은 내공 무공의 축**이다(정의서 §3-3 양·음·합·식).
        ///   캐릭터가 그것을 겸하면 내공 형태소를 배울 이유가 옅어진다.
        ///
        /// ⚠⚠ **2026-08-01 — 위 논거의 전제가 이미 무너져 있다.** 기력 소모 상수가 3 으로
        ///   내려가(커밋 9605db8) 3자 무공은 9, 4자도 12 다. 평타 전락률을 처음 실측하니
        ///   **세 경지 · 다섯 유형 전부 0.0%** — 최대기력이 50 이어도 아무도 마르지 않는다.
        ///   즉 ⓐ 와 ⓑ 는 *"80 으로 올리면"* 죽는 것이 아니라 **50 인 지금 이미 죽어 있다.**
        ///   (탈(奪) 민감도 52.5% = 무의미가 그 증상이다.)
        ///   → 이 값을 다시 볼 때는 최대기력이 아니라 **소모 상수 쪽**을 함께 봐야 한다.
        /// </remarks>
        public static CharacterStats MaxLevel()
        {
            return new CharacterStats(maxHealth: 600, maxQi: 50, attack: 8, defense: 3, agility: 4);
        }

        /// <summary>
        /// **시작 캐릭터** — 정의서 §1-1 의 값(체력만 스케일 반영).
        ///
        /// ⚠ 여기서 검증하는 것은 밸런스가 아니라 **전투의 성립**이다 — 8~15턴 안에 끝나는가,
        ///   무승부가 나지 않는가. 시작 시점에 전투가 성립하지 않으면 게임이 시작되지 않는다.
        /// </summary>
        public static CharacterStats Starting()
        {
            return new CharacterStats(maxHealth: 260, maxQi: 50, attack: 3, defense: 1, agility: 1);
        }
    }
}
