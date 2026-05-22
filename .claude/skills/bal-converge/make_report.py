#!/usr/bin/env python3
"""bal-converge HTML report generator.

Game-agnostic. Reads a JSON payload via --in or stdin, writes a self-contained
HTML file (inline SVG + inline CSS, no JS, no CDN) to --out or stdout.

Schema: see ./make_report.schema.md (or the SKILL.md "리포트 생성" section).
"""

import argparse
import html
import json
import math
import os
import sys
from collections import defaultdict
from datetime import datetime


REASON_INFO = {
    "pass":            ("PASS",        "pass",  "KPI 도달"),
    "max_iter":        ("MAX_ITER",    "warn",  "사이클 한도 소진"),
    "max_wall":        ("MAX_WALL",    "warn",  "wall-time 한도 소진"),
    "diverged":        ("DIVERGED",    "fail",  "distance 발산 감지"),
    "no_op":           ("NO_OP",       "warn",  "적용 가능 knob 0개"),
    "apply_abort_2":   ("APPLY_ABORT", "fail",  "bal-apply config 없음"),
    "apply_abort_3":   ("APPLY_ABORT", "fail",  "영향 자산 dirty"),
    "bal_run_failed":  ("RUN_FAILED",  "fail",  "/bal-run session ID 없음"),
}


# ---------- helpers ----------

def esc(s):
    if s is None:
        return ""
    return html.escape(str(s))


def fmt_num(v, digits=2):
    if v is None:
        return "—"
    if isinstance(v, bool):
        return "true" if v else "false"
    try:
        if abs(v) >= 100:
            return f"{v:.1f}"
        return f"{v:.{digits}f}"
    except Exception:
        return esc(v)


def fmt_pct_change(before, after):
    try:
        if before == 0:
            return "—"
        delta = (after - before) / abs(before) * 100
        sign = "+" if delta >= 0 else ""
        return f"{sign}{delta:.1f}%"
    except Exception:
        return "—"


def fmt_duration(sec):
    if sec is None:
        return "—"
    sec = int(sec)
    if sec < 60:
        return f"{sec}s"
    m = sec // 60
    s = sec % 60
    if m < 60:
        return f"{m}m" if s == 0 else f"{m}m{s}s"
    h = m // 60
    m = m % 60
    return f"{h}h{m}m"


def label_for_metric(name, display_labels):
    info = display_labels.get("metrics", {}).get(name, {})
    return info.get("ko", name), info.get("desc", "")


def label_for_knob(name, display_labels):
    info = display_labels.get("knobs", {}).get(name, {})
    return info.get("ko", name), info.get("desc", "")


def asset_short(path):
    if not path or path == "UNRESOLVED":
        return path or "—"
    return os.path.splitext(os.path.basename(path))[0]


def field_short(field):
    if not field:
        return ""
    if "[" in field and "]" in field:
        import re
        m = re.search(r"\[\w+=([^\]]+)\]", field)
        last = field.rsplit(".", 1)[-1]
        return f"{last} ({m.group(1)})" if m else last
    return field.rsplit(".", 1)[-1]


# ---------- gauge SVG ----------

def gauge_svg(metric_name, kind, target, current, display_labels):
    """metric_name: alias. kind: 'range' or 'cmp'.
    target: dict {'min':lo, 'max':hi} (range) or {'op':op, 'val':val} (cmp).
    current: float or None.
    """
    if kind == "range":
        lo = target["min"]
        hi = target["max"]
        scale_max = max(hi * 1.25, (current or 0) * 1.2, hi + (hi - lo))
        scale_max = max(scale_max, 1e-9)
        tx = 20 + lo / scale_max * 360
        tw = (hi - lo) / scale_max * 360
        marker_x = 20 + (current or 0) / scale_max * 360
        marker_x = max(20, min(380, marker_x))
        axis_labels = [(20, "0"), (tx, fmt_num(lo, 0)), (tx + tw, fmt_num(hi, 0)), (380, fmt_num(scale_max, 0))]
    else:
        op = target["op"]
        v = target["val"]
        scale_max = max(v * 2, (current or 0) * 1.5, v + 1)
        scale_max = max(scale_max, 1e-9)
        v_x = 20 + v / scale_max * 360
        marker_x = 20 + (current or 0) / scale_max * 360
        marker_x = max(20, min(380, marker_x))
        if op in (">=", ">"):
            tx = v_x
            tw = 380 - v_x
        elif op in ("<=", "<"):
            tx = 20
            tw = v_x - 20
        else:  # ==
            band = (scale_max * 0.05)
            tx = 20 + max(0, (v - band)) / scale_max * 360
            tw = (2 * band) / scale_max * 360
        axis_labels = [(20, "0"), (v_x, fmt_num(v, 0)), (380, fmt_num(scale_max, 0))]

    label_xml = "".join(
        f'<text class="label-axis" x="{x:.1f}" y="48" text-anchor="{"end" if x > 370 else "middle" if x > 30 else "start"}">{esc(lbl)}</text>'
        for x, lbl in axis_labels
    )

    return (
        f'<svg class="gauge" viewBox="0 0 400 50" preserveAspectRatio="none">'
        f'<rect class="track" x="20" y="22" width="360" height="14" rx="3"/>'
        f'<rect class="target" x="{tx:.1f}" y="22" width="{tw:.1f}" height="14" rx="3"/>'
        f'<line class="marker" x1="{marker_x:.1f}" y1="16" x2="{marker_x:.1f}" y2="42" stroke="currentColor" stroke-width="2"/>'
        f'<circle class="marker" cx="{marker_x:.1f}" cy="29" r="4"/>'
        f'{label_xml}'
        f'</svg>'
    )


# ---------- trend chart SVG ----------

def trend_svg(cycles):
    distances = [c.get("distance") for c in cycles if c.get("distance") is not None]
    if len(distances) < 2:
        return None

    max_d = max(distances)
    scale_max = max_d * 1.1 if max_d > 0 else 1.0
    x_lo, x_hi = 50, 570
    y_lo, y_hi = 30, 210

    n = len(distances)

    def xpos(i):
        if n == 1:
            return x_lo
        return x_lo + (i / (n - 1)) * (x_hi - x_lo)

    def ypos(d):
        return y_hi - (d / scale_max) * (y_hi - y_lo)

    points = []
    for i, c in enumerate(cycles):
        d = c.get("distance")
        if d is None:
            continue
        x = xpos(i)
        y = ypos(d)
        passed = bool(c.get("pass"))
        iter_n = c.get("iter", i)
        sid = c.get("session_id", "")
        metric_summary = ", ".join(f"{k} {fmt_num(v)}" for k, v in (c.get("metrics") or {}).items())
        tip = f"iter {iter_n}{' (baseline)' if c.get('is_baseline') else ''} — distance {fmt_num(d, 2)}"
        if metric_summary:
            tip += f" — {metric_summary}"
        if passed:
            tip += " — PASS"
        points.append((x, y, d, tip, passed, iter_n))

    path_d = "M" + " L".join(f"{x:.1f},{y:.1f}" for x, y, *_ in points)
    area_d = (
        f"M{points[0][0]:.1f},{points[0][1]:.1f} "
        + " ".join(f"L{x:.1f},{y:.1f}" for x, y, *_ in points[1:])
        + f" L{points[-1][0]:.1f},{y_hi} L{points[0][0]:.1f},{y_hi} Z"
    )

    grid_ys = [y_lo, y_lo + (y_hi - y_lo) * 0.25, y_lo + (y_hi - y_lo) * 0.5, y_lo + (y_hi - y_lo) * 0.75, y_hi]
    grid = "".join(f'<line class="gridline" x1="{x_lo}" y1="{y:.1f}" x2="{x_hi}" y2="{y:.1f}"/>' for y in grid_ys)
    y_axis_labels = ""
    for i, y in enumerate(grid_ys):
        v = scale_max * (1 - i / 4)
        y_axis_labels += f'<text x="{x_lo - 8}" y="{y + 4:.1f}" text-anchor="end">{fmt_num(v, 2)}</text>'

    x_axis_labels = ""
    for x, y, d, tip, passed, iter_n in points:
        x_axis_labels += f'<text x="{x:.1f}" y="228" text-anchor="middle">iter {iter_n}</text>'

    dot_xml = ""
    label_xml = ""
    for x, y, d, tip, passed, iter_n in points:
        color = ' fill="#10b981"' if passed else ""
        r = "5" if passed else "4"
        dot_xml += (
            f'<circle class="dot" cx="{x:.1f}" cy="{y:.1f}" r="{r}"{color}>'
            f'<title>{esc(tip)}</title></circle>'
        )
        label_style = ' style="fill:#10b981;font-weight:600"' if passed else ""
        label_xml += f'<text class="label-pt" x="{x:.1f}" y="{y - 11:.1f}"{label_style}>{fmt_num(d, 2)}</text>'

    return (
        f'<svg class="trend" viewBox="0 0 600 240" preserveAspectRatio="none">'
        f'<g>{grid}</g>'
        f'<g class="axis">'
        f'<line x1="{x_lo}" y1="{y_hi}" x2="{x_hi}" y2="{y_hi}"/>'
        f'<line x1="{x_lo}" y1="{y_lo}" x2="{x_lo}" y2="{y_hi}"/>'
        f'{y_axis_labels}{x_axis_labels}'
        f'</g>'
        f'<line class="threshold" x1="{x_lo}" y1="{y_hi}" x2="{x_hi}" y2="{y_hi}"/>'
        f'<text class="threshold-label" x="{x_hi}" y="{y_hi - 3}" text-anchor="end">PASS line</text>'
        f'<path class="area-dist" d="{area_d}"/>'
        f'<path class="line-dist" d="{path_d}"/>'
        f'{dot_xml}{label_xml}'
        f'</svg>'
    )


# ---------- impact ranking ----------

def compute_impact_ranking(cycles):
    """Crude per-knob distance contribution: split each cycle's distance delta
    evenly across the knobs applied in that cycle, then aggregate by knob name.
    Returns list of (knob, total_delta) sorted by |delta| desc."""
    by_knob = defaultdict(float)
    prev_dist = None
    for c in cycles:
        d = c.get("distance")
        if d is None:
            prev_dist = None
            continue
        if prev_dist is not None:
            delta = d - prev_dist
            knobs = c.get("applied_knobs") or []
            if knobs:
                share = delta / len(knobs)
                for k in knobs:
                    by_knob[k.get("knob", "?")] += share
        prev_dist = d
    items = sorted(by_knob.items(), key=lambda kv: abs(kv[1]), reverse=True)
    return items


# ---------- render sections ----------

STYLE = """
:root {
  --bg: #f8fafc; --card: #ffffff; --text: #0f172a; --muted: #64748b;
  --border: #e2e8f0; --accent: #2563eb; --success: #10b981;
  --warning: #f59e0b; --danger: #ef4444;
  --mono: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
}
* { box-sizing: border-box; }
body { margin: 0; background: var(--bg); color: var(--text); font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif; font-size: 14px; line-height: 1.55; }
.wrap { max-width: 1080px; margin: 0 auto; padding: 32px 24px 80px; }
header.top { display: flex; align-items: center; justify-content: space-between; margin-bottom: 24px; padding-bottom: 16px; border-bottom: 1px solid var(--border); }
header.top h1 { font-size: 18px; margin: 0; font-weight: 600; }
header.top .meta { font-family: var(--mono); color: var(--muted); font-size: 12px; }
.badge { display: inline-block; padding: 3px 10px; border-radius: 999px; font-size: 12px; font-weight: 600; letter-spacing: 0.02em; }
.badge.pass { background: #dcfce7; color: #065f46; }
.badge.fail { background: #fee2e2; color: #991b1b; }
.badge.warn { background: #fef3c7; color: #92400e; }
section { margin: 32px 0; }
h2 { font-size: 14px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; color: var(--muted); margin: 0 0 12px; }
.card { background: var(--card); border: 1px solid var(--border); border-radius: 8px; padding: 16px 20px; }
.summary-grid { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; }
.summary-grid .card .k { font-size: 11px; color: var(--muted); text-transform: uppercase; letter-spacing: 0.04em; }
.summary-grid .card .v { font-size: 22px; font-weight: 600; margin-top: 4px; font-family: var(--mono); }
.summary-grid .card .v.green { color: var(--success); }
.summary-grid .card .v.red { color: var(--danger); }
.summary-grid .card .v.amber { color: var(--warning); }
.summary-grid .card .sub { font-size: 11px; color: var(--muted); margin-top: 4px; }
.kpi-row { display: grid; grid-template-columns: 160px 1fr 120px; gap: 16px; align-items: center; padding: 12px 0; border-top: 1px solid var(--border); }
.kpi-row:first-of-type { border-top: 0; }
.kpi-row .name { font-weight: 600; }
.kpi-row .name .desc { display: block; font-weight: 400; font-size: 11px; color: var(--muted); margin-top: 2px; }
.kpi-row .val { text-align: right; font-family: var(--mono); }
.kpi-row .val .num { font-size: 16px; font-weight: 600; }
.kpi-row .val .pass-tag { font-size: 11px; }
svg.gauge { display: block; width: 100%; height: 50px; }
svg.gauge .track { fill: #f1f5f9; }
svg.gauge .target { fill: #d1fae5; }
svg.gauge .marker { fill: var(--text); }
svg.gauge .label-axis { font-family: var(--mono); font-size: 9px; fill: var(--muted); }
.chart-card { padding: 20px 24px 8px; }
svg.trend { display: block; width: 100%; height: 280px; }
svg.trend .axis line { stroke: var(--border); stroke-width: 1; }
svg.trend .axis text { font-family: var(--mono); font-size: 10px; fill: var(--muted); }
svg.trend .gridline { stroke: #f1f5f9; stroke-width: 1; }
svg.trend .line-dist { stroke: var(--accent); stroke-width: 2; fill: none; }
svg.trend .dot { fill: var(--accent); cursor: default; }
svg.trend .dot:hover { fill: var(--text); }
svg.trend .area-dist { fill: var(--accent); opacity: 0.08; }
svg.trend .label-pt { font-family: var(--mono); font-size: 10px; fill: var(--muted); text-anchor: middle; }
svg.trend .threshold { stroke: var(--success); stroke-dasharray: 4 3; stroke-width: 1; }
svg.trend .threshold-label { font-family: var(--mono); font-size: 9px; fill: var(--success); }
table { width: 100%; border-collapse: collapse; font-size: 13px; }
table th, table td { padding: 8px 10px; text-align: left; border-bottom: 1px solid var(--border); }
table th { font-size: 11px; text-transform: uppercase; letter-spacing: 0.04em; color: var(--muted); font-weight: 600; background: #f8fafc; }
table td.num, table th.num { text-align: right; font-family: var(--mono); }
table tr:hover td { background: #f8fafc; }
.pill { display: inline-block; padding: 1px 7px; border-radius: 3px; font-size: 11px; font-family: var(--mono); }
.pill.pass { background: #dcfce7; color: #065f46; }
.pill.fail { background: #fee2e2; color: #991b1b; }
.knob-block { margin: 12px 0; padding: 12px 16px; background: #f8fafc; border-left: 3px solid var(--accent); border-radius: 4px; }
.knob-block .head { font-family: var(--mono); font-size: 12px; color: var(--muted); margin-bottom: 6px; }
.knob-block .change { font-family: var(--mono); font-size: 13px; }
.knob-block .change .knob-name { font-weight: 600; color: var(--text); }
.knob-block .change .arrow { color: var(--muted); }
.knob-block .change .from { color: var(--danger); }
.knob-block .change .to { color: var(--success); }
.knob-block .why { font-size: 12px; color: var(--muted); margin-top: 4px; }
.nbox { padding: 18px 22px; border-radius: 8px; margin-bottom: 12px; border: 1px solid var(--border); }
.nbox h3 { margin: 0 0 10px; font-size: 14px; font-weight: 600; }
.nbox p, .nbox li { font-size: 13px; line-height: 1.65; }
.nbox ul { margin: 6px 0 10px; padding-left: 20px; }
.src-tag { display: inline-block; font-size: 10px; font-weight: 700; letter-spacing: 0.06em; padding: 2px 7px; border-radius: 3px; text-transform: uppercase; margin-bottom: 10px; }
.src-tag.chat { background: #ede9fe; color: #5b21b6; }
.src-tag.data { background: #dbeafe; color: #1e40af; }
.src-tag.mix  { background: #fef3c7; color: #92400e; }
.nbox.chat { background: #faf5ff; }
.nbox.data { background: #eff6ff; }
.nbox.mix  { background: #fffbeb; }
.impact-list { margin: 8px 0 4px; }
.impact-row { display: grid; grid-template-columns: 24px 1fr 90px 60px; gap: 10px; align-items: center; padding: 6px 0; font-size: 12px; }
.impact-row .rank { font-family: var(--mono); color: var(--muted); text-align: center; }
.impact-row .name { font-family: var(--mono); }
.impact-row .bar-wrap { background: #e2e8f0; height: 8px; border-radius: 4px; overflow: hidden; }
.impact-row .bar { background: var(--accent); height: 100%; }
.impact-row .val { font-family: var(--mono); text-align: right; font-size: 11px; color: var(--muted); }
.delta-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; margin: 10px 0; }
.delta-card { background: #fff; border: 1px solid var(--border); border-radius: 6px; padding: 10px 14px; }
.delta-card .label { font-size: 11px; color: var(--muted); text-transform: uppercase; letter-spacing: 0.04em; }
.delta-card .ba { display: flex; align-items: baseline; gap: 8px; margin-top: 6px; font-family: var(--mono); }
.delta-card .before { color: var(--danger); font-size: 13px; }
.delta-card .arrow { color: var(--muted); }
.delta-card .after { color: var(--success); font-size: 18px; font-weight: 600; }
.delta-card .delta-pct { font-size: 11px; color: var(--muted); margin-top: 2px; }
.empty { padding: 20px; text-align: center; color: var(--muted); font-size: 12px; font-style: italic; }
footer.links { margin-top: 40px; padding-top: 16px; border-top: 1px solid var(--border); font-size: 12px; color: var(--muted); }
footer.links a { color: var(--accent); text-decoration: none; }
footer.links a:hover { text-decoration: underline; }
footer.links code { font-family: var(--mono); background: #f1f5f9; padding: 1px 5px; border-radius: 3px; font-size: 11px; }
"""


def render_header(data):
    reason = data.get("reason", "")
    label, cls, _ = REASON_INFO.get(reason, (reason.upper() or "?", "warn", ""))
    ts = data.get("timestamp_iso", "")
    project = data.get("project_name", "")
    version = data.get("version", "")
    branch = data.get("branch", "")
    kpi = data.get("kpi_expression", "")
    last_iter = max((c.get("iter", 0) for c in data.get("cycles") or [{}]), default=0)
    return (
        f'<header class="top">'
        f'<div>'
        f'<h1>bal-converge — {esc(project)}{(" · " + esc(version)) if version else ""}</h1>'
        f'<div class="meta">{esc(ts)}'
        f'{" · branch <code>" + esc(branch) + "</code>" if branch else ""}'
        f'{" · KPI <code>" + esc(kpi) + "</code>" if kpi else ""}'
        f'</div></div>'
        f'<div><span class="badge {cls}">{esc(label)} · iter {last_iter}</span></div>'
        f'</header>'
    )


def render_summary(data):
    reason = data.get("reason", "")
    label, cls, sub = REASON_INFO.get(reason, (reason.upper() or "?", "warn", ""))
    last_iter = max((c.get("iter", 0) for c in data.get("cycles") or [{}]), default=0)
    max_iter = data.get("max_iter", "?")
    max_wall = data.get("max_wall_sec", 0)
    wall = data.get("wall_sec", 0)
    knob_count = sum(len(c.get("applied_knobs") or []) for c in (data.get("cycles") or []))
    assets = set()
    for c in data.get("cycles") or []:
        for k in c.get("applied_knobs") or []:
            a = k.get("asset")
            if a and a != "UNRESOLVED":
                assets.add(a)
    color_class = "green" if cls == "pass" else ("amber" if cls == "warn" else "red")
    return (
        f'<section><h2>요약</h2><div class="summary-grid">'
        f'<div class="card"><div class="k">결과</div><div class="v {color_class}">{esc(label)}</div><div class="sub">{esc(sub)}</div></div>'
        f'<div class="card"><div class="k">사이클</div><div class="v">{last_iter} / {esc(max_iter)}</div><div class="sub">max_iter={esc(max_iter)}</div></div>'
        f'<div class="card"><div class="k">소요 시간</div><div class="v">{fmt_duration(wall)}</div><div class="sub">max_wall={fmt_duration(max_wall)}</div></div>'
        f'<div class="card"><div class="k">변경 knob</div><div class="v">{knob_count}</div><div class="sub">자산 {len(assets)}개 수정</div></div>'
        f'</div></section>'
    )


def render_kpi_section(data):
    cycles = data.get("cycles") or []
    if not cycles:
        return ""
    last = cycles[-1]
    term_results = last.get("term_results") or []
    if not term_results:
        return ""
    display_labels = data.get("display_labels") or {}

    rows = []
    for t in term_results:
        name = t.get("name", "?")
        ko, desc = label_for_metric(name, display_labels)
        if not desc:
            kind = t.get("kind")
            if kind == "range":
                desc = f"{name} (target {fmt_num(t.get('target_min'),0)}~{fmt_num(t.get('target_max'),0)})"
            elif kind == "cmp":
                desc = f"{name} (target {t.get('target_op','')}{fmt_num(t.get('target_val'),0)})"
        kind = t.get("kind")
        if kind == "range":
            target = {"min": t.get("target_min"), "max": t.get("target_max")}
        else:
            target = {"op": t.get("target_op"), "val": t.get("target_val")}
        gauge = gauge_svg(name, kind, target, t.get("current"), display_labels)
        passed = bool(t.get("pass"))
        pill_class = "pass" if passed else "fail"
        pill_text = "PASS" if passed else "FAIL"
        rows.append(
            f'<div class="kpi-row"><div class="name">{esc(ko)}<span class="desc">{esc(desc)}</span></div>'
            f'<div>{gauge}</div>'
            f'<div class="val"><div class="num">{fmt_num(t.get("current"))}</div>'
            f'<div class="pass-tag"><span class="pill {pill_class}">{pill_text}</span></div></div></div>'
        )
    return f'<section><h2>KPI 현황 (최종 사이클)</h2><div class="card">{"".join(rows)}</div></section>'


def render_trend(data):
    cycles = data.get("cycles") or []
    svg = trend_svg(cycles)
    if not svg:
        return ""
    return (
        f'<section><h2>KPI distance 추이</h2>'
        f'<div class="card chart-card">{svg}'
        f'<div style="font-size:11px;color:var(--muted);text-align:right;margin-top:4px;padding-right:8px;">'
        f'점 위에 마우스를 올리면 사이클 상세 (브라우저 기본 툴팁)</div></div></section>'
    )


def render_cycles_table(data):
    cycles = data.get("cycles") or []
    if not cycles:
        return ""
    display_labels = data.get("display_labels") or {}
    metric_keys = list((data.get("metric_aliases") or {}).keys())
    if not metric_keys and cycles:
        metric_keys = list((cycles[0].get("metrics") or {}).keys())

    header_metric_cells = "".join(
        f'<th class="num">{esc(label_for_metric(k, display_labels)[0])}</th>' for k in metric_keys
    )
    rows = []
    for c in cycles:
        iter_n = c.get("iter", "?")
        iter_label = f"{iter_n} (baseline)" if c.get("is_baseline") else str(iter_n)
        sid = c.get("session_id", "")
        metrics = c.get("metrics") or {}
        metric_cells = "".join(f'<td class="num">{fmt_num(metrics.get(k))}</td>' for k in metric_keys)
        dist = c.get("distance")
        knob_n = len(c.get("applied_knobs") or [])
        passed = bool(c.get("pass"))
        pill_class = "pass" if passed else "fail"
        pill_text = "PASS" if passed else "FAIL"
        rows.append(
            f'<tr><td>{esc(iter_label)}</td><td><code>{esc(sid)}</code></td>'
            f'{metric_cells}<td class="num">{fmt_num(dist)}</td>'
            f'<td class="num">{knob_n if knob_n else "—"}</td>'
            f'<td><span class="pill {pill_class}">{pill_text}</span></td></tr>'
        )
    return (
        f'<section><h2>사이클 상세</h2><div class="card" style="padding:0;overflow:hidden;">'
        f'<table><thead><tr><th>iter</th><th>session</th>{header_metric_cells}'
        f'<th class="num">distance</th><th class="num">knobs</th><th>결과</th></tr></thead>'
        f'<tbody>{"".join(rows)}</tbody></table></div></section>'
    )


def render_applied_knobs(data):
    cycles = data.get("cycles") or []
    display_labels = data.get("display_labels") or {}
    blocks = []
    for c in cycles:
        knobs = c.get("applied_knobs") or []
        if not knobs:
            continue
        iter_n = c.get("iter", "?")
        changes = []
        for k in knobs:
            knob = k.get("knob", "?")
            ko, _ = label_for_knob(knob, display_labels)
            asset = asset_short(k.get("asset"))
            field = field_short(k.get("field", ""))
            frm = k.get("from")
            to = k.get("to")
            pct = ""
            try:
                pct_str = fmt_pct_change(float(frm), float(to))
                if pct_str != "—":
                    pct = f' <span style="color:var(--muted)">({pct_str})</span>'
            except Exception:
                pass
            why = k.get("why", "")
            changes.append(
                f'<div class="change" style="margin-top:6px;">'
                f'<span class="knob-name">{esc(knob)}</span>'
                f' <span class="arrow">→</span> '
                f'{esc(asset)}{(" · <code>" + esc(field) + "</code>") if field else ""}'
                f' &nbsp;<span class="from">{esc(fmt_num(frm))}</span> <span class="arrow">→</span> '
                f'<span class="to">{esc(fmt_num(to))}</span>{pct}</div>'
                f'{("<div class=\"why\">Why: " + esc(why) + "</div>") if why else ""}'
            )
        blocks.append(
            f'<div class="knob-block"><div class="head">iter {esc(iter_n)} · {len(knobs)} knob{"s" if len(knobs)!=1 else ""}</div>'
            f'{"".join(changes)}</div>'
        )
    if not blocks:
        return ""
    return f'<section><h2>적용 변경 이력</h2>{"".join(blocks)}</section>'


def render_narrative(data):
    narrative = data.get("narrative") or {}
    display_labels = data.get("display_labels") or {}
    cycles = data.get("cycles") or []

    parts = ['<section><h2>해석 / 메모</h2>']

    # box 1: chat summary
    chat = narrative.get("chat_summary") or {}
    chat_html = ['<div class="nbox chat"><span class="src-tag chat">💬 이번 세션 대화 요약</span>']
    kpi_rationale = chat.get("kpi_rationale")
    if kpi_rationale:
        chat_html.append('<h3>왜 이 KPI 였나</h3>')
        chat_html.append(f'<p>{esc(kpi_rationale)}</p>')
    priors = chat.get("prior_hypotheses") or []
    if priors:
        chat_html.append('<h3>사전 가설</h3><ul>')
        for p in priors:
            chat_html.append(f'<li>{esc(p)}</li>')
        chat_html.append('</ul>')
    concerns = chat.get("user_concerns") or []
    if concerns:
        chat_html.append('<h3>사용자가 표명한 우려</h3><ul>')
        for c in concerns:
            chat_html.append(f'<li>{esc(c)}</li>')
        chat_html.append('</ul>')
    if not (kpi_rationale or priors or concerns):
        invocation = chat.get("invocation_summary") or "이번 호출 인자만 가지고 진행. 추가 논의 없음."
        chat_html.append(f'<p>{esc(invocation)}</p>')
    chat_html.append(
        '<p style="font-size:11px;color:var(--muted);margin-top:12px;">'
        '※ 이 박스는 Claude가 종료 시점에 대화 로그를 요약한 재구성입니다. '
        '정확한 사용자 발언은 대화 원문을 참조하세요.</p></div>'
    )
    parts.append("".join(chat_html))

    # box 2: data analysis
    data_extra = narrative.get("data_analysis_extra") or {}
    data_html = ['<div class="nbox data"><span class="src-tag data">📊 데이터 기반 자동 분석</span>']

    if cycles:
        baseline = cycles[0]
        last = cycles[-1]
        baseline_obs = data_extra.get("baseline_observation")
        if baseline_obs:
            data_html.append('<h3>1) 시작 상태 (baseline)</h3>')
            data_html.append(f'<p>{esc(baseline_obs)}</p>')

        metric_keys = list((data.get("metric_aliases") or {}).keys())
        if not metric_keys:
            metric_keys = list((baseline.get("metrics") or {}).keys())
        if len(cycles) >= 2 and metric_keys:
            data_html.append('<div class="delta-grid">')
            for k in metric_keys:
                before = (baseline.get("metrics") or {}).get(k)
                after = (last.get("metrics") or {}).get(k)
                if before is None or after is None:
                    continue
                ko, desc = label_for_metric(k, display_labels)
                pct = fmt_pct_change(before, after)
                data_html.append(
                    f'<div class="delta-card"><div class="label">{esc(ko)} ({k})</div>'
                    f'<div class="ba"><span class="before">{fmt_num(before)}</span>'
                    f'<span class="arrow">→</span><span class="after">{fmt_num(after)}</span></div>'
                    f'<div class="delta-pct">{esc(pct)} · {len(cycles)-1} 사이클 변경 후</div></div>'
                )
            data_html.append('</div>')

        ranking = compute_impact_ranking(cycles)
        if ranking:
            max_abs = max(abs(v) for _, v in ranking) or 1.0
            data_html.append('<h3>2) 변경 효과 순위 (단일 knob 기준, distance 기여도)</h3>')
            data_html.append('<p style="font-size:12px;color:var(--muted);margin:0 0 4px;">'
                             '사이클 적용 직전·직후 distance 차이를 그 사이클에서 적용된 knob들에 균등 분할.</p>')
            data_html.append('<div class="impact-list">')
            for i, (kname, delta) in enumerate(ranking[:6]):
                ko, _ = label_for_knob(kname, display_labels)
                pct = abs(delta) / max_abs * 100
                sign = "−" if delta < 0 else "+"
                data_html.append(
                    f'<div class="impact-row"><div class="rank">{i+1}</div>'
                    f'<div class="name">{esc(kname)}{(" — " + esc(ko)) if ko != kname else ""}</div>'
                    f'<div class="bar-wrap"><div class="bar" style="width:{pct:.0f}%"></div></div>'
                    f'<div class="val">{sign}{fmt_num(abs(delta))}</div></div>'
                )
            data_html.append('</div>')

        key_obs = data_extra.get("key_observation")
        if key_obs:
            data_html.append(f'<p style="font-size:12px;margin-top:8px;"><strong>핵심 관찰:</strong> {esc(key_obs)}</p>')

        if len(cycles) >= 2:
            data_html.append('<h3>3) 진행 과정 한눈에</h3><ul>')
            prev = cycles[0]
            for c in cycles[1:]:
                iter_n = c.get("iter", "?")
                knobs = c.get("applied_knobs") or []
                knob_summary = ", ".join(
                    f'{esc(k.get("knob","?"))}' for k in knobs
                ) if knobs else "(no knobs)"
                d_prev = prev.get("distance")
                d_cur = c.get("distance")
                d_txt = ""
                if d_prev is not None and d_cur is not None:
                    d_txt = f' distance {fmt_num(d_prev)} → {fmt_num(d_cur)}.'
                data_html.append(f'<li><strong>iter {esc(iter_n)}</strong> ({knob_summary}):{d_txt}</li>')
                prev = c
            data_html.append('</ul>')

        signals = data_extra.get("signals") or []
        if signals:
            data_html.append('<h3>4) 진행 신호</h3><ul>')
            for s in signals:
                data_html.append(f'<li>{esc(s)}</li>')
            data_html.append('</ul>')
    else:
        data_html.append('<div class="empty">사이클 데이터 없음 (baseline 이전 단계에서 종료).</div>')

    data_html.append('</div>')
    parts.append("".join(data_html))

    # box 3: conclusion
    conclusion = narrative.get("conclusion") or {}
    validated = conclusion.get("validated") or []
    next_hyp = conclusion.get("next_hypotheses") or []
    if validated or next_hyp:
        c_html = ['<div class="nbox mix"><span class="src-tag mix">🎯 결론 + 후속 가설 (대화 + 데이터)</span>']
        if validated:
            c_html.append('<h3>이번 사이클이 입증한 것</h3><ul>')
            for v in validated:
                c_html.append(f'<li>{esc(v)}</li>')
            c_html.append('</ul>')
        if next_hyp:
            c_html.append('<h3>다음 세션 후속 가설</h3><ul>')
            for n in next_hyp:
                c_html.append(f'<li>{esc(n)}</li>')
            c_html.append('</ul>')
        c_html.append('</div>')
        parts.append("".join(c_html))

    parts.append('</section>')
    return "".join(parts)


def render_footer(data):
    cycles = data.get("cycles") or []
    converge_sid = data.get("converge_session_id") or ""
    pt_base = data.get("playtrace_url") or "http://localhost:8000"
    sids = [c.get("session_id") for c in cycles if c.get("session_id")]
    sids_str = ", ".join(f"<code>{esc(s)}</code>" for s in sids) or "—"
    modified = data.get("modified_assets") or []
    mod_str = ", ".join(f"<code>{esc(asset_short(m))}</code>" for m in modified) or "—"
    parts = ['<footer class="links"><div><strong>관련 자료</strong></div>']
    if converge_sid:
        parts.append(
            f'<div style="margin-top:6px;">· PlayTrace dashboard (converge_run session): '
            f'<a href="{esc(pt_base)}/dashboard?session={esc(converge_sid)}">{esc(converge_sid)}</a></div>'
        )
    parts.append(f'<div>· 사이클별 raw 데이터: {sids_str}</div>')
    parts.append(f'<div>· git diff: {mod_str} ({len(modified)} files)</div>')
    parts.append(
        '<div style="margin-top:12px;font-size:11px;">'
        '이 리포트는 단일 HTML 파일로 외부 의존성 0개 (CDN/JS lib 없음). 인라인 SVG + CSS만 사용. '
        '5년 뒤에도, 오프라인에서도 그대로 열림.</div></footer>'
    )
    return "".join(parts)


def render_report(data):
    title = f"bal-converge report — {data.get('timestamp_iso','')} ({data.get('reason','').upper()})"
    return (
        f'<!doctype html><html lang="ko"><head><meta charset="utf-8">'
        f'<title>{esc(title)}</title><style>{STYLE}</style></head>'
        f'<body><div class="wrap">'
        f'{render_header(data)}'
        f'{render_summary(data)}'
        f'{render_kpi_section(data)}'
        f'{render_trend(data)}'
        f'{render_cycles_table(data)}'
        f'{render_applied_knobs(data)}'
        f'{render_narrative(data)}'
        f'{render_footer(data)}'
        f'</div></body></html>'
    )


# ---------- entry point ----------

def main():
    ap = argparse.ArgumentParser(description="bal-converge HTML report generator")
    ap.add_argument("--in", dest="in_file", help="JSON input file (default: stdin)")
    ap.add_argument("--out", dest="out_file", help="HTML output file (default: stdout)")
    args = ap.parse_args()

    if args.in_file:
        with open(args.in_file, "r", encoding="utf-8") as f:
            data = json.load(f)
    else:
        data = json.load(sys.stdin)

    html_text = render_report(data)

    if args.out_file:
        os.makedirs(os.path.dirname(os.path.abspath(args.out_file)), exist_ok=True)
        with open(args.out_file, "w", encoding="utf-8") as f:
            f.write(html_text)
        print(args.out_file)
    else:
        sys.stdout.write(html_text)


if __name__ == "__main__":
    main()
