# RevitUiController Developer Agent

## Project Overview
RevitUiController — CLI-инструмент для автоматизации UI Windows через FlaUI UIA3.
Поддерживает любые Win32 приложения, с расширенной поддержкой Revit.
Позволяет программно управлять лентой, диалогами, PropertySheet, TaskDialog, видами, canvas, WebView2, выполнять скрипты, запись, assertions, UiMap (Page Object Model), сессии, multi-instance.

## Стек
- **Framework**: .NET 10 (`net10.0-windows`)
- **Language**: C# (nullable enabled, implicit usings)
- **Решение**: 5 проектов (Core, Revit, Host, Daemon, McpServer)
- **DI**: Microsoft.Extensions.DependencyInjection
- **Key deps**: FlaUI.UIA3, OpenCvSharp4, YamlDotNet, WinForms, MCP SDK
- **Platform**: Windows x64 only
- **Tests**: xUnit (`RevitUiController.Tests`)

## Архитектура
```
Solution: RevitUiController (5 projects)
  Core       — generic UI automation, no Revit dependency
  Revit      — Revit-specific profiles, launcher, commands, pipe bridge
  Host       — CLI entry point with DI, config, plugin loading
  Daemon     — persistent named pipe server (background automation)
  McpServer  — Model Context Protocol stdio server (AI tool integration)

Runtime:
  Host → DI → Profile → Provider → WindowSession → Command
  Daemon ↔ Host / McpServer (named pipe)
```

## Референсы (загружать по необходимости)
- `.kilo/agent/revit-ui-controller-architecture.md` — полная архитектура, все проекты, DI, профили, провайдеры, протоколы
- `.kilo/agent/revit-ui-controller-build-run.md` — сборка, запуск (Host/Daemon/McpServer), флаги, конфиг, отладка
- `.kilo/agent/revit-ui-controller-commands.md` — полный справочник команд (80+ команд, WebView2, Daemon, batch)
- `.kilo/agent/revit-ui-controller-extending.md` — как добавить команду, плагин, профиль, провайдер, сервис

## Команды сборки и запуска
```powershell
# Build all
dotnet build tools\RevitUiController -c Debug

# Run Host (main CLI)
dotnet run --project tools\RevitUiController\RevitUiController.Host -- state --pretty
dotnet run --project tools\RevitUiController\RevitUiController.Host -- ribbon Wall Architecture --pretty

# Run Host with profile
dotnet run --project tools\RevitUiController\RevitUiController.Host -- --profile notepad list-controls

# Daemon
dotnet run --project tools\RevitUiController\RevitUiController.Daemon -- --daemon

# MCP Server (requires daemon running)
dotnet run --project tools\RevitUiController\RevitUiController.McpServer

# Tests
dotnet test tools\RevitUiController.Tests
```

## Основные файлы
| Файл | Назначение |
|------|-----------|
| `RevitUiController.Host/Program.cs` | CLI entry, DI setup, flag parsing, plugin loading |
| `RevitUiController.Core/ICommand.cs` | Interface: `Name`, `Description`, `Usage`, `ExecuteAsync()` |
| `RevitUiController.Core/UiCommandBase.cs` | Abstract base: auto state-capture, diff, error handling |
| `RevitUiController.Core/CommandRegistry.cs` | Centralized command registry (type + instance registration) |
| `RevitUiController.Core/WindowSession.cs` | Connect to ANY window via FlaUI UIA3 |
| `RevitUiController.Core/AutomationHelper.cs` | `FindFirstEnabledVisible`, `SafeGetChildren`, `TryClick` |
| `RevitUiController.Core/UiMap.cs` | YAML Page Object Model |
| `RevitUiController.Core/CoreSettings.cs` | Global settings |
| `RevitUiController.Core/ConfigLoader.cs` | config.yaml loader |
| `RevitUiController.Core/Commands/` | 55 generic commands |
| `RevitUiController.Revit/Commands/` | 15 Revit-specific commands |
| `RevitUiController.Revit/RevitProfile.cs` | IApplicationProfile for Revit |
| `RevitUiController.Revit/PipeBridgeClient.cs` | Named pipe client for Revit API |

## Ключевые паттерны
- **Command Pattern**: 70+ `ICommand`, auto-discovered via assembly scan
- **DI**: `Microsoft.Extensions.DependencyInjection`, 22 service interfaces
- **Abstract Base**: `UiCommandBase` — boilerplate (state capture, diff, formatting)
- **Strategy Pattern**: `AiFindCommand` (6 strategies), `CvMatchClient`, `LlmVisionClient` (4 providers)
- **Profile Pattern**: `IApplicationProfile` — RevitProfile / GenericProfile / custom config
- **Provider Pattern**: `IAutomationProvider` — UIA3 / WinAppDriver / Composite
- **Plugin Pattern**: `IPlugin` — DLL loading from `Host/Plugins/`
- **Daemon Protocol**: Named pipe, JSON line-delimited, bidirectional
- **Retry/Resilience**: `RetryPolicy` с exponential backoff
- **Page Object Model**: `UiMap` — logical names → version-specific selectors
- **Fallback layers**: FlaUI → Win32 → WinAppDriver → OpenCV → LLM Vision

## Правила разработки
1. Код добавлять в соответствующий проект (Core/Revit/Host/Daemon/McpServer)
2. Всегда использовать `Safe()` extension вместо пустых `catch {}`
3. Использовать `await Task.Delay(..., ct)` вместо `Thread.Sleep`
4. Возвращать `CommandResult` с `Success`, опционально `Error`, `Diff`, `Data`
5. Расширять `UiCommandBase`, а не raw `ICommand` (для auto state diff)
6. Проверять `CoreSettings.IsUiaOnly` если используешь mouse/GDI (RDP support)
7. Для поиска элементов — проходить по search hierarchy (FlaUI → UiMap → CV → LLM Vision)
8. Новые generic команды — в `Core/Commands/`, Revit-специфичные — в `Revit/Commands/`
9. Команды регистрируются через `CommandRegistry.Register<T>()` (assembly scan)
10. Для сервисов — создать interface в `Core/Services/`, реализацию там же, зарегистрировать в Host DI
