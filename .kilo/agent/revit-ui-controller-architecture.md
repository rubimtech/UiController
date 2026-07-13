# RevitUiController: Architecture & Patterns

## Solution Structure (6 projects)

```
RevitUiController/
├── RevitUiController.csproj (root)     # Legacy entry point — flat architecture, no DI
│   ├── Program.cs                      # Static Commands dictionary, manual routing
│   ├── Commands/                       # 75 command files (includes WebView2 commands)
│   ├── Models/                         # 6 model files
│   ├── ClickFallbackChain.cs           # Click strategy chain with fallback
│   ├── PipeBridgeClient.cs             # Named pipe to Revit API (events, heartbeat)
│   ├── RevitVersionProfile.cs          # Version detection from registry/file/title
│   ├── WebView2Client.cs              # Playwright-based CDP client
│   ├── Retry.cs, ScreenshotHelper.cs, MouseControl.cs, NativeMethods.cs
│   └── RevitInstanceManager.cs, UiMap.cs
│
├── RevitUiController.Core/             # Core library — no Revit dependency
│   ├── Commands/                       # Generic commands (55 files)
│   ├── Models/                         # CommandResult, ElementInfo, etc.
│   ├── Services/                       # 11 interfaces + 11 implementations (22 files)
│   ├── Protocol/                       # DaemonProtocol (DaemonRequest/Response, DaemonClient)
│   ├── ICommand.cs                     # Command interface
│   ├── UiCommandBase.cs                # Abstract base with auto state-capture
│   ├── CommandRegistry.cs              # Centralized command registry
│   ├── IApplicationProfile.cs          # App profile interface
│   ├── IApplicationLauncher.cs         # App launcher interface
│   ├── IAutomationProvider.cs          # UIA provider abstraction
│   ├── IPlugin.cs                      # Plugin interface
│   ├── AutomationHelper.cs             # FlaUI search/interaction
│   ├── WindowSession.cs                # FlaUI UIA3 wrapper
│   ├── DesktopWindowManager.cs         # Window finding/switching
│   ├── ConfigLoader.cs / ConfigModel.cs# config.yaml support
│   ├── CoreSettings.cs                 # Global settings singleton
│   ├── UiMap.cs / LocaleMap.cs         # Page Object Model
│   ├── ActiveWindowTracker.cs          # Tracks foreground window changes
│   ├── ElementCache.cs / ElementSearchStrategies.cs
│   ├── EventService.cs / HighlightHelper.cs
│   ├── OutputFormatter.cs / CommandResultStore.cs
│   ├── RecorderService.cs / SessionContext.cs
│   ├── SafeExtensions.cs / SafetyGuard.cs / Win32Helper.cs
│   ├── LlmVisionClient.cs / LlmVisionCache.cs
│   ├── UIA3AutomationProvider.cs / WinAppDriverProvider.cs / WinAppDriverClient.cs
│   └── CompositeAutomationProvider.cs
│
├── RevitUiController.Revit/            # Revit-specific extensions
│   ├── Commands/                       # Revit-specific commands (15)
│   ├── RevitProfile.cs                 # IApplicationProfile for Revit
│   ├── RevitLauncher.cs                # IApplicationLauncher for Revit
│   ├── RevitPlugin.cs                  # Plugin registration
│   ├── PipeBridgeClient.cs             # Named pipe to Revit API bridge
│   └── RevitInstanceManager.cs         # Multi-instance management
│
├── RevitUiController.Host/             # CLI host (NEW architecture — DI-based)
│   ├── Program.cs                      # DI setup, flag parsing, command dispatch
│   ├── config.yaml                     # Profiles & defaults
│   └── Plugins/                        # Plugin DLLs loaded at runtime (dir may not exist)
│
├── RevitUiController.Daemon/           # Background daemon server
│   ├── Program.cs                      # Named pipe server + client CLI
│   ├── DaemonServer.cs                 # Persistent command execution server
│   ├── DaemonSettings.cs               # Auto-screenshot config
│   └── EventWatcherService.cs          # Dialog open/close event polling
│
└── RevitUiController.McpServer/        # MCP stdio server
    ├── Program.cs                      # DI builder + McpServer setup
    ├── DaemonBridge.cs                 # DaemonClient wrapper with connection mgmt
    ├── McpConfig.cs                    # Pipe name config
    └── RevitUiTools.cs                 # 33 [McpServerTool] annotated methods
```

## Execution Flow

```
Runtime flow (Host — new arch):
  Host CLI → DI container → Profile → Provider → WindowSession → Command

Daemon flow:
  Daemon (named pipe) → DaemonServer → Command

MCP flow:
  MCP Client (stdio) → McpServer → DaemonBridge → Daemon DaemonServer → Command
  (MCP has NO direct connection to Host CLI — goes through Daemon only)
```

## DI Container (Microsoft.Extensions.DependencyInjection)

Registered in `RevitUiController.Host/Program.cs`:

### Providers (IAutomationProvider)
| Provider | Flag | Description |
|----------|------|-------------|
| `UIA3AutomationProvider` | `--provider uia3` (default) | FlaUI UIA3 |
| `WinAppDriverProvider` | `--provider wad` | WinAppDriver REST API |
| `CompositeAutomationProvider` | `--provider composite` | UIA3 + WAD fallback |

### Service Interfaces (in `Core/Services/`)
11 interfaces + 11 implementations = 22 files:
| Interface | Implementation | Purpose |
|-----------|---------------|---------|
| `IAutomationService` | `AutomationServiceWrapper` | Session lifecycle |
| `ILoggingService` | `LoggingServiceWrapper` | Structured logging |
| `IScreenshotService` | `ScreenshotService` | Screenshot capture |
| `IOutputFormatterService` | `OutputFormatterService` | JSON formatting |
| `IUiMapService` | `UiMapService` | Page Object Model |
| `ISafetyGuardService` | `SafetyGuardService` | Destructive action guard |
| `IEventService` | `EventServiceWrapper` | UIA event-driven automation |
| `IRecorderService` | `RecorderServiceWrapper` | Action recording |
| `ISessionContextService` | `SessionContextService` | Session state |
| `ICvMatchService` | `CvMatchService` | OpenCV MatchTemplate |
| `ILlmVisionService` | `LlmVisionService` | LLM Vision |

## Application Profiles

```yaml
# config.yaml
profiles:
  revit:
    processName: Revit
    displayName: Autodesk Revit
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

- `IApplicationProfile` — ProcessName, ExecutablePaths, PipeName, ConfigDirectory, KnownVersions, UiMapFileName, LocaleFileName, TemplatesDirectory, ScriptsDirectory, DetectVersionFromTitle(), DetectVersionFromFileVersion(), BuildLlmSystemPrompt()
- `RevitProfile` — hardcoded Revit defaults
- `GenericProfile` — any process by name
- Custom profiles via `config.yaml`

## Key Interfaces

### IApplicationProfile
`Name`, `ProcessName`, `DisplayName`, `ExecutablePaths`, `PipeName`, `ConfigDirectory`, `KnownVersions`, `UiMapFileName`, `LocaleFileName`, `TemplatesDirectory`, `ScriptsDirectory`, `DetectVersionFromTitle()`, `DetectVersionFromFileVersion()`, `BuildLlmSystemPrompt()`

### IApplicationLauncher
`Launch()`, `FindRunning()`, `WaitForReady()`, `IsAlive()`
⚠️ NOTE: `RegisterLauncher()` in Host/Program.cs is defined but NOT called from `ConfigureServices()` — IApplicationLauncher is never registered in DI.

### IAutomationProvider
`GetDesktop()`, `GetRootElement(IntPtr hwnd)`, `FindFirst()`, `FindFirstEnabledVisible()`, `FindAllChildren()`, `FindAllByControlType()`, `FindActiveDialogs()`, `UIA3` (property), `IsUia3` (property), `Name` (property), implements `IDisposable`

### IPlugin
`Name`, `RegisterCommands(CommandRegistry)`

### CommandRegistry
`Register(ICommand)`, `Register<T>()`, `RegisterType(Type)`, `RegisterAlias()`, `GetCommand()`, `GetCommandType()`, `AllCommands` (property), `AllAliases` (property), `AllCommandTypes` (property)

## Daemon Protocol (named pipe `\\.\pipe\RevitUiController`)

Line-delimited JSON. `DaemonRequest` has 28 fields:

| Field | Type | Description |
|-------|------|-------------|
| `command` | string | Command name or `__connect`, `__ping`, `__shutdown`, `__batch`, `__undo`, `__events`, `__watch` |
| `args` | List\<string\>? | Positional arguments |
| `pid` | int? | Process ID to connect to |
| `processName` | string? | Process name filter |
| `windowTitle` | string? | Window title filter |
| `useActive` | bool | Connect to foreground window |
| `timeout` | int? | Command/connection timeout (seconds) |
| `interval` | int? | Polling interval for watch |
| `condition` | string? | Watch condition (found/gone/enabled/disabled/text:...) |
| `subCommand` | string? | Sub-command for compound operations |
| `subArgs` | List\<string\>? | Sub-command arguments |
| `action` | string? | Action type (status/undo/checkpoint/undo-to) |
| `count` | int? | Count (undo steps, events, etc.) |
| `maxEvents` | int? | Max events to return |
| `commands` | List\<DaemonRequest\>? | Batch sub-commands |
| `type` | string? | ControlType filter |
| `tab` | string? | Tab name |
| `panel` | string? | Ribbon panel name |
| `waitAfter` | int? | Wait after action (seconds) |
| `modifiers` | string? | Keyboard modifiers (ctrl/shift/alt) |
| `retry` | bool? | Auto-retry on failure |
| `elementIds` | List\<string\>? | Element IDs for selection |
| `propertyName` | string? | Property name to read |
| `locale` | string? | Locale override |
| `strategy` | string? | Search strategy (auto/uia/vision/win32) |
| `onError` | string? | Error handling: stop/skip |
| `if` | string? | Conditional: previous_success/previous_failed |
| `onlyIf` | OnlyIfCondition? | `{dialog?, exists?, element?, enabled?}` |

Response: `DaemonResponse` with `Success`, `Error`, `ErrorInfo`, `Data`, `Command`, `StateDiff`, `Screenshot`, `DurationMs`.

## Plugin System
- `IPlugin` interface with `RegisterCommands(CommandRegistry)`
- DLLs expected in `Host/Plugins/` loaded at startup (directory may not exist yet — code handles gracefully)
- RevitPlugin registers all Revit-specific commands

## MCP Server (33 tools)
The MCP server uses `DaemonBridge` (wraps `DaemonClient`) to communicate with the Daemon via named pipe. It exposes 33 `[McpServerTool]` methods:
- `revit_connect`, `revit_click`, `revit_find`, `revit_ribbon`, `revit_ps`, `revit_type`, `revit_switch_view`
- `revit_wait_for`, `revit_wait_close`, `revit_wait_element`, `revit_task_dialog`, `revit_batch`
- `revit_list_windows`, `revit_list_controls`, `revit_state`, `revit_safe_click`
- `revit_select`, `revit_get_property`, `revit_key_combo`, `revit_screenshot`
- `revit_list_tabs`, `revit_status_bar`
- `revit_undo`, `revit_checkpoint`, `revit_undo_last`, `revit_undo_to`
- `revit_events`, `revit_ping`
- `revit_session_begin`, `revit_session_end`, `revit_session_status`, `revit_session_set`, `revit_session_get`

Requires: `--pipe <name>` flag (default: `RevitUiController`), daemon running with `--daemon`.

## LLM Vision Providers (auto-selection by priority)
1. **RouterAI** — `ROUTERAI_API_KEY`, model `qwen/qwen-vl-max`
2. **OpenAI** — `OPENAI_API_KEY`, model `gpt-4o`
3. **Anthropic** — `ANTHROPIC_API_KEY`, model `claude-sonnet-4-20250514`
4. **Ollama** — local, model `llama3.2-vision`

## Key Patterns
- **Command Pattern**: 70+ `ICommand` implementations (Core 55 + Revit 15), auto-discovered via assembly scan (+ 75 legacy commands in root project)
- **Abstract Base**: `UiCommandBase` handles boilerplate (state capture, diff, error formatting)
- **Strategy Pattern**: `AiFindCommand` (6 strategies), `CvMatchClient`, `LlmVisionClient` (4 providers)
- **Retry/Resilience**: `RetryPolicy` with exponential backoff
- **Page Object Model**: `UiMap` maps logical names to version-specific UIA selectors
- **Fallback layers**: FlaUI → Win32 → WinAppDriver → OpenCV → LLM Vision
- **Composite Provider**: UIA3 + WinAppDriver failover
- **DI + Plugins**: Microsoft.Extensions.DependencyInjection + assembly-scan plugin loading
- **Legacy (root project)**: Flat static architecture, no DI — separate command set (75 files)
