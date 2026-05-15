# PlayTrace API 컨닝페이퍼

> 원본: 프로젝트 `docs/MANUAL.md`. 이 파일은 핵심만 추린 quick reference. 모순이 있으면 원본 우선.

## 아키텍처

```
게임 클라이언트 → POST /api/sessions, /api/logs → FastAPI(uvicorn) → SQLite
                                                                      ↑
                                                  Dashboard (1.5초 polling)
```

서버 디폴트: `http://localhost:8000`. 다른 주소면 IP/포트 교체.

## 식별자 5개

| 용어 | 설명 |
|---|---|
| `project_name` | 게임 이름. 최상위 격리 키. |
| `version` | 빌드 또는 모델 버전. project 내 분리. |
| `test_session_id` | 서버 자동 발급. 형식 `YYYYMMDD_NNN`. (project, version, 날짜) 스코프 내 증가. |
| `play_no` | 단일 세션 내 플레이 회차. 정수. **클라이언트가 관리**. 같은 게임 판의 모든 로그에는 같은 값. |
| `key` / `value` | 측정 항목 이름 + 값. 스키마 자유. |

## 엔드포인트 6개

### POST /api/sessions
요청: `{"project_name", "version", "session_name"?(nullable)}`  
응답: `{"test_session_id": "20260516_001"}`

### GET /api/sessions
쿼리: `project_name`, `version` (둘 다 선택)  
응답: 세션 객체 배열 (created_at DESC).

### POST /api/logs
요청 (모두 필수):
```
project_name, version, test_session_id,
play_no (int),
key (string), value (bool|int|float|string),
client_time (int — Unix epoch ms)
```
응답: `{"success": true|false, "message": null|"..."}`

⚠️ **검증 실패도 HTTP 200**. body의 `success` 필드 확인 필수.

**value 타입 분기** (Python `bool ⊂ int` 영향):
| JSON 입력 | 저장 컬럼 | value_type |
|---|---|---|
| `true`/`false` | `value_bool` (1/0) | `"bool"` |
| `80`, `0.75` | `value_number` (float) | `"number"` |
| `"alive"` | `value_text` | `"text"` |

### GET /api/logs/latest
대시보드 polling용. 쿼리: `project_name`*, `version`*, `test_session_id`*, `after_id`(default 0), `play_no`, `key`, `limit`(default 500, max 5000). `*`=필수.

응답: `{"items": [...], "last_id": N}`. 빈 응답이면 `last_id`는 입력 `after_id` 그대로.

### GET /api/logs/search
페이지네이션. 쿼리: 모두 선택 — `project_name`, `version`, `test_session_id`, `play_no`, `key`, `start_time`, `end_time`(ms), `page`(default 1), `size`(default 5000, max 5000).

응답: `{"items":[...], "page":N, "size":M}`.

### GET /health
`{"status":"ok"}`.

## LogItem 필드 해석

`/api/logs/latest`와 `/api/logs/search` 응답의 item은 value 컬럼 4개를 모두 포함. `value_type`으로 분기:

| value_type | 사용 필드 |
|---|---|
| `"number"` | `value_number` (float) |
| `"text"` | `value_text` (string) |
| `"bool"` | `value_bool` (0 또는 1) |
| 나머지 | null |

## 안티패턴 (반드시 회피)

| 실수 | 결과 | 수정 |
|---|---|---|
| 같은 key에 number/text 섞기 | 차트 결손 | key 설계 시 타입 통일 |
| `client_time`을 초로 전송 | X축이 1970년 | `int(time.time()*1000)` 또는 `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()` |
| `play_no` 무질서 | 회차 필터 망가짐 | 게임 판마다 일정한 값 |
| `success` 미확인 | 조용한 데이터 소실 | 항상 `resp["success"]` 검사 |
| batch/retry queue | MVP 비목표 | 단건 fire-and-forget만 |

## 대시보드

URL: `http://localhost:8000/dashboard`

화면 구성:
- 셀렉터 캐스케이드: Project → Version → Session
- 필터: play_no, key
- chip: key 다중, session 다중
- 로그 테이블 (최근 1000건, client_time DESC)
- 차트 (Chart.js, **number 타입만** 플로팅)

차트 동작:
- X축 client_time(ms) → HH:MM:SS
- `text`/`bool` 은 차트 안 나옴 → 테이블에서 확인
- key chip 없으면 빈 차트

다중 세션 비교:
| 모드 | 색상 | 선 |
|---|---|---|
| 단일 (chip 1개) | key별 팔레트 | 실선 |
| 다중 (chip 2+) | 세션별 팔레트 | key별 dash 패턴 |

세션 chip이 `opacity:0.4`면 polling 실패 (5회 재시도, 서버 복구 시 자동 복원).

## 미지원 기능 (요청 금지)

- 인증/API Key (내부망 전용)
- WebSocket push (1.5초 polling만)
- Batch insert
- 서버사이드 통계/집계 API
- PostgreSQL (SQLite 고정)
- Replay / Alert / Anomaly Detection
- 데이터 자동 retention (수동 삭제)

## DB 스키마 (참고)

`logs` 컬럼:
```
id (PK, INT AUTOINCREMENT)
project_name, version, test_session_id (TEXT)
play_no (INT)
key (TEXT)
value_type (TEXT)  -- "number"/"text"/"bool"
value_number (REAL), value_text (TEXT), value_bool (INT)
client_time, server_received_at (INT, ms)
```

인덱스: `(project, version, session)`, `(..., key)`, `(..., play_no)`, `(client_time)`

`sessions` 컬럼:
```
id (PK), project_name, version, test_session_id, session_name (nullable), created_at
```

## Unity C# 통합 최소 스니펫

```csharp
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class PlayTraceClient : MonoBehaviour
{
    public string BaseUrl = "http://localhost:8000";
    public string SessionId { get; private set; }
    public bool IsReady => !string.IsNullOrEmpty(SessionId);
    private string _project, _version;

    public void BeginSession(string project, string version, string name)
    {
        _project = project; _version = version;
        StartCoroutine(CreateSessionCo(name));
    }

    private IEnumerator CreateSessionCo(string name)
    {
        string body = "{\"project_name\":\"" + _project + "\",\"version\":\"" + _version
            + "\",\"session_name\":" + (name == null ? "null" : "\"" + Escape(name) + "\"") + "}";
        using (var req = MakePost(BaseUrl + "/api/sessions", body))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                // very simple parse — production code should use a JSON lib
                string t = req.downloadHandler.text;
                int i = t.IndexOf("\"test_session_id\""); if (i < 0) yield break;
                int s = t.IndexOf('"', i + 17) + 1;
                int e = t.IndexOf('"', s);
                SessionId = t.Substring(s, e - s);
            }
        }
    }

    public void Log(int playNo, string key, object value)
    {
        if (!IsReady) return;
        StartCoroutine(SendLogCo(playNo, key, value));
    }

    private IEnumerator SendLogCo(int playNo, string key, object value)
    {
        long t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string vJson = value switch {
            null => "null",
            bool b => b ? "true" : "false",
            int i => i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            long l => l.ToString(System.Globalization.CultureInfo.InvariantCulture),
            float f => f.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
            double d => d.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
            string s => "\"" + Escape(s) + "\"",
            _ => "\"" + Escape(value.ToString()) + "\""
        };
        string body = "{\"project_name\":\"" + _project + "\",\"version\":\"" + _version
            + "\",\"test_session_id\":\"" + SessionId + "\",\"play_no\":" + playNo
            + ",\"key\":\"" + Escape(key) + "\",\"value\":" + vJson
            + ",\"client_time\":" + t + "}";
        using (var req = MakePost(BaseUrl + "/api/logs", body))
        {
            yield return req.SendWebRequest();
            // body의 success 검증 권장
        }
    }

    private static UnityWebRequest MakePost(string url, string body)
    {
        var r = new UnityWebRequest(url, "POST");
        r.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        r.downloadHandler = new DownloadHandlerBuffer();
        r.SetRequestHeader("Content-Type", "application/json");
        return r;
    }

    private static string Escape(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            switch (c) {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
```

⚠️ 위 스니펫은 시작점일 뿐. 실제 통합에서는:
- success 필드 검증 추가
- 첫 실패 1회만 경고 (spam 방지)
- 세션 미준비 시 드롭 정책
- 학습 모드(time-scale 가속) 시 throttle 또는 disable 옵션
