using System.Collections.Generic;
using System.Text;
using Jianghu.Core.Combat;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Display;
using Jianghu.Core.Martial.Morphemes;
using NUnit.Framework;

namespace Jianghu.Tests.EditMode.Martial
{
    /// <summary>
    /// **표시 계층(`Display`) 회귀 테스트.** Phase 4 무공 목록 화면이 그리는 것을 여기서 검증한다.
    ///
    /// ⚠⚠ **이 테스트가 존재하는 이유**가 곧 표시 계층을 Core 에 둔 이유다. 첫 스모크 화면에서
    ///   ⓐ 축 하나만 찍혀 성(聖)이 무효과로 보이고 ⓑ enum 이 영문으로 새고 ⓒ 접미사 글자가
    ///   사라졌는데, Unity 층에 있으면 이 셋 중 **무엇도 `dotnet test` 로 잡히지 않는다.**
    ///   그래서 계산을 Core 로 내리고 그 자리에 이 파일을 붙였다.
    /// </summary>
    public class ArtBreakdownTests
    {
        private static IReadOnlyList<MartialArt> All => MartialArtCatalog.All;

        // ─────────────────────────── 전수: 이름을 삼키지 않는가 ───────────────────────────

        /// <summary>
        /// **항목을 이어붙이면 무공명이 그대로 나와야 한다.** 138종 전수.
        ///
        /// ⚠⚠ 글자 *수*가 아니라 **이어붙인 문자열**로 검사한다. 접미사가 2자(`신공`)일 수 있어
        ///   개수 비교로는 `신풍양공` 의 `공` 이 빠진 것을 못 잡는다 — 그게 정확히 스모크 화면이
        ///   낸 결함이다.
        /// </summary>
        [Test]
        public void 항목을_이어붙이면_무공명이_된다()
        {
            var broken = new List<string>();

            foreach (MartialArt art in All)
            {
                ArtBreakdown b = ArtBreakdown.Of(art);
                var sb = new StringBuilder();
                foreach (MorphemeContribution c in b.Characters) sb.Append(c.Text);

                if (sb.ToString() != art.Name)
                {
                    broken.Add(art.Name + " → '" + sb + "'");
                }
            }

            Assert.That(broken, Is.Empty, "이름을 온전히 덮지 못한 무공: " + string.Join(" · ", broken));
        }

        /// <summary>본체 형태소는 전부 1자다 — 접미사만 여러 자일 수 있다.</summary>
        [Test]
        public void 형태소_항목은_한_글자이고_접미사만_길_수_있다()
        {
            foreach (MartialArt art in All)
            {
                foreach (MorphemeContribution c in ArtBreakdown.Of(art).Characters)
                {
                    if (c.IsSuffix) continue;
                    Assert.That(c.Text.Length, Is.EqualTo(1), art.Name + " 의 '" + c.Text + "'");
                }
            }
        }

        /// <summary>접미사는 **맨 뒤에 하나**뿐이다.</summary>
        [Test]
        public void 접미사는_맨_뒤에_최대_하나다()
        {
            foreach (MartialArt art in All)
            {
                IReadOnlyList<MorphemeContribution> cs = ArtBreakdown.Of(art).Characters;
                for (int i = 0; i < cs.Count - 1; i++)
                {
                    Assert.That(cs[i].IsSuffix, Is.False, art.Name + " 의 " + i + "번 항목이 접미사다");
                }
            }
        }

        // ─────────────────────────── 전수: 0 인 축이 섞이지 않는가 ───────────────────────────

        /// <summary>
        /// **0 인 축은 목록에 없어야 한다.** 있으면 화면이 20여 개의 `0` 으로 진짜 기여를 덮는다.
        /// 글자별 · 합계 양쪽을 본다.
        /// </summary>
        [Test]
        public void 표시_축에_0_이_섞이지_않는다()
        {
            foreach (MartialArt art in All)
            {
                ArtBreakdown b = ArtBreakdown.Of(art);

                foreach (StatAxisValue v in b.Total)
                {
                    Assert.That(v.Value, Is.Not.EqualTo(0), art.Name + " 합계 '" + v.Name + "'");
                }
                foreach (MorphemeContribution c in b.Characters)
                {
                    foreach (StatAxisValue v in c.Axes)
                    {
                        Assert.That(v.Value, Is.Not.EqualTo(0), art.Name + " " + c.Text + " '" + v.Name + "'");
                    }
                }
            }
        }

        /// <summary>
        /// **글자별 기여를 다 더하면 합계가 된다.** 종(宗) 2배가 걸린 무공도 포함해서다 —
        /// 표시 계층이 파서와 **같은** <see cref="MorphemeParser.EffectiveDelta"/> 를 쓰기 때문이다.
        ///
        /// ⚠ 축 이름별로 더한다. 목록에서 0 이 빠져 있으므로 축 집합이 무공마다 다르다.
        /// </summary>
        [Test]
        public void 글자별_기여의_합이_합계와_같다()
        {
            foreach (MartialArt art in All)
            {
                ArtBreakdown b = ArtBreakdown.Of(art);

                var summed = new Dictionary<string, double>();
                foreach (MorphemeContribution c in b.Characters)
                {
                    foreach (StatAxisValue v in c.Axes)
                    {
                        double had;
                        summed[v.Name] = (summed.TryGetValue(v.Name, out had) ? had : 0) + v.Value;
                    }
                }

                foreach (StatAxisValue v in b.Total)
                {
                    double got;
                    Assert.That(summed.TryGetValue(v.Name, out got), Is.True,
                        art.Name + " 합계에 '" + v.Name + "' 가 있는데 글자별에는 없다");
                    Assert.That(got, Is.EqualTo(v.Value).Within(1e-9), art.Name + " '" + v.Name + "'");
                }

                // 반대 방향 — 글자별에만 있고 합계에 없는 축은 **상쇄돼 0 이 된 경우뿐**이어야 한다.
                foreach (KeyValuePair<string, double> kv in summed)
                {
                    if (kv.Value == 0) continue;
                    bool found = false;
                    foreach (StatAxisValue v in b.Total) if (v.Name == kv.Key) found = true;
                    Assert.That(found, Is.True, art.Name + " 글자별 '" + kv.Key + "' 가 합계에 없다");
                }
            }
        }

        // ─────────────────────────── enum 이 영문으로 새지 않는가 ───────────────────────────

        /// <summary>
        /// **화면에 나가는 이름에 ASCII 알파벳이 없어야 한다.** `Fist` · `Single` · `Front` 가
        /// 그대로 새던 것이 스모크 화면의 결함 ⓑ 였다.
        /// ⚠ 무공 `Id` 는 영문이지만 **화면에 안 나가므로** 검사 대상이 아니다.
        /// </summary>
        [Test]
        public void 표시용_이름에_영문_enum_이_새지_않는다()
        {
            foreach (MartialArt art in All)
            {
                ArtBreakdown b = ArtBreakdown.Of(art);
                AssertNoAscii(b.TierName, art.Name + " 계층");
                AssertNoAscii(b.DisciplineName, art.Name + " 유형");
                AssertNoAscii(b.AlignmentName, art.Name + " 성향");
                AssertNoAscii(b.KindName, art.Name + " 종류");
                AssertNoAscii(b.ScopeName, art.Name + " 범위");
                AssertNoAscii(b.RowName, art.Name + " 열");
                AssertNoAscii(b.LineageName, art.Name + " 분류");
                AssertNoAscii(b.RuleName, art.Name + " 규칙");

                foreach (MorphemeContribution c in b.Characters)
                {
                    AssertNoAscii(c.CategoryName, art.Name + " " + c.Text + " 카테고리");
                    foreach (StatAxisValue v in c.Axes) AssertNoAscii(v.Name, art.Name + " 축");
                }
            }
        }

        /// <summary>사전에 실린 카테고리는 전부 한글 이름을 갖는다 — `?` 도 영문도 아니어야 한다.</summary>
        [Test]
        public void 사전에_쓰인_카테고리는_전부_한글_이름을_갖는다()
        {
            foreach (Morpheme m in MorphemeDictionary.All)
            {
                string name = KoreanNames.Of(m.Category);
                Assert.That(name, Is.Not.EqualTo("?"), m.ToString());
                AssertNoAscii(name, m.ToString() + " 카테고리");
            }
        }

        // ─────────────────────────── 개별 확인 ───────────────────────────

        /// <summary>
        /// 성(聖)은 **공격이 0 이면서** 방어·막기·상태저항을 준다. 스모크 화면이 공격 축만 찍어
        /// *"기여 없는 글자"* 로 보이게 만들었던 바로 그 경우다.
        /// </summary>
        [Test]
        public void 공격이_0_인_글자도_자기_축을_드러낸다()
        {
            Morpheme saint = FindMorpheme('성');
            Assert.That(saint.Delta.Attack, Is.EqualTo(0), "전제가 깨졌다 — 성(聖)에 공격이 생겼다");

            IReadOnlyList<StatAxisValue> axes = ArtStatDeltaDisplay.NonZeroAxes(saint.Delta);
            Assert.That(axes, Is.Not.Empty, "성(聖)이 아무 축도 안 낸다");
        }

        /// <summary>
        /// **접미사 글자가 목록에 남는다.** `신풍양공` 의 `공` 이 사라지던 것이 결함 ⓒ 였다.
        /// ⚠ 수치는 주지 않으므로 축은 비어 있어야 한다.
        /// </summary>
        [Test]
        public void 접미사도_항목으로_나오고_수치는_주지_않는다()
        {
            MartialArt art = FindByName("신풍양공");
            ArtBreakdown b = ArtBreakdown.Of(art);

            MorphemeContribution last = b.Characters[b.Characters.Count - 1];
            Assert.That(last.IsSuffix, Is.True);
            Assert.That(last.Text, Is.EqualTo("공"));
            Assert.That(last.Axes, Is.Empty);
            Assert.That(last.CategoryName, Is.EqualTo("접미사"));

            // ⚠ *"형태소 아님"* 은 **의미 칸**에 있다 — 카테고리 칸에 넣었더니 표의 폭을 넘쳤다.
            Assert.That(last.Meaning, Does.Contain("형태소 아님"));
            Assert.That(last.Meaning, Does.StartWith("내공 무공"));
        }

        /// <summary>
        /// <see cref="ArtStatDelta.AttackPenalty"/> 는 이미 <see cref="ArtStatDelta.Attack"/> 안에 있다.
        /// 따로 찍으면 `환(−2)` 이 −4 로 읽힌다.
        /// </summary>
        [Test]
        public void 공격페널티는_따로_찍지_않는다()
        {
            ArtStatDelta d = ArtStatDelta.Of(attack: -2);
            Assert.That(d.AttackPenalty, Is.EqualTo(-2), "전제가 깨졌다");

            IReadOnlyList<StatAxisValue> axes = ArtStatDeltaDisplay.NonZeroAxes(d);
            Assert.That(axes.Count, Is.EqualTo(1), ArtStatDeltaDisplay.Join(axes));
            Assert.That(axes[0].Name, Is.EqualTo("공격"));
        }

        /// <summary>%p 와 % 를 섞지 않는다 — 정의서가 구분하는 것이라 화면도 구분해야 한다.</summary>
        [Test]
        public void 확률_단위와_배율_단위를_구분한다()
        {
            IReadOnlyList<StatAxisValue> axes = ArtStatDeltaDisplay.NonZeroAxes(
                ArtStatDelta.Of(evasion: 5, qiCostPercent: -30, critMultiplier: 0.3, paralysisStack: 1));

            Assert.That(Unit(axes, "회피율"), Is.EqualTo("%p"));
            Assert.That(Unit(axes, "기력소모"), Is.EqualTo("%"));
            Assert.That(Unit(axes, "치명배율"), Is.EqualTo("배"));
            Assert.That(Unit(axes, "마비"), Is.EqualTo("스택"));
        }

        /// <summary>
        /// **회피율·명중은 사전 값이 곧 %p 가 아니다** — 엔진이 상수를 곱한다. 화면이 그 환산을
        /// 거치지 않으면 `피·둔·섬 15` 가 `+15%p` 로 뜨는데 실제 효과는 **+4.5%p** 다(3.33배 과장).
        ///
        /// ⚠⚠ 상수를 여기 베끼지 않고 **엔진 상수를 참조해** 대조한다. 베끼면 저쪽이 움직였을 때
        ///   테스트가 옛 값을 지키며 통과해 버린다 — 그러면 이 테스트가 결함을 **가려 준다.**
        /// </summary>
        [Test]
        public void 회피율과_명중은_엔진_환산을_거쳐_표시된다()
        {
            foreach (Morpheme m in MorphemeDictionary.All)
            {
                IReadOnlyList<StatAxisValue> axes = ArtStatDeltaDisplay.NonZeroAxes(m.Delta);

                if (m.Delta.Evasion != 0)
                {
                    StatAxisValue v = Axis(axes, "회피율", m.ToString());
                    Assert.That(v.Value, Is.EqualTo(m.Delta.Evasion * Combatant.EvasionPointToPercent).Within(1e-9),
                        m + " 회피율");
                    Assert.That(v.Unit, Is.EqualTo("%p"));
                }

                if (m.Delta.Accuracy != 0)
                {
                    StatAxisValue v = Axis(axes, "명중", m.ToString());
                    Assert.That(v.Value, Is.EqualTo(m.Delta.Accuracy * CombatResolver.AccuracyPointToPercent).Within(1e-9),
                        m + " 명중");
                    Assert.That(v.Unit, Is.EqualTo("%p"));
                }
            }
        }

        /// <summary>
        /// 환산 상수가 1 이 아님을 못박는다 — 1 이 되면 위 테스트가 **아무것도 검증하지 않게** 된다.
        /// ⚠ 이 저장소는 *"표본에 그 조건이 있는가"* 를 안 물어 0 을 잘못 읽은 전례가 있다.
        /// </summary>
        [Test]
        public void 환산_상수는_1_이_아니라서_검증이_의미를_갖는다()
        {
            Assert.That(Combatant.EvasionPointToPercent, Is.Not.EqualTo(1.0));
            Assert.That(CombatResolver.AccuracyPointToPercent, Is.Not.EqualTo(1));

            Morpheme dodge = FindMorpheme('피');
            Assert.That(dodge.Delta.Evasion, Is.Not.EqualTo(0), "회피 형태소가 사전에 없다 — 표본이 비었다");
        }

        /// <summary>양수는 `+` 를 달고 음수는 그대로 둔다.</summary>
        [Test]
        public void 부호를_붙여_찍는다()
        {
            Assert.That(new StatAxisValue("공격", 1.5, "").Text, Is.EqualTo("+1.5"));
            Assert.That(new StatAxisValue("기력소모", -30, "%").Text, Is.EqualTo("-30%"));
        }

        /// <summary>강호무학은 무공 자체에 성향이 없다 — 빈 칸이 아니라 그 사실을 적는다.</summary>
        [Test]
        public void 강호무학의_성향은_익힌_사람을_따른다고_적는다()
        {
            foreach (MartialArt art in All)
            {
                if (art.Tier != ArtTier.Wanderer) continue;
                Assert.That(art.Alignment, Is.Null, art.Name);
                Assert.That(ArtBreakdown.Of(art).AlignmentName, Is.EqualTo("익힌 사람을 따름"));
                return;
            }
            Assert.Fail("강호무학이 카탈로그에 없다 — 이 테스트의 전제가 깨졌다");
        }

        // ─────────────────────────── 도우미 ───────────────────────────

        private static void AssertNoAscii(string text, string where)
        {
            Assert.That(text, Is.Not.Null.And.Not.Empty, where);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool ascii = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
                Assert.That(ascii, Is.False, where + " 에 영문이 샜다: '" + text + "'");
            }
        }

        private static StatAxisValue Axis(IReadOnlyList<StatAxisValue> axes, string name, string where)
        {
            foreach (StatAxisValue v in axes) if (v.Name == name) return v;
            Assert.Fail(where + " 에 축 '" + name + "' 이 없다");
            return default(StatAxisValue);
        }

        private static string Unit(IReadOnlyList<StatAxisValue> axes, string name)
        {
            foreach (StatAxisValue v in axes) if (v.Name == name) return v.Unit;
            Assert.Fail("축 '" + name + "' 이 없다");
            return null;
        }

        private static MartialArt FindByName(string name)
        {
            foreach (MartialArt a in All) if (a.Name == name) return a;
            Assert.Fail("무공 '" + name + "' 이 카탈로그에 없다");
            return null;
        }

        private static Morpheme FindMorpheme(char korean)
        {
            foreach (Morpheme m in MorphemeDictionary.All) if (m.Korean == korean) return m;
            Assert.Fail("형태소 '" + korean + "' 이 사전에 없다");
            return null;
        }
    }
}
