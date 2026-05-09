<p align="center">
  <img src="Assets/WinAirBarLogo.png" alt="WinAirBar floating Windows taskbar menu logo" width="460">
</p>

<h1 align="center">WinAirBar</h1>

<p align="center">
  <strong>A lightweight floating Windows taskbar menu, window switcher, and app launcher for Windows 10 and Windows 11.</strong>
</p>

<p align="center">
  <a href="https://github.com/UncostlyDevs/WinAirBar/releases/latest">Download latest release</a>
  |
  <a href="https://winairbar.com">Website</a>
  |
  <a href="mailto:sag@winairbar.com">Contact</a>
</p>

<p align="center">
  <img alt="Latest release" src="https://img.shields.io/github/v/release/UncostlyDevs/WinAirBar?label=release">
  <img alt="Windows 10 and 11" src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4">
  <img alt=".NET 8 WPF" src="https://img.shields.io/badge/.NET-8.0-512BD4">
  <img alt="License" src="https://img.shields.io/github/license/UncostlyDevs/WinAirBar">
</p>

## What Is WinAirBar?

WinAirBar is a small Windows productivity utility that gives you a compact floating taskbar-style menu for switching windows, reopening recent windows, launching pinned or frequent apps, and reaching common system controls without digging through the Start menu or taskbar.

It is built for people who want a cleaner Windows desktop workflow: quick window switching, a fast app launcher, local window history, power controls, volume controls, network shortcuts, and customizable action buttons in one lightweight tray app.

WinAirBar was previously released as **AirBar**. Version 1.2.0 renamed the product, updated the release metadata, and safely migrates existing local settings from `%AppData%\AirBar` to `%AppData%\WinAirBar`.

## Why Use It?

- Get a floating Windows taskbar menu from a mouse trigger instead of reaching for the main taskbar.
- Switch between active windows from a compact flyout.
- Reopen recent windows and frequently used apps faster.
- Keep pinned launch shortcuts close without cluttering the desktop.
- Put power, volume, network, settings, and custom actions in one bottom action bar.
- Use retro Windows-inspired themes alongside modern Windows 10/11 styling.
- Keep data local: no telemetry, no account, no cloud sync, no background service calling home.

## Features

### Floating Taskbar Menu

Open a compact taskbar-style flyout with your configured mouse trigger. The menu is designed for quick repeated use: switch windows, jump into launchers, and get back to work.

### Window Switcher

See open windows in a focused list and bring the one you need forward. WinAirBar is useful as a lightweight window switcher for people who bounce between many apps during the day.

### App Launcher

Launch pinned apps, frequent apps, and configured shortcuts from the same menu. This makes WinAirBar a simple Windows app launcher without turning it into a full search box or plugin platform.

### Recent Window History

Track window history locally so you can reopen or return to recent work more easily. History is stored under your user profile, not uploaded anywhere.

### Bottom Action Bar

Configure quick actions for power, restart, sleep, sign out, lock, volume, sound settings, network, folders, app data, settings, and custom launch targets.

### Safety Prompts

Power actions ask for confirmation before shutdown, restart, sleep, or sign out. Custom action slots ask for confirmation before first launch.

### Windows Themes

WinAirBar includes theme assets inspired by Windows 11, Windows 10, Windows 7, Windows XP, Windows 9x, Windows 3.1, and pixel-style utility icons.

## Download

Download the latest Windows x64 release from:

https://github.com/UncostlyDevs/WinAirBar/releases/latest

Current v1.2.0 release asset:

```text
WinAirBar-v1.2.0-win-x64.exe
```

WinAirBar is distributed as a self-contained Windows executable. No installer is required.

Because the executable is currently unsigned, Windows SmartScreen may show a first-run warning. Verify the SHA256 checksum published with the release before running the EXE.

```powershell
Get-FileHash .\WinAirBar-v1.2.0-win-x64.exe -Algorithm SHA256
```

Expected v1.2.0 SHA256:

```text
0F8175C93B8CAAE4F3F5E5C2AB2FA6342BE1534871D25A09799273E6C986FA21
```

## Requirements

- Windows 10 or Windows 11.
- Windows x64 for the published self-contained release.
- .NET 8 SDK only if you want to build from source.

## Build From Source

Clone the repository, then run:

```powershell
dotnet restore FloatingTaskbarMenu.csproj
dotnet build FloatingTaskbarMenu.csproj
```

Run from source:

```powershell
dotnet run --project FloatingTaskbarMenu.csproj
```

Publish a Windows x64 self-contained build:

```powershell
dotnet publish FloatingTaskbarMenu.csproj -c Release -r win-x64 --self-contained true
```

The published executable is written under:

```text
bin\Release\net8.0-windows\win-x64\publish\
```

## Privacy And Security

- WinAirBar does not collect telemetry.
- WinAirBar does not require a user account.
- WinAirBar does not send your app data to a remote service.
- Settings, launcher data, pinned profiles, logs, and window history are stored locally under `%AppData%\WinAirBar`.
- First launch of v1.2.0 copies existing AirBar data from `%AppData%\AirBar` only when WinAirBar data is not already present.
- The old `%AppData%\AirBar` folder is left in place as a backup.
- Autostart uses the current user's Windows Run key only.
- WinAirBar does not require administrator privileges.

Report security issues to:

```text
sag@winairbar.com
```

## Good Fit

WinAirBar may be useful if you searched for:

- Windows taskbar launcher
- floating taskbar menu for Windows
- Windows 11 app launcher
- Windows window switcher
- taskbar alternative for Windows
- system tray productivity tool
- local-first Windows utility
- lightweight WPF desktop app launcher

WinAirBar is intentionally not a full keyboard search launcher, file indexer, or plugin ecosystem. If you want a keyboard-first Spotlight-style search box, another launcher may fit better. If you want a small floating menu for windows, apps, and system actions, WinAirBar is built for that lane.

## Source Layout

- `App.xaml` / `App.xaml.cs` - application startup, theme loading, and tray icon.
- `Controls/` - flyout controls for windows, launcher, settings, history, and bottom actions.
- `Core/` - window tracking, settings, launcher, history, theme, migration, and system helper services.
- `Models/` - serializable app and settings models.
- `Styles/` - shared Windows style resources.
- `Windows/` - WinAirBar windows and dialogs.
- `Assets/` - WinAirBar logo, application icon, and theme icons.
- `release/` - packaged release artifacts and checksums.

## Project Info

- Website: https://winairbar.com
- Contact: sag@winairbar.com
- Latest release: https://github.com/UncostlyDevs/WinAirBar/releases/latest
- License: MIT

## Contributing

Early feedback is welcome. If WinAirBar helps your workflow, opening an issue with your Windows version, use case, and what felt confusing is genuinely useful.

Good first feedback areas:

- Trigger behavior.
- Window switching ergonomics.
- App launcher behavior.
- Theme polish.
- Release/install friction.
- Missing system actions.

## License

WinAirBar is released under the MIT License. See `LICENSE`.
