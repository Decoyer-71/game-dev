---
name: concept-review-process
description: 구상안 검토 3단계와 존댓말 규약은 CLAUDE.md §8 에 명문화돼 있다 — 그 배경과 강제 수단
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 1e00fcc4-f348-4368-908c-a5c72ffea729
  modified: 2026-07-27T14:09:47.301Z
---

게임 컨셉 구상안을 받으면 **사전조사 → 구현 가능성 → 재미 검토** 3단계를 거쳐 답하고, **존댓말**을 쓴다 (2026-07-27 지시).

절차 본문은 `D:\GameDev\CLAUDE.md` §8 에 있고, `.claude/hooks/session_banner.py` 가 매 세션 자동 주입한다. **여기에 내용을 중복해 적지 않는다** — 규약이 바뀌면 CLAUDE.md 만 고치면 되게 둔다.

**Why:** 사용자가 Phase 0 인프라를 먼저 깔아둔 이유가 "프로토타입 제작을 서두르지 않고 컨셉을 제대로 검토하기 위해서"다. 조급하게 구현으로 넘어가는 것을 명시적으로 거부했다. 그리고 규약을 memory 에만 두면 recall 여부에 좌우되므로, 반드시 지켜야 하는 것은 배너로 확정 주입하도록 옮겼다.

**How to apply:** 구상안을 받자마자 코드를 쓰지 않는다. 3단계를 마치고 판단을 제시한 뒤 진행 여부를 확인받는다. 재미 검토에서는 듣기 좋은 말이 아니라 구조적 약점을 지적한다 — 이전에 "재미 가설이 없다"는 지적이 실제로 방향 전환을 만들었다. 사전조사는 `game-research` 에 위임하고 `verify` 를 통과시킨 뒤 채택한다([[jianghu-core-unity-split]] 과 같은 위임 규율).
