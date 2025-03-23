@echo off
setlocal enabledelayedexpansion

echo Установка PyDockMonitor...

REM Проверяем права администратора
net session >nul 2>&1
if %errorLevel% == 0 (
    echo Запущено с правами администратора
) else (
    echo Требуются права администратора
    echo Запуск с повышенными правами...
    powershell -Command "Start-Process '%~dpnx0' -Verb RunAs"
    exit /b
)

REM Создаем директорию для приложения
set "INSTALL_DIR=%ProgramFiles%\PyDockMonitor"
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

REM Копируем файлы
echo Копирование файлов...
copy /Y "dist\PyDockMonitor.exe" "%INSTALL_DIR%"
copy /Y "SoundVolumeView.exe" "%INSTALL_DIR%"
copy /Y "libusb-1.0.dll" "%INSTALL_DIR%"
copy /Y "config.yml.example" "%INSTALL_DIR%"


REM Создаем конфиг, если его нет
if not exist "%INSTALL_DIR%\config.yml" (
    copy /Y "%INSTALL_DIR%\config.yml.example" "%INSTALL_DIR%\config.yml"
)

REM Создаем запланированную задачу
echo Создание запланированной задачи...
schtasks /create /tn "PyDockMonitor" /tr "\"%INSTALL_DIR%\PyDockMonitor.exe\"" /sc onlogon /ru "%USERNAME%" /f >nul 2>&1

echo Установка завершена успешно!
echo Приложение будет запускаться автоматически при входе в систему
echo Файлы установлены в: %INSTALL_DIR%
pause 