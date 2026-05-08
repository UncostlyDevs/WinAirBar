using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WpfButton = System.Windows.Controls.Button;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfUserControl = System.Windows.Controls.UserControl;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfImage = System.Windows.Controls.Image;
using FloatingTaskbarMenu.Core;
using FloatingTaskbarMenu.Models;
using FloatingTaskbarMenu.Windows;
using Forms = System.Windows.Forms;

namespace FloatingTaskbarMenu.Controls;

public partial class AuxiliaryControls : WpfUserControl
{
    private Popup? _activePopup;
    private readonly AppLauncherService _appLauncherService;
    private readonly BottomActionBarService _bottomActionBarService;
    private SettingsService? _settingsService;
    private WindowHistoryService? _historyService;
    private Action? _settingsApplied;

    public AuxiliaryControls()
    {
        InitializeComponent();
        _appLauncherService = new AppLauncherService();
        _bottomActionBarService = new BottomActionBarService();
        Loaded += (s, e) => RefreshButtons();
    }

    public void SetContext(SettingsService settingsService, WindowHistoryService historyService, Action? settingsApplied = null)
    {
        _settingsService = settingsService;
        _historyService = historyService;
        _settingsApplied = settingsApplied;
        RefreshButtons();
    }

    public void RefreshButtons()
    {
        if (ActionGrid == null || _settingsService == null)
            return;

        _bottomActionBarService.EnsureSlots(_settingsService.Settings);
        ActionGrid.Children.Clear();

        foreach (var slot in _settingsService.Settings.BottomActionSlots.OrderBy(s => s.SlotIndex))
            ActionGrid.Children.Add(CreateSlotButton(slot));
    }

    public void ShowSettingsFlyout(FrameworkElement placementTarget)
        => ShowPopup(placementTarget, CreateSettingsPopup());

    private WpfButton CreateSlotButton(BottomActionSlot slot)
    {
        var button = new WpfButton
        {
            Margin = new Thickness(2, 0, 2, 0),
            Padding = new Thickness(4, 2, 4, 2),
            Height = 52,
            Style = (Style)System.Windows.Application.Current.Resources["AuxiliaryButtonStyle"],
            Tag = slot
        };

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var iconSource = _bottomActionBarService.GetIcon(slot);

        if (iconSource != null)
        {
            var icon = new WpfImage
            {
                Source = iconSource,
                Width = 18,
                Height = 18,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
            stack.Children.Add(icon);
        }
        else
        {
            stack.Children.Add(new TextBlock
            {
                Text = _bottomActionBarService.GetIconText(slot),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrush("TextPrimaryBrush", WpfBrushes.White),
                FontFamily = new WpfFontFamily(_bottomActionBarService.GetIconFontFamily(slot)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            });
        }

        var label = new TextBlock
        {
            Text = _bottomActionBarService.GetDisplayLabel(slot),
            FontSize = 9,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        label.SetResourceReference(TextBlock.FontFamilyProperty, "AirBarFontFamily");
        stack.Children.Add(label);

        button.Content = stack;
        button.Click += (s, e) => ExecuteSlot(slot, button);
        button.MouseRightButtonUp += (s, e) =>
        {
            ShowSlotContextMenu(button, slot);
            e.Handled = true;
        };

        return button;
    }

    private void ExecuteSlot(BottomActionSlot slot, FrameworkElement placementTarget)
    {
        try
        {
            if (slot.ActionKind == BottomActionKind.Custom)
            {
                if (!ConfirmCustomLaunch(slot))
                    return;

                _bottomActionBarService.LaunchCustomTarget(slot);
                return;
            }

            switch (slot.BuiltInAction)
            {
                case BottomBuiltInAction.History:
                    ShowPopup(placementTarget, CreateHistoryPopup());
                    break;
                case BottomBuiltInAction.Launcher:
                case BottomBuiltInAction.FrequentApps:
                    ShowPopup(placementTarget, CreateLauncherPopup());
                    break;
                case BottomBuiltInAction.PowerMenu:
                    ShowPopup(placementTarget, CreatePowerPopup());
                    break;
                case BottomBuiltInAction.Settings:
                    Process.Start(new ProcessStartInfo { FileName = "ms-settings:", UseShellExecute = true });
                    break;
                case BottomBuiltInAction.AirBarSettings:
                    ShowPopup(placementTarget, CreateSettingsPopup());
                    break;
                case BottomBuiltInAction.Lock:
                    Process.Start(new ProcessStartInfo { FileName = "rundll32.exe", Arguments = "user32.dll,LockWorkStation", UseShellExecute = true });
                    break;
                case BottomBuiltInAction.Sleep:
                    RunPowerAction("Sleep", "put this PC to sleep", "rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0");
                    break;
                case BottomBuiltInAction.Shutdown:
                    RunPowerAction("Shutdown", "shut down this PC immediately", "shutdown", "/s /t 0");
                    break;
                case BottomBuiltInAction.Restart:
                    RunPowerAction("Restart", "restart this PC immediately", "shutdown", "/r /t 0");
                    break;
                case BottomBuiltInAction.SignOut:
                    RunPowerAction("Sign out", "sign out of Windows", "shutdown", "/l");
                    break;
                case BottomBuiltInAction.WifiSettings:
                    Process.Start(new ProcessStartInfo { FileName = "ms-settings:network-wifi", UseShellExecute = true });
                    break;
                case BottomBuiltInAction.VolumeMixer:
                    ShowPopup(placementTarget, CreateVolumePopup());
                    break;
                case BottomBuiltInAction.OpenAppDataFolder:
                    Process.Start(new ProcessStartInfo { FileName = _bottomActionBarService.GetAppDataDirectory(), UseShellExecute = true });
                    break;
            }
        }
        catch { }
    }

    private bool ConfirmCustomLaunch(BottomActionSlot slot)
    {
        if (slot.CustomLaunchConfirmed)
            return true;

        var target = string.IsNullOrWhiteSpace(slot.Arguments)
            ? slot.TargetPath
            : $"{slot.TargetPath} {slot.Arguments}";

        var result = System.Windows.MessageBox.Show(
            $"WinAirBar will open this custom target:\n\n{target}\n\nOnly continue if you configured this slot and trust this target.",
            "Confirm Custom Action",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return false;

        slot.CustomLaunchConfirmed = true;
        _settingsService?.Save();
        return true;
    }

    private static void RunPowerAction(string title, string description, string fileName, string arguments)
    {
        var result = System.Windows.MessageBox.Show(
            $"This will {description}.\n\nContinue?",
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
            Process.Start(new ProcessStartInfo { FileName = fileName, Arguments = arguments, UseShellExecute = true });
    }

    private void ShowSlotContextMenu(FrameworkElement placementTarget, BottomActionSlot slot)
    {
        var menu = new ContextMenu();
        if (System.Windows.Application.Current.Resources["Win11ContextMenuStyle"] is Style ctxStyle)
            menu.Style = ctxStyle;

        menu.Items.Add(CreateMenuItem("Edit in WinAirBar Settings", () => ShowPopup(placementTarget, CreateSettingsPopup())));

        var builtInMenu = new MenuItem { Header = "Replace with built-in" };
        if (System.Windows.Application.Current.Resources["Win11MenuItemStyle"] is Style itemStyle)
            builtInMenu.Style = itemStyle;

        foreach (var builtIn in _bottomActionBarService.GetBuiltIns())
            builtInMenu.Items.Add(CreateMenuItem(builtIn.Label, () => ReplaceSlot(slot.SlotIndex, _bottomActionBarService.CreateBuiltInSlot(slot.SlotIndex, builtIn.Id))));

        menu.Items.Add(builtInMenu);

        menu.Items.Add(CreateMenuItem("Replace with custom file or shortcut", () =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Launchable targets|*.exe;*.lnk;*.bat;*.cmd;*.ps1;*.url|All files|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                var label = Path.GetFileNameWithoutExtension(dialog.FileName);
                ReplaceSlot(slot.SlotIndex, _bottomActionBarService.CreateCustomSlot(slot.SlotIndex, dialog.FileName, label));
            }
        }));

        menu.Items.Add(CreateMenuItem("Replace with custom folder", () =>
        {
            using var dialog = new Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
            {
                var label = Path.GetFileName(dialog.SelectedPath.TrimEnd(Path.DirectorySeparatorChar));
                ReplaceSlot(slot.SlotIndex, _bottomActionBarService.CreateCustomSlot(slot.SlotIndex, dialog.SelectedPath, label));
            }
        }));

        menu.Items.Add(CreateMenuItem("Replace with custom URL", () =>
        {
            var dialog = new InputDialog("Custom URL", "Enter a URL or URI:");
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
                ReplaceSlot(slot.SlotIndex, _bottomActionBarService.CreateCustomSlot(slot.SlotIndex, dialog.InputText.Trim(), dialog.InputText.Trim()));
        }));

        menu.Items.Add(CreateMenuItem("Choose custom icon", () =>
        {
            var iconDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Icon files|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.exe;*.lnk|All files|*.*"
            };

            if (iconDialog.ShowDialog() == true)
            {
                slot.CustomIconPath = iconDialog.FileName;
                slot.UseAutoIcon = false;
                SaveAndRefresh();
            }
        }));

        menu.Items.Add(CreateMenuItem("Use auto icon", () =>
        {
            slot.UseAutoIcon = true;
            slot.CustomIconPath = "";
            SaveAndRefresh();
        }));

        menu.Items.Add(CreateMenuItem("Reset to default", () =>
        {
            var defaults = _bottomActionBarService.CreateDefaultSlots();
            ReplaceSlot(slot.SlotIndex, defaults[slot.SlotIndex]);
        }));

        menu.Items.Add(CreateMenuItem("Clear slot", () =>
        {
            ReplaceSlot(slot.SlotIndex, new BottomActionSlot
            {
                SlotIndex = slot.SlotIndex,
                ActionKind = BottomActionKind.Custom,
                DisplayLabel = $"Slot {slot.SlotIndex + 1}",
                TargetPath = ""
            });
        }));

        menu.PlacementTarget = placementTarget;
        menu.IsOpen = true;
    }

    private MenuItem CreateMenuItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        if (System.Windows.Application.Current.Resources["Win11MenuItemStyle"] is Style itemStyle)
            item.Style = itemStyle;
        item.Click += (s, e) => action();
        return item;
    }

    private void ReplaceSlot(int slotIndex, BottomActionSlot replacement)
    {
        if (_settingsService == null)
            return;

        _bottomActionBarService.EnsureSlots(_settingsService.Settings);
        var index = _settingsService.Settings.BottomActionSlots.FindIndex(s => s.SlotIndex == slotIndex);
        replacement.SlotIndex = slotIndex;

        if (index >= 0)
            _settingsService.Settings.BottomActionSlots[index] = replacement;
        else
            _settingsService.Settings.BottomActionSlots.Add(replacement);

        SaveAndRefresh();
    }

    private void SaveAndRefresh()
    {
        if (_settingsService == null)
            return;

        _bottomActionBarService.EnsureSlots(_settingsService.Settings);
        _settingsService.Save();
        RefreshButtons();
    }

    private void CloseActivePopup()
    {
        try
        {
            if (_activePopup != null)
            {
                _activePopup.IsOpen = false;
                _activePopup = null;
            }
        }
        catch { }
    }

    private void ShowPopup(FrameworkElement? placementTarget, UIElement content)
    {
        try
        {
            CloseActivePopup();

            if (placementTarget == null) return;

            var border = new Border
            {
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12),
                Child = content
            };
            border.SetResourceReference(Border.BackgroundProperty, "FlyoutBackgroundBrush");
            border.SetResourceReference(Border.BorderBrushProperty, "MenuBorderBrush");
            border.SetResourceReference(Border.CornerRadiusProperty, "AirBarCornerRadius");
            border.SetResourceReference(Border.BorderThicknessProperty, "AirBarBorderThickness");

            if (Window.GetWindow(this) is TaskbarMenuWindow menuWindow)
                menuWindow.SuppressNextDeactivateClose();

            _activePopup = new Popup
            {
                PlacementTarget = placementTarget,
                Placement = PlacementMode.Top,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                StaysOpen = false,
                Child = border
            };

            _activePopup.Closed += (s, e) => _activePopup = null;
            _activePopup.IsOpen = true;
        }
        catch { }
    }

    private UIElement CreateHistoryPopup()
        => _historyService == null || _settingsService == null
            ? CreateUnavailableText("History unavailable")
            : new HistoryFlyoutView(_historyService, _settingsService.Settings.HistoryFilter);

    private UIElement CreateSettingsPopup()
        => _settingsService == null
            ? CreateUnavailableText("Settings unavailable")
            : new SettingsFlyoutView(_settingsService, () =>
            {
                _settingsApplied?.Invoke();
                RefreshButtons();
            });

    private static TextBlock CreateUnavailableText(string text)
    {
        var block = new TextBlock { Text = text };
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimaryBrush");
        block.SetResourceReference(TextBlock.FontFamilyProperty, "AirBarFontFamily");
        return block;
    }

    private UIElement CreatePowerPopup()
    {
        var stack = new StackPanel { Width = 180 };

        stack.Children.Add(new TextBlock
        {
            Text = "Power",
            Foreground = GetBrush("TextPrimaryBrush", WpfBrushes.White),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 8)
        });

        stack.Children.Add(CreatePopupButton(ThemeIconKind.Lock, "\uE72E", "Lock", () => Process.Start(new ProcessStartInfo { FileName = "rundll32.exe", Arguments = "user32.dll,LockWorkStation", UseShellExecute = true }), false));
        stack.Children.Add(CreatePopupButton(ThemeIconKind.Sleep, "\uE708", "Sleep", () => RunPowerAction("Sleep", "put this PC to sleep", "rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0"), false));
        stack.Children.Add(CreatePopupButton(ThemeIconKind.Shutdown, "\uE7E8", "Shutdown", () => RunPowerAction("Shutdown", "shut down this PC immediately", "shutdown", "/s /t 0"), false));
        stack.Children.Add(CreatePopupButton(ThemeIconKind.Restart, "\uE72C", "Restart", () => RunPowerAction("Restart", "restart this PC immediately", "shutdown", "/r /t 0"), false));
        stack.Children.Add(CreatePopupButton(ThemeIconKind.SignOut, "\uF3B1", "Sign Out", () => RunPowerAction("Sign out", "sign out of Windows", "shutdown", "/l"), false));

        return stack;
    }

    private UIElement CreateVolumePopup()
    {
        var stack = new StackPanel { Width = 172 };

        stack.Children.Add(new TextBlock
        {
            Text = "Volume",
            Foreground = GetBrush("TextPrimaryBrush", WpfBrushes.White),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var controlRow = new UniformGrid
        {
            Rows = 1,
            Columns = 3,
            Margin = new Thickness(0, 0, 0, 8)
        };
        controlRow.Children.Add(CreateVolumeIconButton(ThemeIconKind.VolumeDown, "\uE992", "Volume down", () => SendMediaKey(VkVolumeDown)));
        controlRow.Children.Add(CreateVolumeIconButton(ThemeIconKind.VolumeMute, "\uE74F", "Mute / unmute", () => SendMediaKey(VkVolumeMute)));
        controlRow.Children.Add(CreateVolumeIconButton(ThemeIconKind.VolumeUp, "\uE767", "Volume up", () => SendMediaKey(VkVolumeUp)));
        stack.Children.Add(controlRow);

        stack.Children.Add(CreatePopupButton(ThemeIconKind.SoundSettings, "\uE713", "Sound Settings", () =>
            Process.Start(new ProcessStartInfo { FileName = "ms-settings:sound", UseShellExecute = true }), false));

        return stack;
    }

    private WpfButton CreatePopupButton(ThemeIconKind iconKind, string fallbackGlyph, string label, Action action, bool destructive, bool closeAfterClick = true)
    {
        var button = new WpfButton
        {
            Content = CreateIconLabel(iconKind, fallbackGlyph, label),
            Foreground = destructive ? WpfBrushes.White : GetBrush("AccentTextBrush", WpfBrushes.White),
            Background = destructive ? new WpfSolidColorBrush(WpfColor.FromRgb(180, 60, 60)) : GetBrush("AccentDarkBrush", new WpfSolidColorBrush(WpfColor.FromRgb(0, 120, 212))),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 2, 0, 0),
            FontFamily = GetFontFamily("AirBarFontFamily", "Segoe UI Variable, Segoe UI")
        };
        if (System.Windows.Application.Current.Resources["MenuItemButtonStyle"] is Style buttonStyle)
            button.Style = buttonStyle;

        button.Click += (s, e) =>
        {
            try { action(); } catch { }
            if (closeAfterClick)
                CloseActivePopup();
        };

        return button;
    }

    private WpfButton CreateVolumeIconButton(ThemeIconKind iconKind, string fallbackGlyph, string tooltip, Action action)
    {
        var button = new WpfButton
        {
            Width = 48,
            Height = 40,
            Margin = new Thickness(2),
            Padding = new Thickness(0),
            ToolTip = tooltip,
            Content = CreateThemeIcon(iconKind, fallbackGlyph, 18, "TextPrimaryBrush")
        };

        if (System.Windows.Application.Current.Resources["AuxiliaryButtonStyle"] is Style iconStyle)
            button.Style = iconStyle;

        button.Click += (s, e) =>
        {
            try { action(); } catch { }
            e.Handled = true;
        };

        return button;
    }

    private UIElement CreateLauncherPopup()
    {
        var stack = new StackPanel { Width = 280 };

        stack.Children.Add(new TextBlock
        {
            Text = "App Launcher",
            Foreground = GetBrush("TextPrimaryBrush", WpfBrushes.White),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var apps = _appLauncherService.GetFrequentApps(10);
        foreach (var app in apps)
            stack.Children.Add(CreateAppButton(app));

        if (apps.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "No apps yet. Launch apps from the window list to add them here.",
                Foreground = GetBrush("TextSecondaryBrush", new WpfSolidColorBrush(WpfColor.FromRgb(170, 170, 170))),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });
        }

        return stack;
    }

    private WpfButton CreateAppButton(AppLauncher app)
    {
        var button = new WpfButton
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left,
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 2, 0, 0),
            Background = WpfBrushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        if (System.Windows.Application.Current.Resources["LauncherRowButtonStyle"] is Style rowStyle)
            button.Style = rowStyle;

        var dockPanel = new DockPanel
        {
            LastChildFill = true,
            VerticalAlignment = VerticalAlignment.Center
        };

        var iconSource = app.Icon ?? _bottomActionBarService.GetAssociatedIcon(app.ExecutablePath);
        UIElement iconElement;
        if (iconSource != null)
        {
            iconElement = new WpfImage
            {
                Source = iconSource,
                Width = 22,
                Height = 22,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            RenderOptions.SetBitmapScalingMode(iconElement, BitmapScalingMode.HighQuality);
        }
        else
        {
            iconElement = new Border
            {
                Width = 22,
                Height = 22,
                Margin = new Thickness(0, 0, 10, 0),
                CornerRadius = new CornerRadius(5),
                Background = GetBrush("ItemHoverBrush", new WpfSolidColorBrush(WpfColor.FromRgb(56, 56, 56))),
                Child = CreateThemeIcon(ThemeIconKind.Launcher, "\uE71D", 14, "TextSecondaryBrush")
            };
        }
        DockPanel.SetDock(iconElement, Dock.Left);
        dockPanel.Children.Add(iconElement);

        dockPanel.Children.Add(new TextBlock
        {
            Text = app.Name,
            FontWeight = app.IsPinned ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = GetBrush("TextPrimaryBrush", WpfBrushes.White),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        button.Content = dockPanel;
        button.Click += (s, e) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = app.ExecutablePath, UseShellExecute = true });
                _appLauncherService.RecordLaunch(app);
                CloseActivePopup();
            }
            catch { }
        };

        button.MouseRightButtonUp += (s, e) =>
        {
            var contextMenu = new ContextMenu();
            if (System.Windows.Application.Current.Resources["Win11ContextMenuStyle"] is Style ctxStyle)
                contextMenu.Style = ctxStyle;

            var pinItem = new MenuItem
            {
                Header = app.IsPinned ? "Unpin from Launcher" : "Pin to Launcher"
            };
            if (System.Windows.Application.Current.Resources["Win11MenuItemStyle"] is Style itemStyle)
                pinItem.Style = itemStyle;

            pinItem.Click += (sender, args) =>
            {
                if (app.IsPinned)
                    _appLauncherService.UnpinApp(app.ExecutablePath);
                else
                    _appLauncherService.PinApp(app.ExecutablePath);

                RefreshLauncherPopup();
            };
            contextMenu.Items.Add(pinItem);
            contextMenu.PlacementTarget = button;
            contextMenu.IsOpen = true;
            e.Handled = true;
        };

        return button;
    }

    private void RefreshLauncherPopup()
    {
        if (_activePopup?.Child is not Border border || border.Child is not StackPanel stack)
            return;

        stack.Children.Clear();
        stack.Children.Add(new TextBlock
        {
            Text = "App Launcher",
            Foreground = GetBrush("TextPrimaryBrush", WpfBrushes.White),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 8)
        });

        foreach (var app in _appLauncherService.GetFrequentApps(10))
            stack.Children.Add(CreateAppButton(app));
    }

    private static StackPanel CreateIconLabel(ThemeIconKind iconKind, string fallbackGlyph, string label)
    {
        var panel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (!string.IsNullOrEmpty(label))
        {
            var icon = CreateThemeIcon(iconKind, fallbackGlyph, 14, "AccentTextBrush");
            icon.Margin = new Thickness(0, 0, 7, 0);
            panel.Children.Add(icon);

            panel.Children.Add(new TextBlock
            {
                Text = label,
                FontFamily = GetFontFamily("AirBarFontFamily", "Segoe UI Variable, Segoe UI"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        return panel;
    }

    private static ThemeIconView CreateThemeIcon(ThemeIconKind iconKind, string fallbackGlyph, double size, string foregroundResourceKey)
    {
        var icon = new ThemeIconView
        {
            Kind = iconKind,
            FallbackGlyph = fallbackGlyph,
            IconSize = size,
            GlyphFontSize = size,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        icon.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, foregroundResourceKey);
        return icon;
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private static void SendMediaKey(byte virtualKey)
    {
        try
        {
            keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
            keybd_event(virtualKey, 0, KeyEventFKeyUp, UIntPtr.Zero);
        }
        catch { }
    }

    private static System.Windows.Media.Brush GetBrush(string resourceKey, System.Windows.Media.Brush fallback)
        => System.Windows.Application.Current.Resources[resourceKey] as System.Windows.Media.Brush ?? fallback;

    private static WpfFontFamily GetFontFamily(string resourceKey, string fallback)
        => System.Windows.Application.Current.Resources[resourceKey] as WpfFontFamily ?? new WpfFontFamily(fallback);

    private const uint KeyEventFKeyUp = 0x0002;
    private const byte VkVolumeMute = 0xAD;
    private const byte VkVolumeDown = 0xAE;
    private const byte VkVolumeUp = 0xAF;
}
