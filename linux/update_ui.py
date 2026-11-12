"""Non-blocking updater UI. No download or install starts before an explicit click."""
from pathlib import Path
import threading

from PySide6.QtCore import QObject, QProcess, QProcessEnvironment, QTimer, Signal
from PySide6.QtWidgets import QCheckBox, QComboBox, QDialog, QHBoxLayout, QLabel, QProgressBar, QPushButton, QTabWidget, QTextEdit, QVBoxLayout, QWidget

import update_core as core


class Worker(QObject):
    completed = Signal(object)
    failed = Signal(str)
    progress = Signal(int)
    finished = Signal()

    def __init__(self, job, parent):
        super().__init__(parent)
        self.cancelled = threading.Event()
        self.job = job

    def start(self):
        threading.Thread(target=self.run, daemon=True).start()

    def run(self):
        try:
            result = self.job(self)
            if not self.cancelled.is_set():
                self.completed.emit(result)
        except Exception as error:
            if not self.cancelled.is_set():
                self.failed.emit(str(error))
        finally:
            try:
                self.finished.emit()
            except RuntimeError:
                # The application can exit while a cancelled network read finishes.
                pass


class UpdateDialog(QDialog):
    def __init__(self, controller, history=False, check_on_show=True):
        super().__init__()
        self.controller = controller
        self.updater = controller.updater
        self.worker = None
        self.process = None
        self.closed = False
        self.setWindowTitle("RestTimer — به‌روزرسانی و تغییرات")
        self.setWindowIcon(controller.icon)
        self.resize(700, 640)
        self.setMinimumSize(620, 540)
        layout = QVBoxLayout(self)
        layout.setContentsMargins(24, 22, 24, 22)
        layout.setSpacing(14)
        title = QLabel("به‌روزرسانی و تغییرات")
        title.setObjectName("title")
        layout.addWidget(title)
        self.description = QLabel()
        self.description.setWordWrap(True)
        layout.addWidget(self.description)
        self.daily = QCheckBox("بررسی خودکار روزانه و اعلان نسخهٔ جدید")
        self.daily.setChecked(self.updater.state["daily_enabled"])
        self.daily.toggled.connect(self.toggle_daily)
        layout.addWidget(self.daily)
        self.tabs = QTabWidget()
        self.latest_notes = QTextEdit()
        self.latest_notes.setReadOnly(True)
        self.tabs.addTab(self.latest_notes, "آخرین نسخه")
        history_tab = QWidget()
        history_layout = QVBoxLayout(history_tab)
        self.versions = QComboBox()
        self.versions.addItem("تغییرات همراه برنامه (آفلاین)", None)
        self.versions.currentIndexChanged.connect(self.select_history)
        self.history_notes = QTextEdit()
        self.history_notes.setReadOnly(True)
        history_layout.addWidget(self.versions)
        history_layout.addWidget(self.history_notes)
        self.tabs.addTab(history_tab, "تاریخچهٔ نسخه‌ها")
        self.tabs.setCurrentIndex(1 if history else 0)
        layout.addWidget(self.tabs, 1)
        self.history_notes.setPlainText(self.bundled_history())
        consent = QLabel("با انتخاب «دانلود و نصب»، به‌روزرسانی انجام می‌شود. برای نصب، مجوز مدیر سیستم درخواست می‌شود و برنامه پس از نصب دوباره اجرا خواهد شد.")
        consent.setObjectName("muted")
        consent.setWordWrap(True)
        layout.addWidget(consent)
        self.progress = QProgressBar()
        self.progress.setRange(0, 100)
        layout.addWidget(self.progress)
        self.status = QLabel()
        self.status.setWordWrap(True)
        self.status.setObjectName("muted")
        layout.addWidget(self.status)
        buttons = QHBoxLayout()
        self.install = QPushButton("دانلود و نصب")
        self.install.setObjectName("primary")
        self.install.clicked.connect(self.download)
        self.check = QPushButton("بررسی دوباره")
        self.check.clicked.connect(self.refresh)
        self.close_button = QPushButton("بستن / لغو")
        self.close_button.clicked.connect(self.close)
        for button in (self.install, self.check, self.close_button):
            buttons.addWidget(button)
        layout.addLayout(buttons)
        self.show_latest()
        if check_on_show:
            QTimer.singleShot(0, self.refresh)

    def bundled_history(self):
        return (self.controller.root / "CHANGELOG.md").read_text(encoding="utf-8")

    def toggle_daily(self, enabled):
        self.updater.state["daily_enabled"] = enabled
        self.updater.save()

    def select_history(self):
        release = self.versions.currentData()
        self.history_notes.setPlainText((release["tag_name"] + "\n\n" + (release.get("body") or "توضیحی ثبت نشده است."))
                                       if release else self.bundled_history())

    def show_latest(self):
        release = self.updater.state["latest"]
        self.displayed_release = release
        newer = core.stable(release) and core.version_tuple(release["tag_name"]) > core.version_tuple(self.controller.version)
        self.description.setText("نسخهٔ نصب‌شده: " + self.controller.version + ("    ·    نسخهٔ جدید: " + release["tag_name"] if newer else ""))
        self.latest_notes.setPlainText(release["tag_name"] + "\n\n" + (release.get("body") or "توضیحی ثبت نشده است.")
                                      if core.stable(release) else self.bundled_history())
        self.install.setEnabled(bool(newer) and self.worker is None and self.process is None)
        self.check.setEnabled(self.worker is None and self.process is None)
        self.install.setText("دانلود و نصب " + release["tag_name"] if newer else "دانلود و نصب")

    def run_job(self, job, success, progress=None):
        def failed(_):
            if not self.closed:
                self.status.setText("دریافت اطلاعات یا فایل ممکن نشد. اتصال را بررسی و دوباره تلاش کنید؛ فایل ناقص نصب نمی‌شود.")

        def finished():
            self.worker = None
            if not self.closed:
                self.show_latest()

        self.worker = self.controller.run_update_job(job, success, failed, progress, finished)
        self.show_latest()

    def refresh(self):
        if self.worker is not None or self.process is not None or self.closed:
            return
        self.status.setText("در حال بررسی نسخه و دریافت تغییرات…")
        self.updater.attempt()

        def received(result):
            if self.closed:
                return
            latest, history = result
            self.updater.accept_latest(latest)
            self.show_latest()
            self.versions.clear()
            self.versions.addItem("تغییرات همراه برنامه (آفلاین)", None)
            for release in history:
                self.versions.addItem("RestTimer " + release["tag_name"], release)
            self.versions.setCurrentIndex(1 if history else 0)
            self.status.setText("نسخهٔ جدید آماده است. تغییرات را بخوانید و برای نصب تأیید کنید."
                                if core.stable(latest) and core.version_tuple(latest["tag_name"]) > core.version_tuple(self.controller.version)
                                else "برنامهٔ شما به‌روز است.")

        self.run_job(lambda worker: (self.updater.latest(), self.updater.history()), received)

    def download(self):
        if not self.install.isEnabled():
            return
        approved_release = self.displayed_release
        try:
            kind = core.package_kind()
            core.installer_asset(approved_release, kind)
        except (OSError, ValueError):
            self.status.setText("بستهٔ سازگار با این سیستم هنوز آماده نشده است.")
            return
        self.progress.setValue(0)
        self.status.setText("در حال دانلود و بررسی فایل… برای لغو پنجره را ببندید.")

        def received(path):
            if not self.closed:
                self.install_package(path, kind)

        self.run_job(lambda worker: core.download(approved_release, kind, self.updater.directory / "downloads",
                                                  worker.cancelled, worker.progress.emit), received, self.progress.setValue)

    def install_package(self, path, kind):
        try:
            command = core.install_command(path, kind)
        except (OSError, ValueError):
            self.status.setText("مدیر نصب یا سرویس مجوز سیستم در دسترس نیست. بسته را از پوشهٔ دانلودهای RestTimer نصب کنید.")
            return
        self.process = QProcess(self)
        env = QProcessEnvironment()
        for key, value in core.system_environment().items():
            env.insert(key, value)
        self.process.setProcessEnvironment(env)
        self.process.setProcessChannelMode(QProcess.MergedChannels)
        self.process.readyReadStandardOutput.connect(lambda: self.process.readAllStandardOutput() if self.process else None)
        self.process.finished.connect(self.install_finished)
        self.process.errorOccurred.connect(self.install_error)
        self.close_button.setEnabled(False)
        self.status.setText("در حال نصب… درخواست مجوز مدیر سیستم را تأیید کنید.")
        self.process.start(command[0], command[1:])

    def install_finished(self, exit_code, exit_status):
        process, self.process = self.process, None
        if process:
            process.deleteLater()
        self.close_button.setEnabled(True)
        if exit_code == 0 and exit_status == QProcess.NormalExit:
            self.status.setText("نصب کامل شد. در حال اجرای دوبارهٔ برنامه…")
            self.controller.restart_after_update()
        else:
            self.status.setText("نصب انجام نشد یا مجوز رد شد. برنامهٔ فعلی همچنان باز است؛ می‌توانید دوباره تلاش کنید.")
        self.show_latest()

    def install_error(self, error):
        if error == QProcess.FailedToStart:
            self.install_finished(-1, QProcess.CrashExit)

    def closeEvent(self, event):
        if self.process is not None:
            event.ignore()
            return
        self.closed = True
        if self.worker is not None:
            self.worker.cancelled.set()
        event.accept()

    def reject(self):
        self.close()
