using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using copilot_usage_maui.Models;

namespace copilot_usage_maui.Services;

/// <summary>토큰 만료 전용 예외. UI에서 구분하여 재인증 안내 표시.</summary>
class ClaudeTokenExpiredException : InvalidOperationException
{
    public ClaudeTokenExpiredException(string message) : base(message) { }
}

class ClaudeUsageService
{
    readonly SettingsService _settings;
    readonly HttpClient _http = new();

    // ─── Cache ────────────────────────────────────────────────────────────────
    ClaudeUsageSnapshot? _cachedSnapshot;
    DateTime _cacheTimestamp = DateTime.MinValue;
    static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);
    readonly SemaphoreSlim _fetchLock = new(1, 1);

    static readonly string CacheFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "copilot-usage-maui", "claude-usage-cache.json");

    public ClaudeUsageService(SettingsService settings)
    {
        _settings = settings;
        LoadCacheFromDisk();
    }

    // ─── Public entry point ─────────────────────────────────────────────────

    public async Task<ClaudeUsageSnapshot> GetUsageSnapshotAsync(bool forceRefresh = false)
    {
        // 캐시 히트 (lock 없이 빠른 체크)
        if (!forceRefresh && _cachedSnapshot is not null
            && (DateTime.UtcNow - _cacheTimestamp) < CacheTtl)
            return _cachedSnapshot;

        await _fetchLock.WaitAsync();
        try
        {
            // Double-check after lock
            if (!forceRefresh && _cachedSnapshot is not null
                && (DateTime.UtcNow - _cacheTimestamp) < CacheTtl)
                return _cachedSnapshot;

            int method = _settings.ClaudeAuthMethod;
            var result = method switch
            {
                1 => await GetUsageViaOAuthAsync(),
                2 => await GetUsageViaCliAsync(),
                _ => await GetUsageAutoAsync()
            };

            _cachedSnapshot = result;
            _cacheTimestamp = DateTime.UtcNow;
            SaveCacheToDisk(result);
            return result;
        }
        finally { _fetchLock.Release(); }
    }

    async Task<ClaudeUsageSnapshot> GetUsageAutoAsync()
    {
        Exception? oauthEx = null;
        try { return await GetUsageViaOAuthAsync(); }
        catch (ClaudeTokenExpiredException) { throw; } // 만료는 CLI fallback 없이 즉시 전파
        catch (Exception ex) { oauthEx = ex; }

        try { return await GetUsageViaCliAsync(); }
        catch (Exception cliEx)
        {
            string combined = oauthEx is not null
                ? $"OAuth: {oauthEx.Message}\nCLI: {cliEx.Message}"
                : cliEx.Message;
            throw new InvalidOperationException(combined, cliEx);
        }
    }

    /// <summary>설정 화면 "인증 확인" 버튼용 진단</summary>
    public async Task<string> DiagnoseAsync()
    {
        var sb = new System.Text.StringBuilder();

        string credPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", ".credentials.json");

        if (!File.Exists(credPath))
        {
            sb.AppendLine("✗ credentials 파일 없음");
            sb.AppendLine($"  경로: {credPath}");
        }
        else
        {
            sb.AppendLine("✓ credentials 파일 존재");
            try
            {
                string fileJson = await File.ReadAllTextAsync(credPath);
                var file = JsonSerializer.Deserialize<ClaudeCredentialsFile>(fileJson);
                var creds = file?.ClaudeAiOauth;

                if (creds is null)
                    sb.AppendLine("✗ claudeAiOauth 필드 없음");
                else if (string.IsNullOrEmpty(creds.AccessToken))
                    sb.AppendLine("✗ accessToken 비어있음");
                else if (creds.IsExpired)
                    sb.AppendLine($"✗ 토큰 만료됨 (expiresAt: {DateTimeOffset.FromUnixTimeMilliseconds(creds.ExpiresAt):yyyy-MM-dd HH:mm} UTC)");
                else
                {
                    sb.AppendLine($"✓ 토큰 유효 (plan: {creds.SubscriptionType}, tier: {creds.RateLimitTier})");
                    var scopes = creds.Scopes;
                    sb.AppendLine($"  scopes: [{string.Join(", ", scopes)}]");
                    if (!scopes.Contains("user:profile"))
                        sb.AppendLine("  ⚠ user:profile 스코프 없음 — usage API 접근 불가할 수 있음");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"✗ 파일 읽기 실패: {ex.Message}");
            }
        }

        string? claudePath = ResolveClaudePath(_settings.ClaudeCliPath);
        if (claudePath is null)
            sb.AppendLine("✗ Claude CLI 없음 (PATH에서 찾지 못함)");
        else
            sb.AppendLine($"✓ Claude CLI: {claudePath}");

        return sb.ToString().Trim();
    }

    // ─── OAuth via api.anthropic.com (HttpClient 직접 호출) ─────────────────

    async Task<ClaudeUsageSnapshot> GetUsageViaOAuthAsync()
    {
        var (creds, isExpired) = await ReadCredentialsAsync();
        if (creds is null)
        {
            if (isExpired)
                throw new ClaudeTokenExpiredException(
                    "Claude OAuth 토큰이 만료되었습니다.");
            throw new InvalidOperationException(
                "Claude OAuth 토큰을 찾을 수 없습니다. 'claude' 명령으로 로그인하세요.");
        }

        // api.anthropic.com은 표준 API — Cloudflare TLS 핑거프린트 검사 없음
        // WebView2 불필요, HttpClient로 직접 호출 가능
        using var request = new HttpRequestMessage(
            HttpMethod.Get, "https://api.anthropic.com/api/oauth/usage");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", creds.AccessToken);
        request.Headers.Add("anthropic-beta", "oauth-2025-04-20");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var response = await _http.SendAsync(request, cts.Token);

        string json = await response.Content.ReadAsStringAsync(cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            // 429 + 캐시 있음 → 캐시 반환 (에러 표시 안 함)
            if ((int)response.StatusCode == 429 && _cachedSnapshot is not null)
                return _cachedSnapshot;

            string msg = (int)response.StatusCode == 429
                ? "API 요청 한도 초과 (429). 잠시 후 다시 시도하세요."
                : $"HTTP {(int)response.StatusCode}: {Truncate(json)}";
            throw new HttpRequestException(msg);
        }

        var snapshot = ParseOAuthUsageResponse(json);
        return snapshot with { Plan = creds.SubscriptionType ?? creds.RateLimitTier };
    }

    async Task<(ClaudeOAuthCredentials? Creds, bool IsExpired)> ReadCredentialsAsync()
    {
        string credPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", ".credentials.json");

        if (!File.Exists(credPath)) return (null, false);

        try
        {
            string json = await File.ReadAllTextAsync(credPath);
            var file = JsonSerializer.Deserialize<ClaudeCredentialsFile>(json);
            var creds = file?.ClaudeAiOauth;

            if (creds is null || string.IsNullOrEmpty(creds.AccessToken)) return (null, false);
            if (creds.IsExpired) return (null, true);

            return (creds, false);
        }
        catch
        {
            return (null, false);
        }
    }

    // GET https://api.anthropic.com/api/oauth/usage 응답 파싱
    static ClaudeUsageSnapshot ParseOAuthUsageResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("API 응답이 비어있습니다.");

        try
        {
            var resp = JsonSerializer.Deserialize<ClaudeUsageApiResponse>(json);
            if (resp is null)
                throw new InvalidOperationException(
                    $"API 응답 파싱 실패 (null)\nRaw: {Truncate(json)}");

            var session = ToRateWindow(resp.FiveHour, 300);
            var weekly = ToRateWindow(resp.SevenDay, 10080);

            var modelWindows = new Dictionary<string, ClaudeRateWindow>();
            if (resp.SevenDayOpus is not null)
                modelWindows["Claude Opus (7d)"] = ToRateWindow(resp.SevenDayOpus, 10080)!;
            if (resp.SevenDaySonnet is not null)
                modelWindows["Claude Sonnet (7d)"] = ToRateWindow(resp.SevenDaySonnet, 10080)!;

            // 모든 필드가 null이면 원본 응답 포함 (디버깅용)
            if (session is null && weekly is null && modelWindows.Count == 0)
                throw new InvalidOperationException(
                    $"API 응답에 사용량 데이터가 없습니다.\nRaw: {Truncate(json)}");

            return new ClaudeUsageSnapshot(session, weekly, modelWindows, null, null, null, DateTime.UtcNow);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"JSON 파싱 오류: {ex.Message}\nRaw: {Truncate(json)}", ex);
        }
    }

    static string Truncate(string s) => s.Length > 300 ? s[..300] + "..." : s;

    static ClaudeRateWindow? ToRateWindow(ClaudeUsageWindow? w, int windowMinutes)
    {
        if (w is null) return null;
        var resetsAt = w.ResetsAtUtc;
        return new ClaudeRateWindow(w.Utilization, windowMinutes, resetsAt, BuildResetDescription(resetsAt));
    }

    static string BuildResetDescription(DateTime? resetsAt)
    {
        if (resetsAt is null) return "";
        var remaining = resetsAt.Value - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero) return "Resetting...";
        if (remaining.TotalDays >= 1)
            return $"{(int)remaining.TotalDays}d {remaining.Hours}h {remaining.Minutes}m";
        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
        return $"{remaining.Minutes}m";
    }

    // ─── CLI fallback ────────────────────────────────────────────────────────

    async Task<ClaudeUsageSnapshot> GetUsageViaCliAsync()
    {
        string claudePath = ResolveClaudePath(_settings.ClaudeCliPath)
            ?? throw new InvalidOperationException(
                "Claude CLI를 찾을 수 없습니다. 'claude' 명령이 PATH에 있는지 확인하거나, 설정에서 경로를 지정하세요.");

        var psi = new ProcessStartInfo
        {
            FileName = claudePath,
            Arguments = "usage --format json",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Claude CLI 프로세스를 시작하지 못했습니다.");

        string stdout = await process.StandardOutput.ReadToEndAsync(cts.Token);
        string stderr = await process.StandardError.ReadToEndAsync(cts.Token);
        await process.WaitForExitAsync(cts.Token);

        if (!string.IsNullOrWhiteSpace(stdout) && stdout.TrimStart().StartsWith('{'))
            return ParseCliJsonOutput(stdout);

        string combined = stdout + "\n" + stderr;
        return ParseCliTextOutput(combined);
    }

    static ClaudeUsageSnapshot ParseCliJsonOutput(string json)
    {
        try { return ParseOAuthUsageResponse(json); }
        catch { return EmptySnapshot(); }
    }

    static ClaudeUsageSnapshot ParseCliTextOutput(string text)
    {
        text = Regex.Replace(text, @"\x1B\[[0-9;]*[mK]", "");

        ClaudeRateWindow? session = null;
        ClaudeRateWindow? weekly = null;
        string? email = null;
        string? plan = null;

        var pctMatches = Regex.Matches(text, @"(\d+(?:\.\d+)?)\s*%");
        var timeMatch = Regex.Match(text, @"(\d+)\s*h(?:ours?)?\s*(\d+)\s*m(?:in)?");

        DateTime? resetsAt = null;
        if (timeMatch.Success)
        {
            int hours = int.Parse(timeMatch.Groups[1].Value);
            int mins = int.Parse(timeMatch.Groups[2].Value);
            resetsAt = DateTime.UtcNow.AddHours(hours).AddMinutes(mins);
        }

        var emailMatch = Regex.Match(text, @"[\w.+-]+@[\w-]+\.[\w.-]+");
        if (emailMatch.Success) email = emailMatch.Value;

        foreach (var p in new[] { "max", "pro", "team", "free" })
            if (text.Contains(p, StringComparison.OrdinalIgnoreCase)) { plan = p; break; }

        if (pctMatches.Count > 0 && double.TryParse(pctMatches[0].Groups[1].Value, out double pct1))
            session = new ClaudeRateWindow(pct1, 300, resetsAt, BuildResetDescription(resetsAt));

        if (pctMatches.Count > 1 && double.TryParse(pctMatches[1].Groups[1].Value, out double pct2))
            weekly = new ClaudeRateWindow(pct2, 10080, null, "");

        return new ClaudeUsageSnapshot(session, weekly, [], email, plan, null, DateTime.UtcNow);
    }

    static ClaudeUsageSnapshot EmptySnapshot()
        => new(null, null, [], null, null, null, DateTime.UtcNow);

    /// <summary>외부에서 Claude CLI 경로를 확인할 때 사용.</summary>
    public static string? FindClaudePath() => ResolveClaudePath(null);

    static string? ResolveClaudePath(string? customPath)
    {
        // 1. 사용자 지정 경로
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
            return customPath;

        // 2. where 명령어로 OS에 위임 (PATH + PATHEXT 전체 검색)
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "claude",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc is not null)
            {
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(3000);
                if (proc.ExitCode == 0)
                {
                    string? firstLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault()?.Trim();
                    if (!string.IsNullOrEmpty(firstLine) && File.Exists(firstLine))
                        return firstLine;
                }
            }
        }
        catch { /* where 명령 실패 시 무시 */ }

        // 3. 잘 알려진 경로 (PATH에 등록 안 한 사용자용 폴백)
        var wellKnown = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "bin", "claude.exe"),
            @"C:\Program Files\nodejs\claude.cmd",
            @"C:\Program Files\nodejs\claude.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "npm", "claude.cmd"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "claude", "claude.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude", "local", "claude.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AnthropicClaude", "claude.exe"),
        };
        return wellKnown.FirstOrDefault(File.Exists);
    }

    // ─── Disk cache ───────────────────────────────────────────────────────────

    void LoadCacheFromDisk()
    {
        try
        {
            if (!File.Exists(CacheFilePath)) return;
            string json = File.ReadAllText(CacheFilePath);
            var snapshot = JsonSerializer.Deserialize<ClaudeUsageSnapshot>(json);
            if (snapshot is not null)
            {
                _cachedSnapshot = snapshot;
                _cacheTimestamp = File.GetLastWriteTimeUtc(CacheFilePath);
            }
        }
        catch { /* 디스크 캐시 로드 실패 시 무시 */ }
    }

    void SaveCacheToDisk(ClaudeUsageSnapshot snapshot)
    {
        try
        {
            string? dir = Path.GetDirectoryName(CacheFilePath);
            if (dir is not null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(snapshot);
            File.WriteAllText(CacheFilePath, json);
        }
        catch { /* 디스크 캐시 저장 실패 시 무시 */ }
    }
}
