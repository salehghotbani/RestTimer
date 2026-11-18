"""Verify actual start/stop dialogs, durable totals, discard and midnight splitting."""
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
import time
from datetime import datetime

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'linux'))
from PySide6.QtCore import QTimer, QDate
from PySide6.QtGui import QFont
from PySide6.QtWidgets import QApplication, QPushButton, QDialog
import work_tracking
import resttimer as module

app = QApplication([])
app.setStyleSheet(module.STYLE)
app.setFont(QFont("Segoe UI" if sys.platform == "win32" else "DejaVu Sans", 10))
with tempfile.TemporaryDirectory(dir=ROOT / 'build') as directory:
    os.environ['XDG_CONFIG_HOME'] = directory
    journal = work_tracking.Journal(Path(directory) / 'journal')
    start = datetime(2026, 11, 17, 23, 59, 58).timestamp()
    split = work_tracking.split_days('پروژه', start, 4)
    assert [row['Seconds'] for row in split] == [2, 2]
    journal.save('one', 'پروژه', start, 4)
    journal.save('one', 'پروژه', start, 4)
    journal.save('two', 'پروژه', start, 1)
    assert journal.report('2026-11-17') == [('پروژه', 3)]
    assert journal.report('2026-11-18') == [('پروژه', 2)]
    controller = module.Controller(app)
    controller.updater.state['daily_enabled'] = False
    controller.settings.update_panel.check_on_show = False
    def configure():
        dialog = app.activeModalWidget()
        assert isinstance(dialog, module.WorkStartDialog)
        dialog.task.setText('Write a report')
        dialog.duration.parts[0].setValue(0)
        dialog.duration.parts[1].setValue(0)
        dialog.duration.parts[2].setValue(30)
        dialog.begin()
    QTimer.singleShot(0, configure)
    controller.settings.reset_timer.click()
    assert controller.running and controller.task_name == 'Write a report'
    assert controller.session_seconds == 30 and controller.preferences['work_seconds'] == 3600
    time.sleep(.1)
    def choose(save):
        def answer():
            dialog = app.activeModalWidget()
            assert isinstance(dialog, QDialog)
            dialog.done(1 if save else 2)
        QTimer.singleShot(0, answer)
    choose(True)
    controller.stop_timer_action.trigger()
    assert not controller.running and controller.segment_id is None
    saved = controller.journal.report(QDate.currentDate().toString('yyyy-MM-dd'))
    assert len(saved) == 1 and saved[0][0] == 'Write a report' and saved[0][1] > 0
    controller.start_timer()
    time.sleep(.05)
    choose(False)
    controller.stop_timer()
    assert controller.journal.report(QDate.currentDate().toString('yyyy-MM-dd')) == saved
    controller.settings.select_page('report')
    assert controller.settings.report.rows.rowCount() == 1
    controller.settings.report.day.setDate(QDate(2000, 1, 1))
    assert controller.settings.report.rows.rowCount() == 0
    controller.settings.report.day.setDate(QDate.currentDate())
    controller.settings.show()
    app.processEvents()
    controller.settings.grab().save(str(ROOT / 'build/work-report-linux.png'))
    controller.start_timer()
    controller.begin_break()
    assert controller.segment_id is None
    assert all(hasattr(overlay, 'reset') for overlay in controller.overlays)
    controller.stop_timer()
    controller.overlays[0].reset.click()
    assert not controller.running and controller.breaking
    controller.begin_work()
    controller.quit()
print('Work dialogs, save/discard, reports, day splitting, idempotent storage and break reset passed.')
