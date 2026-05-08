using System.Runtime.InteropServices;
using System.IO;
using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Core;

public class GlobalMouseHook : IDisposable
{
    private delegate nint LowLevelMouseProc(int nCode, nint wParam, nint lParam);

    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207;
    private const int WM_MBUTTONUP = 0x0208;

    private nint _hookId = nint.Zero;
    private LowLevelMouseProc? _hookProc;
    private DateTime? _pressStartTime;
    private int _longPressDurationMs = 600;
    private MouseButton _triggerButton = MouseButton.Left;
    private bool _isHooked;
    private int _buttonDownMsg;
    private int _buttonUpMsg;
    private volatile bool _fired;
    private readonly string _logFilePath;

    public event EventHandler? LongPressDetected;

    public GlobalMouseHook()
    {
        var logDirectory = AppIdentity.AppDataDirectory;
        Directory.CreateDirectory(logDirectory);
        _logFilePath = Path.Combine(logDirectory, "hook_debug.log");
    }

    private void Log(string message)
    {
        try
        {
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
        }
        catch { }
    }

    public int LongPressDurationMs
    {
        get => _longPressDurationMs;
        set => _longPressDurationMs = value;
    }

    public MouseButton TriggerButton
    {
        get => _triggerButton;
        set
        {
            _triggerButton = value;
            UpdateButtonMessages();
        }
    }

    public void Start()
    {
        if (_isHooked) return;
        UpdateButtonMessages();
        _hookProc = HookCallback;
        _hookId = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, GetModuleHandle(null), 0);
        
        if (_hookId == nint.Zero)
        {
            var error = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            Log($"Failed to set hook. Error: {error}");
        }
        else
        {
            Log($"Hook started successfully. Trigger button: {_triggerButton}, Duration: {_longPressDurationMs}ms");
        }
        
        _isHooked = true;
    }

    public void Stop()
    {
        if (!_isHooked) return;
        UnhookWindowsHookEx(_hookId);
        _hookId = nint.Zero;
        _hookProc = null;
        _pressStartTime = null;
        _isHooked = false;
        Log("Hook stopped");
    }

    private void UpdateButtonMessages()
    {
        _buttonDownMsg = _triggerButton switch
        {
            MouseButton.Left => WM_LBUTTONDOWN,
            MouseButton.Right => WM_RBUTTONDOWN,
            MouseButton.Middle => WM_MBUTTONDOWN,
            _ => WM_LBUTTONDOWN
        };
        _buttonUpMsg = _triggerButton switch
        {
            MouseButton.Left => WM_LBUTTONUP,
            MouseButton.Right => WM_RBUTTONUP,
            MouseButton.Middle => WM_MBUTTONUP,
            _ => WM_LBUTTONUP
        };
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            int wParamInt = (int)wParam;

            if (wParamInt == _buttonDownMsg)
            {
                _pressStartTime = DateTime.UtcNow;
                _fired = false;
                Log($"Button down detected: {_triggerButton}");
            }
            else if (wParamInt == _buttonUpMsg && _pressStartTime.HasValue && !_fired)
            {
                var pressDuration = (DateTime.UtcNow - _pressStartTime.Value).TotalMilliseconds;
                Log($"Button up detected. Duration: {pressDuration}ms, Required: {_longPressDurationMs}ms");
                
                if (pressDuration >= _longPressDurationMs)
                {
                    _fired = true;
                    var handler = LongPressDetected;
                    if (handler != null)
                    {
                        try 
                        { 
                            Log("Firing LongPressDetected event");
                            handler(this, EventArgs.Empty); 
                        }
                        catch { }
                    }
                }
                _pressStartTime = null;
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        Stop();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint GetModuleHandle(string? lpModuleName);
}
