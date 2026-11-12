#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
python -m PyInstaller --noconfirm --clean --onedir --windowed --name resttimer \
  --add-data "VERSION:." --add-data "assets/RestTimer.png:assets" linux/resttimer.py
python tools/package_linux.py
python tools/package_linux.py --rpm
