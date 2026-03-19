using System.Threading;

namespace copilot_usage_maui.Services;

/// <summary>
/// Claude.ai API를 WebView2(Chromium)를 통해 호출하는 서비스.
/// HttpClient는 Cloudflare TLS 핑거프린트 검사에서 차단되므로,
/// WebView2를 통해 실제 Chromium TLS로 요청하여 우회한다.
///
/// 통신 방식: WebView2 PostMessage (window.chrome.webview.postMessage)
/// - JS에서 fetch 완료 후 postMessage로 결과 전달
/// - MauiProgram.cs의 WebViewHandler 매핑에서 CoreWebView2.WebMessageReceived 수신
/// - HandleWebMessage()로 TCS 완료
/// </summary>
class ClaudeWebFetchService
{
    // ClaudeDashBoardPage.OnMounted()에서 등록 — HTML을 WebView에 로드하는 콜백
    Action<string>? _setHtmlCallback;

    // fetch 결과를 기다리는 TCS (PostMessage로 완료)
    TaskCompletionSource<string>? _fetchTcs;

    readonly SemaphoreSlim _lock = new(1, 1);

    public bool IsReady => _setHtmlCallback is not null;

    /// <summary>ClaudeDashBoardPage에서 마운트 시 등록</summary>
    public void RegisterSetHtmlCallback(Action<string> callback)
        => _setHtmlCallback = callback;

    /// <summary>페이지 언마운트 시 등록 해제</summary>
    public void Unregister() => _setHtmlCallback = null;

    /// <summary>
    /// WebView2의 CoreWebView2.WebMessageReceived에서 호출.
    /// JS에서 window.chrome.webview.postMessage(msg)로 전송된 메시지 처리.
    /// </summary>
    public void HandleWebMessage(string message)
    {
        if (message.StartsWith("ok:"))
            _fetchTcs?.TrySetResult(message[3..]);
        else if (message.StartsWith("err:"))
            _fetchTcs?.TrySetException(new HttpRequestException(message[4..]));
    }

    /// <summary>
    /// WebView2(Chromium)를 통해 url에 Bearer 토큰으로 GET 요청을 보내고
    /// 응답 JSON 문자열을 반환한다.
    /// </summary>
    public async Task<string> FetchJsonAsync(string url, string bearerToken)
    {
        if (_setHtmlCallback is null)
            throw new InvalidOperationException(
                "WebView not registered. Claude 탭을 먼저 열어주세요.");

        await _lock.WaitAsync();
        try
        {
            _fetchTcs = new TaskCompletionSource<string>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            string html = BuildFetchHtml(url, bearerToken);
            _setHtmlCallback(html);

            return await _fetchTcs.Task.WaitAsync(TimeSpan.FromSeconds(30));
        }
        finally
        {
            _fetchTcs = null;
            _lock.Release();
        }
    }

    /// <summary>
    /// fetch()를 실행하는 HTML 조각.
    /// 성공 시 window.chrome.webview.postMessage('ok:' + json),
    /// 실패 시 window.chrome.webview.postMessage('err:' + errorMsg).
    /// </summary>
    static string BuildFetchHtml(string url, string token)
    {
        string safeToken = token.Replace("'", "\\'").Replace("\n", "").Replace("\r", "");
        return $@"<!DOCTYPE html><html><body><script>
fetch('{url}', {{
  signal: AbortSignal.timeout(25000),
  headers: {{
    'Authorization': 'Bearer {safeToken}',
    'Accept': 'application/json'
  }},
  credentials: 'include'
}})
.then(function(r) {{ if (!r.ok) throw new Error('HTTP ' + r.status); return r.text(); }})
.then(function(t) {{ window.chrome.webview.postMessage('ok:' + t); }})
.catch(function(e) {{ window.chrome.webview.postMessage('err:' + e.message); }});
</script></body></html>";
    }
}
