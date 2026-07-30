using System.Collections.Generic;
using Jianghu.Core.Martial.Morphemes;

namespace Jianghu.Core.Martial
{
    /// <summary>
    /// 무공 138종. **이 파일에는 수치가 하나도 없다** — 전부 무공명에서 유도된다(정의서 §0).
    ///
    /// ⚠⚠ 2026-07-30 전면 교체. 이전에는 36종에 `basePower: 25` 같은 값을 손으로 박아 뒀는데,
    ///   형태소 체계로 넘어오며 **수치가 이 파일에서 사라졌다.** 여기 남은 것은 이름·계층·유형·문파뿐이고
    ///   공격력·기력 소모·명중·상태이상은 <see cref="MartialArtFactory"/> 가 이름을 분해해 만든다.
    ///   그래서 밸런스를 고칠 때 이 파일이 아니라 **형태소 사전**을 고친다 — 정의서 §0 채택 근거 1
    ///   ("콘텐츠 비용이 선형 → 상수")이 여기서 실물이 된다.
    ///
    /// 근거 문서: 작명은 `../../docs/martial-art-naming.md`, 형태소 수치는 `martial-resource-spec.md`.
    ///
    /// ⚠ 성향은 문파에서 끌어온다(<see cref="SchoolCatalog"/>). 무공마다 적으면 문파와 어긋날 수 있다.
    /// ⚠ **강호무학과 절대경지는 성향이 없다**(null). 강호무학은 익힌 사람을 따르고(§5-1 "시작점이자
    ///   최후의 보루"), 절대경지는 기연으로 얻어 문파에 속하지 않는다(§5-3).
    ///
    /// 구성: 강호 9 + 소문파 28 + 문파무학 72 + 전승 9 + 대형세력 16 + 절대경지 4 = **138** (§5-4)
    /// </summary>
    public static class MartialArtCatalog
    {
        private static readonly List<MartialArt> AllArts = BuildAll();

        public static IReadOnlyList<MartialArt> All => AllArts;

        /// <summary>공격 초식만. 승률표(`Tools/Sandbox`)가 쓴다.</summary>
        public static List<MartialArt> Techniques()
        {
            var result = new List<MartialArt>();
            for (int i = 0; i < AllArts.Count; i++)
            {
                if (!AllArts[i].Discipline.IsSupport()) result.Add(AllArts[i]);
            }
            return result;
        }

        public static MartialArt ById(string id)
        {
            for (int i = 0; i < AllArts.Count; i++)
            {
                if (AllArts[i].Id == id) return AllArts[i];
            }
            return null;
        }

        /// <summary>이름으로 찾는다. 무공명은 138종이 전부 고유하다(테스트로 고정).</summary>
        public static MartialArt ByName(string name)
        {
            for (int i = 0; i < AllArts.Count; i++)
            {
                if (AllArts[i].Name == name) return AllArts[i];
            }
            return null;
        }

        public static List<MartialArt> BySchool(string school)
        {
            var result = new List<MartialArt>();
            for (int i = 0; i < AllArts.Count; i++)
            {
                if (AllArts[i].School == school) result.Add(AllArts[i]);
            }
            return result;
        }

        // ─────────────────────────── 구축 ───────────────────────────

        private static List<MartialArt> BuildAll()
        {
            var list = new List<MartialArt>();
            int seq = 0;

            void Add(string name, ArtKind kind, ArtTier tier, Discipline d, string school = null,
                     Alignment? explicitAlignment = null)
            {
                Alignment? align = explicitAlignment;
                if (align == null && !string.IsNullOrEmpty(school))
                {
                    School s = SchoolCatalog.ByName(school);
                    if (s != null) align = s.Alignment;
                }

                seq++;
                list.Add(MartialArtFactory.Create(
                    "a" + seq.ToString("D3"), name, kind, tier, d, align, school));
            }

            void Atk(string name, ArtTier tier, Discipline d, string school) => Add(name, ArtKind.Attack, tier, d, school);
            void Inr(string name, ArtTier tier, string school) => Add(name, ArtKind.Internal, tier, Discipline.InnerArt, school);
            void Stp(string name, ArtTier tier, string school) => Add(name, ArtKind.Movement, tier, Discipline.Movement, school);

            // ═══ 강호무학 9 — 성향 없음. 무기 접미사가 종류를 밝힌다 ═══
            Add("절정검법", ArtKind.Attack, ArtTier.Wanderer, Discipline.Sword);
            Add("벌중도법", ArtKind.Attack, ArtTier.Wanderer, Discipline.Blade);
            Add("자쾌창법", ArtKind.Attack, ArtTier.Wanderer, Discipline.Spear);
            Add("타환권법", ArtKind.Attack, ArtTier.Wanderer, Discipline.Fist);
            Add("투유표법", ArtKind.Attack, ArtTier.Wanderer, Discipline.Dagger);
            Add("양화신공", ArtKind.Internal, ArtTier.Wanderer, Discipline.InnerArt);
            Add("음유심법", ArtKind.Internal, ArtTier.Wanderer, Discipline.InnerArt);
            Add("피신보", ArtKind.Movement, ArtTier.Wanderer, Discipline.Movement);
            Add("반유신법", ArtKind.Movement, ArtTier.Wanderer, Discipline.Movement);

            // ═══ 소문파 28 (7문파 × 4) ═══
            const ArtTier MIN = ArtTier.Minor;
            Atk("정풍창", MIN, Discipline.Spear, "종남파");
            Atk("정탈창", MIN, Discipline.Spear, "종남파");
            Inr("신풍양공", MIN, "종남파");
            Stp("쾌풍섬보", MIN, "종남파");

            Atk("창천낙월", MIN, Discipline.Sword, "점창파");
            Atk("쾌자탈", MIN, Discipline.Sword, "점창파");
            Inr("양명정공", MIN, "점창파");
            Stp("쾌섬신술", MIN, "점창파");

            Atk("중화참", MIN, Discipline.Blade, "하북팽가");
            Atk("후탈벌", MIN, Discipline.Blade, "하북팽가");
            Inr("화중양공", MIN, "하북팽가");
            Stp("항중화보", MIN, "하북팽가");

            Atk("환혈참", MIN, Discipline.Blade, "녹림");
            Atk("궤독벌", MIN, Discipline.Blade, "녹림");
            Inr("암음혈공", MIN, "녹림");
            Stp("둔암유보", MIN, "녹림");

            Atk("유독척", MIN, Discipline.Dagger, "장강수로채");
            Atk("변혈투", MIN, Discipline.Dagger, "장강수로채");
            Inr("음수유공", MIN, "장강수로채");
            Stp("유수피술", MIN, "장강수로채");

            Atk("환비격", MIN, Discipline.Fist, "시마궁");
            Atk("야궤타", MIN, Discipline.Fist, "시마궁");
            Inr("암음궤공", MIN, "시마궁");
            Stp("반환야보", MIN, "시마궁");

            Atk("중경창", MIN, Discipline.Spear, "흑문");
            Atk("암중자", MIN, Discipline.Spear, "흑문");
            Inr("한음중공", MIN, "흑문");
            Stp("항암중보", MIN, "흑문");

            // ═══ 문파무학 72 (9대문파 × 8) ═══
            const ArtTier MAJ = ArtTier.Major;
            Atk("중뇌창", MAJ, Discipline.Spear, "소림사");
            Atk("정천창군", MAJ, Discipline.Spear, "소림사");
            Atk("후격비혼", MAJ, Discipline.Fist, "소림사");
            Atk("직뇌타비", MAJ, Discipline.Fist, "소림사");
            Inr("중뇌양공", MAJ, "소림사");
            Inr("명식후결", MAJ, "소림사");
            Stp("계중명보", MAJ, "소림사");
            Stp("응후급보", MAJ, "소림사");

            Atk("유수참탈", MAJ, Discipline.Sword, "무당파");
            Atk("참천멸월", MAJ, Discipline.Sword, "무당파");
            Atk("유운박탈", MAJ, Discipline.Fist, "무당파");
            Atk("변현격탈", MAJ, Discipline.Fist, "무당파");
            Inr("유식수공", MAJ, "무당파");
            Inr("변명양결", MAJ, "무당파");
            Stp("피현유보", MAJ, "무당파");
            Stp("역변명보", MAJ, "무당파");

            Atk("쾌풍절탈", MAJ, Discipline.Sword, "화산파");
            Atk("유명참일", MAJ, Discipline.Sword, "화산파");
            Atk("급쾌벌탈", MAJ, Discipline.Blade, "화산파");
            Atk("풍정단", MAJ, Discipline.Blade, "화산파");
            Inr("쾌풍양공", MAJ, "화산파");
            Inr("신음유결", MAJ, "화산파");
            Stp("섬풍쾌보", MAJ, "화산파");
            Stp("반신풍보", MAJ, "화산파");

            Atk("정화참탈", MAJ, Discipline.Sword, "남궁세가");
            Atk("직천참일", MAJ, Discipline.Sword, "남궁세가");
            Atk("직명벌탈", MAJ, Discipline.Blade, "남궁세가");
            Atk("정화단광", MAJ, Discipline.Blade, "남궁세가");
            Inr("정화양공", MAJ, "남궁세가");
            Inr("직명합결", MAJ, "남궁세가");
            Stp("방정화보", MAJ, "남궁세가");
            Stp("반직명보", MAJ, "남궁세가");

            Atk("쾌독척", MAJ, Discipline.Dagger, "사천당가");
            Atk("궤암포독", MAJ, Discipline.Dagger, "사천당가");
            Atk("환독타", MAJ, Discipline.Fist, "사천당가");
            Atk("유암격독", MAJ, Discipline.Fist, "사천당가");
            Inr("암음독공", MAJ, "사천당가");
            Inr("쾌양독결", MAJ, "사천당가");
            Stp("둔암쾌보", MAJ, "사천당가");
            Stp("피급독술", MAJ, "사천당가");

            Atk("중혈창", MAJ, Discipline.Spear, "서량군문");
            Atk("후냉자혈", MAJ, Discipline.Spear, "서량군문");
            Atk("중명벌혈", MAJ, Discipline.Blade, "서량군문");
            Atk("후참혈군", MAJ, Discipline.Blade, "서량군문");
            Inr("중냉양공", MAJ, "서량군문");
            Inr("야음혈결", MAJ, "서량군문");
            Stp("항중냉보", MAJ, "서량군문");
            Stp("거후혈보", MAJ, "서량군문");

            Atk("환야절혈", MAJ, Discipline.Sword, "살문");
            Atk("절해망혼", MAJ, Discipline.Sword, "살문");
            Atk("궤암척혈", MAJ, Discipline.Dagger, "살문");
            Atk("환한투독", MAJ, Discipline.Dagger, "살문");
            Inr("야음환공", MAJ, "살문");
            Inr("한양궤결", MAJ, "살문");
            Stp("둔야환보", MAJ, "살문");
            Stp("섬암궤술", MAJ, "살문");

            Atk("중한참경", MAJ, Discipline.Sword, "천마신교");
            Atk("후냉절월", MAJ, Discipline.Sword, "천마신교");
            Atk("중야격경", MAJ, Discipline.Fist, "천마신교");
            Atk("후암타비", MAJ, Discipline.Fist, "천마신교");
            Inr("중한양공", MAJ, "천마신교");
            Inr("후음비결", MAJ, "천마신교");
            Stp("호중한보", MAJ, "천마신교");
            Stp("역후비보", MAJ, "천마신교");

            Atk("환화격혈", MAJ, Discipline.Fist, "혈교");
            Atk("궤화박경", MAJ, Discipline.Fist, "혈교");
            Atk("환몽자혈", MAJ, Discipline.Spear, "혈교");
            Atk("환창혈만", MAJ, Discipline.Spear, "혈교");
            Inr("환화양공", MAJ, "혈교");
            Inr("휘음궤결", MAJ, "혈교");
            Stp("어환화보", MAJ, "혈교");
            Stp("반궤경보", MAJ, "혈교");

            // ═══ 전승무학 9 — 대문파당 1. 극한경지를 쓸 수 있는 유일한 계층(§5-2) ═══
            const ArtTier LEG = ArtTier.Legacy;
            Atk("성뇌후격", LEG, Discipline.Fist, "소림사");
            Inr("선음유수", LEG, "무당파");                        // 유일한 내공 전승무학
            Atk("존풍쾌절", LEG, Discipline.Sword, "화산파");
            Atk("제화정참", LEG, Discipline.Sword, "남궁세가");
            Atk("만우쾌사", LEG, Discipline.Dagger, "사천당가");   // ⚠ 유일하게 극한경지를 안 쓴다
            Atk("패혈중창", LEG, Discipline.Spear, "서량군문");
            Atk("황야환투", LEG, Discipline.Dagger, "살문");
            Atk("마한중참", LEG, Discipline.Sword, "천마신교");
            Atk("종환화격", LEG, Discipline.Fist, "혈교");

            // ═══ 대형세력 16 (4세력 × 4) — 급은 대문파급(§5-5) ═══
            // ⚠ 세력은 `SchoolCatalog` 에 없으므로 성향을 명시한다.
            //   **제천성만 null** 이다 — 정·사·마 출신을 다 받는 유일한 세력이고,
            //   그 성향 배타 해제가 제천성의 특권이다(§5-5-b).
            void Fac(string name, ArtKind kind, Discipline d, string faction, Alignment? align)
                => Add(name, kind, ArtTier.Major, d, faction, align);

            Fac("명정자탈", ArtKind.Attack, Discipline.Spear, "무림맹", Alignment.Orthodox);
            Fac("절지낙월", ArtKind.Attack, Discipline.Sword, "무림맹", Alignment.Orthodox);
            Fac("광양직공", ArtKind.Internal, Discipline.InnerArt, "무림맹", Alignment.Orthodox);
            Fac("방직명보", ArtKind.Movement, Discipline.Movement, "무림맹", Alignment.Orthodox);

            Fac("궤야척혈", ArtKind.Attack, Discipline.Dagger, "사도련", Alignment.Unorthodox);
            Fac("환벌혈군", ArtKind.Attack, Discipline.Blade, "사도련", Alignment.Unorthodox);
            Fac("한음궤공", ArtKind.Internal, Discipline.InnerArt, "사도련", Alignment.Unorthodox);
            Fac("둔궤야보", ArtKind.Movement, Discipline.Movement, "사도련", Alignment.Unorthodox);

            Fac("중냉참경", ArtKind.Attack, Discipline.Sword, "제천성", null);
            Fac("후명격경", ArtKind.Attack, Discipline.Fist, "제천성", null);
            Fac("냉음중공", ArtKind.Internal, Discipline.InnerArt, "제천성", null);
            Fac("거후명보", ArtKind.Movement, Discipline.Movement, "제천성", null);

            Fac("환야참비", ArtKind.Attack, Discipline.Sword, "천마신교", Alignment.Demonic);
            Fac("궤격비전", ArtKind.Attack, Discipline.Fist, "천마신교", Alignment.Demonic);
            Fac("암음환결", ArtKind.Internal, Discipline.InnerArt, "천마신교", Alignment.Demonic);
            Fac("섬환야술", ArtKind.Movement, Discipline.Movement, "천마신교", Alignment.Demonic);

            // ═══ 절대경지 4 — 기연으로만. 전부 내공이고 성향이 없다(§5-3) ═══
            // ⚠ 규칙 변경 4종(상태이상 면역 / 기력 무소모 / 2회 행동 / 상성 절대우위)은
            //   형태소가 아니라 **별도 플래그**로 붙는다(설계안 §3-4). 아직 미구현이다.
            Add("정합광일", ArtKind.Internal, ArtTier.Absolute, Discipline.InnerArt);
            Add("식유수혼", ArtKind.Internal, ArtTier.Absolute, Discipline.InnerArt);
            Add("음쾌신월", ArtKind.Internal, ArtTier.Absolute, Discipline.InnerArt);
            Add("합현혼유", ArtKind.Internal, ArtTier.Absolute, Discipline.InnerArt);

            return list;
        }
    }
}
