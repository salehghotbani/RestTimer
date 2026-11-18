"""Linux desktop frontend for RestTimer. Run from source with Python and PySide6."""
import json
import math
import os
from pathlib import Path
import re
import sys
import time
import subprocess
import uuid
import work_tracking

from PySide6.QtCore import QDate, QVariantAnimation, QPropertyAnimation, QEasingCurve, QIODevice, QSize, QLocale, QSaveFile, QStandardPaths, Qt, QTimer, QUrl
from PySide6.QtGui import QAction, QColor, QCursor, QFont, QIcon, QPainter, QTextCursor, QTextLayout, QDesktopServices
from PySide6.QtNetwork import QLocalServer, QLocalSocket
from PySide6.QtWidgets import (
    QAbstractSpinBox, QApplication, QCheckBox, QComboBox, QDialog, QFrame, QHBoxLayout, QLabel,
    QDateEdit, QLineEdit, QTableWidget, QTableWidgetItem, QHeaderView, QMenu, QMessageBox, QTextEdit, QPushButton, QListWidget, QStackedWidget, QScrollArea, QSpinBox, QSystemTrayIcon, QVBoxLayout, QWidget,
)
import update_core
import i18n
import ui_icons
import rtl_ui
from ui_controls import CheckBox as QCheckBox, DropDown as QComboBox, ResizeTransition
from i18n import tr
from update_ui import UpdatePanel, Worker

ROOT = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parents[1]))
VERSION = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
DEFAULTS = {"work_seconds": 3600, "break_seconds": 900, "language": "auto", "calendar": "auto", "color_mode": "system"}
STYLE = """
QDialog, QMenu { background: #0d1521; color: #edf4fa; }
QScrollArea, QScrollArea > QWidget > QWidget { background: #0d1521; border: none; }
QScrollBar:vertical { background: transparent; width: 12px; margin: 4px 2px; border: none; }
QScrollBar::handle:vertical { background: #9bb0c3; min-height: 28px; border-radius: 4px; }
QScrollBar::handle:vertical:hover, QScrollBar::handle:vertical:pressed { background: #83e6c4; }
QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical { height: 0; border: none; background: transparent; }
QScrollBar::add-page:vertical, QScrollBar::sub-page:vertical { background: transparent; }
QScrollBar:horizontal { background: transparent; height: 12px; margin: 2px 4px; border: none; }
QScrollBar::handle:horizontal { background: #9bb0c3; min-width: 28px; border-radius: 4px; }
QScrollBar::handle:horizontal:hover, QScrollBar::handle:horizontal:pressed { background: #83e6c4; }
QScrollBar::add-line:horizontal, QScrollBar::sub-line:horizontal { width: 0; border: none; background: transparent; }
QScrollBar::add-page:horizontal, QScrollBar::sub-page:horizontal { background: transparent; }
QComboBox { min-height:22px; padding:5px 34px 5px 10px; selection-background-color:#223a48; }
QComboBox::drop-down { subcontrol-origin:padding; subcontrol-position:top right; width:28px; border:none; }
QComboBox::down-arrow { image:url("CHEVRON_ICON"); width:12px; height:12px; }
QComboBox QAbstractItemView { background:#141f2e; color:#edf4fa; selection-background-color:#223a48; selection-color:#83e6c4; border:1px solid #2c3e52; padding:4px; outline:0; }
QTextEdit, QComboBox, QLineEdit, QDateEdit, QTableWidget, QTabWidget::pane { background: #141f2e; color: #edf4fa; border: 1px solid #2c3e52; border-radius: 6px; padding: 8px; }
QHeaderView::section { background: #1e2e40; color: #edf4fa; border: none; padding: 10px; }
QTableWidget { gridline-color: #2c3e52; selection-background-color: #223a48; selection-color: #83e6c4; }
QTableWidget::item { padding: 8px; }
QTabBar::tab { background: #1e2e40; color: #edf4fa; padding: 10px 20px; }
QTabBar::tab:selected { background: #223a48; color: #83e6c4; }
QProgressBar { background: #141f2e; color: #edf4fa; border: 1px solid #2c3e52; border-radius: 5px; text-align: center; }
QProgressBar::chunk { background: #16836b; border-radius: 5px; }
QLabel { color: #edf4fa; background: transparent; }
QLabel#muted { color: #9bb0c3; }
QLabel#error { color: #ffa49b; }
QLabel#title { font-size: 23px; font-weight: bold; }
QLabel#sectionTitle { font-size: 14px; font-weight: bold; color: #83e6c4; }
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
QCheckBox { color: #edf4fa; spacing: 10px; }
QCheckBox::indicator { width: 16px; height: 16px; border: 1px solid #2c3e52; border-radius: 4px; background: #0c1623; }
QCheckBox::indicator:checked { background: #83e6c4; border-color: #83e6c4; image: url("CHECK_ICON"); }
QCheckBox::indicator:hover { border-color: #83e6c4; }
QCheckBox:disabled { color: #9bb0c3; }
QCheckBox::indicator:disabled { border-color: #9bb0c3; }
QMenu { border: 1px solid #2c3e52; padding: 3px; }
QMenu::item { padding: 3px 12px; border-radius: 4px; }
QMenu::item:selected { background: #223a48; color: #83e6c4; }
QMenu::item:disabled { color: #9bb0c3; }
QMenu::separator { height: 1px; background: #2c3e52; margin: 2px 6px; }
QFrame#datePopup { background:#141f2e; border:1px solid #2c3e52; border-radius:12px; }
QCalendarWidget QWidget { background:#141f2e; color:#edf4fa; }
QCalendarWidget QToolButton { border:none; padding:6px; border-radius:6px; color:#edf4fa; }
QCalendarWidget QToolButton:hover { background:#223a48; }
QCalendarWidget QAbstractItemView { background:#141f2e; color:#edf4fa; selection-background-color:#223a48; selection-color:#83e6c4; border:none; outline:0; }
QFrame#settingsSidebar { background: #141f2e; border: 1px solid #2c3e52; border-radius: 14px; }
QListWidget#settingsNav { background: transparent; color: #edf4fa; border: none; outline: 0; }
QListWidget#settingsNav::item { padding: 12px 8px; margin: 3px 0; border-radius: 8px; }
QListWidget#settingsNav::item:selected { background: #223a48; color: #83e6c4; }
"""
STYLE = STYLE.replace("CHEVRON_ICON", (ROOT / "assets/chevron-dark.svg").as_posix())
STYLE = STYLE.replace("CHECK_ICON", (ROOT / "assets/check-dark.svg").as_posix())


def apply_color_scheme(app, scheme):
    mode = app.property("resttimer_color_mode") or "system"
    if mode != "system":
        scheme = Qt.ColorScheme.Light if mode == "light" else Qt.ColorScheme.Dark
    style = STYLE
    if scheme == Qt.ColorScheme.Light:
        colors = {"#0d1521": "#f4f7fa", "#edf4fa": "#1a2838", "#141f2e": "#ffffff",
                  "#2c3e52": "#cdd8e2", "#1e2e40": "#e7edf3", "#223a48": "#dbeee8",
                  "#ffa49b": "#ae2a2a", "#83e6c4": "#11705b", "#a4f2d8": "#16836b", "#9bb0c3": "#536778", "#0c1623": "#eaf0f5", "#102c30": "#dbeee8"}
        style = re.sub(r"#[a-fA-F0-9]{6}", lambda match: colors.get(match.group(), match.group()), style)
        style = style.replace("check-dark.svg", "check-light.svg").replace("chevron-dark.svg","chevron-light.svg")
        style += "\nQPushButton#primary { color: white; } QProgressBar { color: #11705b; } QProgressBar::chunk { background: #dbeee8; }"
    app.setStyleSheet(style)
    for widget in app.topLevelWidgets():
        ui_icons.apply(widget)


def verify_ui_assets():
    for name in ("check-dark.svg", "check-light.svg", "git.png"):
        if QIcon(str(ROOT / "assets" / name)).pixmap(16, 16).isNull():
            raise RuntimeError("Missing checkbox indicator or SVG support: " + name)


def verify_language_fonts(app):
    """Smoke checks use shaped glyphs, including fallback fonts, to catch tofu."""
    for sample in ("استراحت", "استراحة", "工作", "休憩", "휴식", "विराम", "Работа"):
        layout = QTextLayout(sample, app.font())
        layout.beginLayout()
        line = layout.createLine()
        line.setLineWidth(2000)
        layout.endLayout()
        runs = layout.glyphRuns()
        if not runs or any(glyph == 0 for run in runs for glyph in run.glyphIndexes()):
            raise RuntimeError("Missing system font coverage for a supported interface script.")


def config_root():
    return Path(os.environ.get("XDG_CONFIG_HOME", Path.home() / ".config"))


def read_preferences():
    try:
        data = json.loads((config_root() / "resttimer/settings.json").read_text(encoding="utf-8"))
        if (isinstance(data, dict) and type(data.get("work_seconds")) is int
                and type(data.get("break_seconds")) is int
                and 1 <= data["work_seconds"] <= 86400 and 1 <= data["break_seconds"] <= 7200):
            return {"work_seconds": data["work_seconds"], "break_seconds": data["break_seconds"],
                    "language": i18n.preference(data.get("language")),
                    "calendar": data.get("calendar") if data.get("calendar") in ("gregorian","persian","hijri") else "auto",
                    "color_mode": data.get("color_mode") if data.get("color_mode") in ("light","dark") else "system"}
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
    widget.setAlignment(Qt.AlignCenter if center else Qt.AlignAbsolute | (Qt.AlignRight if i18n.is_rtl() else Qt.AlignLeft) | Qt.AlignVCenter)
    widget.setLayoutDirection(Qt.RightToLeft if i18n.is_rtl() else Qt.LeftToRight)
    if name in ("title","sectionTitle"):
        widget.setWordWrap(True)
    return widget


class DurationEditor(QFrame):
    def __init__(self, title, seconds, max_hours):
        super().__init__()
        self.setObjectName("section")
        layout = QVBoxLayout(self)
        layout.setContentsMargins(12, 10, 12, 10)
        heading = label(title)
        heading.setFont(QFont("DejaVu Sans", 11, QFont.Bold))
        layout.addWidget(heading)
        fields_container = QWidget()
        fields_container.setLayoutDirection(Qt.LeftToRight)
        fields = QHBoxLayout(fields_container)
        fields.setContentsMargins(0, 0, 0, 0)
        fields.setDirection(QHBoxLayout.LeftToRight)
        fields.setSpacing(14)
        self.parts = []
        for caption, value, maximum in (
            (tr("hours"), seconds // 3600, max_hours),
            (tr("minutes"), seconds // 60 % 60, 59),
            (tr("seconds"), seconds % 60, 59),
        ):
            column = QVBoxLayout()
            spin = QSpinBox()
            spin.setAccessibleName(title + " – " + caption)
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
        layout.addWidget(fields_container)

    def seconds(self):
        for part in self.parts:
            part.interpretText()
        h, m, s = (part.value() for part in self.parts)
        return h * 3600 + m * 60 + s


class UnsavedChangesDialog(QDialog):
    def __init__(self, parent):
        super().__init__(parent)
        self.setObjectName("UnsavedChangesDialog")
        self.setWindowTitle(tr("unsaved_title"))
        self.setWindowIcon(parent.controller.icon)
        self.setMinimumWidth(480)
        self.resize(520, 220)
        layout = QVBoxLayout(self)
        layout.setContentsMargins(12, 12, 12, 12)
        heading = label(tr("unsaved_title"), "title")
        heading.setWordWrap(True)
        layout.addWidget(heading)
        message = label(tr("unsaved_message"), "muted")
        message.setWordWrap(True)
        layout.addWidget(message, 1)
        actions = QHBoxLayout()
        self.save = QPushButton(tr("save_changes"))
        self.save.setObjectName("primary")
        self.save.clicked.connect(self.accept)
        self.save.setDefault(True)
        self.discard = QPushButton(tr("discard_changes"))
        self.discard.clicked.connect(lambda: self.done(2))
        actions.addWidget(self.save)
        actions.addWidget(self.discard)
        layout.addLayout(actions)
        ui_icons.apply(self)


class WorkStartDialog(QDialog):
    def __init__(self, controller):
        super().__init__(controller.settings)
        self.setWindowTitle(tr("new_session"))
        self.setMinimumWidth(540)
        layout = QVBoxLayout(self)
        layout.setContentsMargins(12, 12, 12, 12)
        layout.setSpacing(12)
        layout.addWidget(label(tr("new_session"), "title"))
        layout.addWidget(label(tr("task_name")))
        self.task = QLineEdit(controller.task_name or "")
        self.task.setMaxLength(120)
        self.task.setAccessibleName(tr("task_name"))
        layout.addWidget(self.task)
        self.duration = DurationEditor(tr("work_duration"), controller.session_seconds or controller.preferences["work_seconds"], 24)
        layout.addWidget(self.duration)
        self.error = label("", "error")
        self.error.setWordWrap(True)
        layout.addWidget(self.error)
        begin = QPushButton(tr("new_session"))
        begin.setObjectName("primary")
        begin.clicked.connect(self.begin)
        layout.addWidget(begin)
        ui_icons.apply(self)

    def begin(self):
        if not self.task.text().strip():
            self.error.setText(tr("task_required"))
        elif not 1 <= self.duration.seconds() <= 86400:
            self.error.setText(tr("work_validation"))
        else:
            self.accept()


from dashboard import WorkReport


class SettingsDialog(QDialog):
    NAV_PAGES = ("report", "general", "updates", "history", "about")
    def __init__(self, controller, update_panel=None, draft=None):
        super().__init__()
        self.controller = controller
        self.setWindowTitle("RestTimer — " + tr("preferences_title"))
        self.setWindowIcon(controller.icon)
        self.setMinimumSize(780, 660)
        self.current_page = "general"
        outer = QHBoxLayout(self)
        outer.setContentsMargins(12, 12, 12, 12)
        outer.setSpacing(10)
        sidebar = QFrame()
        self.sidebar = sidebar
        self.sidebar_collapsed = False
        sidebar.setObjectName("settingsSidebar")
        sidebar.setFixedWidth(180)
        side = QVBoxLayout(sidebar)
        side.setContentsMargins(8, 12, 8, 8)
        self.sidebar_toggle = QPushButton()
        self.sidebar_toggle.setObjectName("sidebar_toggle")
        self.sidebar_toggle.setProperty("ui_glyph", "menu")
        self.sidebar_toggle.setFixedSize(32,32)
        self.sidebar_toggle.setStyleSheet("padding:4px; min-height:0;")
        self.sidebar_toggle.clicked.connect(lambda: self.set_sidebar_collapsed(not self.sidebar_collapsed, animate=True))
        side.addWidget(self.sidebar_toggle,0,Qt.AlignAbsolute | (Qt.AlignRight if i18n.is_rtl() else Qt.AlignLeft))
        self.sidebar_brand = QWidget()
        self.sidebar_brand.setFixedHeight(76)
        brand_layout = QVBoxLayout(self.sidebar_brand)
        brand_layout.setContentsMargins(0,0,0,0)
        icon = label("", center=True)
        icon.setPixmap(controller.icon.pixmap(36, 36))
        icon.setFixedHeight(36)
        brand_layout.addWidget(icon)
        self.sidebar_name=label("RestTimer", center=True)
        brand_layout.addWidget(self.sidebar_name)
        brand_layout.addStretch(1)
        side.addWidget(self.sidebar_brand)
        self.navigation = QListWidget()
        self.navigation.setObjectName("settingsNav")
        self.navigation.setWordWrap(False)
        self.navigation.setItemDelegate(rtl_ui.NavigationDelegate(self.navigation))
        self.navigation.setHorizontalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        self.navigation.setAccessibleName(tr("settings_navigation"))
        for key in ("dashboard", "general", "updates_tab", "version_history", "about"):
            self.navigation.addItem(tr(key))
        side.addWidget(self.navigation, 1)
        self.sidebar_version = label("v" + controller.version, "muted", center=True)
        self.sidebar_version.setFixedHeight(28)
        side.addWidget(self.sidebar_version)
        outer.addWidget(sidebar)
        self.pages = QStackedWidget()
        outer.addWidget(self.pages, 1)
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.NoFrame)
        body = QWidget()
        layout = QVBoxLayout(body)
        scroll.setWidget(body)
        self.pages.addWidget(scroll)
        layout.setContentsMargins(12, 12, 12, 12)
        scroll.setHorizontalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        scroll.setVerticalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
        layout.setSpacing(8)
        self.general_sections={}
        def section(key):
            frame=QFrame()
            frame.setObjectName("section")
            frame.setAccessibleName(tr(key))
            section_layout=QVBoxLayout(frame)
            section_layout.setContentsMargins(10,6,10,6)
            section_layout.setSpacing(4)
            section_layout.addWidget(label(tr(key),"sectionTitle"))
            self.general_sections[key]=frame
            return frame,section_layout
        time_section,time_layout=section("time_settings")
        locale_section,locale_layout=section("language_region")
        appearance_section,appearance_layout=section("appearance")
        behavior_section,behavior_layout=section("app_behavior")
        layout.addWidget(time_section)
        middle=QHBoxLayout()
        middle.setSpacing(8)
        middle.addWidget(locale_section,3)
        middle.addWidget(appearance_section,2)
        layout.addLayout(middle)
        layout.addWidget(behavior_section)
        self.work = DurationEditor(tr("work_short"), controller.preferences["work_seconds"], 24)
        self.rest = DurationEditor(tr("break_short"), controller.preferences["break_seconds"], 2)
        durations=QHBoxLayout()
        durations.setSpacing(6)
        durations.addWidget(self.work,1)
        durations.addWidget(self.rest,1)
        time_layout.addLayout(durations)
        for editor in (self.work,self.rest):
            editor.layout().setContentsMargins(6,4,6,4)
            editor.findChild(QHBoxLayout).setSpacing(6)
            editor.layout().setSpacing(2)
            for spin in editor.parts:
                spin.setMinimumSize(40,26)
                spin.setStyleSheet("font-size:22px;")
                spin.parentWidget().layout().setContentsMargins(4,2,4,2)
        self.startup = QCheckBox(tr("startup"))
        self.startup.setChecked(startup_path().exists())
        behavior_layout.addWidget(self.startup)
        self.daily = QCheckBox(tr("daily_check"))
        self.daily.setChecked(controller.updater.state["daily_enabled"])
        self.daily.toggled.connect(self.toggle_daily)
        behavior_layout.addWidget(self.daily)
        timer_controls = QHBoxLayout()
        self.start_timer = QPushButton(tr("start"))
        self.start_timer.setObjectName("primary")
        self.start_timer.clicked.connect(controller.start_timer)
        self.stop_timer = QPushButton(tr("stop"))
        self.stop_timer.clicked.connect(controller.stop_timer)
        self.timer_status = label("", "muted")
        self.timer_status.setWordWrap(True)
        timer_controls.addWidget(self.start_timer)
        timer_controls.addWidget(self.stop_timer)
        self.reset_timer = QPushButton(tr("reset_timer"))
        self.reset_timer.clicked.connect(controller.reset_timer)
        timer_controls.addWidget(self.reset_timer)
        time_layout.addLayout(timer_controls)
        time_layout.addWidget(self.timer_status)
        languages = QHBoxLayout()
        languages.addWidget(label(tr("language")))
        self.language = QComboBox()
        self.language.setAccessibleName(tr("language"))
        self.language.setLayoutDirection(Qt.LeftToRight)
        self.language.addItem(tr("system_language"), "auto")
        for code in i18n.SUPPORTED:
            self.language.addItem(i18n.native_name(code), code)
        self.language.setCurrentIndex(self.language.findData(controller.preferences.get("language", "auto")))
        languages.addWidget(self.language, 1)
        locale_layout.addLayout(languages)
        language_hint = label(tr("language_hint"), "muted")
        language_hint.setWordWrap(True)
        language_hint.hide()
        self.calendar_choice = QComboBox()
        self.theme_choice = QComboBox()
        for box,key,values,keys in (
            (self.calendar_choice,"calendar",("auto","gregorian","persian","hijri"),("system_calendar","gregorian","persian","hijri")),
            (self.theme_choice,"color_mode",("system","light","dark"),("system_theme","light_theme","dark_theme"))):
            row=QHBoxLayout()
            if key=="calendar": row.addWidget(label(tr("calendar")))
            box.setAccessibleName(tr("calendar" if key=="calendar" else "appearance"))
            for value,text in zip(values,keys): box.addItem(tr(text),value)
            box.setCurrentIndex(box.findData(controller.preferences.get(key,values[0])))
            row.addWidget(box,1)
            (locale_layout if key=="calendar" else appearance_layout).addLayout(row)
        appearance_layout.addStretch(1)
        self.calendar_choice.currentIndexChanged.connect(self.preview_calendar)
        self.theme_choice.currentIndexChanged.connect(self.preview_theme)
        self.note = label(tr("duration_note"), "muted")
        self.note.setWordWrap(True)

        self.update_notice = QPushButton()
        self.update_notice.setVisible(False)
        self.update_notice.clicked.connect(controller.check_updates)
        layout.addWidget(self.update_notice)
        buttons = QHBoxLayout()
        buttons.addWidget(self.note,1)
        self.save = QPushButton(tr("save"))
        self.save.setObjectName("primary")
        self.save.clicked.connect(self.save_durations)
        self.save.setMinimumWidth(150)
        buttons.addWidget(self.save)
        layout.addLayout(buttons)
        layout.addStretch(1)
        self.update_panel = update_panel or UpdatePanel(controller)
        controller.update_dialog = self.update_panel
        self.pages.addWidget(self.update_panel)
        self.update_panel.daily_changed.connect(self.daily.setChecked)
        self.report = WorkReport(controller)
        self.pages.addWidget(self.report)
        self.pages.addWidget(self.about_page())
        self.navigation.currentRowChanged.connect(self.select_index)
        self.navigation.setCurrentRow(0)
        self.remember_saved_settings()
        screen = controller.app.primaryScreen()
        self.resize(min(1020, screen.availableGeometry().width() - 40), min(800, screen.availableGeometry().height() - 48))
        self.set_sidebar_collapsed(False)
        if draft:
            self.restore_draft(draft)
        self.language.currentIndexChanged.connect(self.preview_language)
        ui_icons.apply(self)
        rtl_ui.right_scrollbars(self)

    def render_sidebar(self, icons_only):
        self.sidebar_name.setVisible(not icons_only)
        # Reserve the same brand and footer heights in both states.
        self.sidebar_brand.setVisible(True)
        self.sidebar_version.setVisible(True)
        self.sidebar_version.setText("" if icons_only else "v"+self.controller.version)
        action=tr("expand_sidebar" if self.sidebar_collapsed else "collapse_sidebar")
        self.sidebar_toggle.setToolTip(action)
        self.sidebar_toggle.setAccessibleName(action)
        for index,key in enumerate(("dashboard","general","updates_tab","version_history","about")):
            item=self.navigation.item(index)
            item.setText("" if icons_only else tr(key))
            item.setToolTip(tr(key))
            item.setData(Qt.AccessibleTextRole,tr(key))
            item.setTextAlignment(Qt.AlignAbsolute | Qt.AlignVCenter | (Qt.AlignRight if i18n.is_rtl() else Qt.AlignLeft))
        self.navigation.viewport().update()

    def set_sidebar_collapsed(self, collapsed, animate=False):
        current=self.sidebar.width()
        before=self.pages.grab() if animate else None
        if hasattr(self,"content_transition") and self.content_transition is not None:
            before=self.content_transition.grab() if animate else None
            self.content_transition.hide();self.content_transition.deleteLater();self.content_transition=None
        if hasattr(self,"sidebar_animation"):
            self.sidebar_animation.stop()
            self.sidebar_animation.deleteLater()
        self.sidebar_collapsed=collapsed
        target=56 if collapsed else max(180,min(260,max(self.navigation.fontMetrics().horizontalAdvance(tr(key)) for key in ("dashboard","general","updates_tab","version_history","about"))+64))
        self.sidebar.layout().setContentsMargins(4,12,4,8)
        self.sidebar.setMinimumWidth(0)
        self.sidebar.setMaximumWidth(16777215)
        if not animate:
            self.sidebar.setFixedWidth(target)
            self.render_sidebar(collapsed)
            return
        self.sidebar.setFixedWidth(target);self.layout().activate()
        page_layout=self.pages.currentWidget().layout()
        if page_layout is not None: page_layout.activate()
        after=self.pages.grab()
        self.sidebar.setMinimumWidth(0)
        self.sidebar.setMaximumWidth(current);self.layout().activate()
        self.content_transition=ResizeTransition(self.pages,before,after)
        self.content_transition.setGeometry(self.pages.rect());self.content_transition.show();self.content_transition.raise_()
        self.render_sidebar(True)
        self.sidebar_animation=QPropertyAnimation(self.sidebar,b"maximumWidth",self)
        self.sidebar_animation.setDuration(220)
        self.sidebar_animation.setStartValue(current)
        self.sidebar_animation.setEndValue(target)
        self.sidebar_animation.setEasingCurve(QEasingCurve.OutCubic)
        def frame(value):
            self.layout().activate()
            if self.content_transition is not None:
                self.content_transition.setGeometry(self.pages.rect())
                span=target-current
                self.content_transition.progress=1 if not span else (value-current)/span
                self.content_transition.update()
        self.sidebar_animation.valueChanged.connect(frame)
        def finish():
            if self.content_transition is not None:
                self.content_transition.hide();self.content_transition.deleteLater();self.content_transition=None
            self.sidebar.setFixedWidth(target)
            self.render_sidebar(self.sidebar_collapsed)
        self.sidebar_animation.finished.connect(finish)
        self.sidebar_animation.start()

    def preview_calendar(self):
        if hasattr(self,"report"):
            self.report.day.preference=self.calendar_choice.currentData()
            self.report.day.refresh_caption()

    def preview_theme(self):
        self.controller.app.setProperty("resttimer_color_mode",self.theme_choice.currentData())
        apply_color_scheme(self.controller.app,self.controller.app.styleHints().colorScheme())

    def preview_language(self, index):
        if index >= 0 and self.controller.settings is self and not self.controller.quitting:
            self.controller.preview_language(self.language.itemData(index))

    def capture_draft(self, previous):
        return {"parts": [[(part.value(), part.lineEdit().text()) for part in editor.parts] for editor in (self.work, self.rest)],
                "language": self.language.currentData(), "calendar": self.calendar_choice.currentData(), "color_mode": self.theme_choice.currentData(), "daily": self.daily.isChecked(), "startup": self.startup.isChecked(),
                "saved_preferences": self.saved_preferences.copy(), "saved_daily": self.saved_daily, "saved_startup": self.saved_startup,
                "note": self.note.text(), "source_language": previous, "page": self.current_page,
                "geometry": self.geometry(), "sidebar_collapsed": self.sidebar_collapsed, "report_date": self.report.day.date(), "chart_type": self.report.chart_type.currentIndex(), "scroll": self.pages.widget(0).verticalScrollBar().value()}

    def restore_draft(self, draft):
        self.saved_preferences = draft["saved_preferences"]
        self.saved_daily, self.saved_startup = draft["saved_daily"], draft["saved_startup"]
        for editor, parts in zip((self.work, self.rest), draft["parts"]):
            for part, (value, raw) in zip(editor.parts, parts):
                part.setValue(value)
                part.lineEdit().setText(raw)
        self.language.setCurrentIndex(self.language.findData(draft["language"]))
        self.calendar_choice.setCurrentIndex(self.calendar_choice.findData(draft["calendar"]))
        self.theme_choice.setCurrentIndex(self.theme_choice.findData(draft["color_mode"]))
        self.preview_calendar()
        self.daily.setChecked(draft["daily"])
        self.startup.setChecked(draft["startup"])
        self.note.setText(i18n.translate_text(draft["note"], draft["source_language"]))
        self.setGeometry(draft["geometry"])
        self.report.day.setDate(draft["report_date"])
        self.report.chart_type.setCurrentIndex(draft["chart_type"])
        self.set_sidebar_collapsed(draft["sidebar_collapsed"])
        self.select_page(draft["page"])

    def about_page(self):
        scroll = QScrollArea()
        scroll.setWidgetResizable(True)
        scroll.setFrameShape(QFrame.NoFrame)
        page = QFrame()
        page.setObjectName("section")
        layout = QVBoxLayout(page)
        layout.setContentsMargins(12, 12, 12, 12)
        layout.setSpacing(12)
        logo = label("", center=True)
        logo.setPixmap(self.controller.icon.pixmap(48, 48))
        layout.addWidget(logo)
        layout.addWidget(label("RestTimer", "title", center=True))
        layout.addWidget(label(tr("installed", self.controller.version), "muted", center=True))
        description = label(tr("about_description"), center=True)
        description.setWordWrap(True)
        layout.addWidget(description)
        layout.addWidget(label(tr("author") + ": Saleh Ghotbani", center=True))
        layout.addWidget(label(tr("license") + ": MIT", center=True))
        project = QPushButton(tr("project_site"))
        project.setObjectName("project_site")
        project.setIcon(QIcon(str(ROOT / "assets/git.png")))
        project.setIconSize(QSize(22, 22))
        project.clicked.connect(self.open_project)
        layout.addWidget(project)
        self.license_text = QTextEdit()
        self.license_text.setObjectName("license_text")
        self.license_text.setReadOnly(True)
        self.license_text.setAcceptRichText(False)
        self.license_text.setLayoutDirection(Qt.LeftToRight)
        self.license_text.setAccessibleName("MIT License")
        self.license_text.setPlainText((ROOT / "LICENSE").read_text(encoding="utf-8"))
        self.license_text.selectAll()
        self.license_text.setAlignment(Qt.AlignLeft | Qt.AlignAbsolute)
        cursor = self.license_text.textCursor()
        cursor.movePosition(QTextCursor.Start)
        self.license_text.setTextCursor(cursor)
        self.license_text.setMinimumHeight(180)
        layout.addWidget(self.license_text, 1)
        scroll.setWidget(page)
        return scroll

    def open_project(self):
        if not QDesktopServices.openUrl(QUrl("https://github.com/salehghotbani/RestTimer")):
            QMessageBox.information(self, "RestTimer", tr("open_link_error"))

    def toggle_daily(self, enabled):
        panel = getattr(self, "update_panel", None)
        if panel:
            panel.daily.setChecked(enabled)

    def ensure_update_panel(self):
        if not self.update_panel.closed:
            return
        previous = self.update_panel
        self.pages.removeWidget(previous)
        previous.setParent(None)
        previous.deleteLater()
        self.update_panel = UpdatePanel(self.controller, check_on_show=previous.check_on_show)
        self.controller.update_dialog = self.update_panel
        self.update_panel.daily_changed.connect(self.daily.setChecked)
        self.pages.insertWidget(1, self.update_panel)

    def select_index(self, index):
        self.current_page = self.NAV_PAGES[index]
        self.pages.setCurrentIndex({"general":0,"updates":1,"history":1,"report":2,"about":3}[self.current_page])
        if self.current_page == "report":
            self.report.refresh()
        if self.current_page in ("updates", "history"):
            self.update_panel.set_page(self.current_page == "history")
            if self.isVisible():
                self.update_panel.ensure_loaded()

    def select_page(self, page):
        self.ensure_update_panel()
        self.navigation.setCurrentRow(self.NAV_PAGES.index(page))
        self.select_index(self.navigation.currentRow())

    def showEvent(self, event):
        self.ensure_update_panel()
        self.select_index(self.navigation.currentRow())
        super().showEvent(event)

    def remember_saved_settings(self):
        self.saved_preferences = self.controller.preferences.copy()
        self.saved_daily = self.controller.updater.state["daily_enabled"]
        self.saved_startup = self.startup.isChecked()

    def has_pending_changes(self):
        # Include uncommitted text in spin boxes, even when its value is unchanged.
        for editor in (self.work, self.rest):
            for part in editor.parts:
                if not part.hasAcceptableInput():
                    return True
        return (self.work.seconds() != self.saved_preferences["work_seconds"]
                or self.rest.seconds() != self.saved_preferences["break_seconds"]
                or self.language.currentData() != self.saved_preferences.get("language", "auto")
                or self.calendar_choice.currentData() != self.saved_preferences.get("calendar","auto")
                or self.theme_choice.currentData() != self.saved_preferences.get("color_mode","system")
                or self.daily.isChecked() != self.saved_daily
                or self.startup.isChecked() != self.saved_startup)

    def restore_saved_settings(self):
        for editor, key in ((self.work, "work_seconds"), (self.rest, "break_seconds")):
            seconds = self.saved_preferences[key]
            for part, value in zip(editor.parts, (seconds // 3600, seconds // 60 % 60, seconds % 60)):
                part.setValue(value)
                part.lineEdit().setText(part.textFromValue(value))
        self.language.blockSignals(True)
        self.language.setCurrentIndex(self.language.findData(self.saved_preferences.get("language", "auto")))
        self.language.blockSignals(False)
        self.calendar_choice.setCurrentIndex(self.calendar_choice.findData(self.saved_preferences.get("calendar","auto")))
        self.theme_choice.setCurrentIndex(self.theme_choice.findData(self.saved_preferences.get("color_mode","system")))
        self.daily.setChecked(self.saved_daily)
        self.startup.setChecked(self.saved_startup)

    def confirm_pending_changes(self):
        if not self.has_pending_changes():
            return True
        choice = UnsavedChangesDialog(self).exec()
        if choice == QDialog.Accepted:
            if self.save_durations(closing=True):
                return True
            self.select_page("general")
            return False
        if choice == 2:
            self.restore_saved_settings()
            self.controller.apply_language(keep_visible=False)
            return True
        return False

    def save_durations(self, checked=False, closing=False):
        work, rest = self.work.seconds(), self.rest.seconds()
        if not 1 <= work <= 86400 or not 1 <= rest <= 7200:
            self.note.setText(tr("combined_validation"))
            return False
        if self.controller.breaking:
            self.note.setText(tr("save_during_break"))
            return False
        updated = {"work_seconds": work, "break_seconds": rest, "language": self.language.currentData(), "calendar": self.calendar_choice.currentData(), "color_mode": self.theme_choice.currentData()}
        try:
            if self.startup.isChecked() != self.saved_startup:
                set_startup(self.startup.isChecked())
            atomic_write(config_root() / "resttimer/settings.json", json.dumps(updated))
        except OSError:
            self.note.setText(tr("config_error"))
            try:
                set_startup(self.saved_startup)
            except OSError:
                pass
            return False
        durations_changed = any(updated[key] != self.controller.preferences[key] for key in ("work_seconds", "break_seconds"))
        self.controller.preferences = updated
        self.controller.updater.state["daily_enabled"] = self.daily.isChecked()
        self.controller.updater.save()
        self.controller.startup_action.setChecked(self.startup.isChecked())
        self.remember_saved_settings()
        if durations_changed:
            self.controller.begin_work()
        self.controller.apply_language(keep_visible=not closing)
        self.controller.settings.note.setText(tr("settings_saved"))
        return True

    def closeEvent(self, event):
        if self.update_panel.process is not None:
            self.update_panel.status.setText(tr("install_busy"))
            self.select_page("updates")
            event.ignore()
            return
        if not self.confirm_pending_changes():
            event.ignore()
            return
        self.update_panel.request_close()
        if not self.controller.tray.isSystemTrayAvailable():
            self.controller.quit()
        event.accept()

    def reject(self):
        self.close()


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
        self.card.setLayoutDirection(Qt.LeftToRight)
        self.card.setObjectName("breakCard")
        self.card.setLayoutDirection(Qt.LeftToRight)
        row = QHBoxLayout(self.card)
        row.setContentsMargins(12, 12, 12, 12)
        row.setSpacing(12)
        controls = QWidget()
        self.controls = controls
        controls_column = QVBoxLayout(controls)
        controls_column.setContentsMargins(0, 0, 0, 0)
        controls_column.setSpacing(6)
        timer_controls = QHBoxLayout()
        timer_controls.setSpacing(6)
        self.start = QPushButton(tr("start"))
        self.start.setObjectName("primary")
        self.start.clicked.connect(start)
        self.stop = QPushButton(tr("stop"))
        self.stop.clicked.connect(stop)
        timer_controls.addWidget(self.start)
        timer_controls.addWidget(self.stop)
        self.reset = QPushButton(tr("reset_timer"))
        timer_controls.addWidget(self.reset)
        controls_column.addLayout(timer_controls)
        self.skip = QPushButton(tr("skip"))
        self.skip.clicked.connect(finish)
        controls_column.addWidget(self.skip)
        for button,key in ((self.start,"start"),(self.stop,"stop"),(self.reset,"reset_timer"),(self.skip,"skip")):
            button.setStyleSheet("font-size:12px; padding:5px 8px;")
            button.setAccessibleName(tr(key))
            button.setToolTip(tr(key))
        row.addWidget(controls)
        timer_column = QVBoxLayout()
        timer_column.setSpacing(0)
        self.countdown = label("", "timer", center=True)
        self.countdown.setLayoutDirection(Qt.LeftToRight)
        self.countdown.setMinimumWidth(160)
        timer_column.addWidget(self.countdown)
        self.timer_caption = label(tr("remaining"), "muted", center=True)
        self.timer_caption.setWordWrap(True)
        timer_column.addWidget(self.timer_caption)
        row.addLayout(timer_column)
        text = QVBoxLayout()
        self.heading = label(tr("break_heading"))
        self.heading.setFont(QFont("DejaVu Sans", 13, QFont.Bold))
        self.heading.setWordWrap(True)
        text.addWidget(self.heading)
        self.caption = label(tr("break_caption"), "muted")
        self.caption.setWordWrap(True)
        text.addWidget(self.caption)
        row.addLayout(text, 1)
        ui_icons.apply(self)

    def layout_card(self):
        width = min(920, self.width() - 24)
        compact = width < 680
        for button,key in ((self.start,"start"),(self.stop,"stop"),(self.reset,"reset_timer")):
            button.setText("" if compact else tr(key))
        self.controls.setFixedWidth(sum(button.sizeHint().width() for button in (self.start,self.stop,self.reset))+12)
        self.caption.setVisible(not compact)
        self.heading.setVisible(width >= 420)
        self.timer_caption.setVisible(not compact)
        needed=self.card.layout().totalHeightForWidth(width)
        height=max(96,needed,self.card.layout().minimumSize().height())
        self.card.setGeometry((self.width()-width)//2,16,width,height)

    def resizeEvent(self,event):
        self.layout_card()
        super().resizeEvent(event)

    def set_running(self, running):
        self.start.setEnabled(not running)
        self.stop.setEnabled(running)
        self.heading.setText(tr("break_heading") if running else tr("break_paused"))
        self.timer_caption.setText(tr("remaining") if running else tr("timer_paused"))
        self.layout_card()

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
        app.setProperty("resttimer_color_mode", self.preferences.get("color_mode","system"))
        apply_color_scheme(app,app.styleHints().colorScheme())
        i18n.use(self.preferences.get("language", "auto"), QLocale.system().uiLanguages())
        app.setLayoutDirection(Qt.RightToLeft if i18n.is_rtl() else Qt.LeftToRight)
        self.icon = QIcon(str(ROOT / "assets/RestTimer.png"))
        self.task_name = None
        self.session_seconds = None
        self.segment_id = None
        self.segment_started = self.segment_clock = None
        self.segment_elapsed = 0
        self.journal = work_tracking.Journal(config_root() / "resttimer/work-log")
        self.prompting = False
        self.breaking = False
        self.paused_at = time.monotonic()
        self.quitting = False
        self.overlays = []
        self.display_signature = None
        self.root, self.version = ROOT, VERSION
        self.updater = update_core.Updater(config_root() / "resttimer/updates")
        self.workers = []
        self.update_dialog = None
        self.checking_updates = False
        self.instance = None
        self.deadline = self.paused_at + self.preferences["work_seconds"]
        self.menu = QMenu()
        self.settings_action = self.menu.addAction(tr("dashboard"), self.show_dashboard)
        self.menu.addSeparator()
        self.start_timer_action = self.menu.addAction(tr("start"), self.start_timer)
        self.stop_timer_action = self.menu.addAction(tr("stop"), self.stop_timer)
        self.start_action = self.menu.addAction(tr("start_break"), self.begin_break)
        self.reset_timer_action = self.menu.addAction(tr("reset_timer"), self.reset_timer)
        self.menu.addSeparator()
        self.status = self.menu.addAction("RestTimer")
        self.status.setEnabled(False)
        self.startup_action = QAction(tr("startup"), self.menu)
        self.startup_action.setCheckable(True)
        self.startup_action.setChecked(startup_path().exists())
        self.startup_action.triggered.connect(self.toggle_startup)
        self.menu.addAction(self.startup_action)
        self.update_action = self.menu.addAction(tr("updates_menu"), self.check_updates)
        self.quit_action = self.menu.addAction(tr("quit"), self.quit)
        self.menu_minimum_size = self.menu.minimumSize()
        self.menu_maximum_size = self.menu.maximumSize()
        self.menu.aboutToShow.connect(self.prepare_tray_menu)
        self.menu.aboutToHide.connect(lambda: QTimer.singleShot(0,self.release_tray_menu))
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

    def prepare_tray_menu(self):
        self.menu.setMinimumSize(self.menu_minimum_size)
        self.menu.setMaximumSize(self.menu_maximum_size)
        release = self.updater.state["latest"]
        if update_core.stable(release) and version_tuple(release["tag_name"]) > version_tuple(VERSION):
            self.update_action.setText(tr("update_available",release["tag_name"]))
        self.menu.setStyleSheet("")
        self.menu.ensurePolished()
        self.menu.sizeHint()
        screen = self.app.screenAt(QCursor.pos()) or self.app.primaryScreen()
        available = screen.availableGeometry().size() - QSize(4,4)
        row_width = min(max((self.menu.actionGeometry(action).width() for action in self.menu.actions()),default=0), available.width()-8)
        # Match the app's 12px horizontal item padding and lock hit targets too.
        content_width = max(1,row_width-24)
        self.menu.setStyleSheet("QMenu::item { min-width: %dpx; max-width: %dpx; }" % (content_width,content_width))
        self.menu.setFixedSize(self.menu.sizeHint().boundedTo(available))

    def release_tray_menu(self):
        if not self.menu.isVisible():
            self.menu.setMinimumSize(self.menu_minimum_size)
            self.menu.setMaximumSize(self.menu_maximum_size)

    def tray_activated(self, reason):
        if reason == QSystemTrayIcon.Trigger:
            self.menu.close()
            self.show_dashboard()

    def apply_language(self, keep_visible=True):
        self.set_interface_language(self.preferences.get("language", "auto"), keep_visible)

    def preview_language(self, selected):
        self.set_interface_language(selected)

    def set_interface_language(self, selected, keep_visible=True):
        previous = i18n.code
        i18n.use(selected, QLocale.system().uiLanguages())
        if previous == i18n.code:
            return
        self.app.setLayoutDirection(Qt.RightToLeft if i18n.is_rtl() else Qt.LeftToRight)
        for action, key in ((self.start_timer_action, "start"), (self.stop_timer_action, "stop"),
                            (self.start_action, "start_break"), (self.reset_timer_action, "reset_timer"),
                            (self.settings_action, "dashboard"),
                            (self.startup_action, "startup"), (self.update_action, "updates_menu"),
                            (self.quit_action, "quit")):
            action.setText(tr(key))
        previous_settings = self.settings
        draft = previous_settings.capture_draft(previous)
        visible = previous_settings.isVisible()
        current_page = previous_settings.current_page
        panel = previous_settings.update_panel
        previous_settings.pages.removeWidget(panel)
        panel.setParent(None)
        self.settings = SettingsDialog(self, update_panel=panel, draft=draft)
        self.settings.select_page(current_page)
        previous_settings.hide()
        if visible and keep_visible:
            self.settings.show()
            self.settings.pages.widget(0).verticalScrollBar().setValue(draft["scroll"])
            if current_page == "general":
                self.settings.language.setFocus()
        previous_settings.deleteLater()
        if self.update_dialog is not None and not self.update_dialog.closed:
            self.update_dialog.retranslate(previous)
        self.tick()

    def show_dashboard(self):
        self.show_page("report")

    def show_settings(self):
        self.show_page("general")

    def show_page(self, page):
        if not self.breaking:
            self.settings.select_page(page)
            self.settings.show()
            self.settings.raise_()
            self.settings.activateWindow()

    def toggle_startup(self, enabled):
        try:
            set_startup(enabled)
        except OSError:
            QMessageBox.warning(self.settings, "RestTimer", tr("startup_error"))
        actual = startup_path().exists()
        self.startup_action.setChecked(actual)
        self.settings.startup.setChecked(actual)
        self.settings.saved_startup = actual

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
        if self.segment_id is not None and self.segment_clock is None and not self.finish_segment(True):
            return
        if not self.running:
            self.deadline += time.monotonic() - self.paused_at
            self.paused_at = None
        self.start_segment()
        self.tick()

    def stop_timer(self):
        if self.running:
            self.paused_at = time.monotonic()
        self.finish_segment(True)
        self.tick()

    def start_segment(self):
        if self.breaking or not self.task_name or self.segment_clock is not None:
            return
        if self.segment_id is None:
            self.segment_id = uuid.uuid4().hex
            self.segment_started = time.time()
        self.segment_clock = time.monotonic()

    def finish_segment(self, ask):
        if self.segment_id is None:
            return True
        if self.segment_clock is not None:
            self.segment_elapsed += time.monotonic() - self.segment_clock
            self.segment_clock = None
        save = True
        if ask:
            prompt = QDialog(self.settings)
            prompt.setWindowTitle(tr("save_work"))
            layout = QVBoxLayout(prompt)
            layout.addWidget(label(tr("save_work_message")))
            layout.addWidget(label(self.task_name + "  ·  " + work_tracking.format_time(self.segment_elapsed), "title"))
            actions = QHBoxLayout()
            yes = QPushButton(tr("save_changes"))
            yes.setObjectName("primary")
            yes.clicked.connect(prompt.accept)
            no = QPushButton(tr("discard_changes"))
            no.clicked.connect(lambda: prompt.done(2))
            actions.addWidget(yes)
            actions.addWidget(no)
            layout.addLayout(actions)
            ui_icons.apply(prompt)
            result = prompt.exec()
            if result not in (1, 2):
                return False
            save = result == 1
        try:
            if save:
                self.journal.save(self.segment_id, self.task_name, self.segment_started, self.segment_elapsed)
        except OSError:
            QMessageBox.warning(self.settings, "RestTimer", tr("log_error"))
            return False
        self.segment_id = None
        self.segment_elapsed = 0
        return True

    def reset_timer(self):
        if self.breaking:
            now = time.monotonic()
            if not self.running:
                self.paused_at = now
            self.deadline = now + self.preferences["break_seconds"]
            self.tick()
            return
        self.prompting = True
        try:
            dialog = WorkStartDialog(self)
            if dialog.exec() != QDialog.Accepted:
                return
            if self.running:
                self.paused_at = time.monotonic()
            if not self.finish_segment(True):
                return
            self.task_name = dialog.task.text().strip()
            self.session_seconds = dialog.duration.seconds()
            self.paused_at = None
            self.deadline = time.monotonic() + self.session_seconds
            self.start_segment()
        finally:
            self.prompting = False
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
            overlay.reset.clicked.connect(self.reset_timer)
            overlay.set_running(self.running)
            overlay.showFullScreen()

    def begin_break(self):
        if self.breaking:
            return
        if not self.finish_segment(False):
            self.paused_at = time.monotonic()
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
        self.deadline = now + (self.session_seconds or self.preferences["work_seconds"])
        if self.running:
            self.start_segment()
        self.tick()

    def tick(self):
        self.start_timer_action.setEnabled(not self.running)
        self.stop_timer_action.setEnabled(self.running)
        self.settings.start_timer.setEnabled(not self.running)
        self.settings.stop_timer.setEnabled(self.running)
        clock_time = time.monotonic() if self.running else self.paused_at
        remaining = max(0, math.ceil(self.deadline - clock_time))
        if remaining == 0 and self.running and not self.prompting:
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
        status_text = tr("end_break" if self.breaking else "next_break") + "  \u200e" + text
        if not self.running:
            status_text = tr("paused") + " · " + status_text
        if self.task_name and not self.breaking:
            status_text = self.task_name + " · " + status_text
        if self.status.text() != status_text:
            self.status.setText(status_text)
        self.settings.timer_status.setText(self.status.text())
        self.tray.setToolTip(("RestTimer  " if self.running else "RestTimer · " + tr("paused") + "  ") + text)
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
        text = tr("update_available", release["tag_name"])
        if not self.menu.isVisible():
            self.update_action.setText(text)
        self.settings.update_notice.setText(text)
        self.settings.update_notice.setVisible(True)
        if not self.updater.state["daily_enabled"]:
            return
        if self.updater.state["notified_tag"] == release["tag_name"]:
            return
        if QSystemTrayIcon.isSystemTrayAvailable():
            self.tray.showMessage(tr("update_title"), text, QSystemTrayIcon.Information, 10000)
        else:
            self.settings.show()
            self.app.alert(self.settings)
        self.updater.state["notified_tag"] = release["tag_name"]
        self.updater.save()

    def check_updates(self, checked=False, history=False):
        if self.breaking:
            return
        self.settings.select_page("history" if history else "updates")
        self.settings.show()
        self.settings.raise_()
        self.settings.activateWindow()

    def restart_after_update(self):
        if self.running:
            self.paused_at = time.monotonic()
        if not self.finish_segment(False):
            return
        try:
            if self.instance:
                lock, server = self.instance
                server.close()
                lock.close()
                self.instance = None
            subprocess.Popen(["/opt/resttimer/resttimer", "--background"], env=update_core.system_environment(), close_fds=True)
        except OSError:
            self.update_dialog.status.setText(tr("restart_failed"))
            self.instance = acquire_instance(self)
            return
        self.quit()

    def quit(self):
        if self.update_dialog is not None and self.update_dialog.process is not None:
            self.update_dialog.status.setText(tr("install_busy"))
            self.settings.select_page("updates")
            self.settings.show()
            self.settings.raise_()
            return
        if self.running:
            self.paused_at = time.monotonic()
        if not self.finish_segment(True):
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
    app = QApplication(sys.argv)
    app.setApplicationName("RestTimer")
    app.setOrganizationName("RestTimer")
    app.setQuitOnLastWindowClosed(False)
    app.setFont(QFont("DejaVu Sans", 10))
    apply_color_scheme(app, app.styleHints().colorScheme())
    app.styleHints().colorSchemeChanged.connect(lambda scheme: apply_color_scheme(app, scheme))
    controller = Controller(app)
    if "--smoke-test" in sys.argv:
        verify_language_fonts(app)
        verify_ui_assets()
        if update_core.tls_context().cert_store_stats()["x509_ca"] == 0:
            raise RuntimeError("No system certificate authorities available for updates.")
        if controller.running:
            raise RuntimeError("The timer must launch paused.")
        import tempfile
        with tempfile.TemporaryDirectory() as folder:
            report = controller.settings.report
            report.refresh()
            for mode in range(3):
                report.chart_type.setCurrentIndex(mode)
                report.export(Path(folder) / (str(mode)+".png"), False)
                report.export(Path(folder) / (str(mode)+".pdf"), True)
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
