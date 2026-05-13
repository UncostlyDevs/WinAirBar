using System.IO;
using System.Text.Json;
using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Core;

public class WorkspaceVersionService
{
    public const int VersionLimit = 10;

    private readonly string _workspaceDirectory;
    private readonly string _versionsDirectory;

    public WorkspaceVersionService(string? appDataDirectory = null)
    {
        var root = appDataDirectory ?? AppIdentity.AppDataDirectory;
        _workspaceDirectory = Path.Combine(root, "Workspaces");
        _versionsDirectory = Path.Combine(root, "WorkspaceVersions");
        Directory.CreateDirectory(_versionsDirectory);
    }

    public void SaveVersionBeforeOverwrite(string workspaceName)
    {
        try
        {
            var safeName = WorkspaceService.NormalizeWorkspaceName(workspaceName);
            var source = Path.Combine(_workspaceDirectory, $"{safeName}.json");
            if (!File.Exists(source))
                return;

            var targetDirectory = Path.Combine(_versionsDirectory, safeName);
            Directory.CreateDirectory(targetDirectory);
            var target = Path.Combine(targetDirectory, $"{DateTime.Now:yyyyMMdd-HHmmss-fff}.json");
            File.Copy(source, target, overwrite: true);
            PruneVersions(safeName);
        }
        catch { }
    }

    public List<string> GetVersions(string workspaceName)
    {
        try
        {
            var directory = Path.Combine(_versionsDirectory, WorkspaceService.NormalizeWorkspaceName(workspaceName));
            if (!Directory.Exists(directory))
                return new List<string>();

            return Directory.GetFiles(directory, "*.json")
                .OrderByDescending(File.GetLastWriteTime)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public Workspace? LoadVersion(string workspaceName, string versionId)
    {
        try
        {
            var path = Path.Combine(_versionsDirectory, WorkspaceService.NormalizeWorkspaceName(workspaceName), $"{StorageHelpers.ToSafeFileName(versionId)}.json");
            if (!File.Exists(path))
                return null;

            var workspace = JsonSerializer.Deserialize<Workspace>(File.ReadAllText(path));
            return workspace == null ? null : WorkspaceRestoreRules.NormalizeWorkspace(workspace, workspace.Name);
        }
        catch
        {
            return null;
        }
    }

    private void PruneVersions(string workspaceName)
    {
        try
        {
            var directory = Path.Combine(_versionsDirectory, WorkspaceService.NormalizeWorkspaceName(workspaceName));
            foreach (var file in Directory.GetFiles(directory, "*.json").OrderByDescending(File.GetLastWriteTime).Skip(VersionLimit))
                File.Delete(file);
        }
        catch { }
    }
}
