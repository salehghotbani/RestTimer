"""Linux desktop frontend for RestTimer. Run from source with Python and PySide6."""
import json
import math
import os
from pathlib import Path
import re
import sys
import time

from PySide6.QtCore import QIODevice, QLocale, QSaveFile, QStandardPaths, Qt, QTimer, QUrl
from PySide6.QtGui import QAction, QColor, QCursor, QDesktopServices, QFont, QIcon, QPainter
from PySide6.QtNetwork import QLocalServer, QLocalSocket, QNetworkAccessManager, QNetworkReply, QNetworkRequest
from PySide6.QtWidgets import (
    QAbstractSpinBox, QApplication, QCheckBox, QDialog, QFrame, QHBoxLayout, QLabel,
    QMenu, QMessageBox, QPushButton, QSpinBox, QSystemTrayIcon, QVBoxLayout, QWidget,
)

ROOT = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parents[1]))
VERSION = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
RELEASES_URL = "https://github.com/salehghotbani/RestTimer/releases/latest"
DEFAULTS = {"work_seconds": 3600, "break_seconds": 900}
STYLE = """
QDialog, QMenu { background: #0d1521; color: #edf4fa; }
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
QSpinBox {
    background: #0c1623; color: #edf4fa; border: 1px solid #2c3e52;
    border-radius: 10px; font-size: 26px; padding: 7px; min-width: 70px;
}
QSpinBox:focus { border-color: #83e6c4; }
QCheckBox { color: #9bb0c3; spacing: 10px; }
QMenu { border: 1px solid #2c3e52; padding: 7px; }
QMenu::item { padding: 11px 30px; border-radius: 7px; }
QMenu::item:selected { background: #223a48; color: #83e6c4; }
QMenu::item:disabled { color: #9bb0c3; }
QMenu::separator { height: 1px; background: #2c3e52; margin: 5px 12px; }
"""


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
    widget.setAlignment(Qt.AlignCenter if center else Qt.AlignRight | Qt.AlignVCenter)
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
            spin.setAlignment(Qt.AlignCenter)
            spin.setLayoutDirection(Qt.LeftToRight)
            spin.setButtonSymbols(QAbstractSpinBox.NoButtons)
            column.addWidget(spin)
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
        self.note = label("با تغییر زمان‌ها، شمارش کار از نو شروع می‌شود.", "muted")
        self.note.setWordWrap(True)
        layout.addWidget(self.note)
        buttons = QHBoxLayout()
        self.save = QPushButton("ذخیرهٔ زمان‌ها")
        self.save.setObjectName("primary")
        self.save.clicked.connect(self.save_durations)
        buttons.addWidget(self.save)
        self.updates = QPushButton("بررسی به‌روزرسانی")
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
    def __init__(self, screen, finish):
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
        self.skip = QPushButton("رد کردن استراحت")
        self.skip.setFixedWidth(120)
        self.skip.setStyleSheet("font-size: 12px;")
        self.skip.clicked.connect(finish)
        row.addWidget(self.skip)
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
        heading = label("وقت استراحت است")
        heading.setFont(QFont("DejaVu Sans", 13, QFont.Bold))
        text.addWidget(heading)
        self.caption = label("چند دقیقه از صفحه فاصله بگیر.", "muted")
        self.caption.setWordWrap(True)
        text.addWidget(self.caption)
        row.addLayout(text, 1)

    def resizeEvent(self, event):
        width = min(760, self.width() - 32)
        self.card.setGeometry((self.width() - width) // 2, 24, width, 116)
        self.caption.setVisible(width >= 620)
        self.timer_caption.setVisible(width >= 620)
        super().resizeEvent(event)

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
        self.quitting = False
        self.overlays = []
        self.display_signature = None
        self.reply = None
        self.deadline = time.monotonic() + self.preferences["work_seconds"]
        self.menu = QMenu()
        self.status = self.menu.addAction("RestTimer")
        self.status.setEnabled(False)
        self.menu.addSeparator()
        self.start_action = self.menu.addAction("شروع استراحت همین حالا", self.begin_break)
        self.settings_action = self.menu.addAction("تنظیم زمان‌ها…", self.show_settings)
        self.startup_action = QAction("اجرا با ورود به سیستم", self.menu)
        self.startup_action.setCheckable(True)
        self.startup_action.setChecked(startup_path().exists())
        self.startup_action.triggered.connect(self.toggle_startup)
        self.menu.addAction(self.startup_action)
        self.update_action = self.menu.addAction("بررسی به‌روزرسانی…", self.check_updates)
        self.menu.addSeparator()
        self.menu.addAction("خروج", self.quit)
        self.tray = QSystemTrayIcon(self.icon, app)
        self.tray.setContextMenu(self.menu)
        self.tray.activated.connect(self.tray_activated)
        self.tray.show()
        self.settings = SettingsDialog(self)
        self.network = QNetworkAccessManager(app)
        self.timer = QTimer(app)
        self.timer.setInterval(250)
        self.timer.timeout.connect(self.tick)
        self.timer.start()
        app.screenAdded.connect(lambda _: self.refresh_displays())
        app.screenRemoved.connect(lambda _: self.refresh_displays())
        self.tick()

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

    def refresh_displays(self):
        if not self.breaking:
            return
        screens = self.app.screens()
        signature = tuple((screen.name(), screen.geometry().getRect()) for screen in screens)
        if signature == self.display_signature:
            return
        self.clear_overlays()
        self.overlays = [BreakOverlay(screen, self.begin_work) for screen in screens]
        self.display_signature = signature
        for overlay in self.overlays:
            overlay.showFullScreen()

    def begin_break(self):
        if self.breaking:
            return
        self.menu.close()
        self.breaking = True
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
        self.deadline = time.monotonic() + self.preferences["work_seconds"]
        self.tick()

    def tick(self):
        remaining = max(0, math.ceil(self.deadline - time.monotonic()))
        if remaining == 0:
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
        self.tray.setToolTip("RestTimer  " + text)
        if self.breaking:
            self.refresh_displays()
            for overlay in self.overlays:
                overlay.countdown.setText(text)

    def check_updates(self):
        if self.reply is not None or self.breaking:
            return
        self.show_settings()
        self.update_action.setEnabled(False)
        self.settings.updates.setEnabled(False)
        self.settings.note.setText("در حال بررسی آخرین نسخه…")
        request = QNetworkRequest(QUrl("https://api.github.com/repos/salehghotbani/RestTimer/releases/latest"))
        request.setRawHeader(b"User-Agent", f"RestTimer/{VERSION}".encode())
        request.setRawHeader(b"Accept", b"application/vnd.github+json")
        request.setTransferTimeout(15000)
        self.reply = self.network.get(request)
        self.reply.finished.connect(self.update_finished)

    def update_finished(self):
        reply, self.reply = self.reply, None
        if reply is None:
            return
        try:
            if self.quitting:
                return
            if reply.attribute(QNetworkRequest.HttpStatusCodeAttribute) == 404:
                self.settings.note.setText("هنوز نسخه‌ای برای دریافت منتشر نشده است.")
                return
            if reply.error() != QNetworkReply.NoError or reply.bytesAvailable() > 2 * 1024 * 1024:
                raise ValueError("Release request failed")
            data = json.loads(bytes(reply.readAll()))
            if data.get("draft") or data.get("prerelease"):
                raise ValueError("No stable release")
            if version_tuple(data.get("tag_name")) <= version_tuple(VERSION):
                self.settings.note.setText("برنامهٔ شما به‌روز است.")
                return
            self.settings.note.setText("نسخهٔ جدید آمادهٔ دریافت است.")
            choice = QMessageBox.question(
                self.settings, "RestTimer",
                "نسخهٔ جدید منتشر شده است. صفحهٔ دریافت بستهٔ DEB یا RPM باز شود؟",
            )
            if choice == QMessageBox.Yes:
                QDesktopServices.openUrl(QUrl(RELEASES_URL))
        except (ValueError, TypeError, AttributeError):
            self.settings.note.setText("بررسی نسخه ممکن نشد. اتصال اینترنت را بررسی کن و دوباره تلاش کن.")
        finally:
            reply.deleteLater()
            self.update_action.setEnabled(True)
            self.settings.updates.setEnabled(True)

    def quit(self):
        self.quitting = True
        self.timer.stop()
        if self.reply is not None:
            self.reply.abort()
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
    app.setStyleSheet(STYLE)
    controller = Controller(app)
    if "--smoke-test" in sys.argv:
        controller.begin_break()
        QTimer.singleShot(300, controller.begin_work)
        QTimer.singleShot(600, controller.quit)
    else:
        instance = acquire_instance(controller)
        if instance is None:
            controller.quit()
            return 0
        if "--background" not in sys.argv or not QSystemTrayIcon.isSystemTrayAvailable():
            controller.show_settings()
    return app.exec()


if __name__ == "__main__":
    raise SystemExit(main())
