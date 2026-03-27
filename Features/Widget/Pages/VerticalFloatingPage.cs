using copilot_usage_maui.Features.GithubCopilot.Components;
using copilot_usage_maui.Features.Widget.Model;
using copilot_usage_maui.Helpers;
using copilot_usage_maui.Models;
using copilot_usage_maui.Services;

namespace copilot_usage_maui.Features.Widget.Pages;

public partial class VerticalFloatingPage : Component<UsedState>
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
                VStack(
                    new SvgIcon()
                        .FileName(State.IconFileName)
                        .Size(20)
                        .TintColor(AppColors.TextSecondary),

                    Data(),

                    Label(State.ResetTimeText)
                        .FontSize(8)
                        .TextColor(AppColors.PopupText3)
                        .Center()
                )
                .Spacing(5)
                .Padding(8, 10)
            )
            .BackgroundColor(AppColors.PopupPage);

    VisualNode Data()
        => State.WeeklyUsedPercent.HasValue ?
             VStack(
                DonutMiniCardS("S", State.SessionUsedPercent ?? 0.0),

                Border()
                    .Height(1)
                    .Width(10)
                    .BackgroundColor(AppColors.DividerColor)
                    .HFill(),

                DonutMiniCardW("W", State.WeeklyUsedPercent ?? 0.0)
             )
             .HCenter()
             .Spacing(6) :
             DonutMiniCardS("S", State.UsedPercent)
                .HCenter();
    VStack DonutMiniCardS(string title, double pct)
        => VStack(
                TitleText(title),
                Grid(
                    Dounut(pct),
                    PercentText(pct)
                )
            )
            .Spacing(4);

    VStack DonutMiniCardW(string title, double pct)
        => VStack(
                Grid(
                    Dounut(pct),
                    PercentText(pct)
                ),
                TitleText(title)
            )
            .Spacing(4);

    Label TitleText(string title)
       => Label(title)
            .FontSize(7)
            .TextColor(AppColors.PopupText3)
            .HCenter();

    Label PercentText(double pct)
    {
        var fillColor = AppColors.StatusColorForPercent(pct);

        return Label($"{pct:F0}")
                        .FontSize(11)
                        .TextColor(fillColor)
                        .Center();
    }

    SkiaCanvas Dounut(double pct)
        => new SkiaCanvas()
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
                });

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