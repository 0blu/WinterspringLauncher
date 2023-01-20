#!/bin/sh

set -e

if [ $# -ne 2 ]; then
    echo "Error: Invalid arguments"
    echo "Run: buid_app.sh <version> <WinterspringLauncherBinary>"
    exit 1
fi

if [ ! -d "AppTemplate" ]; then
    echo "Error: AppTemplate folder is not in cwd"
    exit 1
fi

VERSION="$1"
EXE_FILE="$2"

if [ ! -f "$EXE_FILE" ]; then
    echo "Error: '$EXE_FILE' is not a valid file"
    exit 1
fi

OPENSSL_DL="https://github.com/0blu/prebuilt-openssl3-for-macos/releases/download/openssl-3.0.7/openssl-3.0.7.zip"
APP_PATH="output/Winterspring Launcher.app"

echo "Building for:"
echo "- Version: $VERSION"
echo "- Binary: $EXE_FILE"
echo "- Result: $APP_PATH"

echo "Deleting existing app"
rm -rf "$APP_PATH"
mkdir -p "$APP_PATH"

echo "Copy template"
cp -r AppTemplate/* "$APP_PATH/."

echo "Download openssl3"
mkdir "$APP_PATH/Libs"
curl --fail -SL "$OPENSSL_DL" -o "$APP_PATH/Libs/openssl3.zip"
(cd "$APP_PATH/Libs/" \
    && unzip ./openssl3.zip \
    && mv openssl-3.*/*.3.dylib . \
    && rm openssl3.zip \
    && rm -rf openssl-3.* \
)

echo "Copying launcher executable"
cp "$EXE_FILE" "$APP_PATH"

echo "Replace version in info.plist"
sed -i.bak "s/{{VERSION}}/$VERSION/g" "$APP_PATH/Resources/info.plist"

echo "Making everthing executable"
chmod -R a+x "$APP_PATH"

echo "Done building '$APP_PATH'"
