using System.Runtime.InteropServices;
using RevitUiController.Models;

namespace RevitUiController;

public class DesktopWindowManager : IDisposable
{
    public ActiveWindowTracker ActiveTracker { get; }
    public WindowSession? CurrentSession { get; private set; }
    public WindowInfo? CurrentTargetInfo => ActiveTracker.CurrentForeground;

    public DesktopWindowManager()
    {
        ActiveTracker = new ActiveWindowTracker();
    }

    public async Task<WindowSession?> ResolveTarget(WindowQuery query, int timeoutSec = 30, CancellationToken ct = default)
    {
        CurrentSession?.Dispose();
        CurrentSession = await WindowSession.Resolve(query, timeoutSec, ct);
        return CurrentSession;
    }

    public async Task<WindowSession?> ResolveActive(CancellationToken ct = default)
    {
        CurrentSession?.Dispose();
        CurrentSession = await WindowSession.ConnectToActive(ct: ct);
        return CurrentSession;
    }

    public bool FocusWindow(IntPtr hWnd)
    {
        if (NativeMethods.IsIconic(hWnd))
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
        return NativeMethods.SetForegroundWindow(hWnd);
    }

    public bool FocusWindow(string title)
    {
        foreach (var w in ActiveTracker.GetAllWindows())
        {
            if (w.Title.Contains(title, StringComparison.OrdinalIgnoreCase))
                return FocusWindow(w.Hwnd);
        }
        return false;
    }

    public List<WindowInfo> GetAllWindows()
    {
        return ActiveTracker.GetAllWindows();
    }

    public List<MonitorInfo> GetAllMonitors()
    {
        var result = new List<MonitorInfo>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd)) return true;
            var title = NativeMethods.GetWindowTitle(hWnd);
            if (string.IsNullOrEmpty(title)) return true;
            var hMonitor = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero) return true;

            var mi = new NativeMethods.MONITORINFOEX();
            mi.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFOEX));
            mi.szDevice = new string('\0', 32);
            if (!NativeMethods.GetMonitorInfo(hMonitor, ref mi)) return true;

            var dpi = NativeMethods.GetMonitorDpi(hWnd);
            var deviceName = mi.szDevice.TrimEnd('\0');
            var idx = result.FindIndex(m => m.DeviceName == deviceName);

            if (idx < 0)
            {
                result.Add(new MonitorInfo
                {
                    Index = result.Count,
                    DeviceName = deviceName,
                    IsPrimary = (mi.dwFlags & 1) != 0,
                    X = mi.rcMonitor.X,
                    Y = mi.rcMonitor.Y,
                    Width = mi.rcMonitor.Width,
                    Height = mi.rcMonitor.Height,
                    WorkX = mi.rcWork.X,
                    WorkY = mi.rcWork.Y,
                    WorkWidth = mi.rcWork.Width,
                    WorkHeight = mi.rcWork.Height,
                    DpiScale = dpi
                });
            }
            return true;
        }, IntPtr.Zero);

        return result;
    }

    public WindowInfo? GetActiveWindowInfo()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        return hwnd != IntPtr.Zero ? ActiveWindowTracker.WindowFromHandle(hwnd) : null;
    }

    public void Dispose()
    {
        CurrentSession?.Dispose();
        ActiveTracker?.Dispose();
    }
}
