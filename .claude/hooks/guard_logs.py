# -*- coding: utf-8 -*-
"""
PreToolUse 훅 (D:/GameDev/CLAUDE.md §5 강제)

메인 세션이 Unity 대용량 로그·생성물을 직접 열람하려 하면 차단하고 sub-agent 위임을 유도한다.
sub-agent 는 면제 — 위임 대상이 원문을 읽는 것은 §5 가 의도한 동작이다.

CK3 작업공간의 guard_vanilla.py 이식본. 차단 대상만 도메인에 맞게 교체했다.

────────────────────────────────────────────────────────────────────────────
⚠⚠ 2026-07-27 결함 3건 수정 — 전부 실측으로 확인한 것이다
────────────────────────────────────────────────────────────────────────────
 1) 스크래치패드 오차단 (실증: Read 가 실제로 deny 됐다)
    이전 판은 '/temp/' 를 드라이브 구분 없이 검사했다. 그런데 시스템이 임시파일 용도로
    지정한 경로가 C:/Users/<user>/AppData/Local/Temp/claude/.../scratchpad 라서,
    막으려던 Unity 의 Temp/ 가 아니라 스크래치패드가 '빌드 중간 산출물' 로 오인돼 막혔다.
    → 생성물 패턴(SCOPED)은 WORKSPACE 하위 경로일 때만 적용하도록 범위를 좁혔다.

 2) PowerShell 우회 (실증: PowerShell 로 Temp 경로를 읽고 써도 통과했다)
    settings.json 의 matcher 가 "Read|Grep|Glob|Bash" 라 PowerShell 툴에서는 이 훅이
    아예 호출되지 않았다. 이 환경의 주 셸이 PowerShell 이므로 가드가 사실상 무력했다.
    → matcher 에 PowerShell 을 추가했다. (이 파일이 아니라 settings.json 쪽 수정)

 3) BOM 으로 인한 무력화 (실증: BOM 3바이트가 붙자 Editor.log 가 그냥 통과했다)
    입력 앞에 BOM 이 붙으면 json.loads 가 예외를 냈고, except 절이 조용히 통과시켰다.
    → 파싱 전에 BOM 을 벗겨낸다.

⚠ fail-open(파싱 실패 시 통과)은 **의도적으로 유지**한다.
  가드 자체의 버그로 모든 도구 호출이 막혀 세션이 마비되는 쪽이 더 나쁘기 때문이다.
  즉 이 훅은 '실수 방지 장치'이지 '보안 경계'가 아니다. 그 전제로 쓸 것.
"""
import sys
import json

#   "deny" = 메인 세션 직접 열람을 하드 블록(기본)
#   "ask"  = 사용자 확인으로 하향(필요 시)
MODE = "deny"

# 이 경로 하위일 때만 SCOPED 패턴을 적용한다. 바깥(스크래치패드 등)은 우리 알 바 아니다.
WORKSPACE = "d:/gamedev/"

# 작업공간 하위에서만 차단할 것 — 전부 '생성물'이다. 사람이 쓴 것이 아니므로 원본이 따로 있다.
SCOPED = (
    ("/library/", "Unity Library/ (에디터 생성물, 대용량)"),
    ("/temp/", "Unity Temp/ (에디터 중간 생성물)"),
    ("/obj/", "빌드 중간 산출물 obj/"),
)
# ⚠ '/bin/' 은 일부러 넣지 않았다. 내용이 대부분 바이너리라 컨텍스트 오염 위험이 낮은 반면,
#   bash 경로(/usr/bin/ 등)와 충돌해 오탐을 만들 여지가 있다. 규칙 표면은 좁을수록 좋다.

# 경로가 어디에 있든 차단할 것 — Unity 로그는 %LOCALAPPDATA% 아래라 작업공간 밖에 있다.
GLOBAL = (
    ("editor.log", "Unity 에디터 로그"),
    ("upm.log", "Unity 패키지 매니저 로그"),
)

try:
    sys.stdout.reconfigure(encoding="utf-8")
except Exception:
    pass


def emit(decision, reason):
    print(json.dumps({
        "hookSpecificOutput": {
            "hookEventName": "PreToolUse",
            "permissionDecision": decision,
            "permissionDecisionReason": reason,
        }
    }, ensure_ascii=False))
    sys.exit(0)


def tokenize(blob):
    """명령 문자열을 경로 후보 토큰으로 쪼갠다. 공백과 따옴표가 구분자다."""
    out = []
    buf = []
    for ch in blob:
        if ch in " \t\n\r\"'|,;()":
            if buf:
                out.append("".join(buf))
                buf = []
        else:
            buf.append(ch)
    if buf:
        out.append("".join(buf))
    return out


def classify(blob):
    """차단 사유 문자열을 반환. 차단할 이유가 없으면 None."""
    for needle, label in GLOBAL:
        if needle in blob:
            return label

    # ⚠⚠ 2026-07-29 추가 수정 — 문자열 전체가 아니라 **토큰 단위**로 판단한다.
    #   한 명령에 작업공간 경로와 스크래치패드 경로가 함께 오면
    #   ("python D:/GameDev/... 를 읽어 C:/.../Temp/claude/... 에 쓴다")
    #   전체 문자열에는 WORKSPACE 도 있고 '/temp/' 도 있어서 오탐이 났다. 실제로 밟았다.
    #   토큰마다 "이 경로가 작업공간 하위인가"를 따로 보면 오탐이 사라진다.
    for token in tokenize(blob):
        if WORKSPACE not in token:
            continue
        for needle, label in SCOPED:
            if needle in token:
                return label
        # 프로젝트 로그: <프로젝트>/Logs/*.log
        if "/logs/" in token and ".log" in token:
            return "프로젝트 로그"

    return None


try:
    raw = sys.stdin.buffer.read().decode("utf-8", "replace")
    raw = raw.lstrip("\ufeff").strip()  # ⚠⚠ BOM 제거 — 없으면 파싱 실패로 가드가 조용히 죽는다
    data = json.loads(raw)
except Exception:
    sys.exit(0)  # fail-open (위 주석 참조)

# sub-agent 면제 — 위임 대상이 원문을 읽는 것은 §5 가 의도한 동작이다.
if data.get("agent_id") or data.get("agent_type"):
    sys.exit(0)

ti = data.get("tool_input") or {}
parts = []
# path 계열 + 셸 command 만 검사(Grep 의 pattern 은 제외 → 오탐 방지)
for k in ("file_path", "path", "notebook_path", "command"):
    v = ti.get(k)
    if isinstance(v, str):
        parts.append(v)
blob = " ".join(parts).replace("\\", "/").lower()

hit = classify(blob)
if hit:
    reason = (
        "⛔ CLAUDE.md §5 위반 차단 — " + hit + " 은(는) 메인 세션에서 직접 열람 금지.\n"
        "→ 로그·오류 진단은 `diagnosis` sub-agent 에 위임하고 '요약'만 받으세요(원문 덤프 금지).\n"
        "   Unity API·문서 대조는 `docs-lookup`, 게임 디자인 리서치는 `game-research`,\n"
        "   sub-agent 산출물 검증은 `verify`.\n"
        "(규칙: .claude/hooks/guard_logs.py, MODE=" + MODE + ")"
    )
    emit("deny" if MODE == "deny" else "ask", reason)

sys.exit(0)
