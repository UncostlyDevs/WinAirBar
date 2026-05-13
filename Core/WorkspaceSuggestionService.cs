using System.IO;
using System.Text.Json;
using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Core;

public class WorkspaceSuggestionService
{
    public const int SuggestionThreshold = 3;
    private readonly string _suggestionsPath;
    private List<WorkspaceSuggestion> _suggestions = new();

    public WorkspaceSuggestionService(string? appDataDirectory = null)
    {
        var root = appDataDirectory ?? AppIdentity.AppDataDirectory;
        _suggestionsPath = Path.Combine(root, "WorkspaceSuggestions", "suggestions.json");
        Load();
    }

    public WorkspaceSuggestion? RecordObservation(IEnumerable<WindowInfo> windows)
    {
        Load();
        var processNames = windows
            .Select(w => w.ProcessName.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name) && !IsIgnoredProcess(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        if (processNames.Count < 2)
            return null;

        var signature = string.Join("|", processNames).ToLowerInvariant();
        var suggestion = _suggestions.FirstOrDefault(s => s.Signature == signature);
        if (suggestion == null)
        {
            suggestion = new WorkspaceSuggestion
            {
                Signature = signature,
                ProcessNames = processNames,
                SeenCount = 0
            };
            _suggestions.Add(suggestion);
        }

        suggestion.SeenCount++;
        suggestion.LastSeen = DateTime.Now;
        Save();
        return suggestion;
    }

    public IReadOnlyList<WorkspaceSuggestion> GetActiveSuggestions()
    {
        Load();
        var cutoff = DateTime.Now.AddDays(-7);
        return _suggestions
            .Where(s => !s.Dismissed && !s.NeverSuggest && s.SeenCount >= SuggestionThreshold && s.LastSeen >= cutoff)
            .OrderByDescending(s => s.SeenCount)
            .ThenByDescending(s => s.LastSeen)
            .ToList();
    }

    public void Dismiss(string id, bool neverSuggest)
    {
        Load();
        var suggestion = _suggestions.FirstOrDefault(s => s.Id == id);
        if (suggestion == null)
            return;

        suggestion.Dismissed = true;
        suggestion.NeverSuggest = neverSuggest;
        Save();
    }

    private static bool IsIgnoredProcess(string name)
        => name.Equals("WinAirBar", StringComparison.OrdinalIgnoreCase)
           || name.Equals("AirBar", StringComparison.OrdinalIgnoreCase)
           || name.Equals("FloatingTaskbarMenu", StringComparison.OrdinalIgnoreCase);

    private void Load()
    {
        try
        {
            if (File.Exists(_suggestionsPath))
                _suggestions = JsonSerializer.Deserialize<List<WorkspaceSuggestion>>(File.ReadAllText(_suggestionsPath)) ?? new List<WorkspaceSuggestion>();
        }
        catch
        {
            _suggestions = new List<WorkspaceSuggestion>();
        }
    }

    private void Save()
    {
        try { StorageHelpers.WriteJsonAtomic(_suggestionsPath, _suggestions); } catch { }
    }
}
