# RevitUiController: Build, Run & Debug

## Project Info
- **Framework**: .NET 10 (`net10.0-windows`)
- **Language**: C# (nullable enabled, implicit usings)
- **Solution**: 6 projects under `tools/RevitUiController/` (1 legacy + 5 new)
- **Dependencies**: FlaUI.UIA3, OpenCvSharp4, YamlDotNet, WinForms, MS.DI, Microsoft.Playwright (root only), ModelContextProtocol (McpServer)
- **Platform**: Windows x64 only

## Build Commands
```powershell
# Restore
dotnet restore tools\RevitUiController

# Build all projects
dotnet build tools\RevitUiController -c Debug

# Build individual projects
dotnet build tools\RevitUiController\RevitUiController.csproj -c Debug      # Legacy root
dotnet build tools\RevitUiController\RevitUiController.Core -c Debug
dotnet build tools\RevitUiController\RevitUiController.Revit -c Debug
dotnet build tools\RevitUiController\RevitUiController.Host -c Release      # NEW: DI-based CLI
dotnet build tools\RevitUiController\RevitUiController.Daemon -c Release
dotnet build tools\RevitUiController\RevitUiController.McpServer -c Release
```

## Legacy Root Run (flat arch — no DI)
```powershell
dotnet run --project tools\RevitUiController\RevitUiController.csproj -- state --pretty
dotnet run --project tools\RevitUiController\RevitUiController.csproj -- ribbon Wall Architecture
```
Legacy root includes WebView2 commands (`wv-*`), fallback chain, and 75 commands not in new arch.

## Run Commands (Host — NEW DI-based CLI)

```powershell
# Default: connect to Revit process, run command
dotnet run --project tools\RevitUiController\RevitUiController.Host -- state --pretty
dotnet run --project tools\RevitUiController\RevitUiController.Host -- ribbon "Wall" Architecture --pretty
dotnet run --project tools\RevitUiController\RevitUiController.Host -- find "OK" --pretty

# Connect by window title (any app)
dotnet run --project tools\RevitUiController\RevitUiController.Host -- --window-title "Блокнот" list-controls
dotnet run --project tools\RevitUiController\RevitUiController.Host -- --active info

# Connect by PID
dotnet run --project tools\RevitUiController\RevitUiController.Host -- --pid 1234 click "OK"

# Non-Revit process
dotnet run --project tools\RevitUiController\RevitUiController.Host -- --process-name notepad list-controls

# Run script
dotnet run --project tools\RevitUiController\RevitUiController.Host -- script scripts/create-wall.rvs

# With profile flag
dotnet run --project tools\RevitUiController\RevitUiController.Host -- --profile notepad list-controls

# With automation provider flag
dotnet run --project tools\RevitUiController\RevitUiController.Host -- --provider composite state
```

## Daemon Run
```powershell
# Start daemon (background named pipe server)
dotnet run --project tools\RevitUiController\RevitUiController.Daemon -- --daemon

# With auto-screenshot on error (not documented in other sections)
dotnet run --project tools\RevitUiController\RevitUiController.Daemon -- --daemon --auto-screenshot

# Interactive mode
dotnet run --project tools\RevitUiController\RevitUiController.Daemon

# Send command to running daemon
dotnet run --project tools\RevitUiController\RevitUiController.Daemon -- state
dotnet run --project tools\RevitUiController\RevitUiController.Daemon -- click "OK"
dotnet run --project tools\RevitUiController\RevitUiController.Daemon -- connect
dotnet run --project tools\RevitUiController\RevitUiController.Daemon -- --pipe MyPipe --daemon

# Daemon batch commands
dotnet run --project tools\RevitUiController\RevitUiController.Daemon -- batch '{"commands":[{"command":"click","args":["OK"]}]}'

# Daemon protocol: __undo, __events, __watch
dotnet run --project tools\RevitUiController\RevitUiController.Daemon -- __undo --action status
```

## MCP Server Run
```powershell
dotnet run --project tools\RevitUiController\RevitUiController.McpServer
# Requires: RevitUiController.Daemon running with --daemon flag
# Uses DaemonBridge to communicate with daemon (NO direct connection to Host CLI)
# Supports --pipe <name> flag (default: RevitUiController)
```

## Global Flags (Host)
| Flag | Default | Description |
|------|---------|-------------|
| `--pretty` | false | Pretty-print JSON |
| `--screenshot` | false | Include base64 screenshot in output |
| `--verbosity <level>` | normal | minimal / normal / full |
| `--pid <N>` | null | Connect to specific PID |
| `--process-name <name>` | Revit | Process name to find |
| `--window-title <title>` | null | Connect by window title (contains match) |
| `--active` | false | Connect to foreground window |
| `--connect-timeout <sec>` | 30 | Wait timeout for process |
| `--non-interactive` | false | Auto-reject destructive actions |
| `--uia-only` | false | UIA-only mode (no GDI/mouse for RDP) |
| `--provider <name>` | uia3 | uia3 / wad / composite |
| `--profile <name>` | revit | App profile from config.yaml or by process name |

## Config (`config.yaml`)
Location: `RevitUiController.Host/config.yaml` or `%APPDATA%/UiController/config.yaml`
```yaml
profiles:
  revit:
    processName: Revit
    displayName: Autodesk Revit          # Human-readable name
    pipeName: ReVibe
    executablePaths:
      - "C:\\Program Files\\Autodesk\\Revit 2026\\Revit.exe"
    configDirectory: "%LOCALAPPDATA%/ReVibe/UiController/"
    uiMap: uimap.yaml
    scriptsDir: scripts
    templatesDir: templates
    knownYears: [2022, 2023, 2024, 2025, 2026, 2027]
    llmPrompt: "Look at this screenshot of Autodesk Revit."
  notepad:
    processName: notepad
    displayName: Windows Notepad
defaults:
  profile: revit
  connectTimeout: 30
  verbosity: normal
```

## Test Commands
```powershell
dotnet test tools\RevitUiController.Tests
```

## Known Issues
- `IApplicationLauncher.RegisterLauncher()` defined in Host/Program.cs but never called — launcher not registered in DI
- `ps` alias registered for both `process-list` and PropertySheet commands — conflict
- `allure-open` command documented but does NOT exist in code (only `allure-setup`, `allure-report`)
- WebView2 commands (`wv-*`) exist only in legacy root project, NOT in Core/Commands/
- Host/Plugins/ directory may not exist — code handles gracefully

## Debug Tips
- Use `--pretty` for human-readable JSON output
- Use `--verbosity full` for maximum detail
- Auto-screenshot on error (always, unless `--screenshot` is set)
- Ctrl+C cancels any running command via CancellationToken
- Use `dry-run <script.rvs>` to simulate a script without real clicks
- Deprecated commands show migration hints (e.g. `revit-api` → `pipe-api`, `revit-instances` → `app-instances --profile revit`, `revit-restart` → `app-restart --profile revit`)

## Output Format
All commands return JSON (`CommandResult`):
```json
{
  "command": "ribbon",
  "success": true,
  "diff": { "activeDialog": "Modify | Walls" },
  "data": { ... },
  "durationMs": 1234
}
```
