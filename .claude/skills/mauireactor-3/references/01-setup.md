# 01. 설정 & 프로젝트 구성

## 새 프로젝트 만들기

```bash
# 템플릿 설치
dotnet new install Reactor.Maui.TemplatePack

# 프로젝트 생성
dotnet new maui-reactor-startup -o my-app
cd my-app

# Android 빌드
dotnet build -f net9.0-android

# 실행
dotnet build -t:Run -f net9.0-android
```

---

## Hot-Reload 설정

```bash
# Hot-reload 도구 설치 (한 번만)
dotnet tool install -g Reactor.Maui.HotReloadConsole

# 터미널 1: hot-reload 시작
dotnet-maui-reactor -f net9.0-android

# 터미널 2: 앱 실행
dotnet build -t:Run -f net9.0-android
```

**Hot-Reload 트러블슈팅:**
- State 클래스는 public 값 타입 프로퍼티만 포함
- 복잡한 State는 별도 어셈블리에 분리
- 동작 안 하면 앱 캐시 삭제
- Android hot-reload는 `adb` 설치 필요

---

## Program.cs 템플릿

```csharp
using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using MauiControls = Microsoft.Maui.Controls;

namespace MyApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiReactorApp<HomePage>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-SemiBold.ttf", "OpenSansSemiBold");
            });

        return builder.Build();
    }
}
```

---

## 패키지 구조

```
MyApp/
├── Program.cs          # 앱 진입점
├── HomePage.cs         # 메인 페이지 컴포넌트
├── Services/           # 비즈니스 로직
├── Models/             # 데이터 클래스 (State 클래스 포함 권장)
└── Components/         # 재사용 컴포넌트
```

---

## 버전 호환성

| 항목 | 버전 |
|------|------|
| Skill 적용 범위 | MauiReactor **3.x** |
| Target Framework | **net9.0** |
| .NET MAUI | 9.0+ |
| 지원 플랫폼 | Android, iOS, macOS, Windows |

> MauiReactor 4.x는 별도 skill로 제공 예정
