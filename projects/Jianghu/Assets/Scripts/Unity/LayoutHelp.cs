using UnityEngine;
using UnityEngine.UI;

namespace Jianghu.Unity
{
    /// <summary>
    /// **레이아웃 그룹 안에서 칸의 크기를 못박는 도우미.**
    ///
    /// ⚠⚠ 여기 모아 둔 것은 전부 **한 번씩 밟고 나서 생긴 것**이다. 화면이 둘이 되었으므로
    ///   같은 함정을 두 번 파지 않도록 한 곳에 둔다.
    /// </summary>
    internal static class LayoutHelp
    {
        /// <summary>있으면 쓰고 없으면 붙인다.</summary>
        public static LayoutElement Element(GameObject go)
        {
            LayoutElement element = go.GetComponent<LayoutElement>();
            return element != null ? element : go.AddComponent<LayoutElement>();
        }

        /// <summary>
        /// 폭을 못박는다(최소·선호 둘 다).
        /// ⚠⚠ **최소폭까지 못박히므로** 줄이 넘치면 줄어드는 게 아니라 **밖으로 삐져나온다.**
        ///   폭은 어림이라도 미리 계산해 여유를 둘 것.
        /// </summary>
        public static void Width(GameObject go, float width)
        {
            LayoutElement element = Element(go);
            element.preferredWidth = width;
            element.minWidth = width;
            element.flexibleWidth = 0;
        }

        /// <summary>높이를 못박는다.</summary>
        public static void Height(GameObject go, float height)
        {
            LayoutElement element = Element(go);
            element.preferredHeight = height;
            element.minHeight = height;
        }

        /// <summary>남는 자리를 먹게 한다.</summary>
        public static void Flexible(GameObject go)
        {
            LayoutElement element = Element(go);
            element.flexibleWidth = 1;
            element.flexibleHeight = 1;
        }

        /// <summary>
        /// **글자가 줄바꿈으로 깨지지 않도록 폭을 못박는다.**
        ///
        /// ⚠⚠ 레이아웃 그룹 안의 <see cref="Text"/> 는 `minWidth` 가 0 이라, 줄에 자리가 모자라면
        ///   uGUI 가 이 칸을 최소폭까지 줄이고 **줄어든 칸 안에서 글자가 wrap 된다.**
        ///   `방어 +5` 가 `방어`/`+5` 로 갈라진 것이 그 증상이었다.
        /// ⚠ <see cref="Text.preferredWidth"/> 는 **한 줄로 그렸을 때의 실측 폭**이다. 그것을
        ///   min·preferred 양쪽에 걸면 줄어들 수도 늘어날 수도 없다.
        /// ⚠ 동적 OS 폰트는 글리프가 아직 아틀라스에 없으면 폭을 0 근처로 낼 수 있다. 그때
        ///   0 으로 못박으면 칸이 통째로 사라지므로 글자 수로 바닥을 깔아 둔다.
        /// ⚠⚠ **한 줄 안의 칸을 전부 잠그거나 하나도 잠그지 않는다.** 하나만 잠그면 부족분이
        ///   안 잠근 칸으로 전부 몰려 그것만 깨진다 — 실제로 그렇게 한 번 더 밟았다.
        /// </summary>
        public static void LockWidth(Text text, string content)
        {
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            float width = Mathf.Max(text.preferredWidth, content.Length * 9f);
            LayoutElement element = Element(text.gameObject);
            element.preferredWidth = width;
            element.minWidth = width;
        }
    }
}
