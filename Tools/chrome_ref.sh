#!/bin/zsh
# Renders Tools/test/*.svg with headless Chrome as <name>.ref.png, for comparing with SLSvg output.
cd "$(dirname "$0")/test"
for f in "$@"; do
  s=$(grep -oE 'viewBox="[^"]+"' $f.svg | head -1 | sed 's/viewBox="//;s/"//'); w=$(echo $s | awk -v k=${SCALE:-1} '{printf "%d", $3*k}'); h=$(echo $s | awk -v k=${SCALE:-1} '{printf "%d", $4*k}')
  echo "<html><body style='margin:0;background:transparent'><img src='$f.svg' width=$w height=$h style='display:block'></body></html>" > $f.html
  "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new --disable-gpu --hide-scrollbars --default-background-color=00000000 --force-device-scale-factor=1 --window-size=$w,$h --screenshot=$PWD/$f.ref.png file://$PWD/$f.html >/dev/null 2>&1
done
