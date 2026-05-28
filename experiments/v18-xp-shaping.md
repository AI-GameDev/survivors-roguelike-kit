# v18 — XP potential-based distance shaping

## 한 줄

v17 모델이 XP gem 추종 의도를 보이지 않음. potential-based distance shaping + MOVEMENT_BONUS 정리 + observation 확장으로 v18 from-scratch 재학습.

## 학습 환경 freeze (이 값들 위에서 학습됨, 변경 시 v18 무효)

기준 브랜치: `balance/v17-tuning` @ `3775fb4`.

| 항목 | 값 | 파일 |
|---|---|---|
| ExpConfig.experienceIncrement | 21 | `Assets/RGame/RoguelikeKit/ScriptableObjects/Exp/...` |
| Ghost\*.asset Count (7 variants) | 15 | `Assets/RGame/RoguelikeKit/ScriptableObjects/Stage/SpawnSet/Ghost*` |
| Gear / Gear 1 | 원본 (변경 없음) | 동일 폴더 |
| SurvivorFighterAgent MaxStep | 75000 (= 240Hz × 312s) | `Assets/Scripts/ML/SurvivorFighterAgent.cs` |
| ML-Agents Academy fixedDeltaTime | 1/240 (inference quirk) | runtime |

학습 도중 위 값 변경 시 → 재학습 필요. 밸런싱 lever 작업은 v18 학습 완료 후 별도 사이클.

## 학습 재현 체크리스트

학습 시작 전 확인:

- [ ] `git status` clean, branch = `v18-xp-shaping`
- [ ] `Tools/ML/Restore Training Mode` 실행 (Inference 모드 잔재 제거)
- [ ] `Tools/ML/Open MLInitialization Scene` (백업 씬 로드 방지)
- [ ] `MLAgentBootstrap` 인스펙터: `_useInference=0`, `_inferenceModel=null`
- [ ] `Assets/ML-Models/SurvivorFighterAgent_v17_best.onnx` 존재 (덮어쓰기 방지)
- [ ] `ml-training/results/survivor_fighter_v17/` 폴더 존재 (보존)
- [ ] PlayTrace 서버 health OK
- [ ] `ml-training/configs/survivor_fighter.yaml` 의 `run_id: survivor_fighter_v18`

학습 명령:

```bash
cd ml-training && source .venv/bin/activate
mlagents-learn configs/survivor_fighter.yaml \
  --run-id=survivor_fighter_v18 \
  --timeout-wait 180 --time-scale 20
```

학습 완료 후:

- [ ] `Assets/ML-Models/SurvivorFighterAgent_v18_best.onnx`, `v18_final.onnx` 로 복사
- [ ] `~/ml-archives/survivors-roguelike-kit/v18/` 에 onnx + checkpoint 백업
- [ ] `artifacts/ml/v18/manifest.json` 생성 (sha256 + source_commit + run_id)
- [ ] 본 문서 결과 표 갱신 + C3 commit

## v17 → v18 변경 요약 (계획)

| 항목 | v17 | v18 (계획) | 이유 |
|---|---|---|---|
| XP distance shaping | 없음 | `gamma * Phi(s') - Phi(s)`, `Phi=-dist_to_nearest_xp`, scale ±0.0005~±0.001/step | sparse pickup 신호 보강 |
| `MOVEMENT_BONUS_MAX` | +0.0008/step | 0 또는 +0.0002/step (1/4) | XP-방향과 무관한 이동 보상이 XP signal 흐림 |
| `XP_OBS_RANGE` | 12m | 20m | 시야 밖 ↔ 위험 회피 구분 |
| `TOP_XP_COUNT` | 2 | 4 | 정책 판별력 |
| 추가 obs | — | 12m 이내 XP 개수 1 float | 밀도 정보 |
| `VECTOR_OBS_SIZE` | 39 | 50 (= 39 + 2×3 + 1 + 다른 가능 변경) | 위 변경 반영 |
| Trainer (YAML) | `time_horizon=256` | 유지 (smoke 결과로 재평가) | 일단 보수적 |

위 표는 **선행 검증 결과 보고 강도 조정**. 검증에서 "이미 부분 추종" 확인 시 약하게, "완전 무관심" 확인 시 강하게.

## 선행 검증 (단계 0) — v17 XP-방향 상관관계 측정

목적: v17이 정말 XP를 무관심하게 무시하는지 데이터 확정.

추가 logger 키 (`Assets/Scripts/ML/MLBalanceLogger.cs`):

| 키 | 의미 |
|---|---|
| `agent.xp_align_cos` | nearest XP gem 방향 vs 실제 이동 방향 코사인 (−1~+1) |
| `agent.nearest_xp_distance` | nearest XP gem 까지 거리 (m) |
| `agent.nearby_enemy_count` | PROXIMITY_RANGE 5m 안 적 수 |

폴링 주기: 1초. nearest XP 없거나 velocity ≈ 0 시 skip.

분석 그룹:

| 그룹 | 조건 | 해석 |
|---|---|---|
| 안전 상황 | `nearby_enemy_count == 0` | XP 추종 강도 (방해 요소 없음) |
| 위험 상황 | `nearby_enemy_count >= 3` | 위험 회피 우선도 |

판정 트리:

- 안전 시 `cos ≈ 0` → **무관심 확정** → v18 shaping 강하게 (±0.001/step)
- 안전 시 `cos > 0.3`, 위험 시 `cos < 0` → **부분 추종 + 위험 판단 작동** → shaping 약하게 (±0.0003/step) + MOVEMENT_BONUS 축소만으로 충분 가능
- 안전 시 `cos > 0.5` → 추종 자체는 OK, **못 먹는** 게 문제 → 자석 stat / 픽업 메커니즘 조사로 방향 전환

## 결과 (학습 후 누적)

### 선행 검증 (v17)

| 그룹 | n | mean(cos) | median(cos) | mean(dist) |
|---|---|---|---|---|
| 안전 (enemy=0) | — | — | — | — |
| 중간 (1~2) | — | — | — | — |
| 위험 (≥3) | — | — | — | — |

판정: TBD

### Smoke ablation (100k step)

| run | XP shaping scale | MOVEMENT_BONUS | dur mean | level mean | XP align cos mean | 비고 |
|---|---|---|---|---|---|---|
| — | — | — | — | — | — | — |

### v18 본 학습 (1.5M step)

| step | mean reward | dur mean | level mean | XP align cos (안전) | 비고 |
|---|---|---|---|---|---|
| — | — | — | — | — | — |

## 변경 사유 (smoke 결과로 설계 바꾼 경우)

(없음)

## 결론

(학습 완료 후)
