"""Catalog integrity and locale resolution shared by both desktop frontends."""
import ast
import json
from pathlib import Path
import re
import string
import sys
import unittest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "linux"))
import i18n


class LocalizationTests(unittest.TestCase):
    def test_locale_resolution(self):
        for locale, expected in (("fa-IR", "fa"), ("ar-SA", "ar"), ("es-MX", "es"),
                                 ("pt_BR", "pt"), ("zh-Hant-TW", "zh"), ("en-GB", "en"),
                                 ("ja-JP", "ja"), ("he-IL", "en"), ("xx", "en")):
            self.assertEqual(i18n.resolve("auto", [locale]), expected)
            self.assertEqual(i18n.resolve("de", [locale]), "de")
        self.assertEqual(i18n.resolve("auto", ["xx", "fr-FR", "en-US"]), "fr")
        self.assertEqual(i18n.resolve("auto", []), "en")
        self.assertEqual(i18n.preference("../../fr"), "auto")
        self.assertEqual(i18n.preference(None), "auto")

    def test_catalogs_and_placeholders(self):
        english = i18n.catalog("en")["strings"]
        formatter = string.Formatter()
        def fields(value):
            return sorted(field for _, field, _, _ in formatter.parse(value) if field is not None)
        self.assertEqual({path.stem for path in (ROOT / "locales").glob("*.json")}, set(i18n.SUPPORTED))
        self.assertNotIn("he", i18n.SUPPORTED)
        for code in i18n.SUPPORTED:
            catalog = i18n.catalog(code)
            for key in ("settings_menu", "updates_menu", "history", "update_available"):
                self.assertNotIn("…", catalog["strings"][key])
                self.assertNotIn("...", catalog["strings"][key])
            self.assertTrue(catalog["name"].strip())
            self.assertEqual(catalog["direction"], "rtl" if code in ("fa", "ar") else "ltr")
            self.assertEqual(set(catalog["strings"]), set(english), code)
            i18n.use(code, ["en-US"])
            self.assertEqual(i18n.is_rtl(), code in ("fa", "ar"))
            for key, value in catalog["strings"].items():
                self.assertIsInstance(value, str)
                self.assertTrue(value.strip(), (code, key))
                self.assertEqual(fields(value), fields(english[key]), (code, key))
                value.format(*["1.8.0" for _ in fields(value)])
                self.assertEqual(i18n.tr(key), value)
            self.assertIn("1.8.0", i18n.tr("update_available", "1.8.0"))
        i18n.use("auto", ["en-US"])
        self.assertEqual(i18n.catalog("fa")["strings"]["settings_menu"], "تنظیمات")

    def test_used_keys_exist(self):
        known = set(i18n.catalog("en")["strings"])
        for path in ROOT.glob("*.cs"):
            for key in re.findall(r'I18n\.T\("([a-z_]+)"', path.read_text(encoding="utf-8")):
                self.assertIn(key, known, str(path))
        for path in (ROOT / "linux").glob("*.py"):
            tree = ast.parse(path.read_text(encoding="utf-8"))
            for node in ast.walk(tree):
                if isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id == "tr":
                    if node.args and isinstance(node.args[0], ast.Constant):
                        self.assertIn(node.args[0].value, known, str(path))


if __name__ == "__main__":
    unittest.main()
