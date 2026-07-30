using System.Collections.Generic;
using Jianghu.Core.Martial.Morphemes;
using NUnit.Framework;

namespace Jianghu.Tests.Martial
{
    /// <summary>
    /// 형태소 사전 무결성 검증 (설계안 §5-1 중 1단계 해당분).
    ///
    /// ⚠ **여기는 다른 테스트와 성격이 다르다.** `AlignmentCurveTests` 는 절대 수치를 피하고 역전 순서만
    /// 보지만(수치가 미검증 초기값이라서), 이 사전은 **정의서를 옮겨 적은 것**이라 옮기다 틀린 것을
    /// 잡아내는 게 목적이다. 그래서 개수와 구조를 직접 확인한다.
    ///
    /// 다만 개별 수치를 일일이 박지는 않는다 — 정의서 수치는 승률표로 조정될 예정이므로,
    /// **조정해도 살아남는 구조**(카테고리 개수 · 대소 관계 · 페널티 유무)를 위주로 본다.
    /// 수치를 직접 박는 것은 정의서의 대표 예시 하나뿐이다.
    /// </summary>
    public class MorphemeDictionaryTests
    {
        // ─────────────────────────── 옮겨 적기 무결성 ───────────────────────────

        [Test]
        public void 형태소_사전은_여든한자다()
        {
            // ⚠ 정의서 §3 원안은 75자다. 두 번 늘었고 둘 다 정의서에 역반영했다:
            //   · 2026-07-29 상태이상 **탈(奪 기력소실)·경(硬 경직)** 2자 → 77자
            //     (엔진에 있는 상태이상을 형태소가 가리키지 못하던 구멍. `martial-art-naming.md` §4)
            //   · 2026-07-30 범위 **다(多)·군(群)·전(全)·만(萬)** 4자 → 81자
            //     (광역 공격. §3-12 신설. `martial-art-naming.md` §4-C)
            Assert.AreEqual(84, MorphemeDictionary.Count,
                "형태소는 84자여야 한다(정의서 75 + 상태이상 2 + 범위 4 + 종교 3). 실제 {0}자 — 옮기다 빠뜨렸거나 더 넣었다.",
                MorphemeDictionary.Count);
        }

        [Test]
        public void 종교_형태소는_기존_축의_복제가_아니다()
        {
            // ⚠⚠ 2026-07-30 사용자 결정 — *"새 조합으로 넣는 것으로 진행"*.
            //   `verify` 가 조사안에서 잡아낸 문제가 이것이었다: 혜(慧)=명중+2 는 적·확과, 오(悟)=치명률+10%p 는
            //   명·광·휘와 **수치가 완전히 같아** 글자를 고를 이유가 어감뿐이 된다.
            //   그러면 "이름에서 성능을 읽는다"(정의서 §0)가 희석된다. 그래서 셋 다 **없던 자리**에 넣었다.

            // 계(戒) — 방어군 5자는 전부 페널티가 없는데 계만 페널티를 갖는다.
            Morpheme gye = MorphemeDictionary.Get('계');
            Assert.AreEqual(MorphemeCategory.Defense, gye.Category);
            Assert.Less(gye.Delta.Speed, 0, "계(戒)에 페널티가 없다 — 그러면 막기군의 상위호환이 된다.");
            Assert.Greater(gye.Delta.BlockChance, MorphemeDictionary.Get('방').Delta.BlockChance,
                "계(戒)가 막기에 극단적으로 몰려 있지 않다.");

            // 현(玄) — 수식 11자는 전부 페널티가 없고, 회피 축을 가진 수식도 없었다. 두 겹으로 새 자리다.
            Morpheme hyeon = MorphemeDictionary.Get('현');
            Assert.AreEqual(MorphemeCategory.Modifier, hyeon.Category);
            Assert.Less(hyeon.Delta.Accuracy, 0, "현(玄)에 페널티가 없다.");
            foreach (Morpheme m in MorphemeDictionary.ByCategory(MorphemeCategory.Modifier))
            {
                if (m.Korean == '현') continue;
                Assert.AreEqual(0, m.Delta.Evasion, 1e-9, "수식 {0} 이 회피를 갖는다 — 현(玄)의 자리가 겹친다.", m);
            }

            // 식(息) — 내공 3자는 전부 기력의 '양'을 다루는데 식만 '소모율'을 다룬다.
            Morpheme sik = MorphemeDictionary.Get('식');
            Assert.AreEqual(MorphemeCategory.Internal, sik.Category);
            Assert.Less(sik.Delta.QiCostPercent, 0, "식(息)이 기력 소모를 줄이지 않는다.");
            foreach (Morpheme m in MorphemeDictionary.ByCategory(MorphemeCategory.Internal))
            {
                if (m.Korean == '식') continue;
                Assert.AreEqual(0, m.Delta.QiCostPercent, 1e-9, "내공 {0} 이 소모율을 갖는다 — 식(息)의 자리가 겹친다.", m);
            }
        }

        [Test]
        public void 종교_형태소는_극한경지_전용_축을_건드리지_않는다()
        {
            // ⚠⚠ `verify` 가 지적한 가장 중요한 위험이다. **방어무시는 마(魔)만, 상태이상 저항은 성(聖)만** 쓴다.
            //   정의서 §5-2 는 극한경지의 격을 "페널티가 없는 대신 계층·개수 제한" 으로 유지하는데,
            //   일반 형태소가 그 축을 가지면 **희소성이 깎인다.** 조사안의 허(虛)·인(忍)·연(蓮)이 그랬고,
            //   그래서 셋 다 채택하지 않았다.
            foreach (char c in new[] { '계', '현', '식' })
            {
                Morpheme m = MorphemeDictionary.Get(c);
                Assert.AreEqual(0, m.Delta.DefenseIgnore, 1e-9, "{0} 이 방어무시를 갖는다 — 극한경지 마(魔)의 자리다.", m);
                Assert.AreEqual(0, m.Delta.StatusResist, 1e-9, "{0} 이 상태이상 저항을 갖는다 — 극한경지 성(聖)의 자리다.", m);
            }
        }

        [Test]
        public void 카테고리별_개수가_정의서와_일치한다()
        {
            // 정의서 §3-1 ~ §3-10 의 소절별 글자 수. 합이 77 이다(상태이상만 원안 5 → 7).
            AssertCategoryCount(MorphemeCategory.AttackMethod, 14, "§3-1 공격 방식");
            AssertCategoryCount(MorphemeCategory.Defense, 12, "§3-2 방어 (원안 11 + 계戒)");
            AssertCategoryCount(MorphemeCategory.Internal, 4, "§3-3 내공 (원안 3 + 식息)");
            AssertCategoryCount(MorphemeCategory.Status, 7, "§3-4 상태이상 (원안 5 + 탈·경)");
            AssertCategoryCount(MorphemeCategory.Form, 9, "§3-5 무공형태");
            AssertCategoryCount(MorphemeCategory.Modifier, 12, "§3-6 수식 (원안 11 + 현玄)");
            AssertCategoryCount(MorphemeCategory.Element, 5, "§3-7 자연속성");
            AssertCategoryCount(MorphemeCategory.Pinnacle, 9, "§3-8 극한경지");
            AssertCategoryCount(MorphemeCategory.Negation, 5, "§3-9 부정");
            AssertCategoryCount(MorphemeCategory.Tag, 3, "§3-10 무학분류");
            AssertCategoryCount(MorphemeCategory.Scope, 4, "§3-12 범위 (2026-07-30 신설)");
        }

        [Test]
        public void 범위_형태소는_전부_대가를_치른다()
        {
            // ⚠⚠ 대가가 이 카테고리의 본체다. 대가 없는 범위 형태소가 하나라도 있으면
            //   다대다 전투가 생기는 순간 **모든 무공이 그 글자를 쓴다.**
            //   대가는 두 축 중 하나다 — 공격을 깎거나(다·군·전), 기력을 늘리거나(만).
            foreach (Morpheme m in MorphemeDictionary.ByCategory(MorphemeCategory.Scope))
            {
                bool paysInPower = m.Delta.Attack < 0;
                bool paysInQi = m.Delta.QiCostPercent > 0;

                Assert.IsTrue(paysInPower || paysInQi,
                    "범위 {0}({1}) 이 대가를 치르지 않는다 — 공격을 깎거나 기력을 늘려야 한다.", m, m.Meaning);
                Assert.AreNotEqual(AttackScope.Single, m.Scope, "범위 {0} 의 대상이 단일이다.", m);
            }
        }

        [Test]
        public void 전과_만은_같은_전원_타격이되_대가가_다르다()
        {
            // 전(全)과 만(萬)은 둘 다 "전원" 이라 대상 수로는 구분되지 않는다.
            // **대가의 축을 갈라** 성능이 아니라 성격으로 구분했다 — 이게 없으면 둘 중 하나는 죽은 글자다.
            Morpheme jeon = MorphemeDictionary.Get('전');
            Morpheme man = MorphemeDictionary.Get('만');

            Assert.AreEqual(AttackScope.All, jeon.Scope);
            Assert.AreEqual(AttackScope.All, man.Scope);

            Assert.Less(jeon.Delta.Attack, 0, "전(全)은 위력을 팔아야 한다.");
            Assert.AreEqual(0, jeon.Delta.QiCostPercent, 1e-9, "전(全)은 기력이 아니라 위력을 판다.");

            Assert.Greater(man.Delta.QiCostPercent, 0, "만(萬)은 기력을 팔아야 한다.");
            Assert.AreEqual(0, man.Delta.Attack, 1e-9, "만(萬)은 위력을 유지한다.");
        }

        [Test]
        public void 한글_표기가_겹치지_않는다()
        {
            // 정의서 §3-6 이 신(迅/神)·중(中/重)·명(命/明)·일(日/日) 네 건의 충돌을 정리했다.
            // 그 정리가 되살아나면 한 글자가 두 뜻을 갖게 되어 "이름에서 성능을 읽는다" 가 깨진다.
            var seen = new HashSet<char>();
            foreach (Morpheme m in MorphemeDictionary.All)
            {
                Assert.IsTrue(seen.Add(m.Korean), "한글 표기 '{0}' 이 사전에 두 번 나온다 ({1}).", m.Korean, m);
            }
            foreach (Morpheme m in MorphemeDictionary.BackgroundWords)
            {
                Assert.IsTrue(seen.Add(m.Korean), "배경어 '{0}' 이 형태소와 겹친다 ({1}).", m.Korean, m);
            }
        }

        // ─────────────────────────── 구조 (수치가 조정돼도 살아남아야 하는 것) ───────────────────────────

        [Test]
        public void 공격방식은_위력이_클수록_느리다()
        {
            // 정의서 §3-1 의 설계 의도 — 베기(+2/0) → 찌르기(+1.5/+0.5) → 때리기(+1/+1) → 던지기(+0.5/+1.5).
            // 위력과 속도를 맞바꾸는 구조이며, 수치를 조정하더라도 이 방향은 유지돼야 한다.
            Morpheme slash = MorphemeDictionary.Get('참');
            Morpheme thrust = MorphemeDictionary.Get('자');
            Morpheme strike = MorphemeDictionary.Get('타');
            Morpheme throwing = MorphemeDictionary.Get('투');

            Assert.Greater(slash.Delta.Attack, thrust.Delta.Attack, "베기가 찌르기보다 약하다.");
            Assert.Greater(thrust.Delta.Attack, strike.Delta.Attack, "찌르기가 때리기보다 약하다.");
            Assert.Greater(strike.Delta.Attack, throwing.Delta.Attack, "때리기가 던지기보다 약하다.");

            Assert.Less(slash.Delta.Speed, thrust.Delta.Speed, "베기가 찌르기보다 빠르다.");
            Assert.Less(thrust.Delta.Speed, strike.Delta.Speed, "찌르기가 때리기보다 빠르다.");
            Assert.Less(strike.Delta.Speed, throwing.Delta.Speed, "때리기가 던지기보다 빠르다.");
        }

        [Test]
        public void 무공형태_아홉자는_모두_페널티를_갖는다()
        {
            // ⚠⚠ 정의서 §2-2 의 핵심 주장이다 — 무공형태 5종은 전부 페널티가 있고, 그래서
            //   페널티 없는 상위호환(베기 = 공격+2)이 다른 카테고리에 존재한다. 필수로 강제해야만
            //   페널티가 "열등함이 아니라 성격" 이 된다. 페널티가 사라지면 그 논리가 무너진다.
            foreach (Morpheme m in MorphemeDictionary.ByCategory(MorphemeCategory.Form))
            {
                bool hasPenalty = m.Delta.Attack < 0 || m.Delta.Accuracy < 0 || m.Delta.Speed < 0;
                Assert.IsTrue(hasPenalty, "무공형태 {0}({1}) 에 페널티가 없다 — 상위호환이 되어버린다.", m, m.Meaning);
            }
        }

        [Test]
        public void 극한경지_아홉자는_페널티가_없다()
        {
            // 정의서 §5-2 — 극한경지 9자는 유일하게 페널티가 없다. 대신 전승무학 전용 · 무공당 1자로 막는다.
            // ⚠ 기력소모(QiCostPercent)만 예외로 둔다. 선(仙)의 −30% 는 페널티가 아니라 이득이다.
            foreach (Morpheme m in MorphemeDictionary.ByCategory(MorphemeCategory.Pinnacle))
            {
                ArtStatDelta d = m.Delta;
                Assert.GreaterOrEqual(d.Attack, 0, "극한경지 {0} 의 공격이 음수다.", m);
                Assert.GreaterOrEqual(d.Defense, 0, "극한경지 {0} 의 방어가 음수다.", m);
                Assert.GreaterOrEqual(d.Accuracy, 0, "극한경지 {0} 의 명중이 음수다.", m);
                Assert.GreaterOrEqual(d.Speed, 0, "극한경지 {0} 의 속도가 음수다.", m);
                Assert.GreaterOrEqual(d.MaxQi, 0, "극한경지 {0} 의 최대기력이 음수다.", m);
                Assert.GreaterOrEqual(d.QiRegen, 0, "극한경지 {0} 의 기력회복이 음수다.", m);
                Assert.LessOrEqual(d.QiCostPercent, 0, "극한경지 {0} 이 기력을 더 쓴다 — 페널티다.", m);
            }
        }

        [Test]
        public void 부정과_무학분류는_자체_수치가_없다()
        {
            // 정의서 §3-9 · §3-10. 이 여덟 자는 상성 규칙(§4)에서만 일한다.
            // 수치가 붙으면 "부정 한자를 넣으면 그냥 이득" 이 되어 상성 설계가 무너진다.
            foreach (Morpheme m in MorphemeDictionary.ByCategory(MorphemeCategory.Negation))
            {
                Assert.IsTrue(m.Delta.IsZero, "부정 {0} 에 수치가 붙어 있다.", m);
                Assert.IsTrue(m.IsNegation, "부정 {0} 에 부정 플래그가 없다.", m);
            }
            foreach (Morpheme m in MorphemeDictionary.ByCategory(MorphemeCategory.Tag))
            {
                Assert.IsTrue(m.Delta.IsZero, "무학분류 {0} 에 수치가 붙어 있다.", m);
                Assert.AreNotEqual(ArtLineage.None, m.Lineage, "무학분류 {0} 에 분류 태그가 없다.", m);
            }
        }

        [Test]
        public void 종주만_무공형태_효과를_두배로_만든다()
        {
            // 정의서 §3-8 — 종(宗)만 수치가 아니라 규칙을 만진다. 이 플래그가 번지면
            // "이름에서 성능을 읽는다" 가 아니라 "숨은 규칙을 외운다" 가 된다.
            int count = 0;
            foreach (Morpheme m in MorphemeDictionary.All)
            {
                if (m.DoublesFormEffect)
                {
                    count++;
                    Assert.AreEqual('종', m.Korean, "종(宗) 이 아닌 {0} 이 무공형태 2배 규칙을 갖고 있다.", m);
                }
            }
            Assert.AreEqual(1, count, "무공형태 2배 규칙을 가진 글자가 {0} 개다. 종(宗) 하나여야 한다.", count);
        }

        // ─────────────────────────── 합산 ───────────────────────────

        [Test]
        public void 신환보의_본체가_정의서_수치를_낸다()
        {
            // 정의서 §0 의 대표 예시 — 신환보 = 신(빠르다 속도+2) + 환(기만 명중+2/공격−2) + 보(경공 접미사).
            // ⚠ 이 테스트만 수치를 직접 박는다. 정의서가 이 예시로 시스템 전체를 설명하고 있어
            //   여기서 어긋나면 정의서 §0 이 거짓이 되기 때문이다.
            ArtStatDelta sum = MorphemeDictionary.Get('신').Delta + MorphemeDictionary.Get('환').Delta;

            Assert.AreEqual(2, sum.Speed, 1e-9, "신환보의 속도가 +2 가 아니다.");
            Assert.AreEqual(2, sum.Accuracy, 1e-9, "신환보의 명중이 +2 가 아니다.");
            Assert.AreEqual(-2, sum.Attack, 1e-9, "신환보의 공격이 −2 가 아니다.");
        }

        [Test]
        public void 델타는_축별로_합산된다()
        {
            // 이 시스템 전체가 이 연산 하나 위에 서 있다 — 무공 122개의 수치가 여기서 나온다.
            ArtStatDelta a = ArtStatDelta.Of(attack: 1.5, speed: 0.5, critChance: 10);
            ArtStatDelta b = ArtStatDelta.Of(attack: -2, speed: 2, defense: 3);
            ArtStatDelta sum = a + b;

            Assert.AreEqual(-0.5, sum.Attack, 1e-9, "공격이 합산되지 않았다.");
            Assert.AreEqual(2.5, sum.Speed, 1e-9, "속도가 합산되지 않았다.");
            Assert.AreEqual(3, sum.Defense, 1e-9, "한쪽에만 있는 축이 누락됐다.");
            Assert.AreEqual(10, sum.CritChance, 1e-9, "한쪽에만 있는 축이 누락됐다.");
        }

        [Test]
        public void 공격_합은_음수가_될_수_있다()
        {
            // ⚠ 설계안 §1-E — `사(던지기 +0.5)` + `환(기만 −2)` = 공격 −1.5 이고, 이건 §2-2 상 적법한 2자 무공이다.
            //   여기서 미리 0 으로 잘라내지 않는다. 잘라내면 §5-4 민감도 측정의 대상 자체가 사라진다.
            //   실제로 문제인지는 측정으로 판정한다.
            ArtStatDelta sum = MorphemeDictionary.Get('사').Delta + MorphemeDictionary.Get('환').Delta;

            Assert.Less(sum.Attack, 0,
                "공격 합이 음수가 되지 않는다. 어딘가에서 하한이 걸렸다면 설계안 §1-E 측정이 불가능해진다.");
        }

        // ─────────────────────────── 배경어 규제 (R1 · R4) ───────────────────────────

        [Test]
        public void 배경어는_수치를_갖지_않는다()
        {
            // 결정 A — 배경어는 성능도 비용도 0 인 순수 중립 글자다.
            foreach (Morpheme m in MorphemeDictionary.BackgroundWords)
            {
                Assert.IsTrue(m.Delta.IsZero, "배경어 {0} 에 수치가 붙어 있다 — 그러면 배경어가 아니라 형태소다.", m);
                Assert.IsTrue(m.IsBackground, "배경어 {0} 의 카테고리가 Background 가 아니다.", m);
            }
        }

        [Test]
        public void R1_미등록_글자는_배경어로_삼켜지지_않는다()
        {
            // ⚠⚠ **배경어 규제의 핵심 테스트다.** 이게 깨지면 파서가 모르는 글자를 전부 조용히 삼키고,
            //   ⓐ 오타가 검출되지 않고 ⓑ 사전에 추가했어야 할 형태소가 묻히며
            //   ⓒ 역산 리포트(설계안 §5-2)가 통째로 무의미해진다.
            //   정의서가 75자를 공들여 고른 이유가 거기서 증발한다.
            Morpheme found;

            Assert.IsFalse(MorphemeDictionary.TryGet('복', out found),
                "미등록 글자 '복'(伏) 이 사전에서 찾혔다. 배경어 화이트리스트가 새고 있다.");
            Assert.IsFalse(MorphemeDictionary.Contains('묘'),
                "미등록 글자 '묘' 가 사전에 있다.");
            Assert.Throws<System.ArgumentException>(() => MorphemeDictionary.Get('복'),
                "미등록 글자를 Get 했는데 예외가 나지 않는다.");
        }

        [Test]
        public void 중은_무공형태이지_배경어가_아니다()
        {
            // ⚠⚠ 2026-07-29 구현 중 발견 — 설계안은 초기 배경어를 천·중 2자로 잡았으나 **중은 불가능하다.**
            //   한글 '중' 은 이미 무공형태 중(重, 무거움)이 차지하고 있다(정의서 §3-5).
            //   사전의 키는 한자가 아니라 한글이므로, 중을 배경어로 등록하면 중(重)을 가리게 된다.
            //   정의서 §3-6 이 "중 | 맞히다 中 | 무공형태 重 | 맞히다에서 제외" 로 이미 정리한 충돌이
            //   배경어로 되살리려는 순간 그대로 돌아온 것이다.
            Morpheme jung = MorphemeDictionary.Get('중');

            Assert.AreEqual(MorphemeCategory.Form, jung.Category, "중이 무공형태가 아니다.");
            Assert.AreEqual('重', jung.Hanja, "중의 한자가 重 이 아니다.");
            Assert.IsFalse(jung.IsBackground, "중이 배경어로 등록됐다 — 무공형태 '무거움' 이 사라진다.");
        }

        [Test]
        public void 배경어_사전은_최소로_유지된다()
        {
            // R4 — 배경어 추가는 언제나 차선책이다. 사전이 슬금슬금 부풀면 R1 이 무력화된다.
            // ⚠⚠ 이 테스트는 "늘리지 말라" 가 아니라 **"늘릴 때 의식하고 늘려라"** 는 장치다.
            //   **정확히 같은 수를 요구하는 것이 의도다** — 한 자라도 늘리면 이 테스트가 깨지고,
            //   그때 R4 절차("이 글자는 정말 수치가 없어야 하는가")를 밟았는지 되묻게 된다.
            //   근거는 `docs/martial-art-naming.md` §4-B 에 남긴다.
            Assert.AreEqual(5, MorphemeDictionary.BackgroundCount,
                "배경어가 {0} 자다. 늘렸다면 R4 절차를 밟았는지 확인하고 이 수를 함께 고칠 것.",
                MorphemeDictionary.BackgroundCount);
        }

        [Test]
        public void 배경어는_목적어가_될_수_있는_명사다()
        {
            // 배경어의 존재 이유는 **문장형 무공명의 목적어**다 — `창천낙월`(하늘을 찔러 달을 떨어뜨린다).
            // 그래서 전부 명사여야 하고, 수치를 붙일 축이 있으면 형태소이지 배경어가 아니다.
            var expected = new Dictionary<char, char> { { '천', '天' }, { '지', '地' }, { '해', '海' }, { '운', '雲' }, { '몽', '夢' } };

            foreach (Morpheme m in MorphemeDictionary.BackgroundWords)
            {
                Assert.IsTrue(expected.ContainsKey(m.Korean), "예상하지 않은 배경어 {0} 가 등록됐다.", m);
                Assert.AreEqual(expected[m.Korean], m.Hanja, "배경어 {0} 의 한자가 다르다.", m);
            }
        }

        [Test]
        public void 광은_배경어나_신규_형태소가_될_수_없다()
        {
            // ⚠⚠ 2026-07-30 확인 — 광역 공격 형태소 후보로 '광'(廣)이 제안됐으나 **불가능하다.**
            //   한글 '광' 은 이미 수식 광(光, 밝다 = 치명률 +10%p)이 차지하고 있다.
            //   사전의 키는 한자가 아니라 한글이므로 한자가 달라도 쓸 수 없다.
            //   같은 이유로 성(星)·공(空)·산(山)도 불가능하다 — 성(聖)·공(功 접미사)·산(散)이 선점했다.
            Morpheme gwang = MorphemeDictionary.Get('광');

            Assert.AreEqual(MorphemeCategory.Modifier, gwang.Category);
            Assert.AreEqual('光', gwang.Hanja);
            Assert.IsFalse(gwang.IsBackground);
        }

        // ─────────────────────────── 도우미 ───────────────────────────

        private static void AssertCategoryCount(MorphemeCategory category, int expected, string section)
        {
            int actual = MorphemeDictionary.ByCategory(category).Count;
            Assert.AreEqual(expected, actual, "{0} 은 {1}자여야 하는데 {2}자다.", section, expected, actual);
        }
    }
}
