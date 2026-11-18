"""Calendar selection, date identity, theme preview/discard and popup appearance."""
import os
from pathlib import Path
import sys
import tempfile
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'linux'))
from PySide6.QtCore import QDate, QLocale, Qt
from PySide6.QtGui import QFont
from PySide6.QtWidgets import QApplication, QCalendarWidget
import resttimer as module
import i18n
from date_picker import calendar

app=QApplication([])
app.setFont(QFont('Segoe UI' if sys.platform=='win32' else 'DejaVu Sans',10))
output=ROOT/'build/appearance'
output.mkdir(parents=True,exist_ok=True)
with tempfile.TemporaryDirectory(dir=ROOT/'build') as folder:
    os.environ['XDG_CONFIG_HOME']=folder
    controller=module.Controller(app)
    controller.updater.state['daily_enabled']=False
    controller.settings.update_panel.check_on_show=False
    for code in ('en','fa','ar'):
        controller.preview_language(code)
        settings=controller.settings
        controller.show_settings()
        settings.resize(900,700)
        settings.select_page('general')
        groups=settings.general_sections
        assert set(groups)=={'time_settings','language_region','appearance','app_behavior'}
        for key,controls in (
            ('time_settings',(settings.work,settings.rest,settings.start_timer,settings.stop_timer,settings.reset_timer)),
            ('language_region',(settings.language,settings.calendar_choice)),
            ('appearance',(settings.theme_choice,)),
            ('app_behavior',(settings.startup,settings.daily))):
            for control in controls:
                assert groups[key].isAncestorOf(control), f'{key} does not contain its settings'
        app.processEvents()
        for group in groups.values():
            assert group.parentWidget().rect().contains(group.geometry()), 'Settings section is clipped'
        settings.report.day.setDate(QDate(2024,3,20))
        for kind in ('gregorian','persian','hijri'):
            settings.calendar_choice.setCurrentIndex(settings.calendar_choice.findData(kind))
            picker=settings.report.day
            assert picker.date()==QDate(2024,3,20), 'Changing a calendar changed the report date'
            parts=calendar(kind).partsFromDate(picker.date())
            assert parts.year==({'gregorian':2024,'persian':1403,'hijri':1445}[kind])
            if kind=='persian': assert (parts.month,parts.day)==(1,1)
            settings.select_page('report')
            picker.open_calendar()
            app.processEvents()
            widget=picker.popup.findChild(QCalendarWidget)
            assert widget.calendar().name()==calendar(kind).name()
            assert widget.selectedDate()==picker.date()
            picker.popup.grab().save(str(output/f'calendar-{code}-{kind}.png'))
            picker.choose(QDate(2024,3,21))
            assert picker.date()==QDate(2024,3,21)
            picker.setDate(QDate(2024,3,20))
        settings.select_page('general')
        for mode in ('light','dark','system'):
            settings.theme_choice.setCurrentIndex(settings.theme_choice.findData(mode))
            app.processEvents()
            if mode!='system': assert ('#f4f7fa' in app.styleSheet())==(mode=='light')
            settings.grab().save(str(output/f'general-{code}-{mode}.png'))
        assert settings.has_pending_changes()
        settings.restore_saved_settings()
        assert not settings.has_pending_changes()
    settings.calendar_choice.setCurrentIndex(settings.calendar_choice.findData('persian'))
    settings.theme_choice.setCurrentIndex(settings.theme_choice.findData('light'))
    assert settings.save_durations()
    saved=module.read_preferences()
    assert saved['calendar']=='persian' and saved['color_mode']=='light'
    assert not controller.running, 'Saving appearance started the timer'
    settings.calendar_choice.setCurrentIndex(settings.calendar_choice.findData('hijri'))
    settings.theme_choice.setCurrentIndex(settings.theme_choice.findData('dark'))
    original_exec=module.UnsavedChangesDialog.exec
    module.UnsavedChangesDialog.exec=lambda self: 2
    try:
        assert settings.confirm_pending_changes()
    finally:
        module.UnsavedChangesDialog.exec=original_exec
    assert app.property('resttimer_color_mode')=='light'
    assert settings.report.day.preference=='persian'
    controller.quit()
print('Three calendar conversions preserve report dates; calendar popups and theme preview/discard passed.')
