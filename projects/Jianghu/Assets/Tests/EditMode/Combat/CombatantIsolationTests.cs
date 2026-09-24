using System;
using System.Collections.Generic;
using System.Reflection;
using Jianghu.Core.Characters;
using Jianghu.Core.Combat;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;
using NUnit.Framework;

namespace Jianghu.Tests.Combat
{
    /// <summary>
    /// **미장착 무공이 능력치를 주지 못하게 하는 장치가 살아 있는가** (2026-09-10 신설 · 정의서 §0-1).
    ///
    /// ⚠⚠ **원래 재려던 것은 여기서 잴 수 없다.** 설계 단계에서 계획한 불변식은
    ///   *"같은 편성을 든 두 사람은 다른 무공을 얼마나 더 익혔든 값이 같다"* 였는데,
    ///   <see cref="Combatant"/> 가 <see cref="Loadout"/> 만 받게 되면서 **"더 익힌 무공"을 담을
    ///   자리 자체가 사라졌다.** 그 픽스처를 만들 수 없다는 것이 곧 격리가 성립한다는 뜻이다.
    ///   → 그래서 이 파일은 **격리를 다시 뚫는 두 경로**를 대신 지킨다.
    ///
    /// ⚠ 리플렉션을 쓴다. 이 저장소에서 처음이다(선례 0건) — 축이 늘어나도 **테스트를 안 고쳐도
    ///   자동으로 검사 대상이 되게** 하려는 것이다. netstandard2.1 표준 API 이고 EditMode 는 Mono 라
    ///   AOT 스트리핑 문제도 없다.
    /// </summary>
    public class CombatantIsolationTests
    {
        private static LearnedArt Pick(ArtKind kind)
        {
            IReadOnlyList<MartialArt> all = MartialArtCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Discipline.KindOf() == kind)
                    return new LearnedArt(all[i], 0, Alignment.Orthodox);
            }
            Assert.Fail("카탈로그에 " + kind.ToKorean() + " 무공이 없다.");
            return null;
        }

        private static Combatant Make(Loadout loadout)
        {
            return new Combatant("표본", CharacterStats.MaxLevel(), loadout);
        }

        /// <summary>
        /// ⛔⛔ **원시 목록을 받는 생성자를 되살리면 그 순간 우회로가 된다.**
        ///
        /// 격리의 실체는 *"<see cref="Combatant"/> 가 익힌 무공 전부를 볼 수 없다"* 인데,
        /// `IReadOnlyList&lt;LearnedArt&gt;` 를 받는 생성자가 **하나라도** 있으면 호출자가
        /// 그 목록을 통째로 넘길 수 있다. 편의를 위해 오버로드를 하나 추가하는 것은
        /// 매우 자연스러운 변경이라 사람 눈으로는 안 걸린다 — 그래서 기계가 지킨다.
        /// </summary>
        [Test]
        public void 생성자가_무공_목록을_직접_받지_않는다()
        {
            ConstructorInfo[] ctors = typeof(Combatant).GetConstructors(
                BindingFlags.Public | BindingFlags.Instance);
            Assert.Greater(ctors.Length, 0, "공개 생성자가 하나도 없다 — 검사가 무의미해졌다.");

            for (int i = 0; i < ctors.Length; i++)
            {
                ParameterInfo[] ps = ctors[i].GetParameters();
                for (int j = 0; j < ps.Length; j++)
                {
                    Type t = ps[j].ParameterType;
                    bool takesArtList =
                        typeof(IEnumerable<LearnedArt>).IsAssignableFrom(t) ||
                        (t.IsArray && t.GetElementType() == typeof(LearnedArt));

                    Assert.IsFalse(takesArtList,
                        "Combatant 생성자가 무공 목록(" + t.Name + " " + ps[j].Name + ")을 직접 받는다. " +
                        "정의서 §0-1 격리가 뚫린다 — Loadout 으로만 받아야 한다.");
                }
            }
        }

        /// <summary>
        /// **파생 값 전부를 자동 열거해 편성 의존성을 확인한다.**
        ///
        /// ⚠⚠ `verify`(2026-09-10)가 잡은 구멍이 여기 반영돼 있다 — *"수치 프로퍼티"* 만 훑으면
        ///   <see cref="Combatant.IsStatusImmune"/> 등 **bool 4종**과 인자를 받는
        ///   <see cref="Combatant.CounterCountAgainst"/> 를 통째로 놓친다.
        ///   **정확히 그 다섯이 절대경지·상성 누수가 나타나는 자리다.** 그래서 bool 을 포함하고,
        ///   메서드는 <see cref="ArtLineage"/> 값마다 따로 호출해 비교한다.
        ///
        /// 검사 내용은 *"같은 편성이면 같은 값"* 이다. 트리비얼해 보이지만, 어떤 파생 값이
        /// 전역 상태·시간·난수에 기대게 되면 여기서 깨진다.
        /// </summary>
        [Test]
        public void 파생_값_전부가_편성에만_의존한다()
        {
            LearnedArt atk = Pick(ArtKind.Attack);
            LearnedArt inr = Pick(ArtKind.Internal);
            LearnedArt stp = Pick(ArtKind.Movement);

            // 같은 무공으로 만든 **서로 다른** 편성 인스턴스 둘.
            Combatant a = Make(new Loadout(atk, inr, stp));
            Combatant b = Make(new Loadout(atk, inr, stp));

            IReadOnlyList<PropertyInfo> axes = DerivedAxes();
            Assert.Greater(axes.Count, 8, "파생 축이 너무 적게 잡혔다 — 열거 필터가 좁아졌다.");

            for (int i = 0; i < axes.Count; i++)
            {
                object va = axes[i].GetValue(a);
                object vb = axes[i].GetValue(b);
                Assert.AreEqual(vb, va, "축 '" + axes[i].Name + "' 이 편성 외의 것에 의존한다.");
            }

            // 인자를 받는 축 — 상성. ArtLineage 전부 + 무소속(null).
            Array lineages = Enum.GetValues(typeof(ArtLineage));
            Assert.AreEqual(0, a.CounterCountAgainst(null), "무소속에는 상성이 성립하지 않는다.");
            for (int i = 0; i < lineages.Length; i++)
            {
                ArtLineage lineage = (ArtLineage)lineages.GetValue(i);
                Assert.AreEqual(b.CounterCountAgainst(lineage), a.CounterCountAgainst(lineage),
                    "상성(" + lineage + ")이 편성 외의 것에 의존한다.");
            }
        }

        /// <summary>
        /// **열거가 조용히 좁아지지 않는지 확인한다.**
        ///
        /// 위 테스트는 "잡힌 축이 전부 같다" 를 볼 뿐이라, **필터가 축을 놓치면 조용히 통과**한다.
        /// `verify` 가 지목한 다섯(bool 4 + 메서드 1)이 실제로 잡히는지 이름으로 못박는다.
        /// ⚠ 축을 지우거나 비공개로 바꾸면 여기서 걸린다 — 그때는 이 목록을 함께 고쳐야 한다.
        /// </summary>
        [Test]
        public void 열거가_bool_축과_상성_메서드를_빠뜨리지_않는다()
        {
            List<string> names = new List<string>();
            IReadOnlyList<PropertyInfo> axes = DerivedAxes();
            for (int i = 0; i < axes.Count; i++) names.Add(axes[i].Name);

            string[] mustHave =
            {
                "IsStatusImmune", "HasNoQiCost", "ActsTwice", "HasCounterSupremacy",
            };
            for (int i = 0; i < mustHave.Length; i++)
            {
                CollectionAssert.Contains(names, mustHave[i],
                    "bool 축 '" + mustHave[i] + "' 이 열거에서 빠졌다. 절대경지 누수가 여기서 난다.");
            }

            Assert.IsNotNull(
                typeof(Combatant).GetMethod("CounterCountAgainst", BindingFlags.Public | BindingFlags.Instance),
                "상성 메서드가 사라졌다 — 위 테스트의 상성 검사가 무의미해진다.");
        }

        /// <summary>
        /// <see cref="Combatant"/> 의 공개 파생 축 — **수치(int/double)와 bool 전부**.
        /// 인자 없는 읽기 프로퍼티만 잡는다. 새 축을 추가하면 자동으로 여기 들어온다.
        /// </summary>
        private static IReadOnlyList<PropertyInfo> DerivedAxes()
        {
            List<PropertyInfo> found = new List<PropertyInfo>();
            PropertyInfo[] props = typeof(Combatant).GetProperties(
                BindingFlags.Public | BindingFlags.Instance);

            for (int i = 0; i < props.Length; i++)
            {
                PropertyInfo p = props[i];
                if (!p.CanRead) continue;
                if (p.GetIndexParameters().Length > 0) continue;

                Type t = p.PropertyType;
                if (t == typeof(int) || t == typeof(double) || t == typeof(bool))
                    found.Add(p);
            }
            return found;
        }
    }
}
