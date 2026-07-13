# RevitUiController — AI Agent Reference

## Overview

RevitUiController — CLI-инструмент для программного управления Windows-приложениями через UI Automation (FlaUI UIA3).
Поддерживает **любые окна** (Revit, Notepad, браузеры, Tauri/React). Позволяет AI-агентам и CI-сценариям
выполнять действия без необходимости смотреть на экран.

> **Архитектура**: 6 проектов. Новая DI-архитектура (Core/Revit/Host/Daemon/McpServer) + legacy root (`RevitUiController.csproj`, flat static, 75 команд). WebView2 команды (`wv-*`) существуют **только** в legacy проекте.

## Quick Start (для агентов)

```powershell
# Запуск команды (из директории RevitUiController) — НОВАЯ архитектура (DI-based)
dotnet run --project RevitUiController.Host -- <command> [args...] [--flags]

# Запуск команды — LEGACY (flat arch, включает wv-* команды)
dotnet run --project RevitUiController.csproj -- <command> [args...] [--flags]

# Пример: получить состояние UI
dotnet run --project RevitUiController.Host -- state --pretty

# Пример: нажать кнопку на ленте
dotnet run --project RevitUiController.Host -- ribbon "Wall" Architecture --pretty
```

---

## Response Format (JSON)

Каждая команда возвращает `CommandResult` в STDOUT:

```json
{
  "command": "ribbon",
  "success": true,
  "error": null,
  "errorInfo": null,
  "diff": {
    "activeDialog": "Modify | Walls",
    "newDialogs": ["Modify | Walls"],
    "closedDialogs": [],
    "activeTabChanged": true,
    "statusBarText": null
  },
  "data": { "button": "Wall", "tab": "Architecture" },
  "screenshot": null,
  "durationMs": 1234
}
```

**Поля:**

| Поле | Тип | Описание |
|------|-----|----------|
| `command` | string | Имя выполненной команды |
| `success` | bool | true если команда успешна |
| `error` | string? | Текст ошибки (null при успехе) |
| `errorInfo` | object? | Самоописывающаяся ошибка: `{code, query, suggestions, availableElements}` |
| `diff` | object? | State diff: активный диалог, новые/закрытые диалоги, смена таба |
| `data` | object? | Результат команды (зависит от команды) |
| `screenshot` | string? | base64 PNG скриншот (только с `--screenshot`) |
| `durationMs` | number | Время выполнения в миллисекундах |

**Exit codes:** 0 = success, 1 = error.

---

## Global Flags (применимы к любой команде)

| Флаг | Описание |
|------|----------|
| `--pretty` | Pretty-print JSON (человекочитаемый) |
| `--screenshot` | Включить base64 скриншот в ответ |
| `--verbosity minimal\|normal\|full` | Детальность ответа (minimal = success/diff, normal + data, full + dump) |
| `--pid <number>` | Подключиться к конкретному PID |
| `--process-name <name>` | Имя процесса (по умолч: `Revit`) |
| `--window-title <title>` | Подключиться к окну по заголовку (contains) |
| `--active` | Подключиться к активному (foreground) окну |
| `--connect-timeout <sec>` | Таймаут ожидания процесса (по умолч: 30) |
| `--non-interactive` | CI-режим: отклонять деструктивные действия без промпта |
| `--uia-only` | UIA-only без GDI/mouse_event (для RDP/headless). Создаёт WinAppDriver |
| `--daemon` | Запустить в режиме демона (persistent named pipe server) |
| `--wv-setup` | Установить registry-ключ для WebView2 CDP remote debugging |
| `--provider <name>` | uia3 (default) / wad / composite |
| `--profile <name>` | Профиль приложения из config.yaml |
| `--auto-screenshot` (daemon) | Авто-скриншот при ошибке в daemon |

**Примеры подключения:**

```powershell
# К активному окну
dotnet run --project RevitUiController.Host -- --active state --pretty

# К окну Notepad по заголовку
dotnet run --project RevitUiController.Host -- --window-title "Блокнот" state --pretty

# К процессу по имени
dotnet run --project RevitUiController.Host -- --process-name "notepad" list-controls --pretty
```

---

## Полный справочник команд

### 🖥️ Desktop Window Management (любые окна)

```powershell
list-all (la) [--filter <text>]
  Список ВСЕХ visible top-level окон на рабочем столе.
  --filter: фильтр по заголовку (contains, case-insensitive)

active
  Информация о текущем активном окне + монитор: PID, заголовок, позиция, размер, DPI.
  Полезно для проверки, на какое окно сейчас направлены команды.

focus <title> [--pid <N>|--hwnd <hex>]
  Переключиться на окно (bring to foreground).
  --pid: точный PID
  --hwnd: Hex HWND

monitors
  Список мониторов: разрешение, DPI, work area, primary.
  Полезно для координатных кликов (mouse-click, canvas-click).
```

### 🔍 UI Exploration (интроспекция текущего окна)

```powershell
list-windows (lw)
  Список всех дочерних окон/диалогов подключённого процесса.

list-controls (lc) [window-name]
  Дерево контролов в окне (до 5 уровней). Если window-name не указан — все окна.
  Полезно для первого знакомства с UI.

find <name>
  Поиск контрола по имени. Возвращает: AutomationId, ControlType, BoundingRect, IsEnabled, IsOffscreen.
  Стратегия: Name exact → Name contains → AutomationId.

dump [depth] [-f <file>] [-t <type>]
  Полный дамп UIA-дерева.
  depth: глубина (по умолч. 3)
  -f: запись в файл вместо stdout
  -t: фильтр по ControlType (Button, TabItem, Edit, ComboBox...)

inspect [index-path]
  Инспекция элемента в стиле Spy++: inspect 37 0 покажет свойства дочернего элемента по пути.

state
  Быстрый снапшот UI: активное окно, список диалогов, активный таб, активный вид, текст статус-бара.
  Это самая полезная команда для первого запроса агента.

info
  Информация о подключённом окне: PID, заголовок, размер, версия (Revit), AutomationId корня.
```

### 🧩 UIA Pattern Tools (чтение UI без скриншотов)

Чтение и взаимодействие с контролами через UIA-паттерны — без скриншотов, без LLM Vision.

```powershell
patterns <name>
  Показать все UIA-паттерны элемента (InvokePattern, TogglePattern, ValuePattern,
  GridPattern, TablePattern, ScrollPattern, ExpandCollapsePattern, SelectionPattern...).

dump-patterns [depth] [--type <ct>] [--filter-name <name>]
  Дамп UIA-дерева с перечислением поддерживаемых паттернов у каждого элемента.

tree-expand <name> [--all] [--depth N]
  Развернуть TreeView-узел и рекурсивно дампить всю ветку.
  --all: развернуть все узлы
  --depth: глубина развёртывания

combo-read (cr) <name>
  Открыть ComboBox, прочитать ВСЕ items, закрыть. Возвращает массив строк.

grid-read (gr) <name> [--rows N]
  Прочитать DataGrid через GridPattern: строки × колонки, structured data.

list-items (li) <name> [--max N]
  Прочитать все ListBox/ListView items (Text, AutomationId, IsSelected).

table-read (tr) <name> [--rows N]
  Прочитать Table с column headers и строками.

scroll-to <name> [--parent <p>]
  ScrollIntoView — прокрутить к элементу через ScrollItemPattern.
```

### 🎯 Advanced Pattern Actions

```powershell
invoke <name>
  Вызвать InvokePattern (надёжнее Click для кнопок). Использует UIA invoke, а не mouse click.

toggle <name> [on|off]
  Переключить checkbox/switch через TogglePattern.
  Без аргумента: прочитать состояние.
  on/off: установить состояние.

set-value <name> <text>
  Установить текст через ValuePattern (надёжнее type для текстовых полей).
```

### 🔍 Advanced Search & Watch

```powershell
find-all (fa) <name> [--max N] [--type <ct>]
  Найти ВСЕ совпадения, не только первое.
  Возвращает массив элементов с Name, AutomationId, ControlType, BoundingRect.

watch <command> [args...] --until <condition> [--interval <sec>] [--timeout <sec>]
  Поллинг команды до выполнения условия.
  --until: found (команда успешна), gone (код ошибки), enabled/disabled, text:substring (вывод содержит)
  Полезно: watch find "Modify | Walls" --until found --interval 1 --timeout 30
```

### ⌨️ Keyboard & Clipboard

```powershell
key-combo (kc) <keys>
  Отправить хоткей. Синтаксис:
    ^c = Ctrl+C, ^v = Ctrl+V
    %{F4} = Alt+F4
    {TAB} = Tab, {ENTER} = Enter
    ^+s = Ctrl+Shift+S

clipboard-get (cg)
  Прочитать текст из буфера обмена.

clipboard-set (cs) <text>
  Записать текст в буфер обмена.
```

### 🖼️ Region Tools

```powershell
screenshot-region (sr) <x> <y> <w> <h>
  Скриншот области экрана (пиксели). С --screenshot включит base64 в ответ.

highlight-region (hr) <x> <y> <w> <h> [duration-ms]
  Подсветка области красным overlay (по умолч. 2000 мс).
```

### 🖱️ Navigation & Interactions (Revit-focused)

```powershell
click <name>
  Нажать кнопку/контрол по имени. FlaUI Click через UIA Invoke если доступен.

safe-click <name>
  Идемпотентный клик — не падает если элемент уже исчез. Возвращает success:true в любом случае.

ribbon <button> [tab]
  Нажать кнопку на ленте. Если tab указан — переключиться на таб.
  Пример: ribbon "Wall" Architecture

ribbon <button> --tab <tab>
  Явное указание таба через флаг (для скриптов).

type <control> <text>
  Ввести текст в контрол. Фокусит, кликает, очищает, вводит текст.

switch-view (sv) <view-name>
  Переключить вкладку вида в Revit (Project Browser).

expand
  Развернуть "Подробности" в диалоге (ExpandCollapsePattern).

dropdown <btn> <item> [tab]
  SplitButton/DropDownButton → выбрать пункт меню.
  Пример: dropdown "Wall" "Rectangle" Architecture
```

### 📋 PropertySheet (диалоги свойств)

```powershell
ps <title> fields
  Прочитать все поля диалога (label → value пары).

ps <title> tabs
  Список вкладок диалога.

ps <title> type <label> <value>
  Ввести значение в поле по лейблу (находит Edit/ComboBox рядом с текстом).

ps <title> check <label> [true/false]
  Установить/прочитать CheckBox по лейблу.

ps <title> select <label> <option>
  Выбрать значение в ComboBox.

ps <title> click <button>
  Нажать кнопку в диалоге (OK/Cancel/Apply).

ps-batch <title> <json> [--tab <t>] [--timeout <sec>]
  Batch-fill нескольких полей из JSON.
  Пример: ps-batch "Instance Properties" '{"Height": "3000", "Level": "Level 2", "Structural Wall": false}'
  Поддерживает: string (type), boolean (check), автоматическое определение ComboBox vs Edit.
```

### 💬 TaskDialog

```powershell
taskdialog <title> read
  Прочитать заголовок, сообщение, футер.

taskdialog <title> click <button>
  Нажать кнопку (Да/Нет/OK/Отмена).

taskdialog <title> expand
  Развернуть "Показать подробности".
```

### 🎯 Smart Search

```powershell
ai-find <query> [--type <ct>] [--parent <p>] [--tab <t>] [--deep] [--max N]
```

Многостратегический поиск элемента. Стратегии (в порядке приоритета):
1. **Name** — exact → startsWith → contains
2. **Locale** — RU↔EN перевод через LocaleMap
3. **AutomationId** — поиск по AutoId всего дерева
4. **Regex** — `Regex.IsMatch(element.Name, query, IgnoreCase)`
5. **Sibling** — элементы на том же Y-уровне (только `--deep`)
6. **Tab-scoped** — переключает таб и ищет

При ошибке возвращает `{code: "NotFound", query, suggestions}`.

### 🏷️ Ribbon Tools

```powershell
ribbon-tabs (rt) [tab-name]
  Список табов (с кнопками если tab-name указан) или переключиться на таб.

rb [tab-name]
  Deep scan: все табы → панели → кнопки. Полная структура ленты.
  Без аргумента: список всех табов. С табом: панели и кнопки.

ribbon-find <tab> [panel [btn]]
  Найти таб/панель/кнопку и показать локацию (BoundingRect, AutomationId).

ribbon-panel <tab> [panel]
  Кнопки на конкретной панели (с фильтром по имени панели).

context-tabs
  Контекстные табы (Modify | Walls, Modify | Floors, ...).

qat [click <name>]
  Quick Access Toolbar: список кнопок или клик по кнопке.
```

### ⏱️ Waiting & Retry

```powershell
wait <seconds>
  Пауза N секунд.

wait-for <title> [timeout]
  Дождаться появления диалога (таймаут в секундах, по умолч. 15).

wait-close <title> [timeout]
  Дождаться закрытия диалога.

wait-element <name> [timeout]
  Дождаться появления UIA-элемента.

wait-progress [timeout]
  Дождаться завершения ProgressBar (RangeValue.Value >= 100).

retry-click <name> [--attempts N] [--delay Ms]
  Клик с экспоненциальным retry. По умолч: 3 попытки, 500мс задержка.

retry-dialog <title> [--attempts N] [--delay Ms]
  Дождаться диалога с retry-логикой.
```

### ✅ Assertions

```powershell
assert-dialog <title> exists
  Проверить что диалог открыт.

assert-dialog <title> text <expected>
  Проверить текст внутри диалога (contains).

assert-dialog <title> button <name>
  Проверить что кнопка существует.

assert-dialog <title> enabled <name>
  Проверить что кнопка активна.

assert-ribbon <tab>
  Проверить что таб существует.

assert-ribbon <tab> button <name>
  Проверить что кнопка на табе есть.

assert-view <name>
  Проверить что вкладка вида активна.
```

### 🖱️ Mouse & Canvas

```powershell
mouse-click <x> <y>
  Клик по координатам (DPI-aware, физические пиксели).

mouse-drag <x1> <y1> <x2> <y2>
  Drag from-to.

mouse-scroll <ticks>
  Scroll wheel (положительное = вверх, отрицательное = вниз).

mouse-pos
  Позиция курсора (x, y, DPI).

mouse-type <text>
  SendKeys через активный элемент.

canvas-click <x> <y> [--relative]
  Клик на GraphicsView (Revit viewport).
  --relative: координаты относительно viewport, а не экрана.

canvas-drag <x1> <y1> <x2> <y2> [--relative]
  Drag на GraphicsView.

canvas-zoom <factor>
  Zoom колесом над GraphicsView (положительное = in, отрицательное = out).

canvas-screenshot
  Скриншот GraphicsView.
```

### 🖼️ Computer Vision (OpenCV MatchTemplate)

```powershell
cv-match <template.png> [--region x,y,w,h] [--threshold 0.8]
  Найти шаблон на скриншоте. Ищет .png в ./templates/, ./cv-templates/, %LOCALAPPDATA%/ReVibe/UiController/templates/.

cv-click <template.png> [--threshold 0.8] [--region x,y,w,h]
  Найти шаблон и кликнуть в центр совпадения.

cv-templates [filter]
  Список доступных шаблонов.
```

### 🤖 LLM Vision (AI-powered)

```powershell
llm-find <description> [--region x,y,w,h] --provider <p> [--model <m>] [--timeout <s>]
  Найти элемент по текстовому описанию на скриншоте через LLM Vision.
  Возвращает координаты (elementX, elementY), имя, confidence.

llm-click <description> [--region x,y,w,h] --provider <p> [--model <m>]
  Найти и кликнуть по описанию. Делает screenshot до и после.
```

Провайдеры (требуется `--provider`):

| Провайдер | API Key | Модель по умолч. |
|-----------|---------|------------------|
| `routerai` | `ROUTERAI_API_KEY` | `qwen/qwen-vl-max` |
| `openai` | `OPENAI_API_KEY` | `gpt-4o` |
| `anthropic` | `ANTHROPIC_API_KEY` | `claude-sonnet-4-20250514` |
| `ollama` | локально | `llama3.2-vision` |

### 🔌 Revit API Bridge (через Named Pipe)

```powershell
revit-api <cmd> [--payload <json>]
  [DEPRECATED] Use `pipe-api` instead.
  Выполнить Revit API команду через Named Pipe (требуется ReVibe аддин).
  Пример: revit-api getParameter --payload '{"elementId":123,"paramName":"Height"}'

pipe-api <cmd> [--payload <json>]
  Выполнить Revit API команду через Named Pipe.

pipe-events [--last N]
  Показать последние PipeEvents из Revit named pipe.

revit-select <id> [id ...]
  Выбрать элементы по ID.

revit-get views
  Список открытых видов.

revit-get elements
  Элементы из активного вида.

revit-get categories
  Категории Revit.

revit-undo
  Отменить последнее действие (Ctrl+Z).
```

### 📜 Scripts & Recording

```powershell
script <file.rvs>
  Выполнить скрипт (.rvs). Одна команда на строку, # комментарий.

dry-run <file.rvs>
  Симуляция скрипта без реальных кликов (проверка синтаксиса).

record-start <path>
  Начать запись действий в .rvs файл.

record-stop
  Остановить запись и сохранить.

record-save [--path <p>] [--diff]
  Сохранить запись без остановки (--diff: показать git diff).

record-status
  Статус записи (активна ли, путь, количество команд).

script-list (sl) [--path <d>] [--git]
  Список .rvs файлов.

script-log (slog) [--file <p>] [--last N]
  Git log для скриптов.

script-diff (sdiff) [--file <p>] [--commit <h>]
  Git diff для скриптов.

record-video [--fps 5] [--quality]
  Запись экрана через FFmpeg (gdigrab).

record-video-stop
  Остановить запись, сохранить .mp4 в screenshots/.

record-export --xunit <file.rvs>
  Экспорт .rvs в xUnit C#. Выход: <file>.xunit.cs

record-export --gherkin <file.rvs>
  Экспорт .rvs в Gherkin. Выход: <file>.feature

record-export --python <file.rvs>
  Экспорт .rvs в Python. Выход: <file>.py
```

### 🗺️ UI Map (Page Object Model)

```powershell
uimap-load [path]
  Загрузить YAML-карту UI.

uimap-save [path]
  Сохранить карту в YAML.

uimap-resolve <name> [--version Y]
  Разрешить логическое имя в селекторы (с учётом версии Revit).

uimap-register <name> --auto-id <id>
  Зарегистрировать entry (AutoId/Name/Tab).

uimap-list [filter]
  Список всех registered entry.

uimap-auto <name> <element-name>
  Найти элемент, извлечь селекторы, зарегистрировать.
```

YAML-формат:
```yaml
entries:
  WallButton:
    automationId: RibbonButton_Wall
    name: Стена
    tab: Архитектура
    fallbacks: [Wall, Стена]
    versions:
      "2025": { automationId: RibbonButton_Wall_2025 }
```

Авто-загрузка: `./uimap.yaml`, `./config/uimap.yaml`, `%LOCALAPPDATA%/ReVibe/UiController/uimap.yaml`

### ⚡ Stateful Session

```powershell
session-begin [--dialog <title>] [--tab <tab>]
  Начать stateful сессию. При активной сессии:
    - type/ps/taskdialog без диалога → авто-скоп на ActiveDialog
    - click в скриптах → авто-скоп на ActiveDialog
    - set/get-output → переменные сессии с $varName подстановкой

session-end
  Закончить сессию.

session-status
  Контекст: ActiveDialog, ActiveTab, переменные, стек диалогов.
```

### 🌐 WebView2 (CDP через Playwright) — Legacy root project only

```powershell
wv-connect [--port 9222] [--timeout 30]
  Подключиться к WebView2 через CDP.

wv-click <selector>
  Клик по CSS/XPath селектору.

wv-type <selector> <text>
  Ввод текста.

wv-text <selector>
  Чтение innerText элемента.

wv-wait <selector> [--timeout 5]
  Ожидание появления элемента.

wv-eval <js>
  Выполнить JavaScript на странице. Возвращает результат.

wv-url
  Текущий URL WebView2.

wv-screenshot [--path <file.png>]
  Скриншот всей страницы (FullPage).

wv-list (wv-ls) [--filter <text>]
  Список интерактивных элементов (role, name, selector, tag).
```

### 🧩 Event-Driven Automation (< 100ms отклик)

```powershell
listen-start
  Запустить event-driven listener (WinEventHook + UIA events).
  Подписывается: WindowOpened, WindowClosed, MenuOpened, MenuClosed, ToolTipOpened, TextChange.

listen-stop
  Остановить listener.

event-log [--last N]
  Показать последние N событий (type, name, controlType, timestamp).
```

### 🔄 Daemon & Batch

```powershell
daemon [--start|--stop|--status] [--pipe <name>] [--profile <name>]
  Persistent daemon: named pipe сервер. Одно подключение для множества команд.
  После --start можно отправлять команды через pipe.
  Флаг --auto-screenshot включает авто-скриншот при ошибке.

batch <json-array> [--pretty]
  Выполнить несколько команд из JSON-массива. Возвращает массив CommandResult.
  Каждая команда поддерживает: command, args, onError (stop/skip), if (previous_success/previous_failed), onlyIf (dialog/exists/element/enabled).
  Пример: batch '[{"command":"state"},{"command":"monitors"}]'

__undo [--action status|undo|checkpoint|undo-to] [--count N]
  Daemon protocol: управление undo стеком. (не CLI команда, а pipe протокол)

__events [--timeout N] [--max-events N]
  Daemon protocol: получить последние UI события.

__watch [--command <cmd>] [--condition found|gone|enabled|disabled]
  Daemon protocol: длительный poll команды.
```

### 🏗️ Revit Instance Management

```powershell
revit-instances (ri)
  [DEPRECATED] Use `app-instances --profile revit` instead.

revit-launch --version <year> [--project <path.rvt>]
  Запустить новый экземпляр Revit указанной версии.

multi-exec --all <command> [args...]
  Выполнить команду на ВСЕХ запущенных экземплярах Revit.

session-switch <pid>
  Переключить активную сессию на другой PID.

app-instances --profile <name>
  Список запущенных экземпляров для любого профиля.

app-restart --profile <name>
  Запустить приложение если не запущено (или перезапустить).
```

### 🔄 Fallback-слои

```powershell
win32-click <name>
  Win32 SendMessage fallback — клик через PostMessage.

win32-enum
  Перечислить Win32 дочерние окна.

wad-connect
  Подключиться к WinAppDriver (требуется запущенный WinAppDriver).

wad-find <method> <value>
  Найти элемент через WinAppDriver REST API.
  method: name, xpath, accessibilityId, className, tagName

wad-click <element-id>
  Клик через WinAppDriver по element ID.
```

### 🛡️ Safety & Diagnostics

```powershell
safety-check
  Проверить/закрыть неожиданные warning-диалоги.

process-list
  Список Revit-процессов (PID, окно, версия, название).

process-info
  Детали подключённого процесса.

logs [--tail N] [--level L]
  Логи контроллера/плагина.

statusbar
  Текст статус-бара Revit.

highlight <name> [ms]
  Подсветить элемент полупрозрачным красным overlay.

highlight-clear
  Снять подсветку.

cache-clear
  Очистить кэш UIA-элементов.

cache-stats
  Статистика кэша (размер, TTL, хиты/миссы).

cached-find <name>
  Поиск с кэшем (TTL 5 секунд).

allure-setup [--output <dir>]
  Инициализировать Allure reporting (создаёт директорию allure-results).

allure-report [--input <dir>] [--output <dir>]
  Сгенерировать HTML Allure отчёт (требуется Allure CLI).
```

---

## MCP Server (Model Context Protocol)

Запускается как stdio-сервер. Общается с Daemon через `DaemonBridge` (обёртка над `DaemonClient`).
Требует запущенного Daemon (`--daemon`).

```powershell
dotnet run --project tools\RevitUiController\RevitUiController.McpServer
dotnet run --project tools\RevitUiController\RevitUiController.McpServer -- --pipe MyPipe
```

### 33 MCP Tools

| Tool | Параметры | Описание |
|------|-----------|----------|
| `revit_connect` | `process_name?`, `pid?`, `window_title?`, `timeout?` | Подключиться к процессу (должен быть вызван первым) |
| `revit_click` | `name`, `type?`, `tab?`, `wait_after?`, `modifiers?`, `retry?`, `timeout?` | Клик по элементу |
| `revit_find` | `name`, `type?`, `locale?`, `strategy?` | Поиск элемента (6 стратегий) |
| `revit_ribbon` | `button`, `tab?`, `panel?` | Клик по кнопке ленты |
| `revit_ps` | `title`, `action?`, `field?`, `value?` | PropertySheet операции |
| `revit_type` | `name`, `text` | Ввод текста |
| `revit_switch_view` | `name` | Переключение вида |
| `revit_wait_for` | `title`, `timeout?` | Ожидание диалога |
| `revit_wait_close` | `title`, `timeout?` | Ожидание закрытия |
| `revit_wait_element` | `name`, `timeout?` | Ожидание элемента |
| `revit_task_dialog` | `title`, `action?`, `button?` | TaskDialog (read/click/expand) |
| `revit_batch` | `commands` (JSON array) | Batch с условной логикой |
| `revit_list_windows` | — | Список окон |
| `revit_list_controls` | `name?` | Список контролов |
| `revit_state` | — | Snapshot UI |
| `revit_safe_click` | `name` | Идемпотентный клик |
| `revit_select` | `element_ids` (comma-separated) | Выбор элементов |
| `revit_get_property` | `element_id`, `property_name` | Чтение свойства |
| `revit_key_combo` | `keys` | Хоткей |
| `revit_screenshot` | — | Скриншот base64 |
| `revit_list_tabs` | `tab?` | Список табов ленты |
| `revit_status_bar` | — | Статус-бар |
| `revit_undo` | `action` (status\|undo\|checkpoint\|undo-to), `count?`, `name?` | Управление undo |
| `revit_checkpoint` | `name` | Создать checkpoint |
| `revit_undo_last` | `count?` | Откатить N команд |
| `revit_undo_to` | `name` | Откатить к checkpoint |
| `revit_events` | `timeout?`, `max_events?` | UI события |
| `revit_ping` | — | Проверка daemon |
| `revit_session_begin` | `name` (default: "default") | Начать сессию |
| `revit_session_end` | — | Закончить сессию |
| `revit_session_status` | — | Статус сессии |
| `revit_session_set` | `name`, `value` | Установить переменную |
| `revit_session_get` | `name` | Получить переменную |

---

## Daemon Protocol (named pipe `\\.\pipe\RevitUiController`)

Line-delimited JSON. `DaemonRequest` содержит **28 полей**:

| Поле | Тип | Описание |
|------|-----|----------|
| `command` | string | Команда или `__connect`, `__ping`, `__shutdown`, `__batch`, `__undo`, `__events`, `__watch` |
| `args` | List\<string\>? | Позиционные аргументы |
| `pid` | int? | PID для подключения |
| `processName` | string? | Имя процесса |
| `windowTitle` | string? | Фильтр по заголовку |
| `useActive` | bool | Использовать активное окно |
| `timeout` | int? | Таймаут (сек) |
| `interval` | int? | Интервал поллинга для watch |
| `condition` | string? | Условие watch (found/gone/enabled/disabled/text:...) |
| `subCommand` | string? | Подкоманда |
| `subArgs` | List\<string\>? | Аргументы подкоманды |
| `action` | string? | Тип действия (status/undo/checkpoint/undo-to) |
| `count` | int? | Количество (undo steps, events) |
| `maxEvents` | int? | Макс. событий |
| `commands` | List\<DaemonRequest\>? | Batch-команды |
| `type` | string? | ControlType фильтр |
| `tab` | string? | Имя таба |
| `panel` | string? | Имя панели ленты |
| `waitAfter` | int? | Пауза после действия (сек) |
| `modifiers` | string? | Модификаторы (ctrl/shift/alt) |
| `retry` | bool? | Авто-retry при ошибке |
| `elementIds` | List\<string\>? | ID элементов для select |
| `propertyName` | string? | Имя свойства для чтения |
| `locale` | string? | Локализация |
| `strategy` | string? | Стратегия поиска |
| `onError` | string? | stop/skip |
| `if` | string? | previous_success/previous_failed |
| `onlyIf` | OnlyIfCondition? | `{dialog?, exists?, element?, enabled?}` |

Response: `{"success":true,"data":{...},"error":null,"stateDiff":{...},"screenshot":"...","durationMs":1234}`

---

## Plugin System
- `IPlugin` interface with `RegisterCommands(CommandRegistry)`
- DLLs expected in `Host/Plugins/` loaded at startup (директория может не существовать — код обрабатывает gracefully)
- RevitPlugin registers all Revit-specific commands

---

## LLM Vision Providers (auto-selection by priority)
1. **RouterAI** — `ROUTERAI_API_KEY`, model `qwen/qwen-vl-max`
2. **OpenAI** — `OPENAI_API_KEY`, model `gpt-4o`
3. **Anthropic** — `ANTHROPIC_API_KEY`, model `claude-sonnet-4-20250514`
4. **Ollama** — local, model `llama3.2-vision`

---

## Key Interfaces

### IApplicationProfile
`Name`, `ProcessName`, `DisplayName`, `ExecutablePaths`, `PipeName`, `ConfigDirectory`, `KnownVersions`, `UiMapFileName`, `LocaleFileName`, `TemplatesDirectory`, `ScriptsDirectory`, `DetectVersionFromTitle()`, `DetectVersionFromFileVersion()`, `BuildLlmSystemPrompt()`

### IApplicationLauncher
`Launch()`, `FindRunning()`, `WaitForReady()`, `IsAlive()`
⚠️ `RegisterLauncher()` в Host/Program.cs определён, но НЕ вызывается из `ConfigureServices()`.

### IAutomationProvider
`GetDesktop()`, `GetRootElement(IntPtr hwnd)`, `FindFirst()`, `FindFirstEnabledVisible()`, `FindAllChildren()`, `FindAllByControlType()`, `FindActiveDialogs()`, `UIA3` (property), `IsUia3` (property), `Name` (property), implements `IDisposable`

### IPlugin
`Name`, `RegisterCommands(CommandRegistry)`

### CommandRegistry
`Register(ICommand)`, `Register<T>()`, `RegisterType(Type)`, `RegisterAlias()`, `GetCommand()`, `GetCommandType()`, `AllCommands` (property), `AllAliases` (property), `AllCommandTypes` (property)

---

## Configuration (config.yaml)

Location: `RevitUiController.Host/config.yaml` или `%APPDATA%/UiController/config.yaml`

```yaml
profiles:
  revit:
    processName: Revit
    displayName: Autodesk Revit          # Человекочитаемое имя
    pipeName: ReVibe
    executablePaths:
      - "C:\\Program Files\\Autodesk\\Revit 2026\\Revit.exe"
      - "C:\\Program Files\\Autodesk\\Revit 2025\\Revit.exe"
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

---

## Best Practices для AI-агентов

1. **Начинайте с `state --pretty`** — получите snapshot UI: активное окно, диалоги, табы, виды.
2. **Используйте `ai-find`** для сложного поиска — он сам попробует 6 стратегий.
3. **Проверяйте `diff`** в ответе — он показывает какие диалоги открылись/закрылись.
4. **Idempotence** — `safe-click` не падает если элемент исчез.
5. **Stateful session** — `session-begin` + `session-end` для последовательности команд.
6. **Для RDP/headless** используйте `--uia-only` (иначе GDI/mouse_event не работают).
7. **Для CI** используйте `--non-interactive` — все деструктивные действия отклоняются.
8. **Auto-screenshot при ошибке** — PNG сохраняется в `screenshots/error_<timestamp>.png`.
9. **UI Map** — загружайте `uimap-load` для версионированных селекторов.
10. **Запись** — используйте `record-start` для создания воспроизводимых .rvs скриптов.
11. **Скриншоты с LLM Vision** — `llm-find` + `llm-click` когда UIA не справляется.
12. **Координаты** — `mouse-pos` + `monitors` для понимания DPI перед mouse-click.
13. **MCP** — используйте McpServer для AI-интеграции через 33 инструмента.
14. **Daemon** — для production используйте `--daemon` режим (persistent named pipe).

---

## .rvs Script Format

Файл скрипта: одна команда на строку, `#` — комментарий.

**Директивы:**

| Директива | Описание |
|-----------|----------|
| `wait-for "Title" [sec]` | Дождаться появления диалога |
| `wait-close "Title" [sec]` | Дождаться закрытия диалога |
| `window "Title"` | Установить активный диалог для последующих команд |
| `set <var> <value>` | Установить переменную сессии |
| `get-output <var>` | Сохранить результат последней команды в `$var` |
| `select "Label" "Option"` | Выбрать значение в ComboBox |
| `if-dialog "Title"` | Условное выполнение |
| `on-error stop\|skip` | Обработка ошибок |
| `wait-any "Title1" "Title2"` | Ожидание любого из диалогов |
| `while-dialog "Title"` | Цикл пока диалог открыт |

**Пример `create-wall.rvs`:**
```rvs
session-begin
ribbon Wall Architecture
wait-for "Modify | Walls" 15
ps type "Height" 3000
select "Level" "Level 2"
check "Structural Wall" false
set wallResult "created"
ps click OK
wait-close "Modify | Walls" 10
session-end
```

---

## Element Search Hierarchy (как агент находит элементы)

```
1. FlaUI UIA3 AutomationId     — самый быстрый и точный
2. FlaUI UIA3 Name contains    + LocaleMap RU↔EN
3. ai-find (6 стратегий)       — name → locale → autoId → regex → sibling → tab
4. UiMap resolve               — логическое имя → селекторы (version-specific)
5. Mouse-клик по координатам    — BoundingRect, DPI-aware
6. Win32 SendInput/PostMessage — низкоуровневый Win32 API
7. WinAppDriver                 — REST API fallback
8. WebView2 CDP (wv-*)         — для Tauri/React приложений (legacy root only)
```

---

## Известные проблемы / TODO

| Проблема | Статус |
|----------|--------|
| `RegisterLauncher()` определён, но не вызывается — `IApplicationLauncher` не в DI | 🚫 Баг |
| Алиас `ps` конфликтует: `process-list` и PropertySheet | 🚫 Баг |
| `allure-open` команда не существует (только `allure-setup`, `allure-report`) | 📄 Документация |
| WebView2 команды (`wv-*`) только в legacy root, не в Core/Commands/ | 🏗️ Перенести |
| `Host/Plugins/` директория может не существовать | ⚠️ Graceful handling |
