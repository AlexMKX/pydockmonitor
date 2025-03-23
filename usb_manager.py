import logging
from typing import Set, Optional
import usb1
from retry import retry

logger = logging.getLogger("PyDockMonitor")

class USBManager:
    def __init__(self):
        self.context = usb1.USBContext()
    
    def get_device_name(self, device: usb1.USBDevice) -> str:
        """Получение уникального идентификатора устройства"""
        try:
            return f'{device.getVendorID():X}-{device.getProductID():X}-{device.getbcdDevice():X}-{device.getbcdUSB():X}'
        except Exception as e:
            logger.error(f"Ошибка при получении имени устройства: {e}")
            return ""
    
    @retry(tries=3, delay=1)
    def get_current_devices(self) -> Set[str]:
        """Получение списка текущих USB-устройств с повторными попытками"""
        try:
            return set(self.get_device_name(d) for d in self.context.getDeviceList())
        except Exception as e:
            logger.error(f"Ошибка при получении списка устройств: {e}")
            return set()
    
    def is_device_present(self, device_id: str) -> bool:
        """Проверка наличия конкретного устройства"""
        return device_id in self.get_current_devices()
    
    def get_device_info(self, device_id: str) -> Optional[dict]:
        """Получение подробной информации об устройстве"""
        try:
            for device in self.context.getDeviceList():
                if self.get_device_name(device) == device_id:
                    return {
                        'vendor_id': device.getVendorID(),
                        'product_id': device.getProductID(),
                        'device_version': device.getbcdDevice(),
                        'usb_version': device.getbcdUSB(),
                        'manufacturer': device.getManufacturer(),
                        'product': device.getProduct()
                    }
        except Exception as e:
            logger.error(f"Ошибка при получении информации об устройстве {device_id}: {e}")
        return None 