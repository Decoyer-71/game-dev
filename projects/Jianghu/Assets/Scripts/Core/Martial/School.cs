using System;
using System.Collections.Generic;
using Jianghu.Core.Martial.Morphemes;

namespace Jianghu.Core.Martial
{
    /// <summary>
    /// 문파 하나의 정의.
    ///
    /// 문파의 정체성은 **가르치는 무공 풀**이 만든다 — 스탯 보정을 주지 않는다.
    /// 조사에서 "수치 보정만으로 정체성을 만든 성공 사례"가 확인되지 않았기 때문이다
    /// (docs/martial-system-proposal.md §2). 그래서 이 클래스에는 능력치 필드가 없다.
    /// </summary>
    public sealed class School
    {
        public string Name { get; }
        public SchoolTier Tier { get; }
        public Alignment Alignment { get; }

        /// <summary>이 문파가 가르치는 무기 유형. 소문파는 1종, 대문파는 2종.</summary>
        public IReadOnlyList<Discipline> Weapons { get; }

        /// <summary>세가(世家) — 혈연으로 전승되는 가문인가. 구파(문파)와 구분한다.</summary>
        public bool IsFamily { get; }

        /// <summary>
        /// 이 문파의 무학분류(武學分類) — 양기·음기·혼합.
        ///
        /// **상성(§4)이 실제로 작동하려면 이 값이 있어야 한다.** 무공에 `낙월`(달을 떨어뜨린다)이 붙으면
        /// **음기무학에 상성 +1** 인데, 누가 음기무학인지 모르면 그 보너스가 갈 곳이 없다.
        ///
        /// ⚠⚠ **성향과 직교한다** (2026-07-30 확정). 정파라고 다 같은 분류가 아니다:
        ///   · 정파 → 양기 또는 혼합    · 사파 → 양기 또는 음기    · 마도 → 음기 또는 혼합
        /// 각 분류가 두 성향에 걸치므로 **상성이 성향의 경계를 가로지른다.**
        /// 정파 무공이 사파와 마도를 한 번에 찌를 수도, 같은 정파를 찌를 수도 있다.
        /// </summary>
        public ArtLineage Lineage { get; }

        public School(
            string name, SchoolTier tier, Alignment alignment, ArtLineage lineage, bool isFamily,
            params Discipline[] weapons)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("문파명은 비어 있을 수 없다.", nameof(name));
            if (tier == SchoolTier.Wanderer) throw new ArgumentException("강호무학은 문파가 아니다.", nameof(tier));
            if (weapons == null || weapons.Length == 0) throw new ArgumentException("무기 유형이 없다.", nameof(weapons));

            int expected = tier == SchoolTier.Major ? 2 : 1;
            if (weapons.Length != expected)
            {
                throw new ArgumentException(
                    name + ": " + (tier == SchoolTier.Major ? "대문파는" : "소문파는") + " 무기 " + expected + "종이어야 한다 (현재 " + weapons.Length + "종)",
                    nameof(weapons));
            }
            for (int i = 0; i < weapons.Length; i++)
            {
                if (weapons[i].IsSupport())
                {
                    throw new ArgumentException("내공·경공은 무기가 아니다.", nameof(weapons));
                }
            }

            if (lineage == ArtLineage.None)
            {
                throw new ArgumentException(name + ": 문파는 무학분류를 가져야 한다. 상성(§4)이 갈 곳을 잃는다.", nameof(lineage));
            }

            Name = name;
            Tier = tier;
            Alignment = alignment;
            Lineage = lineage;
            IsFamily = isFamily;
            Weapons = weapons;
        }

        /// <summary>
        /// 이 문파가 보유해야 할 무공 수.
        ///
        /// **대문파 9** = 문파무학 8(공격 4 · 내공 2 · 경공 2) + **전승무학 1**
        /// **소문파 4** = 공격 2 · 내공 1 · 경공 1
        ///
        /// ⚠⚠ 2026-07-29 정의서 §5-1 에 맞춰 **6/3 에서 고쳤다.** 이전 값은
        ///   `martial-system-proposal.md` §3-1(총 84개) 기준이었고, 정의서가 총 122개로 확정하며
        ///   대문파 공격을 무기당 2종(총 4)으로 늘리고 전승무학을 계층으로 세웠다.
        ///   무공 리소스의 진실의 원천은 정의서다(설계안 §1-H).
        /// </summary>
        public int ExpectedArtCount => Tier == SchoolTier.Major ? 9 : 4;

        public override string ToString() => Name;
    }
}
