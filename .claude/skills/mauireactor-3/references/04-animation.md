# 04. 애니메이션 시스템

## ⚠️ WithAnimation() 위치 규칙 (가장 중요!)

`WithAnimation()`은 **애니메이션할 속성 뒤, 같은 컴포넌트**에 위치해야 합니다.

```csharp
// ✅ CORRECT — 속성 뒤에 WithAnimation()
Image()
    .Source("city.jpg")
    .Scale(State.IsExpanded ? 1.0 : 0.5)    // 애니메이션 속성
    .Opacity(State.IsExpanded ? 1.0 : 0.0)  // 애니메이션 속성
    .WithAnimation()                          // ← 여기!
    .Margin(10)                              // 비애니메이션 속성은 뒤에 가도 OK

// ❌ WRONG — 속성보다 앞에 위치
Image()
    .Source("city.jpg")
    .WithAnimation()
    .Scale(State.IsExpanded ? 1.0 : 0.5)    // 애니메이션 안 됨!

// ❌ WRONG — 부모 컨테이너에 적용
VStack(
    Image()
        .Scale(State.IsExpanded ? 1.0 : 0.5)
)
.WithAnimation()  // 잘못된 위치! Image에 적용 안 됨
```

---

## Property-Based Animation (WithAnimation)

상태 변경 시 속성값을 자동으로 보간(interpolate)하는 가장 간단한 애니메이션.

```csharp
class ExpandableImageState
{
    public bool IsExpanded { get; set; } = false;
}

class ExpandableImage : Component<ExpandableImageState>
{
    public override VisualNode Render()
        => ContentPage(
            Frame(
                Image()
                    .Source("city.jpg")
                    .OnTap(() => SetState(s => s.IsExpanded = !s.IsExpanded))
                    .Scale(State.IsExpanded ? 1.0 : 0.5)
                    .Opacity(State.IsExpanded ? 1.0 : 0.0)
                    .WithAnimation()   // Scale과 Opacity가 함께 보간됨
                    .Margin(10)
            )
            .HasShadow(true)
        );
}
```

### WithAnimation() 커스터마이징

```csharp
.WithAnimation()               // 기본: 600ms, Linear
.WithAnimation(1000)           // 1000ms
.WithAnimation(800, Easing.CubicOut)  // 800ms + easing
```

**Easing 함수 목록:**
- `Easing.Linear` — 일정 속도
- `Easing.SinIn`, `SinOut`, `SinInOut` — 사인 곡선
- `Easing.CubicIn`, `CubicOut`, `CubicInOut` — 3차 곡선
- `Easing.BounceOut` — 튀기는 효과
- `Easing.SpringOut` — 스프링 효과

**애니메이션 가능한 속성들:**
- `Opacity()`, `Scale()`, `Rotation()`
- `TranslationX()`, `TranslationY()`
- `CornerRadius()`
- 대부분의 레이아웃 속성 (Width, Height, Margin, Padding)

---

## AnimationController (복잡한 순차 애니메이션)

여러 단계를 순서대로 실행하는 고급 애니메이션.

```csharp
class AnimatedPageState
{
    public double X { get; set; } = 0;
    public double Y { get; set; } = 0;
    public double Rotation { get; set; } = 0;
}

class AnimatedPage : Component<AnimatedPageState>
{
    private AnimationController _animController = new();

    public override VisualNode Render()
        => ContentPage(
            VStack(
                Frame(Label("Animated Box"))
                    .TranslationX(State.X)
                    .TranslationY(State.Y)
                    .Rotation(State.Rotation)
                    .Margin(20),

                _animController
                    .Add(
                        new SequenceAnimation
                        {
                            // 단계 1: X축으로 이동
                            new DoubleAnimation()
                                .StartValue(0)
                                .TargetValue(200)
                                .Duration(TimeSpan.FromSeconds(2))
                                .Easing(Easing.CubicOut)
                                .OnTick(v => SetState(s => s.X = v)),

                            // 단계 2: Y축으로 이동
                            new DoubleAnimation()
                                .StartValue(0)
                                .TargetValue(300)
                                .Duration(TimeSpan.FromSeconds(1.5))
                                .OnTick(v => SetState(s => s.Y = v))
                        }
                    ),

                Button("Start")  .OnClicked(() => _animController.PlayAsync()),
                Button("Pause")  .OnClicked(() => _animController.IsPaused = !_animController.IsPaused),
                Button("Stop")   .OnClicked(() => _animController.IsEnabled = false)
            )
            .Padding(20)
        );
}
```

### AnimationController 컨트롤

```csharp
_animController.PlayAsync()          // 시작
_animController.IsPaused = true      // 일시정지 (현재 위치에서)
_animController.IsPaused = false     // 재개
_animController.IsEnabled = false    // 정지 (초기 상태로 리셋)
```

### Animation 타입

| 타입 | 용도 |
|------|------|
| `DoubleAnimation` | 숫자값 보간 |
| `SequenceAnimation` | 순서대로 실행 |
| `ParallelAnimation` | 동시에 실행 |
| `CubicBezierPathAnimation` | 베지어 곡선 경로 이동 |
| `QuadraticBezierPathAnimation` | 2차 베지어 경로 이동 |
