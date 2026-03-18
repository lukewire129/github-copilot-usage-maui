# 06. 네비게이션

## NavigationPage 설정

앱 루트에서 NavigationPage로 감싸야 Push/Pop이 동작합니다.

```csharp
class App : Component
{
    public override VisualNode Render()
        => NavigationPage(
            new HomePage()
        )
        .Title("My App");
}
```

---

## 기본 네비게이션

```csharp
class HomePage : Component
{
    public override VisualNode Render()
        => ContentPage("Home",
            VStack(
                Button("상세로 이동")
                    .OnClicked(async () =>
                        await Navigation.PushAsync<DetailsPage>()
                    ),
                Button("모달로 이동")
                    .OnClicked(async () =>
                        await Navigation.PushModalAsync<ModalPage>()
                    )
            )
        );
}

class DetailsPage : Component
{
    public override VisualNode Render()
        => ContentPage("Details",
            Button("뒤로")
                .OnClicked(async () =>
                    await Navigation.PopAsync()
                )
        );
}
```

---

## 파라미터와 함께 네비게이션

```csharp
class ChildPageProps
{
    public int InitialValue { get; set; }
    public Action<int>? OnValueSet { get; set; }
}

class ChildPage : Component<ChildPageState, ChildPageProps>
{
    public override VisualNode Render()
        => ContentPage("Child Page",
            VStack(
                Label($"받은 값: {Props.InitialValue}"),
                Button("값 전달", () => Props.OnValueSet?.Invoke(99))
            ));
}

// 호출 측
await Navigation.PushAsync<ChildPage, ChildPageProps>(props =>
{
    props.InitialValue = State.CurrentValue;
    props.OnValueSet = (value) => SetState(s => s.CurrentValue = value);
});
```

---

## 네비게이션 메서드 목록

| 메서드 | 설명 |
|--------|------|
| `Navigation.PushAsync<T>()` | 스택에 페이지 추가 |
| `Navigation.PushAsync<T, TProps>(p => ...)` | Props와 함께 이동 |
| `Navigation.PopAsync()` | 스택에서 현재 페이지 제거 |
| `Navigation.PushModalAsync<T>()` | 모달로 표시 |
| `Navigation.PopModalAsync()` | 모달 닫기 |
| `Navigation.PopToRootAsync()` | 루트 페이지로 이동 |

---

## Shell 네비게이션 (탭/플라이아웃)

```csharp
class AppShell : Component
{
    public override VisualNode Render()
        => Shell(
            FlyoutItem("Home", "home.png",
                ShellContent()
                    .Route("home")
                    .RenderContent(() => new HomePage())
            ),
            TabBar(
                Tab("Tab1", "tab1.png",
                    ShellContent()
                        .Route("tab1")
                        .RenderContent(() => new Tab1Page())
                ),
                Tab("Tab2", "tab2.png",
                    ShellContent()
                        .Route("tab2")
                        .RenderContent(() => new Tab2Page())
                )
            )
        );
}
```

**Shell에서 라우팅:**
```csharp
// 등록
Routing.RegisterRoute("details", typeof(DetailsPage));

// 이동
await Shell.Current.GoToAsync("details");
await Shell.Current.GoToAsync($"details?id={itemId}");
```
