# RevitUiController: Architecture & Patterns

## Solution Structure (5 projects)

```
RevitUiController/
├── RevitUiController.Core/         # Core library — no Revit dependency
│   ├── Commands/                   # Generic commands (55 commands)
│   ├── Models/                     # CommandResult, ElementInfo, etc.
│   ├── Services/                   # DI service interfaces & implementations (22 services)
│   ├── Protocol/                   # DaemonProtocol (DaemonRequest/Response, DaemonClient)
│   ├── ICommand.cs                 # Command interface
│   ├── UiCommandBase.cs            # Abstract base with auto state-capture
│   ├── CommandRegistry.cs          # Centralized command registry (type + instance)
│   ├── IApplicationProfile.cs      # App profile interface
│   ├── IApplicationLauncher.cs     # App launcher interface
│   ├── IAutomationProvider.cs      # UIA provider abstraction
│   ├── IPlugin.cs                  # Plugin interface
│   ├── AutomationHelper.cs         # FlaUI search/interaction
│   ├── WindowSession.cs            # FlaUI UIA3 wrapper
│   ├── DesktopWindowManager.cs     # Window finding/switching
│   ├── ConfigLoader.cs / ConfigModel.cs  # config.yaml support
│   ├── CoreSettings.cs             # Global settings singleton
│   └── UiMap.cs / LocaleMap.cs     # Page Object Model
│
├── RevitUiController.Revit/        # Revit-specific extensions
│   ├── Commands/                   # Revit-specific commands (15)
│   ├── RevitProfile.cs             # IApplicationProfile for Revit
│   ├── RevitLauncher.cs            # IApplicationLauncher for Revit
│   ├── RevitPlugin.cs              # Plugin registration
│   ├── PipeBridgeClient.cs         # Named pipe to Revit API bridge
│   └── RevitInstanceManager.cs     # Multi-instance management
│
├── RevitUiController.Host/         # CLI host (entry point)
│   ├── Program.cs                  # DI setup, flag parsing, command dispatch
│   ├── config.yaml                 # Profiles & defaults
│   └── Plugins/                    # Plugin DLLs loaded at runtime
│
├── RevitUiController.Daemon/       # Background daemon server
│   ├── Program.cs                  # Named pipe server + client CLI
│   ├── DaemonServer.cs             # Persistent command execution server
│   └── EventWatcherService.cs      # Dialog open/close event monitoring
│
└── RevitUiController.McpServer/    # MCP stdio server
    └── Program.cs                  # Model Context Protocol tools
```

## Execution Flow

```
Runtime flow:
  Host CLI → DI container → Profile → Provider → WindowSession → Command

Daemon flow:
  Daemon (named pipe) → Host CLI / MCP Server → DaemonServer → Command

MCP flow:
  MCP Client (stdio) → McpServer → DaemonClient → Daemon DaemonServer → Command
```

## DI Container (Microsoft.Extensions.DependencyInjection)

Registered in `RevitUiController.Host/Program.cs`:

### Providers (IAutomationProvider)
| Provider | Flag | Description |
|----------|------|-------------|
| `UIA3AutomationProvider` | `--provider uia3` (default) | FlaUI UIA3 |
| `WinAppDriverProvider` | `--provider wad` | WinAppDriver REST API |
| `CompositeAutomationProvider` | `--provider composite` | UIA3 + WAD fallback |

### Service Interfaces (in `Services/`)
| Interface | Implementation | Purpose |
|-----------|---------------|---------|
| `IAutomationService` | `AutomationService` | Session lifecycle |
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
    pipeName: ReVibe
    executablePaths: [...]
    knownYears: [2022..2027]
  notepad:
    processName: notepad

defaults:
  profile: revit
  connectTimeout: 30
```

- `IApplicationProfile` — process name, paths, pipe, versions, LLM prompt
- `RevitProfile` — hardcoded Revit defaults
- `GenericProfile` — any process by name
- Custom profiles via `config.yaml`

## Key Interfaces

### IApplicationProfile
`Name`, `ProcessName`, `ExecutablePaths`, `PipeName`, `ConfigDirectory`, `KnownVersions`, `DetectVersionFromTitle()`, `BuildLlmSystemPrompt()`

### IApplicationLauncher
`Launch()`, `FindRunning()`, `WaitForReady()`, `IsAlive()`

### IAutomationProvider
`GetDesktop()`, `FindFirst()`, `FindFirstEnabledVisible()`, `FindAllChildren()`, `FindActiveDialogs()`

### IPlugin
`Name`, `RegisterCommands(CommandRegistry)`

### CommandRegistry
`Register(ICommand)`, `Register<T>()`, `RegisterType(Type)`, `RegisterAlias()`, `GetCommand()`, `GetCommandType()`

## Daemon Protocol (named pipe `\\.\pipe\RevitUiController`)

Line-delimited JSON:
```json
{"command":"__connect","processName":"Revit"}
{"command":"__ping"}
{"command":"__shutdown"}
{"command":"__batch","commands":[...]}
{"command":"click","args":["OK"]}
```

Response: `{"success":true,"data":{...},"error":null}`

## Plugin System
- `IPlugin` interface with `RegisterCommands(CommandRegistry)`
- DLLs in `Host/Plugins/` loaded at startup
- RevitPlugin registers all Revit-specific commands

## LLM Vision Providers (auto-selection by priority)
1. **RouterAI** — `ROUTERAI_API_KEY`, model `qwen/qwen-vl-max`
2. **OpenAI** — `OPENAI_API_KEY`, model `gpt-4o`
3. **Anthropic** — `ANTHROPIC_API_KEY`, model `claude-sonnet-4-20250514`
4. **Ollama** — local, model `llama3.2-vision`

## Key Patterns
- **Command Pattern**: 70+ `ICommand` implementations, auto-discovered via assembly scan
- **Abstract Base**: `UiCommandBase` handles boilerplate (state capture, diff, error formatting)
- **Strategy Pattern**: `AiFindCommand` (6 strategies), `CvMatchClient`, `LlmVisionClient` (4 providers)
- **Retry/Resilience**: `RetryPolicy` with exponential backoff
- **Page Object Model**: `UiMap` maps logical names to version-specific UIA selectors
- **Fallback layers**: FlaUI → Win32 → WinAppDriver → OpenCV → LLM Vision
- **Composite Provider**: UIA3 + WinAppDriver failover
- **DI + Plugins**: Microsoft.Extensions.DependencyInjection + assembly-scan plugin loading
