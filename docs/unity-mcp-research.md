# Unity MCP 조사 결과

**조사일 2026-07-27** · 웹 리서치 sub-agent 위임 + 로컬 환경 실측
관련: `../CLAUDE.md` §2(환경 실측값), §6(지뢰 목록)

---

## 요약 판정

| 질문 | 답 |
|---|---|
| Unity MCP 가 존재하는가 | **존재한다. Unity 공식 제공물까지 있다** |
| 지금 이 환경에서 쓸 수 있는가 | **아니다 — 미연결.** 사용자가 설정해야 함 |
| 연결하면 게임을 만들어 주는가 | **아니다.** 에디터 자동화 원격조종 + 오류 피드백 루프이지 게임 제작기가 아니다 |

---

## 1. 실존 목록

### 공식 (Unity Technologies)
- **Unity MCP Server** — `com.unity.ai.assistant` 패키지에 포함. Unity AI 오픈 베타의 일부로 **2026-05-04 출시**, 버전 `2.10.0-pre.1` (**프리릴리스**)
- 요구: **Unity 6 (6000.0) 이상**. Pro/Enterprise/Industry 는 기본 포함, Personal 은 14일 1,000 크레딧 체험
- 출처: [docs.unity3d.com — Unity MCP Get Started](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.10/manual/integration/unity-mcp-get-started.html) (공식)
- ⚠ **혼동 주의**: Unite Seoul(2026-07-21)에서 Unity 7용 **"무료" MCP** 를 별도 발표 — 얼리베타 2026-12, 정식 2027-Q1. 현재 베타판과의 관계(승계인지 별물인지) **미확인**

### 커뮤니티 (GitHub API 실측, 2026-07-27 기준 — 전부 활성)

| 저장소 | ★ | 최종 푸시 | 라이선스 | 비고 |
|---|---|---|---|---|
| [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) | 12,875 | 07-13 | MIT | 사실상 표준. Unity 2021.3~6.x, 47툴 |
| [IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) | 3,681 | 07-27 | Apache-2.0 | 70+툴, 런타임(빌드된 게임)에서도 동작. **경로 공백 불가** |
| [CoderGamester/mcp-unity](https://github.com/CoderGamester/mcp-unity) | 1,845 | 07-24 | MIT | Unity 6+, Node.js 18+ |
| [AnkleBreaker-Studio/unity-mcp-server](https://github.com/AnkleBreaker-Studio/unity-mcp-server) | 351 | 07-24 | - | 268툴 |

---

## 2. 작동 구조 — **에디터가 설치·실행 중이어야 한다**

전 구현체 공통. **MCP 서버는 에디터를 대체하지 않고 원격조종할 뿐이다.**

- **공식**: 에디터 로드 시 브리지 자동 기동 → named pipe(Win)/Unix socket IPC → `~/.unity/relay/` 릴레이 바이너리가 MCP 서버 역할
- **커뮤니티**: 에디터 내 브리지 패키지(UPM 설치) + 외부 서버 프로세스. CoderGamester = WebSocket:8090 + Node.js, CoplayDev = Python 3.10+/uv, IvanMurzak = stdio/streamableHttp

→ **Unity 미설치 환경에서는 사용 불가. 브리지 패키지 설치 필수.**

---

## 3. 기능 경계

**자동화되는 것**: 씬 생성·로드·저장 / GameObject CRUD·트랜스폼·리페어런팅 / 컴포넌트 값 수정 / 프리팹·머티리얼 생성 / C# 스크립트 파일 생성·편집 / **콘솔 로그 읽기(= 오류 피드백 루프 성립)** / 플레이모드 on·off / 테스트 실행 / 스크립트 리컴파일 / 패키지 추가 / 에디터 메뉴 항목 실행 / 빌드·프로파일링

**사람 몫으로 남는 것**: **3D 모델·텍스처·사운드 등 실 에셋 제작**(Unity 는 이걸 별도 `com.unity.ai.generators` 패키지로 분리. ⚠ MCP 툴로 노출되는지 미확인) / 씬의 미세 배치·감각적 튜닝 / 게임 프레임워크 설계 / 아트 디렉션 / 밸런싱

**MCP 는 에셋을 배치할 뿐 제작하지 않는다.**

---

## 4. 한계

- **규모** — Unity 는 "같은 걸 하는 방법이 여러 개이고 버전마다 바뀌어" LLM 이 혼동한다. 사용자가 *무엇*이 아니라 *어떻게*를 지시해야 해서 **도메인 지식이 여전히 필요**. 씬 상태를 컨텍스트에 넣는 구조라 **토큰 소모가 크다**(실사용 후기: 토큰 한도로 작업 중단 반복)
- **에셋** — 여전히 사람/별도 생성툴 몫
- **안정성** — 전부 prerelease/pre-1.0 급. 환경 제약 있음(경로 공백 등)
- **⚠ 법적 리스크** — 2026-06-30 Unity ToS 17.2(ff)(gg) 개정이 서드파티 MCP 금지로 읽혀 논란. Unity 는 Reddit 댓글로 "로컬 사용은 무관"이라 부인했으나 **공식 성명·약관 문구 수정은 없다**. 일부 프로젝트는 선제적으로 저장소를 내렸다 ([roboin.io](https://roboin.io/article/en/2026/07/02/unity-denies-concerns-over-ban-on-third-party-ai/), 2차 보도)
- **⚠⚠ 보안 — 사실상 임의 코드 실행 권한을 주는 구조** — C# 작성 + 리컴파일 + `execute_menu_item` 조합이면 에디터 권한 전체다. 일부 구현은 `execute_code` 툴을 직접 노출하며, 그 문서가 **"프롬프트 인젝션으로 악성 코드 실행 가능 — 이건 취약점 신고 대상이 아니라 의도된 설계"** 라고 명시한다. 공식판은 외부 클라이언트 최초 접속 시 Accept/Deny 다이얼로그 요구(단 Unity AI Gateway 경유는 무승인 자동 허용)

---

## 5. "LLM 이 MCP 로 게임을 처음부터 끝까지" — **아니다**

- **데모 수준은 실제로 된다**: 큐브·프리미티브 프로토타입, 룰 기반 로직 생성
- **실증 반례**: Claude/Cursor 로 **오델로**(가장 단순한 룰 게임) 완성 시도 → 완전 자동화 실패. 저자 결론 "씬 편집과 미세 조정은 결국 손이 빠르다", 현실적 용도는 **디버깅과 기존 코드 리팩터 보조** ([note.com/atali](https://note.com/atali/n/n64b709af8411?hl=en), 개인 후기)
- **마케팅 vs 실사용 괴리**: "330+ 툴", "AI로 게임을 만드세요" 류는 **툴 개수 = 능력**이라는 착시. 툴이 많다고 설계 판단이 생기지 않는다. **출시 가능 수준 게임을 MCP 주도로 만든 검증 사례는 발견되지 않았다**

---

## 6. 이 환경의 현재 상태 (로컬 실측 2026-07-27)

| 항목 | 결과 |
|---|---|
| Unity 에디터 | **6000.0.58f1 설치됨** — 공식 MCP 의 "Unity 6 이상" 조건 충족 |
| Unity Hub | 설치됨 |
| Unity MCP 연결 | **없음**. 커넥터 레지스트리에서 `unity`/`game engine`/`godot`/`unreal` 검색 **0건** |
| MCP 설정 파일 | `claude_desktop_config.json`, `.claude.json`, `.claude/settings.json` 어디에도 `mcpServers` 항목 없음 |
| dotnet | 런타임만, **SDK 없음** |

→ **요구 조건(Unity 6)은 갖춰져 있고, 막히는 건 연결 설정뿐이다.** 브리지 패키지 설치와 MCP 서버 등록은 **사용자가 직접** 해야 한다.

---

## 7. MCP 없이 지금 가능한 것

Unity 프로젝트는 결국 **파일**이다. C# 스크립트, `.unity` 씬, `.prefab`, `.asset`, `ProjectSettings` 가 전부 텍스트(YAML) 다.

- **가능**: C# 스크립트 작성·편집, 프로젝트 파일 직접 편집, 배치 모드 실행(`Unity.exe -batchmode -executeMethod`)
- **불가(MCP 필요)**: 씬 그래프 실시간 조작, 에디터 콘솔 즉시 읽기, 플레이 모드 제어 — 즉 **피드백 루프**

CK3 모드 작업의 `error.log` 워크플로와 구조가 같다. 차이는 **CK3 는 전부 텍스트라 파일 직접 편집으로 충분한 반면, Unity 는 씬·프리팹이 에디터를 거쳐야 해서 브리지가 필수**라는 점이다.

---

## 출처

공식: [Unity MCP Get Started](https://docs.unity3d.com/Packages/com.unity.ai.assistant@2.10/manual/integration/unity-mcp-get-started.html)
커뮤니티 도구: [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) · [IvanMurzak/Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) · [CoderGamester/mcp-unity](https://github.com/CoderGamester/mcp-unity) · [AnkleBreaker-Studio/unity-mcp-server](https://github.com/AnkleBreaker-Studio/unity-mcp-server)
2차 보도: [egamers.io — Unity 7 로드맵](https://egamers.io/unity-7-roadmap-public-api-cli-and-a-free-mcp-bring-the-editor-to-non-users-and-coding-agents/) · [roboin.io — ToS 논란](https://roboin.io/article/en/2026/07/02/unity-denies-concerns-over-ban-on-third-party-ai/)
실사용 후기: [note.com/atali](https://note.com/atali/n/n64b709af8411?hl=en)
