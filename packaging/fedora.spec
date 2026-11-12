Name:           resttimer
Version:        %{app_version}
Release:        1
Summary:        Persian desktop work and break timer
License:        MIT
URL:            https://github.com/salehghotbani/RestTimer
Packager:       Seyed Mohammad Saleh Ghotbani
ExclusiveArch:  x86_64
AutoReqProv:    no
Requires:       ca-certificates, dejavu-sans-fonts, dbus-libs, fontconfig, glib2, openssl-libs
Requires:       libglvnd-egl, libglvnd-glx, libX11, libXext, libXrender, libICE, libSM, libxcb
Requires:       xcb-util-cursor, xcb-util-image, xcb-util-keysyms, xcb-util-renderutil, xcb-util-wm
Requires:       libxkbcommon, libxkbcommon-x11, xkeyboard-config
Requires:       libwayland-client.so.0()(64bit), libwayland-cursor.so.0()(64bit), libwayland-egl.so.1()(64bit)

# The PyInstaller payload is already linked and stripped as needed.
%define __os_install_post %{nil}
%global debug_package %{nil}

%description
Schedule synchronized breaks on all displays with configurable work and
rest durations. Includes a Persian desktop frontend for X11 and Wayland.

%install
mkdir -p "%{buildroot}"
cp -a "%{app_payload}/." "%{buildroot}/"

%files
/opt/resttimer
/usr/bin/resttimer
/usr/share/applications/resttimer.desktop
/usr/share/icons/hicolor/256x256/apps/resttimer.png
%license /usr/share/doc/resttimer/copyright
%doc /usr/share/doc/resttimer/THIRD_PARTY_NOTICES.md
%doc /usr/share/doc/resttimer/README.md
%doc /usr/share/doc/resttimer/licenses
