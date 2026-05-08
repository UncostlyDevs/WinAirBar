using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using FloatingTaskbarMenu.Models;

namespace FloatingTaskbarMenu.Core;

public class BottomActionBarService
{
    private readonly ThemeIconService _themeIconService = new();

    private static readonly List<BottomBuiltInActionDefinition> BuiltIns =
    [
        new() { Id = BottomBuiltInAction.History, Label = "History", IconText = "\uE81C", IconGlyph = "\uE81C" },
        new() { Id = BottomBuiltInAction.Launcher, Label = "Launcher", IconText = "\uE71D", IconGlyph = "\uE71D" },
        new() { Id = BottomBuiltInAction.PowerMenu, Label = "Power", IconText = "\uE7E8", IconGlyph = "\uE7E8" },
        new() { Id = BottomBuiltInAction.Settings, Label = "Settings", IconText = "\uE713", IconGlyph = "\uE713" },
        new() { Id = BottomBuiltInAction.AirBarSettings, Label = "WinAirBar Settings", IconText = "\uE713", IconGlyph = "\uE713" },
        new() { Id = BottomBuiltInAction.Lock, Label = "Lock", IconText = "\uE72E", IconGlyph = "\uE72E" },
        new() { Id = BottomBuiltInAction.Sleep, Label = "Sleep", IconText = "\uE708", IconGlyph = "\uE708" },
        new() { Id = BottomBuiltInAction.Shutdown, Label = "Shutdown", IconText = "\uE7E8", IconGlyph = "\uE7E8" },
        new() { Id = BottomBuiltInAction.Restart, Label = "Restart", IconText = "\uE72C", IconGlyph = "\uE72C" },
        new() { Id = BottomBuiltInAction.SignOut, Label = "Sign Out", IconText = "\uF3B1", IconGlyph = "\uF3B1" },
        new() { Id = BottomBuiltInAction.WifiSettings, Label = "Wi-Fi", IconText = "\uE701", IconGlyph = "\uE701" },
        new() { Id = BottomBuiltInAction.VolumeMixer, Label = "Volume", IconText = "\uE767", IconGlyph = "\uE767" },
        new() { Id = BottomBuiltInAction.FrequentApps, Label = "Frequent Apps", IconText = "\uE71D", IconGlyph = "\uE71D" },
        new() { Id = BottomBuiltInAction.OpenAppDataFolder, Label = "App Data", IconText = "\uE8B7", IconGlyph = "\uE8B7" }
    ];

    public IReadOnlyList<BottomBuiltInActionDefinition> GetBuiltIns() => BuiltIns;

    public BottomBuiltInActionDefinition GetDefinition(BottomBuiltInAction id)
        => BuiltIns.First(d => d.Id == id);

    public List<BottomActionSlot> CreateDefaultSlots()
    {
        return
        [
            CreateBuiltInSlot(0, BottomBuiltInAction.History),
            CreateBuiltInSlot(1, BottomBuiltInAction.Launcher),
            CreateBuiltInSlot(2, BottomBuiltInAction.PowerMenu),
            CreateBuiltInSlot(3, BottomBuiltInAction.Settings)
        ];
    }

    public void EnsureSlots(Settings settings)
    {
        settings.BottomActionSlots ??= [];

        var defaults = CreateDefaultSlots();
        for (var i = 0; i < 4; i++)
        {
            var existing = settings.BottomActionSlots.FirstOrDefault(s => s.SlotIndex == i);
            if (existing == null)
            {
                settings.BottomActionSlots.Add(defaults[i]);
                continue;
            }

            existing.SlotIndex = i;
            RepairBuiltInLabelMismatch(existing);
            if (existing.ActionKind == BottomActionKind.BuiltIn && string.IsNullOrWhiteSpace(existing.DisplayLabel))
                existing.DisplayLabel = GetDefinition(existing.BuiltInAction).Label;
        }

        settings.BottomActionSlots = settings.BottomActionSlots
            .Where(s => s.SlotIndex >= 0 && s.SlotIndex < 4)
            .OrderBy(s => s.SlotIndex)
            .Take(4)
            .ToList();
    }

    private void RepairBuiltInLabelMismatch(BottomActionSlot slot)
    {
        if (slot.ActionKind != BottomActionKind.BuiltIn || string.IsNullOrWhiteSpace(slot.DisplayLabel))
            return;

        var labelMatch = BuiltIns.FirstOrDefault(b => string.Equals(b.Label, slot.DisplayLabel, StringComparison.OrdinalIgnoreCase));
        if (labelMatch != null && labelMatch.Id != slot.BuiltInAction)
            slot.BuiltInAction = labelMatch.Id;
    }

    public BottomActionSlot CreateBuiltInSlot(int slotIndex, BottomBuiltInAction action)
    {
        var definition = GetDefinition(action);
        return new BottomActionSlot
        {
            SlotIndex = slotIndex,
            ActionKind = BottomActionKind.BuiltIn,
            BuiltInAction = action,
            DisplayLabel = definition.Label,
            UseAutoIcon = true
        };
    }

    public BottomActionSlot CreateCustomSlot(int slotIndex, string targetPath, string label)
    {
        return new BottomActionSlot
        {
            SlotIndex = slotIndex,
            ActionKind = BottomActionKind.Custom,
            DisplayLabel = label,
            TargetPath = targetPath,
            UseAutoIcon = true
        };
    }

    public string GetDisplayLabel(BottomActionSlot slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.DisplayLabel))
            return slot.DisplayLabel;

        return slot.ActionKind == BottomActionKind.BuiltIn
            ? GetDefinition(slot.BuiltInAction).Label
            : Path.GetFileNameWithoutExtension(slot.TargetPath);
    }

    public string GetIconText(BottomActionSlot slot)
    {
        if (slot.ActionKind == BottomActionKind.BuiltIn)
            return GetDefinition(slot.BuiltInAction).IconGlyph;

        var label = GetDisplayLabel(slot);
        return string.IsNullOrWhiteSpace(label) ? "\uE8A5" : label[..1].ToUpperInvariant();
    }

    public string GetIconFontFamily(BottomActionSlot slot)
        => slot.ActionKind == BottomActionKind.BuiltIn
            ? GetDefinition(slot.BuiltInAction).IconFontFamily
            : "Segoe UI Variable, Segoe UI";

    public BitmapSource? GetIcon(BottomActionSlot slot)
    {
        if (!slot.UseAutoIcon && !string.IsNullOrWhiteSpace(slot.CustomIconPath))
            return LoadImageOrAssociatedIcon(slot.CustomIconPath);

        if (slot.ActionKind == BottomActionKind.BuiltIn && slot.UseAutoIcon)
        {
            var themeIcon = _themeIconService.GetIcon(slot.BuiltInAction);
            if (themeIcon != null)
                return themeIcon;
        }

        if (slot.ActionKind == BottomActionKind.Custom && !string.IsNullOrWhiteSpace(slot.TargetPath))
            return LoadImageOrAssociatedIcon(slot.TargetPath);

        return null;
    }

    public BitmapSource? GetAssociatedIcon(string path)
        => LoadImageOrAssociatedIcon(path);

    public void LaunchCustomTarget(BottomActionSlot slot)
    {
        if (string.IsNullOrWhiteSpace(slot.TargetPath))
            return;

        var startInfo = new ProcessStartInfo
        {
            FileName = slot.TargetPath,
            UseShellExecute = true
        };

        if (!string.IsNullOrWhiteSpace(slot.Arguments))
            startInfo.Arguments = slot.Arguments;

        if (!string.IsNullOrWhiteSpace(slot.WorkingDirectory))
            startInfo.WorkingDirectory = slot.WorkingDirectory;

        Process.Start(startInfo);
    }

    public string GetAppDataDirectory()
        => AppIdentity.AppDataDirectory;

    private BitmapSource? LoadImageOrAssociatedIcon(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".ico")
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.UriSource = new Uri(path, UriKind.Absolute);
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.EndInit();
                    image.Freeze();
                    return image;
                }

                using var icon = Icon.ExtractAssociatedIcon(path);
                if (icon != null)
                {
                    var source = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromWidthAndHeight(32, 32));
                    source.Freeze();
                    return source;
                }
            }
        }
        catch { }

        return null;
    }
}
