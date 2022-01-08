import usb1, time, subprocess, yaml, click, os
import win32com.client as win32

root_path = os.path.join(os.environ['APPDATA'], 'dock_monitor')
os.makedirs(root_path, exist_ok=True)

with open(os.path.join(root_path, 'config.yml')) as cfile:
    config = yaml.safe_load(cfile)


def get_dev_name(device: usb1.USBDevice) -> str:
    return f'{d.getVendorID():X}-{d.getProductID():X}-{d.getbcdDevice():X}-{d.getbcdUSB():X}'


def toggle_desktop():
    # fix for Windows 11 it loses windows position after monitor disconnection
    shell = win32.gencache.EnsureDispatch('Shell.Application')
    shell.ToggleDesktop()
    shell.ToggleDesktop()


def on_docked():
    print("Docked")
    for d in config['restart_devices']:
        subprocess.run(f'pnputil /restart-device "{d}"', shell=True)
    subprocess.run(f'SoundVolumeView.exe  /Enable "{config["dock_comm"]}"')
    subprocess.run(f'SoundVolumeView.exe  /SetDefault "{config["dock_comm"]}" 2')
    subprocess.run(f'SoundVolumeView.exe  /SetDefault "{config["dock_multimedia"]}" 1')
    subprocess.run(f'SoundVolumeView.exe  /SetDefault "{config["dock_multimedia"]}" 0')
    toggle_desktop()


def on_undocked():
    print("UnDocked")
    subprocess.run(f'SoundVolumeView.exe  /Disable "{config["dock_comm"]}"')
    subprocess.run(f'SoundVolumeView.exe  /SetDefault "{config["undock_multimedia"]}" 2')
    subprocess.run(f'SoundVolumeView.exe  /SetDefault "{config["undock_multimedia"]}" 1')
    subprocess.run(f'SoundVolumeView.exe  /SetDefault "{config["undock_multimedia"]}" 0')
    toggle_desktop()


firstrun = True
with usb1.USBContext() as context:
    while True:
        l = []
        if not firstrun:
            l = context.getDeviceList()
        time.sleep(1)
        l2 = context.getDeviceList()
        l3 = list(set(l2) - set(l))
        l4 = list(set(l) - set(l2))
        docked = None
        firstrun = False
        for d in l3:
            print(f'inserted : {get_dev_name(d)}')
            if get_dev_name(d) in config['dock_device']:
                docked = True
                break
        for d in l4:
            print(f'removed : {get_dev_name(d)}')
            if get_dev_name(d) in config['dock_device']:
                docked = False
                break
        if docked is not None:
            if docked:
                on_docked()
            else:
                on_undocked()
