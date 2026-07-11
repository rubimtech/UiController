# План рефакторинга RevitUiController

**Дата:** 2026-07-11
**Основание:** `CLAUDE_REVIEW_2026-07-11.md` (9 проблем, 5 приоритетов)

---

## Фаза 0 — Подготовка (перед любыми правками)

- [ ] Создать git-ветку `refactor/phase-1` от `6f6eeef` (до текущих изменений)
- [ ] Написать интеграционные smoke-тесты для ключевых сценариев:
  - `--uia-only screenshot-region`
  - `click` на известную кнопку
  - `--non-interactive` + destructive action
  - `revit-api` (если есть сервер)
- [ ] Удалить `[Obsolete]`-файлы: `RevitSession.cs`, `FlakyRetry.cs`
- [ ] Заменить все `Console.Error.WriteLine` внутри команд на `LoggingService`

---

## Фаза 1 — Базовая надёжность и headless (проблемы 1, 2, 3)

### 1.1 WinAppDriver — статический клиент (проблема 1 — headless-скриншот)

**Файлы:** `Program.cs`, `ScreenshotHelper.cs`, `WinAppDriverCommands.cs`

**Задача:** `Program.WadClient` нигде не присваивается → `GetWadScreenshot()` всегда возвращает `null`.

**Решение:**
- В `Program.ParseGlobalFlags()` при `--uia-only` создать и присвоить `WadClient` (лениво, при первом обращении).
- Alternative: добавить lazy-фабрику `Lazy<WinAppDriverClient>` в `ScreenshotHelper`.
- Убрать `using var` в `WinAppDriverCommands.cs` — клиент должен жить дольше одной команды. Либо сделать счётчик ссылок, либо передавать владение в `Program.WadClient`.

**Проверка:** `--uia-only screenshot-region 0 0 100 100` возвращает не-null.

### 1.2 Watchdog на зависшее UIA-дерево (проблема 2)

**Файлы:** `AutomationHelper.cs`

**Задача:** `SafeGetChildren` создаёт `CancellationTokenSource`, но реальный COM-вызов `FindAllChildren()` висит бесконечно — таймаут декоративный.

**Решение:**
```csharp
public static AutomationElement[] SafeGetChildren(AutomationElement element, int timeoutMs = 4000)
{
    var task = Task.Run(() => element.FindAllChildren().ToArray());
    if (task.Wait(timeoutMs))
        return task.Result;
    LoggingService.Warn(nameof(SafeGetChildren), $"Timeout after {timeoutMs}ms");
    return Array.Empty<AutomationElement>();
}
```
Либо использовать `Task<T>` + `Wait(timeout)`. COM-апартмент может мешать — если `Task.Run` не сработает в STA, придётся в отдельный поток:
```csharp
var thread = new Thread(() => { result = element.FindAllChildren().ToArray(); });
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
if (!thread.Join(timeoutMs)) thread.Abort();
```

**Проверка:** При зависшем UIA-дереве `SafeGetChildren` возвращает пустой массив за `timeoutMs`, а не виснет навсегда.

### 1.3 Убрать параллельный обход UIA-дерева (проблема 3)

**Файлы:** `AutomationHelper.cs`

**Задача:** `FindControlsByName` использует `Parallel.ForEach` с `MaxDegreeOfParallelism=4` для обхода живых COM-объектов UIA3/FlaUI, которые не потокобезопасны.

**Решение:**
- Заменить `Parallel.ForEach` на последовательный `foreach` с `Queue<AutomationElement>` (BFS).
- Это затрагивает только `FindControlsByName` (используется `ai-find`, `find-all`, `expand`).
- Основной `click`-путь (`FindFirstEnabledVisible`) уже последовательный — его не трогать.

**Проверка:** `ai-find` и `find-all` работают без редких зависаний при перерисовке UI.

---

## Фаза 2 — Headless-клик (проблема 4)

### 2.1 Клик-лестница: InvokePattern → PostMessage → мышь

**Файлы:** `MouseControl.cs`, `ClickCommand.cs`, `Win32Helper.cs`

**Задача:** `--uia-only` не делает реально headless-клик. `ClickAtUia` вызывает FlaUI `element.Click()` — физическое перемещение мыши. `Drag` игнорирует `--uia-only` полностью.

**Решение:**
- В `ClickAtUia` изменить приоритет:
  1. `InvokePattern` (если элемент его поддерживает) — headless
  2. `BM_CLICK` / `PostMessage(WM_LBUTTONDOWN+UP)` — headless
  3. FlaUI `element.Click()` — fallback с физической мышью (логировать какой способ сработал)
- `Drag` при `--uia-only`: использовать WinAppDriver drag (если есть), либо UIA `TransformPattern`, либо возвращать ошибку.
- В `ClickCommand.ExecuteAsync`: если элемент найден, попробовать `InvokePattern`, `BM_CLICK` до физического клика.

**Проверка:** `--uia-only click "OK"` кликает без перемещения курсора мыши.

---

## Фаза 3 — Безопасность и приватность (проблемы 5, 7)

### 3.1 SafetyGuard в диспетчере команд (проблема 5)

**Файлы:** `Program.cs`, `SafetyGuard.cs`

**Задача:** `SafetyGuard` вызывается только в `ScriptCommands.cs`. Прямые команды (`click "Purge Unused"`, `revit-api setParameter`) идут мимо защиты. `--non-interactive` просто ставит флаг, который никто не проверяет в диспетчере.

**Решение:**
- В `Program.Main` после получения `cmd` и перед `cmd.ExecuteAsync` добавить:
  ```csharp
  var cmdArgs = cleanArgs.Skip(1).ToArray();
  if (SafetyGuard.IsDestructive(cmdName, cmdArgs) && !SafetyGuard.ConfirmDestructiveAction($"{cmdName} {string.Join(" ", cmdArgs)}"))
      return 0;
  ```
- В `SafetyGuard.IsDestructive`: улучшить детекцию — вместо `Contains` по 11 словам, сделать список команд, которые считаются «потенциально деструктивными» (`click`, `safe-click`, `ribbon`, `revit-api`, `win32-click`), и проверять аргументы. Для `revit-api` — парсить JSON-пейлоад на ключевые слова.
- Задокументировать в README, что `--non-interactive` глобально отклоняет деструктивные действия.

**Проверка:** `--non-interactive click "Purge Unused"` не выполняет клик без подтверждения.

### 3.2 Явный выбор LLM-провайдера (проблема 7)

**Файлы:** `LlmVisionClient.cs`, `LlmCommands.cs` (`LlmFindCommand`, `LlmClickCommand`)

**Задача:** `ResolveProvider` без `--provider` берёт первого доступного — RouterAI. Если в окружении есть `ROUTERAI_API_KEY`, скриншот окна уходит на сторонний сервис без подтверждения.

**Решение:**
- В `ResolveProvider` при отсутствии явного `--provider` не выбирать провайдера, а возвращать `null`.
- В `LlmFindCommand`/`LlmClickCommand`: если провайдер не указан явно — писать в stderr:
  ```
  [LLM] No provider specified. Use --provider to choose: routerai, openai, anthropic, ollama
  ```
  и завершаться с exit code 1.
- При каждом сетевом вызове со скриншотом логировать предупреждение:
  ```
  [LLM] Sending screenshot to {provider}/{model}
  ```

**Проверка:** `llm-find "кнопка OK"` без `--provider` не отправляет скриншот, а просит указать провайдера.

---

## Фаза 4 — Корректность (проблемы 6, 8)

### 4.1 Определение версии Revit (проблема 6)

**Файлы:** `RevitVersionProfile.cs`, `RevitInstanceManager.cs`

**Задача:** `DetectVersion`/`DetectYearFromTitle` ищут год в заголовке окна через `Contains` → файл `Объект_2025.rvt` в Revit 2023 определится как 2025.

**Решение:**
- Инвертировать приоритет:
  1. `FileVersionInfo` главного модуля процесса (уже есть `DetectFromFileVersion`)
  2. Реестр (`DetectFromRegistry`)
  3. Заголовок окна как последний вариант — искать не `Contains(year)`, а regex `\b(202[2-7])\b` на границах слов
- В `RevitInstanceManager.DetectYearFromTitle` убрать дублирование — вызывать `RevitVersionProfile.DetectVersion` или удалить метод.

**Проверка:** Revit 2023 с файлом `Объект_2025.rvt` определяется как 2023.

### 4.2 Revit API pipe — документация (проблема 8)

**Файлы:** `README.md`, `RevitApiCommand.cs`, `PipeBridgeClient.cs`

**Задача:** Серверного аддина нет в репозитории, pipe без аутентификации.

**Решение:**
- В README добавить секцию «Revit API Bridge» с указанием, что требуется отдельный серверный аддин (ссылка, если есть).
- В `PipeBridgeClient.cs` добавить TODO или простую аутентификацию (токен из аргументов/файла, передаваемый в рукопожатие).
- Если серверного аддина нет и не планируется — пометить `revit-api`/`revit-select`/`revit-get` как experimental.

---

## Фаза 5 — Гигиена (проблема 9)

### 5.1 Мёртвый код

- Удалить `RevitSession.cs` (164 строк, `[Obsolete("Use WindowSession instead.")]`).
- Удалить `FlakyRetry.cs` (49 строк, `[Obsolete("Use RetryPolicy instead.")]`).
- Проверить, кто импортирует эти классы, и заменить на `WindowSession`/`Retry`.

### 5.2 Дублирование в справке

- В `PrintHelp()` (`Program.cs:466-480`) `cv-match`/`cv-click`/`cv-templates` перечислены дважды.
- Убрать дублирующиеся строки.

### 5.3 `LastOutput`

- `Program.cs:271`: `Console.Out?.ToString()` возвращает имя типа `TextWriter`.
- Исправить: захватывать реальный вывод команды через `StringWriter`.

### 5.4 Auto-screenshot base64 в stderr

- `Program.cs:286-291`: при ненулевом exit-коде пишет base64 в stderr (сотни КБ).
- Вместо этого писать PNG в файл (`screenshots/error_{timestamp}.png`) и выводить путь.

### 5.5 LICENSE

- Добавить файл `LICENSE` с полным текстом MIT и именем правообладателя.

---

## Порядок выполнения

```
Фаза 0 (подготовка)
  └── ветка: refactor/prep
Фаза 1 (headless + watchdog)
  └── ветка: refactor/phase-1
Фаза 2 (headless-клик)
  └── ветка: refactor/phase-2
Фаза 3 (безопасность)
  └── ветка: refactor/phase-3
Фаза 4 (корректность)
  └── ветка: refactor/phase-4
Фаза 5 (гигиена)
  └── ветка: refactor/phase-5
```

Каждая фаза может быть применена независимо (нет жёстких зависимостей между фазами 1-5, только внутри фазы). После каждой фазы — сборка (`dotnet build`) и smoke-тест.
