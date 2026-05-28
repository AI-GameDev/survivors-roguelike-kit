# Iteration 1 — Analyst notes

## Headline

- **With Skill: 100% pass rate (24/24 assertions across 3 evals).**
- **Without Skill: 83% pass rate (20/24).**
- Cost: with_skill is ~75% slower (157s vs 90s) and uses ~32% more tokens. Reasonable trade for thoroughness.

## What failed in baselines

| Eval | Failed assertion | Root cause |
|---|---|---|
| eval-0 | `kpi=` not in recommended command | Baseline guessed flag syntax `--kpi "Dur 90~150 AND Lvl>=4"` (double-dash) — wrong for bal-converge which uses `kpi="..."` style |
| eval-1 | "smoke is pipeline-validation only" caveat | Baseline didn't articulate the smoke trade-off |
| eval-2 | Q&A defaults not documented (2/4 markers); only 3/5 core sections; only 1 knob id mentioned | Baseline output was a generic plan, not following the structured 6-section template |

## Patterns hidden by aggregate

1. **Both with_skill agents hit the same SKILL.md doc bug**: `/api/logs/search` example uses POST but the real endpoint is GET. Both adapted inline. **Action: update SKILL.md Phase 2 sample code to use GET explicitly.**
2. **eval-1 with_skill reported "Phase 1 graceful skip — no prior reports"** but `playbalance/reports/converge_20260522-161957_max_iter.html` exists. Other with_skill agents (eval-0, eval-2) found it correctly. Possibly an agent error rather than a skill bug, but worth verifying the glob pattern in SKILL.md is unambiguous.
3. **All 3 with_skill agents independently chose to exclude `enemy_damage`** from the whitelist based on Phase 3 trap surfacing. Strong signal that Phase 3 (memory trap intersection) is doing useful work and the user-facing recommendation lands.
4. **Both baselines tried to write `summary.md` and were blocked** by a harness guard ("subagents should return findings as text"). Not a skill issue, but means evals that require subagents to write files need to either inline-capture the result (what we did) or restructure the file output spec.

## Suggested skill improvements (for iteration 2 if user wants)

- **SKILL.md Phase 2**: change `curl -s -m 5 "..."` sample for `/api/logs/search` to be unambiguous — explicit GET with query params, with a footnote that POST returns 405.
- **SKILL.md Phase 1**: make the prior-reports check more robust — current code uses `ls -t playbalance/reports/converge_*.html` which can return empty if shell is in a different cwd. Consider absolute-path lookup or explicit cwd assertion.
- **Cost optimization**: the recommendation command + plan content are mostly derivable from Phases 1-3. Could shorten Phase 2 baseline scan (currently each candidate session triggers an extra `/api/logs/search` call) — sample size 5 instead of 20.
