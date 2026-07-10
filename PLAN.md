# RevitUiController — План разработки (агентская версия)

> **Цель:** инструмент, которым управляет AI-агент (Claude), а не человек. Агент должен сам находить вкладки/кнопки,
> кликать, вводить значения, проверять результат, читать логи плагина — и по ответам инструмента понимать,
> что произошло в UI, без необходимости человека смотреть на экран или парсить произвольный текст.
>
> Это меняет приоритеты по сравнению с "классическим" UI-тест-раннером: главное — не набор возможностей,
> а **машиночитаемый, диагностируемый и предсказуемый интерфейс** вокруг них.

---

## 0. Agent Interface Layer (новый раздел, наивысший приоритет)

Всё остальное в плане бесполезно для агента, если ответы команд нельзя надёжно распарсить и понять,
что реально произошло. Это должно быть реализовано **до** или **вместе с** пунктами 2.x.

### 0.1. JSON как основной формат вывода

| Задача | Описание |
|---|---|
| `--json` (JSON по умолчанию, `--pretty` для человека) | ✅ `OutputFormatter.FormatResult` + `Program.IsPretty` — все команды возвращают `CommandResult` JSON |
| Единая схема элемента | ✅ `ElementInfo` модель: `{controlType, name, automationId, enabled, visible, boundingRect, children}` |
| Единая схема результата действия | ✅ `CommandResult` модель: `{success, error, errorInfo?, diff?, screenshot?, data}` |

### 0.2. State diff после каждого действия ✅

Вместо "OK/Fail" — снапшот **до и после**: какое окно активно, какие диалоги появились/закрылись,
какие контролы стали enabled/disabled. Агенту не нужно гадать и делать лишний `dump`.

Реализовано: `OutputFormatter.CaptureState()` / `ComputeDiff()` — action-команды (click, ribbon, safe-click) снимают UiState до и после, включают `UiStateDiff` в `CommandResult`.

```json
{
  "action": "click",
  "target": "Wall",
  "success": true,
  "diff": {
    "newDialogs": ["Modify | Walls"],
    "closedDialogs": [],
    "activeTabChanged": false
  }
}
```

### 0.3. Команда `state` — дешёвый снапшот ✅

Реализовано: `Commands/StateCommand.cs` — возвращает `UiState` JSON (active window, open dialogs, ribbon tab).

### 0.4. Скриншот + JSON в одном ответе ✅

Реализовано: `ScreenshotHelper.CaptureBase64()` / `CaptureWindow()` — флаг `--screenshot` в любой команде, base64 PNG в `CommandResult.Screenshot`.

### 0.5. Самоописывающиеся ошибки с кандидатами ✅

Реализовано: `SelfDescribingError` модель + `OutputFormatter.FormatError()` — `{code, query, suggestions}` в `CommandResult.ErrorInfo`.

```json
{"error": "NotFound", "query": "Wall", "suggestions": ["Стена", "Wall by Face", "Wall: Sweep"]}
```

### 0.6. Verbosity-контроль ✅

Реализовано: `--verbosity minimal|normal|full` — `Program.Verbosity` статик, `minimal` (только success/diff), `normal` (+ ключевые поля), `full` (весь дамп).

### 0.7. Идемпотентность / safe retry ✅

Реализовано: `Commands/SafeClickCommand.cs` — `safe-click` проверяет существование элемента перед кликом; если его уже нет — возвращает success (skipped).

---

## 1. ✅ Что уже реализовано

| Возможность | Команда | Подробнее |
|---|---|---|
| Подключение к Revit через FlaUI UIA3 | — | `RevitSession.Connect()` — находит процесс Revit по main window handle |
| Список окон/диалогов | `lw` / `list-windows` | Заголовки всех child-окон Revit |
| Список контролов в окне (дерево до 5 ур.) | `lc` / `list-controls [filter]` | `[ControlType] "Name" enabled=... id="..."` |
| Полный дамп UIA-дерева | `dump [depth] [-f file] [-t type] [-id id]` | Фильтрация по ControlType / AutomationId |
| Инспекция элемента (Spy++-стиль) | `inspect [index-path]` | BoundingRect, IsEnabled, Children |
| Поиск контрола по имени | `find <name>` | Bounds, enabled, visible, id |
| Нажать кнопку/контрол по имени | `click <name>` | Поиск в mMainTabs + дерево |
| Нажать кнопку на ленте (таб → кнопка) | `ribbon <btn> [tab]` | С переключением активного таба |
| Список табов ленты | `rt` / `ribbon-tabs [tab]` | Кнопки для каждого таба |
| Deep-скан ленты (табы → панели → кнопки) | `rb [tab]` | Переключает табы, читает mMainTabPanels |
| Переключение вкладки вида | `sv` / `switch-view [name]` | Tab-контрол → TabItem → Click |
| Ввод текста в поле | `type <control> <text>` | ValuePattern / SendKeys fallback |
| Развернуть/свернуть детали диалога | `expand` | Ищет "Подробности"/"Details" |
| Активные диалоги | `AutomationHelper.FindActiveDialogs()` | visible + Window-контролы |
| Поиск по AutomationId | `AutomationHelper.FindControlsByName()` | Contains-проверка по Name + AutomationId |
| Скрипты | `script <file>` | Построчное выполнение с `#`-комментариями |
| Пауза | `wait <sec>` | Thread.Sleep |

---

## 2. 🚧 Что планируем реализовать

### 2.1. ✅ Matchers + ожидания (стабильность)

| Задача | Описание | Статус |
|--------|----------|--------|
| `Retry.cs` — `WaitForElement(condition, timeout, interval)` | Полинг с таймаутом вместо Sleep | ✅ `Retry.cs` |
| `WaitForDialog(title, timeout)` | Дождаться появления модального окна | ✅ `Retry.WaitForDialog` |
| `WaitForDialogClose(title, timeout)` | Дождаться закрытия диалога | ✅ `Retry.WaitForDialogClose` |
| `ByAutomationId.cs` | Чистый поиск по AutomationId (быстрее) | ✅ Встроен в `AutomationHelper.FindControlsByName` |
| `ByIndex.cs` | Поиск по индексу из дампа | ❌ |
| `ByCondition.cs` | Комбинация: AND/OR условий | ❌ |

### 2.2. ✅ WPF-диалоги (PropertySheet)

| Задача | Описание | Статус |
|---|---|---|
| `PropertySheet.cs` | Модель окна "ИмяКоманды — ProjectName" | ✅ `Commands/DialogCommands.cs` (команда `ps`) |
| Читать вкладки диалога | TabItem-ы внутри PropertySheet | ✅ `ps <title> tabs` |
| Найти поле по лейблу | Рядом стоящий текст → соседний Edit | ✅ `ps <title> type <label> <value>` |
| `type` в поле | Ввод значения | ✅ |
| CheckBox | Прочитать состояние, установить | ✅ `ps <title> check <label> [true/false]` |
| ComboBox | Развернуть список, выбрать элемент | ✅ `ps <title> select <label> <option>` |
| DataGrid | Прочитать строки, выбрать ячейку | ✅ `ps <title> fields` — читает колонки, строки, выделенную ячейку |
| Button (OK/Cancel/Apply) | Нажать по имени | ✅ `ps <title> click <button>` |

### 2.3. ✅ TaskDialog

| Возможность | Описание | Статус |
|---|---|---|
| Прочитать заголовок | Текст заголовка окна | ✅ `Commands/TaskDialogCommand.cs` |
| Прочитать основное сообщение | Static-текст | ✅ `taskdialog <title> read` |
| Прочитать футер / детали | В том числе — развернуть футер | ✅ |
| Нажать кнопку | Да/Нет/OK/Отмена/Закрыть | ✅ `taskdialog <title> click <button>` |
| Развернуть подробности | Эмуляция клика по "Показать подробности" | ✅ `taskdialog <title> expand` |

### 2.4. ✅ Умная работа с лентой

| Задача | Описание | Статус |
|---|---|---|
| Поиск таба по AutomationId | Быстрее, чем по Name | ✅ `ribbon-find <tab>` |
| Поиск панели | mRibbonPanelView → DataItem | ✅ `ribbon-find <tab> <panel>` |
| Поиск кнопки внутри панели | Кнопка на конкретной панели | ✅ `ribbon-find <tab> <panel> <btn>` |
| DropDownButton | SplitButton → открыть список → выбрать | ✅ `dropdown <btn> <item> [tab]` |
| Контекстные табы | Появляются при выборе элемента (Modify, Modify \| Walls, ...) | ✅ `context-tabs` |
| Ribbon Quick Access Toolbar | Кнопки на панели быстрого доступа | ✅ `qat [click <name>]` |

### 2.5. ✅ Assert-проверки

| Assert | Описание | Статус |
|---|---|---|
| `AssertDialog.IsOpen(title)` | Диалог открыт | ✅ `assert-dialog <title> exists` |
| `AssertDialog.TextContains(title, text)` | Текст внутри диалога | ✅ `assert-dialog <title> text <expected>` |
| `AssertDialog.ButtonExists(title, name)` | Кнопка есть | ✅ `assert-dialog <title> button <name>` |
| `AssertDialog.ButtonEnabled(title, name)` | Кнопка активна | ✅ `assert-dialog <title> enabled <name>` |
| `AssertDialog.FieldValue(title, label, value)` | Поле содержит значение | ✅ `assert-dialog <title> field <label> <value>` |
| `AssertRibbon.TabExists(tabName)` | Таб ленты существует | ✅ `assert-ribbon <tab>` |
| `AssertRibbon.ButtonExists(tabName, btnName)` | Кнопка на табе есть | ✅ `assert-ribbon <tab> button <name>` |
| `AssertView.IsActive(viewName)` | Вкладка вида активна | ✅ `assert-view <name>` |
| `AssertElement.Exists(category, family, type)` | Элемент определённого типа в проекте | ❌ |

### 2.6. xUnit-тесты

```csharp
[Collection("Revit")]
public class WallCreationTests
{
    [Fact]
    public void WallCommand_OpensPropertySheet()
    {
        RibbonAction.Click("Wall", "Architecture");
        var dialog = Wait.ForDialog("Modify | Walls");
        AssertDialog.IsOpen("Modify | Walls");
        dialog.Click("OK");
    }
}
```

- `RevitFixture.cs` — `IClassFixture<RevitFixture>`, подключение к Revit
- `EnsureProjectOpen()` — проверить/открыть `.rvt`
- `CleanupDialogs()` — после каждого теста закрыть все модальные окна
- Отчёты: `.trx` + Allure

### 2.7. ✅ Script-расширения (.rvs)

```rvs
# create-wall.rvs
ribbon Wall Architecture
wait-for "Modify | Walls"
type "Height" 3000
select "Level" "Level 2"
click OK
```

| Директива | Описание | Статус |
|---|---|---|
| `wait-for "Title" [timeout]` | Дождаться окна | ✅ `Retry.WaitForDialog` |
| `wait-close "Title" [timeout]` | Дождаться закрытия | ✅ `Retry.WaitForDialogClose` |
| `assert-text "Title" "expected"` | Проверить текст | ✅ Через `assert-dialog` (зарегистрированная команда) |
| `assert-button "Title" "Button"` | Проверить кнопку | ✅ Через `assert-dialog` |
| `select "Title" "Field" "Value"` | ComboBox → выбрать значение | ✅ Inline handler в ScriptCommand |
| `window "Title"` | Привязать последующие команды к окну | ✅ Inline handler (логирование) |

---

## 3. 💡 Перспектива

### 3.1. ✅ Mouse-управление (курсор)

Когда UIA не видит элемент (кастомные OpenGL-холсты, DirectX-вьюпорты) — клик по координатам.

| Задача | Описание | Статус |
|---|---|---|
| `Mouse.Click(x, y)` | Клик по абсолютным координатам экрана | ✅ `MouseControl.ClickAt()` + команда `mouse-click <x> <y>` |
| `Mouse.Click(element)` | BoundingRectangle → center → клик | ✅ `MouseControl.ClickElement()` |
| `Mouse.Drag(x1,y1, x2,y2)` | Drag-and-drop | ✅ `MouseControl.Drag()` + команда `mouse-drag` |
| `Mouse.Scroll(ticks)` | Scroll wheel | ✅ `MouseControl.Scroll()` + команда `mouse-scroll` |
| `Mouse.Type(text)` | SendKeys через активный элемент | ✅ `mouse-type <text>` |
| `Screen.Capture(x,y,w,h)` | Скриншот области для CV-анализа | ✅ `ScreenshotHelper.CaptureBase64()` |

**DPI-awareness:** ✅ `GetDpiForMonitor` + `ToPhysical()` — пересчёт координат с учётом per-monitor DPI. При масштабировании 125–150% координаты корректируются.

### 3.2. ✅ WinAppDriver / Win32 Fallback

| Задача | Описание | Статус |
|---|---|---|
| `Win32.Click(hWnd, x, y)` | SendMessage(BM_CLICK) / PostMessage | ✅ `Win32Helper.ClickButton()` + `win32-click` команда |
| `Win32.GetText(hWnd)` | GetWindowText для Win32-контролов | ✅ `Win32Helper.GetText()` |
| `Win32.EnumChildWindows` | Обход Win32-дерева там, где UIA не видит | ✅ `Win32Helper.EnumChildWindowsList()` + `win32-enum` команда |
| `Appium/WinAppDriver` | REST-драйвер для Windows | ❌ |

### 3.3. ✅ Canvas / OpenGL / GraphicsView

| Задача | Описание | Статус |
|---|---|---|
| `GraphicsView.Zoom(factor)` | Колесо мыши по центру вьюпорта | ✅ `canvas-zoom <factor>` — скролл по центру GraphicsView |
| `GraphicsView.Pan(dx, dy)` | Drag по вьюпорту | ✅ `canvas-drag <x1> <y1> <x2> <y2> [--relative]` |
| `GraphicsView.Select(x, y)` | Клик в точке для выбора элемента | ✅ `canvas-click <x> <y> [--relative]` |
| `GraphicsView.GetTransform()` | Прочитать zoom/pan (через Revit API?) | ❌ (требует Revit API bridge) |
| `CV.MatchTemplate(template)` | OpenCV поиск иконки на скриншоте → клик | ❌ |

### 3.4. ✅ Статус-бар / Progress-диалоги

| Задача | Описание | Статус |
|---|---|---|
| `ProgressBar.WaitForComplete(timeout)` | Ждать завершения долгой операции | ✅ `WaitProgressCommand` — поллинг `ControlType.ProgressBar` + RangeValue |
| `StatusBar.ReadText()` | Текст подсказок Revit в статус-баре | ✅ `statusbar` — поиск по AutomationId/ControlType |

### 3.5. ✅ Revit API bridge (гибрид)

| Задача | Описание | Статус |
|---|---|---|
| `RevitAPI.Execute(commandName)` | Вызвать RevitCommand через Revit API | ✅ `revit-api <cmd> [--payload <json>]` через `\\.\pipe\ReVibe` |
| `RevitAPI.GetElementIds(category)` | Список ID элементов | ✅ `revit-get elements` |
| `RevitAPI.SetParameter(elementId, param, value)` | Изменить параметр | ✅ Через `revit-api setParameter --payload '{...}'` |
| `RevitAPI.GetOpenViews()` | Список открытых видов | ✅ `revit-get views` |

### 3.6. Canvas через координатные клики + CV

| Задача | Описание |
|---|---|
| `RevitCanvas.ClickElement(familyName)` | Найти и кликнуть по элементу на чертеже (CV-based) |
| `RevitCanvas.DragElement(from, to)` | Переместить элемент |
| `RevitCanvas.GetSelectionBox()` | Прочитать выбранные элементы |

### 3.7. Параллельные сценарии / CI

| Задача | Описание | Статус |
|---|---|---|
| Запуск Revit из кода (`Process.Start`) | Автостарт Revit, открытие проекта | ✅ `SafetyGuard.StartRevit()` + `revit-restart` |
| `dotnet test` в CI | GitHub Actions / Jenkins | ✅ `.github/workflows/revit-uictrl.yml` + `.kilo/scripts/run-uictrl-tests.ps1` |
| Retry-логика для flaky-тестов | 3 попытки с экспоненциальным backoff | ❌ (есть `Retry.cs` с поллингом, но не в тестах) |
| Allure-отчёты | Шаги, скриншоты, вложения | ❌ |
| Запись видео прохождения тестов | Скринкаст для отладки | ❌ |

---

## 4. ✅ Локализация и версионность (критично для этого проекта)

| Задача | Описание | Статус |
|---|---|---|
| `LocaleMap.cs` | RU↔EN словарь стандартных имён Revit (табы, кнопки, диалоги) — 22 записи | ✅ |
| Логические ключи в `.rvs` | Скрипты пишутся на ключах (`Wall`), а не на locale-зависимом тексте | ✅ `LocaleMap.Normalize()` |
| `RevitVersionProfile.cs` | Маппинг AutomationId/структуры под конкретную версию Revit (2022–2027) | ✅ |
| Автоопределение версии | При `Connect()` — по версии подключённого процесса | ✅ `RevitVersionProfile.Detect()` |

---

## 5. ✅ Диагностика и отладка

| Задача | Описание | Статус |
|---|---|---|
| Структурированное логирование | Каждый шаг с таймстампом в `%LOCALAPPDATA%\ReVibe\UiController\logs\uictrl_*.log` | ✅ `LoggingService.cs` — Info/Warn/Error с `[timestamp] [level] [command]` |
| Автоскриншот при ошибке | Capture при падении assert/действия | ✅ В `Program.cs` — автоскриншот при exitCode != 0 |
| Highlight-оверлей | Подсветка найденного элемента перед кликом | ✅ `HighlightHelper.cs` + `highlight`/`highlight-clear` — полупрозрачный overlay |
| Dry-run режим | Прогон `.rvs`-скрипта с логом шагов без реальных кликов | ✅ `dry-run <script>` |
| Чтение логов плагина | `logs [--tail N] [--since ts] [--level Error]` — логи revitCopilot | ✅ `logs --plugin` — читает `%LOCALAPPDATA%\RuBIMtech\ReVibe\logs\` |

---

## 6. ✅ Safety-guardrails

| Задача | Описание | Статус |
|---|---|---|
| Whitelist / подтверждение для деструктивных действий | Delete/Purge/Overwrite — не должны срабатывать случайно от автосценария на реальном проекте | ✅ `SafetyGuard.IsDestructive()` + `ConfirmDestructiveAction()` — блокировка в скриптах для click/safe-click/ribbon с деструктивными паттернами |
| Обработка неожиданных диалогов | Если во время скрипта вылез посторонний warning-диалог — закрыть по паттерну или упасть с понятной ошибкой, а не зависнуть | ✅ `SafetyGuard.DismissWarningDialogs()` + `safety-check` команда |
| Recovery/restart | Детект зависшего/упавшего процесса Revit и рестарт сессии в CI | ✅ `SafetyGuard.StartRevit()` + `WaitForRevitReady()` + `revit-restart` команда |

---

## 7. Прочее

| Задача | Описание | Статус |
|---|---|---|
| Element cache + invalidation | UIA-дерево меняется динамически; кэш найденных элементов с TTL/инвалидацией по событию | ✅ `ElementCache.cs` — 5s TTL + auto-refresh через `FindFirstEnabledVisible` + `cached-find`/`cache-clear`/`cache-stats` команды |
| Recorder | Запись действий пользователя (клики/ввод) в `.rvs`-скрипт автоматически | ✅ `RecorderService.cs` + `record-start`/`record-stop`/`record-status` — запись CLI-команд и скриптов в .rvs |
| BDD-слой (Reqnroll/SpecFlow) | Gherkin-сценарии на русском | ❌ |

---

## 8. Стек технологий

| Технология | Назначение | Статус |
|---|---|---|
| **FlaUI.UIA3** | Основной фреймворк UI Automation | ✅ Есть |
| **FlaUI.Core.Input.Mouse** | Mouse-клики, drag, scroll | ✅ Встроен в FlaUI |
| **Win32 SendInput / PostMessage** | Fallback для OpenGL/DirectX | 💡 План |
| **WinAppDriver** | REST API-драйвер Windows (альтернатива) | ✅ `WinAppDriverClient.cs` + `wad-connect`/`wad-find`/`wad-click` |
| **Allure** | Отчёты с шагами и скриншотами | ✅ `AllureConfigCommands.cs` + `AllureConfig.cs` (генерация JSON в allure-results) |
| **Reqnroll** | BDD-слой для тестов | ✅ `Reqnroll/` — RevitContext, RibbonSteps, .feature файлы |

---

## 9. Иерархия поиска элементов

Приоритет (сначала самый надёжный → fallback):

```
1. FlaUI UIA3 (AutomationId)
2. FlaUI UIA3 (Name содержит, с учётом LocaleMap)
3. FlaUI UIA3 (ControlType + индекс/положение)
4. Mouse-клик по координатам (BoundingRectangle, с учётом DPI)
5. Win32 SendInput / PostMessage
6. WinAppDriver
7. OpenCV поиск иконки на скриншоте
```

---

## 10. Сборка / запуск

```powershell
# Сборка
dotnet build tools\RevitUiController

# CLI-режим (JSON по умолчанию)
dotnet run --project tools\RevitUiController -- ribbon "Wall" Architecture

# CLI-режим с человекочитаемым выводом
dotnet run --project tools\RevitUiController -- ribbon "Wall" Architecture --pretty

# xUnit-тесты
dotnet test tools\RevitUiController.Tests

# .rvs скрипт
dotnet run --project tools\RevitUiController -- script scenarios\create-wall.rvs
```

---

## 11. Критерии готовности

**Слой агентского интерфейса (приоритет #1):**
- [x] JSON-вывод для всех команд (единая схема элемента и результата)
- [x] State diff после действия (before/after)
- [x] Команда `state` — дешёвый снапшот
- [x] Скриншот по флагу в любой команде
- [x] Самоописывающиеся ошибки с кандидатами
- [x] Verbosity-контроль (minimal/normal/full)
- [x] Идемпотентность / safe retry

**Базовый функционал:**
- [x] Подключение к Revit, чтение UIA-дерева, базовые клики
- [x] Работа с лентой (табы, панели, кнопки)
- [x] Работа с видами (переключение)
- [x] Script-режим (построчное выполнение)
- [x] Matchers + ожидания (WaitForElement, WaitForDialog)
- [x] PropertySheet — чтение/заполнение полей, checkbox, combobox
- [x] TaskDialog — чтение текста, нажатие кнопок
- [x] Assert-проверки
- [x] xUnit-тесты + CleanupDialogs (15 тестов, RevitFixture)

**Локализация и версии:**
- [x] LocaleMap (RU/EN)
- [x] RevitVersionProfile + автоопределение версии

**Диагностика:**
- [x] Структурированное логирование (LoggingService, файл uictrl_*.log)
- [x] Автоскриншот при ошибке
- [x] Чтение логов плагина (`logs --plugin`)
- [x] Dry-run режим

**Safety:**
- [x] Whitelist для деструктивных действий
- [x] Обработка неожиданных диалогов
- [x] Recovery/restart Revit-процесса

**Расширенное:**
- [x] Mouse-клики по координатам (с DPI-awareness)
- [x] Win32 fallback для сложных контролов (SendMessage/EnumChildWindows)
- [x] xUnit-тесты + CleanupDialogs (15 тестов в RevitUiController.Tests)
- [x] Element cache + invalidation (5s TTL, auto-refresh)
- [x] DataGrid — чтение колонок и строк
- [x] AssertFieldValue — проверка значения поля
- [x] CI-запуск (GitHub Actions workflow + PowerShell скрипт)
- [x] Revit API bridge (гибридный режим) через Named Pipe
- [x] Recorder действий → `.rvs`
- [x] Highlight-оверлей (полупрозрачный overlay)
- [x] StatusBar / ProgressBar
- [x] WinAppDriver (REST API клиент, wad-connect/wad-find/wad-click)
- [x] Canvas / GraphicsView (click, drag, zoom, screenshot)
- [x] Allure-отчёты (генерация JSON результатов + CLI-команда)
- [x] Retry-логика для flaky-тестов (экспоненциальный backoff)
- [x] BDD-слой (Reqnroll: RevitContext, RibbonSteps, .feature)
- [ ] OpenCV MatchTemplate (CV-поиск на скриншоте)