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

### 600k smoke 학습 (run_id `survivor_fighter_v19_smoke`, from-scratch)

- 완료: 2026-05-29 09:52. 총 ~12585s (~3h30m), Step 600000 도달 → onnx export → 정상 종료. 에러 0.
- final onnx: `ml-training/results/.../SurvivorFighterAgent.onnx` (=600021 ckpt) → `Assets/ML-Models/SurvivorFighterAgent_v19_smoke.onnx` 백업.
- 학습 막판 mean reward 출렁임(123→0.016) + "No episode completed" 다수 = milestone 간헐 발화 + 에피소드 길어진 흔적. **판정 지표 아님** — inference 측정으로만 판정.

### Inference 측정 (v19_smoke onnx, 15 episodes, PlayTrace session `20260529_001`, ~25분)

| Play | Dur(s) | Cause | Lvl | Kills | DmgTaken | EXP |
|---|---|---|---|---|---|---|
| 1 | 85.3 | death | 8 | 60 | 103 | 219 |
| 2 | 76.2 | death | 5 | 37 | 101 | 74 |
| 3 | 127.8 | death | 6 | 37 | 710 | 130 |
| 4 | 75.8 | death | 3 | 36 | 100 | 139 |
| 5 | 70.1 | death | 5 | 35 | 100 | 114 |
| 6 | 64.4 | death | 5 | 25 | 100 | −126 |
| 7 | 79.5 | death | 5 | 36 | 120 | −6 |
| 8 | 86.5 | death | 7 | 72 | 100 | 125 |
| 9 | 26.7 | death | 2 | 10 | 100 | 60 |
| **10** | **343.9** | **timeout (clear)** | **30** | **1739** | 488 | −586 |
| 11 | 93.8 | death | 6 | 76 | 101 | 130 |
| 12 | 110.5 | death | 12 | 132 | 432 | 329 |
| 13 | 74.8 | death | 5 | 31 | 100 | 114 |
| 14 | 128.1 | death | 17 | 218 | 551 | 96 |
| 15 | 84.9 | death | 7 | 45 | 103 | 49 |

**판정 (vs v18_smoke2 baseline):**

| 지표 | v18_smoke2 | v19_smoke | 변화 |
|---|---|---|---|
| 평균 생존 | 42.8s | **101.9s** | +138% ✅ |
| median 생존 | 34.9s | **84.9s** | +143% ✅ |
| 90s wall 통과 | 0/15 | **5/15 (33%)** | ✅ 최초 |
| clear_rate (timeout) | 0% | **7% (1/15)** | ✅ 최초 clear |

- **셋 다 큰 폭 개선 → reward 재설계 성공.** milestone + MOVEMENT 축소 + KILL 약화가 의도대로 작동.
- play #10 이 90s wall 통과 후 343.9s/Lvl 30/1739 kills 로 snowball 완주 → [bimodal kill threshold](../) 패턴 그대로: wall 넘으면 무한 snowball. v19 는 wall 통과율을 0→33% 로 끌어올림.
- 비고: 일부 play `final_exp` 음수(#6 −126, #10 −586). 게임 EXP 누적 메트릭 quirk 로 추정, 판정 지표 아님 — 후속 점검 항목.

## 변경 사유 (smoke 결과로 설계 바꾼 경우)

- **2026-05-29**: v18 smoke2 재검토에서 reward 구조 문제 확정 (생존 미학습, kill farming + 이동보너스 게이밍) → reward 5묶음 재설계로 v19 시작. 버전 명명/브랜치 사용자 확정 (라벨 v19, 브랜치 v18-xp-shaping 유지).

## 결론

**v19 reward 재설계 = 성공. 1.5M 본 학습으로 진행 권장.** 600k smoke 만으로 v18_smoke2 대비 생존 +138%, 90s wall 통과 0→33%, clear 0→7% 달성 — 셋 다 핸드오프 판정 기준 통과. 600k 에서 이미 신호가 뚜렷하므로 1.5M 에서 wall 통과율/clear_rate 추가 상승 기대.

- 다음: run_id `survivor_fighter_v19`, max_steps 1.5M, from-scratch (`initialize_from` 금지), reward·환경 freeze 유지.
- smoke 모델 그대로 reward 미세조정 불필요 — 현 설계로 본 학습 직행.
