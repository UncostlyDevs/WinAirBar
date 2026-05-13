using System.IO;
using System.IO.Compression;
using System.Text.Json;
using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Core;

[Flags]
public enum BackupSection
{
    None = 0,
    Settings = 1,
    BottomActions = 2,
    LauncherApps = 4,
    PinnedProfiles = 8,
    WindowHistory = 16,
    Workspaces = 32,
    WorkspaceSnapshots = 64,
    All = Settings | BottomActions | LauncherApps | PinnedProfiles | WindowHistory | Workspaces | WorkspaceSnapshots
}

public sealed class BackupManifest
{
    public string AppName { get; set; } = AppIdentity.ProductName;
    public int ExportVersion { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string WinAirBarVersion { get; set; } = typeof(AppIdentity).Assembly.GetName().Version?.ToString() ?? "unknown";
    public List<string> SelectedSections { get; set; } = new();
    public List<string> Files { get; set; } = new();
}

public sealed class BackupPreview
{
    public string SourcePath { get; set; } = "";
    public BackupManifest Manifest { get; set; } = new();
    public BackupSection IncludedSections { get; set; }
    public List<string> IncludedSectionNames => BackupService.SectionNames(IncludedSections);
    public List<string> IncludedWorkspaces { get; set; } = new();
    public List<string> IncludedProfiles { get; set; } = new();
    public List<string> IncludedWorkspaceSnapshots { get; set; } = new();
}

public sealed class BackupExportResult
{
    public string ZipPath { get; set; } = "";
    public BackupManifest Manifest { get; set; } = new();
}

public sealed class BackupImportResult
{
    public string PreImportBackupPath { get; set; } = "";
    public BackupSection ImportedSections { get; set; }
    public List<BackupConflict> Conflicts { get; set; } = new();
}

public sealed class BackupConflict
{
    public BackupSection Section { get; set; }
    public string Name { get; set; } = "";
    public string EntryName { get; set; } = "";
    public string TargetPath { get; set; } = "";
    public BackupConflictChoice Choice { get; set; } = BackupConflictChoice.ImportAsCopy;
}

public class BackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _appDataDirectory;

    public BackupService(string? appDataDirectory = null)
    {
        _appDataDirectory = appDataDirectory ?? AppIdentity.AppDataDirectory;
    }

    public string CreateDefaultBackupPath()
    {
        var directory = Path.Combine(_appDataDirectory, "Backups");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"WinAirBar-Backup-{DateTime.Now:yyyy-MM-dd-HH-mm}.zip");
    }

    public BackupExportResult Export(string zipPath, BackupSection sections)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(zipPath) ?? _appDataDirectory);
        if (File.Exists(zipPath))
            File.Delete(zipPath);

        var manifest = new BackupManifest
        {
            CreatedAt = DateTime.Now,
            SelectedSections = SectionNames(sections)
        };

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            AddSelectedFiles(archive, manifest, sections);
            manifest.Files.Insert(0, "manifest.json");
            AddJson(archive, "manifest.json", manifest);
        }

        return new BackupExportResult { ZipPath = zipPath, Manifest = manifest };
    }

    public BackupPreview PreviewImport(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var manifest = ReadManifest(archive) ?? new BackupManifest();
        var sections = DetectSections(archive);
        return new BackupPreview
        {
            SourcePath = zipPath,
            Manifest = manifest,
            IncludedSections = sections,
            IncludedWorkspaces = ListEntries(archive, "workspaces/"),
            IncludedProfiles = ListEntries(archive, "profiles/"),
            IncludedWorkspaceSnapshots = ListEntries(archive, "workspace-snapshots/")
        };
    }

    public BackupImportResult Import(string zipPath, BackupSection sections)
        => Import(zipPath, sections, PreviewConflicts(zipPath, sections));

    public List<BackupConflict> PreviewConflicts(string zipPath, BackupSection sections)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var importSections = sections & DetectSections(archive);
        var conflicts = new List<BackupConflict>();

        if (importSections.HasFlag(BackupSection.Settings))
            AddSingleFileConflict(conflicts, archive, BackupSection.Settings, "Settings", "settings/settings.json", SettingsPath, BackupConflictChoice.KeepLocal);

        if (importSections.HasFlag(BackupSection.BottomActions))
            AddSingleFileConflict(conflicts, archive, BackupSection.BottomActions, "Bottom Actions", "settings/bottom-actions.json", SettingsPath, BackupConflictChoice.KeepLocal);

        if (importSections.HasFlag(BackupSection.LauncherApps))
            AddSingleFileConflict(conflicts, archive, BackupSection.LauncherApps, "Launcher Apps", "launcher/launcher.json", Path.Combine(_appDataDirectory, "launcher.json"), BackupConflictChoice.KeepLocal);

        if (importSections.HasFlag(BackupSection.PinnedProfiles))
            AddDirectoryConflicts(conflicts, archive, BackupSection.PinnedProfiles, "profiles/", Path.Combine(_appDataDirectory, "Profiles"), BackupConflictChoice.ImportAsCopy);

        if (importSections.HasFlag(BackupSection.WindowHistory))
            AddSingleFileConflict(conflicts, archive, BackupSection.WindowHistory, "Window History", "history/history.json", Path.Combine(_appDataDirectory, "History", "history.json"), BackupConflictChoice.KeepLocal);

        if (importSections.HasFlag(BackupSection.Workspaces))
            AddDirectoryConflicts(conflicts, archive, BackupSection.Workspaces, "workspaces/", Path.Combine(_appDataDirectory, "Workspaces"), BackupConflictChoice.ImportAsCopy);

        if (importSections.HasFlag(BackupSection.WorkspaceSnapshots))
            AddDirectoryConflicts(conflicts, archive, BackupSection.WorkspaceSnapshots, "workspace-snapshots/", Path.Combine(_appDataDirectory, "WorkspaceSnapshots"), BackupConflictChoice.ImportAsCopy);

        return conflicts;
    }

    public BackupImportResult Import(string zipPath, BackupSection sections, IEnumerable<BackupConflict> conflicts)
    {
        var preview = PreviewImport(zipPath);
        var importSections = sections & preview.IncludedSections;
        var preImportPath = Path.Combine(_appDataDirectory, "Backups", "PreImport", $"WinAirBar-PreImport-{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.zip");
        Export(preImportPath, BackupSection.All);
        var conflictList = conflicts.ToList();

        using var archive = ZipFile.OpenRead(zipPath);
        if (importSections.HasFlag(BackupSection.Settings))
            ExtractEntryWithConflict(archive, "settings/settings.json", SettingsPath, conflictList);

        if (importSections.HasFlag(BackupSection.BottomActions))
            ImportBottomActions(archive, conflictList);

        if (importSections.HasFlag(BackupSection.LauncherApps))
            ExtractEntryWithConflict(archive, "launcher/launcher.json", Path.Combine(_appDataDirectory, "launcher.json"), conflictList);

        if (importSections.HasFlag(BackupSection.PinnedProfiles))
            ExtractDirectoryWithConflicts(archive, "profiles/", Path.Combine(_appDataDirectory, "Profiles"), conflictList);

        if (importSections.HasFlag(BackupSection.WindowHistory))
            ExtractEntryWithConflict(archive, "history/history.json", Path.Combine(_appDataDirectory, "History", "history.json"), conflictList);

        if (importSections.HasFlag(BackupSection.Workspaces))
            ExtractDirectoryWithConflicts(archive, "workspaces/", Path.Combine(_appDataDirectory, "Workspaces"), conflictList);

        if (importSections.HasFlag(BackupSection.WorkspaceSnapshots))
            ExtractDirectoryWithConflicts(archive, "workspace-snapshots/", Path.Combine(_appDataDirectory, "WorkspaceSnapshots"), conflictList);

        return new BackupImportResult
        {
            PreImportBackupPath = preImportPath,
            ImportedSections = importSections,
            Conflicts = conflictList
        };
    }

    public static List<string> SectionNames(BackupSection sections)
    {
        var names = new List<string>();
        foreach (var (section, name) in SectionMap())
        {
            if (sections.HasFlag(section))
                names.Add(name);
        }
        return names;
    }

    public static BackupSection FromNames(IEnumerable<string> names)
    {
        var result = BackupSection.None;
        foreach (var name in names)
        {
            var match = SectionMap().FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
            result |= match.Section;
        }
        return result;
    }

    private string SettingsPath => Path.Combine(_appDataDirectory, "settings.json");

    private void AddSelectedFiles(ZipArchive archive, BackupManifest manifest, BackupSection sections)
    {
        if (sections.HasFlag(BackupSection.Settings))
            AddFileIfExists(archive, manifest, SettingsPath, "settings/settings.json");

        if (sections.HasFlag(BackupSection.BottomActions))
            AddBottomActions(archive, manifest);

        if (sections.HasFlag(BackupSection.LauncherApps))
            AddFileIfExists(archive, manifest, Path.Combine(_appDataDirectory, "launcher.json"), "launcher/launcher.json");

        if (sections.HasFlag(BackupSection.PinnedProfiles))
            AddDirectory(archive, manifest, Path.Combine(_appDataDirectory, "Profiles"), "profiles");

        if (sections.HasFlag(BackupSection.WindowHistory))
            AddFileIfExists(archive, manifest, Path.Combine(_appDataDirectory, "History", "history.json"), "history/history.json");

        if (sections.HasFlag(BackupSection.Workspaces))
            AddDirectory(archive, manifest, Path.Combine(_appDataDirectory, "Workspaces"), "workspaces");

        if (sections.HasFlag(BackupSection.WorkspaceSnapshots))
            AddDirectory(archive, manifest, Path.Combine(_appDataDirectory, "WorkspaceSnapshots"), "workspace-snapshots");
    }

    private void AddBottomActions(ZipArchive archive, BackupManifest manifest)
    {
        try
        {
            var settings = ReadSettings();
            AddJson(archive, "settings/bottom-actions.json", settings.BottomActionSlots);
            manifest.Files.Add("settings/bottom-actions.json");
        }
        catch { }
    }

    private Settings ReadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(SettingsPath)) ?? new Settings();
        }
        catch { }
        return new Settings();
    }

    private void ImportBottomActions(ZipArchive archive, IReadOnlyList<BackupConflict>? conflicts = null)
    {
        var entry = archive.GetEntry("settings/bottom-actions.json");
        if (entry == null)
            return;

        var conflict = FindConflict(conflicts, entry.FullName);
        if (conflict?.Choice == BackupConflictChoice.KeepLocal)
            return;

        using var stream = entry.Open();
        var slots = JsonSerializer.Deserialize<List<BottomActionSlot>>(stream) ?? new List<BottomActionSlot>();
        var settings = ReadSettings();
        settings.BottomActionSlots = slots;
        new BottomActionBarService().EnsureSlots(settings);
        StorageHelpers.WriteJsonAtomic(SettingsPath, settings);
    }

    private static BackupSection DetectSections(ZipArchive archive)
    {
        var names = archive.Entries.Select(entry => entry.FullName.Replace('\\', '/')).ToList();
        var sections = BackupSection.None;
        if (names.Contains("settings/settings.json", StringComparer.OrdinalIgnoreCase))
            sections |= BackupSection.Settings;
        if (names.Contains("settings/bottom-actions.json", StringComparer.OrdinalIgnoreCase))
            sections |= BackupSection.BottomActions;
        if (names.Contains("launcher/launcher.json", StringComparer.OrdinalIgnoreCase))
            sections |= BackupSection.LauncherApps;
        if (names.Any(name => name.StartsWith("profiles/", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            sections |= BackupSection.PinnedProfiles;
        if (names.Contains("history/history.json", StringComparer.OrdinalIgnoreCase))
            sections |= BackupSection.WindowHistory;
        if (names.Any(name => name.StartsWith("workspaces/", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            sections |= BackupSection.Workspaces;
        if (names.Any(name => name.StartsWith("workspace-snapshots/", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            sections |= BackupSection.WorkspaceSnapshots;
        return sections;
    }

    private static BackupManifest? ReadManifest(ZipArchive archive)
    {
        try
        {
            var entry = archive.GetEntry("manifest.json");
            if (entry == null)
                return null;

            using var stream = entry.Open();
            return JsonSerializer.Deserialize<BackupManifest>(stream);
        }
        catch
        {
            return null;
        }
    }

    private static void AddFileIfExists(ZipArchive archive, BackupManifest manifest, string sourcePath, string entryName)
    {
        if (!File.Exists(sourcePath))
            return;

        archive.CreateEntryFromFile(sourcePath, entryName, CompressionLevel.Optimal);
        manifest.Files.Add(entryName);
    }

    private static void AddDirectory(ZipArchive archive, BackupManifest manifest, string sourceDirectory, string entryPrefix)
    {
        if (!Directory.Exists(sourceDirectory))
            return;

        foreach (var file in Directory.GetFiles(sourceDirectory, "*.json", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file).Replace('\\', '/');
            var entryName = $"{entryPrefix}/{relative}";
            archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
            manifest.Files.Add(entryName);
        }
    }

    private static void AddJson<T>(ZipArchive archive, string entryName, T value)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        JsonSerializer.Serialize(stream, value, JsonOptions);
    }

    private static void ExtractEntry(ZipArchive archive, string entryName, string targetPath)
    {
        var entry = archive.GetEntry(entryName);
        if (entry == null)
            return;

        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        entry.ExtractToFile(targetPath, overwrite: true);
    }

    private static void ExtractEntryWithConflict(ZipArchive archive, string entryName, string targetPath, IReadOnlyList<BackupConflict> conflicts)
    {
        var conflict = FindConflict(conflicts, entryName);
        if (conflict?.Choice == BackupConflictChoice.KeepLocal)
            return;

        var finalTarget = conflict?.Choice == BackupConflictChoice.ImportAsCopy
            ? MakeCopyPath(targetPath)
            : targetPath;
        ExtractEntry(archive, entryName, finalTarget);
    }

    private static void ExtractDirectory(ZipArchive archive, string entryPrefix, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var entry in archive.Entries.Where(entry =>
                     entry.FullName.StartsWith(entryPrefix, StringComparison.OrdinalIgnoreCase)
                     && entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            var relative = entry.FullName[entryPrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(relative))
                continue;

            var targetPath = Path.GetFullPath(Path.Combine(targetDirectory, relative));
            var root = Path.GetFullPath(targetDirectory);
            if (!targetPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? root);
            entry.ExtractToFile(targetPath, overwrite: true);
        }
    }

    private static void ExtractDirectoryWithConflicts(ZipArchive archive, string entryPrefix, string targetDirectory, IReadOnlyList<BackupConflict> conflicts)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var entry in archive.Entries.Where(entry =>
                     entry.FullName.StartsWith(entryPrefix, StringComparison.OrdinalIgnoreCase)
                     && entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            var relative = entry.FullName[entryPrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(relative))
                continue;

            var targetPath = SafeTargetPath(targetDirectory, relative);
            if (targetPath == null)
                continue;

            var conflict = FindConflict(conflicts, entry.FullName);
            if (conflict?.Choice == BackupConflictChoice.KeepLocal)
                continue;

            if (conflict?.Choice == BackupConflictChoice.ImportAsCopy)
                targetPath = MakeCopyPath(targetPath);

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? Path.GetFullPath(targetDirectory));
            entry.ExtractToFile(targetPath, overwrite: true);
        }
    }

    private static List<string> ListEntries(ZipArchive archive, string entryPrefix)
        => archive.Entries
            .Where(entry => entry.FullName.StartsWith(entryPrefix, StringComparison.OrdinalIgnoreCase)
                            && entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(entry => Path.GetFileNameWithoutExtension(entry.FullName))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static void AddSingleFileConflict(List<BackupConflict> conflicts, ZipArchive archive, BackupSection section, string name, string entryName, string targetPath, BackupConflictChoice defaultChoice)
    {
        if (archive.GetEntry(entryName) == null || !File.Exists(targetPath))
            return;

        conflicts.Add(new BackupConflict
        {
            Section = section,
            Name = name,
            EntryName = entryName,
            TargetPath = targetPath,
            Choice = defaultChoice
        });
    }

    private static void AddDirectoryConflicts(List<BackupConflict> conflicts, ZipArchive archive, BackupSection section, string entryPrefix, string targetDirectory, BackupConflictChoice defaultChoice)
    {
        foreach (var entry in archive.Entries.Where(entry =>
                     entry.FullName.StartsWith(entryPrefix, StringComparison.OrdinalIgnoreCase)
                     && entry.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            var relative = entry.FullName[entryPrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            var targetPath = SafeTargetPath(targetDirectory, relative);
            if (targetPath == null || !File.Exists(targetPath))
                continue;

            conflicts.Add(new BackupConflict
            {
                Section = section,
                Name = Path.GetFileNameWithoutExtension(targetPath),
                EntryName = entry.FullName,
                TargetPath = targetPath,
                Choice = defaultChoice
            });
        }
    }

    private static string? SafeTargetPath(string targetDirectory, string relativePath)
    {
        var targetPath = Path.GetFullPath(Path.Combine(targetDirectory, relativePath));
        var root = Path.GetFullPath(targetDirectory);
        return targetPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? targetPath : null;
    }

    private static BackupConflict? FindConflict(IReadOnlyList<BackupConflict>? conflicts, string entryName)
        => conflicts?.FirstOrDefault(conflict => string.Equals(conflict.EntryName.Replace('\\', '/'), entryName.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));

    private static string MakeCopyPath(string targetPath)
    {
        var directory = Path.GetDirectoryName(targetPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(targetPath);
        var extension = Path.GetExtension(targetPath);
        var candidate = Path.Combine(directory, $"{name} (Imported){extension}");
        var index = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, $"{name} (Imported {index}){extension}");
            index++;
        }

        return candidate;
    }

    private static IReadOnlyList<(BackupSection Section, string Name)> SectionMap()
        =>
        [
            (BackupSection.Settings, "Settings"),
            (BackupSection.BottomActions, "Bottom Actions"),
            (BackupSection.LauncherApps, "Launcher Apps"),
            (BackupSection.PinnedProfiles, "Pinned Profiles"),
            (BackupSection.WindowHistory, "Window History"),
            (BackupSection.Workspaces, "Workspaces"),
            (BackupSection.WorkspaceSnapshots, "Workspace Snapshots")
        ];
}
