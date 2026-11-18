"""Theme-aware vector icons shared by the desktop controls."""
from functools import lru_cache
from PySide6.QtCore import QByteArray, Qt, QSize
from PySide6.QtGui import QIcon, QPixmap, QPainter
from PySide6.QtSvg import QSvgRenderer
from PySide6.QtWidgets import QApplication, QPushButton
from i18n import tr

PATHS = {
 'menu': '<path d="M4 6h16M4 12h16M4 18h16"/>',
 'play': '<path d="m8 5 12 7-12 7Z" fill="COLOR" stroke="none"/>',
 'pause': '<path d="M6 5h4v14H6zm8 0h4v14h-4z" fill="COLOR" stroke="none"/>',
 'reset': '<path d="M4 9a8 8 0 1 1 0 7M3 3v6h6"/>',
 'save': '<path d="M4 3h16v18H4zM8 3v6h8V3M8 21v-7h8v7"/>',
 'close': '<path d="m6 6 12 12M18 6 6 18"/>',
 'skip': '<path d="m5 5 11 7-11 7Z" fill="COLOR" stroke="none"/><path d="M19 5v14"/>',
 'break': '<path d="M7 2v3m5-3v3M4 8h13v10H4zM17 8a4 4 0 0 1 0 8M3 21h16"/>',
 'download': '<path d="M12 3v12m-5-5 5 5 5-5M4 16v5h16v-5"/>',
 'dashboard': '<path d="M3 3h7v7H3zm11 0h7v7h-7zM3 14h7v7H3zm11 0h7v7h-7z"/>',
 'settings': '<circle cx="12" cy="12" r="7"/><circle cx="12" cy="12" r="3"/><path d="M12 2v3m0 14v3M2 12h3m14 0h3M5 5l2 2m10 10 2 2M5 19l2-2M17 7l2-2"/>',
 'history': '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l4 3"/>',
 'info': '<circle cx="12" cy="12" r="9"/><path d="M12 11v6"/><circle cx="12" cy="7" r="1" fill="COLOR"/>',
 'quit': '<path d="M10 3H4v18h6M9 12h12m-5-5 5 5-5 5"/>',
 'image': '<path d="M3 4h18v16H3zm1 14 7-6 4 4 4-4"/><circle cx="8.5" cy="8.5" r="1.5"/>',
 'document': '<path d="M5 3h14v18H5zM9 9h6m-6 4h6m-6 4h6"/>',
}
MAPPING = dict(zip(
 ['start','stop','reset_timer','save','save_changes','discard_changes','skip','start_break','break_short','install','check_again','updates_tab','updates_menu','dashboard','general','version_history','about','quit_app','new_session'],
 ['play','pause','reset','save','save','close','skip','break','break','download','reset','download','download','dashboard','settings','history','info','quit','play']))


@lru_cache(maxsize=128)
def icon(kind, color):
    markup = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="COLOR" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">'+PATHS[kind]+'</svg>'
    renderer = QSvgRenderer(QByteArray(markup.replace('COLOR', color).encode()))
    pixmap = QPixmap(32,32)
    pixmap.fill(Qt.transparent)
    painter = QPainter(pixmap)
    renderer.render(painter)
    painter.end()
    pixmap.setDevicePixelRatio(2)
    return QIcon(pixmap)


def color_for(primary=False):
    light = '#f4f7fa' in QApplication.instance().styleSheet()
    return ('#ffffff' if light else '#0d1521') if primary else ('#1a2838' if light else '#edf4fa')


def apply(root):
    for button in root.findChildren(QPushButton):
        if button.objectName() == 'project_site':
            continue
        kind = 'image' if button.text() == 'PNG' else 'document' if button.text() == 'PDF' else None
        if kind is None:
            kind = button.property('ui_glyph')
        if kind is None:
            kind = next((value for key,value in MAPPING.items() if button.text() == tr(key)),None)
        if kind:
            button.setProperty('ui_glyph',kind)
            button.setIcon(icon(kind,color_for(button.objectName() == 'primary')))
            button.setIconSize(QSize(16,16))
    navigation = getattr(root,'navigation',None)
    if navigation is not None:
        for index,kind in enumerate(('dashboard','settings','download','history','info')):
            navigation.item(index).setIcon(icon(kind,color_for()))
