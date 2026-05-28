#!/usr/bin/env python3
"""Programmatic grader for bal-plan iteration-1 runs.

For each (eval, run-config) pair, reads plan.md + summary.md and evaluates the
assertions listed in eval_metadata.json. Writes grading.json per run with the
exact field names the eval-viewer expects (text/passed/evidence).
"""
from __future__ import annotations
import json
import re
from pathlib import Path

ITER = Path(__file__).parent
EVALS = sorted([d for d in ITER.iterdir() if d.is_dir() and d.name.startswith("eval-")])

# Real knob ids loaded from .claude/bal-apply.json — to verify "no invented names"
BAL_APPLY = json.loads((ITER.parent.parent.parent.parent / ".claude/bal-apply.json").read_text())
REAL_KNOB_IDS = {k["id"] for k in BAL_APPLY["knobs"]}
# valid /bal-converge args (per .claude/skills/bal-converge/SKILL.md 인자 파싱 표)
VALID_CONVERGE_ARGS = {"kpi", "N", "max_iter", "timeout", "max_wall"}


def grade_text(plan: str, summary: str, assertion: str) -> tuple[bool, str]:
    """Return (passed, evidence) for one assertion against the run's outputs."""
    text = f"{plan}\n\n{summary}"
    a = assertion

    if a.startswith("plan.md file exists"):
        if len(plan) > 500:
            return True, f"plan.md = {len(plan)} chars"
        return False, f"plan.md = {len(plan)} chars (<= 500)"

    if a.startswith("summary.md file exists"):
        if len(summary) > 0:
            return True, f"summary.md = {len(summary)} chars"
        return False, "summary.md missing"

    if "recommended /bal-converge command" in a.lower() or "copy-pasteable line" in a.lower():
        m = re.search(r"^[ >`]*/bal-converge[^\n]{0,300}", text, re.MULTILINE)
        if m:
            return True, f"matched: {m.group(0).strip()[:120]}"
        return False, "no /bal-converge line found"

    if a == "Recommended command includes a kpi= argument":
        m = re.search(r"/bal-converge[^\n]*kpi\s*=", text)
        if m: return True, f"matched: {m.group(0)[:80]}"
        return False, "no kpi= in /bal-converge line"

    if "prior converge report history" in a:
        # Look for reference to a real prior report (20260522, max_iter, or the actual filename)
        for needle in ("max_iter", "20260522-161957", "converge_2026", "이전 리포트", "이전 converge"):
            if needle in text:
                return True, f"found marker: {needle!r}"
        return False, "no reference to prior reports"

    if "memory trap" in a:
        # At least one of: enemy_damage int round-trip, Ghost burst spawn
        markers = []
        if re.search(r"enemy[_ ]damage.{0,80}(round[ -]?trip|반올림|정수|integer|no[- ]op)", text, re.IGNORECASE | re.DOTALL):
            markers.append("enemy_damage round-trip")
        if re.search(r"(Ghost|spawn).{0,200}(60s|burst|cutoff|structural)", text, re.IGNORECASE | re.DOTALL):
            markers.append("Ghost burst / structural cutoff")
        if markers:
            return True, "; ".join(markers)
        return False, "no trap surfaced"

    if "actual knob ids" in a:
        mentioned = {k for k in REAL_KNOB_IDS if k in text}
        if len(mentioned) >= 2:
            return True, f"real knob ids found ({len(mentioned)}): {sorted(mentioned)}"
        if mentioned:
            return False, f"only {len(mentioned)} real knob id mentioned: {sorted(mentioned)}"
        return False, "no real knob ids mentioned"

    if "does NOT claim /bal-converge was executed" in a:
        # Bad phrases: "executed /bal-converge", "ran /bal-converge", "실행했", "돌렸"
        # But OK: "recommended /bal-converge", "추천", "다음 단계"
        bad_patterns = [
            r"executed\s*/?bal-converge",
            r"ran\s*/?bal-converge",
            r"/bal-converge\s*(?:was|has been|completed)",
            r"실행\s*완료.*bal-converge",
        ]
        for p in bad_patterns:
            if re.search(p, text, re.IGNORECASE):
                return False, f"claim found: {p}"
        return True, "no execution claim detected"

    # eval-1 specific
    if "Dur>=120" in a and "KPI section" in a:
        if "Dur>=120" in text or "Dur >= 120" in text:
            return True, "Dur>=120 present"
        return False, "Dur>=120 not in plan"

    if a == "Plan budget section reflects smoke mode: N<=2 AND max_iter=1":
        # Look at the recommended /bal-converge command — that's the canonical budget.
        m = re.search(r"/bal-converge[^\n]+", text)
        if not m:
            return False, "no /bal-converge command line"
        line = m.group(0)
        m_n = re.search(r"\bN\s*=\s*(\d+)", line)
        m_mi = re.search(r"max[_-]?iter\s*=\s*(\d+)", line)
        n = int(m_n.group(1)) if m_n else None
        mi = int(m_mi.group(1)) if m_mi else None
        if n is not None and n <= 2 and mi == 1:
            return True, f"command: N={n} max_iter={mi}"
        return False, f"command N={n} max_iter={mi} (line: {line[:120]})"

    if 'Recommended command contains kpi="Dur>=120"' in a:
        if re.search(r'/bal-converge[^\n]*kpi\s*=\s*"?Dur\s*>=\s*120"?', text):
            return True, "found"
        return False, "kpi=\"Dur>=120\" missing in command"

    if "Recommended command contains N=2 AND max_iter=1" in a:
        m = re.search(r"/bal-converge[^\n]*", text)
        if m:
            line = m.group(0)
            if re.search(r"\bN\s*=\s*2\b", line) and re.search(r"max_?iter\s*=\s*1\b", line):
                return True, line[:120]
            return False, f"line: {line[:120]}"
        return False, "no /bal-converge line"

    if "smoke mode is pipeline-validation only" in a:
        if re.search(r"smoke.{0,300}(파이프라인|pipeline|검증|validation|시그널.{0,30}(없|불가|기대\s*X))", text, re.IGNORECASE | re.DOTALL):
            return True, "smoke caveat present"
        return False, "no smoke caveat"

    # eval-2 specific
    if "Summary documents defaults taken for all 4 Q&A" in a:
        markers = sum(1 for k in ("KPI", "Baseline", "Whitelist", "Budget", "예산", "knob") if k in summary)
        if markers >= 3:
            return True, f"Q&A markers in summary: {markers}"
        return False, f"Q&A markers in summary: {markers}"

    if "Plan has all 6 sections per SKILL.md template" in a:
        sections = ["Context", "Direction", "Trap", "Recommended", "checklist"]
        found = sum(1 for s in sections if re.search(s, plan, re.IGNORECASE))
        if found >= 4:
            return True, f"{found}/5 core sections found"
        return False, f"{found}/5 core sections"

    if "single copy-pasteable /bal-converge line with no placeholders" in a:
        m = re.search(r"^[ >`]*/bal-converge[^\n]+", plan, re.MULTILINE)
        if not m:
            return False, "no /bal-converge line"
        line = m.group(0).strip()
        if re.search(r"<[^>]+>|\bTODO\b|\bplaceholder\b|\.\.\.", line):
            return False, f"placeholder in line: {line[:120]}"
        return True, line[:120]

    if "bal-converge-supported args" in a:
        m = re.search(r"/bal-converge\s+([^\n]+)", text)
        if not m:
            return False, "no /bal-converge command"
        line = m.group(1)
        # Extract arg names from "name=value" pairs
        args = set(re.findall(r"\b(\w+)\s*=", line))
        invalid = args - VALID_CONVERGE_ARGS
        if invalid:
            return False, f"invalid args: {sorted(invalid)}"
        return True, f"args used: {sorted(args)}"

    return False, f"UNRECOGNIZED ASSERTION: {a!r}"


def grade_run(run_dir: Path, assertions: list[dict]) -> dict:
    plan_path = run_dir / "outputs" / "plan.md"
    summary_path = run_dir / "outputs" / "summary.md"
    plan = plan_path.read_text() if plan_path.exists() else ""
    summary = summary_path.read_text() if summary_path.exists() else ""
    expectations = []
    n_pass = 0
    for a in assertions:
        passed, evidence = grade_text(plan, summary, a["text"])
        expectations.append({"text": a["text"], "passed": passed, "evidence": evidence})
        if passed:
            n_pass += 1
    n_total = len(expectations)
    return {
        "expectations": expectations,
        "summary": {
            "passed": n_pass,
            "failed": n_total - n_pass,
            "total": n_total,
            "pass_rate": round(n_pass / n_total, 4) if n_total else 0.0,
        },
    }


def main():
    for eval_dir in EVALS:
        meta = json.loads((eval_dir / "eval_metadata.json").read_text())
        assertions = meta["assertions"]
        for run_name in ("with_skill", "without_skill"):
            # New layout: eval-N/<config>/run-1/{outputs,grading.json,timing.json}
            run_dir = eval_dir / run_name / "run-1"
            if not run_dir.exists():
                # Fallback for old flat layout
                run_dir = eval_dir / run_name
                if not run_dir.exists():
                    continue
            grading = grade_run(run_dir, assertions)
            (run_dir / "grading.json").write_text(json.dumps(grading, indent=2, ensure_ascii=False))
            s = grading["summary"]
            print(f"{eval_dir.name}/{run_name}: {s['passed']}/{s['total']} pass ({s['pass_rate']*100:.0f}%)")


if __name__ == "__main__":
    main()
