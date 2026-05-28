---
name: ml-init
description: Unity 프로젝트에 ML-Agents 강화학습 환경을 초기 설정. ML-Agents 패키지/Python 환경/폴더 구조를 자동으로 준비. 사용자가 "/ml-init", "ML-Agents 초기화", "강화학습 환경 설정", "ml 프로젝트 시작" 등을 언급하면 사용.
disable-model-invocation: false
---

# ml-init: ML-Agents 프로젝트 초기화

이 스킬은 Unity 프로젝트에 ML-Agents 강화학습 개발 환경을 처음 설정할 때 사용한다. 한 번만 실행하면 되며, `/ml-setting`과 `/ml-start`가 작동하기 위한 기반을 만든다.

## ⚠️ 절대 규칙

**모든 bash 명령은 절대 경로 사용**. `cd <subdir>`가 후속 명령의 cwd에 영향을 줘서 멱등성/검증 실패가 발생함.

```bash
# ❌ 나쁜 예 — cwd가 ml-training/으로 누적됨
cd ml-training && uv sync

# ✅ 좋은 예 — 한 번의 서브셸에 격리
(cd /Users/<user>/<project>/ml-training && uv sync)
# 또는
cd /Users/<user>/<project>/ml-training && uv sync 2>&1; cd - > /dev/null
```

스킬 시작 시 프로젝트 루트를 변수로 캡쳐하여 사용:
```bash
PROJECT_ROOT="$(pwd)"  # 또는 Unity 프로젝트 루트 자동 감지
```

## 실행 흐름 요약

1. 환경 감지 (Unity 프로젝트 루트, Unity 버전, cmux)
2. **ML-Agents 패키지 + unity-cli-connector를 manifest.json에 직접 추가** (단순 안내 금지)
3. 프로젝트 폴더 구조 생성
4. Python 환경(`ml-training/`) 생성 및 `uv sync`
5. AGENTS.md / .gitignore 업데이트
6. 검증 및 다음 단계 안내

## 1단계: 환경 감지 + 기존 레이아웃 점검

```bash
# Unity 프로젝트 루트 확인 (현재 디렉토리)
ls Assets ProjectSettings 2>&1
cat ProjectSettings/ProjectVersion.txt | head -1

# cmux 환경 여부
echo "CMUX: ${CMUX_WORKSPACE_ID:-없음}"

# uv 설치 확인
which uv || echo "uv 미설치 — https://docs.astral.sh/uv/ 에서 설치 필요"
```

### 기존 레이아웃 충돌 감지 (중요)

표준 컨벤션과 다른 위치에 학습 결과/스크립트가 있으면 사용자 선택을 받는다:

```bash
# 비표준 위치에 학습 결과가 있는지 검색
echo "=== 비표준 학습 결과 위치 검색 ==="
find Assets -maxdepth 3 -type d \( -name "result*" -o -name "results" -o -name "training_result*" \) 2>/dev/null
find . -maxdepth 2 -type d -name "*ml-agents*" 2>/dev/null | grep -v node_modules | grep -v .venv
```

찾은 폴더가 있으면 AskUserQuestion으로 처리 방향 결정:
- **레거시 유지** (기본): 기존 위치는 그대로, 신규 학습부터 `ml-training/` 사용. AGENTS.md에 두 위치 모두 명시.
- **마이그레이션**: 기존 결과를 `ml-training/results/legacy/`로 이동
- **취소**: 작업 중단

### cmux 표시

```bash
cmux set-status ml-init "초기화 진행 중..." --icon hourglass
cmux set-progress 0.0 --label "환경 감지"
```

## 2단계: 패키지 설치 (직접 manifest.json 편집)

⚠️ **중요 — 단순 안내로 끝내지 말 것**: 누락된 패키지는 **반드시 Edit 툴로 `Packages/manifest.json`에 직접 추가**한다. "Unity Editor에서 설치하세요"라고 안내만 하고 종료하면 안 됨. 두 패키지 모두 manifest.json에 한 줄 추가하면 Unity가 다음 새로고침 시 자동 다운로드함.

### 2-1. 현재 상태 확인

```bash
grep -E "com\.unity\.ml-agents|unity-cli-connector" /절대경로/Packages/manifest.json
```

### 2-2. 패키지별 처리 규칙

| 패키지 | 누락 시 처리 방법 |
|--------|----------------|
| `com.unity.ml-agents` | **즉시 manifest.json에 추가** (사용자 확인 불필요 — 공식 Unity 패키지, 가역적 변경) |
| `com.youngwoocho02.unity-cli-connector` | **AskUserQuestion으로 동의 확인 후 manifest.json에 추가** (서드파티 git 패키지이므로 동의 필요) |

### 2-3. 실제 편집 (Edit 툴 사용)

`com.unity.ml-agents` 추가 — `com.unity.inputsystem` 다음 줄에 삽입:
```
old_string:
    "com.unity.inputsystem": "<버전>",
    "com.unity.multiplayer.center": "<버전>",

new_string:
    "com.unity.inputsystem": "<버전>",
    "com.unity.ml-agents": "4.0.2",
    "com.unity.multiplayer.center": "<버전>",
```

`com.youngwoocho02.unity-cli-connector` 추가 — 마지막 의존성 뒤에 추가 (콤마 주의):
```
old_string:
    "com.unity.modules.xr": "1.0.0"
  }

new_string:
    "com.unity.modules.xr": "1.0.0",
    "com.youngwoocho02.unity-cli-connector": "https://github.com/youngwoocho02/unity-cli.git?path=unity-connector"
  }
```

### 2-4. 검증

편집 후 `grep`으로 두 항목이 모두 있는지 확인하고, 사용자에게 다음 정보 보고:
- 추가된 패키지 목록
- "Unity Editor를 켜면(또는 켜져 있으면) 자동으로 패키지를 다운로드함"
- `Library/PackageCache/`에 `com.unity.ml-agents@*`, `com.youngwoocho02.unity-cli-connector@*` 폴더 생성을 다음 단계 검증 기준으로 안내

### 2-5. 예외 — manifest.json이 잠긴 경우

`Packages/manifest.json`이 git lock 또는 권한 문제로 편집 불가하면 그제서야 사용자에게 수동 설치 안내. 그렇지 않으면 항상 직접 편집한다.

### 2-6. 흔한 실수 (하지 말 것)

❌ "패키지가 없습니다. Unity Editor에서 설치해주세요"라고만 안내하고 종료
❌ `com.youngwoocho02.unity-cli-connector`를 동의 없이 무단 추가 (서드파티이므로 AskUserQuestion 필수)
❌ `com.unity.ml-agents`만 추가하고 cli-connector는 "안내만" 처리

## 3단계: 폴더 구조 생성 (멱등)

`mkdir -p`은 이미 존재하는 폴더에 대해서도 안전:
```bash
mkdir -p Assets/Scripts/ML Assets/Editor/ML Assets/ML-Models Assets/Scenes/Training
mkdir -p ml-training/configs
```

### asmdef 전략 — 게임 코드 어셈블리 구조 진단 후 분기

> ⚠️ **커스텀 asmdef를 만들기 전에 반드시 게임 코드의 어셈블리 구조를 진단할 것.**
> 커스텀 asmdef는 `Assembly-CSharp`(기본 어셈블리)를 이름으로 참조할 수 없음 — Unity의 근본 제약.
> 진단 없이 ML.asmdef를 만들면 게임 클래스(`PlayerController` 등)가 보이지 않아 `CS0246` 다발 발생.

> ⚠️ **ML-Agents 어셈블리 이름**: 하이픈 포함 `Unity.ML-Agents` (NOT `Unity.MLAgents`). C# `using Unity.MLAgents;` 네임스페이스와 다름.

**1단계: 진단**

```bash
# 메인 게임 스크립트 폴더(써드파티·Mirror·Editor 제외)에 asmdef가 있는지 확인
GAME_ASMDEF=$(find Assets -maxdepth 4 -name "*.asmdef" \
  -not -path "*/ML/*" \
  -not -path "*/Mirror/*" \
  -not -path "*/Editor/*" \
  -not -path "*/Plugins/*" \
  2>/dev/null | head -1)

if [ -z "$GAME_ASMDEF" ]; then
  echo "케이스 1: 메인 게임이 Assembly-CSharp(기본) → ML asmdef 생성 안 함"
else
  echo "케이스 2: 메인 게임 asmdef 발견: $GAME_ASMDEF → ML.asmdef 생성 + 참조 추가"
fi
```

**케이스 1 — 메인 게임이 `Assembly-CSharp`에 있을 때 (대부분의 프로젝트)**

`ML.asmdef`와 `ML.Editor.asmdef`를 **생성하지 않는다**.
- ML 스크립트가 자동으로 `Assembly-CSharp`에 합류 → 게임 클래스 참조 가능
- Editor 스크립트가 자동으로 `Assembly-CSharp-Editor`에 합류 → 게임 클래스 + ML 스크립트 모두 참조 가능
- `Unity.ML-Agents` 패키지는 `autoReferenced: true`이므로 양쪽에서 자동으로 보임

**케이스 2 — 메인 게임이 커스텀 asmdef일 때 (예: `Game.asmdef`)**

게임 asmdef 이름 추출:
```bash
GAME_ASMDEF_NAME=$(grep '"name"' "$GAME_ASMDEF" | head -1 | sed 's/.*"name": "\(.*\)".*/\1/')
echo "게임 asmdef 이름: $GAME_ASMDEF_NAME"
```

`Assets/Scripts/ML/ML.asmdef` 생성:
```json
{
    "name": "ML",
    "rootNamespace": "",
    "references": [
        "Unity.ML-Agents",
        "Unity.InputSystem",
        "<GAME_ASMDEF_NAME>"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`Assets/Editor/ML/ML.Editor.asmdef` 생성 (케이스 2 전용):
```json
{
    "name": "ML.Editor",
    "rootNamespace": "",
    "references": [
        "ML",
        "Unity.ML-Agents",
        "<GAME_ASMDEF_NAME>"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

## 4단계: Python 환경 설정 (Option A — 프로젝트별 격리)

**중요**: 학습 알고리즘(PPO/SAC 등) 코드는 작성하지 않는다. Unity가 배포하는 `mlagents` PyPI 패키지에 모두 들어있다. 여기서 만드는 것은 의존성 명세 파일들뿐.

**디스크/시간 추정** (실측 기반):
- venv 크기: **약 530MB** (캐시 포함 시 더 빠름)
- `uv sync` 소요: **첫 설치 5~10분 / 캐시 적중 시 ~1분**
- 디스크: 첫 설치 시 PyPI에서 다운로드 (~1.2GB), 이후 프로젝트는 uv 캐시 재사용

### 4-1. `ml-training/pyproject.toml` 작성

```toml
[project]
name = "mlagents-trainer"
version = "0.1.0"
description = "Unity ML-Agents training environment"
readme = "README.md"
requires-python = ">=3.10.1,<=3.10.12"
dependencies = [
  "mlagents==1.1.0",
  "setuptools<60",
  "torch>=2.2.1,<2.3",
]

[tool.uv]
override-dependencies = [
  "grpcio==1.78.0",
]
```

### 4-2. 부가 파일 작성

`ml-training/.python-version`:
```
3.10
```

`ml-training/.gitignore`:
```
.venv/
results/
*.egg-info/
__pycache__/
```

`ml-training/README.md`:
```markdown
# ML-Agents Trainer

이 폴더는 `/ml-init`이 자동 생성한 Python 학습 환경입니다.

## 사용법
- 학습 시작: 프로젝트 루트에서 `/ml-start` 실행
- 수동 실행: `cd ml-training && source .venv/bin/activate && mlagents-learn configs/<name>.yaml --run-id=<id>`
```

### 4-3. uv sync 실행

⚠️ **사용자에게 확인 후 실행**: 캐시 상태에 따라 1~10분 소요. 첫 실행이면 ~530MB venv 생성.

캐시 적중 가능성 사전 확인 (다른 프로젝트에서 mlagents를 이미 설치한 적 있으면 1분 이내):
```bash
ls ~/Library/Caches/uv/ 2>/dev/null | head -3 || echo "uv 캐시 없음 (첫 설치)"
```

`run_in_background: true`로 실행하고 Monitor로 진행 상황 추적 권장:
```bash
cd ml-training && uv sync 2>&1
```

cmux 환경이면 진행 표시:
```bash
cmux set-progress 0.5 --label "uv sync 실행 중"
```

### 4-4. 검증

```bash
cd ml-training && source .venv/bin/activate && mlagents-learn --help | head -5
```

오류 없이 도움말이 나오면 성공.

## 5단계: 프로젝트 설정 업데이트

### 5-1. AGENTS.md 업데이트

AGENTS.md가 있으면 다음 섹션을 추가 (없으면 생성):

````markdown
## ML-Agents 강화학습 환경

이 프로젝트는 ML-Agents 4.x로 강화학습을 진행한다.

- 학습 환경 루트: `ml-training/`
- Agent 스크립트: `Assets/Scripts/ML/`
- Editor 자동화: `Assets/Editor/ML/`
- 학습된 모델: `Assets/ML-Models/`
- 학습 결과: `ml-training/results/`

### 워크플로우
1. `/ml-setting` — 씬 분석 후 Agent 코드/YAML 설정 생성
2. `/ml-start` — Python 트레이너 + Unity 실행 + 모니터링

### 수동 실행 명령
```bash
cd ml-training && source .venv/bin/activate
mlagents-learn configs/<name>.yaml --run-id=<id> --time-scale 20
```
````

### 5-2. 루트 .gitignore 업데이트

```
ml-training/.venv/
ml-training/results/
Assets/ML-Models/*.onnx
```

(.onnx는 LFS로 관리하지 않을 경우. 필요시 사용자와 상의)

## 6단계: 완료 보고

cmux 환경:
```bash
cmux set-progress 1.0 --label "초기화 완료"
cmux set-status ml-init "준비 완료" --icon check
cmux notify --title "ml-init 완료" --body "다음 단계: /ml-setting"
```

채팅에 다음 형식으로 보고:
```
✅ ML-Agents 환경 초기화 완료

생성된 항목:
- Assets/Scripts/ML/ (케이스 1: asmdef 없음 / 케이스 2: ML.asmdef 포함)
- Assets/Editor/ML/ (케이스 1: asmdef 없음 / 케이스 2: ML.Editor.asmdef 포함)
- Assets/ML-Models/
- ml-training/ (Python venv 포함, ~530MB)
- AGENTS.md ML-Agents 섹션 추가

다음 단계: /ml-setting 실행하여 Agent 설정 시작
```

## 에러 처리

| 상황 | 대응 |
|------|------|
| Unity 프로젝트가 아님 | `Assets/`, `ProjectSettings/` 없음 → 작업 중단, 사용자에게 알림 |
| uv 미설치 | 설치 명령 안내 (`brew install uv` 또는 공식 설치 스크립트) |
| Python 3.10 없음 | `uv python install 3.10` 안내 |
| ML-Agents 패키지 미설치 | UPM 설치 가이드 출력 후 사용자 결정 대기 |
| `uv sync` 실패 | 에러 로그 분석. macOS Apple Silicon에서 grpcio 빌드 실패 시 `pyproject.toml`의 override 확인 |
| `ml-training/` 이미 존재 | 사용자에게 덮어쓰기 여부 확인 |

## 멱등성

이 스킬은 재실행해도 안전해야 한다:

| 항목 | 멱등 처리 |
|------|----------|
| 폴더 (`mkdir -p`) | 자동 멱등, 추가 처리 불필요 |
| `ML.asmdef` | 케이스 2에서만 생성. 존재 시 건너뜀, references에 `Unity.ML-Agents` 있는지 검증 |
| `pyproject.toml` | 존재 시 내용 비교 → 다르면 사용자 확인 후 갱신 |
| `.python-version`, `.gitignore`, `README.md` | 존재 시 건너뜀 (사용자 커스터마이즈 보존) |
| `uv sync` | 의존성이 이미 충족되면 즉시 종료 (안전) |
| `AGENTS.md` ML-Agents 섹션 | `grep "## ML-Agents 강화학습 환경"` 검색 → 있으면 갱신, 없으면 추가 |
| 루트 `.gitignore` ML 항목 | `grep "ml-training/.venv"` 검색 → 없을 때만 추가 |

### 재실행 시 출력 예시

```
ℹ️ 이미 초기화된 항목:
  - Assets/Scripts/ML/ (asmdef 없음 — Assembly-CSharp 방식)
  - ml-training/.venv (531MB, mlagents==1.1.0)
  - AGENTS.md ML-Agents 섹션

✅ 신규 작업 없음. 환경 정상.
```
