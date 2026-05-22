# Summary — /bal-plan mode=smoke kpi="Dur>=120"

- **Interpreted** as pre-converge planning: pick KPI, knobs, N, max_iter for a smoke run before invoking `/bal-converge`.
- **Mode**: smoke → forced **N=2, max_iter=1** (memory: bal-apply needs n>=3, so iter 2 would abort — smoke is plumbing-only).
- **KPI**: `median(episode.duration_sec) >= 120s`. Lower than typical 180-240 target; appropriate for smoke acceptance.
- **Knob whitelist**: `enemy_hp` + `spawn_rate` (Ghost stage set). Excluded `enemy_damage` (integer round-trip risk at low N).
- **Trap flagged**: Ghost burst spawn at t=60s is a *structural* cutoff — if smoke fails Dur<60, the fix is spawn timing, not stats.
- **Env**: PlayTrace healthy; bal-run/bal-apply configs present; latest real converge (`converge_20260522-161957_max_iter.html`) hit max_iter without converging — supports caution.
- **Recommended command** (not executed):
  `/bal-converge mode=smoke kpi="Dur>=120" N=2 max_iter=1 per_play_timeout=180 knobs=enemy_hp,spawn_rate baseline=HEAD`
- **Outputs**:
  - `.../without_skill/outputs/plan.md`
  - `.../without_skill/outputs/summary.md`
