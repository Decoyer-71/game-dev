using System;
using System.Collections.Generic;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;
using NUnit.Framework;

namespace Jianghu.Tests.Martial
{
    /// <summary>
    /// 무공명 → <see cref="MartialArt"/> 변환 검증 (설계안 §4 1단계 "엔진이 형태소 수치를 읽게 한다").
    ///
    /// ⚠⚠ **이 파일이 형태소 체계의 실증이다.** 정의서 §0 은 *"무공을 개별 데이터로 손으로 만들지 않고,
    ///   무공명 = 형태소 조합으로 수치를 자동 유도한다"* 고 선언했는데, 그 선언이 실제로 성립하는지를
    ///   여기서 확인한다 — **수치를 인자로 하나도 넘기지 않고** 무공이 만들어지는가.
    /// </summary>
    public class MartialArtFactoryTests
    {
        [Test]
        public void 수치를_넘기지_않아도_무공이_만들어진다()
        {
            // 넘기는 것은 이름·종류·계층·유형·성향뿐이다. **수치는 하나도 없다.**
            MartialArt art = MartialArtFactory.Create(
                "jc_changcheon", "창천낙월", ArtKind.Attack, ArtTier.Minor,
                Discipline.Sword, Alignment.Orthodox, school: "점창파");

            Assert.IsTrue(art.IsMorphemeDerived, "형태소 유도 무공으로 표시되지 않았다.");
            Assert.AreEqual("창천낙월", art.Name);

            // 창(槍 찌르기)의 값이 그대로 실려야 한다. 천·낙·월은 수치가 0 이다.
            //
            // ⚠⚠ **사전 값을 여기 박지 않는다**(2026-08-02 교정). 원래 `1.5`·`0.5` 를 상수로 적어 뒀는데,
            //   공격방식 속도를 재조정하자 이 테스트가 깨졌다 — 지뢰 목록의 *"테스트에 절대 수치를 박지
            //   말 것 · 값이 아니라 모양을 검증한다"* 가 실제로 터진 것이다. 이 테스트가 확인하려는 것은
            //   *"수치를 안 넘겨도 형태소에서 유도되는가"*(구조)이지 *"찌르기 속도가 정확히 0.5 인가"*(값)가
            //   아니므로, 사전을 조회해 **같은지**를 본다.
            Morpheme thrust;
            Assert.IsTrue(MorphemeDictionary.TryGet('창', out thrust), "사전에 창(槍)이 없다.");
            Assert.AreEqual(thrust.Delta.Attack, art.Delta.Attack, 1e-9, "공격이 형태소에서 유도되지 않았다.");
            Assert.AreEqual(thrust.Delta.Speed, art.Delta.Speed, 1e-9, "속도가 형태소에서 유도되지 않았다.");
            Assert.Greater(art.Delta.Attack, 0, "찌르기가 공격을 주지 않는다.");
        }

        [Test]
        public void 기력_소모도_이름에서_나온다()
        {
            // 본체 4자 − 배경어 1자(천) = 성능 3 → 3 × 4 = 12.
            MartialArt art = MartialArtFactory.Create(
                "jc_changcheon", "창천낙월", ArtKind.Attack, ArtTier.Minor,
                Discipline.Sword, Alignment.Orthodox, school: "점창파");

            Assert.AreEqual(3 * MorphemeParser.QiCostPerMorpheme, art.QiCost);
        }

        [Test]
        public void 강호무학은_접미사에서_종류를_얻는다()
        {
            // ⚠ 강호무학만 `kind` 를 무시하고 접미사로 판정한다. 그래서 엉뚱한 kind 를 넘겨도 통과한다.
            //   `~보`(경공)로 끝나므로 경공 무공으로 읽히고, 방어 형태소 피(避)가 필수를 채운다.
            MartialArt art = MartialArtFactory.Create(
                "w_pisin", "피신보", ArtKind.Attack, ArtTier.Wanderer,
                Discipline.Movement, alignment: null);

            Assert.IsTrue(art.IsMorphemeDerived);
            Assert.IsNull(art.Alignment, "강호무학은 성향이 없어야 한다 — 익힌 사람을 따른다.");
            Assert.AreEqual(15, art.Delta.Evasion, 1e-9, "피(避)의 회피 +15%p 가 유도되지 않았다.");
        }

        [Test]
        public void 조합_규칙을_어기면_만들어지지_않는다()
        {
            // `참자` = 공격방식 2자(참·자). 정의서 §2-2 규칙 2 위반.
            // ⚠ 조용히 넘기면 밸런스가 왜 이상한지 나중에 추적할 수 없다.
            Assert.Throws<ArgumentException>(() => MartialArtFactory.Create(
                "bad", "참자", ArtKind.Attack, ArtTier.Major,
                Discipline.Sword, Alignment.Orthodox, school: "화산파"));
        }

        [Test]
        public void 계층_제약이_생성_단계에서_걸린다()
        {
            // 극한경지는 전승무학 전용이다(정의서 §5-2). 대문파로 만들려 하면 걸려야 한다.
            MartialArt art;
            IReadOnlyList<string> problems;

            Assert.IsFalse(MartialArtFactory.TryCreate(
                "bad", "마한중참", ArtKind.Attack, ArtTier.Major,
                Discipline.Sword, Alignment.Demonic, "천마신교", 1, null, out art, out problems));
            Assert.IsNull(art);

            // 같은 이름이 전승무학으로는 통과한다.
            Assert.IsTrue(MartialArtFactory.TryCreate(
                "cm_legacy", "마한중참", ArtKind.Attack, ArtTier.Legacy,
                Discipline.Sword, Alignment.Demonic, "천마신교", 1, null, out art, out problems));
            Assert.IsNotNull(art);
        }

        [Test]
        public void 실패는_예외_대신_목록으로도_받을_수_있다()
        {
            // 역산 리포트(설계안 §5-2)를 위한 경로. 첫 실패에서 멈추면 "무엇이 잘못됐는가" 를 못 모은다.
            MartialArt art;
            IReadOnlyList<string> problems;

            Assert.IsFalse(MartialArtFactory.TryCreate(
                "bad", "복묘권법", ArtKind.Attack, ArtTier.Wanderer,
                Discipline.Fist, null, null, 1, null, out art, out problems));

            Assert.AreEqual(2, problems.Count, "미등록 글자 두 개가 각각 보고돼야 한다.");
        }

        [Test]
        public void 레거시_무공과_형태소_무공이_구분된다()
        {
            // ⚠ 과도기다. `MartialArtCatalog` 36종은 아직 손으로 박은 수치를 쓴다.
            //   섞이면 "이 무공 수치가 어디서 왔지" 를 추적할 수 없으므로 플래그로 가른다.
            MartialArt legacy = MartialArt.Technique(
                "old", "옛무공", Discipline.Sword, Alignment.Orthodox, basePower: 25, qiCost: 8);
            MartialArt derived = MartialArtFactory.Create(
                "new", "참정검법", ArtKind.Attack, ArtTier.Wanderer,
                Discipline.Sword, alignment: null);

            Assert.IsFalse(legacy.IsMorphemeDerived);
            Assert.IsTrue(derived.IsMorphemeDerived);
        }

        /// <summary>
        /// **범위 형태소가 무공까지 실려 온다** (2026-08-09 신설 · `MartialArt.Scope`).
        ///
        /// ⚠⚠ 그전까지 `MartialArtFactory` 가 `parsed.Scope` 를 **그냥 버렸다.** 2026-08-02 의
        ///   `CounterTargets` 누락과 **똑같은 형태**이고, 그 자리 주석이 *"AttackScope 는 아직
        ///   같은 상태로 남아 있다"* 고 스스로 적어 두고 있었다. 이 테스트가 그 회귀를 막는다.
        /// ⚠ 이 단계는 **값을 나르기만** 한다 — 전투 결과는 한 톨도 안 바뀐다(1대1은 상대가
        ///   하나뿐이라 읽을 곳이 없다). 실제로 쓰는 것은 다대다다(`docs/multi-combat-plan.md`).
        /// </summary>
        [Test]
        public void 범위_형태소가_무공의_Scope_로_실려_온다()
        {
            // 군(群) = 3인. 대문파 이상만 범위를 쓸 수 있다(정의서 §3-12 · ArtCompositionRule).
            MartialArt group = MartialArtFactory.Create(
                "g", "정천창군", ArtKind.Attack, ArtTier.Major,
                Discipline.Spear, Alignment.Orthodox, "소림사");
            Assert.AreEqual(AttackScope.Three, group.Scope,
                "군(群) 을 문 무공인데 Scope 가 안 왔다 — 팩토리가 parsed.Scope 를 또 버렸는지 볼 것.");

            // 범위 글자가 없으면 단일 대상이다.
            MartialArt single = MartialArtFactory.Create(
                "s", "참정검법", ArtKind.Attack, ArtTier.Wanderer,
                Discipline.Sword, alignment: null);
            Assert.AreEqual(AttackScope.Single, single.Scope,
                "범위 형태소가 없는데 Single 이 아니다 — 기본값이 샜다.");
        }
    }
}
