"""Exercise the real language selector while preserving a paused timer."""
import json
import os
from pathlib import Path
import sys
import tempfile
import threading

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
    controller.updater.save()
    controller.settings.daily.setChecked(False)
    controller.settings.remember_saved_settings()
    controller.stop_timer()
    controller.settings.show()
    old_deadline, old_pause = controller.deadline, controller.paused_at
    # Retain the update dialog during a language change, including a running download.
    dialog = controller.settings.update_panel
    dialog.check_on_show = False
    class PendingDownload:
        def __init__(self): self.cancelled = threading.Event()
    pending = PendingDownload()
    dialog.worker = pending
    original_revision = dialog.latest_notes.document().revision()
    dialog.latest_notes.verticalScrollBar().setValue(100)
    original_scroll = dialog.latest_notes.verticalScrollBar().value()
    for page in ("updates", "history", "about", "general"):
        controller.settings.select_page(page)
        app.processEvents()
        assert controller.settings.update_panel is dialog and not pending.cancelled.is_set()
    assert dialog.latest_notes.document().revision() == original_revision
    assert dialog.latest_notes.verticalScrollBar().value() == original_scroll
    controller.settings.daily.setChecked(True)
    controller.settings.daily.setChecked(False)
    assert not dialog.daily.isChecked()
    assert not module.update_core.Updater(module.config_root() / "resttimer/updates").state["daily_enabled"]
    dialog.daily.setChecked(True)
    assert controller.settings.daily.isChecked()
    assert not controller.updater.state["daily_enabled"], "Draft daily checks persisted before Save"
    controller.settings.daily.setChecked(False)
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
        assert dialog.heading.text() == i18n.tr("version_history" if dialog.current_history else "updates_title")
        assert not dialog.install.isEnabled(), "Changing languages enabled installation during a download"
        assert controller.start_timer_action.text() == i18n.tr("start")
        assert controller.settings.navigation.count() == 4
        assert controller.settings.update_panel is dialog
        assert not pending.cancelled.is_set()
        for mode, name in ((Qt.ColorScheme.Light, "light"), (Qt.ColorScheme.Dark, "dark")):
            module.apply_color_scheme(app, mode)
            for page in ("general", "updates", "history", "about"):
                controller.settings.select_page(page)
                app.processEvents()
                assert controller.settings.update_panel is dialog and not pending.cancelled.is_set()
                controller.settings.grab().save(str(ROOT / f"build/linux-localization-{page}-{code}-{name}.png"))
            controller.settings.select_page("general")
            prompt = module.UnsavedChangesDialog(controller.settings)
            prompt.show()
            app.processEvents()
            assert prompt.layoutDirection() == app.layoutDirection()
            prompt.grab().save(str(ROOT / f"build/linux-localization-unsaved-{code}-{name}.png"))
            prompt.close()
            prompt.deleteLater()
        print("Localized settings, persistence, paused timer and active update passed:", code)
    controller.settings.language.setCurrentIndex(0)
    controller.settings.save.click()
    app.processEvents()
    assert module.read_preferences()["language"] == "auto"
    assert i18n.code == i18n.resolve("auto", module.QLocale.system().uiLanguages())
    assert controller.deadline == old_deadline and controller.paused_at == old_pause
    dialog.worker = None
    assert dialog.request_close()
    controller.settings.select_page("updates")
    replacement = controller.settings.update_panel
    replacement.check_on_show = False
    assert replacement is not dialog and not replacement.closed
    assert controller.update_dialog is replacement
    assert replacement.request_close()
    controller.quit()
print("All language selectors, persistence, directions and timer preservation passed.")
