"""Streamlit dashboard for MLBalanceLogger JSONL outputs.

Run: `cd ml-training && uv run streamlit run dashboard.py`
"""
from __future__ import annotations

import json
import pathlib
from collections import defaultdict

import pandas as pd
import plotly.express as px
import streamlit as st

LOG_DIR = pathlib.Path(__file__).parent / "balance-logs"


# ---------- 데이터 로딩 ----------

@st.cache_data(show_spinner=False)
def load_episodes(path: pathlib.Path, mtime: float) -> list[dict]:
    """JSONL 파일 한 줄 = 한 episode. mtime을 cache key에 포함시켜 갱신 시 자동 무효화."""
    return [json.loads(line) for line in path.read_text().splitlines() if line.strip()]


def list_runs() -> list[pathlib.Path]:
    if not LOG_DIR.exists():
        return []
    return sorted(LOG_DIR.glob("*.jsonl"), key=lambda p: p.stat().st_mtime, reverse=True)


# ---------- 집계 헬퍼 ----------

def aggregate_damage_taken(episodes: list[dict]) -> pd.DataFrame:
    """rows: enemy, cols: kind, val: total damage."""
    agg: dict[tuple[str, str], int] = defaultdict(int)
    for e in episodes:
        for enemy, kinds in e.get("damage_taken_matrix", {}).items():
            for kind, dmg in kinds.items():
                agg[(enemy, kind)] += dmg
    if not agg:
        return pd.DataFrame()
    df = pd.DataFrame(
        [(enemy, kind, dmg) for (enemy, kind), dmg in agg.items()],
        columns=["enemy", "kind", "damage"],
    )
    return df.pivot(index="enemy", columns="kind", values="damage").fillna(0).astype(int)


def aggregate_damage_dealt(episodes: list[dict]) -> pd.DataFrame:
    """long form: skill, enemy, damage."""
    agg: dict[tuple[str, str], int] = defaultdict(int)
    for e in episodes:
        for skill, by_enemy in e.get("damage_dealt_matrix", {}).items():
            for enemy, dmg in by_enemy.items():
                agg[(skill, enemy)] += dmg
    return pd.DataFrame(
        [(s, en, d) for (s, en), d in agg.items()],
        columns=["skill", "enemy", "damage"],
    )


def aggregate_bins(episodes: list[dict]) -> pd.DataFrame:
    rows = []
    for e in episodes:
        for b in e.get("spawn_pressure_5s_bins", []):
            rows.append({**b, "episode": e["episode_index"]})
    if not rows:
        return pd.DataFrame()
    df = pd.DataFrame(rows)
    df["t_sec"] = (df["t_sec"] / 5).round() * 5
    return df.groupby("t_sec")[
        ["active_enemies_avg", "kills", "damage_dealt", "damage_taken"]
    ].mean().reset_index()


# ---------- UI ----------

st.set_page_config(page_title="Balance Dashboard", layout="wide")
st.title("⚔️ v8 Balance Dashboard")

runs = list_runs()
if not runs:
    st.warning(f"No JSONL files in {LOG_DIR}. Run inference first.")
    st.stop()

with st.sidebar:
    st.header("Run / Filter")
    run_names = [p.name for p in runs]
    selected_name = st.selectbox("Run", run_names, index=0)
    selected_path = LOG_DIR / selected_name
    episodes_all = load_episodes(selected_path, selected_path.stat().st_mtime)

    st.caption(f"{len(episodes_all)} episodes, {selected_path.stat().st_size / 1024:.1f} KB")

    causes = sorted({e["outcome"]["cause"] for e in episodes_all})
    sel_causes = st.multiselect("Cause", causes, default=causes)
    episodes = [e for e in episodes_all if e["outcome"]["cause"] in sel_causes]

    if len(episodes) >= 2:
        idx_min, idx_max = st.slider(
            "Episode index range",
            min_value=int(min(e["episode_index"] for e in episodes)),
            max_value=int(max(e["episode_index"] for e in episodes)),
            value=(
                int(min(e["episode_index"] for e in episodes)),
                int(max(e["episode_index"] for e in episodes)),
            ),
        )
        episodes = [e for e in episodes if idx_min <= e["episode_index"] <= idx_max]

    if st.button("🔄 Refresh"):
        load_episodes.clear()
        st.rerun()

if not episodes:
    st.info("Filtered set is empty — adjust sidebar filters.")
    st.stop()

st.caption(
    f"**Run**: `{selected_name}` · **Model**: `{episodes[0].get('model', '?')}` "
    f"· **Filtered**: {len(episodes)} / {len(episodes_all)} episodes"
)

# 1) KPI 카드
df_outcome = pd.json_normalize([e["outcome"] for e in episodes])
c1, c2, c3, c4 = st.columns(4)
c1.metric("평균 생존 (s)", f"{df_outcome['duration_real_sec'].mean():.1f}")
c2.metric("평균 레벨", f"{df_outcome['final_level'].mean():.2f}")
c3.metric("평균 처치", f"{df_outcome['total_kills'].mean():.1f}")
c4.metric("평균 받은 데미지", f"{df_outcome['total_damage_taken'].mean():.1f}")

st.divider()

# 2) 적 위협도
st.subheader("🛡️ 적 타입별 위협도")
threat = aggregate_damage_taken(episodes)
col_l, col_r = st.columns([3, 2])
with col_l:
    if threat.empty:
        st.info("받은 데미지 데이터 없음.")
    else:
        fig = px.imshow(
            threat,
            text_auto=True,
            aspect="auto",
            color_continuous_scale="Reds",
            labels=dict(x="공격 타입", y="적", color="누적 데미지"),
            title="받은 데미지 (적 × 공격 타입)",
        )
        st.plotly_chart(fig, use_container_width=True)

with col_r:
    death_attackers = pd.Series(
        [e["death_context"].get("last_hit_attacker") for e in episodes if e["death_context"].get("last_hit_attacker")]
    ).value_counts().reset_index()
    death_attackers.columns = ["enemy", "deaths"]
    if death_attackers.empty:
        st.info("사망 원인 데이터 없음.")
    else:
        fig = px.bar(
            death_attackers,
            x="enemy",
            y="deaths",
            title="사망 원인 빈도 (last hit)",
            color="deaths",
            color_continuous_scale="Reds",
        )
        st.plotly_chart(fig, use_container_width=True)

st.divider()

# 3) 스킬별 가한 데미지
st.subheader("⚔️ 스킬별 가한 데미지")
dealt = aggregate_damage_dealt(episodes)
if dealt.empty:
    st.info("가한 데미지 데이터 없음.")
else:
    skill_total = dealt.groupby("skill")["damage"].sum().sort_values(ascending=False)
    skill_order = skill_total.index.tolist()
    fig = px.bar(
        dealt,
        x="skill",
        y="damage",
        color="enemy",
        category_orders={"skill": skill_order},
        title=f"스킬 × 적별 누적 데미지 (총 {int(dealt['damage'].sum()):,})",
    )
    st.plotly_chart(fig, use_container_width=True)

st.divider()

# 4) 시간대별 5초 bin
st.subheader("⏱️ 시간대별 평균 (5초 bin)")
bins_avg = aggregate_bins(episodes)
if bins_avg.empty:
    st.info("bin 데이터 없음.")
else:
    fig = px.line(
        bins_avg,
        x="t_sec",
        y=["active_enemies_avg", "kills", "damage_dealt", "damage_taken"],
        markers=True,
        labels={"value": "평균값", "variable": "지표", "t_sec": "에피소드 시간 (초)"},
        title="에피소드 시간별 활성 적 / 처치 / 데미지 평균",
    )
    st.plotly_chart(fig, use_container_width=True)

st.divider()

# 5) 에피소드 표
st.subheader("📋 에피소드 표")
df_view = df_outcome.copy()
df_view.insert(0, "episode_index", [e["episode_index"] for e in episodes])
df_view["death_by"] = [
    f"{e['death_context'].get('last_hit_attacker', '?')}/{e['death_context'].get('last_hit_kind', '?')}"
    for e in episodes
]
st.dataframe(df_view, use_container_width=True, hide_index=True)
