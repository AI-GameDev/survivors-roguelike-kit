# Unity ML-Agents inference 적용 패턴

Unity ML-Agents에서 학습된 `.onnx` 모델을 Play 모드에 적용하는 4가지 전형적 방식. 어느 패턴을 쓰는지 grep으로 판별하고 그에 맞춰 wiring한다.

## 공통 사실

ML-Agents에서 inference 모드의 정수는 다음 2줄:
```csharp
behaviorParameters.Model = modelAsset;
behaviorParameters.BehaviorType = BehaviorType.InferenceOnly;
```

`behaviorParameters`는 `Unity.MLAgents.Policies.BehaviorParameters` 컴포넌트, `modelAsset`는 `Unity.InferenceEngine.ModelAsset` (구버전: `Unity.Barracuda.NNModel`).

문제는 *어디에서* 이 2줄이 실행되느냐 — 그게 프로젝트 패턴을 결정한다.

## 패턴 A: BehaviorParameters 직접 (씬/프리팹 인스펙터)

가장 단순. 씬이나 프리팹의 BehaviorParameters 컴포넌트에 .onnx asset을 드래그.

**탐지**:
```bash
grep -rln 'BehaviorParameters' Assets 2>/dev/null
# 씬/프리팹의 yaml 안에서:
grep -rn 'm_Model:' Assets --include='*.unity' --include='*.prefab' 2>/dev/null
```

**적용 (자동화 어려움)**:
- 추천: 사용자에게 인스펙터에서 .onnx 드래그 안내.
- 자동화 가능 옵션: `.unity`/`.prefab` yaml의 `m_Model: {fileID: 0}` 라인 찾아 새 GUID로 치환 (위험 — yaml 직접 편집).

**판별 단서**: 부트스트랩 코드 / Editor 메뉴 / 직렬화 필드가 모두 없고 BehaviorParameters만 있으면 이 패턴.

## 패턴 B: 부트스트랩 직렬화 필드 (런타임 setup)

커스텀 MonoBehaviour가 시작 시 BehaviorParameters를 동적으로 채운다. 학습 vs 추론을 인스펙터 토글로 전환.

**시그니처**:
```csharp
[SerializeField] private bool _useInference;
[SerializeField] private Unity.InferenceEngine.ModelAsset _inferenceModel;

private void Setup() {
    var bp = rig.AddComponent<BehaviorParameters>();
    if (_useInference && _inferenceModel != null) {
        bp.Model = _inferenceModel;
        bp.BehaviorType = BehaviorType.InferenceOnly;
    }
}
```

**탐지**:
```bash
grep -rln 'SerializeField.*ModelAsset\|_inferenceModel\|_useInference' Assets --include='*.cs' 2>/dev/null
grep -rln 'BehaviorType.InferenceOnly' Assets --include='*.cs' 2>/dev/null
```

**적용**:
- 부트스트랩이 부착된 씬 yaml에서 SerializedObject 필드 갱신:
  - `_useInference = true`
  - `_inferenceModel` = ModelAsset GUID
- Editor 스크립트로 자동화 가능 (예: `SerializedObject so = new SerializedObject(bootstrap); so.FindProperty("_useInference").boolValue = true; ...`).

**판별 단서**: `_inferenceModel` 또는 비슷한 필드명을 가진 MonoBehaviour 발견. 이 프로젝트의 `MLAgentBootstrap.cs`가 대표 예.

## 패턴 C: Editor 메뉴 자동화 (`[MenuItem]`)

패턴 B 위에 `Tools/ML/Setup Inference Mode (v17 best)` 같은 메뉴 항목으로 모델 경로를 하드코딩.

**시그니처**:
```csharp
[MenuItem("Tools/ML/Setup Inference Mode (vXX best)")]
public static void SetupVXXBest() {
    const string MODEL_PATH = "Assets/ML-Models/AgentName_vXX_best.onnx";
    ApplyInferenceMode(true, MODEL_PATH);
}

private static void ApplyInferenceMode(bool useInference, string modelPath) {
    var bootstrap = ...;
    var so = new SerializedObject(bootstrap);
    so.FindProperty("_useInference").boolValue = useInference;
    so.FindProperty("_inferenceModel").objectReferenceValue =
        AssetDatabase.LoadAssetAtPath<ModelAsset>(modelPath);
    so.ApplyModifiedProperties();
    EditorSceneManager.SaveScene(...);
}
```

**탐지**:
```bash
grep -rln 'MenuItem.*Inference\|MenuItem.*Setup.*Model' Assets/Editor 2>/dev/null
grep -rn 'ApplyInferenceMode\|EnableInference' Assets/Editor --include='*.cs' 2>/dev/null
```

**적용**:
- 이미 메뉴가 있으면 그 함수를 호출하는 게 가장 안전.
- `unity-cli exec` 로 메뉴의 static 메서드를 직접 호출하거나, 사용자에게 Unity Editor의 Tools 메뉴에서 클릭 안내.
- 새 모델을 위해 메뉴 항목을 추가하려면 기존 패턴 그대로 복사해 모델 경로만 바꾸기. `ApplyInferenceMode`를 public으로 노출하면 메뉴 하드코딩 없이 동적 경로도 가능.

**판별 단서**: `Assets/Editor/` 디렉토리 내 MenuItem 발견. 이 프로젝트의 `SetupSurvivorFighterTraining.cs`가 대표 예.

## 패턴 D: AssetDatabase 검색 (코드에서 .onnx 자동 발견)

부트스트랩이 Resources 또는 AssetDatabase로 .onnx를 검색해 자동 wiring.

**시그니처**:
```csharp
var guids = AssetDatabase.FindAssets("t:ModelAsset");
var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
bp.Model = model;
```

**탐지**:
```bash
grep -rln 'FindAssets.*ModelAsset\|LoadAssetAtPath.*ModelAsset\|Resources.Load.*ModelAsset' Assets --include='*.cs' 2>/dev/null
```

**적용**:
- 검색 조건(이름 패턴, 폴더 등)을 조정하면 자동으로 새 모델 픽업. 사용자에게 어떤 모델이 픽업될지 확인 후 진행.

**판별 단서**: 드물지만 ml-agents 예제 프로젝트에서 발견됨.

## 패턴별 의사결정 트리

```
1. Assets/Editor 안에 MenuItem("...Inference...") 있나?
     ├─ YES → 패턴 C. 메뉴 호출 또는 새 메뉴 추가.
     └─ NO ↓
2. .cs 안에 _inferenceModel 또는 비슷한 SerializeField 있나?
     ├─ YES → 패턴 B. SerializedObject 갱신 또는 메뉴 추가 권장.
     └─ NO ↓
3. .cs 안에 AssetDatabase.FindAssets.*ModelAsset 있나?
     ├─ YES → 패턴 D. 검색 조건 확인.
     └─ NO ↓
4. 씬/프리팹의 BehaviorParameters만 발견되나?
     ├─ YES → 패턴 A. 사용자에게 인스펙터 안내, 또는 yaml GUID 치환.
     └─ NO  → wiring 메커니즘 없음. 사용자에게 패턴 B를 직접 추가하도록 제안.
```

## .onnx GUID 추출/매칭

씬 yaml에서 현재 wired된 모델 GUID 추출:
```bash
# 예시: 부트스트랩 직렬화의 _inferenceModel 라인
grep -A1 '_inferenceModel:' Assets/Scenes/<name>.unity | grep 'guid:'
```

모델 파일의 GUID는 `.meta` 파일 안:
```bash
grep '^guid:' Assets/ML-Models/<modelname>.onnx.meta
```

두 값이 일치하면 그 모델이 현재 wired된 것.

## 흔한 함정

- **ModelAsset 네임스페이스 충돌**: Unity 6+ 의 `Unity.InferenceEngine.ModelAsset` vs 구버전 `Unity.Barracuda.NNModel`. 프로젝트의 ml-agents 버전 확인 필요.
- **메뉴 호출 후 씬 미저장**: SerializedObject 갱신만 하고 `EditorSceneManager.MarkSceneDirty` / `SaveScene` 호출 누락 → 다음 Play에서 변경 사라짐.
- **DontDestroyOnLoad 부트스트랩**: 씬 리로드해도 부트스트랩은 살아남으므로 wiring은 첫 씬 진입 시점에만 결정. 학습→인퍼런스 전환 후 Play 재시작 권장.
- **prefab override**: 씬에서 BehaviorParameters를 변경했지만 prefab 원본에서 다시 override 받음. prefab 자체를 수정해야 영구.
- **time-scale 영향 없음**: inference 적용은 Editor 메타데이터 변경이라 timeScale과 무관.
