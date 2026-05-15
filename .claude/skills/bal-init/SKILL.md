---
name: bal-init
description: ML-Agents 학습 직후 ~ 밸런싱 테스트 직전 단계의 초기화 워크플로우. (1) 학습된 모델이 Editor Play로 즉시 동작하도록 inference wiring을 검증/적용하고, (2) PlayTrace 서버(docs/MANUAL.md 참조)로 게임 이벤트를 단건 스트리밍하는 로깅 통합을 사용자와의 AskUserQuestion 인터뷰로 설계·구현한다. 사용자가 "/bal-init", "bal init", "밸런싱 초기화", "학습 모델 적용", "PlayTrace 통합", "로그 서버 연결", "테스트 환경 셋업" 등을 언급하면 사용. 선행 조건: /ml-start 완료 후 .onnx 모델이 존재.
disable-model-invocation: false
---

# bal-init: 학습 후 밸런싱 테스트 환경 셋업

이 스킬은 학습이 끝난 뒤 "Play를 누르면 곧장 학습된 AI로 돌아가고, 의미있는 신호가 PlayTrace 서버에 쌓이는 상태"까지 다리를 놓는다. 두 가지 책임을 순서대로 수행한다.

```
Phase 1: 모델 inference wiring 검증·적용     (Play 누르면 학습된 모델로 동작?)
Phase 2: PlayTrace 로깅 통합                  (서버에 의미있는 신호 흘러가?)
```

## 핵심 원칙

1. **게임-비종속**: 특정 게임/에이전트 클래스명을 가정하지 않는다. 모든 경로/이름은 탐색 또는 사용자 질의로 발견한다. references/ 에 적힌 클래스명은 "예시 패턴"일 뿐이다.
2. **기존 게임 코드 무수정**: 가능하면 EventChannelSO / UnityEvent / 기존 hook을 이용. base 코드 수정이 필요하면 반드시 `AskUserQuestion`으로 일회성 예외 승인.
3. **AskUserQuestion 우선**: 자동 추론을 줄이고 사용자 의도를 명시적으로 확인. 단 read-only 탐색(grep/Read)을 충분히 한 뒤 구체적 질문을 한다 — "어떤 게임인가요?"가 아니라 "X와 Y 중 어느 쪽인가요?".
4. **검증 가능한 종결**: 각 Phase 끝에 "동작했는지 어떻게 아는가" 가시화 — Editor 콘솔 로그 / curl 응답 / 대시보드 URL 등.

## 사전 확인

`/bal-init` 시작 시 다음을 빠르게 확인:

```bash
# 1) Unity 프로젝트인지
test -d Assets || echo "NOT_UNITY"

# 2) 학습된 모델이 존재하는지
find Assets -name '*.onnx' -type f 2>/dev/null | head
```

조건 미충족이면 스킬을 일찍 종료:
- Unity 프로젝트가 아니면 → "이 스킬은 Unity ML-Agents 전용입니다. 다른 엔진의 워크플로우는 아직 지원하지 않습니다." 후 종료.
- .onnx 모델이 하나도 없으면 → "학습된 모델을 찾을 수 없습니다. `/ml-start` 로 학습을 먼저 완료하시거나, 모델 경로를 알려주세요." 후 `AskUserQuestion`으로 직접 경로 입력 받기.

cmux가 있으면 상태 표시:
```bash
cmux set-status bal-init "초기화 중..." --icon hourglass 2>/dev/null
cmux set-progress 0.0 2>/dev/null
```

---

## Phase 1 — 학습된 모델 적용 검증·실행

### 1단계: 모델 인벤토리

후보 모델을 다 모은다. 정렬은 mtime 내림차순(가장 최근에 학습한 것 우선).

```bash
find Assets -name '*.onnx' -type f -exec stat -f '%m %N' {} \; 2>/dev/null | sort -rn | head -20
```

결과를 사람이 읽기 좋게 가공해 사용자에게 보여줄 준비를 한다 (basename + mtime, 또는 디렉토리 기준 그룹).

### 2단계: inference wiring 메커니즘 탐색

프로젝트가 어떤 방식으로 .onnx를 BehaviorParameters에 연결하는지 결정한다. 자세한 패턴은 `references/inference-unity.md` 참조. 핵심 grep 4개:

```bash
# (A) Editor 메뉴 자동화 패턴
grep -rln 'MenuItem.*Inference\|MenuItem.*Setup.*Model' Assets/Editor 2>/dev/null

# (B) 부트스트랩 직렬화 필드 패턴
grep -rln 'SerializeField.*ModelAsset\|SerializeField.*_inferenceModel\|_useInference' Assets 2>/dev/null

# (C) BehaviorParameters 인스펙터 직접 할당 (씬/프리팹)
grep -rln 'BehaviorParameters' Assets 2>/dev/null | head -5

# (D) ApplyInferenceMode/SetInferenceModel 같은 유틸 메서드
grep -rln 'ApplyInferenceMode\|SetInferenceModel\|EnableInference' Assets 2>/dev/null
```

탐지된 결과로 메커니즘을 분류:
- **메뉴 기반**: `MenuItem` 발견. → `Tools/ML/Setup Inference Mode (...)` 같은 메뉴를 그대로 호출하거나 패턴을 따라 추가 메뉴 생성 제안.
- **부트스트랩 필드 기반**: `_inferenceModel` 필드 발견. → 씬/프리팹의 직렬화 값을 SerializedObject로 갱신.
- **인스펙터 직접**: BehaviorParameters만 있고 매핑 코드는 없음. → 씬/프리팹의 Model 슬롯을 GUID로 갱신.
- **없음**: 학습 후 inference로 전환할 mechanism이 프로젝트에 없음. → 사용자에게 어디에 모델을 꽂아야 하는지 질문하고 필요 시 패턴 (B) 또는 (A)에 해당하는 짧은 Editor 스크립트를 새로 작성 제안.

### 3단계: 현재 wired 상태 확인

가장 최근 모델과 현재 적용된 모델이 같은지 확인.

`unity-cli`가 있고 Editor가 실행 중이면 짧은 C# 코드로 직접 확인하는 방법이 가장 정확:

```bash
unity-cli editor refresh --compile 2>/dev/null
unity-cli exec '<C# snippet that finds BehaviorParameters and prints Model.name>'
```

`unity-cli`가 없거나 Editor가 닫혀 있으면:
- 씬/프리팹 yaml 안의 `_inferenceModel` 또는 `m_Model` 필드 GUID 추출
- 후보 모델들의 `<name>.onnx.meta` 안의 GUID와 매칭
- 일치하는 모델 이름을 사용자에게 보고

### 4단계: 적용 결정 + 실행

다음 표 기준으로 행동:

| 현재 상태 | 행동 |
|---|---|
| 최신 모델이 이미 wired | "이미 X가 적용되어 있습니다" 출력 → Phase 2로 |
| 다른 모델이 wired (구버전) | `AskUserQuestion`으로 [최신으로 교체 / 현재 유지] 묻기 |
| 아무 모델도 wired 안 됨 | `AskUserQuestion`으로 후보 중 어느 것 적용할지 묻기 |
| best vs final 둘 다 있음 | `AskUserQuestion`으로 [best / final / 다른 것] 묻기 — best는 학습 중 최고 보상, final은 마지막 체크포인트 |

적용 절차:
- **메뉴 기반**이면: 가능하면 `unity-cli exec`로 메뉴 함수 호출, 아니면 사용자에게 `Tools/ML/...` 메뉴 클릭 안내.
- **부트스트랩 필드 기반**이면: Editor 스크립트로 SerializedObject 갱신 + `AssetDatabase.SaveAssets` (이 프로젝트의 `ApplyInferenceMode` 패턴 참고).
- **인스펙터 직접**이면: 사용자에게 인스펙터에서 .onnx 드래그 안내, 또는 GUID 치환 스크립트 제안.

### 5단계: Phase 1 검증

```bash
# Editor 컴파일 클린 확인
unity-cli editor refresh --compile 2>&1 | tail -3
unity-cli console --type error --lines 5

# Editor가 떠 있으면 Play로 5~10초 돌려보기
unity-cli editor play --wait 2>/dev/null
# 콘솔에서 ML 관련 로그 (Behavior Type, Inference, Model loaded 등)이 나오는지 확인
unity-cli console --pattern 'Behavior|Inference|onnx|Model' --lines 10 2>/dev/null
unity-cli editor stop 2>/dev/null
```

Editor가 닫혀 있으면: "Editor를 띄우고 Play를 눌러 학습된 모델로 동작하는지 확인해 주세요" 안내.

Phase 1 성공 기준:
- ✅ 컴파일 에러 없음
- ✅ 사용자가 의도한 모델이 wired
- ✅ Play 시 InferenceOnly 모드 진입 (콘솔/플레이 화면)

---

## Phase 2 — PlayTrace 로깅 통합

### 1단계: MANUAL.md 흡수

```bash
# 매뉴얼 경로 확인
ls docs/MANUAL.md 2>/dev/null
```

없으면 `AskUserQuestion`으로 경로 입력 받기. 있으면 Read로 읽기. 핵심 API는 `references/playtrace-api.md` 에도 요약본이 있으니 양쪽을 교차 참고.

### 2단계: 서버 health check

```bash
curl -s -m 3 http://localhost:8000/health
```

응답이 `{"status":"ok"}` 가 아니면 **서버 운영자에게 기동 요청**. MANUAL §2 정책상 클라이언트 개발자(이 스킬이 돌아가는 환경)는 서버를 직접 띄울 책임이 없으므로 `uvicorn` 같은 기동 명령을 시도하지 말고, 다음 안내로 `AskUserQuestion` 차단 게이트를 띄운다.

```
PlayTrace 서버에 연결할 수 없습니다 (http://localhost:8000/health 무응답).
서버 운영자에게 기동을 요청해 주세요. 어떻게 진행할까요?
```

옵션:
- **재시도** — 서버가 떠 있다고 알려 줬으니 health check 다시 시도 (Recommended).
- **URL 변경** — 서버 주소가 다른 머신/포트 (사용자에게 대체 URL 입력 받기, MANUAL §2의 "다른 머신에서 실행 중이라면 IP/호스트:포트 교체" 정책).
- **중단** — `/bal-init` 종료. 서버 없이는 Phase 2 후속 단계(클라이언트 코드 작성 후 검증)가 의미를 잃으므로 무리하게 진행하지 않음.

### 3단계: 기존 PlayTrace 통합 점검

```bash
grep -rln 'PlayTraceClient\|playtrace\|/api/logs\|/api/sessions' Assets 2>/dev/null
```

- **이미 통합되어 있음**: 어떤 키들이 이미 흘러가는지 보고 → 기존 키를 살려두고 추가/조정 위주로.
- **부분 통합**: 클라이언트는 있는데 일부 이벤트만 hook됨. → 누락된 이벤트 추가.
- **미통합**: 새로 클라이언트 작성 + hook 부착.

### 4단계: 게임 표면 정찰

이 게임에서 의미있을 만한 이벤트 후보를 모은다 (사용자 결정의 보기 만들기). 다양한 장르를 고려한 grep 시그니처:

```bash
# 데미지/HP 변화
grep -rln 'OnDamage\|TakeDamage\|Hp\|HP\|Health' Assets --include='*.cs' 2>/dev/null | head -10

# 죽음/처치
grep -rln 'OnDeath\|OnKill\|Die()\|Killed' Assets --include='*.cs' 2>/dev/null | head -10

# 픽업/획득
grep -rln 'Pickup\|Collect\|Loot\|Reward' Assets --include='*.cs' 2>/dev/null | head -10

# 레벨/진행
grep -rln 'LevelUp\|Exp\|Stage\|Wave\|Phase\|Round' Assets --include='*.cs' 2>/dev/null | head -10

# 스킬/아이템 선택
grep -rln 'Skill\|Item\|Choice\|Upgrade' Assets --include='*.cs' 2>/dev/null | head -10

# 통화/점수
grep -rln 'Gold\|Coin\|Score\|Currency' Assets --include='*.cs' 2>/dev/null | head -10

# 이벤트 채널 패턴 (RSOFramework 류)
grep -rln 'EventChannelSO\|UnityEvent' Assets --include='*.cs' 2>/dev/null | head -10
```

조사 결과를 4–8개 카테고리로 요약 (예: "이 프로젝트엔 HP 변화, 적 처치, 레벨업, 스킬 선택, 보물 상자 이벤트가 있는 것 같습니다").

### 5단계: AskUserQuestion으로 로깅 의도 정리

질문 4개 이내로 압축. 각 질문은 사용자가 이 게임에 대해 가장 잘 알기에 충분히 구체적이게:

**질문 1 — 식별 키 매핑**
- `project_name`: 이 게임 이름은 무엇으로 할지 (디폴트: 현재 디렉토리 basename)
- `version`: 빌드 버전 / 모델 이름 / 둘 다 묶기

**질문 2 — play_no 증가 규칙**
- 옵션 예: "사망마다 +1 (vampire-survivors 류)" / "라운드/스테이지 클리어마다 +1" / "수동 (대시보드용으로 모두 1)" / 기타

**질문 3 — 시계열 메트릭 (multiSelect)**
- 4단계에서 찾은 number 후보 중 추적할 것: HP, 점수, 통화, 활성 적 수, 처치수, EXP, ...

**질문 4 — 이벤트 (multiSelect)**
- 단건 이벤트(주로 text/bool/count): 스킬 선택, 아이템 픽업, 레벨업, 보스 처치, 게임오버, ...

질문은 한 번에 모두(`questions` 배열에 4개)가 아니라 답변에 따라 다음 질문이 바뀐다면 순차 호출. 일반적으로는 한 묶음으로 처리해도 무난.

### 6단계: 통합 구현

**(A) PlayTraceClient.cs (없으면 신규)**

위치: 보통 `Assets/Scripts/Logging/` 또는 사용자 게임 코드 폴더 (`Assets/RGame/...`)와 겹치지 않게 분리. 사용자에게 폴더 선택 받거나 합리적 디폴트(`Assets/Scripts/PlayTrace/`) 사용.

최소 인터페이스:
```csharp
public class PlayTraceClient : MonoBehaviour
{
    public string BaseUrl = "http://localhost:8000";
    public bool IsReady { get; }
    public void BeginSession(string projectName, string version, string sessionName);
    public void Log(int playNo, string key, object value); // fire-and-forget
}
```

요구사항 (MANUAL §3.5 안티패턴 회피):
- `client_time`은 반드시 ms epoch (`DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`)
- bool/int/long/float/double/string 각각을 JSON 타입에 맞게 직렬화. C#에서 bool은 int 서브클래스가 아니지만 `switch`/`is` 순서 주의.
- 응답 body `success` 필드 확인 (HTTP 200 + success:false 패턴 존재)
- 같은 key에 number와 text 섞지 않기 — 키 설계할 때 사용자에게 명시.
- batch / retry queue 도입 금지 (MVP 정책)

**(B) hook 부착**

선호 순서:
1. **EventChannelSO 리스너**: 기존 SO 이벤트가 있으면 부착 컴포넌트만 새로 추가 (base 무수정).
2. **UnityEvent / C# event 구독**: public 이벤트가 있으면 구독.
3. **새 MonoBehaviour를 prefab에 추가**: 기존 이벤트 surface가 있는 객체에 옵저버 컴포넌트로 부착.
4. **(최후의 수단) base 코드 수정**: 위 셋이 다 안 되면 `AskUserQuestion`으로 일회성 예외 승인 요청. 메모리 `feedback_balance_logger_exception.md` 패턴 (정적 brokerage hook, listener 없으면 no-op) 참고.

**(C) 모델 이름과 version 연동**

Phase 1에서 적용한 모델 이름을 PlayTrace의 `version` 필드에 넘기면 대시보드에서 모델별 비교 가능. 부트스트랩 코드에서 모델 이름 노출 방법:
- 이미 `[SerializeField] _inferenceModel` 있으면 그 .name 사용.
- 없으면 사용자에게 수동 입력 받기.

### 7단계: Phase 2 검증

```bash
# 컴파일
unity-cli editor refresh --compile 2>&1 | tail -3
unity-cli console --type error --lines 5

# Play 시작 후 짧게 모니터링
unity-cli editor play --wait
sleep 8

# 서버에 세션 도착했는지
curl -s "http://localhost:8000/api/sessions?project_name=<USER_PROJECT>" | head -5

# 키들이 흘러갔는지
curl -s "http://localhost:8000/api/logs/search?project_name=<USER_PROJECT>&size=20" | head

unity-cli editor stop
```

성공 기준:
- ✅ 컴파일 에러 없음
- ✅ 세션 1개 생성됨
- ✅ 사용자가 5단계에서 선택한 키들이 서버에 도착
- ✅ `success:false` 응답이 없음 (값 타입 일관성 확인)

성공 시 대시보드 URL을 안내:
```
http://localhost:8000/dashboard
  → project=<USER_PROJECT> 선택
  → version=<MODEL_NAME> 선택
  → 최신 session 선택 → key chip 추가
```

---

## 종결 처리

```bash
cmux set-status bal-init "완료" --icon check 2>/dev/null
cmux set-progress 1.0 2>/dev/null
```

다음 단계 안내:
- 사용자가 의도한 시간만큼 Play로 데이터를 모은다.
- 대시보드에서 시계열/이벤트 확인.
- 밸런싱 조정이 필요하면 `/ml-setting` 또는 `/ml-start`로 다시 학습.

---

## 자주 발생하는 함정 (체크리스트)

이 스킬을 실행하면서 빠지기 쉬운 실수들:

- **하드코딩**: `SurvivorFighterAgent`, `MLAgentBootstrap` 등을 추정. → grep으로 발견, 같은 이름이 없으면 사용자에게 물어볼 것.
- **value 타입 일관성**: 같은 키에 80(number)과 "alive"(text)를 섞어 보내면 차트에서 number만 그려짐. 키 설계 시 사용자에게 명시.
- **`client_time` 초 단위 송신**: 차트가 1970년으로 찍힘. 항상 ms.
- **세션 자동생성 폭주**: `Init` 함수가 씬 리로드마다 호출되면 매번 새 세션 생성. `MLAgentBootstrap` 같은 DontDestroyOnLoad GO에 부착해 1회만 생성하게 유도.
- **HTTP 200 trap**: 검증 실패도 200 반환 → body `success` 확인.
- **time-scale 학습 모드 부담**: time-scale=20에서 초당 수백 POST 가능. 사용자에게 학습 중에도 켤지/끌지 옵션 명시 (기본: 학습 중은 끄거나 샘플링).
- **base 코드 수정 거부 후 우회**: EventChannel 발견했다고 자만 말고 정말로 그 채널이 필요한 시점에 발화하는지 코드 추적 후 사용.

## 참고 자료

- `references/playtrace-api.md` — PlayTrace API 컨닝페이퍼 (MANUAL §3.2/3.5/7 요약)
- `references/inference-unity.md` — Unity ML-Agents inference 적용 4가지 패턴 + grep 시그니처
- 프로젝트 `docs/MANUAL.md` — PlayTrace 원본 매뉴얼 (있는 경우)
