# Release Plan

## v0.5.0 — Structured Responses & Screenshots

**Цель:** Заменить сырой JSON на структурированный ответ, добавить авто-скриншоты.

### P0.1 Структурированный ответ
- Обновить `DaemonResponse` — success/data/error/screenshot/stateDiff/durationMs
- Убрать перехват `Console.Out` в Daemon — ответ через прямой JSON
- Все MCP-инструменты возвращают строгую структуру, а не string

### P0.2 Скриншот после каждого действия
- Флаг `--auto-screenshot` на Daemon
- После каждой команды: base64 скриншот в ответе
- ROI: crop вокруг изменённого элемента

---

## v0.6.0 — Event-Driven & Smart Errors

**Цель:** Убрать polling, сделать ошибки самодиагностируемыми.

### P0.3 Event-driven ожидание
- Wait-команды через `EventService` (sub-100ms)
- Daemon сам определяет reasonable timeout
- Wait возвращает событие: что изменилось

### P0.4 Умная ошибка с контекстом
- Self-describing error: код, сообщение, suggestions
- Fuzzy match (Levenshtein) при ненайденном элементе
- Auto-fallback: ai-find, locale, LLM Vision

---

## v0.7.0 — Session Context

**Цель:** Контекст между вызовами MCP.

### P0.5 Сессия с контекстом
- `revit_session_begin(name)` / `revit_session_end()`
- Переменные (SetVariable/GetVariable)
- Стек диалогов (автоматический pop при закрытии)
- История команд (undo-стек)
- Статус сессии в ответе каждой команды

---

## v0.8.0 — Rich Tools & Undo

**Цель:** Типизированные параметры инструментов, undo с контекстом.

### P1.6 Rich MCP tools
- `revit_click(name, type?, tab?, wait_after?, modifiers?, retry?, timeout?)`
- `revit_find(name, type?, locale?, strategy?)`
- `revit_ribbon(button, tab?, panel?)`
- `revit_select(elements_ids[])`
- `revit_get_property(element_id, property_name)`

### P1.7 Undo с контекстом
- Daemon хранит стек команд
- `revit_undo_last(n)` / `revit_undo_to(checkpoint)`
- `revit_checkpoint("name")`

---

## v0.9.0 — Smart Retry & Batch

**Цель:** Цепочка fallback'ов, условный batch.

### P1.8 Smart retry & fallback chain
- `revit_click` сам пробует: UIA → ai-find → LLM Vision → Win32
- Результат: какая стратегия сработала

### P1.9 Batch с условиями
- `onError: "stop|skip|fallback"`
- `if: "previous_success"`
- `onlyIf: { dialog, exists }`

---

## v1.0.0 — LLM Vision & Stability

**Цель:** LLM Vision как fallback, стабилизация.

### P1.10 LLM Vision как built-in fallback
- Встроить в цепочку revit_find
- Кэширование результата
- Self-healing (если AutomationId изменился)

### Стабилизация
- Тесты
- Документация
- Исправление багов
