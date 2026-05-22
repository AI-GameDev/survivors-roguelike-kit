# bal-plan smoke run — summary

## Args parsed
- mode=smoke → N=2, max_iter=1
- kpi="Dur>=120" (user explicit)
- baseline=auto (default)

## Phases executed
1. Pre-checks — PlayTrace /health ok; .claude/bal-{converge,apply,run}.json all present; playbalance/plans/ created.
2. Phase 1 (Report inventory) — playbalance/reports/converge_*.html empty. First plan ever; graceful-skipped.
3. Phase 2 (Baseline scan) — /api/sessions + GET /api/logs/search:
   - 20260522_005: N=6, Dur 62.0±0.1s (best)
   - 20260522_008: N=2, Dur 62.9±1.0s
   - 20260522_010: N=1, Dur 61.9s
   Note: POST /api/logs/search returned 405; switched to GET (matches PlayTrace MANUAL).
4. Phase 3 (Trap surfacing) — Matched memory traps to bal-apply.json knobs:
   - enemy_damage integer round-trip absorption
   - Ghost 60s burst spawn structural cutoff (spawn_rate / spawn_intensity_curve)
   - Strong signal: σ/μ ≈ 0.2% at ~62s → structural, not stat.
5. Phase 4 (Q&A) — Skipped (test env, no real user). Defaults assumed below.
6. Phase 5 (Plan write) — playbalance/plans/plan_20260522-164803.md

## Assumed Q&A defaults
- KPI: user-supplied "Dur>=120" (no confirm needed).
- Baseline: auto → reuse (Phase 2 found recent ≤24h candidates).
- Knob whitelist: full (5 auto-knobs; spawn_intensity_curve excluded as manual-edit per bal-apply.json).
- Budget: mode=smoke → N=2, max_iter=1 (1-iter ceiling per memory: bal-apply min_runs_for_analysis=3 aborts iter 2).

## Recommended /bal-converge command
/bal-converge kpi="Dur>=120" N=2 max_iter=1

## Failures / notes
- /api/logs/search POST returned 405 → retried as GET with query string; succeeded.
- No prior converge reports — Phase 1 graceful-skipped per skill spec.
- Baseline candidates cluster at ~62s with near-zero variance, suggesting Dur>=120 unreachable via stat-only knobs; smoke result will likely be no-signal, prompting transition to manual spawn_intensity_curve editing.

## Artifacts
- Plan: /Users/mingyukim/Documents/GitHub/018_Study-Koomin/survivors-roguelike-kit/playbalance/plans/plan_20260522-164803.md
- Copy: .claude/skills/bal-plan-workspace/iteration-1/eval-1-explicit-args/with_skill/outputs/plan.md
