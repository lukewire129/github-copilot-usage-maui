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

## Stateful/Stateless 설계 가이드 (내부 Diffing 메커니즘)

### 왜 이 선택이 중요한가

MauiReactor는 React와 유사한 **트리 비교(Diffing)** 를 수행한다. `SetState()` 호출 시 해당 컴포넌트 아래 전체 트리를 다시 `Render()` → old/new 비교 → 변경분만 네이티브에 적용한다.

**핵심 원칙: `SetState()`를 호출한 컴포넌트가 invalidation의 시작점이며, 그 아래 모든 Component의 `Render()`가 재호출된다.**

```
SetState() → _invalidated=true → RequireLayoutCycle (부모 체인 전파)
→ PageHost.OnLayout() → Layout() 순회 시작
→ invalidated 노드: _children=null → Render() 재호출 → MergeChildrenFrom(old vs new)
→ 아래 모든 자식 Component: MergeWith 과정에서 newNode.Children 접근 → Render() 호출됨
```

### Diffing 방식: 인덱스 기반 Positional Matching

old/new 트리를 **인덱스(위치) 기반**으로 1:1 비교한다. React의 key 기반 재배치와 다름.

```
old[0] ↔ new[0] → 타입 같으면 네이티브 컨트롤 재사용 (MergeWith)
old[1] ↔ new[1] → 타입 다르면 old Unmount + new 새로 Mount
old[2]           → new에 없으면 Unmount
         new[2]  → old에 없으면 새로 Mount
```

**따라서 조건부 렌더링 시 순서를 바꾸면 안 된다:**

```csharp
// ❌ 나쁜 예: 조건에 따라 자식 순서가 바뀜 → 매번 Unmount/Mount
public override VisualNode Render()
    => VStack(
        State.ShowHeader ? Header() : null,  // index 0이 Header 또는 Body
        Body()
    );

// ✅ 좋은 예: 순서 유지 + 표시/숨김으로 처리
public override VisualNode Render()
    => VStack(
        Header().IsVisible(State.ShowHeader),  // index 0은 항상 Header
        Body()                                  // index 1은 항상 Body
    );
```

### Stateful 배치가 성능을 결정한다

Stateful/Stateless 여부와 관계없이, **부모가 invalidate되면 아래 모든 Component의 `Render()`가 호출된다.** 차이점은 "누가 invalidation을 트리거할 수 있느냐"이다.

```
Stateless: SetState가 없음 → 독립적 invalidation 불가 → 부모가 invalidate될 때만 영향
Stateful:  SetState 가능 → 독립적으로 Invalidate → 해당 컴포넌트 아래만 re-render
```

**설계 원칙:**

```csharp
// ❌ 나쁜 예: 최상위 하나의 Stateful에 모든 상태 집중
// → 작은 상태 변경에도 전체 트리 순회
class MainPage : Component<AppState>
{
    public override VisualNode Render()
        => ContentPage(
            Header(),           // 불필요하게 Render() 재호출됨
            Body(),             // 불필요하게 Render() 재호출됨
            Footer()            // 불필요하게 Render() 재호출됨
            // Header, Body, Footer 모두 매번 Render() 호출
        );
}

// ✅ 좋은 예: 상태가 변하는 영역만 Stateful로 격리
// → Body만 invalidate, Header/Footer는 영향 없음
class MainPage : Component
{
    public override VisualNode Render()
        => ContentPage(
            Header(),           // Stateless: MainPage가 invalidate될 때만
            new BodySection(),  // Stateful: 독립적으로 invalidate
            Footer()            // Stateless: MainPage가 invalidate될 때만
        );
}

class BodySection : Component<BodyState>
{
    public override VisualNode Render()
        => VStack(
            // 여기서 SetState → BodySection 아래만 re-render
            // Header, Footer는 전혀 영향 없음
        );
}
```

**정리:**

| 상황 | Stateful 위치 | 영향 범위 |
|------|-------------|----------|
| 최상위 하나만 Stateful | 루트 | SetState 시 전체 트리 순회 |
| 영역별로 Stateful 분리 | 각 섹션 | 해당 섹션 아래만 순회 |
| 모든 컴포넌트 Stateful | 전체 | 각각 독립 invalidation 가능하지만 불필요한 State 생성/이전 비용 |

**권장: 상태가 실제로 변하는 최소 범위를 Stateful로 감싸고, 나머지는 Stateless로 둔다.**

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