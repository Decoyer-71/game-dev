using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Jianghu.Unity
{
    /// <summary>
    /// **화면을 스스로 세운다.** 씬에 아무것도 없어도 Play 를 누르면 UI 가 뜬다.
    ///
    /// ⚠⚠ **왜 이렇게 하는가 — 사용자 에디터 작업을 0 으로 만들기 위해서다**(설계 `docs/ui-martial-list-plan.md` §2).
    ///   Unity MCP 가 미연결이라 Claude 는 씬 배치·프리팹·인스펙터 연결을 못 한다. 그 전부가 사용자 비용이므로
    ///   (작업공간 §9 — *"가장 비쌈 · 사용자만 가능"*), **코드가 계층을 통째로 만든다.**
    ///   공식 문서가 <c>RuntimeInitializeOnLoadMethod</c> 에 대해 *"에디터에서 Play 모드에 들어갈 때도
    ///   같은 호출이 보장된다"* 고 명시하므로 이 방식이 성립한다.
    ///
    /// ⚠⚠ **이것은 프로토타입 스캐폴딩이다.** 실제 게임이 되면 씬·프리팹 구조로 바뀐다(설계 §6).
    ///   그래도 지금 버리지 않는 이유는 **UI 파이프라인(한글·조립·Core 바인딩)을 먼저 뚫는 것**이 목적이기 때문이다.
    /// </summary>
    public static class UiBootstrap
    {
        /// <summary>만든 루트의 이름. ⚠ 중복 생성을 막는 표식으로도 쓴다.</summary>
        private const string RootName = "[Jianghu UI]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            // ⚠⚠ **중복 가드** (설계 §2 에서 미리 등재한 함정). 도메인 리로드를 끈 상태로 Play 를 재진입하면
            //   정적 상태가 남아 두 번 만들어질 수 있다. 이미 있으면 지우고 다시 만든다 —
            //   *"남겨 두고 건너뛴다"* 가 아니라 **다시 만든다.** 스크립트를 고친 뒤 Play 를 눌렀을 때
            //   옛 화면이 그대로 남아 있으면 고친 것이 반영 안 된 줄 알고 헤매게 된다.
            GameObject old = GameObject.Find(RootName);
            if (old != null)
            {
                // Destroy 는 프레임 끝에 지워지므로 같은 이름으로 새로 만들면 그 프레임 동안 둘이 공존한다.
                // 먼저 이름을 바꿔 다음 Find 가 안 걸리게 한다. (공식 문서 - 런타임에는 DestroyImmediate 대신 Destroy)
                old.name = RootName + " (버려짐)";
                Object.Destroy(old);
            }

            var root = new GameObject(RootName);
            Object.DontDestroyOnLoad(root);

            EnsureEventSystem();
            Canvas canvas = CreateCanvas(root);
            MartialListScreen.Build(canvas.transform);
        }

        /// <summary>
        /// 입력을 받을 <see cref="EventSystem"/> 을 보장한다.
        ///
        /// ⚠ 이 프로젝트는 `activeInputHandler: 2`(Both, `ProjectSettings.asset:679` 실측)라
        ///   구식 입력 모듈도 동작한다. **씬에 이미 있으면 손대지 않는다** — 두 입력 모듈을 같은
        ///   `EventSystem` 에 얹으면 충돌하는 것이 알려진 함정이다.
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Object.DontDestroyOnLoad(go);
        }

        /// <summary>
        /// 화면 전체를 덮는 Canvas.
        /// ⚠ <see cref="RenderMode.ScreenSpaceOverlay"/> 라 **카메라를 참조하지 않는다** — 씬 카메라 설정과 무관하고
        ///   URP 2D 렌더러와도 얽히지 않는다.
        /// </summary>
        private static Canvas CreateCanvas(GameObject root)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(root.transform, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // ⚠ 해상도가 달라져도 글자 크기가 같이 변하도록 기준 해상도를 정한다. 안 그러면 4K 에서 깨알이 된다.
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }
    }
}
