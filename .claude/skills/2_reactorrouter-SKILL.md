---
name: reactorrouter-routing
description: Create ReactorRouter routing configuration and navigation for MauiReactor apps. Use this skill when building route definitions, nested layouts with Outlet, Link components, programmatic navigation, route parameters, and transitions. ReactorRouter provides React Router v6-style declarative routing as an alternative to MAUI Shell navigation.
---

# ReactorRouter Skill

ReactorRouter is a lightweight routing library for MauiReactor that provides declarative, React Router v6-style navigation.

## When to Use ReactorRouter vs Shell

| Feature | ReactorRouter | Shell |
|---------|---------------|-------|
| Route definition | Declarative array | XML-based |
| Nested layouts | ✅ Outlet component | Limited |
| Transitions | Built-in | Basic |
| Complexity | Better for complex apps | Simple apps |
| Learning curve | React Router style | MAUI native |

**Choose ReactorRouter if:**
- You have nested layouts/routes
- You want React Router-like routing
- You need fine-grained transitions
- Your app structure is complex

---

## Basic Setup

### 1. Define Routes

```csharp
private static readonly RouteDefinition[] Routes =
[
    new RouteDefinition("/", typeof(RootLayout),
        // Child routes render inside RootLayout's Outlet
        new RouteDefinition("dashboard", typeof(DashboardLayout),
            RouteDefinition.Index(typeof(HomePage)),
            new RouteDefinition("settings", typeof(SettingsPage))
                { Transition = TransitionType.SlideLeft },
            new RouteDefinition("profile/:userId", typeof(ProfilePage))
                { Transition = TransitionType.Fade }
        ),
        new RouteDefinition("login", typeof(LoginPage))
            { Transition = TransitionType.Fade },
        new RouteDefinition("*", typeof(NotFoundPage)) // Fallback route
    )
];
```

### 2. Initialize Router in MainPage

```csharp
class MainPage : Component
{
    public override VisualNode Render()
        => ContentPage(
            new Router()
                .Routes(Routes)
                .InitialPath("/dashboard")
        );
}
```

### 3. Use Outlet in Layout Components

```csharp
class DashboardLayout : Component
{
    public override VisualNode Render()
        => Grid("50, *", "200, *",
            // Sidebar
            new Sidebar().GridRowSpan(2),

            // Top Bar
            new TopBar().GridColumn(1),

            // Content Area - Child routes render here
            new Outlet()
                .Transition(TransitionType.Fade)
                .Duration(300)
                .GridRow(1)
                .GridColumn(1)
        );
}
```

---

## Core Components

### Router

```csharp
new Router()
    .Routes(Routes)                    // Required: route definitions
    .InitialPath("/dashboard")         // Initial route on startup
```

### Outlet

Place in layout components where child routes should render.

```csharp
new Outlet()
    .Transition(TransitionType.Fade)   // Slide, Fade, etc.
    .Duration(300)                     // Animation duration ms
    .GridRow(1).GridColumn(1)          // Position in grid
```

### Link

Navigate via link component.

```csharp
// Simple link
new Link().To("/dashboard/settings")

// Link with custom content
new Link().To("/profile/123")
    .Child(Label("View Profile"))

// Link with button styling
new Link().To("/login")
    .Child(Button("Login"))
```

### NavigationService

Programmatic navigation.

```csharp
// Simple navigation
NavigationService.Instance.NavigateTo("/profile/123");

// With callback
NavigationService.Instance.NavigateTo("/settings", () =>
{
    Debug.WriteLine("Navigation complete");
});
```

---

## Route Definition Pattern

```csharp
// Basic route
new RouteDefinition("pagename", typeof(PageComponent))

// Route with parameters
new RouteDefinition("profile/:userId", typeof(ProfilePage))

// Route with transition
new RouteDefinition("settings", typeof(SettingsPage))
    { Transition = TransitionType.SlideLeft }

// Index route (default child)
RouteDefinition.Index(typeof(HomePage))

// Fallback route
new RouteDefinition("*", typeof(NotFoundPage))

// Nested routes
new RouteDefinition("dashboard", typeof(DashboardLayout),
    RouteDefinition.Index(typeof(HomePage)),
    new RouteDefinition("analytics", typeof(AnalyticsPage)),
    new RouteDefinition("reports", typeof(ReportsPage))
)
```

---

## Route Parameters

Parameters defined with `:paramName` are passed to component constructor.

```csharp
// Route definition
new RouteDefinition("profile/:userId", typeof(ProfilePage))

// Component receives parameter
class ProfilePageState
{
    public string UserId { get; set; }
}

class ProfilePage : Component<ProfilePageState>
{
    private string _userId;
    
    public ProfilePage(string userId) 
    { 
        _userId = userId;
    }
    
    public override VisualNode Render()
    {
        return ContentPage("Profile",
            Label($"User: {_userId}")
        );
    }
}
```

---

## Transitions

Available transition types:

```csharp
TransitionType.Fade      // Fade in/out
TransitionType.Slide     // Slide from right
TransitionType.SlideLeft // Slide from left
TransitionType.SlideUp   // Slide from bottom
TransitionType.SlideDown // Slide from top
TransitionType.Scale     // Scale animation
TransitionType.None      // No transition (instant)
```

Example:

```csharp
new RouteDefinition("settings", typeof(SettingsPage))
{
    Transition = TransitionType.SlideLeft
}
```

Apply transition to Outlet:

```csharp
new Outlet()
    .Transition(TransitionType.Fade)
    .Duration(500)
```

---

## Complete Example: Multi-Level Navigation

### Route Definition

```csharp
private static readonly RouteDefinition[] Routes =
[
    new RouteDefinition("/", typeof(RootLayout),
        new RouteDefinition("dashboard", typeof(DashboardLayout),
            RouteDefinition.Index(typeof(HomePage)),
            new RouteDefinition("analytics", typeof(AnalyticsPage)),
            new RouteDefinition("users/:userId", typeof(UserDetailPage))
                { Transition = TransitionType.Fade }
        ),
        new RouteDefinition("settings", typeof(SettingsPage))
            { Transition = TransitionType.SlideLeft },
        new RouteDefinition("login", typeof(LoginPage))
            { Transition = TransitionType.Fade },
        new RouteDefinition("*", typeof(NotFoundPage))
    )
];
```

### RootLayout (Top Level)

```csharp
class RootLayout : Component
{
    public override VisualNode Render()
        => ContentPage(
            new Outlet()
                .Transition(TransitionType.Fade)
                .Duration(300)
        );
}
```

### DashboardLayout (Nested Layout)

```csharp
class DashboardLayout : Component
{
    public override VisualNode Render()
        => Grid("50, *", "200, *",
            // Sidebar with navigation links
            VStack(
                new Link().To("/dashboard")
                    .Child(Label("Home")),
                new Link().To("/dashboard/analytics")
                    .Child(Label("Analytics")),
                new Link().To("/settings")
                    .Child(Label("Settings")),
                new Link().To("/login")
                    .Child(Label("Logout"))
            )
            .Padding(10)
            .Spacing(10)
            .GridRowSpan(2),

            // Top bar
            Label("Dashboard")
                .FontSize(20)
                .FontAttributes(FontAttributes.Bold)
                .Padding(10)
                .GridColumn(1),

            // Content area with transitions
            new Outlet()
                .Transition(TransitionType.Fade)
                .Duration(300)
                .GridRow(1)
                .GridColumn(1)
                .Padding(10)
        );
}
```

### Detail Page with Route Parameters

```csharp
class UserDetailPageState
{
    public string UserId { get; set; }
    public string UserName { get; set; } = "Loading...";
    public bool IsLoading { get; set; } = true;
}

class UserDetailPage : Component<UserDetailPageState>
{
    private string _userId;
    
    public UserDetailPage(string userId)
    {
        _userId = userId;
    }
    
    public override void OnMountedAsync()
    {
        _ = LoadUserAsync();
    }
    
    public override VisualNode Render()
    {
        return ContentPage("User Detail",
            VStack(
                Label($"User ID: {_userId}"),
                State.IsLoading
                    ? ActivityIndicator().IsRunning(true)
                    : Label($"Name: {State.UserName}"),
                new Link().To("/dashboard/analytics")
                    .Child(Button("Back to Analytics"))
            )
            .Center()
            .Padding(20)
            .Spacing(10)
        );
    }
    
    private async Task LoadUserAsync()
    {
        await Task.Delay(1000); // Simulate API call
        SetState(s =>
        {
            s.UserName = $"User {_userId}";
            s.IsLoading = false;
        });
    }
}
```

---

## Common Navigation Patterns

### Button Navigation

```csharp
Button("Go to Settings", () =>
    NavigationService.Instance.NavigateTo("/settings")
)
```

### Conditional Navigation Based on State

```csharp
class LoginPageState
{
    public bool IsLoggedIn { get; set; } = false;
}

class LoginPage : Component<LoginPageState>
{
    public override VisualNode Render()
    {
        return State.IsLoggedIn
            ? new Router()
                .Routes(Routes)
                .InitialPath("/dashboard")
            : ContentPage("Login",
                Button("Login", () => 
                {
                    SetState(s => s.IsLoggedIn = true);
                    NavigationService.Instance.NavigateTo("/dashboard");
                })
            );
    }
}
```

### Back Navigation

```csharp
Button("Back", () =>
    NavigationService.Instance.GoBack()
)
```

---

## Best Practices

1. **Define all routes at app start** - Don't dynamically add routes
2. **Use nested layouts** - Keep sidebar/header in layout, content in Outlet
3. **Route parameters for IDs only** - Use state for complex data
4. **Name routes clearly** - Use lowercase with hyphens: `/user-details`
5. **Fallback route last** - Always end with `new RouteDefinition("*", typeof(NotFoundPage))`
6. **Outlet in layouts** - Child routes render inside parent's Outlet
7. **Transitions on Outlet** - Not on Router, to control child animations
8. **Avoid nested routers** - Use Outlet instead for nested content

---

## Migration from Shell

If converting from Shell to ReactorRouter:

```csharp
// Old (Shell-based)
Shell.Current.GoToAsync("pagename");

// New (ReactorRouter)
NavigationService.Instance.NavigateTo("/pagename");
```

```csharp
// Old (Shell route groups)
// In AppShell.xaml - complex XML structure

// New (ReactorRouter)
// In C# array - cleaner and more flexible
private static readonly RouteDefinition[] Routes = [ ... ];
```

---

## Troubleshooting

### Route not found
- Check spelling of route path
- Ensure route is defined in Routes array
- Check that parent layout has `<Outlet>`

### Parameters not passed
- Parameter must be in route definition: `":userId"`
- Component constructor must accept parameter: `public MyPage(string userId)`
- Parameter names must match

### Transitions not working
- Outlet must have `.Transition()` set
- Duration is in milliseconds
- Parent Router and Outlet both support transitions

### Back button not working
- Use `NavigationService.Instance.GoBack()`
- Or implement custom back button in layout

---

## Resource Links

- [ReactorRouter GitHub](https://github.com/lukewire129/ReactorRouter)
- [NuGet Package](https://www.nuget.org/packages/ReactorRouter/)

---

**Version**: 1.0  
**Last updated**: 2025  
**Framework**: MauiReactor with ReactorRouter routing
