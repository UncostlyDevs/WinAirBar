using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FloatingTaskbarMenu.Core;
using FloatingTaskbarMenu.Models;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfControl = System.Windows.Controls.Control;
using WpfImage = System.Windows.Controls.Image;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;

namespace FloatingTaskbarMenu.Windows;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly PinnedProfileService _pinnedProfileService;
    private readonly ThemeService _themeService;
    private readonly BottomActionBarService _bottomActionBarService;
    private Settings _settings;
    private readonly string _logFilePath = string.Empty;
    private readonly List<BottomActionSlotEditor> _bottomActionEditors = new();
    private bool _loadingTextColorControls;

    public SettingsWindow(SettingsService settingsService)
    {
        try
        {
            var logDirectory = AppIdentity.AppDataDirectory;
            Directory.CreateDirectory(logDirectory);
            _logFilePath = Path.Combine(logDirectory, "settings_debug.log");
            Log("SettingsWindow constructor started");
        }
        catch { }

        _settingsService = settingsService;
        Log("Creating PinnedProfileService");
        _pinnedProfileService = new PinnedProfileService();
        Log("Creating ThemeService");
        _themeService = new ThemeService();
        _bottomActionBarService = new BottomActionBarService();
        _settings = settingsService.Settings;

        Log("Initializing component");
        _loadingTextColorControls = true;
        InitializeComponent();
        DataContext = _settings;

        Log("Loading UI elements");
        InitializeTriggerButtonSelection();
        Log("Loading profiles");
        LoadProfiles();
        Log("Loading history filters");
        LoadHistoryFilters();
        Log("Loading themes");
        LoadThemes();
        Log("Loading bottom action editors");
        LoadBottomActionEditors();
        RefreshTextColorControls();
        _loadingTextColorControls = false;
        
        Log("Setting up event handlers");
        ProfileComboBox.SelectionChanged += OnProfileSelectionChanged;
        HistoryFilterComboBox.SelectionChanged += OnHistoryFilterSelectionChanged;
        ThemeComboBox.SelectionChanged += OnThemeSelectionChanged;
        
        Log("SettingsWindow constructor completed");
    }

    private void Log(string message)
    {
        try
        {
            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
            File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
        }
        catch { }
    }

    public event EventHandler? SettingsApplied;

    private void InitializeTriggerButtonSelection()
    {
        foreach (var radioButton in TriggerButtonPanel.Children.OfType<System.Windows.Controls.RadioButton>())
        {
            if (radioButton.Tag is MouseButton tag && tag == _settings.TriggerButton)
            {
                radioButton.IsChecked = true;
                break;
            }
        }
    }

    private void LoadProfiles()
    {
        var profiles = _pinnedProfileService.GetProfileNames();
        ProfileComboBox.ItemsSource = profiles;
        ProfileComboBox.SelectedItem = _settings.CurrentPinnedProfile;
    }

    private void LoadHistoryFilters()
    {
        var filters = new[] { "Session", "Day", "Week", "Forever" };
        HistoryFilterComboBox.ItemsSource = filters;
        HistoryFilterComboBox.SelectedItem = _settings.HistoryFilter.ToString();
    }

    private void LoadThemes()
    {
        var themes = _themeService.GetThemeNames();
        _settings.CurrentTheme = ThemeService.NormalizeThemeName(_settings.CurrentTheme);
        ThemeComboBox.ItemsSource = themes;
        ThemeComboBox.SelectedItem = _settings.CurrentTheme;
    }

    private void LoadBottomActionEditors()
    {
        _bottomActionBarService.EnsureSlots(_settings);
        BottomActionsPanel.Children.Clear();
        _bottomActionEditors.Clear();

        foreach (var slot in _settings.BottomActionSlots.OrderBy(s => s.SlotIndex))
        {
            var editor = CreateBottomActionEditor(slot);
            _bottomActionEditors.Add(editor);
            BottomActionsPanel.Children.Add(editor.Container);
        }
    }

    private BottomActionSlotEditor CreateBottomActionEditor(BottomActionSlot slot)
    {
        var builtInLabels = _bottomActionBarService.GetBuiltIns().Select(b => b.Label).ToList();

        var container = new Border
        {
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 0, 0, 8)
        };
        BindResource(container, Border.BackgroundProperty, "MenuBackgroundBrush");
        BindResource(container, Border.BorderBrushProperty, "BorderBrush");
        BindResource(container, Border.CornerRadiusProperty, "AirBarInnerCornerRadius");

        var root = new StackPanel();
        container.Child = root;

        var slotTitle = new TextBlock
        {
            Text = $"Slot {slot.SlotIndex + 1}",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        BindResource(slotTitle, TextBlock.ForegroundProperty, "TextPrimaryBrush");
        BindResource(slotTitle, TextBlock.FontFamilyProperty, "AirBarDisplayFontFamily");
        root.Children.Add(slotTitle);

        var previewPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var previewImage = new WpfImage
        {
            Width = 20,
            Height = 20,
            Margin = new Thickness(0, 0, 8, 0)
        };
        previewPanel.Children.Add(previewImage);
        var previewText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        BindResource(previewText, TextBlock.ForegroundProperty, "TextPrimaryBrush");
        BindResource(previewText, TextBlock.FontFamilyProperty, "AirBarFontFamily");
        previewPanel.Children.Add(previewText);
        root.Children.Add(previewPanel);

        root.Children.Add(MakeLabel("Display Label"));
        var labelBox = MakeTextBox(_bottomActionBarService.GetDisplayLabel(slot));
        root.Children.Add(labelBox);

        root.Children.Add(MakeLabel("Action Type"));
        var actionTypeCombo = MakeComboBox(["BuiltIn", "Custom"], slot.ActionKind == BottomActionKind.BuiltIn ? "BuiltIn" : "Custom");
        root.Children.Add(actionTypeCombo);

        root.Children.Add(MakeLabel("Built-in Action"));
        var builtInCombo = MakeComboBox(builtInLabels, _bottomActionBarService.GetDefinition(slot.BuiltInAction).Label);
        root.Children.Add(builtInCombo);

        root.Children.Add(MakeLabel("Custom Target"));
        var targetBox = MakeTextBox(slot.TargetPath);
        root.Children.Add(targetBox);

        var targetButtons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
        var browseFileButton = MakeSmallButton("Browse File");
        browseFileButton.Click += (s, e) =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Launchable targets|*.exe;*.lnk;*.bat;*.cmd;*.ps1;*.url|All files|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                targetBox.Text = dialog.FileName;
                if (string.IsNullOrWhiteSpace(labelBox.Text))
                    labelBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
                UpdateBottomActionPreview(previewImage, previewText, BuildSlotFromEditor(slot.SlotIndex, labelBox, actionTypeCombo, builtInCombo, targetBox, null, null, null, null));
            }
        };
        targetButtons.Children.Add(browseFileButton);

        var browseFolderButton = MakeSmallButton("Browse Folder");
        browseFolderButton.Margin = new Thickness(8, 0, 0, 0);
        browseFolderButton.Click += (s, e) =>
        {
            using var dialog = new Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                targetBox.Text = dialog.SelectedPath;
                if (string.IsNullOrWhiteSpace(labelBox.Text))
                    labelBox.Text = Path.GetFileName(dialog.SelectedPath.TrimEnd(Path.DirectorySeparatorChar));
                UpdateBottomActionPreview(previewImage, previewText, BuildSlotFromEditor(slot.SlotIndex, labelBox, actionTypeCombo, builtInCombo, targetBox, null, null, null, null));
            }
        };
        targetButtons.Children.Add(browseFolderButton);

        var browseUrlButton = MakeSmallButton("Set URL");
        browseUrlButton.Margin = new Thickness(8, 0, 0, 0);
        browseUrlButton.Click += (s, e) =>
        {
            var dialog = new InputDialog("Custom URL", "Enter a URL or URI:");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
                targetBox.Text = dialog.InputText.Trim();
        };
        targetButtons.Children.Add(browseUrlButton);
        root.Children.Add(targetButtons);

        root.Children.Add(MakeLabel("Arguments"));
        var argumentsBox = MakeTextBox(slot.Arguments);
        root.Children.Add(argumentsBox);

        root.Children.Add(MakeLabel("Working Directory"));
        var workingDirectoryBox = MakeTextBox(slot.WorkingDirectory);
        root.Children.Add(workingDirectoryBox);

        var autoIconCheck = new WpfCheckBox
        {
            Content = "Use auto icon",
            IsChecked = slot.UseAutoIcon,
            Margin = new Thickness(0, 8, 0, 6)
        };
        BindResource(autoIconCheck, WpfControl.ForegroundProperty, "TextPrimaryBrush");
        BindResource(autoIconCheck, WpfControl.FontFamilyProperty, "AirBarFontFamily");
        root.Children.Add(autoIconCheck);

        root.Children.Add(MakeLabel("Custom Icon Path"));
        var iconPathBox = MakeTextBox(slot.CustomIconPath);
        root.Children.Add(iconPathBox);

        var iconButtons = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        var browseIconButton = MakeSmallButton("Browse Icon");
        browseIconButton.Click += (s, e) =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Icon files|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.exe;*.lnk|All files|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                iconPathBox.Text = dialog.FileName;
                autoIconCheck.IsChecked = false;
                UpdateBottomActionPreview(previewImage, previewText, BuildSlotFromEditor(slot.SlotIndex, labelBox, actionTypeCombo, builtInCombo, targetBox, argumentsBox, workingDirectoryBox, autoIconCheck, iconPathBox));
            }
        };
        iconButtons.Children.Add(browseIconButton);

        var resetButton = MakeSmallButton("Reset Slot");
        resetButton.Margin = new Thickness(8, 0, 0, 0);
        resetButton.Click += (s, e) =>
        {
            var defaultSlot = _bottomActionBarService.CreateDefaultSlots()[slot.SlotIndex];
            labelBox.Text = defaultSlot.DisplayLabel;
            actionTypeCombo.SelectedItem = "BuiltIn";
            builtInCombo.SelectedItem = _bottomActionBarService.GetDefinition(defaultSlot.BuiltInAction).Label;
            targetBox.Text = "";
            argumentsBox.Text = "";
            workingDirectoryBox.Text = "";
            autoIconCheck.IsChecked = true;
            iconPathBox.Text = "";
            UpdateBottomActionPreview(previewImage, previewText, defaultSlot);
            UpdateBottomActionEditorVisibility(actionTypeCombo, builtInCombo, targetBox, targetButtons, argumentsBox, workingDirectoryBox, autoIconCheck, iconPathBox, iconButtons);
        };
        iconButtons.Children.Add(resetButton);
        root.Children.Add(iconButtons);

        actionTypeCombo.SelectionChanged += (s, e) =>
            UpdateBottomActionEditorVisibility(actionTypeCombo, builtInCombo, targetBox, targetButtons, argumentsBox, workingDirectoryBox, autoIconCheck, iconPathBox, iconButtons);

        actionTypeCombo.SelectionChanged += (s, e) =>
            UpdateBottomActionPreview(previewImage, previewText, BuildSlotFromEditor(slot.SlotIndex, labelBox, actionTypeCombo, builtInCombo, targetBox, argumentsBox, workingDirectoryBox, autoIconCheck, iconPathBox));
        builtInCombo.SelectionChanged += (s, e) =>
            UpdateBottomActionPreview(previewImage, previewText, BuildSlotFromEditor(slot.SlotIndex, labelBox, actionTypeCombo, builtInCombo, targetBox, argumentsBox, workingDirectoryBox, autoIconCheck, iconPathBox));
        labelBox.TextChanged += (s, e) =>
            UpdateBottomActionPreview(previewImage, previewText, BuildSlotFromEditor(slot.SlotIndex, labelBox, actionTypeCombo, builtInCombo, targetBox, argumentsBox, workingDirectoryBox, autoIconCheck, iconPathBox));
        targetBox.TextChanged += (s, e) =>
            UpdateBottomActionPreview(previewImage, previewText, BuildSlotFromEditor(slot.SlotIndex, labelBox, actionTypeCombo, builtInCombo, targetBox, argumentsBox, workingDirectoryBox, autoIconCheck, iconPathBox));
        iconPathBox.TextChanged += (s, e) =>
            UpdateBottomActionPreview(previewImage, previewText, BuildSlotFromEditor(slot.SlotIndex, labelBox, actionTypeCombo, builtInCombo, targetBox, argumentsBox, workingDirectoryBox, autoIconCheck, iconPathBox));
        autoIconCheck.Checked += (s, e) =>
            UpdateBottomActionPreview(previewImage, previewText, BuildSlotFromEditor(slot.SlotIndex, labelBox, actionTypeCombo, builtInCombo, targetBox, argumentsBox, workingDirectoryBox, autoIconCheck, iconPathBox));
        autoIconCheck.Unchecked += (s, e) =>
            UpdateBottomActionPreview(previewImage, previewText, BuildSlotFromEditor(slot.SlotIndex, labelBox, actionTypeCombo, builtInCombo, targetBox, argumentsBox, workingDirectoryBox, autoIconCheck, iconPathBox));

        UpdateBottomActionPreview(previewImage, previewText, slot);
        UpdateBottomActionEditorVisibility(actionTypeCombo, builtInCombo, targetBox, targetButtons, argumentsBox, workingDirectoryBox, autoIconCheck, iconPathBox, iconButtons);

        return new BottomActionSlotEditor
        {
            Container = container,
            SlotIndex = slot.SlotIndex,
            LabelBox = labelBox,
            ActionTypeCombo = actionTypeCombo,
            BuiltInCombo = builtInCombo,
            TargetBox = targetBox,
            ArgumentsBox = argumentsBox,
            WorkingDirectoryBox = workingDirectoryBox,
            AutoIconCheck = autoIconCheck,
            IconPathBox = iconPathBox,
            PreviewImage = previewImage,
            PreviewText = previewText
        };
    }

    private void UpdateBottomActionEditorVisibility(
        WpfComboBox actionTypeCombo,
        WpfComboBox builtInCombo,
        WpfTextBox targetBox,
        FrameworkElement targetButtons,
        WpfTextBox argumentsBox,
        WpfTextBox workingDirectoryBox,
        WpfCheckBox autoIconCheck,
        WpfTextBox iconPathBox,
        FrameworkElement iconButtons)
    {
        var isBuiltIn = (actionTypeCombo.SelectedItem as string) == "BuiltIn";
        builtInCombo.Visibility = isBuiltIn ? Visibility.Visible : Visibility.Collapsed;
        targetBox.Visibility = isBuiltIn ? Visibility.Collapsed : Visibility.Visible;
        targetButtons.Visibility = isBuiltIn ? Visibility.Collapsed : Visibility.Visible;
        argumentsBox.Visibility = isBuiltIn ? Visibility.Collapsed : Visibility.Visible;
        workingDirectoryBox.Visibility = isBuiltIn ? Visibility.Collapsed : Visibility.Visible;
        autoIconCheck.Visibility = Visibility.Visible;
        iconPathBox.Visibility = Visibility.Visible;
        iconButtons.Visibility = Visibility.Visible;
    }

    private void UpdateBottomActionPreview(WpfImage previewImage, TextBlock previewText, BottomActionSlot slot)
    {
        previewImage.Source = _bottomActionBarService.GetIcon(slot);
        previewText.Text = _bottomActionBarService.GetDisplayLabel(slot);
    }

    private void RefreshBottomActionEditorPreviews()
    {
        foreach (var editor in _bottomActionEditors)
        {
            var slot = BuildSlotFromEditor(
                editor.SlotIndex,
                editor.LabelBox,
                editor.ActionTypeCombo,
                editor.BuiltInCombo,
                editor.TargetBox,
                editor.ArgumentsBox,
                editor.WorkingDirectoryBox,
                editor.AutoIconCheck,
                editor.IconPathBox);

            UpdateBottomActionPreview(editor.PreviewImage, editor.PreviewText, slot);
        }
    }

    private BottomActionSlot BuildSlotFromEditor(
        int slotIndex,
        WpfTextBox labelBox,
        WpfComboBox actionTypeCombo,
        WpfComboBox builtInCombo,
        WpfTextBox? targetBox,
        WpfTextBox? argumentsBox,
        WpfTextBox? workingDirectoryBox,
        WpfCheckBox? autoIconCheck,
        WpfTextBox? iconPathBox)
    {
        var slot = new BottomActionSlot
        {
            SlotIndex = slotIndex,
            DisplayLabel = labelBox.Text.Trim(),
            ActionKind = (actionTypeCombo.SelectedItem as string) == "Custom" ? BottomActionKind.Custom : BottomActionKind.BuiltIn,
            UseAutoIcon = autoIconCheck?.IsChecked != false,
            CustomIconPath = iconPathBox?.Text.Trim() ?? ""
        };

        if (slot.ActionKind == BottomActionKind.BuiltIn)
        {
            var selectedLabel = builtInCombo.SelectedItem as string;
            var selectedDefinition = _bottomActionBarService.GetBuiltIns().FirstOrDefault(b => b.Label == selectedLabel)
                ?? _bottomActionBarService.GetBuiltIns().First();
            slot.BuiltInAction = selectedDefinition.Id;
            if (string.IsNullOrWhiteSpace(slot.DisplayLabel))
                slot.DisplayLabel = selectedDefinition.Label;
        }
        else
        {
            slot.TargetPath = targetBox?.Text.Trim() ?? "";
            slot.Arguments = argumentsBox?.Text.Trim() ?? "";
            slot.WorkingDirectory = workingDirectoryBox?.Text.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(slot.DisplayLabel) && !string.IsNullOrWhiteSpace(slot.TargetPath))
                slot.DisplayLabel = System.IO.Path.GetFileNameWithoutExtension(slot.TargetPath);
        }

        return slot;
    }

    private TextBlock MakeLabel(string text)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 4)
        };
        BindResource(label, TextBlock.ForegroundProperty, "TextPrimaryBrush");
        BindResource(label, TextBlock.FontFamilyProperty, "AirBarFontFamily");
        return label;
    }

    private WpfTextBox MakeTextBox(string text)
    {
        var textBox = new WpfTextBox
        {
            Text = text,
            FontSize = 12,
            Padding = new Thickness(8, 4, 8, 4),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 8)
        };
        BindResource(textBox, WpfControl.BackgroundProperty, "HoverBrush");
        BindResource(textBox, WpfControl.BorderBrushProperty, "BorderBrush");
        BindResource(textBox, WpfControl.ForegroundProperty, "TextPrimaryBrush");
        BindResource(textBox, WpfControl.FontFamilyProperty, "AirBarFontFamily");
        return textBox;
    }

    private WpfComboBox MakeComboBox(IEnumerable<string> items, string selected)
    {
        var combo = new WpfComboBox
        {
            ItemsSource = items.ToList(),
            SelectedItem = selected,
            FontSize = 12,
            Padding = new Thickness(8, 4, 8, 4),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 8)
        };
        BindResource(combo, WpfControl.BackgroundProperty, "HoverBrush");
        BindResource(combo, WpfControl.BorderBrushProperty, "BorderBrush");
        BindResource(combo, WpfControl.ForegroundProperty, "TextPrimaryBrush");
        BindResource(combo, WpfControl.FontFamilyProperty, "AirBarFontFamily");
        BindResource(combo, ItemsControl.ItemContainerStyleProperty, "SettingsComboBoxItemStyle");
        return combo;
    }

    private WpfButton MakeSmallButton(string content)
    {
        return new WpfButton
        {
            Content = content,
            Padding = new Thickness(8, 4, 8, 4),
            Style = (Style)FindResource("MenuItemButtonStyle")
        };
    }

    private static void BindResource(FrameworkElement element, DependencyProperty property, string resourceKey)
        => element.SetResourceReference(property, resourceKey);

    private void OnUseCustomTextColorsChanged(object sender, RoutedEventArgs e)
    {
        if (_loadingTextColorControls)
            return;

        SaveCurrentThemeTextColors(UseCustomTextColorsCheck.IsChecked == true);
        ApplyAndPersistAppearance();
    }

    private void OnPickTextColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button || button.Tag is not string key)
            return;

        using var dialog = new Forms.ColorDialog
        {
            FullOpen = true,
            Color = ToDrawingColor(GetTextColorValue(key))
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
            return;

        var hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        SetTextColorValue(key, hex);
        SaveCurrentThemeTextColors(true);
        RefreshTextColorControls();
        ApplyAndPersistAppearance();
    }

    private void OnTextColorHexChanged(object sender, TextChangedEventArgs e)
    {
        if (_loadingTextColorControls || sender is not WpfTextBox textBox || textBox.Tag is not string key)
            return;

        if (!TryNormalizeHex(textBox.Text, out var hex))
        {
            SetTextColorError(key, "Use #RRGGBB");
            return;
        }

        SetTextColorError(key, "");
        SetTextColorValue(key, hex);
        if (!string.Equals(textBox.Text, hex, StringComparison.Ordinal))
        {
            _loadingTextColorControls = true;
            textBox.Text = hex;
            textBox.CaretIndex = textBox.Text.Length;
            _loadingTextColorControls = false;
        }
        SaveCurrentThemeTextColors(true);
        UseCustomTextColorsCheck.IsChecked = true;
        RefreshTextColorSwatches();
        ApplyAndPersistAppearance();
    }

    private void OnResetTextColorsClick(object sender, RoutedEventArgs e)
    {
        _themeService.ResetCurrentTextColors(_settings);
        RefreshTextColorControls();
        ApplyAndPersistAppearance();
    }

    private void RefreshTextColorControls()
    {
        _loadingTextColorControls = true;
        var colors = _themeService.GetCurrentTextColors(_settings);
        _settings.UseCustomTextColors = colors.Enabled;
        UseCustomTextColorsCheck.IsChecked = colors.Enabled;
        PrimaryColorBox.Text = colors.PrimaryColor;
        SecondaryColorBox.Text = colors.SecondaryColor;
        SetTextColorError("PrimaryColor", "");
        SetTextColorError("SecondaryColor", "");
        RefreshTextColorSwatches();
        _loadingTextColorControls = false;
    }

    private void RefreshTextColorSwatches()
    {
        SetSwatch(PrimaryColorButton, PrimaryColorBox.Text);
        SetSwatch(SecondaryColorButton, SecondaryColorBox.Text);
    }

    private void SetSwatch(WpfButton button, string colorText)
    {
        if (!TryNormalizeHex(colorText, out var hex))
            hex = "#000000";

        try
        {
            var color = (WpfColor)WpfColorConverter.ConvertFromString(hex);
            button.Background = new WpfSolidColorBrush(color);
            button.BorderBrush = new WpfSolidColorBrush(color);
            button.ToolTip = hex;
        }
        catch { }
    }

    private void SetTextColorError(string key, string message)
    {
        var error = key switch
        {
            "PrimaryColor" => PrimaryColorError,
            "SecondaryColor" => SecondaryColorError,
            _ => null
        };

        if (error == null)
            return;

        error.Text = message;
        error.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    private string GetTextColorValue(string key)
        => key switch
        {
            "PrimaryColor" => PrimaryColorBox.Text,
            "SecondaryColor" => SecondaryColorBox.Text,
            _ => "#000000"
        };

    private void SetTextColorValue(string key, string value)
    {
        var box = key switch
        {
            "PrimaryColor" => PrimaryColorBox,
            "SecondaryColor" => SecondaryColorBox,
            _ => null
        };

        if (box == null || string.Equals(box.Text, value, StringComparison.Ordinal))
            return;

        var wasLoading = _loadingTextColorControls;
        _loadingTextColorControls = true;
        box.Text = value;
        box.CaretIndex = box.Text.Length;
        _loadingTextColorControls = wasLoading;
    }

    private void SaveCurrentThemeTextColors(bool enabled)
    {
        var theme = _themeService.LoadTheme(_settings.CurrentTheme);
        var primary = TryNormalizeHex(PrimaryColorBox.Text, out var primaryHex) ? primaryHex : theme.ForegroundColor;
        var secondary = TryNormalizeHex(SecondaryColorBox.Text, out var secondaryHex) ? secondaryHex : theme.SecondaryForegroundColor;
        _themeService.SetCurrentTextColors(_settings, enabled, primary, secondary);
    }

    private static bool TryNormalizeHex(string? value, out string hex)
    {
        hex = "";
        var candidate = (value ?? "").Trim();
        if (candidate.Length == 6)
            candidate = "#" + candidate;

        if (candidate.Length != 7 || candidate[0] != '#')
            return false;

        for (var i = 1; i < candidate.Length; i++)
        {
            if (!Uri.IsHexDigit(candidate[i]))
                return false;
        }

        hex = candidate.ToUpperInvariant();
        return true;
    }

    private static System.Drawing.Color ToDrawingColor(string colorText)
    {
        if (!TryNormalizeHex(colorText, out var hex))
            hex = "#000000";

        return System.Drawing.ColorTranslator.FromHtml(hex);
    }

    private void OnProfileSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileComboBox.SelectedItem is string profileName)
        {
            _settings.CurrentPinnedProfile = profileName;
        }
    }

    private void OnHistoryFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryFilterComboBox.SelectedItem is string filterString && Enum.TryParse<HistoryFilter>(filterString, out var filter))
        {
            _settings.HistoryFilter = filter;
        }
    }

    private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is string themeName)
        {
            _settings.CurrentTheme = ThemeService.NormalizeThemeName(themeName);
            var theme = _themeService.LoadTheme(_settings.CurrentTheme);
            ApplyThemeToSettings(theme);
            DataContext = null;
            DataContext = _settings;
            RefreshTextColorControls();
            RefreshBottomActionEditorPreviews();
            _settingsService.Save();
            SettingsApplied?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyThemeToSettings(Theme theme)
    {
        _settings.DarkMode = theme.DarkMode;
        _settings.AccentColor = theme.AccentColor;
        _settings.CornerRadius = theme.CornerRadius;
        _settings.FontSize = theme.FontSize;
        _settings.MinimalMode = theme.MinimalMode;
        _settings.UseCustomTextColors = _themeService.GetCurrentTextColors(_settings).Enabled;
        _themeService.ApplyThemeResources(_settings);
    }

    private void ApplyCurrentAppearanceResources()
        => _themeService.ApplyThemeResources(_settings);

    private void ApplyAndPersistAppearance()
    {
        ApplyCurrentAppearanceResources();
        _settingsService.Save();
        SettingsApplied?.Invoke(this, EventArgs.Empty);
    }

    private void OnNewProfileClick(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog("New Profile Name", "Enter profile name:");
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            var newProfile = new PinnedProfile { Name = dialog.InputText };
            _pinnedProfileService.SaveProfile(newProfile);
            LoadProfiles();
            ProfileComboBox.SelectedItem = dialog.InputText;
            _settings.CurrentPinnedProfile = dialog.InputText;
        }
    }

    private void OnDeleteProfileClick(object sender, RoutedEventArgs e)
    {
        if (ProfileComboBox.SelectedItem is string profileName && profileName != "Default")
        {
            _pinnedProfileService.DeleteProfile(profileName);
            LoadProfiles();
            ProfileComboBox.SelectedItem = "Default";
            _settings.CurrentPinnedProfile = "Default";
        }
    }

    private void OnTriggerButtonChecked(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton radioButton && radioButton.Tag is MouseButton tag)
        {
            _settings.TriggerButton = tag;
        }
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        CommitBottomActionEditors();
        ApplyAndPersistAppearance();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        CommitBottomActionEditors();
        ApplyAndPersistAppearance();
        Close();
    }

    private void CommitBottomActionEditors()
    {
        var slots = _bottomActionEditors
            .OrderBy(e => e.SlotIndex)
            .Select(e => BuildSlotFromEditor(
                e.SlotIndex,
                e.LabelBox,
                e.ActionTypeCombo,
                e.BuiltInCombo,
                e.TargetBox,
                e.ArgumentsBox,
                e.WorkingDirectoryBox,
                e.AutoIconCheck,
                e.IconPathBox))
            .ToList();

        _settings.BottomActionSlots = slots;
        _bottomActionBarService.EnsureSlots(_settings);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private sealed class BottomActionSlotEditor
    {
        public Border Container { get; init; } = null!;
        public int SlotIndex { get; init; }
        public WpfTextBox LabelBox { get; init; } = null!;
        public WpfComboBox ActionTypeCombo { get; init; } = null!;
        public WpfComboBox BuiltInCombo { get; init; } = null!;
        public WpfTextBox TargetBox { get; init; } = null!;
        public WpfTextBox ArgumentsBox { get; init; } = null!;
        public WpfTextBox WorkingDirectoryBox { get; init; } = null!;
        public WpfCheckBox AutoIconCheck { get; init; } = null!;
        public WpfTextBox IconPathBox { get; init; } = null!;
        public WpfImage PreviewImage { get; init; } = null!;
        public TextBlock PreviewText { get; init; } = null!;
    }
}
