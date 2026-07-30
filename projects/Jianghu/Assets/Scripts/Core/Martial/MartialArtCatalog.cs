using System.Collections.Generic;

namespace Jianghu.Core.Martial
{
    /// <summary>
    /// 무공 정의 목록.
    ///
    /// 전투 코드는 무공을 하드코딩하지 않고 **이 목록에서만** 가져온다.
    /// 무공이 84개로 늘어도 <see cref="CombatResolver"/> 는 한 줄도 바뀌지 않는다.
    ///
    /// ── 진행 상태 ────────────────────────────────────────────────────────
    /// 최종 목표는 **84개**(강호무학 9 + 소문파 7×3 + 대문파 9×6).
    /// 지금은 **1차 36개**만 채웠다 — 3계층 × 3성향 × 5무기를 전부 덮는 최소 집합이다.
    /// 한 번에 84개를 넣으면 문제가 생겼을 때 원인이 수치인지 구조인지 분리할 수 없다.
    ///   · 강호무학 9 (전부)
    ///   · 소문파 3곳 — 점창파(정·검) / 녹림(사·도) / 흑문(마·창)
    ///   · 대문파 3곳 — 소림사(정·창권) / 살문(사·검비도) / 천마신교(마·검권)
    /// ────────────────────────────────────────────────────────────────────
    ///
    /// ⚠⚠ **무공명은 전부 창작이고 수치는 미검증 초기값이다.**
    /// 문파명은 무협 관례를 따랐지만(SchoolCatalog 참조) 개별 무공명은 어느 작품에서도 가져오지 않았다.
    /// 널리 알려진 무공명을 그대로 쓰면 법적 문제 이전에 표절로 읽히기 때문이다.
    /// 수치는 승률표(Tools/Sandbox)를 돌려 계층 안에서 조정할 대상이다.
    ///
    /// ⚠ 아직 넣지 않은 것: **상징 무공 표시**(대문파의 전승무학)와 **입문 조건**(요구 숙달치).
    ///   둘 다 구조가 검증된 뒤에 얹는다.
    /// </summary>
    public static class MartialArtCatalog
    {
        /// <summary>강호무학 — 문파에 속하지 않은 무학.</summary>
        public const string Wanderer = "";

        // 상태이상 정의를 짧게 쓰기 위한 도우미
        private static StatusApplication Bleed(int chance, int potency, int turns)
            => new StatusApplication(StatusEffectKind.Bleed, chance, potency, turns);

        private static StatusApplication Poison(int chance, int potency)
            => new StatusApplication(StatusEffectKind.Poison, chance, potency, 1);

        private static StatusApplication QiDrain(int chance, int potency, int turns)
            => new StatusApplication(StatusEffectKind.QiDrain, chance, potency, turns);

        private static StatusApplication Stagger(int chance, int potency, int turns)
            => new StatusApplication(StatusEffectKind.Stagger, chance, potency, turns);

        private static readonly List<MartialArt> AllArts = new List<MartialArt>
        {
            // ══════════════ 강호무학 (9) ══════════════
            // 무소속. **상태이상이 없다** — 평범하지만 안정적인 것이 정체성이다.
            MartialArt.Technique("wd_sword", "청강검법(靑剛劍法)", Discipline.Sword, Alignment.Orthodox,
                basePower: 22, qiCost: 6, hitCount: 1, accuracyBonus: 5, school: Wanderer),
            MartialArt.Technique("wd_blade", "벌목도법(伐木刀法)", Discipline.Blade, Alignment.Orthodox,
                basePower: 26, qiCost: 8, hitCount: 1, accuracyBonus: -10, school: Wanderer),
            MartialArt.Technique("wd_spear", "파산창법(把山槍法)", Discipline.Spear, Alignment.Orthodox,
                basePower: 22, qiCost: 5, hitCount: 1, accuracyBonus: 0, school: Wanderer),
            MartialArt.Technique("wd_fist", "통배권(通背拳)", Discipline.Fist, Alignment.Orthodox,
                basePower: 22, qiCost: 5, hitCount: 3, accuracyBonus: 5, school: Wanderer),
            MartialArt.Technique("wd_dagger", "유엽비도(柳葉飛刀)", Discipline.Dagger, Alignment.Orthodox,
                basePower: 18, qiCost: 4, hitCount: 2, accuracyBonus: 10, school: Wanderer),
            MartialArt.Support("wd_inner1", "토납심결(吐納心訣)", Discipline.InnerArt, Alignment.Orthodox,
                maxQiBonus: 18, powerBonusPercent: 10, school: Wanderer),
            MartialArt.Support("wd_inner2", "축기공(蓄氣功)", Discipline.InnerArt, Alignment.Orthodox,
                maxQiBonus: 25, powerBonusPercent: 5, school: Wanderer),
            MartialArt.Support("wd_move1", "답보(踏步)", Discipline.Movement, Alignment.Orthodox,
                evasionBonus: 7, initiativeBonus: 5, school: Wanderer),
            MartialArt.Support("wd_move2", "유신보(遊身步)", Discipline.Movement, Alignment.Orthodox,
                evasionBonus: 10, initiativeBonus: 2, school: Wanderer),

            // ══════════════ 소문파 · 점창파 (정파 · 검) ══════════════
            MartialArt.Technique("jc_nakseong", "낙성검(落星劍)", Discipline.Sword, Alignment.Orthodox,
                basePower: 24, qiCost: 7, hitCount: 1, accuracyBonus: 5, school: "점창파",
                QiDrain(40, 6, 2)),
            MartialArt.Support("jc_inner", "창운심결(蒼雲心訣)", Discipline.InnerArt, Alignment.Orthodox,
                maxQiBonus: 22, powerBonusPercent: 12, school: "점창파"),
            MartialArt.Support("jc_move", "표풍신법(飄風身法)", Discipline.Movement, Alignment.Orthodox,
                evasionBonus: 9, initiativeBonus: 7, school: "점창파"),

            // ══════════════ 소문파 · 녹림 (사파 · 도) ══════════════
            MartialArt.Technique("nr_sanjeok", "산적도법(山賊刀法)", Discipline.Blade, Alignment.Unorthodox,
                basePower: 27, qiCost: 8, hitCount: 1, accuracyBonus: -10, school: "녹림",
                Bleed(40, 7, 3)),
            MartialArt.Support("nr_inner", "흑호공(黑虎功)", Discipline.InnerArt, Alignment.Unorthodox,
                maxQiBonus: 20, powerBonusPercent: 12, school: "녹림"),
            MartialArt.Support("nr_move", "도주보(逃走步)", Discipline.Movement, Alignment.Unorthodox,
                evasionBonus: 11, initiativeBonus: 4, school: "녹림"),

            // ══════════════ 소문파 · 흑문 (마도 · 창) ══════════════
            MartialArt.Technique("hm_heuksal", "흑살창(黑殺槍)", Discipline.Spear, Alignment.Demonic,
                basePower: 24, qiCost: 6, hitCount: 1, accuracyBonus: 0, school: "흑문",
                Stagger(45, 8, 2)),
            MartialArt.Support("hm_inner", "암류심법(暗流心法)", Discipline.InnerArt, Alignment.Demonic,
                maxQiBonus: 20, powerBonusPercent: 14, school: "흑문"),
            MartialArt.Support("hm_move", "귀영보(鬼影步)", Discipline.Movement, Alignment.Demonic,
                evasionBonus: 8, initiativeBonus: 8, school: "흑문"),

            // ══════════════ 대문파 · 소림사 (정파 · 창/권) ══════════════
            MartialArt.Technique("sr_hangma", "항마창(降魔槍)", Discipline.Spear, Alignment.Orthodox,
                basePower: 26, qiCost: 7, hitCount: 1, accuracyBonus: 0, school: "소림사",
                QiDrain(45, 7, 2)),
            MartialArt.Technique("sr_bokho", "복호권(伏虎拳)", Discipline.Fist, Alignment.Orthodox,
                basePower: 24, qiCost: 5, hitCount: 3, accuracyBonus: 5, school: "소림사",
                QiDrain(40, 6, 2)),
            MartialArt.Support("sr_inner1", "금강선공(金剛禪功)", Discipline.InnerArt, Alignment.Orthodox,
                maxQiBonus: 28, powerBonusPercent: 15, school: "소림사"),
            MartialArt.Support("sr_inner2", "세수공(洗髓功)", Discipline.InnerArt, Alignment.Orthodox,
                maxQiBonus: 35, powerBonusPercent: 10, school: "소림사"),
            MartialArt.Support("sr_move1", "나한보(羅漢步)", Discipline.Movement, Alignment.Orthodox,
                evasionBonus: 10, initiativeBonus: 6, school: "소림사"),
            MartialArt.Support("sr_move2", "초혜공(草鞋功)", Discipline.Movement, Alignment.Orthodox,
                evasionBonus: 6, initiativeBonus: 11, school: "소림사"),

            // ══════════════ 대문파 · 살문 (사파 · 검/비도) ══════════════
            MartialArt.Technique("sm_muyeong", "무영검(無影劍)", Discipline.Sword, Alignment.Unorthodox,
                basePower: 25, qiCost: 7, hitCount: 1, accuracyBonus: 5, school: "살문",
                Bleed(40, 7, 3)),
            // ⚠ 비도 + 중독 — 비도 숙달(상태이상 확률 +30%p)과 곱해져 강해지기 쉽다. 위력을 낮게 잡았다.
            MartialArt.Technique("sm_jeolmyeong", "절명비도(絶命飛刀)", Discipline.Dagger, Alignment.Unorthodox,
                basePower: 19, qiCost: 4, hitCount: 2, accuracyBonus: 10, school: "살문",
                Poison(45, 4)),
            MartialArt.Support("sm_inner1", "잠행심법(潛行心法)", Discipline.InnerArt, Alignment.Unorthodox,
                maxQiBonus: 22, powerBonusPercent: 13, school: "살문"),
            MartialArt.Support("sm_inner2", "사혼공(死魂功)", Discipline.InnerArt, Alignment.Unorthodox,
                maxQiBonus: 18, powerBonusPercent: 16, school: "살문"),
            MartialArt.Support("sm_move1", "무성보(無聲步)", Discipline.Movement, Alignment.Unorthodox,
                evasionBonus: 12, initiativeBonus: 5, school: "살문"),
            MartialArt.Support("sm_move2", "야행술(夜行術)", Discipline.Movement, Alignment.Unorthodox,
                evasionBonus: 8, initiativeBonus: 9, school: "살문"),

            // ══════════════ 대문파 · 천마신교 (마도 · 검/권) ══════════════
            MartialArt.Technique("cm_geomgyeol", "천마검결(天魔劍訣)", Discipline.Sword, Alignment.Demonic,
                basePower: 26, qiCost: 8, hitCount: 1, accuracyBonus: 0, school: "천마신교",
                Stagger(45, 8, 2)),
            MartialArt.Technique("cm_mara", "마라권(魔羅拳)", Discipline.Fist, Alignment.Demonic,
                basePower: 24, qiCost: 5, hitCount: 3, accuracyBonus: 5, school: "천마신교",
                Stagger(35, 6, 2)),
            MartialArt.Support("cm_inner1", "탈혼심공(奪魂心功)", Discipline.InnerArt, Alignment.Demonic,
                maxQiBonus: 30, powerBonusPercent: 16, school: "천마신교"),
            MartialArt.Support("cm_inner2", "마령신공(魔靈神功)", Discipline.InnerArt, Alignment.Demonic,
                maxQiBonus: 22, powerBonusPercent: 20, school: "천마신교"),
            MartialArt.Support("cm_move1", "마영보(魔影步)", Discipline.Movement, Alignment.Demonic,
                evasionBonus: 10, initiativeBonus: 8, school: "천마신교"),
            MartialArt.Support("cm_move2", "흑풍신법(黑風身法)", Discipline.Movement, Alignment.Demonic,
                evasionBonus: 7, initiativeBonus: 12, school: "천마신교"),
        };

        public static IReadOnlyList<MartialArt> All => AllArts;

        public static MartialArt ById(string id)
        {
            for (int i = 0; i < AllArts.Count; i++)
            {
                if (AllArts[i].Id == id) return AllArts[i];
            }
            return null;
        }

        /// <summary>공격 초식만(내공·경공 제외).</summary>
        public static List<MartialArt> Techniques() => Where(a => !a.Discipline.IsSupport());

        public static List<MartialArt> ByDiscipline(Discipline d) => Where(a => a.Discipline == d);

        public static List<MartialArt> ByAlignment(Alignment a) => Where(x => x.Alignment == a);

        /// <summary>해당 문파의 무공. 빈 문자열이면 강호무학.</summary>
        public static List<MartialArt> BySchool(string school) => Where(a => a.School == (school ?? string.Empty));

        /// <summary>계층별. 밸런싱은 **같은 계층 안에서만** 비교한다(SchoolTier 주석 참조).</summary>
        public static List<MartialArt> ByTier(SchoolTier tier) => Where(a => a.Tier == tier);

        private static List<MartialArt> Where(System.Func<MartialArt, bool> predicate)
        {
            var result = new List<MartialArt>();
            for (int i = 0; i < AllArts.Count; i++)
            {
                if (predicate(AllArts[i])) result.Add(AllArts[i]);
            }
            return result;
        }
    }
}
