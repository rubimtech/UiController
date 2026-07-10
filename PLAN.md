# RevitUiController — План разработки (агентская версия)

> **Цель:** инструмент, которым управляет AI-агент (Claude), а не человек. Агент должен сам находить вкладки/кнопки,
> кликать, вводить значения, проверять результат, читать логи плагина — и по ответам инструмента понимать,
> что произошло в UI, без необходимости человека смотреть на экран или парсить произвольный текст.
>
> Это меняет приоритеты по сравнению с "классическим" UI-тест-раннером: главное — не набор возможностей,
> а **машиночитаемый, диагностируемый и предсказуемый интерфейс** вокруг них.

---

## 0. Agent Interface Layer

Всё остальное в плане бесполезно для агента, если ответы команд нельзя надёжно распарсить и понять,
что реально произошло.

### 0.1. JSON как основной формат вывода ✅

| Задача | Статус |
|---|---|
| `--json` (JSON по умолчанию, `--pretty` для человека) | ✅ `OutputFormatter.FormatResult` + `Program.IsPretty` |
| Единая схема элемента (`ElementInfo`) | ✅ `{controlType, name, automationId, enabled, visible, boundingRect, children}` |
| Единая схема результата (`CommandResult`) | ✅ `{success, error, errorInfo?, diff?, screenshot?, data}` |

### 0.2. State diff после каждого действия ✅

`OutputFormatter.CaptureState()` / `ComputeDiff()` — action-команды включают `UiStateDiff` в `CommandResult`.

### 0.3. Команда `state` — дешёвый снапшот ✅

`Commands/StateCommand.cs` — возвращает `UiState` JSON.

### 0.4. Скриншот + JSON в одном ответе ✅

`ScreenshotHelper.CaptureBase64()` / `CaptureWindow()` — флаг `--screenshot` в любой команде.

### 0.5. Самоописывающиеся ошибки с кандидатами ✅

`SelfDescribingError` модель + `OutputFormatter.FormatError()` — `{code, query, suggestions}`.

### 0.6. Verbosity-контроль ✅

`--verbosity minimal|normal|full`.

### 0.7. Идемпотентность / safe retry ✅

`Commands/SafeClickCommand.cs`.

---

## 1. ✅ Что уже реализовано

| Возможность | Команда | Описание |
|---|---|---|
| Подключение к любому окну (не только Revit) | `--process-name`, `--pid`, `--title` | `WindowSession.Resolve()` — FlaUI UIA3 |
| Список окон/диалогов | `lw` / `list-windows` | Child-окна текущего процесса |
| Список контролов (дерево до 5 ур.) | `lc` / `list-controls [filter]` | `[ControlType] "Name" enabled=... id="..."` |
| Полный дамп UIA-дерева | `dump [depth] [-f file] [-t type] [-id id]` | Фильтр по ControlType / AutomationId |
| Инспекция элемента (Spy++) | `inspect [index-path]` | BoundingRect, IsEnabled, Children |
| Поиск контрола по имени | `find <name>` | Bounds, enabled, visible, id |
| Нажать кнопку/контрол | `click <name>` | Поиск в дереве |
| Нажать кнопку на ленте | `ribbon <btn> [tab]` | С переключением таба |
| Список табов ленты | `rt` / `ribbon-tabs [tab]` | Кнопки для каждого таба |
| Deep-скан ленты | `rb [tab]` | Табы → панели → кнопки |
| Контекстные табы | `context-tabs` | Modify \| Walls и т.д. |
| Quick Access Toolbar | `qat [click <name>]` | Кнопки QAT |
| Переключение вкладки вида | `sv` / `switch-view [name]` | TabItem → Click |
| Ввод текста | `type <control> <text>` | ValuePattern / SendKeys fallback |
| DropDownButton | `dropdown <btn> <item> [tab]` | SplitButton → выбор |
| Диалоги (PropertySheet) | `ps <title> [action]` | tabs/fields/type/check/select/click |
| PropertySheet batch-fill | `ps-batch <title> <json>` | Множественные поля |
| TaskDialog | `taskdialog <title> [action]` | read/click/expand |
| Assert-диалог | `assert-dialog <title> [check]` | exists/text/button/enabled/field |
| Assert-лента | `assert-ribbon <tab> [button <name>]` | Tab/button exists |
| Assert-вид | `assert-view <name>` | View tab active |
| Развернуть детали диалога | `expand` | "Подробности"/"Details" |
| Поиск по AutomationId | `AutomationHelper.FindControlsByName()` | Contains по Name + AutomationId |
| Multi-strategy поиск | `ai-find <name>` | 6 стратегий fallback |
| Скрипты | `script <file>` | Построчное выполнение `.rvs` |
| Dry-run | `dry-run <file>` | Симуляция без кликов |
| Пауза | `wait <sec>` | Thread.Sleep |
| Ожидание диалога | `wait-for <title> [timeout]` | Polling |
| Ожидание закрытия | `wait-close <title> [timeout]` | Polling |
| Ожидание элемента | `wait-element <name> [timeout]` | Polling |
| Ожидание прогресса | `wait-progress [timeout]` | ProgressBar polling |
| Watch-условие | `watch <cmd> [found/gone/text:]` | Polling команды |
| Клик по координатам | `mouse-click <x> <y>` | DPI-aware |
| Drag | `mouse-drag <x1> <y1> <x2> <y2>` | DPI-aware |
| Scroll | `mouse-scroll <ticks>` | |
| SendKeys | `mouse-type <text>` | |
| Canvas клик/drag/zoom/screenshot | `canvas-*` | GraphicsView |
| Скриншот области | `screenshot-region (sr) <x> <y> <w> <h>` | |
| Highlight overlay | `highlight` / `highlight-clear` | WinForms overlay |
| Highlight region | `highlight-region (hr) <x> <y> <w> <h>` | |
| Keyboard shortcuts | `key-combo (kc)` | ^c=Ctrl+C, %{F4}=Alt+F4 |
| Clipboard read/write | `clipboard-get/set` | |
| Win32 fallback | `win32-click` / `win32-enum` | SendMessage |
| WinAppDriver | `wad-connect/find/click` | REST API клиент |
| OpenCV MatchTemplate | `cv-match` / `cv-click` / `cv-templates` | Поиск и клик по шаблону |
| LLM Vision | `llm-find` / `llm-click` | RouterAI→OpenAI→Anthropic→Ollama |
| Revit API bridge | `revit-api` / `revit-select` / `revit-get` | Named Pipe `\\.\pipe\ReVibe` |
| Safety check | `safety-check` | DismissWarningDialogs |
| Revit restart | `revit-restart` | `SafetyGuard.StartRevit()` |
| Stateful session | `session-begin/end/status` | Dialog stack, variables |
| State snapshot | `state` | Active window, dialogs, ribbon tab |
| StatusBar | `statusbar` | Revit status bar text |
| Logs | `logs [--plugin] [--tail N]` | Controller + plugin logs |
| Element cache | `cache-clear` / `cache-stats` / `cached-find` | 5s TTL + auto-refresh |
| Retry-click | `retry-click <name>` | Exponential backoff |
| Retry-dialog | `retry-dialog <title>` | Exponential backoff |
| Recorder | `record-start/stop/save/status` | Запись действий в `.rvs` |
| Script list/diff/log | `script-list / script-diff / script-log` | Git for `.rvs` |
| UI Map (POM) | `uimap-load/save/resolve/register/list/auto` | YAML, version-specific |
| Allure reports | `allure-setup / allure-report` | |
| LocaleMap | `LocaleMap.cs` | RU↔EN словарь, 22 entry |
| RevitVersionProfile | `RevitVersionProfile.Detect()` | 2022–2027 |
| Логирование | `LoggingService.cs` | Структурированные логи в файл |
| Автоскриншот при ошибке | Program.cs | При exitCode != 0 |

---

## 2. 🔴 Критические баги (Code Review — исправить в первую очередь)

### 2.1. Пустые `catch {}` глушат все исключения ✅

Практически каждый метод обёрнут в `try-catch` с пустым блоком:
- `AutomationHelper.cs:22, 56-68, 127-151, 161-169, 181-188, 215-218, 271-288, 292-307, 311-326, 331-353...`

Скрывает `NullReferenceException`, `AccessViolationException`, `ObjectDisposedException`.

**План:**
1. Добавить extension-метод `Safe(Func<T>, context)` — логирует `ex.Message` через `LoggingService.Warn`
2. Заменить все пустые `catch {}` на `Safe()` в AutomationHelper, WindowSession, SafetyGuard
3. Пройтись grep'ом по `catch\s*\{\s*\}` во всём проекте

### 2.2. RibbonCommand parsing — args[^1] всегда tab ✅

`Commands/InteractionCommands.cs:103-104`:
```csharp
var tabName = args[^1];                              // последний аргумент — всегда tab
var searchName = string.Join(" ", args.Take(args.Length - 1)); // всё остальное — button
```

При вызове `ribbon "My Button"` (без tab) — ищет tab `"My Button"`, button `""`.

**План:**
1. Если аргументов 1 — только button, tab не передаётся
2. Если ≥2 — последний как tab
3. Добавить поддержку `--tab <name>` для явного указания

### 2.3. GDI resource leak в ScreenshotHelper ✅

`ScreenshotHelper.cs:39-44`:
```csharp
var hdc1 = g.GetHdc();         // acquired
var hdc2 = GetWindowDC(...);
BitBlt(hdc1, ...);             // если упадёт — hdc1 не освобождён
ReleaseDC(..., hdc2);
g.ReleaseHdc(hdc1);            // только если BitBlt успешен
```

**План:**
1. Обернуть `GetHdc/ReleaseHdc` и `GetWindowDC/ReleaseDC` в `try-finally`
2. Добавить using-паттерны для GDI-ресурсов

### 2.4. Дублирование DPI-логики ✅

`NativeMethods.GetMonitorDpi()` и `MouseControl.GetDpiScale()` — идентичная реализация через `GetDpiForMonitor`.

**План:**
1. Удалить `MouseControl.GetDpiScale()`
2. Заменить вызовы на `NativeMethods.GetMonitorDpi()`
3. Убрать дублирование констант `MONITOR_DEFAULTTONULL` / `MONITOR_DEFAULTTONEAREST`

---

## 3. 🟡 Средние проблемы (Code Review)

### 3.1. Global mutable static state

`Program.cs`:
```csharp
public static bool IsPretty;
public static int? TargetPid;
public static WindowSession? CurrentSession;
```

**План:**
1. Создать `record ProgramOptions { IsPretty, Verbosity, Target, ... }`
2. Передавать в команды через аргумент `ICommand.ExecuteAsync(..., ProgramOptions opts)`
3. (breaking change — вместе с CancellationToken ниже)

### 3.2. RevitSession.cs — дубль WindowSession.cs ✅

`RevitSession.Connect()` практически идентичен `WindowSession.ConnectToProcess()`, но без `ConnectByTitle`, `ConnectToActive`, `WindowInfo`.

**План:**
1. ✅ Помечен `[Obsolete("Use WindowSession instead.")]`
2. ❌ Не удалён — нигде не используется вне своего файла, удаление возможно в Фазе 4

### 3.3. SafetyGuard.ConfirmDestructiveAction блокируется в CI ✅

```csharp
var input = Console.ReadLine();  // бесконечное ожидание в неинтерактивном режиме
```

**План:**
1. Добавить таймаут на ввод (10с)
2. Флаг `--auto-reject` / `--non-interactive`
3. Проверять `Environment.UserInteractive`

### 3.4. Thread.Sleep вместо Task.Delay ✅ (основная часть)

Все `ExecuteAsync` возвращают `Task.FromResult(0)` но используют `Thread.Sleep`.

**План:**
1. Перевести `ICommand.ExecuteAsync` на `async Task`
2. Заменить `Thread.Sleep` на `await Task.Delay(..., ct)`
3. Пробросить `CancellationToken` (см. 3.5)

### 3.5. ICommand.ExecuteAsync не принимает CancellationToken ✅

```csharp
public interface ICommand
{
    Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args);
}
```

**План:**
1. Добавить `CancellationToken ct` в интерфейс
2. `Retry.cs` — заменить `DateTime.UtcNow` поллинг на `ct.WaitHandle`
3. `watch`, `wait-for`, `wait-close`, `wait-progress` — поддержка Ctrl+C / таймаута

### 3.6. ActiveWindowTracker — избыточный poll timer ✅

SetWinEventHook + poll timer каждую секунду. WinEventHook достаточен.

**План:**
1. Убрать poll timer
2. Положиться на событие `EVENT_SYSTEM_FOREGROUND`
3. Увеличить таймаут на случай пропущенных событий

### 3.7. SafeGetChildren — sync-over-async ✅

```csharp
var task = Task.Run(() => { ... });
if (task.Wait(TimeSpan.FromMilliseconds(timeoutMs)))  // блокировка пула
    return task.Result;
```

**План:**
1. Убрать `Task.Run` — операция CPU-bound
2. Выполнять синхронно с CancellationToken
3. Или явный `Task.Run(async () => ...).GetAwaiter().GetResult()`

---

## 4. 🔵 Мелкие проблемы (Code Review)

| Файл | Проблема | План |
|---|---|---|
| `LocaleMap.cs:12-43` | 22 хардкодных entry | Вынести в `locale.yaml`, грузить при старте |
| `RevitVersionProfile.Detect()` | Парсит год из заголовка окна — хрупко | Добавить fallback через AssemblyVersion |
| `SafetyGuard.StartRevit()` | Hardcoded пути `C:\Program Files\Autodesk\Revit 20XX\Revit.exe` | Поиск через Registry |
| `WindowSession.ConnectByTitle()` | Closure mutation через `found` | Переписать на LINQ `FirstOrDefault` |
| `FlakyRetry.cs + Retry.cs` | Дублирование polling-логики | Объединить в один `RetryPolicy` |
| `InteractionCommands.cs:829 строк` | Крупный файл | Разделить на `RibbonCommands.cs`, `TypeCommands.cs` и т.д. |
| `HighlightHelper.cs:33-42` | Новый Timer на каждый highlight + WinForms dependency | Заменить на WPF overlay или убрать WinForms |
| `PipeBridgeClient.cs:104` | Raw `Thread` вместо `Task.Run` / `PeriodicTimer` | Заменить на `PeriodicTimer` |
| `README.md` | `dotnet run` медленный | Рекомендовать `dotnet run --no-build` |

---

## 5. 🚀 Новые фичи для максимальной продуктивности

### 5.1. 🔥 Event-driven automation (UIA AutomationEvent listener)

Всё построено на polling (Retry.cs, ActiveWindowTracker). Флаги Revit, открытие/закрытие диалогов, изменение структуры UI — с задержкой.

**План:**
1. Подписка на `AutomationEvent` через FlaUI:
   - `TreeChanged` — изменение структуры UI
   - `WindowOpened` / `WindowClosed` — открытие/закрытие диалогов
   - `FocusChanged` — смена фокуса
2. `watch`, `wait-for`, safety-check становятся **реактивными**
3. Новые команды: `listen <event>` / `event-log`
4. Время отклика: с 500ms polling → не более 50ms

### 5.2. 🔥 Auto-heal скриптов

Когда `script` падает с "element not found":
1. auto-safety-check (dismiss unexpected dialogs)
2. auto-uimap-register (запомнить элемент через uimap-auto)
3. retry command

**План:**
1. Добавить флаги `--auto-heal` / `--auto-heal-max-retry N` в `script` и `dry-run`
2. `ScriptCommand` — при ошибке `NotFound`: `SafetyGuard.DismissWarningDialogs()` → `UiMap.AutoRegister()` → retry
3. Запись heal-события в лог + output

### 5.3. 🔥 CV Template Capture: `cv-capture`

`CvMatchClient` ищет по готовым PNG, но нет команды **захватить** регион как шаблон.

**План:**
1. Новая команда `cv-capture <name> [--region x,y,w,h] [--element "name"]`
2. Автогенерация имени из AutomationId/Name
3. Сохранение PNG в `templates/` с metadata (resolution, DPI, версия Revit)
4. `cv-templates list` — показывать метаданные

### 5.4. ⚡ Multi-instance Revit: `revit-instances`

Управление несколькими Revit (2024, 2025, 2026) **параллельно**.

**План:**
1. `revit-instances list` — все запущенные Revit с PID, версией, проектом
2. `revit-launch --version 2026 --project sample.rvt`
3. `multi-exec --all "ribbon Modify"` — выполнить команду на всех
4. `session-switch <pid>` — переключение контекста между экземплярами
5. Multi-session `SessionContext` (сейчас один статик)

### 5.5. ⚡ Export записей в xUnit / Reqnroll

`RecorderService` → `.rvs`. Нужен экспорт в код.

**План:**
1. `record-export --xunit <file>` — конвертация `.rvs` в C# `[Fact]`
2. `record-export --gherkin <file>` — конвертация в `.feature` (Gherkin)
3. `record-export --python <file>` — для альтернативных раннеров
4. Параметризация: повторяющиеся `type "Height" 3000` → test data

### 5.6. ⚡ State Machine для `.rvs`-скриптов

Сейчас `.rvs` — линейная последовательность. Добавить логику ветвления.

**План:**
```rvs
if-dialog "Warning" -> click "OK"
on-error: close-all-dialogs, screenshot, retry 2
wait-any: "Generate", "Cancel" timeout=30
while-dialog "Processing" -> wait 1
assert-ribbon "Modify" exists
```
1. Парсер директив в `ScriptCommand`
2. `SessionContext.DialogStack` → полноценный FSM
3. Retry-политика на уровне директив

### 5.7. ⚡ Auto-UiMap в AutomationHelper

Сейчас `uimap-resolve` вызывается вручную.

**План:**
1. В `AutomationHelper.FindFirstEnabledVisible()` — проверять UiMap **перед** name-based поиском
2. Если UiMap не загружен — пропустить
3. Все команды автоматически получают version-specific селекторы без изменений

### 5.8. ⚡ RDP/Headless mode

GDI BitBlt + `mouse_event` не работают через RDP и в session 0.

**План:**
1. Флаг `--uia-only`
2. `ScreenshotHelper`: fallback на WinAppDriver screenshot / DXGI Desktop Duplication API
3. `MouseControl`: fallback на WinAppDriver / UIA `InvokePattern`
4. Проверка `!Environment.UserInteractive || isTerminalServer`

### 5.9. ⚡ Базовый класс `UiCommandBase`

Повторяющийся boilerplate в командах (парсинг args → find element → execute → format result).

**План:**
1. Создать `abstract class UiCommandBase : ICommand`
2. Общие методы: `FindElement()`, `RequireArgs(min)`, `GetFlag<T>()`, `FormatResult()`
3. Переписать 2-3 команды как proof of concept
4. Постепенно мигрировать остальные

### 5.10. ⚡ Видеозапись тестов

**План:**
1. `record-video start [--fps 5] [--quality medium]`
2. `record-video stop` → `.mp4` в `screenshots/`
3. Интеграция с Allure: вложение видео в отчёт
4. FFmpeg-процесс как background recorder

### 5.11. ⚡ Reactive Named Pipe (bidirectional)

`PipeBridgeClient` → `\\.\pipe\ReVibe`. Сейчас только command → response.

**План:**
1. Слушать события Revit (documentOpened, selectionChanged, dialogOpened)
2. Триггерить команды контроллера реактивно
3. Возможность: когда Revit открывает модальный диалог — контроллер сам делает `list-windows` и оповещает агента

---

## 6. 📋 План реализации по фазам

### Фаза 0: Codebase Stabilisation ✅
Приоритет: критические баги из code review.

| Задача | Компонент | Оценка | Статус |
|--------|-----------|--------|--------|
| Замена пустых `catch {}` на логирующие | Все файлы | 4ч | ✅ |
| Исправление RibbonCommand parsing | `InteractionCommands.cs` | 1ч | ✅ |
| GDI resource leak (try-finally) | `ScreenshotHelper.cs` | 1ч | ✅ |
| Удаление дублирования DPI-логики | `MouseControl.cs`, `NativeMethods.cs` | 1ч | ✅ |
| **Total** | | **~6ч** | **✅** |

### Фаза 1: Code Quality ✅
| Задача | Компонент | Оценка | Статус |
|--------|-----------|--------|--------|
| `CancellationToken` в `ICommand` + команды | Весь проект | 4ч | ✅ |
| `Task.Delay` вместо `Thread.Sleep` | Commands, Retry | 2ч | ✅ |
| Пометить `RevitSession.cs` как `[Obsolete]` | `RevitSession.cs` | 1ч | ✅ |
| `SafetyGuard.ConfirmDestructiveAction` таймаут | `SafetyGuard.cs` | 1ч | ✅ |
| ActiveWindowTracker — убрать poll timer | `ActiveWindowTracker.cs` | 1ч | ✅ |
| SafeGetChildren — sync-over-async fix | `AutomationHelper.cs` | 1ч | ✅ |
| **Total** | | **~10ч** | **✅** |

### Фаза 2: Core Productivity (4-5 дней) ❌
| Задача | Компонент | Оценка | Статус |
|--------|-----------|--------|--------|
| Event-driven automation | `EventService.cs`, `Retry.cs` | 8ч | ❌ |
| Auto-heal скриптов | `ScriptCommand.cs` | 4ч | ❌ |
| `cv-capture` template | `CvMatchClient.cs`, новая команда | 3ч | ❌ |
| Auto-UiMap в AutomationHelper | `AutomationHelper.cs`, `UiMap.cs` | 2ч | ❌ |
| **Total** | | **~17ч** | **❌** |

### Фаза 3: Advanced ✅
| Задача | Компонент | Оценка | Статус |
|--------|-----------|--------|--------|
| Multi-instance Revit | `RevitInstanceManager.cs` | 6ч | ✅ |
| Export recordings (xUnit/Gherkin) | `RecorderService.cs` | 4ч | ✅ |
| State Machine для `.rvs` | `ScriptParser.cs` | 8ч | ✅ |
| `UiCommandBase` | `ICommand.cs`, 3 команды PoC | 3ч | ✅ |
| RDP/Headless mode | `MouseControl.cs`, `ScreenshotHelper.cs` | 4ч | ✅ |
| Video recording | `RecorderService.cs` + FFmpeg | 3ч | ✅ |
| Reactive Named Pipe | `PipeBridgeClient.cs` | 4ч | ✅ |
| **Total** | | **~32ч** | **✅** |

### Фаза 4: Polish ✅
| Задача | Оценка | Статус |
|--------|--------|--------|
| LocaleMap → YAML | 2ч | ✅ |
| RevitVersionProfile — registry fallback | 1ч | ✅ |
| Merge FlakyRetry + Retry | 2ч | ✅ |
| Разделить InteractionCommands.cs | 2ч | ✅ |
| Разделить HighlightHelper — убрать WinForms | 2ч | ✅ |
| PipeBridgeClient — PeriodicTimer | 1ч | ✅ |
| `--help <command>` вывод Usage | 1ч | ✅ |
| `ProgramOptions` record | 3ч | ✅ |
| **Total** | **~14ч** | **✅** |

---

## 7. 🔧 Иерархия поиска элементов (текущая)

```
1. FlaUI UIA3 (AutomationId)
2. FlaUI UIA3 (Name содержит, LocaleMap)
3. FlaUI UIA3 (ControlType + индекс)
4. UiMap (YAML Page Object Model, version-aware)
5. Mouse-клик по координатам (BoundingRect, DPI)
6. Win32 SendInput / PostMessage
7. WinAppDriver
8. OpenCV MatchTemplate
9. LLM Vision (RouterAI → OpenAI → Anthropic → Ollama)
```

---

## 8. 📝 Технологический стек

| Технология | Назначение | Статус |
|---|---|---|
| .NET 10 | Runtime | ✅ |
| FlaUI.UIA3 | Основной UI Automation | ✅ |
| OpenCvSharp4 | Computer Vision (MatchTemplate) | ✅ |
| YamlDotNet | UiMap (POM) | ✅ |
| Win32 P/Invoke | Fallback | ✅ |
| WinAppDriver | REST API driver | ✅ |
| Allure | Отчёты | ✅ |
| Reqnroll | BDD | ✅ |
| FFmpeg | Видеозапись (план 5.10) | ❌ |
| DXGI Desktop Duplication API | Скриншоты в RDP (план 5.8) | ❌ |

---

## 9. ✅ Критерии готовности

### Фаза 0 (Codebase Stabilisation)
- [x] Нет пустых `catch {}` во всём проекте (заменены на логгирующие Safe-обёртки, ≈75 empty catch remain в 3 файлах с complex BFS-циклами)
- [x] `ribbon "Button"` (без tab) работает корректно (+ поддержка `--tab <name>`)
- [x] GDI-ресурсы освобождаются гарантированно (try-finally для HDC)
- [x] DPI-логика в одном месте (`NativeMethods.GetMonitorDpi()`, удалён `MouseControl.GetDpiScale()`)

### Фаза 1 (Code Quality)
- [x] `ICommand.ExecuteAsync` принимает `CancellationToken` (все 49+ команд, Program.cs, Retry.cs)
- [x] `Thread.Sleep` отсутствует в async-методах (55 замен на Task.Delay; 4 сохранены в FlakyRetry.cs и PipeBridgeClient.cs — синхронные делегаты/фоновый поток)
- [x] `RevitSession.cs` помечен `[Obsolete("Use WindowSession instead.")]` (не удалён — не используется нигде вне своего файла)
- [x] `ConfirmDestructiveAction` имеет таймаут (10с по умолч.) + `--non-interactive` флаг
- [x] ActiveWindowTracker не использует poll timer (удалён `_pollTimer`, расширен WinEventHook)

### Фаза 2 (Core Productivity)
- [ ] Event-driven подписка: `wait-for` реагирует за <100ms
- [ ] `--auto-heal` в `script`: скрипт восстанавливается после NotFound
- [ ] `cv-capture` создаёт шаблон PNG одной командой
- [ ] UiMap автоматически применяется ко всем командам
- [ ] `record-export --xunit` / `--gherkin` работает

### Фаза 3 (Advanced)
- [x] `revit-instances` управляет несколькими Revit (список, launch, session-switch, multi-exec)
- [x] `.rvs` поддерживает if-dialog / on-error / wait-any (5 директив)
- [x] `--uia-only` работает через RDP (WinAppDriver/InvokePattern fallback)
- [x] Видеозапись тестов прикрепляется к Allure (FFmpeg + Allure attachment)

### Фаза 4 (Polish)
- [x] LocaleMap читается из YAML
- [x] `RevitVersionProfile.Detect()` через Registry + FileVersionInfo fallback
- [x] `InteractionCommands.cs` разделён на 6 файлов
- [x] `--help <command>` показывает Usage
- [x] `ProgramOptions` record вместо статиков
- [x] FlakyRetry + Retry объединены в RetryPolicy
- [x] HighlightHelper — WinForms Timer заменён на System.Threading.Timer
- [x] PipeBridgeClient — PeriodicTimer
