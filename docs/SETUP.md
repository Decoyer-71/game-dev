# 새 PC 셋업 — 이 작업공간을 다른 컴퓨터에서 복원하기

**작성 2026-07-31** · 저장소를 클론한 직후 이 문서부터 따라간다. 다 끝나면 [`HANDOFF.md`](HANDOFF.md) 로 넘어간다.

---

## 0. 30초 요약

| | |
|---|---|
| **git 이 나르는 것** | 설계 문서 · 소스 전부 · `.claude/` 규율(agent·훅·스킬·메모리) · Unity 프로젝트 설정 |
| **git 이 못 나르는 것** | **툴 설치**(Unity·.NET·gh·Python) · `Library/` · MCP 연결 · 대화 기록 |
| **⚠⚠ 가장 중요** | **`D:\GameDev` 와 `D:\Tools\dotnet` 경로가 하드코딩돼 있다.** 같은 경로를 쓰면 아무것도 안 고쳐도 된다 |
| **완료 판정** | `dotnet test` 가 **159/159** 를 내면 끝이다 (2026-08-01 기준) |

---

## 1. ⚠⚠ 경로 제약 — 먼저 읽을 것

세 곳이 절대경로를 박고 있다. **같은 경로를 쓰는 것이 압도적으로 싸다.**

| 파일 | 박혀 있는 값 | 안 맞으면 생기는 일 |
|---|---|---|
| `.claude/settings.json` | `D:/GameDev/.claude/hooks/*.py` · `D:\Tools\dotnet\dotnet.exe` | **훅이 안 돌고**(§5 로그 가드 무력화) dotnet 권한 규칙이 안 먹어 매번 승인을 묻는다 |
| `projects/Jianghu/Tools/nuget.config` | `D:\GameDev\.nuget-packages` | 패키지 캐시가 C 로 돌아간다 — C 여유가 적으면 문제가 된다 |
| `CLAUDE.md` · `docs/*.md` | 명령 예시 전반 | 문서의 명령을 복사해 붙이면 실패한다 |

→ **권장: 새 PC 에서도 `D:\GameDev` 에 클론하고 .NET SDK 를 `D:\Tools\dotnet` 에 둔다.**
다른 경로를 써야 한다면 §7 을 본다.

---

## 2. 설치할 것

| 도구 | 버전 | 위치 | 왜 |
|---|---|---|---|
| **Unity Hub + 에디터** | **6000.0.58f1 LTS** — ⚠ 버전을 올리지 말 것 | Hub 기본 위치 | 프로젝트가 이 버전으로 고정돼 있다 |
| **.NET SDK** | **8.x** | **`D:\Tools\dotnet`** | 검증 1순위 `dotnet test` 가 이걸 쓴다. ⚠ 기존 PC 는 `-NoPath` 로 설치해 **PATH 에 없다** — 문서의 명령이 전부 전체 경로인 이유 |
| **Python 3** | — | PATH 등록 | `.claude/hooks/` 두 개를 `python` 명령으로 호출한다 |
| **`gh` CLI** | 2.x | `D:\Tools\gh\bin\gh.exe` | PR 생성용(`../CLAUDE.md` §5-B). 없어도 당장 작업은 된다 |
| IDE | Visual Studio 2022 또는 VS Code | — | ⚠ Rider 는 안 쓴다(관련 패키지를 제거해 뒀다) |

**⚠⚠ `winget install` 로 MSI 패키지를 깔지 말 것.** 관리자 권한이 필요해 **UAC 대화상자에서 무한 대기**하고, 증상이 *출력이 완전히 빈 채로 타임아웃*이라 원인이 안 보인다(2026-07-30 실측). **포터블 zip 을 `D:\Tools\<도구>` 에 풀고 사용자 PATH 에 등록한다.**

**⚠ 이 세션에서 새로 등록한 PATH 는 이미 열려 있는 셸에 반영되지 않는다.** 같은 대화 안에서는 계속 전체 경로로 호출해야 한다.

---

## 3. 클론 + 브랜치

저장소는 **PRIVATE** 이라 GitHub 인증이 먼저 필요하다.
클론하면 `main` 이 나오는데 **작업은 전부 `feat/martial-engine` 에 있다.** 반드시 갈아탄다.

```bash
git clone https://github.com/Decoyer-71/game-dev.git D:/GameDev && cd D:/GameDev && git checkout feat/martial-engine
```

---

## 4. ⚠ 메모리 복원 — 잊기 쉬운 단계

`.claude/memory/` 의 7개 파일은 **저장소 안에 둔 이동용 사본**이다. Claude 가 실제로 읽는 곳은 저장소 밖이라 **직접 복사해 넣어야 한다.**

```bash
mkdir -p "$USERPROFILE/.claude/projects/D--GameDev/memory" && cp D:/GameDev/.claude/memory/*.md "$USERPROFILE/.claude/projects/D--GameDev/memory/"
```

⚠ 대상 폴더 이름 `D--GameDev` 는 작업공간 경로 `D:\GameDev` 에서 나온 것이다. **다른 경로에 클론했다면 폴더 이름도 달라진다** — §1 이 같은 경로를 권하는 이유가 하나 더 있는 셈이다.

⚠ 이 사본이 없어도 치명적이진 않다. 내용 대부분이 `CLAUDE.md`·`HANDOFF.md` 에도 적혀 있다. 다만 *"Unity 초심자다 · 응답이 비동기다"* 같은 **사용자 맥락**은 메모리에만 있다.

---

## 5. 검증 — 여기까지 오면 셋업이 끝난 것이다

가장 싼 검증부터 돈다(`../CLAUDE.md` §3). **첫 실행은 NuGet 패키지를 복원하느라 좀 걸린다.**

```bash
D:/Tools/dotnet/dotnet.exe test D:/GameDev/projects/Jianghu/Tools/CoreTests/CoreTests.csproj
```

**159/159 통과가 나오면 로직 쪽 셋업은 완료다.** (2026-08-01 기준 — 그 뒤 테스트가 늘었다면 `HANDOFF.md` §0 의 숫자를 본다)

밸런싱 판정 도구도 같이 확인해 둔다:

```bash
D:/Tools/dotnet/dotnet.exe run --project D:/GameDev/projects/Jianghu/Tools/Sandbox/Sandbox.csproj -c Release
```

---

## 6. Unity 첫 실행

⚠ **Unity 층은 아직 아무것도 안 붙어 있다**(Phase 4 미착수). 지금 단계에서 Unity 는 *컴파일이 깨지지 않았는지* 확인하는 용도다. 급하지 않다면 §5 만으로도 작업을 이어갈 수 있다.

1. **Unity Hub** 를 연다 → `Add` → `Add project from disk` → `D:\GameDev\projects\Jianghu` 선택
2. 목록에 뜬 프로젝트를 클릭해 연다
3. **⚠ 처음 여는 데 수 분~십수 분 걸린다.** `Library/` 폴더(약 1.8GB)를 통째로 다시 만들기 때문이다. 저장소에 없는 게 정상이다 — 있으면 GitHub 한도를 넘긴다
4. 열리면 하단 **Console** 탭을 본다. **에러 0** 이어야 한다
5. 테스트까지 확인하려면 상단 메뉴 **Window → General → Test Runner** → **EditMode** 탭 → **Run All**

**⚠ Unity 는 창에 포커스가 가야 파일 변경을 감지한다.** 백그라운드에 두면 스크립트를 고쳐도 재컴파일이 안 된다.

---

## 7. 다른 경로를 쓰려면 — 고칠 3곳

§1 표의 세 파일을 고친다. **훅과 테스트가 도는지 반드시 §5 로 확인한 뒤 작업을 시작한다.**

1. `.claude/settings.json` — `hooks[].command` 의 경로 2개 + `permissions.allow` 의 dotnet 경로 4개
2. `projects/Jianghu/Tools/nuget.config` — `globalPackagesFolder`
3. `CLAUDE.md` §1·§2 와 `docs/HANDOFF.md` §2·§8 의 경로 기재

⚠ 이 변경은 **모든 PC 에 영향을 준다.** 저장소를 공유하는 다른 PC 가 있다면 경로를 통일하는 편이 낫다.

---

## 8. git 이 나르지 않는 것 — 전체 목록

| 항목 | 어떻게 되찾나 |
|---|---|
| **툴** (Unity · .NET · gh · Python) | §2 대로 설치 |
| **`Library/` `Temp/` `obj/` `bin/`** | Unity·dotnet 이 자동 재생성 |
| **NuGet 캐시** (`.nuget-packages/`) | `dotnet test` 첫 실행 시 자동 복원 |
| **메모리 7개** | §4 대로 복사 |
| **대화 기록** | 되찾을 수 없다 — **`HANDOFF.md` 가 이걸 대신하려고 만든 문서다** |
| **higgsfield MCP 연결** | Claude 앱에서 재연결. ⚠ 크레딧(2026-07-30 실측 1,200)은 계정에 붙어 있어 잃지 않는다 |
| **Unity 2D 템플릿 tgz** | 프로젝트가 이미 있으므로 불필요 |

---

## 9. 셋업이 끝나면

[`HANDOFF.md`](HANDOFF.md) 로 간다. 지금 어디까지 왔고 다음에 무엇을 할지가 거기 있다.
