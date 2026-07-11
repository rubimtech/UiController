# RevitUiController: Build, Run & Debug

## Project Info
- **Framework**: .NET 10 (`net10.0-windows`)
- **Language**: C# (nullable enabled, implicit usings)
- **Dependencies**: FlaUI.UIA3, OpenCvSharp4, YamlDotNet, WinForms
- **Platform**: Windows x64 only

## Build Commands
```powershell
# Restore & build
dotnet restore tools\RevitUiController
dotnet build tools\RevitUiController -c Release
dotnet build tools\RevitUiController -c Debug
```

## Run Commands
```powershell
# Connect to first Revit process and run a command
dotnet run --project tools\RevitUiController -- state --pretty
dotnet run --project tools\RevitUiController -- ribbon "Wall" Architecture --pretty
dotnet run --project tools\RevitUiController -- find "OK" --pretty

# Connect to any window by title
dotnet run --project tools\RevitUiController -- --window-title "Блокнот" list-controls
dotnet run --project tools\RevitUiController -- --active info

# Connect by PID
dotnet run --project tools\RevitUiController -- --pid 1234 click "OK"

# Non-Revit process
dotnet run --project tools\RevitUiController -- --process-name notepad list-controls

# Run a script file
dotnet run --project tools\RevitUiController -- script scripts/create-wall.rvs
```

## Global Flags
| Flag | Default | Description |
|------|---------|-------------|
| `--pretty` | false | Pretty-print JSON |
| `--screenshot` | false | Include base64 screenshot in output |
| `--verbosity` | normal | minimal / normal / full |
| `--pid <N>` | null | Connect to specific PID |
| `--process-name <name>` | Revit | Process name to find |
| `--window-title <title>` | null | Connect by window title (contains match) |
| `--active` | false | Connect to foreground window |
| `--connect-timeout <sec>` | 30 | Wait timeout for process |
| `--non-interactive` | false | Auto-reject destructive actions |
| `--uia-only` | false | UIA-only mode (no GDI/mouse for RDP) |

## Test Commands
```powershell
# Requires running Revit
dotnet test tools\RevitUiController.Tests
```

## Debug Tips
- Use `--pretty` for human-readable JSON output
- Use `--verbosity full` for maximum detail
- Auto-screenshot on error (always, unless `--screenshot` is set)
- Use `dry-run <script.rvs>` to simulate a script without real clicks
- Use `script-list`, `script-log`, `script-diff` for script versioning
- Ctrl+C cancels any running command via CancellationToken

## Output Format
All commands return JSON (`CommandResult`):
```json
{
  "command": "ribbon",
  "success": true,
  "diff": { "activeDialog": "Modify | Walls", "newDialogs": [...], "closedDialogs": [] },
  "data": { ... },
  "durationMs": 1234
}
```
