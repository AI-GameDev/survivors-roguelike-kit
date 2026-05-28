# bal-plan Summary

- **Mode**: bare invocation, no `bal-plan` skill registered. Interpreted `/bal-plan` as pre-flight planning for `/bal-converge` using project context.
- **KPI**: `Dur 180~240 AND Lvl>=5` (project default).
- **Knob whitelist**: `level_up_xp_increment`, `spawn_rate`, `player_max_hp`, `enemy_hp`. Excluded `enemy_damage` (integer round-trip trap) and `spawn_intensity_curve` (manual-only).
- **Run shape**: N=5, max_iter=3, per_play_timeout=10m, diverge_window=2.
- **Key risks**: 60 s Ghost burst structural cutoff (prefer `spawn_rate` if `episode.cause` clusters near 60 s); short-life summary key dropouts may bias means; smoke baseline from 2026-05-22 is not reusable.
- **Recommended command** (not executed): `/bal-converge --kpi "Dur 180~240 AND Lvl>=5" --knobs "level_up_xp_increment,spawn_rate,player_max_hp,enemy_hp" --N 5 --max-iter 3`.
- **Outputs**: `plan.md` (this directory) — full plan. No `/bal-converge` invocation made per test constraints.
