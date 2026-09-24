# -*- coding: utf-8 -*-
# PreToolUse 훅 (CLAUDE.md §5-B / L5) — **`main` 에서의 `git commit` 을 차단한다.**
#
# ⚠⚠ 왜 생겼나 — 2026-09-24, PR #7 을 병합한 뒤 `main` 을 체크아웃한 채로 작업을 이어가다
#   **`main` 에 직접 커밋하고 푸시했다.** `confirm-sync` 스킬에 *"브랜치를 확인한다"* 단계가
#   이미 있었는데 건너뛴 것이다. 규율이 기억에 기대면 언젠가 샌다 —
#   `../../CLAUDE.md` §1 의 *"실수를 사람 기억에 맡기지 않는다"* 와 같은 선이다.
#
# ⚠⚠ **차단(deny)이다.** §5-D 의 밸런스 훅은 일부러 차단하지 않는데(측정용 임시 수정이
#   정상 절차라서), 여기는 다르다 — **`main` 직접 커밋에는 정상 절차가 아예 없다.**
#   스코프 브랜치를 만들거나 갈아타면 된다.
#
# ⚠ **`commit` 만 막는다.** `push`·`merge`·`pull` 은 §5-B 의 정상 병합 절차에 쓰이므로 건드리지 않는다
#   (`git checkout main && git pull --ff-only` 는 규정된 흐름이다).
#   ⚠ `revert`·`cherry-pick`·`rebase` 도 커밋을 만들지만 막지 않는다 — **망가진 `main` 을 고치는
#     길까지 막으면 안 된다.** 오탐을 줄이는 쪽을 택했다.
#
# ⚠ **sub-agent 를 면제하지 않는다.** `guard_logs.py` 는 면제하는데(원문 열람이 위임의 목적이라서),
#   여기는 그런 이유가 없다 — 누가 하든 `main` 직접 커밋은 위반이다.
#
# ⚠⚠ **fail-open** — 파싱·git 호출이 실패하면 조용히 통과시킨다. 훅이 작업을 막아 세우는 것보다
#   한 번 새는 편이 낫다. guard_logs·guard_balance 와 같은 규약이다.
import json
import os
import re
import subprocess
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass

MODE = "deny"          # "ask" 로 두면 차단 대신 사용자에게 묻는다

# 이 경로 하위의 저장소일 때만 본다. 바깥 저장소는 우리 규율이 아니다.
WORKSPACE = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

# 보호 대상 브랜치. `master` 도 넣어 둔다 — 이 저장소는 `main` 이지만 실수 여지를 줄인다.
PROTECTED = ("main", "master")


def emit(decision, reason):
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": decision,
            "permissionDecisionReason": reason,
        }
    }, ensure_ascii=False))
    sys.exit(0)


def commit_clauses(command):
    """
    명령 안에 `git ... commit` 호출이 있는가. 있으면 `-C` 로 지정된 저장소 경로를 함께 낸다.

    ⚠ 절(clause) 단위로 쪼갠 뒤 각 절의 **첫 git 하위명령**만 본다.
      그래야 `git log --format=...commit...` 같은 문자열이 오탐되지 않는다.
    ⚠ 따옴표 처리는 하지 않는다(`shlex` 는 Windows 역슬래시 경로에서 깨진다).
      §5-A 가 `-m` 을 금지하고 `-F <파일>` 을 쓰게 하므로 커밋 메시지가 명령줄에 안 들어온다.
    """
    found = []
    for clause in re.split(r"[;|&]+|\n", command):
        toks = clause.split()
        if "git" not in toks:
            continue
        i = toks.index("git") + 1
        repo = None
        while i < len(toks):
            t = toks[i]
            if t == "-C" and i + 1 < len(toks):
                repo = toks[i + 1]
                i += 2
                continue
            if t.startswith("-"):
                i += 1
                continue
            break
        if i >= len(toks):
            continue

        # ⚠⚠ 같은 명령 안에서 **먼저 브랜치를 옮기면** 커밋 시점엔 이미 main 이 아니다.
        #   `git checkout -b feat/x && git commit -F msg` 를 막으면 오탐이다 —
        #   차단형 훅에서 오탐은 곧 작업 중단이므로 이쪽을 먼저 막는다.
        if toks[i] in ("checkout", "switch"):
            return []

        if toks[i] == "commit":
            found.append(repo)
    return found


def branch_of(repo):
    out = subprocess.run(
        ["git", "-C", repo, "rev-parse", "--abbrev-ref", "HEAD"],
        capture_output=True, timeout=10)
    if out.returncode != 0:
        return None
    return out.stdout.decode("utf-8", "replace").strip()


def under_workspace(path):
    try:
        return os.path.commonpath([os.path.abspath(path), WORKSPACE]) == WORKSPACE
    except Exception:
        return False


def main():
    raw = sys.stdin.buffer.read().decode("utf-8", "replace").lstrip("﻿").strip()
    data = json.loads(raw)

    command = (data.get("tool_input") or {}).get("command")
    if not isinstance(command, str):
        return

    repos = commit_clauses(command)
    if not repos:
        return

    cwd = data.get("cwd") or WORKSPACE
    for repo in repos:
        # ⚠ `-C` 가 명시됐으면 **그대로 존중한다.** 그 경로가 없다고 cwd 로 되돌리면
        #   작업공간 밖 저장소를 겨눈 명령이 여기서 차단된다(실제로 테스트에서 오탐이 났다).
        target = repo if repo else cwd
        if not under_workspace(target):
            continue      # 작업공간 밖 저장소는 우리 규율이 아니다

        branch = branch_of(target)
        if branch in PROTECTED:
            emit(MODE, (
                "⛔ CLAUDE.md §5-B / L5 위반 차단 — 지금 `" + branch + "` 에 커밋하려 한다.\n"
                "   **`main` 에 직접 커밋하지 않는다.** 작업은 스코프 브랜치에서 하고 PR 로 병합한다.\n"
                "\n"
                "→ 이어서 할 일이 어느 스코프인지 보고 브랜치를 고른다:\n"
                "     feat/martial-engine   무공·전투 엔진·Core 로직·설계 문서\n"
                "     ui/combat-view        Unity UI 바인딩(화면·바인딩 스크립트)\n"
                "     chore/…  docs/…  asset/…   그 외\n"
                "   새로 딸 때는 `git checkout main && git pull && git checkout -b <새브랜치>` —\n"
                "   ⚠ **브랜치를 새로 만드는 것은 사용자 결정이다. 먼저 물어라**(§5-B).\n"
                "\n"
                "⚠ 이 훅은 `commit` 만 막는다. `push`·`merge`·`pull` 은 §5-B 의 정상 병합 절차다.\n"
                "(규칙: .claude/hooks/guard_branch.py, MODE=" + MODE + ")"
            ))


try:
    main()
except Exception:
    pass          # fail-open — 훅이 작업을 막아 세우는 것보다 한 번 새는 편이 낫다
sys.exit(0)
