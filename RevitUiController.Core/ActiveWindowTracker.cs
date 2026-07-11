using System.Diagnostics;
using System.IO;
using UiController.Core.Models;
using static UiController.Core.NativeMethods;

namespace UiController.Core;

public class ActiveWindowTracker : IDisposable
{
    private IntPtr _hook;
    private WinEventDelegate? _delegate;
    private readonly object _lock = new();

    public event Action<WindowInfo?>? ForegroundChanged;
    public WindowInfo? CurrentForeground { get; private set; }

    public ActiveWindowTracker()
    {
        StartHook();
    }

    private void StartHook()
    {
        _delegate = OnWinEvent;
        _hook = SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_OBJECT_LOCATIONCHANGE,
            IntPtr.Zero, _delegate,
            0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hWnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hWnd == IntPtr.Zero) return;
        UpdateForeground(hWnd);
    }

    private void UpdateForeground(IntPtr hWnd)
    {
        if (!IsWindowVisible(hWnd)) return;
        var info = WindowFromHandle(hWnd);
        if (info == null) return;

        lock (_lock)
        {
            if (CurrentForeground?.Hwnd == hWnd) return;
            CurrentForeground = info;
        }
        ForegroundChanged?.Invoke(info);
    }

    public static WindowInfo? WindowFromHandle(IntPtr hWnd)
    {
        try
        {
            var title = GetWindowTitle(hWnd);
            var pid = (int)GetProcessId(hWnd);
            var procName = "";
            try
            {
                var p = Process.GetProcessById(pid);
                procName = p.ProcessName;
            }
            catch { }

            var rect = new RECT();
            GetWindowRect(hWnd, out rect);

            var mi = NativeMethods.GetMonitorInfoForWindow(hWnd);
            int monitorIndex = 0;

            return new WindowInfo
            {
                Hwnd = hWnd,
                Title = title,
                Pid = pid,
                ProcessName = procName,
                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height,
                MonitorIndex = monitorIndex,
                MonitorName = mi.szDevice.TrimEnd('\0'),
                IsForeground = hWnd == GetForegroundWindow(),
                IsMinimized = IsIconic(hWnd),
                IsVisible = IsWindowVisible(hWnd)
            };
        }
        catch { return null; }
    }

    public List<WindowInfo> GetAllWindows()
    {
        var result = new List<WindowInfo>();
        var fgHwnd = GetForegroundWindow();

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            var title = GetWindowTitle(hWnd);
            if (string.IsNullOrEmpty(title)) return true;

            try
            {
                var pid = (int)GetProcessId(hWnd);
                var procName = "";
                try
                {
                    var p = Process.GetProcessById(pid);
                    procName = p.ProcessName;
                }
                catch { }

                var rect = new RECT();
                if (!GetWindowRect(hWnd, out rect)) return true;

                var mi = GetMonitorInfoForWindow(hWnd);

                result.Add(new WindowInfo
                {
                    Hwnd = hWnd,
                    Title = title,
                    Pid = pid,
                    ProcessName = procName,
                    X = rect.X,
                    Y = rect.Y,
                    Width = rect.Width,
                    Height = rect.Height,
                    MonitorIndex = 0,
                    MonitorName = mi.szDevice.TrimEnd('\0'),
                    IsForeground = hWnd == fgHwnd,
                    IsMinimized = IsIconic(hWnd),
                    IsVisible = true
                });
            }
            catch { }
            return true;
        }, IntPtr.Zero);

        return result;
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
            UnhookWinEvent(_hook);
    }
}
