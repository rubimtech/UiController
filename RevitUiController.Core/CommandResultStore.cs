using UiController.Core.Models;
using UiController.Core.Protocol;

namespace UiController.Core;

public static class CommandResultStore
{
    private static readonly AsyncLocal<CommandResult?> _lastResult = new();

    public static CommandResult? LastResult
    {
        get => _lastResult.Value;
        set => _lastResult.Value = value;
    }

    private static readonly AsyncLocal<DaemonRequest?> _currentRequest = new();

    public static DaemonRequest? CurrentRequest
    {
        get => _currentRequest.Value;
        set => _currentRequest.Value = value;
    }
}
