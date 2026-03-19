# Claude Usage API Integration

## Overview

Claude 사용량(rate limit) 데이터를 가져와 대시보드에 표시하는 기능.
세션(5시간), 주간(7일), 모델별 윈도우, 컨디션 관리, Windows 토스트 알림을 지원한다.

## Authentication

### OAuth Token (Primary)

`~/.claude/.credentials.json` 파일에서 OAuth 토큰을 읽는다.

```json
{
  "claudeAiOauth": {
    "accessToken": "sk-ant-oat01-...",
    "refreshToken": "...",
    "expiresAt": 1742000000000,
    "scopes": ["user:inference", "user:profile"],
    "subscriptionType": "pro",
    "rateLimitTier": "pro"
  }
}
```

**중요:** `user:profile` 스코프가 있어야 usage API에 접근 가능. `user:inference`만 있는 토큰은 차단됨.

### Claude CLI (Fallback)

`claude usage --format json` 명령어의 출력을 파싱한다.
JSON 출력이 없으면 텍스트 출력에서 퍼센트/시간 값을 추출한다.

## API Endpoint

### `GET https://api.anthropic.com/api/oauth/usage`

**Headers:**
```
Authorization: Bearer <accessToken>
anthropic-beta: oauth-2025-04-20
```

**Response (snake_case):**
```json
{
  "five_hour": {
    "utilization": 82.0,
    "resets_at": "2026-03-19T11:00:00.291607+00:00"
  },
  "seven_day": {
    "utilization": 66.0,
    "resets_at": "2026-03-22T12:00:01.291635+00:00"
  },
  "seven_day_opus": null,
  "seven_day_sonnet": null,
  "seven_day_cowork": null,
  "seven_day_oauth_apps": null,
  "iguana_necktie": null,
  "extra_usage": {
    "is_enabled": false,
    "monthly_limit": null,
    "used_credits": null,
    "utilization": null
  }
}
```

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `five_hour` | `ClaudeUsageWindow?` | 5시간 세션 윈도우 |
| `seven_day` | `ClaudeUsageWindow?` | 7일 롤링 윈도우 |
| `seven_day_opus` | `ClaudeUsageWindow?` | Opus 모델 7일 윈도우 |
| `seven_day_sonnet` | `ClaudeUsageWindow?` | Sonnet 모델 7일 윈도우 |
| `seven_day_cowork` | `ClaudeUsageWindow?` | Cowork 7일 윈도우 |
| `seven_day_oauth_apps` | `ClaudeUsageWindow?` | OAuth 앱 7일 윈도우 |
| `extra_usage` | `ClaudeExtraUsage?` | 추가 사용량/크레딧 정보 |
| `rate_limit_tier` | `string?` | 요금제 (max/pro/team/free) |

### ClaudeUsageWindow

| Field | Type | Description |
|-------|------|-------------|
| `utilization` | `double` | 사용률 (0-100) |
| `resets_at` | `string?` | ISO 8601 리셋 시각 (타임존 포함) |

### ClaudeExtraUsage

| Field | Type | Description |
|-------|------|-------------|
| `is_enabled` | `bool` | 추가 사용량 활성화 여부 |
| `used_credits` | `double?` | 사용한 크레딧 (null 가능) |
| `monthly_limit` | `double?` | 월간 한도 (null 가능) |
| `currency` | `string?` | 통화 |

## Why Not `claude.ai/api/usage`?

초기에 `https://claude.ai/api/usage`를 사용하려 시도했으나 실패한 이유:

1. **Cloudflare TLS 핑거프린트 차단**: .NET `HttpClient`는 WinHTTP 기반 TLS를 사용하여
   Cloudflare가 봇으로 판단 → HTTP 403 반환
2. **WebView2 우회 시도**: Chromium TLS를 이용해 Cloudflare를 우회하려 했으나,
   WebView2의 `Navigating` 이벤트가 커스텀 URL 스킴(`clauderesult://`)에 대해 발생하지 않아 실패
3. **PostMessage 시도**: `window.chrome.webview.postMessage()`로 전환했으나 복잡성 증가

**해결**: CodexBar 프로젝트 참조로 `api.anthropic.com/api/oauth/usage` 엔드포인트 발견.
이 엔드포인트는 표준 API 서버로 Cloudflare 차단이 없어 일반 `HttpClient`로 직접 호출 가능.

## Condition Management Logic

단순 퍼센트가 아닌, 남은 시간과 사용 속도를 종합하여 상태를 판정한다.

### 산정 요소

```
경과비율 = (WindowMinutes - 남은분) / WindowMinutes
예상최종사용률 = UsedPercent / 경과비율
```

### 판정 기준

| 상태 | 조건 | 색상 |
|------|------|------|
| 여유 | 예상 < 80% | 초록 (StatusSuccess) |
| 주의 | 예상 80-100% | 주황 (StatusWarning) |
| 위험 | 예상 >= 100% 또는 현재 >= 90% | 빨강 (StatusError) |

### 가장 제한적인 윈도우

`ClaudeUsageSnapshot.MostRestrictive` — 세션/주간 윈도우 중 `UsedPercent`가 가장 높은 것을
기준으로 전체 상태를 결정한다.

## Windows Toast Notifications

`Microsoft.Toolkit.Uwp.Notifications` (7.1.3) 사용.

| 레벨 | 조건 | 메시지 |
|------|------|--------|
| Warning | 사용률 >= 80% | "Claude 사용량 주의" |
| Danger | 사용률 >= 90% | "Claude 한도 임박" |

- 세션(5h)과 주간(7d) 윈도우 각각 체크
- 같은 레벨 중복 발송 방지 (리셋 후 초기화)
- 앱 종료 시 `ToastNotificationManagerCompat.Uninstall()` 호출 필수

## File Structure

```
Models/
  ClaudeCredentials.cs      # ~/.claude/.credentials.json 매핑
  ClaudeRateWindow.cs       # Rate window record + 컨디션 계산
  ClaudeUsageSnapshot.cs    # 스냅샷 record + MostRestrictive
  ClaudeUsageResponse.cs    # API 응답 모델 (snake_case)

Services/
  ClaudeUsageService.cs     # OAuth API 호출 + CLI fallback + 파싱
  NotificationService.cs    # Windows 토스트 알림

Features/Claude/Pages/
  ClaudeDashBoardPage.cs    # 대시보드 UI (MauiReactor)
```

## References

- [CodexBar Claude Docs](https://github.com/steipete/CodexBar/blob/main/docs/claude.md)
- [Anthropic OAuth Beta](https://docs.anthropic.com/) — `anthropic-beta: oauth-2025-04-20` 헤더 필수
