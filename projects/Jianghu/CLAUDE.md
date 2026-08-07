# CLAUDE.md — Jianghu (무협 문파경영 RPG)

> **🔖 세션을 새로 시작했다면 `../../docs/HANDOFF.md` 를 먼저 읽을 것.**
> 지금 어디까지 왔고, 무엇이 막혀 있고, 다음에 무엇을 할지가 거기 있다.

> **⚠ 작업 브랜치는 `feat/martial-engine` 이다.** `main` 에 직접 커밋하지 않는다 (`../../CLAUDE.md` §5-B).
> UI·이미지 에셋 작업은 **별도 브랜치**를 만든다 — 만들기 전에 사용자에게 묻는다.

작업공간 공통 규율은 `../../CLAUDE.md` 에 있다. **이 파일은 이 프로젝트 고유 규칙만 적는다.**
설계 근거·범위·단계는 `../../docs/jianghu-design.md`.

**한 줄 정의**: 문파를 경영해 제자를 키우고, 그 제자가 무공을 조합해 싸우는 2D 턴제 무협 RPG (참조작: 낭만강호).
Unity **6000.0.58f1 LTS** · **Universal 2D (URP 17.0.4)** · PC(Windows) · 프로토타입 단계.

---

## 1. 절대 규칙 — 어기면 컴파일이 깨지게 만들어 뒀다

### 1-0. ⚠⚠ 새 안은 `verify` 를 통과한 뒤에 제시한다

**작업공간 규율 `../../CLAUDE.md` §5-C.** 새 메커닉·공식·수치 체계·규칙 변경안은 사용자에게 올리기 **전에** `verify` 검증을 거친다. 이것만은 컴파일러가 못 잡으므로 사람이 지켜야 한다.

⚠ 계기: 방어관통 **160%** 를 제시했다가 사용자가 잡았다 — 승률은 맞았지만 **존재할 수 없는 값**이었다(방어를 100% 무시하면 더 무시할 것이 없다). 상세는 §5-C.

### 1-1. `Core` 는 UnityEngine 을 참조하지 않는다

`Assets/Scripts/Core/` 아래 코드에서 `using UnityEngine;` 은 **금지**다. 실수를 사람 기억에 맡기지 않고 두 겹으로 강제한다:

1. `Jianghu.Core.asmdef` 의 `"noEngineReferences": true` → Unity 가 컴파일 에러를 낸다
2. `Tools/JianghuCore/JianghuCore.csproj` 가 netstandard2.1 로 빌드 → dotnet 이 컴파일 에러를 낸다

**이유**: 게임 규칙(전투·성장·문파)이 Unity 에 묶이면 ⓐ 자동 테스트가 불가능해지고 ⓑ Unity 를 알아야만 로직을 고칠 수 있게 된다. Core 는 Unity 를 몰라도 읽고 고칠 수 있는 평범한 C# 으로 유지한다.

화면·입력·코루틴 등 Unity 가 필요한 것은 전부 `Assets/Scripts/Unity/` 에 둔다. 이 층은 **Core 를 화면에 그리고 버튼에 연결만** 하고 게임 규칙을 갖지 않는다.

### 1-2. 난수는 `IRandomSource` 를 통해서만 뽑는다

`UnityEngine.Random` · `System.Random` **직접 사용 금지.** 전부 `Jianghu.Core.Rng.IRandomSource` 를 주입받아 쓴다.

**⚠⚠ `System.Random` 이 안 되는 구체적 이유**: 내부 알고리즘이 **런타임 구현마다 다르다.** .NET(Core) 계열과 Unity 의 Mono 는 같은 시드에 서로 다른 시퀀스를 낸다. 그러면 `dotnet test` 로 검증한 전투 결과가 Unity 플레이에서 재현되지 않는다 — 우리 검증 전략의 1순위가 통째로 무너진다. 그래서 `XorShiftRandom` 으로 알고리즘 자체를 고정했다.

난수를 쓰는 함수는 시드나 `IRandomSource` 를 **인자로 받는다.** 내부에서 생성하지 않는다.

### 1-3. C# 9 / .NET Standard 2.1 을 넘지 않는다

Unity 6 의 상한이다. record, 최상위 문, `required`, C# 10+ 문법 전부 금지.
`JianghuCore.csproj` 에 `LangVersion 9.0` + `netstandard2.1` 을 박아 뒀으므로 **dotnet 단계에서 걸린다.**

---

## 2. 폴더 구조

```
Jianghu\
├── CLAUDE.md                        ← 이 파일
├── Assets\                          ← Unity 가 관리. 소스의 원본은 항상 여기다
│   ├── Scripts\
│   │   ├── Core\                    ← 순수 C#. 게임 규칙의 실체 전부
│   │   │   ├── Jianghu.Core.asmdef  ←   noEngineReferences: true
│   │   │   └── Rng\                 ←   결정론적 난수  ✅ 구현·검증 완료
│   │   └── Unity\                   ← 얇은 껍데기 (Jianghu.Unity.asmdef)
│   ├── Tests\EditMode\              ← NUnit 테스트. Unity 와 dotnet 이 공유하는 한 벌
│   ├── Scenes\  Settings\           ← 템플릿 생성물
├── Tools\                           ← ⚠ Assets 밖 = Unity 가 임포트하지 않는다
│   ├── nuget.config                 ←   패키지 캐시를 D 로 (C 여유 5.9GB 라서)
│   ├── JianghuCore\                 ←   Core 를 링크로 컴파일 (netstandard2.1)
│   └── CoreTests\                   ←   Tests 를 링크로 실행 (net8.0)
├── Packages\  ProjectSettings\      ← Unity 생성
└── Library\  Temp\  Logs\           ← ⚠ 생성물. 훅이 직접 열람을 차단한다
```

**⚠ 소스 원본은 언제나 `Assets/` 아래다.** `Tools/` 의 csproj 들은 파일을 **복사하지 않고 `<Compile Include>` 링크로** 끌어다 쓴다. `Tools/` 에 .cs 를 새로 만들지 말 것 — 그러면 Unity 가 모르는 코드가 생긴다.

---

## 3. 검증 순서 — 싼 것부터 (`../../CLAUDE.md` §3)

| 순위 | 수단 | 누가 | 비용 |
|---|---|---|---|
| 1 | **`dotnet test`** | **Claude 단독, 사용자 개입 0** | 수 초 |
| 2 | Unity C# 컴파일 | 에디터 자동 (창 포커스 시) | 수십 초 |
| 3 | Unity Test Runner (EditMode) | 사용자 | 분 단위 |
| 4 | 플레이 테스트 (감각·밸런스) | 사용자 | 분~시간 |
| 5 | 빌드 | — | 가장 비쌈 |

**1순위 명령** (PATH 미등록이라 전체 경로로 호출):

```
D:\Tools\dotnet\dotnet.exe test D:\GameDev\projects\Jianghu\Tools\CoreTests\CoreTests.csproj
```

**⚠ 1순위 통과가 Unity 통과를 보장하지는 않는다.** netstandard2.1 + C# 9 로 좁혀 두어 위험을 크게 줄였을 뿐이다. Core 를 손댄 뒤에는 2순위(에디터 창에 포커스 → 자동 재컴파일 → Console 확인)를 건너뛰지 말 것.

---

## 4. 지뢰 목록

밟은 것만 적는다.

- **⚠ Unity 는 창에 포커스가 가야 파일 변경을 감지한다.** Claude 가 스크립트를 고쳐도 에디터가 백그라운드면 재컴파일이 안 된다. 컴파일 확인이 필요하면 사용자가 에디터 창을 한 번 클릭해야 한다
- **⚠ 이 템플릿은 URP 다** (Built-in 아님). 인터넷 강좌 상당수가 구형 Built-in 기준이라 렌더링·셰이더·조명 자료를 그대로 따라 하면 안 된다. 새 렌더링 API 를 쓰기 전 `docs-lookup` 으로 URP 17.x 기준 대조
- **⚠ 에디터 버전을 올리지 말 것.** `6000.0.58f1 LTS` 고정. Hub 가 6.2 설치를 권해도 무시한다. 버전이 섞이면 "어느 버전 문법인지" 가 흐려진다(`../../CLAUDE.md` §4)
- **⚠ `Library\` `Temp\` `Logs\` 를 직접 읽지 말 것.** 훅이 차단한다. 로그 진단은 `diagnosis` sub-agent 에 위임
- **⚠⚠ "Deprecated packages" 경고 (2026-07-28 밟음·해결)** — 프로젝트를 열 때 뜨던 경고의 원인은 템플릿이 기본으로 넣어준 `com.unity.ide.rider 3.0.37` 이었다(에디터 6000.0.58f1 에서 재현되는 알려진 사례). **이 PC 에는 Rider 가 설치돼 있지 않아** 쓸모없는 패키지였으므로 `Packages/manifest.json` 에서 제거했다. 이 PC 의 IDE 는 **Visual Studio 2022 + VS Code** 이고 `com.unity.ide.visualstudio` 는 유지한다.
  - 교훈: 템플릿이 얹어주는 패키지 중 **안 쓰는 것은 제거해도 된다.** 다만 `packages-lock.json` 에서 다른 패키지가 의존하지 않는지 먼저 확인할 것

---

## 5. 진행 상태

⚠ Phase 계획은 구상안 확정에 따라 재정의됐다. 근거와 검증할 가설은 `../../docs/jianghu-design.md` 참조.

| Phase | 내용 | 상태 |
|---|---|---|
| **0** | 프로젝트 생성 · asmdef 골격 · dotnet 테스트 루프 · 결정론 RNG | **✅ 완료 (2026-07-27)** |
| **1** | 성향 성장곡선 · 유형 · 초식 모델 · **전투 해결기** | **✅⚠⚠ 완료 (2026-07-28)** — dotnet 46/46 · Unity 컴파일 에러 0 · Unity Test Runner 46/46 |
| **2** | 무공·문파·상태이상 체계 | **⚠⚠ 진행 중 (2026-08-05)** — 개별 수치 → **한자 형태소 조합 자동 유도**로 전환 완료. 사전 **88자** + 배경어 6자 · 파서 · 조합 규칙 · **무공 138종 작명 완료** · 카탈로그 수치 0줄. **치명 · 상태이상 · 방어군 4축 · 피해 눈금 · 속도 · 유형 숙달 · 절대경지 규칙 4종 · 기력 축 전부 연결 완료.** **✅ Unity 10차 확인 통과** — dotnet **200/200** · 컴파일 에러 0 · Test Runner 200/200(`Combat` **76** · `Martial` 115 · `Rng` 9). 남은 것은 **인계문서 §4-9-5**. 정의서 `../../docs/martial-resource-spec.md` |
| **3** | 전수 고리 — 주인공 무공 → 제자 → 비무 | 미착수. ⚠ **다대다 전투**가 여기 붙는다 — 범위 형태소 4자와 절대경지 "2회 행동" 은 그전엔 측정 불가 |
| **4** | Unity UI 바인딩 (텍스트/도형만, 2~3화면) | 미착수 |
| *(후순위)* | 아트 투입 — higgsfield | **로직 확정 전엔 뽑지 않는다** (생성 1회마다 비용) |

**⚠⚠ Phase 0 검증 완료 (2026-07-27)**: `dotnet test` 9/9 · Unity 컴파일 에러 0 · Unity Test Runner(EditMode) 9/9.
테스트 한 벌을 양쪽이 공유하는 구조가 **실증**됐다. 한글 테스트명도 양쪽 정상.

---

## 6. 코딩 관례

- **테스트 메서드명은 한글**로 쓴다. 결과 창을 그대로 읽을 수 있다 (실증 완료)
- **그 외 식별자(클래스·필드·메서드)는 영어**로 쓴다. C# 생태계 관례이고, 검색·스택트레이스에서 유리하다
- 무협 용어는 **XML 주석에 한글로** 병기한다 (`/// <summary>내공(內功) — 자원이자 초식 위력의 기반.</summary>`)
- 값이 왜 그 값인지(밸런스 상수·공식)는 **그 자리 주석에 근거를 남긴다** (`../../CLAUDE.md` §4)
