# -*- coding: utf-8 -*-
# SessionStart 훅 (CLAUDE.md §5 · §8) — 새 세션마다 메인 세션 하드룰 + **문서 인덱스**를 자동 주입.
# stdout 이 세션 컨텍스트로 삽입된다. CK3 작업공간의 session_banner.py 이식본.
#
# ⚠ memory 는 관련성 판단에 따라 불려오는 구조라 항상 주입된다는 보장이 없다.
#   반드시 지켜야 하는 규약은 memory 가 아니라 여기에 둔다(매 세션 확정 주입).
#
# ⚠⚠ 2026-09-10 개편 — 그전 이 훅은 **하드코딩 print 뿐이라 파일을 하나도 안 읽었다.**
#   그래서 사용자가 확정해 둔 게임 근간 규칙이 docs/concepts/ 에 적혀 있는데도 세션이 그것을
#   모른 채 설계를 처음부터 다시 만드는 사고가 났다(2026-09-07 · 무공 슬롯 제한 —
#   docs/concepts/martial-art-slots.md 에 2026-08-04 부터 있었다).
#   → 배너가 docs/ 를 **직접 스캔해** 인덱스를 만든다. 손으로 관리하지 않으므로 **낡을 수 없다.**
#
#   ⚠ 이 훅이 강제할 수 있는 것은 **주입**이지 **읽기**가 아니다. 인덱스는 "찾아야 알 수 있는 것"을
#     "이미 거기 있는 것"으로 바꿀 뿐이고, 여는 판단은 여전히 세션 몫이다.
#
# ⚠⚠ **fail-open 이다.** guard_logs.py·guard_balance.py 선례대로 무슨 일이 있어도 exit 0 으로 끝낸다.
#   배너가 세션 시작을 막는 일은 없어야 한다. 인덱스 생성이 실패하면 하드룰만 내고 조용히 넘어간다.
#   ⚠ 단 **개별 문서의 판정 파싱 실패는 조용히 넘기지 않는다** — 목록에서 사라지면 지금 병이
#     그대로 재발하므로 `⚠ 판정 형식 불명` 으로 찍는다.
import os
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

# .claude/hooks/session_banner.py → D:/GameDev
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DOCS = os.path.join(ROOT, "docs")
CONCEPTS = os.path.join(DOCS, "concepts")
PROJECTS = os.path.join(ROOT, "projects")

HEAD_LINES = 40      # 제목·갱신일·판정은 문서 앞머리에 있다. 전문을 읽지 않는다
MAX_ENTRIES = 40     # 인덱스가 이 이상 길어지면 배너가 스스로 안 읽히는 문서가 된다
TITLE_WIDTH = 58
VERDICT_WIDTH = 42

TITLE_RE = re.compile(r"^#\s+(.*\S)")
UPDATED_RE = re.compile(r"갱신\s*[:·]?\s*(\d{4}-\d{2}-\d{2})")
# 표 형식:   | **판정** | ⏸ 보류 — ... |
VERDICT_TABLE_RE = re.compile(r"^\|\s*\*\*판정\*\*\s*\|\s*(.+?)\s*\|\s*$")
# 인라인:    ... · 판정 **⏸ 보류 (사용자 결정 대기)** · ...
#   ⚠ charge-qi-model.md 가 실제로 이 형식이다. 표만 찾으면 조용히 빠진다.
VERDICT_INLINE_RE = re.compile(r"판정\s*\*\*(.+?)\*\*")


def read_head(path):
    """문서 앞머리 몇 줄만 읽는다. BOM 이 붙은 파일이 있어 utf-8-sig 로 연다."""
    lines = []
    with open(path, "r", encoding="utf-8-sig", errors="replace") as f:
        for i, line in enumerate(f):
            if i >= HEAD_LINES:
                break
            lines.append(line.rstrip("\n"))
    return lines


def strip_md(text):
    text = re.sub(r"\*\*(.+?)\*\*", r"\1", text)
    text = re.sub(r"~~(.+?)~~", r"\1", text)
    text = re.sub(r"`(.+?)`", r"\1", text)
    text = re.sub(r"\[(.+?)\]\([^)]*\)", r"\1", text)
    return " ".join(text.split())


def clip(text, width):
    text = " ".join(text.split())
    return text if len(text) <= width else text[: width - 1] + "…"


def title_of(lines, fallback):
    for line in lines:
        m = TITLE_RE.match(line)
        if m:
            return strip_md(m.group(1))
    return fallback


def updated_of(lines):
    for line in lines:
        m = UPDATED_RE.search(line)
        if m:
            return m.group(1)
    return None


def verdict_of(lines):
    """판정을 찾는다. 못 찾으면 None — 호출부가 '형식 불명' 으로 찍는다."""
    for line in lines:
        m = VERDICT_TABLE_RE.match(line)
        if m:
            return strip_md(m.group(1))
    for line in lines:
        m = VERDICT_INLINE_RE.search(line)
        if m:
            return strip_md(m.group(1))
    return None


def md_files(directory):
    if not os.path.isdir(directory):
        return []
    names = [n for n in os.listdir(directory) if n.lower().endswith(".md")]
    return sorted(names)


def build_index():
    """docs/ 를 스캔해 인덱스 줄들을 만든다. 실패는 호출부가 삼킨다."""
    out = []
    count = 0

    # ── 설계·규율 문서 (docs/*.md)
    top = md_files(DOCS)
    if top:
        out.append("[docs/ — 설계·규율 문서]")
        # HANDOFF 를 맨 위로. 프로젝트 인계의 착수점이다
        top.sort(key=lambda n: (n.upper() != "HANDOFF.MD", n.upper()))
        for name in top:
            if count >= MAX_ENTRIES:
                break
            lines = read_head(os.path.join(DOCS, name))
            stamp = updated_of(lines)
            suffix = ("  [갱신 %s]" % stamp) if stamp else ""
            out.append("  %-28s %s%s" % (name, clip(title_of(lines, name), TITLE_WIDTH), suffix))
            count += 1

    # ── 구상안 (docs/concepts/*.md) — 판정을 반드시 함께 낸다
    #    ⚠⚠ 2026-09-07 사고가 난 자리다. 판정이 '보류'여도 목록에는 뜬다 —
    #       보류 딱지가 붙었다고 규칙이 아닌 것은 아니다.
    concepts = [n for n in md_files(CONCEPTS) if not n.startswith("_")]
    if concepts:
        out.append("")
        out.append("[docs/concepts/ — 구상안 · ⚠ 판정이 '보류'여도 근간 규칙일 수 있다. 관련 주제면 열어라]")
        for name in concepts:
            if count >= MAX_ENTRIES:
                break
            lines = read_head(os.path.join(CONCEPTS, name))
            verdict = verdict_of(lines)
            mark = clip(verdict, VERDICT_WIDTH) if verdict else "⚠ 판정 형식 불명 — 문서를 열어 확인할 것"
            out.append("  %-34s %s" % (name, clip(title_of(lines, name), TITLE_WIDTH)))
            out.append("  %-34s   └ %s" % ("", mark))
            count += 1

    # ── 프로젝트 고유 규율 (자동 주입 대상이 아니다)
    proj = []
    if os.path.isdir(PROJECTS):
        for name in sorted(os.listdir(PROJECTS)):
            path = os.path.join(PROJECTS, name, "CLAUDE.md")
            if os.path.isfile(path):
                proj.append("projects/%s/CLAUDE.md" % name)
    if proj:
        out.append("")
        out.append("[프로젝트 고유 규율 — ⚠ 루트 CLAUDE.md 와 달리 자동 주입되지 않는다. 직접 읽어라]")
        for rel in proj:
            out.append("  %s" % rel)

    if count >= MAX_ENTRIES:
        out.append("")
        out.append("  ⚠ 인덱스가 %d개에서 잘렸다. 문서가 너무 많다 — 정리 대상이다." % MAX_ENTRIES)

    return out


HARD_RULES = (
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
)

INDEX_HEADER = (
    "\n"
    "-------- 문서 인덱스 (훅이 docs/ 를 스캔해 매 세션 생성 — 손으로 고치지 않는다) --------\n"
    "⚠⚠ 설계·규칙을 답하기 전에 **관련 문서가 이미 있는지 이 목록에서 먼저 본다.**\n"
    "   2026-09-07 에 사용자가 확정해 둔 규칙을 못 찾고 설계를 다시 만든 사고가 있었다.\n"
)

FOOTER = "=====================================================================\n"


def main():
    sys.stdout.write(HARD_RULES)
    try:
        lines = build_index()
        if lines:
            sys.stdout.write(INDEX_HEADER)
            sys.stdout.write("\n".join(lines) + "\n")
        else:
            sys.stdout.write("\n⚠ 문서 인덱스를 만들지 못했다 — docs/ 를 직접 확인할 것.\n")
    except Exception as exc:
        # fail-open. 인덱스가 없어도 세션은 시작돼야 한다.
        sys.stdout.write("\n⚠ 문서 인덱스 생성 실패(%s) — docs/ 를 직접 확인할 것.\n" % exc.__class__.__name__)
    sys.stdout.write(FOOTER)


try:
    main()
except Exception:
    pass
sys.exit(0)
