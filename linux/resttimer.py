"""Linux desktop frontend for RestTimer. Run from source with Python and PySide6."""
import json
import math
import os
from pathlib import Path
import re
import sys
import time
import subprocess

from PySide6.QtCore import QIODevice, QLocale, QSaveFile, QStandardPaths, Qt, QTimer
from PySide6.QtGui import QAction, QColor, QCursor, QFont, QIcon, QPainter
from PySide6.QtNetwork import QLocalServer, QLocalSocket
from PySide6.QtWidgets import (
    QAbstractSpinBox, QApplication, QCheckBox, QDialog, QFrame, QHBoxLayout, QLabel,
    QMenu, QMessageBox, QPushButton, QSpinBox, QSystemTrayIcon, QVBoxLayout, QWidget,
)
import update_core
from update_ui import UpdateDialog, Worker

ROOT = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parents[1]))
VERSION = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
DEFAULTS = {"work_seconds": 3600, "break_seconds": 900}
STYLE = """
QDialog, QMenu { background: #0d1521; color: #edf4fa; }
QTextEdit, QComboBox, QTabWidget::pane { background: #141f2e; color: #edf4fa; border: 1px solid #2c3e52; border-radius: 6px; padding: 8px; }
QTabBar::tab { background: #1e2e40; color: #edf4fa; padding: 10px 20px; }
QTabBar::tab:selected { background: #223a48; color: #83e6c4; }
QProgressBar { background: #141f2e; color: #edf4fa; border: 1px solid #2c3e52; border-radius: 5px; text-align: center; }
QProgressBar::chunk { background: #16836b; border-radius: 5px; }
QLabel { color: #edf4fa; background: transparent; }
QLabel#muted { color: #9bb0c3; }
QLabel#title { font-size: 23px; font-weight: bold; }
QLabel#timer { font-size: 38px; font-weight: bold; color: #83e6c4; }
QFrame#section, QFrame#breakCard {
    background: #141f2e; border: 1px solid #2c3e52; border-radius: 16px;
}
QPushButton {
    color: #edf4fa; background: #1e2e40; border: 1px solid #2c3e52;
    border-radius: 9px; padding: 9px 14px; min-height: 18px;
}
QPushButton:hover { background: #223a48; border-color: #83e6c4; }
QPushButton:focus { border-color: #83e6c4; }
QPushButton#primary { background: #83e6c4; color: #0d1521; }
QPushButton#primary:hover { background: #a4f2d8; }
QPushButton:disabled { color: #9bb0c3; }
QPushButton#primary:disabled { background: #0c1623; color: #9bb0c3; border-color: #2c3e52; }
QSpinBox {
    background: #0c1623; color: #edf4fa; font-size: 26px;
}
QSpinBox:focus { background: #102c30; color: #83e6c4; }
QFrame#timeField {
    background: #0c1623; border: 1px solid #2c3e52; border-radius: 10px;
}
QCheckBox { color: #9bb0c3; spacing: 10px; }
QMenu { border: 1px solid #2c3e52; padding: 7px; }
QMenu::item { padding: 11px 30px; border-radius: 7px; }
QMenu::item:selected { background: #223a48; color: #83e6c4; }
QMenu::item:disabled { color: #9bb0c3; }
QMenu::separator { height: 1px; background: #2c3e52; margin: 5px 12px; }
"""


def apply_color_scheme(app, scheme):
    style = STYLE
    if scheme == Qt.ColorScheme.Light:
        colors = {"#0d1521": "#f4f7fa", "#edf4fa": "#1a2838", "#141f2e": "#ffffff",
                  "#2c3e52": "#cdd8e2", "#1e2e40": "#e7edf3", "#223a48": "#dbeee8",
                  "#83e6c4": "#11705b", "#a4f2d8": "#16836b", "#9bb0c3": "#536778", "#0c1623": "#eaf0f5", "#102c30": "#dbeee8"}
        style = re.sub(r"#[a-fA-F0-9]{6}", lambda match: colors.get(match.group(), match.group()), style)
        style += "\nQPushButton#primary { color: white; } QProgressBar { color: #11705b; } QProgressBar::chunk { background: #dbeee8; }"
    app.setStyleSheet(style)


def config_root():
    return Path(os.environ.get("XDG_CONFIG_HOME", Path.home() / ".config"))


def read_preferences():
    try:
        data = json.loads((config_root() / "resttimer/settings.json").read_text(encoding="utf-8"))
        if (isinstance(data, dict) and type(data.get("work_seconds")) is int
                and type(data.get("break_seconds")) is int
                and 1 <= data["work_seconds"] <= 86400 and 1 <= data["break_seconds"] <= 7200):
            return {key: data[key] for key in DEFAULTS}
    except (OSError, ValueError):
        pass
    return DEFAULTS.copy()


def atomic_write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    output = QSaveFile(str(path))
    if not output.open(QIODevice.WriteOnly):
        raise OSError(output.errorString())
    encoded = text.encode("utf-8")
    if output.write(encoded) != len(encoded):
        output.cancelWriting()
        raise OSError(output.errorString())
    if not output.commit():
        raise OSError(output.errorString())


def startup_path():
    return config_root() / "autostart/resttimer.desktop"


def desktop_quote(value):
    value = str(value).replace("%", "%%").replace("\\", "\\\\").replace('"', '\\"')
    return '"' + value.replace("$", "\\$").replace("`", "\\`") + '"'


def set_startup(enabled):
    path = startup_path()
    if not enabled:
        path.unlink(missing_ok=True)
        return
    arguments = [sys.executable]
    if not getattr(sys, "frozen", False):
        arguments.append(str(Path(__file__).resolve()))
    arguments.append("--background")
    command = " ".join(desktop_quote(part) for part in arguments)
    atomic_write(path, "[Desktop Entry]\nType=Application\nName=RestTimer\n"
                 f"Exec={command}\nTryExec={sys.executable}\n"
                 "Icon=resttimer\nTerminal=false\nX-GNOME-Autostart-enabled=true\n")


def version_tuple(tag):
    if not isinstance(tag, str) or not re.fullmatch(r"[vV]?\d+\.\d+(?:\.\d+){0,2}", tag):
        raise ValueError("Unsupported release version")
    parts = tuple(int(part) for part in tag.lstrip("vV").split("."))
    return parts + (0,) * (4 - len(parts))


def label(text, name="", center=False):
    widget = QLabel(text)
    widget.setObjectName(name)
    widget.setAlignment(Qt.AlignCenter if center else Qt.AlignAbsolute | Qt.AlignRight | Qt.AlignVCenter)
    return widget


class DurationEditor(QFrame):
    def __init__(self, title, seconds, max_hours):
        super().__init__()
        self.setObjectName("section")
        layout = QVBoxLayout(self)
        layout.setContentsMargins(20, 14, 20, 14)
        heading = label(title)
        heading.setFont(QFont("DejaVu Sans", 11, QFont.Bold))
        layout.addWidget(heading)
        fields = QHBoxLayout()
        fields.setDirection(QHBoxLayout.LeftToRight)
        fields.setSpacing(14)
        self.parts = []
        for caption, value, maximum in (
            ("ساعت", seconds // 3600, max_hours),
            ("دقیقه", seconds // 60 % 60, 59),
            ("ثانیه", seconds % 60, 59),
        ):
            column = QVBoxLayout()
            spin = QSpinBox()
            spin.setAccessibleName(title + "، " + caption)
            spin.setRange(0, maximum)
            spin.setValue(value)
            spin.setFrame(False)
            spin.setMinimumSize(70, 34)
            spin.setAlignment(Qt.AlignCenter)
            spin.setLayoutDirection(Qt.LeftToRight)
            spin.setButtonSymbols(QAbstractSpinBox.NoButtons)
            field = QFrame()
            field.setObjectName("timeField")
            field_layout = QVBoxLayout(field)
            field_layout.setContentsMargins(10, 7, 10, 7)
            field_layout.addWidget(spin)
            column.addWidget(field)
            column.addWidget(label(caption, "muted", center=True))
            fields.addLayout(column)
            self.parts.append(spin)
        layout.addLayout(fields)

    def seconds(self):
        for part in self.parts:
            part.interpretText()
        h, m, s = (part.value() for part in self.parts)
        return h * 3600 + m * 60 + s


class SettingsDialog(QDialog):
    def __init__(self, controller):
        super().__init__()
        self.controller = controller
        self.setWindowTitle("RestTimer — زمان‌بندی استراحت")
        self.setWindowIcon(controller.icon)
        self.setMinimumWidth(520)
        layout = QVBoxLayout(self)
        layout.setContentsMargins(26, 24, 26, 24)
        layout.setSpacing(16)
        layout.addWidget(label("زمان‌بندی استراحت", "title"))
        layout.addWidget(label("زمان هر دوره را با ساعت، دقیقه و ثانیه تنظیم کن.", "muted"))
        self.work = DurationEditor("زمان کار", controller.preferences["work_seconds"], 24)
        self.rest = DurationEditor("زمان استراحت", controller.preferences["break_seconds"], 2)
        layout.addWidget(self.work)
        layout.addWidget(self.rest)
        self.startup = QCheckBox("اجرا با ورود به سیستم")
        self.startup.setChecked(startup_path().exists())
        self.startup.clicked.connect(controller.toggle_startup)
        layout.addWidget(self.startup)
        timer_controls = QHBoxLayout()
        self.start_timer = QPushButton("شروع")
        self.start_timer.setObjectName("primary")
        self.start_timer.clicked.connect(controller.start_timer)
        self.stop_timer = QPushButton("توقف")
        self.stop_timer.clicked.connect(controller.stop_timer)
        self.timer_status = label("", "muted")
        self.timer_status.setWordWrap(True)
        timer_controls.addWidget(self.start_timer)
        timer_controls.addWidget(self.stop_timer)
        timer_controls.addWidget(self.timer_status, 1)
        layout.addLayout(timer_controls)
        self.note = label("با تغییر زمان‌ها، شمارش کار از نو شروع می‌شود.", "muted")
        self.note.setWordWrap(True)
        layout.addWidget(self.note)
        self.update_notice = QPushButton()
        self.update_notice.setVisible(False)
        self.update_notice.clicked.connect(controller.check_updates)
        layout.addWidget(self.update_notice)
        buttons = QHBoxLayout()
        self.save = QPushButton("ذخیرهٔ زمان‌ها")
        self.save.setObjectName("primary")
        self.save.clicked.connect(self.save_durations)
        buttons.addWidget(self.save)
        self.updates = QPushButton("به‌روزرسانی و تغییرات")
        self.updates.clicked.connect(controller.check_updates)
        buttons.addWidget(self.updates)
        start = QPushButton("استراحت")
        start.clicked.connect(controller.begin_break)
        buttons.addWidget(start)
        layout.addLayout(buttons)
        exit_button = QPushButton("خروج از برنامه")
        exit_button.clicked.connect(controller.quit)
        layout.addWidget(exit_button)

    def save_durations(self):
        work, rest = self.work.seconds(), self.rest.seconds()
        if not 1 <= work <= 86400 or not 1 <= rest <= 7200:
            self.note.setText("زمان کار باید بین ۱ ثانیه و ۲۴ ساعت و استراحت بین ۱ ثانیه و ۲ ساعت باشد.")
            return
        if self.controller.breaking:
            self.note.setText("پس از پایان استراحت، زمان‌ها را ذخیره کن.")
            return
        updated = {"work_seconds": work, "break_seconds": rest}
        try:
            atomic_write(config_root() / "resttimer/settings.json", json.dumps(updated))
        except OSError:
            self.note.setText("ذخیرهٔ تنظیمات ممکن نشد. دسترسی به پوشهٔ تنظیمات را بررسی کن.")
            return
        if updated != self.controller.preferences:
            self.controller.preferences = updated
            self.controller.begin_work()
        self.note.setText("زمان‌ها ذخیره شدند.")

    def closeEvent(self, event):
        if not self.controller.tray.isSystemTrayAvailable():
            self.controller.quit()
        event.accept()


class BreakOverlay(QWidget):
    def __init__(self, screen, finish, start, stop):
        super().__init__(None, Qt.FramelessWindowHint | Qt.WindowStaysOnTopHint | Qt.Tool)
        self.finish = finish
        self.setWindowTitle("RestTimer")
        self.setAttribute(Qt.WA_TranslucentBackground)
        # Screen capture may be unavailable on Wayland. The translucent dimmer
        # still works without requesting access to desktop screenshots.
        self.background = screen.grabWindow(0)
        if not self.background.isNull():
            self.background = self.background.scaledToWidth(240, Qt.SmoothTransformation)
        self.setGeometry(screen.geometry())
        self.winId()
        self.windowHandle().setScreen(screen)
        self.card = QFrame(self)
        self.card.setObjectName("breakCard")
        self.card.setLayoutDirection(Qt.LeftToRight)
        row = QHBoxLayout(self.card)
        row.setContentsMargins(20, 14, 20, 14)
        row.setSpacing(20)
        controls = QWidget()
        controls.setFixedWidth(172)
        controls_column = QVBoxLayout(controls)
        controls_column.setContentsMargins(0, 0, 0, 0)
        controls_column.setSpacing(6)
        timer_controls = QHBoxLayout()
        timer_controls.setSpacing(8)
        self.start = QPushButton("شروع")
        self.start.setObjectName("primary")
        self.start.clicked.connect(start)
        self.stop = QPushButton("توقف")
        self.stop.clicked.connect(stop)
        timer_controls.addWidget(self.start)
        timer_controls.addWidget(self.stop)
        controls_column.addLayout(timer_controls)
        self.skip = QPushButton("رد کردن استراحت")
        self.skip.setStyleSheet("font-size: 12px;")
        self.skip.clicked.connect(finish)
        controls_column.addWidget(self.skip)
        row.addWidget(controls)
        timer_column = QVBoxLayout()
        timer_column.setSpacing(0)
        self.countdown = label("", "timer", center=True)
        self.countdown.setLayoutDirection(Qt.LeftToRight)
        self.countdown.setMinimumWidth(160)
        timer_column.addWidget(self.countdown)
        self.timer_caption = label("زمان باقی‌مانده", "muted", center=True)
        timer_column.addWidget(self.timer_caption)
        row.addLayout(timer_column)
        text = QVBoxLayout()
        self.heading = label("وقت استراحت است")
        self.heading.setFont(QFont("DejaVu Sans", 13, QFont.Bold))
        self.heading.setWordWrap(True)
        text.addWidget(self.heading)
        self.caption = label("چند دقیقه از صفحه فاصله بگیر.", "muted")
        self.caption.setWordWrap(True)
        text.addWidget(self.caption)
        row.addLayout(text, 1)

    def resizeEvent(self, event):
        width = min(820, self.width() - 32)
        self.card.setGeometry((self.width() - width) // 2, 24, width, 116)
        self.caption.setVisible(width >= 620)
        self.timer_caption.setVisible(width >= 620)
        super().resizeEvent(event)

    def set_running(self, running):
        self.start.setEnabled(not running)
        self.stop.setEnabled(running)
        self.heading.setText("وقت استراحت است" if running else "استراحت متوقف شده")
        self.timer_caption.setText("زمان باقی‌مانده" if running else "تایمر متوقف است")

    def paintEvent(self, event):
        painter = QPainter(self)
        if not self.background.isNull():
            painter.setRenderHint(QPainter.SmoothPixmapTransform)
            painter.drawPixmap(self.rect(), self.background)
        painter.fillRect(self.rect(), QColor(13, 21, 33, 110))

    def keyPressEvent(self, event):
        if event.key() == Qt.Key_Escape:
            self.finish()
        else:
            super().keyPressEvent(event)

    def closeEvent(self, event):
        event.ignore()
        self.finish()


class Controller:
    def __init__(self, app):
        self.app = app
        self.preferences = read_preferences()
        self.icon = QIcon(str(ROOT / "assets/RestTimer.png"))
        self.breaking = False
        self.paused_at = None
        self.quitting = False
        self.overlays = []
        self.display_signature = None
        self.root, self.version = ROOT, VERSION
        self.updater = update_core.Updater(config_root() / "resttimer/updates")
        self.workers = []
        self.update_dialog = None
        self.checking_updates = False
        self.instance = None
        self.deadline = time.monotonic() + self.preferences["work_seconds"]
        self.menu = QMenu()
        self.start_timer_action = self.menu.addAction("شروع", self.start_timer)
        self.stop_timer_action = self.menu.addAction("توقف", self.stop_timer)
        self.start_action = self.menu.addAction("شروع استراحت همین حالا", self.begin_break)
        self.menu.addSeparator()
        self.status = self.menu.addAction("RestTimer")
        self.status.setEnabled(False)
        self.menu.addSeparator()
        self.settings_action = self.menu.addAction("تنظیم زمان‌ها…", self.show_settings)
        self.startup_action = QAction("اجرا با ورود به سیستم", self.menu)
        self.startup_action.setCheckable(True)
        self.startup_action.setChecked(startup_path().exists())
        self.startup_action.triggered.connect(self.toggle_startup)
        self.menu.addAction(self.startup_action)
        self.update_action = self.menu.addAction("بررسی به‌روزرسانی…", self.check_updates)
        self.menu.addAction("تاریخچهٔ تغییرات…", lambda: self.check_updates(history=True))
        self.menu.addSeparator()
        self.menu.addAction("خروج", self.quit)
        self.tray = QSystemTrayIcon(self.icon, app)
        self.tray.setContextMenu(self.menu)
        self.tray.activated.connect(self.tray_activated)
        self.tray.messageClicked.connect(self.check_updates)
        self.tray.show()
        self.settings = SettingsDialog(self)
        self.timer = QTimer(app)
        self.timer.setInterval(250)
        self.timer.timeout.connect(self.tick)
        self.timer.start()
        app.screenAdded.connect(lambda _: self.refresh_displays())
        app.screenRemoved.connect(lambda _: self.refresh_displays())
        self.tick()
        self.update_timer = QTimer(app)
        self.update_timer.setInterval(60000)
        self.update_timer.timeout.connect(self.daily_updates)
        if "--smoke-test" not in sys.argv:
            self.update_timer.start()
            QTimer.singleShot(5000, self.daily_updates)

    def tray_activated(self, reason):
        if reason == QSystemTrayIcon.Trigger:
            self.menu.popup(QCursor.pos())

    def show_settings(self):
        if not self.breaking:
            self.settings.show()
            self.settings.raise_()
            self.settings.activateWindow()

    def toggle_startup(self, enabled):
        try:
            set_startup(enabled)
        except OSError:
            QMessageBox.warning(self.settings, "RestTimer", "ذخیرهٔ اجرای خودکار ممکن نشد.")
        actual = startup_path().exists()
        self.startup_action.setChecked(actual)
        self.settings.startup.setChecked(actual)

    def clear_overlays(self):
        for overlay in self.overlays:
            overlay.hide()
            overlay.deleteLater()
        self.overlays = []
        self.display_signature = None

    @property
    def running(self):
        return self.paused_at is None

    def start_timer(self):
        if not self.running:
            self.deadline += time.monotonic() - self.paused_at
            self.paused_at = None
        self.tick()

    def stop_timer(self):
        if self.running:
            self.paused_at = time.monotonic()
        self.tick()

    def refresh_displays(self):
        if not self.breaking:
            return
        screens = self.app.screens()
        signature = tuple((screen.name(), screen.geometry().getRect()) for screen in screens)
        if signature == self.display_signature:
            return
        self.clear_overlays()
        self.overlays = [
            BreakOverlay(screen, self.begin_work, self.start_timer, self.stop_timer)
            for screen in screens
        ]
        self.display_signature = signature
        for overlay in self.overlays:
            overlay.set_running(self.running)
            overlay.showFullScreen()

    def begin_break(self):
        if self.breaking:
            return
        self.menu.close()
        self.breaking = True
        self.paused_at = None
        self.start_action.setEnabled(False)
        self.settings_action.setEnabled(False)
        self.deadline = time.monotonic() + self.preferences["break_seconds"]
        self.refresh_displays()
        self.tick()

    def begin_work(self):
        self.clear_overlays()
        self.breaking = False
        self.start_action.setEnabled(True)
        self.settings_action.setEnabled(True)
        now = time.monotonic()
        if not self.running:
            self.paused_at = now
        self.deadline = now + self.preferences["work_seconds"]
        self.tick()

    def tick(self):
        self.start_timer_action.setEnabled(not self.running)
        self.stop_timer_action.setEnabled(self.running)
        self.settings.start_timer.setEnabled(not self.running)
        self.settings.stop_timer.setEnabled(self.running)
        clock_time = time.monotonic() if self.running else self.paused_at
        remaining = max(0, math.ceil(self.deadline - clock_time))
        if remaining == 0 and self.running:
            if self.breaking:
                self.begin_work()
            else:
                self.begin_break()
            return
        hours, tail = divmod(remaining, 3600)
        minutes, seconds = divmod(tail, 60)
        text = f"{minutes:02}:{seconds:02}"
        if hours:
            text = f"{hours}:{text}"
        self.status.setText(("پایان استراحت  " if self.breaking else "تا استراحت بعدی  ") + "\u200e" + text)
        if not self.running:
            self.status.setText("متوقف · " + self.status.text())
        self.settings.timer_status.setText(self.status.text())
        self.tray.setToolTip(("RestTimer  " if self.running else "RestTimer · متوقف  ") + text)
        self.notify_update()
        if self.breaking:
            self.refresh_displays()
            for overlay in self.overlays:
                overlay.countdown.setText(text)
                overlay.set_running(self.running)

    def run_update_job(self, job, success, failure, progress=None, finished=None):
        worker = Worker(job, self.app)
        self.workers.append(worker)
        worker.completed.connect(lambda result: success(result) if not self.quitting else None)
        worker.failed.connect(lambda error: failure(error) if not self.quitting else None)
        if progress:
            worker.progress.connect(progress)
        def done():
            if finished:
                finished()
            self.workers.remove(worker)
            worker.deleteLater()
        worker.finished.connect(done)
        worker.start()
        return worker

    def daily_updates(self):
        if self.quitting or self.checking_updates or not self.updater.due():
            return
        self.checking_updates = True
        self.updater.attempt()
        def received(release):
            self.updater.accept_latest(release)
            self.notify_update()
        def done():
            self.checking_updates = False
        self.run_update_job(lambda worker: self.updater.latest(), received, lambda _: None, finished=done)

    def notify_update(self):
        release = self.updater.state["latest"]
        if self.breaking or not update_core.stable(release) or version_tuple(release["tag_name"]) <= version_tuple(VERSION):
            return
        text = "نسخهٔ " + release["tag_name"] + " آماده است؛ تغییرات و نصب…"
        self.update_action.setText(text)
        self.settings.update_notice.setText(text)
        self.settings.update_notice.setVisible(True)
        if not self.updater.state["daily_enabled"]:
            return
        if self.updater.state["notified_tag"] == release["tag_name"]:
            return
        if QSystemTrayIcon.isSystemTrayAvailable():
            self.tray.showMessage("به‌روزرسانی RestTimer", text, QSystemTrayIcon.Information, 10000)
        else:
            self.settings.show()
            self.app.alert(self.settings)
        self.updater.state["notified_tag"] = release["tag_name"]
        self.updater.save()

    def check_updates(self, checked=False, history=False):
        if self.breaking:
            return
        if self.update_dialog is None or self.update_dialog.closed:
            self.update_dialog = UpdateDialog(self, history)
        elif history:
            self.update_dialog.tabs.setCurrentIndex(1)
        self.update_dialog.show()
        self.update_dialog.raise_()
        self.update_dialog.activateWindow()

    def restart_after_update(self):
        try:
            if self.instance:
                lock, server = self.instance
                server.close()
                lock.close()
                self.instance = None
            subprocess.Popen(["/opt/resttimer/resttimer", "--background"], env=update_core.system_environment(), close_fds=True)
        except OSError:
            self.update_dialog.status.setText("نصب کامل شد؛ برنامه را ببندید و از منوی برنامه‌ها دوباره اجرا کنید.")
            self.instance = acquire_instance(self)
            return
        self.quit()

    def quit(self):
        if self.update_dialog is not None and self.update_dialog.process is not None:
            self.update_dialog.status.setText("نصب در حال انجام است. پس از پایان نصب می‌توانید برنامه را ببندید.")
            self.update_dialog.show()
            self.update_dialog.raise_()
            return
        self.quitting = True
        self.timer.stop()
        self.update_timer.stop()
        for worker in self.workers:
            worker.cancelled.set()
        self.clear_overlays()
        self.tray.hide()
        self.app.quit()


def acquire_instance(controller):
    import fcntl

    runtime = Path(QStandardPaths.writableLocation(QStandardPaths.RuntimeLocation)) / "resttimer"
    runtime.mkdir(mode=0o700, parents=True, exist_ok=True)
    if runtime.stat().st_uid != os.getuid() or runtime.stat().st_mode & 0o077:
        raise OSError("RestTimer needs a private runtime directory.")
    lock = (runtime / "instance.lock").open("a+")
    socket_name = str(runtime / "instance.socket")
    try:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError:
        client = QLocalSocket()
        client.connectToServer(socket_name)
        if client.waitForConnected(2000):
            client.write(b"show")
            client.waitForBytesWritten(1000)
        lock.close()
        return None
    QLocalServer.removeServer(socket_name)
    server = QLocalServer(controller.app)
    if not server.listen(socket_name):
        raise OSError(server.errorString())

    def activate():
        client = server.nextPendingConnection()
        controller.show_settings()
        client.disconnectFromServer()
        client.deleteLater()

    server.newConnection.connect(activate)
    return lock, server


def main():
    QLocale.setDefault(QLocale(QLocale.Persian))
    app = QApplication(sys.argv)
    app.setApplicationName("RestTimer")
    app.setOrganizationName("RestTimer")
    app.setQuitOnLastWindowClosed(False)
    app.setLayoutDirection(Qt.RightToLeft)
    app.setFont(QFont("DejaVu Sans", 10))
    apply_color_scheme(app, app.styleHints().colorScheme())
    app.styleHints().colorSchemeChanged.connect(lambda scheme: apply_color_scheme(app, scheme))
    controller = Controller(app)
    if "--smoke-test" in sys.argv:
        if update_core.tls_context().cert_store_stats()["x509_ca"] == 0:
            raise RuntimeError("No system certificate authorities available for updates.")
        controller.begin_break()
        QTimer.singleShot(300, controller.begin_work)
        QTimer.singleShot(600, controller.quit)
    else:
        instance = acquire_instance(controller)
        if instance is None:
            controller.quit()
            return 0
        controller.instance = instance
        if "--background" not in sys.argv or not QSystemTrayIcon.isSystemTrayAvailable():
            controller.show_settings()
    return app.exec()


if __name__ == "__main__":
    raise SystemExit(main())
