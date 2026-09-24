using System.Collections.Generic;
using Jianghu.Core.Martial;
using Jianghu.Core.Martial.Display;
using UnityEngine;
using UnityEngine.UI;

namespace Jianghu.Unity
{
    /// <summary>
    /// **무공 목록 화면** — 설계 `docs/ui-martial-list-plan.md`.
    ///
    /// 왼쪽에서 138종을 걸러 고르고(<see cref="MartialPicker"/>), 오른쪽에
    /// **이름의 글자 하나하나가 무엇을 넣었는지** 편다.
    ///
    /// ⚠⚠ **이 파일은 계산을 하지 않는다.** 무엇을 보여줄지는 전부 Core 의
    ///   <see cref="ArtBreakdown"/> 이 정하고 여기는 그리기만 한다. 그렇게 나눈 이유가
    ///   1단계 스모크 화면에서 나왔다 — 화면이 직접 축을 골라 찍었더니 성(聖)처럼 공격이 0 인
    ///   글자가 *"아무것도 안 하는 글자"* 로 보였고, **Unity 층은 `dotnet test` 가 안 닿아
    ///   그 결함이 테스트로 잡히지 않았다.** 계산이 Core 에 있으면 잡힌다
    ///   (`Assets/Tests/EditMode/Martial/ArtBreakdownTests.cs`).
    ///
    /// ⚠ 필터·목록은 전투 화면과 **같은 부품**을 쓴다(<see cref="MartialPicker"/>) — 두 벌로
    ///   두면 갈라진다.
    /// </summary>
    internal sealed class MartialListScreen : MonoBehaviour
    {
        private Transform detailContent;

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
            Text title = UiFactory.Label(root, "Title",
                "무공 " + MartialArtCatalog.All.Count + "종 — 이름이 수치를 푼다",
                24, UiFactory.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.offsetMin = new Vector2(20, -52);
            titleRect.offsetMax = new Vector2(-20, -16);

            // ── 우: 상세 ──
            // ⚠ **먼저 만든다.** 좌측 부품이 생성 즉시 첫 무공을 골라 `OnPick` 을 부르고,
            //   그때 `detailContent` 가 없으면 널 참조가 난다.
            GameObject right = UiFactory.Panel(root, "Right", UiFactory.PanelFill);
            var rightRect = right.GetComponent<RectTransform>();
            rightRect.anchorMin = Vector2.zero;
            rightRect.anchorMax = Vector2.one;
            rightRect.offsetMin = new Vector2(16 + MartialPicker.PreferredWidth + 12, 16);
            rightRect.offsetMax = new Vector2(-16, -56);

            ScrollRect detailScroll;
            detailContent = UiFactory.ScrollArea(right.transform, "Detail", out detailScroll);
            UiFactory.Stretch(detailScroll.gameObject, 4f);

            // ── 좌: 무공 고르기 ──
            GameObject left = UiFactory.Panel(root, "Left", UiFactory.PanelFill);
            var leftRect = left.GetComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0, 0);
            leftRect.anchorMax = new Vector2(0, 1);
            leftRect.pivot = new Vector2(0, 0.5f);
            leftRect.offsetMin = new Vector2(16, 16);
            leftRect.offsetMax = new Vector2(16 + MartialPicker.PreferredWidth, -56);

            new MartialPicker(left, OnPick);
        }

        private void OnPick(MartialArt art)
        {
            if (art == null) ShowEmptyDetail();
            else ShowDetail(ArtBreakdown.Of(art));
        }

        // ─────────────────────────── 상세 ───────────────────────────

        private void ShowEmptyDetail()
        {
            UiFactory.Clear(detailContent);
            UiFactory.Label(detailContent, "Empty", "필터에 걸리는 무공이 없다.", 18, UiFactory.InkDim);
        }

        private void ShowDetail(ArtBreakdown b)
        {
            UiFactory.Clear(detailContent);

            LayoutHelp.Height(UiFactory.Label(detailContent, "Name", b.Name, 30, UiFactory.Ink,
                TextAnchor.UpperLeft, FontStyle.Bold).gameObject, 40);
            LayoutHelp.Height(UiFactory.Label(detailContent, "Head", b.HeadLine(), 16, UiFactory.InkDim).gameObject, 24);

            Gap(10);
            LayoutHelp.Height(UiFactory.Label(detailContent, "Caption", "이름이 푸는 수치", 18, UiFactory.Ink,
                TextAnchor.UpperLeft, FontStyle.Bold).gameObject, 26);

            foreach (MorphemeContribution c in b.Characters) AddCharacterRow(c);

            Gap(10);
            LayoutHelp.Height(UiFactory.Label(detailContent, "TotalCaption", "합계", 18, UiFactory.Ink,
                TextAnchor.UpperLeft, FontStyle.Bold).gameObject, 26);
            AddAxisRow("", b.Total);

            // ⚠ 종(宗)이 걸리면 글자별 값이 이미 2배다. 그 사실을 안 적으면 사전을 편 사람이
            //   *"사전 값과 다르다"* 로 읽는다.
            if (b.FormEffectDoubled)
            {
                LayoutHelp.Height(UiFactory.Label(detailContent, "Doubled",
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
            LayoutHelp.Width(UiFactory.Label(strip.transform, "Char", head, 18,
                c.IsSuffix ? UiFactory.InkDim : UiFactory.Ink, TextAnchor.MiddleLeft).gameObject, 64);

            // ⚠⚠ **머리 세 칸을 좁게 잡는 것이 곧 기여 칸을 넓히는 일이다.** 넉넉히 줬더니
            //   축이 셋인 글자(성聖 — 방어·막기확률·상태이상 저항)가 두 줄로 깨졌다.
            //   최장은 카테고리 `절대경지규칙`(6자) · 의미 `기력소실`(4자)이다.
            LayoutHelp.Width(UiFactory.Label(strip.transform, "Category", c.CategoryName, 14,
                UiFactory.InkDim, TextAnchor.MiddleLeft).gameObject, 88);

            LayoutHelp.Width(UiFactory.Label(strip.transform, "Meaning", c.Meaning, 15,
                UiFactory.InkDim, TextAnchor.MiddleLeft).gameObject, 140);

            AddAxisCells(strip.transform, c.Axes, c.EmptyNote);
        }

        private void AddAxisRow(string head, IReadOnlyList<StatAxisValue> axes)
        {
            GameObject strip = UiFactory.HorizontalStrip(detailContent, "Axes", 26f, 8f);
            if (!string.IsNullOrEmpty(head))
            {
                LayoutHelp.Width(UiFactory.Label(strip.transform, "Head", head, 15,
                    UiFactory.InkDim, TextAnchor.MiddleLeft).gameObject, 64);
            }
            AddAxisCells(strip.transform, axes, "수치 없음");
        }

        /// <summary>
        /// 축을 **하나씩 따로** 찍는다.
        /// ⚠ 한 줄에 몰아넣지 않는 이유는 색이다 — 이득과 대가가 한 글자 안에 섞여 있을 수 있어
        ///   (`환(幻)` 처럼) 통째로 칠하면 둘 중 하나가 거짓이 된다.
        /// </summary>
        private static void AddAxisCells(Transform parent, IReadOnlyList<StatAxisValue> axes, string emptyNote)
        {
            if (axes.Count == 0)
            {
                // ⚠⚠ 축 셀과 **같은 잠금**을 건다. 처음엔 `Flexible` 만 줬는데, 창을 작게 쓰면
                //   `형태소 아님 · 수치 없음` 이 다음 줄로 밀렸다 — 머리 칸들이 최소폭까지 못박혀
                //   있어 **줄어들 수 있는 것이 이 칸뿐**이라 여기로 부족분이 몰린다.
                Text none = UiFactory.Label(parent, "None", emptyNote, 14, UiFactory.InkDim, TextAnchor.MiddleLeft);
                LayoutHelp.LockWidth(none, emptyNote);
                LayoutHelp.Flexible(none.gameObject);
                return;
            }

            for (int i = 0; i < axes.Count; i++)
            {
                StatAxisValue v = axes[i];
                Text cell = UiFactory.Label(parent, "Axis" + i, v.ToString(), 15,
                    v.IsGain ? UiFactory.Gain : UiFactory.Loss, TextAnchor.MiddleLeft);
                LayoutHelp.LockWidth(cell, v.ToString());
                LayoutHelp.Element(cell.gameObject).flexibleWidth = 0;
            }

            // 남는 폭을 먹는 빈 칸 — 없으면 축이 줄 가운데로 밀린다.
            var spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(parent, false);
            LayoutHelp.Flexible(spacer);
        }

        private void AddMeta(string name, string value)
        {
            GameObject strip = UiFactory.HorizontalStrip(detailContent, "M_" + name, 24f, 8f);
            LayoutHelp.Width(UiFactory.Label(strip.transform, "Name", name, 15,
                UiFactory.InkDim, TextAnchor.MiddleLeft).gameObject, 130);

            // ⚠ 상성 우위처럼 값이 길어질 수 있다(`양기 · 음기`). 위와 같은 이유로 줄바꿈을 막는다.
            Text v = UiFactory.Label(strip.transform, "Value", value, 15, UiFactory.Ink, TextAnchor.MiddleLeft);
            LayoutHelp.LockWidth(v, value);
            LayoutHelp.Flexible(v.gameObject);
        }

        private void Gap(float height)
        {
            var go = new GameObject("Gap", typeof(RectTransform));
            go.transform.SetParent(detailContent, false);
            LayoutHelp.Height(go, height);
        }
    }
}
