<div align="center">
  <img src="assets/RestTimer.png" width="88" height="88" alt="آیکن RestTimer">
  <h1>RestTimer</h1>
  <p dir="rtl"><strong>یادآوری استراحت روی همهٔ مانیتورها، برای ویندوز و لینوکس</strong></p>
  <p dir="rtl">پیش‌فرض ۶۰ دقیقه کار و ۱۵ دقیقه استراحت است. زمان هر دوره را با ساعت، دقیقه و ثانیه تنظیم کنید.</p>
</div>

<div align="center">

[![آخرین نسخه][release-badge]][releases]
[![نصاب ویندوز][windows-badge]][windows]
[![بستهٔ Ubuntu][ubuntu-badge]][ubuntu]
[![بستهٔ Fedora][fedora-badge]][fedora]
[![مجوز MIT][license-badge]](LICENSE)

[![ساخت و آزمون نصاب‌ها][build-badge]][workflow]
[![معماری x86-64][arch-badge]][releases]

**[دریافت برنامه][releases] · [گزارش اشکال][issues] · [راهنمای مشارکت](CONTRIBUTING.md)**

</div>

<div dir="rtl">

هنگام استراحت، بالای مرکز هر مانیتور یک نوار افقی با شمارش معکوس مشترک نمایش داده می‌شود. دکمهٔ کوچک رد کردن در سمت چپ قرار دارد و پس‌زمینه محو و تیره می‌شود. پایان تایمر، رد کردن یا زدن Esc، دورهٔ بعدی کار را آغاز می‌کند.

## دانلود و نصب

فایل مناسب سیستم خود را از [آخرین انتشار](https://github.com/salehghotbani/RestTimer/releases/latest) دریافت کنید. بسته‌ها برای معماری **x86-64** ساخته می‌شوند.

| سیستم | قالب بسته | دریافت |
| --- | --- | --- |
| **Windows 10 / 11** | نصاب EXE | [نصب ویندوز][windows] |
| **Ubuntu 22.04 یا جدیدتر** | بستهٔ DEB | [نصب Ubuntu][ubuntu] |
| **Fedora 43 یا جدیدتر** | بستهٔ RPM | [نصب Fedora][fedora] |

### ویندوز

فایل **setup.exe** را اجرا کنید. پیش از شروع مراحل نصب، پنجرهٔ انتخاب **فارسی / English** نمایش داده می‌شود. مراحل و گزینه‌های نصب مطابق زبان انتخاب‌شده نمایش داده می‌شوند. نصاب برنامه را برای حساب فعلی نصب می‌کند و میانبر منوی Start و امکان حذف از تنظیمات ویندوز را ایجاد می‌کند. گزینه‌های اجرای خودکار و میانبر دسکتاپ در نصاب قابل انتخاب‌اند. برنامه به .NET Framework 4.8 نیاز دارد که نصاب وجود آن را بررسی می‌کند.

### Ubuntu

بستهٔ DEB را در مدیر نرم‌افزار باز کنید یا در پوشهٔ دانلودشده اجرا کنید.

```bash
sudo apt install ./RestTimer-v1.4.0-linux-amd64.deb
```

### Fedora

بستهٔ RPM را در مدیر نرم‌افزار باز کنید یا اجرا کنید.

```bash
sudo dnf install ./RestTimer-v1.4.0-linux-x86_64.rpm
```

سپس RestTimer را از منوی برنامه‌ها یا فرمان `resttimer` اجرا کنید. Python و Qt مورد نیاز در بسته‌های لینوکس قرار دارند. حذف برنامه با `sudo apt remove resttimer` یا `sudo dnf remove resttimer` انجام می‌شود.

## استفاده

- با کلیک چپ یا راست روی آیکن کنار ساعت، منوی برنامه باز می‌شود. می‌توانید زمان‌ها را تنظیم کنید، استراحت را همان لحظه شروع کنید، اجرای خودکار را تغییر دهید، به‌روزرسانی را بررسی کنید یا از برنامه خارج شوید.
- شمارش از زمان اجرا شروع می‌شود. رد کردن استراحت، زمان کار بعدی را از نو آغاز می‌کند.
- دکمه‌های **شروع** و **توقف** در منوی آیکن، تنظیمات و کادر استراحت قرار دارند. توقف، شمارش همان دوره را مکث می‌کند و شروع از زمان باقی‌مانده ادامه می‌دهد. در حالت توقف، کادر استراحت باز می‌ماند و دکمهٔ شروع روی همهٔ مانیتورها فعال است. رد کردن استراحت یا تغییر زمان‌ها، وضعیت توقف را حفظ می‌کند.
- تنظیمات ویندوز در حساب فعلی و تنظیمات لینوکس در `~/.config/resttimer/settings.json` ذخیره می‌شوند. تنظیمات قدیمیِ دقیقه‌ای ویندوز نیز خوانده می‌شوند.
- اجرای خودکار لینوکس از `~/.config/autostart/resttimer.desktop` استفاده می‌کند.

### دسکتاپ‌های لینوکس

نسخهٔ لینوکس روی X11 و Wayland اجرا می‌شود. در Wayland، در صورت در دسترس نبودن تصویر دسکتاپ، پوشش نیمه‌شفاف صفحه را تیره می‌کند. جای‌گذاری و اولویت پنجره‌ها تحت کنترل مدیر پنجرهٔ سیستم است.

در GNOME، نمایش آیکن سینی سیستم و رفتار کلیک‌های آن به پشتیبانی دسکتاپ یا افزونهٔ AppIndicator وابسته است. اگر سینی سیستم موجود نباشد، پنجرهٔ کنترل برنامه نمایش داده می‌شود. در این حالت، بستن پنجره برنامه را هم می‌بندد. اجرای دوباره از منوی برنامه‌ها، پنجرهٔ نسخهٔ در حال اجرا را باز می‌کند.

## به‌روزرسانی

برنامه هنگام اجرا و سپس هر ۲۴ ساعت، آخرین نسخهٔ پایدار را بررسی می‌کند. اگر اینترنت در دسترس نباشد، ۶ ساعت بعد دوباره تلاش می‌کند. نتیجه بین اجراها ذخیره می‌شود و برای هر نسخه یک اعلان نمایش داده می‌شود. با کلیک روی اعلان یا گزینهٔ «به‌روزرسانی و تغییرات»، تغییرات نسخهٔ جدید را بخوانید و با «دانلود و نصب» تأیید کنید. دانلود دارای نوار پیشرفت و امکان لغو است؛ اندازه و SHA-256 فایل پیش از نصب بررسی می‌شوند. بررسی روزانه را می‌توان در همین پنجره غیرفعال کرد.

در ویندوز، نصب با زبان و مسیر فعلی انجام می‌شود و برنامه خودکار بسته و دوباره اجرا می‌شود. در Ubuntu و Fedora، نصب از طریق apt یا dnf با درخواست مجوز مدیر سیستم انجام می‌شود. رد کردن مجوز یا خطای دانلود، نسخهٔ در حال اجرا را نمی‌بندد.

تب «تاریخچهٔ نسخه‌ها» تغییرات هر انتشار را از GitHub نمایش می‌دهد. [فهرست تغییرات](CHANGELOG.md) نیز همراه برنامه برای مشاهدهٔ آفلاین قرار دارد. برای دیدن اعلان‌ها، اعلان‌های RestTimer باید در تنظیمات سیستم مجاز باشند.

در ویندوز **نصاب EXE جدید** را اجرا کنید. اگر RestTimer باز باشد، نصاب پیش از بستن برنامه اطلاع می‌دهد، آن را خودکار می‌بندد و پس از پایان نصب دوباره اجرا می‌کند. پیام‌ها با زبان انتخاب‌شدهٔ فارسی یا انگلیسی نمایش داده می‌شوند. در Ubuntu یا Fedora، بستهٔ جدید را با همان دستور نصب کنید. تنظیمات زمان‌ها در ارتقا حفظ می‌شوند.

## فناوری‌های استفاده‌شده

[![C# و WinForms][csharp-badge]][winforms]
[![.NET Framework][dotnet-badge]][dotnet]
[![Python][python-badge]][python]
[![PySide6 Essentials][pyside-badge]][pyside]
[![PyInstaller][pyinstaller-badge]][pyinstaller]
[![Inno Setup][inno-badge]][inno]

| فناوری | کاربرد |
| --- | --- |
| **C#، WinForms و .NET Framework 4.8** | رابط ویندوز، منوی کنار ساعت و پوشش هم‌زمان مانیتورها |
| **Python 3.11 و PySide6 Essentials / Qt 6.12** | رابط لینوکس، تنظیم زمان‌ها، آیکن سینی سیستم و پنجره‌های استراحت |
| **Inno Setup 6** | ساخت نصاب EXE، میانبرها و حذف برنامه در ویندوز |
| **PyInstaller، dpkg-deb و rpmbuild** | بسته‌بندی وابستگی‌های لینوکس و تولید DEB و RPM |
| **GitHub Actions، Xvfb، Weston و Docker** | ساخت و آزمون نصب، حذف و اجرای برنامه روی Windows، Ubuntu، Fedora، X11 و Wayland |

## ساخت و مشارکت

دستورهای ساخت نصاب ویندوز، بسته‌های لینوکس و آزمون‌ها در [راهنمای مشارکت](CONTRIBUTING.md) آمده‌اند. workflow مخزن نصب و حذف نصاب ویندوز و بسته‌ها در Ubuntu و Fedora، و اجرای رابط لینوکس روی X11 و Wayland را بررسی می‌کند.

کد پروژه با [مجوز MIT](LICENSE) منتشر می‌شود. مجوز و سورس وابستگی‌های همراه در [اعلان اجزای ثالث](THIRD_PARTY_NOTICES.md) مشخص شده‌اند.

</div>

[releases]: https://github.com/salehghotbani/RestTimer/releases/latest
[windows]: https://github.com/salehghotbani/RestTimer/releases/download/v1.4.0/RestTimer-v1.4.0-windows-x64-setup.exe
[ubuntu]: https://github.com/salehghotbani/RestTimer/releases/download/v1.4.0/RestTimer-v1.4.0-linux-amd64.deb
[fedora]: https://github.com/salehghotbani/RestTimer/releases/download/v1.4.0/RestTimer-v1.4.0-linux-x86_64.rpm
[issues]: https://github.com/salehghotbani/RestTimer/issues
[workflow]: https://github.com/salehghotbani/RestTimer/actions/workflows/packages.yml
[release-badge]: https://img.shields.io/github/v/release/salehghotbani/RestTimer?label=Release&color=16836b
[windows-badge]: https://img.shields.io/badge/Windows-EXE-2563eb
[ubuntu-badge]: https://img.shields.io/badge/Ubuntu-DEB-E95420?logo=ubuntu&logoColor=white
[fedora-badge]: https://img.shields.io/badge/Fedora-RPM-294172?logo=fedora&logoColor=white
[license-badge]: https://img.shields.io/badge/License-MIT-475569
[build-badge]: https://github.com/salehghotbani/RestTimer/actions/workflows/packages.yml/badge.svg?branch=main&event=push
[arch-badge]: https://img.shields.io/badge/Architecture-x86--64-475569
[csharp-badge]: https://img.shields.io/badge/C%23-WinForms-512BD4
[dotnet-badge]: https://img.shields.io/badge/.NET_Framework-4.8-512BD4?logo=dotnet&logoColor=white
[python-badge]: https://img.shields.io/badge/Python-3.11-3776AB?logo=python&logoColor=white
[pyside-badge]: https://img.shields.io/badge/PySide6_Essentials-6.12.0-167a4b?logo=qt&logoColor=white
[pyinstaller-badge]: https://img.shields.io/badge/PyInstaller-6.22.3-345d9d
[inno-badge]: https://img.shields.io/badge/Inno_Setup-6-475569
[winforms]: https://learn.microsoft.com/dotnet/desktop/winforms/
[dotnet]: https://dotnet.microsoft.com/download/dotnet-framework/net48
[python]: https://www.python.org/
[pyside]: https://doc.qt.io/qtforpython-6/
[pyinstaller]: https://pyinstaller.org/
[inno]: https://jrsoftware.org/isinfo.php
