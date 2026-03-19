using copilot_usage_maui.Models;

#if WINDOWS
using Microsoft.Toolkit.Uwp.Notifications;
#endif

namespace copilot_usage_maui.Services;

class NotificationService
{
    // 중복 알림 방지 — 윈도우별 마지막 발송 레벨 (0=없음, 80=주의, 90=위험)
    // 리셋 시 초기화를 위해 리셋 시각도 기록
    readonly Dictionary<string, (int Level, DateTime? ResetsAt)> _lastNotified = new();

    public void CheckAndNotify(ClaudeUsageSnapshot snapshot)
    {
        if (snapshot.SessionWindow is { } sw)
            CheckWindow("session", sw);
        if (snapshot.WeeklyWindow is { } ww)
            CheckWindow("weekly", ww);
    }

    void CheckWindow(string key, ClaudeRateWindow window)
    {
        // 리셋이 지났으면 기록 초기화
        if (_lastNotified.TryGetValue(key, out var last) &&
            last.ResetsAt.HasValue &&
            DateTime.UtcNow >= last.ResetsAt.Value)
        {
            _lastNotified.Remove(key);
        }

        int currentLevel = window.UsedPercent >= 90 ? 90
            : window.UsedPercent >= 80 ? 80
            : 0;

        if (currentLevel == 0) return;

        int lastLevel = _lastNotified.TryGetValue(key, out var l) ? l.Level : 0;
        if (currentLevel <= lastLevel) return; // 이미 같은 or 높은 레벨 알림 발송됨

        _lastNotified[key] = (currentLevel, window.ResetsAt);

        string windowLabel = key == "session"
            ? (AppStrings_IsKorean ? "세션 (5시간)" : "Session (5h)")
            : (AppStrings_IsKorean ? "주간 (7일)" : "Weekly (7d)");

        string resetInfo = window.TimeUntilReset is { } tr && tr > TimeSpan.Zero
            ? AppStrings_IsKorean
                ? $"리셋까지 {(int)tr.TotalHours}시간 {tr.Minutes}분"
                : $"Resets in {(int)tr.TotalHours}h {tr.Minutes}m"
            : "";

        if (currentLevel >= 90)
        {
            string title = AppStrings_IsKorean
                ? $"Claude 한도 임박 — {windowLabel}"
                : $"Claude Near Limit — {windowLabel}";
            string msg = AppStrings_IsKorean
                ? $"사용량 {window.UsedPercent:F0}% 도달. 사용을 자제하세요. {resetInfo}"
                : $"Usage at {window.UsedPercent:F0}%. Consider pausing. {resetInfo}";
            SendToast(title, msg);
        }
        else
        {
            string title = AppStrings_IsKorean
                ? $"Claude 사용량 주의 — {windowLabel}"
                : $"Claude Usage Warning — {windowLabel}";
            string msg = AppStrings_IsKorean
                ? $"사용량이 {window.UsedPercent:F0}%에 도달했습니다. {resetInfo}"
                : $"Usage reached {window.UsedPercent:F0}%. {resetInfo}";
            SendToast(title, msg);
        }
    }

    static bool AppStrings_IsKorean
    {
        get
        {
            int pref = Microsoft.Maui.Storage.Preferences.Default.Get("language_preference", 0);
            if (pref == 1) return false;
            if (pref == 2) return true;
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko";
        }
    }

    static void SendToast(string title, string message)
    {
#if WINDOWS
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();
        }
        catch
        {
            // 토스트 실패는 무시 (알림 권한 없을 수 있음)
        }
#endif
    }
}
