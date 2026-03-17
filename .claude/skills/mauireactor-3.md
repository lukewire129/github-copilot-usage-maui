/---
name: mauireactor-3
description: Develop MauiReactor 3.x applications - a declarative UI component framework for .NET MAUI targeting net9.0. Use this skill whenever users create, debug, or optimize MauiReactor 3.x components. Essential for: building stateless/stateful components, property-based animations (WithAnimation), AnimationController sequences, component lifecycle management, navigation, state management patterns, [Prop]/[Param]/[Inject] attributes, or answering MauiReactor 3.x-specific architecture questions. Always use this skill for any MauiReactor question — even if the user just says 'MauiReactor' without specifying a version, default to this skill unless they explicitly mention version 4+.
---

# MauiReactor 3 Skill

> **Version scope**: This skill covers **MauiReactor 3.x** with **net9.0** target framework.
> A separate skill will be added for MauiReactor 4.x when it becomes available.

A complete guide for developing MauiReactor 3.x applications - a declarative UI framework for .NET MAUI using C# instead of XAML.

## Quick Start

MauiReactor lets you write fluent, declarative UI in pure C#:

```csharp
class MainPage : Component
{
    public override VisualNode Render()
        => ContentPage("Login",
            VStack(
                Label("User:"),
                Entry(),
                Label("Password:"),
                Entry(),
                HStack(
                    Button("Login"),
                    Button("Register")
                )
            )
            .Center()
        );
}
```

Key concepts:
- **No XAML needed** - Pure C# fluent syntax
- **MVU Pattern** - Model-View-Update (like React) instead of MVVM
- **Component-based** - Reusable UI blocks with lifecycle
- **Hot-reload support** - Instant feedback during development
- **Declarative** - Describe what UI should look like, not how to build it

---

## Core Components

### 1. Stateless Components

Stateless components are pure functions that render UI without maintaining internal state:

```csharp
class GreetingCard : Component
{
    [Prop] string _name;

    [Prop] Action<string> _onNameChanged;

    public override VisualNode Render()
        => VStack(
            Label($"Hello {_name}!"),
            Button("Clear")
                .OnClicked(() => _onNameChanged?.Invoke(_name))
        )
        .Padding(20);
}
```

**Use stateless components for:**
- Presentational/dumb components
- Reusable UI pieces (cards, headers, buttons)
- Pure functions with no side effects
- Components that receive all data via [Param] attributes

### 2. Stateful Components

Stateful components maintain internal state and handle user interactions:

```csharp
class CounterPageState
{
    public int Count { get; set; } = 0;
}

class CounterPage : Component<CounterPageState>
{
    public override VisualNode Render()
        => ContentPage("Counter",
            VStack(
                Label($"Count: {State.Count}")
                    .FontSize(32)
                    .Bold(),
                Button("+")
                    .OnClicked(() => SetState(s => s.Count++)),
                Button("-")
                    .OnClicked(() => SetState(s => s.Count--))
            )
            .Center()
        );
}
```

**State Management Rules:**
- State class should only contain public properties
- Use value-types (int, double, string, bool) or string for state properties
- Use `SetState()` to trigger re-render: `SetState(s => s.Property = value)`
- State updates are **batched** - multiple SetState calls trigger one render
- For complex state objects, host them in a separate assembly to avoid hot-reload issues

**Component Lifecycle Methods:**

```csharp
class MyComponent : Component<MyState>
{
    public override VisualNode Render() { ... }
    
    // Called when component is first mounted
    public override void OnMounted()
    {
        // Initialize, load data, etc.
    }
    
    // Called when state changes (before render)
    public override void OnWillMount()
    {
        // Prepare for state change
    }
    
    // Called after component unmounts
    public override void OnUnmounted()
    {
        // Cleanup resources
    }
}
```

### 3. Component Parameters

Pass data from parent to child component:

```csharp
// Parent component
class CustomParameter
{
    public int Numeric { get; set; }
}

partial class ParametersPage: Component
{
    [Param]
    IParameter<CustomParameter> _customParameter;

    public override VisualNode Render()
     => ContentPage("Parameters Sample",
        => VStack(spacing: 10,
                Button("Increment from parent", () => _customParameter.Set(_=>_.Numeric += 1   )),
                Label(_customParameter.Value.Numeric),

                new ParameterChildComponent()
            )
            .Center()
        );
}

// Child component with parameters
partial class ParameterChildComponent: Component
{
    [Param]
    IParameter<CustomParameter> _customParameter;
    
    public override VisualNode Render()
      => VStack(spacing: 10,
            Button("Increment from child", ()=> _customParameter.Set(_=>_.Numeric++)),

            Label(customParameter.Value.Numeric)
        );
}
```

---

## Animation System

MauiReactor provides 3 animation approaches. **WithAnimation() placement is critical** for property-based animations.

### Property-Based Animation (WithAnimation)

The simplest animation: animate properties between states using **RxAnimation**.

#### CRITICAL: WithAnimation() Placement

`WithAnimation()` **MUST be called on the component being animated**, and the properties being animated **MUST come BEFORE WithAnimation()** in the chain:

```csharp
// CORRECT - Properties BEFORE WithAnimation()
Image()
    .Source("city.jpg")
    .Scale(State.IsExpanded ? 1.0 : 0.5)
    .Opacity(State.IsExpanded ? 1.0 : 0.0)
    .WithAnimation()  // Position matters!
    .Margin(10)

// WRONG - Properties AFTER WithAnimation()
Image()
    .Source("city.jpg")
    .WithAnimation()
    .Scale(State.IsExpanded ? 1.0 : 0.5)  // Won't animate!
    .Opacity(State.IsExpanded ? 1.0 : 0.0)
    .Margin(10)

// WRONG - WithAnimation() not on animated component
VStack(
    Image()
        .Source("city.jpg")
        .Scale(State.IsExpanded ? 1.0 : 0.5)
        .Opacity(State.IsExpanded ? 1.0 : 0.0),
    Label("Text")
)
.WithAnimation()  // Wrong! Applied to VStack, not Image
```

#### Example: Fade and Scale Animation

```csharp
class ExpandableImageState
{
    public bool IsExpanded { get; set; } = false;
}

class ExpandableImage : Component<ExpandableImageState>
{
    public override VisualNode Render()
        => ContentPage(
            Frame(
                Image()
                    .HCenter()
                    .VCenter()
                    .Source("city.jpg")
                    .OnTap(() => SetState(s => s.IsExpanded = !s.IsExpanded))
                    .Scale(State.IsExpanded ? 1.0 : 0.5)
                    .Opacity(State.IsExpanded ? 1.0 : 0.0)
                    .WithAnimation()  // Position is critical!
                    .Margin(10)
            )
            .HasShadow(true)
        );
}
```

#### Customizing Animations

```csharp
// Default: 600ms, Linear easing
.WithAnimation()

// Custom duration (in milliseconds)
.WithAnimation(1000)

// Custom easing function
.WithAnimation(800, Easing.CubicOut)

// Available easing functions:
// - Easing.Linear
// - Easing.SinIn, SinOut, SinInOut
// - Easing.CubicIn, CubicOut, CubicInOut
// - Easing.BounceOut
// - Easing.SpringOut
```

**Animatable Properties:**
- `Opacity()` - fade in/out
- `Scale()` - grow/shrink
- `Rotation()` - rotate
- `TranslationX()`, `TranslationY()` - move
- `CornerRadius()` - rounded corners
- Most layout properties (Width, Height, Margin, Padding)

### AnimationController (Advanced)

For complex multi-step animations, use `AnimationController`:

```csharp
class AnimatedPageState
{
    public double X { get; set; } = 0;
    public double Y { get; set; } = 0;
    public double Rotation { get; set; } = 0;
}

class AnimatedPage : Component<AnimatedPageState>
{
    private AnimationController _animController = new();

    public override VisualNode Render()
        => ContentPage(
            VStack(
                Frame(
                    Label("Animated Box")
                )
                .TranslationX(State.X)
                .TranslationY(State.Y)
                .Rotation(State.Rotation)
                .Margin(20),

                // Animation controller with sequence
                _animController
                    .Add(
                        new SequenceAnimation
                        {
                            new DoubleAnimation()
                                .StartValue(0)
                                .TargetValue(200)
                                .Duration(TimeSpan.FromSeconds(2))
                                .Easing(Easing.CubicOut)
                                .OnTick(v => SetState(s => s.X = v)),

                            new DoubleAnimation()
                                .StartValue(0)
                                .TargetValue(300)
                                .Duration(TimeSpan.FromSeconds(1.5))
                                .OnTick(v => SetState(s => s.Y = v))
                        }
                    ),

                Button("Start")
                    .OnClicked(() => _animController.PlayAsync()),
                Button("Pause")
                    .OnClicked(() => _animController.IsPaused = !_animController.IsPaused),
                Button("Stop")
                    .OnClicked(() => _animController.IsEnabled = false)
            )
            .Padding(20)
        );
}
```

**Animation Types:**
- `DoubleAnimation` - Animate numeric values
- `CubicBezierPathAnimation` - Smooth path animation with bezier curves
- `QuadraticBezierPathAnimation` - Path animation with quadratic bezier
- `SequenceAnimation` - Run animations one after another
- `ParallelAnimation` - Run multiple animations simultaneously

**AnimationController Controls:**

```csharp
_animController.PlayAsync()          // Start animation
_animController.IsPaused = true       // Pause (resumes from same point)
_animController.IsEnabled = false     // Stop (resets to initial state)
```

---

## Fluent API Pattern

All MauiReactor components use fluent method chaining:

```csharp
Button("Click Me")
    .Text("Click Me!")                    // Property setters
    .BackgroundColor(Colors.Blue)
    .TextColor(Colors.White)
    .Padding(12, 8)
    .FontSize(16)
    .OnClicked(() => HandleClick())       // Event handlers
    .IsEnabled(true)                      // Boolean properties
```

**Common Property Methods:**

```csharp
// Text
.Text(string)
.FontSize(double)
.Bold()
.Italic()

// Layout
.Padding(double)
.Padding(h, v)
.Margin(double)
.Margin(h, v)
.Width(double)
.Height(double)

// Visual
.BackgroundColor(Color)
.TextColor(Color)
.Opacity(double) // 0.0 to 1.0
.Scale(double)
.Rotation(double)

// Positioning
.Center()
.HCenter()
.VCenter()
.StartExpand()
.EndExpand()
.FillExpand()

// Events (all end with handler callback)
.OnClicked(Action)
.OnTap(Action)
.OnPropertyChanged(string, Action)
```

---

## Container Components

### VStack - Vertical Stack

```csharp
VStack(
    Label("Item 1"),
    Label("Item 2"),
    Label("Item 3")
)
.Spacing(10)
.Padding(20)
```

### HStack - Horizontal Stack

```csharp
HStack(
    Button("OK"),
    Button("Cancel")
)
.Spacing(10)
```

### Grid - 2D Layout

```csharp
Grid(
    new GridItemsLayout
    {
        Columns = 3,
        RowHeight = 100,
        HorizontalItemSpacing = 10,
        VerticalItemSpacing = 10
    }
)
.Items(listOfComponents)
```

### ScrollView

```csharp
ScrollView(
    VStack(
        items...
    )
)
.VerticalScroll()
```

### Frame - Bordered Container

```csharp
Frame(
    Label("Content")
)
.BorderColor(Colors.Gray)
.CornerRadius(10)
.HasShadow(true)
.Padding(15)
```

---

## State Management Patterns

MauiReactor offers four patterns depending on the **scope** of your state. They progress from narrow to wide: local → parent-to-child → tree-wide shared → app-wide services.

### Pattern 1: Simple Local State

For state that lives inside a single component. Inherit from `Component<State>` and call `SetState()` to mutate — only that component re-renders.

```csharp
class TodoState
{
    public string Input { get; set; } = "";
    public List<string> Items { get; set; } = new();
}

class TodoApp : Component<TodoState>
{
    public override VisualNode Render()
        => VStack(
            Entry()
                .Text(State.Input)
                .OnTextChanged(v => SetState(s => s.Input = v)),
            Button("Add")
                .OnClicked(() => SetState(s => {
                    s.Items.Add(s.Input);
                    s.Input = "";
                }))
        );
}
```

### Pattern 2: Parent-to-Child Props

For **one-way data flow** from parent to child. The parent passes values via constructor arguments, and the child declares them with `[Prop]`. Children can only read — data flows strictly top-down.

```csharp
partial class ParentPage : Component<ParentPageState>
{
    public override VisualNode Render()
        => VStack(spacing: 10,
            new GreetingCard(userName: State.UserName, age: State.Age),
            new ScoreBadge(score: State.Score)
        );
}

partial class GreetingCard : Component
{
    [Prop] string _userName;
    [Prop] int _age;

    public override VisualNode Render()
        => Frame(
            Label($"{_userName}, {_age} years old")
        );
}

partial class ScoreBadge : Component
{
    [Prop] int _score;

    public override VisualNode Render()
        => Label($"Score: {_score}")
            .FontSize(24)
            .Bold();
}
```

### Pattern 3: Shared State Across Components (Parameter)

For **two-way shared state** across the component tree. An ancestor defines `[Param] IParameter<T>`, and any descendant — regardless of depth — can access it with the same declaration. When any component calls `Set()`, **all components referencing that parameter automatically re-render**.

```csharp
public class AppState
{
    public string UserName { get; set; } = "Guest";
    public int Theme { get; set; }
}

// Parent defines the parameter
partial class MainPage : Component
{
    [Param]
    IParameter<AppState> _appState;

    public override VisualNode Render()
        => VStack(spacing: 10,
            Label($"Current User: {_appState.Value.UserName}"),

            new ProfileEditor(),
            new ThemeSwitcher()
        ).Center();
}

// Child reads AND writes the shared state
partial class ProfileEditor : Component
{
    [Param]
    IParameter<AppState> _appState;

    public override VisualNode Render()
        => VStack(spacing: 5,
            Label($"Hello, {_appState.Value.UserName}"),
            Button("Change to Admin", () =>
                _appState.Set(s => s.UserName = "Admin"))
        );
}

// Another child also reads AND writes
partial class ThemeSwitcher : Component
{
    [Param]
    IParameter<AppState> _appState;

    public override VisualNode Render()
        => VStack(spacing: 5,
            Label($"Theme: {_appState.Value.Theme}"),
            Button("Next Theme", () =>
                _appState.Set(s => s.Theme += 1))
        );
}
```

### Pattern 4: Dependency Injection

For app-wide **services and infrastructure** (API clients, databases, business logic). Register services in `Program.cs` and inject them with `[Inject]`. This is not UI state — it connects components to external logic.

```csharp
// In Program.cs
builder.Services.AddSingleton<TodoService>();

// In component
class TodoPage : Component<TodoPageState>
{
    [Inject] TodoService _todoService;

    public override void OnMounted()
    {
        var todos = _todoService.GetTodos();
        // Use todos...
    }
}
```

### Summary

| Pattern | Scope | Direction | Use Case |
|---|---|---|---|
| `Component<State>` | Single component | — | Local UI state |
| `[Prop]` | Parent → Child | One-way | Passing data down |
| `[Param] IParameter<T>` | Entire tree | Two-way | Shared state |
| `[Inject]` | App-wide | — | Services & logic |

---

## Navigation

### Basic Navigation

```csharp
class HomePage : Component
{
    public override VisualNode Render()
        => ContentPage(
            Button("Go to Details")
                .OnClicked(async () => 
                    await Navigation.PushAsync(new DetailsPage())
                )
        );
}
```

### Navigation with Parameters(Props Classes)

```csharp
class ChildPageProps
{
    public int InitialValue { get; set; }
    public Action<int>? OnValueSet { get; set; }
}

class ChildPage : Component<ChildPageState, ChildPageProps>
{
    public override VisualNode Render()
        => ContentPage("Child Page",
            VStack(
                Label($"Initial Value: {Props.InitialValue}"),
                Button("Set Value", () => Props.OnValueSet?.Invoke(42))
            ));
}

// Navigation with props
await Navigation.PushAsync<ChildPage, ChildPageProps>(_ =>
{
    _.InitialValue = State.Value;
    _.OnValueSet = this.OnValueSetFromChildPage;
});
```

### NavigationPage Wrapper

```csharp
class App : Component
{
    public override VisualNode Render()
        => NavigationPage(
            HomePage()
        )
        .Title("My App");
}
```

---

## Common Patterns & Best Practices

### 1. Form Handling

```csharp
class FormState
{
    public string Name { get; set; }
    public string Email { get; set; }
    public bool IsSubmitting { get; set; } = false;
}

class FormPage : Component<FormState>
{
    public override VisualNode Render()
        => VStack(
            Entry()
                .Placeholder("Name")
                .Text(State.Name)
                .OnTextChanged(v => SetState(s => s.Name = v)),
            
            Entry()
                .Placeholder("Email")
                .Text(State.Email)
                .OnTextChanged(v => SetState(s => s.Email = v)),
            
            Button("Submit")
                .IsEnabled(!State.IsSubmitting)
                .OnClicked(HandleSubmit)
        )
        .Padding(20);

    private async void HandleSubmit()
    {
        SetState(s => s.IsSubmitting = true);
        try
        {
            await Api.SubmitForm(State.Name, State.Email);
        }
        finally
        {
            SetState(s => s.IsSubmitting = false);
        }
    }
}
```

### 2. List Rendering

```csharp
class ListState
{
    public List<Item> Items { get; set; }
}

class ListPage : Component<ListState>
{
    public override VisualNode Render()
        => ContentPage(
            CollectionView()
                .Items(State.Items, item => 
                      new ItemRow(item)
                )
        );
}

class ItemRow : Component
{
    private Item _item;
    
    public  ItemRow(Item item)
    {
         this._item = item;
    }

    public override VisualNode Render()
        => Frame(
            Label(_item.Title)
        )
        .Margin(10);
}
```

### 3. Loading States

```csharp
class DataPageState
{
    public bool IsLoading { get; set; } = true;
    public string Data { get; set; }
    public string Error { get; set; }
}

class DataPage : Component<DataPageState>
{
    public override void OnMounted()
    {
          LoadData();
    }

    private async void LoadData()
    {
        try
        {
            var result = await Api.GetData();
            SetState(s => {
                s.Data = result;
                s.IsLoading = false;
            });
        }
        catch (Exception ex)
        {
            SetState(s => {
                s.Error = ex.Message;
                s.IsLoading = false;
            });
        }
    }

    public override VisualNode Render()
    {
        if (State.IsLoading)
            return ContentPage(ActivityIndicator().IsRunning(true));

        if (!string.IsNullOrEmpty(State.Error))
            return ContentPage(Label($"Error: {State.Error}"));

        return ContentPage(Label(State.Data));
    }
}
```

---

## Setup & Configuration

### Create New Project

```bash
# Install template
dotnet new install Reactor.Maui.TemplatePack

# Create project
dotnet new maui-reactor-startup -o my-app
cd my-app

# Build for Android (net9.0)
dotnet build -f net9.0-android

# Run with hot-reload
dotnet build -t:Run -f net9.0-android
```

### Hot-Reload Setup

```bash
# Install hot-reload tool
dotnet tool install -g Reactor.Maui.HotReloadConsole

# Start hot-reload (one terminal)
dotnet-maui-reactor -f net9.0-android

# Run app (another terminal)
dotnet build -t:Run -f net9.0-android
```

### Package Structure

```
MyApp/
├── Program.cs          # App entry point
├── HomePage.cs         # Main page component
├── Services/           # Business logic
├── Models/             # Data classes
└── Components/         # Reusable components
```

### Program.cs Template

```csharp
using Microsoft.Maui;
using Microsoft.Maui.Hosting;
using MauiControls = Microsoft.Maui.Controls;

namespace MyApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiReactorApp<HomePage>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-SemiBold.ttf", "OpenSansSemiBold");
            });

        return builder.Build();
    }
}
```

---

## Debugging Tips

### 1. Check WithAnimation() Placement

Most animation issues stem from `WithAnimation()` being in the wrong position:
- Must be on the **animated component**
- Must come **after** property setters
- Not on parent containers
- Not between properties

### 2. State Mutations

```csharp
// Wrong - direct mutation
SetState(s => {
    s.Items.Add(item);  // Doesn't trigger re-render reliably
});

// Right - create new list
SetState(s => {
    s.Items = new List<Item>(s.Items) { item };
});
```

### 3. Hot-Reload Issues

- State classes should only have public value-type properties
- Host complex state in a separate assembly
- Clear app cache if hot-reload stops working
- Check that adb is installed for Android hot-reload

### 4. Performance

- Use `OnWillMount()` to avoid expensive operations during render
- Avoid creating large lists in Render() method
- Use ComponentView for heavy custom rendering
- Consider splitting large components

---

## Key Documentation Links

- Official Docs: https://adospace.gitbook.io/mauireactor
- GitHub Samples: https://github.com/adospace/reactorui-maui-samples
- Animation Deep Dive: https://adospace.gitbook.io/mauireactor/components/animation
- Component Lifecycle: https://adospace.gitbook.io/mauireactor/components/component-life-cycle

---

## Version Support

| Item | Version |
|---|---|
| **Skill scope** | MauiReactor **3.x** only |
| **Target Framework** | **net9.0** |
| **.NET MAUI** | 9.0+ |
| **Platforms** | Android, iOS, macOS, Windows |

> MauiReactor 4.x will be covered by a separate **MauiReactor4** skill once available.