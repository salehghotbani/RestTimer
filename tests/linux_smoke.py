"""Exercise Linux settings, scheduling, tray actions and display coverage."""
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import time

from PySide6.QtCore import Qt
from PySide6.QtWidgets import QApplication

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("resttimer_linux", ROOT / "linux/resttimer.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

app = QApplication([])
app.setLayoutDirection(Qt.RightToLeft)
app.setStyleSheet(module.STYLE)
(ROOT / "build").mkdir(exist_ok=True)
with tempfile.TemporaryDirectory(dir=ROOT / "build") as directory:
    os.environ["XDG_CONFIG_HOME"] = directory
    assert module.read_preferences() == module.DEFAULTS
    settings_path = Path(directory) / "resttimer/settings.json"
    module.atomic_write(settings_path, json.dumps({"work_seconds": 1, "break_seconds": 2}))
    assert module.read_preferences()["break_seconds"] == 2
    module.atomic_write(settings_path, '{"work_seconds":true,"break_seconds":0}')
    assert module.read_preferences() == module.DEFAULTS
    controller = module.Controller(app)
    controller.settings.show()
    controller.settings.work.parts[0].setValue(1)
    controller.settings.work.parts[1].setValue(2)
    controller.settings.work.parts[2].setValue(3)
    controller.settings.save.click()
    assert controller.preferences["work_seconds"] == 3723
    assert module.read_preferences()["work_seconds"] == 3723
    controller.preferences = {"work_seconds": 1, "break_seconds": 2}
    controller.deadline = time.monotonic() - 1
    controller.tick()
    app.processEvents()
    assert controller.breaking and len(controller.overlays) == len(app.screens())
    controller.overlays[0].skip.click()
    assert not controller.breaking and not controller.overlays
    controller.start_action.trigger()
    assert controller.breaking
    controller.deadline = time.monotonic() - 1
    controller.tick()
    assert not controller.breaking
    assert module.version_tuple("v1.2.0") == module.version_tuple("1.2")
    assert module.version_tuple("v1.3.0") > module.version_tuple("1.2.0")
    module.set_startup(True)
    assert "--background" in module.startup_path().read_text()
    module.set_startup(False)
    assert not module.startup_path().exists()
    output = ROOT / "build"
    output.mkdir(exist_ok=True)
    app.processEvents()
    controller.settings.grab().save(str(output / "linux-settings.png"))
    controller.menu.popup(controller.settings.pos())
    app.processEvents()
    assert controller.menu.isVisible()
    controller.menu.grab().save(str(output / "linux-menu.png"))
    controller.menu.close()
    controller.quit()
print("Linux settings, second-precision transitions, skip, tray actions, autostart and display coverage passed.")
