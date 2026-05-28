---
name: bal-run
description: PlayTrace + cmux를 쓰는 게임의 inference 모델을 N회 자동 플레이로 검증하는 밸런스 테스트. 한 PlayTrace session 안에 play_no 1~N을 누적하고 각 한판에 timeout cap을 둔다. 프로젝트별 config(<project>/.Codex/bal-run.json)에 키 매핑을 캐시해 두 번째 실행부터는 인터뷰 없이 조용히 동작한다. 사용자가 "/bal-run", "bal run", "밸런스 테스트", "플레이테스트 N번", "inference N번 돌려", "한판 X분 캡" 같은 표현을 쓰면 사용. 선행 조건: /bal-init 완료 (inference wiring + PlayTrace 통합).
disable-model-invocation: false
---

# bal-run: 자동 N회 inference 플레이테스트

학습된 모델을 N회 자동 플레이로 돌려서 PlayTrace에 데이터를 쌓고, cmux 패널에 대시보드를 실시간 표시한 채 사용자가 관찰할 수 있는 상태를 만든다. 게임-종속 정보(어떤 key가 "한판 종료" 신호인지, 어떤 키를 차트할지, 표 컬럼이 무엇인지)는 프로젝트별 config 파일에서 읽어와 동작이 게임-비종속이다.

## 가장 중요한 두 가지 진실

### 1. PlayTrace MVP 컨벤션: "1 session = N play_no" (사망 시 자동 재시작)

PlayTrace API 자체는 session/play_no 관계에 강제가 없지만, **권장 패턴**은:
- `BeginSession`은 logger 부착 시 **1회만** 호출
- 게임이 종료(사망/실패)되면 새 episode가 시작되며 `play_no` 증가
- 같은 session 안에서 `play_no = 1, 2, 3, ...` 누적

이 컨벤션을 따르는 게임에서 "N번 테스트"는:

| ❌ 잘못 | ✅ 올바름 |
|---|---|
| `unity-cli editor play → stop` 사이클 N회 | `editor play` 1번 → play_end_key가 N번 누적될 때까지 폴링 → `editor stop` 1번 |
| 결과: session N개, 각 play_no=1 | 결과: session 1개, play_no 1~N |

이걸 거꾸로 하면 대시보드의 PlayNo 비교 UI가 무용지물이 된다.

**컨벤션 위반 감지**: run 중에 새 session ID가 또 생성되면 abort. 그 게임은 다른 패턴을 쓰는 것 — `/bal-init`으로 통합을 재점검해야 한다.

### 2. 대시보드 드롭다운은 페이지 로드 시점에 캐시된다.

새 session ID는 logger의 `BeginSession`이 호출되어야 서버에 존재한다 (보통 Editor play 후 4~6초). 그 이전에 로드된 대시보드는 그 session을 모른다. **새 session ID를 알아낸 직후** `cmux browser reload`로 강제 새로고침 → 드롭다운 재선택 → key chip 채우기를 한 묶음으로 한다.

---

## 인자 파싱

`/bal-run` 뒤에 자유 문자열이 붙는다. 추출할 두 값:

| 변수 | 의미 | 디폴트 |
|---|---|---|
| `N` | 플레이 횟수 | 5 |
| `TIMEOUT_SEC` | 한 판이 사망까지 허용되는 최대 시간 | 600 (10분) |

추출 휴리스틱 (전체 인자 문자열에서):

- `N`: `(\d+)\s*(번|회|판|times?)` 첫 매칭. 없으면 5.
- `TIMEOUT_SEC`: `(\d+)\s*(분|min|minutes?)` → 곱하기 60, 없으면 `(\d+)\s*(초|sec|s)` 첫 매칭, 둘 다 없으면 600.

추출이 끝나면 한 줄로 보고: `N=5, per-play timeout=10분으로 시작합니다.`

값이 비상식적이면 (`N=0`, `TIMEOUT_SEC<30`) `AskUserQuestion`으로 재확인.

---

## Config 로드 & 발견 (게임-비종속의 핵심)

### Config 파일 위치

```
<project>/.Codex/bal-run.json
```

스키마:

```json
{
  "schema_version": 1,
  "project_name": "<PlayTrace project_name>",
  "play_end_key": "<key that signals one play ended>",
  "chart_keys": ["...", "..."],
  "summary_columns": [
    {"label": "<column label>", "key": "<PlayTrace key>"},
    ...
  ]
}
```

### Step 1: Config 존재 체크

```bash
CFG=".Codex/bal-run.json"
if [ -f "$CFG" ]; then
  echo "[bal-run] config 발견 ($CFG) — 인터뷰 건너뜀."
  # 검증: 필수 필드 모두 존재?
  python3 -c "
import json, sys
c = json.load(open('$CFG'))
required = ['schema_version', 'project_name', 'play_end_key', 'chart_keys', 'summary_columns']
missing = [k for k in required if k not in c]
if missing: sys.exit('missing fields: ' + str(missing))
if c.get('schema_version') != 1: sys.exit('unsupported schema_version')
print('OK')
" || die "config 검증 실패. 수정 후 재실행하거나 파일을 삭제해 발견 flow를 다시 시작하세요."
else
  echo "[bal-run] config 없음 — 발견 flow 시작."
  # → Step 2
fi
```

### Step 2: 발견 flow (config 없을 때만)

**Step 2a — 후보 프로젝트 식별**

```bash
PROJECTS=$(curl -s "http://localhost:8000/api/sessions?limit=50" \
  | python3 -c "import json,sys; print('\n'.join(sorted(set(s['project_name'] for s in json.load(sys.stdin)))))")
```

- 1개면 그걸로 확정.
- 2+개면 `AskUserQuestion` (single select, 옵션은 후보 프로젝트 이름들).
- 0개면 cold start → Step 2b (아래).

**Step 2b — Cold start (PlayTrace 비어 있음)**

```
PlayTrace에 이 환경의 데이터가 아직 없습니다.
짧은 Editor Play로 데이터를 만든 후 다시 /bal-run을 실행해 주세요.
(약 30초 정도 플레이하면 충분합니다.)
```

이렇게 안내하고 종료. cold-start 시도는 grep으로 폴백할 수도 있지만, 발견 정확도가 낮으니 사용자에게 1회 play를 부탁하는 게 깔끔.

**Step 2c — 최근 세션의 키 분포 분석**

```bash
SESSION_DATA=$(curl -s "http://localhost:8000/api/sessions?project_name=$PROJECT&limit=5")
# 최근 세션 중 logs 있는 첫 번째 선택
for S in $(echo "$SESSION_DATA" | python3 -c "import json,sys;print('\n'.join(s['test_session_id'] for s in json.load(sys.stdin)))"); do
  LOGS=$(curl -s "http://localhost:8000/api/logs/search?test_session_id=$S&size=5000")
  CNT=$(echo "$LOGS" | python3 -c "import json,sys;print(len(json.load(sys.stdin)['items']))")
  [ "$CNT" -gt 10 ] && { CHOSEN_SESSION=$S; break; }
done
```

`$CHOSEN_SESSION`의 logs로 키별 (play_no당 발생 횟수) 분포 산출:

```python
# logs 데이터 후처리 — Python 인라인
import json, collections
data = json.loads(open('/tmp/logs.json').read())
by_kp = collections.Counter((l['key'], l['play_no']) for l in data['items'])
keys = sorted(set(k for k,p in by_kp))
plays = sorted(set(p for k,p in by_kp))

# play_no 당 정확히 1회씩 발생 = "per-play once" (play_end_key 또는 summary 후보)
per_play_once = [k for k in keys if all(by_kp[(k,p)] == 1 for p in plays)]
# play_no 당 여러 번 = 시계열 차트 후보
per_play_many = [k for k in keys if any(by_kp[(k,p)] >= 2 for p in plays)]
# 값 타입은 logs의 value_type으로 판단
key_types = {l['key']: l['value_type'] for l in data['items']}
per_play_many_numeric = [k for k in per_play_many if key_types.get(k) == 'number']
```

**Step 2d — `AskUserQuestion` 세 묶음**

질문 1 (`play_end_key`, single select):

```
header: Play end key
question: "한 판이 끝났음을 알리는 키는 무엇인가요? 보통 매 플레이 끝에 한 번씩 보내는 키입니다 (예: episode.cause, episode.result, episode.end)."
options: per_play_once 중 이름에 'cause' / 'end' / 'death' / 'result' 포함된 것을 상위로 배치, 최대 4개 (나머지는 'Other')
```

질문 2 (`chart_keys`, multiSelect):

```
header: Chart keys
question: "대시보드에서 실시간으로 보고 싶은 시계열 키들을 고르세요 (4~8개 권장)."
options: per_play_many_numeric (numeric time-series 후보)
```

질문 3 (`summary_columns`, multiSelect):

```
header: Summary columns
question: "한판 끝난 후 요약 표에 보여줄 키들을 고르세요 (5~10개 권장)."
options: per_play_once
```

질문 3 응답으로 받은 키들 각각에 대해 label은 키의 마지막 dot-segment를 카멜케이스로 변환해 디폴트 부여. 사용자가 다음 질문으로 라벨을 다듬고 싶어하면 받지만 일단 자동 라벨로 충분.

**Step 2e — Config 저장**

```python
config = {
    "schema_version": 1,
    "project_name": PROJECT,
    "play_end_key": ANSWER_Q1,
    "chart_keys": ANSWER_Q2,
    "summary_columns": [{"label": auto_label(k), "key": k} for k in ANSWER_Q3]
}
json.dump(config, open(".Codex/bal-run.json", "w"), indent=2, ensure_ascii=False)
```

`.Codex/` 디렉토리가 없으면 `mkdir -p` 먼저. 저장 후 사용자에게 "config가 `.Codex/bal-run.json` 에 저장됐습니다. 다음부터는 이 파일을 읽고 인터뷰 건너뜁니다." 안내.

---

## 사전 점검 (config 로드 후)

다음 3개를 확인. 하나라도 실패하면 중단.

```bash
# (1) PlayTrace 서버
curl -s -m 3 http://localhost:8000/health | grep -q '"ok"' \
  || die "PlayTrace 서버가 http://localhost:8000 에 응답하지 않습니다."

# (2) unity-cli
command -v unity-cli >/dev/null \
  || die "unity-cli가 PATH에 없습니다."

# (3) Editor 응답
unity-cli exec "return 1+1;" 2>/dev/null | grep -q '^2$' \
  || die "Unity Editor가 응답하지 않습니다."

# (4) cmux 환경 (선택)
if [ -z "$CMUX_WORKSPACE_ID" ]; then
  echo "[warn] cmux 환경 아님 — 대시보드 분할 표시는 스킵."
  HAS_CMUX=0
else
  HAS_CMUX=1
fi

# (5) [MANDATORY when HAS_CMUX=1] dashboard pane 점검 — §"대시보드 셋업" 진입.
# 단독 호출이든 bal-converge 안의 인라인 호출이든, dashboard pane 이 살아있고 fresh 한지 확인.
# 세션 resume / pane 손실 / 다른 workspace 이동 등으로 매번 점검 필요. 사용자가 묻기 전 처리.
```

> **흐름 진입의 절대 룰**: `/bal-run` 호출 / 세션 resume / 재시도 — 어떤 경우든 (1)~(5) 사전 점검을 모두 통과시킨다. (5) dashboard pane 점검은 cmux 환경이면 mandatory; 사용자가 "dashboard 안 보여?" 묻기 전에 reflexively 챙긴다. bal-converge 안에서 호출될 때는 caller 가 이미 두 pane (CONVERGE+GAME) 을 만들어 놨을 수 있으므로, 그 경우엔 GAME pane 만 §"대시보드를 새 session으로 맞추기" 단계에서 갱신.

---

## 대시보드 셋업 (cmux 환경에서만)

```bash
# 기존 GAME 용 dashboard pane 이 살아있나? (bal-converge caller 또는 직전 단독 호출이 남긴 것 재사용)
EXISTING_GAME=$(cmux list-pane-surfaces 2>/dev/null | awk '/\[browser\]/ {print $2}' | tail -1)
if [ -n "$EXISTING_GAME" ]; then
  SURF="$EXISTING_GAME"
  echo "Reusing dashboard surface → $SURF"
else
  SURF=$(cmux new-pane --type browser --direction right --url "http://localhost:8000/dashboard?mode=game" 2>&1 | grep -oE 'surface:[0-9]+' | head -1)
  echo "Created dashboard surface → $SURF"
fi
sleep 2
```

`SURF`가 비면 대시보드 단계만 스킵하고 폴링은 진행. 드롭다운/키 chip 채우기는 **새 session ID 확보 후** 진행.

---

## Editor Play 시작 → 새 session ID 캡처

```bash
unity-cli editor play --wait 2>&1 | tail -2
START_MS=$(($(date +%s) * 1000))
START_SEC=$(date +%s)
sleep 5  # logger가 BeginSession 발사할 여유

PROJECT=$(jq -r .project_name $CFG)

# 새 session 발견 (project 필터 적용, START_MS 이후 created_at)
discover_session() {
  curl -s "http://localhost:8000/api/sessions?project_name=$PROJECT&limit=5" | \
    START_MS=$START_MS python3 -c "
import json, sys, os
start = int(os.environ['START_MS'])
data = json.load(sys.stdin)
for s in data:
    if s.get('created_at', 0) >= start - 2000:  # 2s 클럭 드리프트 여유
        print(s['test_session_id'] + '\t' + s['version'])
        return
"
}

SESSION_DATA=$(discover_session)
if [ -z "$SESSION_DATA" ]; then
  sleep 5
  SESSION_DATA=$(discover_session)
fi

[ -z "$SESSION_DATA" ] && die "Editor play 후 새 PlayTrace session이 생성되지 않았습니다. PlayTrace logger가 부착되지 않았거나(주: training 모드 가드 / /bal-init 미완료 / player 미스폰), config의 project_name이 잘못됐을 가능성. 가장 최근 사용 project_name을 확인하려면 '/api/sessions?limit=5'."

SESSION_ID=$(echo "$SESSION_DATA" | cut -f1)
VERSION=$(echo "$SESSION_DATA" | cut -f2)
echo "Captured session=$SESSION_ID (version=$VERSION)"
```

여기서 `version`은 config에 없음 — 매 실행마다 달라질 수 있으니 (학습 결과 새 모델) PlayTrace에서 즉석 발견. project_name과 play_end_key만 stable한 정보로 config에 박는다.

---

## 대시보드를 새 session으로 맞추기 (cmux일 때)

```bash
cmux browser reload --surface $SURF >/dev/null
sleep 2
cmux browser select --surface $SURF "#sel-project" "$PROJECT" >/dev/null
sleep 1
cmux browser select --surface $SURF "#sel-version" "$VERSION" >/dev/null
sleep 1
cmux browser select --surface $SURF "#sel-session" "$SESSION_ID" >/dev/null
sleep 1

# config.chart_keys 자동 추가
CHART_KEYS=$(jq -r '.chart_keys[]' $CFG)
for K in $CHART_KEYS; do
  cmux browser fill --surface $SURF "#sel-key" "$K" >/dev/null 2>&1
  cmux browser press --surface $SURF "Enter" >/dev/null 2>&1
  sleep 0.3
done

cmux set-status bal-run "session=$SESSION_ID, play 0/$N" --icon hourglass
cmux set-progress 0.0 --label "Plays 0/$N"
```

---

## 폴링 루프

```bash
PLAY_END_KEY=$(jq -r .play_end_key $CFG)
LAST_DEATH_TS=$START_SEC
LAST_COUNT=0
TIMEOUT_HIT=""

while true; do
  RESP=$(curl -s "http://localhost:8000/api/logs/search?test_session_id=$SESSION_ID&key=$PLAY_END_KEY&size=5000")
  COUNT=$(echo "$RESP" | python3 -c "import json,sys; print(len(json.load(sys.stdin).get('items',[])))" 2>/dev/null || echo 0)
  NOW=$(date +%s)
  ELAPSED=$((NOW - START_SEC))

  # 컨벤션 위반 감지: run 중 새 session이 또 생기면 abort
  LATEST_SESSION=$(curl -s "http://localhost:8000/api/sessions?project_name=$PROJECT&limit=1" \
    | python3 -c "import json,sys;d=json.load(sys.stdin);print(d[0]['test_session_id'] if d else '')")
  if [ -n "$LATEST_SESSION" ] && [ "$LATEST_SESSION" != "$SESSION_ID" ]; then
    die "이 게임은 'session 1개 × N play' 컨벤션을 따르지 않는 것 같습니다 (run 중 새 session $LATEST_SESSION 생성됨). MLBalanceLogger 패턴(BeginSession 1회) 으로 재구성하거나 /bal-init으로 통합 점검 권장."
  fi

  # 새 play_end 감지
  if [ "$COUNT" -gt "$LAST_COUNT" ]; then
    PLAY_DUR=$((NOW - LAST_DEATH_TS))
    echo "[${ELAPSED}s] PLAY_END #${COUNT} (this play: ${PLAY_DUR}s)"
    LAST_COUNT=$COUNT
    LAST_DEATH_TS=$NOW
    [ "$HAS_CMUX" = "1" ] && {
      cmux set-status bal-run "play $COUNT/$N" --icon hourglass
      cmux set-progress $(awk "BEGIN{print $COUNT/$N}") --label "Plays $COUNT/$N"
    }
  fi

  # 목표 도달
  if [ "$COUNT" -ge "$N" ]; then
    echo "All $N plays complete at ${ELAPSED}s"
    break
  fi

  # 한판 타임아웃
  CURRENT_PLAY_AGE=$((NOW - LAST_DEATH_TS))
  if [ "$CURRENT_PLAY_AGE" -gt "$TIMEOUT_SEC" ]; then
    TIMEOUT_HIT="play_no=$((COUNT+1)) ran ${CURRENT_PLAY_AGE}s without play_end"
    echo "[${ELAPSED}s] TIMEOUT — $TIMEOUT_HIT"
    break
  fi

  sleep 10
done

unity-cli editor stop 2>&1 | tail -1
```

핵심:
- **컨벤션 위반 방어**: 폴링마다 최신 session 확인. 다른 session ID가 떴다면 logger 패턴이 PlayTrace MVP 권장 패턴과 다르므로 abort.
- **`PLAY_END_KEY` 동적**: config에서 읽어 game-agnostic. 이 게임은 `episode.cause`, 다른 게임은 `match.result` 등.
- **per-play timeout**: `LAST_DEATH_TS` 갱신으로 "이번 판"만 측정.

> **메모**: foreground bash가 부담스러우면 (예: N=50, 평균 60s/play = 50분) `run_in_background: true` + task-notification 모델로 전환. 진행 중간 상황은 cmux 사이드바의 set-progress로만 표시.

---

## 요약 보고

config의 summary_columns에 따라 표를 동적 생성:

```bash
python3 <<EOF
import json, urllib.request

CFG = json.load(open(".Codex/bal-run.json"))
cols = CFG["summary_columns"]
PLAY_END_KEY = CFG["play_end_key"]

data = json.loads(urllib.request.urlopen("http://localhost:8000/api/logs/search?test_session_id=$SESSION_ID&size=5000").read())
items = data['items']
plays = {}
for i in items:
    p = i.get('play_no')
    if p is None: continue
    plays.setdefault(p, {})
    # value extraction by value_type
    vt = i.get('value_type')
    if vt == 'number': v = i.get('value_number')
    elif vt == 'text': v = i.get('value_text')
    elif vt == 'bool': v = i.get('value_bool')
    else: v = None
    plays[p][i['key']] = v

# 헤더
header = "Play  " + "  ".join(f"{c['label']:<10}" for c in cols)
print(header)
print("-" * len(header))

# 행 — play_end_key가 있는 play만 (= 완료된 play)
totals = {c['label']: [] for c in cols}
n = 0
for p in sorted(plays):
    if PLAY_END_KEY not in plays[p]:
        continue
    n += 1
    row = f"{p:<5} "
    for c in cols:
        v = plays[p].get(c['key'])
        if v is None:
            row += f"{'-':<11} "
        elif isinstance(v, float):
            row += f"{v:<11.2f}"
            totals[c['label']].append(v)
        elif isinstance(v, int):
            row += f"{v:<11}"
            totals[c['label']].append(v)
        else:
            row += f"{str(v):<11.10s}"
    print(row.rstrip())

# AVG (numeric만)
if n:
    print("-" * len(header))
    avg_row = f"{'AVG':<5} "
    for c in cols:
        vals = totals[c['label']]
        if vals and all(isinstance(v,(int,float)) for v in vals):
            avg_row += f"{sum(vals)/len(vals):<11.2f}"
        else:
            avg_row += f"{'':<11}"
    print(avg_row.rstrip())
EOF
```

표 아래에 timeout 있었으면 한 줄로 명시. 자연어 코멘트는 보수적으로 — 데이터로 단정 가능한 경우만:

- 생존시간 stddev < 5s → "스폰 강도 곡선의 구조적 컷오프 가능성"
- 같은 사망 원인 ≥ 80% → "특정 적/요인이 일관된 종료 트리거"

---

## 종결

```bash
if [ "$HAS_CMUX" = "1" ]; then
  if [ -z "$TIMEOUT_HIT" ]; then
    cmux set-status bal-run "Done ($N plays)" --icon check
    cmux set-progress 1.0 --label "Plays $N/$N"
  else
    cmux set-status bal-run "Timeout at play $((LAST_COUNT+1))" --icon warning
  fi
fi
```

대시보드 surface는 닫지 않는다 — 사용자가 데이터를 둘러보고 싶을 수 있음.

---

## 타임아웃 정책 (왜 한판 timeout이 run 전체 중단인가)

기본 동작: 한 판이 `TIMEOUT_SEC`를 넘으면 **run 전체 중단**. 그 판은 미완료로 표에서 제외하고 timeout 메시지를 첨부.

이유:
1. **게임-비종속을 위해**. 한 판만 강제로 끝내려면 게임별 force-kill API가 필요하다 (`player.SetHp(0)` 종류). 이 스킬은 PlayTrace + cmux만 공유하면 어느 게임에서도 동작해야 한다.
2. **Session 격리 깨짐**. Editor를 stop/play로 재시작하면 새 PlayTrace session이 생성되어 "1 session × N play_no" 컨벤션이 깨진다.
3. **정상 inference면 timeout이 거의 안 발생**. 정상 종료 시간이 분 단위인 모델이 10분(디폴트)을 넘는 건 거의 확실히 비정상 → run 전체가 의미를 잃음.

사용자가 명시적으로 "타임아웃 판만 건너뛰고 계속해"를 요청하면, 그때 게임별 force-end 훅을 `AskUserQuestion`으로 받아 처리.

---

## 자주 빠지는 함정 (체크리스트)

- **Session ≠ play 매핑 오류**: 반복적으로 `editor play → stop` 사이클을 돌리고 있다면 즉시 멈추고 폴링 모델로 갈아탄다.
- **컨벤션 위반**: 폴링 중 새 session 발견 시 abort — logger 패턴이 PlayTrace MVP 권장과 다르다는 신호.
- **Config out of date**: 게임이 새 키를 추가하거나 기존 키 이름을 바꾸면 chart_keys/summary_columns가 빈 컬럼/누락 값이 됨. 사용자가 데이터에 의문이 들면 `.Codex/bal-run.json` 갱신 안내.
- **Project_name 불일치**: config의 project_name이 PlayTrace에 실제 존재하는 것과 다르면 새 session 발견 실패 (NULL). 에러 메시지에서 `/api/sessions` 확인 안내.
- **대시보드 드롭다운 캐시**: 새 session 생성 전에 드롭다운 채우면 그 session이 안 보임. 항상 새 session ID 확보 직후 `cmux browser reload`.
- **`set-progress` 0 미입력**: 시작 시점에 progress=0.0 안 하면 이전 run의 바가 남음.
- **`cmux browser` 명령에 `--surface` 누락**: 새 surface 만들고도 이후 명령이 다른 surface로 향해 NOP. 모든 browser 명령에 `$SURF` 명시.
- **`unity-cli editor play --wait` 응답 무시**: `manage_editor sent (connection closed before response)`는 정상. play 진입 여부는 `unity-cli exec "return Application.isPlaying;"`로 별도 확인.
- **Value 컬럼 혼동**: `value_text` / `value_number` / `value_bool` 중 어느 것을 읽을지는 `value_type`으로 분기. `value_text or value_number`로 합치면 0과 None과 빈 문자열이 섞임.
- **시간 단위**: PlayTrace `client_time`은 ms, bash `date +%s`는 초. `START_MS = sec * 1000` 명시 변환.

## 참조 자료

- **`PlayTrace/docs/MANUAL.md`** — PlayTrace API/필드/예제/대시보드 사용법의 **단일 진실 소스**. POST/GET payload, LogItem `value_type` 분기, session 생성 규칙, 안티패턴 등 모두 여기서 1차 확인. (이 프로젝트의 PlayTrace 경로: `/Users/mingyukim/Documents/GitHub/018_Study-Koomin/PlayTrace/docs/MANUAL.md`)
- `.Codex/skills/bal-init/SKILL.md` — 선행 셋업 (inference wiring + PlayTrace 통합). 이게 안 되어 있으면 bal-run은 안 돌아간다.
- `.Codex/skills/bal-init/references/playtrace-api.md` — PlayTrace API 컨닝페이퍼 (MANUAL.md 요약본 — 충돌 시 MANUAL.md 우선).
- `<project>/.Codex/bal-run.json` — 이 프로젝트의 키 매핑. 첫 실행 시 인터뷰로 만들어지고 이후 자동 사용.
