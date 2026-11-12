"""Updater tests without network access or administrator privileges."""
import hashlib
import io
import json
from pathlib import Path
import sys
import tempfile
import threading
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "linux"))
import update_core as core


class Response(io.BytesIO):
    def __init__(self, data, length=None):
        super().__init__(data)
        self.headers = {"Content-Length": str(len(data) if length is None else length)}


class Updates(unittest.TestCase):
    def release(self, kind="deb"):
        name = "RestTimer-v9.0.0-linux-" + ("amd64.deb" if kind == "deb" else "x86_64.rpm")
        return {"tag_name": "v9.0.0", "body": "تغییرات نسخه", "assets": [{"name": name, "size": 5,
            "digest": "sha256:" + hashlib.sha256(b"hello").hexdigest(),
            "browser_download_url": core.DOWNLOADS + "v9.0.0/" + name}]}

    def test_schedule_persistence(self):
        with tempfile.TemporaryDirectory() as directory:
            updater = core.Updater(directory)
            self.assertTrue(updater.due(1000))
            updater.attempt(1000)
            self.assertFalse(updater.due(1000 + 5 * 3600))
            self.assertTrue(updater.due(1000 + 6 * 3600))
            updater.accept_latest(self.release(), 1000)
            self.assertFalse(updater.due(1000 + 86399))
            self.assertTrue(updater.due(1000 + 86400))
            updater.state["notified_tag"] = "v9.0.0"
            updater.save()
            restored = core.Updater(directory)
            self.assertEqual(restored.state["notified_tag"], "v9.0.0")
            self.assertEqual(restored.state["latest"]["body"], "تغییرات نسخه")
            restored.state["daily_enabled"] = False
            self.assertFalse(restored.due(1000 + 90000))

    def test_metadata(self):
        self.assertFalse(core.stable({"tag_name": "v10.0.0", "prerelease": True}))
        self.assertFalse(core.stable({"tag_name": "v10.0.0", "draft": True}))
        self.assertFalse(core.stable({"tag_name": "../../bad"}))
        for kind in ("deb", "rpm"):
            release = self.release(kind)
            self.assertTrue(core.installer_asset(release, kind)["name"].endswith("." + kind))
            for key, value in (("browser_download_url", "https://example.com/update"), ("digest", ""), ("size", core.MAX_PACKAGE + 1)):
                bad = json.loads(json.dumps(release))
                bad["assets"][0][key] = value
                with self.assertRaises(ValueError):
                    core.installer_asset(bad, kind)

    def test_download_failures_and_cancel(self):
        for kind in ("deb", "rpm"):
            with tempfile.TemporaryDirectory() as directory:
                release = self.release(kind)
                path = core.download(release, kind, directory, threading.Event(), opener=lambda *a, **k: Response(b"hello"))
                self.assertEqual(path.read_bytes(), b"hello")
                path.unlink()
                for data in (b"HELLO", b"he", b"hello and extra"):
                    with self.assertRaises(ValueError):
                        core.download(release, kind, directory, threading.Event(), opener=lambda *a, **k: Response(data, 5))
                    self.assertEqual(list(Path(directory).iterdir()), [])
                cancel = threading.Event()
                cancel.set()
                with self.assertRaises(InterruptedError):
                    core.download(release, kind, directory, cancel, opener=lambda *a, **k: Response(b"hello"))
                self.assertEqual(list(Path(directory).iterdir()), [])

    def test_install_and_environment(self):
        self.assertEqual(core.package_kind({"ID": "ubuntu"}), "deb")
        self.assertEqual(core.package_kind({"ID": "fedora"}), "rpm")
        for kind, tool in (("deb", "apt-get"), ("rpm", "dnf")):
            command = core.install_command(str(Path(tempfile.gettempdir()) / "path with space" / ("update." + kind)), kind, lambda name: "/usr/bin/" + name)
            self.assertEqual(command[:5], ["/usr/bin/pkexec", "/usr/bin/" + tool, "install", "-y", "--"])
            self.assertEqual(len(command), 6)
        env = core.system_environment({"LD_LIBRARY_PATH": "bundle", "LD_LIBRARY_PATH_ORIG": "system", "_PYI_PARENT_PROCESS_LEVEL": "1"})
        self.assertEqual(env["LD_LIBRARY_PATH"], "system")
        self.assertNotIn("_PYI_PARENT_PROCESS_LEVEL", env)
        self.assertEqual(env["PYINSTALLER_RESET_ENVIRONMENT"], "1")


if __name__ == "__main__":
    unittest.main()
