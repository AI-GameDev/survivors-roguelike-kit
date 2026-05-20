---
name: bal-apply
description: bal-run 결과를 분석해 밸런스 문제를 자동 진단하고, ScriptableObject 자산을 수정해 적용 후 git diff로 프리뷰/롤백을 지원하는 스킬. 진단 → 추천 → 사용자 승인 → unity-cli로 자산 편집 → git diff → 확정/롤백 흐름. 프로젝트별 knob 매핑은 <project>/.claude/bal-apply.json에 캐시되며, 다른 게임에 이식 가능. 사용자가 "/bal-apply", "bal apply", "밸런스 적용", "밸런싱 수정", "밸런스 자동 튜닝", "Gear 약화" 같은 표현을 쓰면 사용. 선행 조건: /bal-run 최소 3회 이상 완료.
disable-model-invocation: false
---

# bal-apply: bal-run 결과로 밸런스 자동 적용

`/bal-run`이 만든 PlayTrace 데이터를 입력으로 받아, **일반 진단 휴리스틱 → 추천 → 사용자 승인 → 자산 편집 → git diff 프리뷰 → 롤백 가능 상태**까지 한 번에 가는 스킬. 진단 규칙은 게임-비종속(`episode.*` 키만 사용), "어떤 자산의 어떤 필드를 돌릴까"는 프로젝트별 config가 답한다.

## 큰 그림

```
[1] 최근 bal-run 세션 → PlayTrace에서 데이터 fetch
        ↓
[2] config 로드 (.claude/bal-apply.json)
        ↓
[3] 6개 진단 휴리스틱 실행 → finding 목록 (severity 포함)
        ↓
[4] finding.triggers ↔ knob.triggers 매칭 → 추천 액션
        ↓
[5] AskUserQuestion 묶음 — 어떤 finding을 어떤 knob으로, 변경량은?
        ↓
[6] unity-cli + SerializedObject로 자산 수정
        ↓
[7] git diff -- Assets/ 표시
        ↓
[8] 사용자: keep | rollback 선택
        ↓
[9] "/bal-run 으로 재테스트" 안내
```

## 인자 파싱

`/bal-apply` 뒤 자유 문자열에서:

| 인자 | 의미 | 디폴트 |
|---|---|---|
| `session=<id>` | 분석할 PlayTrace session_id. 없으면 latest 자동 선택 | latest |
| `dry-run` 또는 `--dry-run` | 진단/추천만, 적용 안 함 | off |
| `knob=<id>` | 특정 knob만 추천 (예: `knob=enemy_damage`) | all |
| `--auto` | 모든 AskUserQuestion 단계를 자동 분기로 우회 (무인 모드). bal-converge 같은 오케스트레이션 skill에서 사용. 분기 표는 아래 "--auto 모드 분기 표" 참조 | off |

예시:
- `/bal-apply` → 최신 session, full apply (대화형)
- `/bal-apply dry-run` → 분석만 (대화형)
- `/bal-apply session=20260516_002 knob=spawn_rate` → 특정 session, 특정 knob
- `/bal-apply --auto session=20260516_006` → 무인 자동 적용 (bal-converge 사이클에서 호출)
- `/bal-apply --dry-run --auto session=20260516_006` → 무인 dry-run: 진단 + 매칭만, `PLAN:<knob>|<asset>|<field>|<from>|<to>` 한 줄씩 stdout emit (bal-converge Plan Preview용)

### --auto 모드 분기 표

`--auto` 시 모든 AskUserQuestion이 자동으로 결정된다. 각 지점에서:

| 지점 | --auto 동작 | exit code |
|---|---|---|
| Cold start (config 없음) | 즉시 abort, stderr `[bal-apply] no config — converge cannot proceed` | 2 |
| Finding별 knob 선택 | `triggers` 매칭 knob 중 첫 번째 자동 Apply. 매칭 없으면 Skip | 0 |
| Dirty asset 경고 | 즉시 abort, stderr `UNCOMMITTED_CHANGES on: <paths>` | 3 |
| 자산 후보 다수 (단정 불가) | 해당 knob skip, 다음 finding 진행 (전체 abort 아님) | 0 |
| Keep / Rollback | 자동 Keep. 수정된 각 경로를 stdout에 `MODIFIED:<path>` 한 줄씩 emit | 0 |

`--dry-run --auto` 추가 규칙:
- 자산 후보 다수든 단정 가능이든, *자산 편집은 하지 않음*.
- 적용했을 변경을 계산만 해서 `PLAN:<knob_id>|<asset_path>|<field_path>|<from_value>|<to_value>` 한 줄씩 stdout emit.
- `to_value`는 propertyType을 미리 알 수 없으므로, 디스크에서 현재 값을 읽고 `adjust_default` 적용한 *예상 값* (Integer면 round, Float면 그대로). 실제 적용 시 차이날 수 있다는 주의 안내는 불필요 (Preview 목적).
- 자산을 단정 못한 knob은 `PLAN:<knob_id>|<UNRESOLVED>|<field>|||` 형태로 emit.

---

## 사전 점검

```bash
# (1) PlayTrace
curl -s -m 3 http://localhost:8000/health | grep -q '"ok"' \
  || die "PlayTrace 서버 응답 없음 (http://localhost:8000)."

# (2) unity-cli
command -v unity-cli >/dev/null || die "unity-cli 필요."
unity-cli exec "return 1+1;" 2>/dev/null | grep -q '^2$' \
  || die "Unity Editor 응답 없음."

# (3) git (rollback 위해)
git rev-parse --is-inside-work-tree >/dev/null 2>&1 \
  || die "git 저장소가 아닙니다. rollback 지원이 필요해 git이 필수입니다."

# (4) Editor가 play mode면 안전상 abort
unity-cli exec "return Application.isPlaying;" 2>/dev/null | grep -q '^true$' \
  && die "Editor가 play 중입니다. stop 후 다시 실행해 주세요. (play 중 SaveAssets 시 자산 손상 가능)"

# (5) config 로드
CFG=".claude/bal-apply.json"
[ -f "$CFG" ] || handle_cold_start  # 아래 "Cold start" 섹션
```

`die`는 명령 안의 exit가 아니라, 모델이 사용자에게 즉시 이유를 텍스트로 출력하고 스킬을 종료한다는 의미.

---

## Config 로드 & 검증

```bash
python3 -c "
import json, sys
c = json.load(open('$CFG'))
required = ['schema_version', 'diagnostics', 'knobs']
missing = [k for k in required if k not in c]
if missing: sys.exit('missing: ' + str(missing))
if c['schema_version'] != 1: sys.exit('schema_version != 1')
if not isinstance(c['knobs'], list) or len(c['knobs']) == 0:
    sys.exit('knobs[] empty — config 작성 필요')
print('OK ({} knobs)'.format(len(c['knobs'])))
" || die "config 검증 실패. .claude/bal-apply.json 확인 또는 삭제 후 재실행."
```

---

## 세션 선택 & 데이터 fetch

```bash
# bal-run.json이 있으면 project_name 활용
PROJECT=$(jq -r '.project_name // empty' .claude/bal-run.json 2>/dev/null)
PROJ_FILTER=""
[ -n "$PROJECT" ] && PROJ_FILTER="project_name=$PROJECT&"

if [ -n "$SESSION_ARG" ]; then
  SESSION_ID="$SESSION_ARG"
else
  SESSION_ID=$(curl -s "http://localhost:8000/api/sessions?${PROJ_FILTER}limit=1" \
    | python3 -c "import json,sys;d=json.load(sys.stdin);print(d[0]['test_session_id'] if d else '')")
fi

[ -z "$SESSION_ID" ] && die "분석할 session이 없습니다. /bal-run 으로 데이터를 먼저 만들어 주세요."

echo "[bal-apply] analyzing session $SESSION_ID"

# 전체 로그
curl -s "http://localhost:8000/api/logs/search?test_session_id=$SESSION_ID&size=5000" > /tmp/balapply_logs.json
```

`/tmp/balapply_logs.json` 이 분석 입력. 이후 Python으로 가공.

---

## 진단 휴리스틱

`bal-run.json` 의 `play_end_key` 와 `summary_columns`를 참고해 진단 키 이름을 결정. 없으면 컨벤션 디폴트 사용:

```python
DEFAULTS = {
    "play_end_key": "episode.cause",
    "duration_key": "episode.duration_sec",
    "kills_key": "episode.total_kills",
    "damage_taken_key": "episode.total_damage_taken",
    "level_key": "episode.final_level",
    "killer_key": "episode.last_hit_attacker",
    "burst_key": "episode.recent_5s_damage_taken",
}
```

진단 규칙 6개:

```python
import json, statistics, collections

logs = json.load(open("/tmp/balapply_logs.json"))['items']
cfg = json.load(open(".claude/bal-apply.json"))
dx = cfg['diagnostics']

# play_no별 episode.* 모으기
plays = collections.defaultdict(dict)
for l in logs:
    pn = l.get('play_no')
    if pn is None: continue
    vt = l.get('value_type')
    if vt == 'number': v = l.get('value_number')
    elif vt == 'text': v = l.get('value_text')
    else: v = l.get('value_bool')
    plays[pn][l['key']] = v

# play_end_key 가 있는 play만 완료로 인정
PLAY_END = DEFAULTS['play_end_key']
completed = {pn: p for pn, p in plays.items() if PLAY_END in p}
N = len(completed)

if N < dx['min_runs_for_analysis']:
    abort(f"표본 부족 ({N} plays, {dx['min_runs_for_analysis']} 필요). /bal-run 으로 데이터를 더 모아 주세요.")

findings = []

# 1) killer_concentration — 한 적이 사망 원인의 상당 비율
killers = [p.get(DEFAULTS['killer_key']) for p in completed.values() if p.get(DEFAULTS['killer_key'])]
if killers:
    top, top_count = collections.Counter(killers).most_common(1)[0]
    ratio = top_count / N
    if ratio >= dx['killer_concentration_threshold']:
        findings.append({
            'id': 'killer_concentration',
            'severity': 'high',
            'msg': f"'{top}' 가 사망 원인의 {int(ratio*100)}% ({top_count}/{N})",
            'target_value': top  # asset matching에 사용
        })

# 2) structural_duration_cutoff — 생존시간이 거의 일정
durations = [p.get(DEFAULTS['duration_key']) for p in completed.values()]
durations = [d for d in durations if isinstance(d, (int, float))]
if len(durations) >= 3:
    mean = statistics.mean(durations)
    sd = statistics.stdev(durations) if len(durations) > 1 else 0
    if mean > 0 and sd / mean < dx['duration_stddev_ratio_max']:
        findings.append({
            'id': 'structural_duration_cutoff',
            'severity': 'high',
            'msg': f"생존시간 거의 일정 (mean={mean:.1f}s, σ={sd:.1f}s = {sd/mean*100:.1f}%) — 스폰/난이도 구조적 컷오프 가능성"
        })

# 3) low_kill_rate
kills_per_sec = []
for p in completed.values():
    d = p.get(DEFAULTS['duration_key'])
    k = p.get(DEFAULTS['kills_key'])
    if isinstance(d,(int,float)) and isinstance(k,(int,float)) and d>0:
        kills_per_sec.append(k/d)
if kills_per_sec:
    m = statistics.mean(kills_per_sec)
    if m < dx['min_dps']:
        findings.append({
            'id': 'low_kill_rate',
            'severity': 'med',
            'msg': f"처치 효율 낮음 (mean={m:.2f} kills/sec, 기준 {dx['min_dps']})"
        })

# 4) level_cap_low
levels = [p.get(DEFAULTS['level_key']) for p in completed.values()]
levels = [l for l in levels if isinstance(l,(int,float))]
if levels:
    m = statistics.mean(levels)
    if m < dx['level_cap_floor']:
        findings.append({
            'id': 'level_cap_low',
            'severity': 'med',
            'msg': f"성장 둔화 (avg final_level={m:.1f}, 기준 {dx['level_cap_floor']})"
        })

# 5) burst_damage_at_death
bursts = [(p.get(DEFAULTS['burst_key']), p.get(DEFAULTS['duration_key']), p.get(DEFAULTS['damage_taken_key']))
          for p in completed.values()]
bursts = [(b,d,t) for b,d,t in bursts if all(isinstance(x,(int,float)) for x in (b,d,t)) and d>0]
if bursts:
    burst_avg = statistics.mean(b for b,_,_ in bursts)
    overall_rate_avg = statistics.mean(t*5/d for _,d,t in bursts)  # 5초당 평균 데미지
    if overall_rate_avg > 0 and burst_avg > overall_rate_avg * dx['burst_damage_multiplier']:
        findings.append({
            'id': 'burst_damage_at_death',
            'severity': 'high',
            'msg': f"사망 직전 5초 burst (burst avg={burst_avg:.1f} vs 평균 5초 {overall_rate_avg:.1f})"
        })

# 6) high_damage_taken_variance
dmgs = [p.get(DEFAULTS['damage_taken_key']) for p in completed.values()]
dmgs = [d for d in dmgs if isinstance(d,(int,float))]
if len(dmgs) >= 3:
    m = statistics.mean(dmgs); sd = statistics.stdev(dmgs)
    if m > 0 and sd/m > dx['damage_variance_max']:
        findings.append({
            'id': 'high_damage_taken_variance',
            'severity': 'low',
            'msg': f"방어 불안정 (damage_taken σ/μ={sd/m:.2f})"
        })

# 결과 출력
if not findings:
    print("진단 결과: 이상 없음. 이번 표본에서 발견된 유의미 패턴이 없습니다.")
else:
    print("진단 결과:")
    for f in findings:
        print(f"  [{f['severity']}] {f['id']}: {f['msg']}")
```

이 6개 외에 게임-종속 진단을 추가하려면 config의 `diagnostics` 섹션에 임계값을 더 박고 스킬 본문에 if-블록을 추가하는 게 단순하지만, MVP는 위 6개로 충분.

---

## Findings → Knob 매칭

각 finding의 `id` 가 knob의 `triggers` 배열에 들어있는지 검사. 한 finding당 여러 knob 후보가 나올 수 있음.

```python
candidates = {}
for f in findings:
    matched = [k for k in cfg['knobs'] if f['id'] in k.get('triggers', [])]
    if matched:
        candidates[f['id']] = (f, matched)

if not candidates:
    print("\nfinding은 있으나 매칭되는 knob이 없습니다. .claude/bal-apply.json에 해당 trigger를 가진 knob을 추가하거나 dry-run 결과로 수동 대처해 주세요.")
```

`knob=<id>` 인자로 필터링: `candidates`를 그 id만 남기고 잘라냄.

---

## AskUserQuestion 묶음

각 finding 별로 사용자에게 액션을 묻는다. 한꺼번에 묶지 않고 finding 단위로 진행 (선택지 명확).

각 질문의 옵션:
- 각 매칭된 knob: "Apply <knob.label> (default <adjust_default*100>%)"
- "Skip this finding"
- 옵션 4개 한도 내에 들어가도록 매칭 knob 수가 3개 넘으면 상위 priority 3개만 (간단히 first-3)

`adjust_default` 가 다르거나 사용자가 다른 값을 원하면 옵션 라벨 옆에 "(custom)" 도 둘 수 있지만 MVP에선 default만. 사용자가 다른 값을 원하면 다음 turn에서 명시적으로 인자로 호출.

```python
# pseudocode
for fid, (finding, knobs) in candidates.items():
    if AUTO:
        # 매칭 knob 중 첫 번째 (priority order)를 자동 선택. 매칭 없으면 Skip.
        # action == "open_in_unity" 같은 manual knob은 --auto에서 skip 처리 (자동 적용 불가).
        auto_knobs = [k for k in knobs if k.get('action') != 'open_in_unity']
        if auto_knobs:
            decisions.append((finding, auto_knobs[0]))
        # else: 매칭 0개 또는 모두 manual → skip (decisions에 추가 안 함)
        continue

    answer = ask_user_question(
        question=f"[{finding['severity']}] {finding['msg']}\n어떻게 처리할까요?",
        options=[
            *[f"Apply {k['label']} ({k['adjust_default']*100:+.0f}%)" for k in knobs[:3]],
            "Skip this finding"
        ]
    )
    if answer != "Skip this finding":
        chosen_knob = knobs[<index>]
        decisions.append((finding, chosen_knob))
```

---

## Pre-flight git 안전 체크

```bash
# 영향받을 자산 경로 추측 (각 결정의 asset_glob 기반)
AFFECTED_PATHS=()
for d in "${decisions[@]}"; do
  AFFECTED_PATHS+=( $(eval ls -1 ${d.asset_glob} 2>/dev/null) )
done

# 그 경로들이 이미 미커밋 변경이면 경고
DIRTY=$(git status --porcelain "${AFFECTED_PATHS[@]}" 2>/dev/null | head)
if [ -n "$DIRTY" ]; then
  if [ "$AUTO" = "1" ]; then
    # --auto 모드: 사용자 변경 보호. 즉시 abort.
    echo "UNCOMMITTED_CHANGES on: $(echo "$DIRTY" | awk '{print $2}' | tr '\n' ' ')" >&2
    exit 3
  fi

  echo "다음 자산에 이미 미커밋 변경이 있습니다:"
  echo "$DIRTY"
  echo "rollback 시 사용자가 만든 변경도 함께 사라질 위험이 있습니다."
  # AskUserQuestion: "Continue with risk / Abort"
fi
```

dry-run이면 여기서 종료하고 추천 텍스트만 보고 — 단, `--dry-run --auto` 조합이면 추천을 사람용 텍스트가 아니라 `PLAN:<knob_id>|<asset_path>|<field_path>|<from>|<to>` 한 줄씩 stdout으로 emit (위 "--auto 모드 분기 표" 참조).

---

## 자산 편집 (unity-cli exec)

knob 하나당 (asset 발견 → 편집 → SetDirty → Save). knob action 별 분기.

루프 시작 전 누적 배열 초기화:

```bash
declare -a MODIFIED_PATHS=()
```

`edit_one_knob` 내부에서 적용 성공할 때마다 `MODIFIED_PATHS+=("$ASSET_PATH")` 로 append (Keep/Rollback 단계에서 사용, `--auto` 모드의 `MODIFIED:` emit에도 사용).

```bash
edit_one_knob() {
  local KNOB_JSON="$1"
  local TARGET_VALUE="$2"   # asset_match.name_match_log_key 의 PlayTrace 값 (예: "Gear")

  local ASSET_PATH=$(python3 <<EOF
import json, glob, os
k = json.loads('''$KNOB_JSON''')
target = "$TARGET_VALUE" or None
candidates = glob.glob(k['asset_glob'], recursive=True)
# .meta 제외
candidates = [c for c in candidates if not c.endswith('.meta')]
if target and k.get('asset_match', {}).get('name_mode') == 'filename':
    # 파일명 stem이 target과 일치하는 것
    chosen = next((c for c in candidates if os.path.splitext(os.path.basename(c))[0].lower() == target.lower()), None)
elif len(candidates) == 1:
    chosen = candidates[0]
else:
    chosen = None  # AskUserQuestion으로 처리
print(chosen or '')
EOF
  )

  if [ -z "$ASSET_PATH" ]; then
    if [ "$AUTO" = "1" ]; then
      # 무인 모드: 자산 단정 불가능한 knob은 조용히 skip (전체 abort 아님).
      # --dry-run --auto 라면 PLAN에 UNRESOLVED 표시.
      if [ "$DRY_RUN" = "1" ]; then
        local KID=$(echo "$KNOB_JSON" | jq -r .id)
        local FLD=$(echo "$KNOB_JSON" | jq -r .field_path)
        echo "PLAN:${KID}|UNRESOLVED|${FLD}||"
      fi
      return 1
    fi
    echo "자산을 단정할 수 없음 — 후보 여러 개. AskUserQuestion 으로 선택 받기 (또는 skip)."
    return 1
  fi

  echo "Target asset: $ASSET_PATH"

  # action 분기
  local ACTION=$(echo "$KNOB_JSON" | python3 -c "import json,sys;print(json.load(sys.stdin).get('action','default'))")
  if [ "$ACTION" = "open_in_unity" ]; then
    if [ "$AUTO" = "1" ]; then
      # 무인 모드: 수동 편집 knob skip.
      return 0
    fi
    echo "수동 편집 필요 — $ASSET_PATH 를 Unity Editor에서 열어 ${KNOB_JSON.field_path} 을 조정해 주세요."
    return 0
  fi

  # 자동 편집
  local FIELD=$(echo "$KNOB_JSON" | jq -r .field_path)
  local ADJUST=$(echo "$KNOB_JSON" | jq -r .adjust_default)
  local KID=$(echo "$KNOB_JSON" | jq -r .id)

  # --dry-run --auto: 실편집 안 함. 현재 값 읽어서 PLAN: 한 줄 emit하고 return.
  if [ "$AUTO" = "1" ] && [ "$DRY_RUN" = "1" ]; then
    local FROM TO
    read FROM TO <<< "$(probe_field_value "$ASSET_PATH" "$FIELD" "$ADJUST")"
    echo "PLAN:${KID}|${ASSET_PATH}|${FIELD}|${FROM}|${TO}"
    return 0
  fi

  # field_path 두 패턴 처리
  if [[ "$FIELD" == *"["* ]]; then
    # 배열 named entry: "ValueDefinitions[ValueName=Damage].DefaultValue"
    apply_array_field "$ASSET_PATH" "$FIELD" "$ADJUST"
  else
    apply_scalar_field "$ASSET_PATH" "$FIELD" "$ADJUST"
  fi
  # 적용 성공 시 MODIFIED_PATHS 누적 (Keep/Rollback 단계에서 사용)
  MODIFIED_PATHS+=("$ASSET_PATH")
}

apply_scalar_field() {
  local ASSET_PATH="$1" FIELD="$2" ADJUST="$3"
  # IMPORTANT: pass C# via stdin (heredoc), NOT as a quoted argument.
  # Multi-line scripts passed as a "..." arg to `unity-cli exec` hang the
  # process indefinitely; stdin is the supported form (see `unity-cli --help`).
  # Bash variables expand inside the unquoted heredoc.
  unity-cli exec --usings UnityEditor,UnityEngine <<CSHARP
float adjust = ${ADJUST}f;
var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("$ASSET_PATH");
if (asset == null) return "NOT_FOUND";
var so = new SerializedObject(asset);
var p = so.FindProperty("$FIELD");
if (p == null) return "NO_FIELD";
string result;
if (p.propertyType == SerializedPropertyType.Float) {
  float old = p.floatValue;
  p.floatValue = old * (1f + adjust);
  result = old.ToString("F3") + "->" + p.floatValue.ToString("F3");
} else if (p.propertyType == SerializedPropertyType.Integer) {
  int old = p.intValue;
  p.intValue = Mathf.Max(0, Mathf.RoundToInt(old * (1f + adjust)));
  result = old + "->" + p.intValue;
} else {
  return "UNSUPPORTED_TYPE:" + p.propertyType;
}
so.ApplyModifiedProperties();
EditorUtility.SetDirty(asset);
AssetDatabase.SaveAssets();
return result;
CSHARP
}

apply_array_field() {
  local ASSET_PATH="$1" FIELD="$2" ADJUST="$3"
  # FIELD = "ValueDefinitions[ValueName=Attack].DefaultValue"
  local ARR=$(echo "$FIELD" | sed -E 's/\[.*//')
  local QUERY_KEY=$(echo "$FIELD" | sed -E 's/.*\[([A-Za-z_]+)=.*/\1/')
  local QUERY_VAL=$(echo "$FIELD" | sed -E 's/.*=([^]]+)].*/\1/')
  local SUB=$(echo "$FIELD" | sed -E 's/.*\.//')
  # IMPORTANT: same heredoc-via-stdin rule as apply_scalar_field above.
  unity-cli exec --usings UnityEditor,UnityEngine <<CSHARP
float adjust = ${ADJUST}f;
var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("$ASSET_PATH");
if (asset == null) return "NOT_FOUND";
var so = new SerializedObject(asset);
var arr = so.FindProperty("$ARR");
if (arr == null || !arr.isArray) return "NO_ARRAY";
for (int i = 0; i < arr.arraySize; i++) {
  var e = arr.GetArrayElementAtIndex(i);
  var name = e.FindPropertyRelative("$QUERY_KEY");
  if (name != null && name.stringValue == "$QUERY_VAL") {
    var v = e.FindPropertyRelative("$SUB");
    if (v == null) return "NO_SUB";
    string result;
    if (v.propertyType == SerializedPropertyType.Float) {
      float old = v.floatValue;
      v.floatValue = old * (1f + adjust);
      result = old.ToString("F3") + "->" + v.floatValue.ToString("F3");
    } else if (v.propertyType == SerializedPropertyType.Integer) {
      int old = v.intValue;
      v.intValue = Mathf.Max(0, Mathf.RoundToInt(old * (1f + adjust)));
      result = old + "->" + v.intValue;
    } else {
      return "UNSUPPORTED_TYPE:" + v.propertyType;
    }
    so.ApplyModifiedProperties();
    EditorUtility.SetDirty(asset);
    AssetDatabase.SaveAssets();
    return result;
  }
}
return "NO_MATCH";
CSHARP
}
```

returned 값을 사용자에게 표시 (`old->new`). `NOT_FOUND`, `NO_FIELD`, `NO_SUB`, `NO_MATCH`, `NO_ARRAY`, `UNSUPPORTED_TYPE:<type>` 등의 sentinel은 사용자에게 알리고 skip. `UNSUPPORTED_TYPE` 가 자주 발생하면 해당 knob의 자산을 직접 열어 필드 타입을 확인하고 — 필요하면 핸들러를 위 switch에 추가.

### probe_field_value (read-only, `--dry-run --auto` 용)

`apply_*_field` 의 read-only 버전. SaveAssets 안 함, propertyType에 맞춰 현재 값 + 예상 새 값을 `<from> <to>` 한 줄로 stdout 출력. field_path 패턴 ([] 유무)으로 array/scalar 분기는 동일.

```bash
probe_field_value() {
  local ASSET_PATH="$1" FIELD="$2" ADJUST="$3"
  if [[ "$FIELD" == *"["* ]]; then
    local ARR=$(echo "$FIELD" | sed -E 's/\[.*//')
    local QUERY_KEY=$(echo "$FIELD" | sed -E 's/.*\[([A-Za-z_]+)=.*/\1/')
    local QUERY_VAL=$(echo "$FIELD" | sed -E 's/.*=([^]]+)].*/\1/')
    local SUB=$(echo "$FIELD" | sed -E 's/.*\.//')
    unity-cli exec --usings UnityEditor,UnityEngine <<CSHARP
float adjust = ${ADJUST}f;
var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("$ASSET_PATH");
if (asset == null) return "NOT_FOUND NOT_FOUND";
var so = new SerializedObject(asset);
var arr = so.FindProperty("$ARR");
if (arr == null || !arr.isArray) return "NO_ARRAY NO_ARRAY";
for (int i = 0; i < arr.arraySize; i++) {
  var e = arr.GetArrayElementAtIndex(i);
  var name = e.FindPropertyRelative("$QUERY_KEY");
  if (name != null && name.stringValue == "$QUERY_VAL") {
    var v = e.FindPropertyRelative("$SUB");
    if (v == null) return "NO_SUB NO_SUB";
    if (v.propertyType == SerializedPropertyType.Float) {
      float f = v.floatValue;
      return f.ToString("F3") + " " + (f * (1f + adjust)).ToString("F3");
    } else if (v.propertyType == SerializedPropertyType.Integer) {
      int x = v.intValue;
      return x + " " + Mathf.Max(0, Mathf.RoundToInt(x * (1f + adjust)));
    } else { return "UNSUPPORTED UNSUPPORTED"; }
  }
}
return "NO_MATCH NO_MATCH";
CSHARP
  else
    unity-cli exec --usings UnityEditor,UnityEngine <<CSHARP
float adjust = ${ADJUST}f;
var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("$ASSET_PATH");
if (asset == null) return "NOT_FOUND NOT_FOUND";
var so = new SerializedObject(asset);
var p = so.FindProperty("$FIELD");
if (p == null) return "NO_FIELD NO_FIELD";
if (p.propertyType == SerializedPropertyType.Float) {
  float f = p.floatValue;
  return f.ToString("F3") + " " + (f * (1f + adjust)).ToString("F3");
} else if (p.propertyType == SerializedPropertyType.Integer) {
  int x = p.intValue;
  return x + " " + Mathf.Max(0, Mathf.RoundToInt(x * (1f + adjust)));
} else { return "UNSUPPORTED UNSUPPORTED"; }
CSHARP
  fi
}
```

caller는 `read FROM TO <<< "$(probe_field_value ...)"` 형태로 두 토큰을 받는다. sentinel(`NOT_FOUND` 등)이면 PLAN: line에 그대로 노출 (사용자가 진단 가능).

### Derived target for spawn_rate (설계 / 미구현)

`enemy_damage` knob은 PlayTrace 의 `episode.last_hit_attacker` 가 직접 자산명을 알려준다 (`"Gear"` → `Gear.asset`). `spawn_rate` 는 그런 직접 키가 없어 `structural_duration_cutoff` finding이 트리거되어도 31개 SpawnSet 중 무엇을 줄여야 할지 모른다 — 현재 skip 된다.

해결책 (구현 대기 중): `event.enemy_spawn.set_key` (text) 와 `event.enemy_spawn.actual_rate` (number) 를 활용해 **사망 직전 K초에서 가장 자주 활성이었던 SpawnSet** 을 derived target 으로 뽑는다.

```python
# 의사 코드 — 향후 'derived' asset_match 타입 구현 시 참고
K = 5  # seconds before death
derived_targets = []
for play_no, play_logs in by_play.items():
    death_ts = max(l['timestamp'] for l in play_logs if l['key'] == 'episode.cause')
    window = [l for l in play_logs
              if l['key'] == 'event.enemy_spawn.set_key'
              and death_ts - K*1000 <= l['timestamp'] <= death_ts]
    if window:
        top = collections.Counter(l['value_text'] for l in window).most_common(1)[0][0]
        derived_targets.append(top)
# 모든 play 에서 일관되게 동일 set_key가 나오면 신뢰도 높음 → target
chosen = collections.Counter(derived_targets).most_common(1)[0][0] if derived_targets else None
```

Config 측 schema 변경 (제안):

```json
"asset_match": {
  "type": "value_config_named",
  "name_mode": "filename",
  "name_match_log_key": "event.enemy_spawn.set_key",
  "name_match_window": {"anchor": "episode.cause", "before_sec": 5, "agg": "mode"}
}
```

구현 작업: ① `name_match_window` 인식 → ② 진단 페이즈에서 derived_target 계산 → ③ `edit_one_knob` 의 `TARGET_VALUE` 로 주입.

스킬 한 줄 평: spawn 시계열 데이터는 이미 PlayTrace 에 들어와 있고, 부족한 건 위 추출 로직과 schema 합의뿐.

---

## Git diff 프리뷰

```bash
echo "변경사항:"
git diff --stat -- Assets/
echo ""
git diff -- Assets/ | head -50
```

상세 변경을 사용자가 보고 싶다면 추가 명령 안내 (`git diff Assets/...`).

---

## 확정 / 롤백

```bash
# --auto 모드: 자동 Keep. 수정된 각 자산을 stdout에 한 줄씩 emit하고 종료.
# bal-converge 같은 caller가 grep으로 누적 추적.
if [ "$AUTO" = "1" ]; then
  for p in "${MODIFIED_PATHS[@]}"; do
    echo "MODIFIED:$p"
  done
  exit 0
fi

# AskUserQuestion (대화형 모드만)
# 옵션:
#   1) Keep — 변경 유지 (사용자가 직접 commit)
#   2) Rollback — 변경 사항 모두 되돌림
#   3) Selective rollback — 일부만 되돌리기 (advanced)

if [ "$ANSWER" = "Rollback" ]; then
  # skill이 만진 파일 목록을 정확히
  git checkout -- "${MODIFIED_PATHS[@]}"
  # Unity가 메모리에 수정본을 들고 있을 수 있으니 AssetDatabase 강제 재동기화
  unity-cli editor refresh >/dev/null 2>&1 || true
  echo "롤백 완료."
elif [ "$ANSWER" = "Selective" ]; then
  # 파일별로 keep/revert 묻기
  for p in "${MODIFIED_PATHS[@]}"; do
    # AskUserQuestion: "Keep $p / Revert $p"
    ...
  done
  unity-cli editor refresh >/dev/null 2>&1 || true
else
  echo "변경 유지. 검토 후 사용자가 commit/push 해 주세요."
fi
```

---

## 종결 안내

```
다음 단계:
  /bal-run <N>회 으로 재테스트 → 결과가 개선됐는지 확인
  개선되지 않으면 git checkout 으로 되돌리고 다른 knob 시도

저장: 검토 후 git commit 권장 — 자동 commit은 의도적으로 하지 않습니다.
```

cmux 상태 갱신:

```bash
[ "$HAS_CMUX" = "1" ] && cmux set-status bal-apply "Applied (N knobs)" --icon check
```

---

## Cold start (config 없을 때)

```bash
handle_cold_start() {
  # --auto 모드: 무인 환경에서 cold start는 의미 없음. 즉시 abort.
  if [ "$AUTO" = "1" ]; then
    echo "[bal-apply] no config — converge cannot proceed" >&2
    exit 2
  fi

  echo "[bal-apply] config(.claude/bal-apply.json) 가 없습니다."
  # AskUserQuestion: "템플릿 생성 / 중단"
  if [ "$ANSWER" = "Generate template" ]; then
    mkdir -p .claude
    cat > .claude/bal-apply.json <<'EOF'
{
  "schema_version": 1,
  "diagnostics": {
    "killer_concentration_threshold": 0.6,
    "duration_stddev_ratio_max": 0.10,
    "level_cap_floor": 5,
    "min_dps": 0.25,
    "burst_damage_multiplier": 3.0,
    "damage_variance_max": 0.5,
    "min_runs_for_analysis": 3
  },
  "knobs": [
    // TODO: 다음 형태로 knob을 채워 주세요. 예시는 .claude/skills/bal-apply/SKILL.md 의 "Config 예시" 참고.
    // {
    //   "id": "enemy_damage",
    //   "triggers": ["killer_concentration"],
    //   "asset_glob": "Assets/.../Enemies/**/*.asset",
    //   "asset_match": {"type": "value_config_named", "name_mode": "filename",
    //                    "name_match_log_key": "episode.last_hit_attacker"},
    //   "field_path": "ValueDefinitions[ValueName=Damage].DefaultValue",
    //   "adjust_default": -0.15
    // }
  ]
}
EOF
    echo "템플릿 생성 완료. knobs 배열을 채운 뒤 다시 실행해 주세요."
  fi
  exit
}
```

위 JSON 안에 주석을 넣었지만 JSON 표준이 주석 미지원 — 실제 생성 시 주석은 제거하고 별도 README나 표준 출력 안내로 대체. 또는 `jsonc` (JSON with Comments) 변종을 허용하려면 config 로드 시 `python3 -m json.tool` 대신 줄 단위 `//` 스트립 처리 추가.

MVP는 표준 JSON 유지, 주석 대신 빈 `knobs: []` 와 안내 메시지로.

---

## Config 예시 (이 프로젝트, 참고용)

```json
{
  "knobs": [
    {
      "id": "enemy_damage",
      "triggers": ["killer_concentration", "burst_damage_at_death"],
      "asset_glob": "Assets/RGame/RoguelikeKit/ScriptableObjects/DataSO/Enemy/**/*.asset",
      "asset_match": {
        "type": "value_config_named",
        "name_mode": "filename",
        "name_match_log_key": "episode.last_hit_attacker"
      },
      "field_path": "ValueDefinitions[ValueName=Damage].DefaultValue",
      "adjust_default": -0.15
    },
    {
      "id": "spawn_rate",
      "triggers": ["spawn_cliff", "structural_duration_cutoff"],
      "asset_glob": "Assets/RGame/RoguelikeKit/ScriptableObjects/DataSO/Level/Stages/**/*.asset",
      "field_path": "BaseRatePerSecond",
      "adjust_default": -0.20
    }
  ]
}
```

---

## 자주 빠지는 함정

- **Editor가 play 중에 적용**: SaveAssets 시 자산 상태가 꼬일 수 있음 → 사전 점검에서 abort.
- **Asset glob이 너무 넓어 여러 후보 매칭**: `name_mode: filename` 으로 좁히거나 사용자에게 후보 중 선택 시킴. 단정 못하면 skip하고 사용자에게 알림.
- **`field_path`가 nested 배열**: 2단 이상 nested (예: `Stages[].Sets[].BaseRate`)는 MVP 미지원. 단순 단일 필드 또는 1단 named-array만.
- **AnimationCurve 자동 편집 시도**: 거의 항상 깨짐. `action: open_in_unity` 으로 가이드만.
- **여러 finding이 같은 자산 동시 수정**: 같은 자산에 여러 knob이 매칭되면 순차 적용 (한 knob씩 SaveAssets). 마지막 변경만 반영되지 않게 모든 변경 후 단일 SaveAssets로 묶지 않음 — 각 변경마다 SaveAssets해야 SerializedObject 캐시가 갱신됨.
- **`adjust_range` 무시**: knob에 `adjust_range`가 있는데 사용자가 +200% 같은 값을 요청하면 클램프 + 경고. 본 스킬은 `adjust_default` 만 쓰지만 향후 사용자 입력 받을 때를 위해 검증 로직 포함 권장.
- **bal-run 매핑 무시**: bal-run.json이 있으면 `play_end_key` 등을 거기서 가져와야 컨벤션 일관. 디폴트 fallback과 다르면 진단이 빈 결과 낳음.
- **git에 자산 미커밋**: rollback 정확성을 위해 적용 전 영향 자산이 clean이어야 함. dirty면 경고 후 사용자 결정.
- **`unity-cli exec`에 multi-line 인자 전달**: `unity-cli exec "<여러 줄 C#>"` 형태로 인자에 박으면 무한 hang. 반드시 stdin/heredoc (`unity-cli exec --usings ... <<CSHARP ... CSHARP`) 으로 전달할 것. `unity-cli --help`의 `echo '<code>' | exec` 패턴도 가능.
- **SerializedProperty의 타입을 가정하지 말 것**: 이 프로젝트의 `DefaultValue`처럼 YAML로 정수처럼 보여도 실제 `propertyType`이 `Integer`일 수 있다 (그러면 `floatValue` 는 0). 위 핸들러처럼 `propertyType` 으로 분기하고, 미지원 타입은 `UNSUPPORTED_TYPE` sentinel 로 명시 abort.
- **롤백 후 Unity 메모리 잔여**: `git checkout`만 하면 디스크는 원복되지만 Unity Editor가 메모리에 수정본을 들고 있어 다음 SaveAssets 호출 시 다시 덮어쓸 수 있다. 롤백 직후 `unity-cli editor refresh` 로 AssetDatabase 강제 재동기화 권장.
- **`--auto` 모드에서 사용자 보호**: `--auto`는 dirty asset, 자산 후보 다수, cold start 등 모호한 상황을 conservatively abort/skip 한다 (위 "--auto 모드 분기 표" 참조). 무인 환경에서 잘못된 추측으로 자산을 망가뜨리지 않기 위함. caller(bal-converge 등)는 `exit code` 와 `MODIFIED:` / `PLAN:` 라인을 표준 인터페이스로 사용.

## 참조 자료

- **`PlayTrace/docs/MANUAL.md`** — PlayTrace API/필드/예제의 **단일 진실 소스**. 이 스킬이 PlayTrace에서 logs를 fetch할 때 (`/api/logs/search`) 응답 형식·`value_type` 분기 등은 여기서 1차 확인. (경로: `/Users/mingyukim/Documents/GitHub/018_Study-Koomin/PlayTrace/docs/MANUAL.md`)
- `.claude/skills/bal-run/SKILL.md` — config 캐시 패턴, AskUserQuestion 흐름, unity-cli 사용법
- `.claude/bal-run.json` — `play_end_key`, `summary_columns` (있으면 활용)
- `.claude/bal-apply.json` — 이 스킬의 knob 매핑 (없으면 cold start)
- `Assets/RGame/RoguelikeKit/Scripts/System/Stage/SpawnSet.cs` — SpawnSet 필드 구조 (`BaseRatePerSecond`, `IntensityCurve`)
- `Assets/RGame/RSOFramework/CommonStat/Scripts/ValueConfigSO.cs` — `ValueDefinitions` 배열 구조 (이 프로젝트의 enemy/player 스탯 SO)
