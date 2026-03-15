#!/bin/bash
set -e

APP_NAME="L2 Toolkit"
RID="osx-arm64"
CONFIG="Release"
FRAMEWORK="net10.0"

PUBLISH_DIR="bin/$CONFIG/$FRAMEWORK/$RID/publish"
BUNDLE_DIR="$PUBLISH_DIR/$APP_NAME.app"

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

# Copia todos os arquivos do publish para Contents/MacOS/
# (inclui o binário AOT + quaisquer .dylib nativas, ex: e_sqlite3.dylib)
find "$PUBLISH_DIR" -maxdepth 1 -type f | while read f; do
    cp "$f" "$BUNDLE_DIR/Contents/MacOS/"
done

# Garante permissão de execução no binário principal
chmod +x "$BUNDLE_DIR/Contents/MacOS/$APP_NAME"

# Copia Info.plist
cp "Info.plist" "$BUNDLE_DIR/Contents/Info.plist"

# Copia ícone
cp "images/favicon.icns" "$BUNDLE_DIR/Contents/Resources/favicon.icns"

# Assinatura ad-hoc (necessária no macOS para apps não distribuídos pela App Store)
echo "▶ Assinando bundle (ad-hoc)..."
codesign --deep --force --sign - "$BUNDLE_DIR"

echo ""
echo "✓ Bundle gerado em: $BUNDLE_DIR"
echo ""

open "$PUBLISH_DIR"
