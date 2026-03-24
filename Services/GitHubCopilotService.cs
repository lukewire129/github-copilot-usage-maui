using System.Diagnostics;
using copilot_usage_maui.Models;

namespace copilot_usage_maui.Services;

class GitHubCopilotService
{
    readonly HttpClient _http = new();

    static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(1);
    UsageSummary? _cachedSummary;
    int _cachedMonths;
    DateTime _cacheTimestamp;

    public async Task<string> GetGhTokenAsync()
    {
        string ghPath = ResolveGhPath()
            ?? throw new InvalidOperationException(
                "gh CLI를 찾을 수 없습니다. https://cli.github.com 에서 설치 후 'gh auth login -h github.com -s user -w'을 실행하세요.");

        var psi = new ProcessStartInfo
        {
            FileName = ghPath,
            Arguments = "auth token",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("gh 프로세스를 시작하지 못했습니다.");
        string token = (await process.StandardOutput.ReadToEndAsync()).Trim();
        await process.WaitForExitAsync();
        if (string.IsNullOrEmpty(token))
            throw new InvalidOperationException("gh auth token이 비어 있습니다. 'gh auth login -h github.com -s user -w'을 먼저 실행하세요.");
        return token;
    }

    static string? ResolveGhPath()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), "gh.exe");
            if (File.Exists(candidate)) return candidate;
        }

        var wellKnown = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GitHub CLI", "gh.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "GitHub CLI", "gh.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "GitHub CLI", "gh.exe"),
            @"C:\Program Files\GitHub CLI\gh.exe",
        };
        return wellKnown.FirstOrDefault(File.Exists);
    }

    public async Task<string> GetUsernameAsync(string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        SetHeaders(req, token);
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("login").GetString()
            ?? throw new InvalidOperationException("Could not read username from GitHub API");
    }

    /// <summary>
    /// gh auth refresh를 실행합니다.
    /// 코드가 감지되면 onCodeFound 콜백을 호출하고, 프로세스는 사용자가 브라우저에서 인증을 완료할 때까지 계속 대기합니다.
    /// 인증 완료(프로세스 종료) 후 반환됩니다.
    /// </summary>
    public async Task<string> RefreshGhAuthAsync(Action<string>? onCodeFound = null)
    {
        string ghPath = ResolveGhPath()
            ?? throw new InvalidOperationException("gh CLI를 찾을 수 없습니다. https://cli.github.com 에서 설치하세요.");

        var psi = new ProcessStartInfo
        {
            FileName = ghPath,
            Arguments = "auth refresh -h github.com -s user",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("gh 프로세스를 시작하지 못했습니다.");
        process.StandardInput.AutoFlush = true;

        var output = new System.Text.StringBuilder();
        bool codeReported = false;

        // stderr를 끝까지 계속 읽어야 프로세스가 블록되지 않음
        var stderrTask = Task.Run(async () =>
        {
            var buf = new char[256];
            var chunk = new System.Text.StringBuilder();
            while (!process.StandardError.EndOfStream)
            {
                int read = await process.StandardError.ReadAsync(buf, 0, buf.Length);
                if (read == 0) break;
                var text = new string(buf, 0, read);
                output.Append(text);
                chunk.Append(text);

                string current = chunk.ToString();

                // 코드 발견 시 콜백 호출 (프로세스는 계속 실행 유지)
                if (!codeReported)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(current, @"\b([A-Z0-9]{4}-[A-Z0-9]{4})\b");
                    if (match.Success)
                    {
                        codeReported = true;
                        onCodeFound?.Invoke(match.Groups[1].Value);
                    }
                }
                // break 하지 않음 — 프로세스가 인증 완료까지 대기하므로 stderr를 계속 소비해야 함
            }
        });

        var stdoutTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                output.AppendLine(line);
        });

        await Task.WhenAll(stderrTask, stdoutTask);
        await process.WaitForExitAsync();

        if (!codeReported)
            throw new InvalidOperationException($"인증 코드를 받지 못했습니다. gh auth refresh 실패.\n출력: {output}");

        return output.ToString().Trim();
    }

    public async Task<(string planName, int entitlement)> GetCopilotPlanAsync(string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/copilot_internal/user");
        SetHeaders(req, token);
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var rawJson = await resp.Content.ReadAsStringAsync();
        Debug.WriteLine($"[copilot_internal/user] {rawJson}");
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        string plan = root.TryGetProperty("copilot_plan", out var planEl) ? planEl.GetString() ?? "" : "";
        string planName = plan switch
        {
            "individual" => "Pro",
            "individual_pro" => "Pro Plus",
            _ => plan
        };

        int entitlement = 0;
        if (root.TryGetProperty("premium_interactions", out var piEl) &&
            piEl.TryGetProperty("entitlement", out var entEl) &&
            entEl.TryGetInt32(out var ent))
            entitlement = ent;

        return (planName, entitlement);
    }

    public async Task<UsageSummary> GetUsageSummaryAsync(int months, bool forceRefresh = false)
    {
        if (!forceRefresh && _cachedSummary is not null && _cachedMonths == months &&
            DateTime.UtcNow - _cacheTimestamp < CacheTtl)
            return _cachedSummary;
        string token = await GetGhTokenAsync();
        string username = await GetUsernameAsync(token);

        string planName = "";
        int quota = 0;
        try { (planName, quota) = await GetCopilotPlanAsync(token); } catch { }

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Fetch current month aggregate (MTD total + quota)
        var (mtdUsed, quotaFromApi, mtdModels) = await FetchMonthAsync(token, username, today.Year, today.Month);
        if (quota <= 0 && quotaFromApi > 0) quota = (int)quotaFromApi;
        if (quota <= 0) quota = 300;

        // Fetch recent 14 days for sparkline
        var recentDays = new List<DailyUsage>();
        var startDay = Math.Max(1, today.Day - 13);
        var tasks = new List<Task<(DateOnly date, double total, Dictionary<string, double> models)>>();
        for (int d = startDay; d <= today.Day; d++)
        {
            var date = new DateOnly(today.Year, today.Month, d);
            tasks.Add(FetchDayAsync(token, username, today.Year, today.Month, d, date));
        }
        var dayResults = await Task.WhenAll(tasks);
        foreach (var (date, total, models) in dayResults.OrderBy(r => r.date))
            recentDays.Add(new DailyUsage(date, total, models));

        // Fetch previous months if needed
        for (int i = 1; i < months; i++)
        {
            var prev = new DateOnly(today.Year, today.Month, 1).AddMonths(-i);
            await FetchMonthAsync(token, username, prev.Year, prev.Month);
        }

        var result = CalculateSummary(recentDays, mtdUsed, quota, planName, today, mtdModels);
        _cachedSummary = result;
        _cachedMonths = months;
        _cacheTimestamp = DateTime.UtcNow;
        return result;
    }

    async Task<(double total, double quota, Dictionary<string, double> models)> FetchMonthAsync(
        string token, string username, int year, int month)
    {
        var url = $"https://api.github.com/users/{username}/settings/billing/premium_request/usage?year={year}&month={month}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        SetHeaders(req, token);
        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        return ParsePeriodResponse(json);
    }

    async Task<(DateOnly date, double total, Dictionary<string, double> models)> FetchDayAsync(
        string token, string username, int year, int month, int day, DateOnly date)
    {
        var url = $"https://api.github.com/users/{username}/settings/billing/premium_request/usage?year={year}&month={month}&day={day}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        SetHeaders(req, token);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return (date, 0, []);
        var json = await resp.Content.ReadAsStringAsync();
        var (total, _, models) = ParsePeriodResponse(json);
        return (date, total, models);
    }

    static (double total, double quota, Dictionary<string, double> models) ParsePeriodResponse(string json)
    {
        var models = new Dictionary<string, double>();
        double total = 0;
        double quota = 0;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Try top-level quota fields
        foreach (var key in new[] { "total_monthly_quota", "monthly_quota", "quota" })
            if (root.TryGetProperty(key, out var qp) && qp.TryGetDouble(out var q))
                quota = q;

        // Find usageItems array (may be nested under various keys)
        JsonElement? items = FindArray(root);
        if (items is null) return (total, quota, models);

        foreach (var item in items.Value.EnumerateArray())
        {
            double qty = 0;
            foreach (var key in new[] { "grossQuantity", "quantity", "gross_quantity", "total", "used", "requests", "count" })
                if (item.TryGetProperty(key, out var qp) && qp.TryGetDouble(out var q) && q > 0)
                { qty = q; break; }

            if (qty == 0) continue;
            total += qty;

            string? model = null;
            foreach (var key in new[] { "model", "model_name", "name" })
                if (item.TryGetProperty(key, out var mp) && mp.GetString() is { Length: > 0 } m)
                { model = m; break; }

            if (model != null)
                models[model] = models.GetValueOrDefault(model) + qty;

            foreach (var key in new[] { "total_monthly_quota", "monthly_quota", "quota" })
                if (item.TryGetProperty(key, out var qp) && qp.TryGetDouble(out var q) && q > 0)
                    quota = q;
        }

        return (total, quota, models);
    }

    static JsonElement? FindArray(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Array) return el;
        if (el.ValueKind != JsonValueKind.Object) return null;

        foreach (var key in new[] { "usageItems", "data", "usage", "items", "results", "entries", "days", "models" })
            if (el.TryGetProperty(key, out var child) &&
                (child.ValueKind == JsonValueKind.Array || child.ValueKind == JsonValueKind.Object))
                return FindArray(child);

        return null;
    }

    static UsageSummary CalculateSummary(List<DailyUsage> recentDays, double mtdUsed, int quota, string planName, DateOnly today, Dictionary<string, double>? mtdModels = null)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        double todayUsed = recentDays.FirstOrDefault(d => d.Date == today)?.TotalRequests ?? 0;

        int daysElapsed = today.DayNumber - monthStart.DayNumber + 1;
        int totalDaysInMonth = monthEnd.DayNumber - monthStart.DayNumber + 1;
        int daysRemaining = totalDaysInMonth - daysElapsed;

        double avgDailyUsage = daysElapsed > 0 ? mtdUsed / daysElapsed : 0;
        double projectedTotal = avgDailyUsage * totalDaysInMonth;
        bool projectedOverQuota = projectedTotal > quota;

        DateOnly? projectedRunOutDate = null;
        if (avgDailyUsage > 0)
        {
            double daysUntilRunOut = (quota - mtdUsed) / avgDailyUsage;
            if (daysUntilRunOut >= 0)
                projectedRunOutDate = today.AddDays((int)Math.Ceiling(daysUntilRunOut));
        }

        // mtdModels가 있으면 이달 전체 기준, 없으면 recentDays(최근 14일)에서 집계
        var modelBreakdown = mtdModels is { Count: > 0 }
            ? new Dictionary<string, double>(mtdModels)
            : recentDays.Aggregate(new Dictionary<string, double>(), (acc, day) =>
            {
                foreach (var kv in day.ModelBreakdown)
                    acc[kv.Key] = acc.GetValueOrDefault(kv.Key) + kv.Value;
                return acc;
            });

        return new UsageSummary(
            Quota: quota,
            PlanName: planName,
            MtdUsed: mtdUsed,
            Remaining: Math.Max(0, quota - mtdUsed),
            PercentConsumed: quota > 0 ? mtdUsed / quota * 100 : 0,
            DaysElapsed: daysElapsed,
            DaysRemaining: daysRemaining,
            AvgDailyUsage: avgDailyUsage,
            TodayUsed: todayUsed,
            ProjectedOverQuota: projectedOverQuota,
            ProjectedRunOutDate: projectedRunOutDate,
            RecentDays: recentDays,
            ModelBreakdown: modelBreakdown
        );
    }

    static void SetHeaders(HttpRequestMessage req, string token)
    {
        req.Headers.Authorization = new AuthenticationHeaderValue("token", token);
        req.Headers.UserAgent.ParseAdd("copilot-usage-maui/1.0");
        req.Headers.Accept.ParseAdd("application/vnd.github+json");
        req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
    }
}
