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
bash tools/build_linux.sh
```

Building packages requires Linux x86-64, `dpkg-deb`, `rpmbuild`, and `desktop-file-validate`. The [workflow](https://github.com/salehghotbani/RestTimer/blob/main/.github/workflows/packages.yml) lists the build dependencies and checks installation on Ubuntu and Fedora.

## Reviewing changes

- Try one-second work and break periods, skipping a break, and starting the next period.
- Check that both left-click and right-click open the tray menu, and that clicking outside it or pressing Esc closes it.
- Verify synchronized reminders across monitors and the hour, minute, and second controls.
- Check countdown placement with bottom, top, left, and right taskbars, display scaling, and monitors at negative coordinates. Disabling the strip or quitting must restore the reserved workspace.
- Preserve compatibility with previous settings when changing their format.
- Treat `VERSION` as the shared Windows and Linux version source. Each application release should include an EXE installer, a DEB package, and an RPM package.
- Add release changes to `CHANGELOG.md` and the GitHub release notes. The application uses release notes to explain new versions.
- Check updates with a disconnected network, cancelled downloads, incomplete files, declined installation authorization, and restarting after an upgrade. Download checks must not launch a real installer.

## Documentation languages

Interface translations live in the shared `locales/*.json` catalogs. Use the keys and placeholders in `locales/en.json` as the reference; keep every supported catalog complete. Preserve `{0}` placeholders and the catalog direction (`rtl` for Persian and Arabic, `ltr` for the other supported languages). Run the catalog and UI localization checks after changing translations. Verify that selecting a language preserves the timer and that **System language** restores automatic detection. Native language names are used in both selectors.

**English is the default documentation language.** Maintain these matching guides:

| Guide | English (default) | Persian |
| --- | --- | --- |
| Application guide | [README.md](README.md) | [README.fa.md](README.fa.md) |
| Contribution guide | [CONTRIBUTING.md](CONTRIBUTING.md) | [CONTRIBUTING.fa.md](CONTRIBUTING.fa.md) |

Update both language versions when changing documented behavior or instructions. Keep the language selectors, installation commands, release links, and technology badges consistent. Use English by default for new documentation.

See the [README](README.md) for installation and usage. By submitting changes, you agree to contribute them under the repository's [MIT license](LICENSE).
