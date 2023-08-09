import shutil

import usb1, time, subprocess, yaml, click, os
import win32com.client as win32
import pywintypes
import win32api
import win32con
from screeninfo import get_monitors
import time, retry
import logging

import time
from logging.handlers import RotatingFileHandler

logging.basicConfig(level=logging.DEBUG, format='%(asctime)s %(levelname)s %(message)s')

logger = logging.getLogger("root")


def ResetResolution():
    m = [x for x in get_monitors() if x.is_primary == True]
    (x, y) = (m[0].width, m[0].height)
    # fix for Windows 11 it loses windows position after monitor disconnection
    shell = win32.gencache.EnsureDispatch('Shell.Application')
    shell.ToggleDesktop()
    devmode = pywintypes.DEVMODEType()
    devmode.PelsWidth = 1280
    devmode.PelsHeight = 1024

    devmode.Fields = win32con.DM_PELSWIDTH | win32con.DM_PELSHEIGHT
    win32api.ChangeDisplaySettings(devmode, 0)
    time.sleep(2)
    devmode.PelsWidth = x
    devmode.PelsHeight = y
    win32api.ChangeDisplaySettings(devmode, 0)
    shell.ToggleDesktop()


@retry.retry(tries=2)
def load_config(p):
    os.makedirs(root_path, exist_ok=True)
    try:
        with open(os.path.join(root_path, 'config.yml')) as cfile:
            cfg = yaml.safe_load(cfile)
    except FileNotFoundError as e:
        shutil.copy('config.yml.example', os.path.join(root_path, 'config.yml'))
        raise e
    return cfg


def get_dev_name(d: usb1.USBDevice) -> str:
    return f'{d.getVendorID():X}-{d.getProductID():X}-{d.getbcdDevice():X}-{d.getbcdUSB():X}'


def on_docked():
    logger.debug("Docked")
    for d in config.get('restart_devices', []):
        subprocess.run(f'pnputil /restart-device "{d}"', shell=True)

    subprocess.run(f'SoundVolumeView.exe  /LoadProfile docked_profile')


def on_undocked():
    logger.debug("UnDocked")
    subprocess.run(f'SoundVolumeView.exe  /LoadProfile undocked_profile')
    ResetResolution()


def main_loop():
    first_run = True
    logger.debug("starting loop")
    with usb1.USBContext() as context:
        while True:
            l = []
            if not first_run:
                l = context.getDeviceList()
            time.sleep(2)
            l2 = context.getDeviceList()
            l3 = list(set(l2) - set(l))
            l4 = list(set(l) - set(l2))
            # logging.debug(f'{l2} \n {l3} \n {l4}')
            docked = None
            first_run = False
            for d in l3:
                logger.debug(f'inserted : {get_dev_name(d)}')
                if get_dev_name(d) in config['dock_device']:
                    docked = True
                    break
            for d in l4:
                logger.debug(f'removed : {get_dev_name(d)}')
                if get_dev_name(d) in config['dock_device']:
                    docked = False
                    break
            if docked is not None:
                if docked:
                    on_docked()
                else:
                    on_undocked()


# @click.group()
@click.command()
def detect():
    with usb1.USBContext() as context:
        click.echo("Dock device and press enter")
        click.getchar()
        docked = context.getDeviceList()
        click.echo("Undock device and press enter")
        click.getchar()
        undocked = context.getDeviceList()
        difference = list(set(docked) - set(undocked))
        logger.debug([get_dev_name(x) for x in difference])
        click.echo("Configure sound settings for docked and press enter")
        input()
        subprocess.run(f'SoundVolumeView.exe  /SaveProfile "docked_profile"')
        click.echo("Configure sound settings for undocked and press enter")
        input()
        subprocess.run(f'SoundVolumeView.exe  /SaveProfile "undocked_profile"')


@click.group(invoke_without_command=True)
@click.pass_context
def cli(ctx):
    if ctx.invoked_subcommand is None:
        main_loop()


# ----------------------------------------------------------------------
def create_rotating_log(path):
    """
    Creates a rotating log
    """

    logger.setLevel(logging.DEBUG)

    # add a rotating handler
    handler = RotatingFileHandler(path, maxBytes=1000000,
                                  backupCount=5)
    handler.setFormatter(logging.Formatter('%(asctime)s %(levelname)s %(message)s'))
    logger.addHandler(handler)


# ----------------------------------------------------------------------
if __name__ == '__main__':
    import sys

    # ResetResolution()

    root_path = os.path.join(os.environ['APPDATA'], 'dock_monitor')
    config = load_config(root_path)

    create_rotating_log(os.path.join(root_path, "dock_monitor.log"))
    if getattr(sys, 'frozen', False):
        logger.debug("Running in a PyInstaller bundle")
        application_path = sys._MEIPASS
    elif __file__:
        application_path = os.path.dirname(__file__)

    logging.debug(application_path)
    logging.debug(config)
    shutil.copy(os.path.join(application_path, "SoundVolumeView.exe"), root_path)
    os.chdir(root_path)
    cli.add_command(detect)
    cli()
