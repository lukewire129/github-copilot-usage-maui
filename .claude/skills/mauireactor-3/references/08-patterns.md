# 08. 자주 쓰는 패턴 & 디버깅 팁

## 폼 처리

```csharp
class FormState
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public bool IsSubmitting { get; set; } = false;
}

class FormPage : Component<FormState>
{
    public override VisualNode Render()
        => VStack(
            Entry()
                .Placeholder("Name")
                .Text(State.Name)
                .OnTextChanged(v => SetState(s => s.Name = v)),

            Entry()
                .Placeholder("Email")
                .Text(State.Email)
                .OnTextChanged(v => SetState(s => s.Email = v)),

            Button("Submit")
                .IsEnabled(!State.IsSubmitting)
                .OnClicked(HandleSubmit)
        )
        .Padding(20);

    private async void HandleSubmit()
    {
        SetState(s => s.IsSubmitting = true);
        try
        {
            await Api.SubmitForm(State.Name, State.Email);
        }
        finally
        {
            SetState(s => s.IsSubmitting = false);
        }
    }
}
```

---

## 리스트 렌더링

```csharp
class ListState
{
    public List<Item> Items { get; set; } = new();
}

class ListPage : Component<ListState>
{
    public override VisualNode Render()
        => ContentPage(
            CollectionView()
                .Items(State.Items, item => new ItemRow(item))
        );
}

class ItemRow : Component
{
    private Item _item;

    public ItemRow(Item item) => _item = item;

    public override VisualNode Render()
        => Frame(
            Label(_item.Title)
        )
        .Margin(10);
}
```

---

## 로딩 상태 처리

```csharp
class DataPageState
{
    public bool IsLoading { get; set; } = true;
    public string? Data { get; set; }
    public string? Error { get; set; }
}

class DataPage : Component<DataPageState>
{
    public override void OnMounted() => LoadData();

    private async void LoadData()
    {
        try
        {
            var result = await Api.GetData();
            SetState(s =>
            {
                s.Data = result;
                s.IsLoading = false;
            });
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.Error = ex.Message;
                s.IsLoading = false;
            });
        }
    }

    public override VisualNode Render()
    {
        if (State.IsLoading)
            return ContentPage(ActivityIndicator().IsRunning(true).Center());

        if (!string.IsNullOrEmpty(State.Error))
            return ContentPage(Label($"Error: {State.Error}").Center());

        return ContentPage(Label(State.Data).Center());
    }
}
```

---

## 디버깅 팁

### 1. 애니메이션이 동작하지 않을 때

체크리스트:
- `WithAnimation()`이 **애니메이션 속성 뒤**에 있는가?
- `WithAnimation()`이 **애니메이션할 컴포넌트**에 직접 붙어 있는가?
- 부모 컨테이너(VStack, Frame 등)에 잘못 붙어 있지 않은가?

### 2. 리스트 변경 시 UI가 안 바뀔 때

```csharp
// ❌ WRONG — 직접 Add()는 re-render 보장 안 됨
SetState(s => s.Items.Add(newItem));

// ✅ CORRECT — 새 리스트 인스턴스 생성
SetState(s => s.Items = new List<Item>(s.Items) { newItem });
```

### 3. Hot-Reload가 동작하지 않을 때

- State 클래스에 public 값 타입 프로퍼티만 있는지 확인
- 복잡한 State 클래스는 별도 어셈블리로 분리
- 앱 캐시 삭제 후 재시작
- Android의 경우 `adb` 설치 확인

### 4. 성능 최적화

- `Render()` 안에서 큰 리스트 생성 금지 → `OnMounted()`로 이동
- 무거운 커스텀 렌더링은 `ComponentView` 사용
- 너무 큰 컴포넌트는 분할
- `OnWillMount()`에서 비용 많은 사전 작업 처리

### 5. [Scaffold] 관련 문제

- `Reactor.Maui.ScaffoldGenerator` NuGet 패키지 설치 확인
- `partial class` 키워드 누락 확인
- DataTemplate 기반 컨트롤은 수동 래퍼 작성 필요
- [mauireactor-integration](https://github.com/adospace/mauireactor-integration) 레포에서 기존 래퍼 확인
