using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Core;

public static class WindowControlPlanner
{
    public static bool CanMoveToMonitor(IReadOnlyList<WorkspaceMonitor> monitors)
        => monitors.Count > 1;

    public static WorkspaceRect Center(WorkspaceRect currentBounds, WorkspaceMonitor monitor)
    {
        var width = ClampSize(currentBounds.Width, monitor.WorkWidth);
        var height = ClampSize(currentBounds.Height, monitor.WorkHeight);
        return WorkspacePlacementPlanner.ClampToWorkArea(new WorkspaceRect
        {
            Left = monitor.WorkLeft + (monitor.WorkWidth - width) / 2,
            Top = monitor.WorkTop + (monitor.WorkHeight - height) / 2,
            Width = width,
            Height = height
        }, monitor);
    }

    public static WorkspaceRect SnapLeft(WorkspaceMonitor monitor)
        => Half(monitor, left: true);

    public static WorkspaceRect SnapRight(WorkspaceMonitor monitor)
        => Half(monitor, left: false);

    public static WorkspaceRect MoveToMonitor(WorkspaceRect currentBounds, WorkspaceMonitor targetMonitor)
        => Center(currentBounds, targetMonitor);

    private static WorkspaceRect Half(WorkspaceMonitor monitor, bool left)
    {
        var width = Math.Max(1, monitor.WorkWidth / 2);
        return new WorkspaceRect
        {
            Left = left ? monitor.WorkLeft : monitor.WorkLeft + width,
            Top = monitor.WorkTop,
            Width = width,
            Height = Math.Max(1, monitor.WorkHeight)
        };
    }

    private static double ClampSize(double size, double max)
        => Math.Max(1, Math.Min(size <= 0 ? max : size, Math.Max(1, max)));
}
