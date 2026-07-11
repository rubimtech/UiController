using System.Runtime.InteropServices;
using System.Text;

namespace UiController.Core;

public static class Win32Helper
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, StringBuilder lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, string lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    private delegate bool EnumWindowProc(IntPtr hWnd, IntPtr lParam);

    private const uint WM_CLICK = 0x00F5;
    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint BM_CLICK = 0x00F5;
    private const uint WM_SETTEXT = 0x000C;
    private const uint WM_GETTEXT = 0x000D;
    private const uint WM_GETTEXTLENGTH = 0x000E;

    public static bool ClickButton(IntPtr hWnd)
    {
        return SendMessage(hWnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero) != IntPtr.Zero;
    }

    public static bool PostClick(IntPtr hWnd)
    {
        return PostMessage(hWnd, WM_LBUTTONDOWN, IntPtr.Zero, IntPtr.Zero)
            && PostMessage(hWnd, WM_LBUTTONUP, IntPtr.Zero, IntPtr.Zero);
    }

    public static string GetText(IntPtr hWnd)
    {
        var len = (int)GetWindowTextLength(hWnd);
        if (len == 0) return "";
        var sb = new StringBuilder(len + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static bool SetText(IntPtr hWnd, string text)
    {
        return SendMessage(hWnd, WM_SETTEXT, IntPtr.Zero, text) != IntPtr.Zero;
    }

    public static List<IntPtr> EnumChildWindowsList(IntPtr hWndParent)
    {
        var result = new List<IntPtr>();
        EnumWindowProc callback = (hWnd, lParam) =>
        {
            result.Add(hWnd);
            return true;
        };
        EnumChildWindows(hWndParent, callback, IntPtr.Zero);
        return result;
    }

    public static Dictionary<IntPtr, string> EnumChildWindowsWithText(IntPtr hWndParent)
    {
        var result = new Dictionary<IntPtr, string>();
        EnumWindowProc callback = (hWnd, lParam) =>
        {
            var text = GetText(hWnd);
            if (!string.IsNullOrEmpty(text))
                result[hWnd] = text;
            return true;
        };
        EnumChildWindows(hWndParent, callback, IntPtr.Zero);
        return result;
    }

    public static uint GetProcessId(IntPtr hWnd)
    {
        GetWindowThreadProcessId(hWnd, out var pid);
        return pid;
    }

    public static string GetControlText(IntPtr hWnd)
    {
        var length = (int)SendMessage(hWnd, WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero);
        if (length <= 0) return "";
        var sb = new StringBuilder(length + 1);
        SendMessage(hWnd, WM_GETTEXT, (IntPtr)(sb.Capacity), sb);
        return sb.ToString();
    }

    public static List<IntPtr> EnumChildHandles(IntPtr hWndParent)
    {
        var result = new List<IntPtr>();
        EnumChildWindows(hWndParent, (hWnd, _) => { result.Add(hWnd); return true; }, IntPtr.Zero);
        return result;
    }
}
