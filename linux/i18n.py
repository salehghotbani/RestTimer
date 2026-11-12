"""Shared language catalogs; Qt is only needed by the frontend, not this module."""
import json
import re
from pathlib import Path
import sys

ROOT = Path(getattr(sys, "_MEIPASS", Path(__file__).resolve().parents[1]))
SUPPORTED = ("en", "fa", "ar", "zh", "es", "fr", "de", "pt", "ru", "ja", "ko", "hi", "tr")
_catalogs = {}
code = "en"


def normalize(language):
    candidate = str(language or "").replace("_", "-").split("-")[0].lower()
    return candidate if candidate in SUPPORTED else None


def preference(value):
    return "auto" if value == "auto" else normalize(value) or "auto"


def resolve(selected, system_languages):
    selected = preference(selected)
    if selected != "auto":
        return selected
    if isinstance(system_languages, str):
        system_languages = [system_languages]
    return next((normalize(language) for language in system_languages if normalize(language)), "en")


def use(selected, system_languages):
    global code
    code = resolve(selected, system_languages)


def is_rtl():
    return code in ("fa", "ar")


def catalog(language):
    if language not in _catalogs:
        _catalogs[language] = json.loads((ROOT / "locales" / f"{language}.json").read_text(encoding="utf-8"))
    return _catalogs[language]


def native_name(language):
    return catalog(language)["name"]


def tr(key, *arguments):
    value = catalog(code)["strings"].get(key, catalog("en")["strings"][key])
    return value.format(*arguments) if arguments else value


def translate_text(text, previous):
    if not text or previous == code:
        return text
    for key, value in catalog(previous)["strings"].items():
        if value == text:
            return tr(key)
    for key, value in catalog(previous)["strings"].items():
        if "{0}" in value:
            match = re.fullmatch(re.escape(value).replace(re.escape("{0}"), "(.*?)"), text, re.S)
            if match:
                return tr(key, match[1])
    return text
