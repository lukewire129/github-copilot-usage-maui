using copilot_usage_maui.Platforms.Windows;
using copilot_usage_maui.Services;
using ReactorRouter;
using ReactorRouter.Routing;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace copilot_usage_maui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiReactorApp<App>(app =>
                    {
                        app.UseTheme<ApplicationTheme>();
                    },
                    unhandledExceptionAction: e =>
                    {
                        System.Diagnostics.Debug.WriteLine(e.ExceptionObject);
                    }
                )
                .UseSkiaSharp()
                .UseReactorRouter((r) =>
                {
                    r.Routes(
                        new RouteDefinition("/", typeof(Shared.Layouts.RootLayout),
                        new RouteDefinition("ai", typeof(Shared.Layouts.MainLayout),
                            new RouteDefinition("githubcopilot", typeof(Features.GithubCopilot.Pages.GithubDashBoardPage)),
                            new RouteDefinition("claude", typeof(Features.Claude.Pages.ClaudeDashBoardPage))
                        ),
                    new RouteDefinition("settings", typeof(copilot_usage_maui.Features.Settings.Pages.SettingsPage))
                    ));

                    r.InitialPath("/ai/githubcopilot");
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<SettingsService>();
            builder.Services.AddSingleton<GitHubCopilotService>();
            builder.Services.AddSingleton<ClaudeUsageService>();
            builder.Services.AddSingleton<NotificationService>();
            builder.Services.AddSingleton<WidgetService>();

            builder.Services.AddSingleton<WidgetContextMenuService>();

            builder.Services.AddSingleton<ITrayService, TrayService>();
            builder.Services.AddSingleton<MainWindowService>();


            return builder.Build();
        }
    }
}
