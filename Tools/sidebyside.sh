#!/bin/zsh
# sidebyside.sh <name> <height>: Chrome reference (left) next to SLSvg output (right) as test/<name>.cmp.png
cd "$(dirname "$0")/test"
echo "<html><body style='margin:0;background:#9ab;display:flex;gap:8px'><img src='$1.ref.png' height=$2><img src='$1.cs.png' height=$2></body></html>" > cmp.html
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --window-size=${3:-900},$2 --screenshot=$PWD/$1.cmp.png file://$PWD/cmp.html >/dev/null 2>&1
