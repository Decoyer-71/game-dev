---
name: balance-audit
description: 밸런스에 영향을 주는 것(형태소 사전 수치·전투 상수·성장 곡선·카탈로그)을 고쳤을 때, 기준선과 대조해 **바뀐 지표 전부**를 확인한다. 한 곳만 보고 결론 내는 것을 막는 장치다. 값을 실험할 때도, 확정해서 커밋하기 전에도 쓴다.
---

# 밸런스 감사 — 내가 안 본 지표를 도구가 들이민다

## 이 스킬이 존재하는 이유

**출력에는 필요한 지표가 이미 다 있었는데, `grep` 으로 보고 싶은 부분만 잘라 보고 전체 결론을 내는 실수가 2026-08-04~05 이틀에 네 번 반복됐다.**

마지막 사례가 결정적이다 — 기만 형태소의 대가를 옮기고 *"소문파가 좋아졌으니 작동한다"* 고 보고했는데, **같은 출력의 전승무학 블록에는 종환화격이 79.7%(지배)로 올라 있었다.** 안 본 것이지 없던 것이 아니다.

→ **정보 부족이 아니라 선택적 관찰이다.** 그래서 대책이 *"더 잘 보자"* 가 아니라 **"안 본 지표의 변화를 도구가 강제로 보여주게"** 다. `../../CLAUDE.md` §1 의 *"실수를 사람 기억에 맡기지 않는다"* 와 같은 선이다.

## 언제 쓰는가

**쓴다** — 아래 중 하나라도 손댔으면:

- `Core/Martial/Morphemes/MorphemeDictionary.cs` (형태소 수치)
- `Core/Combat/CombatResolver.cs` · `Combatant.cs` (전투 상수·공식)
- `Core/Martial/AlignmentCurve.cs` · `DisciplineCurve.cs` (성장 곡선)
- `Core/Martial/MartialArtCatalog.cs` (무공 추가·개명)
- `Core/Characters/CharacterStats.cs` (기본 능력치)

**안 쓴다** — 주석만 고쳤을 때, 문서만 고쳤을 때, Sandbox 출력 형식만 바꿨을 때.

## 절차

### 1. 기준선이 최신인지 확인한다

```bash
git log --oneline -1 docs/baseline-metrics.txt
```

⚠ **마지막 밸런스 커밋보다 오래됐으면 기준선부터 다시 뜬다** — 그때의 트리가 깨끗한지 확인하고:

```bash
D:/Tools/dotnet/dotnet.exe run --project projects/Jianghu/Tools/Sandbox/Sandbox.csproj -c Release -- --metrics docs/baseline-metrics.txt
```

### 2. 변경을 적용하고 대조한다

```bash
D:/Tools/dotnet/dotnet.exe run --project projects/Jianghu/Tools/Sandbox/Sandbox.csproj -c Release -- --compare docs/baseline-metrics.txt
```

출력은 **바뀐 지표 전부**를 변화량 크기순으로 낸다.

### 3. ⚠⚠ 보고에 **diff 전문**을 넣는다

**일부만 인용하면 이 도구를 만든 이유가 사라진다.** 100줄이 넘으면 상위 20줄 + *"나머지 N건은 ±X 미만"* 으로 줄이되, **줄였다는 사실과 기준을 반드시 밝힌다.**

- 도구는 **좋아짐/나빠짐을 판정하지 않는다.** 지표마다 방향이 다르다(격차는 작을수록 좋고, 전락률은 목표 구간이 있다). **읽는 것은 사람 몫이다**
- **의도하지 않은 변화가 하나라도 있으면 그것부터 설명한다.** 오늘 놓친 것이 정확히 그 자리에 있었다

### 4. 확정할 때는 기준선도 함께 갱신한다

채택이 확정되면 새 값이 다음 기준선이다:

```bash
D:/Tools/dotnet/dotnet.exe run --project projects/Jianghu/Tools/Sandbox/Sandbox.csproj -c Release -- --metrics docs/baseline-metrics.txt
```

`docs/baseline-metrics.txt` 를 **변경과 같은 커밋에** 넣는다. 그래야 다음 세션이 *"그때 숫자가 뭐였지"* 를 다시 재지 않는다.

## 한계 — 알고 쓴다

- **판단을 대신하지 않는다.** *"전승 51.1 을 받아들일 것인가"* 는 사람이 정한다. 도구는 **놓치지 않게** 할 뿐이다
- **Sandbox 가 안 재는 축은 diff 에도 안 나온다.** 극한경지 선(仙) 처럼 측정 블록이 없는 것은 여전히 보이지 않는다 → 새 축을 만들면 **측정 블록부터 만든다**
- **기준선에 이미 있는 문제는 diff 로 안 보인다.** 지금 기준선에도 대문파 격차 33.8 같은 미해결 항목이 들어 있다. 그건 별건으로 남는다
- 임계 **±0.05 미만은 접는다.** 접은 개수는 `같음 N건` 으로 찍힌다

## 자주 밟는 것

| 증상 | 원인 | 대응 |
|---|---|---|
| `⛔ 기준선 파일이 없다` | 아직 안 떴다 | `--metrics` 로 먼저 뜬다 |
| `신규`/`사라짐` 이 대량 | 지표 키가 바뀌었다(무공 개명 등) | 정상이다. 개명했으면 그 무공 키가 신규+사라짐으로 뜬다 |
| 바뀜이 0건인데 승률이 달라 보임 | Sandbox 표는 봤는데 지표에 없는 값 | **측정 블록에 `Metrics.Add` 를 추가한다** |
