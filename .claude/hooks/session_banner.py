# -*- coding: utf-8 -*-
# SessionStart 훅 (CLAUDE.md §5 · §8) — 새 세션마다 메인 세션 하드룰을 컨텍스트에 자동 주입.
# stdout 이 세션 컨텍스트로 삽입된다. CK3 작업공간의 session_banner.py 이식본.
#
# ⚠ memory 는 관련성 판단에 따라 불려오는 구조라 항상 주입된다는 보장이 없다.
#   반드시 지켜야 하는 규약은 memory 가 아니라 여기에 둔다(매 세션 확정 주입).
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

print(
    "\n"
    "======== 메인 세션 하드룰 (D:/GameDev/CLAUDE.md — 자동 주입) ========\n"
    "[말투] 사용자에게는 **존댓말**로 답한다. (§8-5)\n"
    "\n"
    "[§8 구상안 검토 3단계] 게임 컨셉 구상안을 받으면 코드부터 쓰지 말고 순서대로:\n"
    "  1) 사전조사      -> game-research 위임, 결론을 verify 통과시킨 뒤 채택\n"
    "  2) 구현 가능성   -> §9 비용 판정표 근거. '가능/불가'가 아니라 '얼마나 드는가'\n"
    "  3) 재미 검토     -> 플레이어의 결정은 어디에 있는가 / 결과가 얼마나 빨리 돌아오는가\n"
    "                      / 그 재미가 이 소재여야만 하는 이유가 있는가\n"
    "  4) 결과는 docs/concepts/<슬러그>.md 에 채택·보류·기각 판정과 이유까지 기록\n"
    "\n"
    "[§5 위임] 아래는 반드시 sub-agent 위임. 메인 세션 직접 수행 금지:\n"
    "  · Unity API·패키지 문서 대조        -> docs-lookup\n"
    "  · 콘솔/빌드 로그 진단·오류 규명      -> diagnosis\n"
    "  · 게임 디자인 리서치(선행작·실패사례) -> game-research\n"
    "  · sub-agent 산출물 검증             -> verify\n"
    "메인 세션은 '요약·판정'만 받아 설계/구현 판단. 원문 덤프를 컨텍스트에 싣지 말 것.\n"
    "\n"
    "· 검증은 싼 것부터: dotnet test -> Unity 컴파일 -> Test Runner -> 플레이 -> 빌드 (§3)\n"
    "· PreToolUse 훅이 Unity 로그/Library/Temp/obj 직접 열람을 차단(deny)한다.\n"
    "  (작업공간 D:/GameDev 하위에만 적용 — 스크래치패드는 차단 대상이 아니다)\n"
    "· 프로젝트는 D:\\GameDev\\projects\\ 아래. C 드라이브 여유 5.9GB 라 C 에 만들지 말 것.\n"
    "=====================================================================\n"
)
