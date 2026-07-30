using System.Collections.Generic;
using Jianghu.Core.Martial.Morphemes;

namespace Jianghu.Core.Martial
{
    /// <summary>
    /// 문파 16개 정의. 설계 근거: docs/martial-system-proposal.md §3.
    ///
    /// 대문파 9(정5·사2·마2) + 소문파 7(정3·사2·마2).
    ///
    /// ⚠⚠ **빈 조합은 버그가 아니라 설정이다** (2026-07-29 확정).
    ///   사파에는 권법이 없고, 마도에는 도법과 비도가 없다.
    ///   "사파는 암기와 독이지 정직한 주먹이 아니다", "마도는 도를 쓰지 않는다" 가
    ///   성향의 색깔을 만든다. 모든 칸을 채우면 문파 정체성이 흐려진다.
    ///
    /// ⚠ 문파명은 무협 관례를 따르되 **개별 무공명은 전부 창작한다.**
    ///   널리 알려진 무공명을 그대로 쓰면 법적 문제 이전에 표절로 읽힌다.
    /// </summary>
    public static class SchoolCatalog
    {
        // ⚠⚠ 무학분류(§4 상성의 대상)는 **성향과 직교한다** (2026-07-30 확정).
        //   정파 → 양기·혼합 / 사파 → 양기·음기 / 마도 → 음기·혼합
        //   각 분류가 두 성향에 걸치므로 **상성이 성향의 경계를 가로지른다.** 정파 무공이 정파를 찌를 수도 있다.
        //   분포: 양기 6 · 음기 5 · 혼합 5.
        private static readonly List<School> AllSchools = new List<School>
        {
            // ───────────── 정파 · 대문파 (구파 3 + 세가 2) ─────────────
            // 소림 = 내외공 겸수라 혼합. ⚠ 점혈(點穴) 수법 때문에 **정파인데 마비를 건다**(예외 2).
            new School("소림사", SchoolTier.Major, Alignment.Orthodox, ArtLineage.Mixed, false, Discipline.Spear, Discipline.Fist),
            // 무당 = 태극(음양 조화)이라 혼합.
            new School("무당파", SchoolTier.Major, Alignment.Orthodox, ArtLineage.Mixed, false, Discipline.Sword, Discipline.Fist),
            new School("화산파", SchoolTier.Major, Alignment.Orthodox, ArtLineage.Yang, false, Discipline.Sword, Discipline.Blade),
            new School("남궁세가", SchoolTier.Major, Alignment.Orthodox, ArtLineage.Yang, true, Discipline.Sword, Discipline.Blade),
            // ⚠⚠ 사천당가는 정파이면서 중독을 거는 예외 문파다 (§5-5). 독·암기라 분류도 혼합이다.
            //     성향-상태이상 결합을 코드로 강제하지 않는 이유가 이 문파다.
            new School("사천당가", SchoolTier.Major, Alignment.Orthodox, ArtLineage.Mixed, true, Discipline.Fist, Discipline.Dagger),

            // ───────────── 정파 · 소문파 ─────────────
            new School("종남파", SchoolTier.Minor, Alignment.Orthodox, ArtLineage.Yang, false, Discipline.Spear),
            new School("점창파", SchoolTier.Minor, Alignment.Orthodox, ArtLineage.Yang, false, Discipline.Sword),
            new School("하북팽가", SchoolTier.Minor, Alignment.Orthodox, ArtLineage.Yang, true, Discipline.Blade),

            // ───────────── 사파 · 대문파 ─────────────
            new School("서량군문", SchoolTier.Major, Alignment.Unorthodox, ArtLineage.Yang, false, Discipline.Spear, Discipline.Blade),
            new School("살문", SchoolTier.Major, Alignment.Unorthodox, ArtLineage.Yin, false, Discipline.Sword, Discipline.Dagger),

            // ───────────── 사파 · 소문파 ─────────────
            // 녹림 = 매복·기습이라 음기.
            new School("녹림", SchoolTier.Minor, Alignment.Unorthodox, ArtLineage.Yin, false, Discipline.Blade),
            new School("장강수로채", SchoolTier.Minor, Alignment.Unorthodox, ArtLineage.Yin, false, Discipline.Dagger),

            // ───────────── 마도 · 대문파 ─────────────
            new School("천마신교", SchoolTier.Major, Alignment.Demonic, ArtLineage.Yin, false, Discipline.Sword, Discipline.Fist),
            // ⚠⚠ 혈교(血敎)는 마도인데 **출혈을 건다**(예외 3). 이름이 피인데 피를 안 쓰는 편이 부자연스럽다.
            new School("혈교", SchoolTier.Major, Alignment.Demonic, ArtLineage.Mixed, false, Discipline.Fist, Discipline.Spear),

            // ───────────── 마도 · 소문파 ─────────────
            // ⚠ 최초 안이던 '포달랍궁'은 실존 세계문화유산이자 티베트 불교 성지라 교체했다.
            new School("시마궁", SchoolTier.Minor, Alignment.Demonic, ArtLineage.Mixed, false, Discipline.Fist),
            new School("흑문", SchoolTier.Minor, Alignment.Demonic, ArtLineage.Yin, false, Discipline.Spear),
        };

        public static IReadOnlyList<School> All => AllSchools;

        /// <summary>문파명으로 찾는다. 없거나 빈 문자열이면 null(= 강호무학).</summary>
        public static School ByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < AllSchools.Count; i++)
            {
                if (AllSchools[i].Name == name) return AllSchools[i];
            }
            return null;
        }

        /// <summary>문파명에서 계층을 얻는다. 무소속(빈 문자열·미등록)은 강호무학이다.</summary>
        public static SchoolTier TierOf(string schoolName)
        {
            School s = ByName(schoolName);
            return s == null ? SchoolTier.Wanderer : s.Tier;
        }

        public static List<School> ByTier(SchoolTier tier)
        {
            var result = new List<School>();
            for (int i = 0; i < AllSchools.Count; i++)
            {
                if (AllSchools[i].Tier == tier) result.Add(AllSchools[i]);
            }
            return result;
        }

        public static List<School> ByAlignment(Alignment alignment)
        {
            var result = new List<School>();
            for (int i = 0; i < AllSchools.Count; i++)
            {
                if (AllSchools[i].Alignment == alignment) result.Add(AllSchools[i]);
            }
            return result;
        }
    }
}
