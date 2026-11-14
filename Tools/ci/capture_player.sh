#!/usr/bin/env bash
# Run the actual IL2CPP player with a software OpenGL display. Capture only the
# player window; never upload activation/Editor logs or runner home directories.
set -euo pipefail
# GameCI Docker may leave artifacts/ owned by root.
# Give only the capture output directory to the runner, not the entire checkout.
sudo install -d -o "$(id -u)" -g "$(id -g)" artifacts/visual
player=$(find build/StandaloneLinux64 -type f \( -name '*.x86_64' -o -name 'EVOLUTION' \) -print -quit)
if [[ -z "$player" ]]; then
  echo '::error::No built Linux player executable found.'
  exit 1
fi
# GameCI creates the Linux build as root; chmod as runner fails with EPERM.
# Change only the built executable mode, leaving checkout ownership untouched.
sudo chmod +x "$player"
export EVOLUTION_CAPTURE_PLAYER="$player"
xvfb-run -a -s '-screen 0 2560x1440x24 +extension GLX' bash <<'CAPTURE'
set -euo pipefail
export LIBGL_ALWAYS_SOFTWARE=1
"$EVOLUTION_CAPTURE_PLAYER" -force-glcore -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -popupwindow -logFile /tmp/evolution-player-smoke.log &
player_pid=$!
trap 'kill "$player_pid" 2>/dev/null || true; wait "$player_pid" 2>/dev/null || true' EXIT
window=''
for attempt in $(seq 1 45); do
  if ! kill -0 "$player_pid" 2>/dev/null; then
    echo '::error::Player exited before opening a window.'; exit 1
  fi
  window=$(xdotool search --onlyvisible --pid "$player_pid" 2>/dev/null | head -1 || true)
  if [[ -n "$window" ]]; then break; fi
  sleep 1
done
if [[ -z "$window" ]]; then echo '::error::Player did not open a visible window.'; exit 1; fi
xdotool windowfocus --sync "$window"
sleep 8
xdotool key space
sleep 1
import -window "$window" artifacts/visual/01-overview-1920x1080.png
# Select a bright rendered organelle in the central unobstructed world viewport.
# Read RGB bytes with ImageMagick so no Python imaging dependency is needed.
read -r pick_x pick_y < <(python3 - <<'PY'
import subprocess
raw = subprocess.check_output(['convert', 'artifacts/visual/01-overview-1920x1080.png', '-depth', '8', 'rgb:-'])
width, height = map(int, subprocess.check_output(['identify', '-format', '%w %h', 'artifacts/visual/01-overview-1920x1080.png']).split())
assert (width, height) == (1920, 1080), (width, height)
best = (-1, 960, 540)
for y in range(180, 900):
    for x in range(450, 1450):
        i = (y * width + x) * 3
        r, g, b = raw[i:i+3]
        score = int(r) + int(g) + int(b)
        if score > best[0]: best = (score, x, y)
assert best[0] > 300, 'No bright organism pixels in the world viewport'
print(best[1], best[2])
PY
)
xdotool mousemove --window "$window" "$pick_x" "$pick_y" click 1
xdotool key f
sleep 2
import -window "$window" artifacts/visual/02-selected-1920x1080.png
# Exercise actual scroll input, follow shortcut, orbit and keyboard pan.
xdotool mousemove --window "$window" 960 540 click --repeat 2 --delay 80 4
xdotool key g
sleep 1
xdotool mousedown 3
sleep .2
xdotool mousemove_relative -- 75 30
sleep .2
xdotool mouseup 3
xdotool keydown d
sleep .2
xdotool keyup d
sleep 1
import -window "$window" artifacts/visual/03-orbit-1920x1080.png
xdotool windowsize --sync "$window" 2560 1440
sleep 2
import -window "$window" artifacts/visual/04-selected-2560x1440.png
if [[ $(identify -format '%wx%h' artifacts/visual/04-selected-2560x1440.png) != '2560x1440' ]]; then
  echo '::error::Player window did not resize to 2560x1440.'; exit 1
fi
xdotool key Home
sleep 2
import -window "$window" artifacts/visual/05-overview-2560x1440.png
# Resume once to exercise the running scene after all camera operations.
xdotool key space
sleep 2
if ! kill -0 "$player_pid" 2>/dev/null; then echo '::error::Player exited during smoke capture.'; exit 1; fi
if grep -Ei 'NullReferenceException|ArgumentException|InvalidOperationException|DllNotFoundException|Shader error|shader.*not supported' /tmp/evolution-player-smoke.log; then
  echo '::error::Runtime or shader errors in the real player.'; exit 1
fi
printf 'Actual Linux IL2CPP player rendered under Xvfb with software OpenGL.\nScreenshots: 1920x1080 and 2560x1440.\nInputs exercised: pause, selection, focus, wheel, follow, orbit, keyboard pan, overview.\nHuman review of the PNGs is still required.\n' > artifacts/visual/RESULT.txt
CAPTURE
