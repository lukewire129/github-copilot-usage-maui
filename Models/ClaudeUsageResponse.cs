using System.Text.Json.Serialization;

namespace copilot_usage_maui.Models;

/// <summary>GET https://api.anthropic.com/api/oauth/usage 응답 (snake_case)</summary>
class ClaudeUsageApiResponse
{
    [JsonPropertyName("five_hour")]
    public ClaudeUsageWindow? FiveHour { get; set; }

    [JsonPropertyName("seven_day")]
    public ClaudeUsageWindow? SevenDay { get; set; }

    [JsonPropertyName("seven_day_opus")]
    public ClaudeUsageWindow? SevenDayOpus { get; set; }

    [JsonPropertyName("seven_day_sonnet")]
    public ClaudeUsageWindow? SevenDaySonnet { get; set; }

    [JsonPropertyName("extra_usage")]
    public ClaudeExtraUsage? ExtraUsage { get; set; }

    [JsonPropertyName("rate_limit_tier")]
    public string? RateLimitTier { get; set; }
}

class ClaudeUsageWindow
{
    /// <summary>0-100 사용률</summary>
    [JsonPropertyName("utilization")]
    public double Utilization { get; set; }

    /// <summary>ISO8601 리셋 시각</summary>
    [JsonPropertyName("resets_at")]
    public string? ResetsAt { get; set; }

    public DateTime? ResetsAtUtc => DateTime.TryParse(ResetsAt, out var dt)
        ? dt.ToUniversalTime()
        : null;
}

class ClaudeExtraUsage
{
    [JsonPropertyName("is_enabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("used_credits")]
    public double? UsedCredits { get; set; }

    [JsonPropertyName("monthly_limit")]
    public double? MonthlyLimit { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }
}
