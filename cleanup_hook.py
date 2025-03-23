import os
import sys

def _():
    # Удаляем неконсольную версию после сборки
    if os.path.basename(sys.executable).lower() == 'pydockmonitor_console.exe':
        noconsole_exe = os.path.join(os.path.dirname(sys.executable), 'pydockmonitor.exe')
        if os.path.exists(noconsole_exe):
            try:
                os.remove(noconsole_exe)
                print("Removed non-console version after build")
            except Exception as e:
                print(f"Error removing non-console version: {e}") 