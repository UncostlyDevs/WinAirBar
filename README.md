# WinAirBar

WinAirBar is a lightweight floating Windows taskbar menu for quickly opening active windows, recent windows, pinned or frequent apps, power controls, volume controls, and settings from a compact flyout.

WinAirBar was previously released as AirBar. Version 1.2.0 updates the product name, release metadata, website, and support contact while keeping the same core workflow.

## Features

- Long-press mouse trigger for the floating window list.
- App launcher with pinned and frequent apps.
- Window history and quick reopen support.
- Configurable bottom action bar.
- Power, volume, Wi-Fi, app data, and settings shortcuts.
- Dark, light, and retro Windows-style flyouts and context menus.

## Requirements

- Windows 10 or Windows 11.
- .NET 8 SDK for building from source.

## Build

From the repository root:

```powershell
dotnet restore FloatingTaskbarMenu.csproj
dotnet build FloatingTaskbarMenu.csproj
```

## Run From Source

```powershell
dotnet run --project FloatingTaskbarMenu.csproj
```

WinAirBar runs in the background and places an icon in the Windows notification area. Open Settings from the tray menu or use the configured mouse trigger to open the floating menu.

## Publish A Windows Build

```powershell
dotnet publish FloatingTaskbarMenu.csproj -c Release -r win-x64 --self-contained true
```

The self-contained executable is written under `bin\Release\net8.0-windows\win-x64\publish\`.

To create a checksum for a prebuilt EXE:

```powershell
Get-FileHash .\bin\Release\net8.0-windows\win-x64\publish\WinAirBar.exe -Algorithm SHA256
```

## Security And Privacy

- WinAirBar does not collect telemetry or send app data to a remote service.
- Settings, launcher data, pinned profiles, logs, and window history are stored locally under `%AppData%\WinAirBar`.
- On first v1.2.0 launch, existing AirBar data is copied from `%AppData%\AirBar` when WinAirBar data does not already exist. The old folder is left in place as a backup.
- Autostart uses the current user's Windows Run key only. WinAirBar does not require administrator privileges.
- Power actions ask for confirmation before sleep, shutdown, restart, or sign out.
- Custom action slots ask for confirmation before the first launch of a configured target.
- If you download a prebuilt EXE, verify its SHA256 checksum against the checksum published with the release.

## Source Layout

- `App.xaml` / `App.xaml.cs` - application startup, theme loading, and tray icon.
- `Controls/` - flyout controls for windows, launcher, settings, and bottom actions.
- `Core/` - window tracking, settings, launcher, history, theme, and system helper services.
- `Models/` - serializable app and settings models.
- `Styles/` - shared Windows 11 style resources.
- `Windows/` - WinAirBar windows and dialogs.
- `Assets/` - WinAirBar logo and application icon.

## Project Info

- Website: https://winairbar.com
- Contact: sag@winairbar.com

## License

MIT. See `LICENSE`.
