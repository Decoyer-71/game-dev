using System.Collections.Generic;
using Jianghu.Core.Characters;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;

namespace Jianghu.Core.Combat
{
    /// <summary>
    /// **무공과 경지에서 대전자(<see cref="Combatant"/>)를 만든다.**
    ///
    /// ⚠⚠ **왜 Core 에 있는가** — 이 규칙 셋이 `Tools/Sandbox/Program.cs` 안의 **private 메서드**
    ///   하나에만 있었다. 전투 화면이 같은 것을 다시 쓰면 갈라지는데, 이 저장소는 그 형태를 이미
    ///   **다섯 번** 겪었다 — 상성·범위·열·종류가 `MartialArtFactory` 에서 버려졌던 것,
    ///   그리고 enum 한글 이름표가 네 곳에 흩어져 있던 것.
    ///
    /// ⚠⚠ **여기 있는 규칙은 "측정용 대전자" 의 규칙이다.** 실제 게임의 제자는 Phase 3 에서
    ///   경험치·수련으로 자라며, 그때 이 클래스가 어디까지 쓰일지는 아직 모른다(설계 §10).
    ///
    /// ⚠ **동작을 바꾸지 않고 옮긴 것이다** (2026-08-23). Sandbox 의 지표가 한 칸이라도 움직이면
    ///   그것은 옮기다 규칙을 흘린 것이므로, `--compare` 로 **바뀜 0건**을 확인한 뒤에만 넘어간다(§5-D).
    /// </summary>
    public static class CombatantBuilder
    {
        /// <summary>
        /// 대전자를 만든다.
        ///
        /// <para>규칙 셋 — 전부 측정을 통제하기 위한 것이고, 근거는 각 줄 주석에 있다.</para>
        /// </summary>
        /// <param name="name">
        /// 대전자 이름. **전투 로그가 이 이름으로만 사람을 가리킨다**(<see cref="CombatLogEntry"/> 는
        /// 문자열만 든다). ⚠ 4대4 에서 같은 무공을 여럿이 들면 이름이 겹쳐
        /// *"만우쾌사의 만우쾌사 → 만우쾌사"* 가 되므로, 편성하는 쪽이 `A1 만우쾌사` 처럼 자리를 붙인다.
        /// </param>
        /// <param name="arts">
        /// 익힌 무공들. **첫 번째가 주(主) 무공**이고 성향·유형 숙달·무학분류를 그것에서 읽는다.
        /// ⚠ 비어 있을 수 없다.
        /// </param>
        /// <param name="stage">무공 경지 1~10성.</param>
        /// <param name="stats">캐릭터 능력치. 비우면 <see cref="CharacterStats.MaxLevel"/>.</param>
        public static Combatant Build(
            string name, IReadOnlyList<MartialArt> arts, int stage, CharacterStats stats = null)
        {
            if (arts == null) throw new System.ArgumentNullException(nameof(arts));
            if (arts.Count == 0)
            {
                throw new System.ArgumentException("무공이 하나도 없는 대전자는 만들 수 없다.", nameof(arts));
            }

            MartialArt primary = arts[0];

            // ⚠ 강호무학은 성향이 없어 익힌 사람의 성향이 필요하다. 측정에서는 정파로 고정한다 —
            //   성향별 비교는 문파 무공으로 하고, 강호무학은 계층 비교용 표본일 뿐이다.
            Alignment owner = OwnerAlignmentOf(primary);

            // ⚠⚠ 경지 → 수련 횟수 환산은 **성향마다 다르다**(정파 0.70/회 · 사파 1.60 → 소프트캡 후 1/5 ·
            //   마도 0.45). 그래서 횟수가 아니라 경지로 지정한다 — 그래야 세 성향의 **같은 지점**을 비교한다.
            //   같은 200회가 정파에게는 10성이고 마도에게는 9성이다.
            int sessions = SessionsForStage(owner, stage);

            var learned = new List<LearnedArt>(arts.Count);
            for (int i = 0; i < arts.Count; i++)
            {
                // ⚠ 보조 무공도 **같은 경지**로 둔다. 주 무공만 올리면 보조가 경지 변수에 딸려 흔들린다.
                learned.Add(new LearnedArt(arts[i], sessions, owner));
            }

            // ⚠⚠ **유형 숙달은 만렙 고정이다** (2026-07-31 사용자 교정 — 그 근거를 규칙과 함께 옮겨 왔다).
            //   그전에는 `sessions` 하나로 **무공 숙련과 유형 숙달을 동시에** 올리고 있었다. 둘은
            //   다른 축이다 — 무공 경지는 무공마다 따로 쌓고(1~10성), 유형 숙달(백일창·천일도·만일검)은
            //   **사람이 그 무기를 얼마나 다뤘는가**로 캐릭터 쪽에 가깝다. 뭉쳐서 재면
            //   *"무공이 세진 것인지 사람이 세진 것인지"* 를 분리할 수 없다.
            //   → 캐릭터 능력치를 만렙으로 고정한 것과 같은 이유로 숙달도 고정한다.
            // ⚠⚠ **주 무공의 유형 하나만** 만숙으로 둔다. 보조 무공(내공·경공)의 유형은 넣지 않는데,
            //   이것은 옮겨 온 규칙 그대로다 — 바꾸면 기존 측정이 통째로 움직인다.
            //   ⚠ 한 사람이 검법과 도법을 같이 들면 도(刀)는 미숙달이 된다. **아직 답이 없는 물음**이고
            //     Phase 3(제자 성장)이 다룰 자리다. 여기서 지어내지 않는다.
            var masteries = new List<DisciplineMastery>
            {
                new DisciplineMastery(primary.Discipline, DisciplineCurve.SessionsToMaster(primary.Discipline)),
            };

            return new Combatant(name, stats ?? CharacterStats.MaxLevel(), learned, masteries, LineageOf(primary));
        }

        /// <summary>무공 하나만 익힌 대전자. 이름은 무공명을 그대로 쓴다.</summary>
        public static Combatant Build(MartialArt art, int stage, CharacterStats stats = null)
        {
            if (art == null) throw new System.ArgumentNullException(nameof(art));
            return Build(art.Name, new[] { art }, stage, stats);
        }

        /// <summary>
        /// 이 무공을 익힌 사람의 성향.
        /// ⚠ 강호무학은 무공에 성향이 없어 **정파로 친다** — 측정 통제를 위한 규약이다.
        /// </summary>
        public static Alignment OwnerAlignmentOf(MartialArt art)
        {
            if (art == null) throw new System.ArgumentNullException(nameof(art));
            return art.Alignment ?? Alignment.Orthodox;
        }

        /// <summary>경지에 닿는 데 필요한 수련 횟수. 성향마다 다르다.</summary>
        public static int SessionsForStage(Alignment owner, int stage)
        {
            return AlignmentCurve.SessionsToReach(owner, MartialStage.ProficiencyForStage(stage));
        }

        /// <summary>
        /// 무공의 **소속 문파에서 무학분류를 읽는다**(정의서 §6-4). 상성(§4)이 겨누는 과녁이다.
        ///
        /// ⚠⚠ 2026-08-02 신설. 그전에는 <see cref="Combatant"/> 에 분류를 담을 자리 자체가 없어서
        ///   상성 무공 4종(창천낙월·참천멸월·절해망혼·절지낙월)이 **대가만 치르고 보상을 못 받았다.**
        ///
        /// ⚠ **대형세력(무림맹·사도련·제천성·천마신교 연맹)은 `SchoolCatalog` 에 없어 `null` 이 된다.**
        ///   정의서 §6-4 의 분류표도 문파 16곳만 배정하고 대형세력은 비워 뒀다. 데이터가 없는 것을
        ///   여기서 지어내지 않는다 — 그래서 **`절지낙월`(무림맹)은 방어 상성만 얻고 공격 상성은
        ///   상대가 문파 소속일 때만 발동한다.** 이건 구현 누락이 아니라 **정의서의 빈칸**이다.
        /// </summary>
        public static ArtLineage? LineageOf(MartialArt art)
        {
            if (art == null) throw new System.ArgumentNullException(nameof(art));
            if (string.IsNullOrEmpty(art.School)) return null;

            School school = SchoolCatalog.ByName(art.School);
            return school == null ? (ArtLineage?)null : school.Lineage;
        }
    }
}
