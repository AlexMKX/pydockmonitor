import os
import shutil
import subprocess
import time
import sys
import atexit
from pathlib import Path
import psutil
from retrying import retry

import click
import pywintypes
import win32api
import win32com.client as win32
import win32con
import yaml
from pydantic_settings import BaseSettings
from screeninfo import get_monitors
import win32com.shell.shell as shell
import win32gui
import win32process
import ctypes
from datetime import datetime

from logger import setup_logger
from usb_manager import USBManager
from audio_manager import AudioManager

# Setup logger
logger = setup_logger()

# Define installation path and change working directory
app_dir = os.path.join(os.getenv('APPDATA'), 'dock_monitor')
os.makedirs(app_dir, exist_ok=True)
os.chdir(app_dir)

# Windows Job Object constants
JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000
JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION = 0x00000400
JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK = 0x00001000

# Global variable to store child process
child_process = None

class Config(BaseSettings):
    dock_device: list[str]
    restart_devices: list[str]
    dock_comm: str
    undock_comm: str
    dock_multimedia: str
    undock_multimedia: str
    reset_resolution: bool = False

def reset_resolution():
    """Reset screen resolution"""
    try:
        primary_monitor = next(x for x in get_monitors() if x.is_primary)
        original_width, original_height = primary_monitor.width, primary_monitor.height

        # Fix for Windows 11: window positions lost after monitor disconnect
        shell = win32.gencache.EnsureDispatch('Shell.Application')
        shell.ToggleDesktop()

        devmode = pywintypes.DEVMODEType()
        devmode.Fields = win32con.DM_PELSWIDTH | win32con.DM_PELSHEIGHT

        # Reset to temporary resolution
        temp_width, temp_height = 1280, 1024
        devmode.PelsWidth, devmode.PelsHeight = temp_width, temp_height
        win32api.ChangeDisplaySettings(devmode, 0)
        logger.debug(f"Set temporary resolution to {temp_width}x{temp_height}")
        time.sleep(2)

        # Restore original resolution
        devmode.PelsWidth, devmode.PelsHeight = original_width, original_height
        win32api.ChangeDisplaySettings(devmode, 0)
        logger.debug(f"Resolution restored to {original_width}x{original_height}")

        shell.ToggleDesktop()
        logger.debug("Resolution reset completed successfully")
    except Exception as e:
        logger.error(f"Error resetting resolution: {str(e)}")

def load_config(config_path: str) -> Config:
    """Load configuration"""
    try:
        if not os.path.exists(config_path):
            shutil.copy('config.yml.example', config_path)
            raise FileNotFoundError(f"Created new configuration file: {config_path}")
            
        with open(config_path) as cfile:
            cfg = yaml.safe_load(cfile)
        return Config.model_validate(cfg)
    except Exception as e:
        logger.error(f"Error loading configuration: {e}")
        raise

def on_docked(config: Config, audio_manager: AudioManager):
    """Handle docking station connection"""
    logger.info("Docking station connection detected")
    
    # Restart devices
    for device in config.restart_devices:
        logger.info(f'Restarting device {device}')
        try:
            subprocess.run(f'pnputil /restart-device "{device}"', shell=True, check=True)
        except subprocess.CalledProcessError as e:
            logger.error(f"Error restarting device {device}: {e}")

    # Load audio profile
    if not audio_manager.load_profile('docked_profile.spr'):
        logger.error("Failed to load docking station audio profile")

def on_undocked(config: Config, audio_manager: AudioManager):
    """Handle docking station disconnection"""
    logger.info("Docking station disconnection detected")
    
    # Load audio profile
    if not audio_manager.load_profile('undocked_profile.spr'):
        logger.error("Failed to load standalone mode audio profile")
    
    # Reset resolution if required
    if config.reset_resolution:
        reset_resolution()

def is_hidden_mode():
    """Check if application is running in hidden mode"""
    return len(sys.argv) > 1 and sys.argv[1] == 'hidden'

def is_console_version():
    """Check if running console version"""
    return os.path.basename(sys.executable).lower() == 'pydockmonitor_console.exe'

def create_job_object():
    """Create Windows Job Object for process management"""
    try:
        job = win32api.CreateJobObject(None, "PyDockMonitorJob")
        info = win32api.QueryInformationJobObject(job, win32con.JobObjectExtendedLimitInformation)
        info['BasicLimitInformation']['LimitFlags'] = (
            JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE |
            JOB_OBJECT_LIMIT_DIE_ON_UNHANDLED_EXCEPTION |
            JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK
        )
        win32api.SetInformationJobObject(job, win32con.JobObjectExtendedLimitInformation, info)
        return job
    except Exception as e:
        logger.error(f"Error creating job object: {e}")
        return None

def assign_process_to_job(job, process):
    """Assign process to job object"""
    try:
        win32api.AssignProcessToJobObject(job, process.handle)
        return True
    except Exception as e:
        logger.error(f"Error assigning process to job: {e}")
        return False

def cleanup_child_process():
    """Cleanup function to terminate child process"""
    global child_process
    if child_process:
        try:
            # Get all child processes
            parent = psutil.Process(child_process.pid)
            children = parent.children(recursive=True)
            
            # Terminate all child processes
            for child in children:
                try:
                    child.terminate()
                except psutil.NoSuchProcess:
                    pass
            
            # Terminate parent process
            child_process.terminate()
            try:
                child_process.wait(timeout=3)
            except subprocess.TimeoutExpired:
                child_process.kill()
            
            logger.info("Child process terminated successfully")
        except Exception as e:
            logger.error(f"Error terminating child process: {e}")

def main():
    """Main function"""
    try:
        # If running console version, check for commands
        if is_console_version():
            if len(sys.argv) > 1 and sys.argv[1] == 'install':
                install()
                return
            elif len(sys.argv) > 1 and sys.argv[1] == 'detect':
                detect()
                return
            elif len(sys.argv) > 1 and sys.argv[1] == 'run':
                # Run non-console version
                non_console_exe = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'pydockmonitor.exe')
                if os.path.exists(non_console_exe):
                    global child_process
                    # Start process
                    child_process = subprocess.Popen([non_console_exe])
                    # Register cleanup function
                    atexit.register(cleanup_child_process)
                    logger.info("Started non-console version")
                else:
                    logger.error("Non-console executable not found")
                return
        
        # Load configuration
        config = load_config('config.yml')
        
        # Initialize managers
        usb_manager = USBManager()
        audio_manager = AudioManager()
        
        # Get initial device list
        initial_devices = usb_manager.get_current_devices()
        logger.info(f"Initial devices: {initial_devices}")
        
        # Main monitoring loop
        while True:
            try:
                # Get current devices
                current_devices = usb_manager.get_current_devices()
                
                # Check for device changes
                if current_devices != initial_devices:
                    logger.info(f"Device change detected: {current_devices}")
                    
                    # Check if dock is connected
                    if usb_manager.is_device_present(config.dock_device[0]):
                        logger.info("Docking station connected")
                        on_docked(config, audio_manager)
                    else:
                        logger.info("Docking station disconnected")
                        on_undocked(config, audio_manager)
                    
                    # Update initial devices
                    initial_devices = current_devices
                
                time.sleep(1)
                
            except Exception as e:
                logger.error(f"Error in main loop: {e}")
                time.sleep(5)
                
    except Exception as e:
        logger.error(f"Fatal error: {e}")
    finally:
        # Ensure child process is terminated
        cleanup_child_process()

@click.group(invoke_without_command=True)
@click.pass_context
def cli(ctx):
    """Main command group"""
    if ctx.invoked_subcommand is None:
        try:
            main()
        except Exception as e:
            logger.error(f"Critical error: {e}")
            sys.exit(1)

@cli.command()
def detect():
    """Detect and configure docking station devices"""
    usb_manager = USBManager()
    audio_manager = AudioManager()
    
    click.echo("Connect the docking station and press Enter")
    click.getchar()
    docked = usb_manager.get_current_devices()
    
    click.echo("Disconnect the docking station and press Enter")
    click.getchar()
    undocked = usb_manager.get_current_devices()
    
    difference = list(docked - undocked)
    logger.info(f"Docking station devices detected: {difference}")
    
    click.echo("Configure audio for docking station mode and press Enter")
    input()
    if not audio_manager.save_profile("docked_profile.spr"):
        logger.error("Failed to save docking station audio profile")
        return
    
    click.echo("Configure audio for standalone mode and press Enter")
    input()
    if not audio_manager.save_profile("undocked_profile.spr"):
        logger.error("Failed to save standalone mode audio profile")
        return

def is_task_running(task_name):
    """Check if task is running"""
    try:
        result = subprocess.run(
            f'schtasks /Query /TN "{task_name}"', 
            shell=True, 
            capture_output=True, 
            text=True
        )
        return "Running" in result.stdout
    except subprocess.CalledProcessError:
        return False

@retry(stop_max_attempt_number=10, wait_fixed=1000)
def wait_for_task_stop():
    """Wait for task to stop"""
    if is_task_running("PyDockMonitor"):
        raise Exception("Task is still running")
    return True

def get_other_pydockmonitor_processes():
    """Get list of pydockmonitor.exe processes except current one"""
    current_pid = os.getpid()
    processes = []
    for proc in psutil.process_iter(['pid', 'name']):
        try:
            if proc.info['name'].lower() == 'pydockmonitor.exe' and proc.info['pid'] != current_pid:
                processes.append(proc)
        except (psutil.NoSuchProcess, psutil.AccessDenied):
            pass
    return processes

def install_files():
    """Install program files"""
    try:
        # Define installation paths
        app_dir = os.path.join(os.getenv('APPDATA'), 'dock_monitor')
        os.makedirs(app_dir, exist_ok=True)

        # Check if task is running
        if is_task_running("PyDockMonitor"):
            try:
                subprocess.run('schtasks /End /TN PyDockMonitor', shell=True, check=True)
                logger.info("Requested stop of existing task")
                wait_for_task_stop()
                logger.info("Task stopped successfully")
            except subprocess.CalledProcessError as e:
                logger.error(f"Error stopping task: {e}")
            except Exception as e:
                logger.error(f"Error waiting for task to stop: {e}")

        # Check other running processes
        other_processes = get_other_pydockmonitor_processes()
        for proc in other_processes:
            try:
                proc.terminate()
                try:
                    proc.wait(timeout=3)  # Wait for process to finish
                except psutil.TimeoutExpired:
                    proc.kill()  # Kill process if it doesn't finish
                logger.info(f"Terminated pydockmonitor.exe process (PID: {proc.pid})")
            except psutil.NoSuchProcess:
                pass
            except Exception as e:
                logger.error(f"Error terminating process {proc.pid}: {e}")

        # Check existing config.yml
        existing_config = None
        if os.path.exists('config.yml'):
            try:
                with open('config.yml') as cfile:
                    existing_config = yaml.safe_load(cfile)
                Config.model_validate(existing_config)
                logger.info("Existing config.yml loaded successfully")
            except Exception as e:
                logger.error(f"Error loading existing config.yml: {e}")
                existing_config = None

        # List of files to copy
        files_to_copy = [
            'SoundVolumeView.exe',
            'libusb-1.0.dll',
            'config.yml.example'
        ]

        # Copy files
        for file in files_to_copy:
            src = os.path.join(os.path.dirname(os.path.abspath(__file__)), file)
            if os.path.exists(src):
                try:
                    shutil.copy2(src, file)
                    logger.info(f"Copied file: {file}")
                except PermissionError:
                    logger.error(f"No access to file: {file}")
                    raise
            else:
                logger.error(f"File not found: {file}")
                raise FileNotFoundError(f"File not found: {file}")

        # Copy non-console version
        if is_console_version():
            non_console_exe = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'pydockmonitor.exe')
            if os.path.exists(non_console_exe):
                try:
                    shutil.copy2(non_console_exe, 'pydockmonitor.exe')
                    logger.info("Copied non-console version")
                except PermissionError:
                    logger.error("No access to non-console executable")
                    raise
            else:
                logger.error("Non-console executable not found")
                raise FileNotFoundError("Non-console executable not found")
            
            # Copy console version itself
            if getattr(sys, 'frozen', False):
                # Если мы запущены как скомпилированный exe
                console_exe = sys.executable
            else:
                # Если мы запущены как Python скрипт
                console_exe = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'pydockmonitor_console.exe')
            
            if os.path.exists(console_exe):
                try:
                    shutil.copy2(console_exe, 'pydockmonitor_console.exe')
                    logger.info("Copied console version")
                except PermissionError:
                    logger.error("No access to console executable")
                    raise
            else:
                logger.error("Console executable not found")
                raise FileNotFoundError("Console executable not found")

        # Create configuration file if it doesn't exist or if existing is invalid
        if not existing_config and not os.path.exists('config.yml'):
            shutil.copy2('config.yml.example', 'config.yml')
            logger.info("Created configuration file")

        return app_dir
    except Exception as e:
        logger.error(f"Error installing files: {e}")
        raise

@cli.command()
def install():
    """Install the program"""
    try:
        # Install files
        app_dir = install_files()
        logger.info("Files installed successfully")

        # Install scheduled task
        install_scheduled_task()
        logger.info("Task added to scheduler successfully")

        click.echo("Installation completed successfully")
    except Exception as e:
        logger.error(f"Installation error: {e}")
        click.echo(f"Installation error: {e}", err=True)
        sys.exit(1)

def install_scheduled_task():
    """Install Windows scheduled task"""
    try:
        # Define paths
        app_dir = os.path.join(os.getenv('APPDATA'), 'dock_monitor')
        exe_path = os.path.join(app_dir, 'pydockmonitor.exe')  # Используем неконсольную версию
        
        # Create task XML
        task_xml = f"""<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{os.getenv('USERNAME')}</UserId>
    </LogonTrigger>
    <CalendarTrigger>
      <Enabled>true</Enabled>
      <StartBoundary>2025-01-01T00:00:00</StartBoundary>
      <ScheduleByDay>
        <DaysInterval>1</DaysInterval>
      </ScheduleByDay>
      <Repetition>
        <Interval>PT5M</Interval>
      </Repetition>
    </CalendarTrigger>
  </Triggers>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>true</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <DisallowStartOnRemoteAppSession>false</DisallowStartOnRemoteAppSession>
    <UseUnifiedSchedulingEngine>true</UseUnifiedSchedulingEngine>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>{exe_path}</Command>
      <WorkingDirectory>{app_dir}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>"""

        # Save XML to temporary file
        task_xml_path = os.path.join(app_dir, 'task.xml')
        with open(task_xml_path, 'w', encoding='utf-16') as f:
            f.write(task_xml)
        
        # Create task via XML
        cmd = f'schtasks /Create /TN PyDockMonitor /XML "{task_xml_path}" /F'
        result = subprocess.run(cmd, shell=True, capture_output=True, text=True)
        
        # Remove temporary XML file
        os.remove(task_xml_path)
        
        if result.returncode != 0:
            logger.error(f"Error creating task: {result.stderr}")
            raise Exception(f"Error creating task: {result.stderr}")

        logger.info("Task created successfully in scheduler")
    except Exception as e:
        logger.error(f"Error creating task: {e}")
        raise

if __name__ == '__main__':
    cli()