# PyDockMonitor

A Windows utility that automatically switches audio devices when you connect or disconnect your laptop from a docking station. This helps prevent awkward situations where audio continues playing in the wrong location.

## Features

- Automatic audio device switching when docking/undocking
- Real-time monitoring of docking station connection status
- System tray integration for easy access
- Simple configuration process
- No manual device ID configuration needed

## Use Cases

1. **Office Work**
   - Automatically switch to laptop speakers when undocking
   - Switch to docking station speakers when docking
   - Prevent audio from playing in the wrong location

2. **Home Office**
   - Seamless audio transition between work and personal spaces
   - No more forgotten audio playing in another room

## Installation

### Prerequisites

- Windows 10 or later
- At least two audio devices (laptop speakers and docking station/bluetooth speakers)

### Download

Download the latest release from the [Releases](https://github.com/yourusername/pydockmonitor/releases) page and run the executable.

## Configuration

1. Run the application with the `detect` parameter:
```bash
pydockmonitor.exe detect
```

2. Follow the on-screen prompts:
   - Connect your laptop to the docking station
   - Select the audio device to use when docked
   - Disconnect your laptop
   - Select the audio device to use when undocked
   - The configuration will be saved automatically

## Usage

After configuration, the application will:
- Automatically switch audio devices when docking/undocking

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

