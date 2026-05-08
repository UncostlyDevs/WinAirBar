using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using FloatingTaskbarMenu.Models;
using Vanara.PInvoke;
using static Vanara.PInvoke.User32;

namespace FloatingTaskbarMenu.Core;

public class WindowManager
{
    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> _processNameCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, string> _processPathCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, System.Windows.Media.Imaging.BitmapSource?> _iconCache = new();

    private const int WM_GETICON = 0x007F;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;
    private const int ICON_SMALL2 = 2;
    private const int GCL_HICONSM = -34;

    /// <summary>
    /// Pre-warm process name cache on a background thread to reduce first-open lag.
    /// </summary>
    public void PreWarmCache()
    {
        try
        {
            var procs = Process.GetProcesses();
            foreach (var p in procs)
            {
                try
                {
                    _processNameCache[p.Id] = p.ProcessName;
                    p.Dispose();
                }
                catch { }
            }
        }
        catch { }
    }

    public List<WindowInfo> GetWindows()
    {
        try
        {
            var windows = new List<WindowInfo>();
            var currentProcessId = Environment.ProcessId;
            
            EnumWindows((hWnd, lParam) =>
            {
                try
                {
                    if (!IsWindowVisible(hWnd)) return true;
                    
                    var title = GetWindowText(hWnd);
                    if (string.IsNullOrEmpty(title) || title.Length > 100) return true;

                    // Skip tool windows
                    int exStyle = GetWindowLong(hWnd, -20); // GWL_EXSTYLE
                    if ((exStyle & 0x00000080) != 0) return true; // WS_EX_TOOLWINDOW

                    GetWindowThreadProcessId(hWnd, out int pid);
                    if (pid == currentProcessId) return true;

                    var processName = GetProcessName(pid);

                    // Skip system/shell processes
                    if (string.IsNullOrEmpty(processName)) return true;
                    if (ShouldSkipWindow(processName, title)) return true;

                    var executablePath = GetProcessExecutablePath(pid);
                    var icon = GetIconForProcess(hWnd, pid);
                    
                    windows.Add(new WindowInfo
                    {
                        Handle = hWnd,
                        Title = title,
                        ProcessName = processName,
                        ExecutablePath = executablePath,
                        ProcessId = pid,
                        Icon = icon
                    });
                }
                catch { }
                return true;
            }, nint.Zero);

            return windows;
        }
        catch
        {
            return new List<WindowInfo>();
        }
    }

    private bool ShouldSkipWindow(string processName, string title)
    {
        if (processName.Equals("WinAirBar", StringComparison.OrdinalIgnoreCase) ||
            processName.Equals("AirBar", StringComparison.OrdinalIgnoreCase) ||
            processName.Equals("FloatingTaskbarMenu", StringComparison.OrdinalIgnoreCase))
            return true;

        // Windows Settings often appears as an ApplicationFrameHost shell wrapper.
        if (processName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) &&
            title.Contains("Settings", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public void ActivateWindow(nint hWnd)
    {
        if (IsIconic(hWnd))
        {
            ShowWindow(hWnd, ShowWindowCommand.SW_RESTORE);
        }
        SetForegroundWindow(hWnd);
    }

    public void MinimizeWindow(nint hWnd)
    {
        ShowWindow(hWnd, ShowWindowCommand.SW_SHOWMINIMIZED);
    }

    public void MaximizeWindow(nint hWnd)
    {
        ShowWindow(hWnd, ShowWindowCommand.SW_SHOWMAXIMIZED);
    }

    public void CloseWindow(nint hWnd)
    {
        SendMessage(hWnd, 0x0010, 0, 0); // WM_CLOSE
    }

    private string GetWindowText(nint hWnd)
    {
        int length = GetWindowTextLength(hWnd);
        if (length == 0) return "";

        var text = new System.Text.StringBuilder(length + 1);
        GetWindowText(hWnd, text, length + 1);
        return text.ToString();
    }

    private string GetProcessName(int processId)
    {
        if (_processNameCache.TryGetValue(processId, out var cachedName))
            return cachedName;

        try
        {
            using var process = Process.GetProcessById(processId);
            var name = process.ProcessName;
            _processNameCache[processId] = name;
            return name;
        }
        catch
        {
            return "";
        }
    }

    private string GetProcessExecutablePath(int processId)
    {
        if (_processPathCache.TryGetValue(processId, out var cachedPath))
            return cachedPath;

        try
        {
            using var process = Process.GetProcessById(processId);
            var path = process.MainModule?.FileName ?? "";
            _processPathCache[processId] = path;
            return path;
        }
        catch
        {
            _processPathCache[processId] = "";
            return "";
        }
    }

    /// <summary>
    /// Fast icon lookup using WM_GETICON. Caches result per process ID.
    /// Never calls Process.GetProcessById or File.Exists.
    /// </summary>
    private BitmapSource? GetIconForProcess(nint hWnd, int pid)
    {
        if (_iconCache.TryGetValue(pid, out var cached))
            return cached;

        var result = FetchIconFromWindow(hWnd);
        _iconCache[pid] = result;
        return result;
    }

    private BitmapSource? FetchIconFromWindow(nint hWnd)
    {
        try
        {
            // 1. WM_GETICON ICON_SMALL2 (best quality small icon)
            var hIcon = SendMessage(hWnd, WM_GETICON, ICON_SMALL2, 0);
            if (hIcon == nint.Zero)
                hIcon = SendMessage(hWnd, WM_GETICON, ICON_SMALL, 0);
            if (hIcon == nint.Zero)
                hIcon = SendMessage(hWnd, WM_GETICON, ICON_BIG, 0);

            // 2. Fallback to class icon
            if (hIcon == nint.Zero)
                hIcon = GetClassLongPtr(hWnd, GCL_HICONSM);
            if (hIcon == nint.Zero)
                hIcon = GetClassLongPtr(hWnd, -14); // GCL_HICON

            if (hIcon != nint.Zero)
            {
                var bmp = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bmp.Freeze();
                return bmp;
            }
        }
        catch { }

        return null;
    }


    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(nint hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowTextLength(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, ShowWindowCommand nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, uint Msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(nint hWnd, out int lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern nint GetProp(nint hWnd, string lpString);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern nint GetClassLongPtr(nint hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(nint hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    private enum ShowWindowCommand : int
    {
        SW_HIDE = 0,
        SW_SHOWNORMAL = 1,
        SW_SHOWMINIMIZED = 2,
        SW_SHOWMAXIMIZED = 3,
        SW_SHOWNOACTIVATE = 4,
        SW_RESTORE = 9,
        SW_SHOWDEFAULT = 10,
    }

    enum WINDOW_STYLE : uint
    {
        WS_POPUP = 0x80000000,
        WS_CAPTION = 0x00C00000,
    }

    enum WINDOW_EX_STYLE : uint
    {
        WS_EX_TOOLWINDOW = 0x00000080,
    }

    enum GetWindowLongFlags : int
    {
        GWL_STYLE = -16,
        GWL_EXSTYLE = -20,
    }
}
