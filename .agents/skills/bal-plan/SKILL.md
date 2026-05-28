---
name: bal-plan
description: /bal-converge 를 돌리기 **전에** 방향성을 정하는 사전 분석 skill. 기존 converge 리포트 이력 + 최근 PlayTrace baseline 후보 + 메모리에 누적된 함정(예 enemy_damage 정수 round-trip, Ghost burst spawn 60s cutoff)을 한 번에 훑은 뒤, 사용자와 짧은 Q&A 로 KPI / knob 화이트리스트 / N / max_iter 를 합의해 `playbalance/plans/plan_<ts>.md` 한 장으로 정리하고 **그대로 실행 가능한 `/bal-converge ...` 명령어**까지 추천한다. 사용자가 "/bal-plan", "bal plan", "방향성 정하기", "bal-converge 전에 계획", "수렴 전 분석", "튜닝 방향 잡기" 같은 표현을 쓰면 사용. 1~2시간짜리 본격 사이클을 잘못된 KPI/knob 으로 태우는 사고를 막기 위해, `/bal-converge` 호출 직전에 한 번 돌리는 것을 강하게 권장.
disable-model-invocation: false
---

# bal-plan: bal-converge 직전 방향성 합의

`/bal-converge` 는 N×max_iter 만큼의 플레이를 자동으로 태우는 무인 사이클이다 (보통 1~2시간). 그래서 시작 전에 KPI·knob·예산을 잘못 잡으면 시간이 통째로 날아간다. 이 skill 은 그 직전 단계에서:

1. 이전 converge 리포트들이 무엇을 했는지 (pass/abort 이유, 어떤 knob 을 만졌는지) 훑고,
2. 최근 PlayTrace baseline 후보를 정리해 보여주고,
3. 메모리에 누적된 함정 (예: 정수 round-trip 흡수, structural spawn cutoff) 을 사용자에게 상기시키고,
4. 짧은 Q&A 로 방향을 합의한 뒤,
5. `playbalance/plans/plan_<ts>.md` 1 파일과 **그대로 실행할 수 있는 `/bal-converge` 호출 명령** 을 출력한다.

이 skill 자체는 자산을 건드리지 않는다. 무인 사이클을 돌리지도 않는다. **단발성, single-pass.** 산출물은 plan 파일 + 추천 명령어 한 줄이고, 그 명령어는 사용자가 직접 실행한다.

## 큰 그림

```
[1] 사전 점검 (PlayTrace health, config 파일 존재)
        ↓
[2] Inventory phase — playbalance/reports/converge_*.html 최근 3~5개 파싱
       · REASON 분포 / 자주 만진 knob / 미해결 패턴 정리
       · 첫 실행이라 비어있어도 graceful skip
        ↓
[3] Baseline 후보 스캔 — PlayTrace /api/sessions 최근 ~7일
       · session_id, n_plays, mean Dur, max Dur, mean Lvl, end-cause 분포 한 줄씩
       · 재사용 가능한 후보 vs 새로 측정 가능성 둘 다 살림
        ↓
[4] Trap surfacing — memory + .Codex/bal-apply.json knob 목록 교집합
       · 예: 사용자가 enemy_damage 만지고 싶다 → "정수 round-trip 흡수 주의" 경고
        ↓
[5] 사용자 Q&A (AskUserQuestion 1~2회)
       · KPI (config default 그대로 / 수정)
       · Baseline (재사용 / 새로 측정)
       · Knob whitelist (전체 허용 / 일부 제외)
       · 예산 N, max_iter (smoke / 본격)
        ↓
[5.5] Timeout 자동 추론 (Q&A 없음)
       · baseline max Dur × 2 (안전계수), config default 로 cap
       · 추론 근거 한 줄을 plan 본문에 박음
        ↓
[6] Plan 파일 작성 + 추천 명령 출력
       · playbalance/plans/plan_<YYYYMMDD-HHMMSS>.md
       · 마지막 줄에 copy-paste 가능한 `/bal-converge kpi=... N=... max_iter=... timeout=...m`
```

## 인자 파싱

`/bal-plan` 뒤 자유 문자열에서 (전부 옵션):

| 변수 | 의미 | 디폴트 |
|---|---|---|
| `mode=smoke\|full` | smoke = N≤2 max_iter=1 (파이프라인 검증), full = config default | full |
| `kpi="..."` | 사용자가 미리 정한 KPI. Q&A 에서 confirm only | config `default_kpi` |
| `baseline=<session_id>\|new\|auto` | 재사용 / 새 측정 / 자동 (최근 24h 있으면 재사용, 없으면 new) | auto |
| `timeout=<10m/600s>` | 한 판 timeout. 명시하면 자동 추론 skip 하고 그대로 사용 | Phase 4.5 에서 자동 추론 |

위 인자가 들어와 있으면 해당 항목의 Q&A 는 건너뛰고 한 줄 confirm 만. 인자 없이 그냥 `/bal-plan` 이면 모든 항목을 Q&A 로 받는다.

추출 직후 한 줄 보고:
```
[bal-plan] mode=full, kpi="<from arg or default>", baseline=auto, timeout=auto
```

## 사전 점검

```bash
# (1) PlayTrace
curl -s -m 3 http://localhost:8000/health | grep -q '"ok"' \
  || die "PlayTrace 서버 응답 없음 (http://localhost:8000). 서버를 띄운 뒤 다시 실행하세요."

# (2) /bal-converge 관련 config 들 — bal-plan 산출물의 의미가 있으려면 converge 가 실제 동작해야 함
[ -f .Codex/bal-converge.json ] || die "/bal-converge config 없음 (.Codex/bal-converge.json). /bal-converge 를 한 번 실행해 seed 를 만든 뒤 다시 시도."
[ -f .Codex/bal-apply.json ]    || die "/bal-apply config 없음 (.Codex/bal-apply.json). /bal-apply 의 cold start 를 먼저 통과시키세요."
[ -f .Codex/bal-run.json ]      || die "/bal-run config 없음. /bal-run 을 한 번 돌려 config 를 만든 뒤 다시 시도."

# (3) plans 폴더 (없으면 만들기 — 첫 실행)
mkdir -p playbalance/plans
```

서버 down 같은 1차 실패는 한 줄 안내만 하고 종료. 자동 복구·재시도 안 함 (사용자가 서버를 띄우는 게 빠르다).

## Phase 1: 리포트 이력 inventory

목표: 이전 converge 사이클들이 무엇을 시도했고 어떤 패턴으로 끝났는지 사용자에게 상기.

```bash
# git toplevel 기준 절대 경로로 — 현재 shell cwd 와 무관하게 같은 결과
PROJ_ROOT=$(git rev-parse --show-toplevel 2>/dev/null || pwd)
REPORTS=$(ls -t "$PROJ_ROOT/playbalance/reports/"converge_*.html 2>/dev/null | head -5)
COUNT=$(echo "$REPORTS" | grep -c . || true)
```

> **cwd 함정 주의**: 사이클 도중 다른 디렉토리로 옮긴 적이 있거나 subagent context 에서 cwd 가 프로젝트 루트가 아니면 `ls playbalance/reports/...` 가 silent empty 를 반환해 "리포트 없음" 으로 잘못 판단한다. 항상 `$PROJ_ROOT` 절대 경로로 검사. 만약 위 명령이 0개를 돌려주면 `find "$PROJ_ROOT" -name 'converge_*.html' -path '*/playbalance/reports/*'` 로 한 번 더 확인.

- `$COUNT = 0` → "이전 converge 리포트가 없습니다. 첫 plan 작성입니다." 한 줄만 출력하고 Phase 2 로 진행.
- `$COUNT >= 1` → 각 파일에서 timestamp, reason 을 파일명에서 추출 (`converge_YYYYMMDD-HHMMSS_<reason>.html`). 가장 최근 1~2개는 더 자세히 보기 위해 HTML 본문을 read 해서 KPI 표현식 / 사이클 수 / 만진 knob 목록을 뽑는다.

리포트 HTML 안에는 make_report.py 가 inline 으로 박아둔 JSON payload 가 있다 (한 번 read 후 `<script id="report-data" type="application/json">` 같은 패턴 또는 본문의 KPI 헤더 라인). 정확한 위치는 `.Codex/skills/bal-converge/make_report.py` 참고. 파싱이 까다로우면 fallback 으로 사람이 읽을 수 있는 헤더 텍스트만 인용해도 충분.

요약 출력 예:
```
이전 converge 이력 (최근 3개):
  2026-05-22 16:19 — REASON=max_iter, KPI="Dur 180~240 AND Lvl>=5", iters=1, 만진 knob: enemy_damage
  2026-05-21 14:30 — REASON=apply_abort_3, KPI=default,            iters=0 (dirty 자산)
  2026-05-20 22:11 — REASON=diverged,    KPI="Dur>=120",            iters=3, 만진 knob: enemy_hp, spawn_rate

관찰 패턴:
  - 최근 3회 중 pass 0회. KPI 가 baseline 에서 너무 멀거나 knob 매칭이 부족할 가능성.
  - enemy_damage 가 자주 등장하지만 정수 round-trip 흡수 함정이 있음 (memory 참조).
```

"관찰 패턴" 은 모델이 데이터를 보고 1~3 bullet 으로 직접 적는다. hallucination 금지 — 실제 본 데이터에서만.

## Phase 2: PlayTrace baseline 후보 스캔

> **PlayTrace API 메서드 주의**: `/api/sessions`, `/api/logs/search` 둘 다 **GET** + query string (POST 가 아님). POST 로 보내면 `405 Method Not Allowed`. MANUAL.md 와 충돌하는 인상이 있으면 항상 MANUAL.md 가 우선이지만, 본 skill 의 모든 조회는 GET 으로 통일.

```bash
# 최근 7일 sessions
SINCE_MS=$(python3 -c "import time; print(int((time.time() - 7*86400)*1000))")
SESSIONS=$(curl -s -m 5 "http://localhost:8000/api/sessions?project_name=$(jq -r .project_name .Codex/bal-run.json)&size=50" \
  | python3 -c "
import json, sys, time
data = json.load(sys.stdin)
items = data.get('items', data) if isinstance(data, dict) else data
out = []
for s in items[:20]:
    sid = s.get('test_session_id') or s.get('session_id') or s.get('id')
    name = s.get('session_name','')
    created = s.get('created_at') or s.get('client_time') or 0
    out.append({'id': sid, 'name': name, 'created': created})
print(json.dumps(out))
")
```

> **MANUAL.md 우선**: `/api/sessions` 의 정확한 필드명·쿼리 파라미터·페이지네이션 규칙은 `PlayTrace/docs/MANUAL.md` 에서 1차 확인. 위 코드는 일반 패턴이며 필드 이름이 다르면 MANUAL.md 에 맞춰 수정.

각 후보 session 에 대해 한 줄 요약 (logs/search 로 mean Dur / max Dur / Lvl / end-cause 분포):

```
Baseline 후보 (최근 7일, completed plays >= 3 인 것만):
  20260522_005  N=6  Dur=63.5±5.1s (max 72.1)  Lvl=3.2  end=Gear(83%) Ghost(17%)   ← bal-converge 16:19 의 baseline 재사용본
  20260521_009  N=5  Dur=72.1±8.3s (max 89.4)  Lvl=3.4  end=Gear(60%) Ghost(40%)
  20260520_018  N=10 Dur=58.0±2.1s (max 61.2)  Lvl=2.9  end=Gear(100%)              ← σ/μ=3.6%, structural cutoff 의심
```

**max Dur 는 Phase 4.5 timeout 추론에 사용되므로 반드시 함께 추출**. `max(value_number)` 만 추가하면 됨.

후보가 0개면 "재사용 가능한 baseline 없음, 새 측정 권장 (`baseline=new`)" 안내.

## Phase 3: Trap surfacing — memory ∩ available knobs

memory index (`~/.Codex/projects/.../memory/MEMORY.md`) 의 feedback/project 항목 중 **이 프로젝트의 knob 과 직접 관계 있는 것** 을 골라 사용자에게 상기.

`.Codex/bal-apply.json` 의 knob id 목록을 먼저 추출:

```bash
KNOB_IDS=$(jq -r '.knobs[].id' .Codex/bal-apply.json)
```

알려진 trap 매핑 (모델이 메모리에서 읽어와 매번 적용):

| Trap | Knob 영향 |
|---|---|
| 정수 round-trip 흡수 (enemy_damage=3 에 ±15% → no-op) | `enemy_damage` 등 정수 field |
| Ghost burst spawn 60s structural cutoff | `spawn_rate`, `spawn_intensity_curve` |
| (필요 시 새 trap 을 memory 에서 추가로 발견하면 합류) | |

출력 예:
```
주의 (메모리 트랩):
  · enemy_damage: 자산 값이 작은 정수면 ±15% 가 반올림으로 흡수돼 no-op. 이 knob 을 whitelist 에 둘 거면 max_iter 를 1 늘리거나 adjust_range 를 조정.
  · spawn_rate / spawn_intensity_curve: 60s 단위로 burst spawn (Ghost 변종 7개의 t=60 burst). Dur 늘리는 게 목표면 stat 만 만져선 못 뚫음 — spawn timing 도 후보로.
```

매핑되는 trap 이 0개면 이 섹션 통째로 skip (출력 없음).

## Phase 4: Q&A — KPI / Baseline / Whitelist / 예산

`AskUserQuestion` 으로 한 번에 묶어서 받는다. 인자로 이미 들어온 항목은 question 에서 제외.

**Question 1: KPI**
- config default 그대로 (`<default_kpi>`)
- 수정 (사용자 입력)

**Question 2: Baseline**
- 재사용 (Phase 2 후보 중 가장 최근 + 통계 충실한 것)
- 새 측정 (사이클 시작 시 `/bal-run N회`)

**Question 3: Knob whitelist**
- 전체 허용 (`.Codex/bal-apply.json` 의 모든 knob)
- 일부 제외 (Phase 3 의 trap 으로 경고된 것 제외 권장)
- 단일 knob 만 (가설 검증 모드)

**Question 4: 예산**
- smoke (N=2, max_iter=1) — 파이프라인 검증만, 시그널 기대 X
- 표준 (N=5, max_iter=3) — 약 30~60분, 작은 KPI 변화 가능
- 본격 (N=5, max_iter=5) — 약 60~120분, KPI 도달 시도

> bal-apply 의 `min_runs_for_analysis=3` 때문에 N=2 smoke 는 iter 2 진입 시 자연 abort 한다 (memory: bal-converge smoke 제약). 그래서 smoke 옵션의 label 에 "1 iter 한정" 명시.

knob whitelist 선택을 .Codex/bal-apply.json 의 실제 knob id 목록과 다른 이름으로 추측하지 말 것. 사용자가 "Gear 만" 같이 자연어로 답하면 knob id 와 매칭되는 후보를 한 번 더 confirm.

## Phase 4.5: Timeout 자동 추론

`/bal-converge` 의 `timeout=` 인자 (한 판 hang 방어 cap) 를 데이터로부터 자동 계산. **사용자에게 묻지 않음.** Q&A 부담 줄이고 정상 케이스에 영향 없는 안전 값을 plan 본문에 reasoning 과 함께 명시.

### 추론 룰

```
config_default  = .Codex/bal-converge.json: default_per_play_timeout (예: "10m" → 600s)

if 인자로 timeout=<X> 명시됨:
    recommended = X   # override, skip 추론

elif Phase 2 에서 baseline 후보 1개 이상 있고 max_dur 추출됨:
    candidate = ceil(max_dur_sec * 2.0 / 60)   # 분 단위, 최근 max × 2 안전계수
    candidate = max(candidate, 5)              # 최소 5분 (hang 감지 의미 있는 하한)
    recommended = min(candidate, config_default_min)  # config default 보다 길어지지 않게 cap

else:  # 첫 plan or baseline=new or Phase 2 데이터 부족
    recommended = config_default
```

**reasoning** (plan 본문에 적을 한 줄):
- override 케이스: "사용자 명시 `timeout=<X>`"
- 추론 케이스: "baseline max Dur <D>s × 2 = <recommended>분 (config default <C>분보다 작음)"
- 또는 cap 케이스: "baseline max Dur <D>s × 2 = <X>분 이 config default <C>분 초과 → <C>분 사용"
- default 케이스: "baseline 데이터 없음 → config default <C>분 사용"

### 게임-비종속성

- max Dur × 2 안전계수는 게임에 무관한 일반적 룰 (정상 판은 절대 timeout 안 걸리고, hang 만 잡힘).
- ML-Agents 처럼 MaxStep × fixedDeltaTime hard cap 이 있는 환경에서는 max Dur 가 자연스럽게 timer cap 부근에 모이므로 × 2 가 충분히 보수적.
- 무한정 늘어날 수 있는 일반 게임에서는 config default 가 cap 역할 → 추론값이 그보다 커지지 않게 막아둠.

### 출력 예

```
[bal-plan] timeout 추론: baseline max Dur 89.4s → 3분 권장, config default 10분과 비교해 작음 → 3분 채택
```

또는

```
[bal-plan] timeout 추론: baseline 데이터 없음 → config default 10m 그대로
```

## Phase 5: Plan 파일 작성 + 추천 명령

```bash
TS=$(date +%Y%m%d-%H%M%S)
PLAN="playbalance/plans/plan_${TS}.md"
```

Write 도구로 `$PLAN` 작성. 권장 구조 (헤더는 고정, 본문은 채워진 데이터만):

```markdown
# bal-converge plan — <YYYY-MM-DD HH:MM>

## 1. Context

### 이전 리포트 이력
- 2026-05-22 16:19 — REASON=max_iter, KPI="...", iters=1, knob: enemy_damage
- ... (Phase 1 의 표 그대로)

### 관찰 패턴
- (Phase 1 의 bullet 들)

### Baseline 후보 (PlayTrace 최근 7일)
- (Phase 2 의 표)

## 2. Direction

### KPI
`<선택된 KPI>`

이유: (사용자가 default 그대로면 "config default 사용". 수정했으면 그 이유를 Q&A 답변에서 추출.)

### Baseline
재사용: `<session_id>` (N=<n>, mean Dur=<d>s, Lvl=<l>)  *또는*  새 측정 예정.

### Knob whitelist
허용 (<k>개):
- enemy_damage — (label / desc from bal-converge.json display_labels)
- ...

제외 (이유):
- spawn_intensity_curve — AnimationCurve 수동 편집 대상 (auto 안 됨)
- (Phase 3 trap 으로 제외한 것 있으면 같이 명시)

### 예산
N=<n>, max_iter=<m>, per-play timeout=<t>분. 예상 wall-time ≈ <m × (n × per-play 평균 + apply 30s)>.

**Timeout 근거**: <Phase 4.5 reasoning 한 줄 그대로>. 정상 케이스에선 hit 안 됨, hang 감지용 안전망.

## 3. Hypothesis priority

(사용자가 Q&A 에서 가설을 언급했거나 이전 리포트의 미해결 항목이 있으면 우선순위 bullet 으로. 없으면 이 섹션 생략.)

1. ...
2. ...

## 4. Traps (주의)

- (Phase 3 trap 그대로 — plan 파일에 박아 둬야 사이클 중 사용자가 다시 확인 가능)

## 5. Recommended command

```
/bal-converge kpi="<KPI>" N=<n> max_iter=<m> timeout=<t>m
```

> `timeout` 은 항상 포함. 추론값이 config default 와 같아도 명시 (사용자가 plan 만 봐도 어떤 cap 으로 돌아가는지 알 수 있어야 함).
>
> Baseline 재사용 케이스에는 별도 인자가 없음 — bal-converge 가 자체적으로 직전 24h session 을 재사용한다 (`baseline=reuse` 가 들어가면 더 명시적이지만 v1 의 bal-converge 가 이미 자동 처리).

## 6. Post-run checklist

- [ ] HTML 리포트가 `playbalance/reports/converge_<ts>_<reason>.html` 에 생성됐는지
- [ ] REASON 이 pass 가 아니면 이 plan 의 가설 / KPI 가 적절했는지 회고
- [ ] git diff 로 만진 자산을 확인 후 commit / rollback 결정
```

작성 후 사용자에게:

```
✅ Plan 작성 완료: playbalance/plans/plan_<ts>.md

다음 단계 — 아래 명령을 그대로 실행:

  /bal-converge kpi="<KPI>" N=<n> max_iter=<m>
```

마지막 줄의 명령어는 **literal copy-paste 가능한 한 줄**. pseudocode 금지, 변수 placeholder 금지. 사용자가 그대로 붙여넣기만 하면 동작해야 함.

## 게임-비종속

이 skill 구조는 게임 정보 0:
- 리포트 이력은 `playbalance/reports/` 파일명 패턴만 의존
- baseline 후보는 PlayTrace API + `.Codex/bal-run.json:project_name` 만 사용
- knob 목록은 `.Codex/bal-apply.json` 에서 동적으로 읽음
- KPI 디폴트 / 라벨은 `.Codex/bal-converge.json` 에서 읽음

다른 게임에 이식할 때 SKILL.md 그대로 복사하면 됨. 게임 특유의 trap 매핑만 그 프로젝트의 memory 에서 가져옴.

## 함정 (이 skill 자체)

- **single-pass 룰**: bal-plan 은 loop 가 아니다. plan 작성 → 사용자에게 명령 추천 → 종료. `/bal-converge` 를 자기가 실행하지 말 것 (사용자 의도 / 시간 / 자산 변경에 대한 책임 분리). 사용자가 plan 본 뒤 마음 바꾸면 그냥 안 돌리면 됨.
- **planning 자체가 무거워지지 말 것**: Phase 1~3 은 read-only 데이터 수집. 합쳐서 1~2분 이내. 사용자가 "그냥 default 로 돌릴게" 하면 Q&A 4 개로 묶어서 한 번에 끝낸다 (옵션 미리 채워서 빠른 confirm).
- **knob id hallucination 금지**: whitelist 작성 시 반드시 `.Codex/bal-apply.json` 의 실제 id 만 사용. "Gear 약화" 같은 자연어를 받으면 어떤 knob id 와 매칭되는지 한 번 더 사용자에게 confirm.
- **이전 리포트 깊이 분석 욕심 금지**: Phase 1 은 표 + 패턴 bullet 까지만. 사용자가 "왜 그게 diverged 됐는지 더 봐줘" 라고 명시하면 그때 깊게. plan skill 의 spec 밖.
- **Phase 1 의 HTML 파싱이 실패하면**: 파일명 + mtime + reason 만 가지고 진행. 본문 read 가 실패해도 plan 작성은 계속 가능. 절대 die 하지 말 것.
- **plan 파일이 너무 길어지지 말 것**: 사용자가 다시 읽지 않는 문서는 가치 없음. 한 plan = 한 화면 (~80~120 줄) 목표. 이전 리포트 인용은 표 한 개, narrative 는 bullet 위주.
- **추천 명령의 인자는 bal-converge 가 실제 지원하는 것만**: kpi=, N=, max_iter=, timeout=, max_wall=. `baseline=`, `whitelist=` 같은 가짜 인자 만들지 말 것 (bal-converge.SKILL.md 의 인자 파싱 표 참조). whitelist 는 bal-apply.json 직접 편집으로만 가능 — plan 에서 권하는 제외 항목은 사용자가 수동으로 config 수정해야 한다고 명시.
- **Timeout 추론을 사용자에게 묻지 말 것**: Phase 4.5 는 데이터로부터 자동 계산하는 단계지 Q&A 항목이 아니다. Q&A 부담 4개로 유지. 사용자가 `timeout=` 을 인자로 명시한 경우만 추론 skip 하고 그대로 사용. 추론값이 잘못됐다고 느끼면 사용자가 plan 보고 명령어 수동 수정 가능.
- **Timeout 추론값이 0 이거나 음수가 되지 않게**: max Dur 가 0~1s 인 비정상 baseline (test session 등) 도 가능. 항상 `max(추론값, 5분)` 하한 강제. 또 항상 `min(추론값, config default)` cap.

## 참조 자료

- `.Codex/skills/bal-converge/SKILL.md` — 인자 문법, KPI 표현식 syntax, 산출물 위치
- `.Codex/skills/bal-apply/SKILL.md` — knob 구조, 자동/수동 분기
- `.Codex/skills/bal-run/SKILL.md` — `chart_keys`, `play_end_key` 컨벤션
- `.Codex/bal-converge.json` — default_kpi, display_labels
- `.Codex/bal-apply.json` — knob 목록 (whitelist 선택의 단일 진실 소스)
- `.Codex/bal-run.json` — `project_name` (PlayTrace 쿼리), `play_end_key`
- `playbalance/README.md` — 폴더 컨벤션 (`plans/` 도 같은 규약)
- `PlayTrace/docs/MANUAL.md` — `/api/sessions`, `/api/logs/search` payload·필드의 단일 진실 소스
- `~/.Codex/projects/.../memory/MEMORY.md` — 누적 trap 인덱스
