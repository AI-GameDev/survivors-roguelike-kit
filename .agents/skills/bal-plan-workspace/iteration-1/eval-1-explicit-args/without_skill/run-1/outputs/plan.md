# bal-plan (smoke) — Dur>=120

Generated: 2026-05-22 (no real user; defaults documented).

## Inputs (from user)
- mode: `smoke`
- kpi: `Dur>=120` (episode.duration_sec >= 120 seconds, median across runs)

## Environment check
- PlayTrace API `http://localhost:8000/health` → `{"status":"ok"}` (OK)
- `.claude/bal-run.json`, `.claude/bal-apply.json` present (bal-* wired)
- Recent converge reports under `playbalance/reports/`:
  - `_sample_converge_20260522-144530_pass.html`
  - `converge_20260522-161957_max_iter.html` (most recent: hit max_iter, did NOT converge)

## Known traps (from memory)
1. **Ghost burst spawn at t=60s** is a *structural* duration cutoff. Pure stat knobs (HP/damage) historically failed to push median Dur past 60s window — must consider `spawn_rate` / spawn timing on Ghost stage set, not just enemy stats.
2. **smoke mode constraint**: N=2 → only **1 iter** possible. `bal-apply` requires `min_runs_for_analysis=3` so iter 2 auto-aborts. Smoke is for plumbing/sanity, NOT real KPI convergence.
3. **enemy_damage integer round-trip** in ScriptableObject — small adjust_default values may quantize to 0.
4. **unity-cli background-bash hangs** — keep edits foreground/single-shot.

## Goal interpretation
`Dur>=120` is a **looser** target than typical (prior runs aim Dur 180-240). For smoke this is appropriate: a low bar to verify the loop runs end-to-end. Treat as a plumbing smoke test, not a tuning run.

## Plan

### KPI (success criteria)
- Primary: `median(episode.duration_sec) >= 120` across the run set.
- Smoke acceptance: loop completes 1 iter without error; report HTML generated; PlayTrace session has N>=2 plays logged.

### Defaults chosen (no user available)
| Param | Value | Rationale |
|---|---|---|
| `N` (plays/iter) | **2** | smoke convention; matches memory note on smoke constraint |
| `max_iter` | **1** | iter 2 would abort on min_runs_for_analysis=3 |
| `per_play_timeout` | **180s** | KPI is 120s; allow headroom |
| `knob whitelist` | `enemy_hp`, `spawn_rate` (Ghost stage set only) | low-risk; covers stat + structural axes given Ghost cutoff trap |
| `knob blacklist` | `enemy_damage` | integer round-trip risk in smoke (too few samples to verify quantization) |
| `baseline` | current `HEAD` (`balance/v17-tuning`) | no prior baseline pin needed for smoke |

### Steps
1. **Verify env** → `curl /health` (already done: ok). Verify `.claude/bal-run.json` chart_keys include `episode.duration_sec` (yes).
2. **Run 1 iter** of bal-run (N=2, cap=180s/play). Verify PlayTrace session shows `play_no` 1..2 with `episode.duration_sec`.
3. **Analyze**: bal-apply diagnostics (note: will likely no-op or warn due to min_runs_for_analysis=3 vs N=2 — that is the *expected* smoke behavior).
4. **Report**: HTML written under `playbalance/reports/converge_<ts>_<status>.html`.
5. **Evaluate KPI**: median Dur >= 120 → PASS smoke; else mark plumbing OK but KPI not met (which is fine for smoke).

### Verification per step
- Step 2 → PlayTrace `/sessions/<id>` returns 2 plays with non-null duration.
- Step 3 → bal-apply log shows either "n<min, skipping" or a clean diagnostic dump (no exception).
- Step 4 → report HTML exists, opens, no broken links (self-contained per memory rule).
- Step 5 → numeric check on session medians.

## Recommended next command (do NOT auto-run)
```
/bal-converge mode=smoke kpi="Dur>=120" N=2 max_iter=1 per_play_timeout=180 \
  knobs=enemy_hp,spawn_rate baseline=HEAD
```

## Risks / notes
- Because smoke runs only 1 iter, a "fail" on Dur>=120 does NOT justify tuning decisions. Promote to N>=5, max_iter>=3 before drawing balance conclusions.
- If the run dies in <60s consistently, suspect the Ghost burst spawn trap, not stat knobs.
