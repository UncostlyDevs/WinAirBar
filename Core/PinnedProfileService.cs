using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Core;

public class PinnedProfileService
{
    private readonly string _profilesDirectory;

    public PinnedProfileService()
    {
        _profilesDirectory = Path.Combine(AppIdentity.AppDataDirectory, "Profiles");
        Directory.CreateDirectory(_profilesDirectory);
    }

    public List<string> GetProfileNames()
    {
        try
        {
            if (!Directory.Exists(_profilesDirectory))
                return new List<string> { "Default" };

            var files = Directory.GetFiles(_profilesDirectory, "*.json");
            var names = new List<string> { "Default" };
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
                    names.Add(name);
            }
            return names;
        }
        catch
        {
            return new List<string> { "Default" };
        }
    }

    public PinnedProfile LoadProfile(string profileName)
    {
        try
        {
            var filePath = Path.Combine(_profilesDirectory, $"{profileName}.json");
            if (!File.Exists(filePath))
                return new PinnedProfile { Name = profileName };

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<PinnedProfile>(json) ?? new PinnedProfile { Name = profileName };
        }
        catch
        {
            return new PinnedProfile { Name = profileName };
        }
    }

    public void SaveProfile(PinnedProfile profile)
    {
        try
        {
            var filePath = Path.Combine(_profilesDirectory, $"{profile.Name}.json");
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch { }
    }

    public void DeleteProfile(string profileName)
    {
        try
        {
            if (profileName == "Default")
                return;

            var filePath = Path.Combine(_profilesDirectory, $"{profileName}.json");
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch { }
    }

    public bool ProfileExists(string profileName)
    {
        if (profileName == "Default")
            return true;

        var filePath = Path.Combine(_profilesDirectory, $"{profileName}.json");
        return File.Exists(filePath);
    }
}
