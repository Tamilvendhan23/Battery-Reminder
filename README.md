# Battery Reminder

A tiny, tray-only battery reminder for Windows. It tells you when to plug in and when to unplug, using an always-on-top banner that shows even over full-screen remote desktop / VDI windows.

No main window, no background service, no telemetry, no network access. Everything is configured from the tray icon.

## Features

- **Three alerts:** critical, low battery, and stop-charging (protects battery health). Each can be set to a level or turned off.
- **High-priority banner:** an always-on-top banner appears at the top centre of the monitor where your mouse is. It does not steal keyboard focus, so you can keep typing. It re-asserts its position every 1.5 s, flashes for critical alerts, updates the percentage live, and closes itself when you plug in (or unplug, for the stop-charging alert). Click it to dismiss.
- **Repeat reminders:** remind once, or every 1 / 2 / 5 / 10 / 15 / 30 minutes while the condition persists.
- **Sound control:** alert sound on/off, plus quiet-hour presets that mute sound only (the banner still appears).
- **Tray icon styles:** battery icon or percentage number, colour-coded by level (blue, amber, red, green when charging).
- **Status banner:** left-click the tray icon (or launch the app a second time) for a 4-second status banner with percentage, charging state, and time remaining.
- **Start with Windows:** one menu checkbox, no admin rights needed.
- **Single instance:** launching a second copy just shows the status banner of the running one.

## Usage

Run `BatteryReminder.exe`. A battery icon appears in the notification area (it may be under the `^` overflow arrow; drag it onto the taskbar to keep it visible).

- **Left-click:** status banner
- **Right-click:** settings menu

### Tray menu

| Item | Options |
|---|---|
| Critical alert | Off, 5, 8, 10, 15, 20 % |
| Low battery alert | Off, 15, 20, 25, 30, 35, 40, 50 % |
| Stop-charging alert | Off, 80, 85, 90, 95, 100 % |
| Repeat reminder | Off (once), every 1 / 2 / 5 / 10 / 15 / 30 min |
| Banner stays | Until dismissed or fixed, 10 / 30 / 60 s |
| Sound | Alert sound on/off, quiet hours: none, 22:00-07:00, 23:00-07:00, 00:00-06:00 |
| Tray icon | Battery, percentage number |
| Send test alert | Shows a test banner |
| Start with Windows | On/off |
| Exit | Closes the app |

Levels that would contradict each other (for example a low level below the critical level) are greyed out automatically.

## Power, memory and storage

Designed to stay out of the way on a laptop:

- **Event-driven:** subscribes to Windows power notifications (battery percentage, AC/DC change, resume from sleep). There is no polling loop; the app sleeps until Windows reports a change.
- **Adaptive safety timer:** a slow fallback check runs every 10 min when far from any alert level, every 3 min within 10 %, and every 30 s within 3 %. On a device with no battery it checks every 15 min.
- **Timers only when needed:** banner timers exist only while a banner is visible. The tray icon is redrawn only when the reading changes.
- **Efficiency mode:** runs in Windows 11 Efficiency mode (EcoQoS), with no background garbage-collection thread and no localization data loaded.
- **Disk:** only `settings.json` (about 1 KB, written when you change a setting) and a capped `error.log` (max 100 KB). No history or cache is stored.

## Requirements

- Windows 10 or Windows 11 (x64)
- To build: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- To run the framework-dependent build: .NET 8 Desktop Runtime

## Build

From the project folder:

```bat
build.bat
```

or

```powershell
.\build.ps1
```

This produces two outputs:

| Folder | Description |
|---|---|
| `publish/` | Self-contained single file. Runs anywhere, but larger because it bundles the .NET runtime. |
| `publish-framework-dependent/` | Very small single file. Requires the .NET 8 Desktop Runtime. Recommended for low disk usage. |

For development:

```bash
dotnet run
```

## Files and settings

| Path | Purpose |
|---|---|
| `%AppData%\BatteryReminder\settings.json` | Your settings (written when you change something in the menu) |
| `%AppData%\BatteryReminder\error.log` | Error log, capped at 100 KB |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` | "Start with Windows" entry (only when enabled) |

To fully remove the app: uncheck "Start with Windows", choose Exit, then delete the exe and the `%AppData%\BatteryReminder` folder.

## Project structure

```
Program.cs                         Entry point, single-instance handling
TrayApp.cs                         Tray icon, settings menu, app lifetime
Models/
  AppSettings.cs                   Settings and validation
  BatteryInfo.cs                   Battery snapshot and status text
Services/
  BatteryMonitorService.cs         Battery reading, alert rules, adaptive fallback timer
  PowerNotificationWindow.cs       Hidden window receiving Windows power events
  SettingsManager.cs               Atomic JSON load/save
  StartupManager.cs                Start-with-Windows registry entry
  NativeMethods.cs                 Win32 interop (power events, topmost, Efficiency mode)
  Log.cs                           Small capped error log
UI/
  AlertPopup.cs                    Always-on-top, no-focus alert banner
  AlertPresenter.cs                Sound plus banner show/update/close logic
  IconHelper.cs                    Renders the tray icon (no handle leaks)
  Palette.cs                       Colours shared by icon and banner
```

## Known limitations

- Exclusive full-screen games (DirectX exclusive mode) can still cover the banner. Normal full-screen apps and remote desktop / VDI windows (RDP, Citrix, etc.) are covered.
- The banner is not shown on the Windows secure desktop (UAC prompt, lock screen).
- Time remaining is Windows' own estimate and is only available while on battery.
- Windows 11 Efficiency mode is applied on Windows 11 only; on Windows 10 the app simply runs normally (it is already idle).

## Troubleshooting

- **No icon visible:** open the `^` overflow in the notification area and drag the icon out. Check `error.log` if the app exits unexpectedly.
- **No alerts on a desktop PC:** expected. Without a battery there is nothing to monitor.
- **Alert did not appear at a threshold:** the alert fires once per crossing. Use "Repeat reminder" to be reminded again, or "Send test alert" to confirm the banner works.
