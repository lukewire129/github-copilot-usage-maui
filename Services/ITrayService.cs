namespace copilot_usage_maui.Services;

public interface ITrayService
{
    void Initialize();

    Action? ClickHandler { get; set; }

    Action<int, int>? RightClickHandler { get; set; }
}