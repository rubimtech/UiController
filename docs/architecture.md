# RevitUiController — Архитектура и функционал

## Обзор

**RevitUiController** — CLI-инструмент для Windows UI-автоматизации на C# (.NET 10). Первичная цель — Autodesk Revit (2022–2027), но работает с любым Desktop-приложением. Управляет Revit через UIA, Win32, OpenCV, LLM Vision и WebView2 CDP.

---

## Структура проекта

```
RevitUiController/
├── RevitUiController.csproj          # Главный CLI-проект (точка входа)
├── Program.cs                        # Парсинг флагов, регистрация ~190 команд
├── ICommand.cs                       # Интерфейс команды
├── UiCommandBase.cs                  # Базовый класс с diff-состояния
├── Commands/ (30+ файлов)           # Реализации команд
├── Models/ (7 файлов)               # Модели данных
├── RevitUiController.Core/          # Core-библиотека (DI-ready)
├── RevitUiController.Daemon/        # Named-pipe сервер (демон)
├── RevitUiController.Host/          # Host-приложение с DI и плагинами
├── RevitUiController.McpServer/     # MCP-сервер (Model Context Protocol)
└── RevitUiController.Revit/         # Revit-специфичная логика
```

---

## Компоненты

### 1. Ядро (Root проект)

| Файл | Назначение |
|------|------------|
| **Program.cs** | Точка входа, парсинг флагов (`--pretty`, `--screenshot`, `--pid`, `--daemon`, `--uia-only`, и др.), регистрация 190+ команд, автозапуск демона, справка |
| **WindowSession.cs** | Подключение к окну через FlaUI UIA3. Фабрики: `ConnectToProcess()`, `ConnectToActive()`, `ConnectByTitle()`, `Resolve()` |
| **DesktopWindowManager.cs** | Оркестратор окон: фокус, перечисление, мониторы |
| **ActiveWindowTracker.cs** | Отслеживание активного окна через WinEventHook |
| **AutomationHelper.cs** | (456 строк) Поиск элементов: BFS по имени/AutomationId, UiMap, fallback-цепочки, клики, ввод текста, поля, чекбоксы, комбобоксы |
| **OutputFormatter.cs** | JSON-форматирование: `CommandResult`, `UiStateDiff`, `CaptureState()`, `ComputeDiff()` |
| **LoggingService.cs** | Файловый лог (структурированный) в `%LOCALAPPDATA%/ReVibe/UiController/logs/` |
| **SafetyGuard.cs** | Защита от деструктивных действий: подтверждение, авто-отклонение в non-interactive, дайминг диалогов |
| **SessionContext.cs** | Контекст сессии: активный диалог, таб, переменные, стек диалогов |
| **UiMap.cs** | Page Object Model на YAML. Версионирование (Revit 2022–2027), fallback-цепочки, CRUD |
| **LocaleMap.cs** | Русско-английский словарь UI-надписей (YAML + hardcoded fallback) |
| **ElementCache.cs** | Кэш элементов (TTL 5с) с авто-обновлением |
| **EventService.cs** | Event-driven автоматизация (UIA events). `WaitForDialogAsync()`, `WaitForElementAsync()`, `WaitForProgressAsync()` |
| **RecorderService.cs** | Запись CLI-команд в `.rvs` скрипты |
| **Retry.cs** | Полинг: `WaitForElement()`, `WaitForDialog()`, `RetryPolicy` с backoff |
| **ScreenshotHelper.cs** | Скриншоты через GDI BitBlt / WinAppDriver / WebView2 |
| **HighlightHelper.cs** | Подсветка элементов через полупрозрачные оверлеи |
| **MouseControl.cs** | Мышь: DPI-aware клики, драг, скролл через `mouse_event`, InvokePattern, PostMessage, FlaUI, WinAppDriver |
| **Win32Helper.cs** | Win32 fallback: `BM_CLICK`, `WM_SETTEXT`, `EnumChildWindows` |
| **WinAppDriverClient.cs** | REST-клиент WinAppDriver (для RDP/headless) |
| **WebView2Client.cs** | CDP-автоматизация WebView2 через Microsoft.Playwright: клики, ввод, JS-exec, скриншоты |
| **PipeBridgeClient.cs** | Named-pipe клиент к ReVibe API Bridge |
| **RevitInstanceManager.cs** | Управление многоми Revit: листинг, подключение, запуск, execute-on-all |
| **RevitVersionProfile.cs** | Детекция версии Revit (file version -> registry -> title -> default) |
| **CvMatchClient.cs** | OpenCV template matching: `MatchTemplate()`, `MatchAny()`, поиск шаблонов |
| **LlmVisionClient.cs** | LLM Vision API (RouterAI, OpenAI, Anthropic, Ollama): поиск элементов по описанию |
| **NativeMethods.cs** | P/Invoke: `EnumWindows`, `SetWinEventHook`, `GetDpiForMonitor`, и др. |

### 2. RevitUiController.Core (Core Library)

DI-ready библиотека с интерфейсами и сервисами.

**Интерфейсы:** `ICommand`, `IAutomationProvider`, `IApplicationProfile`, `IApplicationLauncher`, `IPlugin`, `ILoggingService`, `IAutomationService`, `IScreenshotService`, `IOutputFormatterService`, `IUiMapService`, `ISafetyGuardService`, `IEventService`, `IRecorderService`, `ISessionContextService`, `ICvMatchService`, `ILlmVisionService`.

**Провайдеры автоматизации:**
- `UIA3AutomationProvider` — FlaUI UIA3
- `WinAppDriverProvider` — WinAppDriver REST
- `CompositeAutomationProvider` — UIA3 + fallback на WinAppDriver

**Конфигурация:** Загрузка `config.yaml`, `CoreSettings`, профили.

**Реестр команд:** `CommandRegistry` — регистрация с алиасами, сканирование сборок.

**Протокол:** `DaemonProtocol` — `DaemonRequest`/`DaemonResponse` для named-pipe.

### 3. RevitUiController.Daemon

Named-pipe демон для постоянной автоматизации.

| Файл | Назначение |
|------|------------|
| **DaemonServer.cs** | (442 строки) Сервер на `\\.\pipe\RevitUiController`. Внутренние команды: `__ping`, `__shutdown`, `__connect`, `__watch`, `__events`, `__batch`, `__undo`. `HandleWatch()` — полинг до условия, `HandleUndo()` — откат через Revit API |
| **EventWatcherService.cs** | (165 строк) Мониторинг диалогов (300ms полинг), генерация `UiEvent`, отслеживание open/close переходов |

### 4. RevitUiController.Host

Host-приложение с DI-контейнером (Microsoft.Extensions.DependencyInjection) и загрузкой плагинов.

- `ConfigureServices()` — DI-регистрация сервисов
- `CreateAutomationProvider()` — выбор провайдера
- `LoadPlugins()` — загрузка `IPlugin` из `Plugins/*.dll`
- Поддержка `config.yaml` с профилями

### 5. RevitUiController.McpServer

MCP-сервер для интеграции с AI-ассистентами (Claude, Kilo и др.).

**Инструменты (21):** `revit_connect`, `revit_click`, `revit_find`, `revit_ribbon`, `revit_ps`, `revit_type`, `revit_switch_view`, `revit_wait_for`, `revit_task_dialog`, `revit_batch`, `revit_list_windows`, `revit_list_controls`, `revit_state`, `revit_safe_click`, `revit_key_combo`, `revit_screenshot`, `revit_list_tabs`, `revit_status_bar`, `revit_undo`, `revit_events`, `revit_ping`.

Транспорт: stdio (стандартный MCP). Требует запущенного Daemon.

### 6. RevitUiController.Revit

Revit-специфичная логика.

| Файл | Назначение |
|------|------------|
| **RevitProfile.cs** | Профиль Revit: process name, пути, pipe, версии |
| **RevitLauncher.cs** | Запуск Revit |
| **RevitPlugin.cs** | Плагин для Host |
| **PipeBridgeClient.cs** | Pipe-клиент к Revit API (`QueryUndoStack`, `PerformUndo`) |
| **RevitSafetyExtensions.cs** | `IsRevitProcessAlive`, `StartRevit`, `WaitForRevitReady` |

**Revit-команды:**
- `revit-api`, `revit-select`, `revit-get` — Revit API через pipe
- `revit-instances`, `revit-launch`, `multi-exec`, `session-switch` — управление инстансами
- `ribbon`, `ribbon-find`, `ribbon-tabs`, `ribbon-buttons`, `qat`, `context-tabs`, `ribbon-panel` — работа с лентой
- `switch-view` — переключение вкладок видов
- `state` — состояние UI
- `task-dialog` — чтение/клик TaskDialog
- `status-bar`, `wait-progress` — статусбар и прогресс
- `safety-check`, `revit-restart` — безопасность
- `process-list`, `process-info` — процессы
- `cv-capture` — захват шаблонов
- `canvas-click`, `canvas-drag`, `canvas-zoom`, `canvas-screenshot` — работа с видовым экраном
- `assert-dialog`, `assert-ribbon`, `assert-view` — ассерты

---

## Слои автоматизации (сверху вниз)

1. **FlaUI UIA3** (основной) — быстрый, надежный, поиск по AutomationId/Name
2. **UiMap (Page Object Model)** — YAML-маппинг логических имен с версионированием
3. **ai-find (6 стратегий)** — мульти-стратегия: name -> locale -> autoId -> regex -> sibling -> tab-scoped
4. **LLM Vision** — скриншот + LLM для поиска по описанию
5. **OpenCV** — template matching для пиксель-перфект детекции иконок
6. **Win32 SendMessage/PostMessage** — низкоуровневый fallback
7. **WinAppDriver REST API** — fallback для RDP/headless
8. **WebView2 CDP (Playwright)** — для WebView2/Tauri/React-приложений

---

## Data Flow

```
CLI args -> Program.Main()
  -> ParseGlobalFlags()
  -> WindowSession.Connect()
  -> ExecuteAsync(window, args)
    -> CaptureState() (before)
    -> UIA interaction (AutomationHelper)
    -> CaptureState() (after)
    -> ComputeDiff()
    -> FormatResult(JSON) via OutputFormatter
  -> Console output
```

---

## Паттерны проектирования

- **Command**: каждая CLI-операция — `ICommand`
- **Strategy**: цепочка fallback-стратегий поиска элементов
- **Observer**: `ActiveWindowTracker` (WinEventHook), `AutomationEventService` (UIA events)
- **Singleton/Static**: Program, SessionContext, LocaleMap, UiMap, LoggingService
- **Proxy/Adapter**: WinAppDriverClient, PipeBridgeClient, WebView2Client
- **Plugin**: Host загружает `IPlugin` из DLL
- **DI**: Host использует Microsoft.Extensions.DependencyInjection

---

## 🗺️ План развития

Полный roadmap с приоритетами (P0–P3) и детальным описанием каждого улучшения — в [`future.md`](future.md).

Ключевые направления:
- **P0**: структурированные ответы, auto-screenshot, event-driven ожидание, умные ошибки, сессия с контекстом
- **P1**: rich MCP-инструменты, undo с чекпоинтами, fallback chain, batch с условиями, LLM Vision fallback
- **P2**: WebView2/Ribbon/Canvas автоматизация, high-level Revit-команды, dry-run, мониторинг прогресса
- **P3**: multi-instance orchestration, прогрессивное логирование, assert-команды
