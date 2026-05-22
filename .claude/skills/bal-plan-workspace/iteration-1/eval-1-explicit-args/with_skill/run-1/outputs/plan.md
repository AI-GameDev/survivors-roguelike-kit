# bal-converge plan — 2026-05-22 16:48

## 1. Context

### 이전 리포트 이력
이전 converge 리포트가 없습니다 (`playbalance/reports/converge_*.html` 비어있음). 첫 plan 작성.

### 관찰 패턴
- 첫 사이클이라 누적 패턴 없음.
- 메모리 함정 기반 사전 주의는 4번 섹션 참조.

### Baseline 후보 (PlayTrace 최근 7일, project=survivors-roguelike-kit)
| session_id      | N | mean Dur (s) | std | 비고 |
|-----------------|---|--------------|-----|------|
| 20260522_005    | 6 | 62.0         | 0.1 | σ/μ=0.2%, structural cutoff 의심 |
| 20260522_008    | 2 | 62.9         | 1.0 | 표본 부족 |
| 20260522_010    | 1 | 61.9         | 0.0 | 표본 부족 |

→ 가장 견고한 후보는 **20260522_005** (N=6). 모든 baseline 이 ~62s 에서 거의 점에 가깝게 묶여 있음 — Ghost 60s burst structural cutoff 와 일치.

## 2. Direction

### KPI
`Dur>=120`

이유: 사용자가 인자로 명시 (`kpi="Dur>=120"`). 현재 baseline ~62s 에서 약 2배 늘리는 목표. config default(`Dur 180~240 AND Lvl>=5`) 보다 완화된 단일 metric 게이트로 smoke 파이프라인 검증에 적합.

### Baseline
재사용: `20260522_005` (N=6, mean Dur=62.0s). bal-converge 가 직전 24h 세션을 자체 재사용하므로 별도 인자 불필요.

### Knob whitelist
허용 (5개):
- `enemy_damage` — 적 공격력 (Enemy SO Attack)
- `enemy_hp` — 적 체력 (Enemy SO HP)
- `spawn_rate` — 초당 적 등장 빈도 (SpawnSet BaseRatePerSecond)
- `level_up_xp_increment` — 레벨업 XP 증가량
- `player_max_hp` — 플레이어 최대 체력

제외 (이유):
- `spawn_intensity_curve` — AnimationCurve 수동 편집 대상 (auto 적용 불가, bal-apply.json 명시)

> whitelist 강제 제외는 `.claude/bal-apply.json` 수동 편집으로만 가능. 현재 config 그대로 두고 사이클 돌리면 위 5개가 후보가 됨.

### 예산
mode=smoke → **N=2, max_iter=1**. 예상 wall-time ≈ 2 × 10m + apply 30s ≈ **20~25분**.

> Smoke 는 파이프라인 동작 확인용. bal-apply `min_runs_for_analysis=3` 때문에 iter 2 진입 시 자연 abort — smoke 는 본질적으로 1 iter 한정 (memory: bal-converge smoke 제약).

## 3. Hypothesis priority

1. **Spawn timing 가설**: baseline σ/μ=0.2% 는 stat 흡수가 아니라 **structural cutoff** 시그널. Ghost 7 변종이 t=60 에서 burst spawn → 어떤 stat knob 도 60s 벽을 못 뚫을 가능성. smoke 1 iter 에서 stat-only adjust 가 시그널 없이 끝나면 spawn timing(curve) 으로 가야 함.
2. 보조: `enemy_damage` 정수 round-trip 흡수 위험 — adjust 가 ±15% 인데 자산 값이 작은 정수면 no-op. iter 0 의 변경 자산 diff 를 반드시 확인.

## 4. Traps (주의)

- **enemy_damage 정수 round-trip 흡수**: 자산 값이 1~3 같은 작은 정수면 ±15% 가 반올림으로 흡수돼 no-op. iter 결과 KPI 변화 없으면 git diff 가 실제로 값을 바꿨는지 1차 확인.
- **Ghost burst spawn 60s structural cutoff**: baseline ~62s 의 분산이 0.1s 수준이라는 게 stat 문제가 아니라 spawn timing 문제라는 강한 시그널. Dur≥120 목표는 stat 만으로는 안 뚫릴 가능성 큼. smoke 결과 보고 `spawn_intensity_curve` 수동 편집 단계 진입 결정.

## 5. Recommended command

```
/bal-converge kpi="Dur>=120" N=2 max_iter=1
```

> Baseline 별도 인자 없음 — bal-converge 가 직전 24h session 을 자동 재사용 (20260522_005 또는 _008 후보).

## 6. Post-run checklist

- [ ] HTML 리포트가 `playbalance/reports/converge_<ts>_<reason>.html` 에 생성됐는지
- [ ] REASON 이 pass 가 아니면 baseline 62s → 120s 목표가 stat-only 로 가능했는지 회고
- [ ] git diff 로 만진 자산을 확인 — enemy_damage 정수 흡수 여부 1차 점검
- [ ] smoke 결과가 시그널 없음(no-op) 이면 본격 사이클 가기 전에 spawn_intensity_curve 수동 편집 단계로 전환 검토
