# -*- mode: python ; coding: utf-8 -*-

block_cipher = None

# Общие настройки для обеих версий
common_datas = [
    ('SoundVolumeView.exe', '.'),
    ('libusb-1.0.dll', '.'),
    ('config.yml.example', '.')
]

common_options = {
    'debug': False,
    'bootloader_ignore_signals': False,
    'strip': False,
    'upx': True,
    'upx_exclude': [],
    'runtime_tmpdir': None,
    'disable_windowed_traceback': False,
    'argv_emulation': False,
    'target_arch': None,
    'codesign_identity': None,
    'entitlements_file': None,
}

# Анализ для неконсольной версии
a_noconsole = Analysis(
    ['main.py'],
    pathex=[],
    binaries=[],
    datas=common_datas,
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)
# Создаем PYZ для обеих версий
pyz_noconsole = PYZ(a_noconsole.pure, a_noconsole.zipped_data, cipher=block_cipher)

# Создаем EXE для неконсольной версии
exe_noconsole = EXE(
    pyz_noconsole,
    a_noconsole.scripts,
    a_noconsole.binaries,
    a_noconsole.zipfiles,
    a_noconsole.datas,
    [],
    name='pydockmonitor',
    console=False,
    **common_options
)

common_datas = [
    ('SoundVolumeView.exe', '.'),
    ('libusb-1.0.dll', '.'),
    ('config.yml.example', '.'),
    ('dist/pydockmonitor.exe','.')
]
# Анализ для консольной версии
a_console = Analysis(
    ['main.py'],
    pathex=[],
    binaries=[],
    datas=common_datas,
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=['cleanup_hook.py'],
    excludes=[],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)


pyz_console = PYZ(a_console.pure, a_console.zipped_data, cipher=block_cipher)


# Создаем EXE для консольной версии
exe_console = EXE(
    pyz_console,
    a_console.scripts,
    a_console.binaries,
    a_console.zipfiles,
    a_console.datas,
    [],
    name='pydockmonitor_console',
    console=True,
    **common_options
)
