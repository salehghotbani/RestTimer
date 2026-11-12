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

The interface supports **13 languages**: English, Persian, Arabic, Simplified Chinese, Spanish, French, German, Portuguese, Russian, Japanese, Korean, Hindi and Turkish. The Windows installer supports **English and Persian**. This documentation is available in [English](README.md) and [Persian](README.fa.md).

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
sudo apt install ./RestTimer-v1.9.2-linux-amd64.deb
```

### Fedora

Open the RPM in your software manager, or run:

```bash
sudo dnf install ./RestTimer-v1.9.2-linux-x86_64.rpm
```

Launch RestTimer from the application menu or with `resttimer`. The Linux packages bundle Python and Qt. Uninstall with `sudo apt remove resttimer` or `sudo dnf remove resttimer`.

## Using RestTimer

- Left-click or right-click the tray icon to open the menu. **Start** («شروع»), **Stop** («توقف»), and **Start a break now** («شروع استراحت همین حالا») are the first entries. You can also change durations, toggle launch at sign-in, check for updates, and quit.
- The countdown starts when the application launches. Skipping a break resets the next work period.
- Start and Stop are available in the tray menu, settings, and break reminder. Stop pauses the current period; Start resumes from its remaining time. During a paused break, reminders stay open and can be resumed from any monitor. Skipping a break or changing durations preserves the paused state.
- On Windows, the update window and menu follow the Windows **application** color mode and support high contrast. Theme changes apply while the window is open. Linux follows the desktop color scheme reported by Qt.
- Release notes have headings, spacing, and paragraph direction appropriate for Persian and English. The Windows update window is resizable and has a separate version list for browsing release history.

### Windows floating timer

A compact floating box shows the countdown beside the primary taskbar. Its width fits the localized label and time; it has a solid background, rounded corners and a subtle border. The work or break label stays on the right and time on the left, with a paused indicator. Colors follow the Windows **system** light/dark mode and high-contrast settings.

| Taskbar position | Countdown placement |
| --- | --- |
| Bottom | Above the taskbar, near the notification area |
| Top | Below the taskbar, near the notification area |
| Left or right | Beside the taskbar’s inner edge, near the bottom of the screen; text stays horizontal |

In settings, toggle **Show floating timer**, then select **Save settings**. Your choice persists across restarts and does not reset the timer.

Only this small box floats above application windows, hiding automatically while a foreground fullscreen game or application fills the same display and returning when you leave fullscreen. It reserves no desktop row and does not resize maximized windows. It stays outside the taskbar, follows its position automatically, cannot be dragged, and lets mouse clicks pass through without taking focus. It remains visible when the taskbar auto-hides. Available taskbar positions depend on your Windows desktop configuration.

### Settings and startup

Settings opens one window with vertical tabs for **General**, **Updates**, **Version history**, and **About**. Browse releases and application details without opening extra windows. Version history is available here instead of in the tray menu.

In **General**, change durations, language, launch at sign-in, the floating timer, or daily update checks, then select **Save settings**. The daily-check switch on Updates stays synchronized with General. Changes remain pending until saved. If you close the window with unsaved changes, choose **Save changes** or **Discard changes**; close that dialog to continue editing. Unchanged settings close without a prompt.

Switching tabs preserves pending settings and running update downloads. A confirmed close cancels the download. The window stays open while an installer is running. Navigation uses the sidebar; General has no duplicate update or cancel buttons, and sections have no Close / cancel buttons.

Minute and second fields accept **0–59**, including Persian and Arabic digits on Windows. Settings, release notes and version lists use slim, rounded scrollbars that follow the system color mode.

The interface follows your system UI language by default. Unsupported languages fall back to English. Choosing a language in **Settings → Language** updates the interface immediately, including its reading direction. Edited fields, timer state and active update downloads are preserved. **Save settings** keeps the choice across restarts; **Discard changes** restores the previous language. **System language** returns to automatic detection. Persian and Arabic use right-to-left layouts; other languages use left-to-right layouts. Language names appear in their own language. All settings checkboxes share rounded, theme-aware indicators.

Windows settings are stored in the current user's account; older minute-based settings are still read. Linux stores settings in `~/.config/resttimer/settings.json` and its launch-at-sign-in entry in `~/.config/autostart/resttimer.desktop`.

### Linux desktops

The Linux frontend runs on X11 and Wayland. When desktop capture is unavailable on Wayland, a translucent overlay still dims the screen. Window placement and stacking are controlled by the desktop window manager.

On GNOME, tray visibility and click behavior depend on desktop support or an AppIndicator extension. If no system tray is available, RestTimer shows its control window; closing that window quits the application. Launching RestTimer again from the application menu opens the existing instance's settings window.

## Updates

While running, RestTimer checks the latest stable release at startup when due, then every 24 hours. A failed check is retried six hours later. The schedule persists across restarts, and each new version generates one notification. You can disable daily checks in **Settings → General** or **Updates**. System notifications must be allowed for RestTimer to show alerts.

Click the notification or open **Updates and changes**, read the release notes, and approve **Download and install**. **RestTimer downloads and installs the update itself.** Downloads show progress and can be cancelled. File size and SHA-256 are checked before installation.

Windows updates use the current installation path and installer language; Setup closes the running application and reopens it after installation. Ubuntu and Fedora updates use apt or dnf and request administrator authorization. Download failures or declined authorization leave the current application running.

The **Version history** page retrieves release notes from GitHub in their published language. An [English changelog](CHANGELOG.md) and [Persian changelog](CHANGELOG.fa.md) are bundled for offline use. Persian users see the Persian offline history; other interface languages use English offline history.

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
| **Python 3.11 and PySide6 Essentials / Qt 6.11** | Linux interface, duration controls, tray icon, and break windows |
| **Inno Setup 6** | Windows EXE installer, shortcuts, and uninstallation |
| **PyInstaller, dpkg-deb and rpmbuild** | Bundling Linux dependencies and building DEB and RPM packages |
| **GitHub Actions, Xvfb, Weston and Docker** | Building and checking installation, removal, and desktop execution on Windows, Ubuntu, Fedora, X11, and Wayland |

## Building and contributing

See the [contribution guide](CONTRIBUTING.md) for build commands, packaging, and checks. The repository workflow verifies Windows installation and removal, Ubuntu and Fedora package installation and removal, and Linux execution on X11 and Wayland.

English is the default documentation language. Keep the [Persian README](README.fa.md) and [Persian contribution guide](CONTRIBUTING.fa.md) in sync when updating the English guides.

RestTimer is released under the [MIT license](LICENSE). Bundled dependency licenses and source information are listed in [third-party notices](THIRD_PARTY_NOTICES.md).

[releases]: https://github.com/salehghotbani/RestTimer/releases/latest
[windows]: https://github.com/salehghotbani/RestTimer/releases/download/v1.9.2/RestTimer-v1.9.2-windows-x64-setup.exe
[ubuntu]: https://github.com/salehghotbani/RestTimer/releases/download/v1.9.2/RestTimer-v1.9.2-linux-amd64.deb
[fedora]: https://github.com/salehghotbani/RestTimer/releases/download/v1.9.2/RestTimer-v1.9.2-linux-x86_64.rpm
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
[pyside-badge]: https://img.shields.io/badge/PySide6_Essentials-6.11.3-167a4b?logo=qt&logoColor=white
[pyinstaller-badge]: https://img.shields.io/badge/PyInstaller-6.22.3-345d9d
[inno-badge]: https://img.shields.io/badge/Inno_Setup-6-475569
[winforms]: https://learn.microsoft.com/dotnet/desktop/winforms/
[dotnet]: https://dotnet.microsoft.com/download/dotnet-framework/net48
[python]: https://www.python.org/
[pyside]: https://doc.qt.io/qtforpython-6/
[pyinstaller]: https://pyinstaller.org/
[inno]: https://jrsoftware.org/isinfo.php
