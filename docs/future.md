# RevitUiController — План развития (для AI-агента)

## P0: Критические улучшения для работы AI

### 1. Структурированный ответ (вместо сырого JSON)

**Проблема:** Все MCP-инструменты возвращают `string` — сырой JSON, который AI должен парсить сам. Нет типизированных ответов, ошибки вперемешку с данными.

**Решение:**
- Все MCP-инструменты возвращают строгую структуру:
  ```json
  {
    "success": true/false,
    "data": { ... },
    "error": { "code": "...", "message": "...", "suggestions": [...] },
    "screenshot": "base64...",     // всегда, если включён флаг
    "stateDiff": { ... },          // что изменилось
    "durationMs": 123
  }
  ```
- Убрать перехват `Console.Out` в Daemon — ответ должен быть не stdout команды, а структурированный JSON от DaemonResponse

### 2. Скриншот после каждого действия

**Проблема:** AI "слеп". `revit_screenshot()` нужно вызывать отдельно, нет автоматической обратной связи.

**Решение:**
- Флаг `--auto-screenshot` на Daemon — после каждой команды возвращать скриншот в base64
- Region of Interest — возвращать не весь экран, а область вокруг изменённого элемента (diff-based crop)
- Опционально: annotate-скриншот (обвести найденный/нажатый элемент)

### 3. Event-driven ожидание (не polling)

**Проблема:** `revit_wait_for()` внутри делает polling 500ms. Это медленно и не надёжно для быстрых диалогов.

**Решение:**
- Все wait-команды используют `EventService` (UIA events, sub-100ms latency)
- AI не нужно указывать таймаут — Daemon сам определяет reasonable timeout
- Wait возвращает событие: что именно изменилось (диалог открыт/закрыт, элемент появился/исчез)

### 4. Умная ошибка с контекстом

**Проблема:** При ошибке AI получает `"Element 'xxx' not found"` и не знает, что делать.

**Решение:**
- Self-describing error:
  ```json
  {
    "code": "element_not_found",
    "message": "Element 'Properties' not found",
    "query": "Properties",
    "suggestions": [
      "Try 'ai-find Properties' for multi-strategy search",
      "Try 'list-controls' to see available elements",
      "Check locale: 'properties' in Russian may be 'Свойства'"
    ],
    "availableElements": ["Propertys", "Property", "Properties 1"]
  }
  ```
- **Fuzzy match**: если элемент не найден, вернуть похожие (Levenshtein distance)
- **Auto-fallback**: ai-find, locale, LLM Vision если элемент не найден

### 5. Сессия с контекстом между вызовами

**Проблема:** Каждый MCP-вызов — отдельный запрос. AI не может сказать "запомни это окно" или "работай с прошлым контекстом".

**Решение:**
- `revit_session_begin(name)` — старт именованной сессии (сохраняется в SessionContext)
- `revit_session_end()` — завершение
- Контекст сессии:
  - Активное окно/диалог
  - История команд (undo-стек)
  - Переменные (`SetVariable`/`GetVariable`)
  - Стек диалогов (автоматический pop при закрытии)
- AI может положить ID элемента в переменную и ссылаться на него позже

---

## P1: Важные улучшения

### 6. Rich MCP tools (типизированные параметры)

**Проблема:** Сейчас `revit_click(name)` принимает только имя. Нет фильтров, нет модификаторов.

**Решение:**
- `revit_click(name, type?, tab?, wait_after?, modifiers?, retry?, timeout?)`
- `revit_find(name, type?, locale?, strategy?)` — явный выбор стратегии
- `revit_ribbon(button, tab?, panel?)` — поиск по tab + panel + button
- `revit_select(elements_ids[])` — выбор нескольких элементов
- `revit_get_property(element_id, property_name)` — чтение параметра

### 7. Undo с контекстом

**Проблема:** `revit_undo` работает, но нет связи с выполненными командами.

**Решение:**
- Daemon хранит стек выполненных команд с параметрами
- `revit_undo_last(n)` — откат последних n команд
- `revit_undo_to(checkpoint)` — откат до чекпоинта
- Чекпоинты: `revit_checkpoint("before_delete")`
- При ошибке: автоматический откат до последнего чекпоинта

### 8. Smart retry & fallback chain

**Проблема:** Если `click` не сработал, AI должен явно вызывать `safe-click`, `win32-click`, `canvas-click`.

**Решение:**
- `revit_click` сам пробует цепочку: UIA click → ai-find + click → LLM Vision → Win32 click → WinAppDriver
- Возвращает результат с указанием, какая стратегия сработала
- Конфигурируемая цепочка (можно отключить дорогие стратегии)

### 9. Batch с условиями

**Проблема:** `revit_batch()` выполняет всё последовательно. Если первый шаг упал, остальные всё равно бегут.

**Решение:**
- Batch с conditional execution:
  ```json
  [
    { "command": "click", "args": ["Button"], "onError": "stop|skip|fallback" },
    { "command": "wait-for", "args": ["Dialog"], "timeout": 5, "if": "previous_success" },
    { "command": "click", "args": ["OK"], "onlyIf": { "dialog": "Dialog", "exists": true } }
  ]
  ```
- Batch возвращает подробный результат по каждому шагу с duration

### 10. LLM Vision как built-in fallback

**Проблема:** LLM Vision доступен через отдельную команду (`revit_find`), но не встроен в поиск.

**Решение:**
- Встроить LLM Vision как последний fallback в цепочку `revit_find`
- Кэширование результата LLM (если элемент найден, запомнить координаты и AutomationId)
- Self-healing: если AutomationId изменился, LLM Vision находит элемент по описанию

---

## P2: Желательно

### 11. Слепая зона: WebView2/Ribbon/Canvas

**Проблема:** Ribbon, Canvas, WebView2 — ограниченно доступны через UIA.

**Решение:**
- Ribbon: автоматическая детекция контекстных табов, поиск по иконке (OpenCV)
- Canvas: клик по координатам (уже есть), но нужно связать LLM Vision + координаты
- WebView2: Playwright + CDP для автоматизации диалогов на WebView2 (уже есть, но не во всех командах)

### 12. Undo-стек с визуализацией

**Проблема:** AI не видит, что делает Revit. Нет "diff" между состояниями.

**Решение:**
- `revit_undo_status()` — возвращает список выполненных действий с diff
- После каждой команды: `UiStateDiff` (активный диалог, открытые/закрытые диалоги, смена таба)
- Если diff показывает неожиданный диалог — AI получает предупреждение

### 13. Команды для типовых Revit-операций

**Проблема:** Сейчас всё через клики. Нет high-level команд.

**Решение:**
- `revit_create_element(category, family, type)` — создание элемента
- `revit_select_by_category(category)` — выбор по категории
- `revit_modify_parameter(element_ids, param_name, value)` — изменение параметра
- `revit_export(format, path)` — экспорт
- `revit_print_sheets(sheets[])` — печать листов

### 14. Режим dry-run / simulation

**Проблема:** AI не может "прикинуть" результат операции.

**Решение:**
- `revit_dry_run(commands[])` — выполняет без деструктивных действий, возвращает ожидаемый diff
- Preview: какие диалоги откроются, какие элементы изменятся
- Безопасность: AI может проверить сценарий перед выполнением

### 15. Мониторинг прогресса

**Проблема:** Revit показывает прогресс-бар, но AI не знает о нём.

**Решение:**
- `revit_wait_progress(timeout?)` — ждёт завершения прогресс-бара (event-driven)
- Возвращает результат: успех, ошибка, длительность
- Фоновая проверка прогресс-бара после каждой команды

---

## P3: Нишевые улучшения

### 16. Multi-instance orchestration

**Проблема:** Работа с одним Revit. Если запущено 3 инстанса — нужно явно переключаться.

**Решение:**
- `revit_execute_all(command)` — выполнить на всех инстансах
- `revit_session_switch(pid)` — быстрое переключение между сессиями
- Parallel batch: выполнить шаги на разных инстансах

### 17. Прогрессивное логирование для AI

**Проблема:** Логи слишком подробные.

**Решение:**
- Логи в структурированном JSON, доступные через MCP
- Три уровня: summary → details → full
- Auto-truncation для long output
- Экспорт лога сессии для анализа

### 18. Команды для тестирования

**Проблема:** Нет assert-команд в MCP.

**Решение:**
- `revit_assert_dialog(title, exists?, text?, button?)` — проверить диалог
- `revit_assert_element(name, exists?, enabled?, value?)` — проверить элемент
- `revit_assert_state(view?, tab?, dialog?)` — проверить состояние
- Assert возвращает `{ passed: true/false, actual: { ... }, expected: { ... } }`
