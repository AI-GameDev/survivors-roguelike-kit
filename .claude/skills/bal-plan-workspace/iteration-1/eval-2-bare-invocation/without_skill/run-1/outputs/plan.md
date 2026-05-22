# bal-plan — Pre-flight Plan for `/bal-converge`

Generated: 2026-05-22
Branch: `balance/v17-tuning` (HEAD `c1b3600`)
Mode: bare invocation (no `bal-plan` skill registered). Defaults chosen from project config and hand-off context.

---

## 1. Context Snapshot

- Project: `survivors-roguelike-kit` (Unity 6 Vampire Survivors–style).
- Last hand-off (`docs/hand-off.md`) flags **본격 KPI 사이클** as the top-priority next action.
- Smoke run (`playbalance/reports/converge_20260522-161957_max_iter.html`) ended at `max_iter` with N=2 noise dominating signal — not a real convergence result.
- Open trap memos:
  - **`enemy_damage` integer round-trip** (small ints like `3` absorb ±15 % adjust → effectively no-op). Treat as caution, not as a hard exclusion.
  - **60 s structural cutoff = Ghost burst spawn** (`Ghost.asset` t=60 burst). Pure stat knobs are weak against this; spawn timing is the real lever for `Dur` past 60 s.
  - **bal-converge smoke constraint**: N=2 collides with `bal-apply.min_runs_for_analysis = 3`, so any real cycle must use **N ≥ 5**.
  - **iter 1 short-life summary key dropouts** (logger ShutdownAsync timing). Still open; just flag in report — not a planning blocker.

## 2. KPI

Use the project default from `.claude/bal-converge.json`:

```
Dur 180~240 AND Lvl>=5
```

- `Dur` = `episode.duration_sec` — target window 180–240 s.
- `Lvl` = `episode.final_level` — floor 5.
- Rationale: matches existing config + last hand-off intent. No reason to invent a new KPI without a user in the loop.

## 3. Knob Whitelist

Picked from `.claude/bal-apply.json`, ordered by expected impact on the KPI window and integer-safety:

1. `level_up_xp_increment` — primary lever for `Lvl ≥ 5`. Numeric float field, no rounding trap. Trigger: `level_cap_low`.
2. `spawn_rate` — primary lever for `Dur` past the 60 s Ghost wall (structural cutoff trigger). Float field.
3. `player_max_hp` — secondary lever for `Dur`, integer/float on PlayerStat `HP`. Watch rounding on small values but range is large enough to step.
4. `enemy_hp` — secondary lever for kill rate / pacing. Trigger: `low_kill_rate`.

**Excluded for this cycle:**

- `enemy_damage` — small-int round-trip trap until the bal-apply integer guard lands. Re-enable in a later iteration.
- `spawn_intensity_curve` — manual `open_in_unity` only; not auto-applicable.

## 4. Run Shape

- `N = 5` plays per iteration (above `min_runs_for_analysis = 3`, low enough to keep wall-time bounded).
- `max_iter = 3` (project config allows 5; pick 3 to bound a first real cycle and re-plan after).
- `per_play_timeout = 10m` (project default).
- `diverge_window = 2` (project default — abort if no improvement across two consecutive iters).
- `max_wall_sec = 1800` (project default; ~30 min cap).

Expected wall time: roughly 30–60 min depending on episode length distribution.

## 5. Acceptance / Abort Rules

- **Pass**: KPI satisfied on the iteration's aggregate (mean `Dur` in [180, 240], mean `Lvl` ≥ 5).
- **Abort**: `diverge_window` triggers, `max_iter` hit, or `max_wall_sec` hit. Each path produces an HTML report under `playbalance/reports/`.
- **Diagnostic flag** (not a blocker): if `episode.cause` clusters at ~60 s, prefer `spawn_rate` next iter over stat knobs (Ghost burst hypothesis).

## 6. Recommended `/bal-converge` Invocation

```
/bal-converge \
  --kpi "Dur 180~240 AND Lvl>=5" \
  --knobs "level_up_xp_increment,spawn_rate,player_max_hp,enemy_hp" \
  --N 5 \
  --max-iter 3
```

(Per the test constraints, this command is **not** being executed in this session — it is the recommendation a human operator should approve before running.)

## 7. Open Risks Carried Into the Cycle

- `enemy_damage` integer rounding remains unfixed; whitelist works around it but does not solve it.
- Short-life summary key dropouts may bias means downward; if a high fraction of plays end < 40 s, treat the iteration as low-signal.
- Smoke result from `20260522-161957` cannot be used as baseline for evaluating iter-1 deltas; this cycle establishes its own baseline.
