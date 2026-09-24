using System;
using System.Collections.Generic;
using Jianghu.Core.Characters;
using Jianghu.Core.Combat;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;
using NUnit.Framework;

namespace Jianghu.Tests.Combat
{
    /// <summary>
    /// **편성 불변식** — 정의서 §0-1 장착 규정 (2026-09-10 신설).
    ///
    /// 종류당 최대 하나. 공격 1 · 내공 1 · 경공 1 이고 **빈 슬롯을 허용**한다
    /// (게임 시작 시 플레이어는 무공을 하나도 안 익힌 상태다).
    ///
    /// ⚠⚠ **이 파일이 `SelectArtTests` 를 대체한다.** 그 파일은 *"공격 무공을 여럿 배웠을 때
    ///   배운 순서에 좌우되지 않는가"* 를 재는 회귀 테스트였는데(2026-08-05 신설),
    ///   **장착 규정이 그 버그 클래스를 구조적으로 도달 불가능하게 만들었다** — 공격 무공을
    ///   둘 장착할 수 없으므로 *"둘 중 무엇을 고르는가"* 라는 질문 자체가 성립하지 않는다.
    ///   픽스처를 만들 수 없어 지운 것이 아니라, **검사할 대상이 없어져서** 옮긴 것이다.
    ///   원 결함의 경위는 `CombatResolver.SelectArt` 주석과 `HANDOFF.md` §4-9-6 에 남아 있다.
    ///
    /// ⚠ 절대 수치를 박지 않는다. 검사하는 것은 **모양**이다.
    /// </summary>
    public class LoadoutTests
    {
        /// <summary>카탈로그에서 그 종류의 무공을 <paramref name="index"/> 번째로 집는다.</summary>
        private static LearnedArt Pick(ArtKind kind, int index = 0)
        {
            IReadOnlyList<MartialArt> all = MartialArtCatalog.All;
            int seen = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Discipline.KindOf() != kind) continue;
                if (seen++ < index) continue;
                return new LearnedArt(all[i], 0, Alignment.Orthodox);
            }
            Assert.Fail("카탈로그에 " + kind.ToKorean() + " 무공이 " + (index + 1) + "개 이상 있어야 한다.");
            return null;
        }

        [Test]
        public void 빈_편성이_허용된다()
        {
            // 게임 시작 시점의 정상 상태다. 여기서 던지면 첫 화면부터 못 만든다.
            Assert.IsTrue(Loadout.Empty.IsEmpty);
            Assert.AreEqual(0, Loadout.Empty.Equipped.Count);
            Assert.IsNull(Loadout.Empty.Attack);

            Combatant bare = new Combatant("허수아비", CharacterStats.MaxLevel(), Loadout.Empty);
            Assert.AreEqual(0, bare.Equipped.Count);
        }

        [Test]
        public void 종류별로_하나씩_장착된다()
        {
            LearnedArt atk = Pick(ArtKind.Attack);
            LearnedArt inr = Pick(ArtKind.Internal);
            LearnedArt stp = Pick(ArtKind.Movement);

            Loadout loadout = new Loadout(atk, inr, stp);

            Assert.AreSame(atk, loadout.Attack);
            Assert.AreSame(inr, loadout.Internal);
            Assert.AreSame(stp, loadout.Movement);
            Assert.AreEqual(3, loadout.Equipped.Count);
            Assert.IsFalse(loadout.IsEmpty);
        }

        [Test]
        public void 공격_무공을_둘_넘기면_거부한다()
        {
            // ⚠⚠ 이것이 옛 `SelectArtTests` 두 건을 대신한다. "순서에 좌우되는가" 를 물을 수 없는 것은
            //   결함이 고쳐져서가 아니라 **후보가 둘일 수 없어서**다.
            LearnedArt first = Pick(ArtKind.Attack, 0);
            LearnedArt second = Pick(ArtKind.Attack, 1);

            ArgumentException e = Assert.Throws<ArgumentException>(() => new Loadout(first, second));
            StringAssert.Contains("공격", e.Message);
            StringAssert.Contains(first.Art.Name, e.Message);
            StringAssert.Contains(second.Art.Name, e.Message);
        }

        [Test]
        public void 내공_무공을_둘_넘기면_거부한다()
        {
            Assert.Throws<ArgumentException>(
                () => new Loadout(Pick(ArtKind.Internal, 0), Pick(ArtKind.Internal, 1)));
        }

        [Test]
        public void 경공_무공을_둘_넘기면_거부한다()
        {
            Assert.Throws<ArgumentException>(
                () => new Loadout(Pick(ArtKind.Movement, 0), Pick(ArtKind.Movement, 1)));
        }

        [Test]
        public void null_을_넘기면_거부한다()
        {
            // 조용히 걸러내면 호출자는 자기가 무엇을 잃었는지 모른다.
            Assert.Throws<ArgumentNullException>(() => new Loadout(Pick(ArtKind.Attack), null));
        }

        [Test]
        public void 장착_순서가_보존된다()
        {
            LearnedArt atk = Pick(ArtKind.Attack);
            LearnedArt inr = Pick(ArtKind.Internal);

            Loadout ordered = new Loadout(inr, atk);

            Assert.AreSame(inr, ordered.Equipped[0], "받은 순서를 재정렬하면 안 된다.");
            Assert.AreSame(atk, ordered.Equipped[1]);
        }

        /// <summary>
        /// **접미사 사전이 종류와 유형을 어긋나게 정하지 않는가.**
        ///
        /// ⚠⚠ `Discipline.KindOf()` 는 유형에서 종류를 **되읽는다.** 런타임 <see cref="MartialArt"/> 가
        ///   <see cref="ArtKind"/> 를 안 들고 다니기 때문이다. 그 되읽기가 옳으려면 접미사 사전의
        ///   두 열이 **짝을 유지**해야 하는데, 지금은 사람이 손으로 짝지어 둔 것뿐이다.
        ///   → 어긋나는 항목이 하나라도 생기면 여기서 빨간불이 뜬다.
        /// </summary>
        [Test]
        public void 접미사사전의_종류와_유형이_어긋나지_않는다()
        {
            IReadOnlyList<ArtSuffix> suffixes = ArtSuffixCatalog.All;
            Assert.Greater(suffixes.Count, 0, "접미사 사전이 비어 있다.");

            for (int i = 0; i < suffixes.Count; i++)
            {
                ArtSuffix s = suffixes[i];
                if (!s.HasDiscipline) continue;   // ~봉법·~편법 은 유형 대응이 없다(설계상 의도)

                Assert.AreEqual(s.Kind, s.Discipline.KindOf(),
                    "접미사 '~" + s.Text + "' 의 종류(" + s.Kind + ")와 유형에서 되읽은 종류(" +
                    s.Discipline.KindOf() + ")가 어긋난다. KindOf 가 거짓말을 하게 된다.");
            }
        }

        /// <summary>카탈로그 138종 전부가 세 슬롯 중 정확히 하나에 들어가는가.</summary>
        [Test]
        public void 카탈로그의_모든_무공이_슬롯_하나에_들어간다()
        {
            IReadOnlyList<MartialArt> all = MartialArtCatalog.All;
            int attack = 0, inner = 0, movement = 0;

            for (int i = 0; i < all.Count; i++)
            {
                ArtKind kind = all[i].Discipline.KindOf();
                if (kind == ArtKind.Attack) attack++;
                else if (kind == ArtKind.Internal) inner++;
                else movement++;
            }

            Assert.AreEqual(all.Count, attack + inner + movement, "어느 슬롯에도 안 들어가는 무공이 있다.");
            Assert.Greater(attack, 0, "공격 무공이 없다.");
            Assert.Greater(inner, 0, "내공 무공이 없다.");
            Assert.Greater(movement, 0, "경공 무공이 없다.");
        }
    }
}
