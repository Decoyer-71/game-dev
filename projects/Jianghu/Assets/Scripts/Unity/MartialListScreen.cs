using System.Collections.Generic;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Display;
using Jianghu.Core.Martial.Morphemes;
using UnityEngine;
using UnityEngine.UI;

namespace Jianghu.Unity
{
    /// <summary>
    /// **무공 목록 화면** — 설계 `docs/ui-martial-list-plan.md` §5 의 2-b · 2-c.
    ///
    /// 왼쪽에 138종을 계층·유형·성향으로 걸러 늘어놓고, 하나를 고르면 오른쪽에
    /// **이름의 글자 하나하나가 무엇을 넣었는지** 편다.
    ///
    /// ⚠⚠ **이 파일은 계산을 하지 않는다.** 무엇을 보여줄지는 전부 Core 의
    ///   <see cref="ArtBreakdown"/> 이 정하고 여기는 그리기만 한다. 그렇게 나눈 이유가
    ///   1단계 스모크 화면에서 나왔다 — 화면이 직접 축을 골라 찍었더니 성(聖)처럼 공격이 0 인
    ///   글자가 *"아무것도 안 하는 글자"* 로 보였고, **Unity 층은 `dotnet test` 가 안 닿아
    ///   그 결함이 테스트로 잡히지 않았다.** 계산이 Core 에 있으면 잡힌다
    ///   (`Assets/Tests/EditMode/Martial/ArtBreakdownTests.cs`).
    ///
    /// ⚠ 프리팹 없이 코드로 조립한다(<see cref="UiFactory"/>). 프로토타입 스캐폴딩이며 설계 §6 이
    ///   버려질 것으로 미리 등재해 뒀다.
    /// </summary>
    internal sealed class MartialListScreen : MonoBehaviour
    {
        // ─────────────────────────── 필터 선택지 ───────────────────────────

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

        private int tierFilter = All;
        private int disciplineFilter = All;
        private int alignmentFilter = All;

        private MartialArt selected;

        // ─────────────────────────── 화면 조각 ───────────────────────────

        private Transform listContent;
        private Transform detailContent;
        private Text countLabel;
        private readonly List<Button> tierButtons = new List<Button>();
        private readonly List<Button> disciplineButtons = new List<Button>();
        private readonly List<Button> alignmentButtons = new List<Button>();

        /// <summary>선택된 목록 항목의 배경을 되돌리기 위해 무공별 버튼을 들고 있는다.</summary>
        private readonly Dictionary<MartialArt, Image> listRows = new Dictionary<MartialArt, Image>();

        // ─────────────────────────── 조립 ───────────────────────────

        public static MartialListScreen Build(Transform parent)
        {
            GameObject root = UiFactory.Panel(parent, "MartialListScreen", UiFactory.Background);
            UiFactory.Stretch(root);

            var screen = root.AddComponent<MartialListScreen>();
            screen.Compose(root.transform);
            return screen;
        }

        private void Compose(Transform root)
        {
            // ── 머리말 ──
            Text title = UiFactory.Label(root, "Title",
                "무공 " + MartialArtCatalog.All.Count + "종 — 이름이 수치를 푼다",
                24, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(20, -52);
            titleRect.offsetMax = new Vector2(-20, -16);

            // ── 좌: 필터 + 목록 ──
            GameObject left = UiFactory.Panel(root, "Left", UiFactory.PanelFill);
            var leftRect = left.GetComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0, 0);
            leftRect.anchorMax = new Vector2(0, 1);
            leftRect.pivot = new Vector2(0, 0.5f);
            leftRect.offsetMin = new Vector2(16, 16);
            leftRect.offsetMax = new Vector2(16 + LeftWidth, -56);

            UiFactory.VerticalStack(left, 8, 6f);
            BuildFilters(left.transform);

            countLabel = UiFactory.Label(left.transform, "Count", "", 15, UiFactory.InkDim);
            AddHeight(countLabel.gameObject, 20);

            ScrollRect listScroll;
            listContent = UiFactory.ScrollArea(left.transform, "List", out listScroll);
            Flexible(listScroll.gameObject);

            // ── 우: 상세 ──
            GameObject right = UiFactory.Panel(root, "Right", UiFactory.PanelFill);
            var rightRect = right.GetComponent<RectTransform>();
            rightRect.anchorMin = Vector2.zero;
            rightRect.anchorMax = Vector2.one;
            rightRect.offsetMin = new Vector2(16 + LeftWidth + 12, 16);
            rightRect.offsetMax = new Vector2(-16, -56);

            ScrollRect detailScroll;
            detailContent = UiFactory.ScrollArea(right.transform, "Detail", out detailScroll);
            UiFactory.Stretch(detailScroll.gameObject, 4f);

            RefreshList();
        }

        private const float LeftWidth = 560f;

        // ─────────────────────────── 필터 ───────────────────────────

        private void BuildFilters(Transform parent)
        {
            BuildFilterRow(parent, "계층", TierOptions.Length, tierButtons,
                i => KoreanNames.Of(TierOptions[i]),
                i => { tierFilter = i; RefreshList(); });

            BuildFilterRow(parent, "유형", DisciplineOptions.Length, disciplineButtons,
                i => KoreanNames.Of(DisciplineOptions[i]),
                i => { disciplineFilter = i; RefreshList(); });

            BuildFilterRow(parent, "성향", AlignmentOptions.Length, alignmentButtons,
                i => KoreanNames.Of(AlignmentOptions[i]),
                i => { alignmentFilter = i; RefreshList(); });
        }

        /// <summary>
        /// 필터 한 줄. 맨 앞은 항상 **전체**다.
        /// ⚠ 버튼을 리스트에 담아 두는 것은 **선택 표시를 다시 칠하기 위해서**다. 안 그러면 무엇이
        ///   켜져 있는지 화면에 안 보이고, 필터가 걸린 줄 모른 채 *"무공이 왜 9종뿐이지"* 를 묻게 된다.
        /// </summary>
        private void BuildFilterRow(Transform parent, string title, int count, List<Button> sink,
                                    System.Func<int, string> nameOf, System.Action<int> onPick)
        {
            GameObject strip = UiFactory.HorizontalStrip(parent, title + "Filter", 30f);

            Text label = UiFactory.Label(strip.transform, "Label", title, 15, UiFactory.InkDim, TextAnchor.MiddleLeft);
            Width(label.gameObject, 44);

            AddFilterButton(strip.transform, "전체", sink, () => onPick(All));
            for (int i = 0; i < count; i++)
            {
                int index = i;   // ⚠ 클로저가 루프 변수를 잡지 않도록 복사한다.
                AddFilterButton(strip.transform, nameOf(index), sink, () => onPick(index));
            }
        }

        private void AddFilterButton(Transform parent, string label, List<Button> sink, System.Action onClick)
        {
            Button button = UiFactory.Row(parent, "F_" + label, label, 14,
                UiFactory.RowFill, UiFactory.Ink, () => onClick(), 30f);

            // 글자 폭에 맞춰 좁힌다 — 한글 한 자를 약 15px 로 잡고 좌우 여백을 더한다.
            Width(button.gameObject, 22 + label.Length * 15);
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

        private void RefreshList()
        {
            PaintFilter(tierButtons, tierFilter);
            PaintFilter(disciplineButtons, disciplineFilter);
            PaintFilter(alignmentButtons, alignmentFilter);

            UiFactory.Clear(listContent);
            listRows.Clear();

            var shown = new List<MartialArt>();
            foreach (MartialArt art in MartialArtCatalog.All)
            {
                if (Passes(art)) shown.Add(art);
            }

            countLabel.text = shown.Count + "종" + (shown.Count == MartialArtCatalog.All.Count ? "" : " (걸러짐)");

            foreach (MartialArt art in shown)
            {
                MartialArt captured = art;
                Button row = UiFactory.Row(listContent, "A_" + art.Id, RowLabel(art), 16,
                    UiFactory.RowFill, UiFactory.Ink, () => Select(captured), 30f);
                listRows[art] = row.GetComponent<Image>();
            }

            // ⚠ 걸러져서 사라진 무공이 선택돼 있으면 상세를 비운다 — 목록에 없는 것을 계속 보여주면
            //   *"이게 왜 여기 있지"* 가 된다.
            if (selected != null && !listRows.ContainsKey(selected)) selected = null;

            if (selected == null && shown.Count > 0) Select(shown[0]);
            else if (selected != null) Select(selected);
            else ShowEmptyDetail();
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

        // ─────────────────────────── 상세 ───────────────────────────

        private void Select(MartialArt art)
        {
            foreach (KeyValuePair<MartialArt, Image> kv in listRows)
            {
                kv.Value.color = kv.Key == art ? UiFactory.RowSelected : UiFactory.RowFill;
            }

            selected = art;
            ShowDetail(ArtBreakdown.Of(art));
        }

        private void ShowEmptyDetail()
        {
            UiFactory.Clear(detailContent);
            UiFactory.Label(detailContent, "Empty", "필터에 걸리는 무공이 없다.", 18, UiFactory.InkDim);
        }

        private void ShowDetail(ArtBreakdown b)
        {
            UiFactory.Clear(detailContent);

            AddHeight(UiFactory.Label(detailContent, "Name", b.Name, 30, UiFactory.Ink,
                TextAnchor.UpperLeft, FontStyle.Bold).gameObject, 40);
            AddHeight(UiFactory.Label(detailContent, "Head", b.HeadLine(), 16, UiFactory.InkDim).gameObject, 24);

            Gap(10);
            AddHeight(UiFactory.Label(detailContent, "Caption", "이름이 푸는 수치", 18, UiFactory.Ink,
                TextAnchor.UpperLeft, FontStyle.Bold).gameObject, 26);

            foreach (MorphemeContribution c in b.Characters) AddCharacterRow(c);

            Gap(10);
            AddHeight(UiFactory.Label(detailContent, "TotalCaption", "합계", 18, UiFactory.Ink,
                TextAnchor.UpperLeft, FontStyle.Bold).gameObject, 26);
            AddAxisRow("", b.Total);

            // ⚠ 종(宗)이 걸리면 글자별 값이 이미 2배다. 그 사실을 안 적으면 사전을 편 사람이
            //   *"사전 값과 다르다"* 로 읽는다.
            if (b.FormEffectDoubled)
            {
                AddHeight(UiFactory.Label(detailContent, "Doubled",
                    "종(宗) — 무공형태의 효과가 페널티까지 함께 2배다", 15, UiFactory.InkDim).gameObject, 22);
            }

            Gap(10);
            AddMeta("기력 소모", b.QiCost.ToString());
            AddMeta("타격 횟수", b.HitCount + "회");
            AddMeta("타격 범위", b.ScopeName);
            AddMeta("먼저 닿는 열", b.RowName);
            AddMeta("무학분류", b.LineageName);
            AddMeta("상성 우위", b.CounterNames.Count == 0 ? "없음" : string.Join(" · ", b.CounterNames));
            AddMeta("절대경지 규칙", b.RuleName);
        }

        /// <summary>글자 한 줄 — `참(斬) │ 공격방식 │ 베기 │ 공격 +1`.</summary>
        private void AddCharacterRow(MorphemeContribution c)
        {
            GameObject strip = UiFactory.HorizontalStrip(detailContent, "C_" + c.Text, 26f, 8f);

            string head = c.IsSuffix ? c.Text : c.Text + "(" + c.Hanja + ")";
            Width(UiFactory.Label(strip.transform, "Char", head, 18,
                c.IsSuffix ? UiFactory.InkDim : UiFactory.Ink, TextAnchor.MiddleLeft).gameObject, 72);

            Width(UiFactory.Label(strip.transform, "Category", c.CategoryName, 14,
                UiFactory.InkDim, TextAnchor.MiddleLeft).gameObject, 130);

            Width(UiFactory.Label(strip.transform, "Meaning", c.Meaning, 15,
                UiFactory.InkDim, TextAnchor.MiddleLeft).gameObject, 110);

            AddAxisCells(strip.transform, c.Axes);
        }

        private void AddAxisRow(string head, IReadOnlyList<StatAxisValue> axes)
        {
            GameObject strip = UiFactory.HorizontalStrip(detailContent, "Axes", 26f, 8f);
            if (!string.IsNullOrEmpty(head))
            {
                Width(UiFactory.Label(strip.transform, "Head", head, 15, UiFactory.InkDim, TextAnchor.MiddleLeft).gameObject, 72);
            }
            AddAxisCells(strip.transform, axes);
        }

        /// <summary>
        /// 축을 **하나씩 따로** 찍는다.
        /// ⚠ 한 줄에 몰아넣지 않는 이유는 색이다 — 이득과 대가가 한 글자 안에 섞여 있을 수 있어
        ///   (`환(幻)` 처럼) 통째로 칠하면 둘 중 하나가 거짓이 된다.
        /// </summary>
        private static void AddAxisCells(Transform parent, IReadOnlyList<StatAxisValue> axes)
        {
            if (axes.Count == 0)
            {
                Text none = UiFactory.Label(parent, "None", "수치 없음", 14, UiFactory.InkDim, TextAnchor.MiddleLeft);
                Flexible(none.gameObject);
                return;
            }

            for (int i = 0; i < axes.Count; i++)
            {
                StatAxisValue v = axes[i];
                Text cell = UiFactory.Label(parent, "Axis" + i, v.ToString(), 15,
                    v.IsGain ? UiFactory.Gain : UiFactory.Loss, TextAnchor.MiddleLeft);

                // ⚠⚠ 폭을 못박지 않는다. `Text` 가 `ILayoutElement` 라 **제 글자를 재서** 폭을 낸다 —
                //   처음엔 `글자수 × 12` 로 어림했는데, 한글은 약 15px 이고 ASCII·기호는 약 8px 이라
                //   `상태이상 저항 +30%p` 같은 혼합 문자열에서 어림이 크게 빗나간다.
                // ⚠ 남는 폭이 모자라면 uGUI 가 최소폭까지 줄이며 글자를 자른다. 실측으로 축이 가장
                //   많은 것은 **합계 6축(`환창혈만`)** 이므로 거기서 먼저 드러난다 — 설계 §5 4번 확인 항목.
                LayoutElement element = Element(cell.gameObject);
                element.flexibleWidth = 0;
            }

            // 남는 폭을 먹는 빈 칸 — 없으면 축이 줄 가운데로 밀린다.
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(parent, false);
            Flexible(spacer);
        }

        private void AddMeta(string name, string value)
        {
            GameObject strip = UiFactory.HorizontalStrip(detailContent, "M_" + name, 24f, 8f);
            Width(UiFactory.Label(strip.transform, "Name", name, 15, UiFactory.InkDim, TextAnchor.MiddleLeft).gameObject, 130);
            Text v = UiFactory.Label(strip.transform, "Value", value, 15, UiFactory.Ink, TextAnchor.MiddleLeft);
            Flexible(v.gameObject);
        }

        private void Gap(float height)
        {
            var go = new GameObject("Gap", typeof(RectTransform));
            go.transform.SetParent(detailContent, false);
            AddHeight(go, height);
        }

        // ─────────────────────────── 레이아웃 도우미 ───────────────────────────

        private static LayoutElement Element(GameObject go)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            return element != null ? element : go.AddComponent<LayoutElement>();
        }

        private static void Width(GameObject go, float width)
        {
            LayoutElement element = Element(go);
            element.preferredWidth = width;
            element.minWidth = width;
            element.flexibleWidth = 0;
        }

        private static void AddHeight(GameObject go, float height)
        {
            LayoutElement element = Element(go);
            element.preferredHeight = height;
            element.minHeight = height;
        }

        private static void Flexible(GameObject go)
        {
            LayoutElement element = Element(go);
            element.flexibleWidth = 1;
            element.flexibleHeight = 1;
        }

    }
}
