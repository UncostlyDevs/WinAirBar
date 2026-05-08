using System.IO;

namespace FloatingTaskbarMenu.Core;

public static class AppIdentity
{
    public const string ProductName = "WinAirBar";
    public const string LegacyProductName = "AirBar";
    public const string Website = "https://winairbar.com";
    public const string ContactEmail = "sag@winairbar.com";

    private static readonly object MigrationLock = new();
    private static bool _migrationAttempted;

    public static string AppDataDirectory
    {
        get
        {
            EnsureLegacyAppDataMigrated();
            return CurrentAppDataDirectory;
        }
    }

    public static string CurrentAppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductName);

    public static string LegacyAppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacyProductName);

    public static void EnsureLegacyAppDataMigrated()
    {
        if (_migrationAttempted)
            return;

        lock (MigrationLock)
        {
            if (_migrationAttempted)
                return;

            _migrationAttempted = true;

            try
            {
                if (!Directory.Exists(LegacyAppDataDirectory))
                    return;

                Directory.CreateDirectory(CurrentAppDataDirectory);
                CopyFileIfMissing("settings.json");
                CopyFileIfMissing("launcher.json");
                CopyDirectoryIfMissing("History");
                CopyDirectoryIfMissing("Profiles");
            }
            catch
            {
                // Migration is best-effort; the app can still start with defaults.
            }
        }
    }

    private static void CopyFileIfMissing(string relativePath)
    {
        var source = Path.Combine(LegacyAppDataDirectory, relativePath);
        var target = Path.Combine(CurrentAppDataDirectory, relativePath);

        if (!File.Exists(source) || File.Exists(target))
            return;

        var targetDirectory = Path.GetDirectoryName(target);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
            Directory.CreateDirectory(targetDirectory);

        File.Copy(source, target, overwrite: false);
    }

    private static void CopyDirectoryIfMissing(string relativePath)
    {
        var source = Path.Combine(LegacyAppDataDirectory, relativePath);
        var target = Path.Combine(CurrentAppDataDirectory, relativePath);

        if (!Directory.Exists(source))
            return;

        CopyDirectory(source, target);
    }

    private static void CopyDirectory(string sourceDirectory, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);

        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            var target = Path.Combine(targetDirectory, Path.GetFileName(file));
            if (!File.Exists(target))
                File.Copy(file, target, overwrite: false);
        }

        foreach (var directory in Directory.GetDirectories(sourceDirectory))
            CopyDirectory(directory, Path.Combine(targetDirectory, Path.GetFileName(directory)));
    }
}
