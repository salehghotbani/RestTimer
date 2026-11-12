# Third-party components

RestTimer source code is licensed under MIT. The Linux packages also contain
unmodified CPython, PySide6 Essentials / Shiboken6, Qt libraries and the
PyInstaller bootloader. Their licenses remain applicable to those components.

- CPython 3.11.9 — Python Software Foundation license, `licenses/Python.txt`.
- PySide6 / Shiboken6 and Qt 6.12.0 — LGPL-3.0, copyright The Qt Company Ltd. and contributors.
- PyInstaller 6.22.3 — GPL with the bootloader distribution exception, `licenses/PyInstaller.txt`.
- OpenSSL — Apache-2.0, `licenses/OpenSSL.txt`.
- Qt Base bundled components — `licenses/QtBase-6.12.0-NOTICES.txt`.

Full LGPLv3 and GPLv3 texts are included in `licenses/`. The program dynamically
loads Qt from the installed `/opt/resttimer/_internal/` directory. You may replace
compatible libraries there, debug them, or rebuild with modified libraries using
the source and build scripts in this repository. RestTimer imposes no additional
restrictions on those activities.

Corresponding upstream source is available at these locations.

- [Qt Base 6.12.0](https://download.qt.io/archive/qt/6.12/6.12.0/submodules/qtbase-everywhere-src-6.12.0.tar.xz)
- [Qt Wayland 6.12.0](https://download.qt.io/archive/qt/6.12/6.12.0/submodules/qtwayland-everywhere-src-6.12.0.tar.xz)
- [PySide6 / Shiboken6 6.12.0](https://download.qt.io/official_releases/QtForPython/pyside6/PySide6-6.12.0-src/pyside-setup-everywhere-src-6.12.0.tar.xz)
- [CPython 3.11.9](https://www.python.org/ftp/python/3.11.9/Python-3.11.9.tar.xz)
- [PyInstaller 6.22.3](https://github.com/pyinstaller/pyinstaller/tree/v6.22.3)
- [OpenSSL source and licenses](https://github.com/openssl/openssl)

The Windows installer uses the system .NET Framework and is generated with
[Inno Setup](https://jrsoftware.org/isinfo.php).

The installer includes the community Persian translation by Peyman Mohammadi,
listed on the Inno Setup translations page. Its original author header is
preserved in `packaging/languages/Farsi.isl`.
[Translation source](https://github.com/jrsoftware/issrc/blob/2e0e2d7abff09c81de67daf4c2dcba10c5ff32ab/Files/Languages/Unofficial/Farsi.isl).
