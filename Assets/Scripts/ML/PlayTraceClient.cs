#region

using System;
using System.Collections;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

#endregion

namespace RGame.MLAgents
{
    /// <summary>
    ///     PlayTrace 서버(FastAPI + SQLite, http://localhost:8000)로 게임 로그를 단건 전송하는 HTTP 클라이언트.
    ///     - BeginSession: 코루틴으로 POST /api/sessions → SessionId 저장.
    ///     - Log: fire-and-forget POST /api/logs. SessionId 미준비 시 드롭.
    ///     - 학습 모드(time-scale=20)에서도 UnityWebRequest는 실시간 처리되어 정상 동작.
    /// </summary>
    public class PlayTraceClient : MonoBehaviour
    {
        [SerializeField] private string _baseUrl = "http://localhost:8000";

        public string BaseUrl => _baseUrl;
        public string ProjectName { get; private set; }
        public string Version { get; private set; }
        public string SessionId { get; private set; }
        public bool IsReady => !string.IsNullOrEmpty(SessionId);

        private bool _droppedWarningShown;
        private bool _failureWarningShown;

        public void BeginSession(string projectName, string version, string sessionName)
        {
            ProjectName = projectName;
            Version = version;
            StartCoroutine(CreateSessionCoroutine(sessionName));
        }

        public void Log(int playNo, string key, object value)
        {
            if (!IsReady)
            {
                if (!_droppedWarningShown)
                {
                    Debug.LogWarning("[PlayTrace] SessionId 미준비 — log drop (key=" + key + "). 이후 동일 경고 생략.");
                    _droppedWarningShown = true;
                }
                return;
            }
            StartCoroutine(SendLogCoroutine(playNo, key, value));
        }

        private IEnumerator CreateSessionCoroutine(string sessionName)
        {
            string body = BuildSessionBody(ProjectName, Version, sessionName);
            using (var req = BuildPostRequest(_baseUrl + "/api/sessions", body))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("[PlayTrace] 세션 생성 실패: " + req.error + " (body=" + req.downloadHandler.text + ")");
                    yield break;
                }
                string id = ExtractStringField(req.downloadHandler.text, "test_session_id");
                if (string.IsNullOrEmpty(id))
                {
                    Debug.LogError("[PlayTrace] 세션 응답에 test_session_id 없음: " + req.downloadHandler.text);
                    yield break;
                }
                SessionId = id;
                Debug.Log("[PlayTrace] session created: " + SessionId + " (project=" + ProjectName + " version=" + Version + ")");
            }
        }

        private IEnumerator SendLogCoroutine(int playNo, string key, object value)
        {
            long clientTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string body = BuildLogBody(ProjectName, Version, SessionId, playNo, key, value, clientTime);
            using (var req = BuildPostRequest(_baseUrl + "/api/logs", body))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                {
                    WarnOnce("[PlayTrace] 로그 전송 실패(network): " + req.error);
                    yield break;
                }
                // PlayTrace는 검증 실패도 HTTP 200으로 반환 — body.success 확인 필요.
                string respText = req.downloadHandler != null ? req.downloadHandler.text : "";
                if (respText.IndexOf("\"success\":true", StringComparison.Ordinal) < 0)
                {
                    WarnOnce("[PlayTrace] 로그 전송 실패(server): " + respText);
                }
            }
        }

        private void WarnOnce(string msg)
        {
            if (_failureWarningShown) return;
            _failureWarningShown = true;
            Debug.LogWarning(msg + " — 이후 동일 경고 생략.");
        }

        private static UnityWebRequest BuildPostRequest(string url, string jsonBody)
        {
            var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            return req;
        }

        // ============= JSON 빌더 =============

        private static string BuildSessionBody(string project, string version, string sessionName)
        {
            var sb = new StringBuilder(128);
            sb.Append('{');
            AppendKV(sb, "project_name", project); sb.Append(',');
            AppendKV(sb, "version", version); sb.Append(',');
            sb.Append("\"session_name\":");
            if (sessionName == null) sb.Append("null"); else AppendString(sb, sessionName);
            sb.Append('}');
            return sb.ToString();
        }

        private static string BuildLogBody(
            string project, string version, string sessionId, int playNo,
            string key, object value, long clientTime)
        {
            var sb = new StringBuilder(256);
            sb.Append('{');
            AppendKV(sb, "project_name", project); sb.Append(',');
            AppendKV(sb, "version", version); sb.Append(',');
            AppendKV(sb, "test_session_id", sessionId); sb.Append(',');
            sb.Append("\"play_no\":").Append(playNo.ToString(CultureInfo.InvariantCulture)).Append(',');
            AppendKV(sb, "key", key); sb.Append(',');
            sb.Append("\"value\":");
            AppendJsonValue(sb, value);
            sb.Append(',');
            sb.Append("\"client_time\":").Append(clientTime.ToString(CultureInfo.InvariantCulture));
            sb.Append('}');
            return sb.ToString();
        }

        private static void AppendKV(StringBuilder sb, string key, string value)
        {
            sb.Append('"').Append(key).Append("\":");
            if (value == null) sb.Append("null"); else AppendString(sb, value);
        }

        private static void AppendJsonValue(StringBuilder sb, object value)
        {
            if (value == null) { sb.Append("null"); return; }
            // bool은 int의 서브클래스가 C#에는 없지만 PlayTrace 서버 측 분기 순서를 따라 명시.
            switch (value)
            {
                case bool b: sb.Append(b ? "true" : "false"); return;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case float f:
                    if (float.IsNaN(f) || float.IsInfinity(f)) sb.Append("null");
                    else sb.Append(f.ToString("F4", CultureInfo.InvariantCulture));
                    return;
                case double d:
                    if (double.IsNaN(d) || double.IsInfinity(d)) sb.Append("null");
                    else sb.Append(d.ToString("F4", CultureInfo.InvariantCulture));
                    return;
                case string s: AppendString(sb, s); return;
                default: AppendString(sb, value.ToString()); return;
            }
        }

        private static void AppendString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append(string.Format(CultureInfo.InvariantCulture, "\\u{0:X4}", (int)c));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // 매우 단순한 string 필드 추출. JsonUtility는 동적 응답 파싱이 어렵고 Newtonsoft 의존성을 피하기 위함.
        private static string ExtractStringField(string json, string field)
        {
            if (string.IsNullOrEmpty(json)) return null;
            string marker = "\"" + field + "\"";
            int i = json.IndexOf(marker, StringComparison.Ordinal);
            if (i < 0) return null;
            i = json.IndexOf(':', i + marker.Length);
            if (i < 0) return null;
            // skip whitespace
            i++;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t')) i++;
            if (i >= json.Length || json[i] != '"') return null;
            int start = i + 1;
            int end = json.IndexOf('"', start);
            if (end < 0) return null;
            return json.Substring(start, end - start);
        }
    }
}
