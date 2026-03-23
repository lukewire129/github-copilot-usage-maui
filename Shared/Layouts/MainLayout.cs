using copilot_usage_maui.Services;
using MauiReactor.Parameters;
using ReactorRouter.Components;
using ReactorRouter.Navigation;

namespace copilot_usage_maui.Shared.Layouts;

public class ProviderState
{
    public string Name { get; set; }
    public bool IsSelected { get; set; }
    public string Icon { get; set; }
    public string Url { get; set; }
}

public class MainLayoutState
{
    public ProviderState[] Providers { get; set; } = new[]
    {
        new ProviderState { Name = "Copilot", IsSelected = false, Icon = "providericon_copilot.svg", Url="/ai/githubcopilot" },
        new ProviderState { Name = "Claude", IsSelected = false, Icon = "providericon_claude.svg", Url="/ai/claude" }
    };
    public bool IsPinned { get; set; }
}

partial class MainLayout : Component
{
    [Param] IParameter<MainLayoutState> _providersParam;

#if WINDOWS
    [Inject] MainWindowService _mainWindowService;
#endif

    public override VisualNode Render()
    {
        var isPinned = _providersParam.Value.IsPinned;
        var providers = _providersParam.Value.Providers;

        return ContentView(
            Grid(
                // Header: 탭 버튼 + 핀 버튼
                Grid(
                    HStack(
                        providers.Select(p => TabButton(p))
                    )
                    .Spacing(5)
                    .Padding(12, 10),

                    HStack(
                        // Settings 기어 아이콘
                        Button("⚙")
                            .OnClicked(() => NavigationService.Instance.NavigateTo("/settings"))
                            .BackgroundColor(Colors.Transparent)
                            .TextColor(AppColors.PopupText3)
                            .FontSize(13)
                            .WidthRequest(28)
                            .HeightRequest(28),
                        // 핀 버튼
                        Button(isPinned ? "📌" : "\uE718")
                            .OnClicked(TogglePin)
                            .BackgroundColor(isPinned ? Color.FromArgb("#E6F1FB") : Colors.Transparent)
                            .TextColor(isPinned ? Color.FromArgb("#185FA5") : AppColors.PopupText3)
                            .FontSize(11)
                            .WidthRequest(28)
                            .HeightRequest(28)
                    )
                    .Spacing(4)
                    .GridColumn(1)
                    .VCenter()
                    .Margin(0, 0, 10, 0)
                )
                .Columns("*, Auto"),

                // 콘텐츠 영역
                new Outlet()
                    .GridRow(1)
            )
            .Rows("Auto, *")
        )
        .BackgroundColor(AppColors.PopupPage);
    }

    VisualNode TabButton(ProviderState provider)
    {
        Color bgColor;
        Color textColor;

        if (provider.IsSelected)
        {
            bgColor = provider.Name == "Copilot" ? AppColors.CopilotPrimary : AppColors.ClaudePrimary;
            textColor = Colors.White;
        }
        else
        {
            bgColor = Colors.Transparent;
            textColor = AppColors.PopupText3;
        }

        return Button(provider.Name)
            .OnClicked(() => NavigationService.Instance.NavigateTo(provider.Url))
            .BackgroundColor(bgColor)
            .TextColor(textColor)
            .FontSize(11)
            .FontAttributes(MauiControls.FontAttributes.Bold)
            .Padding(12, 6)
            .CornerRadius(8);
    }

    void TogglePin()
    {
#if WINDOWS
        _mainWindowService.TogglePin();
#endif
        _providersParam.Set(s => s.IsPinned = !s.IsPinned);
    }
}
