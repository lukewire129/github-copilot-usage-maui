# 03. 상태 관리 패턴

## 한눈에 비교

| 패턴 | 범위 | 방향 | 쓰는 경우 |
|------|------|------|----------|
| `Component<State>` | 단일 컴포넌트 | — | 로컬 UI 상태 |
| `[Prop]` | 부모 → 자식 | **단방향** (읽기 전용) | 부모가 값을 내려줄 때 |
| `[Param] IParameter<T>` | 트리 전체 | **양방향** (누구나 변경 가능) | 여러 컴포넌트가 공유할 때 |
| `[Inject]` | 앱 전체 | — | 서비스/인프라 (API, DB) |

> **헷갈릴 때:** 자식이 값을 **쓸(변경할) 필요**가 있으면 `[Param]`, 읽기만 하면 `[Prop]`

---

## Pattern 1: 로컬 상태 (Component\<State\>)

단일 컴포넌트 내부에서만 사용하는 상태.

```csharp
class TodoState
{
    public string Input { get; set; } = "";
    public List<string> Items { get; set; } = new();
}

class TodoApp : Component<TodoState>
{
    public override VisualNode Render()
        => VStack(
            Entry()
                .Text(State.Input)
                .OnTextChanged(v => SetState(s => s.Input = v)),
            Button("Add")
                .OnClicked(() => SetState(s =>
                {
                    // 리스트 변경 시 새 인스턴스 생성 필수!
                    s.Items = new List<string>(s.Items) { s.Input };
                    s.Input = "";
                }))
        );
}
```

---

## Pattern 2: 부모→자식 Props ([Prop])

부모가 값을 자식에게 **일방향**으로 전달. 자식은 읽기만 가능.

```csharp
partial class ParentPage : Component<ParentPageState>
{
    public override VisualNode Render()
        => VStack(spacing: 10,
            new GreetingCard(userName: State.UserName, age: State.Age),
            new ScoreBadge(score: State.Score)
        );
}

// 자식: [Prop]으로 받기
partial class GreetingCard : Component
{
    [Prop] string _userName;
    [Prop] int _age;

    public override VisualNode Render()
        => Frame(Label($"{_userName}, {_age} years old"));
}
```

**[Prop] 규칙:**
- `partial class` 필수
- 필드명은 소문자 + 언더스코어(`_camelCase`)
- 자식에서 값 변경 불가 — 변경이 필요하면 `Action<T>` Prop을 추가

---

## Pattern 3: 트리 공유 상태 ([Param])

컴포넌트 트리 어디서든 **읽기/쓰기** 가능한 공유 상태.  
한 컴포넌트가 `Set()` 호출 → **해당 파라미터를 참조하는 모든 컴포넌트 재렌더링**.

```csharp
public class AppState
{
    public string UserName { get; set; } = "Guest";
    public int Theme { get; set; }
}

// 조상 컴포넌트: 파라미터 선언
partial class MainPage : Component
{
    [Param]
    IParameter<AppState> _appState;

    public override VisualNode Render()
        => VStack(spacing: 10,
            Label($"Current User: {_appState.Value.UserName}"),
            new ProfileEditor(),   // 자손도 동일 파라미터 접근 가능
            new ThemeSwitcher()
        ).Center();
}

// 자손: 동일한 선언으로 읽기 + 쓰기
partial class ProfileEditor : Component
{
    [Param]
    IParameter<AppState> _appState;

    public override VisualNode Render()
        => VStack(spacing: 5,
            Label($"Hello, {_appState.Value.UserName}"),
            Button("Change to Admin", () =>
                _appState.Set(s => s.UserName = "Admin"))
        );
}
```

**[Param] 규칙:**
- 조상과 자손 모두 **동일한 타입**으로 선언
- `_appState.Value` → 현재 값 읽기
- `_appState.Set(s => ...)` → 값 변경 (모든 참조자 재렌더링)
- `partial class` 필수

---

## Pattern 4: 의존성 주입 ([Inject])

UI 상태가 아닌 **앱 서비스** 연결용. Program.cs에 등록 후 사용.

```csharp
// Program.cs
builder.Services.AddSingleton<TodoService>();
builder.Services.AddScoped<AuthService>();

// 컴포넌트
class TodoPage : Component<TodoPageState>
{
    [Inject] TodoService _todoService;
    [Inject] AuthService _authService;

    public override void OnMounted()
    {
        var todos = _todoService.GetTodos();
        SetState(s => s.Items = todos);
    }
}
```

**[Inject] 사용 시기:**
- API 클라이언트, 데이터베이스, 비즈니스 로직
- 앱 전체에서 공유되는 인프라
- UI 상태(표시 여부, 입력값 등)에는 사용 금지
