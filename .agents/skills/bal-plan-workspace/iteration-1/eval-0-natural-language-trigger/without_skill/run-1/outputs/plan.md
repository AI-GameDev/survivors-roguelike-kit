# bal-converge 사전 계획 (plan)

작성: 2026-05-22
대상 명령: `/bal-converge`
선행 환경 체크: PlayTrace `/health` = ok

## 1. 지난 사이클 회고 (converge_20260522-161957_max_iter.html)

- **종료 사유**: `max_iter` 도달. KPI 미수렴.
- **KPI 사용값(디폴트)**: `Dur 180~240 AND Lvl>=5`
- **세팅**: N=2 (smoke), max_iter=2
- **iter 0 baseline**: Dur 61.98s, Lvl 3.67, Kills 27, distance 2.23
- **iter 1 결과**: Dur 61.92s (사실상 무변화), Lvl 3.00 (오히려 -18%), distance 2.37 (악화)
- **차단 요인**:
  1. `min_runs_for_analysis=3` vs N=2 smoke 충돌 → iter 2 분석 자체가 abort.
  2. iter 1 play_no=1 이 32s 조기 종료 + summary key 미수집 → bal-apply 진단 누락.
  3. enemy_damage 정수 round-trip(작은 정수에 ±15% 적용 시 흡수).
- **구조적 가설(메모리 인덱스)**:
  - `project_ghost_burst_structural_cutoff`: 60초 부근에서 Ghost burst spawn (Ghost.asset 변종 7개의 t=60 burst)이 사망률을 끌어올림 → 단순 stat knob 으로는 Dur 180~240 진입 불가능. **AnimationCurve / spawn timing 자체를 다뤄야 함.**
  - `project_bal_converge_smoke_constraint`: N=2 smoke 는 1 iter만 의미 있음. KPI 수렴엔 N≥5 필수.

## 2. 이번 사이클 방향성

### 2-1. KPI

**최종 목표(stretch)**: `Dur 180~240 AND Lvl>=5`
**이번 사이클 중간 목표(realistic)**: `Dur 90~150 AND Lvl>=4`

**근거**: baseline 이 62s/Lvl 3.67. 한 번에 3배 Dur + Lvl 5 도달은 비현실적이며 knob 들이 -30%/+30% cap 안에서 흡수하지 못함. 중간 목표를 정복한 뒤 다음 plan 사이클에서 stretch KPI 로 옮겨가는 2-단계 전략. (실패시 보존된 reports 로부터 다음 plan-of-plan 작성 가능)

### 2-2. Knob 화이트리스트

`min_runs_for_analysis=3` 와 enemy_damage round-trip, AnimationCurve 수동 룰 고려:

| knob | 선택 | 사유 |
|---|---|---|
| `spawn_rate` | **포함** | 60s 컷오프(Ghost burst) 의 영향을 줄이는 가장 보수적 자동 knob. BaseRatePerSecond float → round-trip 안전. |
| `level_up_xp_increment` | **포함** | Lvl>=4 도달엔 거의 필수. ExpConfig 단일 자산 → 매칭 깔끔. |
| `player_max_hp` | **포함** | 60s 부근 즉사 완화 → Dur 늘림. design-time PlayerStat 만 매칭됨 (이미 안전). |
| `enemy_hp` | **포함(소폭)** | low_kill_rate 트리거. spawn_rate 와 직교한 효과로 Dur/Lvl 동시 견인 기대. |
| `enemy_damage` | **제외** | 정수 round-trip 문제(메모리 인덱스) — 이번엔 우회. 필요시 다음 plan 에서 별도 trigger. |
| `spawn_intensity_curve` | **제외(수동)** | AnimationCurve, action=open_in_unity. 무인 사이클에서 못 만짐. converge 종료 후 별도로 다룰 작업. |

### 2-3. 사이클 파라미터

- **N=6** (smoke=N≤2 제약 우회, min_runs_for_analysis=3 만족 + 통계 노이즈 완화)
- **max_iter=5** (지난번 2 → 부족했음. 5 면 baseline + 4 적용 사이클)
- **timeout cap**: 240s/한 판 (KPI 상한 240 보다 살짝 여유)

## 3. 안전 장치

1. **iter 1 종료시 자동 종료 조건 X** — 1 iter 만에 KPI 수렴 기대 안 함. 5 iter 까지 끝까지 보기.
2. **enemy_damage 만지지 말 것** — 화이트리스트에서 제외했지만, bal-apply 가 trigger 기반으로 자동 선택할 수 있으므로 knob 이름 화이트리스트를 옵션으로 전달.
3. **AnimationCurve 변경 발생 시** converge 가 멈출 수 있음 → curve knob 도 명시적 제외.
4. **baseline 재수집 권장**: iter 0 을 새로 찍어 noise band 갱신(메모리상 Ghost burst 가설 재확인 포함).

## 4. 실행 계획 (단계별)

1. `/bal-converge` 호출 (아래 추천 커맨드).
2. iter 0 baseline 확인 → 메모리상 Ghost 60s 컷오프 패턴 유지되는지 spot check.
3. iter 1~4 진행 동안 dashboard 2-pane(CONVERGE + GAME) 유지 (메모리 인덱스 `feedback_bal_dashboard_panes_reflexive`).
4. 종료 후 `playbalance/reports/converge_*.html` 의 KPI distance 추이 + knob 변화 누적표 검토.
5. 만약 중간 KPI 도달 → stretch KPI(`Dur 180~240 AND Lvl>=5`) 로 다음 plan 작성.
   만약 미달 → spawn_intensity_curve 수동 편집을 별도 작업으로 분리.

## 5. 추천 명령 (그대로 실행 가능)

```bash
/bal-converge \
  --kpi "Dur 90~150 AND Lvl>=4" \
  --knobs spawn_rate,level_up_xp_increment,player_max_hp,enemy_hp \
  --N 6 \
  --max-iter 5 \
  --timeout-per-play 240
```

> 파라미터 이름이 skill 구현과 다를 수 있음 — `/bal-converge --help` 로 1차 검증 후 미세 조정. KPI/knob/N/max_iter 의 **값** 자체는 위 분석대로 유지.
