#!/usr/bin/env bash
# ==============================================================================
# CherryHud Release Packaging Script
# Builds CherryHud.app, creates high-compression .dmg installer & .zip archive
# ==============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# 1. Determine Version
VERSION=""
ARCH="arm64"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --arch)
            ARCH="$2"
            shift 2
            ;;
        --version)
            VERSION="$2"
            shift 2
            ;;
        *)
            if [[ -z "$VERSION" ]]; then
                VERSION="$1"
            elif [[ -z "$ARCH" ]]; then
                ARCH="$1"
            fi
            shift
            ;;
    esac
done

if [[ -z "$VERSION" ]]; then
    # Extract version from CherryHud.csproj
    VERSION=$(grep -o '<Version>[^<]*</Version>' "$REPO_ROOT/CherryHud/CherryHud.csproj" | sed -e 's/<Version>//' -e 's/<\/Version>//')
    if [[ -z "$VERSION" ]]; then
        VERSION="1.0.1"
    fi
fi

echo "============================================================"
echo "🍒 Building CherryHud Release v${VERSION} (${ARCH})"
echo "============================================================"

# Paths
DIST_DIR="$REPO_ROOT/dist"
APP_NAME="CherryHud"
APP_BUNDLE="$DIST_DIR/${APP_NAME}.app"
PUBLISH_DIR="$DIST_DIR/publish_${ARCH}"
DMG_NAME="CherryHud-${VERSION}-macOS-${ARCH}.dmg"
DMG_PATH="$DIST_DIR/$DMG_NAME"
# Clean previous build artifacts
rm -rf "$PUBLISH_DIR" "$APP_BUNDLE" "$DMG_PATH" "$DIST_DIR"/*.zip "$DIST_DIR"/temp_rw.dmg
mkdir -p "$DIST_DIR"

# 2. Build Native Universal Library
echo "⚙️  Compiling native libfpsmonitor.dylib (universal arm64 + x86_64)..."
clang -dynamiclib -O3 -arch arm64 -arch x86_64 \
    -framework Foundation \
    -framework AppKit \
    -framework ScreenCaptureKit \
    -framework CoreMedia \
    -framework CoreGraphics \
    -framework IOKit \
    -framework ServiceManagement \
    "$REPO_ROOT/Native/fpsmonitor.m" \
    -o "$REPO_ROOT/Native/libfpsmonitor.dylib"

# 3. Publish .NET 10 project
echo "📦 Publishing .NET 10 project for osx-${ARCH}..."
DOTNET_BIN="${DOTNET_ROOT:-$HOME/.dotnet}/dotnet"
if ! command -v "$DOTNET_BIN" &>/dev/null; then
    if command -v dotnet &>/dev/null; then
        DOTNET_BIN="dotnet"
    elif [[ -x "/Applications/Rider.app/Contents/lib/ReSharperHost/macos-arm64/dotnet/dotnet" ]]; then
        DOTNET_BIN="/Applications/Rider.app/Contents/lib/ReSharperHost/macos-arm64/dotnet/dotnet"
    else
        DOTNET_BIN="dotnet"
    fi
fi

"$DOTNET_BIN" publish "$REPO_ROOT/CherryHud/CherryHud.csproj" \
    -c Release \
    -r "osx-${ARCH}" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:Version="$VERSION" \
    -p:AssemblyVersion="$VERSION" \
    -p:FileVersion="$VERSION" \
    -o "$PUBLISH_DIR"

# 4. Construct macOS .app Bundle
echo "🏗️  Assembling ${APP_NAME}.app bundle..."
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Copy published application files
cp -R "$PUBLISH_DIR/"* "$APP_BUNDLE/Contents/MacOS/"

# Ensure libfpsmonitor.dylib is inside MacOS folder
cp "$REPO_ROOT/Native/libfpsmonitor.dylib" "$APP_BUNDLE/Contents/MacOS/libfpsmonitor.dylib"

# Copy app icon
if [[ -f "$REPO_ROOT/CherryHud/Assets/CherryHud.icns" ]]; then
    cp "$REPO_ROOT/CherryHud/Assets/CherryHud.icns" "$APP_BUNDLE/Contents/Resources/CherryHud.icns"
fi

# Prepare Info.plist
INFO_PLIST_SRC="$REPO_ROOT/CherryHud/Info.plist"
if [[ -f "$INFO_PLIST_SRC" ]]; then
    sed -e "s/0\.9\.0/$VERSION/g" "$INFO_PLIST_SRC" > "$APP_BUNDLE/Contents/Info.plist"
else
    cat << EOF > "$APP_BUNDLE/Contents/Info.plist"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>CherryHud</string>
    <key>CFBundleIconFile</key>
    <string>CherryHud.icns</string>
    <key>CFBundleIdentifier</key>
    <string>com.matricard.cherryhud</string>
    <key>CFBundleName</key>
    <string>CherryHud</string>
    <key>CFBundleDisplayName</key>
    <string>CherryHud</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>${VERSION}</string>
    <key>CFBundleVersion</key>
    <string>${VERSION}</string>
    <key>LSMinimumSystemVersion</key>
    <string>13.0</string>
    <key>LSUIElement</key>
    <true/>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSSupportsAutomaticGraphicsSwitching</key>
    <true/>
    <key>NSScreenCaptureUsageDescription</key>
    <string>CherryHud requires screen capture access to measure real-time FPS and frametimes of active 3D games.</string>
</dict>
</plist>
EOF
fi

# Ensure executable permissions
chmod +x "$APP_BUNDLE/Contents/MacOS/CherryHud"

# Code sign for macOS Apple Silicon Gatekeeper
# Prefer stable Apple Development / Developer ID certificate from Keychain to preserve TCC permissions (Screen Recording, etc.) across updates!
SIGN_IDENTITY=$(security find-identity -v -p codesigning 2>/dev/null | grep -E "Apple Development|Developer ID Application" | head -n 1 | sed -e 's/.*"\(.*\)".*/\1/')
ENTITLEMENTS_FILE="$REPO_ROOT/CherryHud/entitlements.plist"

if [[ -n "$SIGN_IDENTITY" ]]; then
    echo "🔐 Signing nested libraries and ${APP_NAME}.app with persistent identity: ${SIGN_IDENTITY}..."
    find "$APP_BUNDLE" -name "*.dylib" -exec codesign --force -s "$SIGN_IDENTITY" {} \;
    codesign --force --deep --entitlements "$ENTITLEMENTS_FILE" -s "$SIGN_IDENTITY" "$APP_BUNDLE"
else
    echo "🔐 Signing ${APP_NAME}.app (ad-hoc fallback)..."
    codesign --force --deep -s - "$APP_BUNDLE"
fi

# 5. Create .dmg Installer
echo "💿 Building ${DMG_NAME} installer..."
DMG_STAGING="$DIST_DIR/dmg_staging"
rm -rf "$DMG_STAGING"
mkdir -p "$DMG_STAGING/.background"

# Generate background image
if command -v swift &>/dev/null && [[ -f "$SCRIPT_DIR/generate_dmg_background.swift" ]]; then
    echo "🎨 Rendering custom dark installer background..."
    swift "$SCRIPT_DIR/generate_dmg_background.swift" "$DMG_STAGING/.background/background.png" "$REPO_ROOT/CherryHud/Assets/logo.png" || true
fi

# Copy app bundle & Applications symlink
cp -R "$APP_BUNDLE" "$DMG_STAGING/"
ln -s /Applications "$DMG_STAGING/Applications"

# Set volume icon if available
if [[ -f "$REPO_ROOT/CherryHud/Assets/CherryHud.icns" ]]; then
    cp "$REPO_ROOT/CherryHud/Assets/CherryHud.icns" "$DMG_STAGING/.VolumeIcon.icns"
fi

# Calculate DMG size with buffer
STAGING_SIZE_MB=$(du -sm "$DMG_STAGING" | awk '{print int($1 * 1.3) + 25}')
DMG_TMP="$DIST_DIR/temp_rw.dmg"
rm -f "$DMG_TMP"

# Unmount previous volume if mounted
while mount | grep -q "/Volumes/${APP_NAME}"; do
    VOL=$(mount | grep "/Volumes/${APP_NAME}" | head -n1 | awk '{print $3}')
    hdiutil detach "$VOL" -force 2>/dev/null || break
done

hdiutil create -size "${STAGING_SIZE_MB}m" -fs HFS+ -volname "${APP_NAME}" -ov "$DMG_TMP"

ATTACH_OUTPUT=$(hdiutil attach -readwrite -noverify -noautoopen "$DMG_TMP")
MOUNT_DIR=$(echo "$ATTACH_OUTPUT" | grep "/Volumes/" | tail -n1 | awk '{$1=$2=""; print $0}' | sed -e 's/^[[:space:]]*//')

if [[ -n "$MOUNT_DIR" && -d "$MOUNT_DIR" ]]; then
    echo "Mounted staging image at: $MOUNT_DIR"

    # Copy files to mounted volume
    cp -R "$APP_BUNDLE" "$MOUNT_DIR/"
    ln -s /Applications "$MOUNT_DIR/Applications"
    mkdir -p "$MOUNT_DIR/.background"
    if [[ -f "$DMG_STAGING/.background/background.png" ]]; then
        cp "$DMG_STAGING/.background/background.png" "$MOUNT_DIR/.background/background.png"
    fi

    # Set custom volume icon
    if [[ -f "$REPO_ROOT/CherryHud/Assets/CherryHud.icns" ]]; then
        cp "$REPO_ROOT/CherryHud/Assets/CherryHud.icns" "$MOUNT_DIR/.VolumeIcon.icns"
        if command -v SetFile &>/dev/null; then
            SetFile -a C "$MOUNT_DIR" || true
        fi
    fi

    sleep 2

    # Configure Finder layout via AppleScript (if GUI session available)
    osascript << APPLESCRIPT || echo "ℹ️  Headless mode: Finder layout skipped."
tell application "Finder"
    set targetFolder to (POSIX file "$MOUNT_DIR" as alias)
    open targetFolder
    set current view of container window of targetFolder to icon view
    set toolbar visible of container window of targetFolder to false
    set statusbar visible of container window of targetFolder to false
    set the bounds of container window of targetFolder to {350, 150, 990, 570}
    set theViewOptions to the icon view options of container window of targetFolder
    set icon size of theViewOptions to 110
    set arrangement of theViewOptions to not arranged
    try
        set background picture of theViewOptions to file ".background:background.png"
    end try
    set position of item "${APP_NAME}.app" of container window of targetFolder to {160, 195}
    set position of item "Applications" of container window of targetFolder to {480, 195}
    update targetFolder without registering applications
    delay 1
    close container window of targetFolder
end tell
APPLESCRIPT

    sync
    hdiutil detach "$MOUNT_DIR" -force || true
fi

# Convert to final compressed read-only DMG
echo "⚡ Compressing read-only disk image (UDZO)..."
hdiutil convert "$DMG_TMP" -format UDZO -imagekey zlib-level=9 -ov -o "$DMG_PATH"

rm -f "$DMG_TMP"

if [[ -n "$SIGN_IDENTITY" ]]; then
    echo "🔐 Signing ${DMG_NAME} installer..."
    codesign --force -s "$SIGN_IDENTITY" "$DMG_PATH" || true
fi

# Cleanup everything in dist except the final target .dmg
echo "🧹 Cleaning up intermediate files in $DIST_DIR (keeping only $DMG_NAME)..."
find "$DIST_DIR" -mindepth 1 ! -name "$DMG_NAME" -exec rm -rf {} +

echo ""
echo "============================================================"
echo "✅ Build Completed Successfully!"
echo "============================================================"
echo "Installer in $DIST_DIR:"
ls -lh "$DIST_DIR"
echo ""
echo "DMG Installer: $DMG_PATH"
echo "============================================================"
