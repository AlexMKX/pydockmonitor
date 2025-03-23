import os
import shutil
import subprocess
import time
import sys
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

from logger import setup_logger
from usb_manager import USBManager
from audio_manager import AudioManager

# Настройка логгера
logger = setup_logger()

# Определяем путь установки и меняем рабочую директорию
app_dir = os.path.join(os.getenv('APPDATA'), 'dock_monitor')
os.makedirs(app_dir, exist_ok=True)
os.chdir(app_dir)

class Config(BaseSettings):
    dock_device: list[str]
    restart_devices: list[str]
    dock_comm: str
    undock_comm: str
    dock_multimedia: str
    undock_multimedia: str
    reset_resolution: bool = False

def reset_resolution():
    """Сброс разрешения экрана"""
    try:
        primary_monitor = next(x for x in get_monitors() if x.is_primary)
        original_width, original_height = primary_monitor.width, primary_monitor.height

        # Исправление для Windows 11: потеря позиций окон после отключения монитора
        shell = win32.gencache.EnsureDispatch('Shell.Application')
        shell.ToggleDesktop()

        devmode = pywintypes.DEVMODEType()
        devmode.Fields = win32con.DM_PELSWIDTH | win32con.DM_PELSHEIGHT

        # Сброс на временное разрешение
        temp_width, temp_height = 1280, 1024
        devmode.PelsWidth, devmode.PelsHeight = temp_width, temp_height
        win32api.ChangeDisplaySettings(devmode, 0)
        logger.debug(f"Установлено временное разрешение {temp_width}x{temp_height}")
        time.sleep(2)

        # Возврат к исходному разрешению
        devmode.PelsWidth, devmode.PelsHeight = original_width, original_height
        win32api.ChangeDisplaySettings(devmode, 0)
        logger.debug(f"Разрешение восстановлено до {original_width}x{original_height}")

        shell.ToggleDesktop()
        logger.debug("Сброс разрешения успешно завершен")
    except Exception as e:
        logger.error(f"Ошибка при сбросе разрешения: {str(e)}")

def load_config(config_path: str) -> Config:
    """Загрузка конфигурации"""
    try:
        if not os.path.exists(config_path):
            shutil.copy('config.yml.example', config_path)
            raise FileNotFoundError(f"Создан новый конфигурационный файл: {config_path}")
            
        with open(config_path) as cfile:
            cfg = yaml.safe_load(cfile)
        return Config.model_validate(cfg)
    except Exception as e:
        logger.error(f"Ошибка при загрузке конфигурации: {e}")
        raise

def on_docked(config: Config, audio_manager: AudioManager):
    """Обработка подключения док-станции"""
    logger.info("Обнаружено подключение док-станции")
    
    # Перезапуск устройств
    for device in config.restart_devices:
        logger.info(f'Перезапуск устройства {device}')
        try:
            subprocess.run(f'pnputil /restart-device "{device}"', shell=True, check=True)
        except subprocess.CalledProcessError as e:
            logger.error(f"Ошибка при перезапуске устройства {device}: {e}")

    # Загрузка профиля звука
    if not audio_manager.load_profile('docked_profile.spr'):
        logger.error("Не удалось загрузить профиль для док-станции")

def on_undocked(config: Config, audio_manager: AudioManager):
    """Обработка отключения док-станции"""
    logger.info("Обнаружено отключение док-станции")
    
    # Загрузка профиля звука
    if not audio_manager.load_profile('undocked_profile.spr'):
        logger.error("Не удалось загрузить профиль для автономного режима")
    
    # Сброс разрешения если требуется
    if config.reset_resolution:
        reset_resolution()

def main_loop(config: Config):
    """Основной цикл программы"""
    logger.info("Запуск мониторинга USB-устройств")
    
    usb_manager = USBManager()
    audio_manager = AudioManager()
    
    dock_devices = set(config.dock_device)
    dev_list_before = usb_manager.get_current_devices()
    docked_before = False
    first_run = True

    while True:
        try:
            dev_list_current = usb_manager.get_current_devices()
            
            if not first_run and dev_list_current == dev_list_before:
                time.sleep(2)
                continue

            if not first_run:
                logger.debug("Изменен список устройств")
                removed = dev_list_before - dev_list_current
                added = dev_list_current - dev_list_before
                logger.debug(f"Удалены: {removed}")
                logger.debug(f"Добавлены: {added}")

            docked_dev_status = dock_devices.intersection(dev_list_current)
            logger.debug(f"Статус док-устройств: {docked_dev_status}")
            docked = bool(docked_dev_status)

            if docked_before != docked or first_run:
                logger.info("Изменено состояние док-станции")
                if docked:
                    time.sleep(10)  # Даем время на инициализацию устройств
                    on_docked(config, audio_manager)
                else:
                    on_undocked(config, audio_manager)

            docked_before = docked
            dev_list_before = dev_list_current
            first_run = False
            
        except Exception as e:
            logger.error(f"Ошибка в основном цикле: {e}")
            time.sleep(5)  # Пауза перед повторной попыткой

@click.group(invoke_without_command=True)
@click.pass_context
def cli(ctx):
    """Главная группа команд"""
    if ctx.invoked_subcommand is None:
        try:
            config = load_config('config.yml')
            main_loop(config)
        except Exception as e:
            logger.error(f"Критическая ошибка: {e}")
            sys.exit(1)

@cli.command()
def detect():
    """Определение и настройка устройств док-станции"""
    usb_manager = USBManager()
    audio_manager = AudioManager()
    
    click.echo("Подключите док-станцию и нажмите Enter")
    click.getchar()
    docked = usb_manager.get_current_devices()
    
    click.echo("Отключите док-станцию и нажмите Enter")
    click.getchar()
    undocked = usb_manager.get_current_devices()
    
    difference = list(docked - undocked)
    logger.info(f"Обнаружены устройства док-станции: {difference}")
    
    click.echo("Настройте звук для режима док-станции и нажмите Enter")
    input()
    if not audio_manager.save_profile("docked_profile.spr"):
        logger.error("Не удалось сохранить профиль для док-станции")
        return
    
    click.echo("Настройте звук для автономного режима и нажмите Enter")
    input()
    if not audio_manager.save_profile("undocked_profile.spr"):
        logger.error("Не удалось сохранить профиль для автономного режима")
        return

def is_task_running(task_name):
    """Проверяет, запущена ли задача"""
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
    """Ждет остановки задачи"""
    if is_task_running("PyDockMonitor"):
        raise Exception("Task is still running")
    return True

def get_other_pydockmonitor_processes():
    """Получает список процессов pydockmonitor.exe, кроме текущего"""
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
    """Установка файлов программы"""
    try:
        # Определяем пути установки
        app_dir = os.path.join(os.getenv('APPDATA'), 'dock_monitor')
        os.makedirs(app_dir, exist_ok=True)

        # Проверяем, запущена ли задача
        if is_task_running("PyDockMonitor"):
            try:
                subprocess.run('schtasks /End /TN PyDockMonitor', shell=True, check=True)
                logger.info("Запрошена остановка существующей задачи")
                wait_for_task_stop()
                logger.info("Задача успешно остановлена")
            except subprocess.CalledProcessError as e:
                logger.error(f"Ошибка при остановке задачи: {e}")
            except Exception as e:
                logger.error(f"Ошибка при ожидании остановки задачи: {e}")

        # Проверяем другие запущенные процессы
        other_processes = get_other_pydockmonitor_processes()
        for proc in other_processes:
            try:
                proc.terminate()
                try:
                    proc.wait(timeout=3)  # Ждем завершения процесса
                except psutil.TimeoutExpired:
                    proc.kill()  # Если процесс не завершился, убиваем его
                logger.info(f"Завершен процесс pydockmonitor.exe (PID: {proc.pid})")
            except psutil.NoSuchProcess:
                pass
            except Exception as e:
                logger.error(f"Ошибка при завершении процесса {proc.pid}: {e}")

        # Проверяем существующий config.yml
        existing_config = None
        if os.path.exists('config.yml'):
            try:
                with open('config.yml') as cfile:
                    existing_config = yaml.safe_load(cfile)
                Config.model_validate(existing_config)
                logger.info("Существующий config.yml успешно загружен")
            except Exception as e:
                logger.error(f"Ошибка при загрузке существующего config.yml: {e}")
                existing_config = None

        # Список файлов для копирования
        files_to_copy = [
            'SoundVolumeView.exe',
            'libusb-1.0.dll',
            'config.yml.example',
            'docked_profile.spr',
            'undocked_profile.spr'
        ]

        # Копируем файлы
        for file in files_to_copy:
            src = os.path.join(os.path.dirname(os.path.abspath(__file__)), file)
            if os.path.exists(src):
                try:
                    shutil.copy2(src, file)
                    logger.info(f"Скопирован файл: {file}")
                except PermissionError:
                    logger.error(f"Нет доступа к файлу: {file}")
                    raise
            else:
                logger.error(f"Файл не найден: {file}")
                raise FileNotFoundError(f"Файл не найден: {file}")

        # Копируем исполняемый файл
        if getattr(sys, 'frozen', False):
            exe_src = sys.executable
            try:
                shutil.copy2(exe_src, 'pydockmonitor.exe')
                logger.info("Скопирован исполняемый файл")
            except PermissionError:
                logger.error("Нет доступа к исполняемому файлу")
                raise

        # Создаем конфигурационный файл если его нет или если существующий невалиден
        if not existing_config and not os.path.exists('config.yml'):
            shutil.copy2('config.yml.example', 'config.yml')
            logger.info("Создан конфигурационный файл")

        return app_dir
    except Exception as e:
        logger.error(f"Ошибка при установке файлов: {e}")
        raise

@cli.command()
def install():
    """Установка программы"""
    try:
        # Устанавливаем файлы
        app_dir = install_files()
        logger.info("Файлы успешно установлены")

        # Устанавливаем задачу в планировщик
        install_scheduled_task()
        logger.info("Задача успешно добавлена в планировщик")

        click.echo("Установка успешно завершена")
    except Exception as e:
        logger.error(f"Ошибка при установке: {e}")
        click.echo(f"Ошибка при установке: {e}", err=True)
        sys.exit(1)

def install_scheduled_task():
    """Установка задачи в планировщике Windows"""
    try:
        # Определяем пути
        app_dir = os.path.join(os.getenv('APPDATA'), 'dock_monitor')
        exe_path = os.path.join(app_dir, 'pydockmonitor.exe')
        
        # Создаем XML для задачи
        task_xml = f"""<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>{os.getenv('USERNAME')}</UserId>
    </LogonTrigger>
  </Triggers>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>false</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>true</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
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

        # Сохраняем XML во временный файл
        task_xml_path = os.path.join(app_dir, 'task.xml')
        with open(task_xml_path, 'w', encoding='utf-16') as f:
            f.write(task_xml)
        
        # Создаем задачу через XML
        cmd = f'schtasks /Create /TN PyDockMonitor /XML "{task_xml_path}" /F'
        result = subprocess.run(cmd, shell=True, capture_output=True, text=True)
        
        # Удаляем временный XML файл
        os.remove(task_xml_path)
        
        if result.returncode != 0:
            logger.error(f"Ошибка при создании задачи: {result.stderr}")
            raise Exception(f"Ошибка при создании задачи: {result.stderr}")

        logger.info("Задача успешно создана в планировщике")
    except Exception as e:
        logger.error(f"Ошибка при создании задачи: {e}")
        raise

if __name__ == '__main__':
    cli()