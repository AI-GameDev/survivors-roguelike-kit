# v19 — 생존 중심 reward 재설계

## 한 줄

v18 에서 observation 결손은 고쳤지만(`FindObjectsByType<Exp>`), 600k smoke2 가 *kill farming + 방향 무관 이동보너스 긁기* 로 mean reward 만 부풀리고 **생존을 학습 못함**(dur 42.8s, clear 0%). reward surface 를 생존 중심으로 재설계하고 from-scratch 재학습.

목표 정책: **(B) 잘 살고 잘 클리어하는 agent** (XP 적극 추종 아님 — 사용자 확정). 선행 트랙은 [v18-xp-shaping.md](v18-xp-shaping.md).

## 환경

- 브랜치: `v18-xp-shaping` 유지 (실험 라벨만 v19). 실험 라벨 v19 = run-id / onnx / 본 노트 네임스페이스.
- 기준 커밋: `c41fed0` (v18 C6) 에서 reward 코드만 변경 후 분기 없이 이어 커밋.

## 학습 환경 freeze (이 값들 위에서 학습됨, 변경 시 v19 무효)

v18 과 동일 (reward 외 환경 변경 없음):

| 항목 | 값 | 파일 |
|---|---|---|
| ExpConfig.experienceIncrement | 21 | `Assets/RGame/RoguelikeKit/ScriptableObjects/Exp/...` |
| Ghost\*.asset Count (7 variants) | 15 | `Assets/RGame/RoguelikeKit/ScriptableObjects/Stage/SpawnSet/Ghost*` |
| Gear / Gear 1 | 원본 (변경 없음) | 동일 폴더 |
| SurvivorFighterAgent MaxStep | 75000 (= 240Hz × 312s) | `Assets/Scripts/ML/SurvivorFighterAgent.cs` |
| ML-Agents Academy fixedDeltaTime | 1/240 (inference quirk) | runtime |
| Exp gem observation | `FindObjectsByType<Exp>` + 5-decision 캐시 (v18 fix 유지) | `SurvivorFighterAgent.cs` |

## v18 → v19 reward 변경 (적용 완료, `SurvivorFighterAgent.cs`)

| 상수 / 항목 | v18 | v19 | 이유 |
|---|---|---|---|
| `MOVEMENT_BONUS_MAX` | 0.0008 | **0.0002** | 1순위 진범. 312s×240Hz×0.0008 = 최대 +60 누적 = 사실상 최대 reward 원천 → "그냥 돌아다니기"가 최적전략. clear 0% 인데 mean reward 양수였던 이유. 1/4 로 축소. |
| `KILL_REWARD` | 0.5 | **0.15** | kills 20×0.5=+10 이 DEATH −3 의 3배 → "죽이다 죽기" 유도. kill farming 약화. |
| survival milestone | 없음 | **신설** | 30/60/90/120/180/240s 누적생존 첫 통과 시 +0.3/+0.5/+1.0/+1.5/+2.0/+2.5 단발. 90s wall 통과를 직접 보상하는 dense 생존 신호. |
| `CLEAR_REWARD` | 2.0 | **5.0** | 312s 완주 = 최종 목표 가치 ↑. |
| HP_LOSS / XP_GAIN / THREAT / COMBAT / STOP | — | **유지** | survival 보상 효과 먼저 분리 측정 (Codex 권고). |

구현: milestone 은 `_nextMilestoneIdx` 커서로 각 임계 1회만 발화. 누적생존 sec = `(StepCount − _episodeStartStep) / 240`. `OnEpisodeBegin` 에서 커서 0 리셋, `OnActionReceived` 에서 `CheckSurvivalMilestones()` 호출. reward surface 완전 변경이므로 **from-scratch 재학습 필수** (`initialize_from` 금지).

## 판정 기준 (600k smoke 측정 시 — v18 과 다름)

**mean reward 보지 말 것** (이동/kill 누적으로 부풀려짐). 대신:

| 지표 | PlayTrace 키 | v18_smoke2 현재 | 목표 방향 |
|---|---|---|---|
| 평균/중앙 생존 시간 | `episode.duration_sec` | 42.8s / median 34.9s | ↑ |
| 90s wall 통과 수 | `episode.duration_sec` max > 90 인 판 수 | 0 | > 0 |
| clear_rate | `episode.cause == "timeout"` 비율 | 0% | > 0% |

이 셋이 개선되면 1.5M 본 학습, 아니면 milestone 수치 / MOVEMENT 축소폭 재조정.

## 학습 재현 체크리스트

학습 시작 전:

- [ ] `git status` clean, branch = `v18-xp-shaping`
- [ ] reward 변경 4건 + milestone 코드 빌드 통과 (Editor 컴파일 에러 0)
- [ ] `Tools/ML/Restore Training Mode` 실행 (`_useInference=0`, `_inferenceModel=null`)
- [ ] freeze 자산 확인 (Exp increment=21, Ghost Count=15, MaxStep=75000)
- [ ] v17/v18 onnx + `ml-training/results/` 보존 (덮어쓰기 방지)
- [ ] v19 smoke yaml 작성 + `run_id: survivor_fighter_v19_smoke` (initialize_from 없음)

학습 실행 (cmux pane, `.venv` 활성):

```bash
cd ml-training && source .venv/bin/activate
mlagents-learn configs/<v19_smoke yaml> \
  --run-id=survivor_fighter_v19_smoke \
  --timeout-wait 180 --time-scale 20
```

trainer 먼저 → ~12s 후 `unity-cli editor play`. 학습 중엔 logger skip (Academy.IsCommunicatorOn) → 진척은 pane mean reward + ckpt 로 추적.

inference 측정 절차: `results/<run>/SurvivorFighterAgent.onnx` → `Assets/ML-Models/` cp → `AssetDatabase.ImportAsset(path, ForceUpdate)` → 씬 `_useInference=true` + 모델 주입 → `editor play` → PlayTrace 새 session(version=onnx명) 누적. 끝나면 `Tools/ML/Restore Training Mode`.

## 결과 (학습 후 누적)

(v19 smoke 학습/측정 후 갱신)

## 변경 사유 (smoke 결과로 설계 바꾼 경우)

- **2026-05-29**: v18 smoke2 재검토에서 reward 구조 문제 확정 (생존 미학습, kill farming + 이동보너스 게이밍) → reward 5묶음 재설계로 v19 시작. 버전 명명/브랜치 사용자 확정 (라벨 v19, 브랜치 v18-xp-shaping 유지).

## 결론

(v19 학습 완료 후 갱신)
