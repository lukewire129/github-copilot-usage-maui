using copilot_usage_maui.Features.GithubCopilot.Components;
using copilot_usage_maui.Features.Widget.Model;
using copilot_usage_maui.Helpers;
using copilot_usage_maui.Models;
using copilot_usage_maui.Services;

namespace copilot_usage_maui.Features.Widget.Pages;

public partial class HorizontalFloatingPage : Component<UsedState>
{
    [Inject] WidgetService _widgetService;

    protected override void OnMounted()
    {
        base.OnMounted();
        _widgetService.DataChanged += UpdateData;
        if (_widgetService.Current is { } data)
            UpdateData(data);
    }

    protected override void OnWillUnmount()
    {
        base.OnWillUnmount();
        _widgetService.DataChanged -= UpdateData;
    }

    private void UpdateData(WidgetData data)
    {
        SetState(s =>
        {
            s.IconFileName = data.IconFileName;
            s.UsedPercent = data.UsedPercent;
            s.SessionUsedPercent = data.SessionUsedPercent;
            s.WeeklyUsedPercent = data.WeeklyUsedPercent;
            s.ResetTimeText = ShortenResetText(data.ResetTimeText);
        });
    }

    public override VisualNode Render()
        => ContentView(
            HStack(
                new SvgIcon()
                    .FileName(State.IconFileName)
                    .Size(18)
                    .TintColor(AppColors.TextSecondary),

                Data(),

                Label(State.ResetTimeText)
                    .FontSize(9)
                    .TextColor(AppColors.PopupText3)
                    .VCenter()
            )
            .VCenter()
            .Spacing(6)
            .Padding(10, 0)
        )
        .BackgroundColor(AppColors.PopupPage);

    VisualNode Data()
        => State.WeeklyUsedPercent.HasValue ?
             HStack(
                DonutMiniCard("S", State.SessionUsedPercent ?? 0.0),

                Ellipse()
                    .Width(3)
                    .Height(3),

                DonutMiniCard("W", State.WeeklyUsedPercent ?? 0.0)
             )
             .VCenter()
             .Spacing(3) :

             DonutMiniCard("S", State.UsedPercent);

    HStack DonutMiniCard(string title, double pct)
    {
        var fillColor = AppColors.StatusColorForPercent(pct);

        return HStack(
                Label(title).FontSize(11).TextColor(AppColors.PopupText3)
                    .VCenter(),
                Grid(
                    new SkiaCanvas()
                        .WidthRequest(30).HeightRequest(30)
                        .OnPaintSurface((sender, e) =>
                        {
                            DonutRenderer.DrawOnCanvas(
                                e.Surface.Canvas,
                                e.Info.Width, e.Info.Height,
                                strokeWidth: 3.5f * (e.Info.Width / 28f),
                                percent: pct,
                                trackColor: DonutRenderer.GetTrackColor(),
                                fillColor: DonutRenderer.GetStatusColor(pct));
                        }),
                    Label($"{pct:F0}")
                        .FontSize(11)
                        .TextColor(fillColor)
                        .HCenter()
                        .VCenter()
                )
            )
            .Spacing(4)
            .Padding(9, 11);
    }

    string ShortenResetText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // Korean: "3시간 42분 후 초기화"
        var krMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"(\d+)시간\s*(\d+)분");
        if (krMatch.Success)
            return $"{krMatch.Groups[1].Value}h {krMatch.Groups[2].Value}m";

        var krDayMatch = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)일");
        if (krDayMatch.Success)
            return $"{krDayMatch.Groups[1].Value}d";

        // English: "Resets in 3h 42m"
        var enMatch = System.Text.RegularExpressions.Regex.Match(text,
            @"(\d+h\s*\d+m|\d+[dhm])");
        if (enMatch.Success)
            return enMatch.Groups[1].Value;

        return text;
    }
}