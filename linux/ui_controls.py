"""Native checkbox behavior with wrapped text and matching themed indicators."""
from PySide6.QtCore import QRect, QSize, Qt
from PySide6.QtGui import QPalette, QPainter
from PySide6.QtWidgets import QCheckBox, QSizePolicy, QStyle, QStyleOptionButton


class CheckBox(QCheckBox):
    def __init__(self,*args,**kwargs):
        super().__init__(*args,**kwargs)
        policy=QSizePolicy(QSizePolicy.Preferred,QSizePolicy.Minimum)
        policy.setHeightForWidth(True)
        self.setSizePolicy(policy)

    def text_gap(self):
        style=self.style()
        return style.pixelMetric(QStyle.PM_IndicatorWidth,None,self)+style.pixelMetric(QStyle.PM_CheckBoxLabelSpacing,None,self)+4

    def heightForWidth(self,width):
        height=self.fontMetrics().boundingRect(QRect(0,0,max(1,width-self.text_gap()),32767),Qt.TextWordWrap,self.text()).height()
        return max(24,height+6)

    def minimumSizeHint(self):
        longest=max((self.fontMetrics().horizontalAdvance(word) for word in self.text().split()),default=0)
        return QSize(max(80,longest+self.text_gap()),max(24,self.fontMetrics().height()+6))

    def sizeHint(self):
        hint=super().sizeHint()
        return QSize(hint.width(),max(24,hint.height()))

    def hitButton(self,point):
        return self.rect().contains(point)

    def paintEvent(self,event):
        option=QStyleOptionButton()
        self.initStyleOption(option)
        text_rect=self.style().subElementRect(QStyle.SE_CheckBoxContents,option,self)
        option.text=''
        painter=QPainter(self)
        self.style().drawControl(QStyle.CE_CheckBox,option,painter,self)
        painter.setFont(self.font())
        painter.setPen(option.palette.color(QPalette.WindowText))
        painter.drawText(text_rect,Qt.AlignAbsolute|Qt.AlignVCenter|Qt.TextWordWrap|(Qt.AlignRight if self.layoutDirection()==Qt.RightToLeft else Qt.AlignLeft),self.text())
        painter.end()
