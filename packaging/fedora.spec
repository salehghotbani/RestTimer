Name:           resttimer
Version:        %{app_version}
Release:        1
Summary:        Multilingual desktop work and break timer
License:        MIT
URL:            https://github.com/salehghotbani/RestTimer
Packager:       Seyed Mohammad Saleh Ghotbani
ExclusiveArch:  x86_64
AutoReqProv:    no
Requires:       ca-certificates, polkit, dejavu-sans-fonts, dbus-libs, fontconfig, glib2, openssl-libs
Requires:       google-noto-sans-cjk-fonts, google-noto-sans-devanagari-fonts
Requires:       libglvnd-egl, libglvnd-glx, libX11, libXext, libXrender, libICE, libSM, libxcb
Requires:       xcb-util-cursor, xcb-util-image, xcb-util-keysyms, xcb-util-renderutil, xcb-util-wm
Requires:       libxkbcommon, libxkbcommon-x11, xkeyboard-config
Requires:       libwayland-client.so.0()(64bit), libwayland-cursor.so.0()(64bit), libwayland-egl.so.1()(64bit)

# The PyInstaller payload is already linked and stripped as needed.
%define __os_install_post %{nil}
%global debug_package %{nil}

%description
Schedule synchronized breaks on all displays with configurable work and
rest durations. Includes a multilingual desktop frontend for X11 and Wayland.

%install
mkdir -p "%{buildroot}"
cp -a "%{app_payload}/." "%{buildroot}/"

%files
/opt/resttimer
/usr/bin/resttimer
/usr/share/applications/resttimer.desktop
/usr/share/icons/hicolor/256x256/apps/resttimer.png
%license /usr/share/doc/resttimer/copyright
%license /usr/share/doc/resttimer/LICENSE
%doc /usr/share/doc/resttimer/THIRD_PARTY_NOTICES.md
%doc /usr/share/doc/resttimer/README.md
%doc /usr/share/doc/resttimer/README.fa.md
%doc /usr/share/doc/resttimer/CONTRIBUTING.md
%doc /usr/share/doc/resttimer/CONTRIBUTING.fa.md
%doc /usr/share/doc/resttimer/assets
%doc /usr/share/doc/resttimer/CHANGELOG.md
%doc /usr/share/doc/resttimer/CHANGELOG.fa.md
%doc /usr/share/doc/resttimer/licenses
