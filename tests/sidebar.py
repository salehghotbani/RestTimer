"""Exercise sidebar toggling and physical right-side scrollbars in RTL and LTR."""
import os
from pathlib import Path
import sys
import tempfile
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'linux'))
from PySide6.QtCore import QPoint, Qt
from PySide6.QtGui import QFont
from PySide6.QtTest import QTest
from PySide6.QtWidgets import QApplication, QStyleOptionViewItem
import resttimer as module
import i18n
app=QApplication([])
app.setFont(QFont('Segoe UI' if sys.platform=='win32' else 'DejaVu Sans',10))
module.apply_color_scheme(app,Qt.ColorScheme.Dark)
output=ROOT/'build/sidebar'
output.mkdir(parents=True,exist_ok=True)

def right_bar(area):
    for _ in range(3):
        app.processEvents()
    bar=area.verticalScrollBar()
    assert bar.isVisible(), f'Expected overflow: area={area.objectName()} visible={area.isVisible()} h={area.height()} range={bar.maximum()} page={controller.settings.current_page}'
    assert bar.mapTo(area,QPoint(0,0)).x() > area.width()/2, 'Scrollbar must be on the physical right'

with tempfile.TemporaryDirectory(dir=ROOT/'build') as folder:
    os.environ['XDG_CONFIG_HOME']=folder
    controller=module.Controller(app)
    controller.updater.state['daily_enabled']=False
    controller.settings.update_panel.check_on_show=False
    for code in ('en','fa','ar'):
        controller.preview_language(code)
        settings=controller.settings
        settings.resize(900,700)
        controller.show_settings()
        app.processEvents()
        settings.work.parts[2].setValue(17)
        page=settings.current_page
        worker=settings.update_panel
        settings.sidebar_toggle.click()
        QTest.qWait(260)
        assert settings.sidebar_collapsed and settings.sidebar.width()==56
        assert settings.current_page==page and settings.work.parts[2].value()==17
        assert settings.update_panel is worker
        assert settings.navigation.item(0).data(Qt.AccessibleTextRole)==i18n.tr('dashboard')
        settings.navigation.setCurrentRow(0)
        assert settings.current_page=='report'
        settings.grab().save(str(output/f'collapsed-{code}.png'))
        # A language preview rebuilds the panel while preserving view and draft state.
        other='fa' if code=='en' else 'en'
        controller.preview_language(other)
        settings=controller.settings
        assert settings.sidebar_collapsed and settings.work.parts[2].value()==17
        controller.preview_language(code)
        settings=controller.settings
        assert settings.sidebar_collapsed
        settings.sidebar_toggle.click()
        QTest.qWait(260)
        assert not settings.sidebar_collapsed and settings.sidebar.width()==180
        assert settings.work.parts[2].value()==17
        option=QStyleOptionViewItem()
        settings.navigation.itemDelegate().initStyleOption(option,settings.navigation.model().index(0,0))
        assert option.direction==(Qt.RightToLeft if code in ('fa','ar') else Qt.LeftToRight)
        assert option.displayAlignment & (Qt.AlignRight if code in ('fa','ar') else Qt.AlignLeft)
        settings.select_page('general')
        area=settings.pages.widget(0)
        assert not area.verticalScrollBar().isVisible() and not area.horizontalScrollBar().isVisible()
        assert area.widget().height()<=area.viewport().height()
        settings.select_page('updates')
        settings.update_panel.latest_notes.setPlainText('\n'.join(['English line — خط فارسی']*500))
        right_bar(settings.update_panel.latest_notes)
        settings.select_page('report')
        settings.report.rows.setRowCount(100)
        right_bar(settings.report.rows)
        settings.report.refresh()
        settings.grab().save(str(output/f'expanded-{code}.png'))
        settings.restore_saved_settings()
    controller.quit()
print('Collapse/expand, icon navigation, draft preservation, language direction and physical right scrollbars passed.')
