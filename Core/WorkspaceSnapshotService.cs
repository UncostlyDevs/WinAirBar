using System.IO;
using System.Text.Json;
using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Core;

public class WorkspaceSnapshotService
{
    public const int AutomaticSnapshotLimit = 5;

    private readonly string _snapshotDirectory;
    private readonly string _automaticDirectory;
    private readonly string _manualDirectory;

    public WorkspaceSnapshotService(string? snapshotDirectory = null)
    {
        _snapshotDirectory = snapshotDirectory ?? Path.Combine(AppIdentity.AppDataDirectory, "WorkspaceSnapshots");
        _automaticDirectory = Path.Combine(_snapshotDirectory, "Automatic");
        _manualDirectory = Path.Combine(_snapshotDirectory, "Manual");
        Directory.CreateDirectory(_automaticDirectory);
        Directory.CreateDirectory(_manualDirectory);
    }

    public string SnapshotDirectory => _snapshotDirectory;

    public Workspace CaptureAutomaticRollback(IEnumerable<WindowInfo> windows, Settings settings, WindowManager windowManager)
    {
        var snapshot = BuildWorkspace($"Undo Restore {DateTime.Now:yyyy-MM-dd HH-mm-ss}", windows, settings, windowManager);
        var path = Path.Combine(_automaticDirectory, $"rollback-{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
        StorageHelpers.WriteJsonAtomic(path, snapshot);
        PruneAutomaticSnapshots();
        return snapshot;
    }

    public Workspace SaveManualSnapshot(string name, IEnumerable<WindowInfo> windows, Settings settings, WindowManager windowManager)
    {
        var safeName = StorageHelpers.ToSafeFileName(name, "Snapshot");
        var snapshot = BuildWorkspace(safeName, windows, settings, windowManager);
        StorageHelpers.WriteJsonAtomic(Path.Combine(_manualDirectory, $"{safeName}.json"), snapshot);
        return snapshot;
    }

    public Workspace? LoadLatestAutomaticRollback()
    {
        var path = LatestAutomaticPath();
        return path == null ? null : LoadSnapshotFile(path);
    }

    public bool HasAutomaticRollback()
        => LatestAutomaticPath() != null;

    public List<string> GetManualSnapshotNames()
    {
        try
        {
            return Directory.GetFiles(_manualDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .OrderByDescending(name => File.GetLastWriteTime(Path.Combine(_manualDirectory, $"{name}.json")))
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public Workspace LoadManualSnapshot(string name)
    {
        var safeName = StorageHelpers.ToSafeFileName(name, "Snapshot");
        return LoadSnapshotFile(Path.Combine(_manualDirectory, $"{safeName}.json"))
            ?? WorkspaceRestoreRules.NormalizeWorkspace(new Workspace { Name = safeName }, safeName);
    }

    public void PruneAutomaticSnapshots(int limit = AutomaticSnapshotLimit)
    {
        try
        {
            var files = Directory.GetFiles(_automaticDirectory, "*.json")
                .OrderByDescending(File.GetLastWriteTime)
                .Skip(Math.Max(0, limit))
                .ToList();

            foreach (var file in files)
                File.Delete(file);
        }
        catch { }
    }

    private string? LatestAutomaticPath()
    {
        try
        {
            return Directory.GetFiles(_automaticDirectory, "*.json")
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static Workspace BuildWorkspace(string name, IEnumerable<WindowInfo> windows, Settings settings, WindowManager windowManager)
    {
        var now = DateTime.Now;
        var monitors = windowManager.GetMonitors();
        var workspace = new Workspace
        {
            SchemaVersion = WorkspaceRestoreRules.CurrentSchemaVersion,
            Name = WorkspaceService.NormalizeWorkspaceName(name),
            CreatedAt = now,
            UpdatedAt = now,
            CapturedProfile = settings.CurrentPinnedProfile,
            CapturedTheme = settings.CurrentTheme,
            ShowWindowList = settings.ShowWindowList,
            ShowSystemTray = settings.ShowSystemTray,
            ShowAuxiliaryControls = settings.ShowAuxiliaryControls,
            Monitors = monitors,
            Items = windows
                .Where(window => !string.IsNullOrWhiteSpace(window.ProcessName))
                .Select(window => WorkspaceRestoreRules.ToWorkspaceItem(window, monitors))
                .ToList()
        };

        return WorkspaceRestoreRules.NormalizeWorkspace(workspace, workspace.Name);
    }

    private static Workspace? LoadSnapshotFile(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var json = File.ReadAllText(path);
            var snapshot = JsonSerializer.Deserialize<Workspace>(json);
            return snapshot == null ? null : WorkspaceRestoreRules.NormalizeWorkspace(snapshot, snapshot.Name);
        }
        catch
        {
            return null;
        }
    }
}
