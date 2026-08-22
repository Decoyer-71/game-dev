using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Jianghu.Unity
{
    /// <summary>
    /// **uGUI 조각을 코드로 찍어내는 공장.** 프리팹 없이 화면을 세우기 위한 것이다.
    ///
    /// ⚠⚠ **왜 프리팹이 아닌가** — Unity MCP 미연결이라 Claude 는 씬·프리팹·인스펙터를 못 만진다.
    ///   그 전부가 사용자 비용이고 작업공간 §9 가 *"가장 비쌈 · 사용자만 가능"* 으로 등재한 항목이다.
    ///   그래서 **에디터 작업 0** 을 유지한다(<see cref="UiBootstrap"/>).
    ///
    /// ⚠ **이 파일은 나중에 버려질 쪽이다**(설계 `docs/ui-martial-list-plan.md` §6). 프리팹 구조로
    ///   옮기면 조립 코드는 사라진다 — 남는 것은 *"어떤 정보를 어떻게 배치했는가"* 뿐이다.
    ///   그래서 여기에는 **게임 규칙을 한 줄도 두지 않는다.** 규칙과 계산은 Core 에 있다.
    ///
    /// ⚠ 색은 상수로 모아 둔다. 화면이 여럿이 되면 테마로 뽑겠지만 아직 하나뿐이다.
    /// </summary>
    internal static class UiFactory
    {
        // ─────────────────────────── 색 ───────────────────────────

        public static readonly Color Background = new Color(0.09f, 0.09f, 0.11f);
        public static readonly Color PanelFill = new Color(0.14f, 0.14f, 0.17f);
        public static readonly Color RowFill = new Color(0.18f, 0.18f, 0.22f);
        public static readonly Color RowSelected = new Color(0.30f, 0.38f, 0.52f);
        public static readonly Color Ink = new Color(0.92f, 0.92f, 0.94f);
        public static readonly Color InkDim = new Color(0.62f, 0.62f, 0.68f);

        /// <summary>이득(양수) 색. ⚠ 초록/빨강만 쓰면 색각 이상에서 구분이 안 되므로 부호도 함께 찍는다(<c>StatAxisValue.Text</c>).</summary>
        public static readonly Color Gain = new Color(0.55f, 0.82f, 0.60f);

        /// <summary>대가(음수) 색.</summary>
        public static readonly Color Loss = new Color(0.90f, 0.55f, 0.52f);

        // ─────────────────────────── 조각 ───────────────────────────

        /// <summary>부모를 가득 채우는 사각형을 만든다. 여백은 <paramref name="pad"/>.</summary>
        public static RectTransform Stretch(GameObject go, float pad = 0f)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(pad, pad);
            rect.offsetMax = new Vector2(-pad, -pad);
            return rect;
        }

        /// <summary>배경색이 있는 판.</summary>
        public static GameObject Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        /// <summary>
        /// 글자.
        /// ⚠ <see cref="Text.horizontalOverflow"/> 를 <c>Wrap</c>, 세로를 <c>Overflow</c> 로 둔다 —
        ///   세로를 자르면 <see cref="ContentSizeFitter"/> 가 늘려 주기 전에 글자가 먼저 사라진다.
        /// </summary>
        public static Text Label(Transform parent, string name, string value, int size, Color color,
                                 TextAnchor anchor = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.font = KoreanFont.Get();
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.text = value;
            return text;
        }

        /// <summary>누를 수 있는 판. 글자는 <paramref name="label"/>, 눌리면 <paramref name="onClick"/>.</summary>
        public static Button Row(Transform parent, string name, string label, int size,
                                 Color fill, Color ink, UnityAction onClick, float height)
        {
            GameObject go = Panel(parent, name, fill);
            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            if (onClick != null) button.onClick.AddListener(onClick);

            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;

            Text text = Label(go.transform, "Text", label, size, ink, TextAnchor.MiddleLeft);
            Stretch(text.gameObject, 0f).offsetMin = new Vector2(10, 0);

            return button;
        }

        /// <summary>가로로 늘어놓는 줄. 필터 버튼 묶음이 쓴다.</summary>
        public static GameObject HorizontalStrip(Transform parent, string name, float height, float spacing = 4f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            return go;
        }

        /// <summary>세로로 쌓는 통. 여백·간격은 인자로.</summary>
        public static VerticalLayoutGroup VerticalStack(GameObject go, int pad, float spacing)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(pad, pad, pad, pad);
            layout.spacing = spacing;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            return layout;
        }

        /// <summary>
        /// 세로 스크롤 영역을 만들고 **내용을 담을 <see cref="Transform"/>** 을 돌려준다.
        ///
        /// ⚠ 마스크로 <see cref="RectMask2D"/> 를 쓴다. <see cref="Mask"/> 는 <see cref="Image"/> 를
        ///   함께 요구하고 스텐실을 잡아먹는데, 사각형으로 자르는 것뿐이라 그럴 이유가 없다.
        /// ⚠ 내용 쪽 피벗을 위로 둬야(<c>(0.5, 1)</c>) 항목이 늘어날 때 **아래로** 자란다.
        /// </summary>
        public static Transform ScrollArea(Transform parent, string name, out ScrollRect scroll)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(ScrollRect));
            root.transform.SetParent(parent, false);
            scroll = root.GetComponent<ScrollRect>();

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewport.transform.SetParent(root.transform, false);
            RectTransform viewportRect = Stretch(viewport);

            // ⚠⚠ **투명한데도 Image 를 얹는 이유는 스크롤 입력이다.** 공식 매뉴얼이
            //   *"스크롤 입력은 content 가 아니라 ScrollRect 의 경계 안에서 받아야 한다"* 고 적는데,
            //   포인터 레이캐스트는 `Graphic` 이 있는 곳에서만 잡힌다. 마스크(`RectMask2D`)는
            //   자르기만 하지 레이캐스트 대상이 아니다.
            //   → 없으면 **항목 사이 틈이나 목록 아래 빈 자리에서 시작한 드래그·휠이 먹지 않는다.**
            //   ⚠ 알파 0 이어도 레이캐스트는 사각형으로 판정한다(`alphaHitTestMinimumThreshold` 를
            //     건드리지 않는 한). 그래서 보이지 않으면서 입력만 받는다.
            var catcher = viewport.GetComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);

            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            VerticalStack(content, 6, 3f);
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            return content.transform;
        }

        /// <summary>자식을 전부 지운다. 필터가 바뀌거나 선택이 바뀔 때 다시 그리기 위한 것.</summary>
        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                // ⚠ 런타임이므로 DestroyImmediate 가 아니라 Destroy 다. 다만 프레임 끝에 지워지므로
                //   같은 프레임에 다시 채우면 잠깐 공존한다 — 부모에서 떼어 그 사이를 없앤다.
                Transform child = parent.GetChild(i);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }
        }
    }
}
