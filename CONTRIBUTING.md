# Contributing to RestTimer

**English** | [فارسی](CONTRIBUTING.fa.md)

Report bugs and suggest improvements in [Issues](https://github.com/salehghotbani/RestTimer/issues). For interface issues, include the application version, operating system, monitor count, display scaling, and, on Linux, whether you use X11 or Wayland.

Fork the repository and submit related changes on a separate branch. Explain the resulting behavior and how you verified it in your pull request. Include screenshots for visual changes.

## Windows development

Install a .NET SDK, the .NET Framework 4.8 Developer Pack, and Inno Setup 6 for installer builds.

```powershell
dotnet build -c Release
powershell -File tools/build_windows.ps1
dotnet build tests/updates/WindowsUpdateTests.csproj -c Release -o obj/update-tests
./obj/update-tests/WindowsUpdateTests.exe
./tests/windows_localization.ps1
```

If Inno Setup is installed in a custom location, set the `ISCC` environment variable to the full path of `ISCC.exe`.

## Linux development

The Linux frontend is in `linux/resttimer.py`. Create a virtual environment and install the build dependencies:

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -r requirements-build-linux.txt
python linux/resttimer.py
python tests/test_update_core.py
python tests/test_localization.py
QT_QPA_PLATFORM=offscreen python tests/linux_smoke.py
QT_QPA_PLATFORM=offscreen python tests/linux_localization.py
QT_QPA_PLATFORM=offscreen python tests/visual_audit.py
bash tools/build_linux.sh
```

Building packages requires Linux x86-64, `dpkg-deb`, `rpmbuild`, and `desktop-file-validate`. The [workflow](https://github.com/salehghotbani/RestTimer/blob/main/.github/workflows/packages.yml) lists the build dependencies and checks installation on Ubuntu and Fedora.

## Reviewing changes

- Try one-second work and break periods, skipping a break, and starting the next period.
- Check that left-click opens Dashboard and right-click opens the tray menu, and that clicking outside it or pressing Esc closes it.
- Verify synchronized reminders across monitors and the hour, minute, and second controls.
- Check floating-timer placement with bottom, top, left, and right taskbars, display scaling, and monitors at negative coordinates. Showing, pausing, hiding or closing the box must not change the desktop work area. Verify topmost behavior, click-through and light/dark backgrounds.
- Preserve compatibility with previous settings when changing their format.
- Treat `VERSION` as the shared Windows and Linux version source. Each application release should include an EXE installer, a DEB package, and an RPM package.
- Add release changes to `CHANGELOG.md` and the GitHub release notes. The application uses release notes to explain new versions.
- Check updates with a disconnected network, cancelled downloads, incomplete files, declined installation authorization, and restarting after an upgrade. Download checks must not launch a real installer.
- Check Dashboard, General, Updates, Version history and About, including all four General cards, in both layout directions and color modes. Daily update switches must stay synchronized and persist. Navigating between sections must preserve active downloads; closing must cancel downloads and wait for active installers.
- Check Save, Discard, and closing the unsaved-changes prompt. Duration, language, floating-timer, startup and daily-check drafts must not apply on Discard. Invalid settings must keep General open; closing an unchanged window must not prompt.
- Change languages before saving and verify immediate text and direction updates, retained field edits and downloads, and restoration of the original language on Discard. Compare every checkbox in checked, unchecked, focused, light and dark states.
- Verify that the floating countdown hides for a foreground fullscreen window on its own monitor, returns afterward without taking focus, and remains visible over ordinary windows. Check minute/second limits with typed and pasted digits, and scrollbar dragging, wheel scrolling and keyboard navigation in long notes. General must fit without scrolling.

## Documentation languages

Interface translations live in the shared `locales/*.json` catalogs. Use the keys and placeholders in `locales/en.json` as the reference; keep every supported catalog complete. Preserve `{0}` placeholders and the catalog direction (`rtl` for Persian and Arabic, `ltr` for the other supported languages). Run the catalog and UI localization checks after changing translations. Verify that selecting a language preserves the timer and that **System language** restores automatic detection. Native language names are used in both selectors.

**English is the default documentation language.** Maintain these matching guides:

| Guide | English (default) | Persian |
| --- | --- | --- |
| Application guide | [README.md](README.md) | [README.fa.md](README.fa.md) |
| Contribution guide | [CONTRIBUTING.md](CONTRIBUTING.md) | [CONTRIBUTING.fa.md](CONTRIBUTING.fa.md) |

Update both language versions when changing documented behavior or instructions. Keep the language selectors, installation commands, release links, and technology badges consistent. Use English by default for new documentation.

See the [README](README.md) for installation and usage. By submitting changes, you agree to contribute them under the repository's [MIT license](LICENSE).

Run `./tests/windows_visual.ps1` for the Windows visual audit. It and `tests/visual_audit.py` cover every page and dialog in all 13 languages, both themes, normal/minimum sizes and both sidebar states. Reports and contact sheets are written to `obj/visual-audit` and `build/visual-audit`. The audits also exercise interrupted/reversed sidebar animations.

## Local checks before pushing

Application Actions are paused until explicitly re-enabled. Do not dispatch or re-enable them for app changes. Website deployment should be triggered only by actual changes in its own website repository/paths; this repository currently has no website workflow.

On a Windows host with Docker Desktop (Linux containers), .NET SDK, GitHub CLI and Inno Setup installed, enable the tracked hook with `git config core.hooksPath .githooks`. Commit first, then run `./tools/check-before-push.ps1`, or push to invoke it. Windows and Ubuntu 22.04/Python 3.11.9 checks run locally in parallel; Ubuntu 24.04/Fedora 43 installation checks also run locally. First-time image setup downloads dependencies; later builds use cached layers. Registry preferences are restored after installer checks. Logs are in `build/check-*.log`.

A failed or unavailable check blocks push. An exact clean Git tree that passed within 24 hours may reuse its receipt. No installer upload, release publication or website deployment occurs in these scripts.
