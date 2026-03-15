#!/bin/bash
set -e

APP_NAME="L2 Toolkit"
BUNDLE_ID="com.majesticworld.l2toolkit"
VERSION="3.0.0"
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

# Remove bundle anterior se existir
rm -rf "$BUNDLE_DIR"

# Cria estrutura do bundle
mkdir -p "$BUNDLE_DIR/Contents/MacOS"
mkdir -p "$BUNDLE_DIR/Contents/Resources"

# Copia o binário AOT e garante permissão de execução
cp "$PUBLISH_DIR/$APP_NAME" "$BUNDLE_DIR/Contents/MacOS/$APP_NAME"
chmod +x "$BUNDLE_DIR/Contents/MacOS/$APP_NAME"

# Copia Info.plist
cp "Info.plist" "$BUNDLE_DIR/Contents/Info.plist"

# Copia ícone para Resources/
cp "images/favicon.icns" "$BUNDLE_DIR/Contents/Resources/favicon.icns"

echo ""
echo "✓ Bundle gerado em: $BUNDLE_DIR"
echo ""

# Abre a pasta de saída no Finder
open "$PUBLISH_DIR"
