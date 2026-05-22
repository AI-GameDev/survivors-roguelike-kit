# bal-converge plan — 2026-05-22 16:48

## 1. Context

### 이전 리포트 이력 (최근 1개)

| 시각 | REASON | KPI | iters | 만진 knob |
|---|---|---|---|---|
| 2026-05-22 16:19 | max_iter (smoke) | `Dur 180~240 AND Lvl>=5` | 1 / 2 | enemy_damage(no-op), spawn_rate×2, level_up_xp_increment, player_max_hp |

(`_sample_converge_20260522-144530_pass.html` 는 샘플 파일이라 history 에서 제외)

### 관찰 패턴

- pass 0회 / smoke 1회. 표본이 N=2 라 시그널보다 분산이 컸음 (iter 1 play_no=1 이 32s 단명으로 summary 누락 → 사실상 N=1).
- baseline 자체가 60s structural cutoff 에 갇혀 있음 (σ=0.1s, σ/μ=0.2%). KPI 라인(Dur≥180)까지 거리가 stat knob 만으로는 좁히기 어렵다.
- `enemy_damage` 변경이 정수 round-trip(3×0.85=2.55→3)으로 흡수 — knob 자체가 효과 없는 라운드를 만듦.

### Baseline 후보 (PlayTrace 최근 7일, completed plays >= 2)

| session_id | N | mean Dur | mean Lvl | end-cause |
|---|---|---|---|---|
| 20260522_005 | 4 | 62.0±0.1s | 4.0 | death(100%) ← 16:19 converge 의 baseline |
| 20260522_001 | 4 | 63.7±0.8s | 3.0 | death(100%) |
| 20260522_008 | 2 | 62.9±1.0s | 2.5 | death(100%) |
| 20260522_006 | 2 | 62.1±0.2s | 4.5 | death(100%) |

> `episode.cause=death` 는 cause 자체. 사망 주체는 `episode.last_hit_attacker` (이전 리포트에서 Gear 66%, Slime 17%) 에서 확인.

## 2. Direction

### KPI

`Dur 180~240 AND Lvl>=5`

이유: config default 그대로 유지. 이전 사이클이 max_iter(smoke)로 끝났을 뿐 KPI 자체가 부적절했다는 신호는 없음. 다만 baseline Dur=62s 에서 180s 로 가는 거리는 매우 멀어 — `max_iter=3` 안에 distance 가 좁혀지지 않으면 KPI 를 더 부드럽게(Dur≥120 등) 재조정하는 후속 plan 필요.

### Baseline

재사용: `20260522_005` (N=4, mean Dur 62.0±0.1s, Lvl 4.0).  
사이클 시작 시 bal-converge 가 24h 이내 baseline 자동 재사용 — 명시적 인자 없이 동작.

### Knob whitelist

허용 (4개):
- **enemy_hp** — 적 체력. low_kill_rate 트리거. 정수 round-trip 위험 적음 (HP 값이 일반적으로 충분히 큼).
- **spawn_rate** — 스폰 빈도 (BaseRatePerSecond). structural_duration_cutoff 트리거. 60s cutoff 를 뚫을 가장 유력한 stat knob.
- **level_up_xp_increment** — 레벨업 XP 증가량. level_cap_low 트리거. Lvl≥5 KPI 직격.
- **player_max_hp** — 플레이어 체력. high_damage_taken_variance / rapid_death 트리거.

제외 (2개):
- **enemy_damage** — Gear/Slime base damage 가 작은 정수(3)라 ±15% adjust 가 round-trip 으로 흡수 (이전 리포트 실증). adjust_default 또는 min-step 보강 전까지 사이클 자원 낭비. → bal-apply.json 에서 `enemy_damage` knob 객체를 임시 제외하거나 trigger 매핑을 비워야 효과 있음. **plan 만으로는 자동 제외 안 됨 — 사용자가 사이클 시작 전 수동으로 .claude/bal-apply.json 의 enemy_damage 항목을 주석/삭제하거나, 한 사이클 결과 보고 판단**.
- **spawn_intensity_curve** — AnimationCurve 수동 편집 대상. `action: open_in_unity` 라 auto 안 됨 (config 에 이미 반영됨).

### 예산

N=5, max_iter=3. 예상 wall-time ≈ 3 × (5 × 10m + 30s apply) ≈ 153m (≈ 2.5h). per-play timeout 이 보통 5m 안쪽으로 끝나므로 실제는 ~60~80m 수준.

## 3. Hypothesis priority

1. **structural cutoff 가 60s 부근에서 Ghost burst spawn 때문일 가능성 (project memory)** — spawn_rate 단독 감소로 cutoff 가 깨지는지 1차 검증. 안 깨지면 spawn_intensity_curve 수동 편집이 필수.
2. **Lvl 부족은 XP 증가량 문제일 가능성** — level_up_xp_increment 감소로 Lvl 평균이 baseline 4.0 에서 5+ 로 이동하는지.
3. **player_max_hp +10% 가 분산 감소 + Dur 연장에 기여** — 이전 사이클에서 적용은 됐지만 N=2 라 검증 불가. 본격 N=5 로 재검증.

## 4. Traps (주의)

- **`enemy_damage` 정수 round-trip 흡수** — 자산 값이 작은 정수면 ±15% 가 반올림으로 흡수돼 no-op. whitelist 에 두려면 adjust_default 를 -0.4 수준으로 키우거나 min absolute step 도입 필요. 본 plan 에서는 통째로 제외 권장.
- **Ghost burst spawn 60s structural cutoff** — Ghost.asset 변종 7개의 t=60 burst 가 dominant. Dur 늘리는 게 목표면 stat 만 만져선 못 뚫음. `spawn_rate` 만으로 안 풀리면 다음 plan 에서 `spawn_intensity_curve` 수동 편집 사이클 (사용자가 Unity Editor 에서 직접) 별도로 끼워야 한다.
- **smoke 모드의 min_runs_for_analysis=3 자연 abort** — 이번 plan 은 N=5 라 안전. 다만 per-play 가 너무 짧게 죽으면 (≤32s 등) summary 키 누락으로 유효 N 이 떨어질 수 있음. timeout 충분히 잡고 시작.

## 5. Recommended command

```
/bal-converge kpi="Dur 180~240 AND Lvl>=5" N=5 max_iter=3
```

> Baseline 재사용은 bal-converge 가 24h 내 session 을 자동 감지. 명시적 `baseline=` 인자 불필요.  
> Knob whitelist (enemy_damage 제외) 는 `/bal-converge` 인자로 지정 불가 — 사이클 시작 **전에** `.claude/bal-apply.json` 의 `enemy_damage` knob 객체를 일시적으로 주석 처리하거나 `triggers: []` 로 비워둬야 효과. 그대로 두면 bal-apply 가 매 사이클 enemy_damage 를 다시 시도(no-op)함 — 시간 낭비는 아니지만 1 slot 자리 차지.

## 6. Post-run checklist

- [ ] HTML 리포트가 `playbalance/reports/converge_<ts>_<reason>.html` 에 생성됐는지
- [ ] REASON 이 pass 가 아니면: distance 가 monotonic 하게 줄어드는지 vs 진동인지 확인 → 진동이면 knob 간 충돌, 단조 미달이면 KPI 가 baseline 에서 너무 멀거나 적절한 knob 부재
- [ ] 60s cutoff 가 안 깨지면 다음 plan 에서 `spawn_intensity_curve` 수동 편집 사이클 별도 설계
- [ ] git diff 로 만진 자산을 확인 후 commit / rollback 결정 (특히 PlayerStat.asset, ExpConfig.asset)
