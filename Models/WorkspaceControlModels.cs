namespace FloatingTaskbarMenu.Models;

public enum WorkspaceRestoreMode
{
    Full,
    MissingOnly,
    LayoutOnly,
    Clean
}

public enum WorkspaceCleanupAction
{
    Keep,
    Minimize,
    Close
}

public enum WorkspaceDefaultPosition
{
    None,
    Center,
    SnapLeft,
    SnapRight
}

public enum WorkspacePlanItemKind
{
    Matched,
    WillLaunch,
    MissingTarget,
    LowConfidence,
    MonitorRemap,
    ChangedPosition
}

public enum WorkspaceAutoActionKind
{
    OpenTarget,
    OpenSettingsUri,
    Theme,
    Volume,
    Mute,
    Sleep,
    Shutdown,
    Restart,
    SignOut
}

public enum BackupConflictChoice
{
    KeepLocal,
    ImportAsCopy,
    Overwrite
}

public class WorkspaceMetadata
{
    public string Notes { get; set; } = "";
    public string IconKey { get; set; } = "workspace";
    public string Color { get; set; } = "#60CDFF";
    public WorkspaceRestoreMode DefaultRestoreMode { get; set; } = WorkspaceRestoreMode.Full;
    public WorkspaceCleanupAction DefaultCleanupAction { get; set; } = WorkspaceCleanupAction.Minimize;
    public List<WorkspaceAutoAction> AutoActions { get; set; } = new();
    public List<WorkspaceScreenMemory> ScreenGallery { get; set; } = new();
}

public class WorkspaceScreenMemory
{
    public string MonitorId { get; set; } = "";
    public string Name { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public string Details { get; set; } = "";
    public string WindowList { get; set; } = "";
    public DateTime CapturedAt { get; set; } = DateTime.Now;
}

public class WorkspaceAutoAction
{
    public string Name { get; set; } = "";
    public WorkspaceAutoActionKind Kind { get; set; } = WorkspaceAutoActionKind.OpenTarget;
    public string Target { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public bool RequiresConfirmation { get; set; }
    public bool Enabled { get; set; } = true;
}

public class WorkspaceRestorePlan
{
    public string WorkspaceName { get; set; } = "";
    public WorkspaceRestoreMode Mode { get; set; } = WorkspaceRestoreMode.Full;
    public List<WorkspacePlanItem> Items { get; set; } = new();
    public List<WorkspaceExtraWindowPlan> ExtraWindows { get; set; } = new();
    public int MatchedCount => Items.Count(i => i.Kind == WorkspacePlanItemKind.Matched || i.Kind == WorkspacePlanItemKind.ChangedPosition || i.Kind == WorkspacePlanItemKind.MonitorRemap);
    public int LaunchCount => Items.Count(i => i.Kind == WorkspacePlanItemKind.WillLaunch);
    public int MissingCount => Items.Count(i => i.Kind == WorkspacePlanItemKind.MissingTarget);
    public int LowConfidenceCount => Items.Count(i => i.Kind == WorkspacePlanItemKind.LowConfidence);
    public int ExtraCount => ExtraWindows.Count;
}

public class WorkspacePlanItem
{
    public WorkspacePlanItemKind Kind { get; set; }
    public WorkspaceItem Item { get; set; } = new();
    public WindowInfo? MatchedWindow { get; set; }
    public int MatchScore { get; set; }
    public WorkspaceMatchConfidence MatchConfidence { get; set; } = WorkspaceMatchConfidence.None;
    public bool MonitorRemapped { get; set; }
    public string Message { get; set; } = "";
}

public class WorkspaceExtraWindowPlan
{
    public WindowInfo Window { get; set; } = new();
    public WorkspaceCleanupAction Action { get; set; } = WorkspaceCleanupAction.Minimize;
    public bool IsProtected { get; set; }
    public bool IsRisky { get; set; }
    public string Reason { get; set; } = "";
}

public class WorkspaceTimelineEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string WorkspaceName { get; set; } = "";
    public string EventType { get; set; } = "";
    public string Summary { get; set; } = "";
    public List<WorkspaceTimelineItem> Items { get; set; } = new();
}

public class WorkspaceTimelineItem
{
    public string DisplayName { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
}

public class WorkspaceRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProcessName { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public bool ExcludeFromCapture { get; set; }
    public bool NeverCleanup { get; set; }
    public WorkspaceCleanupAction DefaultCleanupAction { get; set; } = WorkspaceCleanupAction.Minimize;
    public WorkspaceRestoreMode? DefaultRestoreMode { get; set; }
    public WorkspaceDefaultPosition DefaultPosition { get; set; } = WorkspaceDefaultPosition.None;
}

public class WorkspaceSuggestion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Signature { get; set; } = "";
    public List<string> ProcessNames { get; set; } = new();
    public int SeenCount { get; set; }
    public DateTime FirstSeen { get; set; } = DateTime.Now;
    public DateTime LastSeen { get; set; } = DateTime.Now;
    public bool Dismissed { get; set; }
    public bool NeverSuggest { get; set; }
}

public class PinnedWindowGroup
{
    public string Name { get; set; } = "Group";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public List<PinnedWindowGroupItem> Items { get; set; } = new();
}

public class PinnedWindowGroupItem
{
    public string DisplayName { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string ProcessName { get; set; } = "";
}
