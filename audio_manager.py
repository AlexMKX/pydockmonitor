import logging
import subprocess
from pathlib import Path
from typing import Optional

logger = logging.getLogger("PyDockMonitor")

class AudioManager:
    def __init__(self, sound_volume_view_path: str = "SoundVolumeView.exe"):
        self.sound_volume_view = sound_volume_view_path
        self._check_sound_volume_view()
    
    def _check_sound_volume_view(self) -> bool:
        """Проверка наличия SoundVolumeView.exe"""
        if not Path(self.sound_volume_view).exists():
            logger.error(f"SoundVolumeView.exe не найден по пути: {self.sound_volume_view}")
            return False
        return True
    
    def load_profile(self, profile_path: str) -> bool:
        """Загрузка профиля звука"""
        try:
            if not Path(profile_path).exists():
                logger.error(f"Профиль не найден: {profile_path}")
                return False
                
            result = subprocess.run(
                [self.sound_volume_view, "/LoadProfile", profile_path],
                capture_output=True,
                text=True
            )
            
            if result.returncode != 0:
                logger.error(f"Ошибка загрузки профиля: {result.stderr}")
                return False
                
            logger.info(f"Профиль успешно загружен: {profile_path}")
            return True
            
        except Exception as e:
            logger.error(f"Ошибка при загрузке профиля {profile_path}: {e}")
            return False
    
    def save_profile(self, profile_path: str) -> bool:
        """Сохранение профиля звука"""
        try:
            result = subprocess.run(
                [self.sound_volume_view, "/SaveProfile", profile_path],
                capture_output=True,
                text=True
            )
            
            if result.returncode != 0:
                logger.error(f"Ошибка сохранения профиля: {result.stderr}")
                return False
                
            logger.info(f"Профиль успешно сохранен: {profile_path}")
            return True
            
        except Exception as e:
            logger.error(f"Ошибка при сохранении профиля {profile_path}: {e}")
            return False
    
    def list_audio_devices(self) -> Optional[list]:
        """Получение списка аудио-устройств"""
        try:
            result = subprocess.run(
                [self.sound_volume_view, "/scomma", "devices.txt"],
                capture_output=True,
                text=True
            )
            
            if result.returncode != 0:
                logger.error(f"Ошибка получения списка устройств: {result.stderr}")
                return None
                
            with open("devices.txt", "r", encoding="utf-8") as f:
                devices = [line.strip() for line in f if line.strip()]
            
            Path("devices.txt").unlink()
            return devices
            
        except Exception as e:
            logger.error(f"Ошибка при получении списка аудио-устройств: {e}")
            return None 