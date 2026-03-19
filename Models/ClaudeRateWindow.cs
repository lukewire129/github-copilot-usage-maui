namespace copilot_usage_maui.Models;

record ClaudeRateWindow(
    double UsedPercent,       // 0-100
    int WindowMinutes,        // 300(5h) or 10080(7d)
    DateTime? ResetsAt,       // UTC
    string ResetDescription   // "Resets in 2h 15m"
)
{
    public double RemainingPercent => Math.Max(0, 100 - UsedPercent);
    public bool IsExhausted => UsedPercent >= 100;
    public bool IsNearlyExhausted => UsedPercent >= 90;

    /// <summary>경과 비율 (0~1). ResetsAt 기반으로 계산.</summary>
    public double ElapsedRatio
    {
        get
        {
            if (ResetsAt is null || WindowMinutes <= 0) return 0;
            var totalSpan = TimeSpan.FromMinutes(WindowMinutes);
            var remaining = ResetsAt.Value - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) return 1.0;
            if (remaining >= totalSpan) return 0.0;
            return 1.0 - remaining / totalSpan;
        }
    }

    /// <summary>이 속도로 계속 쓰면 리셋 시점의 예상 사용률 (0~200+).</summary>
    public double ProjectedFinalPercent
    {
        get
        {
            double elapsed = ElapsedRatio;
            if (elapsed <= 0.001) return UsedPercent;
            return UsedPercent / elapsed;
        }
    }

    public TimeSpan? TimeUntilReset
        => ResetsAt.HasValue ? ResetsAt.Value - DateTime.UtcNow : null;
}
