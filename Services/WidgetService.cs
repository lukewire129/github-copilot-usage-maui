using copilot_usage_maui.Models;

namespace copilot_usage_maui.Services;

public class WidgetService
{
    public WidgetData? Current { get; private set; }

    public event Action<WidgetData>? DataChanged;

    public void Update(WidgetData data)
    {
        Current = data;
        DataChanged?.Invoke(data);
    }
}
