"""The tray popup must keep its frame and hit targets stable while time updates."""
import os
from pathlib import Path
import sys
import tempfile
import time
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'linux'))
from PySide6.QtCore import QPoint, Qt
from PySide6.QtGui import QFont
from PySide6.QtWidgets import QApplication
import resttimer as module
app=QApplication([])
app.setFont(QFont('Segoe UI' if sys.platform=='win32' else 'DejaVu Sans',10))
module.apply_color_scheme(app,Qt.ColorScheme.Dark)

def settle():
    for _ in range(3): app.processEvents()

with tempfile.TemporaryDirectory(dir=ROOT/'build') as folder:
    os.environ['XDG_CONFIG_HOME']=folder
    controller=module.Controller(app)
    controller.updater.state['daily_enabled']=False
    controller.settings.update_panel.check_on_show=False
    area=app.primaryScreen().availableGeometry()
    for code in ('en','fa'):
        controller.preview_language(code)
        for name in ('','Report work','W'*120):
            controller.task_name=name
            controller.paused_at=time.monotonic()
            controller.deadline=controller.paused_at+3600
            controller.tick()
            controller.menu.popup(QPoint(area.right()-2,area.bottom()-2))
            settle()
            bounds=controller.menu.geometry()
            rows=[controller.menu.actionGeometry(action) for action in controller.menu.actions()]
            assert bounds.width()<=area.width() and bounds.height()<=area.height()
            changed=[]
            def count(): changed.append(controller.status.text())
            controller.status.changed.connect(count)
            for _ in range(12): controller.tick()
            assert not changed, 'A paused, unchanged label must not be rewritten'
            for remaining in (3599,3598,600,599,60,59):
                controller.deadline=controller.paused_at+remaining
                controller.tick()
                settle()
                assert controller.menu.geometry()==bounds, 'Popup frame moved or resized'
                assert [controller.menu.actionGeometry(action) for action in controller.menu.actions()]==rows, f'Menu hit targets moved: {code=} {name=} {remaining=} old={rows[0]} new={controller.menu.actionGeometry(controller.menu.actions()[0])} style={controller.menu.styleSheet()}'
            assert len(changed)==6, 'One final label update is expected for each changed time'
            controller.status.changed.disconnect(count)
            controller.menu.close()
            settle()
    controller.task_name=None
    controller.quit()
print('Stable popup geometry and hit targets, atomic labels, long names and live countdowns passed in English and Persian.')
