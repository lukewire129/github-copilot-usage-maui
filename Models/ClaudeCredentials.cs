using System.Text.Json.Serialization;

namespace copilot_usage_maui.Models;

/// <summary>~/.claude/.credentials.json 루트 구조</summary>
class ClaudeCredentialsFile
{
    [JsonPropertyName("claudeAiOauth")]
    public ClaudeOAuthCredentials? ClaudeAiOauth { get; set; }
}

class ClaudeOAuthCredentials
{
    [JsonPropertyName("accessToken")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    /// <summary>Unix timestamp (밀리초)</summary>
    [JsonPropertyName("expiresAt")]
    public long ExpiresAt { get; set; }

    [JsonPropertyName("scopes")]
    public List<string> Scopes { get; set; } = [];

    [JsonPropertyName("subscriptionType")]
    public string? SubscriptionType { get; set; }

    [JsonPropertyName("rateLimitTier")]
    public string? RateLimitTier { get; set; }

    /// <summary>만료 여부 (5분 버퍼 포함)</summary>
    public bool IsExpired
        => ExpiresAt > 0 &&
           DateTimeOffset.FromUnixTimeMilliseconds(ExpiresAt).UtcDateTime < DateTime.UtcNow.AddMinutes(5);
}
