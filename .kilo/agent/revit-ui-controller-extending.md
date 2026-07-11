# RevitUiController: Extending the Codebase

## How to Add a New Command

### 1. Create a new file in `Commands/`

Implement `ICommand` (raw) or extend `UiCommandBase` (recommended):

```csharp
using FlaUI.Core.AutomationElements;
using RevitUiController.Models;

namespace RevitUiController.Commands;

public class MyNewCommand : UiCommandBase
{
    public override string Name => "my-command";
    public override string Description => "Does something cool";
    public override string Usage => "my-command <arg1> [--flag value]";

    protected override async Task<CommandResult> ExecuteInternalAsync(
        AutomationElement window, string[] args, CancellationToken ct)
    {
        RequireArgs(args, 1); // throws if args.Length < 1

        var value = GetFlag(args, "--flag", "default");
        var hasFlag = HasFlag(args, "--verbose");

        var element = FindElement(window, args[0]);
        if (element == null)
            return new CommandResult
            {
                Success = false,
                Error = $"NotFound: element '{args[0]}' not found"
            };

        // ... do work ...
        return new CommandResult { Success = true, Data = new { result = "ok" } };
    }
}
```

### 2. Register in `Program.cs` static constructor

```csharp
Register(new MyNewCommand());
```

### 3. Optionally add an alias

```csharp
private static readonly Dictionary<string, string> Aliases = new()
{
    ["mc"] = "my-command",
};
```

### 4. Add to help text in `PrintHelp()`

## UiCommandBase Helper Methods

| Method | Purpose |
|--------|---------|
| `FindElement(root, name)` | Find first enabled visible element by name |
| `RequireArgs(args, min)` | Throw if not enough args |
| `GetFlag<T>(args, flag, default)` | Parse typed flag value from args |
| `HasFlag(args, flag)` | Check if flag is present |

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
- **Check `Program.IsUiaOnly`** if you use mouse/GDI and need RDP support

## Configuration Files
| File | Location | Format |
|------|----------|--------|
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
