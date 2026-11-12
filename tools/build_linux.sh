#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
python -m PyInstaller --noconfirm --clean --onedir --windowed --name resttimer \
  --add-data "VERSION:." --add-data "CHANGELOG.md:." --add-data "CHANGELOG.fa.md:." --add-data "locales:locales" --add-data "assets/RestTimer.png:assets" --add-data "assets/check-dark.svg:assets" --add-data "assets/check-light.svg:assets" linux/resttimer.py
python tools/package_linux.py
python tools/package_linux.py --rpm
