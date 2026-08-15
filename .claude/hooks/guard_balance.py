# -*- coding: utf-8 -*-
"""
PostToolUse 훅 — 밸런스 파일을 고치면 `balance-audit` 를 상기시킨다.

⚠⚠ 왜 훅인가 (2026-08-05 신설)
────────────────────────────────────────────────────────────────────────────
스킬은 **모델이 불러야 로드되는 지침**이라 결국 기억에 의존한다. 그런데 이 작업공간에서
기억에 의존한 규율은 이미 여러 번 실패했다 — HANDOFF §4-3-6 의 실수 목록이 열 개가 넘는데도
2026-08-04~05 이틀에 **"한 곳만 보고 전체 결론을 낸다"** 는 같은 병을 네 번 밟았다.

그래서 상기(想起)를 **harness 가 하게** 옮긴다. 이 훅은 밸런스에 영향을 주는 파일이
수정되는 것을 보면, 모델에게 "대조 없이 보고하지 마라" 를 **그 자리에서** 들이민다.

⚠ 이것은 '차단' 이 아니라 '상기' 다. PostToolUse 라 이미 수정은 끝난 뒤이고, 막을 수도 없다.
  막지 않는 이유는 두 가지다:
    ⓐ 측정을 위해 **일부러 임시로 고쳤다가 되돌리는** 작업이 정상 절차다(HANDOFF §4-3-7).
       그걸 막으면 실험 자체가 불가능해진다.
    ⓑ 이 훅의 목적은 *수정을 막는 것*이 아니라 *보고를 부실하게 하는 것*을 막는 것이다.

⚠ fail-open 을 의도적으로 유지한다(guard_logs.py 와 같은 방침).
  훅 자체의 버그로 세션이 마비되는 쪽이 더 나쁘다. 이건 '실수 방지 장치'이지 '보안 경계'가 아니다.
────────────────────────────────────────────────────────────────────────────
"""
import sys
import json

# 고치면 승률이 움직이는 파일들. 경로 일부만 맞으면 된다(구분자는 아래서 정규화한다).
BALANCE_PATHS = (
    "core/martial/morphemes/morphemedictionary.cs",
    "core/martial/alignmentcurve.cs",
    "core/martial/disciplinecurve.cs",
    "core/martial/martialartcatalog.cs",
    "core/combat/combatresolver.cs",
    "core/combat/combatant.cs",
    "core/characters/characterstats.cs",
)

MESSAGE = (
    "⚠⚠ 밸런스 파일을 고쳤다 — 보고 전에 `balance-audit` 를 거쳐라.\n"
    "   기준선 대조:  D:/Tools/dotnet/dotnet.exe run --project "
    "projects/Jianghu/Tools/Sandbox/Sandbox.csproj -c Release -- "
    "--compare docs/baseline-metrics.txt\n"
    "   ⚠ 승률표를 grep 으로 일부만 보고 결론 내지 마라. 그 실수를 이틀에 네 번 밟았고,\n"
    "     마지막엔 같은 출력 안에 있던 전승무학 지배(79.7%)를 놓쳤다.\n"
    "   ⚠ 측정용 임시 변경이면 **되돌린 뒤 `git diff` 로 0줄을 확인**하는 것까지가 절차다."
)


def main():
    raw = sys.stdin.read()
    if raw.startswith("\ufeff"):        # BOM 이 붙으면 json.loads 가 터진다(guard_logs.py 사고)
        raw = raw.lstrip("\ufeff")
    try:
        payload = json.loads(raw)
    except Exception:
        return 0                        # fail-open

    tool = payload.get("tool_name") or ""
    if tool not in ("Edit", "Write", "NotebookEdit"):
        return 0

    tool_input = payload.get("tool_input") or {}
    path = str(tool_input.get("file_path") or "").replace("\\", "/").lower()
    if not path:
        return 0

    for needle in BALANCE_PATHS:
        if needle in path:
            # PostToolUse 는 stdout 을 모델에게 피드백으로 전달한다.
            sys.stdout.write(MESSAGE)
            return 0
    return 0


if __name__ == "__main__":
    sys.exit(main())
