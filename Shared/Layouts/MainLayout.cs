using copilot_usage_maui.Features.GithubCopilot.Components;
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
}

partial class MainLayout : Component
{
    [Param] IParameter<MainLayoutState> _providersParam;
    public override VisualNode Render()
    {
        return ContentView(
                Grid(
                    HStack(_providersParam.Value.Providers.Select(provider => ProviderItem(provider)))
                        .Padding(10)
                        .Height(100)
                        .Spacing(10),

                    Divider()
                        .Margin(10, 10, 10, 20)
                        .GridRow(1),

                    new Outlet()
                        .GridRow(2),

                    Divider()
                        .Margin(10, 10, 10, 20)
                        .GridRow(3),

                    Label("Settings")
                        .OnTapped(() => NavigationService.Instance.NavigateTo("/settings"))
                        .BackgroundColor(Colors.Transparent)
                        .TextColor(AppColors.TextSecondary)
                        .HeightRequest(44)
                        .GridRow(4)
                        .HCenter()
                )
            .Rows("auto, auto, *, auto, auto")
        );

    }

    public bool IsLightTheme => MauiControls.Application.Current?.RequestedTheme == AppTheme.Light;

    private Border Divider()
        => Border()
            .Stroke(IsLightTheme ? AppColors.CopyButtonBg : Colors.White)
            .HFill()
            .Height(1);

    private VisualNode ProviderItem(ProviderState provider)
        => Border(
                VStack(
                    new SvgIcon()
                        .FileName($"{provider.Icon}")
                        .Size(30f)
                        .TintColor(Color.FromArgb("#676780")),
                    Label(provider.Name)
                        .TextColor(provider.IsSelected ? Colors.White : AppColors.TextSecondary)
                )
                .InputTransparent(true)
           )
           .OnTapped(() =>
           {
               NavigationService.Instance.NavigateTo(provider.Url);
           })
           .Padding(10)
           .StrokeCornerRadius(5)
           .Stroke(Colors.Transparent)
           .BackgroundColor(provider.IsSelected ? IsLightTheme ? Color.FromArgb("#66BB6A") : AppColors.StatusSuccess : Colors.Transparent);
}

