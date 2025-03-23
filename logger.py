import logging
import os
from logging.handlers import RotatingFileHandler
from pathlib import Path

def setup_logger(log_dir: str = "logs") -> logging.Logger:
    """
    Настройка логгера с ротацией файлов и форматированием
    """
    # Создаем директорию для логов если её нет
    Path(log_dir).mkdir(parents=True, exist_ok=True)
    
    # Создаем логгер
    logger = logging.getLogger("PyDockMonitor")
    logger.setLevel(logging.DEBUG)
    
    # Форматтер для логов
    formatter = logging.Formatter(
        '%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        datefmt='%Y-%m-%d %H:%M:%S'
    )
    
    # Хендлер для файла с ротацией
    file_handler = RotatingFileHandler(
        os.path.join(log_dir, "pydockmonitor.log"),
        maxBytes=5*1024*1024,  # 5MB
        backupCount=5,
        encoding='utf-8'
    )
    file_handler.setLevel(logging.DEBUG)
    file_handler.setFormatter(formatter)
    
    # Хендлер для консоли
    console_handler = logging.StreamHandler()
    console_handler.setLevel(logging.INFO)
    console_handler.setFormatter(formatter)
    
    # Добавляем хендлеры к логгеру
    logger.addHandler(file_handler)
    logger.addHandler(console_handler)
    
    return logger 