namespace copilot_usage_maui.Components;

class AppShellState
{
    public bool ShowSettings { get; set; }
}

partial class AppShell : Component<AppShellState>
{
    public override VisualNode Render()
        => Window(
        State.ShowSettings
            ? new SettingsPage()
                .OnBack(() => SetState(s => s.ShowSettings = false))
            : new DashboardPage()
                .OnOpenSettings(() => SetState(s => s.ShowSettings = true))
        )
        .Title("GitHub Copilot Usage")
        .Width(450)
        .Height(700);

}
