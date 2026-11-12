"""Create the x86-64 Debian or RPM package from the same Linux bundle."""

import argparse
import os
from pathlib import Path
import platform
import shutil
import subprocess
from tempfile import TemporaryDirectory

ROOT = Path(__file__).resolve().parents[1]
VERSION = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
PROJECT_URL = "https://github.com/salehghotbani/RestTimer"
DESKTOP_ID = "resttimer"
DEPENDENCIES = (
    "ca-certificates", "pkexec", "fonts-dejavu-core", "libdbus-1-3", "libegl1", "libfontconfig1",
    "libgl1", "libglib2.0-0t64 | libglib2.0-0", "libice6", "libsm6", "libssl3t64 | libssl3",
    "libwayland-client0", "libwayland-cursor0", "libwayland-egl1", "libx11-6", "libx11-xcb1",
    "libxcb-cursor0", "libxcb-icccm4", "libxcb-image0", "libxcb-keysyms1", "libxcb-randr0",
    "libxcb-render-util0", "libxcb-shape0", "libxcb-shm0", "libxcb-sync1", "libxcb-xfixes0",
    "libxcb-xkb1", "libxext6", "libxkbcommon-x11-0", "libxkbcommon0", "libxrender1", "xkb-data",
)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rpm", action="store_true", help="Build the Fedora RPM")
    args = parser.parse_args()
    if platform.system() != "Linux" or platform.machine() != "x86_64":
        raise SystemExit("Build the Linux packages on Linux x86-64.")
    version = VERSION
    output = ROOT / "dist/release"
    output.mkdir(parents=True, exist_ok=True)
    desktop = ROOT / "packaging" / f"{DESKTOP_ID}.desktop"
    subprocess.run(["desktop-file-validate", str(desktop)], check=True)
    with TemporaryDirectory(prefix="package-", dir=ROOT / "build") as directory:
        package = Path(directory) / "payload"
        package.mkdir()
        os.chmod(package, 0o755)
        shutil.copytree(ROOT / "dist/resttimer", package / "opt/resttimer")
        (package / "usr/bin").mkdir(parents=True)
        (package / "usr/bin/resttimer").symlink_to("/opt/resttimer/resttimer")
        for source, destination in (
            (desktop, f"usr/share/applications/{desktop.name}"),
            (ROOT / "assets/RestTimer.png", "usr/share/icons/hicolor/256x256/apps/resttimer.png"),
            (ROOT / "LICENSE", "usr/share/doc/resttimer/copyright"),
            (ROOT / "THIRD_PARTY_NOTICES.md", "usr/share/doc/resttimer/THIRD_PARTY_NOTICES.md"),
            (ROOT / "README.md", "usr/share/doc/resttimer/README.md"),
            (ROOT / "README.fa.md", "usr/share/doc/resttimer/README.fa.md"),
            (ROOT / "CONTRIBUTING.md", "usr/share/doc/resttimer/CONTRIBUTING.md"),
            (ROOT / "CONTRIBUTING.fa.md", "usr/share/doc/resttimer/CONTRIBUTING.fa.md"),
            (ROOT / "assets/RestTimer.png", "usr/share/doc/resttimer/assets/RestTimer.png"),
            (ROOT / "LICENSE", "usr/share/doc/resttimer/LICENSE"),
            (ROOT / "CHANGELOG.md", "usr/share/doc/resttimer/CHANGELOG.md"),
            (ROOT / "CHANGELOG.fa.md", "usr/share/doc/resttimer/CHANGELOG.fa.md"),
        ):
            target = package / destination
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
        from PySide6.QtCore import Qt
        from PySide6.QtGui import QImage

        icon_path = package / "usr/share/icons/hicolor/256x256/apps/resttimer.png"
        icon = QImage(str(icon_path)).scaled(256, 256, Qt.KeepAspectRatio, Qt.SmoothTransformation)
        if not icon.save(str(icon_path)):
            raise RuntimeError("Could not write the application icon.")
        shutil.copytree(ROOT / "licenses", package / "usr/share/doc/resttimer/licenses")
        if args.rpm:
            build_rpm(Path(directory), package, version, output)
            return
        control = package / "DEBIAN/control"
        control.parent.mkdir()
        size = sum(path.stat().st_size for path in package.rglob("*") if path.is_file())
        control.write_text(
            f"Package: resttimer\nVersion: {version}\nArchitecture: amd64\n"
            "Maintainer: Saleh Ghotbani <salehghotbani@users.noreply.github.com>\n"
            f"Section: utils\nPriority: optional\nInstalled-Size: {(size + 1023) // 1024}\n"
            f"Depends: {', '.join(DEPENDENCIES)}\nHomepage: {PROJECT_URL}\n"
            "Description: Persian desktop work and break timer\n"
            " Schedule synchronized breaks on all displays with configurable durations.\n",
            encoding="utf-8",
        )
        subprocess.run(["dpkg-deb", "--root-owner-group", "--build", str(package),
                        str(output / f"RestTimer-v{version}-linux-amd64.deb")], check=True)


def build_rpm(work, payload, version, output):
    spec = ROOT / "packaging/fedora.spec"
    subprocess.run([
        "rpmbuild", "-bb", "--define", f"_topdir {work}",
        "--define", f"app_version {version}", "--define", f"app_payload {payload}", str(spec),
    ], check=True)
    package = work / "RPMS" / "x86_64" / f"resttimer-{version}-1.x86_64.rpm"
    shutil.copyfile(package, output / f"RestTimer-v{version}-linux-x86_64.rpm")


if __name__ == "__main__":
    main()
