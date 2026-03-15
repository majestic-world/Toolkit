#!/bin/bash
set -e

APP_NAME="L2 Toolkit"
RID="osx-arm64"
CONFIG="Release"
FRAMEWORK="net10.0"

PUBLISH_DIR="bin/$CONFIG/$FRAMEWORK/$RID/publish"
BUNDLE_DIR="$PUBLISH_DIR/$APP_NAME.app"
DMG_OUT="$PUBLISH_DIR/$APP_NAME.dmg"

echo "▶ Compilando $APP_NAME (Native AOT, $RID)..."
dotnet publish -r $RID -c $CONFIG \
  -p:PublishAot=true \
  -p:OptimizationPreference=Speed \
  -p:StackTraceSupport=false \
  -p:InvariantGlobalization=true

echo "▶ Montando bundle .app..."

rm -rf "$BUNDLE_DIR"
mkdir -p "$BUNDLE_DIR/Contents/MacOS"
mkdir -p "$BUNDLE_DIR/Contents/Resources"

# Copia todos os arquivos do publish (binário AOT + .dylib nativas)
find "$PUBLISH_DIR" -maxdepth 1 -type f | while read f; do
    cp "$f" "$BUNDLE_DIR/Contents/MacOS/"
done

chmod +x "$BUNDLE_DIR/Contents/MacOS/$APP_NAME"

cp "Info.plist" "$BUNDLE_DIR/Contents/Info.plist"
cp "images/favicon.icns" "$BUNDLE_DIR/Contents/Resources/favicon.icns"

echo "▶ Assinando bundle (ad-hoc)..."
codesign --deep --force --sign - "$BUNDLE_DIR"

echo "▶ Criando instalador .dmg..."
rm -f "$DMG_OUT"

create-dmg \
  --volname "$APP_NAME" \
  --volicon "images/favicon.icns" \
  --window-pos 200 120 \
  --window-size 560 400 \
  --icon-size 128 \
  --icon "$APP_NAME.app" 140 200 \
  --hide-extension "$APP_NAME.app" \
  --app-drop-link 420 200 \
  "$DMG_OUT" \
  "$BUNDLE_DIR"

echo ""
echo "✓ Bundle : $BUNDLE_DIR"
echo "✓ Instalador: $DMG_OUT"
echo ""

open "$PUBLISH_DIR"
