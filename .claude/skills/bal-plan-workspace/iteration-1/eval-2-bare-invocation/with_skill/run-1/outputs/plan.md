# bal-converge plan — 2026-05-22 16:47

## 1. Context

### 이전 리포트 이력
- 2026-05-22 16:19 — REASON=max_iter, KPI="Dur 180~240 AND Lvl>=5", iters=1/2 (smoke), 만진 knob: enemy_damage(no-op), spawn_rate×2, level_up_xp_increment, player_max_hp

(이전 1개만 발견 — sample 리포트 제외. 첫 본격 plan에 해당.)

### 관찰 패턴
- 최근 1회 max_iter 종료. baseline Dur=61.98s σ=0.1s (σ/μ=0.2%) — structural cutoff 명백.
- iter 1 변경에도 Dur 변화 없음 (-0.1%), Lvl은 오히려 -18%. smoke N=2 로 분산이 시그널 압도.
- enemy_damage(3→3) 가 정수 round-trip 으로 흡수됨 — knob whitelist 후보에서 제외 권장.

### Baseline 후보 (PlayTrace 최근 7일, survivors-roguelike-kit)
| session | name | 비고 |
|---|---|---|
| `20260522_010` | SurvivorFighterAgent_v17_best run | 직전 converge iter 1 결과 (변경 자산 반영) |
| `20260522_005` | SurvivorFighterAgent_v17_best run | 직전 converge baseline (N=6, Dur=61.98s, Lvl=3.67) ← 재사용 권장 |
| `20260522_008/006/004~001` | v17_best run 시리즈 | 동일 모델, 짧은 단발 |

## 2. Direction

### KPI
`Dur 180~240 AND Lvl>=5`

이유: config default 그대로. 이전 사이클이 smoke 검증용이었기 때문에 본격 KPI 수렴을 첫 시도. baseline 거리 (Dur 62→180 / Lvl 3.7→5) 가 크므로 max_iter 여유 필요.

### Baseline
재사용: `20260522_005` (N=6, mean Dur=61.98s σ=0.1s, Lvl=3.67, end=Gear 66% + Slime 17%).
v1 bal-converge 가 24h 이내 session 을 자동 재사용한다고 SKILL.md 명시 — 별도 인자 없이 시작 가능.

### Knob whitelist
허용 (4개):
- `enemy_hp` — 적 체력 (Enemy SO의 HP)
- `spawn_rate` — 스폰 빈도 (BaseRatePerSecond) **← 60s structural cutoff 돌파의 1순위 후보**
- `level_up_xp_increment` — 레벨업 XP 증가량 (Lvl 끌어올리기)
- `player_max_hp` — 플레이어 체력 (생존 시간 확장 보조)

제외 (이유):
- `enemy_damage` — 자산 값이 정수 3. ±15% adjust 가 round-trip 흡수 → no-op (memory: enemy_damage 정수 round-trip 함정). 이번 cycle 에서는 통제 변수로 고정.
- `spawn_intensity_curve` — AnimationCurve 수동 편집 대상. bal-apply auto 범위 밖.

> **수동 작업 필요**: 위 whitelist 를 강제하려면 `.claude/bal-apply.json` 의 `enemy_damage` / `spawn_intensity_curve` 엔트리를 비활성화하거나 적용 직전 confirm 단계에서 거절. `/bal-converge` 인자만으로는 whitelist 전달 불가.

### 예산
N=5, max_iter=3. 예상 wall-time ≈ 3 × (5 × ~70s + apply 30s) ≈ 20~25분 (baseline 재사용으로 iter 0 측정 없음).

## 3. Hypothesis priority

1. **60s structural cutoff = Ghost burst spawn 가설 (project memory)** — baseline σ=0.1s 가 그 증거. `spawn_rate` 하향이 cutoff 돌파의 1순위. Gear 변종이 사망원인 dominant 였으니 derived target 으로 Gear 계열 spawn 도 같이 잡힐 것.
2. Lvl ≥5 미달 — `level_up_xp_increment` 하향으로 레벨업 가속 (이전 iter 1 에서 21→17 시도, 효과는 N=2 분산에 묻힘 → N=5 로 재검).
3. Gear 변종 사망원인 66% — `enemy_hp` 약화로 Gear 처치 속도 보강 (Dur 증가 보조).

## 4. Traps (주의)

- **enemy_damage 정수 round-trip**: 자산값 3 에 ±15% → 2.55 / 3.45 → round=3. no-op. → whitelist 에서 제외. 향후 적용 시 adjust ≥ ±35% 또는 absolute step 도입 필요.
- **Ghost burst spawn 60s structural cutoff**: Ghost.asset 변종 7개의 t=60 burst. stat knob 만으로는 못 뚫음. spawn timing (`spawn_rate`) 을 반드시 후보에 포함.
- **bal-converge smoke 제약**: N=2 는 1 iter 만 실행 가능 (bal-apply min_runs_for_analysis=3 충돌). 본격 KPI 수렴은 N≥5 필수 — 이번 plan 은 N=5 로 설정.
- **iter 결과의 자산 변경**: smoke 사이클의 변경이 v17 튜닝 라인에 남아있을 수 있음 (`git diff --stat -- Assets/` 로 사전 확인). 새 baseline 측정이 필요하다고 판단되면 `baseline=new` 로 시작.

## 5. Recommended command

```
/bal-converge kpi="Dur 180~240 AND Lvl>=5" N=5 max_iter=3
```

## 6. Post-run checklist

- [ ] HTML 리포트가 `playbalance/reports/converge_<ts>_<reason>.html` 에 생성됐는지
- [ ] REASON 이 `pass` 가 아니면 본 plan 의 가설 (60s cutoff = Ghost burst) 이 데이터로 뒷받침됐는지 회고
- [ ] `git diff --stat -- Assets/` 로 만진 자산 (Gear*, ExpConfig, PlayerStat 등) 확인 후 commit / rollback 결정
- [ ] enemy_damage 제외가 의도대로 됐는지 (bal-apply.json knob 활성 상태) 확인
