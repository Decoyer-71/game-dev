using System;
using System.Collections.Generic;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Display;
using Jianghu.Core.Martial.Morphemes;
using UnityEngine;
using UnityEngine.UI;

namespace Jianghu.Unity
{
    /// <summary>
    /// **무공 138종을 걸러 보여주고 하나를 고르게 하는 조각.** 화면이 아니라 **부품**이다.
    ///
    /// ⚠⚠ **왜 따로 뺐는가** — 무공 목록 화면과 전투 화면이 **같은 고르기**를 쓴다. 전투 화면에
    ///   두 번째 사본을 만들면 필터가 갈라지고, 그것은 이 저장소가 이번 작업에서만
    ///   이름표(네 곳) · 대전자 생성 규칙 · 승률 공식으로 **세 번** 겪은 형태다.
    ///
    /// ⚠ 고른 결과로 무엇을 할지는 부르는 쪽이 정한다(<see cref="onPick"/>) — 목록 화면은 상세를
    ///   그리고, 전투 화면은 편성 슬롯에 넣는다.
    /// </summary>
    internal sealed class MartialPicker
    {
        /// <summary>
        /// 이 부품이 필요로 하는 폭.
        /// ⚠ 실측 — 가장 긴 목록 줄이 22자(≈362px)이고 **가장 긴 필터 줄(계층)이 ≈482px** 다.
        ///   필터 줄이 폭을 정한다. 이보다 좁게 주면 버튼이 패널 밖으로 삐져나간다.
        /// </summary>
        public const float PreferredWidth = 520f;

        /// <summary>⚠ 목록 순서는 세기 순이다. 선언 순서를 바꾸면 화면 순서가 바뀐다.</summary>
        private static readonly ArtTier[] TierOptions =
        {
            ArtTier.Wanderer, ArtTier.Minor, ArtTier.Major, ArtTier.Legacy, ArtTier.Absolute,
        };

        private static readonly Discipline[] DisciplineOptions =
        {
            Discipline.Sword, Discipline.Blade, Discipline.Spear, Discipline.Fist,
            Discipline.Dagger, Discipline.InnerArt, Discipline.Movement,
        };

        private static readonly Alignment[] AlignmentOptions =
        {
            Alignment.Orthodox, Alignment.Unorthodox, Alignment.Demonic,
        };

        /// <summary>필터 '전체'. ⚠ <c>Alignment?</c> 의 null 은 **강호무학**이라는 뜻으로 이미 쓰이므로 null 을 '전체'로 쓸 수 없다.</summary>
        private const int All = -1;

        private readonly Action<MartialArt> onPick;
        private readonly bool highlightSelection;

        private readonly List<Button> tierButtons = new List<Button>();
        private readonly List<Button> disciplineButtons = new List<Button>();
        private readonly List<Button> alignmentButtons = new List<Button>();
        private readonly Dictionary<MartialArt, Image> rows = new Dictionary<MartialArt, Image>();

        private Transform listContent;
        private Text countLabel;

        private int tierFilter = All;
        private int disciplineFilter = All;
        private int alignmentFilter = All;

        /// <summary>마지막으로 고른 무공. 걸러져 사라지면 null 이 된다.</summary>
        public MartialArt Selected { get; private set; }

        /// <summary>필터에 걸린 무공 수.</summary>
        public int ShownCount { get; private set; }

        /// <param name="panel">이 부품이 채울 판. ⚠ 세로 배치를 **여기서** 붙이므로 빈 판을 넘긴다.</param>
        /// <param name="onPick">목록에서 무공을 눌렀을 때. 부르는 쪽이 무엇을 할지 정한다.</param>
        /// <param name="highlightSelection">
        /// 고른 줄에 색을 줄 것인가. 목록 화면은 *"지금 보고 있는 것"* 이라 켜고,
        /// 전투 화면은 누를 때마다 슬롯에 담기는 것이라 **끈다** — 켜 두면 담긴 것이 아니라
        /// 마지막으로 누른 것이 강조돼 거짓 신호가 된다.
        /// </param>
        public MartialPicker(GameObject panel, Action<MartialArt> onPick, bool highlightSelection = true)
        {
            this.onPick = onPick;
            this.highlightSelection = highlightSelection;

            UiFactory.VerticalStack(panel, 8, 6f);
            BuildFilters(panel.transform);

            countLabel = UiFactory.Label(panel.transform, "Count", "", 15, UiFactory.InkDim);
            LayoutHelp.Height(countLabel.gameObject, 20);

            ScrollRect scroll;
            listContent = UiFactory.ScrollArea(panel.transform, "List", out scroll);
            LayoutHelp.Flexible(scroll.gameObject);

            Refresh();
        }

        // ─────────────────────────── 필터 ───────────────────────────

        private void BuildFilters(Transform parent)
        {
            BuildFilterRow(parent, "계층", TierOptions.Length, tierButtons,
                i => KoreanNames.Of(TierOptions[i]),
                i => { tierFilter = i; Refresh(); });

            BuildFilterRow(parent, "유형", DisciplineOptions.Length, disciplineButtons,
                i => KoreanNames.Of(DisciplineOptions[i]),
                i => { disciplineFilter = i; Refresh(); });

            BuildFilterRow(parent, "성향", AlignmentOptions.Length, alignmentButtons,
                i => KoreanNames.Of(AlignmentOptions[i]),
                i => { alignmentFilter = i; Refresh(); });
        }

        /// <summary>
        /// 필터 한 줄. 맨 앞은 항상 **전체**다.
        /// ⚠ 버튼을 리스트에 담아 두는 것은 **선택 표시를 다시 칠하기 위해서**다. 안 그러면 무엇이
        ///   켜져 있는지 화면에 안 보이고, 필터가 걸린 줄 모른 채 *"무공이 왜 9종뿐이지"* 를 묻게 된다.
        /// </summary>
        private void BuildFilterRow(Transform parent, string title, int count, List<Button> sink,
                                    Func<int, string> nameOf, Action<int> onFilter)
        {
            GameObject strip = UiFactory.HorizontalStrip(parent, title + "Filter", 30f);

            Text label = UiFactory.Label(strip.transform, "Label", title, 15, UiFactory.InkDim, TextAnchor.MiddleLeft);
            LayoutHelp.Width(label.gameObject, 40);

            AddFilterButton(strip.transform, "전체", sink, () => onFilter(All));
            for (int i = 0; i < count; i++)
            {
                int index = i;   // ⚠ 클로저가 루프 변수를 잡지 않도록 복사한다.
                AddFilterButton(strip.transform, nameOf(index), sink, () => onFilter(index));
            }
        }

        private void AddFilterButton(Transform parent, string label, List<Button> sink, Action onClick)
        {
            Button button = UiFactory.Row(parent, "F_" + label, label, 14,
                UiFactory.RowFill, UiFactory.Ink, () => onClick(), 30f);

            // 글자 폭에 맞춰 좁힌다 — 14pt 한글 한 자를 약 14px 로 잡고 좌우 여백을 더한다.
            // ⚠⚠ **최소폭도 함께 못박히므로**(`LayoutHelp.Width`) 줄이 넘치면 줄어드는 게 아니라
            //   **패널 밖으로 삐져나온다.** 가장 긴 계층 줄이 여백 포함 약 482px 이라
            //   <see cref="PreferredWidth"/> 를 520 으로 잡았다.
            LayoutHelp.Width(button.gameObject, 16 + label.Length * 14);
            button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
            sink.Add(button);
        }

        /// <summary>켜진 필터에 색을 준다. 인덱스 0 이 '전체' 라 실제 값은 하나씩 밀려 있다.</summary>
        private static void PaintFilter(List<Button> buttons, int selectedIndex)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                bool on = (i - 1) == selectedIndex;
                buttons[i].GetComponent<Image>().color = on ? UiFactory.RowSelected : UiFactory.RowFill;
            }
        }

        // ─────────────────────────── 목록 ───────────────────────────

        private void Refresh()
        {
            PaintFilter(tierButtons, tierFilter);
            PaintFilter(disciplineButtons, disciplineFilter);
            PaintFilter(alignmentButtons, alignmentFilter);

            UiFactory.Clear(listContent);
            rows.Clear();

            var shown = new List<MartialArt>();
            foreach (MartialArt art in MartialArtCatalog.All)
            {
                if (Passes(art)) shown.Add(art);
            }
            ShownCount = shown.Count;

            countLabel.text = shown.Count + "종" + (shown.Count == MartialArtCatalog.All.Count ? "" : " (걸러짐)");

            foreach (MartialArt art in shown)
            {
                MartialArt captured = art;
                Button row = UiFactory.Row(listContent, "A_" + art.Id, RowLabel(art), 16,
                    UiFactory.RowFill, UiFactory.Ink, () => Pick(captured), 30f);
                rows[art] = row.GetComponent<Image>();
            }

            // ⚠⚠ **자동 선택은 강조를 켠 쪽에서만 한다.** 안 그러면 목록을 새로 그릴 때마다
            //   — 즉 **필터를 누를 때마다** — `onPick` 이 저절로 불린다. 전투 화면에서 그것은
            //   *"필터를 눌렀더니 슬롯에 무공이 하나 더 담겼다"* 가 된다.
            //   ⚠ 컴파일러가 못 잡는 종류다. `UnityLayerCheck` 는 오류 0 을 냈고, 이건 읽다가 찾았다.
            if (!highlightSelection) return;

            // ⚠ 걸러져서 사라진 무공이 선택돼 있으면 놓는다 — 목록에 없는 것을 계속 가리키면
            //   *"이게 왜 여기 있지"* 가 된다.
            if (Selected != null && !rows.ContainsKey(Selected)) Selected = null;

            if (Selected == null && shown.Count > 0) Pick(shown[0]);
            else if (Selected != null) Pick(Selected);
            else
            {
                // 걸린 무공이 없다. 부르는 쪽이 빈 상태를 그리도록 알린다.
                if (onPick != null) onPick(null);
            }
        }

        private void Pick(MartialArt art)
        {
            if (highlightSelection)
            {
                Selected = art;
                foreach (KeyValuePair<MartialArt, Image> kv in rows)
                {
                    kv.Value.color = kv.Key == art ? UiFactory.RowSelected : UiFactory.RowFill;
                }
            }

            if (onPick != null) onPick(art);
        }

        private bool Passes(MartialArt art)
        {
            if (tierFilter != All && art.Tier != TierOptions[tierFilter]) return false;
            if (disciplineFilter != All && art.Discipline != DisciplineOptions[disciplineFilter]) return false;

            // ⚠ 강호무학은 무공 자체에 성향이 없다(익힌 사람을 따른다). 성향 필터를 걸면 빠지는 것이 맞다.
            if (alignmentFilter != All)
            {
                if (!art.Alignment.HasValue) return false;
                if (art.Alignment.Value != AlignmentOptions[alignmentFilter]) return false;
            }
            return true;
        }

        private static string RowLabel(MartialArt art)
        {
            return art.Name + "   " + KoreanNames.Of(art.Tier)
                   + " · " + KoreanNames.Of(art.Discipline)
                   + " · " + KoreanNames.Short(art.Alignment);
        }
    }
}
