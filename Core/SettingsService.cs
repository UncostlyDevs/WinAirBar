using System.IO;
using System.Text.Json;
using FloatingTaskbarMenu.Models;
using Microsoft.Win32;

namespace FloatingTaskbarMenu.Core;

public class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        AppIdentity.AppDataDirectory,
        "settings.json"
    );
    private readonly BottomActionBarService _bottomActionBarService = new();

    private static readonly string AppName = AppIdentity.ProductName;
    private static readonly string LegacyAppName = AppIdentity.LegacyProductName;
    private static readonly string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public Settings Settings { get; private set; } = new Settings();

    public void Load()
    {
        try
        {
            AppIdentity.EnsureLegacyAppDataMigrated();
            MigrateLegacyAutoStart();

            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }

            _bottomActionBarService.EnsureSlots(Settings);
            Settings.CurrentTheme = ThemeService.NormalizeThemeName(Settings.CurrentTheme);
            Settings.AutoStartWithWindows = IsAutoStartEnabled();
            SyncThemeModeFields();
        }
        catch
        {
            Settings = new Settings();
            _bottomActionBarService.EnsureSlots(Settings);
            Settings.CurrentTheme = ThemeService.NormalizeThemeName(Settings.CurrentTheme);
            SyncThemeModeFields();
        }
    }

    private void SyncThemeModeFields()
    {
        var themeService = new ThemeService();
        var theme = themeService.LoadTheme(Settings.CurrentTheme);
        Settings.DarkMode = theme.DarkMode;
        Settings.AccentColor = theme.AccentColor;
        Settings.UseCustomTextColors = themeService.GetCurrentTextColors(Settings).Enabled;
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    public void SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath ?? "";
                key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
                key.DeleteValue(LegacyAppName, false);
            }

            Settings.AutoStartWithWindows = enabled;
            Save();
        }
        catch
        {
            // Ignore registry errors
        }
    }

    public bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            if (key == null) return false;

            var value = key.GetValue(AppName);
            return value != null;
        }
        catch
        {
            return false;
        }
    }

    private static void MigrateLegacyAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null)
                return;

            var legacyValue = key.GetValue(LegacyAppName);
            if (legacyValue == null)
                return;

            if (key.GetValue(AppName) == null)
            {
                var exePath = Environment.ProcessPath;
                key.SetValue(AppName, string.IsNullOrWhiteSpace(exePath) ? legacyValue : $"\"{exePath}\"");
            }

            key.DeleteValue(LegacyAppName, false);
        }
        catch
        {
            // Ignore registry migration errors.
        }
    }
}
