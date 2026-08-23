using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Jianghu.Unity
{
    /// <summary>
    /// **화면을 갈아 끼우는 띠.** 위쪽에 이름표 버튼을 두고 아래 자리에 고른 화면을 세운다.
    ///
    /// ⚠ 화면이 둘이 되면서 생겼다. 누를 때마다 **통째로 다시 만든다** — 상태를 살려 두면
    ///   *"돌아왔더니 편성이 남아 있다/사라졌다"* 가 애매해지는데, 지금은 그 규칙을 정할 근거가 없다.
    ///   ⚠⚠ 그래서 **전투 편성은 화면을 떠나면 사라진다.** 설계 §7 이 *"편성 프리셋은 안 만든다"* 고
    ///     적어 둔 것과 같은 선이고, 필요해지면 그때 만든다.
    /// </summary>
    internal sealed class ScreenRouter : MonoBehaviour
    {
        private const float NavHeight = 38f;

        private readonly List<Button> tabs = new List<Button>();
        private Transform content;
        private int current = -1;

        private static readonly string[] TabNames = { "무공 목록", "전투" };

        public static ScreenRouter Build(Transform parent)
        {
            GameObject root = UiFactory.Panel(parent, "Root", UiFactory.Background);
            UiFactory.Stretch(root);

            var router = root.AddComponent<ScreenRouter>();
            router.Compose(root.transform);
            return router;
        }

        private void Compose(Transform root)
        {
            // ── 아래: 화면 자리 (먼저 만든다 — 탭이 곧바로 여기에 화면을 세운다) ──
            var area = new GameObject("Content", typeof(RectTransform));
            area.transform.SetParent(root, false);
            content = area.transform;

            var areaRect = area.GetComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = Vector2.zero;
            areaRect.offsetMax = new Vector2(0, -NavHeight);

            // ── 위: 탭 띠 ──
            GameObject bar = UiFactory.Panel(root, "Nav", UiFactory.PanelFill);
            var barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0, 1);
            barRect.anchorMax = new Vector2(1, 1);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.offsetMin = new Vector2(0, -NavHeight);
            barRect.offsetMax = Vector2.zero;

            var layout = bar.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.spacing = 6f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                Button b = UiFactory.Row(bar.transform, "Tab" + i, TabNames[i], 15,
                    UiFactory.RowFill, UiFactory.Ink, () => Show(index), NavHeight - 8f);
                LayoutHelp.Width(b.gameObject, 24 + TabNames[i].Length * 15);
                b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
                tabs.Add(b);
            }

            Show(0);
        }

        private void Show(int index)
        {
            if (index == current) return;
            current = index;

            for (int i = 0; i < tabs.Count; i++)
            {
                tabs[i].GetComponent<Image>().color =
                    i == index ? UiFactory.RowSelected : UiFactory.RowFill;
            }

            UiFactory.Clear(content);

            if (index == 0) MartialListScreen.Build(content);
            else CombatScreen.Build(content);
        }
    }
}
