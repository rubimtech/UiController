# RevitUiController: Extending the Codebase

## Project Structure — Where to Put Code

| What | Project |
|------|---------|
| Generic commands (any app) | `RevitUiController.Core/Commands/` |
| Revit-specific commands | `RevitUiController.Revit/Commands/` |
| Service interface | `RevitUiController.Core/Services/I*.cs` |
| Service implementation | `RevitUiController.Core/Services/*.cs` |
| Plugin (IPlugin) | Separate DLL → `Host/Plugins/` |
| Profile (IApplicationProfile) | `RevitUiController.Revit/` or any project |
| Launcher (IApplicationLauncher) | Any project |
| Provider (IAutomationProvider) | `RevitUiController.Core/` or any project |

## How to Add a New Command

### Create command file in `Core/Commands/` (generic) or `Revit/Commands/` (Revit-specific)

Namespace: `UiController.Core.Commands` or `RevitUiController.Revit.Commands`

```csharp
using FlaUI.Core.AutomationElements;
using UiController.Core;
using UiController.Core.Models;

namespace UiController.Core.Commands;

public class MyNewCommand : UiCommandBase
{
    public override string Name => "my-command";
    public override string Description => "Does something cool";
    public override string Usage => "my-command <arg1> [--flag value]";

    protected override async Task<CommandResult> ExecuteInternalAsync(
        AutomationElement window, string[] args, CancellationToken ct)
    {
        RequireArgs(args, 1);

        var value = GetFlag(args, "--flag", "default");
        var hasFlag = HasFlag(args, "--verbose");

        var element = FindElement(window, args[0]);
        if (element == null)
            return new CommandResult
            {
                Success = false,
                Error = $"NotFound: element '{args[0]}' not found"
            };

        return new CommandResult { Success = true, Data = new { result = "ok" } };
    }
}
```

### Registration
Commands are **auto-discovered** via assembly scan in `Host/Program.cs`:
```csharp
Registry.RegisterType(typeof(MyNewCommand));
// or register alias:
Registry.RegisterAlias("mc", "my-command");
```

### UiCommandBase Helper Methods
| Method | Purpose |
|--------|---------|
| `FindElement(root, name)` | Find first enabled visible element by name |
| `RequireArgs(args, min)` | Throw if not enough args |
| `GetFlag<T>(args, flag, default)` | Parse typed flag value from args |
| `HasFlag(args, flag)` | Check if flag is present |

## How to Add a Service

### 1. Create interface in `Core/Services/`
```csharp
namespace UiController.Core.Services;
public interface IMyService
{
    string DoSomething();
}
```

### 2. Create implementation in `Core/Services/`
```csharp
namespace UiController.Core.Services;
public class MyService : IMyService
{
    public string DoSomething() => "result";
}
```

### 3. Register in `Host/Program.cs` DI
```csharp
services.AddSingleton<IMyService, MyService>();
```

## How to Create a Plugin (IPlugin)

### 1. Create a separate class library
```csharp
using UiController.Core;
using UiController.Core.Commands;

public class MyPlugin : IPlugin
{
    public string Name => "MyPlugin";
    public void RegisterCommands(CommandRegistry registry)
    {
        registry.Register(new MyCommand());
        registry.RegisterAlias("mc", "my-command");
    }
}
```

### 2. Build to `Host/Plugins/`
```
RevitUiController.Host/bin/Debug/net10.0-windows/Plugins/MyPlugin.dll
```

Plugin DLLs are auto-loaded at startup.

## How to Add a Custom Profile

### Via config.yaml (no code)
```yaml
profiles:
  myapp:
    processName: MyApp
    displayName: My Application
    executablePaths:
      - "C:\\Program Files\\MyApp\\MyApp.exe"
```
Usage: `--profile myapp`

### Via code (IApplicationProfile)
```csharp
public class MyAppProfile : IApplicationProfile
{
    public string Name => "MyApp";
    public string ProcessName => "MyApp";
    public string[] ExecutablePaths => ["C:\\Program Files\\MyApp\\MyApp.exe"];
    public string? PipeName => null;
    // ...
}
```
Then use `--profile MyApp` or set `CoreSettings.CurrentProfile = new MyAppProfile()`.

## How to Add a Custom Automation Provider

```csharp
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

public class MyProvider : IAutomationProvider
{
    public UIA3Automation? UIA3 => null;
    public bool IsUia3 => false;
    public string Name => "MyProvider";
    // implement all methods...
}
```
Register in `Host/Program.cs` `CreateAutomationProvider()` method.

## AutomationHelper Key Methods
| Method | Purpose |
|--------|---------|
| `FindFirstEnabledVisible(parent, name)` | BFS search with UiMap + fallbacks |
| `SafeGetChildren(element, timeoutMs)` | Get children with timeout |
| `FindControlsByName(parent, name, max)` | Parallel BFS across all children |
| `FindActiveDialogs(parent)` | Get open dialog windows |
| `FindFieldByLabel(dialog, label)` | Find Edit/ComboBox near a Text label |
| `FindCheckboxByLabel(dialog, label)` | Find checkbox by label text |
| `FindComboByLabel(dialog, label)` | Find ComboBox by label |
| `FindDialogButton(dialog, name)` | Find button in dialog |
| `TryClick(element, label)` | Safe click with logging |
| `SendTextSafe(element, text)` | ValuePattern → SendKeys fallback |
| `Tokenize(line)` | Quote-aware string tokenization |

## Element Search Strategies (for ai-find)
1. Name exact/starts-with/contains
2. LocaleMap RU↔EN translation
3. AutomationId search (full tree)
4. Regex match on Name
5. Sibling elements (same Y-level, with `--deep`)
6. Tab-scoped search (switch tab and search)

## Writing Tests
Tests are xUnit-based in `RevitUiController.Tests`:
```powershell
dotnet test tools\RevitUiController.Tests
```

## Key Patterns to Follow
- **Use `Safe()` extension** instead of empty `catch {}` — wraps try-catch with logging
- **Use `await Task.Delay(..., ct)`** instead of `Thread.Sleep` — respects CancellationToken
- **Return `CommandResult`** with `Success`, optional `Error`, `Diff`, `Data`
- **Use `LoggingService.Warn(context, message)`** for non-fatal errors
- **Extend `UiCommandBase`** rather than raw `ICommand` for automatic state diff
- **Check `CoreSettings.IsUiaOnly`** if you use mouse/GDI and need RDP support

## Configuration Files
| File | Location | Format |
|------|----------|--------|
| config.yaml | `./config.yaml`, `%APPDATA%/UiController/config.yaml` | YAML |
| UiMap | `./uimap.yaml`, `./config/`, `%LOCALAPPDATA%/ReVibe/UiController/` | YAML |
| Locale | `./locale.yaml`, `./config/`, `%LOCALAPPDATA%/ReVibe/UiController/` | YAML |
| Templates | `./templates/`, `./cv-templates/`, `%LOCALAPPDATA%/ReVibe/UiController/templates/` | PNG |
| Logs | `%LOCALAPPDATA%/ReVibe/UiController/logs/` | Text |

## UiMap YAML Format
```yaml
entries:
  WallButton:
    automationId: RibbonButton_Wall
    name: Стена
    tab: Архитектура
    fallbacks: [Wall, Стена]
    versions:
      "2025": { automationId: RibbonButton_Wall_2025 }
```

## Environment Variables
| Variable | Purpose |
|----------|---------|
| `ROUTERAI_API_KEY` | RouterAI LLM Vision auth |
| `OPENAI_API_KEY` | OpenAI LLM Vision auth |
| `ANTHROPIC_API_KEY` | Anthropic LLM Vision auth |
