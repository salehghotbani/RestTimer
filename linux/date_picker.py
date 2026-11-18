"""Calendar-aware selector; its public date remains a Gregorian QDate."""
from PySide6.QtCore import QCalendar, QDate, QLocale, Qt, Signal
from PySide6.QtGui import QColor, QTextCharFormat
from PySide6.QtWidgets import QApplication, QCalendarWidget, QFrame, QPushButton, QToolButton, QVBoxLayout
from i18n import tr, is_rtl
import i18n
import ui_icons


def calendar(preference):
    kind = preference if preference != 'auto' else 'persian' if i18n.code == 'fa' else 'hijri' if i18n.code == 'ar' else 'gregorian'
    return QCalendar({'persian': QCalendar.System.Jalali, 'hijri': QCalendar.System.IslamicCivil}.get(kind, QCalendar.System.Gregorian))


class DatePicker(QPushButton):
    dateChanged = Signal(QDate)

    def __init__(self, preference='auto'):
        super().__init__()
        self.preference = preference
        self._date = QDate.currentDate()
        self.popup = None
        self.setAccessibleName(tr('report_date'))
        self.clicked.connect(self.open_calendar)
        self.refresh_caption()

    def date(self):
        return self._date

    def setDate(self, date):
        changed = self._date != date
        self._date = date
        self.refresh_caption()
        if changed:
            self.dateChanged.emit(date)

    def refresh_caption(self):
        self.setText(QLocale(i18n.code).toString(self._date, QLocale.LongFormat, calendar(self.preference)))

    def open_calendar(self):
        if self.popup:
            self.popup.deleteLater()
        self.popup = QFrame(self, Qt.Popup)
        self.popup.setObjectName('datePopup')
        layout = QVBoxLayout(self.popup)
        layout.setContentsMargins(12, 12, 12, 12)
        picker = QCalendarWidget()
        picker.setCalendar(calendar(self.preference))
        picker.setLocale(QLocale(i18n.code))
        picker.setFirstDayOfWeek(Qt.Saturday if is_rtl() else QLocale(i18n.code).firstDayOfWeek())
        picker.setSelectedDate(self._date)
        picker.setGridVisible(False)
        picker.setMinimumSize(310, 290)
        picker.setVerticalHeaderFormat(QCalendarWidget.NoVerticalHeader)
        light = '#f4f7fa' in QApplication.instance().styleSheet()
        header = QTextCharFormat()
        header.setForeground(QColor('#536778' if light else '#9bb0c3'))
        header.setBackground(QColor('#ffffff' if light else '#141f2e'))
        picker.setHeaderTextFormat(header)
        for weekday in (Qt.Saturday, Qt.Sunday):
            picker.setWeekdayTextFormat(weekday, header)
        for name, glyph in (('qt_calendar_prevmonth','previous'),('qt_calendar_nextmonth','next')):
            button = picker.findChild(QToolButton, name)
            if button:
                button.setIcon(ui_icons.icon(glyph, ui_icons.color_for()))
                button.setAccessibleName(tr('previous_month' if glyph=='previous' else 'next_month'))
        picker.clicked.connect(self.choose)
        picker.activated.connect(self.choose)
        layout.addWidget(picker)
        today = QPushButton(tr('today'))
        today.clicked.connect(lambda: self.choose(QDate.currentDate()))
        layout.addWidget(today)
        self.popup.adjustSize()
        point = self.mapToGlobal(self.rect().bottomLeft())
        screen = self.screen().availableGeometry()
        point.setX(max(screen.left(), min(point.x(), screen.right()-self.popup.width())))
        point.setY(max(screen.top(), min(point.y()+5, screen.bottom()-self.popup.height())))
        self.popup.move(point)
        self.popup.show()

    def choose(self, date):
        self.setDate(date)
        self.popup.close()
