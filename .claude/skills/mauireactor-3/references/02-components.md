# 02. 컴포넌트 & 생명주기

## Stateless 컴포넌트

내부 상태 없이 UI만 렌더링하는 순수 함수형 컴포넌트.

```csharp
// partial 키워드 필수 (소스 제너레이터용)
partial class GreetingCard : Component
{
    [Prop] string _name;
    [Prop] Action<string> _onNameChanged;

    public override VisualNode Render()
        => VStack(
            Label($"Hello {_name}!"),
            Button("Clear")
                .OnClicked(() => _onNameChanged?.Invoke(_name))
        )
        .Padding(20);
}
```

**사용 시기:**
- 표시 전용 컴포넌트 (카드, 헤더, 배지)
- 모든 데이터를 `[Prop]`으로 받는 경우
- 사이드 이펙트 없는 순수 UI 조각

---

## Stateful 컴포넌트

내부 상태를 가지고 사용자 인터랙션을 처리하는 컴포넌트.

```csharp
// State 클래스: public 프로퍼티만 사용
class CounterPageState
{
    public int Count { get; set; } = 0;
}

class CounterPage : Component<CounterPageState>
{
    public override VisualNode Render()
        => ContentPage("Counter",
            VStack(
                Label($"Count: {State.Count}")
                    .FontSize(32)
                    .Bold(),
                Button("+")
                    .OnClicked(() => SetState(s => s.Count++)),
                Button("-")
                    .OnClicked(() => SetState(s => s.Count--))
            )
            .Center()
        );
}
```

**State 작성 규칙:**
- 값 타입(int, double, string, bool) 또는 string만 사용
- `SetState()` 로만 변경 → 자동 리렌더링
- 여러 SetState는 **배치 처리** (한 번만 렌더링)
- 복잡한 객체는 **별도 어셈블리**에 분리 (hot-reload 안정성)

---

## Props Class (Navigation용 컴포넌트)

페이지 이동 시 초기 데이터를 전달할 때 사용.

```csharp
class ChildPageProps
{
    public int InitialValue { get; set; }
    public Action<int>? OnValueSet { get; set; }
}

// State + Props 모두 사용
class ChildPage : Component<ChildPageState, ChildPageProps>
{
    public override VisualNode Render()
        => ContentPage("Child Page",
            VStack(
                Label($"Initial Value: {Props.InitialValue}"),
                Button("Set Value", () => Props.OnValueSet?.Invoke(42))
            ));
}

// 호출 측
await Navigation.PushAsync<ChildPage, ChildPageProps>(_ =>
{
    _.InitialValue = State.Value;
    _.OnValueSet = this.OnValueSetFromChildPage;
});
```

---

## 컴포넌트 생명주기

```csharp
class MyComponent : Component<MyState>
{
    public override VisualNode Render() { ... }

    // 컴포넌트가 처음 마운트될 때 (데이터 로드, 초기화)
    public override void OnMounted()
    {
        LoadData();
    }

    // 상태 변경 직전 (렌더링 준비)
    public override void OnWillMount()
    {
        // 렌더링 전 사전 작업
    }

    // 컴포넌트가 언마운트된 후 (리소스 정리)
    public override void OnUnmounted()
    {
        // 구독 취소, 타이머 중지 등
    }
}
```

**생명주기 순서:**
```
OnWillMount → Render → OnMounted
상태 변경 시 → OnWillMount → Render
제거 시 → OnUnmounted
```

**주의:** 비용이 많이 드는 작업은 `Render()` 안에 넣지 말고 `OnMounted()` 또는 `OnWillMount()`에서 처리.
