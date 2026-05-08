# Changelog

## WinAirBar v1.2.0 - New Name, New Home

AirBar is now **WinAirBar**.

This release updates the app's public identity, metadata, website, support
contact, release package names, and Windows-facing branding while keeping the
same lightweight floating taskbar workflow.

### What's New

- Renamed the product from **AirBar** to **WinAirBar**.
- Added the official website: https://winairbar.com
- Added the support/security contact: sag@winairbar.com
- Updated the app title, tray text, splash screen, settings windows, README,
  security policy, license metadata, and package metadata.
- Updated the Windows executable and release package naming for v1.2.0.
- Preserved existing user settings with a safe migration from `%AppData%\AirBar`
  to `%AppData%\WinAirBar`.

### Upgrade Notes

Existing AirBar users should keep their settings, pinned launcher data, profiles,
and window history after launching WinAirBar v1.2.0.

The old `%AppData%\AirBar` folder is left in place as a backup. WinAirBar now
uses `%AppData%\WinAirBar`.

### Download

Use the Windows x64 self-contained EXE:

`WinAirBar-v1.2.0-win-x64.exe`

SHA256:

`0F8175C93B8CAAE4F3F5E5C2AB2FA6342BE1534871D25A09799273E6C986FA21`

Verify in PowerShell:

```powershell
Get-FileHash .\WinAirBar-v1.2.0-win-x64.exe -Algorithm SHA256
```

### Technical Notes

- Version bumped to `1.2.0`.
- Assembly/output name changed to `WinAirBar`.
- Application metadata now points to `https://winairbar.com`.
- Contact and security reporting now use `sag@winairbar.com`.
- Autostart registry naming is migrated from `AirBar` to `WinAirBar` when applicable.
- Internal implementation namespaces are intentionally left stable to avoid unnecessary churn.

