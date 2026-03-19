using copilot_usage_maui.Shared.Layouts;
using MauiReactor.Parameters;

namespace copilot_usage_maui.Features.Claude.Pages;

class ClaudeDashBoardPageState
{
    public bool IsLoading { get; set; } = true;
    public string? Error { get; set; }
}
partial class ClaudeDashBoardPage : Component<ClaudeDashBoardPageState>
{

    [Param] IParameter<MainLayoutState> _providerStateParam;

    protected override async void OnMounted()
    {
        _providerStateParam.Set(p =>
        {
            p.Providers = p.Providers
            .Select(x => new ProviderState
            {
                Name = x.Name,
                Icon = x.Icon,
                Url = x.Url,
                IsSelected = x.Url == "/ai/claude"
            })
            .ToArray();
        });
    }
    public override VisualNode Render()
        => ScrollView(
            VStack(
                // Header
                Grid("Auto", "*, Auto",
                    VStack(
                        Label("Claude")
                            .FontSize(20)
                            .FontAttributes(MauiControls.FontAttributes.Bold),
                        Label(DateTime.Today.ToString(AppStrings.DateFormat))
                            .FontSize(12)
                            .TextColor(AppColors.TextSecondary)
                    ).Spacing(2)
                )
            )
            .Spacing(20)
            .Padding(24, 20)
        );
}
