---
name: bal-converge
description: 목표 KPI(예: "Dur 180~240 AND Lvl>=5")를 정해두면 /bal-run → 메트릭 분석 → KPI 평가 → 통과 시 종료, 실패 시 /bal-apply --auto → 다시 /bal-run 사이클을 자동으로 반복하는 오케스트레이션 skill. 사이클 시작 전 Plan Preview를 보여주고 사용자 승인을 1회 받은 뒤 무인 실행. 사용자가 "/bal-converge", "bal converge", "밸런스 자동 수렴", "목표까지 자동 튜닝", "KPI 도달까지 반복" 같은 표현을 쓰면 사용. 선행 조건: /bal-apply --auto 동작 + /bal-run config 완료.
disable-model-invocation: false
---

# bal-converge: 목표 KPI 도달까지 자동 사이클

`/bal-run` + `/bal-apply --auto` 를 묶어 사용자가 정한 KPI에 도달할 때까지 반복한다. 자동 사이클이지만 **시작 전 1회**, 어떤 knob을 어떤 방향으로 조절할 계획인지 사용자에게 Plan Preview를 보여주고 Proceed/Abort 승인을 받는다. 이후는 무인.

게임-종속 정보는 `.claude/bal-converge.json` 한 파일에만 (metric 별칭, 디폴트 KPI). 다른 게임에 그대로 옮겨갈 수 있다.

## 큰 그림

```
[1] 인자 + config 로드 (KPI, 한도)
        ↓
[1.5] cmux dashboard pane: 기존 browser surface 모두 정리 → 새 pane 2개 생성
       (CONVERGE_SURF + GAME_SURF — 각각 KPI/apply 패널과 game 시계열 차트 전용)
        ↓
[2] Baseline /bal-run 1세트 → SESSION_ID, mean metrics
        ↓
[2.5] GAME_SURF 를 baseline SESSION_ID 로 select + chart_keys chips
        ↓
[3] /bal-apply --dry-run --auto session=<baseline_id> → PLAN: 라인들 받음
        ↓
[4] Plan Preview 출력 + AskUserQuestion (Proceed / Modify KPI / Abort)
        ↓ (Proceed)
[4.5] converge_run session 생성 → CONVERGE_SURF selectors 설정 + chart_keys chips
        ↓
[5] 사이클 루프 (최대 max_iter):
      (a) KPI 평가 → 통과면 break
      (b) divergence 감지 (2 사이클 연속 distance 증가) → break
      (c) wall-time 초과 → break
      (d) /bal-apply --auto session=<id> → MODIFIED: 누적
      (e) MODIFIED 0개면 break (no-op)
      (f) /bal-run N회 → 새 SESSION_ID
      (g) GAME_SURF reload + selectors 새 SESSION_ID 로 갱신 (chart chips 자동 재추가)
        ↓
[6] 종료 보고 (사이클별 metric 표 + REASON + 누적 MODIFIED 파일 + git diff --stat)
```

baseline에서 받은 SESSION_ID는 사이클의 첫 KPI 평가에 그대로 재사용된다 (한 번 더 bal-run 안 함).

## 인자 파싱

`/bal-converge` 뒤 자유 문자열에서:

| 변수 | 의미 | 디폴트 |
|---|---|---|
| `kpi="..."` | 목표 KPI 표현식 (큰따옴표 권장) | config의 `default_kpi` |
| `N=<int>` | 사이클당 bal-run play 수 | config의 `default_N` (5) |
| `timeout=<10m/600s>` | 한 판 timeout (bal-run에 전달) | config의 `default_per_play_timeout` (10m) |
| `max_iter=<int>` | 최대 사이클 수 | config의 `max_iter` (5) |
| `max_wall=<30m/1800s>` | 전체 wall-time 한도 | config의 `max_wall_sec` (1800) |

추출 휴리스틱:
- `kpi`: `kpi="..."` 안의 내용 또는 첫 번째 인용된 문자열.
- `N`: `(\d+)\s*(?:번|회|판)` 첫 매칭 (단, `max_iter`, `max_wall`의 숫자는 제외).
- `timeout`: `(?:한판|per-play|timeout)\s*[=:]?\s*(\d+)\s*(분|m|min|초|s)`.
- `max_iter`: `max[_-]?iter[=:]?\s*(\d+)` 또는 "최대 N 사이클".
- `max_wall`: `max[_-]?wall[=:]?\s*(\d+)\s*(분|m|초|s)` 또는 "전체 N분".

비상식 값(`N=0`, `max_iter=0`, `timeout<30s`)은 AskUserQuestion 으로 재확인.

추출 후 한 줄 보고:
```
[bal-converge] KPI="Dur 180~240 AND Lvl>=5", N=5, per-play timeout=10분, max_iter=5, max_wall=30분
```

예시:
- `/bal-converge` → 모든 디폴트
- `/bal-converge kpi="Dur 180~240 AND Lvl>=5"` → KPI만 명시
- `/bal-converge kpi="Dur>=120" N=3 max_iter=2` → 빠른 smoke

---

## 사전 점검

```bash
# (1) PlayTrace
curl -s -m 3 http://localhost:8000/health | grep -q '"ok"' \
  || die "PlayTrace 서버 응답 없음 (http://localhost:8000)."

# (2) unity-cli + Editor 응답
command -v unity-cli >/dev/null || die "unity-cli 필요."
unity-cli exec "return 1+1;" 2>/dev/null | grep -q '^2$' \
  || die "Unity Editor 응답 없음."

# (3) Editor가 play mode면 abort (bal-apply 안전 조건 동일)
unity-cli exec "return Application.isPlaying;" 2>/dev/null | grep -q '^true$' \
  && die "Editor가 play 중입니다. stop 후 다시 실행해 주세요."

# (4) git 저장소 + working tree 클린한 편인지 (Assets/ 미커밋 파일 경고)
git rev-parse --is-inside-work-tree >/dev/null 2>&1 \
  || die "git 저장소 아님."
DIRTY=$(git status --porcelain Assets/ 2>/dev/null | head)
if [ -n "$DIRTY" ]; then
  echo "[warn] Assets/ 미커밋 변경 있음:"
  echo "$DIRTY"
  # AskUserQuestion: Continue / Abort. bal-apply --auto가 dirty면 exit 3로 깨질 위험.
fi

# (5) 두 의존 skill 존재
[ -f .claude/skills/bal-run/SKILL.md ]   || die "/bal-run skill이 없습니다."
[ -f .claude/skills/bal-apply/SKILL.md ] || die "/bal-apply skill이 없습니다."

# (6) 두 의존 config 존재
[ -f .claude/bal-run.json ]   || die "/bal-run 설정 없음. 먼저 /bal-run 한 번으로 config 생성."
[ -f .claude/bal-apply.json ] || die "/bal-apply 설정 없음. 먼저 /bal-apply dry-run으로 cold start 통과 또는 수동 작성."

# (7) cmux 환경 (선택)
if [ -z "$CMUX_WORKSPACE_ID" ]; then
  HAS_CMUX=0
else
  HAS_CMUX=1
fi

# (8) [MANDATORY when HAS_CMUX=1] dashboard panes 점검 — §"Dashboard 패널 셋업" 진입.
# 이 step 을 건너뛰면 사용자가 "dashboard 안 보여?" 묻는 사태 반드시 발생.
# 세션 resume / pane 손실 / 다른 workspace 이동 등으로 매번 fresh 보장 필요.
# 코드는 §"Dashboard 패널 셋업" 참조. 모든 흐름 진입 (cold start / resume / 사용자 재호출) 시 실행.
```

> **흐름 진입의 절대 룰**: bal-converge 호출 / 세션 resume / 사용자가 "다시 시작" 류 표현 사용 시 — 어떤 경우든 위 (1)~(8) 사전 점검을 **모두** 다시 통과시킨다. (8) dashboard pane 점검은 cmux 환경이면 mandatory; 사용자가 묻기 전에 reflexively 챙긴다.

---

## Dashboard 패널 셋업 (cmux 명시적 정리 + 신규 생성)

bal-converge 는 진행 상황을 실시간으로 보기 위해 **사이클 시작 시점에 dashboard 브라우저 pane 2개를 명시적으로 띄운다**. 이전 실행 / 수동 실험 / 다른 skill 호출에서 남은 dashboard pane이 워크스페이스에 누적되는 걸 피하기 위해 **시작 시 현재 workspace 의 모든 browser surface를 닫고 새로 2개** 만든다 (CONVERGE_SURF + GAME_SURF).

`HAS_CMUX=0` 이면 통째로 skip. 실패해도 사이클 자체는 진행 (warn + 계속).

```bash
CONVERGE_SURF=""
GAME_SURF=""
if [ "$HAS_CMUX" = "1" ]; then
  # 1. 현재 workspace 의 browser 타입 surface 목록 → 모두 닫기
  CLOSED=0
  while IFS= read -r SURF; do
    [ -z "$SURF" ] && continue
    cmux close-surface --surface "$SURF" >/dev/null 2>&1 && CLOSED=$((CLOSED+1)) || true
  done < <(cmux list-pane-surfaces 2>/dev/null | awk '/\[browser\]/ {print $2}')
  echo "[bal-converge] 기존 browser surface ${CLOSED}개 정리"

  # 2. dashboard pane 2개 생성 (결정론적 레이아웃: claude(좌) | GAME(우상) / CONVERGE(우하)):
  #    - GAME_SURF:     매 사이클의 game session 전용 (실시간 시계열 차트). 우상단.
  #    - CONVERGE_SURF: converge_run session 전용 (KPI/apply meta 패널 시각화). 우하단.
  #
  # 주의: `cmux new-pane` 은 split 후에도 focus 를 옮기지 않는다. 두 번 연속으로 호출하면
  #       둘 다 caller(claude) pane 기준으로 분할되어 우상/우하 레이아웃이 안 만들어진다.
  #       그래서 첫 split 후 focus-pane 으로 새 pane 으로 옮기고, 두 번째 split 을 그 위에서 한다.
  CALLER_PANE_REF=$(cmux identify --json 2>/dev/null | python3 -c 'import sys,json; print(json.load(sys.stdin)["caller"]["pane_ref"])' 2>/dev/null)

  # (a) claude 오른쪽으로 새 browser pane → GAME (우상단)
  NEW1=$(cmux new-pane --type browser --direction right --url "http://localhost:8000/dashboard?mode=game" 2>&1)
  GAME_SURF=$(echo "$NEW1" | grep -oE 'surface:[0-9]+' | head -1)
  GAME_PANE=$(echo "$NEW1" | grep -oE 'pane:[0-9]+' | head -1)

  # (b) GAME pane 으로 focus 이동 (다음 split 의 기준점 결정론화)
  [ -n "$GAME_PANE" ] && cmux focus-pane --pane "$GAME_PANE" >/dev/null 2>&1

  # (c) GAME 아래로 새 browser pane → CONVERGE (우하단)
  NEW2=$(cmux new-pane --type browser --direction down --url "http://localhost:8000/dashboard?mode=converge" 2>&1)
  CONVERGE_SURF=$(echo "$NEW2" | grep -oE 'surface:[0-9]+' | head -1)

  # (d) 원래 claude pane 으로 focus 복귀
  [ -n "$CALLER_PANE_REF" ] && cmux focus-pane --pane "$CALLER_PANE_REF" >/dev/null 2>&1

  if [ -z "$CONVERGE_SURF" ] || [ -z "$GAME_SURF" ]; then
    echo "[bal-converge] WARNING: dashboard pane 생성 일부 실패 (game=$GAME_SURF, converge=$CONVERGE_SURF). 누락된 pane은 수동으로 열어주세요."
  else
    echo "[bal-converge] dashboard panes: game=$GAME_SURF (우상단), converge=$CONVERGE_SURF (우하단)"
  fi
fi
```

**설계 메모:**
- 두 pane으로 분리하는 이유: PlayTrace dashboard는 한 페이지 = 한 session selector. KPI/apply 패널(converge_run session)과 실시간 게임 시계열(매 사이클마다 새 game session)을 동시에 보려면 pane 2개가 필요. 하나만 두면 매 사이클 selector 스위치 때 KPI 누적 거동을 잃거나 게임 시계열을 못 봄.
- 레이아웃 규약: `claude(좌) | GAME(우상) / CONVERGE(우하)`. 시선 흐름은 위(현재 iter 실시간) → 아래(누적 KPI 거동). 이 순서는 `new-pane` 두 번 사이에 `focus-pane` 으로 새 pane 에 명시적 focus 를 옮겨야 결정론적으로 만들어진다 — `new-pane` 은 자동 focus 이동을 하지 않는다.
- `cmux browser open-split` 도 동일 효과지만 split panel 구조 안에서 `list-pane-surfaces` 의 `[browser]` 필터링이 일관되지 않을 수 있다. `new-pane --type browser` 는 독립 pane을 만들어 다음 사이클 cleanup 시점에 분명히 잡힌다.
- cleanup 은 **현재 workspace 안** 의 browser surface 만 대상. 다른 workspace의 브라우저 pane(예: Obsidian, ml-tensorboard 등)은 건드리지 않는다.
- `/bal-run` 이 자체 dashboard pane을 만들지 않도록(중복 누적 방지) bal-converge 안의 bal-run 호출은 인라인 폴링 패턴을 권장. 별도 호출이라면 사이클 끝나면 dashboard 가 누적될 수 있다 — 다음 `/bal-converge` 실행 시 자동 정리됨 (이 섹션의 step 1).

---

## Config 로드 & 검증

```bash
CFG=".claude/bal-converge.json"
[ -f "$CFG" ] || die "[bal-converge] config 없음 ($CFG). seed로 다음 내용을 만들고 다시 실행해 주세요:
{
  \"schema_version\": 1,
  \"default_kpi\": \"Dur 180~240 AND Lvl>=5\",
  \"metric_aliases\": {\"Dur\": \"episode.duration_sec\", \"Lvl\": \"episode.final_level\"},
  \"max_iter\": 5, \"max_wall_sec\": 1800, \"diverge_window\": 2,
  \"default_N\": 5, \"default_per_play_timeout\": \"10m\"
}"

python3 -c "
import json, sys
c = json.load(open('$CFG'))
for k in ['schema_version','default_kpi','metric_aliases','max_iter','max_wall_sec','diverge_window']:
    if k not in c: sys.exit('missing: '+k)
if c['schema_version'] != 1: sys.exit('schema_version != 1')
if not c['metric_aliases']: sys.exit('metric_aliases empty')
print('OK ({} aliases)'.format(len(c['metric_aliases'])))
" || die "config 검증 실패."
```

---

## Baseline 측정

Plan Preview 만들기 전에 한 번 측정해야 KPI distance와 PLAN의 from-value를 채울 수 있다.

옵션 두 가지:
1. **새로 측정** (디폴트): `/bal-run N회 timeout=<t>` 호출 → `SESSION_ID` 추출.
2. **재사용**: 직전 24h 안에 같은 project의 bal-run session이 존재하면 거기서 SESSION_ID 가져옴. 사용자가 인자로 `baseline=reuse` 명시했거나, "비용 절감" 모드일 때.

새 측정 호출 예시:
```bash
# Skill tool로 호출: skill=bal-run, args="${N}회 한판 ${TIMEOUT}"
# 결과 stdout에서 SESSION_ID 캡처
BAL_RUN_OUT=$(invoke_skill bal-run "${N}회 한판 ${TIMEOUT}")
BASELINE_ID=$(echo "$BAL_RUN_OUT" | grep -oE 'session=[A-Za-z0-9_]+' | tail -1 | cut -d= -f2)
[ -z "$BASELINE_ID" ] && die "/bal-run 결과에서 session ID를 못 찾았습니다. /bal-run 직접 호출로 디버깅 필요."

# §[2.5] GAME_SURF 갱신 — baseline 측정 직후. 시계열 차트 바로 표시.
# (PROJECT/VERSION 은 bal-run 출력에서 추출하거나 sessions API 로 1회 lookup)
update_game_pane "$BASELINE_ID"
```

> **메모**: 모델이 SKILL.md를 시뮬레이션하는 구조이므로, `invoke_skill` 은 실제로는 Skill tool 호출이다. 결과는 직접 stdout으로 받아 parsing.

---

## Plan Preview (bal-apply --dry-run --auto)

baseline session으로 어떤 knob이 매칭되고 어떤 값을 어떤 방향으로 바꿀지 사용자에게 보여준다.

```bash
DRY_OUT=$(invoke_skill bal-apply "--dry-run --auto session=$BASELINE_ID")
DRY_RC=$?
[ $DRY_RC -ne 0 ] && die "[bal-converge] /bal-apply --dry-run --auto 가 exit $DRY_RC 로 종료. baseline session에 진단 실패. 보통 원인:
  - exit 2: config 없음
  - exit 3: 영향 자산 dirty (커밋/스태시 후 재시도)
  먼저 /bal-apply dry-run 으로 단독 진단 확인하세요."

# PLAN:knob|asset|field|from|to 라인들 파싱
PLAN_LINES=$(echo "$DRY_OUT" | grep '^PLAN:')
PLAN_COUNT=$(echo "$PLAN_LINES" | grep -c '^PLAN:' || true)
[ "$PLAN_COUNT" -eq 0 ] && die "[bal-converge] 진단 결과 적용할 knob이 0개입니다 (모든 finding이 trigger 매칭 0이거나 자산 단정 실패). KPI는 이미 충족? KPI를 다시 확인하거나 /bal-apply config(.claude/bal-apply.json)에 knob을 추가하세요."
```

baseline 메트릭 집계 (KPI 거리 표시용):
```python
# stdin: 위의 BASELINE_ID, KPI_EXPR, metric_aliases
import json, urllib.request, statistics, re, collections

logs = json.loads(urllib.request.urlopen(
  f"http://localhost:8000/api/logs/search?test_session_id={BASELINE_ID}&size=5000").read())['items']

plays = collections.defaultdict(dict)
for l in logs:
    pn = l.get('play_no')
    if pn is None: continue
    vt = l.get('value_type')
    v = l.get('value_number') if vt == 'number' else (l.get('value_text') if vt=='text' else l.get('value_bool'))
    plays[pn][l['key']] = v

# 완료 play만
PLAY_END = "episode.cause"  # bal-run.json 의 play_end_key, 없으면 디폴트
completed = [p for p in plays.values() if PLAY_END in p]

aliases = config['metric_aliases']  # {Dur: episode.duration_sec, ...}
metrics = {}
for alias, key in aliases.items():
    vals = [p[key] for p in completed if isinstance(p.get(key),(int,float))]
    if vals: metrics[alias] = statistics.mean(vals)

# KPI 파싱 + 평가 + distance (parser는 아래 섹션)
pass_, dist, term_results = eval_kpi(KPI_EXPR, metrics)
```

Plan Preview 출력 (사용자에게 보이는 텍스트):

```
=== Plan Preview ===
Baseline session: 20260516_006 (N=5 plays, mean over completed)

Metrics:
  Dur = 64.5s     target 180~240    distance=1.92    [FAIL]
  Lvl = 3.0       target >=5        distance=0.40    [FAIL]
  KPI overall: distance=2.32, PASS=No

Findings (3):
  [high] killer_concentration: 'Gear' 가 사망 원인의 100% (3/3)
  [high] structural_duration_cutoff: 생존시간 거의 일정 (σ/μ=1.9%)
  [med]  level_cap_low: avg final_level=3.0 < 5

Auto-apply plan (3 knobs):
  enemy_damage         → Gear.asset                    Attack              5 → 4      (-15%)
  spawn_rate           → UNRESOLVED (asset 단정 불가)                                  (skip)
  level_up_xp_increment → ExpConfig.asset              experienceIncrement 50 → 40    (-20%)

한도: max_iter=5, max_wall=30분, per-play timeout=10분, N=5
예상 wall-time (보수적): max_iter × (N × per-play + apply 30s) ≈ 5 × (5×10분 + 0.5분) ≈ 252분
  → max_wall=30분이 먼저 트리거될 가능성 큼. max_iter 또는 max_wall 조정 권장.
```

`UNRESOLVED` 라인이 있으면 한 줄 경고 + 권장 안내 (예: spawn_rate는 hand-off의 derived target 설계 구현 필요).

AskUserQuestion:
- **Proceed** → 사이클 루프 진입
- **Modify KPI** → 새 KPI 문자열 입력 받고 baseline 재측정 없이 같은 baseline_id로 KPI만 갈아 재평가 (PLAN: 라인은 그대로 valid, 변경량은 KPI에 무관)
- **Abort** → 자산 변경 없이 종료

---

## Converge run session 생성 + Meta 키 발사 헬퍼

자동 사이클 진입 직전에 dashboard용 메타 session 하나를 만들고, 각 사이클의 KPI/apply 결과를 PlayTrace에 발사할 헬퍼를 정의한다. dashboard JS가 `meta.kpi.*` / `meta.apply.*` 키를 인식해 시각화 패널을 그린다 (PlayTrace 서버는 무변경, 기존 POST `/api/logs` + POST `/api/sessions` 만 사용).

### 1) `display_labels` 로드 (옵션)

`.claude/bal-converge.json` 의 `display_labels` 필드(있으면)에서 메트릭/knob의 한국어 라벨과 자세한 설명을 가져온다. 없으면 영문 alias 그대로 폴백 — 게임-비종속 유지.

```bash
LABELS_JSON=$(python3 -c "
import json
c = json.load(open('.claude/bal-converge.json'))
print(json.dumps(c.get('display_labels', {'metrics':{},'knobs':{}})))
")
```

### 2) `converge_run` session 생성 (PlayTrace에 POST)

```bash
CONVERGE_NAME="converge_run_$(date +%Y%m%d_%H%M%S)"
CREATE_PAYLOAD=$(python3 -c "
import json
print(json.dumps({'project_name': '$PROJECT', 'version': '$VERSION', 'session_name': '$CONVERGE_NAME'}))
")
CONVERGE_SID=$(curl -s -X POST -H 'Content-Type: application/json' \
  -d "$CREATE_PAYLOAD" http://localhost:8000/api/sessions | \
  python3 -c "import json,sys; print(json.load(sys.stdin).get('test_session_id',''))")

[ -z "$CONVERGE_SID" ] && {
  echo "[bal-converge] WARNING: converge_run session 생성 실패. 사이클 session 단독 기록만 진행."
  CONVERGE_SID=""
}
echo "[bal-converge] converge_run session: $CONVERGE_SID (name=$CONVERGE_NAME)"

# CONVERGE_SURF (위 "Dashboard 패널 셋업" 섹션) 이 살아있으면
# 즉시 converge_run session 으로 selectors 채우기 + chart_keys chip 자동 추가
# (GAME_SURF 는 §[2.5] / §[5](g) 에서 game session selector 로 별도 설정)
if [ -n "$CONVERGE_SURF" ] && [ -n "$CONVERGE_SID" ]; then
  sleep 2  # 페이지 초기 로드 여유
  cmux browser select --surface "$CONVERGE_SURF" "#sel-project" "$PROJECT" >/dev/null 2>&1 || true
  sleep 1
  cmux browser select --surface "$CONVERGE_SURF" "#sel-version" "$VERSION" >/dev/null 2>&1 || true
  sleep 1
  cmux browser select --surface "$CONVERGE_SURF" "#sel-session" "$CONVERGE_SID" >/dev/null 2>&1 || true
  echo "[bal-converge] CONVERGE_SURF → project=$PROJECT version=$VERSION session=$CONVERGE_SID"

  # chart_keys chips 자동 추가 (.claude/bal-run.json 에서 읽어 재사용)
  # 시계열 차트가 비어있지 않도록 게임-메트릭 시계열 키들을 chip으로 채운다.
  # bal-run.json 이 없거나 chart_keys 가 비어있으면 silent skip.
  if [ -f .claude/bal-run.json ]; then
    CHART_KEYS=$(jq -r '.chart_keys[]?' .claude/bal-run.json 2>/dev/null)
    CHIP_COUNT=0
    for K in $CHART_KEYS; do
      [ -z "$K" ] && continue
      cmux browser fill --surface "$CONVERGE_SURF" "#sel-key" "$K" >/dev/null 2>&1 || continue
      cmux browser press --surface "$CONVERGE_SURF" "Enter" >/dev/null 2>&1 || continue
      CHIP_COUNT=$((CHIP_COUNT+1))
      sleep 0.3
    done
    [ "$CHIP_COUNT" -gt 0 ] && echo "[bal-converge] CONVERGE_SURF chart chips: ${CHIP_COUNT}개 추가"
  fi
fi
```

`PROJECT`/`VERSION` 은 baseline `/bal-run` 호출 후 캡처해둔 값을 사용 (sessions API 응답 또는 bal-run 출력의 `version=...` 라인에서).

### 2.5) GAME_SURF 갱신 헬퍼 (사이클 + baseline 공통)

매 `/bal-run` 완료 직후 (그리고 baseline 측정 직후) GAME_SURF 를 새 SESSION_ID 로 reload + selectors 갱신 + chart chips 재추가.

```bash
update_game_pane() {  # SID
  local SID="$1"
  [ "$HAS_CMUX" != "1" ] || [ -z "$GAME_SURF" ] || [ -z "$SID" ] && return 0

  cmux browser reload --surface "$GAME_SURF" >/dev/null 2>&1 || true
  sleep 2
  cmux browser select --surface "$GAME_SURF" "#sel-project" "$PROJECT" >/dev/null 2>&1 || true
  sleep 1
  cmux browser select --surface "$GAME_SURF" "#sel-version" "$VERSION" >/dev/null 2>&1 || true
  sleep 1
  cmux browser select --surface "$GAME_SURF" "#sel-session" "$SID" >/dev/null 2>&1 || true
  sleep 1

  if [ -f .claude/bal-run.json ]; then
    for K in $(jq -r '.chart_keys[]?' .claude/bal-run.json 2>/dev/null); do
      [ -z "$K" ] && continue
      cmux browser fill --surface "$GAME_SURF" "#sel-key" "$K" >/dev/null 2>&1 || continue
      cmux browser press --surface "$GAME_SURF" "Enter" >/dev/null 2>&1 || continue
      sleep 0.3
    done
  fi
  echo "[bal-converge] GAME_SURF → session=$SID"
}
```

호출 지점:
- §[2] baseline `/bal-run` 직후: `update_game_pane "$BASELINE_ID"`
- §[5](g) 매 사이클 `/bal-run` 직후: `update_game_pane "$SID"`

`reload` 가 필요한 이유: dashboard 페이지의 session 드롭다운은 페이지 로드 시점에 캐시. 새 session 이 서버에 생긴 직후 reload 하지 않으면 새 SID 가 드롭다운에 안 보여 select 가 실패한다.

### 3) `emit_meta` 헬퍼 (단건 POST, silent skip)

```bash
emit_meta() {  # SID, KEY, TYPE(text|number|bool), VAL
  local SID="$1" KEY="$2" TYPE="$3" VAL="$4"
  [ -z "$SID" ] && return 0   # converge_run 생성 실패 같은 케이스 skip
  local NOW
  NOW=$(python3 -c 'import time; print(int(time.time()*1000))')
  local PAYLOAD
  PAYLOAD=$(KEY="$KEY" TYPE="$TYPE" VAL="$VAL" python3 -c '
import json, os
v = os.environ["VAL"]
t = os.environ["TYPE"]
if t == "number":
    v = float(v) if "." in v else int(v)
elif t == "bool":
    v = v.lower() in ("true","1","yes")
# text는 그대로 str
print(json.dumps({
  "project_name": "'"$PROJECT"'",
  "version": "'"$VERSION"'",
  "test_session_id": "'"$SID"'",
  "play_no": 0,
  "key": os.environ["KEY"],
  "value": v,
  "client_time": '"$NOW"',
}))')
  curl -s -m 3 -X POST -H 'Content-Type: application/json' \
    -d "$PAYLOAD" http://localhost:8000/api/logs >/dev/null || true
}
```

> **macOS 호환 주의**: `date +%s%3N` 은 GNU date 전용 (BSD/macOS에서 `N`이 그대로 남음). `python3 -c 'import time; print(int(time.time()*1000))'` 으로 ms timestamp 생성.

### 4) `emit_kpi_block` — 매 사이클 KPI 평가 시점

```bash
emit_kpi_block() {  # PER_CYCLE_SID, EXPR, ITER, DIST, PASS, TERMS_JSON
  local PCSID="$1" EXPR="$2" ITER="$3" DIST="$4" PASSV="$5" TJSON="$6"
  for SID in "$PCSID" "$CONVERGE_SID"; do
    [ -z "$SID" ] && continue
    emit_meta "$SID" "meta.kpi.expression" text "$EXPR"
    emit_meta "$SID" "meta.kpi.iter"       number "$ITER"
    emit_meta "$SID" "meta.kpi.distance"   number "$DIST"
    emit_meta "$SID" "meta.kpi.pass"       bool   "$PASSV"
    # TERMS_JSON: [{"name":"Dur","kind":"range","target_min":90,"target_max":120,"current":63.12,"distance":0.90,"pass":false,"label":"평균 생존시간(초)","desc":"..."}, ...]
    echo "$TJSON" | python3 -c "
import json, sys, os, subprocess
terms = json.load(sys.stdin)
sid = '$SID'
for t in terms:
  prefix = f'meta.kpi.term.{t[\"name\"]}'
  pairs = [(f'{prefix}.label', 'text', t.get('label', t['name'])),
           (f'{prefix}.desc',  'text', t.get('desc', '')),
           (f'{prefix}.current', 'number', t['current']),
           (f'{prefix}.distance', 'number', t['distance']),
           (f'{prefix}.pass', 'bool', 'true' if t['pass'] else 'false')]
  if t['kind'] == 'range':
    pairs += [(f'{prefix}.target_min', 'number', t['target_min']),
              (f'{prefix}.target_max', 'number', t['target_max'])]
  else:
    pairs += [(f'{prefix}.target_op',  'text',   t['target_op']),
             (f'{prefix}.target_val', 'number', t['target_val'])]
  for k, ty, v in pairs:
    subprocess.run(['bash','-c', f'emit_meta \"$SID\" \"{k}\" {ty} \"{v}\"'], env={**os.environ, 'SID':sid})
"
  done
}
```

> 위 emit_kpi_block의 내부 python 안에서 다시 bash로 `emit_meta` 호출하는 형태는 모델 시뮬레이션에서 어색하다. 실제로는 KPI 평가 결과를 Python에서 한꺼번에 다 POST하는 게 단순. 아래 단순화한 형태 권장:

```python
# (단순화) Python 한 번에 모든 meta.kpi.* POST
def emit_kpi_python(per_cycle_sid, expr, iter_n, dist, passed, term_results, project, version, converge_sid, labels):
    import urllib.request, json, time
    now_ms = int(time.time()*1000)
    for sid in [per_cycle_sid, converge_sid]:
        if not sid: continue
        base = {"project_name": project, "version": version,
                "test_session_id": sid, "play_no": 0, "client_time": now_ms}
        def post(key, value):
            try:
                req = urllib.request.Request(
                    "http://localhost:8000/api/logs",
                    data=json.dumps({**base, "key": key, "value": value}).encode(),
                    headers={"Content-Type": "application/json"}, method="POST")
                urllib.request.urlopen(req, timeout=3).read()
            except Exception:
                pass  # silent skip
        post("meta.kpi.expression", expr)
        post("meta.kpi.iter", iter_n)
        post("meta.kpi.distance", dist)
        post("meta.kpi.pass", bool(passed))
        for term, value, td, ok in term_results:
            name = term[1]
            ml = labels.get("metrics", {}).get(name, {})
            prefix = f"meta.kpi.term.{name}"
            post(f"{prefix}.label", ml.get("ko", name))
            post(f"{prefix}.desc",  ml.get("desc", ""))
            post(f"{prefix}.current", value if isinstance(value,(int,float)) else 0)
            post(f"{prefix}.distance", td)
            post(f"{prefix}.pass", bool(ok))
            if term[0] == "range":
                post(f"{prefix}.target_min", term[2])
                post(f"{prefix}.target_max", term[3])
            else:
                post(f"{prefix}.target_op", term[2])
                post(f"{prefix}.target_val", term[3])
```

### 5) `emit_apply_block` — 매 apply 결정 직후

```python
def emit_apply_python(per_cycle_sid, iter_n, plan_line, project, version, converge_sid, labels):
    """plan_line: 'PLAN:knob|asset|field|from|to' 형식 (bal-apply --dry-run --auto의 출력) 또는
    실 apply 시에는 (knob, asset, field, from, to) 5-tuple 직접 전달."""
    import urllib.request, json, time, os
    now_ms = int(time.time()*1000)
    knob, asset, field, frm, to = plan_line.split("|")  # 또는 직접 전달
    asset_name = os.path.splitext(os.path.basename(asset))[0] if asset and asset != "UNRESOLVED" else asset
    # field_short: 마지막 segment + 명확화
    if "[" in field and "]" in field:
        # ValueDefinitions[ValueName=Attack].DefaultValue → "DefaultValue (Attack)"
        import re
        m = re.search(r"\[\w+=([^\]]+)\]", field)
        last_seg = field.rsplit(".", 1)[-1]
        field_short = f"{last_seg} ({m.group(1)})" if m else last_seg
    else:
        field_short = field
    kl = labels.get("knobs", {}).get(knob, {})

    for sid in [per_cycle_sid, converge_sid]:
        if not sid: continue
        base = {"project_name": project, "version": version,
                "test_session_id": sid, "play_no": 0, "client_time": now_ms}
        def post(key, value):
            try:
                req = urllib.request.Request(
                    "http://localhost:8000/api/logs",
                    data=json.dumps({**base, "key": key, "value": value}).encode(),
                    headers={"Content-Type": "application/json"}, method="POST")
                urllib.request.urlopen(req, timeout=3).read()
            except Exception:
                pass
        post("meta.apply.iter",        iter_n)
        post("meta.apply.knob_id",     knob)
        post("meta.apply.knob_label",  kl.get("ko", knob))
        post("meta.apply.knob_desc",   kl.get("desc", ""))
        post("meta.apply.asset",       asset)
        post("meta.apply.asset_name",  asset_name)
        post("meta.apply.field",       field)
        post("meta.apply.field_short", field_short)
        # from/to 숫자/텍스트 분기
        try:
            post("meta.apply.from", float(frm) if "." in str(frm) else int(frm))
            post("meta.apply.to",   float(to)  if "." in str(to)  else int(to))
        except (ValueError, TypeError):
            post("meta.apply.from_text", str(frm))
            post("meta.apply.to_text",   str(to))
```

### 6) 호출 지점

- 사이클 루프 (a) "KPI 평가" 직후 → `emit_kpi_python(SID, KPI, ITER, DIST, PASS, term_results, PROJECT, VERSION, CONVERGE_SID, labels)`
- (d) "/bal-apply --auto" 의 `MODIFIED:` 처리 직후 → 각 적용된 knob에 대해 `emit_apply_python(...)`. `from`/`to` 값은 직전 `--dry-run --auto` 의 PLAN 라인에서 미리 캡처해두거나, 실 apply 직후 자산 read로 보완.
- 종료 시점 → `meta.converge.end = true` 한 건만 발사 (선택).

POST 한 사이클당 약 20~30건. 실패는 silent skip — bal-converge 사이클 안정성 보존.

---

## 사이클 루프

baseline_id를 첫 사이클의 PASS 평가에 재사용. 즉 사이클 1은 bal-run 없이 PLAN preview에서 받은 데이터로 시작.

```bash
ITER=0
SID="$BASELINE_ID"
LAST_METRICS="$BASELINE_METRICS"
START=$(date +%s)
PREV=""; PREV2=""
declare -a ALL_MODIFIED=()
REASON=""

[ "$HAS_CMUX" = "1" ] && {
  cmux set-status bal-converge "iter 0/$MAX_ITER" --icon hourglass
  cmux set-progress 0.0 --label "Iter 0/$MAX_ITER"
}

while [ $ITER -lt $MAX_ITER ]; do
  ITER=$((ITER+1))

  # (a) KPI 평가
  read PASS DIST <<< "$(eval_kpi_oneline "$KPI" "$LAST_METRICS")"
  echo "[iter $ITER] session=$SID metrics=$LAST_METRICS dist=$DIST pass=$PASS"
  if [ "$PASS" = "1" ]; then REASON=pass; break; fi

  # (b) divergence: 3 사이클 모두 있을 때만 검사
  if [ -n "$PREV" ] && [ -n "$PREV2" ]; then
    if awk "BEGIN{exit !($DIST > $PREV && $PREV > $PREV2)}"; then
      REASON=diverged; break
    fi
  fi
  PREV2="$PREV"; PREV="$DIST"

  # (c) wall-time
  if [ $(( $(date +%s) - START )) -ge $MAX_WALL ]; then REASON=max_wall; break; fi

  # (d) /bal-apply --auto
  APPLY_OUT=$(invoke_skill bal-apply "--auto session=$SID")
  RC=$?
  if [ $RC -ne 0 ]; then REASON="apply_abort_$RC"; break; fi
  MODS=$(echo "$APPLY_OUT" | grep '^MODIFIED:' | sed 's/^MODIFIED://')
  if [ -z "$MODS" ]; then REASON=no_op; break; fi
  while IFS= read -r p; do ALL_MODIFIED+=("$p"); done <<< "$MODS"

  [ "$HAS_CMUX" = "1" ] && cmux set-status bal-converge "iter $ITER applied $(echo "$MODS" | wc -l) knobs" --icon hourglass

  # (f) /bal-run 다시
  BAL_OUT=$(invoke_skill bal-run "${N}회 한판 ${TIMEOUT}")
  SID=$(echo "$BAL_OUT" | grep -oE 'session=[A-Za-z0-9_]+' | tail -1 | cut -d= -f2)
  [ -z "$SID" ] && { REASON=bal_run_failed; break; }
  LAST_METRICS=$(fetch_metrics "$SID")

  # (g) GAME_SURF 를 새 session 으로 갱신 (실시간 시계열 차트 즉시 가시화)
  update_game_pane "$SID"

  [ "$HAS_CMUX" = "1" ] && cmux set-progress $(awk "BEGIN{print $ITER/$MAX_ITER}") --label "Iter $ITER/$MAX_ITER"
done

[ -z "$REASON" ] && REASON=max_iter
```

### `fetch_metrics` (한 session → metric_aliases mean dict)

```bash
fetch_metrics() {
  local SID="$1"
  python3 <<PYEOF
import json, urllib.request, statistics, collections
data = json.loads(urllib.request.urlopen(f"http://localhost:8000/api/logs/search?test_session_id=$SID&size=5000").read())
plays = collections.defaultdict(dict)
for l in data['items']:
    pn = l.get('play_no')
    if pn is None: continue
    vt = l.get('value_type')
    v = l.get('value_number') if vt=='number' else (l.get('value_text') if vt=='text' else l.get('value_bool'))
    plays[pn][l['key']] = v
PLAY_END = "episode.cause"
completed = [p for p in plays.values() if PLAY_END in p]
aliases = $ALIASES_JSON
out = {}
for a, k in aliases.items():
    vals = [p[k] for p in completed if isinstance(p.get(k),(int,float))]
    if vals: out[a] = round(statistics.mean(vals), 4)
print(json.dumps(out))
PYEOF
}
```

caller는 `LAST_METRICS=$(fetch_metrics "$SID")` → `'{"Dur": 65.5, "Lvl": 3.0, ...}'` JSON 한 줄.

---

## KPI 파서

문법 (단순 BNF):
- `expr := term (AND term)*`
- `term := name op num` (op: `>=` `<=` `>` `<` `==`)
- `term := name lo~hi` (범위, lo/hi inclusive)

모든 term이 만족하면 PASS=1.

```python
import re

def parse_kpi(expr):
    terms = []
    for raw in re.split(r'\s+AND\s+', expr.strip()):
        t = raw.strip()
        # range form: "Dur 180~240"
        m = re.match(r'^(\w+)\s*(-?\d+(?:\.\d+)?)\s*~\s*(-?\d+(?:\.\d+)?)$', t)
        if m:
            terms.append(('range', m.group(1), float(m.group(2)), float(m.group(3))))
            continue
        # cmp form: "Lvl>=5"
        m = re.match(r'^(\w+)\s*(>=|<=|==|>|<)\s*(-?\d+(?:\.\d+)?)$', t)
        if not m:
            raise ValueError(f"Cannot parse term: {t!r}")
        terms.append(('cmp', m.group(1), m.group(2), float(m.group(3))))
    return terms
```

---

## KPI 평가 + distance

```python
def eval_kpi(expr, metrics):
    """
    metrics: {alias_name: value}, e.g. {"Dur": 65.5, "Lvl": 3.0}
    returns (pass:bool, distance:float, term_results:list of (term, value, dist, ok))
    """
    terms = parse_kpi(expr)
    results = []
    total = 0.0
    all_pass = True
    for term in terms:
        kind, name = term[0], term[1]
        v = metrics.get(name)
        if v is None:
            # 누락 metric — fail로 처리, distance는 큰 값(=1.0) 부여
            results.append((term, None, 1.0, False))
            total += 1.0; all_pass = False
            continue
        if kind == 'range':
            lo, hi = term[2], term[3]
            if lo <= v <= hi:
                d, ok = 0.0, True
            else:
                d = min(abs(v - lo), abs(v - hi)) / max(hi - lo, 1e-9)
                ok = False
        else:  # cmp
            op, t = term[2], term[3]
            ok = {'>=': v>=t, '<=': v<=t, '>': v>t, '<': v<t, '==': v==t}[op]
            if ok:
                d = 0.0
            elif op in ('>=','>'):
                d = max(0, t - v) / max(abs(t), 1)
            elif op in ('<=','<'):
                d = max(0, v - t) / max(abs(t), 1)
            else:  # ==
                d = abs(v - t) / max(abs(t), 1)
        results.append((term, v, d, ok))
        total += d
        if not ok: all_pass = False
    return all_pass, total, results
```

bash에서 호출용 one-liner:

```bash
eval_kpi_oneline() {
  local EXPR="$1" METRICS_JSON="$2"
  python3 -c "
import sys, json, re
# (위 parse_kpi + eval_kpi 함수 inline)
expr = '''$EXPR'''
m = json.loads('''$METRICS_JSON''')
p, d, _ = eval_kpi(expr, m)
print(('1' if p else '0') + ' ' + ('%.4f' % d))
"
}
```

---

## 종료 보고

```bash
echo ""
echo "=== bal-converge 종료 ==="
echo "REASON: $REASON  ITER: $ITER/$MAX_ITER  WALL: $(( $(date +%s) - START ))s"
echo ""
echo "사이클별 메트릭 (mean over completed plays):"
# 각 사이클의 LAST_METRICS, DIST, PASS를 표로
# (구현 시 사이클마다 결과를 배열에 누적해 두기)

echo ""
echo "누적 수정 파일 ($(printf "%s\n" "${ALL_MODIFIED[@]}" | sort -u | wc -l)):"
printf "%s\n" "${ALL_MODIFIED[@]}" | sort -u

echo ""
echo "=== git diff --stat (영향 자산) ==="
git diff --stat -- $(printf "%s " "${ALL_MODIFIED[@]}" | sort -u)

echo ""
echo "다음 단계:"
case "$REASON" in
  pass) echo "  ✓ KPI 도달. 검토 후 'git add' + 'git commit' 으로 변경 확정." ;;
  max_iter|max_wall) echo "  - KPI 미도달. 누적 변경이 의도와 맞으면 commit, 아니면 'git checkout -- $(printf "%s " "${ALL_MODIFIED[@]}" | sort -u | tr '\n' ' ')' 로 rollback." ;;
  diverged) echo "  ! KPI distance가 사이클마다 증가했습니다. knob 매칭이 잘못됐을 가능성. .claude/bal-apply.json 검토 또는 KPI를 더 도달 가능한 값으로." ;;
  no_op) echo "  - 적용할 knob이 0개로 떨어졌습니다 (모든 finding이 trigger 매칭 0). 새 knob 추가 검토." ;;
  apply_abort_2) echo "  ! /bal-apply config 없음 (cold start). .claude/bal-apply.json 작성." ;;
  apply_abort_3) echo "  ! 영향 자산 dirty. 사이클 중간에 외부에서 자산 수정됨? 'git status' 확인." ;;
  bal_run_failed) echo "  ! /bal-run 이 session ID를 안 내놓음. /bal-run 단독 호출로 디버깅." ;;
esac

[ "$HAS_CMUX" = "1" ] && {
  if [ "$REASON" = "pass" ]; then
    cmux set-status bal-converge "Done (PASS at iter $ITER)" --icon check
  else
    cmux set-status bal-converge "Stopped: $REASON (iter $ITER)" --icon warning
  fi
  cmux set-progress 1.0 --label "Iter $ITER/$MAX_ITER ($REASON)"
}
```

---

## 게임-비종속

KPI 파서, distance 함수, 사이클 로직, divergence 감지는 게임 정보 0. 게임-종속은:
- `.claude/bal-converge.json` 의 `metric_aliases` (alias → PlayTrace key)
- `default_kpi` (이 게임 특유의 목표)

새 게임 이식 시 위 두 필드만 갈아끼우면 된다 (해당 게임에 `/bal-run`, `/bal-apply` skill 셋업이 먼저 되어 있어야 함).

---

## 자주 빠지는 함정

- **baseline_id 재사용 가정**: baseline 측정 후 즉시 사이클 진입한다면 SID=baseline_id로 시작해 첫 사이클은 bal-run 안 함. 단 baseline_id의 metric이 stale하다면 (사이클 중간에 외부 변경 등) PASS 판정이 잘못될 수 있음. 24h 룰로 막음.
- **distance 단조성 가정**: 한 사이클에 여러 knob을 동시 적용하면 distance가 비단조로 진동할 수 있음. `diverge_window=2` 는 연속 2회 증가 (3 사이클 필요)로 둠 — 너무 빠르게 abort하지 않게.
- **apply_abort_3 (dirty)**: 사이클 시작 전에 사용자가 자산을 만지면 다음 bal-apply --auto가 즉시 exit 3. 사이클 중간에 발생하면 부분 적용 상태로 종료. 사용자가 git checkout으로 복구 후 재실행.
- **no_op 무한루프 방어**: --auto가 모든 knob을 skip하면 MODIFIED 0개, 사이클 무한 의미. break + REASON=no_op로 처리.
- **`--auto` 무조건 신뢰 금지**: `--auto`는 사용자 보호를 위해 보수적으로 skip 많이 함. 자주 no_op로 종료된다면 .claude/bal-apply.json knob에 `asset_match` 추가가 필요할 가능성.
- **PlayTrace `play_end_key` 컨벤션 가정**: 위 fetch_metrics 는 `episode.cause` 하드코딩. 다른 게임에서는 `.claude/bal-run.json` 의 `play_end_key` 를 읽어야 함. 향후 metric_aliases 옆에 `play_end_key` 도 bal-converge config에 두는 게 깔끔.
- **bal-run discovery 호출**: bal-converge 사이클 안에서 bal-run을 처음 호출했는데 config(.claude/bal-run.json) 가 없으면 bal-run이 discovery flow로 빠져 AskUserQuestion을 띄움 — bal-converge의 무인 사이클을 멈춤. 사전 점검 (6)에서 막음.
- **Skill tool 결과의 noise**: bal-run/bal-apply stdout에 "Update available" 같은 unrelated 라인이 섞일 수 있음. SESSION_ID, PLAN:, MODIFIED: 같은 sentinel 라인을 `grep -oE` 로 명확히 추출.
- **`unity-cli` background-bash hang (2026-05-20 관찰)**: background bash (`run_in_background: true`) 안에서 `unity-cli exec` 를 연속 호출하면 두 번째 호출에서 hang하는 패턴이 v0.3.18/v0.3.19 모두에서 재현됨 (TTY/stdin pipe + connector handshake 조합 추정). 따라서 bal-converge 사이클이나 `--auto` 흐름 안의 `probe_field_value`, `unity-cli editor refresh` 같은 호출은 **foreground 단발 실행**으로만 발사할 것. 폴링 루프가 길어서 background로 떼고 싶다면 unity-cli 호출은 메인 루프 안에 두고 PlayTrace polling 같은 cheap한 작업만 background로 분리.
- **dashboard pane 누적**: 이 skill 의 "Dashboard 패널 셋업" 은 사이클 **시작 시점에 한 번만** 정리하고 정확히 2개 (CONVERGE_SURF + GAME_SURF) 를 만든다. bal-converge 안의 `/bal-run` 호출이 별도 dashboard pane 을 만들지 않도록 인라인 폴링 패턴을 권장 — Skill tool 로 그대로 호출하면 bal-run.SKILL.md §"대시보드 셋업" 이 또 split을 만들어 누적된다. 누적된 경우 다음 `/bal-converge` 실행이 자동 정리. 즉시 정리 원하면 `cmux list-pane-surfaces | awk '/\[browser\]/ {print $2}' | xargs -I{} cmux close-surface --surface {}`.
- **GAME_SURF 갱신 누락**: §[5](g) 의 `update_game_pane` 호출을 빠뜨리면 사이클이 진행돼도 GAME pane은 baseline session에 그대로 남아 사용자가 "왜 차트가 안 움직이지?" 라고 느낀다. baseline 측정 직후 (§[2.5]) 와 매 `/bal-run` 직후 (§[5](g)) 둘 다 호출 필수.
- **`cmux browser reload` 누락**: 새 session 이 서버에 막 생성된 직후 reload 없이 select 하면 드롭다운 캐시에 그 session 이 없어 NOP. `update_game_pane` 안에 reload + sleep 2 가 들어가 있는 이유.
- **cleanup 범위 = 현재 workspace**: `cmux list-pane-surfaces` 가 기본적으로 현재 workspace 만 보므로 다른 workspace 의 브라우저(예: Obsidian, ml-tensorboard) 는 안 닫힌다. 단, **현재 workspace 안에 dashboard 외 다른 브라우저 pane** (예: 사용자가 수동으로 열어둔 reference URL) 도 함께 닫힌다는 점 주의. 보존 원하면 사이클 시작 전에 다른 workspace 로 옮겨두기.

## 참조 자료

- **`PlayTrace/docs/MANUAL.md`** — PlayTrace API/필드/예제/대시보드 사용법의 **단일 진실 소스**. 이 스킬이 발사하는 `meta.kpi.*` / `meta.apply.*` 키의 payload 형식 (`POST /api/sessions`, `POST /api/logs`)·LogItem 응답·dashboard 폴링 동작 등은 모두 여기서 1차 확인. SKILL.md 본문 코드는 사용 패턴 가이드일 뿐, payload 형식 충돌 시 MANUAL.md 우선. (경로: `/Users/mingyukim/Documents/GitHub/018_Study-Koomin/PlayTrace/docs/MANUAL.md`)
- `.claude/skills/bal-run/SKILL.md` — N회 플레이 + 데이터 수집. session ID는 `session=<id>` 형태로 stdout emit.
- `.claude/skills/bal-apply/SKILL.md` — `--auto` 분기 표, `MODIFIED:`/`PLAN:` emit 규칙, `probe_field_value`.
- `.claude/bal-converge.json` — KPI/한도/metric_aliases/display_labels (이 skill의 캐시).
- `docs/hand-off.md` — Phase 1 검증 기록, derived target 설계 (spawn_rate UNRESOLVED 문제).
