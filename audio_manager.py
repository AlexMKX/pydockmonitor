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
        """Check if SoundVolumeView.exe exists"""
        if not Path(self.sound_volume_view).exists():
            logger.error(f"SoundVolumeView.exe not found at: {self.sound_volume_view}")
            return False
        return True
    
    def load_profile(self, profile_path: str) -> bool:
        """Load audio profile"""
        try:
            if not Path(profile_path).exists():
                logger.error(f"Profile not found: {profile_path}")
                return False
                
            result = subprocess.run(
                [self.sound_volume_view, "/LoadProfile", profile_path],
                capture_output=True,
                text=True
            )
            
            if result.returncode != 0:
                logger.error(f"Error loading profile: {result.stderr}")
                return False
                
            logger.info(f"Profile successfully loaded: {profile_path}")
            return True
            
        except Exception as e:
            logger.error(f"Error loading profile {profile_path}: {e}")
            return False
    
    def save_profile(self, profile_path: str) -> bool:
        """Save audio profile"""
        try:
            result = subprocess.run(
                [self.sound_volume_view, "/SaveProfile", profile_path],
                capture_output=True,
                text=True
            )
            
            if result.returncode != 0:
                logger.error(f"Error saving profile: {result.stderr}")
                return False
                
            logger.info(f"Profile successfully saved: {profile_path}")
            return True
            
        except Exception as e:
            logger.error(f"Error saving profile {profile_path}: {e}")
            return False
    
    def list_audio_devices(self) -> Optional[list]:
        """Get list of audio devices"""
        try:
            result = subprocess.run(
                [self.sound_volume_view, "/scomma", "devices.txt"],
                capture_output=True,
                text=True
            )
            
            if result.returncode != 0:
                logger.error(f"Error getting device list: {result.stderr}")
                return None
                
            with open("devices.txt", "r", encoding="utf-8") as f:
                devices = [line.strip() for line in f if line.strip()]
            
            Path("devices.txt").unlink()
            return devices
            
        except Exception as e:
            logger.error(f"Error getting audio device list: {e}")
            return None 