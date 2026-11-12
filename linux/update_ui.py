"""Non-blocking updater UI. No download or install starts before an explicit click."""
from pathlib import Path
import threading
import html
import re

from PySide6.QtCore import QObject, QProcess, QProcessEnvironment, QTimer, Signal, Qt
from PySide6.QtGui import QTextCursor
from PySide6.QtWidgets import QCheckBox, QComboBox, QDialog, QHBoxLayout, QLabel, QProgressBar, QPushButton, QTabWidget, QTextEdit, QVBoxLayout, QWidget

import update_core as core
import i18n
from i18n import tr


def notes_html(markdown):
    paragraphs = []
    beginning = True
    for raw in (markdown or "").splitlines():
        line = raw.strip()
        plain = line.lstrip("# *")
        if not line:
            continue
        if beginning and (plain == "تغییرات RestTimer" or plain == "RestTimer changelog" or re.fullmatch(r"RestTimer\s+[vV]?\d+(?:\.\d+){1,3}|نسخه[ٔ\s]*[\d٫.]+(?:\s+RestTimer)?", plain)):
            continue
        beginning = False
        heading = line.startswith("#") or re.fullmatch(r"[A-Za-z ]+:", line)
        text = re.sub(r"\[([^\]]+)\]\([^)]*\)", r"\1", plain).replace("**", "").replace("`", "")
        if text.startswith("- "):
            text = "•  " + text[2:]
        rtl = bool(re.search(r"[\u0600-\u06ff]", text))
        direction, alignment = ("rtl", "right") if rtl else ("ltr", "left")
        tag = "h3" if heading else "p"
        paragraphs.append(f'<{tag} dir="{direction}" style="text-align:{alignment};margin:8px 0 12px;">{html.escape(text)}</{tag}>')
    return '<div style="font-size:11pt;">' + "".join(paragraphs) + '</div>'


def set_notes(widget, markdown):
    # Keep the current document and scroll position when navigating between pages.
    if getattr(widget, "_notes_markdown", None) == markdown:
        return
    widget.setHtml(notes_html(markdown))
    # Qt's HTML importer can inherit the outer div's alignment. Apply both
    # paragraph properties explicitly, preserving heading and character styles.
    block = widget.document().begin()
    while block.isValid():
        rtl = bool(re.search(r"[\u0600-\u06ff]", block.text()))
        formatting = block.blockFormat()
        formatting.setAlignment(Qt.AlignAbsolute | (Qt.AlignRight if rtl else Qt.AlignLeft))
        formatting.setLayoutDirection(Qt.RightToLeft if rtl else Qt.LeftToRight)
        QTextCursor(block).setBlockFormat(formatting)
        block = block.next()
    widget._notes_markdown = markdown


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


class UpdatePanel(QWidget):
    daily_changed = Signal(bool)

    def __init__(self, controller, check_on_show=True):
        super().__init__()
        self.controller = controller
        self.updater = controller.updater
        self.worker = None
        self.process = None
        self.closed = False
        self.check_on_show = check_on_show
        self.initially_checked = False
        self.current_history = False
        self.setWindowTitle("RestTimer — " + tr("updates_title"))
        self.setWindowIcon(controller.icon)
        layout = QVBoxLayout(self)
        layout.setContentsMargins(24, 22, 24, 22)
        layout.setSpacing(14)
        title = QLabel(tr("updates_title"))
        self.heading = title
        title.setObjectName("title")
        layout.addWidget(title)
        self.description = QLabel()
        self.description.setWordWrap(True)
        layout.addWidget(self.description)
        self.daily = QCheckBox(tr("daily_check"))
        self.daily.setChecked(self.updater.state["daily_enabled"])
        self.daily.toggled.connect(self.toggle_daily)
        layout.addWidget(self.daily)
        self.tabs = QTabWidget()
        self.latest_notes = QTextEdit()
        self.latest_notes.setReadOnly(True)
        self.tabs.addTab(self.latest_notes, tr("latest"))
        history_tab = QWidget()
        history_layout = QVBoxLayout(history_tab)
        self.versions = QComboBox()
        self.versions.addItem(tr("bundled_history"), None)
        self.versions.currentIndexChanged.connect(self.select_history)
        self.history_notes = QTextEdit()
        self.history_notes.setReadOnly(True)
        history_layout.addWidget(self.versions)
        history_layout.addWidget(self.history_notes)
        self.tabs.addTab(history_tab, tr("version_history"))
        self.tabs.tabBar().hide()
        layout.addWidget(self.tabs, 1)
        set_notes(self.history_notes, self.bundled_history())
        consent = QLabel(tr("consent_linux"))
        self.consent = consent
        consent.setObjectName("muted")
        consent.setWordWrap(True)
        layout.addWidget(consent)
        self.progress = QProgressBar()
        self.progress.setRange(0, 100)
        self.progress.setVisible(False)
        layout.addWidget(self.progress)
        self.status = QLabel()
        self.status.setWordWrap(True)
        self.status.setObjectName("muted")
        layout.addWidget(self.status)
        buttons = QHBoxLayout()
        self.install = QPushButton(tr("install"))
        self.install.setObjectName("primary")
        self.install.clicked.connect(self.download)
        self.check = QPushButton(tr("check_again"))
        self.check.clicked.connect(self.refresh)
        for button in (self.install, self.check):
            buttons.addWidget(button)
        layout.addLayout(buttons)
        self.show_latest()

    def ensure_loaded(self):
        if self.check_on_show and not self.initially_checked and not self.closed:
            self.initially_checked = True
            self.refresh()

    def set_page(self, history):
        self.current_history = history
        self.tabs.setCurrentIndex(1 if history else 0)
        self.heading.setText(tr("version_history" if history else "updates_title"))
        self.consent.setVisible(not history)
        self.install.setVisible(not history)
        self.show_latest()

    def retranslate(self, previous=None):
        self.setLayoutDirection(Qt.RightToLeft if i18n.is_rtl() else Qt.LeftToRight)
        self.setWindowTitle("RestTimer — " + tr("updates_title"))
        self.heading.setText(tr("version_history" if self.current_history else "updates_title"))
        self.daily.setText(tr("daily_check"))
        self.tabs.setTabText(0, tr("latest"))
        self.tabs.setTabText(1, tr("version_history"))
        self.versions.setItemText(0, tr("bundled_history"))
        self.consent.setText(tr("consent_linux"))
        self.check.setText(tr("check_again"))
        if previous:
            for key, value in i18n.catalog(previous)["strings"].items():
                if self.status.text() == value:
                    self.status.setText(tr(key))
                    break
        self.show_latest()
        self.select_history()

    def bundled_history(self):
        return (self.controller.root / ("CHANGELOG.fa.md" if i18n.code == "fa" else "CHANGELOG.md")).read_text(encoding="utf-8")

    def toggle_daily(self, enabled):
        self.daily_changed.emit(enabled)

    def select_history(self):
        release = self.versions.currentData()
        set_notes(self.history_notes, (release.get("body") or tr("no_notes")) if release else self.bundled_history())

    def show_latest(self):
        release = self.updater.state["latest"]
        self.displayed_release = release
        newer = core.stable(release) and core.version_tuple(release["tag_name"]) > core.version_tuple(self.controller.version)
        self.description.setText(tr("installed", self.controller.version) + ("    ·    " + tr("new_version", release["tag_name"]) if newer else ""))
        set_notes(self.latest_notes, (release.get("body") or tr("no_notes")) if core.stable(release) else self.bundled_history())
        self.install.setEnabled(bool(newer) and self.worker is None and self.process is None)
        self.check.setEnabled(self.worker is None and self.process is None)
        self.install.setText(tr("install") + (" " + release["tag_name"] if newer else ""))

    def run_job(self, job, success, progress=None):
        def failed(_):
            if not self.closed:
                self.status.setText(tr("download_error"))

        def finished():
            self.worker = None
            if not self.closed:
                self.show_latest()

        self.worker = self.controller.run_update_job(job, success, failed, progress, finished)
        self.show_latest()

    def refresh(self):
        if self.worker is not None or self.process is not None or self.closed:
            return
        self.status.setText(tr("checking"))
        self.updater.attempt()

        def received(result):
            if self.closed:
                return
            latest, history = result
            self.updater.accept_latest(latest)
            self.show_latest()
            self.versions.clear()
            self.versions.addItem(tr("bundled_history"), None)
            for release in history:
                self.versions.addItem("RestTimer " + release["tag_name"], release)
            self.versions.setCurrentIndex(1 if history else 0)
            self.status.setText(tr("update_ready")
                                if core.stable(latest) and core.version_tuple(latest["tag_name"]) > core.version_tuple(self.controller.version)
                                else tr("up_to_date"))

        self.run_job(lambda worker: (self.updater.latest(), self.updater.history()), received)

    def download(self):
        if not self.install.isEnabled():
            return
        approved_release = self.displayed_release
        try:
            kind = core.package_kind()
            core.installer_asset(approved_release, kind)
        except (OSError, ValueError):
            self.status.setText(tr("package_unavailable"))
            return
        self.progress.setValue(0)
        self.progress.setVisible(True)
        self.status.setText(tr("downloading"))

        def received(path):
            if not self.closed:
                self.install_package(path, kind)

        self.run_job(lambda worker: core.download(approved_release, kind, self.updater.directory / "downloads",
                                                  worker.cancelled, worker.progress.emit), received, self.progress.setValue)

    def install_package(self, path, kind):
        try:
            command = core.install_command(path, kind)
        except (OSError, ValueError):
            self.status.setText(tr("installer_unavailable"))
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
        self.status.setText(tr("installing"))
        self.process.start(command[0], command[1:])

    def install_finished(self, exit_code, exit_status):
        process, self.process = self.process, None
        if process:
            process.deleteLater()
        if exit_code == 0 and exit_status == QProcess.NormalExit:
            self.status.setText(tr("install_complete"))
            self.controller.restart_after_update()
        else:
            self.status.setText(tr("install_failed"))
        self.show_latest()

    def install_error(self, error):
        if error == QProcess.FailedToStart:
            self.install_finished(-1, QProcess.CrashExit)

    def request_close(self):
        if self.process is not None:
            self.status.setText(tr("install_busy"))
            return False
        self.closed = True
        if self.worker is not None:
            self.worker.cancelled.set()
        return True

    def closeEvent(self, event):
        event.accept() if self.request_close() else event.ignore()

    def reject(self):
        self.close()
