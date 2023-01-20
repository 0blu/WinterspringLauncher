#!/bin/sh

set -e

if [ ! -d "output" ]; then
    echo "Error: no 'output' directory"
    exit 1
fi

sips --setProperty dpiWidth 144 --setProperty dpiHeight 144 dmg_backgroung.png

rm -rf output_dmg
mkdir output_dmg
create-dmg \
    --volname "Winterspring Launcher Installer" \
    --background dmg_background.png \
    --window-size 525 310 \
    --icon-size 90 \
    --icon "Winterspring Launcher.app" 0 120 \
    --hide-extension "Winterspring Launcher.app" \
    --app-drop-link 280 120 \
    output_dmg/WinterspringLauncher.dmg output
