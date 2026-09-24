using System.Collections.Generic;
using Jianghu.Core.Characters;
using Jianghu.Core.Combat;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;
using NUnit.Framework;

namespace Jianghu.Tests.EditMode.Combat
{
    /// <summary>
    /// **대전자 생성 규칙 회귀 테스트.**
    ///
    /// ⚠⚠ 이 규칙 셋은 원래 `Tools/Sandbox/Program.cs` 안의 **private 메서드**에만 있었다 —
    ///   즉 `dotnet test` 가 한 번도 닿은 적이 없다. 전투 화면이 같은 대전자를 만들어야 해서
    ///   Core 로 올렸고, 올린 김에 규칙을 여기 못박는다.
    ///
    /// ⚠ 이관은 **동작 무변경**이 조건이었고 `Sandbox --compare` 로 **바뀜 0건**을 확인했다.
    ///   이 파일은 *"앞으로 안 바뀌게"* 지키는 쪽이다.
    /// </summary>
    public class CombatantBuilderTests
    {
        private static MartialArt Find(string name)
        {
            foreach (MartialArt a in MartialArtCatalog.All) if (a.Name == name) return a;
            Assert.Fail("무공 '" + name + "' 이 카탈로그에 없다 — 이 테스트의 전제가 깨졌다");
            return null;
        }

        private static MartialArt FirstOf(ArtTier tier)
        {
            foreach (MartialArt a in MartialArtCatalog.All) if (a.Tier == tier) return a;
            Assert.Fail(tier + " 표본이 카탈로그에 없다");
            return null;
        }

        // ─────────────────────────── 성향 ───────────────────────────

        /// <summary>강호무학은 무공에 성향이 없다 — 익힌 사람이 필요하고, 측정에서는 정파로 친다.</summary>
        [Test]
        public void 강호무학은_정파로_친다()
        {
            MartialArt wanderer = FirstOf(ArtTier.Wanderer);
            Assert.That(wanderer.Alignment, Is.Null, "전제가 깨졌다 — 강호무학에 성향이 생겼다");
            Assert.That(CombatantBuilder.OwnerAlignmentOf(wanderer), Is.EqualTo(Alignment.Orthodox));

            Combatant c = CombatantBuilder.Build(wanderer, 10);
            Assert.That(c.Equipped[0].EffectiveAlignment, Is.EqualTo(Alignment.Orthodox));
        }

        /// <summary>성향이 있는 무공은 그 성향을 그대로 쓴다.</summary>
        [Test]
        public void 성향이_있으면_그것을_쓴다()
        {
            foreach (MartialArt art in MartialArtCatalog.All)
            {
                if (!art.Alignment.HasValue) continue;
                Assert.That(CombatantBuilder.OwnerAlignmentOf(art), Is.EqualTo(art.Alignment.Value), art.Name);
            }
        }

        // ─────────────────────────── 경지 → 수련 횟수 ───────────────────────────

        /// <summary>
        /// **같은 경지라도 성향마다 수련 횟수가 다르다.** 그래서 측정이 횟수가 아니라 경지를 지정한다.
        /// ⚠ 셋이 같아지면 이 규칙은 의미를 잃으므로 **다름 자체를 못박는다.**
        /// </summary>
        [Test]
        public void 같은_경지라도_성향마다_수련_횟수가_다르다()
        {
            int orthodox = CombatantBuilder.SessionsForStage(Alignment.Orthodox, 10);
            int unorthodox = CombatantBuilder.SessionsForStage(Alignment.Unorthodox, 10);
            int demonic = CombatantBuilder.SessionsForStage(Alignment.Demonic, 10);

            Assert.That(orthodox, Is.GreaterThan(0));
            Assert.That(new[] { orthodox, unorthodox, demonic }, Is.Unique,
                "세 성향의 10성 도달 횟수가 같다 — 경지로 지정하는 이유가 사라진다");
        }

        /// <summary>경지가 높을수록 수련 횟수가 는다.</summary>
        [Test]
        public void 경지가_높을수록_수련_횟수가_는다()
        {
            int low = CombatantBuilder.SessionsForStage(Alignment.Orthodox, 3);
            int high = CombatantBuilder.SessionsForStage(Alignment.Orthodox, 10);
            Assert.That(high, Is.GreaterThan(low));
        }

        // ─────────────────────────── 유형 숙달 ───────────────────────────

        /// <summary>
        /// **유형 숙달은 무공 경지와 무관하게 만렙 고정이다** (2026-07-31 사용자 교정).
        /// 두 축이 함께 움직이면 *"무공이 세진 것인지 사람이 세진 것인지"* 를 못 가른다.
        /// </summary>
        [Test]
        public void 유형_숙달은_경지와_무관하게_고정이다()
        {
            MartialArt art = Find("성뇌후격");

            Combatant low = CombatantBuilder.Build(art, 1);
            Combatant high = CombatantBuilder.Build(art, 10);

            Assert.That(low.Masteries.Count, Is.EqualTo(1));
            Assert.That(low.Masteries[0].Discipline, Is.EqualTo(art.Discipline));
            Assert.That(low.Masteries[0].TrainingSessions,
                Is.EqualTo(high.Masteries[0].TrainingSessions),
                "경지가 다른데 유형 숙달이 달라졌다 — 두 축이 다시 뭉쳤다");

            // 무공 숙련 쪽은 반대로 **경지를 따라 움직여야** 한다. 안 그러면 경지가 아무 일도 안 한다.
            Assert.That(low.Equipped[0].TrainingSessions, Is.LessThan(high.Equipped[0].TrainingSessions));
        }

        /// <summary>
        /// **주 무공의 유형 하나만** 만숙으로 둔다. 보조 무공의 유형은 넣지 않는다.
        /// ⚠ 옮겨 온 규칙 그대로다 — 바꾸면 기존 측정이 통째로 움직인다.
        /// </summary>
        [Test]
        public void 보조_무공의_유형은_숙달에_들어가지_않는다()
        {
            MartialArt attack = Find("성뇌후격");        // 권
            MartialArt support = Find("신풍양공");       // 내공
            Assert.That(attack.Discipline, Is.Not.EqualTo(support.Discipline), "전제가 깨졌다");

            Combatant c = CombatantBuilder.Build("A1", new List<MartialArt> { attack, support }, 10);

            Assert.That(c.Masteries.Count, Is.EqualTo(1));
            Assert.That(c.Masteries[0].Discipline, Is.EqualTo(attack.Discipline));
        }

        // ─────────────────────────── 무공 목록 ───────────────────────────

        /// <summary>보조 무공도 **같은 경지**로 들어간다. 주 무공만 올리면 보조가 변수에 딸려 흔들린다.</summary>
        [Test]
        public void 보조_무공도_같은_경지로_들어간다()
        {
            Combatant c = CombatantBuilder.Build(
                "A1", new List<MartialArt> { Find("성뇌후격"), Find("신풍양공") }, 6);

            Assert.That(c.Equipped.Count, Is.EqualTo(2));
            Assert.That(c.Equipped[1].TrainingSessions, Is.EqualTo(c.Equipped[0].TrainingSessions));
            Assert.That(c.Equipped[1].EffectiveAlignment, Is.EqualTo(c.Equipped[0].EffectiveAlignment));
        }

        /// <summary>
        /// **세 종류를 하나씩 들 수 있다** — 공격 1 · 내공 1 · 경공 1 (정의서 §0-1 장착 규정).
        ///
        /// ⚠⚠ **이 테스트는 `무공을_셋_이상_들_수_있다` 를 대체한다.** 그 테스트는
        ///   *"전투 화면이 `SelectArt` 를 다중 후보로 돌리려면 필요하다 — 후보 1개 맹점을 걷어내는
        ///   자리"* 를 근거로 삼았는데, **그 전제가 틀렸다.** 선택은 엔진이 아니라 **플레이어가
        ///   전투 전에** 한다(2026-09-10 사용자 확정). 후보를 여럿 만드는 것은 맹점을 걷어내는
        ///   것이 아니라 **규칙을 어기는 것**이다.
        ///   ⚠ 옛 표본(`성뇌후격`·`만우쾌사`)은 **둘 다 공격 무공**이라 지금은 만들 수조차 없다 —
        ///     아래 `같은_종류를_둘_주면_거부한다` 가 그것을 검사한다.
        /// </summary>
        [Test]
        public void 종류별로_하나씩_들_수_있다()
        {
            Combatant c = CombatantBuilder.Build(
                "A1",
                new List<MartialArt> { Find("성뇌후격"), Find("신풍양공"), Find("반신풍보") },
                10);

            Assert.That(c.Equipped.Count, Is.EqualTo(3));
            Assert.That(c.Loadout.Attack.Art.Name, Is.EqualTo("성뇌후격"));
            Assert.That(c.Loadout.Internal.Art.Name, Is.EqualTo("신풍양공"));
            Assert.That(c.Loadout.Movement.Art.Name, Is.EqualTo("반신풍보"));
        }

        /// <summary>
        /// **같은 종류를 둘 주면 거부한다** — 장착 규정이 빌더를 통해서도 강제된다.
        /// ⚠ 조용히 하나를 버리면 부르는 쪽은 자기가 무엇을 잃었는지 모른다.
        /// </summary>
        [Test]
        public void 같은_종류를_둘_주면_거부한다()
        {
            // 성뇌후격(권) · 만우쾌사(비도) — 유형은 다르지만 **둘 다 공격 무공**이다.
            System.ArgumentException e = Assert.Throws<System.ArgumentException>(
                () => CombatantBuilder.Build(
                    "A1", new List<MartialArt> { Find("성뇌후격"), Find("만우쾌사") }, 10));

            StringAssert.Contains("공격", e.Message);
        }

        /// <summary>무공이 하나도 없으면 만들 수 없다 — 조용히 넘기지 않는다.</summary>
        [Test]
        public void 무공이_없으면_예외다()
        {
            Assert.Throws<System.ArgumentException>(
                () => CombatantBuilder.Build("A1", new List<MartialArt>(), 10));
        }

        // ─────────────────────────── 이름 ───────────────────────────

        /// <summary>
        /// **이름을 부르는 쪽이 정한다.** 전투 로그가 이름 문자열로만 사람을 가리키므로
        /// (`CombatLogEntry`), 4대4 에서 같은 무공을 여럿이 들면 자리 이름이 필요하다.
        /// </summary>
        [Test]
        public void 이름을_넘기면_그_이름이_붙는다()
        {
            MartialArt art = Find("만우쾌사");

            Assert.That(CombatantBuilder.Build(art, 10).Name, Is.EqualTo(art.Name));
            Assert.That(CombatantBuilder.Build("A1 " + art.Name, new List<MartialArt> { art }, 10).Name,
                Is.EqualTo("A1 만우쾌사"));
        }

        // ─────────────────────────── 무학분류 ───────────────────────────

        /// <summary>문파가 있으면 그 문파의 무학분류를 쓴다.</summary>
        [Test]
        public void 문파에서_무학분류를_읽는다()
        {
            MartialArt art = Find("성뇌후격");           // 소림사
            School school = SchoolCatalog.ByName(art.School);
            Assert.That(school, Is.Not.Null, "전제가 깨졌다 — 소림사가 사라졌다");

            Assert.That(CombatantBuilder.LineageOf(art), Is.EqualTo(school.Lineage));
            Assert.That(CombatantBuilder.Build(art, 10).Lineage, Is.EqualTo(school.Lineage));
        }

        /// <summary>문파가 없는 무공(강호무학)은 분류가 없다.</summary>
        [Test]
        public void 문파가_없으면_무학분류도_없다()
        {
            MartialArt wanderer = FirstOf(ArtTier.Wanderer);
            Assert.That(wanderer.School, Is.Null.Or.Empty, "전제가 깨졌다");
            Assert.That(CombatantBuilder.LineageOf(wanderer), Is.Null);
        }

        /// <summary>
        /// **대형세력은 `SchoolCatalog` 에 없어 분류가 null 이다** — 정의서 §6-4 의 빈칸이지
        /// 구현 누락이 아니다.
        /// ⚠ 표본이 실제로 있는지 함께 확인한다. 0 이면 이 규칙은 검증되지 않는다.
        /// </summary>
        [Test]
        public void 대형세력은_분류가_비어_있다()
        {
            int found = 0;
            foreach (MartialArt art in MartialArtCatalog.All)
            {
                if (string.IsNullOrEmpty(art.School)) continue;
                if (SchoolCatalog.ByName(art.School) != null) continue;

                found++;
                Assert.That(CombatantBuilder.LineageOf(art), Is.Null, art.Name + " (" + art.School + ")");
            }
            Assert.That(found, Is.GreaterThan(0), "SchoolCatalog 에 없는 세력 표본이 하나도 없다");
        }

        // ─────────────────────────── 능력치 ───────────────────────────

        /// <summary>능력치를 안 주면 만렙이다. 주면 그것을 쓴다.</summary>
        [Test]
        public void 능력치는_기본이_만렙이고_넘기면_그것을_쓴다()
        {
            MartialArt art = Find("성뇌후격");

            Assert.That(CombatantBuilder.Build(art, 10).Stats.MaxHealth,
                Is.EqualTo(CharacterStats.MaxLevel().MaxHealth));

            CharacterStats starting = CharacterStats.Starting();
            Assert.That(CombatantBuilder.Build(art, 10, starting).Stats.MaxHealth,
                Is.EqualTo(starting.MaxHealth));
        }
    }
}
