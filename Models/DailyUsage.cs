namespace copilot_usage_maui.Models;

record DailyUsage(DateOnly Date, double TotalRequests, Dictionary<string, double> ModelBreakdown);
