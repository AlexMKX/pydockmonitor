#!/bin/bash

echo "Сборка PyDockMonitor..."

# Проверяем наличие Python
if ! command -v python3 &> /dev/null; then
    echo "Ошибка: Python не установлен"
    exit 1
fi

# Проверяем наличие необходимых файлов
required_files=(
    "SoundVolumeView.exe"
    "libusb-1.0.dll"
    "config.yml.example"
    "docked_profile.spr"
    "undocked_profile.spr"
)

for file in "${required_files[@]}"; do
    if [ ! -f "$file" ]; then
        echo "Ошибка: $file не найден"
        exit 1
    fi
done

# Создаем виртуальное окружение, если его нет
if [ ! -d ".venv" ]; then
    echo "Создание виртуального окружения..."
    python3 -m venv .venv
    if [ $? -ne 0 ]; then
        echo "Ошибка: Не удалось создать виртуальное окружение"
        exit 1
    fi
fi

# Активируем виртуальное окружение
source .venv/bin/activate

# Устанавливаем зависимости
echo "Установка зависимостей..."
pip install -r requirements.txt
if [ $? -ne 0 ]; then
    echo "Ошибка: Не удалось установить зависимости"
    exit 1
fi

# Собираем приложение
echo "Сборка приложения..."
pyinstaller --clean PyDockMonitor.spec
if [ $? -ne 0 ]; then
    echo "Ошибка: Не удалось собрать приложение"
    exit 1
fi

# Проверяем, что exe создан
if [ ! -f "dist/PyDockMonitor" ]; then
    echo "Ошибка: Исполняемый файл не был создан"
    exit 1
fi

echo "Сборка завершена успешно!"
echo "Исполняемый файл находится в папке dist" 