# Dock Monitor

A Windows service that automatically detects docking station connect/disconnect events and applies per-profile actions: audio switching, display reset, Bluetooth device reconnection, and USB device restart.

## Features

- **Automatic dock detection** via USB VID/PID polling (WMI)
- **Docked / Undocked profiles** with independent actions for each state
- **Audio device switching** by device name via Windows Core Audio COM API
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
    "Audio": {
      "RenderDefault": "Creative T30 Wireless",
      "RenderMultimedia": "Creative T30 Wireless",
      "RenderCommunications": "Jabra SPEAK 510 USB",
      "CaptureDefault": "Microphone Array",
      "CaptureCommunications": "Jabra SPEAK 510 USB"
    },
    "BluetoothConnect": ["00:02:3C:49:A0:93"],
    "ResetResolution": false
  },

  // Actions when undocked
  "Undocked": {
    "Audio": {
      "RenderDefault": "Realtek(R) Audio",
      "RenderMultimedia": "Realtek(R) Audio",
      "CaptureDefault": "Microphone Array"
    }
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
| `Audio`            | object | null    | Audio device assignments (see Audio Config below) |
| `BluetoothConnect` | array  | []      | MAC addresses of BT devices to reconnect          |
| `ResetResolution`  | bool   | false   | Temporarily change and restore display resolution |
| `TempWidth`        | int    | 1280    | Temporary resolution width                        |
| `TempHeight`       | int    | 1024    | Temporary resolution height                       |
| `RestoreDelayMs`   | int    | 2000    | Delay before restoring original resolution        |

### Audio Config (`Audio`)

Audio devices are matched by friendly name (case-insensitive). Each field is optional — omit or set to `null` to leave that role unchanged.

| Key                      | Description                              |
| ------------------------ | ---------------------------------------- |
| `RenderDefault`          | Default playback device (eConsole)       |
| `RenderMultimedia`       | Multimedia playback device (eMultimedia) |
| `RenderCommunications`   | Communications playback device           |
| `CaptureDefault`         | Default recording device (eConsole)      |
| `CaptureMultimedia`      | Multimedia recording device              |
| `CaptureCommunications`  | Communications recording device          |

Use the `set-audio` wizard to configure interactively:

```
DockMonitor.Service.exe set-audio
```

The wizard lists all active audio devices, then prompts you to pick devices for each role in docked and undocked profiles.

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
| `set-audio`      | Interactive audio device configuration wizard        |
| `restart-bt`     | Restart Bluetooth adapter                            |
| `connect-bt MAC` | Connect a paired Bluetooth device by MAC address     |
| `help`           | Show help                                            |

## Paths

| Path                                      | Description                           |
| ----------------------------------------- | ------------------------------------- |
| `C:\ProgramData\dock-monitor\config.json` | Configuration file                    |
| `C:\ProgramData\dock-monitor\logs\`       | Log files                             |
| `C:\ProgramData\dock-monitor\`            | Data directory                        |

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

