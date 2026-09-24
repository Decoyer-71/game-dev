using System;
using System.Collections.Generic;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Morphemes;

namespace Jianghu.Core.Combat
{
    /// <summary>
    /// **편성(編成) — 이번 전투에 들고 나가는 무공.** 정의서 §0-1 장착 규정의 실체다.
    ///
    /// 종류(<see cref="ArtKind"/>)당 **최대 하나**. 공격 1 · 내공 1 · 경공 1 이고,
    /// **빈 슬롯을 허용한다** — 게임 시작 시 플레이어는 무공을 하나도 안 익힌 상태다.
    ///
    /// ⚠⚠ **이 타입의 존재 이유는 "안 고른 무공이 능력치를 주는 것"을 구조로 막는 것이다.**
    ///   그전 <see cref="Combatant"/> 는 *익힌 무공 전부*를 받아 파생 능력치 13곳이 그것을 훑었다.
    ///   그래서 **한 번도 안 내보낸 무공을 들고만 있어도 승률이 47.0% → 56.0%(+9%p)** 올랐다
    ///   (4대4 · 10성 · 500판 실측). 그 무공의 창(槍)이 주는 속도 +1 이 그대로 붙은 것이다.
    ///   ⚠ 대가는 안 붙었다 — 기력소모 +200% 는 *그 무공을 쓸 때만* 읽히므로
    ///     **이득만 새고 대가는 안 새는 비대칭**이었다.
    ///
    /// ⚠⚠ **막는 방식이 "규율" 이 아니라 "가시성" 이다.** <see cref="Combatant"/> 안에는
    ///   미장착 무공이 **존재하지 않는다.** 그러므로 앞으로 어떤 파생 능력치를 새로 짜도
    ///   미장착 무공에 닿을 수가 없다 — 기억해서 거르는 것이 아니라 **닿을 대상이 없다.**
    ///   (`../../CLAUDE.md` §1 의 *"실수를 사람 기억에 맡기지 않는다"* 와 같은 선이다.)
    ///
    /// ⚠ **"익힌 무공 전부 합산" 규칙을 뒤집는 것이 아니다.** 정의서 §4(방어 상성)와
    ///   §5-3(절대경지 `any`)의 폴딩 방식은 그대로이고, **합산 대상만** *익힌 것 전부* 에서
    ///   *장착한 것* 으로 좁아진다. ⛔ 이것을 *"지금 쓰는 공격 초식 1개"* 로 더 좁히면 두 규칙이 깨진다.
    /// </summary>
    public sealed class Loadout
    {
        private static readonly LearnedArt[] None = new LearnedArt[0];

        /// <summary>아무것도 장착하지 않은 편성. 게임 시작 시점의 정상 상태다.</summary>
        public static readonly Loadout Empty = new Loadout();

        /// <summary>공격 슬롯. 실제로 초식을 내는 무공이다. 비어 있으면 맨손으로 싸운다.</summary>
        public LearnedArt Attack { get; }

        /// <summary>내공 슬롯. 스스로 공격하지 않고 능력치로만 일한다. 절대경지 4종이 전부 여기 붙는다.</summary>
        public LearnedArt Internal { get; }

        /// <summary>경공 슬롯. 〃</summary>
        public LearnedArt Movement { get; }

        /// <summary>
        /// 실제로 장착된 것만, **넘겨받은 순서 그대로**.
        ///
        /// ⚠ 슬롯 순서로 재정렬하지 않는다. <see cref="CombatResolver"/> 의 초식 선택이
        ///   *"동점이면 목록 순서상 앞선 것"* 을 쓰므로, 순서를 바꾸면 기존 측정과 갈라질 여지가 생긴다.
        ///   (공격 무공이 최대 1개라 실제로는 동점이 날 수 없지만, **바꿀 이유가 없는 것은 안 바꾼다.**)
        /// </summary>
        public IReadOnlyList<LearnedArt> Equipped { get; }

        /// <summary>
        /// 편성을 만든다. **같은 종류를 둘 넘기면 그 자리에서 던진다** — 조용히 하나를 버리면
        /// 호출자는 자기가 무엇을 잃었는지 모른다.
        /// </summary>
        /// <exception cref="ArgumentException">같은 종류의 무공이 둘 이상일 때.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="arts"/> 안에 null 이 있을 때.</exception>
        public Loadout(params LearnedArt[] arts)
        {
            if (arts == null || arts.Length == 0)
            {
                Equipped = None;
                return;
            }

            for (int i = 0; i < arts.Length; i++)
            {
                LearnedArt learned = arts[i];
                if (learned == null)
                    throw new ArgumentNullException(nameof(arts), "편성에 null 을 넣을 수 없다. 빈 슬롯은 아예 안 넘기면 된다.");

                ArtKind kind = learned.Art.Discipline.KindOf();
                LearnedArt taken = SlotOf(kind);
                if (taken != null)
                {
                    throw new ArgumentException(
                        string.Format(
                            "{0} 슬롯은 하나뿐인데 둘을 넘겼다 — '{1}' 와 '{2}'. 정의서 §0-1 장착 규정.",
                            kind.ToKorean(), taken.Art.Name, learned.Art.Name),
                        nameof(arts));
                }

                if (kind == ArtKind.Internal) Internal = learned;
                else if (kind == ArtKind.Movement) Movement = learned;
                else Attack = learned;
            }

            // ⚠ 입력 순서를 보존한다. 위 루프에서 슬롯에 꽂는 것과 별개로 목록은 받은 대로 만든다.
            LearnedArt[] equipped = new LearnedArt[arts.Length];
            Array.Copy(arts, equipped, arts.Length);
            Equipped = equipped;
        }

        /// <summary>그 종류의 슬롯에 무엇이 꽂혀 있는가. 비어 있으면 null.</summary>
        public LearnedArt SlotOf(ArtKind kind)
        {
            if (kind == ArtKind.Internal) return Internal;
            if (kind == ArtKind.Movement) return Movement;
            return Attack;
        }

        /// <summary>세 칸이 전부 비었는가.</summary>
        public bool IsEmpty
        {
            get { return Equipped.Count == 0; }
        }

        public override string ToString()
        {
            return string.Format(
                "편성[공격={0} 내공={1} 경공={2}]",
                Attack == null ? "―" : Attack.Art.Name,
                Internal == null ? "―" : Internal.Art.Name,
                Movement == null ? "―" : Movement.Art.Name);
        }
    }
}
