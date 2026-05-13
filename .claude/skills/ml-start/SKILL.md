---
name: ml-start
description: ML-Agents 강화학습을 실제로 실행하고 모니터링. mlagents-learn Python 트레이너 + Unity Editor 플레이를 동시 시작하고, cmux 멀티패널로 학습 로그/TensorBoard를 표시하며 주기적으로 진행 상황을 보고. 사용자가 "/ml-start", "학습 시작", "강화학습 실행", "training 시작" 등을 언급하면 사용. 선행 조건: /ml-init + /ml-setting 완료.
disable-model-invocation: false
---

# ml-start: ML-Agents 학습 실행 및 모니터링

이 스킬은 학습을 실행하고 사용자 대신 모니터링한다. 학습은 보통 수십 분~수 시간 걸리므로 주기적 확인과 cmux 사이드바 표시가 핵심.

## ⚠️ 절대 규칙

**모든 bash 명령은 절대 경로 사용**. 학습 중 cwd가 잘못되면 결과 파일 위치가 꼬이고 모니터링 실패함.

```bash
PROJECT_ROOT="$(pwd)"
ML_TRAINING="$PROJECT_ROOT/ml-training"
```

cd가 필요하면 서브셸로 격리:
```bash
(cd "$ML_TRAINING" && source .venv/bin/activate && mlagents-learn ...)
```

## 전체 흐름

1. 사전 검증
2. 학습 설정 선택 (configs 폴더에 여러 YAML 있을 때)
3. (선택) ml-test-validator 서브에이전트로 smoke test
4. cmux 레이아웃 구성
5. mlagents-learn 시작 → "Listening on port" 대기
6. Unity Editor 플레이 시작
7. TensorBoard 시작
8. 모니터링 루프 (Monitor 또는 /loop)
9. 완료 처리

## 선행 조건: 커스텀 서브에이전트 로드 여부

이 스킬은 `ml-test-validator` 서브에이전트(선택적 smoke test)를 호출함. 세션 재시작 후에만 로드되므로 "Agent type not found" 시 `general-purpose` 폴백 사용.

## 1단계: 사전 검증

```bash
# Python 환경
test -d ml-training/.venv && echo "venv OK" || echo "/ml-init 필요"

# YAML 설정 존재 확인
ls ml-training/configs/*.yaml 2>/dev/null
```

### Unity Editor 명시 차단 (필수)

`/ml-start`는 Unity Editor가 반드시 실행 중이어야 하며, 미실행 상태로 진행하면 mlagents-learn이 무한 hang함. 단순 안내 ❌, 명시적 게이트 ✅:

```bash
unity-cli status 2>&1
EDITOR_OK=$?
```

> ℹ️ `unity-cli status` (top-level) — Editor 실행 여부 + 프로젝트 경로 + 상태(ready/compiling) 반환. `unity-cli editor`는 play/stop/pause/refresh 서브커맨드만 있음 (status 없음).

**Editor 미실행이면 AskUserQuestion으로 차단**:

| 옵션 | 동작 |
|------|------|
| Editor 실행 후 다시 시도 (Recommended) | 사용자가 Unity Editor를 열고 학습 씬 로드 후 알림. `/ml-start` 처음부터 재실행 |
| 강제 진행 | 위험 인지 후 진행 (mlagents-learn은 시작되나 Unity 연결 대기 — 60초 timeout 적용됨) |
| 취소 | `/ml-start` 종료, cmux 상태 정리 |

차단 흐름 코드:
```bash
if [ $EDITOR_OK -ne 0 ]; then
  # AskUserQuestion 호출 후 결과에 따라 분기
  cmux set-status ml-start "Editor 미실행 — 사용자 응답 대기" --icon alert
fi
```

### 기타 검증

```bash
cmux set-status ml-start "사전 검증 중..." --icon hourglass
cmux set-progress 0.0
```

검증 실패(YAML 없음/venv 없음) 시 사용자에게 알리고 중단.

## 2단계: 학습 설정 선택

YAML 파일이 여러 개면 AskUserQuestion으로 선택:
```bash
ls ml-training/configs/*.yaml | xargs -n1 basename
```

선택된 YAML에서 핵심 정보 추출:
```bash
# behavior name
grep -A1 "^behaviors:" ml-training/configs/<name>.yaml | tail -1 | tr -d ': '

# max_steps
grep "max_steps:" ml-training/configs/<name>.yaml | head -1
```

run-id 결정: `<name>_<timestamp>` 형식 추천 (사용자에게 확인).

이미 같은 run-id 결과가 있으면 `--resume` 또는 `--force` 선택지 제시:
```bash
ls ml-training/results/ 2>/dev/null
```

## 3단계: (선택) Smoke Test

처음 실행하는 시나리오라면 사용자에게 smoke test 권유:
- "본격 학습 전에 1000~5000 step의 짧은 검증을 진행할까요? (5분 이내)"
- 동의 시 **ml-test-validator 서브에이전트 호출**:
  - prompt: YAML 경로, behavior name, 환경 정보
- 결과가 ❌면 사용자에게 수정 권고 후 중단
- ⚠️이면 사용자 결정에 맡김
- ✅면 본격 학습 진행

## 4단계: cmux 레이아웃 구성

cmux 환경이 아니면 단순히 백그라운드 실행 + Bash 모니터링으로 폴백.

> **자동 재사용 규칙**: 학습 패널과 TensorBoard 패널은 각각 고정 라벨(`ml-train-log`, `ml-tensorboard`)로 식별한다. 이미 존재하면 새로 만들지 않고 기존 패널을 재사용한다. 패널 이름을 수동으로 바꾸면 식별이 깨지므로 그대로 유지할 것.

cmux 환경이면:
```bash
# 고정 라벨
TRAIN_LABEL="ml-train-log"
TB_LABEL="ml-tensorboard"

# 현재 워크스페이스 ID 확인
WS=$(cmux identify --json | jq -r '.caller.workspace_ref')

# 기존 surface 목록에서 라벨로 검색
SURFACE_LIST=$(cmux list-pane-surfaces --workspace "$WS")

find_surface() {
  echo "$SURFACE_LIST" | grep -F " $1 " | head -1 | grep -oE "surface:[0-9]+"
}

TRAIN_SURFACE=$(find_surface "$TRAIN_LABEL")
TB_SURFACE=$(find_surface "$TB_LABEL")

# --- 학습 패널 ---
if [ -z "$TRAIN_SURFACE" ]; then
  TRAIN_SURFACE=$(cmux new-split right --workspace "$WS" --json 2>&1 | grep -oE "surface:[0-9]+" | head -1)
  cmux rename-tab --surface "$TRAIN_SURFACE" "$TRAIN_LABEL"
  cmux log --level info "ml-train-log 패널 신규 생성: $TRAIN_SURFACE"
else
  # 기존 패널 재사용 — 진행 중인 명령 정리
  cmux send-key --surface "$TRAIN_SURFACE" C-c
  sleep 1
  cmux send --surface "$TRAIN_SURFACE" "clear\n"
  cmux log --level info "ml-train-log 기존 패널 재사용: $TRAIN_SURFACE"
fi

# --- TensorBoard 패널 ---
if [ -z "$TB_SURFACE" ]; then
  TB_SURFACE=$(cmux new-split down --surface "$TRAIN_SURFACE" --json 2>&1 | grep -oE "surface:[0-9]+" | head -1)
  cmux rename-tab --surface "$TB_SURFACE" "$TB_LABEL"
  cmux log --level info "ml-tensorboard 패널 신규 생성: $TB_SURFACE"
else
  cmux send-key --surface "$TB_SURFACE" C-c
  sleep 1
  cmux send --surface "$TB_SURFACE" "clear\n"
  cmux log --level info "ml-tensorboard 기존 패널 재사용: $TB_SURFACE"
fi
```

레이아웃:
```
+-----------------------+------------------+
|                       |  Training Logs   |
|   Claude (메인)       |  (ml-train-log)  |
|                       +------------------+
|                       |  TensorBoard     |
|                       | (ml-tensorboard) |
+-----------------------+------------------+
```

## 5단계: mlagents-learn 시작

학습 패널에 명령 전송 (`cmux send` + `--surface`):
```bash
cmux send --surface $TRAIN_SURFACE \
  "cd /절대경로/ml-training && source .venv/bin/activate && mlagents-learn configs/<name>.yaml --run-id=<run-id> --time-scale 20\n"
```

> ⚠️ cmux CLI에는 `send-surface`/`send-key-surface`라는 명령이 **없음**. 실제 형식은 `send --surface <id> <text>` 와 `send-key --surface <id> <key>`.

⚠️ `--force` vs `--resume` 옵션:
- 신규 시작: `--force` 또는 옵션 없이
- 이어서 학습: `--resume` (기존 results 폴더 필요)

사용자가 선택한 옵션 추가.

### "Listening on port" 대기 (60초 timeout 강제)

Python이 Unity 연결을 기다리는 상태가 될 때까지 폴링. **반드시 timeout 적용** — 무한 hang 방지:

```bash
READY=0
for i in $(seq 1 12); do
  if cmux read-screen --surface $TRAIN_SURFACE --lines 50 2>&1 | grep -q "Listening on port"; then
    READY=1
    break
  fi
  sleep 5
done

if [ $READY -eq 0 ]; then
  # 60초 timeout — Python이 Listening 진입 못함
  cmux notify --title "ml-start: Python 트레이너 응답 없음" \
    --body "60초 내에 'Listening on port' 메시지 없음. mlagents-learn 출력 확인 필요."
  cmux set-status ml-start "Python 응답 없음" --icon alert
  # 사용자에게 패널 직접 확인 요청 + 진행 여부 질문
fi
```

준비 안 되면 일반적 원인 (사용자에게 안내):
- 포트 충돌 (5004) → `--base-port 5005`로 재시도 안내
- mlagents 환경 문제 → 패널의 에러 로그 확인
- Python 버전 불일치 → `/ml-init` 재실행 필요

## 6단계: Unity Editor 플레이 시작 (retry + cleanup 포함)

```bash
unity-cli editor play 2>&1
PLAY_STATUS=$?
```

### 실패 시 1회 retry → 그래도 실패면 cleanup

```bash
if [ $PLAY_STATUS -ne 0 ]; then
  cmux log --level warning "Unity Editor play 실패 — 5초 후 재시도"
  sleep 5
  unity-cli editor play 2>&1
  PLAY_STATUS=$?
fi

if [ $PLAY_STATUS -ne 0 ]; then
  # 정리: Python 트레이너 SIGTERM 후 종료
  cmux log --level error "Unity 연결 실패 — Python 트레이너 종료 중"
  cmux send-key --surface $TRAIN_SURFACE C-c
  sleep 2
  cmux notify --title "ml-start 중단" --body "Unity Editor 플레이 시작 실패. Editor 상태를 확인해주세요."
  cmux set-status ml-start "중단" --icon alert
  # 사용자에게 보고 후 종료
  exit 1
fi
```

성공 확인:
```bash
unity-cli status 2>&1   # "ready" 또는 "playing" 표시
```

학습 시작 신호 대기 — **30초 timeout + mlagents 자체 에러 동시 감지**:

⚠️ **중요한 사실** (실측): mlagents-learn은 **약 15초 내**에 Unity 응답 안 오면 `UnityTimeOutException` 던지며 자살함. 우리 timeout은 그것보다 짧거나 같아야 의미 있음.

```bash
TRAINING_STARTED=0
ERROR_DETECTED=""
for i in $(seq 1 6); do  # 5초 × 6 = 30초
  SCREEN=$(cmux read-screen --surface $TRAIN_SURFACE --lines 30 2>&1)

  # 성공: Step 출력 감지
  if echo "$SCREEN" | grep -qE "Step:\s*[0-9]+"; then
    TRAINING_STARTED=1
    break
  fi

  # 실패: mlagents 자체 에러 감지 (UnityTimeOutException 등)
  if echo "$SCREEN" | grep -qE "UnityTimeOutException|Traceback|UnityWorkerInUseException"; then
    ERROR_DETECTED=$(echo "$SCREEN" | grep -E "Exception|Error" | head -3)
    break
  fi

  sleep 5
done

if [ -n "$ERROR_DETECTED" ]; then
  cmux notify --title "ml-start: mlagents 에러" --body "$ERROR_DETECTED"
  cmux log --level error "$ERROR_DETECTED"
  # mlagents가 이미 죽었으므로 cleanup 불필요 (run-id 폴더는 사용자 정리 권유)
elif [ $TRAINING_STARTED -eq 0 ]; then
  cmux notify --title "ml-start: 학습 시작 안 됨" \
    --body "30초 내 Step 없음. Agent BehaviorType=Default 확인 + BehaviorName이 YAML과 일치하는지 확인."
  cmux log --level warning "Step 미감지 — Agent 설정 확인 권장"
fi
```

**일반적 실패 원인** (사용자에게 안내):
- `UnityTimeOutException` → Unity 씬에 Agent 컴포넌트 없음, BehaviorType ≠ Default, 또는 BehaviorName mismatch
- `UnityWorkerInUseException` → 이전 학습 트레이너가 아직 떠 있음 (다른 cmux 패널 확인)
- "behavior name does not match" → YAML behavior와 Agent BehaviorParameters 이름 불일치

## 7단계: TensorBoard 시작

```bash
cmux send --surface $TB_SURFACE \
  "cd /절대경로/ml-training && source .venv/bin/activate && tensorboard --logdir results --port 6006\n"
```

사용자에게 안내:
```
TensorBoard: http://localhost:6006
```

## 8단계: 모니터링 루프

`/loop` 스킬 호출 (사용자가 cmux 메인 패널에서 사용 가능) **또는** Monitor 도구로 학습 패널 watching.

### 추천: Monitor 도구 사용

```bash
# mlagents 출력에서 Step과 Mean Reward 추출하는 모니터
```

Monitor 호출:
- description: "ML-Agents 학습 진행 모니터"
- persistent: true
- command:
  ```bash
  while true; do
    OUTPUT=$(cmux read-screen --surface $TRAIN_SURFACE --lines 5 2>&1)
    echo "$OUTPUT" | grep -E "Step:\s*[0-9]+|Error|Exception|Mean Reward" || true
    sleep 60
  done
  ```

### 메트릭 파싱 및 cmux 사이드바 갱신

mlagents 출력 형식: `[INFO] <Behavior>. Step: 25000. Time Elapsed: 42.123 s. Mean Reward: 0.234. Std of Reward: 0.012.`

학습 단계마다 (모니터 이벤트 수신 시):
```bash
# Step 추출
STEP=$(echo "$LINE" | grep -oE "Step: [0-9]+" | grep -oE "[0-9]+")
# Mean Reward 추출
REWARD=$(echo "$LINE" | grep -oE "Mean Reward: -?[0-9.]+" | grep -oE "-?[0-9.]+$")
# max_steps 비율 (YAML에서 미리 추출한 값 사용)
PROGRESS=$(echo "scale=3; $STEP / $MAX_STEPS" | bc)

cmux set-progress $PROGRESS --label "$STEP / $MAX_STEPS"
cmux set-status reward "$REWARD" --icon chart
cmux set-status step "$STEP" --icon zap
cmux log --level info "Step $STEP: reward=$REWARD"
```

### 주기

- 60~270초 간격 (캐시 유지를 위해 300초 미만)
- 학습이 길면 점진적으로 간격 늘림

### 중간 보고

10만 step마다 또는 사용자가 요청 시:
```
📊 학습 진행: 250,000 / 5,000,000 step (5%)
- Mean Reward: 0.456
- Std of Reward: 0.123
- Elapsed: 12분
- ETA: 약 4시간

추세: 보상 상승 중 (이전 100k step: 0.234 → 0.456)
```

### 에러 감지

학습 패널에서 에러 발견 시:
- "Traceback", "Error", "Exception", "ObjectDisposedException" 등
- 즉시 사용자에게 알림 + cmux notify
```bash
cmux notify --title "ML 학습 에러" --body "$(echo $LINE | head -c 100)"
cmux set-status ml-start "에러 발생" --icon alert
```

## 9단계: 안전 중단 (사용자가 중단 요청 시 반드시 이 순서 준수)

> ⚠️ **절대 규칙**: Unity를 먼저 멈추거나 `kill -9`를 쓰면 체크포인트가 유실된다.  
> mlagents가 SIGTERM을 받은 뒤 `.pt`/`.onnx`를 디스크에 다 쓸 때까지 기다려야 함.

### 9-1. SIGTERM 전송 (체크포인트 저장 트리거)

cmux 패널에 Ctrl+C를 보내는 것이 가장 안전한 방법:
```bash
cmux send-key --surface $TRAIN_SURFACE C-c
```

또는 프로세스에 직접:
```bash
kill -SIGTERM $(pgrep -f mlagents-learn)
```

### 9-2. 체크포인트 저장 대기 (최대 60초)

```bash
RUN_DIR="$ML_TRAINING/results/$RUN_ID"
SAVED=0
for i in $(seq 1 12); do   # 5초 × 12 = 60초
  # .pt 파일이 생기거나 training_status.json이 유효하면 저장 완료
  if ls "$RUN_DIR/KuloCardAgent/"*.pt 2>/dev/null | head -1 | grep -q ".pt"; then
    SAVED_STEP=$(python3 -c "
import json
d=json.load(open('$RUN_DIR/run_logs/training_status.json'))
fc=d.get('KuloCardAgent',{}).get('final_checkpoint',{})
print(fc.get('steps','?'))
" 2>/dev/null)
    SAVED=1
    break
  fi
  sleep 5
done

if [ $SAVED -eq 1 ]; then
  cmux log --level info "체크포인트 저장 완료: step $SAVED_STEP"
  cmux notify --title "ML 학습 중단" --body "체크포인트 저장 완료 (step $SAVED_STEP). --resume으로 재개 가능."
else
  cmux log --level warning "60초 내 체크포인트 미확인 — Unity 중단 후 results 폴더 수동 확인 권장"
  cmux notify --title "ML 학습 중단" --body "체크포인트 저장 미확인. checkpoint_interval 설정 확인 필요."
fi
```

> ℹ️ `checkpoint_interval`이 현재 step보다 크면 자동 체크포인트가 없으므로 SIGTERM 시 저장되는 final_checkpoint만 존재함.  
> **권장**: `checkpoint_interval: 50000` 이하 설정.

### 9-3. Unity 정지 (체크포인트 확인 후)

```bash
unity-cli editor stop
```

### 9-4. 재개 방법 안내

사용자에게 출력:
```
학습 중단됨 (step $SAVED_STEP 저장)
재개하려면:
  mlagents-learn configs/<name>.yaml --run-id=<run-id> --resume --time-scale 20
```

---

## 10단계: 완료 처리 (max_steps 도달 또는 정상 종료)

학습 종료 감지:
- "Saved Model" 또는 "Exported .onnx" 메시지
- 또는 max_steps 도달

### 10-1. Unity 정지

```bash
unity-cli editor stop
```

### 10-2. 모델 복사

```bash
# 가장 최근 결과 폴더의 .onnx 파일 찾기
LATEST_RUN=$(ls -t ml-training/results/ | head -1)
ONNX_FILE=$(ls ml-training/results/$LATEST_RUN/*.onnx 2>/dev/null | head -1)

if [ -n "$ONNX_FILE" ]; then
  cp "$ONNX_FILE" Assets/ML-Models/
  unity-cli editor refresh
fi
```

### 10-3. Inference 모드 전환 옵션

사용자에게:
- "학습된 모델로 Inference 모드 전환할까요?"
- 동의 시: `unity-cli menu "Tools/ML/Setup Inference Mode"`

### 10-4. 최종 보고

```bash
cmux set-progress 1.0 --label "완료"
cmux set-status ml-start "완료" --icon check
cmux notify --title "🎉 ML-Agents 학습 완료" --body "Final reward: $FINAL_REWARD"
```

채팅 출력:
```
✅ 학습 완료

- Run ID: <name>_<timestamp>
- Total steps: 5,000,000
- Final Mean Reward: 0.812
- 학습 시간: 4시간 23분
- 모델: Assets/ML-Models/<Behavior>.onnx

TensorBoard 결과: ml-training/results/<run-id>/
다음 단계:
- 게임 씬에서 모델 테스트 (Inference Mode 자동 적용 옵션 선택)
- 추가 학습: /ml-start --resume <run-id>
```

## 에러 복구

| 상황 | 대응 |
|------|------|
| Unity가 응답 없음 | unity-cli editor stop → status 확인 → 사용자에게 보고 |
| 학습 크래시 (Python) | results 폴더 보존, `--resume` 옵션 안내 |
| 포트 충돌 | `--base-port 5005` 등 다른 포트 시도 |
| 메모리 부족 | num_envs 줄이기, batch_size 줄이기 권고 |
| Cuda OOM | torch CPU fallback (mlagents는 보통 CPU) |
| TensorBoard 포트 충돌 | `--port 6007` 등 변경 |

## 사용자 인터랙션 시점

스킬은 자동 진행하되, 다음 시점에 사용자 확인:
1. run-id 결정 시 (옵션이 여러 개일 때)
2. smoke test 진행 여부
3. `--force` vs `--resume` 결정
4. 에러 발생 시 (재시도/중단/디버그)
5. 학습 중간 보고를 더 보고싶은지 (옵션)
6. 완료 후 Inference Mode 전환
7. **사용자가 중단 요청 시** → 반드시 9단계(안전 중단) 절차 실행. `kill -9` 또는 Unity 먼저 중단 금지.

## 폴백 (cmux 미사용 환경)

cmux가 없으면:
- 학습 패널 분할 대신 `Bash run_in_background: true`로 mlagents-learn 실행
- TensorBoard도 백그라운드 실행
- 모니터링은 백그라운드 작업의 출력 파일을 주기적으로 Read
- 사이드바 표시 대신 채팅으로 진행 보고
