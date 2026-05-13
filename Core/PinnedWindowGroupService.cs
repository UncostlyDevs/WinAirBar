using System.Diagnostics;
using System.IO;
using System.Text.Json;
using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Core;

public class PinnedWindowGroupService
{
    private readonly string _groupsDirectory;

    public PinnedWindowGroupService(string? appDataDirectory = null)
    {
        var root = appDataDirectory ?? AppIdentity.AppDataDirectory;
        _groupsDirectory = Path.Combine(root, "WindowGroups");
        Directory.CreateDirectory(_groupsDirectory);
    }

    public List<string> GetGroupNames()
    {
        try
        {
            return Directory.GetFiles(_groupsDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public PinnedWindowGroup CaptureGroup(string name, IEnumerable<WindowInfo> windows)
    {
        var group = new PinnedWindowGroup
        {
            Name = StorageHelpers.ToSafeFileName(name, "Group"),
            Items = windows
                .Where(w => !string.IsNullOrWhiteSpace(w.ProcessName))
                .Select(w => new PinnedWindowGroupItem
                {
                    DisplayName = string.IsNullOrWhiteSpace(w.Title) ? w.ProcessName : w.Title,
                    ExecutablePath = w.ExecutablePath,
                    ProcessName = w.ProcessName
                })
                .ToList()
        };

        SaveGroup(group);
        return group;
    }

    public PinnedWindowGroup LoadGroup(string name)
    {
        try
        {
            var safeName = StorageHelpers.ToSafeFileName(name, "Group");
            var path = Path.Combine(_groupsDirectory, $"{safeName}.json");
            if (File.Exists(path))
                return JsonSerializer.Deserialize<PinnedWindowGroup>(File.ReadAllText(path)) ?? new PinnedWindowGroup { Name = safeName };
            return new PinnedWindowGroup { Name = safeName };
        }
        catch
        {
            return new PinnedWindowGroup { Name = StorageHelpers.ToSafeFileName(name, "Group") };
        }
    }

    public void SaveGroup(PinnedWindowGroup group)
        => StorageHelpers.WriteJsonAtomic(Path.Combine(_groupsDirectory, $"{StorageHelpers.ToSafeFileName(group.Name, "Group")}.json"), group);

    public void LaunchGroup(string name)
    {
        var group = LoadGroup(name);
        foreach (var item in group.Items)
        {
            try
            {
                var target = !string.IsNullOrWhiteSpace(item.ExecutablePath) ? item.ExecutablePath : item.ProcessName;
                if (!string.IsNullOrWhiteSpace(target))
                    Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
            }
            catch { }
        }
    }
}
