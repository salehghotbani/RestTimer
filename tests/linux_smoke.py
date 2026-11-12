"""Exercise Linux settings, scheduling, tray actions and display coverage."""
import importlib.util
import json
import math
import os
from pathlib import Path
import tempfile
import time
import sys
import threading

from PySide6.QtCore import Qt
from PySide6.QtGui import QFont
from PySide6.QtWidgets import QApplication

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "linux"))
spec = importlib.util.spec_from_file_location("resttimer_linux", ROOT / "linux/resttimer.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

app = QApplication([])
app.setLayoutDirection(Qt.RightToLeft)
app.setStyleSheet(module.STYLE)
app.setFont(QFont("DejaVu Sans", 10))
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
    controller.updater.state["daily_enabled"] = False
    controller.settings.show()
    app.processEvents()
    for editor in (controller.settings.work, controller.settings.rest):
        for part in editor.parts:
            assert part.lineEdit().width() > 30
            assert part.lineEdit().text()
    controller.settings.work.parts[0].setValue(1)
    controller.settings.work.parts[1].setValue(2)
    controller.settings.work.parts[2].setValue(3)
    controller.settings.save.click()
    assert controller.preferences["work_seconds"] == 3723
    assert module.read_preferences()["work_seconds"] == 3723
    controller.preferences = {"work_seconds": 1, "break_seconds": 2}
    controller.deadline = time.monotonic() + 0.5
    controller.stop_timer_action.trigger()
    paused_at = controller.paused_at
    frozen = controller.deadline - paused_at
    status = controller.status.text()
    time.sleep(1.1)
    controller.tick()
    assert not controller.breaking and controller.paused_at == paused_at
    assert controller.status.text() == status
    resume_before = time.monotonic()
    controller.start_timer_action.trigger()
    resume_after = time.monotonic()
    assert controller.running
    assert resume_before + frozen - 0.001 <= controller.deadline <= resume_after + frozen + 0.001
    controller.settings.stop_timer.click()
    assert not controller.running
    controller.settings.start_timer.click()
    assert controller.running
    controller.deadline = time.monotonic() - 1
    controller.tick()
    app.processEvents()
    assert controller.breaking and len(controller.overlays) == len(app.screens())
    controller.overlays[0].stop.click()
    frozen = controller.deadline - controller.paused_at
    countdown = controller.overlays[0].countdown.text()
    time.sleep(2.1)
    controller.tick()
    assert controller.breaking and not controller.running
    assert controller.overlays[0].countdown.text() == countdown
    controller.clear_overlays()
    controller.tick()
    for overlay in controller.overlays:
        assert overlay.start.isEnabled() and not overlay.stop.isEnabled()
    resume_before = time.monotonic()
    controller.overlays[-1].start.click()
    resume_after = time.monotonic()
    assert controller.running
    assert resume_before + frozen - 0.001 <= controller.deadline <= resume_after + frozen + 0.001
    assert all(not overlay.start.isEnabled() and overlay.stop.isEnabled() for overlay in controller.overlays)
    controller.overlays[0].stop.click()
    app.processEvents()
    controller.overlays[0].card.grab().save(str(ROOT / "build/linux-overlay-paused.png"))
    controller.overlays[0].skip.click()
    assert not controller.breaking and not controller.overlays
    assert not controller.running
    assert math.isclose(
        controller.deadline - controller.paused_at,
        controller.preferences["work_seconds"],
        rel_tol=0,
        abs_tol=0.000001,
    )
    controller.settings.work.parts[2].setValue(4)
    controller.settings.save.click()
    assert not controller.running and controller.preferences["work_seconds"] == 3724
    controller.settings.start_timer.click()
    assert controller.running
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
    controller.updater.state["latest"] = {"tag_name": "v9.0.0", "body": "تغییرات آزمایشی نسخهٔ جدید", "assets": []}
    dialog = module.UpdateDialog(controller, check_on_show=False)
    dialog.show()
    app.processEvents()
    assert dialog.install.isEnabled() and "تغییرات" in dialog.latest_notes.toPlainText()
    assert dialog.tabs.count() == 2 and module.VERSION in dialog.history_notes.toPlainText()
    dialog.grab().save(str(output / "linux-updates.png"))
    dialog.tabs.setCurrentIndex(1)
    app.processEvents()
    dialog.grab().save(str(output / "linux-history.png"))
    controller.update_dialog = dialog
    dialog.process = object()
    controller.quit()
    assert not controller.quitting, "Quitting interrupted an active package installation"
    dialog.process = None
    dialog.reject()
    assert dialog.closed
    results = []
    main_thread = threading.get_ident()
    controller.run_update_job(lambda worker: "done", lambda value: results.append((value, threading.get_ident())), lambda error: results.append(error))
    for _ in range(200):
        app.processEvents()
        if results and not controller.workers:
            break
        time.sleep(0.01)
    assert results == [("done", main_thread)], "Background updater did not return to the UI thread"
    controller.quit()
print("Linux settings, second-precision transitions, skip, tray actions, autostart and display coverage passed.")
