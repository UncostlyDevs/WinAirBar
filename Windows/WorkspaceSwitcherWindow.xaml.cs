using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using FloatingTaskbarMenu.Core;
using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Windows;

public partial class WorkspaceSwitcherWindow : Window
{
    private readonly WindowManager _windowManager;
    private readonly SettingsService _settingsService;
    private readonly WorkspaceService _workspaceService = new();
    private readonly WorkspaceAnalysisService _analysisService = new(new WorkspaceRuleService());
    private readonly WorkspaceSnapshotService _snapshotService = new();
    private readonly WorkspaceTimelineService _timelineService = new();

    public WorkspaceSwitcherWindow(WindowManager windowManager, SettingsService settingsService)
    {
        _windowManager = windowManager;
        _settingsService = settingsService;
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshCards();
        WorkspaceCards.Focus();
    }

    private void RefreshCards()
    {
        var monitors = _windowManager.GetMonitors();
        var rows = _workspaceService.GetWorkspaceNames()
            .Select(name =>
            {
                var workspace = _workspaceService.LoadWorkspace(name);
                var preview = _workspaceService.BuildPreview(workspace, monitors);
                var formatted = WorkspacePreviewFormatter.Format(preview);
                return new SwitcherCardRow
                {
                    Name = workspace.Name,
                    Info = formatted.Info,
                    Health = formatted.Health,
                    Color = workspace.Metadata.Color
                };
            })
            .ToList();

        WorkspaceCards.ItemsSource = rows;
        WorkspaceCards.SelectedIndex = rows.Count > 0 ? 0 : -1;
    }

    private Workspace? SelectedWorkspace()
        => WorkspaceCards.SelectedItem is SwitcherCardRow row ? _workspaceService.LoadWorkspace(row.Name) : null;

    private void OnPreviewClick(object sender, RoutedEventArgs e)
    {
        var workspace = SelectedWorkspace();
        if (workspace == null)
            return;

        var plan = _analysisService.BuildPlan(workspace, _windowManager.GetWindows(), _windowManager, workspace.Metadata.DefaultRestoreMode);
        ThemedMessageBox.Show(
            this,
            $"{workspace.Name}\n\nMatched: {plan.MatchedCount}\nTo launch: {plan.LaunchCount}\nMissing: {plan.MissingCount}\nExtras: {plan.ExtraCount}",
            "Workspace Preview",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        var workspace = SelectedWorkspace();
        if (workspace == null)
            return;

        _snapshotService.CaptureAutomaticRollback(_windowManager.GetWindows(), _settingsService.Settings, _windowManager);
        var result = _workspaceService.RestoreWorkspace(workspace, _windowManager);
        _timelineService.Record(workspace.Name, "restore:switcher", $"Switcher restore: {result.RestoredCount} restored, {result.LaunchedCount} launched", WorkspaceTimelineService.FromRestoreResult(result));
        Close();
    }

    private void OnMissingOnlyClick(object sender, RoutedEventArgs e)
    {
        var workspace = SelectedWorkspace();
        if (workspace == null)
            return;

        _snapshotService.CaptureAutomaticRollback(_windowManager.GetWindows(), _settingsService.Settings, _windowManager);
        var plan = _analysisService.BuildPlan(workspace, _windowManager.GetWindows(), _windowManager, WorkspaceRestoreMode.MissingOnly);
        var result = _analysisService.ExecutePlan(plan, workspace, _windowManager);
        _timelineService.Record(workspace.Name, "restore:missing-only:switcher", $"Switcher missing-only: {result.LaunchedCount} launched", WorkspaceTimelineService.FromRestoreResult(result));
        Close();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
        => Close();

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            OnRestoreClick(sender, e);
            e.Handled = true;
        }
    }

    private sealed class SwitcherCardRow
    {
        public string Name { get; init; } = "";
        public string Info { get; init; } = "";
        public string Health { get; init; } = "";
        public string Color { get; init; } = "#60CDFF";
        public System.Windows.Media.Brush ColorBrush
        {
            get
            {
                try { return (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(Color)!; }
                catch { return System.Windows.Media.Brushes.DeepSkyBlue; }
            }
        }

        public System.Windows.Media.Brush BorderBrush => Health.Equals("Ready", StringComparison.OrdinalIgnoreCase)
            ? System.Windows.Media.Brushes.DeepSkyBlue
            : System.Windows.Media.Brushes.Goldenrod;
    }
}
