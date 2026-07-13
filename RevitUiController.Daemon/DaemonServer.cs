using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FlaUI.Core.AutomationElements;
using UiController.Core;
using UiController.Core.Models;
using UiController.Core.Protocol;

namespace UiController.Daemon;

public class DaemonServer : IDisposable
{
    private readonly string _pipeName;
    private readonly IApplicationProfile _profile;
    private WindowSession? _session;
    private AutomationElement? _mainWindow;
    private CancellationTokenSource _cts = new();
    private Task? _listenTask;
    private readonly CommandRegistry _registry = new();
    private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly EventWatcherService _eventWatcher = new();
    private AutomationEventService? _eventService;
    private volatile bool _shutdownRequested;
    private volatile int _inFlightCommands;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DaemonServer(string pipeName = "RevitUiController", IApplicationProfile? profile = null)
    {
        _pipeName = pipeName;
        _profile = profile ?? new GenericProfile("Revit");
    }

    public void RegisterCommand(ICommand cmd) => _commands[cmd.Name] = cmd;
    public bool IsConnected => _session != null && _mainWindow != null;
    public bool IsShutdownRequested => _shutdownRequested;

    public async Task StartAsync(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        LoggingService.Info("Daemon", $"Listening on pipe \\\\.\\pipe\\{_pipeName}");
        _listenTask = Task.Run(() => ListenLoop(_cts.Token), _cts.Token);
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && !_shutdownRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(
                    _pipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Message, PipeOptions.Asynchronous);

                await pipe.WaitForConnectionAsync(ct);
                LoggingService.Info("Daemon", "Client connected");

                using (pipe)
                using (var reader = new StreamReader(pipe, Encoding.UTF8))
                using (var writer = new StreamWriter(pipe, Encoding.UTF8) { AutoFlush = true })
                {
                    while (pipe.IsConnected && !ct.IsCancellationRequested && !_shutdownRequested)
                    {
                        var line = await reader.ReadLineAsync(ct);
                        if (line == null) break;

                        Interlocked.Increment(ref _inFlightCommands);
                        try
                        {
                            var response = await ProcessRequest(line, ct);
                            await writer.WriteLineAsync(response);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref _inFlightCommands);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (InvalidOperationException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                LoggingService.Error("Daemon", $"Pipe error: {ex.Message}");
                if (pipe != null)
                {
                    try { pipe.Dispose(); } catch { }
                }
                if (!ct.IsCancellationRequested && !_shutdownRequested)
                {
                    await Task.Delay(1000, ct);
                }
            }
        }
    }

    internal async Task<string> ProcessRequest(string line, CancellationToken ct)
    {
        DaemonRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<DaemonRequest>(line, JsonOptions);
        }
        catch
        {
            return JsonError(ErrorCode.InvalidArgs, "Invalid JSON request");
        }

        if (request == null)
            return JsonError(ErrorCode.InvalidArgs, "Empty request");

        if (string.IsNullOrEmpty(request.Command))
            return JsonError(ErrorCode.InvalidArgs, "Missing 'command' field");

        var cmdName = request.Command.ToLowerInvariant();

        switch (cmdName)
        {
            case "__ping":
                return JsonOk(new { status = "alive", connected = IsConnected });

            case "__shutdown":
                _shutdownRequested = true;
                _ = Task.Run(async () =>
                {
                    var deadline = DateTime.UtcNow.AddSeconds(5);
                    while (Volatile.Read(ref _inFlightCommands) > 0 && DateTime.UtcNow < deadline)
                        await Task.Delay(100);
                    _cts.Cancel();
                    Dispose();
                });
                return JsonOk(new { message = "shutting down" });

            case "__connect":
                return await HandleConnect(request);

            case "__disconnect":
                DisconnectSession();
                return JsonOk(null);

            case "__watch":
                return await HandleWatch(request, ct);

            case "__events":
                return await HandleEvents(request, ct);

            case "__batch":
                return await HandleBatch(request, ct);

            case "__undo":
                return await HandleUndo(request, ct);

            case "session-begin":
                var sessionName = request.Args?.FirstOrDefault() ?? "default";
                SessionContext.Begin(sessionName);
                return JsonOk(new { session = sessionName, started = true });

            case "session-end":
                var endedName = SessionContext.Name;
                SessionContext.End();
                return JsonOk(new { session = endedName, ended = true });

            case "session-status":
                return JsonOk(SessionContext.FullStatus());

            case "session-set":
                if (request.Args?.Count >= 2)
                {
                    SessionContext.SetVariable(request.Args[0], request.Args[1]);
                    return JsonOk(new { variable = request.Args[0], set = true });
                }
                return JsonError(ErrorCode.InvalidArgs, "Usage: <name> <value>");

            case "session-get":
                if (request.Args?.Count >= 1)
                {
                    var val = SessionContext.GetVariable(request.Args[0]);
                    return JsonOk(new { variable = request.Args[0], value = val });
                }
                return JsonError(ErrorCode.InvalidArgs, "Usage: <name>");

            case "checkpoint":
                var cpName = request.Args?.FirstOrDefault() ?? "auto_" + DateTime.UtcNow.Ticks;
                SessionContext.SetCheckpoint(cpName);
                return JsonOk(new { checkpoint = cpName, created = true });

            case "undo-last":
                return await HandleUndoLast(request, ct);

            case "undo-to":
                request.Action = "undo-to";
                return await HandleUndo(request, ct);

            case "wait-for":
                return await HandleWaitFor(request, ct);

            case "wait-close":
                return await HandleWaitClose(request, ct);

            case "wait-element":
                return await HandleWaitElement(request, ct);

            default:
                return await ExecuteCommand(cmdName, request, ct);
        }
    }

    private async Task<string> HandleConnect(DaemonRequest request)
    {
        var pid = request.Pid;
        var processName = request.ProcessName ?? _profile.ProcessName;
        var title = request.WindowTitle;
        var timeout = request.Timeout ?? 30;
        var useActive = request.UseActive;

        DisconnectSession();

        WindowSession? session = null;
        var provider = new UIA3AutomationProvider();

        try
        {
            if (useActive)
                session = await WindowSession.ConnectToActive(provider, timeout, _cts.Token);
            else if (pid.HasValue)
                session = await WindowSession.ConnectToProcess(provider, pid, processName, timeout, _cts.Token);
            else if (!string.IsNullOrEmpty(title))
                session = await WindowSession.ConnectByTitle(title, provider, timeout, _cts.Token);
            else
                session = await WindowSession.ConnectToProcess(provider, processName: processName, timeoutSec: timeout, ct: _cts.Token);
        }
        catch (Exception ex)
        {
            return JsonError(ErrorCode.ConnectionFailed, ex.Message);
        }

        if (session == null)
            return JsonError(ErrorCode.ConnectionFailed, $"Could not connect to process '{processName}'");

        _session = session;
        _mainWindow = session.MainWindow;
        SessionContext.Begin();
        SessionContext.ActiveHwnd = session.Process?.MainWindowHandle.ToInt64();
        SessionContext.ActivePid = session.Process?.Id;
        SessionContext.ActiveProcessName = session.Process?.ProcessName;

        _eventWatcher.Start(session);

        try
        {
            _eventService?.StopListening();
            _eventService?.Dispose();
            _eventService = new AutomationEventService(session.Automation, session.MainWindow);
            _eventService.StartListening();
            LoggingService.Info("Daemon", "AutomationEventService started for event-driven waiting");
        }
        catch (Exception ex)
        {
            LoggingService.Warn("Daemon", $"Failed to start AutomationEventService: {ex.Message}. Falling back to polling.");
        }

        var diagCount = 0;
        try { diagCount = AutomationHelper.FindActiveDialogs(session.MainWindow).Count; } catch { }

        return JsonOk(new
        {
            pid = session.Process?.Id,
            processName = session.Process?.ProcessName,
            windowTitle = session.MainWindow.Name,
            openDialogs = diagCount,
            connected = true
        });
    }

    private async Task<string> HandleWatch(DaemonRequest request, CancellationToken ct)
    {
        if (!IsConnected)
            return JsonError(ErrorCode.ConnectionFailed, "Not connected. Send __connect first.");

        var condition = request.Condition ?? "found";
        var intervalMs = Math.Max(100, request.Interval ?? 500);
        var timeoutSec = request.Timeout ?? 30;
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSec);

        var subCmd = request.SubCommand ?? "";
        var subArgs = request.SubArgs?.ToArray() ?? Array.Empty<string>();

        if (!_commands.TryGetValue(subCmd, out var cmd))
            return JsonError(ErrorCode.CommandNotFound, $"Unknown sub-command: {subCmd}");

        var found = false;
        CommandResult? lastResult = null;

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            CommandResultStore.LastResult = null;
            try
            {
                var ec = await cmd.ExecuteAsync(_mainWindow!, subArgs, ct);
                lastResult = CommandResultStore.LastResult;
                found = EvaluateCondition(ec, lastResult, condition);
            }
            catch
            {
                found = condition is "gone" or "disabled";
            }
            if (found) break;
            await Task.Delay(intervalMs, ct);
        }

        return JsonSerialize(new DaemonResponse
        {
            Success = found,
            ErrorCode = found ? ErrorCode.Unknown : ErrorCode.Timeout,
            Error = found ? null : $"Condition '{condition}' not met within {timeoutSec}s",
            Data = new { condition, met = found, elapsed = timeoutSec - (deadline - DateTime.UtcNow).TotalSeconds, lastOutput = lastResult },
            StateDiff = lastResult?.Diff,
            DurationMs = lastResult?.DurationMs ?? 0
        });
    }

    private async Task<string> HandleEvents(DaemonRequest request, CancellationToken ct)
    {
        var timeoutSec = request.Timeout ?? 30;
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSec);
        var events = new List<object>();

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (_eventWatcher.TryDequeue(out var evt))
            {
                events.Add(new { type = evt.Type, title = evt.Title, text = evt.Text, timestamp = evt.Timestamp });
                if (events.Count >= (request.MaxEvents ?? 10)) break;
            }
            else
            {
                await Task.Delay(100, ct);
            }
        }

        return JsonOk(new { events, total = events.Count, remaining = _eventWatcher.Count });
    }

    private async Task<string> HandleBatch(DaemonRequest request, CancellationToken ct)
    {
        if (request.Commands == null || request.Commands.Count == 0)
            return JsonError(ErrorCode.InvalidArgs, "Batch requires a 'commands' array");

        if (!IsConnected)
            return JsonError(ErrorCode.ConnectionFailed, "Not connected. Send __connect first.");

        var results = new List<DaemonResponse>();
        bool previousSuccess = true;
        bool overallSuccess = true;

        foreach (var sub in request.Commands)
        {
            if (string.IsNullOrEmpty(sub.Command)) continue;

            // Check 'if' condition
            if (!string.IsNullOrEmpty(sub.If))
            {
                bool conditionMet = sub.If switch
                {
                    "previous_success" => previousSuccess,
                    "previous_failed" => !previousSuccess,
                    _ => true
                };
                if (!conditionMet)
                {
                    results.Add(new DaemonResponse
                    {
                        Success = true,
                        Command = sub.Command,
                        Data = new { skipped = true, reason = $"condition '{sub.If}' not met" }
                    });
                    continue;
                }
            }

            // Check 'onlyIf' condition
            if (sub.OnlyIf != null)
            {
                bool uiConditionMet = await CheckOnlyIfCondition(sub.OnlyIf, ct);
                if (!uiConditionMet)
                {
                    results.Add(new DaemonResponse
                    {
                        Success = true,
                        Command = sub.Command,
                        Data = new { skipped = true, reason = "onlyIf condition not met" }
                    });
                    continue;
                }
            }

            CommandResultStore.LastResult = null;
            try
            {
                var responseJson = await ExecuteCommand(sub.Command.ToLowerInvariant(), sub, ct);
                var response = JsonSerializer.Deserialize<DaemonResponse>(responseJson, JsonOptions);
                if (response != null)
                {
                    results.Add(response);
                    previousSuccess = response.Success;
                    if (!response.Success)
                    {
                        overallSuccess = false;
                        var onError = sub.OnError ?? "stop";
                        if (onError == "stop") break;
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add(new DaemonResponse { Success = false, ErrorCode = ErrorCode.InternalError, Error = ex.Message, Command = sub.Command });
                previousSuccess = false;
                overallSuccess = false;
                var onError = sub.OnError ?? "stop";
                if (onError == "stop") break;
            }
        }

        return JsonSerialize(new DaemonResponse
        {
            Success = overallSuccess,
            ErrorCode = overallSuccess ? ErrorCode.Unknown : ErrorCode.BatchPartialFailure,
            Data = new { results, total = results.Count, succeeded = results.Count(r => r.Success) }
        });
    }

    private async Task<bool> CheckOnlyIfCondition(OnlyIfCondition condition, CancellationToken ct)
    {
        if (condition.Dialog != null && _mainWindow != null)
        {
            var exists = AutomationHelper.FindFirstEnabledVisible(_mainWindow, condition.Dialog) != null;
            return condition.Exists == null || condition.Exists == exists;
        }
        if (condition.Element != null && _mainWindow != null)
        {
            var el = AutomationHelper.FindFirstEnabledVisible(_mainWindow, condition.Element);
            var enabled = el?.IsEnabled ?? false;
            return condition.Enabled == null || condition.Enabled == enabled;
        }
        return true;
    }

    private async Task<string> HandleUndo(DaemonRequest request, CancellationToken ct)
    {
        if (!IsConnected)
            return JsonError(ErrorCode.ConnectionFailed, "Not connected");

        var client = new RevitUiController.Revit.PipeBridgeClient();
        if (!client.Connect(2000))
            return JsonError(ErrorCode.RevitApiError, "Revit API pipe not available");

        if (request.Action == "status")
        {
            var result = client.SendCommand("undo_stack", new { });
            return result != null
                ? JsonOk(result)
                : JsonError(ErrorCode.RevitApiError, "Undo stack query failed");
        }

        if (request.Action is "undo" or "rollback")
        {
            var count = request.Count ?? 1;
            var result = client.SendCommand("undo", new { count });
            if (result == null)
                return JsonError(ErrorCode.RevitApiError, "Undo command failed");
            SessionContext.RemoveLastCommands(count);
            return JsonOk(new { undoCount = count, result });
        }

        if (request.Action == "checkpoint")
        {
            var cpName = request.Args?.FirstOrDefault() ?? "auto_" + DateTime.UtcNow.Ticks;
            SessionContext.SetCheckpoint(cpName);
            return JsonOk(new { checkpoint = cpName, created = true });
        }

        if (request.Action == "undo-to")
        {
            var cpName = request.Args?.FirstOrDefault();
            if (string.IsNullOrEmpty(cpName))
                return JsonError(ErrorCode.InvalidArgs, "Checkpoint name required for undo-to action");
            var cpIndex = SessionContext.GetCheckpointIndex(cpName);
            if (cpIndex == null)
                return JsonError(ErrorCode.InvalidArgs, $"Checkpoint '{cpName}' not found");
            var historyCount = SessionContext.CommandHistory.Count;
            var stepsToUndo = historyCount - cpIndex.Value;
            if (stepsToUndo <= 0)
                return JsonOk(new { checkpoint = cpName, undone = 0, message = "Already at or before checkpoint" });
            var result = client.SendCommand("undo", new { count = stepsToUndo });
            if (result == null)
                return JsonError(ErrorCode.RevitApiError, "Undo command failed");
            SessionContext.RemoveLastCommands(stepsToUndo);
            return JsonOk(new { checkpoint = cpName, undone = stepsToUndo, result });
        }

        return JsonError(ErrorCode.InvalidArgs, "Undo action must be 'status', 'undo', 'checkpoint', or 'undo-to'");
    }

    private async Task<string> HandleUndoLast(DaemonRequest request, CancellationToken ct)
    {
        if (!IsConnected)
            return JsonError(ErrorCode.ConnectionFailed, "Not connected");

        var count = request.Count ?? 1;
        if (count <= 0)
            return JsonError(ErrorCode.InvalidArgs, "Count must be positive");

        var client = new RevitUiController.Revit.PipeBridgeClient();
        if (!client.Connect(2000))
            return JsonError(ErrorCode.RevitApiError, "Revit API pipe not available");

        var actualUndone = 0;
        for (int i = 0; i < count; i++)
        {
            var result = client.SendCommand("undo", new { count = 1 });
            if (result == null)
                break;
            actualUndone++;
        }

        SessionContext.RemoveLastCommands(actualUndone);

        return JsonOk(new { undone = actualUndone, requested = count, partial = actualUndone < count });
    }

    private async Task<string> HandleWaitFor(DaemonRequest request, CancellationToken ct)
    {
        if (!IsConnected)
            return JsonError(ErrorCode.ConnectionFailed, "Not connected. Send __connect first.");

        var args = request.Args?.ToArray() ?? Array.Empty<string>();
        var title = args.Length > 0 ? args[0] : "";
        var timeoutMs = (request.Timeout ?? 30) * 1000;

        if (string.IsNullOrEmpty(title))
            return JsonError(ErrorCode.InvalidArgs, "Missing dialog title argument");

        AutomationElement? dialog;

        if (_eventService is { IsListening: true })
        {
            LoggingService.Info("Daemon", $"wait-for: waiting for dialog '{title}' (event-driven, timeout={timeoutMs}ms)");
            dialog = await _eventService.WaitForDialogAsync(title, timeoutMs, ct);
        }
        else
        {
            LoggingService.Info("Daemon", $"wait-for: waiting for dialog '{title}' (polling fallback, timeout={timeoutMs}ms)");
            dialog = await Retry.WaitForDialog(_mainWindow!, title, timeoutMs, ct: ct);
        }

        if (dialog != null)
            return JsonOk(new { dialog = dialog.Name ?? title, appeared = true, method = _eventService?.IsListening == true ? "event" : "polling" });

        return JsonError(ErrorCode.Timeout, $"Dialog '{title}' did not appear within {timeoutMs / 1000}s");
    }

    private async Task<string> HandleWaitClose(DaemonRequest request, CancellationToken ct)
    {
        if (!IsConnected)
            return JsonError(ErrorCode.ConnectionFailed, "Not connected. Send __connect first.");

        var args = request.Args?.ToArray() ?? Array.Empty<string>();
        var title = args.Length > 0 ? args[0] : "";
        var timeoutMs = (request.Timeout ?? 30) * 1000;

        if (string.IsNullOrEmpty(title))
            return JsonError(ErrorCode.InvalidArgs, "Missing dialog title argument");

        bool closed;

        if (_eventService is { IsListening: true })
        {
            LoggingService.Info("Daemon", $"wait-close: waiting for dialog '{title}' to close (event-driven, timeout={timeoutMs}ms)");
            closed = await _eventService.WaitForDialogCloseAsync(title, timeoutMs, ct);
        }
        else
        {
            LoggingService.Info("Daemon", $"wait-close: waiting for dialog '{title}' to close (polling fallback, timeout={timeoutMs}ms)");
            closed = await Retry.WaitForDialogClose(_mainWindow!, title, timeoutMs, ct: ct);
        }

        if (closed)
            return JsonOk(new { title, closed = true, method = _eventService?.IsListening == true ? "event" : "polling" });

        return JsonError(ErrorCode.Timeout, $"Dialog '{title}' did not close within {timeoutMs / 1000}s");
    }

    private async Task<string> HandleWaitElement(DaemonRequest request, CancellationToken ct)
    {
        if (!IsConnected)
            return JsonError(ErrorCode.ConnectionFailed, "Not connected. Send __connect first.");

        var args = request.Args?.ToArray() ?? Array.Empty<string>();
        var name = args.Length > 0 ? args[0] : "";
        var timeoutMs = (request.Timeout ?? 30) * 1000;

        if (string.IsNullOrEmpty(name))
            return JsonError(ErrorCode.InvalidArgs, "Missing element name argument");

        AutomationElement? element;

        if (_eventService is { IsListening: true })
        {
            LoggingService.Info("Daemon", $"wait-element: waiting for element '{name}' (event-driven, timeout={timeoutMs}ms)");
            element = await _eventService.WaitForElementAsync(name, timeoutMs, ct);
        }
        else
        {
            LoggingService.Info("Daemon", $"wait-element: waiting for element '{name}' (polling fallback, timeout={timeoutMs}ms)");
            element = await Retry.WaitForElement(_mainWindow!, name, timeoutMs, ct: ct);
        }

        if (element != null)
            return JsonOk(new { element = element.Name ?? name, found = true, method = _eventService?.IsListening == true ? "event" : "polling" });

        var similar = _mainWindow != null ? AutomationHelper.FindSimilarElementNames(_mainWindow, name) : new();
        var suggestions = new List<string>
        {
            "Try 'ai-find \"" + name + "\"' for multi-strategy search",
            "Try 'list-controls' to see available elements",
            "Check locale: '" + name + "' in Russian may be localized"
        };
        if (similar.Count > 0)
            suggestions.Add("Similar elements: " + string.Join(", ", similar.Take(3)));
        var errorInfo = new SelfDescribingError
        {
            Code = ErrorCode.Timeout,
            CodeString = ErrorCode.Timeout.ToString(),
            Query = name,
            Suggestions = suggestions,
            AvailableElements = similar.Count > 0 ? similar : null
        };
        return JsonError(ErrorCode.Timeout, $"Element '{name}' did not appear within {timeoutMs / 1000}s", errorInfo);
    }

    private async Task<string> ExecuteCommand(string cmdName, DaemonRequest request, CancellationToken ct)
    {
        if (!IsConnected && cmdName is not ("session-begin" or "session-end" or "session-status"))
            return JsonError(ErrorCode.ConnectionFailed, "Not connected. Send __connect first.");

        if (!_commands.TryGetValue(cmdName, out var cmd))
            return JsonError(ErrorCode.CommandNotFound, $"Unknown command: {cmdName}");

        var args = request.Args?.ToArray() ?? Array.Empty<string>();

        try
        {
            CommandResultStore.LastResult = null;
            CommandResultStore.CurrentRequest = request;
            var exitCode = await cmd.ExecuteAsync(_mainWindow!, args, ct);
            var cmdResult = CommandResultStore.LastResult;

            SessionContext.RecordCommand(cmdName, request.Args, cmdResult?.Success ?? exitCode == 0, cmdResult?.DurationMs ?? 0);

            if (cmdResult?.Diff != null)
            {
                SessionContext.ActiveDialog = cmdResult.Diff.ActiveDialog;
                foreach (var d in cmdResult.Diff.NewDialogs)
                    SessionContext.PushDialog(d);
                foreach (var d in cmdResult.Diff.ClosedDialogs)
                    if (SessionContext.DialogStack.Count > 0)
                        SessionContext.PopDialog();
            }

            if (DaemonSettings.AutoScreenshot && cmdResult != null && string.IsNullOrEmpty(cmdResult.Screenshot) && _mainWindow != null)
            {
                cmdResult.Screenshot = CaptureAutoScreenshot(cmdResult);
            }

            return JsonSerialize(new DaemonResponse
            {
                Success = cmdResult?.Success ?? exitCode == 0,
                ErrorCode = ParseErrorCode(cmdResult?.ErrorInfo?.CodeString),
                Error = cmdResult?.Error,
                ErrorInfo = cmdResult?.ErrorInfo,
                Data = cmdResult?.Data,
                Command = cmdName,
                StateDiff = cmdResult?.Diff,
                Screenshot = cmdResult?.Screenshot,
                DurationMs = cmdResult?.DurationMs ?? 0
            });
        }
        catch (OperationCanceledException)
        {
            return JsonError(ErrorCode.Timeout, "Command cancelled");
        }
        catch (Exception ex)
        {
            return JsonError(ErrorCode.InternalError, ex.Message);
        }
        finally
        {
            if (_session != null && _mainWindow != null)
            {
                try
                {
                    var dialogs = AutomationHelper.FindActiveDialogs(_mainWindow);
                    _eventWatcher.ScanDialogs(dialogs);
                }
                catch { }
            }
        }
    }

    private static bool EvaluateCondition(int exitCode, CommandResult? result, string condition)
    {
        return condition.ToLowerInvariant() switch
        {
            "found" or "enabled" => exitCode == 0,
            "gone" or "disabled" => exitCode != 0,
            var c when c.StartsWith("text:") => result?.Data?.ToString()?.Contains(c[5..], StringComparison.OrdinalIgnoreCase) ?? false,
            _ => exitCode == 0
        };
    }

    private void DisconnectSession()
    {
        _eventWatcher.Stop();

        if (_eventService != null)
        {
            try { _eventService.StopListening(); } catch { }
            try { _eventService.Dispose(); } catch { }
            _eventService = null;
        }

        _session?.Dispose();
        _session = null;
        _mainWindow = null;
        SessionContext.End();
    }

    private static ErrorCode ParseErrorCode(string? codeString)
    {
        if (string.IsNullOrEmpty(codeString)) return ErrorCode.Unknown;
        if (Enum.TryParse<ErrorCode>(codeString, true, out var code)) return code;
        return ErrorCode.Unknown;
    }

    private string? CaptureAutoScreenshot(CommandResult cmdResult)
    {
        try
        {
            if (cmdResult.Diff?.NewDialogs is { Count: > 0 })
            {
                foreach (var dialogName in cmdResult.Diff.NewDialogs)
                {
                    try
                    {
                        var dialog = AutomationHelper.FindFirstEnabledVisible(_mainWindow!, dialogName);
                        if (dialog != null)
                        {
                            var rect = dialog.BoundingRectangle;
                            if (rect.Width > 0 && rect.Height > 0)
                            {
                                return ScreenshotHelper.CaptureBase64(
                                    (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
                            }
                        }
                    }
                    catch { }
                }
            }

            return ScreenshotHelper.CaptureWindow(_mainWindow!);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        DisconnectSession();
        _eventWatcher.Dispose();
        _eventService?.Dispose();
    }

    private string JsonOk(object? data) =>
        JsonSerialize(new DaemonResponse { Success = true, Data = data });

    private string JsonError(ErrorCode code, string message) =>
        JsonSerialize(new DaemonResponse { Success = false, ErrorCode = code, Error = message });

    private string JsonError(ErrorCode code, string message, SelfDescribingError? errorInfo) =>
        JsonSerialize(new DaemonResponse { Success = false, ErrorCode = code, Error = message, ErrorInfo = errorInfo });

    private string JsonSerialize(DaemonResponse response) =>
        JsonSerializer.Serialize(response, JsonOptions);
}
