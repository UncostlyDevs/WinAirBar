using System.Windows;
using FloatingTaskbarMenu.Core;
using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Windows;

public partial class BackupConflictWindow : Window
{
    private readonly List<BackupConflict> _conflicts;

    public BackupConflictWindow(IEnumerable<BackupConflict> conflicts)
    {
        _conflicts = conflicts.Select(Clone).ToList();
        InitializeComponent();
    }

    public List<BackupConflict> Conflicts => _conflicts;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ChoiceColumn.ItemsSource = Enum.GetValues<BackupConflictChoice>();
        ConflictGrid.ItemsSource = _conflicts;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        ConflictGrid.CommitEdit();
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static BackupConflict Clone(BackupConflict conflict)
        => new()
        {
            Section = conflict.Section,
            Name = conflict.Name,
            EntryName = conflict.EntryName,
            TargetPath = conflict.TargetPath,
            Choice = conflict.Choice
        };
}
