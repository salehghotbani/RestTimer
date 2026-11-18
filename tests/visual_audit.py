"""Check every page/language/theme at narrow and normal sizes and during sidebar motion."""
import os
from pathlib import Path
import sys
import tempfile
import time
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'linux'))
from PySide6.QtCore import QPoint, QSize, Qt
from PySide6.QtGui import QFont, QFontDatabase, QImage, QPainter, QColor
from PySide6.QtTest import QTest
from PySide6.QtWidgets import QApplication, QLabel, QScrollArea, QAbstractScrollArea, QCheckBox
import resttimer as module
import i18n

app=QApplication([])
if sys.platform=='win32':
    # The offscreen Qt plugin has no Windows font enumeration; register real fonts
    # so this audit measures shaped text rather than missing-glyph squares.
    for name in ('segoeui.ttf','segoeuib.ttf','seguisb.ttf','msyh.ttc','msgothic.ttc','YuGothM.ttc','malgun.ttf','Nirmala.ttc'):
        path=Path(os.environ['WINDIR'])/'Fonts'/name
        if path.exists(): QFontDatabase.addApplicationFont(str(path))
app.setFont(QFont('Segoe UI' if sys.platform=='win32' else 'DejaVu Sans',10))
module.verify_language_fonts(app)
output=ROOT/'build/visual-audit'
output.mkdir(parents=True,exist_ok=True)
issues=[]

def labels_fit(root,sample):
    for check in root.findChildren(QCheckBox):
        if check.isVisible() and check.heightForWidth(check.width())>check.height()+2:
            issues.append(sample+' | checkbox text clipped: '+check.text())
    for label in root.findChildren(QLabel):
        if not label.isVisible() or not label.text() or label.pixmap() and not label.pixmap().isNull(): continue
        if label.wordWrap():
            needed=label.heightForWidth(label.width())
            if needed>label.height()+2: issues.append(sample+' | wrapped label clipped: '+label.text())
        elif label.fontMetrics().horizontalAdvance(label.text())>label.width()+4:
            issues.append(sample+' | label width: '+label.text())

def inspect(settings,sample):
    # QScrollArea may legitimately scroll About/license text, but the General page must fit.
    if settings.current_page=='general':
        area=settings.pages.widget(0)
        if area.widget().height()>area.viewport().height() or area.widget().width()>area.viewport().width():
            issues.append(sample+f' | general page overflow body={area.widget().size()} viewport={area.viewport().size()} minimum={area.widget().minimumSizeHint()}')
        for group in settings.general_sections.values():
            if not group.parentWidget().rect().contains(group.geometry()): issues.append(sample+' | settings group clipped')
    labels_fit(settings.pages.currentWidget(),sample)
    for area in settings.pages.currentWidget().findChildren(QAbstractScrollArea):
        bar=area.horizontalScrollBar()
        if bar.isVisible() and bar.maximum()>0: issues.append(sample+' | horizontal scrollbar: '+area.metaObject().className())

with tempfile.TemporaryDirectory(dir=ROOT/'build') as folder:
    os.environ['XDG_CONFIG_HOME']=folder
    controller=module.Controller(app)
    controller.updater.state['daily_enabled']=False
    controller.settings.update_panel.check_on_show=False
    codes=sys.argv[1:] or i18n.SUPPORTED
    for code in codes:
        controller.preview_language(code)
        settings=controller.settings
        controller.show_settings()
        for mode,scheme in (('light',Qt.ColorScheme.Light),('dark',Qt.ColorScheme.Dark)):
            app.setProperty('resttimer_color_mode','system')
            module.apply_color_scheme(app,scheme)
            wrapped=module.QCheckBox(i18n.tr('daily_check'))
            wrapped.setFixedWidth(220);wrapped.resize(220,wrapped.heightForWidth(220));wrapped.show();app.processEvents()
            QTest.mouseClick(wrapped,Qt.LeftButton,pos=QPoint(100,wrapped.height()-3))
            assert wrapped.isChecked(), 'Wrapped checkbox must respond over its full text'
            QTest.keyClick(wrapped,Qt.Key_Space)
            assert not wrapped.isChecked(), 'Checkbox keyboard behavior changed'
            wrapped.hide();wrapped.deleteLater()
            sheet=QImage(1500,1040,QImage.Format_RGB32);sheet.fill(QColor('gray'));canvas=QPainter(sheet)
            row=0
            for size in ((1020,800),(780,660)):
                settings.resize(*size)
                for collapsed in (False,True):
                    settings.set_sidebar_collapsed(collapsed)
                    for col,page in enumerate(settings.NAV_PAGES):
                        settings.select_page(page);app.processEvents()
                        sample=f'{code} {mode} {size} {collapsed} {page}'
                        inspect(settings,sample)
                        frame=settings.grab().scaled(300,240,Qt.IgnoreAspectRatio,Qt.SmoothTransformation)
                        canvas.drawPixmap(col*300,row*260,frame)
                        canvas.setPen(QColor('white'));canvas.drawText(col*300,row*260+255,page+' '+str(size[0])+(' rail' if collapsed else ' open'))
                    row+=1
            canvas.end();sheet.save(str(output/f'{code}-{mode}.png'))
            settings.set_sidebar_collapsed(False);app.processEvents()
            initial_y=settings.navigation.mapToGlobal(QPoint(0,0)).y()
            settings.sidebar_toggle.click()
            widths=[]
            for _ in range(12):
                QTest.qWait(20)
                widths.append(settings.sidebar.width())
                if settings.navigation.mapToGlobal(QPoint(0,0)).y()!=initial_y: issues.append(f'{code} {mode} | sidebar navigation jumps')
            if any(b>a for a,b in zip(widths,widths[1:])): issues.append(f'{code} {mode} | sidebar collapse reverses unexpectedly')
            # Interrupt and reverse an unfinished animation, then complete expansion.
            settings.sidebar_toggle.click();QTest.qWait(45);settings.sidebar_toggle.click();QTest.qWait(45);settings.sidebar_toggle.click();QTest.qWait(240)
            if settings.sidebar_collapsed or settings.navigation.item(0).text()!=i18n.tr('dashboard'): issues.append(f'{code} {mode} | interrupted expansion lost labels')
            for index in range(5):
                settings.navigation.setCurrentRow(index);app.processEvents()
                if settings.current_page!=settings.NAV_PAGES[index]: issues.append(f'{code} {mode} | navigation failed')
            sheet=QImage(1500,420,QImage.Format_RGB32);sheet.fill(QColor('gray'));canvas=QPainter(sheet)
            for index,prompt in enumerate((module.UnsavedChangesDialog(settings),module.WorkStartDialog(controller))):
                prompt.show();app.processEvents();labels_fit(prompt,f'{code} {mode} popup {index}')
                canvas.drawPixmap(index*500,0,prompt.grab().scaled(490,400,Qt.IgnoreAspectRatio,Qt.SmoothTransformation))
                prompt.hide();prompt.deleteLater()
            overlay=module.BreakOverlay(app.primaryScreen(),lambda:None,lambda:None,lambda:None)
            overlay.countdown.setText('14:52');overlay.set_running(False);overlay.show();app.processEvents()
            labels_fit(overlay.card,f'{code} {mode} break')
            canvas.drawPixmap(1000,0,overlay.card.grab().scaled(500,150,Qt.IgnoreAspectRatio,Qt.SmoothTransformation))
            overlay.hide();overlay.deleteLater();canvas.end();sheet.save(str(output/f'popups-{code}-{mode}.png'))
        print('Audited',code)
    controller.quit()
(output/'issues.txt').write_text('\n'.join(issues),encoding='utf-8')
print('Visual audit findings:',len(issues))
if issues: raise SystemExit(2)
