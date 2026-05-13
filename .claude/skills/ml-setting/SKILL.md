---
name: ml-setting
description: Unity 씬과 플레이어를 분석하여 ML-Agents 강화학습 설정을 생성. Q&A로 학습 목표/알고리즘/관측/행동/보상을 결정한 후 Agent C# 스크립트, 학습 YAML, Editor 자동화 스크립트를 자동 생성. 사용자가 "/ml-setting", "ml 에이전트 설정", "강화학습 시나리오 만들기" 등을 언급하면 사용. 선행 조건: /ml-init 완료.
disable-model-invocation: false
---

# ml-setting: ML-Agents 에이전트 설정

이 스킬은 사용자와 대화하며 학습 시나리오를 설계하고, 그 결과를 Agent C# 스크립트와 YAML 설정 파일로 코드 생성한다.

## ⚠️ 절대 규칙

**모든 bash 명령은 절대 경로 사용**. `cd <subdir>`가 후속 명령의 cwd에 영향을 줘서 검증 실패가 발생함.

```bash
PROJECT_ROOT="$(pwd)"  # 스킬 시작 시 캡쳐
```

이후 모든 파일 경로는 `$PROJECT_ROOT/...` 형식 사용 또는 Read/Write/Edit 도구는 절대경로 강제.

## 선행 조건 확인

### 1. /ml-init 완료 여부

```bash
PROJECT_ROOT="$(pwd)"
test -d "$PROJECT_ROOT/ml-training/.venv" && test -d "$PROJECT_ROOT/Assets/Scripts/ML" && echo "OK" || echo "ml-init 필요"
```

미완료 시 사용자에게 `/ml-init` 먼저 실행 안내.

### 2. 커스텀 서브에이전트 로드 여부

이 스킬은 `unity-expert`, `rl-advisor`, `ml-code-reviewer` 서브에이전트를 호출함. `.claude/agents/` 파일이 있어도 **세션 시작 시점**에만 로드되므로:

- 새 프로젝트에 처음 설치한 경우 → Claude Code 재시작 필요
- Agent 도구 호출 시 "Agent type 'unity-expert' not found" 에러가 나오면 → 폴백으로 `general-purpose` 사용 + SKILL.md의 페르소나 지침을 prompt에 직접 포함

폴백 패턴 예시:
```
Agent({
  description: "...",
  subagent_type: "general-purpose",
  prompt: "당신은 Unity 6 (URP) 게임 개발 전문가입니다. ...<원래 unity-expert.md 내용>... 분석해주세요."
})
```

## 전체 흐름

1. 씬 선택 (사용자 질문)
2. unity-expert 서브에이전트로 씬/플레이어 분석
   - **2-A. 씬 적합성 평가** (빈 씬이면 분기 → 다른 씬 선택 / 환경 구축 / 취소)
3. Q&A로 학습 시나리오 결정 (5단계)
4. rl-advisor 서브에이전트로 알고리즘/하이퍼파라미터 추천
5. 사용자 최종 확인
6. 코드 생성 (Agent.cs + YAML + Editor 스크립트)
7. ml-code-reviewer 서브에이전트로 코드 검증
8. unity-cli로 컴파일 → 에러 확인 (Editor 미실행 시 graceful skip)
9. Inspector 설정 안내

cmux 환경이면 진행 상황 표시:
```bash
cmux set-status ml-setting "씬 분석 중..." --icon hourglass
cmux set-progress 0.1
```

## 1단계: 씬 선택

```bash
ls Assets/Scenes/*.unity 2>/dev/null | xargs -n1 basename
```

사용자에게 AskUserQuestion으로 씬 선택을 받는다. 옵션이 4개 이하면 모든 씬을 옵션으로, 많으면 텍스트 입력.

## 2단계: 씬/플레이어 분석 (unity-expert 호출)

### ⚠️ unity-cli exec 호환성 이슈 (Unity 6 + macOS)

**알려진 이슈**: macOS에서 Unity 6은 `unity-cli exec`가 종종 실패함:
```
Win32Exception: ApplicationName='dotnet', Native error= Cannot find the specified file
```

**원인**: Unity.app이 launchctl로 시작되어 사용자 PATH (`/usr/local/bin` 등) 미상속.

**대응**: `unity-cli exec`에 의존하지 말고 **다음 폴백 우선 사용**:
- 씬 정보 → `.unity` 파일 직접 grep
- 컴포넌트 확인 → 사용자에게 직접 질문 (AskUserQuestion)
- 자동 컴포넌트 추가 → MenuItem Editor 스크립트 생성 후 `unity-cli menu` 호출
- exec를 시도해야 한다면 실패 시 즉시 폴백, 사용자에게 "에러 메시지는 무시 가능"이라고 안내

**해결책 (선택)**: `sudo ln -s /usr/local/bin/dotnet /usr/bin/dotnet` 또는 launchctl 환경변수 설정 — 단, 시스템 변경 필요하므로 사용자 동의 필수.

### Unity Editor 실행 여부 분기 (graceful degradation)

```bash
unity-cli status 2>/dev/null
EDITOR_OK=$?
```

| 상태 | 동작 |
|------|------|
| Editor 실행 중 | `unity-cli scene open "Assets/Scenes/<선택한씬>.unity"`로 씬 로드 + unity-expert 호출 |
| Editor 미실행 | scene open 스킵 → unity-expert가 `.unity` 파일을 직접 grep으로 분석 (파일 기반) |

**중요**: `/ml-setting`은 Editor가 없어도 작동 가능. unity-expert는 `Read`, `Grep`, `Glob` 도구로 `.unity` YAML 파일을 직접 파싱하므로 Editor 의존성 없음. Editor가 있으면 더 정확한 분석(런타임 컴포넌트 상태)이 가능할 뿐.

### 사용자 안내 (Editor 미실행 시)

```markdown
ℹ️ Unity Editor 미실행 — 파일 기반 분석으로 진행합니다.
Editor를 실행하면 더 정확한 분석이 가능하지만 필수는 아닙니다.
씬 파일과 컴포넌트 스크립트를 직접 분석하여 진행하겠습니다.
```

### unity-expert 호출

```
Agent({
  description: "씬과 플레이어 분석",
  subagent_type: "unity-expert",
  prompt: "씬 파일: <절대경로>, 플레이어 GameObject: <이름>, ML-Agents 통합 목적. Editor 실행 여부: <Yes/No>"
})
```

서브에이전트의 보고서를 받아 사용자에게 요약 제시.

### 2-A. 씬 적합성 평가 (필수 분기)

unity-expert 보고서를 바탕으로 씬이 ML-Agents 학습에 적합한지 판단:

| 신호 | 판정 | 분기 |
|------|------|------|
| 플레이어 GameObject 존재 + 환경(바닥/벽) 존재 | ✅ 적합 | 3단계로 진행 |
| 플레이어 없음 (URP 빈 템플릿 등) | ❌ 빈 씬 | 사용자 선택 (아래) |
| 플레이어 있으나 환경 미구축 (NavMesh/콜라이더 없음) | ⚠️ 부분 적합 | 사용자 선택 (아래) |
| 이미 ML-Agents 컴포넌트 존재 | ⚠️ 기존 시나리오 | 덮어쓰기 vs 별도 이름 (V2/V3) 질문 |

**빈 씬/부분 적합 시 AskUserQuestion 옵션**:
- **다른 씬 선택**: 1단계로 돌아가 다른 씬 선택 (가장 권장 — 기존 환경 재사용)
- **기존 학습 씬 패턴 차용**: 프로젝트 내 다른 학습 씬(예: `FPSExplorationTraining`)이 있으면 그 씬으로 전환
- **환경 신규 구축**: 사용자가 직접 바닥/벽/플레이어 프리팹을 추가한 후 재실행 안내 → 작업 일시 중단
- **취소**: /ml-setting 종료

**기존 시나리오 발견 시**:
- 기존 BehaviorName이 무엇인지 사용자에게 알리고
- 새 시나리오 이름(예: `<기존>V2`, `<기존>V3`) 제안 또는 덮어쓰기 확인

⚠️ **중요**: 이 분기 없이 빈 씬에서 Q&A를 진행하면 사용자가 선택한 시나리오를 적용할 대상이 없어 코드 생성 단계에서 막힘. 반드시 이 단계에서 차단.

## 3단계: Q&A 의사결정 (5단계 결정 트리)

각 단계마다 AskUserQuestion 사용. 사용자 답변을 누적하여 최종 시나리오 명세 작성.

### Phase A. 학습 목표
질문 예시:
- "이 캐릭터가 무엇을 학습하길 원하나요?"
  - 옵션: 탐험(맵 커버리지), 목표 도달(네비게이션), 전투/회피, 자원 수집, 기타
- "에피소드 종료 조건은?"
  - 옵션: 시간 초과, 목표 달성, 사망/실패, 영역 이탈

### Phase B. 알고리즘 방향 (Q&A 후 rl-advisor 호출)

수집된 정보로 **rl-advisor 서브에이전트 호출**:
- prompt: 학습 목표, 환경 복잡도(예상 관측 수), 행동 종류(연속/이산), 보상의 sparse/dense 추정

추천 결과를 사용자에게 보여주고 AskUserQuestion으로 알고리즘 최종 선택 (PPO/SAC/POCA).

### Phase C. 관측 공간
질문 예시:
- "에이전트가 어떤 정보를 인지해야 하나요?" (multiSelect)
  - 옵션: 자기 위치/속도, 회전, 목표까지 거리/방향, 주변 장애물(레이캐스트), 카메라 영상, 시간/체력 등 상태
- 레이캐스트 선택 시 추가 질문: "RayPerceptionSensor3D를 사용할까요? (방향 수, 거리)"

이 단계에서 vector_observation_size를 계산.

### Phase D. 행동 공간
- "행동 방식: 연속(continuous)? 이산(discrete)?"
- 캐릭터 컨트롤러에 따라 자동 추천:
  - CharacterController: 보통 continuous (move x/y, rotate)
  - 점프/공격 같은 이벤트: discrete branch 추가
- 액션 인덱스 매핑 결정 (예: action[0]=moveX, action[1]=moveZ, action[2]=lookX)

### Phase E. 보상 설계
- "성공 시 보상: +1 정도?"
- "step 페널티: -0.0005 (긴 에피소드 회피)?"
- "sparse 보상이라면 curiosity reward 추가?"
- 보상 설계가 복잡하면 rl-advisor에 재확인 요청.

## 4단계: 시나리오 명세 정리 및 사용자 최종 확인

수집된 모든 정보를 다음 형식으로 정리하여 사용자에게 보여주고 AskUserQuestion으로 진행 여부 확인.

```markdown
## 생성될 시나리오

- Behavior 이름: <PlayerName>Agent
- 학습 알고리즘: PPO
- 관측 공간: 12 (위치 3 + 속도 3 + 회전 2 + 목표거리 1 + 레이캐스트 3)
- 행동 공간: 3 continuous (moveX, moveZ, lookX)
- 보상:
  - 목표 도달: +1.0
  - step 페널티: -0.0005
  - 영역 이탈: -1.0
  - curiosity strength: 0.05
- max_steps: 5,000,000
- 환경 단위: <환경 root 이름>
- 병렬 환경 수: 8 (4x2 그리드)
```

## 5단계: 코드 생성

### 5-1. Agent C# 스크립트 (Assets/Scripts/ML/<Name>Agent.cs)

기존 `FPSExplorationAgent.cs`를 참고하되, 사용자 시나리오에 맞게 파라미터화하여 생성.

#### 🛡️ 코드 생성 시 반드시 지킬 안전 규칙 (Critical)

**1. 속도 정규화 — Y축은 별도 스케일** (낙하 시 Y velocity가 -10~-30 m/s까지 폭주 가능):
```csharp
// ❌ 위험: 수평 속도 기준으로 Y 정규화 → 낙하 시 |값| > 1
sensor.AddObservation(localVel.y / _moveSpeed);

// ✅ 안전: Y축은 더 큰 스케일로 별도 정규화
sensor.AddObservation(Mathf.Clamp(localVel.x / _moveSpeed, -1f, 1f));
sensor.AddObservation(Mathf.Clamp(localVel.y / 20f, -1f, 1f));  // 별도 스케일
sensor.AddObservation(Mathf.Clamp(localVel.z / _moveSpeed, -1f, 1f));
```

**2. 방향 벡터 정규화 — zero-vector NaN 가드**:
```csharp
// ❌ 위험: 두 점이 같으면 .normalized가 NaN
Vector3 toGoal = (_goal.position - transform.position).normalized;

// ✅ 안전: sqrMagnitude 가드
Vector3 diff = _goal.position - transform.position;
Vector3 toGoal = diff.sqrMagnitude > 1e-6f ? diff.normalized : Vector3.forward;
```

**3. MaxStep 명시** (step penalty 무한 누적 방지):
- Agent 컴포넌트 Inspector의 `Max Step` 필드를 명시적으로 설정 (예: 5000)
- 또는 코드에서 `[SerializeField] private int maxStep = 5000;` + `Initialize()`에서 `MaxStep = maxStep;` 적용
- SetupEditor 스크립트에서 BehaviorParameters와 함께 자동 설정 권장

**4. UnityEngine.Mathf API 함정 — `Tanh`/`Sinh`/`Cosh` 등 없음**:
```csharp
// ❌ 컴파일 에러: UnityEngine.Mathf에는 Tanh 없음
sensor.AddObservation(Mathf.Tanh(dist / 10f));

// ✅ System.Math 사용 + float 캐스팅
sensor.AddObservation((float)System.Math.Tanh(dist / 10f));
```

Mathf에 있는 것: Sin, Cos, Tan, Asin, Acos, Atan, Atan2, Sqrt, Pow, Exp, Log, Log10 등.
**없는 것**: Tanh, Sinh, Cosh, Atanh, Asinh, Acosh → `System.Math` 사용 필요.

#### 핵심 구조 (필수 메서드)

```csharp
public class <Name>Agent : Agent
{
    [Header("References")]
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform environmentRoot;
    // 시나리오에 따라 추가 필드

    [Header("Rewards")]
    [SerializeField] private float <reward1> = 1.0f;
    [SerializeField] private float stepPenalty = -0.0005f;

    [Header("Episode")]
    [SerializeField] private int agentMaxStep = 5000;  // ★ 반드시 명시

    // cached components (씬 분석 결과에 따라)
    private CharacterController _controller;
    // 또는 Rigidbody _rigidbody;

    public override void Initialize() {
        MaxStep = agentMaxStep;  // ★ 무한 누적 방지
        // 컴포넌트 캐싱
    }
    public override void OnEpisodeBegin() { /* 상태 리셋, 스폰 */ }
    public override void CollectObservations(VectorSensor sensor) { /* N개 관측, Y축 별도 스케일, NaN 가드 */ }
    public override void OnActionReceived(ActionBuffers actions) { /* 액션 적용 + 보상 */ }
    public override void Heuristic(in ActionBuffers actionsOut) { /* 키보드/마우스 */ }
}
```

### 컨트롤러 타입별 분기

| 타입 | OnActionReceived 패턴 |
|------|-----------------------|
| CharacterController | `_controller.Move(velocity * Time.deltaTime)` |
| Rigidbody | FixedUpdate-친화: `_rigidbody.AddForce()` 또는 `_rigidbody.velocity = ...` |
| NavMeshAgent | `_navAgent.SetDestination()` (행동을 목표 지점으로) |

### 5-2. 학습 YAML (ml-training/configs/<name>.yaml)

```yaml
default_settings: null
behaviors:
  <BehaviorName>:
    trainer_type: ppo
    hyperparameters:
      batch_size: 256
      buffer_size: 4096
      learning_rate: 3.0e-4
      beta: 5.0e-3
      epsilon: 0.2
      lambd: 0.95
      num_epoch: 3
      learning_rate_schedule: linear
    network_settings:
      normalize: false
      hidden_units: 256
      num_layers: 3
      vis_encode_type: simple
    reward_signals:
      extrinsic:
        gamma: 0.99
        strength: 1.0
      # curiosity 옵션 (sparse 보상 시):
      # curiosity:
      #   strength: 0.05
      #   gamma: 0.99
      #   network_settings:
      #     hidden_units: 256
    keep_checkpoints: 5
    checkpoint_interval: 250000
    max_steps: 5000000
    time_horizon: 64
    summary_freq: 10000
    threaded: false
env_settings:
  num_envs: 1
  base_port: 5004
  env_args: null
  seed: -1
engine_settings:
  width: 84
  height: 84
  quality_level: 0
  time_scale: 20
  target_frame_rate: -1
  capture_frame_rate: 60
  no_graphics: false
checkpoint_settings:
  run_id: <name>_01
  initialize_from: null
  load_model: false
  resume: false
  force: true
  train_model: false
  inference: false
  results_dir: results
```

rl-advisor 추천값을 그대로 반영. 환경 복잡도에 따라 hidden_units / num_layers / batch_size 조정.

### 5-3. Editor 스크립트 (Assets/Editor/ML/Setup<Name>Training.cs)

ℹ️ Editor 스크립트의 어셈블리 위치는 `/ml-init`이 진단한 결과를 따름.
- 메인 게임이 `Assembly-CSharp`인 경우: `ML.Editor.asmdef` 없음 (정상) — Editor 스크립트는 `Assembly-CSharp-Editor`에 들어가 모든 게임 클래스를 자동 참조
- 메인 게임이 커스텀 asmdef인 경우: `ML.Editor.asmdef`가 존재하며 references에 ML과 게임 asmdef 포함

별도 검증 불필요.

기존 `SetupParallelTraining.cs`를 참고하여 일반화:

```csharp
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class Setup<Name>Training
{
    private const int EnvCount = 8;
    private const float Spacing = 50f;
    private const string SceneName = "<TrainingSceneName>";

    [MenuItem("Tools/ML/Setup Parallel Training")]
    public static void SetupParallel()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != SceneName)
        {
            Debug.LogError($"[Setup<Name>Training] {SceneName} 씬을 먼저 열어주세요.");
            return;
        }

        // 4x2 그리드로 환경 복제
        var template = GameObject.Find("TrainingArea_00");
        for (int i = 1; i < EnvCount; i++)
        {
            int row = i / 4;
            int col = i % 4;
            var env = Object.Instantiate(template);
            env.name = $"TrainingArea_{i:D2}";
            env.transform.position = new Vector3(col * Spacing, 0, row * Spacing);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Setup<Name>Training] {EnvCount}개 환경 생성 완료");
    }
}
```

기존 SetupInferenceMode.cs도 일반화하여 함께 생성:
- `[MenuItem("Tools/ML/Setup Training Mode")]` (모델 제거 + BehaviorType=Default)
- `[MenuItem("Tools/ML/Setup Inference Mode")]` (모델 할당 + BehaviorType=InferenceOnly)

## 6단계: 코드 검증 (ml-code-reviewer 호출)

생성된 Agent C# 스크립트에 대해 **ml-code-reviewer 서브에이전트 호출**:
- prompt: 생성된 파일 경로, 기대 vector_observation_size, action 구성, 보상 설계 정보 전달

리뷰 결과를 받아:
- 🔴 Critical 이슈가 있으면 사용자에게 보여주고 수정
- 🟡 Warning은 사용자에게 보여주되 진행 가능
- 🟢 OK면 다음 단계

## 7단계: 컴파일 검증 (Unity Editor 실행 시에만)

먼저 Unity Editor 실행 여부 확인:
```bash
unity-cli status 2>&1
```

### 케이스 A: Editor 실행 중

```bash
unity-cli editor refresh --compile 2>&1
unity-cli console --type error --lines 20
```

에러가 있으면 수정 후 재컴파일 (최대 3회 시도).

### 케이스 B: Editor 미실행

⚠️ **컴파일 검증 스킵하고 사용자에게 안내**:

```markdown
ℹ️ Unity Editor가 실행되지 않아 컴파일 검증을 스킵합니다.

생성된 파일:
- Assets/Scripts/ML/<Name>Agent.cs
- Assets/Editor/ML/Setup<Name>Training.cs
- ml-training/configs/<name>.yaml

Unity Editor를 열고 다음을 수행하세요:
1. 자동 컴파일 대기 (수 초)
2. Console에서 ML 관련 에러 확인
3. 에러가 있으면 채팅으로 알려주세요
```

ml-code-reviewer가 Critical 이슈를 발견했더라도 Editor 미실행이면 **사용자가 Editor를 열기 전까지 작업을 보류**하고 안내만 제공.

## 8단계: Inspector 설정 안내

자동으로 설정 가능한 항목은 unity-cli exec로 처리하고, 수동 설정이 필요한 항목은 사용자에게 안내:

```markdown
## 다음 Inspector 설정이 필요합니다:

1. <PlayerGameObject>에 <Name>Agent 컴포넌트 추가
2. BehaviorParameters 컴포넌트 확인:
   - Behavior Name: <BehaviorName>
   - Vector Observation Space Size: <N>
   - Continuous Actions: <N>
3. Decision Requester 컴포넌트 추가 (Decision Period: 5)
4. <Name>Agent의 spawnPoints, environmentRoot 필드 연결
5. 저장 후 `/ml-start` 실행
```

가능하면 unity-cli exec로 자동 추가 시도:
```bash
unity-cli exec "
  var go = GameObject.Find(\"<PlayerName>\");
  if (go.GetComponent<<Name>Agent>() == null) go.AddComponent<<Name>Agent>();
  if (go.GetComponent<Unity.MLAgents.DecisionRequester>() == null) {
    var dr = go.AddComponent<Unity.MLAgents.DecisionRequester>();
    dr.DecisionPeriod = 5;
  }
"
```

## 9단계: 완료 보고

```bash
cmux set-progress 1.0
cmux set-status ml-setting "설정 완료" --icon check
cmux notify --title "ml-setting 완료" --body "다음: /ml-start"
```

채팅 출력:
```
✅ ML-Agents 시나리오 생성 완료

생성된 파일:
- Assets/Scripts/ML/<Name>Agent.cs
- Assets/Editor/ML/Setup<Name>Training.cs
- ml-training/configs/<name>.yaml

Behavior: <BehaviorName>
관측: <N>개, 행동: <N>개 (continuous/discrete)
알고리즘: PPO/SAC

다음 단계:
1. Inspector에서 spawnPoints, environmentRoot 연결
2. /ml-start 실행
```

## 멱등성 / 재실행

같은 씬에 대해 재실행 시:
- 기존 파일 발견하면 사용자에게 확인 ("덮어쓰기 / 새 이름 / 취소")
- 학습 결과(`ml-training/results/`)는 절대 삭제하지 않음

## 에러 처리

| 상황 | 대응 |
|------|------|
| 사용자가 중간 취소 | 생성된 부분 파일 정리 여부 질문 |
| Q&A 답변이 모호 | 다시 묻거나 보수적 기본값 사용 |
| 컴파일 에러 발생 | 에러 메시지 공유 후 수정 시도 (최대 3회) |
| unity-cli 미사용 가능 | 모든 Unity 작업을 수동 가이드로 변경 |
