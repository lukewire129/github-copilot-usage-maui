# 05. 레이아웃 & Fluent API

## 컨테이너 컴포넌트

### VStack — 수직 스택

```csharp
VStack(
    Label("Item 1"),
    Label("Item 2"),
    Label("Item 3")
)
.Spacing(10)
.Padding(20)
```

### HStack — 수평 스택

```csharp
HStack(
    Button("OK"),
    Button("Cancel")
)
.Spacing(10)
```

### Grid — 2D 레이아웃

```csharp
// 행/열 정의 문자열: "*" = 비율, "100" = 고정, "Auto" = 자동
Grid("*, Auto", "*, 200",
    Label("상단 왼쪽"),
    Button("우측 고정")
        .GridColumn(1),
    Entry()
        .GridRow(1)
        .GridColumnSpan(2)
)
```

### ScrollView

```csharp
ScrollView(
    VStack(
        // 스크롤할 내용
    )
)
.VerticalScroll()
```

### Frame — 테두리가 있는 컨테이너

```csharp
Frame(
    Label("Content")
)
.BorderColor(Colors.Gray)
.CornerRadius(10)
.HasShadow(true)
.Padding(15)
```

---

## Fluent API 공통 속성

모든 MauiReactor 컴포넌트에서 사용 가능한 메서드 체이닝.

### 텍스트

```csharp
.Text("내용")
.FontSize(16)
.Bold()
.Italic()
.TextColor(Colors.Black)
```

### 레이아웃

```csharp
.Padding(20)          // 전체
.Padding(16, 8)       // 좌우, 상하
.Margin(10)
.Margin(16, 8)
.Width(200)
.Height(50)
.MinWidth(100)
.MaxHeight(300)
```

### 시각

```csharp
.BackgroundColor(Colors.White)
.Opacity(0.8)         // 0.0 ~ 1.0
.Scale(1.5)
.Rotation(45)
.CornerRadius(8)
.IsVisible(true)
.IsEnabled(false)
```

### 위치/정렬

```csharp
.Center()             // 가로세로 모두 가운데
.HCenter()            // 가로 가운데
.VCenter()            // 세로 가운데
.HStart()             // 가로 왼쪽
.HEnd()               // 가로 오른쪽
.VStart()             // 세로 위
.VEnd()               // 세로 아래
.FillExpand()         // 남은 공간 모두 채움
.StartExpand()        // 시작 + 확장
.EndExpand()          // 끝 + 확장
```

### Grid 셀 지정

```csharp
.GridRow(0)
.GridColumn(1)
.GridRowSpan(2)
.GridColumnSpan(3)
```

### 이벤트 핸들러

```csharp
.OnClicked(() => { })                          // 버튼 클릭
.OnTap(() => { })                              // 탭
.OnTextChanged(v => SetState(s => s.Text = v)) // 텍스트 변경
.OnValueChanged((s, args) => { })              // 값 변경 (Slider 등)
.OnPropertyChanged("PropertyName", () => { }) // 특정 속성 변경
```

---

## 자주 쓰는 컴포넌트

```csharp
Label("텍스트")
Entry().Placeholder("입력하세요").Text(State.Input)
Button("클릭").OnClicked(() => { })
Image().Source("image.png").Aspect(Aspect.AspectFit)
ActivityIndicator().IsRunning(State.IsLoading)
Switch().IsToggled(State.IsOn).OnToggled((s, e) => SetState(s => s.IsOn = e.Value))
Slider().Minimum(0).Maximum(100).Value(State.Progress)
    .OnValueChanged((s, e) => SetState(s => s.Progress = e.NewValue))
Picker().Items(new[]{"A","B","C"}).SelectedIndex(State.Index)
DatePicker().Date(State.SelectedDate)
CheckBox().IsChecked(State.IsChecked)
```
