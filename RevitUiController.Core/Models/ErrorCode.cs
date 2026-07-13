namespace UiController.Core.Models;

public enum ErrorCode
{
    Unknown,
    InvalidArgs,
    ElementNotFound,
    WindowNotResponding,
    DialogUnexpectedlyClosed,
    Timeout,
    AmbiguousMatch,
    ConnectionFailed,
    ProcessNotFound,
    WindowNotFound,
    AutomationFailed,
    ClickFailed,
    TypeFailed,
    ReadFailed,
    CommandNotFound,
    SafetyRejected,
    ScriptError,
    RevitApiError,
    UndoStackEmpty,
    BatchPartialFailure,
    EventStreamTimeout,
    InternalError
}
