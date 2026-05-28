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

### 선행 검증 (v17_best, inference, 2 sessions)

`agent.xp_in_range` (12m 안 XP gem 존재 여부, 1초/0.25초 폴링):

| session | total | ones (in_range=1) | 비율 |
|---|---|---|---|
| 20260528_001 (1초 폴링, 92s) | 92 (cos 0건) | 0 | **0%** |
| 20260528_002 (0.25초 폴링, 183s) | 766 | 0 | **0%** |

**Sanity check (Unity reflection)**:
- player 주변 12m 안 active Exp gem 3개 (3.7m / 5.3m / 10m) 존재 확인됨
- 하지만 `Physics2D.OverlapCircleAll(pos, 12f)` 결과에 안 잡힘
- **원인 확정**: Exp40_Pooled prefab 에 Collider2D 가 0개 (self + child 모두). `Physics2D.OverlapCircleAll` 로는 영원히 0건 반환
- → `SurvivorFighterAgent.CollectObservations` 의 XP 6 floats 가 학습 내내 패딩값 `(1,0,0,1,0,0)`

**판정**: "XP 무관심" 은 RL 학습 실패가 아니라 **observation 정보 결손**. v17 의 XP 픽업은 적 쫓다 1.5m magnet range 우연히 진입한 random walk 결과.

→ 가설 1번 (sparse pickup signal) / 2번 (obs range 한계) / 3번 (MOVEMENT_BONUS noise) 모두 *검증 불가* — XP signal 자체가 0이었으니까.

### v18 smoke 1차 (200k step, obs fix 단독)

**학습 진행 표 (5000 step summary)**:

| step | mean reward | std | step/sec |
|---|---|---|---|
| 50k | -4.617 | 1.49 | 56 |
| 100k | -3.432 | 1.01 | 34 |
| 135k | -2.475 (peak1) | 0.84 | 25 |
| 160k | -0.890 (peak2) | — | 22 |
| 180k | -1.406 | — | 21 |
| 200k | **-0.121** (final, all-time best) | — | 18 |

학습 곡선 패턴: peak -5 ~ -2.5 진동 + 마지막 50k 에서 -1 ~ -0.1 수렴.

**Inference 측정 (v18_smoke onnx, 4 episodes, ~26초/ep)**:

| 지표 | v17 baseline | v18_smoke 200k |
|---|---|---|
| `xp_in_range` 비율 | **0%** (0/766) | **66.4%** (267/402) |
| `nearest_xp_distance` mean | (관측 불가) | **6.22m**, median 5.74m |
| `xp_align_cos` mean | — | -0.005 (≈ random walk) |
| cos positive% (전체) | — | 49.2% |
| cos by safe (0 적) | — | -0.033, pos%=46% |
| cos by mid (2 적) | — | -0.502, pos%=14% |
| cos by danger (≥3 적) | — | **+0.032**, pos%=52.8% |
| episode dur mean | ~83~180s | **26s** (24~30s) |
| final_level mean | 5+ 일반 | **1.25** (1,1,1,2) |
| total_kills mean | 다수 | 3.25 |
| episode causes | mixed | **death × 4 (전부)** |

**해석**:
- ✅ obs fix 압도적 효과 (in_range 0 → 66.4%, gem 평균 거리 6.22m)
- ⚠️ cos ≈ 0 = XP 방향 *적극 추종 안 함* (mean reward -0.12 까지 학습됐는데 XP 방향성은 random walk)
- ❌ episode dur 26초 (v17 의 ~1/4) — XP obs 받았지만 행동에 연결 못함, 또는 새 관측 분포가 v17 회피 학습 패턴 흔들어 사망률 상승

### 학습 속도 둔화 발견 (사용자 관찰 + 데이터 검증)

`FindObjectsByType<Exp>` 도입 후 학습 속도 단조 감소:

| step | step/sec |
|---|---|
| 10k | 132 (baseline) |
| 50k | 56 (×0.42) |
| 100k | 34 (×0.26) |
| 150k | 22 (×0.17) |
| 200k | **18** (×0.14 — 7배 둔화) |

**v17 학습 평균: ~140 step/sec → v18 학습 평균: ~32 step/sec (4~5배 느림)**.

원인 후보 (우선순위):
1. `FindObjectsByType<Exp>` Unity 전역 GameObject traverse 비용 (active+inactive pool)
2. 학습 후반 episode 길이 증가 → 한 episode 안 active object 누적 → 그 episode 후반 step 더 느림
3. **단**: `_decisionPeriod = 5` 라 agent `CollectObservations` 는 이미 48Hz. 캐시 효과 제한적일 수 있음 — 실제 병목은 `ApplyProximityShaping`의 매-step (240Hz) enemy query 일 가능성

### 다음 단계 결정 (Codex cross-check 동의)

200k mean reward 학습됐는데 `cos ≈ 0` 이라는 사실은 "obs fix 만으론 XP-seeking 신호 약함" 을 시사 — *학습 부족 단독* 가설보다 강함.

**Plan**:
1. Agent obs FindObjectsByType + (선택) ApplyProximityShaping 의 enemy query — n-step (5) 캐시 + null/disabled 필터
2. 600k smoke 추가 학습 (캐시 적용)
3. 학습 중 10k/25k/50k/100k 시점 step/sec 추세 추적 — 후반 둔화 완화 여부 평가
4. 학습 후 inference 10~20 episode 측정
5. 판정 (Codex 권고):
   - cos / xp_in_range / pickup rate / final_level / episode dur 다섯 지표 함께
   - **(a)** 다섯 지표 다 향상 → v18 1.5M 본 학습 진행
   - **(b)** cos 좋지만 dur 짧으면 → "gem 만 쫓아 자살" 정책 → v19 (회피 시 같이 보강)
   - **(c)** 거의 다 정체 → v19 (potential-based shaping 추가)

## 변경 사유 (smoke 결과로 설계 바꾼 경우)

- **2026-05-28**: 선행 검증에서 *observation 결손 버그* 발견 → reward shaping 가설 검증 미루고 obs fix 단독 (V18a) 로 전환. Codex/사용자 동의.
- **2026-05-28**: 200k smoke 결과 `mean reward -0.12 학습됐는데 cos ≈ 0` → "obs fix 만으론 XP-seeking 신호 약함" 가설 강화. 1.5M 직진 대신 *캐시 + 600k 추가 smoke* 로 결정 데이터 더 모으기.
- **2026-05-28**: 학습 속도 단조 둔화 (132 → 18 step/sec) 발견 → FindObjectsByType 캐시 + ApplyProximityShaping 캐시 검토.

## 결론

(600k smoke + 측정 + 판정 완료 후 갱신)
