# RevitUiController: Architecture & Patterns

## High-level Architecture

```
CLI (dotnet run) → Program.cs (flags → session → command dispatch)
  → DesktopWindowManager (window resolution)
    → WindowSession (FlaUI UIA3 wrapper)
      → ICommand.ExecuteAsync(window, args)
        → AutomationHelper (FlaUI search/interaction)
          → OutputFormatter (JSON response)
```

## Key Files

### Core Infrastructure
| File | Purpose |
|------|---------|
| `Program.cs` | CLI entry, flag parsing, command registration, session lifecycle |
| `ICommand.cs` | Interface: `Name`, `Description`, `Usage`, `ExecuteAsync()` |
| `UiCommandBase.cs` | Abstract base: auto state-capture, diff, error handling, `FindElement()`, `RequireArgs()`, `GetFlag()`, `HasFlag()` |
| `WindowSession.cs` | Connect to ANY window via FlaUI UIA3 (`ConnectToProcess`, `ConnectByTitle`, `ConnectToActive`, `Resolve`) |
| `DesktopWindowManager.cs` | Orchestrator: window finding, switching, monitors, ActiveWindowTracker |
| `SessionContext.cs` | Stateful session: dialog stack, variables, active tab |

### Search & Interaction
| File | Purpose |
|------|---------|
| `AutomationHelper.cs` | `FindFirstEnabledVisible`, `SafeGetChildren`, `TryClick`, `SendTextSafe`, `FindFieldByLabel` |
| `UiMap.cs` | YAML Page Object Model: load/save/resolve with version-specific selectors |
| `LocaleMap.cs` | RU↔EN translation (YAML + hardcoded fallback) |
| `ElementCache.cs` | 5s TTL element cache with auto-refresh |
| `AiFindCommand.cs` | 6-strategy intelligent element search |
| `Retry.cs` | `Retry.WaitFor*` polling + `RetryPolicy` with exponential backoff |

### Fallback Layers
| File | Purpose |
|------|---------|
| `Win32Helper.cs` | Win32 SendMessage/PostMessage fallback |
| `WinAppDriverClient.cs` | WinAppDriver REST API client |
| `CvMatchClient.cs` | OpenCV MatchTemplate (template image search) |
| `LlmVisionClient.cs` | Multi-provider LLM Vision (RouterAI → OpenAI → Anthropic → Ollama) |
| `MouseControl.cs` | DPI-aware mouse clicks, drag, scroll |

### Output & Logging
| File | Purpose |
|------|---------|
| `OutputFormatter.cs` | JSON formatting: `FormatResult`, `FormatError`, `CaptureState`, `ComputeDiff` |
| `LoggingService.cs` | Structured file logging to `%LOCALAPPDATA%/ReVibe/UiController/logs/` |
| `RecorderService.cs` | Record actions to `.rvs` script files |

### Safety & Diagnostics
| File | Purpose |
|------|---------|
| `SafetyGuard.cs` | Destructive action confirmation, warning dismissal |
| `ScreenshotHelper.cs` | Screenshot capture (GDI BitBlt + WinAppDriver fallback) |
| `HighlightHelper.cs` | Semi-transparent overlay for element highlighting |
| `EventService.cs` | UIA event-driven automation (<100ms response) |

### Revit Integration
| File | Purpose |
|------|---------|
| `PipeBridgeClient.cs` | Named Pipe client (`\\.\pipe\ReVibe`) for Revit API bridge |
| `RevitInstanceManager.cs` | Multi-instance Revit management |
| `RevitVersionProfile.cs` | Version detection (2022-2027) |

## Models (in `Models/`)
- `CommandResult.cs` — standard JSON response (`Success`, `Error`, `Diff`, `Data`, `Screenshot`, `DurationMs`)
- `ElementInfo.cs` — UI element data (`ControlType`, `Name`, `AutomationId`, `BoundingRect`, `Children`)
- `ProgramOptions.cs` — immutable options record
- `WindowInfo.cs` / `MonitorInfo.cs` / `WindowQuery.cs` — window/monitor metadata

## Element Search Hierarchy
```
1. FlaUI AutomationId match
2. FlaUI Name contains + LocaleMap RU↔EN
3. ai-find (6 strategies: name → locale → autoId → regex → sibling → tab-scoped)
4. UiMap resolve (logical name → version-specific selectors)
5. Mouse click by BoundingRect (DPI-aware coordinates)
6. Win32 SendInput / PostMessage
7. WinAppDriver REST API
8. OpenCV MatchTemplate
9. LLM Vision (RouterAI → OpenAI → Anthropic → Ollama)
```

## LLM Vision Providers (auto-selection by priority)
1. **RouterAI** — `ROUTERAI_API_KEY`, model `qwen/qwen-vl-max`
2. **OpenAI** — `OPENAI_API_KEY`, model `gpt-4o`
3. **Anthropic** — `ANTHROPIC_API_KEY`, model `claude-sonnet-4-20250514`
4. **Ollama** — local, model `llama3.2-vision`

## Key Patterns
- **Command Pattern**: 70+ `ICommand` implementations registered in `Program.cs` static constructor
- **Abstract Base**: `UiCommandBase` handles boilerplate (state capture, diff, error formatting)
- **Strategy Pattern**: `AiFindCommand` (6 strategies), `CvMatchClient` (multiple templates), `LlmVisionClient` (4 providers)
- **Retry/Resilience**: `RetryPolicy` with exponential backoff
- **Idempotence**: `SafeClick` doesn't fail if element is already gone
- **Page Object Model**: `UiMap` maps logical names to version-specific UIA selectors in YAML
