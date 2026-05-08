using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media.Imaging;
using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Core;

public class AppLauncherService
{
    private readonly string _launcherFilePath;
    private List<AppLauncher> _apps = new();

    public AppLauncherService()
    {
        var launcherDirectory = AppIdentity.AppDataDirectory;
        Directory.CreateDirectory(launcherDirectory);
        _launcherFilePath = Path.Combine(launcherDirectory, "launcher.json");
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_launcherFilePath))
            {
                var json = File.ReadAllText(_launcherFilePath);
                _apps = JsonSerializer.Deserialize<List<AppLauncher>>(json) ?? new List<AppLauncher>();
            }
        }
        catch { _apps = new List<AppLauncher>(); }
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_apps, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_launcherFilePath, json);
        }
        catch { }
    }

    public void RecordLaunch(AppLauncher app)
    {
        try
        {
            Load();
            var existing = _apps.FirstOrDefault(a => IsSameLauncherTarget(a, app.ExecutablePath));
            if (existing != null)
            {
                existing.Name = string.IsNullOrWhiteSpace(app.Name) ? existing.Name : app.Name;
                existing.ExecutablePath = PreferStableTarget(existing.ExecutablePath, app.ExecutablePath);
                existing.LaunchCount++;
                existing.LastLaunched = DateTime.Now;
                existing.Icon = app.Icon;
            }
            else
            {
                app.LaunchCount = 1;
                app.LastLaunched = DateTime.Now;
                _apps.Add(app);
            }
            Save();
        }
        catch { }
    }

    public void AddOrUpdateApp(string name, string executablePath, BitmapSource? icon)
    {
        try
        {
            Load();
            var existing = _apps.FirstOrDefault(a => IsSameLauncherTarget(a, executablePath));
            if (existing != null)
            {
                existing.Name = name;
                existing.ExecutablePath = PreferStableTarget(existing.ExecutablePath, executablePath);
                existing.Icon = icon;
            }
            else
            {
                _apps.Add(new AppLauncher
                {
                    Name = name,
                    ExecutablePath = executablePath,
                    Icon = icon,
                    LaunchCount = 0,
                    LastLaunched = DateTime.Now,
                    IsPinned = false
                });
            }
            Save();
        }
        catch { }
    }

    public void PinApp(string executablePath)
    {
        try
        {
            Load();
            var app = _apps.FirstOrDefault(a => IsSameLauncherTarget(a, executablePath));
            if (app != null)
            {
                app.ExecutablePath = PreferStableTarget(app.ExecutablePath, executablePath);
                app.IsPinned = true;
                Save();
            }
        }
        catch { }
    }

    public void UnpinApp(string executablePath)
    {
        try
        {
            Load();
            var app = _apps.FirstOrDefault(a => IsSameLauncherTarget(a, executablePath));
            if (app != null)
            {
                app.ExecutablePath = PreferStableTarget(app.ExecutablePath, executablePath);
                app.IsPinned = false;
                Save();
            }
        }
        catch { }
    }

    public List<AppLauncher> GetFrequentApps(int maxCount)
    {
        try
        {
            Load();
            var now = DateTime.Now;
            var oneWeekAgo = now.AddDays(-7);

            // Sort: pinned first, then by launch count and recency
            return _apps
                .Where(a => a.IsPinned || a.LastLaunched >= oneWeekAgo)
                .OrderByDescending(a => a.IsPinned)
                .ThenByDescending(a => a.LaunchCount)
                .ThenByDescending(a => a.LastLaunched)
                .Take(maxCount)
                .ToList();
        }
        catch { return new List<AppLauncher>(); }
    }

    public AppLauncher? GetApp(string executablePath)
    {
        Load();
        return _apps.FirstOrDefault(a => IsSameLauncherTarget(a, executablePath));
    }

    private static bool IsSameLauncherTarget(AppLauncher app, string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return false;

        if (EqualsIgnoreCase(app.ExecutablePath, target) || EqualsIgnoreCase(app.Name, target))
            return true;

        var targetLeaf = GetTargetLeaf(target);
        if (string.IsNullOrWhiteSpace(targetLeaf))
            return false;

        return EqualsIgnoreCase(app.Name, targetLeaf)
            || EqualsIgnoreCase(GetTargetLeaf(app.ExecutablePath), targetLeaf);
    }

    private static string PreferStableTarget(string current, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return current;

        if (LooksLikePathOrUri(candidate) || !LooksLikePathOrUri(current))
            return candidate;

        return current;
    }

    private static bool LooksLikePathOrUri(string value)
        => !string.IsNullOrWhiteSpace(value)
            && (Path.IsPathRooted(value)
                || value.Contains(Path.DirectorySeparatorChar)
                || value.Contains(Path.AltDirectorySeparatorChar)
                || value.Contains(':'));

    private static string GetTargetLeaf(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        try
        {
            var fileName = Path.GetFileNameWithoutExtension(value);
            return string.IsNullOrWhiteSpace(fileName) ? value.Trim() : fileName.Trim();
        }
        catch
        {
            return value.Trim();
        }
    }

    private static bool EqualsIgnoreCase(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}
