using System.IO;
using System.Text.Json;
using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Core;

public class WorkspaceTimelineService
{
    private readonly string _timelinePath;
    private List<WorkspaceTimelineEvent> _events = new();

    public WorkspaceTimelineService(string? appDataDirectory = null)
    {
        var root = appDataDirectory ?? AppIdentity.AppDataDirectory;
        _timelinePath = Path.Combine(root, "WorkspaceTimeline", "timeline.json");
        Load();
    }

    public IReadOnlyList<WorkspaceTimelineEvent> GetEvents(string? workspaceName = null, int limit = 80)
    {
        Load();
        var events = _events.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(workspaceName))
            events = events.Where(e => string.Equals(e.WorkspaceName, workspaceName, StringComparison.OrdinalIgnoreCase));

        return events
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToList();
    }

    public void Record(string workspaceName, string eventType, string summary, IEnumerable<WorkspaceTimelineItem>? items = null)
    {
        Load();
        _events.Insert(0, new WorkspaceTimelineEvent
        {
            WorkspaceName = workspaceName,
            EventType = eventType,
            Summary = summary,
            Items = items?.ToList() ?? new List<WorkspaceTimelineItem>()
        });

        if (_events.Count > 1000)
            _events = _events.Take(1000).ToList();

        Save();
    }

    public static IEnumerable<WorkspaceTimelineItem> FromRestoreResult(WorkspaceRestoreResult result)
        => result.Items.Select(item => new WorkspaceTimelineItem
        {
            DisplayName = item.DisplayName,
            Status = item.Status.ToString(),
            Message = item.Message
        });

    private void Load()
    {
        try
        {
            if (File.Exists(_timelinePath))
                _events = JsonSerializer.Deserialize<List<WorkspaceTimelineEvent>>(File.ReadAllText(_timelinePath)) ?? new List<WorkspaceTimelineEvent>();
        }
        catch
        {
            _events = new List<WorkspaceTimelineEvent>();
        }
    }

    private void Save()
    {
        try
        {
            StorageHelpers.WriteJsonAtomic(_timelinePath, _events);
        }
        catch { }
    }
}
