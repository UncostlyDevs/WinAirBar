using System.IO;
using System.Text.Json;
using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Core;

public class WorkspaceRuleService
{
    private readonly string _rulesPath;
    private List<WorkspaceRule> _rules = new();

    public WorkspaceRuleService(string? appDataDirectory = null)
    {
        var root = appDataDirectory ?? AppIdentity.AppDataDirectory;
        _rulesPath = Path.Combine(root, "WorkspaceRules", "rules.json");
        Load();
    }

    public IReadOnlyList<WorkspaceRule> GetRules()
    {
        Load();
        return _rules.ToList();
    }

    public void SaveRule(WorkspaceRule rule)
    {
        Load();
        var existing = _rules.FindIndex(r => r.Id == rule.Id);
        if (existing >= 0)
            _rules[existing] = rule;
        else
            _rules.Add(rule);
        Save();
    }

    public void DeleteRule(string id)
    {
        Load();
        _rules.RemoveAll(rule => string.Equals(rule.Id, id, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    public bool ExcludeFromCapture(WindowInfo window)
        => Match(window)?.ExcludeFromCapture == true;

    public WorkspaceCleanupAction CleanupActionFor(WindowInfo window, WorkspaceCleanupAction fallback)
    {
        var rule = Match(window);
        if (rule == null)
            return fallback;

        if (rule.NeverCleanup)
            return WorkspaceCleanupAction.Keep;

        return rule.DefaultCleanupAction;
    }

    public WorkspaceDefaultPosition PositionFor(WindowInfo window)
        => Match(window)?.DefaultPosition ?? WorkspaceDefaultPosition.None;

    public WorkspaceRestoreMode RestoreModeFor(WindowInfo window, WorkspaceRestoreMode fallback)
        => Match(window)?.DefaultRestoreMode ?? fallback;

    private WorkspaceRule? Match(WindowInfo window)
        => _rules.FirstOrDefault(rule =>
            (!string.IsNullOrWhiteSpace(rule.ExecutablePath) && string.Equals(rule.ExecutablePath, window.ExecutablePath, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(rule.ProcessName) && string.Equals(rule.ProcessName, window.ProcessName, StringComparison.OrdinalIgnoreCase)));

    private void Load()
    {
        try
        {
            if (File.Exists(_rulesPath))
                _rules = JsonSerializer.Deserialize<List<WorkspaceRule>>(File.ReadAllText(_rulesPath)) ?? new List<WorkspaceRule>();
        }
        catch
        {
            _rules = new List<WorkspaceRule>();
        }
    }

    private void Save()
    {
        try { StorageHelpers.WriteJsonAtomic(_rulesPath, _rules); } catch { }
    }
}
