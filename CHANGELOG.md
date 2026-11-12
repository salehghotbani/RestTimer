# RestTimer changelog

## 1.8.0
- Interface translations for English, Persian, Arabic, Simplified Chinese, Spanish, French, German, Portuguese, Russian, Japanese, Korean, Hindi and Turkish.
- Follow the system UI language by default, with English as the fallback for unsupported languages.
- A saved language selector in settings, including an option to return to the system language.
- Apply language changes without restarting the application or resetting the remaining time or paused state.
- Localized menus, settings, break reminders, countdown labels, update messages and notifications, with right-to-left layout for Persian and Arabic.
- English and Persian offline release histories; release notes from GitHub retain their published language.

## 1.7.1
- Show the countdown with left and right taskbars, in a thin bottom row near the same side.
- Support top taskbars with the strip underneath, while retaining the strip above bottom taskbars.
- Adapt to taskbar movement while preserving horizontal text, reserved workspace and separation from taskbar icons.
- Use one “Show countdown strip” setting for all taskbar positions.

## 1.7.0
- Move the Windows countdown to a separate thin strip above the taskbar, with real Shell-managed workspace reservation.
- Keep taskbar icons and maximized windows out of the countdown’s space.
- Release workspace when disabling the strip or quitting; re-enable without changing remaining time.
- Preserve compact transparent text, system-aware colors and click-through behavior.
- Include English-default and Persian README and contribution guides with all installers.

## 1.6.0
- Place Start, Stop and Start a break now first in the tray menu.
- Redesign the Windows update and history window with consistent navigation and a separate version selector.
- Follow Windows application light/dark mode and support high contrast.
- Improve release-note headings, spacing and Persian/English paragraph direction; remove repeated titles.
- Add a resizable layout and themed progress bar while retaining explicit installation approval.
- Follow the Linux desktop color scheme reported by Qt.

## 1.5.1
- Use a thinner single-line countdown with smaller text, label on the right and time on the left.
- Remove the countdown background and border.
- Follow Windows system light/dark mode and high contrast for text colors.

## 1.5.0
- Add a fixed floating countdown beside the Windows notification area and chevron.
- Share work, break and paused states with the main timer.
- Add a persistent setting to show or hide the countdown.
- Prevent dragging, allow clicks through, and hide with the taskbar or another fullscreen application.

## 1.4.0
- Check for updates daily and notify about new versions.
- Show new-version changes before approving download and installation.
- Download with progress, cancellation and file verification.
- Add version history and bundled offline release notes.
- Install updates with user approval on Windows, Ubuntu and Fedora.

## 1.3.2
- Explain automatic close and restart during setup in English and Persian.
- Close a running application and reopen it after installation, including older versions.

## 1.3.1
- Choose English or Persian before Windows setup begins.
- Translate installation and removal steps, options and messages.

## 1.3.0
- Add Start and Stop to the tray menu, settings and break reminder.
- Resume the remaining countdown and synchronize monitors.

## 1.2.0
- Add a Windows EXE installer, Ubuntu DEB and Fedora RPM.
- Style the tray menu and support left-click and right-click.
- Set hours, minutes and seconds, with synchronized reminders across monitors.

## 1.1.0
- Add manual update checks and the Persian break-schedule interface.
