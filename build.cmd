@echo off
echo Сборка PyDockMonitor...

REM Проверяем наличие Python
python --version >nul 2>&1
if errorlevel 1 (
    echo Ошибка: Python не установлен
    exit /b 1
)

REM Проверяем наличие необходимых файлов
if not exist "SoundVolumeView.exe" (
    echo Ошибка: SoundVolumeView.exe не найден
    exit /b 1
)
if not exist "libusb-1.0.dll" (
    echo Ошибка: libusb-1.0.dll не найден
    exit /b 1
)
if not exist "config.yml.example" (
    echo Ошибка: config.yml.example не найден
    exit /b 1
)
if not exist "docked_profile.spr" (
    echo Ошибка: docked_profile.spr не найден
    exit /b 1
)
if not exist "undocked_profile.spr" (
    echo Ошибка: undocked_profile.spr не найден
    exit /b 1
)

REM Создаем виртуальное окружение, если его нет
if not exist ".venv" (
    echo Создание виртуального окружения...
    python -m venv .venv
    if errorlevel 1 (
        echo Ошибка: Не удалось создать виртуальное окружение
        exit /b 1
    )
)

REM Активируем виртуальное окружение
call .venv\Scripts\activate.bat

REM Устанавливаем зависимости
echo Установка зависимостей...
pip install -r requirements.txt
if errorlevel 1 (
    echo Ошибка: Не удалось установить зависимости
    exit /b 1
)

REM Собираем приложение
echo Сборка приложения...
pyinstaller --clean PyDockMonitor.spec
if errorlevel 1 (
    echo Ошибка: Не удалось собрать приложение
    exit /b 1
)

REM Проверяем, что exe создан
if not exist "dist\PyDockMonitor.exe" (
    echo Ошибка: Исполняемый файл не был создан
    exit /b 1
)

echo Сборка завершена успешно!
echo Исполняемый файл находится в папке dist
