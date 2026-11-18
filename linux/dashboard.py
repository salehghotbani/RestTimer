"""Daily dashboard, native charts, and paginated PNG/PDF exports."""
import math
import os
from pathlib import Path
import uuid
from PySide6.QtCore import QDate, QMarginsF, QPointF, QRectF, Qt
from PySide6.QtGui import QColor, QFont, QImage, QPainter, QPen, QPdfWriter, QPageSize
from PySide6.QtWidgets import (QApplication, QComboBox, QDateEdit, QFileDialog, QHeaderView,
    QHBoxLayout, QLabel, QMessageBox, QPushButton, QTableWidget, QTableWidgetItem, QVBoxLayout, QWidget)
from i18n import tr, is_rtl
import work_tracking

COLORS = ['#189c81', '#4285e0', '#e3a03a', '#a06ede', '#da647b', '#319ebb', '#99a639', '#838e9f']


def text(painter, rect, value, color, size=11, bold=False, right=False, wrap=False):
    painter.setPen(QColor(color))
    font = QFont(QApplication.font())
    font.setPointSizeF(size)
    font.setBold(bold)
    painter.setFont(font)
    painter.drawText(rect, Qt.AlignVCenter | (Qt.AlignRight if right else Qt.AlignLeft) | (Qt.TextWrapAnywhere if wrap else Qt.TextSingleLine), value)


def draw_chart(painter, rect, entries, mode, printing=False):
    light = printing or '#f4f7fa' in QApplication.instance().styleSheet()
    ink, muted, surface, border = ('#1a2838', '#536778', '#ffffff', '#dce4ea') if light else ('#edf4fa', '#9bb0c3', '#141f2e', '#2c3e52')
    painter.fillRect(rect, QColor(surface))
    painter.save()
    scale = min(rect.width()/800, rect.height()/360)
    painter.translate(rect.left()+(rect.width()-800*scale)/2, rect.top()+(rect.height()-360*scale)/2)
    painter.scale(scale, scale)
    painter.setRenderHint(QPainter.Antialiasing)
    totals = work_tracking.totals(entries)
    text(painter, QRectF(20, 8, 760, 30), tr('hourly_work' if mode == 2 else 'task_distribution'), ink, right=is_rtl())
    if not totals or sum(seconds for _, seconds in totals) <= 0:
        text(painter, QRectF(30, 130, 730, 60), tr('no_work'), muted, right=is_rtl())
        painter.restore()
        return
    if mode == 2:
        hours = work_tracking.hourly(entries)
        maximum = max(1, max(hours)/60)
        left, top, width, height = 64, 60, 700, 238
        for i in range(5):
            y = top + height - height*i/4
            painter.setPen(QPen(QColor(border)))
            painter.drawLine(QPointF(left, y), QPointF(left+width, y))
            text(painter, QRectF(2, y-10, 56, 20), f'{maximum*i/4:.1f}', muted, 9)
        points = [QPointF(left+width*i/23, top+height-height*seconds/60/maximum) for i, seconds in enumerate(hours)]
        painter.setPen(QPen(QColor(COLORS[0]), 3))
        for first, second in zip(points, points[1:]):
            painter.drawLine(first, second)
        painter.setBrush(QColor(COLORS[0]))
        for point in points:
            painter.drawEllipse(point, 3, 3)
        for hour in (0, 4, 8, 12, 16, 20, 23):
            text(painter, QRectF(left+width*hour/23-30, 302, 78, 24), f'{hour:02}:00', muted, 9)
        text(painter, QRectF(4, 35, 150, 20), tr('chart_minutes'), muted, 9)
        if any(not entry.get('StartedAt') for entry in entries):
            text(painter, QRectF(20, 332, 760, 24), tr('legacy_hours'), muted, 9, right=is_rtl())
    else:
        shown = totals[:7]
        if len(totals) > 7:
            shown.append((tr('other_tasks'), sum(seconds for _, seconds in totals[7:])))
        total = sum(seconds for _, seconds in totals)
        if mode == 0:
            angle = 90 * 16
            painter.setPen(Qt.NoPen)
            for i, (_, seconds) in enumerate(shown):
                sweep = 360 * 16 * seconds / total
                painter.setBrush(QColor(COLORS[i]))
                painter.drawPie(QRectF(35, 65, 250, 250), int(angle), -int(sweep))
                angle -= sweep
            painter.setBrush(QColor(surface))
            painter.drawEllipse(QRectF(98, 128, 124, 124))
            for i, (task, seconds) in enumerate(shown):
                y = 54+i*35
                painter.setPen(Qt.NoPen)
                painter.setBrush(QColor(COLORS[i]))
                painter.drawEllipse(QRectF(325, y+9, 12, 12))
                text(painter, QRectF(350, y, 260, 32), task, ink, right=is_rtl())
                text(painter, QRectF(620, y, 160, 32), f'{100*seconds/total:.1f}% · {work_tracking.format_time(seconds)}', muted, 9)
        else:
            maximum = max(seconds for _, seconds in shown)
            for i, (task, seconds) in enumerate(shown):
                y = 48+i*35
                text(painter, QRectF(16, y, 210, 30), task, ink, right=is_rtl())
                painter.fillRect(QRectF(240, y+7, 390*seconds/maximum, 18), QColor(COLORS[i]))
                text(painter, QRectF(645, y, 145, 30), work_tracking.format_time(seconds), muted, 9)
    painter.restore()


def report_image(entries, day, mode, first=0, count=None, page=1, pages=1):
    totals = work_tracking.totals(entries)
    count = len(totals) if count is None else count
    height = 650 + count*88
    image = QImage(1400, height, QImage.Format_RGB32)
    image.fill(Qt.white)
    painter = QPainter(image)
    try:
        text(painter, QRectF(48, 18, 1304, 64), 'RestTimer · '+tr('dashboard'), '#1a2838', 23, True, is_rtl())
        text(painter, QRectF(48, 85, 1304, 44), day+'   ·   '+tr('total_time')+' '+work_tracking.format_time(sum(seconds for _, seconds in totals)), '#536778', 14, right=is_rtl())
        draw_chart(painter, QRectF(48, 145, 1304, 400), entries, mode, True)
        text(painter, QRectF(380 if is_rtl() else 60, 555, 950, 44), tr('task_name'), '#1a2838', 14, right=is_rtl())
        text(painter, QRectF(60 if is_rtl() else 1050, 555, 300, 44), tr('time_spent'), '#1a2838', 14)
        for i, (task, seconds) in enumerate(totals[first:first+count]):
            y = 604+i*88
            if i % 2 == 0:
                painter.fillRect(QRectF(48, y, 1304, 88), QColor('#f4f7fa'))
            text(painter, QRectF(380 if is_rtl() else 60, y, 950, 88), task, '#1a2838', 16, right=is_rtl(), wrap=True)
            text(painter, QRectF(60 if is_rtl() else 1050, y, 300, 88), work_tracking.format_time(seconds), '#1a2838', 14)
        text(painter, QRectF(48, height-36, 1304, 30), f'{page} / {pages}', '#536778', 12)
    finally:
        painter.end()
    return image


def export_report(path, entries, day, mode, as_pdf):
    path = Path(path)
    temporary = path.with_name(path.name + '.' + uuid.uuid4().hex + '.tmp')
    try:
        if as_pdf:
            writer = QPdfWriter(str(temporary))
            writer.setResolution(150)
            writer.setPageSize(QPageSize(QPageSize.A4))
            writer.setPageMargins(QMarginsF(10, 10, 10, 10))
            writer.setTitle('RestTimer - '+day)
            painter = QPainter(writer)
            if not painter.isActive():
                raise OSError('Cannot open PDF output')
            try:
                count = len(work_tracking.totals(entries))
                pages = max(1, math.ceil(count/14))
                for page in range(pages):
                    if page and not writer.newPage():
                        raise OSError('Cannot create PDF page')
                    image = report_image(entries, day, mode, page*14, min(14, count-page*14), page+1, pages)
                    scale = min(writer.width()/image.width(), writer.height()/image.height())
                    painter.drawImage(QRectF(0, 0, image.width()*scale, image.height()*scale), image)
            finally:
                painter.end()
                del writer
        elif not report_image(entries, day, mode).save(str(temporary), 'PNG'):
            raise OSError('Cannot write PNG output')
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


class Chart(QWidget):
    def __init__(self, report):
        super().__init__()
        self.report = report
        self.setMinimumHeight(220)
        self.setAccessibleName(tr('chart_type'))

    def paintEvent(self, event):
        painter = QPainter(self)
        draw_chart(painter, QRectF(self.rect()), self.report.entries, self.report.chart_type.currentIndex())
        painter.end()


class WorkReport(QWidget):
    def __init__(self, controller):
        super().__init__()
        self.controller = controller
        self.entries = []
        layout = QVBoxLayout(self)
        layout.setContentsMargins(20, 20, 20, 20)
        layout.setSpacing(12)
        heading = QLabel(tr('dashboard'))
        heading.setObjectName('title')
        layout.addWidget(heading)
        self.day = QDateEdit(QDate.currentDate())
        self.day.setCalendarPopup(True)
        self.day.setAccessibleName(tr('report_date'))
        layout.addWidget(self.day)
        actions = QHBoxLayout()
        self.chart_type = QComboBox()
        self.chart_type.addItems([tr('pie_chart'), tr('bar_chart'), tr('line_chart')])
        self.chart_type.setAccessibleName(tr('chart_type'))
        actions.addWidget(self.chart_type, 1)
        self.png = QPushButton('PNG')
        self.pdf = QPushButton('PDF')
        self.png.setAccessibleName(tr('export_png'))
        self.pdf.setAccessibleName(tr('export_pdf'))
        actions.addWidget(self.png)
        actions.addWidget(self.pdf)
        layout.addLayout(actions)
        self.total = QLabel()
        self.total.setObjectName('muted')
        layout.addWidget(self.total)
        self.chart = Chart(self)
        layout.addWidget(self.chart, 3)
        self.rows = QTableWidget(0, 2)
        self.rows.setHorizontalHeaderLabels([tr('task_name'), tr('time_spent')])
        self.rows.horizontalHeader().setSectionResizeMode(0, QHeaderView.Stretch)
        self.rows.horizontalHeader().setSectionResizeMode(1, QHeaderView.ResizeToContents)
        self.rows.verticalHeader().hide()
        self.rows.setEditTriggers(QTableWidget.NoEditTriggers)
        layout.addWidget(self.rows, 2)
        self.day.dateChanged.connect(self.refresh)
        self.chart_type.currentIndexChanged.connect(self.chart.update)
        self.png.clicked.connect(lambda: self.choose_export(False))
        self.pdf.clicked.connect(lambda: self.choose_export(True))

    def refresh(self):
        self.rows.setRowCount(0)
        try:
            self.entries = self.controller.journal.entries(self.day.date().toString('yyyy-MM-dd'))
            entries = work_tracking.totals(self.entries)
            for task, seconds in entries:
                row = self.rows.rowCount()
                self.rows.insertRow(row)
                self.rows.setItem(row, 0, QTableWidgetItem(task))
                self.rows.setItem(row, 1, QTableWidgetItem(work_tracking.format_time(seconds)))
            self.total.setText(tr('total_time')+'  '+work_tracking.format_time(sum(seconds for _, seconds in entries))+'    ·    '+tr('task_count')+' '+str(len(entries)) if entries else tr('no_work'))
            self.png.setEnabled(True)
            self.pdf.setEnabled(True)
        except (OSError, ValueError, KeyError):
            self.entries = []
            self.total.setText(tr('log_error'))
            self.png.setEnabled(False)
            self.pdf.setEnabled(False)
        self.chart.update()

    def export(self, path, as_pdf):
        export_report(path, self.entries, self.day.date().toString('yyyy-MM-dd'), self.chart_type.currentIndex(), as_pdf)

    def choose_export(self, as_pdf):
        extension = 'pdf' if as_pdf else 'png'
        name = 'RestTimer-'+self.day.date().toString('yyyy-MM-dd')+'.'+extension
        path, _ = QFileDialog.getSaveFileName(self, tr('export_pdf' if as_pdf else 'export_png'), name, extension.upper()+' (*.'+extension+')')
        if not path:
            return
        if not Path(path).suffix:
            path += '.'+extension
        try:
            self.export(path, as_pdf)
            self.total.setText(tr('export_saved'))
        except (OSError, ValueError):
            QMessageBox.warning(self, 'RestTimer', tr('export_error'))
