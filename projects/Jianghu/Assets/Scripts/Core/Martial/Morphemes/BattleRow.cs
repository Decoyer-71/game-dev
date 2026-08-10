using System.Collections.Generic;

namespace Jianghu.Core.Martial.Morphemes
{
    /// <summary>
    /// 진형(陣形)의 열 — 전열 / 후열. 다대다 전투에서만 뜻이 있다(설계 <c>docs/multi-combat-plan.md</c> §D3-1).
    ///
    /// ⚠⚠ **사람의 속성이 아니라 그 전투에서의 배치다.** 그래서 <see cref="Combat.Combatant"/> 에 열 필드를
    ///   넣지 않고 <see cref="Combat.BattlePlacement"/> 로 전투에 들어갈 때 함께 넘긴다.
    ///
    /// ⚠ **2열인 이유** — *"보호받는가 / 노출되는가"* 라는 이진 결정이라 선명하다. 3열 이상이면
    ///   *"가장 가까이"* 의 단계가 늘어 조합이 폭발하고, 열을 가리키는 형태소도 열마다 필요해진다.
    /// ⚠ **전열이 전멸하면 후열이 곧 전열이 된다.** 그래서 전열을 비우는 배치는 *"모두 노출"* 을 뜻하지
    ///   배치를 무의미하게 만들지 않는다.
    /// ⚠ **1대1(<c>CombatResolver.Resolve</c>)에는 열이 없다**(§D3-8). 상대가 하나뿐이라 잴 것이 없다.
    /// </summary>
    public enum BattleRow
    {
        /// <summary>전열(前列) — 먼저 맞는 자리.</summary>
        Front = 0,

        /// <summary>후열(後列) — 전열이 살아 있는 동안 보호받는 자리.</summary>
        Rear = 1,
    }

    /// <summary>
    /// **무공이 어느 열에 닿는가** — 정의서에 없는 축을 새로 만들지 않고 **이미 있는 글자의 성질을 읽는다**
    /// (2026-08-09 사용자 지시 · 설계 §D3-3).
    ///
    /// > *"형태소를 별도로 만들지 말고 지금 있는 형태소 중에서 우선 후열공격, 우선 전열공격
    /// >  이런 식으로 반영하는 게 좋을 것 같다."*
    ///
    /// 열 성향을 갖는 것은 **둘뿐**이다:
    /// <list type="bullet">
    ///   <item><b>공격방식</b>(필수 1자) — 던지기만 후열, 베기·찌르기·때리기는 전열.
    ///     ⭐ *"어떻게 치는가"* 가 *"어디까지 닿는가"* 를 정한다 — 이름이 성능을 말한다(정의서 §0)</item>
    ///   <item><b>수식 어둡다</b>(야·암·한) — 후열. *"어둠을 틈타 뒤로 돈다."*</item>
    /// </list>
    ///
    /// ⛔⛔ **기만(환·궤)에는 주지 않는다.** 실측 세 안(A: 공격방식만 · B: +기만 · C: +어둡다) 중
    ///   **C 를 채택**했다(설계 §D3-3-b). 기만은 **명중 +2 인 이미 강한 형태소**라 후열까지 얹으면
    ///   중복 강화이고, 14종이 한 형태소에 몰려 *"기만 = 후열"* 이라는 단일 경로가 된다.
    ///   반대로 **어둡다는 지금 죽은 글자다** — 치명배율 +0.3 인데 기본 치명률이 10% 라 90% 의 타격에
    ///   안 닿아 민감도표에서 세 경지 전부 *"⚠ 무의미"* 였다. 후열 성향은 **죽은 축을 살리는** 쪽이다.
    ///   ⚠⚠ 다만 **이 저장소에 전례가 없는 시도다**(`verify` 5차). 지금까지 죽은 축을 살린 사례는 전부
    ///     *같은 축 안에서 수치를 조정*한 것이었고, **다른 메커닉을 겹쳐 얹은 적은 없다.**
    ///
    /// ⚠ **어둡다의 치명배율 +0.3 은 지우지 않는다.** 그대로 두고 후열 성향을 **더한다** —
    ///   치명 설계가 나중에 바뀌면 그 축이 살아날 수 있다.
    /// </summary>
    public static class BattleRowRule
    {
        /// <summary>
        /// 후열에 닿는 공격방식의 뜻. <see cref="MorphemeDictionary"/> 의 `투척포사` 행이다.
        ///
        /// ⚠⚠ **뜻(<see cref="Morpheme.Meaning"/>)으로 무리를 가른다.** 사전이 네 글자를 한 행으로 묶어
        ///   같은 뜻을 주므로 그것이 이 무리의 유일한 식별자다 — 글자를 하나씩 늘어놓으면 사전에
        ///   글자가 추가될 때 조용히 새는 목록이 하나 더 생긴다.
        /// ⚠ 뜻 문자열이 바뀌면 이 규칙이 **조용히 죽는다.** 그래서 사전에 이 뜻이 실재하는지를
        ///   회귀 테스트가 못박는다(`TeamCombatTests`).
        /// </summary>
        public const string ThrowingMeaning = "던지기";

        /// <summary>후열 성향을 갖는 수식의 뜻. `야암한` 행이다.</summary>
        public const string DarkMeaning = "어둡다";

        /// <summary>
        /// 이름 **앞에서부터** 훑어 열 성향을 가진 **첫 형태소**가 정한다 (설계 §D3-3-a, 2026-08-09 사용자 제안).
        ///
        /// > *"각각의 형태소가 붙게 되면 먼저 앞선 글자 형태소를 기준으로 적용하는 거지."*
        ///
        /// ⭐ **글자 순서에 처음으로 의미가 생긴다.** 지금까지 순서는 작명 관례일 뿐 아무것도 뜻하지 않았다.
        ///   같은 글자 조합으로 **전열기와 후열기를 둘 다** 지을 수 있게 된다.
        /// ⚠⚠ **대가 — 작명 관례가 규칙을 한 방향으로 고정한다.** 이름은 대체로
        ///   `[수식][무공형태][공격방식][상태이상]` 순이라, 수식에 열 성향을 주면 공격방식은
        ///   사실상 발동하지 않는다. 순서를 뒤집는 이름을 지어야 규칙이 양방향으로 산다.
        /// ⚠ 열 성향을 가진 글자가 하나도 없으면 **전열**이다 — 보조 무공(내공·경공)과
        ///   공격방식이 없는 이름이 여기 온다.
        ///
        /// ⚠⚠ <see cref="ParsedArtName.Scope"/>·<see cref="ParsedArtName.Rule"/> 은 **마지막** 것을 취하는데
        ///   여기만 **첫** 것을 취한다. 다른 규칙이 아니라 **다른 물음**이기 때문이다 — 저 둘은 카테고리당
        ///   1자 규칙에 막혀 애초에 둘 이상 나올 수 없고, 이쪽은 **둘 이상 나오는 것을 전제로** 우선순위를 정한다.
        /// </summary>
        public static BattleRow Of(IReadOnlyList<Morpheme> body)
        {
            if (body == null) return BattleRow.Front;

            for (int i = 0; i < body.Count; i++)
            {
                BattleRow row;
                if (TryRowOf(body[i], out row)) return row;
            }
            return BattleRow.Front;
        }

        /// <summary>형태소 하나의 열 성향. 성향이 없으면 false.</summary>
        public static bool TryRowOf(Morpheme morpheme, out BattleRow row)
        {
            row = BattleRow.Front;
            if (morpheme == null) return false;

            if (morpheme.Category == MorphemeCategory.AttackMethod)
            {
                row = morpheme.Meaning == ThrowingMeaning ? BattleRow.Rear : BattleRow.Front;
                return true;
            }

            if (morpheme.Category == MorphemeCategory.Modifier && morpheme.Meaning == DarkMeaning)
            {
                row = BattleRow.Rear;
                return true;
            }

            return false;
        }

        /// <summary>반대 열. 우선 열이 모자랄 때 넘어갈 곳이다(설계 §D4).</summary>
        public static BattleRow Opposite(BattleRow row)
        {
            return row == BattleRow.Front ? BattleRow.Rear : BattleRow.Front;
        }
    }
}
