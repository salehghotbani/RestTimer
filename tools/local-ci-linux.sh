#!/usr/bin/env bash
set -euo pipefail
cd /work
# Use Git's canonical source, including LF for shell scripts and RPM specs.
tar xf /src.tar
trap 'mkdir -p /out; cp -r build /out/ 2>/dev/null || true; cp -r dist/release /out/ 2>/dev/null || true' EXIT
python -m pip install -r requirements-build-linux.txt
python tests/test_localization.py
python tests/test_update_core.py
for test in linux_smoke work_tracking dashboard sidebar tray_menu appearance visual_audit linux_localization; do
    QT_QPA_PLATFORM=offscreen python "tests/$test.py"
done
test "$(pdfinfo build/dashboard-linux/many.pdf | awk '/Pages:/ {print $2}')" = 4
pdftoppm -scale-to 1300 -f 1 -singlefile -png build/dashboard-linux/fa-2.pdf build/dashboard-linux/fa-pdf-preview
bash tools/build_linux.sh
xvfb-run -a dist/resttimer/resttimer --smoke-test
export XDG_RUNTIME_DIR="$(mktemp -d)"
chmod 700 "$XDG_RUNTIME_DIR"
weston --backend=headless-backend.so --socket=resttimer-local --idle-time=0 --use-pixman > build/weston.log 2>&1 &
weston_pid=$!
for attempt in $(seq 1 30); do test -S "$XDG_RUNTIME_DIR/resttimer-local" && break; sleep .2; done
test -S "$XDG_RUNTIME_DIR/resttimer-local"
QT_QPA_PLATFORM=wayland WAYLAND_DISPLAY=resttimer-local dist/resttimer/resttimer --smoke-test
kill "$weston_pid"
