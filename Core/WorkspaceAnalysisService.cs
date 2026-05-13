using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Core;

public class WorkspaceAnalysisService
{
    private readonly WorkspaceRuleService _ruleService;

    public WorkspaceAnalysisService(WorkspaceRuleService? ruleService = null)
    {
        _ruleService = ruleService ?? new WorkspaceRuleService();
    }

    public WorkspaceRestorePlan BuildPlan(
        Workspace workspace,
        IReadOnlyList<WindowInfo> currentWindows,
        WindowManager windowManager,
        WorkspaceRestoreMode mode = WorkspaceRestoreMode.Full)
    {
        workspace = WorkspaceRestoreRules.NormalizeWorkspace(workspace, workspace.Name);
        var plan = new WorkspaceRestorePlan
        {
            WorkspaceName = workspace.Name,
            Mode = mode
        };

        var usedHandles = new HashSet<nint>();
        foreach (var item in workspace.Items)
        {
            var match = WorkspaceRestoreRules.FindBestMatch(new[] { item }, currentWindows, handle => usedHandles.Contains(handle));
            if (match != null && match.Confidence != WorkspaceMatchConfidence.None)
            {
                usedHandles.Add(match.Window.Handle);

                var resolved = windowManager.ResolveTargetBounds(item, workspace.Monitors, out _, out var remapped);
                var moved = match.Window != null && !resolved.IsEmpty && IsPositionChanged(match.Window, resolved);
                plan.Items.Add(new WorkspacePlanItem
                {
                    Kind = !WorkspaceRestoreRules.IsAutoPlaceable(match.Confidence)
                        ? WorkspacePlanItemKind.LowConfidence
                        : remapped ? WorkspacePlanItemKind.MonitorRemap
                        : moved ? WorkspacePlanItemKind.ChangedPosition
                        : WorkspacePlanItemKind.Matched,
                    Item = item,
                    MatchedWindow = match.Window,
                    MatchScore = match.Score,
                    MatchConfidence = match.Confidence,
                    MonitorRemapped = remapped,
                    Message = MatchMessage(match, remapped, moved)
                });
                continue;
            }

            var missing = WorkspaceRestoreRules.HasMissingExecutablePath(item) || WorkspaceDocumentResolver.IsMissingDocumentTarget(item);
            plan.Items.Add(new WorkspacePlanItem
            {
                Kind = missing ? WorkspacePlanItemKind.MissingTarget : WorkspacePlanItemKind.WillLaunch,
                Item = item,
                Message = missing ? "Missing app or document target" : "Not currently open; will launch"
            });
        }

        foreach (var extra in currentWindows.Where(window => !usedHandles.Contains(window.Handle)))
        {
            if (IsWinAirBarWindow(extra))
                continue;

            var (isProtected, isRisky, reason) = ClassifyExtraWindow(extra);
            var action = isProtected || isRisky
                ? WorkspaceCleanupAction.Keep
                : _ruleService.CleanupActionFor(extra, workspace.Metadata.DefaultCleanupAction);

            plan.ExtraWindows.Add(new WorkspaceExtraWindowPlan
            {
                Window = extra,
                IsProtected = isProtected,
                IsRisky = isRisky,
                Reason = reason,
                Action = action
            });
        }

        return plan;
    }

    public WorkspaceRestoreResult ExecutePlan(WorkspaceRestorePlan plan, Workspace workspace, WindowManager windowManager, WindowHistoryService? historyService = null, SettingsService? settingsService = null)
    {
        return plan.Mode switch
        {
            WorkspaceRestoreMode.MissingOnly => ExecuteMissingOnly(plan, workspace, windowManager),
            WorkspaceRestoreMode.LayoutOnly => ExecuteLayoutOnly(plan, workspace, windowManager),
            WorkspaceRestoreMode.Clean => ExecuteClean(plan, workspace, windowManager, historyService, settingsService),
            _ => windowManager == null ? new WorkspaceRestoreResult { WorkspaceName = workspace.Name } : new WorkspaceService().RestoreWorkspace(workspace, windowManager)
        };
    }

    public void ApplyCleanup(IEnumerable<WorkspaceExtraWindowPlan> extras, WindowManager windowManager, WindowHistoryService? historyService = null, SettingsService? settingsService = null)
    {
        foreach (var extra in extras)
        {
            try
            {
                if (extra.IsProtected && extra.Action == WorkspaceCleanupAction.Close)
                    continue;

                switch (extra.Action)
                {
                    case WorkspaceCleanupAction.Minimize:
                        windowManager.MinimizeWindow(extra.Window.Handle);
                        break;
                    case WorkspaceCleanupAction.Close:
                        RecordHistory(extra.Window, historyService, settingsService);
                        windowManager.CloseWindow(extra.Window.Handle);
                        break;
                }
            }
            catch { }
        }
    }

    private WorkspaceRestoreResult ExecuteMissingOnly(WorkspaceRestorePlan plan, Workspace workspace, WindowManager windowManager)
    {
        var result = new WorkspaceRestoreResult { WorkspaceName = workspace.Name };
        foreach (var itemPlan in plan.Items)
        {
            if (itemPlan.Kind != WorkspacePlanItemKind.WillLaunch)
            {
                result.Items.Add(new WorkspaceRestoreItemResult
                {
                    DisplayName = itemPlan.Item.DisplayName,
                    Status = WorkspaceRestoreStatus.Skipped,
                    Message = itemPlan.Kind == WorkspacePlanItemKind.MissingTarget ? "Missing target" : "Already open"
                });
                continue;
            }

            var bounds = windowManager.ResolveTargetBounds(itemPlan.Item, workspace.Monitors, out _, out _);
            var launched = windowManager.TryLaunchWorkspaceItem(itemPlan.Item, bounds, out var message);
            result.Items.Add(new WorkspaceRestoreItemResult
            {
                DisplayName = itemPlan.Item.DisplayName,
                Status = launched ? WorkspaceRestoreStatus.Launched : WorkspaceRestoreStatus.Skipped,
                Message = message
            });
        }

        return result;
    }

    private WorkspaceRestoreResult ExecuteLayoutOnly(WorkspaceRestorePlan plan, Workspace workspace, WindowManager windowManager)
    {
        var result = new WorkspaceRestoreResult { WorkspaceName = workspace.Name };
        foreach (var itemPlan in plan.Items)
        {
            if (itemPlan.MatchedWindow != null && WorkspaceRestoreRules.IsAutoPlaceable(itemPlan.MatchConfidence))
            {
                windowManager.RestoreWindowLayout(itemPlan.MatchedWindow, itemPlan.Item, workspace.Monitors);
                result.Items.Add(new WorkspaceRestoreItemResult
                {
                    DisplayName = itemPlan.Item.DisplayName,
                    Status = WorkspaceRestoreStatus.Restored,
                    MatchScore = itemPlan.MatchScore,
                    MatchConfidence = itemPlan.MatchConfidence,
                    Message = "Repositioned existing window"
                });
            }
            else
            {
                result.Items.Add(new WorkspaceRestoreItemResult
                {
                    DisplayName = itemPlan.Item.DisplayName,
                    Status = WorkspaceRestoreStatus.Skipped,
                    Message = "No matching open window"
                });
            }
        }

        return result;
    }

    private WorkspaceRestoreResult ExecuteClean(WorkspaceRestorePlan plan, Workspace workspace, WindowManager windowManager, WindowHistoryService? historyService, SettingsService? settingsService)
    {
        var result = new WorkspaceService().RestoreWorkspace(workspace, windowManager);
        ApplyCleanup(plan.ExtraWindows, windowManager, historyService, settingsService);
        return result;
    }

    private static bool IsPositionChanged(WindowInfo window, WorkspaceRect target)
    {
        const double tolerance = 16;
        return Math.Abs(window.Left - target.Left) > tolerance
            || Math.Abs(window.Top - target.Top) > tolerance
            || Math.Abs(window.Width - target.Width) > tolerance
            || Math.Abs(window.Height - target.Height) > tolerance;
    }

    private static string MatchMessage(WorkspaceMatch match, bool remapped, bool moved)
    {
        var text = $"{match.Confidence} match ({match.Score})";
        if (remapped)
            text += "; monitor remap";
        if (moved)
            text += "; position differs";
        return text;
    }

    public static (bool IsProtected, bool IsRisky, string Reason) ClassifyExtraWindow(WindowInfo window)
    {
        if (IsWinAirBarWindow(window))
            return (true, false, "WinAirBar window");

        var process = window.ProcessName ?? "";
        if (process.Contains("setup", StringComparison.OrdinalIgnoreCase)
            || process.Contains("installer", StringComparison.OrdinalIgnoreCase)
            || process.Equals("msiexec", StringComparison.OrdinalIgnoreCase)
            || process.Equals("explorer", StringComparison.OrdinalIgnoreCase)
            || process.Equals("Taskmgr", StringComparison.OrdinalIgnoreCase))
            return (true, false, "Protected system or installer window");

        var title = window.Title ?? "";
        if (title.Contains('*') || title.Contains("Untitled", StringComparison.OrdinalIgnoreCase) || title.Contains("Document", StringComparison.OrdinalIgnoreCase))
            return (false, true, "May contain unsaved work");

        return (false, false, "Extra window");
    }

    private static bool IsWinAirBarWindow(WindowInfo window)
        => window.ProcessName.Equals("WinAirBar", StringComparison.OrdinalIgnoreCase)
           || window.ProcessName.Equals("AirBar", StringComparison.OrdinalIgnoreCase)
           || window.ProcessName.Equals("FloatingTaskbarMenu", StringComparison.OrdinalIgnoreCase);

    private static void RecordHistory(WindowInfo window, WindowHistoryService? historyService, SettingsService? settingsService)
    {
        try
        {
            if (historyService == null || settingsService?.Settings.TrackWindowHistory != true)
                return;

            historyService.AddWindowHistory(new WindowHistory
            {
                Title = window.Title,
                ProcessName = window.ProcessName,
                ExecutablePath = string.IsNullOrWhiteSpace(window.ExecutablePath) ? window.ProcessName : window.ExecutablePath,
                ClosedTime = DateTime.Now
            });
        }
        catch { }
    }
}
