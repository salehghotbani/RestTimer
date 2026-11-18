"""Place scrollbars on the physical right independently of text direction."""
from PySide6.QtCore import Qt, QRect, QSize
from PySide6.QtGui import QColor, QPainter
from PySide6.QtWidgets import QApplication, QAbstractScrollArea, QComboBox, QScrollArea, QStyledItemDelegate, QStyle, QStyleOptionViewItem
import i18n


class NavigationDelegate(QStyledItemDelegate):
    def sizeHint(self, option, index):
        return QSize(40,48)

    def initStyleOption(self, option, index):
        super().initStyleOption(option,index)
        option.direction = Qt.RightToLeft if i18n.is_rtl() else Qt.LeftToRight
        option.displayAlignment = Qt.AlignAbsolute | Qt.AlignVCenter | (Qt.AlignRight if i18n.is_rtl() else Qt.AlignLeft)

    def paint(self,painter,option,index):
        item=QStyleOptionViewItem(option)
        self.initStyleOption(item,index)
        light='#f4f7fa' in QApplication.instance().styleSheet()
        selected=bool(item.state & QStyle.State_Selected)
        hovered=bool(item.state & QStyle.State_MouseOver)
        painter.save()
        painter.setRenderHint(QPainter.Antialiasing)
        rect=item.rect.adjusted(0,3,0,-3)
        if selected or hovered:
            painter.setPen(Qt.NoPen)
            painter.setBrush(QColor('#dbeee8' if light else '#223a48'))
            painter.drawRoundedRect(rect,8,8)
        # The leading icon stays anchored while the sidebar width animates.
        rtl=i18n.is_rtl()
        icon_rect=QRect(rect.right()-31 if rtl else rect.left()+14,rect.center().y()-9,18,18)
        item.icon.paint(painter,icon_rect)
        text_rect=rect.adjusted(14 if rtl else 40,0,-40 if rtl else -14,0)
        painter.setFont(item.font)
        painter.setPen(QColor(('#11705b' if light else '#83e6c4') if selected else ('#1a2838' if light else '#edf4fa')))
        text=item.fontMetrics.elidedText(item.text,Qt.ElideRight,max(0,text_rect.width()))
        painter.drawText(text_rect,item.displayAlignment | Qt.TextSingleLine,text)
        painter.restore()


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
