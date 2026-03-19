namespace copilot_usage_maui.SettingsComponents;

class CustomDropDownState
{
    public bool IsOpen { get; set; } = false;
}

partial class CustomDropDown : Component<CustomDropDownState>
{
    [Prop] string[] itemsSource;
    [Prop] int selectedIndex;
    [Prop] Action<int> onSelectedIndexChanged;

    public override VisualNode Render()
    {
        return VStack(
            // 드롭다운 버튼
            Button($"{itemsSource[selectedIndex]}  {(State.IsOpen ? "▴" : "▾")}")
                .HFill()
                .BackgroundColor(AppColors.CardBackground)
                .TextColor(AppColors.CopyButtonText)
                .BorderColor(AppColors.DividerColor)
                .BorderWidth(1)
                .OnClicked(() => SetState(s => s.IsOpen = !s.IsOpen)),

            // 드롭다운 목록 (열려있을 때만 표시)
            State.IsOpen
                ? Border(
                    VStack(
                        itemsSource.Select((item, i) =>
                            (VisualNode)Button(item)
                                .HFill()
                                .BackgroundColor(i == selectedIndex ? AppColors.Accent : Colors.Transparent)
                                .TextColor(i == selectedIndex ? AppColors.TextOnAccent : AppColors.CopyButtonText)
                                                .OnClicked(() =>
                                {
                                    var idx = Array.IndexOf(itemsSource, item);
                                    onSelectedIndexChanged?.Invoke(idx);
                                    SetState(s => s.IsOpen = false);
                                })
                        ).ToArray()
                    )
                    .BackgroundColor(AppColors.CardBackground)
                )
                .BackgroundColor(AppColors.CardBackground)
                .Stroke(AppColors.DividerColor)
                .StrokeThickness(1)
                .StrokeShape(new MauiReactor.Shapes.RoundRectangle())
                .ZIndex(2)
                : null
        )
        .HFill();
    }
}
