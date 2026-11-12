**English** | [فارسی](README.fa.md)

<div align="center">
  <img src="assets/RestTimer.png" width="88" height="88" alt="RestTimer icon">
  <h1>RestTimer</h1>
  <p><strong>Work and break reminders across all your monitors, on Windows and Linux.</strong></p>
  <p>Work for 60 minutes, then take a 15-minute break. Customize both periods in hours, minutes and seconds.</p>
</div>

<div align="center">

[![Latest release][release-badge]][releases]
[![Windows installer][windows-badge]][windows]
[![Ubuntu package][ubuntu-badge]][ubuntu]
[![Fedora package][fedora-badge]][fedora]
[![MIT license][license-badge]](LICENSE)

[![Build and installer checks][build-badge]][workflow]
[![x86-64 architecture][arch-badge]][releases]

**[Get RestTimer][releases] · [Report an issue][issues] · [Contribute](CONTRIBUTING.md)**

</div>

During a break, a horizontal reminder appears at the top center of every monitor, with a shared countdown. A small skip button sits on the left, while the rest of the screen is blurred and dimmed. When the timer ends, or you skip the break or press Esc, the next work period begins.

The application controls currently use Persian. The Windows installer supports both **English and Persian**. This documentation is available in [English](README.md) and [Persian](README.fa.md).

## Download and install

Choose the installer for your system from the [latest release][releases]. Packages target **x86-64**.

| System | Package | Download |
| --- | --- | --- |
| **Windows 10 / 11** | EXE installer | [Windows installer][windows] |
| **Ubuntu 22.04 or later** | DEB package | [Ubuntu package][ubuntu] |
| **Fedora 43 or later** | RPM package | [Fedora package][fedora] |

### Windows

Run **setup.exe** and select **English** or **فارسی** before installation begins. The installation and uninstallation steps follow your selected language. Setup installs RestTimer for the current account, creates a Start menu shortcut, and supports removal through Windows Settings. Launch at sign-in and a desktop shortcut are optional. Setup checks for the required .NET Framework 4.8.

### Ubuntu

Open the DEB in your software manager, or run this command from the download directory:

```bash
sudo apt install ./RestTimer-v1.6.0-linux-amd64.deb
```

### Fedora

Open the RPM in your software manager, or run:

```bash
sudo dnf install ./RestTimer-v1.6.0-linux-x86_64.rpm
```

Launch RestTimer from the application menu or with `resttimer`. The Linux packages bundle Python and Qt. Uninstall with `sudo apt remove resttimer` or `sudo dnf remove resttimer`.

## Using RestTimer

- Left-click or right-click the tray icon to open the menu. **Start** («شروع»), **Stop** («توقف»), and **Start a break now** («شروع استراحت همین حالا») are the first entries. You can also change durations, toggle launch at sign-in, check for updates, and quit.
- The countdown starts when the application launches. Skipping a break resets the next work period.
- Start and Stop are available in the tray menu, settings, and break reminder. Stop pauses the current period; Start resumes from its remaining time. During a paused break, reminders stay open and can be resumed from any monitor. Skipping a break or changing durations preserves the paused state.
- On Windows, the update window and menu follow the Windows **application** color mode and support high contrast. Theme changes apply while the window is open. Linux follows the desktop color scheme reported by Qt.
- Release notes have headings, spacing, and paragraph direction appropriate for Persian and English. The Windows update window is resizable and has a separate version list for browsing release history.

### Windows taskbar countdown

A fixed, single-line countdown appears just left of the notification area, beside the chevron. It shows the work or break period and paused state, with the label on the right and time on the left. It has no background or border; its text follows the Windows **system** color mode and high-contrast settings.

In settings, toggle **«نمایش زمان کنار فلش آیکن‌های تسک‌بار»** (show the countdown beside the taskbar icons), then select **«ذخیرهٔ تنظیمات»** (save settings). Your choice persists across restarts and does not reset the timer.

The countdown cannot be dragged and lets mouse clicks pass through. It hides with an auto-hidden taskbar and when another application runs fullscreen. It uses the primary horizontal taskbar and does not reserve space in the Windows taskbar layout.

### Settings and startup

Windows settings are stored in the current user's account; older minute-based settings are still read. Linux stores settings in `~/.config/resttimer/settings.json` and its launch-at-sign-in entry in `~/.config/autostart/resttimer.desktop`.

### Linux desktops

The Linux frontend runs on X11 and Wayland. When desktop capture is unavailable on Wayland, a translucent overlay still dims the screen. Window placement and stacking are controlled by the desktop window manager.

On GNOME, tray visibility and click behavior depend on desktop support or an AppIndicator extension. If no system tray is available, RestTimer shows its control window; closing that window quits the application. Launching RestTimer again from the application menu opens the existing instance's settings window.

## Updates

While running, RestTimer checks the latest stable release at startup when due, then every 24 hours. A failed check is retried six hours later. The schedule persists across restarts, and each new version generates one notification. You can disable daily checks in the update window. System notifications must be allowed for RestTimer to show alerts.

Click the notification or open **«به‌روزرسانی و تغییرات»** (updates and changes), read the release notes, and approve **«دانلود و نصب»** (download and install). **RestTimer downloads and installs the update itself.** Downloads show progress and can be cancelled. File size and SHA-256 are checked before installation.

Windows updates use the current installation path and installer language; Setup closes the running application and reopens it after installation. Ubuntu and Fedora updates use apt or dnf and request administrator authorization. Download failures or declined authorization leave the current application running.

The **«تاریخچهٔ نسخه‌ها»** (version history) page retrieves release notes from GitHub. A bundled [changelog](CHANGELOG.md), currently in Persian, is available offline.

For a manual upgrade on Windows, run the new **EXE installer**. If RestTimer is running, Setup explains that it will close and reopen the application, then does so automatically. On Ubuntu and Fedora, install the new package using the same command as above. Your duration settings are preserved during upgrades.

## Built with

[![C# and WinForms][csharp-badge]][winforms]
[![.NET Framework][dotnet-badge]][dotnet]
[![Python][python-badge]][python]
[![PySide6 Essentials][pyside-badge]][pyside]
[![PyInstaller][pyinstaller-badge]][pyinstaller]
[![Inno Setup][inno-badge]][inno]

| Technology | Purpose |
| --- | --- |
| **C#, WinForms and .NET Framework 4.8** | Windows interface, tray menu, and synchronized monitor overlays |
| **Python 3.11 and PySide6 Essentials / Qt 6.12** | Linux interface, duration controls, tray icon, and break windows |
| **Inno Setup 6** | Windows EXE installer, shortcuts, and uninstallation |
| **PyInstaller, dpkg-deb and rpmbuild** | Bundling Linux dependencies and building DEB and RPM packages |
| **GitHub Actions, Xvfb, Weston and Docker** | Building and checking installation, removal, and desktop execution on Windows, Ubuntu, Fedora, X11, and Wayland |

## Building and contributing

See the [contribution guide](CONTRIBUTING.md) for build commands, packaging, and checks. The repository workflow verifies Windows installation and removal, Ubuntu and Fedora package installation and removal, and Linux execution on X11 and Wayland.

English is the default documentation language. Keep the [Persian README](README.fa.md) and [Persian contribution guide](CONTRIBUTING.fa.md) in sync when updating the English guides.

RestTimer is released under the [MIT license](LICENSE). Bundled dependency licenses and source information are listed in [third-party notices](THIRD_PARTY_NOTICES.md).

[releases]: https://github.com/salehghotbani/RestTimer/releases/latest
[windows]: https://github.com/salehghotbani/RestTimer/releases/download/v1.6.0/RestTimer-v1.6.0-windows-x64-setup.exe
[ubuntu]: https://github.com/salehghotbani/RestTimer/releases/download/v1.6.0/RestTimer-v1.6.0-linux-amd64.deb
[fedora]: https://github.com/salehghotbani/RestTimer/releases/download/v1.6.0/RestTimer-v1.6.0-linux-x86_64.rpm
[issues]: https://github.com/salehghotbani/RestTimer/issues
[workflow]: https://github.com/salehghotbani/RestTimer/actions/workflows/packages.yml
[release-badge]: https://img.shields.io/github/v/release/salehghotbani/RestTimer?label=Release&color=16836b
[windows-badge]: https://img.shields.io/badge/Windows-EXE-2563eb
[ubuntu-badge]: https://img.shields.io/badge/Ubuntu-DEB-E95420?logo=ubuntu&logoColor=white
[fedora-badge]: https://img.shields.io/badge/Fedora-RPM-294172?logo=fedora&logoColor=white
[license-badge]: https://img.shields.io/badge/License-MIT-475569
[build-badge]: https://github.com/salehghotbani/RestTimer/actions/workflows/packages.yml/badge.svg?branch=main&event=push
[arch-badge]: https://img.shields.io/badge/Architecture-x86--64-475569
[csharp-badge]: https://img.shields.io/badge/C%23-WinForms-512BD4
[dotnet-badge]: https://img.shields.io/badge/.NET_Framework-4.8-512BD4?logo=dotnet&logoColor=white
[python-badge]: https://img.shields.io/badge/Python-3.11-3776AB?logo=python&logoColor=white
[pyside-badge]: https://img.shields.io/badge/PySide6_Essentials-6.12.0-167a4b?logo=qt&logoColor=white
[pyinstaller-badge]: https://img.shields.io/badge/PyInstaller-6.22.3-345d9d
[inno-badge]: https://img.shields.io/badge/Inno_Setup-6-475569
[winforms]: https://learn.microsoft.com/dotnet/desktop/winforms/
[dotnet]: https://dotnet.microsoft.com/download/dotnet-framework/net48
[python]: https://www.python.org/
[pyside]: https://doc.qt.io/qtforpython-6/
[pyinstaller]: https://pyinstaller.org/
[inno]: https://jrsoftware.org/isinfo.php
