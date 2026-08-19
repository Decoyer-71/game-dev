using System.Collections.Generic;
using System.Text;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;
using NUnit.Framework;

namespace Jianghu.Tests.Martial
{
    /// <summary>
    /// **이미 만들어진 무공을 다시 분해할 수 있는가** — <see cref="MartialArtFactory.Decompose"/> 검증
    /// (2026-08-09 신설).
    ///
    /// ⚠⚠ **왜 생겼나.** Phase 4 무공 상세 화면(*"이름의 어느 글자가 어느 수치를 넣었는가"*)을 설계하다
    ///   `verify` 가 잡았다 — `MorphemeParser.Parse(name)` 의 1인자 오버로드는 **접미사에서 종류를 읽는
    ///   강호무학 9종 전용**이라, 나머지 **129종은 `ArtKind` 를 명시하지 않으면 예외**가 난다.
    ///   그런데 `MartialArt` 가 자기 `Kind` 를 **보관하지 않고 있었다.**
    ///   → 즉 **카탈로그의 93%를 다시 분해할 수 없는 상태**였고, 화면을 짜다 런타임에 터졌을 것이다.
    ///
    /// ⚠ 그래서 이 파일이 보는 것은 *"몇 종이 되는가"* 가 아니라 **"138종 전부가 되는가"** 다.
    ///   표본 몇 개로 전수 결론을 내는 것이 이 저장소가 반복해서 밟은 실수다.
    /// </summary>
    public class MartialArtDecomposeTests
    {
        [Test]
        public void 카탈로그_138종을_전부_다시_분해할_수_있다()
        {
            var failures = new List<string>();
            IReadOnlyList<MartialArt> all = MartialArtCatalog.All;

            for (int i = 0; i < all.Count; i++)
            {
                MartialArt art = all[i];
                try
                {
                    ParsedArtName parsed = MartialArtFactory.Decompose(art);
                    if (parsed == null) failures.Add(art.Name + " — null 이 돌아왔다");
                }
                catch (System.Exception e)
                {
                    failures.Add(art.Name + "(" + art.Tier + "·" + art.Kind + ") — " + e.GetType().Name + ": " + e.Message);
                }
            }

            Assert.IsEmpty(failures, "다시 분해하지 못한 무공 " + failures.Count + "종:\n" + string.Join("\n", failures));
            Assert.AreEqual(138, all.Count, "카탈로그 종수가 138 이 아니다 — 이 시험의 전제가 바뀌었다");
        }

        [Test]
        public void 강호무학이_아닌_무공도_분해된다()
        {
            // ⚠⚠ **이 시험이 없으면 위 전수 시험이 통과해도 안심할 수 없다** — 목록 첫 항목만 눌러 보면
            //   강호무학 9종이라, 화면 스모크 테스트가 129종 버그를 그냥 지나친다.
            //   그래서 계층별로 **하나씩 못박는다.**
            var byTier = new Dictionary<ArtTier, MartialArt>();
            IReadOnlyList<MartialArt> all = MartialArtCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!byTier.ContainsKey(all[i].Tier)) byTier[all[i].Tier] = all[i];
            }

            Assert.GreaterOrEqual(byTier.Count, 4, "계층이 넷 미만이면 이 시험이 성립하지 않는다");
            foreach (KeyValuePair<ArtTier, MartialArt> kv in byTier)
            {
                Assert.DoesNotThrow(
                    () => MartialArtFactory.Decompose(kv.Value),
                    kv.Key + " 계층(" + kv.Value.Name + ")을 다시 분해하지 못했다");
            }
        }

        [Test]
        public void 다시_분해한_결과가_만들_때와_같다()
        {
            // ⚠ 분해가 되기만 하면 되는 것이 아니다. **만들 때 쓴 값과 같아야** 화면이 거짓말을 안 한다.
            IReadOnlyList<MartialArt> all = MartialArtCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                MartialArt art = all[i];
                ParsedArtName parsed = MartialArtFactory.Decompose(art);

                Assert.AreEqual(art.Kind, parsed.Kind, art.Name + " 의 종류가 달라졌다");
                Assert.AreEqual(art.Scope, parsed.Scope, art.Name + " 의 범위가 달라졌다");
                Assert.AreEqual(art.Rule, parsed.Rule, art.Name + " 의 절대경지 규칙이 달라졌다");
                Assert.AreEqual(art.PreferredRow, parsed.PreferredRow, art.Name + " 의 열이 달라졌다");
                Assert.AreEqual(art.QiCost, parsed.QiCost, art.Name + " 의 기력 소모가 달라졌다");
                Assert.AreEqual(art.Delta.Attack, parsed.Delta.Attack, 1e-9, art.Name + " 의 공격 합이 달라졌다");
            }
        }

        [Test]
        public void 분해_결과가_이름의_글자를_순서대로_덮는다()
        {
            // ⚠ 상세 화면은 **글자별로** 기여를 늘어놓는다. 그러려면 분해 결과가 이름의 글자를
            //   빠짐없이·순서대로 들고 있어야 한다.
            //
            // ⚠⚠ **처음에 `본체 == 이름` 으로 썼다가 틀렸다** — 접미사가 **강호무학 전용이 아니다.**
            //   `신풍양공`(소문파 내공)도 `공` 이 접미사로 떨어져 본체는 `신풍양` 3자다.
            //   내공은 `공`·`결`, 경공은 `보`·`술` 로 끝나는 것이 작명 관례이기 때문이다.
            //   → **상세 화면은 접미사 글자에 "형태소 아님" 을 표시해야 한다.** 수치가 없는 글자인데
            //     아무 표시가 없으면 사용자가 *"이 글자는 왜 기여가 없지"* 를 묻게 된다.
            IReadOnlyList<MartialArt> all = MartialArtCatalog.All;
            int withSuffix = 0;

            for (int i = 0; i < all.Count; i++)
            {
                MartialArt art = all[i];
                ParsedArtName parsed = MartialArtFactory.Decompose(art);

                var body = new StringBuilder();
                for (int j = 0; j < parsed.Body.Count; j++) body.Append(parsed.Body[j].Korean);

                Assert.IsTrue(art.Name.StartsWith(body.ToString()),
                    art.Name + " 의 본체(" + body + ")가 이름 앞부분과 다르다 — 글자가 새거나 뒤바뀐다");

                int rest = art.Name.Length - body.Length;
                if (parsed.HasSuffix)
                {
                    Assert.Greater(rest, 0, art.Name + " 은 접미사가 있다는데 남는 글자가 없다");
                    withSuffix++;
                }
                else
                {
                    Assert.AreEqual(0, rest, art.Name + " 은 접미사가 없다는데 글자가 남는다");
                }
            }

            Assert.Greater(withSuffix, 0, "접미사를 가진 무공이 하나도 없다 — 시험이 공허하다");
        }
    }
}
