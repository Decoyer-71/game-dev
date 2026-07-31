---
name: jianghu-core-unity-split
description: "Jianghu 프로젝트에서 게임 규칙을 Unity 밖 순수 C# 으로 뺀 이유와 그 대가"
metadata: 
  node_type: memory
  type: project
  originSessionId: 1e00fcc4-f348-4368-908c-a5c72ffea729
  modified: 2026-07-27T13:41:22.011Z
---

Jianghu(무협 문파경영 RPG, `D:\GameDev\projects\Jianghu`)는 게임 규칙 전부를 `Assets/Scripts/Core` 의 **UnityEngine 미참조 순수 C#** 으로 두고, Unity 층은 화면·입력만 담당하는 껍데기로 유지한다. 2026-07-27 착수 시점에 정한 구조다.

**Why:** ① 사용자가 [[unity-beginner]] 라서, 게임 규칙이 Unity 에 묶이면 Unity 를 익히기 전까지 로직을 스스로 고칠 수 없다. ② Core 가 Unity 와 무관해야 `dotnet test` 로 **사용자 개입 없이** 검증 루프가 돌아간다 — Unity MCP 미연결 상태에서 이게 유일한 자동 피드백 루프다.

**How to apply:** Core 에 Unity 의존을 들이는 제안은 거절하고 Unity 층으로 밀어낸다. 대가는 Unity 편의 기능(ScriptableObject, 코루틴, Vector3 등)을 Core 에서 못 쓴다는 것 — 데이터는 Unity 층에서 Core 의 순수 타입으로 변환해 넘긴다. 강제 수단은 `Jianghu.Core.asmdef` 의 `noEngineReferences: true` 와 netstandard2.1/C# 9 로 고정한 `Tools/JianghuCore/JianghuCore.csproj`.
