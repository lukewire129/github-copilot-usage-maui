using ReactorRouter.Components;

namespace copilot_usage_maui.Shared.Layouts;

partial class RootLayout : Component
{
    public override VisualNode Render()
        => new Outlet();
}
