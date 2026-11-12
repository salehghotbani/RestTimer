# RestTimer changelog

## 1.9.2
- Preview the selected interface language immediately, including right-to-left layouts, navigation, update messages and the tray menu. Save keeps the choice across restarts; Discard restores the previous language.
- Preserve edited fields, checkbox choices, timer state and active update downloads when previewing languages.
- Give every Windows settings checkbox the same rounded, theme-aware appearance, including Show floating timer.
- Add matching rounded checkbox indicators and checkmarks for Linux light and dark themes.

## 1.9.1
- Remove the duplicate Updates and changes and Cancel buttons from General, and all Close / cancel buttons from settings sections.
- Prompt to save or discard pending settings when closing. Closing the prompt continues editing; invalid settings keep the window open. Daily checks and launch-at-sign-in changes now wait for Save.
- Hide the Windows floating countdown while a foreground game or application fills its display; restore it automatically when leaving fullscreen.
- Add slim, rounded, theme-aware scrollbars to settings, release notes and version lists on Windows and Linux.
- Reject minute and second values outside 0–59 while editing on Windows, including pasted Persian and Arabic numerals; retain the same bounds on Linux.
- Use “تنظیمات” for the Persian Settings menu entry and remove ellipses from tray menu labels in every language.

## 1.9.0
- Unify General settings, Updates, Version history and About in one window with vertical sidebar navigation on Windows and Linux.
- Add the daily update check switch to General settings and keep it synchronized with the Updates page; save changes immediately.
- Remove Version history from the tray menu; browse releases inside the settings window instead.
- Preserve active downloads while switching sections or changing the Linux interface language, and prevent closing the window during installation.
- Localize the new sections in all 13 supported languages with light/dark themes and right-to-left layouts.
- Use Qt 6.11.3 on Linux to avoid an upstream Qt 6.12.0 reference-count crash with Python 3.11; preserve release-note scroll positions when navigating sections.

## 1.8.2
- Make the tray menu more compact on Windows and Linux, with smaller row padding and outer margins.
- Use smaller Windows menu text and tighter rounded highlights and separators while retaining light/dark colors and all timer controls.

## 1.8.1
- Replace the full-width reserved Windows strip with a compact floating timer sized to its localized label and countdown.
- Add a solid light/dark background, rounded corners, subtle border and distinct work/break colors.
- Keep the box above application windows, including borderless fullscreen windows, without activation or dragging.
- Stop reserving desktop workspace or resizing maximized windows. Keep the box outside the taskbar at all four positions.
- Preserve the timer, pause state, language choice and visibility setting; rename the option to “Show floating timer”.

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
