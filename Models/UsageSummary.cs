namespace copilot_usage_maui.Models;

record UsageSummary(
    int Quota,
    string PlanName,
    double MtdUsed,
    double Remaining,
    double PercentConsumed,
    int DaysElapsed,
    int DaysRemaining,
    double AvgDailyUsage,
    double TodayUsed,
    bool ProjectedOverQuota,
    DateOnly? ProjectedRunOutDate,
    List<DailyUsage> RecentDays,
    Dictionary<string, double> ModelBreakdown
);
