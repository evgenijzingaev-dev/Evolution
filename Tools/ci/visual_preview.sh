#!/usr/bin/env bash
set -euo pipefail

# The Editor build may be headless; the URP player must have a real GL context.
# A hardware runner renders natively on its own display, not inside the build container.
backend=${EVOLUTION_VISUAL_BACKEND:-software}
case "$backend" in
  software)
    export LIBGL_ALWAYS_SOFTWARE=1
    if [[ ${EVOLUTION_INSIDE_XVFB:-} != 1 ]]; then
      export EVOLUTION_INSIDE_XVFB=1
      exec xvfb-run --auto-servernum --server-args='-screen 0 1920x1080x24 +extension GLX' bash "$0" "$@"
    fi
    ;;
  hardware)
    unset LIBGL_ALWAYS_SOFTWARE
    : "${DISPLAY:?GPU runner needs a working Xorg display and permission to access it}"
    ;;
  *) echo '::error::VISUAL_PREVIEW_BACKEND must be software or hardware.'; exit 1 ;;
esac

if [[ ${1:-} == --probe ]]; then
  glxinfo -B | python3 -c '
import os, re, sys
text = sys.stdin.read()
fields = {}
for line in text.splitlines():
    for key in ("OpenGL vendor string", "OpenGL renderer string", "OpenGL core profile version string"):
        if line.strip().startswith(key + ":"):
            fields[key] = line.split(":", 1)[1].strip()
for key, value in fields.items():
    print(key + ": " + value)
version = re.match(r"(\d+)\.(\d+)", fields.get("OpenGL core profile version string", ""))
if not version or tuple(map(int, version.groups())) < (4, 5):
    sys.exit("URP preview requires a working OpenGL 4.5 context. See docs/VISUAL_PREVIEW.md for a GPU runner.")
if os.getenv("EVOLUTION_VISUAL_BACKEND") == "hardware" and any(word in fields.get("OpenGL renderer string", "").lower() for word in ("llvmpipe", "softpipe", "software")):
    sys.exit("Hardware runner selected but software renderer detected.")
print("GL context probe passed; Unity URP rendering is still unverified until the five captures pass.")
'
  exit
fi

player=build/visual-preview/StandaloneLinux64/EVOLUTION
[[ -x $player ]] || { echo '::error::Visual Preview player missing.'; exit 1; }
destination="$PWD/artifacts/visual-preview"
mkdir -p "$destination"
# Private player logs are deliberately excluded from upload (as are Editor/license logs).
private_log=$(mktemp)
trap 'rm -f "$private_log"' EXIT
if ! timeout --signal=TERM --kill-after=15s 240s "$player" \
    -force-glcore -popupwindow -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 \
    -evolution-visual-preview "$destination" -logFile "$private_log"; then
  echo '::error::Real Unity render failed or timed out. No substitute images were produced. See docs/VISUAL_PREVIEW.md for GPU setup.'
  # Print only known diagnostic categories, never arbitrary log contents.
  python3 - "$private_log" <<'PY'
import sys
from pathlib import Path
text = Path(sys.argv[1]).read_text(errors="replace")
for category in ("Visual Preview:", "Shader error", "Failed to initialize graphics", "Exception", "Out of memory"):
    if category.lower() in text.lower():
        print("Detected diagnostic category: " + category)
PY
  exit 1
fi
python3 Tools/ci/check_visual_preview.py "$destination"
