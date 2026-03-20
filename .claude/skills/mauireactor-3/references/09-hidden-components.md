# 09. 숨겨진/잘 안 알려진 컴포넌트

공식 문서에서 찾기 어렵거나, 실제 사용하다 발견한 컴포넌트들을 기록합니다.
새로운 컴포넌트를 발견하면 여기에 추가하세요.

---

## Timer

**언제 쓰나**: 일정 시간마다 반복 작업이 필요할 때 (자동 새로고침, 카운트다운 등)

**공식 문서**: https://adospace.gitbook.io/mauireactor/components/animation/timer

### API

```csharp
Timer()
    .IsEnabled(bool)   // 타이머 실행 여부
    .Interval(int)     // 간격 (밀리초)
    .OnTick(Action)    // 틱마다 실행할 콜백 (Action — async 아님!)
```

### 핵심 특성

- **VisualNode**로 Render 트리에 배치 → 페이지 unmount 시 **자동 정리** (별도 Dispose 불필요)
- `IsEnabled` false→true 전환 시 카운트다운이 **리셋**됨
- `OnTick`은 `Action`이므로 비동기 작업은 fire-and-forget 패턴 사용: `() => _ = SomeAsync()`

### 자동 새로고침 패턴 (타이머 리셋 포함)

`IsLoading` 상태와 연동하면 수동 새로고침 시에도 타이머가 자연스럽게 리셋됨:

```csharp
// State에 interval 값 보관
class MyPageState
{
    public bool IsLoading { get; set; }
    public int AutoRefreshIntervalMs { get; set; }  // 0이면 비활성
}

// Render 트리에 Timer 배치
Timer()
    .IsEnabled(!State.IsLoading && State.AutoRefreshIntervalMs > 0)
    .Interval(State.AutoRefreshIntervalMs > 0 ? State.AutoRefreshIntervalMs : 60_000)
    .OnTick(() => _ = LoadData())
```

**동작 원리**: `LoadData()` 시작 시 `IsLoading=true` → Timer 비활성화 → 완료 후 `IsLoading=false` → Timer 재시작(리셋).
수동 새로고침도 동일한 `LoadData()`를 호출하므로 타이머가 자동으로 리셋됨.

### 설정 변경 연동 패턴

설정에서 간격을 변경할 때 실시간 반영하려면 이벤트로 State 업데이트:

```csharp
protected override void OnMounted()
{
    base.OnMounted();
    SetState(s => s.AutoRefreshIntervalMs = SettingsService.GetAutoRefreshIntervalMs(...));
    SettingsService.AutoRefreshIntervalChanged += OnAutoRefreshIntervalChanged;
}

protected override void OnWillUnmount()
{
    SettingsService.AutoRefreshIntervalChanged -= OnAutoRefreshIntervalChanged;
    base.OnWillUnmount();
}

void OnAutoRefreshIntervalChanged(object? sender, EventArgs e)
    => SetState(s => s.AutoRefreshIntervalMs = SettingsService.GetAutoRefreshIntervalMs(...));
```

### 주의사항

- `Interval`에 `0`을 넣으면 안 됨 → `IsEnabled=false`일 때도 fallback 값 필요: `.Interval(ms > 0 ? ms : 60_000)`
- Grid 배치 시 `Timer`는 보이지 않는 노드이므로 다른 자식과 column/row 충돌해도 무방

---

<!-- 새 컴포넌트 발견 시 동일한 형식으로 추가 -->
