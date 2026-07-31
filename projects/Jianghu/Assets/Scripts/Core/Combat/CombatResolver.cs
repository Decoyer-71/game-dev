using System;
using System.Collections.Generic;
using System.Text;
using Jianghu.Core.Martial;
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

        // ── 상태이상 규칙 상수. 근거: docs/martial-system-proposal.md §5 ──
        /// <summary>중독 최대 중첩.</summary>
        public const int MaxPoisonStacks = 5;

        /// <summary>⚠⚠ 경직이 이만큼 쌓이면 **마비가 확정 발동**한다. 확률이 개입하지 않는다.</summary>
        public const int StaggerStacksForParalysis = 3;

        /// <summary>마비 지속(턴). 연장 불가.</summary>
        public const int ParalysisTurns = 1;

        /// <summary>마비 발동 후 이 턴 수만큼 경직을 새로 걸 수 없다 — 연속 마비 차단.</summary>
        public const int StaggerLockAfterParalysis = 2;

        /// <summary>쓸 수 있는 초식이 없을 때의 맨손 공격. 기력을 쓰지 않는다.</summary>
        private static readonly LearnedArt BasicStrike = new LearnedArt(
            MartialArt.Technique("basic_strike", "평타", Discipline.Fist, Alignment.Orthodox, basePower: 0, qiCost: 0));

        /// <summary>전투 중에만 존재하는 상태이상 하나.</summary>
        private sealed class ActiveStatus
        {
            public StatusEffectKind Kind;
            public int Potency;
            public int RemainingTurns;  // 출혈 · 기력소실 · 경직
            public int Stacks;          // 중독 · 경직
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

            return new CombatResult(outcome, winner, turn, a.Health, d.Health, log);
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

            // 3) 행동
            LearnedArt chosen = SelectArt(actor);
            int mastery = actor.Def.MasteryOf(chosen.Art.Discipline);

            int qiCost = EffectiveQiCost(chosen.Art, mastery);   // 권 숙달 → 기력 소모 감소
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

            int landed = 0;
            int crits = 0;
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

                damage += perHit;
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

            log.Add(CombatLogEntry.Action(
                turn, actor.Def.Name, target.Def.Name, chosen.Art.Name,
                attempts, landed, damage, qiCost, target.Health, note));
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

        /// <summary>명중한 초식의 상태이상 부여를 판정한다. 로그에 붙일 설명을 돌려준다.</summary>
        private static string ApplyEffects(
            int turn, Fighter actor, Fighter target, MartialArt art, int mastery,
            IRandomSource rng, List<CombatLogEntry> log)
        {
            if (art.Effects.Count == 0) return null;

            // 비도 숙달 → 상태이상이 더 잘 걸린다.
            int chanceBonus = DisciplineCurve.StatusChanceBonus(art.Discipline, mastery);

            StringBuilder note = null;
            for (int i = 0; i < art.Effects.Count; i++)
            {
                StatusApplication app = art.Effects[i];
                int chance = Clamp(app.ChancePercent + chanceBonus, 0, 100);
                if (!rng.Chance(chance)) continue;

                string applied = Apply(turn, target, app, log);
                if (applied == null) continue;

                if (note == null) note = new StringBuilder();
                else note.Append(' ');
                note.Append('[').Append(applied).Append(']');
            }

            return note?.ToString();
        }

        /// <summary>상태이상 하나를 실제로 건다. 걸리지 않았으면 null.</summary>
        private static string Apply(int turn, Fighter target, StatusApplication app, List<CombatLogEntry> log)
        {
            switch (app.Kind)
            {
                case StatusEffectKind.Bleed:
                case StatusEffectKind.QiDrain:
                {
                    // 지속제 — 중첩하지 않고 지속만 갱신한다.
                    ActiveStatus s = target.Find(app.Kind);
                    if (s == null)
                    {
                        target.Statuses.Add(new ActiveStatus
                        {
                            Kind = app.Kind,
                            Potency = app.Potency,
                            RemainingTurns = app.DurationTurns,
                        });
                    }
                    else
                    {
                        s.Potency = app.Potency;
                        s.RemainingTurns = app.DurationTurns;
                    }
                    return StatusApplication.NameOf(app.Kind);
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
                            Potency = app.Potency,
                            Stacks = 1,
                        });
                        return "중독 1중첩";
                    }
                    if (s.Stacks >= MaxPoisonStacks) return null;   // 이미 최대
                    s.Stacks++;
                    s.Potency = app.Potency;
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
                            Potency = app.Potency,
                            RemainingTurns = app.DurationTurns,
                            Stacks = 0,
                        };
                        target.Statuses.Add(s);
                    }
                    s.Potency = app.Potency;
                    s.RemainingTurns = app.DurationTurns;
                    s.Stacks++;

                    if (s.Stacks >= StaggerStacksForParalysis)
                    {
                        // 확정 발동. 경직은 전부 소멸하고, 한동안 다시 쌓을 수 없다.
                        target.Statuses.Remove(s);
                        target.ParalyzeTurns = ParalysisTurns;
                        target.StaggerLockTurns = StaggerLockAfterParalysis + ParalysisTurns;
                        log.Add(CombatLogEntry.StatusTick(turn, target.Def.Name,
                            "경직 " + StaggerStacksForParalysis + "중첩 → 마비!", 0, 0, target.Health));
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
                if (EffectiveQiCost(learned.Art, mastery) > actor.Qi) continue;

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
        ///   4자 무공(기력 16) 기준 3턴 만에 고갈되고, 그 뒤로는 평타(피해 1)만 나가
        ///   체력 100 을 50턴 안에 못 깎는다. 실제로 **전원 무승부 · 승률 0** 이 나왔다.
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
            double totalPower = (actor.Stats.Attack + artPower) * (100 + actor.PowerBonusPercent) / 100.0;

            // 도 숙달 → 방어 관통. 위력을 올리는 게 아니라 상대 방어를 무시한다 —
            // 그래서 단단한 상대에게만 강하고, 물렁한 상대에겐 이점이 거의 없다.
            int penetration = DisciplineCurve.DefensePenetrationPercent(art.Art.Discipline, mastery);
            double effectiveDefense = target.Stats.Defense * (100 - penetration) / 100.0;

            double afterDefense = totalPower - effectiveDefense;

            int perHit = (int)Math.Round(afterDefense / attempts, MidpointRounding.AwayFromZero);
            return perHit < 1 ? 1 : perHit;   // 아무리 단단해도 최소 1 은 들어간다(교착 방지)
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

        /// <summary>권 숙달로 깎인 실제 기력 소모량.</summary>
        private static int EffectiveQiCost(MartialArt art, int mastery)
        {
            int reduction = DisciplineCurve.QiCostReductionPercent(art.Discipline, mastery);
            if (reduction <= 0) return art.QiCost;

            int cost = (int)Math.Round(art.QiCost * (100 - reduction) / 100.0, MidpointRounding.AwayFromZero);
            return cost < 0 ? 0 : cost;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
