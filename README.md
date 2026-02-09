# Dock Monitor

A Windows service that automatically detects docking station connect/disconnect events and applies per-profile actions: audio switching, display reset, Bluetooth device reconnection, and USB device restart.

## Features

- **Automatic dock detection** via USB VID/PID polling (WMI)
- **Docked / Undocked profiles** with independent actions for each state
- **Audio profile switching** via [SoundVolumeView](https://www.nirsoft.net/utils/sound_volume_view.html)
- **Display resolution reset** (workaround for Windows 11 scaling bugs)
- **Bluetooth device reconnect** after docking (via Win32 `BluetoothSetServiceState`)
- **Bluetooth adapter restart** via SetupDi `DICS_PROPCHANGE`
- **USB device restart** for problematic peripherals
- **Runs as a Windows service** or interactively in foreground
- **JSONC configuration** with hot-reload (comments allowed)
- **Structured logging** with Serilog (file + console)

## Requirements

- Windows 10/11 (x64)
- Administrator privileges (for service install and device restart)

## Quick Start

### 1. Download

Grab `DockMonitor.Service.exe` from [Releases](../../releases).

### 2. Detect your dock

```
DockMonitor.Service.exe detect
```

Follow the wizard: disconnect your dock, press Enter, reconnect, press Enter. The wizard identifies dock-specific USB devices and writes them to `config.json`.

### 3. Configure profiles

Edit `C:\ProgramData\dock-monitor\config.json`:

```jsonc
{
  // USB VID/PID tokens that identify your dock
  "DockDevices": [
    { "Token": "VID_17EF&PID_3082", "Name": "Lenovo USB Ethernet" },
    { "Token": "VID_8086&PID_15EF", "Name": "Thunderbolt 3 Dock" }
  ],

  // PnP device instance IDs to restart on dock connect
  "RestartDevices": [],

  // Actions when docked
  "Docked": {
    "AudioProfile": "docked_profile.spr",
    "BluetoothConnect": ["00:02:3C:49:A0:93"],
    "ResetResolution": false
  },

  // Actions when undocked
  "Undocked": {
    "AudioProfile": "undocked_profile.spr"
  },

  "PollIntervalMs": 1000
}
```

### 4. Install as service

```
DockMonitor.Service.exe install
```

## Configuration Reference

### Top-level

| Key              | Type   | Description                                          |
| ---------------- | ------ | ---------------------------------------------------- |
| `DockDevices`    | array  | USB VID/PID tokens that identify the dock            |
| `RestartDevices` | array  | PnP device instance IDs to restart on dock connect   |
| `Docked`         | object | Profile applied when dock is connected               |
| `Undocked`       | object | Profile applied when dock is disconnected            |
| `PollIntervalMs` | int    | USB polling interval in milliseconds (default: 1000) |

### Profile (`Docked` / `Undocked`)

| Key                | Type   | Default | Description                                       |
| ------------------ | ------ | ------- | ------------------------------------------------- |
| `AudioProfile`     | string | null    | SoundVolumeView `.spr` profile file to load       |
| `BluetoothConnect` | array  | []      | MAC addresses of BT devices to reconnect          |
| `ResetResolution`  | bool   | false   | Temporarily change and restore display resolution |
| `TempWidth`        | int    | 1280    | Temporary resolution width                        |
| `TempHeight`       | int    | 1024    | Temporary resolution height                       |
| `RestoreDelayMs`   | int    | 2000    | Delay before restoring original resolution        |

### Audio Profiles

Audio profiles are managed by [SoundVolumeView](https://www.nirsoft.net/utils/sound_volume_view.html). Place `SoundVolumeView.exe` in the data directory (`C:\ProgramData\dock-monitor\`).

1. Connect your dock, configure audio as desired
2. Save profile: `SoundVolumeView.exe /ssaveprofile "C:\ProgramData\dock-monitor\docked_profile.spr"`
3. Disconnect dock, configure audio
4. Save profile: `SoundVolumeView.exe /ssaveprofile "C:\ProgramData\dock-monitor\undocked_profile.spr"`

## CLI Commands

```
DockMonitor.Service.exe [command]
```

| Command          | Description                                          |
| ---------------- | ---------------------------------------------------- |
| `run`            | Run monitor in foreground                            |
| `install`        | Install as Windows service                           |
| `uninstall`      | Remove Windows service                               |
| `detect`         | Interactive dock device detection wizard             |
| `list-usb`       | List all PnP device IDs                              |
| `list-bt`        | List paired Bluetooth devices with connection status |
| `test`           | Exit code 0 if docked, 1 if undocked                 |
| `docked`         | Manually execute docked profile actions              |
| `undocked`       | Manually execute undocked profile actions            |
| `restart-bt`     | Restart Bluetooth adapter                            |
| `connect-bt MAC` | Connect a paired Bluetooth device by MAC address     |
| `help`           | Show help                                            |

## Paths

| Path                                      | Description                           |
| ----------------------------------------- | ------------------------------------- |
| `C:\ProgramData\dock-monitor\config.json` | Configuration file                    |
| `C:\ProgramData\dock-monitor\logs\`       | Log files                             |
| `C:\ProgramData\dock-monitor\`            | Data directory (audio profiles, etc.) |

## Service Management

```cmd
:: Install
DockMonitor.Service.exe install

:: Uninstall
DockMonitor.Service.exe uninstall

:: Manual control
net start DockMonitor
net stop DockMonitor
sc query DockMonitor
```

The service runs as `LocalSystem` and starts automatically on boot. Configuration changes are detected automatically and trigger a service restart.

## Building from Source

```bash
git clone https://github.com/AlexMKX/pydockmonitor.git
cd pydockmonitor

dotnet publish dotnet/DockMonitor.Service/DockMonitor.Service.csproj \
  -c Release -r win-x64 \
  -p:PublishSingleFile=true -p:SelfContained=true
```

Output: `dotnet/DockMonitor.Service/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/DockMonitor.Service.exe`

### Prerequisites

- .NET 8 SDK
- Windows 10+ SDK (for Windows-specific APIs)

## License

MIT

