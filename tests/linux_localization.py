"""Exercise the real language selector while preserving a paused timer."""
import json
import os
from pathlib import Path
import sys
import tempfile

from PySide6.QtCore import Qt
from PySide6.QtWidgets import QApplication

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "linux"))
import resttimer as module
import i18n

app = QApplication([])
app.setQuitOnLastWindowClosed(False)
module.verify_language_fonts(app)
(ROOT / "build").mkdir(exist_ok=True)
with tempfile.TemporaryDirectory(dir=ROOT / "build") as directory:
    os.environ["XDG_CONFIG_HOME"] = directory
    module.atomic_write(module.config_root() / "resttimer/settings.json", json.dumps({"work_seconds": 3723, "break_seconds": 901}))
    controller = module.Controller(app)
    controller.updater.state["daily_enabled"] = False
    controller.stop_timer()
    controller.settings.show()
    old_deadline, old_pause = controller.deadline, controller.paused_at
    # Retain the update dialog during a language change, including a running download.
    dialog = module.UpdateDialog(controller, check_on_show=False)
    controller.update_dialog = dialog
    dialog.worker = object()
    for code in i18n.SUPPORTED:
        controller.settings.language.setCurrentIndex(controller.settings.language.findData(code))
        controller.settings.save.click()
        app.processEvents()
        assert i18n.code == code
        assert module.read_preferences()["language"] == code
        assert controller.deadline == old_deadline and controller.paused_at == old_pause
        assert not controller.running and controller.preferences["work_seconds"] == 3723
        assert controller.settings.language.count() == len(i18n.SUPPORTED) + 1
        assert app.layoutDirection() == (Qt.RightToLeft if code in ("fa", "ar") else Qt.LeftToRight)
        assert dialog.heading.text() == i18n.tr("updates_title")
        assert not dialog.install.isEnabled(), "Changing languages enabled installation during a download"
        assert controller.start_timer_action.text() == i18n.tr("start")
        for mode, name in ((Qt.ColorScheme.Light, "light"), (Qt.ColorScheme.Dark, "dark")):
            module.apply_color_scheme(app, mode)
            dialog.show()
            app.processEvents()
            controller.settings.grab().save(str(ROOT / f"build/linux-localization-settings-{code}-{name}.png"))
            dialog.grab().save(str(ROOT / f"build/linux-localization-updates-{code}-{name}.png"))
        print("Localized settings, persistence, paused timer and active update passed:", code)
    controller.settings.language.setCurrentIndex(0)
    controller.settings.save.click()
    app.processEvents()
    assert module.read_preferences()["language"] == "auto"
    assert i18n.code == i18n.resolve("auto", module.QLocale.system().uiLanguages())
    assert controller.deadline == old_deadline and controller.paused_at == old_pause
    dialog.worker = None
    dialog.close()
    controller.quit()
print("All language selectors, persistence, directions and timer preservation passed.")
