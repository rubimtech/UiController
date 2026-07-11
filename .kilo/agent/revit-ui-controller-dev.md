# RevitUiController Developer Agent

## Project Overview
RevitUiController — CLI-инструмент для автоматизации UI Revit через FlaUI UIA3.
Позволяет программно управлять лентой, диалогами, PropertySheet, TaskDialog, видами, canvas, выполнять скрипты, запись, assertions, UiMap (Page Object Model), сессии, multi-instance.

## Стек
- **Framework**: .NET 10 (`net10.0-windows`)
- **Язык**: C# (nullable enabled, implicit usings)
- **Ключевые зависимости**: FlaUI.UIA3, OpenCvSharp4, YamlDotNet, WinForms
- **Платформа**: Windows x64 only
- **Тесты**: xUnit (`RevitUiController.Tests`)

## Архитектура
```
CLI (dotnet run) → Program.cs (flags → session → command dispatch)
  → DesktopWindowManager (window resolution)
    → WindowSession (FlaUI UIA3 wrapper)
      → ICommand.ExecuteAsync(window, args)
        → AutomationHelper (FlaUI search/interaction)
          → OutputFormatter (JSON response)
```

## Референсы (загружать по необходимости)
- `.kilo/agent/revit-ui-controller-architecture.md` — полная архитектура, ключевые файлы, search hierarchy, LLM Vision providers, patterns
- `.kilo/agent/revit-ui-controller-build-run.md` — сборка, запуск, отладка, глобальные флаги, output format
- `.kilo/agent/revit-ui-controller-commands.md` — полный справочник команд (80+ команд)
- `.kilo/agent/revit-ui-controller-extending.md` — как добавить новую команду, UiCommandBase helpers, AutomationHelper API, тесты, UiMap YAML

## Команды сборки и запуска
```powershell
# Build
dotnet build tools\RevitUiController -c Debug

# Run (из корня ReviBE)
dotnet run --project tools\RevitUiController -- state --pretty
dotnet run --project tools\RevitUiController -- ribbon "Wall" Architecture --pretty

# Tests
dotnet test tools\RevitUiController.Tests
```

## Основные файлы
| Файл | Назначение |
|------|-----------|
| `Program.cs` | CLI entry, flag parsing, command registration, session lifecycle |
| `ICommand.cs` | Interface: `Name`, `Description`, `Usage`, `ExecuteAsync()` |
| `UiCommandBase.cs` | Abstract base: auto state-capture, diff, error handling |
| `WindowSession.cs` | Connect to ANY window via FlaUI UIA3 |
| `DesktopWindowManager.cs` | Orchestrator: window finding, switching, monitors |
| `AutomationHelper.cs` | `FindFirstEnabledVisible`, `SafeGetChildren`, `TryClick`, `SendTextSafe` |
| `UiMap.cs` | YAML Page Object Model: load/save/resolve with version-specific selectors |
| `Commands/` | 70+ ICommand implementations |

## Ключевые паттерны
- **Command Pattern**: 70+ `ICommand` в `Program.cs` static constructor
- **Abstract Base**: `UiCommandBase` — boilerplate (state capture, diff, error formatting)
- **Strategy Pattern**: `AiFindCommand` (6 strategies), `CvMatchClient`, `LlmVisionClient` (4 providers)
- **Retry/Resilience**: `RetryPolicy` с exponential backoff
- **Page Object Model**: `UiMap` — логические имена → version-specific UIA selectors в YAML
- **Fallback layers**: FlaUI → Win32 → WinAppDriver → OpenCV → LLM Vision

## Правила разработки
1. Всегда использовать `Safe()` extension вместо пустых `catch {}`
2. Использовать `await Task.Delay(..., ct)` вместо `Thread.Sleep`
3. Возвращать `CommandResult` с `Success`, опционально `Error`, `Diff`, `Data`
4. Расширять `UiCommandBase`, а не raw `ICommand`
5. Проверять `Program.IsUiaOnly` если используешь mouse/GDI (RDP support)
6. Для поиска элементов — проходить по search hierarchy (FlaUI → UiMap → OpenCV → LLM Vision)
7. Новые команды регистрировать в `Program.cs` static constructor + добавлять алиас + help text
