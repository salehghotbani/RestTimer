"""Dashboard data, date filters, chart modes, startup pause and actual PNG/PDF exports."""
import os
from pathlib import Path
import sys
import tempfile
from datetime import datetime
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT/'linux'))
from PySide6.QtCore import QDate, Qt
from PySide6.QtGui import QFont, QImage
from PySide6.QtWidgets import QApplication
import resttimer as module
import dashboard
import work_tracking
import i18n
app=QApplication([])
app.setFont(QFont('Segoe UI' if sys.platform=='win32' else 'DejaVu Sans', 10))
output=ROOT/'build/dashboard-linux'
output.mkdir(parents=True,exist_ok=True)
with tempfile.TemporaryDirectory(dir=ROOT/'build') as temporary:
    os.environ['XDG_CONFIG_HOME']=temporary
    controller=module.Controller(app)
    controller.settings.update_panel.check_on_show=False
    assert not controller.running and controller.start_timer_action.isEnabled() and not controller.stop_timer_action.isEnabled()
    controller.updater.state['daily_enabled']=False
    before=controller.deadline-controller.paused_at
    app.processEvents()
    assert controller.deadline-controller.paused_at==before
    day=datetime.now().replace(hour=8,minute=30,second=0,microsecond=0)
    controller.journal.save('writing','نوشتن گزارش',day.timestamp(),5400)
    controller.journal.save('coding','Development',day.replace(hour=11,minute=0).timestamp(),3600)
    controller.journal.save('reading','Reading',day.replace(hour=14,minute=0).timestamp(),1800)
    selected=day.strftime('%Y-%m-%d')
    raw=controller.journal.entries(selected)
    hourly=work_tracking.hourly(raw)
    assert hourly[8]==1800 and hourly[9]==3600 and hourly[11]==3600 and hourly[14]==1800
    assert sum(hourly)==10800
    legacy={'Day':selected,'Task':'Legacy','Seconds':600}
    assert sum(work_tracking.hourly(raw+[legacy]))==10800
    assert sum(seconds for _,seconds in work_tracking.totals(raw+[legacy]))==11400
    for code in ('en','fa'):
        controller.preview_language(code)
        report=controller.settings.report
        report.day.setDate(QDate(day.year,day.month,day.day))
        controller.settings.select_page('report')
        assert report.rows.rowCount()==3
        for mode in range(3):
            report.chart_type.setCurrentIndex(mode)
            report.export(output/f'{code}-{mode}.png',False)
            report.export(output/f'{code}-{mode}.pdf',True)
            image=QImage(str(output/f'{code}-{mode}.png'))
            assert not image.isNull() and image.width()==1400
            assert (output/f'{code}-{mode}.pdf').read_bytes().startswith(b'%PDF-')
            for scheme,name in ((Qt.ColorScheme.Dark,'dark'),(Qt.ColorScheme.Light,'light')):
                module.apply_color_scheme(app,scheme)
                controller.settings.show()
                app.processEvents()
                controller.settings.grab().save(str(output/f'ui-{code}-{mode}-{name}.png'))
        report.day.setDate(QDate(2000,1,1))
        assert report.rows.rowCount()==0
        report.export(output/f'empty-{code}.pdf',True)
        report.export(output/f'empty-{code}.png',False)
        report.day.setDate(QDate(day.year,day.month,day.day))
    report=controller.settings.report
    report.chart_type.setCurrentIndex(2)
    controller.preview_language('en')
    assert controller.settings.report.day.date()==QDate(day.year,day.month,day.day)
    assert controller.settings.report.chart_type.currentIndex()==2
    many=[{'Task':f'Task {i:02}', 'Day':selected, 'Seconds':60*(i+1)} for i in range(55)]
    many[0]['Task']='W'*120
    dashboard.export_report(output/'many.pdf',many,selected,1,True)
    dashboard.export_report(output/'legacy-line.pdf',raw+[legacy],selected,2,True)
    missing=output/'does-not-exist'/'report.pdf'
    try:
        dashboard.export_report(missing,raw,selected,0,True)
        raise AssertionError('Expected write failure')
    except OSError:
        pass
    assert not missing.exists()
    controller.quit()
print('Dashboard dates, exact hourly totals, legacy records, 3 chart modes, PNG/PDF, pagination, write failure and paused startup passed.')
