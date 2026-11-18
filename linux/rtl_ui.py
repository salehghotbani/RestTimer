"""Place scrollbars on the physical right independently of text direction."""
from PySide6.QtCore import Qt
from PySide6.QtWidgets import QAbstractScrollArea, QComboBox, QScrollArea, QStyledItemDelegate
import i18n


class NavigationDelegate(QStyledItemDelegate):
    def initStyleOption(self, option, index):
        super().initStyleOption(option,index)
        option.direction = Qt.RightToLeft if i18n.is_rtl() else Qt.LeftToRight
        option.displayAlignment = Qt.AlignAbsolute | Qt.AlignVCenter | (Qt.AlignRight if i18n.is_rtl() else Qt.AlignLeft)


def right_scrollbars(root):
    areas = root.findChildren(QAbstractScrollArea)
    if isinstance(root,QAbstractScrollArea):
        areas.insert(0,root)
    for area in areas:
        area.setLayoutDirection(Qt.LeftToRight)
        if isinstance(area,QScrollArea) and area.widget() is not None:
            area.widget().setLayoutDirection(Qt.RightToLeft if i18n.is_rtl() else Qt.LeftToRight)
    for combo in root.findChildren(QComboBox):
        combo.view().setLayoutDirection(Qt.LeftToRight)
