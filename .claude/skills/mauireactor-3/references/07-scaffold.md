# 07. [Scaffold] — 서드파티 컨트롤 래핑

## 개념

MauiReactor는 기본 MAUI 컨트롤만 기본 제공합니다.  
Syncfusion, Telerik, DevExpress 같은 서드파티 컨트롤을 사용하려면 **MauiReactor VisualNode 래퍼**를 만들어야 합니다.

`[Scaffold]` 어트리뷰트를 사용하면 **소스 제너레이터**가 래퍼 코드를 자동 생성합니다.

```
네이티브 컨트롤 (예: SfTextInputLayout)
    ↓ [Scaffold] 어트리뷰트 적용
소스 제너레이터 자동 실행
    ↓
MauiReactor VisualNode 생성 (fluent API 포함)
    ↓
컴포넌트에서 일반 컨트롤처럼 사용
```

---

## 기본 사용법

```csharp
// 1. partial class에 [Scaffold] 어트리뷰트 적용
[Scaffold(typeof(Syncfusion.Maui.Core.SfTextInputLayout))]
public partial class SfTextInputLayout { }

// 2. 컴포넌트에서 일반 컨트롤처럼 사용
class LoginPage : Component<LoginPageState>
{
    public override VisualNode Render()
        => ContentPage("Login",
            VStack(
                new SfTextInputLayout()
                    .Hint("Username"),
                new SfTextInputLayout()
                    .Hint("Password")
            )
        );
}
```

---

## 다양한 서드파티 예시

### Syncfusion

```csharp
[Scaffold(typeof(Syncfusion.Maui.Core.SfTextInputLayout))]
public partial class SfTextInputLayout { }

[Scaffold(typeof(Syncfusion.Maui.Buttons.SfSwitch))]
public partial class SfSwitch { }

[Scaffold(typeof(Syncfusion.Maui.Charts.SfCartesianChart))]
public partial class SfCartesianChart { }
```

### LiveCharts (SkiaSharp)

```csharp
[Scaffold(typeof(LiveChartsCore.SkiaSharpView.Maui.CartesianChart))]
partial class CartesianChart { }

[Scaffold(typeof(LiveChartsCore.SkiaSharpView.Maui.PieChart))]
partial class PieChart { }
```

### 사용 예시 (LiveCharts)

```csharp
[Scaffold(typeof(LiveChartsCore.SkiaSharpView.Maui.CartesianChart))]
partial class CartesianChart { }

class ChartPage : Component<ChartPageState>
{
    public override VisualNode Render()
        => ContentPage("Chart",
            new CartesianChart()
                .Series(() => new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Values = State.Values,
                        Fill = null,
                    }
                })
        );
}
```

---

## nuget 패키지 설치

`[Scaffold]` 어트리뷰트 및 소스 제너레이터를 사용하려면:

```xml
<!-- .csproj에 추가 -->
<PackageReference Include="Reactor.Maui.ScaffoldGenerator" Version="3.*" />
```

---

## 제한사항

`ScaffoldGenerator`가 **자동 처리하지 못하는 경우:**

- `DataTemplate`을 사용하여 자식 항목을 렌더링하는 컨트롤
  - 예: CollectionView 스타일 커스터마이징, 복잡한 ItemTemplate
- 네이티브 렌더러 수준의 커스터마이징이 필요한 경우

이런 경우에는:
1. MauiReactor 소스의 `ItemsView`, `Shell` 래퍼 구현 참고
2. [mauireactor-integration 레포](https://github.com/adospace/mauireactor-integration)에서 기존 래퍼 확인
3. GitHub Issue 등록

---

## 통합 레포 (커뮤니티 래퍼 모음)

주요 서드파티 래퍼가 정리된 레포:  
👉 https://github.com/adospace/mauireactor-integration

포함된 벤더:
- Syncfusion
- DevExpress  
- UraniumUI
- CommunityToolkit
- SkiaSharp
- Mapsui (지도)
- The49 (Bottom Sheet)
- HorusStudio

---

## 동작 원리 요약

| 단계 | 설명 |
|------|------|
| `[Scaffold(typeof(NativeControl))]` | 소스 제너레이터 트리거 |
| 소스 제너레이터 실행 | 네이티브 컨트롤의 프로퍼티/이벤트 분석 |
| VisualNode 클래스 자동 생성 | Mount/Render/Unmount 생명주기 + fluent 메서드 |
| 컴포넌트에서 사용 | `new SfTextInputLayout().Hint("...")` 형태로 사용 |

> **VisualNode**란: MauiReactor가 각 페이지에 생성하는 Visual Tree의 노드.  
> 네이티브 컨트롤의 초기화, 업데이트, 해제를 MVU 프레임워크 안에서 처리합니다.
