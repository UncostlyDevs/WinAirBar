# WinAirBar Theme Icon Sources

Only visual icon files are bundled here. No scripts, registry files, shell
binaries, sound files, skin packs, installers, DLLs, or system components are
kept in the app.

Risk labels:

- `permissive`: free/open icon source with permissive or Apache-style terms.
- `authentic-noncommercial`: authentic Windows-era visual asset copied for this
  noncommercial retro build; remove before any commercial/relicensed build.
- `GPL`: GPL-licensed source; avoid bundling in proprietary releases unless the
  full release posture is reviewed.
- `GPL-unclear`: source project has no clear repo-local license file or points
  at copyleft/theme-art licensing; treat as noncommercial until reviewed.
- `file-checked`: Wikimedia or similar file checked individually.

## Current Release Policy

This build prioritizes authentic retro visuals for noncommercial use. If WinAirBar
is ever sold, relicensed commercially, or distributed as a clean-room product,
remove every `authentic-noncommercial` asset and keep only default modern themes
plus `permissive` replacements.

## Permissive Pixel Fillers

These small utility icons fill roles that were previously hard-coded as Segoe
Fluent / MDL2 glyphs: history arrows, pin markers, lock, network, volume states,
and sign out. SVGs were rendered locally into transparent PNGs.

Source: Pixelarticons by Gerrit Halfmann / halfmage
URL: https://github.com/halfmage/pixelarticons
License: MIT
Risk label: `permissive`

- `pixel/arrow-left.png` from `svg/arrow-left.svg`
- `pixel/arrow-right.png` from `svg/arrow-right.svg`
- `pixel/pin.png` from `svg/map-pin.svg`
- `pixel/lock.png` from `svg/lock.svg`
- `pixel/network.png` from `svg/wifi.svg`
- `pixel/volume-down.png` from `svg/volume-1.svg`
- `pixel/volume-mute.png` from `svg/volume.svg`
- `pixel/volume-up.png` from `svg/volume-3.svg`

Source: Pictogrammers Memory Icons
URL: https://github.com/Pictogrammers/Memory
License: Pictogrammers Free License / Apache 2.0 for icons
Risk label: `permissive`

- `pixel/logout.png` from `src/svg/logout.svg`

## win31

Source archive: `win31icons.zip`
Risk label: `authentic-noncommercial`

- `win31/history.png` from `icons/clock.png`
- `win31/launcher.png` from `icons/programs.png`
- `win31/settings.png` from `icons/controlpanel.png`
- `win31/power.png` from `icons/exit.png`
- `win31/open-folder.png` from `icons/filemgr.png`
- `win31/volume.png` from `icons/mplayer.png`
- `win31/restart.png` from `icons/msdos.png`
- `win31/sleep.png` from `icons/blankscreen.png`

## win9x

Source archive: `WinRetro_Themes-master (1).zip`
Risk label: `authentic-noncommercial`

- `win9x/history.ico` from `WinRetro_Themes-master/WIN 9X THEME/System Icons/timedate_200.ico`
- `win9x/launcher.ico` from `WinRetro_Themes-master/WIN 9X THEME/System Icons/explorer_100.ico`
- `win9x/settings.ico` from `WinRetro_Themes-master/WIN 9X THEME/System Icons/shell32_137.ico`
- `win9x/power.ico` from `WinRetro_Themes-master/WIN 9X THEME/System Icons/powercfg_205.ico`
- `win9x/volume.ico` from `WinRetro_Themes-master/WIN 9X THEME/System Icons/sndvol32_300.ico`
- `win9x/network.ico` from `WinRetro_Themes-master/WIN 9X THEME/System Icons/rnaapp_100.ico`
- `win9x/open-folder.ico` from `WinRetro_Themes-master/WIN 9X THEME/System Icons/shell32_4.ico`
- `win9x/restart.ico` from `WinRetro_Themes-master/WIN 9X THEME/System Icons/rundll32_100.ico`
- `win9x/sleep.ico` from `WinRetro_Themes-master/WIN 9X THEME/System Icons/desk_100.ico`

## xp

Source archive: `WinRetro_Themes-master (1).zip`
Risk label: `authentic-noncommercial`

- `xp/history.ico` from `WinRetro_Themes-master/WIN XP THEME/System Icons/22.ico`
- `xp/launcher.ico` from `WinRetro_Themes-master/WIN XP THEME/System Icons/326.ico`
- `xp/settings.ico` from `WinRetro_Themes-master/WIN XP THEME/System Icons/137.ico`
- `xp/power.ico` from `WinRetro_Themes-master/WIN XP THEME/System Icons/210.ico`
- `xp/open-folder.ico` from `WinRetro_Themes-master/WIN XP THEME/System Icons/4.ico`
- `xp/volume.png` from `WinRetro_Themes-master/WIN XP THEME/System Icons/audio-volume-muted.png`
- `xp/network.png` from `WinRetro_Themes-master/WIN XP THEME/System Icons/notification-network-wireless.png`
- `xp/restart.ico` from `WinRetro_Themes-master/WIN XP THEME/System Icons/238.ico`
- `xp/sleep.ico` from `WinRetro_Themes-master/WIN XP THEME/System Icons/34.ico`

## win7

Source: B00merang Windows 7 icon theme
URL: https://github.com/B00merang-Artwork/Windows-7
Project licensing page: https://b00merang-project.github.io/licensing
Risk label: `authentic-noncommercial` / `GPL-unclear`

Several upstream entries are Git symlinks. The resolved source path is recorded
where it differs from the requested theme alias.

- `win7/back.png` from `actions/go-previous.png` resolved to `actions/back.png`
- `win7/next.png` from `actions/go-next.png` resolved to `actions/forward.png`
- `win7/settings.png` from `apps/gnome-control-center.png` resolved to `apps/preferences-system.png`
- `win7/history.png` from `apps/clock.png` resolved to `apps/gnome-panel-clock.png`
- `win7/lock.png` from `actions/system-lock-screen.png` resolved to `actions/gnome-lockscreen.png`
- `win7/sleep.png` from `actions/sleep.png` resolved to `actions/system-suspend.png`
- `win7/shutdown.png` from `actions/system-shutdown.png` resolved to `actions/boot.png`
- `win7/restart.png` from `actions/system-restart.png` resolved to `actions/gnome-reboot.png`
- `win7/sign-out.png` from `actions/system-log-out.png` resolved to `actions/gnome-logout.png`
- `win7/volume-down.png` from `status/18/audio-volume-low.png`
- `win7/volume-mute.png` from `status/18/audio-volume-muted.png`
- `win7/volume-up.png` from `status/18/audio-volume-high.png`
- `win7/sound-settings.png` from `apps/multimedia-volume-control.png`
- `win7/network.png` from `devices/network-wireless.png`
- `win7/folder.png` from `apps/system-file-manager.png` resolved to `apps/file-manager.png`
- `win7/launcher.png` from `categories/applications-system.png`

## win10

Source: B00merang Windows 10 icon theme
URL: https://github.com/B00merang-Artwork/Windows-10
Project licensing page: https://b00merang-project.github.io/licensing
Risk label: `authentic-noncommercial` / `GPL-unclear`

Several upstream entries are Git symlinks. The resolved source path is recorded
where it differs from the requested theme alias.

- `win10/back.png` from `24x24/actions/go-previous.png`
- `win10/next.png` from `24x24/actions/go-next.png`
- `win10/settings.png` from `24x24/categories/preferences-system.png`
- `win10/history.png` from `24x24/apps/org.gnome.clocks.png`
- `win10/lock.png` from `24x24/actions/system-lock-screen.png`
- `win10/sleep.png` from `24x24/apps/preferences-system-power-manager.png` resolved to `24x24/apps/gnome-power-manager.png`
- `win10/shutdown.png` from `24x24/actions/application-exit.png`
- `win10/restart.png` from `48x48/actions/system-restart.png` resolved to `48x48/actions/view-refresh.png`
- `win10/sign-out.png` from `24x24/actions/exit.png` resolved to `24x24/actions/application-exit.png`
- `win10/volume-down.png` from `24x24/status/audio-volume-low.png`
- `win10/volume-mute.png` from `24x24/status/audio-volume-muted.png`
- `win10/volume-up.png` from `24x24/status/audio-volume-high.png`
- `win10/sound-settings.png` from `24x24/apps/multimedia-volume-control.png`
- `win10/network.png` from `24x24/devices/network-wireless.png`
- `win10/folder.png` from `24x24/apps/system-file-manager.png`
- `win10/launcher.png` from `24x24/categories/applications-system.png`
- `win10/power.png` from `24x24/apps/preferences-system-power.png` resolved to `24x24/apps/gnome-power-manager.png`

