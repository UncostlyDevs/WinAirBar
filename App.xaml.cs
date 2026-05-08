using System.Windows;
using System.IO;
using WpfApplication = System.Windows.Application;
using FloatingTaskbarMenu.Core;
using FloatingTaskbarMenu.Windows;

namespace FloatingTaskbarMenu;

public partial class App : WpfApplication
{
    private GlobalMouseHook? _mouseHook;
    private WindowManager? _windowManager;
    private SettingsService? _settingsService;
    private TaskbarMenuWindow? _menuWindow;
    private SettingsWindow? _settingsWindow;
    private SplashWindow? _splashWindow;
    private DateTime _splashShownAt;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private bool _isInitialized = false;
    private readonly string _logFilePath = string.Empty;
    private const int SplashMinimumDisplayMs = 1800;

    public App()
    {
        try
        {
            var logDirectory = AppIdentity.AppDataDirectory;
            Directory.CreateDirectory(logDirectory);
            _logFilePath = Path.Combine(logDirectory, "app_debug.log");
            Log("App constructor called");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create log: {ex.Message}");
        }
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

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            Log("OnStartup called");
            base.OnStartup(e);
            ShowSplashWindow();

            // Create hidden main window first (keeps app alive, minimal overhead)
            var mainWindow = new MainWindow();
            mainWindow.Show();
            Log("Main window created and shown");

            // Load settings asynchronously
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    Log("Starting async settings load");
                    _settingsService = new SettingsService();
                    _settingsService.Load();
                    Log("Settings loaded");

                    Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            Log("Creating WindowManager");
                            new ThemeService().ApplyThemeResources(_settingsService.Settings);
                            _windowManager = new WindowManager();

                            Log("Creating GlobalMouseHook");
                            _mouseHook = new GlobalMouseHook();
                            _mouseHook.LongPressDurationMs = _settingsService.Settings.LongPressDurationMs;
                            _mouseHook.TriggerButton = _settingsService.Settings.TriggerButton;
                            _mouseHook.LongPressDetected += OnLongPressDetected;
                            Log("Starting mouse hook");
                            _mouseHook.Start();

                            Log("Creating notify icon");
                            CreateNotifyIcon();
                            _isInitialized = true;
                            Log("Initialization complete");
                            CloseSplashWindow();
                        }
                        catch (Exception ex)
                        {
                            Log($"Exception in dispatcher init: {ex.Message}");
                            Log($"Stack trace: {ex.StackTrace}");
                            CloseSplashWindow();
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log($"Exception in async load: {ex.Message}");
                    Log($"Stack trace: {ex.StackTrace}");
                    // Fallback to synchronous if async fails
                    Dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            Log("Fallback to synchronous init");
                            _settingsService = new SettingsService();
                            _settingsService.Load();
                            new ThemeService().ApplyThemeResources(_settingsService.Settings);
                            _windowManager = new WindowManager();
                            _mouseHook = new GlobalMouseHook();
                            _mouseHook.LongPressDurationMs = _settingsService.Settings.LongPressDurationMs;
                            _mouseHook.TriggerButton = _settingsService.Settings.TriggerButton;
                            _mouseHook.LongPressDetected += OnLongPressDetected;
                            _mouseHook.Start();
                            CreateNotifyIcon();
                            _isInitialized = true;
                            Log("Fallback initialization complete");
                            CloseSplashWindow();
                        }
                        catch (Exception ex2)
                        {
                            Log($"Exception in fallback init: {ex2.Message}");
                            Log($"Stack trace: {ex2.StackTrace}");
                            CloseSplashWindow();
                        }
                    });
                }
            });
        }
        catch (Exception ex)
        {
            Log($"Exception in OnStartup: {ex.Message}");
            Log($"Stack trace: {ex.StackTrace}");
            CloseSplashWindow();
        }
    }

    private void ShowSplashWindow()
    {
        try
        {
            _splashWindow = new SplashWindow();
            _splashShownAt = DateTime.UtcNow;
            _splashWindow.Show();
            Log("Splash window shown");
        }
        catch (Exception ex)
        {
            Log($"Exception showing splash window: {ex.Message}");
            _splashWindow = null;
        }
    }

    private void CloseSplashWindow()
    {
        try
        {
            if (_splashWindow == null)
                return;

            var elapsedMs = (DateTime.UtcNow - _splashShownAt).TotalMilliseconds;
            var remainingMs = Math.Max(0, SplashMinimumDisplayMs - elapsedMs);
            if (remainingMs <= 0)
            {
                CloseSplashWindowNow();
                return;
            }

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(remainingMs)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                CloseSplashWindowNow();
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            Log($"Exception closing splash window: {ex.Message}");
            CloseSplashWindowNow();
        }
    }

    private void CloseSplashWindowNow()
    {
        try
        {
            _splashWindow?.Close();
            Log("Splash window closed");
        }
        catch { }
        finally
        {
            _splashWindow = null;
        }
    }

    private void CreateNotifyIcon()
    {
        try
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = LoadApplicationIcon(),
                Text = AppIdentity.ProductName,
                Visible = true
            };

            var contextMenu = new System.Windows.Forms.ContextMenuStrip();
            
            var settingsItem = new System.Windows.Forms.ToolStripMenuItem("Settings");
            settingsItem.Click += (s, e) => OpenSettingsWindow();
            contextMenu.Items.Add(settingsItem);

            var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => Shutdown();
            contextMenu.Items.Add(exitItem);

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => OpenSettingsWindow();
        }
        catch
        {
            // Tray icon is optional
        }
    }

    private static System.Drawing.Icon LoadApplicationIcon()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
            return System.Drawing.Icon.ExtractAssociatedIcon(processPath) ?? System.Drawing.SystemIcons.Application;

        return System.Drawing.SystemIcons.Application;
    }

    private void OpenSettingsWindow()
    {
        try
        {
            Log("Opening settings window");
            if (_settingsWindow == null)
            {
                Log("Creating new SettingsWindow");
                _settingsWindow = new SettingsWindow(_settingsService!);
                _settingsWindow.SettingsApplied += OnSettingsApplied;
                _settingsWindow.Closed += (s, args) => _settingsWindow = null;
            }
            Log("Showing settings window");
            _settingsWindow.Show();
            _settingsWindow.Activate();
            Log("Settings window opened successfully");
        }
        catch (Exception ex)
        {
            Log($"Exception opening settings window: {ex.Message}");
            Log($"Stack trace: {ex.StackTrace}");
            try { _settingsWindow?.Close(); } catch { }
            _settingsWindow = null;
        }
    }

    private void OnSettingsApplied(object? sender, EventArgs e)
    {
        // Apply new settings to running components
        if (_mouseHook != null)
        {
            _mouseHook.LongPressDurationMs = _settingsService!.Settings.LongPressDurationMs;
            _mouseHook.TriggerButton = _settingsService.Settings.TriggerButton;
        }
    }

    private void OnLongPressDetected(object? sender, EventArgs e)
    {
        try
        {
            Log("OnLongPressDetected called");
            
            if (!_isInitialized || _windowManager == null || _settingsService == null)
            {
                Log($"Not initialized: _isInitialized={_isInitialized}, _windowManager={_windowManager != null}, _settingsService={_settingsService != null}");
                return;
            }

            Log("Starting to create menu window");
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    Log("Dispatcher invoked - creating fresh menu window");
                    
                    // Always create a fresh menu window to avoid stale state
                    Log("Closing existing menu window if any");
                    _menuWindow?.Close();
                    _menuWindow = null;

                    Log("Creating TaskbarMenuWindow");
                    _menuWindow = new TaskbarMenuWindow(_windowManager!, _settingsService!);
                    Log("TaskbarMenuWindow created");
                    _menuWindow.Closed += (s, args) => _menuWindow = null;
                    
                    Log("Refreshing windows");
                    _menuWindow.RefreshWindows();
                    Log("Windows refreshed");
                    
                    Log("Positioning at cursor");
                    _menuWindow.PositionAtCursor();
                    Log("Positioned at cursor");
                    
                    Log("Showing window");
                    _menuWindow.Show();
                    Log("Window shown");
                    
                    Log("Activating window");
                    _menuWindow.Activate();
                    Log("Window activated");
                    
                    Log("Menu window shown successfully");
                }
                catch (Exception ex)
                {
                    Log($"Exception in dispatcher: {ex.Message}");
                    Log($"Stack trace: {ex.StackTrace}");
                    try { _menuWindow?.Close(); } catch { }
                    _menuWindow = null;
                }
            });
        }
        catch (Exception ex)
        {
            Log($"Exception in OnLongPressDetected: {ex.Message}");
            Log($"Stack trace: {ex.StackTrace}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mouseHook?.Stop();
        _mouseHook?.Dispose();
        _notifyIcon?.Dispose();
        _settingsService?.Save();
        base.OnExit(e);
    }
}
