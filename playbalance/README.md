# playbalance/

밸런싱 작업의 산출물을 모아두는 폴더. 강화학습 모델(`Assets/ML-Models/`)·학습 결과(`ml-training/results/`)·skill config(`.claude/bal-*.json`) 같은 기존 자산은 그대로 둔 채, **새로 생기는 분석 리포트**를 한 곳에 쌓는다.

git tracked — 다음 세션에 `git pull` 만 해도 이전 밸런싱 이력이 그대로 복원되도록.

## 폴더 구조

```
playbalance/
├── README.md                       # 이 파일
└── reports/
    ├── _sample_*.html              # 레이아웃 예시 (underscore prefix = 실제 실행 결과 아님)
    └── <type>_<날짜>-<시각>[_<reason>].html
```

## 파일명 규칙

```
<type>_<YYYYMMDD>-<HHMMSS>[_<reason>].html
```

| 필드 | 값 |
|---|---|
| `type` | `converge` / `run` / `apply` |
| `YYYYMMDD` | 리포트 생성 날짜 |
| `HHMMSS` | 리포트 생성 시각 |
| `reason` | bal-converge 종료 사유 (`pass`, `max_iter`, `max_wall`, `diverged`, `no_op`, `apply_abort_2`, `apply_abort_3`, `bal_run_failed`) |

예시:
- `converge_20260522-144530_pass.html` — bal-converge KPI 도달
- `converge_20260522-153012_diverged.html` — distance 발산으로 중단
- `converge_20260522-160245_apply_abort_2.html` — bal-apply config 없음으로 사이클 0 종료

날짜순 정렬이 타입 무관하게 한 번에 됨 → `ls -t reports/` 로 최근 작업 흐름 한눈에.

## 어느 skill 이 어떤 리포트를 쓰나

| Skill | 쓰는 리포트 |
|---|---|
| `/bal-converge` | `converge_*.html` — KPI 자동 수렴 사이클 전체 기록 (구현됨) |
| `/bal-run` | `run_*.html` (예정 — 현재 미구현) |
| `/bal-apply` | `apply_*.html` (예정 — 현재 미구현) |

## 리포트 형식

자립 단일 HTML 파일:
- 외부 의존성 0개 — 인라인 SVG + 인라인 CSS, JS 라이브러리 없음
- 오프라인에서, 5년 뒤에도, GitHub preview 에서도 그대로 열림
- 한 파일 보통 20~80KB

레이아웃 예시: [`reports/_sample_converge_20260522-144530_pass.html`](reports/_sample_converge_20260522-144530_pass.html)

리포트 구성:
- 상단 요약 (결과 배지, 사이클 수, 소요 시간, 변경 knob 수)
- KPI 게이지 (목표 구간 + 현재값 마커)
- distance 추이 차트
- 사이클별 표
- 적용 변경 이력 (knob → asset.field, from→to, why)
- 해석/메모 (3박스: 💬 대화 요약 / 📊 데이터 분석 / 🎯 결론+후속 가설)
- footer (PlayTrace dashboard 드릴다운 링크, 영향 자산 git diff)

## 보는 법

브라우저로 더블클릭, 또는:
```bash
open playbalance/reports/converge_20260522-144530_pass.html
```

라이브 드릴다운 (per-play 시계열 등 raw 데이터) 은 리포트 footer 의 PlayTrace dashboard 링크로.
