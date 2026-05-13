using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FloatingTaskbarMenu.Core;
using FloatingTaskbarMenu.Models;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingImageFormat = System.Drawing.Imaging.ImageFormat;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;

namespace FloatingTaskbarMenu.Windows;

public partial class WorkspaceControlCenterWindow : Window
{
    private readonly WindowManager _windowManager;
    private readonly SettingsService _settingsService;
    private readonly WorkspaceService _workspaceService = new();
    private readonly WorkspaceRuleService _ruleService = new();
    private readonly WorkspaceAnalysisService _analysisService;
    private readonly WorkspaceTimelineService _timelineService = new();
    private readonly WorkspaceVersionService _versionService = new();
    private readonly WorkspaceSuggestionService _suggestionService = new();
    private readonly PinnedWindowGroupService _groupService = new();
    private readonly AppLauncherService _launcherService = new();
    private readonly WorkspaceSnapshotService _snapshotService = new();
    private readonly WorkspaceAutoActionService _autoActionService = new();
    private readonly WindowHistoryService _historyService = new();
    private readonly string _screenGalleryDirectory = Path.Combine(AppIdentity.AppDataDirectory, "WorkspaceScreenGallery");

    private Workspace? _selectedWorkspace;
    private WorkspaceRestorePlan? _currentPlan;
    private bool _loading;

    public WorkspaceControlCenterWindow(WindowManager windowManager, SettingsService settingsService)
    {
        _windowManager = windowManager;
        _settingsService = settingsService;
        _analysisService = new WorkspaceAnalysisService(_ruleService);
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            MetadataRestoreModeCombo.ItemsSource = Enum.GetValues<WorkspaceRestoreMode>();
            RestoreModeCombo.ItemsSource = Enum.GetValues<WorkspaceRestoreMode>();
            MetadataCleanupCombo.ItemsSource = Enum.GetValues<WorkspaceCleanupAction>();
            ExtraActionColumn.ItemsSource = Enum.GetValues<WorkspaceCleanupAction>();
            RuleCleanupCombo.ItemsSource = Enum.GetValues<WorkspaceCleanupAction>();
            RuleRestoreModeCombo.ItemsSource = Enum.GetValues<WorkspaceRestoreMode>();
            RulePositionCombo.ItemsSource = Enum.GetValues<WorkspaceDefaultPosition>();
            MetadataRestoreModeCombo.SelectedItem = WorkspaceRestoreMode.Full;
            RestoreModeCombo.SelectedItem = WorkspaceRestoreMode.Full;
            MetadataCleanupCombo.SelectedItem = WorkspaceCleanupAction.Minimize;
            RuleCleanupCombo.SelectedItem = WorkspaceCleanupAction.Minimize;
            RuleRestoreModeCombo.SelectedItem = WorkspaceRestoreMode.Full;
            RulePositionCombo.SelectedItem = WorkspaceDefaultPosition.None;
            CaptureNameBox.Text = $"Workspace {DateTime.Now:MMM d HH-mm}";
            SnapshotNameBox.Text = $"Snapshot {DateTime.Now:MMM d HH-mm}";
            RefreshAll();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Workspace Control Center could not finish loading: {ex.Message}";
            ThemedMessageBox.Show(
                this,
                $"Workspace Control Center could not finish loading.\n\n{ex.Message}",
                "Workspace Control Center",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void RefreshAll()
    {
        RefreshSuggestionsFromCurrentWindows();
        RefreshWorkspaceList();
        RefreshRules();
        RefreshGroups();
        RefreshLauncherTags();
        RefreshSuggestions();
        RefreshScreenGallery();
    }

    private void RefreshWorkspaceList()
    {
        var currentMonitors = _windowManager.GetMonitors();
        var cards = _workspaceService.GetWorkspaceNames()
            .Select(name =>
            {
                var workspace = _workspaceService.LoadWorkspace(name);
                var preview = _workspaceService.BuildPreview(workspace, currentMonitors);
                var formatted = WorkspacePreviewFormatter.Format(preview);
                var lastRestore = _timelineService.GetEvents(name, 1).FirstOrDefault(e => e.EventType.Contains("restore", StringComparison.OrdinalIgnoreCase));
                return new WorkspaceCardRow
                {
                    Name = workspace.Name,
                    Info = formatted.Info,
                    Health = formatted.Health,
                    Notes = workspace.Metadata.Notes,
                    Color = workspace.Metadata.Color,
                    Thumbnail = LoadGalleryImage(workspace.Metadata.ScreenGallery?.FirstOrDefault()?.ImagePath ?? ""),
                    AppCount = workspace.Items.Count,
                    LastRestore = lastRestore?.CreatedAt.ToString("g") ?? "Never"
                };
            })
            .ToList();

        var selectedName = (WorkspaceList.SelectedItem as WorkspaceCardRow)?.Name;
        WorkspaceList.ItemsSource = cards;
        WorkspaceList.SelectedItem = cards.FirstOrDefault(card => card.Name == selectedName) ?? cards.FirstOrDefault();
    }

    private void SelectWorkspace(string name)
    {
        if (WorkspaceList.ItemsSource is not IEnumerable<WorkspaceCardRow> rows)
            return;

        WorkspaceList.SelectedItem = rows.FirstOrDefault(row => string.Equals(row.Name, name, StringComparison.OrdinalIgnoreCase));
        LoadSelectedWorkspace();
    }

    private void RefreshScreenGallery()
    {
        try
        {
            if (_selectedWorkspace?.Metadata.ScreenGallery?.Count > 0)
            {
                var rows = _selectedWorkspace.Metadata.ScreenGallery
                    .Select(ScreenGalleryRow.FromMemory)
                    .ToList();
                ScreenGalleryItems.ItemsSource = rows;
                DetailsScreenStrip.ItemsSource = rows;
                return;
            }

            var emptyRows = new List<ScreenGalleryRow>
            {
                new()
                {
                    Name = "No saved screens yet",
                    Details = "Use Save New or Update Selected to capture visual memory.",
                    WindowList = _selectedWorkspace == null ? "Pick a workspace first." : "This workspace has layout data, but no saved gallery yet."
                }
            };
            ScreenGalleryItems.ItemsSource = emptyRows;
            DetailsScreenStrip.ItemsSource = emptyRows;
        }
        catch (Exception ex)
        {
            var errorRows = new List<ScreenGalleryRow>
            {
                new()
                {
                    Name = "Screens",
                    Details = "Preview unavailable.",
                    WindowList = ex.Message
                }
            };
            ScreenGalleryItems.ItemsSource = errorRows;
            DetailsScreenStrip.ItemsSource = errorRows;
        }
    }

    private void LoadSelectedWorkspace()
    {
        if (WorkspaceList.SelectedItem is not WorkspaceCardRow card)
        {
            _selectedWorkspace = null;
            OverviewTitle.Text = "Select a workspace";
            OverviewSummary.Text = "";
            CaptureNameBox.Text = $"Workspace {DateTime.Now:MMM d HH-mm}";
            return;
        }

        _loading = true;
        _selectedWorkspace = _workspaceService.LoadWorkspace(card.Name);
        _selectedWorkspace.Metadata ??= new WorkspaceMetadata();
        OverviewTitle.Text = _selectedWorkspace.Name;
        OverviewSummary.Text = $"{card.Info} - {card.Health} - Last opened: {card.LastRestore}";
        CaptureNameBox.Text = _selectedWorkspace.Name;
        SnapshotNameBox.Text = $"{_selectedWorkspace.Name} snapshot {DateTime.Now:MMM d HH-mm}";
        NotesBox.Text = _selectedWorkspace.Metadata.Notes;
        IconKeyBox.Text = _selectedWorkspace.Metadata.IconKey;
        ColorBox.Text = _selectedWorkspace.Metadata.Color;
        MetadataRestoreModeCombo.SelectedItem = _selectedWorkspace.Metadata.DefaultRestoreMode;
        MetadataCleanupCombo.SelectedItem = _selectedWorkspace.Metadata.DefaultCleanupAction;
        AutoActionsBox.Text = string.Join(Environment.NewLine, (_selectedWorkspace.Metadata.AutoActions ?? new List<WorkspaceAutoAction>()).Select(FormatAutoAction));
        RestoreModeCombo.SelectedItem = _selectedWorkspace.Metadata.DefaultRestoreMode;
        VersionCombo.ItemsSource = _versionService.GetVersions(_selectedWorkspace.Name);
        TimelineGrid.ItemsSource = _timelineService.GetEvents(_selectedWorkspace.Name, 80).Select(TimelineRow.FromEvent).ToList();
        _loading = false;
        RefreshScreenGallery();
        PreviewSelectedWorkspace();
    }

    private void PreviewSelectedWorkspace()
    {
        if (_selectedWorkspace == null)
        {
            PlanGrid.ItemsSource = null;
            ExtraGrid.ItemsSource = null;
            return;
        }

        var mode = RestoreModeCombo.SelectedItem is WorkspaceRestoreMode selected
            ? selected
            : _selectedWorkspace.Metadata.DefaultRestoreMode;
        _currentPlan = _analysisService.BuildPlan(_selectedWorkspace, _windowManager.GetWindows(), _windowManager, mode);
        PlanGrid.ItemsSource = _currentPlan.Items.Select(PlanRow.FromPlan).ToList();
        ExtraGrid.ItemsSource = _currentPlan.ExtraWindows.Select(extra => new ExtraRow(extra)).ToList();
        StatusText.Text = $"Plan ready: {_currentPlan.MatchedCount} already open, {_currentPlan.LaunchCount} to open, {_currentPlan.MissingCount} missing, {_currentPlan.ExtraCount} extra.";
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
        => RefreshAll();

    private void OnRefreshScreensClick(object sender, RoutedEventArgs e)
        => RefreshScreenGallery();

    private void OnCaptureWorkspaceClick(object sender, RoutedEventArgs e)
    {
        var name = CaptureNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = $"Workspace {DateTime.Now:MMM d HH-mm}";

        var workspace = _workspaceService.CaptureWorkspace(name, _windowManager.GetWindows(), _settingsService.Settings, _windowManager, _ruleService.GetRules());
        SaveScreenMemory(workspace);
        _timelineService.Record(workspace.Name, "capture", $"Saved workspace with {workspace.Items.Count} window(s).");
        StatusText.Text = $"Saved {workspace.Name}.";
        RefreshAll();
        SelectWorkspace(workspace.Name);
    }

    private void OnUpdateWorkspaceClick(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkspace == null)
        {
            StatusText.Text = "Pick a workspace first.";
            return;
        }

        _versionService.SaveVersionBeforeOverwrite(_selectedWorkspace.Name);
        var workspace = _workspaceService.CaptureWorkspace(_selectedWorkspace.Name, _windowManager.GetWindows(), _settingsService.Settings, _windowManager, _ruleService.GetRules());
        SaveScreenMemory(workspace);
        _timelineService.Record(workspace.Name, "update", $"Updated workspace with {workspace.Items.Count} window(s).");
        StatusText.Text = $"Updated {workspace.Name}.";
        RefreshAll();
        SelectWorkspace(workspace.Name);
    }

    private void OnSaveSnapshotClick(object sender, RoutedEventArgs e)
    {
        var name = SnapshotNameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = $"Snapshot {DateTime.Now:MMM d HH-mm}";

        var snapshot = _snapshotService.SaveManualSnapshot(name, _windowManager.GetWindows(), _settingsService.Settings, _windowManager);
        _timelineService.Record(snapshot.Name, "manual-snapshot", $"Saved snapshot with {snapshot.Items.Count} window(s).");
        SnapshotNameBox.Text = $"{snapshot.Name} copy {DateTime.Now:HH-mm}";
        StatusText.Text = $"Saved snapshot {snapshot.Name}.";
    }

    private void OnWorkspaceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
            return;

        WorkspaceTabs.SelectedIndex = 0;
        LoadSelectedWorkspace();
    }

    private void OnOpenSwitcherClick(object sender, RoutedEventArgs e)
    {
        var switcher = new WorkspaceSwitcherWindow(_windowManager, _settingsService) { Owner = this };
        switcher.Show();
        switcher.Activate();
    }

    private void OnWorkspaceCardRightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            item.IsSelected = true;
            item.Focus();
            item.ContextMenu = CreateWorkspaceContextMenu();
        }
    }

    private ContextMenu CreateWorkspaceContextMenu()
    {
        var menu = new ContextMenu();
        if (TryFindResource("Win11ContextMenuStyle") is Style contextStyle)
            menu.Style = contextStyle;

        menu.Items.Add(CreateWorkspaceMenuItem("Preview", OnWorkspaceMenuPreviewClick));
        menu.Items.Add(CreateWorkspaceMenuItem("Restore", OnWorkspaceMenuRestoreClick));
        menu.Items.Add(CreateWorkspaceMenuItem("Update From Current", OnWorkspaceMenuUpdateClick));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateWorkspaceMenuItem("Rename", OnWorkspaceMenuRenameClick));
        menu.Items.Add(CreateWorkspaceMenuItem("Duplicate", OnWorkspaceMenuDuplicateClick));
        menu.Items.Add(CreateWorkspaceMenuItem("Copy Name", OnWorkspaceMenuCopyNameClick));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateWorkspaceMenuItem("Save Current Snapshot", OnWorkspaceMenuSnapshotClick));
        menu.Items.Add(CreateWorkspaceMenuItem("Open Switcher", OnOpenSwitcherClick));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateWorkspaceMenuItem("Delete", OnWorkspaceMenuDeleteClick));
        return menu;
    }

    private MenuItem CreateWorkspaceMenuItem(string header, RoutedEventHandler handler)
    {
        var item = new MenuItem { Header = header };
        if (TryFindResource("Win11MenuItemStyle") is Style itemStyle)
            item.Style = itemStyle;
        item.Click += handler;
        return item;
    }

    private void OnWorkspaceMenuPreviewClick(object sender, RoutedEventArgs e)
    {
        WorkspaceTabs.SelectedIndex = 2;
        PreviewSelectedWorkspace();
    }

    private void OnWorkspaceMenuRestoreClick(object sender, RoutedEventArgs e)
    {
        WorkspaceTabs.SelectedIndex = 2;
        PreviewSelectedWorkspace();
        OnRestorePlanClick(sender, e);
    }

    private void OnWorkspaceMenuUpdateClick(object sender, RoutedEventArgs e)
        => OnUpdateWorkspaceClick(sender, e);

    private void OnWorkspaceMenuSnapshotClick(object sender, RoutedEventArgs e)
        => OnSaveSnapshotClick(sender, e);

    private void OnWorkspaceMenuCopyNameClick(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkspace == null)
            return;

        try
        {
            System.Windows.Clipboard.SetText(_selectedWorkspace.Name);
            StatusText.Text = $"Copied {_selectedWorkspace.Name}.";
        }
        catch
        {
            StatusText.Text = "Could not copy the workspace name.";
        }
    }

    private void OnWorkspaceMenuDuplicateClick(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkspace == null)
            return;

        var defaultName = UniqueWorkspaceName($"{_selectedWorkspace.Name} Copy");
        var dialog = new InputDialog("Duplicate Workspace", "New name:", defaultName) { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.InputText))
            return;

        var newName = UniqueWorkspaceName(dialog.InputText.Trim());
        var copy = CloneWorkspace(_selectedWorkspace);
        copy.Name = newName;
        copy.CreatedAt = DateTime.Now;
        copy.UpdatedAt = DateTime.Now;
        CopyScreenGallery(copy, _selectedWorkspace.Metadata.ScreenGallery);
        _workspaceService.SaveWorkspace(copy);
        _timelineService.Record(newName, "copy", $"Duplicated workspace from {_selectedWorkspace.Name}.");
        StatusText.Text = $"Duplicated {newName}.";
        RefreshAll();
        SelectWorkspace(newName);
    }

    private void OnWorkspaceMenuRenameClick(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkspace == null)
            return;

        var oldName = _selectedWorkspace.Name;
        var dialog = new InputDialog("Rename Workspace", "New name:", oldName) { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.InputText))
            return;

        var newName = WorkspaceService.NormalizeWorkspaceName(dialog.InputText.Trim());
        if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase))
            return;

        if (_workspaceService.GetWorkspaceNames().Any(name => string.Equals(name, newName, StringComparison.OrdinalIgnoreCase)))
        {
            ThemedMessageBox.Show(this, "A workspace with that name already exists.", "Rename Workspace", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var renamed = CloneWorkspace(_selectedWorkspace);
        renamed.Name = newName;
        renamed.UpdatedAt = DateTime.Now;
        CopyScreenGallery(renamed, _selectedWorkspace.Metadata.ScreenGallery);
        _workspaceService.SaveWorkspace(renamed);
        _workspaceService.DeleteWorkspace(oldName);
        MoveVersionDirectory(oldName, newName);
        DeleteScreenGallery(oldName);
        _timelineService.Record(newName, "rename", $"Renamed workspace from {oldName}.");
        StatusText.Text = $"Renamed to {newName}.";
        RefreshAll();
        SelectWorkspace(newName);
    }

    private void OnWorkspaceMenuDeleteClick(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkspace == null)
            return;

        var name = _selectedWorkspace.Name;
        var result = ThemedMessageBox.Show(
            this,
            $"Delete workspace \"{name}\"?\n\nSaved screen images for this workspace will also be removed.",
            "Delete Workspace",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
            return;

        _workspaceService.DeleteWorkspace(name);
        DeleteScreenGallery(name);
        _timelineService.Record(name, "delete", "Workspace deleted from Control Center.");
        _selectedWorkspace = null;
        StatusText.Text = $"Deleted {name}.";
        RefreshAll();
    }

    private void OnSaveMetadataClick(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkspace == null)
            return;

        _selectedWorkspace.Metadata.Notes = NotesBox.Text.Trim();
        _selectedWorkspace.Metadata.IconKey = IconKeyBox.Text.Trim();
        _selectedWorkspace.Metadata.Color = string.IsNullOrWhiteSpace(ColorBox.Text) ? "#60CDFF" : ColorBox.Text.Trim();
        if (MetadataRestoreModeCombo.SelectedItem is WorkspaceRestoreMode restoreMode)
            _selectedWorkspace.Metadata.DefaultRestoreMode = restoreMode;
        if (MetadataCleanupCombo.SelectedItem is WorkspaceCleanupAction cleanup)
            _selectedWorkspace.Metadata.DefaultCleanupAction = cleanup;
        _selectedWorkspace.Metadata.AutoActions = ParseAutoActions(AutoActionsBox.Text).ToList();

        _versionService.SaveVersionBeforeOverwrite(_selectedWorkspace.Name);
        _workspaceService.SaveWorkspace(_selectedWorkspace);
        _timelineService.Record(_selectedWorkspace.Name, "metadata", "Workspace metadata updated");
        StatusText.Text = "Workspace metadata saved.";
        RefreshWorkspaceList();
    }

    private void OnPreviewClick(object sender, RoutedEventArgs e)
        => PreviewSelectedWorkspace();

    private void OnRestorePlanClick(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkspace == null)
            return;

        if (_currentPlan == null)
            PreviewSelectedWorkspace();

        if (_currentPlan == null)
            return;

        foreach (var row in ExtraGrid.Items.OfType<ExtraRow>())
            row.Apply();

        if (_currentPlan.Mode == WorkspaceRestoreMode.Clean)
        {
            var closeTargets = _currentPlan.ExtraWindows
                .Where(extra => extra.Action == WorkspaceCleanupAction.Close && !extra.IsProtected)
                .Select(extra => string.IsNullOrWhiteSpace(extra.Window.Title) ? extra.Window.ProcessName : extra.Window.Title)
                .ToList();

            if (closeTargets.Count > 0)
            {
                var message = "Clean Restore will close these windows after recording them in history:" +
                              Environment.NewLine + Environment.NewLine +
                              string.Join(Environment.NewLine, closeTargets.Select(title => "- " + title));
                if (ThemedMessageBox.Show(this, message, "Confirm Window Close", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return;
            }
        }

        _snapshotService.CaptureAutomaticRollback(_windowManager.GetWindows(), _settingsService.Settings, _windowManager);
        var result = ExecutePreviewedRestore(_selectedWorkspace, _currentPlan);
        RunAutoActionsWithConfirmations(_selectedWorkspace);
        _timelineService.Record(_selectedWorkspace.Name, $"restore:{_currentPlan.Mode}", RestoreSummary(result), WorkspaceTimelineService.FromRestoreResult(result));
        StatusText.Text = RestoreSummary(result);
        RefreshWorkspaceList();
    }

    private WorkspaceRestoreResult ExecutePreviewedRestore(Workspace workspace, WorkspaceRestorePlan plan)
    {
        if (plan.Mode == WorkspaceRestoreMode.Full)
            return _workspaceService.RestoreWorkspace(workspace, _windowManager);

        if (plan.Mode == WorkspaceRestoreMode.Clean)
        {
            var result = _workspaceService.RestoreWorkspace(workspace, _windowManager);
            _analysisService.ApplyCleanup(plan.ExtraWindows, _windowManager, _historyService, _settingsService);
            return result;
        }

        return _analysisService.ExecutePlan(plan, workspace, _windowManager, _historyService, _settingsService);
    }

    private void OnRestoreVersionClick(object sender, RoutedEventArgs e)
    {
        if (_selectedWorkspace == null || VersionCombo.SelectedItem is not string versionId)
            return;

        var version = _versionService.LoadVersion(_selectedWorkspace.Name, versionId);
        if (version == null)
            return;

        _snapshotService.CaptureAutomaticRollback(_windowManager.GetWindows(), _settingsService.Settings, _windowManager);
        var result = _workspaceService.RestoreWorkspace(version, _windowManager);
        _timelineService.Record(_selectedWorkspace.Name, "restore-version", $"Restored version {versionId}", WorkspaceTimelineService.FromRestoreResult(result));
        StatusText.Text = RestoreSummary(result);
    }

    private void OnSaveRuleClick(object sender, RoutedEventArgs e)
    {
        var process = RuleProcessBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(process))
        {
            StatusText.Text = "Enter a process name for the rule.";
            return;
        }

        var rule = new WorkspaceRule
        {
            ProcessName = process,
            ExcludeFromCapture = RuleExcludeCheck.IsChecked == true,
            NeverCleanup = RuleNeverCleanupCheck.IsChecked == true,
            DefaultCleanupAction = RuleCleanupCombo.SelectedItem is WorkspaceCleanupAction cleanup ? cleanup : WorkspaceCleanupAction.Minimize,
            DefaultRestoreMode = RuleRestoreModeCombo.SelectedItem is WorkspaceRestoreMode mode ? mode : null,
            DefaultPosition = RulePositionCombo.SelectedItem is WorkspaceDefaultPosition position ? position : WorkspaceDefaultPosition.None
        };
        _ruleService.SaveRule(rule);
        _timelineService.Record("", "rule", $"Saved rule for {process}");
        RefreshRules();
        PreviewSelectedWorkspace();
    }

    private void OnDeleteRuleClick(object sender, RoutedEventArgs e)
    {
        if (RulesGrid.SelectedItem is not WorkspaceRule rule)
            return;

        _ruleService.DeleteRule(rule.Id);
        _timelineService.Record("", "rule", $"Deleted rule for {rule.ProcessName}");
        RefreshRules();
        PreviewSelectedWorkspace();
    }

    private void RefreshRules()
        => RulesGrid.ItemsSource = _ruleService.GetRules().ToList();

    private void OnCaptureGroupClick(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(GroupNameBox.Text) ? "Quick Group" : GroupNameBox.Text.Trim();
        var group = _groupService.CaptureGroup(name, _windowManager.GetWindows());
        _timelineService.Record("", "group", $"Captured window group {group.Name}");
        RefreshGroups();
        StatusText.Text = $"Captured group {group.Name}.";
    }

    private void OnLaunchGroupClick(object sender, RoutedEventArgs e)
    {
        if (GroupsList.SelectedItem is not string name)
            return;

        _groupService.LaunchGroup(name);
        _timelineService.Record("", "group", $"Launched window group {name}");
        StatusText.Text = $"Launched group {name}.";
    }

    private void RefreshGroups()
        => GroupsList.ItemsSource = _groupService.GetGroupNames();

    private void RefreshSuggestionsFromCurrentWindows()
    {
        try { _suggestionService.RecordObservation(_windowManager.GetWindows()); } catch { }
    }

    private void RefreshSuggestions()
        => SuggestionsGrid.ItemsSource = _suggestionService.GetActiveSuggestions().Select(SuggestionRow.FromSuggestion).ToList();

    private void OnSaveSuggestionClick(object sender, RoutedEventArgs e)
    {
        if (SuggestionsGrid.SelectedItem is not SuggestionRow row)
            return;

        var defaultName = string.Join(" + ", row.Source.ProcessNames.Take(3));
        var dialog = new InputDialog("Save Suggested Workspace", "Workspace name:", defaultName) { Owner = this };
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.InputText))
            return;

        var names = row.Source.ProcessNames;
        var windows = _windowManager.GetWindows()
            .Where(w => names.Any(name => string.Equals(name, w.ProcessName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        _workspaceService.CaptureWorkspace(dialog.InputText.Trim(), windows, _settingsService.Settings, _windowManager, _ruleService.GetRules());
        _suggestionService.Dismiss(row.Source.Id, neverSuggest: false);
        _timelineService.Record(dialog.InputText.Trim(), "suggestion", "Suggestion accepted as workspace");
        RefreshAll();
    }

    private void OnDismissSuggestionClick(object sender, RoutedEventArgs e)
    {
        if (SuggestionsGrid.SelectedItem is SuggestionRow row)
        {
            _suggestionService.Dismiss(row.Source.Id, neverSuggest: false);
            _timelineService.Record("", "suggestion", $"Dismissed suggestion {row.Apps}");
            RefreshSuggestions();
        }
    }

    private void OnNeverSuggestClick(object sender, RoutedEventArgs e)
    {
        if (SuggestionsGrid.SelectedItem is SuggestionRow row)
        {
            _suggestionService.Dismiss(row.Source.Id, neverSuggest: true);
            _timelineService.Record("", "suggestion", $"Never suggest {row.Apps}");
            RefreshSuggestions();
        }
    }

    private void RefreshLauncherTags()
        => LauncherGrid.ItemsSource = _launcherService.GetAllApps().Select(app => new LauncherTagRow(app)).ToList();

    private void OnSaveLauncherTagsClick(object sender, RoutedEventArgs e)
    {
        foreach (var row in LauncherGrid.Items.OfType<LauncherTagRow>())
            _launcherService.SetTags(row.ExecutablePath, row.TagsText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        _timelineService.Record("", "launcher-tags", "Launcher tags updated");
        RefreshLauncherTags();
        StatusText.Text = "Launcher tags saved.";
    }

    private void RunAutoActionsWithConfirmations(Workspace workspace)
    {
        var actions = workspace.Metadata.AutoActions ?? new List<WorkspaceAutoAction>();
        _autoActionService.RunSafeActions(actions, _settingsService);
        foreach (var action in _autoActionService.DangerousActions(actions))
        {
            var name = string.IsNullOrWhiteSpace(action.Name) ? action.Kind.ToString() : action.Name;
            var message = $"Workspace '{workspace.Name}' wants to run this session/power action:\n\n{name}\n\nConfirm every time before continuing.";
            if (ThemedMessageBox.Show(this, message, "Confirm Workspace Auto Action", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                _autoActionService.RunDangerousAction(action, _settingsService);
        }
    }

    private static string RestoreSummary(WorkspaceRestoreResult result)
        => $"Restored {result.RestoredCount}, launched {result.LaunchedCount}, skipped {result.SkippedCount}, failed {result.FailedCount}.";

    private static string FormatAutoAction(WorkspaceAutoAction action)
        => $"{action.Kind}|{action.Name}|{action.Target}|{action.Arguments}";

    private static IEnumerable<WorkspaceAutoAction> ParseAutoActions(string text)
    {
        foreach (var line in text.Split([Environment.NewLine, "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('|');
            if (parts.Length == 0 || !Enum.TryParse<WorkspaceAutoActionKind>(parts[0].Trim(), ignoreCase: true, out var kind))
                continue;

            yield return new WorkspaceAutoAction
            {
                Kind = kind,
                Name = parts.Length > 1 ? parts[1].Trim() : "",
                Target = parts.Length > 2 ? parts[2].Trim() : "",
                Arguments = parts.Length > 3 ? parts[3].Trim() : "",
                RequiresConfirmation = kind is WorkspaceAutoActionKind.Sleep or WorkspaceAutoActionKind.Shutdown or WorkspaceAutoActionKind.Restart or WorkspaceAutoActionKind.SignOut,
                Enabled = true
            };
        }
    }

    private static bool IsWindowOnMonitor(WindowInfo window, WorkspaceMonitor monitor)
    {
        if (!string.IsNullOrWhiteSpace(window.MonitorId) &&
            string.Equals(window.MonitorId, monitor.Id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var centerX = window.Left + window.Width / 2;
        var centerY = window.Top + window.Height / 2;
        return centerX >= monitor.Left &&
               centerX <= monitor.Left + monitor.Width &&
               centerY >= monitor.Top &&
               centerY <= monitor.Top + monitor.Height;
    }

    private static string WindowLabel(WindowInfo window)
        => string.IsNullOrWhiteSpace(window.Title) ? window.ProcessName : window.Title;

    private void SaveScreenMemory(Workspace workspace)
    {
        try
        {
            workspace.Metadata ??= new WorkspaceMetadata();
            var safeName = StorageHelpers.ToSafeFileName(workspace.Name, "Workspace");
            var targetDirectory = Path.Combine(_screenGalleryDirectory, safeName);
            Directory.CreateDirectory(targetDirectory);

            foreach (var oldFile in Directory.GetFiles(targetDirectory, "*.png"))
            {
                try { File.Delete(oldFile); } catch { }
            }

            var monitors = _windowManager.GetMonitors();
            var windows = _windowManager.GetWindows();
            var capturedAt = DateTime.Now;
            var gallery = new List<WorkspaceScreenMemory>();

            foreach (var item in monitors.Select((monitor, index) => new { monitor, index }))
            {
                var monitorWindows = windows
                    .Where(window => IsWindowOnMonitor(window, item.monitor))
                    .OrderBy(window => WindowLabel(window), StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var fileName = $"screen-{item.index + 1}.png";
                var path = Path.Combine(targetDirectory, fileName);
                CaptureMonitorImage(item.monitor, path);

                gallery.Add(new WorkspaceScreenMemory
                {
                    MonitorId = item.monitor.Id,
                    Name = item.monitor.IsPrimary ? $"Screen {item.index + 1} - main" : $"Screen {item.index + 1}",
                    ImagePath = path,
                    Details = $"{item.monitor.Width:0} x {item.monitor.Height:0} - {monitorWindows.Count} open - saved {capturedAt:g}",
                    WindowList = monitorWindows.Count == 0
                        ? "No tracked windows here."
                        : string.Join(", ", monitorWindows.Take(6).Select(WindowLabel))
                          + (monitorWindows.Count > 6 ? $" and {monitorWindows.Count - 6} more" : ""),
                    CapturedAt = capturedAt
                });
            }

            workspace.Metadata.ScreenGallery = gallery;
            _workspaceService.SaveWorkspace(workspace);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Workspace saved, but screen memory was not saved: {ex.Message}";
        }
    }

    private string UniqueWorkspaceName(string baseName)
    {
        var safeBase = WorkspaceService.NormalizeWorkspaceName(baseName);
        var existing = _workspaceService.GetWorkspaceNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(safeBase))
            return safeBase;

        for (var index = 2; index < 1000; index++)
        {
            var candidate = $"{safeBase} {index}";
            if (!existing.Contains(candidate))
                return candidate;
        }

        return $"{safeBase} {DateTime.Now:yyyyMMdd-HHmmss}";
    }

    private static Workspace CloneWorkspace(Workspace workspace)
        => JsonSerializer.Deserialize<Workspace>(JsonSerializer.Serialize(workspace)) ?? new Workspace { Name = workspace.Name };

    private void CopyScreenGallery(Workspace targetWorkspace, IEnumerable<WorkspaceScreenMemory>? sourceGallery)
    {
        targetWorkspace.Metadata ??= new WorkspaceMetadata();
        var memories = sourceGallery?.ToList() ?? new List<WorkspaceScreenMemory>();
        if (memories.Count == 0)
        {
            targetWorkspace.Metadata.ScreenGallery = new List<WorkspaceScreenMemory>();
            return;
        }

        var targetDirectory = Path.Combine(_screenGalleryDirectory, StorageHelpers.ToSafeFileName(targetWorkspace.Name, "Workspace"));
        Directory.CreateDirectory(targetDirectory);
        var copied = new List<WorkspaceScreenMemory>();

        foreach (var memory in memories)
        {
            var sourcePath = memory.ImagePath;
            var fileName = StorageHelpers.ToSafeFileName(Path.GetFileNameWithoutExtension(sourcePath), "screen") + ".png";
            var targetPath = Path.Combine(targetDirectory, fileName);

            try
            {
                if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
                    File.Copy(sourcePath, targetPath, overwrite: true);
            }
            catch { }

            copied.Add(new WorkspaceScreenMemory
            {
                MonitorId = memory.MonitorId,
                Name = memory.Name,
                ImagePath = File.Exists(targetPath) ? targetPath : "",
                Details = memory.Details,
                WindowList = memory.WindowList,
                CapturedAt = memory.CapturedAt
            });
        }

        targetWorkspace.Metadata.ScreenGallery = copied;
    }

    private void DeleteScreenGallery(string workspaceName)
    {
        try
        {
            var directory = Path.Combine(_screenGalleryDirectory, StorageHelpers.ToSafeFileName(workspaceName, "Workspace"));
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch { }
    }

    private static void MoveVersionDirectory(string oldName, string newName)
    {
        try
        {
            var root = Path.Combine(AppIdentity.AppDataDirectory, "WorkspaceVersions");
            var oldDirectory = Path.Combine(root, WorkspaceService.NormalizeWorkspaceName(oldName));
            var newDirectory = Path.Combine(root, WorkspaceService.NormalizeWorkspaceName(newName));
            if (!Directory.Exists(oldDirectory))
                return;

            if (Directory.Exists(newDirectory))
                Directory.Delete(newDirectory, recursive: true);

            Directory.Move(oldDirectory, newDirectory);
        }
        catch { }
    }

    private static void CaptureMonitorImage(WorkspaceMonitor monitor, string path)
    {
        var left = (int)Math.Round(monitor.Left);
        var top = (int)Math.Round(monitor.Top);
        var width = Math.Max(1, (int)Math.Round(monitor.Width));
        var height = Math.Max(1, (int)Math.Round(monitor.Height));

        using var bitmap = new DrawingBitmap(width, height, DrawingPixelFormat.Format32bppPArgb);
        using (var graphics = DrawingGraphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(left, top, 0, 0, new System.Drawing.Size(width, height));
        }

        bitmap.Save(path, DrawingImageFormat.Png);
    }

    private static BitmapSource? LoadGalleryImage(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 520;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private sealed class ScreenGalleryRow
    {
        public string Name { get; init; } = "";
        public string Details { get; init; } = "";
        public string WindowList { get; init; } = "";
        public BitmapSource? Image { get; init; }
        public string EmptyText => Image == null ? "Screen preview unavailable" : "";

        public static ScreenGalleryRow FromMemory(WorkspaceScreenMemory memory)
            => new()
            {
                Name = memory.Name,
                Details = memory.Details,
                WindowList = memory.WindowList,
                Image = LoadGalleryImage(memory.ImagePath)
            };
    }

    private sealed class WorkspaceCardRow
    {
        public string Name { get; init; } = "";
        public string Info { get; init; } = "";
        public string Health { get; init; } = "";
        public string Notes { get; init; } = "";
        public string Color { get; init; } = "#60CDFF";
        public BitmapSource? Thumbnail { get; init; }
        public string ThumbnailFallback => Thumbnail == null ? "No screen" : "";
        public int AppCount { get; init; }
        public string LastRestore { get; init; } = "";
        public System.Windows.Media.Brush ColorBrush
        {
            get
            {
                try { return (System.Windows.Media.Brush)new BrushConverter().ConvertFromString(Color)!; }
                catch { return System.Windows.Media.Brushes.DeepSkyBlue; }
            }
        }
    }

    private sealed class PlanRow
    {
        public string Kind { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string ProcessName { get; init; } = "";
        public string Confidence { get; init; } = "";
        public string Message { get; init; } = "";

        public static PlanRow FromPlan(WorkspacePlanItem item)
            => new()
            {
                Kind = item.Kind.ToString(),
                DisplayName = item.Item.DisplayName,
                ProcessName = string.IsNullOrWhiteSpace(item.Item.ProcessName) ? item.MatchedWindow?.ProcessName ?? "" : item.Item.ProcessName,
                Confidence = item.MatchConfidence.ToString(),
                Message = item.Message
            };
    }

    private sealed class ExtraRow
    {
        private readonly WorkspaceExtraWindowPlan _plan;

        public ExtraRow(WorkspaceExtraWindowPlan plan)
        {
            _plan = plan;
            Action = plan.Action;
        }

        public string Title => string.IsNullOrWhiteSpace(_plan.Window.Title) ? _plan.Window.ProcessName : _plan.Window.Title;
        public string ProcessName => _plan.Window.ProcessName;
        public string Reason => _plan.Reason;
        public WorkspaceCleanupAction Action { get; set; }
        public void Apply() => _plan.Action = Action;
    }

    private sealed class TimelineRow
    {
        public string CreatedAt { get; init; } = "";
        public string EventType { get; init; } = "";
        public string Summary { get; init; } = "";
        public string Details { get; init; } = "";

        public static TimelineRow FromEvent(WorkspaceTimelineEvent item)
            => new()
            {
                CreatedAt = item.CreatedAt.ToString("g"),
                EventType = item.EventType,
                Summary = item.Summary,
                Details = string.Join("; ", item.Items.Take(3).Select(i => $"{i.DisplayName}: {i.Status}"))
            };
    }

    private sealed class SuggestionRow
    {
        public WorkspaceSuggestion Source { get; init; } = new();
        public string Apps => string.Join(", ", Source.ProcessNames);
        public int SeenCount => Source.SeenCount;
        public string LastSeen => Source.LastSeen.ToString("g");

        public static SuggestionRow FromSuggestion(WorkspaceSuggestion suggestion)
            => new() { Source = suggestion };
    }

    private sealed class LauncherTagRow
    {
        public LauncherTagRow(AppLauncher app)
        {
            Name = app.Name;
            ExecutablePath = app.ExecutablePath;
            TagsText = string.Join(", ", app.Tags ?? new List<string>());
        }

        public string Name { get; }
        public string ExecutablePath { get; }
        public string TagsText { get; set; }
    }
}
