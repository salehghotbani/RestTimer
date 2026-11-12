"""Release metadata, persistent daily scheduling and verified package downloads."""
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import tempfile
import time
import urllib.error
import urllib.request

API = "https://api.github.com/repos/salehghotbani/RestTimer/releases"
DOWNLOADS = "https://github.com/salehghotbani/RestTimer/releases/download/"
MAX_PACKAGE = 250 * 1024 * 1024


def version_tuple(tag):
    if not isinstance(tag, str) or not re.fullmatch(r"[vV]?\d+\.\d+(?:\.\d+){0,2}", tag):
        raise ValueError("Unsupported release version")
    parts = tuple(int(part) for part in tag.lstrip("vV").split("."))
    return parts + (0,) * (4 - len(parts))


def stable(release):
    try:
        return (isinstance(release, dict) and not release.get("draft")
                and not release.get("prerelease") and bool(version_tuple(release.get("tag_name"))))
    except ValueError:
        return False


def request_json(url):
    request = urllib.request.Request(url, headers={"User-Agent": "RestTimer-Updater", "Accept": "application/vnd.github+json",
                                                  "X-GitHub-Api-Version": "2022-11-28"})
    with urllib.request.urlopen(request, timeout=20) as response:
        data = response.read(8 * 1024 * 1024 + 1)
        if len(data) > 8 * 1024 * 1024:
            raise ValueError("Release response too large")
        return json.loads(data)


class Updater:
    def __init__(self, directory):
        self.directory = Path(directory)
        self.state = {"daily_enabled": True, "next_attempt": 0, "last_checked": 0, "notified_tag": None, "latest": None}
        try:
            data = json.loads((self.directory / "state.json").read_text(encoding="utf-8"))
            if isinstance(data, dict):
                self.state.update({key: data[key] for key in self.state if key in data})
            if type(self.state["daily_enabled"]) is not bool:
                self.state["daily_enabled"] = True
            for key in ("next_attempt", "last_checked"):
                if type(self.state[key]) not in (int, float):
                    self.state[key] = 0
        except (OSError, ValueError):
            pass

    def save(self):
        temporary = None
        try:
            self.directory.mkdir(parents=True, exist_ok=True, mode=0o700)
            with tempfile.NamedTemporaryFile(dir=self.directory, mode="w", encoding="utf-8", delete=False) as output:
                temporary = Path(output.name)
                json.dump(self.state, output, ensure_ascii=False)
            temporary.replace(self.directory / "state.json")
        except OSError:
            pass
        finally:
            if temporary:
                temporary.unlink(missing_ok=True)

    def due(self, now=None):
        now = time.time() if now is None else now
        return self.state["daily_enabled"] and (now >= self.state["next_attempt"] or self.state["next_attempt"] - now > 86400)

    def attempt(self, now=None):
        self.state["next_attempt"] = (time.time() if now is None else now) + 6 * 3600
        self.save()

    def accept_latest(self, release, now=None):
        now = time.time() if now is None else now
        self.state.update(latest=release, last_checked=now, next_attempt=now + 86400)
        self.save()

    def latest(self):
        try:
            release = request_json(API + "/latest")
        except urllib.error.HTTPError as error:
            if error.code == 404:
                return None
            raise
        if not stable(release):
            raise ValueError("Invalid stable release")
        return release

    def history(self):
        releases, page = [], 1
        while True:
            batch = request_json(API + f"?per_page=100&page={page}")
            if not isinstance(batch, list):
                raise ValueError("Invalid history")
            releases.extend(release for release in batch if stable(release))
            if len(batch) < 100:
                return sorted(releases, key=lambda release: version_tuple(release["tag_name"]), reverse=True)
            page += 1


def package_kind(os_release=None):
    data = platform.freedesktop_os_release() if os_release is None else os_release
    systems = {data.get("ID", ""), *data.get("ID_LIKE", "").split()}
    if systems & {"ubuntu", "debian"}:
        return "deb"
    if systems & {"fedora"}:
        return "rpm"
    raise ValueError("Unsupported package manager")


def installer_asset(release, kind):
    if not stable(release) or kind not in ("deb", "rpm"):
        raise ValueError("Invalid release or platform")
    version = ".".join(str(part) for part in version_tuple(release["tag_name"])[:3])
    name = f"RestTimer-v{version}-linux-" + ("amd64.deb" if kind == "deb" else "x86_64.rpm")
    matches = [asset for asset in release.get("assets", []) if isinstance(asset, dict) and asset.get("name") == name]
    if len(matches) != 1:
        raise ValueError("Installer not available")
    asset = matches[0]
    if (asset.get("browser_download_url") != DOWNLOADS + release["tag_name"] + "/" + name
            or type(asset.get("size")) is not int or not 0 < asset["size"] <= MAX_PACKAGE
            or not re.fullmatch(r"sha256:[a-fA-F0-9]{64}", str(asset.get("digest", "")))):
        raise ValueError("Invalid installer metadata")
    return asset


def download(release, kind, directory, cancelled, progress=lambda value: None, opener=urllib.request.urlopen):
    asset = installer_asset(release, kind)
    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=True, mode=0o700)
    target = directory / asset["name"]
    partial = None
    try:
        request = urllib.request.Request(asset["browser_download_url"], headers={"User-Agent": "RestTimer-Updater"})
        with opener(request, timeout=30) as response, tempfile.NamedTemporaryFile(dir=directory, suffix=".part", delete=False) as output:
            partial = Path(output.name)
            length = response.headers.get("Content-Length")
            if length is not None and int(length) != asset["size"]:
                raise ValueError("Installer size mismatch")
            total, digest = 0, hashlib.sha256()
            while True:
                if cancelled.is_set():
                    raise InterruptedError("Download cancelled")
                chunk = response.read(81920)
                if not chunk:
                    break
                total += len(chunk)
                if total > asset["size"]:
                    raise ValueError("Installer exceeds expected size")
                output.write(chunk)
                digest.update(chunk)
                progress(total * 100 // asset["size"])
            if total != asset["size"] or "sha256:" + digest.hexdigest() != asset["digest"].lower():
                raise ValueError("Installer verification failed")
        if cancelled.is_set():
            raise InterruptedError("Download cancelled")
        partial.replace(target)
        return target
    finally:
        if partial:
            partial.unlink(missing_ok=True)


def install_command(package, kind, find=shutil.which):
    if kind not in ("deb", "rpm") or not Path(package).is_absolute() or Path(package).suffix != "." + kind:
        raise ValueError("Invalid package path")
    pkexec, manager = find("pkexec"), find("apt-get" if kind == "deb" else "dnf")
    if not pkexec or not manager:
        raise OSError("Package manager or polkit is unavailable")
    return [pkexec, manager, "install", "-y", "--", str(package)]


def system_environment(environment=None):
    env = dict(os.environ if environment is None else environment)
    # Restore the system search path before running package tools from a frozen app.
    if "LD_LIBRARY_PATH_ORIG" in env:
        env["LD_LIBRARY_PATH"] = env.pop("LD_LIBRARY_PATH_ORIG")
    else:
        env.pop("LD_LIBRARY_PATH", None)
    for key in list(env):
        if key.startswith("_PYI_") or key in ("QT_PLUGIN_PATH", "QT_QPA_PLATFORM_PLUGIN_PATH"):
            env.pop(key, None)
    env["PYINSTALLER_RESET_ENVIRONMENT"] = "1"
    return env
