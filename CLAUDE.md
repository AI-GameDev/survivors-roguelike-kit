# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Unity 2D Vampire Survivors–style roguelike kit. Originally a Unity Asset Store template, now MIT-licensed open source. Built on Unity **6 (6000.3.11f1)** with the **Built-in Render Pipeline**. Only third-party plugin is **DOTween**.

## How to run / build

There is no CLI build pipeline — open the project in Unity Hub with Unity 6 and press Play.

- Entry scene: `Assets/RGame/RoguelikeKit/Scenes/Initialization.unity` (this is the only scene in Build Settings, index 0).
- `Test.unity` exists alongside it for in-editor experimentation.
- Tests use `com.unity.test-framework` — run them via Unity's **Test Runner** window (Window → General → Test Runner). There is no test repo currently, just the framework dependency.
- `survivors-roguelike-kit.slnx` / `Assembly-CSharp*.csproj` are Unity-regenerated; do not hand-edit.

## Architecture

Two top-level code modules under `Assets/RGame/`:

- **`RSOFramework/`** — reusable engine-style framework: event channels (ScriptableObject events), object pooling, state machine, factories, common stats, FlexBlackboardPro, mobile/phone helpers (joystick). Namespace: `RGame.Framework`.
- **`RoguelikeKit/`** — game-specific code under `RoguelikeKit/Scripts/`. Namespace: `RGame.RoguelikeKit`.

### Scene flow (Addressables-driven)

Scene loading is event-channel + Addressables based, **not** direct `SceneManager.LoadScene` calls.

1. `InitializationLoader` (in `Initialization.unity`) additively loads `PersistentManagers` scene, then raises a `LoadEventChannelSO` to load the Main Menu.
2. `SceneLoader` (in PersistentManagers) listens on `loadLevelChannel` / `loadMenuChannel` and orchestrates unload-current → load-target with fade + loading screen via `BoolEventChannelSO` / `FadeChannelSO` / `VoidEventChannelSO`.
3. Gameplay uses an additive **GamePlay manager scene** (`Scenes/Managers/GamePlay.unity`) layered under the active level scene (`Scenes/GameLevel/Game.unity`).

When adding a new scene, register it as a `GameSceneSO` (Addressable) and trigger transitions by raising the appropriate `LoadEventChannelSO`, not by calling `SceneManager` directly.

### Event-channel pattern

Cross-system communication is `ScriptableObject`-based event channels under `Scripts/Event/` (Buff, MapEvent, Player, SceneEvent, Skill, Stage, Time) and `RSOFramework/Event/`. Subscribe in `OnEnable`, unsubscribe in `OnDisable`. Avoid direct singletons or `FindObjectOfType` calls between systems — wire via channels assigned in the Inspector.

### Gameplay systems (`Scripts/System/`)

Each subsystem is a self-contained folder driven by ScriptableObject configs in `Scripts/SOData/` and the `ScriptableObjects/` asset folders:

- `Stage/` — procedural enemy spawn patterns
- `Map/` — weighted-tile procedural map generation
- `Skill/`, `AttributeSkill/` — active skills, passive attribute skills, evolutions
- `Buff/` — burn / freeze / slow / etc.
- `Enemy/` — enemy spawn & roster (`Game/Enemy/` has `MeleeEnemy`, `RangeEnemy`, `ChargeEnemy` deriving from `BaseEnemy`)
- `Level/`, `PowerUp/`, `Save/`, `Setting/`, `TimeStep/`, `Cheat/`

Player code lives separately in `Scripts/Game/Player/` (Movement, PickUp, Hit, Animator subfolders).

### Input

Input is split: `Assets/Scripts/Input/GameInput.cs` (root-level untracked addition) and `RoguelikeKit/Scripts/Input/`. Uses Unity's new **Input System** package (`com.unity.inputsystem`). An `InputReader` ScriptableObject is referenced by `SceneLoader` and other consumers.

### Localization & Addressables

`com.unity.localization` and `com.unity.addressables` are core dependencies. Scene/asset references use `AssetReference` + Addressables APIs (see `InitializationLoader.cs`). Localization tables live under `RoguelikeKit/LocalizationFiles/`.

## Adding new content

When extending the kit, follow the existing ScriptableObject-driven workflow rather than hard-coding:

- New character → add `CharacterSelectConfigSO` entry, prefab, and skill set
- New enemy → derive from `BaseEnemy` and create its data SO
- New skill / buff / map / stage → create the matching `*SO` asset and register in the relevant config

Refer to the GitBook docs for full how-tos: https://roofen-game.gitbook.io/roofen-game/survivors-roguelike-kit

## Notes

- Many `.meta` files appear modified in `git status` after opening the project in Unity 6 — this is a normal Unity meta-rewrite, not a code change. Don't include them in unrelated commits.
- `Assets/Scripts/` is the user's own scratch area (currently just `Input/GameInput.cs`); the shipped kit code is under `Assets/RGame/`.
- **`docs/` 전체가 `.gitignore` 에 등록되어 있다**. `docs/hand-off.md` 같은 파일은 untracked가 정상 — commit에 포함시키려고 시도하지 말 것. 로컬 메모/인계 영역.

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

### 자산 보존 규칙

**`Assets/ML-Models/*.onnx` 와 `ml-training/results/` 는 삭제·수정·이동 금지.** 학습된 모델과 학습 결과는 누적 자산이며, 새 결과는 새 `run-id`로 추가만 한다. 정리/리팩토링 작업에서도 절대 건드리지 말 것.

## PlayTrace 통합

이 프로젝트는 게임 테스트 로그 수집과 대시보드를 위해 **PlayTrace** (별도 프로젝트, `/Users/mingyukim/Documents/GitHub/018_Study-Koomin/PlayTrace`)에 의존한다. `/bal-run`, `/bal-apply`, `/bal-converge` 등 모든 밸런싱 skill이 `http://localhost:8000` 의 PlayTrace API를 사용.

- **서버는 사용자가 직접 띄움** (uvicorn). Claude는 작업 전 `curl -s -m 3 http://localhost:8000/health` 로 health 체크만 하고, 서버 시작 시도는 하지 않는다. 죽어있으면 "서버가 응답 안 합니다 — 띄워 주세요" 한 줄로 안내.
- **API/필드/예제/대시보드 사용법의 단일 진실 소스는 `PlayTrace/docs/MANUAL.md`**. POST/GET payload, `LogItem` `value_type` 분기, session 생성 규칙, 안티패턴 등 모두 여기서 1차 확인. skill 본문 코드 (bal-* SKILL.md)는 사용 패턴 가이드일 뿐이며, payload 형식 충돌 시 MANUAL.md가 우선.

## 세션 인계

**세션 시작 시 `docs/hand-off.md` 가 존재하면 다른 작업 전에 먼저 읽어 이전 세션의 상태·결정 대기 항목·미해결 가설을 파악한다.** 이 파일은 `.gitignore` 영역의 로컬 인계용 메모로, 매 세션 종료 시 다음 세션에 정말 필요한 정보가 있으면 갱신한다 (없으면 비우거나 그대로 둠 — 억지로 항목 만들지 말 것). 표준 환경 체크(git status, PlayTrace health 등)는 핸드오프에 반복하지 않고 필요할 때만 실행.

## Working Guidelines

Behavioral guidelines to reduce common LLM coding mistakes.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

### 1. Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them - don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

### 2. Simplicity First

**Minimum code that solves the problem. Nothing speculative.**

- No features beyond what was asked.
- No abstractions for single-use code.
- No "flexibility" or "configurability" that wasn't requested.
- No error handling for impossible scenarios.
- If you write 200 lines and it could be 50, rewrite it.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

### 3. Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting.
- Don't refactor things that aren't broken.
- Match existing style, even if you'd do it differently.
- If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:
- Remove imports/variables/functions that YOUR changes made unused.
- Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

### 4. Goal-Driven Execution

**Define success criteria. Loop until verified.**

Transform tasks into verifiable goals:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.
